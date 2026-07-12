# P0 任务完成 - 质量检查清单

## PROMO-02: 秒杀 Redis 库存预占
- [ ] PROMO-02.1: 活动激活时库存从 DB 正确加载到 Redis Hash
- [ ] PROMO-02.2: Lua 脚本原子扣减逻辑正确（HGET→判库存→HINCRBY -1）
- [ ] PROMO-02.3: 库存为 0 时正确返回 0（已售罄），不超卖
- [ ] PROMO-02.4: 扣减成功后正确发布 SeckillOrderCreatedEvent
- [ ] PROMO-02.5: POST /api/seckill/{activityId}/order 端点正常工作
- [ ] PROMO-02.6: 活动结束时库存正确回写 DB
- [ ] PROMO-02.7: 领域层测试覆盖率 ≥ 80%
- [ ] PROMO-02.8: 所有测试通过（单元测试 100% 通过率）

## PM-02: 消费返积分
- [ ] PM-02.1: OrderEventConsumer 正确消费 OrderAfterSalesWindowClosedEvent
- [ ] PM-02.2: 积分计算正确（1元=1积分）
- [ ] PM-02.3: 成长值计算正确（1元=1成长值）
- [ ] PM-02.4: PointsAccount.EarnPoints 正确调用
- [ ] PM-02.5: MemberLevel.AddGrowthValue 正确调用
- [ ] PM-02.6: PointsEarnedEvent 正确发布
- [ ] PM-02.7: 事件消费幂等（EventId 去重）
- [ ] PM-02.8: 领域层测试覆盖率 ≥ 80%
- [ ] PM-02.9: 所有测试通过（单元测试 100% 通过率）

## PAY-02: 微信支付 SDK 对接
- [ ] PAY-02.1: WeChatPayChannel 正确实现 IPaymentChannel 接口
- [ ] PAY-02.2: CreatePaymentAsync 统一下单逻辑正确
- [ ] PAY-02.3: QueryPaymentAsync 查询逻辑正确
- [ ] PAY-02.4: ClosePaymentAsync 关闭逻辑正确
- [ ] PAY-02.5: CreateRefundAsync 退款逻辑正确
- [ ] PAY-02.6: QueryRefundAsync 退款查询逻辑正确
- [ ] PAY-02.7: 敏感参数从配置读取，不硬编码
- [ ] PAY-02.8: 领域层测试覆盖率 ≥ 80%
- [ ] PAY-02.9: 所有测试通过（单元测试 100% 通过率）

## PAY-03: 支付宝 SDK 对接
- [ ] PAY-03.1: AlipayChannel 正确实现 IPaymentChannel 接口
- [ ] PAY-03.2: CreatePaymentAsync 创建支付逻辑正确
- [ ] PAY-03.3: QueryPaymentAsync 查询逻辑正确
- [ ] PAY-03.4: ClosePaymentAsync 关闭逻辑正确
- [ ] PAY-03.5: CreateRefundAsync 退款逻辑正确
- [ ] PAY-03.6: QueryRefundAsync 退款查询逻辑正确
- [ ] PAY-03.7: 敏感参数从配置读取，不硬编码
- [ ] PAY-03.8: 领域层测试覆盖率 ≥ 80%
- [ ] PAY-03.9: 所有测试通过（单元测试 100% 通过率）

## NTF-02: 领域模型对齐重构
- [ ] NTF-02.1: NotificationTemplate 字段对齐（Code/Subject/Body/Name/SmsTemplateCode/Description/OperatorId）
- [ ] NTF-02.2: Variables 改为 List<TemplateVariable>
- [ ] NTF-02.3: AddVariable/RemoveVariable/ContainsPlaceholder 方法正确
- [ ] NTF-02.4: NotificationRecord 字段对齐（TemplateCode/ErrorMessage/ErrorCode/ContentSnapshot 等）
- [ ] NTF-02.5: 状态机 6 态完整流转（Pending/Sending/Succeeded/Failed/Retried/DeadLettered）
- [ ] NTF-02.6: 值对象（Recipient/TemplateVariable/ChannelSendRequest/ChannelSendResult/NotificationRequest）定义正确
- [ ] NTF-02.7: EF Core 迁移脚本正确
- [ ] NTF-02.8: 领域层测试覆盖率 ≥ 80%
- [ ] NTF-02.9: 所有测试通过（单元测试 100% 通过率）

## NTF-03: INotificationService 统一发送入口
- [ ] NTF-03.1: NotificationRequest 命令对象定义正确
- [ ] NTF-03.2: SendAsync 发送链路完整（查模板→渲染→频率校验→创建记录→发送→回写）
- [ ] NTF-03.3: 同步调用 3s 超时转异步
- [ ] NTF-03.4: IdempotencyKey 去重正确
- [ ] NTF-03.5: POST /api/notifications/send 端点正常工作
- [ ] NTF-03.6: 模板禁用时正确拦截
- [ ] NTF-03.7: 模板缺失时正确降级
- [ ] NTF-03.8: 应用层测试覆盖率 ≥ 70%
- [ ] NTF-03.9: 所有测试通过（单元测试 100% 通过率）

## NTF-04: 渠道适配器重构
- [ ] NTF-04.1: INotificationChannel 接口定义正确
- [ ] NTF-04.2: ChannelSendRequest/ChannelSendResult 记录类型正确
- [ ] NTF-04.3: SmtpEmailChannel 基于 MailKit 实现
- [ ] NTF-04.4: AliyunSmsChannel 基于阿里云 SDK 实现
- [ ] NTF-04.5: TencentSmsChannel 基于腾讯云 SDK 实现
- [ ] NTF-04.6: 敏感参数加密存储
- [ ] NTF-04.7: 旧适配器替换为规约命名
- [ ] NTF-04.8: 领域层测试覆盖率 ≥ 80%
- [ ] NTF-04.9: 所有测试通过（单元测试 100% 通过率）

## NTF-05: 事件消费者映射
- [ ] NTF-05.1: NotificationEventConsumer 消费 12 类入站事件
- [ ] NTF-05.2: EventTemplateMapping 映射表配置正确
- [ ] NTF-05.3: 变量补全逻辑正确
- [ ] NTF-05.4: 事件消费幂等（EventId 去重）
- [ ] NTF-05.5: 变量补全失败不阻塞队列
- [ ] NTF-05.6: 主交易不等待通知完成
- [ ] NTF-05.7: 字段缺失时正确发布 NotificationFailedEvent
- [ ] NTF-05.8: 应用层测试覆盖率 ≥ 70%
- [ ] NTF-05.9: 所有测试通过（单元测试 100% 通过率）

## SYS-02: 运营数据看板 DashboardReport
- [ ] SYS-02.1: DashboardReport 聚合根定义正确
- [ ] SYS-02.2: ReportType/ReportPeriod/MetricItem 值对象正确
- [ ] SYS-02.3: IStatisticsAggregationService 实现正确
- [ ] SYS-02.4: StatisticsEventConsumer 消费 12 类入站事件
- [ ] SYS-02.5: 统计投影读模型正确
- [ ] SYS-02.6: 8 个看板 API 端点正常工作
- [ ] SYS-02.7: 即时聚合超时降级返回历史快照
- [ ] SYS-02.8: 本域不回写各域业务库
- [ ] SYS-02.9: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-02.10: 所有测试通过（单元测试 100% 通过率）

## SYS-03: 死信队列管理 DeadLetterMessage
- [ ] SYS-03.1: DeadLetterMessage 聚合根定义正确
- [ ] SYS-03.2: Create/Retry/Discard 方法正确
- [ ] SYS-03.3: IDeadLetterQueueManager 接口定义正确
- [ ] SYS-03.4: RabbitMqDeadLetterManager 实现正确
- [ ] SYS-03.5: IDeadLetterRetryService 实现正确
- [ ] SYS-03.6: 6 个死信管理 API 端点正常工作
- [ ] SYS-03.7: 重投幂等
- [ ] SYS-03.8: 丢弃原因必填
- [ ] SYS-03.9: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-03.10: 所有测试通过（单元测试 100% 通过率）

## SYS-04: 索引重建管理 IndexRebuildTask
- [ ] SYS-04.1: IndexRebuildTask 聚合根定义正确
- [ ] SYS-04.2: 状态机完整流转（Create→Start→ReportProgress→Complete/Fail→Retry）
- [ ] SYS-04.3: IIndexRebuildTrigger 接口定义正确
- [ ] SYS-04.4: ElasticsearchRebuildTrigger 实现正确
- [ ] SYS-04.5: IIndexRebuildOrchestrator 实现正确
- [ ] SYS-04.6: 4 个索引重建 API 端点正常工作
- [ ] SYS-04.7: 同索引已有执行中任务返回 409
- [ ] SYS-04.8: 增量事件补偿回放
- [ ] SYS-04.9: 重试次数上限 3 次
- [ ] SYS-04.10: 领域层测试覆盖率 ≥ 80%
- [ ] SYS-04.11: 所有测试通过（单元测试 100% 通过率）

## 全局质量检查
- [ ] 所有 11 个 P0 任务完成
- [ ] 整体完成率从 18.4% 提升至 31.0%
- [ ] progress.md 状态更新正确
- [ ] 每个任务独立 commit，commit 消息包含任务 ID
- [ ] 代码符合 DDD 分层架构（Domain→Application→Infrastructure→API）
- [ ] 所有测试通过（单元测试 100% 通过率）
- [ ] 领域层测试覆盖率 ≥ 80%