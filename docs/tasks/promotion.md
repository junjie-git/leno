# 促销域 (Promotion) 开发任务

> **限界上下文**: BC5 促销域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis / RabbitMQ  
> **依赖**: `shared-kernel`  
> **对应文档**: `05-促销域.md`

---

## 模块概述

促销域管理满减活动、优惠券、秒杀活动三类促销能力。满减与优惠券由运营配置，下单时订单域经防腐层查询适用优惠；秒杀活动以 Redis 预扣库存 + 异步创建订单模式支撑高并发。支付成功后消费 `OrderPaidEvent` 核销优惠券，取消时退还。

---

## Task 1: 项目初始化与领域层 — 满减活动聚合

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Leno.Promotion.Domain.csproj`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Aggregates/PromotionActivity.cs`

- [ ] 创建 Leno.Promotion.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `PromotionActivity` 聚合根（ActivityId、Name、Type、Status、StartTime、EndTime、Rules、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `PromotionActivity.Create` 工厂方法（校验时间区间，置待生效态）
- [ ] 实现 `Activate`/`Pause`/`Close` 状态流转方法
- [ ] 实现 `AddRule`/`RemoveRule` 方法（满减规则：门槛金额、减免金额）
- [ ] 定义 `PromotionType` 值对象（FullReduction/Coupon/Seckill）
- [ ] 定义 `PromotionStatus` 值对象（Pending/Active/Paused/Closed）
- [ ] 编写单元测试覆盖状态机
- [ ] 提交：`feat(promotion): add PromotionActivity aggregate`

---

## Task 2: 领域层 — 优惠券聚合

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs`

- [ ] 实现 `Coupon` 聚合根（CouponId、Name、Type、FaceValue、MinSpend、ValidityType、ValidFrom、ValidTo、ValidDays、TotalQty、IssuedQty、Status、Version）
- [ ] 实现 `Coupon.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现 `Coupon.Issue(quantity)`（发放数量校验，已发放不超过总量）
- [ ] 实现 `UserCoupon` 聚合（UserCouponId、UserId、CouponId、Status、Source、ReceivedAt、UsedAt、UsedOrderId、ExpiredAt）
- [ ] 实现 `UserCoupon.Receive`（校验未重复领取、未过期、有剩余量）
- [ ] 实现 `UserCoupon.Lock(orderId)`（下单锁定，待支付期间不可他用）
- [ ] 实现 `UserCoupon.Consume(orderId)`（支付成功核销，已使用态）
- [ ] 实现 `UserCoupon.Release()`（订单取消退还，回到未使用态）
- [ ] 实现 `UserCoupon.Expire()`（过期标记）
- [ ] 定义 `CouponType`（FixedAmount/Percentage/FullReduction）、`CouponStatus`（Unused/Locked/Used/Expired）
- [ ] 编写单元测试覆盖优惠券全生命周期
- [ ] 提交：`feat(promotion): add Coupon and UserCoupon aggregates`

---

## Task 3: 领域层 — 秒杀活动聚合

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs`

- [ ] 实现 `SeckillActivity` 聚合根（ActivityId、SpuId、SkuId、SeckillPrice、OriginalPrice、TotalStock、AvailableStock、LimitPerUser、StartTime、EndTime、Status、Version）
- [ ] 实现 `SeckillActivity.Create` 工厂方法（校验秒杀价 < 原价、库存 > 0、时间合法）
- [ ] 实现 `Activate`/`Close` 状态流转
- [ ] 实现 `DeductStock(userId, quantity)`（校验限购与库存，Redis 预扣）
- [ ] 实现 `RestoreStock(quantity)`（秒杀订单取消回退库存）
- [ ] 定义 `SeckillStatus` 值对象（Pending/Active/Ended/Closed）
- [ ] 编写单元测试
- [ ] 提交：`feat(promotion): add SeckillActivity aggregate`

---

## Task 4: 领域层 — 领域服务与仓储接口

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Services/IPromotionQueryService.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Services/ISeckillStockService.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Repositories/IPromotionActivityRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Repositories/ICouponRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Repositories/IUserCouponRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Repositories/ISeckillActivityRepository.cs`

- [ ] 定义 `IPromotionQueryService` 防腐层接口（GetApplicablePromotionsAsync，供订单域查询满减与优惠券优惠）
- [ ] 定义 `ISeckillStockService` 接口（Redis 预扣库存原子操作）
- [ ] 定义各仓储接口
- [ ] 提交：`feat(promotion): add domain services and repository interfaces`

---

## Task 5: 领域事件定义

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Events/CouponIssuedEvent.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Events/SeckillOrderCreatedEvent.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Domain/Events/SeckillStockSoldOutEvent.cs`

- [ ] 定义 `CouponIssuedEvent`（couponId、userId、receivedAt）
- [ ] 定义 `SeckillOrderCreatedEvent`（activityId、skuId、userId、orderId、seckillPrice）— 消费方：通知域
- [ ] 定义 `SeckillStockSoldOutEvent`（activityId、skuId、soldOutAt）— 售罄通知
- [ ] 提交：`feat(promotion): add domain integration events`

---

## Task 6: 基础设施层 — EF Core 仓储与 Redis 秒杀库存

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/PromotionDbContext.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCorePromotionActivityRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreCouponRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreUserCouponRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreSeckillActivityRepository.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs`

- [ ] 实现 `PromotionDbContext`（各 DbSet 配置）
- [ ] 实现各 EF Core 仓储
- [ ] 实现 `RedisSeckillStockService`（Lua 脚本原子预扣秒杀库存 + 限购校验）
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证 Redis 秒杀原子性
- [ ] 提交：`feat(promotion): add EF Core repositories and Redis seckill stock`

---

## Task 7: 应用层 — 满减与优惠券管理用例

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Application/IPromotionAppService.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Application/ICouponAppService.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Application/Services/PromotionAppService.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs`

- [ ] 实现满减活动 CRUD 用例（运营创建/编辑/启停）
- [ ] 实现优惠券模板 CRUD 与发放用例（运营创建模板、批量发放给用户）
- [ ] 实现用户领券用例（买家领取可用优惠券，校验剩余量与领取限制）
- [ ] 实现用户优惠券查询用例（我的优惠券列表，按状态筛选）
- [ ] 实现 `PromotionQueryAppService`（防腐层实现，供订单域查询适用优惠）
- [ ] 编写单元测试
- [ ] 提交：`feat(promotion): add promotion and coupon application services`

---

## Task 8: 应用层 — 秒杀用例

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Application/ISeckillAppService.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs`

- [ ] 实现秒杀活动管理用例（运营创建/启停秒杀活动，初始化 Redis 库存）
- [ ] 实现秒杀下单用例（校验活动进行中→Redis 预扣库存+限购校验→异步创建订单→发布 `SeckillOrderCreatedEvent`）
- [ ] 实现秒杀活动查询用例（活动列表、详情、库存剩余）
- [ ] 秒杀下单以异步模式处理，前端轮询或 WebSocket 获取结果
- [ ] 编写单元测试覆盖秒杀高并发场景
- [ ] 提交：`feat(promotion): add seckill application service`

---

## Task 9: 基础设施层 — 事件消费者

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/OrderEventConsumer.cs`

- [ ] 实现 `OrderPaidEvent` 消费者（核销优惠券：UserCoupon.Consume）
- [ ] 实现 `OrderCancelledEvent` 消费者（退还优惠券：UserCoupon.Release）
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试
- [ ] 提交：`feat(promotion): add order event consumers for coupon lifecycle`

---

## Task 10: 表现层 — API 控制器

**文件:**
- Create: `src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs`

- [ ] 实现 `PromotionsController`（运营端 CRUD /api/admin/promotions）
- [ ] 实现 `CouponsController`（运营端 CRUD /api/admin/coupons、买家端 GET /api/coupons/available、POST /api/coupons/{id}/receive、GET /api/coupons/mine）
- [ ] 实现 `SeckillController`（GET /api/seckill/activities、GET /api/seckill/activities/{id}、POST /api/seckill/activities/{id}/place）
- [ ] 配置 JWT 鉴权与角色策略
- [ ] 编写 API 集成测试
- [ ] 提交：`feat(promotion): add API controllers`
