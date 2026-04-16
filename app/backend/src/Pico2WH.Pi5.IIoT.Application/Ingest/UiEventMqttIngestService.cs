using System.Text.Json;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;

namespace Pico2WH.Pi5.IIoT.Application.Ingest;

public sealed class UiEventMqttIngestService : IUiEventMqttIngestService
{
    private readonly IUiEventIngestRepository _repo;

    public UiEventMqttIngestService(IUiEventIngestRepository repo)
    {
        _repo = repo;
    }

    public async Task IngestUiEventJsonAsync(
        string siteId,
        string deviceId,
        string jsonPayload,
        CancellationToken cancellationToken = default)
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

        var item = new UiEventIngestItem(
            DeviceId: deviceId.Trim(),
            SiteId: siteId.Trim(),
            DeviceTimeUtc: deviceTime,
            EventType: Truncate(GetString(root, "event_type") ?? "unknown", 16),
            EventValue: Truncate(GetString(root, "event_value") ?? "", 64),
            Channel: Truncate(GetString(root, "channel") ?? "ui", 32),
            PayloadJson: jsonPayload,
            IngestedAtUtc: DateTime.UtcNow);

        await _repo.AddAsync(item, cancellationToken).ConfigureAwait(false);
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
