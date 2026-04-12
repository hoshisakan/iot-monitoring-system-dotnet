using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using RefreshTokenEntity = Pico2WH.Pi5.IIoT.Domain.Entities.RefreshToken;

namespace Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtService _jwt;

    public RefreshTokenCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IJwtService jwt)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _jwt = jwt;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var existing = await _refreshTokens.GetByTokenHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (existing is null || existing.IsRevoked || existing.ExpiresAtUtc <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh Token 無效或已過期。");

        var user = await _users.GetByIdAsync(existing.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("使用者不存在或已停用。");

        existing.Revoke(DateTime.UtcNow, "rotation");
        await _refreshTokens.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        var access = _jwt.CreateAccessToken(user);
        var plainRefresh = _jwt.GenerateRefreshTokenPlainText();
        var newHash = _jwt.HashRefreshToken(plainRefresh);
        var newToken = new RefreshTokenEntity(user.Id, newHash, DateTime.UtcNow.Add(_jwt.RefreshTokenLifetime), DateTime.UtcNow);
        await _refreshTokens.AddAsync(newToken, cancellationToken).ConfigureAwait(false);

        var expiresIn = (int)_jwt.AccessTokenLifetime.TotalSeconds;
        return new RefreshTokenResult(access, plainRefresh, expiresIn);
    }
}
