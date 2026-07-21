using System.Security.Claims;
using System.Text.Json;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// T26 单元测试：验证 WhitelistMiddleware 行为契约。
/// <para>
/// 覆盖三类场景：
/// <list type="bullet">
///   <item>白名单路径放行（无认证也放行）。</item>
///   <item>非白名单路径 + 未认证 → 401 + JSON。</item>
///   <item>非白名单路径 + 已认证 → 放行。</item>
/// </list>
/// 同时验证 WhitelistOptions 默认值、热更新（IOptionsMonitor）、配置化路径覆盖。
/// </para>
/// </summary>
public class WhitelistMiddlewareTests
{
    /// <summary>
    /// 默认白名单路径之一（/api/auth/login）应放行，即使未认证。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhitelistedPathUnauthenticated_ShouldCallNext()
    {
        // Arrange
        var options = new WhitelistOptions();
        var monitor = new TestOptionsMonitor<WhitelistOptions>(options);
        var nextCalled = false;
        var middleware = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/login";
        // User 未设置 → Identity 为 null → 未认证

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/refresh-token")]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/metrics")]
    [InlineData("/METRICS")] // 大小写不敏感
    public async Task InvokeAsync_DefaultWhitelistedPaths_ShouldPassThrough(string path)
    {
        // Arrange
        var monitor = new TestOptionsMonitor<WhitelistOptions>(new WhitelistOptions());
        var nextCalled = false;
        var middleware = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);

        var context = new DefaultHttpContext();
        context.Request.Path = path;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// 非白名单路径 + 未认证 → 401 + JSON { code=401, message="未认证" }，不应调用 next。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_NonWhitelistedPathUnauthenticated_ShouldReturn401AndNotCallNext()
    {
        // Arrange
        var monitor = new TestOptionsMonitor<WhitelistOptions>(new WhitelistOptions());
        var nextCalled = false;
        var middleware = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);

        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        context.Request.Path = "/api/orders";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.ContentType.Should().Contain("application/json");

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        doc.RootElement.GetProperty("code").GetInt32().Should().Be(401);
        doc.RootElement.GetProperty("message").GetString().Should().Be("未认证");
    }

    /// <summary>
    /// 非白名单路径 + 已认证 → 放行（调用 next）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_NonWhitelistedPathAuthenticated_ShouldCallNext()
    {
        // Arrange
        var monitor = new TestOptionsMonitor<WhitelistOptions>(new WhitelistOptions());
        var nextCalled = false;
        var middleware = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "user1") },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// T26：自定义白名单路径（覆盖默认值）应被中间件识别。
    /// 例如新增 /api/public/* 到白名单，未认证也应放行。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_CustomWhitelistedPath_ShouldPassThrough()
    {
        // Arrange：自定义白名单仅含 /api/public
        var options = new WhitelistOptions
        {
            Paths = new List<string> { "/api/public" }
        };
        var monitor = new TestOptionsMonitor<WhitelistOptions>(options);
        var nextCalled = false;
        var middleware = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/public/info";

        // Act
        await middleware.InvokeAsync(context);

        // Assert：自定义路径放行
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// T26：当自定义白名单不包含默认路径时，原默认路径不再放行（验证配置覆盖生效）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_CustomWhitelistExcludingDefaultPaths_DefaultPathsShouldRequireAuth()
    {
        // Arrange：自定义白名单仅含 /api/public，不包含 /api/auth/login
        var options = new WhitelistOptions
        {
            Paths = new List<string> { "/api/public" }
        };
        var monitor = new TestOptionsMonitor<WhitelistOptions>(options);
        var nextCalled = false;
        var middleware = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);

        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = "/api/auth/login"; // 原默认白名单路径，但已被覆盖

        // Act
        await middleware.InvokeAsync(context);

        // Assert：因配置覆盖，原默认路径不再放行 → 401
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// T26：IOptionsMonitor 热更新 — 运行时变更 CurrentValue 应立即生效。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OptionsMonitorHotReload_NewPathShouldImmediatelyPass()
    {
        // Arrange：初始白名单仅含 /api/public
        var options = new WhitelistOptions
        {
            Paths = new List<string> { "/api/public" }
        };
        var monitor = new TestOptionsMonitor<WhitelistOptions>(options);
        var middleware = new WhitelistMiddleware(_ => Task.CompletedTask, monitor);

        // 初始：/api/new 未在白名单 → 401
        var context1 = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        context1.Request.Path = "/api/new";
        await middleware.InvokeAsync(context1);
        context1.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        // Act：热更新 — 向白名单追加 /api/new
        monitor.Update(new WhitelistOptions
        {
            Paths = new List<string> { "/api/public", "/api/new" }
        });

        var nextCalled = false;
        var middleware2 = new WhitelistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, monitor);
        var context2 = new DefaultHttpContext();
        context2.Request.Path = "/api/new";
        await middleware2.InvokeAsync(context2);

        // Assert：热更新后 /api/new 放行
        nextCalled.Should().BeTrue();
        context2.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// T26：null/空路径 → 不命中白名单 → 未认证时 401。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvokeAsync_NullOrEmptyPathUnauthenticated_ShouldReturn401(string? path)
    {
        // Arrange
        var monitor = new TestOptionsMonitor<WhitelistOptions>(new WhitelistOptions());
        var middleware = new WhitelistMiddleware(_ => Task.CompletedTask, monitor);

        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = path ?? string.Empty;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// T26：WhitelistOptions.IsWhitelisted 直接单元测试 — 默认 5 个路径均命中。
    /// </summary>
    [Fact]
    public void WhitelistOptions_DefaultPaths_ShouldContainFiveEntries()
    {
        var options = new WhitelistOptions();

        options.Paths.Should().HaveCount(5);
        options.Paths.Should().Contain("/api/auth/login");
        options.Paths.Should().Contain("/api/auth/register");
        options.Paths.Should().Contain("/api/auth/refresh-token");
        options.Paths.Should().Contain("/health");
        options.Paths.Should().Contain("/metrics");
    }

    /// <summary>
    /// T26：WhitelistOptions.IsWhitelisted — null/空路径返回 false。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WhitelistOptions_IsWhitelisted_NullOrEmpty_ShouldReturnFalse(string? path)
    {
        var options = new WhitelistOptions();

        options.IsWhitelisted(path).Should().BeFalse();
    }

    /// <summary>
    /// T26：WhitelistOptions.IsWhitelisted — 前缀匹配（子路径放行）。
    /// </summary>
    [Fact]
    public void WhitelistOptions_IsWhitelisted_PrefixMatch_ShouldReturnTrue()
    {
        var options = new WhitelistOptions();

        options.IsWhitelisted("/health/live").Should().BeTrue();
        options.IsWhitelisted("/health/ready").Should().BeTrue();
        options.IsWhitelisted("/metrics/foo").Should().BeTrue();
        options.IsWhitelisted("/METRICS").Should().BeTrue(); // 大小写不敏感
    }

    /// <summary>
    /// T26：WhitelistOptions.IsWhitelisted — 非白名单前缀返回 false。
    /// </summary>
    [Fact]
    public void WhitelistOptions_IsWhitelisted_NonMatchingPath_ShouldReturnFalse()
    {
        var options = new WhitelistOptions();

        options.IsWhitelisted("/api/orders").Should().BeFalse();
        options.IsWhitelisted("/api/auth/logout").Should().BeFalse();
    }

    /// <summary>
    /// T26：WhitelistOptions.IsWhitelisted — 跳过空白前缀项，避免空字符串匹配所有路径。
    /// </summary>
    [Fact]
    public void WhitelistOptions_IsWhitelisted_EmptyPrefixEntries_ShouldBeSkipped()
    {
        var options = new WhitelistOptions
        {
            Paths = new List<string> { "", "  ", "/api/real" }
        };

        // 空白前缀项不应匹配任意路径
        options.IsWhitelisted("/api/anything").Should().BeFalse();
        // 真实前缀正常匹配
        options.IsWhitelisted("/api/real").Should().BeTrue();
    }
}

/// <summary>
/// 简易 IOptionsMonitor 测试替身，支持运行时通过 <see cref="Update"/> 模拟热更新。
/// 不依赖 Microsoft.Extensions.Options.ConfigurationExtensions，纯内存实现。
/// </summary>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    private T _currentValue;
    private readonly List<Action<T, string?>> _listeners = new();

    public TestOptionsMonitor(T initialValue)
    {
        _currentValue = initialValue;
    }

    public T CurrentValue => _currentValue;

    public T Get(string? name) => _currentValue;

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        _listeners.Add(listener);
        return new ChangeListenerDisposable(() => _listeners.Remove(listener));
    }

    /// <summary>
    /// 模拟配置热更新：替换 CurrentValue 并触发所有 OnChange 监听器。
    /// </summary>
    public void Update(T newValue)
    {
        _currentValue = newValue;
        foreach (var listener in _listeners)
        {
            listener(newValue, null);
        }
    }

    private sealed class ChangeListenerDisposable : IDisposable
    {
        private readonly Action _unsubscribe;
        public ChangeListenerDisposable(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
