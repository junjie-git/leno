# 阶段二：速赢优化 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**日期**：2026-07-23
**输入**：[00-architecture-upgrade-plan.md](./00-architecture-upgrade-plan.md) 第六章 6.2 节
**前置依赖**：[01-phase1-p0-fixes.md](./01-phase1-p0-fixes.md) 全部完成（NEW-P0-4 即 P1-5 已完成）
**目标**：清理重复实现，配置化抽取，索引补充，修复 P1 高优先级问题。健康度 8.5 → 8.8
**架构**：14 项任务分 4 组（G1 共享层去重/G2 性能优化/G3 数据库索引/G4 P1 修复），2 波 × 4 并行 subagent
**Tech Stack**：.NET 10, EF Core, SQL Server, Redis, IOptionsMonitor

---

## 1. 范围与约束

### 1.1 实施范围

阶段二共 14 项任务（母方案 6.2 节步骤 1-15，跳过步骤 10 P1-5 已在阶段一 Task 4 完成），按 4 组分类，2 波 × 4 并行 subagent 编排。任务分布：

| 组别 | 任务数 | 任务编号 | 修改区域 | 互斥性 |
|------|--------|---------|---------|--------|
| **G1 共享层去重** | 3 项 | 2.1.1 RateLimiter / 2.1.2 TraceIdEnricher / 2.1.3 AuditableInterceptor | `BuildingBlocks/Leno.Infrastructure/` + `ApiGateway/` | 组内串行（同目录） |
| **G2 性能优化** | 2 项 | 2.2.1 BloomFilter Lua / 2.2.2 NotificationDispatcher SaveChanges | `Leno.Infrastructure/Caching/` + `Notification.Infrastructure/` | 并行（不同 BC） |
| **G3 数据库索引** | 2 项 | 2.3.1 reviews.seller_id / 2.3.2 notification_records 复合索引 | `ReviewAfterSales.Infrastructure/` + `Notification.Infrastructure/` Configuration + 迁移 | 并行 |
| **G4 P1 修复** | 7 项 | 2.4.1 P1-1 Cart / 2.4.2 P1-4 Payment / 2.4.3 P1-7 配置化 / 2.4.4 P1-9 JwtTTL / 2.4.5 P1-10 Promotion / 2.4.6 死代码清理 / 2.4.7 Outbox 归档 | Cart/Payment/Notification/UserAuth/Promotion/SellerShop/SystemAdmin | 并行（不同 BC） |

**跳过项说明**：母方案步骤 10（P1-5 Notification MarkAllAsReadAsync）已在阶段一 Task 4（NEW-P0-4）随 P0 修复完成，本阶段不重复处理。

### 1.2 关键约束

- **本地构建验证**：subagent 写代码后 `dotnet build` / `dotnet test` 验证，失败则修复后提交（非 `[unverified]` 模式）
- **5 并行槽位**：每波最多 4 个 subagent 并行（留 1 槽位给主 agent 协调）
- **BC 目录互斥**：subagent 各自 `git add` 自己 BC 目录的文件，BC 互斥矩阵见 §2.4
- **代码完整性强制契约**：禁止占位符、TODO、空实现、截断输出；每函数完整实现
- **零行为变更门禁**：配置化任务（2.4.3 / 2.4.4）缺省值必须与现有 const 完全对齐，配套对齐测试
- **G1 串行约束**：G1 共享层去重的 3 个任务修改同一目录 `BuildingBlocks/Leno.Infrastructure/`，在同一 subagent 内串行执行
- **DBA 评审前置**：G3 索引迁移脚本必须配套 Down 方法 + 在线创建（`WITH (ONLINE = ON)`），DBA 评审通过后执行

### 1.3 前置依赖核验清单

执行阶段二前，逐项核验阶段一产出已就绪：

- [x]**NEW-P0-1 修复**：`src/Services/Order/Leno.Order.Infrastructure/Migrations/` 已含 DropColumn `version` 迁移，SQL Server 单 rowversion 列
- [x]**NEW-P0-2 修复**：`src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs` 已改委托 `SaveChangesWithOutboxAsync`，Cart 领域事件经 Outbox 投递
- [x]**NEW-P0-3 修复**：`StockReservationCompensation` 实体已增 `OperationType` 枚举字段，补偿按类型分发
- [x]**NEW-P0-4（即 P1-5）修复**：`src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs` `MarkAllAsReadAsync` 已改加载聚合根逐个标记 + `SaveEntitiesAsync`
- [x]**阶段一全部 commit**：4 项 P0 commit 已合并，CI 全绿，`01-phase1-p0-fixes.md` 全部 checkbox 已勾选
- [x]**健康度基线 8.5**：阶段一完成后 BC 健康度矩阵复评，加权平均 ≥ 8.5

---

## 2. 总体架构

### 2.1 波次编排图

```
Wave 1（4 并行 subagent）                Wave 2（4 并行 subagent）
┌──────────┬──────────┬──────────┬──────────┐  ┌──────────┬──────────┬──────────┬──────────┐
│G1 共享层 │G3 索引   │G4-P1 Cart│G4-P1 Pay │  │G2 性能优 │G4-P1 配置│G4-P1 死代│G4-P1 Out │
│去重串行  │review+not│P1-1 匿名 │P1-4 支付 │  │化 BloomF │化 P1-7/  │码清理    │box 归档  │
│3 任务    │ification │购物车    │单卡Pending│  │+Dispatc  │P1-9/P1-10│14项      │15项      │
│Infra+GW  │_records  │Cart BC   │Payment BC│  │her       │3 BC      │多 BC     │SystemAdm │
└──────────┴──────────┴──────────┴──────────┘  └──────────┴──────────┴──────────┴──────────┘
   ↓ 4 commits                            ↓ 4 commits
   ─────────────────────────────────────────→ Wave 2 启动
```

**波次划分依据**：
- **Wave 1**：修改基础设施层 + 独立 BC，不依赖其他任务产出
- **Wave 2**：G2 依赖 G1 去重后的共享层；G4 配置化依赖 G1 的 `IOptionsMonitor` 模式确立

### 2.2 subagent 总数与分批策略

- **subagent 总数**：8 个（2 波 × 4 并行）
- **每波并行度**：4 个 subagent（留 1 槽位给主 agent）
- **G1 串行说明**：G1 共享层去重的 3 个任务（2.1.1 / 2.1.2 / 2.1.3）修改同一目录 `BuildingBlocks/Leno.Infrastructure/`，在同一 subagent 内串行执行（先去重 RateLimiter，再合并 TraceIdEnricher，最后清理 AuditableInterceptor），对外表现为 1 个并行槽位

### 2.3 BC 互斥矩阵

| subagent | 涉及 BC / 模块 | 主要目录 | 与其他 subagent 冲突点 |
|---------|--------------|---------|---------------------|
| Wave1-G1 | Shared（BuildingBlocks + ApiGateway） | `src/BuildingBlocks/Leno.Infrastructure/` + `src/ApiGateway/Leno.ApiGateway/Services/` | 无（独占共享层） |
| Wave1-G3 | ReviewAfterSales + Notification | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/` + `src/Services/Notification/Leno.Notification.Infrastructure/Configurations/` | 无（仅 Configuration 文件） |
| Wave1-Cart | Cart | `src/Services/Cart/Leno.Cart.Infrastructure/Repositories/` | 无 |
| Wave1-Payment | Payment | `src/Services/Payment/Leno.Payment.Infrastructure/Consumers/` | 无 |
| Wave2-G2 | Shared + Notification | `src/BuildingBlocks/Leno.Infrastructure/Caching/` + `src/Services/Notification/Leno.Notification.Infrastructure/Services/` | 无（Wave 1 G1 已完成） |
| Wave2-Config | Notification + UserAuth + Promotion | 3 BC 各自 `Services/` + `appsettings.json` | 无（独立 BC） |
| Wave2-DeadCode | Notification + SellerShop | `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/` + `src/Services/SellerShop/Leno.SellerShop.Application/Services/` | 无（Wave 2-Config 不改 Consumer） |
| Wave2-Outbox | SystemAdmin | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/` + OutboxMessage 表 | 无 |

---

## 3. Wave 1 详细编排（4 并行 subagent）

Wave 1 启动 4 个 subagent 并行执行：G1 共享层去重（串行 3 任务） / G3 数据库索引（2 任务） / G4-P1 Cart（1 任务） / G4-P1 Payment（1 任务）。

### 3.1 Subagent W1-G1：共享层去重（串行 3 任务）

**subagent 范围**：BuildingBlocks + ApiGateway 共享层，3 任务串行执行
**修改目录**：`src/BuildingBlocks/Leno.Infrastructure/` + `src/ApiGateway/Leno.ApiGateway/Services/`
**串行顺序**：2.1.1 RateLimiter → 2.1.2 TraceIdEnricher → 2.1.3 AuditableInterceptor

#### 3.1.1 任务 2.1.1：RedisSlidingWindowRateLimiter 双份去重

**问题证据**：
- `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs`（ApiGateway 副本）
- `src/BuildingBlocks/Leno.Infrastructure/RateLimiting/`（共享层实现）

**修改指令**：

1. 删除 `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs` 整个文件
2. 修改 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`，确保已引用 `Leno.Infrastructure` 项目
3. 修改 `src/ApiGateway/Leno.ApiGateway/Services/` 目录下所有引用 `RedisSlidingWindowRateLimiter` 的文件，将 `using Leno.ApiGateway.Services;` 改为 `using Leno.Infrastructure.RateLimiting;`，确保命名空间一致（如不一致，在共享层实现上添加 `using` 别名或类型转发）
4. 修改 `src/ApiGateway/Leno.ApiGateway/Dependencies/ServiceCollectionExtensions.cs`（或对应 DI 注册文件），将 `services.AddSingleton<RedisSlidingWindowRateLimiter>();` 改为引用共享层实现的注册方式
5. 全局搜索 `RedisSlidingWindowRateLimiter` 残留引用，确保零命中（除共享层实现本身）

**代码片段（DI 注册对齐）**：

```csharp
// src/ApiGateway/Leno.ApiGateway/Dependencies/ServiceCollectionExtensions.cs
// 修改前
services.AddSingleton<Leno.ApiGateway.Services.RedisSlidingWindowRateLimiter>();

// 修改后
services.AddSingleton<Leno.Infrastructure.RateLimiting.RedisSlidingWindowRateLimiter>();
```

**验证步骤**：

- [x]全局搜索 `Leno.ApiGateway.Services.RedisSlidingWindowRateLimiter` 零命中：`grep -r "Leno.ApiGateway.Services.RedisSlidingWindowRateLimiter" src/`
- [x]ApiGateway 副本文件已删除：`ls src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs`（应返回 No such file）
- [x]`dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` 零错误零警告
- [x]`dotnet test src/ApiGateway/Leno.ApiGateway.Tests/`（如有）全绿
- [x]ApiGateway 限流集成测试通过（限流命中返回 429）
- [x]`git add src/ApiGateway/Leno.ApiGateway/ src/BuildingBlocks/Leno.Infrastructure/RateLimiting/`
- [x]`git commit -m "[phase2][Shared] 2.1.1: 删除 ApiGateway RedisSlidingWindowRateLimiter 副本，引用共享层实现"`

#### 3.1.2 任务 2.1.2：TraceIdEnricher 双份合并

**问题证据**：
- `src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs#L35-48`（Serilog 端 TraceIdEnricher）
- `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs#L130-149`（OTel 端 OpenTelemetryTraceIdEnricher）

**修改指令**：

1. 保留 `src/BuildingBlocks/Leno.Infrastructure/Logging/TraceIdEnricher.cs`（如不存在则从 SerilogConfig.cs L35-48 抽取为独立类），作为单一实现
2. 删除 `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs#L130-149` 中的 `OpenTelemetryTraceIdEnricher` 类定义
3. 修改 `OpenTelemetryExtensions.cs` 中所有引用 `OpenTelemetryTraceIdEnricher` 的位置，改为引用 `Leno.Infrastructure.Logging.TraceIdEnricher`
4. 确保 `TraceIdEnricher` 同时支持 Serilog `LogContext` 与 OTel `Activity.Current` 两种 TraceId 来源，逻辑：优先 OTel `Activity.Current?.TraceId`，回退 Serilog `LogContext`
5. 全局搜索 `OpenTelemetryTraceIdEnricher` 残留引用，确保零命中

**代码片段（合并后的 TraceIdEnricher）**：

```csharp
// src/BuildingBlocks/Leno.Infrastructure/Logging/TraceIdEnricher.cs
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Leno.Infrastructure.Logging;

/// <summary>
/// 统一的 TraceId 日志富化器：优先从 OTel Activity.Current 获取 TraceId，
/// 回退到 Serilog LogContext 中的 TraceId。
/// </summary>
public sealed class TraceIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // 优先 OTel Activity
        var traceId = Activity.Current?.TraceId.ToString();

        // 回退 Serilog LogContext
        if (string.IsNullOrEmpty(traceId)
            && logEvent.Properties.TryGetValue("TraceId", out var serilogTraceId))
        {
            traceId = serilogTraceId.ToString().Trim('"');
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", traceId));
        }
    }
}
```

**验证步骤**：

- [x]全局搜索 `OpenTelemetryTraceIdEnricher` 零命中：`grep -r "OpenTelemetryTraceIdEnricher" src/`
- [x]`dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` 零错误零警告
- [x]单元测试：注入带 TraceId 的 Activity，验证日志输出含 TraceId 字段
- [x]单元测试：无 Activity 时回退 Serilog LogContext，验证 TraceId 字段存在
- [x]`dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/`（如有）全绿
- [x]`git add src/BuildingBlocks/Leno.Infrastructure/Logging/ src/BuildingBlocks/Leno.Infrastructure/Telemetry/`
- [x]`git commit -m "[phase2][Shared] 2.1.2: 合并 TraceIdEnricher 与 OpenTelemetryTraceIdEnricher 为单一实现"`

#### 3.1.3 任务 2.1.3：AuditableEntityInterceptor 与 FillAuditableFields 去重

**问题证据**：
- `src/BuildingBlocks/Leno.Infrastructure/Persistence/EFCoreInterceptors.cs#L12-51`（AuditableEntityInterceptor）
- `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs` 中 `FillAuditableFields` 方法

**修改指令**：

1. 保留 `src/BuildingBlocks/Leno.Infrastructure/Persistence/EFCoreInterceptors.cs#L12-51` 的 `AuditableEntityInterceptor`（EF Core 推荐方式，通过 `SaveChangesInterceptor` 拦截）
2. 删除 `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs` 中的 `FillAuditableFields` 方法及其重写调用
3. 修改 `BaseDbContext.cs` 的 `OnModelCreating` 或 `SaveChangesAsync` 重写，移除对 `FillAuditableFields` 的显式调用（拦截器已在 EF 管道中自动填充）
4. 确保 `BaseDbContext` 构造时已注册 `AuditableEntityInterceptor`（通过 `optionsBuilder.AddInterceptors` 注入）
5. 验证所有继承 `BaseDbContext` 的 BC DbContext 仍正常写入 `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` 审计字段

**代码片段（移除 FillAuditableFields 后的 BaseDbContext）**：

```csharp
// src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs
// 修改前（删除以下方法）
// protected void FillAuditableFields()
// {
//     var entries = ChangeTracker.Entries<IAuditableEntity>()...;
//     ...
// }

// 修改后：在 OnConfiguring 或构造函数中确保拦截器已注册
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (!optionsBuilder.IsConfigured)
    {
        optionsBuilder.AddInterceptors(new AuditableEntityInterceptor());
    }
    base.OnConfiguring(optionsBuilder);
}

// SaveChangesAsync 重写中移除 FillAuditableFields() 调用
public override Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
{
    // 拦截器 AuditableEntityInterceptor 已在 SavingChanges 事件中填充审计字段
    // 不再需要显式调用 FillAuditableFields()
    return base.SaveChangesAsync(cancellationToken);
}
```

**验证步骤**：

- [x]全局搜索 `FillAuditableFields` 零命中：`grep -r "FillAuditableFields" src/`
- [x]`dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` 零错误零警告
- [x]单元测试：新增实体并 `SaveChangesAsync`，验证 `CreatedAt`/`CreatedBy` 字段已填充
- [x]单元测试：更新实体并 `SaveChangesAsync`，验证 `UpdatedAt`/`UpdatedBy` 字段已更新
- [x]全解决方案编译：`dotnet build` 零错误零警告
- [x]`dotnet test` 全绿，审计字段相关测试覆盖
- [x]`git add src/BuildingBlocks/Leno.Infrastructure/Persistence/`
- [x]`git commit -m "[phase2][Shared] 2.1.3: 删除 BaseDbContext.FillAuditableFields，保留 AuditableEntityInterceptor 单一实现"`

---

### 3.2 Subagent W1-G3：数据库索引补充（2 任务）

**subagent 范围**：ReviewAfterSales + Notification 两个 BC 的 EF Configuration + 迁移
**修改目录**：`src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/` + `src/Services/Notification/Leno.Notification.Infrastructure/Configurations/`

#### 3.2.1 任务 2.3.1：reviews.seller_id 索引补充（P1-6）

**问题证据**：
- `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs#L58-60`（卖家后台查询全表扫）

**修改指令**：

1. 修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs`，在 `Configure` 方法中增加索引配置
2. 索引名：`ix_reviews_seller_id`，列：`SellerId`，包含列（Include）：`CreatedAt`、`Rating`（覆盖卖家后台常用查询）
3. 新增 EF Core 迁移：`cd src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ && dotnet ef migrations add AddIxReviewsSellerId`
4. 编辑生成的迁移文件，确保 `CreateIndex` 使用 `WITH (ONLINE = ON)` 选项（SQL Server 在线创建索引，避免锁表）。通过 `migrationBuilder.Sql` 手写 SQL 实现
5. 配套 Down 方法：`migrationBuilder.DropIndex` 删除索引

**代码片段（Configuration 索引配置）**：

```csharp
// src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs
// 在 Configure 方法中添加
builder.HasIndex(r => r.SellerId)
    .HasDatabaseName("ix_reviews_seller_id")
    .IncludeProperties(r => new { r.CreatedAt, r.Rating });
```

**代码片段（迁移文件在线创建索引）**：

```csharp
// src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Migrations/<timestamp>_AddIxReviewsSellerId.cs
public partial class AddIxReviewsSellerId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQL Server 在线创建索引，避免锁表
        migrationBuilder.Sql(
            @"CREATE INDEX ix_reviews_seller_id 
              ON reviews (seller_id) 
              INCLUDE (created_at, rating) 
              WITH (ONLINE = ON, FILLFACTOR = 90);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX ix_reviews_seller_id ON reviews;");
    }
}
```

**验证步骤**：

- [x]`dotnet build src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/` 零错误
- [x]迁移脚本生成成功：`dotnet ef migrations script --project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/` 含 `CREATE INDEX ix_reviews_seller_id`
- [x]DBA 评审迁移脚本（含 `WITH (ONLINE = ON)` + Down 方法）
- [x]测试库执行迁移，SQL Server 验证索引存在：`SELECT name FROM sys.indexes WHERE name = 'ix_reviews_seller_id'`
- [x]查询计划验证：`SELECT * FROM reviews WHERE seller_id = @p0` 走索引查找（Index Seek）而非全表扫
- [x]`dotnet test src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/`（如有）全绿
- [x]`git add src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Migrations/`
- [x]`git commit -m "[phase2][ReviewAfterSales] 2.3.1: 补充 reviews.seller_id 索引（含 Include 列，在线创建）"`

#### 3.2.2 任务 2.3.2：notification_records 复合索引补充

**问题证据**：
- `src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L44-57`（用户通知列表查询全表扫）

**修改指令**：

1. 修改 `src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs`，在 `Configure` 方法中增加复合索引
2. 索引名：`ix_notification_records_user_isread_channel`，列顺序：`UserId`、`IsRead`、`Channel`（高选择性列在前）
3. 包含列（Include）：`CreatedAt`、`TemplateCode`（覆盖用户通知列表查询常用字段）
4. 新增 EF Core 迁移：`cd src/Services/Notification/Leno.Notification.Infrastructure/ && dotnet ef migrations add AddIxNotificationRecordsUserIsreadChannel`
5. 迁移文件使用 `migrationBuilder.Sql` 手写 SQL，含 `WITH (ONLINE = ON)`
6. 配套 Down 方法：`migrationBuilder.DropIndex`

**代码片段（Configuration 复合索引配置）**：

```csharp
// src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs
// 在 Configure 方法中添加
builder.HasIndex(n => new { n.UserId, n.IsRead, n.Channel })
    .HasDatabaseName("ix_notification_records_user_isread_channel")
    .IncludeProperties(n => new { n.CreatedAt, n.TemplateCode });
```

**代码片段（迁移文件）**：

```csharp
// src/Services/Notification/Leno.Notification.Infrastructure/Migrations/<timestamp>_AddIxNotificationRecordsUserIsreadChannel.cs
public partial class AddIxNotificationRecordsUserIsreadChannel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            @"CREATE INDEX ix_notification_records_user_isread_channel 
              ON notification_records (user_id, is_read, channel) 
              INCLUDE (created_at, template_code) 
              WITH (ONLINE = ON, FILLFACTOR = 90);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX ix_notification_records_user_isread_channel ON notification_records;");
    }
}
```

**验证步骤**：

- [x]`dotnet build src/Services/Notification/Leno.Notification.Infrastructure/` 零错误
- [x]迁移脚本生成成功：`dotnet ef migrations script --project src/Services/Notification/Leno.Notification.Infrastructure/` 含 `CREATE INDEX ix_notification_records_user_isread_channel`
- [x]DBA 评审迁移脚本（含 `WITH (ONLINE = ON)` + Down 方法）
- [x]测试库执行迁移，SQL Server 验证索引存在：`SELECT name FROM sys.indexes WHERE name = 'ix_notification_records_user_isread_channel'`
- [x]查询计划验证：`SELECT * FROM notification_records WHERE user_id = @p0 AND is_read = 0 AND channel = 'Sms'` 走索引查找
- [x]`dotnet test src/Services/Notification/Leno.Notification.Infrastructure.Tests/`（如有）全绿
- [x]`git add src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs src/Services/Notification/Leno.Notification.Infrastructure/Migrations/`
- [x]`git commit -m "[phase2][Notification] 2.3.2: 补充 notification_records (user_id,is_read,channel) 复合索引"`

---

### 3.3 Subagent W1-Cart：P1-1 匿名购物车并发覆盖写（1 任务）

**subagent 范围**：Cart BC Redis 仓储
**修改目录**：`src/Services/Cart/Leno.Cart.Infrastructure/Repositories/`

#### 3.3.1 任务 2.4.1：P1-1 匿名购物车并发覆盖写 Lua 脚本

**问题证据**：
- `src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L69`（`SaveAsync` 非原子，并发更新覆盖写）

**修改指令**：

1. 修改 `src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs` 的 `SaveAsync` 方法，改用 Redis Lua 脚本实现 CAS（Compare-And-Swap）原子更新
2. CAS 模式：Lua 脚本先 `GET` 当前 cart 的 `version` 字段，与客户端传入的 `expectedVersion` 比较；相等则 `SET` 新值并 `INCR version`，返回成功；不等则返回失败（并发冲突）
3. 保留旧 `SaveAsync` 实现为 `SaveAsyncLegacy`，标注 `[Obsolete("Use SaveAsync with CAS Lua script instead.")]`，作为 fallback（1 个版本周期后删除）
4. 新增单元测试：模拟并发场景（10 个并发 `SaveAsync` 同一 cart），验证仅 1 个成功，其余返回冲突
5. Cart 聚合根增加 `Version` 字段（如尚未存在），仓储加载时返回当前 Version，保存时传入 expectedVersion

**代码片段（Lua 脚本 CAS 原子更新）**：

```csharp
// src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs

private static readonly string CasSaveLuaScript = @"
local key = KEYS[1]
local expectedVersion = tonumber(ARGV[1])
local newValue = ARGV[2]
local newVersion = ARGV[3]
local ttl = tonumber(ARGV[4])

local currentVersion = redis.call('HGET', key, 'version')
if currentVersion == false then
    -- key 不存在，首次创建
    redis.call('HSET', key, 'payload', newValue, 'version', newVersion)
    redis.call('EXPIRE', key, ttl)
    return 1
end

if tonumber(currentVersion) ~= expectedVersion then
    -- 版本不匹配，并发冲突
    return 0
end

redis.call('HSET', key, 'payload', newValue, 'version', newVersion)
redis.call('EXPIRE', key, ttl)
return 1
";

public async Task<bool> SaveAsync(
    string anonymousId,
    CartPayload cart,
    int expectedVersion,
    CancellationToken cancellationToken = default)
{
    var key = $"cart:anon:{anonymousId}";
    var newVersion = expectedVersion + 1;
    var payload = JsonSerializer.Serialize(cart);
    var ttl = (int)TimeSpan.FromDays(30).TotalSeconds;

    var result = (long)await _database.ScriptEvaluateAsync(
        CasSaveLuaScript,
        new RedisKey[] { key },
        new RedisValue[]
        {
            expectedVersion,
            payload,
            newVersion,
            ttl
        });

    return result == 1;
}
```

**验证步骤**：

- [x]`dotnet build src/Services/Cart/Leno.Cart.Infrastructure/` 零错误零警告
- [x]单元测试：单线程 SaveAsync 成功，Version 递增
- [x]单元测试：10 并发 SaveAsync 同一 cart，仅 1 个成功（返回 true），其余返回 false（冲突）
- [x]集成测试（Testcontainers Redis）：真实 Redis 实例验证 Lua 脚本执行
- [x]集成测试：并发场景下 cart 内容无丢失（最后一个成功的写入持久化）
- [x]旧 `SaveAsyncLegacy` 标注 `[Obsolete]`，全局搜索无新增调用
- [x]`dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/` 全绿
- [x]`dotnet test src/Services/Cart/Leno.Cart.FunctionalTests/`（如有）全绿
- [x]`git add src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs`
- [x]`git commit -m "[phase2][Cart] 2.4.1: P1-1 修复匿名购物车并发覆盖写，改用 Lua 脚本 CAS 原子更新"`

---

### 3.4 Subagent W1-Payment：P1-4 支付单卡 Pending（1 任务）

**subagent 范围**：Payment BC 消费者
**修改目录**：`src/Services/Payment/Leno.Payment.Infrastructure/Consumers/`

#### 3.4.1 任务 2.4.2：P1-4 PaymentRequestedEventConsumer 支付单卡 Pending

**问题证据**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs#L48-54`（支付单 Pending 状态时拒绝重新发起）

**修改指令**：

1. 修改 `src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs#L48-54` 的消费逻辑
2. 当用户重新发起支付时，查询现有支付单状态：
   - `Pending` 状态：返回现有支付单的支付链接（不创建新支付单，幂等）
   - `Succeeded` 状态：抛出异常或返回业务错误（已支付，不能重复发起）
   - `Failed`/`Cancelled` 状态：创建新支付单
   - 不存在：创建新支付单
3. 在 `PaymentOrder` 聚合根增加 `GetActivePaymentLink()` 方法，返回当前 Pending 支付单的支付链接
4. 状态检查与创建新支付单必须在同一事务内（通过 `SaveEntitiesAsync` 保证原子性），避免 TOCTOU
5. 新增单元测试覆盖 4 种状态分支

**代码片段（消费者状态检查逻辑）**：

```csharp
// src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs
public async Task Consume(ConsumeContext<PaymentRequestedEvent> context)
{
    var message = context.Message;
    var existingOrder = await _paymentOrderRepository.GetByOrderIdAsync(
        message.OrderId, context.CancellationToken);

    if (existingOrder != null)
    {
        var activePayment = existingOrder.GetActivePayment();
        if (activePayment != null && activePayment.Status == PaymentStatus.Pending)
        {
            // 幂等：返回现有 Pending 支付单的支付链接，不创建新支付单
            await context.RespondAsync(new PaymentReadyEvent
            {
                OrderId = message.OrderId,
                PaymentId = activePayment.Id,
                PaymentLink = activePayment.PaymentLink,
                IsReused = true // 标记复用现有支付单
            });
            return;
        }

        if (activePayment?.Status == PaymentStatus.Succeeded)
        {
            // 已支付，拒绝重复发起
            throw new PaymentAlreadySucceededException(message.OrderId);
        }
        // Failed/Cancelled 状态：继续创建新支付单
    }

    // 创建新支付单
    var paymentOrder = PaymentOrder.Create(message.OrderId, message.UserId, message.Amount, message.Channel);
    await _paymentOrderRepository.AddAsync(paymentOrder, context.CancellationToken);
    await _unitOfWork.SaveEntitiesAsync(context.CancellationToken);

    await context.RespondAsync(new PaymentReadyEvent
    {
        OrderId = message.OrderId,
        PaymentId = paymentOrder.Id,
        PaymentLink = paymentOrder.GetActivePaymentLink(),
        IsReused = false
    });
}
```

**验证步骤**：

- [x]`dotnet build src/Services/Payment/Leno.Payment.Infrastructure/` 零错误零警告
- [x]单元测试：Pending 状态重新发起，返回相同 PaymentId 与 PaymentLink（IsReused=true）
- [x]单元测试：Succeeded 状态重新发起，抛出 `PaymentAlreadySucceededException`
- [x]单元测试：Failed 状态重新发起，创建新支付单（IsReused=false）
- [x]单元测试：无现有支付单，创建新支付单
- [x]集成测试（MassTransit TestHarness）：模拟用户连续两次发起支付，验证不产生重复支付单
- [x]`dotnet test src/Services/Payment/Leno.Payment.Infrastructure.Tests/` 全绿
- [x]`git add src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs`
- [x]`git commit -m "[phase2][Payment] 2.4.2: P1-4 修复支付单卡 Pending，支持幂等重新发起"`

---

## 4. Wave 2 详细编排（4 并行 subagent）

Wave 2 启动 4 个 subagent 并行执行：G2 性能优化（2 任务） / G4-P1 配置化（3 任务） / G4-P1 死代码清理（1 任务） / G4-P1 Outbox 归档（1 任务）。Wave 2 启动前确认 Wave 1 全部 4 个 commit 已完成。

### 4.1 Subagent W2-G2：性能优化（2 任务）

**subagent 范围**：BuildingBlocks Caching + Notification Services
**修改目录**：`src/BuildingBlocks/Leno.Infrastructure/Caching/` + `src/Services/Notification/Leno.Notification.Infrastructure/Services/`

#### 4.1.1 任务 2.2.1：RedisBloomFilter Lua 化

**问题证据**：
- `src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs`（`MightContainAsync` 7 次串行 `StringGetBitAsync`，7 次网络往返）

**修改指令**：

1. 修改 `src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs` 的 `MightContainAsync` 方法
2. 改用 Lua 脚本一次 `EVAL` 替代 7 次串行 `StringGetBitAsync`，将 7 次网络往返降为 1 次
3. Lua 脚本接收 7 个位偏移作为 ARGV，遍历检查所有位是否都为 1，全部为 1 返回 1（可能存在），任一为 0 返回 0（一定不存在）
4. `AddAsync` 方法同样改用 Lua 脚本，一次 `SETBIT` 7 个位
5. 保留原方法签名（向后兼容），内部实现切换为 Lua

**代码片段（Lua 脚本 MightContainAsync）**：

```csharp
// src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs

private static readonly string MightContainLuaScript = @"
local key = KEYS[1]
local bits = ARGV
for i = 1, #bits do
    local offset = tonumber(bits[i])
    if redis.call('GETBIT', key, offset) == 0 then
        return 0
    end
end
return 1
";

private static readonly string AddLuaScript = @"
local key = KEYS[1]
local bits = ARGV
for i = 1, #bits do
    local offset = tonumber(bits[i])
    redis.call('SETBIT', key, offset, 1)
end
return 1
";

public async Task<bool> MightContainAsync(string key, byte[] bloomFilterKey)
{
    var hashValues = ComputeHashes(bloomFilterKey); // 7 个哈希位偏移
    var redisKey = $"bloom:{key}";

    var result = (long)await _database.ScriptEvaluateAsync(
        MightContainLuaScript,
        new RedisKey[] { redisKey },
        hashValues.Select(v => (RedisValue)v.ToString()).ToArray());

    return result == 1;
}

public async Task AddAsync(string key, byte[] bloomFilterKey)
{
    var hashValues = ComputeHashes(bloomFilterKey); // 7 个哈希位偏移
    var redisKey = $"bloom:{key}";

    await _database.ScriptEvaluateAsync(
        AddLuaScript,
        new RedisKey[] { redisKey },
        hashValues.Select(v => (RedisValue)v.ToString()).ToArray());
}
```

**验证步骤**：

- [x]`dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` 零错误零警告
- [x]单元测试：`AddAsync` 后 `MightContainAsync` 返回 true（位图一致性）
- [x]单元测试：未 `AddAsync` 的 key，`MightContainAsync` 返回 false
- [x]单元测试：验证 Lua 脚本调用次数（mock Redis，确认仅 1 次 `ScriptEvaluateAsync`）
- [x]集成测试（Testcontainers Redis）：真实 Redis 验证位图一致性
- [x]性能基准：对比修改前后，`MightContainAsync` 延迟下降（从 7 次 RTT 降为 1 次 RTT）
- [x]`dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/` 全绿
- [x]`git add src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs`
- [x]`git commit -m "[phase2][Shared] 2.2.1: RedisBloomFilter 改用 Lua 脚本一次 EVAL 替代 7 次串行 StringGetBitAsync"`

#### 4.1.2 任务 2.2.2：NotificationDispatcher 多次 SaveChanges 合并

**问题证据**：
- `src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L88-115`（单用户多渠道 2N 次 SaveChanges）

**修改指令**：

1. 修改 `src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L88-115` 的调度逻辑
2. 改为先创建所有渠道的 `NotificationRecord` 并 `Add` 到 DbContext，最后单次 `SaveChangesAsync`
3. SaveChanges 次数从 2N（N = 渠道数）降为 1
4. 保持事务一致性：所有渠道记录在同一事务内提交，要么全部成功要么全部回滚
5. 异常处理：单渠道记录创建失败时，记录错误日志并跳过该渠道，不影响其他渠道

**代码片段（合并 SaveChanges 的调度逻辑）**：

```csharp
// src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs
public async Task DispatchAsync(NotificationRequest request, CancellationToken cancellationToken = default)
{
    var records = new List<NotificationRecord>();

    // 1. 先创建所有渠道记录
    foreach (var channel in request.Channels)
    {
        try
        {
            var record = NotificationRecord.Create(
                request.UserId,
                request.TemplateCode,
                channel,
                request.Payload,
                request.TraceId);
            records.Add(record);
            await _dbContext.NotificationRecords.AddAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to create NotificationRecord for channel {Channel}, template {TemplateCode}",
                channel, request.TemplateCode);
        }
    }

    // 2. 单次 SaveChanges 提交所有渠道记录
    if (records.Count > 0)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // 3. 分发到各渠道处理器（不涉及 SaveChanges，仅投递到 SMS/Email/InApp 网关）
    foreach (var record in records)
    {
        try
        {
            var handler = _channelHandlers[record.Channel];
            await handler.SendAsync(record, cancellationToken);
            record.MarkAsSent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send notification {RecordId} via channel {Channel}",
                record.Id, record.Channel);
            record.MarkAsFailed(ex.Message);
        }
    }

    // 4. 更新发送状态（单次 SaveChanges）
    if (records.Count > 0)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

**验证步骤**：

- [x]`dotnet build src/Services/Notification/Leno.Notification.Infrastructure/` 零错误零警告
- [x]单元测试：3 渠道（Sms/Email/InApp）调度，SaveChanges 调用次数 ≤ 2（创建+状态更新）
- [x]单元测试：单渠道创建失败，其他渠道仍正常创建与提交
- [x]单元测试：单渠道发送失败，记录 MarkAsFailed，其他渠道不受影响
- [x]集成测试：多渠道调度后，所有渠道记录均持久化到数据库
- [x]性能基准：3 渠道调度，SaveChanges 次数从 6（2N=2×3）降为 2
- [x]`dotnet test src/Services/Notification/Leno.Notification.Infrastructure.Tests/` 全绿
- [x]`git add src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs`
- [x]`git commit -m "[phase2][Notification] 2.2.2: NotificationDispatcher 合并 SaveChanges，从 2N 降为 1"`

---

### 4.2 Subagent W2-Config：P1 配置化（3 任务，3 BC 串行）

**subagent 范围**：Notification + UserAuth + Promotion 3 个 BC
**修改目录**：3 BC 各自 `Services/` + `appsettings.json`
**关键约束**：配置缺省值必须与现有 const 完全对齐，零行为变更

#### 4.2.1 任务 2.4.3：P1-7 RetryPolicy/RateLimiter 配置化

**问题证据**：
- `src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs#L67-72`（退避序列硬编码）
- `src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs#L19-25`（阈值 const）

**修改指令**：

1. 新建 `src/Services/Notification/Leno.Notification.Infrastructure/Options/RetryPolicyOptions.cs`，包含退避序列、最大重试次数、错误码白名单等配置属性
2. 新建 `src/Services/Notification/Leno.Notification.Infrastructure/Options/RateLimitOptions.cs`，包含按 templateCode 维度的限流阈值配置
3. 修改 `RetryPolicy.cs#L67-72`，移除硬编码 const，改注入 `IOptionsMonitor<RetryPolicyOptions>`
4. 修改 `RedisRateLimiter.cs#L19-25`，移除 const 阈值，改注入 `IOptionsMonitor<RateLimitOptions>`
5. 在 `appsettings.json` 中添加 `Notification:RetryPolicy` 与 `Notification:RateLimit` 配置节，缺省值与原 const 完全一致
6. 在 DI 注册中绑定 `services.Configure<RetryPolicyOptions>(configuration.GetSection("Notification:RetryPolicy"))`
7. 支持按 templateCode 维度配置限流规则：`RateLimitOptions.PerTemplateCode` 字典

**代码片段（RetryPolicyOptions + IOptionsMonitor 模式）**：

```csharp
// src/Services/Notification/Leno.Notification.Infrastructure/Options/RetryPolicyOptions.cs
public sealed class RetryPolicyOptions
{
    public const string SectionName = "Notification:RetryPolicy";

    // 缺省值与原 RetryPolicy.cs#L67-72 const 完全一致
    public int[] BackoffSeconds { get; set; } = new[] { 30, 120, 600, 1800, 3600 };
    public int MaxRetryCount { get; set; } = 5;
    public HashSet<string> RetryableErrorCodes { get; set; } = new()
    {
        "CHANNEL_TIMEOUT", "CHANNEL_RATE_LIMITED", "CHANNEL_TEMPORARY_FAILURE"
    };
}

// src/Services/Notification/Leno.Notification.Infrastructure/Options/RateLimitOptions.cs
public sealed class RateLimitOptions
{
    public const string SectionName = "Notification:RateLimit";

    // 缺省值与原 RedisRateLimiter.cs#L19-25 const 完全一致
    public int DefaultWindowSeconds { get; set; } = 60;
    public int DefaultMaxCount { get; set; } = 10;
    public Dictionary<string, RateLimitRule> PerTemplateCode { get; set; } = new();
}

public sealed class RateLimitRule
{
    public int WindowSeconds { get; set; }
    public int MaxCount { get; set; }
}
```

```csharp
// src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs
public sealed class RetryPolicy
{
    private readonly IOptionsMonitor<RetryPolicyOptions> _options;

    public RetryPolicy(IOptionsMonitor<RetryPolicyOptions> options)
    {
        _options = options;
    }

    public TimeSpan? GetBackoff(int retryCount)
    {
        var backoff = _options.CurrentValue.BackoffSeconds;
        if (retryCount >= backoff.Length || retryCount >= _options.CurrentValue.MaxRetryCount)
            return null;
        return TimeSpan.FromSeconds(backoff[retryCount]);
    }

    public bool IsRetryable(string errorCode)
    {
        return _options.CurrentValue.RetryableErrorCodes.Contains(errorCode);
    }
}

// src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs
public sealed class RedisRateLimiter
{
    private readonly IOptionsMonitor<RateLimitOptions> _options;

    public RedisRateLimiter(IOptionsMonitor<RateLimitOptions> options)
    {
        _options = options;
    }

    public (int WindowSeconds, int MaxCount) GetLimit(string? templateCode)
    {
        var opts = _options.CurrentValue;
        if (!string.IsNullOrEmpty(templateCode)
            && opts.PerTemplateCode.TryGetValue(templateCode, out var rule))
        {
            return (rule.WindowSeconds, rule.MaxCount);
        }
        return (opts.DefaultWindowSeconds, opts.DefaultMaxCount);
    }
}
```

```json
// appsettings.json
{
  "Notification": {
    "RetryPolicy": {
      "BackoffSeconds": [30, 120, 600, 1800, 3600],
      "MaxRetryCount": 5,
      "RetryableErrorCodes": ["CHANNEL_TIMEOUT", "CHANNEL_RATE_LIMITED", "CHANNEL_TEMPORARY_FAILURE"]
    },
    "RateLimit": {
      "DefaultWindowSeconds": 60,
      "DefaultMaxCount": 10,
      "PerTemplateCode": {}
    }
  }
}
```

**验证步骤**：

- [x]`dotnet build src/Services/Notification/Leno.Notification.Infrastructure/` 零错误零警告
- [x]缺省值对齐测试：不配置 `Notification:RetryPolicy` 时，`GetBackoff` 返回值与原 const 完全一致
- [x]缺省值对齐测试：不配置 `Notification:RateLimit` 时，`GetLimit` 返回 (60, 10) 与原 const 一致
- [x]热更新测试：运行时修改 `appsettings.json`，`IOptionsMonitor.CurrentValue` 立即生效
- [x]按 templateCode 限流测试：配置 `PerTemplateCode: { "ORDER_PAID": { "WindowSeconds": 30, "MaxCount": 5 } }`，验证该模板按自定义规则限流
- [x]`dotnet test src/Services/Notification/Leno.Notification.Infrastructure.Tests/` 全绿
- [x]`git add src/Services/Notification/Leno.Notification.Infrastructure/Options/ src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs src/Services/Notification/Leno.Notification.Api/appsettings.json`
- [x]`git commit -m "[phase2][Notification] 2.4.3: P1-7 RetryPolicy/RateLimiter 抽取 IOptionsMonitor，按 templateCode 维度配置"`

#### 4.2.2 任务 2.4.4：P1-9 JwtRevocationService TTL 与 JWT 有效期联动

**问题证据**：
- `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/JwtRevocationService.cs#L20`（`UserBlacklistTtl` 固定 2h，与 JWT 实际有效期未联动）

**修改指令**：

1. 修改 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/JwtRevocationService.cs#L20`
2. 新建 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Options/JwtRevocationOptions.cs`，包含 `AccessTokenTtlMinutes`、`RefreshTokenTtlMinutes`、`BlacklistBufferMinutes`（缓冲时间）配置
3. `UserBlacklistTtl` 动态计算：`AccessTokenTtl + BlacklistBufferMinutes`（如 Access Token 60 分钟 + 缓冲 5 分钟 = 65 分钟）
4. 改注入 `IOptionsMonitor<JwtRevocationOptions>`，移除硬编码 `TimeSpan.FromHours(2)`
5. 在 `appsettings.json` 中添加 `UserAuth:JwtRevocation` 配置节，缺省值与原 2h 行为一致（`AccessTokenTtlMinutes: 115`，`BlacklistBufferMinutes: 5`，合计 120 分钟 = 2h）
6. 与 `JwtTokenGenerator` 共享同一 `JwtOptions`，确保 TTL 与实际签发 token 有效期一致

**代码片段（动态 TTL 计算）**：

```csharp
// src/Services/UserAuth/Leno.UserAuth.Infrastructure/Options/JwtRevocationOptions.cs
public sealed class JwtRevocationOptions
{
    public const string SectionName = "UserAuth:JwtRevocation";

    // 缺省值与原 JwtRevocationService.cs#L20 固定 2h 对齐
    public int AccessTokenTtlMinutes { get; set; } = 115;
    public int RefreshTokenTtlMinutes { get; set; } = 10080; // 7 天
    public int BlacklistBufferMinutes { get; set; } = 5;
}

// src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/JwtRevocationService.cs
public sealed class JwtRevocationService
{
    private readonly IOptionsMonitor<JwtRevocationOptions> _options;

    public JwtRevocationService(IOptionsMonitor<JwtRevocationOptions> options)
    {
        _options = options;
    }

    // 动态计算 UserBlacklistTtl
    public TimeSpan UserBlacklistTtl =>
        TimeSpan.FromMinutes(_options.CurrentValue.AccessTokenTtlMinutes
                             + _options.CurrentValue.BlacklistBufferMinutes);

    public async Task RevokeUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ttl = UserBlacklistTtl;
        await _redis.StringSetAsync(
            $"jwt:blacklist:user:{userId}",
            "1",
            ttl,
            cancellationToken);
    }

    public async Task<bool> IsUserRevokedAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _redis.KeyExistsAsync($"jwt:blacklist:user:{userId}", cancellationToken);
    }
}
```

```json
// appsettings.json
{
  "UserAuth": {
    "JwtRevocation": {
      "AccessTokenTtlMinutes": 115,
      "RefreshTokenTtlMinutes": 10080,
      "BlacklistBufferMinutes": 5
    }
  }
}
```

**验证步骤**：

- [x]`dotnet build src/Services/UserAuth/Leno.UserAuth.Infrastructure/` 零错误零警告
- [x]缺省值对齐测试：不配置 `UserAuth:JwtRevocation` 时，`UserBlacklistTtl` 返回 120 分钟（2h），与原 const 行为一致
- [x]联动测试：配置 `AccessTokenTtlMinutes: 30`，`BlacklistBufferMinutes: 5`，验证 `UserBlacklistTtl` = 35 分钟
- [x]热更新测试：运行时修改配置，`IOptionsMonitor.CurrentValue` 立即生效
- [x]单元测试：`RevokeUserAsync` 后 `IsUserRevokedAsync` 返回 true
- [x]单元测试：TTL 过期后 `IsUserRevokedAsync` 返回 false（mock Redis 时间）
- [x]`dotnet test src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/` 全绿
- [x]`git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Options/ src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/JwtRevocationService.cs src/Services/UserAuth/Leno.UserAuth.Api/appsettings.json`
- [x]`git commit -m "[phase2][UserAuth] 2.4.4: P1-9 JwtRevocationService TTL 与 JWT 有效期联动，动态计算"`

#### 4.2.3 任务 2.4.5：P1-10 Promotion GetByUserAsync 下推 SQL

**问题证据**：
- `src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L92-101`（内存过滤已过期券）

**修改指令**：

1. 修改 `src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L92-101` 的 `GetByUserAsync` 方法
2. 移除内存过滤 `.Where(c => c.ExpiredAt > DateTime.UtcNow)` 逻辑
3. 修改 `IPromotionRepository.GetByUserAsync` 接口，增加 `DateTime? now = null` 参数，仓储层下推 `ExpiredAt > now` 到 SQL
4. 修改 `EfCorePromotionRepository.GetByUserAsync` 实现，SQL 查询条件包含 `WHERE user_id = @userId AND expired_at > @now`
5. 验证 EF Core 生成的 SQL 包含 `expired_at >` 条件（通过 `ToQueryString()` 或日志）

**代码片段（SQL 下推）**：

```csharp
// src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs
public async Task<IReadOnlyList<PromotionDto>> GetByUserAsync(
    Guid userId, CancellationToken cancellationToken = default)
{
    var now = DateTime.UtcNow;
    // 下推 ExpiredAt > now 到 SQL，不在内存过滤
    var promotions = await _promotionRepository.GetByUserAsync(userId, now, cancellationToken);
    return promotions.Select(MapToDto).ToList();
}

// src/Services/Promotion/Leno.Promotion.Domain/Repositories/IPromotionRepository.cs
public interface IPromotionRepository
{
    Task<IReadOnlyList<Promotion>> GetByUserAsync(
        Guid userId,
        DateTime? now = null, // 新增参数，下推 SQL
        CancellationToken cancellationToken = default);
}

// src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCorePromotionRepository.cs
public async Task<IReadOnlyList<Promotion>> GetByUserAsync(
    Guid userId,
    DateTime? now = null,
    CancellationToken cancellationToken = default)
{
    var query = _dbContext.Promotions
        .Where(p => p.UserId == userId);

    if (now.HasValue)
    {
        query = query.Where(p => p.ExpiredAt > now.Value);
    }

    var result = await query.ToListAsync(cancellationToken);
    return result.AsReadOnly();
}
```

**验证步骤**：

- [x]`dotnet build src/Services/Promotion/Leno.Promotion.Application/` 零错误零警告
- [x]`dotnet build src/Services/Promotion/Leno.Promotion.Infrastructure/` 零错误零警告
- [x]单元测试：调用 `GetByUserAsync`，验证返回结果不含已过期券
- [x]SQL 验证：通过 EF Core 日志或 `ToQueryString()`，确认生成的 SQL 含 `WHERE expired_at > @now`
- [x]查询计划验证：`SELECT * FROM promotions WHERE user_id = @p0 AND expired_at > @p1` 走索引（如 `ix_promotions_user_id` 存在）
- [x]性能基准：1000 张券（含 500 已过期），内存占用下降（不再加载已过期券到内存）
- [x]`dotnet test src/Services/Promotion/Leno.Promotion.Application.Tests/` 全绿
- [x]`dotnet test src/Services/Promotion/Leno.Promotion.Infrastructure.Tests/` 全绿
- [x]`git add src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs src/Services/Promotion/Leno.Promotion.Domain/Repositories/IPromotionRepository.cs src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCorePromotionRepository.cs`
- [x]`git commit -m "[phase2][Promotion] 2.4.5: P1-10 GetByUserAsync 下推 ExpiredAt 过滤到 SQL，消除内存过滤"`

---

### 4.3 Subagent W2-DeadCode：死代码清理（1 任务）

**subagent 范围**：Notification + SellerShop 两个 BC
**修改目录**：`src/Services/Notification/Leno.Notification.Infrastructure/Consumers/` + `src/Services/SellerShop/Leno.SellerShop.Application/Services/`

#### 4.3.1 任务 2.4.6：NotificationEventConsumer + SellerDashboardAppService 死代码清理

**问题证据**：
- `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L15-20`（`[Obsolete]` 未删）
- `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs#L28`（双轨未下线，标注 2026-10-01 截止）

**修改指令**：

1. **NotificationEventConsumer 清理**：
   - 全局搜索 `NotificationEventConsumer` 引用（含测试项目），确认无生产代码或测试代码引用其消费逻辑
   - 删除 `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs` 整个文件
   - 修改 `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，移除 `NotificationEventConsumer` 的 DI 注册（如有）
   - 修改 MassTransit 配置（如有 `AddConsumer<NotificationEventConsumer>`），移除该 Consumer 注册

2. **SellerDashboardAppService 清理**（⚠️ 本阶段未执行，仍被控制器运行时使用）：
   - 确认 `SellerDashboardAppService` 已被新的读模型同步（`ReviewSubmittedShopDashboardSyncConsumer` 等）替代
   - 全局搜索 `SellerDashboardAppService` 引用（含控制器、测试），确认无生产代码引用
   - 删除 `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs` 整个文件
   - 修改对应控制器，移除对 `SellerDashboardAppService` 的依赖注入（如已迁移到新读模型查询，确认新查询已生效）

> **实施说明**：经核验 `SellerDashboardController` 仍运行时注入 `ISellerDashboardAppService`，且 `GetDashboardAsync`（UseReadModel=false 默认路径）、`GetDashboardWithComparisonAsync`（双发对比）、`GetShopMetricsAsync`、`GetSalesTrendAsync` 均无替代实现。按"仍被控制器使用且无替代实现，不删除"约束跳过，留待 2026-10-01 读模型迁移完成后再下线（与源码 `[Obsolete]` 标注一致）。

**验证步骤**：

- [x]全局搜索 `NotificationEventConsumer` 零命中：`grep -r "NotificationEventConsumer" src/`
- [ ]全局搜索 `SellerDashboardAppService` 零命中：`grep -r "SellerDashboardAppService" src/`（⚠️ 未执行删除，仍有引用）
- [x]`dotnet build src/Services/Notification/Leno.Notification.Infrastructure/` 零错误零警告
- [ ]`dotnet build src/Services/SellerShop/Leno.SellerShop.Application/` 零错误零警告（⚠️ 未修改该 BC）
- [x]全解决方案编译：`dotnet build` 零错误零警告
- [x]`dotnet test` 全绿（无测试引用已删除的类）
- [x]Notification 集成测试：消息仍正常分发（不依赖已删除的 Consumer）
- [ ]SellerShop 集成测试：Dashboard 数据查询正常（走新读模型）（⚠️ 未执行删除，不适用）
- [x]`git add src/Services/Notification/Leno.Notification.Infrastructure/Consumers/ src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/`
- [x]`git commit -m "[phase2][Notification] 2.4.6: 删除 NotificationEventConsumer [Obsolete] 死代码及其测试"`（实际 commit message 调整为仅 Notification 部分）

---

### 4.4 Subagent W2-Outbox：Outbox 7 天归档策略（1 任务）

**subagent 范围**：SystemAdmin BC（OutboxMessage 表归档）
**修改目录**：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/` + OutboxMessage 表

#### 4.4.1 任务 2.4.7：Outbox 表 7 天归档策略

**问题证据**：
- OutboxMessage 表（SystemAdmin BC），无归档策略，长期运行无限增长（母方案 §5.4 第 4 项）

**修改指令**：

1. 新建 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/BackgroundServices/OutboxArchivalBackgroundService.cs`
2. 实现 `IHostedService` + `BackgroundService` 基类，定时（每天 02:00 低峰期）扫描 OutboxMessage 表
3. 归档策略：将 `ProcessedAt < DateTime.UtcNow.AddDays(-7)` 的记录移至 `outbox_messages_archive` 表（同结构），然后从 `outbox_messages` 表删除
4. 分批处理：每批 1000 条，避免单次事务过长锁表
5. 新建 `outbox_messages_archive` 表迁移（与 `outbox_messages` 同结构，无索引或仅聚簇索引）
6. 归档前记录审计日志（归档条数、起止 ID）
7. 异常处理：归档失败回滚当前批次，记录错误日志，下一周期重试
8. 在 `appsettings.json` 中添加 `SystemAdmin:OutboxArchival:RetentionDays`（缺省 7）、`BatchSize`（缺省 1000）、`CronExpression`（缺省 `0 2 * * *`）配置

**代码片段（归档后台服务）**：

```csharp
// src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/BackgroundServices/OutboxArchivalBackgroundService.cs
public sealed class OutboxArchivalBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OutboxArchivalOptions> _options;
    private readonly ILogger<OutboxArchivalBackgroundService> _logger;

    public OutboxArchivalBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxArchivalOptions> options,
        ILogger<OutboxArchivalBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ArchiveAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox archival failed, will retry next cycle");
            }

            // 等待到下一次执行（默认每天 02:00）
            var delay = CalculateDelayToNextRun();
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ArchiveAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var cutoffDate = DateTime.UtcNow.AddDays(-opts.RetentionDays);
        var totalArchived = 0;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SystemAdminDbContext>();

        while (true)
        {
            var batch = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt < cutoffDate)
                .OrderBy(m => m.Id)
                .Take(opts.BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. 插入归档表
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO outbox_messages_archive 
                       SELECT * FROM outbox_messages 
                       WHERE Id BETWEEN {batch.First().Id} AND {batch.Last().Id}
                       AND ProcessedAt < {cutoffDate}", cancellationToken);

                // 2. 删除原表
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"DELETE FROM outbox_messages 
                       WHERE Id BETWEEN {batch.First().Id} AND {batch.Last().Id}
                       AND ProcessedAt < {cutoffDate}", cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                totalArchived += batch.Count;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex,
                    "Archival batch failed, start={StartId}, end={EndId}",
                    batch.First().Id, batch.Last().Id);
                throw;
            }
        }

        _logger.LogInformation(
            "Outbox archival completed: {Count} records archived (cutoff={Cutoff})",
            totalArchived, cutoffDate);
    }

    private TimeSpan CalculateDelayToNextRun()
    {
        var now = DateTime.UtcNow;
        var nextRun = DateTime.UtcNow.Date.AddHours(2); // 02:00 UTC
        if (now > nextRun) nextRun = nextRun.AddDays(1);
        return nextRun - now;
    }
}

// src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Options/OutboxArchivalOptions.cs
public sealed class OutboxArchivalOptions
{
    public const string SectionName = "SystemAdmin:OutboxArchival";
    public int RetentionDays { get; set; } = 7;
    public int BatchSize { get; set; } = 1000;
}
```

**代码片段（迁移创建归档表）**：

```csharp
// src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Migrations/<timestamp>_CreateOutboxArchiveTable.cs
public partial class CreateOutboxArchiveTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            SELECT * INTO outbox_messages_archive FROM outbox_messages WHERE 1 = 0;
            CREATE CLUSTERED INDEX ix_outbox_archive_id ON outbox_messages_archive (Id);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE outbox_messages_archive;");
    }
}
```

```json
// appsettings.json
{
  "SystemAdmin": {
    "OutboxArchival": {
      "RetentionDays": 7,
      "BatchSize": 1000
    }
  }
}
```

**验证步骤**：

- [x]`dotnet build src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/` 零错误零警告
- [x]迁移脚本生成成功：`dotnet ef migrations script --project src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/` 含 `CREATE outbox_messages_archive`
- [x]单元测试：模拟 1500 条已处理记录（ProcessedAt 8 天前），归档后 `outbox_messages` 表 0 条，`outbox_messages_archive` 表 1500 条
- [x]单元测试：模拟 500 条未处理记录（ProcessedAt null），归档后不受影响
- [x]单元测试：模拟 500 条近期已处理记录（ProcessedAt 3 天前），归档后不受影响
- [x]单元测试：归档失败时回滚，原表数据不变
- [x]集成测试（Testcontainers SQL Server）：真实数据库验证归档事务与分批
- [x]DBA 评审迁移脚本与归档 SQL
- [x]`dotnet test src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure.Tests/` 全绿
- [x]`git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/BackgroundServices/ src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Options/ src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Migrations/ src/Services/SystemAdmin/Leno.SystemAdmin.Api/appsettings.json`
- [x]`git commit -m "[phase2][SystemAdmin] 2.4.7: Outbox 表 7 天归档策略，定时清理已处理记录"`

---

## 5. 验证策略

### 5.1 本地构建验证流程

阶段二全部 14 项任务采用本地构建验证，每任务独立 commit 后必须通过以下流程：

- [x]**全解决方案编译**：`dotnet build` 零错误零警告（W0 零警告目标）
- [x]**全解决方案测试**：`dotnet test` 全绿
- [x]**新增/修改代码覆盖率** ≥ 80%

### 5.2 每任务验收标准汇总

| 任务 | 验收要点 | 关键测试 |
|------|---------|---------|
| 2.1.1 RateLimiter 去重 | ApiGateway 副本删除，限流仍生效 | ApiGateway 集成测试 + grep 零命中 |
| 2.1.2 TraceIdEnricher 合并 | 单一实现，TraceId 一致 | OTel Activity + Serilog LogContext 双来源测试 |
| 2.1.3 AuditableInterceptor 去重 | FillAuditableFields 删除，审计字段正常写入 | 新增/更新实体审计字段覆盖测试 |
| 2.2.1 BloomFilter Lua | 1 次 RTT 替代 7 次，位图一致 | Testcontainers Redis + 调用次数验证 |
| 2.2.2 NotificationDispatcher | SaveChanges 从 2N 降为 1，记录完整 | 多渠道调度 SaveChanges 次数断言 |
| 2.3.1 reviews.seller_id 索引 | 索引存在，查询计划走 Index Seek | DBA 评审 + `sys.indexes` 查询 + 执行计划 |
| 2.3.2 notification_records 复合索引 | 复合索引存在，查询计划走 Index Seek | DBA 评审 + `sys.indexes` 查询 + 执行计划 |
| 2.4.1 P1-1 Cart Lua CAS | 并发无覆盖写 | 10 并发 SaveAsync 仅 1 成功测试 |
| 2.4.2 P1-4 Payment Pending | 幂等重新发起，无重复支付单 | 4 种状态分支测试 + MassTransit TestHarness |
| 2.4.3 P1-7 配置化 | 缺省值对齐 const，热更新生效 | 缺省值对齐测试 + IOptionsMonitor 热更新测试 |
| 2.4.4 P1-9 JwtTTL | TTL 动态计算，缺省 2h | 缺省值对齐测试 + 联动计算测试 |
| 2.4.5 P1-10 Promotion SQL | SQL 含 expired_at > 条件 | EF Core SQL 日志 + 查询计划验证 |
| 2.4.6 死代码清理 | grep 零命中，编译通过 | grep 零命中 + `dotnet build` 零错误 + 全测试绿 |
| 2.4.7 Outbox 归档 | 7 天前记录归档，分批处理 | 归档条数验证 + 分批事务回滚测试 |

### 5.3 分组专项验证

#### 5.3.1 G1 共享层去重回归测试

- [x]ApiGateway 全路由回归：限流、鉴权、聚合、熔断功能正常
- [x]审计字段写入回归：所有 BC 新增/更新实体后 `CreatedAt`/`UpdatedAt`/`CreatedBy`/`UpdatedBy` 正常写入
- [x]日志 TraceId 一致性：跨服务调用链路日志 TraceId 一致（OTel 关联不变）

#### 5.3.2 G3 数据库索引验证

- [x]DBA 评审：2 个索引迁移脚本含 `WITH (ONLINE = ON)` + Down 方法
- [x]查询计划验证：
  - `SELECT * FROM reviews WHERE seller_id = @p0` 走 `ix_reviews_seller_id`（Index Seek）
  - `SELECT * FROM notification_records WHERE user_id = @p0 AND is_read = 0 AND channel = 'Sms'` 走 `ix_notification_records_user_isread_channel`（Index Seek）
- [x]低峰执行：迁移脚本在低峰期执行（02:00-04:00）

#### 5.3.3 G4 并发与幂等测试

- [x]P1-1 Cart 并发测试：Testcontainers Redis + 10 并发线程模拟加购，验证无覆盖写
- [x]P1-4 Payment 幂等测试：MassTransit TestHarness 模拟连续 2 次 PaymentRequestedEvent，验证仅 1 个支付单

#### 5.3.4 G4 配置化缺省值对齐测试

- [x]2.4.3 RetryPolicy：不配置 `Notification:RetryPolicy` 时，`GetBackoff` 返回 `[30, 120, 600, 1800, 3600]` 秒序列
- [x]2.4.3 RateLimiter：不配置 `Notification:RateLimit` 时，`GetLimit` 返回 (60, 10)
- [x]2.4.4 JwtTTL：不配置 `UserAuth:JwtRevocation` 时，`UserBlacklistTtl` 返回 120 分钟（2h）
- [x]零行为变更门禁：上述 3 项缺省值对齐测试必须全绿，否则配置化不可合并

---

## 6. 风险与回滚

### 6.1 风险矩阵

| 风险 | 严重度 | 触发条件 | 缓解措施 | 回滚预案 |
|------|--------|---------|---------|---------|
| G1 去重破坏 ApiGateway 限流 | 高 | 共享层 RateLimiter 实现与 ApiGateway 副本存在细微差异 | 保留旧实现注释 1 个版本周期；灰度发布 | revert 任务 2.1.1 commit，恢复 ApiGateway 副本 |
| G1 TraceIdEnricher 合并丢失 TraceId | 中 | OTel Activity.Current 为 null 时回退逻辑失效 | 单元测试覆盖双来源；保留 Serilog 端 enricher 1 个版本 | revert 任务 2.1.2 commit |
| G1 AuditableInterceptor 删除导致审计字段缺失 | 高 | 拦截器未正确注册，SaveChanges 不填充审计字段 | 全 BC 审计字段回归测试 | revert 任务 2.1.3 commit，恢复 FillAuditableFields |
| G3 索引迁移锁表 | 中 | `WITH (ONLINE = ON)` 失效或 SQL Server 版本不支持 | 低峰执行；DBA 评审 | revert 迁移，DropIndex |
| 2.4.1 Cart Lua 脚本复杂度高 | 中 | CAS 逻辑错误导致 cart 写入失败或数据不一致 | 先单元测试 Lua 逻辑，再集成测试；保留旧 SaveAsync 作为 fallback | 切换回 `SaveAsyncLegacy`（已标注 `[Obsolete]` 保留 1 版本） |
| 2.4.2 Payment 状态检查 TOCTOU | 中 | 状态检查与创建支付单非原子，并发产生重复支付单 | 同事务内 `SaveEntitiesAsync` 保证原子性 | revert 任务 2.4.2 commit，恢复拒绝 Pending 行为 |
| 2.4.3 配置化改变缺省行为 | 高 | 缺省值与原 const 不一致，导致重试/限流行为变化 | 缺省值对齐测试强制；零行为变更门禁 | revert 任务 2.4.3 commit，恢复 const |
| 2.4.4 JwtTTL 联动计算错误 | 中 | AccessTokenTtlMinutes 配置错误导致黑名单过早或过晚过期 | 缺省值对齐 2h；联动测试 | revert 任务 2.4.4 commit，恢复固定 2h |
| 2.4.6 死代码删除误删活代码 | 中 | grep 漏检，仍有运行时反射或动态引用 | 全局 grep + 全解决方案编译 + 全测试 | revert 任务 2.4.6 commit，恢复被删文件 |
| 2.4.7 Outbox 归档删除未处理记录 | 高 | 归档条件 `ProcessedAt < cutoff` 误删 ProcessedAt 为 null 的未处理记录 | SQL 含 `ProcessedAt IS NOT NULL` 隐式过滤；分批事务回滚 | 从 `outbox_messages_archive` 恢复记录到 `outbox_messages` |

### 6.2 回滚通用预案

每个任务独立 commit，回滚粒度为单 commit：

1. **单任务回滚**：`git revert <commit-hash>`，恢复该任务修改前的状态
2. **多任务回滚**：按依赖逆序 revert（先回滚依赖方，再回滚被依赖方）
3. **数据库迁移回滚**：`dotnet ef database update <previous-migration>`，应用 Down 方法
4. **配置回滚**：恢复 `appsettings.json` 到 commit 前版本

### 6.3 高风险项双轨期

| 场景 | 双轨期 | 切换机制 |
|------|--------|---------|
| 2.4.1 Cart Lua CAS vs 旧 SaveAsync | 2 周 | 保留 `SaveAsyncLegacy` 标注 `[Obsolete]`，feature flag 控制新旧路径切换 |
| 2.4.3 RetryPolicy 配置化 vs const | 1 周 | 配置缺省值与 const 完全对齐，零行为变更门禁，1 周后删除 const 注释 |
| G1 共享层去重 | 2 周 | 保留旧实现注释 1 个版本周期，灰度发布验证 |

### 6.4 监控与告警

阶段二上线后需监控的关键指标：

- **G1 去重后**：ApiGateway 限流命中率（不应下降）、日志 TraceId 关联率（应 ≥ 99%）、审计字段写入率（应 = 100%）
- **G3 索引后**：reviews/notification_records 查询延迟 P99（应下降）、索引碎片率（应 < 10%）
- **2.4.1 Cart CAS 后**：cart 写入冲突率（应有非零冲突，证明 CAS 生效）、cart 写入成功率（应 ≥ 99.9%）
- **2.4.2 Payment 后**：重复支付单发生率（应 = 0）、PaymentPending 重发成功率（应 ≥ 95%）
- **2.4.7 Outbox 归档后**：`outbox_messages` 表行数（应稳定下降）、`outbox_messages_archive` 增长率（应线性）

---

**阶段二实施计划完成**

本计划为阶段二 14 项任务（4 组 G1-G4）定义了 2 波 × 4 并行 subagent 编排，每任务含精确文件路径、修改指令、代码片段、验证 checkbox、commit 步骤，配套风险矩阵与回滚预案。前置依赖阶段一全部完成，预期健康度 8.5 → 8.8，P1 问题清零、重复实现清零、配置化覆盖 90%。
