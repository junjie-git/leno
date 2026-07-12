# P0 任务完成 - 任务列表

> **执行模式**: Master Agent 全流程自主编排
> **任务选择**: 按优先级 P0 + 依赖关系顺序
> **总任务数**: 11 | **预计批次**: 5 批

---

## 第一批：独立可并行执行

### Task 1: PROMO-02 秒杀 Redis 库存预占

- [x] 1.1: 活动激活时，从 DB 加载秒杀库存写入 Redis Hash（`seckill:{activityId}:stock`，field=skuId）
- [x] 1.2: 实现 Lua 脚本：`HGET stock` → 判库存 > 0 → `HINCRBY -1` → 返回 1
- [x] 1.3: 库存为 0 时返回 0（已售罄）
- [x] 1.4: 扣减成功后发布 `SeckillOrderCreatedEvent`（userId、skuId、seckillPrice、quantity、activityId）
- [x] 1.5: 实现 `POST /api/seckill/{activityId}/order` 秒杀下单端点
- [x] 1.6: 活动结束时回写剩余库存到 DB
- [x] 1.7: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 1.8: 编写应用层单元测试
- [x] 1.9: 运行全部测试并确保通过（125 passed）

### Task 2: PM-02 消费返积分

- [x] 2.1: 在基础设施层创建 `OrderEventConsumer` 消费者
- [x] 2.2: 消费 `OrderAfterSalesWindowClosedEvent`（携带 PaidAmount）
- [x] 2.3: 按比例计算积分（1 元 = 1 积分）和成长值（1 元 = 1 成长值）
- [x] 2.4: 调用 `PointsAccount.EarnPoints(points, source: "消费返积分", orderId)`
- [x] 2.5: 调用 `Member.AddGrowthValue(growthValue)`
- [x] 2.6: 发布 `PointsEarnedEvent`（points、source、orderId）
- [x] 2.7: 幂等消费以 EventId 去重（Redis 去重）
- [x] 2.8: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 2.9: 编写应用层单元测试
- [x] 2.10: 运行全部测试并确保通过（117 passed）

---

## 第二批：支付域并行

### Task 3: PAY-02 微信支付 SDK 对接

- [x] 3.1: 添加微信支付 SDK NuGet 包
- [x] 3.2: 创建 `WeChatPayChannel` 实现 `IPaymentChannel` 接口
- [x] 3.3: 实现 `CreatePaymentAsync` - 统一下单（JSAPI/Native/H5），返回支付参数/二维码
- [x] 3.4: 实现 `QueryPaymentAsync` - 查询订单状态
- [x] 3.5: 实现 `ClosePaymentAsync` - 关闭订单
- [x] 3.6: 实现 `CreateRefundAsync` - 申请退款
- [x] 3.7: 实现 `QueryRefundAsync` - 查询退款状态
- [x] 3.8: 微信支付参数配置（AppId、MchId、ApiV3Key、PrivateKey、NotifyUrl）
- [x] 3.9: 敏感参数从环境变量/配置中心读取，不落代码仓库
- [x] 3.10: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 3.11: 编写应用层单元测试
- [x] 3.12: 运行全部测试并确保通过（104 passed）

### Task 4: PAY-03 支付宝 SDK 对接

- [x] 4.1: 添加 `AlipaySDKNet` NuGet 包
- [x] 4.2: 创建 `AlipayChannel` 实现 `IPaymentChannel` 接口
- [x] 4.3: 实现 `CreatePaymentAsync` - 创建支付（page.pay/wap.pay/app.pay）
- [x] 4.4: 实现 `QueryPaymentAsync` - 查询订单
- [x] 4.5: 实现 `ClosePaymentAsync` - 关闭订单
- [x] 4.6: 实现 `CreateRefundAsync` - 申请退款
- [x] 4.7: 实现 `QueryRefundAsync` - 查询退款
- [x] 4.8: 支付宝参数配置（AppId、PrivateKey、AlipayPublicKey、NotifyUrl）
- [x] 4.9: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 4.10: 编写应用层单元测试
- [x] 4.11: 运行全部测试并确保通过（117 passed）

---

## 第三批：通知域顺序执行（NTF-02 → NTF-03 → NTF-04 → NTF-05）

### Task 5: NTF-02 领域模型对齐重构

- [x] 5.1: NotificationTemplate 字段调整：`EventType` → `Code`、`TitleTemplate` → `Subject`、`ContentTemplate` → `Body`
- [x] 5.2: NotificationTemplate 新增字段：`Name`、`SmsTemplateCode`、`Description`、`OperatorId`
- [x] 5.3: `Variables` 从 `List<string>` 改为 `List<TemplateVariable>`（Name/Required/Description）
- [x] 5.4: 新增方法：`AddVariable(variable)`、`RemoveVariable(name)`、`ContainsPlaceholder(name)`
- [x] 5.5: NotificationRecord 字段调整：`EventType` → `TemplateCode`、`FailReason` → `ErrorMessage` + `ErrorCode`
- [x] 5.6: NotificationRecord 新增字段：`ContentSnapshot`、`ChannelMessageId`、`ChannelReceipt`、`MaxRetry`、`NextRetryAt`、`SentAt`、`FailedAt`、`BusinessRef`、`IdempotencyKey`
- [x] 5.7: 状态机重构：4 态（Pending/Sent/Failed/Abandoned）→ 6 态（Pending/Sending/Succeeded/Failed/Retried/DeadLettered）
- [x] 5.8: 新增值对象：`Recipient`、`TemplateVariable`、`ChannelSendRequest`、`ChannelSendResult`、`NotificationRequest`
- [x] 5.9: EF Core 迁移脚本（数据迁移+状态映射）
- [x] 5.10: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 5.11: 运行全部测试并确保通过（140 passed）

### Task 6: NTF-03 INotificationService 统一发送入口

- [x] 6.1: 在领域层定义 `NotificationRequest` 命令对象
- [x] 6.2: 在应用层实现 `INotificationService.SendAsync(NotificationRequest)` 接口
- [x] 6.3: 发送链路：查模板 → 渲染 → 频率校验 → 创建 NotificationRecord → 选渠道发送 → 回写状态
- [x] 6.4: 同步调用超时阈值 3s，超时返回"已受理转异步"
- [x] 6.5: 幂等键（IdempotencyKey）去重：重复请求返回已存在记录
- [x] 6.6: 实现 `POST /api/notifications/send` 端点（内部服务间调用，API Key 鉴权）
- [x] 6.7: 模板禁用时拦截返回错误
- [x] 6.8: 模板缺失降级跳过发送并记录告警
- [x] 6.9: 编写应用层单元测试（覆盖率 ≥ 70%）
- [x] 6.10: 运行全部测试并确保通过（154 passed）

### Task 7: NTF-04 渠道适配器重构

- [x] 7.1: 在领域层定义 `INotificationChannel` 接口（`Channel` 属性 + `SendAsync` 方法）
- [x] 7.2: 在领域层定义 `ChannelSendRequest` / `ChannelSendResult` 记录类型
- [x] 7.3: 实现 `SmtpEmailChannel`（基于 **MailKit**，SMTP 连接池复用）
- [x] 7.4: 实现 `AliyunSmsChannel`（基于阿里云短信 SDK）
- [x] 7.5: 实现 `TencentSmsChannel`（基于腾讯云短信 SDK）
- [x] 7.6: 配置结构：EmailChannelOptions、SmsChannelOptions
- [x] 7.7: 替换现有 `EmailChannel`/`SmsChannel`/`InAppChannel` 为规约命名
- [x] 7.8: SMTP 连接超时 10s，短信 API 超时转异步重试
- [x] 7.9: 敏感参数加密存储
- [x] 7.10: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 7.11: 运行全部测试并确保通过（154 passed）

### Task 8: NTF-05 事件消费者映射

- [x] 8.1: 在基础设施层创建 `NotificationEventConsumer` 消费 12 类入站事件
- [x] 8.2: 事件→模板映射表配置化（`EventTemplateMapping`）：UserRegisteredEvent→user_registered_welcome 等
- [x] 8.3: 变量补全：部分变量需回调上游查询接口（如订单金额）
- [x] 8.4: 事件消费幂等（以 EventId 去重）
- [x] 8.5: 变量补全失败时记录并跳过，不阻塞队列
- [x] 8.6: 主交易不等待通知完成
- [x] 8.7: 事件字段缺失导致无法渲染时记录失败并发布 NotificationFailedEvent
- [x] 8.8: 编写应用层单元测试（覆盖率 ≥ 70%）
- [x] 8.9: 运行全部测试并确保通过（186 passed）

---

## 第四批：系统管理域并行

### Task 9: SYS-02 运营数据看板 DashboardReport

- [x] 9.1: 创建 `DashboardReport` 聚合根（ReportType、Period、Metrics、Granularity、GeneratedAt、DataVersion）
- [x] 9.2: 创建值对象：`ReportType`、`ReportPeriod`、`MetricItem`
- [x] 9.3: 实现 `IStatisticsAggregationService.AggregateAsync(reportType, period)`
- [x] 9.4: 在基础设施层创建 `StatisticsEventConsumer` 消费 12 类入站事件
- [x] 9.5: 维护统计投影读模型（时序库或 ES 聚合索引）
- [x] 9.6: 实现 8 个看板 API 端点（overview/payment-stats/points-stats/notification-delivery/after-sales-stats/shop-ranking/reports/reports/{id}）
- [x] 9.7: 即时聚合超时降级返回历史快照
- [x] 9.8: 看板快照不可变，重算新建新版本
- [x] 9.9: 本域不回写各域业务库
- [x] 9.10: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 9.11: 编写应用层单元测试
- [x] 9.12: 运行全部测试并确保通过（360/361 passed）

### Task 10: SYS-03 死信队列管理 DeadLetterMessage

- [x] 10.1: 创建 `DeadLetterMessage` 聚合根（OriginalMessageId、SourceContext、OriginalTopic、Payload、Headers、ErrorReason、Status）
- [x] 10.2: 实现工厂方法 `Create` 与 `Retry(operatorId)`、`Discard(operatorId, reason)` 方法
- [x] 10.3: 在领域层定义 `IDeadLetterQueueManager` 接口（`FetchAsync`、`RepublishAsync`）
- [x] 10.4: 在基础设施层实现 `RabbitMqDeadLetterManager`（对接 RabbitMQ Management HTTP API）
- [x] 10.5: 实现 `IDeadLetterRetryService.RetryAsync(message)` 领域服务
- [x] 10.6: 实现 6 个死信管理 API 端点（列表/详情/重投/丢弃/批量重投/批量丢弃）
- [x] 10.7: 重投幂等：已重投消息重复请求返回当前状态
- [x] 10.8: 丢弃原因必填
- [x] 10.9: 仅系统管理员可处置死信
- [x] 10.10: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 10.11: 编写应用层单元测试
- [x] 10.12: 运行全部测试并确保通过（361 passed）

### Task 11: SYS-04 索引重建管理 IndexRebuildTask

- [x] 11.1: 创建 `IndexRebuildTask` 聚合根（TargetContext、IndexName、Status、TriggeredBy、Progress、ErrorMessage）
- [x] 11.2: 实现完整状态机：Create → Start → ReportProgress → Complete/Fail → Retry
- [x] 11.3: 在领域层定义 `IIndexRebuildTrigger` 接口（`StartAsync`、`GetProgressAsync`）
- [x] 11.4: 在基础设施层实现 `ElasticsearchRebuildTrigger`（调用各域 ES reindex API）
- [x] 11.5: 实现 `IIndexRebuildOrchestrator` 领域服务（触发+进度跟踪+补偿回放）
- [x] 11.6: 实现 4 个索引重建 API 端点（列表/触发/详情/重试）
- [x] 11.7: 同索引已有执行中任务返回 409
- [x] 11.8: 重建期间增量事件补偿回放
- [x] 11.9: 重试次数上限 3 次
- [x] 11.10: 编写领域层单元测试（覆盖率 ≥ 80%）
- [x] 11.11: 编写应用层单元测试
- [x] 11.12: 运行全部测试并确保通过（361 passed）

---

## 第五批：进度更新与总结

### Task 12: 进度更新与报告生成

- [x] 12.1: 更新 `progress.md` 中所有已完成任务状态
- [x] 12.2: 更新各模块完成率统计
- [x] 12.3: 生成阶段性成果报告
- [x] 12.4: 提交代码（每个任务独立 commit）

---

# Task Dependencies

- **Task 1 (PROMO-02)** 和 **Task 2 (PM-02)** 可并行执行（第一批）
- **Task 3 (PAY-02)** 和 **Task 4 (PAY-03)** 可并行执行（第二批）
- **Task 5 (NTF-02)** → **Task 6 (NTF-03)** → **Task 7 (NTF-04)** → **Task 8 (NTF-05)** 顺序依赖（第三批）
- **Task 9 (SYS-02)**、**Task 10 (SYS-03)**、**Task 11 (SYS-04)** 可并行执行（第四批）
- **Task 12** 依赖所有任务完成（第五批）
- 第一批和第二批之间无依赖，可全部并行启动
- 第四批与第三批之间无依赖，可并行启动
