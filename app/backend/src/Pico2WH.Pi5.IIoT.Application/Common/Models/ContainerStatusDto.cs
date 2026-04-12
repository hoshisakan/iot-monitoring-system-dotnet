namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>容器狀態（對齊 <c>/api/v1/system/status</c> 所需欄位）。</summary>
public sealed record ContainerStatusDto(
    string ContainerId,
    string Name,
    string Status,
    long? UptimeSec,
    string? Ip,
    string? HealthStatus);
