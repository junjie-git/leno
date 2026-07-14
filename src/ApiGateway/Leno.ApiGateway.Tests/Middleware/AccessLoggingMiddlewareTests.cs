using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// 轻量级测试用 Logger，捕获 <see cref="ILogger.Log{TState}"/> 调用以便断言。
/// </summary>
internal sealed class CapturingLogger<TCategory> : ILogger<TCategory>
{
    public List<(LogLevel Level, string Message, object? State)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception), state));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

public class AccessLoggingMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string method, string path, string? userIdHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.UserAgent = "TestAgent/1.0";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        if (userIdHeader is not null)
        {
            context.Request.Headers["X-User-Id"] = userIdHeader;
        }

        return context;
    }

    /// <summary>
    /// 从 CapturingLogger 捕获的状态中提取 <see cref="AccessLogEntry"/>。
    /// MEL 的 <c>LogInformation</c> 扩展方法会把结构化参数包装进 <c>FormattedLogValues</c>
    /// (实现 <c>IReadOnlyList&lt;KeyValuePair&lt;string,object?&gt;&gt;</c>)，原始 entry 作为 "AccessLog" 键的值。
    /// </summary>
    private static AccessLogEntry GetEntry(CapturingLogger<AccessLoggingMiddleware> logger)
    {
        logger.Entries.Should().ContainSingle();
        var state = logger.Entries[0].State;

        // 标准 MEL 路径：LogInformation("{@AccessLog}", entry) 把参数包装进 FormattedLogValues
        if (state is System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object?>> list)
        {
            foreach (var kvp in list)
            {
                if (kvp.Value is AccessLogEntry entry)
                {
                    return entry;
                }
            }
        }

        // 兜底：直接以 entry 作为 state（例如直接调用 ILogger.Log）
        if (state is AccessLogEntry directEntry)
        {
            return directEntry;
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected AccessLogEntry in state, but got {state?.GetType().FullName ?? "null"}");
    }

    [Fact]
    public async Task InvokeAsync_LogsEntryWithExpectedFields()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        var capturedStatusCode = 200;

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = capturedStatusCode;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("POST", "/api/order/create", userIdHeader: "12345");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Information);

        var entry = GetEntry(logger);
        entry.Method.Should().Be("POST");
        entry.Path.Should().Be("/api/order/create");
        entry.StatusCode.Should().Be(200);
        entry.Duration.Should().BeGreaterThanOrEqualTo(0);
        entry.ClientIp.Should().Be("192.168.1.100");
        entry.UserId.Should().Be("12345");
        entry.UserAgent.Should().Be("TestAgent/1.0");
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_ReadsUserIdFromHttpContextItemsWhenHeaderAbsent()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Items["UserId"] = "67890";
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = GetEntry(logger);
        entry.UserId.Should().Be("67890");
    }

    [Fact]
    public async Task InvokeAsync_UserIdIsNullWhenNoSourceAvailable()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/health/live");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = GetEntry(logger);
        entry.UserId.Should().BeNull();
        entry.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_CapturesTargetServiceFromReverseProxyFeature()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        var clusterConfig = new Yarp.ReverseProxy.Configuration.ClusterConfig { ClusterId = "order-api" };
        // YARP 2.2.0 的 ClusterModel 构造签名为 (ClusterConfig, HttpMessageInvoker)，且该类为 sealed 不可 Mock。
        var clusterModel = new ClusterModel(clusterConfig, new System.Net.Http.HttpMessageInvoker(new System.Net.Http.SocketsHttpHandler()));

        var reverseProxyFeatureMock = new Mock<IReverseProxyFeature>();
        reverseProxyFeatureMock.SetupGet(f => f.Cluster).Returns(clusterModel);

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("POST", "/api/order/create");
        context.Features.Set(reverseProxyFeatureMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = GetEntry(logger);
        entry.TargetService.Should().Be("order-api");
    }

    [Fact]
    public async Task InvokeAsync_PropagatesExceptionFromNextAndStillLogs()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = _ => throw new InvalidOperationException("downstream failure");

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");

        // Act
        var act = async () => await middleware.InvokeAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        // 即使下游抛异常，访问日志仍应记录（状态码默认 500）
        var entry = GetEntry(logger);
        entry.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_CapturesTraceIdFromCurrentActivity()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");

        using var activity = new System.Diagnostics.Activity("test-activity")
            .SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C)
            .Start();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = GetEntry(logger);
        entry.TraceId.Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_PrefersXForwardedForOverRemoteIpAddress()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.99, 192.168.1.1";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = GetEntry(logger);
        entry.ClientIp.Should().Be("10.0.0.99");
    }
}
