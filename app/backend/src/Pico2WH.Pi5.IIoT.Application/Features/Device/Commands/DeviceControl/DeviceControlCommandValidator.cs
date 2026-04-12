using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Application.Features.Device.Commands.DeviceControl;

public sealed class DeviceControlCommandValidator : AbstractValidator<DeviceControlCommand>
{
    public DeviceControlCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Command).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ValuePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.Value16Bit).InclusiveBetween(0, 65535);
        RuleFor(x => x.RequestId).NotEmpty().MaximumLength(128);
    }
}
