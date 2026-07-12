# 评价与售后域 - 任务执行计划

> **模块**: BC8 评价与售后域
> **对应文档**: `06-评价与售后域.md`
> **任务 ID 前缀**: RAS
> **总任务数**: 6 | **P0**: 1 | **P1**: 4 | **P2**: 1

---

## 模块概述

评价与售后域负责商品评价管理与退货退款售后处理。已实现核心功能（评价提交、售后申请），但缺失评价审核、卖家回复、售后状态机、退款请求发起、评价评分回写商品域与图片上传。

---

## Task RAS-01: 测试项目创建 [P0]

### 子任务 Checklist

- [ ] RAS-01.1: 创建 `Leno.ReviewAfterSales.Domain.Tests` 项目
- [ ] RAS-01.2: 创建 `Leno.ReviewAfterSales.Application.Tests` 项目
- [ ] RAS-01.3: 创建 `Leno.ReviewAfterSales.Api.Tests` 项目
- [ ] RAS-01.4: 覆盖 Review 聚合（Submit、Approve、Hide、Reply）
- [ ] RAS-01.5: 覆盖 AfterSales 聚合（Submit、Cancel、Approve、Reject、ReturnGoods、ConfirmReturn、CompleteRefund）
- [ ] RAS-01.6: 配置测试覆盖率 ≥ 80%

### 验收标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖评价生命周期
- [ ] 覆盖售后状态机全流转

---

## Task RAS-02: 评价审核与回复 [P1]

### 子任务 Checklist

- [ ] RAS-02.1: 在 Review 聚合中实现 `Approve(operatorId)` 方法
- [ ] RAS-02.2: 在 Review 聚合中实现 `Hide(operatorId, reason)` 方法
- [ ] RAS-02.3: 在 Review 聚合中实现 `Reply(sellerId, replyContent)` 方法
- [ ] RAS-02.4: `Approve` 时发布 `ReviewApprovedEvent`（驱动积分发放）
- [ ] RAS-02.5: `Hide` 时发布 `ReviewHiddenEvent`（驱动商品评分更新）
- [ ] RAS-02.6: 实现 `POST /api/admin/reviews/{id}/approve` 端点
- [ ] RAS-02.7: 实现 `POST /api/admin/reviews/{id}/hide` 端点
- [ ] RAS-02.8: 实现 `POST /api/seller/reviews/{id}/reply` 端点
- [ ] RAS-02.9: 回复内容长度限制（≤ 500 字）

### 验收标准
- [ ] 运营可审核通过/隐藏评价
- [ ] 审核通过发布 ReviewApprovedEvent
- [ ] 隐藏评价发布 ReviewHiddenEvent
- [ ] 卖家可回复评价

---

## Task RAS-03: 售后状态机与审核 [P1]

### 子任务 Checklist

- [ ] RAS-03.1: 在 AfterSales 聚合中实现完整状态机：
  - `Submit` → 待审核（PendingReview）
  - `Cancel` → 已取消（Cancelled）
  - `Approve` → 已通过（Approved）
  - `Reject(reason)` → 已拒绝（Rejected）
  - `ReturnGoods` → 退货中（Returning）
  - `ConfirmReturn` → 退款中（Refunding）
  - `CompleteRefund` → 已完成（Completed）
- [ ] RAS-03.2: 状态流转校验（不可跳转/回退，仅允许合法转换）
- [ ] RAS-03.3: 实现 `POST /api/seller/after-sales/{id}/approve` 端点
- [ ] RAS-03.4: 实现 `POST /api/seller/after-sales/{id}/reject` 端点
- [ ] RAS-03.5: 实现 `POST /api/seller/after-sales/{id}/confirm-return` 端点
- [ ] RAS-03.6: 实现 `POST /api/admin/after-sales/{id}/approve` 端点（运营审核）
- [ ] RAS-03.7: 实现 `POST /api/admin/after-sales/{id}/reject` 端点（运营审核）
- [ ] RAS-03.8: 每个状态变更发布对应事件

### 验收标准
- [ ] 售后状态机完整流转
- [ ] 卖家/运营可审核售后申请
- [ ] 状态流转校验（不可跳转/回退）

---

## Task RAS-04: 退款请求发起 [P1]

### 子任务 Checklist

- [ ] RAS-04.1: 售后审核通过后，发布 `RefundRequestedIntegrationEvent`
- [ ] RAS-04.2: 事件携带：PaymentId、RefundAmount、RefundReason、AfterSalesId
- [ ] RAS-04.3: 支付集成域消费该事件执行退款
- [ ] RAS-04.4: 在基础设施层创建 `RefundEventConsumer` 消费者
- [ ] RAS-04.5: 消费 `RefundSucceededIntegrationEvent`：流转售后单至退款完成
- [ ] RAS-04.6: 消费 `RefundFailedIntegrationEvent`：记录失败原因
- [ ] RAS-04.7: 发布 `RefundCompletedEvent` 驱动订单域回滚、积分扣回

### 验收标准
- [ ] 售后审核通过后发起退款
- [ ] 退款成功流转售后单状态
- [ ] 发布 RefundCompletedEvent

---

## Task RAS-05: 评价评分回写商品域 [P1]

### 子任务 Checklist

- [ ] RAS-05.1: 评价提交时发布 `ReviewSubmittedEvent`（携带 productId、newScore、reviewCount）
- [ ] RAS-05.2: 评价隐藏时发布 `ReviewHiddenEvent`（携带 productId）
- [ ] RAS-05.3: 在基础设施层创建 `ReviewEventPublisher` 发布集成事件
- [ ] RAS-05.4: 商品域消费 `ReviewSubmittedEvent` 更新 `Product.Score` 字段
- [ ] RAS-05.5: 商品域消费 `ReviewHiddenEvent` 重新计算评分
- [ ] RAS-05.6: 评分计算正确（加权平均，考虑隐藏评价）

### 验收标准
- [ ] 评价提交后商品评分更新
- [ ] 评价隐藏后商品评分重新计算
- [ ] 评分计算正确

---

## Task RAS-06: 售后凭证与评价图片上传 [P2]

### 子任务 Checklist

- [ ] RAS-06.1: 售后申请支持上传凭证图片（通过 `IFileStorageService`）
- [ ] RAS-06.2: 评价支持上传图片
- [ ] RAS-06.3: 图片数量限制（凭证 ≤ 5 张，评价 ≤ 9 张）
- [ ] RAS-06.4: 图片大小限制（单张 ≤ 5MB）
- [ ] RAS-06.5: 图片格式限制（JPG/PNG/WebP）
- [ ] RAS-06.6: 图片存储通过 `IFileStorageService` 抽象（不直接依赖具体实现）

### 验收标准
- [ ] 售后申请支持上传凭证图片
- [ ] 评价支持上传图片
- [ ] 图片数量与大小限制