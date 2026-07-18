# Leno 电商平台全面优化方案设计

> **⚠️ SUPERSEDED**: 本 spec 已被 [2026-07-17-comprehensive-optimization-v2-design.md](./2026-07-17-comprehensive-optimization-v2-design.md) 接管。
>
> 本文档保留作为历史参考，不再代表当前架构决策。当前实施请以 V2 spec 为准。
>
> **接管关系**：
> - V1 spec 的快轨 4 个 Wave（F1-F4）已由 V2 spec §5-§8 接管并实施完成
> - V1 spec 的慢轨 6 个里程碑（M1-M6）已由 V2 spec §9-§14 接管，部分已实施完成
>
> **接管日期**: 2026-07-17

**文档版本**：V1.0
**创建日期**：2026-07-13
**适用范围**：Leno 电商平台全部 11 个限界上下文（BC1-BC11）及共享内核
**触发原因**：对 /workspace 仓库全面分析后识别出架构边界违规、共享内核泄漏、大量样板重复、占位实现、测试覆盖不均、CQRS 未落地、网关能力不足等问题

---

## 1 背景与目标

### 1.1 项目现状

Leno 是基于 .NET 10 的 DDD 微服务电商平台，按 11 个限界上下文拆分：

- 6 个核心域：用户认证、商品、购物车、订单交易、促销、评价售后
- 3 个支撑域：积分会员、支付集成、卖家店铺
- 2 个通用子域：消息通知、系统管理

文档体系完善（13 篇 spec + 编码规范），代码层面已有 2153+ 个单元测试。但全面分析后发现多个值得优化的问题（详见第 2 节）。

### 1.2 既有优化工作

本方案与已完成的 spec 不重复：

- [.trae/specs/refactor-to-microservices](../../../.trae/specs/refactor-to-microservices/spec.md)：服务容器化、API 网关、健康端点、CI 独立化（已落地）
- [.trae/specs/replace-placeholder-implementations](../../../.trae/specs/replace-placeholder-implementations/spec.md)：30 处业务占位实现替换（已落地）
- [.trae/specs/p0-task-completion](../../../.trae/specs/p0-task-completion/)、[p1-task-completion](../../../.trae/specs/p1-task-completion/)、[p2-task-completion](../../../.trae/specs/p2-task-completion/)：分阶段任务完成

本方案聚焦**架构合规性、代码质量、通用能力重构**三类未被既有 spec 覆盖的优化点。

### 1.3 优化目标

1. 消除限界上下文边界违规，恢复 DDD 战略设计约束
2. 清理共享内核中泄漏的技术细节，回归"领域内核"职责
3. 消除跨 BC 大量重复样板代码，提升可维护性
4. 建立占位实现零容忍机制，确保代码真实可用
5. 补强测试覆盖，建立覆盖率门槛
6. 落地 CQRS 读写分离，提升读多场景性能
7. 增强 API 网关能力（限流、熔断、健康聚合）
8. 同步通信迁移至 gRPC，提升跨服务性能与契约治理
9. 同步更新编码规范与 spec

---

## 2 问题分析

### 2.1 限界上下文边界违规（P0 最严重）

[Leno.Notification.Infrastructure.csproj](../../../src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj) 直接 `ProjectReference` 了 `Promotion.Domain` 和 `PointsMembership.Domain`，且 [NotificationEventConsumer.cs](../../../src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs) 直接 `using Leno.Promotion.Domain.Events` 与 `Leno.PointsMembership.Domain.Events`，订阅以下**领域事件**：

- `SeckillOrderCreatedEvent`（Promotion）
- `PointsEarnedEvent`、`MemberLevelUpgradedEvent`、`MembershipActivatedEvent`（PointsMembership）

这是 DDD 最严重的违规——上下文间应只通过集成事件契约（位于 `Leno.SharedContracts`）通信。Promotion/PointsMembership 把领域事件直接发布到 MQ 总线，其他 BC 直接订阅。一旦 Promotion 重构 `SeckillOrderCreatedEvent` 内部字段，Notification 编译即断裂；运行时若版本不一致，反序列化会静默失败。

### 2.2 共享内核职责混乱（P0）

[Leno.SharedKernel](../../../src/BuildingBlocks/Leno.SharedKernel/) 当前混合 4 类职责：

| 职责 | 位置 | 是否合理 |
|---|---|---|
| 领域抽象（AggregateRoot、Entity、IRepository 等） | Abstractions/ | 合理 |
| 通用值对象（Money、PageRequest） | ValueObjects/ | 合理 |
| 基础设施抽象（ICacheService、IEventBus、IFileStorageService 等） | Abstractions/ | **越界**：实现在 Infrastructure，应迁移 |
| 持久化细节（MoneyJsonConverter.ToStorage、Entity.Version） | ValueObjects/ + Abstractions/ | **越界**：泄漏 SQL Server rowversion 与 EF Core 值转换器 |

具体越界点：

1. [Entity.cs:41](../../../src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs) `Version` 字段注释"SQL Server rowversion"，`byte[]` 是 EF Core/SQL Server 并发令牌格式
2. [MoneyJsonConverter.cs:66-90](../../../src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs) `ToStorage/FromStorage` 为 EF Core 值转换器服务，存储格式 `amount|currency`
3. [DomainException.cs:13](../../../src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs) 携带 `HttpStatusCode`，被 [GlobalExceptionMiddleware.cs](../../../src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs) 直接读取
4. `ICacheService`、`IBloomFilter`、`IFileStorageService`、`IExternalChannelOptions` 在 SharedKernel.Abstractions，但实现在 Leno.Infrastructure
5. `PageResult<T>` 在 SharedKernel.ValueObjects 与 SharedContracts.Responses 双定义，字段完全相同

### 2.3 跨 BC 样板重复（P1）

11 份 [UnitOfWork.cs](../../../src/Services/Order/Leno.Order.Infrastructure/UnitOfWork.cs) 仅 DbContext 类型不同，其余 56 行逐字相同，包含内部嵌套类 `EfCoreUnitOfWorkTransaction` 被复制 11 次。

11 份 `Program.cs` 高度相似（70+ 行中仅 6 处差异：using、注释、AddXxxConsumers、AddXxxInfrastructure、AddDbContextCheck）。

每个 DbContext 重复声明 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();`。

### 2.4 占位实现残留（P1）

- 1 个 0 字节空文件：[NewFeatureTests.cs](../../../src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/NewFeatureTests.cs)
- 15 个 `SmokeTest_ShouldPass` 占位测试（Cart.Domain.Tests、Order.{Api/Application/Domain/Infrastructure}.Tests、Promotion.{Application/Domain/Infrastructure}.Tests、Product.{Api/Application/Domain/Infrastructure}.Tests、UserAuth.{Api/Application/Infrastructure}.Tests）
- 7 个仅含 `GlobalUsings.cs` 的空测试项目（ReviewAfterSales.Api/Application.Tests、SellerShop.Api/Application.Tests、Notification.Api.Tests、SystemAdmin.Api/Application.Tests）
- 3 个缺失 Infrastructure.Tests 项目（Cart、PointsMembership、SystemAdmin）
- [IntegrationEventConsumerBase](../../../src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs) 默认空幂等实现形同虚设
- 与 [replace-placeholder-implementations spec](../../../.trae/specs/replace-placeholder-implementations/spec.md) 的差异：既有 spec 已处理 30 处业务占位，本方案聚焦占位**机制**（CI 强制禁止、规范约束）与剩余测试占位

### 2.5 测试覆盖不均（P1）

| 服务 | Domain.Tests | Application.Tests | Api.Tests | Infrastructure.Tests |
|---|---|---|---|---|
| Payment | 64 | 8 | 13 | 86（完善）|
| Product | 110 | 20 | 13 | 25 |
| Order | 82 | 22 | 10 | 14 |
| Promotion | 90 | 27 | 25 | 11 |
| PointsMembership | 168 | 10 | 21 | **无项目** |
| SellerShop | 288 | **0（空）** | **0（空）** | 无项目 |
| ReviewAfterSales | 124 | **0（空）** | **0（空）** | 无项目 |
| SystemAdmin | 460 | **0（空）** | **0（空）** | **无项目** |
| Notification | 211 | 75 | **0（空）** | 无项目 |
| Cart | 51 | 18 | 8 | **无项目** |
| UserAuth | 79+ | 1 占位 | 1 占位 | 1 占位 |

SellerShop、ReviewAfterSales、SystemAdmin 的 Api/Application 层完全无测试。多个 AppService 实现未被测试覆盖。

### 2.6 CQRS 未落地（P2）

[编码规范.md](../../../docs/编码规范.md) 第 7 章描述了 CQRS，但代码中无任何 Command/Query 分离（`Glob src/Services/**/{Commands,Queries}/**/*.cs` 返回 0 个匹配）。11 个 BC 全部用单一 `IXxxAppService` 模式。

只有 Product、Order、ReviewAfterSales 3 个 BC 实现了 ES 读模型同步，其余 8 个 BC 查询直接读写库。

### 2.7 API 网关能力不足（P2）

[Leno.ApiGateway/Program.cs:26-69](../../../src/ApiGateway/Leno.ApiGateway/Program.cs) 手工 `/health` 轮询重复 YARP 能力；未启用 YARP 的限流、熔断、缓存策略；无 BFF 聚合。

### 2.8 同步通信性能与契约治理（P2 新增）

当前跨 BC 同步调用走 HttpClient + JSON（[CartPriceService](../../../src/Services/Cart/Infrastructure/Services/CartPriceService.cs) 等 10+ 处防腐层）。性能、类型安全、契约治理均有提升空间。

---

## 3 设计原则与优先级框架

### 3.1 设计原则

1. **不破坏既有功能** — 所有重构必须保持现有 2153+ 个单元测试通过
2. **增量演进** — 按 P0→P1→P2 顺序推进，每个 PR 可独立验证、独立回滚
3. **遵循既有 spec 与编码规范** — 复用 [docs/spec/](../../../docs/spec/) 与 [编码规范.md](../../../docs/编码规范.md) 已确立的 DDD/CQRS/事件驱动约束
4. **与已完成 spec 不重复** — 不覆盖 [refactor-to-microservices](../../../.trae/specs/refactor-to-microservices/spec.md) 与 [replace-placeholder-implementations](../../../.trae/specs/replace-placeholder-implementations/spec.md) 已完成的工作
5. **测试先行** — 任何重构前先补关键路径测试，重构后再验证

### 3.2 优先级定义

| 级别 | 含义 | 触发条件 |
|---|---|---|
| P0 | 阻塞生产/严重违反 DDD | 限界上下文边界被穿透、领域层泄漏技术细节 |
| P1 | 重大质量/可维护性问题 | 大量重复代码、占位实现、测试覆盖严重缺失 |
| P2 | 改进性优化 | CQRS 落地、网关增强、gRPC 迁移等 |

### 3.3 优化路线图全景

| # | 优化主线 | 优先级 | 子任务数 | 依赖 |
|---|---|---|---|---|
| 1 | 限界上下文边界修复 | P0 | 4 | 无 |
| 2 | 共享内核职责清理 | P0 | 5 | 无 |
| 3 | 跨 BC 样板去重 | P1 | 4 | 主线 2 |
| 4 | 占位实现禁止与替换 | P1 | 8 | 主线 3（子任务 3.4）|
| 5 | 测试覆盖补强 | P1 | 3 | 主线 4 |
| 6 | CQRS 落地 | P2 | 3 | 主线 2、3 |
| 7 | API 网关增强 | P2 | 3 | 无 |
| 8 | 文档与规范同步 | P2 | 1 | 全部主线收尾 |
| 9 | 同步通信迁移至 gRPC | P2 | 4 | 主线 1 |

### 3.4 里程碑

| 里程碑 | 完成主线 | 验收标准 |
|---|---|---|
| M1: 架构合规 | 1、2 | Notification 不引用 BC.Domain；SharedKernel 不含技术细节 |
| M2: 代码质量 | 3、4 | UnitOfWork 合并；占位零容忍（CI 强制）；空幂等修复 |
| M3: 测试健全 | 5 | 覆盖率门槛达标；3 个缺失 Infrastructure.Tests 补齐 |
| M4: 通信升级 | 9 | gRPC 契约落地；HttpClient 防腐层下线 |
| M5: 能力增强 | 6、7 | 商品/订单查询走 ES；网关限流熔断 |
| M6: 文档同步 | 8 | 编码规范含 gRPC 约定、占位零容忍条款 |

### 3.5 依赖关系图

```
P0 主线 1 (边界修复) ──────┐
                          ├──→ P1 主线 3 (样板去重) ──→ P2 主线 6 (CQRS)
P0 主线 2 (内核清理) ──────┤
                          ├──→ P1 主线 5 (测试补强) ──→ P2 主线 7 (网关)
P1 主线 4 (占位清理) ──────┘

P2 主线 9 (gRPC) ←── 主线 1 (契约稳定后启动)

P2 主线 8 (文档同步) ←── 全部主线收尾
```

---

## 4 P0 主线 1 — 限界上下文边界修复

### 4.1 子任务 1.1：在 SharedContracts 新增事件契约

**新建文件：**

- [Leno.SharedContracts/Events/PromotionEvents.cs](../../../src/BuildingBlocks/Leno.SharedContracts/Events/PromotionEvents.cs)
  - `SeckillOrderCreatedIntegrationEvent`（秒杀订单创建，含 SeckillActivityId、UserId、SkuId、Quantity、SeckillPrice、Currency、OccurredOn）
  - `SeckillStockPreOccupiedIntegrationEvent`（秒杀库存预占成功）
- `Leno.SharedContracts/Events/PointsMembershipEvents.cs`
  - `PointsEarnedIntegrationEvent`（积分入账，含 UserId、PointsAmount、Source、Reason、OccurredOn）
  - `PointsConsumedIntegrationEvent`、`PointsRevertedIntegrationEvent`
  - `MemberLevelChangedIntegrationEvent`（含 UserId、OldLevel、NewLevel）
  - `PaidMemberSubscribedIntegrationEvent`

**字段来源：** 命名与字段遵循 [docs/spec/00-需求文档总览与DDD架构.md](../../../docs/spec/00-需求文档总览与DDD架构.md) 第 5 节"跨上下文领域事件清单"已规划但未落地的契约。

### 4.2 子任务 1.2：Promotion/PointsMembership 通过 Outbox 翻译领域事件

**现状：** [OutboxDbContextExtensions.cs:28-32](../../../src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs) 当前通过 `domainEvent is IIntegrationEvent` 检查将领域事件直接进发件箱，导致领域事件类型与契约类型耦合。

**方案：**

- 新建 `Leno.Infrastructure/EventBus/IIntegrationEventMapper` 接口：
```csharp
public interface IIntegrationEventMapper
{
    IIntegrationEvent? Map(IDomainEvent domainEvent);
}
```
- 修改 `OutboxDbContextExtensions`：从"领域事件直接实现 IIntegrationEvent"改为"通过 mapper 翻译"
- 在 `Promotion.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 注册 `PromotionIntegrationEventMapper`，将：
  - `SeckillOrderCreatedEvent` → `SeckillOrderCreatedIntegrationEvent`
  - `SeckillStockPreOccupiedEvent` → `SeckillStockPreOccupiedIntegrationEvent`
- PointsMembership 同理处理 5 个事件

### 4.3 子任务 1.3：删除 Notification.Infrastructure 跨 BC 引用

- 改 [NotificationEventConsumer.cs](../../../src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs)、[PromotionEventConsumer.cs](../../../src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PromotionEventConsumer.cs)、[PointsEventConsumer.cs](../../../src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PointsEventConsumer.cs) 订阅 SharedContracts 中的 IntegrationEvent 类型
- 从 [Leno.Notification.Infrastructure.csproj](../../../src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj) 删除两处 ProjectReference
- 编译验证：Notification 项目应只引用 SharedContracts，不引用任何其他 BC 的 Domain/Application

### 4.4 子任务 1.4：补 Notification 消费者测试

为上述修改的 3 个消费者补单元测试，验证对新 IntegrationEvent 类型的处理逻辑（字段映射、通知渲染、幂等）。

### 4.5 向后兼容性

MQ 上同时存在新旧两种消息格式期间，Outbox 翻译器同时发布两种格式（双发），消费者同时订阅两种类型并基于 EventId 去重，验证一周后下线旧格式。

---

## 5 P0 主线 2 — 共享内核职责清理

### 5.1 子任务 2.1：移除 `Entity.Version` 字段的持久化耦合

**现状：** [Entity.cs:41](../../../src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs) 注释"SQL Server rowversion"，`byte[] Version` 是 EF Core/SQL Server 并发令牌格式。

**方案：**

- 从 `Entity` 基类移除 `Version` 字段
- 在各 BC 的 `IEntityTypeConfiguration<T>` 中通过 shadow property 实现乐观锁：
```csharp
builder.Property<byte[]>("RowVersion").IsRowVersion();
```
- EF Core 自动维护 shadow property，领域层不感知

**迁移步骤：** 先在 Infrastructure 加 shadow property 配置并验证测试通过 → 再删除 Domain 字段 → 验证全量测试通过

### 5.2 子任务 2.2：移除 `DomainException.HttpStatusCode`

**现状：** [DomainException.cs:13](../../../src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs) 携带 HTTP 状态码，被 [GlobalExceptionMiddleware.cs](../../../src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs) 直接读取。

**方案：**

- DomainException 只保留 `ErrorCode`，移除 `HttpStatusCode` 字段
- 在 `Leno.Infrastructure/Middleware` 新建 `ErrorCodeMapping`：
```csharp
public static class ErrorCodeMapping
{
    private static readonly Dictionary<string, int> _mapping = new()
    {
        ["ORDER_DOMAIN_ERROR"] = 400,
        ["PAYMENT_DOMAIN_ERROR"] = 400,
        ["UNAUTHORIZED"] = 401,
        // ...
    };
    public static int GetStatusCode(string errorCode) =>
        _mapping.TryGetValue(errorCode, out var code) ? code : 400;
}
```
- 修改 `GlobalExceptionMiddleware` 查表映射

### 5.3 子任务 2.3：迁移 `MoneyJsonConverter.ToStorage/FromStorage`

**现状：** [MoneyJsonConverter.cs:66-90](../../../src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs) 为 EF Core 值转换器服务，存储格式 `amount|currency` 是持久化细节。

**方案：**

- 删除 `ToStorage/FromStorage` 静态方法
- 在 `Leno.Infrastructure/Persistence` 新建 `MoneyValueConverter : ValueConverter<Money, string>`
- 各 BC 的 `IEntityTypeConfiguration<T>` 中 `OwnsOne` 改为 `Property(...).HasConversion<MoneyValueConverter>()`

### 5.4 子任务 2.4：迁移基础设施抽象到 `Leno.Infrastructure.Abstractions`

**现状：** `ICacheService`、`IBloomFilter`、`IEventBus`、`IFileStorageService`、`IExternalChannelOptions` 在 SharedKernel.Abstractions，但实现在 Leno.Infrastructure。

**方案：**

- 在 `Leno.Infrastructure` 内新建 `Abstractions/` 子命名空间
- 迁移上述接口到 `Leno.Infrastructure.Abstractions`
- SharedKernel 只保留 `IAggregateRoot`、`IEntity`、`IRepository`、`IUnitOfWork`、`IDomainEvent`、`IHasDomainEvents` 等真正领域抽象
- 所有 BC.Infrastructure 改 using 命名空间，无需修改逻辑

### 5.5 子任务 2.5：合并 `PageResult<T>` 双定义

**现状：** [SharedKernel/ValueObjects/PageResult.cs](../../../src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs)（record 不可变）与 [SharedContracts/Responses/PageResult.cs](../../../src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs)（class 可变）字段完全相同。

**方案：**

- 保留 SharedContracts 版本作为对外响应契约
- 删除 SharedKernel 版本
- 领域层如需分页结果直接复用 SharedContracts 类型，或使用 `(IReadOnlyList<T> Items, int Total)` 元组

---

## 6 P1 主线 3 — 跨 BC 样板去重

### 6.1 子任务 3.1：抽取泛型 `EfCoreUnitOfWork<TDbContext>`

**现状：** 11 份 [UnitOfWork.cs](../../../src/Services/Order/Leno.Order.Infrastructure/UnitOfWork.cs) 仅 DbContext 类型不同，其余 56 行逐字相同，内部嵌套类 `EfCoreUnitOfWorkTransaction` 被复制 11 次。

**方案：**

在 `Leno.Infrastructure/Persistence` 新建：

```csharp
public sealed class EfCoreUnitOfWork<TDbContext> : IUnitOfWork
    where TDbContext : DbContext
{
    private readonly TDbContext _context;

    public EfCoreUnitOfWork(TDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesWithOutboxAsync(ct);
        return true;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfCoreUnitOfWorkTransaction(transaction);
    }

    public void Dispose() => _context.Dispose();
}

internal sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction _transaction;

    public EfCoreUnitOfWorkTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);
    public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);
    public void Dispose() => _transaction.Dispose();
    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
```

- 删除 11 个 BC 的 `UnitOfWork.cs`
- DI 注册改为 `services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<XxxDbContext>>()`
- 消除约 600 行重复代码

### 6.2 子任务 3.2：`BaseDbContext` 暴露 `OutboxMessages` DbSet

- 在 [BaseDbContext.cs](../../../src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs) 添加 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();`
- 从 11 个 BC 的 DbContext 删除该声明

### 6.3 子任务 3.3：抽取 `AddLenoService<TDbContext>` 一站式扩展方法

**现状：** 11 份 `Program.cs` 高度相似（70+ 行中仅 6 处差异）。

**方案：**

在 `Leno.Infrastructure/Dependencies` 新建 `WebApplicationExtensions.cs`：

```csharp
public static IServiceCollection AddLenoService<TDbContext>(
    this IServiceCollection services,
    IConfiguration configuration,
    Action<IBusRegistrationConfigurator>? configureConsumers = null)
    where TDbContext : DbContext
{
    services.AddLenoInfrastructure(configuration, configureConsumers);
    services.AddHttpContextAccessor();
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(/* ... */);
    services.AddAuthorization(/* ... */);
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(/* ... */);
    services.AddScoped<GlobalExceptionMiddleware>();
    services.AddScoped<InternalApiKeyMiddleware>();
    services.AddHealthChecks().AddDbContextCheck<TDbContext>(tags: new[] { "ready" });
    return services;
}

public static WebApplication UseLenoPipeline(this WebApplication app)
{
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<InternalApiKeyMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
    if (app.Environment.IsDevelopment()) app.UseSwagger().UseSwaggerUI();
    return app;
}
```

各 BC 的 `Program.cs` 缩减到 ~15 行：

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLenoService<OrderDbContext>(builder.Configuration, cfg => cfg.AddOrderConsumers());
builder.Services.AddOrderInfrastructure(builder.Configuration);
var app = builder.Build();
app.UseLenoPipeline();
app.Run();
```

### 6.4 子任务 3.4：合并双 Consumer 基类

**现状：** [IntegrationEventConsumerBase.cs](../../../src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs) 与 `RedisIntegrationEventConsumerBase.cs` 职责重叠，前者默认空幂等。

**方案：**

- 合并为单一基类，构造函数注入 `IIdempotencyStore`
- 删除默认空实现，强制子类注入
- 新建 `Leno.Infrastructure/EventBus/IIdempotencyStore` 接口与 `RedisIdempotencyStore` 实现（基于 Redis SET NX，TTL 24 小时）

```csharp
public interface IIdempotencyStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct);
    Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct);
}
```

- 全量审计所有 Consumer 子类，确认未自行实现幂等的都已获得默认 Redis 幂等保护

---

## 7 P1 主线 4 — 占位实现禁止与替换

### 7.1 子任务 4.1：删除纯占位文件

- 删除 [NewFeatureTests.cs](../../../src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/NewFeatureTests.cs)（0 字节）
- 重命名 `NewFeatureTests1-6.cs` 为有意义的名称（如 `PointsAccountConsumeRevertTests.cs`、`ReviewApprovedEventConsumerTests.cs` 等，按其内实际测试类名）

### 7.2 子任务 4.2：替换 15 个 `SmokeTest_ShouldPass` 占位

15 个占位测试位于：Cart.Domain.Tests、Order.{Api/Application/Domain/Infrastructure}.Tests、Promotion.{Application/Domain/Infrastructure}.Tests、Product.{Api/Application/Domain/Infrastructure}.Tests、UserAuth.{Api/Application/Infrastructure}.Tests。

**策略：** 不为这些项目"凑数补占位"，删除 SmokeTest，随后续主线 5 补充真实测试。

### 7.3 子任务 4.3：补全 7 个空测试项目的关键测试

- SellerShop.Api.Tests、SellerShop.Application.Tests
- ReviewAfterSales.Api.Tests、ReviewAfterSales.Application.Tests
- Notification.Api.Tests
- SystemAdmin.Api.Tests、SystemAdmin.Application.Tests

**优先级：** 先补 SystemAdmin（12 个 AppService 未测）与 SellerShop（3 个 AppService 未测），再补 ReviewAfterSales。

### 7.4 子任务 4.4：补建 3 个缺失的 Infrastructure.Tests 项目

- Cart.Infrastructure.Tests：库存预占 Redis Lua 脚本、缓存服务
- PointsMembership.Infrastructure.Tests：5 个 Consumer、积分冻结/释放、Redis 限流
- SystemAdmin.Infrastructure.Tests：健康聚合、索引重建、死信管理

加入 [Leno.slnx](../../../Leno.slnx)。

### 7.5 子任务 4.5：修复 `IntegrationEventConsumerBase` 空幂等

见主线 3 子任务 3.4，强制注入 `IIdempotencyStore`，全量审计 Consumer 子类。

### 7.6 子任务 4.6：建立占位实现的静态检测

**目标：** 在 CI 层面强制禁止占位实现，建立零容忍机制。

**方案：**

- 在 [Directory.Build.props](../../../Directory.Build.props) 启用 `TreatWarningsAsErrors` 已有
- 引入 [BannedApiAnalyzers](https://github.com/dotnet/roslyn-analyzers) 包，配置 `BannedSymbols.txt` 禁止：
  - `System.NotImplementedException`
  - 在非测试代码中的 `return default!`、`return null!`（通过自定义 Roslyn analyzer 或 Grep CI 步骤）
- 新建 `.editorconfig` 规则强化代码风格约束
- CI 流水线新增 Grep 扫描步骤，PR 中出现以下模式直接阻止合并：
  - `throw new NotImplementedException`
  - `NotImplementedException`
  - 仅含 `Assert.True(true)` 或 `true.Should().BeTrue()` 的 SmokeTest
  - `return default!` / `return null!`（在 `src/Services/**/*.Application/`、`src/Services/**/*.Domain/`、`src/Services/**/*.Infrastructure/` 下）

### 7.7 子任务 4.7：补全真实业务逻辑（覆盖既有 spec 未涵盖的占位）

- 已确认 [replace-placeholder-implementations spec](../../../.trae/specs/replace-placeholder-implementations/spec.md) 已处理 30 处业务占位（report.md 显示完成）
- 本次扫描重点：
  - 跨 BC 防腐层的 `fail-safe 返回空值`模式——审计 [CartPriceService](../../../src/Services/Cart/Infrastructure/Services/CartPriceService.cs)、[AntiCorruptionServices](../../../src/Services/Order/Infrastructure/Services/AntiCorruptionServices.cs) 等，将"调用失败返回空集合/零值"的静默兜底改为**显式抛领域异常 + 告警**
  - 避免数据不一致被掩盖
- 审计 [IntegrationEventConsumerBase](../../../src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs) 默认空幂等（见主线 3 子任务 3.4 已覆盖）

### 7.8 子任务 4.8：编码规范补充"占位实现零容忍"条款

- 在 [docs/编码规范.md](../../../docs/编码规范.md) 第 13 章"测试编码规范"后新增第 13.4 节"占位实现禁止"
- 明确：
  - 禁止 `throw new NotImplementedException()`
  - 禁止空方法体（仅 `return default` 或 `return null`）
  - 禁止 SmokeTest 占位（如 `Assert.True(true)`、`true.Should().BeTrue()`）
  - 禁止 `return default!` / `return null!`
  - 所有业务逻辑必须真实实现，否则 PR 不予合并
- PR 模板新增 checklist 项："本 PR 不含任何占位实现"
- CI 流水线在合并前自动扫描（子任务 4.6）

---

## 8 P1 主线 5 — 测试覆盖补强

### 8.1 子任务 5.1：补 SellerShop、ReviewAfterSales、SystemAdmin 的 Application 测试

- SellerShop：补 `SellerAppService`、`ShopAppService`、`SellerDashboardAppService` 3 个 AppService 测试，覆盖入驻审核、店铺状态流转、看板数据聚合
- ReviewAfterSales：补 `ReviewAppService`、`AfterSalesAppService` 2 个 AppService 测试，覆盖评价提交审核、售后单状态流转、退款请求发起
- SystemAdmin：12 个 AppService 中至少补 `AuditLogAppService`、`FeatureFlagAppService`、`ScheduledTaskAppService`、`DeadLetterAppService` 4 个核心服务测试

### 8.2 子任务 5.2：补 AppService 未覆盖场景

各 BC 当前已测 AppService 列表与未测 AppService 列表：

| 服务 | 已测 AppService | 未测 AppService |
|---|---|---|
| Cart | CartAppService | AnonymousCartAppService |
| Order | OrderAppService | FreightTemplateAppService, LogisticsCompanyAppService, OrderInternalQueryService |
| Product | SPUAppService | BrandAppService, CategoryAppService, InventoryAppService, ProductInternalQueryService |
| Payment | PaymentAppService, RefundAppService | PaymentChannelConfigAppService, ReconciliationAppService, PaymentInternalQueryService |
| Promotion | PromotionAppService | CouponAppService, SeckillAppService, PromotionCalculateAppService |
| UserAuth | AddressAppService, UserAppService | AccountAppService, UserAdminAppService, OAuthClientAppService, PermissionAppService, UserInternalQueryService |
| PointsMembership | PointsAppService | MemberAppService, MembershipPackageAppService, ExchangeCouponAppService, TaskAppService, PointsInternalAppService, PointsOffsetAppService |

**优先级：** 优先补"写操作"AppService（创建/取消/状态流转），查询类可后补。

### 8.3 子任务 5.3：建立测试覆盖率门槛

- 在 [Directory.Build.props](../../../Directory.Build.props) 或 CI 中配置 coverlet 收集覆盖率
- CI 新增覆盖率门槛：
  - Domain 层 ≥ 80%
  - Application 层 ≥ 60%
  - Infrastructure 层 ≥ 40%
- 低于门槛的 PR 阻止合并
- 覆盖率报告通过 ReportGenerator 生成 HTML，归档到 CI artifacts

---

## 9 P2 主线 6 — CQRS 落地

### 9.1 子任务 6.1：商品搜索引入显式 Query Handler

- 在 `Leno.Product.Application` 新建 `Queries/` 子目录，将 `IProductSearchService` 拆为：
  - `Queries/ProductSearchQuery`（record，含 Keyword/CategoryId/Page 等）
  - `Queries/Handlers/ProductSearchQueryHandler`（走 ES）
- 推广 `ReadModelSyncConsumerBase<TEvent,TReadModel>` 到 Promotion（秒杀活动读模型）、PointsMembership（积分账户读模型）

### 9.2 子任务 6.2：订单查询分离

- `IOrderAppService.GetOrderAsync` 等查询方法迁移到独立 `OrderQueryHandler`，走 ES 读库
- 写操作（PlaceOrder、Cancel、ConfirmReceipt）保留在 `OrderAppService`

### 9.3 子任务 6.3：店铺看板读模型

- SellerShop 引入 ES 读模型承载店铺订单数、商品数、销售额聚合
- `ISellerDashboardAppService` 改为走读模型查询

**说明：** CQRS 是改进性优化，不强制全部 BC 落地；只在读多写少的场景引入，避免过度设计。其余 BC 维持单一 AppService 模式可接受。

---

## 10 P2 主线 7 — API 网关增强

### 10.1 子任务 7.1：移除手工 `/health` 轮询

- 改用 YARP 自带的 `ForwardInterceptor` + 各后端的 `/health/ready` 端点配置
- 网关 `/health` 改为 YARP 健康检查聚合（YARP 7.x+ 原生支持）

### 10.2 子任务 7.2：启用 YARP 限流与熔断

- 在 [appsettings.json](../../../src/ApiGateway/Leno.ApiGateway/appsettings.json) 的 `ReverseProxy:Clusters` 节点为每个 cluster 配置：
  - `RateLimiterPolicy`（基于 Redis 滑动窗口）
  - `CircuitBreaker`（连续失败 5 次开路 30 秒）
  - `Timeout`（默认 30 秒，秒杀接口 5 秒）
- 复用 [编码规范.md](../../../docs/编码规范.md) 第 10.4 节的 `SlidingWindowRateLimiter`

### 10.3 子任务 7.3：评估 BFF 聚合需求

- 调研前端实际调用模式，识别需要聚合的端点（如"订单详情页"需同时拉订单+商品+物流）
- 若有 3+ 个聚合场景，新建 `Leno.ApiGateway/Aggregators/` 目录实现 BFF；否则保持现状

---

## 11 P2 主线 9 — 同步通信迁移至 gRPC

### 11.1 子任务 9.1：定义 gRPC 契约（.proto）

在 `Leno.SharedContracts` 新建 `Protos/` 目录，按域定义内部服务契约：

- `product.proto`：`ProductInternalQueryService`
  - `GetSkuInfo(GetSkuInfoRequest) returns (SkuInfoResponse)`
  - `BatchGetSkuInfo(BatchGetSkuInfoRequest) returns (BatchGetSkuInfoResponse)`
- `promotion.proto`：`PromotionInternalService`
  - `CalculateDiscount(CalculateDiscountRequest) returns (CalculateDiscountResponse)`
- `points.proto`：`PointsInternalService`
  - `TrialOffset(TrialOffsetRequest) returns (TrialOffsetResponse)`
  - `Freeze(FreezeRequest) returns (FreezeResponse)`
  - `Release(ReleaseRequest) returns (ReleaseResponse)`
- `user.proto`：`UserInternalService`
  - `GetUserContacts(GetUserContactsRequest) returns (UserContactsResponse)`
- `order.proto`：`OrderInternalService`
  - `GetOrderStatus(GetOrderStatusRequest) returns (OrderStatusResponse)`
- `payment.proto`：`PaymentInternalService`
  - `GetPaymentInfo(GetPaymentInfoRequest) returns (PaymentInfoResponse)`

**字段映射：** 与 [replace-placeholder-implementations spec](../../../.trae/specs/replace-placeholder-implementations/spec.md) 中定义的内部 REST 端点入参/返回一一对应。

**契约治理：**

- .proto 文件由 SharedContracts 统一治理
- 版本化：package 加版本后缀如 `leno.product.v1`
- 字段变更需走契约评审流程，向后兼容（仅新增字段，不删除/重命名）

### 11.2 子任务 9.2：服务端实现（各 BC 暴露 gRPC 端点）

- 各 BC.Api 项目新增 `GrpcServices/` 目录，实现 .proto 定义的服务
- 复用既有 AppService（`IXxxInternalQueryService`）作为业务逻辑入口，gRPC 服务仅做协议适配
- `Program.cs` 注册：
  - `services.AddGrpc(options => options.Interceptors.Add<InternalApiKeyInterceptor>());`
  - `app.MapGrpcService<ProductInternalGrpcService>();`
- 保留 `X-Internal-Key` 头部鉴权（gRPC 拦截器实现 `InternalApiKeyInterceptor`）

### 11.3 子任务 9.3：客户端迁移（HttpClient → gRPC Client）

- 在 `Leno.Infrastructure/AntiCorruption` 新建 `GrpcAntiCorruptionClientBase`，封装 gRPC 调用 + `X-Internal-Key` 头部 + 错误转换
- 各 BC 的防腐层服务改为注入类型化 gRPC 客户端：
  - [CartPriceService](../../../src/Services/Cart/Infrastructure/Services/CartPriceService.cs) → 注入 `ProductInternalQueryService.ProductInternalQueryServiceClient`
  - [AntiCorruptionServices](../../../src/Services/Order/Infrastructure/Services/AntiCorruptionServices.cs) → 注入 Product/Promotion/Points 的 gRPC 客户端
  - [PaymentInfoQueryService](../../../src/Services/ReviewAfterSales/Infrastructure/Services/PaymentInfoQueryService.cs) → 注入 `PaymentInternalService.PaymentInternalServiceClient`
- 配置：`appsettings.json` 的 `ServiceUrls` 改为 `GrpcServiceUrls`
- 端口分配：在既有 [refactor-to-microservices spec](../../../.trae/specs/refactor-to-microservices/spec.md) 端口表（5151-5161）基础上新增 gRPC 端口（5251-5261）

### 11.4 子任务 9.4：过渡兼容与下线 HttpClient

- 过渡期：gRPC 与既有 HttpClient 端点**双活**，通过配置开关 `AntiCorruption:UseGrpc` 切换
- 全量验证 gRPC 调用稳定后，删除各 BC 的 `internal/` REST 控制器与 HttpClient 防腐层代码
- docker-compose 服务定义新增 gRPC 端口暴露

### 11.5 关键技术决策

- gRPC 仅用于**同步**跨 BC 通信
- **异步**事件总线（RabbitMQ + MassTransit）保持不变
- 内部 REST 端点（`X-Internal-Key` 鉴权的 `internal/*` 路由）最终下线，由 gRPC 取代
- 外部 API（用户/前端访问）保持 REST，通过 API 网关暴露
- HTTP/2 + Protobuf 提升性能约 5-10 倍（序列化 + 网络往返），且强类型契约避免手写 DTO 反序列化

---

## 12 P2 主线 8 — 文档与规范同步

### 12.1 子任务 8.1：更新编码规范与 spec

- 将本次优化产生的所有架构决策同步到 [docs/spec/00-需求文档总览与DDD架构.md](../../../docs/spec/00-需求文档总览与DDD架构.md) 与 [docs/编码规范.md](../../../docs/编码规范.md)
- 更新编码规范第 4.1 节"聚合根基类"代码示例（移除 `Version` 字段）
- 更新第 6.2 节"集成事件"章节，补充"领域事件 → 集成事件翻译"约定（`IIntegrationEventMapper`）
- 在第 2.2 节"项目命名与依赖关系"补充 `Leno.Infrastructure.Abstractions` 子命名空间
- 新增第 13.4 节"占位实现禁止"（见主线 4 子任务 4.8）
- 新增第 15 章"gRPC 内部服务通信"约定（.proto 治理、版本化、错误映射）

---

## 13 验收标准

### 13.1 主线 1 验收

- [ ] `Leno.Notification.Infrastructure.csproj` 不再引用 `Promotion.Domain` 或 `PointsMembership.Domain`
- [ ] `Leno.SharedContracts/Events/` 新增 `PromotionEvents.cs`、`PointsMembershipEvents.cs`
- [ ] `OutboxDbContextExtensions` 通过 `IIntegrationEventMapper` 翻译，不再依赖 `domainEvent is IIntegrationEvent`
- [ ] Notification 3 个消费者订阅 IntegrationEvent 类型，不订阅 Domain Event 类型
- [ ] 编译通过，既有测试全绿

### 13.2 主线 2 验收

- [ ] `Entity` 基类不含 `Version` 字段
- [ ] `DomainException` 不含 `HttpStatusCode` 字段
- [ ] `MoneyJsonConverter` 不含 `ToStorage/FromStorage` 方法
- [ ] `ICacheService`、`IBloomFilter`、`IFileStorageService`、`IEventBus`、`IExternalChannelOptions` 迁移到 `Leno.Infrastructure.Abstractions`
- [ ] `PageResult<T>` 只在 SharedContracts 存在一份
- [ ] 编译通过，既有测试全绿

### 13.3 主线 3 验收

- [ ] 11 个 BC 的 `UnitOfWork.cs` 删除，统一使用 `EfCoreUnitOfWork<TDbContext>`
- [ ] `BaseDbContext` 暴露 `OutboxMessages` DbSet
- [ ] 11 个 BC 的 `Program.cs` 缩减到 ~15 行
- [ ] `IntegrationEventConsumerBase` 与 `RedisIntegrationEventConsumerBase` 合并为单一基类
- [ ] 编译通过，既有测试全绿

### 13.4 主线 4 验收

- [ ] [NewFeatureTests.cs](../../../src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/NewFeatureTests.cs) 删除或填充真实测试
- [ ] 15 个 `SmokeTest_ShouldPass` 占位删除或替换为真实测试
- [ ] 7 个空测试项目补齐关键测试
- [ ] 3 个缺失 Infrastructure.Tests 项目补建
- [ ] CI 流水线扫描 `NotImplementedException`、`return default!`、SmokeTest 等模式，违反即阻止合并
- [ ] [docs/编码规范.md](../../../docs/编码规范.md) 第 13.4 节新增"占位实现禁止"条款

### 13.5 主线 5 验收

- [ ] SellerShop、ReviewAfterSales、SystemAdmin 的 Application 测试覆盖率 ≥ 60%
- [ ] CI 配置覆盖率门槛（Domain ≥ 80%、Application ≥ 60%、Infrastructure ≥ 40%）
- [ ] 覆盖率报告归档到 CI artifacts

### 13.6 主线 6 验收

- [ ] `Leno.Product.Application/Queries/` 子目录存在
- [ ] `IProductSearchService` 拆分为 Query + QueryHandler
- [ ] Promotion、PointsMembership 推广 `ReadModelSyncConsumerBase`
- [ ] SellerShop 引入 ES 读模型

### 13.7 主线 7 验收

- [ ] [Leno.ApiGateway/Program.cs](../../../src/ApiGateway/Leno.ApiGateway/Program.cs) 移除手工 `/health` 轮询
- [ ] YARP cluster 配置 RateLimiterPolicy、CircuitBreaker、Timeout
- [ ] BFF 聚合需求评估完成

### 13.8 主线 9 验收

- [ ] `Leno.SharedContracts/Protos/` 目录存在 6 个 .proto 文件
- [ ] 各 BC.Api 项目 `GrpcServices/` 目录存在 gRPC 服务实现
- [ ] 防腐层服务通过 gRPC 客户端调用
- [ ] `AntiCorruption:UseGrpc` 配置开关可用
- [ ] gRPC 端口在 docker-compose 中暴露
- [ ] 过渡期 HttpClient 端点下线

### 13.9 主线 8 验收

- [ ] [docs/编码规范.md](../../../docs/编码规范.md) 更新完成（含 gRPC 约定、占位零容忍、`IIntegrationEventMapper`、`Leno.Infrastructure.Abstractions`）
- [ ] [docs/spec/00-需求文档总览与DDD架构.md](../../../docs/spec/00-需求文档总览与DDD架构.md) 同步架构决策

---

## 14 风险与缓解

### 14.1 主线 1 风险：MQ 消息格式兼容

**风险：** 双发期间消息量翻倍；下线旧格式时若有遗漏消费者会导致消息丢失。

**缓解：** 双发期 1 周观察 MQ 消费 lag；下线前通过日志确认无消费者订阅旧格式（基于 MassTransit 拓扑检查）；保留回滚开关。

### 14.2 主线 2 风险：Entity.Version 移除导致乐观锁失效

**风险：** shadow property 配置错误可能导致并发更新覆盖。

**缓解：** 在迁移前为每个聚合的 EF Core 配置补测试（并发更新场景）；迁移后跑并发测试验证乐观锁仍生效。

### 14.3 主线 3 风险：DI 注册遗漏

**风险：** 删除 11 份 UnitOfWork 后某个 BC 忘记注册 `EfCoreUnitOfWork<XxxDbContext>`。

**缓解：** 迁移脚本批量改 DI 注册；每个 BC 迁移后单独跑该 BC 的测试套件验证。

### 14.4 主线 4 风险：CI 占位零容忍过于严格

**风险：** 紧急修复时无法快速合并含临时占位的 PR。

**缓解：** 允许 `// TEMP-PLACEHOLDER` 标记的临时占位，但 CI 检查 24 小时内必须替换（通过 issue tracker 跟踪）。

### 14.5 主线 9 风险：gRPC 迁移影响生产

**风险：** gRPC 双活期间配置错误可能导致防腐层调用失败。

**缓解：** `AntiCorrelation:UseGrpc` 默认 false，灰度切换；gRPC 客户端配置 health check 探测目标服务可用性；失败自动 fallback 到 HttpClient（过渡期）。

---

## 15 实施顺序

```
M1 (架构合规):
  1. 主线 1: 限界上下文边界修复（4 子任务，约 1-2 周）
  2. 主线 2: 共享内核职责清理（5 子任务，约 1-2 周）

M2 (代码质量):
  3. 主线 3: 跨 BC 样板去重（4 子任务，约 1 周）
  4. 主线 4: 占位实现禁止与替换（8 子任务，约 2 周）

M3 (测试健全):
  5. 主线 5: 测试覆盖补强（3 子任务，约 2-3 周）

M4 (通信升级):
  6. 主线 9: 同步通信迁移至 gRPC（4 子任务，约 3-4 周）

M5 (能力增强):
  7. 主线 6: CQRS 落地（3 子任务，约 2 周）
  8. 主线 7: API 网关增强（3 子任务，约 1 周）

M6 (文档同步):
  9. 主线 8: 文档与规范同步（1 子任务，约 1 周）
```

**总周期：** 约 13-19 周（3-5 个月），按里程碑分批交付，每个里程碑可独立验证。

---

## 附录 A：既有 spec 引用

| Spec | 路径 | 与本方案关系 |
|---|---|---|
| 单体模块重构为微服务架构 | [.trae/specs/refactor-to-microservices/spec.md](../../../.trae/specs/refactor-to-microservices/spec.md) | 提供容器化、网关、健康端点基础，本方案主线 7 增强网关能力 |
| 替换占位实现为真实业务逻辑 | [.trae/specs/replace-placeholder-implementations/spec.md](../../../.trae/specs/replace-placeholder-implementations/spec.md) | 已处理 30 处业务占位，本方案主线 4 聚焦占位机制与剩余测试占位 |
| P0 任务完成 | [.trae/specs/p0-task-completion/](../../../.trae/specs/p0-task-completion/) | 既有分阶段任务，本方案不重复 |
| P1 任务完成 | [.trae/specs/p1-task-completion/](../../../.trae/specs/p1-task-completion/) | 同上 |
| P2 任务完成 | [.trae/specs/p2-task-completion/](../../../.trae/specs/p2-task-completion/) | 同上 |

---

## 附录 B：术语表

| 术语 | 定义 |
|---|---|
| 限界上下文（BC） | 领域模型的显式边界，拥有独立聚合、统一语言与持久化模型 |
| 防腐层（ACL） | 隔离外部上下文或遗留系统的翻译层 |
| 集成事件 | 跨上下文传递的事件，经事件总线发布订阅 |
| 领域事件 | 上下文内部已发生的重要业务事实 |
| 发件箱模式 | 聚合保存与事件记录在同一事务写入，后台进程轮询发布 |
| CQRS | 读写职责分离，Command 走写库、Query 走读库 |
| gRPC | 基于 HTTP/2 + Protobuf 的高性能 RPC 协议 |
| YARP | .NET 反向代理库，用于 API 网关 |
| BFF | Backend for Frontend，为前端定制的聚合层 |

---

**文档结束。本方案为 Leno 电商平台全面优化的纲领性设计，所有子任务实施前需通过架构评审，确保与既有 spec 不冲突。**
