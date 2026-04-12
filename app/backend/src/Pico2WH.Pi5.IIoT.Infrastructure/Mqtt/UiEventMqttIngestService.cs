using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;

public sealed class UiEventMqttIngestService : IUiEventMqttIngestService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UiEventMqttIngestService> _logger;

    public UiEventMqttIngestService(ApplicationDbContext db, ILogger<UiEventMqttIngestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task IngestUiEventJsonAsync(string siteId, string deviceId, string jsonPayload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(deviceId))
            return;

        using var doc = JsonDocument.Parse(jsonPayload, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;

        var deviceTime = ParseDeviceTime(root) ?? DateTime.UtcNow;
        if (deviceTime.Kind == DateTimeKind.Unspecified)
            deviceTime = DateTime.SpecifyKind(deviceTime, DateTimeKind.Utc);
        else
            deviceTime = deviceTime.ToUniversalTime();

        var eventType = Truncate(GetString(root, "event_type") ?? "unknown", 16);
        var eventValue = Truncate(GetString(root, "event_value") ?? "", 64);
        var channel = Truncate(GetString(root, "channel") ?? "ui", 32);

        var row = new DeviceUiEventRecord
        {
            DeviceId = deviceId.Trim(),
            DeviceTimeUtc = deviceTime,
            EventType = eventType,
            EventValue = eventValue,
            Channel = channel,
            SiteId = siteId.Trim(),
            PayloadJson = jsonPayload,
            IngestedAtUtc = DateTime.UtcNow
        };

        await _db.DeviceUiEvents.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    private static DateTime? ParseDeviceTime(JsonElement root)
    {
        if (!root.TryGetProperty("device_time", out var el))
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

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
