using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.UiEvents.Queries.ListUiEvents;

public sealed record ListUiEventsQuery(
    string? DeviceId,
    string? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize) : IRequest<PagedResult<UiEventListItemDto>>;
