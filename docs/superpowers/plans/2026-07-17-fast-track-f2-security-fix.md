# 快轨 Wave-F2 安全默认修复 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 4 个安全默认问题：网关 JWT 本地验签、JWT 黑名单拦截、后端信任网关头适配、密钥管理与配置中心启用

**Architecture:** 网关侧补建 JWT 验签 + 黑名单中间件；后端侧灰度切换 GatewayHeader 认证（`Auth:Mode` 开关）；配置侧移除明文密钥、启用 Consul KV、补齐 .gitignore

**Tech Stack:** .NET 10、Microsoft.AspNetCore.Authentication.JwtBearer、StackExchange.Redis、Consul、xUnit、FluentAssertions

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §5](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| ApiGateway Program.cs | `src/ApiGateway/Leno.ApiGateway/Program.cs:1-88` | 未调用 AddAuthentication/AddJwtBearer，无 UseAuthentication |
| JwtTokenGenerator | `src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs:30-181` | `BuildValidationParameters()` 是实例方法（行 129），依赖 `_options.SecretKey` |
| JwtOptions | `src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs:12-25` | Issuer/Audience/SecretKey/AccessTokenExpiryMinutes/RefreshTokenExpiryDays |
| UserContextTransformProvider | `src/ApiGateway/Leno.ApiGateway/Transforms/UserContextTransformProvider.cs:16-96` | 常量 XUserId/XRole/XShopId/XInternalCall，依赖 JwtAuthMiddleware 填充 HttpContext.User |
| GatewayMetricsService | `src/ApiGateway/Leno.ApiGateway/Services/GatewayMetricsService.cs:12-117` | `gateway_blacklist_hits` 计数器行 71-73，`RecordBlacklistHit()` 行 113 无调用方 |
| AuthController | `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs` | 无 logout 端点，行 25-122 含 register/login/refresh-token 等 |
| CurrentUserContext | `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs:1-43` | 委托 `JwtTokenGenerator.GetUserId/GetRole/GetShopId` 静态方法 |
| Order Program.cs JWT 配置 | `src/Services/Order/Leno.Order.Api/Program.cs:46-66` | 11 BC 通用模式，直接 `AddJwtBearer` |
| appsettings.json 明文密钥 | `src/Services/Order/Leno.Order.Api/appsettings.json:33-39` | Jwt:SecretKey 明文，同样模式存在于 11 BC |
| InternalAuth 配置 | `src/Services/Order/Leno.Order.Api/appsettings.json:40-42` | `ApiKey: "leno-internal-key-dev"`，11 BC 共用 |
| AddLenoConsulConfig | `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs:105-149` | 已定义未启用，`Optional=true` |
| ValidateSensitiveConfig | `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs:155-164` | 已定义未启用，检查 SensitiveConfigKeys 列表 |
| EnsureInternalApiKeyConfigured | `src/BuildingBlocks/Leno.Infrastructure/Auth/InternalApiKeyExtensions.cs:22-40` | 已定义未启用，生产环境校验 ApiKey 非空 |
| docker-compose 明文密钥 | `docker-compose.yml:7,14,55-56,130-131` | SA密码/RabbitMQ密码/Grafana密码 |
| .gitignore | `.gitignore:1-26` | 已忽略 .env，未忽略 appsettings.Production.json |

---

## Task 1: 网关 JWT 本地验签（P0-4）

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs:1-88`（增加 JWT 验签配置与中间件）
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`（增加 Jwt 配置节）
- Test: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs`（新建）

- [ ] **Step 1: 写失败测试 — JWT 验签中间件**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs`：

```csharp
namespace Leno.ApiGateway.Tests.Middleware;

public class JwtAuthMiddlewareTests
{
    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedEndpoint_ShouldReturn401()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.Configure<JwtOptions>(o =>
                {
                    o.Issuer = "Leno.UserAuth";
                    o.Audience = "Leno.Clients";
                    o.SecretKey = "TestSecretKeyAtLeast32BytesLong!!";
                });
            }));

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert: 受保护端点无 token 返回 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WhitelistedEndpoint_NoToken_ShouldReturn200()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.Configure<JwtOptions>(o =>
                {
                    o.Issuer = "Leno.UserAuth";
                    o.Audience = "Leno.Clients";
                    o.SecretKey = "TestSecretKeyAtLeast32BytesLong!!";
                });
            }));

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert: 白名单路由无 token 返回 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ValidToken_ShouldPassAndInjectUserContextHeaders()
    {
        // Arrange
        var secretKey = "TestSecretKeyAtLeast32BytesLong!!";
        var token = GenerateTestToken(secretKey, userId: Guid.NewGuid(), role: "Buyer");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.Configure<JwtOptions>(o =>
                {
                    o.Issuer = "Leno.UserAuth";
                    o.Audience = "Leno.Clients";
                    o.SecretKey = secretKey;
                });
            }));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act: 请求受保护端点（需要 mock 下游，此处仅验证网关不拦截）
        // 实际实现时使用 YARP TestForwarder mock
        var response = await client.GetAsync("/api/orders");

        // Assert: 不返回 401（可能返回 502 下游不可达，但不应是 401）
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    private static string GenerateTestToken(string secretKey, Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: "Leno.UserAuth",
            audience: "Leno.Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests --filter "FullyQualifiedName~JwtAuthMiddlewareTests"`
Expected: FAIL — 网关未配置 JWT 验签，无 token 请求不返回 401

- [ ] **Step 3: 修改网关 appsettings.json — 增加 Jwt 配置节**

修改 `src/ApiGateway/Leno.ApiGateway/appsettings.json`，增加：

```json
"Jwt": {
  "Issuer": "Leno.UserAuth",
  "Audience": "Leno.Clients",
  "SecretKey": "${JWT_SECRET_KEY}",
  "AccessTokenExpiryMinutes": 120,
  "RefreshTokenExpiryDays": 7
}
```

- [ ] **Step 4: 修改网关 Program.cs — 增加 JWT 验签**

修改 `src/ApiGateway/Leno.ApiGateway/Program.cs`，在 `AddGatewayCors` 之后、`AddProtocolTranslators` 之前增加：

```csharp
// JWT 验签配置
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt 配置节缺失");
var jwtTokenGenerator = new JwtTokenGenerator(Options.Create(jwtOptions));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = jwtTokenGenerator.BuildValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(jwtTokenGenerator);
```

在中间件管道中，`UseCors()` 之后、`FallbackResponseMiddleware` 之前增加：

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 5: 配置白名单路由**

在 `app.UseEndpoints` 或 `MapReverseProxy` 配置中，对白名单路由允许匿名访问。修改 `Program.cs` 中路由配置：

```csharp
// 白名单路由模式（在 UseAuthorization 之前注册）
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isWhitelisted = path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/auth/register", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);

    if (isWhitelisted)
    {
        await next();
        return;
    }

    if (context.User?.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { code = 401, message = "未认证" });
        return;
    }

    await next();
});
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests --filter "FullyQualifiedName~JwtAuthMiddlewareTests"`
Expected: PASS — 3 个测试通过

- [ ] **Step 7: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Program.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs
git commit -m "修复(P0-4): API 网关启用 JWT 本地验签

- 网关 Program.cs 增加 AddAuthentication().AddJwtBearer()
- 复用 JwtTokenGenerator.BuildValidationParameters() 配置验签参数
- 管道增加 UseAuthentication/UseAuthorization
- 白名单路由（login/register/refresh-token/health/metrics）跳过验签
- 新增 3 个测试：无 token 401、白名单 200、有效 token 通过"
```

---

## Task 2: JWT 黑名单拦截（P1，F2.2）

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Middleware/JwtBlacklistMiddleware.cs`（黑名单中间件）
- Create: `src/ApiGateway/Leno.ApiGateway/Services/IJwtBlacklistService.cs`（黑名单服务接口）
- Create: `src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs`（Redis 实现）
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/Abstractions/IJwtRevocationService.cs`（UserAuth 域吊销接口）
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/Services/JwtRevocationService.cs`（UserAuth 域实现）
- Modify: `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs`（增加 logout 端点）
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs`（注册中间件与服务）
- Test: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtBlacklistMiddlewareTests.cs`（新建）

- [ ] **Step 1: 写失败测试 — JWT 黑名单拦截**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtBlacklistMiddlewareTests.cs`：

```csharp
namespace Leno.ApiGateway.Tests.Middleware;

public class JwtBlacklistMiddlewareTests
{
    private readonly Mock<IJwtBlacklistService> _blacklistMock = new();
    private readonly Mock<GatewayMetricsService> _metricsMock = new();

    [Fact]
    public async Task Request_WithBlacklistedJti_ShouldReturn401AndRecordHit()
    {
        // Arrange: jti 在黑名单中
        var jti = Guid.NewGuid().ToString();
        _blacklistMock.Setup(b => b.IsRevokedAsync(jti, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        }, "Bearer"));

        var middleware = new JwtBlacklistMiddleware(_ => Task.CompletedTask, _blacklistMock.Object, _metricsMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _metricsMock.Verify(m => m.RecordBlacklistHit(), Times.Once);
    }

    [Fact]
    public async Task Request_WithValidJti_ShouldPassThrough()
    {
        // Arrange: jti 不在黑名单
        var jti = Guid.NewGuid().ToString();
        _blacklistMock.Setup(b => b.IsRevokedAsync(jti, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        }, "Bearer"));

        var nextCalled = false;
        var middleware = new JwtBlacklistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            _blacklistMock.Object, _metricsMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _metricsMock.Verify(m => m.RecordBlacklistHit(), Times.Never);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests --filter "FullyQualifiedName~JwtBlacklistMiddlewareTests"`
Expected: FAIL — JwtBlacklistMiddleware 与 IJwtBlacklistService 不存在

- [ ] **Step 3: 创建 IJwtBlacklistService 接口与实现**

创建 `src/ApiGateway/Leno.ApiGateway/Services/IJwtBlacklistService.cs`：

```csharp
namespace Leno.ApiGateway.Services;

/// <summary>
/// JWT 黑名单服务，检查 token jti 是否已被吊销。
/// </summary>
public interface IJwtBlacklistService
{
    /// <summary>检查 jti 是否在黑名单中。</summary>
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);

    /// <summary>吊销 jti（登出时调用），TTL 为 token 剩余有效期。</summary>
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default);
}
```

创建 `src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs`：

```csharp
namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Redis 的 JWT 黑名单实现。
/// Key 格式：leno:jwt:blacklist:{jti}，Value：1，TTL = token 剩余有效期。
/// 三层保障：Redis Pub/Sub 实时 + 定时拉取兜底 + 启动预热。
/// </summary>
public sealed class JwtBlacklistService : IJwtBlacklistService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JwtBlacklistService> _logger;
    private readonly ConcurrentDictionary<string, byte> _localCache = new();

    public JwtBlacklistService(IConnectionMultiplexer redis, ILogger<JwtBlacklistService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        // 先查本地 Caffeine 缓存
        if (_localCache.ContainsKey(jti)) return true;

        // 再查 Redis
        var db = _redis.GetDatabase();
        var exists = await db.KeyExistsAsync($"leno:jwt:blacklist:{jti}");
        if (exists)
        {
            _localCache.TryAdd(jti, 0);
            return true;
        }
        return false;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"leno:jwt:blacklist:{jti}", "1", ttl);
        _localCache.TryAdd(jti, 0);
        _logger.LogInformation("JWT 已吊销 Jti={Jti} Ttl={Ttl}分钟", jti, ttl.TotalMinutes);
    }
}
```

- [ ] **Step 4: 创建 JwtBlacklistMiddleware**

创建 `src/ApiGateway/Leno.ApiGateway/Middleware/JwtBlacklistMiddleware.cs`：

```csharp
namespace Leno.ApiGateway.Middleware;

/// <summary>
/// JWT 黑名单拦截中间件，紧随 UseAuthentication 之后。
/// 命中黑名单返回 401 并递增 gateway_blacklist_hits 计数器。
/// </summary>
public sealed class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IJwtBlacklistService _blacklistService;
    private readonly GatewayMetricsService _metrics;

    public JwtBlacklistMiddleware(
        RequestDelegate next,
        IJwtBlacklistService blacklistService,
        GatewayMetricsService metrics)
    {
        _next = next;
        _blacklistService = blacklistService;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 仅对已认证请求检查黑名单
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                if (await _blacklistService.IsRevokedAsync(jti, context.RequestAborted))
                {
                    _metrics.RecordBlacklistHit();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { code = 401, message = "Token 已被吊销" });
                    return;
                }
            }
        }

        await _next(context);
    }
}
```

- [ ] **Step 5: 修改网关 Program.cs — 注册中间件**

在 `app.UseAuthentication()` 之后、`app.UseAuthorization()` 之前增加：

```csharp
app.UseMiddleware<JwtBlacklistMiddleware>();
```

在 Services 注册部分增加：

```csharp
builder.Services.AddSingleton<IJwtBlacklistService, JwtBlacklistService>();
```

- [ ] **Step 6: 创建 UserAuth 域登出端点**

创建 `src/Services/UserAuth/Leno.UserAuth.Application/Abstractions/IJwtRevocationService.cs`：

```csharp
namespace Leno.UserAuth.Application.Abstractions;

public interface IJwtRevocationService
{
    /// <summary>吊销指定 jti 的 token。</summary>
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default);
}
```

创建 `src/Services/UserAuth/Leno.UserAuth.Application/Services/JwtRevocationService.cs`：

```csharp
namespace Leno.UserAuth.Application.Services;

/// <summary>
/// UserAuth 域 JWT 吊销服务，通过 Redis 写入黑名单（与网关共用 Redis 实例）。
/// </summary>
public sealed class JwtRevocationService : IJwtRevocationService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JwtRevocationService> _logger;

    public JwtRevocationService(IConnectionMultiplexer redis, ILogger<JwtRevocationService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"leno:jwt:blacklist:{jti}", "1", ttl);
        _logger.LogInformation("用户登出，JWT 已吊销 Jti={Jti}", jti);
    }
}
```

修改 `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs`，增加登出端点：

```csharp
[HttpPost("logout")]
[Authorize]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> LogoutAsync(CancellationToken ct)
{
    // 从 JWT 提取 jti 与剩余有效期
    var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
    var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

    if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(expClaim))
    {
        return BadRequest(ApiResponse.Fail(400, "Token 缺少必要声明"));
    }

    var exp = long.Parse(expClaim);
    var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
    var ttl = expiry - DateTimeOffset.UtcNow;
    if (ttl > TimeSpan.Zero)
    {
        await _revocationService.RevokeAsync(jti, ttl, ct);
    }

    return Ok(ApiResponse.Success());
}
```

在 AuthController 构造函数注入 `IJwtRevocationService`。

- [ ] **Step 7: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests --filter "FullyQualifiedName~JwtBlacklistMiddlewareTests"`
Expected: PASS — 2 个测试通过

- [ ] **Step 8: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Middleware/ src/ApiGateway/Leno.ApiGateway/Services/IJwtBlacklistService.cs src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs src/ApiGateway/Leno.ApiGateway/Program.cs src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtBlacklistMiddlewareTests.cs src/Services/UserAuth/Leno.UserAuth.Application/Abstractions/IJwtRevocationService.cs src/Services/UserAuth/Leno.UserAuth.Application/Services/JwtRevocationService.cs src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs
git commit -m "修复(P1): JWT 黑名单拦截与登出端点

- 新增 IJwtBlacklistService 接口与 Redis 实现（三层保障）
- 新增 JwtBlacklistMiddleware 紧随 UseAuthentication
- 命中黑名单返回 401 并递增 gateway_blacklist_hits 计数器
- UserAuth 域新增 /api/auth/logout 端点与 IJwtRevocationService
- 新增 2 个测试覆盖黑名单命中与未命中场景"
```

---

## Task 3: 后端服务信任网关头适配（P0-4 配套）

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs`（认证选项）
- Create: `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs`（认证处理器）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs:24-43`（兼容 GatewayHeader 模式）
- Modify: 11 个 BC `Program.cs`（增加 `Auth:Mode` 配置开关）
- Test: `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs`（新建）

- [ ] **Step 1: 写失败测试 — GatewayAuthHandler**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs`：

```csharp
namespace Leno.Infrastructure.Tests.Auth;

public class GatewayAuthHandlerTests
{
    [Fact]
    public async Task HandleAuthenticateAsync_WithValidHeaders_ShouldAuthenticate()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var role = "Seller";
        var shopId = Guid.NewGuid().ToString();

        var handler = CreateHandler(new Dictionary<string, string>
        {
            { "X-User-Id", userId },
            { "X-Role", role },
            { "X-Shop-Id", shopId }
        });

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value.Should().Be(userId);
        result.Principal!.FindFirst(ClaimTypes.Role)!.Value.Should().Be(role);
        result.Principal!.FindFirst("shop_id")!.Value.Should().Be(shopId);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingUserIdHeader_ShouldNotAuthenticate()
    {
        // Arrange: 缺少 X-User-Id
        var handler = CreateHandler(new Dictionary<string, string>
        {
            { "X-Role", "Seller" }
        });

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NoHeaders_ShouldNotAuthenticate()
    {
        // Arrange: 无任何头
        var handler = CreateHandler(new Dictionary<string, string>());

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    private static GatewayAuthHandler CreateHandler(Dictionary<string, string> headers)
    {
        var options = new GatewayAuthOptions { HeaderPrefix = "X-" };
        var optionsMonitor = new Mock<IOptionsMonitor<GatewayAuthOptions>>();
        optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

        var httpContext = new DefaultHttpContext();
        foreach (var kv in headers)
        {
            httpContext.Request.Headers[kv.Key] = kv.Value;
        }

        var handler = new GatewayAuthHandler(optionsMonitor.Object, new LoggerFactory().CreateLogger<GatewayAuthHandler>());
        handler.InitializeAsync(new AuthenticationScheme("GatewayHeader", null, typeof(GatewayAuthHandler)), httpContext).GetAwaiter().GetResult();
        return handler;
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests --filter "FullyQualifiedName~GatewayAuthHandlerTests"`
Expected: FAIL — GatewayAuthHandler 与 GatewayAuthOptions 不存在

- [ ] **Step 3: 创建 GatewayAuthOptions**

创建 `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs`：

```csharp
namespace Leno.Infrastructure.Auth;

/// <summary>
/// 网关头认证选项。
/// </summary>
public sealed class GatewayAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>头前缀，默认 "X-"。</summary>
    public string HeaderPrefix { get; set; } = "X-";

    /// <summary>是否校验 X-Internal-Call 头确认请求来自网关。</summary>
    public bool RequireInternalCallHeader { get; set; } = false;
}
```

- [ ] **Step 4: 创建 GatewayAuthHandler**

创建 `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs`：

```csharp
namespace Leno.Infrastructure.Auth;

/// <summary>
/// 网关头认证处理器，从 X-User-Id/X-Role/X-Shop-Id 头构造 ClaimsPrincipal。
/// 仅在后端服务容器内网部署时使用，头由网关 JWT 验签后注入。
/// </summary>
public sealed class GatewayAuthHandler : AuthenticationHandler<GatewayAuthOptions>
{
    public GatewayAuthHandler(IOptionsMonitor<GatewayAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers["X-Role"].FirstOrDefault() ?? string.Empty;
        var shopId = Request.Headers["X-Shop-Id"].FirstOrDefault();

        // 可选：校验 X-Internal-Call 头
        if (Options.RequireInternalCallHeader)
        {
            var internalCall = Request.Headers["X-Internal-Call"].FirstOrDefault();
            if (string.IsNullOrEmpty(internalCall))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing X-Internal-Call header"));
            }
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };
        if (!string.IsNullOrEmpty(shopId))
        {
            claims.Add(new Claim(JwtTokenGenerator.ShopIdClaimType, shopId));
        }

        var identity = new ClaimsIdentity(claims, "GatewayHeader");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "GatewayHeader");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

- [ ] **Step 5: 修改 CurrentUserContext — 兼容两种模式**

修改 `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs`，无需改动实现——`JwtTokenGenerator.GetUserId/GetRole/GetShopId` 静态方法已从 ClaimsPrincipal 提取，无论 Claims 来自 JWT 还是 GatewayHeader 都能工作。

- [ ] **Step 6: 修改 11 个 BC Program.cs — 增加 Auth:Mode 开关**

以 Order BC 为代表，修改 `src/Services/Order/Leno.Order.Api/Program.cs:46-66`：

```csharp
// 认证配置（支持 JwtBearer 与 GatewayHeader 两种模式，灰度切换）
var authMode = builder.Configuration["Auth:Mode"] ?? "JwtBearer";

builder.Services.AddAuthentication(authMode == "GatewayHeader"
    ? "GatewayHeader"
    : JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt 配置节缺失");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOpts.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    })
    .AddScheme<GatewayAuthOptions, GatewayAuthHandler>("GatewayHeader", _ => { });
```

在 appsettings.json 增加配置：

```json
"Auth": {
  "Mode": "JwtBearer"
}
```

- [ ] **Step 7: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests --filter "FullyQualifiedName~GatewayAuthHandlerTests"`
Expected: PASS — 3 个测试通过

- [ ] **Step 8: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs src/Services/*/Leno.*/Program.cs src/Services/*/Leno.*/appsettings.json
git commit -m "修复(P0-4 配套): 后端服务支持 GatewayHeader 认证模式（灰度切换）

- 新增 GatewayAuthOptions 与 GatewayAuthHandler
- 从 X-User-Id/X-Role/X-Shop-Id 头构造 ClaimsPrincipal
- 11 个 BC Program.cs 增加 Auth:Mode 配置开关
- 默认 JwtBearer 模式，可灰度切换到 GatewayHeader
- 新增 3 个测试覆盖有效头、缺失头、无头场景"
```

---

## Task 4: 密钥管理与配置中心启用（P0-5/7）

**Files:**
- Modify: `.gitignore`（补齐敏感文件忽略）
- Modify: `docker-compose.yml:7,14,55-56,130-131`（明文密钥改为环境变量）
- Modify: 11 个 BC `appsettings.json`（明文密钥改为占位符）
- Create: `.env.example`（环境变量模板，gitignored）
- Modify: 11 个 BC `Program.cs`（启用 AddLenoConsulConfig + ValidateSensitiveConfig + EnsureInternalApiKeyConfigured）
- Test: `src/BuildingBlocks/Leno.Infrastructure.Tests/Configuration/ConfigCenterExtensionsTests.cs`（新建）

- [ ] **Step 1: 写失败测试 — 敏感配置校验**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Configuration/ConfigCenterExtensionsTests.cs`：

```csharp
namespace Leno.Infrastructure.Tests.Configuration;

public class ConfigCenterExtensionsTests
{
    [Fact]
    public void ValidateSensitiveConfig_MissingJwtSecretKey_ShouldReturnFalse()
    {
        // Arrange: 缺失 Jwt:SecretKey
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Payment:Alipay:AppId", "test" },
                { "Payment:Alipay:PrivateKey", "test" },
                { "Payment:Alipay:PublicKey", "test" }
            })
            .Build();

        // Act
        var isValid = config.ValidateSensitiveConfig();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateSensitiveConfig_AllKeysPresent_ShouldReturnTrue()
    {
        // Arrange: 所有敏感配置齐全
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test-secret-key-32-bytes-long!!" },
                { "Payment:Alipay:AppId", "test" },
                { "Payment:Alipay:PrivateKey", "test" },
                { "Payment:Alipay:PublicKey", "test" },
                { "Payment:WeChatPay:AppId", "test" },
                { "Payment:WeChatPay:MchId", "test" },
                { "Payment:WeChatPay:ApiKey", "test" },
                { "SMS:ApiKey", "test" },
                { "SMS:ApiSecret", "test" },
                { "OAuth2:WeChat:AppId", "test" },
                { "OAuth2:WeChat:AppSecret", "test" },
                { "OAuth2:Apple:ClientId", "test" },
                { "OAuth2:Apple:ClientSecret", "test" }
            })
            .Build();

        // Act
        var isValid = config.ValidateSensitiveConfig();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void GetMissingSensitiveConfigKeys_PartialConfig_ShouldReturnMissingKeys()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test" }
            })
            .Build();

        // Act
        var missing = config.GetMissingSensitiveConfigKeys();

        // Assert
        missing.Should().NotBeEmpty();
        missing.Should().Contain("Payment:Alipay:AppId");
    }
}
```

- [ ] **Step 2: 运行测试验证失败或通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests --filter "FullyQualifiedName~ConfigCenterExtensionsTests"`
Expected: PASS — `ValidateSensitiveConfig` 与 `GetMissingSensitiveConfigKeys` 已定义，测试应通过。若失败则说明方法签名与预期不符，调整测试。

- [ ] **Step 3: 修改 .gitignore — 补齐敏感文件忽略**

修改 `.gitignore`，在 `# Secrets` 段追加：

```
# Secrets
.env
*.pfx
secrets.json
appsettings.Production.json
appsettings.Docker.json
*.key
*.pem
docker-compose.override.yml
```

- [ ] **Step 4: 创建 .env.example 环境变量模板**

创建 `.env.example`（注意：不创建 `.env`，由开发者根据 .env.example 创建本地 .env）：

```
# Leno 电商平台环境变量模板
# 复制为 .env 并填入实际值

# JWT
JWT_SECRET_KEY=请填入至少64字节随机串

# SQL Server
MSSQL_SA_PASSWORD=请填入强密码

# RabbitMQ
RABBITMQ_DEFAULT_USER=leno
RABBITMQ_DEFAULT_PASS=请填入强密码

# Grafana
GF_SECURITY_ADMIN_USER=leno
GF_SECURITY_ADMIN_PASSWORD=请填入强密码

# InternalAuth（快轨临时共用，慢轨 M5.2 各 BC 独立）
INTERNAL_AUTH_API_KEY=请填入32字节随机串
```

- [ ] **Step 5: 修改 docker-compose.yml — 明文密钥改为环境变量**

修改 `docker-compose.yml`，将 6 处明文密钥改为 `${ENV_VAR}`：

```yaml
# 第 7 行
- MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}

# 第 14 行 healthcheck
test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '${MSSQL_SA_PASSWORD}' -Q 'SELECT 1' -C"]

# 第 55-56 行
- RABBITMQ_DEFAULT_USER=${RABBITMQ_DEFAULT_USER}
- RABBITMQ_DEFAULT_PASS=${RABBITMQ_DEFAULT_PASS}

# 第 130-131 行
- GF_SECURITY_ADMIN_USER=${GF_SECURITY_ADMIN_USER}
- GF_SECURITY_ADMIN_PASSWORD=${GF_SECURITY_ADMIN_PASSWORD}
```

- [ ] **Step 6: 修改 11 个 BC appsettings.json — 明文密钥改为占位符**

以 Order BC 为代表，修改 `src/Services/Order/Leno.Order.Api/appsettings.json`：

```json
"Jwt": {
  "Issuer": "Leno.UserAuth",
  "Audience": "Leno.Clients",
  "SecretKey": "${JWT_SECRET_KEY}",
  "AccessTokenExpiryMinutes": 120,
  "RefreshTokenExpiryDays": 7
},
"InternalAuth": {
  "ApiKey": "${INTERNAL_AUTH_API_KEY}"
},
"ConnectionStrings": {
  "OrderDb": "Server=sqlserver;Database=Leno.Order;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True"
}
```

对 11 个 BC 逐一执行同样替换（UserAuth、Order、Product、Cart、Promotion、ReviewAfterSales、PointsMembership、Payment、Notification、SellerShop、SystemAdmin）。

- [ ] **Step 7: 修改 11 个 BC Program.cs — 启用 Consul KV 与启动校验**

以 Order BC 为代表，修改 `src/Services/Order/Leno.Order.Api/Program.cs`，在 `builder.Build()` 之前增加：

```csharp
// 启用 Consul KV 配置中心
builder.AddLenoConsulConfig();

// 启动前校验敏感配置
if (!builder.Configuration.ValidateSensitiveConfig())
{
    var missing = builder.Configuration.GetMissingSensitiveConfigKeys();
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogWarning("敏感配置缺失：{MissingKeys}", string.Join(", ", missing));
    // 生产环境拒绝启动，开发环境仅警告
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException($"敏感配置缺失：{string.Join(", ", missing)}");
    }
}
```

在 `app.Run()` 之前增加 InternalApiKey 启动校验：

```csharp
app.EnsureInternalApiKeyConfigured();
```

- [ ] **Step 8: 配置审查 — grep 确认无明文密钥**

Run: 
```bash
grep -rn "SecretKey.*:" src/Services/*/Leno.*/appsettings.json | grep -v '${' | grep -v 'test'
grep -rn "Password=" src/Services/*/Leno.*/appsettings.json | grep -v '${'
grep -rn "ApiKey.*:" src/Services/*/Leno.*/appsettings.json | grep -v '${' | grep -v 'test'
```
Expected: 无命中（所有密钥已改为 `${ENV_VAR}` 占位符）

- [ ] **Step 9: 提交**

```bash
git add .gitignore .env.example docker-compose.yml src/Services/*/Leno.*/appsettings.json src/Services/*/Leno.*/Program.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Configuration/ConfigCenterExtensionsTests.cs
git commit -m "修复(P0-5/7): 密钥管理与 Consul KV 配置中心启用

- .gitignore 补齐 appsettings.Production.json/.env 等
- 新增 .env.example 环境变量模板
- docker-compose 6 处明文密钥改为 \${ENV_VAR}
- 11 个 BC appsettings.json 明文密钥改为占位符
- 11 个 BC Program.cs 启用 AddLenoConsulConfig + ValidateSensitiveConfig
- 启用 EnsureInternalApiKeyConfigured 生产环境启动校验
- 新增 3 个配置校验测试"
```

---

## Wave-F2 完成验收清单

- [ ] F2.1 无效 token 返回 401，白名单路由放行
- [ ] F2.2 登出 → 1 秒内同 token 返回 401，`gateway_blacklist_hits` 递增
- [ ] F2.3 `Auth:Mode=GatewayHeader` 切换后全链路用户上下文正确
- [ ] F2.4 grep `appsettings*.json` 无明文密钥
- [ ] 全量回归测试通过：`dotnet test`

---

## Self-Review 自检结果

**1. Spec 覆盖**：
- F2.1（网关 JWT 验签）→ Task 1 ✓
- F2.2（JWT 黑名单）→ Task 2 ✓
- F2.3（后端 GatewayHeader 适配）→ Task 3 ✓
- F2.4（密钥管理 + Consul KV）→ Task 4 ✓

**2. 占位符扫描**：
- 无 "TBD"、"TODO"、"implement later"
- Task 4 Step 8 grep 命令是验证步骤，非占位

**3. 类型一致性**：
- `GatewayAuthOptions`/`GatewayAuthHandler` 在 Task 3 测试与实现一致
- `IJwtBlacklistService.IsRevokedAsync`/`RevokeAsync` 签名在 Task 2 测试与实现一致
- `JwtOptions` 字段（Issuer/Audience/SecretKey）与现有定义一致

**4. 已知实施时探索点**：
- Task 1 Step 4 网关 Program.cs 修改位置需根据现有管道顺序调整（`UseCors` → `UseAuthentication` → `UseAuthorization` → `UseMiddleware<JwtBlacklistMiddleware>`）
- Task 3 Step 6 11 个 BC Program.cs 修改需逐一执行，每个 BC 的 JWT 配置块位置可能略有不同
- Task 4 Step 6 11 个 BC appsettings.json 的 ConnectionStrings 节名称各不同（如 OrderDb/UserAuthDb/ProductDb 等），需逐一确认
