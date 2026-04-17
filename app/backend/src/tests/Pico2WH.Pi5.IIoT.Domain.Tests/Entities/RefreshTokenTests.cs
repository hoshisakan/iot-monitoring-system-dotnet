using FluentAssertions;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Domain.Tests.Entities;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Revoke_is_idempotent()
    {
        var uid = Guid.NewGuid();
        var t = new RefreshToken(uid, "hash", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        t.Revoke(DateTime.UtcNow, "logout");
        t.IsRevoked.Should().BeTrue();

        t.Revoke(DateTime.UtcNow, "logout");
        t.RevokedReason.Should().Be("logout");
    }
}
