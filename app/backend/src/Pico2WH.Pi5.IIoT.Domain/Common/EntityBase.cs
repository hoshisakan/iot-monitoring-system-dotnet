namespace Pico2WH.Pi5.IIoT.Domain.Common;

/// <summary>共用技術主鍵（<see cref="Guid"/>）與建立／更新時間（UTC）。</summary>
public abstract class EntityBase
{
    public Guid Id { get; protected set; }

    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime? UpdatedAtUtc { get; protected set; }
}
