# 积分与会员域 - 缺失功能任务

> **限界上下文**: BC7 积分与会员域
> **对应文档**: `07-积分与会员域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

积分与会员域已实现核心功能（积分账户、签到、会员等级、付费会员），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 消费返积分 | P0 关键 | 消费 OrderAfterSalesWindowClosedEvent 发放积分 |
| 评价返积分 | P1 重要 | 消费 ReviewApprovedEvent 发放评价积分 |
| 新人积分发放 | P1 重要 | 消费 UserRegisteredEvent 发放新人积分 |
| 积分冻结/释放 | P1 重要 | 下单冻结积分、取消释放积分 |
| 积分抵扣扣减 | P1 重要 | 支付成功正式扣减积分 |
| 退款扣回积分 | P1 重要 | 消费 RefundCompletedEvent 扣回积分 |
| 成长值计算 | P1 重要 | 基于近 12 个月成长值计算会员等级 |
| 会员等级升降级 | P1 重要 | 定时任务评估会员等级，发布 MemberLevelChangedEvent |
| 积分兑换优惠券 | P1 重要 | 积分兑换优惠券完整流程 |
| 积分过期处理 | P1 重要 | 定时任务处理过期积分 |
| 任务中心 | P2 一般 | 用户完成任务获取积分 |
| 积分流水查询 | P2 一般 | 积分收支明细查询 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.PointsMembership.Domain.Tests`、`Leno.PointsMembership.Application.Tests`、`Leno.PointsMembership.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目
2. 覆盖 PointsAccount 聚合（EarnPoints、ConsumePoints、FreezePoints、ReleasePoints、RevertPoints）
3. 覆盖 MemberLevel 聚合（CalculateLevel、Upgrade、Downgrade）
4. 覆盖 PaidMember 聚合（Subscribe、Renew、Expire）
5. 覆盖应用服务与 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖积分获取/消耗/冻结/释放/扣回全流程
- [ ] 覆盖会员等级计算逻辑
- [ ] 覆盖付费会员订阅/续费/过期

### 参考
- `编码规范.md` 第 13 章
- `07-积分与会员域.md` 第 8 章验收标准

---

## Task 2: 消费返积分

**严重程度**: P0 关键

### 功能描述
消费 `OrderAfterSalesWindowClosedEvent`，根据订单实付金额发放消费返积分与成长值。

### 技术实现路径
1. 在基础设施层创建 `OrderEventConsumer` 消费者
2. 消费 `OrderAfterSalesWindowClosedEvent`（携带 PaidAmount）
3. 按比例计算积分（如 1 元 = 1 积分）和成长值
4. 调用 `PointsAccount.EarnPoints` 发放积分
5. 调用 `MemberLevel.AddGrowthValue` 增加成长值
6. 幂等消费以 EventId 去重

### 预期完成标准
- [ ] 售后期结束后自动发放消费返积分
- [ ] 积分与成长值正确计算
- [ ] 发布 PointsEarnedEvent
- [ ] 事件消费幂等

### 参考
- `07-积分与会员域.md` 第 4 章积分获取规则
- `00-需求文档总览与DDD架构.md` 第 5 章 OrderAfterSalesWindowClosedEvent

---

## Task 3: 评价返积分与新人积分

**严重程度**: P1 重要

### 功能描述
消费 `ReviewApprovedEvent` 发放评价积分，消费 `UserRegisteredEvent` 发放新人积分。

### 技术实现路径
1. 消费 `ReviewApprovedEvent`：发放评价积分（如 10 积分/条）
2. 消费 `UserRegisteredEvent`：发放新人积分（如 100 积分）
3. 校验每日评价积分上限
4. 幂等消费以 EventId 去重

### 预期完成标准
- [ ] 评价审核通过后发放评价积分
- [ ] 用户注册后发放新人积分
- [ ] 每日评价积分上限校验
- [ ] 事件消费幂等

### 参考
- `07-积分与会员域.md` 第 4 章
- `00-需求文档总览与DDD架构.md` 第 5 章 ReviewApprovedEvent、UserRegisteredEvent

---

## Task 4: 积分冻结/释放/抵扣/扣回

**严重程度**: P1 重要

### 功能描述
实现积分在下单→支付→取消→退款全链路中的冻结/释放/抵扣/扣回操作。

### 技术实现路径
1. 实现内部接口：
   - `POST internal/points/trial-offset` - 试算积分抵扣金额（已有）
   - `POST internal/points/freeze` - 冻结积分（已有）
   - `POST internal/points/release` - 释放冻结积分（已有）
2. 消费 `OrderPaidEvent`：将冻结积分转为正式扣减
3. 消费 `OrderCancelledEvent`：释放冻结积分
4. 消费 `RefundCompletedEvent`：扣回已发放积分
5. 积分账户可为负，后续获取优先抵扣

### 预期完成标准
- [ ] 下单时冻结积分
- [ ] 支付成功时正式扣减积分
- [ ] 订单取消时释放冻结积分
- [ ] 退款时扣回积分
- [ ] 积分余额可为负

### 参考
- `07-积分与会员域.md` 第 4 章
- `00-需求文档总览与DDD架构.md` 第 5 章事件清单

---

## Task 5: 成长值与会员等级

**严重程度**: P1 重要

### 功能描述
实现基于近 12 个月成长值的会员等级评定与自动升降级。

### 技术实现路径
1. 实现 `MemberLevel` 聚合（V0-V4 等级）
2. 成长值计算：消费积分发放时同步增加成长值
3. 等级评定规则：近 12 个月成长值累计达标
4. 定时任务每日评估会员等级
5. 等级变更时发布 `MemberLevelChangedEvent`
6. 消费该事件发送升降级通知

### 预期完成标准
- [ ] 基于近 12 个月成长值评定等级
- [ ] 每日自动评估
- [ ] 等级变更发布 MemberLevelChangedEvent
- [ ] 升降级通知发送

### 参考
- `07-积分与会员域.md` 第 4 章会员等级
- `00-需求文档总览与DDD架构.md` 统一语言术语表

---

## Task 6: 积分兑换优惠券

**严重程度**: P1 重要

### 功能描述
实现用户以积分兑换优惠券的完整流程。

### 技术实现路径
1. 实现 API：`POST /api/points/exchange-coupon`
2. 校验积分余额充足
3. 发布 `PointsExchangeCouponRequestedEvent`
4. 消费 `CouponExchangeSucceededEvent`：正式扣减积分
5. 消费失败时释放积分

### 预期完成标准
- [ ] 积分兑换优惠券完整流程
- [ ] 积分不足时拒绝兑换
- [ ] 兑换成功后正式扣减积分
- [ ] 兑换失败时释放积分

### 参考
- `07-积分与会员域.md` 第 4 章
- `00-需求文档总览与DDD架构.md` 第 5 章 PointsExchangeCouponRequestedEvent

---

## Task 7: 积分过期处理

**严重程度**: P1 重要

### 功能描述
定时任务处理过期积分，按先进先出原则标记过期。

### 技术实现路径
1. 创建后台服务 `PointsExpiryService`
2. 定时扫描积分流水中的过期积分
3. 按先进先出原则标记过期
4. 发布 `PointsExpiredEvent`
5. 批处理避免大事务

### 预期完成标准
- [ ] 过期积分自动标记
- [ ] 先进先出原则
- [ ] 批处理性能可接受
- [ ] 发布 PointsExpiredEvent

### 参考
- `07-积分与会员域.md` 第 4 章积分过期

---

## Task 8: 任务中心

**严重程度**: P2 一般

### 功能描述
实现用户任务中心，用户完成任务获取积分奖励。

### 技术实现路径
1. 创建 `Task` 聚合（任务类型、奖励积分、完成条件）
2. 实现 `TaskCenter` 领域服务
3. 实现 API：
   - `GET /api/points/tasks` - 任务列表
   - `POST /api/points/tasks/{taskId}/complete` - 完成任务
4. 任务类型：每日签到（已实现）、完善资料、首次下单、分享商品等

### 预期完成标准
- [ ] 任务列表查询
- [ ] 任务完成获取积分
- [ ] 每日任务重置
- [ ] 一次性任务不可重复完成

### 参考
- `07-积分与会员域.md` 第 4 章任务中心