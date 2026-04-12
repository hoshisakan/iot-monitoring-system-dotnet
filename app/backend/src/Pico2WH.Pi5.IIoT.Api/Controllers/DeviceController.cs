using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pico2WH.Pi5.IIoT.Application.Features.Device.Commands.DeviceControl;

namespace Pico2WH.Pi5.IIoT.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/device")]
public sealed class DeviceController : ControllerBase
{
    private readonly ISender _mediator;

    public DeviceController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("control")]
    [ProducesResponseType(typeof(DeviceControlAcceptedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Control([FromBody] DeviceControlRequest body, CancellationToken cancellationToken)
    {
        if (!string.Equals(body.Command, "set_pwm", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = new { code = "INVALID_COMMAND", message = "目前僅支援 set_pwm。" } });

        if (body.Value is < 0 or > 100)
            return BadRequest(new { error = new { code = "INVALID_VALUE", message = "value 必須在 0～100。" } });

        var value16 = (int)Math.Round(body.Value / 100.0 * 65535);

        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var uid))
            userId = uid;

        var siteId = body.SiteId ?? User.FindFirstValue("tenant_scope") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(siteId))
            return BadRequest(new { error = new { code = "MISSING_SITE", message = "請提供 site_id 或於 JWT 含 tenant_scope。" } });

        var requestId = Request.Headers["X-Request-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        var result = await _mediator.Send(
            new DeviceControlCommand(siteId, body.DeviceId, "set_pwm", body.Value, value16, requestId, userId),
            cancellationToken).ConfigureAwait(false);

        return Accepted(new DeviceControlAcceptedResponse(
            "accepted",
            body.DeviceId,
            "set_pwm",
            body.Value));
    }

    public sealed record DeviceControlRequest(string DeviceId, string Command, int Value, string? SiteId);

    public sealed record DeviceControlAcceptedResponse(
        string Status,
        string DeviceId,
        string Command,
        int Value);
}
