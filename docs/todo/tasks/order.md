# 订单与交易域 - 任务执行计划

> **模块**: BC4 订单与交易域
> **对应文档**: `04-订单与交易域.md`
> **任务 ID 前缀**: ORD
> **总任务数**: 9 | **P0**: 3 | **P1**: 5 | **P2**: 1

---

## 模块概述

订单域是交易核心，承载下单到完结的全链路状态流转。已实现核心功能（下单、支付、发货、确认收货、取消、物流公司、运费模板），但缺失库存预占 Redis Lua 脚本、超时取消、积分抵现、优惠分摊、售后期结束事件、会员订阅订单、物流轨迹查询与运营强制取消。

---

## Task ORD-01: 测试项目创建 [P0]

### 子任务 Checklist

- [x] ORD-01.1: 创建 `Leno.Order.Domain.Tests` 项目
- [x] ORD-01.2: 创建 `Leno.Order.Application.Tests` 项目
- [x] ORD-01.3: 创建 `Leno.Order.Api.Tests` 项目
- [x] ORD-01.4: 覆盖 Order 聚合（Create、ApplyDiscount、ApplyPointsOffset、MarkAsPaid、Ship、ConfirmReceipt、Cancel、CloseAfterSalesWindow、CompleteMembershipOrder、ForceCancel）
- [x] ORD-01.5: 覆盖 StockReservation 聚合（ReserveStock、ConfirmStockDeduction、ReleaseStock、Replenish）
- [x] ORD-01.6: 覆盖 OrderItem 实体（Create、ApplyDiscount）
- [x] ORD-01.7: 覆盖金额恒等式校验（TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount）
- [ ] ORD-01.8: 配置测试覆盖率 ≥ 80%

### 验收标准
- [x] 领域层单元测试 55 项（Order 40 + StockReservation 15 + OrderItem 8）
- [x] 应用层单元测试 14 项（GetById、Ship、ConfirmReceipt、Cancel、ForceCancel、PayAsync、QueryAsync）
- [x] API 集成测试 10 项（健康检查、认证、买家端4个、卖家端1个、运营端2个）
- [ ] 测试覆盖率 ≥ 80%（待配置 coverlet）

---

## Task ORD-02: 库存预占 Redis Lua 脚本 [P0]

### 子任务 Checklist

- [x] ORD-02.1: 在 `Leno.Order.Infrastructure` 中创建 `StockReservationCacheService`（实现 `IInventoryRepository`）
- [x] ORD-02.2: 实现 Lua 脚本：判余量（GET stockKey）→ 扣减（DECRBY）→ 记录预占（HSET reservationKey）
- [x] ORD-02.3: Redis Key 设计：`stock:{skuId}`（可售库存）、`reservation:{skuId}`（预占台账，Hash field=orderId）
- [x] ORD-02.4: 预占成功返回 1，库存不足返回 0，Lua 脚本执行失败返回 -1
- [x] ORD-02.5: 在 `CreateOrderCommandHandler` 中集成预占流程（先预占 → 再持久化订单）
- [x] ORD-02.6: 预占失败时回滚已预占的 SKU 库存
- [x] ORD-02.7: 支付成功时调用 `ConfirmAsync` 确认扣减
- [x] ORD-02.8: 订单取消时调用 `ReleaseAsync` 释放预占
- [x] ORD-02.9: 实现库存对账定时任务（Redis 与 DB 周期校正）
- [ ] ORD-02.10: 编写 Lua 脚本并发压测（模拟 1000 并发下单）

### 验收标准
- [x] Lua 脚本原子执行判余量 + 扣减 + 记录预占
- [ ] 并发下单不超卖
- [x] 预占失败返回明确错误
- [x] 库存预占/释放/确认完整闭环

---

## Task ORD-03: 订单超时自动取消 [P0]

### 子任务 Checklist

- [x] ORD-03.1: 订单创建时，通过 MassTransit 投递 30 分钟延迟消息（`ScheduleSend`）
- [x] ORD-03.2: 创建 `OrderTimeoutConsumer` 消费延迟消息
- [x] ORD-03.3: 消费时重新加载订单，检查状态：仍为 `PendingPayment` 则调用 `Order.Cancel`
- [x] ORD-03.4: 取消后发布 `OrderCancelledEvent`（携带被取消的订单信息）
- [x] ORD-03.5: 已支付/已取消订单忽略延迟消息（幂等）
- [x] ORD-03.6: 取消时释放预占库存（调用 `ReleaseAsync`）
- [x] ORD-03.7: 取消时释放冻结积分（事件携带积分数）
- [x] ORD-03.8: 取消时退还优惠券（事件携带 couponId）
- [ ] ORD-03.9: 编写超时取消端到端测试

### 验收标准
- [x] 订单创建后 30 分钟未支付自动取消
- [x] 取消后释放预占库存、退还优惠券、释放冻结积分
- [x] 已支付/已取消订单不重复处理

---

## Task ORD-04: 积分抵现 [P1]

### 子任务 Checklist

- [x] ORD-04.1: 在 Order 聚合中实现 `ApplyPointsOffset(pointsOffsetAmount)` 方法
- [x] ORD-04.2: 校验 `PointsOffsetAmount ≤ ItemsAmount - DiscountAmount`
- [x] ORD-04.3: 校验单笔订单积分抵扣上限（INV-18）
- [x] ORD-04.4: 更新 `TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount`
- [x] ORD-04.5: 应用层下单前调用积分域确认接口冻结积分（`POST internal/points/freeze`）
- [x] ORD-04.6: 支付成功时通过 `OrderPaidEvent` 驱动积分域正式扣减
- [x] ORD-04.7: 订单取消时通过 `OrderCancelledEvent` 驱动积分域释放冻结积分

### 验收标准
- [x] 订单支持积分抵扣金额
- [x] 积分抵扣金额 ≤ 商品金额 - 优惠金额
- [x] 100 积分 = 1 元
- [x] TotalAmount 正确计算

---

## Task ORD-05: 优惠分摊 [P1]

### 子任务 Checklist

- [x] ORD-05.1: 在 Order 聚合中实现 `ApplyDiscount(discountAmount, discountAllocations)` 方法
- [x] ORD-05.2: 校验各行分摊之和等于优惠总额
- [x] ORD-05.3: 校验各行分摊不超行小计
- [x] ORD-05.4: 更新各 OrderItem 的 `DiscountAllocation` 字段
- [x] ORD-05.5: 更新 `DiscountAmount` 与 `TotalAmount`
- [x] ORD-05.6: 下单时调用促销域计算结果后应用分摊
- [x] ORD-05.7: 编写优惠分摊计算单元测试

### 验收标准
- [x] 优惠金额按行分摊
- [x] 各行分摊之和 = 优惠总额
- [x] 各行分摊 ≤ 行小计
- [x] TotalAmount 正确计算

---

## Task ORD-06: 售后期结束事件 [P1]

### 子任务 Checklist

- [ ] ORD-06.1: 在 Order 聚合中实现 `CloseAfterSalesWindow()` 方法
- [ ] ORD-06.2: 校验 `Status == Completed` 且当前时间 ≥ `AfterSalesWindowEndsAt`
- [ ] ORD-06.3: 附加 `OrderAfterSalesWindowClosedEvent`（携带 PaidAmount）
- [ ] ORD-06.4: 订单完成时投递延迟消息（售后期天数后触发，默认 7 天）
- [ ] ORD-06.5: 创建 `AfterSalesWindowConsumer` 消费延迟消息，调用 `CloseAfterSalesWindow`
- [ ] ORD-06.6: 积分域消费 `OrderAfterSalesWindowClosedEvent` 发放消费返积分

### 验收标准
- [ ] 售后期结束时发布 OrderAfterSalesWindowClosedEvent
- [ ] 事件携带 PaidAmount 供积分域计算
- [ ] 仅已完成订单可关闭售后期

---

## Task ORD-07: 会员订阅订单 [P1]

### 子任务 Checklist

- [ ] ORD-07.1: 在 Order 聚合中实现 `CompleteMembershipOrder()` 方法
- [ ] ORD-07.2: 校验 `OrderType = MembershipSubscription` 且 `Status = Paid`
- [ ] ORD-07.3: 直接流转至 `Completed`，`AfterSalesWindowEndsAt = CompletedAt`
- [ ] ORD-07.4: 附加 `OrderCompletedEvent` 和 `OrderAfterSalesWindowClosedEvent`
- [ ] ORD-07.5: 会员订阅订单创建时不要求 SellerId（可空）
- [ ] ORD-07.6: 消费 `PaymentSucceededIntegrationEvent` 时检测 OrderType 自动调用完成方法

### 验收标准
- [ ] 会员订阅订单支付后直接完成
- [ ] 无发货流程、无售后期
- [ ] 发布 OrderCompletedEvent 和 OrderAfterSalesWindowClosedEvent

---

## Task ORD-08: 物流轨迹查询 [P1]

### 子任务 Checklist

- [ ] ORD-08.1: 在领域层定义 `ILogisticsTrackingService` 接口（`QueryTraceAsync(logisticsNo, companyCode)`）
- [ ] ORD-08.2: 在基础设施层实现物流公司 API 适配器（对接快递鸟/菜鸟等）
- [ ] ORD-08.3: 实现 `GET /api/orders/{id}/logistics-trace` 端点
- [ ] ORD-08.4: 校验物流公司 `SupportTracking` 属性，不支持时返回友好提示
- [ ] ORD-08.5: 物流轨迹缓存到 Redis（TTL 1 小时）
- [ ] ORD-08.6: 轨迹查询失败时返回缓存数据并标记

### 验收标准
- [ ] 支持查询已发货订单的物流轨迹
- [ ] 仅支持轨迹查询的物流公司可查
- [ ] 物流轨迹缓存优化

---

## Task ORD-09: 运营强制取消异常订单 [P2]

### 子任务 Checklist

- [ ] ORD-09.1: 完善 `POST /api/admin/orders/{id}/force-cancel` 端点（已有端点，需完善）
- [ ] ORD-09.2: 强制取消已支付订单：触发退款流程（发布 `RefundRequestedIntegrationEvent`）
- [ ] ORD-09.3: 强制取消待支付订单：直接取消（释放库存/积分/优惠券）
- [ ] ORD-09.4: 记录操作日志与审计日志
- [ ] ORD-09.5: 通知买卖双方（通过 BC10 通知域）
- [ ] ORD-09.6: 仅 Admin 角色可操作

### 验收标准
- [ ] 运营可强制取消异常订单
- [ ] 已支付订单取消触发退款
- [ ] 记录操作日志与审计日志