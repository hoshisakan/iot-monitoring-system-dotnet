using MediatR;

namespace Pico2WH.Pi5.IIoT.Application.Features.Device.Commands.DeviceControl;

public sealed record DeviceControlCommand(
    string SiteId,
    string DeviceId,
    string Command,
    int ValuePercent,
    int Value16Bit,
    string RequestId,
    Guid? RequestedByUserId) : IRequest<DeviceControlResult>;

public sealed record DeviceControlResult(bool Accepted, Guid AuditId);
