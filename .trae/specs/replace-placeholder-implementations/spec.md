# 替换占位实现为真实业务逻辑 Spec

## Why

代码库中存在 30 处占位/桩实现，分布在 7 个模块的 18 个文件中。这些占位实现导致核心业务流程无法走通：下单时商品/促销/积分防腐层返回空值或零值、支付渠道仅模拟不真实调用、短信/邮件渠道无法发送、售后与评价资格校验默认放行、事件消费者无幂等去重、定时任务无法手动触发。需将这些占位实现替换为真实业务逻辑代码，使各模块具备生产可用能力。

## What Changes

### 一、基础设施层（BuildingBlocks）
- **Redis 幂等去重基类**：为 `IntegrationEventConsumerBase<T>` 提供基于 Redis 的幂等去重默认实现（`IsProcessedAsync`/`MarkAsProcessedAsync`），所有消费者可选择继承 Redis 版基类获得幂等能力。

### 二、目标域查询 API 新增（供跨域防腐层调用）
- **Product 域**：新增按 `skuId` 单个/批量查询 SKU 概要的内部端点（价格、可售状态、标题、主图、卖家标识）。
- **Promotion 域**：新增折扣试算端点（入参 userId + 订单项列表，返回优惠总金额）。
- **PointsMembership 域**：新增积分试扣、冻结、释放三个端点。
- **UserAuth 域**：新增按 `userId` 查询用户联系方式（手机号、邮箱，未脱敏）的内部端点。
- **Order 域**：新增按 `orderId` 查询订单状态概要的内部端点（供售后/评价资格校验使用）。
- **Payment 域**：新增按 `orderId` 查询支付信息概要的内部端点（供售后域关联支付单）。
- 所有新增内部端点使用内部 API Key 头部鉴权（`X-Internal-Key`），不依赖用户 JWT。

### 三、跨域防腐层 HTTP 客户端实现（消费侧）
- **Cart 域**：`CartPriceService` 改为 HttpClient 调用 Product 域 SKU 查询端点。
- **Order 域**：`ProductAntiCorruptionService`、`PromotionAntiCorruptionService`、`PointsAntiCorruptionService`（3 个方法）改为 HttpClient 调用各自目标域端点。
- **ReviewAfterSales 域**：`PaymentInfoQueryService` 改为 HttpClient 调用 Payment 域端点；`AfterSalesEligibilityChecker`、`ReviewEligibilityChecker` 改为 HttpClient 调用 Order 域端点并实现真实校验逻辑。
- **Notification 域**：新增用户联系方式防腐层，供 `SmsChannel`/`EmailChannel` 查询收件人手机号/邮箱。

### 四、第三方渠道真实实现
- **Payment 域**：`AlipayClient`（4 方法）和 `WeChatPayClient`（4 方法）将 `SimulateXxxResponse()` 模拟调用替换为真实 `HttpClient` HTTP 调用与响应解析。
- **Notification 域**：`SmtpClientWrapper` 使用 `MailKit` 真实发送邮件；`SmsClient` 通过 HttpClient 调用阿里云/腾讯云短信 API。
- **Order 域**：`LogisticsTrackingService` 通过 HttpClient 调用第三方物流轨迹查询 API。

### 五、上下文与事件契约修复
- **Order 域**：`OrderAppService.ShipAsync` 注入 `ICurrentUserContext` 获取发货操作人标识（替换 `Guid.Empty`）；`OrderPricingDomainService.ValidatePricesAsync` 接入商品域防腐层校验下单价格。
- **SharedContracts**：`RefundCompletedEvent` 和 `PaymentFailedEvent` 新增 `UserId` 字段，发布方同步填充。
- **Notification 域**：`AfterSalesEventConsumer`（RefundCompletedEvent）和 `PaymentEventConsumer`（PaymentFailedEvent）使用事件中的 `UserId` 发送通知（替换跳过逻辑）。
- **SystemAdmin 域**：`ScheduledTaskExecutor.RunNowAsync` 改为通过 `ISchedulerFactory` 获取调度器并调用 `TriggerJob` 立即触发作业。

## Impact

- **Affected specs**: PaymentEvents 契约（SharedContracts）、IntegrationEventConsumerBase（BuildingBlocks）、各域防腐层接口与实现
- **Affected code**:
  - BuildingBlocks: `Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs`
  - SharedContracts: `Events/PaymentEvents.cs`
  - Product: `Controllers/`（新增内部查询控制器）、`Application/`（新增查询服务）
  - Promotion: `Controllers/`、`Application/`（新增折扣试算）
  - PointsMembership: `Controllers/`、`Application/`（新增积分操作）、`Domain/`（暴露冻结/释放）
  - UserAuth: `Controllers/`、`Application/`（新增联系方式查询）
  - Order: `Application/Services/`（ShipAsync、LogisticsTrackingService）、`Infrastructure/Services/`（防腐层、价格校验）、`Infrastructure/Dependencies/`
  - Cart: `Infrastructure/Services/CartPriceService.cs`、`Infrastructure/Dependencies/`
  - ReviewAfterSales: `Infrastructure/Services/`（3 个防腐层）、`Infrastructure/Dependencies/`
  - Payment: `Infrastructure/Channels/Alipay/AlipayClient.cs`、`Infrastructure/Channels/WeChatPay/WeChatPayClient.cs`
  - Notification: `Infrastructure/Channels/`（SMTP、SMS、渠道）、`Infrastructure/Consumers/`（2 个消费者）、`Infrastructure/Dependencies/`
  - SystemAdmin: `Infrastructure/Services/ScheduledTaskExecutor.cs`

## ADDED Requirements

### Requirement: 内部服务间查询鉴权
系统 SHALL 提供基于 `X-Internal-Key` 请求头的内部服务间鉴权机制，用于跨域查询端点的访问控制。内部端点不依赖用户 JWT，仅校验请求头中的内部密钥是否与配置值匹配。

#### Scenario: 合法内部请求
- **WHEN** 服务以正确的 `X-Internal-Key` 头部调用内部查询端点
- **THEN** 请求被授权，返回查询结果

#### Scenario: 非法或缺失内部密钥
- **WHEN** 请求未携带 `X-Internal-Key` 或密钥不匹配
- **THEN** 返回 401 Unauthorized

### Requirement: Redis 幂等去重基类
系统 SHALL 提供 `RedisIntegrationEventConsumerBase<T>` 抽象基类，继承自 `IntegrationEventConsumerBase<T>`，使用 Redis SET NX 实现事件幂等去重。

#### Scenario: 首次处理事件
- **WHEN** 消费者收到事件且 Redis 中不存在该 EventId 的记录
- **THEN** `IsProcessedAsync` 返回 false，`HandleAsync` 执行，`MarkAsProcessedAsync` 写入 Redis（TTL 24 小时）

#### Scenario: 重复事件
- **WHEN** 消费者收到事件且 Redis 中已存在该 EventId 的记录
- **THEN** `IsProcessedAsync` 返回 true，跳过处理

### Requirement: Product 域 SKU 查询端点
Product 域 SHALL 提供内部端点 `GET internal/products/skus/{skuId}` 和 `POST internal/products/skus/batch`，返回 SKU 的价格、可售状态、标题、主图、卖家标识。

### Requirement: Promotion 域折扣试算端点
Promotion 域 SHALL 提供内部端点 `POST internal/promotions/calculate`，入参为 userId 与订单项列表，返回适用优惠总金额。

### Requirement: PointsMembership 域积分操作端点
PointsMembership 域 SHALL 提供三个内部端点：`POST internal/points/trial-offset`（试扣试算）、`POST internal/points/freeze`（冻结积分）、`POST internal/points/release`（释放积分）。

### Requirement: UserAuth 域联系方式查询端点
UserAuth 域 SHALL 提供内部端点 `GET internal/users/{userId}/contacts`，返回用户手机号与邮箱（未脱敏）。

### Requirement: Order 域订单状态查询内部端点
Order 域 SHALL 提供内部端点 `GET internal/orders/{orderId}/status`，返回订单状态、买家标识、订单行列表（含 skuId、数量、售后状态）。

### Requirement: Payment 域支付信息查询内部端点
Payment 域 SHALL 提供内部端点 `GET internal/payments/{orderId}/info`，返回支付单标识与支付渠道。

### Requirement: 真实第三方支付渠道调用
Payment 域的 `AlipayClient` 和 `WeChatPayClient` SHALL 通过 HttpClient 向真实支付网关发起 HTTP 请求，解析真实响应，不再使用模拟响应。

### Requirement: 真实通知渠道发送
Notification 域的 `SmtpClientWrapper` SHALL 使用 MailKit 通过 SMTP 协议发送真实邮件；`SmsClient` SHALL 通过 HttpClient 调用短信服务商 API 发送真实短信。

### Requirement: 真实物流轨迹查询
Order 域的 `LogisticsTrackingService` SHALL 通过 HttpClient 调用物流轨迹查询 API，返回真实轨迹节点。

### Requirement: 售后与评价资格真实校验
ReviewAfterSales 域的 `AfterSalesEligibilityChecker` SHALL 通过调用 Order 域内部端点校验：订单在售后期内、无重复售后、申请人为订单买家；`ReviewEligibilityChecker` SHALL 校验：订单已完成、未重复评价、在评价期内、申请人为订单买家。

### Requirement: 下单价格真实校验
Order 域的 `OrderPricingDomainService.ValidatePricesAsync` SHALL 通过商品域防腐层查询 SKU 实际售价，校验下单时传入的 `ExpectedPrice` 与实际售价一致，不一致时抛出领域异常。

### Requirement: 发货操作人标识
Order 域的 `OrderAppService.ShipAsync` SHALL 从 `ICurrentUserContext` 获取当前操作人标识并传入 `Order.Ship`，不再使用 `Guid.Empty`。

### Requirement: 定时任务手动触发
SystemAdmin 域的 `ScheduledTaskExecutor.RunNowAsync` SHALL 通过 `ISchedulerFactory` 获取 Quartz 调度器并调用 `TriggerJob` 立即触发对应作业执行。

## MODIFIED Requirements

### Requirement: PaymentFailedEvent 事件契约
`PaymentFailedEvent` 新增 `UserId` 字段（`Guid`），由 Payment 域在发布事件时从支付单中填充。Notification 域消费者使用该字段发送支付失败通知，不再跳过。

### Requirement: RefundCompletedEvent 事件契约
`RefundCompletedEvent` 新增 `UserId` 字段（`Guid`），由 Payment 域在发布事件时从关联的售后单/订单中填充。Notification 域消费者使用该字段发送退款完成通知，不再跳过。

### Requirement: 通知渠道收件人查询
`SmsChannel.SendAsync` 和 `EmailChannel.SendAsync` SHALL 通过用户联系方式防腐层按 `record.UserId` 查询真实手机号/邮箱地址，不再使用空字符串占位。

### Requirement: 跨域防腐层 HTTP 实现
Cart 域 `CartPriceService`、Order 域三个防腐层服务、ReviewAfterSales 域三个防腐层服务 SHALL 使用 HttpClient（通过 `IHttpClientFactory` 类型化客户端注册）调用目标域内部查询端点，不再返回占位值。
