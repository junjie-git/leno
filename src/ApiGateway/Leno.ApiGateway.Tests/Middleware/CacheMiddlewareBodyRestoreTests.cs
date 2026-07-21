using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// CacheMiddleware Response.Body 恢复验证。
/// 验证 P0-T9：_next 抛异常时 Response.Body 必须在 finally 中恢复为原始流，
/// 避免异常传播时上层中间件写入错误的流导致响应损坏。
/// </summary>
public class CacheMiddlewareBodyRestoreTests
{
    private static (Mock<IConnectionMultiplexer> redisMock, Mock<IDatabase> dbMock) CreateRedisMocks()
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        // 缓存未命中
        dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.NullValue);
        // StringSetAsync 默认返回 true（缓存写入）
        dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        return (redisMock, dbMock);
    }

    private static CacheMiddleware CreateMiddleware(RequestDelegate next, Mock<IConnectionMultiplexer> redisMock)
    {
        var options = Options.Create(new CacheOptions { Enabled = true });
        return new CacheMiddleware(next, redisMock.Object, options);
    }

    [Fact]
    public async Task InvokeAsync_NextThrows_ShouldRestoreResponseBody()
    {
        // Arrange — 下游抛异常时，Response.Body 必须在 finally 中恢复
        var (redisMock, _) = CreateRedisMocks();

        RequestDelegate nextThrows = _ => throw new InvalidOperationException("downstream error");
        var middleware = CreateMiddleware(nextThrows, redisMock);

        var context = new DefaultHttpContext();
        var originalBody = context.Response.Body;
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);

        // Assert — 异常传播，但 Response.Body 必须恢复为原始流
        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Response.Body.Should().BeSameAs(originalBody,
            "异常发生时 Response.Body 必须在 finally 中恢复为原始流");
    }

    [Fact]
    public async Task InvokeAsync_NextSucceeds_ShouldRestoreResponseBody()
    {
        // Arrange — 正常完成时 Response.Body 也应恢复为原始流，且响应内容写回原始流
        var (redisMock, _) = CreateRedisMocks();

        RequestDelegate nextSucceeds = async ctx =>
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("OK");
        };
        var middleware = CreateMiddleware(nextSucceeds, redisMock);

        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        context.Response.Body = originalBody;
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Should().BeSameAs(originalBody,
            "正常完成后 Response.Body 应恢复为原始流");
        originalBody.ToArray().Should().NotBeEmpty("响应内容应写入原始流");
        originalBody.ToArray().Should().Equal(System.Text.Encoding.UTF8.GetBytes("OK"));
    }

    [Fact]
    public async Task InvokeAsync_NextThrows_ShouldNotWritePartialResponseToOriginalBody()
    {
        // Arrange — 下游抛异常时，不应将部分响应写入原始流（异常应直接传播到错误处理中间件）
        var (redisMock, _) = CreateRedisMocks();

        RequestDelegate nextThrowsPartial = async ctx =>
        {
            await ctx.Response.WriteAsync("partial");
            throw new InvalidOperationException("downstream error after partial write");
        };
        var middleware = CreateMiddleware(nextThrowsPartial, redisMock);

        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        context.Response.Body = originalBody;
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);

        // Assert — 异常传播
        await act.Should().ThrowAsync<InvalidOperationException>();
        // Response.Body 已恢复
        context.Response.Body.Should().BeSameAs(originalBody);
        // 原始流不应包含部分响应（异常路径跳过 CopyToAsync）
        originalBody.ToArray().Should().BeEmpty(
            "异常路径不应将部分响应写入原始流，由上层错误处理中间件统一处理");
    }

    [Fact]
    public async Task InvokeAsync_CacheMissThenNextThrows_ResponseBodyRestored_BodyIsEmpty()
    {
        // Arrange — 验证 finally 恢复后，原始流未被污染
        var (redisMock, _) = CreateRedisMocks();

        RequestDelegate nextThrows = _ => throw new InvalidOperationException("boom");
        var middleware = CreateMiddleware(nextThrows, redisMock);

        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        originalBody.Write(System.Text.Encoding.UTF8.GetBytes("pre-existing"));
        context.Response.Body = originalBody;
        context.Request.Method = "GET";
        context.Request.Path = "/api/products/list";

        var originalContent = originalBody.ToArray();

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Response.Body.Should().BeSameAs(originalBody);
        // 原始流内容应保持不变（未被 memoryStream 内容覆盖）
        originalBody.ToArray().Should().Equal(originalContent,
            "异常时原始流内容不应被修改");
    }
}
