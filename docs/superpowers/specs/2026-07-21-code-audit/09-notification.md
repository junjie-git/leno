# Notification（消息通知域）代码分析报告

## 概述
- 扫描范围：src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/
- 代码行数（业务，非测试、非 Migrations）：约 7137 行
- 问题总数：高 12 / 中 18 / 低 9

## 🔴 高风险问题

### 1. DI 注册导致 SmsChannel 重复键异常，全渠道调度链路必崩
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L109-L116`
  关联触发点：`src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L70`、`src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs#L53`、`src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs#L107`、`src/Services/Notification/Leno.Notification.Application/Services/DeadLetterAppService.cs#L66`
- **类别**：A2 异常处理不当 / B8 仓储滥用 / C7 资源/连接池
- **根因**：`AliyunSmsChannel` 与 `TencentSmsChannel` 同时以 `INotificationChannel` 注册到 DI（Lines 115-116），两者都返回 `Channel => NotificationChannel.Sms`（见 `Channels/SmsChannel.cs#L33` 与 `#L120`）。所有依赖方使用 `_channels.ToDictionary(c => c.Channel)` 时，因两次插入相同的 `NotificationChannel.Sms` 键，`Dictionary` 抛出 `ArgumentException: An item with the same key has already been added`。
- **影响**：每次 `NotificationDispatcher.DispatchAsync` 调用、每次 `NotificationDispatchJob.ExecuteAsync`、每次 `NotificationRetryJob.ProcessScheduledRetriesAsync`、每次 `DeadLetterAppService.BatchResendAsync` 都会崩溃。**通知域全部出站调度功能在生产环境不可用**——不仅短信不能发，邮件、站内信调度也会一并失效。
- **修复建议**：让 `AliyunSmsChannel` 与 `TencentSmsChannel` 共用一个外壳类（如 `SmsChannel(ISmsProvider provider)`），按 `IChannelSelector.SelectProvider` 在运行时决定使用哪家；或改用 `_channels.ToLookup(c => c.Channel)` 后再按 provider 选择；或只注册主用实现，备用通过 failover 内部切换。
- **影响范围**：`INotificationDispatcher`、`NotificationDispatchJob`、`NotificationRetryJob`、`DeadLetterAppService`、`NotificationConfigAppService.TestSendAsync`、`NotificationRecordsController.ResendRecordAsync`。

### 2. EmailChannelOptions / SmsChannelOptions 字段名与 appsettings.json 不匹配，邮件与短信均无法发送
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L105-L106` 与 `src/Services/Notification/Leno.Notification.Api/appsettings.json#L63-L80`
- **类别**：A2 异常处理不当 / C3 缓存策略（配置）
- **根因**：
  - `EmailChannelOptions` 字段为 `Host / Port / Username / Password / From / UseSsl`（见 `Channels/EmailChannelOptions.cs#L9-L24`），但 `appsettings.json` 中 `Notification:Email` 节点的键是 `SmtpHost / Port / Username / Password / FromAddress / EnableSsl`。`Host`、`From`、`UseSsl` 都绑定不到值，`Host` 永远是 `string.Empty`。
  - `SmsChannelOptions` 字段为 `Provider / AccessKeyId / AccessKeySecret / SignName`（见 `Channels/SmsChannelOptions.cs#L8-L18`），但 `appsettings.json` 中 `Notification:Sms` 节点的键是 `Provider / AccessKey / Secret / SignName / TemplateCode / Endpoint`。`AccessKeyId`、`AccessKeySecret` 永远是 `string.Empty`。
- **影响**：`SmtpEmailChannel.SendAsync` 第 44 行 `if (string.IsNullOrWhiteSpace(_options.Host))` 永远成立，所有邮件发送返回 `EMAIL_CONFIG_MISSING`。`AliyunSmsChannel.SendAsync` 第 47 行同理，所有短信发送返回 `SMS_CONFIG_MISSING`。运营管理员无法通过界面修改，因为 `NotificationConfigAppService.UpdateConfigAsync` 也不持久化（见中等风险 #9）。
- **修复建议**：统一字段名（推荐 appsettings 改用 `Host / From / UseSsl / AccessKeyId / AccessKeySecret`，与 Options 类对齐）；或将 Options 类字段重命名匹配 appsettings；或使用 `[ConfigurationKeyName("SmtpHost")]` 特性显式映射。
- **影响范围**：`SmtpEmailChannel`、`AliyunSmsChannel`、`TencentSmsChannel` 全部渠道发送能力。

### 3. MassTransit 消费者重复订阅，每条集成事件被处理两次
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L14-L26` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/{Order,User,Payment,AfterSales,Promotion,Points}EventConsumer.cs`、注册入口 `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L158-L164`
- **类别**：A7 异步消息可靠性 / B5 CQRS 职责混乱
- **根因**：`NotificationEventConsumer` 实现了 12 个事件的 `IConsumer<T>` 接口，并已注册；同时 `OrderEventConsumer`、`UserEventConsumer`、`PaymentEventConsumer`、`AfterSalesEventConsumer`、`PromotionEventConsumer`、`PointsEventConsumer` 也分别实现了相同事件接口并全部注册。MassTransit 默认每个 Consumer 类型对应一个临时队列 + 订阅，同一条事件会被分发给所有订阅者，因此 `OrderCreatedEvent` 会同时进入 `OrderEventConsumer` 与 `NotificationEventConsumer` 两个队列并各自处理一次。
- **影响**：每条事件触发两次 `INotificationService.SendAsync` 调用。虽然 `IdempotencyKey = evt.EventId.ToString()` 兜底去重（见 `NotificationService.cs#L54-L68`），但该去重存在并发竞争（见中等风险 #15），仍可能产生重复通知；且浪费一倍 DB 查询/写入与日志开销，对秒杀、促销等高峰场景放大负载。
- **修复建议**：二选一：① 删除 `NotificationEventConsumer`，保留按 BC 拆分的专用 Consumer；② 删除所有专用 Consumer，仅保留 `NotificationEventConsumer`。然后删除 `ServiceCollectionExtensions.cs#L158-L164` 中冗余的 `AddConsumer` 调用。
- **影响范围**：所有事件订阅与通知发送链路。

### 4. OrderEventConsumer 处理 OrderCancelledEvent 时 UserId 强制为 Guid.Empty，必定失败
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Consumers/OrderEventConsumer.cs#L99-L119`
- **类别**：A1 空引用与边界条件 / A7 异步消息可靠性
- **根因**：第 108 行 `UserId = Guid.Empty`，注释为“通知发送给买家需通过订单查询，这里使用事件中的 sellerId 作为 fallback”——但 `OrderCancelledEvent` 实际并未携带可用的 `SellerId`，且代码直接以 `Guid.Empty` 调用 `_notificationService.SendAsync(request, ...)`。`NotificationRecord.Create` 第 102-105 行明确校验 `userId == Guid.Empty` 抛 `NotificationDomainException("UserId 不可为空", "NOTIFICATION_USER_EMPTY")`。`NotificationService.SendAsync` 未捕获 `NotificationDomainException`，异常向上冒泡到 MassTransit。
- **影响**：所有 `OrderCancelledEvent` 处理必定抛异常，经 MassTransit 重试 3 次（`appsettings.json#L82-L86`）后进入死信队列。订单取消后用户永远收不到通知，且占用 MQ 重试资源。
- **修复建议**：从 `OrderCancelledEvent` 中正确提取买家 ID（如需查询订单仓储，应注入 `IOrderQueryService` 防腐层）；或在事件契约中增加 `BuyerId` 字段；或在 `OrderEventConsumer` 中先做 `if (evt.BuyerId == Guid.Empty) { _logger.LogWarning(...); return; }` 跳过。
- **影响范围**：`OrderEventConsumer`、订单取消全链路。

### 5. NotificationCallbacksController 回执不持久化，ApplyReceipt 状态变更只在内存
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L82-L110`
- **类别**：A8 事务边界 / B6 层依赖反向
- **根因**：`ProcessReceiptAsync` 第 105 行调用 `_recordRepository.UpdateAsync(record, ct)` 仅将实体标记为 `EntityState.Modified`，但**未调用 `IUnitOfWork.SaveChangesAsync(ct)`**。控制器注入了 `INotificationRecordRepository`、`IConfiguration`、`ILogger` 但未注入 `IUnitOfWork`，故无法保存。EF Core ChangeTracker 在请求结束、`DbContext` 被 Dispose 时丢弃所有变更。
- **影响**：渠道回执（邮件送达成功、短信回执失败）永远不写库。`NotificationRecord` 永远停留在 `Sending` 状态，重试 Job 不会拾取（`GetRetryableAsync` 只查 `Failed` 状态），运营在管理后台永远看到“发送中”的记录，无法识别真正失败的通知。
- **修复建议**：注入 `IUnitOfWork` 并在第 105 行后调用 `await _unitOfWork.SaveChangesAsync(ct);`；更优做法是将回执处理下沉到一个新的 `IReceiptAppService` 应用服务，控制器只做协议适配。
- **影响范围**：所有邮件/短信回执处理。

### 6. NotificationCallbacksController 默认回调密钥硬编码，可伪造回执
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L112-L124`
- **类别**：A2 异常处理不当（安全）
- **根因**：第 119 行 `var secret = _configuration["Notification:CallbackSecret"] ?? "LenoNotificationCallbackSecret2024";`——配置缺失时回退到源码内可见的硬编码默认密钥。该密钥同时作为 HMAC-SHA256 的 key（第 128 行）和参与签名的 raw 字符串（第 120 行），任何拿到源码的攻击者均可构造合法签名，将任意 `ChannelMessageId` 标记为“送达成功”或“送达失败”。此外第 121 行 `raw` 包含 `timestamp` 但**未校验时间戳新鲜度**（无防重放），攻击者可无限次重放历史回执。
- **影响**：攻击者可伪造任意通知记录的送达状态，污染运营统计、掩盖真实送达失败；也可将成功记录标记为失败触发不必要的重试。
- **修复建议**：① 删除默认 fallback，启动时校验 `Notification:CallbackSecret` 必须配置，缺失则抛异常拒绝启动；② 加入时间戳新鲜度校验（如 ±5 分钟）；③ 在 Redis 中缓存已处理 `ChannelMessageId + Timestamp` 短期去重；④ 将密钥仅作为 HMAC key，不要同时拼入 raw。
- **影响范围**：`/api/notifications/callbacks/email`、`/api/notifications/callbacks/sms` 两个公开端点。

### 7. NotificationRecordsController.ResendRecordAsync 只改状态不真正发送，死信重发后永久滞留 Sending
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L100-L133`
- **类别**：A4 状态机非法迁移 / A7 异步消息可靠性
- **根因**：第 125 行调用 `record.MarkResend()` 将状态从 `DeadLettered` 迁移到 `Sending`（见 `NotificationRecord.cs#L255-L269`），随后第 126-127 行 `UpdateAsync` + `SaveChangesAsync` 持久化。但**控制器未调用任何 `INotificationChannel.SendAsync`**，注释“死信已重新发送”是误导。`NotificationDispatchJob` 第 66 行只查询 `Status == NotificationStatus.Pending` 的记录，`NotificationRetryJob` 第 109 行只查询 `Status == NotificationStatus.Retried` 的记录，没有任何 Job 会拾取 `Sending` 状态的记录。
- **影响**：运营点击“重发”后，记录从死信变成 Sending 状态后永久卡死，既不会被发送，也无法再次进入死信流程（`MarkResend` 要求 `Status == DeadLettered`）。死信记录被“救活”后又无法继续流转，比不重发还糟糕。
- **修复建议**：要么在控制器中直接调用 `INotificationChannel.SendAsync` 完成发送（参考 `DeadLetterAppService.BatchResendAsync` 第 95-108 行的写法，但需修复其状态机问题）；要么将状态改为 `Pending` 而非 `Sending`，让 `NotificationDispatchJob` 接管实际发送。
- **影响范围**：`/api/admin/notifications/records/{id}/resend` 端点。

### 8. NotificationService.SendAsync 超时分支把记录永久滞留在 Sending 状态
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L131-L166`
- **类别**：A4 状态机非法迁移 / A7 异步消息可靠性
- **根因**：第 132 行 `record.MarkSending()` 先把内存中状态从 `Pending` 改为 `Sending`。第 134-135 行创建 3 秒超时 CTS。当 `channel.SendAsync` 在 3 秒内未返回，进入第 150-166 行的超时分支：仅 `UpdateAsync` + `SaveChangesAsync` 保存当前 Sending 状态，返回 `Succeeded = true, ErrorCode = "ACCEPTED_TIMEOUT"`。注释称“异步处理中”，但**没有任何调度器拾取 Sending 状态的记录**。`NotificationDispatchJob` 只查 Pending，`NotificationRetryJob` 只查 Retried。
- **影响**：所有 3 秒未完成的渠道发送（如 SMTP 慢响应、SMS HTTP 慢响应）都会让记录永久卡在 Sending 状态，运营看到的是“发送中”永远不结束，无法识别真实结果。返回 `Succeeded = true` 也具有误导性——调用方以为发送成功。
- **修复建议**：超时时应调用 `record.MarkFailed("发送超时", "ACCEPTED_TIMEOUT")` 让其进入 Failed 状态，由重试 Job 后续处理；或将记录状态保持为 Pending 让 DispatchJob 重试；或新增一个 `Sending` 状态的清理 Job。
- **影响范围**：`INotificationService.SendAsync` 的所有调用方，包括 `NotificationSendController` 与全部事件 Consumer。

### 9. AliyunSmsChannel/TencentSmsChannel 硬编码模板编码，模板系统的 SmsTemplateCode 字段完全失效
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L58-L64`（Aliyun）与 `#L145-L151`（Tencent）
- **类别**：A1 边界条件 / B3 防腐层缺失
- **根因**：Aliyun 第 62 行 `TemplateCode = "SMS_000000"`，Tencent 第 149 行 `TemplateId = "000000"`，两者都是硬编码占位。`NotificationTemplate` 聚合根有 `SmsTemplateCode` 字段（`Aggregates/NotificationTemplate.cs#L29-L30`），但 `ChannelSendRequest` 值对象（`ValueObjects/ChannelSendRequest.cs`）并未携带该字段，`AliyunSmsChannel` 无法获取实际模板编码。
- **影响**：所有短信使用同一模板编码调用服务商，阿里云/腾讯云会因模板不匹配返回错误；即便配置正确，业务也无法区分“订单创建短信”和“支付成功短信”——两者发送的内容字段完全相同（都是 `request.Body` 拼到 `TemplateParam`）。
- **修复建议**：在 `ChannelSendRequest` 中增加 `string? SmsTemplateCode` 字段，由 `NotificationDispatcher` 与 `NotificationService` 从 `NotificationTemplate.SmsTemplateCode` 透传；`AliyunSmsChannel`/`TencentSmsChannel` 优先使用该字段，缺失时返回 `SMS_CONFIG_MISSING` 错误而非使用占位。
- **影响范围**：`AliyunSmsChannel`、`TencentSmsChannel`、所有短信通知。

### 10. AliyunSmsChannel/TencentSmsChannel 用响应体当 ChannelMessageId，回执匹配永远失败
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L75-L79`（Aliyun）与 `#L162-L166`（Tencent）
- **类别**：A1 边界条件 / B7 事件契约一致性
- **根因**：发送成功时 `return new ChannelSendResult(true, null, null, responseContent);` —— `responseContent` 是整个 HTTP 响应体（JSON 字符串，可能含换行/特殊字符），被赋给 `ChannelMessageId`。`NotificationRecord.ChannelMessageId` 配置 `HasMaxLength(128)`（`Configurations/NotificationRecordConfiguration.cs#L34`），实际写入会被截断。回执回调通过 `GetByChannelMessageIdAsync(channelMessageId)` 精确匹配查询（`EfCoreNotificationRecordRepository.cs#L144-L145`），截断后的字符串与渠道回传的 messageId 不匹配。
- **影响**：所有短信回执无法匹配到通知记录，`NotificationCallbacksController` 第 91 行返回 404，回执被丢弃；`NotificationRecord.Status` 永远停留在 `Sending`，与问题 #5 叠加导致短信状态完全不可观测。
- **修复建议**：从阿里云/腾讯云响应 JSON 中解析真正的 `BizId` / `SerialNo` 字段作为 `ChannelMessageId`；解析失败时返回 `null` 并以 `SMS_EXCEPTION` 标记。
- **影响范围**：所有短信发送的回执链路。

### 11. 通知模板 (Code, Channel) 索引未声明唯一，多启用模板时返回不确定
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationTemplateConfiguration.cs#L45` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationTemplateRepository.cs#L26-L28`、`#L60-L62`
- **类别**：A5 边界条件 / B2 聚合设计违规 / C2 缺失索引
- **根因**：`HasIndex(t => new { t.Code, t.Channel })` 未调用 `IsUnique()`。`GetEnabledAsync(code, channel)` 与 `GetEnabledByCodeAsync(code)` 都用 `FirstOrDefaultAsync`，若 DBA 或运营误录入了同一 code+channel 的两个 Enabled 模板，返回结果不确定。`NotificationService.SendAsync` 第 71 行使用 `GetEnabledByCodeAsync` 忽略 channel 维度，更增加了不确定性。
- **影响**：模板配置错误时发送内容不可预测，且无任何告警。模板修改日志（`OperatorId` 字段）也无法追溯。
- **修复建议**：将索引改为 `HasIndex(t => new { t.Code, t.Channel }).IsUnique()`；并在 `NotificationTemplate.Create` 与 `Update` 工厂方法中通过领域服务校验同 code+channel 不存在其他 Enabled 模板（或仅允许一个 Enabled）。
- **影响范围**：`NotificationTemplate` 聚合、`NotificationService.SendAsync`、`NotificationDispatcher.DispatchAsync`。

### 12. NotificationRecordsController.GetByIdAsync 全表加载后内存查找单条模板
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L36-L46`
- **类别**：C1 N+1 查询 / C4 大对象/全表扫
- **根因**：`GetByIdAsync` 调用 `_templateAppService.QueryTemplatesAsync(null, null, 1, int.MaxValue, ct)` 加载所有模板到内存，再 `result.Items.FirstOrDefault(t => t.TemplateId == templateId)`。`int.MaxValue` 作为 pageSize 传给 EF Core 会被翻译为 `TOP (int.MaxValue)`，几乎等同于全表扫描。`INotificationTemplateAppService` 接口（`Application/INotificationTemplateAppService.cs`）也确实没有按 ID 查询的方法。
- **影响**：模板表数据增长后，每次按 ID 查询都把全表读入应用内存，CPU/内存/网络压力线性放大。`CreatedAtAction`（第 33 行）创建模板后会跳到该方法，所以每次创建模板都触发一次全表扫。
- **修复建议**：在 `INotificationTemplateAppService` 增加 `Task<NotificationTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken ct)` 方法，实现中调用 `_templateRepository.GetByIdAsync` 直接走主键查询。
- **影响范围**：`/api/admin/notification-templates/{templateId}` 与创建模板后的 Location 跳转。

## 🟡 中风险问题

### 13. ChannelSelector.NormalizeProvider 死代码 + 首字母大写未实现
- **位置**：`src/Services/Notification/Leno.Notification.Domain/Services/ChannelSelector.cs#L143-L148`
- **类别**：A1 边界条件
- **根因**：`return provider.Trim();` 后的注释 `// Simple normalization: capitalize first letter` 是死代码，永远不会执行。`NormalizeProvider` 仅做 Trim，未做大写化。若 `appsettings.json` 中 `Notification:Sms:Provider` 配置为 `"aliyun"`（小写），`GetSmsFallback` 第 134-140 行 switch 不匹配 `"Aliyun"` 也不匹配 `"Tencent"`，返回 `null`，导致短信永远无 failover。
- **影响**：failover 在配置大小写不规范时静默失效。
- **修复建议**：实现首字母大写：`return char.ToUpperInvariant(provider.Trim()[0]) + provider.Trim()[1..];` 或用 `string.Equals(..., StringComparison.OrdinalIgnoreCase)` 比较。
- **影响范围**：`ChannelSelector`、SMS failover 流程。

### 14. EfCoreNotificationRecordRepository.GetRetryableAsync 用 DefaultMaxRetry 常量而非聚合自身 MaxRetry
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L73-L80`
- **类别**：B2 聚合设计违规 / A5 边界条件
- **根因**：查询条件 `n.RetryCount < NotificationRecord.DefaultMaxRetry`（值为 3）。`NotificationRecord.MaxRetry` 字段允许在 `Create` 工厂方法传入自定义值（`Aggregates/NotificationRecord.cs#L95`），但 SQL 查询无法用聚合内字段做条件，硬编码常量。当前 `NotificationService` / `NotificationDispatcher` 都未传 maxRetry，所有记录 MaxRetry=3，问题被掩盖；一旦未来需要按业务调整 MaxRetry，重试 Job 立即失灵。
- **影响**：自定义 MaxRetry 的记录会在 RetryCount=DefaultMaxRetry 后不再被重试 Job 拾取，永远停留在 Failed 状态。
- **修复建议**：将查询改为 `n.RetryCount < n.MaxRetry`（EF Core 可翻译为 SQL）；或在 `NotificationRecord.CanRetry` 上加 `[NotMapped]` 表达式列。
- **影响范围**：`NotificationRetryJob`、`NotificationRecord` 自定义 MaxRetry 场景。

### 15. IdempotencyKey 索引非唯一 + 幂等检查无锁，并发请求产生重复通知
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L48` 与 `src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L53-L68`、`src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L102-L103`
- **类别**：A3 并发与竞态 / C6 Outbox/幂等性
- **根因**：`HasIndex(n => n.IdempotencyKey)` 未声明 `IsUnique()`。`NotificationService.SendAsync` 先 `GetByIdempotencyKeyAsync` 检查再 `AddAsync` 创建，两步之间无事务、无锁、无唯一约束。两个并发请求（如问题 #3 的双消费者场景）同时检查都未命中，都会创建记录。
- **影响**：同一业务事件触发重复通知，损害用户体验，短信/邮件配额浪费。
- **修复建议**：① 索引改为 `HasIndex(n => n.IdempotencyKey).IsUnique().HasFilter("[idempotency_key] IS NOT NULL")`；② 在 `NotificationService.SendAsync` 用 `BeginTransactionAsync` + 隔离级别 `Serializable` 或在 Insert 时使用 `INSERT ... ON CONFLICT DO NOTHING`；③ 用 Redis 分布式锁包裹幂等检查与创建。
- **影响范围**：`NotificationService.SendAsync`、所有事件 Consumer。

### 16. ChannelMessageId 缺索引，回执匹配全表扫
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L44-L48` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L144-L145`
- **类别**：C2 缺失索引
- **根因**：`NotificationRecordConfiguration` 索引列表中没有 `ChannelMessageId`。`GetByChannelMessageIdAsync` 用 `FirstOrDefaultAsync(n => n.ChannelMessageId == channelMessageId)` 查询，无索引支持。记录数增长后回执回调端点性能急剧下降。
- **影响**：回执处理延迟，MQ/HTTP 调用方超时；高并发回执时数据库 CPU 飙升。
- **修复建议**：在 `NotificationRecordConfiguration` 增加 `builder.HasIndex(n => n.ChannelMessageId).HasDatabaseName("ix_notification_records_channel_message_id");`。
- **影响范围**：`NotificationCallbacksController`、所有回执处理。

### 17. (Status, NextRetryAt) / (Status, RetryCount) 复合索引缺失，重试 Job 拾取低效
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L44-L48` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L106-L115`、`#L73-L80`
- **类别**：C2 缺失索引 / C4 大对象/全表扫
- **根因**：仅有 `Status` 单列索引。`GetRetriedWithExpiredNextRetryAsync` 查询条件 `Status == Retried AND NextRetryAt <= UtcNow`，`GetRetryableAsync` 查询条件 `Status == Failed AND RetryCount < DefaultMaxRetry`。Status 单列索引对二级条件无帮助，需扫描该 Status 下的全部记录。`NotificationRetryJob` 每 N 秒跑一次，状态为 Failed/Retried 的记录可能很多（高峰失败积压时）。
- **影响**：重试 Job 在积压场景下查询变慢，进一步加剧积压。
- **修复建议**：增加复合索引 `HasIndex(n => new { n.Status, n.NextRetryAt })` 与 `HasIndex(n => new { n.Status, n.RetryCount })`。
- **影响范围**：`NotificationRetryJob`、`NotificationDispatchJob`。

### 18. RateLimitAppService 用 static 内存字典存储限流配置，不持久化且线程不安全
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/RateLimitAppService.cs#L16-L39`、`#L63-L78`
- **类别**：A3 并发与竞态 / C3 缓存策略
- **根因**：`DefaultConfigs` 是 `static readonly Dictionary<...>`，`UpdateRateLimitAsync` 第 66-71 行直接修改字典内对象属性。问题：① 修改不持久化，进程重启丢失；② 多实例部署时各实例配置不一致；③ `Dictionary` 不是线程安全的，并发读写可能死锁或数据损坏；④ `GetRateLimitAsync` 返回的是同一引用，调用方修改会污染全局。
- **影响**：运营通过 `PUT /api/admin/notification-rate-limits` 修改的配置仅在当前进程内存中生效，重启后丢失，其他实例不感知；并发修改可能抛异常。
- **修复建议**：将限流配置持久化到 DB（新增 `notification_rate_limit_configs` 表）或 Consul KV；用 `IOptionsMonitor<T>` 模式 + `ConcurrentDictionary`；或使用 `IMemoryCache` 配合分布式配置中心。
- **影响范围**：`RateLimitAppService`、所有调用方。

### 19. NotificationConfigAppService.UpdateConfigAsync 不持久化，配置修改形同虚设
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationConfigAppService.cs#L57-L84`
- **类别**：A8 事务边界 / C3 缓存策略
- **根因**：方法注释明确说“Options 模式中 IOptionsMonitor 不直接支持运行时修改...这里提供一个实现框架”。实际仅写审计日志（第 65、81 行），不调用任何持久化逻辑，不刷新 `IOptionsMonitor` 源。运营改了配置后，下次发送仍用旧配置。
- **影响**：`PUT /api/admin/notification-config` 端点完全无效，运营无法在线修改 SMTP/AccessKey 等配置。
- **修复建议**：① 将配置持久化到 DB 表 + 通过 `IOptionsMonitor` 的自定义 `IOptionsChangeTokenSource` 触发热重载；或 ② 写入 Consul KV，由 Consul Provider 推送到各实例。
- **影响范围**：`NotificationConfigAppService`、所有渠道配置。

### 20. IRateLimiter 注册但从未被调用，限流形同虚设
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L122` 与 `src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L51-L184`、`src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L54-L116`
- **类别**：B5 CQRS 职责混乱 / C8 限流/熔断
- **根因**：`RedisRateLimiter` 实现了完整的滑动窗口限流（基于 Redis Sorted Set），并注册到 DI。但 `NotificationService.SendAsync` 与 `NotificationDispatcher.DispatchAsync` 都未注入 `IRateLimiter`、未调用 `AcquireAsync`。所有通知发送完全绕过限流。
- **影响**：恶意攻击者或上游 Bug 可触发海量短信/邮件发送，浪费配额、触发服务商封禁、轰炸用户。
- **修复建议**：在 `NotificationService.SendAsync` 第 4 步（创建记录后、调用渠道前）注入 `IRateLimiter.AcquireAsync(recipient, templateCode, channel)`，被拒绝时不调用渠道、记录状态为 `Failed` 并附 `RATE_LIMITED` 错误码。
- **影响范围**：所有通知发送链路。

### 21. NotificationAppService.GetNotificationsAsync Total 与过滤条件不一致
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs#L33-L47`
- **类别**：A1 边界条件
- **根因**：第 35 行 `QueryByUserAsync(userId, isRead, ...)` 按 `isRead` 过滤；第 36 行 `CountByUserAsync(userId, null, ct)` **不传 isRead**，返回的是该用户全部站内信总数。当用户在“未读”页（isRead=false）翻页时，Total 是已读+未读总数，分页元数据与列表内容不匹配，前端无法正确渲染分页控件。
- **影响**：前端分页错误，运营/用户对数据量判断失真。
- **修复建议**：将第 36 行改为 `await _recordRepository.CountByUserAsync(userId, isRead, ct);`，让 Total 与过滤条件一致。
- **影响范围**：`/api/notifications` 端点。

### 22. NotificationAppService.MarkAsReadAsync N+1 查询
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/NotificationAppService.cs#L50-L65`
- **类别**：C1 N+1 查询
- **根因**：`foreach` 中对每个 recordId 调用 `GetByIdAsync` + `UpdateAsync`。100 条记录触发 100 次 SELECT + 100 次 UPDATE（UPDATE 实际只是 `_context.Update` 不立即执行，但 100 次主键查询必然 N+1）。
- **影响**：批量标记已读接口在高频调用时（如用户每天打开应用一次性标记 50 条）显著增加 DB 压力。
- **修复建议**：在仓储层增加 `Task<List<NotificationRecord>> GetByIdsAsync(List<Guid> ids, ...)` 一次查出；或直接用 `ExecuteUpdateAsync` 走批量 SQL（参考 `MarkAllAsReadAsync` 第 83-88 行的写法，但需注意该写法绕过聚合根的争议）。
- **影响范围**：`/api/notifications/read` 端点。

### 23. DeadLetterAppService.BatchResendAsync 状态机异常导致记录卡死在 Sending
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/DeadLetterAppService.cs#L94-L119`
- **类别**：A4 状态机非法迁移 / A8 事务边界
- **根因**：第 95 行 `record.MarkResend()` 先将状态从 DeadLettered 改为 Sending；第 96 行 `BuildChannelSendRequestAsync` 在 try 内但 `MarkResend` 之前。若 `BuildChannelSendRequestAsync` 抛出（如用户联系方式服务失败），catch 第 113-118 行只记录 `Errors.Add(...)`，**不重置状态**。第 121 行 `SaveChangesAsync` 会把 Sending 状态写入 DB。
- **影响**：异常路径下的死信记录被“救活”到 Sending 状态后永久卡死（与问题 #7 同因）。即便正常路径下 `MarkFailed` 被调用，第 107 行 `UpdateAsync` 之前状态已是 Sending，`MarkFailed` 第 187 行校验 `Status != NotificationStatus.Sending` 抛异常，进入 catch 又是 Sending 状态——又卡死。
- **修复建议**：将 `MarkResend` 调用挪到 `BuildChannelSendRequestAsync` 之后、`sender.SendAsync` 之前；或在 catch 中调用 `record.MoveToDeadLetter("重发失败:" + ex.Message)` 回到死信状态。
- **影响范围**：`/api/admin/dead-letters/batch-resend` 端点。

### 24. NotificationRetryJob.ProcessScheduledRetriesAsync MarkSending 在 try 块外
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs#L108-L165`
- **类别**：A2 异常处理不当 / A4 状态机非法迁移
- **根因**：第 111 行 `record.MarkSending()` 在 try 块外。若状态机校验抛出（如记录已被另一实例并发改为 Sending），异常直接冒泡，整个 `ProcessScheduledRetriesAsync` 方法终止，后续记录本轮不被处理。`SaveChangesAsync` 第 168 行不执行，但 `MarkSending` 已修改内存中实体的状态——下一轮 Job 拉到的还是 Retried（DB 没改），但若 DbContext 共享，可能污染 ChangeTracker。
- **影响**：单条记录的状态机异常会导致整批重试中断，其他记录被推迟到下一轮。
- **修复建议**：将 `MarkSending` 移入 try 块，并在 catch 中处理状态机异常（如记录日志后 `continue`）。
- **影响范围**：`NotificationRetryJob`。

### 25. NotificationRetryJob / NotificationDispatchJob 无锁并发，多实例重复拾取
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs#L47-L83` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs#L61-L94`、`#L99-L170`
- **类别**：A3 并发与竞态 / A7 异步消息可靠性
- **根因**：`GetPendingAsync` / `GetRetryableAsync` / `GetRetriedWithExpiredNextRetryAsync` 都是简单 SELECT，无 `FOR UPDATE` / `SKIP LOCKED`，无乐观锁版本检查。两个 Job 实例（K8s 多副本或一个副本内并发 HostedService）同时拉取同一批记录，各自 `MarkSending` + 调用渠道发送，导致**同一通知被发送两次**。`MarkSending` 不抛异常（两条都从 Pending 转 Sending 各自成功），最终 `SaveChangesAsync` 时第二条覆盖第一条的状态。
- **影响**：用户收到重复短信/邮件；短信配额翻倍消耗。
- **修复建议**：① 在 SQL 层用 `SELECT ... WITH (UPDLOCK, READPAST)` 或 PostgreSQL `FOR UPDATE SKIP LOCKED`；② 在应用层用 Redis 分布式锁包裹记录 ID；③ 引入乐观锁（`AggregateRoot.Version` 字段已存在）在 `SaveChangesAsync` 时检测并发冲突，冲突则放弃。
- **影响范围**：`NotificationDispatchJob`、`NotificationRetryJob`、所有 Job 调度场景。

### 26. NotificationDispatcher.DispatchAsync 一次性 SaveChanges 包裹多条记录，部分失败影响其他
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L69-L116`
- **类别**：A8 事务边界
- **根因**：第 71 行 `foreach` 内对每个 channel 都做 `AddAsync` + `SaveChangesAsync`（第 84-85 行）+ `UpdateAsync` + `SaveChangesAsync`（第 110-111 行）。每个 channel 是独立事务，但若某个 channel 的 `SaveChangesAsync` 抛出（如 DB 死锁），整个 `DispatchAsync` 方法终止，后续 channel 不被处理。已经成功的 channel 状态已落库，但调用方以为整次调度失败。
- **影响**：多渠道偏好（如 Email + Sms 同时发送）场景下，一个渠道失败可能导致另一个渠道不发送。
- **修复建议**：将每个 channel 的处理放入独立 try-catch，单渠道失败不影响其他；或在调度入口用一个总事务包裹元数据写入（如 `NotificationRequest`），渠道发送异步化。
- **影响范围**：`NotificationDispatcher.DispatchAsync`、所有多渠道通知。

### 27. AliyunSmsChannel 缺少阿里云签名算法，生产环境调用必然失败
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L66-L71`（Aliyun）与 `#L153-L157`（Tencent）
- **类别**：A2 异常处理不当 / B3 防腐层缺失
- **根因**：阿里云短信 API 要求 HMAC-SHA1 签名（包含 AccessKeySecret、时间戳、公共参数等），腾讯云要求 TC3-HMAC-SHA256。当前实现仅 `httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.AccessKeyId}")` —— 既没有签名，又把 AccessKeyId 当 Bearer Token 传入。任何对阿里云/腾讯云真实端点的调用都会返回 401/403。
- **影响**：所有短信发送在真实环境 100% 失败，但因返回 `SMS_HTTP_ERROR`（可重试），会进入重试→死信循环，浪费资源。
- **修复建议**：使用阿里云/腾讯云官方 SDK（`AliyunSDK.Core`、`TencentCloud.Sdk`）封装防腐层；或按官方文档实现签名算法。
- **影响范围**：`AliyunSmsChannel`、`TencentSmsChannel`、所有短信发送。

### 28. InAppChannel Redis 失败时返回 Succeeded=true，未读计数缓存与 DB 长期不一致
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Channels/InAppChannel.cs#L35-L49`
- **类别**：A3 并发与竞态 / C3 缓存策略
- **根因**：第 43-48 行 catch 任意异常后仍返回 `ChannelSendResult(true, null, null, null)`。`NotificationRecord.MarkSucceeded` 被调用，记录状态写入 Succeeded。但 Redis 的 `notification:unread:{userId}` 计数未自增。`NotificationsController.GetUnreadCountAsync` 通过 `INotificationAppService.GetUnreadCountAsync` → `CountByUserAsync` 走 DB 查询，所以未读计数本身没问题；但若有其他模块（如 ApiGateway 聚合）读 Redis 计数，会读到错误值。
- **影响**：Redis 故障期间所有站内信未读计数缓存缺失，恢复后需重建；若有缓存消费者，体验不一致。
- **修复建议**：区分“DB 写入成功”与“缓存更新成功”两个语义。可以保留 Succeeded=true，但记录单独的 `CacheSyncFailed` 状态字段供后台 Job 补偿；或引入定时同步 Job 周期性重建 Redis 计数。
- **影响范围**：`InAppChannel`、未读计数缓存。

### 29. SmtpEmailChannel.AuthenticateAsync 超时不映射为 SMTP_CONNECT_TIMEOUT，错误分类不一致
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs#L67-L83`
- **类别**：A2 异常处理不当
- **根因**：第 67-75 行专门 catch `ConnectAsync` 的 `OperationCanceledException` 返回 `SMTP_CONNECT_TIMEOUT`。但第 77-79 行 `AuthenticateAsync` 与第 82 行 `SendAsync` 没有对应处理，超时会落到第 109 行的通用 catch 返回 `EMAIL_EXCEPTION`。`ChannelSelector.IsRetryableError` 与 `RetryPolicy.ShouldRetry` 把 `SMTP_CONNECT_TIMEOUT` 和 `EMAIL_EXCEPTION` 都判为可重试，但日志与统计维度不一致，运营难以定位是连接问题还是认证问题。
- **影响**：超时错误分类不准确，可观测性下降。
- **修复建议**：将 `AuthenticateAsync` 也包入 try-catch，超时返回 `SMTP_AUTH_TIMEOUT`；或将整个 SMTP 流程的 OperationCanceledException 统一处理。
- **影响范围**：`SmtpEmailChannel.SendAsync`、邮件错误分类。

### 30. SmtpEmailChannel.DisconnectAsync 用 CancellationToken.None，网络故障可能挂起
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Channels/EmailChannel.cs#L82-L83`
- **类别**：A6 资源泄漏 / A2 异常处理不当
- **根因**：第 82 行 `await client.SendAsync(message, linkedCts.Token);` 用 linkedCts（含超时）；第 83 行 `await client.DisconnectAsync(true, CancellationToken.None);` 用 `CancellationToken.None`。如果网络在 SendAsync 后断开，DisconnectAsync 内部的 QUIT 命令会等待 TCP 超时（默认可能数十秒），整个 `SendAsync` 方法阻塞。`using var client = new SmtpClient();` 在方法退出时 Dispose，但 Dispose 也会等待。
- **影响**：网络抖动场景下 SMTP 渠道请求线程被长时间占用，可能耗尽线程池。
- **修复建议**：将 DisconnectAsync 也用 linkedCts.Token；或在 finally 块中 `client.DisconnectAsync(false, ct)` 强制断开。
- **影响范围**：`SmtpEmailChannel.SendAsync`。

### 31. TemplateRenderer.Render 同步方法不校验必填变量，与异步方法行为不一致
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/TemplateRenderer.cs#L42-L50` 与 `#L52-L76`
- **类别**：A1 边界条件 / B7 事件契约一致性
- **根因**：`Render`（同步）第 47-48 行直接调用 `RenderTemplate` 不校验必填变量；`RenderAsync`（异步）第 59 行先调用 `ValidateRequiredVariables`。`NotificationService.SendAsync` 第 88 行使用同步 `Render`，`NotificationTemplateAppService.PreviewAsync` 第 150 行也用同步。前者意味着必填变量缺失也不会报错，渲染后 `{{var}}` 占位符原样保留在标题/内容中，用户收到含 `{{orderId}}` 的通知。
- **影响**：模板配置的“必填变量”校验在主发送链路失效，运营误以为缺失会拦截。
- **修复建议**：① 让 `Render` 也调用 `ValidateRequiredVariables`；或 ② `NotificationService.SendAsync` 改用 `RenderAsync` 并把 `ContentSnapshot` 写入 `NotificationRecord.ContentSnapshot`（聚合根有该字段但当前从未写入）。
- **影响范围**：`NotificationService.SendAsync`、所有事件 Consumer 发送路径。

### 32. TemplateRenderer.Render 标题不 HTML 转义，邮件标题存在 XSS 风险
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/TemplateRenderer.cs#L47-L49`
- **类别**：A2 异常处理不当（安全）
- **根因**：第 47 行 `RenderTemplate(notificationTemplate.Subject, variables, escapeHtml: false);` —— 标题不转义 HTML。`MimeMessage.Subject` 不是 HTML 上下文，但部分邮件客户端（如 Outlook 网页版、Gmail）会对 Subject 做轻度 HTML 解析。如果变量值含 `<script>` 等标签，可能被注入到邮件列表预览。
- **影响**：用户变量被注入到邮件标题，攻击者可构造恶意用户名/订单字段触发存储型 XSS。
- **修复建议**：标题也做 HTML 转义（`escapeHtml: true`），或对 Subject 做单独的纯文本清洗（移除 `<` `>` `&` 等）。
- **影响范围**：所有邮件通知标题。

### 33. NotificationRecordsController / NotificationCallbacksController 越层访问仓储与聚合
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L22-L46`、`#L107-L132` 与 `src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L19-L34`、`#L97`
- **类别**：B6 层依赖反向
- **根因**：`NotificationRecordsController` 直接注入 `INotificationRecordRepository`、`IEnumerable<INotificationChannel>`、`IUnitOfWork`，在控制器内直接调用仓储 + 聚合方法 + 保存变更，绕过应用服务层。`NotificationCallbacksController` 同样直接调用 `INotificationRecordRepository` 与 `record.ApplyReceipt`。控制器本应只做协议适配（HTTP ↔ DTO），业务编排应放应用服务。
- **影响**：业务逻辑散落在控制器，难以复用与测试；事务边界、领域事件发布等横切关注点无法统一处理。
- **修复建议**：新增 `INotificationRecordAppService.ResendRecordAsync(recordId, operatorId)` 与 `IReceiptAppService.HandleEmailReceiptAsync/HandleSmsReceiptAsync`，控制器只转发调用。
- **影响范围**：两个控制器的全部端点。

### 34. ApplyReceipt 失败回执不改状态，记录滞留 Sending
- **位置**：`src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs#L299-L334`
- **类别**：A4 状态机非法迁移
- **根因**：第 318-330 行：`succeeded=true` 时改状态为 Succeeded；`succeeded=false` 时仅设置 `ErrorMessage/ErrorCode`，**不改状态**。若记录当前状态为 Sending（渠道异步发送已 accept 但回执确认失败），状态保持 Sending。`NotificationRetryJob` 不会拾取 Sending 状态记录，永远停留在 Sending。
- **影响**：渠道回执确认失败的通知无法重试，也无法死信，运营看到永远“发送中”。
- **修复建议**：`succeeded=false` 时调用 `MarkFailed("渠道回执确认失败", "CHANNEL_RECEIPT_FAILED")`（需调整 MarkFailed 状态前置条件或在 ApplyReceipt 内直接置为 Failed）。注意 `MarkFailed` 当前要求 `Status == Sending`，ApplyReceipt 已隐含此条件，可复用。
- **影响范围**：`ApplyReceipt`、所有回执失败场景。

### 35. EfCoreNotificationRecordRepository.MarkAllAsReadAsync 绕过聚合根
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L83-L88`
- **类别**：B2 聚合设计违规 / B8 仓储滥用
- **根因**：使用 `ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true))` 直接 UPDATE 数据库，绕过 `NotificationRecord.MarkAsRead()` 聚合方法。`MarkAsRead` 第 244-247 行的渠道校验（仅站内信可标记已读）被跳过——若 DB 中存在 Email/Sms 渠道的记录（虽然按业务不应有 IsRead 字段被设置，但 schema 上允许），该 UPDATE 会把它们的 IsRead 也置为 true，与领域规则冲突。
- **影响**：聚合不变量被绕过，未来若 `MarkAsRead` 增加领域事件或审计字段，ExecuteUpdate 不会触发。
- **修复建议**：保留 `MarkAllAsReadAsync` 的批量优化，但加上 `n.Channel == NotificationChannel.InApp` 过滤条件；或在 SQL 中限定 `WHERE channel = 0`。
- **影响范围**：`NotificationAppService.MarkAllAsReadAsync`。

### 36. NotificationDispatcher 重复创建模板查询与渠道字典
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L70` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs#L53`、`src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationRetryJob.cs#L107`
- **类别**：C7 资源/连接池
- **根因**：每次 `DispatchAsync` 调用都 `_channels.ToDictionary(c => c.Channel)`，构建一个新 Dictionary。Scoped 生命周期下 `INotificationDispatcher` 每次请求都新建，但 `_channels` 是同一组单例渠道实现，反复构建字典浪费 CPU。叠加问题 #1 的重复键异常，这一行直接崩溃。
- **影响**：性能浪费，且异常路径下无法服务。
- **修复建议**：将渠道字典缓存为字段，在构造函数中构建一次；或改用 `IChannelRegistry` 单例服务。
- **影响范围**：所有调度路径。

### 37. NotificationService.SendAsync 渲染失败不创建记录，运营无法追溯
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L86-L99`
- **类别**：A1 边界条件 / C5 异步消息堆积
- **根因**：模板渲染抛异常时直接返回 `TEMPLATE_RENDER_FAILED`，不创建 `NotificationRecord`。运营在管理后台无法查询到这次失败的通知，无法知道哪些用户因模板问题未收到通知。如果 IdempotencyKey 已设置，重试时 `GetByIdempotencyKeyAsync` 找不到记录，会再次尝试渲染，再次失败，进入死循环。
- **影响**：渲染失败的通知“消失”，运营无感知；MQ 重试无意义。
- **修复建议**：渲染失败时也创建 `NotificationRecord`，状态直接置为 `Failed` 并附 `TEMPLATE_RENDER_FAILED` 错误码；或在 `NotificationEventConsumer` 中将 `TEMPLATE_RENDER_FAILED` 视为不可重试，不再 await 抛异常。
- **影响范围**：`NotificationService.SendAsync`、所有事件 Consumer。

### 38. NotificationPreference 偏好聚合未在 NotificationService.SendAsync 中查询使用
- **位置**：`src/Services/Notification/Leno.Notification.Application/Services/NotificationService.cs#L51-L184` 与 `src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L63-L67`
- **类别**：B5 CQRS 职责混乱
- **根因**：`NotificationService.SendAsync`（API/Consumer 入口）**完全不查询 `NotificationPreference`**，直接根据模板的 Channel 发送。`NotificationDispatcher.DispatchAsync` 第 64-67 行才查偏好。两个入口行为不一致：通过 `NotificationSendController` 调用的通知绕过用户偏好，通过事件 Consumer（如 `OrderEventConsumer`）的也绕过（因为它们都调用 `INotificationService`，不调用 `INotificationDispatcher`）。`NotificationDispatcher` 似乎无人调用——`NotificationEventConsumer` 与各专用 Consumer 都直接调用 `INotificationService`。
- **影响**：用户设置的“不接收短信”偏好完全失效；`NotificationDispatcher` 是死代码。
- **修复建议**：统一入口——要么让 `NotificationService.SendAsync` 内部委托 `NotificationDispatcher`，要么删除 `NotificationDispatcher` 并把偏好查询移入 `NotificationService`。
- **影响范围**：所有通知发送路径、用户偏好功能。

## 🟢 低风险问题

### 39. Recipient.Equals 与 GetHashCode 比较算法不一致
- **位置**：`src/Services/Notification/Leno.Notification.Domain/ValueObjects/Recipient.cs#L74-L83`
- **类别**：A1 边界条件
- **根因**：`Equals` 用 `StringComparison.OrdinalIgnoreCase` 比较邮箱；`GetHashCode` 用 `Email.ToUpperInvariant().GetHashCode()`。两者算法不同，少数 Unicode 边缘字符（如土耳其语 dotless i）可能产生不同 hash，违反 `Equals`/`GetHashCode` 契约——两个 Equals 相等的对象 GetHashCode 不等，作为字典 key 时会出现“找不到”的 bug。
- **影响**：极少数用户邮箱含特殊字符时，Recipient 作为字典 key 可能丢失。
- **修复建议**：统一用 `StringComparer.OrdinalIgnoreCase.GetHashCode(Email)` 替换 `Email.ToUpperInvariant().GetHashCode()`。

### 40. NotificationDbContextDesignTimeFactory 硬编码数据库密码
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/NotificationDbContextDesignTimeFactory.cs#L15`
- **类别**：A2 异常处理不当（安全）
- **根因**：连接串 `Server=localhost,1433;Database=LenoNotification;User Id=sa;Password=Leno@SqlServer2019;...` 直接写源码。虽是设计期工厂，但密码真实可见，可能被用于攻击开发/测试环境。
- **影响**：源码泄漏数据库凭据。
- **修复建议**：从环境变量读取，或用占位符 `Password=***`，文档说明需配置。

### 41. NotificationSendController 失败返回 200 OK + body code=400，HTTP 语义混乱
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L63-L69`
- **类别**：A2 异常处理不当
- **根因**：发送失败时 `return Ok(ApiResponse.Fail<SendNotificationResponse>(400, ...))`——HTTP 状态码 200，但 body 中 code=400。调用方按 HTTP 状态码判断会以为成功。
- **影响**：服务间调用的错误处理逻辑混乱，重试逻辑可能失效。
- **修复建议**：失败时返回 `BadRequest(...)`（HTTP 400）。

### 42. RetryPolicy.ShouldRetry 与 ChannelSelector.IsRetryableError 默认保守可重试
- **位置**：`src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs#L96-L98` 与 `src/Services/Notification/Leno.Notification.Domain/Services/ChannelSelector.cs#L116-L118`
- **类别**：A5 边界条件
- **根因**：未知错误码默认返回 `true`（可重试）。本应直接死信的未知错误（如服务商返回业务错误）会被反复重试 3 次再死信，浪费资源。
- **影响**：未知错误占用重试资源 30s × 3 次。
- **修复建议**：保守策略改为“未知错误默认不可重试”，由人工分析后再加入可重试白名单。

### 43. NotificationRecord.MarkAsRead 无幂等保护
- **位置**：`src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationRecord.cs#L242-L250`
- **类别**：A4 状态机非法迁移
- **根因**：`MarkAsRead` 不检查当前 `IsRead` 状态，重复调用不抛异常也不通知。虽然无副作用，但缺少幂等日志难以排查重复调用来源。
- **影响**：可观测性弱。
- **修复建议**：可保持现状（幂等成功），或加 `if (IsRead) return;` 减少不必要的更新触发。

### 44. NotificationTemplate.Update 不允许更新 Code/Channel
- **位置**：`src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationTemplate.cs#L87-L93`
- **类别**：A5 边界条件
- **根因**：`Update` 方法只更新 `subject/body/variables`，`Code` 与 `Channel` 不可变。但未在文档或异常中说明，运营尝试修改 Code 时会困惑——请求成功但 Code 没变。
- **影响**：运营体验差，可能误以为修改成功。
- **修复建议**：在 DTO 层校验 Code/Channel 与现存记录一致，不一致时返回 400；或在 `Update` 方法签名中显式接收 `code`、`channel` 参数并校验不变。

### 45. NotificationPreference.GetChannels 每次返回新列表
- **位置**：`src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationPreference.cs#L84-L92`
- **类别**：C7 资源/连接池
- **根因**：第 91 行 `return [NotificationChannel.InApp];` 每次调用都分配新 `List<T>`。高频调用时（如每条通知都查偏好）增加 GC 压力。
- **影响**：性能微损。
- **修复建议**：缓存为 `static readonly List<NotificationChannel> DefaultChannels = [NotificationChannel.InApp];` 返回不可变引用。

### 46. NotificationTemplate.Update 不校验 SmsTemplateCode 格式
- **位置**：`src/Services/Notification/Leno.Notification.Domain/Aggregates/NotificationTemplate.cs#L87-L93`
- **类别**：A5 边界条件
- **根因**：`SmsTemplateCode` 字段无任何格式校验，可以是任意字符串。结合问题 #9（SmsTemplateCode 从未被使用），即便填错也无感知。
- **影响**：未来修复 #9 后，错误格式会导致短信发送失败。
- **修复建议**：校验 `SmsTemplateCode` 格式（如阿里云 `SMS_` 前缀，腾讯云纯数字），不合法时抛 `NotificationDomainException`。

### 47. NotificationSendController 双路由 Obsolete 注释无迁移计划
- **位置**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L27-L29`
- **类别**：A2 异常处理不当
- **根因**：`[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]` + `[HttpPost("internal/notifications/send")]` 同时挂在方法上。两个路由都生效，但没有迁移脚本或监控告警来跟踪旧路由的调用量下降。“1 周后下线”无法落实。
- **影响**：旧路由长期残留，增加维护成本。
- **修复建议**：在旧路由上添加弃用日志（记录调用方），并设置监控告警；达到下线时间后删除旧路由特性。

## BC 健康度评分

| 维度 | 评分(0-5) | 说明 |
|------|-----------|------|
| 功能正确性 | 1.5 | 渠道 DI 重复键必崩（#1）、配置绑定错位（#2）、回执不持久化（#5）、超时分支卡死（#8）、OrderCancelled 必失败（#4）等多个 P0 级 Bug，生产可用性严重不达标。 |
| DDD 合规 | 2.5 | 聚合根与值对象边界基本清晰，但 `MarkAllAsReadAsync` 绕过聚合（#35）、控制器越层访问仓储（#33）、`NotificationService` 与 `NotificationDispatcher` 双入口且偏好未生效（#38）等暴露分层缺陷。 |
| 性能与可靠性 | 1.5 | 限流注册但未启用（#20）、Job 无锁并发（#25）、IdempotencyKey 无唯一约束（#15）、索引缺失（#16/#17）、N+1 查询（#22）、全表扫单条查询（#12）等多重性能与可靠性隐患。 |
