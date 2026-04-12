using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Login;
using Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Logout;
using Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.RefreshToken;

namespace Pico2WH.Pi5.IIoT.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginCommand(body.Username, body.Password), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<RefreshTokenResult>> Refresh([FromBody] RefreshRequest body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(body.RefreshToken), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<LogoutResponse>> Logout([FromBody] LogoutRequest body, CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(body.RefreshToken), cancellationToken).ConfigureAwait(false);
        return Ok(new LogoutResponse("ok", "logout success"));
    }

    public sealed record LoginRequest(string Username, string Password);

    public sealed record RefreshRequest(string RefreshToken);

    public sealed record LogoutRequest(string RefreshToken);

    public sealed record LogoutResponse(string Status, string Message);
}
