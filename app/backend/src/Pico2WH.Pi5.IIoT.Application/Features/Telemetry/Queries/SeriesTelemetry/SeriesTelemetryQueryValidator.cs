using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.SeriesTelemetry;

public sealed class SeriesTelemetryQueryValidator : AbstractValidator<SeriesTelemetryQuery>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(90);

    public SeriesTelemetryQueryValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Metrics).NotEmpty();
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
        RuleFor(x => x.MaxPoints).InclusiveBetween(10, 5000).When(x => x.MaxPoints.HasValue);
        RuleFor(x => x)
            .Must(q => q.ToUtc - q.FromUtc <= MaxRange)
            .WithMessage("查詢時間區間不可超過 90 天，請縮小 from/to 或分段查詢。");
    }
}
