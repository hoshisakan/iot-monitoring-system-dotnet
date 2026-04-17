using FluentAssertions;
using Moq;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Ingest;

namespace Pico2WH.Pi5.IIoT.Application.Tests.Ingest;

public sealed class TelemetryMqttIngestServiceTests
{
    [Fact]
    public async Task IngestTelemetryJsonAsync_should_map_payload_and_topic_fallback_sync_back()
    {
        TelemetryIngestItem? captured = null;
        var repo = new Mock<ITelemetryIngestRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<TelemetryIngestItem>(), It.IsAny<CancellationToken>()))
            .Callback<TelemetryIngestItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var sut = new TelemetryMqttIngestService(repo.Object);
        var payload =
            """{"device_time":"2026-04-16T12:34:56Z","server_time":"2026-04-16T12:34:58Z","temperature_c":26.5,"humidity_pct":"61.2","lux":345.6,"co2_ppm":780,"pir_active":true,"rssi":-58}""";

        await sut.IngestTelemetryJsonAsync(" lab ", " pi5-001 ", payload, isSyncBackFromTopic: true, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SiteId.Should().Be("lab");
        captured.DeviceId.Should().Be("pi5-001");
        captured.DeviceTimeUtc.Should().Be(DateTime.Parse("2026-04-16T12:34:56Z").ToUniversalTime());
        captured.ServerTimeUtc.Should().Be(DateTime.Parse("2026-04-16T12:34:58Z").ToUniversalTime());
        captured.IsSyncBack.Should().BeTrue();
        captured.TemperatureC.Should().Be(26.5);
        captured.HumidityPct.Should().Be(61.2);
        captured.Lux.Should().Be(345.6);
        captured.Co2Ppm.Should().Be(780);
        captured.PirActive.Should().BeTrue();
        captured.RssiDbm.Should().Be(-58);
        captured.RawPayloadJson.Should().Be(payload);
    }

    [Fact]
    public async Task IngestTelemetryJsonAsync_should_use_payload_sync_back_over_topic_fallback()
    {
        TelemetryIngestItem? captured = null;
        var repo = new Mock<ITelemetryIngestRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<TelemetryIngestItem>(), It.IsAny<CancellationToken>()))
            .Callback<TelemetryIngestItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var sut = new TelemetryMqttIngestService(repo.Object);
        var payload = """{"device_time":1713267296000,"is_sync_back":false}""";

        await sut.IngestTelemetryJsonAsync("site-1", "dev-1", payload, isSyncBackFromTopic: true, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DeviceTimeUtc.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1713267296000).UtcDateTime);
        captured.IsSyncBack.Should().BeFalse();
    }

    [Fact]
    public async Task IngestTelemetryJsonAsync_should_skip_when_site_or_device_is_missing()
    {
        var repo = new Mock<ITelemetryIngestRepository>();
        var sut = new TelemetryMqttIngestService(repo.Object);
        var payload = """{"temperature_c":25.5}""";

        await sut.IngestTelemetryJsonAsync("", "dev-1", payload, isSyncBackFromTopic: false, CancellationToken.None);
        await sut.IngestTelemetryJsonAsync("site-1", "", payload, isSyncBackFromTopic: false, CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.IsAny<TelemetryIngestItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
