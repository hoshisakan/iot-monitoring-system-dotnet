using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.SeriesTelemetry;

public sealed class SeriesTelemetryQueryValidator : AbstractValidator<SeriesTelemetryQuery>
{
    public SeriesTelemetryQueryValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Metrics).NotEmpty();
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
        RuleFor(x => x.MaxPoints).InclusiveBetween(10, 50_000).When(x => x.MaxPoints.HasValue);
    }
}
