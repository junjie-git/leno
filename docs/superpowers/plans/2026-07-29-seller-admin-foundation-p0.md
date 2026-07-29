# 卖家管理后台 Foundation + P0 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建 `web/seller/` Vue 3 SPA 脚手架与共享基础设施，并实现 P0 阶段 12 个核心业务页面（工作台 3 + 商品 4 + 订单 3 + 售后 2），产出可运行、可测试的卖家管理后台。

**Architecture:** 从 `web/system-admin/` 复制 `shared/` 作为基线（http/auth/components/layout/tokens/utils），新增 `shop.store.ts`（店铺状态门禁）与 `TodoBadge`/`ShopStatusGuard` 两个卖家特有组件。鉴权走 `permissions[]` 后端驱动 + `v-permission` 指令。API 直连后端 `/api`，`code === 200` 成功判定。静态路由 + permission 过滤菜单可见性（无后端菜单 API，等同 system-admin 静态回退模式）。

**Tech Stack:** Vue 3.5 + TypeScript 5 + Vite 6 + Ant Design Vue 4 + Pinia 2 + Vue Router 4 + axios 1.7 + ECharts 5.5 + Vitest 2

**关联 spec:** [2026-07-29-seller-admin-frontend-design.md](../specs/2026-07-29-seller-admin-frontend-design.md)

---

## 文件结构总览

### 新建文件（Foundation）

```
web/seller/
├── package.json
├── vite.config.ts
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── eslint.config.js
├── index.html
├── .env.development
├── .env.production
├── .gitignore
├── playwright.config.ts
├── tests/setup.ts
└── src/
    ├── main.ts
    ├── App.vue
    ├── app/
    │   ├── env.ts
    │   ├── pinia.ts
    │   ├── provider.vue
    │   └── router.ts
    ├── shared/
    │   ├── http/
    │   │   ├── client.ts          # 从 system-admin 复制
    │   │   ├── errors.ts           # 从 system-admin 复制
    │   │   ├── idempotency.ts      # 从 system-admin 复制
    │   │   ├── index.ts            # 从 system-admin 复制
    │   │   └── mock/
    │   │       └── index.ts        # passThrough 兜底
    │   ├── auth/
    │   │   ├── auth.store.ts       # 从 system-admin 改造（Seller 角色 + shopId）
    │   │   ├── permission.ts       # 从 system-admin 复制
    │   │   ├── PermissionGuard.vue # 从 system-admin 复制
    │   │   └── index.ts
    │   ├── shop/
    │   │   ├── shop.store.ts       # 新增
    │   │   └── index.ts
    │   ├── types/
    │   │   └── index.ts            # 从 system-admin 复制
    │   ├── tokens/
    │   │   ├── design-tokens.css   # 从 system-admin 复制
    │   │   └── antd-theme.ts       # 从 system-admin 复制
    │   ├── utils/
    │   │   ├── format.ts           # 从 system-admin 复制
    │   │   ├── validators.ts       # 从 system-admin 复制
    │   │   └── logger.ts           # 从 system-admin 复制
    │   ├── components/
    │   │   ├── StatusTag.vue       # 从 system-admin 复制 + 扩展卖家状态映射
    │   │   ├── IdempotencyButton.vue
    │   │   ├── ConfirmDialog.vue
    │   │   ├── DataTable.vue
    │   │   ├── EmptyState.vue
    │   │   ├── ErrorBoundary.vue
    │   │   ├── DateTimeRangePicker.vue
    │   │   ├── JsonViewer.vue
    │   │   ├── PasswordStrengthIndicator.vue
    │   │   ├── StatisticCard.vue
    │   │   ├── DashboardCard.vue
    │   │   ├── TodoBadge.vue        # 新增
    │   │   ├── ShopStatusGuard.vue  # 新增
    │   │   ├── charts/
    │   │   │   ├── ChartLine.vue
    │   │   │   ├── ChartBar.vue
    │   │   │   └── ChartPie.vue
    │   │   └── index.ts
    │   ├── layout/
    │   │   ├── BasicLayout.vue
    │   │   ├── SiderMenu.vue
    │   │   ├── HeaderBar.vue
    │   │   └── FooterBar.vue
    │   └── pages/
    │       ├── Forbidden.vue
    │       ├── NotFound.vue
    │       ├── Maintenance.vue
    │       ├── RateLimited.vue
    │       └── ServerError.vue
    └── modules/
        ├── 08-account/             # Login（P1 但 Foundation 必需）
        │   ├── api/auth.api.ts
        │   ├── types/auth.dto.ts
        │   ├── views/Login.vue
        │   ├── routes.ts
        │   └── index.ts
        ├── 02-dashboard/
        │   ├── api/dashboard.api.ts
        │   ├── types/dashboard.dto.ts
        │   ├── views/Overview.vue
        │   ├── views/SalesTrend.vue
        │   ├── views/LowStockAlert.vue
        │   ├── routes.ts
        │   └── index.ts
        ├── 03-product-management/
        │   ├── api/product.api.ts
        │   ├── types/product.dto.ts
        │   ├── views/ProductList.vue
        │   ├── views/ProductEdit.vue
        │   ├── views/SkuManagement.vue
        │   ├── views/PriceHistory.vue
        │   ├── routes.ts
        │   └── index.ts
        ├── 05-order-fulfillment/
        │   ├── api/order.api.ts
        │   ├── types/order.dto.ts
        │   ├── views/PendingShipment.vue
        │   ├── views/OrderList.vue
        │   ├── views/LogisticsTrace.vue
        │   ├── routes.ts
        │   └── index.ts
        └── 06-after-sales/
            ├── api/aftersales.api.ts
            ├── types/aftersales.dto.ts
            ├── views/AfterSalesList.vue
            ├── views/AfterSalesDetail.vue
            ├── routes.ts
            └── index.ts
```

### 从 system-admin 复制的文件（无改动或仅改导入路径）

以下文件从 `web/system-admin/src/shared/` 复制到 `web/seller/src/shared/`，内容不变：

- `http/client.ts`、`http/errors.ts`、`http/idempotency.ts`、`http/index.ts`
- `http/mock/index.ts`（passThrough 兜底框架）
- `types/index.ts`
- `tokens/design-tokens.css`、`tokens/antd-theme.ts`
- `utils/format.ts`、`utils/validators.ts`、`utils/logger.ts`
- `auth/permission.ts`、`auth/PermissionGuard.vue`
- `components/IdempotencyButton.vue`、`components/ConfirmDialog.vue`、`components/DataTable.vue`、`components/EmptyState.vue`、`components/ErrorBoundary.vue`、`components/DateTimeRangePicker.vue`、`components/JsonViewer.vue`、`components/PasswordStrengthIndicator.vue`、`components/StatisticCard.vue`、`components/DashboardCard.vue`
- `components/charts/ChartLine.vue`、`components/charts/ChartBar.vue`、`components/charts/ChartPie.vue`

### 需改造的文件

- `auth/auth.store.ts` — `AdminUserDto` → `SellerUserDto`（增 `shopId/shopName/shopStatus`），`isAdmin` → `isSeller`，移除 `dynamicMenuEnabled`/`menusLoaded`
- `components/StatusTag.vue` — 增 `type="product"/"order"/"aftersales"/"shop"/"freightTemplate"` 状态映射
- `layout/BasicLayout.vue` — Content 区适配卖家
- `layout/SiderMenu.vue` — 静态路由 + permission 过滤（非动态菜单）
- `layout/HeaderBar.vue` — 增店铺名 + TodoBadge × 2

---

## Task 1: 项目脚手架

**Files:**
- Create: `web/seller/package.json`
- Create: `web/seller/vite.config.ts`
- Create: `web/seller/tsconfig.json`
- Create: `web/seller/tsconfig.app.json`
- Create: `web/seller/tsconfig.node.json`
- Create: `web/seller/eslint.config.js`
- Create: `web/seller/index.html`
- Create: `web/seller/.env.development`
- Create: `web/seller/.env.production`
- Create: `web/seller/.gitignore`
- Create: `web/seller/tests/setup.ts`

- [ ] **Step 1: 创建 package.json**

```json
{
  "name": "@leno/seller",
  "version": "1.0.0",
  "private": true,
  "type": "module",
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
  },
  "dependencies": {
    "@ant-design/icons-vue": "^7.0.1",
    "vue-echarts": "^7.0.3",
    "ant-design-vue": "^4.2.6",
    "axios": "^1.7.9",
    "dayjs": "^1.11.13",
    "echarts": "^5.5.1",
    "lodash-es": "^4.17.21",
    "pinia": "^2.3.0",
    "pinia-plugin-persistedstate": "^4.2.0",
    "vue": "^3.5.13",
    "vue-router": "^4.5.0",
    "web-vitals": "^4.2.4"
  },
  "devDependencies": {
    "@playwright/test": "^1.49.1",
    "@types/lodash-es": "^4.17.12",
    "@types/node": "^20.17.10",
    "@typescript-eslint/eslint-plugin": "^8.18.2",
    "@typescript-eslint/parser": "^8.18.2",
    "@vitejs/plugin-vue": "^5.2.1",
    "@vitest/coverage-v8": "^2.1.8",
    "@vue/test-utils": "^2.4.6",
    "axios-mock-adapter": "^2.1.0",
    "eslint": "^9.17.0",
    "eslint-plugin-vue": "^9.32.0",
    "jsdom": "^25.0.1",
    "typescript": "^5.7.2",
    "vite": "^6.0.5",
    "vitest": "^2.1.8",
    "vue-tsc": "^2.2.0"
  },
  "engines": {
    "node": ">=20.0.0",
    "pnpm": ">=9.0.0"
  }
}
```

- [ ] **Step 2: 创建 vite.config.ts**

```ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  server: {
    port: 5174,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
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
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './tests/setup.ts',
    exclude: [
      '**/node_modules/**',
      '**/dist/**',
      '**/cypress/**',
      '**/.{idea,git,cache,output,temp}/**',
      '**/{karma,rollup,webpack,vite,vitest,jest,ava,babel,nyc,cypress,tsup,build}.config.*',
      'tests/e2e/**',
    ],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'json-summary'],
      include: ['src/**/*.ts', 'src/**/*.vue'],
      exclude: ['src/**/*.spec.ts', 'src/main.ts', 'src/app/provider.vue'],
      thresholds: {
        lines: 70,
        functions: 70,
        branches: 60,
        statements: 70,
      },
    },
  },
})
```

- [ ] **Step 3: 创建 tsconfig.json + tsconfig.app.json + tsconfig.node.json**

`tsconfig.json`:
```json
{
  "files": [],
  "references": [
    { "path": "./tsconfig.app.json" },
    { "path": "./tsconfig.node.json" }
  ]
}
```

`tsconfig.app.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "useDefineForClassFields": true,
    "module": "ESNext",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "skipLibCheck": true,
    "moduleResolution": "Bundler",
    "allowImportingTsExtensions": true,
    "isolatedModules": true,
    "moduleDetection": "force",
    "noEmit": true,
    "jsx": "preserve",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "paths": {
      "@/*": ["./src/*"]
    },
    "types": ["vite/client", "vitest/globals"]
  },
  "include": ["src/**/*.ts", "src/**/*.d.ts", "src/**/*.tsx", "src/**/*.vue", "tests/**/*.ts"]
}
```

`tsconfig.node.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2023"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "Bundler",
    "allowImportingTsExtensions": true,
    "isolatedModules": true,
    "moduleDetection": "force",
    "noEmit": true,
    "strict": true,
    "types": ["node"]
  },
  "include": ["vite.config.ts", "playwright.config.ts"]
}
```

- [ ] **Step 4: 创建 eslint.config.js**

从 `web/system-admin/eslint.config.js` 复制，内容完全一致。

- [ ] **Step 5: 创建 index.html**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Leno 卖家管理后台</title>
</head>
<body>
  <div id="app"></div>
  <script type="module" src="/src/main.ts"></script>
</body>
</html>
```

- [ ] **Step 6: 创建环境变量文件**

`.env.development`:
```
VITE_API_BASE=/api
VITE_API_TARGET=http://localhost:5001
VITE_REQUIRE_2FA=false
VITE_USE_MOCK=true
VITE_APP_VERSION=dev
```

`.env.production`:
```
VITE_API_BASE=/api
VITE_REQUIRE_2FA=false
VITE_USE_MOCK=false
VITE_APP_VERSION=1.0.0
```

- [ ] **Step 7: 创建 .gitignore**

从 `web/system-admin/.gitignore` 复制。

- [ ] **Step 8: 创建 tests/setup.ts**

```ts
import { config } from '@vue/test-utils'

config.global.mocks = {
  $t: (key: string) => key,
}

// Mock window.matchMedia（Ant Design Vue 响应式需要）
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

// Mock IntersectionObserver
class MockIntersectionObserver {
  observe = () => {}
  unobserve = () => {}
  disconnect = () => {}
  takeRecords = () => []
}
Object.defineProperty(window, 'IntersectionObserver', {
  writable: true,
  value: MockIntersectionObserver,
})

// Mock ResizeObserver（ECharts 需要）
class MockResizeObserver {
  observe = () => {}
  unobserve = () => {}
  disconnect = () => {}
}
Object.defineProperty(window, 'ResizeObserver', {
  writable: true,
  value: MockResizeObserver,
})
```

- [ ] **Step 9: 安装依赖并验证**

Run: `cd /workspace && pnpm install`
Expected: 安装成功，无错误

- [ ] **Step 10: 验证 TypeScript 配置**

Run: `cd /workspace/web/seller && pnpm typecheck`
Expected: 无错误输出（因为还没有源文件）

- [ ] **Step 11: Commit**

```bash
cd /workspace
git add web/seller/
git commit -m "feat(seller): 搭建项目脚手架（package.json/vite/tsconfig/eslint/env）"
```

---

## Task 2: 共享类型与设计令牌

**Files:**
- Create: `web/seller/src/shared/types/index.ts`
- Create: `web/seller/src/shared/tokens/design-tokens.css`
- Create: `web/seller/src/shared/tokens/antd-theme.ts`
- Create: `web/seller/src/app/env.ts`

- [ ] **Step 1: 复制 types/index.ts**

从 `web/system-admin/src/shared/types/index.ts` 复制到 `web/seller/src/shared/types/index.ts`，内容完全一致（`ApiResponse<T>`、`PageResult<T>`、`ErrorBody` 等）。

- [ ] **Step 2: 复制 tokens/design-tokens.css**

从 `web/system-admin/src/shared/tokens/design-tokens.css` 复制。

- [ ] **Step 3: 复制 tokens/antd-theme.ts**

从 `web/system-admin/src/shared/tokens/antd-theme.ts` 复制。

- [ ] **Step 4: 创建 app/env.ts**

```ts
export const env = {
  apiBase: import.meta.env.VITE_API_BASE,
  require2FA: import.meta.env.VITE_REQUIRE_2FA === 'true',
  useMock: import.meta.env.VITE_USE_MOCK === 'true',
  appVersion: import.meta.env.VITE_APP_VERSION ?? '0.0.0',
} as const
```

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add web/seller/src/shared/types/ web/seller/src/shared/tokens/ web/seller/src/app/env.ts
git commit -m "feat(seller): 添加共享类型与设计令牌"
```

---

## Task 3: HTTP 客户端与错误处理

**Files:**
- Create: `web/seller/src/shared/http/client.ts`
- Create: `web/seller/src/shared/http/errors.ts`
- Create: `web/seller/src/shared/http/idempotency.ts`
- Create: `web/seller/src/shared/http/index.ts`
- Create: `web/seller/src/shared/http/mock/index.ts`
- Test: `web/seller/src/shared/http/client.spec.ts`
- Test: `web/seller/src/shared/http/errors.spec.ts`
- Test: `web/seller/src/shared/http/idempotency.spec.ts`

- [ ] **Step 1: 复制 HTTP 基础文件**

从 `web/system-admin/src/shared/http/` 复制以下文件到 `web/seller/src/shared/http/`，内容完全一致：
- `client.ts`
- `errors.ts`
- `idempotency.ts`
- `index.ts`
- `mock/index.ts`（passThrough 兜底框架）
- `client.spec.ts`
- `errors.spec.ts`
- `idempotency.spec.ts`

- [ ] **Step 2: 运行测试验证**

Run: `cd /workspace/web/seller && pnpm test -- --reporter=dot`
Expected: 所有 http 测试通过

- [ ] **Step 3: Commit**

```bash
cd /workspace
git add web/seller/src/shared/http/
git commit -m "feat(seller): 添加 HTTP 客户端与错误处理（从 system-admin 复制基线）"
```

---

## Task 4: 工具函数

**Files:**
- Create: `web/seller/src/shared/utils/format.ts`
- Create: `web/seller/src/shared/utils/validators.ts`
- Create: `web/seller/src/shared/utils/logger.ts`
- Test: 对应 spec 文件

- [ ] **Step 1: 复制工具函数**

从 `web/system-admin/src/shared/utils/` 复制以下文件到 `web/seller/src/shared/utils/`：
- `format.ts`、`format.spec.ts`
- `validators.ts`、`validators.spec.ts`
- `logger.ts`、`logger.spec.ts`

- [ ] **Step 2: 运行测试验证**

Run: `cd /workspace/web/seller && pnpm test -- --reporter=dot`
Expected: 所有 utils 测试通过

- [ ] **Step 3: Commit**

```bash
cd /workspace
git add web/seller/src/shared/utils/
git commit -m "feat(seller): 添加工具函数（format/validators/logger）"
```

---

## Task 5: Auth Store（Seller 角色）

**Files:**
- Create: `web/seller/src/shared/auth/auth.store.ts`
- Create: `web/seller/src/shared/auth/permission.ts`
- Create: `web/seller/src/shared/auth/PermissionGuard.vue`
- Create: `web/seller/src/shared/auth/index.ts`
- Test: `web/seller/src/shared/auth/auth.store.spec.ts`
- Test: `web/seller/src/shared/auth/permission.spec.ts`
- Test: `web/seller/src/shared/auth/PermissionGuard.spec.ts`

- [ ] **Step 1: 创建 auth.store.ts（Seller 角色 + shopId）**

```ts
import { defineStore } from 'pinia'
import { authApi } from '@/modules/08-account/api/auth.api'
import { logger } from '@/shared/utils/logger'

/**
 * 卖家用户视图（含店铺信息）
 */
export interface SellerUserDto {
  id: string
  username: string
  email: string
  phone?: string
  nickname?: string
  avatar?: string
  shopId?: string
  shopName?: string
  shopStatus?: string
  status: string
  roles: string[]
}

/**
 * 登录请求体
 */
export interface LoginDto {
  username: string
  password: string
}

/**
 * 登录响应体（与后端 AuthController.Login 返回结构对齐）
 */
export interface LoginResultDto {
  token: string
  expiresIn: number
  user: SellerUserDto
  roles: string[]
  permissions: string[]
}

/**
 * 鉴权状态
 */
export interface AuthState {
  token: string | null
  user: SellerUserDto | null
  roles: string[]
  permissions: string[]
  loginAt: number | null
  expiresAt: number | null
  /** 2FA 待处理标志，本次不接通，永远为 false */
  twoFactorPending: boolean
}

/**
 * 鉴权 Store（Seller 角色）
 *
 * - 持久化字段：token / user / roles / permissions / expiresAt
 * - login：POST /api/auth/login → 填充 state
 * - fetchProfile：GET /api/users/me → 刷新 user 与 permissions
 * - logout：best-effort 调用 /api/auth/logout，无论成败都清空 state
 */
export const useAuthStore = defineStore('auth', {
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
    isAuthenticated: (s): boolean => !!s.token && (s.expiresAt ?? 0) > Date.now(),
    isSeller: (s): boolean => s.roles.includes('Seller'),
    hasPermission: (s) => (perm: string): boolean =>
      s.permissions.includes(perm) || s.permissions.includes('*'),
  },
  actions: {
    /**
     * 登录
     */
    async login(body: LoginDto): Promise<void> {
      const result = await authApi.login(body)
      this.token = result.token
      this.user = result.user
      this.roles = result.roles
      this.permissions = result.permissions
      this.loginAt = Date.now()
      this.expiresAt = Date.now() + result.expiresIn * 1000
      this.twoFactorPending = false
    },

    /**
     * 拉取当前用户 profile，刷新 user/permissions
     */
    async fetchProfile(): Promise<void> {
      const { profile, permissions } = await authApi.getProfile()
      this.user = profile
      this.permissions = permissions
      if (profile.roles && profile.roles.length > 0) {
        this.roles = profile.roles
      }
    },

    /**
     * 登出：best-effort 调用后端 logout，失败不阻塞；最终清空 state
     */
    async logout(): Promise<void> {
      try {
        await authApi.logout()
      } catch (e) {
        logger.warn('authApi.logout 失败（忽略）', e)
      }
      this.token = null
      this.user = null
      this.roles = []
      this.permissions = []
      this.loginAt = null
      this.expiresAt = null
      this.twoFactorPending = false
    },

    /**
     * 角色校验：传入的角色列表与 store.roles 有交集则通过
     */
    hasRole(roles: string[]): boolean {
      if (roles.length === 0) return true
      return roles.some((r) => this.roles.includes(r))
    },
  },
  persist: {
    storage: localStorage,
    pick: ['token', 'user', 'roles', 'permissions', 'expiresAt'],
  },
})
```

- [ ] **Step 2: 复制 permission.ts 与 PermissionGuard.vue**

从 `web/system-admin/src/shared/auth/` 复制 `permission.ts` 和 `PermissionGuard.vue`，内容完全一致。同时复制对应的 spec 文件。

- [ ] **Step 3: 创建 auth/index.ts**

```ts
export { useAuthStore } from './auth.store'
export type { AuthState, SellerUserDto, LoginDto, LoginResultDto } from './auth.store'
export { vPermission } from './permission'
export { default as PermissionGuard } from './PermissionGuard.vue'
```

- [ ] **Step 4: 编写 auth.store.spec.ts**

```ts
import { setActivePinia, createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from './auth.store'

// Mock authApi
vi.mock('@/modules/08-account/api/auth.api', () => ({
  authApi: {
    login: vi.fn().mockResolvedValue({
      token: 'test-token',
      expiresIn: 3600,
      user: { id: '1', username: 'seller1', email: 's@test.com', status: 'Active', roles: ['Seller'], shopId: 'shop1', shopName: '测试店铺', shopStatus: 'Active' },
      roles: ['Seller'],
      permissions: ['product:list', 'order:ship'],
    }),
    getProfile: vi.fn().mockResolvedValue({
      profile: { id: '1', username: 'seller1', email: 's@test.com', status: 'Active', roles: ['Seller'] },
      permissions: ['product:list', 'order:ship'],
    }),
    logout: vi.fn().mockResolvedValue(undefined),
  },
}))

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('login 成功后填充 token/user/roles/permissions', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.token).toBe('test-token')
    expect(store.user?.shopId).toBe('shop1')
    expect(store.roles).toEqual(['Seller'])
    expect(store.permissions).toContain('product:list')
    expect(store.isAuthenticated).toBe(true)
    expect(store.isSeller).toBe(true)
    expect(store.twoFactorPending).toBe(false)
  })

  it('hasPermission 返回 true 当权限存在', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.hasPermission('product:list')).toBe(true)
    expect(store.hasPermission('product:create')).toBe(false)
  })

  it('hasPermission 返回 true 当有 * 通配权限', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    store.permissions = ['*']
    expect(store.hasPermission('any:permission')).toBe(true)
  })

  it('hasRole 返回 true 当角色匹配', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.hasRole(['Seller'])).toBe(true)
    expect(store.hasRole(['Admin'])).toBe(false)
  })

  it('hasRole 空数组返回 true（无角色要求）', () => {
    const store = useAuthStore()
    expect(store.hasRole([])).toBe(true)
  })

  it('logout 清空所有状态', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.token).toBe('test-token')
    await store.logout()
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
    expect(store.roles).toEqual([])
    expect(store.permissions).toEqual([])
  })

  it('isAuthenticated 返回 false 当 token 过期', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    store.expiresAt = Date.now() - 1000
    expect(store.isAuthenticated).toBe(false)
  })
})
```

- [ ] **Step 5: 运行测试验证**

Run: `cd /workspace/web/seller && pnpm test src/shared/auth/ --reporter=dot`
Expected: 所有 auth 测试通过

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add web/seller/src/shared/auth/
git commit -m "feat(seller): 添加 Auth Store（Seller 角色 + shopId/shopStatus）"
```

---

## Task 6: Shop Store（店铺状态门禁，新增）

**Files:**
- Create: `web/seller/src/shared/shop/shop.store.ts`
- Create: `web/seller/src/shared/shop/index.ts`
- Test: `web/seller/src/shared/shop/shop.store.spec.ts`

- [ ] **Step 1: 编写 shop.store.spec.ts（先写失败测试）**

```ts
import { setActivePinia, createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useShopStore } from './shop.store'

vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    put: vi.fn(),
  },
}))

import { client } from '@/shared/http'

describe('useShopStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('fetchMyShop 拉取店铺信息并填充 state', async () => {
    vi.mocked(client.get).mockResolvedValue({
      shopId: 'shop-1',
      shopName: '测试店铺',
      status: 'Active',
      qualificationsStatus: {},
    } as any)
    const store = useShopStore()
    await store.fetchMyShop()
    expect(store.shopId).toBe('shop-1')
    expect(store.shopName).toBe('测试店铺')
    expect(store.shopStatus).toBe('Active')
  })

  it('canPublish 返回 true 仅当 status === Active', () => {
    const store = useShopStore()
    store.shopStatus = 'Active'
    expect(store.canPublish).toBe(true)
    store.shopStatus = 'Suspended'
    expect(store.canPublish).toBe(false)
    store.shopStatus = 'PendingReview'
    expect(store.canPublish).toBe(false)
  })

  it('canFulfill 返回 true 仅当 status !== Rejected', () => {
    const store = useShopStore()
    store.shopStatus = 'Active'
    expect(store.canFulfill).toBe(true)
    store.shopStatus = 'Suspended'
    expect(store.canFulfill).toBe(true)
    store.shopStatus = 'Rejected'
    expect(store.canFulfill).toBe(false)
  })

  it('isOnboardingComplete 返回 true 仅当 status === Active', () => {
    const store = useShopStore()
    store.shopStatus = 'Active'
    expect(store.isOnboardingComplete).toBe(true)
    store.shopStatus = 'PendingReview'
    expect(store.isOnboardingComplete).toBe(false)
  })

  it('updateShop 调用 PUT /shops/me', async () => {
    vi.mocked(client.put).mockResolvedValue({} as any)
    const store = useShopStore()
    store.shopId = 'shop-1'
    await store.updateShop({ shopName: '新名称' } as any)
    expect(client.put).toHaveBeenCalledWith('/shops/me', { shopName: '新名称' })
  })
})
```

- [ ] **Step 2: 运行测试验证失败**

Run: `cd /workspace/web/seller && pnpm test src/shared/shop/ --reporter=dot`
Expected: FAIL — `shop.store.ts` 不存在

- [ ] **Step 3: 实现 shop.store.ts**

```ts
import { defineStore } from 'pinia'
import { client } from '@/shared/http'
import { logger } from '@/shared/utils/logger'

/**
 * 店铺状态枚举
 */
export type ShopStatus = 'Active' | 'Suspended' | 'PendingReview' | 'Rejected'

/**
 * 店铺信息 DTO
 */
export interface ShopDto {
  shopId: string
  shopName: string
  status: ShopStatus
  qualificationsStatus: Record<string, 'Approved' | 'Pending' | 'Rejected'>
}

/**
 * 店铺状态
 */
interface ShopState {
  shopId: string | null
  shopName: string | null
  shopStatus: ShopStatus | null
  qualificationsStatus: Record<string, 'Approved' | 'Pending' | 'Rejected'>
}

/**
 * Shop Store — 店铺状态门禁
 *
 * - canPublish: 仅 Active 态可上架商品
 * - canFulfill: 非 Rejected 态可履约既有订单
 * - isOnboardingComplete: Active 表示入驻完成
 */
export const useShopStore = defineStore('shop', {
  state: (): ShopState => ({
    shopId: null,
    shopName: null,
    shopStatus: null,
    qualificationsStatus: {},
  }),
  getters: {
    canPublish: (s): boolean => s.shopStatus === 'Active',
    canFulfill: (s): boolean => s.shopStatus !== 'Rejected',
    isOnboardingComplete: (s): boolean => s.shopStatus === 'Active',
  },
  actions: {
    /**
     * 拉取当前卖家店铺信息
     * GET /api/shops/me
     */
    async fetchMyShop(): Promise<void> {
      try {
        const shop = await client.get<ShopDto>('/shops/me')
        this.shopId = shop.shopId
        this.shopName = shop.shopName
        this.shopStatus = shop.status
        this.qualificationsStatus = shop.qualificationsStatus ?? {}
      } catch (e) {
        logger.warn('fetchMyShop 失败', e)
      }
    },

    /**
     * 更新店铺信息
     * PUT /api/shops/me
     */
    async updateShop(dto: Partial<Pick<ShopDto, 'shopName'>>): Promise<void> {
      await client.put<ShopDto>('/shops/me', dto)
      if (dto.shopName) this.shopName = dto.shopName
    },
  },
  persist: {
    storage: localStorage,
    pick: ['shopId', 'shopName', 'shopStatus'],
  },
})
```

- [ ] **Step 4: 创建 shop/index.ts**

```ts
export { useShopStore } from './shop.store'
export type { ShopDto, ShopStatus } from './shop.store'
```

- [ ] **Step 5: 运行测试验证通过**

Run: `cd /workspace/web/seller && pnpm test src/shared/shop/ --reporter=dot`
Expected: 所有 5 个测试通过

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add web/seller/src/shared/shop/
git commit -m "feat(seller): 添加 Shop Store（店铺状态门禁 canPublish/canFulfill）"
```

---

## Task 7: 共享组件（从 system-admin 复制 + StatusTag 扩展）

**Files:**
- Create: `web/seller/src/shared/components/` 下 16 个组件 + spec
- Create: `web/seller/src/shared/components/index.ts`

- [ ] **Step 1: 复制无改动组件**

从 `web/system-admin/src/shared/components/` 复制以下文件到 `web/seller/src/shared/components/`（含 spec）：
- `IdempotencyButton.vue`、`IdempotencyButton.spec.ts`
- `ConfirmDialog.vue`、`ConfirmDialog.spec.ts`
- `DataTable.vue`、`DataTable.spec.ts`
- `EmptyState.vue`、`EmptyState.spec.ts`
- `ErrorBoundary.vue`、`ErrorBoundary.spec.ts`
- `DateTimeRangePicker.vue`、`DateTimeRangePicker.spec.ts`
- `JsonViewer.vue`、`JsonViewer.spec.ts`
- `PasswordStrengthIndicator.vue`、`PasswordStrengthIndicator.spec.ts`
- `StatisticCard.vue`、`StatisticCard.spec.ts`
- `DashboardCard.vue`（从 `modules/01-dashboard/components/` 复制）
- `charts/ChartLine.vue`、`charts/ChartLine.spec.ts`
- `charts/ChartBar.vue`
- `charts/ChartPie.vue`
- `StatusTag.vue`、`StatusTag.spec.ts`

- [ ] **Step 2: 改造 StatusTag.vue — 扩展卖家状态映射**

读取复制后的 `StatusTag.vue`，在 `statusMap` 对象中新增 `product`、`order`、`aftersales`、`shop`、`freightTemplate` 五个 type 的状态映射（详见 spec §5.2）。保留原有 system-admin 的状态映射（`user`、`role`、`order` 等如有）。

关键改动：在 `statusMap` 中增加以下映射：

```ts
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
```

同时更新 `StatusTag.spec.ts`，新增卖家状态映射的测试用例。

- [ ] **Step 3: 创建 components/index.ts**

```ts
export { default as StatusTag } from './StatusTag.vue'
export { default as IdempotencyButton } from './IdempotencyButton.vue'
export { default as ConfirmDialog } from './ConfirmDialog.vue'
export { default as DataTable } from './DataTable.vue'
export { default as EmptyState } from './EmptyState.vue'
export { default as ErrorBoundary } from './ErrorBoundary.vue'
export { default as DateTimeRangePicker } from './DateTimeRangePicker.vue'
export { default as JsonViewer } from './JsonViewer.vue'
export { default as PasswordStrengthIndicator } from './PasswordStrengthIndicator.vue'
export { default as StatisticCard } from './StatisticCard.vue'
export { default as DashboardCard } from './DashboardCard.vue'
export { default as ChartLine } from './charts/ChartLine.vue'
export { default as ChartBar } from './charts/ChartBar.vue'
export { default as ChartPie } from './charts/ChartPie.vue'
```

- [ ] **Step 4: 运行测试验证**

Run: `cd /workspace/web/seller && pnpm test src/shared/components/ --reporter=dot`
Expected: 所有组件测试通过

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add web/seller/src/shared/components/
git commit -m "feat(seller): 添加共享组件（从 system-admin 复制 + StatusTag 扩展卖家状态映射）"
```

---

## Task 8: 新增组件 TodoBadge + ShopStatusGuard

**Files:**
- Create: `web/seller/src/shared/components/TodoBadge.vue`
- Create: `web/seller/src/shared/components/ShopStatusGuard.vue`
- Test: `web/seller/src/shared/components/TodoBadge.spec.ts`
- Test: `web/seller/src/shared/components/ShopStatusGuard.spec.ts`

- [ ] **Step 1: 编写 TodoBadge.spec.ts**

```ts
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import TodoBadge from './TodoBadge.vue'

describe('TodoBadge', () => {
  it('count > 0 时显示徽标', () => {
    const wrapper = mount(TodoBadge, {
      props: { count: 5, label: '待发货' },
    })
    expect(wrapper.text()).toContain('待发货')
    expect(wrapper.text()).toContain('5')
    expect(wrapper.find('.ant-badge-count').exists()).toBe(true)
  })

  it('count === 0 时不显示数字徽标', () => {
    const wrapper = mount(TodoBadge, {
      props: { count: 0, label: '待发货' },
    })
    expect(wrapper.text()).toContain('待发货')
    expect(wrapper.find('.ant-badge-count').exists()).toBe(false)
  })

  it('点击触发 click 事件', async () => {
    const wrapper = mount(TodoBadge, {
      props: { count: 3, label: '售后' },
    })
    await wrapper.trigger('click')
    expect(wrapper.emitted('click')).toBeTruthy()
  })
})
```

- [ ] **Step 2: 实现 TodoBadge.vue**

```vue
<script setup lang="ts">
import { Badge } from 'ant-design-vue'

const props = defineProps<{
  count: number
  label: string
}>()

const emit = defineEmits<{
  click: []
}>()
</script>

<template>
  <Badge :count="count" :overflow-count="99" :offset="[6, 0]">
    <span class="todo-badge-label" @click="emit('click')">
      {{ label }}
    </span>
  </Badge>
</template>

<style scoped>
.todo-badge-label {
  cursor: pointer;
  padding: 4px 8px;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.65);
  transition: color 0.2s;
}
.todo-badge-label:hover {
  color: #1677ff;
}
</style>
```

- [ ] **Step 3: 运行 TodoBadge 测试验证**

Run: `cd /workspace/web/seller && pnpm test src/shared/components/TodoBadge.spec.ts --reporter=dot`
Expected: 3 个测试通过

- [ ] **Step 4: 编写 ShopStatusGuard.spec.ts**

```ts
import { mount } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import ShopStatusGuard from './ShopStatusGuard.vue'
import { useShopStore } from '@/shared/shop'

describe('ShopStatusGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('requires=canPublish + 店铺 Active 时显示 slot 内容', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Active'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canPublish' },
      slots: { default: '<button>上架</button>' },
    })
    expect(wrapper.html()).toContain('上架')
    expect(wrapper.find('.shop-status-guard-fallback').exists()).toBe(false)
  })

  it('requires=canPublish + 店铺非 Active 时显示 fallbackText', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Suspended'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canPublish', fallbackText: '店铺暂停中，无法上架' },
      slots: { default: '<button>上架</button>' },
    })
    expect(wrapper.html()).toContain('店铺暂停中，无法上架')
    expect(wrapper.html()).not.toContain('上架')
  })

  it('requires=canFulfill + 店铺 Rejected 时显示 fallbackText', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Rejected'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canFulfill', fallbackText: '店铺已驳回' },
      slots: { default: '<button>发货</button>' },
    })
    expect(wrapper.html()).toContain('店铺已驳回')
  })

  it('requires=canFulfill + 店铺 Suspended 时显示 slot 内容（允许履约）', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Suspended'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canFulfill' },
      slots: { default: '<button>发货</button>' },
    })
    expect(wrapper.html()).toContain('发货')
  })
})
```

- [ ] **Step 5: 实现 ShopStatusGuard.vue**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Alert } from 'ant-design-vue'
import { useShopStore } from '@/shared/shop'

const props = withDefaults(defineProps<{
  requires: 'canPublish' | 'canFulfill'
  fallbackText?: string
}>(), {
  fallbackText: '当前店铺状态不允许此操作',
})

const shop = useShopStore()

const allowed = computed(() => {
  return props.requires === 'canPublish' ? shop.canPublish : shop.canFulfill
})
</script>

<template>
  <Alert
    v-if="!allowed"
    class="shop-status-guard-fallback"
    type="warning"
    show-icon
    :message="fallbackText"
  />
  <slot v-else />
</template>
```

- [ ] **Step 6: 运行测试验证**

Run: `cd /workspace/web/seller && pnpm test src/shared/components/ShopStatusGuard.spec.ts --reporter=dot`
Expected: 4 个测试通过

- [ ] **Step 7: 更新 components/index.ts 导出新增组件**

在 `web/seller/src/shared/components/index.ts` 末尾添加：

```ts
export { default as TodoBadge } from './TodoBadge.vue'
export { default as ShopStatusGuard } from './ShopStatusGuard.vue'
```

- [ ] **Step 8: Commit**

```bash
cd /workspace
git add web/seller/src/shared/components/TodoBadge.vue web/seller/src/shared/components/ShopStatusGuard.vue web/seller/src/shared/components/TodoBadge.spec.ts web/seller/src/shared/components/ShopStatusGuard.spec.ts web/seller/src/shared/components/index.ts
git commit -m "feat(seller): 新增 TodoBadge（Header 待办徽标）与 ShopStatusGuard（店铺状态门禁）"
```

---

## Task 9: 布局组件

**Files:**
- Create: `web/seller/src/shared/layout/BasicLayout.vue`
- Create: `web/seller/src/shared/layout/SiderMenu.vue`
- Create: `web/seller/src/shared/layout/HeaderBar.vue`
- Create: `web/seller/src/shared/layout/FooterBar.vue`

- [ ] **Step 1: 复制 FooterBar.vue**

从 `web/system-admin/src/shared/layout/FooterBar.vue` 复制。

- [ ] **Step 2: 创建 BasicLayout.vue**

```vue
<script setup lang="ts">
import { Layout } from 'ant-design-vue'
import { ref } from 'vue'
import SiderMenu from './SiderMenu.vue'
import HeaderBar from './HeaderBar.vue'
import FooterBar from './FooterBar.vue'

const { Sider, Header, Content } = Layout

const collapsed = ref(false)
const isDesktop = ref(window.innerWidth >= 992)

window.addEventListener('resize', () => {
  isDesktop.value = window.innerWidth >= 992
  if (window.innerWidth < 1200) collapsed.value = true
})
</script>

<template>
  <div v-if="!isDesktop" class="desktop-only-notice">
    <p>请使用桌面端访问卖家管理后台</p>
    <p class="hint">建议屏幕宽度 ≥ 992px</p>
  </div>
  <Layout v-else class="basic-layout">
    <Sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :width="200"
      :collapsed-width="80"
      class="basic-sider"
    >
      <SiderMenu :collapsed="collapsed" />
    </Sider>
    <Layout>
      <Header class="basic-header">
        <HeaderBar :collapsed="collapsed" @toggle="collapsed = !collapsed" />
      </Header>
      <Content class="basic-content">
        <RouterView />
      </Content>
      <FooterBar />
    </Layout>
  </Layout>
</template>

<style scoped>
.basic-layout { min-height: 100vh; }
.basic-sider { position: fixed; left: 0; top: 0; bottom: 0; z-index: 100; }
.basic-header {
  position: fixed; top: 0; right: 0; left: 200px; z-index: 99;
  height: 64px; padding: 0 24px; background: #fff; box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  display: flex; align-items: center; transition: left 0.2s;
}
.basic-content {
  margin-left: 200px; margin-top: 64px; padding: 24px; min-height: calc(100vh - 64px - 32px);
  transition: margin-left 0.2s;
}
.desktop-only-notice {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  min-height: 100vh; font-size: 18px; color: #595959;
}
.desktop-only-notice .hint { font-size: 14px; color: #8C8C8C; margin-top: 8px; }
</style>
```

- [ ] **Step 3: 创建 SiderMenu.vue（静态路由 + permission 过滤）**

```vue
<script setup lang="ts">
import { Menu } from 'ant-design-vue'
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, ShopOutlined, TruckOutlined,
  ProfileOutlined, CustomerServiceOutlined, CommentOutlined,
  SettingOutlined, ExportOutlined, UserOutlined,
} from '@ant-design/icons-vue'
import { useAuthStore } from '@/shared/auth'
import type { Component } from 'vue'

const props = defineProps<{ collapsed: boolean }>()

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const iconMap: Record<string, Component> = {
  DashboardOutlined, ShopOutlined, TruckOutlined,
  ProfileOutlined, CustomerServiceOutlined, CommentOutlined,
  SettingOutlined, ExportOutlined, UserOutlined,
}

// 菜单结构定义（与路由 meta.menuGroup 对齐）
interface MenuGroup {
  key: string
  label: string
  icon: string
  children: { key: string; label: string; path: string; permission?: string }[]
}

const menuGroups: MenuGroup[] = [
  {
    key: '02-dashboard', label: '工作台', icon: 'DashboardOutlined',
    children: [
      { key: 'dashboard.overview', label: '经营概览', path: '/dashboard/overview', permission: 'dashboard:view' },
      { key: 'dashboard.sales-trend', label: '销售趋势', path: '/dashboard/sales-trend', permission: 'dashboard:sales-trend' },
      { key: 'dashboard.low-stock', label: '库存预警', path: '/dashboard/low-stock', permission: 'dashboard:low-stock' },
    ],
  },
  {
    key: '03-product-management', label: '商品管理', icon: 'ShopOutlined',
    children: [
      { key: 'product.list', label: '商品列表', path: '/products', permission: 'product:list' },
    ],
  },
  {
    key: '04-logistics', label: '物流管理', icon: 'TruckOutlined',
    children: [
      { key: 'freight-template.list', label: '运费模板', path: '/logistics/freight-templates', permission: 'freight-template:list' },
      { key: 'logistics-company.list', label: '物流公司', path: '/logistics/companies', permission: 'logistics-company:list' },
    ],
  },
  {
    key: '05-order-fulfillment', label: '订单履约', icon: 'ProfileOutlined',
    children: [
      { key: 'order.pending-shipment', label: '待发货', path: '/orders/pending-shipment', permission: 'order:list' },
      { key: 'order.list', label: '订单列表', path: '/orders', permission: 'order:list' },
    ],
  },
  {
    key: '06-after-sales', label: '售后处理', icon: 'CustomerServiceOutlined',
    children: [
      { key: 'aftersales.list', label: '售后列表', path: '/after-sales', permission: 'aftersales:list' },
    ],
  },
  {
    key: '07-review', label: '评价管理', icon: 'CommentOutlined',
    children: [
      { key: 'review.list', label: '评价回复', path: '/reviews', permission: 'review:list' },
    ],
  },
  {
    key: '01-onboarding', label: '店铺设置', icon: 'SettingOutlined',
    children: [
      { key: 'shop.application', label: '入驻申请', path: '/shop/application', permission: 'shop:application:submit' },
      { key: 'shop.profile', label: '店铺信息', path: '/shop/profile', permission: 'shop:profile:view' },
      { key: 'shop.qualifications', label: '资质管理', path: '/shop/qualifications', permission: 'shop:qualification:upload' },
    ],
  },
  {
    key: '09-export', label: '报表导出', icon: 'ExportOutlined',
    children: [
      { key: 'export.sales', label: '销售报表', path: '/export/sales', permission: 'export:sales' },
    ],
  },
  {
    key: '08-account', label: '个人中心', icon: 'UserOutlined',
    children: [
      { key: 'account.profile', label: '账号信息', path: '/account/profile', permission: 'account:profile:view' },
      { key: 'account.notifications', label: '消息通知', path: '/account/notifications', permission: 'notification:list' },
    ],
  },
]

// 过滤无权限的菜单项
const visibleGroups = computed(() => {
  return menuGroups
    .map(group => ({
      ...group,
      children: group.children.filter(child =>
        !child.permission || auth.hasPermission(child.permission),
      ),
    }))
    .filter(group => group.children.length > 0)
})

const selectedKeys = computed(() => {
  const matched = route.meta.menuKey
  return matched ? [matched as string] : []
})
const openKeys = computed(() => visibleGroups.value.map(g => g.key))

function onMenuClick({ key }: { key: string }) {
  for (const group of menuGroups) {
    const item = group.children.find(c => c.key === key)
    if (item) {
      router.push(item.path)
      return
    }
  }
}
</script>

<template>
  <div class="sider-logo" v-if="!collapsed">
    <h1>Leno 卖家</h1>
  </div>
  <div class="sider-logo-mini" v-else>
    <span>L</span>
  </div>
  <Menu
    mode="inline"
    theme="dark"
    :selected-keys="selectedKeys"
    :default-open-keys="openKeys"
    @click="onMenuClick"
  >
    <Menu.ItemGroup v-for="group in visibleGroups" :key="group.key" :title="group.label">
      <template #icon>
        <component :is="iconMap[group.icon]" />
      </template>
      <Menu.Item v-for="child in group.children" :key="child.key">
        {{ child.label }}
      </Menu.Item>
    </Menu.ItemGroup>
  </Menu>
</template>

<style scoped>
.sider-logo {
  height: 64px; display: flex; align-items: center; justify-content: center;
  background: #001529; color: #fff;
}
.sider-logo h1 { margin: 0; font-size: 18px; font-weight: 600; }
.sider-logo-mini {
  height: 64px; display: flex; align-items: center; justify-content: center;
  background: #001529; color: #fff; font-size: 24px; font-weight: 700;
}
</style>
```

- [ ] **Step 4: 创建 HeaderBar.vue（含店铺名 + TodoBadge × 2）**

```vue
<script setup lang="ts">
import { Button, Avatar, Dropdown, Space, Tooltip } from 'ant-design-vue'
import {
  MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, UserOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/shared/auth'
import { useShopStore } from '@/shared/shop'
import { TodoBadge, StatusTag } from '@/shared/components'
import type { ShopStatus } from '@/shared/shop'

defineProps<{ collapsed: boolean }>()
const emit = defineEmits<{ toggle: [] }>()

const router = useRouter()
const auth = useAuthStore()
const shop = useShopStore()

// 待办徽标数据（从 dashboard store 或 props 获取，此处用 ref 占位）
const pendingShipmentCount = ref(0)
const afterSalesPendingCount = ref(0)

// TODO: P0 dashboard 模块实现后从 dashboard store 获取实际数据
// 暂时通过 window 全局事件或 Pinia store 共享

function goToPendingShipment() {
  router.push('/orders/pending-shipment')
}
function goToAfterSales() {
  router.push('/after-sales?status=Pending')
}
function goToNotifications() {
  router.push('/account/notifications')
}
function goToProfile() {
  router.push('/account/profile')
}
async function onLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <div class="header-bar">
    <Button type="text" @click="emit('toggle')">
      <MenuUnfoldOutlined v-if="collapsed" />
      <MenuFoldOutlined v-else />
    </Button>

    <!-- 店铺名 + 状态 -->
    <Space class="shop-info" v-if="shop.shopName">
      <span class="shop-name">{{ shop.shopName }}</span>
      <StatusTag type="shop" :status="shop.shopStatus as string" />
    </Space>

    <div class="header-right">
      <TodoBadge :count="pendingShipmentCount" label="待发货" @click="goToPendingShipment" />
      <TodoBadge :count="afterSalesPendingCount" label="售后" @click="goToAfterSales" />

      <Tooltip title="消息通知">
        <Badge :count="0" :offset="[-2, 4]">
          <Button type="text" shape="circle" @click="goToNotifications">
            <BellOutlined />
          </Button>
        </Badge>
      </Tooltip>

      <Dropdown>
        <Space class="user-info">
          <Avatar :size="32">
            <UserOutlined v-if="!auth.user?.avatar" />
            <img v-else :src="auth.user.avatar" alt="avatar" />
          </Avatar>
          <span>{{ auth.user?.nickname || auth.user?.username }}</span>
        </Space>
        <template #overlay>
          <Menu>
            <Menu.Item key="profile" @click="goToProfile">
              <UserOutlined /> 账号信息
            </Menu.Item>
            <Menu.Divider />
            <Menu.Item key="logout" @click="onLogout">
              <LogoutOutlined /> 退出登录
            </Menu.Item>
          </Menu>
        </template>
      </Dropdown>
    </div>
  </div>
</template>

<script lang="ts">
import { ref } from 'vue'
import { Menu as DropdownMenu } from 'ant-design-vue'
</script>

<style scoped>
.header-bar { display: flex; align-items: center; width: 100%; height: 100%; }
.shop-info { margin-left: 16px; }
.shop-name { font-weight: 500; font-size: 14px; }
.header-right { margin-left: auto; display: flex; align-items: center; gap: 16px; }
.user-info { cursor: pointer; }
</style>
```

- [ ] **Step 5: 运行 typecheck 验证**

Run: `cd /workspace/web/seller && pnpm typecheck`
Expected: 无错误（注意：路由和模块尚未完整，可能有导入错误，先跳过模块导入的检查）

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add web/seller/src/shared/layout/
git commit -m "feat(seller): 添加布局组件（BasicLayout/SiderMenu/HeaderBar/FooterBar）"
```

---

## Task 10: 框架页

**Files:**
- Create: `web/seller/src/shared/pages/Forbidden.vue`
- Create: `web/seller/src/shared/pages/NotFound.vue`
- Create: `web/seller/src/shared/pages/Maintenance.vue`
- Create: `web/seller/src/shared/pages/RateLimited.vue`
- Create: `web/seller/src/shared/pages/ServerError.vue`

- [ ] **Step 1: 复制框架页**

从 `web/system-admin/src/shared/pages/` 复制 `Forbidden.vue` 和 `NotFound.vue`。

- [ ] **Step 2: 创建 Maintenance.vue**

```vue
<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
</script>

<template>
  <Result status="info" title="系统维护中" sub-title="系统正在维护，请稍后再来访问">
    <template #extra>
      <Button type="primary" @click="() => window.location.reload()">刷新页面</Button>
    </template>
  </Result>
</template>

<script lang="ts">
const window = globalThis.window
</script>
```

- [ ] **Step 3: 创建 RateLimited.vue**

```vue
<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <Result status="warning" title="操作过于频繁" sub-title="您的操作触发了限流保护，请稍后重试">
    <template #extra>
      <Button type="primary" @click="router.back()">返回上一页</Button>
    </template>
  </Result>
</template>
```

- [ ] **Step 4: 创建 ServerError.vue**

```vue
<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <Result status="500" title="服务器错误" sub-title="服务器内部错误，请稍后重试或联系平台客服">
    <template #extra>
      <Button type="primary" @click="router.push('/dashboard/overview')">返回首页</Button>
    </template>
  </Result>
</template>
```

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add web/seller/src/shared/pages/
git commit -m "feat(seller): 添加框架页（403/404/维护/限流/500）"
```

---

## Task 11: App 入口与路由

**Files:**
- Create: `web/seller/src/app/pinia.ts`
- Create: `web/seller/src/app/provider.vue`
- Create: `web/seller/src/app/router.ts`
- Create: `web/seller/src/main.ts`
- Create: `web/seller/src/App.vue`

- [ ] **Step 1: 创建 app/pinia.ts**

```ts
import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'

const pinia = createPinia()
pinia.use(piniaPluginPersistedstate)

export default pinia
```

- [ ] **Step 2: 创建 app/provider.vue**

从 `web/system-admin/src/app/provider.vue` 复制，内容完全一致（Ant Design Vue ConfigProvider + zhCN locale + theme）。

- [ ] **Step 3: 创建 app/router.ts**

```ts
import {
  createRouter,
  createWebHistory,
  type RouteRecordRaw,
  type Router,
} from 'vue-router'
import { message } from 'ant-design-vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { useShopStore } from '@/shared/shop'
import BasicLayout from '@/shared/layout/BasicLayout.vue'
import Forbidden from '@/shared/pages/Forbidden.vue'
import NotFound from '@/shared/pages/NotFound.vue'

// P0 模块路由
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
// P1 模块路由（Login 必需）
import account from '@/modules/08-account/routes'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'account.login',
    component: () => import('@/modules/08-account/views/Login.vue'),
    meta: { anonymous: true, title: '登录' },
  },
  {
    path: '/403',
    name: 'forbidden',
    component: Forbidden,
    meta: { anonymous: true, title: '无权访问' },
  },
  {
    path: '/404',
    name: 'not-found',
    component: NotFound,
    meta: { anonymous: true, title: '页面不存在' },
  },
  {
    path: '/',
    name: 'basic',
    component: BasicLayout,
    children: [
      { path: '', redirect: '/dashboard/overview' },
      ...dashboard,
      ...product,
      ...order,
      ...afterSales,
      ...account,
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'catch-all',
    component: NotFound,
    meta: { anonymous: true, title: '页面不存在' },
  },
]

export const router: Router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  const shop = useShopStore()

  // 1. 公开路由直接放行
  if (to.meta.anonymous) return true

  // 2. 未登录跳 /login
  if (!auth.isAuthenticated) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  // 3. 首次进入拉取 profile + shop
  if (!auth.user) {
    try {
      await auth.fetchProfile()
      await shop.fetchMyShop()
    } catch {
      await auth.logout()
      return { path: '/login' }
    }
  }

  // 4. 角色校验
  if (!auth.hasRole((to.meta.roles ?? []) as string[])) {
    return { path: '/403' }
  }

  // 5. 权限校验
  if (to.meta.permission && !auth.hasPermission(to.meta.permission as string)) {
    return { path: '/403' }
  }

  // 6. 店铺状态门禁
  if (to.meta.requiresActiveShop && !shop.canPublish) {
    message.warning('店铺当前状态不允许此操作，请先完成入驻或联系平台')
    return { path: '/shop/application' }
  }

  return true
})
```

- [ ] **Step 4: 创建 main.ts**

```ts
import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import EChartsVue from 'vue-echarts'
import 'echarts'
import App from './App.vue'
import pinia from './app/pinia'
import { router } from './app/router'
import './shared/tokens/design-tokens.css'
import { logger } from './shared/utils/logger'

const app = createApp(App)

app.use(pinia)
app.use(router)
app.use(Antd)
app.component('ECharts', EChartsVue)

// 全局错误处理
app.config.errorHandler = (err) => {
  logger.error('Unhandled app error', err)
}

app.mount('#app')
```

- [ ] **Step 5: 创建 App.vue**

```vue
<script setup lang="ts">
import Provider from './app/provider.vue'
</script>

<template>
  <Provider>
    <RouterView />
  </Provider>
</template>
```

- [ ] **Step 6: 运行 typecheck**

Run: `cd /workspace/web/seller && pnpm typecheck`
Expected: 可能有模块路由缺失错误（模块尚未创建），记录但不阻塞

- [ ] **Step 7: Commit**

```bash
cd /workspace
git add web/seller/src/app/ web/seller/src/main.ts web/seller/src/App.vue
git commit -m "feat(seller): 添加 App 入口与路由（守卫含 profile+shop 拉取）"
```

---

## Task 12: Account 模块 — Login 页

**Files:**
- Create: `web/seller/src/modules/08-account/api/auth.api.ts`
- Create: `web/seller/src/modules/08-account/types/auth.dto.ts`
- Create: `web/seller/src/modules/08-account/views/Login.vue`
- Create: `web/seller/src/modules/08-account/routes.ts`
- Create: `web/seller/src/modules/08-account/index.ts`
- Test: `web/seller/src/modules/08-account/api/auth.api.spec.ts`

- [ ] **Step 1: 创建 types/auth.dto.ts**

```ts
export interface LoginDto {
  username: string
  password: string
}

export interface LoginResultDto {
  token: string
  expiresIn: number
  user: {
    id: string
    username: string
    email: string
    phone?: string
    nickname?: string
    avatar?: string
    shopId?: string
    shopName?: string
    shopStatus?: string
    status: string
    roles: string[]
  }
  roles: string[]
  permissions: string[]
}

export interface ProfileResultDto {
  profile: {
    id: string
    username: string
    email: string
    phone?: string
    nickname?: string
    avatar?: string
    shopId?: string
    shopName?: string
    shopStatus?: string
    status: string
    roles: string[]
  }
  permissions: string[]
}
```

- [ ] **Step 2: 编写 auth.api.spec.ts**

```ts
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { authApi } from './auth.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: {
    post: vi.fn(),
    get: vi.fn(),
  },
}))

describe('authApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('login 调用 POST /auth/login', async () => {
    vi.mocked(client.post).mockResolvedValue({ token: 't', expiresIn: 3600, user: {}, roles: [], permissions: [] } as any)
    await authApi.login({ username: 'u', password: 'p' })
    expect(client.post).toHaveBeenCalledWith('/auth/login', { username: 'u', password: 'p' })
  })

  it('getProfile 调用 GET /users/me', async () => {
    vi.mocked(client.get).mockResolvedValue({ profile: {}, permissions: [] } as any)
    await authApi.getProfile()
    expect(client.get).toHaveBeenCalledWith('/users/me')
  })

  it('logout 调用 POST /auth/logout', async () => {
    vi.mocked(client.post).mockResolvedValue(undefined as any)
    await authApi.logout()
    expect(client.post).toHaveBeenCalledWith('/auth/logout')
  })
})
```

- [ ] **Step 3: 实现 auth.api.ts**

```ts
import { client } from '@/shared/http'
import type { LoginDto, LoginResultDto, ProfileResultDto } from '../types/auth.dto'

export const authApi = {
  login: (body: LoginDto) =>
    client.post<LoginResultDto>('/auth/login', body),

  getProfile: () =>
    client.get<ProfileResultDto>('/users/me'),

  logout: () =>
    client.post<void>('/auth/logout'),
}
```

- [ ] **Step 4: 运行 API 测试**

Run: `cd /workspace/web/seller && pnpm test src/modules/08-account/ --reporter=dot`
Expected: 3 个测试通过

- [ ] **Step 5: 创建 Login.vue**

```vue
<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Form, FormItem, Input, InputPassword, Button, Alert, Card } from 'ant-design-vue'
import { UserOutlined, LockOutlined } from '@ant-design/icons-vue'
import { useAuthStore } from '@/shared/auth'
import { useShopStore } from '@/shared/shop'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const shop = useShopStore()

const loading = ref(false)
const errorMsg = ref('')

const form = reactive({
  username: '',
  password: '',
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function onSubmit() {
  errorMsg.value = ''
  loading.value = true
  try {
    await auth.login(form)
    await shop.fetchMyShop()
    const redirect = (route.query.redirect as string) || '/dashboard/overview'
    // 店铺未完成入驻时引导到入驻页
    if (shop.shopStatus === 'PendingReview' || shop.shopStatus === 'Rejected') {
      router.push('/shop/application')
    } else {
      router.push(redirect)
    }
  } catch (e: any) {
    if (e?.status === 401) errorMsg.value = '账号或密码错误'
    else if (e?.status === 403) errorMsg.value = '账号已禁用'
    else if (e?.status === 429) errorMsg.value = '操作过于频繁，请稍后重试'
    else errorMsg.value = e?.message || '登录失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-container">
    <Card class="login-card">
      <div class="login-header">
        <h1>Leno 卖家管理后台</h1>
        <p>请登录您的卖家账号</p>
      </div>

      <Alert v-if="errorMsg" type="error" :message="errorMsg" show-icon class="login-alert" />

      <Form :model="form" :rules="rules" layout="vertical" @finish="onSubmit">
        <FormItem name="username">
          <Input
            v-model:value="form.username"
            size="large"
            placeholder="用户名"
            @pressEnter="onSubmit"
          >
            <template #prefix><UserOutlined /></template>
          </Input>
        </FormItem>
        <FormItem name="password">
          <InputPassword
            v-model:value="form.password"
            size="large"
            placeholder="密码"
            @pressEnter="onSubmit"
          >
            <template #prefix><LockOutlined /></template>
          </InputPassword>
        </FormItem>
        <FormItem>
          <Button type="primary" html-type="submit" size="large" block :loading="loading">
            登录
          </Button>
        </FormItem>
      </Form>

      <!-- 2FA UI 预留（本次不接通） -->
      <div class="two-factor-placeholder">
        <Input disabled size="large" placeholder="两步验证（暂未启用）">
          <template #prefix><LockOutlined /></template>
        </Input>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.login-container {
  min-height: 100vh; display: flex; align-items: center; justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
.login-card { width: 400px; padding: 24px; }
.login-header { text-align: center; margin-bottom: 24px; }
.login-header h1 { font-size: 24px; color: #1677ff; margin-bottom: 8px; }
.login-header p { color: #8c8c8c; font-size: 14px; }
.login-alert { margin-bottom: 16px; }
.two-factor-placeholder { margin-top: 16px; opacity: 0.5; }
</style>
```

- [ ] **Step 6: 创建 routes.ts**

```ts
import type { RouteRecordRaw } from 'vue-router'

export const accountRoutes: RouteRecordRaw[] = [
  {
    path: '/account/profile',
    name: 'account.profile',
    component: () => import('./views/Profile.vue'),
    meta: {
      title: '账号信息',
      menuKey: 'account.profile',
      roles: ['Seller'],
      permission: 'account:profile:view',
      menuGroup: '08-account',
    },
  },
  {
    path: '/account/notifications',
    name: 'account.notifications',
    component: () => import('./views/Notifications.vue'),
    meta: {
      title: '消息通知',
      menuKey: 'account.notifications',
      roles: ['Seller'],
      permission: 'notification:list',
      menuGroup: '08-account',
    },
  },
]
```

注意：Login.vue 通过静态路由在 `app/router.ts` 中直接注册（`/login`），不在 `accountRoutes` 中。

- [ ] **Step 7: 创建 index.ts**

```ts
export { accountRoutes } from './routes'
export { authApi } from './api/auth.api'
```

- [ ] **Step 8: 创建 Profile.vue 与 Notifications.vue 占位（P1 实现，先占位让路由不报错）**

```vue
<!-- web/seller/src/modules/08-account/views/Profile.vue -->
<script setup lang="ts">
import { Result } from 'ant-design-vue'
</script>
<template>
  <Result status="info" title="账号信息" sub-title="P1 阶段实现" />
</template>
```

```vue
<!-- web/seller/src/modules/08-account/views/Notifications.vue -->
<script setup lang="ts">
import { Result } from 'ant-design-vue'
</script>
<template>
  <Result status="info" title="消息通知" sub-title="P1 阶段实现" />
</template>
```

- [ ] **Step 9: 运行 typecheck 验证**

Run: `cd /workspace/web/seller && pnpm typecheck`
Expected: 0 error

- [ ] **Step 10: 启动 dev server 验证**

Run: `cd /workspace/web/seller && pnpm dev`
Expected: 服务在 5174 端口启动，访问 `http://localhost:5174/login` 显示登录页

- [ ] **Step 11: Commit**

```bash
cd /workspace
git add web/seller/src/modules/08-account/ web/seller/src/app/
git commit -m "feat(seller): 添加 Account 模块 Login 页（账号密码登录 + 2FA UI 预留）"
```

---

## Task 13: Dashboard 模块（3 页）

**Files:**
- Create: `web/seller/src/modules/02-dashboard/api/dashboard.api.ts`
- Create: `web/seller/src/modules/02-dashboard/types/dashboard.dto.ts`
- Create: `web/seller/src/modules/02-dashboard/views/Overview.vue`
- Create: `web/seller/src/modules/02-dashboard/views/SalesTrend.vue`
- Create: `web/seller/src/modules/02-dashboard/views/LowStockAlert.vue`
- Create: `web/seller/src/modules/02-dashboard/routes.ts`
- Create: `web/seller/src/modules/02-dashboard/index.ts`
- Test: `web/seller/src/modules/02-dashboard/api/dashboard.api.spec.ts`

- [ ] **Step 1: 创建 types/dashboard.dto.ts**

```ts
/**
 * 工作台概览 DTO
 */
export interface SellerDashboardDto {
  shopId: string
  shopName: string
  status: string
  productCount: number
  totalOrders: number
  pendingOrders: number
  completedOrders: number
  totalRevenue: number
  todayOrderCount: number
  todaySalesAmount: number
  todaySalesCurrency: string
  todayAvgRating: number
  todayRatingCount: number
  todayRefundCount: number
}

/**
 * 销售趋势条目 DTO
 */
export interface SalesTrendItemDto {
  date: string
  orderCount: number
  salesAmount: number
}

/**
 * 库存预警条目 DTO（后端缺失，mock 兜底）
 */
export interface LowStockItemDto {
  productId: string
  productName: string
  skuId: string
  skuName: string
  stock: number
  threshold: number
}

/**
 * 日期范围参数
 */
export interface DateRangeParams {
  from: string
  to: string
}
```

- [ ] **Step 2: 编写 dashboard.api.spec.ts**

```ts
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { dashboardApi } from './dashboard.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn() },
}))

describe('dashboardApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('getDashboard 调用 GET /seller/dashboard', async () => {
    vi.mocked(client.get).mockResolvedValue({ shopId: 's1' } as any)
    await dashboardApi.getDashboard()
    expect(client.get).toHaveBeenCalledWith('/seller/dashboard')
  })

  it('getSalesTrend 调用 GET /seller/sales-trend 带日期参数', async () => {
    vi.mocked(client.get).mockResolvedValue([] as any)
    await dashboardApi.getSalesTrend({ from: '2026-07-01', to: '2026-07-07' })
    expect(client.get).toHaveBeenCalledWith('/seller/sales-trend', {
      params: { from: '2026-07-01', to: '2026-07-07' },
    })
  })
})
```

- [ ] **Step 3: 实现 dashboard.api.ts**

```ts
import { client } from '@/shared/http'
import type { SellerDashboardDto, SalesTrendItemDto, DateRangeParams } from '../types/dashboard.dto'

export const dashboardApi = {
  getDashboard: () =>
    client.get<SellerDashboardDto>('/seller/dashboard'),

  getSalesTrend: (params: DateRangeParams) =>
    client.get<SalesTrendItemDto[]>('/seller/sales-trend', { params }),
}
```

- [ ] **Step 4: 运行 API 测试**

Run: `cd /workspace/web/seller && pnpm test src/modules/02-dashboard/ --reporter=dot`
Expected: 2 个测试通过

- [ ] **Step 5: 创建 Overview.vue**

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, Row, Col, List, Button, Segmented, Spin, Skeleton, Empty } from 'ant-design-vue'
import { Breadcrumb } from 'ant-design-vue'
import dayjs from 'dayjs'
import { dashboardApi } from '../api/dashboard.api'
import type { SellerDashboardDto, SalesTrendItemDto } from '../types/dashboard.dto'
import { DashboardCard, ChartLine, EmptyState } from '@/shared/components'

const loading = ref(true)
const dashboard = ref<SellerDashboardDto | null>(null)
const trendLoading = ref(true)
const trendData = ref<SalesTrendItemDto[]>([])
const trendRange = ref<'7d' | '30d'>('7d')

async function fetchDashboard() {
  loading.value = true
  try {
    dashboard.value = await dashboardApi.getDashboard()
  } finally {
    loading.value = false
  }
}

async function fetchTrend() {
  trendLoading.value = true
  const days = trendRange.value === '7d' ? 6 : 29
  const from = dayjs().subtract(days, 'day').format('YYYY-MM-DD')
  const to = dayjs().format('YYYY-MM-DD')
  try {
    trendData.value = await dashboardApi.getSalesTrend({ from, to })
  } finally {
    trendLoading.value = false
  }
}

function onTrendRangeChange(val: '7d' | '30d') {
  trendRange.value = val
  fetchTrend()
}

const chartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['销售额', '订单数'] },
  xAxis: { type: 'category', data: trendData.value.map(d => d.date) },
  yAxis: [
    { type: 'value', name: '销售额', position: 'left' },
    { type: 'value', name: '订单数', position: 'right' },
  ],
  series: [
    {
      name: '销售额',
      type: 'line',
      smooth: true,
      yAxisIndex: 0,
      itemStyle: { color: '#1677FF' },
      data: trendData.value.map(d => d.salesAmount),
    },
    {
      name: '订单数',
      type: 'line',
      smooth: true,
      yAxisIndex: 1,
      itemStyle: { color: '#52C41A' },
      data: trendData.value.map(d => d.orderCount),
    },
  ],
}))

onMounted(() => {
  fetchDashboard()
  fetchTrend()
})
</script>

<script lang="ts">
import { computed } from 'vue'
</script>

<template>
  <div class="dashboard-overview">
    <Breadcrumb class="page-breadcrumb">
      <Breadcrumb.Item>首页</Breadcrumb.Item>
      <Breadcrumb.Item>工作台</Breadcrumb.Item>
      <Breadcrumb.Item>经营概览</Breadcrumb.Item>
    </Breadcrumb>

    <!-- 统计卡片 -->
    <Skeleton v-if="loading" active :paragraph="{ rows: 2 }" />
    <Row v-else-if="dashboard" :gutter="16" class="stat-cards">
      <Col :span="6">
        <DashboardCard title="今日订单数" :value="dashboard.todayOrderCount" icon="ShoppingOutlined" />
      </Col>
      <Col :span="6">
        <DashboardCard title="今日销售额" :value="dashboard.todaySalesAmount" prefix="¥" icon="DollarOutlined" />
      </Col>
      <Col :span="6">
        <DashboardCard title="待发货" :value="dashboard.pendingOrders" icon="TruckOutlined" />
      </Col>
      <Col :span="6">
        <DashboardCard title="售后待处理" :value="dashboard.todayRefundCount" icon="CustomerServiceOutlined" />
      </Col>
    </Row>

    <!-- 销售趋势图 -->
    <Card class="trend-card" title="销售趋势">
      <template #extra>
        <Segmented
          :value="trendRange"
          :options="[
            { label: '近7天', value: '7d' },
            { label: '近30天', value: '30d' },
          ]"
          @change="onTrendRangeChange"
        />
      </template>
      <Spin :spinning="trendLoading">
        <EmptyState
          v-if="trendData.length === 0 && !trendLoading"
          title="暂无销售数据"
          description="有订单后将自动生成趋势"
        />
        <v-chart v-else class="trend-chart" :option="chartOption" autoresize />
      </Spin>
    </Card>
  </div>
</template>

<style scoped>
.dashboard-overview { }
.page-breadcrumb { margin-bottom: 16px; }
.stat-cards { margin-bottom: 24px; }
.trend-card { margin-bottom: 24px; }
.trend-chart { height: 350px; }
</style>
```

- [ ] **Step 6: 创建 SalesTrend.vue**

```vue
<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Card, Segmented, Spin, Row, Col, DatePicker } from 'ant-design-vue'
import { Breadcrumb } from 'ant-design-vue'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { dashboardApi } from '../api/dashboard.api'
import type { SalesTrendItemDto } from '../types/dashboard.dto'
import { ChartLine, EmptyState } from '@/shared/components'

const RangePicker = DatePicker.RangePicker

const loading = ref(true)
const trendData = ref<SalesTrendItemDto[]>([])
const dateRange = ref<[Dayjs, Dayjs]>([
  dayjs().subtract(6, 'day'),
  dayjs(),
])

async function fetchTrend() {
  loading.value = true
  const from = dateRange.value[0].format('YYYY-MM-DD')
  const to = dateRange.value[1].format('YYYY-MM-DD')
  try {
    trendData.value = await dashboardApi.getSalesTrend({ from, to })
  } finally {
    loading.value = false
  }
}

function onRangeChange(dates: [Dayjs, Dayjs] | null) {
  if (dates) {
    dateRange.value = dates
    fetchTrend()
  }
}

const chartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['销售额', '订单数'] },
  xAxis: { type: 'category', data: trendData.value.map(d => d.date) },
  yAxis: [
    { type: 'value', name: '销售额', position: 'left' },
    { type: 'value', name: '订单数', position: 'right' },
  ],
  series: [
    {
      name: '销售额', type: 'line', smooth: true, yAxisIndex: 0,
      itemStyle: { color: '#1677FF' },
      data: trendData.value.map(d => d.salesAmount),
    },
    {
      name: '订单数', type: 'line', smooth: true, yAxisIndex: 1,
      itemStyle: { color: '#52C41A' },
      data: trendData.value.map(d => d.orderCount),
    },
  ],
}))

onMounted(fetchTrend)
</script>

<template>
  <div>
    <Breadcrumb class="page-breadcrumb">
      <Breadcrumb.Item>首页</Breadcrumb.Item>
      <Breadcrumb.Item>工作台</Breadcrumb.Item>
      <Breadcrumb.Item>销售趋势</Breadcrumb.Item>
    </Breadcrumb>

    <Card title="销售趋势分析">
      <template #extra>
        <RangePicker
          :value="dateRange"
          @change="onRangeChange"
          :allow-clear="false"
        />
      </template>
      <Spin :spinning="loading">
        <EmptyState
          v-if="trendData.length === 0 && !loading"
          title="暂无销售数据"
          description="请调整时间范围或等待订单产生"
        />
        <v-chart v-else class="trend-chart" :option="chartOption" autoresize />
      </Spin>
    </Card>
  </div>
</template>

<style scoped>
.page-breadcrumb { margin-bottom: 16px; }
.trend-chart { height: 450px; }
</style>
```

- [ ] **Step 7: 创建 LowStockAlert.vue（mock 兜底 + 后端未就绪徽标）**

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, Table, Tag, Button } from 'ant-design-vue'
import { Breadcrumb } from 'ant-design-vue'
import type { LowStockItemDto } from '../types/dashboard.dto'

const loading = ref(true)
const dataSource = ref<LowStockItemDto[]>([])

const columns = [
  { title: '商品名称', dataIndex: 'productName', key: 'productName' },
  { title: 'SKU', dataIndex: 'skuName', key: 'skuName' },
  { title: '当前库存', dataIndex: 'stock', key: 'stock', width: 120 },
  { title: '预警阈值', dataIndex: 'threshold', key: 'threshold', width: 120 },
  {
    title: '状态', key: 'status', width: 100,
    customRender: ({ record }: { record: LowStockItemDto }) => {
      const ratio = record.stock / record.threshold
      const color = ratio < 0.5 ? 'red' : ratio < 1 ? 'orange' : 'green'
      return h(Tag, { color }, () => ratio < 0.5 ? '严重不足' : ratio < 1 ? '不足' : '正常')
    },
  },
]

onMounted(() => {
  // TODO(BE-2): 后端 /api/seller/dashboard/low-stock 尚未实现，使用 mock 数据
  setTimeout(() => {
    dataSource.value = [
      { productId: 'p1', productName: '测试商品A', skuId: 's1', skuName: '红色-L', stock: 3, threshold: 10 },
      { productId: 'p2', productName: '测试商品B', skuId: 's2', skuName: '蓝色-M', stock: 5, threshold: 20 },
    ]
    loading.value = false
  }, 500)
})
</script>

<script lang="ts">
import { h } from 'vue'
</script>

<template>
  <div>
    <Breadcrumb class="page-breadcrumb">
      <Breadcrumb.Item>首页</Breadcrumb.Item>
      <Breadcrumb.Item>工作台</Breadcrumb.Item>
      <Breadcrumb.Item>库存预警</Breadcrumb.Item>
    </Breadcrumb>

    <Tag color="warning" class="backend-notice">后端未就绪：当前为 mock 数据（BE-2 待实现）</Tag>

    <Card title="库存预警列表">
      <Table
        :columns="columns"
        :data-source="dataSource"
        :loading="loading"
        row-key="skuId"
        :pagination="{ pageSize: 20 }"
      >
        <template #emptyText>
          <EmptyState title="暂无库存预警" description="所有商品库存充足" />
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.page-breadcrumb { margin-bottom: 16px; }
.backend-notice { margin-bottom: 16px; }
</style>
```

- [ ] **Step 8: 创建 routes.ts**

```ts
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/dashboard/overview',
    name: 'dashboard.overview',
    component: () => import('./views/Overview.vue'),
    meta: {
      title: '经营概览',
      menuKey: 'dashboard.overview',
      roles: ['Seller'],
      permission: 'dashboard:view',
      menuGroup: '02-dashboard',
    },
  },
  {
    path: '/dashboard/sales-trend',
    name: 'dashboard.sales-trend',
    component: () => import('./views/SalesTrend.vue'),
    meta: {
      title: '销售趋势',
      menuKey: 'dashboard.sales-trend',
      roles: ['Seller'],
      permission: 'dashboard:sales-trend',
      menuGroup: '02-dashboard',
    },
  },
  {
    path: '/dashboard/low-stock',
    name: 'dashboard.low-stock',
    component: () => import('./views/LowStockAlert.vue'),
    meta: {
      title: '库存预警',
      menuKey: 'dashboard.low-stock',
      roles: ['Seller'],
      permission: 'dashboard:low-stock',
      menuGroup: '02-dashboard',
    },
  },
]

export default routes
```

- [ ] **Step 9: 创建 index.ts**

```ts
export { default } from './routes'
export { dashboardApi } from './api/dashboard.api'
```

- [ ] **Step 10: 运行 typecheck + test**

Run: `cd /workspace/web/seller && pnpm typecheck && pnpm test src/modules/02-dashboard/ --reporter=dot`
Expected: 0 error + 2 个测试通过

- [ ] **Step 11: Commit**

```bash
cd /workspace
git add web/seller/src/modules/02-dashboard/
git commit -m "feat(seller): 添加 Dashboard 模块（经营概览/销售趋势/库存预警mock）"
```

---

## Task 14-16: Product / Order / After-sales 模块

这三个模块遵循与 Dashboard 完全相同的模式：
1. 创建 `types/{module}.dto.ts`（DTO 接口）
2. 编写 `api/{module}.api.spec.ts`（TDD 先写测试）
3. 实现 `api/{module}.api.ts`
4. 创建 `views/*.vue`（列表页 + 详情/编辑页）
5. 创建 `routes.ts` + `index.ts`
6. 运行 typecheck + test
7. Commit

每个模块的具体 API 端点与 DTO 结构见 spec §2.3（Product）、§2.4（Order）、§2.5（After-sales）。

**Product 模块关键点**（spec §2.3）：
- 端点前缀 `/api/products`（非 `/api/seller/products`）
- 4 页：ProductList、ProductEdit（new/edit）、SkuManagement、PriceHistory
- 操作：submitForReview、takeDown（需理由）、republish
- 价格调整：`POST /products/{id}/skus/{skuId}/price`

**Order 模块关键点**（spec §2.4 + BE-1）：
- 端点前缀 `/api/seller/orders`，物流轨迹 `/api/orders/{id}/logistics-trace`
- `page` 默认值 `0`（BE-1 待统一），加 TODO 标注
- 3 页：PendingShipment、OrderList、LogisticsTrace
- 发货操作：`POST /seller/orders/{id}/ship` + Idempotency-Key

**After-sales 模块关键点**（spec §2.5）：
- 端点前缀 `/api/seller/after-sales`，分页 `page=1` 起
- 2 页：AfterSalesList、AfterSalesDetail
- 操作：approve、reject（需理由）、confirm-return

每个模块按上述 Task 13 的 11 步流程逐一实现。

---

## Task 17: 全量验证与 CI 集成

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: 运行全量 lint + typecheck + test**

Run: `cd /workspace/web/seller && pnpm lint && pnpm typecheck && pnpm test --coverage --reporter=dot`
Expected: lint 0 error，typecheck 0 error，测试通过且覆盖率达标（lines ≥ 70%，functions ≥ 70%，branches ≥ 60%）

- [ ] **Step 2: 运行 build 验证**

Run: `cd /workspace/web/seller && pnpm build`
Expected: `dist/` 生成，无错误

- [ ] **Step 3: 启动 dev server 端到端验证**

Run: `cd /workspace/web/seller && pnpm dev`
Expected:
- 访问 `http://localhost:5174/login` 显示登录页
- 登录后跳转 `/dashboard/overview`
- SiderMenu 显示菜单项
- 商品/订单/售后列表页可访问

- [ ] **Step 4: 添加 CI job 到 .github/workflows/ci.yml**

在现有 ci.yml 中新增 `web-seller` job：

```yaml
  web-seller:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: web/seller
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
        with:
          version: 9
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: pnpm
          cache-dependency-path: web/seller/pnpm-lock.yaml
      - run: pnpm install --frozen-lockfile
      - run: pnpm lint
      - run: pnpm typecheck
      - run: pnpm test -- --coverage --reporter=dot
      - run: pnpm build
      - uses: actions/upload-artifact@v4
        with:
          name: web-seller-dist
          path: web/seller/dist
```

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add .github/workflows/ci.yml
git commit -m "ci(seller): 添加 web-seller CI job（lint/typecheck/test/build）"
```

- [ ] **Step 6: 最终提交并推送**

```bash
cd /workspace
git push origin dev
```

---

## 自审清单

### Spec 覆盖核对

| Spec 章节 | 覆盖 Task | 状态 |
|---|---|---|
| §1 总体架构 | Task 1-2 | ✅ |
| §1.6 shared 基线复制 | Task 3-8 | ✅ |
| §2.2 Dashboard 3 页 | Task 13 | ✅ |
| §2.3 Product 4 页 | Task 14 | ✅ |
| §2.4 Order 3 页（BE-1） | Task 15 | ✅ |
| §2.5 After-sales 2 页 | Task 16 | ✅ |
| §2.11 框架页 | Task 10 | ✅ |
| §3 鉴权与路由守卫 | Task 5, 11 | ✅ |
| §3.2 Shop Store | Task 6 | ✅ |
| §3.10 Header 待办徽标 | Task 8, 9 | ✅ |
| §4 数据流与 HTTP | Task 3 | ✅ |
| §4.7 Order page=0 标注 | Task 15 | ✅ |
| §5 共享组件 | Task 7-8 | ✅ |
| §5.4 布局 | Task 9 | ✅ |
| §6 测试与构建 | Task 17 | ✅ |
| §6.8 CI | Task 17 | ✅ |
| §7.1 BE-1/2/3 标注 | Task 13 (BE-2), 15 (BE-1) | ✅ |
| §8 验收标准 | Task 17 | ✅ |

### P1/P2 模块占位

Login（Task 12）包含 Profile.vue 与 Notifications.vue 占位页，P1 计划实现完整功能。Onboarding/Logistics/Review 模块在 P1 计划中实现。

---

## 执行交接

Plan complete and saved to `docs/superpowers/plans/2026-07-29-seller-admin-foundation-p0.md`. Two execution options:

**1. Subagent-Driven (recommended)** - 每个 Task 派发独立 subagent，任务间审查，快速迭代

**2. Inline Execution** - 在当前会话中按批次执行，检查点审查

Which approach?
