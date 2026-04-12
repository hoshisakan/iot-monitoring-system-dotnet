using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using Npgsql;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;

/// <summary>背景訂閱 MQTT（<c>iiot/+/+/telemetry/#</c>、<c>ui-events</c>、<c>status</c>），並呼叫 ingest 寫入資料庫。</summary>
public sealed class MqttIngestHostedService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<MqttIngestHostedService> _logger;
    private readonly MqttOptions _mqtt;

    private CancellationTokenSource? _stopCts;
    private Task? _runTask;

    public MqttIngestHostedService(
        IOptions<MqttOptions> mqttOptions,
        IServiceScopeFactory scopeFactory,
        IHostEnvironment hostEnvironment,
        ILogger<MqttIngestHostedService> logger)
    {
        _mqtt = mqttOptions.Value;
        _scopeFactory = scopeFactory;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_mqtt.Enabled || !_mqtt.IngestEnabled)
        {
            _logger.LogInformation("MQTT ingest 未啟用（Mqtt:Enabled 或 Mqtt:IngestEnabled 為 false）。");
            return Task.CompletedTask;
        }

        _stopCts = new CancellationTokenSource();
        _runTask = RunAsync(_stopCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopCts is null)
            return;

        _stopCts.Cancel();
        if (_runTask is not null)
            await Task.WhenAny(_runTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);

        _stopCts.Dispose();
        _stopCts = null;
    }

    public void Dispose() => _stopCts?.Dispose();

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        while (!stoppingToken.IsCancellationRequested)
        {
            IMqttClient? client = null;
            try
            {
                client = factory.CreateMqttClient();
                var clientId = _mqtt.ClientId + "-ingest";
                var opts = MqttNetClientConfigurator.BuildClientOptions(_mqtt, _hostEnvironment, clientId, _logger);

                client.ApplicationMessageReceivedAsync += OnApplicationMessageAsync;

                await client.ConnectAsync(opts, stoppingToken).ConfigureAwait(false);

                var topicFilters = _mqtt.EffectiveSubscribeTopicFilters;
                foreach (var topicFilter in topicFilters)
                {
                    await client
                        .SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic(topicFilter).Build(),
                            stoppingToken)
                        .ConfigureAwait(false);
                }

                _logger.LogInformation(
                    "MQTT ingest 已訂閱 {Count} 個 topic：{TopicFilters}",
                    topicFilters.Count,
                    string.Join(", ", topicFilters));

                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT ingest 連線異常，5 秒後重試");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                if (client is not null)
                {
                    try
                    {
                        client.ApplicationMessageReceivedAsync -= OnApplicationMessageAsync;
                        await client.DisconnectAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignore
                    }

                    client.Dispose();
                }
            }
        }
    }

    private async Task OnApplicationMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic ?? string.Empty;
        var payload = PayloadToString(e.ApplicationMessage);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var telemetryIngest = scope.ServiceProvider.GetRequiredService<ITelemetryMqttIngestService>();
            var uiIngest = scope.ServiceProvider.GetRequiredService<IUiEventMqttIngestService>();
            var statusLogIngest = scope.ServiceProvider.GetRequiredService<IStatusLogMqttIngestService>();

            _logger.LogInformation(
                "mod=mqtt_ingest topic={Topic} device_id_hint={Hint} payload_len={Len}",
                topic,
                TryTopicDeviceId(topic),
                payload.Length);

            if (!TryParseIiotTopic(topic, out var siteId, out var deviceId, out var route))
            {
                _logger.LogDebug("略過非 iiot ingest 路由 topic={Topic}", topic);
                return;
            }

            switch (route)
            {
                case IiotRoute.Telemetry:
                    var syncFromTopic = topic.Contains("sync-back", StringComparison.OrdinalIgnoreCase);
                    try
                    {
                        await telemetryIngest
                            .IngestTelemetryJsonAsync(siteId, deviceId, payload, syncFromTopic, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
                    {
                        _logger.LogDebug(ex, "遙測重複略過 topic={Topic}", topic);
                    }

                    break;
                case IiotRoute.UiEvents:
                    await uiIngest.IngestUiEventJsonAsync(siteId, deviceId, payload, CancellationToken.None)
                        .ConfigureAwait(false);
                    break;
                case IiotRoute.Status:
                    await statusLogIngest.IngestStatusLogJsonAsync(siteId, deviceId, payload, CancellationToken.None)
                        .ConfigureAwait(false);
                    break;
                default:
                    _logger.LogDebug("略過 topic={Topic}", topic);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT ingest 處理失敗 topic={Topic}", topic);
        }
    }

    private static string? TryTopicDeviceId(string topic)
    {
        var p = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return p.Length > 2 ? p[2] : null;
    }

    private static string PayloadToString(MqttApplicationMessage message)
    {
        if (message.PayloadSegment.Count == 0)
            return string.Empty;
        return Encoding.UTF8.GetString(message.PayloadSegment.ToArray());
    }

    private static bool TryParseIiotTopic(string topic, out string siteId, out string deviceId, out IiotRoute route)
    {
        siteId = string.Empty;
        deviceId = string.Empty;
        route = IiotRoute.Unknown;

        var p = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 4 || !string.Equals(p[0], "iiot", StringComparison.OrdinalIgnoreCase))
            return false;

        siteId = p[1];
        deviceId = p[2];

        if (string.Equals(p[3], "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            route = IiotRoute.Telemetry;
            return true;
        }

        if (string.Equals(p[3], "ui-events", StringComparison.OrdinalIgnoreCase))
        {
            route = IiotRoute.UiEvents;
            return true;
        }

        if (string.Equals(p[3], "status", StringComparison.OrdinalIgnoreCase))
        {
            route = IiotRoute.Status;
            return true;
        }

        return false;
    }

    private enum IiotRoute
    {
        Unknown = 0,
        Telemetry = 1,
        UiEvents = 2,
        Status = 3
    }
}
