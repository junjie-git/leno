# 卖家管理后台前端设计文档

**文档版本**：V1.0
**创建日期**：2026-07-29
**所属项目**：Leno 电商平台
**文档类型**：前端实现设计 spec
**关联文档**：
- [docs/spec/11-卖家与店铺管理域.md](../../spec/11-卖家与店铺管理域.md) — 后端 BC10 需求文档
- [docs/design-prompts/seller/00-overview.md](../../design-prompts/seller/00-overview.md) — 23 页 UI 提示词总览
- [docs/design-prompts/shared/design-system.md](../../design-prompts/shared/design-system.md) — 共享设计系统
- [docs/design-prompts/shared/components.md](../../design-prompts/shared/components.md) — 共享组件约定
- [docs/designs/seller/](../../designs/seller/) — 23 页 HTML 设计稿
- [docs/designs/_shared/tokens.css](../../designs/_shared/tokens.css) — 设计令牌 CSS
- [docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md](./2026-07-27-system-admin-frontend-design.md) — system-admin 前端设计（架构模板）
- [docs/superpowers/specs/2026-07-29-system-admin-api-frontend-integration-design.md](./2026-07-29-system-admin-api-frontend-integration-design.md) — ApiResponse.code === 200 成功约定来源

## 0 摘要

本 spec 描述 Leno 卖家管理后台前端 SPA 的实现设计。后端卖家相关 BC（SellerShop / Product / Order / ReviewAfterSales / Identity / Notification）已完整实装所有 API 端点；其中 2 个端点缺失（low-stock、exports），1 个端点分页参数约定与全局不一致（Order BC `page` 从 0 起，其余从 1 起）。前端缺失，本 spec 定义其架构、模块、数据流、鉴权、视觉规范、测试与构建。

**交付物**：仅前端 Vue 3 SPA，位于 `web/seller/`，覆盖 23 页 9 模块 + 5 框架页，分 P0→P1→P2 三阶段交付。直连后端 API，账号密码登录（2FA UI 预留）。

**关键决策**：
1. 分 P0→P1→P2 三阶段交付，每阶段独立可发布、可验收
2. 缺失的 2 个端点（low-stock、exports）UI 先上 + mock 兜底 +「后端未就绪」徽标
3. 后端服务对接 ReviewAfterSales 合并版 + Identity 服务
4. 代码共享策略选方案 A — 完全独立 SPA，从 system-admin 复制 `shared/` 作为基线后独立演化
5. `ApiResponse.code === 200` 视为成功（与 system-admin 2026-07-29 决策一致）
6. Order BC 分页参数 `page` 后端待统一为 1 起（BE-1）；**前端不做转换**，直接传后端约定的 `page=0`（首页），并在 `order.api.ts` 与路由表中加 `// TODO(backend): BE-1 待统一 page 从 1 起` 标注。调用方与表格组件按"后端实际语义"使用，待后端统一后无需改前端调用代码，仅需移除 TODO 注释
7. 权限从后端获取，与 system-admin 一致（`permissions[]` 驱动菜单/按钮可见性）

## 1 总体架构与项目骨架

### 1.1 技术栈与版本（严格遵循 shared/design-system.md §1，与 system-admin 一致）

| 维度 | 选型 | 版本 |
|---|---|---|
| 框架 | Vue 3 SFC + `<script setup>` + Composition API | 3.5.x |
| 语言 | TypeScript strict | 5.x |
| 构建 | Vite | 6.x |
| UI 库 | Ant Design Vue | 4.x |
| 状态 | Pinia + pinia-plugin-persistedstate | 2.x |
| 路由 | Vue Router | 4.x |
| 图表 | @vue-echarts + echarts | 7.x / 5.5 |
| HTTP | axios | 1.7.x |
| 工具 | dayjs、lodash-es、@ant-design/icons-vue | 最新稳定 |
| 包管理 | pnpm | 9.x |
| Node | ≥ 20 LTS | — |
| 测试 | Vitest 2.x + @vue/test-utils 2.x + jsdom + Playwright 1.x（E2E 可选） | — |

### 1.2 目录骨架（`web/seller/`，结构与 system-admin 对齐）

```
web/seller/
├── public/
├── src/
│   ├── main.ts                      # 入口：createApp + 注册插件
│   ├── App.vue                      # 根组件 <RouterView>
│   ├── app/
│   │   ├── router.ts                # 聚合各模块 routes.ts + 守卫
│   │   ├── pinia.ts                 # createPinia + 持久化插件
│   │   ├── provider.vue             # 全局 ConfigProvider（主色/圆角/字体）
│   │   └── env.ts                   # import.meta.env 类型化封装
│   ├── shared/                      # 从 system-admin 复制为基线，独立演化
│   │   ├── http/                    # client.ts、errors.ts、idempotency.ts、mock/
│   │   ├── auth/                    # useAuthStore、AuthGuard、permission helper（Seller 角色）
│   │   ├── shop/                    # useShopStore（ShopId/店铺状态门禁，新增）
│   │   ├── layout/                  # BasicLayout + SiderMenu + HeaderBar（含待办徽标）
│   │   ├── components/              # StatusTag、IdempotencyButton、ConfirmDialog、DataTable、EmptyState、ChartLine/Bar/Pie、DashboardCard 等
│   │   ├── tokens/                  # design-tokens.css（来自 designs/_shared/tokens.css）+ antd theme.ts
│   │   ├── utils/                   # format（日期/金额/百分比）、validators、logger
│   │   └── types/                   # ApiResponse<T>、PageResult<T>、ErrorBody 等通用类型
│   ├── modules/
│   │   ├── 01-onboarding/           # P1 4页
│   │   ├── 02-dashboard/            # P0 3页
│   │   ├── 03-product-management/   # P0 4页
│   │   ├── 04-logistics/            # P1 2页
│   │   ├── 05-order-fulfillment/    # P0 3页
│   │   ├── 06-after-sales/          # P0 2页
│   │   ├── 07-review/               # P1 1页
│   │   ├── 08-account/              # P1 3页
│   │   └── 09-export/               # P2 1页
│   └── assets/                      # 图标、字体
├── tests/                           # Vitest setup + Playwright e2e
├── index.html
├── vite.config.ts                   # proxy /api → 后端，端口 5174
├── tsconfig.json                    # strict、paths 别名 @/
├── package.json
├── pnpm-lock.yaml
├── .env.development                 # VITE_API_BASE、VITE_USE_MOCK=true、VITE_REQUIRE_2FA=false
├── .env.production
└── playwright.config.ts
```

每个 `modules/NN-name/` 内部统一为：
```
NN-name/
├── views/          # .vue 页面（命名与 design-prompts 文件名 PascalCase 化）
├── api/            # {module}.api.ts —— 按 design-prompts §3 API 表实现
├── stores/         # {module}.store.ts —— 仅跨页面共享状态才建
├── types/          # {module}.dto.ts —— 请求/响应 DTO 与枚举
├── routes.ts       # 本模块路由项数组，meta={title,roles,permission,menuKey,icon,menuGroup,requiresActiveShop}
└── index.ts        # 导出 routes、api、store，供 app/router.ts 聚合
```

### 1.3 启动流程

```
main.ts
  ├─ createApp(App)
  ├─ app.use(createPinia())        // 注册持久化插件（localStorage）
  ├─ app.use(router)                // 路由表来自 app/router.ts 聚合各模块
  ├─ app.use(Antd)                  // Ant Design Vue 全量注册
  ├─ app.component('ECharts', EChartsVue)  // 全局图表
  └─ app.mount('#app')

App.vue
  └─ <AConfigProvider :theme="theme" :locale="zhCN">
       <RouterView />
     </AConfigProvider>
```

### 1.4 Vite 代理

`vite.config.ts` 中：
```ts
server: {
  port: 5174,                        // 与 system-admin(5173) 错开，便于并行开发
  proxy: {
    '/api': {
      target: 'http://localhost:5001',   // 后端网关端口
      changeOrigin: true,
      // 后端若 HTTPS 自签：secure: false
    },
  },
}
```

`shared/http/client.ts` 中 `axios.create({ baseURL: '/api' })`，请求时只写相对路径。

### 1.5 模块路由聚合

`app/router.ts` 静态导入 9 个模块的 `routes.ts`，concat 后挂上 `beforeEach` 守卫：

```ts
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
// P1
import onboarding from '@/modules/01-onboarding/routes'
import logistics from '@/modules/04-logistics/routes'
import review from '@/modules/07-review/routes'
import account from '@/modules/08-account/routes'
// P2
import exportMod from '@/modules/09-export/routes'

const routes = [
  { path: '/login', component: Login, meta: { anonymous: true, title: '登录' } },
  { path: '/403', component: Forbidden, meta: { anonymous: true, title: '无权访问' } },
  { path: '/404', component: NotFound, meta: { anonymous: true, title: '页面不存在' } },
  { path: '/', component: BasicLayout, children: [
    { path: '', redirect: '/dashboard/overview' },
    ...dashboard,
    ...product,
    ...order,
    ...afterSales,
    ...onboarding,
    ...logistics,
    ...review,
    ...account,
    ...exportMod,
  ]},
  { path: '/:pathMatch(.*)*', component: NotFound },
]
```

### 1.6 从 system-admin 复制的基线文件清单

以下文件作为基线复制到 `web/seller/src/shared/`，随后按卖家域独立演化：

| 类别 | 文件 | 改动点 |
|---|---|---|
| http | `client.ts`、`errors.ts`、`idempotency.ts`、`index.ts` | 无（`code === 200` 成功判定已对齐） |
| http/mock | `index.ts`（passThrough 兜底） | 保留框架，handler 按卖家域重写 |
| types | `index.ts`（`ApiResponse<T>`、`PageResult<T>`、`ErrorBody`） | 无 |
| tokens | `design-tokens.css`、`antd-theme.ts` | 无（设计系统完全共享） |
| utils | `format.ts`、`validators.ts`、`logger.ts` | 无 |
| components | `StatusTag`、`IdempotencyButton`、`ConfirmDialog`、`DataTable`、`EmptyState`、`ErrorBoundary`、`DateTimeRangePicker`、`JsonViewer`、`PasswordStrengthIndicator`、`charts/ChartLine/ChartBar/ChartPie` | StatusTag 增 `type="product"/"order"/"aftersales"/"shop"/"freightTemplate"` 状态映射 |
| components | `DashboardCard`、`StatisticCard`（从 01-dashboard 复用） | 无 |
| layout | `BasicLayout`、`SiderMenu`、`HeaderBar` | HeaderBar 增店铺名 + 待发货/售后待办徽标；SiderMenu 卖家菜单结构 |
| auth | `auth.store.ts`、`permission.ts`、`PermissionGuard.vue`、`index.ts` | `auth.store.ts` 调整为 Seller 角色；`user` 含 `shopId/shopName/shopStatus` |

**新增（system-admin 无）**：
- `shared/shop/shop.store.ts` — ShopId/店铺状态/资质状态门禁
- `shared/components/TodoBadge.vue` — Header 待办徽标
- `shared/components/ShopStatusGuard.vue` — 店铺状态门禁包装

## 2 模块拆分与页面映射（按 P0/P1/P2 分阶段）

本节把 23 页映射到 9 个 `modules/NN-name/views/*.vue`，明确每页的路由、真实后端 API（路径已据探查报告修正偏差）。**P0→P1→P2 分阶段交付**，每阶段独立可发布。

### 2.1 阶段划分

| 阶段 | 模块 | 页数 | 交付目标 |
|---|---|---|---|
| **P0** | 02-dashboard / 03-product-management / 05-order-fulfillment / 06-after-sales | 12 | 日常经营必用：工作台 3 + 商品 4 + 订单 3 + 售后 2。其中 low-stock 走 mock |
| **P1** | 01-onboarding / 04-logistics / 07-review / 08-account | 10 | 配置与治理：入驻 4 + 物流 2 + 评价 1 + 账号 3 |
| **P2** | 09-export | 1 | 增强功能：报表导出（mock 兜底） |

### 2.2 P0 — 模块 02-dashboard（3 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `02-dashboard/overview.md` | `Overview.vue` | `/dashboard/overview` | `GET /api/seller/dashboard` + `GET /api/seller/sales-trend?from=&to=` |
| `02-dashboard/sales-trend.md` | `SalesTrend.vue` | `/dashboard/sales-trend` | `GET /api/seller/sales-trend?from=&to=`（自定义范围）+ `GET /api/seller/metrics?from=&to=` |
| `02-dashboard/low-stock-alert.md` | `LowStockAlert.vue` | `/dashboard/low-stock` | **后端缺失** → mock handler 兜底，UI 加「后端未就绪」徽标 |

图表：`ChartLine` 双 Y 轴（销售额/订单数），主色 `#1677FF`；统计卡片用 `DashboardCard`。

### 2.3 P0 — 模块 03-product-management（4 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `03-product-management/product-list.md` | `ProductList.vue` | `/products` | `GET /api/products`（Seller 角色自动按 ShopId 过滤） |
| `03-product-management/product-edit.md` | `ProductEdit.vue` | `/products/new`、`/products/:id/edit` | `POST /api/products`、`PUT /api/products/{id}`、`GET /api/products/{id}` |
| `03-product-management/sku-management.md` | `SkuManagement.vue` | `/products/:id/skus` | `POST /api/products/{id}/skus`、`POST /api/products/{id}/skus/{skuId}/price` |
| `03-product-management/price-history.md` | `PriceHistory.vue` | `/products/:id/price-history` | `GET /api/products/{id}/price-history?skuId=` |

**关键路径修正**：商品端点在 `/api/products` 前缀下（非概览文档的 `/api/seller/products`），靠 `[Authorize(Roles = "Seller")]` 类级特性 + `ApplyShopScope` 实现 ShopId 隔离。提交审核 `POST /api/products/{id}/submit`、下架 `POST /api/products/{id}/take-down`、重新上架 `POST /api/products/{id}/republish`。

### 2.4 P0 — 模块 05-order-fulfillment（3 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `05-order-fulfillment/pending-shipment.md` | `PendingShipment.vue` | `/orders/pending-shipment` | `GET /api/seller/orders?status=PendingShipment&page=0&pageSize=20`（首页 `page=0`，BE-1 待统一） |
| `05-order-fulfillment/order-list.md` | `OrderList.vue` | `/orders` | `GET /api/seller/orders?status=&orderNo=&startDate=&endDate=&page=0&pageSize=20`（首页 `page=0`，BE-1 待统一） |
| `05-order-fulfillment/logistics-trace.md` | `LogisticsTrace.vue` | `/orders/:id/trace` | `GET /api/orders/{id}/logistics-trace`（买卖家共用路径） |

**关键路径修正**：物流轨迹实际端点为 `/api/orders/{id}/logistics-trace`（非 `/api/seller/orders/{id}/trace`）。

**分页参数**：Order BC 后端 `page` 从 **0** 起，与 SellerShop/Review 的 `page=1` 不一致。**前端不做转换**，按后端实际语义使用（首页传 `page=0`）；`order.api.ts` 与调用处加 `// TODO(backend): BE-1 待 Order BC 统一 page 从 1 起` 标注，待后端统一后移除注释即可，无需改调用代码。详见 §7.1 BE-1。

### 2.5 P0 — 模块 06-after-sales（2 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `06-after-sales/after-sales-list.md` | `AfterSalesList.vue` | `/after-sales` | `GET /api/seller/after-sales?status=&page=1&pageSize=20` |
| `06-after-sales/after-sales-detail.md` | `AfterSalesDetail.vue` | `/after-sales/:id` | `GET /api/seller/after-sales/{id}` + `POST /api/seller/after-sales/{id}/approve`、`/reject`、`/confirm-return` |

**对接 ReviewAfterSales 合并版**（BFF 已使用此服务）。分页 `page=1` 起。

### 2.6 P1 — 模块 01-onboarding（4 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `01-onboarding/application.md` | `Application.vue` | `/shop/application` | `POST /api/shops/application`（**单数 application**） |
| `01-onboarding/shop-profile.md` | `ShopProfile.vue` | `/shop/profile` | `GET /api/shops/me`、`PUT /api/shops/me` |
| `01-onboarding/qualifications.md` | `Qualifications.vue` | `/shop/qualifications` | `POST /api/shops/me/qualifications`（multipart/form-data） |
| `01-onboarding/shop-preview.md` | `ShopPreview.vue` | `/shop/preview` | `GET /api/shops/me`（只读预览） |

**关键路径修正**：入驻申请实际路径为 `/api/shops/application`（非 `/api/seller/applications/*`）。资质上传走 `multipart/form-data`，含文件非空校验。

### 2.7 P1 — 模块 04-logistics（2 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `04-logistics/freight-templates.md` | `FreightTemplates.vue` | `/logistics/freight-templates` | `GET/POST /api/seller/freight-templates`、`PUT /api/seller/freight-templates/{id}/rules`、`POST /{id}/enable`、`/disable`、`GET /mine` |
| `04-logistics/logistics-companies.md` | `LogisticsCompanies.vue` | `/logistics/companies` | `GET /api/seller/logistics-companies?page=1&pageSize=50`（仅 Enabled 项） |

### 2.8 P1 — 模块 07-review（1 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `07-review/review-reply.md` | `ReviewReply.vue` | `/reviews` | `GET /api/seller/reviews?rating=&replied=&productName=&startDate=&endDate=&page=1&pageSize=20` + `POST /api/reviews/{id}/reply` |

**对接 ReviewAfterSales 合并版**：卖家回复路径为 `/api/reviews/{id}/reply`（**无 seller 前缀**，与概览文档一致）。注意合并版未提供 `/api/seller/reviews/{id}` 详情端点，详情信息已含在列表项中。

### 2.9 P1 — 模块 08-account（3 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `08-account/login.md` | `Login.vue` | `/login` | `POST /api/auth/login`（Identity 域） |
| `08-account/profile.md` | `Profile.vue` | `/account/profile` | `GET/PUT /api/users/me`、`PUT /api/users/me/password`（oldPassword 字段） |
| `08-account/notifications.md` | `Notifications.vue` | `/account/notifications` | `GET /api/notifications?isRead=&page=1&pageSize=20`、`GET /api/notifications/unread-count`、`POST /api/notifications/read`、`/read-all` |

**对接 Identity 服务**（system-admin 已验证）：登录返回 `{ token, expiresIn, user, roles, permissions }`；改密字段为 `oldPassword`（非 `currentPassword`）；含双因子端点（`POST /api/users/me/two-factor/enable` 等，本次仅 UI 预留不接通）。

### 2.10 P2 — 模块 09-export（1 页）

| design-prompt | Vue 视图 | 路由 path | 真实后端 API |
|---|---|---|---|
| `09-export/sales-export.md` | `SalesExport.vue` | `/export/sales` | **后端缺失** → mock handler 兜底，UI 加「后端未就绪」徽标 |

### 2.11 框架页（5 页，复用 system-admin 模式）

| design-prompt | Vue 视图 | 路由 path |
|---|---|---|
| `10-framework/forbidden.html` | `Forbidden.vue` | `/403` |
| `10-framework/not-found.html` | `NotFound.vue` | `/404` |
| `10-framework/maintenance.html` | `Maintenance.vue` | `/maintenance` |
| `10-framework/rate-limited.html` | `RateLimited.vue` | `/rate-limited` |
| `10-framework/server-error.html` | `ServerError.vue` | `/server-error` |

### 2.12 命名约定（遵循 `docs/conventions/naming-conventions.md` 与 `docs/design-prompts/shared/writing-guide.md`）

- 视图文件：PascalCase `.vue`，与 design-prompt 文件名 PascalCase 化对应（`product-list.md` → `ProductList.vue`）
- API 函数：camelCase 动词开头（`listProducts`、`submitForReview`、`takeDown`）
- DTO 类型：PascalCase + `Dto` 后缀（`ProductListItemDto`、`SellerDashboardDto`）
- Store：`useXxxStore`（`useAuthStore`、`useShopStore`、`useProductStore`）
- 路由 name：`{module}.{view}` kebab-case（`product.list`、`order.pending-shipment`）

### 2.13 页面与 controller 端点覆盖核对

- 23 业务页 + 5 框架页 ↔ 28 个 .vue ↔ SellerShop / Product / Order / ReviewAfterSales / Identity / Notification 6 个 BC 的卖家端 controller
- 100% 覆盖 design-prompts 列出的 API 端点（2 个缺失端点用 mock 兜底）
- 4 处路径偏差已在各模块小节显式标注修正

## 3 鉴权与路由守卫（权限模型与 system-admin 对齐）

### 3.1 Auth Store（`shared/auth/auth.store.ts`，Seller 角色）

```ts
interface AuthState {
  token: string | null
  user: SellerUserDto | null          // { id, username, shopId, shopName, shopStatus, avatar? }
  roles: string[]                     // ['Seller']
  permissions: string[]               // 后端返回，如 ['product:create','order:ship','aftersales:approve', ...]
  loginAt: number | null
  expiresAt: number | null            // token 过期时间戳（ms）
  twoFactorPending: boolean           // 永远 false（本次不接通 2FA）
}

const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    token: null,
    user: null,
    roles: [],
    permissions: [],
    loginAt: null,
    expiresAt: null,
    twoFactorPending: false,
  }),
  getters: {
    isAuthenticated: (s) => !!s.token && (s.expiresAt ?? 0) > Date.now(),
    isSeller: (s) => s.roles.includes('Seller'),
    hasPermission: (s) => (perm: string) =>
      s.permissions.includes(perm) || s.permissions.includes('*'),
    hasRole: (s) => (roles: string[]) =>
      roles.length === 0 || roles.some(r => s.roles.includes(r)),
  },
  actions: {
    async login(body: LoginDto)       // POST /api/auth/login → 存 token/user/roles/permissions
    async fetchProfile()              // GET /api/users/me → 刷新 user/permissions
    async logout()                    // 清状态 + 跳 /login + POST /api/auth/logout (best-effort)
  },
  persist: {
    storage: localStorage,
    pick: ['token', 'user', 'roles', 'permissions', 'expiresAt'],
  },
})
```

与 system-admin 唯一差异：`user` 含 `shopId/shopName/shopStatus`（卖家特有），用于 Header 展示店铺名 + 店铺状态门禁（`shopStatus === 'Suspended'` 时禁止上架商品但允许履约既有订单）。

### 3.2 Shop Store（`shared/shop/shop.store.ts`，新增）

```ts
interface ShopState {
  shopId: string | null
  shopName: string | null
  shopStatus: 'Active' | 'Suspended' | 'PendingReview' | 'Rejected' | null
  qualificationsStatus: { [type: string]: 'Approved' | 'Pending' | 'Rejected' }
}

const useShopStore = defineStore('shop', {
  state: (): ShopState => ({ /* 初始值 */ }),
  getters: {
    canPublish: (s) => s.shopStatus === 'Active',           // 可上架商品
    canFulfill: (s) => s.shopStatus !== 'Rejected',         // 可履约既有订单
    isOnboardingComplete: (s) => s.shopStatus === 'Active',
  },
  actions: {
    async fetchMyShop()    // GET /api/shops/me → 刷新 shopId/name/status
    async updateShop(dto)  // PUT /api/shops/me
  },
  persist: { storage: localStorage, pick: ['shopId', 'shopName', 'shopStatus'] },
})
```

登录成功后 `useShopStore.fetchMyShop()` 拉取店铺信息；路由守卫与页面按钮据此门禁（概览文档 §3 路由守卫：暂停态禁止上架商品但允许履约既有订单）。

### 3.3 登录流程（仅账号密码，2FA UI 预留）

```
Login.vue
  ├─ 用户输入 username + password
  ├─ POST /api/auth/login { username, password }
  │   ├─ 200 → { token, expiresIn, user, roles, permissions }
  │   │       → useAuthStore.login() 持久化
  │   │       → useShopStore.fetchMyShop() 拉店铺信息
  │   │       → 若 shopStatus === 'PendingReview'/'Rejected' → 跳 /shop/application 引导完善
  │   │       → 否则跳 redirect ?? /dashboard/overview
  │   ├─ 401 → inline「账号或密码错误」
  │   ├─ 403 → inline「账号已禁用」
  │   └─ 429 → 倒计时按钮「操作过于频繁，请 N 秒后重试」
  └─ OTP 输入区静态展示 + 角标「2FA 暂未启用」，提交按钮 disabled
```

环境变量 `VITE_REQUIRE_2FA=false`（默认）。若未来后端 2FA 就绪，把 Login.vue 第二步接通即可，无需改架构。

### 3.4 路由守卫（`app/router.ts`）

```ts
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  const shop = useShopStore()

  // 1. 公开路由（/login、/403、/404 等）直接放行
  if (to.meta.anonymous) return true

  // 2. 未登录 → 跳 /login?redirect=to.fullPath
  if (!auth.isAuthenticated) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  // 3. 首次进入或刷新后 user 为空 → 拉取 profile + shop
  if (!auth.user) {
    try {
      await auth.fetchProfile()
      await shop.fetchMyShop()
    } catch { auth.logout(); return { path: '/login' } }
  }

  // 4. 角色校验：meta.roles 与 auth.roles 取交集
  if (!auth.hasRole(to.meta.roles ?? [])) {
    return { path: '/403' }
  }

  // 5. 权限校验：meta.permission（可选）
  if (to.meta.permission && !auth.hasPermission(to.meta.permission)) {
    return { path: '/403' }
  }

  // 6. 店铺状态门禁：上架类路由需 shop.canPublish
  if (to.meta.requiresActiveShop && !shop.canPublish) {
    message.warning('店铺当前状态不允许此操作，请先完成入驻或联系平台')
    return { path: '/shop/application' }
  }

  return true
})
```

### 3.5 路由元信息约定

每个模块 `routes.ts` 的路由项：
```ts
{
  path: 'products',
  name: 'product.list',
  component: () => import('../views/ProductList.vue'),
  meta: {
    title: '商品列表',                  // 面包屑 + 页面标题
    menuKey: 'product.list',           // 菜单高亮
    icon: 'ShopOutlined',
    roles: ['Seller'],                  // 角色级（页面可见性）
    permission: 'product:list',         // 权限级（可选，更细粒度）
    menuGroup: '03-product-management', // Sider 分组
    requiresActiveShop: false,          // 是否要求店铺 Active 态
  },
}
```

`requiresActiveShop: true` 仅用于「新增商品」「商品编辑」路由；列表页与订单/售后履约页设 `false`（允许暂停态卖家处理既有订单）。

### 3.6 菜单渲染（`shared/layout/SiderMenu.vue`，与 system-admin 一致）

- 从 `router.options.routes` 中过滤 `meta.menuGroup` 不为空的路由
- 按 `menuGroup` 分组（工作台 / 商品管理 / 物流管理 / 订单履约 / 售后处理 / 评价管理 / 店铺设置 / 报表导出 / 个人中心）
- 每项 `v-if="auth.hasRole(item.meta.roles) && auth.hasPermission(item.meta.permission)"` 控制可见
- 当前路由通过 `menuKey` 匹配高亮
- 一级菜单按 `docs/design-prompts/seller/00-overview.md` §2 顺序：工作台 → 商品管理 → 物流管理 → 订单履约 → 售后处理 → 评价管理 → 店铺设置 → 报表导出 → 个人中心

### 3.7 权限控制两层（与 system-admin 一致）

**页面级**：路由 `meta.roles` + `meta.permission` + 守卫拦截（3.4 步骤 4-5）

**按钮/操作级**：通过 `<PermissionGuard>` 组件或 `v-permission` 指令

```vue
<IdempotencyButton
  v-permission="'product:submit-review'"
  type="primary"
  @click="onSubmitReview"
>提交审核</IdempotencyButton>

<PermissionGuard permission="product:take-down">
  <a-button danger @click="onTakeDown">下架</a-button>
</PermissionGuard>
```

`v-permission` 实现：无权限时 `el.style.display = 'none'`（不删 DOM，避免 hydration 问题）。

### 3.8 权限码命名约定（与后端约定对齐）

权限码格式 `{domain}:{action}`，由后端在登录响应 `permissions[]` 返回。卖家端预期权限码：

| 域 | 权限码 |
|---|---|
| 商品 | `product:list`、`product:create`、`product:edit`、`product:submit-review`、`product:take-down`、`product:republish`、`product:sku:manage`、`product:price:adjust`、`product:price-history:view` |
| 订单 | `order:list`、`order:ship`、`order:trace:view` |
| 售后 | `aftersales:list`、`aftersales:approve`、`aftersales:reject`、`aftersales:confirm-return` |
| 评价 | `review:list`、`review:reply` |
| 店铺 | `shop:application:submit`、`shop:profile:view`、`shop:profile:edit`、`shop:qualification:upload` |
| 物流 | `freight-template:list`、`freight-template:create`、`freight-template:edit`、`freight-template:enable`、`logistics-company:list` |
| 报表 | `export:sales` |
| 账号 | `account:profile:view`、`account:profile:edit`、`account:password:change`、`notification:list`、`notification:read` |
| 工作台 | `dashboard:view`、`dashboard:sales-trend`、`dashboard:low-stock` |

前端不硬编码权限码判断逻辑（除 `*` 通配），所有可见性均由后端返回的 `permissions[]` 驱动。若后端返回 `['*']` 表示全权限（超级卖家）。

### 3.9 Token 刷新与登出

- **过期前刷新**：响应拦截器收到 401 时，若 `expiresAt` 即将到（< 5 分钟）且未在刷新中，尝试 `POST /api/auth/refresh-token`；失败则跳 `/login`
- **登出**：清 store + localStorage + sessionStorage（除偏好设置）；`router.push('/login')`；best-effort 调 `POST /api/auth/logout`（失败不阻塞）

### 3.10 Header 待办徽标（卖家特有，system-admin 无）

Header 右侧除通知铃铛外，增加两个红色 Badge 提醒（概览文档 §2 快捷入口）：

```
[待发货 N 单]  [售后待处理 N 单]  [🔔 通知]  [用户头像 ▼]
```

- 数据来源：`GET /api/seller/dashboard` 返回的 `pendingOrders`（待发货）+ `todayRefundCount`（售后待处理）
- 刷新策略：进入工作台时拉取；每 60 秒轮询一次；切换路由时不刷新（避免频繁请求）
- 点击「待发货 N 单」→ 跳 `/orders/pending-shipment`
- 点击「售后待处理 N 单」→ 跳 `/after-sales?status=Pending`

## 4 数据流与 HTTP 客户端

### 4.1 后端响应信封（与 system-admin 完全一致，已验证 `code === 200`）

```ts
interface ApiResponse<T> {
  code: number       // 200 成功（非 0）；非 200 业务错误码
  message: string    // 人类可读信息
  data: T            // 业务负载，可能为 null
  traceId?: string   // OpenTelemetry traceId
}

interface PageResult<T> {
  items: T[]
  total: number
  page: number       // 后端统一从 1 起；Order BC 当前从 0 起（BE-1 待统一）
  pageSize: number
}
```

### 4.2 axios 实例（`shared/http/client.ts`，从 system-admin 复制为基线）

```ts
const client: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})
```

成功判定 `body.code === 200`（与 system-admin 2026-07-29 对齐决策一致），非 200 抛 `BusinessError`。

### 4.3 请求拦截器（4 层，与 system-admin 一致 + ShopId 冗余）

1. **鉴权**：从 `useAuthStore().token` 读取（通过 localStorage 直读避免循环依赖），注入 `Authorization: Bearer {token}`
2. **幂等键**：`withIdempotency()` 包装时注入 `Idempotency-Key: {uuid v4}`
3. **traceId**：生成 `X-Request-Id`，便于后端日志关联
4. **ShopId 上下文**（可选）：部分写操作后端需 `X-Shop-Id` 头辅助校验，从 `useShopStore().shopId` 读取注入（后端主要靠 JWT Claims 解析，此头为冗余校验兜底）

### 4.4 响应拦截器（与 system-admin 一致）

1. **HTTP 层**：5xx → `ServerError`；401 → token 刷新流程；403 → `ForbiddenError`；404 → `NotFoundError`；409 → `ConcurrencyError`；429 → `RateLimitedError`
2. **业务层**：`code !== 200` → `BusinessError(code, message)`
3. **数据解包**：成功时返回 `response.data.data`，调用方拿到的就是 `T`
4. **traceId 透传**：失败时带出 traceId 供 UI 展示

### 4.5 错误类型层级（从 system-admin 复制，无改动）

```ts
abstract class AppError { abstract kind: string; message: string; traceId?: string }
class NetworkError extends AppError       // 网络异常、超时
class BusinessError extends AppError      // code !== 200
class UnauthorizedError extends AppError // 401
class ForbiddenError extends AppError     // 403
class NotFoundError extends AppError      // 404
class RateLimitedError extends AppError   // 429，含 retryAfter
class ServerError extends AppError        // 5xx
class ConcurrencyError extends AppError  // 409 乐观锁冲突，含 currentVersion
```

### 4.6 API 函数模板（每个模块 `api/{module}.api.ts`）

以 `03-product-management/api/product.api.ts` 为例：

```ts
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  ProductListItemDto, ProductDetailDto, CreateProductDto,
  UpdateProductDto, AddSkuDto, AdjustPriceDto, PriceChangeRecordDto,
  ActionReasonDto,
} from '../types/product.dto'

export interface ListProductsParams {
  keyword?: string
  status?: ProductStatus[]
  categoryId?: string
  page?: number    // 从 1 起
  pageSize?: number
}

export const productApi = {
  list: (params: ListProductsParams) =>
    client.get<PageResult<ProductListItemDto>>('/products', { params }),

  get: (id: string) =>
    client.get<ProductDetailDto>(`/products/${id}`),

  create: (body: CreateProductDto) =>
    client.post<ProductDetailDto>('/products', body, withIdempotency()),

  update: (id: string, body: UpdateProductDto) =>
    client.put<ProductDetailDto>(`/products/${id}`, body, withIdempotency()),

  addSku: (productId: string, body: AddSkuDto) =>
    client.post<ProductDetailDto>(`/products/${productId}/skus`, body, withIdempotency()),

  adjustPrice: (productId: string, skuId: string, body: AdjustPriceDto) =>
    client.post(`/products/${productId}/skus/${skuId}/price`, body, withIdempotency()),

  submitForReview: (id: string) =>
    client.post(`/products/${id}/submit`, null, withIdempotency()),

  takeDown: (id: string, body: ActionReasonDto) =>
    client.post(`/products/${id}/take-down`, body, withIdempotency()),

  republish: (id: string) =>
    client.post(`/products/${id}/republish`, null, withIdempotency()),

  getPriceHistory: (id: string, skuId?: string) =>
    client.get<PriceChangeRecordDto[]>(`/products/${id}/price-history`, { params: { skuId } }),
}
```

### 4.7 订单 api 分页参数标注（`05-order-fulfillment/api/order.api.ts`）

```ts
import { client } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { OrderListItemDto, OrderStatus, ShipOrderDto } from '../types/order.dto'

// TODO(backend): BE-1 待 Order BC 统一 page 从 1 起（当前从 0 起，与 SellerShop/Review 不一致）
//   后端统一后，将下方 page 默认值从 0 改为 1，并移除此 TODO 与调用处的同步标注。
export interface ListOrdersParams {
  status?: OrderStatus
  orderNo?: string
  startDate?: string
  endDate?: string
  page?: number    // 后端当前从 0 起（首页传 0），BE-1 待统一为 1
  pageSize?: number
}

export const orderApi = {
  list: (params: ListOrdersParams) => {
    const { page = 0, pageSize = 20, ...rest } = params
    return client.get<PageResult<OrderListItemDto>>('/seller/orders', {
      params: { ...rest, page, pageSize },
    })
  },

  ship: (id: string, body: ShipOrderDto) =>
    client.post(`/seller/orders/${id}/ship`, body, withIdempotency()),

  getLogisticsTrace: (orderId: string) =>
    client.get<LogisticsTrackingDto>(`/orders/${orderId}/logistics-trace`),
}
```

**待后端统一 `page` 后，仅需将默认值 `page = 0` 改为 `page = 1` 并移除 TODO 注释**，无需改调用方代码。spec 验收项会跟踪此项（BE-1）。

### 4.8 缓存策略

| 数据 | 缓存 | 过期 |
|---|---|---|
| 工作台 dashboard 概览 | Pinia + sessionStorage | 1 分钟 + 手动刷新按钮 |
| 销售趋势 sales-trend | Pinia | 5 分钟，时间范围变更失效 |
| 店铺信息 shop | Pinia + localStorage | 持久至登出 |
| 当前用户 profile/角色权限 | Pinia + localStorage | 持久至登出 |
| 商品/订单/售后列表 | 不缓存 | 实时拉取 |
| 物流公司列表 | Pinia | 10 分钟（低频变更） |
| 数据字典（如适用） | Pinia | 10 分钟，编辑后失效 |
| 通知未读数 | Pinia | 60 秒轮询刷新 |
| Header 待办徽标数 | Pinia（共享 dashboard 数据） | 60 秒轮询 |

### 4.9 乐观锁与并发

后端 spec 11 INV-SELLER 中 Product、FreightTemplate 含 `Version` 字段。前端编辑流：

1. GET 拿到资源含 `version: 3`
2. PUT 请求头 `X-Resource-Version: 3` + body 含 `version: 3`
3. 若被他人修改，后端返 409 + `{ currentVersion: 4 }`
4. 响应拦截器抛 `ConcurrencyError`，UI 弹「该资源已被他人修改，是否刷新后重试？」对话框，确认后重新 GET

### 4.10 文件上传/下载

- **上传**（店铺资质 `POST /api/shops/me/qualifications`）：`Content-Type: multipart/form-data`，单文件 ≤ 10MB，前端 `<a-upload>` + `beforeUpload` 校验类型（图片/PDF）与大小
- **下载**（P2 报表导出）：`responseType: 'blob'`，文件名从 `Content-Disposition` 解析

### 4.11 全局错误处理（`main.ts` 注册，与 system-admin 一致）

```ts
app.config.errorHandler = (err) => {
  if (err instanceof BusinessError) message.error(err.message)
  else if (err instanceof ConcurrencyError) {
    Modal.confirm({
      title: '资源已被他人修改',
      content: `当前版本：v${err.currentVersion}。是否刷新后重试？`,
      okText: '刷新重试',
      cancelText: '取消',
      onOk: () => window.location.reload(),
    })
  }
  else if (err instanceof RateLimitedError) message.warning(`操作过于频繁，请 ${err.retryAfter}s 后重试`)
  else if (err instanceof ForbiddenError) message.error('无权限访问')
  // NetworkError/ServerError 由全局 ErrorBoundary 兜底
}
```

### 4.12 Mock 退役策略（与 system-admin 2026-07-29 决策一致）

- 初始 `VITE_USE_MOCK=true`，所有 API 走 mock handler
- 仅 2 个真实缺失端点（low-stock、exports）的 mock handler **长期保留**，并加「后端未就绪」徽标
- 其余端点的 mock handler 在 P0/P1/P2 各阶段联调通过后逐步退役，但**保留存档**便于将来临时切回调试
- mock handler 响应体 `code: 200`（与真实后端成功约定一致）

## 5 共享组件与视觉规范

### 5.1 共享组件清单（从 system-admin 复制为基线 + 卖家特有新增）

| 组件 | 路径 | 职责 | 关键 props | 来源 |
|---|---|---|---|---|
| `StatusTag` | `shared/components/StatusTag.vue` | 通用状态标签 | `type`（product/order/aftersales/shop/freightTemplate）、`status` | 复制 + 扩展状态映射 |
| `IdempotencyButton` | `shared/components/IdempotencyButton.vue` | 包装 `<a-button>`，自动注入 `Idempotency-Key`，loading 期间禁用 | `idempotencyKey?`、`loading`、`onClick` | 复制，无改动 |
| `PermissionGuard` | `shared/components/PermissionGuard.vue` | 无权限时隐藏 slot 内容 | `permission` | 复制，无改动 |
| `v-permission` 指令 | `shared/auth/permission.ts` | 按钮级权限隐藏 | `string` 权限码 | 复制，无改动 |
| `DataTable` | `shared/components/DataTable.vue` | 包装 `<a-table>`，统一分页/筛选/空态/列设置 | `columns`、`fetcher`、`rowKey` | 复制，无改动 |
| `EmptyState` | `shared/components/EmptyState.vue` | 包装 `<a-empty>`，含 CTA 按钮 | `description`、`actionText?`、`@action` | 复制，无改动 |
| `ConfirmDialog` | `shared/components/ConfirmDialog.vue` | 包装 `Modal.confirm`，统一危险/普通样式 | `danger`、`title`、`content`、`requireInput?` | 复制，无改动 |
| `DateTimeRangePicker` | `shared/components/DateTimeRangePicker.vue` | 包装 `<a-range-picker>`，输出 ISO 8601 UTC | `value`、`@change` | 复制，无改动 |
| `ChartLine` / `ChartBar` / `ChartPie` | `shared/components/charts/` | 包装 `@vue-echarts`，预设主题色与 tooltip | `series`、`xAxis`、`loading` | 复制，无改动 |
| `JsonViewer` | `shared/components/JsonViewer.vue` | 数据展示，可折叠 + 语法高亮 | `data`、`maxHeight` | 复制，无改动 |
| `ErrorBoundary` | `shared/components/ErrorBoundary.vue` | 包装 `<a-result status="error">`，含重试 | `#fallback` slot | 复制，无改动 |
| `PasswordStrengthIndicator` | `shared/components/PasswordStrengthIndicator.vue` | 密码强度可视化 | `password` | 复制，无改动 |
| `StatisticCard` | `shared/components/StatisticCard.vue` | 统计数值卡片 | `title`、`value`、`prefix`、`suffix`、`trend` | 复制，无改动 |
| `DashboardCard` | `shared/components/DashboardCard.vue` | 工作台统计卡片（含环比趋势箭头） | `title`、`value`、`icon`、`trend`、`trendType` | 复制，无改动 |
| **`TodoBadge`** | `shared/components/TodoBadge.vue` | Header 待办徽标（待发货/售后待处理） | `count`、`label`、`@click` | **新增**（卖家特有） |
| **`ShopStatusGuard`** | `shared/components/ShopStatusGuard.vue` | 店铺状态门禁包装，非 Active 态显示提示 | `requires`（'canPublish'/'canFulfill'）、`fallbackText` | **新增**（卖家特有） |

### 5.2 StatusTag 状态映射扩展

```ts
// shared/components/StatusTag.vue 扩展 statusMap
const statusMap = {
  product: {
    Draft: { color: 'default', text: '草稿' },
    PendingReview: { color: 'warning', text: '待审核' },
    Approved: { color: 'success', text: '已上架' },
    TakenDown: { color: 'default', text: '已下架' },
    Rejected: { color: 'error', text: '已驳回' },
  },
  order: {
    PendingShipment: { color: 'warning', text: '待发货' },
    Shipped: { color: 'processing', text: '已发货' },
    Delivered: { color: 'processing', text: '已送达' },
    Completed: { color: 'success', text: '已完成' },
    Cancelled: { color: 'default', text: '已取消' },
    Refunded: { color: 'default', text: '已退款' },
  },
  aftersales: {
    Pending: { color: 'warning', text: '待处理' },
    Approved: { color: 'processing', text: '已同意' },
    Rejected: { color: 'error', text: '已拒绝' },
    ReturnInProgress: { color: 'processing', text: '退货中' },
    Refunded: { color: 'success', text: '已退款' },
    Closed: { color: 'default', text: '已关闭' },
  },
  shop: {
    PendingReview: { color: 'warning', text: '审核中' },
    Active: { color: 'success', text: '正常' },
    Suspended: { color: 'error', text: '暂停' },
    Rejected: { color: 'error', text: '已驳回' },
  },
  freightTemplate: {
    Enabled: { color: 'success', text: '启用' },
    Disabled: { color: 'default', text: '禁用' },
  },
}
```

颜色映射严格遵循 `design-system.md` §2.1：成功绿 `#52C41A`、警告黄 `#FAAD14`、错误红 `#FF4D4F`、进行中蓝 `#1677FF`（processing）、默认灰 `#8C8C8C`。

### 5.3 设计令牌（`shared/tokens/design-tokens.css` + `antd-theme.ts`，直接复用 system-admin）

```css
:root {
  /* 色彩（与 designs/_shared/tokens.css 一致） */
  --c-primary:#1677FF; --c-success:#52C41A; --c-warning:#FAAD14; --c-error:#FF4D4F;
  --n1:#FFFFFF; --n2:#FAFAFA; --n3:#F5F5F5; --n5:#D9D9D9; --n7:#8C8C8C; --n9:#595959; --n10:#000000D9;
  --sider-bg:#001529;

  /* 圆角 */
  --r-base:6px; --r-card:8px; --r-lg:12px;

  /* 间距 4/8/12/16/24/32/48 */
  --s1:4px; --s2:8px; --s3:12px; --s4:16px; --s6:24px; --s8:32px; --s12:48px;

  /* 字号 */
  --fs-sm:12px; --fs-base:14px; --fs-lg:16px; --fs-xl:20px; --fs-2xl:24px; --fs-3xl:30px;

  /* 阴影 */
  --sh-card:0 1px 2px 0 rgba(0,0,0,.03),0 1px 6px -1px rgba(0,0,0,.02),0 2px 4px 0 rgba(0,0,0,.02);
  --sh-modal:0 12px 32px 4px rgba(0,0,0,.08),0 8px 20px 8px rgba(0,0,0,.06);

  /* 布局 */
  --sider-width:200px; --header-h:64px; --footer-h:32px;
}
```

`app/provider.vue` 映射到 Ant Design Vue 4.x ConfigProvider theme（与 system-admin 完全一致）：

```ts
const theme = {
  token: {
    colorPrimary: '#1677FF',
    colorSuccess: '#52C41A',
    colorWarning: '#FAAD14',
    colorError: '#FF4D4F',
    borderRadius: 6,
    fontFamily: '"PingFang SC","Microsoft YaHei",-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif',
    fontSize: 14,
  },
  components: {
    Table: { rowHoverBg: '#FAFAFA', headerBg: '#FAFAFA', headerColor: '#595959', cellPaddingBlock: 12 },
    Menu: { darkItemBg: '#001529', darkItemSelectedBg: '#1677FF' },
  },
}
```

### 5.4 全局布局（`shared/layout/BasicLayout.vue`）

```
┌────────────────────────────────────────────────────────────────┐
│ Header 64px: Logo+店铺名 │ 面包屑 │ [待发货N] [售后N] [🔔] [👤▼] │
├──────────┬─────────────────────────────────────────────────────┤
│ Sider    │ Content (padding 24px)                                │
│ 200px    │   <RouterView />                                     │
│ #001529  │                                                      │
│ 可折叠    │                                                      │
│ (80px)   │                                                      │
│          │                                                      │
├──────────┴─────────────────────────────────────────────────────┤
│ Footer 32px: © Leno · v1.0.0                                     │
└────────────────────────────────────────────────────────────────┘
```

- Header 固定顶部、Sider 固定左侧（`position: fixed`）
- Content `margin-left: 200px; margin-top: 64px;`
- Sider `<a-layout-sider collapsible :collapsed-width="80">`，992-1199px 自动折叠
- <992px 显示「请使用桌面端访问」提示页
- **HeaderBar 卖家特有**：Logo 右侧显示当前店铺名（`useShopStore().shopName`）+ 店铺状态 `<StatusTag type="shop" />`；右侧待办徽标 `<TodoBadge>` × 2 + 通知铃铛 + 用户头像下拉

### 5.5 表格密度与样式（与 system-admin 一致）

- 表格统一 `size="middle"`、`rowKey` 显式声明
- 列宽：状态列 100px、时间列 160px、操作列按按钮数（80-200px）
- 操作列用 `<a-space>` 包裹 `<a-button type="link" size="small">`
- 行高 48px，hover 背景 `#FAFAFA`
- >100 行启用虚拟滚动 `:scroll="{ y: 500 }"`

### 5.6 字体与图标

- 字体栈：`"PingFang SC","Microsoft YaHei",-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif`
- 等宽字体（订单号/JSON）：`"SF Mono","Cascadia Code",Consolas,monospace`
- 图标：`@ant-design/icons-vue`，尺寸 12/14/16/20/24/32，颜色 `currentColor`
- 命名风格：PascalCase + `Outlined` 后缀（`ShopOutlined`、`TruckOutlined`、`CustomerServiceOutlined`）

### 5.7 危险操作确认（落地概览文档 §5）

- 删除/下架/拒绝售后/强制取消 → 必走 `ConfirmDialog`，`danger: true`，确认按钮 `#FF4D4F`
- 下架/拒绝需填理由 → `requireInput: { label: '下架原因', min: 1, max: 500 }`，未填禁用确认按钮
- 批量操作前弹「将影响 N 条，是否继续？」

### 5.8 加载/空/错误三态（与 system-admin 一致）

| 状态 | 实现 |
|---|---|
| 加载中 | 表格 `<a-skeleton :rows="5">`；卡片 `<a-skeleton active />`；详情抽屉 `<a-spin />` |
| 空数据 | `<EmptyState description="暂无xxx" actionText="刷新" @action="reload" />` |
| 错误 | `<ErrorBoundary>` 兜底，展示「加载失败 #traceId」+ 重试按钮 |
| 网络错误 | `message.error('网络异常，请检查连接')` 3s 自动消失 |
| 403 | 跳 `/403` 专用页 |

### 5.9 响应式断点（与 system-admin 一致）

- ≥ 1200px：Sider 全展开 200px
- 992-1199px：Sider 折叠 80px
- < 992px：显示「请使用桌面端访问」全屏提示，不渲染主应用

### 5.10 可访问性

- 所有交互元素键盘可达，`Tab` 顺序符合视觉顺序
- 颜色对比度 ≥ WCAG AA（主色 `#1677FF` on white = 4.5:1 通过）
- 表单控件 `<label>` 关联，错误提示 `aria-describedby`
- 图标按钮 `aria-label`，状态变化 `aria-live="polite"`
- 对话框聚焦管理（打开时聚焦首个输入，关闭时还原触发元素）

## 6 测试、构建与可观测

### 6.1 测试分层（与 system-admin 一致）

| 层级 | 工具 | 覆盖范围 | 覆盖率门槛 |
|---|---|---|---|
| 单元测试 | Vitest 2.x | `shared/utils/`、`shared/http/` 拦截器逻辑、`shared/auth/` store 状态机、`shared/shop/` store、各模块 `api/*.api.ts` URL/参数构造 | 行覆盖 ≥ 70% |
| 组件测试 | Vitest + @vue/test-utils 2.x + jsdom | `shared/components/*` 16 个组件（含 2 个新增）props/emit/slot 行为 | 行覆盖 ≥ 60% |
| 类型检查 | `vue-tsc --noEmit` | 全量 .vue 与 .ts | 0 error |
| Lint | ESLint 9 + eslint-plugin-vue + @typescript-eslint | 全量代码 | 0 error，warn ≤ 阈值 |
| E2E（可选） | Playwright 1.x | 登录 → 工作台 → 商品列表 → 编辑 → 提交审核 → 订单待发货 → 发货 关键路径 | 至少 1 个 happy path |

### 6.2 单元测试约定（与 system-admin 一致）

- 文件命名：`*.spec.ts` 与源码同目录
- API 测试用 `vi.spyOn(client, 'get'/'post')` 拦截，断言 URL/method/params/headers
- Store 测试用 `setActivePinia(createPinia())` 隔离
- 时间相关用 `vi.useFakeTimers()`

示例（product.api.spec.ts）：
```ts
it('submitForReview 注入 Idempotency-Key 头', async () => {
  const mock = vi.spyOn(client, 'post').mockResolvedValue({ data: {} })
  await productApi.submitForReview('prod-1')
  expect(mock).toHaveBeenCalledWith('/products/prod-1/submit', null,
    expect.objectContaining({ headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }) }))
})

it('order.list 默认 page=0（BE-1 待统一为 1）', async () => {
  const mock = vi.spyOn(client, 'get').mockResolvedValue({ data: { items: [], total: 0, page: 0, pageSize: 20 } })
  await orderApi.list({ pageSize: 20 })   // 不传 page，使用默认 0
  expect(mock).toHaveBeenCalledWith('/seller/orders',
    { params: expect.objectContaining({ page: 0, pageSize: 20 }) })
})
```

### 6.3 重点测试用例清单

**shared 层**：
- `client.spec.ts`：`code === 200` 视为成功；`code !== 200` 抛 `BusinessError`；401/403/404/409/429/5xx 错误转换；CSV 导出（字符串响应）不被 unwrap
- `auth.store.spec.ts`：登录成功存 token/roles/permissions；fetchProfile 刷新；logout 清状态；`hasPermission` 与 `hasRole` 逻辑；`*` 通配权限
- `shop.store.spec.ts`：`fetchMyShop` 拉取；`canPublish`/`canFulfill` 状态门禁 getter
- `permission.spec.ts`：`v-permission` 指令无权限时 `display: none`
- `StatusTag.spec.ts`：5 个 type 的状态映射正确（product/order/aftersales/shop/freightTemplate）
- `TodoBadge.spec.ts`（新增）：count=0 时隐藏；count>0 显示；click 事件触发
- `ShopStatusGuard.spec.ts`（新增）：requires='canPublish' + 店铺非 Active 时显示 fallbackText
- `IdempotencyButton.spec.ts`：点击生成 Idempotency-Key；loading 期间禁用
- `ConfirmDialog.spec.ts`：danger=true 红色确认按钮；requireInput 未填禁用确认

**modules 层**（每个 api 文件 1 个 spec）：
- `product.api.spec.ts`：list/get/create/update/addSku/adjustPrice/submitForReview/takeDown/republish/getPriceHistory 的 URL/方法/参数
- `order.api.spec.ts`：list（默认 `page=0`，BE-1 标注）/ship/getLogisticsTrace
- `aftersales.api.spec.ts`：list/get/approve/reject/confirmReturn
- `review.api.spec.ts`：list（含筛选参数）/reply（路径 `/reviews/{id}/reply` 无 seller 前缀）
- `shop.api.spec.ts`：application（单数路径）/getMe/updateMe/submitQualification（multipart）
- `logistics.api.spec.ts`：freightTemplate CRUD + enable/disable + mine；logisticsCompanies 只读
- `auth.api.spec.ts`：login 返回结构断言；changePassword 字段 `oldPassword`
- `notification.api.spec.ts`：list/unreadCount/markAsRead/markAllAsRead

### 6.4 Vite 配置要点（`vite.config.ts`）

```ts
export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
  server: {
    port: 5174,                       // 与 system-admin(5173) 错开
    proxy: { '/api': { target: 'http://localhost:5001', changeOrigin: true } },
  },
  build: {
    target: 'es2022',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vue: ['vue', 'vue-router', 'pinia'],
          antd: ['ant-design-vue', '@ant-design/icons-vue'],
          echarts: ['echarts', 'vue-echarts'],
        },
      },
    },
  },
  test: { environment: 'jsdom', globals: true, setupFiles: './tests/setup.ts' },
})
```

### 6.5 TypeScript 配置（`tsconfig.json`，与 system-admin 一致）

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "jsx": "preserve",
    "paths": { "@/*": ["src/*"] },
    "types": ["vite/client", "vitest/globals"]
  },
  "include": ["src/**/*", "tests/**/*"]
}
```

### 6.6 环境变量

`.env.development`：
```
VITE_API_BASE=/api
VITE_API_TARGET=http://localhost:5001
VITE_REQUIRE_2FA=false
VITE_USE_MOCK=true
VITE_APP_VERSION=dev
```

`.env.production`：
```
VITE_API_BASE=/api
VITE_REQUIRE_2FA=false
VITE_USE_MOCK=false
VITE_APP_VERSION=1.0.0
```

类型化封装 `app/env.ts`：
```ts
export const env = {
  apiBase: import.meta.env.VITE_API_BASE,
  require2FA: import.meta.env.VITE_REQUIRE_2FA === 'true',
  useMock: import.meta.env.VITE_USE_MOCK === 'true',
  appVersion: import.meta.env.VITE_APP_VERSION,
} as const
```

### 6.7 package.json scripts（与 system-admin 一致）

```json
{
  "scripts": {
    "dev": "vite",
    "build": "vue-tsc --noEmit && vite build",
    "preview": "vite preview",
    "lint": "eslint . --max-warnings 0",
    "lint:fix": "eslint . --fix",
    "typecheck": "vue-tsc --noEmit",
    "test": "vitest run",
    "test:watch": "vitest",
    "test:coverage": "vitest run --coverage",
    "e2e": "playwright test"
  }
}
```

### 6.8 CI 集成（`.github/workflows/ci.yml` 增量）

新增前端 job：
```yaml
web-seller:
  runs-on: ubuntu-latest
  defaults: { run: { working-directory: web/seller } }
  steps:
    - uses: actions/checkout@v4
    - uses: pnpm/action-setup@v4
      with: { version: 9 }
    - uses: actions/setup-node@v4
      with: { node-version: 20, cache: pnpm, cache-dependency-path: web/seller/pnpm-lock.yaml }
    - run: pnpm install --frozen-lockfile
    - run: pnpm lint
    - run: pnpm typecheck
    - run: pnpm test -- --coverage --reporter=dot
    - run: pnpm build
    - uses: actions/upload-artifact@v4
      with: { name: web-seller-dist, path: web/seller/dist }
```

### 6.9 可观测性（与 system-admin 一致）

| 维度 | 实现 |
|---|---|
| 前端日志 | `shared/utils/logger.ts`：dev 写 console，prod 批量 POST 到后端（best-effort） |
| 错误追踪 | `app.config.errorHandler` + `window.addEventListener('unhandledrejection')` 统一捕获，注入 traceId |
| 性能监控 | `web-vitals` 库上报 LCP/CLS/INP |
| traceId 传播 | 每次请求生成 `X-Request-Id`；响应头 `traceparent` 写入 store 供错误展示 |
| 用户行为审计 | 关键写操作（上架/下架/发货/售后审核）成功后 best-effort 补充前端视角 traceId |

### 6.10 性能预算（验收门槛）

| 指标 | 目标 |
|---|---|
| 首屏 LCP（生产构建） | < 2.5s（1440p 桌面、4G 模拟） |
| 路由切换 | < 300ms（含数据加载） |
| 表格 > 100 行 | 启用 `virtual-scroll` |
| 产物体积（gzip） | 主 chunk < 200KB，Antd chunk < 350KB，ECharts chunk < 300KB |
| 防抖节流 | 搜索输入 300ms debounce；窗口 resize 100ms throttle |

## 7 后端待办项与范围外事项

### 7.1 后端待办项（前端 spec 标注，需后端跟进）

本 spec 识别出 3 项后端待办，前端会在代码中显式标注，待后端修复后移除兼容代码：

| # | 待办项 | 当前状态 | 前端临时处理 | 影响范围 |
|---|---|---|---|---|
| BE-1 | **Order BC 分页参数 `page` 统一为 1 起** | 后端 `ListSellerOrdersAsync` 默认 `page=0`，与 SellerShop/Review 的 `page=1` 不一致 | `order.api.ts` 中 `page` 默认值 `0`（首页传 0），加 `// TODO(backend): BE-1 待统一 page 从 1 起` 标注；前端**不做转换**，直接按后端实际语义使用 | 仅 `05-order-fulfillment` 模块 |
| BE-2 | **补齐 `/api/seller/dashboard/low-stock` 端点** | 全代码库无此路由 | P0 low-stock 页走 mock handler + UI「后端未就绪」徽标 | 仅 `02-dashboard/LowStockAlert.vue` |
| BE-3 | **补齐 `/api/seller/exports/*` 端点** | 全代码库无此路由 | P2 sales-export 页走 mock handler + UI「后端未就绪」徽标 | 仅 `09-export/SalesExport.vue` |

**验收联动**：
- BE-1 修复后，前端将 `order.api.ts` 中 `page` 默认值从 `0` 改为 `1`，移除 TODO 注释；同步更新 `order.api.spec.ts` 中首页用例的断言（从 `page: 0` 改为 `page: 1`）；调用方与表格组件无需改动
- BE-2/BE-3 修复后，关闭对应页面的 mock handler + 移除「后端未就绪」徽标 + `VITE_USE_MOCK` 局部切 false

### 7.2 范围外事项（YAGNI，明确不在本 spec 范围内）

- 后端各 BC 代码改动（含 BE-1/2/3 待办项的实现，单独提后端 spec）
- 移动端 / 平板端适配（概览文档明确不支持移动端，<992px 显示桌面端提示）
- 国际化（i18n）— 当前仅中文
- 暗色主题切换（仅预留 ConfigProvider 切换点，不实现）
- PWA / 离线缓存
- 实时推送（WebSocket / SSE）— 待办徽标与通知用 60 秒轮询
- Sentry / APM 接入 — 仅预留 transport，不接入
- 2FA 实际接通 — 仅 UI 预留 OTP 区，`VITE_REQUIRE_2FA=false`
- OAuth 第三方登录 — 仅预留入口，本次不接通
- `web/buyer-app/`、`web/operations/` 前端开发 — 单独立项
- 抽取 `@leno/shared` workspace 共享包（方案 B 已否决，方案 A 独立 SPA）

### 7.3 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| 后端双实现服务（Review/AfterSales vs ReviewAfterSales；Identity vs UserAuth）线上部署变更 | 评价回复路径差异（`/api/reviews/{id}/reply` vs `/api/seller/reviews/{id}/reply`） | 本 spec 对接 ReviewAfterSales + Identity；若线上切换，仅需调整 `review.api.ts` 路径常量，单点修改 |
| Order BC `page` 默认值 0 遗漏更新 | 后端统一为 1 后前端未同步，首页错位 | BE-1 待办项在 spec 验收项跟踪；后端统一后，前端将 `order.api.ts` 默认值从 `0` 改为 `1` + 移除 TODO + 更新测试断言 |
| 23 页规模大，实施周期长 | 长尾风险 | P0→P1→P2 分阶段，每阶段独立可发布、可验收 |
| Ant Design Vue 4.x 与 Vue 3.5 兼容性 bug | 表格/表单异常 | 锁定 patch 版本（`^4.2.6`），问题版本回退 |
| 后端 2FA 接口未来就绪 | Login.vue 需改造 | 通过 `VITE_REQUIRE_2FA` 开关切换，UI 已预留 OTP 区 |
| 从 system-admin 复制的 shared/ 随时间漂移 | 两端基础组件行为不一致 | 接受独立演化的代价（方案 A 决策已权衡）；低频变更的基础设施，漂移风险可控 |
| 商品端点 `/api/products` 靠角色隔离而非路径前缀 | 越权风险（若后端 ShopId 过滤失效） | 后端 `ApplyShopScope` + JWT Claims 双重校验已就位；前端无额外缓解措施，依赖后端 |

### 7.4 实施前依赖确认

实施前需确认以下后端依赖就绪（writing-plans 阶段生成对照表）：

1. `POST /api/auth/login` 返回结构含 `{ token, expiresIn, user, roles, permissions }`，`user` 含 `shopId/shopName/shopStatus`
2. `GET /api/users/me` 返回当前用户 profile + permissions
3. `GET /api/shops/me` 返回店铺信息含 `shopId/shopName/status`
4. `GET /api/seller/dashboard` + `/api/seller/sales-trend` + `/api/seller/metrics` 就绪
5. `GET/POST/PUT /api/products`（Seller 角色）就绪
6. `GET /api/seller/orders` + `POST /api/seller/orders/{id}/ship` + `GET /api/orders/{id}/logistics-trace` 就绪
7. `GET /api/seller/after-sales` + `/api/seller/after-sales/{id}` + 3 个审核操作端点就绪（ReviewAfterSales 合并版）
8. `GET /api/seller/reviews` + `POST /api/reviews/{id}/reply` 就绪（ReviewAfterSales 合并版）
9. `GET/POST /api/seller/freight-templates` + `GET /api/seller/logistics-companies` 就绪
10. `GET /api/notifications` + `unread-count` + `read` + `read-all` 就绪
11. 后端 CORS 允许 `localhost:5174`（dev）或经网关同源（prod）
12. 后端 `Idempotency-Key` 头识别（cosmetic 保留，不强制消费）+ 409 乐观锁冲突响应格式 `{ currentVersion: number }`

若任一依赖未就绪，对应页面降级为 mock 数据 + 「后端未就绪」徽标，不阻塞其他页面交付。

## 8 验收标准

按阶段与维度给出可勾选验收项，每项对应 design-prompt §8 验收要点与本 spec 设计点。

### 8.1 全局架构

- [ ] `web/seller/` 目录按 §1.2 创建
- [ ] `pnpm dev` 启动成功，端口 5174，`/api` 代理到 `localhost:5001`
- [ ] `pnpm build` 产物 `dist/` 生成，无 TypeScript 错误
- [ ] `pnpm lint`、`pnpm typecheck`、`pnpm test` 全部通过
- [ ] CI `web-seller` job 通过

### 8.2 鉴权与路由

- [ ] `/login` 页账号密码登录成功后跳 `/dashboard/overview`
- [ ] 未登录访问受保护路由跳 `/login?redirect=...`
- [ ] 登录后刷新页面，token 与 user 从 localStorage 恢复
- [ ] `Seller` 角色可见所有菜单；非卖家角色跳 `/403`
- [ ] 无权限按钮被 `v-permission` 隐藏
- [ ] 401 自动跳 `/login`；403 跳 `/403`
- [ ] `shopStatus === 'Suspended'` 时禁止访问新增/编辑商品路由，引导跳 `/shop/application`
- [ ] Header 待办徽标显示待发货/售后待处理数，点击正确跳转

### 8.3 P0 12 页覆盖

- [ ] 02-dashboard 3 页全部可访问，图表正常渲染（low-stock 走 mock + 徽标）
- [ ] 03-product-management 4 页全部可访问，CRUD 操作正常
- [ ] 05-order-fulfillment 3 页全部可访问，发货操作携带 Idempotency-Key
- [ ] 06-after-sales 2 页全部可访问，审核操作（approve/reject/confirm-return）正常

### 8.4 P1 10 页覆盖

- [ ] 01-onboarding 4 页全部可访问，入驻申请与资质上传正常
- [ ] 04-logistics 2 页全部可访问，运费模板 CRUD + 物流公司只读正常
- [ ] 07-review 1 页可访问，评价回复（`/api/reviews/{id}/reply`）正常
- [ ] 08-account 3 页全部可访问，登录页 OTP 区静态预留

### 8.5 P2 1 页覆盖

- [ ] 09-export 1 页可访问（走 mock + 「后端未就绪」徽标）

### 8.6 数据流

- [ ] 所有 API 调用走 `shared/http/client.ts`，baseURL 为 `/api`
- [ ] 响应拦截器解包 `ApiResponse.data`，调用方拿到的就是 `T`
- [ ] 成功判定 `code === 200`，非 200 抛 `BusinessError`
- [ ] 409 乐观锁冲突弹「刷新后重试」对话框
- [ ] 429 限流提示倒计时
- [ ] 5xx 显示 `<ErrorBoundary>` + traceId
- [ ] Order api `page` 默认值 `0` + TODO 标注存在（BE-1 跟踪）

### 8.7 视觉规范

- [ ] 主色 `#1677FF`、圆角 `6px`、字体栈与 design-tokens.css 一致
- [ ] Sider 深色 `#001529`，折叠至 80px
- [ ] 表格 `size="middle"`，行高 48px
- [ ] 危险操作 `ConfirmDialog` 红色确认按钮
- [ ] 加载/空/错误三态齐全
- [ ] ≥ 1200px Sider 全展开；992-1199px 折叠；< 992px 提示桌面端

### 8.8 测试

- [ ] 单元测试行覆盖 ≥ 70%（shared/utils、shared/http、shared/auth、shared/shop、各模块 api）
- [ ] 组件测试行覆盖 ≥ 60%（16 个 shared/components）
- [ ] vue-tsc 0 error
- [ ] ESLint 0 error
- [ ] E2E 至少 1 个 happy path（登录 → 工作台 → 商品列表 → 编辑 → 提交审核 → 订单待发货 → 发货）

### 8.9 后端待办跟踪

- [ ] BE-1：Order BC `page` 统一为 1 起（后端 spec 单独立项，前端将 `order.api.ts` 默认值从 `0` 改为 `1`，移除 TODO，更新测试断言）
- [ ] BE-2：补齐 `/api/seller/dashboard/low-stock`（后端 spec 单独立项，前端关闭 mock + 移除徽标）
- [ ] BE-3：补齐 `/api/seller/exports/*`（后端 spec 单独立项，前端关闭 mock + 移除徽标）
