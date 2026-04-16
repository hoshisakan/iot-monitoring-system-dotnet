using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class StatusLogIngestRepository : IStatusLogIngestRepository
{
    private readonly ApplicationDbContext _db;

    public StatusLogIngestRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(StatusLogIngestItem item, CancellationToken cancellationToken = default)
    {
        var row = new AppLogRecord
        {
            DeviceId = item.DeviceId,
            Channel = item.Channel,
            Level = item.Level,
            Message = item.Message,
            PayloadJson = item.PayloadJson,
            SourceIp = null,
            DeviceTimeUtc = item.DeviceTimeUtc,
            CreatedAtUtc = item.CreatedAtUtc
        };

        await _db.AppLogs.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
