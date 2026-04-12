namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>遙測列表單筆（對齊列表 API）。</summary>
public sealed record TelemetryListItemDto(
    long Id,
    string DeviceId,
    string SiteId,
    DateTime DeviceTimeUtc,
    DateTime ServerTimeUtc,
    bool IsSyncBack,
    double? TemperatureC,
    double? HumidityPct,
    double? Lux,
    double? Co2Ppm,
    double? TemperatureCScd41,
    double? HumidityPctScd41,
    bool? PirActive,
    double? PressureHpa,
    double? GasResistanceOhm,
    double? AccelX,
    double? AccelY,
    double? AccelZ,
    double? GyroX,
    double? GyroY,
    double? GyroZ,
    int? RssiDbm);
