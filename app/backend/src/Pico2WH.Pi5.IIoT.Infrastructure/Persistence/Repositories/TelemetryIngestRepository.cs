using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class TelemetryIngestRepository : ITelemetryIngestRepository
{
    private readonly ApplicationDbContext _db;

    public TelemetryIngestRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(TelemetryIngestItem item, CancellationToken cancellationToken = default)
    {
        var reading = new TelemetryReading(
            deviceId: new DeviceId(item.DeviceId),
            siteId: item.SiteId,
            deviceTimeUtc: item.DeviceTimeUtc,
            serverTimeUtc: item.ServerTimeUtc,
            isSyncBack: item.IsSyncBack,
            temperatureC: item.TemperatureC,
            humidityPct: item.HumidityPct,
            lux: item.Lux,
            co2Ppm: item.Co2Ppm,
            temperatureCScd41: item.TemperatureCScd41,
            humidityPctScd41: item.HumidityPctScd41,
            pirActive: item.PirActive,
            pressureHpa: item.PressureHpa,
            gasResistanceOhm: item.GasResistanceOhm,
            accelX: item.AccelX,
            accelY: item.AccelY,
            accelZ: item.AccelZ,
            gyroX: item.GyroX,
            gyroY: item.GyroY,
            gyroZ: item.GyroZ,
            rssiDbm: item.RssiDbm,
            rawPayloadJson: item.RawPayloadJson);

        await _db.TelemetryReadings.AddAsync(reading, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
