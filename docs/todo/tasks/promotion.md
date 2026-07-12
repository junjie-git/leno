# 促销域 - 任务执行计划

> **模块**: BC5 促销域
> **对应文档**: `05-促销域.md`
> **任务 ID 前缀**: PROMO
> **总任务数**: 6 | **P0**: 2 | **P1**: 4 | **P2**: 0

---

## 模块概述

促销域负责优惠券、满减活动与秒杀活动的管理。已实现核心功能（优惠券、满减、秒杀），但缺失秒杀 Redis 库存预占、秒杀异步落单、优惠券核销/退还、积分兑换优惠券与优惠券过期处理。

---

## Task PROMO-01: 测试项目创建 [P0]

### 子任务 Checklist

- [x] PROMO-01.1: 创建 `Leno.Promotion.Domain.Tests` 项目
- [x] PROMO-01.2: 创建 `Leno.Promotion.Application.Tests` 项目
- [x] PROMO-01.3: 创建 `Leno.Promotion.Api.Tests` 项目
- [x] PROMO-01.4: 覆盖 Coupon 聚合（Create、Enable、Disable、Issue、IsReceivable）
- [x] PROMO-01.5: 覆盖 SeckillActivity 聚合（Create、Activate、Close、DeductStock、RestoreStock）
- [x] PROMO-01.6: 覆盖 Promotion 聚合（满减规则计算、AddRule、RemoveRule、状态机）
- [x] PROMO-01.7: 配置测试覆盖率 ≥ 80%

### 验收标准
- [x] 领域层单元测试覆盖率 ≥ 80%
- [x] 覆盖优惠券生命周期全流程
- [x] 覆盖秒杀活动状态流转

### 测试统计
- 领域层测试: 54 项 (SeckillActivity 15 + Coupon 14 + UserCoupon 10 + PromotionActivity 15)
- 应用层测试: 19 项 (PromotionAppService 4 + CouponAppService 5 + SeckillAppService 10)
- API 层测试: 25 项 (PromotionsController 7 + CouponsController 9 + SeckillController 7 + Health 2)
- 总计: 98 项测试

---

## Task PROMO-02: 秒杀 Redis 库存预占 [P0]

### 子任务 Checklist

- [ ] PROMO-02.1: 活动激活时，从 DB 加载秒杀库存 → 写入 Redis Hash（`seckill:{activityId}:stock`，field=skuId）
- [ ] PROMO-02.2: 实现 Lua 脚本：`HGET stock` → 判库存 > 0 → `HINCRBY -1` → 返回 1
- [ ] PROMO-02.3: 库存为 0 时返回 0（已售罄）
- [ ] PROMO-02.4: 扣减成功后发布 `SeckillOrderCreatedEvent`（userId、skuId、seckillPrice、quantity、activityId）
- [ ] PROMO-02.5: 实现 `POST /api/seckill/{activityId}/order` 秒杀下单端点
- [ ] PROMO-02.6: 活动结束时回写剩余库存到 DB
- [ ] PROMO-02.7: 编写秒杀并发压测（模拟 10000 并发）

### 验收标准
- [ ] 秒杀库存 Redis 原子扣减
- [ ] 不超卖
- [ ] 扣减成功异步落单
- [ ] 活动结束库存回写 DB

---

## Task PROMO-03: 秒杀异步落单 [P1]

### 子任务 Checklist

- [ ] PROMO-03.1: Redis 预占成功后发布 `SeckillOrderCreatedEvent`（经发件箱模式）
- [ ] PROMO-03.2: 事件携带：UserId、SkuId、SeckillPrice、Quantity、ActivityId、OrderId
- [ ] PROMO-03.3: 订单域消费该事件创建订单（以秒杀价固化）
- [ ] PROMO-03.4: 消息通知域发送秒杀成功通知
- [ ] PROMO-03.5: 落单失败时回滚 Redis 库存（`HINCRBY +1`）
- [ ] PROMO-03.6: 实现落单失败补偿机制（定时任务扫描未落单的预占记录）

### 验收标准
- [ ] 秒杀成功异步创建订单
- [ ] 落单失败回滚 Redis 库存
- [ ] 事件发布与库存扣减原子（发件箱模式）

---

## Task PROMO-04: 优惠券核销与退还 [P1]

### 子任务 Checklist

- [ ] PROMO-04.1: 在基础设施层创建 `OrderEventConsumer` 消费者
- [ ] PROMO-04.2: 消费 `OrderPaidEvent`：调用 `Coupon.Use` 核销券
- [ ] PROMO-04.3: 消费 `OrderCancelledEvent`：调用 `Coupon.Return` 退还券
- [ ] PROMO-04.4: 消费 `RefundCompletedEvent`：退还已核销券（恢复可使用状态）
- [ ] PROMO-04.5: 幂等消费以 EventId 去重
- [ ] PROMO-04.6: 退还券有效期不变（不延长）

### 验收标准
- [ ] 支付成功时核销已使用优惠券
- [ ] 订单取消时退还优惠券
- [ ] 退款完成时退还优惠券
- [ ] 事件消费幂等

---

## Task PROMO-05: 积分兑换优惠券 [P1]

### 子任务 Checklist

- [ ] PROMO-05.1: 在基础设施层创建 `PointsExchangeConsumer` 消费者
- [ ] PROMO-05.2: 消费 `PointsExchangeCouponRequestedEvent`（携带 userId、couponTemplateId）
- [ ] PROMO-05.3: 校验积分兑换券模板存在且有效（Enabled + 未过期）
- [ ] PROMO-05.4: 创建优惠券实例（关联用户，设置有效期）
- [ ] PROMO-05.5: 发布 `CouponExchangeSucceededEvent`（携带 couponId、userId、pointsConsumed）
- [ ] PROMO-05.6: 模板不存在或已停用时兑换失败

### 验收标准
- [ ] 积分兑换优惠券完整流程
- [ ] 兑换成功后发布 CouponExchangeSucceededEvent
- [ ] 模板不存在或已停用时兑换失败

---

## Task PROMO-06: 优惠券过期处理 [P1]

### 子任务 Checklist

- [ ] PROMO-06.1: 创建后台服务 `CouponExpiryService`（`BackgroundService`）
- [ ] PROMO-06.2: 定时扫描已领取未使用的优惠券（`Status = Claimed` 且 `ExpireAt < now`）
- [ ] PROMO-06.3: 批量调用 `Coupon.Expire` 标记过期
- [ ] PROMO-06.4: 批处理每批 500 条，避免大事务
- [ ] PROMO-06.5: 过期券不可再使用（Use 方法校验 ExpireAt）
- [ ] PROMO-06.6: 扫描频率：每小时一次

### 验收标准
- [ ] 过期优惠券自动标记为已过期
- [ ] 批处理避免大事务
- [ ] 过期券不可再使用