using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Domain.Repositories;

/// <summary>遙測持久化埠（由 Infrastructure <c>Persistence/Repositories</c> 實作）。</summary>
public interface ITelemetryRepository
{
    Task<IReadOnlyList<TelemetryReading>> ListForDeviceAsync(
        DeviceId deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountForDeviceAsync(
        DeviceId deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
