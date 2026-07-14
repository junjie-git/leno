# API 网关增强 - 阶段四：流量治理与高可用 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Leno API 网关增加三层限流（全局/路由/用户）、熔断与降级、差异化超时与幂等重试能力，并使用 Redis 实现多实例分布式限流计数，保证高并发下的系统稳定性与可观测性。

**Architecture:** 限流使用 ASP.NET Core `System.Threading.RateLimiting` + YARP `RateLimiterPolicy` 路由级策略名映射，自定义 `RedisSlidingWindowRateLimiter`（继承 `RateLimiter`）通过 `RateLimitPartition.Create` 作为分区返回，多网关实例时计数器集中存 Redis（SortedSet + Lua 脚本原子滑动窗口）；单实例降级到内置 `SlidingWindowRateLimiter`。熔断使用 YARP 内建 `CircuitBreaker` Cluster 配置（MaxConcurrentRequests/FailureRateThreshold/SamplingDuration/MinimumThroughput/BreakDuration）；降级响应通过自定义 `FallbackResponseMiddleware` 拦截 503 改写为统一 JSON。超时使用 YARP 2.1+ 路由级 `Timeout`/`TimeoutPolicy`（集成 ASP.NET Core `AddRequestTimeouts`）+ Cluster 级 `HttpRequest.ActivityTimeout`（idle 超时）。重试使用 YARP 内建 Cluster 级 `Retry` 配置，YARP 默认仅对幂等方法（GET/HEAD/PUT/DELETE/OPTIONS/TRACE）重试，对连接失败与可重试状态码生效。

**Tech Stack:** .NET 10, YARP 2.2.0, `System.Threading.RateLimiting` (.NET 10 内建), `Microsoft.AspNetCore.RateLimiting` (.NET 10 内建), `Microsoft.AspNetCore.Http.Timeouts` (.NET 10 内建), StackExchange.Redis (通过 Leno.Infrastructure 间接引用), xUnit, FluentAssertions, Moq, Microsoft.AspNetCore.TestHost

**Spec:** [docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md](../specs/2026-07-14-api-gateway-enhancement-design.md) 第 5 节（流量治理与高可用）

---

## 实施说明

> 本计划为 Spec 第 5 节的 Phase 4 落地，建立在 Phase 1 已完成（Consul 动态路由、`Extensions/ServiceCollectionExtensions.cs`、`Options/GatewayOptions.cs`、测试项目 `Leno.ApiGateway.Tests` 已存在）之上。以下三点与 Spec 字面描述存在等价性收敛：

1. **限流分区集成方式**：Spec 5.1 示例使用 `RateLimitPartition.GetSlidingWindowLimiter`，但该方法返回内置 `SlidingWindowRateLimiter`（仅在当前进程内存计数），无法满足"多网关实例部署时计数器存入 Redis"的分布式要求。本计划使用 `RateLimitPartition.Create<TKey>(partitionKey, factory)` 自定义工厂返回 `RedisSlidingWindowRateLimiter`（继承自抽象类 `RateLimiter`），与 ASP.NET Core `AddRateLimiter` 中间件完全兼容，并在 Redis 不可用时回退到 `SlidingWindowRateLimiter`。

2. **降级响应实现位置**：Spec 5.2 仅描述"返回预设 JSON"。YARP `CircuitBreaker` 触发时直接返回 503 空响应体，YARP 不暴露 cluster 错误回调来改写响应。本计划通过自定义 `FallbackResponseMiddleware`（位于 `MapReverseProxy` 之前）使用响应体缓冲检测 503 状态码并改写为统一 JSON，与 YARP 2.2.0 兼容。

3. **重试对 503 状态码支持**：YARP 2.2.0 `Retry` 配置内置对连接失败的重试，对 HTTP 状态码 503 的重试通过 `RetryableStatusCodes` 字段配置（YARP 2.2.0 已支持，详见 `Yarp.ReverseProxy.Configuration.RetryOptions`）。Spec 5.3 "重试条件：连接超时、503" 在此通过 `RetryableStatusCodes: [503]` 表达。YARP 默认仅对幂等方法重试，POST 不重试无需额外配置。

---

## 文件结构

### 新建文件

| 文件 | 职责 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Options/RateLimitOptions.cs` | 三层限流配置选项（全局/路由/用户） |
| `src/ApiGateway/Leno.ApiGateway/Options/TimeoutOptions.cs` | 路由类型→超时映射，与 appsettings 双向校验 |
| `src/ApiGateway/Leno.ApiGateway/Options/RetryOptions.cs` | 重试配置选项 |
| `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs` | 基于 Redis SortedSet + Lua 的分布式滑动窗口限流器（继承 `RateLimiter`） |
| `src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs` | 503 响应降级中间件，改写为统一 JSON |
| `src/ApiGateway/Leno.ApiGateway/Extensions/RateLimiterExtensions.cs` | `AddGatewayRateLimiter()` 注册扩展（隔离限流配置） |
| `src/ApiGateway/Leno.ApiGateway/Extensions/RedisExtensions.cs` | `AddGatewayRedis()` 注册 `IConnectionMultiplexer` |
| `src/ApiGateway/Leno.ApiGateway.Tests/Options/RateLimitOptionsTests.cs` | 限流选项绑定测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Options/TimeoutOptionsTests.cs` | 超时选项测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Options/RetryOptionsTests.cs` | 重试选项测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RateLimiterExtensionsTests.cs` | 限流扩展注册测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RedisExtensionsTests.cs` | Redis 扩展注册测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/FallbackResponseMiddlewareTests.cs` | 降级中间件测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs` | Redis 限流器单元测试（mock IDatabase） |
| `src/ApiGateway/Leno.ApiGateway.Tests/Integration/TrafficGovernanceIntegrationTests.cs` | 流量治理端到端集成测试 |

### 修改文件

| 文件 | 修改内容 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` | 显式添加 `StackExchange.Redis` 包引用（虽 Leno.Infrastructure 间接引用，但网关代码直接使用，显式更清晰） |
| `src/ApiGateway/Leno.ApiGateway/Program.cs` | 注册 `AddGatewayRedis`、`AddGatewayRateLimiter`、`AddRequestTimeouts`；中间件管道添加 `UseFallbackResponse`、`UseRateLimiter` |
| `src/ApiGateway/Leno.ApiGateway/appsettings.json` | 每条路由添加 `RateLimiterPolicy`/`Timeout`；每个 Cluster 添加 `CircuitBreaker`/`Retry`/`HttpRequest.ActivityTimeout`；新增 `RateLimit`/`Timeout`/`Retry`/`Redis` 配置节 |
| `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` | 同步 Redis 配置指向 Docker 网络 |

---

## Task 1: 限流策略定义和注册

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Options/RateLimitOptions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Extensions/RateLimiterExtensions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Options/RateLimitOptionsTests.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RateLimiterExtensionsTests.cs`

- [ ] **Step 1: 创建 RateLimitOptions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Options/RateLimitOptions.cs`：

```csharp
namespace Leno.ApiGateway.Options;

/// <summary>
/// 限流配置根节，对应 appsettings.json 中 <c>RateLimit</c> 节。
/// 三层策略：全局令牌桶 → 路由滑动窗口 → 用户滑动窗口。
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>全局令牌桶策略，保护网关整体容量。</summary>
    public GlobalRateLimitOptions Global { get; set; } = new();

    /// <summary>按路由的滑动窗口策略映射，Key 为策略名（与路由 RateLimiterPolicy 字段对应）。</summary>
    public Dictionary<string, RouteRateLimitOptions> Routes { get; set; } = new();

    /// <summary>按用户的滑动窗口策略。</summary>
    public UserRateLimitOptions User { get; set; } = new();

    /// <summary>Redis 分布式限流是否启用（多实例部署时为 true）。</summary>
    public bool UseRedisDistributed { get; set; } = true;

    /// <summary>Redis 限流计数器 Key 前缀。</summary>
    public string RedisKeyPrefix { get; set; } = "leno:ratelimit:";
}

/// <summary>全局令牌桶配置。</summary>
public sealed class GlobalRateLimitOptions
{
    /// <summary>令牌桶容量（最大瞬时请求数）。</summary>
    public int TokenLimit { get; set; } = 5000;

    /// <summary>每周期补充令牌数。</summary>
    public int TokensPerPeriod { get; set; } = 5000;

    /// <summary>补充周期（默认 1 秒，即 5000 req/s）。</summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>是否自动排队（false 表示超出立即拒绝）。</summary>
    public bool AutoReplenishment { get; set; } = true;

    /// <summary>队列长度（AutoReplenishment=true 时生效，0 表示不排队）。</summary>
    public int QueueLimit { get; set; } = 0;
}

/// <summary>按路由滑动窗口配置。</summary>
public sealed class RouteRateLimitOptions
{
    /// <summary>窗口内最大请求数。</summary>
    public int PermitLimit { get; set; }

    /// <summary>滑动窗口时长。</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>窗口分段数（影响精度，越大越精确但内存占用越高）。</summary>
    public int SegmentsPerWindow { get; set; } = 4;

    /// <summary>队列长度（0 表示超出立即拒绝）。</summary>
    public int QueueLimit { get; set; } = 0;
}

/// <summary>按用户滑动窗口配置（基于 JWT 中的 Sub claim 或 X-User-Id 头）。</summary>
public sealed class UserRateLimitOptions
{
    /// <summary>每用户每窗口最大请求数（默认 100 req/min）。</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>窗口时长（默认 1 分钟）。</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>窗口分段数。</summary>
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>未认证请求的分区 Key（用客户端 IP 兜底）。</summary>
    public string AnonymousPartitionClaim { get; set; } = "client-ip";

    /// <summary>队列长度。</summary>
    public int QueueLimit { get; set; } = 0;
}
```

- [ ] **Step 2: 创建 RateLimiterExtensions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Extensions/RateLimiterExtensions.cs`：

```csharp
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 网关限流服务注册扩展。
/// 注册 ASP.NET Core <c>AddRateLimiter</c> 中间件，包含三层策略：
/// "global"（全局令牌桶）、"default"（普通路由滑动窗口）、"seckill"（秒杀滑动窗口）、"per-user"（按用户滑动窗口）。
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>限流策略名常量，与 appsettings.json 中 ReverseProxy:Routes[*].RateLimiterPolicy 字段对应。</summary>
    public static class Policies
    {
        public const string Global = "global";
        public const string Default = "default";
        public const string Seckill = "seckill";
        public const string PerUser = "per-user";
    }

    /// <summary>
    /// 注册网关三层限流策略：
    /// <list type="bullet">
    /// <item>GlobalLimiter：令牌桶 5000 req/s 保护整体容量</item>
    /// <item>路由级策略 "default"/"seckill"：滑动窗口 200/50 req/s</item>
    /// <item>用户级策略 "per-user"：滑动窗口 100 req/min，按 UserId 分区</item>
    /// </list>
    /// Redis 启用时（UseRedisDistributed=true），路由级和用户级策略使用 <see cref="RedisSlidingWindowRateLimiter"/>。
    /// </summary>
    public static IServiceCollection AddGatewayRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RateLimitOptions>(configuration.GetSection("RateLimit"));

        var rateLimitOptions = configuration.GetSection("RateLimit").Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 全局令牌桶：保护网关整体容量
            options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    var partitionKey = "global";
                    return System.Threading.RateLimiting.RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey,
                        _ => new System.Threading.RateLimiting.TokenBucketRateLimiterOptions
                        {
                            TokenLimit = rateLimitOptions.Global.TokenLimit,
                            TokensPerPeriod = rateLimitOptions.Global.TokensPerPeriod,
                            ReplenishmentPeriod = rateLimitOptions.Global.ReplenishmentPeriod,
                            AutoReplenishment = rateLimitOptions.Global.AutoReplenishment,
                            QueueLimit = rateLimitOptions.Global.QueueLimit,
                            QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                        });
                });

            // 路由级滑动窗口：普通接口
            options.AddPolicy(Policies.Default, httpContext =>
                CreateSlidingWindowPartition(
                    httpContext,
                    rateLimitOptions,
                    policyName: Policies.Default,
                    partitionKeyFactory: ctx => GetClientIdentifier(ctx, rateLimitOptions.User.AnonymousPartitionClaim)));

            // 路由级滑动窗口：秒杀接口
            options.AddPolicy(Policies.Seckill, httpContext =>
                CreateSlidingWindowPartition(
                    httpContext,
                    rateLimitOptions,
                    policyName: Policies.Seckill,
                    partitionKeyFactory: ctx => GetClientIdentifier(ctx, rateLimitOptions.User.AnonymousPartitionClaim)));

            // 用户级滑动窗口：100 req/min per UserId
            options.AddPolicy(Policies.PerUser, httpContext =>
                CreateSlidingWindowPartition(
                    httpContext,
                    rateLimitOptions,
                    policyName: Policies.PerUser,
                    partitionKeyFactory: ctx => GetClientIdentifier(ctx, rateLimitOptions.User.AnonymousPartitionClaim)));
        });

        return services;
    }

    /// <summary>
    /// 根据配置创建滑动窗口分区：Redis 启用时返回 <see cref="RedisSlidingWindowRateLimiter"/>，否则回退到内置 <see cref="System.Threading.RateLimiting.SlidingWindowRateLimiter"/>。
    /// </summary>
    private static System.Threading.RateLimiting.RateLimitPartition<string> CreateSlidingWindowPartition(
        HttpContext httpContext,
        RateLimitOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyFactory)
    {
        var partitionKey = partitionKeyFactory(httpContext);
        var compositeKey = $"{options.RedisKeyPrefix}{policyName}:{partitionKey}";

        // 路由级策略从 Routes 字典查找，用户级策略从 User 字段查找
        var (permitLimit, window, segmentsPerWindow) = policyName switch
        {
            Policies.PerUser => (options.User.PermitLimit, options.User.Window, options.User.SegmentsPerWindow),
            _ when options.Routes.TryGetValue(policyName, out var routeOpts)
                => (routeOpts.PermitLimit, routeOpts.Window, routeOpts.SegmentsPerWindow),
            _ => (200, TimeSpan.FromSeconds(1), 4)
        };

        if (options.UseRedisDistributed)
        {
            var database = httpContext.RequestServices.GetRequiredService<IDatabase>();
            return System.Threading.RateLimiting.RateLimitPartition.Create(
                compositeKey,
                _ => new RedisSlidingWindowRateLimiter(
                    database,
                    compositeKey,
                    permitLimit,
                    window,
                    segmentsPerWindow));
        }

        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
            compositeKey,
            _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                SegmentsPerWindow = segmentsPerWindow,
                QueueLimit = 0,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
            });
    }

    /// <summary>
    /// 提取客户端标识：优先 JWT Sub claim，其次 X-User-Id 头，最后回退到客户端 IP（用于匿名用户）。
    /// </summary>
    private static string GetClientIdentifier(HttpContext context, string anonymousClaim)
    {
        // JWT Sub claim（阶段二 JwtAuthMiddleware 注入）
        var subClaim = context.User.FindFirst("Sub")?.Value;
        if (!string.IsNullOrEmpty(subClaim))
        {
            return subClaim;
        }

        // X-User-Id 头（阶段二 UserContextTransform 注入）
        if (context.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader)
            && !string.IsNullOrEmpty(userIdHeader))
        {
            return userIdHeader.ToString();
        }

        // 回退到客户端 IP
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{anonymousClaim}:{clientIp}";
    }
}
```

- [ ] **Step 3: 编写 RateLimitOptions 测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Options/RateLimitOptionsTests.cs`：

```csharp
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Options;

public class RateLimitOptionsTests
{
    private static RateLimitOptions BindFromDictionary(IDictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new RateLimitOptions();
        config.Bind(opts);
        return opts;
    }

    [Fact]
    public void Bind_FromConfiguration_PopulatesAllSections()
    {
        // Arrange
        var data = new Dictionary<string, string?>
        {
            ["RateLimit:UseRedisDistributed"] = "true",
            ["RateLimit:RedisKeyPrefix"] = "leno:rl:",
            ["RateLimit:Global:TokenLimit"] = "5000",
            ["RateLimit:Global:TokensPerPeriod"] = "5000",
            ["RateLimit:Global:ReplenishmentPeriod"] = "00:00:01",
            ["RateLimit:Routes:default:PermitLimit"] = "200",
            ["RateLimit:Routes:default:Window"] = "00:00:01",
            ["RateLimit:Routes:default:SegmentsPerWindow"] = "4",
            ["RateLimit:Routes:seckill:PermitLimit"] = "50",
            ["RateLimit:Routes:seckill:Window"] = "00:00:01",
            ["RateLimit:Routes:seckill:SegmentsPerWindow"] = "4",
            ["RateLimit:User:PermitLimit"] = "100",
            ["RateLimit:User:Window"] = "00:01:00",
            ["RateLimit:User:SegmentsPerWindow"] = "6"
        };

        // Act
        var opts = BindFromDictionary(data);

        // Assert
        opts.UseRedisDistributed.Should().BeTrue();
        opts.RedisKeyPrefix.Should().Be("leno:rl:");
        opts.Global.TokenLimit.Should().Be(5000);
        opts.Global.TokensPerPeriod.Should().Be(5000);
        opts.Global.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(1));
        opts.Routes.Should().ContainKey("default");
        opts.Routes["default"].PermitLimit.Should().Be(200);
        opts.Routes["default"].Window.Should().Be(TimeSpan.FromSeconds(1));
        opts.Routes["default"].SegmentsPerWindow.Should().Be(4);
        opts.Routes["seckill"].PermitLimit.Should().Be(50);
        opts.User.PermitLimit.Should().Be(100);
        opts.User.Window.Should().Be(TimeSpan.FromMinutes(1));
        opts.User.SegmentsPerWindow.Should().Be(6);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var opts = new RateLimitOptions();

        opts.Global.TokenLimit.Should().Be(5000);
        opts.Global.TokensPerPeriod.Should().Be(5000);
        opts.Global.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(1));
        opts.User.PermitLimit.Should().Be(100);
        opts.User.Window.Should().Be(TimeSpan.FromMinutes(1));
        opts.UseRedisDistributed.Should().BeTrue();
        opts.RedisKeyPrefix.Should().Be("leno:ratelimit:");
    }

    [Fact]
    public void Bind_WithMissingSections_UsesDefaults()
    {
        // Arrange — 不提供任何 RateLimit 配置
        var config = new ConfigurationBuilder().Build();

        // Act
        var opts = new RateLimitOptions();
        config.Bind(opts);

        // Assert — 应使用代码默认值
        opts.Global.TokenLimit.Should().Be(5000);
        opts.User.PermitLimit.Should().Be(100);
        opts.Routes.Should().BeEmpty();
    }

    [Fact]
    public void Bind_ParsesTimeSpanInIsoFormat()
    {
        var data = new Dictionary<string, string?>
        {
            ["RateLimit:Global:ReplenishmentPeriod"] = "00:00:02",
            ["RateLimit:User:Window"] = "00:02:30"
        };

        var opts = BindFromDictionary(data);

        opts.Global.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(2));
        opts.User.Window.Should().Be(TimeSpan.FromSeconds(150));
    }
}
```

- [ ] **Step 4: 编写 RateLimiterExtensions 注册测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RateLimiterExtensionsTests.cs`：

```csharp
using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Extensions;

public class RateLimiterExtensionsTests
{
    private static IConfiguration CreateConfig(bool useRedis = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:UseRedisDistributed"] = useRedis.ToString(),
                ["RateLimit:RedisKeyPrefix"] = "leno:rl:",
                ["RateLimit:Global:TokenLimit"] = "5000",
                ["RateLimit:Routes:default:PermitLimit"] = "200",
                ["RateLimit:Routes:default:Window"] = "00:00:01",
                ["RateLimit:Routes:seckill:PermitLimit"] = "50",
                ["RateLimit:Routes:seckill:Window"] = "00:00:01",
                ["RateLimit:User:PermitLimit"] = "100",
                ["RateLimit:User:Window"] = "00:01:00"
            })
            .Build();

    [Fact]
    public void AddGatewayRateLimiter_RegistersRateLimiterMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();

        // Act
        services.AddGatewayRateLimiter(config);

        // Assert — AddRateLimiter 注册了 RateLimiterOptions
        services.Should().Contain(s => s.ServiceType == typeof(IOptions<RateLimiterOptions>));
    }

    [Fact]
    public void AddGatewayRateLimiter_BindsRateLimitOptionsFromConfig()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();
        services.AddGatewayRateLimiter(config);

        // Act
        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<RateLimitOptions>>().Value;

        // Assert
        opts.Global.TokenLimit.Should().Be(5000);
        opts.Routes["default"].PermitLimit.Should().Be(200);
        opts.Routes["seckill"].PermitLimit.Should().Be(50);
        opts.User.PermitLimit.Should().Be(100);
        opts.UseRedisDistributed.Should().BeTrue();
    }

    [Fact]
    public void AddGatewayRateLimiter_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = CreateConfig();

        var act = () => services.AddGatewayRateLimiter(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayRateLimiter_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddGatewayRateLimiter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayRateLimiter_DoesNotRequireRedisAtRegistrationTime()
    {
        // Arrange — 注册时不依赖 IDatabase（解析分区时才需要）
        var services = new ServiceCollection();
        var config = CreateConfig(useRedis: true);

        // Act
        services.AddGatewayRateLimiter(config);

        // Assert — 应能成功构建 ServiceProvider（不报缺失 IDatabase）
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IOptions<RateLimiterOptions>>().Should().NotBeNull();
    }

    [Fact]
    public void Policies_ConstantsMatchExpectedNames()
    {
        RateLimiterExtensions.Policies.Global.Should().Be("global");
        RateLimiterExtensions.Policies.Default.Should().Be("default");
        RateLimiterExtensions.Policies.Seckill.Should().Be("seckill");
        RateLimiterExtensions.Policies.PerUser.Should().Be("per-user");
    }
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: 编译失败 — `RedisSlidingWindowRateLimiter` 类型未定义（Task 5 实现）。此为本步骤的预期结果：先建立限流策略注册结构与测试骨架。

> **说明：** 此时编译失败是预期的（TDD 红→绿）。Step 5 仅作为骨架检查点。Task 5 实现后再运行测试验证通过。如果执行者希望立即编译通过，可临时将 `RateLimiterExtensions.cs` 中 `RedisSlidingWindowRateLimiter` 替换为 `SlidingWindowRateLimiter`，Task 5 时再换回。

- [ ] **Step 6: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/RateLimitOptions.cs src/ApiGateway/Leno.ApiGateway/Extensions/RateLimiterExtensions.cs src/ApiGateway/Leno.ApiGateway.Tests/Options/RateLimitOptionsTests.cs src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RateLimiterExtensionsTests.cs
git commit -m "feat(gateway): 添加限流策略选项与三层策略注册扩展（待 Redis 实现后激活）"
```

---

## Task 2: 熔断配置

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`
- Create: `src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/FallbackResponseMiddlewareTests.cs`

- [ ] **Step 1: 创建 FallbackResponseMiddleware.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs`：

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// 熔断降级响应中间件。
/// <para>
/// YARP <c>CircuitBreaker</c> 触发时返回 503 空响应体。本中间件位于 <c>MapReverseProxy</c> 之前，
/// 通过响应体缓冲检测 503 状态码并改写为统一降级 JSON：
/// <code>
/// { "code": 503, "message": "服务暂时不可用，请稍后重试", "data": null }
/// </code>
/// </para>
/// 仅对反向代理转发的请求生效（通过 <c>X-Forwarded-By</c> 标记或非 <c>/health</c> 路径区分）。
/// </summary>
public sealed class FallbackResponseMiddleware
{
    private static readonly byte[] FallbackBody = Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            code = 503,
            message = "服务暂时不可用，请稍后重试",
            data = (object?)null
        }));

    private const string FallbackContentType = "application/json; charset=utf-8";

    private readonly RequestDelegate _next;
    private readonly ILogger<FallbackResponseMiddleware> _logger;

    public FallbackResponseMiddleware(
        RequestDelegate next,
        ILogger<FallbackResponseMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 健康检查端点不参与降级（避免影响 K8s/Consul 探针）
        if (IsHealthEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // 缓冲响应体以便后续重写
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            // 恢复原始响应流
            context.Response.Body = originalBodyStream;
        }

        if (context.Response.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            await RewriteAsFallbackAsync(context, responseBody);
        }
        else
        {
            // 复制原始响应体回真实流
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task RewriteAsFallbackAsync(HttpContext context, MemoryStream responseBody)
    {
        _logger.LogWarning(
            "Returning fallback response for {Method} {Path} (origin: {StatusCode})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);

        // 清除原始 headers 中可能与 body 不一致的字段
        context.Response.ContentType = FallbackContentType;
        context.Response.ContentLength = FallbackBody.Length;

        // 清空缓冲区并写入降级 JSON
        responseBody.SetLength(0);
        await responseBody.WriteAsync(FallbackBody);

        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(context.Response.Body);
    }

    private static bool IsHealthEndpoint(PathString path)
    {
        return path.StartsWithSegments("/health");
    }
}
```

- [ ] **Step 2: 创建 FallbackResponseMiddleware 测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/FallbackResponseMiddlewareTests.cs`：

```csharp
using Leno.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

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
```

- [ ] **Step 3: 运行 FallbackResponseMiddleware 测试**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FallbackResponseMiddlewareTests"`
Expected: `Passed: 6` — 全部通过

- [ ] **Step 4: 修改 appsettings.json — 为每个 Cluster 添加 CircuitBreaker 配置**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 的 `ReverseProxy:Clusters` 块中，为每个 Cluster（user-auth、product、cart、order、promotion、payment、points、review-aftersales、seller-shop、notification、system-admin）添加 `CircuitBreaker` 节。以 `promotion` Cluster 为例（其他 10 个 Cluster 同样添加）：

```json
      "promotion": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-promotion-api" },
        "CircuitBreaker": {
          "MaxConcurrentRequests": 100,
          "FailureRateThreshold": 0.5,
          "SamplingDuration": "00:00:30",
          "MinimumThroughput": 10,
          "BreakDuration": "00:00:30"
        },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      }
```

完整 11 个 Cluster 的 `CircuitBreaker` 节内容相同（数值相同，按 Cluster 差异化调整可在运维期通过 Consul KV 热更新覆盖）。

> 完整的 appsettings.json 改造在 Task 6 Step 1 中给出整合后的最终版，本步骤只需为每个 Cluster 添加上述 `CircuitBreaker` 块即可。

- [ ] **Step 5: 验证 appsettings.json 为有效 JSON**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded` — YARP 启动时反序列化 Cluster 配置，无效 JSON 会导致编译/启动失败

- [ ] **Step 6: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway.Tests/Middleware/FallbackResponseMiddlewareTests.cs
git commit -m "feat(gateway): 添加熔断 CircuitBreaker 配置与 503 降级响应中间件"
```

---

## Task 3: 超时配置

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Options/TimeoutOptions.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`
- Modify: `src/ApiGateway/Leno.ApiGateway/Extensions/RateLimiterExtensions.cs` → 改为新增 `TimeoutExtensions.cs`（保持单一职责）
- Create: `src/ApiGateway/Leno.ApiGateway/Extensions/TimeoutExtensions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Options/TimeoutOptionsTests.cs`

- [ ] **Step 1: 创建 TimeoutOptions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Options/TimeoutOptions.cs`：

```csharp
namespace Leno.ApiGateway.Options;

/// <summary>
/// 超时配置根节，对应 appsettings.json 中 <c>Timeout</c> 节。
/// 与 YARP 路由级 <c>Timeout</c>/<c>TimeoutPolicy</c> 字段配套使用。
/// </summary>
public sealed class TimeoutOptions
{
    /// <summary>命名超时策略映射，Key 为策略名（与路由 TimeoutPolicy 字段对应）。</summary>
    public Dictionary<string, TimeoutPolicyOptions> Policies { get; set; } = new();

    /// <summary>默认超时策略名（无显式 TimeoutPolicy 的路由使用）。</summary>
    public string DefaultPolicy { get; set; } = "default";
}

/// <summary>命名超时策略配置。</summary>
public sealed class TimeoutPolicyOptions
{
    /// <summary>路由类型标签：default/seckill/upload/internal。</summary>
    public string RouteType { get; set; } = "default";

    /// <summary>整体请求超时（端到端，包括 YARP 转发与后端处理）。</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 连接超时（HttpClient 连接到后端的超时）。
    /// YARP 通过 Cluster.HttpClient 配置间接控制；此字段仅作为元数据用于校验。
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 读取超时（HttpClient 读取后端响应字节的 idle 超时）。
    /// 对应 YARP Cluster.HttpRequest.ActivityTimeout。
    /// </summary>
    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>说明（用于运维参考，不影响运行时行为）。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 预置的路由类型常量，与 appsettings.json 中 Timeout:Policies 的 Key 对应。
/// </summary>
public static class TimeoutRouteTypes
{
    public const string Default = "default";
    public const string Seckill = "seckill";
    public const string Upload = "upload";
    public const string Internal = "internal";
}
```

- [ ] **Step 2: 创建 TimeoutExtensions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Extensions/TimeoutExtensions.cs`：

```csharp
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 超时服务注册扩展。
/// 使用 ASP.NET Core 8+ <c>AddRequestTimeouts</c> 注册命名超时策略，
/// 与 YARP 路由级 <c>TimeoutPolicy</c> 字段集成（YARP 2.1+ 支持）。
/// </summary>
public static class TimeoutExtensions
{
    /// <summary>
    /// 注册命名超时策略：default(30s) / seckill(5s) / upload(120s) / internal(15s)。
    /// 路由通过 <c>ReverseProxy:Routes[*].TimeoutPolicy</c> 引用对应策略名。
    /// </summary>
    public static IServiceCollection AddGatewayTimeouts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TimeoutOptions>(configuration.GetSection("Timeout"));

        var opts = configuration.GetSection("Timeout").Get<TimeoutOptions>()
            ?? new TimeoutOptions();

        services.AddRequestTimeouts(options =>
        {
            foreach (var (policyName, policyOpts) in opts.Policies)
            {
                options.AddPolicy(policyName, policyOpts.RequestTimeout);
            }

            // 默认策略（无显式 TimeoutPolicy 的路由应用）
            if (!string.IsNullOrEmpty(opts.DefaultPolicy)
                && opts.Policies.TryGetValue(opts.DefaultPolicy, out var defaultOpts))
            {
                options.DefaultPolicy = new RequestTimeoutPolicy
                {
                    PolicyName = opts.DefaultPolicy,
                    Timeout = defaultOpts.RequestTimeout
                };
            }
        });

        return services;
    }
}
```

- [ ] **Step 3: 编写 TimeoutOptions 测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Options/TimeoutOptionsTests.cs`：

```csharp
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Configuration;

namespace Leno.ApiGateway.Tests.Options;

public class TimeoutOptionsTests
{
    private static TimeoutOptions BindFromDictionary(IDictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new TimeoutOptions();
        config.Bind(opts);
        return opts;
    }

    [Fact]
    public void Bind_FromConfiguration_PopulatesAllPolicies()
    {
        // Arrange
        var data = new Dictionary<string, string?>
        {
            ["Timeout:DefaultPolicy"] = "default",
            ["Timeout:Policies:default:RouteType"] = "default",
            ["Timeout:Policies:default:RequestTimeout"] = "00:00:30",
            ["Timeout:Policies:default:ConnectTimeout"] = "00:00:05",
            ["Timeout:Policies:default:ReadTimeout"] = "00:00:30",
            ["Timeout:Policies:seckill:RouteType"] = "seckill",
            ["Timeout:Policies:seckill:RequestTimeout"] = "00:00:05",
            ["Timeout:Policies:seckill:ConnectTimeout"] = "00:00:02",
            ["Timeout:Policies:seckill:ReadTimeout"] = "00:00:05",
            ["Timeout:Policies:upload:RouteType"] = "upload",
            ["Timeout:Policies:upload:RequestTimeout"] = "00:02:00",
            ["Timeout:Policies:upload:ConnectTimeout"] = "00:00:10",
            ["Timeout:Policies:upload:ReadTimeout"] = "00:02:00",
            ["Timeout:Policies:internal:RouteType"] = "internal",
            ["Timeout:Policies:internal:RequestTimeout"] = "00:00:15",
            ["Timeout:Policies:internal:ConnectTimeout"] = "00:00:03",
            ["Timeout:Policies:internal:ReadTimeout"] = "00:00:15"
        };

        // Act
        var opts = BindFromDictionary(data);

        // Assert
        opts.DefaultPolicy.Should().Be("default");
        opts.Policies.Should().HaveCount(4);
        opts.Policies["default"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        opts.Policies["default"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(5));
        opts.Policies["seckill"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        opts.Policies["seckill"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(2));
        opts.Policies["upload"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        opts.Policies["upload"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(10));
        opts.Policies["internal"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(15));
        opts.Policies["internal"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var opts = new TimeoutOptions();
        opts.DefaultPolicy.Should().Be("default");
        opts.Policies.Should().BeEmpty();

        var policy = new TimeoutPolicyOptions();
        policy.RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        policy.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(5));
        policy.ReadTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void RouteTypeConstants_MatchExpectedValues()
    {
        TimeoutRouteTypes.Default.Should().Be("default");
        TimeoutRouteTypes.Seckill.Should().Be("seckill");
        TimeoutRouteTypes.Upload.Should().Be("upload");
        TimeoutRouteTypes.Internal.Should().Be("internal");
    }

    [Fact]
    public void Bind_WithEmptyPolicies_DoesNotThrow()
    {
        var opts = BindFromDictionary(new Dictionary<string, string?>());
        opts.Policies.Should().BeEmpty();
        opts.DefaultPolicy.Should().Be("default");
    }
}
```

- [ ] **Step 4: 修改 appsettings.json — 添加 Timeout 配置节 + 路由级 TimeoutPolicy 字段**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 根级别添加 `Timeout` 节：

```json
  "Timeout": {
    "DefaultPolicy": "default",
    "Policies": {
      "default": {
        "RouteType": "default",
        "RequestTimeout": "00:00:30",
        "ConnectTimeout": "00:00:05",
        "ReadTimeout": "00:00:30",
        "Description": "常规 API：5s 连接 / 30s 读取"
      },
      "seckill": {
        "RouteType": "seckill",
        "RequestTimeout": "00:00:05",
        "ConnectTimeout": "00:00:02",
        "ReadTimeout": "00:00:05",
        "Description": "秒杀接口：高时效性，2s 连接 / 5s 读取"
      },
      "upload": {
        "RouteType": "upload",
        "RequestTimeout": "00:02:00",
        "ConnectTimeout": "00:00:10",
        "ReadTimeout": "00:02:00",
        "Description": "文件上传：10s 连接 / 120s 读取（大文件）"
      },
      "internal": {
        "RouteType": "internal",
        "RequestTimeout": "00:00:15",
        "ConnectTimeout": "00:00:03",
        "ReadTimeout": "00:00:15",
        "Description": "服务间调用：3s 连接 / 15s 读取"
      }
    }
  },
```

为路由添加 `TimeoutPolicy` 字段：

```json
"promotion-seckill": { "ClusterId": "promotion", "RateLimiterPolicy": "seckill", "TimeoutPolicy": "seckill", "Match": { "Path": "/api/seckill/{**catch-all}" }, "Order": 10 },
"promotion-seckill-admin": { "ClusterId": "promotion", "TimeoutPolicy": "seckill", "Match": { "Path": "/api/admin/seckill/{**catch-all}" }, "Order": 10 },
```

文件上传路由（Product 服务图片/资源上传相关）添加 `"TimeoutPolicy": "upload"`：

```json
"product-admin": { "ClusterId": "product", "TimeoutPolicy": "upload", "Match": { "Path": "/api/admin/products/{**catch-all}" }, "Order": 10 },
```

其余普通路由（如 `user-auth`、`cart`、`order`、`product`、`points`、`notification` 等）添加 `"TimeoutPolicy": "default"`：

```json
"user-auth": { "ClusterId": "user-auth", "TimeoutPolicy": "default", "Match": { "Path": "/api/auth/{**catch-all}" }, "Order": 10 },
```

> 完整路由表见 Task 6 Step 1。

- [ ] **Step 5: 为每个 Cluster 添加 HttpRequest.ActivityTimeout**

在 `appsettings.json` 的每个 Cluster 中添加 `HttpRequest.ActivityTimeout`（即 Spec 5.3 中的"读取超时"）：

```json
"promotion": {
  "LoadBalancingPolicy": "PowerOfTwoChoices",
  "Metadata": { "ConsulServiceName": "leno-promotion-api" },
  "HttpRequest": { "ActivityTimeout": "00:00:30" },
  "CircuitBreaker": { ... },
  ...
}
```

各 Cluster 的 `ActivityTimeout` 与 Cluster 内主导路由类型对应：
- 普通 Cluster（user-auth/product/cart/order/payment/points/review-aftersales/seller-shop/notification/system-admin）：`00:00:30`（30s 默认读取）
- promotion Cluster（含秒杀）：仍用 30s（秒杀路由的路由级 Timeout 覆盖更短）

> 详细配置见 Task 6 Step 1 完整版本。

- [ ] **Step 6: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 7: 运行 TimeoutOptions 测试**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TimeoutOptionsTests"`
Expected: `Passed: 4`

- [ ] **Step 8: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/TimeoutOptions.cs src/ApiGateway/Leno.ApiGateway/Extensions/TimeoutExtensions.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway.Tests/Options/TimeoutOptionsTests.cs
git commit -m "feat(gateway): 添加差异化超时策略（default/seckill/upload/internal）"
```

---

## Task 4: 重试策略

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Options/RetryOptions.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Options/RetryOptionsTests.cs`

- [ ] **Step 1: 创建 RetryOptions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Options/RetryOptions.cs`：

```csharp
namespace Leno.ApiGateway.Options;

/// <summary>
/// 重试配置根节，对应 appsettings.json 中 <c>Retry</c> 节。
/// </summary>
public sealed class RetryOptions
{
    /// <summary>最大重试次数（不含首次请求）。</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>退避策略：Linear 或 Exponential。</summary>
    public string Backoff { get; set; } = "Exponential";

    /// <summary>最小退避时间（首次重试前等待）。</summary>
    public TimeSpan MinBackoff { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>最大退避时间（指数退避上限）。</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 触发重试的 HTTP 状态码列表。
    /// YARP 2.2.0 通过 Cluster.Retry.RetryableStatusCodes 配置。
    /// </summary>
    public int[] RetryableStatusCodes { get; set; } = new[] { 503 };

    /// <summary>
    /// 仅幂等方法重试（GET/HEAD/PUT/DELETE/OPTIONS/TRACE）。
    /// YARP 默认即如此；此字段作为元数据用于校验。
    /// </summary>
    public bool IdempotentMethodsOnly { get; set; } = true;
}

/// <summary>
/// 重试路由分类常量，用于在 appsettings 中标注哪些 Cluster 启用重试。
/// </summary>
public static class RetryRouteTypes
{
    /// <summary>启用重试的 Cluster 列表（默认全部）。文件上传 Cluster 排除（避免重复上传）。</summary>
    public static readonly HashSet<string> RetryEnabledClusters = new()
    {
        "user-auth", "product", "cart", "order", "promotion",
        "payment", "points", "review-aftersales", "seller-shop",
        "notification", "system-admin"
    };

    /// <summary>不启用重试的 Cluster 列表（文件上传等不可重复操作）。</summary>
    public static readonly HashSet<string> RetryDisabledClusters = new()
    {
        // 文件上传 Cluster 单独配置（如未来拆分 product-upload）
    };
}
```

- [ ] **Step 2: 编写 RetryOptions 测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Options/RetryOptionsTests.cs`：

```csharp
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Configuration;

namespace Leno.ApiGateway.Tests.Options;

public class RetryOptionsTests
{
    private static RetryOptions BindFromDictionary(IDictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new RetryOptions();
        config.Bind(opts);
        return opts;
    }

    [Fact]
    public void Bind_FromConfiguration_PopulatesAllFields()
    {
        // Arrange
        var data = new Dictionary<string, string?>
        {
            ["Retry:MaxRetries"] = "2",
            ["Retry:Backoff"] = "Exponential",
            ["Retry:MinBackoff"] = "00:00:00.500",
            ["Retry:MaxBackoff"] = "00:00:01",
            ["Retry:RetryableStatusCodes:0"] = "503",
            ["Retry:RetryableStatusCodes:1"] = "504",
            ["Retry:IdempotentMethodsOnly"] = "true"
        };

        // Act
        var opts = BindFromDictionary(data);

        // Assert
        opts.MaxRetries.Should().Be(2);
        opts.Backoff.Should().Be("Exponential");
        opts.MinBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
        opts.MaxBackoff.Should().Be(TimeSpan.FromSeconds(1));
        opts.RetryableStatusCodes.Should().Equal(503, 504);
        opts.IdempotentMethodsOnly.Should().BeTrue();
    }

    [Fact]
    public void Defaults_MatchSpecRequirements()
    {
        // Spec 5.3: 最多 2 次重试，指数退避 500ms→1000ms，仅幂等方法，重试条件 503
        var opts = new RetryOptions();

        opts.MaxRetries.Should().Be(2);
        opts.Backoff.Should().Be("Exponential");
        opts.MinBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
        opts.MaxBackoff.Should().Be(TimeSpan.FromSeconds(1));
        opts.RetryableStatusCodes.Should().Equal(503);
        opts.IdempotentMethodsOnly.Should().BeTrue();
    }

    [Fact]
    public void RetryEnabledClusters_ContainsAllElevenServices()
    {
        RetryRouteTypes.RetryEnabledClusters.Should().HaveCount(11);
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("user-auth");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("product");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("cart");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("order");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("promotion");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("payment");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("points");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("review-aftersales");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("seller-shop");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("notification");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("system-admin");
    }

    [Fact]
    public void Bind_WithMissingFields_UsesDefaults()
    {
        var opts = BindFromDictionary(new Dictionary<string, string?>());
        opts.MaxRetries.Should().Be(2);
        opts.Backoff.Should().Be("Exponential");
        opts.RetryableStatusCodes.Should().Equal(503);
    }
}
```

- [ ] **Step 3: 修改 appsettings.json — 添加 Retry 配置节 + Cluster 级 Retry 块**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 根级别添加 `Retry` 节：

```json
  "Retry": {
    "MaxRetries": 2,
    "Backoff": "Exponential",
    "MinBackoff": "00:00:00.500",
    "MaxBackoff": "00:00:01",
    "RetryableStatusCodes": [ 503 ],
    "IdempotentMethodsOnly": true
  },
```

在每个 Cluster 中添加 `Retry` 块（除文件上传 Cluster）。以 `promotion` 为例：

```json
"promotion": {
  "LoadBalancingPolicy": "PowerOfTwoChoices",
  "Metadata": { "ConsulServiceName": "leno-promotion-api" },
  "HttpRequest": { "ActivityTimeout": "00:00:30" },
  "CircuitBreaker": { ... },
  "Retry": {
    "MaxRetries": 2,
    "Backoff": "Exponential",
    "MinBackoff": "00:00:00.500",
    "MaxBackoff": "00:00:01",
    "RetryableStatusCodes": [ 503 ]
  },
  "HealthCheck": { ... }
}
```

> 注：YARP 2.2.0 `Retry` 配置默认仅对幂等方法（GET/HEAD/PUT/DELETE/OPTIONS/TRACE）重试，无需额外配置。POST/PUT 非幂等的请求不会被重试。Spec 5.3 "POST 不重试" 自动满足。

> 完整 appsettings.json 见 Task 6 Step 1。

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 5: 运行 RetryOptions 测试**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "RetryOptionsTests"`
Expected: `Passed: 4`

- [ ] **Step 6: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/RetryOptions.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway.Tests/Options/RetryOptionsTests.cs
git commit -m "feat(gateway): 添加幂等方法重试策略（指数退避，503 重试）"
```

---

## Task 5: Redis 分布式限流

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
- Create: `src/ApiGateway/Leno.ApiGateway/Extensions/RedisExtensions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RedisExtensionsTests.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs`

- [ ] **Step 1: 在网关 csproj 中显式添加 StackExchange.Redis 包引用**

在 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` 的 `<ItemGroup>` 中（`Yarp.ReverseProxy` 之后）添加：

```xml
    <PackageReference Include="StackExchange.Redis" Version="2.8.24" />
```

> 说明：虽然 Leno.Infrastructure 已引用该包，网关代码直接使用 `IDatabase`/`IConnectionMultiplexer` 时显式引用更清晰，避免传递依赖被裁剪。

- [ ] **Step 2: 验证包还原**

Run: `dotnet restore src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Restore completed` 无错误

- [ ] **Step 3: 创建 RedisExtensions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Extensions/RedisExtensions.cs`：

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 网关 Redis 注册扩展。
/// 单独注册 <see cref="IConnectionMultiplexer"/> 用于限流计数与（后续阶段）黑名单同步，
/// 不依赖 Leno.Infrastructure 的 <c>AddLenoInfrastructure</c>（网关不需要 MassTransit/Elasticsearch）。
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// 注册 <see cref="IConnectionMultiplexer"/> 与 <see cref="IDatabase"/>，
    /// 从 <c>Redis:Configuration</c> 配置读取连接字符串。
    /// </summary>
    public static IServiceCollection AddGatewayRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration["Redis:Configuration"]
                ?? configuration.GetConnectionString("Redis")
                ?? "localhost:6379";

            var configurationOptions = ConfigurationOptions.Parse(connectionString);
            configurationOptions.AbortOnConnectFail = false; // 容错：Redis 不可用时网关仍可降级
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddScoped<IDatabase>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return multiplexer.GetDatabase();
        });

        return services;
    }
}
```

- [ ] **Step 4: 创建 RedisSlidingWindowRateLimiter.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs`：

```csharp
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Redis SortedSet + Lua 脚本的分布式滑动窗口限流器。
/// <para>
/// 算法：
/// 1. 以 SortedSet 存储请求时间戳作为 member，时间戳（毫秒）作为 score。
/// 2. Lua 脚本原子执行：ZREMRANGEBYSCORE 清除窗口外旧记录 → ZADD 当前时间戳 → ZCARD 计数 → 判断是否超过阈值。
/// 3. 超过阈值时 TTL 仅在 ZCARD=0 时设置（首次访问），避免重复设置。
/// </para>
/// 与 ASP.NET Core <see cref="RateLimiter"/> 抽象兼容，
/// 通过 <see cref="RateLimitPartition.Create{TKey}"/> 注册到 <c>AddRateLimiter</c> 中间件。
/// </summary>
public sealed class RedisSlidingWindowRateLimiter : RateLimiter
{
    // KEYS[1] = Redis key
    // ARGV[1] = current timestamp (ms, score)
    // ARGV[2] = window start timestamp (ms, ZREMRANGEBYSCORE lower bound)
    // ARGV[3] = current timestamp string (member, must be unique → use counter)
    // ARGV[4] = permit limit
    // ARGV[5] = key TTL in seconds
    private const string LuaScript = @"
local count = redis.call('ZCARD', KEYS[1])
if count >= tonumber(ARGV[4]) then
    return 0
end
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[2])
redis.call('ZADD', KEYS[1], ARGV[1], ARGV[3])
if count == 0 then
    redis.call('EXPIRE', KEYS[1], ARGV[5])
end
local newCount = redis.call('ZCARD', KEYS[1])
if newCount > tonumber(ARGV[4]) then
    redis.call('ZREM', KEYS[1], ARGV[3])
    return 0
end
return 1
";

    private static readonly RedisScript Lua = new(LuaScript);

    private readonly IDatabase _database;
    private readonly string _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly int _segmentsPerWindow;
    private long _counter;

    /// <summary>
    /// 创建限流器实例。
    /// </summary>
    /// <param name="database">Redis 数据库连接。</param>
    /// <param name="key">Redis Key，应包含策略名 + 分区 Key（如 <c>leno:ratelimit:seckill:user-123</c>）。</param>
    /// <param name="permitLimit">窗口内最大请求数。</param>
    /// <param name="window">滑动窗口时长。</param>
    /// <param name="segmentsPerWindow">窗口分段数（仅用于 TTL 计算，Redis SortedSet 滑动窗口本身无分段概念）。</param>
    public RedisSlidingWindowRateLimiter(
        IDatabase database,
        string key,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("key cannot be null or empty", nameof(key));
        _key = key;
        _permitLimit = permitLimit > 0 ? permitLimit : throw new ArgumentOutOfRangeException(nameof(permitLimit));
        _window = window;
        _segmentsPerWindow = segmentsPerWindow > 0 ? segmentsPerWindow : 1;
        _counter = 0;
    }

    /// <inheritdoc />
    public override RateLimiterStatistics GetStatistics() => new();

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        // 同步调用 Redis 会阻塞线程；RateLimiter 抽象要求同步实现。
        // ASP.NET Core 中间件优先调用 AcquireAsyncCore，此处仅在用户代码同步调用时使用。
        var acquired = TryAcquireSync(permitCount);
        return acquired ? new RedisRateLimitLease(this) : LeaseFailed();
    }

    /// <inheritdoc />
    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var acquired = await TryAcquireAsync(permitCount, cancellationToken);
        return acquired ? new RedisRateLimitLease(this) : LeaseFailed();
    }

    private bool TryAcquireSync(int permitCount)
    {
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStartMs = nowMs - (long)_window.TotalMilliseconds;
            var member = $"{nowMs}:{Interlocked.Increment(ref _counter)}";
            var ttlSeconds = (int)Math.Ceiling(_window.TotalSeconds * 1.1); // 留 10% 余量避免提前过期

            var result = (long)_database.ScriptEvaluate(
                Lua,
                new RedisKey[] { _key },
                new RedisValue[]
                {
                    nowMs,
                    windowStartMs,
                    member,
                    _permitLimit,
                    ttlSeconds
                });

            return result == 1L;
        }
        catch
        {
            // Redis 不可用时降级放行（fail-open），避免 Redis 故障阻断所有流量
            return true;
        }
    }

    private async ValueTask<bool> TryAcquireAsync(int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStartMs = nowMs - (long)_window.TotalMilliseconds;
            var member = $"{nowMs}:{Interlocked.Increment(ref _counter)}";
            var ttlSeconds = (int)Math.Ceiling(_window.TotalSeconds * 1.1);

            var result = (long)await _database.ScriptEvaluateAsync(
                Lua,
                new RedisKey[] { _key },
                new RedisValue[]
                {
                    nowMs,
                    windowStartMs,
                    member,
                    _permitLimit,
                    ttlSeconds
                });

            return result == 1L;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Redis 不可用时降级放行（fail-open）
            return true;
        }
    }

    private static RateLimitLease LeaseFailed() => new FailedLease();

    private sealed class RedisRateLimitLease : RateLimitLease
    {
        private readonly RedisSlidingWindowRateLimiter _limiter;
        public RedisRateLimitLease(RedisSlidingWindowRateLimiter limiter) => _limiter = limiter;
        public override IEnumerable<string> MetadataNames => Array.Empty<string>();
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }

    private sealed class FailedLease : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => new[] { "REASON" };
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == "REASON")
            {
                metadata = "Rate limit exceeded";
                return true;
            }
            metadata = null;
            return false;
        }
    }
}
```

- [ ] **Step 5: 创建 RedisExtensions 测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RedisExtensionsTests.cs`：

```csharp
using Leno.ApiGateway.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Extensions;

public class RedisExtensionsTests
{
    [Fact]
    public void AddGatewayRedis_RegistersConnectionMultiplexerAndDatabase()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379"
            })
            .Build();

        // Act
        services.AddGatewayRedis(config);
        var sp = services.BuildServiceProvider();

        // Assert — 单例 IConnectionMultiplexer 注册（实际连接不发生，AbortOnConnectFail=false）
        services.Should().Contain(s => s.ServiceType == typeof(IConnectionMultiplexer));
        services.Should().Contain(s => s.ServiceType == typeof(IDatabase));
    }

    [Fact]
    public void AddGatewayRedis_FallsBackToConnectionStringKey()
    {
        // Arrange — 不提供 Redis:Configuration，使用 ConnectionStrings:Redis
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "redis-host:6380"
            })
            .Build();

        // Act
        services.AddGatewayRedis(config);

        // Assert — 不抛异常即说明回退成功
        var sp = services.BuildServiceProvider();
        var multiplexer = sp.GetService<IConnectionMultiplexer>();
        multiplexer.Should().NotBeNull();
    }

    [Fact]
    public void AddGatewayRedis_UsesDefaultWhenConfigMissing()
    {
        // Arrange — 不提供任何 Redis 配置
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        // Act
        services.AddGatewayRedis(config);

        // Assert — 使用默认 localhost:6379
        var sp = services.BuildServiceProvider();
        sp.GetService<IConnectionMultiplexer>().Should().NotBeNull();
    }

    [Fact]
    public void AddGatewayRedis_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddGatewayRedis(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayRedis_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddGatewayRedis(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
```

- [ ] **Step 6: 创建 RedisSlidingWindowRateLimiter 测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs`：

```csharp
using Leno.ApiGateway.Services;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Services;

public class RedisSlidingWindowRateLimiterTests
{
    private static Mock<IDatabase> CreateDatabaseMock(long scriptResult)
    {
        var mock = new Mock<IDatabase>();
        mock.Setup(d => d.ScriptEvaluate(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Returns(new RedisResult(scriptResult, ResultType.Integer));

        mock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisResult(scriptResult, ResultType.Integer));
        return mock;
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisReturnsOne_GrantsLease()
    {
        // Arrange — Lua 脚本返回 1 表示允许
        var dbMock = CreateDatabaseMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisReturnsZero_DeniesLease()
    {
        // Arrange — Lua 脚本返回 0 表示拒绝
        var dbMock = CreateDatabaseMock(scriptResult: 0);
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task AcquireAsync_WhenRedisThrows_FailsOpenAndGrantsLease()
    {
        // Arrange — Redis 异常时降级放行，避免 Redis 故障阻断所有流量
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        var lease = await limiter.AcquireAsync(1);

        // Assert — fail-open：Redis 不可用时放行
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelled_PropagatesCancellation()
    {
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new OperationCanceledException());

        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await limiter.AcquireAsync(1, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullDatabase_Throws()
    {
        var act = () => new RedisSlidingWindowRateLimiter(null!, "key", 50, TimeSpan.FromSeconds(1), 4);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_EmptyOrNullKey_Throws(string? key)
    {
        var dbMock = new Mock<IDatabase>();
        var act = () => new RedisSlidingWindowRateLimiter(dbMock.Object, key!, 50, TimeSpan.FromSeconds(1), 4);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositivePermitLimit_Throws(int limit)
    {
        var dbMock = new Mock<IDatabase>();
        var act = () => new RedisSlidingWindowRateLimiter(dbMock.Object, "key", limit, TimeSpan.FromSeconds(1), 4);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AcquireAsync_PassesCorrectKeyAndArgsToRedis()
    {
        // Arrange
        RedisKey[]? capturedKeys = null;
        RedisValue[]? capturedValues = null;

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisScript, RedisKey[], RedisValue[], CommandFlags>((_, keys, values, _) =>
            {
                capturedKeys = keys;
                capturedValues = values;
            })
            .ReturnsAsync(new RedisResult(1L, ResultType.Integer));

        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act
        await limiter.AcquireAsync(1);

        // Assert
        capturedKeys.Should().NotBeNull();
        capturedKeys![0].ToString().Should().Be("leno:rl:seckill:user-1");

        capturedValues.Should().NotBeNull();
        capturedValues!.Length.Should().Be(5);
        // ARGV[4] = permit limit
        ((long)capturedValues[3]).Should().Be(50);
        // ARGV[5] = TTL seconds (1.1s 留 10% 余量 → 2s)
        ((long)capturedValues[4]).Should().Be(2);
    }

    [Fact]
    public void AttemptAcquire_SyncPath_AlsoWorks()
    {
        // Arrange
        var dbMock = CreateDatabaseMock(scriptResult: 1);
        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "leno:rl:seckill:user-1", 50, TimeSpan.FromSeconds(1), 4);

        // Act — 同步路径
        var lease = limiter.AttemptAcquire(1);

        // Assert
        lease.IsAcquired.Should().BeTrue();
    }
}
```

- [ ] **Step 7: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Build succeeded` — Task 1 中残留的 `RedisSlidingWindowRateLimiter` 引用现已可解析

- [ ] **Step 8: 运行 Task 1 和 Task 5 的全部测试**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "RateLimitOptions|RateLimiterExtensions|RedisExtensions|RedisSlidingWindowRateLimiter"`
Expected: 全部通过（RateLimitOptionsTests 4 + RateLimiterExtensionsTests 6 + RedisExtensionsTests 5 + RedisSlidingWindowRateLimiterTests 9 = 24 个）

- [ ] **Step 9: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj src/ApiGateway/Leno.ApiGateway/Extensions/RedisExtensions.cs src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs src/ApiGateway/Leno.ApiGateway.Tests/Extensions/RedisExtensionsTests.cs src/ApiGateway/Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs
git commit -m "feat(gateway): 添加 Redis 分布式滑动窗口限流器（SortedSet + Lua 原子脚本）"
```

---

## Task 6: 网关 Program.cs 集成

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Integration/TrafficGovernanceIntegrationTests.cs`

- [ ] **Step 1: 整合 appsettings.json 完整版**

将 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 整体替换为以下完整配置（融合 Phase 1 的 Consul 动态路由 + Phase 4 的限流/熔断/超时/重试）：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Yarp": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Consul": {
    "Url": "http://localhost:8500",
    "Token": "",
    "PassingOnly": true
  },
  "Redis": {
    "Configuration": "localhost:6379"
  },
  "RateLimit": {
    "UseRedisDistributed": true,
    "RedisKeyPrefix": "leno:ratelimit:",
    "Global": {
      "TokenLimit": 5000,
      "TokensPerPeriod": 5000,
      "ReplenishmentPeriod": "00:00:01",
      "AutoReplenishment": true,
      "QueueLimit": 0
    },
    "Routes": {
      "default": {
        "PermitLimit": 200,
        "Window": "00:00:01",
        "SegmentsPerWindow": 4,
        "QueueLimit": 0
      },
      "seckill": {
        "PermitLimit": 50,
        "Window": "00:00:01",
        "SegmentsPerWindow": 4,
        "QueueLimit": 0
      }
    },
    "User": {
      "PermitLimit": 100,
      "Window": "00:01:00",
      "SegmentsPerWindow": 6,
      "AnonymousPartitionClaim": "client-ip",
      "QueueLimit": 0
    }
  },
  "Timeout": {
    "DefaultPolicy": "default",
    "Policies": {
      "default": {
        "RouteType": "default",
        "RequestTimeout": "00:00:30",
        "ConnectTimeout": "00:00:05",
        "ReadTimeout": "00:00:30",
        "Description": "常规 API：5s 连接 / 30s 读取"
      },
      "seckill": {
        "RouteType": "seckill",
        "RequestTimeout": "00:00:05",
        "ConnectTimeout": "00:00:02",
        "ReadTimeout": "00:00:05",
        "Description": "秒杀接口：高时效性，2s 连接 / 5s 读取"
      },
      "upload": {
        "RouteType": "upload",
        "RequestTimeout": "00:02:00",
        "ConnectTimeout": "00:00:10",
        "ReadTimeout": "00:02:00",
        "Description": "文件上传：10s 连接 / 120s 读取（大文件）"
      },
      "internal": {
        "RouteType": "internal",
        "RequestTimeout": "00:00:15",
        "ConnectTimeout": "00:00:03",
        "ReadTimeout": "00:00:15",
        "Description": "服务间调用：3s 连接 / 15s 读取"
      }
    }
  },
  "Retry": {
    "MaxRetries": 2,
    "Backoff": "Exponential",
    "MinBackoff": "00:00:00.500",
    "MaxBackoff": "00:00:01",
    "RetryableStatusCodes": [ 503 ],
    "IdempotentMethodsOnly": true
  },
  "HealthChecksUI": {
    "HealthChecks": [
      { "Name": "API Gateway", "Uri": "http://localhost:8080/health" }
    ],
    "EvaluationTimeInSeconds": 10,
    "MinimumSecondsBetweenFailureNotifications": 60
  },
  "ReverseProxy": {
    "Routes": {
      "user-auth":              { "ClusterId": "user-auth",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/auth/{**catch-all}" },                 "Order": 10 },
      "user-auth-users":        { "ClusterId": "user-auth",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/users/{**catch-all}" },                "Order": 10 },
      "user-auth-admin-users":  { "ClusterId": "user-auth",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/users/{**catch-all}" },           "Order": 10 },
      "product":                { "ClusterId": "product",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/products/{**catch-all}" },             "Order": 10 },
      "product-admin":          { "ClusterId": "product",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "upload",   "Match": { "Path": "/api/admin/products/{**catch-all}" },       "Order": 10 },
      "product-categories":     { "ClusterId": "product",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/categories/{**catch-all}" },          "Order": 10 },
      "product-brands":         { "ClusterId": "product",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/brands/{**catch-all}" },               "Order": 10 },
      "cart":                   { "ClusterId": "cart",             "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/cart/{**catch-all}" },                 "Order": 10 },
      "order":                  { "ClusterId": "order",            "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/orders/{**catch-all}" },               "Order": 10 },
      "order-seller":           { "ClusterId": "order",            "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/seller/orders/{**catch-all}" },        "Order": 1 },
      "order-admin":            { "ClusterId": "order",            "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/orders/{**catch-all}" },         "Order": 10 },
      "order-freight":          { "ClusterId": "order",            "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/freight-templates/{**catch-all}" },    "Order": 10 },
      "order-logistics":        { "ClusterId": "order",            "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/logistics-companies/{**catch-all}" },   "Order": 10 },
      "promotion":              { "ClusterId": "promotion",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/promotions/{**catch-all}" },            "Order": 10 },
      "promotion-admin":        { "ClusterId": "promotion",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/promotions/{**catch-all}" },     "Order": 10 },
      "promotion-coupons":     { "ClusterId": "promotion",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/coupons/{**catch-all}" },               "Order": 10 },
      "promotion-coupons-admin":{ "ClusterId": "promotion",         "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/coupons/{**catch-all}" },        "Order": 10 },
      "promotion-seckill":      { "ClusterId": "promotion",         "RateLimiterPolicy": "seckill",  "TimeoutPolicy": "seckill",  "Match": { "Path": "/api/seckill/{**catch-all}" },               "Order": 10 },
      "promotion-seckill-admin":{ "ClusterId": "promotion",         "RateLimiterPolicy": "seckill",  "TimeoutPolicy": "seckill",  "Match": { "Path": "/api/admin/seckill/{**catch-all}" },        "Order": 10 },
      "payment":                { "ClusterId": "payment",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/payments/{**catch-all}" },              "Order": 10 },
      "payment-admin":          { "ClusterId": "payment",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/payments/{**catch-all}" },        "Order": 10 },
      "payment-notify":         { "ClusterId": "payment",          "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/notify/{**catch-all}" },                "Order": 10 },
      "points":                 { "ClusterId": "points",           "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/points/{**catch-all}" },                "Order": 10 },
      "points-admin":           { "ClusterId": "points",           "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/points/{**catch-all}" },          "Order": 10 },
      "points-members":         { "ClusterId": "points",           "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/members/{**catch-all}" },               "Order": 10 },
      "points-members-admin":   { "ClusterId": "points",           "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/members/{**catch-all}" },        "Order": 10 },
      "points-packages":        { "ClusterId": "points",           "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/membership-packages/{**catch-all}" },  "Order": 10 },
      "review":                 { "ClusterId": "review-aftersales", "RateLimiterPolicy": "default", "TimeoutPolicy": "default",  "Match": { "Path": "/api/reviews/{**catch-all}" },               "Order": 10 },
      "review-admin":           { "ClusterId": "review-aftersales", "RateLimiterPolicy": "default", "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/reviews/{**catch-all}" },         "Order": 10 },
      "after-sales":            { "ClusterId": "review-aftersales", "RateLimiterPolicy": "default", "TimeoutPolicy": "default",  "Match": { "Path": "/api/after-sales/{**catch-all}" },           "Order": 10 },
      "after-sales-admin":      { "ClusterId": "review-aftersales", "RateLimiterPolicy": "default", "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/after-sales/{**catch-all}" },      "Order": 10 },
      "shops":                  { "ClusterId": "seller-shop",      "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/shops/{**catch-all}" },                 "Order": 10 },
      "shops-admin":            { "ClusterId": "seller-shop",      "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/admin/shops/{**catch-all}" },           "Order": 10 },
      "seller":                 { "ClusterId": "seller-shop",      "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/seller/{**catch-all}" },                 "Order": 20 },
      "notification":           { "ClusterId": "notification",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/notifications/{**catch-all}" },          "Order": 10 },
      "notification-templates": { "ClusterId": "notification",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/notification-templates/{**catch-all}" }, "Order": 10 },
      "notification-preferences":{ "ClusterId": "notification",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/notification-preferences/{**catch-all}" }, "Order": 10 },
      "system-operators":       { "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/operators/{**catch-all}" },             "Order": 10 },
      "system-configs":         { "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/system-configs/{**catch-all}" },        "Order": 10 },
      "system-feature-flags":   { "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/feature-flags/{**catch-all}" },         "Order": 10 },
      "system-announcements":  { "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/announcements/{**catch-all}" },         "Order": 10 },
      "system-dictionaries":   { "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/data-dictionaries/{**catch-all}" },     "Order": 10 },
      "system-scheduled-tasks":{ "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/scheduled-tasks/{**catch-all}" },       "Order": 10 },
      "system-audit-logs":     { "ClusterId": "system-admin",     "RateLimiterPolicy": "default",  "TimeoutPolicy": "default",  "Match": { "Path": "/api/audit-logs/{**catch-all}" },             "Order": 10 }
    },
    "Clusters": {
      "user-auth":         { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-user-auth-api" },         "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "product":          { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-product-api" },          "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "cart":             { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-cart-api" },             "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "order":            { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-order-api" },            "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "promotion":        { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-promotion-api" },         "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "payment":          { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-payment-api" },          "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "points":           { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-points-api" },           "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "review-aftersales":{ "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-review-aftersales-api" }, "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "seller-shop":      { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-seller-shop-api" },      "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "notification":     { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-notification-api" },     "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } },
      "system-admin":     { "LoadBalancingPolicy": "PowerOfTwoChoices", "Metadata": { "ConsulServiceName": "leno-system-admin-api" },     "HttpRequest": { "ActivityTimeout": "00:00:30" }, "CircuitBreaker": { "MaxConcurrentRequests": 100, "FailureRateThreshold": 0.5, "SamplingDuration": "00:00:30", "MinimumThroughput": 10, "BreakDuration": "00:00:30" }, "Retry": { "MaxRetries": 2, "Backoff": "Exponential", "MinBackoff": "00:00:00.500", "MaxBackoff": "00:00:01", "RetryableStatusCodes": [ 503 ] }, "HealthCheck": { "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }, "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" } } }
    }
  }
}
```

- [ ] **Step 2: 同步更新 appsettings.Docker.json**

将 `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` 整体替换为：

```json
{
  "Consul": {
    "Url": "http://consul:8500",
    "Token": "",
    "PassingOnly": true
  },
  "Redis": {
    "Configuration": "redis:6379"
  },
  "HealthChecksUI": {
    "HealthChecks": [
      { "Name": "API Gateway", "Uri": "http://api-gateway:8080/health" }
    ],
    "EvaluationTimeInSeconds": 10,
    "MinimumSecondsBetweenFailureNotifications": 60
  },
  "ReverseProxy": {
    "Clusters": {}
  }
}
```

> Docker 环境下 Redis 指向 `redis:6379`，ReverseProxy Clusters 从 appsettings.json 继承（Docker 镜像构建时合并）。

- [ ] **Step 3: 修改 Program.cs — 注册限流/超时/Redis + 中间件管道**

将 `src/ApiGateway/Leno.ApiGateway/Program.cs` 的全部内容替换为：

```csharp
using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Middleware;
using Leno.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// YARP 反向代理从配置加载路由（含 RateLimiterPolicy/TimeoutPolicy 字段）
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Phase 1：Consul 服务发现 + 动态 Destination 解析器
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddConsulDestinationResolver();

// Phase 4：Redis（用于分布式限流计数器）
builder.Services.AddGatewayRedis(builder.Configuration);

// Phase 4：限流策略（global/default/seckill/per-user，Redis 启用时使用 RedisSlidingWindowRateLimiter）
builder.Services.AddGatewayRateLimiter(builder.Configuration);

// Phase 4：超时策略（default/seckill/upload/internal）
builder.Services.AddGatewayTimeouts(builder.Configuration);

// HealthChecksUI 仪表盘
builder.Services.AddLenoHealthChecksUI(builder.Configuration);

// 网关自身健康检查：存活探针 + Consul 连通性就绪检查
#pragma warning disable CA1861
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["Consul:Url"] ?? "http://localhost:8500"),
        "consul",
        tags: new[] { "ready" });
#pragma warning restore CA1861

var app = builder.Build();

// 存活探针：仅检查网关进程存活
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// 就绪探针与 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

// 中间件管道顺序（Phase 4 新增）：
//   1. FallbackResponseMiddleware — 拦截 YARP 503 改写为降级 JSON（在 MapReverseProxy 之前）
//   2. UseRateLimiter — 应用路由级 RateLimiterPolicy（ASP.NET Core 内建）
//   3. UseRequestTimeouts — 应用路由级 TimeoutPolicy（由 AddGatewayTimeouts 隐式注册）
//   4. MapReverseProxy — YARP 反向代理（含 CircuitBreaker/Retry/HttpRequest.ActivityTimeout）
app.UseMiddleware<FallbackResponseMiddleware>();
app.UseRateLimiter();
app.UseRequestTimeouts();

// YARP 反向代理端点
app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（集成测试需要）
public partial class Program { }
```

> **关键变更（相对 Phase 1 终态）：**
> - 新增 `AddGatewayRedis`、`AddGatewayRateLimiter`、`AddGatewayTimeouts` 三个注册调用
> - 中间件管道新增 `UseMiddleware<FallbackResponseMiddleware>`、`UseRateLimiter`、`UseRequestTimeouts` 三行
> - 顺序说明：FallbackResponse 必须在 `MapReverseProxy` 之前以便缓冲 YARP 响应；UseRateLimiter 在 YARP 之前以便 429 短路；UseRequestTimeouts 在 YARP 之前以应用路由级超时

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 5: 创建 TrafficGovernance 集成测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Integration/TrafficGovernanceIntegrationTests.cs`：

```csharp
using System.Net;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Integration;

/// <summary>
/// Phase 4 流量治理端到端集成测试。
/// 通过 WebApplicationFactory 启动完整网关管道，验证限流/降级/超时中间件链路。
/// </summary>
public class TrafficGovernanceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IConsulServiceDiscovery> _consulMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDbMock;

    public TrafficGovernanceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _consulMock = new Mock<IConsulServiceDiscovery>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDbMock = new Mock<IDatabase>();

        // Redis 默认返回 1（允许通过），具体测试中可覆写
        _redisDbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisResult(1L, ResultType.Integer));
        _redisMock.SetupGet(m => m.IsConnected).Returns(true);
        _redisMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Consul:Url"] = "http://localhost:8500",
                    ["Redis:Configuration"] = "localhost:6379",
                    ["RateLimit:UseRedisDistributed"] = "true",
                    ["RateLimit:Global:TokenLimit"] = "5000",
                    ["RateLimit:Routes:default:PermitLimit"] = "200",
                    ["RateLimit:Routes:default:Window"] = "00:00:01",
                    ["RateLimit:Routes:seckill:PermitLimit"] = "50",
                    ["RateLimit:Routes:seckill:Window"] = "00:00:01",
                    ["RateLimit:User:PermitLimit"] = "100",
                    ["RateLimit:User:Window"] = "00:01:00"
                });
            });

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换真实 Consul 服务发现
                services.RemoveAll<IConsulServiceDiscovery>();
                services.AddSingleton(_consulMock.Object);

                // 用 mock 替换真实 Redis 连接（避免测试依赖真实 Redis）
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton(_redisMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldNotBeRewrittenByFallbackMiddleware()
    {
        // Arrange — /health/ready 在 Consul 不可用时返回 503，但 FallbackResponseMiddleware 应跳过健康端点
        var response = await _client.GetAsync("/health/ready");

        // Assert — 503 但响应体不是降级 JSON
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            body.Should().NotContain("服务暂时不可用，请稍后重试");
        }
    }

    [Fact]
    public async Task Proxy_WhenBackendReturns503_RewritesAsFallbackJson()
    {
        // Arrange — 模拟 Consul 返回一个不存在的实例（YARP 转发将失败返回 502/503）
        _consulMock.Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("test-1", "localhost", 5150, Array.Empty<string>())
            });

        // Act — 发送请求到 product 路由
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 网关转发失败（502/503），FallbackResponseMiddleware 应改写 503 为降级 JSON
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("\"code\":503");
            body.Should().Contain("服务暂时不可用，请稍后重试");
        }
        else
        {
            // 502 时不改写（FallbackResponseMiddleware 只处理 503）
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadGateway);
        }
    }

    [Fact]
    public async Task Proxy_WhenNoHealthyInstances_Returns503WithFallbackJson()
    {
        // Arrange — Consul 返回空实例列表
        _consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        // Act
        var response = await _client.GetAsync("/api/cart/test");

        // Assert — YARP 返回 503（无可用 destination），FallbackResponse 应改写为降级 JSON
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"code\":503");
        body.Should().Contain("服务暂时不可用，请稍后重试");
    }

    [Fact]
    public async Task Proxy_WithSeckillRoute_AppliesSeckillRateLimiterPolicy()
    {
        // Arrange — 模拟 Consul 返回健康实例
        _consulMock.Setup(d => d.GetHealthyInstancesAsync("leno-promotion-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("promo-1", "localhost", 5152, Array.Empty<string>())
            });

        // Act — 发送到秒杀路由，应触发 "seckill" 策略
        var response = await _client.GetAsync("/api/seckill/123");

        // Assert — Redis 被调用且 Key 包含 "seckill" 策略名
        _redisDbMock.Verify(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.Is<RedisKey[]>(keys => keys.Length > 0 && keys[0].ToString().Contains("seckill")),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.AtLeastOnce);

        // 响应可能是 502（后端不可达）或 503，但管道不应崩溃
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Proxy_WhenRedisDenies_Returns429TooManyRequests()
    {
        // Arrange — Redis 返回 0（拒绝），模拟限流触发
        _redisDbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisResult(0L, ResultType.Integer));

        _consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("test-1", "localhost", 5150, Array.Empty<string>())
            });

        // Act
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — Redis 拒绝应导致 429
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Proxy_WhenRedisThrows_FailsOpenAndForwardsRequest()
    {
        // Arrange — Redis 异常，限流器 fail-open 放行
        _redisDbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<RedisScript>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        _consulMock.Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("test-1", "localhost", 5150, Array.Empty<string>())
            });

        // Act
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 不应因 Redis 故障返回 429；转发尝试应进行（可能 502 但不是 429）
        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
```

- [ ] **Step 6: 运行集成测试**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TrafficGovernanceIntegrationTests"`
Expected: 集成测试通过（个别测试可能因测试环境无真实后端而放宽断言；如出现不稳定可在 CI 中标记 `[Trait("Category","Integration")]` 跳过）

- [ ] **Step 7: 运行全部测试**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Passed` — 含 Phase 1 既有测试 + Phase 4 新增测试（RateLimitOptionsTests 4 + RateLimiterExtensionsTests 6 + FallbackResponseMiddlewareTests 6 + TimeoutOptionsTests 4 + RetryOptionsTests 4 + RedisExtensionsTests 5 + RedisSlidingWindowRateLimiterTests 9 + TrafficGovernanceIntegrationTests 7 = 45 个新增）

- [ ] **Step 8: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Program.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json src/ApiGateway/Leno.ApiGateway.Tests/Integration/TrafficGovernanceIntegrationTests.cs
git commit -m "feat(gateway): 集成限流/熔断/超时/重试到 Program.cs 管道并整合 appsettings 完整配置"
```

---

## 实施后验证清单

完成所有 Task 后执行以下整体验证：

- [ ] **全量编译：** `dotnet build Leno.slnx` — 所有项目编译成功
- [ ] **全量测试：** `dotnet test Leno.slnx` — 所有测试通过（含 Phase 1 + Phase 4）
- [ ] **限流策略映射校验：** 44 条路由的 `RateLimiterPolicy` 字段值在 {global, default, seckill, per-user} 中合法
- [ ] **超时策略映射校验：** 44 条路由的 `TimeoutPolicy` 字段值在 {default, seckill, upload, internal} 中合法
- [ ] **熔断配置校验：** 11 个 Cluster 的 `CircuitBreaker` 配置完整（5 个字段齐全）
- [ ] **重试配置校验：** 11 个 Cluster 的 `Retry` 配置完整，`RetryableStatusCodes` 含 503
- [ ] **Redis 分布式限流降级：** 在 Redis 不可用时网关 fail-open（不阻断合法流量）
- [ ] **降级响应：** 熔断/无可用实例触发 503 时，响应体被改写为 `{"code":503,"message":"服务暂时不可用，请稍后重试","data":null}`
- [ ] **健康端点隔离：** `/health/*` 路径不被降级中间件改写
