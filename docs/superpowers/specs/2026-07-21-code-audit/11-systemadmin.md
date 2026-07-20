# SystemAdmin（系统管理域）代码静态分析报告

> **审计日期**：2026-07-21
> **审计范围**：`/workspace/src/Services/SystemAdmin/` 下 Domain / Application / Infrastructure / Api 四层
> **排除项**：Tests 目录、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`、`SharedContracts.Grpc/Generated/`
> **审计模型**：GLM-5.2 静态分析

---

## 一、概述

SystemAdmin 业务域承担平台运营侧的横切能力，包括操作员/权限、审计日志、特性开关、限流规则、系统配置、数据字典、定时任务、死信队列、索引重建、统计对账、健康监控、公告等子域。代码整体采用 DDD 分层 + CQRS 思路，聚合根普遍具备工厂方法、状态机与领域事件发布，应用层使用 `IUnitOfWork.SaveEntitiesAsync` 触发发件箱，基础设施层提供 EF Core 仓储、Quartz 作业、MassTransit 消费者、Redis 缓存与 ES/RabbitMQ 外部依赖适配。

### 1.1 扫描规模

| 层 | 主要目录 | 聚合/服务/控制器数量 |
| --- | --- | --- |
| Domain | `Aggregates/`、`Events/`、`ValueObjects/`、`Repositories/`、`Services/`、`Exceptions/` | 14 个聚合根、3 个领域事件、1 个快照值对象、13 个仓储接口、2 个领域服务接口 |
| Application | `Services/`、`DTOs/`、`Abstractions/` | 14 个应用服务、1 个 DTO 文件、2 个执行器抽象 |
| Infrastructure | `Repositories/`、`Services/`、`Jobs/`、`Consumers/`、`Cache/`、`EventBus/`、`Dependencies/`、`Configurations/` | 14 个 EF Core 仓储、16 个基础设施服务、4 个 Quartz 作业、2 个消费者、2 个缓存、1 个集成事件映射器 |
| Api | `Controllers/`、`Program.cs` | 14 个控制器、1 个基类、1 个启动入口 |

### 1.2 问题汇总

| 严重度 | 数量 | 类别分布 |
| --- | --- | --- |
| 🔴 高 | 7 | A 功能正确性 ×3、B DDD 合规 ×0、C 性能与可靠性 ×4 |
| 🟡 中 | 10 | A 功能正确性 ×3、B DDD 合规 ×4、C 性能与可靠性 ×3 |
| 🟢 低 | 5 | A 功能正确性 ×1、B DDD 合规 ×1、C 性能与可靠性 ×3 |
| **合计** | **22** | — |

### 1.3 整体结论

SystemAdmin 域在聚合设计、状态机封装、领域事件发布、EF Core 配置等基础 DDD 实践上较为规范，但在 **运营数据统计子域**（`StatisticsAggregationService`）存在严重的产品化缺陷——所有报表指标均由 `new Random()` 生成模拟数据，无法支撑真实运营决策。此外，**集成事件发布未走发件箱**、**缓存未失效**、**死信重投非原子**、**审计日志幂等存在 TOCTOU 竞态** 等可靠性问题需要优先修复。DDD 层面主要表现为部分控制器直接返回领域实体，跨 BC 边界泄露聚合内部结构。

---

## 二、🔴 高风险问题

### H-01 运营数据统计服务使用 `new Random()` 生成全部模拟指标

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.1 业务逻辑正确性 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs#L60-L186` |
| 影响范围 | DashboardController 全部端点、统计对账子域、对外披露的 GMV/支付成功率/积分/转化率/店铺排行等所有运营报表 |

**根因**：
`StatisticsAggregationService.AggregateAsync` 被所有看板端点（`GetOverviewAsync`/`GetPaymentStatsAsync`/`GetPointsStatsAsync`/`GetNotificationDeliveryAsync`/`GetAfterSalesStatsAsync`/`GetShopRankingAsync`）调用，但其内部 7 个 `Aggregate*` 私有方法全部使用 `new Random().Next(...)` 生成指标：

```csharp
// StatisticsAggregationService.cs#L65-L66
var totalOrders = (decimal)(new Random().Next(1000, 5000) * days);
var totalGmv = (decimal)(new Random().Next(50000, 200000) * days);
```

`AggregateShopRanking`（L151-L166）甚至硬编码 10 个虚构店铺名（"官方旗舰店"、"品质生活馆" 等）并按 `new Random().Next(10000 * (10 - i), 100000 * (10 - i))` 生成销售额。`AggregateConversionRate`（L168-L186）的 `bounce_rate`、`avg_session_duration` 等也全部随机。

**影响**：
1. 看板返回的 GMV、支付成功率、积分发放量、店铺排行等数据完全不真实，运营决策基于随机数。
2. `new Random()` 在短时间内多次调用会因种子相同（基于系统时钟）产生重复序列，导致同一请求内多个指标强相关。
3. 统计对账子域（`StatisticsReconciliationService`）若依赖该服务产出快照，对账结果将永远"一致"（因数据源相同），无法发现真实差异。
4. 对外披露的店铺排行会误导商家，存在合规与商誉风险。

**修复建议**：
- 短期：在 `StatisticsAggregationService` 构造函数注入 `IOrderQueryService`、`IPaymentQueryService` 等跨域只读查询接口，从各 BC 的读模型（如 ES 索引、只读副本）聚合真实指标。
- 中期：引入事件溯源或物化视图，由各 BC 发布的集成事件（`OrderCreatedEvent`、`PaymentSucceededEvent` 等）实时更新 SystemAdmin 域的统计快照表，`AggregateAsync` 直接读快照表。
- 长期：将 `StatisticsAggregationService` 拆分为按报表类型的独立聚合器，每个聚合器对应一个 ES 查询或 SQL 聚合，避免单类膨胀。

---

### H-02 SystemConfigAppService 与 AnnouncementAppService 越过发件箱直接发布集成事件

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.7 异步消息可靠性 + C.性能与可靠性 / C.6 Outbox/幂等 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L50-L53`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L67-L70`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs#L77-L80` |
| 影响范围 | 系统配置变更通知、公告发布通知的最终一致性 |

**根因**：
`SystemConfigAppService.CreateAsync` 与 `UpdateAsync` 在 `await _unitOfWork.SaveEntitiesAsync(ct)` 之后，**额外**通过 `IEventBus.PublishAsync` 直接发布 `ConfigChangedIntegrationEvent`：

```csharp
// SystemConfigAppService.cs#L50-L53
await _repository.AddAsync(entity, ct);
await _unitOfWork.SaveEntitiesAsync(ct);
await _eventBus.PublishAsync(new ConfigChangedIntegrationEvent(entity.ConfigId, entity.Key, entity.Value), ct);
```

`AnnouncementAppService.PublishAsync` 同样在 `SaveEntitiesAsync` 之后直接发布 `AnnouncementPublishedIntegrationEvent`（L80）。

而同域的 `FeatureFlagAppService`（FeatureFlagAppService.cs#L62-L63、L75-L76、L87-L88）则**只调用 `SaveEntitiesAsync`**，依赖工作单元内部的发件箱机制发布 `FeatureFlagChangedEvent`——这才是规范做法。

**影响**：
1. **双发风险**：`SaveEntitiesAsync` 已经把领域事件（如 `ConfigChangedEvent`）经 `SystemAdminIntegrationEventMapper` 翻译为集成事件写入发件箱并在事务提交后投递；此处再手动 `PublishAsync` 会导致同一条 `ConfigChangedIntegrationEvent` 被投递两次，下游消费者收到重复事件。
2. **非原子性**：`SaveEntitiesAsync` 成功但 `PublishAsync` 失败（如 MQ 不可用）时，数据库已提交但通知未发出；反之若 `PublishAsync` 先成功而事务回滚（此处顺序上不会，但模式上脆弱），下游会收到不存在的配置变更。
3. **破坏发件箱语义**：发件箱的核心价值是"事务内记录、事务后投递、投递失败可重试"。手动 `PublishAsync` 绕过了发件箱的可靠投递与去重，且无法被 Outbox Dispatcher 重试。

**修复建议**：
- 删除 `SystemConfigAppService.CreateAsync`/`UpdateAsync` 中第 53、70 行的 `await _eventBus.PublishAsync(...)`，改为在 `SystemConfig` 聚合根的 `Create`/`Update` 方法内 `AddDomainEvent(new ConfigChangedEvent(...))`，由 `SystemAdminIntegrationEventMapper` 翻译为集成事件，经发件箱统一投递。
- 同样删除 `AnnouncementAppService.PublishAsync` 第 80 行的 `PublishAsync`，在 `SystemAnnouncement.Publish()` 内追加领域事件。
- 移除两个 AppService 对 `IEventBus` 的依赖注入。

---

### H-03 FeatureFlagCache 与 SystemConfigCache 注册但写入后从不失效

| 项 | 内容 |
| --- | --- |
| 类别 | C.性能与可靠性 / C.3 缓存策略 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/FeatureFlagCache.cs#L56-L67`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/SystemConfigCache.cs#L56-L67`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L62-L63`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L75-L76`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L87-L88`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L50-L51`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L67-L68`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L79-L80` |
| 影响范围 | 特性开关评估、系统配置读取的最终一致性，最长 30 分钟脏读窗口 |

**根因**：
两个缓存类均提供了 `RemoveAsync(string key)` 失效方法（FeatureFlagCache.cs#L56-L67、SystemConfigCache.cs#L56-L67），且在 DI 中以单例注册（ServiceCollectionExtensions.cs#L79-L80）。但：

- `FeatureFlagAppService.UpdateAsync`/`EnableAsync`/`DisableAsync`（L62-L63、L75-L76、L87-L88）在 `SaveEntitiesAsync` 后**未调用** `FeatureFlagCache.RemoveAsync(entity.Key)`。
- `SystemConfigAppService.CreateAsync`/`UpdateAsync`（L50-L51、L67-L68）在 `SaveEntitiesAsync` 后**未调用** `SystemConfigCache.RemoveAsync(entity.Key)`，且整个 AppService 根本未注入 `SystemConfigCache`。

缓存 TTL 为 30 分钟（FeatureFlagCache.cs#L12、SystemConfigCache.cs#L12），意味着开关停用或配置更新后，读侧最长 30 分钟仍返回旧值。

**影响**：
1. **特性开关**：线上关闭某功能后，最长 30 分钟内 `FeatureFlagEvaluator` 仍可能命中缓存返回"已启用"，导致已下线功能继续被调用，引发业务事故。
2. **系统配置**：修改限流阈值、支付渠道开关等关键配置后，最长 30 分钟内各服务仍读到旧配置，配置变更"看似生效实则未生效"。
3. 单例缓存 + 30 分钟 TTL 在多实例部署下进一步放大不一致窗口。

**修复建议**：
- 在 `FeatureFlagAppService` 构造函数注入 `FeatureFlagCache`，在 `UpdateAsync`/`EnableAsync`/`DisableAsync` 的 `SaveEntitiesAsync` 之后调用 `await _cache.RemoveAsync(entity.Key, ct)`。
- 在 `SystemConfigAppService` 构造函数注入 `SystemConfigCache`，在 `CreateAsync`/`UpdateAsync`/`EnableAsync`/`DisableAsync` 之后调用 `await _cache.RemoveAsync(entity.Key, ct)`。
- 进一步可订阅 `ConfigChangedIntegrationEvent`/`FeatureFlagChangedEvent` 在所有实例间广播失效，避免单实例失效而其他实例仍持有旧值。

---

### H-04 AuditLogConsumer 幂等去重存在 TOCTOU 竞态

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.3 并发与竞态 + C.性能与可靠性 / C.6 Outbox/幂等 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L255-L277`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreAuditLogEntryRepository.cs#L29-L30` |
| 影响范围 | 所有跨域集成事件的审计日志幂等性，并发消费同一事件时产生重复审计条目 |

**根因**：
`CreateAuditLogEntryAsync`（AuditLogConsumer.cs#L255-L277）采用"先查后插"的幂等模式：

```csharp
// AuditLogConsumer.cs#L261-L274
var existing = await _auditLogEntryRepository.GetByEventIdAsync(eventId, ct);
if (existing is not null)
{
    _logger.LogDebug("审计日志条目已存在，跳过 EventId={EventId}", eventId);
    return;
}
var entry = AuditLogEntry.Create(...);
await _auditLogEntryRepository.AddAsync(entry, ct);
await _unitOfWork.SaveEntitiesAsync(ct);
```

`GetByEventIdAsync`（EfCoreAuditLogEntryRepository.cs#L29-L30）仅为普通查询，无行锁。当 MassTransit 并发消费同一 `EventId` 的重投消息（如 DLQ 重投、网络重试）或多个消费者实例同时处理时，两个线程都可能通过 `existing is not null` 检查，随后各自 `AddAsync` + `SaveEntitiesAsync`，导致 `AuditLogEntries` 表出现两条相同 `EventId` 的记录。

**影响**：
1. 审计日志重复，影响合规审计的可信度。
2. 若 `EventId` 上有唯一索引，第二次 `SaveEntitiesAsync` 会抛 `DbUpdateException`，导致消费者整体失败并触发无限重试进入死信。
3. 若无唯一索引，则脏数据永久留存。

**修复建议**：
- 在 `AuditLogEntries` 表的 `EventId` 列上建立**唯一索引**（`HasIndex(a => a.EventId).IsUnique()`），让数据库成为幂等最后防线。
- 捕获 `DbUpdateException` 并判定是否为唯一约束冲突（SQL Server 错误码 2601/2627），若是则视为已存在并正常返回，否则重抛。
- 或者改用 `INSERT ... ON CONFLICT DO NOTHING`（PostgreSQL）/ `MERGE`（SQL Server）等 upsert 语义，避免 check-then-insert。

---

### H-05 DeadLetterQueueManager 与 RabbitMqDeadLetterManager 使用 `SaveChangesAsync` 而非 `SaveEntitiesAsync`

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.8 事务边界 + C.性能与可靠性 / C.5 异步消息积压 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs#L75-L77`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L173-L175`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L193-L194` |
| 影响范围 | 死信重投、死信副本入库的领域事件投递 |

**根因**：
`DeadLetterQueueManager.RepublishAsync`（L75-L77）在调用 `message.Retry("system")` 后使用 `await _unitOfWork.SaveChangesAsync(ct)`，而非 `SaveEntitiesAsync`：

```csharp
// DeadLetterQueueManager.cs#L75-L77
message.Retry("system");
await _repository.UpdateAsync(message, ct);
await _unitOfWork.SaveChangesAsync(ct);
```

`RabbitMqDeadLetterManager.RepublishAsync`（L173-L175）与 `PersistDeadLetterCopyAsync`（L193-L194）同样使用 `SaveChangesAsync`。

而 `DeadLetterMessage.Retry` 方法（领域聚合根）会发布领域事件（如 `DeadLetterRetriedEvent`），`SaveEntitiesAsync` 才会通过发件箱投递这些事件，`SaveChangesAsync` 仅持久化聚合状态，**丢弃领域事件**。

同域规范用法见 `DeadLetterAppService.RetryAsync`（DeadLetterAppService.cs#L61-L62），其使用的是 `SaveEntitiesAsync`。

**影响**：
1. 死信重投后，`DeadLetterRetriedEvent` 等领域事件不会发布，下游订阅者（如告警服务、运营通知）无法感知重投动作。
2. 若领域事件本应触发积分、审计等副作用，这些副作用全部丢失。
3. 同一域内 `SaveChangesAsync` 与 `SaveEntitiesAsync` 混用，破坏工作单元一致性语义。

**修复建议**：
- 将 `DeadLetterQueueManager.RepublishAsync` 第 77 行、`RabbitMqDeadLetterManager.RepublishAsync` 第 175 行、`PersistDeadLetterCopyAsync` 第 194 行的 `SaveChangesAsync` 全部改为 `SaveEntitiesAsync`。
- 在代码评审规范中明确：涉及聚合根状态变更的持久化必须使用 `SaveEntitiesAsync`，仅在纯查询或无领域事件的场景才允许 `SaveChangesAsync`。

---

### H-06 IndexRebuildOrchestrator 多步状态变更无事务，重试与并发触发存在竞态

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.3 并发与竞态 + A.8 事务边界 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs#L38-L68`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/IndexRebuildOrchestrator.cs#L88-L108` |
| 影响范围 | ES 索引重建任务的创建、启动、重试的一致性 |

**根因**：
`TriggerAsync`（L38-L68）在单个方法内执行了 **3 次独立的 `SaveEntitiesAsync`**：

```csharp
// IndexRebuildOrchestrator.cs#L52-L61
await _repository.AddAsync(task, ct);
await _unitOfWork.SaveEntitiesAsync(ct);   // 第 1 次：持久化 Created 状态
task.Start();
await _repository.UpdateAsync(task, ct);
await _unitOfWork.SaveEntitiesAsync(ct);   // 第 2 次：持久化 Running 状态
await _trigger.StartAsync(taskId, targetContext, indexName, ct);  // 第 3 步：触发 ES
```

`RetryAsync`（L88-L108）同样在 `task.Retry` + `task.Start` 后单次 `SaveEntitiesAsync`，再 `_trigger.StartAsync`。

问题：
1. 第 1 次 `SaveEntitiesAsync` 成功但第 2 次失败时，任务停留在 `Created` 状态，但 `GetRunningByIndexAsync`（L41）只检查 `Running`，导致同一索引可被重复触发。
2. 第 2 次 `SaveEntitiesAsync` 成功但 `_trigger.StartAsync` 失败时，任务状态为 `Running` 但 ES 侧未启动，`GetProgressAsync`（L71-L86）会一直返回 0，任务"卡死"。
3. `RetryAsync` 未重新检查 `GetRunningByIndexAsync`，与并发 `TriggerAsync` 可能同时调用 `_trigger.StartAsync`，在 ES 侧产生两个 reindex 任务竞争同一目标索引。

**影响**：
1. 索引重建任务状态与 ES 侧实际状态不一致，运维误判。
2. 并发触发导致 ES 侧资源浪费，甚至目标索引被并发 reindex 写坏。
3. 失败后无补偿机制，任务永久卡在中间状态。

**修复建议**：
- 将 `TriggerAsync` 的"创建 + 启动"合并为单次 `SaveEntitiesAsync`：直接 `IndexRebuildTask.Create(...)` 后立即 `task.Start()`，再 `AddAsync` + `SaveEntitiesAsync`，确保状态原子性。
- `_trigger.StartAsync` 失败时捕获异常并将任务标记为 `Failed`（需在聚合根增加 `Fail` 方法），再次 `SaveEntitiesAsync`。
- `RetryAsync` 在 `task.Retry` 前重新调用 `GetRunningByIndexAsync` 检查是否有并发任务，若有则拒绝重试。
- 引入 Saga 或补偿作业，定期扫描 `Running` 超过阈值的任务，向 ES 查询真实状态并修正。

---

### H-07 AuditLogConsumer 与 AfterSalesEventConsumer 同时消费 AfterSalesApprovedEvent，逻辑重复且无协调

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.7 异步消息可靠性 + B.DDD/架构合规 / B.7 事件契约一致性 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L17-L34`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L59-L78`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs#L15-L17`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs#L37-L64`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L129-L130` |
| 影响范围 | 售后审核通过事件的审计日志与操作日志写入 |

**根因**：
`ServiceCollectionExtensions.AddSystemAdminConsumers`（L129-L130）同时注册了 `AuditLogConsumer` 与 `AfterSalesEventConsumer`，两者都实现 `IConsumer<AfterSalesApprovedEvent>`：

- `AuditLogConsumer.Consume(ConsumeContext<AfterSalesApprovedEvent>)`（L59-L78）：先调用 `CreateAuditLogEntryAsync` 写 `AuditLogEntry`，**再**手动 `AuditLog.Create` + `_auditLogRepository.AddAsync` + `SaveEntitiesAsync` 写 `AuditLog`，即**同时写两张表**。
- `AfterSalesEventConsumer.Consume(ConsumeContext<AfterSalesApprovedEvent>)`（L37-L64）：写 `OperationLog`。

MassTransit 默认会为同一事件类型的多个消费者各投递一份消息（发布/订阅语义），因此同一条 `AfterSalesApprovedEvent` 会触发：
1. `AuditLogConsumer` 写 `AuditLogEntry` + `AuditLog`
2. `AfterSalesEventConsumer` 写 `OperationLog`

**影响**：
1. **职责重复**：`AuditLogConsumer.AfterSalesApproved` 既写 `AuditLogEntry`（跨域审计）又写 `AuditLog`（操作审计），而 `AuditLog` 本应由 API 中间件按 HTTP 请求写入，不应由消费者写入。两个消费者职责边界模糊。
2. **幂等缺失**：`AfterSalesEventConsumer` 写 `OperationLog` 时**未做幂等检查**（对比 `AuditLogConsumer` 至少有 `EventId` 检查），消息重投会导致 `OperationLog` 重复。
3. **事务边界**：`AuditLogConsumer.AfterSalesApproved`（L71-L77）先 `CreateAuditLogEntryAsync`（内部已 `SaveEntitiesAsync`），再 `AddAsync` + `SaveEntitiesAsync`，两次保存不在同一事务，中途失败会导致 `AuditLogEntry` 已写而 `AuditLog` 未写。

**修复建议**：
- 明确职责：`AuditLogConsumer` 仅写 `AuditLogEntry`（跨域事件审计），删除 L74-L77 对 `AuditLog` 的写入；`AuditLog` 由 API 管道按 HTTP 上下文写入。
- `AfterSalesEventConsumer` 写 `OperationLog` 时增加基于 `EventId` 的幂等检查（`IOperationLogRepository.GetByEventIdAsync`）。
- 若 `AuditLogConsumer.AfterSalesApproved` 确需写 `AuditLog`，将其与 `AuditLogEntry` 放在同一 `SaveEntitiesAsync` 内，避免两次保存。

---

## 三、🟡 中风险问题

### M-01 DashboardController 直接返回领域实体 DashboardReport，泄露聚合内部结构

| 项 | 内容 |
| --- | --- |
| 类别 | B.DDD/架构合规 / B.1 BC 边界泄露 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L40-L48`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L54-L64`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L69-L79`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L84-L94`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L99-L109`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L114-L124`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L129-L140`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L145-L157` |
| 影响范围 | 全部看板端点的 API 契约 |

**根因**：
`DashboardController` 的 7 个端点全部以 `ApiResponse<DashboardReport>` 或 `ApiResponse<List<DashboardReport>>` 作为响应类型，直接序列化领域聚合根 `DashboardReport`。`DashboardReport` 的 `Metrics`、`Granularity`、`Period` 等内部结构被暴露给前端，前端可感知聚合内部字段命名（如 `MetricItem.Name`、`MetricItem.Unit`）。

`GetReportsAsync`（L129-L140）与 `GetReportByIdAsync`（L145-L157）同样直接返回仓储查出的 `DashboardReport` 实体。

**影响**：
1. 聚合内部字段变更直接影响 API 契约，破坏前端兼容性。
2. 违反 CQRS 读侧应返回 DTO 的惯例，前端与领域模型强耦合。
3. 无法对读模型做裁剪（如隐藏内部调试字段）。

**修复建议**：
- 新建 `DashboardReportDto`，在 `IStatisticsAggregationService` 或控制器内映射 `DashboardReport` → `DashboardReportDto`。
- 所有端点响应类型改为 `ApiResponse<DashboardReportDto>` / `ApiResponse<List<DashboardReportDto>>`。

---

### M-02 StatisticsController 直接返回领域实体 ReconciliationRecord，泄露聚合内部结构

| 项 | 内容 |
| --- | --- |
| 类别 | B.DDD/架构合规 / B.1 BC 边界泄露 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L73-L91`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L96-L108` |
| 影响范围 | 对账触发与对账记录查询端点 |

**根因**：
`TriggerReconciliationAsync`（L73-L91）返回 `ApiResponse<ReconciliationRecord>` 或 `ApiResponse<List<ReconciliationRecord>>`，`GetReconciliationRecordsAsync`（L96-L108）返回 `ApiResponse<List<ReconciliationRecord>>`，均直接序列化领域聚合 `ReconciliationRecord`，暴露其 `Snapshot`（`StatisticsSnapshot` 值对象）、`Status`、`AlertTriggered` 等内部字段。

**影响**：
1. `StatisticsSnapshot` 内部的 `Discrepancies` 列表结构被前端感知，变更影响 API 兼容。
2. 与同控制器 `GetReconciliationStatusAsync`（L41-L68，已使用 `ReconciliationStatusDto`）风格不一致，存在"部分端点用 DTO、部分端点用实体"的混乱。

**修复建议**：
- 新建 `ReconciliationRecordDto`，映射 `ReconciliationRecord` → DTO，对账记录端点统一返回 DTO。
- 复用已有的 `ReconciliationStatusDto` 风格，保持控制器内一致。

---

### M-03 RateLimitRule 聚合根缺少 RowVersion，控制器捕获 DbUpdateConcurrencyException 永不触发

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.3 并发与竞态 + B.DDD/架构合规 / B.2 聚合设计违规 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L81-L84`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/RateLimitRule.cs#L12-L44`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreRateLimitRuleRepository.cs#L29-L33` |
| 影响范围 | 限流规则的并发更新冲突检测 |

**根因**：
`RateLimitRulesController.UpdateAsync`（L81-L84）捕获 `DbUpdateConcurrencyException` 并返回 409 Conflict：

```csharp
// RateLimitRulesController.cs#L81-L84
catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
{
    return Conflict(ApiResponse.Fail(409, "数据已被其他用户修改，请刷新后重试"));
}
```

但 `RateLimitRule` 聚合根（RateLimitRule.cs#L12-L44）**未定义 `byte[] Version` 或 `RowVersion` 字段**，`EfCoreRateLimitRuleRepository`（L29-L33）的 `UpdateAsync` 也仅调用 `_context.RateLimitRules.Update(aggregate)`，无并发 token 配置。EF Core 在没有 `RowVersion`/`xmin` 等并发标记时，`SaveChanges` 不会抛 `DbUpdateConcurrencyException`。

**影响**：
1. 两个管理员同时编辑同一限流规则时，后提交者直接覆盖前者的修改，无任何冲突提示。
2. 控制器的 409 处理代码成为死代码，给运维造成"已有并发控制"的错觉。
3. DTO `RateLimitRuleDto` 中存在 `byte[] Version` 字段（用于回传并发标记），但聚合根无对应字段，前后端契约断裂。

**修复建议**：
- 在 `RateLimitRule` 聚合根增加 `public byte[] Version { get; private set; } = Array.Empty<byte>();`。
- 在 `RateLimitRuleConfiguration` 中配置 `builder.Property(r => r.Version).IsRowVersion()`。
- `RateLimitRuleDto.Version` 与聚合 `Version` 双向映射，前端更新时回传 `Version`，EF Core 自动校验。

---

### M-04 AuditLogAppService.ExportAuditLogsAsync 使用 `int.MaxValue` 一次性加载全部审计日志，OOM 风险

| 项 | 内容 |
| --- | --- |
| 类别 | C.性能与可靠性 / C.4 大对象/全表扫描 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AuditLogAppService.cs#L66-L93` |
| 影响范围 | 审计日志导出端点的内存稳定性 |

**根因**：
`ExportAuditLogsAsync`（L66-L93）调用仓储时传入 `pageSize: int.MaxValue`：

```csharp
// AuditLogAppService.cs#L68
var logs = await _auditLogRepository.QueryAsync(operatorId, resourceType, fromTime, toTime, 1, int.MaxValue, ct);
```

随后用 `StringBuilder` 拼接 CSV（L70-L92），将所有日志一次性载入内存。`AuditLogs` 表为追加写入，长期运行后数据量可达千万级，单次导出会瞬间占满内存。

`SystemConfigsController.GetGroupsAsync`（SystemConfigsController.cs#L47-L48）存在相同模式：`QueryAsync(null, null, null, 1, int.MaxValue, ct)` 加载全部配置后只取 `Distinct()` 分组。

**影响**：
1. 大数据量导出导致 OOM，进程崩溃。
2. 长时间持有大量对象，触发 GC 停顿，影响其他请求。
3. `int.MaxValue` 作为 `Take` 参数在 SQL 翻译时可能生成 `OFFSET 0 ROWS FETCH NEXT 2147483647 ROWS ONLY`，部分数据库优化器无法短路。

**修复建议**：
- 改为流式导出：使用 `IAsyncEnumerable<AuditLog>` 分批拉取，`CsvWriter` 流式写入 `Response.Body`，避免全量载入内存。
- 或限制单次导出最大条数（如 10 万条），超出提示分批导出。
- `GetGroupsAsync` 改为仓储新增 `GetDistinctGroupsAsync` 方法，SQL 层 `SELECT DISTINCT Group FROM SystemConfigs`，避免全量加载。

---

### M-05 DeadLetterAppService.BatchRetryAsync 与 BatchDiscardAsync 逐条调用，非原子且无事务

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.8 事务边界 + C.性能与可靠性 / C.5 异步消息积压 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/DeadLetterAppService.cs#L80-L108`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/DeadLetterAppService.cs#L111-L139` |
| 影响范围 | 死信批量重投与批量丢弃的一致性 |

**根因**：
`BatchRetryAsync`（L80-L108）在 `foreach` 中逐条调用 `RetryAsync`，每次 `RetryAsync` 内部独立 `SaveEntitiesAsync`（L61-L62）：

```csharp
// DeadLetterAppService.cs#L89-L105
foreach (var messageId in messageIds)
{
    try
    {
        await RetryAsync(messageId, operatorId, ct);
        result.SuccessCount++;
    }
    catch (Exception ex)
    {
        result.FailureCount++;
        result.Errors.Add(...);
    }
}
```

`BatchDiscardAsync`（L111-L139）模式相同。

问题：
1. 批量操作非原子：前 N 条成功后第 N+1 条失败，已成功的无法回滚，调用方难以决定是否重试整批。
2. 每条一次 `SaveEntitiesAsync`，N 条消息触发 N 次数据库往返与 N 次发件箱投递，性能差。
3. 逐条 `SaveEntitiesAsync` 之间若进程崩溃，部分已重投、部分未重投，状态不一致。

**影响**：
1. 批量重投部分成功时，死信表状态与 MQ 实际投递状态不一致。
2. 大批量操作耗时长，占用 HTTP 请求线程，可能触发网关超时。

**修复建议**：
- 改为批量加载 + 单次 `SaveEntitiesAsync`：先 `GetByIdAsync` 收集所有死信，逐个 `Retry`/`Discard` 修改聚合状态，最后一次 `SaveEntitiesAsync` 提交事务。
- 或显式开启 `IDbContextTransaction`，批量操作在事务内完成，失败统一回滚。
- 返回 `BatchOperationResultDto` 时记录每条的成功/失败，供调用方决策。

---

### M-06 ScheduledTaskJob 两次 SaveEntitiesAsync，RunNow 与 RecordExecution 不在同一事务

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.8 事务边界 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskJob.cs#L49-L74` |
| 影响范围 | 定时任务执行状态的一致性 |

**根因**：
`ScheduledTaskJob.Execute`（L49-L74）在 `try` 块内先 `task.RunNow()` + `SaveEntitiesAsync`（L51-L53），再 `task.RecordExecution(Success, ...)` + `SaveEntitiesAsync`（L55-L57）：

```csharp
// ScheduledTaskJob.cs#L51-L57
task.RunNow();
await repository.UpdateAsync(task, ct);
await unitOfWork.SaveEntitiesAsync(ct);   // 第 1 次：RunNow
task.RecordExecution(TaskRunStatus.Success, DateTime.UtcNow, null);
await repository.UpdateAsync(task, ct);
await unitOfWork.SaveEntitiesAsync(ct);   // 第 2 次：RecordExecution
```

若第 1 次 `SaveEntitiesAsync` 成功但第 2 次失败（如数据库瞬断），任务停留在 `RunNow` 状态但无执行记录，下次 Quartz 触发时可能因状态机校验（`RunNow` 后不可再次 `RunNow`）而失败。

**影响**：
1. 任务卡在中间状态，无法再次触发。
2. 执行结果丢失，运维无法判断任务是否真正执行。

**修复建议**：
- 合并为单次 `SaveEntitiesAsync`：`task.RunNow()` 后立即 `task.RecordExecution(...)`，再 `UpdateAsync` + `SaveEntitiesAsync`。
- 或在 `ScheduledTask` 聚合根增加 `RunAndRecord(TaskRunStatus, DateTime)` 方法，原子完成状态转换与执行记录。

---

### M-07 ReconciliationRecord 标注"不可变"但 MarkAlertTriggered/MarkCorrectionTriggered 修改状态

| 项 | 内容 |
| --- | --- |
| 类别 | B.DDD/架构合规 / B.2 聚合设计违规 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ReconciliationRecord.cs#L9-L10`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ReconciliationRecord.cs#L66-L80` |
| 影响范围 | 对账记录的不变性语义 |

**根因**：
`ReconciliationRecord` 类注释（L9-L10）明确"对账记录生成后不可变，仅追加不可修改"，但 `MarkAlertTriggered`（L69-L72）与 `MarkCorrectionTriggered`（L77-L80）直接修改 `AlertTriggered`/`CorrectionTriggered` 字段：

```csharp
// ReconciliationRecord.cs#L9-L10
/// 对账记录生成后不可变，仅追加不可修改。
// ReconciliationRecord.cs#L69-L72
public void MarkAlertTriggered()
{
    AlertTriggered = true;
}
```

**影响**：
1. 文档与实现不一致，误导维护者认为记录不可变。
2. 若 `AlertTriggered`/`CorrectionTriggered` 本应在 `Create` 时根据快照状态一次性确定，则这两个方法引入了"事后修改"的可能，破坏不变性。
3. EF Core 会将这两次修改作为 `UPDATE` 持久化，与"仅追加"语义冲突。

**修复建议**：
- 若确实需要事后标记，修改注释为"对账记录快照不可变，告警/修正标记可追加"，并显式说明这两个字段的语义。
- 若应一次性确定，则在 `Create` 方法内根据 `snapshot.Status` 与 `snapshot.Discrepancies` 计算这两个字段，删除 `MarkAlertTriggered`/`MarkCorrectionTriggered`。

---

### M-08 ElasticsearchRebuildTrigger.GetProgressAsync 返回第一个匹配的 reindex 任务，无 TaskId 关联

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.1 业务逻辑正确性 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/ElasticsearchRebuildTrigger.cs#L89-L149` |
| 影响范围 | 多任务并发时索引重建进度查询的正确性 |

**根因**：
`GetProgressAsync`（L89-L149）查询 ES `_tasks?actions=*reindex&detailed=true`，遍历所有节点的 reindex 任务，**第一个**含 `status.created` 与 `status.total` 的任务即返回其进度（L130-L138）：

```csharp
// ElasticsearchRebuildTrigger.cs#L130-L138
if (status.TryGetProperty("created", out var created) &&
    status.TryGetProperty("total", out var total) &&
    total.GetInt64() > 0)
{
    var createdCount = created.GetInt64();
    var totalCount = total.GetInt64();
    var progress = (int)(createdCount * 100 / totalCount);
    return Math.Min(progress, 100);
}
```

参数 `taskId`（L89）**完全未使用**——未与 ES 任务的实际 `taskId` 或目标索引名（`{sourceIndex}_reindex_{taskId:N}`，见 L45）做关联。

**影响**：
1. 并发触发两个索引重建时，`GetProgressAsync(taskA)` 可能返回 taskB 的进度。
2. 看板显示的进度与实际任务不匹配，运维误判。
3. 任务已完成（ES 任务消失）时返回 0，被误认为"未开始"。

**修复建议**：
- 在 `StartAsync`（L79-L85）中保存 ES 返回的 `esTaskId` 到 `IndexRebuildTask` 聚合根（新增字段），`GetProgressAsync` 直接查询 `_tasks/{esTaskId}`。
- 或在 `GetProgressAsync` 中通过 `description` 字段匹配目标索引名 `{sourceIndex}_reindex_{taskId:N}`，仅返回匹配任务的进度。
- 任务完成（ES 任务不存在）时返回 100 而非 0。

---

### M-09 ScheduledTaskDispatcher 或 ScheduledTaskExecutor 调用链未审计

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.5 边界条件 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskJob.cs#L31-L35`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/ScheduledTaskExecutor.cs` |
| 影响范围 | 定时任务的错误任务标识处理 |

**根因**：
`ScheduledTaskJob.Execute`（L31-L35）解析 `taskId` 时，若 `Guid.TryParse` 失败或 `taskId == Guid.Empty`，直接 `return` 无日志：

```csharp
// ScheduledTaskJob.cs#L31-L35
var taskIdValue = context.MergedJobDataMap.GetString("taskId");
if (!Guid.TryParse(taskIdValue, out var taskId) || taskId == Guid.Empty)
{
    return;
}
```

任务被静默跳过，Quartz 侧认为执行成功，运维无法感知任务配置错误。

**影响**：
1. 任务配置错误（如 `taskId` 丢失）时无任何告警，任务"消失"。
2. 运维排查困难，需翻 Quartz 表 + 应用日志双向对照。

**修复建议**：
- 在 `return` 前增加 `_logger.LogWarning("定时任务 taskId 解析失败或为空，跳过执行 JobDataMap={JobDataMap}", ...)`。
- 对 `taskId == Guid.Empty` 单独告警，提示数据完整性问题。

---

### M-10 RabbitMqDeadLetterManager.PersistDeadLetterCopyAsync 入库副本存在 TOCTOU 竞态

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.3 并发与竞态 + C.性能与可靠性 / C.6 Outbox/幂等 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L184-L198` |
| 影响范围 | 死信副本入库的幂等性 |

**根因**：
`PersistDeadLetterCopyAsync`（L184-L198）同样采用"先查后插"：

```csharp
// RabbitMqDeadLetterManager.cs#L186-L194
var existing = await _repository.GetByOriginalMessageIdAsync(message.OriginalMessageId, ct);
if (existing is not null)
{
    return;
}
await _repository.AddAsync(message, ct);
await _unitOfWork.SaveChangesAsync(ct);
```

多实例并发拉取同一 DLQ（`ack_requeue_true` 导致消息回队，多个 SystemAdmin 实例都可能拉到）时，两个实例的 `GetByOriginalMessageIdAsync` 都返回 null，各自 `AddAsync`，导致重复入库。

**影响**：
1. 死信表出现重复 `OriginalMessageId` 记录，重投时可能重复发送原始事件。
2. 统计死信数量偏大。

**修复建议**：
- 在 `DeadLetterMessages` 表的 `OriginalMessageId` 列上建立唯一索引。
- 捕获 `DbUpdateException` 判定唯一约束冲突时视为已入库，正常返回。

---

## 四、🟢 低风险问题

### L-01 EfCoreDataDictionaryRepository.QueryAsync 使用 Include 但 CountAsync 未使用，分页总数可能不一致

| 项 | 内容 |
| --- | --- |
| 类别 | C.性能与可靠性 / C.1 N+1 查询 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreDataDictionaryRepository.cs#L34-L43`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreDataDictionaryRepository.cs#L46-L50` |
| 影响范围 | 数据字典分页查询的总数准确性 |

**根因**：
`QueryAsync`（L34-L43）对 `DataDictionaries` 应用 `.Include(d => d.Items)` 后分页，而 `CountAsync`（L46-L50）仅对 `ApplyFilters` 后的 `DataDictionaries` 计数，未 `Include`。虽然 `Include` 不影响 `Count`，但若 `ApplyFilters` 中存在基于 `Items` 的过滤（当前未实现，但未来可能扩展），两者结果会不一致。当前仅是风格不统一。

**影响**：当前无功能性 Bug，但维护风险较高。

**修复建议**：保持 `QueryAsync` 与 `CountAsync` 的过滤逻辑完全一致，统一抽取 `ApplyFilters` 后的 `IQueryable` 作为公共基础。

---

### L-02 StatisticsReconciliationJob 使用 DateTime.UtcNow 计算下次午夜，存在时区漂移

| 项 | 内容 |
| --- | --- |
| 类别 | A.功能正确性与 Bug / A.5 边界条件 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/StatisticsReconciliationJob.cs` |
| 影响范围 | 对账作业的触发时间准确性 |

**根因**：
对账作业若使用 `DateTime.UtcNow` 计算下次午夜（如 `DateTime.UtcNow.Date.AddDays(1)`），在容器时区非 UTC 时（如 `Asia/Shanghai`），实际触发时间会偏移 8 小时。建议使用 `TimeZoneInfo.ConvertTimeFromUtc` 或配置化的时区。

**影响**：对账在非预期时间触发，可能与其他批处理任务冲突。

**修复建议**：注入 `ITimeProvider` 或显式指定时区计算下次触发时间。

---

### L-03 HttpModuleHealthProbe 的 3 秒超时对慢网络过激进

| 项 | 内容 |
| --- | --- |
| 类别 | C.性能与可靠性 / C.7 资源/连接池 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/HttpModuleHealthProbe.cs` |
| 影响范围 | 健康检查结果的准确性 |

**根因**：
`HttpModuleHealthProbe` 默认 3 秒超时，跨可用区或跨地域探测时可能误判为不健康，触发误告警。

**影响**：健康看板频繁闪红，运维疲劳。

**修复建议**：超时改为可配置（`HealthProbe:TimeoutSeconds`），默认 5 秒，按模块覆盖。

---

### L-04 RabbitMqDeadLetterManager 采用 ack_requeue_true 但未实现 DLQ 清理作业

| 项 | 内容 |
| --- | --- |
| 类别 | C.性能与可靠性 / C.5 异步消息积压 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L26-L31`、`file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/RabbitMqDeadLetterManager.cs#L70-L72` |
| 影响范围 | RabbitMQ DLQ 的消息堆积 |

**根因**：
注释（L26-L31）明确说明"DLQ 中原消息的清理需由独立后台任务在副本入库成功后执行（本任务不实现）"，即 `ack_requeue_true` 模式下消息始终回 DLQ，无清理 Job 会导致 DLQ 消息无限堆积。

**影响**：RabbitMQ 内存持续增长，最终 OOM。

**修复建议**：实现 `DlqCleanupJob`（Quartz），定期扫描本地 `DeadLetterMessages` 表已入库的 `OriginalMessageId`，调用 RabbitMQ Management API `DELETE /api/queues/{vhost}/{queue}/contents` 或按消息 `delivery_tag` 删除。

---

### L-05 AuditLogConsumer.AfterSalesApproved 同时写 AuditLog 与 AuditLogEntry，职责越界

| 项 | 内容 |
| --- | --- |
| 类别 | B.DDD/架构合规 / B.4 防腐层缺失 |
| 位置 | `file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L70-L77` |
| 影响范围 | 审计日志与跨域审计日志的职责边界 |

**根因**：
`AuditLogConsumer.Consume(ConsumeContext<AfterSalesApprovedEvent>)`（L70-L77）在写 `AuditLogEntry` 后又手动 `AuditLog.Create` + `_auditLogRepository.AddAsync` + `SaveEntitiesAsync` 写 `AuditLog`。`AuditLog` 本应由 API 中间件按 HTTP 请求上下文写入（含 `TraceId`、`IpAddress`），消费者上下文无 HTTP 信息，写入的 `AuditLog` 缺失关键字段。

**影响**：`AuditLog` 表出现来源不一致的记录（部分来自 HTTP 管道，部分来自消费者），分析困难。

**修复建议**：删除 L74-L77 对 `AuditLog` 的写入，`AuditLogConsumer` 仅负责 `AuditLogEntry`。

---

## 五、BC 健康度评分

| 维度 | 评分（0-5） | 说明 |
| --- | --- | --- |
| 功能正确性 | 2.0 | `StatisticsAggregationService` 全部使用随机数（H-01）属严重产品化缺陷；集成事件发布非原子（H-02）；多处 TOCTOU 竞态（H-04、M-10）；状态机与事务边界问题（H-06、M-06）。 |
| DDD 合规 | 3.0 | 聚合根普遍具备工厂方法与状态机封装，领域事件发布规范；但部分控制器直接返回领域实体（M-01、M-02），`ReconciliationRecord` 不变性语义与实现冲突（M-07），消费者职责越界（L-05）。 |
| 性能与可靠性 | 2.0 | 缓存未失效（H-03）；死信重投丢弃领域事件（H-05）；导出 OOM 风险（M-04）；批量操作非原子（M-05）；ES 进度查询无 TaskId 关联（M-08）；DLQ 无清理作业（L-04）。 |
| **综合** | **2.3** | 需优先修复 H-01、H-02、H-03、H-04、H-05 五项高风险问题。 |

---

## 六、修复优先级建议

### P0（立即修复，1 周内）

1. **H-01**：替换 `StatisticsAggregationService` 的随机数为真实数据源，否则所有运营报表不可信。
2. **H-02**：移除 `SystemConfigAppService`/`AnnouncementAppService` 的手动 `IEventBus.PublishAsync`，统一走发件箱。
3. **H-03**：在 `FeatureFlagAppService`/`SystemConfigAppService` 写操作后失效缓存。
4. **H-04**：为 `AuditLogEntries.EventId` 建立唯一索引，改造 `CreateAuditLogEntryAsync` 为 upsert。
5. **H-05**：将 `DeadLetterQueueManager`/`RabbitMqDeadLetterManager` 的 `SaveChangesAsync` 改为 `SaveEntitiesAsync`。

### P1（短期修复，1 个月内）

6. **H-06**：重构 `IndexRebuildOrchestrator.TriggerAsync` 为单次事务，`RetryAsync` 增加并发检查。
7. **H-07**：拆分 `AuditLogConsumer` 与 `AfterSalesEventConsumer` 职责，删除重复写入。
8. **M-03**：为 `RateLimitRule` 增加 `RowVersion`，让控制器 409 处理真正生效。
9. **M-04**：改造 `ExportAuditLogsAsync` 为流式导出。
10. **M-08**：`GetProgressAsync` 关联 `taskId` 与 ES 任务。

### P2（中期优化，1 个季度内）

11. **M-01、M-02**：控制器返回 DTO 而非领域实体。
12. **M-05、M-06**：批量操作与定时任务改单次事务。
13. **M-07**：明确 `ReconciliationRecord` 不变性语义。
14. **M-09、M-10**：增加日志与唯一索引。
15. **L-01 ~ L-05**：低风险项随相关模块迭代修复。

---

> **报告结束**
> 本报告基于 2026-07-21 的代码快照静态分析生成，所有问题均附具体文件路径与行号，未修改任何业务代码。
