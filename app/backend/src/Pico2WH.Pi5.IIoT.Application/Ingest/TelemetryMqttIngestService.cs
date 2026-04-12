using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Application.Ingest;

public sealed class TelemetryMqttIngestService : ITelemetryMqttIngestService
{
    private readonly ITelemetryRepository _telemetry;
    private readonly ILogger<TelemetryMqttIngestService> _logger;

    public TelemetryMqttIngestService(ITelemetryRepository telemetry, ILogger<TelemetryMqttIngestService> logger)
    {
        _telemetry = telemetry;
        _logger = logger;
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

        var reading = new TelemetryReading(
            new DeviceId(deviceId),
            siteId.Trim(),
            deviceTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(deviceTime, DateTimeKind.Utc) : deviceTime.ToUniversalTime(),
            serverTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(serverTime, DateTimeKind.Utc) : serverTime.ToUniversalTime(),
            isSyncBack,
            GetDouble(root, "temperature_c"),
            GetDouble(root, "humidity_pct"),
            GetDouble(root, "lux"),
            GetDouble(root, "co2_ppm"),
            GetDouble(root, "temperature_c_scd41"),
            GetDouble(root, "humidity_pct_scd41"),
            GetBool(root, "pir_active") ?? GetBool(root, "motion"),
            GetDouble(root, "pressure"),
            GetDouble(root, "gas_resistance"),
            GetDouble(root, "accel_x"),
            GetDouble(root, "accel_y"),
            GetDouble(root, "accel_z"),
            GetDouble(root, "gyro_x"),
            GetDouble(root, "gyro_y"),
            GetDouble(root, "gyro_z"),
            GetInt32(root, "rssi"),
            jsonPayload);

        await _telemetry.AddAsync(reading, cancellationToken).ConfigureAwait(false);
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
