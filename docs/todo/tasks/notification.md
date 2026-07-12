# 消息通知域 - 任务执行计划

> **模块**: BC10 消息通知域
> **对应文档**: `09-消息通知集成.md`
> **任务 ID 前缀**: NTF
> **总任务数**: 13 | **P0**: 5 | **P1**: 4 | **P2**: 4

---

## 模块概述

消息通知域负责全平台通知模板管理、多渠道发送、用户偏好与频率控制。已实现模板管理、发送记录、用户偏好与基础多渠道发送，但与需求文档存在较大差距——聚合模型、值对象、状态机均需对齐重构。

---

## Task NTF-01: 测试项目创建 [P0]

### 子任务 Checklist

- [ ] NTF-01.1: 创建 `Leno.Notification.Domain.Tests` 项目
- [ ] NTF-01.2: 创建 `Leno.Notification.Application.Tests` 项目
- [ ] NTF-01.3: 创建 `Leno.Notification.Api.Tests` 项目
- [ ] NTF-01.4: 覆盖 NotificationTemplate 聚合（Create、Update、AddVariable、RemoveVariable、Enable、Disable）
- [ ] NTF-01.5: 覆盖 NotificationRecord 聚合（Create、MarkSending、MarkSucceeded、MarkFailed、ScheduleRetry、MoveToDeadLetter）
- [ ] NTF-01.6: 覆盖模板渲染（AC-NTF-01~03）
- [ ] NTF-01.7: 覆盖重试与死信（AC-NTF-06~09）
- [ ] NTF-01.8: 覆盖频率限制（AC-NTF-13~15）
- [ ] NTF-01.9: 配置测试覆盖率 ≥ 80%

### 验收标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖 NotificationRecord 状态机全流转
- [ ] 覆盖模板渲染、重试、死信、频率限制

---

## Task NTF-02: 领域模型对齐重构 [P0]

### 子任务 Checklist

- [ ] NTF-02.1: NotificationTemplate 字段调整：`EventType` → `Code`、`TitleTemplate` → `Subject`、`ContentTemplate` → `Body`
- [ ] NTF-02.2: NotificationTemplate 新增字段：`Name`、`SmsTemplateCode`、`Description`、`OperatorId`
- [ ] NTF-02.3: `Variables` 从 `List<string>` 改为 `List<TemplateVariable>`（Name/Required/Description）
- [ ] NTF-02.4: 新增方法：`AddVariable(variable)`、`RemoveVariable(name)`、`ContainsPlaceholder(name)`
- [ ] NTF-02.5: NotificationRecord 字段调整：`EventType` → `TemplateCode`、`FailReason` → `ErrorMessage` + `ErrorCode`
- [ ] NTF-02.6: NotificationRecord 新增字段：`ContentSnapshot`、`ChannelMessageId`、`ChannelReceipt`、`MaxRetry`、`NextRetryAt`、`SentAt`、`FailedAt`、`BusinessRef`、`IdempotencyKey`
- [ ] NTF-02.7: 状态机重构：4 态（Pending/Sent/Failed/Abandoned）→ 6 态（Pending/Sending/Succeeded/Failed/Retried/DeadLettered）
- [ ] NTF-02.8: 新增值对象：`Recipient`、`TemplateVariable`、`ChannelSendRequest`、`ChannelSendResult`、`NotificationRequest`
- [ ] NTF-02.9: EF Core 迁移脚本（数据迁移+状态映射）

### 验收标准
- [ ] NotificationTemplate 字段与方法对齐需求文档
- [ ] NotificationRecord 状态机 6 态完整流转
- [ ] Recipient 值对象按渠道校验格式
- [ ] 内容快照在发送时固化（AC-NTF-04、AC-NTF-05）

---

## Task NTF-03: INotificationService 统一发送入口 [P0]

### 子任务 Checklist

- [ ] NTF-03.1: 在领域层定义 `NotificationRequest` 命令对象
- [ ] NTF-03.2: 在应用层实现 `INotificationService.SendAsync(NotificationRequest)` 接口
- [ ] NTF-03.3: 发送链路：查模板 → 渲染 → 频率校验 → 创建 NotificationRecord → 选渠道发送 → 回写状态
- [ ] NTF-03.4: 同步调用超时阈值 3s，超时返回"已受理转异步"
- [ ] NTF-03.5: 幂等键（IdempotencyKey）去重：重复请求返回已存在记录
- [ ] NTF-03.6: 实现 `POST /api/notifications/send` 端点（内部服务间调用，API Key 鉴权）
- [ ] NTF-03.7: 模板禁用时拦截返回错误（AC-NTF-21）
- [ ] NTF-03.8: 模板缺失降级跳过发送并记录告警

### 验收标准
- [ ] 同步发送 3s 内返回结果（AC-NTF-16）
- [ ] 超时转异步不抛异常阻断调用方（AC-NTF-17）
- [ ] 幂等键重复请求返回已存在记录（AC-NTF-19）

---

## Task NTF-04: 渠道适配器重构 [P0]

### 子任务 Checklist

- [ ] NTF-04.1: 在领域层定义 `INotificationChannel` 接口（`Channel` 属性 + `SendAsync` 方法）
- [ ] NTF-04.2: 在领域层定义 `ChannelSendRequest` / `ChannelSendResult` 记录类型
- [ ] NTF-04.3: 实现 `SmtpEmailChannel`（基于 **MailKit**，SMTP 连接池复用）
- [ ] NTF-04.4: 实现 `AliyunSmsChannel`（基于阿里云短信 SDK）
- [ ] NTF-04.5: 实现 `TencentSmsChannel`（基于腾讯云短信 SDK）
- [ ] NTF-04.6: 配置结构：EmailChannelOptions（Host/Port/Username/Password/From/UseSsl）、SmsChannelOptions（Provider/AccessKeyId/AccessKeySecret/SignName）
- [ ] NTF-04.7: 替换现有 `EmailChannel`/`SmsChannel`/`InAppChannel` 为规约命名
- [ ] NTF-04.8: SMTP 连接超时 10s，短信 API 超时转异步重试
- [ ] NTF-04.9: 敏感参数加密存储（AC-NTF-12）

### 验收标准
- [ ] SmtpEmailChannel 基于 MailKit 实现
- [ ] AliyunSmsChannel/TencentSmsChannel 基于对应 SDK
- [ ] 错误分级：可重试（SMTP 421/450、限流）vs 不可重试（邮箱不存在 550、签名不符）

---

## Task NTF-05: 事件消费者映射 [P0]

### 子任务 Checklist

- [ ] NTF-05.1: 在基础设施层创建 `NotificationEventConsumer` 消费 12 类入站事件
- [ ] NTF-05.2: 事件→模板映射表配置化（`EventTemplateMapping`）：UserRegisteredEvent→user_registered_welcome、OrderCreatedEvent→order_created、OrderPaidEvent→order_paid 等
- [ ] NTF-05.3: 变量补全：部分变量需回调上游查询接口（如订单金额）
- [ ] NTF-05.4: 事件消费幂等（以 EventId 去重）
- [ ] NTF-05.5: 变量补全失败时记录并跳过，不阻塞队列
- [ ] NTF-05.6: 主交易不等待通知完成（AC-NTF-18）
- [ ] NTF-05.7: 事件字段缺失导致无法渲染时记录失败并发布 NotificationFailedEvent

### 验收标准
- [ ] 消费 12 类业务事件并映射到通知模板
- [ ] 事件消费幂等
- [ ] 变量补全失败不阻塞队列

---

## Task NTF-06: 发送失败重试与死信处理 [P1]

### 子任务 Checklist

- [ ] NTF-06.1: 在领域层实现 `IRetryPolicy` 领域服务（`ShouldRetry`、`NextDelay`）
- [ ] NTF-06.2: 错误分级：可重试（SMTP 421/450、限流、超时、5xx）→ 退避重试；不可重试（邮箱不存在 550、黑名单、签名不符）→ 直接死信
- [ ] NTF-06.3: 重试时间间隔：指数退避 30s / 2min / 10min
- [ ] NTF-06.4: 创建后台 worker `NotificationRetryJob` 周期扫描 `NextRetryAt` 到期的 Retried 记录
- [ ] NTF-06.5: 重试达 MaxRetry（3 次）仍失败 → `MoveToDeadLetter` 进入死信终态
- [ ] NTF-06.6: 实现死信管理 API（列表/批量重发/丢弃）
- [ ] NTF-06.7: 丢弃原因必填，批量操作记录审计日志
- [ ] NTF-06.8: 死信队列积压超阈值告警

### 验收标准
- [ ] 可重试错误指数退避重试（AC-NTF-06）
- [ ] 重试 3 次仍失败进入死信（AC-NTF-07）
- [ ] 不可重试错误直接死信不重试（AC-NTF-08）

---

## Task NTF-07: 模板渲染服务 [P1]

### 子任务 Checklist

- [ ] NTF-07.1: 在领域层实现 `ITemplateRenderService.RenderAsync(template, variables)`
- [ ] NTF-07.2: 必填变量缺失 → 抛领域异常，拒绝发送（AC-NTF-01）
- [ ] NTF-07.3: 可选变量缺失 → 渲染成功（AC-NTF-02）
- [ ] NTF-07.4: 正文含未定义占位符 → 保存模板时返回 400（AC-NTF-03）
- [ ] NTF-07.5: 变量值含 HTML 特殊字符 → 转义防注入
- [ ] NTF-07.6: 渲染结果固化到 `NotificationRecord.ContentSnapshot`

### 验收标准
- [ ] 必填变量缺失返回 400 拒绝发送
- [ ] 可选变量缺失渲染成功
- [ ] HTML 特殊字符转义防注入

---

## Task NTF-08: 渠道参数配置管理 [P1]

### 子任务 Checklist

- [ ] NTF-08.1: 实现 `GET /api/admin/notification-config` - 渠道配置（脱敏展示）
- [ ] NTF-08.2: 实现 `PUT /api/admin/notification-config` - 更新渠道配置
- [ ] NTF-08.3: 实现 `POST /api/admin/notification-config/test` - 测试发送验证
- [ ] NTF-08.4: 敏感参数加密存储，展示脱敏为 `******`（AC-NTF-12）
- [ ] NTF-08.5: 配置变更热更新适配器实例重建（AC-NTF-11）
- [ ] NTF-08.6: 在途发送沿用旧适配器实例，新发送使用新实例
- [ ] NTF-08.7: 配置变更记录审计日志

### 验收标准
- [ ] 系统管理员可查看/更新渠道配置
- [ ] 敏感参数脱敏展示
- [ ] 切换服务商只改配置不改代码（AC-NTF-10）

---

## Task NTF-09: 通知频率限制与防骚扰 [P1]

### 子任务 Checklist

- [ ] NTF-09.1: 在领域层定义 `IRateLimiter.AcquireAsync(recipient, templateCode, channel)` 接口
- [ ] NTF-09.2: 在基础设施层实现 `RedisRateLimiter`（基于 Redis 滑动窗口）
- [ ] NTF-09.3: 限流规则：邮件 10 条/小时/收件人、短信 5 条/小时/收件人、20 条/天/收件人
- [ ] NTF-09.4: 验证码类通知可配置豁免或单独限流
- [ ] NTF-09.5: 发送前调用频率校验，超限拒绝并记录 errorCode=RATE_LIMITED
- [ ] NTF-09.6: Redis 不可用时降级为放行并告警（AC-NTF-15）
- [ ] NTF-09.7: 实现 `GET/PUT /api/admin/notification-rate-limits` 端点

### 验收标准
- [ ] 短信 1 小时超限返回 429（AC-NTF-13）
- [ ] 未达上限正常通过（AC-NTF-14）
- [ ] Redis 不可用降级放行并告警

---

## Task NTF-10: 多渠道选择与故障转移 [P2]

### 子任务 Checklist

- [ ] NTF-10.1: 在领域层实现 `IChannelSelector.Select(channel, recipient)` 方法
- [ ] NTF-10.2: 邮件渠道默认 SMTP
- [ ] NTF-10.3: 短信渠道按配置 Provider 字段选择阿里云或腾讯云
- [ ] NTF-10.4: 主适配器失败且配置了备适配器时，可重试切备适配器
- [ ] NTF-10.5: 故障转移仅对可重试错误生效，不跨渠道
- [ ] NTF-10.6: 所有服务商均不可用记录失败并告警

### 验收标准
- [ ] 按配置 Provider 选择短信服务商
- [ ] 主服务商失败自动切备服务商
- [ ] 故障转移不跨渠道

---

## Task NTF-11: 渠道回执接收与状态更新 [P2]

### 子任务 Checklist

- [ ] NTF-11.1: 实现 `POST /api/notifications/callbacks/email` - 邮件回执回调
- [ ] NTF-11.2: 实现 `POST /api/notifications/callbacks/sms` - 短信回执回调
- [ ] NTF-11.3: 验签防伪造，验签失败返回 401（AC-NTF-25）
- [ ] NTF-11.4: 按 ChannelMessageId 匹配 NotificationRecord，调用 `ApplyReceipt` 更新状态
- [ ] NTF-11.5: 已 Succeeded 的记录幂等去重不重复处理（AC-NTF-26）
- [ ] NTF-11.6: 回执原文脱敏存储

### 验收标准
- [ ] 回执验签通过后更新记录状态（AC-NTF-24）
- [ ] 验签失败返回 401
- [ ] 重复回执幂等去重

---

## Task NTF-12: 发送记录查询与送达率统计 [P2]

### 子任务 Checklist

- [ ] NTF-12.1: 实现 `GET /api/notifications/records` - 发送记录列表（多维度筛选+分页）
- [ ] NTF-12.2: 实现 `GET /api/notifications/records/{id}` - 记录详情
- [ ] NTF-12.3: 实现 `GET /api/notifications/records/by-business/{businessRef}` - 按业务关联追踪
- [ ] NTF-12.4: 实现 `POST /api/admin/notifications/records/{id}/resend` - 死信记录重发
- [ ] NTF-12.5: 实现 `GET /api/admin/notifications/statistics` - 送达率统计
- [ ] NTF-12.6: 手机号与邮箱在列表脱敏展示（如 138****1234）
- [ ] NTF-12.7: 统计报表按渠道与模板分桶

### 验收标准
- [ ] 运营可查询发送记录与详情
- [ ] 按 BusinessRef 追踪业务通知
- [ ] 送达率统计报表含渠道分布

---

## Task NTF-13: 通知模板管理增强 [P2]

### 子任务 Checklist

- [ ] NTF-13.1: 模板变量维护：新增/编辑模板时支持增减 `TemplateVariable`
- [ ] NTF-13.2: 保存时校验变量名与正文占位符 `{{var}}` 一致
- [ ] NTF-13.3: 实现 `POST /api/admin/notification-templates/{id}/preview` - 模板预览
- [ ] NTF-13.4: 已禁用模板编辑须先启用
- [ ] NTF-13.5: 非运营角色返回 403（AC-NTF-22）

### 验收标准
- [ ] 模板变量定义与正文占位符一致性校验
- [ ] 模板预览渲染结果
- [ ] 已禁用模板编辑须先启用