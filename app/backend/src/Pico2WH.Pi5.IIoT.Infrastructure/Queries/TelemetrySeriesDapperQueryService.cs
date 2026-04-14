using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Queries;

/// <summary>
/// Dapper implementation for telemetry series query.
/// Keeps API response contract unchanged while moving read path off EF change tracking.
/// </summary>
public sealed class TelemetrySeriesDapperQueryService : ITelemetrySeriesQuery
{
    private static readonly Regex SafeIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private sealed record MetricSpec(string ColumnName, string? Unit);

    private static readonly IReadOnlyDictionary<string, MetricSpec> AllowedMetrics =
        new Dictionary<string, MetricSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["temperature_c"] = new("TemperatureC", "C"),
            ["humidity_pct"] = new("HumidityPct", "%"),
            ["lux"] = new("Lux", "lx"),
            ["co2_ppm"] = new("Co2Ppm", "ppm"),
            ["temperature_c_scd41"] = new("TemperatureCScd41", "C"),
            ["humidity_pct_scd41"] = new("HumidityPctScd41", "%"),
            ["pir_active"] = new("PirActive", null),
            ["pressure_hpa"] = new("PressureHpa", "hPa"),
            ["gas_resistance_ohm"] = new("GasResistanceOhm", "ohm"),
            ["accel_x"] = new("AccelX", "g"),
            ["accel_y"] = new("AccelY", "g"),
            ["accel_z"] = new("AccelZ", "g"),
            ["gyro_x"] = new("GyroX", "dps"),
            ["gyro_y"] = new("GyroY", "dps"),
            ["gyro_z"] = new("GyroZ", "dps"),
            ["rssi"] = new("Rssi", "dBm")
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
        var requested = metrics
            .Select(m => m.Trim())
            .Where(m => AllowedMetrics.ContainsKey(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count == 0)
            return new SeriesTelemetryResult(deviceId, fromUtc, toUtc, Array.Empty<SeriesMetricDto>());

        var sql = $"""
            SELECT
                device_time AS DeviceTimeUtc,
                temperature_c AS TemperatureC,
                humidity_pct AS HumidityPct,
                lux AS Lux,
                co2_ppm AS Co2Ppm,
                temperature_c_scd41 AS TemperatureCScd41,
                humidity_pct_scd41 AS HumidityPctScd41,
                pir_active AS PirActive,
                pressure_hpa AS PressureHpa,
                gas_resistance_ohm AS GasResistanceOhm,
                accel_x AS AccelX,
                accel_y AS AccelY,
                accel_z AS AccelZ,
                gyro_x AS GyroX,
                gyro_y AS GyroY,
                gyro_z AS GyroZ,
                rssi AS Rssi
            FROM "{_schema}"."telemetry_records"
            WHERE device_id = @DeviceId
              AND device_time >= @FromUtc
              AND device_time <= @ToUtc
            ORDER BY device_time ASC
            """;

        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = (await conn.QueryAsync<TelemetrySeriesRow>(
                new CommandDefinition(
                    sql,
                    new { DeviceId = deviceId, FromUtc = fromUtc, ToUtc = toUtc },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false))
            .ToList();

        var cap = maxPoints is > 0 and < 50_000 ? maxPoints.Value : 2000;
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

            if (points.Count == 0)
                continue;

            points = Downsample(points, cap);
            series.Add(new SeriesMetricDto(metric, spec.Unit, points));
        }

        return new SeriesTelemetryResult(deviceId, fromUtc, toUtc, series);
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
            "PirActive" => row.PirActive,
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

    private static List<SeriesPointDto> Downsample(IReadOnlyList<SeriesPointDto> points, int max)
    {
        if (points.Count <= max)
            return points.ToList();

        var stride = (double)points.Count / max;
        var result = new List<SeriesPointDto>(max);
        for (var i = 0; i < max; i++)
        {
            var idx = (int)(i * stride);
            if (idx >= points.Count)
                idx = points.Count - 1;
            result.Add(points[idx]);
        }
        return result;
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
        public int? Rssi { get; init; }
    }
}
