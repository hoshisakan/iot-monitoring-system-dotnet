using AutoMapper;
using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.ListTelemetry;

public sealed class ListTelemetryQueryHandler : IRequestHandler<ListTelemetryQuery, PagedResult<TelemetryListItemDto>>
{
    private readonly ITelemetryRepository _telemetry;
    private readonly IMapper _mapper;

    public ListTelemetryQueryHandler(ITelemetryRepository telemetry, IMapper mapper)
    {
        _telemetry = telemetry;
        _mapper = mapper;
    }

    public async Task<PagedResult<TelemetryListItemDto>> Handle(ListTelemetryQuery request, CancellationToken cancellationToken)
    {
        var id = new DeviceId(request.DeviceId);
        var items = await _telemetry
            .ListForDeviceAsync(id, request.FromUtc, request.ToUtc, request.Page, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var total = await _telemetry
            .CountForDeviceAsync(id, request.FromUtc, request.ToUtc, cancellationToken)
            .ConfigureAwait(false);

        var dtos = _mapper.Map<IReadOnlyList<TelemetryListItemDto>>(items);

        return new PagedResult<TelemetryListItemDto>
        {
            Items = dtos,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }
}
