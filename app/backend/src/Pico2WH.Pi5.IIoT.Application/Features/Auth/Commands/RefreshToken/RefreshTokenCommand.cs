using MediatR;

namespace Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResult>;

public sealed record RefreshTokenResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType = "Bearer");
