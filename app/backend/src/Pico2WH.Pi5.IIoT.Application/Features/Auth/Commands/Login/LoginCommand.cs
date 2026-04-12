using MediatR;

namespace Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

public sealed record LoginResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType = "Bearer");
