using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Queries;

/// <summary>
/// 遙測時序：優先於 PostgreSQL 以 <c>date_bin</c> 分桶聚合，避免長區間全量載入記憶體。
/// 對齊規格書 §6.0.4（<c>max_points</c>、metadata、時間桶 avg／<c>pir_active</c> 用 bool_or）。
/// </summary>
public sealed class TelemetrySeriesDapperQueryService : ITelemetrySeriesQuery
{
    private static readonly Regex SafeIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private sealed record MetricSpec(string ColumnName, string DbColumn, string? Unit, bool IsBoolean);

    private static readonly IReadOnlyDictionary<string, MetricSpec> AllowedMetrics =
        new Dictionary<string, MetricSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["temperature_c"] = new("TemperatureC", "temperature_c", "C", false),
            ["humidity_pct"] = new("HumidityPct", "humidity_pct", "%", false),
            ["lux"] = new("Lux", "lux", "lx", false),
            ["co2_ppm"] = new("Co2Ppm", "co2_ppm", "ppm", false),
            ["temperature_c_scd41"] = new("TemperatureCScd41", "temperature_c_scd41", "C", false),
            ["humidity_pct_scd41"] = new("HumidityPctScd41", "humidity_pct_scd41", "%", false),
            ["pir_active"] = new("PirActive", "pir_active", null, true),
            ["pressure_hpa"] = new("PressureHpa", "pressure_hpa", "hPa", false),
            ["gas_resistance_ohm"] = new("GasResistanceOhm", "gas_resistance_ohm", "ohm", false),
            ["accel_x"] = new("AccelX", "accel_x", "g", false),
            ["accel_y"] = new("AccelY", "accel_y", "g", false),
            ["accel_z"] = new("AccelZ", "accel_z", "g", false),
            ["gyro_x"] = new("GyroX", "gyro_x", "dps", false),
            ["gyro_y"] = new("GyroY", "gyro_y", "dps", false),
            ["gyro_z"] = new("GyroZ", "gyro_z", "dps", false),
            ["rssi"] = new("Rssi", "rssi", "dBm", false),
        };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _schema;

    public TelemetrySeriesDapperQueryService(
        IDbConnectionFactory connectionFactory,
        IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionFactory = connectionFactory;
        var schema = (databaseOptions.Value.DefaultSchema ?? "public").Trim();
        if (!SafeIdentifier.IsMatch(schema))
            throw new InvalidOperationException($"Invalid database schema identifier: '{schema}'.");
        _schema = schema;
    }

    public async Task<SeriesTelemetryResult> QueryAsync(
        string deviceId,
        IReadOnlyList<string> metrics,
        DateTime fromUtc,
        DateTime toUtc,
        int? maxPoints,
        CancellationToken cancellationToken = default)
    {
        var targetPoints = Math.Clamp(maxPoints ?? 500, 10, 5000);
        var requested = metrics
            .Select(m => m.Trim())
            .Where(m => AllowedMetrics.ContainsKey(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count == 0)
        {
            return new SeriesTelemetryResult(
                deviceId, fromUtc, toUtc, Array.Empty<SeriesMetricDto>(),
                false, 0, 0, 0);
        }

        var rangeMs = (long)Math.Ceiling((toUtc - fromUtc).TotalMilliseconds);
        var bucketWidthMs = rangeMs > 0
            ? (long)Math.Ceiling(rangeMs / (double)targetPoints)
            : 0L;

        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var countSql = $"""
            SELECT COUNT(*)::bigint
            FROM "{_schema}"."telemetry_records"
            WHERE device_id = @DeviceId
              AND device_time >= @FromUtc
              AND device_time <= @ToUtc
            """;

        var sourcePoints = Convert.ToInt64(
            await conn.ExecuteScalarAsync(
                    new CommandDefinition(
                        countSql,
                        new { DeviceId = deviceId, FromUtc = fromUtc, ToUtc = toUtc },
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false),
            CultureInfo.InvariantCulture);

        var sourcePointsInt = sourcePoints > int.MaxValue ? int.MaxValue : (int)sourcePoints;

        if (sourcePoints == 0)
        {
            return new SeriesTelemetryResult(
                deviceId, fromUtc, toUtc, Array.Empty<SeriesMetricDto>(),
                false, 0, 0, 0);
        }

        IReadOnlyList<SeriesMetricDto> series;
        bool downsampled;
        int returnedPoints;

        if (sourcePoints <= targetPoints)
        {
            series = await LoadRawSeriesAsync(
                    conn, deviceId, fromUtc, toUtc, requested, cancellationToken)
                .ConfigureAwait(false);
            downsampled = false;
            returnedPoints = sourcePointsInt;
        }
        else
        {
            series = await LoadBucketedSeriesAsync(
                    conn, deviceId, fromUtc, toUtc, requested, targetPoints, cancellationToken)
                .ConfigureAwait(false);
            downsampled = true;
            returnedPoints = series.Count == 0 ? 0 : series.Max(s => s.Points.Count);
        }

        return new SeriesTelemetryResult(
            deviceId,
            fromUtc,
            toUtc,
            series,
            downsampled,
            sourcePointsInt,
            returnedPoints,
            bucketWidthMs);
    }

    private async Task<IReadOnlyList<SeriesMetricDto>> LoadRawSeriesAsync(
        System.Data.Common.DbConnection conn,
        string deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<string> requested,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.Append("SELECT device_time AS \"DeviceTimeUtc\"");
        foreach (var m in requested)
        {
            var spec = AllowedMetrics[m];
            sb.Append(CultureInfo.InvariantCulture, $", {spec.DbColumn} AS \"{spec.ColumnName}\"");
        }

        sb.Append(CultureInfo.InvariantCulture, $"""
            
            FROM "{_schema}"."telemetry_records"
            WHERE device_id = @DeviceId
              AND device_time >= @FromUtc
              AND device_time <= @ToUtc
            ORDER BY device_time ASC
            """);

        var rows = (await conn.QueryAsync<TelemetrySeriesRow>(
                new CommandDefinition(
                    sb.ToString(),
                    new { DeviceId = deviceId, FromUtc = fromUtc, ToUtc = toUtc },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false))
            .ToList();

        var series = new List<SeriesMetricDto>(requested.Count);
        foreach (var metric in requested)
        {
            var spec = AllowedMetrics[metric];
            var points = new List<SeriesPointDto>();
            foreach (var r in rows)
            {
                if (!TryGetMetricValue(r, spec.ColumnName, out var value))
                    continue;
                points.Add(new SeriesPointDto(r.DeviceTimeUtc, value));
            }

            if (points.Count > 0)
                series.Add(new SeriesMetricDto(metric, spec.Unit, points));
        }

        return series;
    }

    private async Task<IReadOnlyList<SeriesMetricDto>> LoadBucketedSeriesAsync(
        System.Data.Common.DbConnection conn,
        string deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<string> requested,
        int targetPoints,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.Append("""
            SELECT
              date_bin(
                ((@ToUtc::timestamptz - @FromUtc::timestamptz) / @TargetPoints::double precision),
                device_time::timestamptz,
                @FromUtc::timestamptz
              ) AS "DeviceTimeUtc"
            """);

        foreach (var m in requested)
        {
            var spec = AllowedMetrics[m];
            if (spec.IsBoolean)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $", bool_or({spec.DbColumn}) AS \"{spec.ColumnName}\"");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $", avg({spec.DbColumn}) AS \"{spec.ColumnName}\"");
            }
        }

        sb.Append(CultureInfo.InvariantCulture, $"""

            FROM "{_schema}"."telemetry_records"
            WHERE device_id = @DeviceId
              AND device_time >= @FromUtc
              AND device_time <= @ToUtc
            GROUP BY 1
            ORDER BY 1 ASC
            """);

        var rows = (await conn.QueryAsync<TelemetrySeriesRow>(
                new CommandDefinition(
                    sb.ToString(),
                    new { DeviceId = deviceId, FromUtc = fromUtc, ToUtc = toUtc, TargetPoints = targetPoints },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false))
            .ToList();

        var series = new List<SeriesMetricDto>(requested.Count);
        foreach (var metric in requested)
        {
            var spec = AllowedMetrics[metric];
            var points = new List<SeriesPointDto>();
            foreach (var r in rows)
            {
                if (!TryGetMetricValue(r, spec.ColumnName, out var value))
                    continue;
                points.Add(new SeriesPointDto(r.DeviceTimeUtc, value));
            }

            if (points.Count > 0)
                series.Add(new SeriesMetricDto(metric, spec.Unit, points));
        }

        return series;
    }

    private static bool TryGetMetricValue(TelemetrySeriesRow row, string metricColumn, out object? value)
    {
        value = metricColumn switch
        {
            "TemperatureC" => row.TemperatureC,
            "HumidityPct" => row.HumidityPct,
            "Lux" => row.Lux,
            "Co2Ppm" => row.Co2Ppm,
            "TemperatureCScd41" => row.TemperatureCScd41,
            "HumidityPctScd41" => row.HumidityPctScd41,
            "PirActive" => row.PirActive.HasValue ? row.PirActive.Value : null,
            "PressureHpa" => row.PressureHpa,
            "GasResistanceOhm" => row.GasResistanceOhm,
            "AccelX" => row.AccelX,
            "AccelY" => row.AccelY,
            "AccelZ" => row.AccelZ,
            "GyroX" => row.GyroX,
            "GyroY" => row.GyroY,
            "GyroZ" => row.GyroZ,
            "Rssi" => row.Rssi,
            _ => null
        };
        return value is not null;
    }

    private sealed class TelemetrySeriesRow
    {
        public DateTime DeviceTimeUtc { get; init; }
        public double? TemperatureC { get; init; }
        public double? HumidityPct { get; init; }
        public double? Lux { get; init; }
        public double? Co2Ppm { get; init; }
        public double? TemperatureCScd41 { get; init; }
        public double? HumidityPctScd41 { get; init; }
        public bool? PirActive { get; init; }
        public double? PressureHpa { get; init; }
        public double? GasResistanceOhm { get; init; }
        public double? AccelX { get; init; }
        public double? AccelY { get; init; }
        public double? AccelZ { get; init; }
        public double? GyroX { get; init; }
        public double? GyroY { get; init; }
        public double? GyroZ { get; init; }
        public double? Rssi { get; init; }
    }
}
