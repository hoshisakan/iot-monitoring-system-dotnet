using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Repositories;

namespace Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtService _jwt;

    public LogoutCommandHandler(IRefreshTokenRepository refreshTokens, IJwtService jwt)
    {
        _refreshTokens = refreshTokens;
        _jwt = jwt;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var existing = await _refreshTokens.GetByTokenHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (existing is null || existing.IsRevoked)
            return Unit.Value;

        existing.Revoke(DateTime.UtcNow, "logout");
        await _refreshTokens.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
