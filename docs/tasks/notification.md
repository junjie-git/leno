# 消息通知域 (Notification) 开发任务

> **限界上下文**: BC9 消息通知集成  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / RabbitMQ / SMTP / SMS SDK  
> **依赖**: `shared-kernel`  
> **对应文档**: `09-消息通知集成.md`

---

## 模块概述

消息通知域作为平台统一通知出口，消费各域发布的领域事件，根据通知模板与用户偏好生成站内信、短信、邮件通知。通知模板配置驱动，支持变量插值。通知记录持久化供查询与重试。外部渠道（短信/邮件）发送失败自动重试，超阈值进死信队列告警。

---

## Task 1: 项目初始化与领域层 — Notification 聚合

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Domain/Leno.Notification.Domain.csproj`
- Create: `src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs`
- Create: `src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationTemplate.cs`

- [ ] 创建 Leno.Notification.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `NotificationRecord` 聚合根（RecordId、UserId、EventType、Channel、Title、Content、Status、RetryCount、SentAt、FailReason、CreatedAt、Version）
- [ ] 实现 `NotificationRecord.Create` 工厂方法（从模板渲染内容，置待发送态）
- [ ] 实现 `MarkSent(sentAt)`（发送成功）
- [ ] 实现 `MarkFailed(reason)`（发送失败，增加 RetryCount）
- [ ] 实现 `MarkAbandoned()`（超过重试上限，放弃发送）
- [ ] 实现 `NotificationTemplate` 聚合根（TemplateId、EventType、Channel、TitleTemplate、ContentTemplate、Variables、Status、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `NotificationTemplate.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 定义 `NotificationChannel`（InApp/SMS/Email）、`NotificationStatus`（Pending/Sent/Failed/Abandoned）
- [ ] 编写单元测试
- [ ] 提交：`feat(notification): add NotificationRecord and NotificationTemplate aggregates`

---

## Task 2: 领域层 — 用户偏好与渲染服务

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationPreference.cs`
- Create: `src/Services/Notification/Leno.Notification.Domain/Services/ITemplateRenderer.cs`
- Create: `src/Services/Notification/Leno.Notification.Domain/Repositories/INotificationRecordRepository.cs`
- Create: `src/Services/Notification/Leno.Notification.Domain/Repositories/INotificationTemplateRepository.cs`
- Create: `src/Services/Notification/Leno.Notification.Domain/Repositories/INotificationPreferenceRepository.cs`

- [ ] 实现 `NotificationPreference` 聚合（UserId、EventChannels 字典、Status）— 用户可配置每类事件的通知渠道偏好
- [ ] 实现 `NotificationPreference.SetChannelPreference(eventType, channels)`（设置某事件通知渠道）
- [ ] 定义 `ITemplateRenderer` 接口（RenderAsync(template, variables) 返回标题与内容）
- [ ] 定义各仓储接口
- [ ] 提交：`feat(notification): add preference aggregate and renderer interface`

---

## Task 3: 基础设施层 — EF Core 仓储与模板渲染

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/NotificationDbContext.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationTemplateRepository.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationPreferenceRepository.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Services/TemplateRenderer.cs`

- [ ] 实现 `NotificationDbContext`（各 DbSet 配置）
- [ ] 实现各 EF Core 仓储
- [ ] 实现 `TemplateRenderer`（变量插值渲染，支持 `{{variable}}` 占位符语法）
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证模板渲染
- [ ] 提交：`feat(notification): add EF Core repositories and template renderer`

---

## Task 4: 基础设施层 — 站内信渠道

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/InAppChannel.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/IChannel.cs`

- [ ] 定义 `IChannel` 接口（SendAsync(record)）
- [ ] 实现 `InAppChannel`（站内信写入 DB，标记已读状态，Redis 缓存未读计数）
- [ ] 编写单元测试
- [ ] 提交：`feat(notification): add in-app notification channel`

---

## Task 5: 基础设施层 — 短信渠道

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/Sms/SmsClient.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/Sms/SmsOptions.cs`

- [ ] 实现 `SmsChannel`（调用短信服务商 API 发送短信）
- [ ] 实现 `SmsClient`（HTTP Client 调用，支持阿里云/腾讯云短信 SDK 抽象）
- [ ] 实现 `SmsOptions`（配置驱动：Provider、AccessKey、Secret、SignName、TemplateCode）
- [ ] 配置 Polly 重试策略（3 次指数退避）
- [ ] 编写单元测试 Mock 短信 API
- [ ] 提交：`feat(notification): add SMS notification channel`

---

## Task 6: 基础设施层 — 邮件渠道

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/Email/SmtpClientWrapper.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Channels/Email/EmailOptions.cs`

- [ ] 实现 `EmailChannel`（SMTP 发送邮件，支持 HTML 模板）
- [ ] 实现 `SmtpClientWrapper`（MailKit 封装，异步发送）
- [ ] 实现 `EmailOptions`（配置驱动：SmtpHost、Port、Username、Password、FromAddress、EnableSsl）
- [ ] 配置 Polly 重试策略
- [ ] 编写单元测试 Mock SMTP
- [ ] 提交：`feat(notification): add email notification channel`

---

## Task 7: 基础设施层 — 事件消费者

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/UserEventConsumer.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/OrderEventConsumer.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PaymentEventConsumer.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PromotionEventConsumer.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/PointsEventConsumer.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/AfterSalesEventConsumer.cs`

- [ ] 实现 `UserEventConsumer`（消费 UserRegisteredEvent→欢迎通知、UserSuspendedEvent→暂停通知）
- [ ] 实现 `OrderEventConsumer`（消费 OrderCreatedEvent→下单成功通知、OrderShippedEvent→发货通知、OrderCompletedEvent→收货确认/评价邀请）
- [ ] 实现 `PaymentEventConsumer`（消费 PaymentSucceededEvent→支付成功通知、PaymentFailedEvent→支付失败提醒）
- [ ] 实现 `PromotionEventConsumer`（消费 SeckillOrderCreatedEvent→秒杀成功通知、CouponIssuedEvent→优惠券到账通知）
- [ ] 实现 `PointsEventConsumer`（消费 PointsEarnedEvent→积分到账通知、MemberLevelUpgradedEvent→升级通知、MembershipActivatedEvent→会员激活通知）
- [ ] 实现 `AfterSalesEventConsumer`（消费 AfterSalesApprovedEvent→售后通过通知、RefundCompletedEvent→退款到账通知）
- [ ] 各消费者：查模板→查偏好→渲染→选渠道→发送→记录
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试验证事件消费链路
- [ ] 提交：`feat(notification): add event consumers for all domains`

---

## Task 8: 基础设施层 — 发送调度与重试

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs`

- [ ] 实现 `NotificationDispatchJob`（轮询待发送通知，按渠道分发，异步执行）
- [ ] 实现 `NotificationRetryJob`（轮询失败通知，按 RetryCount 决定重试或放弃）
- [ ] 超过最大重试次数（默认 3 次）的通知标记 Abandoned 并告警
- [ ] 编写集成测试
- [ ] 提交：`feat(notification): add notification dispatch and retry jobs`

---

## Task 9: 应用层 — 通知查询与模板管理用例

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Application/INotificationAppService.cs`
- Create: `src/Services/Notification/Leno.Notification.Application/INotificationTemplateAppService.cs`
- Create: `src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs`
- Create: `src/Services/Notification/Leno.Notification.Application/Services/NotificationTemplateAppService.cs`

- [ ] 实现 `GetNotificationsAsync(userId)`（用户站内信列表，含未读计数）
- [ ] 实现 `MarkAsReadAsync(recordIds)`（标记已读）
- [ ] 实现 `MarkAllAsReadAsync(userId)`（全部已读）
- [ ] 实现通知模板管理用例（运营 CRUD 模板，含预览渲染）
- [ ] 实现用户通知偏好管理用例
- [ ] 编写单元测试
- [ ] 提交：`feat(notification): add notification query and template management services`

---

## Task 10: 表现层 — API 控制器

**文件:**
- Create: `src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs`
- Create: `src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs`

- [ ] 实现 `NotificationsController`（GET /api/notifications、POST /api/notifications/read、POST /api/notifications/read-all、GET /api/notifications/unread-count）
- [ ] 实现 `NotificationTemplatesController`（运营端 CRUD /api/admin/notification-templates）
- [ ] 实现偏好管理接口（GET/PUT /api/users/me/notification-preferences）
- [ ] 配置 JWT 鉴权与角色策略
- [ ] 编写 API 集成测试
- [ ] 提交：`feat(notification): add API controllers`
