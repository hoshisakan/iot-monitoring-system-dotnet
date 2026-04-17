using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Ingest;

namespace Pico2WH.Pi5.IIoT.Application.Tests.Ingest;

public sealed class StatusLogMqttIngestServiceTests
{
    [Fact]
    public async Task IngestStatusLogJsonAsync_should_normalize_level_and_compose_message()
    {
        StatusLogIngestItem? captured = null;
        var repo = new Mock<IStatusLogIngestRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<StatusLogIngestItem>(), It.IsAny<CancellationToken>()))
            .Callback<StatusLogIngestItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<StatusLogMqttIngestService>>();
        var sut = new StatusLogMqttIngestService(repo.Object, logger.Object);
        var payload = """{"device_id":"dev-x","module":"sensor","log_level":"WARNING","message":"threshold reached","device_time":"2026-04-16T12:34:56Z"}""";

        await sut.IngestStatusLogJsonAsync("lab", "dev-fallback", payload, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DeviceId.Should().Be("dev-x");
        captured.Channel.Should().Be("status");
        captured.Level.Should().Be("warn");
        captured.Message.Should().Be("[sensor] threshold reached");
        captured.DeviceTimeUtc.Should().Be(DateTime.Parse("2026-04-16T12:34:56Z").ToUniversalTime());
    }

    [Fact]
    public async Task IngestStatusLogJsonAsync_should_fallback_device_and_truncate_message()
    {
        StatusLogIngestItem? captured = null;
        var repo = new Mock<IStatusLogIngestRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<StatusLogIngestItem>(), It.IsAny<CancellationToken>()))
            .Callback<StatusLogIngestItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<StatusLogMqttIngestService>>();
        var sut = new StatusLogMqttIngestService(repo.Object, logger.Object);
        var message = new string('m', 9000);
        var payload = $$"""{"module":"{{new string('x', 100)}}","log_level":"ERR","message":"{{message}}","device_time":1713267296000}""";

        await sut.IngestStatusLogJsonAsync("lab", "fallback-device", payload, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DeviceId.Should().Be("fallback-device");
        captured.Level.Should().Be("error");
        captured.DeviceTimeUtc.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1713267296000).UtcDateTime);
        captured.Message.Length.Should().Be(8000);
        captured.Message.Should().StartWith("[");
    }
}
