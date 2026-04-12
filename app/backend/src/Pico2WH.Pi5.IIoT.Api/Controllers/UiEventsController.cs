using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Features.UiEvents.Queries.ListUiEvents;

namespace Pico2WH.Pi5.IIoT.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ui-events")]
public sealed class UiEventsController : ControllerBase
{
    private readonly ISender _mediator;

    public UiEventsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UiEventListItemDto>>> List(
        [FromQuery] string device_id,
        [FromQuery] string? site_id,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(device_id))
            return BadRequest(new { error = new { code = "MISSING_DEVICE_ID" } });

        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        if (!string.IsNullOrWhiteSpace(from) &&
            DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var f))
            fromUtc = f.ToUniversalTime();

        if (!string.IsNullOrWhiteSpace(to) &&
            DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
            toUtc = t.ToUniversalTime();

        var result = await _mediator
            .Send(new ListUiEventsQuery(device_id, site_id, fromUtc, toUtc, page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}
