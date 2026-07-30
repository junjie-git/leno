# 卖家管理后台 P1 批次 3（数据导出）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成 P1 批次 3 的 09-export 数据导出模块（1 页 + 3 个 API 端点 + mock handler + 路由注册），采用"仅 UI + BE-3 标记"策略，全量验证通过后提交推送。

**Architecture:** 延续 P0 五段式模块结构（`api/ + types/ + views/ + routes.ts + index.ts`），新增 `09-export` 模块。API 客户端完整实现 3 个方法（`createTask` / `listTasks` / `getDownloadUrl`），其中 `getDownloadUrl` 为同步返回字符串 URL 的辅助方法（不调用 axios），`createTask` / `listTasks` 走标准 `http` 客户端 + `.then(r => r.data)` 解包。Mock 层新增 `handlers/export.ts`：`createTask` 与 `download` 返回 HTTP 501 + BE-3 标记，`listTasks` 返回 200 + 空列表占位。`SalesExport.vue` 采用左右两栏布局（Row + Col，左 8/24 新建任务表单 + 右 16/24 历史任务列表），提交/下载触发 501 后 `message.warning('后端接口未就绪（BE-3）')`，并实现"有 Processing 任务时每 3 秒轮询"逻辑（当前 mock 返回空列表，轮询不实际触发）。

**Tech Stack:** Vue 3.5 + TypeScript 5.7 + Vite 6 + Ant Design Vue 4.2 + Pinia 2.3 + Vue Router 4.5 + axios 1.7 + dayjs 1.11 + Vitest 2.1 + axios-mock-adapter 2.1

---

## 关键设计决策（实施前必读）

1. **依赖批次 1 的 `http` 别名**：批次 1 在 `shared/http/index.ts` 追加了 `export { client as http } from './client'`。本批次所有 API 客户端使用 `import { http, withIdempotency } from '@/shared/http'`。Task 1 Step 1 含前置验证：若别名缺失则补一行，保证本批次可独立编译。
2. **BE-3 策略**：后端 3 个端点全部未实现。API 客户端完整实现（方法签名 + axios 调用），mock 拦截 `createTask` / `download` 返回 HTTP 501 + `{ code: 'BE-3', message: 'BE-3 待后端实现' }`，`listTasks` 返回 200 + 空列表。响应拦截器将 501 转为 `ServerError`，页面 catch 后统一 `message.warning('后端接口未就绪（BE-3）')`。
3. **`getDownloadUrl` 同步返回字符串**：该方法不调用 axios，仅拼接完整下载 URL `/api/seller/export/tasks/${taskId}/download`（含 `/api` 前缀，便于直接用于 `window.open`）。下载按钮点击后，页面将该 URL 去掉 `/api` 前缀交给 `http.get(path, { responseType: 'blob' })` 触发 mock 501，catch 后 warning。BE-3 就绪后该路径会返回真实文件流，页面用 Blob 触发浏览器下载。
4. **mock `listTasks` 返回空列表**：seed 中 `exportTasks` 初始化为空数组，`createTask` 返回 501 不写入 seed，故列表恒为空。右栏展示 `EmptyState`「暂无导出任务」。轮询逻辑代码完整实现，但因无 Processing 任务而不会实际启动定时器。
5. **状态 Tag 不扩展 `StatusTag` 组件**：`StatusTag` 当前无 `exportTask` 类型映射。为缩小 shared 改动范围，`SalesExport.vue` 内部定义 `statusMeta`（Processing→processing 蓝 / Completed→success 绿 / Failed→error 红），直接用 ant-design-vue `Tag` 渲染。
6. **测试用 `axios-mock-adapter` + `vitest`**：`export.api.spec.ts` 使用 `axios-mock-adapter` 挂载到真实 `client` 实例（含响应拦截器），验证 URL / method / params / body / `Idempotency-Key` 头 / 响应解包 / 501 错误转换。这与 mock handler 层的 axios-mock-adapter 栈一致，更接近真实行为。
7. **响应解包**：`createTask` / `listTasks` 内部 `.then(r => r.data)` 解包（响应拦截器已 unwrap `ApiResponse.data`）。mock reply 体须为 `{ code: 200, message, data }` envelope 形态。
8. **`formatDateTime` 复用**：复用 `@/shared/utils/format` 的 `formatDateTime`（已存在，签名 `formatDateTime(value: string | number | Date | null | undefined): string`）。
9. **验证命令工作目录**：除特别说明外，所有 `pnpm` 命令在 `/workspace/web/seller` 下执行。

---

## File Structure

### 新建文件
| 文件 | 职责 |
|------|------|
| `web/seller/src/modules/09-export/types/export.dto.ts` | 导出任务 DTO（ReportType / ExportFormat / ExportTaskStatus / CreateExportTaskDto / ExportTaskDto / ExportTaskQueryParams） |
| `web/seller/src/modules/09-export/api/export.api.ts` | 导出 API 客户端（3 方法：createTask / listTasks / getDownloadUrl） |
| `web/seller/src/modules/09-export/api/export.api.spec.ts` | 导出 API 测试（axios-mock-adapter） |
| `web/seller/src/modules/09-export/views/SalesExport.vue` | 销售报表导出页（左右两栏 + BE-3 提示 + 轮询） |
| `web/seller/src/modules/09-export/routes.ts` | 模块路由 |
| `web/seller/src/modules/09-export/index.ts` | 模块出口 |
| `web/seller/src/shared/http/mock/handlers/export.ts` | export mock handler（501 + 空列表） |

### 修改文件
| 文件 | 改动 |
|------|------|
| `web/seller/src/shared/http/index.ts` | （前置验证）确保 `http` 别名导出存在 |
| `web/seller/src/shared/http/mock/data/types.ts` | `MockSeed` 追加 `exportTasks: unknown[]` |
| `web/seller/src/shared/http/mock/data/seed.ts` | `ensureSeedData` 初始化 `exportTasks: []` |
| `web/seller/src/shared/http/mock/index.ts` | 注册 `registerExportHandlers` + 更新启动日志 |
| `web/seller/src/app/router.ts` | 注册 09-export 路由 |

---

## Task 1: 09-export 类型 + API 客户端 + spec

**Files:**
- Verify/Maybe modify: `web/seller/src/shared/http/index.ts`
- Create: `web/seller/src/modules/09-export/types/export.dto.ts`
- Create: `web/seller/src/modules/09-export/api/export.api.spec.ts`
- Create: `web/seller/src/modules/09-export/api/export.api.ts`

- [ ] **Step 1: 前置验证 `http` 别名存在**

读取 `web/seller/src/shared/http/index.ts`，确认是否已含 `export { client as http } from './client'`（批次 1 应已添加）。

若**已存在**：跳过本步，进入 Step 2。

若**缺失**：在 `export { client, withIdempotency } from './client'` 行之后追加一行：

```typescript
export { client as http } from './client'
```

修改后该文件前部应为：

```typescript
export { client, withIdempotency } from './client'
export { client as http } from './client'
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

export { setupMockAdapter } from './mock'
```

- [ ] **Step 2: 创建 export.dto.ts**

创建 `web/seller/src/modules/09-export/types/export.dto.ts`：

```typescript
/**
 * 09-export 数据导出 DTO
 *
 * 与后端 ExportController 对接（BE-3 待后端实现）：
 * - POST /api/seller/export/sales              创建导出任务（幂等）
 * - GET  /api/seller/export/tasks              查询导出任务列表
 * - GET  /api/seller/export/tasks/{id}/download 下载导出文件
 */

/** 报表类型 */
export type ReportType = 'SalesSummary' | 'OrderDetail' | 'ProductSales'

/** 导出格式 */
export type ExportFormat = 'Excel' | 'CSV'

/** 任务状态 */
export type ExportTaskStatus = 'Processing' | 'Completed' | 'Failed'

/** 创建导出任务 */
export interface CreateExportTaskDto {
  reportType: ReportType
  startDate: string
  endDate: string
  format: ExportFormat
}

/** 导出任务 */
export interface ExportTaskDto {
  id: string
  reportType: ReportType
  startDate: string
  endDate: string
  format: ExportFormat
  status: ExportTaskStatus
  recordCount?: number
  fileSize?: number
  downloadUrl?: string
  errorMessage?: string
  createdAt: string
  completedAt?: string
}

/** 任务查询参数 */
export interface ExportTaskQueryParams {
  page: number
  pageSize: number
  status?: ExportTaskStatus
}

/** 任务列表结果 */
export interface ExportTaskListResultDto {
  items: ExportTaskDto[]
  total: number
}
```

- [ ] **Step 3: 先写 export.api 失败测试（axios-mock-adapter）**

创建 `web/seller/src/modules/09-export/api/export.api.spec.ts`：

```typescript
import { describe, expect, it, beforeEach, afterAll } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client, ServerError } from '@/shared/http'
import { exportApi } from './export.api'
import type { CreateExportTaskDto, ExportTaskDto } from '../types/export.dto'

/**
 * exportApi 单元测试
 *
 * 使用 axios-mock-adapter 挂载到真实 client 实例（含响应拦截器），
 * 验证 URL / method / params / body / Idempotency-Key 头 / 响应解包 / 501 错误转换。
 *
 * mock reply 体须为 { code, message, data } envelope，响应拦截器 unwrap data 后
 * 由 api 函数内部 .then(r => r.data) 二次解包。
 */
const mock = new MockAdapter(client, { onNoMatch: 'throwException' })

const sampleTask: ExportTaskDto = {
  id: 'et-001',
  reportType: 'SalesSummary',
  startDate: '2026-07-01',
  endDate: '2026-07-30',
  format: 'Excel',
  status: 'Processing',
  createdAt: '2026-07-30T10:00:00Z',
}

beforeEach(() => {
  mock.reset()
})

afterAll(() => {
  mock.restore()
})

describe('exportApi.createTask', () => {
  const body: CreateExportTaskDto = {
    reportType: 'SalesSummary',
    startDate: '2026-07-01',
    endDate: '2026-07-30',
    format: 'Excel',
  }

  it('调用 POST /seller/export/sales 带 Idempotency-Key 并解包 data', async () => {
    mock
      .onPost('/seller/export/sales')
      .reply(200, { code: 200, message: 'OK', data: sampleTask })

    const result = await exportApi.createTask(body)

    expect(result).toEqual(sampleTask)
    expect(mock.history.post).toHaveLength(1)
    expect(mock.history.post[0].url).toBe('/seller/export/sales')
    expect(mock.history.post[0].data).toBe(JSON.stringify(body))
    expect(mock.history.post[0].headers['Idempotency-Key']).toBeTruthy()
  })

  it('后端返回 501 抛 ServerError（BE-3）', async () => {
    mock.onPost('/seller/export/sales').reply(501, {
      code: 'BE-3',
      message: 'BE-3 待后端实现：创建导出任务',
    })

    await expect(exportApi.createTask(body)).rejects.toBeInstanceOf(ServerError)
  })
})

describe('exportApi.listTasks', () => {
  it('调用 GET /seller/export/tasks 带 params 并解包', async () => {
    const payload = { items: [sampleTask], total: 1 }
    mock
      .onGet('/seller/export/tasks')
      .reply(200, { code: 200, message: 'OK', data: payload })

    const result = await exportApi.listTasks({ page: 1, pageSize: 20 })

    expect(result).toEqual(payload)
    expect(mock.history.get).toHaveLength(1)
    expect(mock.history.get[0].url).toBe('/seller/export/tasks')
    expect(mock.history.get[0].params).toEqual({ page: 1, pageSize: 20 })
  })

  it('支持 status 筛选参数', async () => {
    mock
      .onGet('/seller/export/tasks')
      .reply(200, { code: 200, message: 'OK', data: { items: [], total: 0 } })

    await exportApi.listTasks({ page: 1, pageSize: 20, status: 'Processing' })

    expect(mock.history.get[0].params).toEqual({
      page: 1,
      pageSize: 20,
      status: 'Processing',
    })
  })

  it('后端返回空列表时正确解包', async () => {
    mock
      .onGet('/seller/export/tasks')
      .reply(200, { code: 200, message: 'OK', data: { items: [], total: 0 } })

    const result = await exportApi.listTasks({ page: 1, pageSize: 50 })

    expect(result).toEqual({ items: [], total: 0 })
  })
})

describe('exportApi.getDownloadUrl', () => {
  it('返回完整下载 URL 字符串（含 /api 前缀）', () => {
    const url = exportApi.getDownloadUrl('et-001')
    expect(url).toBe('/api/seller/export/tasks/et-001/download')
  })

  it('同步返回字符串，非 Promise', () => {
    const url = exportApi.getDownloadUrl('et-002')
    expect(typeof url).toBe('string')
    expect(url).not.toBeInstanceOf(Promise)
  })

  it('不同 taskId 生成不同 URL', () => {
    expect(exportApi.getDownloadUrl('a')).toBe(
      '/api/seller/export/tasks/a/download',
    )
    expect(exportApi.getDownloadUrl('b')).toBe(
      '/api/seller/export/tasks/b/download',
    )
  })
})
```

- [ ] **Step 4: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/modules/09-export/api/export.api.spec.ts`
Expected: FAIL（`Cannot find module './export.api'`）

- [ ] **Step 5: 实现 export.api.ts**

创建 `web/seller/src/modules/09-export/api/export.api.ts`：

```typescript
import { http, withIdempotency } from '@/shared/http'
import type {
  CreateExportTaskDto,
  ExportTaskDto,
  ExportTaskListResultDto,
  ExportTaskQueryParams,
} from '../types/export.dto'

/**
 * 数据导出 API 客户端
 *
 * 与后端 ExportController 对接（BE-3 待后端实现）。响应拦截器已解包
 * ApiResponse.data，调用方拿到的就是业务负载：
 * - POST /api/seller/export/sales                创建导出任务（幂等）
 * - GET  /api/seller/export/tasks                查询导出任务列表
 * - GET  /api/seller/export/tasks/{id}/download  下载导出文件
 *
 * 注：getDownloadUrl 为同步辅助方法，仅拼接下载 URL 字符串（含 /api 前缀），
 * 不发起 HTTP 请求。调用方需自行用 http.get 或 window.open 触发下载。
 */
export const exportApi = {
  /** 创建导出任务（BE-3 待后端实现） */
  createTask(body: CreateExportTaskDto): Promise<ExportTaskDto> {
    return http
      .post<ExportTaskDto>('/seller/export/sales', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 查询导出任务列表（BE-3 待后端实现，mock 返回空列表占位） */
  listTasks(
    params: ExportTaskQueryParams,
  ): Promise<ExportTaskListResultDto> {
    return http
      .get<ExportTaskListResultDto>('/seller/export/tasks', { params })
      .then((r) => r.data)
  },

  /**
   * 构造下载导出文件的完整 URL（同步、非 Promise）
   *
   * 返回值含 `/api` 前缀，可直接用于 window.open；
   * 若需经 axios 调用（触发 mock / 走拦截器），请先去掉 `/api` 前缀再传给 http.get。
   */
  getDownloadUrl(taskId: string): string {
    return `/api/seller/export/tasks/${taskId}/download`
  },
}
```

- [ ] **Step 6: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/modules/09-export/api/export.api.spec.ts`
Expected: PASS（8 tests passed：createTask 2 + listTasks 3 + getDownloadUrl 3）

- [ ] **Step 7: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 8: 提交**

```bash
git add web/seller/src/shared/http/index.ts web/seller/src/modules/09-export/types/export.dto.ts web/seller/src/modules/09-export/api/export.api.ts web/seller/src/modules/09-export/api/export.api.spec.ts
git commit -m "feat(seller): add export DTO and API client for 09-export module"
```

---

## Task 2: export mock handler + seed 扩展 + 装配注册

**Files:**
- Modify: `web/seller/src/shared/http/mock/data/types.ts`
- Modify: `web/seller/src/shared/http/mock/data/seed.ts`
- Create: `web/seller/src/shared/http/mock/handlers/export.ts`
- Modify: `web/seller/src/shared/http/mock/index.ts`

- [ ] **Step 1: 扩展 MockSeed 类型 — 追加 exportTasks 字段**

修改 `web/seller/src/shared/http/mock/data/types.ts`，在 `MockSeed` 接口中追加 `exportTasks` 字段（保留批次 1/2 已加的 `shop` / `qualifications` / `freightTemplates` / `logisticsCompanies` / `reviews` 等字段，若已存在）。

修改后该文件应为：

```typescript
/**
 * Mock 种子数据聚合类型
 *
 * 各模块 DTO 以 unknown[] 占位，handler 内部按需断言。
 * 批次 1 追加 shop / qualifications；批次 2 追加 freightTemplates / logisticsCompanies / reviews；
 * 批次 3 追加 exportTasks。
 */
export interface MockSeed {
  menus: unknown[]
  onlineUsers: unknown[]
  loginLogs: unknown[]
  redisKeys: unknown[]
  redisInfo: unknown
  keyspaces: unknown[]
  serverSnapshot: unknown
  serverHistory: { cpu: unknown[]; memory: unknown[]; diskIo: unknown[] }
  shop: unknown
  qualifications: unknown[]
  freightTemplates: unknown[]
  logisticsCompanies: unknown[]
  reviews: unknown[]
  exportTasks: unknown[]
  nextId: number
}
```

> **注**：若批次 1/2 执行时未添加 `shop` / `freightTemplates` 等字段，按报错提示相应补齐；本批次只确保 `exportTasks: unknown[]` 存在。最小化做法：仅在 `MockSeed` 末尾（`nextId` 之前）追加 `exportTasks: unknown[]` 一行。

- [ ] **Step 2: 扩展 seed.ts — 初始化 exportTasks 为空数组**

修改 `web/seller/src/shared/http/mock/data/seed.ts` 的 `ensureSeedData` 函数内 seed 初始化对象，在 `nextId: 1000` 之前追加一行 `exportTasks: []`。

若批次 1/2 已扩展 seed 对象，定位到 seed 对象末尾（`nextId` 之前），追加：

```typescript
    exportTasks: [],
```

修改后 seed 对象片段（示意，保留既有字段）：

```typescript
  const seed: MockSeed = {
    menus: buildMenuSeed(),
    onlineUsers: buildOnlineUserSeed(),
    loginLogs: buildLoginLogSeed(),
    redisKeys: buildRedisKeySeed(),
    redisInfo: buildRedisInfoSeed(),
    keyspaces: buildKeyspaceSeed(),
    serverSnapshot: buildServerSnapshotSeed(),
    serverHistory: { cpu: [], memory: [], diskIo: [] },
    // 批次 1/2 字段（若已存在）
    shop: buildShopSeed ? buildShopSeed() : null,
    qualifications: buildQualificationSeed ? buildQualificationSeed() : [],
    freightTemplates: buildFreightTemplateSeed ? buildFreightTemplateSeed() : [],
    logisticsCompanies: buildLogisticsCompanySeed ? buildLogisticsCompanySeed() : [],
    reviews: buildReviewSeed ? buildReviewSeed() : [],
    // 批次 3 新增
    exportTasks: [],
    nextId: 1000,
  }
```

> **实施提示**：上述片段含条件判断仅为说明"兼容批次 1/2 可能存在的 builder"。实际执行时，请直接在 seed 对象的 `nextId` 行之前插入 `exportTasks: [],` 一行，**不要**引入 `buildShopSeed ?` 这类条件三元（会因函数未定义而 ReferenceError）。若批次 1/2 的字段已就位，保持原样不动，仅追加 `exportTasks: []`。

最小化修改（推荐）：在 `ensureSeedData` 的 seed 对象中，将：

```typescript
    nextId: 1000,
  }
```

改为：

```typescript
    exportTasks: [],
    nextId: 1000,
  }
```

- [ ] **Step 3: 实现 handlers/export.ts**

创建 `web/seller/src/shared/http/mock/handlers/export.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData } from '../data/seed'

/**
 * 数据导出 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/export/...）：
 * - POST /seller/export/sales                创建导出任务 → 501（BE-3）
 * - GET  /seller/export/tasks                查询任务列表 → 200 空列表占位
 * - GET  /seller/export/tasks/{id}/download  下载导出文件 → 501（BE-3）
 *
 * BE-3 策略：createTask 与 download 返回 HTTP 501 + BE-3 标记，
 * 响应拦截器转为 ServerError；listTasks 返回 200 + 空列表，
 * 页面据此展示 EmptyState 并不触发轮询。
 */
export function registerExportHandlers(mock: MockAdapter): void {
  // 创建导出任务（BE-3）
  mock.onPost('/seller/export/sales').reply(() => {
    return [
      501,
      {
        code: 'BE-3',
        message: 'BE-3 待后端实现：创建导出任务',
      },
    ]
  })

  // 查询导出任务列表（BE-3：返回空列表占位，便于页面渲染空状态）
  mock.onGet('/seller/export/tasks').reply(() => {
    const seed = loadSeedData()
    const items = (seed.exportTasks as any[]) ?? []
    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          items,
          total: items.length,
        },
      },
    ]
  })

  // 下载导出文件（BE-3）
  mock.onGet(/\/seller\/export\/tasks\/[^/]+\/download$/).reply(() => {
    return [
      501,
      {
        code: 'BE-3',
        message: 'BE-3 待后端实现：下载导出文件',
      },
    ]
  })
}
```

- [ ] **Step 4: 在 mock/index.ts 注册 export handler**

修改 `web/seller/src/shared/http/mock/index.ts`：

4a. 在 import 区追加（在最后一个 `register*Handlers` import 之后）：

```typescript
import { registerExportHandlers } from './handlers/export'
```

4b. 在 `setupMockAdapter` 函数内，最后一个 `registerXxxHandlers(mock)` 调用之后追加一行：

```typescript
  registerExportHandlers(mock)
```

4c. 将启动日志行更新为（P1 全部 5 个新 handler 完成后预期值）：

```typescript
  console.log('[Mock] 已启用 10 个 handler，共 36 个 endpoint')
```

> **数字说明**：P0 基线 5 handler / 19 endpoint + 批次 1 shop 1/5 + 批次 2 freight+logistics+review 3/9 + 批次 3 export 1/3 = 10 handler / 36 endpoint。若批次 1/2 实际数字有出入，请按实际累加调整此处的两个数字。

修改后 `setupMockAdapter` 函数注册区片段（示意，保留批次 1/2 已有调用）：

```typescript
  registerMenuHandlers(mock)
  registerOnlineUserHandlers(mock)
  registerLoginLogHandlers(mock)
  registerCacheHandlers(mock)
  registerServerMonitorHandlers(mock)
  registerShopHandlers(mock) // 批次 1（若已存在）
  registerFreightHandlers(mock) // 批次 2（若已存在）
  registerLogisticsHandlers(mock) // 批次 2（若已存在）
  registerReviewHandlers(mock) // 批次 2（若已存在）
  registerExportHandlers(mock) // 批次 3 新增

  // 未匹配的请求透传到真实后端
  mock.onAny().passThrough()

  // 启动日志
  console.log('[Mock] 已启用 10 个 handler，共 36 个 endpoint')
```

> **实施提示**：若批次 1/2 的 handler 注册调用名称不同（如 `registerShopHandler` 单数），按实际名称保留；本批次只确保新增 `registerExportHandlers(mock)` 一行并更新日志数字。

- [ ] **Step 5: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 6: 提交**

```bash
git add web/seller/src/shared/http/mock/data/types.ts web/seller/src/shared/http/mock/data/seed.ts web/seller/src/shared/http/mock/handlers/export.ts web/seller/src/shared/http/mock/index.ts
git commit -m "feat(seller): add export mock handler with BE-3 501 markers and empty task list"
```

---

## Task 3: SalesExport.vue — 销售报表导出（左右两栏 + BE-3 + 轮询）

**Files:**
- Create: `web/seller/src/modules/09-export/views/SalesExport.vue`

- [ ] **Step 1: 实现 SalesExport.vue**

创建 `web/seller/src/modules/09-export/views/SalesExport.vue`：

```vue
<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted, onUnmounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Row,
  Col,
  Card,
  Form,
  FormItem,
  Select,
  RadioGroup,
  RadioButton,
  RangePicker,
  Table,
  Tag,
  Button,
  Skeleton,
  Tooltip,
  Space,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import type { Dayjs } from 'dayjs'
import { exportApi } from '../api/export.api'
import type {
  ReportType,
  ExportFormat,
  ExportTaskStatus,
  ExportTaskDto,
  CreateExportTaskDto,
} from '../types/export.dto'
import { http } from '@/shared/http'
import { IdempotencyButton, EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 销售报表导出页
 *
 * 路由 /export/sales，权限 export:sales
 * 3 个 API 端点全部 BE-3 标记：
 * - 提交新建任务 → createTask → mock 501 → message.warning('后端接口未就绪（BE-3）')
 * - 下载已完成任务 → getDownloadUrl + http.get → mock 501 → message.warning
 * - 历史任务列表 → listTasks → mock 200 空列表 → EmptyState
 *
 * 轮询：有 Processing 状态任务时每 3 秒刷新列表（当前 mock 返回空列表，不触发）。
 */

const loading = ref(false)
const submitting = ref(false)
const tasks = ref<ExportTaskDto[]>([])

const form = reactive<{
  reportType: ReportType
  dateRange: [Dayjs, Dayjs] | null
  format: ExportFormat
}>({
  reportType: 'SalesSummary',
  dateRange: null,
  format: 'Excel',
})

const reportTypeOptions: Array<{ label: string; value: ReportType }> = [
  { label: '销售汇总', value: 'SalesSummary' },
  { label: '订单明细', value: 'OrderDetail' },
  { label: '商品销量', value: 'ProductSales' },
]

const reportTypeLabels: Record<ReportType, string> = {
  SalesSummary: '销售汇总',
  OrderDetail: '订单明细',
  ProductSales: '商品销量',
}

const statusMeta: Record<ExportTaskStatus, { color: string; label: string }> = {
  Processing: { color: 'processing', label: '处理中' },
  Completed: { color: 'success', label: '已完成' },
  Failed: { color: 'error', label: '失败' },
}

const columns: TableColumnsType = [
  { title: '类型', dataIndex: 'reportType', key: 'reportType', width: 120 },
  { title: '时间范围', key: 'range', width: 200 },
  { title: '格式', dataIndex: 'format', key: 'format', width: 90 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110 },
  { title: '记录数', dataIndex: 'recordCount', key: 'recordCount', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'action', width: 140 },
]

const hasProcessing = computed(() =>
  tasks.value.some((t) => t.status === 'Processing'),
)

let pollTimer: ReturnType<typeof setTimeout> | null = null

function schedulePoll(): void {
  if (pollTimer !== null) return
  pollTimer = setTimeout(async () => {
    pollTimer = null
    await loadTasks(true)
    if (hasProcessing.value) schedulePoll()
  }, 3000)
}

function stopPoll(): void {
  if (pollTimer !== null) {
    clearTimeout(pollTimer)
    pollTimer = null
  }
}

watch(hasProcessing, (v) => {
  if (v) schedulePoll()
  else stopPoll()
})

async function loadTasks(silent = false): Promise<void> {
  if (!silent) loading.value = true
  try {
    const res = await exportApi.listTasks({ page: 1, pageSize: 50 })
    tasks.value = res.items
  } catch (e) {
    logger.error('加载导出任务列表失败', e)
    if (!silent) message.error('加载导出任务列表失败')
  } finally {
    if (!silent) loading.value = false
  }
}

function onDateRangeChange(dates: [Dayjs, Dayjs] | null): void {
  form.dateRange = dates
  if (dates && dates[0] && dates[1]) {
    const diffDays = dates[1].diff(dates[0], 'day')
    if (diffDays > 90) {
      message.error('时间范围不能超过 90 天')
      form.dateRange = null
    }
    if (diffDays < 0) {
      message.error('结束时间不能早于开始时间')
      form.dateRange = null
    }
  }
}

function buildBody(): CreateExportTaskDto | null {
  if (!form.dateRange || !form.dateRange[0] || !form.dateRange[1]) {
    return null
  }
  return {
    reportType: form.reportType,
    startDate: form.dateRange[0].format('YYYY-MM-DD'),
    endDate: form.dateRange[1].format('YYYY-MM-DD'),
    format: form.format,
  }
}

async function onSubmit(): Promise<void> {
  const body = buildBody()
  if (!body) {
    message.warning('请选择时间范围')
    return
  }
  submitting.value = true
  try {
    await exportApi.createTask(body)
    // BE-3 就绪后：创建成功，刷新列表
    await loadTasks()
    message.success('导出任务已创建，请稍后在右侧列表查看进度')
  } catch (e) {
    logger.warn('创建导出任务失败（BE-3）', e)
    message.warning('后端接口未就绪（BE-3）')
  } finally {
    submitting.value = false
  }
}

async function onDownload(task: ExportTaskDto): Promise<void> {
  const fullUrl = exportApi.getDownloadUrl(task.id)
  // axios baseURL=/api，故去掉 /api 前缀再交给 http.get，以命中 mock 拦截
  const axiosPath = fullUrl.replace(/^\/api/, '')
  try {
    const res = await http.get<Blob>(axiosPath, { responseType: 'blob' })
    // BE-3 就绪后：用 Blob 触发浏览器下载
    const blobUrl = URL.createObjectURL(res.data)
    const a = document.createElement('a')
    a.href = blobUrl
    a.download = `${task.reportType}-${task.id}.${
      task.format === 'Excel' ? 'xlsx' : 'csv'
    }`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(blobUrl)
  } catch (e) {
    logger.warn('下载导出文件失败（BE-3）', e)
    message.warning('后端接口未就绪（BE-3）')
  }
}

async function onRetry(task: ExportTaskDto): Promise<void> {
  submitting.value = true
  try {
    await exportApi.createTask({
      reportType: task.reportType,
      startDate: task.startDate,
      endDate: task.endDate,
      format: task.format,
    })
    await loadTasks()
    message.success('重试任务已创建')
  } catch (e) {
    logger.warn('重试导出任务失败（BE-3）', e)
    message.warning('后端接口未就绪（BE-3）')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadTasks()
})

onUnmounted(() => {
  stopPoll()
})
</script>

<template>
  <div class="sales-export-page">
    <Breadcrumb class="sales-export-breadcrumb">
      <BreadcrumbItem>数据导出</BreadcrumbItem>
      <BreadcrumbItem>销售报表</BreadcrumbItem>
    </Breadcrumb>

    <Row :gutter="16" class="sales-export-row">
      <!-- 左栏：新建导出任务 -->
      <Col :span="8">
        <Card class="sales-export-card" :bordered="true">
          <template #title>
            <span class="sales-export-card-title">新建导出任务</span>
          </template>
          <Form layout="vertical" :label-col="{ style: { width: '100px' } }">
            <FormItem label="报表类型" required>
              <Select
                v-model:value="form.reportType"
                :options="reportTypeOptions"
                placeholder="请选择报表类型"
              />
            </FormItem>
            <FormItem label="时间范围" required>
              <RangePicker
                :value="form.dateRange"
                style="width: 100%"
                :allow-clear="true"
                @change="onDateRangeChange"
              />
              <div class="sales-export-hint">单次导出时间范围不能超过 90 天</div>
            </FormItem>
            <FormItem label="导出格式" required>
              <RadioGroup v-model:value="form.format">
                <RadioButton value="Excel">Excel</RadioButton>
                <RadioButton value="CSV">CSV</RadioButton>
              </RadioGroup>
            </FormItem>
            <FormItem>
              <IdempotencyButton
                :loading="submitting"
                block
                @click="onSubmit"
              >
                创建导出任务
              </IdempotencyButton>
            </FormItem>
          </Form>
          <div class="sales-export-be3-tip">
            后端导出接口未就绪（BE-3），提交后将提示"后端接口未就绪"。
          </div>
        </Card>
      </Col>

      <!-- 右栏：历史任务列表 -->
      <Col :span="16">
        <Card class="sales-export-card" :bordered="true">
          <template #title>
            <span class="sales-export-card-title">历史任务列表</span>
          </template>
          <template #extra>
            <Button size="small" @click="loadTasks()">刷新</Button>
          </template>

          <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
          <EmptyState
            v-else-if="tasks.length === 0"
            description="暂无导出任务"
          />
          <Table
            v-else
            :columns="columns"
            :data-source="tasks"
            row-key="id"
            :pagination="false"
            size="middle"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'reportType'">
                {{ reportTypeLabels[record.reportType as ReportType] || record.reportType }}
              </template>
              <template v-else-if="column.key === 'range'">
                {{ record.startDate }} ~ {{ record.endDate }}
              </template>
              <template v-else-if="column.key === 'format'">
                {{ record.format }}
              </template>
              <template v-else-if="column.key === 'status'">
                <Tag :color="statusMeta[record.status as ExportTaskStatus].color">
                  {{ statusMeta[record.status as ExportTaskStatus].label }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'recordCount'">
                {{ record.recordCount ?? '—' }}
              </template>
              <template v-else-if="column.key === 'createdAt'">
                {{ formatDateTime(record.createdAt) }}
              </template>
              <template v-else-if="column.key === 'action'">
                <Space>
                  <Button
                    v-if="record.status === 'Completed'"
                    type="link"
                    size="small"
                    @click="onDownload(record as ExportTaskDto)"
                  >
                    下载
                  </Button>
                  <Tooltip
                    v-if="record.status === 'Failed'"
                    :title="record.errorMessage || '任务失败，可重试'"
                  >
                    <Button
                      type="link"
                      size="small"
                      :loading="submitting"
                      @click="onRetry(record as ExportTaskDto)"
                    >
                      重试
                    </Button>
                  </Tooltip>
                  <span
                    v-if="record.status === 'Processing'"
                    class="sales-export-processing-text"
                  >
                    处理中…
                  </span>
                </Space>
              </template>
            </template>
          </Table>
        </Card>
      </Col>
    </Row>
  </div>
</template>

<style scoped>
.sales-export-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.sales-export-breadcrumb {
  font-size: 14px;
}
.sales-export-row {
  align-items: stretch;
}
.sales-export-card {
  border-radius: 8px;
  height: 100%;
}
.sales-export-card-title {
  font-size: 15px;
  font-weight: 500;
}
.sales-export-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}
.sales-export-be3-tip {
  margin-top: 12px;
  padding: 8px 12px;
  background: #fffbe6;
  border: 1px solid #ffe58f;
  border-radius: 6px;
  font-size: 12px;
  color: #ad6800;
  line-height: 1.6;
}
.sales-export-processing-text {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/09-export/views/SalesExport.vue
git commit -m "feat(seller): add SalesExport page with two-column layout and BE-3 markers"
```

---

## Task 4: 09-export routes.ts + index.ts

**Files:**
- Create: `web/seller/src/modules/09-export/routes.ts`
- Create: `web/seller/src/modules/09-export/index.ts`

- [ ] **Step 1: 实现 routes.ts**

创建 `web/seller/src/modules/09-export/routes.ts`：

```typescript
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/export/sales',
    name: 'export.sales',
    component: () => import('./views/SalesExport.vue'),
    meta: {
      title: '销售报表',
      menuKey: 'export.sales',
      roles: ['Seller'],
      permission: 'export:sales',
      menuGroup: '09-export',
    },
  },
]

export default routes
```

- [ ] **Step 2: 实现 index.ts**

创建 `web/seller/src/modules/09-export/index.ts`：

```typescript
export { default } from './routes'
export { exportApi } from './api/export.api'
```

- [ ] **Step 3: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors（SalesExport.vue 已存在，懒加载可解析）

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/modules/09-export/routes.ts web/seller/src/modules/09-export/index.ts
git commit -m "feat(seller): add 09-export module routes and entry"
```

---

## Task 5: app/router.ts 注册 09-export 路由

**Files:**
- Modify: `web/seller/src/app/router.ts`

- [ ] **Step 1: 添加 09-export 路由 import**

修改 `web/seller/src/app/router.ts`，在模块路由 import 区（`import account from '@/modules/08-account/routes'` 之后）追加：

```typescript
import exportModule from '@/modules/09-export/routes'
```

> **命名说明**：使用 `exportModule` 而非 `export`，避免与 JS 保留字 `export` 冲突。

修改后 import 区顺序为（示意，保留批次 1/2 已有 import）：

```typescript
// 模块路由
import onboarding from '@/modules/01-onboarding/routes' // 批次 1（若已存在）
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import logistics from '@/modules/04-logistics/routes' // 批次 2（若已存在）
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
import review from '@/modules/07-review/routes' // 批次 2（若已存在）
import account from '@/modules/08-account/routes'
import exportModule from '@/modules/09-export/routes'
```

> **实施提示**：若批次 1/2 的 import 名称或顺序不同，按实际保留；本批次只确保新增 `import exportModule from '@/modules/09-export/routes'` 一行。

- [ ] **Step 2: 将 09-export 路由注入 BasicLayout children**

在 `app/router.ts` 的 BasicLayout `children` 数组中，`...account` 之后追加 `...exportModule`：

```typescript
    children: [
      { path: '', redirect: '/dashboard/overview' },
      ...onboarding, // 批次 1（若已存在）
      ...dashboard,
      ...product,
      ...logistics, // 批次 2（若已存在）
      ...order,
      ...afterSales,
      ...review, // 批次 2（若已存在）
      ...account,
      ...exportModule,
    ],
```

> **实施提示**：若批次 1/2 的展开项不存在，保持原 children 不动，仅在 `...account` 之后追加 `...exportModule,` 一行。

- [ ] **Step 3: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/app/router.ts
git commit -m "feat(seller): register 09-export sales route"
```

---

## Task 6: 全量验证 + 提交推送

**Files:**
- 无（仅验证与推送）

- [ ] **Step 1: Lint 全量检查**

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 2: TypeCheck 全量检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 3: 全量单元测试**

Run (cwd: `web/seller`): `pnpm test`
Expected: 全部通过（P0/P1 既有用例 + 本批次新增 `export.api.spec.ts` 8 个用例全部 PASS）

- [ ] **Step 4: 生产构建**

Run (cwd: `web/seller`): `pnpm build`
Expected: 构建成功（`vue-tsc --noEmit` 通过 + `vite build` 产出 `dist`）

- [ ] **Step 5: 推送到远程仓库**

```bash
git push origin dev
```
Expected: 推送成功，远程 `origin/dev` 包含本批次全部 6 个 commit。

- [ ] **Step 6: 人工冒烟（可选，mock 模式）**

启动 `VITE_USE_MOCK=true pnpm dev`，访问 `/export/sales` 验证：
- 左栏「新建导出任务」表单：报表类型 Select（销售汇总/订单明细/商品销量）+ 时间范围 RangePicker + 格式 Radio（Excel/CSV）+ 创建按钮
- 时间范围选超过 90 天 → `message.error('时间范围不能超过 90 天')` 并清空
- 不选时间范围点提交 → `message.warning('请选择时间范围')`
- 选合法范围点提交 → mock 501 → `message.warning('后端接口未就绪（BE-3）')`
- 右栏「历史任务列表」展示 `EmptyState`「暂无导出任务」（mock 返回空列表）
- 点「刷新」按钮 → 重新请求 listTasks → 仍为空列表
- 路由 `/export/sales` 可访问，菜单高亮正确

---

## Self-Review（计划自检）

**1. Spec 覆盖检查（对照批次 3 范围 6 项）**

| 批次 3 范围项 | 覆盖 Task |
|---|---|
| 1. 09-export 模块：types/export.dto.ts | Task 1 Step 2 |
| 2. 09-export 模块：api/export.api.ts + spec（3 端点，BE-3 标记） | Task 1 Step 3-5（spec + api），3 端点 createTask/listTasks/getDownloadUrl |
| 3. 09-export 模块：SalesExport.vue（左右两栏 + BE-3 提示） | Task 3 |
| 4. Mock handler：handlers/export.ts（501 + BE-3）+ seed 扩展 | Task 2 |
| 5. 路由更新：app/router.ts 注册 09-export 路由 | Task 4（模块 routes）+ Task 5（app/router 注入） |
| 6. 全量验证 + 提交推送 | Task 6 |

无遗漏。补充项：`http` 别名前置验证（Task 1 Step 1）；`MockSeed` 类型 + seed 初始化 `exportTasks`（Task 2 Step 1-2）。

**2. 占位符扫描**

- 全文未出现 `TODO`/`FIXME`/`...省略`/`Similar to Task` 等占位符。
- 所有 Vue SFC 含完整 `<script setup lang="ts">` + `<template>` + `<style scoped>`。
- 所有 API / mock / 组件代码为可直接编译运行的完整实现。
- Task 2 / Task 5 中含"示意片段 + 实施提示"，是为兼容批次 1/2 可能的执行差异，每处均给出"最小化修改"的明确指令（追加一行），非占位符。

**3. 类型一致性检查**

- `ReportType` / `ExportFormat` / `ExportTaskStatus` / `CreateExportTaskDto` / `ExportTaskDto` / `ExportTaskQueryParams` / `ExportTaskListResultDto` 在 `export.dto.ts`（Task 1）定义，被 `export.api.ts`、`export.api.spec.ts`、`SalesExport.vue` 一致引用。
- `exportApi` 方法名（`createTask`/`listTasks`/`getDownloadUrl`）在 API、测试、页面中一致。
- `getDownloadUrl` 返回 `/api/seller/export/tasks/${taskId}/download`（含 `/api` 前缀），与 `SalesExport.vue` 下载逻辑 `fullUrl.replace(/^\/api/, '')` 一致；mock handler 拦截 `/seller/export/tasks/{id}/download`（不含 `/api`，因 client baseURL=/api）一致。
- `ExportTaskStatus` 含 `Processing`/`Completed`/`Failed`，与 `SalesExport.vue` 的 `statusMeta` 三项一致。
- `ExportTaskListResultDto` 形态 `{ items, total }` 与 mock `listTasks` 返回 `data: { items, total: items.length }` 一致，与 `loadTasks` 中 `res.items` 解构一致。
- mock `createTask` / `download` 返回 501，响应拦截器转 `ServerError`，与 spec 测试 `rejects.toBeInstanceOf(ServerError)` 一致，与页面 catch 块 `message.warning('后端接口未就绪（BE-3）')` 一致。

**4. 已知限制**

- `listTasks` mock 恒返回空列表（`createTask` 返回 501 不写入 seed），故右栏始终展示 `EmptyState`，下载/重试按钮不会实际渲染。轮询逻辑代码完整但因无 Processing 任务而不会启动定时器。BE-3 后端就绪后需补 seed 写入与状态流转逻辑。
- `getDownloadUrl` 同步返回字符串，下载需页面自行用 `http.get` 触发；BE-3 就绪后该路径返回 Blob，页面已实现 Blob 下载逻辑。
- 未扩展 `StatusTag` 组件的 export 类型映射（页面内用 Tag 实现）；若后续需统一状态展示，可在 `StatusTag.vue` 追加 `exportTask` 类型并迁移。

---

## 执行交接

计划已完成并保存至 `docs/superpowers/plans/2026-07-30-seller-admin-p1-batch3-export.md`。两种执行方式可选：

**1. Subagent 驱动（推荐）** — 每个 Task 派发独立 subagent，任务间审查，迭代快速。

**2. 内联执行** — 在当前会话使用 executing-plans 批量执行，设检查点审查。

选择哪种方式？
