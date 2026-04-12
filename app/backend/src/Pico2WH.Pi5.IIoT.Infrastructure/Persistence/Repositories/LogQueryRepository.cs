using Microsoft.EntityFrameworkCore;
using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class LogQueryRepository : ILogQueryRepository
{
    private readonly ApplicationDbContext _db;

    public LogQueryRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<StructuredLogEntry> Items, int TotalCount)> QueryAsync(
        LogQueryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var q = _db.AppLogs.AsNoTracking().AsQueryable();

        if (filter.FromUtc.HasValue)
            q = q.Where(a => a.CreatedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue)
            q = q.Where(a => a.CreatedAtUtc <= filter.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(filter.DeviceId))
            q = q.Where(a => a.DeviceId == filter.DeviceId);
        if (!string.IsNullOrWhiteSpace(filter.Channel))
            q = q.Where(a => a.Channel == filter.Channel);
        if (!string.IsNullOrWhiteSpace(filter.Level))
            q = q.Where(a => a.Level == filter.Level);

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await q
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(a => new StructuredLogEntry
        {
            Id = a.Id,
            DeviceId = a.DeviceId,
            Channel = a.Channel,
            Level = a.Level,
            Message = a.Message,
            PayloadJson = a.PayloadJson,
            SourceIp = a.SourceIp,
            DeviceTimeUtc = a.DeviceTimeUtc,
            CreatedAtUtc = a.CreatedAtUtc
        }).ToList();

        return (items, total);
    }

    public async Task<IReadOnlyList<StructuredLogEntry>> FindByDeviceAsync(
        string deviceId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var q = _db.AppLogs.AsNoTracking().Where(a => a.DeviceId == deviceId);

        if (fromUtc.HasValue)
            q = q.Where(a => a.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            q = q.Where(a => a.CreatedAtUtc <= toUtc.Value);

        var rows = await q
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(a => new StructuredLogEntry
        {
            Id = a.Id,
            DeviceId = a.DeviceId,
            Channel = a.Channel,
            Level = a.Level,
            Message = a.Message,
            PayloadJson = a.PayloadJson,
            SourceIp = a.SourceIp,
            DeviceTimeUtc = a.DeviceTimeUtc,
            CreatedAtUtc = a.CreatedAtUtc
        }).ToList();
    }
}
