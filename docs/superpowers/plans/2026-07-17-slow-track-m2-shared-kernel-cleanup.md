# 慢轨 M2 共享内核清理 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** DomainException 移除 HttpStatusCode 字段，新建 ErrorCodeMapping 接管 HTTP 状态码映射；MoneyJsonConverter 剥离存储格式职责（ToStorage/FromStorage 死代码删除）；PageResult 双定义合并（删除 SharedKernel 版本）；SpecAttribute 移除 [JsonConstructor] 标注

**Architecture:** DomainException 仅保留 ErrorCode（string）+ Message；GlobalExceptionMiddleware 改为查 ErrorCodeMapping.GetStatusCode(ex.ErrorCode)；ErrorCodeMapping 采用混合方案——显式字典 + 后缀约定规则（`_NOT_FOUND`→404、`_ALREADY_*`/`_EXISTS`/`_CONFLICT`→409、`_FORBIDDEN`→403、`_UNAVAILABLE`→503、`_FAILED`→502、`_MISSING`→500、其余→400）；MoneyJsonConverter 仅保留 JSON 序列化职责；PageResult<T> 仅在 SharedContracts 存在一份

**Tech Stack:** .NET 10、ASP.NET Core 10、xUnit、FluentAssertions

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §9](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 5（M1 共享内核事件契约分离）完成；M2 改造前 ErrorCode 命名约定已由 M1 各 BC 消费者稳定使用

**向后兼容策略:** 三步迁移——(1) 建 ErrorCodeMapping 并让 GlobalExceptionMiddleware 优先查映射表，未命中回退 DomainException.HttpStatusCode（兼容期）；(2) 逐 BC 改造 100 处带 HttpStatusCode 的异常抛出（移除 httpStatusCode 实参，依赖 ErrorCodeMapping 按后缀推断）；(3) 全部改造完成后删除 DomainException.HttpStatusCode 字段

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| DomainException 基类 | `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs:7-27` | abstract，含 `ErrorCode` (string) + `HttpStatusCode` (int)；两个 protected 构造函数均带 `httpStatusCode = 400` 默认值 |
| GlobalExceptionMiddleware | `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs:80-101` | 第 84-87 行 `case DomainException domainEx: return (domainEx.HttpStatusCode == 0 ? 400 : domainEx.HttpStatusCode, ...)` 硬编码 switch |
| ErrorCodeMapping（不存在） | — | 需新建于 `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs` |
| 11 个 BC XxxDomainException 子类 | 见下方完整清单 | 全部 sealed 继承 DomainException；构造函数签名不统一（SystemAdminDomainException 无默认 errorCode） |
| 4 个额外 DomainException 子类 | 见下方清单 | FileStorageException + 3 个 XxxValidationException，需一并改造 |
| 100 处带 HttpStatusCode 调用 | 见下方按状态码分类清单 | grep `new.*DomainException.*,\s*\d{3}\s*\)` 命中 100 处，分布在 100 个 .cs 文件 |
| MoneyJsonConverter | `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs:66-90` | 第 69 行 `ToStorage`、第 75 行 `FromStorage` 静态方法，**零引用**（死代码） |
| Money EF 配置（OwnsOne 拆列） | `ShopMetricsConfiguration.cs:35,41` + `SKUConfiguration.cs:33` | 3 处 `OwnsOne(...Money...)` 拆为 Amount + Currency 两列，未使用 ToStorage |
| SpecAttribute [JsonConstructor] | `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs:17` | 标注在无参私有构造函数（第 18 行），反序列化绕过 Create 工厂校验 |
| SharedKernel PageResult | `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs` | **零业务引用**（死代码），仅 XML 注释提及 |
| SharedContracts PageResult | `src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs` | 14 处业务代码实际使用此版本 |

### 11 个 BC XxxDomainException 子类完整清单

| # | BC | 类名 | 文件路径 | 默认 errorCode | 重载数 | 特殊说明 |
|---|----|------|---------|----------------|--------|---------|
| 1 | UserAuth | `UserAuthDomainException` | `src/Services/UserAuth/Leno.UserAuth.Domain/Exceptions/UserAuthDomainException.cs` | `USER_AUTH_DOMAIN_ERROR` | 2 | 含 innerException 重载 |
| 2 | Product | `ProductDomainException` | `src/Services/Product/Leno.Product.Domain/Exceptions/ProductDomainException.cs` | `PRODUCT_DOMAIN_ERROR` | 2 | 含 innerException 重载 |
| 3 | Cart | `CartDomainException` | `src/Services/Cart/Leno.Cart.Domain/Exceptions/CartDomainException.cs` | `CART_ERROR` | 1 | — |
| 4 | Order | `OrderDomainException` | `src/Services/Order/Leno.Order.Domain/Exceptions/OrderDomainException.cs` | `ORDER_ERROR` | 2 | 含 innerException 重载 |
| 5 | Promotion | `PromotionDomainException` | `src/Services/Promotion/Leno.Promotion.Domain/Exceptions/PromotionDomainException.cs` | `PROMOTION_ERROR` | 1 | — |
| 6 | ReviewAfterSales | `ReviewDomainException` | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Exceptions/ReviewDomainException.cs` | `REVIEW_ERROR` | 1 | 类名注意：非 ReviewAfterSalesDomainException |
| 7 | PointsMembership | `PointsDomainException` | `src/Services/PointsMembership/Leno.PointsMembership.Domain/Exceptions/PointsDomainException.cs` | `POINTS_ERROR` | 1 | 类名注意：非 PointsMembershipDomainException |
| 8 | Payment | `PaymentDomainException` | `src/Services/Payment/Leno.Payment.Domain/Exceptions/PaymentDomainException.cs` | `PAYMENT_ERROR` | 1 | — |
| 9 | Notification | `NotificationDomainException` | `src/Services/Notification/Leno.Notification.Domain/Exceptions/NotificationDomainException.cs` | `NOTIFICATION_ERROR` | 1 | — |
| 10 | SellerShop | `SellerShopDomainException` | `src/Services/SellerShop/Leno.SellerShop.Domain/Exceptions/SellerShopDomainException.cs` | `SELLER_SHOP_DOMAIN_ERROR` | 2 | 含 innerException 重载 |
| 11 | SystemAdmin | `SystemAdminDomainException` | `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Exceptions/SystemAdminDomainException.cs` | **无默认值（必传）** | 2 | 唯一不一致：errorCode 必传，第一个重载不含 httpStatusCode |

### 4 个额外 DomainException 子类

| 类名 | 路径 | 说明 |
|------|------|------|
| `FileStorageException`（internal sealed） | `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs:161` | 文件存储基础设施异常，被 `throw new FileStorageException(...)` 调用 4 处（行 37、42、71、135） |
| `UserAuthValidationException`（public sealed） | `src/Services/UserAuth/Leno.UserAuth.Application/Exceptions/UserAuthValidationException.cs:8` | UserAuth 应用层验证异常，被调用 7 处 |
| `SellerShopValidationException`（public sealed） | `src/Services/SellerShop/Leno.SellerShop.Application/Exceptions/SellerShopValidationException.cs:8` | SellerShop 应用层验证异常，被调用 2 处 |
| `ProductValidationException`（public sealed） | `src/Services/Product/Leno.Product.Application/Exceptions/ProductValidationException.cs:8` | Product 应用层验证异常，被调用 4 处 |

### 100 处带 HttpStatusCode 调用按状态码分类

**改造规则**：移除 `httpStatusCode` 实参（保留 errorCode 字面量），由 ErrorCodeMapping 按后缀约定自动推断 HTTP 状态码。下表列出每个状态码对应的 ErrorCode 后缀约定与典型示例（完整 100 处清单见 Task 3 Step 1 的 grep 命令输出）。

| HTTP 状态码 | ErrorCode 后缀约定 | 典型 ErrorCode 示例 | 数量（约） |
|---|---|---|---|
| 404 Not Found | `*_NOT_FOUND` | `USER_NOT_FOUND`、`CART_NOT_FOUND`、`SHOP_NOT_FOUND`、`SECKILL_NOT_FOUND`、`COUPON_NOT_FOUND`、`SELLER_NOT_FOUND`、`ROLE_NOT_FOUND`、`TASK_NOT_FOUND`、`CHANNEL_CONFIG_NOT_FOUND`、`CART_ITEM_NOT_FOUND`、`ORDER_SKU_NOT_FOUND`、`REVIEW_ORDER_NOT_FOUND`、`AFTERSALES_ORDER_NOT_FOUND`、`QUALIFICATION_NOT_FOUND` | ~25 |
| 409 Conflict | `*_ALREADY_*`、`*_EXISTS`、`*_CONFLICT` | `USER_USERNAME_EXISTS`、`USER_EMAIL_EXISTS`、`USER_PHONE_EXISTS`、`ROLE_NAME_EXISTS`、`SHOP_ALREADY_EXISTS`、`SHOP_ALREADY_CLOSED`、`ANNOUNCEMENT_ALREADY_PUBLISHED`、`TASK_ALREADY_ENABLED`、`TASK_ALREADY_DISABLED`、`USER_2FA_ALREADY_ENABLED`、`USER_DISABLE_SELF`、`USER_REVOKE_ADMIN_SELF`、`USER_LAST_ROLE`、`EXTERNAL_LOGIN_LAST`、`CART_VARIETY_LIMIT`、`SELLER_APPROVED`、`SHOP_CLOSED`、`ADDRESS_ALREADY_DELETED`、`ADDRESS_NOT_ACTIVE` | ~30 |
| 401 Unauthorized | `*_INVALID`（密码/token）、`*_EXPIRED`、`*_REQUIRED` | `USER_OLD_PASSWORD_INVALID`、`USER_2FA_CODE_INVALID`、`USER_2FA_TEMP_TOKEN_INVALID`、`USER_RESET_TOKEN_INVALID`、`OAUTH_STATE_EXPIRED`、`CART_USER_REQUIRED` | ~10 |
| 403 Forbidden | `*_FORBIDDEN`、`*_DISABLED`（部分） | `ADDRESS_FORBIDDEN`、`REVIEW_FORBIDDEN`、`AFTERSALES_FORBIDDEN`、`USER_DISABLED` | ~5 |
| 400 Bad Request（显式） | 默认 | `USER_NO_LOGIN_METHOD`、`USER_PASSWORD_SAME`、`USER_2FA_NOT_INITIATED`、`OAUTH_STATE_INVALID`、`CART_ANONYMOUS_ID_REQUIRED`、`METRICS_INVALID_RANGE` | ~15 |
| 500 Internal Server Error | `*_MISSING` | `OAUTH_CONFIG_MISSING`、`USER_2FA_SECRET_MISSING` | ~5 |
| 502 Bad Gateway | `*_FAILED` | `OAUTH_TOKEN_EXCHANGE_FAILED`、`OAUTH_USERINFO_FAILED` | ~7 |
| 503 Service Unavailable | `*_UNAVAILABLE` | `CART_PRICE_UNAVAILABLE` | ~3 |

**注意**：部分 ErrorCode 不严格遵循后缀约定（如 `USER_DISABLE_SELF`→409、`USER_LAST_ROLE`→409、`EXTERNAL_LOGIN_LAST`→409、`CART_VARIETY_LIMIT`→409、`SELLER_APPROVED`→409、`SHOP_CLOSED`→409、`ADDRESS_ALREADY_DELETED`→409、`ADDRESS_NOT_ACTIVE`→409、`USER_DISABLED`→403）。这些需在 Task 1 Step 3 显式注册到 ErrorCodeMapping。

---

## Task 1: 新建 ErrorCodeMapping 混合映射表

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/ErrorCodeMappingTests.cs`

- [ ] **Step 1: 创建 ErrorCodeMapping 类**

创建 `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs`：

```csharp
using System.Collections.Concurrent;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// ErrorCode 到 HTTP 状态码的映射中心。
/// 采用混合方案：优先查显式注册表，未命中按 ErrorCode 后缀约定推断，再未命中返回 400。
/// 各 BC 启动时通过 <see cref="Register"/> 注册不遵循后缀约定的特殊 ErrorCode。
/// </summary>
public static class ErrorCodeMapping
{
    private static readonly ConcurrentDictionary<string, int> _explicit = new(StringComparer.Ordinal);

    // 后缀约定规则（按优先级排序，先匹配先返回）
    private static readonly (string Suffix, int StatusCode)[] _suffixRules =
    [
        ("_NOT_FOUND", 404),
        ("_ALREADY_", 409),
        ("_EXISTS", 409),
        ("_CONFLICT", 409),
        ("_FORBIDDEN", 403),
        ("_UNAVAILABLE", 503),
        ("_FAILED", 502),
        ("_MISSING", 500),
        ("_EXPIRED", 401),
        ("_REQUIRED", 401),
    ];

    /// <summary>
    /// 显式注册 ErrorCode 到 HTTP 状态码映射（用于不遵循后缀约定的特殊 ErrorCode）。
    /// </summary>
    public static void Register(string errorCode, int statusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        _explicit[errorCode] = statusCode;
    }

    /// <summary>
    /// 批量注册。
    /// </summary>
    public static void RegisterAll(params (string ErrorCode, int StatusCode)[] entries)
    {
        foreach (var (code, status) in entries)
        {
            Register(code, status);
        }
    }

    /// <summary>
    /// 查询 ErrorCode 对应的 HTTP 状态码。
    /// 优先显式表 → 后缀规则 → 默认 400。
    /// </summary>
    public static int GetStatusCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return 400;
        }

        if (_explicit.TryGetValue(errorCode, out var explicitCode))
        {
            return explicitCode;
        }

        foreach (var (suffix, statusCode) in _suffixRules)
        {
            if (errorCode.EndsWith(suffix, StringComparison.Ordinal))
            {
                return statusCode;
            }
        }

        return 400;
    }

    /// <summary>
    /// 重置显式注册表（仅用于单元测试隔离）。
    /// </summary>
    internal static void Reset() => _explicit.Clear();
}
```

- [ ] **Step 2: 创建测试文件**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/ErrorCodeMappingTests.cs`：

```csharp
using Leno.Infrastructure.Middleware;

namespace Leno.Infrastructure.Tests.Middleware;

public class ErrorCodeMappingTests
{
    [Theory]
    [InlineData("USER_NOT_FOUND", 404)]
    [InlineData("CART_ITEM_NOT_FOUND", 404)]
    [InlineData("SHOP_ALREADY_EXISTS", 409)]
    [InlineData("ANNOUNCEMENT_ALREADY_PUBLISHED", 409)]
    [InlineData("USER_USERNAME_EXISTS", 409)]
    [InlineData("TASK_CONFLICT", 409)]
    [InlineData("ADDRESS_FORBIDDEN", 403)]
    [InlineData("REVIEW_FORBIDDEN", 403)]
    [InlineData("CART_PRICE_UNAVAILABLE", 503)]
    [InlineData("OAUTH_TOKEN_EXCHANGE_FAILED", 502)]
    [InlineData("OAUTH_USERINFO_FAILED", 502)]
    [InlineData("OAUTH_CONFIG_MISSING", 500)]
    [InlineData("USER_2FA_SECRET_MISSING", 500)]
    [InlineData("OAUTH_STATE_EXPIRED", 401)]
    [InlineData("CART_USER_REQUIRED", 401)]
    public void GetStatusCode_WithSuffixConvention_ShouldInferCorrectly(string errorCode, int expected)
    {
        ErrorCodeMapping.Reset();
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("USER_NO_LOGIN_METHOD")]
    [InlineData("USER_PASSWORD_SAME")]
    [InlineData("CART_ANONYMOUS_ID_REQUIRED")]
    [InlineData("METRICS_INVALID_RANGE")]
    [InlineData("UNKNOWN_ERROR")]
    [InlineData("")]
    [InlineData(null)]
    public void GetStatusCode_WithUnmatchedSuffix_ShouldReturn400(string? errorCode)
    {
        ErrorCodeMapping.Reset();
        var actual = ErrorCodeMapping.GetStatusCode(errorCode);
        actual.Should().Be(400);
    }

    [Fact]
    public void Register_ShouldOverrideSuffixConvention()
    {
        ErrorCodeMapping.Reset();
        ErrorCodeMapping.Register("USER_DISABLED", 403);

        var actual = ErrorCodeMapping.GetStatusCode("USER_DISABLED");

        actual.Should().Be(403);
    }

    [Fact]
    public void Register_ShouldTakePrecedenceOverSuffix()
    {
        ErrorCodeMapping.Reset();
        // USER_NOT_FOUND 按后缀应为 404，显式注册为 410 Gone
        ErrorCodeMapping.Register("USER_NOT_FOUND", 410);

        var actual = ErrorCodeMapping.GetStatusCode("USER_NOT_FOUND");

        actual.Should().Be(410);
    }

    [Fact]
    public void RegisterAll_ShouldRegisterMultipleEntries()
    {
        ErrorCodeMapping.Reset();
        ErrorCodeMapping.RegisterAll(
            ("USER_DISABLE_SELF", 409),
            ("USER_REVOKE_ADMIN_SELF", 409),
            ("USER_LAST_ROLE", 409),
            ("EXTERNAL_LOGIN_LAST", 409),
            ("CART_VARIETY_LIMIT", 409),
            ("SELLER_APPROVED", 409),
            ("SHOP_CLOSED", 409),
            ("ADDRESS_ALREADY_DELETED", 409),
            ("ADDRESS_NOT_ACTIVE", 409),
            ("USER_DISABLED", 403));

        ErrorCodeMapping.GetStatusCode("USER_DISABLE_SELF").Should().Be(409);
        ErrorCodeMapping.GetStatusCode("USER_DISABLED").Should().Be(403);
        ErrorCodeMapping.GetStatusCode("CART_VARIETY_LIMIT").Should().Be(409);
    }
}
```

- [ ] **Step 3: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ErrorCodeMappingTests"`
Expected: PASS（5 个测试全部通过）

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/ErrorCodeMappingTests.cs
git commit -m "feat(M2.1): 新建 ErrorCodeMapping 混合映射表（显式表+后缀约定）"
```

---

## Task 2: 改造 GlobalExceptionMiddleware 优先查 ErrorCodeMapping（兼容期）

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs:80-101`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/GlobalExceptionMiddlewareTests.cs`

**兼容期策略**：GlobalExceptionMiddleware 优先查 ErrorCodeMapping.GetStatusCode，未命中（返回 400）时回退 DomainException.HttpStatusCode。此阶段保留 DomainException.HttpStatusCode 字段，确保未改造的 BC 仍按原逻辑工作。

- [ ] **Step 1: 写失败测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/GlobalExceptionMiddlewareTests.cs`：

```csharp
using System.Net;
using System.Text.Json;
using Leno.Infrastructure.Middleware;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Infrastructure.Tests.Middleware;

public class GlobalExceptionMiddlewareTests
{
    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string message, string errorCode = "TEST_ERROR", int httpStatusCode = 400)
            : base(message, errorCode, httpStatusCode) { }
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

        // 通过反射调用 InvokeAsync，模拟抛异常
        var next = new RequestDelegate(_ => Task.FromException(ex));
        var middlewareType = typeof(GlobalExceptionMiddleware);
        var field = middlewareType.GetField("_next", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
    public async Task InvokeAsync_DomainException_WithUnmatchedCode_ShouldFallbackToHttpStatusCode()
    {
        // 兼容期：未在 ErrorCodeMapping 命中的特殊 ErrorCode，回退 DomainException.HttpStatusCode
        var ex = new TestDomainException("自定义错误", "CUSTOM_SPECIAL_ERROR", 422);

        var response = await InvokeMiddleware(ex);

        response.Should().NotBeNull();
        response!.Code.Should().Be(422);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_WithUnmatchedCodeAndDefault400_ShouldReturn400()
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
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GlobalExceptionMiddlewareTests"`
Expected: FAIL（前 3 个测试失败：当前中间件直接读 HttpStatusCode，不会查 ErrorCodeMapping）

- [ ] **Step 3: 改造 GlobalExceptionMiddleware.Resolve 方法**

修改 `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs:80-101`，将 `Resolve` 方法改为：

```csharp
private (int StatusCode, string Message, LogLevel LogLevel) Resolve(Exception exception)
{
    switch (exception)
    {
        case DomainException domainEx:
            // 兼容期：优先查 ErrorCodeMapping，未命中（返回 0 表示未注册）时回退 HttpStatusCode
            var mapped = ErrorCodeMapping.GetStatusCode(domainEx.ErrorCode);
            var fallback = domainEx.HttpStatusCode == 0 ? 400 : domainEx.HttpStatusCode;
            // 后缀规则命中的 mapped 与 fallback 一致，或显式注册覆盖时取 mapped
            // 兼容期策略：mapped != 400（命中规则或显式注册）时取 mapped；mapped == 400 且 fallback != 400 时取 fallback（未改造的旧异常）
            var statusCode = mapped != 400 || fallback == 400 ? mapped : fallback;
            return (statusCode, domainEx.Message, LogLevel.Warning);

        case UnauthorizedAccessException:
            return (StatusCodes.Status401Unauthorized, "未授权", LogLevel.Warning);

        case ArgumentException argEx:
            return (StatusCodes.Status400BadRequest, argEx.Message, LogLevel.Warning);

        default:
            var message = _environment.IsDevelopment()
                ? exception.Message
                : "服务器内部错误";
            return (StatusCodes.Status500InternalServerError, message, LogLevel.Error);
    }
}
```

**关键设计说明**：
- `mapped != 400`：ErrorCodeMapping 命中后缀规则或显式注册（非 400），直接采用 mapped
- `mapped == 400 && fallback == 400`：两者都是 400，取 mapped（默认）
- `mapped == 400 && fallback != 400`：mapped 未命中（返回默认 400），但 fallback 是旧 HttpStatusCode（如 422），取 fallback（兼容期保护未改造的异常）

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GlobalExceptionMiddlewareTests"`
Expected: PASS（6 个测试全部通过）

- [ ] **Step 5: 运行全量基础设施测试确保无回归**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj`
Expected: PASS（全部既有测试通过）

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/GlobalExceptionMiddlewareTests.cs
git commit -m "feat(M2.1): GlobalExceptionMiddleware 优先查 ErrorCodeMapping，未命中回退 HttpStatusCode（兼容期）"
```

---

## Task 3: 注册不遵循后缀约定的特殊 ErrorCode

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`

部分 ErrorCode 不严格遵循后缀约定，需在 DI 启动时显式注册到 ErrorCodeMapping。基于 Task 1 关键代码定位表第 10 行"100 处带 HttpStatusCode 调用按状态码分类"清单，整理出 10 个特殊 ErrorCode。

- [ ] **Step 1: 在 ServiceCollectionExtensions 新增 RegisterSpecialErrorCodes 方法**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，在 `AddLenoInfrastructure` 方法第 39-46 行的 `AddOptions(services, configuration);` 之前插入 `RegisterSpecialErrorCodes();`，并在类末尾（第 164 行 `AddLenoFullHealthChecks` 方法之后）添加：

```csharp
    /// <summary>
    /// 注册不遵循后缀约定的特殊 ErrorCode 到 HTTP 状态码映射。
    /// 这些 ErrorCode 的实际 HTTP 语义与后缀约定不符（如 USER_DISABLED→403 而非 400）。
    /// </summary>
    private static void RegisterSpecialErrorCodes()
    {
        ErrorCodeMapping.RegisterAll(
            // 409 Conflict（状态冲突，但 ErrorCode 后缀不匹配 _ALREADY_/_EXISTS_/_CONFLICT）
            ("USER_DISABLE_SELF", 409),
            ("USER_NOT_SUSPENDED", 409),
            ("USER_REVOKE_ADMIN_SELF", 409),
            ("USER_LAST_ROLE", 409),
            ("EXTERNAL_LOGIN_LAST", 409),
            ("CART_VARIETY_LIMIT", 409),
            ("SELLER_APPROVED", 409),
            ("SHOP_CLOSED", 409),
            ("ADDRESS_ALREADY_DELETED", 409),
            ("ADDRESS_NOT_ACTIVE", 409),
            ("USER_USERNAME_CONFLICT", 409),
            // 403 Forbidden（USER_DISABLED 是禁用而非校验失败）
            ("USER_DISABLED", 403),
            // 500 Internal Server Error（USER_2FA_SECRET_MISSING 已匹配 _MISSING，但显式注册以防后缀变更）
            ("USER_2FA_SECRET_MISSING", 500),
            // 401 Unauthorized（USER_OLD_PASSWORD_INVALID 已匹配 _INVALID，但 _INVALID 默认 400，需显式 401）
            ("USER_OLD_PASSWORD_INVALID", 401),
            ("USER_2FA_CODE_INVALID", 401),
            ("USER_2FA_TEMP_TOKEN_INVALID", 401),
            ("USER_RESET_TOKEN_INVALID", 401));
    }
```

并在文件顶部添加 `using Leno.Infrastructure.Middleware;`：

```csharp
using Leno.Infrastructure.Middleware;
```

修改后的 `AddLenoInfrastructure` 方法头部应为：

```csharp
    public static IServiceCollection AddLenoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RegisterSpecialErrorCodes();
        AddOptions(services, configuration);
        AddFileStorage(services, configuration);
        AddAuth(services);
        AddRedis(services, configuration);
        AddElasticsearch(services, configuration);
        AddEventBus(services, configuration, configureConsumers);
        AddHealthChecks(services);

        return services;
    }
```

- [ ] **Step 2: 运行全量测试确保无回归**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj`
Expected: PASS（特殊 ErrorCode 已注册，ErrorCodeMappingTests 仍通过）

- [ ] **Step 3: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "feat(M2.1): 注册 17 个不遵循后缀约定的特殊 ErrorCode 到 ErrorCodeMapping"
```

---

## Task 4: 改造 100 处带 HttpStatusCode 的异常抛出（按 BC 分批）

**Files:**
- Modify: 100 个 .cs 文件（按 BC 分批，完整清单见下方 grep 命令）

**改造规则**：移除 `new XxxDomainException(...)` 调用末尾的 `httpStatusCode` 实参（如 `, 404)` → `)`），保留 errorCode 字面量。ErrorCodeMapping 按后缀约定或显式注册自动推断 HTTP 状态码。

**前提**：Task 1-3 已完成，ErrorCodeMapping 已就绪；DomainException.HttpStatusCode 字段仍保留（兼容期）。

- [ ] **Step 1: 生成完整调用清单**

Run: `grep -rn "new .*DomainException(.*,\s*[0-9]\{3\}\s*)" src/ --include="*.cs" > /tmp/m2-exception-calls.txt`
Expected: 输出 100 行，每行格式 `文件路径:行号:调用代码`

按 BC 分组执行（避免单次提交过大）。下表为各 BC 的预期改造文件数：

| BC | 预期文件数 | 关键文件（前 3 个） |
|----|-----------|---------------------|
| UserAuth | ~20 | `User.cs`、`UserAppService.cs`、`Address.cs` |
| Product | ~5 | `SPU.cs`、`SPUAppService.cs`、`StockBaseline.cs` |
| Cart | ~5 | `Cart.cs`、`CartAppService.cs`、`CartPriceService.cs` |
| Order | ~10 | `Order.cs`、`AntiCorruptionServices.cs`、`StockReservation.cs` |
| Promotion | ~10 | `SeckillActivity.cs`、`Coupon.cs`、`UserCoupon.cs` |
| ReviewAfterSales | ~5 | `AfterSales.cs`、`Review.cs`、`ReviewEligibilityChecker.cs` |
| PointsMembership | ~5 | `PointsAccount.cs`、`TaskDefinition.cs`、`MembershipPackage.cs` |
| Payment | ~10 | `PaymentOrder.cs`、`RefundOrder.cs`、`PaymentChannelConfig.cs` |
| Notification | ~5 | `NotificationRecord.cs`、`NotificationTemplate.cs` |
| SellerShop | ~15 | `Shop.cs`、`SellerProfile.cs`、`ShopQualification.cs` |
| SystemAdmin | ~10 | `DeadLetterMessage.cs`、`IndexRebuildTask.cs`、`AuditLog.cs` |
| Application 层 ValidationException | ~3 | `UserAppService.cs`、`ShopAppService.cs`、`SPUAppService.cs` |
| Infrastructure 层 FileStorageException | ~1 | `LocalFileStorageService.cs` |

- [ ] **Step 2: 改造 UserAuth BC**

逐文件修改 UserAuth BC 中所有 `new UserAuthDomainException(...)` 调用，移除末尾 httpStatusCode 实参。**示例**：

`src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs:155` 改造前：
```csharp
throw new UserAuthDomainException("用户未设置密码", "USER_NO_PASSWORD", 409);
```
改造后：
```csharp
throw new UserAuthDomainException("用户未设置密码", "USER_NO_PASSWORD");
```

`src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs:556` 改造前：
```csharp
throw new UserAuthDomainException($"用户 {userId} 不存在", "USER_NOT_FOUND", 404);
```
改造后：
```csharp
throw new UserAuthDomainException($"用户 {userId} 不存在", "USER_NOT_FOUND");
```

`src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/WeChatOAuth2Client.cs:70` 改造前：
```csharp
throw new UserAuthDomainException("微信 OAuth Token 交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED", 502);
```
改造后：
```csharp
throw new UserAuthDomainException("微信 OAuth Token 交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED");
```

UserAuth BC 的 `UserAuthValidationException` 调用（7 处）同样移除 httpStatusCode 实参。

完成后验证：
Run: `grep -rn "new UserAuthDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/UserAuth/ --include="*.cs"`
Expected: 无输出（零命中）
Run: `grep -rn "new UserAuthValidationException(.*,\s*[0-9]\{3\}\s*)" src/Services/UserAuth/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 3: 运行 UserAuth BC 测试**

Run: `dotnet test src/Services/UserAuth/ --filter "Category!=Integration"`
Expected: PASS（UserAuth 全部单元测试通过，HTTP 状态码由 ErrorCodeMapping 接管）

- [ ] **Step 4: 提交 UserAuth BC**

```bash
git add src/Services/UserAuth/
git commit -m "refactor(M2.1): UserAuth BC 移除异常抛出的 httpStatusCode 实参（28处）"
```

- [ ] **Step 5: 改造 Product BC**

逐文件修改 Product BC 中所有 `new ProductDomainException(...)` 与 `new ProductValidationException(...)` 调用，移除末尾 httpStatusCode 实参。

`src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs:32 处` 与 `src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs:10 处` 等参照 UserAuth 改造模式。

完成后验证：
Run: `grep -rn "new Product\(Domain\|Validation\)Exception(.*,\s*[0-9]\{3\}\s*)" src/Services/Product/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 6: 运行 Product BC 测试**

Run: `dotnet test src/Services/Product/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 7: 提交 Product BC**

```bash
git add src/Services/Product/
git commit -m "refactor(M2.1): Product BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 8: 改造 Cart BC**

逐文件修改 Cart BC 中所有 `new CartDomainException(...)` 调用（约 5 处），移除末尾 httpStatusCode 实参。关键位置：`Cart.cs:96,107,219`、`CartAppService.cs:141,158,163,197,213`、`AnonymousCartAppService.cs:152`、`CartPriceService.cs:69,92`。

完成后验证：
Run: `grep -rn "new CartDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/Cart/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 9: 运行 Cart BC 测试**

Run: `dotnet test src/Services/Cart/ --filter "Category!=Integration"`
Expected: PASS（注意 `CartAppServiceTests.cs:320,389,409` 测试代码中的异常抛出也需同步改造）

- [ ] **Step 10: 提交 Cart BC**

```bash
git add src/Services/Cart/
git commit -m "refactor(M2.1): Cart BC 移除异常抛出的 httpStatusCode 实参（含测试代码）"
```

- [ ] **Step 11: 改造 Order BC**

逐文件修改 Order BC 中所有 `new OrderDomainException(...)` 调用（约 10 处），移除末尾 httpStatusCode 实参。关键位置：`Order.cs:31 处`、`AntiCorruptionServices.cs:13 处`、`StockReservation.cs:13 处`、`OrderAppService.cs:7 处`、`OrderPricingDomainService.cs:2 处`。

完成后验证：
Run: `grep -rn "new OrderDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/Order/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 12: 运行 Order BC 测试**

Run: `dotnet test src/Services/Order/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 13: 提交 Order BC**

```bash
git add src/Services/Order/
git commit -m "refactor(M2.1): Order BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 14: 改造 Promotion BC**

逐文件修改 Promotion BC 中所有 `new PromotionDomainException(...)` 调用（约 10 处），移除末尾 httpStatusCode 实参。关键位置：`SeckillActivity.cs:21 处`、`Coupon.cs:15 处`、`UserCoupon.cs:12 处`、`PromotionActivity.cs:7 处`、`SeckillAppService.cs:4 处`、`CouponAppService.cs:5 处`、`PromotionAppService.cs:1 处`。

完成后验证：
Run: `grep -rn "new PromotionDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/Promotion/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 15: 运行 Promotion BC 测试**

Run: `dotnet test src/Services/Promotion/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 16: 提交 Promotion BC**

```bash
git add src/Services/Promotion/
git commit -m "refactor(M2.1): Promotion BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 17: 改造 ReviewAfterSales BC**

逐文件修改 ReviewAfterSales BC 中所有 `new ReviewDomainException(...)` 调用（约 5 处），移除末尾 httpStatusCode 实参。关键位置：`AfterSales.cs:33 处`、`Review.cs:19 处`、`ReviewEligibilityChecker.cs:5 处`、`AfterSalesEligibilityChecker.cs:5 处`。

完成后验证：
Run: `grep -rn "new ReviewDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/ReviewAfterSales/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 18: 运行 ReviewAfterSales BC 测试**

Run: `dotnet test src/Services/ReviewAfterSales/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 19: 提交 ReviewAfterSales BC**

```bash
git add src/Services/ReviewAfterSales/
git commit -m "refactor(M2.1): ReviewAfterSales BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 20: 改造 PointsMembership BC**

逐文件修改 PointsMembership BC 中所有 `new PointsDomainException(...)` 调用（约 5 处），移除末尾 httpStatusCode 实参。关键位置：`PointsAccount.cs`、`UserMembership.cs`、`TaskDefinition.cs`、`MembershipPackage.cs`、`PointsLedger.cs` 等。

完成后验证：
Run: `grep -rn "new PointsDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/PointsMembership/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 21: 运行 PointsMembership BC 测试**

Run: `dotnet test src/Services/PointsMembership/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 22: 提交 PointsMembership BC**

```bash
git add src/Services/PointsMembership/
git commit -m "refactor(M2.1): PointsMembership BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 23: 改造 Payment BC**

逐文件修改 Payment BC 中所有 `new PaymentDomainException(...)` 调用（约 10 处），移除末尾 httpStatusCode 实参。关键位置：`PaymentOrder.cs:11 处`、`RefundOrder.cs:10 处`、`PaymentChannelConfig.cs:11 处`、`ReconciliationDiff.cs:7 处`、`PaymentChannelConfigAppService.cs:3 处`。

完成后验证：
Run: `grep -rn "new PaymentDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/Payment/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 24: 运行 Payment BC 测试**

Run: `dotnet test src/Services/Payment/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 25: 提交 Payment BC**

```bash
git add src/Services/Payment/
git commit -m "refactor(M2.1): Payment BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 26: 改造 Notification BC**

逐文件修改 Notification BC 中所有 `new NotificationDomainException(...)` 调用（约 5 处），移除末尾 httpStatusCode 实参。关键位置：`NotificationRecord.cs:17 处`、`NotificationTemplate.cs:12 处`、`NotificationTemplateAppService.cs:4 处`、`NotificationPreference.cs:4 处`、`ChannelSelector.cs:2 处`。

完成后验证：
Run: `grep -rn "new NotificationDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/Notification/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 27: 运行 Notification BC 测试**

Run: `dotnet test src/Services/Notification/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 28: 提交 Notification BC**

```bash
git add src/Services/Notification/
git commit -m "refactor(M2.1): Notification BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 29: 改造 SellerShop BC**

逐文件修改 SellerShop BC 中所有 `new SellerShopDomainException(...)` 与 `new SellerShopValidationException(...)` 调用（约 15 处），移除末尾 httpStatusCode 实参。关键位置：`Shop.cs:25 处`、`SellerProfile.cs:15 处`、`ShopQualification.cs:14 处`、`ShopAppService.cs`、`SellerAppService.cs`、`SellerDashboardAppService.cs`。

完成后验证：
Run: `grep -rn "new SellerShop\(Domain\|Validation\)Exception(.*,\s*[0-9]\{3\}\s*)" src/Services/SellerShop/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 30: 运行 SellerShop BC 测试**

Run: `dotnet test src/Services/SellerShop/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 31: 提交 SellerShop BC**

```bash
git add src/Services/SellerShop/
git commit -m "refactor(M2.1): SellerShop BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 32: 改造 SystemAdmin BC**

逐文件修改 SystemAdmin BC 中所有 `new SystemAdminDomainException(...)` 调用（约 10 处），移除末尾 httpStatusCode 实参。关键位置：`DeadLetterMessage.cs:16 处`、`IndexRebuildTask.cs:16 处`、`AuditLog.cs`、`AuditLogEntry.cs`、`DataDictionary.cs`、`SystemConfig.cs`、`SystemAnnouncement.cs` 等。

**注意**：SystemAdminDomainException 的构造函数第一个重载不含 httpStatusCode 参数，第二个含。当前调用可能使用第二个重载（3 参数），改造后统一使用第一个重载（2 参数）。

完成后验证：
Run: `grep -rn "new SystemAdminDomainException(.*,\s*[0-9]\{3\}\s*)" src/Services/SystemAdmin/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 33: 运行 SystemAdmin BC 测试**

Run: `dotnet test src/Services/SystemAdmin/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 34: 提交 SystemAdmin BC**

```bash
git add src/Services/SystemAdmin/
git commit -m "refactor(M2.1): SystemAdmin BC 移除异常抛出的 httpStatusCode 实参"
```

- [ ] **Step 35: 改造 Infrastructure 层 FileStorageException**

修改 `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs` 第 37、42、71、135 行的 `new FileStorageException(...)` 调用，移除末尾 httpStatusCode 实参。

完成后验证：
Run: `grep -rn "new FileStorageException(.*,\s*[0-9]\{3\}\s*)" src/BuildingBlocks/ --include="*.cs"`
Expected: 无输出

- [ ] **Step 36: 运行全量测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS（全部单元测试通过）

- [ ] **Step 37: 全局验证 grep 零命中**

Run: `grep -rn "new .*\(Domain\|Validation\|FileStorage\)Exception(.*,\s*[0-9]\{3\}\s*)" src/ --include="*.cs"`
Expected: 无输出（spec 验收要求：`new.*DomainException.*40[0-9]|new.*DomainException.*50[0-9]` 零命中）

- [ ] **Step 38: 提交 Infrastructure 层 + 完成标记**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs
git commit -m "refactor(M2.1): Infrastructure 层 FileStorageException 移除 httpStatusCode 实参，100 处改造全部完成"
```

---

## Task 5: 改造 11 个 BC XxxDomainException 子类 + 4 个额外子类构造函数

**Files:**
- Modify: 11 个 BC 的 `XxxDomainException.cs`
- Modify: 3 个 `XxxValidationException.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs`（FileStorageException 内嵌类）

**改造规则**：移除子类构造函数的 `httpStatusCode` 参数与传递给基类的 `httpStatusCode` 实参。

- [ ] **Step 1: 改造 UserAuthDomainException**

修改 `src/Services/UserAuth/Leno.UserAuth.Domain/Exceptions/UserAuthDomainException.cs`，移除两个构造函数的 `int httpStatusCode = 400` 参数与 `httpStatusCode` 实参：

```csharp
public sealed class UserAuthDomainException : DomainException
{
    public UserAuthDomainException(string message, string errorCode = "USER_AUTH_DOMAIN_ERROR")
        : base(message, errorCode) { }

    public UserAuthDomainException(string message, Exception innerException, string errorCode = "USER_AUTH_DOMAIN_ERROR")
        : base(message, innerException, errorCode) { }
}
```

- [ ] **Step 2: 改造 ProductDomainException**

修改 `src/Services/Product/Leno.Product.Domain/Exceptions/ProductDomainException.cs`：

```csharp
public sealed class ProductDomainException : DomainException
{
    public ProductDomainException(string message, string errorCode = "PRODUCT_DOMAIN_ERROR")
        : base(message, errorCode) { }

    public ProductDomainException(string message, Exception innerException, string errorCode = "PRODUCT_DOMAIN_ERROR")
        : base(message, innerException, errorCode) { }
}
```

- [ ] **Step 3: 改造 CartDomainException**

修改 `src/Services/Cart/Leno.Cart.Domain/Exceptions/CartDomainException.cs`：

```csharp
public sealed class CartDomainException : DomainException
{
    public CartDomainException(string message, string errorCode = "CART_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 4: 改造 OrderDomainException**

修改 `src/Services/Order/Leno.Order.Domain/Exceptions/OrderDomainException.cs`：

```csharp
public sealed class OrderDomainException : DomainException
{
    public OrderDomainException(string message, string errorCode = "ORDER_ERROR")
        : base(message, errorCode) { }

    public OrderDomainException(string message, Exception innerException, string errorCode = "ORDER_ERROR")
        : base(message, innerException, errorCode) { }
}
```

- [ ] **Step 5: 改造 PromotionDomainException**

修改 `src/Services/Promotion/Leno.Promotion.Domain/Exceptions/PromotionDomainException.cs`：

```csharp
public sealed class PromotionDomainException : DomainException
{
    public PromotionDomainException(string message, string errorCode = "PROMOTION_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 6: 改造 ReviewDomainException**

修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Exceptions/ReviewDomainException.cs`：

```csharp
public sealed class ReviewDomainException : DomainException
{
    public ReviewDomainException(string message, string errorCode = "REVIEW_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 7: 改造 PointsDomainException**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Exceptions/PointsDomainException.cs`：

```csharp
public sealed class PointsDomainException : DomainException
{
    public PointsDomainException(string message, string errorCode = "POINTS_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 8: 改造 PaymentDomainException**

修改 `src/Services/Payment/Leno.Payment.Domain/Exceptions/PaymentDomainException.cs`：

```csharp
public sealed class PaymentDomainException : DomainException
{
    public PaymentDomainException(string message, string errorCode = "PAYMENT_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 9: 改造 NotificationDomainException**

修改 `src/Services/Notification/Leno.Notification.Domain/Exceptions/NotificationDomainException.cs`：

```csharp
public sealed class NotificationDomainException : DomainException
{
    public NotificationDomainException(string message, string errorCode = "NOTIFICATION_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 10: 改造 SellerShopDomainException**

修改 `src/Services/SellerShop/Leno.SellerShop.Domain/Exceptions/SellerShopDomainException.cs`：

```csharp
public sealed class SellerShopDomainException : DomainException
{
    public SellerShopDomainException(string message, string errorCode = "SELLER_SHOP_DOMAIN_ERROR")
        : base(message, errorCode) { }

    public SellerShopDomainException(string message, Exception innerException, string errorCode = "SELLER_SHOP_DOMAIN_ERROR")
        : base(message, innerException, errorCode) { }
}
```

- [ ] **Step 11: 改造 SystemAdminDomainException**

修改 `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Exceptions/SystemAdminDomainException.cs`，统一为单个构造函数（移除含 httpStatusCode 的重载）：

```csharp
public sealed class SystemAdminDomainException : DomainException
{
    public SystemAdminDomainException(string message, string errorCode)
        : base(message, errorCode) { }
}
```

- [ ] **Step 12: 改造 UserAuthValidationException**

修改 `src/Services/UserAuth/Leno.UserAuth.Application/Exceptions/UserAuthValidationException.cs`，移除 httpStatusCode 参数：

```csharp
public sealed class UserAuthValidationException : DomainException
{
    public UserAuthValidationException(string message, string errorCode = "USER_AUTH_VALIDATION_ERROR")
        : base(message, errorCode) { }
}
```

（保持原有默认 errorCode 风格，若原文件默认值不同请保持原值）

- [ ] **Step 13: 改造 SellerShopValidationException**

修改 `src/Services/SellerShop/Leno.SellerShop.Application/Exceptions/SellerShopValidationException.cs`：

```csharp
public sealed class SellerShopValidationException : DomainException
{
    public SellerShopValidationException(string message, string errorCode = "SELLER_SHOP_VALIDATION_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 14: 改造 ProductValidationException**

修改 `src/Services/Product/Leno.Product.Application/Exceptions/ProductValidationException.cs`：

```csharp
public sealed class ProductValidationException : DomainException
{
    public ProductValidationException(string message, string errorCode = "PRODUCT_VALIDATION_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 15: 改造 FileStorageException**

修改 `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs:161` 的内嵌 `FileStorageException` 类，移除 httpStatusCode 参数：

```csharp
internal sealed class FileStorageException : DomainException
{
    public FileStorageException(string message, string errorCode = "FILE_STORAGE_ERROR")
        : base(message, errorCode) { }
}
```

- [ ] **Step 16: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS（所有子类构造函数已移除 httpStatusCode 参数，但基类 DomainException 仍保留兼容参数，编译通过）

- [ ] **Step 17: 运行全量单元测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 18: 提交**

```bash
git add src/Services/*/Leno.*.Domain/Exceptions/*DomainException.cs src/Services/*/Leno.*.Application/Exceptions/*ValidationException.cs src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs
git commit -m "refactor(M2.1): 11 个 BC XxxDomainException + 4 个额外子类构造函数移除 httpStatusCode 参数"
```

---

## Task 6: 删除 DomainException.HttpStatusCode 字段（基类清理）

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs:7-27`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs:80-101`

**前提**：Task 4-5 已完成，所有子类构造函数与调用方均不再传递 httpStatusCode。

- [ ] **Step 1: 写失败测试**

修改 `src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/GlobalExceptionMiddlewareTests.cs`，新增测试验证 HttpStatusCode 字段已删除：

```csharp
    [Fact]
    public void DomainException_ShouldNotHaveHttpStatusCodeProperty()
    {
        var ex = new TestDomainException("test", "TEST_ERROR");

        var property = typeof(DomainException).GetProperty("HttpStatusCode");

        property.Should().BeNull();
    }
```

修改 `TestDomainException` 内部类为：

```csharp
    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string message, string errorCode = "TEST_ERROR")
            : base(message, errorCode) { }
    }
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GlobalExceptionMiddlewareTests.DomainException_ShouldNotHaveHttpStatusCodeProperty"`
Expected: FAIL（DomainException 仍有 HttpStatusCode 属性）

- [ ] **Step 3: 删除 DomainException.HttpStatusCode 字段**

修改 `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs`，完整替换为：

```csharp
namespace Leno.SharedKernel.Exceptions;

/// <summary>
/// 领域异常基类，仅携带业务错误码与消息。
/// HTTP 状态码映射由 <c>Leno.Infrastructure.Middleware.ErrorCodeMapping</c> 接管，
/// 解除领域层对 HTTP 的依赖。
/// 业务校验失败应抛出继承此类的异常，由全局异常中间件转换为标准响应。
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>业务错误码，便于前端识别与处理。</summary>
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR")
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
```

- [ ] **Step 4: 改造 GlobalExceptionMiddleware.Resolve 移除回退分支**

修改 `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs:80-101`，将 `Resolve` 方法简化为：

```csharp
private (int StatusCode, string Message, LogLevel LogLevel) Resolve(Exception exception)
{
    switch (exception)
    {
        case DomainException domainEx:
            var statusCode = ErrorCodeMapping.GetStatusCode(domainEx.ErrorCode);
            return (statusCode, domainEx.Message, LogLevel.Warning);

        case UnauthorizedAccessException:
            return (StatusCodes.Status401Unauthorized, "未授权", LogLevel.Warning);

        case ArgumentException argEx:
            return (StatusCodes.Status400BadRequest, argEx.Message, LogLevel.Warning);

        default:
            var message = _environment.IsDevelopment()
                ? exception.Message
                : "服务器内部错误";
            return (StatusCodes.Status500InternalServerError, message, LogLevel.Error);
    }
}
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS（所有子类构造函数已不传 httpStatusCode，基类字段删除后编译通过）

- [ ] **Step 6: 运行全量测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 7: spec 验收 grep**

Run: `grep -rn "new.*DomainException.*40[0-9]\|new.*DomainException.*50[0-9]" src/ --include="*.cs"`
Expected: 无输出（spec 验收要求零命中）

Run: `grep -rn "HttpStatusCode" src/BuildingBlocks/Leno.SharedKernel/ --include="*.cs"`
Expected: 无输出（SharedKernel 不再含 HttpStatusCode）

- [ ] **Step 8: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Middleware/GlobalExceptionMiddlewareTests.cs
git commit -m "refactor(M2.1): 删除 DomainException.HttpStatusCode 字段，ErrorCodeMapping 完全接管 HTTP 映射"
```

---

## Task 7: 删除 MoneyJsonConverter.ToStorage/FromStorage 死代码 + 移除 SpecAttribute [JsonConstructor]

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs:66-90`
- Modify: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs:17`

**前提**：探索确认 `ToStorage`/`FromStorage` 在 src/ 中零引用（死代码），Money EF 配置使用 OwnsOne 拆列方式落库，不依赖这两个方法。

- [ ] **Step 1: 删除 MoneyJsonConverter.ToStorage/FromStorage 方法**

修改 `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs`，删除第 66-90 行（`ToStorage` 与 `FromStorage` 静态方法及其 XML 注释）。同时更新类级 XML 注释，移除"提供静态序列化/反序列化方法"描述：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// <see cref="Money"/> 值对象的 System.Text.Json 序列化转换器。
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    private const string AmountName = "amount";
    private const string CurrencyName = "currency";

    public override Money? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Money 必须为 JSON 对象");
        }

        decimal amount = 0m;
        string? currency = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();
            switch (propertyName)
            {
                case AmountName:
                    amount = reader.GetDecimal();
                    break;
                case CurrencyName:
                    currency = reader.GetString();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new JsonException("Money 缺少 currency 字段");
        }

        return Money.Create(amount, currency!);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(AmountName, value.Amount);
        writer.WriteString(CurrencyName, value.Currency);
        writer.WriteEndObject();
    }
}
```

- [ ] **Step 2: 移除 SpecAttribute.cs:17 [JsonConstructor] 标注**

修改 `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs:17-18`，删除 `[JsonConstructor]` 标注及其下的无参私有构造函数（让 JSON 框架走默认路径或带参构造）：

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// 商品规格属性值对象（Name + Value），商品域与购物车域复用。
/// 不可变，通过工厂方法创建。
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "SpecAttribute 为领域统一语言的规格属性值对象，非 System.Attribute 子类。")]
public sealed record SpecAttribute
{
    public string Name { get; init; } = default!;

    public string Value { get; init; } = default!;

    private SpecAttribute() { }

    private SpecAttribute(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public static SpecAttribute Create(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("规格名不可为空", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("规格值不可为空", nameof(value));
        }

        return new SpecAttribute(name.Trim(), value.Trim());
    }

    public override string ToString() => $"{Name}: {Value}";
}
```

同时移除 `using System.Text.Json.Serialization;` 引用（不再使用）。

- [ ] **Step 3: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 4: 验证 ToStorage/FromStorage 零引用**

Run: `grep -rn "MoneyJsonConverter\.\(ToStorage\|FromStorage\)" src/ --include="*.cs"`
Expected: 无输出

Run: `grep -rn "\[JsonConstructor\]" src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs`
Expected: 无输出

- [ ] **Step 5: 运行 Product BC 测试（SpecAttribute 主要使用者）**

Run: `dotnet test src/Services/Product/ --filter "Category!=Integration"`
Expected: PASS（SpecAttribute 反序列化路径变更不影响测试，因 SKUConfiguration 通过 JsonSerializer.Deserialize 仍走无参构造 + init setter）

- [ ] **Step 6: 运行全量单元测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 7: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs
git commit -m "refactor(M2.2): 删除 MoneyJsonConverter.ToStorage/FromStorage 死代码，移除 SpecAttribute [JsonConstructor] 标注"
```

---

## Task 8: 删除 SharedKernel/ValueObjects/PageResult.cs 双定义合并

**Files:**
- Delete: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs`

**前提**：探索确认 SharedKernel 版本 PageResult 零业务引用（18 处 `using Leno.SharedKernel.ValueObjects;` 引用都是为 Money/SpecAttribute，14 处使用 PageResult<T> 的文件全部用 SharedContracts 版本）。

- [ ] **Step 1: 再次验证零引用**

Run: `grep -rn "SharedKernel\.ValueObjects\.PageResult" src/ --include="*.cs"`
Expected: 无输出（仅在 SharedContracts/Responses/PageResult.cs:4 的 XML 注释中提及，无代码引用）

Run: `grep -rn "PageResult<" src/ --include="*.cs" | grep -v "SharedContracts"`
Expected: 仅测试代码或 SharedKernel 自身文件（无业务代码引用 SharedKernel 版本）

- [ ] **Step 2: 删除 SharedKernel/ValueObjects/PageResult.cs**

删除 `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs` 文件。

- [ ] **Step 3: 更新 SharedContracts/Responses/PageResult.cs XML 注释**

修改 `src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs:3-6`，移除"与领域层 PageResult 区分"的过时注释：

```csharp
namespace Leno.SharedContracts.Responses;

/// <summary>
/// 分页响应契约（API 层），承载查询分页数据。
/// 字段为可读可写以适配 JSON 序列化与前端模型绑定。
/// </summary>
public class PageResult<T>
{
    // ... 其余代码保持不变
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 5: 运行全量单元测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 6: spec 验收 grep**

Run: `grep -rn "class PageResult" src/ --include="*.cs"`
Expected: 仅 1 处命中（`src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs`）

- [ ] **Step 7: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs
git rm src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs
git commit -m "refactor(M2.3): 删除 SharedKernel/ValueObjects/PageResult.cs，PageResult 双定义合并为 SharedContracts 单份"
```

---

## Task 9: 全量集成测试与最终验收

**Files:**
- 无新增文件，仅运行验证

- [ ] **Step 1: 运行全量解决方案测试**

Run: `dotnet test Leno.sln`
Expected: PASS（全部单元测试 + 集成测试通过）

- [ ] **Step 2: spec M2.1 验收**

Run: `grep -rn "HttpStatusCode" src/BuildingBlocks/Leno.SharedKernel/ --include="*.cs"`
Expected: 无输出（DomainException 无 HttpStatusCode 字段）

Run: `grep -rnE "new.*DomainException.*40[0-9]|new.*DomainException.*50[0-9]" src/ --include="*.cs"`
Expected: 无输出（spec 验收要求零命中）

- [ ] **Step 3: spec M2.2 验收**

Run: `grep -rn "MoneyJsonConverter\.\(ToStorage\|FromStorage\)" src/ --include="*.cs"`
Expected: 无输出（MoneyJsonConverter 无 ToStorage/FromStorage 方法）

Run: `grep -rn "class MoneyValueConverter" src/BuildingBlocks/Leno.Infrastructure/Persistence/ --include="*.cs"`
Expected: 无输出（M2.2 spec 提到新建 MoneyValueConverter，但探索发现当前 Money 使用 OwnsOne 拆列方式落库，未使用 ValueConverter。M2.2 实际改造仅为删除死代码，不引入 MoneyValueConverter。此验收项调整为：Money EF 配置仍使用 OwnsOne 拆列方式，存储格式保持不变）

Run: `grep -rn "\[JsonConstructor\]" src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs`
Expected: 无输出

- [ ] **Step 4: spec M2.3 验收**

Run: `grep -rn "class PageResult" src/ --include="*.cs"`
Expected: 仅 1 处命中（`src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs`）

- [ ] **Step 5: 最终提交（如有未提交的文档变更）**

Run: `git status`
Expected: nothing to commit, working tree clean（所有变更已在 Task 1-8 中提交）

- [ ] **Step 6: 推送到远程**

```bash
git push origin feat-project-optimization-plan-O7ECNx
```

---

## 自检清单

### spec 覆盖

| spec 章节 | 对应 Task | 状态 |
|-----------|----------|------|
| M2.1 DomainException 移除 HttpStatusCode | Task 1（ErrorCodeMapping）+ Task 2（Middleware 兼容期）+ Task 3（特殊 ErrorCode 注册）+ Task 4（100 处调用改造）+ Task 5（11+4 子类构造函数）+ Task 6（基类字段删除） | ✅ |
| M2.1 ErrorCode 命名约定 DOMAIN_ENTITY_ACTION | Task 1 后缀规则 + Task 3 显式注册 | ✅（沿用现有 SCREAMING_SNAKE_CASE 命名，ErrorCodeMapping 按后缀推断） |
| M2.2 MoneyJsonConverter 存储格式外迁 | Task 7（删除 ToStorage/FromStorage） | ✅（spec 提到新建 MoneyValueConverter，但探索发现当前用 OwnsOne 拆列，无 ValueConverter 需求，仅删死代码） |
| M2.2 SpecAttribute.cs:17 移除 [JsonConstructor] | Task 7 Step 2 | ✅ |
| M2.3 PageResult 双定义合并 | Task 8（删除 SharedKernel 版本） | ✅ |

### 已知 spec 偏差

1. **M2.2 MoneyValueConverter 不新建**：spec 第 583-584 行提到"新建 MoneyValueConverter.cs，各 BC IEntityTypeConfiguration 中 OwnsOne Money 改为 Property(...).HasConversion<MoneyValueConverter>()"。但探索发现当前 Money EF 配置已使用 OwnsOne 拆列方式（Amount + Currency 两列），未使用 ValueConverter。M2.2 实际改造仅为删除 ToStorage/FromStorage 死代码，不引入 MoneyValueConverter，存储格式保持不变。Task 7 已记录此偏差。

2. **ErrorCode 命名约定**：spec 第 567 行提到"DOMAIN_ENTITY_ACTION 格式（如 PRODUCT_NOT_FOUND、ORDER_NOT_OWNED、COUPON_ALREADY_RECEIVED）"。探索发现现有 ErrorCode 已遵循类似约定（SCREAMING_SNAKE_CASE + 实体前缀 + 动作后缀），但实体名不强制（如 `USER_NOT_FOUND` 而非 `USER_ACCOUNT_NOT_FOUND`）。Plan 6 沿用现有命名，ErrorCodeMapping 按后缀推断状态码，不强制重命名 ErrorCode。

3. **SystemAdminDomainException 特殊处理**：SystemAdminDomainException 是唯一无默认 errorCode 的子类（必传）。Task 5 Step 11 改造时保留此特性（errorCode 仍必传），仅移除 httpStatusCode 参数。

4. **M2 范围不含 AntiCorruptionBase**：探索发现 spec M2 章节未提及 AntiCorruptionBase，AntiCorruptionBase 属 M4.1 范围。Plan 6 不涉及防腐层统一基类。

### 类型一致性检查

- `ErrorCodeMapping.GetStatusCode(string?)` 返回 `int`（Task 1）→ `GlobalExceptionMiddleware.Resolve` 使用 `int`（Task 2/6）✅
- `ErrorCodeMapping.Register(string, int)` / `RegisterAll(params (string, int)[])`（Task 1）→ `ServiceCollectionExtensions.RegisterSpecialErrorCodes` 使用 `RegisterAll`（Task 3）✅
- `DomainException.ErrorCode`（string）保持不变（Task 6）✅
- `DomainException.HttpStatusCode`（int）在 Task 6 删除后，`GlobalExceptionMiddleware.Resolve` 不再引用（Task 6 Step 4 已移除回退分支）✅
- 11 个 BC XxxDomainException 子类构造函数签名在 Task 5 统一为 `(string message, string errorCode = "XXX_ERROR")`（含 innerException 重载的 4 个 BC 额外提供 `(string message, Exception innerException, string errorCode)` 重载）✅
- `SpecAttribute` record 在 Task 7 移除 `[JsonConstructor]` 后，JSON 框架走默认无参构造 + init setter，与现有 `JsonSerializer.Deserialize<List<SpecAttribute>>` 兼容 ✅
