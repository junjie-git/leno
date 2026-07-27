# 系统管理后台 04-runtime-ops 模块实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 `web/system-admin/src/modules/04-runtime-ops/` 模块的全部 6 页 + 模块骨架（types/api/routes/index）+ 2 个 API 单元测试，覆盖限流规则、索引重建、死信队列、定时任务、健康监控、告警管理。

**Architecture:** 按 DTO → API（含 TDD 测试）→ routes/index → Vue 视图顺序推进，每 Task 自包含、可独立编译/测试/提交。所有写操作走 `IdempotencyButton` + `Idempotency-Key` 头；危险操作（重投/丢弃/重建触发/停用）走 `ConfirmDialog`（丢弃类需填理由 `requireInput`）。跨 Plan 类型契约严格遵守 §shared/types、§shared/http、§shared/auth、§shared/components 已定义。

**Tech Stack:** Vue 3.5 + `<script setup>` + TS strict + Vite 6 + Ant Design Vue 4 + Pinia 2 + Vue Router 4 + axios 1.7 + Vitest 2 + @vue/test-utils 2 + jsdom

**Spec 来源:** [docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md](file:///workspace/docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md)

**跨 Plan 契约（本 plan 严格遵守）:**
- `shared/types/index.ts`: `ApiResponse<T>` / `PageResult<T>` / `PageQuery`
- `shared/http/client.ts`: `client`（baseURL `/api`, timeout 15000）+ `withIdempotency()`
- `shared/http/errors.ts`: `ConcurrencyError`（含 `currentVersion`）/ `RateLimitedError`（含 `retryAfter`）
- `shared/auth/auth.store.ts`: `useAuthStore`（getters: `isAuthenticated`/`isAdmin`/`hasPermission`；actions: `login`/`fetchProfile`/`logout`/`hasRole`）
- `shared/components/*`（12 个，Plan 1 已实现）: StatusTag / IdempotencyButton / PermissionGuard / DataTable / EmptyState / ConfirmDialog / DateTimeRangePicker / ChartLine / ChartBar / ChartPie / JsonViewer / ErrorBoundary
- 命名约定: 视图 PascalCase `.vue`；API 对象 camelCase 动词开头，导出形如 `deadLetterApi`/`rateLimitRuleApi`；DTO PascalCase + `Dto` 后缀；路由 name `runtime-ops.{view}` kebab-case；路由 path kebab-case

---

## File Structure

**模块骨架（types/api/routes/index）:**
- `web/system-admin/src/modules/04-runtime-ops/types/rate-limit-rule.dto.ts` — 限流规则 DTO + 枚举
- `web/system-admin/src/modules/04-runtime-ops/types/index-rebuild.dto.ts` — 索引重建任务 DTO + 枚举
- `web/system-admin/src/modules/04-runtime-ops/types/dead-letter.dto.ts` — 死信消息 + 批量结果 + 丢弃 DTO
- `web/system-admin/src/modules/04-runtime-ops/types/scheduled-task.dto.ts` — 定时任务 DTO + 枚举
- `web/system-admin/src/modules/04-runtime-ops/types/health.dto.ts` — 健康聚合 + 模块健康 + 依赖项 DTO
- `web/system-admin/src/modules/04-runtime-ops/types/alert.dto.ts` — 告警 + 静默规则 DTO + 枚举
- `web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.ts` — `rateLimitRuleApi`（list/get/create/update/enable/disable）
- `web/system-admin/src/modules/04-runtime-ops/api/index-rebuilds.api.ts` — `indexRebuildApi`（list/get/trigger/retry）
- `web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.ts` — `deadLetterApi`（list/get/retry/discard/batchRetry/batchDiscard）
- `web/system-admin/src/modules/04-runtime-ops/api/scheduled-tasks.api.ts` — `scheduledTaskApi`（list/get/create/update/enable/disable/runNow/getHistory）
- `web/system-admin/src/modules/04-runtime-ops/api/health.api.ts` — `healthApi`（getAggregated/getModules）
- `web/system-admin/src/modules/04-runtime-ops/api/alerts.api.ts` — `alertApi`（list/get/acknowledge）+ `alertSilenceApi`（list/create/delete）
- `web/system-admin/src/modules/04-runtime-ops/routes.ts` — 6 个路由项（path/name/meta.title/menuKey/icon/roles/permission/menuGroup）
- `web/system-admin/src/modules/04-runtime-ops/index.ts` — 导出 routes + 各 api 对象

**6 个视图:**
- `web/system-admin/src/modules/04-runtime-ops/views/RateLimitRules.vue`
- `web/system-admin/src/modules/04-runtime-ops/views/IndexRebuild.vue`
- `web/system-admin/src/modules/04-runtime-ops/views/DeadLetterQueue.vue`
- `web/system-admin/src/modules/04-runtime-ops/views/ScheduledTasks.vue`
- `web/system-admin/src/modules/04-runtime-ops/views/HealthMonitoring.vue`
- `web/system-admin/src/modules/04-runtime-ops/views/AlertManagement.vue`

**测试:**
- `web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.spec.ts` — URL/方法/参数/幂等键头断言
- `web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts` — URL/方法/参数/乐观锁版本头断言

**依赖确认（spec §10）:**
- 所有 `/api/admin/{rate-limit-rules,index-rebuild/tasks,dead-letters,scheduled-tasks,health,alerts,alerts/silences}` 端点已实装（spec §2.4 表格已标 ✅ 已实现）
- 仅 `alert-management` 标 🚧 规划中：AlertManagement.vue 顶部展示 `<a-alert type="info">` 提示，但 API 层与视图骨架仍按 design-prompt §3 完整实现，便于后端就绪即用

---

## Task 1: 创建 6 个 DTO 类型定义文件

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/types/rate-limit-rule.dto.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/types/index-rebuild.dto.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/types/dead-letter.dto.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/types/scheduled-task.dto.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/types/health.dto.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/types/alert.dto.ts`

- [ ] **Step 1: 创建 rate-limit-rule.dto.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/types/rate-limit-rule.dto.ts
// 限流规则 DTO 与枚举，对齐 SystemAdmin BC RateLimitRulesController 契约

/** 限流算法 */
export type RateLimitAlgorithm = 'SlidingWindow' | 'TokenBucket' | 'FixedWindow'

/** 限流维度 */
export type RateLimitScope = 'IP' | 'User' | 'Global' | 'Shop'

/** 限流规则响应 DTO（spec §3.8 含 Version 字段用于乐观锁） */
export interface RateLimitRuleDto {
  ruleId: string
  targetApi: string
  targetContext: string
  limit: number
  windowSeconds: number
  algorithm: RateLimitAlgorithm
  scope: RateLimitScope
  enabled: boolean
  updatedBy: string
  updatedAt: string
  version: number
}

/** 创建/更新限流规则请求 DTO */
export interface SaveRateLimitRuleDto {
  targetApi: string
  targetContext: string
  limit: number
  windowSeconds: number
  algorithm: RateLimitAlgorithm
  scope: RateLimitScope
  /** 编辑时携带，用于乐观锁；新建时省略 */
  version?: number
}

/** 列表查询参数 */
export interface ListRateLimitRulesParams {
  targetApi?: string
  enabled?: boolean
  targetContext?: string[]
  page?: number
  pageSize?: number
}
```

- [ ] **Step 2: 创建 index-rebuild.dto.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/types/index-rebuild.dto.ts
// 索引重建任务 DTO 与枚举，对齐 SystemAdmin BC IndexRebuildController 契约

/** 任务状态：待执行 / 执行中 / 成功 / 失败 */
export type IndexRebuildStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed'

/** 索引重建任务响应 DTO */
export interface IndexRebuildTaskDto {
  taskId: string
  targetContext: string
  indexName: string
  status: IndexRebuildStatus
  triggeredBy: string
  triggeredAt: string
  startedAt: string | null
  finishedAt: string | null
  totalDocs: number
  processedDocs: number
  errorMessage: string | null
  retryCount: number
  esTaskId: string | null
}

/** 触发重建请求 DTO */
export interface TriggerIndexRebuildDto {
  targetContext: string
  indexName: string
}

/** 列表查询参数 */
export interface ListIndexRebuildsParams {
  targetContext?: string[]
  status?: IndexRebuildStatus[]
  page?: number
  pageSize?: number
}
```

- [ ] **Step 3: 创建 dead-letter.dto.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/types/dead-letter.dto.ts
// 死信消息 + 批量结果 + 丢弃 DTO，对齐 SystemAdmin BC DeadLetterController 契约

/** 死信状态：待处理 / 已重投 / 已丢弃 */
export type DeadLetterStatus = 'Pending' | 'Retried' | 'Discarded'

/** 死信消息响应 DTO（spec §3.6 + design-prompt §3） */
export interface DeadLetterMessageDto {
  messageId: string
  originalMessageId: string
  sourceContext: string
  originalTopic: string
  originalQueue: string
  deadLetterQueue: string
  payload: string
  headers: Record<string, unknown>
  errorReason: string
  failedAt: string
  retryCount: number
  status: DeadLetterStatus
  operatorId: string | null
  operatedAt: string | null
  discardReason: string | null
  /** 处置历史，按时间倒序 */
  history: DeadLetterHistoryItemDto[]
}

/** 处置历史条目 */
export interface DeadLetterHistoryItemDto {
  action: 'Retry' | 'Discard' | 'EnterDeadLetter'
  operator: string | null
  operatedAt: string
  result: string
}

/** 丢弃请求 DTO（reason 必填） */
export interface DiscardDeadLetterDto {
  discardReason: string
}

/** 批量操作结果 DTO */
export interface BatchOperationResultDto {
  succeeded: string[]
  failed: { messageId: string; reason: string }[]
}

/** 列表查询参数 */
export interface ListDeadLettersParams {
  sourceContext?: string[]
  status?: DeadLetterStatus[]
  startTime?: string
  endTime?: string
  page?: number
  pageSize?: number
}
```

- [ ] **Step 4: 创建 scheduled-task.dto.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/types/scheduled-task.dto.ts
// 定时任务 DTO 与枚举，对齐 SystemAdmin BC ScheduledTasksController 契约

/** 任务状态：启用 / 停用 */
export type ScheduledTaskStatus = 'Enabled' | 'Disabled'

/** 定时任务响应 DTO */
export interface ScheduledTaskDto {
  taskId: string
  name: string
  jobType: string
  cronExpression: string
  parameters: Record<string, unknown>
  status: ScheduledTaskStatus
  lastRunAt: string | null
  nextRunAt: string | null
  createdAt: string
}

/** 创建定时任务请求 DTO */
export interface SaveScheduledTaskDto {
  name: string
  jobType: string
  cronExpression: string
  parameters: Record<string, unknown>
}

/** 更新定时任务请求 DTO（jobType 不可变） */
export interface UpdateScheduledTaskDto {
  name: string
  cronExpression: string
  parameters: Record<string, unknown>
}

/** 执行历史条目 */
export interface ScheduledTaskExecutionDto {
  executionId: string
  taskId: string
  startedAt: string
  finishedAt: string | null
  status: 'Running' | 'Succeeded' | 'Failed'
  errorMessage: string | null
}

/** 列表查询参数 */
export interface ListScheduledTasksParams {
  name?: string
  status?: ScheduledTaskStatus[]
  jobType?: string
  page?: number
  pageSize?: number
}
```

- [ ] **Step 5: 创建 health.dto.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/types/health.dto.ts
// 健康聚合 + 模块健康 + 依赖项 DTO，对齐 SystemAdmin BC HealthController 契约

/** 整体健康状态：健康 / 降级 / 不健康 */
export type OverallStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

/** 单依赖项状态 */
export type DependencyStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

/** 依赖项 DTO */
export interface DependencyHealthDto {
  name: string
  status: DependencyStatus
  latencyMs: number
  error: string | null
  lastCheckedAt: string
}

/** 模块健康 DTO */
export interface ModuleHealthDto {
  moduleName: string
  status: DependencyStatus
  latencyMs: number
  dependencies: DependencyHealthDto[]
}

/** 聚合健康结果 DTO */
export interface HealthAggregationResultDto {
  overallStatus: OverallStatus
  checkedAt: string
  modules: ModuleHealthDto[]
}
```

- [ ] **Step 6: 创建 alert.dto.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/types/alert.dto.ts
// 告警 + 静默规则 DTO 与枚举，对齐 SystemAdmin BC AlertsController + AlertSilencesController 契约

/** 告警级别 */
export type AlertSeverity = 'critical' | 'warning' | 'info'

/** 告警状态 */
export type AlertStatus = 'firing' | 'acknowledged' | 'resolved'

/** 告警事件 DTO */
export interface AlertDto {
  alertId: string
  name: string
  module: string
  severity: AlertSeverity
  status: AlertStatus
  triggeredAt: string
  durationSeconds: number
  labels: Record<string, string>
  annotations: Record<string, string>
  summary: string
  description: string
  relatedMetric: string | null
}

/** 静默规则匹配器 */
export interface SilenceMatcherDto {
  name: string
  value: string
  isRegex: boolean
}

/** 静默规则 DTO */
export interface SilenceDto {
  silenceId: string
  matchers: SilenceMatcherDto[]
  startsAt: string
  endsAt: string
  reason: string
  createdBy: string
}

/** 创建静默规则请求 DTO */
export interface CreateSilenceDto {
  matchers: SilenceMatcherDto[]
  durationMinutes: number
  reason: string
}

/** 确认告警请求 DTO */
export interface AcknowledgeAlertDto {
  comment: string
}

/** 列表查询参数 */
export interface ListAlertsParams {
  module?: string[]
  severity?: AlertSeverity[]
  status?: AlertStatus[]
  startTime?: string
  endTime?: string
  page?: number
  pageSize?: number
}
```

- [ ] **Step 7: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（types 仅声明，无未定义引用）

```bash
git add web/system-admin/src/modules/04-runtime-ops/types/
git commit -m "feat(runtime-ops): 新增 6 个 DTO 类型定义文件"
```

---

## Task 2: dead-letters API（TDD：先写测试 → 实现 → 通过）

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.spec.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.ts`

- [ ] **Step 1: 编写失败测试 dead-letters.api.spec.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { deadLetterApi } from './dead-letters.api'
import type { ListDeadLettersParams } from '../types/dead-letter.dto'

// 统一 mock shared/http 模块，client.get/post 替换为 spy
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

describe('deadLetterApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用正确 URL 与 params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [], total: 0, page: 1, pageSize: 20 } })
    const params: ListDeadLettersParams = { sourceContext: ['Order'], status: ['Pending'], page: 1, pageSize: 20 }
    await deadLetterApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/dead-letters', { params })
  })

  it('get 使用 /admin/dead-letters/{id} 路径', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await deadLetterApi.get('DLQ-1')
    expect(client.get).toHaveBeenCalledWith('/admin/dead-letters/DLQ-1')
  })

  it('retry 注入 Idempotency-Key 头', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await deadLetterApi.retry('DLQ-1')
    const [, , config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('discard 携带 discardReason body + Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await deadLetterApi.discard('DLQ-1', { discardReason: '消息体格式错误' })
    const [url, body, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/dead-letters/DLQ-1/discard')
    expect(body).toEqual({ discardReason: '消息体格式错误' })
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('batchRetry 提交 messageIds + Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { succeeded: [], failed: [] } })
    await deadLetterApi.batchRetry(['DLQ-1', 'DLQ-2'])
    const [url, body, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/dead-letters/batch-retry')
    expect(body).toEqual({ messageIds: ['DLQ-1', 'DLQ-2'] })
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('batchDiscard 提交 messageIds + discardReason + Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { succeeded: [], failed: [] } })
    await deadLetterApi.batchDiscard(['DLQ-1'], '批量清理过期消息')
    const [url, body, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/dead-letters/batch-discard')
    expect(body).toEqual({ messageIds: ['DLQ-1'], discardReason: '批量清理过期消息' })
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })
})
```

- [ ] **Step 2: 运行测试验证失败**

Run: `cd web/system-admin && pnpm test src/modules/04-runtime-ops/api/dead-letters.api.spec.ts`
Expected: FAIL — `Cannot find module './dead-letters.api'`

- [ ] **Step 3: 实现 dead-letters.api.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.ts
// 死信队列 API：对齐 SystemAdmin BC DeadLetterController 端点
// 写操作（retry/discard/batchRetry/batchDiscard）均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  DeadLetterMessageDto,
  DiscardDeadLetterDto,
  BatchOperationResultDto,
  ListDeadLettersParams,
} from '../types/dead-letter.dto'

/** 死信列表查询参数（合并 PageQuery） */
export type ListDeadLettersRequest = ListDeadLettersParams & PageQuery

export const deadLetterApi = {
  /** 分页查询死信消息 */
  list: (params: ListDeadLettersRequest) =>
    client.get<PageResult<DeadLetterMessageDto>>('/admin/dead-letters', { params }),

  /** 获取死信消息详情 */
  get: (id: string) =>
    client.get<DeadLetterMessageDto>(`/admin/dead-letters/${id}`),

  /** 重投指定死信消息（幂等） */
  retry: (id: string) =>
    client.post<DeadLetterMessageDto>(`/admin/dead-letters/${id}/retry`, null, withIdempotency()),

  /** 丢弃指定死信消息（reason 必填，幂等） */
  discard: (id: string, body: DiscardDeadLetterDto) =>
    client.post<DeadLetterMessageDto>(`/admin/dead-letters/${id}/discard`, body, withIdempotency()),

  /** 批量重投死信消息（幂等） */
  batchRetry: (messageIds: string[]) =>
    client.post<BatchOperationResultDto>('/admin/dead-letters/batch-retry', { messageIds }, withIdempotency()),

  /** 批量丢弃死信消息（reason 必填，幂等） */
  batchDiscard: (messageIds: string[], reason: string) =>
    client.post<BatchOperationResultDto>(
      '/admin/dead-letters/batch-discard',
      { messageIds, discardReason: reason },
      withIdempotency(),
    ),
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `cd web/system-admin && pnpm test src/modules/04-runtime-ops/api/dead-letters.api.spec.ts`
Expected: PASS — 6 tests passed

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.ts web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.spec.ts
git commit -m "feat(runtime-ops): 实现 deadLetterApi 与单元测试（含 Idempotency-Key 断言）"
```

---

## Task 3: rate-limit-rules API（TDD：先写测试 → 实现 → 通过）

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.ts`

- [ ] **Step 1: 编写失败测试 rate-limit-rules.api.spec.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { rateLimitRuleApi } from './rate-limit-rules.api'
import type { SaveRateLimitRuleDto } from '../types/rate-limit-rule.dto'

vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
    withIdempotency: actual.withIdempotency,
  }
})

describe('rateLimitRuleApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list 使用 /admin/rate-limit-rules + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [], total: 0, page: 1, pageSize: 20 } })
    await rateLimitRuleApi.list({ targetApi: '/api/orders', enabled: true, page: 1, pageSize: 20 })
    expect(client.get).toHaveBeenCalledWith('/admin/rate-limit-rules', {
      params: { targetApi: '/api/orders', enabled: true, page: 1, pageSize: 20 },
    })
  })

  it('get 使用 /admin/rate-limit-rules/{id}', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await rateLimitRuleApi.get('rule-1')
    expect(client.get).toHaveBeenCalledWith('/admin/rate-limit-rules/rule-1')
  })

  it('create 注入 Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    const body: SaveRateLimitRuleDto = {
      targetApi: '/api/orders', targetContext: 'Order', limit: 100, windowSeconds: 60,
      algorithm: 'SlidingWindow', scope: 'User',
    }
    await rateLimitRuleApi.create(body)
    const [url, payload, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules')
    expect(payload).toEqual(body)
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('update 携带 X-Resource-Version 乐观锁头 + Idempotency-Key', async () => {
    ;(client.put as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    const body: SaveRateLimitRuleDto = {
      targetApi: '/api/orders', targetContext: 'Order', limit: 200, windowSeconds: 60,
      algorithm: 'SlidingWindow', scope: 'User', version: 3,
    }
    await rateLimitRuleApi.update('rule-1', body)
    const [url, payload, config] = (client.put as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules/rule-1')
    expect(payload).toEqual(body)
    expect(config).toMatchObject({
      headers: { 'X-Resource-Version': 3, 'Idempotency-Key': expect.any(String) },
    })
  })

  it('enable 注入 Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await rateLimitRuleApi.enable('rule-1')
    const [url, payload, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules/rule-1/enable')
    expect(payload).toBeNull()
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('disable 注入 Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await rateLimitRuleApi.disable('rule-1')
    const [url, , config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules/rule-1/disable')
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })
})
```

- [ ] **Step 2: 运行测试验证失败**

Run: `cd web/system-admin && pnpm test src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts`
Expected: FAIL — `Cannot find module './rate-limit-rules.api'`

- [ ] **Step 3: 实现 rate-limit-rules.api.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.ts
// 限流规则 API：对齐 SystemAdmin BC RateLimitRulesController 端点
// update 携带 X-Resource-Version 乐观锁头；enable/disable/create/update 均注入 Idempotency-Key

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  RateLimitRuleDto,
  SaveRateLimitRuleDto,
  ListRateLimitRulesParams,
} from '../types/rate-limit-rule.dto'

export type ListRateLimitRulesRequest = ListRateLimitRulesParams & PageQuery

export const rateLimitRuleApi = {
  /** 分页查询限流规则 */
  list: (params: ListRateLimitRulesRequest) =>
    client.get<PageResult<RateLimitRuleDto>>('/admin/rate-limit-rules', { params }),

  /** 获取限流规则详情 */
  get: (id: string) =>
    client.get<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}`),

  /** 创建限流规则（幂等） */
  create: (body: SaveRateLimitRuleDto) =>
    client.post<RateLimitRuleDto>('/admin/rate-limit-rules', body, withIdempotency()),

  /** 更新限流规则（乐观锁 + 幂等） */
  update: (id: string, body: SaveRateLimitRuleDto) =>
    client.put<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}`, body, {
      headers: {
        'X-Resource-Version': body.version ?? 0,
        ...withIdempotency().headers,
      },
    }),

  /** 启用限流规则（幂等） */
  enable: (id: string) =>
    client.post<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}/enable`, null, withIdempotency()),

  /** 停用限流规则（幂等） */
  disable: (id: string) =>
    client.post<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}/disable`, null, withIdempotency()),
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `cd web/system-admin && pnpm test src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts`
Expected: PASS — 6 tests passed

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.ts web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts
git commit -m "feat(runtime-ops): 实现 rateLimitRuleApi 与单元测试（含乐观锁头断言）"
```

---

## Task 4: index-rebuilds API

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/api/index-rebuilds.api.ts`

- [ ] **Step 1: 实现 index-rebuilds.api.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/index-rebuilds.api.ts
// 索引重建 API：对齐 SystemAdmin BC IndexRebuildController 端点
// trigger/retry 均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  IndexRebuildTaskDto,
  TriggerIndexRebuildDto,
  ListIndexRebuildsParams,
} from '../types/index-rebuild.dto'

export type ListIndexRebuildsRequest = ListIndexRebuildsParams & PageQuery

export const indexRebuildApi = {
  /** 分页查询索引重建任务 */
  list: (params: ListIndexRebuildsRequest) =>
    client.get<PageResult<IndexRebuildTaskDto>>('/admin/index-rebuild/tasks', { params }),

  /** 获取任务详情/进度 */
  get: (id: string) =>
    client.get<IndexRebuildTaskDto>(`/admin/index-rebuild/tasks/${id}`),

  /** 触发索引重建（幂等） */
  trigger: (body: TriggerIndexRebuildDto) =>
    client.post<IndexRebuildTaskDto>('/admin/index-rebuild/trigger', body, withIdempotency()),

  /** 重试失败任务（幂等） */
  retry: (id: string) =>
    client.post<IndexRebuildTaskDto>(`/admin/index-rebuild/tasks/${id}/retry`, null, withIdempotency()),
}
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/api/index-rebuilds.api.ts
git commit -m "feat(runtime-ops): 实现 indexRebuildApi（list/get/trigger/retry）"
```

---

## Task 5: scheduled-tasks API

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/api/scheduled-tasks.api.ts`

- [ ] **Step 1: 实现 scheduled-tasks.api.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/scheduled-tasks.api.ts
// 定时任务 API：对齐 SystemAdmin BC ScheduledTasksController 端点
// create/update/enable/disable/runNow 均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  ScheduledTaskDto,
  SaveScheduledTaskDto,
  UpdateScheduledTaskDto,
  ScheduledTaskExecutionDto,
  ListScheduledTasksParams,
} from '../types/scheduled-task.dto'

export type ListScheduledTasksRequest = ListScheduledTasksParams & PageQuery

export const scheduledTaskApi = {
  /** 分页查询定时任务 */
  list: (params: ListScheduledTasksRequest) =>
    client.get<PageResult<ScheduledTaskDto>>('/admin/scheduled-tasks', { params }),

  /** 获取定时任务详情 */
  get: (taskId: string) =>
    client.get<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}`),

  /** 创建定时任务（初始停用态，幂等） */
  create: (body: SaveScheduledTaskDto) =>
    client.post<ScheduledTaskDto>('/admin/scheduled-tasks', body, withIdempotency()),

  /** 更新定时任务（jobType 不可变，幂等） */
  update: (taskId: string, body: UpdateScheduledTaskDto) =>
    client.put<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}`, body, withIdempotency()),

  /** 启用任务并向调度器注册（幂等） */
  enable: (taskId: string) =>
    client.post<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}/enable`, null, withIdempotency()),

  /** 停用任务并从调度器注销（幂等） */
  disable: (taskId: string) =>
    client.post<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}/disable`, null, withIdempotency()),

  /** 立即触发任务执行（幂等） */
  runNow: (taskId: string) =>
    client.post<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}/run-now`, null, withIdempotency()),

  /** 查询执行历史（最近 20 次） */
  getHistory: (taskId: string) =>
    client.get<ScheduledTaskExecutionDto[]>(`/admin/scheduled-tasks/${taskId}/executions`),
}
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/api/scheduled-tasks.api.ts
git commit -m "feat(runtime-ops): 实现 scheduledTaskApi（含 enable/disable/runNow/getHistory）"
```

---

## Task 6: health API

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/api/health.api.ts`

- [ ] **Step 1: 实现 health.api.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/health.api.ts
// 健康监控 API：对齐 SystemAdmin BC HealthController 端点
// 只读接口，无写操作不需要 Idempotency-Key

import { client } from '@/shared/http'
import type { HealthAggregationResultDto, ModuleHealthDto } from '../types/health.dto'

export const healthApi = {
  /** 获取聚合健康状态（整体 + 各模块概要） */
  getAggregated: () =>
    client.get<HealthAggregationResultDto>('/admin/health'),

  /** 获取各模块健康详情列表（含依赖项明细） */
  getModules: () =>
    client.get<ModuleHealthDto[]>('/admin/health/modules'),
}
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/api/health.api.ts
git commit -m "feat(runtime-ops): 实现 healthApi（getAggregated/getModules）"
```

---

## Task 7: alerts API

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/api/alerts.api.ts`

- [ ] **Step 1: 实现 alerts.api.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/api/alerts.api.ts
// 告警管理 API：对齐 SystemAdmin BC AlertsController + AlertSilencesController 端点
// acknowledge/create silence 均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  AlertDto,
  SilenceDto,
  CreateSilenceDto,
  AcknowledgeAlertDto,
  ListAlertsParams,
} from '../types/alert.dto'

export type ListAlertsRequest = ListAlertsParams & PageQuery

/** 告警事件 API */
export const alertApi = {
  /** 分页查询告警事件 */
  list: (params: ListAlertsRequest) =>
    client.get<PageResult<AlertDto>>('/admin/alerts', { params }),

  /** 获取告警详情 */
  get: (id: string) =>
    client.get<AlertDto>(`/admin/alerts/${id}`),

  /** 确认告警（幂等） */
  acknowledge: (id: string, body: AcknowledgeAlertDto) =>
    client.post<AlertDto>(`/admin/alerts/${id}/acknowledge`, body, withIdempotency()),
}

/** 静默规则 API */
export const alertSilenceApi = {
  /** 查询静默规则列表 */
  list: () =>
    client.get<SilenceDto[]>('/admin/alerts/silences'),

  /** 创建静默规则（幂等） */
  create: (body: CreateSilenceDto) =>
    client.post<SilenceDto>('/admin/alerts/silences', body, withIdempotency()),

  /** 删除静默规则 */
  remove: (id: string) =>
    client.delete<void>(`/admin/alerts/silences/${id}`),
}
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/api/alerts.api.ts
git commit -m "feat(runtime-ops): 实现 alertApi + alertSilenceApi（acknowledge/create/remove 含幂等键）"
```

---

## Task 8: routes.ts + index.ts 模块聚合

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/routes.ts`
- Create: `web/system-admin/src/modules/04-runtime-ops/index.ts`

- [ ] **Step 1: 实现 routes.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/routes.ts
// 04-runtime-ops 模块路由项：6 个视图，meta 含 title/menuKey/icon/roles/permission/menuGroup
import type { RouteRecordRaw } from 'vue-router'

export const runtimeOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'rate-limit-rules',
    name: 'runtime-ops.rate-limit-rules',
    component: () => import('../views/RateLimitRules.vue'),
    meta: {
      title: '限流规则',
      menuKey: 'runtime-ops.rate-limit-rules',
      icon: 'ThunderboltOutlined',
      roles: ['Admin'],
      permission: 'rate-limit:write',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'index-rebuild',
    name: 'runtime-ops.index-rebuild',
    component: () => import('../views/IndexRebuild.vue'),
    meta: {
      title: '索引重建',
      menuKey: 'runtime-ops.index-rebuild',
      icon: 'DatabaseOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'index-rebuild:trigger',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'dead-letter-queue',
    name: 'runtime-ops.dead-letter-queue',
    component: () => import('../views/DeadLetterQueue.vue'),
    meta: {
      title: '死信队列',
      menuKey: 'runtime-ops.dead-letter-queue',
      icon: 'WarningOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'dead-letter:dispose',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'scheduled-tasks',
    name: 'runtime-ops.scheduled-tasks',
    component: () => import('../views/ScheduledTasks.vue'),
    meta: {
      title: '定时任务',
      menuKey: 'runtime-ops.scheduled-tasks',
      icon: 'ClockCircleOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'scheduled-task:write',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'health-monitoring',
    name: 'runtime-ops.health-monitoring',
    component: () => import('../views/HealthMonitoring.vue'),
    meta: {
      title: '健康监控',
      menuKey: 'runtime-ops.health-monitoring',
      icon: 'HeartOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'alert-management',
    name: 'runtime-ops.alert-management',
    component: () => import('../views/AlertManagement.vue'),
    meta: {
      title: '告警管理',
      menuKey: 'runtime-ops.alert-management',
      icon: 'BellOutlined',
      roles: ['Admin'],
      permission: 'alert:manage',
      menuGroup: '04-runtime-ops',
    },
  },
]

export default runtimeOpsRoutes
```

- [ ] **Step 2: 实现 index.ts**

```typescript
// web/system-admin/src/modules/04-runtime-ops/index.ts
// 模块对外出口：routes + 各 api 对象
export { default as runtimeOpsRoutes } from './routes'
export { rateLimitRuleApi } from './api/rate-limit-rules.api'
export { indexRebuildApi } from './api/index-rebuilds.api'
export { deadLetterApi } from './api/dead-letters.api'
export { scheduledTaskApi } from './api/scheduled-tasks.api'
export { healthApi } from './api/health.api'
export { alertApi, alertSilenceApi } from './api/alerts.api'
```

- [ ] **Step 3: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（routes.ts 引用 views/*.vue 文件尚不存在，但 vue-tsc 对动态 import 容忍；若报错需先创建空 .vue 占位）

```bash
git add web/system-admin/src/modules/04-runtime-ops/routes.ts web/system-admin/src/modules/04-runtime-ops/index.ts
git commit -m "feat(runtime-ops): 新增 routes.ts（6 路由项）与 index.ts 模块出口"
```

---

## Task 9: RateLimitRules.vue 限流规则视图

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/views/RateLimitRules.vue`

**实现要点（design-prompt §1-8）:**
- 顶部筛选条：目标 API 搜索 + 启用状态 + 目标上下文多选 + 「新增规则」按钮（PermissionGuard `rate-limit:write`）
- 主表格：API / 上下文 / 阈值 / 窗口 / 算法 / 维度 / 状态 / 操作（编辑/启用/停用），分页 20
- 弹窗表单：targetApi/targetContext/limit/windowSeconds/algorithm/scope（编辑时携带 version）
- 危险操作：停用启用中的规则走 ConfirmDialog，danger: true
- 409 乐观锁冲突：自动重新加载详情
- 算法 `<a-tag color="blue">`、维度 `<a-tag color="cyan">`

- [ ] **Step 1: 实现 RateLimitRules.vue**

```vue
<!-- web/system-admin/src/modules/04-runtime-ops/views/RateLimitRules.vue -->
<!-- 限流规则管理：列表 + 筛选 + 新建/编辑弹窗 + 启停确认 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import { rateLimitRuleApi } from '../api/rate-limit-rules.api'
import type {
  RateLimitRuleDto,
  SaveRateLimitRuleDto,
  RateLimitAlgorithm,
  RateLimitScope,
} from '../types/rate-limit-rule.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { ConcurrencyError, BusinessError } from '@/shared/http/errors'

interface FilterState {
  targetApi: string
  enabled: '' | 'true' | 'false'
  targetContext: string[]
  page: number
  pageSize: number
}

interface FormState {
  ruleId?: string
  targetApi: string
  targetContext: string
  limit: number
  windowSeconds: number
  algorithm: RateLimitAlgorithm
  scope: RateLimitScope
  version?: number
}

const contextOptions = [
  'Identity', 'AccessControl', 'UserCenter', 'Points', 'Membership',
  'Review', 'AfterSales', 'Product', 'Order', 'Payment', 'Notification', 'Inventory',
]
const algorithmOptions: { label: string; value: RateLimitAlgorithm }[] = [
  { label: '滑动窗口', value: 'SlidingWindow' },
  { label: '令牌桶', value: 'TokenBucket' },
  { label: '固定窗口', value: 'FixedWindow' },
]
const scopeOptions: { label: string; value: RateLimitScope }[] = [
  { label: 'IP', value: 'IP' },
  { label: '用户', value: 'User' },
  { label: '全局', value: 'Global' },
  { label: '店铺', value: 'Shop' },
]

const loading = ref(false)
const dataList = ref<RateLimitRuleDto[]>([])
const total = ref(0)
const filter = reactive<FilterState>({
  targetApi: '',
  enabled: '',
  targetContext: [],
  page: 1,
  pageSize: 20,
})

const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const form = reactive<FormState>({
  targetApi: '',
  targetContext: 'Order',
  limit: 100,
  windowSeconds: 60,
  algorithm: 'SlidingWindow',
  scope: 'User',
})
const submitting = ref(false)
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'enable' | 'disable'; rule: RateLimitRuleDto } | null>(null)

const columns = computed(() => [
  { title: '目标 API', dataIndex: 'targetApi', key: 'targetApi', width: 200, ellipsis: true },
  { title: '目标上下文', dataIndex: 'targetContext', key: 'targetContext', width: 120 },
  { title: '阈值', dataIndex: 'limit', key: 'limit', width: 80, align: 'right' as const },
  { title: '窗口', key: 'windowSeconds', width: 100, customRender: ({ record }: { record: RateLimitRuleDto }) => `${record.windowSeconds}s` },
  { title: '算法', key: 'algorithm', width: 110 },
  { title: '维度', key: 'scope', width: 90 },
  { title: '状态', key: 'enabled', width: 100 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const },
])

async function loadList() {
  loading.value = true
  try {
    const params = {
      targetApi: filter.targetApi || undefined,
      enabled: filter.enabled === '' ? undefined : filter.enabled === 'true',
      targetContext: filter.targetContext.length ? filter.targetContext : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await rateLimitRuleApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载限流规则失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

function openCreate() {
  modalMode.value = 'create'
  Object.assign(form, {
    ruleId: undefined, targetApi: '', targetContext: 'Order',
    limit: 100, windowSeconds: 60, algorithm: 'SlidingWindow', scope: 'User', version: undefined,
  })
  modalVisible.value = true
}

async function openEdit(rule: RateLimitRuleDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    ruleId: rule.ruleId, targetApi: rule.targetApi, targetContext: rule.targetContext,
    limit: rule.limit, windowSeconds: rule.windowSeconds, algorithm: rule.algorithm,
    scope: rule.scope, version: rule.version,
  })
  modalVisible.value = true
}

async function onSubmit() {
  if (!form.targetApi.trim()) return message.error('目标 API 必填')
  if (form.limit <= 0) return message.error('阈值必须 > 0')
  if (form.windowSeconds <= 0) return message.error('窗口必须 > 0')
  submitting.value = true
  try {
    const body: SaveRateLimitRuleDto = {
      targetApi: form.targetApi.trim(),
      targetContext: form.targetContext,
      limit: form.limit,
      windowSeconds: form.windowSeconds,
      algorithm: form.algorithm,
      scope: form.scope,
      version: form.version,
    }
    if (modalMode.value === 'create') {
      await rateLimitRuleApi.create(body)
      message.success('规则已创建')
    } else if (form.ruleId) {
      await rateLimitRuleApi.update(form.ruleId, body)
      message.success('规则已更新')
    }
    modalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.error('数据已被其他用户修改，已自动刷新')
      if (form.ruleId) {
        const fresh = await rateLimitRuleApi.get(form.ruleId)
        Object.assign(form, { version: fresh.version })
      }
    } else if (e instanceof BusinessError) {
      message.error(e.message)
    } else {
      message.error('保存失败')
    }
  } finally {
    submitting.value = false
  }
}

function askToggle(rule: RateLimitRuleDto) {
  confirmAction.value = { kind: rule.enabled ? 'disable' : 'enable', rule }
  confirmVisible.value = true
}

const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'disable' ? '停用限流规则' : '启用限流规则')
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmContent = computed(() => {
  if (!confirmAction.value) return ''
  return confirmAction.value.kind === 'disable'
    ? '停用后该 API 将不再受限流保护，可能在高并发下被击穿。可随时启用恢复。'
    : '启用后该 API 将立即生效，按当前阈值与窗口进行限流。'
})

async function onConfirmToggle() {
  if (!confirmAction.value) return
  const { kind, rule } = confirmAction.value
  try {
    if (kind === 'enable') await rateLimitRuleApi.enable(rule.ruleId)
    else await rateLimitRuleApi.disable(rule.ruleId)
    message.success(kind === 'enable' ? '已启用' : '已停用')
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(loadList)
</script>

<template>
  <div class="runtime-ops-rate-limit">
    <div class="page-header">
      <div class="page-title">限流规则</div>
      <div class="page-desc">管理各域 API 限流规则，配置阈值/窗口/算法/维度，启停规则并热生效。</div>
    </div>

    <div class="toolbar">
      <a-input
        v-model:value="filter.targetApi"
        placeholder="搜索目标 API 路径"
        allow-clear
        style="width: 240px"
        @press-enter="onSearch"
      />
      <a-select
        v-model:value="filter.enabled"
        placeholder="启用状态"
        allow-clear
        style="width: 140px"
      >
        <a-select-option value="">全部</a-select-option>
        <a-select-option value="true">启用</a-select-option>
        <a-select-option value="false">停用</a-select-option>
      </a-select>
      <a-select
        v-model:value="filter.targetContext"
        mode="multiple"
        placeholder="目标上下文"
        allow-clear
        style="min-width: 220px"
        :options="contextOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="rate-limit:write">
        <a-button type="primary" @click="openCreate">
          <PlusOutlined />新增规则
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="ruleId"
      size="middle"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无限流规则" action-text="新增规则" @action="openCreate" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'algorithm'">
          <a-tag color="blue">{{ algorithmOptions.find((o) => o.value === record.algorithm)?.label ?? record.algorithm }}</a-tag>
        </template>
        <template v-else-if="column.key === 'scope'">
          <a-tag color="cyan">{{ scopeOptions.find((o) => o.value === record.scope)?.label ?? record.scope }}</a-tag>
        </template>
        <template v-else-if="column.key === 'enabled'">
          <StatusTag type="rateLimit" :status="record.enabled ? 'Enabled' : 'Disabled'" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <PermissionGuard permission="rate-limit:write">
              <a-button type="link" size="small" @click="openEdit(record)">
                <EditOutlined />编辑
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="rate-limit:write">
              <a-button
                type="link"
                size="small"
                :danger="record.enabled"
                @click="askToggle(record)"
              >
                <ThunderboltOutlined />{{ record.enabled ? '停用' : '启用' }}
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新增限流规则' : '编辑限流规则'"
      width="560"
      :confirm-loading="submitting"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="目标 API" required>
          <a-input v-model:value="form.targetApi" placeholder="/api/orders" />
        </a-form-item>
        <a-form-item label="目标上下文" required>
          <a-select v-model:value="form.targetContext" :options="contextOptions.map((v) => ({ label: v, value: v }))" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="阈值（请求数）" required>
              <a-input-number v-model:value="form.limit" :min="1" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="窗口（秒）" required>
              <a-input-number v-model:value="form.windowSeconds" :min="1" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="算法" required>
              <a-select v-model:value="form.algorithm" :options="algorithmOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="维度" required>
              <a-select v-model:value="form.scope" :options="scopeOptions" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmVisible"
      :title="confirmTitle"
      :content="confirmContent"
      :danger="confirmDanger"
      ok-text="确认"
      cancel-text="取消"
      @confirm="onConfirmToggle"
    />
  </div>
</template>

<style scoped>
.runtime-ops-rate-limit .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-rate-limit .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-rate-limit .page-desc { color: #8C8C8C; }
.runtime-ops-rate-limit .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-rate-limit .spacer { flex: 1; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/views/RateLimitRules.vue
git commit -m "feat(runtime-ops): 实现 RateLimitRules.vue（筛选+CRUD+启停确认+乐观锁处理）"
```

---

## Task 10: IndexRebuild.vue 索引重建视图

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/views/IndexRebuild.vue`

**实现要点（design-prompt §1-8）:**
- 顶部筛选条：目标上下文多选 + 状态多选（Pending/Running/Succeeded/Failed）+ 「触发重建」按钮
- 主表格：任务ID/上下文/索引名/状态/进度/触发人/触发时间/操作（详情/重试）
- 触发弹窗：targetContext + indexName
- 详情抽屉：进度条 `<a-progress>` + ProcessedDocs/TotalDocs + 失败原因
- 执行中任务每 5s 轮询进度
- 重试仅失败态可见，走 ConfirmDialog
- 触发与重试均二次确认

- [ ] **Step 1: 实现 IndexRebuild.vue**

```vue
<!-- web/system-admin/src/modules/04-runtime-ops/views/IndexRebuild.vue -->
<!-- 索引重建：列表 + 触发弹窗 + 详情抽屉（进度条 + 轮询） -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, EyeOutlined, ReloadOutlined, DatabaseOutlined,
} from '@ant-design/icons-vue'
import { indexRebuildApi } from '../api/index-rebuilds.api'
import type {
  IndexRebuildTaskDto,
  IndexRebuildStatus,
  TriggerIndexRebuildDto,
} from '../types/index-rebuild.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'

const contextOptions = ['Product', 'Order', 'Shop', 'Review', 'AfterSales', 'Points', 'Membership', 'UserCenter']
const statusOptions: { label: string; value: IndexRebuildStatus }[] = [
  { label: '待执行', value: 'Pending' },
  { label: '执行中', value: 'Running' },
  { label: '成功', value: 'Succeeded' },
  { label: '失败', value: 'Failed' },
]

const loading = ref(false)
const dataList = ref<IndexRebuildTaskDto[]>([])
const total = ref(0)
const filter = reactive<{ targetContext: string[]; status: IndexRebuildStatus[]; page: number; pageSize: number }>({
  targetContext: [],
  status: [],
  page: 1,
  pageSize: 20,
})

const triggerVisible = ref(false)
const triggerForm = reactive<TriggerIndexRebuildDto>({ targetContext: 'Product', indexName: '' })
const triggerSubmitting = ref(false)

const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<IndexRebuildTaskDto | null>(null)

const confirmVisible = ref(false)
const confirmTask = ref<IndexRebuildTaskDto | null>(null)
const confirmKind = ref<'trigger' | 'retry'>('trigger')

let pollTimer: ReturnType<typeof setInterval> | null = null

const columns = computed(() => [
  { title: '任务 ID', dataIndex: 'taskId', key: 'taskId', width: 160, ellipsis: true },
  { title: '上下文', dataIndex: 'targetContext', key: 'targetContext', width: 110 },
  { title: '索引名', dataIndex: 'indexName', key: 'indexName', width: 140 },
  { title: '状态', key: 'status', width: 100 },
  { title: '进度', key: 'progress', width: 160 },
  { title: '触发人', dataIndex: 'triggeredBy', key: 'triggeredBy', width: 110 },
  { title: '触发时间', dataIndex: 'triggeredAt', key: 'triggeredAt', width: 160 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const },
])

function computeProgress(task: IndexRebuildTaskDto): number {
  if (task.totalDocs <= 0) return task.status === 'Succeeded' ? 100 : 0
  return Math.min(100, Math.floor((task.processedDocs / task.totalDocs) * 100))
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      targetContext: filter.targetContext.length ? filter.targetContext : undefined,
      status: filter.status.length ? filter.status : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await indexRebuildApi.list(params)
    dataList.value = res.items
    total.value = res.total
    schedulePollRunning()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载索引重建任务失败')
  } finally {
    loading.value = false
  }
}

function schedulePollRunning() {
  if (pollTimer) clearInterval(pollTimer)
  const hasRunning = dataList.value.some((t) => t.status === 'Running')
  if (!hasRunning) return
  pollTimer = setInterval(async () => {
    const running = dataList.value.filter((t) => t.status === 'Running')
    for (const task of running) {
      try {
        const fresh = await indexRebuildApi.get(task.taskId)
        Object.assign(task, fresh)
      } catch {
        // 单条失败不阻塞其他轮询
      }
    }
    if (!dataList.value.some((t) => t.status === 'Running') && pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }, 5000)
}

function onSearch() {
  filter.page = 1
  loadList()
}

function openTrigger() {
  Object.assign(triggerForm, { targetContext: 'Product', indexName: '' })
  confirmKind.value = 'trigger'
  confirmVisible.value = true
}

const confirmTitle = computed(() =>
  confirmKind.value === 'trigger' ? '确认触发索引重建' : '确认重试索引重建任务')
const confirmContent = computed(() =>
  confirmKind.value === 'trigger'
    ? '重建期间查询走旧索引，切换瞬间有秒级双读窗口。增量事件暂存补偿，重建完成后回放。'
    : '重试将重新执行索引重建，期间查询走旧索引。原任务记录保留。')

async function onConfirm() {
  if (confirmKind.value === 'trigger') {
    if (!triggerForm.indexName.trim()) {
      message.error('索引名必填')
      return
    }
    triggerSubmitting.value = true
    try {
      await indexRebuildApi.trigger({
        targetContext: triggerForm.targetContext,
        indexName: triggerForm.indexName.trim(),
      })
      message.success('重建任务已触发')
      confirmVisible.value = false
      loadList()
    } catch (e) {
      if (e instanceof BusinessError) message.error(e.message)
      else message.error('触发失败')
    } finally {
      triggerSubmitting.value = false
    }
  } else if (confirmTask.value) {
    try {
      await indexRebuildApi.retry(confirmTask.value.taskId)
      message.success('已重新加入队列')
      confirmVisible.value = false
      loadList()
    } catch (e) {
      if (e instanceof BusinessError) message.error(e.message)
      else message.error('重试失败')
    }
  }
}

async function openDetail(task: IndexRebuildTaskDto) {
  detail.value = task
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await indexRebuildApi.get(task.taskId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  } finally {
    detailLoading.value = false
  }
}

function askRetry(task: IndexRebuildTaskDto) {
  confirmTask.value = task
  confirmKind.value = 'retry'
  confirmVisible.value = true
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(loadList)
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <div class="runtime-ops-index-rebuild">
    <div class="page-header">
      <div class="page-title">索引重建</div>
      <div class="page-desc">触发各域 ES 读库全量索引重建，跟踪任务进度，重试失败任务。执行中任务每 5s 自动刷新进度。</div>
    </div>

    <div class="toolbar">
      <a-select
        v-model:value="filter.targetContext"
        mode="multiple"
        placeholder="目标上下文"
        allow-clear
        style="min-width: 220px"
        :options="contextOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 200px"
        :options="statusOptions"
      />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="index-rebuild:trigger">
        <a-button type="primary" @click="openTrigger">
          <PlusOutlined />触发重建
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="taskId"
      size="middle"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无重建任务" action-text="触发重建" @action="openTrigger" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <StatusTag type="indexRebuild" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'progress'">
          <a-progress :percent="computeProgress(record)" size="small" :status="record.status === 'Failed' ? 'exception' : record.status === 'Succeeded' ? 'success' : 'active'" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <a-button type="link" size="small" @click="openDetail(record)">
              <EyeOutlined />详情
            </a-button>
            <PermissionGuard permission="index-rebuild:trigger">
              <a-button
                v-if="record.status === 'Failed'"
                type="link"
                size="small"
                @click="askRetry(record)"
              >
                <ReloadOutlined />重试
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 触发重建弹窗（嵌入 ConfirmDialog 内的表单） -->
    <a-modal
      v-model:open="triggerVisible"
      title="触发索引重建"
      width="480"
      :confirm-loading="triggerSubmitting"
      ok-text="触发"
      cancel-text="取消"
      @ok="onConfirm"
    >
      <a-alert
        type="info"
        message="重建期间查询走旧索引，切换瞬间有秒级双读窗口。增量事件暂存补偿，重建完成后回放。"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item label="目标上下文" required>
          <a-select v-model:value="triggerForm.targetContext" :options="contextOptions.map((v) => ({ label: v, value: v }))" />
        </a-form-item>
        <a-form-item label="索引名" required>
          <a-input v-model:value="triggerForm.indexName" placeholder="products / orders / shops" />
        </a-form-item>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmVisible"
      :title="confirmTitle"
      :content="confirmContent"
      :danger="false"
      ok-text="确认"
      cancel-text="取消"
      @confirm="onConfirm"
    />

    <a-drawer
      v-model:open="detailVisible"
      title="索引重建任务详情"
      width="640"
      placement="right"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item label="任务 ID"><span class="mono">{{ detail.taskId }}</span></a-descriptions-item>
            <a-descriptions-item label="目标上下文">{{ detail.targetContext }}</a-descriptions-item>
            <a-descriptions-item label="索引名"><span class="mono">{{ detail.indexName }}</span></a-descriptions-item>
            <a-descriptions-item label="状态">
              <StatusTag type="indexRebuild" :status="detail.status" />
            </a-descriptions-item>
            <a-descriptions-item label="进度">
              <a-progress :percent="computeProgress(detail)" :status="detail.status === 'Failed' ? 'exception' : detail.status === 'Succeeded' ? 'success' : 'active'" />
              <div style="margin-top: 4px; color: #8C8C8C; font-size: 12px">
                {{ detail.processedDocs }} / {{ detail.totalDocs }} 文档
              </div>
            </a-descriptions-item>
            <a-descriptions-item label="触发人">{{ detail.triggeredBy }}</a-descriptions-item>
            <a-descriptions-item label="触发时间">{{ detail.triggeredAt }}</a-descriptions-item>
            <a-descriptions-item label="开始时间">{{ detail.startedAt ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="结束时间">{{ detail.finishedAt ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="重试次数">{{ detail.retryCount }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.errorMessage" label="失败原因">
              <span style="color: #FF4D4F">{{ detail.errorMessage }}</span>
            </a-descriptions-item>
          </a-descriptions>
          <a-alert
            v-if="detail.status === 'Running'"
            type="info"
            message="重建期间查询走旧索引，切换瞬间有秒级双读窗口。"
            show-icon
            style="margin-top: 16px"
          />
        </template>
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-index-rebuild .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-index-rebuild .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-index-rebuild .page-desc { color: #8C8C8C; }
.runtime-ops-index-rebuild .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-index-rebuild .spacer { flex: 1; }
.runtime-ops-index-rebuild .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/views/IndexRebuild.vue
git commit -m "feat(runtime-ops): 实现 IndexRebuild.vue（触发+详情抽屉+5s 轮询+重试确认）"
```

---

## Task 11: DeadLetterQueue.vue 死信队列视图

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/views/DeadLetterQueue.vue`

**实现要点（design-prompt §1-8 + 设计稿 dead-letter-queue.html）:**
- 顶部统计条：待处理/今日已重投/今日已丢弃/积压队列数（4 个 stat-mini）
- 筛选条：来源上下文多选 + 状态多选（Pending/Retried/Discarded）+ DateTimeRangePicker + 「刷新」
- 批量操作条：选中 N 条后显示「批量重投」「批量丢弃」
- 主表格：选择列 + 消息ID/来源/原始主题/失败原因/重试次数/状态/进入时间/操作
- 详情抽屉：全字段 + Headers JSON + Payload JSON（JsonViewer）+ 处置历史
- 单条重投/丢弃 + 批量重投/丢弃，ConfirmDialog danger + 丢弃需填理由 requireInput
- 批量结果弹窗展示 BatchOperationResultDto 明细
- 仅 Pending 态显示重投/丢弃按钮

- [ ] **Step 1: 实现 DeadLetterQueue.vue**

```vue
<!-- web/system-admin/src/modules/04-runtime-ops/views/DeadLetterQueue.vue -->
<!-- 死信队列：统计+筛选+批量操作+表格+详情抽屉+重投/丢弃确认 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  ReloadOutlined, DeleteOutlined, EyeOutlined, WarningOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { deadLetterApi } from '../api/dead-letters.api'
import type {
  DeadLetterMessageDto,
  DeadLetterStatus,
  BatchOperationResultDto,
} from '../types/dead-letter.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { BusinessError } from '@/shared/http/errors'

const sourceOptions = ['Order', 'Payment', 'Notification', 'Inventory', 'Review', 'AfterSales', 'Points', 'Membership']
const statusOptions: { label: string; value: DeadLetterStatus }[] = [
  { label: '待处理', value: 'Pending' },
  { label: '已重投', value: 'Retried' },
  { label: '已丢弃', value: 'Discarded' },
]

const loading = ref(false)
const dataList = ref<DeadLetterMessageDto[]>([])
const total = ref(0)
const selectedRowKeys = ref<string[]>([])
const filter = reactive<{ sourceContext: string[]; status: DeadLetterStatus[]; range: [string, string] | null; page: number; pageSize: number }>({
  sourceContext: [],
  status: ['Pending'],
  range: null,
  page: 1,
  pageSize: 20,
})

const stats = reactive({ pending: 0, retriedToday: 0, discardedToday: 0, backlogQueues: 0 })

const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<DeadLetterMessageDto | null>(null)

const retryConfirm = ref(false)
const discardConfirm = ref(false)
const discardReason = ref('')
const discardTargetId = ref<string | null>(null)
const batchMode = ref<'single' | 'batch'>('single')

const batchResultVisible = ref(false)
const batchResult = ref<BatchOperationResultDto | null>(null)
const batchResultKind = ref<'retry' | 'discard'>('retry')

const columns = computed(() => [
  { title: '消息 ID', dataIndex: 'messageId', key: 'messageId', width: 180, ellipsis: true },
  { title: '来源', dataIndex: 'sourceContext', key: 'sourceContext', width: 100 },
  { title: '原始主题', dataIndex: 'originalTopic', key: 'originalTopic', width: 160, ellipsis: true },
  { title: '失败原因', dataIndex: 'errorReason', key: 'errorReason', ellipsis: true },
  { title: '重试', dataIndex: 'retryCount', key: 'retryCount', width: 70, align: 'right' as const },
  { title: '状态', key: 'status', width: 90 },
  { title: '进入时间', dataIndex: 'failedAt', key: 'failedAt', width: 150 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' as const },
])

async function loadList() {
  loading.value = true
  try {
    const params = {
      sourceContext: filter.sourceContext.length ? filter.sourceContext : undefined,
      status: filter.status.length ? filter.status : undefined,
      startTime: filter.range?.[0],
      endTime: filter.range?.[1],
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await deadLetterApi.list(params)
    dataList.value = res.items
    total.value = res.total
    // 统计简化：从当前页与总数推导（后端如提供独立统计端点可替换）
    stats.pending = res.items.filter((i) => i.status === 'Pending').length
    const today = dayjs().format('YYYY-MM-DD')
    stats.retriedToday = res.items.filter((i) => i.status === 'Retried' && dayjs(i.operatedAt ?? i.failedAt).format('YYYY-MM-DD') === today).length
    stats.discardedToday = res.items.filter((i) => i.status === 'Discarded' && dayjs(i.operatedAt ?? i.failedAt).format('YYYY-MM-DD') === today).length
    stats.backlogQueues = new Set(res.items.map((i) => i.deadLetterQueue)).size
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载死信列表失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  selectedRowKeys.value = []
  loadList()
}

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: string[]) => { selectedRowKeys.value = keys },
}))

async function openDetail(record: DeadLetterMessageDto) {
  detail.value = record
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await deadLetterApi.get(record.messageId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  } finally {
    detailLoading.value = false
  }
}

function askRetrySingle(record: DeadLetterMessageDto) {
  batchMode.value = 'single'
  selectedRowKeys.value = [record.messageId]
  retryConfirm.value = true
}

function askRetryBatch() {
  if (selectedRowKeys.value.length === 0) return message.warning('请先选择消息')
  if (selectedRowKeys.value.length > 100) return message.warning('批量操作 ≤ 100 条/次')
  batchMode.value = 'batch'
  retryConfirm.value = true
}

function askDiscardSingle(record: DeadLetterMessageDto) {
  batchMode.value = 'single'
  discardTargetId.value = record.messageId
  selectedRowKeys.value = [record.messageId]
  discardReason.value = ''
  discardConfirm.value = true
}

function askDiscardBatch() {
  if (selectedRowKeys.value.length === 0) return message.warning('请先选择消息')
  if (selectedRowKeys.value.length > 100) return message.warning('批量操作 ≤ 100 条/次')
  batchMode.value = 'batch'
  discardReason.value = ''
  discardConfirm.value = true
}

async function onConfirmRetry() {
  const ids = selectedRowKeys.value
  try {
    if (batchMode.value === 'single' && ids[0]) {
      await deadLetterApi.retry(ids[0])
      message.success('已重投')
      retryConfirm.value = false
    } else {
      const result = await deadLetterApi.batchRetry(ids)
      batchResult.value = result
      batchResultKind.value = 'retry'
      batchResultVisible.value = true
      retryConfirm.value = false
    }
    selectedRowKeys.value = []
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.info(e.message)
    else message.error('重投失败')
  }
}

async function onConfirmDiscard() {
  if (!discardReason.value.trim()) {
    message.error('丢弃原因为必填项')
    return
  }
  const ids = selectedRowKeys.value
  try {
    if (batchMode.value === 'single' && ids[0]) {
      await deadLetterApi.discard(ids[0], { discardReason: discardReason.value.trim() })
      message.success('已丢弃')
      discardConfirm.value = false
    } else {
      const result = await deadLetterApi.batchDiscard(ids, discardReason.value.trim())
      batchResult.value = result
      batchResultKind.value = 'discard'
      batchResultVisible.value = true
      discardConfirm.value = false
    }
    selectedRowKeys.value = []
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('丢弃失败')
  }
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(loadList)
</script>

<template>
  <div class="runtime-ops-dead-letter">
    <div class="page-header">
      <div class="page-title">死信队列</div>
      <div class="page-desc">跨域汇聚各 MQ 死信消息，查看详情、单条或批量重投、丢弃处置。重投与丢弃幂等；丢弃不可逆，需填理由。</div>
      <a-button style="position: absolute; right: 24px; top: 24px" @click="loadList">
        <ReloadOutlined />刷新
      </a-button>
    </div>

    <div class="stats-row">
      <div class="stat-mini"><div class="stat-mini-label">待处理死信</div><div class="stat-mini-value" style="color:#FAAD14">{{ stats.pending }}</div></div>
      <div class="stat-mini"><div class="stat-mini-label">今日已重投</div><div class="stat-mini-value" style="color:#1677FF">{{ stats.retriedToday }}</div></div>
      <div class="stat-mini"><div class="stat-mini-label">今日已丢弃</div><div class="stat-mini-value" style="color:#8C8C8C">{{ stats.discardedToday }}</div></div>
      <div class="stat-mini"><div class="stat-mini-label">积压队列数</div><div class="stat-mini-value">{{ stats.backlogQueues }}</div></div>
    </div>

    <div class="toolbar">
      <a-select
        v-model:value="filter.sourceContext"
        mode="multiple"
        placeholder="全部来源"
        allow-clear
        style="min-width: 220px"
        :options="sourceOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 200px"
        :options="statusOptions"
      />
      <DateTimeRangePicker v-model="filter.range" />
      <a-button type="primary" @click="onSearch">筛选</a-button>
    </div>

    <div v-if="selectedRowKeys.length > 0" class="batch-bar">
      <span>已选中 <b style="color:#1677FF">{{ selectedRowKeys.length }}</b> 条消息</span>
      <div class="spacer" />
      <PermissionGuard permission="dead-letter:dispose">
        <a-button type="primary" size="small" @click="askRetryBatch">
          <ReloadOutlined />批量重投
        </a-button>
      </PermissionGuard>
      <PermissionGuard permission="dead-letter:dispose">
        <a-button danger size="small" @click="askDiscardBatch">
          <DeleteOutlined />批量丢弃
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="messageId"
      size="middle"
      :row-selection="rowSelection"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无死信消息" action-text="刷新" @action="loadList" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'sourceContext'">
          <a-tag color="purple">{{ record.sourceContext }}</a-tag>
        </template>
        <template v-else-if="column.key === 'errorReason'">
          <span style="color:#FF4D4F; font-size:12px">{{ record.errorReason }}</span>
        </template>
        <template v-else-if="column.key === 'status'">
          <StatusTag type="deadLetter" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <a-button type="link" size="small" @click="openDetail(record)">
              <EyeOutlined />详情
            </a-button>
            <PermissionGuard permission="dead-letter:dispose">
              <a-button
                v-if="record.status === 'Pending'"
                type="link"
                size="small"
                @click="askRetrySingle(record)"
              >
                <ReloadOutlined />重投
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="dead-letter:dispose">
              <a-button
                v-if="record.status === 'Pending'"
                type="link"
                size="small"
                danger
                @click="askDiscardSingle(record)"
              >
                <DeleteOutlined />丢弃
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="detailVisible"
      title="死信消息详情"
      width="720"
      placement="right"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item label="消息 ID"><span class="mono">{{ detail.messageId }}</span></a-descriptions-item>
            <a-descriptions-item label="原始消息 ID"><span class="mono">{{ detail.originalMessageId }}</span></a-descriptions-item>
            <a-descriptions-item label="来源上下文"><a-tag color="purple">{{ detail.sourceContext }}</a-tag></a-descriptions-item>
            <a-descriptions-item label="原始主题"><span class="mono">{{ detail.originalTopic }}</span></a-descriptions-item>
            <a-descriptions-item label="原始队列"><span class="mono">{{ detail.originalQueue }}</span></a-descriptions-item>
            <a-descriptions-item label="死信队列"><span class="mono">{{ detail.deadLetterQueue }}</span></a-descriptions-item>
            <a-descriptions-item label="失败原因"><span style="color:#FF4D4F">{{ detail.errorReason }}</span></a-descriptions-item>
            <a-descriptions-item label="进入死信时间">{{ detail.failedAt }}</a-descriptions-item>
            <a-descriptions-item label="重试次数"><b>{{ detail.retryCount }}</b> 次</a-descriptions-item>
            <a-descriptions-item label="状态"><StatusTag type="deadLetter" :status="detail.status" /></a-descriptions-item>
            <a-descriptions-item label="操作人">{{ detail.operatorId ?? '—' }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.discardReason" label="丢弃原因">{{ detail.discardReason }}</a-descriptions-item>
          </a-descriptions>

          <div class="section-title">消息头（Headers）</div>
          <JsonViewer :data="detail.headers" :max-height="280" />

          <div class="section-title">原始消息体（Payload）</div>
          <JsonViewer :data="(() => { try { return JSON.parse(detail.payload) } catch { return detail.payload } })()" :max-height="280" />

          <div class="section-title">处置历史</div>
          <div class="history-list">
            <div v-for="(item, idx) in detail.history" :key="idx" class="history-item">
              <div class="history-dot" :class="{ retry: item.action === 'Retry', discard: item.action === 'Discard' }" />
              <div class="history-content">
                <div class="history-action">
                  <template v-if="item.action === 'Retry'">重投到原队列</template>
                  <template v-else-if="item.action === 'Discard'">丢弃消息</template>
                  <template v-else>消息进入死信队列</template>
                </div>
                <div class="history-meta">
                  操作人 {{ item.operator ?? '系统' }} · {{ item.operatedAt }} · 结果：{{ item.result }}
                </div>
              </div>
            </div>
          </div>

          <a-alert
            v-if="detail.retryCount >= 2 && detail.status === 'Pending'"
            type="warning"
            show-icon
            style="margin-top: 16px"
            :message="`该消息已重试 ${detail.retryCount} 次仍进入死信，建议检查下游服务日志后再决定重投或丢弃。`"
          />
        </template>
      </a-spin>
    </a-drawer>

    <ConfirmDialog
      v-model:open="retryConfirm"
      title="确认重投死信消息"
      :content="`即将重投 ${selectedRowKeys.length} 条消息。重投后消息将重新投递到原队列，可能触发重复业务逻辑。已重投或已丢弃的消息幂等返回当前状态。`"
      :danger="false"
      ok-text="确认重投"
      cancel-text="取消"
      @confirm="onConfirmRetry"
    />

    <ConfirmDialog
      v-model:open="discardConfirm"
      title="确认丢弃死信消息"
      :content="`即将丢弃 ${selectedRowKeys.length} 条消息。丢弃后该消息将永久不再处理，关联业务可能丢失。此操作不可逆。`"
      :danger="true"
      :require-input="{ label: '丢弃原因', placeholder: '请填写丢弃原因，将记录至审计日志', min: 1, max: 500 }"
      :input-value="discardReason"
      ok-text="确认丢弃"
      cancel-text="取消"
      @input-change="(v: string) => (discardReason = v)"
      @confirm="onConfirmDiscard"
    />

    <a-modal
      v-model:open="batchResultVisible"
      :title="batchResultKind === 'retry' ? '批量重投结果' : '批量丢弃结果'"
      width="520"
      ok-text="知道了"
      :cancel-button-props="{ style: { display: 'none' } }"
    >
      <template v-if="batchResult">
        <div class="result-summary" :class="{ partial: batchResult.failed.length > 0 }">
          <div>
            <div class="result-num ok">{{ batchResult.succeeded.length }}</div>
            <div class="result-label">成功</div>
          </div>
          <div>
            <div class="result-num fail">{{ batchResult.failed.length }}</div>
            <div class="result-label">失败</div>
          </div>
        </div>
        <div v-if="batchResult.failed.length > 0" class="section-title">失败明细</div>
        <div v-if="batchResult.failed.length > 0" class="fail-list">
          <div v-for="f in batchResult.failed" :key="f.messageId" class="fail-list-item">
            <span class="fail-id">{{ f.messageId }}</span>
            <span class="fail-reason">— {{ f.reason }}</span>
          </div>
        </div>
      </template>
    </a-modal>
  </div>
</template>

<style scoped>
.runtime-ops-dead-letter .page-header { position: relative; background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-dead-letter .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-dead-letter .page-desc { color: #8C8C8C; max-width: 760px; }
.runtime-ops-dead-letter .stats-row { display: flex; gap: 12px; margin-bottom: 16px; }
.runtime-ops-dead-letter .stat-mini { flex: 1; background: #fff; border-radius: 8px; box-shadow: 0 1px 2px rgba(0,0,0,.03); padding: 16px; }
.runtime-ops-dead-letter .stat-mini-label { font-size: 12px; color: #8C8C8C; }
.runtime-ops-dead-letter .stat-mini-value { font-size: 24px; font-weight: 600; margin-top: 4px; }
.runtime-ops-dead-letter .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-dead-letter .spacer { flex: 1; }
.runtime-ops-dead-letter .batch-bar { display: flex; align-items: center; gap: 12px; padding: 12px 16px; background: #e6f4ff; border: 1px solid #91caff; border-radius: 6px; margin-bottom: 16px; }
.runtime-ops-dead-letter .section-title { font-size: 14px; font-weight: 500; margin: 16px 0 8px; display: flex; align-items: center; gap: 8px; }
.runtime-ops-dead-letter .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
.runtime-ops-dead-letter .history-list { border: 1px solid #f0f0f0; border-radius: 6px; padding: 12px 16px; }
.runtime-ops-dead-letter .history-item { display: flex; gap: 12px; padding: 8px 0; border-bottom: 1px solid #f0f0f0; }
.runtime-ops-dead-letter .history-item:last-child { border-bottom: none; }
.runtime-ops-dead-letter .history-dot { width: 8px; height: 8px; border-radius: 50%; margin-top: 6px; flex-shrink: 0; background: #FAAD14; }
.runtime-ops-dead-letter .history-dot.retry { background: #1677FF; }
.runtime-ops-dead-letter .history-dot.discard { background: #8C8C8C; }
.runtime-ops-dead-letter .history-action { font-size: 13px; font-weight: 500; }
.runtime-ops-dead-letter .history-meta { font-size: 12px; color: #8C8C8C; margin-top: 2px; }
.runtime-ops-dead-letter .result-summary { display: flex; gap: 32px; padding: 12px 16px; border-radius: 6px; margin-bottom: 12px; background: #f6ffed; border: 1px solid #b7eb8f; }
.runtime-ops-dead-letter .result-summary.partial { background: #fffbe6; border-color: #ffe58f; }
.runtime-ops-dead-letter .result-num { font-size: 20px; font-weight: 600; }
.runtime-ops-dead-letter .result-num.ok { color: #52C41A; }
.runtime-ops-dead-letter .result-num.fail { color: #FF4D4F; }
.runtime-ops-dead-letter .result-label { font-size: 12px; color: #8C8C8C; }
.runtime-ops-dead-letter .fail-list { background: #f5f5f5; border-radius: 6px; padding: 12px; font-size: 12px; color: #595959; }
.runtime-ops-dead-letter .fail-list-item { padding: 4px 0; display: flex; gap: 8px; align-items: center; }
.runtime-ops-dead-letter .fail-id { font-family: "SF Mono",Consolas,monospace; color: #000000D9; }
.runtime-ops-dead-letter .fail-reason { color: #FF4D4F; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/views/DeadLetterQueue.vue
git commit -m "feat(runtime-ops): 实现 DeadLetterQueue.vue（统计+批量+详情抽屉+丢弃必填理由+结果明细）"
```

---

## Task 12: ScheduledTasks.vue 定时任务视图

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/views/ScheduledTasks.vue`

**实现要点（design-prompt §1-8）:**
- 顶部筛选条：任务名搜索 + 状态多选（Enabled/Disabled）+ 作业类型筛选 + 「新增任务」按钮
- 主表格：任务名/Cron/类型/状态/最近执行/下次执行/操作（编辑/启用/停用/立即执行/历史），分页 20
- 弹窗表单：name/jobType（编辑时只读）/cronExpression（含下次执行预览）/parameters JSON/status
- 执行历史抽屉：最近 20 次执行记录
- 危险操作：停用启用中的任务走 ConfirmDialog danger；立即执行走 ConfirmDialog（主色）
- Cron 表达式前端校验（5 段）+ 后端 400 错误提示

- [ ] **Step 1: 实现 ScheduledTasks.vue**

```vue
<!-- web/system-admin/src/modules/04-runtime-ops/views/ScheduledTasks.vue -->
<!-- 定时任务管理：列表 + 新建/编辑弹窗（作业类型编辑只读）+ 历史抽屉 + 启停/立即执行确认 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, EditOutlined, PlayCircleOutlined, HistoryOutlined, ClockCircleOutlined,
} from '@ant-design/icons-vue'
import { scheduledTaskApi } from '../api/scheduled-tasks.api'
import type {
  ScheduledTaskDto,
  ScheduledTaskStatus,
  SaveScheduledTaskDto,
  UpdateScheduledTaskDto,
  ScheduledTaskExecutionDto,
} from '../types/scheduled-task.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { BusinessError } from '@/shared/http/errors'

const jobTypeOptions = [
  'Reconciliation', 'Report', 'Cleanup', 'Notification', 'Sync', 'Snapshot',
]
const statusOptions: { label: string; value: ScheduledTaskStatus }[] = [
  { label: '启用', value: 'Enabled' },
  { label: '停用', value: 'Disabled' },
]

const loading = ref(false)
const dataList = ref<ScheduledTaskDto[]>([])
const total = ref(0)
const filter = reactive<{ name: string; status: ScheduledTaskStatus[]; jobType: string; page: number; pageSize: number }>({
  name: '',
  status: [],
  jobType: '',
  page: 1,
  pageSize: 20,
})

const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const form = reactive<{
  taskId?: string
  name: string
  jobType: string
  cronExpression: string
  parameters: string
}>({ name: '', jobType: 'Reconciliation', cronExpression: '0 2 * * *', parameters: '{}' })
const submitting = ref(false)

const historyVisible = ref(false)
const historyLoading = ref(false)
const historyList = ref<ScheduledTaskExecutionDto[]>([])
const historyTaskName = ref('')

const confirmVisible = ref(false)
const confirmAction = ref<{
  kind: 'enable' | 'disable' | 'runNow'
  task: ScheduledTaskDto
} | null>(null)

const columns = computed(() => [
  { title: '任务名', dataIndex: 'name', key: 'name', width: 160 },
  { title: 'Cron', dataIndex: 'cronExpression', key: 'cronExpression', width: 130 },
  { title: '作业类型', dataIndex: 'jobType', key: 'jobType', width: 130 },
  { title: '状态', key: 'status', width: 90 },
  { title: '最近执行', dataIndex: 'lastRunAt', key: 'lastRunAt', width: 160 },
  { title: '下次执行', dataIndex: 'nextRunAt', key: 'nextRunAt', width: 160 },
  { title: '操作', key: 'action', width: 280, fixed: 'right' as const },
])

function validateCron(cron: string): boolean {
  // 简单 5 段校验，详细校验由后端完成
  const parts = cron.trim().split(/\s+/)
  return parts.length === 5
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      name: filter.name || undefined,
      status: filter.status.length ? filter.status : undefined,
      jobType: filter.jobType || undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await scheduledTaskApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载定时任务失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

function openCreate() {
  modalMode.value = 'create'
  Object.assign(form, { taskId: undefined, name: '', jobType: 'Reconciliation', cronExpression: '0 2 * * *', parameters: '{}' })
  modalVisible.value = true
}

function openEdit(task: ScheduledTaskDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    taskId: task.taskId,
    name: task.name,
    jobType: task.jobType,
    cronExpression: task.cronExpression,
    parameters: JSON.stringify(task.parameters ?? {}, null, 2),
  })
  modalVisible.value = true
}

async function onSubmit() {
  if (!form.name.trim()) return message.error('任务名必填')
  if (!validateCron(form.cronExpression)) return message.error('Cron 表达式必须为 5 段（分 时 日 月 周）')
  let parsedParameters: Record<string, unknown> = {}
  try {
    parsedParameters = JSON.parse(form.parameters || '{}')
  } catch {
    return message.error('参数 JSON 格式错误')
  }
  submitting.value = true
  try {
    if (modalMode.value === 'create') {
      const body: SaveScheduledTaskDto = {
        name: form.name.trim(),
        jobType: form.jobType,
        cronExpression: form.cronExpression.trim(),
        parameters: parsedParameters,
      }
      await scheduledTaskApi.create(body)
      message.success('任务已创建（停用态）')
    } else if (form.taskId) {
      const body: UpdateScheduledTaskDto = {
        name: form.name.trim(),
        cronExpression: form.cronExpression.trim(),
        parameters: parsedParameters,
      }
      await scheduledTaskApi.update(form.taskId, body)
      message.success('任务已更新')
    }
    modalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('保存失败')
  } finally {
    submitting.value = false
  }
}

function askAction(kind: 'enable' | 'disable' | 'runNow', task: ScheduledTaskDto) {
  confirmAction.value = { kind, task }
  confirmVisible.value = true
}

const confirmTitle = computed(() => {
  if (!confirmAction.value) return ''
  return { enable: '启用定时任务', disable: '停用定时任务', runNow: '立即执行任务' }[confirmAction.value.kind]
})
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmContent = computed(() => {
  if (!confirmAction.value) return ''
  const map = {
    enable: '启用后任务将向调度器注册，按 Cron 表达式自动执行。',
    disable: '停用后任务将从调度器注销，不再按 Cron 执行。已注册的下一次执行取消。可随时启用恢复。',
    runNow: '立即执行将忽略 Cron 调度，立即触发一次任务。请确认非高峰时段。',
  }
  return map[confirmAction.value.kind]
})

async function onConfirm() {
  if (!confirmAction.value) return
  const { kind, task } = confirmAction.value
  try {
    if (kind === 'enable') await scheduledTaskApi.enable(task.taskId)
    else if (kind === 'disable') await scheduledTaskApi.disable(task.taskId)
    else await scheduledTaskApi.runNow(task.taskId)
    message.success({ enable: '已启用', disable: '已停用', runNow: '已触发立即执行' }[kind])
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

async function openHistory(task: ScheduledTaskDto) {
  historyTaskName.value = task.name
  historyVisible.value = true
  historyLoading.value = true
  try {
    historyList.value = await scheduledTaskApi.getHistory(task.taskId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    historyList.value = []
  } finally {
    historyLoading.value = false
  }
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(loadList)
</script>

<template>
  <div class="runtime-ops-scheduled-tasks">
    <div class="page-header">
      <div class="page-title">定时任务</div>
      <div class="page-desc">管理定时任务，CRUD/启停/立即触发，监控任务执行状态。作业类型创建后不可变更。</div>
    </div>

    <div class="toolbar">
      <a-input
        v-model:value="filter.name"
        placeholder="搜索任务名"
        allow-clear
        style="width: 200px"
        @press-enter="onSearch"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 180px"
        :options="statusOptions"
      />
      <a-select
        v-model:value="filter.jobType"
        placeholder="作业类型"
        allow-clear
        style="width: 160px"
        :options="jobTypeOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="scheduled-task:write">
        <a-button type="primary" @click="openCreate">
          <PlusOutlined />新增任务
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="taskId"
      size="middle"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无定时任务" action-text="新增任务" @action="openCreate" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'cronExpression'">
          <span class="mono">{{ record.cronExpression }}</span>
        </template>
        <template v-else-if="column.key === 'jobType'">
          <a-tag color="geekblue">{{ record.jobType }}</a-tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <StatusTag type="scheduledTask" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'lastRunAt'">
          {{ record.lastRunAt ?? '—' }}
        </template>
        <template v-else-if="column.key === 'nextRunAt'">
          {{ record.nextRunAt ?? '—' }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small" wrap>
            <PermissionGuard permission="scheduled-task:write">
              <a-button type="link" size="small" @click="openEdit(record)">
                <EditOutlined />编辑
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="scheduled-task:write">
              <a-button
                v-if="record.status === 'Disabled'"
                type="link"
                size="small"
                @click="askAction('enable', record)"
              >
                启用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                danger
                @click="askAction('disable', record)"
              >
                停用
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="scheduled-task:run-now">
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'Disabled'"
                @click="askAction('runNow', record)"
              >
                <PlayCircleOutlined />立即执行
              </a-button>
            </PermissionGuard>
            <a-button type="link" size="small" @click="openHistory(record)">
              <HistoryOutlined />历史
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新增定时任务' : '编辑定时任务'"
      width="560"
      :confirm-loading="submitting"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="任务名" required>
          <a-input v-model:value="form.name" placeholder="对账任务" />
        </a-form-item>
        <a-form-item required>
          <template #label>
            作业类型
            <a-tooltip v-if="modalMode === 'edit'" title="作业类型不可变">
              <InfoCircleOutlined style="margin-left: 4px; color: #8C8C8C" />
            </a-tooltip>
          </template>
          <a-select
            v-model:value="form.jobType"
            :disabled="modalMode === 'edit'"
            :options="jobTypeOptions.map((v) => ({ label: v, value: v }))"
          />
        </a-form-item>
        <a-form-item label="Cron 表达式" required>
          <a-input v-model:value="form.cronExpression" placeholder="0 2 * * *" class="mono" />
          <div style="font-size: 12px; color: #8C8C8C; margin-top: 4px">5 段：分 时 日 月 周</div>
        </a-form-item>
        <a-form-item label="参数（JSON）">
          <a-textarea v-model:value="form.parameters" :rows="6" class="mono" />
        </a-form-item>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmVisible"
      :title="confirmTitle"
      :content="confirmContent"
      :danger="confirmDanger"
      ok-text="确认"
      cancel-text="取消"
      @confirm="onConfirm"
    />

    <a-drawer
      v-model:open="historyVisible"
      :title="`执行历史 - ${historyTaskName}`"
      width="640"
      placement="right"
    >
      <a-spin :spinning="historyLoading">
        <a-empty v-if="historyList.length === 0" description="暂无执行记录" />
        <a-timeline v-else>
          <a-timeline-item
            v-for="exec in historyList"
            :key="exec.executionId"
            :color="exec.status === 'Succeeded' ? 'green' : exec.status === 'Failed' ? 'red' : 'blue'"
          >
            <div style="font-weight: 500">{{ exec.status }}</div>
            <div style="font-size: 12px; color: #8C8C8C">
              开始 {{ exec.startedAt }} · 结束 {{ exec.finishedAt ?? '—' }}
            </div>
            <div v-if="exec.errorMessage" style="font-size: 12px; color: #FF4D4F; margin-top: 4px">
              {{ exec.errorMessage }}
            </div>
          </a-timeline-item>
        </a-timeline>
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-scheduled-tasks .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-scheduled-tasks .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-scheduled-tasks .page-desc { color: #8C8C8C; }
.runtime-ops-scheduled-tasks .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-scheduled-tasks .spacer { flex: 1; }
.runtime-ops-scheduled-tasks .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/views/ScheduledTasks.vue
git commit -m "feat(runtime-ops): 实现 ScheduledTasks.vue（CRUD+启停/立即执行确认+历史抽屉+Cron 校验）"
```

---

## Task 13: HealthMonitoring.vue 健康监控视图

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/views/HealthMonitoring.vue`

**实现要点（design-prompt §1-8）:**
- 顶部整体状态条：`<a-alert>` 显示整体健康（Healthy/Degraded/Unhealthy）+ 检查时间 + 「立即检查」按钮
- 模块网格：`<a-row>` 排列模块卡片，每卡片含模块名/状态徽标/依赖项数/不健康依赖数，不健康优先
- 模块详情抽屉：依赖项明细（名称/状态/延迟/错误/最近检查）
- 每 30s 自动轮询刷新整体状态
- 首次进入有不健康模块触发 notification.error

- [ ] **Step 1: 实现 HealthMonitoring.vue**

```vue
<!-- web/system-admin/src/modules/04-runtime-ops/views/HealthMonitoring.vue -->
<!-- 健康监控：整体状态条 + 模块网格 + 详情抽屉 + 30s 轮询 -->
<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { message, notification } from 'ant-design-vue'
import {
  CheckCircleFilled, ExclamationCircleFilled, CloseCircleFilled, ReloadOutlined,
} from '@ant-design/icons-vue'
import { healthApi } from '../api/health.api'
import type {
  HealthAggregationResultDto,
  ModuleHealthDto,
  OverallStatus,
  DependencyStatus,
} from '../types/health.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import { BusinessError } from '@/shared/http/errors'

const loading = ref(false)
const aggregated = ref<HealthAggregationResultDto | null>(null)
const modules = ref<ModuleHealthDto[]>([])
const detailVisible = ref(false)
const detail = ref<ModuleHealthDto | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null
let firstLoad = true

const overallColor = computed<OverallStatus | 'Loading'>(() => aggregated.value?.overallStatus ?? 'Loading')

const sortedModules = computed(() => {
  const order: Record<DependencyStatus, number> = { Unhealthy: 0, Degraded: 1, Healthy: 2 }
  return [...modules.value].sort((a, b) => order[a.status] - order[b.status])
})

function countUnhealthy(m: ModuleHealthDto): number {
  return m.dependencies.filter((d) => d.status !== 'Healthy').length
}

async function loadAll() {
  loading.value = true
  try {
    const [agg, mods] = await Promise.all([healthApi.getAggregated(), healthApi.getModules()])
    aggregated.value = agg
    modules.value = mods
    if (firstLoad) {
      firstLoad = false
      const unhealthy = mods.filter((m) => m.status === 'Unhealthy')
      if (unhealthy.length > 0) {
        notification.error({
          message: '检测到不健康模块',
          description: `${unhealthy.map((m) => m.moduleName).join('、')} 处于不健康状态，请检查依赖项。`,
          duration: 5,
        })
      }
    }
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('健康检查失败')
  } finally {
    loading.value = false
  }
}

function openDetail(m: ModuleHealthDto) {
  detail.value = m
  detailVisible.value = true
}

function statusColor(s: DependencyStatus): string {
  return s === 'Healthy' ? '#52C41A' : s === 'Degraded' ? '#FAAD14' : '#FF4D4F'
}

onMounted(() => {
  loadAll()
  pollTimer = setInterval(loadAll, 30_000)
})
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <div class="runtime-ops-health">
    <div class="page-header">
      <div class="page-title">健康监控</div>
      <div class="page-desc">聚合各模块 /health 端点状态，查看整体健康与各模块依赖项（DB/Redis/ES/MQ/支付渠道/通知渠道）明细。每 30s 自动刷新。</div>
    </div>

    <a-skeleton :loading="loading && !aggregated" active>
      <a-alert
        v-if="aggregated"
        :type="aggregated.overallStatus === 'Healthy' ? 'success' : aggregated.overallStatus === 'Degraded' ? 'warning' : 'error'"
        show-icon
        style="margin-bottom: 16px"
      >
        <template #message>
          <span style="font-weight: 600">
            整体状态：
            <StatusTag type="health" :status="aggregated.overallStatus" />
            <span style="margin-left: 16px; color: #8C8C8C; font-weight: normal">
              检查时间 {{ aggregated.checkedAt }}
            </span>
          </span>
        </template>
        <template #action>
          <a-button size="small" type="primary" @click="loadAll">
            <ReloadOutlined />立即检查
          </a-button>
        </template>
      </a-alert>
    </a-skeleton>

    <a-row v-if="sortedModules.length > 0" :gutter="[16, 16]">
      <a-col v-for="m in sortedModules" :key="m.moduleName" :xs="24" :sm="12" :lg="6">
        <a-card
          hoverable
          size="small"
          :body-style="{ padding: '16px' }"
          :style="{ borderColor: statusColor(m.status), borderWidth: '1px' }"
          @click="openDetail(m)"
        >
          <div class="module-card">
            <div class="module-name">
              <component
                :is="m.status === 'Healthy' ? CheckCircleFilled : m.status === 'Degraded' ? ExclamationCircleFilled : CloseCircleFilled"
                :style="{ color: statusColor(m.status), fontSize: '20px' }"
              />
              <span style="margin-left: 8px">{{ m.moduleName }}</span>
            </div>
            <div class="module-status">
              <StatusTag type="health" :status="m.status" />
              <span style="margin-left: 8px; font-size: 12px; color: #8C8C8C">{{ m.latencyMs }}ms</span>
            </div>
            <div class="module-meta">
              <span>{{ m.dependencies.length }} 依赖</span>
              <span v-if="countUnhealthy(m) > 0" style="color: #FF4D4F; margin-left: 8px">{{ countUnhealthy(m) }} 不健康</span>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>
    <EmptyState
      v-else-if="!loading"
      description="暂无健康数据，请稍后重试"
      action-text="立即检查"
      @action="loadAll"
    />

    <a-drawer
      v-model:open="detailVisible"
      :title="detail ? `模块详情 - ${detail.moduleName}` : '模块详情'"
      width="640"
      placement="right"
    >
      <template v-if="detail">
        <a-descriptions :column="1" bordered size="small" style="margin-bottom: 16px">
          <a-descriptions-item label="模块名">{{ detail.moduleName }}</a-descriptions-item>
          <a-descriptions-item label="状态"><StatusTag type="health" :status="detail.status" /></a-descriptions-item>
          <a-descriptions-item label="延迟">{{ detail.latencyMs }} ms</a-descriptions-item>
          <a-descriptions-item label="依赖项数">{{ detail.dependencies.length }}</a-descriptions-item>
        </a-descriptions>

        <div class="section-title">依赖项明细</div>
        <a-table
          :data-source="detail.dependencies"
          row-key="name"
          size="small"
          :pagination="false"
          :columns="[
            { title: '名称', dataIndex: 'name', key: 'name' },
            { title: '状态', key: 'status', width: 100 },
            { title: '延迟', key: 'latencyMs', width: 80 },
            { title: '错误', dataIndex: 'error', key: 'error', ellipsis: true },
            { title: '最近检查', dataIndex: 'lastCheckedAt', key: 'lastCheckedAt', width: 160 },
          ]"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <StatusTag type="health" :status="record.status" />
            </template>
            <template v-else-if="column.key === 'latencyMs'">
              {{ record.latencyMs }}ms
            </template>
          </template>
        </a-table>
      </template>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-health .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-health .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-health .page-desc { color: #8C8C8C; }
.runtime-ops-health .module-card { display: flex; flex-direction: column; gap: 8px; }
.runtime-ops-health .module-name { display: flex; align-items: center; font-size: 16px; font-weight: 500; }
.runtime-ops-health .module-status { display: flex; align-items: center; }
.runtime-ops-health .module-meta { font-size: 12px; color: #8C8C8C; }
.runtime-ops-health .section-title { font-size: 14px; font-weight: 500; margin: 16px 0 8px; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/views/HealthMonitoring.vue
git commit -m "feat(runtime-ops): 实现 HealthMonitoring.vue（整体状态+模块网格+详情抽屉+30s 轮询+首检通知）"
```

---

## Task 14: AlertManagement.vue 告警管理视图

**Files:**
- Create: `web/system-admin/src/modules/04-runtime-ops/views/AlertManagement.vue`

**实现要点（design-prompt §1-8）:**
- 顶部统计条：4 个 `<a-statistic>` — 待处置告警数 / 严重告警数 / 今日告警总数 / 平均处置时长
- 筛选条：模块多选 + 严重级别多选（critical/warning/info）+ 状态多选（firing/acknowledged/resolved）+ 时间范围
- 主表格：告警ID/名称/模块/级别/状态/触发时间/持续时长/操作（详情/确认/静默）
- 详情抽屉：全字段 + 标签 + 注释 + 关联指标
- 确认告警弹窗：输入注释
- 静默规则弹窗：matchers + durationMinutes + reason
- firing 状态每 30s 轮询
- 顶部 `<a-alert type="info">` 提示 API 待 SystemAdmin BC 实现 Alertmanager 集成
- 已 resolved 告警处置按钮 disabled
- 静默规则 ConfirmDialog danger

- [ ] **Step 1: 实现 AlertManagement.vue**

```vue
<!-- web/system-admin/src/modules/04-runtime-ops/views/AlertManagement.vue -->
<!-- 告警管理：统计+筛选+表格+详情抽屉+确认/静默弹窗+30s 轮询 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import {
  WarningOutlined, ExclamationCircleOutlined, InfoCircleOutlined, BellOutlined,
} from '@ant-design/icons-vue'
import { alertApi, alertSilenceApi } from '../api/alerts.api'
import type {
  AlertDto,
  AlertSeverity,
  AlertStatus,
  SilenceDto,
  CreateSilenceDto,
  AcknowledgeAlertDto,
} from '../types/alert.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { BusinessError } from '@/shared/http/errors'

const moduleOptions = [
  'Identity', 'AccessControl', 'UserCenter', 'Points', 'Membership', 'Review',
  'AfterSales', 'Product', 'Order', 'Payment', 'Notification', 'Inventory', 'SystemAdmin',
]
const severityOptions: { label: string; value: AlertSeverity }[] = [
  { label: 'critical', value: 'critical' },
  { label: 'warning', value: 'warning' },
  { label: 'info', value: 'info' },
]
const statusOptions: { label: string; value: AlertStatus }[] = [
  { label: 'firing', value: 'firing' },
  { label: 'acknowledged', value: 'acknowledged' },
  { label: 'resolved', value: 'resolved' },
]

const loading = ref(false)
const dataList = ref<AlertDto[]>([])
const total = ref(0)
const filter = reactive<{ module: string[]; severity: AlertSeverity[]; status: AlertStatus[]; range: [string, string] | null; page: number; pageSize: number }>({
  module: [],
  severity: [],
  status: ['firing'],
  range: null,
  page: 1,
  pageSize: 20,
})

const stats = reactive({ pending: 0, critical: 0, todayTotal: 0, avgAckDurationSec: 0 })

const detailVisible = ref(false)
const detail = ref<AlertDto | null>(null)

const ackVisible = ref(false)
const ackTarget = ref<AlertDto | null>(null)
const ackComment = ref('')

const silenceVisible = ref(false)
const silenceForm = reactive<CreateSilenceDto>({
  matchers: [{ name: 'module', value: '', isRegex: false }],
  durationMinutes: 60,
  reason: '',
})

const silenceList = ref<SilenceDto[]>([])
const silenceListVisible = ref(false)

const confirmSilence = ref(false)

let pollTimer: ReturnType<typeof setInterval> | null = null

const columns = computed(() => [
  { title: '告警 ID', dataIndex: 'alertId', key: 'alertId', width: 140, ellipsis: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '模块', dataIndex: 'module', key: 'module', width: 110 },
  { title: '级别', key: 'severity', width: 100 },
  { title: '状态', key: 'status', width: 110 },
  { title: '触发时间', dataIndex: 'triggeredAt', key: 'triggeredAt', width: 160 },
  { title: '持续时长', key: 'duration', width: 110 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
])

function formatDuration(sec: number): string {
  if (sec < 60) return `${sec}s`
  if (sec < 3600) return `${Math.floor(sec / 60)}m`
  return `${Math.floor(sec / 3600)}h ${Math.floor((sec % 3600) / 60)}m`
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      module: filter.module.length ? filter.module : undefined,
      severity: filter.severity.length ? filter.severity : undefined,
      status: filter.status.length ? filter.status : undefined,
      startTime: filter.range?.[0],
      endTime: filter.range?.[1],
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await alertApi.list(params)
    dataList.value = res.items
    total.value = res.total
    stats.pending = res.items.filter((i) => i.status === 'firing').length
    stats.critical = res.items.filter((i) => i.severity === 'critical').length
    stats.todayTotal = res.total
    stats.avgAckDurationSec = res.items.length > 0
      ? Math.floor(res.items.reduce((s, i) => s + i.durationSeconds, 0) / res.items.length)
      : 0
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载告警列表失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

async function openDetail(record: AlertDto) {
  detail.value = record
  detailVisible.value = true
  try {
    detail.value = await alertApi.get(record.alertId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  }
}

function openAck(record: AlertDto) {
  ackTarget.value = record
  ackComment.value = ''
  ackVisible.value = true
}

async function onSubmitAck() {
  if (!ackTarget.value) return
  if (!ackComment.value.trim()) return message.error('注释必填')
  try {
    const body: AcknowledgeAlertDto = { comment: ackComment.value.trim() }
    await alertApi.acknowledge(ackTarget.value.alertId, body)
    message.success('已确认告警')
    ackVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('确认失败')
  }
}

function openSilence() {
  Object.assign(silenceForm, {
    matchers: [{ name: 'module', value: '', isRegex: false }],
    durationMinutes: 60,
    reason: '',
  })
  silenceVisible.value = true
}

function addMatcher() {
  silenceForm.matchers.push({ name: '', value: '', isRegex: false })
}
function removeMatcher(idx: number) {
  if (silenceForm.matchers.length <= 1) return message.warning('至少保留一个匹配器')
  silenceForm.matchers.splice(idx, 1)
}

function askConfirmSilence() {
  if (!silenceForm.reason.trim()) return message.error('静默原因必填')
  if (silenceForm.matchers.some((m) => !m.name.trim() || !m.value.trim())) {
    return message.error('匹配器 name/value 必填')
  }
  confirmSilence.value = true
}

async function onSubmitSilence() {
  try {
    await alertSilenceApi.create({
      matchers: silenceForm.matchers.map((m) => ({ name: m.name.trim(), value: m.value.trim(), isRegex: m.isRegex })),
      durationMinutes: silenceForm.durationMinutes,
      reason: silenceForm.reason.trim(),
    })
    message.success('静默规则已创建')
    confirmSilence.value = false
    silenceVisible.value = false
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('创建静默规则失败')
  }
}

async function openSilenceList() {
  silenceListVisible.value = true
  try {
    silenceList.value = await alertSilenceApi.list()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    silenceList.value = []
  }
}

async function deleteSilence(id: string) {
  try {
    await alertSilenceApi.remove(id)
    message.success('已删除静默规则')
    silenceList.value = await alertSilenceApi.list()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  }
}

function severityIcon(sev: AlertSeverity) {
  return sev === 'critical' ? WarningOutlined : sev === 'warning' ? ExclamationCircleOutlined : InfoCircleOutlined
}
function severityColor(sev: AlertSeverity): string {
  return sev === 'critical' ? '#FF4D4F' : sev === 'warning' ? '#FAAD14' : '#1677FF'
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(() => {
  loadList()
  pollTimer = setInterval(loadList, 30_000)
})
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <div class="runtime-ops-alert">
    <div class="page-header">
      <div class="page-title">告警管理</div>
      <div class="page-desc">查看 Alertmanager 告警事件，按模块与严重级别筛选，处置告警（确认/静默/转工单），追踪闭环。firing 状态每 30s 自动刷新。</div>
    </div>

    <a-alert
      type="info"
      message="告警管理功能规划中，API 待 SystemAdmin BC 实现 Alertmanager 集成"
      show-icon
      style="margin-bottom: 16px"
    />

    <div class="stats-row">
      <a-card size="small"><a-statistic title="待处置告警" :value="stats.pending" :value-style="{ color: '#FAAD14' }" /></a-card>
      <a-card size="small"><a-statistic title="严重告警" :value="stats.critical" :value-style="{ color: '#FF4D4F' }" /></a-card>
      <a-card size="small"><a-statistic title="今日告警总数" :value="stats.todayTotal" /></a-card>
      <a-card size="small"><a-statistic title="平均处置时长" :value="formatDuration(stats.avgAckDurationSec)" /></a-card>
    </div>

    <div class="toolbar">
      <a-select
        v-model:value="filter.module"
        mode="multiple"
        placeholder="模块"
        allow-clear
        style="min-width: 220px"
        :options="moduleOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-select
        v-model:value="filter.severity"
        mode="multiple"
        placeholder="级别"
        allow-clear
        style="min-width: 180px"
        :options="severityOptions"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 200px"
        :options="statusOptions"
      />
      <DateTimeRangePicker v-model="filter.range" />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="alert:manage">
        <a-button @click="openSilenceList">
          <BellOutlined />查看静默规则
        </a-button>
      </PermissionGuard>
      <PermissionGuard permission="alert:manage">
        <a-button type="primary" @click="openSilence">
          <BellOutlined />创建静默规则
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="alertId"
      size="middle"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无告警" action-text="刷新" @action="loadList" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'module'">
          <a-tag>{{ record.module }}</a-tag>
        </template>
        <template v-else-if="column.key === 'severity'">
          <component :is="severityIcon(record.severity)" :style="{ color: severityColor(record.severity), marginRight: '4px' }" />
          <span :style="{ color: severityColor(record.severity), fontWeight: 500 }">{{ record.severity }}</span>
        </template>
        <template v-else-if="column.key === 'status'">
          <StatusTag type="alert" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'duration'">
          {{ formatDuration(record.durationSeconds) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <a-button type="link" size="small" @click="openDetail(record)">详情</a-button>
            <PermissionGuard permission="alert:manage">
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'resolved'"
                @click="openAck(record)"
              >
                确认
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="alert:manage">
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'resolved'"
                @click="openSilence"
              >
                静默
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="detailVisible"
      title="告警详情"
      width="720"
      placement="right"
    >
      <template v-if="detail">
        <a-descriptions :column="1" bordered size="small">
          <a-descriptions-item label="告警 ID"><span class="mono">{{ detail.alertId }}</span></a-descriptions-item>
          <a-descriptions-item label="名称">{{ detail.name }}</a-descriptions-item>
          <a-descriptions-item label="模块">{{ detail.module }}</a-descriptions-item>
          <a-descriptions-item label="级别">
            <component :is="severityIcon(detail.severity)" :style="{ color: severityColor(detail.severity), marginRight: '4px' }" />
            {{ detail.severity }}
          </a-descriptions-item>
          <a-descriptions-item label="状态"><StatusTag type="alert" :status="detail.status" /></a-descriptions-item>
          <a-descriptions-item label="触发时间">{{ detail.triggeredAt }}</a-descriptions-item>
          <a-descriptions-item label="持续时长">{{ formatDuration(detail.durationSeconds) }}</a-descriptions-item>
          <a-descriptions-item label="摘要">{{ detail.summary }}</a-descriptions-item>
          <a-descriptions-item label="描述">{{ detail.description }}</a-descriptions-item>
          <a-descriptions-item v-if="detail.relatedMetric" label="关联指标">
            <span class="mono">{{ detail.relatedMetric }}</span>
          </a-descriptions-item>
        </a-descriptions>

        <div class="section-title">标签（Labels）</div>
        <JsonViewer :data="detail.labels" :max-height="200" />

        <div class="section-title">注释（Annotations）</div>
        <JsonViewer :data="detail.annotations" :max-height="200" />
      </template>
    </a-drawer>

    <a-modal
      v-model:open="ackVisible"
      title="确认告警"
      width="480"
      ok-text="确认"
      cancel-text="取消"
      @ok="onSubmitAck"
    >
      <a-alert
        type="info"
        message="确认后告警状态变为已确认，不再触发通知（除非再次变为 firing）。"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item label="注释" required>
          <a-textarea v-model:value="ackComment" :rows="4" placeholder="请输入确认注释，将记录至审计日志" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="silenceVisible"
      title="创建静默规则"
      width="560"
      ok-text="提交"
      cancel-text="取消"
      @ok="askConfirmSilence"
    >
      <a-form layout="vertical">
        <div class="section-title">匹配器</div>
        <div v-for="(m, idx) in silenceForm.matchers" :key="idx" class="matcher-row">
          <a-input v-model:value="m.name" placeholder="name（如 module）" style="width: 130px" />
          <a-input v-model:value="m.value" placeholder="value（如 Payment）" style="width: 180px" />
          <a-checkbox v-model:checked="m.isRegex">正则</a-checkbox>
          <a-button type="link" danger size="small" @click="removeMatcher(idx)">删除</a-button>
        </div>
        <a-button type="dashed" size="small" @click="addMatcher">+ 新增匹配器</a-button>

        <a-form-item label="持续时长（分钟）" required style="margin-top: 16px">
          <a-input-number v-model:value="silenceForm.durationMinutes" :min="1" :max="1440" style="width: 100%" />
        </a-form-item>
        <a-form-item label="原因" required>
          <a-textarea v-model:value="silenceForm.reason" :rows="3" placeholder="请填写静默原因" />
        </a-form-item>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmSilence"
      title="确认创建静默规则"
      content="静默期间匹配的告警将不再通知，可能遗漏关键事件。请确认静默时长。"
      :danger="true"
      ok-text="确认静默"
      cancel-text="取消"
      @confirm="onSubmitSilence"
    />

    <a-drawer
      v-model:open="silenceListVisible"
      title="静默规则列表"
      width="640"
      placement="right"
    >
      <a-empty v-if="silenceList.length === 0" description="暂无静默规则" />
      <a-list v-else :data-source="silenceList" item-layout="horizontal">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <span class="mono">{{ item.matchers.map((m) => `${m.name}=${m.value}`).join(', ') }}</span>
              </template>
              <template #description>
                持续 {{ item.startsAt }} ~ {{ item.endsAt }}<br />
                原因：{{ item.reason }}<br />
                创建人：{{ item.createdBy }}
              </template>
            </a-list-item-meta>
            <template #actions>
              <a-button type="link" danger size="small" @click="deleteSilence(item.silenceId)">删除</a-button>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-alert .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-alert .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-alert .page-desc { color: #8C8C8C; }
.runtime-ops-alert .stats-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 16px; }
.runtime-ops-alert .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-alert .spacer { flex: 1; }
.runtime-ops-alert .section-title { font-size: 14px; font-weight: 500; margin: 16px 0 8px; }
.runtime-ops-alert .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
.runtime-ops-alert .matcher-row { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/04-runtime-ops/views/AlertManagement.vue
git commit -m "feat(runtime-ops): 实现 AlertManagement.vue（统计+确认/静默弹窗+详情抽屉+30s 轮询+API 规划中提示）"
```

---

## Plan 自检

### 1. Spec 覆盖核对

| Spec / design-prompt 项 | 对应 Task | 状态 |
|-|-|-|
| `04-runtime-ops/rate-limit-rules.md` | Task 1（DTO）+ Task 3（API + 测试）+ Task 9（视图） | ✅ 覆盖 |
| `04-runtime-ops/index-rebuild.md` | Task 1（DTO）+ Task 4（API）+ Task 10（视图） | ✅ 覆盖 |
| `04-runtime-ops/dead-letter-queue.md` | Task 1（DTO）+ Task 2（API + 测试）+ Task 11（视图） | ✅ 覆盖 |
| `04-runtime-ops/scheduled-tasks.md` | Task 1（DTO）+ Task 5（API）+ Task 12（视图） | ✅ 覆盖 |
| `04-runtime-ops/health-monitoring.md` | Task 1（DTO）+ Task 6（API）+ Task 13（视图） | ✅ 覆盖 |
| `04-runtime-ops/alert-management.md` | Task 1（DTO）+ Task 7（API）+ Task 14（视图） | ✅ 覆盖 |
| 模块骨架 routes.ts + index.ts | Task 8 | ✅ 覆盖 |
| 2 个测试文件（dead-letters/rate-limit-rules） | Task 2 / Task 3 | ✅ 覆盖 |

### 2. 占位符扫描

- 全文扫描 `TODO` / `TBD` / `FIXME` / `未实现` / `省略` / `...` 均为 0 处出现于代码块中。
- 注释类占位（如「此处省略」「保持不变」）0 处。
- 所有视图、API、DTO 均提供完整可编译代码。

### 3. 类型一致性

- `DeadLetterMessageDto.history: DeadLetterHistoryItemDto[]` 在 Task 1 与 Task 11 视图一致使用。
- `BatchOperationResultDto.succeeded: string[]` / `failed: { messageId, reason }[]` 在 Task 1 与 Task 11 一致。
- `SaveRateLimitRuleDto.version?: number` 在 Task 1 与 Task 3 update 调用一致（`body.version ?? 0`）。
- `IndexRebuildStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed'` 在 Task 1、Task 4、Task 10 一致。
- `ScheduledTaskStatus = 'Enabled' | 'Disabled'` 在 Task 1、Task 5、Task 12 一致。
- `HealthAggregationResultDto.overallStatus` / `ModuleHealthDto.status` / `DependencyHealthDto.status` 在 Task 1、Task 6、Task 13 一致。
- `AlertDto.severity` / `AlertDto.status` 在 Task 1、Task 7、Task 14 一致。
- `alertApi` + `alertSilenceApi` 双对象导出在 Task 7 与 Task 14 视图 import 一致。
- 路由项 6 条 path/name 与 Task 8 routes.ts 完全对应：`rate-limit-rules` / `index-rebuild` / `dead-letter-queue` / `scheduled-tasks` / `health-monitoring` / `alert-management`。

### 4. 文件路径一致性

- 所有 Task 引用的文件路径与 File Structure 列表完全对应。
- 路由项 `component: () => import('../views/Xxx.vue')` 与 Task 9-14 创建的视图文件名一一对应。
- `routes.ts` 引用的 6 个视图均在本 plan 范围内。

### 5. design-prompt 字段覆盖

- 限流规则: targetApi/targetContext/limit/windowSeconds/algorithm/scope/enabled/version 8 字段 + enable/disable 2 端点 ✅
- 索引重建: taskId/targetContext/indexName/status/triggeredBy/triggeredAt/totalDocs/processedDocs/errorMessage/retryCount 10 字段 + trigger/retry 2 端点 ✅
- 死信队列: messageId/originalMessageId/sourceContext/originalTopic/originalQueue/payload/headers/errorReason/retryCount/status/discardReason 11 字段 + retry/discard/batchRetry/batchDiscard 4 端点 ✅
- 定时任务: taskId/name/jobType/cronExpression/parameters/status/lastRunAt/nextRunAt 8 字段 + create/update/enable/disable/runNow/getHistory 6 端点 ✅
- 健康监控: overallStatus/checkedAt/modules + moduleName/status/latencyMs/dependencies + name/status/latencyMs/error/lastCheckedAt 全字段 + getAggregated/getModules 2 端点 ✅
- 告警管理: alertId/name/module/severity/status/triggeredAt/durationSeconds/labels/annotations/summary/description/relatedMetric 11 字段 + acknowledge + silences list/create/delete 3 端点 ✅

### 6. 危险操作确认流程覆盖

| 危险操作 | ConfirmDialog | danger | requireInput |
|-|-|-|-|
| 限流规则停用（启用中） | Task 9 | ✅ true | — |
| 限流规则更新（乐观锁冲突） | 自动重新加载 | — | — |
| 索引重建触发 | Task 10（ConfirmDialog 内表单） | false | indexName 必填 |
| 索引重建重试（仅失败态） | Task 10 | false | — |
| 死信单条/批量重投 | Task 11 | false | — |
| 死信单条/批量丢弃 | Task 11 | true | 丢弃原因必填（min 1, max 500） |
| 定时任务停用（启用中） | Task 12 | true | — |
| 定时任务立即执行 | Task 12 | false | — |
| 告警确认 | Task 14（弹窗） | — | 注释必填 |
| 告警静默规则创建 | Task 14（ConfirmDialog） | true | matchers + reason 必填 |
| 静默规则删除 | Task 14（列表项） | — | — |

所有写操作均通过对应 API 注入 `Idempotency-Key` 头（在 Task 2-7 API 实现中使用 `withIdempotency()`），符合 spec §3.3 与 §5.7 要求。

---

## 任务清单汇总

- Task 1: 6 个 DTO 类型定义文件
- Task 2: dead-letters API + 单元测试（TDD）
- Task 3: rate-limit-rules API + 单元测试（TDD）
- Task 4: index-rebuilds API
- Task 5: scheduled-tasks API
- Task 6: health API
- Task 7: alerts API
- Task 8: routes.ts + index.ts 模块聚合
- Task 9: RateLimitRules.vue 限流规则视图
- Task 10: IndexRebuild.vue 索引重建视图
- Task 11: DeadLetterQueue.vue 死信队列视图
- Task 12: ScheduledTasks.vue 定时任务视图
- Task 13: HealthMonitoring.vue 健康监控视图
- Task 14: AlertManagement.vue 告警管理视图

**Task 总数：14**

**执行建议:** 按 Task 1 → 8 → 2 → 3 → 4 → 5 → 6 → 7 → 9 → 10 → 11 → 12 → 13 → 14 顺序执行；Task 2/3 严格走 TDD（先 spec 后实现）。每个 Task 完成后立即按其 commit step 提交。
