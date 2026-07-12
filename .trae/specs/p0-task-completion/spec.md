# P0 任务完成 - Master Agent 执行规范

## Why
当前项目已完成 16/87 任务（18.4%），剩余 11 个 P0 关键任务阻塞核心业务流程。需按 Master Agent 架构设计，全流程自主编排完成所有 P0 任务，打通核心交易链路。

## What Changes
- PROMO-02: 秒杀 Redis 库存预占（Lua 脚本原子扣减）
- PAY-02: 微信支付 SDK 对接（统一下单/查询/关闭/退款）
- PAY-03: 支付宝 SDK 对接（创建支付/查询/关闭/退款）
- PM-02: 消费返积分（售后期结束后自动发放）
- NTF-02: 通知域领域模型对齐重构（模板/记录/状态机/值对象）
- NTF-03: INotificationService 统一发送入口（同步/异步/幂等）
- NTF-04: 渠道适配器重构（MailKit SMTP/阿里云短信/腾讯云短信）
- NTF-05: 事件消费者映射（12 类入站事件→模板映射）
- SYS-02: 运营数据看板 DashboardReport（8 个看板 API）
- SYS-03: 死信队列管理 DeadLetterMessage（列表/重投/丢弃）
- SYS-04: 索引重建管理 IndexRebuildTask（状态机/触发/进度/补偿）

## Impact
- Affected specs: promotion, payment, points-membership, notification, system-admin
- Affected code: 5 个限界上下文的 Domain/Application/Infrastructure/API 层
- 依赖关系: PROMO-02 可独立执行；PAY-02/PAY-03 可并行；NTF-02→NTF-03→NTF-04→NTF-05 有顺序依赖；SYS-02/03/04 可并行

## ADDED Requirements

### Requirement: 秒杀 Redis 库存预占
系统 SHALL 在秒杀活动激活时将库存加载到 Redis Hash，通过 Lua 脚本实现原子扣减，防止超卖。

#### Scenario: 秒杀库存扣减成功
- **WHEN** 用户请求秒杀下单且 Redis 库存 > 0
- **THEN** Lua 脚本原子执行 HGET→判库存→HINCRBY -1，返回成功并发布 SeckillOrderCreatedEvent

#### Scenario: 秒杀库存售罄
- **WHEN** 用户请求秒杀下单但 Redis 库存 = 0
- **THEN** Lua 脚本返回 0，拒绝下单

### Requirement: 微信支付 SDK 对接
系统 SHALL 实现 WeChatPayChannel 适配器，支持统一下单、查询、关闭、退款操作。

#### Scenario: 微信支付统一下单
- **WHEN** 调用 CreatePaymentAsync 传入支付金额和订单号
- **THEN** 调用微信支付 V3 API 创建支付订单，返回支付参数/二维码

### Requirement: 支付宝 SDK 对接
系统 SHALL 实现 AlipayChannel 适配器，支持创建支付、查询、关闭、退款操作。

#### Scenario: 支付宝创建支付
- **WHEN** 调用 CreatePaymentAsync 传入支付金额和订单号
- **THEN** 调用支付宝 API 创建支付订单，返回支付参数

### Requirement: 消费返积分
系统 SHALL 在售后期结束后消费 OrderAfterSalesWindowClosedEvent，按比例计算积分和成长值并发放。

#### Scenario: 售后期结束自动返积分
- **WHEN** 消费 OrderAfterSalesWindowClosedEvent（携带 PaidAmount）
- **THEN** 按 1元=1积分 计算积分，调用 PointsAccount.EarnPoints，同时增加成长值

### Requirement: 通知域模型重构
系统 SHALL 将 NotificationTemplate 和 NotificationRecord 字段对齐需求文档，状态机从 4 态重构为 6 态。

#### Scenario: 通知模板字段对齐
- **WHEN** 创建/更新通知模板
- **THEN** 使用 Code/Subject/Body 字段，Variables 为 List<TemplateVariable>

### Requirement: 统一通知发送入口
系统 SHALL 提供 INotificationService.SendAsync 统一发送入口，支持同步/异步/幂等。

#### Scenario: 同步发送成功
- **WHEN** 调用 SendAsync 且模板存在、渲染成功、频率校验通过
- **THEN** 3s 内返回发送结果，创建 NotificationRecord 并回写状态

### Requirement: 渠道适配器重构
系统 SHALL 基于 MailKit 实现 SMTP 邮件渠道，基于阿里云/腾讯云 SDK 实现短信渠道。

#### Scenario: SMTP 邮件发送
- **WHEN** 调用 SmtpEmailChannel.SendAsync
- **THEN** 通过 MailKit SMTP 客户端发送邮件，连接池复用

### Requirement: 事件消费者映射
系统 SHALL 消费 12 类业务事件并映射到通知模板，完成变量补全后调用统一发送入口。

#### Scenario: 用户注册事件触发欢迎通知
- **WHEN** 消费 UserRegisteredEvent
- **THEN** 映射到 user_registered_welcome 模板，渲染后发送通知

### Requirement: 运营数据看板
系统 SHALL 提供 8 个看板 API 端点，聚合订单量/GMV/支付成功率/积分发放/通知送达率/售后量/店铺排行。

#### Scenario: 查询 GMV 看板
- **WHEN** 请求 GET /api/admin/dashboard/overview
- **THEN** 返回订单量、GMV、转化率等聚合指标

### Requirement: 死信队列管理
系统 SHALL 提供死信消息的列表查询、重投、丢弃功能，丢弃原因必填。

#### Scenario: 重投死信消息
- **WHEN** 管理员请求重投死信消息
- **THEN** 通过 RabbitMQ Management API 重新发布消息，幂等去重

### Requirement: 索引重建管理
系统 SHALL 提供 ES 索引重建的触发、进度跟踪、失败重试功能。

#### Scenario: 触发索引重建
- **WHEN** 管理员请求重建商品索引
- **THEN** 创建 IndexRebuildTask，调用 ES reindex API，同索引已有执行中任务返回 409