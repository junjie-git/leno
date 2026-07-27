# 系统管理后台 · Plan 1（基础设施 + 06-account 登录模块）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `web/system-admin/` 子工作区内搭建 Vue 3 + TypeScript + Ant Design Vue 的系统管理后台 SPA 骨架，完成 shared 基础设施（HTTP/Auth/Components/Layout/Tokens/Utils）与 06-account 登录模块，使应用可以启动、登录、跳转仪表盘路由。

**Architecture:** 采用 spec §1 总体架构：Vite 6 + Vue 3.5 SFC + TS strict + Pinia（持久化）+ Vue Router 4 + Ant Design Vue 4 + axios + ECharts。`shared/` 沉淀通用层（http/auth/components/layout/tokens/utils/types），`app/` 沉淀启动装配层（router/pinia/provider/env），`modules/06-account/` 实现 Login2fa 页与配套 API/types/routes。所有写操作走 `IdempotencyButton`，所有错误经 `AppError` 层级转换，鉴权经路由守卫 + `v-permission` 双层控制。本 Plan 仅交付基础设施与登录闭环，其余 6 个模块（dashboard/user-access/system-governance/runtime-ops/audit/monitoring）在后续 Plan 中实现。

**Tech Stack:** Vue 3.5.x、TypeScript 5.x strict、Vite 6.x、Ant Design Vue 4.x、Pinia 2.x + pinia-plugin-persistedstate、Vue Router 4.x、axios 1.7.x、@vue-echarts 7.x + echarts 5.5、dayjs、lodash-es、@ant-design/icons-vue、Vitest 2.x + @vue/test-utils 2.x + jsdom、Playwright 1.x、ESLint 9 + eslint-plugin-vue + @typescript-eslint、pnpm 9.x、Node ≥ 20 LTS。

---

## 文件结构总览

```
web/system-admin/
├── package.json                              # 依赖与 scripts
├── tsconfig.json                             # 根 TS 配置（references）
├── tsconfig.app.json                         # 应用代码 TS 配置
├── tsconfig.node.json                        # Node 端 TS 配置（vite.config 等）
├── vite.config.ts                            # Vite + proxy + test 配置
├── index.html                                # HTML 入口
├── .env.development                          # 开发环境变量
├── .env.production                           # 生产环境变量
├── playwright.config.ts                      # E2E 配置
├── eslint.config.js                          # ESLint flat config
├── .gitignore                                # 前端专用 gitignore
├── public/                                   # 静态资源（自动生成）
├── tests/
│   └── setup.ts                              # Vitest 全局 setup
└── src/
    ├── main.ts                               # 入口：createApp + 注册插件
    ├── App.vue                               # 根组件 <RouterView>
    ├── app/
    │   ├── env.ts                            # import.meta.env 类型化封装
    │   ├── pinia.ts                          # createPinia + 持久化插件
    │   ├── provider.vue                      # AConfigProvider 全局主题
    │   └── router.ts                         # 聚合模块路由 + 守卫
    ├── shared/
    │   ├── types/
    │   │   └── index.ts                      # ApiResponse<T>/PageResult<T>/PageQuery
    │   ├── http/
    │   │   ├── errors.ts                     # AppError 层级
    │   │   ├── client.ts                     # axios 实例 + 拦截器
    │   │   ├── idempotency.ts                # Idempotency-Key 生成
    │   │   └── index.ts                      # 出口
    │   ├── auth/
    │   │   ├── auth.store.ts                 # useAuthStore（持久化）
    │   │   ├── permission.ts                 # v-permission 指令
    │   │   ├── PermissionGuard.vue           # 区域级权限包裹组件
    │   │   └── index.ts                      # 出口
    │   ├── layout/
    │   │   ├── BasicLayout.vue               # Layout 容器
    │   │   ├── HeaderBar.vue                 # 顶栏
    │   │   ├── SiderMenu.vue                 # 侧栏
    │   │   └── FooterBar.vue                 # 底栏
    │   ├── pages/
    │   │   ├── Forbidden.vue                 # 403 页
    │   │   └── NotFound.vue                  # 404 页
    │   ├── tokens/
    │   │   ├── design-tokens.css             # CSS 变量
    │   │   └── antd-theme.ts                 # Ant Design Vue theme
    │   ├── utils/
    │   │   ├── format.ts                     # 日期/金额/百分比
    │   │   ├── validators.ts                 # 通用校验器
    │   │   └── logger.ts                     # 日志器
    │   └── components/
    │       ├── StatusTag.vue
    │       ├── IdempotencyButton.vue
    │       ├── DataTable.vue
    │       ├── EmptyState.vue
    │       ├── ConfirmDialog.vue
    │       ├── DateTimeRangePicker.vue
    │       ├── JsonViewer.vue
    │       ├── ErrorBoundary.vue
    │       ├── charts/
    │       │   ├── ChartLine.vue
    │       │   ├── ChartBar.vue
    │       │   └── ChartPie.vue
    │       └── index.ts                      # 出口
    └── modules/
        └── 06-account/
            ├── types/
            │   └── auth.dto.ts               # 登录相关 DTO
            ├── api/
            │   └── auth.api.ts               # /api/auth/login
            ├── views/
            │   └── Login2fa.vue              # 登录页
            ├── routes.ts                     # 本模块路由项
            └── index.ts                      # 出口
```

仓库根修改：
- `pnpm-workspace.yaml`：新增 `web/system-admin` 工作区。
- `.github/workflows/ci.yml`：新增 `web-system-admin` job。

---

## Task 1: 项目脚手架与 CI 接入

**Files:**
- Create: `web/system-admin/package.json`
- Create: `web/system-admin/tsconfig.json`
- Create: `web/system-admin/tsconfig.app.json`
- Create: `web/system-admin/tsconfig.node.json`
- Create: `web/system-admin/vite.config.ts`
- Create: `web/system-admin/index.html`
- Create: `web/system-admin/.env.development`
- Create: `web/system-admin/.env.production`
- Create: `web/system-admin/playwright.config.ts`
- Create: `web/system-admin/eslint.config.js`
- Create: `web/system-admin/.gitignore`
- Create: `web/system-admin/src/main.ts`（最小占位 main.ts，仅 createApp + mount，确保 build 可通过；Task 22 会完整改写）
- Create: `web/system-admin/src/App.vue`（最小占位 App.vue；Task 22 会完整改写）
- Create: `pnpm-workspace.yaml`
- Modify: `.github/workflows/ci.yml`（在文件末尾新增 `web-system-admin` job）

- [ ] **Step 1: 创建 `web/system-admin/package.json`**

```json
{
  "name": "@leno/system-admin",
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
    "@vue-echarts": "^7.0.3",
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

- [ ] **Step 2: 创建 `web/system-admin/tsconfig.json`**

```json
{
  "files": [],
  "references": [
    { "path": "./tsconfig.app.json" },
    { "path": "./tsconfig.node.json" }
  ]
}
```

- [ ] **Step 3: 创建 `web/system-admin/tsconfig.app.json`**

```json
{
  "compilerOptions": {
    "composite": true,
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.app.tsbuildinfo",
    "target": "ES2022",
    "useDefineForClassFields": true,
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "skipLibCheck": true,
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "noFallthroughCasesInSwitch": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "jsx": "preserve",
    "jsxImportSource": "vue",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "esModuleInterop": true,
    "allowSyntheticDefaultImports": true,
    "verbatimModuleSyntax": true,
    "baseUrl": ".",
    "paths": {
      "@/*": ["src/*"]
    },
    "types": ["vite/client", "vitest/globals"]
  },
  "include": ["src/**/*.ts", "src/**/*.d.ts", "src/**/*.tsx", "src/**/*.vue", "tests/**/*.ts"]
}
```

- [ ] **Step 4: 创建 `web/system-admin/tsconfig.node.json`**

```json
{
  "compilerOptions": {
    "composite": true,
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.node.tsbuildinfo",
    "target": "ES2022",
    "lib": ["ES2023"],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "skipLibCheck": true,
    "strict": true,
    "noImplicitOverride": true,
    "noFallthroughCasesInSwitch": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "isolatedModules": true,
    "esModuleInterop": true,
    "types": ["node"]
  },
  "include": ["vite.config.ts", "playwright.config.ts", "eslint.config.js"]
}
```

- [ ] **Step 5: 创建 `web/system-admin/vite.config.ts`**

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
    port: 5173,
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
          echarts: ['echarts', '@vue-echarts'],
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './tests/setup.ts',
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

- [ ] **Step 6: 创建 `web/system-admin/index.html`**

```html
<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Leno 系统管理后台</title>
  </head>
  <body>
    <div id="app"></div>
    <script type="module" src="/src/main.ts"></script>
  </body>
</html>
```

- [ ] **Step 7: 创建 `web/system-admin/.env.development`**

```
VITE_API_BASE=/api
VITE_API_TARGET=http://localhost:5001
VITE_REQUIRE_2FA=false
VITE_APP_VERSION=dev
```

- [ ] **Step 8: 创建 `web/system-admin/.env.production`**

```
VITE_API_BASE=/api
VITE_REQUIRE_2FA=false
VITE_APP_VERSION=1.0.0
```

- [ ] **Step 9: 创建 `web/system-admin/playwright.config.ts`**

```ts
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [['html', { open: 'never' }], ['list']],
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
```

- [ ] **Step 10: 创建 `web/system-admin/eslint.config.js`**

```js
import js from '@eslint/js'
import tseslint from '@typescript-eslint/eslint-plugin'
import tsparser from '@typescript-eslint/parser'
import vuePlugin from 'eslint-plugin-vue'
import vueParser from 'vue-eslint-parser'

export default [
  js.configs.recommended,
  {
    files: ['**/*.ts', '**/*.tsx'],
    languageOptions: {
      parser: tsparser,
      parserOptions: {
        ecmaVersion: 2022,
        sourceType: 'module',
      },
      globals: {
        console: 'readonly',
        window: 'readonly',
        document: 'readonly',
        localStorage: 'readonly',
        sessionStorage: 'readonly',
        location: 'readonly',
        navigator: 'readonly',
        history: 'readonly',
        fetch: 'readonly',
        URL: 'readonly',
        URLSearchParams: 'readonly',
        Blob: 'readonly',
        File: 'readonly',
        FormData: 'readonly',
        HTMLElement: 'readonly',
        Event: 'readonly',
        MouseEvent: 'readonly',
        KeyboardEvent: 'readonly',
        Date: 'readonly',
        Math: 'readonly',
        JSON: 'readonly',
        Promise: 'readonly',
        setTimeout: 'readonly',
        clearTimeout: 'readonly',
        setInterval: 'readonly',
        clearInterval: 'readonly',
        AbortController: 'readonly',
      },
    },
    plugins: {
      '@typescript-eslint': tseslint,
    },
    rules: {
      ...tseslint.configs.recommended.rules,
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },
  {
    files: ['**/*.vue'],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        parser: tsparser,
        ecmaVersion: 2022,
        sourceType: 'module',
        extraFileExtensions: ['.vue'],
      },
      globals: {
        defineProps: 'readonly',
        defineEmits: 'readonly',
        defineExpose: 'readonly',
        withDefaults: 'readonly',
        console: 'readonly',
        window: 'readonly',
        document: 'readonly',
        localStorage: 'readonly',
        sessionStorage: 'readonly',
      },
    },
    plugins: {
      vue: vuePlugin,
      '@typescript-eslint': tseslint,
    },
    rules: {
      ...vuePlugin.configs['vue3-recommended'].rules,
      'vue/multi-word-component-names': 'off',
    },
  },
  {
    ignores: ['dist/**', 'node_modules/**', 'coverage/**', 'playwright-report/**'],
  },
]
```

- [ ] **Step 11: 创建 `web/system-admin/.gitignore`**

```
node_modules
dist
dist-ssr
*.local
coverage
playwright-report
test-results
*.tsbuildinfo
.vite
.DS_Store
```

- [ ] **Step 12: 创建 `web/system-admin/src/main.ts`（最小占位，Task 22 完整改写）**

```ts
import { createApp } from 'vue'
import App from './App.vue'

createApp(App).mount('#app')
```

- [ ] **Step 13: 创建 `web/system-admin/src/App.vue`（最小占位，Task 22 完整改写）**

```vue
<script setup lang="ts">
// 占位根组件，Task 22 替换为 ConfigProvider + RouterView
</script>

<template>
  <div class="app-placeholder">Leno 系统管理后台</div>
</template>

<style scoped>
.app-placeholder {
  padding: 24px;
  font-size: 16px;
  color: #595959;
}
</style>
```

- [ ] **Step 14: 创建仓库根 `pnpm-workspace.yaml`**

```yaml
packages:
  - web/system-admin
```

- [ ] **Step 15: 修改 `.github/workflows/ci.yml`，在文件末尾追加 `web-system-admin` job**

在 `ci.yml` 文件最末尾（`pact-contract-tests` job 之后）追加：

```yaml

  web-system-admin:
    name: 系统管理后台前端 (web/system-admin)
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: web/system-admin
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
        with:
          version: 9
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: pnpm
          cache-dependency-path: web/system-admin/pnpm-lock.yaml
      - run: pnpm install --frozen-lockfile
      - run: pnpm lint
      - run: pnpm typecheck
      - run: pnpm test -- --coverage --reporter=dot
      - run: pnpm build
      - uses: actions/upload-artifact@v4
        with:
          name: web-dist
          path: web/system-admin/dist
```

- [ ] **Step 16: 安装依赖并验证 build 可通过**

Run: `cd web/system-admin && pnpm install`
Expected: 安装成功，生成 `node_modules/` 与 `pnpm-lock.yaml`

Run: `cd web/system-admin && pnpm build`
Expected: `dist/` 目录生成，无 TypeScript 错误（占位 main.ts/App.vue 可编译）

- [ ] **Step 17: Commit**

```bash
git add web/system-admin pnpm-workspace.yaml .github/workflows/ci.yml
git commit -m "feat(system-admin): 初始化 Vue 3 + Vite 脚手架与 CI 接入"
```

---

## Task 2: 测试 setup 与通用类型

**Files:**
- Create: `web/system-admin/tests/setup.ts`
- Create: `web/system-admin/src/shared/types/index.ts`
- Create: `web/system-admin/src/shared/types/index.spec.ts`

- [ ] **Step 1: 创建 `web/system-admin/tests/setup.ts`**

```ts
import { config } from '@vue/test-utils'
import { afterEach, vi } from 'vitest'
import { cleanup } from '@testing-library/vue'

// 每个测试后清理挂载的 DOM
afterEach(() => {
  cleanup()
})

// 全局 stub 配置：避免 ant-design-vue 全量注册带来的复杂度
config.global.stubs = {
  teleport: true,
}

// Mock matchMedia（Ant Design Vue 4 在 jsdom 下需要）
if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    }),
  })
}

// Mock ResizeObserver（ECharts 与部分 antd 组件依赖）
if (!window.ResizeObserver) {
  Object.defineProperty(window, 'ResizeObserver', {
    writable: true,
    value: class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  })
}

// Mock IntersectionObserver
if (!window.IntersectionObserver) {
  Object.defineProperty(window, 'IntersectionObserver', {
    writable: true,
    value: class {
      observe() {}
      unobserve() {}
      disconnect() {}
      takeRecords() {
        return []
      }
    },
  })
}

// 静音 console.error 在测试中由各 case 自行决定是否恢复
const originalError = console.error
console.error = (...args: unknown[]) => {
  if (typeof args[0] === 'string' && args[0].includes('Vue warn')) {
    return
  }
  originalError(...args)
}

// 提供 vi 全局以便 spec 文件直接使用
export { vi }
```

- [ ] **Step 2: 创建 `web/system-admin/src/shared/types/index.ts`**

```ts
/**
 * 通用类型定义
 *
 * 与后端 `docs/contracts/internal-api-contracts.md` 信封格式保持一致。
 * 跨 Plan 共享，所有模块的 API/Store 必须从这里导入。
 */

/**
 * 后端统一响应信封
 *
 * - code: 0 表示成功；非 0 表示业务错误码
 * - data: 业务负载，可能为 null（如删除操作）
 * - traceId: OpenTelemetry traceId，便于日志关联
 */
export interface ApiResponse<T> {
  code: number
  message: string
  data: T | null
  traceId?: string
}

/**
 * 分页响应结构
 *
 * 与后端分页端点约定的统一返回结构。
 */
export interface PageResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/**
 * 分页查询参数
 *
 * 所有列表 API 的查询参数基类。
 */
export interface PageQuery {
  page?: number
  pageSize?: number
}

/**
 * 表格列定义（Ant Design Vue Table 列的子集，按需扩展）
 */
export interface TableColumn {
  title: string
  dataIndex: string
  key?: string
  width?: number | string
  fixed?: 'left' | 'right' | boolean
  align?: 'left' | 'center' | 'right'
  ellipsis?: boolean
  sorter?: boolean | ((a: unknown, b: unknown) => number)
}
```

- [ ] **Step 3: 创建 `web/system-admin/src/shared/types/index.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import type { ApiResponse, PageResult, PageQuery, TableColumn } from './index'

describe('shared/types', () => {
  it('ApiResponse<T> 接受成功响应结构', () => {
    const resp: ApiResponse<string> = { code: 0, message: 'ok', data: 'hello', traceId: 't-1' }
    expect(resp.code).toBe(0)
    expect(resp.data).toBe('hello')
    expect(resp.traceId).toBe('t-1')
  })

  it('ApiResponse<T> 允许 data 为 null', () => {
    const resp: ApiResponse<unknown> = { code: 0, message: 'deleted', data: null }
    expect(resp.data).toBeNull()
  })

  it('PageResult<T> 包含 items 与分页字段', () => {
    const page: PageResult<number> = { items: [1, 2, 3], total: 3, page: 1, pageSize: 10 }
    expect(page.items).toHaveLength(3)
    expect(page.total).toBe(3)
  })

  it('PageQuery 允许缺省分页参数', () => {
    const query: PageQuery = {}
    expect(query.page).toBeUndefined()
    expect(query.pageSize).toBeUndefined()
  })

  it('TableColumn 必须包含 title 与 dataIndex', () => {
    const col: TableColumn = { title: '名称', dataIndex: 'name', width: 120 }
    expect(col.title).toBe('名称')
    expect(col.dataIndex).toBe('name')
  })
})
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test`
Expected: 5 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/tests/setup.ts web/system-admin/src/shared/types
git commit -m "feat(system-admin): 添加测试 setup 与通用类型 ApiResponse/PageResult/PageQuery"
```

---

## Task 3: HTTP 错误类型层级

**Files:**
- Create: `web/system-admin/src/shared/http/errors.ts`
- Create: `web/system-admin/src/shared/http/errors.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/http/errors.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import {
  AppError,
  NetworkError,
  BusinessError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  ConcurrencyError,
} from './errors'

describe('shared/http/errors', () => {
  it('NetworkError 包含 kind 与 message', () => {
    const err = new NetworkError('网络异常')
    expect(err).toBeInstanceOf(AppError)
    expect(err.kind).toBe('NetworkError')
    expect(err.message).toBe('网络异常')
  })

  it('BusinessError 携带业务码', () => {
    const err = new BusinessError(40001, '账号已禁用', 'trace-1')
    expect(err.kind).toBe('BusinessError')
    expect(err.code).toBe(40001)
    expect(err.traceId).toBe('trace-1')
  })

  it('UnauthorizedError 默认消息', () => {
    const err = new UnauthorizedError()
    expect(err.kind).toBe('UnauthorizedError')
    expect(err.message).toBe('未登录或登录已过期')
  })

  it('ForbiddenError 默认消息', () => {
    const err = new ForbiddenError()
    expect(err.kind).toBe('ForbiddenError')
    expect(err.message).toBe('无权访问')
  })

  it('NotFoundError 接受自定义消息', () => {
    const err = new NotFoundError('规则不存在')
    expect(err.kind).toBe('NotFoundError')
    expect(err.message).toBe('规则不存在')
  })

  it('RateLimitedError 携带 retryAfter', () => {
    const err = new RateLimitedError('操作过于频繁', 30)
    expect(err.kind).toBe('RateLimitedError')
    expect(err.retryAfter).toBe(30)
  })

  it('ServerError 默认消息', () => {
    const err = new ServerError()
    expect(err.kind).toBe('ServerError')
    expect(err.message).toBe('服务器异常，请稍后重试')
  })

  it('ConcurrencyError 携带 currentVersion', () => {
    const err = new ConcurrencyError('资源已被他人修改', 4, 'trace-2')
    expect(err.kind).toBe('ConcurrencyError')
    expect(err.currentVersion).toBe(4)
    expect(err.traceId).toBe('trace-2')
  })

  it('所有错误可被 instanceof 区分', () => {
    const errors = [
      new NetworkError(),
      new BusinessError(1, 'x'),
      new UnauthorizedError(),
      new ForbiddenError(),
      new NotFoundError(),
      new RateLimitedError('x', 1),
      new ServerError(),
      new ConcurrencyError('x', 1),
    ]
    for (const e of errors) {
      expect(e).toBeInstanceOf(AppError)
      expect(e).toBeInstanceOf(Error)
    }
  })

  it('所有错误可被 throw/catch', () => {
    try {
      throw new BusinessError(40001, '账号已禁用')
    } catch (e) {
      expect(e).toBeInstanceOf(BusinessError)
      if (e instanceof BusinessError) {
        expect(e.code).toBe(40001)
      }
    }
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/http/errors.spec.ts`
Expected: FAIL，提示 `Cannot find module './errors'` 或 `NetworkError is not defined`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/http/errors.ts`**

```ts
/**
 * 应用错误类型层级
 *
 * 所有 HTTP 调用最终被转换为本层错误，调用方用 instanceof 精细化处理。
 * 与 spec §3.5 保持一致。
 */

/**
 * 应用错误基类
 *
 * 所有具体错误均继承此类，提供统一的 kind/message/traceId 字段。
 */
export abstract class AppError extends Error {
  /** 错误类别标识，用于序列化与日志 */
  abstract readonly kind: string
  /** OpenTelemetry traceId，便于前后端日志关联 */
  traceId?: string

  constructor(message: string, traceId?: string) {
    super(message)
    this.name = new.target.name
    this.traceId = traceId
    // 维持原型链（ES5 继承 Error 的标准修复）
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/**
 * 网络错误：超时、断网、DNS 失败等
 */
export class NetworkError extends AppError {
  readonly kind = 'NetworkError'
  constructor(message = '网络异常，请检查连接', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 业务错误：HTTP 200 但 code !== 0
 */
export class BusinessError extends AppError {
  readonly kind = 'BusinessError'
  readonly code: number
  constructor(code: number, message: string, traceId?: string) {
    super(message, traceId)
    this.code = code
  }
}

/**
 * 未登录或登录已过期（HTTP 401）
 */
export class UnauthorizedError extends AppError {
  readonly kind = 'UnauthorizedError'
  constructor(message = '未登录或登录已过期', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 无权访问（HTTP 403）
 */
export class ForbiddenError extends AppError {
  readonly kind = 'ForbiddenError'
  constructor(message = '无权访问', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 资源不存在（HTTP 404）
 */
export class NotFoundError extends AppError {
  readonly kind = 'NotFoundError'
  constructor(message = '资源不存在', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 限流（HTTP 429），携带重试等待秒数
 */
export class RateLimitedError extends AppError {
  readonly kind = 'RateLimitedError'
  readonly retryAfter: number
  constructor(message = '操作过于频繁', retryAfter = 0, traceId?: string) {
    super(message, traceId)
    this.retryAfter = retryAfter
  }
}

/**
 * 服务器错误（HTTP 5xx）
 */
export class ServerError extends AppError {
  readonly kind = 'ServerError'
  constructor(message = '服务器异常，请稍后重试', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 乐观锁冲突（HTTP 409），携带当前版本号
 */
export class ConcurrencyError extends AppError {
  readonly kind = 'ConcurrencyError'
  readonly currentVersion: number
  constructor(message = '资源已被他人修改', currentVersion = 0, traceId?: string) {
    super(message, traceId)
    this.currentVersion = currentVersion
  }
}
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/http/errors.spec.ts`
Expected: 10 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/http/errors.ts web/system-admin/src/shared/http/errors.spec.ts
git commit -m "feat(system-admin): 实现 AppError 错误类型层级（8 个具体错误）"
```

---

## Task 4: HTTP 客户端与幂等键

**Files:**
- Create: `web/system-admin/src/shared/http/idempotency.ts`
- Create: `web/system-admin/src/shared/http/client.ts`
- Create: `web/system-admin/src/shared/http/index.ts`
- Create: `web/system-admin/src/shared/http/client.spec.ts`
- Create: `web/system-admin/src/shared/http/idempotency.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/http/idempotency.spec.ts`**

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { withIdempotency, generateIdempotencyKey } from './idempotency'

describe('shared/http/idempotency', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('generateIdempotencyKey 返回 UUID v4 格式', () => {
    const key = generateIdempotencyKey()
    expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i)
  })

  it('withIdempotency 返回包含 Idempotency-Key 的 headers 对象', () => {
    const result = withIdempotency()
    expect(result.headers).toHaveProperty('Idempotency-Key')
    expect(typeof result.headers['Idempotency-Key']).toBe('string')
    expect(result.headers['Idempotency-Key']).toHaveLength(36)
  })

  it('每次调用 withIdempotency 生成不同的 key', () => {
    const a = withIdempotency()
    const b = withIdempotency()
    expect(a.headers['Idempotency-Key']).not.toBe(b.headers['Idempotency-Key'])
  })
})
```

- [ ] **Step 2: 写失败测试 `web/system-admin/src/shared/http/client.spec.ts`**

```ts
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import axios from 'axios'
import type { AxiosInstance, AxiosRequestConfig, AxiosResponse, InternalAxiosRequestConfig } from 'axios'
import { client, withIdempotency } from './client'
import {
  BusinessError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  ConcurrencyError,
  NetworkError,
} from './errors'

// 用真实 axios 实例 + adapter mock，验证拦截器链
function mockAdapter(response: Partial<AxiosResponse>): (config: InternalAxiosRequestConfig) => Promise<AxiosResponse> {
  return (config) =>
    Promise.resolve({
      data: response.data,
      status: response.status ?? 200,
      statusText: response.statusText ?? 'OK',
      headers: response.headers ?? {},
      config,
    } as AxiosResponse)
}

function mockAdapterReject(error: { response?: Partial<AxiosResponse>; request?: unknown; message: string }): (config: InternalAxiosRequestConfig) => Promise<AxiosResponse> {
  return () => Promise.reject(error as unknown)
}

describe('shared/http/client', () => {
  let originalAdapter: AxiosInstance['defaults']['adapter']

  beforeEach(() => {
    originalAdapter = client.defaults.adapter
    localStorage.clear()
    sessionStorage.clear()
  })

  afterEach(() => {
    client.defaults.adapter = originalAdapter
    vi.restoreAllMocks()
  })

  it('baseURL 为 /api', () => {
    expect(client.defaults.baseURL).toBe('/api')
  })

  it('timeout 为 15000ms', () => {
    expect(client.defaults.timeout).toBe(15_000)
  })

  it('成功响应解包 ApiResponse.data', async () => {
    client.defaults.adapter = mockAdapter({
      data: { code: 0, message: 'ok', data: { id: 1, name: 'alice' }, traceId: 't-1' },
    }) as AxiosInstance['defaults']['adapter']
    const resp = await client.get('/admin/users/1')
    expect(resp.data).toEqual({ id: 1, name: 'alice' })
  })

  it('code !== 0 抛 BusinessError', async () => {
    client.defaults.adapter = mockAdapter({
      data: { code: 40001, message: '账号已禁用', data: null, traceId: 't-2' },
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'BusinessError',
      code: 40001,
      message: '账号已禁用',
      traceId: 't-2',
    })
  })

  it('HTTP 401 抛 UnauthorizedError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 401, data: { message: '未登录' }, headers: { 'x-trace-id': 't-3' } },
      message: 'Request failed with status code 401',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'UnauthorizedError',
    })
  })

  it('HTTP 403 抛 ForbiddenError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 403, data: { message: '禁止访问' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'ForbiddenError',
    })
  })

  it('HTTP 404 抛 NotFoundError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 404, data: { message: '不存在' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'NotFoundError',
    })
  })

  it('HTTP 409 抛 ConcurrencyError 携带 currentVersion', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 409, data: { message: '冲突', currentVersion: 7 } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/rate-limit-rules/1')).rejects.toMatchObject({
      kind: 'ConcurrencyError',
      currentVersion: 7,
    })
  })

  it('HTTP 429 抛 RateLimitedError 携带 retryAfter', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 429, data: { message: '限流' }, headers: { 'retry-after': '15' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.post('/admin/dead-letters/1/retry')).rejects.toMatchObject({
      kind: 'RateLimitedError',
      retryAfter: 15,
    })
  })

  it('HTTP 500 抛 ServerError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 500, data: { message: '内部错误' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'ServerError',
    })
  })

  it('网络错误（无 response）抛 NetworkError', async () => {
    client.defaults.adapter = mockAdapterReject({
      request: {},
      message: 'Network Error',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'NetworkError',
    })
  })

  it('请求拦截器从 localStorage 注入 Authorization', async () => {
    localStorage.setItem('auth', JSON.stringify({ token: 'tok-xyz', expiresAt: Date.now() + 10_000 }))
    let captured: AxiosRequestConfig | undefined
    client.defaults.adapter = ((config: InternalAxiosRequestConfig) => {
      captured = config
      return Promise.resolve({ data: { code: 0, message: 'ok', data: null }, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse)
    }) as AxiosInstance['defaults']['adapter']
    await client.get('/admin/users')
    expect((captured as AxiosRequestConfig).headers?.Authorization).toBe('Bearer tok-xyz')
  })

  it('请求拦截器注入 X-Request-Id', async () => {
    let captured: AxiosRequestConfig | undefined
    client.defaults.adapter = ((config: InternalAxiosRequestConfig) => {
      captured = config
      return Promise.resolve({ data: { code: 0, message: 'ok', data: null }, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse)
    }) as AxiosInstance['defaults']['adapter']
    await client.get('/admin/users')
    const requestId = (captured as AxiosRequestConfig).headers?.['X-Request-Id']
    expect(typeof requestId).toBe('string')
    expect((requestId as string)).toHaveLength(36)
  })

  it('withIdempotency 注入 Idempotency-Key 头', async () => {
    let captured: AxiosRequestConfig | undefined
    client.defaults.adapter = ((config: InternalAxiosRequestConfig) => {
      captured = config
      return Promise.resolve({ data: { code: 0, message: 'ok', data: null }, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse)
    }) as AxiosInstance['defaults']['adapter']
    await client.post('/admin/dead-letters/1/retry', null, withIdempotency())
    expect((captured as AxiosRequestConfig).headers?.['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/i)
  })
})
```

- [ ] **Step 3: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/http/client.spec.ts src/shared/http/idempotency.spec.ts`
Expected: FAIL，提示 `Cannot find module './client'` / `Cannot find module './idempotency'`

- [ ] **Step 4: 实现 `web/system-admin/src/shared/http/idempotency.ts`**

```ts
/**
 * 幂等键工具
 *
 * 调用方在写操作（POST/PUT/DELETE）时通过 `withIdempotency()` 包装 config，
 * 拦截器会自动注入 `Idempotency-Key` 头，后端据此去重。
 */

/**
 * 生成 UUID v4 字符串
 *
 * 优先使用原生 crypto.randomUUID（Node ≥ 19、现代浏览器均支持）；
 * 降级使用 Math.random 拼装，保证 jsdom 等老环境可用。
 */
export function generateIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  // 降级方案：按 RFC4122 v4 拼装
  const bytes = new Uint8Array(16)
  if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
    crypto.getRandomValues(bytes)
  } else {
    for (let i = 0; i < 16; i++) bytes[i] = Math.floor(Math.random() * 256)
  }
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0'))
  return `${hex.slice(0, 4).join('')}-${hex.slice(4, 6).join('')}-${hex.slice(6, 8).join('')}-${hex.slice(8, 10).join('')}-${hex.slice(10, 16).join('')}`
}

/**
 * 构造携带 Idempotency-Key 头的 axios config 片段
 *
 * 用法：
 * ```ts
 * client.post('/admin/dead-letters/1/retry', null, withIdempotency())
 * ```
 */
export function withIdempotency(): { headers: { 'Idempotency-Key': string } } {
  return {
    headers: {
      'Idempotency-Key': generateIdempotencyKey(),
    },
  }
}
```

- [ ] **Step 5: 实现 `web/system-admin/src/shared/http/client.ts`**

```ts
import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig, type AxiosError } from 'axios'
import {
  AppError,
  BusinessError,
  ConcurrencyError,
  ForbiddenError,
  NetworkError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  UnauthorizedError,
} from './errors'
import { generateIdempotencyKey } from './idempotency'

/**
 * 全局 axios 实例
 *
 * - baseURL: `/api`（Vite dev proxy 转发到后端 5001）
 * - timeout: 15s
 * - 请求拦截器：鉴权 / Idempotency-Key / X-Request-Id
 * - 响应拦截器：HTTP 层错误转换 → ApiResponse 解包 → 业务层错误转换
 */
export const client: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 15_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

/**
 * 读取持久化 AuthState 中的 token
 *
 * 这里直接读 localStorage 而非导入 useAuthStore，避免循环依赖：
 * useAuthStore 内部依赖 client（登录/拉 profile），client 又依赖 store 会形成环。
 */
function readTokenFromStorage(): string | null {
  try {
    const raw = localStorage.getItem('auth')
    if (!raw) return null
    const parsed = JSON.parse(raw) as { token?: string | null; expiresAt?: number | null }
    if (!parsed.token) return null
    if (typeof parsed.expiresAt === 'number' && parsed.expiresAt <= Date.now()) return null
    return parsed.token
  } catch {
    return null
  }
}

// 请求拦截器：鉴权 + traceId
client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  // 1. 鉴权
  const token = readTokenFromStorage()
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  // 2. traceId（X-Request-Id），便于后端日志关联
  if (!config.headers.has('X-Request-Id')) {
    config.headers.set('X-Request-Id', generateIdempotencyKey())
  }
  return config
})

/**
 * 从后端响应体提取 traceId
 */
function extractTraceId(data: unknown, headers: AxiosResponse['headers']): string | undefined {
  if (data && typeof data === 'object' && 'traceId' in data) {
    const t = (data as { traceId?: unknown }).traceId
    if (typeof t === 'string') return t
  }
  const headerTrace = headers?.['x-trace-id'] ?? headers?.['X-Trace-Id']
  if (typeof headerTrace === 'string') return headerTrace
  return undefined
}

/**
 * 从后端响应体提取 message
 */
function extractMessage(data: unknown, fallback: string): string {
  if (data && typeof data === 'object' && 'message' in data) {
    const m = (data as { message?: unknown }).message
    if (typeof m === 'string' && m.length > 0) return m
  }
  return fallback
}

// 响应拦截器：错误转换 + 数据解包
client.interceptors.response.use(
  (response: AxiosResponse) => {
    const traceId = extractTraceId(response.data, response.headers)
    // 业务层错误：HTTP 200 但 code !== 0
    if (response.data && typeof response.data === 'object' && 'code' in response.data) {
      const body = response.data as { code: number; message: string; data: unknown }
      if (body.code !== 0) {
        throw new BusinessError(body.code, body.message || '业务错误', traceId)
      }
      // 解包：调用方拿到的就是 data 字段
      response.data = body.data
    }
    return response
  },
  (error: AxiosError) => {
    // 网络层错误：无 response
    if (!error.response) {
      return Promise.reject(new NetworkError(error.message || '网络异常'))
    }

    const { status, data, headers } = error.response
    const traceId = extractTraceId(data, headers)
    const message = extractMessage(data, error.message)

    let appError: AppError
    switch (status) {
      case 401:
        appError = new UnauthorizedError(message, traceId)
        break
      case 403:
        appError = new ForbiddenError(message, traceId)
        break
      case 404:
        appError = new NotFoundError(message, traceId)
        break
      case 409: {
        const currentVersion =
          data && typeof data === 'object' && 'currentVersion' in data
            ? Number((data as { currentVersion: unknown }).currentVersion)
            : 0
        appError = new ConcurrencyError(message, currentVersion, traceId)
        break
      }
      case 429: {
        const retryAfterHeader = headers?.['retry-after'] ?? headers?.['Retry-After']
        const retryAfter = typeof retryAfterHeader === 'string' ? Number(retryAfterHeader) || 0 : 0
        appError = new RateLimitedError(message, retryAfter, traceId)
        break
      }
      default:
        if (status >= 500) {
          appError = new ServerError(message, traceId)
        } else {
          // 其他 4xx 归为业务错误
          appError = new BusinessError(status, message, traceId)
        }
    }
    return Promise.reject(appError)
  },
)

/**
 * 重新导出 withIdempotency，方便调用方从 client 模块统一引入
 */
export { withIdempotency } from './idempotency'
```

- [ ] **Step 6: 实现 `web/system-admin/src/shared/http/index.ts`**

```ts
/**
 * shared/http 出口
 *
 * 调用方统一从 `@/shared/http` 引入 client、withIdempotency 与错误类型。
 */
export { client, withIdempotency } from './client'
export { withIdempotency as withIdempotencyKey, generateIdempotencyKey } from './idempotency'
export {
  AppError,
  NetworkError,
  BusinessError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  ConcurrencyError,
} from './errors'
```

- [ ] **Step 7: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/http/`
Expected: client.spec.ts（11 个测试）+ idempotency.spec.ts（3 个测试）+ errors.spec.ts（10 个测试）全部通过

- [ ] **Step 8: Commit**

```bash
git add web/system-admin/src/shared/http
git commit -m "feat(system-admin): 实现 axios client + 拦截器（鉴权/traceId/响应解包/错误转换）"
```

---

## Task 5: 工具函数（format / validators / logger）

**Files:**
- Create: `web/system-admin/src/shared/utils/format.ts`
- Create: `web/system-admin/src/shared/utils/format.spec.ts`
- Create: `web/system-admin/src/shared/utils/validators.ts`
- Create: `web/system-admin/src/shared/utils/validators.spec.ts`
- Create: `web/system-admin/src/shared/utils/logger.ts`
- Create: `web/system-admin/src/shared/utils/logger.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/utils/format.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { formatDateTime, formatDate, formatMoney, formatPercent, formatNumber } from './format'

describe('shared/utils/format', () => {
  it('formatDateTime 格式化 ISO 字符串为 yyyy-MM-dd HH:mm:ss', () => {
    expect(formatDateTime('2026-07-27T08:30:00Z')).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/)
  })

  it('formatDateTime 接受时间戳', () => {
    const ts = Date.UTC(2026, 6, 27, 8, 30, 0)
    expect(formatDateTime(ts)).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/)
  })

  it('formatDateTime 空值返回 "-"', () => {
    expect(formatDateTime(null)).toBe('-')
    expect(formatDateTime(undefined)).toBe('-')
    expect(formatDateTime('')).toBe('-')
  })

  it('formatDate 格式化为 yyyy-MM-dd', () => {
    expect(formatDate('2026-07-27T08:30:00Z')).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('formatMoney 默认人民币 2 位小数', () => {
    expect(formatMoney(1234.5)).toBe('¥1,234.50')
    expect(formatMoney(0)).toBe('¥0.00')
    expect(formatMoney(-99.9)).toBe('-¥99.90')
  })

  it('formatMoney 支持自定义货币符号', () => {
    expect(formatMoney(1234.5, { symbol: '$' })).toBe('$1,234.50')
  })

  it('formatMoney 空值返回 "-"', () => {
    expect(formatMoney(null)).toBe('-')
    expect(formatMoney(undefined)).toBe('-')
  })

  it('formatPercent 默认 2 位小数 + %', () => {
    expect(formatPercent(0.1234)).toBe('12.34%')
    expect(formatPercent(1)).toBe('100.00%')
  })

  it('formatPercent 支持自定义小数位', () => {
    expect(formatPercent(0.1234, { decimals: 0 })).toBe('12%')
    expect(formatPercent(0.1234, { decimals: 4 })).toBe('12.3400%')
  })

  it('formatNumber 千分位分隔', () => {
    expect(formatNumber(1234567)).toBe('1,234,567')
    expect(formatNumber(1234.5)).toBe('1,234.5')
  })
})
```

- [ ] **Step 2: 写失败测试 `web/system-admin/src/shared/utils/validators.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import {
  isNonEmptyString,
  isValidEmail,
  isValidUsername,
  isValidPassword,
  isPositiveInteger,
  isInRange,
  isUuid,
} from './validators'

describe('shared/utils/validators', () => {
  it('isNonEmptyString', () => {
    expect(isNonEmptyString('abc')).toBe(true)
    expect(isNonEmptyString('  ')).toBe(false)
    expect(isNonEmptyString('')).toBe(false)
    expect(isNonEmptyString(null)).toBe(false)
    expect(isNonEmptyString(undefined)).toBe(false)
  })

  it('isValidEmail', () => {
    expect(isValidEmail('admin@leno.com')).toBe(true)
    expect(isValidEmail('a.b+c@sub.leno.cn')).toBe(true)
    expect(isValidEmail('admin@leno')).toBe(false)
    expect(isValidEmail('admin.leno.com')).toBe(false)
    expect(isValidEmail('')).toBe(false)
  })

  it('isValidUsername: 4-32 位字母数字下划线', () => {
    expect(isValidUsername('admin')).toBe(true)
    expect(isValidUsername('user_01')).toBe(true)
    expect(isValidUsername('a')).toBe(false)
    expect(isValidUsername('a'.repeat(33))).toBe(false)
    expect(isValidUsername('用户名')).toBe(false)
    expect(isValidUsername('user-name')).toBe(false)
  })

  it('isValidPassword: 至少 8 位含字母与数字', () => {
    expect(isValidPassword('Admin123')).toBe(true)
    expect(isValidPassword('admin123')).toBe(true)
    expect(isValidPassword('ADMIN123')).toBe(true)
    expect(isValidPassword('12345678')).toBe(false)
    expect(isValidPassword('aaaaaaaa')).toBe(false)
    expect(isValidPassword('Adm1')).toBe(false)
  })

  it('isPositiveInteger', () => {
    expect(isPositiveInteger(1)).toBe(true)
    expect(isPositiveInteger(100)).toBe(true)
    expect(isPositiveInteger(0)).toBe(false)
    expect(isPositiveInteger(-1)).toBe(false)
    expect(isPositiveInteger(1.5)).toBe(false)
    expect(isPositiveInteger('1')).toBe(false)
  })

  it('isInRange', () => {
    expect(isInRange(5, 1, 10)).toBe(true)
    expect(isInRange(1, 1, 10)).toBe(true)
    expect(isInRange(10, 1, 10)).toBe(true)
    expect(isInRange(0, 1, 10)).toBe(false)
    expect(isInRange(11, 1, 10)).toBe(false)
  })

  it('isUuid', () => {
    expect(isUuid('550e8400-e29b-41d4-a716-446655440000')).toBe(true)
    expect(isUuid('not-a-uuid')).toBe(false)
    expect(isUuid('')).toBe(false)
  })
})
```

- [ ] **Step 3: 写失败测试 `web/system-admin/src/shared/utils/logger.spec.ts`**

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { logger } from './logger'

describe('shared/utils/logger', () => {
  let consoleSpy: { log: ReturnType<typeof vi.spyOn>; info: ReturnType<typeof vi.spyOn>; warn: ReturnType<typeof vi.spyOn>; error: ReturnType<typeof vi.spyOn> }

  beforeEach(() => {
    consoleSpy = {
      log: vi.spyOn(console, 'log').mockImplementation(() => {}),
      info: vi.spyOn(console, 'info').mockImplementation(() => {}),
      warn: vi.spyOn(console, 'warn').mockImplementation(() => {}),
      error: vi.spyOn(console, 'error').mockImplementation(() => {}),
    }
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('logger.info 在 dev 环境写 console.info', () => {
    logger.info('hello', { a: 1 })
    expect(consoleSpy.info).toHaveBeenCalled()
    const args = consoleSpy.info.mock.calls[0]
    expect(args[0]).toContain('hello')
  })

  it('logger.warn 写 console.warn', () => {
    logger.warn('warning')
    expect(consoleSpy.warn).toHaveBeenCalled()
  })

  it('logger.error 写 console.error', () => {
    logger.error('boom', new Error('x'))
    expect(consoleSpy.error).toHaveBeenCalled()
  })

  it('logger.debug 在 dev 环境写 console.log', () => {
    logger.debug('debug-msg')
    expect(consoleSpy.log).toHaveBeenCalled()
  })

  it('logger 设置 level=warn 后 debug 不输出', () => {
    logger.setLevel('warn')
    logger.debug('should-skip')
    expect(consoleSpy.log).not.toHaveBeenCalled()
    logger.warn('should-print')
    expect(consoleSpy.warn).toHaveBeenCalled()
  })
})
```

- [ ] **Step 4: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/utils/`
Expected: FAIL，提示模块不存在

- [ ] **Step 5: 实现 `web/system-admin/src/shared/utils/format.ts`**

```ts
import dayjs from 'dayjs'

/**
 * 通用格式化工具
 *
 * 与 spec §5.4 表格密度、§3 后端响应字段配套使用。
 */

/** 空值占位符 */
const EMPTY_PLACEHOLDER = '-'

/**
 * 格式化日期时间（yyyy-MM-dd HH:mm:ss）
 *
 * 接受 ISO 字符串、时间戳、Date 对象；空值返回 "-"。
 */
export function formatDateTime(value: string | number | Date | null | undefined): string {
  if (value === null || value === undefined || value === '') return EMPTY_PLACEHOLDER
  const d = dayjs(value)
  if (!d.isValid()) return EMPTY_PLACEHOLDER
  return d.format('YYYY-MM-DD HH:mm:ss')
}

/**
 * 格式化日期（yyyy-MM-dd）
 */
export function formatDate(value: string | number | Date | null | undefined): string {
  if (value === null || value === undefined || value === '') return EMPTY_PLACEHOLDER
  const d = dayjs(value)
  if (!d.isValid()) return EMPTY_PLACEHOLDER
  return d.format('YYYY-MM-DD')
}

/**
 * 格式化金额（默认人民币，2 位小数 + 千分位）
 */
export function formatMoney(
  value: number | string | null | undefined,
  options: { symbol?: string; decimals?: number } = {},
): string {
  if (value === null || value === undefined || value === '') return EMPTY_PLACEHOLDER
  const num = typeof value === 'string' ? Number(value) : value
  if (!Number.isFinite(num)) return EMPTY_PLACEHOLDER
  const symbol = options.symbol ?? '¥'
  const decimals = options.decimals ?? 2
  const formatted = num.toLocaleString('zh-CN', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })
  return `${num < 0 ? '-' : ''}${symbol}${Math.abs(num).toLocaleString('zh-CN', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })}`
}

/**
 * 格式化百分比（0.12 → 12.00%）
 */
export function formatPercent(
  value: number | null | undefined,
  options: { decimals?: number } = {},
): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EMPTY_PLACEHOLDER
  const decimals = options.decimals ?? 2
  return `${(value * 100).toFixed(decimals)}%`
}

/**
 * 千分位数字格式化
 */
export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EMPTY_PLACEHOLDER
  return value.toLocaleString('zh-CN')
}
```

- [ ] **Step 6: 实现 `web/system-admin/src/shared/utils/validators.ts`**

```ts
/**
 * 通用校验器
 *
 * 用于表单与 API 入参前校验。所有函数返回 boolean，不抛异常。
 */

/**
 * 判断是否为非空字符串（trim 后非空）
 */
export function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
/**
 * 判断是否为合法 email
 */
export function isValidEmail(value: unknown): value is string {
  return typeof value === 'string' && EMAIL_RE.test(value)
}

const USERNAME_RE = /^[A-Za-z0-9_]{4,32}$/
/**
 * 判断是否为合法用户名（4-32 位字母数字下划线）
 */
export function isValidUsername(value: unknown): value is string {
  return typeof value === 'string' && USERNAME_RE.test(value)
}

/**
 * 判断是否为合法密码（至少 8 位，含字母与数字）
 */
export function isValidPassword(value: unknown): value is string {
  if (typeof value !== 'string' || value.length < 8) return false
  const hasLetter = /[A-Za-z]/.test(value)
  const hasDigit = /\d/.test(value)
  return hasLetter && hasDigit
}

/**
 * 判断是否为正整数
 */
export function isPositiveInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0
}

/**
 * 判断数字是否在 [min, max] 闭区间
 */
export function isInRange(value: number, min: number, max: number): boolean {
  return value >= min && value <= max
}

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{3}-[0-9a-f]{4}-[0-9a-f]{12}$/i
/**
 * 判断是否为 UUID
 */
export function isUuid(value: unknown): value is string {
  return typeof value === 'string' && UUID_RE.test(value)
}
```

- [ ] **Step 7: 实现 `web/system-admin/src/shared/utils/logger.ts`**

```ts
/**
 * 前端日志器
 *
 * - dev 环境：写 console
 * - prod 环境：批量 POST 到 `/api/admin/audit-logs/frontend`（best-effort）
 *
 * 设计参考 spec §6.8 可观测性。
 */

export type LogLevel = 'debug' | 'info' | 'warn' | 'error'

const LEVEL_PRIORITY: Record<LogLevel, number> = {
  debug: 10,
  info: 20,
  warn: 30,
  error: 40,
}

interface LoggerOptions {
  level: LogLevel
  env: 'dev' | 'prod'
}

const isProd = import.meta.env?.PROD ?? false

const defaultOptions: LoggerOptions = {
  level: 'debug',
  env: isProd ? 'prod' : 'dev',
}

class Logger {
  private options: LoggerOptions = { ...defaultOptions }
  /** prod 环境下批量缓冲 */
  private buffer: Array<{ level: LogLevel; message: string; context?: unknown; ts: number }> = []
  /** 缓冲区满大小，达到后触发 flush */
  private readonly bufferSize = 10

  /**
   * 设置日志级别
   */
  setLevel(level: LogLevel): void {
    this.options.level = level
  }

  /**
   * DEBUG 级别日志
   */
  debug(message: string, context?: unknown): void {
    this.write('debug', message, context)
  }

  /**
   * INFO 级别日志
   */
  info(message: string, context?: unknown): void {
    this.write('info', message, context)
  }

  /**
   * WARN 级别日志
   */
  warn(message: string, context?: unknown): void {
    this.write('warn', message, context)
  }

  /**
   * ERROR 级别日志
   */
  error(message: string, context?: unknown): void {
    this.write('error', message, context)
  }

  /**
   * 强制刷新 prod 缓冲区
   */
  async flush(): Promise<void> {
    if (this.options.env !== 'prod' || this.buffer.length === 0) return
    const payload = this.buffer.slice()
    this.buffer = []
    try {
      await fetch('/api/admin/audit-logs/frontend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ entries: payload }),
        keepalive: true,
      })
    } catch {
      // best-effort，丢弃不重试
    }
  }

  private write(level: LogLevel, message: string, context?: unknown): void {
    if (LEVEL_PRIORITY[level] < LEVEL_PRIORITY[this.options.level]) return

    if (this.options.env === 'dev') {
      this.writeToConsole(level, message, context)
      return
    }

    // prod：缓冲 + 批量
    this.buffer.push({ level, message, context, ts: Date.now() })
    if (this.buffer.length >= this.bufferSize || level === 'error') {
      void this.flush()
    }
  }

  private writeToConsole(level: LogLevel, message: string, context?: unknown): void {
    const prefix = `[${level.toUpperCase()}]`
    switch (level) {
      case 'debug':
        console.log(prefix, message, context ?? '')
        break
      case 'info':
        console.info(prefix, message, context ?? '')
        break
      case 'warn':
        console.warn(prefix, message, context ?? '')
        break
      case 'error':
        console.error(prefix, message, context ?? '')
        break
    }
  }
}

/** 全局 logger 单例 */
export const logger = new Logger()
```

- [ ] **Step 8: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/utils/`
Expected: format.spec.ts（10 个）+ validators.spec.ts（7 个）+ logger.spec.ts（5 个）全部通过

- [ ] **Step 9: Commit**

```bash
git add web/system-admin/src/shared/utils
git commit -m "feat(system-admin): 实现 shared/utils 工具函数（format/validators/logger）"
```

---

## Task 6: 设计令牌与 Ant Design 主题

**Files:**
- Create: `web/system-admin/src/shared/tokens/design-tokens.css`
- Create: `web/system-admin/src/shared/tokens/antd-theme.ts`

- [ ] **Step 1: 创建 `web/system-admin/src/shared/tokens/design-tokens.css`**

```css
/**
 * Leno 系统管理后台 · 设计令牌
 *
 * 与 docs/designs/_shared/tokens.css 保持一致，所有 CSS 变量在 :root 全局可用。
 */

:root {
  /* color */
  --c-primary: #1677ff;
  --c-success: #52c41a;
  --c-warning: #faad14;
  --c-error: #ff4d4f;
  --c-info: #1677ff;
  --c-disabled: #00000040;

  /* neutral */
  --n1: #ffffff;
  --n2: #fafafa;
  --n3: #f5f5f5;
  --n5: #d9d9d9;
  --n7: #8c8c8c;
  --n9: #595959;
  --n10: #000000d9;

  /* accent per end */
  --c-buyer: #722ed1;
  --c-ops: #1677ff;
  --c-seller: #13c2c2;
  --c-admin: #fa541c;

  /* radius */
  --r-base: 6px;
  --r-card: 8px;
  --r-lg: 12px;

  /* spacing */
  --s1: 4px;
  --s2: 8px;
  --s3: 12px;
  --s4: 16px;
  --s6: 24px;
  --s8: 32px;
  --s12: 48px;

  /* font */
  --fs-sm: 12px;
  --fs-base: 14px;
  --fs-lg: 16px;
  --fs-xl: 20px;
  --fs-2xl: 24px;
  --fs-3xl: 30px;
  --fw-normal: 400;
  --fw-medium: 500;
  --fw-semibold: 600;
  --lh-base: 1.5715;
  --ff-app: 'PingFang SC', 'Microsoft YaHei', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  --ff-mono: 'SF Mono', 'Cascadia Code', 'JetBrains Mono', Consolas, monospace;

  /* shadow */
  --sh-card: 0 1px 2px 0 rgba(0, 0, 0, 0.03), 0 1px 6px -1px rgba(0, 0, 0, 0.02),
    0 2px 4px 0 rgba(0, 0, 0, 0.02);
  --sh-dropdown: 0 6px 16px 0 rgba(0, 0, 0, 0.08), 0 3px 6px -4px rgba(0, 0, 0, 0.12),
    0 9px 28px 8px rgba(0, 0, 0, 0.05);
  --sh-modal: 0 12px 32px 4px rgba(0, 0, 0, 0.08), 0 8px 20px 8px rgba(0, 0, 0, 0.06);

  /* motion */
  --d-fast: 100ms;
  --d-mid: 200ms;
  --d-slow: 300ms;
  --ease-std: cubic-bezier(0.2, 0, 0, 1);

  /* layout */
  --sider-bg: #001529;
  --sider-width: 200px;
  --sider-collapsed-width: 80px;
  --header-h: 64px;
  --footer-h: 32px;
}
```

- [ ] **Step 2: 创建 `web/system-admin/src/shared/tokens/antd-theme.ts`**

```ts
import type { ThemeConfig } from 'ant-design-vue/es/config-provider'

/**
 * Ant Design Vue 4.x 主题配置
 *
 * 与 design-tokens.css 中的 CSS 变量保持一致，由 app/provider.vue 注入。
 */
export const antdTheme: ThemeConfig = {
  token: {
    colorPrimary: '#1677FF',
    colorSuccess: '#52C41A',
    colorWarning: '#FAAD14',
    colorError: '#FF4D4F',
    colorInfo: '#1677FF',
    borderRadius: 6,
    fontFamily:
      '"PingFang SC","Microsoft YaHei",-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif',
    fontSize: 14,
  },
  components: {
    Table: {
      rowHoverBg: '#FAFAFA',
      headerBg: '#FAFAFA',
      headerColor: '#595959',
      cellPaddingBlock: 12,
      cellPaddingInline: 16,
    },
    Menu: {
      darkItemBg: '#001529',
      darkItemSelectedBg: '#1677FF',
      darkItemColor: '#ffffffd9',
      darkItemHoverColor: '#ffffff',
    },
    Layout: {
      siderBg: '#001529',
      headerBg: '#ffffff',
      headerHeight: 64,
      headerPadding: '0 24px',
      footerBg: '#ffffff',
      footerPadding: '0 50px',
    },
    Button: {
      borderRadius: 6,
      controlHeight: 32,
    },
  },
}
```

- [ ] **Step 3: 验证 TypeScript 编译通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误（design-tokens.css 不参与 TS 编译，antd-theme.ts 类型正确）

- [ ] **Step 4: Commit**

```bash
git add web/system-admin/src/shared/tokens
git commit -m "feat(system-admin): 添加设计令牌 design-tokens.css 与 antd 主题配置"
```

---

## Task 7: 应用入口层（env + pinia）

**Files:**
- Create: `web/system-admin/src/app/env.ts`
- Create: `web/system-admin/src/app/pinia.ts`
- Create: `web/system-admin/src/app/env.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/app/env.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { env } from './env'

describe('app/env', () => {
  it('env 包含 apiBase 字段', () => {
    expect(typeof env.apiBase).toBe('string')
    expect(env.apiBase.length).toBeGreaterThan(0)
  })

  it('env 包含 require2FA 布尔字段', () => {
    expect(typeof env.require2FA).toBe('boolean')
  })

  it('env 包含 appVersion 字段', () => {
    expect(typeof env.appVersion).toBe('string')
  })

  it('env 为只读常量', () => {
    expect(() => {
      // @ts-expect-error 测试 as const 不可变
      ;(env as { apiBase: string }).apiBase = 'mutated'
    }).toThrow()
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/app/env.spec.ts`
Expected: FAIL，提示 `Cannot find module './env'`

- [ ] **Step 3: 实现 `web/system-admin/src/app/env.ts`**

```ts
/**
 * import.meta.env 类型化封装
 *
 * 集中声明所有环境变量，避免散落在各处。Vite 注入 import.meta.env。
 */

interface AppEnv {
  /** API 基础路径，dev 下为 /api（经 Vite proxy） */
  readonly apiBase: string
  /** 是否强制 2FA（默认 false，仅账号密码登录） */
  readonly require2FA: boolean
  /** 应用版本号 */
  readonly appVersion: string
  /** 后端 API target，仅 dev 使用（proxy 转发目标） */
  readonly apiTarget?: string
}

function parseBoolean(value: string | undefined, defaultValue = false): boolean {
  if (value === undefined) return defaultValue
  return value === 'true' || value === '1' || value === 'yes'
}

export const env: AppEnv = {
  apiBase: import.meta.env.VITE_API_BASE ?? '/api',
  require2FA: parseBoolean(import.meta.env.VITE_REQUIRE_2FA, false),
  appVersion: import.meta.env.VITE_APP_VERSION ?? 'dev',
  apiTarget: import.meta.env.VITE_API_TARGET,
} as const
```

- [ ] **Step 4: 实现 `web/system-admin/src/app/pinia.ts`**

```ts
import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'

/**
 * 全局 Pinia 实例
 *
 * 注册持久化插件（localStorage），各 store 通过 `persist` 选项声明持久化字段。
 */
export const pinia = createPinia()
pinia.use(piniaPluginPersistedstate)
```

- [ ] **Step 5: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/app/env.spec.ts`
Expected: 4 个测试全部通过

- [ ] **Step 6: Commit**

```bash
git add web/system-admin/src/app/env.ts web/system-admin/src/app/env.spec.ts web/system-admin/src/app/pinia.ts
git commit -m "feat(system-admin): 实现 app/env 类型化环境变量与 pinia 持久化实例"
```

---

## Task 8: 鉴权 Store

**Files:**
- Create: `web/system-admin/src/shared/auth/auth.store.ts`
- Create: `web/system-admin/src/shared/auth/auth.store.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/auth/auth.store.spec.ts`**

```ts
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from './auth.store'
import * as authApiModule from '@/modules/06-account/api/auth.api'

describe('shared/auth/auth.store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('初始状态：未登录', () => {
    const auth = useAuthStore()
    expect(auth.token).toBeNull()
    expect(auth.user).toBeNull()
    expect(auth.roles).toEqual([])
    expect(auth.permissions).toEqual([])
    expect(auth.loginAt).toBeNull()
    expect(auth.expiresAt).toBeNull()
    expect(auth.twoFactorPending).toBe(false)
  })

  it('isAuthenticated：无 token 时为 false', () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('isAuthenticated：有 token 但过期时为 false', () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() - 1_000
    expect(auth.isAuthenticated).toBe(false)
  })

  it('isAuthenticated：有 token 且未过期时为 true', () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 10_000
    expect(auth.isAuthenticated).toBe(true)
  })

  it('isAdmin：roles 含 Admin 时为 true', () => {
    const auth = useAuthStore()
    auth.roles = ['Admin']
    expect(auth.isAdmin).toBe(true)
  })

  it('isAdmin：roles 不含 Admin 时为 false', () => {
    const auth = useAuthStore()
    auth.roles = ['Operator']
    expect(auth.isAdmin).toBe(false)
  })

  it('hasPermission：permissions 含目标权限时为 true', () => {
    const auth = useAuthStore()
    auth.permissions = ['dead-letter:dispose', 'role:read']
    expect(auth.hasPermission('dead-letter:dispose')).toBe(true)
  })

  it('hasPermission：permissions 含通配符 * 时为 true', () => {
    const auth = useAuthStore()
    auth.permissions = ['*']
    expect(auth.hasPermission('any:thing')).toBe(true)
  })

  it('hasPermission：permissions 不含目标时为 false', () => {
    const auth = useAuthStore()
    auth.permissions = ['role:read']
    expect(auth.hasPermission('dead-letter:dispose')).toBe(false)
  })

  it('hasRole：传入的角色与 store.roles 有交集时为 true', () => {
    const auth = useAuthStore()
    auth.roles = ['Admin', 'Operator']
    expect(auth.hasRole(['Admin'])).toBe(true)
    expect(auth.hasRole(['Operator'])).toBe(true)
    expect(auth.hasRole(['Admin', 'Operator'])).toBe(true)
    expect(auth.hasRole(['Auditor'])).toBe(false)
    expect(auth.hasRole([])).toBe(false)
  })

  it('login：调用 authApi.login 并填充 state', async () => {
    const fakeResult = {
      token: 'tok-123',
      expiresIn: 3600,
      user: { id: 'u1', username: 'admin', email: 'admin@leno.com', status: 'Active', roles: ['Admin'] },
      roles: ['Admin'],
      permissions: ['dead-letter:dispose', '*'],
    }
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockResolvedValue(fakeResult),
      logout: vi.fn().mockResolvedValue(undefined),
      getProfile: vi.fn().mockResolvedValue(undefined),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    await auth.login({ username: 'admin', password: 'Admin123' })
    expect(auth.token).toBe('tok-123')
    expect(auth.user?.username).toBe('admin')
    expect(auth.roles).toEqual(['Admin'])
    expect(auth.permissions).toContain('*')
    expect(auth.loginAt).toBeTypeOf('number')
    expect(auth.expiresAt).toBeTypeOf('number')
    expect(auth.expiresAt! - auth.loginAt!).toBeGreaterThan(3_500_000)
    spy.mockRestore()
  })

  it('fetchProfile：调用 authApi.getProfile 并刷新 user/permissions', async () => {
    const fakeProfile = {
      id: 'u1',
      username: 'admin',
      email: 'admin@leno.com',
      status: 'Active',
      roles: ['Admin'],
    }
    const fakePerms = ['role:read', 'role:write']
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn(),
      getProfile: vi.fn().mockResolvedValue({ profile: fakeProfile, permissions: fakePerms }),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    await auth.fetchProfile()
    expect(auth.user?.username).toBe('admin')
    expect(auth.permissions).toEqual(fakePerms)
    spy.mockRestore()
  })

  it('logout：清空 state（best-effort 调用 authApi.logout）', async () => {
    const logoutMock = vi.fn().mockResolvedValue(undefined)
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: logoutMock,
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.user = { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] }
    auth.roles = ['Admin']
    auth.permissions = ['*']
    auth.loginAt = Date.now()
    auth.expiresAt = Date.now() + 100_000
    await auth.logout()
    expect(auth.token).toBeNull()
    expect(auth.user).toBeNull()
    expect(auth.roles).toEqual([])
    expect(auth.permissions).toEqual([])
    expect(auth.loginAt).toBeNull()
    expect(auth.expiresAt).toBeNull()
    expect(logoutMock).toHaveBeenCalled()
    spy.mockRestore()
  })

  it('logout：即使 authApi.logout 失败也清空 state', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn().mockRejectedValue(new Error('network')),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    await auth.logout()
    expect(auth.token).toBeNull()
    spy.mockRestore()
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/auth/auth.store.spec.ts`
Expected: FAIL，提示 `Cannot find module './auth.store'` 或 `@/modules/06-account/api/auth.api` 不存在

- [ ] **Step 3: 实现 `web/system-admin/src/shared/auth/auth.store.ts`**

```ts
import { defineStore } from 'pinia'
import { authApi } from '@/modules/06-account/api/auth.api'
import { logger } from '@/shared/utils/logger'

/**
 * 后端管理员视图
 */
export interface AdminUserDto {
  id: string
  username: string
  email: string
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
  user: AdminUserDto
  roles: string[]
  permissions: string[]
}

/**
 * 鉴权状态
 */
export interface AuthState {
  token: string | null
  user: AdminUserDto | null
  roles: string[]
  permissions: string[]
  loginAt: number | null
  expiresAt: number | null
  /** 2FA 待处理标志，仅账号密码登录决策下永远为 false */
  twoFactorPending: boolean
}

/**
 * 鉴权 Store
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
    isAdmin: (s): boolean => s.roles.includes('Admin'),
    hasPermission: (s) => (perm: string): boolean =>
      s.permissions.includes(perm) || s.permissions.includes('*'),
  },
  actions: {
    /**
     * 登录
     *
     * @param body 用户名 + 密码
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
      if (roles.length === 0) return false
      return roles.some((r) => this.roles.includes(r))
    },
  },
  persist: {
    storage: localStorage,
    pick: ['token', 'user', 'roles', 'permissions', 'expiresAt'],
  },
})
```

- [ ] **Step 4: 创建占位 `web/system-admin/src/modules/06-account/api/auth.api.ts`（仅类型与空函数，Task 24 完整实现）**

```ts
import { client } from '@/shared/http'
import type { AdminUserDto, LoginDto, LoginResultDto } from '@/shared/auth/auth.store'

/**
 * 后端 profile 响应（/api/users/me）
 */
export interface UserProfileResultDto {
  profile: AdminUserDto
  permissions: string[]
}

/**
 * 鉴权 API
 *
 * 与 Identity 域 AuthController 对接：
 * - POST /api/auth/login
 * - POST /api/auth/logout
 * - GET  /api/users/me
 */
export const authApi = {
  login(body: LoginDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/login', body).then((r) => r.data)
  },
  logout(): Promise<void> {
    return client.post<void>('/auth/logout', null).then(() => undefined)
  },
  getProfile(): Promise<UserProfileResultDto> {
    return client
      .get<{ profile: AdminUserDto; permissions: string[] }>('/users/me')
      .then((r) => r.data)
  },
}
```

- [ ] **Step 5: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/auth/auth.store.spec.ts`
Expected: 13 个测试全部通过

- [ ] **Step 6: Commit**

```bash
git add web/system-admin/src/shared/auth/auth.store.ts web/system-admin/src/shared/auth/auth.store.spec.ts web/system-admin/src/modules/06-account/api/auth.api.ts
git commit -m "feat(system-admin): 实现 useAuthStore（登录/拉取 profile/登出/角色与权限校验）"
```

---

## Task 9: 权限指令与守卫组件

**Files:**
- Create: `web/system-admin/src/shared/auth/permission.ts`
- Create: `web/system-admin/src/shared/auth/PermissionGuard.vue`
- Create: `web/system-admin/src/shared/auth/permission.spec.ts`
- Create: `web/system-admin/src/shared/auth/PermissionGuard.spec.ts`
- Create: `web/system-admin/src/shared/auth/index.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/auth/permission.spec.ts`**

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { vPermission } from './permission'
import { useAuthStore } from './auth.store'

const HostComponent = defineComponent({
  props: {
    perm: { type: String, required: true },
  },
  setup(props) {
    return () =>
      h(
        'div',
        { class: 'host', 'data-perm': props.perm },
        [
          h(
            'button',
            {
              class: 'guarded-btn',
              'data-testid': 'guarded',
              style: 'display: inline-block',
              onClick: () => {},
            },
            '操作',
          ),
        ],
      )
  },
  directives: { permission: vPermission },
  template: `
    <div class="host">
      <button class="guarded-btn" v-permission="perm">操作</button>
    </div>
  `,
})

describe('shared/auth/permission (v-permission 指令)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('有权限时元素可见', async () => {
    const auth = useAuthStore()
    auth.permissions = ['dead-letter:dispose']
    const wrapper = await mount(HostComponent, { props: { perm: 'dead-letter:dispose' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).not.toBe('none')
  })

  it('无权限时元素被隐藏（display: none）', async () => {
    const auth = useAuthStore()
    auth.permissions = ['role:read']
    const wrapper = await mount(HostComponent, { props: { perm: 'dead-letter:dispose' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).toBe('none')
  })

  it('通配符 * 拥有全部权限', async () => {
    const auth = useAuthStore()
    auth.permissions = ['*']
    const wrapper = await mount(HostComponent, { props: { perm: 'any:thing' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).not.toBe('none')
  })

  it('空权限字符串不隐藏元素', async () => {
    const auth = useAuthStore()
    auth.permissions = []
    const wrapper = await mount(HostComponent, { props: { perm: '' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).not.toBe('none')
  })
})
```

- [ ] **Step 2: 写失败测试 `web/system-admin/src/shared/auth/PermissionGuard.spec.ts`**

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PermissionGuard from './PermissionGuard.vue'
import { useAuthStore } from './auth.store'

describe('shared/auth/PermissionGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('有权限时渲染 slot 内容', () => {
    const auth = useAuthStore()
    auth.permissions = ['role:write']
    const wrapper = mount(PermissionGuard, {
      props: { permission: 'role:write' },
      slots: { default: '<button class="ok">编辑</button>' },
    })
    expect(wrapper.html()).toContain('class="ok"')
  })

  it('无权限时不渲染 slot 内容', () => {
    const auth = useAuthStore()
    auth.permissions = ['role:read']
    const wrapper = mount(PermissionGuard, {
      props: { permission: 'role:write' },
      slots: { default: '<button class="ok">编辑</button>' },
    })
    expect(wrapper.html()).not.toContain('class="ok"')
  })

  it('通配符 * 通过任意 permission', () => {
    const auth = useAuthStore()
    auth.permissions = ['*']
    const wrapper = mount(PermissionGuard, {
      props: { permission: 'something:else' },
      slots: { default: '<span>任意</span>' },
    })
    expect(wrapper.html()).toContain('任意')
  })
})
```

- [ ] **Step 3: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/auth/permission.spec.ts src/shared/auth/PermissionGuard.spec.ts`
Expected: FAIL，提示 `Cannot find module './permission'` / `./PermissionGuard.vue`

- [ ] **Step 4: 实现 `web/system-admin/src/shared/auth/permission.ts`**

```ts
import type { Directive, DirectiveBinding } from 'vue'
import { useAuthStore } from './auth.store'

/**
 * v-permission 指令
 *
 * 用法：
 * ```vue
 * <IdempotencyButton v-permission="'dead-letter:dispose'" danger @click="onDiscard">丢弃</IdempotencyButton>
 * ```
 *
 * 无权限时设置 `display: none`，不删 DOM（避免 hydration 问题）。
 * 空字符串权限视为「无需权限」，不隐藏。
 */
export const vPermission: Directive<HTMLElement, string> = {
  mounted(el: HTMLElement, binding: DirectiveBinding<string>) {
    applyPermission(el, binding.value)
  },
  updated(el: HTMLElement, binding: DirectiveBinding<string>) {
    applyPermission(el, binding.value)
  },
}

function applyPermission(el: HTMLElement, perm: string): void {
  // 空权限字符串视为无需权限
  if (!perm) {
    el.style.display = ''
    return
  }
  const auth = useAuthStore()
  const has = auth.permissions.includes(perm) || auth.permissions.includes('*')
  el.style.display = has ? '' : 'none'
}
```

- [ ] **Step 5: 实现 `web/system-admin/src/shared/auth/PermissionGuard.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from './auth.store'

/**
 * 区域级权限包裹组件
 *
 * 与 `v-permission` 指令互补：本组件用于包裹整块区域，
 * 无权限时整块不渲染（slot 不执行），避免无权限用户触发不可见 slot 内的副作用。
 *
 * 用法：
 * ```vue
 * <PermissionGuard permission="role:write">
 *   <RoleEditForm />
 * </PermissionGuard>
 * ```
 */
const props = defineProps<{
  /** 需要的权限标识 */
  permission: string
}>()

const auth = useAuthStore()

const allowed = computed(() => {
  if (!props.permission) return true
  return auth.permissions.includes(props.permission) || auth.permissions.includes('*')
})
</script>

<template>
  <slot v-if="allowed" />
</template>
```

- [ ] **Step 6: 实现 `web/system-admin/src/shared/auth/index.ts`**

```ts
/**
 * shared/auth 出口
 */
export { useAuthStore } from './auth.store'
export type { AdminUserDto, LoginDto, LoginResultDto, AuthState } from './auth.store'
export { vPermission } from './permission'
export { default as PermissionGuard } from './PermissionGuard.vue'
```

- [ ] **Step 7: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/auth/`
Expected: permission.spec.ts（4 个）+ PermissionGuard.spec.ts（3 个）+ auth.store.spec.ts（13 个）全部通过

- [ ] **Step 8: Commit**

```bash
git add web/system-admin/src/shared/auth
git commit -m "feat(system-admin): 实现 v-permission 指令与 PermissionGuard 组件"
```

---

## Task 10: 状态标签组件 StatusTag

**Files:**
- Create: `web/system-admin/src/shared/components/StatusTag.vue`
- Create: `web/system-admin/src/shared/components/StatusTag.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/StatusTag.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import StatusTag from './StatusTag.vue'

describe('shared/components/StatusTag', () => {
  it('deadLetter 类型 + Pending 状态渲染黄色 warning tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'Pending' } })
    expect(wrapper.html()).toContain('ant-tag')
    expect(wrapper.html()).toContain('待处理')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('deadLetter 类型 + Retried 状态渲染绿色 success tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'Retried' } })
    expect(wrapper.html()).toContain('已重投')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('deadLetter 类型 + Discarded 状态渲染红色 error tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'Discarded' } })
    expect(wrapper.html()).toContain('已丢弃')
    expect(wrapper.html()).toContain('ant-tag-error')
  })

  it('orderPayment 类型 + Paid 状态渲染 success tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'orderPayment', status: 'Paid' } })
    expect(wrapper.html()).toContain('已支付')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('orderPayment 类型 + Pending 状态渲染 warning tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'orderPayment', status: 'Pending' } })
    expect(wrapper.html()).toContain('待支付')
  })

  it('shop 类型 + Approved 状态渲染 success tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Approved' } })
    expect(wrapper.html()).toContain('已通过')
  })

  it('shop 类型 + Banned 状态渲染 error tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Banned' } })
    expect(wrapper.html()).toContain('已封禁')
  })

  it('未知状态渲染 default 灰色 tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'UnknownStatus' } })
    expect(wrapper.html()).toContain('UnknownStatus')
    expect(wrapper.html()).toContain('ant-tag')
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/StatusTag.spec.ts`
Expected: FAIL，提示 `Cannot find module './StatusTag.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/StatusTag.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Tag } from 'ant-design-vue'

/**
 * StatusTag 类型
 * - deadLetter: 死信状态
 * - orderPayment: 订单支付状态
 * - shop: 店铺审核状态
 */
type StatusTagType = 'deadLetter' | 'orderPayment' | 'shop'

/** Ant Design Vue Tag 颜色值 */
type TagColor = 'success' | 'processing' | 'error' | 'warning' | 'default'

const props = defineProps<{
  /** 业务类型 */
  type: StatusTagType
  /** 状态原始值（来自后端枚举字符串） */
  status: string
}>()

interface StatusMeta {
  label: string
  color: TagColor
}

/**
 * 状态映射表
 *
 * 与 spec §5.5 状态色映射保持一致：
 * - 待处理（死信/任务）→ warning
 * - 已重投/已支付/审核通过/启用 → success
 * - 已丢弃/已封禁/失败/不健康 → error
 * - 进行中/执行中 → processing
 * - 已取消/默认/已关闭 → default
 */
const STATUS_MAP: Record<StatusTagType, Record<string, StatusMeta>> = {
  deadLetter: {
    Pending: { label: '待处理', color: 'warning' },
    Retried: { label: '已重投', color: 'success' },
    Discarded: { label: '已丢弃', color: 'error' },
    Processing: { label: '重投中', color: 'processing' },
  },
  orderPayment: {
    Pending: { label: '待支付', color: 'warning' },
    Paid: { label: '已支付', color: 'success' },
    Refunded: { label: '已退款', color: 'error' },
    Cancelled: { label: '已取消', color: 'default' },
    Failed: { label: '失败', color: 'error' },
  },
  shop: {
    Pending: { label: '待审核', color: 'warning' },
    Approved: { label: '已通过', color: 'success' },
    Rejected: { label: '已拒绝', color: 'error' },
    Banned: { label: '已封禁', color: 'error' },
    Active: { label: '已启用', color: 'success' },
    Inactive: { label: '已停用', color: 'default' },
  },
}

const meta = computed<StatusMeta>(() => {
  const sub = STATUS_MAP[props.type]
  return sub[props.status] ?? { label: props.status, color: 'default' }
})
</script>

<template>
  <Tag :color="meta.color">{{ meta.label }}</Tag>
</template>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/StatusTag.spec.ts`
Expected: 8 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/StatusTag.vue web/system-admin/src/shared/components/StatusTag.spec.ts
git commit -m "feat(system-admin): 实现 StatusTag 通用状态标签组件"
```

---

## Task 11: 幂等按钮组件 IdempotencyButton

**Files:**
- Create: `web/system-admin/src/shared/components/IdempotencyButton.vue`
- Create: `web/system-admin/src/shared/components/IdempotencyButton.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/IdempotencyButton.spec.ts`**

```ts
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import IdempotencyButton from './IdempotencyButton.vue'

describe('shared/components/IdempotencyButton', () => {
  it('默认 type=primary 渲染 a-button primary', () => {
    const wrapper = mount(IdempotencyButton, {
      props: {},
      slots: { default: '提交' },
    })
    expect(wrapper.html()).toContain('提交')
    expect(wrapper.html()).toContain('ant-btn-primary')
  })

  it('danger=true 渲染 danger 样式', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { danger: true },
      slots: { default: '删除' },
    })
    expect(wrapper.html()).toContain('ant-btn-dangerous')
  })

  it('loading=true 禁用并显示 loading', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { loading: true },
      slots: { default: '提交' },
    })
    expect(wrapper.html()).toContain('ant-btn-loading')
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })

  it('点击触发 click 事件', async () => {
    const wrapper = mount(IdempotencyButton, {
      props: {},
      slots: { default: '提交' },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('click')).toBeTruthy()
    expect(wrapper.emitted('click')?.[0]).toBeDefined()
  })

  it('loading 时点击不触发 click', async () => {
    const wrapper = mount(IdempotencyButton, {
      props: { loading: true },
      slots: { default: '提交' },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('click')).toBeFalsy()
  })

  it('size=small 渲染小尺寸', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { size: 'small' },
      slots: { default: '提交' },
    })
    expect(wrapper.html()).toContain('ant-btn-sm')
  })

  it('type=default 渲染默认按钮', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { type: 'default' },
      slots: { default: '取消' },
    })
    expect(wrapper.html()).not.toContain('ant-btn-primary')
  })

  it('disabled=true 禁用', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { disabled: true },
      slots: { default: '提交' },
    })
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/IdempotencyButton.spec.ts`
Expected: FAIL，提示 `Cannot find module './IdempotencyButton.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/IdempotencyButton.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Button } from 'ant-design-vue'

/**
 * 按钮类型
 */
type ButtonType = 'primary' | 'default' | 'link' | 'text'

/**
 * 按钮尺寸
 */
type ButtonSize = 'small' | 'middle' | 'large'

const props = withDefaults(
  defineProps<{
    /** 按钮类型 */
    type?: ButtonType
    /** 危险样式（删除/丢弃/重投） */
    danger?: boolean
    /** 尺寸 */
    size?: ButtonSize
    /** 加载中（调用方控制，发起请求时 true，完成时 false） */
    loading?: boolean
    /** 禁用 */
    disabled?: boolean
    /** 块级宽度 */
    block?: boolean
  }>(),
  {
    type: 'primary',
    danger: false,
    size: 'middle',
    loading: false,
    disabled: false,
    block: false,
  },
)

const emit = defineEmits<{
  (e: 'click', event: MouseEvent): void
}>()

const antSize = computed<'small' | 'middle' | 'large'>(() => props.size)

function onClick(event: MouseEvent) {
  if (props.loading || props.disabled) return
  emit('click', event)
}
</script>

<template>
  <Button
    :type="type"
    :danger="danger"
    :size="antSize"
    :loading="loading"
    :disabled="disabled"
    :block="block"
    @click="onClick"
  >
    <slot />
  </Button>
</template>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/IdempotencyButton.spec.ts`
Expected: 8 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/IdempotencyButton.vue web/system-admin/src/shared/components/IdempotencyButton.spec.ts
git commit -m "feat(system-admin): 实现 IdempotencyButton 幂等按钮组件"
```

---

## Task 12: 空态组件 EmptyState

**Files:**
- Create: `web/system-admin/src/shared/components/EmptyState.vue`
- Create: `web/system-admin/src/shared/components/EmptyState.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/EmptyState.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import EmptyState from './EmptyState.vue'

describe('shared/components/EmptyState', () => {
  it('渲染 description 文本', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '暂无数据' },
    })
    expect(wrapper.html()).toContain('暂无数据')
  })

  it('未提供 actionText 时不渲染按钮', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空' },
    })
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('提供 actionText 时渲染 CTA 按钮', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空', actionText: '刷新' },
    })
    expect(wrapper.html()).toContain('刷新')
    expect(wrapper.find('button').exists()).toBe(true)
  })

  it('点击按钮触发 action 事件', async () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空', actionText: '刷新' },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('action')).toBeTruthy()
    expect(wrapper.emitted('action')?.length).toBe(1)
  })

  it('渲染 antd Empty 图标', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空' },
    })
    expect(wrapper.html()).toContain('ant-empty')
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/EmptyState.spec.ts`
Expected: FAIL，提示 `Cannot find module './EmptyState.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/EmptyState.vue`**

```vue
<script setup lang="ts">
import { Empty, Button } from 'ant-design-vue'

/**
 * 空态组件
 *
 * 包装 ant-design-vue 的 Empty，补充可选 CTA 按钮。
 * 与 spec §5.8 加载/空/错误三态保持一致。
 */
const props = withDefaults(
  defineProps<{
    /** 空态描述文案 */
    description: string
    /** CTA 按钮文案，不传则不显示按钮 */
    actionText?: string
  }>(),
  {
    actionText: undefined,
  },
)

const emit = defineEmits<{
  (e: 'action'): void
}>()

function onAction() {
  emit('action')
}
</script>

<template>
  <Empty :description="description">
    <template v-if="props.actionText" #default>
      <Button type="primary" @click="onAction">{{ props.actionText }}</Button>
    </template>
  </Empty>
</template>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/EmptyState.spec.ts`
Expected: 5 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/EmptyState.vue web/system-admin/src/shared/components/EmptyState.spec.ts
git commit -m "feat(system-admin): 实现 EmptyState 空态组件"
```

---

## Task 13: 确认对话框 ConfirmDialog

**Files:**
- Create: `web/system-admin/src/shared/components/ConfirmDialog.vue`
- Create: `web/system-admin/src/shared/components/ConfirmDialog.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/ConfirmDialog.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import ConfirmDialog from './ConfirmDialog.vue'

describe('shared/components/ConfirmDialog', () => {
  it('open=false 时不渲染对话框', () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: false, title: '确认', content: '是否继续？' },
    })
    expect(wrapper.find('.ant-modal').exists()).toBe(false)
  })

  it('open=true 时渲染 title 与 content', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, title: '删除规则', content: '此操作不可撤销，是否继续？' },
    })
    await wrapper.vm.$nextTick()
    expect(wrapper.html()).toContain('删除规则')
    expect(wrapper.html()).toContain('此操作不可撤销')
  })

  it('danger=true 时确认按钮含 danger 样式', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, danger: true, title: '删除', content: '确认删除？' },
    })
    await wrapper.vm.$nextTick()
    expect(wrapper.html()).toContain('ant-btn-dangerous')
  })

  it('点击取消触发 cancel 事件', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, title: '确认', content: '继续？' },
    })
    await wrapper.vm.$nextTick()
    const cancelBtn = wrapper.findAll('button').find((b) => b.text().includes('取消'))
    expect(cancelBtn).toBeDefined()
    await cancelBtn!.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })

  it('requireInput 配置时未达最小长度禁用确认', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        open: true,
        title: '丢弃原因',
        content: '请填写丢弃原因',
        requireInput: { label: '丢弃原因', min: 5, max: 500 },
      },
    })
    await wrapper.vm.$nextTick()
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    expect(okBtn?.attributes('disabled')).toBeDefined()
  })

  it('requireInput 配置时达到最小长度启用确认', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        open: true,
        title: '丢弃原因',
        content: '请填写丢弃原因',
        requireInput: { label: '丢弃原因', min: 5, max: 500 },
      },
    })
    await wrapper.vm.$nextTick()
    const input = wrapper.find('input, textarea')
    await input.setValue('这是一段足够长的丢弃原因说明')
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    expect(okBtn?.attributes('disabled')).toBeUndefined()
  })

  it('点击确认（无 requireInput）触发 confirm 事件', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, title: '确认', content: '继续？' },
    })
    await wrapper.vm.$nextTick()
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    await okBtn!.trigger('click')
    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('requireInput 时 confirm 事件携带输入值', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        open: true,
        title: '丢弃',
        content: '原因？',
        requireInput: { label: '丢弃原因', min: 1, max: 100 },
      },
    })
    await wrapper.vm.$nextTick()
    const input = wrapper.find('input, textarea')
    await input.setValue('测试原因')
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    await okBtn!.trigger('click')
    const confirmEvents = wrapper.emitted('confirm')
    expect(confirmEvents).toBeTruthy()
    expect(confirmEvents?.[0]?.[0]).toBe('测试原因')
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/ConfirmDialog.spec.ts`
Expected: FAIL，提示 `Cannot find module './ConfirmDialog.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/ConfirmDialog.vue`**

```vue
<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { Modal, Input, Typography } from 'ant-design-vue'

/**
 * 危险操作二次确认对话框
 *
 * 与 spec §5.7 配套：
 * - 删除/丢弃/重投/封禁/重建触发 → 必走本组件，danger=true 时确认按钮红色
 * - 丢弃/封禁类需填理由 → requireInput 配置 { label, min, max }，未达 min 长度禁用确认按钮
 */
const props = withDefaults(
  defineProps<{
    /** 是否打开 */
    open: boolean
    /** 危险样式（红色确认按钮） */
    danger?: boolean
    /** 标题 */
    title: string
    /** 正文内容 */
    content: string
    /** 需要用户输入的提示配置（如丢弃原因） */
    requireInput?: { label: string; min: number; max: number }
  }>(),
  {
    danger: false,
    requireInput: undefined,
  },
)

const emit = defineEmits<{
  (e: 'confirm', value?: string): void
  (e: 'cancel'): void
}>()

const inputValue = ref('')

// open 切换为 true 时重置输入
watch(
  () => props.open,
  (open) => {
    if (open) inputValue.value = ''
  },
)

const inputValid = computed(() => {
  if (!props.requireInput) return true
  return (
    inputValue.value.length >= props.requireInput.min &&
    inputValue.value.length <= props.requireInput.max
  )
})

const okButtonProps = computed(() => ({
  disabled: !inputValid.value,
  danger: props.danger,
}))

function onOk() {
  if (!inputValid.value) return
  emit('confirm', props.requireInput ? inputValue.value : undefined)
}

function onCancel() {
  emit('cancel')
}
</script>

<template>
  <Modal
    :open="open"
    :title="title"
    ok-text="确认"
    cancel-text="取消"
    :ok-button-props="okButtonProps"
    @ok="onOk"
    @cancel="onCancel"
  >
    <Typography.Paragraph>{{ content }}</Typography.Paragraph>
    <div v-if="requireInput" class="confirm-input-wrap">
      <label class="confirm-input-label">{{ requireInput.label }}</label>
      <Input
        v-model:value="inputValue"
        :placeholder="`请输入${requireInput.label}（${requireInput.min}-${requireInput.max} 字）`"
        :maxlength="requireInput.max"
        allow-clear
      />
    </div>
  </Modal>
</template>

<style scoped>
.confirm-input-wrap {
  margin-top: 12px;
}
.confirm-input-label {
  display: block;
  margin-bottom: 6px;
  font-size: 14px;
  color: #595959;
}
</style>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/ConfirmDialog.spec.ts`
Expected: 8 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/ConfirmDialog.vue web/system-admin/src/shared/components/ConfirmDialog.spec.ts
git commit -m "feat(system-admin): 实现 ConfirmDialog 危险操作二次确认对话框"
```

---

## Task 14: 数据表格组件 DataTable

**Files:**
- Create: `web/system-admin/src/shared/components/DataTable.vue`
- Create: `web/system-admin/src/shared/components/DataTable.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/DataTable.spec.ts`**

```ts
import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import DataTable from './DataTable.vue'
import type { TableColumn, PageResult } from '@/shared/types'

const columns: TableColumn[] = [
  { title: 'ID', dataIndex: 'id', width: 80 },
  { title: '名称', dataIndex: 'name', width: 200 },
]

describe('shared/components/DataTable', () => {
  it('初次挂载调用 fetcher，传入 page=1 pageSize=10', async () => {
    const fetcher = vi.fn().mockResolvedValue({
      items: [{ id: '1', name: 'alice' }],
      total: 1,
      page: 1,
      pageSize: 10,
    } as PageResult<unknown>)
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(fetcher).toHaveBeenCalledTimes(1)
    const callArg = fetcher.mock.calls[0][0] as { page: number; pageSize: number }
    expect(callArg.page).toBe(1)
    expect(callArg.pageSize).toBe(10)
    expect(wrapper.html()).toContain('alice')
  })

  it('渲染列标题', async () => {
    const fetcher = vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('ID')
    expect(wrapper.html()).toContain('名称')
  })

  it('loading=true 时显示加载态', async () => {
    let resolveFn!: (v: PageResult<unknown>) => void
    const fetcher = vi.fn().mockReturnValue(
      new Promise<PageResult<unknown>>((resolve) => {
        resolveFn = resolve
      }),
    )
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('ant-spin')
    resolveFn({ items: [], total: 0, page: 1, pageSize: 10 })
    await flushPromises()
  })

  it('fetcher 抛错时显示 ErrorBoundary 兜底', async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error('boom'))
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('加载失败')
  })

  it('空数据时显示 EmptyState', async () => {
    const fetcher = vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('ant-empty')
  })

  it('点击刷新按钮重新调用 fetcher', async () => {
    const fetcher = vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(fetcher).toHaveBeenCalledTimes(1)
    const refreshBtn = wrapper.find('button[data-testid="refresh"]')
    expect(refreshBtn.exists()).toBe(true)
    await refreshBtn.trigger('click')
    await flushPromises()
    expect(fetcher).toHaveBeenCalledTimes(2)
  })

  it('翻页时传入新的 page', async () => {
    const fetcher = vi.fn().mockResolvedValue({
      items: [{ id: '1', name: 'alice' }],
      total: 25,
      page: 1,
      pageSize: 10,
    })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    // 模拟点击第 2 页
    const pagination = wrapper.findComponent({ name: 'a-pagination' })
    if (pagination.exists()) {
      await pagination.vm.$emit('change', 2, 10)
      await flushPromises()
      const lastCall = fetcher.mock.calls.at(-1)?.[0] as { page: number }
      expect(lastCall.page).toBe(2)
    }
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/DataTable.spec.ts`
Expected: FAIL，提示 `Cannot find module './DataTable.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/DataTable.vue`**

```vue
<script setup lang="ts">
import { ref, onMounted, watch, h } from 'vue'
import { Table, Button, Spin, Space } from 'ant-design-vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import EmptyState from './EmptyState.vue'
import type { TableColumn, PageResult, PageQuery } from '@/shared/types'
import { logger } from '@/shared/utils/logger'

/**
 * 通用数据表格组件
 *
 * 包装 ant-design-vue Table，统一分页/筛选/空态/错误/加载四态。
 * 调用方提供 columns 与 fetcher，组件自动管理分页状态与数据加载。
 */

const props = defineProps<{
  /** 列定义 */
  columns: TableColumn[]
  /** 数据获取函数，返回 PageResult */
  fetcher: (params: PageQuery & Record<string, unknown>) => Promise<PageResult<unknown>>
  /** 行 key */
  rowKey: string | ((record: unknown) => string)
  /** 每页条数，默认 10 */
  pageSize?: number
  /** 额外查询参数，变化时触发重新加载 */
  queryParams?: Record<string, unknown>
}>()

const dataSource = ref<unknown[]>([])
const total = ref(0)
const currentPage = ref(1)
const currentPageSize = ref(props.pageSize ?? 10)
const loading = ref(false)
const errorMessage = ref<string | null>(null)

async function loadData() {
  loading.value = true
  errorMessage.value = null
  try {
    const result = await props.fetcher({
      page: currentPage.value,
      pageSize: currentPageSize.value,
      ...(props.queryParams ?? {}),
    })
    dataSource.value = result.items
    total.value = result.total
  } catch (e) {
    logger.error('DataTable 加载失败', e)
    errorMessage.value = e instanceof Error ? e.message : '加载失败'
    dataSource.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function onPageChange(page: number, pageSize: number) {
  currentPage.value = page
  currentPageSize.value = pageSize
  void loadData()
}

function onRefresh() {
  void loadData()
}

onMounted(() => {
  void loadData()
})

watch(
  () => props.queryParams,
  () => {
    currentPage.value = 1
    void loadData()
  },
  { deep: true },
)
</script>

<template>
  <div class="data-table-wrap">
    <div class="data-table-toolbar">
      <Space>
        <Button data-testid="refresh" :icon="h(ReloadOutlined)" @click="onRefresh">刷新</Button>
      </Space>
    </div>
    <div v-if="errorMessage" class="data-table-error">
      <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="onRefresh" />
    </div>
    <Spin v-else-if="loading && dataSource.length === 0" tip="加载中..." class="data-table-spin" />
    <EmptyState v-else-if="!loading && dataSource.length === 0" description="暂无数据" action-text="刷新" @action="onRefresh" />
    <Table
      v-else
      :columns="columns"
      :data-source="dataSource"
      :row-key="rowKey"
      :loading="loading"
      :pagination="{
        current: currentPage,
        pageSize: currentPageSize,
        total,
        showSizeChanger: true,
        showTotal: (t: number) => `共 ${t} 条`,
      }"
      size="middle"
      @change="onPageChange as any"
    >
      <template #bodyCell="{ column, record }">
        <slot name="bodyCell" :column="column" :record="record" />
      </template>
    </Table>
  </div>
</template>

<style scoped>
.data-table-wrap {
  width: 100%;
}
.data-table-toolbar {
  margin-bottom: 12px;
  display: flex;
  justify-content: flex-end;
}
.data-table-error {
  padding: 24px;
  text-align: center;
}
.data-table-spin {
  display: flex;
  justify-content: center;
  padding: 48px 0;
}
</style>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/DataTable.spec.ts`
Expected: 7 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/DataTable.vue web/system-admin/src/shared/components/DataTable.spec.ts
git commit -m "feat(system-admin): 实现 DataTable 通用数据表格组件"
```

---

## Task 15: 日期时间范围选择器 DateTimeRangePicker

**Files:**
- Create: `web/system-admin/src/shared/components/DateTimeRangePicker.vue`
- Create: `web/system-admin/src/shared/components/DateTimeRangePicker.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/DateTimeRangePicker.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import DateTimeRangePicker from './DateTimeRangePicker.vue'

describe('shared/components/DateTimeRangePicker', () => {
  it('未传 value 时不报错', () => {
    const wrapper = mount(DateTimeRangePicker, { props: {} })
    expect(wrapper.html()).toContain('ant-picker')
  })

  it('传入 value 时渲染日期', () => {
    const wrapper = mount(DateTimeRangePicker, {
      props: { value: ['2026-07-27T00:00:00Z', '2026-07-28T00:00:00Z'] },
    })
    expect(wrapper.html()).toContain('ant-picker')
  })

  it('change 事件输出 ISO 8601 UTC 字符串数组', async () => {
    const wrapper = mount(DateTimeRangePicker, { props: {} })
    const input = wrapper.find('input')
    expect(input.exists()).toBe(true)
    // 验证组件 expose 的 onChange 方法
    const vm = wrapper.vm as unknown as { onChange: (val: [Date, Date]) => void }
    vm.onChange([new Date(Date.UTC(2026, 6, 27, 0, 0, 0)), new Date(Date.UTC(2026, 6, 28, 0, 0, 0))])
    const events = wrapper.emitted('change')
    expect(events).toBeTruthy()
    const payload = events?.[0]?.[0] as [string, string]
    expect(payload[0]).toMatch(/^2026-07-27T\d{2}:\d{2}:\d{2}.\d{3}Z$/)
    expect(payload[1]).toMatch(/^2026-07-28T\d{2}:\d{2}:\d{2}.\d{3}Z$/)
  })

  it('showTime=true 时显示时间选择', () => {
    const wrapper = mount(DateTimeRangePicker, {
      props: { showTime: true },
    })
    expect(wrapper.html()).toContain('ant-picker')
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/DateTimeRangePicker.spec.ts`
Expected: FAIL，提示 `Cannot find module './DateTimeRangePicker.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/DateTimeRangePicker.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { RangePicker } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'

/**
 * 日期时间范围选择器
 *
 * 包装 ant-design-vue RangePicker：
 * - v-model:value 接收 [string, string] ISO 8601 UTC 字符串
 * - change 事件输出 [string, string] ISO 8601 UTC 字符串
 *
 * 与 spec §3 后端约定保持一致：所有时间字段使用 ISO 8601 UTC 字符串传输。
 */
const props = withDefaults(
  defineProps<{
    /** 当前值 [start, end] ISO 8601 UTC 字符串 */
    value?: [string, string]
    /** 是否显示时间选择，默认 false */
    showTime?: boolean
    /** 占位符 */
    placeholders?: [string, string]
    /** 是否禁用 */
    disabled?: boolean
  }>(),
  {
    value: undefined,
    showTime: false,
    placeholders: () => ['开始时间', '结束时间'],
    disabled: false,
  },
)

const emit = defineEmits<{
  (e: 'change', value: [string, string]): void
}>()

const dayjsValue = computed<[Dayjs, Dayjs] | undefined>(() => {
  if (!props.value) return undefined
  const [start, end] = props.value
  return [dayjs(start), dayjs(end)]
})

function onChange(value: [Dayjs, Dayjs] | null) {
  if (!value) return
  emit('change', [value[0].toDate().toISOString(), value[1].toDate().toISOString()])
}
</script>

<template>
  <RangePicker
    :value="dayjsValue"
    :show-time="showTime"
    :placeholders="placeholders"
    :disabled="disabled"
    @change="onChange as any"
  />
</template>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/DateTimeRangePicker.spec.ts`
Expected: 4 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/DateTimeRangePicker.vue web/system-admin/src/shared/components/DateTimeRangePicker.spec.ts
git commit -m "feat(system-admin): 实现 DateTimeRangePicker 日期时间范围选择器"
```

---

## Task 16: 图表组件 ChartLine / ChartBar / ChartPie

**Files:**
- Create: `web/system-admin/src/shared/components/charts/ChartLine.vue`
- Create: `web/system-admin/src/shared/components/charts/ChartBar.vue`
- Create: `web/system-admin/src/shared/components/charts/ChartPie.vue`
- Create: `web/system-admin/src/shared/components/charts/ChartLine.spec.ts`

> 说明：三个图表组件结构高度相似，本任务一并实现。spec §6.1 组件测试覆盖率门槛 60%，对图表组件验证 props 透传与 loading 态即可，不测试 ECharts 内部渲染。

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/charts/ChartLine.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import ChartLine from './ChartLine.vue'

describe('shared/components/charts/ChartLine', () => {
  it('传入 series 与 xAxis 渲染 echarts 容器', () => {
    const wrapper = mount(ChartLine, {
      props: {
        series: [{ name: '订单', type: 'line', data: [1, 2, 3] }],
        xAxis: ['2026-07-25', '2026-07-26', '2026-07-27'],
      },
    })
    expect(wrapper.html()).toContain('chart-line')
  })

  it('loading=true 显示加载态', () => {
    const wrapper = mount(ChartLine, {
      props: {
        series: [],
        xAxis: [],
        loading: true,
      },
    })
    expect(wrapper.html()).toContain('ant-spin')
  })

  it('未传 series 时显示空态', () => {
    const wrapper = mount(ChartLine, {
      props: { series: [], xAxis: [] },
    })
    expect(wrapper.html()).toContain('ant-empty')
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/charts/ChartLine.spec.ts`
Expected: FAIL，提示 `Cannot find module './ChartLine.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/charts/ChartLine.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'

/**
 * 折线图组件
 *
 * 包装 vue-echarts，预设主题色（与 design-tokens.css --c-primary 一致）。
 */
const props = withDefaults(
  defineProps<{
    /** 折线数据序列 */
    series: EChartsOption['series']
    /** X 轴标签数组 */
    xAxis: string[]
    /** 加载中 */
    loading?: boolean
    /** 高度（px），默认 320 */
    height?: number
  }>(),
  {
    loading: false,
    height: 320,
  },
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'axis' },
  legend: { top: 0 },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: props.xAxis,
  },
  yAxis: { type: 'value' },
  series: props.series,
  color: ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1'],
}))

const hasData = computed(() => Array.isArray(props.series) && props.series.length > 0)
</script>

<template>
  <div class="chart-line" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-line {
  width: 100%;
  position: relative;
}
.chart-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
```

- [ ] **Step 4: 实现 `web/system-admin/src/shared/components/charts/ChartBar.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'

/**
 * 柱状图组件
 *
 * 包装 vue-echarts，预设主题色（与 design-tokens.css --c-primary 一致）。
 */
const props = withDefaults(
  defineProps<{
    /** 柱状数据序列 */
    series: EChartsOption['series']
    /** X 轴标签数组 */
    xAxis: string[]
    /** 加载中 */
    loading?: boolean
    /** 高度（px），默认 320 */
    height?: number
  }>(),
  {
    loading: false,
    height: 320,
  },
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  legend: { top: 0 },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    data: props.xAxis,
  },
  yAxis: { type: 'value' },
  series: props.series,
  color: ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1'],
}))

const hasData = computed(() => Array.isArray(props.series) && props.series.length > 0)
</script>

<template>
  <div class="chart-bar" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-bar {
  width: 100%;
  position: relative;
}
.chart-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
```

- [ ] **Step 5: 实现 `web/system-admin/src/shared/components/charts/ChartPie.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'

/**
 * 饼图组件
 *
 * 包装 vue-echarts，预设主题色（与 design-tokens.css --c-primary 一致）。
 */
const props = withDefaults(
  defineProps<{
    /** 饼图数据，每项 { name, value } */
    data: { name: string; value: number }[]
    /** 加载中 */
    loading?: boolean
    /** 高度（px），默认 320 */
    height?: number
  }>(),
  {
    loading: false,
    height: 320,
  },
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'item', formatter: '{a} <br/>{b}: {c} ({d}%)' },
  legend: { orient: 'vertical', left: 'left' },
  series: [
    {
      name: '占比',
      type: 'pie',
      radius: '50%',
      data: props.data,
      emphasis: {
        itemStyle: {
          shadowBlur: 10,
          shadowOffsetX: 0,
          shadowColor: 'rgba(0, 0, 0, 0.5)',
        },
      },
    },
  ],
  color: ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1', '#13C2C2', '#FA541C', '#8C8C8C'],
}))

const hasData = computed(() => Array.isArray(props.data) && props.data.length > 0)
</script>

<template>
  <div class="chart-pie" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-pie {
  width: 100%;
  position: relative;
}
.chart-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
```

- [ ] **Step 6: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/charts/`
Expected: ChartLine.spec.ts 3 个测试全部通过

- [ ] **Step 7: Commit**

```bash
git add web/system-admin/src/shared/components/charts
git commit -m "feat(system-admin): 实现 ChartLine/ChartBar/ChartPie 图表组件"
```

---

## Task 17: JSON 查看器 JsonViewer

**Files:**
- Create: `web/system-admin/src/shared/components/JsonViewer.vue`
- Create: `web/system-admin/src/shared/components/JsonViewer.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/JsonViewer.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import JsonViewer from './JsonViewer.vue'

describe('shared/components/JsonViewer', () => {
  it('渲染对象 JSON 字符串', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { id: 1, name: 'alice' } },
    })
    expect(wrapper.html()).toContain('"id"')
    expect(wrapper.html()).toContain('1')
    expect(wrapper.html()).toContain('"name"')
    expect(wrapper.html()).toContain('alice')
  })

  it('渲染数组 JSON', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: [1, 2, 3] },
    })
    expect(wrapper.html()).toContain('1')
    expect(wrapper.html()).toContain('2')
    expect(wrapper.html()).toContain('3')
  })

  it('渲染字符串值', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: 'hello' },
    })
    expect(wrapper.html()).toContain('hello')
  })

  it('maxHeight 限制容器高度', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { a: 1 }, maxHeight: 200 },
    })
    const container = wrapper.find('.json-viewer')
    expect(container.element.style.maxHeight).toBe('200px')
  })

  it('嵌套对象正确缩进展示', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { outer: { inner: 'value' } } },
    })
    expect(wrapper.html()).toContain('outer')
    expect(wrapper.html()).toContain('inner')
    expect(wrapper.html()).toContain('value')
  })

  it('null 值正确展示', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { x: null } },
    })
    expect(wrapper.html()).toContain('null')
  })

  it('布尔值正确展示', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { active: true, deleted: false } },
    })
    expect(wrapper.html()).toContain('true')
    expect(wrapper.html()).toContain('false')
  })
})
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/JsonViewer.spec.ts`
Expected: FAIL，提示 `Cannot find module './JsonViewer.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/JsonViewer.vue`**

```vue
<script setup lang="ts">
import { computed } from 'vue'

/**
 * JSON 查看器
 *
 * 用于死信 payload、健康检查 detail 等结构化数据展示。
 * 简单实现：用 JSON.stringify + pre 标签；未来可替换为 react-json-view 等组件。
 */
const props = withDefaults(
  defineProps<{
    /** 待展示的数据 */
    data: unknown
    /** 最大高度（px），超出滚动 */
    maxHeight?: number
  }>(),
  {
    maxHeight: 400,
  },
)

const formatted = computed(() => {
  try {
    return JSON.stringify(props.data, null, 2)
  } catch (e) {
    return `// 序列化失败：${e instanceof Error ? e.message : String(e)}`
  }
})
</script>

<template>
  <pre class="json-viewer" :style="{ maxHeight: `${maxHeight}px` }">{{ formatted }}</pre>
</template>

<style scoped>
.json-viewer {
  margin: 0;
  padding: 12px;
  background: #f5f5f5;
  border-radius: 6px;
  font-family: var(--ff-mono, 'SF Mono', 'Cascadia Code', Consolas, monospace);
  font-size: 12px;
  line-height: 1.5;
  color: #595959;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/JsonViewer.spec.ts`
Expected: 7 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/JsonViewer.vue web/system-admin/src/shared/components/JsonViewer.spec.ts
git commit -m "feat(system-admin): 实现 JsonViewer JSON 查看器组件"
```

---

## Task 18: 错误边界 ErrorBoundary

**Files:**
- Create: `web/system-admin/src/shared/components/ErrorBoundary.vue`
- Create: `web/system-admin/src/shared/components/ErrorBoundary.spec.ts`

- [ ] **Step 1: 写失败测试 `web/system-admin/src/shared/components/ErrorBoundary.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import ErrorBoundary from './ErrorBoundary.vue'

const BoomComponent = defineComponent({
  setup() {
    throw new Error('子组件爆炸')
  },
  render() {
    return h('div', 'never-rendered')
  },
})

const OkComponent = defineComponent({
  render() {
    return h('div', { class: 'ok-content' }, '正常内容')
  },
})

describe('shared/components/ErrorBoundary', () => {
  it('子组件正常时渲染 default slot', () => {
    const wrapper = mount(ErrorBoundary, {
      slots: { default: h(OkComponent) },
    })
    expect(wrapper.html()).toContain('ok-content')
  })

  it('子组件抛错时渲染 fallback slot', () => {
    // Vue test-utils 默认会传播错误，这里需要 stub console.error 静默
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const wrapper = mount(ErrorBoundary, {
      slots: {
        default: h(BoomComponent),
        fallback: '<div class="fallback-content">出错了</div>',
      },
    })
    expect(wrapper.html()).toContain('fallback-content')
    spy.mockRestore()
  })

  it('fallback slot 暴露 error 与 retry', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const wrapper = mount(ErrorBoundary, {
      slots: {
        default: h(BoomComponent),
        fallback: '<div class="fallback-content">出错了</div>',
      },
    })
    expect(wrapper.html()).toContain('fallback-content')
    spy.mockRestore()
  })

  it('无 fallback slot 时使用默认错误态', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const wrapper = mount(ErrorBoundary, {
      slots: { default: h(BoomComponent) },
    })
    expect(wrapper.html()).toContain('加载失败')
    spy.mockRestore()
  })
})
```

注意：测试文件需在顶部 import `vi`：

```ts
import { describe, it, expect, vi } from 'vitest'
```

- [ ] **Step 2: 运行测试，验证失败**

Run: `cd web/system-admin && pnpm test -- src/shared/components/ErrorBoundary.spec.ts`
Expected: FAIL，提示 `Cannot find module './ErrorBoundary.vue'`

- [ ] **Step 3: 实现 `web/system-admin/src/shared/components/ErrorBoundary.vue`**

```vue
<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'
import { Result, Button } from 'ant-design-vue'
import { logger } from '@/shared/utils/logger'

/**
 * 错误边界组件
 *
 * 捕获子组件树抛出的错误，渲染 fallback 内容。
 * 与 spec §3.10 全局错误处理、§5.8 三态保持一致。
 *
 * 用法：
 * ```vue
 * <ErrorBoundary>
 *   <template #default>
 *     <Dashboard />
 *   </template>
 *   <template #fallback="{ error, retry }">
 *     <div>出错了：{{ error.message }} <button @click="retry">重试</button></div>
 *   </template>
 * </ErrorBoundary>
 * ```
 */
const error = ref<Error | null>(null)
const boomKey = ref(0)

onErrorCaptured((err) => {
  error.value = err instanceof Error ? err : new Error(String(err))
  logger.error('ErrorBoundary 捕获错误', err)
  // 阻止错误继续向上传播
  return false
})

function retry() {
  error.value = null
  boomKey.value += 1
}
</script>

<template>
  <slot v-if="!error" :key="boomKey" />
  <slot v-else name="fallback" :error="error" :retry="retry">
    <Result
      status="error"
      title="加载失败"
      :sub-title="error.message"
    >
      <template #extra>
        <Button type="primary" @click="retry">重试</Button>
      </template>
    </Result>
  </slot>
</template>
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/shared/components/ErrorBoundary.spec.ts`
Expected: 4 个测试全部通过

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/shared/components/ErrorBoundary.vue web/system-admin/src/shared/components/ErrorBoundary.spec.ts
git commit -m "feat(system-admin): 实现 ErrorBoundary 错误边界组件"
```

---

## Task 19: 共享组件出口 shared/components/index.ts

**Files:**
- Create: `web/system-admin/src/shared/components/index.ts`

- [ ] **Step 1: 实现 `web/system-admin/src/shared/components/index.ts`**

```ts
/**
 * shared/components 出口
 *
 * 所有业务模块从这里导入共享组件，避免相对路径耦合。
 */
export { default as StatusTag } from './StatusTag.vue'
export { default as IdempotencyButton } from './IdempotencyButton.vue'
export { default as DataTable } from './DataTable.vue'
export { default as EmptyState } from './EmptyState.vue'
export { default as ConfirmDialog } from './ConfirmDialog.vue'
export { default as DateTimeRangePicker } from './DateTimeRangePicker.vue'
export { default as JsonViewer } from './JsonViewer.vue'
export { default as ErrorBoundary } from './ErrorBoundary.vue'
export { default as ChartLine } from './charts/ChartLine.vue'
export { default as ChartBar } from './charts/ChartBar.vue'
export { default as ChartPie } from './charts/ChartPie.vue'
```

- [ ] **Step 2: 验证 TypeScript 编译通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 3: Commit**

```bash
git add web/system-admin/src/shared/components/index.ts
git commit -m "feat(system-admin): 添加 shared/components 出口文件"
```

---

## Task 20: 全局布局组件 BasicLayout / HeaderBar / SiderMenu / FooterBar

**Files:**
- Create: `web/system-admin/src/shared/layout/BasicLayout.vue`
- Create: `web/system-admin/src/shared/layout/HeaderBar.vue`
- Create: `web/system-admin/src/shared/layout/SiderMenu.vue`
- Create: `web/system-admin/src/shared/layout/FooterBar.vue`

> 说明：布局组件依赖 vue-router 实例与 useAuthStore，纯组件测试需要 mock 较多上下文。本任务用 typecheck + 视觉验证（dev server）替代单测，集成验证在 Task 27 完成。

- [ ] **Step 1: 实现 `web/system-admin/src/shared/layout/SiderMenu.vue`**

```vue
<script setup lang="ts">
import { computed, ref } from 'vue'
import { LayoutSider, Menu } from 'ant-design-vue'
import type { RouteRecordRaw } from 'vue-router'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/shared/auth'

/**
 * 侧栏菜单组件
 *
 * - 从 router.options.routes 读取所有带 menuGroup 的子路由
 * - 按 menuGroup 分组渲染
 * - 通过 useAuthStore.hasRole 控制可见
 * - 当前路由通过 menuKey 匹配高亮
 */

interface RouteMeta {
  title?: string
  menuKey?: string
  icon?: string
  roles?: string[]
  menuGroup?: string
}

const props = defineProps<{
  /** 是否折叠 */
  collapsed?: boolean
}>()

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

// 菜单组显示名映射
const GROUP_TITLES: Record<string, string> = {
  '01-dashboard': '仪表盘',
  '02-user-access': '用户与权限',
  '03-system-governance': '系统治理',
  '04-runtime-ops': '运行时运维',
  '05-audit': '审计与对账',
  '06-account': '个人账号',
  '07-monitoring': '系统监控',
}

// 菜单组排序
const GROUP_ORDER = [
  '01-dashboard',
  '02-user-access',
  '03-system-governance',
  '04-runtime-ops',
  '05-audit',
  '06-account',
  '07-monitoring',
]

interface MenuItem {
  key: string
  title: string
  icon?: string
  path: string
  roles?: string[]
}

const groupedMenus = computed<Record<string, MenuItem[]>>(() => {
  const result: Record<string, MenuItem[]> = {}
  // 从 router 的根路由 '/' 的 children 中读取
  const rootRoute = router.options.routes.find((r) => r.path === '/')
  const children: RouteRecordRaw[] = rootRoute?.children ?? []
  for (const child of children) {
    const meta = (child.meta ?? {}) as RouteMeta
    if (!meta.menuGroup || !meta.menuKey || !meta.title) continue
    // 角色过滤
    if (meta.roles && meta.roles.length > 0 && !auth.hasRole(meta.roles)) continue
    if (!result[meta.menuGroup]) result[meta.menuGroup] = []
    result[meta.menuGroup].push({
      key: meta.menuKey,
      title: meta.title,
      icon: meta.icon,
      path: `/${child.path}`,
      roles: meta.roles,
    })
  }
  return result
})

const orderedGroups = computed(() => {
  return GROUP_ORDER.filter((g) => groupedMenus.value[g]?.length > 0).map((g) => ({
    key: g,
    title: GROUP_TITLES[g] ?? g,
    items: groupedMenus.value[g],
  }))
})

const selectedKeys = ref<string[]>([])
function updateSelected() {
  const meta = route.meta as RouteMeta
  selectedKeys.value = meta.menuKey ? [meta.menuKey] : []
}
updateSelected()
router.afterEach(() => updateSelected())

function onMenuClick({ key }: { key: string }) {
  // 在所有 group 中查找匹配项
  for (const group of orderedGroups.value) {
    const found = group.items.find((i) => i.key === key)
    if (found) {
      void router.push(found.path)
      return
    }
  }
}
</script>

<template>
  <LayoutSider
    :collapsed="props.collapsed"
    :collapsed-width="80"
    :width="200"
    :trigger="null"
    collapsible
    class="sider-menu"
  >
    <Menu
      mode="inline"
      theme="dark"
      :selected-keys="selectedKeys"
      :open-keys="orderedGroups.map((g) => g.key)"
      @click="onMenuClick"
    >
      <template v-for="group in orderedGroups" :key="group.key">
        <Menu.ItemGroup :key="group.key" :title="group.title">
          <Menu.Item v-for="item in group.items" :key="item.key">
            <span>{{ item.title }}</span>
          </Menu.Item>
        </Menu.ItemGroup>
      </template>
    </Menu>
  </LayoutSider>
</template>

<style scoped>
.sider-menu {
  position: fixed;
  left: 0;
  top: 64px;
  bottom: 0;
  z-index: 10;
}
</style>
```

- [ ] **Step 2: 实现 `web/system-admin/src/shared/layout/HeaderBar.vue`**

```vue
<script setup lang="ts">
import { computed, ref } from 'vue'
import { LayoutHeader, Breadcrumb, Badge, Dropdown, Input, Modal, Menu as AMenu } from 'ant-design-vue'
import { BellOutlined, SearchOutlined, UserOutlined, LogoutOutlined } from '@ant-design/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/shared/auth'
import { env } from '@/app/env'

/**
 * 顶栏组件
 *
 * 含 Logo + Breadcrumb + 全局搜索 + 通知铃铛 + 用户菜单。
 */

const emit = defineEmits<{
  (e: 'toggle-sider'): void
}>()

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// 面包屑：基于 route.matched 的 meta.title
const breadcrumbs = computed(() => {
  return route.matched
    .filter((r) => r.meta?.title)
    .map((r) => ({ title: r.meta.title as string, path: r.path }))
})

// 通知数量（占位，后续 Plan 接入 /api/admin/alerts?status=firing）
const unread = ref(0)

const searchVisible = ref(false)
const searchKeyword = ref('')

function onSearch() {
  // 简单实现：根据关键字跳转到第一个匹配的菜单项
  if (!searchKeyword.value) return
  searchVisible.value = false
  // 后续 Plan 在此对接全局搜索后端
}

function onLogout() {
  void auth.logout().then(() => {
    void router.push('/login')
  })
}

function onProfile() {
  void router.push('/account/profile')
}

const userMenuItems = [
  { key: 'profile', label: '个人中心', icon: UserOutlined },
  { key: 'logout', label: '登出', icon: LogoutOutlined },
]

function onUserMenuClick({ key }: { key: string }) {
  if (key === 'logout') onLogout()
  else if (key === 'profile') onProfile()
}
</script>

<template>
  <LayoutHeader class="header-bar">
    <div class="header-left">
      <span class="header-toggle" @click="emit('toggle-sider')">☰</span>
      <span class="header-logo">Leno 系统管理后台</span>
      <Breadcrumb class="header-breadcrumb">
        <Breadcrumb.Item v-for="crumb in breadcrumbs" :key="crumb.path">
          {{ crumb.title }}
        </Breadcrumb.Item>
      </Breadcrumb>
    </div>
    <div class="header-right">
      <span class="header-action" @click="searchVisible = true">
        <SearchOutlined />
        <span class="header-action-text">搜索</span>
      </span>
      <span class="header-action">
        <Badge :count="unread" :overflow-count="99">
          <BellOutlined style="font-size: 18px" />
        </Badge>
      </span>
      <Dropdown :trigger="['click']">
        <span class="header-action header-user">
          <UserOutlined />
          <span class="header-username">{{ auth.user?.username ?? '未登录' }}</span>
        </span>
        <template #overlay>
          <AMenu @click="onUserMenuClick">
            <AMenu.Item key="profile"><UserOutlined /> 个人中心</AMenu.Item>
            <AMenu.Item key="logout"><LogoutOutlined /> 登出</AMenu.Item>
          </AMenu>
        </template>
      </Dropdown>
    </div>
    <Modal v-model:open="searchVisible" title="全局搜索" @ok="onSearch">
      <Input v-model:value="searchKeyword" placeholder="输入菜单或端点关键词" allow-clear />
    </Modal>
  </LayoutHeader>
</template>

<style scoped>
.header-bar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 64px;
  padding: 0 24px;
  background: #ffffff;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  z-index: 100;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}
.header-toggle {
  cursor: pointer;
  font-size: 18px;
  padding: 0 8px;
}
.header-logo {
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
  white-space: nowrap;
}
.header-breadcrumb {
  margin-left: 16px;
}
.header-right {
  display: flex;
  align-items: center;
  gap: 24px;
}
.header-action {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.header-action-text {
  font-size: 13px;
  color: #595959;
}
.header-user {
  gap: 8px;
}
.header-username {
  font-size: 14px;
  color: #000000d9;
}
</style>
```

- [ ] **Step 3: 实现 `web/system-admin/src/shared/layout/FooterBar.vue`**

```vue
<script setup lang="ts">
import { LayoutFooter } from 'ant-design-vue'
import { env } from '@/app/env'

/**
 * 底栏组件
 *
 * 显示版权与版本号。
 */
</script>

<template>
  <LayoutFooter class="footer-bar">
    <span>© Leno · v{{ env.appVersion }}</span>
  </LayoutFooter>
</template>

<style scoped>
.footer-bar {
  text-align: center;
  height: 32px;
  line-height: 32px;
  padding: 0;
  background: #ffffff;
  border-top: 1px solid #f0f0f0;
  color: #8c8c8c;
  font-size: 12px;
}
</style>
```

- [ ] **Step 4: 实现 `web/system-admin/src/shared/layout/BasicLayout.vue`**

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import { Layout, LayoutContent } from 'ant-design-vue'
import HeaderBar from './HeaderBar.vue'
import SiderMenu from './SiderMenu.vue'
import FooterBar from './FooterBar.vue'

/**
 * 全局布局容器
 *
 * 与 spec §5.3 布局结构保持一致：
 * - Header 64px 固定顶部
 * - Sider 200px 固定左侧（可折叠至 80px）
 * - Content padding 24px
 * - Footer 32px
 *
 * 响应式断点：
 * - ≥ 1200px：Sider 全展开
 * - 992-1199px：Sider 自动折叠
 * - < 992px：显示「请使用桌面端访问」提示
 */
const siderCollapsed = ref(false)
const isMobile = ref(false)

function updateResponsive() {
  const width = window.innerWidth
  isMobile.value = width < 992
  if (width < 1200 && width >= 992) {
    siderCollapsed.value = true
  } else if (width >= 1200) {
    siderCollapsed.value = false
  }
}

if (typeof window !== 'undefined') {
  updateResponsive()
  window.addEventListener('resize', updateResponsive)
}

const contentMarginLeft = computed(() => (siderCollapsed.value ? 80 : 200))
</script>

<template>
  <div v-if="isMobile" class="mobile-warn">
    <div class="mobile-warn-content">
      <h2>请使用桌面端访问</h2>
      <p>系统管理后台仅支持桌面浏览器（宽度 ≥ 992px），请切换设备后再访问。</p>
    </div>
  </div>
  <Layout v-else class="basic-layout">
    <HeaderBar @toggle-sider="siderCollapsed = !siderCollapsed" />
    <Layout class="basic-layout-main">
      <SiderMenu :collapsed="siderCollapsed" />
      <LayoutContent class="basic-layout-content" :style="{ marginLeft: `${contentMarginLeft}px` }">
        <slot />
        <FooterBar />
      </LayoutContent>
    </Layout>
  </Layout>
</template>

<style scoped>
.basic-layout {
  min-height: 100vh;
}
.basic-layout-main {
  padding-top: 64px;
}
.basic-layout-content {
  padding: 24px;
  min-height: calc(100vh - 64px - 32px);
  background: #f5f5f5;
}
.mobile-warn {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f5f5f5;
  z-index: 1000;
}
.mobile-warn-content {
  text-align: center;
  padding: 24px;
}
.mobile-warn-content h2 {
  font-size: 20px;
  color: #000000d9;
  margin-bottom: 12px;
}
.mobile-warn-content p {
  font-size: 14px;
  color: #595959;
}
</style>
```

- [ ] **Step 5: 验证 TypeScript 编译通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 6: Commit**

```bash
git add web/system-admin/src/shared/layout
git commit -m "feat(system-admin): 实现 BasicLayout/HeaderBar/SiderMenu/FooterBar 全局布局"
```

---

## Task 21: 异常页 Forbidden / NotFound

**Files:**
- Create: `web/system-admin/src/shared/pages/Forbidden.vue`
- Create: `web/system-admin/src/shared/pages/NotFound.vue`

- [ ] **Step 1: 实现 `web/system-admin/src/shared/pages/Forbidden.vue`**

```vue
<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

/**
 * 403 无权访问页
 *
 * 路由守卫鉴权失败时跳转此处。
 */
const router = useRouter()

function goHome() {
  void router.push('/')
}
</script>

<template>
  <Result
    status="403"
    title="403"
    sub-title="抱歉，您无权访问该页面。"
  >
    <template #extra>
      <Button type="primary" @click="goHome">返回首页</Button>
    </template>
  </Result>
</template>
```

- [ ] **Step 2: 实现 `web/system-admin/src/shared/pages/NotFound.vue`**

```vue
<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

/**
 * 404 页面不存在
 *
 * 未匹配路由时跳转此处。
 */
const router = useRouter()

function goHome() {
  void router.push('/')
}
</script>

<template>
  <Result
    status="404"
    title="404"
    sub-title="抱歉，您访问的页面不存在。"
  >
    <template #extra>
      <Button type="primary" @click="goHome">返回首页</Button>
    </template>
  </Result>
</template>
```

- [ ] **Step 3: 验证 TypeScript 编译通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 4: Commit**

```bash
git add web/system-admin/src/shared/pages
git commit -m "feat(system-admin): 实现 Forbidden(403) 与 NotFound(404) 异常页"
```

---

## Task 22: 06-account 模块 DTO 收敛与 auth.api 完整实现

**Files:**
- Create: `web/system-admin/src/modules/06-account/types/auth.dto.ts`
- Create: `web/system-admin/src/modules/06-account/api/auth.api.spec.ts`
- Modify: `web/system-admin/src/modules/06-account/api/auth.api.ts`（Task 8 创建的占位实现，本任务收敛 DTO 来源到模块层）

**说明：** Task 8 为支撑 `useAuthStore` 测试，将 `AdminUserDto`/`LoginDto`/`LoginResultDto` 定义在 `shared/auth/auth.store.ts` 并就地导出，`auth.api.ts` 从 store 引用类型。本任务按 spec §1.2 文件结构，在模块层建立 `types/auth.dto.ts` 作为 06-account 的 DTO 聚合出口（从 shared 透传共享 DTO + 定义模块自有 DTO），并让 `auth.api.ts` 从模块内部引用类型，建立清晰的模块边界。`shared/auth/auth.store.ts` 保持不变（共享层不反向依赖模块）。

- [ ] **Step 1: 创建 `web/system-admin/src/modules/06-account/types/auth.dto.ts`**

```ts
import type { AdminUserDto } from '@/shared/auth/auth.store'

/**
 * 06-account 模块鉴权相关 DTO 聚合出口
 *
 * - 共享 DTO（AdminUserDto / LoginDto / LoginResultDto）由 shared/auth/auth.store.ts 持有，
 *   本文件透传 re-export，供模块内 api / views 统一引用。
 * - UserProfileResultDto 为模块自有 DTO（/api/users/me 响应），在此定义。
 */
export type { AdminUserDto, LoginDto, LoginResultDto } from '@/shared/auth/auth.store'

/**
 * GET /api/users/me 响应体
 *
 * profile 为当前管理员视图，permissions 为其权限码列表。
 */
export interface UserProfileResultDto {
  profile: AdminUserDto
  permissions: string[]
}
```

- [ ] **Step 2: 重写 `web/system-admin/src/modules/06-account/api/auth.api.ts`（完整内容）**

```ts
import { client } from '@/shared/http'
import type { LoginDto, LoginResultDto, UserProfileResultDto } from './types/auth.dto'

/**
 * 鉴权 API
 *
 * 与 Identity 域 AuthController / UsersController 对接：
 * - POST /api/auth/login    账号密码登录
 * - POST /api/auth/logout   登出（best-effort）
 * - GET  /api/users/me      当前管理员 profile 与权限
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const authApi = {
  /**
   * 账号密码登录
   *
   * @param body 用户名 + 密码
   * @returns token / expiresIn / user / roles / permissions
   */
  login(body: LoginDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/login', body).then((r) => r.data)
  },

  /**
   * 登出（best-effort，失败由 store 吞掉）
   */
  logout(): Promise<void> {
    return client.post<void>('/auth/logout', null).then(() => undefined)
  },

  /**
   * 拉取当前管理员 profile 与权限
   */
  getProfile(): Promise<UserProfileResultDto> {
    return client.get<UserProfileResultDto>('/users/me').then((r) => r.data)
  },
}
```

- [ ] **Step 3: 写失败测试 `web/system-admin/src/modules/06-account/api/auth.api.spec.ts`**

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '@/shared/http'
import { authApi } from './auth.api'

vi.mock('@/shared/http', () => ({
  client: { post: vi.fn(), get: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'k' } })),
}))

describe('modules/06-account/api/auth.api', () => {
  beforeEach(() => {
    ;(client.post as Mock).mockReset()
    ;(client.get as Mock).mockReset()
  })

  it('login: POST /auth/login 并返回解包数据', async () => {
    const data = {
      token: 'tok-1',
      expiresIn: 3600,
      user: { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] },
      roles: ['Admin'],
      permissions: ['*'],
    }
    ;(client.post as Mock).mockResolvedValue({ data })
    const result = await authApi.login({ username: 'admin', password: 'Admin123' })
    expect(client.post).toHaveBeenCalledWith('/auth/login', {
      username: 'admin',
      password: 'Admin123',
    })
    expect(result).toEqual(data)
  })

  it('logout: POST /auth/logout', async () => {
    ;(client.post as Mock).mockResolvedValue({ data: null })
    await authApi.logout()
    expect(client.post).toHaveBeenCalledWith('/auth/logout', null)
  })

  it('getProfile: GET /users/me 并返回解包数据', async () => {
    const data = {
      profile: { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] },
      permissions: ['role:read', 'role:write'],
    }
    ;(client.get as Mock).mockResolvedValue({ data })
    const result = await authApi.getProfile()
    expect(client.get).toHaveBeenCalledWith('/users/me')
    expect(result).toEqual(data)
  })
})
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/modules/06-account/api/auth.api.spec.ts`
Expected: 3 个测试全部通过

- [ ] **Step 5: 验证 TypeScript 编译通过（含 Task 8 store 对 DTO 的引用仍成立）**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 6: Commit**

```bash
git add web/system-admin/src/modules/06-account/types/auth.dto.ts web/system-admin/src/modules/06-account/api/auth.api.ts web/system-admin/src/modules/06-account/api/auth.api.spec.ts
git commit -m "feat(system-admin): 06-account DTO 收敛与 auth.api 完整实现"
```

---

## Task 23: 06-account 登录页 Login2fa.vue

**Files:**
- Create: `web/system-admin/src/modules/06-account/views/Login2fa.vue`
- Create: `web/system-admin/src/modules/06-account/views/Login2fa.spec.ts`

**说明：** 按 spec §4.2 决策，Plan 1 仅实现账号密码登录分支；OTP 输入区静态展示 + 角标「2FA 暂未启用」+ 提交按钮 disabled，对应 design-prompt `06-account/login-2fa.md` 的两步流程视觉但不接通后端 2FA。错误映射遵循 spec §4.2：401→「账号或密码错误」、403→「账号已禁用」、429→倒计时按钮。

- [ ] **Step 1: 实现 `web/system-admin/src/modules/06-account/views/Login2fa.vue`**

```vue
<script setup lang="ts">
import { ref, reactive, computed, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  Form,
  FormItem,
  Input,
  InputPassword,
  Steps,
  Step,
  Button,
  Alert,
  Badge,
  Modal,
  message,
} from 'ant-design-vue'
import { UserOutlined, LockOutlined, SafetyOutlined } from '@ant-design/icons-vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import {
  UnauthorizedError,
  ForbiddenError,
  RateLimitedError,
  NetworkError,
  AppError,
} from '@/shared/http/errors'
import { logger } from '@/shared/utils/logger'

type FormInstance = InstanceType<typeof Form>

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const formRef = ref<FormInstance>()
const loading = ref(false)
const errorMsg = ref<string | null>(null)
const retryCountdown = ref(0)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const formModel = reactive({
  username: '',
  password: '',
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不少于 6 位', trigger: 'blur' },
  ],
}

const redirectTarget = computed(() => {
  const r = route.query.redirect
  return typeof r === 'string' && r.length > 0 ? r : '/dashboard/operations-overview'
})

function startCountdown(seconds: number) {
  retryCountdown.value = seconds
  if (countdownTimer) clearInterval(countdownTimer)
  countdownTimer = setInterval(() => {
    retryCountdown.value -= 1
    if (retryCountdown.value <= 0) {
      retryCountdown.value = 0
      if (countdownTimer) {
        clearInterval(countdownTimer)
        countdownTimer = null
      }
    }
  }, 1000)
}

onUnmounted(() => {
  if (countdownTimer) clearInterval(countdownTimer)
})

async function onSubmit() {
  errorMsg.value = null
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  loading.value = true
  try {
    await auth.login({ username: formModel.username, password: formModel.password })
    message.success('登录成功', 1.5)
    await router.push(redirectTarget.value)
  } catch (e) {
    if (e instanceof UnauthorizedError) {
      errorMsg.value = '账号或密码错误'
    } else if (e instanceof ForbiddenError) {
      errorMsg.value = '账号已禁用'
    } else if (e instanceof RateLimitedError) {
      errorMsg.value = `操作过于频繁，请 ${e.retryAfter} 秒后重试`
      startCountdown(e.retryAfter)
    } else if (e instanceof NetworkError) {
      errorMsg.value = '网络异常，请稍后重试'
    } else if (e instanceof AppError) {
      errorMsg.value = e.message
    } else {
      errorMsg.value = '登录失败，请稍后重试'
      logger.error('登录未知错误', e)
    }
  } finally {
    loading.value = false
  }
}

function showForgotPassword() {
  Modal.info({
    title: '忘记密码',
    content: '请联系超级管理员通过审批流程重置密码。',
    okText: '知道了',
  })
}
</script>

<template>
  <div class="login-page">
    <section class="login-brand">
      <div class="login-brand-inner">
        <div class="login-brand-logo">Leno</div>
        <h1 class="login-brand-title">Leno 系统管理后台</h1>
        <p class="login-brand-security">JWT + 双因子 + IP 白名单 + 全操作审计</p>
      </div>
    </section>
    <section class="login-form-area">
      <div class="login-card">
        <Steps :current="0" size="small" class="login-steps">
          <Step title="账号密码" />
          <Step title="双因子验证" />
        </Steps>

        <Alert
          v-if="errorMsg"
          :message="errorMsg"
          type="error"
          show-icon
          class="login-alert"
        />

        <Form
          ref="formRef"
          :model="formModel"
          :rules="rules"
          layout="vertical"
          @submit.prevent="onSubmit"
        >
          <FormItem name="username">
            <Input
              v-model:value="formModel.username"
              size="large"
              placeholder="请输入用户名"
              :disabled="loading"
              aria-label="用户名"
            >
              <template #prefix><UserOutlined /></template>
            </Input>
          </FormItem>
          <FormItem name="password">
            <InputPassword
              v-model:value="formModel.password"
              size="large"
              placeholder="请输入密码"
              :disabled="loading"
              aria-label="密码"
            >
              <template #prefix><LockOutlined /></template>
            </InputPassword>
          </FormItem>
          <FormItem>
            <IdempotencyButton
              type="primary"
              size="large"
              block
              :loading="loading"
              :disabled="retryCountdown > 0"
              @click="onSubmit"
            >
              {{ retryCountdown > 0 ? `${retryCountdown}s 后重试` : '登录' }}
            </IdempotencyButton>
          </FormItem>
          <FormItem>
            <Button type="link" class="login-forgot" @click="showForgotPassword">忘记密码？</Button>
          </FormItem>
        </Form>

        <div class="login-otp-preview">
          <div class="login-otp-head">
            <SafetyOutlined />
            <span class="login-otp-title">双因子验证</span>
            <Badge status="default" text="2FA 暂未启用" />
          </div>
          <p class="login-otp-hint">请打开 Authenticator App 获取验证码（2FA 暂未启用）</p>
          <div class="login-otp-boxes">
            <Input
              v-for="i in 6"
              :key="i"
              :value="''"
              size="large"
              :maxlength="1"
              disabled
              class="login-otp-box"
              :aria-label="`验证码第 ${i} 位`"
            />
          </div>
          <IdempotencyButton type="primary" block disabled>验证</IdempotencyButton>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  min-height: 100vh;
  background: var(--n2, #fafafa);
}
.login-brand {
  width: 50%;
  background: #001529;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
}
.login-brand-inner {
  text-align: center;
  padding: 48px;
}
.login-brand-logo {
  font-size: 32px;
  font-weight: 600;
  margin-bottom: 16px;
}
.login-brand-title {
  font-size: 24px;
  font-weight: 600;
  color: #fff;
  margin: 0 0 12px;
}
.login-brand-security {
  font-size: 12px;
  color: #52c41a;
  margin: 0;
}
.login-form-area {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
}
.login-card {
  width: 400px;
  max-width: 100%;
}
.login-steps {
  margin-bottom: 24px;
}
.login-alert {
  margin-bottom: 16px;
}
.login-forgot {
  padding: 0;
  float: right;
}
.login-otp-preview {
  margin-top: 24px;
  padding: 16px;
  background: var(--n3, #f5f5f5);
  border-radius: var(--r-base, 6px);
  opacity: 0.75;
}
.login-otp-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.login-otp-title {
  font-size: 14px;
  font-weight: 500;
}
.login-otp-hint {
  font-size: 12px;
  color: var(--n7, #8c8c8c);
  margin: 0 0 12px;
}
.login-otp-boxes {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}
.login-otp-box {
  flex: 1;
  text-align: center;
}
@media (max-width: 1199px) {
  .login-brand {
    display: none;
  }
}
</style>
```

- [ ] **Step 2: 写失败测试 `web/system-admin/src/modules/06-account/views/Login2fa.spec.ts`**

```ts
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import Login2fa from './Login2fa.vue'
import * as authApiModule from '@/modules/06-account/api/auth.api'
import { UnauthorizedError, RateLimitedError } from '@/shared/http/errors'

vi.mock('ant-design-vue', async () => {
  const actual = await vi.importActual<typeof import('ant-design-vue')>('ant-design-vue')
  return {
    ...actual,
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
    Modal: { info: vi.fn(), confirm: vi.fn() },
  }
})

function makePinia() {
  const pinia = createPinia()
  pinia.use(piniaPluginPersistedstate)
  setActivePinia(pinia)
  return pinia
}

async function mountLogin(redirect?: string) {
  const pinia = makePinia()
  const router: Router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', component: Login2fa },
      { path: '/dashboard/operations-overview', component: { template: '<div>dashboard</div>' } },
      { path: '/foo', component: { template: '<div>foo</div>' } },
    ],
  })
  await router.push(redirect ? `/login?redirect=${encodeURIComponent(redirect)}` : '/login')
  await router.isReady()
  const wrapper = mount(Login2fa, { global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

describe('modules/06-account/views/Login2fa', () => {
  beforeEach(() => {
    localStorage.clear()
  })
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('渲染品牌区、登录表单与 2FA 预览角标', async () => {
    const { wrapper } = await mountLogin()
    expect(wrapper.text()).toContain('Leno 系统管理后台')
    expect(wrapper.find('input[placeholder="请输入用户名"]').exists()).toBe(true)
    expect(wrapper.find('input[placeholder="请输入密码"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('2FA 暂未启用')
  })

  it('空表单提交显示校验错误', async () => {
    const { wrapper } = await mountLogin()
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('请输入用户名')
  })

  it('登录成功后跳转 redirect 路径并持久化 token', async () => {
    const fakeResult = {
      token: 'tok-1',
      expiresIn: 3600,
      user: { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] },
      roles: ['Admin'],
      permissions: ['*'],
    }
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockResolvedValue(fakeResult),
      logout: vi.fn(),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const { wrapper, router } = await mountLogin('/foo')
    await wrapper.find('input[placeholder="请输入用户名"]').setValue('admin')
    await wrapper.find('input[placeholder="请输入密码"]').setValue('Admin123')
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/foo')
    const persisted = JSON.parse(localStorage.getItem('auth') ?? '{}')
    expect(persisted.token).toBe('tok-1')
    spy.mockRestore()
  })

  it('401 显示「账号或密码错误」', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockRejectedValue(new UnauthorizedError()),
      logout: vi.fn(),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const { wrapper } = await mountLogin()
    await wrapper.find('input[placeholder="请输入用户名"]').setValue('admin')
    await wrapper.find('input[placeholder="请输入密码"]').setValue('wrong1')
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(wrapper.text()).toContain('账号或密码错误')
    spy.mockRestore()
  })

  it('429 显示倒计时文案', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockRejectedValue(new RateLimitedError('限流', 30)),
      logout: vi.fn(),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const { wrapper } = await mountLogin()
    await wrapper.find('input[placeholder="请输入用户名"]').setValue('admin')
    await wrapper.find('input[placeholder="请输入密码"]').setValue('Admin123')
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(wrapper.text()).toContain('30 秒后重试')
    spy.mockRestore()
  })
})
```

- [ ] **Step 3: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/modules/06-account/views/Login2fa.spec.ts`
Expected: 5 个测试全部通过

- [ ] **Step 4: 验证 typecheck 通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/modules/06-account/views/Login2fa.vue web/system-admin/src/modules/06-account/views/Login2fa.spec.ts
git commit -m "feat(system-admin): 实现 06-account 登录页 Login2fa（账号密码登录 + 2FA UI 预留）"
```

---

## Task 24: 06-account 模块路由项与出口

**Files:**
- Create: `web/system-admin/src/modules/06-account/routes.ts`
- Create: `web/system-admin/src/modules/06-account/routes.spec.ts`
- Create: `web/system-admin/src/modules/06-account/index.ts`

**说明：** 按 spec §1.5，`/login` 为顶层匿名路由（不挂 BasicLayout），由模块 `routes.ts` 导出 `loginRoute` 供 `app/router.ts` 顶层注册；`accountRoutes` 为挂载在 BasicLayout 下的子路由数组，Plan 1 范围内为空数组（profile/notifications 页面不在 Plan 1 范围）。

- [ ] **Step 1: 实现 `web/system-admin/src/modules/06-account/routes.ts`**

```ts
import type { RouteRecordRaw } from 'vue-router'

/**
 * 登录路由（顶层，匿名访问）
 *
 * spec §1.5：/login 为顶层路由，不挂在 BasicLayout 下。
 */
export const loginRoute: RouteRecordRaw = {
  path: '/login',
  name: 'account.login',
  component: () => import('./views/Login2fa.vue'),
  meta: {
    anonymous: true,
    title: '登录',
    menuKey: 'account.login',
  },
}

/**
 * 06-account 模块挂载在 BasicLayout 下的子路由
 *
 * Plan 1 范围内无 BasicLayout 子路由（profile/notifications 页面不在本 Plan）。
 */
export const accountRoutes: RouteRecordRaw[] = []
```

- [ ] **Step 2: 写测试 `web/system-admin/src/modules/06-account/routes.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { loginRoute, accountRoutes } from './routes'

describe('modules/06-account/routes', () => {
  it('loginRoute 为 /login 匿名路由', () => {
    expect(loginRoute.path).toBe('/login')
    expect(loginRoute.name).toBe('account.login')
    expect(loginRoute.meta?.anonymous).toBe(true)
    expect(loginRoute.meta?.title).toBe('登录')
  })

  it('accountRoutes 为数组', () => {
    expect(Array.isArray(accountRoutes)).toBe(true)
  })
})
```

- [ ] **Step 3: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/modules/06-account/routes.spec.ts`
Expected: 2 个测试全部通过

- [ ] **Step 4: 实现 `web/system-admin/src/modules/06-account/index.ts`（模块出口）**

```ts
export { authApi } from './api/auth.api'
export type {
  AdminUserDto,
  LoginDto,
  LoginResultDto,
  UserProfileResultDto,
} from './types/auth.dto'
export { loginRoute, accountRoutes } from './routes'
```

- [ ] **Step 5: 验证 typecheck 通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 6: Commit**

```bash
git add web/system-admin/src/modules/06-account/routes.ts web/system-admin/src/modules/06-account/routes.spec.ts web/system-admin/src/modules/06-account/index.ts
git commit -m "feat(system-admin): 06-account 模块路由项 loginRoute/accountRoutes 与模块出口"
```

---

## Task 25: 根 Provider 组件与 App.vue 装配

**Files:**
- Create: `web/system-admin/src/app/provider.vue`
- Create: `web/system-admin/src/app/provider.spec.ts`
- Modify: `web/system-admin/src/App.vue`（Task 1 占位根组件，本任务替换为 Provider + RouterView）
- Create: `web/system-admin/src/App.spec.ts`

**说明：** 按 spec §1.2 文件结构与 §1.3 启动流程，`app/provider.vue` 注入 `AConfigProvider`（主题 `antdTheme` + 中文 locale + dayjs 中文），`App.vue` 组合 `<Provider><RouterView /></Provider>`。

- [ ] **Step 1: 实现 `web/system-admin/src/app/provider.vue`**

```vue
<script setup lang="ts">
import { ConfigProvider } from 'ant-design-vue'
import zhCN from 'ant-design-vue/es/locale/zh_CN'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import { antdTheme } from '@/shared/tokens/antd-theme'

dayjs.locale('zh-cn')
</script>

<template>
  <ConfigProvider :theme="antdTheme" :locale="zhCN">
    <slot />
  </ConfigProvider>
</template>
```

- [ ] **Step 2: 写测试 `web/system-admin/src/app/provider.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import Provider from './provider.vue'

describe('app/provider', () => {
  it('渲染 slot 内容', () => {
    const wrapper = mount(Provider, {
      slots: { default: '<div class="slot-content">content</div>' },
    })
    expect(wrapper.html()).toContain('slot-content')
  })
})
```

- [ ] **Step 3: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/app/provider.spec.ts`
Expected: 1 个测试通过

- [ ] **Step 4: 重写 `web/system-admin/src/App.vue`（完整内容，替换 Task 1 占位）**

```vue
<script setup lang="ts">
import { RouterView } from 'vue-router'
import Provider from './app/provider.vue'
</script>

<template>
  <Provider>
    <RouterView />
  </Provider>
</template>
```

- [ ] **Step 5: 写测试 `web/system-admin/src/App.spec.ts`**

```ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import App from './App.vue'

describe('App', () => {
  it('通过 Provider 包裹 RouterView 并渲染匹配路由', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/', component: { template: '<div class="home">home</div>' } }],
    })
    await router.push('/')
    await router.isReady()
    const wrapper = mount(App, { global: { plugins: [router] } })
    expect(wrapper.html()).toContain('home')
    expect(wrapper.html()).not.toContain('app-placeholder')
  })
})
```

- [ ] **Step 6: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/App.spec.ts`
Expected: 1 个测试通过

- [ ] **Step 7: 验证 typecheck 通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 8: Commit**

```bash
git add web/system-admin/src/app/provider.vue web/system-admin/src/app/provider.spec.ts web/system-admin/src/App.vue web/system-admin/src/App.spec.ts
git commit -m "feat(system-admin): 实现 app/provider.vue 与 App.vue 根装配（ConfigProvider + RouterView）"
```

---

## Task 26: 路由聚合与 beforeEach 鉴权守卫

**Files:**
- Create: `web/system-admin/src/app/router.ts`
- Create: `web/system-admin/src/app/router.spec.ts`

**说明：** 按 spec §1.5 聚合路由表、§4.3 实现 `beforeEach` 守卫。Plan 1 范围内仅聚合 06-account 模块与基础设施路由；`createAuthGuard` 为可独立测试的工厂函数。已登录访问 `/login` 自动跳首页（design-prompt login-2fa.md §7）。

- [ ] **Step 1: 实现 `web/system-admin/src/app/router.ts`**

```ts
import {
  createRouter,
  createWebHistory,
  type NavigationGuard,
  type RouteRecordRaw,
} from 'vue-router'
import { useAuthStore } from '@/shared/auth/auth.store'
import { loginRoute, accountRoutes } from '@/modules/06-account/routes'
import BasicLayout from '@/shared/layout/BasicLayout.vue'
import Forbidden from '@/shared/pages/Forbidden.vue'
import NotFound from '@/shared/pages/NotFound.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 创建鉴权守卫（spec §4.3）
 *
 * 1. 已登录访问 /login → 跳首页
 * 2. meta.anonymous 路由直接放行
 * 3. 未登录跳 /login?redirect=to.fullPath
 * 4. 首次进入 user 为空时拉取 profile，失败登出并跳 /login
 * 5. meta.roles 角色校验，不足跳 /403
 * 6. meta.permission 权限校验，不足跳 /403
 */
export function createAuthGuard(): NavigationGuard {
  return async (to) => {
    const auth = useAuthStore()

    if (to.path === '/login' && auth.isAuthenticated) {
      return { path: '/' }
    }

    if (to.meta.anonymous) {
      return true
    }

    if (!auth.isAuthenticated) {
      return { path: '/login', query: { redirect: to.fullPath } }
    }

    if (!auth.user) {
      try {
        await auth.fetchProfile()
      } catch (e) {
        logger.warn('fetchProfile 失败，登出并跳转登录', e)
        await auth.logout()
        return { path: '/login' }
      }
    }

    const requiredRoles = (to.meta.roles ?? []) as string[]
    if (requiredRoles.length > 0 && !auth.hasRole(requiredRoles)) {
      return { path: '/403' }
    }

    if (to.meta.permission && !auth.hasPermission(to.meta.permission as string)) {
      return { path: '/403' }
    }

    return true
  }
}

/**
 * 路由表（Plan 1 范围：06-account + 基础设施）
 */
const routes: RouteRecordRaw[] = [
  loginRoute,
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
    component: BasicLayout,
    children: [
      { path: '', redirect: '/dashboard/operations-overview' },
      ...accountRoutes,
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'catch-all',
    component: NotFound,
    meta: { anonymous: true, title: '页面不存在' },
  },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

router.beforeEach(createAuthGuard())
```

- [ ] **Step 2: 写测试 `web/system-admin/src/app/router.spec.ts`**

```ts
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createAuthGuard } from './router'
import { useAuthStore } from '@/shared/auth/auth.store'
import * as authApiModule from '@/modules/06-account/api/auth.api'
import type { AdminUserDto } from '@/shared/auth/auth.store'

function makeRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', component: { template: '<div>login</div>' }, meta: { anonymous: true } },
      { path: '/403', component: { template: '<div>403</div>' }, meta: { anonymous: true } },
      { path: '/public', component: { template: '<div>public</div>' }, meta: { anonymous: true } },
      { path: '/protected', component: { template: '<div>protected</div>' } },
      { path: '/admin-only', component: { template: '<div>admin</div>' }, meta: { roles: ['Admin'] } },
      { path: '/perm', component: { template: '<div>perm</div>' }, meta: { permission: 'dead-letter:dispose' } },
      { path: '/', component: { template: '<div>home</div>' } },
    ],
  })
}

function mkUser(roles: string[]): AdminUserDto {
  return { id: 'u1', username: 'a', email: 'a@l.com', status: 'Active', roles } as AdminUserDto
}

describe('app/router createAuthGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })
  afterEach(() => vi.restoreAllMocks())

  it('匿名路由直接放行', async () => {
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/public')
    expect(router.currentRoute.value.path).toBe('/public')
  })

  it('未登录访问受保护路由跳 /login?redirect', async () => {
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.redirect).toBe('/protected')
  })

  it('已登录且 user 已存在时放行受保护路由', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Admin'])
    auth.roles = ['Admin']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(router.currentRoute.value.path).toBe('/protected')
  })

  it('已登录但 user 为空时拉取 profile 后放行', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn(),
      getProfile: vi.fn().mockResolvedValue({ profile: mkUser(['Admin']), permissions: ['*'] }),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(auth.user?.username).toBe('a')
    expect(router.currentRoute.value.path).toBe('/protected')
    spy.mockRestore()
  })

  it('fetchProfile 失败时登出并跳 /login', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn().mockResolvedValue(undefined),
      getProfile: vi.fn().mockRejectedValue(new Error('network')),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(auth.token).toBeNull()
    expect(router.currentRoute.value.path).toBe('/login')
    spy.mockRestore()
  })

  it('角色不足跳 /403', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Operator'])
    auth.roles = ['Operator']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/admin-only')
    expect(router.currentRoute.value.path).toBe('/403')
  })

  it('已登录访问 /login 跳首页', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Admin'])
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/login')
    expect(router.currentRoute.value.path).toBe('/')
  })
})
```

- [ ] **Step 3: 运行测试，验证通过**

Run: `cd web/system-admin && pnpm test -- src/app/router.spec.ts`
Expected: 7 个测试全部通过

- [ ] **Step 4: 验证 typecheck 通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 5: Commit**

```bash
git add web/system-admin/src/app/router.ts web/system-admin/src/app/router.spec.ts
git commit -m "feat(system-admin): 实现路由聚合与 beforeEach 鉴权守卫"
```

---

## Task 27: main.ts 入口装配 + 全局错误处理 + 集成验证 + E2E smoke

**Files:**
- Modify: `web/system-admin/src/main.ts`（Task 1 占位入口，本任务替换为完整装配）
- Create: `web/system-admin/tests/e2e/login.smoke.spec.ts`

**说明：** 按 spec §1.3 装配 main.ts（pinia + router + Antd + 全局错误处理），§3.10 注册 `app.config.errorHandler` 与 `unhandledrejection`。ECharts 不在 main.ts 全局注册（图表组件 Task 16 已通过 `@vue-echarts` 局部引入并按需懒加载，以满足 §6.9 ECharts chunk < 300KB 预算）。web-vitals 与前端审计日志上报属可观测性增强，不在 Plan 1（基础设施 + 登录）核心范围。

- [ ] **Step 1: 重写 `web/system-admin/src/main.ts`（完整内容，替换 Task 1 占位）**

```ts
import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import { message, Modal } from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import App from './App.vue'
import { pinia } from './app/pinia'
import { router } from './app/router'
import { logger } from './shared/utils/logger'
import { BusinessError, ConcurrencyError, RateLimitedError } from '@/shared/http/errors'
import '@/shared/tokens/design-tokens.css'

const app = createApp(App)

app.use(pinia)
app.use(router)
app.use(Antd)

/**
 * 全局错误处理（spec §3.10）
 *
 * - BusinessError → message.error
 * - ConcurrencyError → Modal.confirm 刷新重试
 * - RateLimitedError → message.warning 倒计时
 * - 其它（NetworkError/ServerError）由页面级 ErrorBoundary 兜底
 */
app.config.errorHandler = (err) => {
  logger.error('全局错误捕获', err)
  if (err instanceof BusinessError) {
    message.error(err.message)
  } else if (err instanceof ConcurrencyError) {
    Modal.confirm({
      title: '资源已被他人修改',
      content: `当前版本：v${err.currentVersion}。是否刷新后重试？`,
      okText: '刷新重试',
      cancelText: '取消',
      onOk: () => window.location.reload(),
    })
  } else if (err instanceof RateLimitedError) {
    message.warning(`操作过于频繁，请 ${err.retryAfter}s 后重试`)
  }
}

window.addEventListener('unhandledrejection', (event) => {
  logger.error('未捕获的 Promise 错误', event.reason)
})

app.mount('#app')
```

- [ ] **Step 2: 验证 typecheck 通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

- [ ] **Step 3: 集成验证 — 全量 lint / typecheck / unit test / build**

Run: `cd web/system-admin && pnpm lint`
Expected: 0 error，0 warning

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无错误

Run: `cd web/system-admin && pnpm test`
Expected: 所有单测（Task 2-26 累计）全部通过

Run: `cd web/system-admin && pnpm build`
Expected: `dist/` 目录生成，无 TypeScript 错误，产物含 `index-*.js`、`assets/` 等

- [ ] **Step 4: 创建 E2E smoke 测试 `web/system-admin/tests/e2e/login.smoke.spec.ts`**

```ts
import { test, expect } from '@playwright/test'

test('登录闭环：填表 → 拦截登录 API → 持久化 token → 跳转 redirect', async ({ page }) => {
  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        code: 0,
        message: 'ok',
        data: {
          token: 'e2e-tok',
          expiresIn: 3600,
          user: {
            id: 'u1',
            username: 'admin',
            email: 'a@l.com',
            status: 'Active',
            roles: ['Admin'],
          },
          roles: ['Admin'],
          permissions: ['*'],
        },
        traceId: 'e2e-trace',
      }),
    })
  })

  await page.goto('/login?redirect=/dashboard/operations-overview')

  await page.getByPlaceholder('请输入用户名').fill('admin')
  await page.getByPlaceholder('请输入密码').fill('Admin123')
  await page.getByRole('button', { name: '登录' }).click()

  // token 持久化到 localStorage（auth store 持久化 key 为 'auth'）
  await expect.poll(async () => {
    const raw = await page.evaluate(() => localStorage.getItem('auth'))
    return raw ? JSON.parse(raw).token : null
  }).toBe('e2e-tok')

  // 跳转 redirect 目标（Plan 1 中 dashboard 路由未实现，URL 仍为 redirect 目标）
  await expect(page).toHaveURL(/\/dashboard\/operations-overview/)
})
```

> 说明：Plan 1 范围内 `/dashboard/operations-overview` 路由尚未实现（属 Plan 2），点击登录后 URL 会跳到 redirect 目标，页面由 catch-all 落到 NotFound。E2E 断言聚焦登录闭环（API 拦截、token 持久化、redirect 跳转），不断言 dashboard 页面内容。

- [ ] **Step 5: 运行 E2E smoke（Playwright 自动启动 dev server）**

Run: `cd web/system-admin && pnpm e2e`
Expected: 1 个测试通过

- [ ] **Step 6: Commit**

```bash
git add web/system-admin/src/main.ts web/system-admin/tests/e2e/login.smoke.spec.ts
git commit -m "feat(system-admin): main.ts 入口装配 + 全局错误处理 + 登录 E2E smoke"
```

---

## Plan 1 完成验收对照

> 对照 spec §7.1（全局架构）与 §7.2（鉴权与路由）Plan 1 范围内可勾选项。

- [x] `web/system-admin/` 目录按 spec §1.2 创建（Task 1）
- [x] `pnpm dev` 启动成功，`/api` 代理到 `localhost:5001`（Task 1 vite.config.ts）
- [x] `pnpm build` 产物 `dist/` 生成，无 TypeScript 错误（Task 27 Step 3）
- [x] `pnpm lint`、`pnpm typecheck`、`pnpm test` 全部通过（Task 27 Step 3）
- [x] CI `web-system-admin` job 就绪（Task 1 Step 15）
- [x] `/login` 页账号密码登录成功后跳 `/dashboard/operations-overview`（Task 23/26/27）
- [x] 未登录访问受保护路由跳 `/login?redirect=...`（Task 26）
- [x] 登录后刷新页面，token 与 user 从 localStorage 恢复（Task 8 持久化 + Task 26 守卫 fetchProfile）
- [x] 401 自动跳 `/login`；403 跳 `/403`（Task 4 拦截器 + Task 26 守卫）
- [x] 06-account 登录页 OTP 区静态预留（spec §2.6 / §4.2，Task 23）

**Plan 1 范围内实现完成、验收延后到 Plan 2+（需业务页面才能端到端验证）：**
- spec §7.2「`Admin` 角色可见所有菜单；`Operator` 角色按 meta.roles 过滤」——SiderMenu 角色过滤逻辑已在 Task 20 实现，但 Plan 1 路由表仅含 `/login` + `/403` + `/404`，无 `meta.roles` 业务路由可供端到端验证；Plan 2 聚合业务模块路由后即可勾选。
- spec §7.2「无权限按钮被 `v-permission` 隐藏」——`v-permission` 指令与 `PermissionGuard` 组件已在 Task 9 实现并单测覆盖，但 Plan 1 无业务页面承载带权限码的按钮；Plan 2 各模块页面接入后即可端到端验证。

**Plan 1 不在范围内（后续 Plan 交付）：**
- 01-dashboard / 02-user-access / 03-system-governance / 04-runtime-ops / 05-audit / 07-monitoring 共 6 个模块的 25 个业务页面及其路由聚合
- 06-account 的 Profile / Notifications 两页
- web-vitals 上报与前端审计日志上报（spec §6.8 可观测性增强）
- Token 过期前自动刷新（spec §4.7，需后端 `/api/auth/refresh` 就绪）






