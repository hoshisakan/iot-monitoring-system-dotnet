using FluentAssertions;
using Moq;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Ingest;

namespace Pico2WH.Pi5.IIoT.Application.Tests.Ingest;

public sealed class UiEventMqttIngestServiceTests
{
    [Fact]
    public async Task IngestUiEventJsonAsync_should_map_and_truncate_fields()
    {
        UiEventIngestItem? captured = null;
        var repo = new Mock<IUiEventIngestRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<UiEventIngestItem>(), It.IsAny<CancellationToken>()))
            .Callback<UiEventIngestItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var sut = new UiEventMqttIngestService(repo.Object);
        var longEventType = new string('t', 20);
        var longEventValue = new string('v', 70);
        var longChannel = new string('c', 40);
        var payload =
            $$"""{"device_time":"2026-04-16T12:34:56Z","event_type":"{{longEventType}}","event_value":"{{longEventValue}}","channel":"{{longChannel}}"}""";

        await sut.IngestUiEventJsonAsync(" lab ", " pi5-001 ", payload, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SiteId.Should().Be("lab");
        captured.DeviceId.Should().Be("pi5-001");
        captured.DeviceTimeUtc.Should().Be(DateTime.Parse("2026-04-16T12:34:56Z").ToUniversalTime());
        captured.EventType.Should().Be(new string('t', 16));
        captured.EventValue.Should().Be(new string('v', 64));
        captured.Channel.Should().Be(new string('c', 32));
        captured.PayloadJson.Should().Be(payload);
    }

    [Fact]
    public async Task IngestUiEventJsonAsync_should_parse_unix_ms_device_time_and_defaults()
    {
        UiEventIngestItem? captured = null;
        var repo = new Mock<IUiEventIngestRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<UiEventIngestItem>(), It.IsAny<CancellationToken>()))
            .Callback<UiEventIngestItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var sut = new UiEventMqttIngestService(repo.Object);
        var payload = """{"device_time":1713267296000}""";

        await sut.IngestUiEventJsonAsync("site-1", "dev-1", payload, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DeviceTimeUtc.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1713267296000).UtcDateTime);
        captured.EventType.Should().Be("unknown");
        captured.EventValue.Should().Be("");
        captured.Channel.Should().Be("ui");
    }
}
