using Leno.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.ApiGateway.Tests.Middleware;

public class FallbackResponseMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string path = "/api/products/123")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        // Response.Body 默认是 NullStream，需要替换为可读 MemoryStream
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_On503_RewritesBodyAsFallbackJson()
    {
        // Arrange
        var context = CreateContext();
        RequestDelegate next = _ =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(503);
        context.Response.ContentType.Should().Contain("application/json");
        var body = await ReadResponseBody(context);
        body.Should().Contain("\"code\":503");
        body.Should().Contain("\"message\":\"服务暂时不可用，请稍后重试\"");
        body.Should().Contain("\"data\":null");
    }

    [Fact]
    public async Task InvokeAsync_On200_PassesThroughOriginalBody()
    {
        // Arrange
        var context = CreateContext();
        RequestDelegate next = async ctx =>
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"data":"ok"}""");
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        var body = await ReadResponseBody(context);
        body.Should().Be("""{"data":"ok"}""");
    }

    [Fact]
    public async Task InvokeAsync_OnHealthEndpoint_DoesNotBufferOrRewrite()
    {
        // Arrange — /health 端点直接放行不参与降级
        var context = CreateContext(path: "/health/ready");
        var innerBodyWritten = false;
        RequestDelegate next = async ctx =>
        {
            ctx.Response.StatusCode = 503;
            await ctx.Response.WriteAsync("health-down");
            innerBodyWritten = true;
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        innerBodyWritten.Should().BeTrue();
        context.Response.StatusCode.Should().Be(503);
        // 健康端点直接写入原始 Body（NullStream 或外层 Response.Body），不会被改写
        var body = await ReadResponseBody(context);
        body.Should().NotContain("服务暂时不可用");
    }

    [Fact]
    public async Task InvokeAsync_OnNon503Error_PassesThroughBody()
    {
        // Arrange — 500 错误不应被改写为降级 JSON
        var context = CreateContext();
        RequestDelegate next = async ctx =>
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsync("""{"error":"internal"}""");
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        var body = await ReadResponseBody(context);
        body.Should().Be("""{"error":"internal"}""");
    }

    [Fact]
    public void Constructor_NullNext_Throws()
    {
        var act = () => new FallbackResponseMiddleware(null!, NullLogger<FallbackResponseMiddleware>.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new FallbackResponseMiddleware(_ => Task.CompletedTask, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
