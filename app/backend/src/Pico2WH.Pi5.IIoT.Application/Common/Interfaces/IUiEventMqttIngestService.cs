namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>將 MQTT <c>ui-events</c> JSON 寫入（對齊 v5 §6.0 階段二）。</summary>
public interface IUiEventMqttIngestService
{
    Task IngestUiEventJsonAsync(string siteId, string deviceId, string jsonPayload, CancellationToken cancellationToken = default);
}
