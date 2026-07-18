using System.Diagnostics;
using System.Text.Json;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// 全局异常中间件，将领域异常、参数异常、未授权异常统一转换为 <see cref="ApiResponse"/> 标准响应。
/// DomainException 通过 <see cref="ErrorCodeMapping"/> 按 ErrorCode 映射 HTTP 状态码（默认 400），
/// 未授权异常映射 401，其他异常映射 500。
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "响应已开始写出，无法再统一处理异常 TraceId={TraceId}", context.TraceIdentifier);
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (statusCode, message, logLevel) = Resolve(exception);

        if (logLevel == LogLevel.Warning)
        {
            _logger.LogWarning(exception, "领域异常 TraceId={TraceId}", traceId);
        }
        else
        {
            _logger.LogError(exception, "未处理异常 TraceId={TraceId}", traceId);
        }

        var response = ApiResponse.Fail(statusCode, message);
        response.TraceId = traceId;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        if (!context.Response.Headers.ContainsKey("X-Trace-Id"))
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
        }

        var json = JsonSerializer.Serialize(response, response.GetType(), JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private (int StatusCode, string Message, LogLevel LogLevel) Resolve(Exception exception)
    {
        switch (exception)
        {
            case DomainException domainEx:
                // 由 ErrorCodeMapping 按 ErrorCode 映射 HTTP 状态码（默认 400）
                var statusCode = ErrorCodeMapping.GetStatusCode(domainEx.ErrorCode);
                return (statusCode, domainEx.Message, LogLevel.Warning);

            case UnauthorizedAccessException:
                return (StatusCodes.Status401Unauthorized, "未授权", LogLevel.Warning);

            case ArgumentException argEx:
                return (StatusCodes.Status400BadRequest, argEx.Message, LogLevel.Warning);

            default:
                var message = _environment.IsDevelopment()
                    ? exception.Message
                    : "服务器内部错误";
                return (StatusCodes.Status500InternalServerError, message, LogLevel.Error);
        }
    }
}
