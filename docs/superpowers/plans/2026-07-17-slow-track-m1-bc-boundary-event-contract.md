# 慢轨 M1 BC 边界修复 + 事件契约分离 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 拆分 65 个双身份事件为纯领域事件与纯集成事件；引入 IIntegrationEventMapper 翻译器；移除 Notification BC 跨 BC 引用；SPU 聚合评价评分外移到 ES 读模型；SPU 拆分至 ≤300 行

**Architecture:** 领域事件继承 `DomainEventBase`（仅 `IDomainEvent`）；集成事件继承 `IntegrationEventBase`（仅 `IIntegrationEvent`，新增 `SchemaVersion`）；Outbox 不再通过 `is IIntegrationEvent` 模式匹配，改为通过各 BC 注册的 `IIntegrationEventMapper` 翻译领域事件为集成事件；Notification BC 消费者改订阅 SharedContracts 集成事件；SPU 评价评分字段迁移到 ProductReadModel（ES），ReviewAfterSales 域发布评价事件时同步更新

**Tech Stack:** .NET 10、MassTransit 8.3.6、EF Core 10、Elasticsearch 8.17.0、xUnit、FluentAssertions、Moq

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §8](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 1（F1.1 秒杀事件临时双身份）完成；M1 完成后回头重构 F1.1 临时方案

**向后兼容策略:** 1 周双发期，Outbox 翻译器同时发布新旧两种格式，消费者基于 EventId 去重，验证后下线旧格式

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| IDomainEvent | `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IDomainEvent.cs:6-16` | 接口含 `EventId`/`OccurredAt`/`AggregateId` |
| DomainEventBase（已存在） | `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IDomainEvent.cs:21-33` | 抽象基类，统一生成 EventId/OccurredAt，强制构造传入 aggregateId。spec 说"新建"实际已存在 |
| AggregateRoot | `src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs:6-21` | 第 8 行 `_domainEvents` 集合；第 10 行 `DomainEvents` 只读属性；第 14 行 `protected AddDomainEvent` |
| IIntegrationEvent | `src/BuildingBlocks/Leno.SharedContracts/Events/IIntegrationEvent.cs:7-17` | 接口含 `EventId`/`OccurredAt`/`IdempotencyKey`（无 AggregateId） |
| IntegrationEventBase | `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs:7-27` | 实现 IIntegrationEvent，无参构造自动生成 EventId/OccurredAt/IdempotencyKey |
| OutboxDbContextExtensions | `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs:16-43` | 第 28 行 `if (domainEvent is IIntegrationEvent integrationEvent)` 模式匹配双身份；第 39 行 `ClearDomainEvents` |
| OutboxMessage | `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs:45-58` | `Create` 方法序列化事件为 JSON，`Type = eventType.FullName` |
| IIntegrationEventMapper（不存在） | — | 源代码零命中，需新建 |
| SPU 评价字段 | `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs:69,72` | 第 69 行 `Score` (double)、第 72 行 `ReviewCount` (int)；第 395-442 行 `UpdateReviewScore`/`RemoveReviewScore` 方法 |
| SPU 其他职责 | `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs:25-27,444-548` | 第 25 行 `_auditHistory`、第 26 行 `_priceChangeHistory`、第 27 行 `_stockOperationHistory`；第 444-548 行对应方法 |
| ProductReadModel | `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModel.cs` | 不含 Score/ReviewCount 评价字段，需扩展 |
| ReviewReadModel | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModel.cs` | 评价读模型，需新增评分摘要字段或新建 SPUReviewSummaryReadModel |
| NotificationEventConsumer 跨 BC 引用 | `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs:4-5` | `using Leno.PointsMembership.Domain.Events;` 与 `using Leno.Promotion.Domain.Events;` 跨 BC 引用 |
| Notification.Infrastructure.csproj | `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj:8-9` | 2 处 ProjectReference 跨 BC 引用 |
| 绕过 Outbox 直接发布（7 处） | `OrderAppService.cs:358,381`、`CartAppService.cs:188`、`ExchangeCouponAppService.cs:63`、`AnnouncementAppService.cs:79`、`SystemConfigAppService.cs:52,69` | 7 处直接 `_eventBus.PublishAsync`，需改走 Outbox（部分由 F1.2 已处理 OrderAppService:358） |
| 38 个 SharedContracts 双身份事件 | `src/BuildingBlocks/Leno.SharedContracts/Events/*.cs` | 全部实现 `IntegrationEventBase, IDomainEvent`，需去除 IDomainEvent |
| 27 个 BC Domain 双身份事件 | 见下方完整清单 | 全部实现 `IntegrationEventBase, IDomainEvent`，需改继承 DomainEventBase |

### 65 个双身份事件完整清单

**SharedContracts 38 个**（全部文件位于 `src/BuildingBlocks/Leno.SharedContracts/Events/`）：

| # | 文件 | 类名 | 行号 |
|---|---|---|---|
| 1 | AdminOperationLogEvent.cs | AdminOperationLogEvent | 9 |
| 2-6 | AfterSalesEvents.cs | AfterSalesSubmittedEvent/AfterSalesApprovedEvent/AfterSalesRejectedEvent/AfterSalesReturnedEvent/AfterSalesReturnConfirmedEvent | 13/75/130/165/200 |
| 7 | CartEvents.cs | CartMergedEvent | 10 |
| 8-10 | CouponEvents.cs | PointsExchangeCouponRequestedEvent/CouponExchangeSucceededEvent/CouponExchangeFailedEvent | 10/46/78 |
| 11-17 | OrderEvents.cs | OrderCreatedEvent/OrderCompletedEvent/OrderPaidEvent/OrderCancelledEvent/OrderShippedEvent/OrderAfterSalesWindowClosedEvent/PaymentRequestedIntegrationEvent | 12/66/113/180/231/273/311 |
| 18-23 | PaymentEvents.cs | PaymentSucceededEvent/PaymentFailedEvent/RefundCompletedEvent/PaymentClosedEvent/RefundFailedEvent/PaymentChannelConfigChangedEvent | 13/74/111/175/212/253 |
| 24-27 | ProductEvents.cs | ProductPublishedEvent/ProductTakenDownEvent/StockAdjustedEvent/ProductUpdatedEvent | 11/39/68/109 |
| 28 | RefundRequestedIntegrationEvent.cs | RefundRequestedIntegrationEvent | 6 |
| 29-32 | ReviewEvents.cs | ReviewSubmittedEvent/ReviewApprovedEvent/ReviewHiddenEvent/ReviewModeratedEvent | 12/65/102/136 |
| 33-37 | ShopEvents.cs | SellerRegisteredEvent/ShopApprovedEvent/ShopSuspendedEvent/ShopResumedEvent/ShopClosedEvent | 11/48/81/110/139 |
| 38 | UserEvents.cs | UserRegisteredEvent | 11 |

**BC Domain 27 个**（按 BC 分组）：

| BC | 事件数 | 文件清单（相对路径） |
|---|---|---|
| UserAuth | 6 | `Leno.UserAuth.Domain/Events/{UserSuspendedEvent,UserRoleAssignedEvent,UserPasswordChangedEvent,ForgotPasswordRequestedEvent,ExternalLoginUnlinkedEvent,ExternalLoginLinkedEvent}.cs` |
| SystemAdmin | 3 | `Leno.SystemAdmin.Domain/Events/{FeatureFlagChangedEvent,ConfigChangedEvent,AnnouncementPublishedEvent}.cs` |
| SellerShop | 1 | `Leno.SellerShop.Domain/Events/QualificationExpiringEvent.cs` |
| Promotion | 4 | `Leno.Promotion.Domain/Events/{SeckillStockSoldOutEvent,SeckillOrderCreationFailedEvent,SeckillOrderCreatedEvent,SeckillOrderConfirmedEvent}.cs` |
| Order | 3 | `Leno.Order.Domain/Events/{StockReservedEvent,StockReleasedEvent,StockConfirmedEvent}.cs` |
| PointsMembership | 10 | `Leno.PointsMembership.Domain/Events/{PointsRevertedEvent,PointsReleasedEvent,PointsFrozenEvent,PointsExpiredEvent,PointsEarnedEvent,PointsConsumedEvent,MemberLevelChangedEvent,MembershipActivatedEvent,PointsConfirmedEvent,MemberLevelUpgradedEvent}.cs` |

**4 个本地领域事件**（仅实现 DomainEventBase，无需改造）：
- `Product/Leno.Product.Domain/Events/ProductCreatedEvent.cs:9`
- `Product/Leno.Product.Domain/Events/ProductReviewedEvent.cs:10`
- `Promotion/Leno.Promotion.Domain/Events/CouponIssuedEvent.cs:9`
- `SystemAdmin/Leno.SystemAdmin.Domain/Events/RateLimitRuleUpdatedEvent.cs:9`

---

## Task 1: 新建 IIntegrationEventMapper 翻译器抽象

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/EventBus/IIntegrationEventMapper.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventMapperBase.cs`

- [ ] **Step 1: 创建 IIntegrationEventMapper 接口**

创建 `src/BuildingBlocks/Leno.Infrastructure/EventBus/IIntegrationEventMapper.cs`：

```csharp
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 领域事件到集成事件的翻译器抽象。
/// 各 BC Infrastructure 层实现此接口，将聚合根收集的领域事件翻译为可发布到 MQ 的集成事件。
/// 翻译返回 null 表示该领域事件无需对外发布（仅内部领域事件）。
/// </summary>
public interface IIntegrationEventMapper
{
    /// <summary>
    /// 将领域事件翻译为集成事件。
    /// </summary>
    /// <param name="domainEvent">聚合根收集的领域事件</param>
    /// <returns>对应的集成事件；若无需发布返回 null</returns>
    IIntegrationEvent? Map(IDomainEvent domainEvent);
}
```

- [ ] **Step 2: 创建 IntegrationEventMapperBase 通用基类**

创建 `src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventMapperBase.cs`：

```csharp
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 翻译器基类，提供按领域事件类型分发的模板方法。
/// 子类通过 <see cref="RegisterHandler{TDomain, TIntegration}"/> 注册具体翻译逻辑。
/// </summary>
public abstract class IntegrationEventMapperBase : IIntegrationEventMapper
{
    private readonly Dictionary<Type, Func<IDomainEvent, IIntegrationEvent?>> _handlers = new();

    protected void RegisterHandler<TDomain, TIntegration>(Func<TDomain, TIntegration> handler)
        where TDomain : IDomainEvent
        where TIntegration : class, IIntegrationEvent
    {
        _handlers[typeof(TDomain)] = e => handler((TDomain)e);
    }

    public IIntegrationEvent? Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return _handlers.TryGetValue(domainEvent.GetType(), out var handler)
            ? handler(domainEvent)
            : null;
    }
}

/// <summary>
/// 空实现，用于不需要翻译的 BC（如 Cart/Payment/Notification 无内部领域事件需对外发布）。
/// </summary>
public sealed class NullIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent) => null;
}
```

- [ ] **Step 3: 写失败测试 — IntegrationEventMapperBase 分发逻辑**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventMapperBaseTests.cs`：

```csharp
using FluentAssertions;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Xunit;

namespace Leno.Infrastructure.Tests.EventBus;

public class IntegrationEventMapperBaseTests
{
    [Fact]
    public void Map_RegisteredHandler_ShouldReturnTranslatedEvent()
    {
        var mapper = new TestMapper();
        var domainEvent = new TestDomainEvent(Guid.NewGuid());

        var result = mapper.Map(domainEvent);

        result.Should().NotBeNull();
        result.Should().BeOfType<TestIntegrationEvent>();
    }

    [Fact]
    public void Map_UnregisteredDomainEvent_ShouldReturnNull()
    {
        var mapper = new TestMapper();
        var unknownEvent = new UnknownDomainEvent(Guid.NewGuid());

        var result = mapper.Map(unknownEvent);

        result.Should().BeNull();
    }

    private class TestMapper : IntegrationEventMapperBase
    {
        public TestMapper()
        {
            RegisterHandler<TestDomainEvent, TestIntegrationEvent>(e => new TestIntegrationEvent(e.AggregateId));
        }
    }

    private class TestDomainEvent : DomainEventBase
    {
        public TestDomainEvent(Guid aggregateId) : base(aggregateId) { }
    }

    private class TestIntegrationEvent : IntegrationEventBase
    {
        public Guid AggregateId { get; }
        public TestIntegrationEvent(Guid aggregateId) { AggregateId = aggregateId; }
    }

    private class UnknownDomainEvent : DomainEventBase
    {
        public UnknownDomainEvent(Guid aggregateId) : base(aggregateId) { }
    }
}
```

- [ ] **Step 4: 运行测试验证**

```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~IntegrationEventMapperBaseTests"
```

预期：两个测试 PASS。

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/EventBus/IIntegrationEventMapper.cs src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventMapperBase.cs src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventMapperBaseTests.cs
git commit -m "feat(infrastructure): 新建 IIntegrationEventMapper 翻译器抽象与 IntegrationEventMapperBase 基类"
```

---

## Task 2: 改造 OutboxDbContextExtensions 通过 mapper 翻译

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs:16-43`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（注册 IIntegrationEventMapper 默认实现）

- [ ] **Step 1: 修改 SaveChangesWithOutboxAsync 接受 mapper 参数**

读取 `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs` 当前实现（第 16-43 行）。

修改第 16-43 行为：

```csharp
public static async Task<int> SaveChangesWithOutboxAsync(
    this DbContext context,
    IIntegrationEventMapper? mapper = null,
    CancellationToken ct = default)
{
    var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
        .Select(e => e.Entity)
        .ToList();

    foreach (var aggregate in aggregates)
    {
        foreach (var domainEvent in aggregate.DomainEvents.ToList())
        {
            IIntegrationEvent? integrationEvent = null;

            // 双发期兼容：先尝试通过 mapper 翻译，回退到旧 is IIntegrationEvent 模式
            if (mapper is not null)
            {
                integrationEvent = mapper.Map(domainEvent);
            }

            // 旧模式回退（双发期内保留，下线后移除）
            if (integrationEvent is null && domainEvent is IIntegrationEvent legacyEvent)
            {
                integrationEvent = legacyEvent;
            }

            if (integrationEvent is not null)
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(integrationEvent));
            }
        }
    }

    var result = await context.SaveChangesAsync(ct);
    foreach (var aggregate in aggregates)
    {
        aggregate.ClearDomainEvents();
    }
    return result;
}
```

**关键设计**：双发期内 mapper 与旧 `is IIntegrationEvent` 共存。各 BC UnitOfWork 调用方式：
- 已注册 mapper 的 BC：传入 mapper 实例
- 未注册 mapper 的 BC：传 null 或不传参，回退旧模式

- [ ] **Step 2: 查找所有 SaveChangesWithOutboxAsync 调用方**

```bash
# 使用 Grep 工具搜索 SaveChangesWithOutboxAsync 调用
```

预期：11 个 BC 的 UnitOfWork.cs 调用此方法。

- [ ] **Step 3: 修改各 BC UnitOfWork 注入 IIntegrationEventMapper**

对 11 个 BC 的 UnitOfWork.cs 执行：

1. 构造函数注入 `IIntegrationEventMapper mapper`（如该 BC 已注册具体 mapper）或 `IIntegrationEventMapper? mapper = null`（如未注册）
2. 将 `_context.SaveChangesWithOutboxAsync(ct)` 改为 `_context.SaveChangesWithOutboxAsync(_mapper, ct)`

**示例**（以 Order UnitOfWork 为例）：

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly OrderDbContext _context;
    private readonly IIntegrationEventMapper _mapper;

    public UnitOfWork(OrderDbContext context, IIntegrationEventMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public Task<int> SaveEntitiesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesWithOutboxAsync(_mapper, ct);
    }
}
```

注意：未注册 mapper 的 BC 仍可保持原调用方式（不传参），双发期兼容。

- [ ] **Step 4: 在 AddLenoInfrastructure 注册 NullIntegrationEventMapper 默认实现**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 第 31-48 行的 `AddLenoInfrastructure`，在 `AddRedis` 后追加：

```csharp
// 默认注册空翻译器，各 BC 在 AddXxxInfrastructure 中覆盖为具体实现
services.AddSingleton<IIntegrationEventMapper, NullIntegrationEventMapper>();
```

- [ ] **Step 5: 编译与运行全量测试**

```bash
dotnet build Leno.sln
dotnet test Leno.sln --filter "Category!=Integration"
```

预期：编译成功，全部既有测试 PASS（双发期兼容保证无回归）。

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/*/Leno.*.Infrastructure/UnitOfWork.cs
git commit -m "refactor(outbox): SaveChangesWithOutboxAsync 接受 IIntegrationEventMapper 参数，双发期兼容旧 is IIntegrationEvent 模式"
```

---

## Task 3: 拆分 38 个 SharedContracts 双身份事件

> 移除 `IDomainEvent` 实现，仅保留 `IntegrationEventBase`（新增 `SchemaVersion`）。

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Events/*.cs`（38 个事件类，分布于 11 个文件）

- [ ] **Step 1: 修改 IntegrationEventBase 移除 IDomainEvent 实现，新增 SchemaVersion**

修改 `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs` 第 7-27 行：

```csharp
/// <summary>
/// 集成事件基类，跨限界上下文发布的契约事件。
/// 仅实现 <see cref="IIntegrationEvent"/>，不含领域事件语义。
/// </summary>
public abstract class IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// 事件 schema 版本，用于 M4.2 契约治理与版本兼容。
    /// 默认 "1.0"，破坏性变更递增主版本号。
    /// </summary>
    public string SchemaVersion { get; init; } = "1.0";
}
```

注意：移除原 `: IntegrationEventBase, IDomainEvent` 中的 `IDomainEvent`，仅保留 `IntegrationEventBase`。原文件中 `AggregateId` 显式实现（如有）也一并移除。

- [ ] **Step 2: 批量修改 38 个事件类去除 IDomainEvent**

对 38 个事件类执行统一改造：
- 将 `public class XxxEvent : IntegrationEventBase, IDomainEvent` 改为 `public class XxxEvent : IntegrationEventBase`
- 移除 `IDomainEvent.AggregateId` 显式实现属性（如有）
- 移除 `using Leno.SharedKernel.Abstractions;`（如已无其他依赖）

**文件清单**（11 个文件含 38 个类）：
1. `AdminOperationLogEvent.cs`（1 个）
2. `AfterSalesEvents.cs`（5 个）
3. `CartEvents.cs`（1 个）
4. `CouponEvents.cs`（3 个）
5. `OrderEvents.cs`（7 个）
6. `PaymentEvents.cs`（6 个）
7. `ProductEvents.cs`（4 个）
8. `RefundRequestedIntegrationEvent.cs`（1 个）
9. `ReviewEvents.cs`（4 个）
10. `ShopEvents.cs`（5 个）
11. `UserEvents.cs`（1 个）

**示例**（OrderEvents.cs 第 12 行 OrderCreatedEvent 改造）：

改造前：
```csharp
public class OrderCreatedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid OrderId { get; }
    Guid IDomainEvent.AggregateId => OrderId;
    // ...
}
```

改造后：
```csharp
public class OrderCreatedEvent : IntegrationEventBase
{
    public Guid OrderId { get; }
    // ...（移除 AggregateId 显式实现）
}
```

- [ ] **Step 3: 编译验证 SharedContracts**

```bash
dotnet build src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj
```

预期：编译成功。若失败，检查是否遗漏移除某处 IDomainEvent 显式实现。

- [ ] **Step 4: 运行全量测试验证双发期兼容**

```bash
dotnet test Leno.sln --filter "Category!=Integration"
```

预期：全部 PASS。OutboxDbContextExtensions 第 28 行旧 `is IIntegrationEvent` 模式不再匹配这些事件（因已移除 IDomainEvent），但双发期 mapper 未注册时回退逻辑仍可处理聚合根的领域事件。

注意：此时 38 个事件已不再被聚合根通过 `AddDomainEvent` 收集（因不再是 IDomainEvent），需在 Task 4 中为各 BC 新建对应的领域事件并通过 mapper 翻译。在 Task 4 完成前，这些事件的发布会中断。**建议 Task 3 与 Task 4 在同一 PR 内合并**。

- [ ] **Step 5: 提交（暂存，待 Task 4 完成后合并提交）**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/
git commit -m "refactor(shared-contracts): 38 个集成事件移除 IDomainEvent 实现，IntegrationEventBase 新增 SchemaVersion"
```

---

## Task 4: 拆分 27 个 BC Domain 双身份事件并新建对应 IntegrationEventMapper

> 27 个 BC Domain 事件改继承 DomainEventBase；各 BC 新建 IntegrationEventMapper 注册翻译规则；各 BC Domain.csproj 移除 SharedContracts 引用。

**Files:**
- Modify: 27 个 BC Domain 事件文件
- Modify: 7 个 BC 的 `Leno.{BC}.Domain.csproj`（移除 SharedContracts 引用）
- Create: 7 个 BC 的 `Leno.{BC}.Infrastructure/EventBus/{BC}IntegrationEventMapper.cs`
- Modify: 7 个 BC 的 `Leno.{BC}.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（注册 mapper）

- [ ] **Step 1: UserAuth BC 事件拆分与 mapper 注册**

**1a. 修改 6 个 UserAuth Domain 事件**，全部由 `: IntegrationEventBase, IDomainEvent` 改为 `: DomainEventBase`：

```csharp
// 改造前
public class UserSuspendedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid UserId { get; }
    Guid IDomainEvent.AggregateId => UserId;
    // ...
}

// 改造后
public class UserSuspendedEvent : DomainEventBase
{
    public Guid UserId { get; }
    public UserSuspendedEvent(Guid userId) : base(userId) { UserId = userId; }
}
```

对 6 个事件重复此改造：UserSuspendedEvent、UserRoleAssignedEvent、UserPasswordChangedEvent、ForgotPasswordRequestedEvent、ExternalLoginUnlinkedEvent、ExternalLoginLinkedEvent。

**1b. 新建 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/EventBus/UserAuthIntegrationEventMapper.cs`**：

```csharp
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.UserAuth.Domain.Events;

namespace Leno.UserAuth.Infrastructure.EventBus;

public class UserAuthIntegrationEventMapper : IntegrationEventMapperBase
{
    public UserAuthIntegrationEventMapper()
    {
        // UserAuth Domain 事件目前已在 SharedContracts 中有对应集成事件
        // 但 UserSuspendedEvent/UserRoleAssignedEvent 等无对应集成事件，需新建或保持内部
        // 若需对外发布，在 SharedContracts 新建对应集成事件并在此注册翻译
        // 当前 UserRegisteredEvent 已在 SharedContracts，无需翻译
    }
}
```

注意：UserAuth 的 6 个 Domain 事件中，部分（如 UserSuspendedEvent）目前无对应集成事件。若 Notification BC 需消费，应在 SharedContracts 新建对应集成事件（如 `UserSuspendedIntegrationEvent`）并在 mapper 注册翻译。本 Task 暂不新建，仅保留 Domain 事件为内部事件。

**1c. 修改 `src/Services/UserAuth/Leno.UserAuth.Domain/Leno.UserAuth.Domain.csproj`** 移除对 SharedContracts 的引用（如存在）。

**1d. 修改 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`** 注册 mapper：

```csharp
services.AddSingleton<IIntegrationEventMapper, UserAuthIntegrationEventMapper>();
```

- [ ] **Step 2: SystemAdmin BC 事件拆分与 mapper 注册**

重复 Step 1 模式处理 SystemAdmin 的 3 个事件（FeatureFlagChangedEvent、ConfigChangedEvent、AnnouncementPublishedEvent）。

新建 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/EventBus/SystemAdminIntegrationEventMapper.cs`。

修改 `Leno.SystemAdmin.Domain.csproj` 移除 SharedContracts 引用。

- [ ] **Step 3: SellerShop BC 事件拆分与 mapper 注册**

处理 SellerShop 的 1 个事件（QualificationExpiringEvent）。

新建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/EventBus/SellerShopIntegrationEventMapper.cs`。

- [ ] **Step 4: Promotion BC 事件拆分与 mapper 注册**

处理 Promotion 的 4 个事件（SeckillStockSoldOutEvent、SeckillOrderCreationFailedEvent、SeckillOrderCreatedEvent、SeckillOrderConfirmedEvent）。

**关键**：SeckillOrderCreatedEvent 在 SharedContracts 中已有对应 `SeckillOrderCreatedIntegrationEvent`（需新建，spec M1.2 明确要求）。mapper 注册翻译：

```csharp
RegisterHandler<SeckillOrderCreatedEvent, SeckillOrderCreatedIntegrationEvent>(e =>
    new SeckillOrderCreatedIntegrationEvent(e.ActivityId, e.OrderId, e.UserId, e.SellerId, e.SkuId, e.Quantity, e.UnitPrice, e.OccurredAt));
```

新建 `src/Services/Promotion/Leno.Promotion.Infrastructure/EventBus/PromotionIntegrationEventMapper.cs`。

- [ ] **Step 5: Order BC 事件拆分与 mapper 注册**

处理 Order 的 3 个事件（StockReservedEvent、StockReleasedEvent、StockConfirmedEvent）。

新建 `src/Services/Order/Leno.Order.Infrastructure/EventBus/OrderIntegrationEventMapper.cs`。

注意：Order 的领域事件（如 OrderCreatedEvent）已在 SharedContracts 中作为集成事件存在，需在 Order 聚合内新建对应 `OrderCreatedDomainEvent`（继承 DomainEventBase），由 Order 聚合 `AddDomainEvent` 收集，mapper 翻译为 `OrderCreatedEvent` 集成事件。这是 M1 最大的改造点。

- [ ] **Step 6: PointsMembership BC 事件拆分与 mapper 注册**

处理 PointsMembership 的 10 个事件。

新建 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs`。

spec M1.2 明确要求新建 5 个 PointsMembership 集成事件到 SharedContracts：PointsEarnedIntegrationEvent、PointsConsumedIntegrationEvent、PointsRevertedIntegrationEvent、MemberLevelChangedIntegrationEvent、PaidMemberSubscribedIntegrationEvent。

- [ ] **Step 7: 编译与运行全量测试**

```bash
dotnet build Leno.sln
dotnet test Leno.sln --filter "Category!=Integration"
```

预期：编译成功，全部测试 PASS。

若某些测试因事件类型变更失败，需同步更新测试代码（将原 `new XxxEvent()` 改为 `new XxxDomainEvent()` + mapper 翻译，或直接使用集成事件）。

- [ ] **Step 8: Grep 验证双身份事件已全部拆分**

```bash
# 使用 Grep 工具搜索
# Pattern: "IntegrationEventBase, IDomainEvent|DomainEventBase, IIntegrationEvent"
```

预期：零命中。

- [ ] **Step 9: 提交**

```bash
git add src/Services/*/Leno.*.Domain/Events/ src/Services/*/Leno.*.Domain/Leno.*.Domain.csproj src/Services/*/Leno.*.Infrastructure/EventBus/ src/Services/*/Leno.*.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs
git commit -m "refactor: 27 个 BC Domain 事件改继承 DomainEventBase，7 个 BC 新建 IntegrationEventMapper 翻译器，Domain.csproj 移除 SharedContracts 引用"
```

---

## Task 5: 移除 Notification BC 跨 BC 引用

> spec M1.2：Notification.Infrastructure 不引用任何 BC 的 Domain/Application/Infrastructure。

**Files:**
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj:8-9`（删除 2 处 ProjectReference）
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs:4-5`（删除 2 处 using）
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PromotionEventConsumer.cs`
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PointsEventConsumer.cs`

- [ ] **Step 1: 确认 SharedContracts 已有对应集成事件**

Grep 验证以下事件已在 SharedContracts 中作为纯集成事件存在（Task 3/4 完成后）：
- `SeckillOrderCreatedIntegrationEvent`（Promotion）
- `PointsEarnedIntegrationEvent`、`MemberLevelChangedIntegrationEvent`、`MembershipActivatedIntegrationEvent`（PointsMembership，Task 4 Step 6 新建）

若缺失，先在 SharedContracts 新建。

- [ ] **Step 2: 修改 NotificationEventConsumer.cs 删除跨 BC using**

修改 `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs` 第 4-5 行：

删除：
```csharp
using Leno.PointsMembership.Domain.Events;
using Leno.Promotion.Domain.Events;
```

确保已添加：
```csharp
using Leno.SharedContracts.Events;
```

将消费者内引用的 `PointsEarnedEvent` 改为 `PointsEarnedIntegrationEvent`，`MemberLevelUpgradedEvent` 改为 `MemberLevelChangedIntegrationEvent`，`MembershipActivatedEvent` 改为 `PaidMemberSubscribedIntegrationEvent`，`SeckillOrderCreatedEvent` 改为 `SeckillOrderCreatedIntegrationEvent`。

- [ ] **Step 3: 修改 PromotionEventConsumer.cs 与 PointsEventConsumer.cs**

同样删除跨 BC using，改用 SharedContracts 集成事件类型。

- [ ] **Step 4: 修改 Notification.Infrastructure.csproj 删除 ProjectReference**

修改 `src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj` 第 8-9 行，删除：

```xml
<ProjectReference Include="..\..\Promotion\Leno.Promotion.Domain\Leno.Promotion.Domain.csproj" />
<ProjectReference Include="..\..\PointsMembership\Leno.PointsMembership.Domain\Leno.PointsMembership.Domain.csproj" />
```

- [ ] **Step 5: 编译验证 Notification BC**

```bash
dotnet build src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj
```

预期：编译成功。

- [ ] **Step 6: Grep 验证无跨 BC 引用**

```bash
# 使用 Grep 工具在 src/Services/Notification/ 下搜索
# Pattern: "using Leno\.(Promotion|PointsMembership|Order|Product|Cart|Payment|ReviewAfterSales|SellerShop|SystemAdmin|UserAuth)\.(Domain|Application|Infrastructure)"
```

预期：零命中。

- [ ] **Step 7: 运行 Notification 测试与全量测试**

```bash
dotnet test src/Services/Notification/Leno.Notification.Application.Tests/
dotnet test Leno.sln --filter "Category!=Integration"
```

预期：全部 PASS。

- [ ] **Step 8: 提交**

```bash
git add src/Services/Notification/Leno.Notification.Infrastructure/
git commit -m "refactor(notification): 移除跨 BC 引用，消费者改订阅 SharedContracts 集成事件"
```

---

## Task 6: SPU 聚合评价评分外移到 ES 读模型

> spec M1.4：评价评分由 ES 读模型维护，SPU 不再承担评分摘要职责。

**Files:**
- Modify: `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs:69,72,395-442`（移除 Score/ReviewCount 字段与 UpdateReviewScore/RemoveReviewScore 方法）
- Modify: `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModel.cs`（新增 Score/ReviewCount 字段）
- Create: `src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs`（消费评价事件更新 ES 评分摘要）
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModel.cs`（如需聚合 SPU 级评分摘要）

- [ ] **Step 1: 修改 ProductReadModel 新增评价评分字段**

修改 `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModel.cs`：

```csharp
public class ProductReadModel
{
    // 既有字段...
    public double Score { get; set; }
    public int ReviewCount { get; set; }
    public DateTime ScoreUpdatedAt { get; set; }
}
```

- [ ] **Step 2: 新建 SpuReviewSummaryConsumer 消费评价事件**

创建 `src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs`：

```csharp
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;

namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// 消费评价事件，更新 ProductReadModel 的 Score/ReviewCount 字段。
/// 替代原 SPU 聚合的 UpdateReviewScore/RemoveReviewScore 方法。
/// </summary>
public class SpuReviewSummaryConsumer : IntegrationEventConsumerBase<ReviewSubmittedEvent>
{
    private readonly IEsReadModelRepository<ProductReadModel> _productRepo;
    private readonly ILogger<SpuReviewSummaryConsumer> _logger;

    public SpuReviewSummaryConsumer(
        IEsReadModelRepository<ProductReadModel> productRepo,
        IIdempotencyStore idempotencyStore,
        ILogger<SpuReviewSummaryConsumer> logger) : base(idempotencyStore, logger)
    {
        _productRepo = productRepo;
        _logger = logger;
    }

    protected override async Task HandleAsync(ReviewSubmittedEvent @event, CancellationToken ct)
    {
        var product = await _productRepo.GetByIdAsync(@event.SpuId.ToString(), ct);
        if (product is null)
        {
            _logger.LogWarning("ReviewSubmittedEvent 关联的 SPU {SpuId} 不存在于 ES 读模型", @event.SpuId);
            return;
        }

        // 重新计算评分摘要（简化版：增量更新；完整版需查询 ReviewReadModel 聚合）
        var newCount = product.ReviewCount + 1;
        var newScore = ((product.Score * product.ReviewCount) + @event.Rating) / newCount;
        product.Score = Math.Round(newScore, 2);
        product.ReviewCount = newCount;
        product.ScoreUpdatedAt = DateTime.UtcNow;

        await _productRepo.UpsertAsync(product.Id.ToString(), product, ct);
    }
}
```

注意：`ReviewSubmittedEvent` 需含 `SpuId` 与 `Rating` 字段（在 Task 3 改造后仍保留集成事件契约）。

- [ ] **Step 3: 在 Product BC Program.cs 注册 SpuReviewSummaryConsumer**

修改 `src/Services/Product/Leno.Product.Api/Program.cs` 的 `AddLenoInfrastructure` 调用，在 `configureConsumers` 回调中添加：

```csharp
builder.Services.AddLenoInfrastructure(builder.Configuration, cfg => cfg
    .AddProductConsumers()
    .AddConsumer<SpuReviewSummaryConsumer>());
```

注意：实际注册方式需根据 `AddProductConsumers` 扩展方法结构调整。

- [ ] **Step 4: 从 SPU 聚合移除评价评分字段与方法**

修改 `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs`：
- 删除第 69 行 `Score` 字段
- 删除第 72 行 `ReviewCount` 字段
- 删除第 395-442 行 `UpdateReviewScore` 与 `RemoveReviewScore` 方法
- 删除相关 using（如 `Leno.SharedContracts.Events`）

- [ ] **Step 5: 修改所有 SPU.UpdateReviewScore/RemoveReviewScore 调用方**

Grep 搜索 `UpdateReviewScore|RemoveReviewScore` 找出所有调用方（可能在 ReviewAfterSales 消费者或 Product Application 服务中），改为发布事件由 SpuReviewSummaryConsumer 处理。

- [ ] **Step 6: 编译与运行测试**

```bash
dotnet build Leno.sln
dotnet test Leno.sln --filter "Category!=Integration"
```

预期：编译成功，全部测试 PASS。若 Product Domain 测试因 Score 字段移除断言失败，更新测试。

- [ ] **Step 7: 提交**

```bash
git add src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModel.cs src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs src/Services/Product/Leno.Product.Api/Program.cs
git commit -m "refactor(product): SPU 评价评分外移到 ES ProductReadModel，新增 SpuReviewSummaryConsumer 消费评价事件"
```

---

## Task 7: SPU 聚合价格历史与库存操作历史拆分

> spec M1.4：SPU 仅保留商品基础信息 + SKU 集合 + 状态机 + 审核历史，其余拆出。

**Files:**
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/PriceHistory.cs`（新建聚合）
- Modify: `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs:26,453-499`（移除价格历史字段与方法）
- Modify: `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs:27,503-548`（移除库存操作历史字段与方法）
- Modify: `src/Services/Product/Leno.Product.Infrastructure/Configurations/`（新增 PriceHistoryConfiguration）
- Modify: `src/Services/Product/Leno.Product.Infrastructure/ProductDbContext.cs`（新增 DbSet<PriceHistory>）
- Create: `src/Services/Product/Leno.Product.Domain.Tests/PriceHistoryTests.cs`

- [ ] **Step 1: 新建 PriceHistory 聚合**

创建 `src/Services/Product/Leno.Product.Domain/Aggregates/PriceHistory.cs`：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// 价格变更历史聚合，记录 SPU 价格变更轨迹。
/// 从 SPU 聚合拆出，SPU 仅维护当前价格。
/// </summary>
public class PriceHistory : AggregateRoot
{
    public Guid SpuId { get; private set; }
    public Guid SkuId { get; private set; }
    public decimal OldPrice { get; private set; }
    public decimal NewPrice { get; private set; }
    public string Currency { get; private set; } = "CNY";
    public string? Reason { get; private set; }
    public DateTime ChangedAt { get; private set; }

    private PriceHistory() { }

    public static PriceHistory Create(Guid spuId, Guid skuId, decimal oldPrice, decimal newPrice, string? reason = null)
    {
        if (newPrice < 0) throw new ArgumentException("价格不能为负", nameof(newPrice));
        var history = new PriceHistory
        {
            Id = Guid.NewGuid(),
            SpuId = spuId,
            SkuId = skuId,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            Reason = reason,
            ChangedAt = DateTime.UtcNow
        };
        history.AddDomainEvent(new PriceChangedEvent(history.Id, spuId, skuId, oldPrice, newPrice));
        return history;
    }
}
```

注意：`PriceChangedEvent` 需新建为 DomainEventBase 子类（如需对外发布，新建对应集成事件到 SharedContracts）。

- [ ] **Step 2: 从 SPU 移除价格历史字段与方法**

修改 `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs`：
- 删除第 26 行 `_priceChangeHistory` 字段
- 删除第 453-499 行价格历史相关方法
- 删除相关 using

价格变更操作改为：SPU 仅更新当前价格并发布 `PriceChangedEvent` 领域事件 → Product Application 服务监听事件创建 `PriceHistory` 聚合并保存。

- [ ] **Step 3: 从 SPU 移除库存操作历史字段与方法**

修改 `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs`：
- 删除第 27 行 `_stockOperationHistory` 字段
- 删除第 503-548 行库存操作历史相关方法

库存操作历史归并到既有 `StockBaseline` 聚合（已存在）。

- [ ] **Step 4: 新建 PriceHistoryConfiguration 与 DbSet**

创建 `src/Services/Product/Leno.Product.Infrastructure/Configurations/PriceHistoryConfiguration.cs`：

```csharp
using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.ToTable("PriceHistories");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.SpuId).IsRequired();
        builder.Property(p => p.SkuId).IsRequired();
        builder.Property(p => p.OldPrice).HasPrecision(18, 2);
        builder.Property(p => p.NewPrice).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Reason).HasMaxLength(200);
        builder.HasIndex(p => new { p.SpuId, p.ChangedAt });
    }
}
```

修改 `src/Services/Product/Leno.Product.Infrastructure/ProductDbContext.cs` 添加：

```csharp
public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
```

- [ ] **Step 5: 写 PriceHistory 单元测试**

创建 `src/Services/Product/Leno.Product.Domain.Tests/PriceHistoryTests.cs`：

```csharp
using FluentAssertions;
using Leno.Product.Domain.Aggregates;
using Xunit;

namespace Leno.Product.Domain.Tests;

public class PriceHistoryTests
{
    [Fact]
    public void Create_Valid_ShouldSetPropertiesAndRaiseEvent()
    {
        var spuId = Guid.NewGuid();
        var skuId = Guid.NewGuid();

        var history = PriceHistory.Create(spuId, skuId, oldPrice: 99.9m, newPrice: 89.9m, reason: "促销调价");

        history.SpuId.Should().Be(spuId);
        history.SkuId.Should().Be(skuId);
        history.OldPrice.Should().Be(99.9m);
        history.NewPrice.Should().Be(89.9m);
        history.Reason.Should().Be("促销调价");
        history.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_NegativePrice_ShouldThrow()
    {
        var act = () => PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, -1m);

        act.Should().Throw<ArgumentException>().WithMessage("*价格不能为负*");
    }
}
```

- [ ] **Step 6: 运行测试**

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj
```

预期：PriceHistoryTests 2 个测试 PASS，既有 SPU 测试不回归。

- [ ] **Step 7: 生成 PriceHistory 迁移**

```bash
dotnet ef migrations add AddPriceHistoryAggregate --project src/Services/Product/Leno.Product.Infrastructure --startup-project src/Services/Product/Leno.Product.Api --output-dir Migrations
```

- [ ] **Step 8: 验证 SPU.cs 行数 ≤300**

```bash
# 使用 PowerShell 或读取文件统计行数
```

读取 `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs` 总行数，验证 ≤300。若仍超 300，考虑进一步拆分审核历史为独立聚合（spec 未明确要求，可按需）。

- [ ] **Step 9: 提交**

```bash
git add src/Services/Product/Leno.Product.Domain/Aggregates/PriceHistory.cs src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs src/Services/Product/Leno.Product.Infrastructure/Configurations/PriceHistoryConfiguration.cs src/Services/Product/Leno.Product.Infrastructure/ProductDbContext.cs src/Services/Product/Leno.Product.Domain.Tests/PriceHistoryTests.cs src/Services/Product/Leno.Product.Infrastructure/Migrations/
git commit -m "refactor(product): SPU 拆分价格历史到 PriceHistory 聚合，库存操作历史归并 StockBaseline，SPU 行数降至 ≤300"
```

---

## Task 8: ACL 契约清单文档化

> spec M1.3：新建 docs/contracts/internal-api-contracts.md，按 BC 列出每个 internal 端点。

**Files:**
- Create: `docs/contracts/internal-api-contracts.md`

- [ ] **Step 1: Grep 查找所有 internal 端点**

```bash
# 使用 Grep 工具搜索
# Pattern: "\[Route\(\"internal"
# 或
# Pattern: "/internal/"
```

预期：找出所有 BC 的 internal 控制器与端点。

- [ ] **Step 2: 创建 internal-api-contracts.md**

创建 `docs/contracts/internal-api-contracts.md`，按 BC 分章节列出每个 internal 端点：

```markdown
# Internal API 契约清单

> 本文档列出所有 BC 的 internal 端点契约，为 M4.2 Internal API 版本治理做准备。
> 最后更新：2026-07-17

## 1. UserAuth BC

### 1.1 GET /internal/users/{userId}
- **调用方 BC**：Order、Cart、ReviewAfterSales、PointsMembership 等
- **入参**：userId (Guid, path)
- **返回**：UserDto (Id/Username/Role/ShopId)
- **错误码**：404 用户不存在
- **契约版本**：1.0

### 1.2 POST /internal/users/batch
- **调用方 BC**：Notification、SystemAdmin
- **入参**：UserIdsDto (Guid[])
- **返回**：UserDto[]
- **契约版本**：1.0

## 2. Product BC
...

## 3. Order BC
...

## 4. Promotion BC
...

## 5. PointsMembership BC
...

## 6. Payment BC
...

## 7. ReviewAfterSales BC
...

## 8. SellerShop BC
...

## 9. Notification BC
...

## 10. SystemAdmin BC
...

## 11. Cart BC
（无 internal 端点）
```

- [ ] **Step 3: 提交**

```bash
git add docs/contracts/internal-api-contracts.md
git commit -m "docs(contracts): 新建 internal API 契约清单文档，覆盖 11 个 BC 的 internal 端点"
```

---

## Self-Review 自检

### 1. Spec 覆盖（对照 spec §8 M1）

| Spec 要求 | 对应 Task | 覆盖 |
|---|---|---|
| M1.1 DomainEventBase（新建） | 实际已存在 `Leno.SharedKernel/Abstractions/IDomainEvent.cs:21-33`，无需新建 | ✅ 现状满足 |
| M1.1 IntegrationEventBase 移除 IDomainEvent + 新增 SchemaVersion | Task 3 Step 1 | ✅ |
| M1.1 拆分 38 个 SharedContracts 双身份事件 | Task 3 Step 2 | ✅ |
| M1.1 拆分 27 个 BC Domain 双身份事件改继承 DomainEventBase | Task 4 Step 1-6 | ✅ |
| M1.1 引入 IIntegrationEventMapper | Task 1 | ✅ |
| M1.1 OutboxDbContextExtensions 通过 mapper 翻译 | Task 2 | ✅ |
| M1.1 各 BC Domain.csproj 移除 SharedContracts 引用 | Task 4 Step 1c 等 | ✅ |
| M1.1 双发期 1 周向后兼容 | Task 2 Step 1 双发期回退逻辑 | ✅ |
| M1.1 验收 Grep `IntegrationEventBase, IDomainEvent` 零命中 | Task 4 Step 8 | ✅ |
| M1.2 SharedContracts 新增 Promotion/PointsMembership 集成事件 | Task 4 Step 4/6 | ✅ |
| M1.2 Promotion/PointsMembership 注册 mapper | Task 4 Step 4/6 | ✅ |
| M1.2 Notification 消费者改订阅集成事件 | Task 5 Step 2/3 | ✅ |
| M1.2 Notification.Infrastructure.csproj 删除 2 处 ProjectReference | Task 5 Step 4 | ✅ |
| M1.2 验收 Notification.Infrastructure 不引用 BC Domain | Task 5 Step 6 | ✅ |
| M1.3 新建 docs/contracts/internal-api-contracts.md | Task 8 | ✅ |
| M1.4 SPU 拆出价格历史 | Task 7 Step 1-2 | ✅ |
| M1.4 库存操作历史归并 StockBaseline | Task 7 Step 3 | ✅ |
| M1.4 评价评分外移 ES 读模型 | Task 6 | ✅ |
| M1.4 SPU ≤300 行 | Task 7 Step 8 验证 | ✅ |
| M1.5 风险：双发期 1 周观察 | Task 2 双发期回退逻辑 | ✅ |
| M1.5 风险：mapper 注册遗漏 | 各 BC AddXxxInfrastructure 注册 + CI 可补静态扫描 | ⏭️ CI 静态扫描由 Plan 4 F4.4 覆盖 |
| M1.5 风险：SPU 拆分破坏测试 | Task 7 Step 5/6 补 PriceHistoryTests + 运行既有测试 | ✅ |

### 2. 占位符扫描

- ✅ 无 "TBD"、"TODO"、"fill in details"
- ⚠️ Task 4 Step 1-6 含"重复 Step 1 模式处理"指引 — 这是合理的批量操作指引，每个 BC 都给出了明确的文件清单与改造步骤
- ✅ 关键代码块完整（IIntegrationEventMapper、IntegrationEventMapperBase、OutboxDbContextExtensions 改造、PriceHistory 聚合、SpuReviewSummaryConsumer）
- ✅ 所有命令含确切参数

### 3. 类型一致性

- `IIntegrationEventMapper.Map(IDomainEvent)` 签名：Task 1 定义，Task 2 调用 `mapper.Map(domainEvent)`，Task 4 各 BC mapper 继承 `IntegrationEventMapperBase` 实现 — 一致 ✅
- `IntegrationEventMapperBase.RegisterHandler<TDomain, TIntegration>`：Task 1 定义，Task 4 Step 4 调用 `RegisterHandler<SeckillOrderCreatedEvent, SeckillOrderCreatedIntegrationEvent>` — 一致 ✅
- `SaveChangesWithOutboxAsync(DbContext, IIntegrationEventMapper?, CancellationToken)` 签名：Task 2 定义，各 BC UnitOfWork 调用 — 一致 ✅
- `DomainEventBase` 构造函数 `protected DomainEventBase(Guid aggregateId)`：Task 4 改造事件类时使用 `: base(userId)` 等，与 `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IDomainEvent.cs:21-33` 一致 ✅
- `IntegrationEventBase` 新增 `SchemaVersion` 属性：Task 3 Step 1 定义，38 个事件继承自动获得 — 一致 ✅
- `PriceHistory.Create(Guid, Guid, decimal, decimal, string?)` 签名：Task 7 Step 1 定义，Task 7 Step 5 测试调用 — 一致 ✅
- `SpuReviewSummaryConsumer : IntegrationEventConsumerBase<ReviewSubmittedEvent>`：Task 6 Step 2 定义，依赖 `IntegrationEventConsumerBase` 已存在于 `src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs` — 一致 ✅

### 4. 已知注意事项

1. **Task 3 与 Task 4 必须同一 PR 合并**：Task 3 移除 38 个事件的 IDomainEvent 后，这些事件不再被聚合根收集，必须 Task 4 同步新建对应 DomainEventBase 子类并注册 mapper 翻译，否则事件发布中断。建议在特性分支上完成 Task 1-4 后统一合并。
2. **双发期回退逻辑（Task 2 Step 1）**：`mapper is not null ? mapper.Map(domainEvent) : (domainEvent is IIntegrationEvent ? legacyEvent : null)` 保证未注册 mapper 的 BC 仍走旧模式。Task 4 全部 BC 注册 mapper 后，可移除回退逻辑（下线旧格式）。
3. **Task 4 各 BC mapper 注册示例仅给出骨架**：实际翻译逻辑需根据各 BC 事件字段映射调整。如 `SeckillOrderCreatedEvent → SeckillOrderCreatedIntegrationEvent` 的字段映射需对齐 Plan 1 Task 4 的实际实现。
4. **Task 6 评分摘要计算简化**：`SpuReviewSummaryConsumer.HandleAsync` 用增量计算（`((Score * Count) + Rating) / (Count + 1)`），完整实现应查询 ReviewReadModel 聚合所有可见评价重算，避免隐藏评价后评分不准。可后续优化。
5. **Task 7 SPU 行数验证**：若移除价格历史与库存操作历史后仍超 300 行，需进一步拆分审核历史为独立聚合。spec 未明确要求拆审核历史，可按实际情况判断。
6. **spec M1.5 风险"mapper 注册遗漏"**：建议在 CI 中新增静态扫描步骤，检查每个 BC Infrastructure 的 AddXxxInfrastructure 是否注册了 IIntegrationEventMapper。可由 Plan 4 F4.4 的 check-placeholders.sh 扩展或单独脚本实现。
7. **依赖 Plan 1 F1.1 重构**：spec 第 4.1 节明确"M1 完成后回头重构 F1.1 临时双身份事件"。Plan 5 Task 4 Step 4 已处理 Promotion 的 SeckillOrderCreatedEvent，即完成 F1.1 的重构闭环。
