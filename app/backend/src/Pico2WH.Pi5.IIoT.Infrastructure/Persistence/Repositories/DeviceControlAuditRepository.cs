using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class DeviceControlAuditRepository : IDeviceControlAuditRepository
{
    private readonly ApplicationDbContext _db;

    public DeviceControlAuditRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(DeviceControlAudit audit, CancellationToken cancellationToken = default)
    {
        await _db.DeviceControlAudits.AddAsync(audit, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
