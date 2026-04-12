namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>將 MQTT <c>status</c>（韌體結構化日誌 JSON）寫入 <c>app_logs</c>。</summary>
public interface IStatusLogMqttIngestService
{
    Task IngestStatusLogJsonAsync(string siteId, string deviceId, string jsonPayload, CancellationToken cancellationToken = default);
}
