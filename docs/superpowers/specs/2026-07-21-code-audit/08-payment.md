# Payment（支付集成域）代码分析报告

## 概述
- 扫描范围：src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/
- 代码行数（业务，非测试）：约 6665 行
- 问题总数：高 6 / 中 9 / 低 5

## 🔴 高风险问题

### 1. WeChatPayNotifyHandler 对 V3 JSON 回调误用 XML 解析，导致全部微信回调失败
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L55` 与 `src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L208-L219`
- **类别**：A2 异常处理不当 / A7 异步消息可靠性
- **根因**：`WeChatPayClient` 与 `WeChatPayAdapter.VerifyNotifyAsync` 完全按 APIv3 JSON 协议解析（`JsonDocument.Parse(rawBody)`、AES-GCM 解密 `resource.ciphertext`），但 `WeChatPayNotifyHandler.HandleAsync` 在第 55 行先调用 `ParseXml(rawBody)`。`XDocument.Parse(json)` 对合法 JSON 报文会抛 `XmlException`，被外层 `catch (Exception ex)`（L88-L92）兜住后直接返回 `"FAIL"`，导致微信所有支付/退款异步通知被判定为失败、触发渠道无限重试。
- **影响**：所有微信支付订单将永远停留在 `Pending`/`ChannelOrdered` 状态，仅能依赖 5 分钟一次的 `PaymentStatusCheckJob` 兜底；退款通知同样全部丢失，退款单长期停留在 `Refunding`。
- **修复建议**：删除 `ParseXml` 调用，直接使用 `WeChatPayAdapter.VerifyNotifyAsync` 返回的 `ChannelNotifyResult` 中的字段；若需 `out_trade_no` 等字段，应在 `ChannelNotifyResult` 上扩展或在适配器解密后回填。
- **影响范围**：`WeChatPayNotifyHandler`、`WeChatPayAdapter.VerifyNotifyAsync`、所有微信支付/退款回调路径。

### 2. 微信支付 V3 回调验签传入 APIv2 密钥当作 RSA 公钥，验签必然失败
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayChannel.cs#L104-L108` 与 `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L158-L159`
- **类别**：A2 异常处理不当 / 安全 / B3 防腐层缺失
- **根因**：`ChannelConfigProvider`（`src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs#L36`）将 `PaymentChannelOptions.WeChatPay.ApiKey`（注释明确为“微信 APIv2 密钥”，32 字节字符串）写入 `ChannelConfig.ApiKey`。`WeChatPayChannel.VerifySignatureAsync` 与 `WeChatPayAdapter.VerifyNotifyAsync` 均把该值作为 `publicKey` 传给 `WeChatPayV3SignatureHelper.VerifyNotifySign`，内部调用 `rsa.ImportFromPem(publicKey)`。32 字节 ASCII 串并非合法 PEM，必然抛 `CryptographicException`，被 `catch { return false; }` 吞掉，验签恒为 `false`。
- **影响**：即使问题 1 修复，`NotifyController.WeChatPayNotifyAsync` 仍会在第一道 `WeChatPayChannel.VerifySignatureAsync` 处返回 401；同时 `WeChatPayNotifyHandler` 内部第二道 `WeChatPayAdapter.VerifyNotifyAsync` 也恒返回 `Verified=false`，造成“双重阻断”。
- **修复建议**：在 `ChannelConfig` 增加 `PlatformPublicKeyPem` 字段，从 `WeChatPayOptions` 或独立的“微信支付平台公钥”配置节读取真正的 PEM 公钥；`ChannelConfigProvider` 应同时注入 `IOptions<WeChatPayOptions>`，区分请求签名用私钥与回调验签用平台公钥。
- **影响范围**：`ChannelConfigProvider`、`WeChatPayChannel`、`WeChatPayAdapter`、`WeChatPayNotifyHandler`。

### 3. 支付宝回调验签使用 RSA 私钥而非支付宝公钥，存在密钥滥用与安全隐患
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs#L165`、`src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayChannel.cs#L46`、`src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs#L36`
- **类别**：安全 / B3 防腐层缺失 / B4 共享内核污染
- **根因**：`ChannelOption.ApiKey` 注释为“支付宝 RSA 私钥”，`ChannelConfigProvider` 把它直接写入 `ChannelConfig.ApiKey`。`AlipaySignatureHelper.VerifySign` 把该值当作 `publicKey` 调用 `rsa.ImportFromPem(publicKey)`。虽然 RSA 私钥 PEM 包含公钥分量、`VerifyData` 技术上能通过，但：(1) 应该使用 `AlipayOptions.AlipayPublicKey`；(2) `AlipayOptions` 已在 `ServiceCollectionExtensions.cs#L59` 注册，却从未被 `ChannelConfigProvider` 注入或使用；(3) 把私钥下发到验签路径显著扩大了密钥泄露面。
- **影响**：验签逻辑虽然能“碰巧”通过，但任何接触到 `ChannelConfig.ApiKey` 的代码路径（日志、调试、内存转储）都可能泄露支付宝私钥；同时与支付宝官方“公钥验签、私钥签名”的安全模型不一致。
- **修复建议**：`ChannelConfig` 拆分为 `PrivateKeyPem`（签名）与 `PublicKeyPem`（验签），`ChannelConfigProvider` 注入 `IOptions<AlipayOptions>` 与 `IOptions<WeChatPayOptions>`，分别填充。
- **影响范围**：`ChannelConfigProvider`、`AlipayAdapter`、`AlipayChannel`、`AlipayClient`（出参签名当前正确使用私钥，但来源相同，存在被误用风险）。

### 4. PaymentsController 存在 IDOR 越权：任意 Buyer 可查询他人订单支付结果与退款结果
- **位置**：`src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L42-L66`
- **类别**：安全 / 授权
- **根因**：`GetPaymentResultAsync(orderId)`、`QueryPaymentStatusAsync(paymentId)`、`GetRefundResultAsync(afterSalesId)` 三个买家端接口均仅标注 `[Authorize(Roles = "Buyer")]`，未校验入参 `orderId`/`paymentId`/`afterSalesId` 是否属于当前用户。`PaymentControllerBase.GetCurrentUserId()` 已提供但从未被调用。买家只需遍历 `orderId` 即可枚举他人支付单的金额、渠道交易号、支付时间等敏感信息。
- **影响**：水平越权读取敏感支付数据；攻击者可批量拉取支付流水用于欺诈或竞品分析。
- **修复建议**：在 `PaymentAppService.GetPaymentResultAsync` 等方法增加 `userId` 入参并过滤；或在控制器层调用 `GetCurrentUserId()` 后传入应用层做归属校验。
- **影响范围**：`PaymentsController`、`IPaymentAppService`、`IRefundAppService`。

### 5. WeChatPayAdapter 未将解密后的 out_trade_no 回填 ChannelNotifyResult，导致无法定位支付单
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L200-L248` 与 `src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L97`
- **类别**：A1 空引用 / A7 异步消息可靠性
- **根因**：`WeChatPayAdapter.VerifyNotifyAsync` 解密 `resource.ciphertext` 后从 `dataRoot` 读取了 `transaction_id`、`trade_state`、`success_time`、`amount`，但未读取 `out_trade_no`。`ChannelNotifyResult` 也没有 `OutTradeNo` 字段。`WeChatPayNotifyHandler.HandlePaymentNotifyAsync` 在 L97 依赖 `GetField(fields, "out_trade_no")`（且 `fields` 来自已损坏的 `ParseXml`），即使修复问题 1 改为从 `result` 取值，也无法获得 `out_trade_no` 来调用 `_paymentOrderRepository.GetByOutTradeNoAsync`。
- **影响**：微信回调即使验签通过、解密成功，仍无法定位本地支付单，最终返回 `FAIL` 触发重试。
- **修复建议**：在 `ChannelNotifyResult` 增加 `OutTradeNo` 属性；`WeChatPayAdapter.VerifyNotifyAsync` 解密后读取 `out_trade_no` 并回填；`WeChatPayNotifyHandler.HandlePaymentNotifyAsync` 改为 `result.OutTradeNo` 查找支付单。
- **影响范围**：`ChannelNotifyResult`、`WeChatPayAdapter`、`WeChatPayNotifyHandler`、`AlipayNotifyHandler`（同样依赖 `fields` 字典，但支付宝 `fields` 来自表单，当前可工作）。

### 6. PaymentRequestedEventConsumer 在渠道下单后保存前崩溃会导致重复下单
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs#L58-L79`
- **类别**：A7 异步消息可靠性 / A3 并发与竞态
- **根因**：消费者先创建 `PaymentOrder` 聚合并调用 `adapter.CreatePaymentAsync`（L67，已向渠道提交真实下单请求），再执行 `AddAsync` + `SaveEntitiesAsync`（L78-L79）。若 `SaveEntitiesAsync` 失败或进程崩溃，重试时 `GetByOrderIdAsync`（L43）仍返回 `null`（支付单未持久化），会再次调用 `adapter.CreatePaymentAsync`，在渠道侧产生重复 `out_trade_no` 下单。支付宝/微信均会因 `out_trade_no` 重复而拒绝，但部分场景（如 `page.pay`/`wap.pay` 仅构建 URL）不会触发渠道侧去重，造成多笔预支付。
- **影响**：资金侧风险——重复下单可能导致用户多次扫码支付；同时 `OutTradeNo` 生成规则（`PAY{yyyyMMddHHmmss}{6位随机}`，见 `PaymentOrder.cs#L101`）不依赖 PaymentId，重试间无法区分。
- **修复建议**：先 `AddAsync(paymentOrder)` + `SaveEntitiesAsync` 持久化 `Pending` 态，再调用渠道；若渠道下单失败再 `MarkFailed` 并更新。或使用幂等键（如 `integrationEvent.EventId` 作为 PaymentId）确保重试安全。
- **影响范围**：`PaymentRequestedEventConsumer`、`PaymentOrder`、`IPaymentChannelAdapter`。

## 🟡 中风险问题

### 7. ReconciliationService 调度时间计算错误，对账在 18:00 UTC+8 而非凌晨 2:00 执行
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs#L48-L49`
- **类别**：A5 边界条件 / C5 异步消息堆积
- **根因**：代码 `var nextRun = now.Date.AddHours(2).AddHours(8);` 等价于 `now.Date.AddHours(10)` = 今日 10:00 UTC = 今日 18:00 UTC+8。注释写“UTC+8 凌晨 2:00 = UTC 18:00”，意图是 UTC 18:00（次日 02:00 北京时间），但实际计算为 UTC 10:00（当日 18:00 北京时间）。对账在白天业务高峰运行，可能加剧数据库负载。
- **影响**：对账任务在 18:00 北京时间执行，与业务高峰重叠；且 `billDate = DateTime.UtcNow.Date.AddDays(-1)` 取的是执行时的 UTC 前一天，与 18:00 UTC+8 的预期账单日错位。
- **修复建议**：改为 `var nextRun = now.Date.AddHours(18);` 直接取 UTC 18:00（北京次日 02:00）。
- **影响范围**：`ReconciliationService.ExecuteAsync`。

### 8. 对账按 CreatedAt 而非 PaidAt 过滤，跨日支付会漏对账或误报差异
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs#L157-L160` 与 `src/Services/Payment/Leno.Payment.Infrastructure/Repositories/EfCorePaymentOrderRepository.cs#L118-L126`
- **类别**：A5 边界条件 / C4 大对象扫描
- **根因**：`LoadSystemOrdersPagedAsync` 调用 `paymentRepo.QueryAsync(null, channel, PaymentStatus.Paid, billDate, endDateExclusive, ...)`。仓储 `ApplyFilters` 用 `o.CreatedAt >= startDate` 与 `o.CreatedAt <= endDate`。渠道对账单按支付完成时间（`PaidAt`/`gmt_payment`/`success_time`）组织。昨日 23:59 创建、今日 00:01 支付的订单会被归入昨日对账，但渠道账单在今日，产生 `SystemOnly` 与 `ChannelOnly` 假差异。
- **影响**：每日对账产生大量假阳性差异，运维需人工 `MarkIgnored`，污染对账看板。
- **修复建议**：`IPaymentOrderRepository.QueryAsync` 增加按 `PaidAt` 过滤的能力，或在 `LoadSystemOrdersPagedAsync` 中改用 `PaidAt >= billDate && PaidAt < billDate.AddDays(1)`。
- **影响范围**：`ReconciliationService`、`IPaymentOrderRepository`、`EfCorePaymentOrderRepository`。

### 9. PaymentChannelConfig.Description 暴露 public setter，绕过聚合不变式
- **位置**：`src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentChannelConfig.cs#L28` 与 `src/Services/Payment/Leno.Payment.Application/Services/PaymentChannelConfigAppService.cs#L55-L58`
- **类别**：B2 聚合设计违规
- **根因**：`PaymentChannelConfig.Description` 是 `{ get; set; }`（public setter）。`Create` 工厂方法对 `description` 做了 `> MaxDescriptionLength(500)` 校验（L85-L89），但 `UpdateAsync` 直接 `config.Description = dto.Description`，未走任何聚合方法，绕过长度校验与领域事件发布。
- **影响**：调用方可写入超长描述（数据库列 `description` `HasMaxLength(500)` 会截断或抛 DbUpdateException），且 `PaymentChannelConfigChangedDomainEvent` 不会为 Description 变更发布。
- **修复建议**：将 `Description` 改为 `private set`，新增 `UpdateDescription(string? description)` 领域方法内做长度校验并发布事件。
- **影响范围**：`PaymentChannelConfig`、`PaymentChannelConfigAppService`。

### 10. RefundRequestedEventConsumer 未校验原支付单状态，可对未支付订单发起退款
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Consumers/RefundRequestedEventConsumer.cs#L60-L64`
- **类别**：A4 状态机非法迁移 / B2 聚合设计违规
- **根因**：消费者只检查 `originalPayment is null`（L61），未检查 `originalPayment.Status == PaymentStatus.Paid`。若原支付单处于 `Pending`/`ChannelOrdered`/`Failed`/`Closed`，仍会创建 `RefundOrder` 并调用 `adapter.CreateRefundAsync`。渠道侧（支付宝/微信）会返回业务错误，但退款单已持久化为 `Refunding` 态，需 `RefundStatusCheckJob` 兜底才能转为 `Failed`。
- **影响**：产生无效退款单，污染退款看板；售后域收到 `RefundFailedEvent` 后可能误判为可重试，进入死循环。
- **修复建议**：在 L64 后增加 `if (originalPayment.Status != PaymentStatus.Paid) throw new InvalidOperationException($"原支付单未支付成功，不可退款 Status={originalPayment.Status}");`，由 MassTransit 重试策略与死信队列处理。
- **影响范围**：`RefundRequestedEventConsumer`、`RefundOrder`、售后域消费者。

### 11. PaymentStatusCheckJob 未关闭超时支付单，Expired 订单永久停留在 Pending/ChannelOrdered
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs#L41-L63` 与 `src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs#L49`（`ExpireAt` 字段）、`src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs#L108`（设置 2 小时过期）
- **类别**：A4 状态机非法迁移 / C5 异步消息堆积
- **根因**：`PaymentOrder.Create` 设置 `ExpireAt = DateTime.UtcNow.AddHours(2)`，但全代码库无任何地方读取 `ExpireAt` 并调用 `MarkClosed`。`PaymentStatusCheckJob` 只查询 `Pending`/`ChannelOrdered` 态并主动查渠道，对超时订单仅做“查不到支付就跳过”，不关闭。订单域因此也收不到 `PaymentClosedEvent`，预占库存无法释放。
- **影响**：超时订单长期占用预占库存；用户重新下单会产生多笔 `Pending` 支付单（虽有 `GetByOrderIdAsync` 幂等，但仅限同订单）。
- **修复建议**：在 `PaymentStatusCheckJob.CheckAsync` 中增加 `if (order.ExpireAt < DateTime.UtcNow) { order.MarkClosed("支付超时自动关闭"); await _paymentOrderRepository.UpdateAsync(order, ct); await _unitOfWork.SaveEntitiesAsync(ct); return; }`。
- **影响范围**：`PaymentStatusCheckJob`、`PaymentOrder`、订单域（依赖 `PaymentClosedEvent` 释放库存）。

### 12. PaymentGrpcService 返回硬编码零值，gRPC 契约与 HTTP 路径语义不一致
- **位置**：`src/Services/Payment/Leno.Payment.Api/GrpcServices/PaymentGrpcService.cs#L48-L63`
- **类别**：B7 事件契约一致性
- **根因**：`MapToProto` 将 `AmountCents = 0L`、`PaidAt = string.Empty`、`TransactionId` 不设置（默认空）。注释自认“DTO 未提供 amount/paid_at/transaction_id/refunded_amount，留默认值”。`PaymentInfoResultDto`（`src/Services/Payment/Leno.Payment.Application/Services/PaymentInternalQueryService.cs#L27-L33`）只含 `PaymentId/Channel/OrderId/Status`，gRPC 消费方（如售后域）拿到的是空壳数据。
- **影响**：gRPC 双轨方案（`AntiCorruption:UseGrpc=true` 时映射，见 `Program.cs#L46-L49`）实际不可用，消费方按零值做业务判断会出错。
- **修复建议**：扩展 `PaymentInfoResultDto` 与 `IPaymentInternalQueryService` 返回 `Amount`、`PaidAt`、`ChannelTradeNo`、`RefundedAmount`，并在 `MapToProto` 中映射。
- **影响范围**：`PaymentGrpcService`、`IPaymentInternalQueryService`、`PaymentInfoResultDto`、所有 gRPC 消费方。

### 13. 聚合缺少乐观并发控制，回调与补偿任务并发更新会覆盖
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Configurations/PaymentOrderConfiguration.cs#L12-L42`（无 `RowVersion`/`IsRowVersion` 配置）；`src/Services/Payment/Leno.Payment.Infrastructure/Configurations/RefundOrderConfiguration.cs#L12-L41`（同）
- **类别**：A3 并发与竞态
- **根因**：`PaymentOrder` 与 `RefundOrder` 的 EF Core 配置未定义 `[Timestamp]` 或 `IsRowVersion()`。`PaymentStatusCheckJob` 与 `AlipayNotifyHandler`/`WeChatPayNotifyHandler` 可能在同一时刻对同一支付单调用 `MarkSucceeded` 并 `UpdateAsync`。两路 `UpdateAsync` 都不会触发 EF Core 的乐观并发异常，后写覆盖先写，可能导致 `PaymentSucceededDomainEvent` 被收集两次（一次在内存中、一次因覆盖丢失），最终通过 Outbox 重复发布或丢失。
- **影响**：重复发布 `PaymentSucceededEvent` 会让订单域重复扣减库存、促销域重复核销优惠券；或后写覆盖先写的 `ChannelTradeNo`，导致对账时交易号不匹配。
- **修复建议**：在 `AggregateRoot` 基类或 `PaymentOrderConfiguration` 增加 `byte[] RowVersion` 属性并 `builder.Property(o => o.RowVersion).IsRowVersion()`；捕获 `DbUpdateConcurrencyException` 后重读聚合判断状态。
- **影响范围**：`PaymentOrder`、`RefundOrder`、`PaymentStatusCheckJob`、`RefundStatusCheckJob`、所有 NotifyHandler。

### 14. ReconciliationDiffConfiguration 与其他聚合配置风格不一致（表名 PascalCase、枚举存字符串）
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Configurations/ReconciliationDiffConfiguration.cs#L14`（`ToTable("ReconciliationDiffs")` PascalCase）、`#L19-L20`、`#L26`（`HasConversion<string>()`）
- **类别**：B4 共享内核污染 / 一致性
- **根因**：`PaymentOrderConfiguration`（`ToTable("payment_orders")` snake_case）、`RefundOrderConfiguration`（`refund_orders`）、`PaymentChannelConfigConfiguration`（`payment_channel_configs`）均使用 snake_case 表名与 `HasConversion<int>()` 枚举存储。`ReconciliationDiffConfiguration` 用 PascalCase 表名 `ReconciliationDiffs` 且枚举存字符串，未设置 `HasColumnName`。同一 DbContext 内两种命名风格混用。
- **影响**：数据库表命名不统一；DBA 在做跨表查询、备份策略时需额外处理；EF Core 迁移生成的列名也不一致。
- **修复建议**：统一改为 `ToTable("reconciliation_diffs")`，枚举改 `HasConversion<int>()`，并为所有列设置 `HasColumnName`。
- **影响范围**：`ReconciliationDiffConfiguration`、`PaymentDbContext`、所有迁移文件。

### 15. AlipayNotifyHandler 退款通知用 trade_no 充当 channelRefundNo，语义错误
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Notify/AlipayNotifyHandler.cs#L163`
- **类别**：A1 空引用 / 数据正确性
- **根因**：`var channelRefundNo = GetField(fields, "trade_no");`。支付宝退款异步通知中 `trade_no` 是原交易号，并非退款单号；支付宝退款无独立“退款交易号”概念。当前代码把原交易号写入 `RefundOrder.ChannelRefundNo`，后续 `RefundStatusCheckJob` 调用 `adapter.QueryRefundAsync(refund.OutTradeNo, refund.OutRefundNo)` 时不使用 `ChannelRefundNo`，暂无功能性 bug，但 `RefundOrderDto.ChannelRefundNo` 对前端展示错误。
- **影响**：运营端退款列表展示的“第三方退款单号”实际是原支付交易号，造成客服对账困难。
- **修复建议**：支付宝退款通知无独立退款号，应将 `ChannelRefundNo` 留空或设为 `refund.OutRefundNo`（商户退款单号），并在 DTO 注释说明。
- **影响范围**：`AlipayNotifyHandler`、`RefundOrder`、`RefundOrderDto`。

## 🟢 低风险问题

### 16. OutTradeNo/OutRefundNo 生成存在秒级碰撞风险
- **位置**：`src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs#L101` 与 `src/Services/Payment/Leno.Payment.Domain/Aggregates/RefundOrder.cs#L119`
- **类别**：A3 并发与竞态
- **根因**：`$"PAY{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100000, 999999)}"`。同一秒内 90 万种可能，高并发下碰撞概率非可忽略。碰撞时数据库唯一索引（`ix_payment_orders_out_trade_no`）会抛 `DbUpdateException`，消费者无重试逻辑。
- **影响**：偶发下单失败，用户需重试。
- **修复建议**：改用 `Guid.NewGuid().ToString("N")[..8]` 或 Snowflake ID 作为后缀，或直接用 `paymentId` 的短编码。

### 17. NotifyController 中 StreamReader 未 using，依赖框架兜底
- **位置**：`src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L51` 与 `#L84`
- **类别**：A6 资源泄漏
- **根因**：`var rawBody = await new StreamReader(Request.Body).ReadToEndAsync(ct);`。`StreamReader` 持有 `Request.Body`，未 `using` 不会立即释放。ASP.NET Core 会在请求结束时释放请求流，但高并发下可能延迟 GC。
- **影响**：轻微的句柄延迟释放，无功能性影响。
- **修复建议**：使用 `using var reader = new StreamReader(Request.Body, leaveOpen: true);`。

### 18. InternalPaymentsController 双路由 + Obsolete 标注未设下线时间表
- **位置**：`src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs#L24-L25`
- **类别**：代码质量
- **根因**：`[Obsolete("双路由期保留，1 周后下线...")]` 标注在方法上，但 ASP.NET Core 路由不受 `Obsolete` 特性影响，两个路由同时生效。“1 周后下线”无自动化跟踪机制。
- **影响**：旧路由长期保留，增加攻击面。
- **修复建议**：在旧路由上增加 `[ApiExplorerSettings(IgnoreApi = true)]` 并设置日历提醒删除。

### 19. WeChatPayAdapter.CreatePaymentAsync 硬编码 NATIVE，忽略 PaymentScene
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs#L43`
- **类别**：功能限制
- **根因**：`const string tradeType = "NATIVE";`。`AlipayAdapter.CreatePaymentAsync` 支持 `QrCode/Page/Wap/App` 四种场景，微信适配器只支持 Native。`IPaymentChannelAdapter.CreatePaymentAsync(PaymentOrder, CancellationToken)` 接口未暴露场景参数，消费方（`PaymentRequestedEventConsumer`）无法指定。
- **影响**：微信支付仅能扫码，不支持 JSAPI/H5/App。
- **修复建议**：在 `IPaymentChannelAdapter` 接口增加 `PaymentScene` 可选参数，或提供单独的 `CreatePaymentAsync(PaymentOrder, PaymentScene, string?, CancellationToken)` 重载（`AlipayAdapter` 已有此重载但未在接口声明）。

### 20. PaymentStatusCheckJob 每轮仅处理 100 笔，积压场景下补偿滞后
- **位置**：`src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs#L17-L18`、`#L45-L49`
- **类别**：C5 异步消息堆积
- **根因**：`BatchSize = 100`，每轮查询 `Pending` 与 `ChannelOrdered` 各 100 笔。若渠道大面积故障恢复后有数千笔待补偿，需数十轮才能扫完，且每笔都要调用渠道查询接口（HTTP 往返），可能超过单轮调度间隔。
- **影响**：支付成功补偿延迟，用户看到“未支付”状态时间拉长。
- **修复建议**：改为循环分页直到 `batch.Count < BatchSize`，或引入 `Skip` 指针记录最后处理时间，避免重复扫前 100 笔。

## BC 健康度评分
| 维度 | 评分(0-5) | 说明 |
|------|-----------|------|
| 功能正确性 | 2 | 微信回调链路（ParseXml + 验签 + out_trade_no 缺失）三重缺陷导致微信支付回调完全不可用；对账调度时间与过滤字段错误；超时订单不关闭。 |
| DDD 合规 | 3 | 聚合根封装状态机基本到位，但 `PaymentChannelConfig.Description` public setter 破坏不变式；`ChannelConfig` 混用私钥/公钥；gRPC 契约返回硬编码零值。 |
| 性能与可靠性 | 2 | 缺少乐观并发控制；补偿任务分页固定 100；Redis 幂等降级策略虽有 T19 注释但 WeChatPayChannel 验签根本性失败；Outbox 依赖基类但聚合无 RowVersion，存在重复发布风险。 |
