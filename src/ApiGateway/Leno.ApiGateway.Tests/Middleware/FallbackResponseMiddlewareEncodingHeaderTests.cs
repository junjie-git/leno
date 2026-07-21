using Leno.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// P1-T18 验证：FallbackResponseMiddleware 降级响应清除编码头。
/// <para>
/// T18：降级响应重写后，原响应的 Transfer-Encoding/Content-Encoding 头必须清除，
/// 否则客户端按残留头解析明文降级响应体会失败。Content-Length 应重算为降级响应体长度。
/// </para>
/// </summary>
public class FallbackResponseMiddlewareEncodingHeaderTests
{
    private static DefaultHttpContext CreateContext(string path = "/api/products/123")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>
    /// T18 验证：503 降级响应应清除 Transfer-Encoding 头。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_On503_RemovesTransferEncodingHeader()
    {
        // Arrange — 下游返回 503 且带 Transfer-Encoding: chunked
        var context = CreateContext();
        RequestDelegate next = _ =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Transfer-Encoding"] = "chunked";
            return Task.CompletedTask;
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — T18：降级后 Transfer-Encoding 头应被清除
        context.Response.Headers.Should().NotContainKey("Transfer-Encoding",
            "降级响应体为明文，Transfer-Encoding: chunked 头应被清除避免客户端解析失败");
    }

    /// <summary>
    /// T18 验证：503 降级响应应清除 Content-Encoding 头。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_On503_RemovesContentEncodingHeader()
    {
        // Arrange — 下游返回 503 且带 Content-Encoding: gzip
        var context = CreateContext();
        RequestDelegate next = _ =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Content-Encoding"] = "gzip";
            return Task.CompletedTask;
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — T18：降级后 Content-Encoding 头应被清除
        context.Response.Headers.Should().NotContainKey("Content-Encoding",
            "降级响应体为明文 JSON，Content-Encoding: gzip 头应被清除避免客户端尝试解压");
    }

    /// <summary>
    /// T18 验证：503 降级响应的 Content-Length 应为降级响应体长度（非原始响应体长度）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_On503_ContentLengthMatchesFallbackBody()
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

        // Assert — T18：Content-Length 应为降级 JSON 体长度
        context.Response.ContentLength.Should().BeGreaterThan(0,
            "降级响应应有非零 Content-Length");
        context.Response.ContentLength.Should().Be(context.Response.Body.Length,
            "Content-Length 应与实际降级响应体长度一致");

        // 验证降级响应体可正确读取
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("\"code\":503");
    }

    /// <summary>
    /// T18 验证：同时携带 Transfer-Encoding 和 Content-Encoding 头的 503 响应，降级后两个头均被清除。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_On503_WithBothEncodingHeaders_RemovesBoth()
    {
        // Arrange
        var context = CreateContext();
        RequestDelegate next = _ =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Transfer-Encoding"] = "chunked";
            context.Response.Headers["Content-Encoding"] = "gzip";
            return Task.CompletedTask;
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — T18：两个编码头均被清除
        context.Response.Headers.Should().NotContainKey("Transfer-Encoding");
        context.Response.Headers.Should().NotContainKey("Content-Encoding");
        context.Response.ContentType.Should().Contain("application/json");
    }

    /// <summary>
    /// T18 验证：200 正常响应不删除编码头（降级逻辑不触发）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_On200_PreservesEncodingHeaders()
    {
        // Arrange
        var context = CreateContext();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.Headers["Content-Encoding"] = "gzip";
            return Task.CompletedTask;
        };

        var middleware = new FallbackResponseMiddleware(next, NullLogger<FallbackResponseMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — 200 响应不触发降级，编码头保留
        context.Response.Headers.Should().ContainKey("Content-Encoding",
            "200 正常响应不触发降级，Content-Encoding 头应保留");
    }
}
