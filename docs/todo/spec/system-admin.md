# 系统管理域 - 缺失功能任务

> **限界上下文**: BC11 系统管理域
> **对应文档**: `12-系统管理域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

系统管理域已实现运营管理功能（操作员、审计日志、定时任务、功能开关、系统配置、系统公告、数据字典），但需求文档定义的数据看板、死信管理、索引重建、跨域审计聚合、限流配置、健康监控六大核心能力完全缺失。

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 运营数据看板（DashboardReport） | P0 关键 | 订单量/GMV/转化率总览看板 F-SYS-001 |
| 支付统计报表 | P0 关键 | 支付成功率、分渠道统计 F-SYS-002 |
| 积分统计报表 | P0 关键 | 积分发放量/消耗量/净增报表 F-SYS-003 |
| 通知送达率监控 | P0 关键 | 邮件/短信送达率与失败原因分布 F-SYS-004 |
| 售后统计报表 | P0 关键 | 售后量/退款金额/类型分布 F-SYS-005 |
| 店铺排行看板 | P0 关键 | 按销售额/订单量排行 F-SYS-006 |
| 死信队列管理（DeadLetterMessage） | P0 关键 | 跨域死信列表/重投/丢弃 F-SYS-007 |
| 索引重建管理（IndexRebuildTask） | P0 关键 | 触发/进度/补偿 F-SYS-008 |
| 跨域审计日志聚合（AuditLogEntry） | P1 重要 | 跨域只读聚合查询 F-SYS-009 |
| 接口限流配置（RateLimitRule） | P1 重要 | 各域 API 限流热生效 F-SYS-010 |
| 系统健康监控 | P1 重要 | 聚合各模块 /health 端点 F-SYS-011 |
| 统计事件消费者 | P1 重要 | 消费各域事件维护统计投影读模型 |
| 基础设施抽象实现 | P1 重要 | IDeadLetterQueueManager/IIndexRebuildTrigger/IModuleHealthProbe/IRateLimitCounter |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.SystemAdmin.Domain.Tests`、`Leno.SystemAdmin.Application.Tests`、`Leno.SystemAdmin.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目，遵循 `{BC}.{层}.Tests` 命名规范
2. 覆盖 DashboardReport 聚合（Generate、周期校验、指标非空校验）
3. 覆盖 DeadLetterMessage 聚合（Create、Retry、Discard、幂等处置）
4. 覆盖 IndexRebuildTask 聚合（Create、Start、ReportProgress、Complete、Fail、Retry）
5. 覆盖 RateLimitRule 聚合（Create、Update、Enable、Disable）
6. 覆盖统计聚合服务（IStatisticsAggregationService）
7. 覆盖 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖 IndexRebuildTask 状态机全流转
- [ ] 覆盖 DeadLetterMessage 状态机全流转
- [ ] 覆盖统计聚合（AC-SYS-001 ~ AC-SYS-006）
- [ ] 覆盖死信处置（AC-SYS-007 ~ AC-SYS-007b）
- [ ] 覆盖索引重建（AC-SYS-008 ~ AC-SYS-008c）

### 参考
- `编码规范.md` 第 13 章
- `12-系统管理域.md` 第 8 章验收标准

---

## Task 2: 运营数据看板 DashboardReport

**严重程度**: P0 关键

### 功能描述
实现 DashboardReport 聚合根与统计聚合服务，消费各域事件产出订单量、GMV、转化率等运营指标只读快照。覆盖 F-SYS-001 ~ F-SYS-006 全部统计看板。

### 技术实现路径

**2.1 DashboardReport 聚合**
1. 创建 `DashboardReport` 聚合根（ReportType、Period、Metrics、Granularity、GeneratedAt、DataVersion）
2. 工厂方法 `Generate(reportType, period, metrics, granularity)`：校验周期起止、指标非空，创建不可变快照
3. 聚合创建后无变更方法（只读快照语义），重算新建新版本 DataVersion 递增
4. 值对象：`ReportType`（OrderGmv/PaymentSuccessRate/PointsIssued/NotificationDelivery/AfterSalesVolume/ShopRanking/ConversionRate）、`ReportPeriod`（Type/Start/End）、`MetricItem`（Name/Value/Unit/Dimensions）

**2.2 IStatisticsAggregationService 领域服务**
1. `AggregateAsync(reportType, period)`：按报表类型与周期从事件投影读模型聚合指标
2. 订单量：统计 `OrderCreatedEvent` 次数
3. GMV：累计 `OrderPaidEvent` 的 paidAmount
4. 转化率：支付订单数 / 下单数 × 100%
5. 支付成功率：成功次数 / (成功 + 失败) 次数
6. 积分发放量：累计 `PointsEarnedEvent` 的 points
7. 通知送达率：成功次数 / (成功 + 失败) 次数
8. 售后量：累计 `AfterSalesApprovedEvent` + `RefundCompletedEvent`
9. 店铺排行：按 sellerId 累计 paidAmount 取 TopN

**2.3 统计事件消费者**
1. 消费以下入站事件维护统计投影读模型（时序库或 ES 聚合索引）：
   - `OrderCreatedEvent` / `OrderPaidEvent` / `OrderCancelledEvent`
   - `PaymentSucceededIntegrationEvent` / `PaymentFailedIntegrationEvent`
   - `PointsEarnedEvent`
   - `NotificationSentEvent` / `NotificationFailedEvent`
   - `RefundCompletedEvent` / `AfterSalesApprovedEvent`
   - `ShopCreatedEvent`
2. 事件消费幂等（以 EventId 去重）

**2.4 API**
1. `GET /api/admin/dashboard/overview` - 运营总览（订单量/GMV/转化率）
2. `GET /api/admin/dashboard/payment-stats` - 支付统计报表
3. `GET /api/admin/dashboard/points-stats` - 积分统计报表
4. `GET /api/admin/dashboard/notification-delivery` - 通知送达率监控
5. `GET /api/admin/dashboard/after-sales-stats` - 售后统计报表
6. `GET /api/admin/dashboard/shop-ranking` - 店铺排行看板
7. `GET /api/admin/dashboard/reports` - 报表快照列表
8. `GET /api/admin/dashboard/reports/{id}` - 报表快照详情

### 预期完成标准
- [ ] 看板聚合返回订单量/GMV/转化率（AC-SYS-001）
- [ ] 即时聚合超时降级返回历史快照（AC-SYS-001a）
- [ ] 支付成功率按渠道分桶（AC-SYS-002）
- [ ] 积分发放量统计（AC-SYS-003）
- [ ] 通知送达率按渠道与模板分桶（AC-SYS-004）
- [ ] 售后量与退款金额统计（AC-SYS-005）
- [ ] 店铺排行按销售额 TopN（AC-SYS-006）
- [ ] 看板快照不可变，重算新建新版本
- [ ] 本域不回写各域业务库（AC-SYS-013）
- [ ] 仅运营/系统管理员可访问

### 参考
- `12-系统管理域.md` F-SYS-001 ~ F-SYS-006
- `12-系统管理域.md` 第 2.1.1 节 DashboardReport 聚合根
- `12-系统管理域.md` 第 3 节领域事件清单（入站事件）
- `12-系统管理域.md` INV-SYS-02 看板快照不可变、INV-SYS-07 本域对业务数据只读

---

## Task 3: 死信队列管理 DeadLetterMessage

**严重程度**: P0 关键

### 功能描述
实现 DeadLetterMessage 聚合根，跨域汇聚各 MQ 死信队列消息，提供重投/丢弃/批量处置与历史查询。

### 技术实现路径

**3.1 DeadLetterMessage 聚合**
1. 创建 `DeadLetterMessage` 聚合根（OriginalMessageId、SourceContext、OriginalTopic、OriginalQueue、Payload、Headers、ErrorReason、FailedAt、RetryCount、Status、OperatorId、OperatedAt、DiscardReason）
2. 工厂方法 `Create(...)`：校验 OriginalMessageId/Payload 非空，置待处理态
3. `Retry(operatorId)`：仅待处理态可调用，置已重投态，RetryCount+1，发布 `DeadLetterRetriedEvent`
4. `Discard(operatorId, reason)`：仅待处理态可调用，置已丢弃态，reason 必填，发布 `DeadLetterDiscardedEvent`
5. 重投与丢弃以 OriginalMessageId 幂等

**3.2 IDeadLetterQueueManager 基础设施抽象**
1. 领域层定义接口：
   ```csharp
   public interface IDeadLetterQueueManager
   {
       Task<IReadOnlyList<DeadLetterMessage>> FetchAsync(string sourceContext, int batchSize, CancellationToken ct = default);
       Task<RetryResult> RepublishAsync(DeadLetterMessage message, CancellationToken ct = default);
   }
   ```
2. 基础设施层实现 `RabbitMqDeadLetterManager`：对接 RabbitMQ 死信队列 API

**3.3 IDeadLetterRetryService 领域服务**
1. `RetryAsync(message)`：校验待处理态 → 调用基础设施抽象重投 → 成功则 `Retry` → 失败回滚状态

**3.4 API**
1. `GET /api/admin/dead-letters` - 死信列表（按来源上下文、状态、时间筛选）
2. `GET /api/admin/dead-letters/{id}` - 死信详情
3. `POST /api/admin/dead-letters/{id}/retry` - 重投单条
4. `POST /api/admin/dead-letters/{id}/discard` - 丢弃单条（body `{discardReason}`）
5. `POST /api/admin/dead-letters/batch-retry` - 批量重投（上限 100 条）
6. `POST /api/admin/dead-letters/batch-discard` - 批量丢弃（上限 100 条）

### 预期完成标准
- [ ] 重投幂等：已重投消息重复请求返回当前状态（AC-SYS-007）
- [ ] 丢弃原因必填（AC-SYS-007a）
- [ ] 批量处置部分失败返回明细（AC-SYS-007b）
- [ ] 仅系统管理员可处置死信（AC-SYS-012）
- [ ] 仅待处理态可重投/丢弃
- [ ] 操作记录审计日志

### 参考
- `12-系统管理域.md` F-SYS-007 死信队列管理
- `12-系统管理域.md` 第 2.1.2 节 DeadLetterMessage 聚合根
- `12-系统管理域.md` 第 2.4 节基础设施抽象
- `12-系统管理域.md` 第 7.2 节死信消息状态机
- `12-系统管理域.md` INV-SYS-03 死信重投幂等

---

## Task 4: 索引重建管理 IndexRebuildTask

**严重程度**: P0 关键

### 功能描述
实现 IndexRebuildTask 聚合根，统一触发并监控各域 ES 读库全量索引重建，含进度跟踪与增量事件补偿。

### 技术实现路径

**4.1 IndexRebuildTask 聚合**
1. 创建 `IndexRebuildTask` 聚合根（TargetContext、IndexName、Status、TriggeredBy、TriggeredAt、StartedAt、FinishedAt、TotalDocs、ProcessedDocs、ErrorMessage、RetryCount）
2. 工厂方法 `Create(targetContext, indexName, triggeredBy)`：置待执行态，发布 `IndexRebuildRequestedEvent`
3. `Start(totalDocs)`：仅待执行态可调用，置执行中态
4. `ReportProgress(processedDocs)`：仅执行中态可调用，progress ≤ totalDocs
5. `Complete()`：仅执行中态可调用，置成功态，发布 `IndexRebuildCompletedEvent`
6. `Fail(reason)`：仅执行中态可调用，置失败态，发布 `IndexRebuildFailedEvent`
7. `Retry()`：仅失败态可调用，回到待执行态，RetryCount+1

**4.2 IIndexRebuildTrigger 基础设施抽象**
1. 领域层定义接口：
   ```csharp
   public interface IIndexRebuildTrigger
   {
       Task<RebuildHandle> StartAsync(string targetContext, string indexName, CancellationToken ct = default);
       Task<ProgressSnapshot> GetProgressAsync(string handle, CancellationToken ct = default);
   }
   ```
2. 基础设施层实现 `ElasticsearchRebuildTrigger`：调用各域 ES reindex 或全量同步接口

**4.3 IIndexRebuildOrchestrator 领域服务**
1. `TriggerAsync(targetContext, indexName, operatorId)`：创建任务 → 触发重建
2. `TrackProgressAsync(taskId)`：拉取进度回写
3. `ApplyCompensationAsync(taskId)`：重建期间暂存增量事件，完成后补偿回放

**4.4 API**
1. `GET /api/admin/index-rebuilds` - 重建任务列表
2. `POST /api/admin/index-rebuilds` - 触发重建（body `{targetContext, indexName}`）
3. `GET /api/admin/index-rebuilds/{id}` - 任务详情与进度
4. `POST /api/admin/index-rebuilds/{id}/retry` - 重试失败任务

### 预期完成标准
- [ ] 触发重建创建待执行任务并启动（AC-SYS-008）
- [ ] 同索引已有执行中任务返回 409（AC-SYS-008a）
- [ ] 重建期间增量事件补偿回放（AC-SYS-008b）
- [ ] 失败任务可重试（AC-SYS-008c）
- [ ] 重试次数上限 3 次
- [ ] 仅系统管理员可触发/重试
- [ ] 操作记录审计日志

### 参考
- `12-系统管理域.md` F-SYS-008 索引重建管理
- `12-系统管理域.md` 第 2.1.3 节 IndexRebuildTask 聚合根
- `12-系统管理域.md` 第 7.1 节索引重建任务状态机
- `12-系统管理域.md` INV-SYS-04 索引重建期间增量事件暂存补偿、INV-SYS-06 同索引重建串行

---

## Task 5: 跨域审计日志聚合 AuditLogEntry

**严重程度**: P1 重要

### 功能描述
实现 AuditLogEntry 只读聚合，消费各域审计事件或查询各域审计接口做跨域聚合查询，补充各域已有审计日志的跨域视角。

### 技术实现路径

**5.1 AuditLogEntry 只读聚合**
1. 创建 `AuditLogEntry` 聚合根（OperatorId、OperatorName、OperatorRole、SourceContext、Action、ResourceType、ResourceId、RequestSummary、ResponseStatus、IpAddress、UserAgent、TraceId、BeforeSnapshot、AfterSnapshot、OccurredAt）
2. 本域不写入审计日志，不暴露 Create/Update/Delete 方法，数据来源于消费各域审计事件或查询各域审计接口的投影
3. 请求摘要脱敏存储（敏感参数掩码）
4. 保留期 180 天，超期归档冷存储

**5.2 审计事件消费者**
1. 消费各域经审计中间件产生的审计事件，投影为 AuditLogEntry 只读聚合
2. 或通过调用各域内部审计查询接口拉取数据做聚合
3. 事件消费幂等

**5.3 API**
1. `GET /api/admin/audit-logs` - 审计日志聚合查询（按操作人、角色、来源上下文、操作类型、资源类型、时间范围、响应状态筛选）
2. `GET /api/admin/audit-logs/{id}` - 审计日志详情

**5.4 与现有审计日志的关系**
- BC1 用户域已有 `AuditLog`，BC11 已有 `AuditLog`（写模型）和 `OperationLog`
- 本任务新增的 `AuditLogEntry` 是跨域只读投影，不替代各域自己的审计日志
- BC1 的 `/api/admin/audit-logs` 端点已移除，审计查询统一收口至本域

### 预期完成标准
- [ ] 审计日志只读不可篡改（AC-SYS-009）
- [ ] 跨域审计日志聚合查询（AC-SYS-009a）
- [ ] 敏感参数脱敏存储与展示（AC-SYS-009b）
- [ ] 仅系统管理员可查询全部，运营可查询自身操作记录
- [ ] 日志保留期 180 天

### 参考
- `12-系统管理域.md` F-SYS-009 审计日志聚合查询
- `12-系统管理域.md` 第 2.1.4 节 AuditLogEntry 聚合根
- `12-系统管理域.md` INV-SYS-01 审计日志只读不可篡改、INV-SYS-11 敏感参数脱敏

---

## Task 6: 接口限流配置 RateLimitRule

**严重程度**: P1 重要

### 功能描述
实现 RateLimitRule 聚合根，统一配置各域 API 限流规则，变更后通过事件通知各域网关热生效。

### 技术实现路径

**6.1 RateLimitRule 聚合**
1. 创建 `RateLimitRule` 聚合根（TargetApi、TargetContext、Limit、WindowSeconds、Algorithm、Scope、Enabled、UpdatedBy、UpdatedAt）
2. 工厂方法 `Create(...)`：校验阈值/窗口 > 0、API 标识非空，发布 `RateLimitRuleUpdatedEvent`
3. `Update(limit, windowSeconds, algorithm, scope, operatorId)`：更新参数，发布事件
4. `Enable(operatorId)` / `Disable(operatorId)`：启停，发布事件
5. 值对象：`LimitAlgorithm`（SlidingWindow/TokenBucket/FixedWindow）、`LimitScope`（Ip/User/Global/Shop）

**6.2 IRateLimitPolicyResolver 领域服务**
1. `ResolveAsync(targetApi)`：依据规则解析为网关可执行的限流策略

**6.3 事件下发**
1. 规则变更后发布 `RateLimitRuleUpdatedEvent`，各域网关订阅热加载新规则
2. 下发存在秒级生效窗口
3. 网关订阅失败时沿用旧规则并告警

**6.4 API**
1. `GET /api/admin/rate-limit-rules` - 规则列表
2. `POST /api/admin/rate-limit-rules` - 新增规则
3. `GET /api/admin/rate-limit-rules/{id}` - 规则详情
4. `PUT /api/admin/rate-limit-rules/{id}` - 更新规则
5. `POST /api/admin/rate-limit-rules/{id}/enable` - 启用规则
6. `POST /api/admin/rate-limit-rules/{id}/disable` - 禁用规则

### 预期完成标准
- [ ] 限流规则变更后网关热生效（AC-SYS-010）
- [ ] 并发编辑乐观锁冲突返回 409（AC-SYS-010a）
- [ ] 仅系统管理员可配置限流规则
- [ ] 操作记录审计日志

### 参考
- `12-系统管理域.md` F-SYS-010 接口限流配置
- `12-系统管理域.md` 第 2.1.5 节 RateLimitRule 聚合根
- `12-系统管理域.md` INV-SYS-05 限流配置热生效

---

## Task 7: 系统健康监控

**严重程度**: P1 重要

### 功能描述
实现 `IHealthAggregator` 聚合各模块 /health 端点状态，产出整体健康视图。

### 技术实现路径

**7.1 IHealthAggregator 领域服务**
1. `AggregateAsync()`：并发拉取各模块 /health 端点，归一化为 ModuleHealth 集合
2. 整体状态取各模块最差状态

**7.2 IModuleHealthProbe 基础设施抽象**
1. 领域层定义接口：
   ```csharp
   public interface IModuleHealthProbe
   {
       Task<ModuleHealth> ProbeAsync(string moduleEndpoint, CancellationToken ct = default);
   }
   ```
2. 基础设施层实现 `HttpModuleHealthProbe`：HTTP 调用各模块健康端点

**7.3 ModuleHealth 值对象**
1. Module（模块名）、Status（Healthy/Degraded/Unhealthy）、Dependencies（List<DependencyHealth>）、CheckedAt

**7.4 API**
1. `GET /api/admin/health` - 聚合健康状态（整体状态 + 各模块状态）
2. `GET /api/admin/health/modules` - 各模块健康详情（含依赖项明细）

**7.5 健康检查配置**
1. 健康端点拉取超时 3s 归为不健康
2. 健康检查频率 30s
3. 不健康模块触发告警

### 预期完成标准
- [ ] 聚合各模块健康状态，整体取最差（AC-SYS-011）
- [ ] 健康端点不可达标记 Unhealthy 并告警（AC-SYS-011a）
- [ ] 仅系统管理员可查看
- [ ] 降级状态（部分依赖不健康）单独标识

### 参考
- `12-系统管理域.md` F-SYS-011 系统健康监控
- `12-系统管理域.md` 第 2.2 节领域服务（IHealthAggregator）
- `12-系统管理域.md` 第 2.4 节基础设施抽象（IModuleHealthProbe）
- `12-系统管理域.md` INV-SYS-12 健康聚合取最差

---

## Task 8: 统计数据源一致性保障

**严重程度**: P1 重要

### 功能描述
确保本域统计看板与各域域内统计的数据源一致，以事件源为唯一真实数据源，避免域内统计与跨域看板出现数值偏差。

### 技术实现路径
1. F-SYS-001~006 统计看板与各域统计使用相同的事件源
2. 本域只读消费各域集成事件做跨域聚合，各域统计基于本域事件投影
3. 统计投影读模型以事件源为准，定期对账校验
4. 对账差异记录告警并触发修正

### 预期完成标准
- [ ] 看板统计数据与各域域内统计一致
- [ ] 对账差异触发告警
- [ ] 本域不回写各域写库（AC-SYS-013）

### 参考
- `12-系统管理域.md` 第 4.0.1 节统计数据源一致性说明
- `12-系统管理域.md` INV-SYS-07 本域对业务数据只读

---

## Task 9: 基础设施抽象实现

**严重程度**: P1 重要

### 功能描述
为领域层定义的四类基础设施抽象接口提供具体实现，对接 RabbitMQ、Elasticsearch、Redis 与各模块 HTTP 端点。

### 技术实现路径

**9.1 RabbitMqDeadLetterManager**
1. 实现 `IDeadLetterQueueManager` 接口
2. 对接 RabbitMQ Management HTTP API 或死信队列消费
3. `FetchAsync`：从指定来源上下文的死信队列拉取消息
4. `RepublishAsync`：将消息重新投递到原队列

**9.2 ElasticsearchRebuildTrigger**
1. 实现 `IIndexRebuildTrigger` 接口
2. 调用各域 ES reindex API 或触发全量同步
3. `StartAsync`：发起重建并返回句柄
4. `GetProgressAsync`：通过 ES Tasks API 查询进度

**9.3 HttpModuleHealthProbe**
1. 实现 `IModuleHealthProbe` 接口
2. HTTP GET 各模块 `/health` 端点
3. 解析健康响应为 ModuleHealth 值对象

**9.4 RedisRateLimitCounter**
1. 实现 `IRateLimitCounter` 接口
2. 基于 Redis Lua 脚本原子计数
3. `TryAcquireAsync`：判断是否超限并计数

### 预期完成标准
- [ ] RabbitMqDeadLetterManager 对接 RabbitMQ 死信队列
- [ ] ElasticsearchRebuildTrigger 对接各域 ES 索引重建
- [ ] HttpModuleHealthProbe 聚合各模块健康端点
- [ ] RedisRateLimitCounter 基于 Lua 脚本原子限流计数

### 参考
- `12-系统管理域.md` 第 2.4 节基础设施抽象
- `10-模块化部署架构.md` 第 6 节故障隔离策略

---

## DDD 分层落点参考

| 分层 | 系统管理域落点 |
|------|---------------|
| 领域层 | DashboardReport/DeadLetterMessage/IndexRebuildTask/AuditLogEntry/RateLimitRule 聚合，ReportType/ReportPeriod/MetricItem/MessageStatus/RebuildStatus/LimitAlgorithm/LimitScope/ModuleHealth 值对象，IStatisticsAggregationService/IDeadLetterRetryService/IIndexRebuildOrchestrator/IHealthAggregator/IRateLimitPolicyResolver 领域服务，IDeadLetterQueueManager/IIndexRebuildTrigger/IModuleHealthProbe/IRateLimitCounter 基础设施抽象，仓储接口，领域事件 |
| 应用层 | IDashboardAppService/ISystemAdminAppService，DTO、Command/Query，各域事件消费者（订阅订单/支付/积分/通知/售后/卖家域事件维护统计投影读模型），限流规则下发协调 |
| 基础设施层 | EfCore 仓储，RabbitMqDeadLetterManager，ElasticsearchRebuildTrigger，HttpModuleHealthProbe，RedisRateLimitCounter，发件箱发布，统计投影读模型同步消费者 |
| 表现层 | DashboardController、DeadLetterController、IndexRebuildController、AuditLogController、RateLimitController、HealthController |