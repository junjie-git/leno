# 订单与交易域 (Order & Transaction) 开发任务

> **限界上下文**: BC4 订单与交易域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis / RabbitMQ / Elasticsearch  
> **依赖**: `shared-kernel`、`user-auth`（地址快照）、`product`（SKU 价格/库存）、`promotion`（优惠）、`payment`（支付结果）、`points-membership`（积分抵现）  
> **对应文档**: `04-订单与交易域.md`

---

## 模块概述

订单与交易域是平台交易核心，承载订单创建、状态机流转、库存预占与扣减、支付请求发起与结果消费、履约发货与确认收货、超时自动取消与多视角查询。采用 CQRS 模式，写侧 EF Core + Redis Lua 库存原子操作，读侧 ES 同步。

---

## Task 1: 项目初始化与领域层 — Order 聚合

**文件:**
- Create: `src/Services/Order/Leno.Order.Domain/Leno.Order.Domain.csproj`
- Create: `src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Aggregates/OrderItem.cs`

- [ ] 创建 Leno.Order.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `Order` 聚合根（OrderId、OrderNo、OrderType、UserId、SellerId、Items、ItemsAmount、DiscountAmount、PointsOffsetAmount、FreightAmount、TotalAmount、Status、AddressSnapshot、PaymentMethod、ExpireAt、PaidAt、PaymentId、TradeNo、ShippedAt、LogisticsNo、CompletedAt、AfterSalesWindowEndsAt、CancelledAt、CancelReason、Version）
- [ ] 实现 `Order.Create` 工厂方法（校验金额恒等式 `TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount`，生成订单号，置待支付，设 ExpireAt=创建+30min，附加 `OrderCreatedEvent`）
- [ ] 实现 `Order.ApplyDiscount(discountAllocations)`（按行分摊优惠，校验各行分摊之和=优惠总额且不超行小计）
- [ ] 实现 `Order.ApplyPointsOffset(pointsOffsetAmount)`（校验 ≤ ItemsAmount - DiscountAmount 且满足上限，更新 TotalAmount）
- [ ] 实现 `Order.MarkAsPaid(paymentId, channel, paidAt, tradeNo)`（待支付→已支付，附加 `OrderPaidEvent`）
- [ ] 实现 `Order.Ship(logisticsNo, shippedAt, operatorId)`（已支付→已发货，附加 `OrderShippedEvent`）
- [ ] 实现 `Order.ConfirmReceipt()`（已发货→已完成，设 AfterSalesWindowEndsAt，附加 `OrderCompletedEvent`）
- [ ] 实现 `Order.CompleteMembershipOrder()`（会员订阅订单专用，已支付→已完成）
- [ ] 实现 `Order.CloseAfterSalesWindow()`（售后期结束，附加 `OrderAfterSalesWindowClosedEvent`）
- [ ] 实现 `Order.Cancel(reason, cancelledBy)`（仅待支付态，附加 `OrderCancelledEvent`，携带 PointsToRelease）
- [ ] 实现 `OrderItem` 实体（SkuId、ProductSnapshot、UnitPrice、Quantity、DiscountAllocation、Subtotal、SourceCartItemId）
- [ ] 编写单元测试覆盖状态机与金额恒等式
- [ ] 提交：`feat(order): add Order aggregate root with state machine`

---

## Task 2: 领域层 — StockReservation 聚合

**文件:**
- Create: `src/Services/Order/Leno.Order.Domain/Aggregates/StockReservation.cs`

- [ ] 实现 `StockReservation` 聚合根（SkuId、AvailableQty、ReservedQty、DeductedQty、Version）
- [ ] 实现不变量：`AvailableQty = 基线 - ReservedQty - DeductedQty ≥ 0`
- [ ] 实现 `ReserveStock(orderId, quantity)`（校验余量，ReservedQty+=quantity，附加 `StockReservedEvent`）
- [ ] 实现 `ConfirmStockDeduction(orderId, quantity)`（ReservedQty-=quantity，DeductedQty+=quantity，附加 `StockConfirmedEvent`）
- [ ] 实现 `ReleaseStock(orderId, quantity)`（ReservedQty-=quantity，附加 `StockReleasedEvent`）
- [ ] 实现 `Replenish(delta)`（商品域补货事件驱动）
- [ ] 编写单元测试覆盖库存预占/扣减/释放
- [ ] 提交：`feat(order): add StockReservation aggregate`

---

## Task 3: 领域层 — 物流公司与运费模板聚合

**文件:**
- Create: `src/Services/Order/Leno.Order.Domain/Aggregates/LogisticsCompany.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Aggregates/FreightTemplate.cs`
- Create: `src/Services/Order/Leno.Order.Domain/ValueObjects/FreightRegionRule.cs`

- [ ] 实现 `LogisticsCompany` 聚合根（CompanyId、Name、Code、ServicePhone、SupportTracking、Status、Version）
- [ ] 实现 `LogisticsCompany.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现 `FreightTemplate` 聚合根（TemplateId、Name、Type、FreeShippingThreshold、RegionRules、Status、Version）
- [ ] 实现 `FreightTemplate.Create`/`UpdateRules`/`Enable`/`Disable` 方法
- [ ] 实现 `FreightRegionRule` 值对象（地区编码、首重/首件单位、首价、续单位、续价）
- [ ] 编写单元测试
- [ ] 提交：`feat(order): add logistics company and freight template aggregates`

---

## Task 4: 领域层 — 领域服务与仓储接口

**文件:**
- Create: `src/Services/Order/Leno.Order.Domain/Services/IStockReservationDomainService.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Services/IOrderPricingDomainService.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Services/IOrderNumberGenerator.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Services/IFreightCalculator.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Repositories/IOrderRepository.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Repositories/ILogisticsCompanyRepository.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Repositories/IFreightTemplateRepository.cs`

- [ ] 定义 `IStockReservationDomainService`（ReserveBatchAsync 批量预占任一失败回滚、ConfirmBatchAsync、ReleaseBatchAsync）
- [ ] 定义 `IOrderPricingDomainService`（ValidatePricesAsync 价格防篡改、CalculateAndAllocate 优惠分摊与金额恒等式校验）
- [ ] 定义 `IOrderNumberGenerator`（订单号生成规则：前缀+yyyyMMdd+机器标识+序列号）
- [ ] 定义 `IFreightCalculator`（按卖家模板与地址区域计算运费）
- [ ] 定义 `IOrderRepository`（GetByIdAsync、GetByOrderNoAsync、QueryAsync、AddAsync、UpdateAsync）
- [ ] 定义 `IInventoryRepository`（ReserveAsync、ConfirmAsync、ReleaseAsync、GetAsync — Redis Lua 实现）
- [ ] 定义物流公司与运费模板仓储接口
- [ ] 提交：`feat(order): add domain services and repository interfaces`

---

## Task 5: 领域事件定义

**文件:**
- Create: `src/Services/Order/Leno.Order.Domain/Events/OrderCreatedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/OrderPaidEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/OrderShippedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/OrderCompletedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/OrderAfterSalesWindowClosedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/OrderCancelledEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/StockReservedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/StockConfirmedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/StockReleasedEvent.cs`
- Create: `src/Services/Order/Leno.Order.Domain/Events/PaymentRequestedIntegrationEvent.cs`

- [ ] 定义所有领域事件，携带关键字段（见文档第3章事件清单）
- [ ] `OrderCreatedEvent` 消费方：购物车域、库存、促销、MQ延迟消息、通知域、ES
- [ ] `OrderPaidEvent` 消费方：库存确认、促销核销、积分扣减、卖家通知、ES
- [ ] `OrderCompletedEvent` 消费方：评价域、积分域、卖家域、MQ延迟消息、ES
- [ ] `OrderCancelledEvent` 携带 PointsToRelease 供积分域释放冻结
- [ ] `PaymentRequestedIntegrationEvent` 消费方：支付集成域
- [ ] 提交：`feat(order): add domain integration events`

---

## Task 6: 基础设施层 — EF Core 仓储与 Redis Lua 库存

**文件:**
- Create: `src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Repositories/EfCoreOrderRepository.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Repositories/EfCoreLogisticsCompanyRepository.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Repositories/EfCoreFreightTemplateRepository.cs`

- [ ] 实现 `OrderDbContext`（DbSet<Order>、DbSet<LogisticsCompany>、DbSet<FreightTemplate>）
- [ ] 配置 Order 实体映射（OrderItem 为 Owned Collection，AddressSnapshot/ProductSnapshot 为 Owned Types）
- [ ] 实现 `EfCoreOrderRepository`（含按 UserId/SellerId/Status/时间区间分页查询）
- [ ] 实现 `RedisInventoryRepository`（Lua 脚本原子完成"判余量+扣减+记录预占"）
- [ ] 实现物流公司与运费模板仓储
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证 Redis Lua 原子性与 EF Core 映射
- [ ] 提交：`feat(order): add EF Core repositories and Redis Lua inventory`

---

## Task 7: 基础设施层 — ES 读模型与事件消费者

**文件:**
- Create: `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModel.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModelSyncConsumer.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentFailedEventConsumer.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Consumers/RefundCompletedEventConsumer.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Consumers/StockAdjustedEventConsumer.cs`

- [ ] 定义 `OrderReadModel`（orderId、orderNo、userId、sellerId、status、items、totalAmount、时间线）
- [ ] 实现 `OrderReadModelSyncConsumer`（消费 OrderCreatedEvent/PaidEvent/ShippedEvent/CompletedEvent/CancelledEvent 同步 ES）
- [ ] 实现 `PaymentSucceededEventConsumer`（加载 Order→校验金额→MarkAsPaid→发布 OrderPaidEvent）
- [ ] 实现 `PaymentFailedEventConsumer`（关单或保持待支付）
- [ ] 实现 `OrderTimeoutDelayMessageConsumer`（MQ 延迟消息到达→检查 ExpireAt→Cancel）
- [ ] 实现 `RefundCompletedEventConsumer`（回滚销量与库存）
- [ ] 实现 `StockAdjustedEventConsumer`（同步商品域库存基线）
- [ ] 编写集成测试验证事件消费链路
- [ ] 提交：`feat(order): add ES read model and event consumers`

---

## Task 8: 应用层 — 创建订单用例（F-ORD-001/002）

**文件:**
- Create: `src/Services/Order/Leno.Order.Application/IOrderAppService.cs`
- Create: `src/Services/Order/Leno.Order.Application/Commands/CreateOrderCommand.cs`
- Create: `src/Services/Order/Leno.Order.Application/Commands/BuyNowCommand.cs`
- Create: `src/Services/Order/Leno.Order.Application/Handlers/CreateOrderCommandHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/DTOs/OrderDto.cs`

- [ ] 定义 `IOrderAppService` 接口
- [ ] 实现 `CreateOrderCommandHandler`（从购物车结算）：
  - [ ] 加载 Cart 聚合取选中项
  - [ ] 防腐层查询商品域 SKU 现价/可售状态
  - [ ] 防腐层查询促销域适用优惠
  - [ ] 积分抵现试算与冻结（调用积分域）
  - [ ] `IOrderPricingDomainService.ValidatePricesAsync` 价格校验
  - [ ] `IOrderPricingDomainService.CalculateAndAllocate` 优惠分摊
  - [ ] `IStockReservationDomainService.ReserveBatchAsync` 库存预占（任一失败回滚）
  - [ ] 按卖家拆分为多个 Order 聚合
  - [ ] `Order.Create` 生成订单（含 ApplyDiscount、ApplyPointsOffset）
  - [ ] UnitOfWork 提交（聚合+发件箱）
- [ ] 实现 `BuyNowCommand` 处理（直接购买，不经购物车）
- [ ] 支持 `Idempotency-Key` 防重复下单
- [ ] 编写单元测试覆盖创建订单全链路
- [ ] 提交：`feat(order): add create order command handlers`

---

## Task 9: 应用层 — 支付、发货、确认收货用例（F-ORD-005~010）

**文件:**
- Create: `src/Services/Order/Leno.Order.Application/Handlers/PayOrderCommandHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/Handlers/ShipOrderCommandHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/Handlers/ConfirmReceiptCommandHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/Handlers/CancelOrderCommandHandler.cs`

- [ ] 实现 `PayOrderCommandHandler`（校验待支付→发布 `PaymentRequestedIntegrationEvent`→返回支付凭证）
- [ ] 实现 `ShipOrderCommandHandler`（校验已支付→Ship→发布 OrderShippedEvent）
- [ ] 实现 `ConfirmReceiptCommandHandler`（校验已发货→ConfirmReceipt→发布 OrderCompletedEvent）
- [ ] 实现 `CancelOrderCommandHandler`（仅待支付→Cancel→释放库存→退还优惠券→释放冻结积分）
- [ ] 编写单元测试
- [ ] 提交：`feat(order): add pay, ship, confirm receipt and cancel handlers`

---

## Task 10: 应用层 — 订单查询与物流轨迹（F-ORD-016~020）

**文件:**
- Create: `src/Services/Order/Leno.Order.Application/Services/OrderQueryService.cs`
- Create: `src/Services/Order/Leno.Order.Application/Services/LogisticsTrackingService.cs`

- [ ] 实现买家视角查询（GET /api/orders，按状态/时间筛选分页，ES 读库）
- [ ] 实现卖家视角查询（GET /api/seller/orders，按店铺订单筛选）
- [ ] 实现运营视角查询（GET /api/admin/orders，全平台分页查询）
- [ ] 实现订单详情（GET /api/orders/{id}，含订单行、地址快照、物流信息、时间线）
- [ ] 实现物流轨迹查询（GET /api/orders/{id}/logistics，对接物流查询接口）
- [ ] 实现订单预览（POST /api/orders/preview，结算页金额预览不创建订单）
- [ ] 编写集成测试
- [ ] 提交：`feat(order): add order query services and logistics tracking`

---

## Task 11: 应用层 — 物流公司管理与运费模板配置（F-ORD-021~023）

**文件:**
- Create: `src/Services/Order/Leno.Order.Application/ILogisticsCompanyAppService.cs`
- Create: `src/Services/Order/Leno.Order.Application/IFreightTemplateAppService.cs`
- Create: `src/Services/Order/Leno.Order.Application/Handlers/ForceCancelOrderCommandHandler.cs`

- [ ] 实现物流公司管理用例（CRUD + 启停，运营维护）
- [ ] 实现运费模板配置用例（CRUD + 启停，运营/卖家配置）
- [ ] 实现运营强制取消异常订单用例（记录操作人与原因，触发取消流程与库存释放）
- [ ] 编写单元测试
- [ ] 提交：`feat(order): add logistics, freight template and force cancel services`

---

## Task 12: 表现层 — API 控制器

**文件:**
- Create: `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs`
- Create: `src/Services/Order/Leno.Order.Api/Controllers/PaymentsController.cs`
- Create: `src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs`
- Create: `src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs`

- [ ] 实现 `OrdersController`（POST /api/orders、POST /api/orders/buy-now、POST /api/orders/preview、GET /api/orders、GET /api/orders/{id}）
- [ ] 实现 POST /api/orders/{id}/ship（卖家发货）、POST /api/orders/{id}/confirm（确认收货）、POST /api/orders/{id}/cancel
- [ ] 实现 `PaymentsController`（POST /api/payments 发起支付，转发为 PaymentRequestedIntegrationEvent）
- [ ] 实现运营端接口（GET /api/admin/orders、POST /api/admin/orders/{id}/force-cancel）
- [ ] 实现物流公司与运费模板管理端点
- [ ] 配置 JWT 鉴权与角色策略
- [ ] 编写 API 集成测试覆盖下单→支付→发货→收货→完成全流程
- [ ] 提交：`feat(order): add API controllers`
