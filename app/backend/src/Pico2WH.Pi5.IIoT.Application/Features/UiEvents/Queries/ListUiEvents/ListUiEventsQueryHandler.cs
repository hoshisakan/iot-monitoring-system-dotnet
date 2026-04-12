using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.UiEvents.Queries.ListUiEvents;

public sealed class ListUiEventsQueryHandler : IRequestHandler<ListUiEventsQuery, PagedResult<UiEventListItemDto>>
{
    private readonly IUiEventsQuery _uiEvents;

    public ListUiEventsQueryHandler(IUiEventsQuery uiEvents)
    {
        _uiEvents = uiEvents;
    }

    public Task<PagedResult<UiEventListItemDto>> Handle(ListUiEventsQuery request, CancellationToken cancellationToken) =>
        _uiEvents.QueryAsync(
            request.DeviceId,
            request.SiteId,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize,
            cancellationToken);
}
