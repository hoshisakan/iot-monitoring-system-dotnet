namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>將 MQTT <c>telemetry</c> JSON 轉為領域實體並寫入（對齊 v5 §2.4.2 ingest 規則）。</summary>
public interface ITelemetryMqttIngestService
{
    /// <param name="isSyncBackFromTopic">例如 topic 含 <c>sync-back</c> 時為 true（v5 補傳）。</param>
    Task IngestTelemetryJsonAsync(
        string siteId,
        string deviceId,
        string jsonPayload,
        bool isSyncBackFromTopic,
        CancellationToken cancellationToken = default);
}
