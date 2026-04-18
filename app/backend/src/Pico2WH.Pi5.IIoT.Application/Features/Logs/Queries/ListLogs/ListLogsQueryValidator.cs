using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Application.Features.Logs.Queries.ListLogs;

public sealed class ListLogsQueryValidator : AbstractValidator<ListLogsQuery>
{
    public ListLogsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
    }
}
