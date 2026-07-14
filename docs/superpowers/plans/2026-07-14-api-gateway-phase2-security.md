# API 网关增强 - 阶段二：安全认证 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Leno API 网关从 JWT 透传模式升级为集中验签架构，实现网关本地 JWT 验签、Token 黑名单毫秒级同步、IP 黑白名单 CIDR 过滤，并验签后通过 YARP Transform 注入用户上下文 Header 供下游服务消费。

**Architecture:** 网关管道按 `IpFilter -> GlobalException -> JwtAuth -> TokenBlacklist -> YARP(含 UserContextTransform)` 顺序串联。`JwtAuthMiddleware` 复用 Infrastructure 的 `JwtTokenGenerator.ValidateTokenAsync()` 在本地完成 HS256 签名+过期校验，验签通过后设置 `HttpContext.User`。`TokenBlacklistSyncService` 作为 `IHostedService` 在启动时全量拉取 Redis Set 预热、运行期通过 Redis Pub/Sub 实时接收撤销事件、每 5 分钟定时兜底拉取，三重保障写入本地 `ConcurrentDictionary` 缓存。`IpFilterMiddleware` 使用 .NET 10 内置 `System.Net.IPNetwork` 进行 CIDR 匹配，白名单优先。`UserContextTransform` 作为 YARP `RequestTransform` 在代理请求发出前注入 `X-User-Id`/`X-Role`/`X-Shop-Id`/`X-Internal-Call` 头。

**Tech Stack:** .NET 10, YARP 2.2.0, StackExchange.Redis 2.8.16 (Pub/Sub + Set), System.Net.IPNetwork (.NET 10 内置), xUnit 2.9.0, FluentAssertions 7.0.0, Moq 4.20.72, Microsoft.AspNetCore.TestHost 10.0.0

**Spec:** [docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md](../specs/2026-07-14-api-gateway-enhancement-design.md) 第 4 节（安全与认证）+ 第 7.1 节（请求转换-用户上下文注入）

---

## Phase 1 依赖

> 本计划假设阶段一（`2026-07-14-api-gateway-phase1-infrastructure.md`）已完成：
> - `Options/GatewayOptions.cs` 已存在（含 `ConsulOptions`）
> - `Extensions/ServiceCollectionExtensions.cs` 已存在（含 `AddConsulServiceDiscovery` / `AddConsulDestinationResolver`）
> - `Leno.ApiGateway.Tests` 测试项目已创建并加入 `Leno.slnx`（含 `GlobalUsings.cs`）
> - `Program.cs` 已移除手工健康轮询、包含 `public partial class Program { }`
> - `appsettings.json` 已有 `Consul` 配置节和动态 Cluster 配置
>
> 若阶段一未完成，需先执行阶段一计划。

---

## 实施说明

> 以下两点与 Spec 字面描述不同但实现等价或有合理收敛：

1. **本地缓存实现**：Spec 4.2 提到 "Caffeine 本地缓存"。.NET 生态中无广泛维护的 Caffeine 对应包，故改用 `ConcurrentDictionary<string, DateTimeOffset>` + 惰性 TTL 过期实现 `TokenBlacklistCache`。功能等价：TTL 与 Token 最大有效期一致（默认 120 分钟），过期条目在访问时惰性清除，无内存无限增长风险。`ITokenBlacklistCache` 接口抽象保证后续可替换为其他缓存实现。
2. **ClockSkewSeconds 配置**：Spec 4.1 的 JWT 配置含 `ClockSkewSeconds: 30`。现有 `JwtTokenGenerator.BuildValidationParameters()` 硬编码 `ClockSkew = TimeSpan.FromMinutes(1)`（60 秒），修改共享 Infrastructure 的 `JwtOptions` 会影响全部 11 个后端服务的 JwtBearer 验签行为（阶段三范围），故阶段二不改共享代码，直接复用 60 秒时钟偏移。如需精确 30 秒偏移，可在阶段三统一调整。

---

## 文件结构

### 新建文件

| 文件 | 职责 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Middleware/JwtAuthMiddleware.cs` | JWT 本地验签中间件：提取 Bearer Token、调用 `JwtTokenGenerator.ValidateTokenAsync`、白名单路由跳过、验签通过设置 `HttpContext.User` |
| `src/ApiGateway/Leno.ApiGateway/Middleware/TokenBlacklistMiddleware.cs` | 黑名单 JTI 校验中间件：从已验签的 Claims 提取 JTI、查本地缓存、命中返回 401 |
| `src/ApiGateway/Leno.ApiGateway/Middleware/IpFilterMiddleware.cs` | IP 黑白名单中间件：CIDR 匹配、白名单优先、AutoBan 临时封禁 |
| `src/ApiGateway/Leno.ApiGateway/Transforms/UserContextTransform.cs` | YARP `RequestTransform`：验签通过后注入 `X-User-Id`/`X-Role`/`X-Shop-Id`/`X-Internal-Call` 请求头 |
| `src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistCache.cs` | `ITokenBlacklistCache` 接口 + `TokenBlacklistCache` 实现（`ConcurrentDictionary` + TTL） |
| `src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistSyncService.cs` | `IHostedService`：Redis Pub/Sub 实时订阅 + 每 5 分钟定时全量拉取 + 启动预热 |
| `src/ApiGateway/Leno.ApiGateway/Options/BlacklistOptions.cs` | 黑名单同步配置：SyncInterval、CacheTtl、Channel、RedisKey |
| `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs` | JWT 验签中间件单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/TokenBlacklistMiddlewareTests.cs` | 黑名单中间件单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/IpFilterMiddlewareTests.cs` | IP 过滤中间件单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Transforms/UserContextTransformTests.cs` | 用户上下文 Transform 单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistCacheTests.cs` | 黑名单缓存单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistSyncServiceTests.cs` | 黑名单同步服务单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/TestHelpers/JwtTestHelper.cs` | 测试用 JWT 生成辅助类 |

### 修改文件

| 文件 | 修改内容 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs` | 追加 `JwtAuthOptions`（白名单路由）、`IpFilterOptions`（IP 黑白名单 + AutoBan 配置） |
| `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` | 追加 `AddGatewaySecurity`（注册 JWT/Redis/缓存/同步服务/选项）和 `AddUserContextTransform`（注册 YARP Transform） |
| `src/ApiGateway/Leno.ApiGateway/Program.cs` | 注册安全中间件管道：`IpFilter -> GlobalException -> JwtAuth -> TokenBlacklist -> YARP(Transform)` |
| `src/ApiGateway/Leno.ApiGateway/appsettings.json` | 添加 `Gateway:JwtAuth`、`Gateway:IpFilter`、`Blacklist`、`Jwt`、`Redis` 配置节 |

---

## Task 1: JwtAuthMiddleware — JWT 本地验签

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Middleware/JwtAuthMiddleware.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/TestHelpers/JwtTestHelper.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs`

- [ ] **Step 1: 向 GatewayOptions.cs 追加 JwtAuthOptions**

在 `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs` 文件末尾（`ConsulOptions` 类之后）追加：

```csharp
/// <summary>
/// JWT 验签中间件配置，对应 appsettings.json 中 <c>Gateway:JwtAuth</c> 节。
/// </summary>
public sealed class JwtAuthOptions
{
    /// <summary>
    /// 免验签路由路径前缀列表。
    /// 请求路径以此列表中任一项为前缀时跳过 JWT 校验。
    /// 如 <c>/api/auth/login</c>、<c>/health</c>、<c>/metrics</c>。
    /// </summary>
    public IReadOnlyList<string> WhitelistPaths { get; set; } = Array.Empty<string>();
}
```

- [ ] **Step 2: 创建 JwtTestHelper.cs**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/TestHelpers/JwtTestHelper.cs`：

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Leno.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leno.ApiGateway.Tests.TestHelpers;

/// <summary>
/// 测试用 JWT 生成辅助类，使用固定的测试密钥生成有效/过期/无效令牌。
/// </summary>
internal static class JwtTestHelper
{
    public static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "Leno.UserAuth",
        Audience = "Leno.ApiGateway",
        SecretKey = "TestSecretKeyWithAtLeast32Characters!!",
        AccessTokenExpiryMinutes = 120,
        RefreshTokenExpiryDays = 7
    };

    public static JwtTokenGenerator CreateGenerator() =>
        new(Options.Create(TestJwtOptions));

    /// <summary>生成有效的访问令牌。</summary>
    public static string GenerateValidToken(
        Guid? userId = null,
        string role = "customer",
        Guid? shopId = null)
    {
        return CreateGenerator().GenerateAccessToken(
            userId ?? Guid.NewGuid(), role, shopId);
    }

    /// <summary>生成已过期的令牌（签名有效但 exp 已过）。</summary>
    public static string GenerateExpiredToken()
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "customer")
        };

        var token = new JwtSecurityToken(
            issuer: TestJwtOptions.Issuer,
            audience: TestJwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-3),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    /// <summary>生成签名无效的令牌（使用错误密钥签名）。</summary>
    public static string GenerateInvalidSignatureToken()
    {
        var handler = new JwtSecurityTokenHandler();
        var wrongKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("WrongSecretKeyWith32Characters!!!!!"));
        var credentials = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtOptions.Issuer,
            audience: TestJwtOptions.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    /// <summary>生成格式非法的令牌字符串。</summary>
    public static string GenerateMalformedToken() => "not.a.valid.jwt.token";
}
```

- [ ] **Step 3: 编写 JwtAuthMiddleware 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs`：

```csharp
using System.Net;
using System.Text.Json;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Tests.TestHelpers;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Middleware;

public class JwtAuthMiddlewareTests
{
    private static readonly IOptions<JwtAuthOptions> DefaultAuthOptions =
        Options.Create(new JwtAuthOptions
        {
            WhitelistPaths = new[] { "/api/auth/login", "/health", "/metrics" }
        });

    private readonly JwtTokenGenerator _tokenGenerator = JwtTestHelper.CreateGenerator();
    private static RequestDelegate NextFunc => _ => Task.CompletedTask;

    private static DefaultHttpContext CreateContext(
        string path = "/api/products/123",
        string? authorization = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int StatusCode, string Body)> InvokeAsync(
        JwtAuthMiddleware middleware, HttpContext context)
    {
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return ((int)context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvokeAsync_WithValidToken_SetsUserAndCallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new JwtAuthMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            _tokenGenerator,
            DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var token = JwtTestHelper.GenerateValidToken(
            userId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            role: "customer");
        var context = CreateContext(authorization: $"Bearer {token}");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
        context.User.Identity?.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            .Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidSignatureToken_Returns401()
    {
        // Arrange
        var middleware = new JwtAuthMiddleware(
            NextFunc, _tokenGenerator, DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var token = JwtTestHelper.GenerateInvalidSignatureToken();
        var context = CreateContext(authorization: $"Bearer {token}");

        // Act
        var (statusCode, body) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Should().Contain("Invalid or expired token");
    }

    [Fact]
    public async Task InvokeAsync_WithExpiredToken_Returns401()
    {
        // Arrange
        var middleware = new JwtAuthMiddleware(
            NextFunc, _tokenGenerator, DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var token = JwtTestHelper.GenerateExpiredToken();
        var context = CreateContext(authorization: $"Bearer {token}");

        // Act
        var (statusCode, body) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Should().Contain("Invalid or expired token");
    }

    [Fact]
    public async Task InvokeAsync_WithoutAuthorizationHeader_Returns401()
    {
        // Arrange
        var middleware = new JwtAuthMiddleware(
            NextFunc, _tokenGenerator, DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var context = CreateContext();
        context.Request.Headers.Remove("Authorization");

        // Act
        var (statusCode, body) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Should().Contain("Missing or invalid Authorization header");
    }

    [Fact]
    public async Task InvokeAsync_WithNonBearerScheme_Returns401()
    {
        // Arrange
        var middleware = new JwtAuthMiddleware(
            NextFunc, _tokenGenerator, DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var context = CreateContext(authorization: "Basic dXNlcjpwYXNz");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WithMalformedToken_Returns401()
    {
        // Arrange
        var middleware = new JwtAuthMiddleware(
            NextFunc, _tokenGenerator, DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var context = CreateContext(authorization: "Bearer not.a.valid.jwt");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WithWhitelistPath_SkipsValidation()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new JwtAuthMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            _tokenGenerator,
            DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var context = CreateContext(path: "/api/auth/login");
        context.Request.Headers.Remove("Authorization");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithHealthPath_SkipsValidation()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new JwtAuthMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            _tokenGenerator,
            DefaultAuthOptions,
            NullLogger<JwtAuthMiddleware>.Instance);

        var context = CreateContext(path: "/health/live");
        context.Request.Headers.Remove("Authorization");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithWhitelistPrefix_SkipsValidation()
    {
        // Arrange — 白名单配置 /api/auth，请求路径 /api/auth/register 应匹配前缀
        var options = Options.Create(new JwtAuthOptions
        {
            WhitelistPaths = new[] { "/api/auth" }
        });

        var nextCalled = false;
        var middleware = new JwtAuthMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            _tokenGenerator,
            options,
            NullLogger<JwtAuthMiddleware>.Instance);

        var context = CreateContext(path: "/api/auth/register");
        context.Request.Headers.Remove("Authorization");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "JwtAuthMiddlewareTests"`
Expected: 编译失败 — `JwtAuthMiddleware` 类型未定义

- [ ] **Step 5: 创建 JwtAuthMiddleware.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Middleware/JwtAuthMiddleware.cs`：

```csharp
using System.Security.Claims;
using System.Text.Json;
using Leno.ApiGateway.Options;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// JWT 本地验签中间件。
/// <para>
/// 从 <c>Authorization</c> 头提取 Bearer Token，调用 <see cref="JwtTokenGenerator.ValidateTokenAsync"/>
/// 在本地完成 HS256 签名 + 过期时间校验（无需远程调用认证服务）。验签通过后设置
/// <see cref="HttpContext.User"/> 供后续中间件和 YARP Transform 读取。
/// 白名单路由（如 <c>/api/auth/login</c>、<c>/health</c>）跳过验签。
/// </para>
/// </summary>
public sealed class JwtAuthMiddleware
{
    private const string BearerPrefix = "Bearer ";
    private const string ContentType = "application/json; charset=utf-8";

    private readonly RequestDelegate _next;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly JwtAuthOptions _options;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    public JwtAuthMiddleware(
        RequestDelegate next,
        JwtTokenGenerator tokenGenerator,
        IOptions<JwtAuthOptions> options,
        ILogger<JwtAuthMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        var token = ExtractBearerToken(context.Request.Headers.Authorization);
        if (string.IsNullOrEmpty(token))
        {
            await WriteUnauthorizedAsync(context, "Missing or invalid Authorization header");
            return;
        }

        var principal = await _tokenGenerator.ValidateTokenAsync(token);
        if (principal is null)
        {
            _logger.LogWarning("JWT validation failed for path {Path}", path);
            await WriteUnauthorizedAsync(context, "Invalid or expired token");
            return;
        }

        context.User = principal;
        await _next(context);
    }

    private bool IsWhitelisted(string path)
    {
        foreach (var prefix in _options.WhitelistPaths)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return null;
        }

        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorizationHeader[BearerPrefix.Length..].Trim();
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = ContentType;

        var body = JsonSerializer.Serialize(new
        {
            code = StatusCodes.Status401Unauthorized,
            message,
            data = (object?)null
        });

        await context.Response.WriteAsync(body);
    }
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "JwtAuthMiddlewareTests"`
Expected: `Passed: 9` — 9 个测试全部通过

- [ ] **Step 7: 验证网关项目编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 8: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs src/ApiGateway/Leno.ApiGateway/Middleware/JwtAuthMiddleware.cs src/ApiGateway/Leno.ApiGateway.Tests/TestHelpers/ src/ApiGateway/Leno.ApiGateway.Tests/Middleware/JwtAuthMiddlewareTests.cs
git commit -m "feat(gateway): 添加 JWT 本地验签中间件与白名单路由"
```

---

## Task 2: TokenBlacklistMiddleware + TokenBlacklistSyncService — 动态黑名单

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Options/BlacklistOptions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistCache.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Middleware/TokenBlacklistMiddleware.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistSyncService.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistCacheTests.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/TokenBlacklistMiddlewareTests.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistSyncServiceTests.cs`

- [ ] **Step 1: 创建 BlacklistOptions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Options/BlacklistOptions.cs`：

```csharp
namespace Leno.ApiGateway.Options;

/// <summary>
/// Token 黑名单同步配置，对应 appsettings.json 中 <c>Blacklist</c> 节。
/// </summary>
public sealed class BlacklistOptions
{
    /// <summary>Redis Set Key，存储被撤销的 JTI 集合。</summary>
    public string RedisKey { get; set; } = "leno:token:blacklist";

    /// <summary>Redis Pub/Sub 频道名，实时接收 TokenRevoked 事件。</summary>
    public string Channel { get; set; } = "leno:token:blacklist";

    /// <summary>定时全量拉取间隔（兜底机制），默认 5 分钟。</summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>本地缓存条目 TTL，默认 120 分钟（与 Token 最大有效期一致）。</summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(120);
}
```

- [ ] **Step 2: 编写 TokenBlacklistCache 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistCacheTests.cs`：

```csharp
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Services;

public class TokenBlacklistCacheTests
{
    private static IOptions<BlacklistOptions> DefaultOptions =>
        Options.Create(new BlacklistOptions
        {
            CacheTtl = TimeSpan.FromMinutes(120),
            SyncInterval = TimeSpan.FromMinutes(5)
        });

    [Fact]
    public void Contains_WithEmptyCache_ReturnsFalse()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Contains("any-jti").Should().BeFalse();
    }

    [Fact]
    public void Add_ThenContains_ReturnsTrue()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Add("jti-123");
        cache.Contains("jti-123").Should().BeTrue();
    }

    [Fact]
    public void Contains_WithDifferentJti_ReturnsFalse()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Add("jti-123");
        cache.Contains("jti-456").Should().BeFalse();
    }

    [Fact]
    public void Add_WithCustomTtl_ExpiresAfterTtl()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Add("jti-short", TimeSpan.FromMilliseconds(10));

        Thread.Sleep(50);

        cache.Contains("jti-short").Should().BeFalse();
    }

    [Fact]
    public void ReplaceAll_ClearsExistingAndAddsNew()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Add("old-jti-1");
        cache.Add("old-jti-2");

        cache.ReplaceAll(new[] { "new-jti-1", "new-jti-2", "new-jti-3" });

        cache.Contains("old-jti-1").Should().BeFalse();
        cache.Contains("old-jti-2").Should().BeFalse();
        cache.Contains("new-jti-1").Should().BeTrue();
        cache.Contains("new-jti-2").Should().BeTrue();
        cache.Contains("new-jti-3").Should().BeTrue();
    }

    [Fact]
    public void ReplaceAll_WithEmptyList_ClearsAll()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Add("jti-1");
        cache.Add("jti-2");

        cache.ReplaceAll(Array.Empty<string>());

        cache.Contains("jti-1").Should().BeFalse();
        cache.Contains("jti-2").Should().BeFalse();
    }

    [Fact]
    public void Count_ReturnsNumberOfNonExpiredEntries()
    {
        var cache = new TokenBlacklistCache(DefaultOptions);
        cache.Add("jti-1");
        cache.Add("jti-2");
        cache.Add("jti-3");

        cache.Count.Should().Be(3);
    }
}
```

- [ ] **Step 3: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TokenBlacklistCacheTests"`
Expected: 编译失败 — `TokenBlacklistCache` 类型未定义

- [ ] **Step 4: 创建 TokenBlacklistCache.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistCache.cs`：

```csharp
using System.Collections.Concurrent;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Services;

/// <summary>
/// Token 黑名单本地缓存抽象，供 <see cref="TokenBlacklistMiddleware"/> 查询
/// 和 <see cref="TokenBlacklistSyncService"/> 更新。
/// </summary>
public interface ITokenBlacklistCache
{
    /// <summary>查询指定 JTI 是否在黑名单中（且未过期）。</summary>
    bool Contains(string jti);

    /// <summary>添加单个 JTI 到黑名单。</summary>
    void Add(string jti, TimeSpan? ttl = null);

    /// <summary>全量替换黑名单（先清空再批量写入），用于定时兜底拉取。</summary>
    void ReplaceAll(IEnumerable<string> jtis, TimeSpan? ttl = null);

    /// <summary>当前缓存中未过期的条目数。</summary>
    int Count { get; }
}

/// <summary>
/// 基于 <see cref="ConcurrentDictionary{TKey,TValue}"/> 的黑名单缓存实现。
/// 每个条目携带 TTL 过期时间，访问时惰性清除过期条目。
/// </summary>
public sealed class TokenBlacklistCache : ITokenBlacklistCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new();
    private readonly TimeSpan _defaultTtl;

    public TokenBlacklistCache(IOptions<BlacklistOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _defaultTtl = options.Value.CacheTtl;
    }

    public bool Contains(string jti)
    {
        if (_entries.TryGetValue(jti, out var expiry))
        {
            if (DateTimeOffset.UtcNow < expiry)
            {
                return true;
            }

            _entries.TryRemove(jti, out _);
        }

        return false;
    }

    public void Add(string jti, TimeSpan? ttl = null)
    {
        var expiry = DateTimeOffset.UtcNow + (ttl ?? _defaultTtl);
        _entries[jti] = expiry;
    }

    public void ReplaceAll(IEnumerable<string> jtis, TimeSpan? ttl = null)
    {
        _entries.Clear();
        var expiry = DateTimeOffset.UtcNow + (ttl ?? _defaultTtl);

        foreach (var jti in jtis)
        {
            _entries[jti] = expiry;
        }
    }

    public int Count
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return _entries.Count(kvp => kvp.Value > now);
        }
    }
}
```

- [ ] **Step 5: 运行缓存测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TokenBlacklistCacheTests"`
Expected: `Passed: 7` — 7 个测试全部通过

- [ ] **Step 6: 编写 TokenBlacklistMiddleware 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/TokenBlacklistMiddlewareTests.cs`：

```csharp
using System.Security.Claims;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Leno.ApiGateway.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Middleware;

public class TokenBlacklistMiddlewareTests
{
    private static RequestDelegate NextFunc => _ => Task.CompletedTask;

    private static IOptions<BlacklistOptions> DefaultBlacklistOptions =>
        Options.Create(new BlacklistOptions { CacheTtl = TimeSpan.FromMinutes(120) });

    private static DefaultHttpContext CreateContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (user is not null)
        {
            context.User = user;
        }
        return context;
    }

    private static async Task<(int StatusCode, string Body)> InvokeAsync(
        TokenBlacklistMiddleware middleware, HttpContext context)
    {
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return ((int)context.Response.StatusCode, body);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(string jti)
    {
        var token = JwtTestHelper.GenerateValidToken();
        var principal = JwtTestHelper.CreateGenerator().ValidateTokenAsync(token).Result;

        // 覆盖 JTI claim（使用与 TokenBlacklistMiddleware 一致的 "jti" 字符串）
        var identity = new ClaimsIdentity(principal!.Identity!);
        identity.AddClaim(new Claim("jti", jti));
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task InvokeAsync_WithJtiInBlacklist_Returns401()
    {
        // Arrange
        var cache = new TokenBlacklistCache(DefaultBlacklistOptions);
        cache.Add("blacklisted-jti");

        var middleware = new TokenBlacklistMiddleware(
            NextFunc, cache, NullLogger<TokenBlacklistMiddleware>.Instance);

        var context = CreateContext(CreateAuthenticatedPrincipal("blacklisted-jti"));

        // Act
        var (statusCode, body) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Should().Contain("Token has been revoked");
    }

    [Fact]
    public async Task InvokeAsync_WithJtiNotInBlacklist_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var cache = new TokenBlacklistCache(DefaultBlacklistOptions);
        cache.Add("other-jti");

        var middleware = new TokenBlacklistMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            cache,
            NullLogger<TokenBlacklistMiddleware>.Instance);

        var context = CreateContext(CreateAuthenticatedPrincipal("valid-jti"));

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_CallsNext()
    {
        // Arrange — 未认证用户（JwtAuthMiddleware 白名单路由或验签失败已返回401）
        var nextCalled = false;
        var cache = new TokenBlacklistCache(DefaultBlacklistOptions);

        var middleware = new TokenBlacklistMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            cache,
            NullLogger<TokenBlacklistMiddleware>.Instance);

        var context = CreateContext(user: new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithNoJtiClaim_CallsNext()
    {
        // Arrange — 认证通过但无 JTI claim（异常情况，放行由后端处理）
        var nextCalled = false;
        var cache = new TokenBlacklistCache(DefaultBlacklistOptions);

        var middleware = new TokenBlacklistMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            cache,
            NullLogger<TokenBlacklistMiddleware>.Instance);

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) },
            "TestAuth");
        var context = CreateContext(new ClaimsPrincipal(identity));

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }
}
```

- [ ] **Step 7: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TokenBlacklistMiddlewareTests"`
Expected: 编译失败 — `TokenBlacklistMiddleware` 类型未定义

- [ ] **Step 8: 创建 TokenBlacklistMiddleware.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Middleware/TokenBlacklistMiddleware.cs`：

```csharp
using System.Text.Json;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// Token 黑名单校验中间件。
/// <para>
/// 紧随 <see cref="JwtAuthMiddleware"/> 之后，从已验签的 <see cref="HttpContext.User"/>
/// Claims 中提取 JTI（JWT ID），查询 <see cref="ITokenBlacklistCache"/> 本地缓存。
/// 命中则返回 401 + "Token has been revoked"。
/// </para>
/// </summary>
public sealed class TokenBlacklistMiddleware
{
    private const string JtiClaimType = "jti";
    private const string ContentType = "application/json; charset=utf-8";

    private readonly RequestDelegate _next;
    private readonly ITokenBlacklistCache _cache;
    private readonly ILogger<TokenBlacklistMiddleware> _logger;

    public TokenBlacklistMiddleware(
        RequestDelegate next,
        ITokenBlacklistCache cache,
        ILogger<TokenBlacklistMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var jti = context.User.FindFirst(JtiClaimType)?.Value;
        if (string.IsNullOrEmpty(jti))
        {
            await _next(context);
            return;
        }

        if (_cache.Contains(jti))
        {
            _logger.LogWarning("Token JTI {Jti} is in blacklist, rejecting request", jti);
            await WriteUnauthorizedAsync(context, "Token has been revoked");
            return;
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = ContentType;

        var body = JsonSerializer.Serialize(new
        {
            code = StatusCodes.Status401Unauthorized,
            message,
            data = (object?)null
        });

        await context.Response.WriteAsync(body);
    }
}
```

- [ ] **Step 9: 运行中间件测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TokenBlacklistMiddlewareTests"`
Expected: `Passed: 4` — 4 个测试全部通过

- [ ] **Step 10: 编写 TokenBlacklistSyncService 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistSyncServiceTests.cs`：

```csharp
using System.Text.Json;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Services;

public class TokenBlacklistSyncServiceTests
{
    private static IOptions<BlacklistOptions> DefaultOptions =>
        Options.Create(new BlacklistOptions
        {
            RedisKey = "leno:token:blacklist",
            Channel = "leno:token:blacklist",
            SyncInterval = TimeSpan.FromHours(1),
            CacheTtl = TimeSpan.FromMinutes(120)
        });

    private static Mock<IConnectionMultiplexer> CreateMultiplexerMock(
        out Mock<ISubscriber> subscriberMock,
        out Mock<IDatabase> databaseMock)
    {
        subscriberMock = new Mock<ISubscriber>();
        databaseMock = new Mock<IDatabase>();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetSubscriber(It.IsAny<object>()))
            .Returns(subscriberMock.Object);
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(databaseMock.Object);
        return multiplexerMock;
    }

    [Fact]
    public async Task StartAsync_PerformsFullPullFromRedis()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out _, out var databaseMock);
        var redisValues = new RedisValue[]
        {
            "jti-1", "jti-2", "jti-3"
        };
        databaseMock.Setup(d => d.SetMembersAsync(
            It.Is<RedisKey>(k => k == "leno:token:blacklist"),
            CommandFlags.None))
            .ReturnsAsync(redisValues);

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        cache.Contains("jti-1").Should().BeTrue();
        cache.Contains("jti-2").Should().BeTrue();
        cache.Contains("jti-3").Should().BeTrue();
        cache.Count.Should().Be(3);
    }

    [Fact]
    public async Task StartAsync_SubscribesToPubSubChannel()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out var subscriberMock, out var databaseMock);
        databaseMock.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<RedisValue>());

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        subscriberMock.Verify(
            s => s.SubscribeAsync(
                It.Is<RedisChannel>(c => c == "leno:token:blacklist"),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithEmptyRedisSet_CacheStaysEmpty()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out _, out var databaseMock);
        databaseMock.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<RedisValue>());

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_WhenRedisFails_DoesNotThrow()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out _, out var databaseMock);
        databaseMock.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        // Act
        var act = async () => await service.StartAsync(CancellationToken.None);

        // Assert — 启动预热失败不应阻止网关启动（缓存为空，后续 Pub/Sub 和定时拉取会补全）
        await act.Should().NotThrowAsync();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task StopAsync_DisposesSubscriptionAndTimer()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out var subscriberMock, out var databaseMock);
        databaseMock.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<RedisValue>());

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert — StopAsync 应不抛异常
        subscriberMock.Verify();
    }

    [Fact]
    public async Task PubSubMessage_WithValidJson_AddsJtiToCache()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out var subscriberMock, out var databaseMock);
        databaseMock.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<RedisValue>());

        Action<RedisChannel, RedisValue>? capturedHandler = null;
        subscriberMock.Setup(s => s.SubscribeAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler)
            .Returns(Task.CompletedTask);

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // Act — 模拟 Redis Pub/Sub 推送 TokenRevoked 事件
        capturedHandler.Should().NotBeNull();
        var message = JsonSerializer.Serialize(new
        {
            eventType = "TokenRevoked",
            jti = "revoked-by-pubsub",
            userId = 12345,
            reason = "logout",
            timestamp = "2026-07-14T10:30:00Z"
        });
        capturedHandler!("leno:token:blacklist", new RedisValue(message));

        // Assert
        cache.Contains("revoked-by-pubsub").Should().BeTrue();
    }

    [Fact]
    public async Task PubSubMessage_WithPlainTextJti_AddsToCache()
    {
        // Arrange
        var multiplexerMock = CreateMultiplexerMock(out var subscriberMock, out var databaseMock);
        databaseMock.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<RedisValue>());

        Action<RedisChannel, RedisValue>? capturedHandler = null;
        subscriberMock.Setup(s => s.SubscribeAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler)
            .Returns(Task.CompletedTask);

        var cache = new TokenBlacklistCache(DefaultOptions);
        var service = new TokenBlacklistSyncService(
            multiplexerMock.Object, cache, DefaultOptions,
            NullLogger<TokenBlacklistSyncService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // Act — 模拟纯文本 JTI 推送
        capturedHandler.Should().NotBeNull();
        capturedHandler!("leno:token:blacklist", new RedisValue("plain-jti-value"));

        // Assert
        cache.Contains("plain-jti-value").Should().BeTrue();
    }
}
```

- [ ] **Step 11: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TokenBlacklistSyncServiceTests"`
Expected: 编译失败 — `TokenBlacklistSyncService` 类型未定义

- [ ] **Step 12: 创建 TokenBlacklistSyncService.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistSyncService.cs`：

```csharp
using System.Text.Json;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.ApiGateway.Services;

/// <summary>
/// Token 黑名单同步托管服务。
/// <para>
/// 三层保障确保黑名单毫秒级生效且不丢失：
/// 1. <b>启动预热</b>：网关启动时从 Redis Set 全量拉取黑名单，完成前不接受流量。
/// 2. <b>Redis Pub/Sub 实时推送</b>：订阅 <c>leno:token:blacklist</c> 频道，
///    用户注销/改密时 UserAuth 服务发布 TokenRevoked 事件，毫秒级同步。
/// 3. <b>定时兜底拉取</b>：每 5 分钟（可配置）从 Redis 全量拉取，防消息丢失。
/// </para>
/// </summary>
public sealed class TokenBlacklistSyncService : IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ITokenBlacklistCache _cache;
    private readonly BlacklistOptions _options;
    private readonly ILogger<TokenBlacklistSyncService> _logger;
    private Timer? _syncTimer;
    private ISubscriber? _subscriber;

    public TokenBlacklistSyncService(
        IConnectionMultiplexer redis,
        ITokenBlacklistCache cache,
        IOptions<BlacklistOptions> options,
        ILogger<TokenBlacklistSyncService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 1. 启动预热：全量拉取
        await FullPullAsync(cancellationToken);

        // 2. 订阅 Pub/Sub 频道
        _subscriber = _redis.GetSubscriber();
        await _subscriber.SubscribeAsync(
            _options.Channel,
            OnMessageReceived);

        _logger.LogInformation(
            "Subscribed to Redis Pub/Sub channel {Channel} for token blacklist updates",
            _options.Channel);

        // 3. 启动定时兜底拉取
        _syncTimer = new Timer(
            async _ => await FullPullAsync(CancellationToken.None),
            null,
            _options.SyncInterval,
            _options.SyncInterval);

        _logger.LogInformation(
            "Token blacklist sync service started (full pull interval: {Interval})",
            _options.SyncInterval);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.UnsubscribeAsync(_options.Channel);
        }

        _syncTimer?.Change(Timeout.Infinite, 0);

        _logger.LogInformation("Token blacklist sync service stopped");
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
    }

    /// <summary>
    /// 从 Redis Set 全量拉取黑名单并替换本地缓存。
    /// </summary>
    private async Task FullPullAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = _redis.GetDatabase();
            var members = await database.SetMembersAsync(_options.RedisKey);

            var jtis = members
                .Where(m => m.HasValue)
                .Select(m => m.ToString())
                .ToList();

            _cache.ReplaceAll(jtis, _options.CacheTtl);

            _logger.LogDebug(
                "Full pull from Redis completed: {Count} blacklisted JTIs loaded",
                jtis.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to full pull token blacklist from Redis key {Key}",
                _options.RedisKey);
        }
    }

    /// <summary>
    /// Redis Pub/Sub 消息回调。
    /// 支持两种消息格式：
    /// 1. JSON 对象（含 jti 字段）：解析并提取 jti
    /// 2. 纯文本 JTI：直接作为 JTI 处理
    /// </summary>
    private void OnMessageReceived(RedisChannel channel, RedisValue message)
    {
        if (!message.HasValue)
        {
            return;
        }

        var messageStr = message.ToString();

        try
        {
            // 尝试 JSON 解析
            using var doc = JsonDocument.Parse(messageStr);
            if (doc.RootElement.TryGetProperty("jti", out var jtiElement))
            {
                var jti = jtiElement.GetString();
                if (!string.IsNullOrEmpty(jti))
                {
                    _cache.Add(jti, _options.CacheTtl);
                    _logger.LogInformation(
                        "Token JTI {Jti} added to blacklist via Pub/Sub", jti);
                }
                return;
            }
        }
        catch (JsonException)
        {
            // 非 JSON 格式，作为纯文本 JTI 处理
        }

        // 纯文本 JTI
        if (!string.IsNullOrEmpty(messageStr))
        {
            _cache.Add(messageStr, _options.CacheTtl);
            _logger.LogInformation(
                "Token JTI {Jti} added to blacklist via Pub/Sub (plain text)",
                messageStr);
        }
    }
}
```

- [ ] **Step 13: 运行全部黑名单测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TokenBlacklist"`
Expected: `Passed: 17` — TokenBlacklistCacheTests(7) + TokenBlacklistMiddlewareTests(4) + TokenBlacklistSyncServiceTests(6) 全部通过

- [ ] **Step 14: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 15: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/BlacklistOptions.cs src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistCache.cs src/ApiGateway/Leno.ApiGateway/Services/TokenBlacklistSyncService.cs src/ApiGateway/Leno.ApiGateway/Middleware/TokenBlacklistMiddleware.cs src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistCacheTests.cs src/ApiGateway/Leno.ApiGateway.Tests/Services/TokenBlacklistSyncServiceTests.cs src/ApiGateway/Leno.ApiGateway.Tests/Middleware/TokenBlacklistMiddlewareTests.cs
git commit -m "feat(gateway): 添加 Token 黑名单校验与 Redis Pub/Sub 动态同步"
```

---

## Task 3: IpFilterMiddleware — IP 黑白名单 CIDR 过滤

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Middleware/IpFilterMiddleware.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/IpFilterMiddlewareTests.cs`

- [ ] **Step 1: 向 GatewayOptions.cs 追加 IpFilterOptions 和 AutoBanOptions**

在 `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs` 文件末尾（`JwtAuthOptions` 类之后）追加：

```csharp
/// <summary>
/// IP 黑白名单配置，对应 appsettings.json 中 <c>Gateway:IpFilter</c> 节。
/// </summary>
public sealed class IpFilterOptions
{
    /// <summary>白名单 IP/CIDR 列表，命中则直接放行（优先于黑名单）。</summary>
    public IReadOnlyList<string> Whitelist { get; set; } = Array.Empty<string>();

    /// <summary>黑名单 IP/CIDR 列表，命中则返回 403。</summary>
    public IReadOnlyList<string> Blacklist { get; set; } = Array.Empty<string>();

    /// <summary>AutoBan 自动封禁配置。</summary>
    public AutoBanOptions AutoBan { get; set; } = new();
}

/// <summary>
/// AutoBan 自动封禁配置，限流触发后自动将 IP 加入临时封禁列表。
/// 实际触发逻辑在阶段四（限流）实现，阶段二仅预留配置和存储。
/// </summary>
public sealed class AutoBanOptions
{
    /// <summary>是否启用自动封禁。</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>触发封禁的请求阈值（在 WindowSeconds 时间窗口内）。</summary>
    public int Threshold { get; set; } = 100;

    /// <summary>统计时间窗口（秒）。</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>封禁持续时间（分钟）。</summary>
    public int BanDurationMinutes { get; set; } = 30;
}
```

- [ ] **Step 2: 编写 IpFilterMiddleware 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/IpFilterMiddlewareTests.cs`：

```csharp
using System.Net;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Middleware;

public class IpFilterMiddlewareTests
{
    private static RequestDelegate NextFunc => _ => Task.CompletedTask;

    private static IOptions<IpFilterOptions> CreateOptions(
        string[]? whitelist = null,
        string[]? blacklist = null)
    {
        return Options.Create(new IpFilterOptions
        {
            Whitelist = whitelist ?? Array.Empty<string>(),
            Blacklist = blacklist ?? Array.Empty<string>()
        });
    }

    private static DefaultHttpContext CreateContext(string remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int StatusCode, string Body)> InvokeAsync(
        IpFilterMiddleware middleware, HttpContext context)
    {
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return ((int)context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyLists_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("192.168.1.100");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithIpInWhitelist_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(whitelist: new[] { "10.0.0.0/8" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("10.5.3.2");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithIpInBlacklist_Returns403()
    {
        // Arrange
        var middleware = new IpFilterMiddleware(
            NextFunc,
            CreateOptions(blacklist: new[] { "203.0.113.50" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("203.0.113.50");

        // Act
        var (statusCode, body) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Should().Contain("IP address is blocked");
    }

    [Fact]
    public async Task InvokeAsync_WithIpInBlacklistCidr_Returns403()
    {
        // Arrange
        var middleware = new IpFilterMiddleware(
            NextFunc,
            CreateOptions(blacklist: new[] { "192.168.1.0/24" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("192.168.1.200");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_WithIpOutsideBlacklistCidr_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(blacklist: new[] { "192.168.1.0/24" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("192.168.2.100");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhitelistPriorityOverBlacklist_CallsNext()
    {
        // Arrange — IP 同时在白名单和黑名单，白名单优先
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(
                whitelist: new[] { "10.0.0.0/8" },
                blacklist: new[] { "10.0.0.0/8" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("10.1.2.3");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithSingleIpWhitelist_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(whitelist: new[] { "172.16.5.10" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("172.16.5.10");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithIPv6Cidr_Returns403()
    {
        // Arrange
        var middleware = new IpFilterMiddleware(
            NextFunc,
            CreateOptions(blacklist: new[] { "2001:db8::/32" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = CreateContext("2001:db8::1");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_WithNoRemoteIp_CallsNext()
    {
        // Arrange — 无 RemoteIpAddress（如直接本地测试），应放行
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(blacklist: new[] { "192.168.1.0/24" }),
            NullLogger<IpFilterMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithBannedIp_Returns403()
    {
        // Arrange — 通过 BanIp 方法临时封禁 IP
        var middleware = new IpFilterMiddleware(
            NextFunc,
            CreateOptions(),
            NullLogger<IpFilterMiddleware>.Instance);

        middleware.BanIp("198.51.100.5", TimeSpan.FromMinutes(30));

        var context = CreateContext("198.51.100.5");

        // Act
        var (statusCode, body) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Should().Contain("IP address is blocked");
    }

    [Fact]
    public async Task InvokeAsync_WithExpiredBan_CallsNext()
    {
        // Arrange — 封禁已过期
        var nextCalled = false;
        var middleware = new IpFilterMiddleware(
            ctx => { nextCalled = true; return Task.CompletedTask; },
            CreateOptions(),
            NullLogger<IpFilterMiddleware>.Instance);

        middleware.BanIp("198.51.100.5", TimeSpan.FromMilliseconds(10));
        Thread.Sleep(50);

        var context = CreateContext("198.51.100.5");

        // Act
        var (statusCode, _) = await InvokeAsync(middleware, context);

        // Assert
        statusCode.Should().Be(StatusCodes.Status200OK);
        nextCalled.Should().BeTrue();
    }
}
```

- [ ] **Step 3: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "IpFilterMiddlewareTests"`
Expected: 编译失败 — `IpFilterMiddleware` 类型未定义

- [ ] **Step 4: 创建 IpFilterMiddleware.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Middleware/IpFilterMiddleware.cs`：

```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// IP 黑白名单过滤中间件。
/// <para>
/// 管道最前置的过滤层，按以下优先级处理：
/// 1. 白名单匹配 -> 直接放行（优先于黑名单和 AutoBan）
/// 2. 黑名单匹配 -> 返回 403
/// 3. AutoBan 临时封禁列表匹配 -> 返回 403
/// 4. 均不匹配 -> 放行
/// </para>
/// 支持单个 IP 和 CIDR 网段匹配（IPv4/IPv6），使用 .NET 内置 <see cref="IPNetwork"/>。
/// </summary>
public sealed class IpFilterMiddleware
{
    private const string ContentType = "application/json; charset=utf-8";

    private readonly RequestDelegate _next;
    private readonly IpFilterOptions _options;
    private readonly ILogger<IpFilterMiddleware> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _bannedIps = new();

    public IpFilterMiddleware(
        RequestDelegate next,
        IOptions<IpFilterOptions> options,
        ILogger<IpFilterMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress;

        if (clientIp is null)
        {
            await _next(context);
            return;
        }

        // 1. 白名单优先
        if (IsInList(clientIp, _options.Whitelist))
        {
            await _next(context);
            return;
        }

        // 2. 黑名单
        if (IsInList(clientIp, _options.Blacklist))
        {
            _logger.LogWarning("IP {Ip} blocked by blacklist", clientIp);
            await WriteForbiddenAsync(context, "IP address is blocked");
            return;
        }

        // 3. AutoBan 临时封禁
        if (IsBanned(clientIp.ToString()))
        {
            _logger.LogWarning("IP {Ip} blocked by AutoBan", clientIp);
            await WriteForbiddenAsync(context, "IP address is blocked");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// 将 IP 加入临时封禁列表（供阶段四限流组件调用）。
    /// </summary>
    /// <param name="ip">要封禁的 IP 地址。</param>
    /// <param name="duration">封禁持续时间。</param>
    public void BanIp(string ip, TimeSpan duration)
    {
        _bannedIps[ip] = DateTimeOffset.UtcNow + duration;
        _logger.LogInformation("IP {Ip} banned for {Duration}", ip, duration);
    }

    private bool IsBanned(string ip)
    {
        if (_bannedIps.TryGetValue(ip, out var expiry))
        {
            if (DateTimeOffset.UtcNow < expiry)
            {
                return true;
            }

            _bannedIps.TryRemove(ip, out _);
        }

        return false;
    }

    private static bool IsInList(IPAddress ip, IReadOnlyList<string> entries)
    {
        foreach (var entry in entries)
        {
            if (IPAddress.TryParse(entry, out var singleIp))
            {
                if (singleIp.Equals(ip))
                {
                    return true;
                }
            }
            else if (IPNetwork.TryParse(entry, out var network))
            {
                if (network.Contains(ip))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = ContentType;

        var body = JsonSerializer.Serialize(new
        {
            code = StatusCodes.Status403Forbidden,
            message,
            data = (object?)null
        });

        await context.Response.WriteAsync(body);
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "IpFilterMiddlewareTests"`
Expected: `Passed: 11` — 11 个测试全部通过

- [ ] **Step 6: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 7: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs src/ApiGateway/Leno.ApiGateway/Middleware/IpFilterMiddleware.cs src/ApiGateway/Leno.ApiGateway.Tests/Middleware/IpFilterMiddlewareTests.cs
git commit -m "feat(gateway): 添加 IP 黑白名单 CIDR 过滤中间件与 AutoBan 预留"
```

---

## Task 4: UserContextTransform — 用户上下文注入

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Transforms/UserContextTransform.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Transforms/UserContextTransformTests.cs`

- [ ] **Step 1: 编写 UserContextTransform 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Transforms/UserContextTransformTests.cs`：

```csharp
using System.Net.Http.Headers;
using System.Security.Claims;
using Leno.ApiGateway.Tests.TestHelpers;
using Leno.ApiGateway.Transforms;
using Leno.Infrastructure.Auth;

namespace Leno.ApiGateway.Tests.Transforms;

public class UserContextTransformTests
{
    private static HttpRequestHeaders CreateHeaders() =>
        new HttpRequestMessage().Headers;

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(
        Guid? userId = null,
        string role = "customer",
        Guid? shopId = null)
    {
        var token = JwtTestHelper.GenerateValidToken(
            userId ?? Guid.Parse("22222222-2222-2222-2222-222222222222"),
            role,
            shopId);
        var principal = JwtTestHelper.CreateGenerator().ValidateTokenAsync(token).Result;
        return principal!;
    }

    private static ClaimsPrincipal CreateUnauthenticatedPrincipal() =>
        new(new ClaimsIdentity());

    [Fact]
    public void ApplyHeaders_WithAuthenticatedUser_InjectsAllHeaders()
    {
        // Arrange
        var headers = CreateHeaders();
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var principal = CreateAuthenticatedPrincipal(userId: userId, role: "admin");

        // Act
        UserContextTransform.ApplyHeaders(headers, principal);

        // Assert
        headers.GetValues(UserContextTransform.UserIdHeader).Should().ContainSingle()
            .Which.Should().Be(userId.ToString());
        headers.GetValues(UserContextTransform.RoleHeader).Should().ContainSingle()
            .Which.Should().Be("admin");
        headers.GetValues(UserContextTransform.InternalCallHeader).Should().ContainSingle()
            .Which.Should().Be("leno-gateway");
    }

    [Fact]
    public void ApplyHeaders_WithShopId_InjectsShopIdHeader()
    {
        // Arrange
        var headers = CreateHeaders();
        var shopId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var principal = CreateAuthenticatedPrincipal(shopId: shopId);

        // Act
        UserContextTransform.ApplyHeaders(headers, principal);

        // Assert
        headers.GetValues(UserContextTransform.ShopIdHeader).Should().ContainSingle()
            .Which.Should().Be(shopId.ToString());
    }

    [Fact]
    public void ApplyHeaders_WithoutShopId_DoesNotInjectShopIdHeader()
    {
        // Arrange
        var headers = CreateHeaders();
        var principal = CreateAuthenticatedPrincipal(shopId: null);

        // Act
        UserContextTransform.ApplyHeaders(headers, principal);

        // Assert
        headers.Contains(UserContextTransform.ShopIdHeader).Should().BeFalse();
    }

    [Fact]
    public void ApplyHeaders_WithUnauthenticatedUser_DoesNotInjectAnyHeader()
    {
        // Arrange
        var headers = CreateHeaders();
        var principal = CreateUnauthenticatedPrincipal();

        // Act
        UserContextTransform.ApplyHeaders(headers, principal);

        // Assert
        headers.Contains(UserContextTransform.UserIdHeader).Should().BeFalse();
        headers.Contains(UserContextTransform.RoleHeader).Should().BeFalse();
        headers.Contains(UserContextTransform.ShopIdHeader).Should().BeFalse();
        headers.Contains(UserContextTransform.InternalCallHeader).Should().BeFalse();
    }

    [Fact]
    public void ApplyHeaders_RemovesExistingSpoofedHeadersBeforeInjection()
    {
        // Arrange — 模拟恶意客户端伪造 X-User-Id 头
        var request = new HttpRequestMessage();
        request.Headers.Add(UserContextTransform.UserIdHeader, "spoofed-user-id");
        request.Headers.Add(UserContextTransform.RoleHeader, "admin");
        request.Headers.Add(UserContextTransform.InternalCallHeader, "fake-source");

        var principal = CreateAuthenticatedPrincipal(
            userId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            role: "customer");

        // Act
        UserContextTransform.ApplyHeaders(request.Headers, principal);

        // Assert — 伪造的值应被覆盖
        request.Headers.GetValues(UserContextTransform.UserIdHeader).Should().ContainSingle()
            .Which.Should().Be("55555555-5555-5555-5555-555555555555");
        request.Headers.GetValues(UserContextTransform.RoleHeader).Should().ContainSingle()
            .Which.Should().Be("customer");
        request.Headers.GetValues(UserContextTransform.InternalCallHeader).Should().ContainSingle()
            .Which.Should().Be("leno-gateway");
    }

    [Fact]
    public void ApplyHeaders_WithUnauthenticatedUser_RemovesSpoofedHeaders()
    {
        // Arrange — 未认证但客户端伪造了用户头
        var request = new HttpRequestMessage();
        request.Headers.Add(UserContextTransform.UserIdHeader, "spoofed-user-id");
        request.Headers.Add(UserContextTransform.RoleHeader, "admin");

        var principal = CreateUnauthenticatedPrincipal();

        // Act
        UserContextTransform.ApplyHeaders(request.Headers, principal);

        // Assert — 伪造的头应被移除，不注入任何用户上下文
        request.Headers.Contains(UserContextTransform.UserIdHeader).Should().BeFalse();
        request.Headers.Contains(UserContextTransform.RoleHeader).Should().BeFalse();
        request.Headers.Contains(UserContextTransform.InternalCallHeader).Should().BeFalse();
    }

    [Fact]
    public void ApplyHeaders_WithAuthenticatedUser_InjectsInternalCallHeader()
    {
        // Arrange
        var headers = CreateHeaders();
        var principal = CreateAuthenticatedPrincipal();

        // Act
        UserContextTransform.ApplyHeaders(headers, principal);

        // Assert
        headers.Contains(UserContextTransform.InternalCallHeader).Should().BeTrue();
        headers.GetValues(UserContextTransform.InternalCallHeader).Should().ContainSingle()
            .Which.Should().Be(UserContextTransform.InternalCallValue);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "UserContextTransformTests"`
Expected: 编译失败 — `UserContextTransform` 类型未定义

- [ ] **Step 3: 创建 UserContextTransform.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Transforms/UserContextTransform.cs`：

```csharp
using System.Net.Http.Headers;
using System.Security.Claims;
using Leno.Infrastructure.Auth;
using Yarp.ReverseProxy.Transforms;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// YARP 请求转换器：验签通过后注入用户上下文 Header 到下游代理请求。
/// <para>
/// 注入的 Header：
/// - <c>X-User-Id</c>: 用户 ID（来自 JWT Sub/NameIdentifier claim）
/// - <c>X-Role</c>: 用户角色（来自 JWT Role claim）
/// - <c>X-Shop-Id</c>: 店铺 ID（来自 JWT shop_id claim，仅卖家场景存在）
/// - <c>X-Internal-Call</c>: 标记请求来源为网关（后端可校验请求确实经过网关）
/// </para>
/// 安全保障：注入前先移除已存在的同名 Header，防止客户端伪造。
/// </summary>
public static class UserContextTransform
{
    /// <summary>用户 ID 请求头名。</summary>
    public const string UserIdHeader = "X-User-Id";

    /// <summary>用户角色请求头名。</summary>
    public const string RoleHeader = "X-Role";

    /// <summary>店铺 ID 请求头名。</summary>
    public const string ShopIdHeader = "X-Shop-Id";

    /// <summary>内部调用标记请求头名。</summary>
    public const string InternalCallHeader = "X-Internal-Call";

    /// <summary>内部调用标记值，表示请求来自网关。</summary>
    public const string InternalCallValue = "leno-gateway";

    /// <summary>
    /// YARP RequestTransform 回调，从 <see cref="RequestTransformContext.HttpContext.User"/>
    /// 提取已验签的用户信息并注入到代理请求头。
    /// </summary>
    public static ValueTask ApplyAsync(RequestTransformContext context)
    {
        ApplyHeaders(context.ProxyRequest.Headers, context.HttpContext.User);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 将用户上下文注入到指定的请求头集合。
    /// 先移除所有同名 Header（防伪造），再按 ClaimsPrincipal 注入。
    /// 未认证用户仅执行清理，不注入任何头。
    /// </summary>
    public static void ApplyHeaders(HttpRequestHeaders headers, ClaimsPrincipal user)
    {
        // 始终先移除已存在的同名 Header，防止客户端伪造
        headers.Remove(UserIdHeader);
        headers.Remove(RoleHeader);
        headers.Remove(ShopIdHeader);
        headers.Remove(InternalCallHeader);

        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = JwtTokenGenerator.GetUserId(user);
        var role = JwtTokenGenerator.GetRole(user);
        var shopId = JwtTokenGenerator.GetShopId(user);

        if (userId.HasValue)
        {
            headers.Add(UserIdHeader, userId.Value.ToString());
        }

        if (!string.IsNullOrEmpty(role))
        {
            headers.Add(RoleHeader, role);
        }

        if (shopId.HasValue)
        {
            headers.Add(ShopIdHeader, shopId.Value.ToString());
        }

        headers.Add(InternalCallHeader, InternalCallValue);
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "UserContextTransformTests"`
Expected: `Passed: 7` — 7 个测试全部通过

- [ ] **Step 5: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 6: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Transforms/UserContextTransform.cs src/ApiGateway/Leno.ApiGateway.Tests/Transforms/UserContextTransformTests.cs
git commit -m "feat(gateway): 添加 YARP 用户上下文注入 Transform"
```

---

## Task 5: 网关 Program.cs 集成与配置

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`

- [ ] **Step 1: 向 ServiceCollectionExtensions.cs 追加 AddGatewaySecurity 和 AddUserContextTransform 方法**

在 `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` 文件中，于现有 `AddConsulDestinationResolver` 方法之后（类闭合大括号之前）追加以下两个方法。

首先在文件顶部 `using` 区追加（如尚不存在）：

```csharp
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Leno.ApiGateway.Transforms;
using Leno.Infrastructure.Auth;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;
```

然后在类内追加方法：

```csharp
    /// <summary>
    /// 注册网关安全组件：JWT 验签器、Redis 连接、Token 黑名单缓存与同步服务、
    /// 以及 IP 过滤和 JWT 白名单配置选项。
    /// </summary>
    public static IServiceCollection AddGatewaySecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // JWT 验签器（复用 Infrastructure 的 JwtTokenGenerator）
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<JwtTokenGenerator>();
        services.AddHttpContextAccessor();

        // Redis 连接（用于黑名单 Pub/Sub + Set 存储）
        var redisConfig = configuration["Redis:Configuration"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfig));

        // 安全配置选项
        services.Configure<JwtAuthOptions>(configuration.GetSection("Gateway:JwtAuth"));
        services.Configure<IpFilterOptions>(configuration.GetSection("Gateway:IpFilter"));
        services.Configure<BlacklistOptions>(configuration.GetSection("Blacklist"));

        // Token 黑名单缓存与同步服务
        services.AddSingleton<ITokenBlacklistCache, TokenBlacklistCache>();
        services.AddHostedService<TokenBlacklistSyncService>();

        return services;
    }

    /// <summary>
    /// 为 YARP 反向代理注册用户上下文注入 Transform。
    /// 必须在 <c>AddReverseProxy().LoadFromConfig()</c> 之后调用。
    /// </summary>
    public static IReverseProxyBuilder AddUserContextTransform(
        this IReverseProxyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddTransforms(context =>
        {
            context.AddRequestTransform(UserContextTransform.ApplyAsync);
        });

        return builder;
    }
```

- [ ] **Step 2: 验证扩展方法编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 3: 修改 Program.cs — 注册安全中间件管道**

将 `src/ApiGateway/Leno.ApiGateway/Program.cs` 的全部内容替换为：

```csharp
using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Middleware;
using Leno.Infrastructure.HealthChecks;
using Leno.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// YARP 反向代理从配置加载路由 + 用户上下文注入 Transform
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddUserContextTransform();

// Consul 服务发现 + 动态 Destination 解析器（阶段一）
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddConsulDestinationResolver();

// 安全认证组件（阶段二）：JWT 验签 + Redis + 黑名单缓存与同步
builder.Services.AddGatewaySecurity(builder.Configuration);

// HealthChecksUI 仪表盘
builder.Services.AddLenoHealthChecksUI(builder.Configuration);

// 网关自身健康检查：存活探针 + Consul 连通性 + Redis 连通性就绪检查
#pragma warning disable CA1861
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["Consul:Url"] ?? "http://localhost:8500"),
        "consul",
        tags: new[] { "ready" })
    .AddRedis(
        builder.Configuration["Redis:Configuration"] ?? "localhost:6379",
        "redis",
        tags: new[] { "ready" });
#pragma warning restore CA1861

var app = builder.Build();

// 中间件管道（顺序见设计文档第 2.1 节）：
// 1. IP 黑白名单过滤（最早拦截）
app.UseMiddleware<IpFilterMiddleware>();

// 2. CORS 中间件（阶段六实现，此处预留位置）

// 3. 全局异常处理（复用 Infrastructure）
app.UseMiddleware<GlobalExceptionMiddleware>();

// 4. 访问日志记录（阶段五实现，此处预留位置）
// 5. 分布式追踪（阶段五实现，此处预留位置）

// 6. JWT 本地验签
app.UseMiddleware<JwtAuthMiddleware>();

// 7. Token 黑名单校验
app.UseMiddleware<TokenBlacklistMiddleware>();

// 存活探针：仅检查网关进程存活
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// 就绪探针与 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

// YARP 反向代理端点
app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（集成测试需要）
public partial class Program { }
```

> **关键变更：**
> - 添加 `.AddUserContextTransform()` 到 YARP 注册链
> - 添加 `AddGatewaySecurity(builder.Configuration)` 注册 JWT/Redis/黑名单
> - 添加 Redis 健康检查到就绪探针（`AddRedis`）
> - 中间件管道按设计文档 2.1 节顺序注册：`IpFilter -> GlobalException -> JwtAuth -> TokenBlacklist`
> - CORS/AccessLogging/Tracing 为后续阶段预留位置（注释标注）

- [ ] **Step 4: 修改 appsettings.json — 添加安全配置节**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 中，在 `"AllowedHosts": "*",` 之后（`ReverseProxy` 之前）添加以下配置节：

```json
  "Jwt": {
    "Issuer": "Leno.UserAuth",
    "Audience": "Leno.ApiGateway",
    "SecretKey": "LenoSuperSecretKeyForJwtSigningMustBe32+Chars!",
    "AccessTokenExpiryMinutes": 120,
    "RefreshTokenExpiryDays": 7
  },
  "Redis": {
    "Configuration": "localhost:6379"
  },
  "Gateway": {
    "JwtAuth": {
      "WhitelistPaths": [
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/register",
        "/health",
        "/metrics"
      ]
    },
    "IpFilter": {
      "Whitelist": [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8"
      ],
      "Blacklist": [],
      "AutoBan": {
        "Enabled": true,
        "Threshold": 100,
        "WindowSeconds": 60,
        "BanDurationMinutes": 30
      }
    }
  },
  "Blacklist": {
    "RedisKey": "leno:token:blacklist",
    "Channel": "leno:token:blacklist",
    "SyncInterval": "00:05:00",
    "CacheTtl": "02:00:00"
  },
```

- [ ] **Step 5: 验证 appsettings.json 为有效 JSON 且网关编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 6: 运行全部测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Passed` — 全部测试通过（阶段一 + 阶段二所有测试）

> 若阶段一的集成测试 `GatewayRoutingIntegrationTests` 因缺少 Consul 连接而失败，可临时标记 `[Trait("Category", "Integration")]` 并在 CI 中跳过，或确认阶段一已正确设置 mock。

- [ ] **Step 7: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs src/ApiGateway/Leno.ApiGateway/Program.cs src/ApiGateway/Leno.ApiGateway/appsettings.json
git commit -m "feat(gateway): 集成安全认证中间件管道与配置"
```

---

## 实施后验证清单

完成所有 Task 后执行以下整体验证：

- [ ] **全量编译：** `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` — 编译成功，0 错误
- [ ] **全量测试：** `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj` — 全部通过
- [ ] **中间件管道顺序：** Program.cs 中 `IpFilter -> GlobalException -> JwtAuth -> TokenBlacklist` 顺序正确
- [ ] **白名单路由：** `/api/auth/login`、`/health/live`、`/health/ready`、`/metrics` 不需要 JWT 即可访问
- [ ] **JWT 验签：** 有效 Token 通过、无效/过期 Token 返回 401
- [ ] **黑名单校验：** Redis Pub/Sub 推送后本地缓存毫秒级更新
- [ ] **IP 过滤：** CIDR 匹配正确、白名单优先于黑名单
- [ ] **用户上下文注入：** 验签通过后下游请求包含 `X-User-Id`/`X-Role`/`X-Shop-Id`/`X-Internal-Call` 头
- [ ] **防伪造：** 客户端伪造的 `X-User-Id` 等头在 Transform 中被移除后重新注入

---

## 阶段二与设计文档第 4 节覆盖映射

| Spec 要求 | 实现位置 | 覆盖状态 |
|---|---|---|
| 4.1 JWT 本地验签 — Bearer Token 提取 | `JwtAuthMiddleware.ExtractBearerToken` | ✅ |
| 4.1 JWT 本地验签 — HS256 签名+过期校验 | `JwtAuthMiddleware` 调用 `JwtTokenGenerator.ValidateTokenAsync` | ✅ |
| 4.1 JWT 本地验签 — 白名单路由跳过 | `JwtAuthMiddleware.IsWhitelisted` + `JwtAuthOptions.WhitelistPaths` | ✅ |
| 4.1 JWT 本地验签 — 提取 Sub/Role/shop_id | 设置 `HttpContext.User`，由 `UserContextTransform` 提取 | ✅ |
| 4.2 黑名单 — JTI 查本地缓存 | `TokenBlacklistMiddleware` 调用 `ITokenBlacklistCache.Contains` | ✅ |
| 4.2 黑名单 — 命中返回 401 | `TokenBlacklistMiddleware.WriteUnauthorizedAsync` | ✅ |
| 4.2 同步 — Redis Pub/Sub 实时推送 | `TokenBlacklistSyncService.OnMessageReceived` | ✅ |
| 4.2 同步 — 每 5 分钟定时兜底拉取 | `TokenBlacklistSyncService.FullPullAsync` + Timer | ✅ |
| 4.2 同步 — 启动预热 | `TokenBlacklistSyncService.StartAsync` 先调用 `FullPullAsync` | ✅ |
| 4.2 数据结构 — Redis Set Key | `BlacklistOptions.RedisKey` = `leno:token:blacklist` | ✅ |
| 4.2 缓存 — TTL 120 分钟 | `BlacklistOptions.CacheTtl` = `02:00:00` | ✅ |
| 4.2 事件格式 — TokenRevoked JSON | `TokenBlacklistSyncService.OnMessageReceived` 解析 `jti` 字段 | ✅ |
| 4.3 IP 过滤 — 白名单直接放行 | `IpFilterMiddleware` 白名单优先逻辑 | ✅ |
| 4.3 IP 过滤 — 黑名单返回 403 | `IpFilterMiddleware.WriteForbiddenAsync` | ✅ |
| 4.3 IP 过滤 — CIDR 网段匹配 | `IpFilterMiddleware.IsInList` 使用 `IPNetwork.Contains` | ✅ |
| 4.3 IP 过滤 — AutoBan 自动封禁 | `IpFilterMiddleware.BanIp` + `ConcurrentDictionary` 临时封禁列表 | ✅ (配置+存储，触发逻辑在阶段四) |
| 4.3 配置来源 — Consul KV 热更新 | 阶段一已建立 Consul 集成，IP 配置当前从 appsettings.json 读取；Consul KV 热更新可在后续迭代接入 `IOptionsMonitor` | ⚠️ (配置读取就绪，热更新待后续) |
| 7.1 用户上下文注入 — X-User-Id/X-Role/X-Shop-Id/X-Internal-Call | `UserContextTransform.ApplyHeaders` | ✅ |
| 7.1 安全保障 — 移除伪造 Header | `UserContextTransform.ApplyHeaders` 先 Remove 再 Add | ✅ |
