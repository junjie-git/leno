# BC6 评价与售后域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC6
- **中文名**：评价与售后域
- **英文名**：ReviewAfterSales
- **涉及端**：buyer-app / operations / seller
- **涉及页面数**：10 页（buyer-app 09-review 3 页 + 10-after-sales 3 页；operations 05-order-ops 2 页；seller 06-after-sales 2 页 + 07-review 1 页）
- **已实现 API 端点数**：22 个（ReviewsController 9 个 + AfterSalesController 13 个，全部位于 src/Services/ReviewAfterSales/）
- **差异统计**：缺失 3 / 闲置 2 / 路径不一致 1 / 能力不匹配 4

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| POST | /api/reviews | [ReviewsController.cs#L45](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L45) | 买家提交评价 | Buyer |
| GET | /api/reviews/order-line/{orderLineId} | [ReviewsController.cs#L56](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L56) | 按订单行查询评价 | Buyer |
| GET | /api/products/{spuId}/reviews | [ReviewsController.cs#L66](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L66) | 按 SPU 分页查询已通过评价 | 公开（匿名） |
| GET | /api/reviews/mine | [ReviewsController.cs#L76](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L76) | 买家我的评价 | Buyer |
| POST | /api/reviews/images | [ReviewsController.cs#L87](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L87) | 买家上传评价图片 | Buyer |
| POST | /api/reviews/{id}/reply | [ReviewsController.cs#L143](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L143) | 卖家回复评价 | Seller |
| POST | /api/admin/reviews/{id}/approve | [ReviewsController.cs#L156](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L156) | 运营审核通过评价 | Operator,Admin |
| POST | /api/admin/reviews/{id}/hide | [ReviewsController.cs#L167](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L167) | 运营隐藏违规评价 | Operator,Admin |
| GET | /api/admin/reviews | [ReviewsController.cs#L178](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L178) | 运营分页查询评价 | Operator,Admin |
| POST | /api/after-sales | [AfterSalesController.cs#L45](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L45) | 买家提交售后申请（AfterSales，待切换） | Buyer |
| POST | /api/after-sales/{id}/return-goods | [AfterSalesController.cs#L56](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L56) | 买家退货填写物流单号（AfterSales，待切换） | Buyer |
| POST | /api/after-sales/{id}/cancel | [AfterSalesController.cs#L67](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L67) | 买家撤销售后申请（AfterSales，待切换） | Buyer |
| GET | /api/after-sales/order/{orderId} | [AfterSalesController.cs#L78](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L78) | 买家按订单查询售后单（AfterSales，待切换） | Buyer |
| GET | /api/after-sales/mine | [AfterSalesController.cs#L89](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L89) | 买家我的售后单（AfterSales，待切换） | Buyer |
| POST | /api/after-sales/images | [AfterSalesController.cs#L100](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L100) | 买家上传售后凭证图片（AfterSales，待切换） | Buyer |
| GET | /api/seller/after-sales | [AfterSalesController.cs#L156](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L156) | 卖家查询收到的售后单（AfterSales，待切换） | Seller |
| POST | /api/seller/after-sales/{id}/approve | [AfterSalesController.cs#L171](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L171) | 卖家审核同意售后（AfterSales，待切换） | Seller |
| POST | /api/seller/after-sales/{id}/reject | [AfterSalesController.cs#L182](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L182) | 卖家驳回售后（AfterSales，待切换） | Seller |
| POST | /api/seller/after-sales/{id}/confirm-return | [AfterSalesController.cs#L193](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L193) | 卖家确认收到退货（AfterSales，待切换） | Seller |
| POST | /api/admin/after-sales/{id}/approve | [AfterSalesController.cs#L206](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L206) | 运营审核通过售后（AfterSales，待切换） | Operator,Admin |
| POST | /api/admin/after-sales/{id}/reject | [AfterSalesController.cs#L217](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L217) | 运营驳回售后（AfterSales，待切换） | Operator,Admin |
| GET | /api/admin/after-sales | [AfterSalesController.cs#L228](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L228) | 运营分页查询全平台售后单（AfterSales，待切换） | Operator,Admin |

> 来源：grep `src/Services/ReviewAfterSales/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> 源码目录 `src/Services/AfterSales/` 当前不存在（Glob 返回 No file found），所有评价与售后端点均位于旧 BC 目录 `src/Services/ReviewAfterSales/` 下。AfterSalesController.cs 中的 13 个售后端点均标注「（AfterSales，待切换）」，待新 BC 独立后迁移。
> 本 BC 未发现 Internal*Controller.cs，无内部端点。

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/reviews/mine | [my-reviews.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/my-reviews.md) | 查询我的评价列表（按状态过滤） | ✅ | Buyer |
| POST | /api/reviews/{reviewId}/append | [my-reviews.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/my-reviews.md) | 买家追评（页面标 ✅，但源码无实现） | 🚧 | Buyer |
| GET | /api/products/{spuId}/reviews | [product-reviews.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/product-reviews.md) | 按 SPU 分页查询已通过评价 | ✅ | 公开 |
| POST | /api/orders/{orderId}/reviews | [review-submit.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/review-submit.md) | 提交评价（路径与源码不一致） | 🚧 | Buyer |
| POST | /api/after-sales | [after-sales-apply.md](file:///e:/Leno/docs/design-prompts/buyer-app/10-after-sales/after-sales-apply.md) | 提交售后申请 | ✅ | Buyer |
| POST | /api/after-sales/images | [after-sales-apply.md](file:///e:/Leno/docs/design-prompts/buyer-app/10-after-sales/after-sales-apply.md) | 上传售后凭证图片 | ✅ | Buyer |
| GET | /api/after-sales/order/{orderId} | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/10-after-sales/after-sales-detail.md) | 按订单查询售后单 | ✅ | Buyer |
| POST | /api/after-sales/{id}/cancel | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/10-after-sales/after-sales-detail.md) | 撤销售后申请 | ✅ | Buyer |
| POST | /api/after-sales/{id}/return-goods | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/10-after-sales/after-sales-detail.md) | 买家退货填写物流单号 | ✅ | Buyer |
| GET | /api/after-sales/mine | [my-after-sales.md](file:///e:/Leno/docs/design-prompts/buyer-app/10-after-sales/my-after-sales.md) | 查询我的售后单分页列表 | ✅ | Buyer |
| GET | /api/admin/after-sales | [after-sales.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/after-sales.md) | 分页查询全平台售后单 | ✅ | Operator,Admin |
| POST | /api/admin/after-sales/{id}/approve | [after-sales.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/after-sales.md) | 运营审核通过售后 | ✅ | Operator,Admin |
| POST | /api/admin/after-sales/{id}/reject | [after-sales.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/after-sales.md) | 运营驳回售后 | ✅ | Operator,Admin |
| GET | /api/admin/reviews | [review-audit.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/review-audit.md) | 分页查询评价（按状态过滤） | ✅ | Operator,Admin |
| POST | /api/admin/reviews/{id}/approve | [review-audit.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/review-audit.md) | 审核通过评价 | ✅ | Operator,Admin |
| POST | /api/admin/reviews/{id}/hide | [review-audit.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/review-audit.md) | 隐藏违规评价 | ✅ | Operator,Admin |
| GET | /api/seller/after-sales | [after-sales-list.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-list.md) | 卖家查询收到的售后单 | ✅ | Seller |
| GET | /api/seller/after-sales/{id} | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-detail.md) | 卖家查询售后单详情 | ➕ | Seller |
| POST | /api/seller/after-sales/{id}/approve | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-detail.md) | 卖家审核同意售后 | ✅ | Seller |
| POST | /api/seller/after-sales/{id}/reject | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-detail.md) | 卖家驳回售后 | ✅ | Seller |
| POST | /api/seller/after-sales/{id}/confirm-return | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-detail.md) | 卖家确认收到退货 | ✅ | Seller |
| GET | /api/seller/reviews | [review-reply.md](file:///e:/Leno/docs/design-prompts/seller/07-review/review-reply.md) | 卖家查询本店铺商品评价 | ➕ | Seller |
| POST | /api/reviews/{id}/reply | [review-reply.md](file:///e:/Leno/docs/design-prompts/seller/07-review/review-reply.md) | 卖家回复评价 | ✅ | Seller |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 跨 BC 端点已剔除：buyer-app/after-sales-detail 引用的 `GET /api/refunds/{afterSalesId}` 属 BC8 支付集成域；review-submit / after-sales-apply 引用的 `GET /api/orders/{orderId}` 属 BC4 订单域。

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| POST | /api/reviews/{reviewId}/append | [my-reviews.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/my-reviews.md) | 买家追评（页面整体标 ✅，但 append 端点源码无实现） | P1 | ReviewsController 新增 `[HttpPost("api/reviews/{id:guid}/append")]`，调用 `IReviewAppService.AppendAdditionalReviewAsync`，调用 `Review.AppendAdditionalReview` 聚合方法；同步修正 my-reviews 页面实现状态标注 |
| GET | /api/seller/after-sales/{id} | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-detail.md) | 卖家查询售后单详情（页面标 ➕） | P0 | AfterSalesController 新增 `[HttpGet("api/seller/after-sales/{id:guid}")]`，复用 `IAfterSalesAppService` 现有聚合根加载逻辑，新增按 `afterSalesId` + `sellerId` 归属校验查询方法，返回 `AfterSalesDto` |
| GET | /api/seller/reviews | [review-reply.md](file:///e:/Leno/docs/design-prompts/seller/07-review/review-reply.md) | 卖家查询本店铺商品评价（页面标 ➕） | P1 | ReviewsController 新增 `[HttpGet("api/seller/reviews")]`，新增 `IReviewAppService.GetBySellerAsync(sellerId, rating?, replied?, productName?, startDate?, endDate?, page, pageSize)`，通过 Review 聚合关联 SPU 的 sellerId 过滤，仅返回 `status=Approved` |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> my-reviews 页面整体状态为 ✅，但其「3. 数据模型与 API 对接」明确列出 append 端点，源码无对应实现，归为缺失并建议同步修正页面状态标注。

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| GET | /api/reviews/order-line/{orderLineId} | [ReviewsController.cs#L56](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L56) | 按订单行查询评价（CreatedAtAction 路由用） | 保留观察（POST /api/reviews 的 CreatedAtAction 依赖此端点）；design-prompts 无页面引用，建议在 review-submit 提交成功跳转场景补调用，或保留为内部路由 |
| POST | /api/reviews/images | [ReviewsController.cs#L87](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L87) | 买家上传评价图片 | 设计稿补调用：review-submit 页面「3. 数据模型与 API 对接」段应显式列出 `POST /api/reviews/images` 作为图片上传端点，与 after-sales-apply 的 `POST /api/after-sales/images` 保持一致 |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> 注意：POST /api/reviews（源码提交评价端点）与 design-prompts 期望的 POST /api/orders/{orderId}/reviews 属路径不一致（见 4.3），不在此列。

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| POST→POST | /api/orders/{orderId}/reviews → /api/reviews | [review-submit.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/review-submit.md) | [ReviewsController.cs#L45](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L45) | 改代码：将源码 `[HttpPost("api/reviews")]` 调整为 `[HttpPost("api/orders/{orderId:guid}/reviews")]`，与 spec 第 5 章 API 设计及 design-prompts 期望路径对齐；orderId 从路径参数读取，body 保留 orderLineId 等字段 |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> spec 文档（docs/spec/06-评价与售后域.md 第 5 章）期望 `POST /api/orders/{orderId}/reviews`，源码实现为 `POST /api/reviews`（orderId 在 body 内）。需以 spec 与 design-prompts 为准调整源码路由。

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 分页+状态筛选 | 分页（无状态筛选） | 缺少 status 查询参数 | [my-reviews.md](file:///e:/Leno/docs/design-prompts/buyer-app/09-review/my-reviews.md) | [ReviewsController.cs#L76](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L76) | 补 `[FromQuery] ReviewStatus? status` 参数，`GetReviewsByUserAsync` 增加 status 过滤 |
| 分页+多维度筛选 | 分页+状态筛选 | 缺少商品名称、评分、时间范围筛选 | [review-audit.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/review-audit.md) | [ReviewsController.cs#L178](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L178) | 补 `productName`、`rating`、`startDate`、`endDate` 等 query 参数，`QueryReviewsAsync` 增加对应过滤 |
| 分页+多维度筛选 | 分页+orderId/userId/sellerId/status 筛选 | 缺少售后单号、售后类型、时间范围筛选 | [after-sales.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/after-sales.md) | [AfterSalesController.cs#L228](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L228) | 补 `afterSalesNo`、`type`、`startDate`、`endDate` 等 query 参数，`QueryAsync` 增加对应过滤 |
| 分页+多维度筛选 | 分页+状态筛选 | 缺少售后单号、订单号、售后类型、申请时间筛选 | [after-sales-list.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-list.md) | [AfterSalesController.cs#L156](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L156) | 补 `afterSalesNo`、`orderId`、`type`、`startDate`、`endDate` 等 query 参数，`GetBySellerAsync` 增加对应过滤 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> product-reviews 页面的标签筛选（有图/好评/中评/差评/有追评）与 my-after-sales 页面的状态筛选已由 design-prompts 明确说明走前端过滤，不计入能力不匹配。

## 5. 拆分过渡说明

> BC6 处于拆分过渡期：旧 BC=ReviewAfterSales，新 BC=AfterSales 独立。当前新 BC 目录 `src/Services/AfterSales/` 尚未建立，所有端点仍由旧 BC `src/Services/ReviewAfterSales/` 承载。

- **旧 BC 与新 BC 对照**：

| 旧 BC（ReviewAfterSales）目录/控制器 | 新 BC（AfterSales）规划目录/控制器 | 当前状态 | 端点数 |
|-|-|-|-|
| src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs | 保留在 ReviewAfterSales（评价不迁移） | ✅ 旧 BC 承载 | 9 |
| src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs | src/Services/AfterSales/Leno.AfterSales.Api/Controllers/AfterSalesController.cs（规划） | 🚧 旧 BC 承载、待迁移 | 13 |
| src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewControllerBase.cs | 拆分为 ReviewControllerBase（留旧）+ AfterSalesControllerBase（新建） | 🚧 待拆分 | — |
| src/Services/AfterSales/（新 BC 目录） | — | 🚧 未建立 | 0 |

- **双轨期端点引用规范**：
  1. **评价类端点**（路径含 `/api/reviews`、`/api/products/{spuId}/reviews`、`/api/admin/reviews`、`/api/seller/reviews`）双轨期继续引用旧 BC `src/Services/ReviewAfterSales/`，不参与迁移。
  2. **售后类端点**（路径含 `/api/after-sales`、`/api/seller/after-sales`、`/api/admin/after-sales`）双轨期仍引用旧 BC `AfterSalesController.cs`，但所有调用方需在文档与接口契约中标注「（AfterSales，待切换）」，便于后续切换识别。
  3. 新 BC `src/Services/AfterSales/` 建立后，售后端点路由保持不变（路径前缀仍为 `/api/after-sales`、`/api/seller/after-sales`、`/api/admin/after-sales`），仅服务实现迁移；前端与运营/卖家后台无需调整调用路径。
  4. 切换前需保证 `IAfterSalesAppService`、`AfterSales` 聚合根、`IAfterSalesRepository` 等领域资产完整迁移至新 BC，旧 BC 暂保留只读副本直至切换完成。
  5. 集成事件 `RefundRequestedIntegrationEvent`、`RefundCompletedEvent`、`AfterSalesRequestedEvent` 等售后相关事件的发布方随新 BC 迁移，消费方（BC4/BC5/BC7/BC8/BC9）订阅契约不变。

- **待切换端点清单**（共 13 个，全部位于 AfterSalesController.cs，标 🚧 待切换）：

| HTTP 方法 | 路径 | Controller:行号 | 用途 | 切换后归属 |
|-|-|-|-|-|
| POST | /api/after-sales | [AfterSalesController.cs#L45](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L45) | 买家提交售后申请 | AfterSales |
| POST | /api/after-sales/{id}/return-goods | [AfterSalesController.cs#L56](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L56) | 买家退货填写物流单号 | AfterSales |
| POST | /api/after-sales/{id}/cancel | [AfterSalesController.cs#L67](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L67) | 买家撤销售后申请 | AfterSales |
| GET | /api/after-sales/order/{orderId} | [AfterSalesController.cs#L78](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L78) | 买家按订单查询售后单 | AfterSales |
| GET | /api/after-sales/mine | [AfterSalesController.cs#L89](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L89) | 买家我的售后单 | AfterSales |
| POST | /api/after-sales/images | [AfterSalesController.cs#L100](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L100) | 买家上传售后凭证图片 | AfterSales |
| GET | /api/seller/after-sales | [AfterSalesController.cs#L156](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L156) | 卖家查询收到的售后单 | AfterSales |
| POST | /api/seller/after-sales/{id}/approve | [AfterSalesController.cs#L171](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L171) | 卖家审核同意售后 | AfterSales |
| POST | /api/seller/after-sales/{id}/reject | [AfterSalesController.cs#L182](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L182) | 卖家驳回售后 | AfterSales |
| POST | /api/seller/after-sales/{id}/confirm-return | [AfterSalesController.cs#L193](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L193) | 卖家确认收到退货 | AfterSales |
| POST | /api/admin/after-sales/{id}/approve | [AfterSalesController.cs#L206](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L206) | 运营审核通过售后 | AfterSales |
| POST | /api/admin/after-sales/{id}/reject | [AfterSalesController.cs#L217](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L217) | 运营驳回售后 | AfterSales |
| GET | /api/admin/after-sales | [AfterSalesController.cs#L228](file:///e:/Leno/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L228) | 运营分页查询全平台售后单 | AfterSales |

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | GET /api/seller/after-sales/{id}（卖家售后详情，阻塞卖家审核流程） | — | — | — |
| P1 | POST /api/reviews/{reviewId}/append；GET /api/seller/reviews | — | POST /api/orders/{orderId}/reviews ↔ /api/reviews | GET /api/reviews/mine（缺 status 筛选）；GET /api/admin/reviews（缺多维度筛选）；GET /api/admin/after-sales（缺多维度筛选）；GET /api/seller/after-sales（缺多维度筛选） |
| P2 | — | GET /api/reviews/order-line/{orderLineId}；POST /api/reviews/images | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖

> 来源：docs/spec/06-评价与售后域.md 第 1.3、1.4 节与第 3 章领域事件清单

- **上游依赖**（本 BC 依赖哪些 BC 的端点/事件）：
  - **BC4 订单域**：消费 `OrderCompletedEvent` 开放评价入口；售后申请与退款金额上限校验依赖订单域提供的订单与订单行实付金额快照查询
  - **BC8 支付集成域**：消费 `RefundSucceededIntegrationEvent` 触发 `AfterSales.ConfirmRefund` 完成退款事实记录；消费 `RefundFailedIntegrationEvent` 重试或转人工
  - **BC1 用户域**：JWT 中携带用户 ID 与角色声明供本域鉴权（评价/售后归属与权限校验）

- **下游依赖**（哪些 BC 依赖本 BC 的端点/事件）：
  - **BC2 商品域**：消费 `ReviewSubmittedEvent`、`ReviewAppendedEvent`、`ReviewHiddenEvent` 回写商品评分摘要（score、reviewCount、好评率）
  - **BC4 订单域**：消费 `RefundCompletedEvent` 回滚对应订单行销量
  - **BC5 促销域**：消费 `RefundCompletedEvent` 按 `couponRefundRequired` 标识退还该订单已核销的优惠券（恢复券使用量与库存）
  - **BC7 积分与会员域**：消费 `ReviewApprovedEvent` 发放评价积分；消费 `RefundCompletedEvent` 扣回已发放的消费积分
  - **BC8 支付集成域**：消费 `RefundRequestedIntegrationEvent` 执行实际退款（对接微信/支付宝）
  - **BC9 消息通知域**：售后状态变更、评价审核结果等通知统一经 `INotificationService` 发送

- **集成事件订阅/发布清单**：

| 方向 | 事件名 | 对端 BC | 触发/消费时机 |
|-|-|-|-|
| 入站（订阅） | `OrderCompletedEvent` | BC4 订单域 | 订单完成 → 开放评价入口 |
| 入站（订阅） | `RefundSucceededIntegrationEvent` | BC8 支付集成域 | 退款成功 → `AfterSales.ConfirmRefund` 流转至 Refunded |
| 入站（订阅） | `RefundFailedIntegrationEvent` | BC8 支付集成域 | 退款失败 → 重试或转人工 |
| 出站（发布） | `ReviewSubmittedEvent` | BC2 商品域 | 评价提交 → 回写评分摘要 |
| 出站（发布） | `ReviewApprovedEvent` | BC7 积分与会员域、BC9 消息通知域 | 评价审核通过 → 发评价积分、通知买家 |
| 出站（发布） | `ReviewRejectedEvent` | BC9 消息通知域 | 评价驳回 → 通知买家 |
| 出站（发布） | `ReviewAppendedEvent` | BC2 商品域 | 买家追评 → 更新评价数 |
| 出站（发布） | `ReviewRepliedEvent` | BC9 消息通知域 | 卖家回复 → 通知买家 |
| 出站（发布） | `ReviewHiddenEvent` | BC2 商品域、BC9 消息通知域 | 运营隐藏 → 重算评分摘要 |
| 出站（发布） | `AfterSalesRequestedEvent` | BC9 消息通知域 | 售后申请提交 → 通知卖家/运营 |
| 出站（发布） | `AfterSalesApprovedEvent` | BC9 消息通知域 | 售后同意 → 通知买家退货/退款 |
| 出站（发布） | `AfterSalesRejectedEvent` | BC9 消息通知域 | 售后驳回 → 通知买家 |
| 出站（发布） | `ReturnShippedEvent` | BC9 消息通知域 | 买家发货退货 → 卖家收货提醒 |
| 出站（发布） | `RefundRequestedIntegrationEvent` | BC8 支付集成域 | 售后退款审核通过 → 请求执行退款 |
| 出站（发布） | `RefundCompletedEvent` | BC4 订单域、BC5 促销域、BC7 积分与会员域、BC9 消息通知域 | 退款完成 → 回滚销量、退还优惠券、扣回积分、通知 |
| 出站（发布） | `AfterSalesCompletedEvent` | BC9 消息通知域 | 售后完成 → 通知、统计 |
| 出站（发布） | `AfterSalesCancelledEvent` | BC9 消息通知域 | 买家撤销 → 通知卖家 |

## 8. 行动建议

- **立即修复**（P0 缺失/不一致）：
  - 在 `AfterSalesController` 新增 `GET /api/seller/after-sales/{id:guid}` 端点，复用 `IAfterSalesAppService` 现有聚合根加载逻辑，新增按 `afterSalesId` + `sellerId` 归属校验的查询方法，返回 `AfterSalesDto`。该端点阻塞 seller 端售后详情页（after-sales-detail）的「查看详情」核心流程，且页面已标 ➕ 待补充。

- **短期补充**（P1 缺失/不匹配）：
  - 在 `ReviewsController` 新增 `POST /api/reviews/{id:guid}/append` 端点，调用 `Review.AppendAdditionalReview` 聚合方法；同步修正 my-reviews 页面实现状态标注（页面整体标 ✅ 但 append 端点未实现）。
  - 在 `ReviewsController` 新增 `GET /api/seller/reviews` 端点，新增 `IReviewAppService.GetBySellerAsync(sellerId, rating?, replied?, productName?, startDate?, endDate?, page, pageSize)`，通过 Review 聚合关联 SPU 的 sellerId 过滤，仅返回 `status=Approved`。
  - 将源码 `POST /api/reviews` 路由调整为 `POST /api/orders/{orderId:guid}/reviews`，与 spec 第 5 章 API 设计及 design-prompts review-submit 期望路径对齐；orderId 从路径参数读取。
  - `GET /api/reviews/mine` 补 `status` 查询参数；`GET /api/admin/reviews` 补 `productName`、`rating`、`startDate`、`endDate`；`GET /api/admin/after-sales` 补 `afterSalesNo`、`type`、`startDate`、`endDate`；`GET /api/seller/after-sales` 补 `afterSalesNo`、`orderId`、`type`、`startDate`、`endDate`。

- **长期规划**（P2 闲置/废弃）：
  - `GET /api/reviews/order-line/{orderLineId}`：作为 `POST /api/reviews` 的 `CreatedAtAction` 路由依赖保留；评估是否在 review-submit 提交成功跳转场景补调用，或保留为内部路由。
  - `POST /api/reviews/images`：design-prompts review-submit 页面应显式补调用此端点，与 after-sales-apply 的 `POST /api/after-sales/images` 形成对称。
  - 拆分过渡：择期建立 `src/Services/AfterSales/` 新 BC 目录，将 `AfterSalesController` 13 个端点连同 `AfterSales` 聚合根、`IAfterSalesAppService`、`IAfterSalesRepository` 整体迁移；迁移期间旧 BC 保留只读副本，路由前缀保持不变。

- **文档同步**（design-prompts API 引用对齐到源码）：
  - review-submit.md「3. 数据模型与 API 对接」段补列 `POST /api/reviews/images` 作为图片上传端点（与 after-sales-apply 对齐）。
  - my-reviews.md 实现状态从 ✅ 调整为 🚧（直至 append 端点补齐），或在「3. 数据模型与 API 对接」段标注 append 端点为 🚧。
  - seller/06-after-sales/after-sales-detail.md 与 seller/07-review/review-reply.md 已正确标 ➕，待端点补齐后回写为 ✅。
