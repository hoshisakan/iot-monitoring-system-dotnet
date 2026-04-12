using Pico2WH.Pi5.IIoT.Domain.Common;

namespace Pico2WH.Pi5.IIoT.Domain.ValueObjects;

/// <summary>裝置識別（對齊 MQTT／API <c>device_id</c>，避免裸 <see cref="string"/> 散落）。</summary>
public readonly record struct DeviceId
{
    public const int MaxLength = 64;

    public string Value { get; }

    public DeviceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("DeviceId 不可為空。");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new DomainException($"DeviceId 長度不可超過 {MaxLength}。");

        Value = trimmed;
    }

    public override string ToString() => Value;
}
