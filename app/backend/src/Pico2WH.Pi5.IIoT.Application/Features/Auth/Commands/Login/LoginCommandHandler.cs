using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using RefreshTokenEntity = Pico2WH.Pi5.IIoT.Domain.Entities.RefreshToken;

namespace Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwt;

    public LoginCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtService jwt)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByUsernameAsync(request.Username, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("帳號或密碼不正確。");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("帳號或密碼不正確。");

        var access = _jwt.CreateAccessToken(user);
        var plainRefresh = _jwt.GenerateRefreshTokenPlainText();
        var hash = _jwt.HashRefreshToken(plainRefresh);

        var refresh = new RefreshTokenEntity(user.Id, hash, DateTime.UtcNow.Add(_jwt.RefreshTokenLifetime), DateTime.UtcNow);
        await _refreshTokens.AddAsync(refresh, cancellationToken).ConfigureAwait(false);

        var expiresIn = (int)_jwt.AccessTokenLifetime.TotalSeconds;
        return new LoginResult(access, plainRefresh, expiresIn);
    }
}
