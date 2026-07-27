using System.Security.Claims;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// Task E1 单元测试：验证 GrayscaleRoutingMiddleware 行为契约。
/// <para>
/// 覆盖以下场景：
/// <list type="bullet">
///   <item>回滚开关（RollbackToLegacy=true）→ 决策为 old。</item>
///   <item>灰度未启用（Enabled=false）→ 决策为 old。</item>
///   <item>内部端点（internal/v1/*）→ 决策为 new。</item>
///   <item>未认证用户（无 userId）→ 决策为 old。</item>
///   <item>用户 ID hash 桶 &lt; Threshold → 决策为 new。</item>
///   <item>用户 ID hash 桶 ≥ Threshold → 决策为 old。</item>
///   <item>配置热更新（IOptionsMonitor.Update）→ 决策立即变更。</item>
///   <item>测试头 X-Test-Role 模拟用户 ID → 正确计算 hash。</item>
///   <item>安全性：客户端伪造的 X-Grayscale-Decision 头被移除。</item>
/// </list>
/// </para>
/// </summary>
public class GrayscaleRoutingMiddlewareTests
{
    /// <summary>
    /// 默认灰度配置：Enabled=true, Threshold=5, InternalSwitchAllToNew=true, RollbackToLegacy=false。
    /// </summary>
    private static GrayscaleOptions DefaultOptions() => new()
    {
        Enabled = true,
        Threshold = 5,
        InternalSwitchAllToNew = true,
        RollbackToLegacy = false
    };

    private static GrayscaleRoutingMiddleware CreateMiddleware(
        RequestDelegate next,
        GrayscaleOptions options)
    {
        var monitor = new TestOptionsMonitor<GrayscaleOptions>(options);
        var logger = NullLogger<GrayscaleRoutingMiddleware>.Instance;
        return new GrayscaleRoutingMiddleware(next, monitor, logger);
    }

    private static GrayscaleRoutingMiddleware CreateMiddlewareWithMonitor(
        RequestDelegate next,
        TestOptionsMonitor<GrayscaleOptions> monitor)
    {
        var logger = NullLogger<GrayscaleRoutingMiddleware>.Instance;
        return new GrayscaleRoutingMiddleware(next, monitor, logger);
    }

    /// <summary>
    /// 回滚开关 RollbackToLegacy=true → 决策为 old，即使灰度启用且用户命中桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_RollbackToLegacy_ShouldAlwaysReturnOld()
    {
        // Arrange：启用回滚，threshold=100（理论上全员走新域），但回滚优先级最高
        var options = new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 100,
            InternalSwitchAllToNew = true,
            RollbackToLegacy = true
        };
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "user-123") },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 灰度未启用 Enabled=false → 决策为 old。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_GrayscaleDisabled_ShouldReturnOld()
    {
        // Arrange：灰度关闭，threshold=100 但 Enabled=false
        var options = new GrayscaleOptions
        {
            Enabled = false,
            Threshold = 100,
            InternalSwitchAllToNew = true,
            RollbackToLegacy = false
        };
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "user-123") },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 内部端点（internal/v1/*）+ InternalSwitchAllToNew=true → 决策为 new（即使未认证）。
    /// </summary>
    [Theory]
    [InlineData("/internal/v1/users/sync")]
    [InlineData("/internal/v1/products/cache-refresh")]
    [InlineData("/INTERNAL/V1/orders")] // 大小写不敏感
    public async Task InvokeAsync_InternalEndpoint_ShouldReturnNew(string path)
    {
        // Arrange
        var options = DefaultOptions();
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        // 未认证（无 Sub claim）

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionNew);
    }

    /// <summary>
    /// 内部端点 + InternalSwitchAllToNew=false → 走灰度判定（未认证 → old）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_InternalEndpointButSwitchOff_ShouldFallBackToGrayscaleDecision()
    {
        // Arrange：关闭内部端点强制切新域
        var options = new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 5,
            InternalSwitchAllToNew = false,
            RollbackToLegacy = false
        };
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/internal/v1/users/sync";
        // 未认证

        // Act
        await middleware.InvokeAsync(context);

        // Assert：未认证 → old
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 未认证用户（无 Sub claim 且无 X-Test-Role 头）→ 决策为 old。
    /// 典型场景：login/register/refresh-token 等白名单端点。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_ShouldReturnOld()
    {
        // Arrange
        var options = DefaultOptions();
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/login";
        // 未设置 User → 未认证

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 用户 ID hash 桶 &lt; Threshold → 决策为 new。
    /// 使用已知 hash 桶值的 userId：通过 ComputeDeterministicHash 计算确认。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UserHashBucketBelowThreshold_ShouldReturnNew()
    {
        // Arrange：找一个 hash 桶 < 5 的 userId
        var userId = FindUserIdWithBucket(3); // 桶=3 < 5
        var options = DefaultOptions(); // Threshold=5
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", userId) },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionNew);
    }

    /// <summary>
    /// 用户 ID hash 桶 ≥ Threshold → 决策为 old。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UserHashBucketAboveThreshold_ShouldReturnOld()
    {
        // Arrange：找一个 hash 桶 >= 5 的 userId
        var userId = FindUserIdWithBucket(50); // 桶=50 >= 5
        var options = DefaultOptions(); // Threshold=5
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", userId) },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// Threshold=0 → 无人走新域（等同回滚，但不依赖 RollbackToLegacy）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ThresholdZero_ShouldAlwaysReturnOld()
    {
        // Arrange
        var options = new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 0,
            InternalSwitchAllToNew = false, // 关闭内部端点强制切新域，纯粹测试 threshold
            RollbackToLegacy = false
        };
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "any-user") },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert：hash % 100 一定 >= 0，所以总是 old
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// Threshold=100 → 所有人走新域（全量切换）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ThresholdHundred_ShouldAlwaysReturnNew()
    {
        // Arrange
        var options = new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 100,
            InternalSwitchAllToNew = false,
            RollbackToLegacy = false
        };
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "any-user") },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert：hash % 100 < 100 恒成立，所以总是 new
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionNew);
    }

    /// <summary>
    /// 配置热更新：从 Threshold=5（决策 old）热更新到 Threshold=100（决策 new），决策应立即变更。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OptionsMonitorHotReload_DecisionShouldChangeImmediately()
    {
        // Arrange：初始 Threshold=5，找一个桶=50 的 userId（决策 old）
        var userId = FindUserIdWithBucket(50);
        var options = new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 5,
            InternalSwitchAllToNew = false,
            RollbackToLegacy = false
        };
        var monitor = new TestOptionsMonitor<GrayscaleOptions>(options);
        var middleware = CreateMiddlewareWithMonitor(_ => Task.CompletedTask, monitor);

        var context1 = new DefaultHttpContext();
        context1.Request.Path = "/api/orders";
        context1.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", userId) },
            authenticationType: "Bearer"));

        // Act 1：初始决策应为 old（桶=50 >= 5）
        await middleware.InvokeAsync(context1);
        context1.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);

        // Act 2：热更新 Threshold=100，相同 userId 决策应变为 new
        monitor.Update(new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 100,
            InternalSwitchAllToNew = false,
            RollbackToLegacy = false
        });

        var context2 = new DefaultHttpContext();
        context2.Request.Path = "/api/orders";
        context2.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", userId) },
            authenticationType: "Bearer"));

        await middleware.InvokeAsync(context2);

        // Assert：热更新后决策变为 new
        context2.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionNew);
    }

    /// <summary>
    /// 配置热更新：紧急回滚 — 从 Enabled=true 热更新到 RollbackToLegacy=true，决策立即变 old。
    /// 模拟生产环境发现新域异常时的一键回滚（TTL &lt; 30 秒）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OptionsMonitorHotReload_RollbackShouldTakeEffectImmediately()
    {
        // Arrange：初始配置启用灰度，Threshold=100，用户走新域
        var userId = FindUserIdWithBucket(50);
        var options = new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 100,
            InternalSwitchAllToNew = true,
            RollbackToLegacy = false
        };
        var monitor = new TestOptionsMonitor<GrayscaleOptions>(options);
        var middleware = CreateMiddlewareWithMonitor(_ => Task.CompletedTask, monitor);

        var context1 = new DefaultHttpContext();
        context1.Request.Path = "/api/orders";
        context1.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", userId) },
            authenticationType: "Bearer"));

        await middleware.InvokeAsync(context1);
        context1.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionNew);

        // Act：紧急回滚
        monitor.Update(new GrayscaleOptions
        {
            Enabled = true,
            Threshold = 100,
            InternalSwitchAllToNew = true,
            RollbackToLegacy = true // 触发回滚
        });

        var context2 = new DefaultHttpContext();
        context2.Request.Path = "/api/orders";
        context2.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", userId) },
            authenticationType: "Bearer"));

        await middleware.InvokeAsync(context2);

        // Assert：回滚后所有请求走 old
        context2.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 测试头 X-Test-Role 模拟用户 ID：当 JWT 缺失时，使用 X-Test-Role 头作为 userId 计算 hash。
    /// 用于测试环境模拟特定用户桶，无需构造真实 JWT。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_TestRoleHeader_ShouldBeUsedAsUserIdWhenJwtAbsent()
    {
        // Arrange：使用 X-Test-Role 头模拟用户
        var testRole = FindUserIdWithBucket(3); // 桶=3 < 5
        var options = DefaultOptions();
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.Request.Headers["X-Test-Role"] = testRole;
        // 未设置 User（无 JWT）

        // Act
        await middleware.InvokeAsync(context);

        // Assert：使用 X-Test-Role 作为 userId，桶=3 < 5 → new
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionNew);
    }

    /// <summary>
    /// JWT Sub claim 优先级高于 X-Test-Role 头。
    /// 当两者同时存在时，应使用 JWT Sub。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_JwtSubShouldTakePrecedenceOverTestRoleHeader()
    {
        // Arrange：JWT Sub 桶=50（old），X-Test-Role 桶=3（new），应使用 JWT → old
        var jwtUserId = FindUserIdWithBucket(50);
        var testRole = FindUserIdWithBucket(3);
        var options = DefaultOptions();
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.Request.Headers["X-Test-Role"] = testRole;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", jwtUserId) },
            authenticationType: "Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert：JWT 优先 → 桶=50 → old
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 安全性：客户端伪造的 X-Grayscale-Decision 头应被移除，由中间件重新计算。
    /// 防止客户端绕过灰度判定直接走新域。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ClientForgedDecisionHeader_ShouldBeRemovedAndRecomputed()
    {
        // Arrange：客户端伪造 X-Grayscale-Decision: new
        var options = DefaultOptions();
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader] =
            GrayscaleRoutingMiddleware.DecisionNew; // 伪造
        // 未认证 → 决策应为 old

        // Act
        await middleware.InvokeAsync(context);

        // Assert：伪造的头被移除，决策重新计算为 old
        context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader]
            .ToString().Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 中间件应始终调用 next（即使决策为 old）。
    /// 中间件只负责设置决策头，路由由 YARP 完成。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldAlwaysCallNext()
    {
        // Arrange
        var options = DefaultOptions();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    /// <summary>
    /// 决策头应始终被设置（无论决策结果）。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_DecisionHeaderShouldAlwaysBeSet()
    {
        // Arrange
        var options = DefaultOptions();
        var middleware = CreateMiddleware(_ => Task.CompletedTask, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/orders";

        // Act
        await middleware.InvokeAsync(context);

        // Assert：决策头存在且值为 new 或 old
        var decision = context.Request.Headers[GrayscaleRoutingMiddleware.DecisionHeader].ToString();
        decision.Should().BeOneOf(
            GrayscaleRoutingMiddleware.DecisionNew,
            GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// 构造函数参数校验：next 为 null 应抛 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_NextNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var monitor = new TestOptionsMonitor<GrayscaleOptions>(DefaultOptions());
        var logger = NullLogger<GrayscaleRoutingMiddleware>.Instance;

        // Act + Assert：使用 Func 返回实例避免 CA1806 误报
        Func<GrayscaleRoutingMiddleware> act = () => new GrayscaleRoutingMiddleware(null!, monitor, logger);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("next");
    }

    /// <summary>
    /// 构造函数参数校验：options 为 null 应抛 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_OptionsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var logger = NullLogger<GrayscaleRoutingMiddleware>.Instance;

        // Act + Assert：使用 Func 返回实例避免 CA1806 误报
        Func<GrayscaleRoutingMiddleware> act = () => new GrayscaleRoutingMiddleware(_ => Task.CompletedTask, null!, logger);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    /// <summary>
    /// 构造函数参数校验：logger 为 null 应抛 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_LoggerNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var monitor = new TestOptionsMonitor<GrayscaleOptions>(DefaultOptions());

        // Act + Assert：使用 Func 返回实例避免 CA1806 误报
        Func<GrayscaleRoutingMiddleware> act = () => new GrayscaleRoutingMiddleware(_ => Task.CompletedTask, monitor, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    /// <summary>
    /// InvokeAsync 参数校验：context 为 null 应抛 ArgumentNullException。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ContextNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask, DefaultOptions());

        // Act + Assert
        Func<Task> act = () => middleware.InvokeAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ============================================================
    // ComputeDecision 静态方法直接单元测试（不通过中间件管道）
    // ============================================================

    /// <summary>
    /// ComputeDecision：回滚优先级最高。
    /// </summary>
    [Fact]
    public void ComputeDecision_RollbackToLegacy_ShouldReturnOld()
    {
        var options = new GrayscaleOptions { RollbackToLegacy = true, Enabled = true, Threshold = 100 };
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "user-1") }, "Bearer"));

        var decision = GrayscaleRoutingMiddleware.ComputeDecision(context, "/api/orders", options);

        decision.Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// ComputeDecision：灰度未启用 → old。
    /// </summary>
    [Fact]
    public void ComputeDecision_Disabled_ShouldReturnOld()
    {
        var options = new GrayscaleOptions { Enabled = false, Threshold = 100 };
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "user-1") }, "Bearer"));

        var decision = GrayscaleRoutingMiddleware.ComputeDecision(context, "/api/orders", options);

        decision.Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    /// <summary>
    /// ComputeDecision：内部端点 → new。
    /// </summary>
    [Theory]
    [InlineData("/internal/v1/users")]
    [InlineData("/internal/v1/anything/deep/path")]
    public void ComputeDecision_InternalPath_ShouldReturnNew(string path)
    {
        var options = DefaultOptions();
        var context = new DefaultHttpContext();

        var decision = GrayscaleRoutingMiddleware.ComputeDecision(context, path, options);

        decision.Should().Be(GrayscaleRoutingMiddleware.DecisionNew);
    }

    /// <summary>
    /// ComputeDecision：未认证用户 → old。
    /// </summary>
    [Fact]
    public void ComputeDecision_NoUserId_ShouldReturnOld()
    {
        var options = DefaultOptions();
        var context = new DefaultHttpContext();

        var decision = GrayscaleRoutingMiddleware.ComputeDecision(context, "/api/orders", options);

        decision.Should().Be(GrayscaleRoutingMiddleware.DecisionOld);
    }

    // ============================================================
    // ComputeDeterministicHash 静态方法单元测试
    // ============================================================

    /// <summary>
    /// ComputeDeterministicHash：相同输入应产生相同输出（确定性）。
    /// </summary>
    [Fact]
    public void ComputeDeterministicHash_SameInput_ShouldReturnSameOutput()
    {
        var hash1 = GrayscaleRoutingMiddleware.ComputeDeterministicHash("user-123");
        var hash2 = GrayscaleRoutingMiddleware.ComputeDeterministicHash("user-123");

        hash1.Should().Be(hash2);
    }

    /// <summary>
    /// ComputeDeterministicHash：不同输入应产生不同输出（分布性）。
    /// </summary>
    [Fact]
    public void ComputeDeterministicHash_DifferentInput_ShouldReturnDifferentOutput()
    {
        var hash1 = GrayscaleRoutingMiddleware.ComputeDeterministicHash("user-1");
        var hash2 = GrayscaleRoutingMiddleware.ComputeDeterministicHash("user-2");

        hash1.Should().NotBe(hash2);
    }

    /// <summary>
    /// ComputeDeterministicHash：返回值在 0-99 区间。
    /// </summary>
    [Fact]
    public void ComputeDeterministicHash_ShouldReturnBucketInRange0To99()
    {
        // 测试多个 userId，所有 hash 应在 0-99 区间
        var testUsers = new[] { "user-1", "user-2", "user-100", "abc", "12345", "test-user-id-9999" };
        foreach (var user in testUsers)
        {
            var hash = GrayscaleRoutingMiddleware.ComputeDeterministicHash(user);
            hash.Should().BeInRange(0, 99, $"userId '{user}' 的 hash 桶应在 0-99 区间");
        }
    }

    /// <summary>
    /// ComputeDeterministicHash：空字符串返回 0。
    /// </summary>
    [Fact]
    public void ComputeDeterministicHash_EmptyString_ShouldReturnZero()
    {
        var hash = GrayscaleRoutingMiddleware.ComputeDeterministicHash(string.Empty);
        hash.Should().Be(0);
    }

    /// <summary>
    /// ComputeDeterministicHash：null 返回 0。
    /// </summary>
    [Fact]
    public void ComputeDeterministicHash_Null_ShouldReturnZero()
    {
        var hash = GrayscaleRoutingMiddleware.ComputeDeterministicHash(null!);
        hash.Should().Be(0);
    }

    /// <summary>
    /// ComputeDeterministicHash：均匀分布检验 — 1000 个不同 userId 的桶分布应大致均匀。
    /// 这确保灰度分流不会聚集到特定桶。
    /// </summary>
    [Fact]
    public void ComputeDeterministicHash_Distribution_ShouldBeApproximatelyUniform()
    {
        // Arrange：生成 1000 个不同 userId
        var buckets = new int[100];
        for (int i = 0; i < 1000; i++)
        {
            var userId = $"user-{i:D4}";
            var bucket = GrayscaleRoutingMiddleware.ComputeDeterministicHash(userId);
            buckets[bucket]++;
        }

        // Assert：每个桶至少应有 1 个样本（1000/100=10，宽松下限）
        // 上限避免聚集（理论上最多 ~30，宽松上限 50）
        for (int i = 0; i < 100; i++)
        {
            buckets[i].Should().BeGreaterThan(0, $"bucket {i} 应至少有 1 个样本");
            buckets[i].Should().BeLessThan(50, $"bucket {i} 不应聚集超过 50 个样本");
        }
    }

    // ============================================================
    // ResolveUserId 静态方法单元测试
    // ============================================================

    /// <summary>
    /// ResolveUserId：JWT Sub claim 存在时返回其值。
    /// </summary>
    [Fact]
    public void ResolveUserId_JwtSubClaimPresent_ShouldReturnClaimValue()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "user-from-jwt") },
            authenticationType: "Bearer"));

        var userId = GrayscaleRoutingMiddleware.ResolveUserId(context);

        userId.Should().Be("user-from-jwt");
    }

    /// <summary>
    /// ResolveUserId：无 JWT Sub claim 时回退到 X-Test-Role 头。
    /// </summary>
    [Fact]
    public void ResolveUserId_NoJwtButTestRoleHeaderPresent_ShouldReturnTestRole()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Test-Role"] = "test-user";

        var userId = GrayscaleRoutingMiddleware.ResolveUserId(context);

        userId.Should().Be("test-user");
    }

    /// <summary>
    /// ResolveUserId：既无 JWT Sub 也无 X-Test-Role 头 → 返回空字符串。
    /// </summary>
    [Fact]
    public void ResolveUserId_NoJwtNoTestRole_ShouldReturnEmptyString()
    {
        var context = new DefaultHttpContext();

        var userId = GrayscaleRoutingMiddleware.ResolveUserId(context);

        userId.Should().BeEmpty();
    }

    /// <summary>
    /// ResolveUserId：JWT Sub 优先于 X-Test-Role 头。
    /// </summary>
    [Fact]
    public void ResolveUserId_BothJwtAndTestRolePresent_ShouldPreferJwt()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Sub", "jwt-user") },
            authenticationType: "Bearer"));
        context.Request.Headers["X-Test-Role"] = "test-user";

        var userId = GrayscaleRoutingMiddleware.ResolveUserId(context);

        userId.Should().Be("jwt-user");
    }

    /// <summary>
    /// ResolveUserId：User 为 null 时返回空字符串（防御性编程）。
    /// </summary>
    [Fact]
    public void ResolveUserId_UserNull_ShouldReturnEmptyString()
    {
        var context = new DefaultHttpContext();
        context.User = null!; // 强制设为 null 测试防御性

        var userId = GrayscaleRoutingMiddleware.ResolveUserId(context);

        userId.Should().BeEmpty();
    }

    // ============================================================
    // GrayscaleOptions 默认值测试
    // ============================================================

    /// <summary>
    /// GrayscaleOptions 默认值应符合 Spec 约定。
    /// </summary>
    [Fact]
    public void GrayscaleOptions_Defaults_ShouldMatchSpec()
    {
        var options = new GrayscaleOptions();

        options.Enabled.Should().BeTrue();
        options.Threshold.Should().Be(5);
        options.InternalSwitchAllToNew.Should().BeTrue();
        options.RollbackToLegacy.Should().BeFalse();
    }

    /// <summary>
    /// GrayscaleOptions.SectionName 应为 "Grayscale"。
    /// </summary>
    [Fact]
    public void GrayscaleOptions_SectionName_ShouldBeGrayscale()
    {
        GrayscaleOptions.SectionName.Should().Be("Grayscale");
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>
    /// 查找具有指定 hash 桶值的 userId。
    /// 用于测试时构造确定性场景（如桶=3 → new，桶=50 → old）。
    /// </summary>
    private static string FindUserIdWithBucket(int targetBucket)
    {
        targetBucket.Should().BeInRange(0, 99, "目标桶必须在 0-99 区间");
        for (int i = 0; i < 100000; i++)
        {
            var userId = $"user-{i:D6}";
            if (GrayscaleRoutingMiddleware.ComputeDeterministicHash(userId) == targetBucket)
            {
                return userId;
            }
        }
        throw new InvalidOperationException($"无法找到桶值为 {targetBucket} 的 userId（搜索空间 100000）");
    }
}
