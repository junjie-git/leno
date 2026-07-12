# 评价与售后域 - 缺失功能任务

> **限界上下文**: BC6 评价与售后域
> **对应文档**: `06-评价与售后域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

评价与售后域已实现核心功能（评价提交、售后申请），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 无任何测试项目 |
| 评价审核 | P1 重要 | 运营审核评价（通过/隐藏） |
| 评价回复 | P1 重要 | 卖家回复买家评价 |
| 售后审核 | P1 重要 | 卖家/运营审核售后申请 |
| 售后状态机 | P1 重要 | 退货退款完整状态流转 |
| 退款请求发起 | P1 重要 | 售后审核通过后发起退款 |
| 评价评分回写商品域 | P1 重要 | ReviewSubmittedEvent 驱动商品评分更新 |
| 评价隐藏联动 | P1 重要 | ReviewHiddenEvent 驱动商品评分更新 |
| 售后凭证上传 | P2 一般 | 售后申请上传凭证图片 |
| 评价图片上传 | P2 一般 | 评价附带图片 |
| 评价追评 | P2 一般 | 用户追加评价 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.ReviewAfterSales.Domain.Tests`、`Leno.ReviewAfterSales.Application.Tests`、`Leno.ReviewAfterSales.Api.Tests` 测试项目。

### 技术实现路径
1. 创建测试项目
2. 覆盖 Review 聚合（Submit、Approve、Hide、Reply）
3. 覆盖 AfterSales 聚合（Submit、Cancel、Approve、Reject、ReturnGoods、Complete）
4. 覆盖应用服务与 API 控制器

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 覆盖评价生命周期
- [ ] 覆盖售后状态机全流转
- [ ] 覆盖退款请求发起

### 参考
- `编码规范.md` 第 13 章
- `06-评价与售后域.md` 第 8 章验收标准

---

## Task 2: 评价审核与回复

**严重程度**: P1 重要

### 功能描述
实现运营审核评价（通过/隐藏）与卖家回复评价功能。

### 技术实现路径
1. 在 Review 聚合中实现 `Approve`、`Hide(reason)` 方法
2. `Approve` 时发布 `ReviewApprovedEvent`（驱动积分发放）
3. `Hide` 时发布 `ReviewHiddenEvent`（驱动商品评分更新）
4. 实现卖家回复功能：`Reply(replyContent)`
5. 实现 API：
   - `POST /api/admin/reviews/{id}/approve` - 审核通过
   - `POST /api/admin/reviews/{id}/hide` - 隐藏评价
   - `POST /api/seller/reviews/{id}/reply` - 卖家回复

### 预期完成标准
- [ ] 运营可审核通过/隐藏评价
- [ ] 审核通过发布 ReviewApprovedEvent
- [ ] 隐藏评价发布 ReviewHiddenEvent
- [ ] 卖家可回复评价
- [ ] 回复内容长度限制

### 参考
- `06-评价与售后域.md` 第 4 章评价审核
- `00-需求文档总览与DDD架构.md` 第 5 章 ReviewApprovedEvent、ReviewHiddenEvent

---

## Task 3: 售后状态机与审核

**严重程度**: P1 重要

### 功能描述
实现售后单完整状态机：待审核→已通过→退货中→退款完成/已拒绝/已取消。

### 技术实现路径
1. 在 AfterSales 聚合中实现完整状态机：
   - `Submit` - 提交申请（待审核）
   - `Cancel` - 买家取消（已取消）
   - `Approve` - 卖家/运营审核通过（已通过）
   - `Reject(reason)` - 拒绝（已拒绝）
   - `ReturnGoods` - 买家退货（退货中）
   - `ConfirmReturn` - 卖家确认收货（退款中）
   - `CompleteRefund` - 退款完成（已完成）
2. 实现 API：
   - `POST /api/seller/after-sales/{id}/approve` - 卖家审核通过
   - `POST /api/seller/after-sales/{id}/reject` - 卖家拒绝
   - `POST /api/seller/after-sales/{id}/confirm-return` - 卖家确认收货
   - `POST /api/admin/after-sales/{id}/approve` - 运营审核
   - `POST /api/admin/after-sales/{id}/reject` - 运营拒绝

### 预期完成标准
- [ ] 售后状态机完整流转
- [ ] 卖家可审核售后申请
- [ ] 运营可审核售后申请
- [ ] 状态流转校验（不可跳转/回退）
- [ ] 每个状态变更发布对应事件

### 参考
- `06-评价与售后域.md` 第 4 章售后功能
- `06-评价与售后域.md` 第 7 章状态机

---

## Task 4: 退款请求发起

**严重程度**: P1 重要

### 功能描述
售后审核通过后，发布 `RefundRequestedIntegrationEvent` 请求支付集成域执行退款。

### 技术实现路径
1. 售后审核通过后，发布 `RefundRequestedIntegrationEvent`
2. 事件携带：paymentId、refundAmount、refundReason、afterSalesId
3. 支付集成域消费该事件执行退款
4. 消费 `RefundSucceededIntegrationEvent`：流转售后单至退款完成
5. 发布 `RefundCompletedEvent` 驱动订单域回滚、积分扣回

### 预期完成标准
- [ ] 售后审核通过后发起退款
- [ ] 退款成功流转售后单状态
- [ ] 发布 RefundCompletedEvent
- [ ] 退款失败记录原因

### 参考
- `06-评价与售后域.md` 第 4 章退款功能
- `00-需求文档总览与DDD架构.md` 第 5 章 RefundRequestedIntegrationEvent、RefundCompletedEvent

---

## Task 5: 评价评分回写商品域

**严重程度**: P1 重要

### 功能描述
评价提交/审核通过/隐藏时，发布事件驱动商品域更新评分摘要。

### 技术实现路径
1. 评价提交时发布 `ReviewSubmittedEvent`（携带 productId、newScore、reviewCount）
2. 评价隐藏时发布 `ReviewHiddenEvent`（携带 productId）
3. 商品域消费这些事件更新 `Product.Score` 字段
4. 商品域重新计算评分摘要

### 预期完成标准
- [ ] 评价提交后商品评分更新
- [ ] 评价隐藏后商品评分重新计算
- [ ] 评分计算正确（加权平均）

### 参考
- `06-评价与售后域.md` 第 3 章领域事件
- `00-需求文档总览与DDD架构.md` 第 5 章 ReviewSubmittedEvent、ReviewHiddenEvent

---

## Task 6: 售后凭证与评价图片上传

**严重程度**: P2 一般

### 功能描述
支持售后申请上传凭证图片，评价附带图片上传。

### 技术实现路径
1. 售后申请支持上传凭证图片（通过 `IFileStorageService`）
2. 评价支持上传图片
3. 图片数量限制（凭证 ≤ 5 张，评价 ≤ 9 张）
4. 图片大小限制（单张 ≤ 5MB）

### 预期完成标准
- [ ] 售后申请支持上传凭证图片
- [ ] 评价支持上传图片
- [ ] 图片数量与大小限制
- [ ] 图片存储通过 IFileStorageService 抽象

### 参考
- `06-评价与售后域.md` 第 4 章
- `00-需求文档总览与DDD架构.md` 第 4.9 节