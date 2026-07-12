# 系统管理域 - 任务执行计划

> **模块**: BC11 系统管理域
> **对应文档**: `12-系统管理域.md`
> **任务 ID 前缀**: SYS
> **总任务数**: 9 | **P0**: 4 | **P1**: 5 | **P2**: 0

---

## 模块概述

系统管理域负责运营数据看板、死信管理、索引重建、跨域审计、限流配置与健康监控。已实现运营管理功能（操作员、审计日志、定时任务、功能开关、系统配置、公告、数据字典），但数据看板、死信管理、索引重建、跨域审计聚合、限流配置、健康监控六大核心能力完全缺失。

---

## Task SYS-01: 测试项目创建 [P0]

### 子任务 Checklist

- [ ] SYS-01.1: 创建 `Leno.SystemAdmin.Domain.Tests` 项目
- [ ] SYS-01.2: 创建 `Leno.SystemAdmin.Application.Tests` 项目
- [ ] SYS-01.3: 创建 `Leno.SystemAdmin.Api.Tests` 项目
- [ ] SYS-01.4: 覆盖 DashboardReport 聚合（Generate、周期校验、指标非空校验）
- [ ] SYS-01.5: 覆盖 DeadLetterMessage 聚合（Create、Retry、Discard）
- [ ] SYS-01.6: 覆盖 IndexRebuildTask 聚合（Create、Start、ReportProgress、Complete、Fail、Retry）
- [ ] SYS-01.7: 覆盖 RateLimitRule 聚合（Create、Update、Enable、Disable）
- [ ] SYS-01.8: 覆盖 IStatisticsAggregationService
- [ ] SYS-01.9: 配置测试覆盖率 ≥ 80%

### 验收标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖 IndexRebuildTask/DeadLetterMessage 状态机全流转
- [ ] 覆盖统计聚合（AC-SYS-001~006）
- [ ] 覆盖死信处置（AC-SYS-007~007b）
- [ ] 覆盖索引重建（AC-SYS-008~008c）

---

## Task SYS-02: 运营数据看板 DashboardReport [P0]

### 子任务 Checklist

- [ ] SYS-02.1: 创建 `DashboardReport` 聚合根（ReportType、Period、Metrics、Granularity、GeneratedAt、DataVersion）
- [ ] SYS-02.2: 创建值对象：`ReportType`（OrderGmv/PaymentSuccessRate/PointsIssued/NotificationDelivery/AfterSalesVolume/ShopRanking/ConversionRate）、`ReportPeriod`、`MetricItem`
- [ ] SYS-02.3: 实现 `IStatisticsAggregationService.AggregateAsync(reportType, period)`
- [ ] SYS-02.4: 在基础设施层创建 `StatisticsEventConsumer` 消费 12 类入站事件
- [ ] SYS-02.5: 维护统计投影读模型（时序库或 ES 聚合索引）
- [ ] SYS-02.6: 实现 8 个看板 API 端点（overview/payment-stats/points-stats/notification-delivery/after-sales-stats/shop-ranking/reports/reports/{id}）
- [ ] SYS-02.7: 即时聚合超时降级返回历史快照（AC-SYS-001a）
- [ ] SYS-02.8: 看板快照不可变，重算新建新版本
- [ ] SYS-02.9: 本域不回写各域业务库（AC-SYS-013）

### 验收标准
- [ ] 看板聚合返回订单量/GMV/转化率（AC-SYS-001）
- [ ] 支付成功率按渠道分桶（AC-SYS-002）
- [ ] 积分发放量统计（AC-SYS-003）
- [ ] 通知送达率按渠道与模板分桶（AC-SYS-004）
- [ ] 售后量与退款金额统计（AC-SYS-005）
- [ ] 店铺排行按销售额 TopN（AC-SYS-006）

---

## Task SYS-03: 死信队列管理 DeadLetterMessage [P0]

### 子任务 Checklist

- [ ] SYS-03.1: 创建 `DeadLetterMessage` 聚合根（OriginalMessageId、SourceContext、OriginalTopic、Payload、Headers、ErrorReason、Status）
- [ ] SYS-03.2: 实现工厂方法 `Create` 与 `Retry(operatorId)`、`Discard(operatorId, reason)` 方法
- [ ] SYS-03.3: 在领域层定义 `IDeadLetterQueueManager` 接口（`FetchAsync`、`RepublishAsync`）
- [ ] SYS-03.4: 在基础设施层实现 `RabbitMqDeadLetterManager`（对接 RabbitMQ Management HTTP API）
- [ ] SYS-03.5: 实现 `IDeadLetterRetryService.RetryAsync(message)` 领域服务
- [ ] SYS-03.6: 实现 6 个死信管理 API 端点（列表/详情/重投/丢弃/批量重投/批量丢弃）
- [ ] SYS-03.7: 重投幂等：已重投消息重复请求返回当前状态（AC-SYS-007）
- [ ] SYS-03.8: 丢弃原因必填（AC-SYS-007a）
- [ ] SYS-03.9: 仅系统管理员可处置死信（AC-SYS-012）

### 验收标准
- [ ] 重投幂等
- [ ] 丢弃原因必填
- [ ] 批量处置部分失败返回明细（AC-SYS-007b）

---

## Task SYS-04: 索引重建管理 IndexRebuildTask [P0]

### 子任务 Checklist

- [ ] SYS-04.1: 创建 `IndexRebuildTask` 聚合根（TargetContext、IndexName、Status、TriggeredBy、Progress、ErrorMessage）
- [ ] SYS-04.2: 实现完整状态机：Create → Start → ReportProgress → Complete/Fail → Retry
- [ ] SYS-04.3: 在领域层定义 `IIndexRebuildTrigger` 接口（`StartAsync`、`GetProgressAsync`）
- [ ] SYS-04.4: 在基础设施层实现 `ElasticsearchRebuildTrigger`（调用各域 ES reindex API）
- [ ] SYS-04.5: 实现 `IIndexRebuildOrchestrator` 领域服务（触发+进度跟踪+补偿回放）
- [ ] SYS-04.6: 实现 4 个索引重建 API 端点（列表/触发/详情/重试）
- [ ] SYS-04.7: 同索引已有执行中任务返回 409（AC-SYS-008a）
- [ ] SYS-04.8: 重建期间增量事件补偿回放（AC-SYS-008b）
- [ ] SYS-04.9: 重试次数上限 3 次

### 验收标准
- [ ] 触发重建创建待执行任务并启动（AC-SYS-008）
- [ ] 同索引已有执行中任务返回 409
- [ ] 重建期间增量事件补偿回放
- [ ] 失败任务可重试（AC-SYS-008c）

---

## Task SYS-05: 跨域审计日志聚合 AuditLogEntry [P1]

### 子任务 Checklist

- [ ] SYS-05.1: 创建 `AuditLogEntry` 只读聚合根（OperatorId、SourceContext、Action、ResourceType、ResourceId、RequestSummary、ResponseStatus、IpAddress、TraceId、OccurredAt）
- [ ] SYS-05.2: 数据来源于消费各域审计事件或查询各域审计接口的投影
- [ ] SYS-05.3: 在基础设施层创建 `AuditLogConsumer` 消费各域审计事件
- [ ] SYS-05.4: 请求摘要脱敏存储（敏感参数掩码）
- [ ] SYS-05.5: 实现 `GET /api/admin/audit-logs` - 聚合查询（多维度筛选）
- [ ] SYS-05.6: 实现 `GET /api/admin/audit-logs/{id}` - 详情
- [ ] SYS-05.7: 保留期 180 天，超期归档冷存储
- [ ] SYS-05.8: 仅系统管理员可查询全部，运营可查询自身操作记录

### 验收标准
- [ ] 审计日志只读不可篡改（AC-SYS-009）
- [ ] 跨域审计日志聚合查询（AC-SYS-009a）
- [ ] 敏感参数脱敏存储与展示（AC-SYS-009b）

---

## Task SYS-06: 接口限流配置 RateLimitRule [P1]

### 子任务 Checklist

- [ ] SYS-06.1: 创建 `RateLimitRule` 聚合根（TargetApi、TargetContext、Limit、WindowSeconds、Algorithm、Scope、Enabled）
- [ ] SYS-06.2: 创建值对象：`LimitAlgorithm`（SlidingWindow/TokenBucket/FixedWindow）、`LimitScope`（Ip/User/Global/Shop）
- [ ] SYS-06.3: 实现 `IRateLimitPolicyResolver.ResolveAsync(targetApi)` 领域服务
- [ ] SYS-06.4: 规则变更后发布 `RateLimitRuleUpdatedEvent`，各域网关订阅热加载
- [ ] SYS-06.5: 实现 6 个限流规则 API 端点（列表/新增/详情/更新/启用/禁用）
- [ ] SYS-06.6: 并发编辑乐观锁冲突返回 409（AC-SYS-010a）
- [ ] SYS-06.7: 限流规则变更后网关热生效（AC-SYS-010）

### 验收标准
- [ ] 限流规则变更后网关热生效
- [ ] 并发编辑乐观锁冲突返回 409
- [ ] 仅系统管理员可配置限流规则

---

## Task SYS-07: 系统健康监控 [P1]

### 子任务 Checklist

- [ ] SYS-07.1: 在领域层定义 `IModuleHealthProbe.ProbeAsync(moduleEndpoint)` 接口
- [ ] SYS-07.2: 在基础设施层实现 `HttpModuleHealthProbe`（HTTP 调用各模块 `/health` 端点）
- [ ] SYS-07.3: 实现 `IHealthAggregator.AggregateAsync()` 领域服务
- [ ] SYS-07.4: 创建 `ModuleHealth` 值对象（Module、Status、Dependencies、CheckedAt）
- [ ] SYS-07.5: 整体状态取各模块最差状态
- [ ] SYS-07.6: 实现 `GET /api/admin/health` - 聚合健康状态
- [ ] SYS-07.7: 实现 `GET /api/admin/health/modules` - 各模块健康详情
- [ ] SYS-07.8: 健康端点拉取超时 3s 归为 Unhealthy 并告警（AC-SYS-011a）

### 验收标准
- [ ] 聚合各模块健康状态，整体取最差（AC-SYS-011）
- [ ] 健康端点不可达标记 Unhealthy 并告警
- [ ] 降级状态单独标识

---

## Task SYS-08: 统计数据源一致性保障 [P1]

### 子任务 Checklist

- [ ] SYS-08.1: F-SYS-001~006 统计看板与各域统计使用相同的事件源
- [ ] SYS-08.2: 本域只读消费各域集成事件做跨域聚合
- [ ] SYS-08.3: 统计投影读模型以事件源为准
- [ ] SYS-08.4: 实现定期对账校验任务（每日凌晨执行）
- [ ] SYS-08.5: 对账差异记录告警并触发修正
- [ ] SYS-08.6: 本域不回写各域写库（AC-SYS-013）

### 验收标准
- [ ] 看板统计数据与各域域内统计一致
- [ ] 对账差异触发告警
- [ ] 本域不回写各域业务库

---

## Task SYS-09: 基础设施抽象实现 [P1]

### 子任务 Checklist

- [ ] SYS-09.1: 实现 `RabbitMqDeadLetterManager`（对接 RabbitMQ Management HTTP API 拉取死信）
- [ ] SYS-09.2: 实现 `ElasticsearchRebuildTrigger`（调用各域 ES reindex API + Tasks API 查进度）
- [ ] SYS-09.3: 实现 `HttpModuleHealthProbe`（HTTP GET 各模块 `/health` 端点）
- [ ] SYS-09.4: 实现 `RedisRateLimitCounter`（基于 Redis Lua 脚本原子计数）
- [ ] SYS-09.5: 编写各基础设施抽象集成测试

### 验收标准
- [ ] RabbitMqDeadLetterManager 对接 RabbitMQ 死信队列
- [ ] ElasticsearchRebuildTrigger 对接各域 ES 索引重建
- [ ] HttpModuleHealthProbe 聚合各模块健康端点
- [ ] RedisRateLimitCounter 基于 Lua 脚本原子限流计数