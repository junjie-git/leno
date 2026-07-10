# Tasks

## 阶段一：基础设施

- [x] Task 1: 创建内部服务间鉴权中间件与配置
  - [x] SubTask 1.1: 在 `Leno.Infrastructure` 中创建 `InternalApiKeyMiddleware`，校验请求头 `X-Internal-Key` 是否匹配配置值（`InternalAuth:ApiKey`），不匹配返回 401
  - [x] SubTask 1.2: 在 `Leno.Infrastructure` 中创建 `InternalApiKeyOptions` 配置类与 `AddInternalApiKeyAuth` 扩展方法
  - [x] SubTask 1.3: 在各域 API 的 `Program.cs` 中注册内部鉴权中间件（按需应用到 `internal/` 路由前缀）

- [x] Task 2: 创建 Redis 幂等去重消费者基类
  - [x] SubTask 2.1: 在 `Leno.Infrastructure/EventBus/` 创建 `RedisIntegrationEventConsumerBase<T>`，继承 `IntegrationEventConsumerBase<T>`，注入 `IConnectionMultiplexer`
  - [x] SubTask 2.2: `IsProcessedAsync` 使用 `StringGetAsync` 查询 key `evt:processed:{eventId}` 是否存在
  - [x] SubTask 2.3: `MarkAsProcessedAsync` 使用 `StringSetAsync` 写入 key，TTL 24 小时
  - [x] SubTask 2.4: 将所有现有消费者基类从 `IntegrationEventConsumerBase<T>` 改为 `RedisIntegrationEventConsumerBase<T>`（各域 Infrastructure 层的 Consumer 类）

## 阶段二：目标域内部查询 API（可并行）

- [x] Task 3: Product 域新增 SKU 查询内部端点
  - [x] SubTask 3.1: 在 `Leno.Product.Application` 新增 `IProductInternalQueryService` 接口与 `SkuInfoResultDto`（SkuId、Price、Currency、Available、Title、MainImageUrl、SellerId）
  - [x] SubTask 3.2: 在 `Leno.Product.Application` 实现 `ProductInternalQueryService`，通过 SKU 仓储查询并组装 DTO
  - [x] SubTask 3.3: 在 `Leno.Product.Api` 新增 `InternalProductsController`，路由 `internal/products/skus/{skuId}`（GET）和 `internal/products/skus/batch`（POST），应用内部鉴权

- [x] Task 4: Promotion 域新增折扣试算内部端点
  - [x] SubTask 4.1: 在 `Leno.Promotion.Application` 新增 `IPromotionCalculateAppService` 接口与 `CalculateDiscountDto`（UserId、Items 列表含 SkuId/Subtotal）、`DiscountResultDto`（TotalDiscountAmount）
  - [x] SubTask 4.2: 实现 `PromotionCalculateAppService`，查询用户可用优惠券与生效满减活动，计算适用优惠总金额
  - [x] SubTask 4.3: 在 `Leno.Promotion.Api` 新增 `InternalPromotionsController`，路由 `internal/promotions/calculate`（POST），应用内部鉴权

- [x] Task 5: PointsMembership 域新增积分操作内部端点
  - [x] SubTask 5.1: 在 `Leno.PointsMembership.Domain` 的 `PointsAccount` 聚合新增 `TryOffset`（试扣返回可抵现金额）、`Freeze`（冻结积分）、`Release`（释放冻结积分）领域方法（如尚不存在）
  - [x] SubTask 5.2: 在 `Leno.PointsMembership.Application` 新增 `IPointsInternalAppService` 接口与对应 DTO（TrialOffsetDto/FreezeDto/ReleaseDto 及结果 DTO）
  - [x] SubTask 5.3: 实现 `PointsInternalAppService`，调用聚合方法并持久化
  - [x] SubTask 5.4: 在 `Leno.PointsMembership.Api` 新增 `InternalPointsController`，路由 `internal/points/trial-offset`、`internal/points/freeze`、`internal/points/release`（均 POST），应用内部鉴权

- [x] Task 6: UserAuth 域新增联系方式查询内部端点
  - [x] SubTask 6.1: 在 `Leno.UserAuth.Application` 新增 `IUserInternalQueryService` 接口与 `UserContactsDto`（UserId、PhoneNumber、Email，未脱敏）
  - [x] SubTask 6.2: 实现 `UserInternalQueryService`，通过用户仓储查询返回真实联系方式
  - [x] SubTask 6.3: 在 `Leno.UserAuth.Api` 新增 `InternalUsersController`，路由 `internal/users/{userId}/contacts`（GET），应用内部鉴权

- [x] Task 7: Order 域新增订单状态查询内部端点
  - [x] SubTask 7.1: 在 `Leno.Order.Application` 新增 `IOrderInternalQueryService` 接口与 `OrderStatusResultDto`（OrderId、Status、UserId、Items 列表含 SkuId/Quantity/AfterSalesStatus、CompletedAt、CreatedAt）
  - [x] SubTask 7.2: 实现 `OrderInternalQueryService`，通过订单仓储查询并组装 DTO
  - [x] SubTask 7.3: 在 `Leno.Order.Api` 新增 `InternalOrdersController`，路由 `internal/orders/{orderId}/status`（GET），应用内部鉴权

- [x] Task 8: Payment 域新增支付信息查询内部端点
  - [x] SubTask 8.1: 在 `Leno.Payment.Application` 新增 `IPaymentInternalQueryService` 接口与 `PaymentInfoResultDto`（PaymentId、Channel、OrderId、Status）
  - [x] SubTask 8.2: 实现 `PaymentInternalQueryService`，通过支付仓储按 orderId 查询
  - [x] SubTask 8.3: 在 `Leno.Payment.Api` 新增 `InternalPaymentsController`，路由 `internal/payments/{orderId}/info`（GET），应用内部鉴权

## 阶段三：跨域防腐层 HTTP 客户端实现（依赖阶段二）

- [x] Task 9: Cart 域 CartPriceService 替换为真实 HTTP 查询
  - [x] SubTask 9.1: 在 `Leno.Cart.Infrastructure` 修改 `CartPriceService`，注入 `HttpClient`、`IOptions<InternalApiKeyOptions>`
  - [x] SubTask 9.2: `GetSkuPricesAsync` 调用 Product 域 `POST internal/products/skus/batch`，携带 `X-Internal-Key` 头，解析响应映射为 `SkuPriceSnapshot` 列表
  - [x] SubTask 9.3: 在 Cart 域 `ServiceCollectionExtensions` 注册 `AddHttpClient<CartPriceService>` 并配置内部密钥

- [x] Task 10: Order 域三个防腐层替换为真实 HTTP 查询
  - [x] SubTask 10.1: 修改 `ProductAntiCorruptionService`，注入 HttpClient，调用 Product 域 `GET internal/products/skus/{skuId}`，解析返回 `SkuInfo`
  - [x] SubTask 10.2: 修改 `PromotionAntiCorruptionService`，注入 HttpClient，调用 Promotion 域 `POST internal/promotions/calculate`，解析返回优惠金额
  - [x] SubTask 10.3: 修改 `PointsAntiCorruptionService` 的 `TryOffsetAsync`/`FreezeAsync`/`ReleaseAsync`，分别调用 PointsMembership 域三个内部端点
  - [x] SubTask 10.4: 在 Order 域 `ServiceCollectionExtensions` 为三个防腐层服务注册 `AddHttpClient<TypedClient>` 并配置内部密钥

- [x] Task 11: ReviewAfterSales 域三个防腐层替换为真实实现
  - [x] SubTask 11.1: 修改 `PaymentInfoQueryService`，注入 HttpClient，调用 Payment 域 `GET internal/payments/{orderId}/info`
  - [x] SubTask 11.2: 修改 `AfterSalesEligibilityChecker.EnsureEligibleAsync`，调用 Order 域 `GET internal/orders/{orderId}/status`，校验：订单在售后期内、申请人为订单买家、同订单行无进行中同类型售后（通过本地仓储查重）
  - [x] SubTask 11.3: 修改 `ReviewEligibilityChecker.EnsureEligibleAsync`，调用 Order 域端点，校验：订单已完成、在评价期内、申请人为订单买家、订单行未重复评价（通过本地仓储查重）
  - [x] SubTask 11.4: 在 ReviewAfterSales 域 `ServiceCollectionExtensions` 为三个服务注册 `AddHttpClient` 并配置内部密钥

- [x] Task 12: Notification 域新增用户联系方式防腐层并修复渠道
  - [x] SubTask 12.1: 在 `Leno.Notification.Infrastructure` 新增 `UserContactAntiCorruptionService`，注入 HttpClient，调用 UserAuth 域 `GET internal/users/{userId}/contacts`，返回手机号与邮箱
  - [x] SubTask 12.2: 修改 `SmsChannel.SendAsync`，通过防腐层查询 `record.UserId` 对应手机号，替换空字符串占位
  - [x] SubTask 12.3: 修改 `EmailChannel.SendAsync`，通过防腐层查询 `record.UserId` 对应邮箱，替换空字符串占位
  - [x] SubTask 12.4: 在 Notification 域 `ServiceCollectionExtensions` 注册 `AddHttpClient<UserContactAntiCorruptionService>` 并配置内部密钥

## 阶段四：第三方渠道真实实现（可并行）

- [x] Task 13: Payment 域 AlipayClient 替换为真实 HTTP 调用
  - [x] SubTask 13.1: 修改 `PreCreateAsync`，将 `SimulatePreCreateResponse()` 替换为 `await PostFormAsync(BuildUrl(), formData, ct)`，解析真实响应
  - [x] SubTask 13.2: 修改 `QueryAsync`，替换为真实 `alipay.trade.query` HTTP 调用
  - [x] SubTask 13.3: 修改 `RefundAsync`，替换为真实 `alipay.trade.refund` HTTP 调用
  - [x] SubTask 13.4: 修改 `QueryRefundAsync`，替换为真实 `alipay.trade.fastpay.refund.query` HTTP 调用
  - [x] SubTask 13.5: 移除 `SimulatePreCreateResponse`/`SimulateQueryResponse`/`SimulateRefundResponse`/`SimulateQueryRefundResponse` 等模拟方法
  - [x] SubTask 13.6: 添加 HTTP 失败响应的异常处理与日志

- [x] Task 14: Payment 域 WeChatPayClient 替换为真实 HTTP 调用
  - [x] SubTask 14.1: 修改 `UnifiedOrderAsync`，将 `SimulateUnifiedOrderResponse()` 替换为 `await PostXmlAsync(BuildUrl(UnifiedOrderPath), requestXml, ct)`
  - [x] SubTask 14.2: 修改 `QueryOrderAsync`，替换为真实 `/pay/orderquery` HTTP 调用
  - [x] SubTask 14.3: 修改 `RefundAsync`，替换为真实 `/secapi/pay/refund` HTTP 调用（含商户证书）
  - [x] SubTask 14.4: 修改 `QueryRefundAsync`，替换为真实 `/pay/refundquery` HTTP 调用
  - [x] SubTask 14.5: 移除 `SimulateUnifiedOrderResponse`/`SimulateQueryOrderResponse`/`SimulateRefundResponse`/`SimulateQueryRefundResponse` 等模拟方法
  - [x] SubTask 14.6: 添加 HTTP 失败响应的异常处理与日志

- [x] Task 15: Notification 域 SmtpClientWrapper 替换为真实 SMTP 发送
  - [x] SubTask 15.1: 在 Notification.Infrastructure.csproj 添加 `MailKit` NuGet 包引用
  - [x] SubTask 15.2: 修改 `SmtpClientWrapper.SendAsync`，使用 `MailKit.Net.Smtp.SmtpClient` 连接 SMTP 服务器、认证、发送 `MimeMessage`，返回真实结果
  - [x] SubTask 15.3: 添加连接失败、认证失败、发送失败的异常处理，返回 `(false, reason)` 元组

- [x] Task 16: Notification 域 SmsClient 替换为真实短信 API 调用
  - [x] SubTask 16.1: 修改 `SmsClient.SendAsync`，使用 HttpClient 调用配置的短信服务商 API（阿里云/腾讯云），构造签名请求
  - [x] SubTask 16.2: 解析服务商响应，映射为 `(bool Succeeded, string? FailReason)` 返回
  - [x] SubTask 16.3: 在 `SmsOptions` 中补充服务商类型、API 地址、签名等配置项

- [x] Task 17: Order 域 LogisticsTrackingService 替换为真实物流查询
  - [x] SubTask 17.1: 修改 `LogisticsTrackingService`，注入 HttpClient，调用配置的物流轨迹查询 API
  - [x] SubTask 17.2: 解析响应映射为 `LogisticsTrackingNode` 列表
  - [x] SubTask 17.3: 在 `appsettings.json` 中补充物流 API 配置（ApiUrl、AppKey、需要时按物流公司编码查询）
  - [x] SubTask 17.4: API 不可用或查询失败时返回空节点列表（而非占位"物流信息暂未更新"）

## 阶段五：上下文与事件契约修复

- [x] Task 18: 修复 PaymentFailedEvent 和 RefundCompletedEvent 事件契约
  - [x] SubTask 18.1: 在 `Leno.SharedContracts/Events/PaymentEvents.cs` 的 `PaymentFailedEvent` 新增 `UserId` 属性
  - [x] SubTask 18.2: 在 `Leno.SharedContracts/Events/PaymentEvents.cs` 的 `RefundCompletedEvent` 新增 `UserId` 属性
  - [x] SubTask 18.3: 在 Payment 域发布 `PaymentFailedEvent` 处填充 `UserId`（从支付单的 UserId 获取）
  - [x] SubTask 18.4: 在 Payment 域发布 `RefundCompletedEvent` 处填充 `UserId`（从关联订单/售后单获取）

- [x] Task 19: 修复 Notification 域两个事件消费者
  - [x] SubTask 19.1: 修改 `PaymentEventConsumer.Consume(PaymentFailedEvent)`，使用事件中的 `UserId` 调用 `_dispatcher.DispatchAsync` 发送支付失败通知（替换跳过逻辑）
  - [x] SubTask 19.2: 修改 `AfterSalesEventConsumer.Consume(RefundCompletedEvent)`，使用事件中的 `UserId` 发送退款完成通知（替换跳过逻辑）

- [x] Task 20: 修复 Order 域 ShipAsync 操作人标识与价格校验
  - [x] SubTask 20.1: 修改 `IOrderAppService.ShipAsync` 与 `OrderAppService.ShipAsync` 签名，新增 `Guid operatorId` 参数（Application 层不引用 Infrastructure，操作人标识由控制器传入）
  - [x] SubTask 20.2: 修改 `OrdersController` 的 Ship 端点，传入 `GetCurrentUserId()` 作为 `operatorId`（替换 `Guid.Empty`）
  - [x] SubTask 20.3: 修改 `OrderPricingDomainService.ValidatePricesAsync`，通过注入的 `IProductAntiCorruptionService` 查询每个 SKU 实际售价，校验与 `ExpectedPrice` 一致，不一致抛出领域异常

- [x] Task 21: 修复 SystemAdmin 域定时任务手动触发
  - [x] SubTask 21.1: 修改 `ScheduledTaskExecutor`，注入 `ISchedulerFactory`（替换或补充现有依赖）
  - [x] SubTask 21.2: 修改 `RunNowAsync`，通过 `ISchedulerFactory.GetScheduler` 获取调度器，构造 `JobKey` 并调用 `scheduler.TriggerJob(jobKey, ct)` 立即触发作业
  - [x] SubTask 21.3: 移除占位日志，添加触发失败异常处理

- [x] Task 23: 清理验证阶段发现的残留占位实现（Task 22 扫描时发现）
  - [x] SubTask 23.1: `AlipaySignatureHelper` 由 SHA256 占位改为真实 RSA-SHA256（RSA2）签名/验签，使用 `RSA.ImportFromPem`
  - [x] SubTask 23.2: 退款聚合 `RefundOrder` 新增 `OutTradeNo` 属性，`RefundRequestedEventConsumer` 加载原支付单并传入真实 OutTradeNo，替换 `PaymentId.ToString()` 占位
  - [x] SubTask 23.3: `IPaymentChannelAdapter.QueryRefundAsync` 与 `IChannelStatusQueryService.QueryRefundStatusAsync` 签名新增 `outTradeNo`，支付宝退款查询用真实原支付单号，微信查询按 out_refund_no（忽略 outTradeNo）
  - [x] SubTask 23.4: 删除 `SmsChannel`/`EmailChannel` 过时的"模拟实现"类注释（实现已是真实 HTTP/MailKit 调用）

## 阶段六：构建验证与提交

- [x] Task 22: 全量构建验证与逐任务提交
  - [x] SubTask 22.1: 运行 `dotnet build Leno.slnx` 确认 0 Error / 0 Warning（含 --no-incremental 全量构建）
  - [x] SubTask 22.2: 修复编译警告（CA1859 具体类型、CA1305 CultureInfo、CA1716 关键字参数等）— 全程 0 Warning
  - [x] SubTask 22.3: 按任务顺序逐个提交（`feat(<scope>): <description>` 规范）并推送到远程 dev 分支

# Task Dependencies

- Task 1 (内部鉴权中间件) → Task 3-8 (各域内部端点依赖鉴权)
- Task 2 (Redis 幂等基类) → 无依赖，可与 Task 1 并行
- Task 3-8 (目标域内部端点) → 可彼此并行，均依赖 Task 1
- Task 9-12 (防腐层 HTTP 客户端) → 依赖对应的 Task 3-8（目标域端点就绪）
  - Task 9 (Cart) 依赖 Task 3 (Product)
  - Task 10 (Order 防腐层) 依赖 Task 3 (Product)、Task 4 (Promotion)、Task 5 (Points)
  - Task 11 (ReviewAfterSales) 依赖 Task 7 (Order)、Task 8 (Payment)
  - Task 12 (Notification 联系方式) 依赖 Task 6 (UserAuth)
- Task 13-17 (第三方渠道) → 可彼此并行，无跨域依赖
- Task 18 (事件契约) → Task 19 (Notification 消费者) 依赖 Task 18
- Task 20 (Order 上下文) 依赖 Task 10 (商品防腐层就绪后才能校验价格)
- Task 21 (SystemAdmin) → 无依赖，可并行
- Task 22 (构建验证) → 依赖所有前序任务完成
