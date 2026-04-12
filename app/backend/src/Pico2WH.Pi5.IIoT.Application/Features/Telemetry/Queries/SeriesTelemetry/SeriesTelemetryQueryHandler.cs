using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.SeriesTelemetry;

public sealed class SeriesTelemetryQueryHandler : IRequestHandler<SeriesTelemetryQuery, SeriesTelemetryResult>
{
    private readonly ITelemetrySeriesQuery _series;

    public SeriesTelemetryQueryHandler(ITelemetrySeriesQuery series)
    {
        _series = series;
    }

    public Task<SeriesTelemetryResult> Handle(SeriesTelemetryQuery request, CancellationToken cancellationToken) =>
        _series.QueryAsync(
            request.DeviceId,
            request.Metrics,
            request.FromUtc,
            request.ToUtc,
            request.MaxPoints,
            cancellationToken);
}
