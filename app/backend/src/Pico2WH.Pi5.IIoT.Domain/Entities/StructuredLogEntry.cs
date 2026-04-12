namespace Pico2WH.Pi5.IIoT.Domain.Entities;

/// <summary>結構化日誌一筆（對齊 <c>app_logs</c>／KV 查詢結果；由 Infrastructure 填入）。</summary>
public sealed class StructuredLogEntry
{
    public required long Id { get; init; }

    public string? DeviceId { get; init; }

    public required string Channel { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }

    public string? PayloadJson { get; init; }

    public string? SourceIp { get; init; }

    public DateTime? DeviceTimeUtc { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
