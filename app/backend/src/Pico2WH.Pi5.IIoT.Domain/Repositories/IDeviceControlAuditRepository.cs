using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Domain.Repositories;

/// <summary>裝置控制審計持久化埠（由 Infrastructure 實作）。</summary>
public interface IDeviceControlAuditRepository
{
    Task AddAsync(DeviceControlAudit audit, CancellationToken cancellationToken = default);
}
