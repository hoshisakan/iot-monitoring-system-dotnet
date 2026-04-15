namespace Pico2WH.Pi5.IIoT.Application.Common.Models;

/// <summary>系統狀態查詢結果，包含容器清單與來源可用性警示。</summary>
public sealed record SystemStatusResultDto(
    IReadOnlyList<ContainerStatusDto> Items,
    string? WarningCode,
    string? WarningMessage);
