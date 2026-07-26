# BC3 购物车域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC3
- **中文名**：购物车域
- **英文名**：Cart
- **涉及端**：buyer-app
- **涉及页面数**：3 页（来自 feature-list：05-cart 全部 3 页 — cart / checkout-preview / checkout-settle）
- **已实现 API 端点数**：15 个（来自源码 Controller 扫描：CartsController 8 个 + AnonymousCartsController 7 个）
- **差异统计**：缺失 0 / 闲置 7 / 路径不一致 0 / 能力不匹配 0

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| GET | /api/cart | [CartsController.cs#L29](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L29) | 获取当前买家购物车（含实时价格与可售状态） | Buyer |
| POST | /api/cart/items | [CartsController.cs#L38](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L38) | 添加购物车项 | Buyer |
| PUT | /api/cart/items/{skuId} | [CartsController.cs#L47](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L47) | 更新购物车项数量 | Buyer |
| DELETE | /api/cart/items/{skuId} | [CartsController.cs#L56](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L56) | 移除购物车项 | Buyer |
| POST | /api/cart/items/select | [CartsController.cs#L65](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L65) | 批量选中/取消选中购物车项 | Buyer |
| PATCH | /api/cart/selection | [CartsController.cs#L74](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L74) | 全选/取消全选所有有效购物车项 | Buyer |
| POST | /api/cart/preview | [CartsController.cs#L83](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L83) | 结算预览（按卖家分组返回选中项，含价格试算） | Buyer |
| POST | /api/cart/merge | [CartsController.cs#L92](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs#L92) | 登录时合并匿名购物车 | Buyer |
| POST | /api/cart/anonymous | [AnonymousCartsController.cs#L31](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L31) | 创建匿名购物车，返回会话标识与空购物车 | 匿名（限流） |
| GET | /api/cart/anonymous | [AnonymousCartsController.cs#L40](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L40) | 获取匿名购物车（含实时价格与可售状态），sessionId 经 X-Cart-Session 头传递 | 匿名（限流） |
| POST | /api/cart/anonymous/items | [AnonymousCartsController.cs#L50](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L50) | 添加匿名购物车项 | 匿名（限流） |
| PUT | /api/cart/anonymous/items/{skuId} | [AnonymousCartsController.cs#L60](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L60) | 更新匿名购物车项数量 | 匿名（限流） |
| DELETE | /api/cart/anonymous/items/{skuId} | [AnonymousCartsController.cs#L70](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L70) | 移除匿名购物车项 | 匿名（限流） |
| POST | /api/cart/anonymous/items/select | [AnonymousCartsController.cs#L80](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L80) | 批量选中/取消选中匿名购物车项 | 匿名（限流） |
| POST | /api/cart/anonymous/preview | [AnonymousCartsController.cs#L90](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L90) | 匿名购物车结算预览 | 匿名（限流） |

> 来源：grep `src/Services/Cart/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」
> 本 BC 无 Internal*Controller.cs 文件，所有端点均为对外端点；匿名端点（AnonymousCartsController）以 X-Cart-Session 头鉴权并启用 IP 维度限流（10 次/分钟）

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/cart | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 查询购物车（含实时价格、失效标记） | ✅ | Buyer |
| POST | /api/cart/items | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 添加购物车项 | ✅ | Buyer |
| PUT | /api/cart/items/{skuId} | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 修改购物车项数量 | ✅ | Buyer |
| DELETE | /api/cart/items/{skuId} | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 删除购物车项 | ✅ | Buyer |
| POST | /api/cart/items/select | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 批量选中/取消选中 | ✅ | Buyer |
| PATCH | /api/cart/selection | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 全选/取消全选 | ✅ | Buyer |
| POST | /api/cart/merge | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 登录时合并匿名购物车 | ✅ | Buyer |
| POST | /api/cart/preview | [cart.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/cart.md) | 结算预览（按卖家分组） | ✅ | Buyer |
| POST | /api/cart/preview | [checkout-preview.md](file:///e:/Leno/docs/design-prompts/buyer-app/05-cart/checkout-preview.md) | 结算预览（按卖家分组返回选中项） | ✅ | Buyer |

> 来源：design-prompts 的「数据与 API」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> BC3 期望端点去重后共 8 个；checkout-preview.md 中 POST /api/cart/preview 与 cart.md 重复，列出以体现页面引用关系
> checkout-settle.md 中引用的 GET /api/addresses、POST /api/orders/preview、POST /api/orders、POST /api/orders/buy-now、GET /api/coupons/mine、GET /api/points/account 均属跨 BC（BC1/BC4/BC5/BC7），不在本表

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| - | - | - | - | - | - |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> 本 BC 3 个页面均标 ✅ 已实现，所有 BC3 期望端点在源码 CartsController 中均有对应实现，无缺失项

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| POST | /api/cart/anonymous | [AnonymousCartsController.cs#L31](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L31) | 创建匿名购物车 | 保留观察：匿名购物车能力为后端预留，buyer-app 当前强制登录访问购物车页，未来若开放匿名加购可启用 |
| GET | /api/cart/anonymous | [AnonymousCartsController.cs#L40](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L40) | 获取匿名购物车 | 保留观察：同上 |
| POST | /api/cart/anonymous/items | [AnonymousCartsController.cs#L50](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L50) | 添加匿名购物车项 | 保留观察：同上 |
| PUT | /api/cart/anonymous/items/{skuId} | [AnonymousCartsController.cs#L60](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L60) | 更新匿名购物车项数量 | 保留观察：同上 |
| DELETE | /api/cart/anonymous/items/{skuId} | [AnonymousCartsController.cs#L70](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L70) | 移除匿名购物车项 | 保留观察：同上 |
| POST | /api/cart/anonymous/items/select | [AnonymousCartsController.cs#L80](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L80) | 批量选中/取消选中匿名购物车项 | 保留观察：同上 |
| POST | /api/cart/anonymous/preview | [AnonymousCartsController.cs#L90](file:///e:/Leno/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L90) | 匿名购物车结算预览 | 保留观察：同上 |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> buyer-app 的 cart.md 第 7 节明确「未登录跳 /login?redirect=/cart」，购物车页强制登录；design-prompts 中 BC3 全部端点鉴权标注均为 Buyer，未提及匿名调用入口
> 匿名购物车域在 spec/03-购物车域.md FP-01 中规划为 Redis 临时存储能力，源码已完整实现，但 buyer-app 暂未对接匿名使用场景，仅 POST /api/cart/merge 在登录后被动消费匿名购物车数据
> 全部 7 个匿名端点建议保留观察，待 buyer-app 决策是否开放匿名加购流程后再决定启用或废弃

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| - | - | - | - | - |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> design-prompts 的 8 个 BC3 期望端点与源码 CartsController 的 8 个端点在方法与路径上完全一致，无路径或方法不一致
> 备注：spec/03-购物车域.md 第 5 章 API 设计中规定修改数量为 PATCH /api/cart/items/{itemId}、单项选中为 PATCH /api/cart/items/{itemId}/selection，但 design-prompts 与源码均统一采用 PUT /api/cart/items/{skuId} 与 POST /api/cart/items/select（批量），属 spec 与实际实现的演进差异，不在本报告差异范围

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| - | - | - | - | - | - |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> design-prompts 中 cart.md 明确「进入页面调用 GET /api/cart 全量加载；操作后局部更新对应项，避免整页刷新」，期望全量返回；源码 GetCartAsync 返回完整 CartDto，能力一致
> 批量选中：design-prompts 期望 SelectCartItemsDto（skuIds、isSelected）批量操作；源码 SelectItemsAsync 接收 SelectCartItemsDto，能力一致
> 全选：design-prompts 期望 ToggleAllSelectionDto（isSelected）切换所有有效项；源码 ToggleAllSelectionAsync 仅传入 isSelected，失效项不受影响，能力一致
> 结算预览：design-prompts 期望 POST /api/cart/preview 无 body 基于当前选中项；源码 PreviewCheckoutAsync 无入参，能力一致
> 无能力不匹配差异

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | - | - | - | - |
| P1 | - | - | - | - |
| P2 | - | 7 个匿名购物车端点（POST/GET /api/cart/anonymous、POST/PUT/DELETE /api/cart/anonymous/items、POST /api/cart/anonymous/items/select、POST /api/cart/anonymous/preview） | - | - |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强
> 闲置的 7 个匿名端点不影响当前 buyer-app 交易闭环（已通过 POST /api/cart/merge 间接消费匿名数据），归为 P2 保留观察

## 7. 跨 BC 依赖
- **上游依赖**：
  - 商品域（BC2）：购物车通过防腐层 `IProductPricingQueryService.GetCurrentPricesAsync(skuIds)` 实时获取 SKU 现价、可售状态、可售库存；不持有商品聚合对象
  - 促销域（BC5）：通过防腐层 `IPromotionQueryService.GetApplicableDiscountsAsync(context)` 获取适用优惠金额与明细，用于总价计算
- **下游依赖**：
  - 订单域（BC4）：下单时购物车选中项转化为订单行快照（价格快照在订单域下单瞬间固化）；订单创建后通过 `OrderCreatedEvent` 通知购物车域清空已结算项
- **集成事件订阅/发布清单**：
  - 订阅（入站）：
    - `OrderCreatedEvent`（订单域）— 触发 `Cart.ClearCheckedOutItems(checkedOutItemIds)` 清空已结算项
    - `ProductTakenDownEvent`（商品域）— 触发 `Cart.MarkInvalid(skuId, reason)` 标记对应 SKU 失效并取消选中
    - `ProductPublishedEvent`（商品域）— 触发 `Cart.MarkValid(skuId)` 恢复失效项为有效
    - `ProductUpdatedEvent`（商品域）— 触发刷新购物车项展示快照（名称、图片等）
  - 发布（出站）：
    - `CartMergedEvent`（领域事件，可发集成）— 登录合并完成，供分析域消费
  - 内部领域事件（仅在当前上下文内消费）：`CartItemAddedEvent`、`CartItemQuantityChangedEvent`、`CartItemRemovedEvent`、`CartItemSelectionChangedEvent`、`CartItemInvalidatedEvent`、`CartItemsClearedEvent`

## 8. 行动建议
- **立即修复**（P0 缺失/不一致）：无。BC3 期望端点全部已实现，路径与方法完全对齐，无阻塞交易闭环的问题。
- **短期补充**（P1 缺失/不匹配）：无。design-prompts 与源码能力一致，无体验影响项。
- **长期规划**（P2 闲置/废弃）：
  - 7 个匿名购物车端点（AnonymousCartsController）保留观察：建议产品决策 buyer-app 是否开放「未登录加购」流程
    - 若开放：在 buyer-app 增加匿名加购入口与 design-prompts 补充匿名端点调用，并补充 X-Cart-Session 头的前端管理逻辑
    - 若不开放：评估是否废弃 AnonymousCartsController，或保留为后续多端（如小程序、H5 营销页）预留能力
  - 注意：POST /api/cart/merge 依赖匿名购物车数据存在，即使 buyer-app 不直接调用匿名端点，匿名购物车存储能力仍需保留以支撑合并流程
- **文档同步**（design-prompts API 引用对齐到源码）：
  - design-prompts 与源码完全对齐，无需同步调整
  - 可选优化：在 cart.md 第 3 节补充说明「匿名购物车能力由后端 AnonymousCartsController 提供，buyer-app 当前仅通过登录后 POST /api/cart/merge 间接消费」，以提升文档可追溯性
