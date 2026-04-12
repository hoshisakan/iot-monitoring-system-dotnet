using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Domain.Entities;

/// <summary>
/// 遙測資料列（對齊 <c>telemetry_records</c> 與韌體 <c>build_payload</c> 指標：
/// 環境、SCD41、PIR、光照、IMU、連線 RSSI 等）。
/// </summary>
/// <remarks>技術主鍵為 <see cref="long"/>（<c>BIGSERIAL</c>）；不繼承 <see cref="Common.EntityBase"/>。</remarks>
public sealed class TelemetryReading
{
    private TelemetryReading()
    {
    }

    public TelemetryReading(
        DeviceId deviceId,
        string siteId,
        DateTime deviceTimeUtc,
        DateTime serverTimeUtc,
        bool isSyncBack,
        double? temperatureC = null,
        double? humidityPct = null,
        double? lux = null,
        double? co2Ppm = null,
        double? temperatureCScd41 = null,
        double? humidityPctScd41 = null,
        bool? pirActive = null,
        double? pressureHpa = null,
        double? gasResistanceOhm = null,
        double? accelX = null,
        double? accelY = null,
        double? accelZ = null,
        double? gyroX = null,
        double? gyroY = null,
        double? gyroZ = null,
        int? rssiDbm = null,
        string? rawPayloadJson = null)
    {
        if (string.IsNullOrWhiteSpace(siteId))
            throw new Common.DomainException("SiteId 不可為空。");

        if (siteId.Length > 64)
            throw new Common.DomainException("SiteId 長度不可超過 64。");

        DeviceId = deviceId;
        SiteId = siteId.Trim();
        DeviceTimeUtc = deviceTimeUtc;
        ServerTimeUtc = serverTimeUtc;
        IsSyncBack = isSyncBack;
        TemperatureC = temperatureC;
        HumidityPct = humidityPct;
        Lux = lux;
        Co2Ppm = co2Ppm;
        TemperatureCScd41 = temperatureCScd41;
        HumidityPctScd41 = humidityPctScd41;
        PirActive = pirActive;
        PressureHpa = pressureHpa;
        GasResistanceOhm = gasResistanceOhm;
        AccelX = accelX;
        AccelY = accelY;
        AccelZ = accelZ;
        GyroX = gyroX;
        GyroY = gyroY;
        GyroZ = gyroZ;
        RssiDbm = rssiDbm;
        RawPayloadJson = rawPayloadJson;
    }

    public long Id { get; private set; }

    public DeviceId DeviceId { get; private set; }

    public string SiteId { get; private set; } = string.Empty;

    public DateTime DeviceTimeUtc { get; private set; }

    public DateTime ServerTimeUtc { get; private set; }

    public bool IsSyncBack { get; private set; }

    public double? TemperatureC { get; private set; }

    public double? HumidityPct { get; private set; }

    public double? Lux { get; private set; }

    public double? Co2Ppm { get; private set; }

    public double? TemperatureCScd41 { get; private set; }

    public double? HumidityPctScd41 { get; private set; }

    public bool? PirActive { get; private set; }

    public double? PressureHpa { get; private set; }

    public double? GasResistanceOhm { get; private set; }

    public double? AccelX { get; private set; }

    public double? AccelY { get; private set; }

    public double? AccelZ { get; private set; }

    public double? GyroX { get; private set; }

    public double? GyroY { get; private set; }

    public double? GyroZ { get; private set; }

    public int? RssiDbm { get; private set; }

    public string? RawPayloadJson { get; private set; }
}
