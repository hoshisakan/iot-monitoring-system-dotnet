using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Domain.Entities;

/// <summary>裝置主檔（對齊四層規格 DDL <c>devices</c> 與 v5 站臺語意）。</summary>
public sealed class Device : EntityBase
{
    private Device()
    {
    }

    public Device(DeviceId deviceId, string name, DateTime lastSeenAtUtc, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("裝置名稱不可為空。");

        if (name.Length > 128)
            throw new DomainException("裝置名稱長度不可超過 128。");

        Id = Guid.NewGuid();
        DeviceIdentifier = deviceId;
        Name = name.Trim();
        IsActive = isActive;
        LastSeenAtUtc = lastSeenAtUtc;
        var now = DateTime.UtcNow;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public DeviceId DeviceIdentifier { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public void UpdateLastSeen(DateTime lastSeenAtUtc)
    {
        LastSeenAtUtc = lastSeenAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("裝置名稱不可為空。");

        if (name.Length > 128)
            throw new DomainException("裝置名稱長度不可超過 128。");

        Name = name.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
