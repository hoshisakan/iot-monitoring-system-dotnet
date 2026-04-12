using Microsoft.EntityFrameworkCore;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class TelemetryRepository : ITelemetryRepository
{
    private readonly ApplicationDbContext _db;

    public TelemetryRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(TelemetryReading reading, CancellationToken cancellationToken = default)
    {
        await _db.TelemetryReadings.AddAsync(reading, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TelemetryReading>> ListForDeviceAsync(
        DeviceId deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _db.TelemetryReadings
            .AsNoTracking()
            .Where(t => t.DeviceId == deviceId && t.DeviceTimeUtc >= fromUtc && t.DeviceTimeUtc <= toUtc)
            .OrderByDescending(t => t.DeviceTimeUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountForDeviceAsync(
        DeviceId deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default) =>
        _db.TelemetryReadings
            .AsNoTracking()
            .CountAsync(
                t => t.DeviceId == deviceId && t.DeviceTimeUtc >= fromUtc && t.DeviceTimeUtc <= toUtc,
                cancellationToken);
}
