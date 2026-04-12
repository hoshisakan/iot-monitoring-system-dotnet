using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>UI 事件列表查詢（由 Infrastructure 對應表／檢視實作）。</summary>
public interface IUiEventsQuery
{
    Task<PagedResult<UiEventListItemDto>> QueryAsync(
        string? deviceId,
        string? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
