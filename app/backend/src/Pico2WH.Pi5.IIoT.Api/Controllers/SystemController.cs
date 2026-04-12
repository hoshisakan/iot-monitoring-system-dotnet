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
        var list = await _mediator.Send(new SystemStatusQuery(includeStopped), cancellationToken)
            .ConfigureAwait(false);

        var items = list.Select(c => new SystemStatusItem(
            c.Name,
            c.ContainerId,
            c.Status,
            c.Ip,
            c.HealthStatus ?? "unknown")).ToList();

        return Ok(new SystemStatusResponse(items));
    }

    public sealed record SystemStatusResponse(IReadOnlyList<SystemStatusItem> Items);

    public sealed record SystemStatusItem(
        string ContainerName,
        string ContainerId,
        string Status,
        string? Ip,
        string Health);
}
