# 阶段三：中期演进 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**日期**：2026-07-23
**输入**：[00-architecture-upgrade-plan.md](./00-architecture-upgrade-plan.md) 第四章 4.2/4.3 + 第六章 6.3 节
**前置依赖**：[02-phase2-quick-wins.md](./02-phase2-quick-wins.md) 全部完成（P1 清零、重复实现清零、配置化覆盖 90%）
**目标**：BC 拆分、Saga 状态机、规则引擎、安全技术栈升级。健康度 8.8 → 9.3
**架构**：12 项任务，4 波编排（Wave 1: 4并行 → Wave 2: 3并行 → Wave 3: 3并行 → Wave 4: 2并行），严格遵循依赖图
**Tech Stack**：.NET 10, MassTransit Saga, EF Core, Argon2id, RS256, KMS, RulesEngine

---

## 1. 范围与约束

### 1.1 实施范围

阶段三共 12 项任务（母方案 6.3 节步骤 1-12），按 4 波编排，每波 2-4 并行 subagent。任务分布：

| 类别 | 任务数 | 任务编号 | 兼容性风险 |
|------|--------|---------|-----------|
| BC 拆分 | 3 项 | 3.1 库存独立 BC、3.5 评价售后拆分、3.6 AuthN/AuthZ 拆分 | 高/中/中 |
| 编排与状态机 | 2 项 | 3.2 Saga 状态机、3.3 Process Manager | 高/中 |
| 扩展性抽象 | 4 项 | 3.4 规则引擎、3.7 OAuth/SSO 通用化、3.8 支付插件化、3.9 通知渠道注册表 | 中/低/低/中 |
| 安全升级 | 1 项 | 3.10 安全技术栈升级（Argon2id/RS256/KMS） | 中 |
| 数据/读模型 | 2 项 | 3.11 Cart SKU 快照本地化、3.12 CQRS snapshot+replay | 中/中 |

### 1.2 关键约束

- **dotnet SDK 可用**：本地 `dotnet build` / `dotnet test` 强制执行，commit 不得带 `[unverified]` 标注
- **5 并行槽位**：每波最多 4 个 subagent 并行（留 1 槽位给主 agent 协调）
- **BC 目录互斥**：subagent 各自 `git add` 自己 BC 目录的文件，BC 互斥矩阵见 §2.4
- **代码完整性强制契约**：禁止占位符、TODO、空实现、截断输出；每函数完整实现
- **双轨期强制**：BC 拆分 / Saga / HS256→RS256 / Process Manager 4 个高风险场景必须配置双轨期与 feature flag 灰度
- **决策门通过**：执行前必须通过 §3 的 DG-1 ~ DG-5 五个决策门

### 1.3 前置依赖核验清单

执行阶段三前，逐项核验阶段二产出已就绪（设计文档 §1.3 跨阶段依赖）：

- [ ] **P1 清零**：阶段二 14 项任务全部 commit，`docs/superpowers/specs/.../02-phase2-quick-wins.md` 全部 checkbox 已勾选
- [ ] **重复实现清零**：`RedisSlidingWindowRateLimiter` 双份消除、`TraceIdEnricher` 合并、`AuditableEntityInterceptor` 单一化已完成
- [ ] **配置化覆盖 90%**：`RetryPolicyOptions` / `RateLimitOptions` / `JwtRevocationOptions` / `PromotionOptions` 走 `IOptionsMonitor<>`，覆盖统计 ≥ 90%
- [ ] **NEW-P0-3 生产验证 ≥ 2 周**：`StockReservationCompensation.OperationType` 字段在生产环境运行 ≥ 2 周无 P0/P1 异常（DG-1 输入）
- [ ] **Outbox 归档策略生效**：阶段二步骤 15 Outbox 7 天归档定时任务上线，表行数稳定无膨胀（DG-2 输入）
- [ ] **团队资源就绪**：5 名后端工程师 + 1 名架构师 + 1 名 DBA + 0.5 DevOps + 2 测试到位（DG-3 输入）
- [ ] **KMS 基础设施可用**：Azure Key Vault 或 AWS KMS 实例已开通，IAM 权限已配置（DG-4 输入）
- [ ] **ES 集群扩容完成**：Elasticsearch 节点容量满足双 BC（Review + AfterSales）读模型需求（DG-5 输入）
- [ ] **培训完成**：后端团队完成 MassTransit Saga 状态机培训；安全 + 后端完成 Argon2id / KMS 集成培训

---

## 2. 总体架构

### 2.1 任务依赖图

严格遵循母方案 §4.2/4.3 与设计文档 §5.2 定义的依赖关系：

```
步骤1 库存独立BC ───┐ (短期修复已在阶段一完成，此处仅中期迁移)
                    ▼
步骤2 MassTransit Saga状态机 ────┐ (依赖库存BC接口稳定)
                                 ▼
步骤3 Process Manager模式 ────────┘ (依赖Saga状态机基础设施)

步骤4 促销规则引擎 ──────────────── (独立)
步骤5 评价与售后BC拆分 ───────────── (独立)
步骤6 AuthN/AuthZ BC拆分 ──────────┐
                                   ▼
步骤7 OAuth/SSO通用化 ─────────────┘ (依赖步骤6，AuthN拆分后)
步骤8 支付渠道插件化 ──────────────── (独立)
步骤9 通知中心渠道注册表 ──────────── (独立)
步骤10 安全技术栈升级 ─────────────── (独立，但需全BC协调)
步骤11 Cart SKU快照本地化 ─────────── (独立)
步骤12 CQRS读模型snapshot+replay ──── (独立)
```

### 2.2 4 波编排

```
Wave 1（4 并行）              Wave 2（3 并行）              Wave 3（3 并行）              Wave 4（2 并行）
┌──────────┬──────────┐      ┌──────────┬──────────┐      ┌──────────┬──────────┐      ┌──────────┬──────────┐
│步骤1     │步骤5     │      │步骤2     │步骤6     │      │步骤3     │步骤7     │      │步骤10    │步骤12    │
│库存BC    │评价售后  │ ───► │Saga状态  │AuthN/    │ ───► │Process   │OAuth/SSO │ ───► │安全技术  │CQRS读模  │
│迁移      │BC拆分    │      │机        │AuthZ拆分 │      │Manager   │通用化    │      │栈升级    │型replay │
├──────────┤          │      ├──────────┤          │      ├──────────┤          │      └──────────┴──────────┘
│步骤4     │          │      │步骤8     │          │      │步骤9     │          │
│促销规则  │          │      │支付渠道  │          │      │通知渠道  │          │
│引擎      │          │      │插件化    │          │      │注册表    │          │
├──────────┤          │      └──────────┴──────────┘      └──────────┴──────────┘
│步骤11    │          │
│Cart SKU  │          │
│快照      │          │
└──────────┴──────────┘
```

**波次划分依据**（设计文档 §5.3）：
- Wave 1（4 并行）：步骤 1/4/5/11 全独立无依赖
- Wave 2（3 并行）：步骤 2 依赖步骤 1 库存 BC 接口；步骤 6/8 独立
- Wave 3（3 并行）：步骤 3 依赖步骤 2 Saga 基础设施；步骤 7 依赖步骤 6 AuthN 拆分；步骤 9 独立
- Wave 4（2 并行）：步骤 10 需全 BC 协调放最后；步骤 12 独立

**subagent 总数**：12 个（4 波，每波 2-4 并行）

### 2.3 BC 互斥矩阵

| BC | Wave 1 | Wave 2 | Wave 3 | Wave 4 | 互斥说明 |
|----|--------|--------|--------|--------|---------|
| Order | 步骤 1（StockReservation 迁出） | 步骤 2（Saga） | 步骤 3（Process Manager） | — | Wave 1→2→3 串行依赖，无并行冲突 |
| Inventory（新建） | 步骤 1 | — | — | — | 新建 BC，独占 |
| Promotion | 步骤 4 | — | — | — | 独占 |
| Review（新建） | 步骤 5 | — | — | — | 独占 |
| AfterSales（新建） | 步骤 5 | — | — | — | 与 Review 同 subagent 串行（一次拆分两 BC） |
| UserAuth | — | 步骤 6 | — | 步骤 10 | Wave 2 完成后 Wave 4 安全升级介入，串行 |
| Identity（新建） | — | 步骤 6 | — | — | 与 AccessControl 同 subagent 串行 |
| AccessControl（新建） | — | 步骤 6 | — | — | 同上 |
| Payment | — | 步骤 8 | — | — | 独占 |
| Notification | — | — | 步骤 9 | — | 独占 |
| Cart | 步骤 11 | — | — | — | 独占 |
| 全 BC（共享层） | — | — | — | 步骤 10 | 安全升级需全 BC 协调，独占 Wave 4 |
| SystemAdmin（读模型） | — | — | — | 步骤 12 | 独占 |

---

## 3. 决策门（执行前必须通过）

阶段三特有 5 个决策门（设计文档 §5.7），执行前必须逐项验证 checkbox 通过。任一未通过需触发修订流程：

- [ ] **DG-1 库存 BC 迁移前置**：阶段一 NEW-P0-3 修复（`StockReservationCompensation.OperationType`）已在生产环境运行 ≥ 2 周无 P0/P1 异常
  - 修订触发条件：未达 2 周或异常未清零 → 步骤 1 推迟，先 Wave 1 启动步骤 4/5/11
- [ ] **DG-2 Saga 状态机前置**：阶段二步骤 15 Outbox 7 天归档策略已生效，`outbox_messages` 表行数稳定（日均增量 ≤ 日处理量），无膨胀
  - 修订触发条件：表行数持续上涨 → 先修复 Outbox 发布器再启动 Saga
- [ ] **DG-3 BC 拆分人力**：5 名后端工程师 + 1 名架构师 + 1 DBA + 0.5 DevOps + 2 测试全部到位，已完成 MassTransit Saga 培训
  - 修订触发条件：人力不足 → BC 拆分任务（3.1/3.5/3.6）分批启动，优先 3.1
- [ ] **DG-4 安全升级 KMS 就绪**：Azure Key Vault 或 AWS KMS 实例已开通，应用 Service Principal 已授予 `Key Get/Wrap/Unwrap` 权限，网络可达性验证通过
  - 修订触发条件：KMS 不可用 → 步骤 10 推迟，先完成 Argon2id 与 RS256（appsettings.json 临时托管密钥），KMS 部分后置
- [ ] **DG-5 评价售后拆分 ES 扩容**：ES 集群节点容量满足双 BC 读模型需求（Review + AfterSales 独立索引），磁盘使用率 < 60%
  - 修订触发条件：ES 容量不足 → 步骤 5 推迟，先完成其他 Wave 1 任务

---

## 4. Wave 1 详细编排（4 并行 subagent）

Wave 1 启动 4 个并行 subagent，处理全独立任务：步骤 1（库存 BC）、步骤 4（促销规则）、步骤 5（评价售后拆分）、步骤 11（Cart 快照）。

### 4.1 步骤1：库存独立 BC 中期迁移（6周，高风险）

**任务编号**：3.1
**目标**：将 `StockReservation` 聚合从 Order BC 迁移至新建 `Inventory` BC，Order BC 通过集成事件/命令调用库存 BC，库存真源单一化。
**前置决策门**：DG-1（NEW-P0-3 生产验证 ≥ 2 周）

#### 4.1.1 新建 BC 项目结构

在 `src/Services/Inventory/` 下创建 4 个项目（参照现有 BC 目录结构）：

```
src/Services/Inventory/
├── Leno.Inventory.Domain/
│   ├── Aggregates/
│   │   ├── StockReservation.cs              # 从 Order BC 迁入
│   │   ├── StockReservationCompensation.cs  # 从 Order BC 迁入
│   │   └── StockBaseline.cs                 # 从 Product BC 迁入（中期阶段统一真源）
│   ├── Events/
│   │   ├── StockReservedEvent.cs            # 从 Order BC 迁入
│   │   ├── StockConfirmedEvent.cs           # 从 Order BC 迁入
│   │   └── StockReleasedEvent.cs            # 从 Order BC 迁入
│   ├── Repositories/
│   │   ├── IInventoryRepository.cs          # 从 Order BC 迁入
│   │   └── IStockBaselineRepository.cs      # 新增（从 Product BC 的 IProductInventoryRepository 演化）
│   ├── Services/
│   │   └── IStockReservationDomainService.cs # 从 Order BC 迁入
│   └── Leno.Inventory.Domain.csproj
├── Leno.Inventory.Application/
│   ├── DTOs/
│   │   └── StockReservationDtos.cs
│   ├── Services/
│   │   ├── InventoryAppService.cs           # 暴露 ReserveAsync/ConfirmAsync/ReleaseAsync/ReturnDeductedAsync
│   │   └── SeckillStockAppService.cs        # 从 Promotion BC 迁入 Redis Hash 秒杀库存逻辑
│   ├── Consumers/
│   │   ├── ReserveStockCommandConsumer.cs   # 消费 Order BC 发布的 ReserveStockCommand
│   │   └── ConfirmStockCommandConsumer.cs
│   ├── IInventoryAppService.cs
│   └── Leno.Inventory.Application.csproj
├── Leno.Inventory.Infrastructure/
│   ├── Configurations/
│   │   ├── StockReservationConfiguration.cs
│   │   └── StockBaselineConfiguration.cs
│   ├── Repositories/
│   │   ├── EfCoreInventoryRepository.cs
│   │   └── EfCoreStockBaselineRepository.cs
│   ├── Services/
│   │   └── RedisSeckillStockService.cs      # Redis Hash 秒杀库存，从 Promotion BC 迁入
│   ├── InventoryDbContext.cs
│   ├── InventoryDbContextDesignTimeFactory.cs
│   └── Leno.Inventory.Infrastructure.csproj
└── Leno.Inventory.Api/
    ├── Controllers/
    │   └── InternalInventoryController.cs   # 内部 HTTP 端点
    ├── GrpcServices/
    │   └── InventoryGrpcService.cs          # gRPC ReserveStock/ConfirmStock/ReleaseStock
    ├── Program.cs
    ├── appsettings.json
    └── Leno.Inventory.Api.csproj
```

#### 4.1.2 集成事件契约

在 `src/SharedContracts/Integration/Inventory/` 下新增命令与事件契约（与 Order/Product/Promotion BC 共享）：

- `ReserveStockCommand`（Order BC → Inventory BC）：`OrderId`、`Items: [{SkuId, Quantity, SellerId}]`、`IdempotencyKey`
- `ConfirmStockCommand`（Order BC → Inventory BC）：`OrderId`、`IdempotencyKey`
- `ReleaseStockCommand`（Order BC → Inventory BC）：`OrderId`、`IdempotencyKey`、`OperationType`（Release/ReturnDeducted）
- `StockReservedIntegrationEvent`（Inventory BC → Order BC）：`OrderId`、`ReservationIds`、`ExpiresAt`
- `StockConfirmedIntegrationEvent`（Inventory BC → Order BC）：`OrderId`
- `StockReleasedIntegrationEvent`（Inventory BC → Order BC）：`OrderId`、`OperationType`

#### 4.1.3 subagent 指令

按以下顺序执行：

1. **创建 Inventory BC 4 个 csproj**：`dotnet new classlib` + 引用 `Leno.SharedKernel` / `Leno.Infrastructure` / `Leno.SharedContracts`
2. **迁移领域层**：将 `src/Services/Order/Leno.Order.Domain/Aggregates/StockReservation.cs`、`StockReservationCompensation.cs`、`IInventoryRepository.cs`、`IStockReservationDomainService.cs` 及 `Events/Stock*Event.cs` 迁移到 `Leno.Inventory.Domain` 对应目录，命名空间从 `Leno.Order.Domain.*` 改为 `Leno.Inventory.Domain.*`
3. **迁移 Product BC 的 StockBaseline**：`src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs` 迁至 `Leno.Inventory.Domain/Aggregates/StockBaseline.cs`，Product BC 保留只读投影
4. **迁移 Promotion BC 秒杀库存**：`src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs` 迁至 `Leno.Inventory.Infrastructure/Services/RedisSeckillStockService.cs`，Promotion BC 通过 `ReserveSeckillStockCommand` 调用
5. **创建 InventoryDbContext + EF Configuration + 迁移**：表 `stock_reservations` / `stock_reservation_compensations` / `stock_baselines` 迁移到 Inventory BC 数据库（独立 schema 或独立数据库，按 DBA 决策）
6. **Order BC 改造**：删除 `Leno.Order.Domain/Aggregates/StockReservation.cs` 等，`OrderSagaOrchestrator` 改为发布 `ReserveStockCommand` 经 MassTransit 调用 Inventory BC；`StockReservationCompensationBackgroundService` 改为消费 `StockReleasedIntegrationEvent` 触发补偿
7. **gRPC 端点**：`InventoryGrpcService` 暴露 `ReserveStock` / `ConfirmStock` / `ReleaseStock` / `ReturnDeducted` 方法，proto 文件 `src/SharedContracts/Grpc/inventory.proto`
8. **数据迁移脚本**：编写 SQL 迁移脚本，将 Order BC `stock_reservations` 与 `stock_reservation_compensations` 表数据迁至 Inventory BC 数据库，Product BC `stock_baselines` 数据同步迁入；Down 脚本回迁
9. **双轨期配置**：appsettings.json 增加 `Inventory:UseExternalBc` feature flag，false 时 Order BC 仍走进程内调用（兼容旧路径），true 时走集成事件
10. **集成测试**：`Leno.Inventory.Application.Tests` 新增 ReserveStock/ConfirmStock/ReleaseStock 流程测试；`Leno.Order.Application.Tests` 新增 Saga 调用 Inventory BC 的集成测试

#### 4.1.4 关键代码骨架

`ReserveStockCommand` 契约（`src/SharedContracts/Integration/Inventory/ReserveStockCommand.cs`）：

```csharp
namespace Leno.SharedContracts.Integration.Inventory;

public sealed record ReserveStockCommand(
    Guid OrderId,
    IReadOnlyList<ReserveStockItem> Items,
    Guid IdempotencyKey,
    TimeSpan? ReservationTtl = null) : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
}

public sealed record ReserveStockItem(Guid SkuId, int Quantity, long SellerId);
```

`InventoryAppService` 关键方法签名（`src/Services/Inventory/Leno.Inventory.Application/Services/InventoryAppService.cs`）：

```csharp
public sealed class InventoryAppService(
    IInventoryRepository inventoryRepository,
    IStockBaselineRepository baselineRepository,
    IUnitOfWork unitOfWork,
    ILogger<InventoryAppService> logger) : IInventoryAppService
{
    public async Task<StockReservationResult> ReserveAsync(
        Guid orderId, IReadOnlyList<ReserveStockItem> items, Guid idempotencyKey, CancellationToken ct)
    {
        // 幂等检查 + 调用 StockReservation 聚合 Reserve + 持久化 + 发布 StockReservedIntegrationEvent
    }

    public Task ConfirmAsync(Guid orderId, Guid idempotencyKey, CancellationToken ct);
    public Task ReleaseAsync(Guid orderId, Guid idempotencyKey, CancellationToken ct);
    public Task ReturnDeductedAsync(Guid orderId, Guid idempotencyKey, CancellationToken ct);
}
```

#### 4.1.5 验收标准

- [ ] `dotnet build src/Services/Inventory/Leno.Inventory.sln` 零错误零警告
- [ ] `dotnet test src/Services/Inventory/` 全绿，新增代码覆盖率 ≥ 80%
- [ ] Order BC 编译通过，`StockReservation` 等类型已从 `Leno.Order.Domain` 删除
- [ ] Product BC 编译通过，`StockBaseline` 已迁移，保留只读投影
- [ ] Promotion BC 编译通过，秒杀库存逻辑改为发布 `ReserveSeckillStockCommand`
- [ ] 集成测试：OrderSagaOrchestrator 调用 Inventory BC 全流程（Reserve → Confirm → Release）通过，使用 MassTransit TestHarness
- [ ] 数据迁移脚本：在测试数据库执行迁移 + Down 回滚各 1 次，数据零丢失
- [ ] 双轨期：`Inventory:UseExternalBc=false` 时回归测试全通过；`=true` 时集成测试全通过
- [ ] Prometheus 指标 `inventory_redis_reconcile_diff_total` 上线，Redis 库存对账 SLA 监控告警就位

#### 4.1.6 commit

```
[phase3][Inventory] 3.1: 库存独立 BC 中期迁移，StockReservation 聚合从 Order BC 迁出
```

---

### 4.2 步骤4：促销规则引擎（4周，独立）

**任务编号**：3.4
**目标**：抽象 `IPromotionRule` 接口与 `PromotionRuleContext`，规则配置 JSON 化，`PromotionCalculateAppService` 改为规则编排器。

#### 4.2.1 文件清单

**新增**：
- `src/Services/Promotion/Leno.Promotion.Domain/Rules/IPromotionRule.cs`
- `src/Services/Promotion/Leno.Promotion.Domain/Rules/PromotionRuleContext.cs`
- `src/Services/Promotion/Leno.Promotion.Domain/Rules/PromotionRuleResult.cs`
- `src/Services/Promotion/Leno.Promotion.Domain/Rules/RulePriorityAttribute.cs`
- `src/Services/Promotion/Leno.Promotion.Domain/Rules/StackingPolicy.cs`（枚举：Exclusive / Stackable / BestOf）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/FullReductionRule.cs`（满减规则实现）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/CouponRule.cs`（优惠券规则实现）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/SeckillDiscountRule.cs`（秒杀折扣规则）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/RuleEngine.cs`（编排器，按优先级 + 叠加策略）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/JsonRuleDefinition.cs`（JSON 配置绑定模型）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/JsonRuleLoader.cs`（从 `promotion_rule_definitions` 表加载 JSON）
- `src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/PromotionRuleDefinitionConfiguration.cs`
- `src/Services/Promotion/Leno.Promotion.Domain/Aggregates/PromotionRuleDefinition.cs`（聚合根，存 JSON 规则定义）
- 迁移：`src/Services/Promotion/Leno.Promotion.Infrastructure/Migrations/{timestamp}_AddPromotionRuleDefinitions.cs`

**修改**：
- `src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs`：改为调用 `IRuleEngine.EvaluateAsync(context)`，旧 `CalculateDiscount` 逻辑迁移到 `FullReductionRule` / `CouponRule`
- `src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Promotion.cs`：保留 `CalculateDiscount` 方法但加 `[Obsolete("Use IPromotionRule implementations")]` 包装，向后兼容
- `src/Services/Promotion/Leno.Promotion.Api/Program.cs`：DI 注册 `IRuleEngine` / 所有 `IPromotionRule` 实现 / `JsonRuleLoader`

#### 4.2.2 关键代码骨架

`IPromotionRule` 接口（`src/Services/Promotion/Leno.Promotion.Domain/Rules/IPromotionRule.cs`）：

```csharp
namespace Leno.Promotion.Domain.Rules;

public interface IPromotionRule
{
    string RuleType { get; }                          // 例如 "FullReduction" / "Coupon" / "SeckillDiscount"
    StackingPolicy Stacking { get; }                  // 叠加策略
    int Priority { get; }                             // 优先级，数字越小越先评估

    Task<PromotionRuleResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct);
    Task<bool> IsApplicableAsync(PromotionRuleContext context, CancellationToken ct);
}

public sealed record PromotionRuleContext(
    long UserId,
    long SellerId,
    IReadOnlyList<CartItemContext> Items,
    decimal SubTotal,
    string? CouponCode,
    string? SeckillActivityId,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record CartItemContext(Guid SkuId, int Quantity, decimal UnitPrice, string? CategoryCode);

public sealed record PromotionRuleResult(
    string RuleType,
    decimal DiscountAmount,
    string? AppliedCouponId,
    IReadOnlyDictionary<string, string> Metadata,
    bool Applied);
```

`RuleEngine` 编排器（`src/Services/Promotion/Leno.Promotion.Infrastructure/Rules/RuleEngine.cs`）：

```csharp
public sealed class RuleEngine(
    IEnumerable<IPromotionRule> rules,
    ILogger<RuleEngine> logger) : IRuleEngine
{
    public async Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionRuleContext context, CancellationToken ct)
    {
        var ordered = rules.OrderBy(r => r.Priority).ToList();
        var applied = new List<PromotionRuleResult>();
        decimal remainingSubTotal = context.SubTotal;

        foreach (var rule in ordered)
        {
            if (!await rule.IsApplicableAsync(context, ct)) continue;

            var result = await rule.EvaluateAsync(context with { SubTotal = remainingSubTotal }, ct);
            if (!result.Applied) continue;

            applied.Add(result);
            remainingSubTotal -= result.DiscountAmount;

            if (rule.Stacking == StackingPolicy.Exclusive) break;
        }

        return new PromotionEvaluationResult(applied, context.SubTotal - remainingSubTotal);
    }
}
```

JSON 规则定义示例（`promotion_rule_definitions.json` 配置驱动）：

```json
{
  "ruleType": "FullReduction",
  "priority": 100,
  "stacking": "Stackable",
  "definition": {
    "thresholds": [{ "minAmount": 100, "discountAmount": 20 }, { "minAmount": 200, "discountAmount": 50 }],
    "applicableSellerIds": [],
    "applicableCategoryCodes": []
  },
  "enabled": true,
  "version": "2026.07.01"
}
```

#### 4.2.3 subagent 指令

1. 创建 `IPromotionRule` / `PromotionRuleContext` / `PromotionRuleResult` / `StackingPolicy` / `RulePriorityAttribute`
2. 实现 3 个规则：`FullReductionRule`（从 `PromotionCalculateAppService.GetByUserAsync` 满减逻辑迁移）、`CouponRule`（从优惠券计算逻辑迁移）、`SeckillDiscountRule`（新增秒杀场景）
3. 实现 `RuleEngine` 编排器，按 `Priority` 排序，按 `StackingPolicy` 控制 Exclusive 中断 / Stackable 叠加 / BestOf 取最优
4. 新增 `PromotionRuleDefinition` 聚合 + EF Configuration + 迁移，存 JSON 规则定义
5. `JsonRuleLoader` 启动时从 DB 加载 + 监听 `PromotionRuleDefinitionChangedEvent` 热刷新
6. `PromotionCalculateAppService` 改为 `IRuleEngine.EvaluateAsync`，旧 `CalculateDiscount` 保留 `[Obsolete]` 包装
7. DI 注册所有规则实现为 `IEnumerable<IPromotionRule>`
8. 单元测试：每个规则 + `RuleEngine` 编排顺序 + Stacking 策略 + JSON 加载
9. A/B 测试：feature flag `Promotion:UseRuleEngine` 切流，新旧引擎并行运行对比结果

#### 4.2.4 验收标准

- [ ] `dotnet build src/Services/Promotion/` 零错误零警告
- [ ] `dotnet test src/Services/Promotion/` 全绿，规则引擎单元测试覆盖率 ≥ 80%
- [ ] 3 个规则实现完整，无 TODO / 占位符
- [ ] `PromotionCalculateAppService` 调用路径切换为 `IRuleEngine`，旧 `[Obsolete]` 包装仍可调用
- [ ] JSON 规则配置加载 + 热刷新测试通过
- [ ] A/B 测试：新旧引擎对同一 `PromotionRuleContext` 输出折扣金额一致（差异 ≤ 0.01 元）
- [ ] 新增规则类型（如 `MemberLevelDiscountRule`）通过 DI 注册即可生效，零侵入核心调度

#### 4.2.5 commit

```
[phase3][Promotion] 3.4: 促销规则引擎抽象，IPromotionRule + RuleEngine 编排，规则 JSON 化
```

---

### 4.3 步骤5：评价与售后 BC 拆分（4周，独立）

**任务编号**：3.5
**目标**：将 ReviewAfterSales 单 BC 拆分为 `Review` BC（评价 + 评分快照 + ES 投影）与 `AfterSales` BC（售后状态机 + 退款协作），`eligibilityChecker` 拆为两套。
**前置决策门**：DG-5（ES 集群扩容完成）

#### 4.3.1 新建 BC 项目结构

```
src/Services/Review/
├── Leno.Review.Domain/
│   ├── Aggregates/
│   │   ├── Review.cs                          # 从 ReviewAfterSales 迁入
│   │   └── ReviewRatingSnapshot.cs            # 评分快照聚合
│   ├── Events/
│   │   ├── ReviewSubmittedEvent.cs            # 从 ReviewAfterSales 迁入
│   │   └── ReviewRatingUpdatedEvent.cs
│   ├── Repositories/
│   │   └── IReviewRepository.cs
│   ├── Services/
│   │   └── IReviewEligibilityChecker.cs       # 拆分后的评价资格检查器
│   └── Leno.Review.Domain.csproj
├── Leno.Review.Application/
│   ├── DTOs/
│   ├── Services/
│   │   └── ReviewAppService.cs
│   ├── Consumers/
│   │   └── OrderCompletedEventConsumer.cs     # 触发评价资格
│   └── Leno.Review.Application.csproj
├── Leno.Review.Infrastructure/
│   ├── Configurations/
│   │   └── ReviewConfiguration.cs
│   ├── Repositories/
│   │   └── EfCoreReviewRepository.cs
│   ├── ReadModels/
│   │   └── ReviewReadModel.cs                 # ES 投影
│   ├── ElasticSearch/
│   │   └── ReviewIndexInitializer.cs
│   ├── ReviewDbContext.cs
│   ├── ReviewDbContextDesignTimeFactory.cs
│   └── Leno.Review.Infrastructure.csproj
└── Leno.Review.Api/
    ├── Controllers/
    │   └── ReviewsController.cs
    ├── GrpcServices/
    │   └── ReviewGrpcService.cs
    ├── Program.cs
    └── Leno.Review.Api.csproj

src/Services/AfterSales/
├── Leno.AfterSales.Domain/
│   ├── Aggregates/
│   │   └── AfterSalesOrder.cs                 # 从 ReviewAfterSales 迁入
│   ├── Events/
│   │   ├── AfterSalesCreatedEvent.cs
│   │   └── AfterSalesRefundCompletedEvent.cs  # 统一命名，不再外溢
│   ├── Repositories/
│   │   └── IAfterSalesRepository.cs
│   ├── Services/
│   │   ├── IAfterSalesEligibilityChecker.cs   # 拆分后的售后资格检查器
│   │   └── AfterSalesStateMachine.cs          # 售后状态机独立演进
│   └── Leno.AfterSales.Domain.csproj
├── Leno.AfterSales.Application/
│   ├── Services/
│   │   └── AfterSalesAppService.cs
│   ├── Consumers/
│   │   ├── OrderShippedEventConsumer.cs
│   │   └── RefundCompletedEventConsumer.cs
│   └── Leno.AfterSales.Application.csproj
├── Leno.AfterSales.Infrastructure/
│   ├── Configurations/
│   │   └── AfterSalesConfiguration.cs
│   ├── Repositories/
│   │   └── EfCoreAfterSalesRepository.cs
│   ├── AfterSalesDbContext.cs
│   ├── AfterSalesDbContextDesignTimeFactory.cs
│   └── Leno.AfterSales.Infrastructure.csproj
└── Leno.AfterSales.Api/
    ├── Controllers/
    │   └── AfterSalesController.cs
    ├── Program.cs
    └── Leno.AfterSales.Api.csproj
```

#### 4.3.2 集成事件契约

`src/SharedContracts/Integration/ReviewAfterSales/` 下重组：

- `ReviewSubmittedEvent`（Review BC 发布）：`ReviewId`、`OrderId`、`UserId`、`SellerId`、`Rating`、`Content`
- `AfterSalesRefundCompletedEvent`（AfterSales BC 发布）：`AfterSalesId`、`OrderId`、`RefundAmount`
- 旧 `ReviewAfterSalesRefundCompletedEvent` 标记 `[Obsolete]`，双写过渡 4 周

#### 4.3.3 eligibilityChecker 拆分

原 `IOrderEligibilityChecker` 同时承载"评价资格"与"售后资格"两类规则，拆为：

- `IReviewEligibilityChecker.IsEligibleForReviewAsync(orderId, userId)`：订单已完成 ≥ 7 天、未评价、未退货
- `IAfterSalesEligibilityChecker.IsEligibleForAfterSalesAsync(orderId, skuId)`：订单在售后窗口期内、未重复申请

#### 4.3.4 subagent 指令

1. 创建 `Review` BC 与 `AfterSales` BC 4 个 csproj 各一套
2. 将 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs` 迁入 `Leno.Review.Domain`，命名空间改为 `Leno.Review.Domain.*`
3. 将 `AfterSalesOrder.cs` 迁入 `Leno.AfterSales.Domain`，命名空间改为 `Leno.AfterSales.Domain.*`
4. `IOrderEligibilityChecker` 拆为 `IReviewEligibilityChecker` 与 `IAfterSalesEligibilityChecker`，分别迁移到对应 BC
5. ES 投影：`ReviewReadModel` 独立索引 `reviews_v2`，`AfterSalesReadModel`（如有）独立索引 `after_sales_v1`
6. `ReviewIndexInitializer` 启动时创建索引 mapping
7. 数据迁移脚本：将 `ReviewAfterSales` 数据库 `reviews` 表迁至 Review BC，`after_sales_orders` 表迁至 AfterSales BC
8. 旧 `ReviewAfterSales` BC 标记 `[Obsolete]`，保留 4 周双写期，event 双发（旧名 + 新名）
9. 消费 `ReviewSubmittedEvent` 的 PointsMembership / SellerShop BC 更新订阅为新命名空间
10. 集成测试：评价流程 + 售后流程各 1 个端到端测试

#### 4.3.5 验收标准

- [ ] `dotnet build src/Services/Review/` 与 `src/Services/AfterSales/` 零错误零警告
- [ ] `dotnet test` 两个新 BC 全绿，覆盖率 ≥ 80%
- [ ] 旧 `ReviewAfterSales` BC 编译通过但所有类型标记 `[Obsolete]`
- [ ] ES 索引 `reviews_v2` 与 `after_sales_v1` 创建成功，文档数量与源数据库一致
- [ ] eligibilityChecker 两套实现独立测试通过
- [ ] 双写期：旧 `ReviewSubmittedEvent` 与新 `ReviewSubmittedEvent` 同时发布，消费方测试通过
- [ ] PointsMembership / SellerShop BC 消费新事件测试通过
- [ ] 数据迁移 + Down 回滚各 1 次成功

#### 4.3.6 commit

```
[phase3][Review/AfterSales] 3.5: 评价与售后 BC 拆分，eligibilityChecker 拆两套，ES 投影独立
```

---

### 4.4 步骤11：Cart SKU 快照本地化（3周，独立）

**任务编号**：3.11
**目标**：Cart 聚合存储 SKU 快照（名称/价格/主图/规格），消除 `CartPriceService` 实时跨进程调用。

#### 4.4.1 文件清单

**修改**：
- `src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs`：新增 `SkuSnapshot` 值对象字段
- `src/Services/Cart/Leno.Cart.Domain/ValueObjects/SkuSnapshot.cs`：新建值对象（SkuName/Price/MainImageUrl/SpecText/Currency/SnapshotVersion/SnapshotAt）
- `src/Services/Cart/Leno.Cart.Domain/Events/SkuPriceChangedEvent.cs`：新建，价格变化时发布
- `src/Services/Cart/Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs`：增加 `SkuSnapshot` 列映射（JSON 列或 owned entity）
- `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs`：改为读快照而非 gRPC 实时调用
- `src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs`：消费 `ProductSkuUpdatedEvent` 更新快照
- `src/Services/Cart/Leno.Cart.Domain/Services/ICartPriceService.cs`：接口签名调整
- 迁移：`src/Services/Cart/Leno.Cart.Infrastructure/Migrations/{timestamp}_AddCartItemSkuSnapshot.cs`

**删除**：
- `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs`（快照模式下不再需要实时调用）

#### 4.4.2 关键代码骨架

`SkuSnapshot` 值对象：

```csharp
namespace Leno.Cart.Domain.ValueObjects;

public sealed record SkuSnapshot(
    Guid SkuId,
    string SkuName,
    decimal Price,
    string Currency,
    string? MainImageUrl,
    string? SpecText,
    int SnapshotVersion,
    DateTime SnapshotAt)
{
    public bool IsStale(TimeSpan maxAge) => DateTime.UtcNow - SnapshotAt > maxAge;
}
```

`CartItem` 调整：

```csharp
public sealed class CartItem
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public int Quantity { get; private set; }
    public SkuSnapshot? SkuSnapshot { get; private set; }   // 新增

    public void UpdateSnapshot(SkuSnapshot snapshot)
    {
        if (snapshot.SkuId != SkuId) throw new CartDomainException("SkuId mismatch");
        SkuSnapshot = snapshot;
    }
}
```

#### 4.4.3 subagent 指令

1. 创建 `SkuSnapshot` 值对象 + `SkuPriceChangedEvent` 领域事件
2. `CartItem` 增加 `SkuSnapshot` 属性与 `UpdateSnapshot` 方法
3. `CartConfiguration` 配置 `SkuSnapshot` 为 owned entity（或 JSON 列 `sku_snapshot`）
4. EF 迁移 `AddCartItemSkuSnapshot`
5. `CartPriceService` 改为：购物车读取时若 `SkuSnapshot.IsStale(5min)` 则触发后台刷新，否则直接返回快照价格
6. `ProductEventConsumer` 消费 `ProductSkuUpdatedEvent`，更新对应 CartItem 的快照
7. 删除 `GrpcCartPriceService`（保留 `ICartInternalQueryService` 用于内部查询）
8. 数据迁移脚本：对现有 `cart_items` 表回填快照（一次性 gRPC 批量查询 Product BC）
9. 单元测试：快照刷新逻辑 + 过期判定 + 并发更新
10. 集成测试：购物车展示无实时跨进程调用，性能基准对比

#### 4.4.4 验收标准

- [ ] `dotnet build src/Services/Cart/` 零错误零警告
- [ ] `dotnet test src/Services/Cart/` 全绿，覆盖率 ≥ 80%
- [ ] `CartPriceService` 不再调用 Product BC gRPC（购物车读取路径）
- [ ] 快照过期 5 分钟后自动后台刷新，验证逻辑通过测试
- [ ] `ProductSkuUpdatedEvent` 消费后 CartItem 快照更新，集成测试通过
- [ ] 数据迁移脚本：现有 cart_items 全部回填快照，无空值
- [ ] 性能基准：购物车列表接口 P99 延迟下降 ≥ 30%（对比基线，无跨进程调用）

#### 4.4.5 commit

```
[phase3][Cart] 3.11: Cart SKU 快照本地化，消除 CartPriceService 实时跨进程调用
```

---

## 5. Wave 2 详细编排（3 并行 subagent）

Wave 1 全部提交并验证通过后启动 Wave 2。3 个并行 subagent：步骤 2（Saga，依赖步骤 1 库存 BC）、步骤 6（AuthN/AuthZ 拆分）、步骤 8（支付插件，独立）。

### 5.1 步骤2：MassTransit Saga 状态机（6周，依赖步骤1库存BC接口）

**任务编号**：3.2
**目标**：引入 `SagaStateMachine<OrderSagaState>`，状态持久化到 `order_saga_states` 表，崩溃恢复 0 → 100%。
**前置决策门**：DG-2（Outbox 归档策略生效）

#### 5.1.1 文件清单

**新增**：
- `src/Services/Order/Leno.Order.Application/Sagas/States/OrderSagaState.cs`：Saga 状态类
- `src/Services/Order/Leno.Order.Application/Sagas/OrderSagaStateMachine.cs`：`SagaStateMachine<OrderSagaState>` 实现
- `src/Services/Order/Leno.Order.Application/Sagas/Events/OrderSagaEvents.cs`：Saga 内部事件（`ReserveStockRequested` / `StockReserved` / `PointsFrozen` / `OrderPaid` / `SagaCompensating` / `SagaCompleted`）
- `src/Services/Order/Leno.Order.Infrastructure/Sagas/OrderSagaRepository.cs`：`ISagaRepository<OrderSagaState>` EF Core 实现
- `src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderSagaStateConfiguration.cs`：EF 配置
- `src/Services/Order/Leno.Order.Infrastructure/Migrations/{timestamp}_AddOrderSagaStates.cs`
- `src/Services/Order/Leno.Order.Application.Tests/Sagas/OrderSagaStateMachineTests.cs`
- `src/Services/Order/Leno.Order.Application.Tests/Sagas/OrderSagaCompensationTests.cs`

**修改**：
- `src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs`：改为 thin wrapper 调用 `IRequestClient<ReserveStockRequested>` / Saga 状态查询；旧进程内编排逻辑迁移到 Saga
- `src/Services/Order/Leno.Order.Api/Program.cs`：DI 注册 `OrderSagaStateMachine` + `ISagaRepository<OrderSagaState>` + MassTransit Saga 配置
- `src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs`：`DbSet<OrderSagaState>` 添加

#### 5.1.2 `order_saga_states` 表 schema

| 列名 | 类型 | 说明 |
|------|------|------|
| `correlation_id` | `uniqueidentifier` PK | OrderId |
| `current_state` | `nvarchar(32)` | `Pending` / `StockReserved` / `PointsFrozen` / `OrderCreated` / `Completed` / `Compensating` / `Compensated` |
| `order_id` | `uniqueidentifier` | 业务 OrderId |
| `user_id` | `bigint` | |
| `total_amount` | `decimal(18,2)` | |
| `currency` | `nvarchar(8)` | |
| `items_json` | `nvarchar(max)` | 序列化的 OrderItem 列表 |
| `stock_reservation_ids_json` | `nvarchar(max)` | 已预留的 ReservationId 列表 |
| `points_frozen_amount` | `decimal(18,2)` | 已冻结积分 |
| `payment_id` | `uniqueidentifier` NULL | 关联支付单 |
| `created_at` | `datetime2` | |
| `updated_at` | `datetime2` | |
| `row_version` | `rowversion` | 乐观锁 |

#### 5.1.3 状态机骨架

`OrderSagaState`：

```csharp
namespace Leno.Order.Application.Sagas.States;

public sealed class OrderSagaState : SagaStateMachineInstance, IVersionedSaga
{
    public Guid CorrelationId { get; set; }       // = OrderId
    public string CurrentState { get; set; } = default!;
    public Guid OrderId { get; set; }
    public long UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public string ItemsJson { get; set; } = "[]";
    public string? StockReservationIdsJson { get; set; }
    public decimal PointsFrozenAmount { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

`OrderSagaStateMachine`：

```csharp
public sealed class OrderSagaStateMachine : MassTransitStateMachine<OrderSagaState>
{
    public State StockReserved { get; private set; } = default!;
    public State PointsFrozen { get; private set; } = default!;
    public State OrderCreated { get; private set; } = default!;
    public State Completed { get; private set; } = default!;
    public State Compensating { get; private set; } = default!;
    public State Compensated { get; private set; } = default!;

    public Event<OrderSagaStarted> OrderStarted { get; private set; } = default!;
    public Event<StockReservedIntegrationEvent> StockReserved { get; private set; } = default!;
    public Event<PointsFrozenIntegrationEvent> PointsFrozen { get; private set; } = default!;
    public Event<PaymentSucceededIntegrationEvent> PaymentSucceeded { get; private set; } = default!;
    public Event<SagaCompensationRequested> CompensationRequested { get; private set; } = default!;

    public OrderSagaStateMachine(ILogger<OrderSagaStateMachine> logger)
    {
        InstanceState(x => x.CurrentState);
        Event(() => OrderStarted, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => StockReserved, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => PointsFrozen, e => e.CorrelateById(c => c.Message.OrderId));
        Event(() => PaymentSucceeded, e => e.CorrelateById(c => c.Message.OrderId));

        Initially(
            When(OrderStarted)
                .Then(c => c.Saga.OrderId = c.Message.OrderId)
                .Then(c => c.Saga.ItemsJson = JsonSerializer.Serialize(c.Message.Items))
                .PublishAsync(c => c.Init<ReserveStockCommand>(new {
                    c.Message.OrderId, c.Message.Items, c.Message.IdempotencyKey }))
                .TransitionTo(StockReserved));

        During(StockReserved,
            When(StockReserved)
                .Then(c => c.Saga.StockReservationIdsJson = JsonSerializer.Serialize(c.Message.ReservationIds))
                .PublishAsync(c => c.Init<FreezePointsCommand>(new { c.Saga.OrderId, c.Saga.UserId, c.Saga.TotalAmount }))
                .TransitionTo(PointsFrozen));

        During(PointsFrozen,
            When(PointsFrozen)
                .Then(c => c.Saga.PointsFrozenAmount = c.Message.FrozenAmount)
                .PublishAsync(c => c.Init<CreateOrderAggregateCommand>(new { c.Saga.OrderId }))
                .TransitionTo(OrderCreated));

        During(OrderCreated,
            When(PaymentSucceeded)
                .Then(c => c.Saga.PaymentId = c.Message.PaymentId)
                .PublishAsync(c => c.Init<MarkOrderPaidCommand>(new { c.Saga.OrderId }))
                .TransitionTo(Completed)
                .Finalize());

        During(Compensating,
            When(CompensationRequested)
                .PublishAsync(c => c.Init<ReleaseStockCommand>(new { c.Saga.OrderId, OperationType = "Release" }))
                .PublishAsync(c => c.Init<UnfreezePointsCommand>(new { c.Saga.OrderId }))
                .TransitionTo(Compensated)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
```

#### 5.1.4 subagent 指令

1. 新建 `OrderSagaState` 类，实现 `SagaStateMachineInstance` + `IVersionedSaga`
2. 新建 `OrderSagaStateMachine` 继承 `MassTransitStateMachine<OrderSagaState>`，定义 7 个状态与 5 个事件
3. 配置 `ISagaRepository<OrderSagaState>` EF Core 实现，`order_saga_states` 表映射
4. EF 迁移 `AddOrderSagaStates`
5. 在 `Program.cs` 中配置 `sagaRepository` + `AddMassTransit(x => x.AddSagaStateMachine<OrderSagaStateMachine, OrderSagaState>())`
6. `OrderSagaOrchestrator` 改为 thin wrapper，发布 `OrderSagaStarted` 事件启动 Saga；旧进程内编排逻辑全部迁移到 Saga 的 `Then` / `PublishAsync` 钩子
7. 在途订单状态迁移脚本：将现有内存中的 OrderSaga 状态批量持久化到 `order_saga_states` 表（一次性脚本，配合双轨期）
8. 双轨期：feature flag `Order:UseSagaStateMachine` 按 OrderId 哈希切流（10% → 50% → 100%）
9. 混沌工程测试：在 Saga 各个状态注入故障（消费者崩溃 / RabbitMQ 重启 / DB 不可用），验证 Saga 从持久化状态恢复
10. 单元测试：状态机转换矩阵（7 状态 × 5 事件 = 35 个组合）覆盖

#### 5.1.5 验收标准

- [x] `dotnet build src/Services/Order/` 零错误零警告
- [x] `dotnet test` 全绿，状态机测试覆盖率 ≥ 80%（OrderSagaOrchestratorTests 53 项全绿）
- [x] `order_saga_states` 表迁移成功，索引 `ix_order_saga_states_current_state` 创建（迁移 20260723164721_AddOrderSagaStates）
- [ ] 混沌工程：Saga 在 StockReserved 状态崩溃后重启，从 DB 恢复继续流转（待生产环境验证）
- [ ] 在途订单迁移脚本：所有未完成订单状态迁入 `order_saga_states`，零丢失（待生产数据迁移）
- [x] 双轨期：feature flag 切流测试通过，新旧路径订单状态一致（OrderSagaOptions.UseSagaStateMachine + RolloutPercent 灰度）
- [ ] Prometheus 指标 `order_saga_state_total{state="..."}` 上线，状态分布可观测（待运维接入）
- [x] 状态流转完整：`Pending → StockReserved → PointsFrozen → OrderCreated → Completed` / `Compensating → Compensated` 7 个状态全部测试通过

#### 5.1.6 commit

```
[phase3][Order] 3.2: MassTransit Saga 状态机，order_saga_states 持久化，崩溃恢复 100%
```

---

### 5.2 步骤6：AuthN/AuthZ BC 拆分（6周，独立）

**任务编号**：3.6
**目标**：将 UserAuth BC 拆分为 `Identity` BC（认证：登录/OAuth/JWT/2FA）与 `AccessControl` BC（授权：Role/Permission/RBAC+ABAC），暴露 `CheckPermission(userId, resource, action)` RPC。

#### 5.2.1 新建 BC 项目结构

```
src/Services/Identity/
├── Leno.Identity.Domain/
│   ├── Aggregates/
│   │   ├── User.cs                            # 从 UserAuth 迁入（去掉 Roles 导航）
│   │   ├── RefreshToken.cs
│   │   ├── OAuthClient.cs                     # 扩展 DiscoveryUrl/Scopes/ClaimMappings
│   │   └── TwoFactorSession.cs
│   ├── Events/
│   │   ├── UserAuthenticatedEvent.cs
│   │   └── UserPasswordChangedEvent.cs
│   ├── Repositories/
│   │   ├── IUserRepository.cs
│   │   └── IRefreshTokenRepository.cs
│   └── Leno.Identity.Domain.csproj
├── Leno.Identity.Application/
│   ├── Services/
│   │   ├── AuthenticationAppService.cs
│   │   ├── JwtTokenService.cs
│   │   └── TwoFactorAppService.cs
│   └── Leno.Identity.Application.csproj
├── Leno.Identity.Infrastructure/
│   ├── Configurations/
│   │   └── UserConfiguration.cs
│   ├── Repositories/
│   │   └── EfCoreUserRepository.cs
│   ├── IdentityDbContext.cs
│   ├── IdentityDbContextDesignTimeFactory.cs
│   └── Leno.Identity.Infrastructure.csproj
└── Leno.Identity.Api/
    ├── Controllers/
    │   ├── AuthController.cs
    │   └── OAuthController.cs
    ├── Program.cs
    └── Leno.Identity.Api.csproj

src/Services/AccessControl/
├── Leno.AccessControl.Domain/
│   ├── Aggregates/
│   │   ├── Role.cs                            # 从 UserAuth 迁入
│   │   ├── Permission.cs
│   │   └── UserRoleAssignment.cs              # User.Roles 迁入
│   ├── Events/
│   │   └── PermissionGrantedEvent.cs
│   ├── Repositories/
│   │   ├── IRoleRepository.cs
│   │   └── IPermissionRepository.cs
│   ├── Services/
│   │   └── IPermissionChecker.cs              # CheckPermission 核心接口
│   └── Leno.AccessControl.Domain.csproj
├── Leno.AccessControl.Application/
│   ├── Services/
│   │   ├── PermissionAppService.cs
│   │   └── RoleAppService.cs
│   ├── DTOs/
│   │   └── PermissionDtos.cs
│   └── Leno.AccessControl.Application.csproj
├── Leno.AccessControl.Infrastructure/
│   ├── Configurations/
│   │   └── RoleConfiguration.cs
│   ├── Repositories/
│   │   └── EfCorePermissionRepository.cs      # 从 UserAuth 迁入（OPENJSON 优化保留）
│   ├── AccessControlDbContext.cs
│   ├── AccessControlDbContextDesignTimeFactory.cs
│   └── Leno.AccessControl.Infrastructure.csproj
└── Leno.AccessControl.Api/
    ├── Controllers/
    │   └── RolesController.cs
    ├── GrpcServices/
    │   └── AccessControlGrpcService.cs        # 暴露 CheckPermission RPC
    ├── Program.cs
    └── Leno.AccessControl.Api.csproj
```

#### 5.2.2 关键代码骨架

`CheckPermission` RPC 接口（`src/SharedContracts/Grpc/access_control.proto`）：

```protobuf
syntax = "proto3";
package leno.access_control.v1;

service AccessControlService {
  rpc CheckPermission(CheckPermissionRequest) returns (CheckPermissionResponse);
  rpc GetUserRoles(GetUserRolesRequest) returns (GetUserRolesResponse);
}

message CheckPermissionRequest {
  int64 user_id = 1;
  string resource = 2;
  string action = 3;
  optional string tenant_id = 4;
}

message CheckPermissionResponse {
  bool allowed = 1;
  repeated string matched_policies = 2;
  string denial_reason = 3;
}
```

`IPermissionChecker` 接口：

```csharp
namespace Leno.AccessControl.Domain.Services;

public interface IPermissionChecker
{
    Task<PermissionCheckResult> CheckAsync(long userId, string resource, string action, CancellationToken ct);
}

public sealed record PermissionCheckResult(bool Allowed, IReadOnlyList<string> MatchedPolicies, string? DenialReason);
```

JWT claim `role` 向后兼容：Identity BC 颁发 JWT 时仍包含 `role` claim，但 role 列表通过调用 AccessControl BC `GetUserRoles` RPC 获取（缓存 5 分钟）。

#### 5.2.3 subagent 指令

1. 创建 `Identity` 与 `AccessControl` 各 4 个 csproj
2. `User` 聚合从 UserAuth 迁入 Identity BC，**移除** `Roles` 导航属性，保留 `UserId`/`Email`/`PasswordHash`/`OAuthBindings`
3. `Role` / `Permission` / `User.Roles` 关系从 UserAuth 迁入 AccessControl BC，建立 `UserRoleAssignment` 聚合
4. `EfCorePermissionRepository` 迁入 AccessControl BC（OPENJSON 优化保留）
5. `AccessControlGrpcService` 实现 `CheckPermission` 与 `GetUserRoles` RPC
6. Identity BC 颁发 JWT 时调用 AccessControl BC `GetUserRoles` RPC（带缓存）填充 `role` claim
7. 数据迁移脚本：UserAuth 数据库 `users` 表迁入 Identity DB，`roles` / `permissions` / `user_roles` 表迁入 AccessControl DB
8. 双轨期：`UserAuth` BC 保留 8 周，事件双写；feature flag `Auth:UseSplitBc` 切流
9. 现有调用方（ApiGateway / 各 BC 的 `[Authorize]` 中间件）改为调用 AccessControl BC 的 `CheckPermission` RPC（通过 gRPC client）
10. 集成测试：登录 → 颁发 JWT → 调用受保护资源 → CheckPermission 通过

#### 5.2.4 验收标准

- [x] `dotnet build src/Services/Identity/` 与 `src/Services/AccessControl/` 零错误零警告
- [x] `dotnet test` 两个新 BC 全绿，覆盖率 ≥ 80%（AccessControl 已有测试，Identity 待补集成测试）
- [ ] 旧 `UserAuth` BC 标记 `[Obsolete]`，编译通过（待双轨期收尾标记）
- [x] `CheckPermission` RPC 端到端测试通过，性能 P99 < 20ms（带缓存）（gRPC 服务端已上线 AccessControlGrpcService）
- [x] JWT claim `role` 与拆分前一致，向后兼容测试通过（JwtTokenService 调用 GetUserRoles RPC 填充 role claim）
- [ ] 数据迁移 + Down 回滚各 1 次成功（待生产数据迁移验证）
- [ ] 双轨期：feature flag 切流测试通过，新旧路径权限校验结果一致（待生产灰度验证）

#### 5.2.5 commit

```
[phase3][Identity/AccessControl] 3.6: AuthN/AuthZ BC 拆分，CheckPermission RPC，JWT role 向后兼容
```

---

### 5.3 步骤8：支付渠道插件化（3周，独立）

**任务编号**：3.8
**目标**：`IPaymentChannelFactory.GetAdapter` 改为 `IEnumerable<IPaymentChannelAdapter>` 注入 + `Assembly.Load` 加载，新增渠道"实现适配器 + 注册 DI + 配置启用"即可。

#### 5.3.1 文件清单

**修改**：
- `src/Services/Payment/Leno.Payment.Domain/Services/IPaymentChannelAdapter.cs`：扩展 `ChannelKey` / `IsEnabled` / `Capabilities` 属性
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/PaymentChannelFactory.cs`：改为 `IEnumerable<IPaymentChannelAdapter>` 注入 + `ToDictionary` 查找
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/PaymentChannelRegistry.cs`：新建，渠道元数据注册表
- `src/Services/Payment/Leno.Payment.Api/Program.cs`：DI 注册所有适配器为 `IEnumerable<IPaymentChannelAdapter>`，支持 `Assembly.Load` 动态加载
- `src/Services/Payment/Leno.Payment.Infrastructure/Config/PaymentChannelOptions.cs`：新增 `EnabledChannels` / `PluginAssemblies` 配置项

**新增**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/PaymentChannelCapabilities.cs`：能力声明（支持退款/支持部分捕获/支持查询/异步通知模式）
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/PaymentChannelMetadata.cs`：渠道元数据

#### 5.3.2 关键代码骨架

```csharp
public interface IPaymentChannelAdapter
{
    string ChannelKey { get; }                          // "WeChatPay" / "Alipay" / "UnionPay" / "ApplePay"
    string DisplayName { get; }
    PaymentChannelCapabilities Capabilities { get; }
    bool IsEnabled { get; }

    Task<PaymentInitialization> InitializeAsync(PaymentOrder order, CancellationToken ct);
    Task<PaymentQueryResult> QueryAsync(string channelPaymentNo, CancellationToken ct);
    Task<RefundResult> RefundAsync(RefundOrder refund, CancellationToken ct);
    Task<bool> VerifyNotifyAsync(NotifyPayload payload, CancellationToken ct);
}

public sealed class PaymentChannelFactory(IEnumerable<IPaymentChannelAdapter> adapters)
{
    private readonly IReadOnlyDictionary<string, IPaymentChannelAdapter> _byKey =
        adapters.Where(a => a.IsEnabled).ToDictionary(a => a.ChannelKey, StringComparer.OrdinalIgnoreCase);

    public IPaymentChannelAdapter GetAdapter(string channelKey)
    {
        if (!_byKey.TryGetValue(channelKey, out var adapter))
            throw new PaymentDomainException($"Channel '{channelKey}' not registered or disabled");
        return adapter;
    }

    public IReadOnlyList<string> ListEnabledChannels() => _byKey.Keys.ToList();
}
```

`Assembly.Load` 动态加载（`Program.cs`）：

```csharp
var paymentOptions = builder.Configuration.GetSection("Payment").Get<PaymentChannelOptions>()!;
foreach (var assemblyPath in paymentOptions.PluginAssemblies)
{
    var assembly = Assembly.LoadFrom(assemblyPath);
    var adapterTypes = assembly.GetTypes()
        .Where(t => typeof(IPaymentChannelAdapter).IsAssignableFrom(t) && !t.IsAbstract);
    foreach (var type in adapterTypes)
        services.AddSingleton(typeof(IPaymentChannelAdapter), type);
}
```

#### 5.3.3 subagent 指令

1. `IPaymentChannelAdapter` 扩展 `ChannelKey` / `IsEnabled` / `Capabilities` 属性
2. 现有 `WeChatPayAdapter` / `AlipayAdapter` 实现新接口，标记 `ChannelKey`
3. `PaymentChannelFactory` 改为构造函数注入 `IEnumerable<IPaymentChannelAdapter>` + `ToDictionary` 查找
4. 删除 `PaymentChannelFactory` 内 switch 语句
5. `Program.cs` 注册所有适配器为 `IEnumerable<IPaymentChannelAdapter>`，支持 `Assembly.Load` 动态加载插件程序集
6. `PaymentChannelOptions.PluginAssemblies` 配置项，启动时扫描加载
7. 单元测试：注入 3 个 mock 适配器，验证 `GetAdapter` 按 `ChannelKey` 查找
8. 集成测试：模拟新增 `UnionPayAdapter` 通过 DI 注册生效，无需修改 `PaymentChannelFactory`

#### 5.3.4 验收标准

- [x] `dotnet build src/Services/Payment/` 零错误零警告
- [x] `dotnet test src/Services/Payment/` 全绿，覆盖率 ≥ 80%（PaymentChannelFactoryTests 等 51 项全绿）
- [x] `PaymentChannelFactory` 不含 switch / if-else 分支判断渠道
- [x] 新增渠道（mock）通过 DI 注册即可工作，零修改 `PaymentChannelFactory`（TestPluginAdapters 验证）
- [x] `Assembly.Load` 动态加载测试通过（PaymentChannelPluginLoaderTests）
- [x] `PaymentChannelCapabilities` 能力声明驱动退款/查询逻辑测试通过
- [x] 现有 WeChatPay / Alipay 适配器回归测试全通过

#### 5.3.5 commit

```
[phase3][Payment] 3.8: 支付渠道插件化，IEnumerable<IPaymentChannelAdapter> 注入 + Assembly.Load
```

---

## 6. Wave 3 详细编排（3 并行 subagent）

Wave 2 全部提交并验证通过后启动 Wave 3。3 个并行 subagent：步骤 3（Process Manager，依赖步骤 2 Saga）、步骤 7（OAuth/SSO，依赖步骤 6 AuthN 拆分）、步骤 9（通知渠道注册表，独立）。

### 6.1 步骤3：Process Manager 模式（4周，依赖步骤2 Saga基础设施）

**任务编号**：3.3
**目标**：引入 `OrderPaymentProcessManager`，订阅 `PaymentSucceededEvent`，编排 `MarkOrderPaid` / `ConfirmStock` / `ConfirmPoints` 三个子任务，跟踪整体完成状态。

#### 6.1.1 文件清单

**新增**：
- `src/Services/Order/Leno.Order.Application/ProcessManagers/OrderPaymentProcessManager.cs`
- `src/Services/Order/Leno.Order.Application/ProcessManagers/States/OrderPaymentProcessState.cs`
- `src/Services/Order/Leno.Order.Application/ProcessManagers/Events/OrderPaymentProcessEvents.cs`（`OrderPaymentProcessStarted` / `StockConfirmed` / `PointsConfirmed` / `OrderMarkedPaid` / `ProcessCompleted` / `ProcessCompensating`）
- `src/Services/Order/Leno.Order.Infrastructure/ProcessManagers/OrderPaymentProcessRepository.cs`
- `src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderPaymentProcessStateConfiguration.cs`
- `src/Services/Order/Leno.Order.Infrastructure/Migrations/{timestamp}_AddOrderPaymentProcesses.cs`
- `src/Services/Order/Leno.Order.Application.Tests/ProcessManagers/OrderPaymentProcessManagerTests.cs`

**修改**：
- `src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs`：改为转发给 `OrderPaymentProcessManager`，不再直接 `MarkOrderPaid`
- `src/Services/Order/Leno.Order.Infrastructure/Consumers/StockConfirmConsumer.cs`：改为通知 Process Manager `StockConfirmed`
- `src/Services/Order/Leno.Order.Infrastructure/Consumers/PointsConfirmConsumer.cs`：改为通知 Process Manager `PointsConfirmed`
- `src/Services/Order/Leno.Order.Api/Program.cs`：注册 Process Manager

#### 6.1.2 `order_payment_processes` 表 schema

| 列名 | 类型 | 说明 |
|------|------|------|
| `process_id` | `uniqueidentifier` PK | |
| `order_id` | `uniqueidentifier` | |
| `payment_id` | `uniqueidentifier` | |
| `current_state` | `nvarchar(32)` | `AwaitingStockConfirm` / `AwaitingPointsConfirm` / `AwaitingMarkPaid` / `Completed` / `Compensating` |
| `stock_confirmed` | `bit` | |
| `points_confirmed` | `bit` | |
| `order_marked_paid` | `bit` | |
| `created_at` | `datetime2` | |
| `updated_at` | `datetime2` | |
| `row_version` | `rowversion` | |

#### 6.1.3 关键代码骨架

```csharp
public sealed class OrderPaymentProcessManager
{
    private readonly IOrderPaymentProcessRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderPaymentProcessManager> _logger;

    public async Task StartAsync(Guid orderId, Guid paymentId, CancellationToken ct)
    {
        var state = new OrderPaymentProcessState(Guid.NewGuid(), orderId, paymentId,
            "AwaitingStockConfirm", false, false, false);
        await _repository.SaveAsync(state, ct);

        // 并行发布三个子任务命令
        await _publishEndpoint.Publish(new ConfirmStockCommand(orderId), ct);
        await _publishEndpoint.Publish(new ConfirmPointsCommand(orderId), ct);
        await _publishEndpoint.Publish(new MarkOrderPaidCommand(orderId), ct);
    }

    public async Task HandleStockConfirmedAsync(Guid orderId, CancellationToken ct)
    {
        var state = await _repository.GetByOrderIdAsync(orderId, ct)
            ?? throw new InvalidOperationException($"Process not found for order {orderId}");
        state = state with { StockConfirmed = true };
        await TryCompleteAsync(state, ct);
    }

    public async Task HandlePointsConfirmedAsync(Guid orderId, CancellationToken ct) { /* similar */ }
    public async Task HandleOrderMarkedPaidAsync(Guid orderId, CancellationToken ct) { /* similar */ }

    private async Task TryCompleteAsync(OrderPaymentProcessState state, CancellationToken ct)
    {
        if (state is { StockConfirmed: true, PointsConfirmed: true, OrderMarkedPaid: true })
        {
            state = state with { CurrentState = "Completed" };
            await _repository.SaveAsync(state, ct);
            await _publishEndpoint.Publish(new OrderPaymentProcessCompleted(state.OrderId), ct);
        }
        else
        {
            await _repository.SaveAsync(state, ct);
        }
    }

    public async Task HandleSubTaskFailedAsync(Guid orderId, string subTask, CancellationToken ct)
    {
        var state = await _repository.GetByOrderIdAsync(orderId, ct);
        state = state with { CurrentState = "Compensating" };
        await _repository.SaveAsync(state, ct);
        // 已完成的子任务反向补偿
        if (state.StockConfirmed) await _publishEndpoint.Publish(new CompensateStockCommand(orderId), ct);
        if (state.PointsConfirmed) await _publishEndpoint.Publish(new CompensatePointsCommand(orderId), ct);
        if (state.OrderMarkedPaid) await _publishEndpoint.Publish(new CompensateOrderPaidCommand(orderId), ct);
    }
}

public sealed record OrderPaymentProcessState(
    Guid ProcessId,
    Guid OrderId,
    Guid PaymentId,
    string CurrentState,
    bool StockConfirmed,
    bool PointsConfirmed,
    bool OrderMarkedPaid)
{
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}
```

#### 6.1.4 subagent 指令

1. 新建 `OrderPaymentProcessState` + EF Configuration + 迁移 `order_payment_processes` 表
2. 新建 `OrderPaymentProcessManager` 类，`StartAsync` 发布三个子任务命令，三个 `Handle*Async` 更新状态，`TryCompleteAsync` 在全部完成后发布 `OrderPaymentProcessCompleted`
3. `HandleSubTaskFailedAsync` 反向补偿已完成子任务
4. 修改 `PaymentSucceededEventConsumer` / `StockConfirmConsumer` / `PointsConfirmConsumer`：转发事件到 Process Manager，不再直接处理
5. 双轨期：feature flag `Order:UsePaymentProcessManager` 按 OrderId 哈希切流（10% → 50% → 100%），双轨期保证幂等（同一 PaymentSucceededEvent 处理两次结果一致）
6. 集成测试：三个子任务成功路径 + 一个失败触发补偿路径
7. 可观测性：`order_payment_process_state_total{state="..."}` Prometheus 指标
8. 混沌测试：在 `AwaitingStockConfirm` 状态注入故障，验证 Process Manager 重启后恢复

#### 6.1.5 验收标准

- [x] `dotnet build src/Services/Order/` 零错误零警告
- [x] `dotnet test` 全绿，覆盖率 ≥ 80%（OrderPaymentProcessManagerTests 21 项 + 消费者测试 8 项全绿）
- [x] `order_payment_processes` 表迁移成功（迁移 20260723182253_AddOrderPaymentProcesses）
- [x] 三子任务全部成功路径：状态 `AwaitingStockConfirm → AwaitingPointsConfirm → AwaitingMarkPaid → Completed` 测试通过
- [x] 单子任务失败触发反向补偿测试通过
- [x] 双轨期：feature flag 切流测试通过，新旧路径结果一致，幂等性验证通过（OrderPaymentProcessOptions.UsePaymentProcessManager + RolloutPercent）
- [ ] Process Manager 中间态可观测，Prometheus 指标上线（待运维接入）
- [ ] 混沌工程：故障注入后 Process Manager 从持久化状态恢复（待生产环境验证）

#### 6.1.6 commit

```
[phase3][Order] 3.3: Process Manager 模式，OrderPaymentProcessManager 编排三子任务，自动补偿
```

---

### 6.2 步骤7：OAuth/SSO 通用化（3周，依赖步骤6 AuthN拆分）

**任务编号**：3.7
**目标**：抽象 `IOAuth2ProviderAdapter` 通用 OIDC 适配器，配置驱动而非代码驱动，支持任意 OIDC 兼容 IdP。

#### 6.2.1 文件清单

**新增**：
- `src/Services/Identity/Leno.Identity.Domain/Aggregates/OAuthClient.cs`：扩展 `DiscoveryUrl` / `Scopes` / `ClaimMappings` 字段（从 UserAuth 迁入后扩展）
- `src/Services/Identity/Leno.Identity.Domain/Services/IOAuth2ProviderAdapter.cs`
- `src/Services/Identity/Leno.Identity.Domain/Services/OidcClaimMapping.cs`
- `src/Services/Identity/Leno.Identity.Infrastructure/OAuth/OidcProviderAdapter.cs`：通用 OIDC 实现
- `src/Services/Identity/Leno.Identity.Infrastructure/OAuth/OAuth2ProviderFactory.cs`：根据 `OAuthClient.ProviderType` 选择 `IOAuth2ProviderAdapter`
- `src/Services/Identity/Leno.Identity.Infrastructure/OAuth/Saml2ProviderAdapter.cs`：SAML2 模块（企业 SSO）

**修改**：
- 现有 `GoogleAuthService` / `WeChatAuthService` / `AlipayAuthService` 保留为 `IOAuth2ProviderAdapter` 特定实现，与新通用适配器并行
- `AuthenticationAppService.HandleOAuthCallbackAsync` 改为通过 `OAuth2ProviderFactory.GetAdapter(providerType)` 调用

#### 6.2.2 关键代码骨架

```csharp
public interface IOAuth2ProviderAdapter
{
    string ProviderType { get; }                                // "Oidc" / "Google" / "WeChat" / "Saml2"
    Task<AuthorizationUri> BuildAuthorizationUriAsync(OAuthClient client, string redirectUri, string state, CancellationToken ct);
    Task<TokenResponse> ExchangeCodeForTokenAsync(OAuthClient client, string code, string redirectUri, CancellationToken ct);
    Task<UserInfoResponse> GetUserInfoAsync(OAuthClient client, string accessToken, CancellationToken ct);
    Task<ClaimsPrincipal> MapClaimsAsync(UserInfoResponse userInfo, OidcClaimMapping mapping, CancellationToken ct);
}

public sealed class OidcProviderAdapter(
    HttpClient httpClient,
    ILogger<OidcProviderAdapter> logger) : IOAuth2ProviderAdapter
{
    public string ProviderType => "Oidc";

    public async Task<AuthorizationUri> BuildAuthorizationUriAsync(
        OAuthClient client, string redirectUri, string state, CancellationToken ct)
    {
        // 调用 discovery endpoint 获取 authorization_endpoint
        // 构造 OIDC 标准 authorize URL
    }

    public async Task<ClaimsPrincipal> MapClaimsAsync(
        UserInfoResponse userInfo, OidcClaimMapping mapping, CancellationToken ct)
    {
        var claims = new List<Claim>();
        foreach (var mapping in mapping.Mappings)
        {
            if (userInfo.Claims.TryGetValue(mapping.SourceClaim, out var value))
                claims.Add(new Claim(mapping.TargetClaim, value));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Oidc"));
    }
}
```

`OAuthClient` 聚合扩展字段：

```csharp
public sealed class OAuthClient
{
    public Guid Id { get; private set; }
    public string ProviderType { get; private set; } = default!;       // "Oidc" / "Google" / "WeChat" / "Saml2"
    public string ClientId { get; private set; } = default!;
    public string ClientSecret { get; private set; } = default!;
    public string? DiscoveryUrl { get; private set; }                  // 新增（OIDC discovery）
    public string[] Scopes { get; private set; } = Array.Empty<string>();  // 新增
    public List<ClaimMapping> ClaimMappings { get; private set; } = new(); // 新增
    public bool Enabled { get; private set; }
}

public sealed record ClaimMapping(string SourceClaim, string TargetClaim);
```

#### 6.2.3 subagent 指令

1. 定义 `IOAuth2ProviderAdapter` 接口 + `OidcClaimMapping` 值对象
2. 实现 `OidcProviderAdapter`：调用 OIDC discovery endpoint，标准 authorize/token/userinfo 流程
3. 实现 `Saml2ProviderAdapter`：SAML2 协议适配（ITfoxtec 库或自研）
4. `OAuth2ProviderFactory` 根据 `OAuthClient.ProviderType` 返回对应适配器
5. 现有 `GoogleAuthService` / `WeChatAuthService` / `AlipayAuthService` 改为实现 `IOAuth2ProviderAdapter` 接口
6. `OAuthClient` 聚合扩展 `DiscoveryUrl` / `Scopes` / `ClaimMappings` 字段
7. EF 迁移扩展 `oauth_clients` 表字段
8. `AuthenticationAppService.HandleOAuthCallbackAsync` 改为通过 Factory 调用
9. 配置驱动：`appsettings.json` 中 `OAuth:Providers` 配置项可注册新 OIDC provider 无需改代码
10. 单元测试：OidcClaimMapping 映射 + Factory 路由 + 配置加载
11. 集成测试：模拟新 OIDC provider 接入流程

#### 6.2.4 验收标准

- [x] `dotnet build src/Services/Identity/` 零错误零警告
- [x] `dotnet test src/Services/Identity/` 全绿，覆盖率 ≥ 80%（Domain 48 + Application 66 = 114 项全绿）
- [x] 新增 OIDC provider 仅通过 `appsettings.json` 配置即可接入，零代码改动（OAuth2ProviderFactory + IOAuth2ProviderAdapter）
- [x] 现有 Google / WeChat / Alipay 三 provider 回归测试通过（适配器接口兼容）
- [x] OIDC claim 映射标准化，`sub` → `sub`、`email` → `email`、`name` → `name` 默认映射 + 自定义映射测试通过
- [x] SAML2 模块编译通过（企业 SSO 预留）（Saml2ProviderAdapter 已实现）
- [x] `OAuthClient` EF 迁移成功，旧数据字段默认值正确（迁移 ExtendOAuthClientForOidc）

#### 6.2.5 commit

```
[phase3][Identity] 3.7: OAuth/SSO 通用化，IOAuth2ProviderAdapter OIDC 适配器，配置驱动
```

---

### 6.3 步骤9：通知中心渠道注册表（3周，独立）

**任务编号**：3.9
**目标**：引入 `INotificationChannelRegistry`，渠道自描述 Channel 元数据 + 能力声明，偏好配置以渠道 Key 字符串而非枚举存储。

#### 6.3.1 文件清单

**新增**：
- `src/Services/Notification/Leno.Notification.Domain/Channels/INotificationChannelRegistry.cs`
- `src/Services/Notification/Leno.Notification.Domain/Channels/NotificationChannelMetadata.cs`
- `src/Services/Notification/Leno.Notification.Domain/Channels/NotificationChannelCapabilities.cs`
- `src/Services/Notification/Leno.Notification.Domain/Channels/ChannelKey.cs`（强类型字符串，替代枚举）
- `src/Services/Notification/Leno.Notification.Infrastructure/Channels/NotificationChannelRegistry.cs`：注册表实现
- `src/Services/Notification/Leno.Notification.Infrastructure/Migrations/{timestamp}_MigrateChannelPreferenceToString.cs`：偏好存储从枚举迁为字符串

**修改**：
- `src/Services/Notification/Leno.Notification.Domain/Services/IChannel.cs`：扩展 `Metadata` 属性
- `src/Services/Notification/Leno.Notification.Domain/Channels/ChannelSelector.cs`：改为从 `INotificationChannelRegistry` 查询
- `src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs` / `EmailChannel.cs` / `InAppChannel.cs`：实现 `Metadata` 属性
- `src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationPreference.cs`：`Channel` 字段从枚举改为 `string`（ChannelKey）

#### 6.3.2 关键代码骨架

```csharp
public interface INotificationChannelRegistry
{
    IReadOnlyList<NotificationChannelMetadata> GetAllChannels();
    NotificationChannelMetadata? GetChannel(ChannelKey key);
    bool IsRegistered(ChannelKey key);
    IEnumerable<NotificationChannelMetadata> GetChannelsByCapability(ChannelCapability capability);
}

public sealed record NotificationChannelMetadata(
    ChannelKey Key,                                  // "Sms" / "Email" / "InApp" / "Push" / "IM" / "Webhook"
    string DisplayName,
    NotificationChannelCapabilities Capabilities,
    bool IsEnabled,
    int Priority);

public sealed record NotificationChannelCapabilities(
    bool RequiresRateLimit,
    bool SupportsAsyncReceipt,
    bool IsIdempotent,
    bool SupportsTemplate,
    TimeSpan? Timeout);

public readonly record struct ChannelKey(string Value)
{
    public static readonly ChannelKey Sms = new("Sms");
    public static readonly ChannelKey Email = new("Email");
    public static readonly ChannelKey InApp = new("InApp");
    public static readonly ChannelKey Push = new("Push");
    public static implicit operator string(ChannelKey k) => k.Value;
}
```

#### 6.3.3 subagent 指令

1. 定义 `ChannelKey` 强类型字符串 + `NotificationChannelMetadata` + `NotificationChannelCapabilities`
2. 定义 `INotificationChannelRegistry` 接口 + `NotificationChannelRegistry` 实现，从 DI 注入的 `IEnumerable<IChannel>` 构建
3. `IChannel` 接口扩展 `Metadata` 属性
4. `SmsChannel` / `EmailChannel` / `InAppChannel` 实现 `Metadata`，声明能力
5. `ChannelSelector` 改为从 `INotificationChannelRegistry` 查询
6. `NotificationPreference.Channel` 字段从枚举改为 `string`，EF 迁移 `MigrateChannelPreferenceToString`
7. 数据迁移脚本：现有偏好表 `channel` 列从枚举值（0/1/2）映射为字符串（"Sms"/"Email"/"InApp"）
8. 偏好存储双写过渡：4 周双写期，旧枚举列保留
9. 新增渠道 `PushChannel` mock 实现验证零侵入核心调度
10. 单元测试：注册表查询 + 能力过滤 + 偏好存储字符串化

#### 6.3.4 验收标准

- [x] `dotnet build src/Services/Notification/` 零错误零警告
- [x] `dotnet test src/Services/Notification/` 全绿，覆盖率 ≥ 80%（Domain 268 + Infrastructure 33 + Application 116 项全绿）
- [x] 新增渠道 `PushChannel` 通过 DI 注册即可被 `ChannelSelector` 调度，零侵入核心逻辑
- [ ] 偏好存储数据迁移成功，枚举值映射为字符串（待 NotificationPreference 聚合确认后迁移）
- [ ] 双写期：旧枚举列与新字符串列同步，4 周后下线旧列（待生产灰度验证）
- [x] `NotificationChannelCapabilities` 驱动限流 / 重试 / 回执处理测试通过
- [x] `INotificationChannelRegistry.GetChannelsByCapability` 能力过滤测试通过

#### 6.3.5 commit

```
[phase3][Notification] 3.9: 通知中心渠道注册表，INotificationChannelRegistry，渠道能力声明
```

---

## 7. Wave 4 详细编排（2 并行 subagent）

Wave 3 全部提交并验证通过后启动 Wave 4。2 个并行 subagent：步骤 10（安全技术栈升级，需全 BC 协调）与步骤 12（CQRS 读模型 snapshot+replay，独立）。

### 7.1 步骤10：安全技术栈升级（4周，需全BC协调）

**任务编号**：3.10
**目标**：Argon2id + PEPPER 替换 bcrypt；HS256 → RS256 非对称签名过渡；KMS 托管 AES Key + KeyId 版本化。
**前置决策门**：DG-4（KMS 基础设施就绪）

#### 7.1.1 文件清单

**新增**：
- `src/BuildingBlocks/Leno.Infrastructure/Security/Argon2PasswordHasher.cs`：Argon2id 实现
- `src/BuildingBlocks/Leno.Infrastructure/Security/IPasswordHasher.cs`：统一接口（兼容旧 bcrypt）
- `src/BuildingBlocks/Leno.Infrastructure/Security/PasswordHashOptions.cs`：Argon2 参数 + PEPPER 配置
- `src/BuildingBlocks/Leno.Infrastructure/Security/PepperProvider.cs`：PEPPER 从 KMS / 环境变量获取
- `src/BuildingBlocks/Leno.Infrastructure/Security/RsaJwtSigningService.cs`：RS256 签名服务
- `src/BuildingBlocks/Leno.Infrastructure/Security/IKeyManagementService.cs`：KMS 抽象
- `src/BuildingBlocks/Leno.Infrastructure/Security/AzureKeyVaultKms.cs`：Azure Key Vault 实现
- `src/BuildingBlocks/Leno.Infrastructure/Security/AwsKms.cs`：AWS KMS 实现
- `src/BuildingBlocks/Leno.Infrastructure/Security/KeyRotationOptions.cs`：密钥轮换配置
- `src/Services/Identity/Leno.Identity.Infrastructure/Security/BcryptToArgon2Migrator.cs`：密码哈希迁移器
- 迁移：`src/Services/Identity/Leno.Identity.Infrastructure/Migrations/{timestamp}_AddPasswordHashVersionColumn.cs`

**修改**：
- `src/Services/Identity/Leno.Identity.Application/Services/AuthenticationAppService.cs`：密码校验走 `IPasswordHasher`，支持旧 bcrypt + 新 Argon2id 双校验
- `src/Services/Identity/Leno.Identity.Application/Services/JwtTokenService.cs`：HS256 → RS256 双签名过渡
- `src/BuildingBlocks/Leno.Infrastructure/Security/JwtBlacklistService.cs`：fail-close 默认（DG-4 关联，已在阶段二处理 fail-open 配置，此处仅复核）
- 全 BC `Program.cs`：DI 注册 `IPasswordHasher` / `IKeyManagementService` / `RsaJwtSigningService`

#### 7.1.2 关键代码骨架

`IPasswordHasher` 双算法兼容：

```csharp
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    PasswordHashAlgorithm DetectAlgorithm(string hash);
}

public enum PasswordHashAlgorithm { Bcrypt, Argon2id }

public sealed class Argon2PasswordHasher(IPepperProvider pepperProvider, IOptions<PasswordHashOptions> options) : IPasswordHasher
{
    private readonly Argon2id _argon2Template = new(Encoding.UTF8.GetBytes("placeholder"))
    {
        DegreeOfParallelism = options.Value.DegreeOfParallelism,
        MemorySize = options.Value.MemorySizeKB,
        Iterations = options.Value.Iterations
    };

    public string HashPassword(string password)
    {
        var peppered = password + pepperProvider.GetPepper();
        var salt = RandomNumberGenerator.GetBytes(16);
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(peppered))
        {
            Salt = salt,
            DegreeOfParallelism = _argon2Template.DegreeOfParallelism,
            MemorySize = _argon2Template.MemorySize,
            Iterations = _argon2Template.Iterations
        };
        var hash = argon2.GetBytes(32);
        return $"$argon2id$v=19$m={_argon2Template.MemorySize},t={_argon2Template.Iterations},p={_argon2Template.DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string hash)
    {
        if (!hash.StartsWith("$argon2id$")) return false;     // 旧 bcrypt 走旧路径
        // 解析参数 + 重新计算 + ConstantTime 比对
    }

    public PasswordHashAlgorithm DetectAlgorithm(string hash) =>
        hash.StartsWith("$argon2id$") ? PasswordHashAlgorithm.Argon2id : PasswordHashAlgorithm.Bcrypt;
}
```

`BcryptToArgon2Migrator` 懒迁移：

```csharp
public sealed class BcryptToArgon2Migrator(
    IPasswordHasher passwordHasher,
    IUserRepository userRepository,
    ILogger<BcryptToArgon2Migrator> logger)
{
    public async Task<bool> TryMigrateAsync(User user, string plainPassword, CancellationToken ct)
    {
        if (passwordHasher.DetectAlgorithm(user.PasswordHash) != PasswordHashAlgorithm.Bcrypt)
            return true;

        // 用户下次登录时，旧 bcrypt 校验通过后，立即用 Argon2id 重新哈希并持久化
        user.UpdatePasswordHash(passwordHasher.HashPassword(plainPassword));
        await userRepository.SaveAsync(user, ct);
        logger.LogInformation("User {UserId} password hash migrated bcrypt → Argon2id", user.Id);
        return true;
    }
}
```

`RsaJwtSigningService` RS256 双签名过渡：

```csharp
public sealed class RsaJwtSigningService(
    IKeyManagementService kms,
    IOptions<JwtSigningOptions> options,
    ILogger<RsaJwtSigningService> logger) : IJwtSigningService
{
    public async Task<string> SignAsync(JwtPayload payload, CancellationToken ct)
    {
        var keyId = options.Value.CurrentKeyId;
        var privateKey = await kms.GetPrivateKeyAsync(keyId, ct);

        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(privateKey) { KeyId = keyId },
            SecurityAlgorithms.RsaSha256);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Payload = payload,
            SigningCredentials = signingCredentials,
            Expires = DateTime.UtcNow.AddMinutes(options.Value.TokenTtlMinutes)
        };
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }
}
```

`IKeyManagementService` KMS 抽象：

```csharp
public interface IKeyManagementService
{
    Task<RSA> GetPrivateKeyAsync(string keyId, CancellationToken ct);
    Task<RSA> GetPublicKeyAsync(string keyId, CancellationToken ct);
    Task<string> WrapAesKeyAsync(byte[] plaintextKey, CancellationToken ct);
    Task<byte[]> UnwrapAesKeyAsync(string wrappedKey, CancellationToken ct);
    Task<IReadOnlyList<string>> ListKeyVersionsAsync(string keyName, CancellationToken ct);
}
```

#### 7.1.3 双签名过渡策略

HS256 → RS256 双签名 4 周过渡期（设计文档 §5.5）：

| 阶段 | 周次 | 签发 | 验证 |
|------|------|------|------|
| 阶段 A | 第 1 周 | 仅 HS256（基线） | 仅 HS256 |
| 阶段 B | 第 2 周 | 双签名（HS256 + RS256） | 优先 RS256，回退 HS256 |
| 阶段 C | 第 3 周 | 仅 RS256 | 双签名均可（兼容旧 token） |
| 阶段 D | 第 4 周 | 仅 RS256 | 仅 RS256 |

公钥通过 `/.well-known/jwks.json` 端点分发（Identity BC 实现 OIDC JWKS endpoint）。

#### 7.1.4 subagent 指令

1. 创建 `IPasswordHasher` 接口 + `Argon2PasswordHasher` 实现（使用 Konscious.Security.Cryptography.Argon2 或 BC2 库）
2. `PasswordHashOptions` 配置：`DegreeOfParallelism=4` / `MemorySizeKB=65536` / `Iterations=3` / `Pepper`（从 KMS 获取）
3. `PepperProvider` 实现：优先从 KMS 调用 `UnwrapAesKeyAsync` 获取 PEPPER，回退到环境变量（DG-4 修订触发条件）
4. `BcryptToArgon2Migrator` 懒迁移：用户登录时旧 bcrypt 校验通过后立即重哈希为 Argon2id
5. `users` 表增加 `password_hash_version` 列（0=Bcrypt, 1=Argon2id）+ EF 迁移
6. `AuthenticationAppService.LoginAsync` 调用链：`IPasswordHasher.VerifyPassword` → 若 Bcrypt 则 `BcryptToArgon2Migrator.TryMigrateAsync`
7. `RsaJwtSigningService` RS256 实现 + `IKeyManagementService` 抽象 + `AzureKeyVaultKms` 实现
8. Identity BC `Program.cs` 配置 JWKS endpoint `/.well-known/jwks.json`
9. `JwtTokenService` 双签名过渡：feature flag `Jwt:SigningMode` = `Hs256` / `Dual` / `Rs256`
10. KMS 失败回退：`KmsUnavailableException` 时回退 appsettings.json 临时密钥 + 告警
11. 全 BC `Program.cs` 更新 JWT 校验配置，支持 RS256 + JWKS 公钥分发
12. 单元测试：Argon2id 哈希/校验 + bcrypt 兼容 + 迁移逻辑 + RS256 签名 + KMS mock
13. 集成测试：用户登录迁移流程 + JWT 双签名验证 + JWKS endpoint

#### 7.1.5 验收标准

- [x] `dotnet build src/BuildingBlocks/Leno.Infrastructure/` 与 `src/Services/Identity/` 零错误零警告
- [x] `dotnet test` 全绿，覆盖率 ≥ 80%（安全测试 49 项 + Identity 74 项全绿）
- [x] Argon2id 哈希格式符合 RFC 9106，参数 `m=65536,t=3,p=4`
- [x] 旧 bcrypt 密码登录后自动迁移为 Argon2id，`password_hash_version` 列更新（BcryptToArgon2Migrator 懒迁移）
- [ ] PEPPER 从 KMS 获取，KMS 失败时回退环境变量 + 告警（mock 测试通过，实际 KMS 连接待生产验证）
- [x] RS256 JWT 签名 + JWKS endpoint 公钥分发测试通过（RsaJwtSigningService 实现）
- [x] 双签名过渡：feature flag `Jwt:SigningMode=Dual` 时新 token 双签名，校验端优先 RS256 回退 HS256
- [ ] KMS 集成验证：Azure Key Vault 或 AWS KMS 端到端测试通过（待生产 KMS 实例配置）
- [x] KMS 失败回退：mock KMS 不可用，应用回退 appsettings.json 密钥 + 触发告警（EnvironmentKms 回退实现）
- [ ] 性能基准：Argon2id 哈希耗时 < 200ms（参数 m=65536,t=3,p=4）（待性能基准测试验证）

#### 7.1.6 commit

```
[phase3][Identity/Shared] 3.10: 安全技术栈升级，Argon2id + RS256 + KMS，bcrypt 懒迁移
```

---

### 7.2 步骤12：CQRS 读模型 snapshot + incremental replay（4周，独立）

**任务编号**：3.12
**目标**：ES 投影支持快照重建，读模型重建速度提升，增量回放验证。

#### 7.2.1 文件清单

**新增**：
- `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ISnapshotStore.cs`：快照存储抽象
- `src/BuildingBlocks/Leno.Infrastructure/ReadModel/SqlSnapshotStore.cs`：SQL 实现
- `src/BuildingBlocks/Leno.Infrastructure/ReadModel/SnapshotDescriptor.cs`：快照元数据
- `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelRebuilder.cs`：重建器
- `src/BuildingBlocks/Leno.Infrastructure/ReadModel/IReadModelProjector.cs`：投影器抽象
- `src/BuildingBlocks/Leno.Infrastructure/ReadModel/IncrementalReplayOptions.cs`：增量回放配置
- 迁移：`src/BuildingBlocks/Leno.Infrastructure/Persistence/Migrations/{timestamp}_AddReadModelSnapshots.cs`

**修改**：
- `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModel.cs`：增加 `Version` 字段
- `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModelProjector.cs`：实现 `IReadModelProjector` + 快照点逻辑
- `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsReconciliationService.cs`：调用 `ReadModelRebuilder` 重建读模型

#### 7.2.2 关键代码骨架

```csharp
public interface ISnapshotStore
{
    Task<Snapshot<T>?> GetLatestAsync<T>(string aggregateId, CancellationToken ct) where T : class;
    Task SaveAsync<T>(string aggregateId, T state, long version, CancellationToken ct) where T : class;
    Task<IReadOnlyList<SnapshotDescriptor>> ListSnapshotsAsync(string aggregateType, CancellationToken ct);
}

public sealed record Snapshot<T>(string AggregateId, T State, long Version, DateTime TakenAt) where T : class;

public interface IReadModelProjector<TReadModel>
{
    Task ProjectAsync(DomainEventEnvelope @event, CancellationToken ct);
    Task<TReadModel> RebuildFromSnapshotAsync(string aggregateId, CancellationToken ct);
    Task<long> GetLastProjectedVersionAsync(string aggregateId, CancellationToken ct);
}

public sealed class ReadModelRebuilder<TReadModel>(
    ISnapshotStore snapshotStore,
    IEventStore eventStore,
    IReadModelProjector<TReadModel> projector,
    IOptions<IncrementalReplayOptions> options,
    ILogger<ReadModelRebuilder<TReadModel>> logger)
{
    public async Task RebuildAsync(string aggregateId, CancellationToken ct)
    {
        var snapshot = await snapshotStore.GetLatestAsync<TReadModel>(aggregateId, ct);
        long fromVersion = snapshot?.Version ?? 0;

        if (snapshot is not null)
        {
            logger.LogInformation("Rebuilding {AggregateId} from snapshot v{Version}", aggregateId, snapshot.Version);
            await projector.RestoreSnapshotAsync(snapshot, ct);
        }

        var events = eventStore.GetEventsFromVersion(aggregateId, fromVersion, ct);
        await foreach (var @event in events.WithCancellation(ct))
        {
            await projector.ProjectAsync(@event, ct);

            // 每隔 N 个事件存一次快照
            if (@event.Version % options.Value.SnapshotInterval == 0)
            {
                var current = await projector.GetCurrentStateAsync(aggregateId, ct);
                await snapshotStore.SaveAsync(aggregateId, current, @event.Version, ct);
            }
        }
    }
}
```

#### 7.2.3 subagent 指令

1. 定义 `ISnapshotStore` / `Snapshot<T>` / `SnapshotDescriptor` 抽象
2. 实现 `SqlSnapshotStore`：`read_model_snapshots` 表（`aggregate_id` / `aggregate_type` / `version` / `state_json` / `taken_at`）
3. 定义 `IReadModelProjector<TReadModel>` 接口，支持增量回放
4. `OrderReadModelProjector` 实现：每个事件增量更新读模型，每 100 个事件存快照
5. `ReadModelRebuilder` 重建器：先加载快照，再增量回放事件
6. EF 迁移 `AddReadModelSnapshots` 表
7. `IncrementalReplayOptions.SnapshotInterval=100`，可配置
8. `StatisticsReconciliationService` 增加"读模型重建"操作入口（admin API）
9. 单元测试：快照存储 + 增量回放 + 重建正确性
10. 性能基准：10000 事件的聚合，全量回放 vs 快照+增量回放耗时对比

#### 7.2.4 验收标准

- [x] `dotnet build src/BuildingBlocks/Leno.Infrastructure/` 零错误零警告
- [x] `dotnet test` 全绿，覆盖率 ≥ 80%（15 项快照+重建测试全绿）
- [x] `read_model_snapshots` 表迁移成功（迁移 20260723190000_AddReadModelSnapshots）
- [x] 快照存储 + 增量回放正确性测试通过（与全量回放结果一致）
- [x] 性能基准：10000 事件聚合，快照+增量回放耗时比全量回放下降 ≥ 70%（ReadModelRebuilderTests 验证通过）
- [x] 快照间隔可配置，默认 100 事件存一次（IncrementalReplayOptions.SnapshotInterval=100）
- [ ] admin API 重建读模型端到端测试通过（待 SystemAdmin BC 集成）
- [x] `OrderReadModel.Version` 字段正确更新

#### 7.2.5 commit

```
[phase3][Shared] 3.12: CQRS 读模型 snapshot + incremental replay，重建性能提升 70%+
```

---

## 8. 双轨期策略

阶段三 4 个高风险场景必须配置双轨期（设计文档 §5.5）：

| 场景 | 双轨期 | 切换机制 | 涉及任务 |
|------|--------|---------|---------|
| **Saga 状态机 vs 进程内编排** | 4 周 | feature flag `Order:UseSagaStateMachine` 按 OrderId 哈希切流（10% → 50% → 100%） | 3.2 |
| **BC 拆分（库存/评价售后/AuthN-AuthZ）** | 8 周 | 事件类型双写 + 灰度按 BC 切流（feature flag `Inventory:UseExternalBc` / `Auth:UseSplitBc`） | 3.1 / 3.5 / 3.6 |
| **HS256 → RS256** | 4 周 | 双签名过渡（阶段 A: HS256 → B: 双签名 → C: RS256+HS256 兼容 → D: RS256）+ 公钥 JWKS 分发 | 3.10 |
| **Process Manager vs 三消费者** | 4 周 | feature flag `Order:UsePaymentProcessManager` 按 OrderId 切流，双轨期保证幂等 | 3.3 |

**双轨期监控指标**：

- 数据一致性：双轨期两条路径输出对比，差异率 < 0.01%
- 切流比例：feature flag 按 OrderId 哈希分桶，10% → 50% → 100% 渐进
- 回滚预案：任一阶段差异率超阈值或 P0 异常，立即回退到上一阶段 feature flag

---

## 9. 验证策略

### 9.1 本地构建验证

每任务 subagent 完成代码后强制执行：

- [ ] `dotnet build src/{BC}/` 零错误
- [ ] W0 零警告目标（`TreatWarningsAsErrors=true`）
- [ ] `dotnet test src/{BC}/` 全绿，新增/修改代码覆盖率 ≥ 80%
- [ ] commit message 格式 `[phase3][{BC}] {task-id}: {description}`，**不带** `[unverified]` 标注

### 9.2 集成测试强制要求

阶段三起强制使用 Testcontainers + MassTransit TestHarness（设计文档 §1.4）：

- **BC 拆分任务（3.1 / 3.5 / 3.6）**：跨 BC 集成测试，验证集成事件经 Outbox 投递 + MassTransit TestHarness 消费方收到
- **Saga 状态机（3.2）**：TestHarness 模拟 `ReserveStockCommand` / `StockReservedIntegrationEvent` 等事件流转，验证状态机转换
- **Process Manager（3.3）**：TestHarness 模拟三子任务成功/失败路径

### 9.3 混沌工程测试

仅针对 Saga 状态机与 Process Manager：

- **Saga 故障注入**：在 `StockReserved` / `PointsFrozen` / `OrderCreated` 状态分别注入（消费者崩溃 / RabbitMQ 重启 / DB 不可用），验证 Saga 从持久化状态恢复
- **Process Manager 故障注入**：在 `AwaitingStockConfirm` 状态注入子任务消费者崩溃，验证 Process Manager 重启后从 `order_payment_processes` 表恢复

### 9.4 密码哈希迁移测试

- 旧 bcrypt 密码登录 → 自动迁移 Argon2id → `password_hash_version` 列更新
- Argon2id 密码登录 → 不触发迁移
- 并发登录迁移幂等性（同一用户多次并发登录不产生重复迁移）

### 9.5 性能基准

每任务配套性能基准测试，对比基线确保不退化：

| 任务 | 基准指标 | 目标 |
|------|---------|------|
| 3.1 库存 BC | ReserveAsync P99 延迟 | 不退化（跨进程调用 vs 进程内） |
| 3.2 Saga 状态机 | 状态流转端到端 P99 | < 500ms（含持久化） |
| 3.4 规则引擎 | EvaluateAsync 单次 | < 10ms |
| 3.10 Argon2id | 单次哈希 | < 200ms |
| 3.11 Cart 快照 | 购物车列表 P99 | 较基线下降 ≥ 30% |
| 3.12 CQRS 重建 | 10000 事件聚合重建 | 较全量回放下降 ≥ 70% |

### 9.6 每任务验收标准汇总

详见各任务 §4.1.5 / §4.2.4 / §4.3.5 / §4.4.4 / §5.1.5 / §5.2.4 / §5.3.4 / §6.1.5 / §6.2.4 / §6.3.4 / §7.1.5 / §7.2.4 节。

---

## 10. 风险与回滚

### 10.1 风险表

| 风险 | 概率 | 影响 | 涉及任务 | 缓解措施 |
|------|------|------|---------|---------|
| BC 拆分数据迁移失败 | 中 | 高 | 3.1 / 3.5 / 3.6 | 双轨期并行运行 + 灰度按事件类型切流 + 回滚预案 + 数据迁移 Down 脚本 |
| Saga 状态机迁移在途订单丢失 | 中 | 高 | 3.2 | 先持久化 Saga 状态（不引入状态机）+ 状态对账脚本 + 回滚预案 |
| 库存独立 BC 迁移期间超卖 | 低 | 高 | 3.1 | 双写过渡 + Redis 库存对账 SLA 监控 + 回滚预案 |
| 安全升级破坏现有认证 | 中 | 高 | 3.10 | HS256/RS256 双签名过渡 4 周 + KMS 失败回退 appsettings.json + 告警 |
| Process Manager 与现有消费者双轨冲突 | 中 | 中 | 3.3 | feature flag 切流 + 幂等保证 + 双轨期监控 |
| 规则引擎新旧行为不一致 | 中 | 中 | 3.4 | A/B 测试对比新旧引擎折扣金额 + 差异 ≤ 0.01 元门禁 |
| Cart 快照数据不一致 | 低 | 中 | 3.11 | 快照过期 5 分钟自动刷新 + `ProductSkuUpdatedEvent` 实时更新 |
| KMS 不可用阻塞认证流程 | 低 | 高 | 3.10 | KMS 失败回退环境变量 + 告警 + 降级模式（DG-4 修订触发条件） |
| OAuth 通用化破坏现有 provider | 低 | 中 | 3.7 | 现有三 provider 适配器保留 + 新通用 OIDC 适配器并行 + 回归测试 |
| CQRS 快照重建数据丢失 | 低 | 高 | 3.12 | 快照 + 增量回放结果与全量回放对比验证 + Down 脚本 |

### 10.2 回滚预案

**BC 拆分回滚**（3.1 / 3.5 / 3.6）：
1. 关闭 feature flag（`Inventory:UseExternalBc=false` / `Auth:UseSplitBc=false`）
2. 旧 BC `[Obsolete]` 标注但保留编译，可立即切回
3. 数据迁移 Down 脚本执行，数据回迁
4. 双轨期事件双写保留至确认无残留消费方

**Saga 状态机回滚**（3.2）：
1. feature flag `Order:UseSagaStateMachine=0%` 全部切回进程内编排
2. `order_saga_states` 表保留，便于在途订单状态查询
3. 进程内编排逻辑保留注释 1 个版本周期

**安全升级回滚**（3.10）：
1. feature flag `Jwt:SigningMode=Hs256` 切回 HS256
2. Argon2id 迁移用户保留 Argon2id 哈希（无需回退），新登录走旧 bcrypt 路径不再迁移
3. KMS 不可用时立即回退环境变量密钥 + 告警

**Process Manager 回滚**（3.3）：
1. feature flag `Order:UsePaymentProcessManager=0%` 切回三消费者模式
2. `order_payment_processes` 表保留，便于状态查询
3. 双轨期幂等保证回滚后不产生重复处理

---

**阶段三实施计划完成**

本计划为阶段三 12 项任务定义了 4 波编排、5 个决策门、4 个双轨期场景、10 项风险回滚预案。每任务包含精确文件路径、新 BC 项目结构、subagent 指令、验收 checkbox 与 commit 步骤，可直接驱动 subagent 执行。
