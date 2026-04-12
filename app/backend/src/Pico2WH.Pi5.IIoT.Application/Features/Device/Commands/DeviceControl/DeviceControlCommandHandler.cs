using System.Text.Json;
using MediatR;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Application.Features.Device.Commands.DeviceControl;

public sealed class DeviceControlCommandHandler : IRequestHandler<DeviceControlCommand, DeviceControlResult>
{
    private readonly IDeviceControlAuditRepository _audits;
    private readonly IMqttPublisher _mqtt;

    public DeviceControlCommandHandler(IDeviceControlAuditRepository audits, IMqttPublisher mqtt)
    {
        _audits = audits;
        _mqtt = mqtt;
    }

    public async Task<DeviceControlResult> Handle(DeviceControlCommand request, CancellationToken cancellationToken)
    {
        var deviceId = new DeviceId(request.DeviceId);
        var audit = new DeviceControlAudit(
            deviceId,
            request.Command,
            request.ValuePercent,
            request.Value16Bit,
            request.RequestId,
            request.RequestedByUserId,
            accepted: true);

        await _audits.AddAsync(audit, cancellationToken).ConfigureAwait(false);

        var topic = $"iiot/{request.SiteId}/{request.DeviceId}/control";
        var payload = JsonSerializer.Serialize(new
        {
            status = "accepted",
            device_id = request.DeviceId,
            command = request.Command,
            value_pct = request.ValuePercent,
            value_16bit = request.Value16Bit
        });

        await _mqtt.PublishAsync(topic, payload, cancellationToken).ConfigureAwait(false);

        return new DeviceControlResult(true, audit.Id);
    }
}
