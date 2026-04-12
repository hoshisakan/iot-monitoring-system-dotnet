using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.SeriesTelemetry;

public sealed record SeriesTelemetryQuery(
    string DeviceId,
    IReadOnlyList<string> Metrics,
    DateTime FromUtc,
    DateTime ToUtc,
    int? MaxPoints) : IRequest<SeriesTelemetryResult>;
