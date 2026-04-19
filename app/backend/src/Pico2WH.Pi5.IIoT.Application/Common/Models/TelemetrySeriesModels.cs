namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>對齊 <c>GET /api/v1/telemetry/series</c> 與規格書 §6.0.4 metadata。</summary>
public sealed record SeriesTelemetryResult(
    string DeviceId,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<SeriesMetricDto> Series,
    bool Downsampled,
    int SourcePoints,
    int ReturnedPoints,
    long BucketWidthMs);

public sealed record SeriesMetricDto(
    string Metric,
    string? Unit,
    IReadOnlyList<SeriesPointDto> Points);

public sealed record SeriesPointDto(DateTime T, object? V);
