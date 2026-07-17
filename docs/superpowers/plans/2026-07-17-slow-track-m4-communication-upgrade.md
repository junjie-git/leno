# 慢轨 M4 通信升级 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新建 `AntiCorruptionBase` 抽象基类统一 9 个 HTTP 防腐层错误处理与埋点，`AddHttpClient<T>` 链上接入 Polly 重试/熔断/Timeout；11 条 internal 路由统一加 `/v1/` 前缀，`IntegrationEventBase` 增加 `SchemaVersion` 字段；新建 11 个 .proto 契约 + buf CLI 校验 + 3 批次 gRPC 服务端/客户端迁移，最终下线全部 HttpClient 防腐层代码

**Architecture:** `AntiCorruptionBase.ExecuteAsync` 模板方法统一 `try/catch` 与 `Metrics.RecordFailure`，写/读操作均 `throwOnFailure=true` 不返回 null；Polly 策略由 `AddLenoApi` 通过 `AddPolicyHandler` 链式注入（重试 3 次指数退避 + 熔断 50%/30s + Timeout 10s），网络故障统一映射 503；gRPC 契约统一放 `Leno.SharedContracts/Protos/`，package `leno.<bc>.v1`，11 个 BC.Api 新增 `GrpcServices/` 复用既有 `IXxxInternalQueryService` 业务逻辑；客户端新建 `GrpcAntiCorruptionClientBase` + 配置开关 `AntiCorruption:UseGrpc` 灰度切换；CI 集成 `buf lint` + `buf breaking` 保证向后兼容

**Tech Stack:** .NET 10、ASP.NET Core 10、Polly v8、Microsoft.Extensions.Http.Polly、Grpc.AspNetCore、Grpc.Net.Client、buf CLI、xUnit、FluentAssertions、Moq

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §11](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 2（F2 安全，Consul KV / GatewayAuthHandler 已就绪）完成；Plan 3（F3 EF Migrations，`MigrateWithLockAsync` 已就绪）完成；Plan 6（M2 共享内核，ErrorCode 命名约定已落地）完成；Plan 7（M3 跨 BC 样板去重，`AddLenoApi`/`UseLenoPipeline` 已就绪）完成

**向后兼容策略:** M4.1 Polly 策略与 `AntiCorruptionBase` 一次性切换（同 BC 内同步改造，无运行期双轨）；M4.2 internal 路由双路由期 1 周（新旧路由并存，验证后下线旧路由）；M4.3 gRPC 通过 `AntiCorruption:UseGrpc` 灰度开关，默认 false，验证 1 周后切 true，全量验证后删除 HttpClient 防腐层代码

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| AntiCorruptionBase（不存在） | — | 需新建于 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs` |
| AntiCorruptionMetrics | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionMetrics.cs` | 仅 Order BC 有，Meter 名 `Leno.Order.AntiCorruption`；Product/Promotion/Points 各防腐层手动调用 13 处（Promotion 7 + Points 6），Product 0 处埋点 |
| 9 个 HTTP 防腐层服务清单 | 见下方完整清单 | Order 3 + Notification 1 + Cart 1 + ReviewAfterSales 3 + Order（Logistics）1，**各自独立实现 try/catch/return null**，无统一基类 |
| Order 防腐层文件 | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs` | **474 行**，含 3 个 sealed class（Product/Promotion/Points）；`ProductAntiCorruptionService` 行 44/52/74 共 3 处 `return null`（P0-3 兜底掩盖网络故障） |
| Notification 防腐层 | `src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs:38` | 单文件单类，无 Polly 策略 |
| Cart 防腐层 | `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs:21` | `BatchEndpoint = "internal/products/skus/batch"` 硬编码 |
| ReviewAfterSales 防腐层 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/` | 3 个文件：`PaymentInfoQueryService.cs`、`AfterSalesEligibilityChecker.cs`、`ReviewEligibilityChecker.cs` |
| AddHttpClient 注册清单 | 7 个 BC `Dependencies/ServiceCollectionExtensions.cs` | 共 18 处 `AddHttpClient<TInterface, TImpl>`；Order 4 + Notification 3 + ReviewAfterSales 3 + Cart 1 + Payment 2 + UserAuth 3 + SystemAdmin 2；**0 处 AddPolicyHandler 调用**（Polly 包悬空） |
| Polly 包引用现状 | Notification + Payment csproj | `Microsoft.Extensions.Http.Polly` 已被引用但未使用；其余 5 BC **未引用** Polly 包 |
| 11 条 internal 路由 | 见下方完整清单 | **全部不带 `/v1/` 前缀**，分布在 7 个 BC 的 7 个控制器 |
| IntegrationEventBase | `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs:1-28` | 含 `EventId`/`OccurredAt`/`IdempotencyKey`，**不含 `SchemaVersion`** 字段 |
| 4 个 IXxxInternalQueryService 接口 | 见下方完整清单 | UserAuth/Product/Payment/Order BC 有此模式；Cart/SellerShop/ReviewAfterSales/Notification/SystemAdmin 用其他模式（直接调 AppService 或仓储） |
| OutboxMessage 持久化 | `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs:22` | `Type` 字段记录事件类型名，需新增 `SchemaVersion` 列 |
| gRPC 基础设施（完全空白） | — | 无 .proto、无 `GrpcServices/` 目录、无 `GrpcAntiCorruptionClientBase`、无 `UseGrpc` 开关、无 `Grpc.AspNetCore` 包引用 |
| buf 工具链（完全空白） | — | 无 `buf.yaml`、无 `buf.gen.yaml`、CI 未集成 buf |

### 9 个 HTTP 防腐层服务完整清单

| # | BC | 服务类 | 文件路径 | 调用方 BC（远程域） |
|---|---|---|---|---|
| 1 | Order | `ProductAntiCorruptionService` | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs:16` | Product |
| 2 | Order | `PromotionAntiCorruptionService` | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs:109` | Promotion |
| 3 | Order | `PointsAntiCorruptionService` | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs:286` | PointsMembership |
| 4 | Order | `LogisticsTrackingService` | `src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs` | 第三方物流 |
| 5 | Notification | `UserContactAntiCorruptionService` | `src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs` | UserAuth |
| 6 | Cart | `CartPriceService` | `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs` | Product |
| 7 | ReviewAfterSales | `PaymentInfoQueryService` | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs` | Payment |
| 8 | ReviewAfterSales | `AfterSalesEligibilityChecker` | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs` | Order |
| 9 | ReviewAfterSales | `ReviewEligibilityChecker` | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs` | Order |

### 18 处 AddHttpClient 注册清单

| BC | 文件路径 | 行号 | 注册 |
|---|---|---|---|
| Order | `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | 59 | `AddHttpClient<IProductAntiCorruptionService, ProductAntiCorruptionService>` |
| Order | 同上 | 60 | `AddHttpClient<IPromotionAntiCorruptionService, PromotionAntiCorruptionService>` |
| Order | 同上 | 61 | `AddHttpClient<IPointsAntiCorruptionService, PointsAntiCorruptionService>` |
| Order | 同上 | 71 | `AddHttpClient<ILogisticsTrackingService, LogisticsTrackingService>` |
| Notification | `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | (3 处) | 3 个防腐层 HttpClient |
| ReviewAfterSales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | (3 处) | 3 个防腐层 HttpClient |
| Cart | `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | (1 处) | `AddHttpClient<CartPriceService>` |
| Payment | `src/Services/Payment/Leno.Payment.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | (2 处) | 2 个 HttpClient |
| UserAuth | `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | (3 处) | 3 个 HttpClient |
| SystemAdmin | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | (2 处) | 2 个 HttpClient |

### 11 条 internal 路由完整清单

| # | BC | 控制器文件 | 路由（当前） | 路由（目标） |
|---|---|---|---|---|
| 1 | Product | `src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs` | `internal/products/skus/{skuId}` + `internal/products/skus/batch` | `internal/v1/products/skus/{skuId}` + `internal/v1/products/skus/batch` |
| 2 | Promotion | `src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs` | `internal/promotions/calculate` + `internal/promotions/lock-coupon` + `internal/promotions/release-coupons` | 加 `/v1/` 前缀 |
| 3 | PointsMembership | `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs` | `internal/points/trial-offset` + `internal/points/freeze` + `internal/points/release` | 加 `/v1/` 前缀 |
| 4 | UserAuth | `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs:22` | `internal/users/{userId}/contacts` | `internal/v1/users/{userId}/contacts` |
| 5 | Order | `src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs:22` | `internal/orders/{orderId}/status` | `internal/v1/orders/{orderId}/status` |
| 6 | Payment | `src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs:23` | `internal/payments/{orderId}/info` | `internal/v1/payments/{orderId}/info` |
| 7 | Notification | `src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs:27` | `internal/notifications/send` | `internal/v1/notifications/send` |

### 4 个 IXxxInternalQueryService 接口位置

| BC | 接口 | 文件路径 |
|---|---|---|
| UserAuth | `IUserInternalQueryService` | `src/Services/UserAuth/Leno.UserAuth.Application/IUserInternalQueryService.cs:6` |
| Product | `IProductInternalQueryService` | `src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs:6` |
| Payment | `IPaymentInternalQueryService` | `src/Services/Payment/Leno.Payment.Application/IPaymentInternalQueryService.cs:6` |
| Order | `IOrderInternalQueryService` | `src/Services/Order/Leno.Order.Application/IOrderInternalQueryService.cs:6` |

### 11 个 BC gRPC 端口分配

| BC | HTTP 端口 | gRPC 端口（HTTP+100） |
|---|---|---|
| UserAuth | 5151 | 5251 |
| Product | 5152 | 5252 |
| Cart | 5153 | 5253 |
| Order | 5154 | 5254 |
| Promotion | 5155 | 5255 |
| ReviewAfterSales | 5156 | 5256 |
| PointsMembership | 5157 | 5257 |
| Payment | 5158 | 5258 |
| Notification | 5159 | 5259 |
| SellerShop | 5160 | 5260 |
| SystemAdmin | 5161 | 5261 |

---

## Task 1: 新建 AntiCorruptionBase 抽象基类

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionBaseTests.cs`

- [ ] **Step 1: 创建通用 AntiCorruptionMetrics 类（提升到 Leno.Infrastructure）**

创建 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs`：

```csharp
using System.Diagnostics.Metrics;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层可观测性指标（M4.1）。
/// 由所有 BC 共享，Meter 名 <c>Leno.&lt;BC&gt;.AntiCorruption</c> 通过 <see cref="CreateMeterName"/> 生成。
/// 各 BC 启动时通过 <c>AddLenoOpenTelemetry</c> 回调 <c>.AddMeter(AntiCorruptionMetrics.MeterName)</c> 订阅。
/// </summary>
public static class AntiCorruptionMetrics
{
    /// <summary>统一 Meter 名称前缀，OTel SDK 须通过 <c>AddMeter(AntiCorruptionMetrics.GetMeterName(bc))</c> 订阅。</summary>
    public const string MeterNamePrefix = "Leno.";

    /// <summary>防腐层服务标识标签名。</summary>
    public const string ServiceLabel = "service";

    /// <summary>防腐层操作标识标签名。</summary>
    public const string OperationLabel = "operation";

    /// <summary>防腐层远程失败计数器名（统一 Prometheus 指标名）。</summary>
    public const string FailureCounterName = "anticorruption_failure_total";

    private static readonly Meter _meter = new("Leno.AntiCorruption", "1.0.0");

    /// <summary>统一 Meter 实例（各 BC 共享）。</summary>
    public static Meter Meter => _meter;

    /// <summary>防腐层远程失败计数器，标签 <c>service</c> + <c>operation</c>。</summary>
    public static Counter<int> FailureCounter { get; } =
        _meter.CreateCounter<int>(
            FailureCounterName,
            unit: "times",
            description: "防腐层远程调用失败次数（按 service/operation 维度统计）");

    /// <summary>按 BC 名生成 Meter 订阅名（如 <c>Leno.Order.AntiCorruption</c>）。</summary>
    public static string GetMeterName(string bcName)
        => $"{MeterNamePrefix}{bcName}.AntiCorruption";

    /// <summary>记录一次防腐层远程失败，按 service/operation 维度递增计数器。</summary>
    /// <param name="service">防腐层服务标识（如 <c>points</c>、<c>promotion</c>）。</param>
    /// <param name="operation">操作标识（如 <c>freeze</c>、<c>calculate_discount</c>）。</param>
    public static void RecordFailure(string service, string operation)
    {
        if (string.IsNullOrEmpty(service) || string.IsNullOrEmpty(operation))
        {
            return;
        }

        FailureCounter.Add(1, new KeyValuePair<string, object?>(ServiceLabel, service),
                              new KeyValuePair<string, object?>(OperationLabel, operation));
    }
}
```

- [ ] **Step 2: 创建 AntiCorruptionBase 抽象基类**

创建 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs`：

```csharp
using System.Net;
using System.Net.Http;
using Leno.SharedKernel.Exceptions;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层抽象基类（M4.1）。
/// 统一 <see cref="ExecuteAsync"/> 模板方法：异常捕获、指标埋点、HTTP 状态码映射。
/// 写操作与读操作均 <c>throwOnFailure=true</c>，不再返回 null（spec M4.1）。
/// 网络故障统一映射 HTTP 503 + ErrorCode <c>{SERVICE}_UNAVAILABLE</c>。
/// </summary>
public abstract class AntiCorruptionBase
{
    /// <summary>防腐层服务标识（如 <c>product</c>、<c>promotion</c>、<c>points</c>），用于指标埋点。</summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// 执行远程调用并统一处理异常与埋点。
    /// 网络故障（HttpRequestException/TaskCanceledException）抛 <c>{SERVICE}_UNAVAILABLE</c> 异常，
    /// 远程非 2xx 状态码抛 <c>{SERVICE}_REMOTE_FAILED</c> 异常。
    /// </summary>
    /// <typeparam name="T">返回类型。</typeparam>
    /// <param name="operation">操作标识（如 <c>get_sku_info</c>），用于指标埋点。</param>
    /// <param name="execute">实际远程调用委托。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>远程调用返回值（不返回 null）。</returns>
    protected async Task<T> ExecuteAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken ct = default)
    {
        try
        {
            return await execute(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE",
                $"防腐层调用 {ServiceName}/{operation} 超时：{ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE",
                $"防腐层调用 {ServiceName}/{operation} 网络故障：{ex.Message}");
        }
        catch (DomainException)
        {
            // 业务异常透传，不重复埋点
            throw;
        }
        catch (Exception ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED",
                $"防腐层调用 {ServiceName}/{operation} 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 执行无返回值的远程调用（写操作）。
    /// 错误处理策略与 <see cref="ExecuteAsync{T}"/> 一致。
    /// </summary>
    protected async Task ExecuteAsync(
        string operation,
        Func<CancellationToken, Task> execute,
        CancellationToken ct = default)
    {
        await ExecuteAsync<object?>(operation, async token =>
        {
            await execute(token).ConfigureAwait(false);
            return null;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验远程响应状态码，非 2xx 抛 <c>{SERVICE}_REMOTE_FAILED</c> 异常。
    /// </summary>
    protected void EnsureSuccessStatusCode(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED",
                $"防腐层调用 {ServiceName}/{operation} 返回非成功状态码 {(int)response.StatusCode} ({response.StatusCode})");
        }
    }
}
```

- [ ] **Step 3: 创建单元测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionBaseTests.cs`：

```csharp
using System.Net;
using System.Net.Http;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Exceptions;
using FluentAssertions;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class AntiCorruptionBaseTests
{
    private sealed class TestAntiCorruption : AntiCorruptionBase
    {
        protected override string ServiceName => "test_service";

        public Task<T> RunExecuteAsync<T>(string op, Func<CancellationToken, Task<T>> fn, CancellationToken ct = default)
            => ExecuteAsync(op, fn, ct);

        public void RunEnsureSuccess(HttpResponseMessage resp, string op) => EnsureSuccessStatusCode(resp, op);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsValue()
    {
        var svc = new TestAntiCorruption();
        var result = await svc.RunExecuteAsync("op", _ => Task.FromResult(42));
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_ThrowsUnavailable()
    {
        var svc = new TestAntiCorruption();
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw new HttpRequestException("connection refused"));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ThrowsUnavailable()
    {
        var svc = new TestAntiCorruption();
        using var cts = new CancellationTokenSource();
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw new OperationCanceledException("timeout"));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task ExecuteAsync_GenericException_ThrowsRemoteFailed()
    {
        var svc = new TestAntiCorruption();
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw new InvalidOperationException("boom"));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
    }

    [Fact]
    public async Task ExecuteAsync_DomainException_Passthrough()
    {
        var svc = new TestAntiCorruption();
        var domainEx = new DomainException("TEST_SERVICE_BUSINESS_ERROR", "biz");
        var act = () => svc.RunExecuteAsync<int>("op", _ => throw domainEx);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_BUSINESS_ERROR");
    }

    [Fact]
    public void EnsureSuccessStatusCode_NonSuccess_ThrowsRemoteFailed()
    {
        var svc = new TestAntiCorruption();
        using var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var act = () => svc.RunEnsureSuccess(resp, "op");

        var ex = act.Should().Throw<DomainException>();
        ex.Which.ErrorCode.Should().Be("TEST_SERVICE_REMOTE_FAILED");
    }

    [Fact]
    public void EnsureSuccessStatusCode_Success_DoesNotThrow()
    {
        var svc = new TestAntiCorruption();
        using var resp = new HttpResponseMessage(HttpStatusCode.OK);
        var act = () => svc.RunEnsureSuccess(resp, "op");
        act.Should().NotThrow();
    }
}
```

> **说明：** `DomainException` 构造函数签名在 Plan 6（M2.1）完成后应为 `(string errorCode, string message)`，移除 `httpStatusCode` 参数。若 Plan 6 尚未完成，临时使用既有签名 `(httpStatusCode, errorCode, message)` 传入 `503`，并在 Plan 6 完成后统一调整。

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AntiCorruptionBaseTests"`
Expected: PASS（7 个测试全部通过）

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionBaseTests.cs
git commit -m "feat(M4.1): 新建 AntiCorruptionBase 抽象基类与通用 AntiCorruptionMetrics"
```

---

## Task 2: 新建 Polly 策略扩展方法 + 集成到 AddLenoApi

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`（引用 Polly 包）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`（AddLenoApi 内调用 AddLenoAntiCorruptionPolly）

- [ ] **Step 1: 在 Leno.Infrastructure.csproj 引用 Polly 包**

修改 `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`，在 `<ItemGroup>` 中增加：

```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
<PackageReference Include="Polly" Version="8.4.1" />
```

> **说明：** Notification/Payment 已有 `Microsoft.Extensions.Http.Polly` 引用，本次统一在 Leno.Infrastructure 引用后，各 BC 通过传递依赖获取。

- [ ] **Step 2: 创建 AddLenoAntiCorruptionPolly 扩展方法**

创建 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层 HttpClient Polly 策略扩展（M4.1）。
/// 统一注入：重试 3 次（指数退避 1s/2s/4s）+ 熔断（失败率 50% 断 30s）+ Timeout 10s。
/// 网络故障（HttpRequestException/TaskCanceledException）触发重试与熔断计数。
/// </summary>
public static class AntiCorruptionPollyExtensions
{
    /// <summary>配置节名（<c>AntiCorruption:Polly</c>）。</summary>
    public const string SectionName = "AntiCorruption:Polly";

    /// <summary>
    /// 为所有防腐层 HttpClient 注入统一 Polly 策略。
    /// 调用方：在 <c>AddLenoApi</c> 内部对每个 <c>AddHttpClient&lt;T&gt;</c> 链式追加 <c>AddPolicyHandler(...)</c>。
    /// </summary>
    /// <param name="services">DI 容器。</param>
    /// <param name="configuration">配置（读取 <c>AntiCorruption:Polly</c>）。</param>
    /// <returns>DI 容器（链式调用）。</returns>
    public static IServiceCollection AddLenoAntiCorruptionPolly(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var retryCount = section?.GetValue("RetryCount", 3) ?? 3;
        var circuitBreakerFailureThreshold = section?.GetValue("CircuitBreakerFailureThreshold", 0.5) ?? 0.5;
        var circuitBreakerDurationSeconds = section?.GetValue("CircuitBreakerDurationSeconds", 30) ?? 30;
        var timeoutSeconds = section?.GetValue("TimeoutSeconds", 10) ?? 10;

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, delay, attempt, ctx) =>
                {
                    // 重试日志由 ILogger 在 AddHttpClient 的 RemoveAllLoggers 后自定义，此处不重复
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 10,
                durationOfBreak: TimeSpan.FromSeconds(circuitBreakerDurationSeconds),
                onBreak: (outcome, breakDelay) => { },
                onReset: () => { },
                onHalfOpen: () => { });

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            timeoutSeconds,
            TimeoutStrategy.Pessimistic,
            onTimeoutAsync: (ctx, span, task) => Task.CompletedTask);

        // 注册到 DI，供 AddHttpClient<T> 链式调用 AddPolicyHandler(...) 时按名解析
        services.AddKeyedSingleton("AntiCorruptionRetry", retryPolicy);
        services.AddKeyedSingleton("AntiCorruptionCircuitBreaker", circuitBreakerPolicy);
        services.AddKeyedSingleton("AntiCorruptionTimeout", timeoutPolicy);

        return services;
    }

    /// <summary>
    /// 获取防腐层 Polly 策略组合（供 <c>AddHttpClient&lt;T&gt;</c> 链式追加 <c>AddPolicyHandler</c>）。
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage>[] GetAntiCorruptionPolicies(
        IServiceProvider services)
    {
        var retry = services.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionRetry");
        var circuit = services.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionCircuitBreaker");
        var timeout = services.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionTimeout");
        return [retry, circuit, timeout];
    }
}
```

- [ ] **Step 3: 在 AddLenoApi 中调用 AddLenoAntiCorruptionPolly**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`，在 `AddLenoApi<TDbContext>` 方法体内（Plan 7 已创建）增加调用：

```csharp
// 在 AddLenoApi<TDbContext> 方法体开头区域增加：
services.AddLenoAntiCorruptionPolly(configuration);
```

并在 `configureInfrastructure` 委托约定文档中注明：BC 专属 `AddHttpClient<TInterface, TImpl>(c => ...)` 后必须追加 `.AddAntiCorruptionPolicies(services)` 链式调用（见 Step 4 工具方法）。

- [ ] **Step 4: 新增 AddAntiCorruptionPolicies 工具方法**

在 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs` 文件末尾增加：

```csharp
/// <summary>
/// 链式追加防腐层 Polly 策略到 <see cref="IHttpClientBuilder"/>。
/// 各 BC 在 <c>AddHttpClient&lt;TInterface, TImpl&gt;(...).AddAntiCorruptionPolicies()</c> 调用。
/// </summary>
public static IHttpClientBuilder AddAntiCorruptionPolicies(this IHttpClientBuilder builder)
{
    builder.AddPolicyHandler((sp, _) =>
        sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionRetry"));
    builder.AddPolicyHandler((sp, _) =>
        sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionCircuitBreaker"));
    builder.AddPolicyHandler((sp, _) =>
        sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionTimeout"));
    return builder;
}
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
Expected: BUILD SUCCESS（无错误）

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs
git commit -m "feat(M4.1): 新建 AntiCorruptionPollyExtensions 与 AddLenoAntiCorruptionPolly 策略统一注入"
```

---

## Task 3: 改造 9 个 HTTP 防腐层服务继承 AntiCorruptionBase

**Files:**
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs`（3 个 sealed class）
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs`
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs`
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs`
- Modify: 上述 4 个 BC 的 `Dependencies/ServiceCollectionExtensions.cs`（`AddHttpClient<T>` 链上追加 `.AddAntiCorruptionPolicies()`）
- Delete: `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionMetrics.cs`（已提升到 Leno.Infrastructure，使用全限定名 `Leno.Infrastructure.AntiCorruption.AntiCorruptionMetrics`）

- [ ] **Step 1: 改造 Order ProductAntiCorruptionService（移除 3 处 return null）**

修改 `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs`，将 `ProductAntiCorruptionService` 改造为继承 `AntiCorruptionBase`，所有方法改为通过 `ExecuteAsync` 模板调用，移除 3 处 `return null`：

```csharp
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedKernel.Exceptions;

namespace Leno.Order.Infrastructure.Services;

public sealed class ProductAntiCorruptionService : IProductAntiCorruptionService, AntiCorruptionBase
{
    private readonly HttpClient _httpClient;

    protected override string ServiceName => "product";

    public ProductAntiCorruptionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<SkuInfo?> GetSkuInfoAsync(long skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_info", async token =>
        {
            using var response = await _httpClient.GetAsync($"internal/v1/products/skus/{skuId}", token);
            EnsureSuccessStatusCode(response, "get_sku_info");
            var sku = await response.Content.ReadFromJsonAsync<SkuInfo>(cancellationToken: token);
            // 不再返回 null：若反序列化为 null 视为远程协议错误
            return sku ?? throw new DomainException("PRODUCT_REMOTE_FAILED", "Product 域返回空 SKU 信息");
        }, ct);
}
```

> **说明：** `SkuInfo?` 返回类型保留可空标注仅为 API 契约兼容，实际不再返回 null。M4.3 gRPC 迁移后改为非空 `SkuInfo`。

- [ ] **Step 2: 改造 Order PromotionAntiCorruptionService + PointsAntiCorruptionService**

在同文件中改造另外 2 个 sealed class，模板与 Step 1 一致：
- `PromotionAntiCorruptionService`：`ServiceName = "promotion"`，5 个方法（CalculateDiscountAsync/LockCouponAsync/ReleaseCouponsAsync 等）改用 `ExecuteAsync`
- `PointsAntiCorruptionService`：`ServiceName = "points"`，5 个方法（TrialOffsetAsync/FreezeAsync/ReleaseAsync/ConfirmAsync 等）改用 `ExecuteAsync`

所有 `AntiCorruptionMetrics.RecordFailure("xxx", "yyy")` 手动调用删除（基类 `ExecuteAsync` 已统一埋点）。

- [ ] **Step 3: 删除 Order 本地 AntiCorruptionMetrics.cs**

删除 `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionMetrics.cs`，并将 Order.Infrastructure 内所有引用改为 `Leno.Infrastructure.AntiCorruption.AntiCorruptionMetrics`（全限定名或 using）。

更新 `src/Services/Order/Leno.Order.Infrastructure.Tests/AntiCorruptionMetricsTests.cs`：测试改为引用 `Leno.Infrastructure.AntiCorruption.AntiCorruptionMetrics`，断言 `Meter.Name == "Leno.AntiCorruption"`。

- [ ] **Step 4: 改造 Notification UserContactAntiCorruptionService**

修改 `src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs`，继承 `AntiCorruptionBase`，`ServiceName = "user_contact"`，方法体改用 `ExecuteAsync`。

- [ ] **Step 5: 改造 Cart CartPriceService**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs`，继承 `AntiCorruptionBase`，`ServiceName = "product"`（远程调用 Product 域），方法体改用 `ExecuteAsync`。`BatchEndpoint` 常量值改为 `"internal/v1/products/skus/batch"`。

- [ ] **Step 6: 改造 ReviewAfterSales 3 个防腐层服务**

修改以下 3 个文件，全部继承 `AntiCorruptionBase`：
- `PaymentInfoQueryService.cs`：`ServiceName = "payment"`，`internal/payments/{orderId}/info` → `internal/v1/payments/{orderId}/info`
- `AfterSalesEligibilityChecker.cs`：`ServiceName = "order"`，`internal/orders/{orderId}/status` → `internal/v1/orders/{orderId}/status`
- `ReviewEligibilityChecker.cs`：`ServiceName = "order"`，`internal/orders/{orderId}/status` → `internal/v1/orders/{orderId}/status`

- [ ] **Step 7: 改造 Order LogisticsTrackingService（第三方物流）**

修改 `src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs`，继承 `AntiCorruptionBase`，`ServiceName = "logistics"`。第三方物流接口若允许降级（无物流轨迹时返回空），可保留 `ExecuteAsync` 内部 catch 后返回空集合，但不再返回 null。

- [ ] **Step 8: 改造 4 个 BC ServiceCollectionExtensions.cs，AddHttpClient 链上追加 .AddAntiCorruptionPolicies()**

修改以下 4 个 BC 的 `Dependencies/ServiceCollectionExtensions.cs` 中所有 `AddHttpClient<TInterface, TImpl>(c => ...)` 调用，链式追加 `.AddAntiCorruptionPolicies()`：

| BC | 文件路径 |
|---|---|
| Order | `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（4 处） |
| Notification | `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（3 处） |
| Cart | `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（1 处） |
| ReviewAfterSales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（3 处） |

样例（Order）：

```csharp
services.AddHttpClient<IProductAntiCorruptionService, ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl))
    .AddAntiCorruptionPolicies();
services.AddHttpClient<IPromotionAntiCorruptionService, PromotionAntiCorruptionService>(c => c.BaseAddress = new Uri(promotionApiUrl))
    .AddAntiCorruptionPolicies();
services.AddHttpClient<IPointsAntiCorruptionService, PointsAntiCorruptionService>(c => c.BaseAddress = new Uri(pointsApiUrl))
    .AddAntiCorruptionPolicies();
services.AddHttpClient<Domain.Services.ILogisticsTrackingService, LogisticsTrackingService>()
    .AddAntiCorruptionPolicies();
```

> **说明：** Payment/UserAuth/SystemAdmin 3 个 BC 的 HttpClient 注册（共 7 处）若为非防腐层用途（如第三方支付回调查询），可暂不追加 Polly；若用于跨 BC 调用，同样追加。实施时按代码实际情况判定，目标：所有 `AddHttpClient<T>` 用于跨 BC 调用的都追加 `.AddAntiCorruptionPolicies()`。

- [ ] **Step 9: 编译并运行相关单元测试**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

Run: `dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/ src/Services/Notification/Leno.Notification.Infrastructure.Tests/ src/Services/Cart/Leno.Cart.Infrastructure.Tests/ src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/`
Expected: 全部 PASS（既有测试无回归）

- [ ] **Step 10: 提交**

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs src/Services/Order/Leno.Order.Infrastructure.Tests/AntiCorruptionMetricsTests.cs src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git rm src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionMetrics.cs
git commit -m "refactor(M4.1): 9 个 HTTP 防腐层服务继承 AntiCorruptionBase，统一错误处理与埋点，移除 ProductAntiCorruptionService 3 处 return null"
```

---

## Task 4: 11 条 internal 路由加 /v1/ 前缀（双路由期）

**Files:**
- Modify: 7 个 BC 的 7 个 `Internal*Controller.cs`（见路由清单）
- Modify: 9 个防腐层服务中的 URL 路径字符串（Task 3 已同步更新，本任务校验）
- Modify: 7 个 BC 的 `appsettings.json` 中 InternalApiKey RoutePrefix 保持 `internal/`（不变）

- [ ] **Step 1: 在 7 个控制器上同时注册新旧路由（双路由期）**

修改 7 个 `Internal*Controller.cs`，对每个 `[HttpGet]/[HttpPost]` 路由特性追加 `/v1/` 版本，同时保留旧路由（带 `[Obsolete]` 标注，1 周后下线）。

样例（`InternalProductsController.cs`）：

```csharp
[HttpGet("internal/v1/products/skus/{skuId:long}")]
[HttpGet("internal/products/skus/{skuId:long}")] // 旧路由，双路由期保留，下线后删除
public async Task<IActionResult> GetSkuInfo(long skuId, CancellationToken ct) { ... }

[HttpPost("internal/v1/products/skus/batch")]
[HttpPost("internal/products/skus/batch")] // 旧路由，双路由期保留
public async Task<IActionResult> BatchGetSkuInfo([FromBody] BatchSkuRequest req, CancellationToken ct) { ... }
```

对 7 个控制器的 11 条路由全部执行同样操作。

- [ ] **Step 2: 同步防腐层调用方 URL（Task 3 已更新，本步骤为校验）**

校验以下 9 个防腐层服务中所有 URL 字符串已使用 `/v1/` 前缀：

```bash
# 验证命令
rg "internal/v1/" src/Services/Order/Leno.Order.Infrastructure/Services/ src/Services/Notification/Leno.Notification.Infrastructure/Services/ src/Services/Cart/Leno.Cart.Infrastructure/Services/ src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/
# 期望：所有 internal 调用 URL 均为 internal/v1/...
```

- [ ] **Step 3: 新增/更新集成测试覆盖 v1 路由**

为 7 个 BC 的 `Api.Tests` 项目新增测试，验证 `internal/v1/...` 与 `internal/...` 两个路由均可访问：

```csharp
// 样例：Leno.Product.Api.Tests/InternalProductsControllerTests.cs
[Theory]
[InlineData("/internal/v1/products/skus/123")]
[InlineData("/internal/products/skus/123")] // 双路由期，旧路由仍可用
public async Task GetSkuInfo_BothRoutes_ReturnSuccess(string route)
{
    // ... 调用 route 并断言 200
}
```

- [ ] **Step 4: 全量测试验证**

Run: `dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 5: 提交**

```bash
git add src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs src/Services/Product/Leno.Product.Api.Tests/InternalProductsControllerTests.cs src/Services/Promotion/Leno.Promotion.Api.Tests/InternalPromotionsControllerTests.cs
git commit -m "feat(M4.2): 11 条 internal 路由加 /v1/ 前缀，双路由期保留旧路由"
```

- [ ] **Step 6: 双路由期 1 周后下线旧路由（独立提交）**

1 周验证后，删除 7 个控制器中所有不带 `/v1/` 的旧路由特性，并删除对应的双路由测试用例。

```bash
git commit -m "refactor(M4.2): 双路由期结束，下线 11 条不带 /v1/ 的旧 internal 路由"
```

---

## Task 5: IntegrationEventBase 增加 SchemaVersion 字段

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs`（新增 SchemaVersion 列）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessageConfiguration.cs`（Plan 7 已上移到 Leno.Infrastructure）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs`（持久化 SchemaVersion）
- Create: EF Core Migration（Plan 3 已建 `MigrateWithLockAsync` 机制）

- [ ] **Step 1: IntegrationEventBase 增加 SchemaVersion 字段**

修改 `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs`：

```csharp
namespace Leno.SharedContracts.Events;

public abstract class IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; init; }

    public DateTime OccurredAt { get; init; }

    public string IdempotencyKey { get; init; }

    /// <summary>
    /// 事件模式版本号（M4.2）。
    /// 默认 1，事件字段变更时递增；消费者可按 SchemaVersion 路由不同 handler。
    /// Outbox 持久化此字段，跨 BC 消费方据此判断是否需升级反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    protected IntegrationEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        IdempotencyKey = EventId.ToString();
    }

    protected IntegrationEventBase(Guid? eventId, DateTime? occurredAt, string? idempotencyKey, int schemaVersion = 1)
    {
        EventId = eventId ?? Guid.NewGuid();
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? EventId.ToString() : idempotencyKey!;
        SchemaVersion = schemaVersion < 1 ? 1 : schemaVersion;
    }
}
```

- [ ] **Step 2: OutboxMessage 新增 SchemaVersion 列**

修改 `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs`，新增属性：

```csharp
/// <summary>事件模式版本号（M4.2），从 IntegrationEventBase.SchemaVersion 复制。</summary>
public int SchemaVersion { get; private set; }

// 在 Create 工厂方法中接收 schemaVersion 参数：
public static OutboxMessage Create(IIntegrationEvent integrationEvent, string? traceId = null)
{
    // 既有逻辑...
    var message = new OutboxMessage
    {
        // 既有字段...
        SchemaVersion = integrationEvent is IntegrationEventBase baseEvt ? baseEvt.SchemaVersion : 1
    };
    // 既有逻辑...
    return message;
}
```

- [ ] **Step 3: OutboxMessageConfiguration 新增列映射**

修改 `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessageConfiguration.cs`（Plan 7 已上移到 Leno.Infrastructure），新增：

```csharp
builder.Property(o => o.SchemaVersion)
    .HasColumnName("SchemaVersion")
    .HasDefaultValue(1)
    .IsRequired();
```

- [ ] **Step 4: OutboxPublisher 持久化 SchemaVersion**

修改 `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs`，发布时在消息头携带 `schema-version`：

```csharp
// 在 SendAsync 时追加 header
var publishHeaders = new Dictionary<string, object?>
{
    ["schema-version"] = outboxMessage.SchemaVersion.ToString()
};
await _publishEndpoint.Publish(message, context =>
{
    foreach (var kv in publishHeaders)
    {
        context.Headers.Set(kv.Key, kv.Value);
    }
}, ct);
```

- [ ] **Step 5: 消费者读取 schema-version header（可选，按需）**

在各 BC 消费者基类 `IntegrationEventConsumerBase` 中（若已存在）增加读取 `schema-version` header 的能力，并在消费日志中打印版本号。本步骤不强制按版本路由，仅记录，后续版本演进时再启用路由。

- [ ] **Step 6: 生成 EF Core Migration**

为每个 BC 生成 migration（Plan 3 已建机制）：

```bash
dotnet ef migrations add AddOutboxSchemaVersion `
  --project src/Services/Order/Leno.Order.Infrastructure `
  --startup-project src/Services/Order/Leno.Order.Api `
  --output-dir Migrations
```

对 11 个 BC 逐一执行（脚本化批量执行）。

- [ ] **Step 7: 全量测试验证**

Run: `dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 8: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessageConfiguration.cs src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs
# 各 BC Migrations 目录新增文件
git add src/Services/*/Leno.*.Infrastructure/Migrations/*
git commit -m "feat(M4.2): IntegrationEventBase 增加 SchemaVersion 字段，Outbox 持久化版本号"
```

---

## Task 6: 新建 11 个 .proto 契约 + buf 工具链

**Files:**
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/product.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/promotion.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/points.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/user.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/payment.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/cart.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/seller.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/review.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/notification.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Protos/system.proto`
- Create: `src/BuildingBlocks/Leno.SharedContracts/buf.yaml`
- Create: `src/BuildingBlocks/Leno.SharedContracts/buf.gen.yaml`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj`（引用 Grpc.AspNetCore 包 + Protobuf 文件）
- Modify: `.github/workflows/ci.yml`（CI 集成 buf lint + buf breaking）

- [ ] **Step 1: 创建 buf.yaml 配置**

创建 `src/BuildingBlocks/Leno.SharedContracts/buf.yaml`：

```yaml
version: v2
modules:
  - path: .
lint:
  use:
    - STANDARD
  except:
    - PACKAGE_VERSION_SUFFIX
breaking:
  use:
    - FILE
  except:
    - EXTENSION_NO_DELETE
```

- [ ] **Step 2: 创建 buf.gen.yaml 配置**

创建 `src/BuildingBlocks/Leno.SharedContracts/buf.gen.yaml`：

```yaml
version: v2
managed:
  enabled: true
  override:
    CSHARP_FILE_SCOPED_NAMESPACE: true
plugins:
  - remote: buf.build/grpc/csharp
    out: ../Leno.SharedContracts.Grpc/Generated
  - remote: buf.build/protocolbuffers/csharp
    out: ../Leno.SharedContracts.Grpc/Generated
```

> **说明：** 新建 `Leno.SharedContracts.Grpc` 项目承载生成的 C# 代码，避免污染 `Leno.SharedContracts` 既有契约。

- [ ] **Step 3: 新建 Leno.SharedContracts.Grpc 项目**

创建 `src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.Core" Version="2.46.*" />
    <PackageReference Include="Google.Protobuf" Version="3.27.0" />
    <PackageReference Include="Grpc.Net.Client" Version="2.63.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: 创建 11 个 .proto 文件**

按 spec §11.3 表格逐个创建 .proto 文件。每个文件 package 为 `leno.<bc>.v1`，service 命名 `XxxInternalService`，message 命名遵循 C# PascalCase。

样例（`product.proto`）：

```protobuf
syntax = "proto3";

package leno.product.v1;

option csharp_namespace = "Leno.SharedContracts.Grpc.Product.V1";

service ProductInternalService {
  rpc GetSkuInfo(GetSkuInfoRequest) returns (SkuInfo);
  rpc BatchGetSkuInfo(BatchGetSkuInfoRequest) returns (BatchGetSkuInfoResponse);
  rpc GetSkuStock(GetSkuStockRequest) returns (SkuStock);
  rpc GetProductDetail(GetProductDetailRequest) returns (ProductDetail);
}

message GetSkuInfoRequest {
  int64 sku_id = 1;
}

message SkuInfo {
  int64 sku_id = 1;
  int64 spu_id = 2;
  string title = 3;
  string main_image = 4;
  int64 price_cents = 5;
  string currency = 6;
  bool salable = 7;
  int64 seller_id = 8;
  int32 stock = 9;
}

message BatchGetSkuInfoRequest {
  repeated int64 sku_ids = 1;
}

message BatchGetSkuInfoResponse {
  repeated SkuInfo skus = 1;
}

message GetSkuStockRequest {
  int64 sku_id = 1;
}

message SkuStock {
  int64 sku_id = 1;
  int32 available = 2;
  int32 reserved = 3;
}

message GetProductDetailRequest {
  int64 spu_id = 1;
}

message ProductDetail {
  int64 spu_id = 1;
  string title = 2;
  string description = 3;
  int64 seller_id = 4;
  repeated SkuInfo skus = 5;
}
```

按 spec 表格完成其余 10 个 .proto 文件。各 .proto 服务与方法清单：

| .proto | 服务 | 方法 |
|---|---|---|
| product.proto | ProductInternalService | GetSkuInfo, BatchGetSkuInfo, GetSkuStock, GetProductDetail |
| promotion.proto | PromotionInternalService | CalculateDiscount, LockCoupon, ReleaseCoupons, GetCouponInfo |
| points.proto | PointsInternalService | TrialOffset, Freeze, Release, Confirm, GetPointsBalance |
| user.proto | UserInternalService | GetUserContacts, GetUserInfo, GetUserAddresses |
| order.proto | OrderInternalService | GetOrderStatus, GetOrderDetail, GetSellerOrders |
| payment.proto | PaymentInternalService | GetPaymentInfo, GetRefundStatus |
| cart.proto | CartInternalService | GetCartSnapshot, GetCheckoutPreview |
| seller.proto | SellerInternalService | GetSellerInfo, GetShopInfo, ValidateSellerOwnership |
| review.proto | ReviewInternalService | GetProductRating, GetOrderReviews |
| notification.proto | NotificationInternalService | GetNotificationPreference, SendNotification |
| system.proto | SystemInternalService | GetFeatureFlag, GetSystemConfig, RecordAuditLog |

- [ ] **Step 5: Leno.SharedContracts.csproj 引用 Grpc.AspNetCore 包并配置 Protobuf**

修改 `src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj`，新增（仅作为 .proto 文件宿主，不直接生成 C# 代码，由 `Leno.SharedContracts.Grpc` 项目生成）：

```xml
<ItemGroup>
  <None Include="Protos\**\*.proto" Pack="true" PackagePath="protos\" />
</ItemGroup>
```

- [ ] **Step 6: 运行 buf generate 生成 C# 代码**

```bash
# 安装 buf CLI（首次）
# Windows: winget install bufbuild.buf
# 或 choco install buf

cd src/BuildingBlocks/Leno.SharedContracts
buf generate
```

Expected: 在 `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/` 目录生成 11 个 .cs 文件。

- [ ] **Step 7: CI 集成 buf lint + buf breaking**

修改 `.github/workflows/ci.yml`（或对应 CI 配置），新增 job：

```yaml
  proto-lint-breaking:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # buf breaking 需要对比 main 分支
      - uses: bufbuild/buf-setup-action@v1
      - name: buf lint
        working-directory: src/BuildingBlocks/Leno.SharedContracts
        run: buf lint
      - name: buf breaking (against main)
        working-directory: src/BuildingBlocks/Leno.SharedContracts
        run: buf breaking --against "https://github.com/${{ github.repository }}.git#branch=main,subdir=src/BuildingBlocks/Leno.SharedContracts"
        if: github.event_name == 'pull_request'
```

- [ ] **Step 8: 编译验证**

Run: `dotnet build src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj`
Expected: BUILD SUCCESS

- [ ] **Step 9: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Protos/ src/BuildingBlocks/Leno.SharedContracts/buf.yaml src/BuildingBlocks/Leno.SharedContracts/buf.gen.yaml src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj src/BuildingBlocks/Leno.SharedContracts.Grpc/ .github/workflows/ci.yml
git commit -m "feat(M4.3): 新建 11 个 .proto 契约 + Leno.SharedContracts.Grpc 项目 + buf CLI 校验集成"
```

---

## Task 7: 新建 GrpcAntiCorruptionClientBase + UseGrpc 灰度开关

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`（AddLenoApi 内根据 UseGrpc 开关注册 gRPC 客户端）
- Modify: 11 个 BC `Program.cs`（启用 gRPC 服务端：`app.MapGrpcService<XxxGrpcService>()`）

- [ ] **Step 1: 创建 AntiCorruptionOptions**

创建 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs`：

```csharp
namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层配置（M4.3）。
/// 通过 <c>AntiCorruption</c> 配置节绑定。
/// </summary>
public sealed class AntiCorruptionOptions
{
    /// <summary>是否启用 gRPC 模式（默认 false，灰度切换）。</summary>
    public bool UseGrpc { get; init; } = false;

    /// <summary>gRPC 服务端点地址映射（按 BC 名），如 <c>Order</c> -> <c>https://leno-order-api:5254</c>。</summary>
    public Dictionary<string, string> GrpcEndpoints { get; init; } = new();

    /// <summary>Polly 策略配置（M4.1）。</summary>
    public PollyOptions Polly { get; init; } = new();
}

public sealed class PollyOptions
{
    public int RetryCount { get; init; } = 3;
    public double CircuitBreakerFailureThreshold { get; init; } = 0.5;
    public int CircuitBreakerDurationSeconds { get; init; } = 30;
    public int TimeoutSeconds { get; init; } = 10;
}
```

- [ ] **Step 2: 创建 GrpcAntiCorruptionClientBase**

创建 `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs`：

```csharp
using Grpc.Core;
using Grpc.Net.Client;
using Leno.SharedKernel.Exceptions;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC 防腐层客户端基类（M4.3）。
/// 统一 gRPC 调用的异常处理与埋点。
/// 错误处理策略与 <see cref="AntiCorruptionBase"/> 一致：网络故障映射 503 + <c>{SERVICE}_UNAVAILABLE</c>。
/// </summary>
public abstract class GrpcAntiCorruptionClientBase
{
    protected abstract string ServiceName { get; }

    protected async Task<T> ExecuteAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken ct = default)
    {
        try
        {
            return await execute(ct).ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable ||
                                       ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE",
                $"gRPC 调用 {ServiceName}/{operation} 不可用：{ex.Status.Detail}");
        }
        catch (RpcException ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_REMOTE_FAILED",
                $"gRPC 调用 {ServiceName}/{operation} 失败：StatusCode={ex.StatusCode} Detail={ex.Status.Detail}");
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, operation);
            throw new DomainException(
                $"{ServiceName.ToUpperInvariant()}_UNAVAILABLE",
                $"gRPC 调用 {ServiceName}/{operation} 超时：{ex.Message}");
        }
    }
}
```

- [ ] **Step 3: AddLenoApi 内根据 UseGrpc 开关注册 gRPC 客户端**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`，在 `AddLenoApi<TDbContext>` 方法体内增加：

```csharp
var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
services.Configure<AntiCorruptionOptions>(configuration.GetSection("AntiCorruption"));

if (antiCorruptionOptions.UseGrpc)
{
    // 启用 gRPC 客户端模式：各 BC 在 configureInfrastructure 委托中注册具体 gRPC 客户端
    // 例如：services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(o => o.Address = new Uri(endpoints["Product"]));
    // 此处仅注册公共基础设施，具体客户端注册由各 BC 委托完成
    services.AddGrpc(opts =>
    {
        opts.EnableDetailedErrors = false; // 生产关闭详细错误
        opts.Interceptors.Add<GrpcAntiCorruptionInterceptor>();
    });
}
```

> **说明：** `GrpcAntiCorruptionInterceptor` 为可选的全局拦截器，用于统一记录 gRPC 调用指标。本计划不强制实现，可后续按需补充。

- [ ] **Step 4: 11 个 BC Program.cs 启用 gRPC 服务端**

修改 11 个 BC 的 `Program.cs`，在 `app.UseLenoPipeline()` 之后增加：

```csharp
// gRPC 服务端（M4.3）：始终启用，客户端通过 UseGrpc 开关切换调用方式
app.MapGrpcService<ProductGrpcService>(); // 各 BC 替换为对应 GrpcService
```

并在 `Program.cs` 顶部增加端口监听：

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP 端口（既有）
    options.ListenAnyIP(5152); // 各 BC 替换端口
    // gRPC 端口（HTTP + 100）
    options.ListenAnyIP(5252, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs src/Services/*/Leno.*.Api/Program.cs
git commit -m "feat(M4.3): 新建 GrpcAntiCorruptionClientBase + UseGrpc 灰度开关 + 11 BC 启用 gRPC 服务端"
```

---

## Task 8: 批次 1（M4.3a）— 6 个高频防腐层 gRPC 服务端 + 客户端迁移

**Files:**
- Create: 6 个 BC `GrpcServices/` 目录与服务实现
- Create: Order BC 的 6 个 gRPC 客户端适配器（替换 HttpClient 防腐层）
- Modify: Order/ReviewAfterSales/Cart BC 的 `ServiceCollectionExtensions.cs`（按 UseGrpc 开关注册 HttpClient 或 gRPC 客户端）

**批次 1 范围**：Product、Promotion、Points、User、Order、Payment（spec §11.3 批次 1）

- [ ] **Step 1: Product BC 新建 ProductGrpcService**

创建 `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`：

```csharp
using Grpc.Core;
using Leno.Product.Application;
using Leno.SharedContracts.Grpc.Product.V1;

namespace Leno.Product.Api.GrpcServices;

/// <summary>
/// Product 域 gRPC 内部服务实现（M4.3a）。
/// 复用 <see cref="IProductInternalQueryService"/> 既有业务逻辑。
/// </summary>
public sealed class ProductGrpcService : ProductInternalService.ProductInternalServiceBase
{
    private readonly IProductInternalQueryService _queryService;
    private readonly ILogger<ProductGrpcService> _logger;

    public ProductGrpcService(IProductInternalQueryService queryService, ILogger<ProductGrpcService> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public override async Task<SkuInfo> GetSkuInfo(GetSkuInfoRequest request, ServerCallContext context)
    {
        var sku = await _queryService.GetSkuInfoAsync(request.SkuId, context.CancellationToken);
        if (sku is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SKU {request.SkuId} 不存在"));
        }
        return MapTo(sku);
    }

    public override async Task<BatchGetSkuInfoResponse> BatchGetSkuInfo(BatchGetSkuInfoRequest request, ServerCallContext context)
    {
        var skus = await _queryService.BatchGetSkuInfoAsync(request.SkuIds.ToList(), context.CancellationToken);
        return new BatchGetSkuInfoResponse { Skus = { skus.Select(MapTo) } };
    }

    public override async Task<SkuStock> GetSkuStock(GetSkuStockRequest request, ServerCallContext context)
    {
        var stock = await _queryService.GetSkuStockAsync(request.SkuId, context.CancellationToken);
        return new SkuStock { SkuId = request.SkuId, Available = stock.Available, Reserved = stock.Reserved };
    }

    public override async Task<ProductDetail> GetProductDetail(GetProductDetailRequest request, ServerCallContext context)
    {
        var detail = await _queryService.GetProductDetailAsync(request.SpuId, context.CancellationToken);
        return MapTo(detail);
    }

    private static SkuInfo MapTo(Application.DTOs.SkuInfoDto dto) => new()
    {
        SkuId = dto.SkuId,
        SpuId = dto.SpuId,
        Title = dto.Title ?? string.Empty,
        MainImage = dto.MainImage ?? string.Empty,
        PriceCents = dto.PriceCents,
        Currency = dto.Currency ?? "CNY",
        Salable = dto.Salable,
        SellerId = dto.SellerId,
        Stock = dto.Stock
    };

    private static ProductDetail MapTo(Application.DTOs.ProductDetailDto dto) => new()
    {
        SpuId = dto.SpuId,
        Title = dto.Title ?? string.Empty,
        Description = dto.Description ?? string.Empty,
        SellerId = dto.SellerId,
        Skus = { dto.Skus.Select(MapTo) }
    };
}
```

> **说明：** 字段名映射需根据 `IProductInternalQueryService` 实际返回 DTO 调整。若 DTO 字段名与 .proto 不一致，在 `MapTo` 方法中做转换。

- [ ] **Step 2: Promotion/Points/User/Order/Payment BC 新建对应 GrpcService**

按 Step 1 模板，在 5 个 BC 的 `Api/GrpcServices/` 目录下新建：
- `PromotionGrpcService.cs`：4 个方法（CalculateDiscount/LockCoupon/ReleaseCoupons/GetCouponInfo）
- `PointsGrpcService.cs`：5 个方法（TrialOffset/Freeze/Release/Confirm/GetPointsBalance）
- `UserGrpcService.cs`：3 个方法（GetUserContacts/GetUserInfo/GetUserAddresses）
- `OrderGrpcService.cs`：3 个方法（GetOrderStatus/GetOrderDetail/GetSellerOrders）
- `PaymentGrpcService.cs`：2 个方法（GetPaymentInfo/GetRefundStatus）

每个 GrpcService 复用对应 BC 的 `IXxxInternalQueryService` 或 AppService 业务逻辑。

- [ ] **Step 3: 6 个 BC Program.cs 注册 GrpcService**

修改 6 个 BC 的 `Program.cs`，在 `app.UseLenoPipeline()` 之后增加 `app.MapGrpcService<XxxGrpcService>()`（Task 7 Step 4 已统一处理，本步骤为校验 6 个 BC 全部完成）。

- [ ] **Step 4: Order BC 新建 gRPC 客户端适配器（Product/Promotion/Points 3 个）**

创建 3 个 gRPC 客户端适配器，实现既有 `IProductAntiCorruptionService`/`IPromotionAntiCorruptionService`/`IPointsAntiCorruptionService` 接口：

样例（`src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/ProductAntiCorruptionGrpcClient.cs`）：

```csharp
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Application.Services;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.SharedKernel.Exceptions;

namespace Leno.Order.Infrastructure.Services.Grpc;

/// <summary>
/// Product 域 gRPC 防腐层客户端（M4.3a）。
/// 实现 <see cref="IProductAntiCorruptionService"/>，通过 gRPC 调用 Product 域。
/// 灰度切换：DI 容器在 <c>AntiCorruption:UseGrpc=true</c> 时注册此类替换 <see cref="ProductAntiCorruptionService"/>。
/// </summary>
public sealed class ProductAntiCorruptionGrpcClient : GrpcAntiCorruptionClientBase, IProductAntiCorruptionService
{
    private readonly ProductInternalService.ProductInternalServiceClient _client;

    protected override string ServiceName => "product";

    public ProductAntiCorruptionGrpcClient(ProductInternalService.ProductInternalServiceClient client)
    {
        _client = client;
    }

    public Task<SkuInfo?> GetSkuInfoAsync(long skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_info", async token =>
        {
            var resp = await _client.GetSkuInfoAsync(new GetSkuInfoRequest { SkuId = skuId }, cancellationToken: token);
            return new SkuInfo
            {
                SkuId = resp.SkuId,
                SpuId = resp.SpuId,
                Title = resp.Title,
                // ... 完整映射
            };
        }, ct);
}
```

> **说明：** 返回类型 `SkuInfo` 此处应为 `Leno.Order.Application.Services.SkuInfo`（Order BC 内部的 DTO），不是 gRPC 生成的 SkuInfo。需在适配器内做映射。

- [ ] **Step 5: Order/Cart/ReviewAfterSales BC ServiceCollectionExtensions 按 UseGrpc 开关注册**

修改 3 个 BC 的 `Dependencies/ServiceCollectionExtensions.cs`，按 `AntiCorruptionOptions.UseGrpc` 开关注册 HttpClient 或 gRPC 客户端：

样例（Order）：

```csharp
var antiCorruptionOpts = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new AntiCorruptionOptions();
var endpoints = antiCorruptionOpts.GrpcEndpoints;

if (antiCorruptionOpts.UseGrpc)
{
    // gRPC 模式
    services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(o =>
        o.Address = new Uri(endpoints["Product"]));
    services.AddGrpcClient<PromotionInternalService.PromotionInternalServiceClient>(o =>
        o.Address = new Uri(endpoints["Promotion"]));
    services.AddGrpcClient<PointsInternalService.PointsInternalServiceClient>(o =>
        o.Address = new Uri(endpoints["Points"]));

    services.AddScoped<IProductAntiCorruptionService, Grpc.ProductAntiCorruptionGrpcClient>();
    services.AddScoped<IPromotionAntiCorruptionService, Grpc.PromotionAntiCorruptionGrpcClient>();
    services.AddScoped<IPointsAntiCorruptionService, Grpc.PointsAntiCorruptionGrpcClient>();
}
else
{
    // HttpClient 模式（既有，灰度期保留）
    services.AddHttpClient<IProductAntiCorruptionService, ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl))
        .AddAntiCorruptionPolicies();
    services.AddHttpClient<IPromotionAntiCorruptionService, PromotionAntiCorruptionService>(c => c.BaseAddress = new Uri(promotionApiUrl))
        .AddAntiCorruptionPolicies();
    services.AddHttpClient<IPointsAntiCorruptionService, PointsAntiCorruptionService>(c => c.BaseAddress = new Uri(pointsApiUrl))
        .AddAntiCorruptionPolicies();
}
```

对 Cart（CartPriceService → Product gRPC）、ReviewAfterSales（PaymentInfoQueryService → Payment gRPC、AfterSalesEligibilityChecker/ReviewEligibilityChecker → Order gRPC）执行同样改造。

- [ ] **Step 6: 新增 gRPC 客户端适配器单元测试**

为 3 个 Order gRPC 适配器 + Cart gRPC 适配器 + ReviewAfterSales gRPC 适配器各新建单元测试，使用 moq 模拟 gRPC Client：

```csharp
[Fact]
public async Task GetSkuInfoAsync_GrpcSuccess_ReturnsSkuInfo()
{
    var mockClient = new Mock<ProductInternalService.ProductInternalServiceClient>();
    // 配置 mock 返回 SkuInfo
    var svc = new ProductAntiCorruptionGrpcClient(mockClient.Object);
    var result = await svc.GetSkuInfoAsync(123);
    result.Should().NotBeNull();
    result!.SkuId.Should().Be(123);
}

[Fact]
public async Task GetSkuInfoAsync_GrpcUnavailable_ThrowsDomainException()
{
    var mockClient = new Mock<ProductInternalService.ProductInternalServiceClient>();
    // 配置 mock 抛 RpcException(StatusCode.Unavailable)
    var svc = new ProductAntiCorruptionGrpcClient(mockClient.Object);
    var act = () => svc.GetSkuInfoAsync(123);
    var ex = await act.Should().ThrowAsync<DomainException>();
    ex.Which.ErrorCode.Should().Be("PRODUCT_UNAVAILABLE");
}
```

- [ ] **Step 7: 端到端集成测试（gRPC 模式）**

新增集成测试 `Leno.Order.Infrastructure.Tests/Integration/OrderGrpcAntiCorruptionIntegrationTests.cs`，使用 Testcontainers 启动 Product/Promotion/Points gRPC 服务端，验证 Order 通过 gRPC 调用成功：

```csharp
[Fact]
public async Task PlaceOrder_WithGrpcAntiCorruption_Succeeds()
{
    // 启动 Product/Promotion/Points gRPC 容器
    // 配置 AntiCorruption:UseGrpc=true
    // 调用 OrderAppService.PlaceOrderAsync
    // 断言订单创建成功
}
```

- [ ] **Step 8: 编译并运行测试**

Run: `dotnet build Leno.sln && dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 9: 提交**

```bash
git add src/Services/Product/Leno.Product.Api/GrpcServices/ src/Services/Promotion/Leno.Promotion.Api/GrpcServices/ src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/ src/Services/UserAuth/Leno.UserAuth.Api/GrpcServices/ src/Services/Order/Leno.Order.Api/GrpcServices/ src/Services/Payment/Leno.Payment.Api/GrpcServices/ src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/ src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/ src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "feat(M4.3a): 批次 1 — 6 个高频防腐层 gRPC 服务端 + 客户端迁移（Product/Promotion/Points/User/Order/Payment）"
```

- [ ] **Step 10: 灰度验证 1 周**

部署后，将 `AntiCorruption:UseGrpc` 配置为 `true`（可通过 Consul KV 动态切换），观察 1 周：
- gRPC 调用成功率 ≥ 99.9%
- 性能指标：OrderSaga 单次下单时间从 30-60ms 降到 5-10ms
- 无 503 错误率异常上升

验证通过后进入批次 2。

---

## Task 9: 批次 2（M4.3b）— Cart、SellerShop gRPC 迁移

**Files:**
- Create: `src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/CartAntiCorruptionGrpcClient.cs`（Order 调 Cart）
- Create: `src/Services/Product/Leno.Product.Infrastructure/Services/Grpc/SellerAntiCorruptionGrpcClient.cs`（Product 调 SellerShop）
- Create: `src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/SellerAntiCorruptionGrpcClient.cs`（Order 调 SellerShop，复用 F1.4 越权校验）
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/SellerAntiCorruptionGrpcClient.cs`（ReviewAfterSales 调 SellerShop）
- Modify: 上述调用方 BC 的 `ServiceCollectionExtensions.cs`

**批次 2 范围**：Cart、SellerShop（spec §11.3 批次 2）

- [ ] **Step 1: Cart BC 新建 CartGrpcService**

创建 `src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs`，实现 `CartInternalService` 的 `GetCartSnapshot`/`GetCheckoutPreview` 2 个方法。复用 Cart 域既有 AppService 业务逻辑。

- [ ] **Step 2: SellerShop BC 新建 SellerGrpcService（重点：ValidateSellerOwnership）**

创建 `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`，实现 `SellerInternalService` 的 3 个方法：
- `GetSellerInfo`：查询卖家信息
- `GetShopInfo`：查询店铺信息
- `ValidateSellerOwnership`：**集中提供卖家归属校验**（spec §11.3 批次 2 要求：F1.4 应用层校验迁移到 SellerShop 域集中提供）

```csharp
public override async Task<ValidateSellerOwnershipResponse> ValidateSellerOwnership(
    ValidateSellerOwnershipRequest request, ServerCallContext context)
{
    var isValid = await _sellerService.ValidateOwnershipAsync(
        request.SellerId, request.ResourceType, request.ResourceId, context.CancellationToken);
    return new ValidateSellerOwnershipResponse { IsValid = isValid };
}
```

> **说明：** F1.4（Plan 1 Task 4）在 Order/ReviewAfterSales 应用层实现了 `RequireOwnedOrderAsync`/`RequireOwnedAfterSalesAsync`。本批次将这些校验逻辑下沉到 SellerShop 域集中提供，调用方通过 gRPC 调用 `ValidateSellerOwnership`，避免各 BC 重复实现归属校验。本步骤为可选优化，若 F1.4 已稳定运行，可保留应用层校验，仅提供 gRPC 端点供未来其他 BC 复用。

- [ ] **Step 3: Cart BC Program.cs 注册 GrpcService**

修改 `src/Services/Cart/Leno.Cart.Api/Program.cs`，增加 `app.MapGrpcService<CartGrpcService>()`。

- [ ] **Step 4: SellerShop BC Program.cs 注册 GrpcService**

修改 `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs`，增加 `app.MapGrpcService<SellerGrpcService>()`。

- [ ] **Step 5: 调用方 BC 新建 gRPC 客户端适配器**

按 Task 8 Step 4 模板，新建：
- `CartAntiCorruptionGrpcClient`（Order 调 Cart）：实现新增的 `ICartAntiCorruptionService` 接口
- `SellerAntiCorruptionGrpcClient`（Product/Order/ReviewAfterSales 调 SellerShop）：实现 `ISellerAntiCorruptionService` 接口

> **说明：** 若调用方原本无 `ICartAntiCorruptionService`/`ISellerAntiCorruptionService` 接口，需新建。接口定义放在调用方 BC 的 Application 层。

- [ ] **Step 6: 调用方 BC ServiceCollectionExtensions 按 UseGrpc 开关注册**

按 Task 8 Step 5 模板，修改 4 个 BC（Order/Cart/Product/ReviewAfterSales）的 `ServiceCollectionExtensions.cs`，按 `UseGrpc` 开关注册 HttpClient 或 gRPC 客户端。

- [ ] **Step 7: 单元测试 + 集成测试**

为 5 个新建 gRPC 服务端实现各新增单元测试，为 4 个 gRPC 客户端适配器各新增单元测试。集成测试覆盖 Cart/SellerShop gRPC 调用场景。

- [ ] **Step 8: 编译并运行测试**

Run: `dotnet build Leno.sln && dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 9: 提交**

```bash
git add src/Services/Cart/Leno.Cart.Api/GrpcServices/ src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/ src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/CartAntiCorruptionGrpcClient.cs src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/SellerAntiCorruptionGrpcClient.cs src/Services/Product/Leno.Product.Infrastructure/Services/Grpc/SellerAntiCorruptionGrpcClient.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/SellerAntiCorruptionGrpcClient.cs src/Services/Cart/Leno.Cart.Api/Program.cs src/Services/SellerShop/Leno.SellerShop.Api/Program.cs src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "feat(M4.3b): 批次 2 — Cart、SellerShop gRPC 迁移，SellerShop.ValidateSellerOwnership 集中提供卖家归属校验"
```

- [ ] **Step 10: 灰度验证 1 周**

部署后，将涉及 BC 的 `AntiCorruption:UseGrpc` 切为 `true`，观察 1 周。验证通过后进入批次 3。

---

## Task 10: 批次 3（M4.3c）— ReviewAfterSales、Notification、SystemAdmin gRPC 迁移

**Files:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs`
- Create: `src/Services/Notification/Leno.Notification.Api/GrpcServices/NotificationGrpcService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/GrpcServices/SystemGrpcService.cs`
- Create: 各调用方 BC 的 gRPC 客户端适配器
- Modify: 调用方 BC 的 `ServiceCollectionExtensions.cs`

**批次 3 范围**：ReviewAfterSales、Notification、SystemAdmin（spec §11.3 批次 3）

- [ ] **Step 1: ReviewAfterSales BC 新建 ReviewGrpcService**

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs`，实现 `ReviewInternalService` 的 2 个方法：`GetProductRating`、`GetOrderReviews`。复用 ReviewAfterSales 域既有 AppService 业务逻辑。

- [ ] **Step 2: Notification BC 新建 NotificationGrpcService**

创建 `src/Services/Notification/Leno.Notification.Api/GrpcServices/NotificationGrpcService.cs`，实现 `NotificationInternalService` 的 2 个方法：`GetNotificationPreference`、`SendNotification`。

- [ ] **Step 3: SystemAdmin BC 新建 SystemGrpcService**

创建 `src/Services/SystemAdmin/Leno.SystemAdmin.Api/GrpcServices/SystemGrpcService.cs`，实现 `SystemInternalService` 的 3 个方法：`GetFeatureFlag`、`GetSystemConfig`、`RecordAuditLog`。

- [ ] **Step 4: 3 个 BC Program.cs 注册 GrpcService**

修改 3 个 BC 的 `Program.cs`，增加 `app.MapGrpcService<XxxGrpcService>()`。

- [ ] **Step 5: 调用方 BC 新建 gRPC 客户端适配器**

按 spec §11.3 表格：
- ReviewInternalService 调用方：Product、Order → 在 Product/Order BC 新建 `ReviewAntiCorruptionGrpcClient`
- NotificationInternalService 调用方：各 BC → 在各 BC 新建 `NotificationAntiCorruptionGrpcClient`（或新建 `INotificationAntiCorruptionService` 接口）
- SystemInternalService 调用方：各 BC → 在各 BC 新建 `SystemAntiCorruptionGrpcClient`

> **说明：** Notification/SystemAdmin 调用方较多，可考虑在 `Leno.Infrastructure.AntiCorruption` 提供通用基类，各 BC 仅做薄封装。

- [ ] **Step 6: 调用方 BC ServiceCollectionExtensions 按 UseGrpc 开关注册**

按 Task 8 Step 5 模板，修改各调用方 BC 的 `ServiceCollectionExtensions.cs`。

- [ ] **Step 7: 单元测试 + 集成测试**

为 3 个新建 gRPC 服务端实现各新增单元测试，为各 gRPC 客户端适配器新增单元测试。集成测试覆盖 ReviewAfterSales/Notification/SystemAdmin gRPC 调用场景。

- [ ] **Step 8: 编译并运行测试**

Run: `dotnet build Leno.sln && dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 9: 提交**

```bash
git add src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ src/Services/Notification/Leno.Notification.Api/GrpcServices/ src/Services/SystemAdmin/Leno.SystemAdmin.Api/GrpcServices/ src/Services/Product/Leno.Product.Infrastructure/Services/Grpc/ src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/ src/Services/*/Leno.*.Api/Program.cs src/Services/*/Leno.*.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "feat(M4.3c): 批次 3 — ReviewAfterSales、Notification、SystemAdmin gRPC 迁移"
```

- [ ] **Step 10: 灰度验证 1 周**

部署后，将涉及 BC 的 `AntiCorruption:UseGrpc` 切为 `true`，观察 1 周。验证通过后进入 Task 11 下线 HttpClient 代码。

---

## Task 11: 下线 HttpClient 防腐层代码 + Internal REST 控制器

**Files:**
- Delete: 9 个 HttpClient 防腐层服务实现文件
- Delete: 7 个 `Internal*Controller.cs`（或保留 `NotificationSendController.cs` 若仍需 HTTP 触发）
- Modify: 各 BC `ServiceCollectionExtensions.cs`（移除 HttpClient 注册分支，仅保留 gRPC 注册）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`（移除 UseGrpc 开关，gRPC 为唯一模式）
- Modify: 各 BC `appsettings.json`（移除 `AntiCorruption:UseGrpc` 配置项，或保留但默认 true）

**前提条件**：3 个批次全部灰度验证通过，gRPC 调用稳定运行 ≥ 1 周。

- [ ] **Step 1: 删除 9 个 HttpClient 防腐层服务实现**

```bash
git rm src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs
git rm src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs
git rm src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs
git rm src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs
git rm src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs
git rm src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs
git rm src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs
```

> **说明：** 若 LogisticsTrackingService 为第三方物流调用（非跨 BC），保留并继续用 HttpClient + Polly，不删除。删除前确认每个文件的实际用途。

- [ ] **Step 2: 删除 7 个 Internal REST 控制器（或保留 Notification）**

```bash
git rm src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs
git rm src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs
git rm src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs
git rm src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs
git rm src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs
git rm src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs
# NotificationSendController 保留（外部触发场景仍需 HTTP）
```

- [ ] **Step 3: 各 BC ServiceCollectionExtensions 移除 HttpClient 注册分支**

修改各 BC 的 `ServiceCollectionExtensions.cs`，移除 `if (antiCorruptionOpts.UseGrpc) { ... } else { HttpClient 分支 }` 中的 else 分支，仅保留 gRPC 注册。

- [ ] **Step 4: WebApplicationExtensions 移除 UseGrpc 开关**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`，移除 `if (antiCorruptionOptions.UseGrpc)` 条件判断，gRPC 注册为唯一路径。

- [ ] **Step 5: 删除相关测试**

删除 9 个 HttpClient 防腐层服务对应的单元测试文件、7 个 `Internal*ControllerTests.cs` 中针对旧路由的测试用例。

- [ ] **Step 6: 全量测试验证**

Run: `dotnet build Leno.sln && dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 7: 性能基准验证**

执行性能基准测试，验证 gRPC 调用 P99 < HttpClient 的 30%：

```bash
dotnet run --project src/Services/Order/Leno.Order.Api -- --bench grpc
# 或使用 BenchmarkDotNet 项目（若存在）
```

Expected: gRPC P99 延迟 < HttpClient P99 的 30%

- [ ] **Step 8: 提交**

```bash
git add -A
git commit -m "refactor(M4.3): 下线全部 HttpClient 防腐层代码与 Internal REST 控制器，gRPC 成为唯一跨 BC 通信方式"
```

---

## Task 12: 全量集成测试与最终验收

**Files:**
- Run: 全量测试套件
- Verify: spec §11 验收清单

- [ ] **Step 1: 全量测试**

Run: `dotnet test Leno.sln --configuration Release`
Expected: 全部 PASS（1648+ 既有测试 + M4 新增测试无回归）

- [ ] **Step 2: 验收清单核对（spec §11.1 M4.1）**

```bash
# 1. 所有防腐层服务继承 AntiCorruptionBase
rg ":\s*AntiCorruptionBase" src/Services/*/Leno.*.Infrastructure/Services/
# 期望：9 个防腐层服务（HttpClient 模式下，gRPC 模式下继承 GrpcAntiCorruptionClientBase）

# 2. 所有 AddHttpClient<T> 配置 Polly（验证期，HttpClient 下线后此校验移除）
rg "AddAntiCorruptionPolicies" src/Services/*/Leno.*.Infrastructure/Dependencies/
# 期望：4 个 BC 共 11 处调用

# 3. Grep 防腐层 return null / return default 零命中（除 ExecuteAsync 内部）
rg "return null|return default" src/Services/*/Leno.*.Infrastructure/Services/
# 期望：0 命中（LogisticsTrackingService 除外，若保留 HttpClient 模式）
```

- [ ] **Step 3: 验收清单核对（spec §11.2 M4.2）**

```bash
# 1. Grep RouteAttribute.*"internal/ 全部含 /v1/ 前缀
rg "\[Http(Get|Post|Put|Delete)\(\"internal" src/Services/*/Leno.*.Api/Controllers/
# 期望：所有路由含 /v1/ 前缀（双路由期下线后旧路由已删除）

# 2. IntegrationEventBase 含 SchemaVersion 字段
rg "SchemaVersion" src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs
# 期望：命中 1 处属性定义
```

- [ ] **Step 4: 验收清单核对（spec §11.3 M4.3）**

```bash
# 1. Leno.SharedContracts/Protos/ 含 11 个 .proto 文件
ls src/BuildingBlocks/Leno.SharedContracts/Protos/*.proto | wc -l
# 期望：11

# 2. CI 集成 buf lint + buf breaking
rg "buf lint|buf breaking" .github/workflows/
# 期望：命中 2 处

# 3. 11 个 BC.Api 含 GrpcServices/ 实现
ls src/Services/*/Leno.*.Api/GrpcServices/*.cs | wc -l
# 期望：11（每个 BC 至少 1 个 GrpcService 文件）

# 4. 所有同步跨 BC 调用通过 gRPC（HttpClient 防腐层全部下线）
rg "AddHttpClient<I\w*AntiCorruptionService" src/Services/*/Leno.*.Infrastructure/Dependencies/
# 期望：0 命中（HttpClient 防腐层代码已删除）

# 5. SellerShop.ValidateSellerOwnership 被各调用方复用
rg "ValidateSellerOwnership" src/Services/*/Leno.*.Infrastructure/Services/Grpc/
# 期望：≥3 处（Product/Order/ReviewAfterSales 各 1 处调用）

# 6. 性能基准：gRPC 调用 P99 < HttpClient 的 30%
# 由 Step 7 性能测试验证
```

- [ ] **Step 5: 文档更新（M6.5 范围，本步骤为预留）**

> **说明：** 编码规范第 15 章"gRPC 内部服务通信"的完整文档化由 Plan 10（M6.5）统一完成。本计划仅确保代码层面的 gRPC 治理已就绪。

- [ ] **Step 6: 提交最终验收记录**

```bash
# 若有任何文档/配置微调，统一提交
git add -A
git commit --allow-empty -m "chore(M4): 通信升级最终验收完成，spec §11 全部验收项通过"
```

---

## 风险与缓解

| 风险 | 缓解 |
|---|---|
| AntiCorruptionBase 模板方法改变既有防腐层行为，导致既有测试失败 | Task 3 每个 BC 改造后立即跑该 BC 单元测试，失败用例现场修复；写操作错误处理从「抛异常」改为「抛异常 + 埋点」，业务语义不变 |
| ProductAntiCorruptionService 移除 3 处 `return null` 后，上游调用方可能因 null 检查失效而 NRE | Task 3 Step 1 改造后，同步检查上游 `PlaceOrderAsync`/`CheckoutAsync` 等调用方的 null 检查逻辑，移除冗余 null 检查或改为捕获 `PRODUCT_UNAVAILABLE` 异常 |
| 11 条 internal 路由加 /v1/ 前缀后，旧客户端调用失败 | 双路由期 1 周，新旧路由并存；CI 集成测试覆盖两个路由；旧路由删除前发全员通知 |
| IntegrationEventBase 新增 SchemaVersion 字段，既有事件反序列化失败 | 字段为 `int` 默认值 1，JSON 反序列化兼容；EF Core Migration `HasDefaultValue(1)` 兼容既有数据 |
| gRPC 迁移 3 批次跨 3+ 周，期间 HttpClient 与 gRPC 并存增加运维复杂度 | `UseGrpc` 灰度开关每 BC 独立配置，可单独回滚；每批次验证 1 周稳定后再进入下一批次 |
| .proto 契约变更未走 buf breaking 校验，导致消费方反序列化失败 | CI 集成 `buf breaking`，PR 阶段强制阻断不向后兼容变更；.proto 文件变更需 PR Review 强制评审 |
| 批次 3 Notification/SystemAdmin 调用方涉及全 BC，迁移工作量爆炸 | Notification/SystemAdmin gRPC 客户端在 `Leno.Infrastructure.AntiCorruption` 提供通用基类，各 BC 仅做薄封装；优先迁移高频调用方，低频调用方保留 HttpClient 模式可接受 |

## 依赖关系

- Task 1 → Task 2 → Task 3（顺序依赖，AntiCorruptionBase 先建，Polly 策略次之，最后改造服务）
- Task 3 → Task 4（防腐层 URL 已在 Task 3 同步更新，Task 4 校验 + 加双路由）
- Task 4 → Task 5（独立，可并行）
- Task 1 → Task 6 → Task 7（proto 契约先建，gRPC 基类次之）
- Task 7 → Task 8 → Task 9 → Task 10（3 批次顺序执行，每批次 1 周验证期）
- Task 10 → Task 11（全部批次验证后下线 HttpClient 代码）
- Task 11 → Task 12（最终验收）
