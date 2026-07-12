# 积分与会员域 - 任务执行计划

> **模块**: BC7 积分与会员域
> **对应文档**: `07-积分与会员域.md`
> **任务 ID 前缀**: PM
> **总任务数**: 8 | **P0**: 2 | **P1**: 5 | **P2**: 1

---

## 模块概述

积分与会员域负责积分账户管理、会员等级评定与付费会员订阅。已实现核心功能（积分账户、签到、会员等级、付费会员），但缺失消费返积分、评价/新人积分、积分冻结/释放/抵扣/扣回、成长值计算、等级升降级、积分兑换优惠券、积分过期与任务中心。

---

## Task PM-01: 测试项目创建 [P0]

### 子任务 Checklist

- [x] PM-01.1: 创建 `Leno.PointsMembership.Domain.Tests` 项目
- [x] PM-01.2: 创建 `Leno.PointsMembership.Application.Tests` 项目
- [x] PM-01.3: 创建 `Leno.PointsMembership.Api.Tests` 项目
- [x] PM-01.4: 覆盖 PointsAccount 聚合（EarnPoints、ConsumePoints、FreezePoints、ReleasePoints、RevertPoints）
- [x] PM-01.5: 覆盖 MemberLevel 聚合（CalculateLevel、Upgrade、Downgrade）
- [x] PM-01.6: 覆盖 PaidMember 聚合（Subscribe、Renew、Expire）
- [x] PM-01.7: 配置测试覆盖率 ≥ 80%

### 验收标准
- [x] 领域层单元测试覆盖率 ≥ 80%
- [x] 覆盖积分获取/消耗/冻结/释放/扣回全流程
- [x] 覆盖会员等级计算逻辑

---

## Task PM-02: 消费返积分 [P0]

### 子任务 Checklist

- [ ] PM-02.1: 在基础设施层创建 `OrderEventConsumer` 消费者
- [ ] PM-02.2: 消费 `OrderAfterSalesWindowClosedEvent`（携带 PaidAmount）
- [ ] PM-02.3: 按比例计算积分（1 元 = 1 积分）和成长值（1 元 = 1 成长值）
- [ ] PM-02.4: 调用 `PointsAccount.EarnPoints(points, source: "消费返积分", orderId)`
- [ ] PM-02.5: 调用 `MemberLevel.AddGrowthValue(growthValue)`
- [ ] PM-02.6: 发布 `PointsEarnedEvent`（points、source、orderId）
- [ ] PM-02.7: 幂等消费以 EventId 去重
- [ ] PM-02.8: 编写消费返积分端到端测试

### 验收标准
- [ ] 售后期结束后自动发放消费返积分
- [ ] 积分与成长值正确计算
- [ ] 发布 PointsEarnedEvent
- [ ] 事件消费幂等

---

## Task PM-03: 评价返积分与新人积分 [P1]

### 子任务 Checklist

- [ ] PM-03.1: 在基础设施层创建 `ReviewEventConsumer` 消费者
- [ ] PM-03.2: 消费 `ReviewApprovedEvent`：发放评价积分（10 积分/条）
- [ ] PM-03.3: 校验每日评价积分上限（每日最多 5 条评价获积分）
- [ ] PM-03.4: 在基础设施层创建 `UserEventConsumer` 消费者
- [ ] PM-03.5: 消费 `UserRegisteredEvent`：发放新人积分（100 积分）
- [ ] PM-03.6: 幂等消费以 EventId 去重

### 验收标准
- [ ] 评价审核通过后发放评价积分
- [ ] 用户注册后发放新人积分
- [ ] 每日评价积分上限校验

---

## Task PM-04: 积分冻结/释放/抵扣/扣回 [P1]

### 子任务 Checklist

- [ ] PM-04.1: 在 PointsAccount 中实现 `FreezePoints(points, orderId)` 方法
- [ ] PM-04.2: 在 PointsAccount 中实现 `ReleasePoints(points, orderId)` 方法
- [ ] PM-04.3: 在 PointsAccount 中实现 `ConsumePoints(points, orderId)` 方法（正式扣减冻结积分）
- [ ] PM-04.4: 在 PointsAccount 中实现 `RevertPoints(points, reason)` 方法（退款扣回）
- [ ] PM-04.5: 实现内部接口 `POST internal/points/freeze`（已有，需完善）
- [ ] PM-04.6: 消费 `OrderPaidEvent`：将冻结积分转为正式扣减
- [ ] PM-04.7: 消费 `OrderCancelledEvent`：释放冻结积分
- [ ] PM-04.8: 消费 `RefundCompletedEvent`：扣回已发放积分
- [ ] PM-04.9: 积分余额可为负（后续获取优先抵扣）

### 验收标准
- [ ] 下单时冻结积分
- [ ] 支付成功时正式扣减积分
- [ ] 订单取消时释放冻结积分
- [ ] 退款时扣回积分

---

## Task PM-05: 成长值与会员等级 [P1]

### 子任务 Checklist

- [ ] PM-05.1: 实现 `MemberLevel` 聚合（V0-V4 等级，成长值阈值）
- [ ] PM-05.2: 成长值计算规则：消费积分发放时同步增加成长值（已由 PM-02 实现）
- [ ] PM-05.3: 等级评定规则：近 12 个月成长值累计达标
- [ ] PM-05.4: 创建定时任务 `MemberLevelEvaluationJob` 每日评估会员等级
- [ ] PM-05.5: 等级变更时发布 `MemberLevelChangedEvent`（UserId、OldLevel、NewLevel、GrowthValue）
- [ ] PM-05.6: 消息通知域消费该事件发送升降级通知
- [ ] PM-05.7: 等级变更记录历史

### 验收标准
- [ ] 基于近 12 个月成长值评定等级
- [ ] 每日自动评估
- [ ] 等级变更发布 MemberLevelChangedEvent

---

## Task PM-06: 积分兑换优惠券 [P1]

### 子任务 Checklist

- [ ] PM-06.1: 实现 `POST /api/points/exchange-coupon` 端点
- [ ] PM-06.2: 校验积分余额充足（≥ 兑换所需积分）
- [ ] PM-06.3: 发布 `PointsExchangeCouponRequestedEvent`（userId、couponTemplateId、pointsConsumed）
- [ ] PM-06.4: 在基础设施层创建 `CouponExchangeConsumer` 消费者
- [ ] PM-06.5: 消费 `CouponExchangeSucceededEvent`：正式扣减积分
- [ ] PM-06.6: 消费 `CouponExchangeFailedEvent`（如有）：释放积分
- [ ] PM-06.7: 兑换失败时释放积分（超时 30s 未收到成功事件自动释放）

### 验收标准
- [ ] 积分兑换优惠券完整流程
- [ ] 积分不足时拒绝兑换
- [ ] 兑换成功后正式扣减积分

---

## Task PM-07: 积分过期处理 [P1]

### 子任务 Checklist

- [ ] PM-07.1: 创建后台服务 `PointsExpiryService`（`BackgroundService`）
- [ ] PM-07.2: 定时扫描积分流水中的过期积分（按先进先出原则）
- [ ] PM-07.3: 调用 `PointsAccount.ExpirePoints(points)` 标记过期
- [ ] PM-07.4: 发布 `PointsExpiredEvent`（userId、points、expiredAt）
- [ ] PM-07.5: 批处理每批 500 条，避免大事务
- [ ] PM-07.6: 扫描频率：每日一次

### 验收标准
- [ ] 过期积分自动标记
- [ ] 先进先出原则
- [ ] 批处理性能可接受

---

## Task PM-08: 任务中心 [P2]

### 子任务 Checklist

- [ ] PM-08.1: 创建 `Task` 聚合（TaskType、Name、Description、RewardPoints、CompletionCondition）
- [ ] PM-08.2: 创建 `UserTask` 实体（UserId、TaskId、Status、CompletedAt）
- [ ] PM-08.3: 任务类型：每日签到（已实现）、完善资料（50 积分）、首次下单（200 积分）、分享商品（5 积分/天）
- [ ] PM-08.4: 实现 `GET /api/points/tasks` - 任务列表（含完成状态）
- [ ] PM-08.5: 实现 `POST /api/points/tasks/{taskId}/complete` - 完成任务获取积分
- [ ] PM-08.6: 每日任务重置（北京时间 0 点）
- [ ] PM-08.7: 一次性任务不可重复完成

### 验收标准
- [ ] 任务列表查询
- [ ] 任务完成获取积分
- [ ] 每日任务重置
- [ ] 一次性任务不可重复完成