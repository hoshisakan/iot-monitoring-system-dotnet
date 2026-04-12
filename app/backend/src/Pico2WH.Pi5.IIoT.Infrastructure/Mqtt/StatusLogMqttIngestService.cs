using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;

/// <summary>
/// 對齊韌體 <c>build_log_payload</c>／<c>publish_control_ack</c>：
/// <c>device_id</c>、<c>module</c>、<c>log_level</c>、<c>error_code</c>、<c>message</c>、<c>device_time</c>。
/// </summary>
public sealed class StatusLogMqttIngestService : IStatusLogMqttIngestService
{
    private const int MaxMessageLength = 8000;
    private const string ChannelStatus = "status";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<StatusLogMqttIngestService> _logger;

    public StatusLogMqttIngestService(ApplicationDbContext db, ILogger<StatusLogMqttIngestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task IngestStatusLogJsonAsync(
        string siteId,
        string deviceId,
        string jsonPayload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        using var doc = JsonDocument.Parse(jsonPayload, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;

        var resolvedDeviceId = Truncate((GetString(root, "device_id") ?? deviceId).Trim(), 64);
        if (string.IsNullOrEmpty(resolvedDeviceId))
            return;

        var deviceTime = ParseDeviceTime(root) ?? DateTime.UtcNow;
        if (deviceTime.Kind == DateTimeKind.Unspecified)
            deviceTime = DateTime.SpecifyKind(deviceTime, DateTimeKind.Utc);
        else
            deviceTime = deviceTime.ToUniversalTime();

        var module = Truncate(GetString(root, "module") ?? "", 64);
        var rawLevel = GetString(root, "log_level") ?? "INFO";
        var level = NormalizeLogLevel(rawLevel);
        var messageBody = GetString(root, "message") ?? "";
        var displayMessage = BuildDisplayMessage(module, messageBody);

        var row = new AppLogRecord
        {
            DeviceId = resolvedDeviceId,
            Channel = ChannelStatus,
            Level = level,
            Message = Truncate(displayMessage, MaxMessageLength),
            PayloadJson = jsonPayload,
            SourceIp = null,
            DeviceTimeUtc = deviceTime,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _db.AppLogs.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "mod=status_log_ingest site_id={SiteId} device_id={DeviceId} level={Level} module={Module}",
            siteId.Trim(),
            resolvedDeviceId,
            level,
            module);
    }

    private static string BuildDisplayMessage(string module, string messageBody)
    {
        if (string.IsNullOrEmpty(module))
            return string.IsNullOrEmpty(messageBody) ? "(no message)" : messageBody;
        if (string.IsNullOrEmpty(messageBody))
            return $"[{module}]";
        return $"[{module}] {messageBody}";
    }

    private static string NormalizeLogLevel(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0)
            return "info";
        s = s.ToLowerInvariant();
        return s switch
        {
            "debug" => "debug",
            "info" or "information" => "info",
            "warn" or "warning" => "warn",
            "error" or "err" or "fatal" or "critical" => "error",
            _ => "info"
        };
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
