using AutoMapper;
using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.Repositories;

namespace Pico2WH.Pi5.IIoT.Application.Features.Logs.Queries.ListLogs;

public sealed class ListLogsQueryHandler : IRequestHandler<ListLogsQuery, PagedResult<LogListItemDto>>
{
    private readonly ILogQueryRepository _logs;
    private readonly IMapper _mapper;

    public ListLogsQueryHandler(ILogQueryRepository logs, IMapper mapper)
    {
        _logs = logs;
        _mapper = mapper;
    }

    public async Task<PagedResult<LogListItemDto>> Handle(ListLogsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _logs
            .QueryAsync(request.Filter, request.Page, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var dtos = _mapper.Map<IReadOnlyList<LogListItemDto>>(items);

        return new PagedResult<LogListItemDto>
        {
            Items = dtos,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }
}
