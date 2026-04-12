namespace Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

/// <summary>MQTT 發布（由 Infrastructure <c>Mqtt</c> 實作）。</summary>
public interface IMqttPublisher
{
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
}
