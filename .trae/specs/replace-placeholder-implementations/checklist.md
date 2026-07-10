# Checklist

## 阶段一：基础设施

- [x] `InternalApiKeyMiddleware` 已创建，校验 `X-Internal-Key` 请求头，不匹配返回 401
- [x] `InternalApiKeyOptions` 配置类与 `AddInternalApiKeyAuth` 扩展方法已创建
- [x] 各域 API 的 `Program.cs` 已注册内部鉴权中间件，应用于 `internal/` 路由前缀
- [x] `RedisIntegrationEventConsumerBase<T>` 已创建，继承 `IntegrationEventConsumerBase<T>`，注入 `IConnectionMultiplexer`
- [x] `IsProcessedAsync` 使用 Redis `StringGetAsync` 查询 `evt:processed:{eventId}` 键
- [x] `MarkAsProcessedAsync` 使用 Redis `StringSetAsync` 写入键，TTL 24 小时
- [x] 所有现有事件消费者已改为继承 `RedisIntegrationEventConsumerBase<T>`

## 阶段二：目标域内部查询 API

- [ ] Product 域 `IProductInternalQueryService` 接口与 `SkuInfoResultDto` 已创建
- [ ] Product 域 `ProductInternalQueryService` 实现通过 SKU 仓储查询并组装 DTO
- [ ] Product 域 `InternalProductsController` 提供 `GET internal/products/skus/{skuId}` 和 `POST internal/products/skus/batch`
- [ ] Promotion 域 `IPromotionCalculateAppService` 接口与 DTO 已创建
- [ ] Promotion 域 `PromotionCalculateAppService` 实现查询用户优惠券与满减活动，计算优惠总金额
- [ ] Promotion 域 `InternalPromotionsController` 提供 `POST internal/promotions/calculate`
- [ ] PointsMembership 域 `PointsAccount` 聚合已新增 `TryOffset`/`Freeze`/`Release` 领域方法（如尚不存在）
- [ ] PointsMembership 域 `IPointsInternalAppService` 接口与 DTO 已创建
- [ ] PointsMembership 域 `PointsInternalAppService` 实现调用聚合方法并持久化
- [ ] PointsMembership 域 `InternalPointsController` 提供 trial-offset、freeze、release 三个端点
- [ ] UserAuth 域 `IUserInternalQueryService` 接口与 `UserContactsDto` 已创建（未脱敏）
- [ ] UserAuth 域 `UserInternalQueryService` 实现通过用户仓储查询真实联系方式
- [ ] UserAuth 域 `InternalUsersController` 提供 `GET internal/users/{userId}/contacts`
- [ ] Order 域 `IOrderInternalQueryService` 接口与 `OrderStatusResultDto` 已创建
- [ ] Order 域 `OrderInternalQueryService` 实现通过订单仓储查询并组装 DTO
- [ ] Order 域 `InternalOrdersController` 提供 `GET internal/orders/{orderId}/status`
- [ ] Payment 域 `IPaymentInternalQueryService` 接口与 `PaymentInfoResultDto` 已创建
- [ ] Payment 域 `PaymentInternalQueryService` 实现通过支付仓储按 orderId 查询
- [ ] Payment 域 `InternalPaymentsController` 提供 `GET internal/payments/{orderId}/info`

## 阶段三：跨域防腐层 HTTP 客户端实现

- [ ] Cart 域 `CartPriceService` 已注入 HttpClient 与内部密钥配置
- [ ] Cart 域 `GetSkuPricesAsync` 调用 Product 域批量 SKU 查询端点，解析响应为 `SkuPriceSnapshot`
- [ ] Cart 域 `ServiceCollectionExtensions` 已注册 `AddHttpClient<CartPriceService>`
- [ ] Order 域 `ProductAntiCorruptionService` 已注入 HttpClient，调用 Product 域 SKU 查询端点
- [ ] Order 域 `PromotionAntiCorruptionService` 已注入 HttpClient，调用 Promotion 域折扣试算端点
- [ ] Order 域 `PointsAntiCorruptionService` 三个方法已分别调用 PointsMembership 域三个端点
- [ ] Order 域 `ServiceCollectionExtensions` 已为三个防腐层注册 `AddHttpClient<TypedClient>`
- [ ] ReviewAfterSales 域 `PaymentInfoQueryService` 已注入 HttpClient，调用 Payment 域端点
- [ ] ReviewAfterSales 域 `AfterSalesEligibilityChecker` 调用 Order 域端点校验售后期、申请人、重复售后
- [ ] ReviewAfterSales 域 `ReviewEligibilityChecker` 调用 Order 域端点校验订单完成、评价期、申请人、重复评价
- [ ] ReviewAfterSales 域 `ServiceCollectionExtensions` 已为三个服务注册 `AddHttpClient`
- [ ] Notification 域 `UserContactAntiCorruptionService` 已创建，调用 UserAuth 域联系方式查询端点
- [ ] Notification 域 `SmsChannel.SendAsync` 通过防腐层查询手机号，不再使用空字符串
- [ ] Notification 域 `EmailChannel.SendAsync` 通过防腐层查询邮箱，不再使用空字符串
- [ ] Notification 域 `ServiceCollectionExtensions` 已注册 `AddHttpClient<UserContactAntiCorruptionService>`

## 阶段四：第三方渠道真实实现

- [ ] Payment 域 `AlipayClient.PreCreateAsync` 通过 HttpClient 调用真实支付宝网关，不再使用 `SimulatePreCreateResponse`
- [ ] Payment 域 `AlipayClient.QueryAsync` 通过 HttpClient 调用真实 `alipay.trade.query`
- [ ] Payment 域 `AlipayClient.RefundAsync` 通过 HttpClient 调用真实 `alipay.trade.refund`
- [ ] Payment 域 `AlipayClient.QueryRefundAsync` 通过 HttpClient 调用真实退款查询
- [ ] Payment 域 `AlipayClient` 的四个 `Simulate*Response` 模拟方法已移除
- [ ] Payment 域 `WeChatPayClient.UnifiedOrderAsync` 通过 HttpClient POST XML 调用真实统一下单
- [ ] Payment 域 `WeChatPayClient.QueryOrderAsync` 通过 HttpClient 调用真实订单查询
- [ ] Payment 域 `WeChatPayClient.RefundAsync` 通过 HttpClient 调用真实退款（含商户证书）
- [ ] Payment 域 `WeChatPayClient.QueryRefundAsync` 通过 HttpClient 调用真实退款查询
- [ ] Payment 域 `WeChatPayClient` 的四个 `Simulate*Response` 模拟方法已移除
- [ ] Notification 域 `Notification.Infrastructure.csproj` 已添加 `MailKit` 包引用
- [ ] Notification 域 `SmtpClientWrapper.SendAsync` 使用 MailKit 真实发送邮件
- [ ] Notification 域 `SmtpClientWrapper` 有连接/认证/发送失败的异常处理
- [ ] Notification 域 `SmsClient.SendAsync` 通过 HttpClient 调用短信服务商 API
- [ ] Notification 域 `SmsOptions` 已补充服务商类型、API 地址、签名等配置项
- [ ] Order 域 `LogisticsTrackingService` 已注入 HttpClient，调用物流轨迹查询 API
- [ ] Order 域 `LogisticsTrackingService` 解析响应映射为 `LogisticsTrackingNode` 列表
- [ ] Order 域 `appsettings.json` 已补充物流 API 配置
- [ ] Order 域 `LogisticsTrackingService` 查询失败时返回空节点列表而非占位文本

## 阶段五：上下文与事件契约修复

- [ ] `PaymentFailedEvent` 已新增 `UserId` 属性
- [ ] `RefundCompletedEvent` 已新增 `UserId` 属性
- [ ] Payment 域发布 `PaymentFailedEvent` 时已填充 `UserId`
- [ ] Payment 域发布 `RefundCompletedEvent` 时已填充 `UserId`
- [ ] Notification 域 `PaymentEventConsumer` 使用事件 `UserId` 发送支付失败通知，不再跳过
- [ ] Notification 域 `AfterSalesEventConsumer` 使用事件 `UserId` 发送退款完成通知，不再跳过
- [ ] Order 域 `OrderAppService` 构造函数已注入 `ICurrentUserContext`
- [ ] Order 域 `ShipAsync` 从 `ICurrentUserContext` 获取操作人标识，不再传 `Guid.Empty`
- [ ] Order 域 `OrderPricingDomainService.ValidatePricesAsync` 通过商品防腐层校验价格一致性
- [ ] SystemAdmin 域 `ScheduledTaskExecutor` 已注入 `ISchedulerFactory`
- [ ] SystemAdmin 域 `RunNowAsync` 通过 `IScheduler.TriggerJob` 立即触发作业

## 阶段六：构建验证

- [ ] `dotnet build Leno.slnx` 通过，0 Error / 0 Warning
- [ ] 无 CA1859 警告（使用具体类型如 Dictionary、List）
- [ ] 无 CA1305 警告（ToString 使用 CultureInfo.InvariantCulture）
- [ ] 无 CA1716 警告（无关键字参数名）
- [ ] 无 CA1711 警告（无不当后缀）
- [ ] 无 CA1725 警告（参数名匹配接口声明）
- [ ] 各任务按 `feat(<scope>): <description>` 规范提交并推送到远程 dev 分支
- [ ] 代码中不再存在 `NotImplementedException`、`Simulate*Response`、占位注释（TODO/占位/桩实现/stub/placeholder 等）
