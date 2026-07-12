# 消息通知域 - 缺失功能任务

> **限界上下文**: BC9 消息通知域
> **对应文档**: `09-消息通知集成.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

通知域已实现模板管理、发送记录、用户偏好与基础多渠道发送，但与需求文档存在较大差距。现有模型与需求文档定义不同，需对齐重构。

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 领域模型对齐重构 | P0 关键 | 聚合/值对象/状态机与需求文档不一致 |
| INotificationService 统一发送入口 | P0 关键 | 同步发送 `SendAsync(NotificationRequest)` |
| 渠道适配器重构（MailKit/阿里云/腾讯云） | P0 关键 | 替换现有通道为规范命名的适配器 |
| 事件消费者映射 | P0 关键 | 消费各域业务事件映射通知模板 |
| 发送失败重试与死信处理 | P1 重要 | 指数退避重试、死信终态、人工重发/丢弃 |
| 模板渲染服务 | P1 重要 | 变量替换、必填校验、内容快照固化 |
| 通知频率限制与防骚扰 | P1 重要 | Redis 滑动窗口限流 |
| 渠道参数配置管理 | P1 重要 | SMTP/短信渠道参数配置化与热更新 |
| 渠道回执接收与状态更新 | P2 一般 | 服务商回执回调验签与状态更新 |
| 多渠道选择与故障转移 | P2 一般 | 服务商优先级与主备切换 |
| 送达率统计报表 | P2 一般 | 运营查询送达率与失败原因分布 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.Notification.Domain.Tests`、`Leno.Notification.Application.Tests`、`Leno.Notification.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目，遵循 `{BC}.{层}.Tests` 命名规范
2. 覆盖 NotificationTemplate 聚合（Create、Update、AddVariable、RemoveVariable、Enable、Disable）
3. 覆盖 NotificationRecord 聚合（Create、MarkSending、MarkSucceeded、MarkFailed、ScheduleRetry、MoveToDeadLetter）
4. 覆盖模板渲染（必填变量校验、占位符替换、缺变量抛异常）
5. 覆盖频率限制（超限拒绝、降级放行）
6. 覆盖 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖 NotificationRecord 状态机全流转
- [ ] 覆盖模板渲染（AC-NTF-01 ~ AC-NTF-03）
- [ ] 覆盖重试与死信（AC-NTF-06 ~ AC-NTF-09）
- [ ] 覆盖频率限制（AC-NTF-13 ~ AC-NTF-15）

### 参考
- `编码规范.md` 第 13 章
- `09-消息通知集成.md` 第 9 章验收标准

---

## Task 2: 领域模型对齐重构

**严重程度**: P0 关键

### 功能描述
将现有 NotificationTemplate、NotificationRecord 聚合与值对象对齐需求文档定义，重构状态机与字段结构。

### 技术实现路径

**2.1 NotificationTemplate 重构**
1. 字段调整：`EventType` → `Code`（模板编码，全局唯一）、`TitleTemplate` → `Subject`、`ContentTemplate` → `Body`
2. 新增字段：`Name`、`SmsTemplateCode`（短信渠道必填）、`Description`、`OperatorId`
3. `Variables` 从 `List<string>` 改为 `List<TemplateVariable>`（Name/Required/Description）
4. 新增方法：`AddVariable(variable)`、`RemoveVariable(name)`、`ContainsPlaceholder(name)`
5. 约束：编码符合 `^[a-z][a-z0-9_]{0,63}$`、邮件 Subject 必填、短信 SmsTemplateCode 必填

**2.2 NotificationRecord 重构**
1. 字段调整：`EventType` → `TemplateCode`、`FailReason` → `ErrorMessage` + `ErrorCode`
2. 新增字段：`ContentSnapshot`（渲染后正文快照）、`ChannelMessageId`、`ChannelReceipt`、`MaxRetry`、`NextRetryAt`、`SentAt`、`FailedAt`、`BusinessRef`、`IdempotencyKey`
3. 状态机重构：
   - 当前 4 态（Pending/Sent/Failed/Abandoned）→ 规约 6 态（Pending/Sending/Succeeded/Failed/Retried/DeadLettered）
   - 新增方法：`MarkSending()`、`ScheduleRetry(delay)`、`MoveToDeadLetter()`、`ApplyReceipt(status, receipt)`
   - `MarkFailed` 改为记录本次失败并判断重试/死信
4. 内容快照在 Create 时固化，后续模板修改不影响历史记录

**2.3 新增值对象**
1. `Recipient`：Email + Phone 互斥，按渠道校验格式
2. `TemplateVariable`：Name（字母数字下划线）、Required、Description
3. `ChannelSendRequest` / `ChannelSendResult` 记录类型（领域层渠道抽象入参/出参）
4. `NotificationRequest`：命令对象（TemplateCode、Channel、Recipient、Variables、BusinessRef、IdempotencyKey）

### 预期完成标准
- [ ] NotificationTemplate 字段与方法对齐需求文档
- [ ] NotificationRecord 状态机 6 态完整流转
- [ ] Recipient 值对象按渠道校验格式
- [ ] TemplateVariable 值对象校验变量名合法性
- [ ] 内容快照在发送时固化（AC-NTF-04、AC-NTF-05）
- [ ] 模板编码重复返回 409（AC-NTF-20）

### 参考
- `09-消息通知集成.md` 第 2.1 节聚合与实体、第 2.1.4 节值对象
- `09-消息通知集成.md` 第 6 节业务规则（INV-NTF-01/02/03/07/10/13）
- `09-消息通知集成.md` 第 7 节状态机

---

## Task 3: INotificationService 统一发送入口

**严重程度**: P0 关键

### 功能描述
实现 `INotificationService.SendAsync(NotificationRequest)` 作为全平台统一通知发送入口，支持同步发送与超时熔断转异步。

### 技术实现路径
1. 在领域层定义 `NotificationRequest` 命令对象
2. 在应用层实现 `INotificationService` 接口：
   ```
   Task<SendResult> SendAsync(NotificationRequest request, CancellationToken ct);
   ```
3. 发送链路：查模板 → 渲染 → 频率校验 → 创建 NotificationRecord（固化快照）→ 选渠道发送 → 回写状态
4. 同步调用超时阈值 3s，超时返回"已受理转异步"并将记录留待重试
5. 幂等键（IdempotencyKey）去重：重复请求返回已存在记录
6. 实现 API：
   - `POST /api/notifications/send` - 内部服务间调用，经 API Key/mTLS 鉴权

### 预期完成标准
- [ ] 同步发送 3s 内返回结果（AC-NTF-16）
- [ ] 超时转异步不抛异常阻断调用方（AC-NTF-17）
- [ ] 幂等键重复请求返回已存在记录（AC-NTF-19）
- [ ] 模板禁用时拦截返回错误（AC-NTF-21）
- [ ] 模板缺失降级跳过发送并记录告警

### 参考
- `09-消息通知集成.md` F-NTF-001 同步发送通知
- `09-消息通知集成.md` 第 5 章 API 设计

---

## Task 4: 渠道适配器重构

**严重程度**: P0 关键

### 功能描述
按需求文档规范重构渠道适配器：`SmtpEmailChannel`（基于 MailKit）、`AliyunSmsChannel`、`TencentSmsChannel`，实现 `INotificationChannel` 接口。

### 技术实现路径
1. 领域层定义 `INotificationChannel` 接口：
   ```csharp
   public interface INotificationChannel
   {
       NotificationChannel Channel { get; }
       Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default);
   }
   ```
2. 领域层定义 `ChannelSendRequest` / `ChannelSendResult` 记录类型
3. 基础设施层实现：
   - `SmtpEmailChannel`：使用 **MailKit** 连接 SMTP，参数从 `EmailChannelOptions` 注入（Host/Port/Username/Password/From/UseSsl）
   - `AliyunSmsChannel`：基于阿里云短信 SDK，参数从 `SmsChannelOptions` 注入
   - `TencentSmsChannel`：基于腾讯云短信 SDK，参数从 `SmsChannelOptions` 注入
4. 配置结构按需求文档第 2.4 节定义
5. 替换现有 `EmailChannel`/`SmsChannel`/`InAppChannel` 为规约命名
6. SMTP 连接超时 10s，连接池复用；短信服务商 API 超时转异步重试

### 预期完成标准
- [ ] SmtpEmailChannel 基于 MailKit 实现
- [ ] AliyunSmsChannel 基于阿里云短信 SDK
- [ ] TencentSmsChannel 基于腾讯云短信 SDK
- [ ] 邮件渠道 SMTP 认证失败、服务商限流归为可重试错误
- [ ] 邮箱不存在（550）归为不可重试错误
- [ ] 短信限流、签名不符归为不可重试错误
- [ ] 参数配置化，敏感参数加密存储（AC-NTF-12）

### 参考
- `09-消息通知集成.md` 第 2.4 节渠道适配抽象
- `09-消息通知集成.md` F-NTF-003 邮件发送、F-NTF-004 短信发送
- `09-消息通知集成.md` 第 8 节配置驱动设计

---

## Task 5: 事件消费者映射

**严重程度**: P0 关键

### 功能描述
消费各域业务事件，按事件类型映射到通知模板编码与变量字典，构造 `NotificationRequest` 入队异步发送。

### 技术实现路径
1. 在基础设施层实现 `NotificationEventConsumer` 消费以下入站事件：
   - `UserRegisteredEvent` → `user_registered_welcome` 欢迎邮件
   - `OrderCreatedEvent` → `order_created` 下单通知
   - `OrderPaidEvent` → `order_paid` 支付成功通知
   - `OrderCancelledEvent` → `order_cancelled` 取消通知
   - `OrderShippedEvent` → `order_shipped` 发货通知
   - `SeckillOrderCreatedEvent` → `seckill_success` 秒杀成功通知
   - `PaymentFailedIntegrationEvent` → `payment_failed` 支付失败通知
   - `AfterSalesApprovedEvent` → `after_sales_approved` 售后进度通知
   - `RefundCompletedEvent` → `refund_completed` 退款到账通知
   - `PointsEarnedEvent` → `points_earned` 积分到账通知（可选）
   - `MemberLevelChangedEvent` → `member_level_changed` 等级变更通知
   - `PaidMemberSubscribedEvent` → `paid_member_subscribed` 付费会员权益通知
2. 事件→模板映射表配置化，支持运营动态调整
3. 变量补全：部分变量需回调上游查询接口（如订单金额）
4. 事件消费幂等（以 EventId 去重）
5. 变量补全失败时记录并跳过，不阻塞队列

### 预期完成标准
- [ ] 消费 12 类业务事件并映射到通知模板
- [ ] 事件消费幂等（以 EventId 去重）
- [ ] 变量补全失败不阻塞队列
- [ ] 主交易不等待通知完成（AC-NTF-18）
- [ ] 事件字段缺失导致无法渲染时记录失败并发布 NotificationFailedEvent

### 参考
- `09-消息通知集成.md` 第 3 节领域事件清单
- `09-消息通知集成.md` F-NTF-002 异步发送通知
- `09-消息通知集成.md` INV-NTF-08 异步不阻塞主交易

---

## Task 6: 发送失败重试与死信处理

**严重程度**: P1 重要

### 功能描述
实现发送失败指数退避重试、死信终态与人工处置（重发/丢弃）。

### 技术实现路径
1. 实现 `IRetryPolicy` 领域服务：
   - `ShouldRetry(record)`：判断是否可重试（未达 MaxRetry 且错误可重试）
   - `NextDelay(retryCount)`：指数退避 30s / 2min / 10min
2. 错误分级：可重试（SMTP 421/450、服务商限流、超时、5xx）→ 退避重试；不可重试（邮箱不存在 550、手机黑名单、签名不符）→ 直接死信
3. 后台 worker 周期扫描 `NextRetryAt` 到期的 Retried 记录重新发送
4. 重试达 MaxRetry（3 次）仍失败 → `MoveToDeadLetter` 进入死信终态
5. 死信管理 API：
   - `GET /api/admin/notifications/dead-letters` - 死信列表
   - `POST /api/admin/notifications/dead-letters/batch-resend` - 批量重发
   - `POST /api/admin/notifications/dead-letters/{id}/discard` - 丢弃单条
   - `POST /api/admin/notifications/dead-letters/batch-discard` - 批量丢弃
6. 人工重发重置 RetryCount 并置回待发送；丢弃标记终态
7. 批量操作记录操作人与记录 ID 清单入审计日志
8. 死信队列积压超阈值告警

### 预期完成标准
- [ ] 可重试错误指数退避重试（AC-NTF-06）
- [ ] 重试 3 次仍失败进入死信（AC-NTF-07）
- [ ] 不可重试错误直接死信不重试（AC-NTF-08）
- [ ] 退避到期重新发送（AC-NTF-09）
- [ ] 死信批量重发/丢弃功能
- [ ] 丢弃原因必填
- [ ] 并发捞取防重复（乐观锁或 SELECT FOR UPDATE）

### 参考
- `09-消息通知集成.md` F-NTF-008 发送失败重试与死信处理
- `09-消息通知集成.md` 第 5 章 API 设计（死信管理接口）
- `09-消息通知集成.md` INV-NTF-03 重试上限、INV-NTF-14 错误分级重试

---

## Task 7: 模板渲染服务

**严重程度**: P1 重要

### 功能描述
实现 `ITemplateRenderService` 模板渲染，校验必填变量、替换占位符、返回渲染后标题与正文。

### 技术实现路径
1. 在领域层实现 `ITemplateRenderService`：
   - `RenderAsync(template, variables)`：校验 Required 变量齐全 → 替换 `{{var}}` 占位符 → 返回标题与正文
2. 渲染规则：
   - 必填变量缺失 → 抛领域异常，拒绝发送
   - 多余变量 → 忽略并记录告警
   - 正文含未定义占位符 → 返回 400
   - 变量值含 HTML 特殊字符 → 转义防注入
3. 渲染结果作为内容快照固化到 `NotificationRecord.ContentSnapshot`
4. 渲染与发送分离，渲染失败不影响渠道

### 预期完成标准
- [ ] 必填变量缺失返回 400 拒绝发送（AC-NTF-01）
- [ ] 可选变量缺失渲染成功（AC-NTF-02）
- [ ] 未定义占位符保存模板时返回 400（AC-NTF-03）
- [ ] HTML 特殊字符转义防注入
- [ ] 渲染结果固化到 NotificationRecord

### 参考
- `09-消息通知集成.md` F-NTF-006 模板渲染
- `09-消息通知集成.md` 第 2.2 节领域服务（ITemplateRenderService）
- `09-消息通知集成.md` INV-NTF-01 内容快照固化、INV-NTF-02 模板变量必填校验

---

## Task 8: 渠道参数配置管理

**严重程度**: P1 重要

### 功能描述
实现系统管理员维护邮件/短信渠道参数，支持配置中心热更新、敏感参数脱敏、测试发送验证。

### 技术实现路径
1. 实现 API：
   - `GET /api/admin/notification-config` - 渠道配置（脱敏展示）
   - `PUT /api/admin/notification-config` - 更新渠道配置
   - `POST /api/admin/notification-config/test` - 测试发送验证
2. 配置结构按需求文档第 2.4 节：
   - 邮件：Provider、SMTP（Host/Port/Username/Password/From/DisplayName/UseSsl）
   - 短信：Provider、阿里云（AccessKeyId/AccessKeySecret/SignName/Endpoint）、腾讯云（SecretId/SecretKey/SignName/SdkAppId）
3. 敏感参数（Password、AccessKeySecret、SecretKey）加密存储，展示脱敏为 `******`
4. 配置变更通过 Consul/Apollo 配置中心热更新，适配器实例重建
5. 在途发送沿用旧适配器实例，新发送使用新实例
6. 多环境配置隔离（开发用 MailHog/沙箱，生产用正式服务商）
7. 配置变更记录审计日志

### 预期完成标准
- [ ] 系统管理员可查看/更新渠道配置
- [ ] 敏感参数脱敏展示（AC-NTF-12）
- [ ] 切换服务商只改配置不改代码（AC-NTF-10）
- [ ] 配置热更新适配器重建（AC-NTF-11）
- [ ] 非系统管理员返回 403（AC-NTF-23）
- [ ] 配置校验失败返回 400

### 参考
- `09-消息通知集成.md` F-NTF-009 渠道参数配置管理
- `09-消息通知集成.md` 第 8 节配置驱动设计
- `09-消息通知集成.md` INV-NTF-05 敏感参数不落库明文、INV-NTF-11 渠道参数配置化

---

## Task 9: 通知频率限制与防骚扰

**严重程度**: P1 重要

### 功能描述
实现 `IRateLimiter` 基于 Redis 滑动窗口按收件人与渠道统计发送量，超限拒绝发送。

### 技术实现路径
1. 在领域层定义 `IRateLimiter` 接口：
   - `AcquireAsync(recipient, templateCode, channel)`：判断是否允许发送
2. 在基础设施层实现 `RedisRateLimiter`：
   - 邮件：10 条/小时/收件人
   - 短信：5 条/小时/收件人、20 条/天/收件人
   - 验证码类通知可配置豁免或单独限流
3. 发送前调用频率校验，超限拒绝并记录 errorCode=RATE_LIMITED
4. Redis 不可用时降级为放行并告警
5. 实现 API：
   - `GET /api/admin/notification-rate-limits` - 查询当前规则
   - `PUT /api/admin/notification-rate-limits` - 更新规则（热加载即时生效）
6. 规则变更记录审计日志

### 预期完成标准
- [ ] 短信 1 小时超限返回 429（AC-NTF-13）
- [ ] 未达上限正常通过（AC-NTF-14）
- [ ] Redis 不可用降级放行并告警（AC-NTF-15）
- [ ] 系统管理员可配置限流规则
- [ ] 规则变更热加载即时生效

### 参考
- `09-消息通知集成.md` F-NTF-010 通知频率限制与防骚扰
- `09-消息通知集成.md` INV-NTF-04 频率限制
- `09-消息通知集成.md` 第 5 章 API 设计（频率限制接口）

---

## Task 10: 多渠道选择与故障转移

**严重程度**: P2 一般

### 功能描述
实现 `IChannelSelector` 按渠道与收件人选择适配器，支持服务商优先级与故障转移。

### 技术实现路径
1. 在领域层实现 `IChannelSelector`：
   - `Select(channel, recipient)`：按渠道与收件人返回 `INotificationChannel` 实现
2. 邮件渠道默认 SMTP
3. 短信渠道按配置 Provider 字段选择阿里云或腾讯云
4. 主适配器失败且配置了备适配器时，可重试切备适配器
5. 故障转移仅对可重试错误生效，不跨渠道（邮件不会转短信）
6. 切换服务商记录在错误信息中

### 预期完成标准
- [ ] 按配置 Provider 选择短信服务商
- [ ] 主服务商失败自动切备服务商
- [ ] 故障转移不跨渠道
- [ ] 所有服务商均不可用记录失败并告警

### 参考
- `09-消息通知集成.md` F-NTF-012 多渠道选择
- `09-消息通知集成.md` 第 2.2 节领域服务（IChannelSelector）

---

## Task 11: 渠道回执接收与状态更新

**严重程度**: P2 一般

### 功能描述
接收服务商异步回执回调，验签后更新 NotificationRecord 状态。

### 技术实现路径
1. 实现回执回调端点：
   - `POST /api/notifications/callbacks/email` - 邮件回执回调
   - `POST /api/notifications/callbacks/sms` - 短信回执回调
2. 验签防伪造，验签失败返回 401
3. 按 ChannelMessageId 匹配 NotificationRecord，调用 `ApplyReceipt` 更新状态
4. 已 Succeeded 的记录以幂等键去重不重复处理
5. 回执原文脱敏存储

### 预期完成标准
- [ ] 回执验签通过后更新记录状态（AC-NTF-24）
- [ ] 验签失败返回 401（AC-NTF-25）
- [ ] 重复回执幂等去重（AC-NTF-26）
- [ ] 记录不存在返回 404

### 参考
- `09-消息通知集成.md` F-NTF-011 渠道回执接收与状态更新
- `09-消息通知集成.md` INV-NTF-12 回执验签

---

## Task 12: 发送记录查询与送达率统计

**严重程度**: P2 一般

### 功能描述
实现发送记录多视角查询与送达率统计报表。

### 技术实现路径
1. 实现 API：
   - `GET /api/notifications/records` - 发送记录列表（按渠道、状态、模板、收件人、时间范围筛选）
   - `GET /api/notifications/records/{id}` - 记录详情（含内容快照、状态、重试次数、回执、错误信息）
   - `GET /api/notifications/records/by-business/{businessRef}` - 按业务关联追踪
   - `POST /api/admin/notifications/records/{id}/resend` - 死信记录重发
   - `GET /api/admin/notifications/statistics` - 送达率统计（总数/成功/失败/送达率/平均耗时/死信数/渠道分布）
2. 手机号与邮箱在列表脱敏展示（如 138****1234）
3. 内容快照原文仅详情页可见
4. 列表查询走读库或缓存
5. 统计报表按渠道与模板分桶

### 预期完成标准
- [ ] 运营可查询发送记录与详情
- [ ] 按 BusinessRef 追踪业务通知
- [ ] 送达率统计报表含渠道分布
- [ ] 手机号/邮箱脱敏展示
- [ ] 列表分页支持

### 参考
- `09-消息通知集成.md` F-NTF-007 发送记录查询与追踪
- `09-消息通知集成.md` 第 5 章 API 设计（记录查询接口）

---

## Task 13: 通知模板管理增强

**严重程度**: P2 一般

### 功能描述
在已有模板管理基础上增强：模板变量定义维护、占位符一致性校验、模板预览。

### 技术实现路径
1. 模板变量维护：
   - 新增/编辑模板时支持增减 `TemplateVariable`（Name/Required/Description）
   - 保存时校验变量名与正文占位符 `{{var}}` 一致
2. 模板预览：
   - `POST /api/admin/notification-templates/{id}/preview` - 传入示例变量值预览渲染结果
3. 已禁用模板编辑须先启用

### 预期完成标准
- [ ] 模板变量定义与正文占位符一致性校验
- [ ] 模板预览渲染结果
- [ ] 已禁用模板编辑须先启用
- [ ] 非运营角色返回 403（AC-NTF-22）

### 参考
- `09-消息通知集成.md` F-NTF-005 通知模板管理
- `09-消息通知集成.md` 第 5 章 API 设计（模板管理接口）

---

## DDD 分层落点参考

| 分层 | 消息通知域落点 |
|------|---------------|
| 领域层 | NotificationTemplate/NotificationRecord 聚合，NotificationChannel/NotificationStatus/TemplateStatus/Recipient/TemplateVariable 值对象，INotificationChannel 渠道抽象，ITemplateRenderService/IChannelSelector/INotificationDispatcher/IRetryPolicy/IRateLimiter 领域服务，领域事件，仓储接口 |
| 应用层 | INotificationService（对外统一入口）、INotificationTemplateAppService、INotificationConfigAppService，DTO、Command/Query，NotificationEventConsumer，重试编排 worker |
| 基础设施层 | SmtpEmailChannel/AliyunSmsChannel/TencentSmsChannel 适配器，EfCore 仓储，RedisRateLimiter，回执回调处理，发件箱发布 |
| 表现层 | NotificationsController（内部发送）、NotificationTemplatesController（运营）、NotificationConfigController（系统管理员）、回执端点 |