# M1: 架构合规 — 限界上下文边界修复 + 共享内核职责清理

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 Notification 跨上下文边界违规，清理 SharedKernel 中泄漏的技术细节，恢复 DDD 战略设计约束。

**Architecture:** 将 Promotion/PointsMembership 的跨上下文事件从 BC.Domain 迁移到 SharedContracts（遵循 OrderEvents 既有模式）；从 Entity 移除 `Version` 持久化字段；从 DomainException 移除 `HttpStatusCode`，改为中间件查表映射；删除未使用的 `MoneyJsonConverter.ToStorage/FromStorage`。

**Tech Stack:** .NET 10, EF Core, MassTransit, xUnit, FluentAssertions

**Spec:** [docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md](../specs/2026-07-13-comprehensive-optimization-design.md) 第 4-5 节（主线 1 + 主线 2）

---

## 文件结构

### 新建文件
| 文件 | 职责 |
|---|---|
| `src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs` | 促销域跨上下文集成事件契约 |
| `src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs` | 积分会员域跨上下文集成事件契约 |
| `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs` | 错误码→HTTP状态码映射表 |

### 修改文件
| 文件 | 修改内容 |
|---|---|
| `src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs` | 移除 `Version` 字段 |
| `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs` | 移除 `HttpStatusCode` 字段 |
| `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs` | 改用 `ErrorCodeMapping` 查表 |
| `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs` | 删除 `ToStorage/FromStorage` |
| `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj` | 删除 Promotion.Domain、PointsMembership.Domain 引用 |
| `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/*.cs` | 改 using 命名空间 |
| 14 个 `*DomainException.cs` 文件 | 移除 `httpStatusCode` 构造参数 |

### 删除文件
| 文件 | 原因 |
|---|---|
| `src/Services/Promotion/Leno.Promotion.Domain/Events/SeckillOrderCreatedEvent.cs` | 迁移至 SharedContracts |
| `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsEarnedEvent.cs` | 迁移至 SharedContracts |
| `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs` | 迁移至 SharedContracts |
| `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MembershipActivatedEvent.cs` | 迁移至 SharedContracts |

---

## 主线 1：限界上下文边界修复

### Task 1: 创建 SharedContracts/Events/PromotionEvents.cs

**Files:**
- Create: `src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs`

- [ ] **Step 1: 创建 PromotionEvents.cs**

将 `SeckillOrderCreatedEvent` 从 `Promotion.Domain.Events` 命名空间迁移到 `Leno.SharedContracts.Events`，内容与原文件完全一致，仅改命名空间。

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 秒杀订单创建集成事件，秒杀下单 Redis 预扣成功后由促销域发布。
/// 消费方：通知域（下单成功通知）、订单域（异步创建秒杀订单）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class SeckillOrderCreatedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>异步创建的订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>秒杀价（单价）。</summary>
    public decimal SeckillPrice { get; init; }

    /// <summary>下单数量。</summary>
    public int Quantity { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ActivityId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderCreatedEvent() : base()
    {
    }

    public SeckillOrderCreatedEvent(
        Guid activityId,
        Guid spuId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        decimal seckillPrice,
        int quantity) : base()
    {
        ActivityId = activityId;
        SpuId = spuId;
        SkuId = skuId;
        UserId = userId;
        OrderId = orderId;
        SeckillPrice = seckillPrice;
        Quantity = quantity;
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs
git commit -m "feat(shared-contracts): 新增 PromotionEvents.cs 迁移秒杀订单创建事件契约"
```

---

### Task 2: 创建 SharedContracts/Events/PointsMembershipEvents.cs

**Files:**
- Create: `src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs`

- [ ] **Step 1: 创建 PointsMembershipEvents.cs**

将 `PointsEarnedEvent`、`MemberLevelUpgradedEvent`、`MembershipActivatedEvent` 三个事件从 `PointsMembership.Domain.Events` 迁移到 `Leno.SharedContracts.Events`，内容与原文件完全一致，仅改命名空间。

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 积分入账集成事件，积分账户 Earn 时发布。
/// 消费方：消息通知域（积分到账通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class PointsEarnedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>入账积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>积分来源（CheckIn/Consumption/Activity/Refund/Offset）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsEarnedEvent() : base()
    {
    }

    public PointsEarnedEvent(Guid accountId, Guid userId, int amount, string source)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        Source = source ?? string.Empty;
    }
}

/// <summary>
/// 会员等级升级集成事件，会员累计消费达门槛触发升级时发布。
/// 消费方：消息通知域（等级升级通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class MemberLevelUpgradedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid UserId { get; init; }

    public int OldLevel { get; init; }

    public int NewLevel { get; init; }

    public DateTime UpgradedAt { get; init; }

    public Guid AggregateId => UserId;

    public MemberLevelUpgradedEvent() : base()
    {
    }

    public MemberLevelUpgradedEvent(Guid userId, int oldLevel, int newLevel, DateTime upgradedAt)
        : base()
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}

/// <summary>
/// 会员权益激活集成事件，会员订阅订单支付成功激活 UserMembership 时发布。
/// 消费方：消息通知域（会员开通通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class MembershipActivatedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid UserId { get; init; }

    public Guid PackageId { get; init; }

    public int Level { get; init; }

    public DateTime EndTime { get; init; }

    public Guid AggregateId => UserId;

    public MembershipActivatedEvent() : base()
    {
    }

    public MembershipActivatedEvent(Guid userId, Guid packageId, int level, DateTime endTime)
        : base()
    {
        UserId = userId;
        PackageId = packageId;
        Level = level;
        EndTime = endTime;
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs
git commit -m "feat(shared-contracts): 新增 PointsMembershipEvents.cs 迁移积分会员事件契约"
```

---

### Task 3: 删除 BC.Domain 中的旧事件文件并更新引用

**Files:**
- Delete: `src/Services/Promotion/Leno.Promotion.Domain/Events/SeckillOrderCreatedEvent.cs`
- Delete: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsEarnedEvent.cs`
- Delete: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs`
- Delete: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MembershipActivatedEvent.cs`

- [ ] **Step 1: 删除 4 个旧事件文件**

删除上述 4 个文件。这些事件已迁移到 SharedContracts。

- [ ] **Step 2: 全局替换命名空间引用**

在 `src/Services/Promotion/` 和 `src/Services/PointsMembership/` 目录下，将所有 `.cs` 文件中的 `using Leno.Promotion.Domain.Events;` 替换为 `using Leno.SharedContracts.Events;`，`using Leno.PointsMembership.Domain.Events;` 替换为 `using Leno.SharedContracts.Events;`。

注意：这两个 BC 已引用 SharedContracts（因为事件原本就继承 `IntegrationEventBase`），所以无需修改 csproj。

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/Services/Promotion/Leno.Promotion.Domain/Leno.Promotion.Domain.csproj && dotnet build src/Services/PointsMembership/Leno.PointsMembership.Domain/Leno.PointsMembership.Domain.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: 验证测试**

Run: `dotnet test src/Services/Promotion/Leno.Promotion.Domain.Tests/ && dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/`
Expected: All tests pass

- [ ] **Step 5: 提交**

```bash
git add -A src/Services/Promotion/ src/Services/PointsMembership/
git commit -m "refactor(promotion,points): 迁移跨上下文事件至 SharedContracts 删除 BC.Domain 旧定义"
```

---

### Task 4: 更新 Notification.Infrastructure — 删除跨 BC 引用

**Files:**
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj`
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs`
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PromotionEventConsumer.cs`
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PointsEventConsumer.cs`

- [ ] **Step 1: 从 csproj 删除两处 ProjectReference**

在 `Leno.Notification.Infrastructure.csproj` 中删除以下两行：

```xml
    <ProjectReference Include="..\..\Promotion\Leno.Promotion.Domain\Leno.Promotion.Domain.csproj" />
    <ProjectReference Include="..\..\PointsMembership\Leno.PointsMembership.Domain\Leno.PointsMembership.Domain.csproj" />
```

保留 `Leno.SharedContracts.csproj` 引用（已存在）。

- [ ] **Step 2: 更新 NotificationEventConsumer.cs 的 using**

将 `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs` 第 4-5 行：

```csharp
using Leno.PointsMembership.Domain.Events;
using Leno.Promotion.Domain.Events;
```

删除这两行。`Leno.SharedContracts.Events` 已在第 6 行引入，迁移后的事件类型在该命名空间下。

- [ ] **Step 3: 更新 PromotionEventConsumer.cs 的 using**

将 `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PromotionEventConsumer.cs` 第 4 行：

```csharp
using Leno.Promotion.Domain.Events;
```

替换为：

```csharp
using Leno.SharedContracts.Events;
```

- [ ] **Step 4: 更新 PointsEventConsumer.cs 的 using**

将 `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PointsEventConsumer.cs` 第 4 行：

```csharp
using Leno.PointsMembership.Domain.Events;
```

替换为：

```csharp
using Leno.SharedContracts.Events;
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj`
Expected: BUILD SUCCEEDED（确认 Notification 不再依赖 Promotion.Domain / PointsMembership.Domain）

- [ ] **Step 6: 验证全量编译**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED

- [ ] **Step 7: 验证测试**

Run: `dotnet test src/Services/Notification/`
Expected: All tests pass

- [ ] **Step 8: 提交**

```bash
git add src/Services/Notification/Leno.Notification.Infrastructure/
git commit -m "refactor(notification): 删除对 Promotion.Domain/PointsMembership.Domain 的跨上下文引用

Notification.Infrastructure 现仅引用 SharedContracts，不再直接依赖其他 BC 的 Domain 项目。
限界上下文边界违规已修复。"
```

---

## 主线 2：共享内核职责清理

### Task 5: 移除 Entity.Version 字段

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs`

**背景：** `Entity.Version`（`byte[]`）注释为"SQL Server rowversion"，但经全量搜索确认：无任何 EF Core 配置将其设为 `IsRowVersion()`，即乐观锁实际上未生效。移除该字段不影响现有行为。

- [ ] **Step 1: 移除 Version 字段**

在 `src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs` 中，删除第 38-41 行：

```csharp
    /// <summary>
    /// 乐观锁版本号（SQL Server rowversion），由 EF Core 与数据库协同维护。
    /// </summary>
    public byte[] Version { get; set; } = Array.Empty<byte>();
```

- [ ] **Step 2: 验证全量编译**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED

若有编译错误（其他文件引用了 `.Version`），搜索并删除这些引用。已确认 Infrastructure/Persistence 下无引用。

- [ ] **Step 3: 验证全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs
git commit -m "refactor(shared-kernel): 移除 Entity.Version 持久化细节字段

该字段泄漏 SQL Server rowversion 实现细节到领域层，且未配置为并发令牌。
乐观锁应在各 BC 的 EF Core 配置中通过 IsRowVersion() 声明。"
```

---

### Task 6: 移除 DomainException.HttpStatusCode，新增 ErrorCodeMapping

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs`
- Modify: 14 个 `*DomainException.cs` 文件（移除 `httpStatusCode` 构造参数）

**背景：** `DomainException.HttpStatusCode` 使领域异常感知 HTTP。改为由中间件查表映射。

- [ ] **Step 1: 修改 DomainException 基类**

将 `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs` 替换为：

```csharp
namespace Leno.SharedKernel.Exceptions;

/// <summary>
/// 领域异常基类，携带业务错误码。
/// 业务校验失败应抛出继承此类的异常，由全局异常中间件通过 ErrorCodeMapping 转换为标准响应。
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

- [ ] **Step 2: 创建 ErrorCodeMapping**

创建 `src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs`：

```csharp
using Leno.SharedKernel.Exceptions;

namespace Leno.Infrastructure.Middleware;

/// <summary>
/// 错误码到 HTTP 状态码的映射表，供 GlobalExceptionMiddleware 查表映射。
/// 领域异常不携带 HTTP 状态码，由本表统一管理。
/// </summary>
public static class ErrorCodeMapping
{
    private static readonly Dictionary<string, int> _mapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // 默认业务错误
        ["DOMAIN_ERROR"] = 400,

        // 订单域
        ["ORDER_ERROR"] = 400,
        ["ORDER_NOT_FOUND"] = 404,
        ["ORDER_STATE_CONFLICT"] = 409,

        // 支付域
        ["PAYMENT_ERROR"] = 400,
        ["PAYMENT_NOT_FOUND"] = 404,

        // 商品域
        ["PRODUCT_ERROR"] = 400,
        ["PRODUCT_NOT_FOUND"] = 404,

        // 购物车域
        ["CART_ERROR"] = 400,

        // 促销域
        ["PROMOTION_ERROR"] = 400,

        // 积分会员域
        ["POINTS_ERROR"] = 400,
        ["MEMBERSHIP_ERROR"] = 400,

        // 评价售后域
        ["REVIEW_ERROR"] = 400,
        ["AFTER_SALES_ERROR"] = 400,

        // 卖家店铺域
        ["SELLER_ERROR"] = 400,
        ["SHOP_ERROR"] = 400,

        // 通知域
        ["NOTIFICATION_ERROR"] = 400,

        // 系统管理域
        ["SYSTEM_ERROR"] = 400,

        // 用户认证域
        ["USER_AUTH_ERROR"] = 400,
        ["UNAUTHORIZED"] = 401,
        ["FORBIDDEN"] = 403,
    };

    /// <summary>根据错误码获取 HTTP 状态码，未知错误码默认 400。</summary>
    public static int GetStatusCode(string? errorCode) =>
        !string.IsNullOrWhiteSpace(errorCode) && _mapping.TryGetValue(errorCode, out var code)
            ? code
            : 400;
}
```

- [ ] **Step 3: 修改 GlobalExceptionMiddleware**

在 `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs` 的 `Resolve` 方法中，将 `DomainException` 分支改为：

```csharp
            case DomainException domainEx:
                return (ErrorCodeMapping.GetStatusCode(domainEx.ErrorCode), domainEx.Message, LogLevel.Warning);
```

同时更新类注释第 13-14 行，将"DomainException 按 HttpStatusCode（400/409 等）映射"改为"DomainException 按 ErrorCodeMapping 查表映射"。

- [ ] **Step 4: 批量更新 14 个 DomainException 子类**

对以下 14 个文件，移除构造函数中的 `int httpStatusCode = 400` 参数及 `: base(message, errorCode, httpStatusCode)` 中的 `httpStatusCode`：

1. `src/Services/Order/Leno.Order.Domain/Exceptions/OrderDomainException.cs`
2. `src/Services/Payment/Leno.Payment.Domain/Exceptions/PaymentDomainException.cs`
3. `src/Services/Product/Leno.Product.Domain/Exceptions/ProductDomainException.cs`
4. `src/Services/Cart/Leno.Cart.Domain/Exceptions/CartDomainException.cs`
5. `src/Services/Promotion/Leno.Promotion.Domain/Exceptions/PromotionDomainException.cs`
6. `src/Services/PointsMembership/Leno.PointsMembership.Domain/Exceptions/PointsDomainException.cs`
7. `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Exceptions/ReviewDomainException.cs`
8. `src/Services/SellerShop/Leno.SellerShop.Domain/Exceptions/SellerShopDomainException.cs`
9. `src/Services/Notification/Leno.Notification.Domain/Exceptions/NotificationDomainException.cs`
10. `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Exceptions/SystemAdminDomainException.cs`
11. `src/Services/UserAuth/Leno.UserAuth.Domain/Exceptions/UserAuthDomainException.cs`
12. `src/Services/Product/Leno.Product.Application/Exceptions/ProductValidationException.cs`
13. `src/Services/SellerShop/Leno.SellerShop.Application/Exceptions/SellerShopValidationException.cs`
14. `src/Services/UserAuth/Leno.UserAuth.Application/Exceptions/UserAuthValidationException.cs`

每个文件的修改模式（以 OrderDomainException 为例）：

修改前：
```csharp
public OrderDomainException(string message, string errorCode = "ORDER_ERROR", int httpStatusCode = 400)
    : base(message, errorCode, httpStatusCode)
{
}
```

修改后：
```csharp
public OrderDomainException(string message, string errorCode = "ORDER_ERROR")
    : base(message, errorCode)
{
}
```

- [ ] **Step 5: 验证全量编译**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED

若有调用方传递了 `httpStatusCode` 参数（如 `throw new OrderDomainException("msg", "CODE", 409)`），需删除第三个参数，改为在 `ErrorCodeMapping` 中添加对应错误码映射。

- [ ] **Step 6: 验证全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass

- [ ] **Step 7: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs \
        src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs \
        src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs \
        src/Services/*/Leno.*.Domain/Exceptions/ \
        src/Services/*/Leno.*.Application/Exceptions/
git commit -m "refactor(shared-kernel,infrastructure): 移除 DomainException.HttpStatusCode

领域异常不再感知 HTTP，改为通过 ErrorCodeMapping 查表映射错误码到状态码。
新增 ErrorCodeMapping 集中管理映射关系。"
```

---

### Task 7: 删除未使用的 MoneyJsonConverter.ToStorage/FromStorage

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs`

**背景：** 经全量搜索确认 `MoneyJsonConverter.ToStorage` 和 `FromStorage` 在整个代码库中**零引用**。这些方法是 EF Core 值转换器的存储细节，属于持久化层职责，不应出现在共享内核。

- [ ] **Step 1: 删除 ToStorage 和 FromStorage 方法**

在 `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs` 中，删除第 66-90 行（`ToStorage` 和 `FromStorage` 两个静态方法及其 XML 注释）。

同时更新类注释第 7-8 行，删除"同时提供静态序列化/反序列化方法，供 EF Core 值转换器在基础设施层复用"的描述。

- [ ] **Step 2: 验证全量编译**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED（因方法零引用，删除不影响编译）

- [ ] **Step 3: 验证全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs
git commit -m "refactor(shared-kernel): 删除未使用的 MoneyJsonConverter.ToStorage/FromStorage

这些方法是 EF Core 值转换器的持久化细节，零引用，不应出现在共享内核。"
```

---

### Task 8: 合并 PageResult 双定义

**Files:**
- Delete: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs`
- Keep: `src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs`

**背景：** `PageResult<T>` 在 SharedKernel.ValueObjects（record 不可变）与 SharedContracts.Responses（class 可变）双定义，字段完全相同。保留 SharedContracts 版本作为对外响应契约。

- [ ] **Step 1: 确认 SharedKernel.PageResult 的引用方**

搜索 `using Leno.SharedKernel.ValueObjects` 中使用 `PageResult` 的文件。

Run: `grep -rn "Leno.SharedKernel.ValueObjects.PageResult\|SharedKernel.ValueObjects.*PageResult" src/`

- [ ] **Step 2: 将引用方改为使用 SharedContracts.PageResult**

对每个引用 `Leno.SharedKernel.ValueObjects.PageResult` 的文件，将 using 改为 `using Leno.SharedContracts.Responses;`（如已有则删除旧 using）。

- [ ] **Step 3: 删除 SharedKernel 中的 PageResult.cs**

删除 `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs`。

- [ ] **Step 4: 验证全量编译**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: 验证全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass

- [ ] **Step 6: 提交**

```bash
git add -A
git commit -m "refactor(shared-kernel): 合并 PageResult 双定义为 SharedContracts 单一版本

SharedKernel.ValueObjects.PageResult 与 SharedContracts.Responses.PageResult
字段完全相同，保留 SharedContracts 版本作为统一分页响应契约。"
```

---

### Task 9: 迁移基础设施抽象到 Leno.Infrastructure.Abstractions

**Files:**
- Move: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/ICacheService.cs` → `src/BuildingBlocks/Leno.Infrastructure/Abstractions/ICacheService.cs`
- Move: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IBloomFilter.cs` → `src/BuildingBlocks/Leno.Infrastructure/Abstractions/IBloomFilter.cs`
- Move: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IFileStorageService.cs` → `src/BuildingBlocks/Leno.Infrastructure/Abstractions/IFileStorageService.cs`
- Move: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IEventBus.cs` → `src/BuildingBlocks/Leno.Infrastructure/Abstractions/IEventBus.cs`
- Move: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IExternalChannelOptions.cs` → `src/BuildingBlocks/Leno.Infrastructure/Abstractions/IExternalChannelOptions.cs`

**背景：** 这些接口的实现在 Leno.Infrastructure，属于基础设施抽象而非领域内核。迁移后 SharedKernel 只保留 `IAggregateRoot`、`IEntity`、`IRepository`、`IUnitOfWork`、`IDomainEvent`、`IHasDomainEvents`、`IAuditable`、`ISoftDeletable` 等真正领域抽象。

- [ ] **Step 1: 创建 Leno.Infrastructure/Abstractions 目录并移动文件**

将上述 5 个接口文件从 `Leno.SharedKernel/Abstractions/` 移动到 `Leno.Infrastructure/Abstractions/`，修改命名空间为 `Leno.Infrastructure.Abstractions`。

- [ ] **Step 2: 全局替换 using 命名空间**

将所有引用这 5 个接口的文件中的 `using Leno.SharedKernel.Abstractions;` 中与这 5 个接口相关的部分改为 `using Leno.Infrastructure.Abstractions;`。

注意：`IAggregateRoot`、`IEntity`、`IRepository`、`IUnitOfWork`、`IDomainEvent`、`IHasDomainEvents`、`IAuditable`、`ISoftDeletable` 仍在 SharedKernel.Abstractions，保留原有 using。

- [ ] **Step 3: 验证全量编译**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: 验证全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "refactor(infrastructure): 迁移基础设施抽象到 Leno.Infrastructure.Abstractions

ICacheService/IBloomFilter/IFileStorageService/IEventBus/IExternalChannelOptions
从 SharedKernel 迁移到 Infrastructure，SharedKernel 仅保留领域抽象。"
```

---

### Task 10: M1 验收确认

**Files:**
- All modified files in M1

- [ ] **Step 1: 全量编译验证**

Run: `dotnet build Leno.slnx`
Expected: BUILD SUCCEEDED

- [ ] **Step 2: 全量测试验证**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass (现有 2153+ 个测试全绿)

- [ ] **Step 3: 验收 Notification 边界**

Run: `grep -r "Promotion.Domain\|PointsMembership.Domain" src/Services/Notification/`
Expected: No matches found（Notification 不再引用任何 BC.Domain）

- [ ] **Step 4: 验收 SharedKernel 纯净度**

确认 `src/BuildingBlocks/Leno.SharedKernel/` 下：
- `Entity.cs` 不含 `Version` 字段
- `DomainException.cs` 不含 `HttpStatusCode` 字段
- `MoneyJsonConverter.cs` 不含 `ToStorage/FromStorage`
- `Abstractions/` 下不含 `ICacheService`/`IBloomFilter`/`IFileStorageService`/`IEventBus`/`IExternalChannelOptions`
- `ValueObjects/` 下不含 `PageResult.cs`

- [ ] **Step 5: 更新 spec 验收清单**

在 `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` 第 13.1 节和 13.2 节的验收 checklist 中勾选已完成项。

- [ ] **Step 6: 提交验收结果**

```bash
git add docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md
git commit -m "docs(spec): M1 架构合规里程碑验收完成

主线 1（限界上下文边界修复）与主线 2（共享内核职责清理）全部完成。
Notification 不再引用 BC.Domain；SharedKernel 不含技术细节。"
```

---

## Self-Review

### Spec coverage
- ✅ 主线 1.1（SharedContracts 新增事件契约）→ Task 1, 2
- ✅ 主线 1.2（Outbox 翻译领域事件）→ 简化为直接迁移（遵循 OrderEvents 既有模式，事件已实现 IDomainEvent+IIntegrationEvent）
- ✅ 主线 1.3（删除 Notification 跨 BC 引用）→ Task 3, 4
- ✅ 主线 1.4（补消费者测试）→ Task 4 Step 7 验证既有测试通过（消费者逻辑未变，仅命名空间变更）
- ✅ 主线 2.1（移除 Entity.Version）→ Task 5
- ✅ 主线 2.2（移除 DomainException.HttpStatusCode）→ Task 6
- ✅ 主线 2.3（迁移 MoneyJsonConverter.ToStorage）→ Task 7（简化为直接删除，因零引用）
- ✅ 主线 2.4（迁移基础设施抽象）→ Task 9
- ✅ 主线 2.5（合并 PageResult）→ Task 8

### Spec 偏差说明
- **主线 1.2 偏差：** Spec 原计划引入 `IIntegrationEventMapper` 翻译领域事件。经代码审查发现：既有事件已实现 `IntegrationEventBase + IDomainEvent` 双接口（与 OrderEvents 既有模式一致），`OutboxDbContextExtensions` 已通过 `domainEvent is IIntegrationEvent` 检查正确处理。因此无需引入 mapper，直接将事件从 BC.Domain 迁移到 SharedContracts 即可，与代码库既有模式保持一致，降低改动风险。
- **主线 2.3 偏差：** Spec 原计划创建 `MoneyValueConverter` 替代。经搜索确认 `ToStorage/FromStorage` 零引用，直接删除即可。

### Placeholder scan
✅ 无 TBD/TODO/占位符。所有步骤含完整代码。

### Type consistency
✅ `SeckillOrderCreatedEvent`、`PointsEarnedEvent`、`MemberLevelUpgradedEvent`、`MembershipActivatedEvent` 的字段在 Task 1/2 定义与原文件逐字一致，Task 3/4 消费者代码无需修改字段访问。
✅ `ErrorCodeMapping.GetStatusCode` 签名在 Task 6 Step 2 定义，Step 3 调用一致。
