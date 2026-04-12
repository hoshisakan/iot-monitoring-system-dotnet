namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

/// <summary>對應四層規格 <c>app_logs</c>（僅 Infrastructure 對映用）。</summary>
public sealed class AppLogRecord
{
    public long Id { get; set; }

    public string? DeviceId { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? PayloadJson { get; set; }

    public string? SourceIp { get; set; }

    public DateTime? DeviceTimeUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
