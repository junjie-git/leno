using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Infrastructure.Tests.Middleware;

public class InternalApiKeyMiddlewareTests
{
    private const string ProtectedKey = "super-secret-internal-key";

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Leno.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (InternalApiKeyMiddleware middleware, Func<bool> nextWasCalled) CreateSut(
        InternalApiKeyOptions options,
        string environmentName)
    {
        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };
        var logger = Mock.Of<ILogger<InternalApiKeyMiddleware>>();
        var env = new TestHostEnvironment { EnvironmentName = environmentName };
        var sut = new InternalApiKeyMiddleware(next, logger, Options.Create(options));
        return (sut, () => called);
    }

    private static DefaultHttpContext CreateContext(string path, string? internalKey = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Headers["X-Internal-Key"] = internalKey;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }

    // ---------- Task 5: fail-closed 与 timing-safe ----------

    [Fact]
    public async Task Production_ApiKeyEmpty_Returns500_AndDoesNotCallNext()
    {
        var options = new InternalApiKeyOptions { ApiKey = string.Empty, RoutePrefix = "internal/" };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal/foo");

        await sut.InvokeAsync(context, new TestHostEnvironment { EnvironmentName = Environments.Production });

        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");
        ReadBody(context).Should().Contain("\"code\":500");
    }

    [Fact]
    public async Task Staging_ApiKeyEmpty_Returns500_AsNonDevelopmentEnvironment()
    {
        var options = new InternalApiKeyOptions { ApiKey = string.Empty, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Staging };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Staging);
        var context = CreateContext("/internal/foo");

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Development_ApiKeyEmpty_AllowsRequestThrough()
    {
        var options = new InternalApiKeyOptions { ApiKey = string.Empty, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Development);
        var context = CreateContext("/internal/foo");

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task ValidApiKey_AllowsRequestThrough()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal/foo", ProtectedKey);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvalidApiKey_Returns401_AndDoesNotCallNext()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal/foo", "wrong-key");

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        ReadBody(context).Should().Contain("\"code\":401");
    }

    [Fact]
    public async Task MissingApiKeyHeader_Returns401_AndDoesNotCallNext()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal/foo", internalKey: null);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task FixedTimeEquals_DoesNotBreakValidKeyComparison()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);

        // 正确密钥应放行（验证 timing-safe 比较不破坏正常匹配）
        var okContext = CreateContext("/internal/foo", ProtectedKey);
        await sut.InvokeAsync(okContext, env);
        nextWasCalled().Should().BeTrue();
        okContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        // 等长但单字符不同的密钥应拒绝（验证 FixedTimeEquals 仍严格）
        var nearMiss = ProtectedKey[..^1] + (ProtectedKey[^1] == 'a' ? 'b' : 'a');
        var badContext = CreateContext("/internal/foo", nearMiss);
        await sut.InvokeAsync(badContext, env);
        badContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // ---------- Task 6: 路由边界精确匹配 ----------

    [Fact]
    public async Task Route_InternalExact_IsProtected()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal", internalKey: null);

        await sut.InvokeAsync(context, env);

        // /internal 精确匹配前缀，缺失密钥应 401
        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Route_InternalSegment_IsProtected()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal/foo", internalKey: null);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Route_InternalProductsSkusBatch_IsProtected()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal/products/skus/batch", internalKey: null);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Route_InternalInfo_PrefixedPathIsNotProtected()
    {
        // /internalinfo 不应被识别为内部路由（无边界斜杠），即便未带密钥也放行
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internalinfo", internalKey: null);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Route_InternalProductsHyphenated_IsNotProtected()
    {
        // /internal-products 同样不应被识别为内部路由
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/internal-products", internalKey: null);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Route_NonInternalPath_IsNotProtected()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey, RoutePrefix = "internal/" };
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var (sut, nextWasCalled) = CreateSut(options, Environments.Production);
        var context = CreateContext("/api/products", internalKey: null);

        await sut.InvokeAsync(context, env);

        nextWasCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    // ---------- 启动校验扩展 EnsureInternalApiKeyConfigured ----------

    private static ApplicationBuilder CreateAppBuilder(InternalApiKeyOptions options, string environmentName)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment { EnvironmentName = environmentName });
        services.AddSingleton<IOptions<InternalApiKeyOptions>>(Options.Create(options));
        var provider = services.BuildServiceProvider();
        return new ApplicationBuilder(provider);
    }

    [Fact]
    public void EnsureInternalApiKeyConfigured_Development_DoesNotThrowWhenApiKeyEmpty()
    {
        var options = new InternalApiKeyOptions { ApiKey = string.Empty };
        var app = CreateAppBuilder(options, Environments.Development);

        var act = () => app.EnsureInternalApiKeyConfigured();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureInternalApiKeyConfigured_Production_ThrowsWhenApiKeyEmpty()
    {
        var options = new InternalApiKeyOptions { ApiKey = string.Empty };
        var app = CreateAppBuilder(options, Environments.Production);

        var act = () => app.EnsureInternalApiKeyConfigured();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*InternalAuth:ApiKey*");
    }

    [Fact]
    public void EnsureInternalApiKeyConfigured_Production_DoesNotThrowWhenApiKeySet()
    {
        var options = new InternalApiKeyOptions { ApiKey = ProtectedKey };
        var app = CreateAppBuilder(options, Environments.Production);

        var act = () => app.EnsureInternalApiKeyConfigured();

        act.Should().NotThrow();
    }
}
