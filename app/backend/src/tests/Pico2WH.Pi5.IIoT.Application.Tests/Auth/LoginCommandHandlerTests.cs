using FluentAssertions;
using Moq;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Features.Auth.Commands.Login;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;

namespace Pico2WH.Pi5.IIoT.Application.Tests.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Valid_credentials_returns_tokens()
    {
        var user = new User("alice", "stored-hash", UserRole.Admin, "site-1");

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var ph = new Mock<IPasswordHasher>();
        ph.Setup(p => p.Verify("secret", "stored-hash")).Returns(true);

        var jwt = new Mock<IJwtService>();
        jwt.SetupGet(j => j.AccessTokenLifetime).Returns(TimeSpan.FromHours(1));
        jwt.SetupGet(j => j.RefreshTokenLifetime).Returns(TimeSpan.FromDays(7));
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<User>())).Returns("access-jwt");
        jwt.Setup(j => j.GenerateRefreshTokenPlainText()).Returns("refresh-plain");
        jwt.Setup(j => j.HashRefreshToken("refresh-plain")).Returns("refresh-hash");

        var refreshRepo = new Mock<IRefreshTokenRepository>();
        refreshRepo
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new LoginCommandHandler(users.Object, refreshRepo.Object, ph.Object, jwt.Object);

        var result = await handler.Handle(new LoginCommand("alice", "secret"), CancellationToken.None);

        result.AccessToken.Should().Be("access-jwt");
        result.RefreshToken.Should().Be("refresh-plain");
        result.ExpiresIn.Should().Be(3600);
        refreshRepo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Wrong_password_throws()
    {
        var user = new User("alice", "stored-hash", UserRole.Admin, "site-1");
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var ph = new Mock<IPasswordHasher>();
        ph.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var jwt = new Mock<IJwtService>();
        var refreshRepo = new Mock<IRefreshTokenRepository>();
        var handler = new LoginCommandHandler(users.Object, refreshRepo.Object, ph.Object, jwt.Object);

        var act = async () => await handler.Handle(new LoginCommand("alice", "bad"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
