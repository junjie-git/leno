# 阶段一：P0 阻塞修复 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**日期**：2026-07-23
**输入**：[00-architecture-upgrade-plan.md](./00-architecture-upgrade-plan.md) 第六章 6.1 节
**前置依赖**：无
**目标**：解除部署阻塞，恢复全域一致性。健康度 8.3 → 8.5
**架构**：4 项 P0 修复，Wave 1a（3 并行）+ Wave 1b（1 串行，依赖 1a）
**Tech Stack**：.NET 10, EF Core, SQL Server, MassTransit, MediatR

---

## 1. 范围与约束

### 1.1 实施范围

母方案 6.1 节定义的 4 项 P0 阻塞修复任务，外加 3 项部分修复遗留项的合并处理：

| Task | 问题 ID | 修改范围 | 影响 BC | 波次 |
|------|---------|---------|---------|------|
| 1 | NEW-P0-1 | Order 表 DropColumn `version` shadow 列 + BaseDbContext 跳过显式 RowVersion 实体 | Order / Shared | 1a |
| 2 | NEW-P0-2 | CartUnitOfWork `SaveChangesAsync` 改委托 `SaveChangesWithOutboxAsync` | Cart | 1a |
| 3 | NEW-P0-3 | StockReservationCompensation 实体增 `OperationType` 字段 + BackgroundService 按类型分发 + migration | Order | 1b（依赖 Task 1） |
| 4 | NEW-P0-4 | Notification `MarkAllAsReadAsync` 改走聚合根 + 新增 `NotificationReadDomainEvent` + 应用层切 `SaveEntitiesAsync` | Notification | 1a |

**任务总数**：4 项 P0（其中 Task 4 同时关闭 P1-5，阶段二步骤 10 跳过）
**预估周期**：1 周
**资源需求**：2 名后端工程师，1 名 DBA 评审迁移脚本

### 1.2 关键约束

- **每任务独立 commit**：message 格式 `[phase1][{BC}] {task-id}: {description}`，严禁一次提交多项
- **本地构建验证**：subagent 写代码后必须执行 `dotnet build` 与 `dotnet test`，失败则修复后提交（非 `[unverified]` 模式）
- **代码完整性强制契约**：禁止占位符、TODO、空实现、`throw new NotImplementedException()`、截断输出，每函数完整实现
- **迁移脚本可逆**：所有 EF Core 迁移必须配套 `Down` 方法，支持回滚
- **向后兼容**：公共接口只增不删；`SaveChangesAsync` 标注 `[Obsolete]` 委托而非删除
- **DBA 评审门禁**：Task 1 / Task 3 的迁移脚本（Up + Down）须由 DBA 评审后方可合并

### 1.3 前置依赖核验清单

阶段一无前置阶段依赖，但需确认以下基线状态：

- [ ] 当前分支为 `feat-architecture-upgrade-plan-iKDayh`，工作区干净（`git status` 无未跟踪文件）
- [ ] 母方案 1.3 节 4 项 NEW-P0 问题在当前源码中确实存在（按本文档第 3、4 章给出的文件路径核验）
- [ ] 本地已安装 .NET SDK 10.0.301+，执行 `dotnet --version` 通过
- [ ] SQL Server LocalDB 或 Developer 实例可用，用于 `dotnet ef migrations` 验证
- [ ] 母方案 `00-architecture-upgrade-plan.md` 已通过架构评审，本计划是其授权下的细化执行文件

---

## 2. 总体架构

### 2.1 波次编排图

```
Wave 1a（3 并行 subagent）              Wave 1b（1 串行 subagent，依赖 Wave 1a 完成）
┌──────────────┬──────────────┬──────────────┐  ┌──────────────────────────┐
│ Task 1       │ Task 2       │ Task 4       │  │ Task 3                   │
│ NEW-P0-1     │ NEW-P0-2     │ NEW-P0-4     │  │ NEW-P0-3                 │
│ Order BC     │ Cart BC      │ Notification │  │ Order BC                 │
│ OrderConfig  │ CartUnitOf   │ BC           │  │ StockCompensation 聚合    │
│ + BaseDbCtx  │ Work 委托    │ Repository   │  │ + BackgroundService 分发 │
│ + DropColumn │ Outbox       │ + 新领域事件 │  │ + AddColumn migration    │
│ migration    │              │ + AppService │  │                          │
└──────────────┴──────────────┴──────────────┘  └──────────────────────────┘
        ↓ git commit                ↓ git commit       ↓ git commit
        ─────────────────────────────────────────────→ Wave 1b 启动
```

### 2.2 subagent 编排

- **subagent 总数**：4 个 `general_purpose_task` subagent
- **波数**：2 波（Wave 1a 3 并行 + Wave 1b 1 串行）
- **并行度限制**：Wave 1a 同时 3 个 subagent（留 1 slot 给主 agent 操作）
- **git 冲突避免**：subagent 各自 `git add` 自己 BC 目录的文件，主 agent 在 Wave 1a 完成后统一 `git push`，再启动 Wave 1b

### 2.3 BC 目录互斥矩阵

| Task | 修改目录 | 互斥 Task |
|------|---------|----------|
| Task 1 | `src/Services/Order/Leno.Order.Infrastructure/Configurations/` + `src/BuildingBlocks/Leno.Infrastructure/Persistence/` + `src/Services/Order/Leno.Order.Infrastructure/Migrations/` | Task 3（同 BC 迁移目录） |
| Task 2 | `src/Services/Cart/Leno.Cart.Infrastructure/` | 无 |
| Task 3 | `src/Services/Order/Leno.Order.Domain/Aggregates/` + `src/Services/Order/Leno.Order.Infrastructure/Services/` + `src/Services/Order/Leno.Order.Infrastructure/Configurations/` + `src/Services/Order/Leno.Order.Infrastructure/Migrations/` | Task 1（同 BC 迁移目录） |
| Task 4 | `src/Services/Notification/Leno.Notification.Domain/` + `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/` + `src/Services/Notification/Leno.Notification.Application/Services/` | 无 |

**冲突规避决策**：Task 1 与 Task 3 均在 `src/Services/Order/Leno.Order.Infrastructure/Migrations/` 产生新迁移文件，且均修改 Order BC 模型快照。为避免 `OrderDbContextModelSnapshot.cs` 合并冲突与迁移时间戳冲突，Task 3 串行等待 Task 1 提交后启动（Wave 1b）。

---

## 3. Wave 1a 详细编排（3 并行 subagent）

### 3.1 Task 1 — NEW-P0-1：Order 双 rowversion 列修复

**问题**：Order 表存在双 rowversion 列——`BaseDbContext.OnModelCreating` 在 L46-55 为所有 `Entity` 子类注入 shadow property `version`（IsRowVersion），而 `OrderConfiguration` 在 L50 又显式声明 `row_version` 列为 IsRowVersion。SQL Server 单表仅允许一个 rowversion 列，导致迁移失败、生产部署阻塞。

**影响 BC**：Order / Shared（BuildingBlocks）
**修改文件**（3 处）：

| # | 文件绝对路径 | 行号 | 修改类型 |
|---|------------|-----|---------|
| 1.1 | `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs` | L43-55 | 修改逻辑：跳过已显式配置 rowversion 的实体 |
| 1.2 | `src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs` | L50 | 保留不动（已正确配置显式 RowVersion） |
| 1.3 | `src/Services/Order/Leno.Order.Infrastructure/Migrations/` | 新增 | 新增迁移 `DropOrderVersionShadowColumn` |

#### 3.1.1 subagent 指令要点

**步骤 1：修改 `BaseDbContext.OnModelCreating` 跳过显式 rowversion 实体**

读取 `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs`，将 L43-55 的循环改为：在为每个 `Entity` 子类添加 `Version` shadow property 之前，先检查该实体是否已存在 `IsRowVersion() = true` 的属性（即显式配置的乐观锁）。若已存在则跳过，避免双 rowversion 列。

修改后的代码片段（替换 L43-55）：

```csharp
// 统一配置乐观锁 shadow property（避免领域层 Entity 携带持久化细节）
// 所有继承 Entity 的实体自动获得名为 "Version" 的 rowversion shadow property
// 跳过 owned type（由 OwnsOne/OwnsMany 持有的实体）以避免 "cannot be configured as non-owned" 异常
// 跳过已显式配置 rowversion 的实体（如 Order 聚合显式声明 RowVersion 列），避免 SQL Server 双 rowversion 列冲突
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
{
    if (typeof(Entity).IsAssignableFrom(entityType.ClrType) && !entityType.IsOwned())
    {
        // 检查实体是否已显式配置 rowversion 并发令牌（如 OrderConfiguration 显式声明 RowVersion）
        var hasExplicitRowVersion = entityType.GetProperties()
            .Any(p => p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate);

        if (!hasExplicitRowVersion)
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property<byte[]>("Version")
                .HasColumnName("version")
                .IsRowVersion();
        }
    }
}
```

**注意**：需在文件顶部确认已 `using Microsoft.EntityFrameworkCore.Metadata;`（提供 `ValueGenerated` 枚举）。若未引用则添加。

**步骤 2：新增迁移 `DropOrderVersionShadowColumn`**

在 `src/Services/Order/Leno.Order.Infrastructure/` 目录下执行迁移命令，生成删除 `orders.version` 列的迁移：

```bash
cd src/Services/Order/Leno.Order.Infrastructure
dotnet ef migrations add DropOrderVersionShadowColumn --context OrderDbContext
```

迁移生成后，**人工核验** `Migrations/<timestamp>_DropOrderVersionShadowColumn.cs` 的 `Up` 方法仅包含以下操作（无其他误删）：

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 删除 BaseDbContext 之前误注入的 shadow rowversion 列，保留 OrderConfiguration 显式声明的 row_version 列
    migrationBuilder.DropColumn(
        name: "version",
        table: "orders");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // 回滚：重新添加 shadow version 列（仅用于紧急回退，会重新引入双 rowversion 冲突，不建议生产回滚）
    migrationBuilder.AddColumn<byte[]>(
        name: "version",
        table: "orders",
        type: "rowversion",
        rowVersion: true,
        nullable: false);
}
```

**若 `dotnet ef migrations add` 生成的 Up 包含其他列变更（如重置 row_version），subagent 必须手工编辑迁移文件，确保仅 DropColumn `version`**。

**步骤 3：核验 `OrderConfiguration.cs` L50 不变**

读取 `src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs` 确认 L50 仍为：

```csharp
builder.Property(o => o.RowVersion).HasColumnName("row_version").IsRowVersion();
```

**不修改该行**——显式 RowVersion 是聚合层的并发控制，应予保留。

#### 3.1.2 验证步骤

- [ ] `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` 零错误零警告
- [ ] `dotnet build src/Services/Order/Leno.Order.Infrastructure/Leno.Order.Infrastructure.csproj` 零错误零警告
- [ ] `dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj --filter "OrderConfigurationTests"` 全绿（验证 RowVersion 仍为并发令牌）
- [ ] `dotnet ef migrations script --context OrderDbContext --output script.sql -p src/Services/Order/Leno.Order.Infrastructure -i` 生成的 SQL 脚本仅包含 `ALTER TABLE orders DROP COLUMN version;`
- [ ] 人工核验 `OrderDbContextModelSnapshot.cs` 中 `orders` 表仅有一个 rowversion 属性（`row_version`），`version` shadow property 已移除
- [ ] 全量 `dotnet build Leno.sln` 零错误（确认未破坏其他 BC 对 BaseDbContext 的依赖）

#### 3.1.3 提交

```bash
cd <repo-root>
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs
git add src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/<timestamp>_DropOrderVersionShadowColumn.cs
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/<timestamp>_DropOrderVersionShadowColumn.Designer.cs
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/OrderDbContextModelSnapshot.cs
git commit -m "[phase1][Order] NEW-P0-1: skip shadow version column for entities with explicit RowVersion to fix dual rowversion migration failure"
```

---

### 3.2 Task 2 — NEW-P0-2：CartUnitOfWork 旁路 Outbox 修复

**问题**：`CartUnitOfWork.SaveChangesAsync` 在 `src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs#L35-36` 直接调 `_context.SaveChangesAsync(ct)`，绕过 Outbox。Cart 聚合产生的领域事件（`SkuAddedToCartEvent` / `SkuRemovedFromCartEvent`）未写入 `OutboxMessage` 表，下游 BC（库存索引、价格快照）收不到 Cart 事件。

**影响 BC**：Cart
**修改文件**（1 处）：

| # | 文件绝对路径 | 行号 | 修改类型 |
|---|------------|-----|---------|
| 2.1 | `src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs` | L34-36 | 修改 `SaveChangesAsync` 委托 `SaveChangesWithOutboxAsync` + 加 `[Obsolete]` |

#### 3.2.1 subagent 指令要点

**步骤 1：修改 `CartUnitOfWork.SaveChangesAsync` 委托 Outbox**

读取 `src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs`，将 L34-36 的实现：

```csharp
/// <inheritdoc />
public Task<int> SaveChangesAsync(CancellationToken ct = default)
    => _context.SaveChangesAsync(ct);
```

替换为（与 `EfCoreUnitOfWork<TDbContext>.SaveChangesAsync` L56-58 风格完全一致）：

```csharp
/// <inheritdoc />
/// <summary>
/// 已废弃：使用 <see cref="SaveEntitiesAsync"/> 替代，确保领域事件经 Outbox 持久化。
/// 此方法保留仅为向后兼容，内部委托给 <see cref="OutboxDbContextExtensions.SaveChangesWithOutboxAsync"/>，
/// 不再直接调 <c>DbContext.SaveChangesAsync</c> 旁路 Outbox（避免 Cart 领域事件丢失或双发）。
/// </summary>
[Obsolete("Use SaveEntitiesAsync to ensure domain events are persisted to outbox. 此方法旁路 Outbox 会导致事件丢失或双发。")]
public Task<int> SaveChangesAsync(CancellationToken ct = default)
    => _context.SaveChangesWithOutboxAsync(_mapper, ct);
```

**说明**：
- `_mapper` 字段已在构造函数注入（L18、L30），无需新增依赖
- `SaveChangesWithOutboxAsync` 是 `OutboxDbContextExtensions` 提供的扩展方法，已在 L58 的 `SaveEntitiesAsync` 中使用，无需新增 using
- `[Obsolete]` 标注与 `EfCoreUnitOfWork<TDbContext>` L56 保持一致，便于编译期发现调用方并迁移至 `SaveEntitiesAsync`

**步骤 2：核验 Cart BC 所有调用方**

执行 `grep -rn "SaveChangesAsync" src/Services/Cart/` 确认 Cart BC 内的 AppService / Consumer 调用模式。若有调用 `IUnitOfWork.SaveChangesAsync` 的代码，标注为后续清理项（阶段二 P1 修复范围），本任务仅修复 UnitOfWork 自身，不波及调用方，保证零行为变更门禁（`SaveChangesAsync` 现在委托 Outbox，行为等价于 `SaveEntitiesAsync`）。

#### 3.2.2 验证步骤

- [ ] `dotnet build src/Services/Cart/Leno.Cart.Infrastructure/Leno.Cart.Infrastructure.csproj` 零错误零警告（`[Obsolete]` 不应产生警告，因为调用方暂未迁移）
- [ ] `dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj` 全绿
- [ ] 新增/修改集成测试：在 `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartSkuIndexIntegrationTests.cs` 中验证 `SaveChangesAsync` 调用后 `OutboxMessage` 表存在对应 `SkuAddedToCartEvent` 集成事件记录（参考 `CartIntegrationEventMapper` 映射规则）
- [ ] 人工核验 `CartUnitOfWork.SaveChangesAsync` 与 `SaveEntitiesAsync` 行为等价（两者均委托 `_context.SaveChangesWithOutboxAsync(_mapper, ct)`），`SaveEntitiesAsync` 额外返回 `true` 并先分发 SKU 反向索引事件，不影响 Outbox 行为

#### 3.2.3 提交

```bash
cd <repo-root>
git add src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs
git add src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartSkuIndexIntegrationTests.cs
git commit -m "[phase1][Cart] NEW-P0-2: delegate CartUnitOfWork.SaveChangesAsync to SaveChangesWithOutboxAsync to prevent domain event loss"
```

---

### 3.3 Task 4 — NEW-P0-4：Notification MarkAllAsReadAsync 绕聚合根修复

**问题**：`EfCoreNotificationRecordRepository.MarkAllAsReadAsync` 在 `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L96-101` 使用 `ExecuteUpdateAsync` 直接生成 `UPDATE` SQL，绕过聚合根。后果：(1) 不触发 `NotificationReadDomainEvent`；(2) 不写审计字段 `UpdatedAt` / `UpdatedBy`（`AuditableEntityInterceptor` 仅对 ChangeTracker 跟踪的实体生效）；(3) 违反 DDD 聚合根封装原则。

**影响 BC**：Notification
**修改文件**（4 处）：

| # | 文件绝对路径 | 行号 | 修改类型 |
|---|------------|-----|---------|
| 4.1 | `src/Services/Notification/Leno.Notification.Domain/Events/NotificationReadDomainEvent.cs` | 新增 | 新增领域事件类 |
| 4.2 | `src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs` | L335-349 | `MarkAsRead()` 增加 `AddDomainEvent` |
| 4.3 | `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs` | L96-101 | `MarkAllAsReadAsync` 改为加载 + 逐个 `MarkAsRead` |
| 4.4 | `src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs` | L74-78 | `SaveChangesAsync` → `SaveEntitiesAsync` |

#### 3.3.1 subagent 指令要点

**步骤 1：新增 `NotificationReadDomainEvent` 领域事件**

在 `src/Services/Notification/Leno.Notification.Domain/` 下创建 `Events/NotificationReadDomainEvent.cs`（Domain 目录当前无 Events 子目录，需新建）：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Events;

/// <summary>
/// 通知记录被标记已读领域事件。
/// 由 <see cref="Aggregates.NotificationRecord.MarkAsRead"/> 在状态从未读流转到已读时收集，
/// mapper 可翻译为集成事件供读模型同步、未读数缓存失效、行为分析等消费方使用。
/// 幂等性：已读记录重复调用 <see cref="Aggregates.NotificationRecord.MarkAsRead"/> 不会发布此事件。
/// </summary>
public sealed class NotificationReadDomainEvent : DomainEventBase
{
    public Guid RecordId { get; init; }
    public Guid UserId { get; init; }
    public DateTime ReadAt { get; init; }

    public NotificationReadDomainEvent(Guid recordId, Guid userId, DateTime readAt)
        : base(recordId)
    {
        RecordId = recordId;
        UserId = userId;
        ReadAt = readAt;
    }
}
```

**说明**：参考 `Leno.Order.Domain.Events.OrderDomainEvents.cs` 中 `OrderCreatedDomainEvent` 的风格（继承 `DomainEventBase`、`init` 属性、构造函数调基类）。`DomainEventBase` 来自 `Leno.SharedKernel.Abstractions`，已通过 `Leno.Notification.Domain.csproj` 间接引用。

**步骤 2：修改 `NotificationRecord.MarkAsRead()` 发布领域事件**

读取 `src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs`，将 L335-349 的 `MarkAsRead()` 方法：

```csharp
public void MarkAsRead()
{
    if (Channel != NotificationChannel.InApp)
    {
        throw new NotificationDomainException("仅站内信可标记已读", "NOTIFICATION_READ_CHANNEL_INVALID");
    }

    // P2-43：幂等保护，已读记录重复调用直接返回，避免触发不必要的更新与 SaveChanges。
    if (IsRead)
    {
        return;
    }

    IsRead = true;
}
```

修改为（在 `IsRead = true;` 之后追加 `AddDomainEvent`）：

```csharp
public void MarkAsRead()
{
    if (Channel != NotificationChannel.InApp)
    {
        throw new NotificationDomainException("仅站内信可标记已读", "NOTIFICATION_READ_CHANNEL_INVALID");
    }

    // P2-43：幂等保护，已读记录重复调用直接返回，避免触发不必要的更新与 SaveChanges。
    if (IsRead)
    {
        return;
    }

    IsRead = true;

    // NEW-P0-4：发布已读领域事件，供 Outbox 同事务发布，下游消费方（未读数缓存失效、读模型同步）可订阅
    AddDomainEvent(new NotificationReadDomainEvent(
        recordId: Id,
        userId: UserId,
        readAt: DateTime.UtcNow));
}
```

**说明**：`AddDomainEvent` 是 `AggregateRoot` 基类提供的方法，已通过 `using Leno.SharedKernel.Abstractions;`（L3 已存在）。需在文件顶部追加 `using Leno.Notification.Domain.Events;`。

**步骤 3：修改 `EfCoreNotificationRecordRepository.MarkAllAsReadAsync` 走聚合根**

读取 `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs`，将 L96-101 的实现：

```csharp
public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
{
    return await _context.NotificationRecords
        .Where(n => n.UserId == userId && n.Channel == NotificationChannel.InApp && !n.IsRead)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}
```

替换为（加载未读记录到 ChangeTracker，逐个调 `MarkAsRead()` 触发领域事件与审计字段填充）：

```csharp
public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
{
    // NEW-P0-4：禁止使用 ExecuteUpdateAsync 绕聚合根。
    // 加载未读站内信记录到 ChangeTracker，逐个调 MarkAsRead() 触发 NotificationReadDomainEvent，
    // 由 AuditableEntityInterceptor 自动填充 UpdatedAt/UpdatedBy 审计字段。
    var unreadRecords = await _context.NotificationRecords
        .Where(n => n.UserId == userId && n.Channel == NotificationChannel.InApp && !n.IsRead)
        .ToListAsync(ct);

    foreach (var record in unreadRecords)
    {
        record.MarkAsRead();
    }

    return unreadRecords.Count;
}
```

**说明**：
- 不再在仓储内调 `SaveChanges`，持久化由应用层 `IUnitOfWork` 统一负责（与 `MarkAsReadAsync(Guid, List<Guid>)` 在 `NotificationAppService` L50-71 的模式一致）
- `ToListAsync` 已在文件 L41、L59 等处使用，`using Microsoft.EntityFrameworkCore;` 已存在
- 返回值语义：标记已读的记录数（与原 `ExecuteUpdateAsync` 返回受影响行数等价）

**步骤 4：修改 `NotificationAppService.MarkAllAsReadAsync` 切 `SaveEntitiesAsync`**

读取 `src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs`，将 L74-78：

```csharp
public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
{
    await _recordRepository.MarkAllAsReadAsync(userId, ct);
    await _unitOfWork.SaveChangesAsync(ct);
}
```

修改为：

```csharp
public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
{
    await _recordRepository.MarkAllAsReadAsync(userId, ct);
    // NEW-P0-4：切 SaveEntitiesAsync，确保 NotificationReadDomainEvent 经 Outbox 同事务发布
    await _unitOfWork.SaveEntitiesAsync(ct);
}
```

**说明**：`SaveEntitiesAsync` 是 `IUnitOfWork` 接口的标准方法（参考 `EfCoreUnitOfWork<TDbContext>.SaveEntitiesAsync` L61-65），内部委托 `SaveChangesWithOutboxAsync` 收集领域事件并写入 Outbox 表。

#### 3.3.2 验证步骤

- [ ] `dotnet build src/Services/Notification/Leno.Notification.Domain/Leno.Notification.Domain.csproj` 零错误零警告
- [ ] `dotnet build src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj` 零错误零警告
- [ ] `dotnet build src/Services/Notification/Leno.Notification.Application/Leno.Notification.Application.csproj` 零错误零警告
- [ ] `dotnet test src/Services/Notification/Leno.Notification.Domain.Tests/Leno.Notification.Domain.Tests.csproj` 全绿（含 `NotificationRecordTests`）
- [ ] 新增/修改单元测试：在 `src/Services/Notification/Leno.Notification.Domain.Tests/NotificationRecordTests.cs` 中验证 `MarkAsRead()` 调用后 `DomainEvents` 集合包含 `NotificationReadDomainEvent`，且 `RecordId` / `UserId` / `ReadAt` 字段正确
- [ ] 新增/修改单元测试：验证 `MarkAsRead()` 重复调用（已读记录）不再追加领域事件（幂等性）
- [ ] `dotnet test src/Services/Notification/Leno.Notification.Infrastructure.Tests/Leno.Notification.Infrastructure.Tests.csproj` 全绿
- [ ] 人工核验：`MarkAllAsReadAsync` 调用链 → 仓储加载记录 → `MarkAsRead()` 添加事件 → `SaveEntitiesAsync` 经 `OutboxDbContextExtensions.SaveChangesWithOutboxAsync` 写入 `OutboxMessage` 表 → `AuditableEntityInterceptor` 填充 `UpdatedAt` / `UpdatedBy`

#### 3.3.3 提交

```bash
cd <repo-root>
git add src/Services/Notification/Leno.Notification.Domain/Events/NotificationReadDomainEvent.cs
git add src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs
git add src/Services/Notification/Leno.Notification.Domain.Tests/NotificationRecordTests.cs
git add src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs
git add src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs
git commit -m "[phase1][Notification] NEW-P0-4: route MarkAllAsReadAsync through aggregate root to emit domain event and audit fields"
```

---

## 4. Wave 1b 详细编排（1 串行 subagent，依赖 Wave 1a）

### 4.1 启动前置核验

Wave 1b 启动前，主 agent 必须确认：

- [ ] Wave 1a 全部 3 个 subagent 已完成并 `git push`
- [ ] Task 1 的 `DropOrderVersionShadowColumn` 迁移已提交，`OrderDbContextModelSnapshot.cs` 已更新
- [ ] `git pull --rebase` 后工作区无冲突
- [ ] `dotnet build Leno.sln` 零错误（基线绿状态）

### 4.2 Task 3 — NEW-P0-3：StockReservationCompensation OperationType 字段

**问题**：`StockReservationCompensationBackgroundService` 在 `src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationCompensationBackgroundService.cs#L111` 对所有 Pending 补偿记录统一调 `IInventoryRepository.ReleaseAsync`。但 `StockReservationDomainService.ReturnDeductedBatchAsync`（L100-116）在 ForceCancel 已支付订单时调用 `ReturnDeductedAsync` 失败，也通过 `RecordCompensationAsync` 写入补偿表（L113）。补偿表无 `OperationType` 字段区分操作类型，后台任务重试时错误调用 `ReleaseAsync`（释放预占，no-op，因为库存已扣减），导致 deducted 库存永久丢失。

**影响 BC**：Order
**修改文件**（5 处 + 1 新增迁移 + 测试更新）：

| # | 文件绝对路径 | 行号 | 修改类型 |
|---|------------|-----|---------|
| 3.1 | `src/Services/Order/Leno.Order.Domain/Aggregates/StockReservationCompensation.cs` | L16-101 | 增 `CompensationOperationType` 枚举 + `OperationType` 属性 + `Create` 工厂方法增参 |
| 3.2 | `src/Services/Order/Leno.Order.Infrastructure/Configurations/StockReservationCompensationConfiguration.cs` | L20 后 | 映射 `operation_type` 列 |
| 3.3 | `src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationCompensationBackgroundService.cs` | L111 | 按 `OperationType` 分发 `ReleaseAsync` / `ReturnDeductedAsync` |
| 3.4 | `src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs` | L56, L94, L113, L127-137 | `RecordCompensationAsync` 增 `operationType` 参数并传播至 `Create` |
| 3.5 | `src/Services/Order/Leno.Order.Infrastructure/Migrations/` | 新增 | 新增迁移 `AddStockCompensationOperationType` |
| 3.6 | `src/Services/Order/Leno.Order.Infrastructure.Tests/StockReservationCompensationTests.cs` | 多处 | 更新现有测试调用 + 新增 OperationType 分发测试 |

#### 4.2.1 subagent 指令要点

**步骤 1：在 `StockReservationCompensation` 聚合根增加 `OperationType` 字段**

读取 `src/Services/Order/Leno.Order.Domain/Aggregates/StockReservationCompensation.cs`。

**1a. 在文件末尾（L174 `CompensationStatus` 枚举后）追加新枚举**：

```csharp
/// <summary>
/// 库存补偿操作类型，决定后台任务重试时调用的 IInventoryRepository 方法。
/// </summary>
public enum CompensationOperationType
{
    /// <summary>
    /// 释放预占库存：对应 <c>IInventoryRepository.ReleaseAsync</c>。
    /// 用于 Saga 补偿（订单未支付取消）、<c>StockReservationDomainService.ReserveBatchAsync</c> 内部回滚、
    /// <c>ReleaseBatchAsync</c> 失败重试。
    /// </summary>
    Release = 0,

    /// <summary>
    /// 归还已扣减库存：对应 <c>IInventoryRepository.ReturnDeductedAsync</c>。
    /// 用于 ForceCancel 已支付/已发货订单时，<c>ReturnDeductedBatchAsync</c> 失败重试。
    /// 误用 Release 会导致 deducted 库存永久丢失（Release 释放预占，对已扣减库存是 no-op）。
    /// </summary>
    ReturnDeducted = 1
}
```

**1b. 在聚合根类（L16 起）增加 `OperationType` 属性**，在 `MaxRetries` 属性（L42）之后添加：

```csharp
/// <summary>
/// 补偿操作类型，决定后台任务重试时调用 <c>ReleaseAsync</c> 还是 <c>ReturnDeductedAsync</c>。
/// 默认 <see cref="CompensationOperationType.Release"/>（向后兼容历史记录）。
/// </summary>
public CompensationOperationType OperationType { get; private set; }
```

**1c. 修改 `Create` 工厂方法（L63-101）增加 `operationType` 参数**：

将方法签名：

```csharp
public static StockReservationCompensation Create(
    Guid id,
    Guid orderId,
    Guid skuId,
    int quantity,
    int maxRetries = DefaultMaxRetries)
```

改为：

```csharp
public static StockReservationCompensation Create(
    Guid id,
    Guid orderId,
    Guid skuId,
    int quantity,
    CompensationOperationType operationType = CompensationOperationType.Release,
    int maxRetries = DefaultMaxRetries)
```

在方法体内增加枚举校验（在 `if (quantity <= 0)` 校验之后）：

```csharp
if (!Enum.IsDefined(operationType))
{
    throw new OrderDomainException("补偿操作类型非法", "STOCK_COMPENSATION_OPERATION_TYPE_INVALID");
}
```

在 `return new StockReservationCompensation(...) { ... }` 对象初始化器（L90-100）中追加：

```csharp
OperationType = operationType,
```

**1d. 更新类顶部 XML 注释（L7-15）**：在"当 ... 调用 ... 失败时"段后补充说明 OperationType 区分 Release / ReturnDeducted。

**步骤 2：修改 `StockReservationCompensationConfiguration` 映射 `operation_type` 列**

读取 `src/Services/Order/Leno.Order.Infrastructure/Configurations/StockReservationCompensationConfiguration.cs`。

在 `builder.Property(c => c.Quantity).HasColumnName("quantity");`（L20）之后追加：

```csharp
// NEW-P0-3：操作类型列，决定后台任务分发到 ReleaseAsync 或 ReturnDeductedAsync
builder.Property(c => c.OperationType)
    .HasColumnName("operation_type")
    .HasConversion<int>();
```

**说明**：`HasConversion<int>()` 与 `OrderConfiguration.cs` L21、L29 处理 `OrderType` / `Status` 枚举的模式一致。

**步骤 3：修改 `StockReservationCompensationBackgroundService` 按类型分发**

读取 `src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationCompensationBackgroundService.cs`。

将 L109-115 的 try 块内：

```csharp
try
{
    await inventoryRepo.ReleaseAsync(compensation.SkuId, compensation.OrderId, compensation.Quantity, ct);
    compensation.MarkSucceeded();
    await compensationRepo.UpdateAsync(compensation, ct);
    await unitOfWork.SaveChangesAsync(ct);
    succeeded++;
}
```

替换为按 `OperationType` 分发：

```csharp
try
{
    // NEW-P0-3：按 OperationType 分发到正确的库存释放方法
    // Release → ReleaseAsync（释放预占，订单未支付取消场景）
    // ReturnDeducted → ReturnDeductedAsync（归还已扣减，ForceCancel 已支付订单场景）
    // 误用方法会导致 deducted 库存永久丢失（Release 对已扣减库存是 no-op）
    switch (compensation.OperationType)
    {
        case CompensationOperationType.Release:
            await inventoryRepo.ReleaseAsync(compensation.SkuId, compensation.OrderId, compensation.Quantity, ct);
            break;
        case CompensationOperationType.ReturnDeducted:
            await inventoryRepo.ReturnDeductedAsync(compensation.SkuId, compensation.OrderId, compensation.Quantity, ct);
            break;
        default:
            throw new InvalidOperationException(
                $"未知的库存补偿操作类型：{compensation.OperationType} CompensationId={compensation.Id}");
    }

    compensation.MarkSucceeded();
    await compensationRepo.UpdateAsync(compensation, ct);
    await unitOfWork.SaveChangesAsync(ct);
    succeeded++;
}
```

**说明**：
- `IInventoryRepository.ReturnDeductedAsync` 已存在（`src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs#L40`），无需新增接口方法
- `InvalidOperationException` 用于防御性编程，避免未来新增枚举值未同步分发逻辑
- `using Leno.Order.Domain.Aggregates;` 已在 L1 存在，`CompensationOperationType` 在同命名空间

**步骤 4：修改 `StockReservationDomainService.RecordCompensationAsync` 传播 `OperationType`**

读取 `src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs`。

**4a. 修改 `RecordCompensationAsync` 方法签名（L127-128）**：

```csharp
private async Task RecordCompensationAsync(
    Guid orderId, Guid skuId, int quantity, Exception failureException, CancellationToken ct)
```

改为：

```csharp
private async Task RecordCompensationAsync(
    Guid orderId, Guid skuId, int quantity,
    CompensationOperationType operationType,
    Exception failureException, CancellationToken ct)
```

**4b. 修改 `RecordCompensationAsync` 内 `Create` 调用（L136-137）**：

```csharp
var compensation = StockReservationCompensation.Create(
    Guid.NewGuid(), orderId, skuId, quantity);
```

改为：

```csharp
var compensation = StockReservationCompensation.Create(
    Guid.NewGuid(), orderId, skuId, quantity, operationType);
```

**4c. 更新三处调用方传播正确的 `OperationType`**：

调用点 1（L56，`ReserveBatchAsync` 内部回滚失败）：

```csharp
await RecordCompensationAsync(orderId, reservedSku, reservedQty, ex, ct);
```

改为：

```csharp
await RecordCompensationAsync(orderId, reservedSku, reservedQty, CompensationOperationType.Release, ex, ct);
```

调用点 2（L94，`ReleaseBatchAsync` 失败）：

```csharp
await RecordCompensationAsync(orderId, skuId, quantity, ex, ct);
```

改为：

```csharp
await RecordCompensationAsync(orderId, skuId, quantity, CompensationOperationType.Release, ex, ct);
```

调用点 3（L113，`ReturnDeductedBatchAsync` 失败，**关键修复点**）：

```csharp
await RecordCompensationAsync(orderId, skuId, quantity, ex, ct);
```

改为：

```csharp
await RecordCompensationAsync(orderId, skuId, quantity, CompensationOperationType.ReturnDeducted, ex, ct);
```

**说明**：
- L1 `using Leno.Order.Domain.Aggregates;` 已存在，`CompensationOperationType` 在同命名空间可直接引用
- 调用点 3 是 NEW-P0-3 的核心修复：ForceCancel 已支付订单走 `ReturnDeducted`，补偿重试时正确调用 `ReturnDeductedAsync`

**步骤 5：新增迁移 `AddStockCompensationOperationType`**

在 `src/Services/Order/Leno.Order.Infrastructure/` 目录下执行：

```bash
cd src/Services/Order/Leno.Order.Infrastructure
dotnet ef migrations add AddStockCompensationOperationType --context OrderDbContext
```

迁移生成后，**人工核验** `Migrations/<timestamp>_AddStockCompensationOperationType.cs` 的内容：

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // NEW-P0-3：增加 operation_type 列，默认 0 (Release) 以兼容历史数据
    migrationBuilder.AddColumn<int>(
        name: "operation_type",
        table: "stock_reservation_compensations",
        type: "int",
        nullable: false,
        defaultValue: 0);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "operation_type",
        table: "stock_reservation_compensations");
}
```

**关键**：`defaultValue: 0` 确保历史 Pending 记录默认为 `Release`（与旧行为等价，向后兼容）。

**步骤 6：更新 `StockReservationCompensationTests` 单元测试**

读取 `src/Services/Order/Leno.Order.Infrastructure.Tests/StockReservationCompensationTests.cs`。

**6a. 现有 `Create` 调用兼容性**：因 `operationType` 参数有默认值 `Release`，现有 13 处 `StockReservationCompensation.Create(...)` 调用无需修改即可编译通过。但为提升覆盖率，至少新增以下测试用例：

```csharp
[Fact]
public void Create_WithOperationType_ReturnDeducted_ShouldStoreOperationType()
{
    var compensation = StockReservationCompensation.Create(
        Guid.NewGuid(), OrderId, SkuId, 5, CompensationOperationType.ReturnDeducted);

    compensation.OperationType.Should().Be(CompensationOperationType.ReturnDeducted);
}

[Fact]
public void Create_DefaultOperationType_ShouldBeRelease()
{
    var compensation = StockReservationCompensation.Create(Guid.NewGuid(), OrderId, SkuId, 5);

    compensation.OperationType.Should().Be(CompensationOperationType.Release);
}

[Fact]
public void RunRetryCycleAsync_WithReturnDeducted_ShouldCallReturnDeductedAsync()
{
    var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
    var compensation = StockReservationCompensation.Create(
        Guid.NewGuid(), OrderId, SkuId, 5, CompensationOperationType.ReturnDeducted);
    compensationRepoMock
        .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<StockReservationCompensation> { compensation });

    await sut.RunRetryCycleAsync(CancellationToken.None);

    inventoryRepoMock.Verify(
        r => r.ReturnDeductedAsync(OrderId, SkuId, 5, It.IsAny<CancellationToken>()),
        Times.Once);
    inventoryRepoMock.Verify(
        r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
        Times.Never);
    compensation.Status.Should().Be(CompensationStatus.Succeeded);
}

[Fact]
public void RunRetryCycleAsync_WithRelease_ShouldCallReleaseAsync()
{
    var sut = CreateSut(out var compensationRepoMock, out var inventoryRepoMock, out var uowMock);
    var compensation = StockReservationCompensation.Create(
        Guid.NewGuid(), OrderId, SkuId, 5, CompensationOperationType.Release);
    compensationRepoMock
        .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<StockReservationCompensation> { compensation });

    await sut.RunRetryCycleAsync(CancellationToken.None);

    inventoryRepoMock.Verify(
        r => r.ReleaseAsync(OrderId, SkuId, 5, It.IsAny<CancellationToken>()),
        Times.Once);
    inventoryRepoMock.Verify(
        r => r.ReturnDeductedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

**说明**：`CreateSut` 辅助方法已在测试文件 L260 附近定义，沿用现有 Moq 模式。

#### 4.2.2 验证步骤

- [ ] `dotnet build src/Services/Order/Leno.Order.Domain/Leno.Order.Domain.csproj` 零错误零警告
- [ ] `dotnet build src/Services/Order/Leno.Order.Infrastructure/Leno.Order.Infrastructure.csproj` 零错误零警告
- [ ] `dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj --filter "StockReservationCompensationTests"` 全绿（含新增 4 个 OperationType 测试用例）
- [ ] `dotnet ef migrations script --context OrderDbContext --output script-p0-3.sql -p src/Services/Order/Leno.Order.Infrastructure -i` 生成的 SQL 包含 `ALTER TABLE stock_reservation_compensations ADD operation_type int NOT NULL DEFAULT 0;`
- [ ] 人工核验 `OrderDbContextModelSnapshot.cs` 中 `stock_reservation_compensations` 实体已包含 `operation_type` 属性映射
- [ ] 人工核验 `StockReservationDomainService` 三处 `RecordCompensationAsync` 调用传播的 `OperationType` 与上下文一致（ReserveBatchAsync/ReleaseBatchAsync → Release；ReturnDeductedBatchAsync → ReturnDeducted）
- [ ] 集成测试 `ForceCancelRefundIntegrationTests`（已存在）通过，验证 ForceCancel 已支付订单的 `ReturnDeductedAsync` 失败后补偿表记录的 `OperationType = ReturnDeducted`，后台任务重试调用 `ReturnDeductedAsync`

#### 4.2.3 提交

```bash
cd <repo-root>
git add src/Services/Order/Leno.Order.Domain/Aggregates/StockReservationCompensation.cs
git add src/Services/Order/Leno.Order.Infrastructure/Configurations/StockReservationCompensationConfiguration.cs
git add src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationCompensationBackgroundService.cs
git add src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/<timestamp>_AddStockCompensationOperationType.cs
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/<timestamp>_AddStockCompensationOperationType.Designer.cs
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/OrderDbContextModelSnapshot.cs
git add src/Services/Order/Leno.Order.Infrastructure.Tests/StockReservationCompensationTests.cs
git commit -m "[phase1][Order] NEW-P0-3: add OperationType to StockReservationCompensation for type-aware retry dispatch (Release vs ReturnDeducted)"
```

---

## 5. 验证策略

### 5.1 本地构建验证流程

每个 subagent 完成代码修改后，必须按以下顺序执行验证：

1. **编译验证**：
   ```bash
   dotnet build Leno.sln -c Debug
   ```
   零错误（W0 零警告目标，`[Obsolete]` 警告允许但须在后续阶段清理）

2. **单元测试**：
   ```bash
   dotnet test Leno.sln -c Debug --no-build
   ```
   全绿；新增/修改代码覆盖率 ≥ 80%（用 `--collect:"XPlat Code Coverage"` 验证）

3. **迁移脚本核验**（仅 Task 1 / Task 3）：
   ```bash
   dotnet ef migrations script --context OrderDbContext -p src/Services/Order/Leno.Order.Infrastructure -i
   ```
   DBA 评审 Up + Down 方法可逆性

4. **回归测试**：执行受影响 BC 的全量测试套件，确保未破坏既有功能

### 5.2 每任务验收标准汇总

| Task | 验收标准 |
|------|---------|
| Task 1 (NEW-P0-1) | `orders` 表仅存在 `row_version` 一个 rowversion 列；`dotnet ef migrations script` 生成的 SQL 仅 `DROP COLUMN version`；`dotnet build` 全绿；`OrderConfigurationTests` 通过 |
| Task 2 (NEW-P0-2) | `CartUnitOfWork.SaveChangesAsync` 委托 `SaveChangesWithOutboxAsync`；Cart 集成测试验证 `OutboxMessage` 表存在 Cart 领域事件记录；`dotnet build` 全绿 |
| Task 3 (NEW-P0-3) | `stock_reservation_compensations` 表新增 `operation_type` 列，默认 0 (Release)；后台任务按 `OperationType` 分发 `ReleaseAsync` / `ReturnDeductedAsync`；ForceCancel 已支付订单的 `ReturnDeducted` 失败后补偿重试成功；4 个新增单元测试全绿 |
| Task 4 (NEW-P0-4) | `MarkAllAsReadAsync` 调用链触发 `NotificationReadDomainEvent` 写入 Outbox；`UpdatedAt` / `UpdatedBy` 审计字段被填充；幂等性测试通过（已读记录重复调用不再追加事件） |

### 5.3 回归测试范围

- **Order BC**：`OrderConfigurationTests`、`StockReservationCompensationTests`、`ForceCancelRefundIntegrationTests`、`SeckillOrderFlowIntegrationTests`、`OrderMigrationIntegrationTests`
- **Cart BC**：`CartSkuIndexIntegrationTests`、`CartProductSyncIntegrationTests`、`RedisAnonymousCartRepositoryTests`
- **Notification BC**：`NotificationRecordTests`、`EfCoreNotificationRecordRepositoryTests`（如存在）、全量 Notification Domain 测试
- **BuildingBlocks**：`Leno.Infrastructure.Tests`（验证 BaseDbContext 修改未破坏其他 BC 的 shadow property 注入）

### 5.4 DBA 评审门禁

Task 1 与 Task 3 的迁移脚本须提交 DBA 评审，重点核验：

- [ ] `DropOrderVersionShadowColumn.Up` 仅删除 `version` 列，不误删 `row_version` 业务列
- [ ] `DropOrderVersionShadowColumn.Down` 可逆（虽不建议生产回滚）
- [ ] `AddStockCompensationOperationType.Up` 的 `defaultValue: 0` 与枚举 `Release = 0` 对齐
- [ ] 迁移执行顺序：Task 1 迁移先于 Task 3 迁移（Wave 1a → Wave 1b 已保证）

---

## 6. 风险与回滚

### 6.1 风险矩阵

| 风险 ID | 风险描述 | 影响 | 概率 | 缓解措施 |
|---------|---------|------|------|---------|
| R1-1 | Task 1 `DropColumn version` 误删业务数据 | 高 | 低 | 迁移前数据库全量备份；`Down` 方法 `AddColumn` 可恢复（虽会重新引入双 rowversion 冲突）；DBA 评审门禁 |
| R1-2 | Task 1 修改 `BaseDbContext` 破坏其他 BC 的 shadow property 注入 | 中 | 低 | 全量 `dotnet build Leno.sln` 验证；`Leno.Infrastructure.Tests` 回归测试覆盖；逻辑仅"跳过已显式配置 rowversion 的实体"，对未显式配置的实体行为不变 |
| R1-3 | Task 1 与 Task 3 迁移文件冲突 | 中 | 中 | Wave 1b 串行等待 Wave 1a 完成；主 agent 在 Wave 1a 后统一 `git push` 再启动 Wave 1b |
| R2-1 | Task 2 Outbox 引入性能开销（Cart 高频写入） | 中 | 中 | 压测对比 SaveChanges 与 SaveChangesWithOutboxAsync 延迟；保留旧实现代码注释 1 个版本周期以便快速回退；如性能不达标，阶段二 P1-1 同步优化 |
| R3-1 | Task 3 历史 Pending 记录 `OperationType` 默认为 Release，但实际应为 ReturnDeducted | 高 | 低 | `defaultValue: 0` (Release) 与旧行为等价（旧版统一调 ReleaseAsync）；仅影响 ForceCancel 已支付订单且失败时间在迁移前后的极少数在途记录；人工巡检补偿表，对 OperationType 不符的 ReturnDeducted 场景手工修正 |
| R3-2 | Task 3 `InvalidOperationException` 防御分支未测试 | 低 | 低 | 单元测试覆盖默认分支（虽编译期枚举穷尽，未来新增枚举值时编译不报错） |
| R4-1 | Task 4 `MarkAllAsReadAsync` 改为加载所有未读记录到内存，大用户量场景内存压力 | 中 | 低 | 当前未读站内信数量有上限（产品策略限制单用户未读 ≤ 500）；如未来突破，阶段二 P1 优化为分页批量处理 |
| R4-2 | Task 4 新增 `NotificationReadDomainEvent` 未配置 IntegrationEventMapper | 低 | 低 | 本阶段仅发布领域事件，未读数缓存失效可由领域事件直接消费（同进程）；集成事件 mapper 留待阶段二补充 |

### 6.2 回滚预案

**每项任务独立回滚**（git revert 单 commit 即可）：

| Task | 回滚命令 | 回滚后状态 |
|------|---------|----------|
| Task 1 | `git revert <commit-hash>` | 恢复双 rowversion 列（重新引入部署阻塞，仅紧急回退用） |
| Task 2 | `git revert <commit-hash>` | 恢复 `SaveChangesAsync` 旁路 Outbox（Cart 事件丢失问题回归） |
| Task 3 | `git revert <commit-hash>` + 数据库 `ALTER TABLE stock_reservation_compensations DROP COLUMN operation_type;` | 恢复统一调 `ReleaseAsync`（ForceCancel 已支付订单 deducted 库存丢失问题回归） |
| Task 4 | `git revert <commit-hash>` | 恢复 `ExecuteUpdateAsync` 绕聚合根（领域事件缺失、审计字段缺失问题回归） |

**全阶段回滚**：按 Task 4 → Task 3 → Task 2 → Task 1 逆序 revert（与提交顺序相反，避免迁移依赖冲突）。

### 6.3 双轨期策略

阶段一不引入双轨期（4 项均为 P0 阻塞修复，必须直接生效）。但 Task 2 的 `[Obsolete]` 标注引入了**软迁移窗口**：Cart BC 调用方可在阶段二逐步从 `SaveChangesAsync` 迁移至 `SaveEntitiesAsync`，编译期警告驱动清理。

---

## 附录 A：母方案部分修复遗留项处理说明

母方案 2.2.2 节标注的 3 项"部分修复"在阶段一的处理：

| 遗留项 | 处理方式 | 关联 Task |
|--------|---------|----------|
| Cart 聚合缺乐观锁（shadow property 隐式生效） | 阶段一不处理，阶段二 P1-1 统一处理（与匿名购物车并发覆盖写合并） | — |
| Order Saga 补偿失败（补偿表无 OperationType） | 即 NEW-P0-3，Task 3 已覆盖 | Task 3 |
| 各 BC Outbox 旁路（Cart 是最后一个旁路的 BC） | 即 NEW-P0-2，Task 2 已覆盖 | Task 2 |

---

## 附录 B：subagent 编排执行清单

主 agent 按以下清单驱动 4 个 subagent：

### Wave 1a（3 并行 subagent，同步启动）

- [ ] 启动 subagent-A：执行 Task 1（NEW-P0-1，Order / Shared）
- [ ] 启动 subagent-B：执行 Task 2（NEW-P0-2，Cart）
- [ ] 启动 subagent-C：执行 Task 4（NEW-P0-4，Notification）
- [ ] 等待 3 个 subagent 全部返回"已完成"报告
- [ ] 主 agent 执行 `git push` 推送 Wave 1a 全部 commits

### Wave 1b（1 串行 subagent，依赖 Wave 1a）

- [ ] 启动前置核验（见 4.1 节）
- [ ] 启动 subagent-D：执行 Task 3（NEW-P0-3，Order）
- [ ] 等待 subagent-D 返回"已完成"报告
- [ ] 主 agent 执行 `git push` 推送 Wave 1b commit

### 阶段一收尾

- [ ] 主 agent 执行 `dotnet build Leno.sln` 全量编译验证
- [ ] 主 agent 执行 `dotnet test Leno.sln` 全量测试验证
- [ ] DBA 评审 Task 1 / Task 3 迁移脚本
- [ ] 主 agent 汇总 4 个 subagent 报告，产出阶段一执行总结（健康度 8.3 → 8.5 验证）
- [ ] 通知阶段二负责人阶段一已就绪，可启动阶段二前置依赖核验

---

**阶段一实施计划完成**

本计划为 4 项 P0 阻塞修复定义了 2 波 4 subagent 的可执行级编排，每任务包含精确文件路径、行号、修改代码片段、验证步骤（checkbox）、commit message 格式。严格前置依赖（Wave 1a → Wave 1b）与 BC 目录互斥矩阵确保无 git 冲突。所有迁移脚本配套 Down 方法，DBA 评审门禁保障生产安全。
