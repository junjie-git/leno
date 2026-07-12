# 促销域 - 缺失功能任务

> **限界上下文**: BC5 促销域
> **对应文档**: `05-促销域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

促销域已实现核心功能（优惠券、满减、秒杀），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 秒杀 Redis 库存预占 | P0 关键 | 秒杀高并发需 Redis Lua 原子操作 |
| 秒杀异步落单 | P1 重要 | 秒杀成功异步创建订单 |
| 优惠券核销 | P1 重要 | 消费 OrderPaidEvent 核销已使用优惠券 |
| 优惠券退还 | P1 重要 | 消费 OrderCancelledEvent 退还优惠券 |
| 积分兑换优惠券 | P1 重要 | 消费 PointsExchangeCouponRequestedEvent 创建券 |
| 优惠券过期处理 | P1 重要 | 定时任务处理过期优惠券 |
| 满减活动缓存 | P2 一般 | 满减活动 Redis 缓存加速计算 |
| 优惠券冲突检测 | P2 一般 | 下单时检测优惠券互斥规则 |
| 促销活动预热 | P2 一般 | 秒杀活动预热（缓存加载） |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.Promotion.Domain.Tests`、`Leno.Promotion.Application.Tests`、`Leno.Promotion.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目
2. 覆盖 Coupon 聚合（Create、Activate、Claim、Use、Return、Expire）
3. 覆盖 SeckillActivity 聚合（Create、Activate、Close）
4. 覆盖 Promotion 聚合（满减规则）
5. 覆盖优惠计算逻辑
6. 覆盖 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖优惠券生命周期全流程
- [ ] 覆盖秒杀活动状态流转
- [ ] 覆盖满减计算规则

### 参考
- `编码规范.md` 第 13 章
- `05-促销域.md` 第 4 章功能需求

---

## Task 2: 秒杀 Redis 库存预占

**严重程度**: P0 关键

### 功能描述
秒杀活动激活时将库存加载到 Redis，秒杀下单时以 Lua 脚本原子扣减，防止超卖。

### 技术实现路径
1. 活动激活时：从 DB 加载秒杀库存 → 写入 Redis Hash
2. 实现 Lua 脚本原子扣减：
   ```lua
   local stock = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0')
   if stock <= 0 then return 0 end
   redis.call('HINCRBY', KEYS[1], ARGV[1], -1)
   return 1
   ```
3. 扣减成功后发布 `SeckillOrderCreatedEvent` 异步落单
4. 活动结束时回写剩余库存到 DB

### 预期完成标准
- [ ] 秒杀库存 Redis 原子扣减
- [ ] 不超卖
- [ ] 扣减成功异步落单
- [ ] 活动结束库存回写 DB

### 参考
- `05-促销域.md` 第 4 章秒杀功能
- `编码规范.md` 第 12.5 节 Lua 脚本

---

## Task 3: 秒杀异步落单

**严重程度**: P1 重要

### 功能描述
秒杀 Redis 预占成功后，发布 `SeckillOrderCreatedEvent`，订单域消费异步创建订单。

### 技术实现路径
1. Redis 预占成功后发布 `SeckillOrderCreatedEvent`
2. 事件携带：userId、skuId、seckillPrice、quantity、activityId
3. 订单域消费该事件创建订单
4. 消息通知域发送秒杀成功通知
5. 落单失败时回滚 Redis 库存

### 预期完成标准
- [ ] 秒杀成功异步创建订单
- [ ] 落单失败回滚 Redis 库存
- [ ] 发送秒杀成功通知
- [ ] 事件发布与库存扣减原子（发件箱模式）

### 参考
- `05-促销域.md` 第 4 章秒杀功能
- `00-需求文档总览与DDD架构.md` 第 5 章 SeckillOrderCreatedEvent

---

## Task 4: 优惠券核销与退还

**严重程度**: P1 重要

### 功能描述
消费订单域事件，支付成功时核销优惠券，订单取消时退还优惠券。

### 技术实现路径
1. 在基础设施层创建 `OrderEventConsumer` 消费者
2. 消费 `OrderPaidEvent`：调用 `Coupon.Use` 核销券
3. 消费 `OrderCancelledEvent`：调用 `Coupon.Return` 退还券
4. 消费 `RefundCompletedEvent`：退还已核销券
5. 幂等消费以 EventId 去重

### 预期完成标准
- [ ] 支付成功时核销已使用优惠券
- [ ] 订单取消时退还优惠券
- [ ] 退款完成时退还优惠券
- [ ] 事件消费幂等

### 参考
- `05-促销域.md` 第 3 章领域事件
- `00-需求文档总览与DDD架构.md` 第 5 章事件清单

---

## Task 5: 积分兑换优惠券

**严重程度**: P1 重要

### 功能描述
消费 `PointsExchangeCouponRequestedEvent`，校验并创建优惠券实例，发布 `CouponExchangeSucceededEvent`。

### 技术实现路径
1. 在基础设施层创建 `PointsExchangeConsumer` 消费者
2. 消费 `PointsExchangeCouponRequestedEvent`
3. 校验积分兑换券模板存在且有效
4. 创建优惠券实例（关联用户）
5. 发布 `CouponExchangeSucceededEvent` 驱动积分域扣减积分

### 预期完成标准
- [ ] 积分兑换优惠券完整流程
- [ ] 兑换成功后发布 CouponExchangeSucceededEvent
- [ ] 模板不存在或已停用时兑换失败
- [ ] 事件消费幂等

### 参考
- `05-促销域.md` 第 3 章
- `00-需求文档总览与DDD架构.md` 第 5 章 PointsExchangeCouponRequestedEvent

---

## Task 6: 优惠券过期处理

**严重程度**: P1 重要

### 功能描述
定时任务处理过期优惠券，将已领取未使用的过期券标记为已过期。

### 技术实现路径
1. 创建后台服务 `CouponExpiryService`
2. 定时扫描已领取未使用的优惠券
3. 到期时间已过的券调用 `Coupon.Expire` 标记过期
4. 批处理，避免大事务

### 预期完成标准
- [ ] 过期优惠券自动标记为已过期
- [ ] 批处理避免大事务
- [ ] 过期券不可再使用

### 参考
- `05-促销域.md` 第 4 章优惠券过期处理