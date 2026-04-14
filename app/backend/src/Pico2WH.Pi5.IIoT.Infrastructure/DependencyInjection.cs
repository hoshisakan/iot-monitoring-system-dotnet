using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Repositories;
using Pico2WH.Pi5.IIoT.Infrastructure.Docker;
using Pico2WH.Pi5.IIoT.Infrastructure.Identity.Jwt;
using Pico2WH.Pi5.IIoT.Infrastructure.Identity.Security;
using Microsoft.Extensions.Hosting;
using Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;
using Pico2WH.Pi5.IIoT.Infrastructure.Queries;

namespace Pico2WH.Pi5.IIoT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));
        services.Configure<DockerOptions>(configuration.GetSection(DockerOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("缺少連線字串：ConnectionStrings:Default");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IDbConnectionFactory>(sp =>
            new NpgsqlConnectionFactory(
                connectionString,
                sp.GetRequiredService<ILogger<NpgsqlConnectionFactory>>()));

        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<LogQueryRepository>();
        services.AddScoped<LogDapperQueryRepository>();
        services.AddScoped<ILogQueryRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return options.UseDapperForLogsQuery
                ? sp.GetRequiredService<LogDapperQueryRepository>()
                : sp.GetRequiredService<LogQueryRepository>();
        });
        services.AddScoped<IDeviceControlAuditRepository, DeviceControlAuditRepository>();

        services.AddSingleton<IJwtService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();

        services.AddScoped<IMqttPublisher, MqttPublisher>();
        services.AddScoped<IUiEventMqttIngestService, UiEventMqttIngestService>();
        services.AddScoped<IStatusLogMqttIngestService, StatusLogMqttIngestService>();
        services.AddHostedService<MqttIngestHostedService>();
        services.AddScoped<IDockerSystemClient, DockerSystemClient>();

        services.AddScoped<TelemetrySeriesDapperQueryService>();
        services.AddScoped<TelemetrySeriesQueryService>();
        services.AddScoped<ITelemetrySeriesQuery>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return options.UseDapperForTelemetrySeries
                ? sp.GetRequiredService<TelemetrySeriesDapperQueryService>()
                : sp.GetRequiredService<TelemetrySeriesQueryService>();
        });
        services.AddScoped<UiEventsEfQuery>();
        services.AddScoped<UiEventsDapperQuery>();
        services.AddScoped<IUiEventsQuery>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return options.UseDapperForUiEventsQuery
                ? sp.GetRequiredService<UiEventsDapperQuery>()
                : sp.GetRequiredService<UiEventsEfQuery>();
        });

        return services;
    }
}
