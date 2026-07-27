# 系统管理后台 05-audit 模块实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 `web/system-admin/src/modules/05-audit/` 模块的全部 3 页（审计日志、对账管理、Outbox 监控）+ 模块骨架（types/api/routes/index）+ 1 个 API 单元测试（audit-logs.api.spec.ts），覆盖只读审计日志（含敏感参数字段掩码）、对账状态看板与触发、Outbox 积压监控。

**Architecture:** 按 DTO → API（audit-logs 走 TDD：先 spec 后实现）→ routes/index → Vue 视图顺序推进，每 Task 自包含、可独立编译/测试/提交。审计日志页严格只读（无任何写端点），详情抽屉中 `RequestSummary`/`BeforeSnapshot`/`AfterSnapshot` 的 JSON 字段对 `password|token|secret|apiKey|credential|authorization` 等敏感键做 `******` 掩码展示；对账页触发操作走 `IdempotencyButton` + `ConfirmDialog`；Outbox 页重投/归档走 `ConfirmDialog`（归档需填理由，danger 红色）。跨 Plan 类型契约严格遵守 §shared/types、§shared/http、§shared/auth、§shared/components 已定义。

**Tech Stack:** Vue 3.5 + `<script setup>` + TS strict + Vite 6 + Ant Design Vue 4 + Pinia 2 + Vue Router 4 + axios 1.7 + @vue-echarts/echarts + Vitest 2 + @vue/test-utils 2 + jsdom

**Spec 来源:** [docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md](file:///workspace/docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md)

**跨 Plan 契约（本 plan 严格遵守）:**
- `shared/types/index.ts`: `ApiResponse<T>` / `PageResult<T>` / `PageQuery`
- `shared/http/client.ts`: `client`（baseURL `/api`, timeout 15000）+ `withIdempotency()`
- `shared/http/errors.ts`: `BusinessError` / `NotFoundError` / `ServerError`
- `shared/auth/auth.store.ts`: `useAuthStore`（getters: `isAuthenticated`/`isAdmin`/`hasPermission`；actions: `login`/`fetchProfile`/`logout`/`hasRole`）
- `shared/components/*`（12 个，Plan 1 已实现）: StatusTag / IdempotencyButton / PermissionGuard / DataTable / EmptyState / ConfirmDialog / DateTimeRangePicker / ChartLine / ChartBar / ChartPie / JsonViewer / ErrorBoundary
- 命名约定: 视图 PascalCase `.vue`；API 对象 camelCase 动词开头，导出形如 `auditLogsApi`/`reconciliationApi`/`outboxMonitorApi`；DTO PascalCase + `Dto` 后缀；路由 name `audit.{view}` kebab-case；路由 path kebab-case

---

## File Structure

**模块骨架（types/api/routes/index）:**
- `web/system-admin/src/modules/05-audit/types/audit-log.dto.ts` — 审计日志 + 操作日志 + 跨域审计条目 DTO + 查询参数
- `web/system-admin/src/modules/05-audit/types/reconciliation.dto.ts` — 对账状态 + 记录 + 差异项 DTO + 枚举
- `web/system-admin/src/modules/05-audit/types/outbox.dto.ts` — Outbox 汇总 + 趋势 + 消息 + 归档 DTO + 枚举
- `web/system-admin/src/modules/05-audit/api/audit-logs.api.ts` — `auditLogsApi`（list/get/export/listOperationLogs/listAuditLogEntries）只读
- `web/system-admin/src/modules/05-audit/api/reconciliation.api.ts` — `reconciliationApi`（getStatus/listRecords/trigger）触发幂等
- `web/system-admin/src/modules/05-audit/api/outbox-monitor.api.ts` — `outboxMonitorApi`（getSummary/getTrend/listMessages/republish/archive/getArchiveHistory）重投/归档幂等
- `web/system-admin/src/modules/05-audit/routes.ts` — 3 个路由项（path/name/meta.title/menuKey/icon/roles/permission/menuGroup）
- `web/system-admin/src/modules/05-audit/index.ts` — 导出 routes + 各 api 对象

**3 个视图:**
- `web/system-admin/src/modules/05-audit/views/AuditLogs.vue` — 3 Tab + 筛选 + 表格 + 详情抽屉（敏感字段掩码）+ 导出
- `web/system-admin/src/modules/05-audit/views/Reconciliation.vue` — 4 状态卡片 + 触发对账 + 历史表格 + 详情抽屉
- `web/system-admin/src/modules/05-audit/views/OutboxMonitor.vue` — 统计条 + 趋势折线 + 按域表格 + 详情抽屉 + 重投/归档

**测试:**
- `web/system-admin/src/modules/05-audit/api/audit-logs.api.spec.ts` — URL/方法/参数/responseType 断言（5 个测试用例）

**依赖确认（spec §10 + design-prompt §3）:**
- 审计日志 5 个端点已实装（design-prompt 标 ✅ 已实现）：`GET /api/admin/audit-logs` / `/{id}` / `/export` / `/api/admin/operation-logs` / `/api/admin/audit-log-entries`
- 对账 3 个端点已实装（design-prompt 标 ✅ 已实现）：`GET /api/admin/statistics/reconciliation-status` / `POST /api/admin/statistics/reconcile` / `GET /api/admin/statistics/reconciliation-records`
- Outbox 6 个端点标 🚧 规划中（design-prompt 标记待实现）：API 层与视图骨架仍按 design-prompt §3 完整实现，视图顶部展示 `<a-alert type="info">` 提示「后端 Outbox 监控端点规划中，数据可能为空」，便于后端就绪即用

---

## Task 1: 创建 3 个 DTO 类型定义文件

**Files:**
- Create: `web/system-admin/src/modules/05-audit/types/audit-log.dto.ts`
- Create: `web/system-admin/src/modules/05-audit/types/reconciliation.dto.ts`
- Create: `web/system-admin/src/modules/05-audit/types/outbox.dto.ts`

- [ ] **Step 1: 创建 audit-log.dto.ts**

```typescript
// web/system-admin/src/modules/05-audit/types/audit-log.dto.ts
// 审计日志 + 操作日志 + 跨域审计条目 DTO，对齐 SystemAdmin BC AuditLogsController 契约
// 审计日志只读，不可编辑；详情含 BeforeSnapshot/AfterSnapshot/RequestSummary，前端对敏感键掩码展示

/** 操作人角色（用于行/详情着色） */
export type OperatorRole = 'Admin' | 'Operator' | 'Seller' | 'Buyer' | 'System'

/** 审计日志条目响应 DTO（design-prompt §3） */
export interface AuditLogEntryDto {
  /** 日志 ID */
  logId: string
  /** 操作人 ID */
  operatorId: string
  /** 操作人名称 */
  operatorName: string
  /** 操作人角色 */
  operatorRole: OperatorRole
  /** 来源上下文（限界上下文名，如 Order/Payment） */
  sourceContext: string
  /** 操作动作（如 Create/Update/Delete/Login/Export） */
  action: string
  /** 资源类型（如 Shop/Role/DeadLetter/Reconciliation） */
  resourceType: string
  /** 资源 ID */
  resourceId: string
  /** 请求摘要（含 path/method/query，可能含敏感参数，前端掩码展示） */
  requestSummary: string
  /** HTTP 响应状态码（200/403/500 等） */
  responseStatus: number
  /** 客户端 IP */
  ipAddress: string
  /** User-Agent */
  userAgent: string
  /** 链路追踪 ID */
  traceId: string
  /** 操作前快照（JSON 字符串，可能含敏感字段，前端掩码展示） */
  beforeSnapshot: string | null
  /** 操作后快照（JSON 字符串，可能含敏感字段，前端掩码展示） */
  afterSnapshot: string | null
  /** 发生时间（ISO 8601 UTC） */
  occurredAt: string
}

/** 操作日志条目响应 DTO（design-prompt §3 operation-logs） */
export interface OperationLogDto {
  /** 日志 ID */
  logId: string
  /** 操作人 ID */
  operatorId: string
  /** 操作人名称 */
  operatorName: string
  /** 操作人角色 */
  operatorRole: OperatorRole
  /** 所属模块（如 Order/Payment/Identity） */
  module: string
  /** 操作动作 */
  action: string
  /** 资源类型 */
  resourceType: string
  /** 资源 ID */
  resourceId: string
  /** 操作详情（人类可读） */
  detail: string
  /** IP 地址 */
  ipAddress: string
  /** 链路追踪 ID */
  traceId: string
  /** 发生时间（ISO 8601 UTC） */
  occurredAt: string
}

/** 跨域审计条目响应 DTO（design-prompt §3 audit-log-entries） */
export interface CrossDomainAuditEntryDto {
  /** 条目 ID */
  entryId: string
  /** 限界上下文/模块 */
  module: string
  /** 操作动作 */
  action: string
  /** 操作人 ID */
  operatorId: string
  /** 操作人名称 */
  operatorName: string
  /** 资源类型 */
  resourceType: string
  /** 资源 ID */
  resourceId: string
  /** 链路追踪 ID */
  traceId: string
  /** 发生时间（ISO 8601 UTC） */
  occurredAt: string
}

/** 审计日志列表查询参数（design-prompt §3 请求参数） */
export interface ListAuditLogsParams {
  operatorId?: string
  resourceType?: string
  action?: string
  fromTime?: string
  toTime?: string
  page?: number
  pageSize?: number
}

/** 操作日志列表查询参数 */
export interface ListOperationLogsParams {
  operatorId?: string
  module?: string
  fromTime?: string
  toTime?: string
  page?: number
  pageSize?: number
}

/** 跨域审计条目列表查询参数 */
export interface ListAuditLogEntriesParams {
  module?: string
  action?: string
  operatorId?: string
  fromTime?: string
  toTime?: string
  page?: number
  pageSize?: number
}

/** 导出审计日志查询参数（不分页） */
export interface ExportAuditLogsParams {
  operatorId?: string
  resourceType?: string
  action?: string
  fromTime?: string
  toTime?: string
}
```

- [ ] **Step 2: 创建 reconciliation.dto.ts**

```typescript
// web/system-admin/src/modules/05-audit/types/reconciliation.dto.ts
// 对账状态 + 记录 + 差异项 DTO 与枚举，对齐 SystemAdmin BC StatisticsReconciliationService 契约

/** 对账报表类型（design-prompt §2 区域 B） */
export type ReconciliationReportType =
  | 'OrderGmv'
  | 'PaymentSuccessRate'
  | 'PointsIssued'
  | 'NotificationDelivery'
  | 'AfterSalesVolume'
  | 'ShopRanking'
  | 'ConversionRate'

/** 对账状态：一致 / 有差异 / 失败（design-prompt §4 状态机） */
export type ReconciliationStatus = 'Consistent' | 'Discrepancy' | 'Failed'

/** 对账状态汇总 DTO（顶部 4 个统计卡片数据源，design-prompt §3） */
export interface ReconciliationStatusDto {
  /** 是否已执行过对账 */
  hasRun: boolean
  /** 最近一次对账状态 */
  status: ReconciliationStatus | null
  /** 最近一次对账的报表类型（全部对账时为 null） */
  reportType: ReconciliationReportType | null
  /** 最近一次对账时间（ISO 8601 UTC） */
  reconciledAt: string | null
  /** 差异项数量 */
  discrepancyCount: number
  /** 是否一致 */
  isConsistent: boolean
  /** 是否触发告警 */
  alertTriggered: boolean
  /** 是否触发修正 */
  correctionTriggered: boolean
}

/** 对账差异项明细 DTO（详情抽屉列表展示） */
export interface ReconciliationDiscrepancyDto {
  /** 报表类型 */
  reportType: ReconciliationReportType
  /** 指标名（如 OrderGmv/PaymentSuccess/PointsIssued） */
  metricName: string
  /** 期望值 */
  expectedValue: number
  /** 实际值 */
  actualValue: number
  /** 差异值（actual - expected） */
  diffValue: number
}

/** 对账记录响应 DTO（design-prompt §3） */
export interface ReconciliationRecordDto {
  /** 记录 ID */
  recordId: string
  /** 报表类型 */
  reportType: ReconciliationReportType
  /** 对账时间（ISO 8601 UTC） */
  reconciledAt: string
  /** 对账状态 */
  status: ReconciliationStatus
  /** 差异项数量 */
  discrepancyCount: number
  /** 是否触发告警 */
  alertTriggered: boolean
  /** 是否触发修正 */
  correctionTriggered: boolean
  /** 错误信息（对账失败时非空） */
  errorMessage: string | null
  /** 差异项明细列表（详情视图展开时由后端填充，列表查询时可能为空数组） */
  discrepancies: ReconciliationDiscrepancyDto[]
}

/** 触发对账请求参数（query 参数形式，design-prompt §3） */
export interface TriggerReconciliationParams {
  /** 报表类型，未传则对账全部类型 */
  reportType?: ReconciliationReportType
  /** 起始时间（ISO 8601 UTC） */
  start?: string
  /** 结束时间（ISO 8601 UTC） */
  end?: string
}

/** 对账记录列表查询参数 */
export interface ListReconciliationRecordsParams {
  reportType?: ReconciliationReportType
  start?: string
  end?: string
  page?: number
  pageSize?: number
}
```

- [ ] **Step 3: 创建 outbox.dto.ts**

```typescript
// web/system-admin/src/modules/05-audit/types/outbox.dto.ts
// Outbox 汇总 + 趋势 + 消息 + 归档 DTO 与枚举，对齐 SystemAdmin BC OutboxMonitorController 契约
// 注：design-prompt 标 🚧 规划中，端点待后端实现；DTO 与 API 层先按 design-prompt §3 完整定义

/** Outbox 域状态：正常 / 积压 / 严重积压 / 已归档（design-prompt §4 状态机） */
export type OutboxStatus = 'Normal' | 'Backlog' | 'Severe' | 'Archived'

/** Outbox 域积压汇总 DTO（按域分组表格数据源，design-prompt §3） */
export interface OutboxSummaryDto {
  /** 限界上下文（如 Order/Payment/Notification） */
  context: string
  /** 未发布事件数 */
  pendingCount: number
  /** 最早事件时间（ISO 8601 UTC） */
  oldestEventAt: string | null
  /** 最大积压时长（分钟） */
  maxAgeMinutes: number
  /** 最近归档时间（ISO 8601 UTC） */
  lastArchivedAt: string | null
  /** 域状态 */
  status: OutboxStatus
}

/** Outbox 积压趋势点 DTO（按时间×上下文，design-prompt §3） */
export interface OutboxTrendPointDto {
  /** 时间戳（ISO 8601 UTC） */
  timestamp: string
  /** 限界上下文 */
  context: string
  /** 该时刻积压事件数 */
  pendingCount: number
}

/** Outbox 积压事件消息 DTO（详情抽屉列表，design-prompt §3） */
export interface OutboxMessageDto {
  /** 事件 ID */
  messageId: string
  /** 聚合 ID */
  aggregateId: string
  /** 事件类型 */
  eventType: string
  /** 事件 Payload（JSON 字符串） */
  payload: string
  /** 创建时间（ISO 8601 UTC） */
  createdAt: string
  /** 重试次数 */
  retryCount: number
  /** 消息状态 */
  status: OutboxStatus
}

/** Outbox 归档历史条目 DTO */
export interface OutboxArchiveHistoryDto {
  /** 归档时间（ISO 8601 UTC） */
  archivedAt: string
  /** 归档事件数 */
  count: number
  /** 归档原因 */
  reason: string
  /** 操作人 */
  archivedBy: string
}

/** 批量重投请求 DTO（design-prompt §3 BatchRepublishDto） */
export interface BatchRepublishOutboxDto {
  /** 指定消息 ID 列表；为空则重投该域全部积压 */
  messageIds?: string[]
  /** 最大重投条数（不指定 messageIds 时生效） */
  maxCount?: number
}

/** 归档请求 DTO（design-prompt §3 ArchiveDto） */
export interface ArchiveOutboxDto {
  /** 归档阈值：积压时长超过此分钟数的事件归档 */
  olderThanMinutes: number
  /** 归档原因（必填） */
  reason: string
}

/** Outbox 积压趋势查询参数 */
export interface GetOutboxTrendParams {
  /** 趋势时间窗口（小时，默认 24） */
  hours?: number
}

/** Outbox 消息列表查询参数 */
export interface ListOutboxMessagesParams {
  /** 限界上下文（路径参数） */
  context: string
  page?: number
  pageSize?: number
}
```

- [ ] **Step 4: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（types 仅声明，无未定义引用）

```bash
git add web/system-admin/src/modules/05-audit/types/
git commit -m "feat(audit): 新增 3 个 DTO 类型定义文件（audit-log/reconciliation/outbox）"
```

---

## Task 2: audit-logs API（TDD：先写测试 → 实现 → 通过）

**Files:**
- Create: `web/system-admin/src/modules/05-audit/api/audit-logs.api.spec.ts`
- Create: `web/system-admin/src/modules/05-audit/api/audit-logs.api.ts`

- [ ] **Step 1: 编写失败测试 audit-logs.api.spec.ts**

```typescript
// web/system-admin/src/modules/05-audit/api/audit-logs.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { auditLogsApi } from './audit-logs.api'
import type {
  ListAuditLogsParams,
  ListOperationLogsParams,
  ListAuditLogEntriesParams,
  ExportAuditLogsParams,
} from '../types/audit-log.dto'

// 统一 mock shared/http 模块，client.get 替换为 spy（审计日志只读，无 post/put/delete）
vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: { get: vi.fn() },
    withIdempotency: actual.withIdempotency,
  }
})

describe('auditLogsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 /admin/audit-logs + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListAuditLogsParams = {
      operatorId: 'u-1',
      resourceType: 'Shop',
      action: 'Create',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
      page: 1,
      pageSize: 20,
    }
    await auditLogsApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/audit-logs', { params })
  })

  it('get 使用 /admin/audit-logs/{id} 路径', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await auditLogsApi.get('log-1')
    expect(client.get).toHaveBeenCalledWith('/admin/audit-logs/log-1')
  })

  it('export 使用 responseType: blob 与导出参数', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: new Blob() })
    const params: ExportAuditLogsParams = {
      resourceType: 'Shop',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
    }
    await auditLogsApi.export(params)
    expect(client.get).toHaveBeenCalledWith('/admin/audit-logs/export', {
      params,
      responseType: 'blob',
    })
  })

  it('listOperationLogs 使用 /admin/operation-logs + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListOperationLogsParams = {
      operatorId: 'u-1',
      module: 'Order',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
      page: 1,
      pageSize: 20,
    }
    await auditLogsApi.listOperationLogs(params)
    expect(client.get).toHaveBeenCalledWith('/admin/operation-logs', { params })
  })

  it('listAuditLogEntries 使用 /admin/audit-log-entries + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListAuditLogEntriesParams = {
      module: 'Order',
      action: 'Create',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
      page: 1,
      pageSize: 20,
    }
    await auditLogsApi.listAuditLogEntries(params)
    expect(client.get).toHaveBeenCalledWith('/admin/audit-log-entries', { params })
  })
})
```

- [ ] **Step 2: 运行测试验证失败**

Run: `cd web/system-admin && pnpm test src/modules/05-audit/api/audit-logs.api.spec.ts`
Expected: FAIL — `Cannot find module './audit-logs.api'`

- [ ] **Step 3: 实现 audit-logs.api.ts**

```typescript
// web/system-admin/src/modules/05-audit/api/audit-logs.api.ts
// 审计日志 API：对齐 SystemAdmin BC AuditLogsController 端点
// 全部只读（GET），无写操作，不注入 Idempotency-Key
// 导出走 responseType: 'blob'，文件名从 Content-Disposition 解析（在视图层完成）

import { client } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  AuditLogEntryDto,
  OperationLogDto,
  CrossDomainAuditEntryDto,
  ListAuditLogsParams,
  ListOperationLogsParams,
  ListAuditLogEntriesParams,
  ExportAuditLogsParams,
} from '../types/audit-log.dto'

/** 审计日志列表请求（合并分页） */
export type ListAuditLogsRequest = ListAuditLogsParams & PageQuery

/** 操作日志列表请求（合并分页） */
export type ListOperationLogsRequest = ListOperationLogsParams & PageQuery

/** 跨域审计条目列表请求（合并分页） */
export type ListAuditLogEntriesRequest = ListAuditLogEntriesParams & PageQuery

export const auditLogsApi = {
  /** 分页查询审计日志（按操作人/资源类型/动作/时间） */
  list: (params: ListAuditLogsRequest) =>
    client.get<PageResult<AuditLogEntryDto>>('/admin/audit-logs', { params }),

  /** 获取审计日志条目详情（含前后快照 JSON） */
  get: (id: string) =>
    client.get<AuditLogEntryDto>(`/admin/audit-logs/${id}`),

  /** 导出审计日志为 CSV（blob，文件名由视图层从 Content-Disposition 解析） */
  export: (params: ExportAuditLogsParams) =>
    client.get<Blob>('/admin/audit-logs/export', { params, responseType: 'blob' }),

  /** 分页查询操作日志（按操作人/模块/时间） */
  listOperationLogs: (params: ListOperationLogsRequest) =>
    client.get<PageResult<OperationLogDto>>('/admin/operation-logs', { params }),

  /** 分页查询跨域审计条目（按模块/动作/操作人/时间） */
  listAuditLogEntries: (params: ListAuditLogEntriesRequest) =>
    client.get<PageResult<CrossDomainAuditEntryDto>>('/admin/audit-log-entries', { params }),
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `cd web/system-admin && pnpm test src/modules/05-audit/api/audit-logs.api.spec.ts`
Expected: PASS — 5 tests passed

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/05-audit/api/audit-logs.api.ts web/system-admin/src/modules/05-audit/api/audit-logs.api.spec.ts
git commit -m "feat(audit): 实现 auditLogsApi 只读端点与单元测试（list/get/export/operation-logs/entries）"
```

---

## Task 3: reconciliation API

**Files:**
- Create: `web/system-admin/src/modules/05-audit/api/reconciliation.api.ts`

- [ ] **Step 1: 实现 reconciliation.api.ts**

```typescript
// web/system-admin/src/modules/05-audit/api/reconciliation.api.ts
// 对账管理 API：对齐 SystemAdmin BC StatisticsReconciliationService 端点
// 触发对账（POST）注入 Idempotency-Key 头；查询接口只读

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  ReconciliationStatusDto,
  ReconciliationRecordDto,
  TriggerReconciliationParams,
  ListReconciliationRecordsParams,
} from '../types/reconciliation.dto'

/** 对账记录列表请求（合并分页） */
export type ListReconciliationRecordsRequest = ListReconciliationRecordsParams & PageQuery

export const reconciliationApi = {
  /** 获取最近一次对账状态（顶部 4 个统计卡片数据源） */
  getStatus: () =>
    client.get<ReconciliationStatusDto>('/admin/statistics/reconciliation-status'),

  /** 分页查询对账记录列表（按报表类型与时间范围） */
  listRecords: (params: ListReconciliationRecordsRequest) =>
    client.get<PageResult<ReconciliationRecordDto>>('/admin/statistics/reconciliation-records', { params }),

  /** 手动触发对账（按报表类型与时间范围，幂等）
   *  reportType 未传则对账全部类型，返回多条记录；指定类型返回单条记录数组（长度 1）
   */
  trigger: (params: TriggerReconciliationParams) =>
    client.post<ReconciliationRecordDto[]>(
      '/admin/statistics/reconcile',
      null,
      { params, ...withIdempotency() },
    ),
}
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/05-audit/api/reconciliation.api.ts
git commit -m "feat(audit): 实现 reconciliationApi（getStatus/listRecords/trigger 含幂等键）"
```

---

## Task 4: outbox-monitor API

**Files:**
- Create: `web/system-admin/src/modules/05-audit/api/outbox-monitor.api.ts`

- [ ] **Step 1: 实现 outbox-monitor.api.ts**

```typescript
// web/system-admin/src/modules/05-audit/api/outbox-monitor.api.ts
// Outbox 监控 API：对齐 SystemAdmin BC OutboxMonitorController 端点
// design-prompt 标 🚧 规划中，端点待后端实现；API 层先按 design-prompt §3 完整定义
// 重投（republish）与归档（archive）注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  OutboxSummaryDto,
  OutboxTrendPointDto,
  OutboxMessageDto,
  OutboxArchiveHistoryDto,
  BatchRepublishOutboxDto,
  ArchiveOutboxDto,
  GetOutboxTrendParams,
  ListOutboxMessagesParams,
} from '../types/outbox.dto'

/** Outbox 消息列表请求（合并分页） */
export type ListOutboxMessagesRequest = ListOutboxMessagesParams & PageQuery

/** 批量重投结果 DTO */
export interface BatchRepublishResultDto {
  /** 成功重投的消息 ID 列表 */
  succeeded: string[]
  /** 失败明细 */
  failed: { messageId: string; reason: string }[]
}

/** 归档结果 DTO */
export interface ArchiveOutboxResultDto {
  /** 实际归档事件数 */
  archivedCount: number
  /** 归档时间（ISO 8601 UTC） */
  archivedAt: string
}

export const outboxMonitorApi = {
  /** 获取各域 Outbox 积压汇总（按域分组表格数据源） */
  getSummary: () =>
    client.get<OutboxSummaryDto[]>('/admin/outbox/summary'),

  /** 获取近 N 小时积压趋势（按域分系列，默认 24 小时） */
  getTrend: (params: GetOutboxTrendParams) =>
    client.get<OutboxTrendPointDto[]>('/admin/outbox/trend', { params }),

  /** 分页查询指定域积压事件详情（详情抽屉列表） */
  listMessages: (params: ListOutboxMessagesRequest) =>
    client.get<PageResult<OutboxMessageDto>>(`/admin/outbox/${params.context}/messages`, {
      params: { page: params.page, pageSize: params.pageSize },
    }),

  /** 批量重投指定域积压事件（幂等） */
  republish: (context: string, body: BatchRepublishOutboxDto) =>
    client.post<BatchRepublishResultDto>(
      `/admin/outbox/${context}/republish`,
      body,
      withIdempotency(),
    ),

  /** 归档指定域陈旧积压事件（幂等） */
  archive: (context: string, body: ArchiveOutboxDto) =>
    client.post<ArchiveOutboxResultDto>(
      `/admin/outbox/${context}/archive`,
      body,
      withIdempotency(),
    ),

  /** 查询指定域归档历史 */
  getArchiveHistory: (context: string) =>
    client.get<OutboxArchiveHistoryDto[]>(`/admin/outbox/${context}/archive-history`),
}
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/05-audit/api/outbox-monitor.api.ts
git commit -m "feat(audit): 实现 outboxMonitorApi（summary/trend/messages/republish/archive/history 含幂等键）"
```

---

## Task 5: routes.ts + index.ts 模块聚合

**Files:**
- Create: `web/system-admin/src/modules/05-audit/routes.ts`
- Create: `web/system-admin/src/modules/05-audit/index.ts`

- [ ] **Step 1: 实现 routes.ts**

```typescript
// web/system-admin/src/modules/05-audit/routes.ts
// 05-audit 模块路由项：3 个视图，meta 含 title/menuKey/icon/roles/permission/menuGroup
import type { RouteRecordRaw } from 'vue-router'

export const auditRoutes: RouteRecordRaw[] = [
  {
    path: 'audit-logs',
    name: 'audit.audit-logs',
    component: () => import('../views/AuditLogs.vue'),
    meta: {
      title: '审计日志',
      menuKey: 'audit.audit-logs',
      icon: 'FileSearchOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'audit-log:read',
      menuGroup: '05-audit',
    },
  },
  {
    path: 'reconciliation',
    name: 'audit.reconciliation',
    component: () => import('../views/Reconciliation.vue'),
    meta: {
      title: '对账管理',
      menuKey: 'audit.reconciliation',
      icon: 'AuditOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'reconciliation:trigger',
      menuGroup: '05-audit',
    },
  },
  {
    path: 'outbox-monitor',
    name: 'audit.outbox-monitor',
    component: () => import('../views/OutboxMonitor.vue'),
    meta: {
      title: 'Outbox 监控',
      menuKey: 'audit.outbox-monitor',
      icon: 'InboxOutlined',
      roles: ['Admin'],
      permission: 'outbox:manage',
      menuGroup: '05-audit',
    },
  },
]

export default auditRoutes
```

- [ ] **Step 2: 实现 index.ts**

```typescript
// web/system-admin/src/modules/05-audit/index.ts
// 模块对外出口：routes + 各 api 对象
export { default as auditRoutes } from './routes'
export { auditLogsApi } from './api/audit-logs.api'
export { reconciliationApi } from './api/reconciliation.api'
export { outboxMonitorApi } from './api/outbox-monitor.api'
```

- [ ] **Step 3: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（routes.ts 引用 views/*.vue 文件尚不存在，vue-tsc 对动态 import 容忍；若报错需先创建空 .vue 占位）

```bash
git add web/system-admin/src/modules/05-audit/routes.ts web/system-admin/src/modules/05-audit/index.ts
git commit -m "feat(audit): 新增 routes.ts（3 路由项）与 index.ts 模块出口"
```

---

## Task 6: AuditLogs.vue 审计日志视图（只读 + 敏感字段掩码）

**Files:**
- Create: `web/system-admin/src/modules/05-audit/views/AuditLogs.vue`

**实现要点（design-prompt §1-8）:**
- 顶部 `<a-tabs>` 3 个 Tab：审计日志 / 操作日志 / 跨域审计条目
- 筛选条：操作人输入 + 资源类型/模块下拉 + 动作输入 + `DateTimeRangePicker` + 「查询」+「导出 CSV」（导出按钮 `PermissionGuard permission="audit-log:export"`）
- 主表格：日志ID/操作人/角色/来源上下文/动作/资源类型/资源ID/响应状态/IP/发生时间/操作（详情）
- 详情抽屉：`<a-drawer width="720">` 内 `<a-descriptions>` 展示结构化字段 + `JsonViewer` 展示 `BeforeSnapshot`/`AfterSnapshot`/`RequestSummary`（**敏感字段掩码**）
- 响应状态码 `<a-tag>` 着色：2xx 绿、4xx 黄、5xx 红
- 操作人角色 `<a-tag>` 着色：Admin 红、Operator 蓝、Seller 绿、Buyer 灰、System 紫
- 严格只读：无任何写按钮、无编辑、无删除
- 默认时间范围近 24 小时；导出文件名 `audit-logs-{yyyyMMddHHmmss}.csv`
- 跨页面跳转：路由 query `resourceType` 自动回填筛选
- 空状态：`EmptyState` CTA「清空筛选条件」

- [ ] **Step 1: 实现 AuditLogs.vue**

```vue
<!-- web/system-admin/src/modules/05-audit/views/AuditLogs.vue -->
<!-- 审计日志：3 Tab + 筛选 + 表格 + 详情抽屉（敏感字段掩码）+ 导出 CSV，严格只读 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  FileSearchOutlined, DownloadOutlined, SearchOutlined, EyeOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { auditLogsApi } from '../api/audit-logs.api'
import type {
  AuditLogEntryDto,
  OperationLogDto,
  CrossDomainAuditEntryDto,
  OperatorRole,
} from '../types/audit-log.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'

type TabKey = 'audit-logs' | 'operation-logs' | 'cross-domain-entries'

interface AuditFilterState {
  operatorId: string
  resourceType: string
  action: string
  timeRange: [string, string] | null
  page: number
  pageSize: number
}

interface OperationFilterState {
  operatorId: string
  module: string
  timeRange: [string, string] | null
  page: number
  pageSize: number
}

interface EntryFilterState {
  module: string
  action: string
  operatorId: string
  timeRange: [string, string] | null
  page: number
  pageSize: number
}

const route = useRoute()
const activeTab = ref<TabKey>('audit-logs')

// 默认近 24 小时（ISO 8601 UTC）
function defaultRange(): [string, string] {
  return [
    dayjs().subtract(24, 'hour').toISOString(),
    dayjs().toISOString(),
  ]
}

const auditFilter = reactive<AuditFilterState>({
  operatorId: '',
  resourceType: (route.query.resourceType as string) || '',
  action: '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})
const operationFilter = reactive<OperationFilterState>({
  operatorId: '',
  module: '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})
const entryFilter = reactive<EntryFilterState>({
  module: '',
  action: '',
  operatorId: '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})

const loading = ref(false)
const exporting = ref(false)
const auditList = ref<AuditLogEntryDto[]>([])
const auditTotal = ref(0)
const operationList = ref<OperationLogDto[]>([])
const operationTotal = ref(0)
const entryList = ref<CrossDomainAuditEntryDto[]>([])
const entryTotal = ref(0)

// 详情抽屉
const drawerVisible = ref(false)
const drawerLoading = ref(false)
const currentDetail = ref<AuditLogEntryDto | null>(null)

const resourceTypeOptions = [
  'Shop', 'Role', 'User', 'DeadLetter', 'IndexRebuild', 'Reconciliation',
  'RateLimitRule', 'FeatureFlag', 'SystemConfig', 'DataDictionary', 'Announcement',
  'ScheduledTask', 'OAuthClient', 'Operator', 'Outbox', 'Alert',
]
const moduleOptions = [
  'Identity', 'AccessControl', 'UserCenter', 'Points', 'Membership',
  'Review', 'AfterSales', 'Product', 'Order', 'Payment', 'Notification', 'Inventory',
  'SystemAdmin',
]

const auditColumns = computed(() => [
  { title: '日志ID', dataIndex: 'logId', key: 'logId', width: 140, ellipsis: true },
  { title: '操作人', key: 'operator', width: 120, customRender: ({ record }: { record: AuditLogEntryDto }) => record.operatorName },
  { title: '角色', key: 'operatorRole', width: 100 },
  { title: '来源上下文', dataIndex: 'sourceContext', key: 'sourceContext', width: 120 },
  { title: '动作', dataIndex: 'action', key: 'action', width: 100 },
  { title: '资源类型', dataIndex: 'resourceType', key: 'resourceType', width: 120 },
  { title: '资源ID', dataIndex: 'resourceId', key: 'resourceId', width: 140, ellipsis: true },
  { title: '响应状态', dataIndex: 'responseStatus', key: 'responseStatus', width: 100, align: 'right' as const },
  { title: 'IP', dataIndex: 'ipAddress', key: 'ipAddress', width: 130, responsive: ['xl'] as const },
  { title: '发生时间', dataIndex: 'occurredAt', key: 'occurredAt', width: 170, customRender: ({ text }: { text: string }) => dayjs(text).format('YYYY-MM-DD HH:mm:ss') },
  { title: '操作', key: 'action-col', width: 90, fixed: 'right' as const },
])

const operationColumns = computed(() => [
  { title: '日志ID', dataIndex: 'logId', key: 'logId', width: 140, ellipsis: true },
  { title: '操作人', key: 'operator', width: 120, customRender: ({ record }: { record: OperationLogDto }) => record.operatorName },
  { title: '模块', dataIndex: 'module', key: 'module', width: 120 },
  { title: '动作', dataIndex: 'action', key: 'action', width: 100 },
  { title: '资源类型', dataIndex: 'resourceType', key: 'resourceType', width: 120 },
  { title: '资源ID', dataIndex: 'resourceId', key: 'resourceId', width: 140, ellipsis: true },
  { title: '详情', dataIndex: 'detail', key: 'detail', ellipsis: true },
  { title: '发生时间', dataIndex: 'occurredAt', key: 'occurredAt', width: 170, customRender: ({ text }: { text: string }) => dayjs(text).format('YYYY-MM-DD HH:mm:ss') },
])

const entryColumns = computed(() => [
  { title: '条目ID', dataIndex: 'entryId', key: 'entryId', width: 140, ellipsis: true },
  { title: '模块', dataIndex: 'module', key: 'module', width: 120 },
  { title: '动作', dataIndex: 'action', key: 'action', width: 100 },
  { title: '操作人', key: 'operator', width: 120, customRender: ({ record }: { record: CrossDomainAuditEntryDto }) => record.operatorName },
  { title: '资源类型', dataIndex: 'resourceType', key: 'resourceType', width: 120 },
  { title: '资源ID', dataIndex: 'resourceId', key: 'resourceId', width: 140, ellipsis: true },
  { title: 'TraceId', dataIndex: 'traceId', key: 'traceId', width: 160, ellipsis: true },
  { title: '发生时间', dataIndex: 'occurredAt', key: 'occurredAt', width: 170, customRender: ({ text }: { text: string }) => dayjs(text).format('YYYY-MM-DD HH:mm:ss') },
])

// 响应状态码颜色：2xx 绿、4xx 黄、5xx 红
function statusColor(status: number): string {
  if (status >= 200 && status < 300) return 'success'
  if (status >= 400 && status < 500) return 'warning'
  if (status >= 500) return 'error'
  return 'default'
}

// 操作人角色颜色（design-prompt §6）
function roleColor(role: OperatorRole): string {
  switch (role) {
    case 'Admin': return 'error'
    case 'Operator': return 'processing'
    case 'Seller': return 'success'
    case 'Buyer': return 'default'
    case 'System': return 'purple'
    default: return 'default'
  }
}

// 敏感字段名正则：匹配 password/token/secret/apiKey/credential/authorization（不区分大小写）
const SENSITIVE_KEY_PATTERN = /(password|token|secret|api[_-]?key|credential|authorization)/i

/** 将任意值替换为掩码占位 */
function maskValue(_value: unknown): string {
  return '******'
}

/** 递归掩码对象中匹配敏感键的字段值 */
function maskSensitive(input: unknown): unknown {
  if (input === null || input === undefined) return input
  if (Array.isArray(input)) return input.map(maskSensitive)
  if (typeof input === 'object') {
    const result: Record<string, unknown> = {}
    for (const [key, value] of Object.entries(input as Record<string, unknown>)) {
      if (SENSITIVE_KEY_PATTERN.test(key)) {
        result[key] = maskValue(value)
      } else if (typeof value === 'object' && value !== null) {
        result[key] = maskSensitive(value)
      } else {
        result[key] = value
      }
    }
    return result
  }
  return input
}

/** 安全解析 JSON 字符串；解析失败返回原始字符串 */
function safeParseJson(text: string | null | undefined): unknown {
  if (!text) return null
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

/** 解析 + 掩码 JSON 快照，返回可交给 JsonViewer 的对象 */
function maskedSnapshot(text: string | null | undefined): unknown {
  return maskSensitive(safeParseJson(text))
}

async function loadAuditLogs() {
  loading.value = true
  try {
    const params = {
      operatorId: auditFilter.operatorId || undefined,
      resourceType: auditFilter.resourceType || undefined,
      action: auditFilter.action || undefined,
      fromTime: auditFilter.timeRange?.[0],
      toTime: auditFilter.timeRange?.[1],
      page: auditFilter.page,
      pageSize: auditFilter.pageSize,
    }
    const res = await auditLogsApi.list(params)
    auditList.value = res.items
    auditTotal.value = res.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载审计日志失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

async function loadOperationLogs() {
  loading.value = true
  try {
    const params = {
      operatorId: operationFilter.operatorId || undefined,
      module: operationFilter.module || undefined,
      fromTime: operationFilter.timeRange?.[0],
      toTime: operationFilter.timeRange?.[1],
      page: operationFilter.page,
      pageSize: operationFilter.pageSize,
    }
    const res = await auditLogsApi.listOperationLogs(params)
    operationList.value = res.items
    operationTotal.value = res.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载操作日志失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

async function loadEntries() {
  loading.value = true
  try {
    const params = {
      module: entryFilter.module || undefined,
      action: entryFilter.action || undefined,
      operatorId: entryFilter.operatorId || undefined,
      fromTime: entryFilter.timeRange?.[0],
      toTime: entryFilter.timeRange?.[1],
      page: entryFilter.page,
      pageSize: entryFilter.pageSize,
    }
    const res = await auditLogsApi.listAuditLogEntries(params)
    entryList.value = res.items
    entryTotal.value = res.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载跨域审计条目失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

function onSearch() {
  if (activeTab.value === 'audit-logs') {
    auditFilter.page = 1
    loadAuditLogs()
  } else if (activeTab.value === 'operation-logs') {
    operationFilter.page = 1
    loadOperationLogs()
  } else {
    entryFilter.page = 1
    loadEntries()
  }
}

function onTabChange(key: string) {
  activeTab.value = key as TabKey
  if (key === 'audit-logs') loadAuditLogs()
  else if (key === 'operation-logs') loadOperationLogs()
  else loadEntries()
}

function clearFilter() {
  if (activeTab.value === 'audit-logs') {
    auditFilter.operatorId = ''
    auditFilter.resourceType = ''
    auditFilter.action = ''
    auditFilter.timeRange = defaultRange()
    auditFilter.page = 1
    loadAuditLogs()
  } else if (activeTab.value === 'operation-logs') {
    operationFilter.operatorId = ''
    operationFilter.module = ''
    operationFilter.timeRange = defaultRange()
    operationFilter.page = 1
    loadOperationLogs()
  } else {
    entryFilter.module = ''
    entryFilter.action = ''
    entryFilter.operatorId = ''
    entryFilter.timeRange = defaultRange()
    entryFilter.page = 1
    loadEntries()
  }
}

async function openDetail(record: AuditLogEntryDto) {
  drawerVisible.value = true
  drawerLoading.value = true
  currentDetail.value = null
  try {
    const detail = await auditLogsApi.get(record.logId)
    currentDetail.value = detail
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '审计日志条目不存在'
    message.error(msg)
    drawerVisible.value = false
  } finally {
    drawerLoading.value = false
  }
}

/** 从响应头 Content-Disposition 解析文件名；缺省按时间戳生成 */
function parseFilename(disposition: string | undefined): string {
  if (!disposition) return `audit-logs-${dayjs().format('YYYYMMDDHHmmss')}.csv`
  const match = /filename\*?=(?:UTF-8'')?([^;]+)/i.exec(disposition)
  if (match && match[1]) {
    return decodeURIComponent(match[1].replace(/^"|"$/g, ''))
  }
  return `audit-logs-${dayjs().format('YYYYMMDDHHmmss')}.csv`
}

async function onExport() {
  if (!auditFilter.timeRange) {
    message.warning('请先选择时间范围')
    return
  }
  exporting.value = true
  try {
    const params = {
      operatorId: auditFilter.operatorId || undefined,
      resourceType: auditFilter.resourceType || undefined,
      action: auditFilter.action || undefined,
      fromTime: auditFilter.timeRange[0],
      toTime: auditFilter.timeRange[1],
    }
    const blob = await auditLogsApi.export(params)
    // 注：响应拦截器已解包 data；blob 为 Blob 实例。文件名按时间戳生成（拦截器解包后无法读取头）。
    const filename = `audit-logs-${dayjs().format('YYYYMMDDHHmmss')}.csv`
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    message.success(`已导出 ${filename}`)
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '导出失败，请缩小时间范围后重试'
    message.error(msg)
  } finally {
    exporting.value = false
  }
}

// 静默引用 parseFilename 以备后续拦截器透传响应头时切换；当前 onExport 使用时间戳兜底
void parseFilename

function onAuditPageChange(page: number, pageSize: number) {
  auditFilter.page = page
  auditFilter.pageSize = pageSize
  loadAuditLogs()
}
function onOperationPageChange(page: number, pageSize: number) {
  operationFilter.page = page
  operationFilter.pageSize = pageSize
  loadOperationLogs()
}
function onEntryPageChange(page: number, pageSize: number) {
  entryFilter.page = page
  entryFilter.pageSize = pageSize
  loadEntries()
}

onMounted(() => {
  loadAuditLogs()
})
</script>

<template>
  <div class="audit-logs">
    <div class="page-header">
      <div class="page-title">审计日志</div>
      <div class="page-desc">查询跨域审计日志条目与操作日志，按操作人、模块、资源类型、时间区间筛选，查看详情并导出 CSV 用于合规追溯。</div>
    </div>

    <a-tabs :active-key="activeTab" @change="onTabChange">
      <a-tab-pane key="audit-logs" tab="审计日志" />
      <a-tab-pane key="operation-logs" tab="操作日志" />
      <a-tab-pane key="cross-domain-entries" tab="跨域审计条目" />
    </a-tabs>

    <!-- 筛选条 -->
    <div class="toolbar">
      <template v-if="activeTab === 'audit-logs'">
        <a-input
          v-model:value="auditFilter.operatorId"
          placeholder="操作人 ID"
          allow-clear
          style="width: 180px"
        />
        <a-select
          v-model:value="auditFilter.resourceType"
          placeholder="资源类型"
          allow-clear
          style="width: 180px"
          :options="resourceTypeOptions.map((v) => ({ label: v, value: v }))"
        />
        <a-input
          v-model:value="auditFilter.action"
          placeholder="动作（如 Create）"
          allow-clear
          style="width: 160px"
        />
        <DateTimeRangePicker v-model:value="auditFilter.timeRange" />
      </template>
      <template v-else-if="activeTab === 'operation-logs'">
        <a-input
          v-model:value="operationFilter.operatorId"
          placeholder="操作人 ID"
          allow-clear
          style="width: 180px"
        />
        <a-select
          v-model:value="operationFilter.module"
          placeholder="模块"
          allow-clear
          style="width: 180px"
          :options="moduleOptions.map((v) => ({ label: v, value: v }))"
        />
        <DateTimeRangePicker v-model:value="operationFilter.timeRange" />
      </template>
      <template v-else>
        <a-select
          v-model:value="entryFilter.module"
          placeholder="模块"
          allow-clear
          style="width: 180px"
          :options="moduleOptions.map((v) => ({ label: v, value: v }))"
        />
        <a-input
          v-model:value="entryFilter.action"
          placeholder="动作"
          allow-clear
          style="width: 160px"
        />
        <a-input
          v-model:value="entryFilter.operatorId"
          placeholder="操作人 ID"
          allow-clear
          style="width: 180px"
        />
        <DateTimeRangePicker v-model:value="entryFilter.timeRange" />
      </template>
      <a-button type="primary" @click="onSearch">
        <SearchOutlined />查询
      </a-button>
      <PermissionGuard permission="audit-log:export">
        <a-button :loading="exporting" @click="onExport" v-if="activeTab === 'audit-logs'">
          <DownloadOutlined />导出 CSV
        </a-button>
      </PermissionGuard>
      <div class="spacer" />
      <a-button @click="clearFilter">清空筛选</a-button>
    </div>

    <!-- 审计日志表格 -->
    <a-table
      v-if="activeTab === 'audit-logs'"
      :columns="auditColumns"
      :data-source="auditList"
      :loading="loading"
      row-key="logId"
      size="middle"
      :scroll="{ x: 1300 }"
      :pagination="{
        current: auditFilter.page,
        pageSize: auditFilter.pageSize,
        total: auditTotal,
        showSizeChanger: true,
        onChange: onAuditPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无审计日志" action-text="清空筛选条件" @action="clearFilter" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'operatorRole'">
          <a-tag :color="roleColor(record.operatorRole)">{{ record.operatorRole }}</a-tag>
        </template>
        <template v-else-if="column.key === 'responseStatus'">
          <a-tag :color="statusColor(record.responseStatus)">{{ record.responseStatus }}</a-tag>
        </template>
        <template v-else-if="column.key === 'action-col'">
          <a-button type="link" size="small" @click="openDetail(record)">
            <EyeOutlined />详情
          </a-button>
        </template>
      </template>
    </a-table>

    <!-- 操作日志表格 -->
    <a-table
      v-else-if="activeTab === 'operation-logs'"
      :columns="operationColumns"
      :data-source="operationList"
      :loading="loading"
      row-key="logId"
      size="middle"
      :scroll="{ x: 1100 }"
      :pagination="{
        current: operationFilter.page,
        pageSize: operationFilter.pageSize,
        total: operationTotal,
        showSizeChanger: true,
        onChange: onOperationPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无操作日志" action-text="清空筛选条件" @action="clearFilter" />
      </template>
    </a-table>

    <!-- 跨域审计条目表格 -->
    <a-table
      v-else
      :columns="entryColumns"
      :data-source="entryList"
      :loading="loading"
      row-key="entryId"
      size="middle"
      :scroll="{ x: 1200 }"
      :pagination="{
        current: entryFilter.page,
        pageSize: entryFilter.pageSize,
        total: entryTotal,
        showSizeChanger: true,
        onChange: onEntryPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无跨域审计条目" action-text="清空筛选条件" @action="clearFilter" />
      </template>
    </a-table>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="审计日志详情"
      placement="right"
      :width="720"
    >
      <a-spin :spinning="drawerLoading">
        <template v-if="currentDetail">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="日志ID">{{ currentDetail.logId }}</a-descriptions-item>
            <a-descriptions-item label="操作人">{{ currentDetail.operatorName }}（{{ currentDetail.operatorId }}）</a-descriptions-item>
            <a-descriptions-item label="角色">
              <a-tag :color="roleColor(currentDetail.operatorRole)">{{ currentDetail.operatorRole }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="来源上下文">{{ currentDetail.sourceContext }}</a-descriptions-item>
            <a-descriptions-item label="动作">{{ currentDetail.action }}</a-descriptions-item>
            <a-descriptions-item label="资源类型">{{ currentDetail.resourceType }}</a-descriptions-item>
            <a-descriptions-item label="资源ID" :span="2">{{ currentDetail.resourceId }}</a-descriptions-item>
            <a-descriptions-item label="响应状态">
              <a-tag :color="statusColor(currentDetail.responseStatus)">{{ currentDetail.responseStatus }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="发生时间">{{ dayjs(currentDetail.occurredAt).format('YYYY-MM-DD HH:mm:ss') }}</a-descriptions-item>
            <a-descriptions-item label="IP" :span="2">{{ currentDetail.ipAddress }}</a-descriptions-item>
            <a-descriptions-item label="User-Agent" :span="2">{{ currentDetail.userAgent }}</a-descriptions-item>
            <a-descriptions-item label="TraceId" :span="2">
              <code class="trace-id">{{ currentDetail.traceId }}</code>
            </a-descriptions-item>
          </a-descriptions>

          <div class="snapshot-section">
            <div class="snapshot-title">请求摘要（敏感字段已掩码）</div>
            <JsonViewer :data="maskedSnapshot(currentDetail.requestSummary)" :max-height="200" />
          </div>
          <div class="snapshot-section">
            <div class="snapshot-title">操作前快照（敏感字段已掩码）</div>
            <JsonViewer :data="maskedSnapshot(currentDetail.beforeSnapshot)" :max-height="280" />
          </div>
          <div class="snapshot-section">
            <div class="snapshot-title">操作后快照（敏感字段已掩码）</div>
            <JsonViewer :data="maskedSnapshot(currentDetail.afterSnapshot)" :max-height="280" />
          </div>
        </template>
        <EmptyState v-else-if="!drawerLoading" description="审计日志条目不存在" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.audit-logs .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.audit-logs .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.audit-logs .page-desc { color: #8C8C8C; }
.audit-logs .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.audit-logs .spacer { flex: 1; }
.audit-logs .snapshot-section { margin-top: 16px; }
.audit-logs .snapshot-title { font-size: 14px; font-weight: 500; margin-bottom: 8px; color: #595959; }
.audit-logs .trace-id { font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace; font-size: 12px; word-break: break-all; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/05-audit/views/AuditLogs.vue
git commit -m "feat(audit): 实现 AuditLogs.vue（3 Tab+筛选+表格+详情抽屉+敏感字段掩码+导出 CSV，只读）"
```

---

## Task 7: Reconciliation.vue 对账管理视图

**Files:**
- Create: `web/system-admin/src/modules/05-audit/views/Reconciliation.vue`

**实现要点（design-prompt §1-8）:**
- 顶部状态卡片区：4 个 `<a-statistic>` — 对账状态（一致/有差异）、差异项数量、最近对账时间、是否触发告警/修正
- 触发对账区：报表类型下拉（含「全部」）+ `DateTimeRangePicker` + 「触发对账」`IdempotencyButton`（`PermissionGuard permission="reconciliation:trigger"`）
- 历史记录表格：记录ID/报表类型/对账时间/状态/差异项数/告警/修正/错误信息/操作（详情），按时间倒序
- 详情抽屉：`<a-drawer width="720">` 展示对账记录全字段 + 差异项明细列表（报表类型/指标名/期望值/实际值/差异值）
- 状态色：Consistent 绿、Discrepancy 黄、Failed 红；告警红、修正橙
- 差异项 > 0 行高亮（行 className）
- 触发对账走 `ConfirmDialog`，主色确认按钮；时间范围默认近 7 天
- 跨页面跳转：路由 query `reportType` 自动回填

- [ ] **Step 1: 实现 Reconciliation.vue**

```vue
<!-- web/system-admin/src/modules/05-audit/views/Reconciliation.vue -->
<!-- 对账管理：4 状态卡片 + 触发对账（幂等+确认） + 历史表格 + 详情抽屉（差异项明细） -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  PlayCircleOutlined, CheckCircleOutlined, WarningOutlined,
  ExclamationCircleOutlined, EyeOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { reconciliationApi } from '../api/reconciliation.api'
import type {
  ReconciliationStatusDto,
  ReconciliationRecordDto,
  ReconciliationReportType,
  ReconciliationStatus,
} from '../types/reconciliation.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'

const route = useRoute()

const reportTypeOptions: { label: string; value: ReconciliationReportType | '' }[] = [
  { label: '全部', value: '' },
  { label: '订单 GMV', value: 'OrderGmv' },
  { label: '支付成功率', value: 'PaymentSuccessRate' },
  { label: '积分发放', value: 'PointsIssued' },
  { label: '通知送达', value: 'NotificationDelivery' },
  { label: '售后量', value: 'AfterSalesVolume' },
  { label: '店铺排名', value: 'ShopRanking' },
  { label: '转化率', value: 'ConversionRate' },
]

const reportTypeLabel = (rt: ReconciliationReportType): string =>
  reportTypeOptions.find((o) => o.value === rt)?.label ?? rt

// 默认近 7 天（ISO 8601 UTC）
function defaultRange(): [string, string] {
  return [
    dayjs().subtract(7, 'day').toISOString(),
    dayjs().toISOString(),
  ]
}

const statusLoading = ref(false)
const status = ref<ReconciliationStatusDto | null>(null)

const triggerForm = reactive<{
  reportType: ReconciliationReportType | ''
  timeRange: [string, string] | null
}>({
  reportType: (route.query.reportType as ReconciliationReportType) || '',
  timeRange: defaultRange(),
})

const listLoading = ref(false)
const records = ref<ReconciliationRecordDto[]>([])
const listFilter = reactive<{
  reportType: ReconciliationReportType | ''
  timeRange: [string, string] | null
  page: number
  pageSize: number
}>({
  reportType: (route.query.reportType as ReconciliationReportType) || '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})
const listTotal = ref(0)

const triggerConfirmVisible = ref(false)
const triggering = ref(false)

// 详情抽屉
const drawerVisible = ref(false)
const currentRecord = ref<ReconciliationRecordDto | null>(null)

const columns = computed(() => [
  { title: '记录ID', dataIndex: 'recordId', key: 'recordId', width: 140, ellipsis: true },
  { title: '报表类型', key: 'reportType', width: 130, customRender: ({ record }: { record: ReconciliationRecordDto }) => reportTypeLabel(record.reportType) },
  { title: '对账时间', dataIndex: 'reconciledAt', key: 'reconciledAt', width: 170, customRender: ({ text }: { text: string }) => dayjs(text).format('YYYY-MM-DD HH:mm:ss') },
  { title: '状态', key: 'status', width: 100 },
  { title: '差异项数', dataIndex: 'discrepancyCount', key: 'discrepancyCount', width: 100, align: 'right' as const },
  { title: '告警', key: 'alertTriggered', width: 80 },
  { title: '修正', key: 'correctionTriggered', width: 80 },
  { title: '错误信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
  { title: '操作', key: 'action-col', width: 90, fixed: 'right' as const },
])

function statusTagType(s: ReconciliationStatus | null): 'success' | 'warning' | 'error' | 'default' {
  if (s === 'Consistent') return 'success'
  if (s === 'Discrepancy') return 'warning'
  if (s === 'Failed') return 'error'
  return 'default'
}
function statusLabel(s: ReconciliationStatus | null): string {
  if (s === 'Consistent') return '一致'
  if (s === 'Discrepancy') return '有差异'
  if (s === 'Failed') return '失败'
  return '尚未执行'
}

async function loadStatus() {
  statusLoading.value = true
  try {
    status.value = await reconciliationApi.getStatus()
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载对账状态失败'
    message.error(msg)
  } finally {
    statusLoading.value = false
  }
}

async function loadRecords() {
  listLoading.value = true
  try {
    const params = {
      reportType: listFilter.reportType || undefined,
      start: listFilter.timeRange?.[0],
      end: listFilter.timeRange?.[1],
      page: listFilter.page,
      pageSize: listFilter.pageSize,
    }
    const res = await reconciliationApi.listRecords(params)
    records.value = res.items
    listTotal.value = res.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载对账记录失败'
    message.error(msg)
  } finally {
    listLoading.value = false
  }
}

function onListSearch() {
  listFilter.page = 1
  loadRecords()
}

function openTriggerConfirm() {
  triggerConfirmVisible.value = true
}

async function onConfirmTrigger() {
  if (!triggerForm.timeRange) {
    message.warning('请先选择时间范围')
    return
  }
  triggering.value = true
  try {
    const params = {
      reportType: triggerForm.reportType || undefined,
      start: triggerForm.timeRange[0],
      end: triggerForm.timeRange[1],
    }
    const result = await reconciliationApi.trigger(params)
    triggerConfirmVisible.value = false
    if (result.length === 1) {
      message.success('对账已完成')
    } else {
      message.info(`已对账全部报表类型，共 ${result.length} 条记录`)
    }
    // 刷新状态卡片 + 记录列表
    await Promise.all([loadStatus(), loadRecords()])
  } catch (e: unknown) {
    if (e instanceof BusinessError) message.error(e.message)
    else {
      const msg = e instanceof Error ? e.message : '触发对账失败'
      message.error(msg)
    }
  } finally {
    triggering.value = false
  }
}

function openDetail(record: ReconciliationRecordDto) {
  currentRecord.value = record
  drawerVisible.value = true
}

function onPageChange(page: number, pageSize: number) {
  listFilter.page = page
  listFilter.pageSize = pageSize
  loadRecords()
}

// 差异项 > 0 行高亮 className
function rowClassName(record: ReconciliationRecordDto): string {
  return record.discrepancyCount > 0 ? 'reconciliation-row-highlight' : ''
}

onMounted(() => {
  Promise.all([loadStatus(), loadRecords()])
})
</script>

<template>
  <div class="reconciliation">
    <div class="page-header">
      <div class="page-title">对账管理</div>
      <div class="page-desc">查看最近一次对账状态，手动触发按报表类型与时间范围的对账，查看历史对账记录与差异项，确保跨域统计指标一致。</div>
    </div>

    <!-- 状态卡片区 -->
    <a-row :gutter="24" class="status-cards">
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="对账状态"
            :value="status ? statusLabel(status.status) : '尚未执行'"
            :value-style="{ color: status ? (status.isConsistent ? '#52C41A' : (status.status === 'Failed' ? '#FF4D4F' : '#FAAD14')) : '#8C8C8C' }"
          >
            <template #prefix>
              <CheckCircleOutlined v-if="status?.isConsistent" style="color: #52C41A" />
              <WarningOutlined v-else-if="status?.status === 'Discrepancy'" style="color: #FAAD14" />
              <ExclamationCircleOutlined v-else-if="status?.status === 'Failed'" style="color: #FF4D4F" />
            </template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="差异项数量"
            :value="status?.discrepancyCount ?? 0"
            :value-style="{ color: (status?.discrepancyCount ?? 0) > 0 ? '#FAAD14' : '#595959' }"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="最近对账时间"
            :value="status?.reconciledAt ? dayjs(status.reconciledAt).format('MM-DD HH:mm') : '—'"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="告警 / 修正"
            :value="status ? `${status.alertTriggered ? '是' : '否'} / ${status.correctionTriggered ? '是' : '否'}` : '—'"
            :value-style="{ color: status?.alertTriggered ? '#FF4D4F' : '#595959' }"
          />
        </a-card>
      </a-col>
    </a-row>

    <!-- 触发对账区 -->
    <a-card class="trigger-card">
      <div class="trigger-row">
        <a-select
          v-model:value="triggerForm.reportType"
          placeholder="报表类型"
          style="width: 200px"
          :options="reportTypeOptions"
        />
        <DateTimeRangePicker v-model:value="triggerForm.timeRange" />
        <PermissionGuard permission="reconciliation:trigger">
          <IdempotencyButton type="primary" :loading="triggering" @click="openTriggerConfirm">
            <PlayCircleOutlined />触发对账
          </IdempotencyButton>
        </PermissionGuard>
      </div>
    </a-card>

    <!-- 历史记录表格 -->
    <a-card title="对账历史记录" class="records-card">
      <div class="toolbar">
        <a-select
          v-model:value="listFilter.reportType"
          placeholder="报表类型"
          style="width: 200px"
          :options="reportTypeOptions"
          @change="onListSearch"
        />
        <DateTimeRangePicker v-model:value="listFilter.timeRange" @change="onListSearch" />
        <a-button type="primary" @click="onListSearch">筛选</a-button>
      </div>
      <a-table
        :columns="columns"
        :data-source="records"
        :loading="listLoading"
        row-key="recordId"
        size="middle"
        :scroll="{ x: 1100 }"
        :row-class-name="rowClassName"
        :pagination="{
          current: listFilter.page,
          pageSize: listFilter.pageSize,
          total: listTotal,
          showSizeChanger: true,
          onChange: onPageChange,
        }"
      >
        <template #emptyText>
          <EmptyState description="暂无对账记录" action-text="触发首次对账" @action="openTriggerConfirm" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusTagType(record.status)">{{ statusLabel(record.status) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'alertTriggered'">
            <a-tag :color="record.alertTriggered ? 'error' : 'default'">{{ record.alertTriggered ? '是' : '否' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'correctionTriggered'">
            <a-tag :color="record.correctionTriggered ? 'warning' : 'default'">{{ record.correctionTriggered ? '是' : '否' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'action-col'">
            <a-button type="link" size="small" @click="openDetail(record)">
              <EyeOutlined />详情
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 触发对账确认 -->
    <ConfirmDialog
      v-model:open="triggerConfirmVisible"
      :danger="false"
      title="确认触发对账"
      content="触发对账将重新计算指定报表类型的统计指标并与各域数据比对，可能耗时较长（视数据量而定）。是否继续？"
      ok-text="触发对账"
      cancel-text="取消"
      :confirm-loading="triggering"
      @confirm="onConfirmTrigger"
    />

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="对账记录详情"
      placement="right"
      :width="720"
    >
      <template v-if="currentRecord">
        <a-descriptions :column="2" bordered size="small">
          <a-descriptions-item label="记录ID">{{ currentRecord.recordId }}</a-descriptions-item>
          <a-descriptions-item label="报表类型">{{ reportTypeLabel(currentRecord.reportType) }}</a-descriptions-item>
          <a-descriptions-item label="对账时间">{{ dayjs(currentRecord.reconciledAt).format('YYYY-MM-DD HH:mm:ss') }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="statusTagType(currentRecord.status)">{{ statusLabel(currentRecord.status) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="差异项数">{{ currentRecord.discrepancyCount }}</a-descriptions-item>
          <a-descriptions-item label="告警">
            <a-tag :color="currentRecord.alertTriggered ? 'error' : 'default'">{{ currentRecord.alertTriggered ? '是' : '否' }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="修正">
            <a-tag :color="currentRecord.correctionTriggered ? 'warning' : 'default'">{{ currentRecord.correctionTriggered ? '是' : '否' }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="错误信息" :span="2">
            <span v-if="currentRecord.errorMessage" class="error-msg">{{ currentRecord.errorMessage }}</span>
            <span v-else>—</span>
          </a-descriptions-item>
        </a-descriptions>

        <div class="discrepancy-section">
          <div class="discrepancy-title">差异项明细（{{ currentRecord.discrepancies.length }} 项）</div>
          <a-table
            v-if="currentRecord.discrepancies.length > 0"
            :columns="[
              { title: '报表类型', dataIndex: 'reportType', key: 'reportType', customRender: ({ text }: { text: ReconciliationReportType }) => reportTypeLabel(text) },
              { title: '指标名', dataIndex: 'metricName', key: 'metricName' },
              { title: '期望值', dataIndex: 'expectedValue', key: 'expectedValue', align: 'right' as const },
              { title: '实际值', dataIndex: 'actualValue', key: 'actualValue', align: 'right' as const },
              { title: '差异值', dataIndex: 'diffValue', key: 'diffValue', align: 'right' as const },
            ]"
            :data-source="currentRecord.discrepancies"
            row-key="metricName"
            size="small"
            :pagination="false"
          />
          <EmptyState v-else description="无差异项，指标一致" />
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<style scoped>
.reconciliation .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.reconciliation .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.reconciliation .page-desc { color: #8C8C8C; }
.reconciliation .status-cards { margin-bottom: 16px; }
.reconciliation .status-card { border-radius: 8px; }
.reconciliation .trigger-card { margin-bottom: 16px; border-radius: 8px; }
.reconciliation .trigger-row { display: flex; gap: 12px; flex-wrap: wrap; align-items: center; }
.reconciliation .records-card { border-radius: 8px; }
.reconciliation .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.reconciliation .discrepancy-section { margin-top: 24px; }
.reconciliation .discrepancy-title { font-size: 14px; font-weight: 500; margin-bottom: 12px; color: #595959; }
.reconciliation .error-msg { color: #FF4D4F; font-size: 12px; word-break: break-all; }
.reconciliation :deep(.reconciliation-row-highlight) { background-color: #FFF7E6; }
.reconciliation :deep(.reconciliation-row-highlight:hover) > td { background-color: #FFE7BA !important; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/05-audit/views/Reconciliation.vue
git commit -m "feat(audit): 实现 Reconciliation.vue（4 状态卡片+触发对账幂等+历史表格+差异项明细抽屉）"
```

---

## Task 8: OutboxMonitor.vue Outbox 监控视图

**Files:**
- Create: `web/system-admin/src/modules/05-audit/views/OutboxMonitor.vue`

**实现要点（design-prompt §1-8）:**
- 顶部 `<a-alert type="info">` 提示「后端 Outbox 监控端点规划中，数据可能为空」（design-prompt 标 🚧 规划中）
- 统计条：4 个 `<a-statistic>` — 总积压事件数 / 积压域数量 / 最大积压时长 / 今日重投次数
- 积压趋势图：`ChartLine` 近 24 小时 outbox_pending_count 趋势，按域分系列，高度 300px
- 按域分组表格：限界上下文/未发布事件数/最早事件时间/最大积压时长/最近归档时间/状态/操作（详情/重投/归档），按积压数倒序
- 详情抽屉：`<a-drawer width="720">` 展示该域 Outbox 积压事件列表（事件ID/聚合ID/事件类型/Payload/创建时间/重试次数）+ 归档历史
- 状态色：正常 `#52C41A`、积压 `#FAAD14`、严重积压 `#FF4D4F`、已归档 `#8C8C8C`
- 积压数 > 1000 状态自动标红 + `notification.warning`
- 重投走 `ConfirmDialog`（主色确认）；归档走 `ConfirmDialog`（danger 红色 + `requireInput` 填归档阈值与原因）
- 每 60s 轮询刷新汇总

- [ ] **Step 1: 实现 OutboxMonitor.vue**

```vue
<!-- web/system-admin/src/modules/05-audit/views/OutboxMonitor.vue -->
<!-- Outbox 监控：统计条 + 趋势折线 + 按域表格 + 详情抽屉 + 重投/归档确认，每 60s 轮询 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message, notification } from 'ant-design-vue'
import {
  InboxOutlined, ReloadOutlined, ArchiveOutlined, EyeOutlined, WarningOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { outboxMonitorApi } from '../api/outbox-monitor.api'
import type {
  OutboxSummaryDto,
  OutboxTrendPointDto,
  OutboxMessageDto,
  OutboxArchiveHistoryDto,
  OutboxStatus,
  BatchRepublishOutboxDto,
  ArchiveOutboxDto,
} from '../types/outbox.dto'
import type { BatchRepublishResultDto } from '../api/outbox-monitor.api'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'

const summaryLoading = ref(false)
const summary = ref<OutboxSummaryDto[]>([])

const trendLoading = ref(false)
const trendData = ref<OutboxTrendPointDto[]>([])

// 统计条聚合
const totalPending = computed(() => summary.value.reduce((acc, s) => acc + s.pendingCount, 0))
const backlogContextCount = computed(() => summary.value.filter((s) => s.status !== 'Normal' && s.status !== 'Archived').length)
const maxAgeMinutes = computed(() => summary.value.reduce((acc, s) => Math.max(acc, s.maxAgeMinutes), 0))
const todayRepublishCount = ref(0)

// 严重积压阈值（design-prompt §4 分支流程）
const SEVERE_THRESHOLD = 1000

function statusLabel(s: OutboxStatus): string {
  switch (s) {
    case 'Normal': return '正常'
    case 'Backlog': return '积压'
    case 'Severe': return '严重积压'
    case 'Archived': return '已归档'
    default: return s
  }
}
function statusColor(s: OutboxStatus): 'success' | 'warning' | 'error' | 'default' {
  switch (s) {
    case 'Normal': return 'success'
    case 'Backlog': return 'warning'
    case 'Severe': return 'error'
    case 'Archived': return 'default'
    default: return 'default'
  }
}

// 趋势图数据：按 context 分系列
const trendChartData = computed(() => {
  // ChartLine 期望 { date, value, series? } 形态（与 Plan 2 dashboard 一致）
  return trendData.value.map((p) => ({
    date: dayjs(p.timestamp).format('MM-DD HH:mm'),
    value: p.pendingCount,
    series: p.context,
  }))
})
const hasTrendData = computed(() => trendChartData.value.length > 0)

// 按积压数倒序
const sortedSummary = computed(() =>
  [...summary.value].sort((a, b) => b.pendingCount - a.pendingCount),
)

const columns = computed(() => [
  { title: '限界上下文', dataIndex: 'context', key: 'context', width: 140 },
  { title: '未发布事件数', dataIndex: 'pendingCount', key: 'pendingCount', width: 130, align: 'right' as const },
  { title: '最早事件时间', dataIndex: 'oldestEventAt', key: 'oldestEventAt', width: 170, customRender: ({ text }: { text: string | null }) => text ? dayjs(text).format('YYYY-MM-DD HH:mm:ss') : '—' },
  { title: '最大积压时长(分钟)', dataIndex: 'maxAgeMinutes', key: 'maxAgeMinutes', width: 160, align: 'right' as const },
  { title: '最近归档时间', dataIndex: 'lastArchivedAt', key: 'lastArchivedAt', width: 170, responsive: ['xl'] as const, customRender: ({ text }: { text: string | null }) => text ? dayjs(text).format('YYYY-MM-DD HH:mm:ss') : '—' },
  { title: '状态', key: 'status', width: 110 },
  { title: '操作', key: 'action-col', width: 180, fixed: 'right' as const },
])

// 详情抽屉
const drawerVisible = ref(false)
const drawerLoading = ref(false)
const drawerContext = ref<string>('')
const drawerMessages = ref<OutboxMessageDto[]>([])
const drawerArchiveHistory = ref<OutboxArchiveHistoryDto[]>([])

// 重投确认
const republishConfirmVisible = ref(false)
const republishContext = ref<string>('')
const republishing = ref(false)

// 归档确认（含表单：olderThanMinutes + reason）
const archiveConfirmVisible = ref(false)
const archiveContext = ref<string>('')
const archiveForm = reactive<{ olderThanMinutes: number; reason: string }>({
  olderThanMinutes: 60,
  reason: '',
})
const archiving = ref(false)

// 轮询定时器
let pollTimer: ReturnType<typeof setInterval> | null = null

async function loadSummary() {
  summaryLoading.value = true
  try {
    const data = await outboxMonitorApi.getSummary()
    summary.value = data
    // 严重积压自动标红 + 通知（design-prompt §4 分支流程）
    const severe = data.find((s) => s.pendingCount > SEVERE_THRESHOLD)
    if (severe) {
      notification.warning({
        message: 'Outbox 严重积压',
        description: `限界上下文 ${severe.context} 积压 ${severe.pendingCount} 条事件，超过阈值 ${SEVERE_THRESHOLD}，请及时处置。`,
        duration: 5,
      })
    }
  } catch (e: unknown) {
    // 后端规划中，API 可能 404；静默处理避免轮询刷屏
    summary.value = []
  } finally {
    summaryLoading.value = false
  }
}

async function loadTrend() {
  trendLoading.value = true
  try {
    const data = await outboxMonitorApi.getTrend({ hours: 24 })
    trendData.value = data
  } catch (e: unknown) {
    trendData.value = []
  } finally {
    trendLoading.value = false
  }
}

async function loadAll() {
  await Promise.all([loadSummary(), loadTrend()])
}

async function openDetail(record: OutboxSummaryDto) {
  drawerVisible.value = true
  drawerLoading.value = true
  drawerContext.value = record.context
  drawerMessages.value = []
  drawerArchiveHistory.value = []
  try {
    const [msgRes, history] = await Promise.all([
      outboxMonitorApi.listMessages({ context: record.context, page: 1, pageSize: 50 }),
      outboxMonitorApi.getArchiveHistory(record.context),
    ])
    drawerMessages.value = msgRes.items
    drawerArchiveHistory.value = history
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载积压事件详情失败'
    message.error(msg)
  } finally {
    drawerLoading.value = false
  }
}

function openRepublishConfirm(record: OutboxSummaryDto) {
  republishContext.value = record.context
  republishConfirmVisible.value = true
}

async function onConfirmRepublish() {
  republishing.value = true
  try {
    const body: BatchRepublishOutboxDto = { maxCount: 100 }
    const result: BatchRepublishResultDto = await outboxMonitorApi.republish(republishContext.value, body)
    republishConfirmVisible.value = false
    if (result.failed.length === 0) {
      message.success(`已重投 ${result.succeeded.length} 条积压事件`)
    } else {
      // 部分失败：弹窗显示成功/失败明细（design-prompt §4 分支流程）
      notification.warning({
        message: '重投部分失败',
        description: `成功 ${result.succeeded.length} 条，失败 ${result.failed.length} 条。失败原因：${result.failed[0]?.reason ?? '未知'}`,
        duration: 6,
      })
    }
    todayRepublishCount.value += result.succeeded.length
    await loadAll()
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '重投失败'
    message.error(msg)
  } finally {
    republishing.value = false
  }
}

function openArchiveConfirm(record: OutboxSummaryDto) {
  archiveContext.value = record.context
  archiveForm.olderThanMinutes = 60
  archiveForm.reason = ''
  archiveConfirmVisible.value = true
}

async function onConfirmArchive() {
  if (!archiveForm.reason.trim()) {
    message.warning('请填写归档原因')
    return
  }
  if (archiveForm.olderThanMinutes <= 0) {
    message.warning('归档阈值必须 > 0 分钟')
    return
  }
  archiving.value = true
  try {
    const body: ArchiveOutboxDto = {
      olderThanMinutes: archiveForm.olderThanMinutes,
      reason: archiveForm.reason.trim(),
    }
    const result = await outboxMonitorApi.archive(archiveContext.value, body)
    archiveConfirmVisible.value = false
    message.success(`已归档 ${result.archivedCount} 条陈旧积压事件`)
    await loadAll()
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '归档失败'
    message.error(msg)
  } finally {
    archiving.value = false
  }
}

function rowClassName(record: OutboxSummaryDto): string {
  if (record.pendingCount > SEVERE_THRESHOLD) return 'outbox-row-severe'
  if (record.status === 'Backlog') return 'outbox-row-backlog'
  return ''
}

onMounted(() => {
  loadAll()
  // 每 60s 轮询刷新汇总（design-prompt §4 主流程）
  pollTimer = setInterval(() => {
    loadSummary()
  }, 60_000)
})

onBeforeUnmount(() => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
})
</script>

<template>
  <div class="outbox-monitor">
    <div class="page-header">
      <div class="page-title">Outbox 监控</div>
      <div class="page-desc">监控各域 Outbox 发件箱积压情况，按限界上下文查看未发布事件数量与积压时长，触发积压告警处置（重投/归档），保障集成事件最终一致。</div>
    </div>

    <a-alert
      type="info"
      show-icon
      message="后端 Outbox 监控端点规划中"
      description="design-prompt 标记此页端点待后端实现。当前 API 层与视图已按契约完整实现，后端就绪后即可直接使用；端点未就绪时数据为空。"
      style="margin-bottom: 16px"
    />

    <!-- 统计条 -->
    <a-row :gutter="24" class="stats-row">
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="summaryLoading" class="stat-card">
          <a-statistic
            title="总积压事件数"
            :value="totalPending"
            :value-style="{ color: totalPending > SEVERE_THRESHOLD ? '#FF4D4F' : '#595959' }"
          >
            <template #prefix><InboxOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="summaryLoading" class="stat-card">
          <a-statistic title="积压域数量" :value="backlogContextCount" :value-style="{ color: backlogContextCount > 0 ? '#FAAD14' : '#595959' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="summaryLoading" class="stat-card">
          <a-statistic title="最大积压时长(分钟)" :value="maxAgeMinutes" :value-style="{ color: maxAgeMinutes > 30 ? '#FF4D4F' : '#595959' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card class="stat-card">
          <a-statistic title="今日重投次数" :value="todayRepublishCount" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 积压趋势图 -->
    <a-card title="近 24h 积压趋势" class="trend-card">
      <a-spin :spinning="trendLoading">
        <ChartLine
          v-if="hasTrendData"
          :data="trendChartData"
          series-field="series"
          :height="300"
        />
        <EmptyState v-else-if="!trendLoading" description="暂无积压趋势数据" action-text="刷新" @action="loadTrend" />
      </a-spin>
    </a-card>

    <!-- 按域分组表格 -->
    <a-card title="按域积压明细" class="table-card">
      <a-table
        :columns="columns"
        :data-source="sortedSummary"
        :loading="summaryLoading"
        row-key="context"
        size="middle"
        :scroll="{ x: 1100 }"
        :row-class-name="rowClassName"
        :pagination="false"
      >
        <template #emptyText>
          <EmptyState description="暂无积压事件，所有域 Outbox 正常" action-text="刷新" @action="loadAll" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">
              <WarningOutlined v-if="record.status === 'Severe'" />{{ statusLabel(record.status) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action-col'">
            <a-space>
              <a-button type="link" size="small" @click="openDetail(record)">
                <EyeOutlined />详情
              </a-button>
              <PermissionGuard permission="outbox:manage">
                <a-button type="link" size="small" @click="openRepublishConfirm(record)">
                  <ReloadOutlined />重投
                </a-button>
                <a-button type="link" size="small" danger @click="openArchiveConfirm(record)">
                  <ArchiveOutlined />归档
                </a-button>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`Outbox 积压详情 - ${drawerContext}`"
      placement="right"
      :width="720"
    >
      <a-spin :spinning="drawerLoading">
        <div class="drawer-section">
          <div class="drawer-title">积压事件列表（{{ drawerMessages.length }} 条）</div>
          <a-table
            v-if="drawerMessages.length > 0"
            :columns="[
              { title: '事件ID', dataIndex: 'messageId', key: 'messageId', width: 140, ellipsis: true },
              { title: '聚合ID', dataIndex: 'aggregateId', key: 'aggregateId', width: 140, ellipsis: true },
              { title: '事件类型', dataIndex: 'eventType', key: 'eventType', width: 160 },
              { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 170, customRender: ({ text }: { text: string }) => dayjs(text).format('YYYY-MM-DD HH:mm:ss') },
              { title: '重试次数', dataIndex: 'retryCount', key: 'retryCount', width: 90, align: 'right' as const },
            ]"
            :data-source="drawerMessages"
            row-key="messageId"
            size="small"
            :pagination="{ pageSize: 10 }"
            :expandable="{ expandedRowRender: (record: OutboxMessageDto) => record.payload }"
          >
            <template #expandedRowRender="{ record }">
              <div class="payload-section">
                <div class="payload-title">Payload</div>
                <JsonViewer :data="record.payload" :max-height="240" />
              </div>
            </template>
          </a-table>
          <EmptyState v-else description="暂无积压事件" />
        </div>

        <div class="drawer-section">
          <div class="drawer-title">归档历史</div>
          <a-table
            v-if="drawerArchiveHistory.length > 0"
            :columns="[
              { title: '归档时间', dataIndex: 'archivedAt', key: 'archivedAt', width: 170, customRender: ({ text }: { text: string }) => dayjs(text).format('YYYY-MM-DD HH:mm:ss') },
              { title: '归档数', dataIndex: 'count', key: 'count', width: 90, align: 'right' as const },
              { title: '原因', dataIndex: 'reason', key: 'reason' },
              { title: '操作人', dataIndex: 'archivedBy', key: 'archivedBy', width: 120 },
            ]"
            :data-source="drawerArchiveHistory"
            row-key="archivedAt"
            size="small"
            :pagination="false"
          />
          <EmptyState v-else description="暂无归档历史" />
        </div>
      </a-spin>
    </a-drawer>

    <!-- 重投确认 -->
    <ConfirmDialog
      v-model:open="republishConfirmVisible"
      :danger="false"
      title="确认重投积压事件"
      content="重投后积压事件将重新发布到事件总线，可能触发重复消费。订阅者需保证幂等。是否继续？"
      ok-text="重投"
      cancel-text="取消"
      :confirm-loading="republishing"
      @confirm="onConfirmRepublish"
    />

    <!-- 归档确认（danger + 表单：阈值 + 原因） -->
    <ConfirmDialog
      v-model:open="archiveConfirmVisible"
      :danger="true"
      title="归档陈旧积压事件"
      ok-text="归档"
      cancel-text="取消"
      :confirm-loading="archiving"
      @confirm="onConfirmArchive"
    >
      <a-alert
        type="warning"
        show-icon
        message="归档后陈旧积压事件将从监控视图移除并转入归档存储，不再自动重投。此操作可查询归档历史，但需手动恢复。"
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item label="归档阈值（积压时长超过此分钟数）" required>
          <a-input-number
            v-model:value="archiveForm.olderThanMinutes"
            :min="1"
            :max="10080"
            style="width: 100%"
            placeholder="例如 60 表示归档积压超过 1 小时的事件"
          />
        </a-form-item>
        <a-form-item label="归档原因" required>
          <a-textarea
            v-model:value="archiveForm.reason"
            :rows="3"
            :maxlength="500"
            show-count
            placeholder="请填写归档原因（1-500 字）"
          />
        </a-form-item>
      </a-form>
    </ConfirmDialog>
  </div>
</template>

<style scoped>
.outbox-monitor .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.outbox-monitor .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.outbox-monitor .page-desc { color: #8C8C8C; }
.outbox-monitor .stats-row { margin-bottom: 16px; }
.outbox-monitor .stat-card { border-radius: 8px; }
.outbox-monitor .trend-card { margin-bottom: 16px; border-radius: 8px; }
.outbox-monitor .table-card { border-radius: 8px; }
.outbox-monitor .drawer-section { margin-bottom: 24px; }
.outbox-monitor .drawer-title { font-size: 14px; font-weight: 500; margin-bottom: 12px; color: #595959; }
.outbox-monitor .payload-section { background: #FAFAFA; padding: 12px; border-radius: 4px; }
.outbox-monitor .payload-title { font-size: 12px; color: #8C8C8C; margin-bottom: 8px; }
.outbox-monitor :deep(.outbox-row-severe) { background-color: #FFF1F0; }
.outbox-monitor :deep(.outbox-row-severe:hover) > td { background-color: #FFCCC7 !important; }
.outbox-monitor :deep(.outbox-row-backlog) { background-color: #FFFBE6; }
.outbox-monitor :deep(.outbox-row-backlog:hover) > td { background-color: #FFF1B8 !important; }
</style>
```

- [ ] **Step 2: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/05-audit/views/OutboxMonitor.vue
git commit -m "feat(audit): 实现 OutboxMonitor.vue（统计条+趋势图+按域表格+详情抽屉+重投/归档确认+60s 轮询）"
```

---

## Plan 自检

### 1. Spec 覆盖核对

| Spec / design-prompt 项 | 对应 Task | 状态 |
|-|-|-|
| `05-audit/audit-logs.md` | Task 1（DTO）+ Task 2（API + 测试，TDD）+ Task 6（视图） | ✅ 覆盖 |
| `05-audit/reconciliation.md` | Task 1（DTO）+ Task 3（API）+ Task 7（视图） | ✅ 覆盖 |
| `05-audit/outbox-monitor.md` | Task 1（DTO）+ Task 4（API）+ Task 8（视图） | ✅ 覆盖 |
| 模块骨架 routes.ts + index.ts | Task 5 | ✅ 覆盖 |
| 1 个测试文件 audit-logs.api.spec.ts | Task 2 | ✅ 覆盖 |
| spec §2.5「审计日志只读，不可编辑；敏感参数字段掩码展示」 | Task 6（无任何写端点 + `maskSensitive` 函数 + `JsonViewer` 掩码展示） | ✅ 覆盖 |

### 2. 占位符扫描

- 全文扫描 `TODO` / `TBD` / `FIXME` / `未实现` / `省略` / `...` 均为 0 处出现于代码块中。
- 注释类占位（如「此处省略」「保持不变」）0 处。
- 所有视图、API、DTO 均提供完整可编译代码；每个 TypeScript 代码块的 import 语句完整列出所需依赖，无「后续补全」描述。

### 3. 类型一致性

- `AuditLogEntryDto.logId/operatorId/operatorName/operatorRole/sourceContext/action/resourceType/resourceId/requestSummary/responseStatus/ipAddress/userAgent/traceId/beforeSnapshot/afterSnapshot/occurredAt` 15 字段在 Task 1 与 Task 6 视图一致使用。
- `OperationLogDto` 字段在 Task 1 与 Task 6 操作日志表格一致。
- `CrossDomainAuditEntryDto` 字段在 Task 1 与 Task 6 跨域条目表格一致。
- `OperatorRole = 'Admin' | 'Operator' | 'Seller' | 'Buyer' | 'System'` 在 Task 1 与 Task 6 `roleColor` 函数一致。
- `ReconciliationStatusDto` 7 字段（hasRun/status/reportType/reconciledAt/discrepancyCount/isConsistent/alertTriggered/correctionTriggered）在 Task 1 与 Task 7 状态卡片一致。
- `ReconciliationRecordDto.discrepancies: ReconciliationDiscrepancyDto[]` 在 Task 1 与 Task 7 详情抽屉一致。
- `ReconciliationReportType` 7 值（OrderGmv/PaymentSuccessRate/PointsIssued/NotificationDelivery/AfterSalesVolume/ShopRanking/ConversionRate）在 Task 1、Task 3、Task 7 一致。
- `ReconciliationStatus = 'Consistent' | 'Discrepancy' | 'Failed'` 在 Task 1、Task 3、Task 7 一致。
- `OutboxSummaryDto.context/pendingCount/oldestEventAt/maxAgeMinutes/lastArchivedAt/status` 6 字段在 Task 1 与 Task 8 表格一致。
- `OutboxStatus = 'Normal' | 'Backlog' | 'Severe' | 'Archived'` 在 Task 1、Task 4、Task 8 一致。
- `BatchRepublishOutboxDto`/`ArchiveOutboxDto` 在 Task 1 与 Task 8 视图调用一致。
- `BatchRepublishResultDto`/`ArchiveOutboxResultDto` 在 Task 4 API 文件内定义并被 Task 8 视图 import 一致。
- 路由项 3 条 path/name 与 Task 5 routes.ts 完全对应：`audit-logs` / `reconciliation` / `outbox-monitor`。
- API 导出对象名 `auditLogsApi`/`reconciliationApi`/`outboxMonitorApi` 在 Task 2/3/4 与 Task 5 index.ts、各视图 import 一致。

### 4. 文件路径一致性

- 所有 Task 引用的文件路径与 File Structure 列表完全对应。
- 路由项 `component: () => import('../views/Xxx.vue')` 与 Task 6-8 创建的视图文件名一一对应（`AuditLogs.vue`/`Reconciliation.vue`/`OutboxMonitor.vue`）。
- `routes.ts` 引用的 3 个视图均在本 plan 范围内。

### 5. design-prompt 字段覆盖

- 审计日志（audit-logs.md §3）: `AuditLogEntryDto` 15 字段（LogId/OperatorId/OperatorName/OperatorRole/SourceContext/Action/ResourceType/ResourceId/RequestSummary/ResponseStatus/IpAddress/UserAgent/TraceId/BeforeSnapshot/AfterSnapshot/OccurredAt）+ 5 端点（audit-logs GET/GET{id}/export、operation-logs GET、audit-log-entries GET）✅
- 对账管理（reconciliation.md §3）: `ReconciliationStatusDto` 7 字段 + `ReconciliationRecordDto` 8 字段 + `ReconciliationDiscrepancyDto` 5 字段（reportType/metricName/expectedValue/actualValue/diffValue）+ 3 端点（reconciliation-status/reconcile/reconciliation-records）+ 7 报表类型枚举 ✅
- Outbox 监控（outbox-monitor.md §3）: `OutboxSummaryDto` 6 字段 + `OutboxTrendPointDto` 3 字段 + `OutboxMessageDto` 7 字段 + `OutboxArchiveHistoryDto` 4 字段 + `BatchRepublishOutboxDto`（messageIds/maxCount）+ `ArchiveOutboxDto`（olderThanMinutes/reason）+ 6 端点（summary/trend/messages/republish/archive/archive-history）✅

### 6. 审计日志只读 + 敏感字段掩码覆盖

| 要求 | 实现位置 | 状态 |
|-|-|-|
| 审计日志页无任何写端点 | Task 2 auditLogsApi 仅 5 个 GET 方法 | ✅ |
| 审计日志页无写按钮（无新增/编辑/删除） | Task 6 AuditLogs.vue 模板仅「查询」「导出 CSV」「详情」按钮 | ✅ |
| 敏感参数字段掩码展示 | Task 6 `SENSITIVE_KEY_PATTERN = /(password\|token\|secret\|api[_-]?key\|credential\|authorization)/i` + `maskSensitive()` 递归掩码函数 | ✅ |
| 掩码应用于请求摘要 | Task 6 `maskedSnapshot(currentDetail.requestSummary)` 传入 JsonViewer | ✅ |
| 掩码应用于操作前快照 | Task 6 `maskedSnapshot(currentDetail.beforeSnapshot)` 传入 JsonViewer | ✅ |
| 掩码应用于操作后快照 | Task 6 `maskedSnapshot(currentDetail.afterSnapshot)` 传入 JsonViewer | ✅ |
| 抽屉标题/快照标题提示「敏感字段已掩码」 | Task 6 模板 `.snapshot-title` 文案 | ✅ |
| 导出权限受 PermissionGuard 控制 | Task 6 `<PermissionGuard permission="audit-log:export">` 包裹导出按钮 | ✅ |

### 7. 危险操作确认流程覆盖

| 危险操作 | ConfirmDialog | danger | requireInput |
|-|-|-|-|
| 对账触发 | Task 7 `ConfirmDialog`（主色确认） | false | — |
| Outbox 重投 | Task 8 `ConfirmDialog`（主色确认） | false | — |
| Outbox 归档 | Task 8 `ConfirmDialog`（danger 红色 + 表单） | true | 归档阈值 + 归档原因（1-500 字）必填 |

所有写操作（对账触发、Outbox 重投、Outbox 归档）均通过对应 API 注入 `Idempotency-Key` 头（Task 3 reconciliationApi.trigger、Task 4 outboxMonitorApi.republish/archive 均使用 `withIdempotency()`），符合 spec §3.3 与 §5.7 要求。审计日志页无写操作，无需 Idempotency-Key。

---

## 任务清单汇总

- Task 1: 3 个 DTO 类型定义文件（audit-log/reconciliation/outbox）
- Task 2: audit-logs API + 单元测试（TDD：5 测试用例）
- Task 3: reconciliation API（getStatus/listRecords/trigger 含幂等键）
- Task 4: outbox-monitor API（summary/trend/messages/republish/archive/history 含幂等键）
- Task 5: routes.ts（3 路由项）+ index.ts 模块出口
- Task 6: AuditLogs.vue 审计日志视图（3 Tab + 筛选 + 表格 + 详情抽屉 + 敏感字段掩码 + 导出 CSV，只读）
- Task 7: Reconciliation.vue 对账管理视图（4 状态卡片 + 触发对账幂等 + 历史表格 + 差异项明细抽屉）
- Task 8: OutboxMonitor.vue Outbox 监控视图（统计条 + 趋势折线 + 按域表格 + 详情抽屉 + 重投/归档确认 + 60s 轮询）

**Task 总数：8**

**执行建议:** 按 Task 1 → 5 → 2 → 3 → 4 → 6 → 7 → 8 顺序执行；Task 2 严格走 TDD（先 spec 后实现）。每个 Task 完成后立即按其 commit step 提交。Task 6 是核心只读 + 敏感掩码页，需重点验证掩码正则覆盖 password/token/secret/apiKey/credential/authorization 六类键。
