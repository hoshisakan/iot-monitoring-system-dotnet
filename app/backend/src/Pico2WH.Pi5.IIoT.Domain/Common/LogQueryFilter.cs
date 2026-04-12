namespace Pico2WH.Pi5.IIoT.Domain.Common;

/// <summary>結構化日誌查詢條件（對齊 <c>/api/v1/logs</c> 篩選語意）。</summary>
public sealed record LogQueryFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? DeviceId = null,
    string? Channel = null,
    string? Level = null);
