using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Domain.Entities;

/// <summary>裝置控制審計（對齊四層規格 <c>device_control_audits</c>）。</summary>
public sealed class DeviceControlAudit
{
    private DeviceControlAudit()
    {
    }

    public DeviceControlAudit(
        DeviceId deviceId,
        string command,
        int valuePercent,
        int value16Bit,
        string requestId,
        Guid? requestedByUserId = null,
        bool accepted = true)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new DomainException("Command 不可為空。");

        if (command.Length > 64)
            throw new DomainException("Command 長度不可超過 64。");

        if (valuePercent is < 0 or > 100)
            throw new DomainException("valuePercent 必須在 0～100。");

        if (value16Bit is < 0 or > 65535)
            throw new DomainException("value16Bit 必須在 0～65535。");

        if (string.IsNullOrWhiteSpace(requestId))
            throw new DomainException("RequestId 不可為空。");

        if (requestId.Length > 128)
            throw new DomainException("RequestId 長度不可超過 128。");

        Id = Guid.NewGuid();
        DeviceIdentifier = deviceId;
        Command = command.Trim();
        ValuePercent = valuePercent;
        Value16Bit = value16Bit;
        RequestId = requestId.Trim();
        RequestedByUserId = requestedByUserId;
        Accepted = accepted;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public DeviceId DeviceIdentifier { get; private set; }

    public string Command { get; private set; } = string.Empty;

    public int ValuePercent { get; private set; }

    public int Value16Bit { get; private set; }

    public Guid? RequestedByUserId { get; private set; }

    public string RequestId { get; private set; } = string.Empty;

    public bool Accepted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
