using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.Common;

namespace Pico2WH.Pi5.IIoT.Application.Features.Logs.Queries.ListLogs;

public sealed record ListLogsQuery(LogQueryFilter Filter, int Page, int PageSize) : IRequest<PagedResult<LogListItemDto>>;
