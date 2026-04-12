using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Pico2WH.Pi5.IIoT.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未處理例外：{Message}", ex.Message);
            await WriteAsync(context, ex).ConfigureAwait(false);
        }
    }

    private static Task WriteAsync(HttpContext context, Exception ex)
    {
        var requestId = context.TraceIdentifier;
        object body;
        int status;

        switch (ex)
        {
            case ValidationException vex:
                status = (int)HttpStatusCode.BadRequest;
                body = new
                {
                    error = new
                    {
                        code = "VALIDATION_ERROR",
                        message = "請求驗證失敗。",
                        request_id = requestId,
                        errors = vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }).ToList()
                    }
                };
                break;
            case UnauthorizedAccessException:
                status = (int)HttpStatusCode.Unauthorized;
                body = new
                {
                    error = new
                    {
                        code = "UNAUTHORIZED",
                        message = ex.Message,
                        request_id = requestId
                    }
                };
                break;
            default:
                status = (int)HttpStatusCode.InternalServerError;
                body = new
                {
                    error = new
                    {
                        code = "INTERNAL_ERROR",
                        message = "伺服器發生錯誤。",
                        request_id = requestId
                    }
                };
                break;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = status;
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
