using MediatR;

namespace Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Unit>;
