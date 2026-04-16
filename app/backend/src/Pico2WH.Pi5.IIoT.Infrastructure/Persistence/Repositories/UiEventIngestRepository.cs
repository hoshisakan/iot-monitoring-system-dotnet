using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class UiEventIngestRepository : IUiEventIngestRepository
{
    private readonly ApplicationDbContext _db;

    public UiEventIngestRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(UiEventIngestItem item, CancellationToken cancellationToken = default)
    {
        var row = new DeviceUiEventRecord
        {
            DeviceId = item.DeviceId,
            DeviceTimeUtc = item.DeviceTimeUtc,
            EventType = item.EventType,
            EventValue = item.EventValue,
            Channel = item.Channel,
            SiteId = item.SiteId,
            PayloadJson = item.PayloadJson,
            IngestedAtUtc = item.IngestedAtUtc
        };

        await _db.DeviceUiEvents.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
