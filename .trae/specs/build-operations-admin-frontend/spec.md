# 运营管理后台前端工程（web/operations）Spec

## Why
Leno 平台已有 system-admin（系统管理后台）与 seller（商家后台）两个 Vue 3 前端工程，但运营管理后台（商品审核、促销配置、卖家治理、订单售后处置、支付对账、通知运营、会员维护等 36 个业务页面）尚无对应前端实现。`docs/design-prompts/operations/` 已给出全部页面的功能与 API 提示词，`docs/designs/operations/` 已给出 39 个页面的视觉设计稿，需要新建 `web/operations` 工程将二者落地为可运行程序。

## What Changes
- **新增** `web/operations` Vue 3 + TS + Vite + Ant Design Vue + Pinia 前端工程（包名 `@leno/operations`，dev 端口 5175）
- **修改** `pnpm-workspace.yaml`：packages 追加 `web/operations`
- 实现 10 个业务模块共 **36 个业务页面** + 5 个框架页（403/404/500/维护/限流），与设计稿目录一一对应
- 实现共享层：axios 封装（token 注入 / Idempotency-Key / X-Request-Id / 错误分类）、auth store + 路由守卫（Operator/Admin 角色）、BasicLayout（Header 64px + Sider 200px 深色 #001529）、共享组件（DataTable / StatusTag / IdempotencyButton / ConfirmDialog / 图表组件等）
- 每个模块固定结构：`api/`（axios 客户端 + spec 单测）+ `types/`（DTO）+ `views/` + `routes.ts` + `index.ts`
- 后端 API 缺口的降级策略（不新增后端代码）：
  - seller-statistics：复用 `GET /api/admin/dashboard/shop-ranking` + `GET /api/admin/shops` 前端二次聚合
  - todo-workbench：并行请求 5 个既有列表端点聚合待办
  - export-center：基于既有列表端点同步拉取并前端生成导出文件（<10000 行）

### 架构决策（对齐既有两工程，消除其风格分裂）
| 决策点 | 采用方案 |
|-|-|
| 路由策略 | 静态聚合（seller 风格），不做动态菜单，无 menu store |
| HTTP 出口 | 仅 `client` 命名（system-admin 风格），不导出 `http` 别名 |
| API 客户端返回 | AxiosResponse，调用方解构 `.data` |
| pinia.ts | named export |
| main.ts | system-admin 三态错误分流（Business/Concurrency/RateLimited）+ seller 双重 mock 守卫（`DEV && VITE_USE_MOCK` 动态 import） |
| ECharts | 统一走 `charts/Chart*` 包装组件，不全局注册 |
| BasicLayout | system-admin 风格（自管 collapsed + CSS margin） |
| dev 端口 / 代理 | 5175；`/api` → `http://localhost:5001` |

## Impact
- Affected specs: 无既有 spec 受影响（新工程独立）
- Affected code:
  - 新增 `web/operations/**`（全部源码）
  - 修改 `/workspace/pnpm-workspace.yaml`（追加一行）
  - 不修改任何后端代码与既有前端工程

## ADDED Requirements

### Requirement: 工程骨架与工作区集成
系统 SHALL 以 `@leno/operations` 包名注册进 pnpm workspace，技术栈与 system-admin/seller 完全一致（Vue ^3.5 / ant-design-vue ^4.2 / pinia ^2.3 / vue-router ^4.5 / axios ^1.7 / echarts ^5.5 / vitest ^2.1），`pnpm install` 后 `pnpm dev`（5175）、`pnpm build`、`pnpm typecheck`、`pnpm lint`、`pnpm test` 全部可执行。

#### Scenario: 工程可构建
- **WHEN** 在 `web/operations` 执行 `pnpm build`
- **THEN** 构建成功产出 `dist/`，无类型错误

### Requirement: 鉴权与路由守卫
系统 SHALL 提供 `/login` 登录页（调用 `POST /api/auth/login`）与全局 `beforeEach` 守卫：未登录访问受保护路由跳 `/login?redirect=`；角色非 Operator/Admin 跳 `/403`；`meta.permission` 校验失败跳 `/403`；token 过期自动登出。Token 持久化至 localStorage（pinia-plugin-persistedstate，仅持久化必要字段）。

#### Scenario: 登录闭环
- **WHEN** 用户在 `/login` 输入正确凭证提交
- **THEN** 调用 `/api/auth/login` 成功后存储 token/user/roles/permissions，跳转 `redirect` 参数指定页面

#### Scenario: 越权拦截
- **WHEN** 未登录或角色不足的用户访问 `/product-ops/product-audit`
- **THEN** 分别重定向到 `/login` 与 `/403`

### Requirement: 全局布局与导航
系统 SHALL 提供 BasicLayout（Header 64px 含 Logo/折叠按钮/面包屑/通知铃铛/用户下拉，Sider 200px 深色 `#001529` 可折叠至 80px，Content padding 24px，Footer 32px），侧栏按 10 个一级菜单分组（数据看板/商品运营/促销运营/卖家运营/订单运营/支付运营/通知运营/会员运营/个人中心/数据导出），≥1200px 全展开、992-1199px 自动折叠、<992px 提示使用桌面端。

#### Scenario: 菜单导航
- **WHEN** 登录后访问 `/`
- **THEN** 重定向至 `/dashboard/overview`，侧栏 10 个分组按序展示，当前路由菜单项高亮

### Requirement: HTTP 层与幂等
系统 SHALL 通过统一 axios 客户端（baseURL `/api`，timeout 15s）注入 `Authorization: Bearer`、`X-Request-Id`，写操作（POST/PUT/DELETE）携带 `Idempotency-Key`；响应按 `code !== 200` 抛 `BusinessError`，401/403/404/409/429/5xx 映射为强类型错误并解包 `data`。

#### Scenario: 业务错误抛出
- **WHEN** 后端返回 HTTP 200 但 `code !== 200`
- **THEN** 调用方捕获 `BusinessError`（含 message 与 traceId），页面提示错误信息

### Requirement: 36 个业务页面
系统 SHALL 按 `docs/designs/operations/` 设计稿实现以下页面（视觉对齐 Ant Design 风格：主色 #1677FF、圆角 6/8px、表格行高 48px、看板数值 24px semibold），API 按 `docs/design-prompts/operations/` 各文件「数据与 API」段对接：

| 模块 | 页面（路由） | 核心端点 |
|-|-|-|
| 01-dashboard | 运营总览 `/dashboard/overview`、支付统计 `/dashboard/payment-stats`、积分统计 `/dashboard/points-stats`、通知送达率 `/dashboard/notification-delivery`、售后统计 `/dashboard/after-sales-stats`、店铺排行 `/dashboard/shop-ranking` | `GET /api/admin/dashboard/*`（6 个）、`GET /api/admin/notifications/statistics` |
| 02-product-ops | 商品审核 `/product-ops/product-audit`、品牌管理 `/product-ops/brand-management`、分类管理 `/product-ops/category-management` | `/api/admin/products/*`（审核/驳回/库存）、`/api/admin/brands*` + `GET /api/brands`、`/api/admin/categories*` + `GET /api/categories/tree` |
| 03-promotion-ops | 促销活动 `/promotion-ops/promotions`、优惠券 `/promotion-ops/coupons`、秒杀活动 `/promotion-ops/seckill` | `/api/admin/promotions*`（含 activate/pause/close 状态机）、`/api/admin/coupons*`（含 publish/stop/issue）、`/api/admin/seckill/activities*` |
| 04-seller-ops | 入驻审核 `/seller-ops/application-audit`、店铺治理 `/seller-ops/shop-governance`、卖家统计 `/seller-ops/seller-statistics` | `/api/admin/shops*`（approve/reject/suspend/resume/close/资质审核）、复用 shop-ranking 二次聚合 |
| 05-order-ops | 订单管理 `/order-ops/orders`、售后处理 `/order-ops/after-sales`、评价审核 `/order-ops/review-audit`、物流公司 `/order-ops/logistics-companies` | `/api/admin/orders*`（force-cancel）、`/api/admin/after-sales*`、`/api/admin/reviews*`、`/api/admin/logistics-companies*` |
| 06-payment-ops | 支付记录 `/payment-ops/payment-records`、退款记录 `/payment-ops/refund-records`、支付渠道 `/payment-ops/payment-channels`、渠道对账 `/payment-ops/reconciliation` | `/api/admin/payments`、`/api/admin/refunds`、`/api/admin/payment-channels*`、`/api/admin/reconciliation/diffs` + `trigger` |
| 07-notification-ops | 通知模板 `/notification-ops/templates`、通知记录 `/notification-ops/records`、通知配置 `/notification-ops/config`、通知限流 `/notification-ops/rate-limits`、死信管理 `/notification-ops/dead-letters` | `/api/admin/notification-templates*`（含 preview）、`/api/notifications/records*` + `/api/admin/notifications/records/{id}/resend`、`/api/admin/notification-config*`、`/api/admin/notification-rate-limits*`、`/api/admin/dead-letters*`（batch-resend/batch-discard） |
| 08-membership-ops | 会员等级 `/membership-ops/member-levels`、会员套餐 `/membership-ops/membership-packages`、积分规则 `/membership-ops/points-rules` | `/api/admin/members/levels*`、`/api/admin/membership-packages*` + `GET /api/membership-packages`、`/api/admin/points/rules*` + `/api/admin/points/award` |
| 09-account | 登录 `/login`、待办工作台 `/account/todo`、个人资料 `/account/profile`、通知中心 `/account/notifications` | `/api/auth/*`、5 端点并行聚合、`/api/users/me*`（含双因子/密码/外部登录）、`/api/notifications*`（unread-count/read/read-all） |
| 10-data-export | 导出中心 `/data-export/export-center` | 基于既有列表端点同步拉取 + 前端生成导出（降级方案，<10000 行） |

#### Scenario: 商品审核闭环
- **WHEN** 运营在商品审核页筛选 `status=PendingAudit`，点击某商品「通过」
- **THEN** 调用 `POST /api/admin/products/{id}/approve`（携带 Idempotency-Key），成功后列表刷新、状态变更为已上架

#### Scenario: 终态操作强制确认
- **WHEN** 运营对促销活动点击「关闭」或对店铺点击「关闭」
- **THEN** 弹出 danger 二次确认对话框，确认后调用对应 close 端点，操作后状态不可逆

#### Scenario: 驳回必填原因
- **WHEN** 运营驳回商品/入驻申请/售后/评价时未填写原因提交
- **THEN** 表单校验阻止提交；填写后调用对应 reject/hide 端点并关闭对话框

### Requirement: 框架页
系统 SHALL 实现 5 个框架页：404 `/404`、403 `/403`、500 `/500`、维护中 `/maintenance`、限流 `/rate-limited`（对齐设计稿 `11-framework/`），未匹配路由兜底跳 404。

#### Scenario: 404 兜底
- **WHEN** 访问未注册路由 `/xxx`
- **THEN** 渲染 404 页面并提供返回首页按钮

### Requirement: 测试与质量门槛
系统 SHALL 为每个 `*.api.ts` 提供 axios-mock-adapter 单测，为共享组件/auth/http 层提供单测，e2e 提供登录冒烟用例；`pnpm test` 覆盖率门槛（lines/functions/statements 70%、branches 60%）通过；`pnpm lint` 与 `pnpm typecheck` 零错误。

#### Scenario: 全量质量验证
- **WHEN** 执行 `pnpm lint && pnpm typecheck && pnpm test && pnpm build`
- **THEN** 四项全部通过退出码 0

### Requirement: 零占位符
全部交付代码 SHALL 无 TODO/FIXME/占位注释/空函数体/未实现分支；所有页面为完整可用实现（含边界校验、加载/空/错误三态）。

## MODIFIED Requirements
无（新工程，不修改既有需求）。

## REMOVED Requirements
无。
