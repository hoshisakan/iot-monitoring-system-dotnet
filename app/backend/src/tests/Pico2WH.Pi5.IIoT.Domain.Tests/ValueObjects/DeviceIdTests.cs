using FluentAssertions;
using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Domain.Tests.ValueObjects;

public sealed class DeviceIdTests
{
    [Fact]
    public void Valid_id_trims_and_preserves_value()
    {
        var id = new DeviceId("  pico2wh-001  ");
        id.Value.Should().Be("pico2wh-001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_throws_domain_exception(string? value)
    {
        var act = () => new DeviceId(value!);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Too_long_throws_domain_exception()
    {
        var act = () => new DeviceId(new string('x', 65));
        act.Should().Throw<DomainException>();
    }
}
