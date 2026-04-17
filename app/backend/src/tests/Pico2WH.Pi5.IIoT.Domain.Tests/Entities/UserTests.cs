using FluentAssertions;
using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Domain.Tests.Entities;

public sealed class UserTests
{
    [Fact]
    public void Create_sets_identity_and_scope()
    {
        var u = new User("admin", "hash", UserRole.Admin, "site-a");

        u.Id.Should().NotBeEmpty();
        u.Username.Should().Be("admin");
        u.PasswordHash.Should().Be("hash");
        u.Role.Should().Be(UserRole.Admin);
        u.TenantScope.Should().Be("site-a");
        u.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Empty_username_throws()
    {
        var act = () => new User(" ", "hash", UserRole.Customer, "site");
        act.Should().Throw<DomainException>();
    }
}
