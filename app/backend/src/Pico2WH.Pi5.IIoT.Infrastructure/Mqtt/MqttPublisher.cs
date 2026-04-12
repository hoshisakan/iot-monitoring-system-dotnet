using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;

public sealed class MqttPublisher : IMqttPublisher
{
    private readonly MqttOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<MqttPublisher> _logger;

    public MqttPublisher(
        IOptions<MqttOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<MqttPublisher> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("MQTT 已停用，略過發布 topic={Topic}", topic);
            return;
        }

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        var clientId = _options.ClientId + "-" + Guid.NewGuid().ToString("N")[..8];
        var mqttOptions = MqttNetClientConfigurator.BuildClientOptions(_options, _hostEnvironment, clientId, _logger);

        await client.ConnectAsync(mqttOptions, cancellationToken).ConfigureAwait(false);

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await client.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
