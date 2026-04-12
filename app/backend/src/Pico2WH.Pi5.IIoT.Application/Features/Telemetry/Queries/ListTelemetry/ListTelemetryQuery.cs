using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.ListTelemetry;

public sealed record ListTelemetryQuery(
    string DeviceId,
    DateTime FromUtc,
    DateTime ToUtc,
    int Page,
    int PageSize) : IRequest<PagedResult<TelemetryListItemDto>>;
