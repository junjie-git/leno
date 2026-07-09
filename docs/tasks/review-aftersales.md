# 评价与售后域 (Review & After-Sales) 开发任务

> **限界上下文**: BC6 评价与售后域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Elasticsearch  
> **依赖**: `shared-kernel`、`order`（订单完成事件）、`payment`（退款事件）  
> **对应文档**: `06-评价与售后域.md`

---

## 模块概述

评价与售后域管理商品评价与售后服务。评价在订单完成后开放，买家提交评价、卖家回复、运营审核。售后服务承接退款退货申请，经审核通过后经支付集成域执行退款，退款完成后回滚订单销量与库存。售后单状态机驱动退款全流程。

---

## Task 1: 项目初始化与领域层 — Review 聚合

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Leno.ReviewAfterSales.Domain.csproj`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs`

- [ ] 创建 Leno.ReviewAfterSales.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `Review` 聚合根（ReviewId、OrderId、OrderItemId、UserId、SpuId、SkuId、Rating、Content、Images、Status、SellerReply、SellerRepliedAt、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `Review.Create` 工厂方法（校验订单已完成、未重复评价、评分 1-5，附加 `ReviewSubmittedEvent`）
- [ ] 实现 `Review.SellerReply(content)`（卖家回复，仅一次）
- [ ] 实现 `Review.Approve`/`Review.Hide`（运营审核/隐藏，附加 `ReviewModeratedEvent`）
- [ ] 定义 `ReviewStatus` 值对象（Pending/Approved/Hidden）
- [ ] 编写单元测试覆盖评价生命周期
- [ ] 提交：`feat(review): add Review aggregate root`

---

## Task 2: 领域层 — AfterSales 聚合

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs`

- [ ] 实现 `AfterSales` 聚合根（AfterSalesId、OrderId、UserId、SellerId、Type、Reason、Description、Images、RefundAmount、Status、ReviewedBy、ReviewedAt、RejectReason、RefundOrderId、RefundedAt、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `AfterSales.Create` 工厂方法（校验订单已支付、售后期内、退款金额合理，置待审核态，附加 `AfterSalesSubmittedEvent`）
- [ ] 实现 `AfterSales.Approve(reviewedBy)`（待审核→审核通过，附加 `AfterSalesApprovedEvent` + `RefundRequestedIntegrationEvent`）
- [ ] 实现 `AfterSales.Reject(reviewedBy, reason)`（待审核→已驳回）
- [ ] 实现 `AfterSales.MarkRefundCompleted(refundOrderId, refundedAt)`（退款完成态，附加 `RefundCompletedEvent`）
- [ ] 实现 `AfterSales.MarkRefundFailed(reason)`（退款失败态）
- [ ] 实现 `AfterSales.Cancel()`（买家撤销申请，仅待审核态）
- [ ] 定义 `AfterSalesType`（ReturnRefund/RefundOnly）、`AfterSalesStatus`（Pending/Approved/Rejected/Refunding/Completed/Failed/Cancelled）
- [ ] 编写单元测试覆盖售后状态机
- [ ] 提交：`feat(review): add AfterSales aggregate root with state machine`

---

## Task 3: 领域层 — 领域服务与仓储接口

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IReviewEligibilityChecker.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IAfterSalesEligibilityChecker.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Repositories/IReviewRepository.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Repositories/IAfterSalesRepository.cs`

- [ ] 定义 `IReviewEligibilityChecker`（校验订单完成且未评价）
- [ ] 定义 `IAfterSalesEligibilityChecker`（校验售后期内、退款金额上限）
- [ ] 定义各仓储接口（含分页查询、按 SPU/订单/用户过滤）
- [ ] 提交：`feat(review): add domain services and repository interfaces`

---

## Task 4: 领域事件定义

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/ReviewSubmittedEvent.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/ReviewModeratedEvent.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/AfterSalesSubmittedEvent.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/AfterSalesApprovedEvent.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/RefundCompletedEvent.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Events/RefundRequestedIntegrationEvent.cs`

- [ ] 定义 `ReviewSubmittedEvent`（reviewId、userId、spuId、rating）— 消费方：商品域（更新评分）、ES
- [ ] 定义 `AfterSalesApprovedEvent`（afterSalesId、userId、type）— 消费方：通知域、系统管理域
- [ ] 定义 `RefundCompletedEvent`（afterSalesId、orderId、refundedAmount）— 消费方：订单域（回滚销量库存）、通知域、系统管理域
- [ ] 定义 `RefundRequestedIntegrationEvent`（afterSalesId、orderId、paymentOrderId、refundAmount、reason）— 消费方：支付集成域
- [ ] 提交：`feat(review): add domain integration events`

---

## Task 5: 基础设施层 — EF Core 仓储与 ES 读模型

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReviewAfterSalesDbContext.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModel.cs`

- [ ] 实现 `ReviewAfterSalesDbContext`（DbSet<Review>、DbSet<AfterSales>）
- [ ] 实现各 EF Core 仓储
- [ ] 定义 `ReviewReadModel`（reviewId、spuId、rating、content、images、userId、createdAt）
- [ ] 实现 ES 评价读模型同步消费者
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试
- [ ] 提交：`feat(review): add EF Core repositories and ES read model`

---

## Task 6: 基础设施层 — 事件消费者

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/OrderCompletedEventConsumer.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundFailedEventConsumer.cs`

- [ ] 实现 `OrderCompletedEventConsumer`（开放评价入口，标记订单可评价）
- [ ] 实现 `RefundSucceededEventConsumer`（加载 AfterSales→MarkRefundCompleted→发布 RefundCompletedEvent）
- [ ] 实现 `RefundFailedEventConsumer`（加载 AfterSales→MarkRefundFailed→通知买家）
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试
- [ ] 提交：`feat(review): add event consumers for order completion and refund results`

---

## Task 7: 应用层 — 评价管理用例

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewAppService.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs`

- [ ] 实现 `SubmitReviewAsync`（买家提交评价，校验资格）
- [ ] 实现 `SellerReplyAsync`（卖家回复评价）
- [ ] 实现 `ModerateReviewAsync`（运营审核/隐藏评价）
- [ ] 实现评价查询（按 SPU 分页、按订单查询、按用户查询）
- [ ] 编写单元测试
- [ ] 提交：`feat(review): add review application service`

---

## Task 8: 应用层 — 售后管理用例

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IAfterSalesAppService.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs`

- [ ] 实现 `SubmitAfterSalesAsync`（买家提交售后申请，校验资格与金额）
- [ ] 实现 `ApproveAfterSalesAsync`（运营审核通过→发布 RefundRequestedIntegrationEvent 请求退款）
- [ ] 实现 `RejectAfterSalesAsync`（运营驳回，附原因）
- [ ] 实现 `CancelAfterSalesAsync`（买家撤销，仅待审核态）
- [ ] 实现售后查询（买家/卖家/运营多视角分页查询）
- [ ] 编写单元测试
- [ ] 提交：`feat(review): add after-sales application service`

---

## Task 9: 表现层 — API 控制器

**文件:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs`

- [ ] 实现 `ReviewsController`（POST /api/reviews、POST /api/reviews/{id}/reply、GET /api/products/{spuId}/reviews）
- [ ] 实现运营审核接口（POST /api/admin/reviews/{id}/approve、POST .../{id}/hide）
- [ ] 实现 `AfterSalesController`（POST /api/after-sales、POST /api/after-sales/{id}/cancel）
- [ ] 实现卖家端接口（GET /api/seller/after-sales、POST /api/seller/after-sales/{id}/agree）
- [ ] 实现运营端接口（GET /api/admin/after-sales、POST /api/admin/after-sales/{id}/approve、POST .../{id}/reject）
- [ ] 配置 JWT 鉴权与角色策略
- [ ] 编写 API 集成测试覆盖评价提交→售后申请→审核→退款全流程
- [ ] 提交：`feat(review): add API controllers`
