# 系统管理后台 - 07-monitoring 模块实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 `web/system-admin/src/modules/07-monitoring/` 模块的全部 1 页（Prometheus 监控看板）+ 模块骨架（types/api/routes/index）+ 1 个 API 单元测试，通过 `<iframe>` 嵌入 Grafana/Prometheus 看板 URL，URL 来自 SystemConfigs 配置项（key: `monitoring.prometheus.dashboard-url`），并提供跨域降级（iframe 加载失败时显示错误兜底 + 「在新窗口打开」按钮）。

**Architecture:** 按 DTO → API（含 TDD 测试）→ routes/index → Vue 视图顺序推进，每 Task 自包含、可独立编译/测试/提交。本模块为只读看板（无写操作、无乐观锁、无幂等键），核心数据为「Prometheus 看板 URL 配置项」，通过 `GET /api/admin/system-configs/by-key/{key}` 获取明文 URL（与 03-system-governance 的 SystemConfigsController `by-key` 端点对齐）。URL 在 sessionStorage 缓存 5 分钟（spec §3.7）。iframe 跨域风险按 spec §9 缓解方案处理：始终提供「在新窗口打开」降级按钮 + 加载超时检测。跨 Plan 类型契约严格遵守 §shared/types、§shared/http、§shared/auth、§shared/components 已定义。

**Tech Stack:** Vue 3.5 `<script setup>` + TypeScript strict + Vite 6 + Ant Design Vue 4.x + Pinia 2 + Vue Router 4 + axios 1.7 + Vitest 2.x + @vue/test-utils 2 + jsdom

**Spec 来源:** [docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md](file:///workspace/docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md) §2.7

**关联 Design Prompt:** `docs/design-prompts/system-admin/07-monitoring/prometheus-dashboard.md`

---

## 跨 Plan 类型契约（本 plan 严格遵守，来自 Plan 1 已实现的 shared 层）

```typescript
// shared/types/index.ts
export interface ApiResponse<T> { code: number; message: string; data: T | null; traceId?: string }
export interface PageResult<T> { items: T[]; total: number; page: number; pageSize: number }
export interface PageQuery { page?: number; pageSize?: number }

// shared/http/client.ts
import type { AxiosInstance } from 'axios'
export const client: AxiosInstance  // baseURL '/api', timeout 15000
export function withIdempotency(): { headers: { 'Idempotency-Key': string } }

// shared/http/errors.ts
export class BusinessError extends Error { kind = 'BusinessError'; code: number; traceId?: string }

// shared/auth/auth.store.ts
export const useAuthStore = defineStore('auth', {
  getters: { isAuthenticated, isAdmin, hasPermission(perm) },
  actions: { login, fetchProfile, logout, hasRole(roles) },
})

// shared/utils/format.ts
export function formatDateTime(iso: string | null): string  // YYYY-MM-DD HH:mm
```

**共享组件（Plan 1 已实现，本 plan 直接 import 使用）：**
`EmptyState` / `ErrorBoundary` —— 路径前缀 `@/shared/components/`。

**命名约定：**
- 视图：PascalCase `.vue`
- API：导出为 `monitoringApi` 对象，方法 camelCase 动词开头
- DTO：PascalCase + `Dto` 后缀
- 路由 name：`monitoring.{view}` kebab-case（`monitoring.prometheus-dashboard`）
- 路由 path：kebab-case（`prometheus-dashboard`）

**跨 Plan 依赖（来自 03-system-governance）：**
- 后端端点 `GET /api/admin/system-configs/by-key/{key}`（SystemConfigsController）— 返回明文配置值，本 plan 复用此端点读取 Prometheus 看板 URL，无需新增后端端点。

---

## 文件结构

### 新建文件（6 个，全部位于 `web/system-admin/src/modules/07-monitoring/`）

**类型层（1）**
- `types/monitoring.dto.ts` — Prometheus 看板 URL 配置项 DTO（`PrometheusDashboardConfigDto`）+ 配置键常量（`MONITORING_CONFIG_KEYS`）+ sessionStorage 缓存 key 与 TTL 常量

**API 层（1 + 1 测试）**
- `api/monitoring.api.ts` — `monitoringApi`（getPrometheusUrl，从 SystemConfigsController `by-key` 端点读取明文 URL）
- `api/monitoring.api.spec.ts` — `monitoringApi` 单元测试（URL 路径、params、返回数据结构断言）

**视图层（1）**
- `views/PrometheusDashboard.vue` — Prometheus 监控看板（iframe 嵌入 + 加载态 + 加载失败兜底 + 「在新窗口打开」降级 + sessionStorage 5 分钟缓存 + 刷新按钮）

**聚合层（2）**
- `routes.ts` — 1 条路由项，挂到 BasicLayout 子路由
- `index.ts` — 聚合导出 routes + monitoringApi 对象

### 依赖项（本 plan 假定 Plan 1 已就绪）
- `web/system-admin/src/shared/http/client.ts`（client）
- `web/system-admin/src/shared/http/errors.ts`（BusinessError）
- `web/system-admin/src/shared/types/index.ts`（ApiResponse）
- `web/system-admin/src/shared/auth/auth.store.ts`（useAuthStore）
- `web/system-admin/src/shared/components/EmptyState.vue` / `ErrorBoundary.vue`
- `web/system-admin/src/app/router.ts`（聚合入口，Plan 1 已注册 `...monitoring` 子路由数组占位）

---

## Task 1: 创建 monitoring.dto.ts 类型与常量

**Files:**
- Create: `web/system-admin/src/modules/07-monitoring/types/monitoring.dto.ts`

- [ ] **Step 1: 创建 monitoring.dto.ts**

```typescript
// web/system-admin/src/modules/07-monitoring/types/monitoring.dto.ts
// 07-monitoring 模块 DTO 与常量定义
// Prometheus 看板 URL 通过 SystemConfigsController 的 by-key 端点读取明文值
// （与 03-system-governance 的 SystemConfigRevealDto 字段对齐，本模块专用 DTO 保持独立性）

/**
 * Prometheus 看板 URL 配置项明文响应 DTO
 * 对应后端 GET /api/admin/system-configs/by-key/{key} 返回结构
 */
export interface PrometheusDashboardConfigDto {
  /** 配置项 ID */
  configId: string
  /** 配置键，固定为 monitoring.prometheus.dashboard-url */
  key: string
  /** 明文看板 URL，如 http://grafana.leno.internal/d/system-overview */
  value: string
}

/**
 * 监控相关 SystemConfigs 配置键集中管理
 * 与后端 SystemConfigsController 存储的 key 字符串完全一致
 */
export const MONITORING_CONFIG_KEYS = {
  /** Prometheus / Grafana 看板嵌入 URL 配置键 */
  PROMETHEUS_DASHBOARD_URL: 'monitoring.prometheus.dashboard-url',
} as const

/**
 * sessionStorage 缓存 key（spec §3.7：Prometheus iframe URL 缓存 5 分钟）
 * 缓存结构：{ url: string, cachedAt: number(ms timestamp) }
 */
export const PROMETHEUS_URL_CACHE_KEY = 'monitoring.prometheus.dashboard-url.cache'

/**
 * sessionStorage 缓存 TTL：5 分钟（spec §3.7）
 */
export const PROMETHEUS_URL_CACHE_TTL_MS = 5 * 60 * 1000

/**
 * iframe 加载超时阈值：10 秒未触发 load 事件视为加载失败
 * 用于跨域场景下 @error 事件可能不触发的兜底检测
 */
export const IFRAME_LOAD_TIMEOUT_MS = 10 * 1000
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（monitoring.dto.ts 仅类型与常量声明，无外部未定义引用）

- [ ] **Step 3: 提交**

```bash
git add web/system-admin/src/modules/07-monitoring/types/monitoring.dto.ts
git commit -m "feat(system-admin/07-monitoring): 新增 monitoring.dto 类型与配置键常量定义"
```

---

## Task 2: monitoring.api.ts + 单元测试（TDD：先写测试 → 验证失败 → 实现 → 验证通过 → 提交）

**Files:**
- Create: `web/system-admin/src/modules/07-monitoring/api/monitoring.api.spec.ts`
- Create: `web/system-admin/src/modules/07-monitoring/api/monitoring.api.ts`

- [ ] **Step 1: 编写失败测试 monitoring.api.spec.ts**

```typescript
// web/system-admin/src/modules/07-monitoring/api/monitoring.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { monitoringApi } from './monitoring.api'
import {
  MONITORING_CONFIG_KEYS,
  type PrometheusDashboardConfigDto,
} from '../types/monitoring.dto'

// 统一 mock shared/http 模块，client.get/post/put/delete 替换为 spy
vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
    withIdempotency: actual.withIdempotency,
  }
})

describe('monitoringApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('getPrometheusUrl 使用 /admin/system-configs/by-key/{key} 路径', async () => {
    const mockData: PrometheusDashboardConfigDto = {
      configId: 'cfg-prom-001',
      key: MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL,
      value: 'http://grafana.leno.internal/d/system-overview',
    }
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockData })

    await monitoringApi.getPrometheusUrl()

    expect(client.get).toHaveBeenCalledWith(
      `/admin/system-configs/by-key/${MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL}`,
    )
  })

  it('getPrometheusUrl 返回明文 URL 数据结构', async () => {
    const mockData: PrometheusDashboardConfigDto = {
      configId: 'cfg-prom-001',
      key: MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL,
      value: 'http://grafana.leno.internal/d/system-overview',
    }
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockData })

    const res = await monitoringApi.getPrometheusUrl()

    expect(res.data).toEqual(mockData)
    expect(res.data?.value).toBe('http://grafana.leno.internal/d/system-overview')
  })

  it('getPrometheusUrl 路径前缀正确且包含完整配置键', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })

    await monitoringApi.getPrometheusUrl()

    const url = (client.get as ReturnType<typeof vi.fn>).mock.calls[0][0] as string
    expect(url.startsWith('/admin/system-configs/by-key/')).toBe(true)
    expect(url).toContain(MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL)
    expect(url).toBe('/admin/system-configs/by-key/monitoring.prometheus.dashboard-url')
  })

  it('getPrometheusUrl 仅使用 GET 方法，不触发 POST/PUT/DELETE', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })

    await monitoringApi.getPrometheusUrl()

    expect(client.get).toHaveBeenCalledTimes(1)
    expect(client.post).not.toHaveBeenCalled()
    expect(client.put).not.toHaveBeenCalled()
    expect(client.delete).not.toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: 运行测试验证失败**

Run: `cd web/system-admin && pnpm test src/modules/07-monitoring/api/monitoring.api.spec.ts`
Expected: FAIL — `Cannot find module './monitoring.api'`（实现文件尚未创建）

- [ ] **Step 3: 实现 monitoring.api.ts**

```typescript
// web/system-admin/src/modules/07-monitoring/api/monitoring.api.ts
// 07-monitoring 模块 API：从 SystemConfigsController 读取 Prometheus 看板 URL 配置项
// 端点对齐 03-system-governance 的 SystemConfigsController by-key 路径
// 本模块为只读看板，无写操作，故无需 Idempotency-Key

import { client } from '@/shared/http'
import type { PrometheusDashboardConfigDto } from '../types/monitoring.dto'
import { MONITORING_CONFIG_KEYS } from '../types/monitoring.dto'

export const monitoringApi = {
  /**
   * 获取 Prometheus / Grafana 看板 URL（明文）
   * 调用 GET /api/admin/system-configs/by-key/{key}，key 为 monitoring.prometheus.dashboard-url
   * 返回的 value 字段为完整看板 URL，供前端 iframe 嵌入或「在新窗口打开」使用
   */
  getPrometheusUrl: () =>
    client.get<PrometheusDashboardConfigDto>(
      `/admin/system-configs/by-key/${MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL}`,
    ),
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `cd web/system-admin && pnpm test src/modules/07-monitoring/api/monitoring.api.spec.ts`
Expected: PASS — 4 tests passed

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/07-monitoring/api/monitoring.api.ts web/system-admin/src/modules/07-monitoring/api/monitoring.api.spec.ts
git commit -m "feat(system-admin/07-monitoring): 实现 monitoringApi.getPrometheusUrl 与 4 个单元测试（URL 路径与返回结构断言）"
```

---

## Task 3: routes.ts + index.ts 模块聚合

**Files:**
- Create: `web/system-admin/src/modules/07-monitoring/routes.ts`
- Create: `web/system-admin/src/modules/07-monitoring/index.ts`

- [ ] **Step 1: 实现 routes.ts**

```typescript
// web/system-admin/src/modules/07-monitoring/routes.ts
// 07-monitoring 模块路由项：1 个视图，meta 含 title/menuKey/icon/roles/menuGroup
// 鉴权：Admin 与 Operator 角色均可访问（只读看板）
import type { RouteRecordRaw } from 'vue-router'

export const monitoringRoutes: RouteRecordRaw[] = [
  {
    path: 'prometheus-dashboard',
    name: 'monitoring.prometheus-dashboard',
    component: () => import('../views/PrometheusDashboard.vue'),
    meta: {
      title: 'Prometheus 监控看板',
      menuKey: 'monitoring.prometheus-dashboard',
      icon: 'MonitorOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '07-monitoring',
    },
  },
]

export default monitoringRoutes
```

- [ ] **Step 2: 实现 index.ts**

```typescript
// web/system-admin/src/modules/07-monitoring/index.ts
// 模块对外出口：routes + monitoringApi 对象
// 供 app/router.ts 聚合 import 与菜单渲染使用
export { default as monitoringRoutes } from './routes'
export { monitoringApi } from './api/monitoring.api'
```

- [ ] **Step 3: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（routes.ts 引用 `../views/PrometheusDashboard.vue` 尚不存在，但 vue-tsc 对动态 `import()` 容忍；若报错需先创建空 `views/PrometheusDashboard.vue` 骨架再回填，Task 4 会实现完整内容）

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/07-monitoring/routes.ts web/system-admin/src/modules/07-monitoring/index.ts
git commit -m "feat(system-admin/07-monitoring): 新增 routes.ts（1 路由项）与 index.ts 模块出口"
```

---

## Task 4: PrometheusDashboard.vue 视图（iframe 嵌入 + 跨域降级 + 加载超时兜底）

**Files:**
- Create: `web/system-admin/src/modules/07-monitoring/views/PrometheusDashboard.vue`

**实现要点（design-prompt §1-8 + spec §9 风险缓解）:**
- 顶部工具栏：标题「Prometheus 监控看板」+「刷新」按钮（清缓存重新加载）+「在新窗口打开」按钮（始终可用，跨域降级方案）
- 进入页面 → 先读 sessionStorage 缓存（5 分钟 TTL，spec §3.7）→ 缓存未命中则调用 `monitoringApi.getPrometheusUrl()` 获取明文 URL
- 拿到 URL → 渲染 `<iframe :src="url">` 全屏嵌入
- 加载三态：
  - 加载中：`<a-spin>` 占位（先加载 URL 阶段，再 iframe 渲染阶段）
  - 加载成功：iframe 全屏展示
  - 加载失败：`<a-result status="error">` 兜底 + 「在新窗口打开」+「重试」按钮
- iframe 加载失败检测（双保险，覆盖跨域场景）：
  1. `@error` 事件触发 → 标记失败
  2. 加载超时检测（10s 未触发 `@load` → 标记失败）— 跨域场景下 `@error` 可能不触发，超时检测兜底
- URL 加载失败（API 错误）：`<a-result status="warning">` + 重试按钮 + 错误信息
- URL 为空（未配置）：`<a-empty>` + 提示「未配置 Prometheus 看板 URL」+ 引导文案
- 跨域降级：始终在工具栏与错误兜底中提供「在新窗口打开」按钮（spec §9 缓解方案）

- [ ] **Step 1: 实现 PrometheusDashboard.vue**

```vue
<!-- web/system-admin/src/modules/07-monitoring/views/PrometheusDashboard.vue -->
<!-- Prometheus 监控看板：iframe 嵌入 Grafana/Prometheus URL，URL 来自 SystemConfigs 配置项 -->
<!-- 跨域降级：始终提供「在新窗口打开」按钮；iframe 加载失败时显示错误兜底 -->
<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  ReloadOutlined,
  LinkOutlined,
  MonitorOutlined,
} from '@ant-design/icons-vue'
import { monitoringApi } from '../api/monitoring.api'
import {
  PROMETHEUS_URL_CACHE_KEY,
  PROMETHEUS_URL_CACHE_TTL_MS,
  IFRAME_LOAD_TIMEOUT_MS,
} from '../types/monitoring.dto'
import { BusinessError } from '@/shared/http/errors'

/** sessionStorage 缓存结构 */
interface CachedUrl {
  url: string
  cachedAt: number
}

/** URL 加载阶段状态 */
const urlLoading = ref(false)
/** iframe 渲染阶段加载状态（拿到 URL 后等待 iframe load 事件） */
const iframeLoading = ref(false)
/** 当前看板 URL（明文，来自后端配置） */
const dashboardUrl = ref('')
/** URL 加载失败错误信息（null 表示无错误） */
const urlLoadError = ref<string | null>(null)
/** iframe 加载失败标志（跨域或不可达） */
const iframeError = ref(false)
/** iframe 加载超时定时器句柄 */
let iframeLoadTimer: number | null = null

const hasUrl = computed(() => !!dashboardUrl.value)

/**
 * 从 sessionStorage 读取缓存的 URL（5 分钟内有效）
 * @returns 缓存的 URL；缓存不存在、过期或解析失败时返回 null
 */
function readCachedUrl(): string | null {
  try {
    const raw = sessionStorage.getItem(PROMETHEUS_URL_CACHE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as CachedUrl
    if (!parsed.url || typeof parsed.cachedAt !== 'number') {
      sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
      return null
    }
    if (Date.now() - parsed.cachedAt > PROMETHEUS_URL_CACHE_TTL_MS) {
      sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
      return null
    }
    return parsed.url
  } catch {
    // JSON 解析失败或 sessionStorage 不可用，清理后回退到远程获取
    try {
      sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
    } catch {
      // sessionStorage 完全不可用时忽略，不影响主流程
    }
    return null
  }
}

/**
 * 将 URL 写入 sessionStorage 缓存
 * @param url 明文看板 URL
 */
function writeCachedUrl(url: string): void {
  try {
    const payload: CachedUrl = { url, cachedAt: Date.now() }
    sessionStorage.setItem(PROMETHEUS_URL_CACHE_KEY, JSON.stringify(payload))
  } catch {
    // sessionStorage 写入失败（隐私模式或空间不足）不阻塞功能，下次仍走远程获取
  }
}

/**
 * 清除 sessionStorage 缓存
 */
function clearCachedUrl(): void {
  try {
    sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
  } catch {
    // 忽略 sessionStorage 不可用错误
  }
}

/**
 * 启动 iframe 加载超时定时器
 * 跨域场景下 iframe 的 @error 事件可能不触发，超时检测作为兜底
 */
function startIframeLoadTimer(): void {
  clearIframeLoadTimer()
  iframeLoadTimer = window.setTimeout(() => {
    if (iframeLoading.value) {
      iframeLoading.value = false
      iframeError.value = true
    }
  }, IFRAME_LOAD_TIMEOUT_MS)
}

/**
 * 清除 iframe 加载超时定时器
 */
function clearIframeLoadTimer(): void {
  if (iframeLoadTimer !== null) {
    clearTimeout(iframeLoadTimer)
    iframeLoadTimer = null
  }
}

/**
 * 获取看板 URL（优先读缓存，缓存未命中或强制刷新时调用后端）
 * @param forceRefresh 是否强制刷新（清除缓存并重新请求）
 * @returns 明文 URL；获取失败时返回 null 并设置 urlLoadError
 */
async function fetchDashboardUrl(forceRefresh: boolean): Promise<string | null> {
  if (!forceRefresh) {
    const cached = readCachedUrl()
    if (cached) return cached
  } else {
    clearCachedUrl()
  }

  urlLoading.value = true
  urlLoadError.value = null
  try {
    const res = await monitoringApi.getPrometheusUrl()
    const url = res.data?.value?.trim()
    if (!url) {
      urlLoadError.value =
        '未配置 Prometheus 看板 URL，请在「系统治理 → 系统配置」中设置键 monitoring.prometheus.dashboard-url'
      return null
    }
    writeCachedUrl(url)
    return url
  } catch (e) {
    if (e instanceof BusinessError) {
      urlLoadError.value = `加载 Prometheus 看板地址失败：${e.message}`
    } else {
      urlLoadError.value = '加载 Prometheus 看板地址失败，请检查网络连接或后端服务状态'
    }
    return null
  } finally {
    urlLoading.value = false
  }
}

/**
 * 初始化看板：获取 URL → 渲染 iframe → 启动加载超时检测
 */
async function initDashboard(): Promise<void> {
  const url = await fetchDashboardUrl(false)
  if (url) {
    dashboardUrl.value = url
    iframeLoading.value = true
    iframeError.value = false
    startIframeLoadTimer()
  }
}

/**
 * 刷新看板：清缓存 + 重新加载
 */
async function onRefresh(): Promise<void> {
  clearIframeLoadTimer()
  dashboardUrl.value = ''
  iframeError.value = false
  urlLoadError.value = null
  await initDashboard()
  if (dashboardUrl.value) {
    message.success('已重新加载 Prometheus 看板')
  }
}

/**
 * iframe load 事件回调：加载成功，清除超时定时器
 */
function onIframeLoad(): void {
  clearIframeLoadTimer()
  iframeLoading.value = false
  iframeError.value = false
}

/**
 * iframe error 事件回调：加载失败（跨域或不可达）
 * 跨域场景下此事件可能不触发，由超时定时器兜底
 */
function onIframeError(): void {
  clearIframeLoadTimer()
  iframeLoading.value = false
  iframeError.value = true
}

/**
 * 在新窗口打开看板 URL（跨域降级方案，spec §9）
 */
function openInNewWindow(): void {
  if (dashboardUrl.value) {
    window.open(dashboardUrl.value, '_blank', 'noopener,noreferrer')
  }
}

onMounted(() => {
  initDashboard()
})

onBeforeUnmount(() => {
  clearIframeLoadTimer()
})
</script>

<template>
  <div class="monitoring-dashboard">
    <!-- 顶部工具栏：标题 + 刷新 + 在新窗口打开（始终可用，跨域降级方案） -->
    <div class="monitoring-toolbar">
      <div class="monitoring-title">
        <MonitorOutlined class="monitoring-title-icon" />
        <span>Prometheus 监控看板</span>
      </div>
      <a-space>
        <a-button :loading="urlLoading" @click="onRefresh">
          <template #icon><ReloadOutlined /></template>
          刷新
        </a-button>
        <a-button
          type="primary"
          :disabled="!hasUrl"
          @click="openInNewWindow"
        >
          <template #icon><LinkOutlined /></template>
          在新窗口打开
        </a-button>
      </a-space>
    </div>

    <!-- 阶段 1：URL 加载中（首次或刷新时获取看板地址） -->
    <div v-if="urlLoading" class="monitoring-loading">
      <a-spin tip="正在加载 Prometheus 看板地址...">
        <div class="monitoring-loading-placeholder" />
      </a-spin>
    </div>

    <!-- 阶段 2：URL 加载失败（API 错误或未配置） -->
    <div v-else-if="urlLoadError" class="monitoring-error">
      <a-result status="warning" title="无法加载 Prometheus 看板" :sub-title="urlLoadError">
        <template #extra>
          <a-button type="primary" :loading="urlLoading" @click="onRefresh">重试</a-button>
        </template>
      </a-result>
    </div>

    <!-- 阶段 3：URL 加载成功，渲染 iframe -->
    <div v-else-if="hasUrl" class="monitoring-frame-wrapper">
      <!-- iframe 加载中遮罩（拿到 URL 后等待 iframe load 事件） -->
      <div v-if="iframeLoading" class="monitoring-frame-loading">
        <a-spin tip="看板加载中...">
          <div class="monitoring-loading-placeholder" />
        </a-spin>
      </div>

      <!-- iframe 嵌入（跨域可能无法访问内容，但 src 仍会加载页面） -->
      <iframe
        v-if="!iframeError"
        :src="dashboardUrl"
        class="monitoring-frame"
        frameborder="0"
        allowfullscreen
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-presentation"
        @load="onIframeLoad"
        @error="onIframeError"
      />

      <!-- iframe 加载失败兜底（跨域被阻止或 URL 不可达，spec §9 缓解方案） -->
      <div v-if="iframeError" class="monitoring-frame-error">
        <a-result
          status="error"
          title="看板嵌入失败"
          sub-title="可能是跨域策略阻止了 iframe 嵌入，或看板地址不可达。可尝试在新窗口中打开。"
        >
          <template #extra>
            <a-button type="primary" @click="openInNewWindow">
              <template #icon><LinkOutlined /></template>
              在新窗口打开
            </a-button>
            <a-button @click="onRefresh">重试</a-button>
          </template>
        </a-result>
      </div>
    </div>

    <!-- 兜底：无 URL 且无错误（理论上不应到达，防御性渲染） -->
    <div v-else class="monitoring-empty">
      <a-empty description="暂无 Prometheus 看板配置">
        <a-button type="primary" @click="onRefresh">重新加载</a-button>
      </a-empty>
    </div>
  </div>
</template>

<style scoped>
.monitoring-dashboard {
  display: flex;
  flex-direction: column;
  /* 减去 header(64px) + footer(32px) + content padding(24px*2) */
  height: calc(100vh - 64px - 32px - 48px);
  background: var(--n2, #FAFAFA);
}

.monitoring-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: var(--n1, #FFFFFF);
  border-bottom: 1px solid var(--n5, #D9D9D9);
  flex-shrink: 0;
}

.monitoring-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 600;
  color: var(--n10, #000000D9);
}

.monitoring-title-icon {
  font-size: 20px;
  color: var(--c-primary, #1677FF);
}

.monitoring-loading,
.monitoring-error,
.monitoring-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  padding: 24px;
}

.monitoring-loading-placeholder {
  width: 480px;
  height: 320px;
}

.monitoring-frame-wrapper {
  position: relative;
  flex: 1;
  margin: 0 16px 16px;
  background: var(--n1, #FFFFFF);
  border-radius: var(--r-card, 8px);
  overflow: hidden;
  box-shadow: var(--sh-card, 0 1px 2px 0 rgba(0, 0, 0, 0.03));
}

.monitoring-frame-loading,
.monitoring-frame-error {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--n2, #FAFAFA);
  z-index: 1;
}

.monitoring-frame {
  width: 100%;
  height: 100%;
  border: none;
  display: block;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（PrometheusDashboard.vue 引用的 monitoringApi、DTO 常量、BusinessError 均已在 Task 1-3 创建；shared 层 EmptyState/ErrorBoundary 由 Plan 1 提供）

- [ ] **Step 3: Lint 检查**

Run: `cd web/system-admin && pnpm lint`
Expected: 0 error，warn ≤ 阈值

- [ ] **Step 4: 运行全部测试确认无回归**

Run: `cd web/system-admin && pnpm test`
Expected: PASS — monitoring 模块 4 个测试 + 其他模块测试全部通过

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/07-monitoring/views/PrometheusDashboard.vue
git commit -m "feat(system-admin/07-monitoring): 实现 PrometheusDashboard.vue 视图（iframe 嵌入 + 跨域降级 + 加载超时兜底 + sessionStorage 5 分钟缓存）"
```

---

## 自检清单

### 1. Spec 覆盖（spec §2.7 + §9）
- [x] 1 页 PrometheusDashboard.vue → Task 4
- [x] 模块骨架 types/monitoring.dto.ts → Task 1
- [x] 模块骨架 api/monitoring.api.ts → Task 2
- [x] 模块骨架 routes.ts → Task 3
- [x] 模块骨架 index.ts → Task 3
- [x] 测试 monitoring.api.spec.ts → Task 2
- [x] iframe URL 来自 SystemConfigs 配置项（key: monitoring.prometheus.dashboard-url）→ Task 2 API + Task 4 视图
- [x] 跨域降级方案（「打开新窗口」链接）→ Task 4（工具栏 + 错误兜底均提供「在新窗口打开」按钮）
- [x] iframe 加载失败显示错误兜底 + 「在新窗口打开」按钮 → Task 4（双保险：@error 事件 + 10s 超时检测）
- [x] sessionStorage 缓存 5 分钟（spec §3.7）→ Task 4（readCachedUrl/writeCachedUrl）

### 2. 占位符扫描
- [x] 无 TODO / TBD / FIXME / 省略号 / 「此处省略」/ 「保持不变」
- [x] 所有函数均有完整实现，无空函数体
- [x] 无 `throw new NotImplementedError()`
- [x] 无仅日志无逻辑的函数

### 3. 类型一致性
- [x] `PrometheusDashboardConfigDto`（Task 1 定义）→ Task 2 API 返回类型 + Task 4 视图使用，字段名 `configId`/`key`/`value` 一致
- [x] `MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL`（Task 1 定义）→ Task 2 API 路径 + 测试断言使用，字符串值 `'monitoring.prometheus.dashboard-url'` 一致
- [x] `PROMETHEUS_URL_CACHE_KEY` / `PROMETHEUS_URL_CACHE_TTL_MS` / `IFRAME_LOAD_TIMEOUT_MS`（Task 1 定义）→ Task 4 视图引用，命名一致
- [x] `monitoringApi`（Task 2 定义）→ Task 3 index.ts 导出 + Task 4 视图 import，对象名一致
- [x] `monitoringRoutes`（Task 3 定义）→ Task 3 index.ts 导出，对象名一致
- [x] 路由 name `monitoring.prometheus-dashboard` / path `prometheus-dashboard`（Task 3）与 spec §2.7 表格一致

### 4. 文件路径一致性
- [x] Task 1: `web/system-admin/src/modules/07-monitoring/types/monitoring.dto.ts`
- [x] Task 2: `web/system-admin/src/modules/07-monitoring/api/monitoring.api.ts` + `.spec.ts`
- [x] Task 3: `web/system-admin/src/modules/07-monitoring/routes.ts` + `index.ts`
- [x] Task 4: `web/system-admin/src/modules/07-monitoring/views/PrometheusDashboard.vue`
- [x] 所有路径与任务说明「Plan 7 范围」完全一致

### 5. Design-prompt 字段覆盖
- [x] §1 页面定位（系统监控模块、Admin 角色）→ Task 3 routes.ts `roles: ['Admin', 'Operator']`
- [x] §2 布局（顶部工具栏 + iframe 主区域）→ Task 4 视图布局
- [x] §3 API（SystemConfigs 配置项读取）→ Task 2 monitoringApi.getPrometheusUrl
- [x] §4 交互（进入页面加载 + 刷新 + 在新窗口打开）→ Task 4 onMounted/onRefresh/openInNewWindow
- [x] §5 组件（`<a-button>`/`<a-spin>`/`<a-result>`/`<a-empty>`/`<a-space>`）→ Task 4 视图模板
- [x] §6 视觉规范（主色 #1677FF、卡片圆角 8px、间距 16px）→ Task 4 CSS 变量引用
- [x] §7 异常处理（加载态/空数据/错误态/跨域降级）→ Task 4 三态 + iframe 失败兜底
- [x] §8 验收（iframe 嵌入正常、跨域降级、加载失败兜底）→ Task 4 完整覆盖

**注：** design-prompt §3 提到的 `/api/admin/monitoring/metrics/*` 系列端点为「待实现」状态，本 plan 按 spec §2.7 与任务说明要求，采用 `<iframe>` 嵌入 Grafana/Prometheus URL 的方案（URL 来自 SystemConfigs 配置项），不实现原生指标查询。若未来后端就绪可补原生看板，但不影响本 plan 交付。

### 6. iframe 跨域降级方案覆盖（spec §9 风险表）
- [x] iframe URL 来自 SystemConfigs 配置项（动态获取，非硬编码）→ Task 2 API
- [x] 若跨域不可解，改为「打开新窗口」链接 → Task 4 工具栏始终提供「在新窗口打开」按钮 + 错误兜底提供同样按钮
- [x] iframe 加载失败时显示错误兜底 + 「在新窗口打开」按钮 → Task 4 `iframeError` 分支
- [x] 加载超时检测（10s）覆盖跨域场景下 `@error` 事件不触发的情况 → Task 4 `startIframeLoadTimer`/`clearIframeLoadTimer`

---

## 执行完毕后

1. 所有 4 个 Task 完成后，运行全量验证：
   ```bash
   cd web/system-admin && pnpm typecheck && pnpm lint && pnpm test && pnpm build
   ```
   Expected: 全部通过，dist 产物生成

2. 在 `app/router.ts`（Plan 1 已注册 `...monitoring` 占位）确认本模块路由已自动聚合，无需额外修改

3. 端到端验证：登录后访问 `/monitoring/prometheus-dashboard`，确认 iframe 嵌入正常或跨域降级按钮可用
