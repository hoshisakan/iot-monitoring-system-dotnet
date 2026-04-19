using System.Text.Json.Nodes;
using EFCore.NamingConventions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Ingest;
using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;
using Pico2WH.Pi5.IIoT.Infrastructure.Queries;
using Testcontainers.PostgreSql;

namespace Pico2WH.Pi5.IIoT.Api.IntegrationTests;

public sealed class DapperReadQueryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private bool _isInitialized;
    private bool _isDisabled;

    private IOptions<DatabaseOptions> _dbOptions = null!;
    private DbContextOptions<ApplicationDbContext> _dbContextOptions = null!;
    private NpgsqlConnectionFactory _connectionFactory = null!;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Logs_dapper_query_should_return_seeded_data()
    {
        if (!await EnsureInitializedAsync())
            return;

        var dapper = new LogDapperQuery(_connectionFactory, _dbOptions);

        var cases = new[]
        {
            new { Filter = new LogQueryFilter(), ExpectedTotal = 5, Page = 1, PageSize = 3, ExpectedItemCount = 3 },
            new { Filter = new LogQueryFilter(DeviceId: "dev-a"), ExpectedTotal = 2, Page = 1, PageSize = 2, ExpectedItemCount = 2 },
            new
            {
                Filter = new LogQueryFilter(
                    FromUtc: new DateTime(2026, 4, 14, 10, 5, 0, DateTimeKind.Utc),
                    ToUtc: new DateTime(2026, 4, 14, 10, 40, 0, DateTimeKind.Utc),
                    DeviceId: "dev-b",
                    Channel: "status",
                    Level: "WARN"),
                // created_at_utc 第三筆為 10:40:04，嚴格 <= 10:40:00 不包含
                ExpectedTotal = 2,
                Page = 1,
                PageSize = 10,
                ExpectedItemCount = 2
            }
        };

        foreach (var c in cases)
        {
            var dapperResult = await dapper.QueryAsync(c.Filter, c.Page, c.PageSize);

            dapperResult.TotalCount.Should().Be(c.ExpectedTotal);
            dapperResult.Items.Should().HaveCount(c.ExpectedItemCount);
        }
    }

    [Fact]
    public async Task Ui_events_dapper_query_should_return_seeded_data()
    {
        if (!await EnsureInitializedAsync())
            return;

        var dapper = new UiEventsDapperQuery(_connectionFactory, _dbOptions);

        var cases = new[]
        {
            new { DeviceId = (string?)null, SiteId = (string?)null, FromUtc = (DateTime?)null, ToUtc = (DateTime?)null, ExpectedTotal = 5, Page = 1, PageSize = 3, ExpectedItemCount = 3 },
            new { DeviceId = (string?)"dev-a", SiteId = (string?)"site-1", FromUtc = (DateTime?)null, ToUtc = (DateTime?)null, ExpectedTotal = 2, Page = 1, PageSize = 2, ExpectedItemCount = 2 },
            new
            {
                DeviceId = (string?)"dev-b",
                SiteId = (string?)"site-2",
                FromUtc = (DateTime?)new DateTime(2026, 4, 14, 10, 10, 0, DateTimeKind.Utc),
                ToUtc = (DateTime?)new DateTime(2026, 4, 14, 10, 50, 0, DateTimeKind.Utc),
                ExpectedTotal = 3,
                Page = 1,
                PageSize = 10,
                ExpectedItemCount = 3
            }
        };

        foreach (var c in cases)
        {
            var dapperResult = await dapper.QueryAsync(c.DeviceId, c.SiteId, c.FromUtc, c.ToUtc, c.Page, c.PageSize);

            dapperResult.TotalCount.Should().Be(c.ExpectedTotal);
            dapperResult.Page.Should().Be(c.Page);
            dapperResult.PageSize.Should().Be(c.PageSize);
            dapperResult.Items.Should().HaveCount(c.ExpectedItemCount);
        }
    }

    [Fact]
    public async Task Logs_query_should_resist_sql_injection_inputs()
    {
        if (!await EnsureInitializedAsync())
            return;

        var dapper = new LogDapperQuery(_connectionFactory, _dbOptions);
        var payload = "' OR 1=1 --";

        var cases = new[]
        {
            new LogQueryFilter(DeviceId: payload),
            new LogQueryFilter(Channel: payload),
            new LogQueryFilter(Level: payload),
            new LogQueryFilter(DeviceId: payload, Channel: payload, Level: payload)
        };

        foreach (var filter in cases)
        {
            var dapperResult = await dapper.QueryAsync(filter, page: 1, pageSize: 50);

            dapperResult.TotalCount.Should().Be(0);
            dapperResult.Items.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Ui_events_query_should_resist_sql_injection_inputs()
    {
        if (!await EnsureInitializedAsync())
            return;

        var dapper = new UiEventsDapperQuery(_connectionFactory, _dbOptions);
        var payload = "' OR 1=1 --";

        var dapperResult = await dapper.QueryAsync(payload, payload, null, null, page: 1, pageSize: 50);

        dapperResult.TotalCount.Should().Be(0);
        dapperResult.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Telemetry_series_query_should_resist_sql_injection_inputs()
    {
        if (!await EnsureInitializedAsync())
            return;

        var dapper = new TelemetrySeriesDapperQuery(_connectionFactory, _dbOptions);
        var payload = "' OR 1=1 --";
        var fromUtc = new DateTime(2026, 4, 14, 9, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 4, 14, 11, 0, 0, DateTimeKind.Utc);

        var result = await dapper.QueryAsync(
            payload,
            new[] { "temperature_c", "temperature_c; DROP TABLE telemetry_records; --" },
            fromUtc,
            toUtc,
            maxPoints: 100);

        result.DeviceId.Should().Be(payload);
        result.Series.Should().BeEmpty();
    }

    [Fact]
    public void Dapper_query_services_should_reject_unsafe_schema_identifier()
    {
        var unsafeOptions = Options.Create(new DatabaseOptions
        {
            DefaultSchema = "public; DROP SCHEMA public CASCADE; --"
        });

        var logs = () => new LogDapperQuery(_connectionFactory, unsafeOptions);
        var uiEvents = () => new UiEventsDapperQuery(_connectionFactory, unsafeOptions);
        var telemetry = () => new TelemetrySeriesDapperQuery(_connectionFactory, unsafeOptions);

        logs.Should().Throw<InvalidOperationException>();
        uiEvents.Should().Throw<InvalidOperationException>();
        telemetry.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Ui_events_ingest_service_should_persist_to_db_through_repository()
    {
        if (!await EnsureInitializedAsync())
            return;

        await using var db = CreateDbContext();
        var repo = new UiEventIngestRepository(db);
        var sut = new UiEventMqttIngestService(repo);
        var payload = """{"device_time":"2026-04-16T12:34:56Z","event_type":"gesture","event_value":"swipe_left","channel":"ui"}""";

        await sut.IngestUiEventJsonAsync("site-int", "dev-int", payload, CancellationToken.None);

        var row = await db.DeviceUiEvents
            .AsNoTracking()
            .OrderByDescending(x => x.EventId)
            .FirstAsync(x => x.DeviceId == "dev-int");

        row.SiteId.Should().Be("site-int");
        row.EventType.Should().Be("gesture");
        row.EventValue.Should().Be("swipe_left");
        row.Channel.Should().Be("ui");
        JsonNode.DeepEquals(JsonNode.Parse(row.PayloadJson!), JsonNode.Parse(payload)).Should().BeTrue();
    }

    [Fact]
    public async Task Status_log_ingest_service_should_persist_to_db_through_repository()
    {
        if (!await EnsureInitializedAsync())
            return;

        await using var db = CreateDbContext();
        var repo = new StatusLogIngestRepository(db);
        var sut = new StatusLogMqttIngestService(repo, NullLogger<StatusLogMqttIngestService>.Instance);
        var payload = """{"device_id":"dev-int-log","module":"sensor","log_level":"WARNING","message":"threshold reached","device_time":"2026-04-16T12:35:00Z"}""";

        await sut.IngestStatusLogJsonAsync("site-int", "dev-fallback", payload, CancellationToken.None);

        var row = await db.AppLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstAsync(x => x.DeviceId == "dev-int-log");

        row.Channel.Should().Be("status");
        row.Level.Should().Be("warn");
        row.Message.Should().Be("[sensor] threshold reached");
        JsonNode.DeepEquals(JsonNode.Parse(row.PayloadJson!), JsonNode.Parse(payload)).Should().BeTrue();
    }

    private async Task<bool> EnsureInitializedAsync()
    {
        if (_isDisabled)
            return false;
        if (_isInitialized)
            return true;

        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("parity_db")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _postgres.StartAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _isDisabled = true;
            return false;
        }

        _dbOptions = Options.Create(new DatabaseOptions
        {
            DefaultSchema = "public"
        });

        _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        _connectionFactory = new NpgsqlConnectionFactory(
            _postgres.GetConnectionString(),
            NullLogger<NpgsqlConnectionFactory>.Instance);

        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedAsync(db);
        _isInitialized = true;
        return true;
    }

    private ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(_dbContextOptions, _dbOptions);
    }

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.AppLogs.AnyAsync() || await db.DeviceUiEvents.AnyAsync() || await db.TelemetryReadings.AnyAsync())
            return;

        var logs = new[]
        {
            new AppLogRecord
            {
                DeviceId = "dev-a",
                Channel = "system",
                Level = "INFO",
                Message = "boot",
                PayloadJson = "{\"ok\":true}",
                SourceIp = "10.0.0.1",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc),
                CreatedAtUtc = new DateTime(2026, 4, 14, 10, 0, 5, DateTimeKind.Utc)
            },
            new AppLogRecord
            {
                DeviceId = "dev-a",
                Channel = "status",
                Level = "WARN",
                Message = "temp high",
                PayloadJson = "{\"temp\":81}",
                SourceIp = "10.0.0.1",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 10, 0, DateTimeKind.Utc),
                CreatedAtUtc = new DateTime(2026, 4, 14, 10, 10, 3, DateTimeKind.Utc)
            },
            new AppLogRecord
            {
                DeviceId = "dev-b",
                Channel = "status",
                Level = "WARN",
                Message = "link jitter",
                PayloadJson = "{\"rssi\":-87}",
                SourceIp = "10.0.0.2",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 20, 0, DateTimeKind.Utc),
                CreatedAtUtc = new DateTime(2026, 4, 14, 10, 20, 2, DateTimeKind.Utc)
            },
            new AppLogRecord
            {
                DeviceId = "dev-b",
                Channel = "status",
                Level = "WARN",
                Message = "packet loss",
                PayloadJson = "{\"loss\":2}",
                SourceIp = "10.0.0.2",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 30, 0, DateTimeKind.Utc),
                CreatedAtUtc = new DateTime(2026, 4, 14, 10, 30, 1, DateTimeKind.Utc)
            },
            new AppLogRecord
            {
                DeviceId = "dev-b",
                Channel = "status",
                Level = "WARN",
                Message = "recover",
                PayloadJson = "{\"ok\":true}",
                SourceIp = "10.0.0.2",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 40, 0, DateTimeKind.Utc),
                CreatedAtUtc = new DateTime(2026, 4, 14, 10, 40, 4, DateTimeKind.Utc)
            }
        };

        var events = new[]
        {
            new DeviceUiEventRecord
            {
                DeviceId = "dev-a",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc),
                EventType = "button",
                EventValue = "press",
                Channel = "ui",
                SiteId = "site-1",
                PayloadJson = "{\"btn\":\"A\"}",
                IngestedAtUtc = new DateTime(2026, 4, 14, 10, 0, 1, DateTimeKind.Utc)
            },
            new DeviceUiEventRecord
            {
                DeviceId = "dev-a",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 15, 0, DateTimeKind.Utc),
                EventType = "gesture",
                EventValue = "left",
                Channel = "ui",
                SiteId = "site-1",
                PayloadJson = "{\"swipe\":\"left\"}",
                IngestedAtUtc = new DateTime(2026, 4, 14, 10, 15, 1, DateTimeKind.Utc)
            },
            new DeviceUiEventRecord
            {
                DeviceId = "dev-b",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 25, 0, DateTimeKind.Utc),
                EventType = "button",
                EventValue = "press",
                Channel = "ui",
                SiteId = "site-2",
                PayloadJson = "{\"btn\":\"B\"}",
                IngestedAtUtc = new DateTime(2026, 4, 14, 10, 25, 1, DateTimeKind.Utc)
            },
            new DeviceUiEventRecord
            {
                DeviceId = "dev-b",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 35, 0, DateTimeKind.Utc),
                EventType = "gesture",
                EventValue = "up",
                Channel = "ui",
                SiteId = "site-2",
                PayloadJson = "{\"swipe\":\"up\"}",
                IngestedAtUtc = new DateTime(2026, 4, 14, 10, 35, 1, DateTimeKind.Utc)
            },
            new DeviceUiEventRecord
            {
                DeviceId = "dev-b",
                DeviceTimeUtc = new DateTime(2026, 4, 14, 10, 45, 0, DateTimeKind.Utc),
                EventType = "gesture",
                EventValue = "down",
                Channel = "ui",
                SiteId = "site-2",
                PayloadJson = "{\"swipe\":\"down\"}",
                IngestedAtUtc = new DateTime(2026, 4, 14, 10, 45, 1, DateTimeKind.Utc)
            }
        };

        await db.AppLogs.AddRangeAsync(logs);
        await db.DeviceUiEvents.AddRangeAsync(events);
        await db.TelemetryReadings.AddRangeAsync(
            new TelemetryReading(
                new DeviceId("dev-a"),
                "site-1",
                new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 14, 10, 0, 5, DateTimeKind.Utc),
                false,
                temperatureC: 25.5,
                humidityPct: 52.1,
                rssiDbm: -65),
            new TelemetryReading(
                new DeviceId("dev-b"),
                "site-2",
                new DateTime(2026, 4, 14, 10, 5, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 14, 10, 5, 5, DateTimeKind.Utc),
                false,
                temperatureC: 26.3,
                humidityPct: 49.0,
                rssiDbm: -72));
        await db.SaveChangesAsync();
    }
}