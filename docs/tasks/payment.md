# 支付集成域 (Payment Integration) 开发任务

> **限界上下文**: BC8 支付集成域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis / HTTP Client  
> **依赖**: `shared-kernel`  
> **对应文档**: `08-支付集成域.md`

---

## 模块概述

支付集成域作为平台与外部支付渠道（微信支付、支付宝）的防腐层，封装渠道差异。接收订单域 `PaymentRequestedIntegrationEvent` 创建支付订单，调用外部渠道下单，接收异步回调通知，验签后发布 `PaymentSucceededEvent`/`PaymentFailedEvent`。退款流程类似：接收 `RefundRequestedIntegrationEvent`→调用渠道退款→回调验签→发布 `RefundSucceededEvent`/`RefundFailedEvent`。配置驱动切换渠道参数。

---

## Task 1: 项目初始化与领域层 — PaymentOrder 聚合

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Domain/Leno.Payment.Domain.csproj`
- Create: `src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Aggregates/RefundOrder.cs`

- [ ] 创建 Leno.Payment.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `PaymentOrder` 聚合根（PaymentId、OutTradeNo、OrderId、UserId、Amount、Channel、ChannelTradeNo、Status、PrepayId、CodeUrl、H5Url、ExpireAt、PaidAt、FailReason、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `PaymentOrder.Create` 工厂方法（生成 OutTradeNo，置待支付态，设 ExpireAt=创建+2h）
- [ ] 实现 `MarkChannelOrdered(channelTradeNo, prepayId/codeUrl/h5Url)`（渠道下单成功，记录支付凭证）
- [ ] 实现 `MarkSucceeded(channelTradeNo, paidAt)`（支付成功，附加 `PaymentSucceededEvent`）
- [ ] 实现 `MarkFailed(reason)`（支付失败，附加 `PaymentFailedEvent`）
- [ ] 实现 `MarkClosed(reason)`（超时关单，对应订单域取消）
- [ ] 实现 `RefundOrder` 聚合根（RefundId、OutRefundNo、PaymentId、OrderId、AfterSalesId、RefundAmount、Channel、ChannelRefundNo、Status、RefundedAt、FailReason、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `RefundOrder.Create` 工厂方法（生成 OutRefundNo，置退款中态）
- [ ] 实现 `RefundOrder.MarkSucceeded(channelRefundNo, refundedAt)`（退款成功，附加 `RefundSucceededEvent`）
- [ ] 实现 `RefundOrder.MarkFailed(reason)`（退款失败，附加 `RefundFailedEvent`）
- [ ] 定义 `PaymentChannel`（WeChatPay/Alipay）、`PaymentStatus`（Pending/ChannelOrdered/Paid/Failed/Closed）、`RefundStatus`（Refunding/Succeeded/Failed）
- [ ] 编写单元测试覆盖支付与退款状态机
- [ ] 提交：`feat(payment): add PaymentOrder and RefundOrder aggregates`

---

## Task 2: 领域层 — 渠道适配接口与配置

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Domain/Services/IPaymentChannelAdapter.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Services/IChannelConfigProvider.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Repositories/IPaymentOrderRepository.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Repositories/IRefundOrderRepository.cs`

- [ ] 定义 `IPaymentChannelAdapter` 接口（CreatePaymentAsync、QueryPaymentAsync、CreateRefundAsync、QueryRefundAsync、VerifyNotifyAsync）
- [ ] 定义 `IChannelConfigProvider` 接口（GetConfigAsync(channel) 返回渠道参数：AppId、MchId、ApiKey、CertPath、NotifyUrl 等）
- [ ] 定义 `IPaymentOrderRepository`（GetByOutTradeNoAsync、GetByOrderIdAsync、AddAsync、UpdateAsync）
- [ ] 定义 `IRefundOrderRepository`（GetByOutRefundNoAsync、GetByAfterSalesIdAsync、AddAsync、UpdateAsync）
- [ ] 提交：`feat(payment): add channel adapter interface and repository interfaces`

---

## Task 3: 领域事件定义

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Domain/Events/PaymentSucceededEvent.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Events/PaymentFailedEvent.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Events/PaymentClosedEvent.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Events/RefundSucceededEvent.cs`
- Create: `src/Services/Payment/Leno.Payment.Domain/Events/RefundFailedEvent.cs`

- [ ] 定义 `PaymentSucceededEvent`（paymentId、orderId、channel、channelTradeNo、amount、paidAt）— 消费方：订单域、积分域、促销域
- [ ] 定义 `PaymentFailedEvent`（paymentId、orderId、reason）
- [ ] 定义 `PaymentClosedEvent`（paymentId、orderId、reason）
- [ ] 定义 `RefundSucceededEvent`（refundId、afterSalesId、orderId、refundedAmount）— 消费方：售后域
- [ ] 定义 `RefundFailedEvent`（refundId、afterSalesId、orderId、reason）— 消费方：售后域
- [ ] 提交：`feat(payment): add domain integration events`

---

## Task 4: 基础设施层 — EF Core 仓储与渠道配置

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/PaymentDbContext.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Repositories/EfCorePaymentOrderRepository.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Repositories/EfCoreRefundOrderRepository.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Config/ChannelConfigProvider.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Config/PaymentChannelOptions.cs`

- [ ] 实现 `PaymentDbContext`（DbSet<PaymentOrder>、DbSet<RefundOrder>）
- [ ] 实现各 EF Core 仓储
- [ ] 实现 `ChannelConfigProvider`（从配置中心读取渠道参数，实现 `IExternalChannelOptions` 契约）
- [ ] 实现 `PaymentChannelOptions`（各渠道配置 DTO：WeChatPay/Alipay 的 AppId、MchId、ApiKey、CertPath、NotifyUrl）
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试
- [ ] 提交：`feat(payment): add EF Core repositories and channel config provider`

---

## Task 5: 基础设施层 — 微信支付适配器

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayAdapter.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPay/WeChatPayClient.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPay/WeChatPaySignatureHelper.cs`

- [ ] 实现 `WeChatPayAdapter`（实现 `IPaymentChannelAdapter`）
- [ ] 实现 `CreatePaymentAsync`（调用微信支付统一下单 API，支持 Native/JSAPI/H5 三种支付方式）
- [ ] 实现 `QueryPaymentAsync`（主动查询支付状态，用于补偿）
- [ ] 实现 `CreateRefundAsync`（调用微信支付退款 API）
- [ ] 实现 `VerifyNotifyAsync`（验签微信回调通知）
- [ ] 实现 `WeChatPaySignatureHelper`（签名生成与校验，HMAC-SHA256）
- [ ] 配置 HttpClient（Polly 重试策略、超时控制）
- [ ] 编写单元测试 Mock 微信 API 响应
- [ ] 提交：`feat(payment): add WeChat Pay channel adapter`

---

## Task 6: 基础设施层 — 支付宝适配器

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayAdapter.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/Alipay/AlipayClient.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/Alipay/AlipaySignatureHelper.cs`

- [ ] 实现 `AlipayAdapter`（实现 `IPaymentChannelAdapter`）
- [ ] 实现 `CreatePaymentAsync`（调用支付宝 alipay.trade.precreate/pc.pay 接口）
- [ ] 实现 `QueryPaymentAsync`（调用 alipay.trade.query）
- [ ] 实现 `CreateRefundAsync`（调用 alipay.trade.refund）
- [ ] 实现 `VerifyNotifyAsync`（验签支付宝异步通知，RSA2）
- [ ] 实现 `AlipaySignatureHelper`（RSA-SHA256 签名生成与校验）
- [ ] 编写单元测试 Mock 支付宝 API 响应
- [ ] 提交：`feat(payment): add Alipay channel adapter`

---

## Task 7: 基础设施层 — 渠道适配工厂与事件消费者

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/PaymentChannelFactory.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Consumers/RefundRequestedEventConsumer.cs`

- [ ] 实现 `PaymentChannelFactory`（根据 channel 参数返回对应适配器实例）
- [ ] 实现 `PaymentRequestedEventConsumer`（接收订单支付请求→创建 PaymentOrder→调用渠道下单→记录支付凭证→返回前端）
- [ ] 实现 `RefundRequestedEventConsumer`（接收售后退款请求→创建 RefundOrder→调用渠道退款→等待回调）
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试
- [ ] 提交：`feat(payment): add channel factory and event consumers`

---

## Task 8: 基础设施层 — 回调通知处理与补偿任务

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Notify/AlipayNotifyHandler.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Jobs/PaymentStatusCheckJob.cs`
- Create: `src/Services/Payment/Leno.Payment.Infrastructure/Jobs/RefundStatusCheckJob.cs`

- [ ] 实现 `WeChatPayNotifyHandler`（接收微信回调→验签→更新 PaymentOrder→发布事件→返回 SUCCESS）
- [ ] 实现 `AlipayNotifyHandler`（接收支付宝回调→验签→更新 PaymentOrder→发布事件→返回 success）
- [ ] 实现 `PaymentStatusCheckJob`（定时轮询待支付订单→主动查询渠道状态→补偿更新）
- [ ] 实现 `RefundStatusCheckJob`（定时轮询退款中订单→主动查询渠道退款状态→补偿更新）
- [ ] 防重放攻击（验签 + OutTradeNo 幂等）
- [ ] 编写集成测试验证回调处理与补偿
- [ ] 提交：`feat(payment): add notify handlers and compensation jobs`

---

## Task 9: 应用层 — 支付与退款查询用例

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Application/IPaymentAppService.cs`
- Create: `src/Services/Payment/Leno.Payment.Application/Services/PaymentAppService.cs`
- Create: `src/Services/Payment/Leno.Payment.Application/Services/RefundAppService.cs`

- [ ] 实现 `GetPaymentResultAsync(orderId)`（查询支付结果，含渠道信息）
- [ ] 实现 `QueryPaymentStatusAsync(paymentId)`（主动查询渠道状态）
- [ ] 实现 `GetRefundResultAsync(afterSalesId)`（查询退款结果）
- [ ] 实现运营查询用例（全平台支付/退款记录分页查询）
- [ ] 编写单元测试
- [ ] 提交：`feat(payment): add payment and refund query services`

---

## Task 10: 表现层 — API 控制器与回调端点

**文件:**
- Create: `src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs`
- Create: `src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs`

- [ ] 实现 `PaymentsController`（GET /api/payments/{orderId}、GET /api/admin/payments）
- [ ] 实现 `NotifyController`（POST /api/notify/wechat-pay、POST /api/notify/alipay）— 无鉴权端点，仅验签
- [ ] 回调端点返回渠道要求的响应格式（微信 SUCCESS XML、支付宝 success 字符串）
- [ ] 配置 HTTPS 与 IP 白名单（生产环境）
- [ ] 编写 API 集成测试覆盖支付请求→回调→事件发布全流程
- [ ] 提交：`feat(payment): add API controllers and notify endpoints`
