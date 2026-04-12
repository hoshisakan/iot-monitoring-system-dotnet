using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>
/// 遙測時序查詢（降採樣／聚合由 Infrastructure 以 SQL 完成；Application 僅宣告契約）。
/// </summary>
public interface ITelemetrySeriesQuery
{
    Task<SeriesTelemetryResult> QueryAsync(
        string deviceId,
        IReadOnlyList<string> metrics,
        DateTime fromUtc,
        DateTime toUtc,
        int? maxPoints,
        CancellationToken cancellationToken = default);
}
