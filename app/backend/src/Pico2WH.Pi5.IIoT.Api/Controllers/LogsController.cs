using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Features.Logs.Queries.ListLogs;
using Pico2WH.Pi5.IIoT.Domain.Common;

namespace Pico2WH.Pi5.IIoT.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/logs")]
public sealed class LogsController : ControllerBase
{
    private static readonly HashSet<string> AllowedChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "telemetry", "ui-events", "status"
    };

    private static readonly HashSet<string> AllowedLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "debug", "info", "warn", "error"
    };

    private readonly ISender _mediator;

    public LogsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LogListItemDto>>> List(
        [FromQuery] string? device_id,
        [FromQuery] string? channel,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? level,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(channel) && !AllowedChannels.Contains(channel))
            return BadRequest(new { error = new { code = "INVALID_CHANNEL", allowed_values = AllowedChannels.ToArray() } });

        if (!string.IsNullOrWhiteSpace(level) && !AllowedLevels.Contains(level))
            return BadRequest(new { error = new { code = "INVALID_LEVEL", allowed_values = AllowedLevels.ToArray() } });

        DateTime? fromUtc = null;
        DateTime? toUtc = null;

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var f))
                return BadRequest(new { error = new { code = "INVALID_FROM" } });
            fromUtc = f.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(f, DateTimeKind.Utc)
                : f.ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
                return BadRequest(new { error = new { code = "INVALID_TO" } });
            toUtc = t.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(t, DateTimeKind.Utc)
                : t.ToUniversalTime();
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            return BadRequest(new { error = new { code = "INVALID_RANGE" } });

        var filter = new LogQueryFilter(fromUtc, toUtc, device_id, channel, level);
        var result = await _mediator.Send(new ListLogsQuery(filter, page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}
