# BC10 卖家与店铺管理域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC10
- **中文名**：卖家与店铺管理域
- **英文名**：SellerShop
- **涉及端**：buyer-app / operations / seller
- **涉及页面数**：11 页（来自 feature-list：buyer-app 04-shop 1 页 + operations 04-seller-ops 3 页 + seller 01-onboarding 4 页 + seller 02-dashboard 3 页 + seller 09-export 1 页；其中 low-stock-alert 仅引用 BC2 端点不参与 BC10 端点对比）
- **已实现 API 端点数**：17 个（来自源码 Controller 扫描：ShopsController 4 + SellerDashboardController 3 + AdminShopsController 10；本 BC 无 Internal*Controller）
- **差异统计**：缺失 5 / 闲置 0 / 路径不一致 0 / 能力不匹配 3

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| POST | /api/shops/application | [ShopsController.cs#L29](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L29) | 卖家提交入驻申请（创建店铺与卖家档案并置待审核） | 已认证用户（卖家） |
| GET | /api/shops/me | [ShopsController.cs#L38](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L38) | 查询当前卖家的店铺资料 | 已认证用户（卖家） |
| PUT | /api/shops/me | [ShopsController.cs#L47](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L47) | 更新当前卖家的店铺基础信息、Logo 与联系方式 | 已认证用户（卖家） |
| POST | /api/shops/me/qualifications | [ShopsController.cs#L56](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L56) | 卖家上传店铺资质（multipart/form-data，含证照图片文件） | 已认证用户（卖家） |
| GET | /api/seller/dashboard | [SellerDashboardController.cs#L52](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs#L52) | 查询当前卖家工作台概览（店铺信息 + 当日运营指标；支持 ES 读模型回退与双发对比） | 已认证用户（卖家） |
| GET | /api/seller/sales-trend | [SellerDashboardController.cs#L159](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs#L159) | 查询当前卖家店铺的销售趋势（按日序列，from/to DateOnly） | 已认证用户（卖家） |
| GET | /api/seller/metrics | [SellerDashboardController.cs#L172](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs#L172) | 查询当前卖家店铺的运营指标明细（按日 from/to DateOnly） | 已认证用户（卖家） |
| GET | /api/admin/shops | [AdminShopsController.cs#L29](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L29) | 分页查询店铺列表（按状态/关键词/类目过滤） | Admin, Operator |
| GET | /api/admin/shops/{id:guid} | [AdminShopsController.cs#L38](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L38) | 查询店铺详情 | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/approve | [AdminShopsController.cs#L47](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L47) | 审核通过店铺入驻申请 | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/reject | [AdminShopsController.cs#L56](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L56) | 驳回店铺入驻申请（请求体 ActionReasonDto） | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/suspend | [AdminShopsController.cs#L65](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L65) | 暂停店铺营业（请求体 ActionReasonDto） | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/resume | [AdminShopsController.cs#L74](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L74) | 恢复店铺营业 | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/close | [AdminShopsController.cs#L83](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L83) | 关闭店铺（终态，请求体 ActionReasonDto） | Admin, Operator |
| GET | /api/admin/shops/{id:guid}/qualifications | [AdminShopsController.cs#L92](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L92) | 查询店铺资质列表 | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/qualifications/{qualId:guid}/approve | [AdminShopsController.cs#L101](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L101) | 审核通过资质 | Admin, Operator |
| POST | /api/admin/shops/{id:guid}/qualifications/{qualId:guid}/reject | [AdminShopsController.cs#L110](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L110) | 驳回资质（请求体 ActionReasonDto） | Admin, Operator |

> 来源：grep `src/Services/SellerShop/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> 本 BC 无 Internal*Controller.cs 文件，所有端点均为对外公开端点
> 鉴权说明：`ShopsController` 与 `SellerDashboardController` 仅声明 `[Authorize]`（无角色限定），按 `GetCurrentUserId()` 从 JWT 取当前卖家标识；`AdminShopsController` 显式 `[Authorize(Roles = "Admin,Operator")]`

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| POST | /api/shops/application | [application.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/application.md) | 提交入驻申请 | ✅ | Seller |
| GET | /api/shops/me | [application.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/application.md) / [qualifications.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/qualifications.md) / [shop-preview.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/shop-preview.md) / [shop-profile.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/shop-profile.md) | 查询当前卖家店铺资料（前置校验/资质集合/状态/基础信息） | ✅ | Seller |
| PUT | /api/shops/me | [shop-profile.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/shop-profile.md) | 更新店铺基础信息 | ✅ | Seller |
| POST | /api/shops/me/qualifications | [qualifications.md](file:///e:/Leno/docs/design-prompts/seller/01-onboarding/qualifications.md) | 上传店铺资质（含证照图片文件） | ✅ | Seller |
| GET | /api/seller/dashboard | [overview.md](file:///e:/Leno/docs/design-prompts/seller/02-dashboard/overview.md) | 工作台概览（店铺信息 + 当日指标） | ✅ | Seller |
| GET | /api/seller/sales-trend?from=&to= | [overview.md](file:///e:/Leno/docs/design-prompts/seller/02-dashboard/overview.md) / [sales-trend.md](file:///e:/Leno/docs/design-prompts/seller/02-dashboard/sales-trend.md) / [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 销售趋势（按日序列） | ✅ | Seller |
| GET | /api/seller/metrics?from=&to= | [sales-trend.md](file:///e:/Leno/docs/design-prompts/seller/02-dashboard/sales-trend.md) / [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 运营指标明细（按日） | ✅ | Seller |
| GET | /api/admin/shops | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) / [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) / [seller-statistics.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/seller-statistics.md) | 分页查询店铺列表 | ✅ | Admin, Operator |
| GET | /api/admin/shops/{id} | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) / [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 查询店铺详情 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/approve | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) | 审核通过入驻申请 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/reject | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) | 驳回入驻申请 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/suspend | [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 暂停店铺营业 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/resume | [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 恢复店铺营业 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/close | [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 关闭店铺（终态） | ✅ | Admin, Operator |
| GET | /api/admin/shops/{id}/qualifications | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) / [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 查询店铺资质列表 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/qualifications/{qualId}/approve | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) / [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 资质审核通过 | ✅ | Admin, Operator |
| POST | /api/admin/shops/{id}/qualifications/{qualId}/reject | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) / [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | 资质驳回 | ✅ | Admin, Operator |
| GET | /api/admin/seller-statistics/overview | [seller-statistics.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/seller-statistics.md) | 跨店铺聚合卖家总数/活跃数/新增数/平均评分（参数 start/end/category） | 🚧 | Operator, Admin |
| POST | /api/seller/export/sales | [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 创建异步导出任务（CreateExportTaskDto：报表类型/时间范围/维度/格式） | ➕ | Seller |
| GET | /api/seller/export/tasks | [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 分页查询当前卖家的导出任务列表 | ➕ | Seller |
| GET | /api/seller/export/tasks/{id}/download | [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 下载已完成的导出文件 | ➕ | Seller |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 说明：buyer-app/04-shop/shop-detail.md（🚧）页面在 BC10 功能清单范围内，但 design-prompts 仅引用 BC2 端点（GET /api/products/search、GET /api/brands），未明确 BC10 端点路径；按 BC10 需求文档 5.3 应补充 `GET /api/shops/{shopId}` 公开端点，详见 4.1 缺失分析

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| GET | /api/admin/seller-statistics/overview | [seller-statistics.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/seller-statistics.md) | 跨店铺聚合卖家总数/活跃数/新增数/平均评分；当前前端基于 dashboard shop-ranking 与店铺列表二次聚合，存在性能损耗 | P1 | 在 AdminShopsController 或新增 SellerStatisticsController 新增端点，参数 start/end/category；底层数据复用 ShopReadModel 与订单事件读模型聚合 |
| POST | /api/seller/export/sales | [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 大数据量销售报表导出（异步任务）；当前 ≤90 天由前端直接调用 sales-trend/metrics 生成文件，>90 天或异常降级场景无后端兜底 | P1 | 在 SellerDashboardController 或新增 SellerExportController 新增端点，接收 CreateExportTaskDto，后台 Job 调用 ISellerDashboardAppService 拉数据生成 Excel/CSV 上传文件存储 |
| GET | /api/seller/export/tasks | [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 查询当前卖家导出任务列表（分页、按 createdAt 倒序、status 可选过滤） | P1 | 同上，新增 ExportTask 仓储与查询端点，按 SellerId 强制过滤 |
| GET | /api/seller/export/tasks/{id}/download | [sales-export.md](file:///e:/Leno/docs/design-prompts/seller/09-export/sales-export.md) | 下载已完成的导出文件（校验任务归属与状态，返回文件流） | P1 | 同上，新增下载端点，复用 IFileStorageService 读取文件流 |
| GET | /api/shops/{shopId} | [shop-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/04-shop/shop-detail.md) | 买家端店铺公开详情（店铺名称/Logo/描述/客服/评分/资质状态）；design-prompts 标 🚧 未明确 BC10 端点路径，仅引用 BC2 商品搜索；按 BC10 需求文档 5.3 应公开此端点 | P1 | 在 ShopsController 或新增 ShopViewController 新增公开端点，去除 [Authorize] 或单独 [AllowAnonymous]；从 ShopReadModel 加载公开字段（客服电话脱敏） |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|

> 说明：源码 17 个端点全部在 design-prompts 中至少一处被引用，无闲置端点

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|

> 说明：design-prompts 中明确引用的 BC10 端点与源码 Controller 在 HTTP 方法与路径上完全匹配，无不一致项
> 备注：BC10 需求文档 5.1 中规划的卖家端路径为 `/api/seller/applications`、`/api/seller/shop`、`/api/seller/shop/qualifications`、`/api/seller/shop/overview`，与源码实际采用的 `/api/shops/application`、`/api/shops/me`、`/api/shops/me/qualifications`、`/api/seller/dashboard` 存在路径差异；但 design-prompts 已与源码实际路径对齐，故不计入本节差异，建议后续修订需求文档以避免混淆

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 批量审核通过/驳回（勾选多行后串行/并行调用单条端点或调用专用批量端点） | 仅提供单条审核端点（approve/reject） | 缺少专用批量端点；前端需循环调用单条端点，效率低且无法保证原子性 | [application-audit.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/application-audit.md) | [AdminShopsController.cs#L47](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L47) / [AdminShopsController.cs#L56](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L56) | 新增 `POST /api/admin/shops/batch-approve` 与 `POST /api/admin/shops/batch-reject`，接收店铺 ID 数组与原因，返回逐条结果 |
| 统计概览（已通过/已暂停/已关闭计数）独立聚合 | `GET /api/admin/shops` 仅返回分页列表与 total | 缺少按状态分组的计数端点；前端需多次调用列表接口聚合，性能差 | [shop-governance.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/shop-governance.md) | [AdminShopsController.cs#L29](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L29) | 新增 `GET /api/admin/shops/statistics/summary` 返回各状态计数；或在分页响应中附带 statusCounts 字段 |
| Top 10 卖家 GMV 排序 + 类目分布聚合 | `GET /api/admin/shops` 仅支持关键词/状态/类目过滤与分页，未支持 GMV 排序与类目分布聚合 | 缺少 GMV 排序参数与类目分布字段 | [seller-statistics.md](file:///e:/Leno/docs/design-prompts/operations/04-seller-ops/seller-statistics.md) | [AdminShopsController.cs#L29](file:///e:/Leno/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/AdminShopsController.cs#L29) | 与 4.1 缺失端点 `/api/admin/seller-statistics/overview` 一并实现，由聚合端点返回 Top 10 与类目分布；现有列表端点保持只读列表语义 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | — | — | — | — |
| P1 | GET /api/admin/seller-statistics/overview；POST /api/seller/export/sales；GET /api/seller/export/tasks；GET /api/seller/export/tasks/{id}/download；GET /api/shops/{shopId} | — | — | 批量审核端点（application-audit）；状态计数概览端点（shop-governance）；GMV 排序与类目分布（seller-statistics） |
| P2 | — | — | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强
> 说明：BC10 现有 17 个端点已覆盖入驻申请、店铺资料、资质、运营治理与工作台核心闭环；缺失端点均为统计/导出/买家端公开详情等增强能力，不影响店铺主体生命周期管理，故未列入 P0

## 7. 跨 BC 依赖

- **上游依赖**：
  - BC1 用户域：卖家账号身份（`SellerApplication.ApplicantUserId` 与 `Shop.SellerId` 均引用用户域 UserId）；申请人角色合法性、登录态由 BC1 提供
- **下游依赖**：
  - BC2 商品域：商品域 `Product.SellerId` 在语义上指代本域 `ShopId`；本域发布 `ShopSuspendedEvent`/`ShopResumedEvent`/`ShopClosedEvent`/`ShopInfoUpdatedEvent` 由商品域消费，置店铺商品不可售/恢复/下架/同步名称展示
  - BC4 订单域：订单域 `Order.SellerId` 同样指代本域 `ShopId`；本域发布 `ShopSuspendedEvent`/`ShopClosedEvent` 由订单域消费，拒绝新单/停止新单
  - BC9 消息通知域：本域入驻审核结果、店铺状态变更、资质过期提醒通过 BC9 向卖家发送邮件/短信通知
  - buyer-app/operations/seller 三端：消费本域 17 个对外端点
- **集成事件订阅/发布清单**：
  - 发布（出站）：
    - `SellerApplicationSubmittedEvent`（提交入驻申请）
    - `SellerApplicationApprovedEvent`（审核通过，驱动创建店铺）
    - `SellerApplicationRejectedEvent`（驳回）
    - `ShopCreatedEvent`（入驻通过创建店铺）
    - `ShopInfoUpdatedEvent`（卖家更新店铺基础信息）
    - `ShopQualificationUpdatedEvent`（卖家更新资质）
    - `ShopSuspendedEvent`（运营暂停店铺）
    - `ShopResumedEvent`（运营恢复店铺）
    - `ShopClosedEvent`（运营关闭店铺）
  - 订阅（入站）：
    - 订单域：`OrderCreatedEvent`、`OrderPaidEvent`、`OrderCompletedEvent`、`OrderCancelledEvent`（维护店铺订单数与销售额概览读模型）
    - 商品域：`ProductPublishedEvent`、`ProductTakenDownEvent`（维护店铺商品数概览读模型）

## 8. 行动建议

- **立即修复**（P0 缺失/不一致）：
  - 无 P0 项；BC10 已实现端点覆盖店铺主体生命周期闭环，无阻塞性问题

- **短期补充**（P1 缺失/不匹配）：
  - 补充买家端店铺公开详情端点 `GET /api/shops/{shopId}`（无鉴权或 AllowAnonymous），支撑 buyer-app/04-shop/shop-detail 页面与 seller/01-onboarding/shop-preview 页面预览；从 ShopReadModel 加载，客服电话脱敏
  - 补充卖家统计聚合端点 `GET /api/admin/seller-statistics/overview`，支撑 operations/04-seller-ops/seller-statistics 页面，返回卖家总数/活跃数/新增数/平均评分/Top 10 GMV 排行/类目分布
  - 补充销售报表导出三端点 `POST /api/seller/export/sales`、`GET /api/seller/export/tasks`、`GET /api/seller/export/tasks/{id}/download`，支撑 seller/09-export/sales-export 页面异步导出场景；后台 Job 复用 `ISellerDashboardAppService.GetSalesTrendAsync`/`GetShopMetricsAsync`，使用 EPPlus/CsvHelper 生成文件
  - 补充店铺治理统计概览端点（如 `GET /api/admin/shops/statistics/summary`）或在分页响应中附带 statusCounts，支撑 shop-governance 页面已通过/已暂停/已关闭计数展示
  - 补充批量审核端点 `POST /api/admin/shops/batch-approve` 与 `POST /api/admin/shops/batch-reject`，提升运营审核效率

- **长期规划**（P2 闲置/废弃）：
  - 无闲置端点；持续观察未来需求演进中是否有端点被淘汰

- **文档同步**（design-prompts API 引用对齐到源码）：
  - design-prompts 与源码实际路径已对齐，无需调整 design-prompts 中 BC10 端点引用
  - 建议同步修订 BC10 需求文档（docs/spec/11-卖家与店铺管理域.md §5.1）的卖家端路径表，将规划的 `/api/seller/applications`、`/api/seller/shop`、`/api/seller/shop/qualifications`、`/api/seller/shop/overview` 调整为源码实际路径 `/api/shops/application`、`/api/shops/me`、`/api/shops/me/qualifications`、`/api/seller/dashboard`，避免后续对接混淆
  - 建议在 buyer-app/04-shop/shop-detail.md「3. 数据模型与 API 对接」段补充 BC10 公开端点 `GET /api/shops/{shopId}` 的明确引用（当前仅引用 BC2 商品搜索端点）
