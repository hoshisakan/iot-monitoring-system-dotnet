using Microsoft.EntityFrameworkCore;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Queries;

/// <summary>由 DB 讀取區間後於記憶體降採樣（後續可改為 SQL 分桶）。</summary>
public sealed class TelemetrySeriesQueryService : ITelemetrySeriesQuery
{
    private static readonly HashSet<string> AllowedMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "temperature_c",
        "humidity_pct",
        "lux",
        "co2_ppm",
        "temperature_c_scd41",
        "humidity_pct_scd41",
        "pir_active",
        "pressure_hpa",
        "gas_resistance_ohm",
        "accel_x",
        "accel_y",
        "accel_z",
        "gyro_x",
        "gyro_y",
        "gyro_z",
        "rssi"
    };

    private readonly ApplicationDbContext _db;

    public TelemetrySeriesQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SeriesTelemetryResult> QueryAsync(
        string deviceId,
        IReadOnlyList<string> metrics,
        DateTime fromUtc,
        DateTime toUtc,
        int? maxPoints,
        CancellationToken cancellationToken = default)
    {
        var id = new DeviceId(deviceId);
        var requested = metrics
            .Select(m => m.Trim())
            .Where(m => AllowedMetrics.Contains(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count == 0)
            return new SeriesTelemetryResult(deviceId, fromUtc, toUtc, Array.Empty<SeriesMetricDto>());

        var rows = await _db.TelemetryReadings
            .AsNoTracking()
            .Where(r => r.DeviceId == id && r.DeviceTimeUtc >= fromUtc && r.DeviceTimeUtc <= toUtc)
            .OrderBy(r => r.DeviceTimeUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var cap = maxPoints is > 0 and < 50_000 ? maxPoints.Value : 2000;

        var series = new List<SeriesMetricDto>();
        foreach (var metric in requested)
        {
            var points = new List<SeriesPointDto>();
            foreach (var r in rows)
            {
                if (!TryGetMetricPoint(r, metric, out var t, out var v))
                    continue;
                points.Add(new SeriesPointDto(t, v));
            }

            if (points.Count == 0)
                continue;

            points = Downsample(points, cap);
            series.Add(new SeriesMetricDto(
                Metric: metric,
                Unit: GetUnit(metric),
                Points: points));
        }

        return new SeriesTelemetryResult(deviceId, fromUtc, toUtc, series);
    }

    private static bool TryGetMetricPoint(TelemetryReading r, string metric, out DateTime t, out object? v)
    {
        t = r.DeviceTimeUtc;
        switch (metric.ToLowerInvariant())
        {
            case "temperature_c":
                if (r.TemperatureC is null) { v = null; return false; }
                v = r.TemperatureC.Value;
                return true;
            case "humidity_pct":
                if (r.HumidityPct is null) { v = null; return false; }
                v = r.HumidityPct.Value;
                return true;
            case "lux":
                if (r.Lux is null) { v = null; return false; }
                v = r.Lux.Value;
                return true;
            case "co2_ppm":
                if (r.Co2Ppm is null) { v = null; return false; }
                v = r.Co2Ppm.Value;
                return true;
            case "temperature_c_scd41":
                if (r.TemperatureCScd41 is null) { v = null; return false; }
                v = r.TemperatureCScd41.Value;
                return true;
            case "humidity_pct_scd41":
                if (r.HumidityPctScd41 is null) { v = null; return false; }
                v = r.HumidityPctScd41.Value;
                return true;
            case "pir_active":
                if (r.PirActive is null) { v = null; return false; }
                v = r.PirActive.Value;
                return true;
            case "pressure_hpa":
                if (r.PressureHpa is null) { v = null; return false; }
                v = r.PressureHpa.Value;
                return true;
            case "gas_resistance_ohm":
                if (r.GasResistanceOhm is null) { v = null; return false; }
                v = r.GasResistanceOhm.Value;
                return true;
            case "accel_x":
                if (r.AccelX is null) { v = null; return false; }
                v = r.AccelX.Value;
                return true;
            case "accel_y":
                if (r.AccelY is null) { v = null; return false; }
                v = r.AccelY.Value;
                return true;
            case "accel_z":
                if (r.AccelZ is null) { v = null; return false; }
                v = r.AccelZ.Value;
                return true;
            case "gyro_x":
                if (r.GyroX is null) { v = null; return false; }
                v = r.GyroX.Value;
                return true;
            case "gyro_y":
                if (r.GyroY is null) { v = null; return false; }
                v = r.GyroY.Value;
                return true;
            case "gyro_z":
                if (r.GyroZ is null) { v = null; return false; }
                v = r.GyroZ.Value;
                return true;
            case "rssi":
                if (r.RssiDbm is null) { v = null; return false; }
                v = r.RssiDbm.Value;
                return true;
            default:
                v = null;
                return false;
        }
    }

    private static string? GetUnit(string metric) =>
        metric.ToLowerInvariant() switch
        {
            "temperature_c" or "temperature_c_scd41" => "C",
            "humidity_pct" or "humidity_pct_scd41" => "%",
            "lux" => "lx",
            "co2_ppm" => "ppm",
            "pir_active" => null,
            "pressure_hpa" => "hPa",
            "gas_resistance_ohm" => "ohm",
            "accel_x" or "accel_y" or "accel_z" => "g",
            "gyro_x" or "gyro_y" or "gyro_z" => "dps",
            "rssi" => "dBm",
            _ => null
        };

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
}
