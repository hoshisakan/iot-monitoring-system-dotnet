namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>UI 事件列表單筆（對齊 <c>device_ui_events</c> 查詢）。</summary>
public sealed record UiEventListItemDto(
    long EventId,
    string DeviceId,
    DateTime DeviceTimeUtc,
    string EventType,
    string EventValue,
    string Channel,
    string SiteId);
