using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.ListTelemetry;

public sealed class ListTelemetryQueryValidator : AbstractValidator<ListTelemetryQuery>
{
    public ListTelemetryQueryValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
    }
}
