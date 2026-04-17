using FluentAssertions;
using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Domain.Tests.Entities;

public sealed class DeviceControlAuditTests
{
    [Fact]
    public void Value_bounds_enforced()
    {
        var dev = new DeviceId("d1");
        var act = () => new DeviceControlAudit(dev, "set_pwm", 101, 0, "req-1");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Accepts_valid_pwm_range()
    {
        var dev = new DeviceId("d1");
        var row = new DeviceControlAudit(dev, "set_pwm", 65, 42598, "req-unique-1");

        row.ValuePercent.Should().Be(65);
        row.Value16Bit.Should().Be(42598);
        row.Accepted.Should().BeTrue();
    }
}
