using Microsoft.EntityFrameworkCore;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Queries;

public sealed class UiEventsEfQuery : IUiEventsQuery
{
    private readonly ApplicationDbContext _db;

    public UiEventsEfQuery(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UiEventListItemDto>> QueryAsync(
        string? deviceId,
        string? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var q = _db.DeviceUiEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(deviceId))
            q = q.Where(e => e.DeviceId == deviceId);
        if (!string.IsNullOrWhiteSpace(siteId))
            q = q.Where(e => e.SiteId == siteId);
        if (fromUtc.HasValue)
            q = q.Where(e => e.DeviceTimeUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            q = q.Where(e => e.DeviceTimeUtc <= toUtc.Value);

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await q
            .OrderByDescending(e => e.DeviceTimeUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(e => new UiEventListItemDto(
            e.EventId,
            e.DeviceId,
            e.DeviceTimeUtc,
            e.EventType,
            e.EventValue,
            e.Channel,
            e.SiteId)).ToList();

        return new PagedResult<UiEventListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
