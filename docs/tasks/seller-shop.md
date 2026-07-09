# 卖家与店铺管理域 (Seller & Shop) 开发任务

> **限界上下文**: BC10 卖家与店铺管理域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis  
> **依赖**: `shared-kernel`、`user-auth`（用户角色关联）  
> **对应文档**: `11-卖家与店铺管理域.md`

---

## 模块概述

卖家与店铺管理域管理卖家入驻申请、店铺信息、运营数据与店铺状态。卖家入驻经审核后开通店铺，关联用户域角色。店铺状态（营业/暂停/关闭）联动商品域商品可售性。提供卖家工作台数据概览与店铺运营指标查询。

---

## Task 1: 项目初始化与领域层 — Shop 聚合

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Leno.SellerShop.Domain.csproj`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/Shop.cs`

- [ ] 创建 Leno.SellerShop.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `Shop` 聚合根（ShopId、SellerId/UserId、ShopName、Logo、Description、ContactPhone、ContactEmail、BusinessLicense、Address、Status、ProductCount、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `Shop.Create` 工厂方法（卖家提交入驻申请，置待审核态）
- [ ] 实现 `Shop.Approve(reviewedBy)`（审核通过→营业中，附加 `ShopApprovedEvent`）
- [ ] 实现 `Shop.Reject(reviewedBy, reason)`（审核驳回→已驳回）
- [ ] 实现 `Shop.Suspend(reason)`（暂停营业，附加 `ShopSuspendedEvent`）
- [ ] 实现 `Shop.Resume()`（恢复营业，附加 `ShopResumedEvent`）
- [ ] 实现 `Shop.Close(reason)`（关闭店铺，附加 `ShopClosedEvent`）
- [ ] 实现 `Shop.UpdateInfo`/`Shop.UpdateLogo`/`Shop.UpdateContact` 方法
- [ ] 实现 `Shop.IncrementProductCount`/`DecrementProductCount`（消费商品域事件维护商品数）
- [ ] 定义 `ShopStatus` 值对象（PendingReview/Active/Suspended/Rejected/Closed）
- [ ] 编写单元测试覆盖状态机
- [ ] 提交：`feat(seller-shop): add Shop aggregate root`

---

## Task 2: 领域层 — SellerProfile 聚合与运营指标

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/SellerProfile.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopMetrics.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/ValueObjects/BusinessLicense.cs`

- [ ] 实现 `SellerProfile` 聚合根（SellerId/UserId、RealName、IdCard、BusinessLicenseNo、BankAccount、Status、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `SellerProfile.Create`/`Update`/`SubmitForVerification` 方法
- [ ] 实现 `BusinessLicense` 值对象（LicenseNo、ImageUrl、ExpireDate）
- [ ] 实现 `ShopMetrics` 聚合（ShopId、Date、OrderCount、SalesAmount、ProductCount、AvgRating、RefundCount）
- [ ] 实现 `ShopMetrics.Record(orderCount, salesAmount)`（每日更新指标）
- [ ] 编写单元测试
- [ ] 提交：`feat(seller-shop): add SellerProfile and ShopMetrics aggregates`

---

## Task 3: 领域层 — 仓储接口与领域服务

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/IShopRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/ISellerProfileRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/IShopMetricsRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Services/IShopQueryService.cs`

- [ ] 定义 `IShopRepository`（GetByIdAsync、GetBySellerIdAsync、QueryAsync、AddAsync、UpdateAsync）
- [ ] 定义 `ISellerProfileRepository`（GetByUserIdAsync、AddAsync、UpdateAsync）
- [ ] 定义 `IShopMetricsRepository`（GetByShopIdAsync、GetByDateRangeAsync、UpsertAsync）
- [ ] 定义 `IShopQueryService` 防腐层接口（GetShopStatusAsync 供商品域查询店铺可售状态）
- [ ] 提交：`feat(seller-shop): add repository interfaces`

---

## Task 4: 领域事件定义

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Events/ShopApprovedEvent.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Events/ShopSuspendedEvent.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Events/ShopResumedEvent.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Events/ShopClosedEvent.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Events/SellerRegisteredEvent.cs`

- [ ] 定义 `ShopApprovedEvent`（shopId、sellerId、shopName）— 消费方：用户域（分配卖家角色）、通知域
- [ ] 定义 `ShopSuspendedEvent`（shopId、sellerId）— 消费方：商品域（商品不可售）、通知域
- [ ] 定义 `ShopResumedEvent`（shopId、sellerId）— 消费方：商品域（商品恢复可售）
- [ ] 定义 `ShopClosedEvent`（shopId、sellerId）— 消费方：商品域（下架全部商品）、用户域（移除卖家角色）
- [ ] 定义 `SellerRegisteredEvent`（sellerId、userId、shopName）— 入驻申请提交
- [ ] 提交：`feat(seller-shop): add domain integration events`

---

## Task 5: 基础设施层 — EF Core 仓储与事件消费者

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreSellerProfileRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopMetricsRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/ProductEventConsumer.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/OrderEventConsumer.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/ReviewEventConsumer.cs`

- [ ] 实现 `SellerShopDbContext`（各 DbSet 配置）
- [ ] 实现各 EF Core 仓储
- [ ] 创建 EF Core Migration 脚本
- [ ] 实现 `ProductEventConsumer`（消费 ProductPublishedEvent→IncrementProductCount、ProductTakenDownEvent→DecrementProductCount）
- [ ] 实现 `OrderEventConsumer`（消费 OrderCompletedEvent→更新 ShopMetrics 销量与销售额）
- [ ] 实现 `ReviewEventConsumer`（消费 ReviewSubmittedEvent→更新 ShopMetrics 平均评分）
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试
- [ ] 提交：`feat(seller-shop): add EF Core repositories and event consumers`

---

## Task 6: 应用层 — 卖家入驻与店铺管理用例

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/IShopAppService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/ISellerAppService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerAppService.cs`

- [ ] 实现 `SubmitShopApplicationAsync`（卖家提交入驻申请：创建 Shop+SellerProfile，置待审核）
- [ ] 实现 `ApproveShopApplicationAsync`（运营审核通过→Shop.Approve→发布事件→通知用户域分配角色）
- [ ] 实现 `RejectShopApplicationAsync`（运营驳回，附原因）
- [ ] 实现 `UpdateShopInfoAsync`（卖家更新店铺信息）
- [ ] 实现 `SuspendShopAsync`/`ResumeShopAsync`/`CloseShopAsync`（运营管理店铺状态）
- [ ] 实现 `GetShopInfoAsync`/`GetSellerProfileAsync`（查询店铺与卖家信息）
- [ ] 编写单元测试
- [ ] 提交：`feat(seller-shop): add shop and seller application services`

---

## Task 7: 应用层 — 卖家工作台与运营数据

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/ISellerDashboardAppService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs`

- [ ] 实现 `GetDashboardAsync(sellerId)`（工作台概览：今日订单数、销售额、待发货、待评价、商品数）
- [ ] 实现 `GetSalesTrendAsync(shopId, dateRange)`（销售趋势图表数据）
- [ ] 实现 `GetShopMetricsAsync(shopId, dateRange)`（店铺运营指标查询）
- [ ] 实现运营视角查询（全平台店铺列表、审核队列、店铺状态管理）
- [ ] 编写单元测试
- [ ] 提交：`feat(seller-shop): add seller dashboard and metrics services`

---

## Task 8: 表现层 — API 控制器

**文件:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs`

- [ ] 实现 `ShopsController`（卖家端：POST /api/shops/application、PUT /api/shops/me、GET /api/shops/me）
- [ ] 实现运营端接口（GET /api/admin/shops、POST /api/admin/shops/{id}/approve、POST .../{id}/reject、POST .../{id}/suspend、POST .../{id}/resume、POST .../{id}/close）
- [ ] 实现 `SellerDashboardController`（GET /api/seller/dashboard、GET /api/seller/sales-trend、GET /api/seller/metrics）
- [ ] 配置 JWT 鉴权与角色策略
- [ ] 编写 API 集成测试覆盖入驻→审核→营业→暂停→恢复全流程
- [ ] 提交：`feat(seller-shop): add API controllers`
