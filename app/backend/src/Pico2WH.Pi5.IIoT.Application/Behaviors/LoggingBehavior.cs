using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Pico2WH.Pi5.IIoT.Application.Behaviors;

/// <summary>MediatR 管線：記錄請求型別與耗時（不含敏感欄位內容）。</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        _logger.LogDebug("MediatR begin {Request}", name);
        var sw = Stopwatch.StartNew();
        try
        {
            return await next();
        }
        finally
        {
            sw.Stop();
            _logger.LogDebug("MediatR end {Request} in {ElapsedMs} ms", name, sw.ElapsedMilliseconds);
        }
    }
}
