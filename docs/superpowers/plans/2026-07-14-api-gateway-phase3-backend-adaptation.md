# API 网关增强 - 阶段三：后端适配 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 11 个 Leno 微服务从"自行验签 JWT"改造为"信任网关注入的 X-User-Id/X-Role/X-Shop-Id 请求头"，创建 GatewayHeader 认证方案和适配后的 CurrentUserContext，保留授权与内部服务间鉴权不变。

**Architecture:** 在 `Leno.Infrastructure.Auth` 中新增 `GatewayAuthOptions`（头部名称常量 + TrustedProxies 配置）和 `GatewayAuthHandler`（从请求头构造 `ClaimsPrincipal`，使 `[Authorize]` 特性正常工作）。改造 `CurrentUserContext` 直接从请求头提取 UserId/Role/ShopId，不再依赖 `JwtTokenGenerator` 静态方法解析 JWT Claims。新增 `AddGatewayAuth()` 扩展方法注册 `Authentication` 方案为 `"GatewayHeader"`，在各微服务 `Program.cs` 中替换原有 `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` 块，保留 `AddLenoInfrastructure()`、`AddAuthorization()`、`AddInternalApiKeyAuth()` 不变。`GatewayAuthHandler` 可选校验 `TrustedProxies` 防止请求绕过网关直连后端。

**Tech Stack:** .NET 10, ASP.NET Core Authentication (`AuthenticationHandler<TOptions>`), xUnit 2.9.0, FluentAssertions 7.0.0, Moq 4.20.72, Microsoft.AspNetCore.TestHost 10.0.0

**Spec:** [docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md](../specs/2026-07-14-api-gateway-enhancement-design.md) 第 4.4 节（后端服务适配改造）+ 第 4.5 节（改造影响范围）

---

## Phase 2 依赖

> 本计划假设阶段二（`2026-07-14-api-gateway-phase2-security.md`）已完成：
> - 网关 `JwtAuthMiddleware` 已实现本地 JWT 集中验签
> - `UserContextTransform` 已在 YARP 管道中注入 `X-User-Id`/`X-Role`/`X-Shop-Id` 请求头到下游请求
> - 网关 `TokenBlacklistMiddleware` 已实现 JTI 黑名单校验
>
> 若阶段二未完成，需先执行阶段二计划。后端服务在网关未启用集中验签时仍可独立工作（GatewayHeader 认证方案本身不依赖网关代码，只要请求中携带正确的头部即可）。

---

## 实施说明

> 以下三点与 Spec 字面描述不同但实现等价或有合理收敛：

1. **CurrentUserContext 直接读 Header 而非读 ClaimsPrincipal**：Spec 4.4 示例注释为"CurrentUserContext 从 X-User-Id/X-Role/X-Shop-Id 头读取"。本计划选择直接从 `HttpContext.Request.Headers` 提取，而非通过 `ClaimsPrincipal` 间接读取。原因：`GatewayAuthHandler` 和 `CurrentUserContext` 若都解析头部会产生重复逻辑；但 `GatewayAuthHandler` 的职责是为 `[Authorize]` 管道构建 `ClaimsPrincipal`（设置 `IsAuthenticated=true`），`CurrentUserContext` 的职责是为应用层提供用户上下文——两者关注点不同。直接读 Header 使 `CurrentUserContext` 与认证方案解耦，即使 `GatewayAuthHandler` 未被调用（如测试场景），只要请求携带头部即可工作。

2. **Jwt 配置节保留不动**：各服务 `appsettings.json` 中的 `Jwt` 配置节不移除。原因：`AddLenoInfrastructure()` 内部注册 `JwtTokenGenerator` 单例，其构造函数需要 `IOptions<JwtOptions>`；UserAuth 服务仍需 `JwtTokenGenerator.GenerateAccessToken()` 为登录用户签发 JWT（网关验签的 Token 仍由 UserAuth 生成）。其他服务虽不直接调用 `GenerateAccessToken`，但 `JwtTokenGenerator` 仍被注册，保留配置避免实例化异常。

3. **TrustedProxies 可选配置**：Spec 4.4 提到"可配置 TrustedProxy 校验请求来源"。本计划在 `GatewayAuthOptions` 中实现 `TrustedProxies` 数组，为空时跳过校验（开发环境），生产环境通过 `appsettings.json` 的 `GatewayAuth:TrustedProxies` 配置网关 IP。这避免了在开发环境需要配置网关 IP 的负担，同时生产环境可启用安全边界。

---

## 文件结构

### 新建文件

| 文件 | 职责 |
|---|---|
| `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs` | GatewayHeader 认证方案配置：头部名称常量 + TrustedProxies |
| `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs` | `AuthenticationHandler<GatewayAuthOptions>` 实现：从请求头构造 ClaimsPrincipal |
| `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs` | GatewayAuthHandler 单元测试 |
| `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/CurrentUserContextTests.cs` | 改造后 CurrentUserContext 单元测试 |
| `src/BuildingBlocks/Leno.Infrastructure.Tests/Dependencies/GatewayAuthExtensionsTests.cs` | AddGatewayAuth 扩展方法注册测试 |
| `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayHeaderIntegrationTests.cs` | 端到端集成测试：Header 注入到 UserContext 读取完整链路 |

### 修改文件

| 文件 | 修改内容 |
|---|---|
| `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs` | 从 `JwtTokenGenerator.GetUserId/GetRole/GetShopId`（ClaimsPrincipal）改为直接读 `X-User-Id`/`X-Role`/`X-Shop-Id` 请求头 |
| `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | 追加 `AddGatewayAuth()` 扩展方法：注册 GatewayHeader 认证方案 + 绑定 GatewayAuthOptions |
| `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs` | 移除 JwtBearer 验签块，替换为 `AddGatewayAuth()` |
| `src/Services/Order/Leno.Order.Api/Program.cs` | 同上 |
| `src/Services/Product/Leno.Product.Api/Program.cs` | 同上 |
| `src/Services/Cart/Leno.Cart.Api/Program.cs` | 同上 |
| `src/Services/Promotion/Leno.Promotion.Api/Program.cs` | 同上 |
| `src/Services/Payment/Leno.Payment.Api/Program.cs` | 同上 |
| `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs` | 同上 |
| `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs` | 同上 |
| `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs` | 同上 |
| `src/Services/Notification/Leno.Notification.Api/Program.cs` | 同上 |
| `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs` | 同上 |

---

## Task 1: 创建 GatewayHeader 认证方案

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs`

- [ ] **Step 1: 创建 GatewayAuthOptions.cs**

创建 `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs`：

```csharp
using Microsoft.AspNetCore.Authentication;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// GatewayHeader 认证方案配置选项。
/// <para>
/// 后端服务信任网关注入的 <c>X-User-Id</c>/<c>X-Role</c>/<c>X-Shop-Id</c> 头，
/// 不再自行验签 JWT。网关在阶段二完成 JWT 集中验签后通过 YARP Transform 注入这些头。
/// </para>
/// </summary>
public sealed class GatewayAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>用户 ID 请求头名称。</summary>
    public const string UserIdHeader = "X-User-Id";

    /// <summary>用户角色请求头名称。</summary>
    public const string RoleHeader = "X-Role";

    /// <summary>店铺 ID 请求头名称（卖家场景）。</summary>
    public const string ShopIdHeader = "X-Shop-Id";

    /// <summary>
    /// 可信代理 IP 列表（网关 IP）。
    /// <para>
    /// 为空时跳过来源校验（仅开发环境使用）。
    /// 生产环境应配置网关 IP 以防止请求绕过网关直连后端。
    /// </para>
    /// </summary>
    public string[] TrustedProxies { get; set; } = Array.Empty<string>();
}
```

- [ ] **Step 2: 创建 GatewayAuthHandler.cs**

创建 `src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs`：

```csharp
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// 从网关注入的请求头提取用户身份的认证处理器。
/// <para>
/// 网关在阶段二完成 JWT 集中验签后，通过 YARP Transform 注入
/// <c>X-User-Id</c>/<c>X-Role</c>/<c>X-Shop-Id</c> 头到下游请求。
/// 后端服务使用此处理器从头部构造 <see cref="ClaimsPrincipal"/>，
/// 替代原先的 JwtBearer 验签，使 <c>[Authorize]</c> 特性正常工作。
/// </para>
/// </summary>
public sealed class GatewayAuthHandler : AuthenticationHandler<GatewayAuthOptions>
{
    public GatewayAuthHandler(
        IOptionsMonitor<GatewayAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 可信代理校验：若配置了 TrustedProxies，请求必须来自其中之一
        if (Options.TrustedProxies.Length > 0)
        {
            var remoteIp = Context.Connection.RemoteIpAddress;
            if (remoteIp is null || !IsTrustedProxy(remoteIp, Options.TrustedProxies))
            {
                return Task.FromResult(AuthenticateResult.Fail("Request not from a trusted proxy."));
            }
        }

        // 从请求头提取用户 ID（必填）
        if (!Request.Headers.TryGetValue(GatewayAuthOptions.UserIdHeader, out var userIdValues)
            || !Guid.TryParse(userIdValues.ToString(), out var userId) || userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userId.ToString())
        };

        // 角色头（可选，支持多值）
        if (Request.Headers.TryGetValue(GatewayAuthOptions.RoleHeader, out var roleValues))
        {
            foreach (var role in roleValues)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                    claims.Add(new Claim("role", role));
                }
            }
        }

        // 店铺 ID 头（可选，卖家场景）
        if (Request.Headers.TryGetValue(GatewayAuthOptions.ShopIdHeader, out var shopIdValues)
            && Guid.TryParse(shopIdValues.ToString(), out var shopId) && shopId != Guid.Empty)
        {
            claims.Add(new Claim(JwtTokenGenerator.ShopIdClaimType, shopId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool IsTrustedProxy(IPAddress remoteIp, string[] trustedProxies)
    {
        foreach (var proxy in trustedProxies)
        {
            if (IPAddress.TryParse(proxy, out var proxyIp) && remoteIp.Equals(proxyIp))
            {
                return true;
            }
        }
        return false;
    }
}
```

- [ ] **Step 3: 编写 GatewayAuthHandler 失败测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs`：

```csharp
using System.Net;
using System.Security.Claims;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Infrastructure.Tests.Auth;

public class GatewayAuthHandlerTests
{
    private static async Task<AuthenticateResult> RunHandlerAsync(
        Action<HttpContext>? setup = null,
        GatewayAuthOptions? options = null)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        setup?.Invoke(context);

        var optionsMonitorMock = new Mock<IOptionsMonitor<GatewayAuthOptions>>();
        optionsMonitorMock.SetupGet(o => o.CurrentValue)
            .Returns(options ?? new GatewayAuthOptions());

        var handler = new GatewayAuthHandler(
            optionsMonitorMock.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        var scheme = new AuthenticationScheme(
            "GatewayHeader", "GatewayHeader", typeof(GatewayAuthHandler));

        await handler.InitializeAsync(scheme, context);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidUserIdAndRole_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
            context.Request.Headers[GatewayAuthOptions.RoleHeader] = "Admin";
        });

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value
            .Should().Be(userId.ToString());
        result.Principal!.FindFirst(ClaimTypes.Role)!.Value
            .Should().Be("Admin");
        result.Principal!.FindFirst("role")!.Value
            .Should().Be("Admin");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidUserIdRoleAndShopId_ReturnsSuccessWithShopIdClaim()
    {
        var userId = Guid.NewGuid();
        var shopId = Guid.NewGuid();

        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
            context.Request.Headers[GatewayAuthOptions.RoleHeader] = "Seller";
            context.Request.Headers[GatewayAuthOptions.ShopIdHeader] = shopId.ToString();
        });

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(JwtTokenGenerator.ShopIdClaimType)!.Value
            .Should().Be(shopId.ToString());
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutUserIdHeader_ReturnsNoResult()
    {
        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.RoleHeader] = "Admin";
        });

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidGuidUserId_ReturnsNoResult()
    {
        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = "not-a-guid";
        });

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyGuidUserId_ReturnsNoResult()
    {
        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = Guid.Empty.ToString();
        });

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithUserIdOnly_NoRoleClaim()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
        });

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(ClaimTypes.Role).Should().BeNull();
        result.Principal!.FindFirst(JwtTokenGenerator.ShopIdClaimType).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutShopIdHeader_NoShopIdClaim()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
            context.Request.Headers[GatewayAuthOptions.RoleHeader] = "Customer";
        });

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(JwtTokenGenerator.ShopIdClaimType).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidShopId_NoShopIdClaim()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
            context.Request.Headers[GatewayAuthOptions.ShopIdHeader] = "invalid-shop-id";
        });

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(JwtTokenGenerator.ShopIdClaimType).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithTrustedProxiesAndValidIp_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(
            context =>
            {
                context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
                context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
            },
            new GatewayAuthOptions { TrustedProxies = new[] { "10.0.0.1", "10.0.0.2" } });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithTrustedProxiesAndInvalidIp_ReturnsFail()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(
            context =>
            {
                context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.50");
            },
            new GatewayAuthOptions { TrustedProxies = new[] { "10.0.0.1" } });

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithTrustedProxiesAndNoRemoteIp_ReturnsFail()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(
            context =>
            {
                context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
                context.Connection.RemoteIpAddress = null;
            },
            new GatewayAuthOptions { TrustedProxies = new[] { "10.0.0.1" } });

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyTrustedProxies_SkipsProxyCheck()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(
            context =>
            {
                context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.50");
            },
            new GatewayAuthOptions { TrustedProxies = Array.Empty<string>() });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithMultipleRoles_AddsAllRoleClaims()
    {
        var userId = Guid.NewGuid();

        var result = await RunHandlerAsync(context =>
        {
            context.Request.Headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
            context.Request.Headers[GatewayAuthOptions.RoleHeader] = "Admin,Operator";
        });

        result.Succeeded.Should().BeTrue();
        var roleClaims = result.Principal!.FindAll(ClaimTypes.Role).ToList();
        roleClaims.Should().HaveCount(1);
        roleClaims.First().Value.Should().Be("Admin,Operator");
    }
}
```

> 注意：HTTP Header 值 `"Admin,Operator"` 作为一个整体字符串传入 `ClaimTypes.Role`。如果网关需要拆分多角色为多个 Header 值，YARP Transform 层负责拆分。此测试验证单值场景的行为。

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "GatewayAuthHandlerTests"`
Expected: 编译失败 — `GatewayAuthOptions` 和 `GatewayAuthHandler` 类型未定义（Step 1 和 Step 2 的文件已创建，此步应在创建文件前运行测试以验证 TDD 流程；若已按顺序创建文件则直接跳到 Step 5）

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "GatewayAuthHandlerTests"`
Expected: `Passed: 12` — 12 个测试全部通过

- [ ] **Step 6: 验证 Infrastructure 项目编译**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
Expected: `Build succeeded`

- [ ] **Step 7: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthOptions.cs src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayAuthHandlerTests.cs
git commit -m "feat(auth): 添加 GatewayHeader 认证方案从请求头构造 ClaimsPrincipal"
```

---

## Task 2: 改造 CurrentUserContext 从 Header 提取

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/CurrentUserContextTests.cs`

- [ ] **Step 1: 编写 CurrentUserContext 失败测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/CurrentUserContextTests.cs`：

```csharp
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace Leno.Infrastructure.Tests.Auth;

public class CurrentUserContextTests
{
    private static CurrentUserContext CreateContext(
        Action<IHeaderDictionary>? setupHeaders = null)
    {
        var httpContextAccessor = new HttpContextAccessor();
        var context = new DefaultHttpContext();
        setupHeaders?.Invoke(context.Request.Headers);
        httpContextAccessor.HttpContext = context;
        return new CurrentUserContext(httpContextAccessor);
    }

    [Fact]
    public void UserId_WithValidGuidHeader_ReturnsParsedGuid()
    {
        var userId = Guid.NewGuid();

        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.UserIdHeader] = userId.ToString();
        });

        sut.UserId.Should().Be(userId);
    }

    [Fact]
    public void UserId_WithoutHeader_ReturnsNull()
    {
        var sut = CreateContext();
        sut.UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_WithInvalidGuid_ReturnsNull()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.UserIdHeader] = "not-a-guid";
        });

        sut.UserId.Should().BeNull();
    }

    [Fact]
    public void Role_WithHeader_ReturnsRoleString()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.RoleHeader] = "Seller";
        });

        sut.Role.Should().Be("Seller");
    }

    [Fact]
    public void Role_WithoutHeader_ReturnsNull()
    {
        var sut = CreateContext();
        sut.Role.Should().BeNull();
    }

    [Fact]
    public void Role_WithEmptyString_ReturnsNull()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.RoleHeader] = "";
        });

        sut.Role.Should().BeNull();
    }

    [Fact]
    public void ShopId_WithValidGuidHeader_ReturnsParsedGuid()
    {
        var shopId = Guid.NewGuid();

        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.ShopIdHeader] = shopId.ToString();
        });

        sut.ShopId.Should().Be(shopId);
    }

    [Fact]
    public void ShopId_WithoutHeader_ReturnsNull()
    {
        var sut = CreateContext();
        sut.ShopId.Should().BeNull();
    }

    [Fact]
    public void ShopId_WithInvalidGuid_ReturnsNull()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.ShopIdHeader] = "invalid";
        });

        sut.ShopId.Should().BeNull();
    }

    [Fact]
    public void IsAuthenticated_WithValidUserIdHeader_ReturnsTrue()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.UserIdHeader] = Guid.NewGuid().ToString();
        });

        sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_WithoutUserIdHeader_ReturnsFalse()
    {
        var sut = CreateContext();
        sut.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_WithInvalidGuidUserId_ReturnsFalse()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.UserIdHeader] = "not-a-guid";
        });

        sut.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_WithAllHeaders_ReturnsTrue()
    {
        var sut = CreateContext(headers =>
        {
            headers[GatewayAuthOptions.UserIdHeader] = Guid.NewGuid().ToString();
            headers[GatewayAuthOptions.RoleHeader] = "Admin";
            headers[GatewayAuthOptions.ShopIdHeader] = Guid.NewGuid().ToString();
        });

        sut.IsAuthenticated.Should().BeTrue();
        sut.UserId.Should().NotBeNull();
        sut.Role.Should().Be("Admin");
        sut.ShopId.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpContextAccessor_Throws()
    {
        var act = () => new CurrentUserContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AllProperties_WithNullHttpContext_ReturnDefaults()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var sut = new CurrentUserContext(accessor);

        sut.IsAuthenticated.Should().BeFalse();
        sut.UserId.Should().BeNull();
        sut.Role.Should().BeNull();
        sut.ShopId.Should().BeNull();
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "CurrentUserContextTests"`
Expected: 失败 — `CurrentUserContext` 仍从 `ClaimsPrincipal` 读取，当未设置 `HttpContext.User` 时所有属性返回 null/false

- [ ] **Step 3: 改造 CurrentUserContext.cs 从 Header 提取**

将 `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs` 的全部内容替换为：

```csharp
using Microsoft.AspNetCore.Http;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// 当前用户上下文抽象，从网关注入的请求头提取用户信息。
/// 供应用层与基础设施层获取当前操作者。
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }

    string? Role { get; }

    Guid? ShopId { get; }

    bool IsAuthenticated { get; }
}

/// <summary>
/// 基于 <see cref="IHttpContextAccessor"/> 的当前用户上下文实现。
/// 从 <c>X-User-Id</c>/<c>X-Role</c>/<c>X-Shop-Id</c> 请求头提取用户信息，
/// 替代原先从 JWT Claims 解析。
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    private IHeaderDictionary? Headers => _httpContextAccessor.HttpContext?.Request.Headers;

    public bool IsAuthenticated =>
        Headers is not null
        && Headers.TryGetValue(GatewayAuthOptions.UserIdHeader, out var value)
        && Guid.TryParse(value.ToString(), out _);

    public Guid? UserId => TryParseGuidHeader(GatewayAuthOptions.UserIdHeader);

    public string? Role
    {
        get
        {
            var value = Headers?[GatewayAuthOptions.RoleHeader].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public Guid? ShopId => TryParseGuidHeader(GatewayAuthOptions.ShopIdHeader);

    private Guid? TryParseGuidHeader(string headerName)
    {
        var value = Headers?[headerName].ToString();
        return value is not null && Guid.TryParse(value, out var id) ? id : null;
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "CurrentUserContextTests"`
Expected: `Passed: 14` — 14 个测试全部通过

- [ ] **Step 5: 验证现有 Infrastructure 测试未回归**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/`
Expected: 所有测试通过（包含 Task 1 的 GatewayAuthHandlerTests 12 个 + Task 2 的 CurrentUserContextTests 14 个 + 既有测试）

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/CurrentUserContextTests.cs
git commit -m "refactor(auth): CurrentUserContext 从请求头提取用户上下文替代 JWT Claims"
```

---

## Task 3: 注册 AddGatewayAuth 扩展方法

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Dependencies/GatewayAuthExtensionsTests.cs`

- [ ] **Step 1: 编写 AddGatewayAuth 注册测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Dependencies/GatewayAuthExtensionsTests.cs`：

```csharp
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Tests.Dependencies;

public class GatewayAuthExtensionsTests
{
    private static IConfiguration CreateConfig(params (string Key, string Value)[] pairs)
    {
        var dict = pairs.ToDictionary(p => p.Key, p => (string?)p.Value);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    [Fact]
    public void AddGatewayAuth_RegistersGatewayHeaderAsDefaultScheme()
    {
        var services = new ServiceCollection();
        var config = CreateConfig();

        services.AddGatewayAuth(config);
        var sp = services.BuildServiceProvider();
        var authOptions = sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        authOptions.DefaultScheme.Should().Be("GatewayHeader");
        authOptions.DefaultAuthenticateScheme.Should().Be("GatewayHeader");
    }

    [Fact]
    public void AddGatewayAuth_RegistersGatewayAuthHandlerForScheme()
    {
        var services = new ServiceCollection();
        var config = CreateConfig();

        services.AddGatewayAuth(config);
        var sp = services.BuildServiceProvider();
        var schemeProvider = sp.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = schemeProvider.GetAllSchemesAsync().Result.ToList();

        schemes.Should().Contain(s => s.Name == "GatewayHeader"
            && s.HandlerType == typeof(GatewayAuthHandler));
    }

    [Fact]
    public void AddGatewayAuth_BindsTrustedProxiesFromConfig()
    {
        var services = new ServiceCollection();
        var config = CreateConfig(
            ("GatewayAuth:TrustedProxies:0", "10.0.0.1"),
            ("GatewayAuth:TrustedProxies:1", "10.0.0.2"));

        services.AddGatewayAuth(config);
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<GatewayAuthOptions>>().Value;

        options.TrustedProxies.Should().Equal("10.0.0.1", "10.0.0.2");
    }

    [Fact]
    public void AddGatewayAuth_WithEmptyConfig_UsesDefaultEmptyTrustedProxies()
    {
        var services = new ServiceCollection();
        var config = CreateConfig();

        services.AddGatewayAuth(config);
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<GatewayAuthOptions>>().Value;

        options.TrustedProxies.Should().BeEmpty();
    }

    [Fact]
    public void AddGatewayAuth_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = CreateConfig();

        var act = () => services.AddGatewayAuth(config);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayAuth_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddGatewayAuth(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayAuth_ReturnsServicesForChaining()
    {
        var services = new ServiceCollection();
        var config = CreateConfig();

        var result = services.AddGatewayAuth(config);

        result.Should().BeSameAs(services);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "GatewayAuthExtensionsTests"`
Expected: 编译失败 — `AddGatewayAuth` 方法未定义

- [ ] **Step 3: 向 ServiceCollectionExtensions.cs 追加 AddGatewayAuth 方法**

在 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 中：

**3a: 添加 using 语句**

在文件顶部 `using` 区（`using Leno.Infrastructure.Auth;` 之后）添加：

```csharp
using Microsoft.AspNetCore.Authentication;
```

**3b: 添加 AddGatewayAuth 方法**

在 `AddInternalApiKeyAuth` 方法之后、`AddOptions` 私有方法之前添加：

```csharp
    /// <summary>
    /// 注册 GatewayHeader 认证方案。
    /// <para>
    /// 从 <c>GatewayAuth</c> 配置节绑定 <see cref="GatewayAuthOptions"/>（含 TrustedProxies）。
    /// 注册默认认证方案为 <c>GatewayHeader</c>，使用 <see cref="GatewayAuthHandler"/>
    /// 从网关注入的 <c>X-User-Id</c>/<c>X-Role</c>/<c>X-Shop-Id</c> 头构造 <see cref="System.Security.Claims.ClaimsPrincipal"/>。
    /// </para>
    /// 调用方仍需单独调用 <c>AddAuthorization()</c> 注册授权服务。
    /// </summary>
    public static IServiceCollection AddGatewayAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<GatewayAuthOptions>(configuration.GetSection("GatewayAuth"));

        services.AddAuthentication("GatewayHeader")
            .AddScheme<GatewayAuthOptions, GatewayAuthHandler>("GatewayHeader", options => { });

        return services;
    }
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "GatewayAuthExtensionsTests"`
Expected: `Passed: 7` — 7 个测试全部通过

- [ ] **Step 5: 验证 Infrastructure 项目编译**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
Expected: `Build succeeded`

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Dependencies/GatewayAuthExtensionsTests.cs
git commit -m "feat(auth): 添加 AddGatewayAuth 扩展方法注册 GatewayHeader 认证方案"
```

---

## Task 4: 改造 3 个代表性微服务 (UserAuth/Order/Product)

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs`
- Modify: `src/Services/Order/Leno.Order.Api/Program.cs`
- Modify: `src/Services/Product/Leno.Product.Api/Program.cs`

> **改造模式说明**（适用于所有微服务）：
> 1. 移除 using: `System.Text`、`Microsoft.AspNetCore.Authentication.JwtBearer`、`Microsoft.IdentityModel.Tokens`、`Leno.Infrastructure.Auth`
> 2. 移除代码: `var jwtOptions = ...` 行 + `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` 块
> 3. 添加代码: `builder.Services.AddGatewayAuth(builder.Configuration);`（在 `AddAuthorization()` 之前）
> 4. 保留: `AddLenoInfrastructure()`、`AddInternalApiKeyAuth()`、`AddAuthorization()`、所有中间件和端点映射

- [ ] **Step 1: 改造 UserAuth.Api/Program.cs**

将 `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.UserAuth.Infrastructure;
using Leno.UserAuth.Infrastructure.Audit;
using Leno.UserAuth.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration);
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 用户与认证授权域基础设施：DbContext、工作单元、仓储、领域服务实现、审计拦截器、FluentValidation 校验器
builder.Services.AddUserAuthInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UserAuthDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<AuditLogMiddleware>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 2: 改造 Order.Api/Program.cs**

将 `src/Services/Order/Leno.Order.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含订单域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddOrderConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 订单域基础设施：DbContext、工作单元、仓储、Redis 库存、防腐层、应用服务、FluentValidation 校验器
builder.Services.AddOrderInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 3: 改造 Product.Api/Program.cs**

将 `src/Services/Product/Leno.Product.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.Product.Infrastructure;
using Leno.Product.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含商品域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddProductConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 商品域基础设施：DbContext、工作单元、仓储、防腐层、ES 搜索、应用服务、FluentValidation 校验器
builder.Services.AddProductInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProductDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 4: 验证 3 个服务编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Api/Leno.UserAuth.Api.csproj src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj src/Services/Product/Leno.Product.Api/Leno.Product.Api.csproj`
Expected: `Build succeeded`（3 个项目均编译成功）

- [ ] **Step 5: 验证现有服务测试未回归**

Run: `dotnet test src/Services/UserAuth/Leno.UserAuth.Api.Tests/ src/Services/Order/Leno.Order.Api.Tests/ src/Services/Product/Leno.Product.Api.Tests/`
Expected: 全部通过

> **说明**：现有服务测试使用 `TestAuthHandler`（注册为 `"Test"` 方案）覆盖默认认证方案，并 Mock `ICurrentUserContext`，因此认证方案的变更不影响现有测试。

- [ ] **Step 6: 提交**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Api/Program.cs src/Services/Order/Leno.Order.Api/Program.cs src/Services/Product/Leno.Product.Api/Program.cs
git commit -m "refactor(auth): UserAuth/Order/Product 服务改用 GatewayHeader 认证替代 JwtBearer"
```

---

## Task 5: 改造剩余 8 个微服务

**Files:**
- Modify: `src/Services/Cart/Leno.Cart.Api/Program.cs`
- Modify: `src/Services/Promotion/Leno.Promotion.Api/Program.cs`
- Modify: `src/Services/Payment/Leno.Payment.Api/Program.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs`
- Modify: `src/Services/Notification/Leno.Notification.Api/Program.cs`
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs`

> 每个服务的改造模式与 Task 4 相同：移除 4 个 using + JWT 验签块，添加 `AddGatewayAuth()`。

- [ ] **Step 1: 改造 Cart.Api/Program.cs**

将 `src/Services/Cart/Leno.Cart.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Dependencies;
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含购物车域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddCartConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 购物车域基础设施：DbContext、工作单元、仓储、Redis 缓存、防腐层、应用服务、FluentValidation 校验器
builder.Services.AddCartInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CartDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 2: 改造 Promotion.Api/Program.cs**

将 `src/Services/Promotion/Leno.Promotion.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.Promotion.Infrastructure;
using Leno.Promotion.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含促销域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddPromotionConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 促销域基础设施：DbContext、工作单元、仓储、Redis 秒杀库存、防腐层、应用服务、FluentValidation 校验器
builder.Services.AddPromotionInfrastructure(builder.Configuration);

// 后台服务：优惠券过期处理
builder.Services.AddHostedService<Leno.Promotion.Api.BackgroundServices.CouponExpiryService>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PromotionDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 3: 改造 Payment.Api/Program.cs**

将 `src/Services/Payment/Leno.Payment.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.Payment.Infrastructure;
using Leno.Payment.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含支付域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddPaymentConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 支付域基础设施：DbContext、工作单元、仓储、渠道配置、渠道适配器、通知处理器、补偿任务、应用服务
builder.Services.AddPaymentInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 4: 改造 PointsMembership.Api/Program.cs**

将 `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.PointsMembership.Infrastructure;
using Leno.PointsMembership.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含积分会员域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddPointsMembershipConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 积分会员域基础设施：DbContext、工作单元、仓储、积分抵扣防腐层、应用服务、FluentValidation 校验器
builder.Services.AddPointsMembershipInfrastructure(builder.Configuration);

// 后台服务：会员成长值等级评估 + 积分过期处理
builder.Services.AddHostedService<Leno.PointsMembership.Api.BackgroundServices.MemberLevelEvaluationJob>();
builder.Services.AddHostedService<Leno.PointsMembership.Api.BackgroundServices.PointsExpiryService>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PointsMembershipDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 5: 改造 ReviewAfterSales.Api/Program.cs**

将 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.ReviewAfterSales.Infrastructure;
using Leno.ReviewAfterSales.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含评价与售后域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddReviewAfterSalesConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 评价与售后域基础设施：DbContext、工作单元、仓储、防腐层、应用服务
builder.Services.AddReviewAfterSalesInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ReviewAfterSalesDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 6: 改造 SellerShop.Api/Program.cs**

将 `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.SellerShop.Infrastructure;
using Leno.SellerShop.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT 生成器、当前用户上下文、事件总线（含卖家域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddSellerShopConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 卖家与店铺管理域基础设施：DbContext、工作单元、仓储、防腐层、应用服务、FluentValidation 校验器
builder.Services.AddSellerShopInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SellerShopDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 7: 改造 Notification.Api/Program.cs**

将 `src/Services/Notification/Leno.Notification.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.Notification.Infrastructure;
using Leno.Notification.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT、当前用户上下文、事件总线（含通知域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddNotificationConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 通知域基础设施：DbContext、工作单元、仓储、模板渲染、渠道、调度器、任务、应用服务
builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 8: 改造 SystemAdmin.Api/Program.cs**

将 `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs` 的全部内容替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.SystemAdmin.Infrastructure;
using Leno.SystemAdmin.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 共享内核基础设施：JWT、当前用户上下文、事件总线（含系统管理域消费者）、Redis、ES、健康检查
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg.AddSystemAdminConsumers());
builder.Services.AddInternalApiKeyAuth(builder.Configuration);

// 系统管理域基础设施：DbContext、工作单元、仓储、缓存、Quartz 调度器、特性开关评估器
builder.Services.AddSystemAdminInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SystemAdminDbContext>(tags: ["ready"]);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// GatewayHeader 认证：信任网关注入的 X-User-Id/X-Role/X-Shop-Id 头
builder.Services.AddGatewayAuth(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
```

- [ ] **Step 9: 验证全部 8 个服务编译**

Run: `dotnet build src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj src/Services/Promotion/Leno.Promotion.Api/Leno.Promotion.Api.csproj src/Services/Payment/Leno.Payment.Api/Leno.Payment.Api.csproj src/Services/PointsMembership/Leno.PointsMembership.Api/Leno.PointsMembership.Api.csproj src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Leno.ReviewAfterSales.Api.csproj src/Services/SellerShop/Leno.SellerShop.Api/Leno.SellerShop.Api.csproj src/Services/Notification/Leno.Notification.Api/Leno.Notification.Api.csproj src/Services/SystemAdmin/Leno.SystemAdmin.Api/Leno.SystemAdmin.Api.csproj`
Expected: `Build succeeded`（8 个项目均编译成功）

- [ ] **Step 10: 验证现有服务测试未回归**

Run: `dotnet test src/Services/Cart/Leno.Cart.Api.Tests/ src/Services/Promotion/Leno.Promotion.Api.Tests/ src/Services/Payment/Leno.Payment.Api.Tests/ src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/ src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api.Tests/ src/Services/SellerShop/Leno.SellerShop.Api.Tests/ src/Services/Notification/Leno.Notification.Api.Tests/ src/Services/SystemAdmin/Leno.SystemAdmin.Api.Tests/`
Expected: 全部通过

- [ ] **Step 11: 提交**

```bash
git add src/Services/Cart/Leno.Cart.Api/Program.cs src/Services/Promotion/Leno.Promotion.Api/Program.cs src/Services/Payment/Leno.Payment.Api/Program.cs src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs src/Services/SellerShop/Leno.SellerShop.Api/Program.cs src/Services/Notification/Leno.Notification.Api/Program.cs src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs
git commit -m "refactor(auth): 剩余 8 个微服务改用 GatewayHeader 认证替代 JwtBearer"
```

---

## Task 6: 端到端集成测试

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayHeaderIntegrationTests.cs`

> 本任务通过 TestServer 搭建一个使用 `AddGatewayAuth` + `CurrentUserContext` 的最小管道，模拟网关注入请求头到后端服务读取用户上下文的完整链路。

- [ ] **Step 1: 编写端到端集成测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayHeaderIntegrationTests.cs`：

```csharp
using System.Net;
using System.Net.Http.Json;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Dependencies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Leno.Infrastructure.Tests.Auth;

public class GatewayHeaderIntegrationTests : IAsyncLifetime
{
    private TestServer _server = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GatewayAuth:TrustedProxies:0"] = ""
                });
            });
            webBuilder.ConfigureServices(services =>
            {
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUserContext, CurrentUserContext>();

                var config = new ConfigurationBuilder().Build();
                services.AddGatewayAuth(config);
                services.AddAuthorization();
                services.AddRouting();
            });
            webBuilder.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapGet("/test/user-context", (ICurrentUserContext userCtx) =>
                    Results.Ok(new
                    {
                        userCtx.IsAuthenticated,
                        UserId = userCtx.UserId?.ToString(),
                        userCtx.Role,
                        ShopId = userCtx.ShopId?.ToString()
                    }));

                app.MapGet("/test/authorized", () => "ok")
                    .RequireAuthorization();
            });
        });

        var host = await hostBuilder.StartAsync();
        _server = host.GetTestServer();
        _client = _server.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient CreateClientWithHeaders(
        string? userId = null,
        string? role = null,
        string? shopId = null)
    {
        var client = _server.CreateClient();
        if (userId is not null)
            client.DefaultRequestHeaders.Add(GatewayAuthOptions.UserIdHeader, userId);
        if (role is not null)
            client.DefaultRequestHeaders.Add(GatewayAuthOptions.RoleHeader, role);
        if (shopId is not null)
            client.DefaultRequestHeaders.Add(GatewayAuthOptions.ShopIdHeader, shopId);
        return client;
    }

    [Fact]
    public async Task FullPipeline_WithAllHeaders_PopulatesUserContext()
    {
        var userId = Guid.NewGuid();
        var shopId = Guid.NewGuid();

        var client = CreateClientWithHeaders(
            userId.ToString(), "Seller", shopId.ToString());

        var response = await client.GetAsync("/test/user-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserContextResponse>();
        body!.IsAuthenticated.Should().BeTrue();
        body.UserId.Should().Be(userId.ToString());
        body.Role.Should().Be("Seller");
        body.ShopId.Should().Be(shopId.ToString());
    }

    [Fact]
    public async Task FullPipeline_WithUserIdOnly_AuthenticatesButNoRoleOrShop()
    {
        var userId = Guid.NewGuid();

        var client = CreateClientWithHeaders(userId: userId.ToString());

        var response = await client.GetAsync("/test/user-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserContextResponse>();
        body!.IsAuthenticated.Should().BeTrue();
        body.UserId.Should().Be(userId.ToString());
        body.Role.Should().BeNull();
        body.ShopId.Should().BeNull();
    }

    [Fact]
    public async Task FullPipeline_WithoutHeaders_NotAuthenticated()
    {
        var response = await _client.GetAsync("/test/user-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserContextResponse>();
        body!.IsAuthenticated.Should().BeFalse();
        body.UserId.Should().BeNull();
        body.Role.Should().BeNull();
        body.ShopId.Should().BeNull();
    }

    [Fact]
    public async Task AuthorizedEndpoint_WithUserIdHeader_ReturnsOk()
    {
        var userId = Guid.NewGuid();

        var client = CreateClientWithHeaders(userId: userId.ToString());

        var response = await client.GetAsync("/test/authorized");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthorizedEndpoint_WithoutUserIdHeader_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/test/authorized");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullPipeline_WithInvalidGuidUserId_NotAuthenticated()
    {
        var client = CreateClientWithHeaders(userId: "not-a-guid");

        var response = await client.GetAsync("/test/user-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserContextResponse>();
        body!.IsAuthenticated.Should().BeFalse();
        body.UserId.Should().BeNull();
    }

    private sealed class UserContextResponse
    {
        public bool IsAuthenticated { get; set; }
        public string? UserId { get; set; }
        public string? Role { get; set; }
        public string? ShopId { get; set; }
    }
}
```

- [ ] **Step 2: 运行集成测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "GatewayHeaderIntegrationTests"`
Expected: `Passed: 6` — 6 个集成测试全部通过

- [ ] **Step 3: 验证全量 Infrastructure 测试通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/`
Expected: 全部通过（Task 1 的 12 个 + Task 2 的 14 个 + Task 3 的 7 个 + Task 6 的 6 个 + 既有测试）

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/GatewayHeaderIntegrationTests.cs
git commit -m "test(auth): 添加 GatewayHeader 认证端到端集成测试"
```

---

## 实施后验证清单

完成所有 Task 后执行以下整体验证：

- [ ] **全量编译：** `dotnet build Leno.slnx` — 所有项目编译成功
- [ ] **全量测试：** `dotnet test Leno.slnx` — 所有测试通过
- [ ] **认证方案验证：** 11 个微服务 `Program.cs` 均使用 `AddGatewayAuth()` 替代 `AddJwtBearer()`，且保留 `AddAuthorization()`
- [ ] **JwtBearer 引用清理：** 11 个微服务 `Program.cs` 不再引用 `Microsoft.AspNetCore.Authentication.JwtBearer` 和 `Microsoft.IdentityModel.Tokens`
- [ ] **CurrentUserContext 验证：** `CurrentUserContext.cs` 不再引用 `JwtTokenGenerator.GetUserId/GetRole/GetShopId`，改为从请求头提取
- [ ] **保留检查：** `AddLenoInfrastructure()`、`AddInternalApiKeyAuth()`、`AddAuthorization()`、`GlobalExceptionMiddleware`、`InternalApiKeyMiddleware` 在所有服务中保持不变
- [ ] **生产配置（可选）：** 在生产环境 `appsettings.json` 中配置 `GatewayAuth:TrustedProxies` 为网关 IP，启用来源校验防止请求绕过网关
