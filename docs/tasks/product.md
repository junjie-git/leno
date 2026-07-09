# 商品域 (Product) 开发任务

> **限界上下文**: BC2 商品域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis / Elasticsearch  
> **依赖**: `shared-kernel`、`seller-shop`（ShopId 引用）  
> **对应文档**: `02-商品域.md`

---

## 模块概述

商品域权威持有 SPU/SKU、分类、品牌与可售库存基线。提供商品发布、编辑、上下架、库存管理、搜索与多视角查询能力。下游订单域通过防腐层查询 SKU 价格与库存，订单行以快照方式固化商品信息。

---

## Task 1: 项目初始化与领域层 — SPU 聚合

**文件:**
- Create: `src/Services/Product/Leno.Product.Domain/Leno.Product.Domain.csproj`
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/SKU.cs`

- [ ] 创建 Leno.Product.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `SPU` 聚合根（SpuId、ShopId/SellerId、Title、Subtitle、MainImageUrl、CategoryId、BrandId、Status、Specs、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `SPU.Create` 工厂方法（校验标题、分类、品牌，置草稿态，附加 `ProductCreatedEvent`）
- [ ] 实现 `SPU.SubmitForReview`（草稿→待审核）、`SPU.Approve`（待审核→已上架）、`SPU.Reject`（待审核→草稿）
- [ ] 实现 `SPU.TakeDown`（已上架→已下架，附加 `ProductTakenDownEvent`）、`SPU.Republish`（已下架→待审核）
- [ ] 实现 `SPU.UpdateInfo`/`SPU.UpdateSpecs` 方法
- [ ] 定义 `ProductStatus` 值对象（Draft/PendingReview/OnSale/OffShelf）
- [ ] 编写单元测试覆盖状态机流转
- [ ] 提交：`feat(product): add SPU aggregate root with status machine`

---

## Task 2: 领域层 — SKU 实体与值对象

**文件:**
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/SKU.cs`
- Create: `src/Services/Product/Leno.Product.Domain/ValueObjects/SkuSpec.cs`
- Create: `src/Services/Product/Leno.Product.Domain/ValueObjects/ProductImage.cs`

- [ ] 实现 `SKU` 实体（SkuId、SpuId、SkuCode、Price、StockQty、SpecAttributes、Status、ImageUrl）
- [ ] 实现 `SKU.Create`（校验 SkuCode 唯一、Price > 0，附加到 SPU 聚合）
- [ ] 实现 `SKU.UpdatePrice`、`SKU.UpdateStock` 方法
- [ ] 实现 `SkuSpec` 值对象（规格属性集合，引用共享内核 SpecAttribute）
- [ ] 实现 `ProductImage` 值对象（Url、SortOrder、IsMain）
- [ ] 编写单元测试覆盖 SKU 校验与价格更新
- [ ] 提交：`feat(product): add SKU entity and value objects`

---

## Task 3: 领域层 — 分类与品牌聚合

**文件:**
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/Category.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/Brand.cs`

- [ ] 实现 `Category` 聚合根（CategoryId、Name、ParentId、Level、SortOrder、Status）
- [ ] 实现 `Category.Create`/`Category.Update`/`Category.Enable`/`Category.Disable` 方法
- [ ] 支持多级分类树（ParentId 自引用，Level 限制最大 3 级）
- [ ] 实现 `Brand` 聚合根（BrandId、Name、Logo、Status）
- [ ] 实现 `Brand.Create`/`Brand.Update`/`Brand.Enable`/`Brand.Disable` 方法
- [ ] 编写单元测试覆盖分类树与品牌管理
- [ ] 提交：`feat(product): add Category and Brand aggregates`

---

## Task 4: 领域层 — 库存聚合与领域服务

**文件:**
- Create: `src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Services/IProductQueryService.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Repositories/ISPURepository.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Repositories/ICategoryRepository.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Repositories/IBrandRepository.cs`

- [ ] 实现 `StockBaseline` 聚合（SkuId、AvailableQty、ReservedQty、DeductedQty、Version）
- [ ] 实现 `StockBaseline.Replenish`（补货，AvailableQty 上调，附加 `StockAdjustedEvent`）
- [ ] 实现 `StockBaseline.SyncReserved`/`SyncDeducted`/`SyncReleased`（消费订单域库存事件同步基线）
- [ ] 定义 `IProductQueryService` 防腐层接口（GetSkuPriceAsync、GetSkuStockAsync、CheckSkusAvailableAsync）
- [ ] 定义各仓储接口（ISPURepository 含 GetByShopIdAsync、QueryAsync 等）
- [ ] 编写单元测试覆盖库存基线同步
- [ ] 提交：`feat(product): add stock baseline and repository interfaces`

---

## Task 5: 领域事件定义

**文件:**
- Create: `src/Services/Product/Leno.Product.Domain/Events/ProductPublishedEvent.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Events/ProductTakenDownEvent.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Events/StockAdjustedEvent.cs`
- Create: `src/Services/Product/Leno.Product.Domain/Events/ProductReviewedEvent.cs`

- [ ] 定义 `ProductPublishedEvent`（spuId、skuIds、sellerId/shopId）— 消费方：卖家域（商品数+1）、ES 读库
- [ ] 定义 `ProductTakenDownEvent`（spuId、sellerId/shopId）— 消费方：卖家域（商品数-1）、ES 读库
- [ ] 定义 `StockAdjustedEvent`（skuId、availableQty、adjustedAt）— 消费方：订单域（同步库存基线）
- [ ] 定义 `ProductReviewedEvent`（spuId、result、reviewedBy）— 审核结果事件
- [ ] 提交：`feat(product): add domain integration events`

---

## Task 6: 基础设施层 — EF Core 仓储实现

**文件:**
- Create: `src/Services/Product/Leno.Product.Infrastructure/ProductDbContext.cs`
- Create: `src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs`
- Create: `src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreCategoryRepository.cs`
- Create: `src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreBrandRepository.cs`
- Create: `src/Services/Product/Leno.Product.Infrastructure/Configurations/SPUConfiguration.cs`

- [ ] 实现 `ProductDbContext`（DbSet<SPU>、DbSet<Category>、DbSet<Brand>、DbSet<StockBaseline>）
- [ ] 配置 SPU 实体映射（SKU 作为 Owned Entity 或 One-to-Many，Specs JSON 列）
- [ ] 配置 Category 自引用关系与 Brand 映射
- [ ] 实现各 EF Core 仓储（含分页查询、按 ShopId 过滤、按状态过滤）
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证仓储 CRUD
- [ ] 提交：`feat(product): add EF Core repository implementations`

---

## Task 7: 基础设施层 — ES 搜索读模型

**文件:**
- Create: `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModel.cs`
- Create: `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs`
- Create: `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs`

- [ ] 定义 `ProductReadModel`（spuId、title、subtitle、mainImage、category、brand、priceRange、shopId、status、specs）
- [ ] 实现 `ProductSearchService`（ES 全文搜索、分类过滤、价格区间、排序、分页）
- [ ] 实现 `ProductReadModelSyncConsumer`（消费 ProductPublishedEvent→索引、ProductTakenDownEvent→删除/标记）
- [ ] 配置 ES 索引映射（中文分词器 ik_max_word、关键字段索引）
- [ ] 编写集成测试验证搜索与读模型同步
- [ ] 提交：`feat(product): add Elasticsearch search read model`

---

## Task 8: 应用层 — 商品发布与管理用例

**文件:**
- Create: `src/Services/Product/Leno.Product.Application/ISPUAppService.cs`
- Create: `src/Services/Product/Leno.Product.Application/DTOs/ProductDto.cs`
- Create: `src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs`

- [ ] 定义 `ISPUAppService` 接口（CreateAsync、UpdateAsync、SubmitForReviewAsync、TakeDownAsync、RepublishAsync、GetByIdAsync）
- [ ] 实现商品创建用例（卖家创建草稿→提交审核→运营审核通过上架）
- [ ] 实现商品编辑与上下架用例（校验卖家归属、状态机校验）
- [ ] 实现 `GetByIdAsync`（返回 SPU+SKU 详情 DTO）
- [ ] 编写 FluentValidation 输入校验
- [ ] 编写单元测试覆盖商品管理用例
- [ ] 提交：`feat(product): add SPU application service`

---

## Task 9: 应用层 — 分类品牌管理与库存查询

**文件:**
- Create: `src/Services/Product/Leno.Product.Application/ICategoryAppService.cs`
- Create: `src/Services/Product/Leno.Product.Application/IBrandAppService.cs`
- Create: `src/Services/Product/Leno.Product.Application/IInventoryAppService.cs`
- Create: `src/Services/Product/Leno.Product.Application/Services/ProductQueryAppService.cs`

- [ ] 实现分类树管理用例（CRUD + 树形结构查询）
- [ ] 实现品牌管理用例（CRUD + 启停）
- [ ] 实现 `ProductQueryAppService`（防腐层实现，供订单域查询 SKU 价格与库存）
- [ ] 实现库存补货用例（卖家/运营补货，发布 StockAdjustedEvent）
- [ ] 编写单元测试
- [ ] 提交：`feat(product): add category, brand and inventory services`

---

## Task 10: 表现层 — API 控制器

**文件:**
- Create: `src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs`
- Create: `src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs`
- Create: `src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs`
- Create: `src/Services/Product/Leno.Product.Api/Controllers/SearchController.cs`

- [ ] 实现 `ProductsController`（卖家端：POST/PUT /api/products，POST .../{id}/submit、.../{id}/take-down）
- [ ] 实现运营审核接口（POST /api/admin/products/{id}/approve、POST .../{id}/reject）
- [ ] 实现 `CategoriesController`（GET /api/categories/tree、POST/PUT /api/admin/categories）
- [ ] 实现 `BrandsController`（GET /api/brands、POST/PUT /api/admin/brands）
- [ ] 实现 `SearchController`（GET /api/products/search 全文搜索）
- [ ] 实现 GET /api/products/{id} 商品详情（买家视角）
- [ ] 配置 JWT 鉴权与角色策略（卖家/运营/买家不同端点）
- [ ] 编写 API 集成测试覆盖发布→审核→上架→搜索全流程
- [ ] 提交：`feat(product): add API controllers for products, categories, brands and search`

---

## Task 11: 消费卖家域店铺状态事件

**文件:**
- Create: `src/Services/Product/Leno.Product.Infrastructure/Consumers/ShopEventConsumer.cs`

- [ ] 实现 `ShopSuspendedEvent` 消费者（店铺暂停→该店铺商品置为不可售）
- [ ] 实现 `ShopResumedEvent` 消费者（店铺恢复→恢复商品可售）
- [ ] 实现 `ShopClosedEvent` 消费者（店铺关闭→下架全部商品）
- [ ] 编写集成测试验证事件消费
- [ ] 提交：`feat(product): add shop event consumers for product availability sync`
