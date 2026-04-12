using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Application.Features.UiEvents.Queries.ListUiEvents;

public sealed class ListUiEventsQueryValidator : AbstractValidator<ListUiEventsQuery>
{
    public ListUiEventsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x).Must(q =>
            !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.ToUtc > q.FromUtc)
            .WithMessage("ToUtc 必須大於 FromUtc。");
    }
}
