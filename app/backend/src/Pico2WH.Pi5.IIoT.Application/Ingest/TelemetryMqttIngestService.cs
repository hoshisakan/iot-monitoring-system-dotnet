using System.Text.Json;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Application.Ingest;

public sealed class TelemetryMqttIngestService : ITelemetryMqttIngestService
{
    private readonly ITelemetryIngestRepository _repo;

    public TelemetryMqttIngestService(ITelemetryIngestRepository repo)
    {
        _repo = repo;
    }

    public async Task IngestTelemetryJsonAsync(
        string siteId,
        string deviceId,
        string jsonPayload,
        bool isSyncBackFromTopic,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(deviceId))
            return;

        using var doc = JsonDocument.Parse(jsonPayload, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;

        var deviceTime = ParseDeviceTime(root) ?? DateTime.UtcNow;
        var serverTime = ParseDateTimeElement(root, "server_time") ?? DateTime.UtcNow;
        var isSyncBack = GetBool(root, "is_sync_back") ?? isSyncBackFromTopic;

        var item = new TelemetryIngestItem(
            DeviceId: new DeviceId(deviceId).Value,
            SiteId: siteId.Trim(),
            deviceTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(deviceTime, DateTimeKind.Utc) : deviceTime.ToUniversalTime(),
            serverTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(serverTime, DateTimeKind.Utc) : serverTime.ToUniversalTime(),
            isSyncBack,
            TemperatureC: GetDouble(root, "temperature_c"),
            HumidityPct: GetDouble(root, "humidity_pct"),
            Lux: GetDouble(root, "lux"),
            Co2Ppm: GetDouble(root, "co2_ppm"),
            TemperatureCScd41: GetDouble(root, "temperature_c_scd41"),
            HumidityPctScd41: GetDouble(root, "humidity_pct_scd41"),
            PirActive: GetBool(root, "pir_active") ?? GetBool(root, "motion"),
            PressureHpa: GetDouble(root, "pressure"),
            GasResistanceOhm: GetDouble(root, "gas_resistance"),
            AccelX: GetDouble(root, "accel_x"),
            AccelY: GetDouble(root, "accel_y"),
            AccelZ: GetDouble(root, "accel_z"),
            GyroX: GetDouble(root, "gyro_x"),
            GyroY: GetDouble(root, "gyro_y"),
            GyroZ: GetDouble(root, "gyro_z"),
            RssiDbm: GetInt32(root, "rssi"),
            RawPayloadJson: jsonPayload);

        await _repo.AddAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private static DateTime? ParseDeviceTime(JsonElement root)
    {
        if (root.TryGetProperty("device_time", out var el))
            return ParseDateTimeElement(el);

        return null;
    }

    private static DateTime? ParseDateTimeElement(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;
        return ParseDateTimeElement(el);
    }

    private static DateTime? ParseDateTimeElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                if (DateTime.TryParse(el.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
                break;
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var unixMs))
                    return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
                break;
        }

        return null;
    }

    private static double? GetDouble(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(el.GetString(), System.Globalization.NumberStyles.Float, null, out var ds) => ds,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => null
        };
    }

    private static bool? GetBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) ? b : null,
            JsonValueKind.Number => el.GetInt32() != 0,
            _ => null
        };
    }

    private static int? GetInt32(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.Number when el.TryGetDouble(out var d) => (int)Math.Round(d),
            JsonValueKind.String when int.TryParse(el.GetString(), System.Globalization.NumberStyles.Integer, null, out var s) => s,
            _ => null
        };
    }
}
