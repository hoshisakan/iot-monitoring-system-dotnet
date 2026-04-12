namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

/// <summary>對應 v5 §A.3 <c>device_ui_events</c>（僅 Infrastructure 對映用）。</summary>
public sealed class DeviceUiEventRecord
{
    public long EventId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public DateTime DeviceTimeUtc { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EventValue { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string SiteId { get; set; } = string.Empty;

    public string? PayloadJson { get; set; }

    public DateTime IngestedAtUtc { get; set; }
}
