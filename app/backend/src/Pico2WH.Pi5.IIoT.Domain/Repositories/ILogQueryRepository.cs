using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Domain.Repositories;

/// <summary>
/// 結構化日誌查詢埠（實作可於 Infrastructure 之 Persistence 或 Logging；擇一註冊）。
/// </summary>
public interface ILogQueryRepository
{
    Task<(IReadOnlyList<StructuredLogEntry> Items, int TotalCount)> QueryAsync(
        LogQueryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StructuredLogEntry>> FindByDeviceAsync(
        string deviceId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
