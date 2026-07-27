# 商家管理后台总览

## 1. 端定位与角色画像
- **目标用户**：卖家（Seller）。已通过平台入驻审核、持有正常态店铺的经营主体，典型场景为发布商品、处理订单与售后、回复评价、查看经营数据。技能水平中等，熟悉电商后台基础操作，但对复杂表单与状态机需清晰引导。
- **核心目标**：让卖家在一个后台高效完成店铺经营全链路——从入驻、商品管理、订单履约、售后处理到经营分析，降低操作成本，提升履约效率。
- **使用频率**：中低频写。订单履约与售后为中频日常操作（每日多次），商品发布与店铺设置为低频操作（每周数次），入驻申请为一次性操作。
- **设备特征**：桌面 1440+，不支持移动端。最小支持宽度 1200px，992-1199px Sider 自动折叠，<992px 不适配。

## 2. 信息架构与导航
- **一级菜单**（按业务域组织，深色 Sider `#001529`）：
  1. 工作台（Dashboard）
  2. 商品管理（Product Management）
  3. 物流管理（Logistics）
  4. 订单履约（Order Fulfillment）
  5. 售后处理（After-Sales）
  6. 评价管理（Review）
  7. 店铺设置（Shop Settings，含入驻与资质）
  8. 报表导出（Export）
  9. 个人中心（Account）
- **二级菜单**：
  - 工作台：[经营概览](./02-dashboard/overview.md)、[销售趋势](./02-dashboard/sales-trend.md)、[库存预警](./02-dashboard/low-stock-alert.md)
  - 商品管理：[商品列表](./03-product-management/product-list.md)、[商品编辑](./03-product-management/product-edit.md)、[SKU 管理](./03-product-management/sku-management.md)、[价格历史](./03-product-management/price-history.md)
  - 物流管理：[运费模板](./04-logistics/freight-templates.md)、[物流公司查询](./04-logistics/logistics-companies.md)
  - 订单履约：[待发货订单](./05-order-fulfillment/pending-shipment.md)、[全部订单](./05-order-fulfillment/order-list.md)、[物流轨迹](./05-order-fulfillment/logistics-trace.md)
  - 售后处理：[售后列表](./06-after-sales/after-sales-list.md)、[售后详情](./06-after-sales/after-sales-detail.md)
  - 评价管理：[评价回复](./07-review/review-reply.md)
  - 店铺设置：[入驻申请](./01-onboarding/application.md)、[店铺资料](./01-onboarding/shop-profile.md)、[资质管理](./01-onboarding/qualifications.md)、[店铺前台预览](./01-onboarding/shop-preview.md)
  - 报表导出：[销售报表导出](./09-export/sales-export.md)
  - 个人中心：[登录](./08-account/login.md)、[个人资料](./08-account/profile.md)、[通知中心](./08-account/notifications.md)
- **菜单组织原则**：按业务域分组，高频日常操作（工作台、订单、售后）前置，低频配置（店铺设置、报表）后置。
- **快捷入口**：Header 顶部提供「待发货 N 单」「售后待处理 N 单」红色 Badge 提醒，点击直达对应待办列表；工作台首页提供快捷操作卡片。

## 3. 页面路由规划
- **路由表**（节选，命名 `{module}.{page}`）：

| path | component | 鉴权 |
|-|-|-|
| `/login` | `views/account/Login.vue` | 公开 |
| `/dashboard/overview` | `views/dashboard/Overview.vue` | Seller |
| `/dashboard/sales-trend` | `views/dashboard/SalesTrend.vue` | Seller |
| `/dashboard/low-stock` | `views/dashboard/LowStockAlert.vue` | Seller |
| `/products` | `views/product/ProductList.vue` | Seller |
| `/products/new` | `views/product/ProductEdit.vue` | Seller |
| `/products/:id/edit` | `views/product/ProductEdit.vue` | Seller |
| `/products/:id/skus` | `views/product/SkuManagement.vue` | Seller |
| `/products/:id/price-history` | `views/product/PriceHistory.vue` | Seller |
| `/logistics/freight-templates` | `views/logistics/FreightTemplates.vue` | Seller |
| `/logistics/companies` | `views/logistics/LogisticsCompanies.vue` | Seller |
| `/orders/pending-shipment` | `views/order/PendingShipment.vue` | Seller |
| `/orders` | `views/order/OrderList.vue` | Seller |
| `/orders/:id/trace` | `views/order/LogisticsTrace.vue` | Seller |
| `/after-sales` | `views/aftersales/AfterSalesList.vue` | Seller |
| `/after-sales/:id` | `views/aftersales/AfterSalesDetail.vue` | Seller |
| `/reviews` | `views/review/ReviewReply.vue` | Seller |
| `/shop/application` | `views/shop/Application.vue` | Seller |
| `/shop/profile` | `views/shop/ShopProfile.vue` | Seller |
| `/shop/qualifications` | `views/shop/Qualifications.vue` | Seller |
| `/shop/preview` | `views/shop/ShopPreview.vue` | Seller |
| `/export/sales` | `views/export/SalesExport.vue` | Seller |
| `/account/profile` | `views/account/Profile.vue` | Seller |
| `/account/notifications` | `views/account/Notifications.vue` | Seller |

- **路由守卫**：`beforeEach` 校验登录态（无 token 跳 `/login`）、角色为 Seller（非卖家跳对应端首页）、店铺状态（暂停态禁止上架商品但允许履约既有订单）、菜单动态加载。

## 4. 全局布局
- **布局结构**：Ant Design Vue `BasicLayout` —— 顶部 Header（64px，固定）+ 左侧 Sider（200px，可折叠至 80px，深色 `#001529`）+ 主内容区（24 栅格，padding 24px）。底部无 Footer。
- **全局组件**：
  - Header 左侧：Logo + 当前店铺名称；中部：`<a-breadcrumb>` 面包屑；右侧：通知铃铛（`<a-badge :count="unread" />`）+ 用户头像下拉菜单（个人中心、修改密码、切换主题、退出登录）。
  - Sider：`<a-menu mode="inline" theme="dark">`，一级图标 + 二级文字，激活态主色 `#1677FF`。
  - 主题切换：Header 用户菜单预留「切换主题」项，通过 `<a-config-provider :theme="{ algorithm: theme.darkAlgorithm }">` 切换暗色（本次不实现暗色样式，仅预留入口）。
  - 请求层：axios 封装 `request.ts`，请求拦截器注入 `Authorization: Bearer {token}`、`Idempotency-Key`（POST/PUT/DELETE）、`X-Trace-Id`；响应拦截器统一处理 401（跳登录）、403（`message.error('无权限访问')`）、500（`message.error('服务异常')`）。

## 5. 设计风格基调
- **整体气质**：简洁现代，中低频写。强调数据密度与操作效率，避免装饰性元素，以表格、表单、卡片为主要信息载体。
- **与共享设计系统的关系**：完全遵循 `shared/design-system.md`。主色 `#1677FF`、圆角 `6px`/`8px`、间距 4/8/12/16/24/32/48 体系、字体 PingFang SC 优先。组件库统一 Ant Design Vue 4.x（`a-` 前缀），不使用移动端组件库。差异点：无（完全遵循共享设计系统）。

## 6. 模块清单

| 模块 | 页面数 | 实现状态分布 | 优先级 |
|-|-|-|-|
| 01-onboarding（入驻与店铺） | 4 | ✅×3 / 🚧×1 | P1 |
| 02-dashboard（工作台） | 3 | ✅×2 / ➕×1 | P0 |
| 03-product-management（商品管理） | 4 | ✅×4 | P0 |
| 04-logistics（物流管理） | 2 | ✅×1 / ➕×1 | P1 |
| 05-order-fulfillment（订单履约） | 3 | ✅×1 / ➕×2 | P0 |
| 06-after-sales（售后处理） | 2 | ✅×1 / ➕×1 | P0 |
| 07-review（评价管理） | 1 | ➕×1 | P1 |
| 08-account（个人账号） | 3 | ✅×3 | P1 |
| 09-export（报表导出） | 1 | ➕×1 | P2 |
| **合计** | **23** | ✅×15 / 🚧×1 / ➕×7 | — |

**说明**：P0 为日常经营必用模块（工作台、商品、订单、售后），P1 为配置与治理模块（店铺、物流、评价、账号），P2 为增强功能（报表导出）。入驻模块虽为 P1，但对新卖家是入口级必经流程。➕ 补充功能页面集中出现在订单履约、售后、评价、物流、报表模块，主要因后端 Seller 角色端点尚需补充（如卖家订单列表、售后详情、评价列表、报表导出任务等），详见各页面提示词末尾的「补充功能说明」。

## 7. 与后端 API 的对应关系

> **域拆分迁移双轨期说明（2026-07-26 起）**：阶段1-2 已完成，新域已就绪并经网关双轨挂载，端点路径不变，仅服务归属更新。旧域代码保留作回滚兜底，待阶段3观察期结束后下线。详见 `docs/feature-inventory/domain-migration-status.md`。

API 来源：SellerShop BC（入驻申请、店铺信息、资质）+ Product BC 卖家端（商品发布、SKU、价格、提交审核、下架）+ Order BC 卖家端（待发货、订单列表、物流跟踪）+ **AfterSales 域**（卖家售后列表 `/api/seller/after-sales/*`、售后详情与审核操作；旧域 ReviewAfterSales 双轨兜底）+ **Review 域**（卖家评价列表 `/api/seller/reviews`、评价回复 `/api/reviews/{id}/reply`；旧域 ReviewAfterSales 双轨兜底）+ **Identity 域**（卖家登录 `/api/auth/*`、个人资料 `/api/users/me`、双因子；旧域 UserAuth 双轨兜底）+ Notification 域（通知中心内部协作）。详细端点见各页面提示词「数据与 API」段。

### 7.1 域拆分映射表

| 模块 | 旧域 | 新域 | 备注 |
|-|-|-|-|
| 06-after-sales 售后处理 | ReviewAfterSales | **AfterSales** | `/api/seller/after-sales/*` 由 AfterSales 域 `SellerAfterSalesController` 接管 |
| 07-review 评价管理 | ReviewAfterSales | **Review** | `/api/seller/reviews`、`/api/reviews/{id}/reply` 由 Review 域 `SellerReviewsController` / `ReviewsController` 接管 |
| 08-account 登录 | UserAuth | **Identity** | `/api/auth/*` 由 Identity 域 `AuthController` 接管 |
| 08-account 个人资料 | UserAuth | **Identity** | `/api/users/me`、密码、双因子由 Identity 域 `UsersController` 接管 |
| 08-account 通知中心 | Notification 域 | Notification 域（未迁移） | `/api/notifications/*` 由 Notification 域提供，不涉及域拆分 |
| 01-onboarding 入驻与店铺 | SellerShop BC | SellerShop BC（未迁移） | `/api/seller/applications/*`、`/api/seller/shops/*` 由 SellerShop BC 提供，不涉及域拆分 |
| 02-dashboard 工作台 | 多 BC 聚合 | 多 BC 聚合（含新域） | 卖家工作台聚合 SellerShop / Product / Order / AfterSales / Review 新域统计 |
| 03-product-management 商品管理 | Product BC | Product BC（未迁移） | `/api/seller/products/*` 由 Product BC 提供，不涉及域拆分 |
| 04-logistics 物流管理 | Order BC / SellerShop BC | Order BC / SellerShop BC（未迁移） | 物流公司与运费模板不涉及域拆分 |
| 05-order-fulfillment 订单履约 | Order BC | Order BC（未迁移） | `/api/seller/orders/*` 由 Order BC 提供，不涉及域拆分 |
| 09-export 报表导出 | Order BC / Product BC | Order BC / Product BC（未迁移） | 报表导出涉及 BC 不在域拆分范围 |
