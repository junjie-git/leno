# 系统管理后台前端设计文档

**文档版本**：V1.0
**创建日期**：2026-07-27
**所属项目**：Leno 电商平台
**文档类型**：前端实现设计 spec
**关联文档**：
- [docs/spec/12-系统管理域.md](../../spec/12-系统管理域.md) — 后端 BC11 需求文档
- [docs/design-prompts/system-admin/00-overview.md](../../design-prompts/system-admin/00-overview.md) — 28 页 UI 提示词总览
- [docs/design-prompts/shared/design-system.md](../../design-prompts/shared/design-system.md) — 共享设计系统
- [docs/design-prompts/shared/components.md](../../design-prompts/shared/components.md) — 共享组件约定
- [docs/designs/system-admin/](../../designs/system-admin/) — 28 页 HTML 设计稿
- [docs/designs/_shared/tokens.css](../../designs/_shared/tokens.css) — 设计令牌 CSS

## 0 摘要

本 spec 描述 Leno 系统管理后台前端 SPA 的实现设计。后端 SystemAdmin BC（`src/Services/SystemAdmin/`）已实装 16 个 Controller 覆盖全部 API；前端缺失，本 spec 定义其架构、模块、数据流、鉴权、视觉规范、测试与构建。

**交付物**：仅前端 Vue 3 SPA，位于 `web/system-admin/`，覆盖 28 页 7 模块，直连后端 API，账号密码登录（2FA UI 预留）。

## 1 总体架构与项目骨架

### 1.1 技术栈与版本（严格遵循 shared/design-system.md §1）

| 维度 | 选型 | 版本 |
|-|-|-|
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

### 1.2 目录骨架

```
web/system-admin/
├── public/                      # 静态资源
├── src/
│   ├── main.ts                  # 入口：createApp + 注册插件
│   ├── App.vue                  # 根组件 <RouterView>
│   ├── app/
│   │   ├── router.ts            # 聚合各模块 routes.ts + 守卫
│   │   ├── pinia.ts             # createPinia + 持久化插件
│   │   ├── provider.vue         # 全局 ConfigProvider（主色/圆角/字体）
│   │   └── env.ts               # import.meta.env 类型化封装
│   ├── shared/
│   │   ├── http/                # axios 实例、拦截器、Idempotency-Key、errors
│   │   ├── auth/                # useAuthStore、AuthGuard、permission helper
│   │   ├── layout/              # BasicLayout + SiderMenu + HeaderBar + FooterBar
│   │   ├── components/          # StatusTag、IdempotencyButton、ConfirmDialog、DataTable、PermissionGuard、EmptyState、DateTimeRangePicker、ChartLine/ChartBar/ChartPie、JsonViewer、ErrorBoundary
│   │   ├── tokens/              # design-tokens.css（来自 designs/_shared/tokens.css）+ antd theme.ts
│   │   ├── utils/               # format（日期/金额/百分比）、validators、logger
│   │   └── types/               # ApiResponse<T>、PageResult<T>、ErrorBody 等通用类型
│   ├── modules/
│   │   ├── 01-dashboard/        # 7 页
│   │   ├── 02-user-access/      # 4 页
│   │   ├── 03-system-governance/# 4 页
│   │   ├── 04-runtime-ops/      # 6 页
│   │   ├── 05-audit/            # 3 页
│   │   ├── 06-account/          # 3 页
│   │   └── 07-monitoring/       # 1 页
│   └── assets/                  # 图标、字体
├── tests/                       # Vitest setup + Playwright e2e
├── index.html
├── vite.config.ts               # proxy /api → 后端
├── tsconfig.json                # strict、paths 别名 @/
├── package.json
├── pnpm-lock.yaml
├── .env.development             # VITE_API_BASE、VITE_REQUIRE_2FA=false
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
├── routes.ts       # 本模块路由项数组，meta={title,roles,menuKey,icon,menuGroup}
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
  port: 5173,
  proxy: {
    '/api': {
      target: 'http://localhost:5001',   // SystemAdmin.Api 默认端口
      changeOrigin: true,
      // 后端若 HTTPS 自签：secure: false
    },
  },
}
```

`shared/http/client.ts` 中 `axios.create({ baseURL: '/api' })`，请求时只写相对路径。

### 1.5 模块路由聚合

`app/router.ts` 静态导入 7 个模块的 `routes.ts`，concat 后挂上 `beforeEach` 守卫：

```ts
import dashboard from '@/modules/01-dashboard/routes'
import userAccess from '@/modules/02-user-access/routes'
import systemGovernance from '@/modules/03-system-governance/routes'
import runtimeOps from '@/modules/04-runtime-ops/routes'
import audit from '@/modules/05-audit/routes'
import account from '@/modules/06-account/routes'
import monitoring from '@/modules/07-monitoring/routes'

const routes = [
  { path: '/login', component: Login2fa, meta: { anonymous: true, title: '登录' } },
  { path: '/403', component: Forbidden, meta: { anonymous: true, title: '无权访问' } },
  { path: '/404', component: NotFound, meta: { anonymous: true, title: '页面不存在' } },
  { path: '/', component: BasicLayout, children: [
    { path: '', redirect: '/dashboard/operations-overview' },
    ...dashboard,
    ...userAccess,
    ...systemGovernance,
    ...runtimeOps,
    ...audit,
    ...account,
    ...monitoring,
  ]},
  { path: '/:pathMatch(.*)*', component: NotFound },
]
```

## 2 模块拆分与页面映射

下表把 `docs/design-prompts/system-admin/` 28 个 prompt 文件逐一映射到 `modules/NN-name/views/*.vue`，并列出每个模块的核心 API（依据 design-prompts §3 + spec 12 §5）。

### 2.1 模块 01-dashboard（7 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API |
|-|-|-|-|
| `01-dashboard/operations-overview.md` | `OperationsOverview.vue` | `/dashboard/operations-overview` | `GET /api/admin/dashboard/overview` |
| `01-dashboard/payment-stats.md` | `PaymentStats.vue` | `/dashboard/payment-stats` | `GET /api/admin/dashboard/payment-stats` |
| `01-dashboard/points-stats.md` | `PointsStats.vue` | `/dashboard/points-stats` | `GET /api/admin/dashboard/points-stats` |
| `01-dashboard/notification-delivery.md` | `NotificationDelivery.vue` | `/dashboard/notification-delivery` | `GET /api/admin/dashboard/notification-delivery` |
| `01-dashboard/after-sales-stats.md` | `AfterSalesStats.vue` | `/dashboard/after-sales-stats` | `GET /api/admin/dashboard/after-sales-stats` |
| `01-dashboard/shop-ranking.md` | `ShopRanking.vue` | `/dashboard/shop-ranking` | `GET /api/admin/dashboard/shop-ranking` |
| `01-dashboard/report-snapshots.md` | `ReportSnapshots.vue` | `/dashboard/report-snapshots` | `GET /api/admin/dashboard/reports`、`GET /api/admin/dashboard/reports/{id}` |

图表组件：`@vue-echarts` 折线/柱状/饼图，主题色统一 `#1677FF`。看板卡片用 `<a-card>` + `<a-statistic>` 组合。

### 2.2 模块 02-user-access（4 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API（域拆分后归属） |
|-|-|-|-|
| `02-user-access/user-management.md` | `UserManagement.vue` | `/user-access/users` | `GET/POST/PUT /api/admin/users`（Identity 域 `AdminUsersController`） |
| `02-user-access/role-management.md` | `RoleManagement.vue` | `/user-access/roles` | `GET/POST/PUT /api/admin/roles`、`/api/admin/roles/{id}/permissions`（AccessControl 域 `AdminRolesController`） |
| `02-user-access/oauth-clients.md` | `OAuthClients.vue` | `/user-access/oauth-clients` | `GET/POST/PUT/DELETE /api/admin/oauth-clients`（Identity 域 `AdminOAuthClientsController`） |
| `02-user-access/operators.md` | `Operators.vue` | `/user-access/operators` | `GET/POST/PUT /api/admin/operators`（SystemAdmin BC `OperatorsController`） |

模块内共享组件：`RolePermissionMatrix`（角色-权限矩阵编辑器）。

### 2.3 模块 03-system-governance（4 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API |
|-|-|-|-|
| `03-system-governance/feature-flags.md` | `FeatureFlags.vue` | `/system-governance/feature-flags` | `GET/POST/PUT /api/admin/feature-flags`（SystemAdmin `FeatureFlagsController`） |
| `03-system-governance/system-configs.md` | `SystemConfigs.vue` | `/system-governance/system-configs` | `GET/PUT /api/admin/system-configs`（SystemAdmin `SystemConfigsController`） |
| `03-system-governance/data-dictionaries.md` | `DataDictionaries.vue` | `/system-governance/data-dictionaries` | `GET/POST/PUT/DELETE /api/admin/data-dictionaries`（SystemAdmin `DataDictionariesController`） |
| `03-system-governance/announcements.md` | `Announcements.vue` | `/system-governance/announcements` | `GET/POST/PUT/DELETE /api/admin/announcements`（SystemAdmin `AnnouncementsController`） |

### 2.4 模块 04-runtime-ops（6 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API |
|-|-|-|-|
| `04-runtime-ops/rate-limit-rules.md` | `RateLimitRules.vue` | `/runtime-ops/rate-limit-rules` | `GET/POST/PUT /api/admin/rate-limit-rules` + `enable/disable`（SystemAdmin `RateLimitRulesController`） |
| `04-runtime-ops/index-rebuild.md` | `IndexRebuild.vue` | `/runtime-ops/index-rebuild` | `GET/POST /api/admin/index-rebuilds`、`/{id}/retry`（SystemAdmin `IndexRebuildController`） |
| `04-runtime-ops/dead-letter-queue.md` | `DeadLetterQueue.vue` | `/runtime-ops/dead-letter-queue` | `GET /api/admin/dead-letters` + `retry/discard/batch-*`（SystemAdmin `DeadLetterController`） |
| `04-runtime-ops/scheduled-tasks.md` | `ScheduledTasks.vue` | `/runtime-ops/scheduled-tasks` | `GET/POST /api/admin/scheduled-tasks`（SystemAdmin `ScheduledTasksController`） |
| `04-runtime-ops/health-monitoring.md` | `HealthMonitoring.vue` | `/runtime-ops/health-monitoring` | `GET /api/admin/health`、`/api/admin/health/modules`（SystemAdmin `HealthController`） |
| `04-runtime-ops/alert-management.md` | `AlertManagement.vue` | `/runtime-ops/alert-management` | `GET/POST/PUT /api/admin/alerts` + `/api/admin/alert-silences`（SystemAdmin `AlertsController` + `AlertSilencesController`） |

所有写操作通过 `IdempotencyButton` 携带 `Idempotency-Key` 头；危险操作（重投/丢弃/重建触发）走 `ConfirmDialog`。

### 2.5 模块 05-audit（3 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API |
|-|-|-|-|
| `05-audit/audit-logs.md` | `AuditLogs.vue` | `/audit/audit-logs` | `GET /api/admin/audit-logs`、`/{id}`（SystemAdmin `AuditLogsController`） |
| `05-audit/reconciliation.md` | `Reconciliation.vue` | `/audit/reconciliation` | `GET /api/admin/reconciliation`（SystemAdmin，对接 `StatisticsReconciliationService`） |
| `05-audit/outbox-monitor.md` | `OutboxMonitor.vue` | `/audit/outbox-monitor` | `GET /api/admin/outbox-monitor`（SystemAdmin `OutboxMonitorController`） |

审计日志只读，不可编辑；敏感参数字段掩码展示。

### 2.6 模块 06-account（3 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API |
|-|-|-|-|
| `06-account/login-2fa.md` | `Login2fa.vue` | `/login` | `POST /api/auth/login`（Identity 域 `AuthController`）；2FA 步骤静态预留 |
| `06-account/profile.md` | `Profile.vue` | `/account/profile` | `GET/PUT /api/users/me`（Identity 域 `UsersController`） |
| `06-account/notifications.md` | `Notifications.vue` | `/account/notifications` | `GET /api/notifications`（Notification 域） |

Login2fa.vue 仅实现账号密码登录分支；OTP 输入框 UI 预留但不强制，符合既定「仅账号密码登录」决策。

### 2.7 模块 07-monitoring（1 页）

| design-prompt | Vue 视图 | 路由 path | 核心 API |
|-|-|-|-|
| `07-monitoring/prometheus-dashboard.md` | `PrometheusDashboard.vue` | `/monitoring/prometheus-dashboard` | `<iframe>` 嵌入 Grafana/Prometheus URL（来自 `SystemConfigsController` 配置项） |

### 2.8 页面与 controller 端点覆盖核对

- 28 页 ↔ 28 个 .vue ↔ 16 个 SystemAdmin controller + Identity/AccessControl/Notification 域 6 个跨域 controller（Identity: AdminUsers/AdminOAuthClients/Auth/Users；AccessControl: AdminRoles；Notification: Notifications）
- 100% 覆盖 design-prompts 列出的 API 端点
- spec 12 §5 列出的 6 类接口（Dashboard/DeadLetter/IndexRebuild/AuditLog/RateLimit/Health）全部命中

### 2.9 命名约定（遵循 `docs/conventions/naming-conventions.md` 与 `docs/design-prompts/shared/writing-guide.md`）

- 视图文件：PascalCase `.vue`，与 design-prompt 文件名 PascalCase 化对应（`dead-letter-queue.md` → `DeadLetterQueue.vue`）
- API 函数：camelCase 动词开头（`listDeadLetters`、`retryDeadLetter`、`batchDiscardDeadLetters`）
- DTO 类型：PascalCase + `Dto` 后缀（`DeadLetterMessageDto`、`BatchOperationResultDto`）
- Store：`useXxxStore`（`useAuthStore`、`useUserStore`）
- 路由 name：`{module}.{view}` kebab-case（`runtime-ops.dead-letter-queue`）

## 3 数据流与 HTTP 客户端

### 3.1 后端响应信封

后端 `docs/contracts/internal-api-contracts.md` 统一返回：

```ts
interface ApiResponse<T> {
  code: number       // 0 成功；非 0 业务错误码
  message: string    // 人类可读信息
  data: T            // 业务负载，可能为 null
  traceId?: string   // OpenTelemetry traceId
}
```

分页响应统一为：
```ts
interface PageResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
```

### 3.2 axios 实例（`shared/http/client.ts`）

```ts
const client = axios.create({
  baseURL: '/api',
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})
```

### 3.3 请求拦截器

按顺序：
1. **鉴权**：从 `useAuthStore().token` 读取，若存在且未过期 → 注入 `Authorization: Bearer {token}`
2. **幂等键**：调用方通过 `withIdempotency()` 包装时，注入 `Idempotency-Key: {uuid v4}`，存入 sessionStorage 防止同次会话重复
3. **乐观锁**：编辑场景调用方传入 `If-Match: {etag}` 或 `X-Resource-Version: {version}`（后端 spec 12 INV-SYS 中 RateLimitRule 用 `Version` 字段）
4. **traceId**：生成 `X-Request-Id`，便于后端日志关联

### 3.4 响应拦截器

按顺序：
1. **HTTP 层**：5xx → 抛 `ServerError`；401 → 清 token 并跳 `/login`；403 → 抛 `ForbiddenError`；404 → 抛 `NotFoundError`；429 → 抛 `RateLimitedError`
2. **业务层**：`code !== 0` → 抛 `BusinessError(code, message)`，由调用方或全局错误处理决定 toast 还是 inline
3. **数据解包**：成功时返回 `response.data.data`，让调用方拿到的就是 `T`
4. **traceId 透传**：失败时把 traceId 一起带出，便于 UI 展示「错误码 #xxx，traceId yyy」

### 3.5 错误类型层级（`shared/http/errors.ts`）

```ts
abstract class AppError { abstract kind: string; message: string; traceId?: string }
class NetworkError extends AppError      // 网络异常、超时
class BusinessError extends AppError     // code !== 0
class UnauthorizedError extends AppError // 401
class ForbiddenError extends AppError    // 403
class NotFoundError extends AppError     // 404
class RateLimitedError extends AppError  // 429，含 retryAfter
class ServerError extends AppError       // 5xx
class ConcurrencyError extends AppError  // 409 乐观锁冲突，含 currentVersion
```

调用方用 `try/catch (e: AppError)` 或 `if (e instanceof ConcurrencyError)` 精细化处理。

### 3.6 API 函数模板（每个模块 `api/{module}.api.ts`）

以 `04-runtime-ops/api/dead-letter.api.ts` 为例：

```ts
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { DeadLetterMessageDto, BatchOperationResultDto, DiscardDeadLetterDto } from '../types/dead-letter.dto'

export interface ListDeadLettersParams {
  sourceContext?: string[]
  status?: ('Pending' | 'Retried' | 'Discarded')[]
  startTime?: string
  endTime?: string
  page?: number
  pageSize?: number
}

export const deadLetterApi = {
  list: (params: ListDeadLettersParams) =>
    client.get<PageResult<DeadLetterMessageDto>>('/admin/dead-letters', { params }),

  get: (id: string) =>
    client.get<DeadLetterMessageDto>(`/admin/dead-letters/${id}`),

  retry: (id: string) =>
    client.post<DeadLetterMessageDto>(`/admin/dead-letters/${id}/retry`, null, withIdempotency()),

  discard: (id: string, body: DiscardDeadLetterDto) =>
    client.post<DeadLetterMessageDto>(`/admin/dead-letters/${id}/discard`, body, withIdempotency()),

  batchRetry: (messageIds: string[]) =>
    client.post<BatchOperationResultDto>('/admin/dead-letters/batch-retry', { messageIds }, withIdempotency()),

  batchDiscard: (messageIds: string[], reason: string) =>
    client.post<BatchOperationResultDto>('/admin/dead-letters/batch-discard', { messageIds, discardReason: reason }, withIdempotency()),
}
```

### 3.7 缓存策略

| 数据 | 缓存 | 过期 |
|-|-|-|
| 看板快照（report-snapshots 列表） | Pinia + sessionStorage | 5 分钟 + 手动刷新按钮 |
| 数据字典（data-dictionaries） | Pinia | 10 分钟，编辑后失效 |
| 当前用户 profile/角色权限 | Pinia + localStorage | 持久至登出 |
| 死信/审计日志/限流规则 | 不缓存 | 实时拉取 |
| 健康状态 | Pinia | 30 秒（与后端检查频率一致） |
| Prometheus iframe URL | sessionStorage | 5 分钟 |

### 3.8 乐观锁与并发

后端 spec 12 INV-SYS 中 RateLimitRule、IndexRebuildTask、DeadLetterMessage 含 `Version` 字段。前端编辑流：

1. GET 拿到资源含 `version: 3`
2. PUT 请求头 `X-Resource-Version: 3` + body 含 `version: 3`
3. 若被他人修改，后端返 409 + `{ currentVersion: 4 }`
4. 响应拦截器抛 `ConcurrencyError`，UI 弹「该规则已被他人修改，是否刷新后重试？」对话框，确认后重新 GET

### 3.9 文件上传/下载

- 上传（如公告附件、字典导入）：`Content-Type: multipart/form-data`，单文件 ≤ 10MB
- 下载（如审计日志导出、报表导出）：`responseType: 'blob'`，文件名从 `Content-Disposition` 解析

### 3.10 全局错误处理（`app/main.ts` 注册）

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
  // NetworkError/ServerError 由全局 ErrorBoundary 兜底
}
```

页面级错误：每个 view 用 `<a-result status="error">` 兜底 + 重试按钮；列表页空数据用 `<EmptyState>`。

## 4 鉴权与路由守卫

### 4.1 Auth Store（`shared/auth/auth.store.ts`）

```ts
interface AuthState {
  token: string | null
  user: AdminUserDto | null
  roles: string[]                       // ['Admin'] | ['Admin','Operator']
  permissions: string[]                 // ['dead-letter:dispose','role:write', ...]
  loginAt: number | null
  expiresAt: number | null              // token 过期时间戳（ms）
  twoFactorPending: boolean             // 永远 false（仅账号密码登录）
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
    isAdmin: (s) => s.roles.includes('Admin'),
    hasPermission: (s) => (perm: string) => s.permissions.includes(perm) || s.permissions.includes('*'),
  },
  actions: {
    async login(body: LoginDto)      // POST /api/auth/login → 存 token/user/roles/permissions
    async fetchProfile()             // GET /api/users/me → 刷新 user/permissions
    async logout()                   // 清状态 + 跳 /login + POST /api/auth/logout（best-effort）
    hasRole(roles: string[]): boolean
  },
  persist: {                          // pinia-plugin-persistedstate
    storage: localStorage,
    pick: ['token', 'user', 'roles', 'permissions', 'expiresAt'],
  },
})
```

### 4.2 登录流程（仅账号密码，2FA UI 预留）

```
Login2fa.vue
  ├─ 用户输入 username + password
  ├─ POST /api/auth/login { username, password }
  │   ├─ 200 → { token, expiresIn, user, roles, permissions }
  │   │       → useAuthStore.login() 持久化
  │   │       → router.push(redirect ?? '/dashboard/operations-overview')
  │   ├─ 401 → inline 错误「账号或密码错误」
  │   ├─ 403 → inline 错误「账号已禁用」
  │   └─ 429 → 倒计时按钮「操作过于频繁，请 N 秒后重试」
  └─ OTP 输入区静态展示 + 角标「2FA 暂未启用」，提交按钮 disabled
```

环境变量 `VITE_REQUIRE_2FA=false`（默认）。若未来后端 2FA 就绪，把 Login2fa.vue 第二步接通即可，无需改架构。

### 4.3 路由守卫（`app/router.ts`）

```ts
router.beforeEach(async (to) => {
  const auth = useAuthStore()

  // 1. 公开路由（/login、404 等）直接放行
  if (to.meta.anonymous) return true

  // 2. 未登录 → 跳 /login?redirect=to.fullPath
  if (!auth.isAuthenticated) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  // 3. 首次进入或刷新后 user 为空 → 拉取 profile（含 roles/permissions）
  if (!auth.user) {
    try { await auth.fetchProfile() }
    catch { auth.logout(); return { path: '/login' } }
  }

  // 4. 角色校验：meta.roles 与 auth.roles 取交集
  const required = (to.meta.roles ?? []) as string[]
  if (required.length && !auth.hasRole(required)) {
    return { path: '/403' }
  }

  // 5. 权限校验：meta.permission（可选）
  if (to.meta.permission && !auth.hasPermission(to.meta.permission)) {
    return { path: '/403' }
  }

  return true
})
```

### 4.4 路由元信息约定

每个模块 `routes.ts` 的路由项：
```ts
{
  path: 'dead-letter-queue',
  name: 'runtime-ops.dead-letter-queue',
  component: () => import('../views/DeadLetterQueue.vue'),
  meta: {
    title: '死信队列',           // 面包屑 + 页面标题
    menuKey: 'runtime-ops.dead-letter-queue',  // 菜单高亮
    icon: 'WarningOutlined',
    roles: ['Admin', 'Operator'], // 来自 00-overview 路由表「鉴权」列
    permission: 'dead-letter:dispose',  // 可选；只控制写按钮显示
    menuGroup: '04-runtime-ops',  // Sider 分组
  },
}
```

### 4.5 菜单渲染（`shared/layout/SiderMenu.vue`）

- 从 `router.options.routes` 中过滤 `meta.menuGroup` 不为空的路由
- 按 `menuGroup` 分组（仪表盘 / 用户与权限 / 系统治理 / 运行时运维 / 审计与对账 / 个人账号 / 系统监控）
- 每项 `v-if="auth.hasRole(item.meta.roles)"` 控制可见
- 当前路由通过 `menuKey` 匹配高亮

### 4.6 权限控制两层

**页面级**：路由 meta.roles + 守卫拦截（4.3 步骤 4）

**按钮/操作级**：通过 `<PermissionGuard>` 组件或 `v-permission` 指令

```vue
<IdempotencyButton
  v-permission="'dead-letter:dispose'"
  danger
  @click="onBatchDiscard"
>批量丢弃</IdempotencyButton>
```

`v-permission` 实现：无权限时 `el.style.display = 'none'`（不删 DOM，避免 hydration 问题）。

### 4.7 Token 刷新与登出

- **过期前刷新**：响应拦截器收到 401 时，若 `expiresAt` 即将到（< 5 分钟）且未在刷新中，则尝试 `POST /api/auth/refresh`；失败则跳 `/login`
- **登出**：清除 store + localStorage + sessionStorage（除偏好设置）；`router.push('/login')`；best-effort 调 `POST /api/auth/logout`（失败不阻塞）

### 4.8 全局搜索与通知铃铛

- **全局搜索**（Cmd/Ctrl+K）：从路由表生成可搜索项（菜单 + 端点），`<a-modal>` 弹出搜索框，选中跳转
- **通知铃铛**：Header 固定，`<a-badge :count="unread" />`，下拉显示最近 5 条告警（来自 `/api/admin/alerts?status=firing`）；点击进 `/account/notifications`

## 5 共享组件与视觉规范

### 5.1 共享组件清单（落地 `docs/design-prompts/shared/components.md`）

| 组件 | 路径 | 职责 | 关键 props |
|-|-|-|-|
| `StatusTag` | `shared/components/StatusTag.vue` | 通用状态标签 | `type`（deadLetter/orderPayment/shop 等）、`status` |
| `IdempotencyButton` | `shared/components/IdempotencyButton.vue` | 包装 `<a-button>`，点击自动注入 `Idempotency-Key`，loading 期间禁用 | `idempotencyKey?`、`loading`、`onClick` |
| `PermissionGuard` | `shared/components/PermissionGuard.vue` | 无权限时隐藏 slot 内容 | `permission` |
| `DataTable` | `shared/components/DataTable.vue` | 包装 `<a-table>`，统一分页/筛选/空态/列设置 | `columns`、`fetcher`、`rowKey` |
| `EmptyState` | `shared/components/EmptyState.vue` | 包装 `<a-empty>`，含 CTA 按钮 | `description`、`actionText?`、`@action` |
| `ConfirmDialog` | `shared/components/ConfirmDialog.vue` | 包装 `Modal.confirm`，统一危险/普通样式 | `danger`、`title`、`content`、`requireInput?` |
| `DateTimeRangePicker` | `shared/components/DateTimeRangePicker.vue` | 包装 `<a-range-picker>`，输出 ISO 8601 UTC | `value`、`@change` |
| `ChartLine` / `ChartBar` / `ChartPie` | `shared/components/charts/` | 包装 `@vue-echarts`，预设主题色与 tooltip | `series`、`xAxis`、`loading` |
| `JsonViewer` | `shared/components/JsonViewer.vue` | 死信 payload 展示，可折叠 + 语法高亮 | `data`、`maxHeight` |
| `ErrorBoundary` | `shared/components/ErrorBoundary.vue` | 包装 `<a-result status="error">`，含重试 | `#fallback` slot |

权限指令 `v-permission` 与 `<PermissionGuard>` 并存：前者用于按钮级简单隐藏，后者用于区域级包裹。

### 5.2 设计令牌（`shared/tokens/design-tokens.css`）

直接复用 `docs/designs/_shared/tokens.css` 的 CSS 变量，并补充 Ant Design Vue 4.x 的 `theme.token` 映射：

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

`app/provider.vue` 中映射到 Ant Design Vue 4.x ConfigProvider theme：
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

### 5.3 全局布局（`shared/layout/BasicLayout.vue`）

```
┌────────────────────────────────────────────────────────┐
│ Header 64px: Logo │ Breadcrumb │ Search │ Bell │ User  │
├─────────┬──────────────────────────────────────────────┤
│ Sider   │ Content (padding 24px)                       │
│ 200px   │   <RouterView />                             │
│ #001529 │                                              │
│         │                                              │
├─────────┴──────────────────────────────────────────────┤
│ Footer 32px: © Leno · v1.0.0                           │
└────────────────────────────────────────────────────────┘
```

- Header 固定顶部、Sider 固定左侧（`position: fixed`）
- Content `margin-left: 200px; margin-top: 64px;`
- Sider 折叠至 80px（`<a-layout-sider collapsible :collapsed-width="80">`），992-1199px 自动折叠
- <992px 显示「请使用桌面端访问」提示页

### 5.4 表格密度与样式

- 表格统一 `size="middle"`、`rowKey` 显式声明
- 列宽：状态列 100px、时间列 160px、操作列按按钮数（80-200px）
- 操作列用 `<a-space>` 包裹 `<a-button type="link" size="small">`
- 行高 48px，hover 背景 `#FAFAFA`

### 5.5 状态色映射（StatusTag type=status）

| 业务状态 | 颜色 | Ant tag color |
|-|-|-|
| 待处理（死信/任务） | 警告黄 | `warning` |
| 已重投/已支付/审核通过/启用 | 成功绿 | `success` |
| 已丢弃/已封禁/失败/不健康 | 错误红 | `error` |
| 进行中/执行中 | 主色蓝 | `processing` |
| 已取消/默认/已关闭 | 中性灰 | `default` |

### 5.6 字体与图标

- 字体栈：`"PingFang SC","Microsoft YaHei",-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif`
- 等宽字体（消息ID/JSON）：`"SF Mono","Cascadia Code",Consolas,monospace`
- 图标：`@ant-design/icons-vue`，尺寸 16/20/24/32，颜色 `currentColor`

### 5.7 危险操作确认（落地 00-overview §5）

- 删除/丢弃/重投/封禁/重建触发 → 必走 `ConfirmDialog`，`danger: true`，确认按钮 `#FF4D4F`
- 丢弃/封禁类需填理由 → `requireInput: { label: '丢弃原因', min: 1, max: 500 }`，未填禁用确认按钮
- 批量操作前弹「将影响 N 条，是否继续？」

### 5.8 加载/空/错误三态

| 状态 | 实现 |
|-|-|
| 加载中 | 表格用 `<a-skeleton :rows="5">`；卡片用 `<a-skeleton active />`；详情抽屉用 `<a-spin />` |
| 空数据 | `<EmptyState description="暂无xxx" actionText="刷新" @action="reload" />` |
| 错误 | `<ErrorBoundary>` 兜底，展示「加载失败 #traceId」+ 重试按钮 |
| 网络错误 | `message.error('网络异常，请检查连接')` 3s 自动消失 |
| 403 | 跳 `/403` 专用页 |

### 5.9 响应式断点（00-overview §4）

- ≥ 1200px：Sider 全展开 200px
- 992-1199px：Sider 折叠 80px
- < 992px：显示「请使用桌面端访问」全屏提示，不渲染主应用

## 6 测试、构建与可观测

### 6.1 测试分层

| 层级 | 工具 | 覆盖范围 | 覆盖率门槛 |
|-|-|-|-|
| 单元测试 | Vitest 2.x | `shared/utils/`、`shared/http/` 拦截器逻辑、`shared/auth/` store 状态机、各模块 `api/*.api.ts` URL/参数构造 | 行覆盖 ≥ 70% |
| 组件测试 | Vitest + @vue/test-utils 2.x + jsdom | `shared/components/*` 11 个组件props/emit/slot 行为 | 行覆盖 ≥ 60% |
| 类型检查 | `vue-tsc --noEmit` | 全量 .vue 与 .ts | 0 error |
| Lint | ESLint 9 + eslint-plugin-vue + @typescript-eslint | 全量代码 | 0 error，warn ≤ 阈值 |
| E2E（可选） | Playwright 1.x | 登录 → 仪表盘 → 死信列表 → 详情 → 重投 关键路径 | 至少 1 个 happy path |
| 视觉回归（可选） | Playwright screenshot | 28 个页面截图对比 | 后期补 |

### 6.2 单元测试约定

- 文件命名：`*.spec.ts` 与源码同目录
- API 测试用 `vi.mock('axios')` 或 `msw` 拦截，断言 URL/method/params/headers
- Store 测试用 `setActivePinia(createPinia())` 隔离
- 时间相关用 `vi.useFakeTimers()`

示例（dead-letter.api.spec.ts）：
```ts
it('retry 注入 Idempotency-Key 头', async () => {
  const mock = vi.spyOn(client, 'post').mockResolvedValue({ data: {} })
  await deadLetterApi.retry('msg-1')
  expect(mock).toHaveBeenCalledWith('/admin/dead-letters/msg-1/retry', null,
    expect.objectContaining({ headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }) }))
})
```

### 6.3 Vite 配置要点（`vite.config.ts`）

```ts
export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5001', changeOrigin: true },
    },
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

### 6.4 TypeScript 配置（`tsconfig.json`）

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

### 6.5 环境变量

`.env.development`：
```
VITE_API_BASE=/api
VITE_API_TARGET=http://localhost:5001
VITE_REQUIRE_2FA=false
VITE_APP_VERSION=dev
```

`.env.production`：
```
VITE_API_BASE=/api
VITE_REQUIRE_2FA=false
VITE_APP_VERSION=1.0.0
```

类型化封装 `app/env.ts`：
```ts
export const env = {
  apiBase: import.meta.env.VITE_API_BASE,
  require2FA: import.meta.env.VITE_REQUIRE_2FA === 'true',
  appVersion: import.meta.env.VITE_APP_VERSION,
} as const
```

### 6.6 CI 集成（`.github/workflows/ci.yml` 增量）

新增前端 job：
```yaml
web-system-admin:
  runs-on: ubuntu-latest
  defaults: { run: { working-directory: web/system-admin } }
  steps:
    - uses: actions/checkout@v4
    - uses: pnpm/action-setup@v4
      with: { version: 9 }
    - uses: actions/setup-node@v4
      with: { node-version: 20, cache: pnpm, cache-dependency-path: web/system-admin/pnpm-lock.yaml }
    - run: pnpm install --frozen-lockfile
    - run: pnpm lint
    - run: pnpm typecheck
    - run: pnpm test -- --coverage --reporter=dot
    - run: pnpm build
    - uses: actions/upload-artifact@v4
      with: { name: web-dist, path: web/system-admin/dist }
```

### 6.7 package.json scripts

```json
{
  "scripts": {
    "dev": "vite",
    "build": "vue-tsc --noEmit && vite build",
    "preview": "vite preview",
    "lint": "eslint . --ext .ts,.vue --max-warnings 0",
    "lint:fix": "eslint . --ext .ts,.vue --fix",
    "typecheck": "vue-tsc --noEmit",
    "test": "vitest run",
    "test:watch": "vitest",
    "test:coverage": "vitest run --coverage",
    "e2e": "playwright test"
  }
}
```

### 6.8 可观测性

| 维度 | 实现 |
|-|-|
| 前端日志 | `shared/utils/logger.ts`：dev 环境写 console，prod 环境批量 POST 到 `/api/admin/audit-logs/frontend`（best-effort） |
| 错误追踪 | `app.config.errorHandler` + `window.addEventListener('unhandledrejection')` 统一捕获，注入 traceId，未来接 Sentry 时只需替换 transport |
| 性能监控 | `web-vitals` 库上报 LCP/CLS/INP 到后端 `/api/admin/health/web-vitals` |
| traceId 传播 | 每次请求生成 `X-Request-Id`；响应头 `traceparent`（后端 OpenTelemetry）写入 store 供错误展示 |
| 用户行为审计 | 关键写操作（重投/丢弃/限流变更/索引重建）成功后通过 `POST /api/admin/audit-logs` 补充前端视角（后端已有审计中间件，前端只补 traceId） |

### 6.9 性能预算（验收门槛）

| 指标 | 目标 |
|-|-|
| 首屏 LCP（生产构建） | < 2.5s（1440p 桌面、4G 模拟） |
| 路由切换 | < 300ms（含数据加载） |
| 表格 > 100 行 | 启用 `virtual-scroll` |
| 产物体积（gzip） | 主 chunk < 200KB，Antd chunk < 350KB，ECharts chunk < 300KB |
| 防抖节流 | 搜索输入 300ms debounce；窗口 resize 100ms throttle |

### 6.10 可访问性

- 所有交互元素键盘可达，`Tab` 顺序符合视觉顺序
- 颜色对比度 ≥ WCAG AA（主色 `#1677FF` on white = 4.5:1 通过）
- 表单控件 `<label>` 关联，错误提示 `aria-describedby`
- 图标按钮 `aria-label`，状态变化 `aria-live="polite"`
- 对话框聚焦管理（打开时聚焦首个输入，关闭时还原触发元素）

## 7 验收标准

按模块给出可勾选验收项，每项对应 design-prompt §8 验收要点与本 spec 设计点。

### 7.1 全局架构

- [ ] `web/system-admin/` 目录按 §1.2 创建
- [ ] `pnpm dev` 启动成功，`/api` 代理到 `localhost:5001`
- [ ] `pnpm build` 产物 `dist/` 生成，无 TypeScript 错误
- [ ] `pnpm lint`、`pnpm typecheck`、`pnpm test` 全部通过
- [ ] CI `web-system-admin` job 通过

### 7.2 鉴权与路由

- [ ] `/login` 页账号密码登录成功后跳 `/dashboard/operations-overview`
- [ ] 未登录访问受保护路由跳 `/login?redirect=...`
- [ ] 登录后刷新页面，token 与 user 从 localStorage 恢复
- [ ] `Admin` 角色可见所有菜单；`Operator` 角色按 meta.roles 过滤
- [ ] 无权限按钮被 `v-permission` 隐藏
- [ ] 401 自动跳 `/login`；403 跳 `/403`

### 7.3 28 页覆盖

- [ ] 01-dashboard 7 页全部可访问，图表正常渲染
- [ ] 02-user-access 4 页全部可访问，CRUD 操作正常
- [ ] 03-system-governance 4 页全部可访问，CRUD 操作正常
- [ ] 04-runtime-ops 6 页全部可访问，写操作携带 Idempotency-Key
- [ ] 05-audit 3 页全部可访问，审计日志只读
- [ ] 06-account 3 页全部可访问，登录页 OTP 区静态预留
- [ ] 07-monitoring 1 页 iframe 嵌入正常

### 7.4 数据流

- [ ] 所有 API 调用走 `shared/http/client.ts`，baseURL 为 `/api`
- [ ] 响应拦截器解包 `ApiResponse.data`，调用方拿到的就是 `T`
- [ ] 409 乐观锁冲突弹「刷新后重试」对话框
- [ ] 429 限流提示倒计时
- [ ] 5xx 显示 `<ErrorBoundary>` + traceId

### 7.5 视觉规范

- [ ] 主色 `#1677FF`、圆角 `6px`、字体栈与 design-tokens.css 一致
- [ ] Sider 深色 `#001529`，折叠至 80px
- [ ] 表格 `size="middle"`，行高 48px
- [ ] 危险操作 `ConfirmDialog` 红色确认按钮
- [ ] 加载/空/错误三态齐全
- [ ] ≥ 1200px Sider 全展开；992-1199px 折叠；< 992px 提示桌面端

### 7.6 测试

- [ ] 单元测试行覆盖 ≥ 70%（shared/utils、shared/http、shared/auth、各模块 api）
- [ ] 组件测试行覆盖 ≥ 60%（11 个 shared/components）
- [ ] vue-tsc 0 error
- [ ] ESLint 0 error
- [ ] E2E 至少 1 个 happy path（登录 → 仪表盘 → 死信列表 → 详情）

## 8 不在范围内

明确以下事项不在本 spec 范围内：

- 后端 SystemAdmin BC 代码改动（如需新增端点，单独提 spec）
- 后端 Identity/AccessControl/Notification 域代码改动
- 移动端 / 平板端适配（00-overview 明确不支持移动端）
- 国际化（i18n）— 当前仅中文
- 暗色主题切换（仅预留 ConfigProvider 切换点，不实现）
- PWA / 离线缓存
- 实时推送（WebSocket / SSE）— 健康监控、告警均用 30s 轮询
- Sentry / APM 接入 — 仅预留 transport，不接入

## 9 风险与缓解

| 风险 | 影响 | 缓解 |
|-|-|-|
| 后端某些端点尚未实装或字段缺失 | 前端页面报错或字段空 | 列出端点实装度对照表（实施前生成），缺失端点用 MSW mock 兜底，标记 TODO 跟踪 |
| 后端 CORS 未配置 | 浏览器拦截请求 | Vite dev proxy 已绕过；生产环境通过 Nginx/网关同源转发 |
| 28 页规模大，实施周期长 | 长尾风险 | 按 P0 → P1 → P2 分阶段实施，每阶段独立可发布 |
| Ant Design Vue 4.x 与 Vue 3.5 兼容性 bug | 表格/表单异常 | 锁定 patch 版本，问题版本回退 |
| 后端 2FA 接口未来就绪 | Login2fa.vue 需改造 | 通过 `VITE_REQUIRE_2FA` 开关切换，UI 已预留 OTP 区 |
| Prometheus iframe 跨域 | 嵌入失败 | 通过后端 `SystemConfigs` 配置项动态获取 URL；若跨域不可解，改为「打开新窗口」链接 |

## 10 实施前依赖确认

实施前需确认以下后端依赖就绪（在 implementation plan 阶段生成对照表）：

1. `POST /api/auth/login` 返回结构含 `{ token, expiresIn, user, roles, permissions }`
2. `GET /api/users/me` 返回当前用户 profile + permissions
3. `GET /api/admin/dashboard/*` 7 个端点就绪
4. `GET /api/admin/dead-letters` 等 04-runtime-ops 6 类端点就绪
5. `GET /api/admin/audit-logs` 等审计端点就绪
6. 后端 CORS 允许 `localhost:5173`（dev）或经网关同源（prod）
7. 后端 `Idempotency-Key` 头识别 + 409 乐观锁冲突响应格式 `{ currentVersion: number }`

若任一依赖未就绪，对应页面降级为 mock 数据 + 警告标识，不阻塞其他页面交付。
