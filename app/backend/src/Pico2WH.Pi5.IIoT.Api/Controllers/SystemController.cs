using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Features.System.Queries.SystemStatus;

namespace Pico2WH.Pi5.IIoT.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    private readonly ISender _mediator;

    public SystemController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusResponse>> Status(
        [FromQuery(Name = "include_stopped")] bool includeStopped = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new SystemStatusQuery(includeStopped), cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(c => new SystemStatusItem(
            c.Name,
            c.ContainerId,
            c.Status,
            c.UptimeSec ?? 0,
            c.Ip,
            c.HealthStatus ?? "unknown")).ToList();

        return Ok(new SystemStatusResponse(DateTimeOffset.UtcNow, items, result.WarningCode, result.WarningMessage));
    }

    public sealed record SystemStatusResponse(
        DateTimeOffset HostTime,
        IReadOnlyList<SystemStatusItem> Items,
        string? WarningCode,
        string? WarningMessage);

    public sealed record SystemStatusItem(
        string ContainerName,
        string ContainerId,
        string Status,
        long UptimeSec,
        string? Ip,
        string Health);
}
