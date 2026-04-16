using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Pico2WH.Pi5.IIoT.Application.Behaviors;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Ingest;

namespace Pico2WH.Pi5.IIoT.Application;

/// <summary>註冊 Application：MediatR、FluentValidation、AutoMapper、管線行為。</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<ITelemetryMqttIngestService, TelemetryMqttIngestService>();
        services.AddScoped<IUiEventMqttIngestService, UiEventMqttIngestService>();
        services.AddScoped<IStatusLogMqttIngestService, StatusLogMqttIngestService>();

        services.AddAutoMapper(assembly);
        services.AddMediatR(typeof(DependencyInjection));
        services.AddValidatorsFromAssembly(assembly);

        // 內層先註冊（靠近 Handler）：Validation → Logging → Authorization（外層）
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));

        return services;
    }
}
