# 订单与交易域 - 缺失功能任务

> **限界上下文**: BC4 订单与交易域
> **对应文档**: `04-订单与交易域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

订单域已实现核心功能（下单、支付、发货、确认收货、取消、物流公司、运费模板），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 库存预占 Redis Lua 脚本 | P0 关键 | 下单高并发库存预占需 Lua 原子操作 |
| 订单超时自动取消 | P0 关键 | MQ 延迟消息驱动超时取消 |
| 积分抵现 | P1 重要 | 订单支持积分抵扣金额 |
| 优惠分摊 | P1 重要 | 优惠金额按行分摊到 OrderItem |
| 售后期结束事件 | P1 重要 | OrderAfterSalesWindowClosedEvent 驱动积分发放 |
| 会员订阅订单 | P1 重要 | OrderType 会员订阅订单场景 |
| 物流轨迹查询 | P1 重要 | 对接物流公司 API 查询轨迹 |
| 订单预览 | P2 一般 | 下单前金额预览功能 |
| 买家立即购买 | P2 一般 | 单 SKU 直接下单 |
| 运营强制取消异常订单 | P2 一般 | 运营端强制取消异常订单 |
| 卖家经营概览 | P2 一般 | 卖家仪表盘订单数据 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.Order.Domain.Tests`、`Leno.Order.Application.Tests`、`Leno.Order.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目
2. 覆盖 Order 聚合所有方法（Create、ApplyDiscount、ApplyPointsOffset、MarkAsPaid、Ship、ConfirmReceipt、Cancel、CloseAfterSalesWindow）
3. 覆盖 StockReservation 聚合（ReserveStock、ConfirmStockDeduction、ReleaseStock）
4. 覆盖 LogisticsCompany 和 FreightTemplate 聚合
5. 覆盖应用服务与 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖订单状态机全流转路径
- [ ] 覆盖金额恒等式校验
- [ ] 覆盖库存预占/扣减/释放逻辑
- [ ] 集成测试覆盖下单→支付→发货→收货全流程

### 参考
- `编码规范.md` 第 13 章
- `04-订单与交易域.md` 第 8 章验收标准

---

## Task 2: 库存预占 Redis Lua 脚本

**严重程度**: P0 关键

### 功能描述
实现 Redis Lua 脚本原子完成"判余量 + 扣减 + 记录预占"操作，保证高并发下单不超卖。

### 技术实现路径
1. 在 `Leno.Order.Infrastructure` 中创建 `StockReservationCacheService`
2. 实现 Lua 脚本：
   ```lua
   local stockKey = KEYS[1]
   local reservationKey = KEYS[2]
   local orderId = ARGV[1]
   local quantity = tonumber(ARGV[2])
   local available = tonumber(redis.call('GET', stockKey) or '0')
   if available < quantity then return 0 end
   redis.call('DECRBY', stockKey, quantity)
   redis.call('HSET', reservationKey, orderId, quantity)
   return 1
   ```
3. 预占成功返回 1，失败返回 0（触发出错提示）
4. 下单应用服务中集成预占流程
5. 订单取消/支付成功时释放/确认预占

### 预期完成标准
- [ ] Lua 脚本原子执行判余量 + 扣减 + 记录预占
- [ ] 并发下单不超卖
- [ ] 预占失败返回明确错误
- [ ] 库存预占/释放/确认完整闭环

### 参考
- `编码规范.md` 第 12.5 节
- `04-订单与交易域.md` 第 2.1.4 节 StockReservation

---

## Task 3: 订单超时自动取消

**严重程度**: P0 关键

### 功能描述
订单创建后 30 分钟未支付自动取消，通过 MQ 延迟消息驱动。

### 技术实现路径
1. 订单创建时投递 30 分钟延迟消息到 RabbitMQ
2. 创建 `OrderTimeoutConsumer` 消费延迟消息
3. 消费时检查订单状态：仍为待支付则调用 `Order.Cancel` 取消
4. 取消后发布 `OrderCancelledEvent` 驱动库存释放、优惠券退还、积分释放
5. 已支付/已取消订单忽略延迟消息

### 预期完成标准
- [ ] 订单创建后 30 分钟未支付自动取消
- [ ] 取消后释放预占库存
- [ ] 取消后退还优惠券
- [ ] 取消后释放冻结积分
- [ ] 已支付/已取消订单不重复处理

### 参考
- `04-订单与交易域.md` 第 2.1.1 节 Cancel 方法
- `00-需求文档总览与DDD架构.md` 第 4.7 节

---

## Task 4: 积分抵现

**严重程度**: P1 重要

### 功能描述
订单支持使用积分抵扣部分金额，100 积分 = 1 元，单笔上限见 INV-18。

### 技术实现路径
1. 在 Order 聚合中实现 `ApplyPointsOffset(pointsOffsetAmount)` 方法
2. 校验 `PointsOffsetAmount ≤ ItemsAmount - DiscountAmount`
3. 应用层下单前调用积分域确认接口冻结积分
4. 支付成功时正式扣减积分（通过 `OrderPaidEvent` 驱动）
5. 订单取消时释放冻结积分（通过 `OrderCancelledEvent` 驱动）
6. 更新 `TotalAmount` 计算含积分抵扣

### 预期完成标准
- [ ] 订单支持积分抵扣金额
- [ ] 积分抵扣金额 ≤ 商品金额 - 优惠金额
- [ ] 100 积分 = 1 元
- [ ] 下单时冻结积分，支付成功扣减，取消释放
- [ ] TotalAmount 正确计算

### 参考
- `04-订单与交易域.md` 第 2.1.1 节 ApplyPointsOffset 方法
- `04-订单与交易域.md` 第 2.1.1 节 PointsOffsetAmount 字段

---

## Task 5: 优惠分摊

**严重程度**: P1 重要

### 功能描述
将优惠总额按行分摊到各 OrderItem 的 DiscountAllocation 字段，保证各行分摊之和等于优惠总额。

### 技术实现路径
1. 在 Order 聚合中实现 `ApplyDiscount(discountAllocations)` 方法
2. 校验各行分摊之和等于优惠总额
3. 校验各行分摊不超行小计
4. 更新 `DiscountAmount` 与 `TotalAmount`
5. 下单时调用促销域计算结果后应用分摊

### 预期完成标准
- [ ] 优惠金额按行分摊
- [ ] 各行分摊之和 = 优惠总额
- [ ] 各行分摊 ≤ 行小计
- [ ] TotalAmount 正确计算

### 参考
- `04-订单与交易域.md` 第 2.1.1 节 ApplyDiscount 方法
- `04-订单与交易域.md` 第 2.1.2 节 DiscountAllocation 字段

---

## Task 6: 售后期结束事件

**严重程度**: P1 重要

### 功能描述
实现 `OrderAfterSalesWindowClosedEvent` 集成事件，在售后期结束时发布，驱动积分域发放消费返积分。

### 技术实现路径
1. 在 Order 聚合中实现 `CloseAfterSalesWindow()` 方法
2. 校验 `Status == Completed` 且当前时间 ≥ `AfterSalesWindowEndsAt`
3. 附加 `OrderAfterSalesWindowClosedEvent`（携带 PaidAmount）
4. 订单完成时投递延迟消息（售后期天数后触发）
5. 积分域消费该事件发放消费返积分与成长值

### 预期完成标准
- [ ] 售后期结束时发布 OrderAfterSalesWindowClosedEvent
- [ ] 事件携带 PaidAmount 供积分域计算
- [ ] 仅已完成订单可关闭售后期
- [ ] 消费返积分正确发放

### 参考
- `04-订单与交易域.md` 第 2.1.1 节 CloseAfterSalesWindow 方法
- `00-需求文档总览与DDD架构.md` 第 5 章事件清单

---

## Task 7: 会员订阅订单

**严重程度**: P1 重要

### 功能描述
实现 `OrderType = 会员订阅订单` 场景，支付成功后直接完成（无发货、无售后期）。

### 技术实现路径
1. 在 Order 聚合中实现 `CompleteMembershipOrder()` 方法
2. 校验 `OrderType = 会员订阅订单` 且 `Status = 已支付`
3. 直接流转至已完成
4. 附加 `OrderCompletedEvent` 和 `OrderAfterSalesWindowClosedEvent`
5. `AfterSalesWindowEndsAt = CompletedAt`（无售后期）

### 预期完成标准
- [ ] 会员订阅订单支付后直接完成
- [ ] 无发货流程
- [ ] 无售后期
- [ ] 发布 OrderCompletedEvent 和 OrderAfterSalesWindowClosedEvent

### 参考
- `04-订单与交易域.md` 第 2.1.1 节 CompleteMembershipOrder 方法
- `04-订单与交易域.md` 第 2.1.1 节 OrderType 字段

---

## Task 8: 物流轨迹查询

**严重程度**: P1 重要

### 功能描述
对接物流公司 API 查询物流轨迹，为买家与卖家提供物流状态追踪。

### 技术实现路径
1. 定义 `ILogisticsTrackingService` 接口
2. 实现物流公司 API 适配器（对接快递鸟、菜鸟等）
3. 实现 API：`GET /api/orders/{id}/logistics-trace`
4. 支持 `SupportTracking` 判断是否可查询
5. 物流轨迹缓存到 Redis（短期）

### 预期完成标准
- [ ] 支持查询已发货订单的物流轨迹
- [ ] 仅支持轨迹查询的物流公司可查
- [ ] 物流轨迹缓存优化
- [ ] 不可查询时返回友好提示

### 参考
- `04-订单与交易域.md` 第 2.1.5 节 LogisticsCompany SupportTracking 字段
- `04-订单与交易域.md` 第 1 章上下文概述

---

## Task 9: 运营强制取消异常订单

**严重程度**: P2 一般

### 功能描述
运营端强制取消异常订单（如已支付但无法履约的订单），走退款流程。

### 技术实现路径
1. 在 `OrdersController` 中实现 `POST /api/admin/orders/{id}/force-cancel`（已有端点，需完善）
2. 强制取消已支付订单：触发退款流程
3. 记录操作日志与审计日志
4. 通知买卖双方

### 预期完成标准
- [ ] 运营可强制取消异常订单
- [ ] 已支付订单取消触发退款
- [ ] 记录操作日志与审计日志
- [ ] 通知买卖双方

### 参考
- `04-订单与交易域.md` 第 4 章 F-ORD-023