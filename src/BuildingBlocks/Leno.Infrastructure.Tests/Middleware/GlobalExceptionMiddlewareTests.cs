using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Leno.Infrastructure.Middleware;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Infrastructure.Tests.Middleware;

public class GlobalExceptionMiddlewareTests
{
    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string message, string errorCode = "TEST_ERROR")
            : base(message, errorCode) { }
    }

    private static GlobalExceptionMiddleware CreateMiddleware()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        return new GlobalExceptionMiddleware(
            _ => Task.CompletedTask,
            logger.Object,
            environment.Object);
    }

    private static async Task<ApiResponse?> InvokeMiddleware(Exception ex)
    {
        ErrorCodeMapping.Reset();
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // 通过反射设置 _next 字段，模拟抛异常
        var next = new RequestDelegate(_ => Task.FromException(ex));
        var middlewareType = typeof(GlobalExceptionMiddleware);
        var field = middlewareType.GetField("_next", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(middleware, next);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public async Task InvokeAsync_DomainException_WithNotExistsError_ShouldReturn404ViaMapping()
    {
        var ex = new TestDomainException("用户不存在", "USER_NOT_FOUND");

        var response = await InvokeMiddleware(ex);

        response.Should().NotBeNull();
        response!.Code.Should().Be((int)HttpStatusCode.NotFound);
        response.Message.Should().Be("用户不存在");
    }

    [Fact]
    public async Task InvokeAsync_DomainException_WithAlreadyExists_ShouldReturn409ViaMapping()
    {
        var ex = new TestDomainException("店铺已存在", "SHOP_ALREADY_EXISTS");

        var response = await InvokeMiddleware(ex);

        response.Should().NotBeNull();
        response!.Code.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_WithForbidden_ShouldReturn403ViaMapping()
    {
        var ex = new TestDomainException("无权操作", "ADDRESS_FORBIDDEN");

        var response = await InvokeMiddleware(ex);

        response.Should().NotBeNull();
        response!.Code.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_WithUnmatchedCode_ShouldReturn400()
    {
        var ex = new TestDomainException("普通错误", "CUSTOM_PLAIN_ERROR");

        var response = await InvokeMiddleware(ex);

        response.Should().NotBeNull();
        response!.Code.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_ShouldReturn401()
    {
        var ex = new UnauthorizedAccessException();

        var response = await InvokeMiddleware(ex);

        response.Should().NotBeNull();
        response!.Code.Should().Be((int)HttpStatusCode.Unauthorized);
    }
}
