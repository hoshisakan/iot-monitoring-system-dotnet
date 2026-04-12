namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>結構化日誌列表單筆。</summary>
public sealed record LogListItemDto(
    long Id,
    string? DeviceId,
    string Channel,
    string Level,
    string Message,
    string? PayloadJson,
    string? SourceIp,
    DateTime? DeviceTimeUtc,
    DateTime CreatedAtUtc);
