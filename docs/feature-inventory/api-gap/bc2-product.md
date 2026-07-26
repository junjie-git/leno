# BC2 商品域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC2
- **中文名**：商品域
- **英文名**：Product
- **涉及端**：buyer-app / operations / seller
- **涉及页面数**：12 页（来自 feature-list）
  - buyer-app：02-home/home-feed，03-catalog/category-nav、product-detail、search-results、search（5 页）
  - operations：02-product-ops/product-audit、brand-management、category-management（3 页）
  - seller：03-product-management/product-list、product-edit、sku-management、price-history（4 页）
- **已实现 API 端点数**：31 个（28 个对外 + 3 个内部；内部端点不计入对外差异）
- **差异统计**：缺失 0 / 闲置 1 / 路径不一致 3 / 能力不匹配 3

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| GET | api/products/search | [SearchController.cs#L36](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/SearchController.cs#L36) | 买家全文搜索在售商品（ES 读侧） | 已认证用户（Buyer） |
| POST | api/products | [ProductsController.cs#L29](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L29) | 卖家创建草稿商品 | Seller |
| PUT | api/products/{id} | [ProductsController.cs#L42](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L42) | 卖家更新商品基础信息 | Seller |
| POST | api/products/{id}/skus | [ProductsController.cs#L51](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L51) | 卖家为商品新增 SKU | Seller |
| POST | api/products/{id}/submit | [ProductsController.cs#L60](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L60) | 卖家提交审核 | Seller |
| POST | api/products/{id}/take-down | [ProductsController.cs#L69](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L69) | 卖家下架商品 | Seller |
| POST | api/products/{id}/republish | [ProductsController.cs#L78](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L78) | 卖家重新上架商品（进入待审核） | Seller |
| GET | api/products/{id} | [ProductsController.cs#L88](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L88) | 查询商品详情（含 SKU），买家/卖家可查 | 已认证用户 |
| GET | api/products | [ProductsController.cs#L98](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L98) | 分页查询商品列表（卖家查本店，运营查全部） | Seller, Operator, Admin |
| POST | api/products/{id}/skus/{skuId}/price | [ProductsController.cs#L108](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L108) | 卖家调整 SKU 价格 | Seller |
| GET | api/products/{id}/price-history | [ProductsController.cs#L117](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L117) | 查询商品价格变更历史 | Seller |
| POST | api/admin/products/{id}/approve | [AdminProductsController.cs#L35](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L35) | 运营审核通过上架 | Admin, Operator |
| POST | api/admin/products/{id}/reject | [AdminProductsController.cs#L44](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L44) | 运营审核驳回 | Admin, Operator |
| POST | api/admin/products/skus/{skuId}/replenish | [AdminProductsController.cs#L53](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L53) | 卖家/运营为指定 SKU 补货 | Admin, Operator |
| POST | api/admin/products/{id}/skus/{skuId}/stock | [AdminProductsController.cs#L62](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L62) | 运营调整 SKU 库存（delta 方式） | Admin, Operator |
| GET | api/admin/products/all | [AdminProductsController.cs#L71](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L71) | 运营/管理员全量商品列表（跨店铺） | Admin, Operator |
| GET | api/categories/tree | [CategoriesController.cs#L28](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L28) | 查询分类树（仅启用，按层级与排序组装） | 已认证用户 |
| GET | api/categories/{id} | [CategoriesController.cs#L38](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L38) | 按标识查询分类详情 | 已认证用户 |
| POST | api/admin/categories | [CategoriesController.cs#L48](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L48) | 运营创建分类 | Admin, Operator |
| PUT | api/admin/categories/{id} | [CategoriesController.cs#L58](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L58) | 运营更新分类 | Admin, Operator |
| POST | api/admin/categories/{id}/enable | [CategoriesController.cs#L68](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L68) | 运营启用分类 | Admin, Operator |
| POST | api/admin/categories/{id}/disable | [CategoriesController.cs#L78](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L78) | 运营停用分类 | Admin, Operator |
| GET | api/brands | [BrandsController.cs#L28](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs#L28) | 分页查询品牌列表 | 已认证用户 |
| GET | api/brands/{id} | [BrandsController.cs#L38](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs#L38) | 按标识查询品牌详情 | 已认证用户 |
| POST | api/admin/brands | [BrandsController.cs#L48](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs#L48) | 运营创建品牌 | Admin, Operator |
| PUT | api/admin/brands/{id} | [BrandsController.cs#L58](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs#L58) | 运营更新品牌 | Admin, Operator |
| POST | api/admin/brands/{id}/enable | [BrandsController.cs#L68](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs#L68) | 运营启用品牌 | Admin, Operator |
| POST | api/admin/brands/{id}/disable | [BrandsController.cs#L78](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/BrandsController.cs#L78) | 运营停用品牌 | Admin, Operator |
| GET（内部） | internal/v1/products/skus/{skuId} | [InternalProductsController.cs#L23](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs#L23) | 内部按 SKU 标识查询概要信息（双路由：L25 旧路由 internal/products/skus/{skuId}，2026-08-15 下线） | 内部（X-Internal-Key） |
| POST（内部） | internal/v1/products/skus/batch | [InternalProductsController.cs#L40](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs#L40) | 内部批量查询 SKU 概要（双路由：L42 旧路由 internal/products/skus/batch，2026-08-15 下线） | 内部（X-Internal-Key） |
| GET（内部） | internal/v1/inventory/stock/{skuId} | [InternalInventoryController.cs#L29](file:///e:/Leno/src/Services/Inventory/Leno.Inventory.Api/Controllers/InternalInventoryController.cs#L29) | 内部查询指定 SKU 当前可用库存（Redis 权威值） | 内部（X-Internal-Key） |

> 来源：grep `src/Services/Product/**/Controllers/*.cs` 与 `src/Services/Inventory/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」，不计入对外差异
> 注：类级 `[Authorize]` 标注表示已认证用户可访问；`ProductsController` 类级标注 `[Authorize(Roles = "Seller")]`，但 `GET /api/products/{id}` 与 `GET /api/products` 在方法级单独覆盖鉴权角色；`SearchController` 类级为 `[Authorize]` 任何已认证用户可访问

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/products/search | [home-feed.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/home-feed.md) | 推荐商品流（按热度/综合排序） | ✅ | Buyer |
| GET | /api/categories/tree | [home-feed.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/home-feed.md) | 分类快捷入口数据 | ✅ | Buyer |
| GET | /api/categories/tree | [category-nav.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/category-nav.md) | 获取完整分类树（含层级） | ✅ | Buyer |
| GET | /api/products/search | [category-nav.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/category-nav.md) | 按分类查询商品列表 | ✅ | Buyer |
| GET | /api/products/{id} | [product-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/product-detail.md) | 查询商品详情（含 SKU 与图片） | ✅ | Buyer |
| GET | /api/products/{id}/price-history | [product-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/product-detail.md) | 查询价格变更历史 | ✅ | Buyer |
| GET | /api/products/search | [search-results.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/search-results.md) | 关键词 + 筛选搜索商品 | ✅ | Buyer |
| GET | /api/brands | [search-results.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/search-results.md) | 筛选面板品牌列表 | ✅ | Buyer |
| GET | /api/products/search | [search.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/search.md) | 联想词触发时预查询（可选） | ✅ | Buyer |
| GET | /api/admin/products/all | [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) | 全量商品分页查询（跨店铺） | ✅ | Admin, Operator |
| POST | /api/admin/products/{id}/approve | [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) | 审核通过并上架 | ✅ | Admin, Operator |
| POST | /api/admin/products/{id}/reject | [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) | 审核驳回 | ✅ | Admin, Operator |
| POST | /api/admin/products/{id}/skus/{skuId}/stock | [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) | 调整 SKU 库存（delta 方式） | ✅ | Admin, Operator |
| POST | /api/admin/products/skus/{skuId}/replenish | [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) | 为指定 SKU 补货 | ✅ | Admin, Operator |
| GET | /api/brands | [brand-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/brand-management.md) | 分页查询品牌列表 | ✅ | 已认证用户 |
| GET | /api/brands/{id} | [brand-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/brand-management.md) | 查询品牌详情 | ✅ | 已认证用户 |
| POST | /api/admin/brands | [brand-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/brand-management.md) | 创建品牌 | ✅ | Admin, Operator |
| PUT | /api/admin/brands/{id} | [brand-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/brand-management.md) | 更新品牌 | ✅ | Admin, Operator |
| POST | /api/admin/brands/{id}/enable | [brand-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/brand-management.md) | 启用品牌 | ✅ | Admin, Operator |
| POST | /api/admin/brands/{id}/disable | [brand-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/brand-management.md) | 停用品牌 | ✅ | Admin, Operator |
| GET | /api/categories/tree | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | 查询分类树（仅启用） | ✅ | 已认证用户 |
| GET | /api/categories/{id} | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | 查询分类详情 | ✅ | 已认证用户 |
| POST | /api/admin/categories | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | 创建分类 | ✅ | Admin, Operator |
| PUT | /api/admin/categories/{id} | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | 更新分类 | ✅ | Admin, Operator |
| POST | /api/admin/categories/{id}/enable | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | 启用分类 | ✅ | Admin, Operator |
| POST | /api/admin/categories/{id}/disable | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | 停用分类 | ✅ | Admin, Operator |
| GET | /api/seller/products | [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) | 分页查询本店商品（后端按 ShopId 过滤） | ✅ | Seller |
| POST | /api/seller/products/{id}/submit-review | [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) | 提交审核 | ✅ | Seller |
| POST | /api/seller/products/{id}/take-down | [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) | 下架商品 | ✅ | Seller |
| POST | /api/products | [product-edit.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-edit.md) | 新增草稿商品 | ✅ | Seller |
| PUT | /api/products/{id} | [product-edit.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-edit.md) | 更新商品基础信息 | ✅ | Seller |
| GET | /api/products/{id} | [product-edit.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-edit.md) | 查询商品详情（编辑回填） | ✅ | Seller |
| POST | /api/products/{id}/submit | [product-edit.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-edit.md) | 提交审核（保存后） | ✅ | Seller |
| GET | /api/products/{id} | [sku-management.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/sku-management.md) | 查询商品详情（含 SKU 集合） | ✅ | Seller |
| POST | /api/products/{id}/skus | [sku-management.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/sku-management.md) | 新增 SKU | ✅ | Seller |
| PUT | /api/products/{id} | [sku-management.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/sku-management.md) | 更新商品（含 SKU 编辑） | ✅ | Seller |
| POST | /api/products/{id}/skus/{skuId}/price | [sku-management.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/sku-management.md) | 调整 SKU 价格 | ✅ | Seller |
| GET | /api/products/{id}/price-history | [price-history.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/price-history.md) | 查询商品价格变更历史 | ✅ | Seller |
| GET | /api/products/{id} | [price-history.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/price-history.md) | 查询商品详情（含 SKU 信息回填筛选） | ✅ | Seller |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> BC2 范围内 12 个主页面引用的端点均标 ✅，但部分路径与源码不一致（详见 4.3）
> 期望端点去重后共 28 个

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> BC2 范围内 12 个主页面（home-feed/category-nav/product-detail/search-results/search/product-audit/brand-management/category-management/product-list/product-edit/sku-management/price-history）中，标 🚧/➕ 的页面仅 home-feed（➕），其引用的 BC2 端点（GET /api/products/search、GET /api/categories/tree）源码均已实现
> 故 BC2 范围内无缺失端点

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| POST | api/products/{id}/republish | [ProductsController.cs#L78](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L78) | 卖家重新上架商品（进入待审核） | 设计稿 product-list.md 第 8 步「重新上架」操作期望调用 POST /api/seller/products/{id}/submit-review，未引用本端点；建议 design-prompts 补调用或后端废弃以避免与 submit 重复 |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> 注：republish 与 submit 在源码中均产生「商品进入待审核」效果，submit 用于草稿/已驳回态，republish 用于已下架态；design-prompts product-list.md 第 8 步将「重新上架」也指向 submit-review，未引用 republish，故 republish 闲置

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| GET→GET | /api/seller/products → /api/products | [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) | [ProductsController.cs#L98](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L98) | design-prompts 期望卖家专用前缀 /api/seller/products，源码统一用 /api/products + Seller 角色按 ShopId 过滤；建议 design-prompts 改文档对齐源码（已实现 ApplyShopScope 自动注入 ShopId） |
| POST→POST | /api/seller/products/{id}/submit-review → /api/products/{id}/submit | [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) | [ProductsController.cs#L60](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L60) | 路径与方法一致，端点命名不一致：design-prompts 用 submit-review，源码用 submit；建议 design-prompts 改文档对齐源码 |
| POST→POST | /api/seller/products/{id}/take-down → /api/products/{id}/take-down | [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) | [ProductsController.cs#L69](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L69) | 路径与方法一致，前缀不一致：design-prompts 用 seller 前缀，源码统一前缀；建议 design-prompts 改文档对齐源码 |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> 三处路径不一致均集中在 seller/03-product-management/product-list.md，该页面 design-prompts 期望统一的 /api/seller/* 前缀，源码采用 /api/products 统一前缀 + 角色过滤；建议统一文档以源码为准

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 排序值 hot/priceAsc/priceDesc/sales | 排序值 price_asc/price_desc/default（ProductSearchQueryDto.Sort 注释 L93） | 缺 hot（综合热度）与 sales（销量）排序；命名风格不一致（驼峰 vs 下划线） | [search-results.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/search-results.md) 与 [home-feed.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/home-feed.md) | [SearchController.cs#L36](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/SearchController.cs#L36) | 源码补 hot/sales 排序逻辑（ES 读模型按销量字段排序），并统一命名风格；或 design-prompts 改文档对齐源码命名 |
| 批量审核通过/驳回 | 仅单个审核（POST /api/admin/products/{id}/approve 与 reject） | 缺批量审核端点；design-prompts product-audit.md 区域 B 工具栏含「批量审核通过、批量驳回」按钮 | [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) | [AdminProductsController.cs#L35](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L35) 与 [AdminProductsController.cs#L44](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs#L44) | 源码补 POST /api/admin/products/batch-approve 与 batch-reject 端点（接收 id 数组与原因），或 design-prompts 改为前端串行调用单个端点 |
| 分类树按关键词搜索 | GET /api/categories/tree 无关键词参数 | 缺关键词搜索能力；design-prompts category-management.md 第 64 行「输入关键词高亮匹配节点并自动展开父链」 | [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) | [CategoriesController.cs#L28](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L28) | 源码补 keyword 查询参数（前端本地过滤也可，但 >200 节点性能差），或 design-prompts 改为前端本地过滤 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> 注：源码 ProductSearchQueryDto（QueryDtos.cs#L76）字段含 Keyword/CategoryId/BrandId/MinPrice/MaxPrice/Sort/Page/PageSize，与 design-prompts 期望参数一致；BrandQueryDto（QueryDtos.cs#L36）含 Status/Keyword/Page/PageSize，与 design-prompts 期望一致

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | — | — | GET /api/seller/products → /api/products；POST /api/seller/products/{id}/submit-review → /api/products/{id}/submit；POST /api/seller/products/{id}/take-down → /api/products/{id}/take-down（三处均阻塞卖家商品管理操作） | — |
| P1 | — | — | — | sort 枚举值不一致（影响搜索结果排序体验）；批量审核缺失（影响运营审核效率）；分类树关键词搜索缺失（影响运营分类管理体验） |
| P2 | — | POST /api/products/{id}/republish | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖
- **上游依赖**：本 BC 依赖 BC10 卖家域的店铺事件
  - 订阅 `ShopSuspendedEvent` → 调用 `SuspendByShop` 置商品店铺暂停态，阻止新单
  - 订阅 `ShopClosedEvent` → 调用 `TakeDown` 下架全部商品，复用已有方法并产出 `ProductTakenDownEvent`
  - 订阅 `ShopResumedEvent` → 调用 `ResumeByShop` 恢复商品已上架态
  - 订阅 BC6 评价域 `ReviewSubmittedEvent`（入站）→ 更新商品评分摘要 `score`
- **下游依赖**：BC3 购物车域、BC4 订单域依赖本 BC 的端点/事件
  - BC3 购物车域消费 `ProductTakenDownEvent` → 标记对应购物车项为失效，阻止下单
  - BC3 购物车域、BC4 订单域消费 `ProductUpdatedEvent` → 同步商品快照与价格
  - BC4 订单域消费 `StockAdjustedEvent` → 同步库存基线
  - BC3/BC4 通过内部端点 `GET internal/v1/products/skus/{skuId}` 与 `POST internal/v1/products/skus/batch` 获取 SKU 概要
  - BC4 通过内部端点 `GET internal/v1/inventory/stock/{skuId}` 获取可用库存
- **集成事件订阅/发布清单**：
  - 发布（出站）：`ProductCreatedEvent`、`ProductSubmittedForReviewEvent`、`ProductPublishedEvent`、`ProductRejectedEvent`、`ProductUpdatedEvent`、`ProductTakenDownEvent`、`StockAdjustedEvent`、`CategoryChangedEvent`
  - 订阅（入站）：`ReviewSubmittedEvent`（BC6 评价域）、`ShopSuspendedEvent`（BC10 卖家域）、`ShopClosedEvent`（BC10 卖家域）、`ShopResumedEvent`（BC10 卖家域）

> 来源：docs/spec/02-商品域.md §1 上下文映射 与 §3 领域事件清单

## 8. 行动建议
- **立即修复**（P0 缺失/不一致）
  - 文档同步：将 [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) 第 3 节 API 表与第 4 节交互流程中 `/api/seller/products`、`/api/seller/products/{id}/submit-review`、`/api/seller/products/{id}/take-down` 三处端点改为源码实际路径 `/api/products`、`/api/products/{id}/submit`、`/api/products/{id}/take-down`，避免卖家前端按错误路径调用导致 404 阻塞商品管理操作
  - 备选方案：若需保留 `/api/seller/*` 前缀以隔离卖家语义，源码补 3 个路由别名端点（不推荐，增加维护成本）
- **短期补充**（P1 缺失/不匹配）
  - sort 枚举值补齐：在 [SearchController.cs#L36](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/SearchController.cs#L36) 对应的 ProductSearchQueryDto.Sort 与 ES 查询逻辑中补 `hot`（综合热度，按 salesCount 倒序）与 `sales`（销量倒序）两个排序值，统一命名风格为下划线（price_asc/price_desc/hot/sales），同步更新 [search-results.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/search-results.md) 与 [home-feed.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/home-feed.md) 文档
  - 批量审核端点：在 [AdminProductsController.cs](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/AdminProductsController.cs) 补 `POST /api/admin/products/batch-approve` 与 `POST /api/admin/products/batch-reject`（接收 `ids: Guid[]` 与 `reason: string`），同步更新 [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) 第 3 节 API 表
  - 分类树关键词搜索：在 [CategoriesController.cs#L28](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/CategoriesController.cs#L28) GetTreeAsync 补 `keyword` 查询参数（服务端按名称模糊匹配并返回匹配节点及父链），或确认 design-prompts 改为前端本地过滤
- **长期规划**（P2 闲置/废弃）
  - 闲置端点 `POST /api/products/{id}/republish`（[ProductsController.cs#L78](file:///e:/Leno/src/Services/Product/Leno.Product.Api/Controllers/ProductsController.cs#L78)）：评估与 `POST /api/products/{id}/submit` 是否合并（两者均产生「进入待审核」效果，submit 用于草稿/已驳回态，republish 用于已下架态）；若保留则在 [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) 第 4 节第 8 步「重新上架」补调用 republish；若废弃则源码移除并更新 spec
- **文档同步**（design-prompts API 引用对齐到源码）
  - [product-list.md](file:///e:/Leno/docs/design-prompts/seller/03-product-management/product-list.md) 第 3 节 API 表 3 行路径全部对齐源码（P0）
  - [search-results.md](file:///e:/Leno/docs/design-prompts/buyer-app/03-catalog/search-results.md) 第 3 节请求参数 sort 字段值对齐源码命名（P1）
  - [home-feed.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/home-feed.md) 第 3 节请求参数 sort=hot 改为源码支持的值（P1）
  - [product-audit.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/product-audit.md) 第 3 节补批量审核端点（P1）
  - [category-management.md](file:///e:/Leno/docs/design-prompts/operations/02-product-ops/category-management.md) 第 3 节补 keyword 参数或第 4 节明确前端本地过滤（P1）
