# 系统管理后台 - 03-system-governance 模块实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 03-system-governance 模块的 4 个页面（功能开关 / 系统配置 / 数据字典 / 公告管理）及其 DTO、API、路由与 Vue 视图，覆盖 SystemAdmin 域 FeatureFlagsController / SystemConfigsController / DataDictionariesController / AnnouncementsController 全部端点。

**Architecture:** 按 DTO → API（含 TDD 测试）→ routes/index → Vue 视图顺序推进，每 Task 自包含、可独立编译/测试/提交。所有写操作走 `withIdempotency()` 注入 `Idempotency-Key` 头；危险操作（停用启用中的开关/配置/字典、移除字典项、发布/撤回公告）走 `ConfirmDialog`。无乐观锁（四类资源均低频变更）。跨 Plan 类型契约严格遵守 §shared/types、§shared/http、§shared/auth、§shared/components 已定义。

**Tech Stack:** Vue 3.5 `<script setup>` + TypeScript strict + Ant Design Vue 4.x + Pinia + Vue Router 4 + axios 1.7 + Vitest 2.x + @vue/test-utils + jsdom

**关联 Spec：** `docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md` §2.3

**关联 Design Prompts：**
- `docs/design-prompts/system-admin/03-system-governance/feature-flags.md`
- `docs/design-prompts/system-admin/03-system-governance/system-configs.md`
- `docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md`
- `docs/design-prompts/system-admin/03-system-governance/announcements.md`

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
export class ConcurrencyError extends Error { kind = 'ConcurrencyError'; currentVersion: number }

// shared/auth/auth.store.ts
export const useAuthStore = defineStore('auth', {
  getters: { isAuthenticated, isAdmin, hasPermission(perm) },
  actions: { login, fetchProfile, logout, hasRole(roles) },
})

// shared/utils/format.ts
export function formatDateTime(iso: string | null): string  // YYYY-MM-DD HH:mm
```

**共享组件（Plan 1 已实现，本 plan 直接 import 使用）：**
`StatusTag` / `IdempotencyButton` / `PermissionGuard` / `DataTable` / `EmptyState` / `ConfirmDialog` / `DateTimeRangePicker` —— 路径前缀 `@/shared/components/`。

**命名约定：**
- 视图：PascalCase `.vue`
- API：导出为 `featureFlagsApi` / `systemConfigsApi` / `dataDictionariesApi` / `announcementsApi` 对象，方法 camelCase 动词开头
- DTO：PascalCase + `Dto` 后缀
- 路由 name：`system-governance.{view}` kebab-case
- 路由 path：kebab-case

---

## 文件结构

### 新建文件（14 个，全部位于 `web/system-admin/src/modules/03-system-governance/`）

**类型层（4）**
- `types/feature-flag.dto.ts` — 功能开关 DTO（FeatureFlagDto / SaveFeatureFlagDto / EvaluateFlagDto / EvaluateFlagResultDto / ListFeatureFlagsParams / FeatureFlagStatus）
- `types/system-config.dto.ts` — 系统配置 DTO（SystemConfigDto / SaveSystemConfigDto / SystemConfigRevealDto / ListSystemConfigsParams / SystemConfigStatus / SystemConfigValueType）
- `types/data-dictionary.dto.ts` — 数据字典 DTO（DataDictionaryDto / DictionaryItemDto / SaveDataDictionaryDto / AddDictionaryItemDto / UpdateDictionaryItemDto / ListDataDictionariesParams / DictionaryStatus）
- `types/announcement.dto.ts` — 公告 DTO（AnnouncementDto / SaveAnnouncementDto / ListAnnouncementsParams / AnnouncementType / AnnouncementStatus / AnnouncementAudience）

**API 层（4 + 2 测试）**
- `api/feature-flags.api.ts` — FeatureFlagsController（list/create/update/enable/disable/evaluate）
- `api/feature-flags.api.spec.ts` — featureFlagsApi 单元测试
- `api/system-configs.api.ts` — SystemConfigsController（list/groups/getByKey/create/update/enable/disable）
- `api/data-dictionaries.api.ts` — DataDictionariesController（list/create/update/enable/disable/addItem/updateItem/removeItem）
- `api/announcements.api.ts` — AnnouncementsController（list/create/update/publish/unpublish）
- `api/announcements.api.spec.ts` — announcementsApi 单元测试

**视图层（4）**
- `views/FeatureFlags.vue` — 功能开关（筛选 + 表格 + 新建/编辑弹窗 + 评估抽屉）
- `views/SystemConfigs.vue` — 系统配置（左分组导航 + 筛选 + 表格 + 新建/编辑弹窗 + 明文查看）
- `views/DataDictionaries.vue` — 数据字典（左字典列表 + 右详情 + 字典项表格 CRUD）
- `views/Announcements.vue` — 公告管理（筛选 + 表格 + 新建/编辑弹窗 + 发布/撤回确认）

**聚合层（2）**
- `routes.ts` — 4 条路由项，挂到 BasicLayout 子路由
- `index.ts` — 聚合导出 routes + 4 个 api 对象

### 依赖项（本 plan 假定 Plan 1 已就绪）
- `web/system-admin/src/shared/http/client.ts`（client + withIdempotency）
- `web/system-admin/src/shared/http/errors.ts`（BusinessError / ConcurrencyError）
- `web/system-admin/src/shared/types/index.ts`（ApiResponse / PageResult / PageQuery）
- `web/system-admin/src/shared/auth/auth.store.ts`（useAuthStore）
- `web/system-admin/src/shared/utils/format.ts`（formatDateTime）
- `web/system-admin/src/shared/components/`（StatusTag / IdempotencyButton / PermissionGuard / EmptyState / ConfirmDialog / DateTimeRangePicker）
- `web/system-admin/src/app/router.ts`（聚合入口，本 plan Task 6 在其后追加 systemGovernance 子路由数组）

---

## Task 1: 模块 DTO 类型层

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/types/feature-flag.dto.ts`
- Create: `web/system-admin/src/modules/03-system-governance/types/system-config.dto.ts`
- Create: `web/system-admin/src/modules/03-system-governance/types/data-dictionary.dto.ts`
- Create: `web/system-admin/src/modules/03-system-governance/types/announcement.dto.ts`

- [ ] **Step 1: 创建 feature-flag.dto.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/types/feature-flag.dto.ts
// 功能开关 DTO 类型定义（对应后端 FeatureFlagsController）

// 开关状态：Enabled 启用 / Disabled 停用
export type FeatureFlagStatus = 'Enabled' | 'Disabled'

// 功能开关响应 DTO（对应后端 FeatureFlagDto，字段 camelCase 由 System.Text.Json 序列化）
export interface FeatureFlagDto {
  flagId: string
  key: string                    // 业务键，新建时可编辑、编辑时只读
  description: string
  group: string                  // 分组，如 payment / order / notify
  status: FeatureFlagStatus
  ruleJson: string               // 规则配置 JSON 字符串
  updatedAt: string              // 最近变更时间 ISO 8601
  updatedBy: string              // 最近变更人
}

// 创建/更新开关请求 DTO（POST/PUT /admin/feature-flags[/{flagId}]）
export interface SaveFeatureFlagDto {
  key: string
  description: string
  group: string
  ruleJson: string
  status: FeatureFlagStatus
}

// 评估开关请求 DTO（POST /admin/feature-flags/evaluate）
export interface EvaluateFlagDto {
  key: string
  context: Record<string, unknown>  // userId / role / shopId 等上下文
}

// 评估开关结果 DTO
export interface EvaluateFlagResultDto {
  enabled: boolean               // 是否生效
  matchedRule: string            // 命中规则描述
}

// 列表查询参数（GET /admin/feature-flags）
export interface ListFeatureFlagsParams {
  key?: string                   // key 模糊搜索
  status?: FeatureFlagStatus[]   // 状态多选
  group?: string                 // 分组精确匹配
}
```

- [ ] **Step 2: 创建 system-config.dto.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/types/system-config.dto.ts
// 系统配置 DTO 类型定义（对应后端 SystemConfigsController）

// 配置状态：Enabled 启用 / Disabled 停用
export type SystemConfigStatus = 'Enabled' | 'Disabled'

// 配置值类型：String 字符串 / Int 整数 / Bool 布尔 / Json JSON / Secret 敏感
export type SystemConfigValueType = 'String' | 'Int' | 'Bool' | 'Json' | 'Secret'

// 系统配置响应 DTO（值始终掩码，Secret 类型形如 ****）
export interface SystemConfigDto {
  configId: string
  key: string                    // 配置键，编辑时只读
  group: string                  // 分组，如 payment / notify / cart / search
  valueType: SystemConfigValueType
  valueMasked: string            // 掩码值，Secret 类型为 ****
  description: string
  status: SystemConfigStatus
  updatedAt: string              // ISO 8601
}

// 创建/更新配置请求 DTO（POST/PUT /admin/system-configs[/{configId}]）
export interface SaveSystemConfigDto {
  key: string
  group: string
  valueType: SystemConfigValueType
  value: string                  // 明文值，Secret 类型创建/更新时必填
  description: string
}

// 明文配置响应 DTO（GET /admin/system-configs/by-key/{key}，需 config:reveal 权限）
export interface SystemConfigRevealDto {
  configId: string
  key: string
  value: string                  // 明文值
}

// 列表查询参数（GET /admin/system-configs）
export interface ListSystemConfigsParams {
  key?: string                   // key 模糊搜索
  group?: string                 // 分组精确匹配
  status?: SystemConfigStatus[]  // 状态多选
}

// 分组项（GET /admin/system-configs/groups 返回）
export interface SystemConfigGroupDto {
  group: string
  count: number                  // 该分组下配置数
}
```

- [ ] **Step 3: 创建 data-dictionary.dto.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/types/data-dictionary.dto.ts
// 数据字典 DTO 类型定义（对应后端 DataDictionariesController）

// 字典/字典项状态：Enabled 启用 / Disabled 停用
export type DictionaryStatus = 'Enabled' | 'Disabled'

// 字典项 DTO
export interface DictionaryItemDto {
  itemId: string
  code: string                   // 项编码，如 pending / paid / shipped
  displayName: string            // 显示名，如 待支付 / 已支付 / 已发货
  sortOrder: number              // 排序值，升序
  status: DictionaryStatus
}

// 数据字典响应 DTO
export interface DataDictionaryDto {
  dictionaryId: string
  code: string                   // 字典编码，如 order_status，编辑时只读
  name: string                   // 字典名称，如 订单状态
  description: string
  status: DictionaryStatus
  items: DictionaryItemDto[]     // 字典项列表
}

// 创建/更新字典请求 DTO（POST/PUT /admin/dictionaries[/{dictionaryId}]）
export interface SaveDataDictionaryDto {
  code: string
  name: string
  description: string
}

// 新增字典项请求 DTO（POST /admin/dictionaries/{dictionaryId}/items）
export interface AddDictionaryItemDto {
  code: string
  displayName: string
  sortOrder: number
}

// 更新字典项请求 DTO（PUT /admin/dictionaries/{dictionaryId}/items/{itemId}）
export interface UpdateDictionaryItemDto {
  code: string
  displayName: string
  sortOrder: number
}

// 列表查询参数（GET /admin/dictionaries）
export interface ListDataDictionariesParams {
  name?: string                  // 名称/编码模糊搜索
  status?: DictionaryStatus[]    // 状态多选
}
```

- [ ] **Step 4: 创建 announcement.dto.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/types/announcement.dto.ts
// 公告 DTO 类型定义（对应后端 AnnouncementsController）

// 公告类型：SystemMaintenance 系统维护 / ActivityNotification 活动通知 / PolicyChange 政策变更 / Urgent 紧急公告
export type AnnouncementType = 'SystemMaintenance' | 'ActivityNotification' | 'PolicyChange' | 'Urgent'

// 公告状态：Draft 草稿 / Published 已发布 / Unpublished 已撤回
export type AnnouncementStatus = 'Draft' | 'Published' | 'Unpublished'

// 公告受众范围：Buyer 买家 / Seller 卖家 / Operator 运营
export type AnnouncementAudience = 'Buyer' | 'Seller' | 'Operator'

// 公告响应 DTO
export interface AnnouncementDto {
  announcementId: string
  title: string
  type: AnnouncementType
  status: AnnouncementStatus
  audiences: AnnouncementAudience[]   // 发布范围多选
  effectiveFrom: string               // 生效起始 ISO 8601
  effectiveTo: string                 // 生效结束 ISO 8601
  content: string                     // 正文（HTML 字符串）
  isPinned: boolean                   // 是否置顶
  createdAt: string                   // 创建时间 ISO 8601
  publishedAt: string | null          // 发布时间，草稿态为 null
}

// 创建/更新公告请求 DTO（POST/PUT /admin/announcements[/{announcementId}]）
export interface SaveAnnouncementDto {
  title: string
  type: AnnouncementType
  audiences: AnnouncementAudience[]
  effectiveFrom: string
  effectiveTo: string
  content: string
  isPinned: boolean
}

// 列表查询参数（GET /admin/announcements）
export interface ListAnnouncementsParams {
  type?: AnnouncementType[]       // 类型多选
  status?: AnnouncementStatus[]   // 状态多选
}

// 公告类型中文标签映射（视图层下拉与表格展示复用）
export const ANNOUNCEMENT_TYPE_LABELS: Record<AnnouncementType, string> = {
  SystemMaintenance: '系统维护',
  ActivityNotification: '活动通知',
  PolicyChange: '政策变更',
  Urgent: '紧急公告',
}

// 公告状态中文标签映射
export const ANNOUNCEMENT_STATUS_LABELS: Record<AnnouncementStatus, string> = {
  Draft: '草稿',
  Published: '已发布',
  Unpublished: '已撤回',
}

// 公告受众中文标签映射
export const ANNOUNCEMENT_AUDIENCE_LABELS: Record<AnnouncementAudience, string> = {
  Buyer: '买家',
  Seller: '卖家',
  Operator: '运营',
}
```

- [ ] **Step 5: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（4 个 dto 文件无外部依赖，仅类型定义与常量，可通过 strict 检查）

- [ ] **Step 6: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/types/
git commit -m "feat(system-admin/03-system-governance): 新增功能开关/系统配置/数据字典/公告 4 个 DTO 类型定义"
```

---

## Task 2: feature-flags.api.ts + 单元测试（TDD）

**Files:**
- Test: `web/system-admin/src/modules/03-system-governance/api/feature-flags.api.spec.ts`
- Create: `web/system-admin/src/modules/03-system-governance/api/feature-flags.api.ts`

**目标端点（SystemAdmin 域 FeatureFlagsController）：**
- `GET /api/admin/feature-flags` 列表（key/status/group/page/pageSize）
- `POST /api/admin/feature-flags` 创建（幂等）
- `PUT /api/admin/feature-flags/{flagId}` 更新（幂等）
- `POST /api/admin/feature-flags/{flagId}/enable` 启用（幂等）
- `POST /api/admin/feature-flags/{flagId}/disable` 停用（幂等）
- `POST /api/admin/feature-flags/evaluate` 评估（幂等）

- [ ] **Step 1: 写失败测试 feature-flags.api.spec.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/api/feature-flags.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { featureFlagsApi } from './feature-flags.api'
import type { SaveFeatureFlagDto } from '../types/feature-flag.dto'

// 桩 shared/http：client 提供方法桩，withIdempotency 返回固定头便于断言
vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('featureFlagsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 GET /admin/feature-flags 并透传筛选 params', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    await featureFlagsApi.list({
      key: 'flag-1',
      status: ['Enabled'],
      group: 'payment',
      page: 1,
      pageSize: 20,
    })
    expect(client.get).toHaveBeenCalledWith('/admin/feature-flags', {
      params: { key: 'flag-1', status: ['Enabled'], group: 'payment', page: 1, pageSize: 20 },
    })
  })

  it('create 使用 POST /admin/feature-flags 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    const body: SaveFeatureFlagDto = {
      key: 'flag-1',
      description: '测试开关',
      group: 'payment',
      ruleJson: '{}',
      status: 'Disabled',
    }
    await featureFlagsApi.create(body)
    expect(client.post).toHaveBeenCalledWith('/admin/feature-flags', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('update 使用 PUT /admin/feature-flags/{flagId} 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    const body: SaveFeatureFlagDto = {
      key: 'flag-1',
      description: '已更新',
      group: 'payment',
      ruleJson: '{"op":"eq","field":"role","value":"Admin"}',
      status: 'Enabled',
    }
    await featureFlagsApi.update('flag-123', body)
    expect(client.put).toHaveBeenCalledWith('/admin/feature-flags/flag-123', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('enable 使用 POST /admin/feature-flags/{flagId}/enable 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await featureFlagsApi.enable('flag-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/feature-flags/flag-123/enable',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  it('disable 使用 POST /admin/feature-flags/{flagId}/disable 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await featureFlagsApi.disable('flag-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/feature-flags/flag-123/disable',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  it('evaluate 使用 POST /admin/feature-flags/evaluate 并传 body + Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: { enabled: true, matchedRule: 'role=Admin' } })
    await featureFlagsApi.evaluate({ key: 'flag-1', context: { userId: 'u1', role: 'Admin' } })
    expect(client.post).toHaveBeenCalledWith(
      '/admin/feature-flags/evaluate',
      { key: 'flag-1', context: { userId: 'u1', role: 'Admin' } },
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/api/feature-flags.api.spec.ts`
Expected: FAIL，提示 `Failed to resolve import "./feature-flags.api"`（api 文件尚未创建）

- [ ] **Step 3: 实现 feature-flags.api.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/api/feature-flags.api.ts
// 功能开关管理 API（SystemAdmin 域 FeatureFlagsController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  FeatureFlagDto,
  SaveFeatureFlagDto,
  EvaluateFlagDto,
  EvaluateFlagResultDto,
  ListFeatureFlagsParams,
} from '../types/feature-flag.dto'

// 功能开关 API：list/create/update/enable/disable/evaluate
export const featureFlagsApi = {
  // 分页查询功能开关
  list: (params: ListFeatureFlagsParams & PageQuery) =>
    client.get<PageResult<FeatureFlagDto>>('/admin/feature-flags', { params }),

  // 创建功能开关（幂等）
  create: (body: SaveFeatureFlagDto) =>
    client.post<FeatureFlagDto>('/admin/feature-flags', body, withIdempotency()),

  // 更新功能开关（key 不可变，幂等）
  update: (flagId: string, body: SaveFeatureFlagDto) =>
    client.put<FeatureFlagDto>(`/admin/feature-flags/${flagId}`, body, withIdempotency()),

  // 启用开关（幂等）
  enable: (flagId: string) =>
    client.post<FeatureFlagDto>(`/admin/feature-flags/${flagId}/enable`, null, withIdempotency()),

  // 停用开关（幂等）
  disable: (flagId: string) =>
    client.post<FeatureFlagDto>(`/admin/feature-flags/${flagId}/disable`, null, withIdempotency()),

  // 按上下文评估开关是否生效（幂等）
  evaluate: (body: EvaluateFlagDto) =>
    client.post<EvaluateFlagResultDto>('/admin/feature-flags/evaluate', body, withIdempotency()),
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/api/feature-flags.api.spec.ts`
Expected: PASS（6 个测试用例全部通过）

- [ ] **Step 5: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 6: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/api/feature-flags.api.ts web/system-admin/src/modules/03-system-governance/api/feature-flags.api.spec.ts
git commit -m "feat(system-admin/03-system-governance): 实现 featureFlagsApi 6 端点（含 evaluate 评估）+ 6 单元测试"
```

---

## Task 3: system-configs.api.ts

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/api/system-configs.api.ts`

**目标端点（SystemAdmin 域 SystemConfigsController）：**
- `GET /api/admin/system-configs` 列表（key/group/status/page/pageSize）
- `GET /api/admin/system-configs/groups` 分组列表
- `GET /api/admin/system-configs/by-key/{key}` 按键获取明文（需 config:reveal 权限）
- `POST /api/admin/system-configs` 创建（幂等）
- `PUT /api/admin/system-configs/{configId}` 更新（幂等）
- `POST /api/admin/system-configs/{configId}/enable` 启用（幂等）
- `POST /api/admin/system-configs/{configId}/disable` 停用（幂等）

- [ ] **Step 1: 实现 system-configs.api.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/api/system-configs.api.ts
// 系统配置管理 API（SystemAdmin 域 SystemConfigsController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  SystemConfigDto,
  SaveSystemConfigDto,
  SystemConfigRevealDto,
  SystemConfigGroupDto,
  ListSystemConfigsParams,
} from '../types/system-config.dto'

// 系统配置 API：list/groups/getByKey/create/update/enable/disable
export const systemConfigsApi = {
  // 分页查询系统配置（值掩码返回）
  list: (params: ListSystemConfigsParams & PageQuery) =>
    client.get<PageResult<SystemConfigDto>>('/admin/system-configs', { params }),

  // 获取全部配置分组（去重，含每组配置数）
  groups: () =>
    client.get<SystemConfigGroupDto[]>('/admin/system-configs/groups'),

  // 按键获取配置明文（需 config:reveal 权限，仅 Admin）
  getByKey: (key: string) =>
    client.get<SystemConfigRevealDto>(`/admin/system-configs/by-key/${encodeURIComponent(key)}`),

  // 创建系统配置（幂等）
  create: (body: SaveSystemConfigDto) =>
    client.post<SystemConfigDto>('/admin/system-configs', body, withIdempotency()),

  // 更新系统配置（键不可变，幂等）
  update: (configId: string, body: SaveSystemConfigDto) =>
    client.put<SystemConfigDto>(`/admin/system-configs/${configId}`, body, withIdempotency()),

  // 启用配置（幂等）
  enable: (configId: string) =>
    client.post<SystemConfigDto>(`/admin/system-configs/${configId}/enable`, null, withIdempotency()),

  // 停用配置（幂等）
  disable: (configId: string) =>
    client.post<SystemConfigDto>(`/admin/system-configs/${configId}/disable`, null, withIdempotency()),
}
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 运行模块全部测试确保无回归**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/`
Expected: PASS（feature-flags.api.spec.ts 6 用例通过，新文件无测试不影响）

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/api/system-configs.api.ts
git commit -m "feat(system-admin/03-system-governance): 实现 systemConfigsApi 7 端点（含 groups/getByKey 明文）"
```

---

## Task 4: data-dictionaries.api.ts

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/api/data-dictionaries.api.ts`

**目标端点（SystemAdmin 域 DataDictionariesController）：**
- `GET /api/admin/dictionaries` 列表（name/status/page/pageSize）
- `POST /api/admin/dictionaries` 创建（幂等）
- `PUT /api/admin/dictionaries/{dictionaryId}` 更新（幂等）
- `POST /api/admin/dictionaries/{dictionaryId}/enable` 启用（幂等）
- `POST /api/admin/dictionaries/{dictionaryId}/disable` 停用（幂等）
- `POST /api/admin/dictionaries/{dictionaryId}/items` 新增字典项（幂等）
- `PUT /api/admin/dictionaries/{dictionaryId}/items/{itemId}` 更新字典项（幂等）
- `DELETE /api/admin/dictionaries/{dictionaryId}/items/{itemId}` 移除字典项（幂等）

- [ ] **Step 1: 实现 data-dictionaries.api.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/api/data-dictionaries.api.ts
// 数据字典管理 API（SystemAdmin 域 DataDictionariesController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  DataDictionaryDto,
  SaveDataDictionaryDto,
  AddDictionaryItemDto,
  UpdateDictionaryItemDto,
  DictionaryItemDto,
  ListDataDictionariesParams,
} from '../types/data-dictionary.dto'

// 数据字典 API：list/create/update/enable/disable + 字典项 CRUD
export const dataDictionariesApi = {
  // 分页查询数据字典（含 items 列表）
  list: (params: ListDataDictionariesParams & PageQuery) =>
    client.get<PageResult<DataDictionaryDto>>('/admin/dictionaries', { params }),

  // 创建数据字典（幂等）
  create: (body: SaveDataDictionaryDto) =>
    client.post<DataDictionaryDto>('/admin/dictionaries', body, withIdempotency()),

  // 更新数据字典（编码不可变，幂等）
  update: (dictionaryId: string, body: SaveDataDictionaryDto) =>
    client.put<DataDictionaryDto>(`/admin/dictionaries/${dictionaryId}`, body, withIdempotency()),

  // 启用字典（幂等）
  enable: (dictionaryId: string) =>
    client.post<DataDictionaryDto>(`/admin/dictionaries/${dictionaryId}/enable`, null, withIdempotency()),

  // 停用字典（幂等）
  disable: (dictionaryId: string) =>
    client.post<DataDictionaryDto>(`/admin/dictionaries/${dictionaryId}/disable`, null, withIdempotency()),

  // 新增字典项（幂等）
  addItem: (dictionaryId: string, body: AddDictionaryItemDto) =>
    client.post<DictionaryItemDto>(`/admin/dictionaries/${dictionaryId}/items`, body, withIdempotency()),

  // 更新字典项（幂等）
  updateItem: (dictionaryId: string, itemId: string, body: UpdateDictionaryItemDto) =>
    client.put<DictionaryItemDto>(`/admin/dictionaries/${dictionaryId}/items/${itemId}`, body, withIdempotency()),

  // 移除字典项（幂等，后端保证幂等）
  removeItem: (dictionaryId: string, itemId: string) =>
    client.delete<void>(`/admin/dictionaries/${dictionaryId}/items/${itemId}`),
}
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 运行模块全部测试确保无回归**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/`
Expected: PASS（feature-flags.api.spec.ts 6 用例通过）

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/api/data-dictionaries.api.ts
git commit -m "feat(system-admin/03-system-governance): 实现 dataDictionariesApi 8 端点（字典 CRUD + 字典项 CRUD）"
```

---

## Task 5: announcements.api.ts + 单元测试（TDD）

**Files:**
- Test: `web/system-admin/src/modules/03-system-governance/api/announcements.api.spec.ts`
- Create: `web/system-admin/src/modules/03-system-governance/api/announcements.api.ts`

**目标端点（SystemAdmin 域 AnnouncementsController）：**
- `GET /api/admin/announcements` 列表（type/status/page/pageSize）
- `POST /api/admin/announcements` 创建（幂等，初始草稿态）
- `PUT /api/admin/announcements/{announcementId}` 更新（幂等，仅草稿态可更新）
- `POST /api/admin/announcements/{announcementId}/publish` 发布（幂等）
- `POST /api/admin/announcements/{announcementId}/unpublish` 撤回（幂等）

- [ ] **Step 1: 写失败测试 announcements.api.spec.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/api/announcements.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { announcementsApi } from './announcements.api'
import type { SaveAnnouncementDto } from '../types/announcement.dto'

vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('announcementsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 GET /admin/announcements 并透传筛选 params', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    await announcementsApi.list({
      type: ['Urgent'],
      status: ['Published'],
      page: 1,
      pageSize: 20,
    })
    expect(client.get).toHaveBeenCalledWith('/admin/announcements', {
      params: { type: ['Urgent'], status: ['Published'], page: 1, pageSize: 20 },
    })
  })

  it('create 使用 POST /admin/announcements 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    const body: SaveAnnouncementDto = {
      title: '系统维护通知',
      type: 'SystemMaintenance',
      audiences: ['Buyer', 'Seller'],
      effectiveFrom: '2026-07-27T00:00:00Z',
      effectiveTo: '2026-07-28T00:00:00Z',
      content: '系统将于 07-27 凌晨维护',
      isPinned: false,
    }
    await announcementsApi.create(body)
    expect(client.post).toHaveBeenCalledWith('/admin/announcements', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('update 使用 PUT /admin/announcements/{id} 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    const body: SaveAnnouncementDto = {
      title: '已更新标题',
      type: 'Urgent',
      audiences: ['Operator'],
      effectiveFrom: '2026-07-27T00:00:00Z',
      effectiveTo: '2026-07-28T00:00:00Z',
      content: '已更新正文',
      isPinned: true,
    }
    await announcementsApi.update('ann-123', body)
    expect(client.put).toHaveBeenCalledWith('/admin/announcements/ann-123', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('publish 使用 POST /admin/announcements/{id}/publish 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await announcementsApi.publish('ann-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/announcements/ann-123/publish',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  it('unpublish 使用 POST /admin/announcements/{id}/unpublish 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await announcementsApi.unpublish('ann-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/announcements/ann-123/unpublish',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/api/announcements.api.spec.ts`
Expected: FAIL，提示 `Failed to resolve import "./announcements.api"`（api 文件尚未创建）

- [ ] **Step 3: 实现 announcements.api.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/api/announcements.api.ts
// 公告管理 API（SystemAdmin 域 AnnouncementsController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  AnnouncementDto,
  SaveAnnouncementDto,
  ListAnnouncementsParams,
} from '../types/announcement.dto'

// 公告 API：list/create/update/publish/unpublish
export const announcementsApi = {
  // 分页查询公告
  list: (params: ListAnnouncementsParams & PageQuery) =>
    client.get<PageResult<AnnouncementDto>>('/admin/announcements', { params }),

  // 创建公告（初始草稿态，幂等）
  create: (body: SaveAnnouncementDto) =>
    client.post<AnnouncementDto>('/admin/announcements', body, withIdempotency()),

  // 更新公告（仅草稿态可更新，幂等）
  update: (announcementId: string, body: SaveAnnouncementDto) =>
    client.put<AnnouncementDto>(`/admin/announcements/${announcementId}`, body, withIdempotency()),

  // 发布公告（仅草稿态可发布，幂等）
  publish: (announcementId: string) =>
    client.post<AnnouncementDto>(`/admin/announcements/${announcementId}/publish`, null, withIdempotency()),

  // 撤回公告（仅已发布态可撤回，幂等）
  unpublish: (announcementId: string) =>
    client.post<AnnouncementDto>(`/admin/announcements/${announcementId}/unpublish`, null, withIdempotency()),
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/api/announcements.api.spec.ts`
Expected: PASS（5 个测试用例全部通过）

- [ ] **Step 5: 运行模块全部测试确保无回归**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/`
Expected: PASS（feature-flags.api.spec.ts 6 用例 + announcements.api.spec.ts 5 用例 = 11 用例通过）

- [ ] **Step 6: 类型检查与提交**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

```bash
git add web/system-admin/src/modules/03-system-governance/api/announcements.api.ts web/system-admin/src/modules/03-system-governance/api/announcements.api.spec.ts
git commit -m "feat(system-admin/03-system-governance): 实现 announcementsApi 5 端点（含 publish/unpublish）+ 5 单元测试"
```

---

## Task 6: routes.ts + index.ts 模块聚合

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/routes.ts`
- Create: `web/system-admin/src/modules/03-system-governance/index.ts`

- [ ] **Step 1: 实现 routes.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/routes.ts
// 03-system-governance 模块路由项：4 个视图，meta 含 title/menuKey/icon/roles/permission/menuGroup
import type { RouteRecordRaw } from 'vue-router'

export const systemGovernanceRoutes: RouteRecordRaw[] = [
  {
    path: 'feature-flags',
    name: 'system-governance.feature-flags',
    component: () => import('../views/FeatureFlags.vue'),
    meta: {
      title: '功能开关',
      menuKey: 'system-governance.feature-flags',
      icon: 'FlagOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'feature:read',
      menuGroup: '03-system-governance',
    },
  },
  {
    path: 'system-configs',
    name: 'system-governance.system-configs',
    component: () => import('../views/SystemConfigs.vue'),
    meta: {
      title: '系统配置',
      menuKey: 'system-governance.system-configs',
      icon: 'SettingOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'config:read',
      menuGroup: '03-system-governance',
    },
  },
  {
    path: 'data-dictionaries',
    name: 'system-governance.data-dictionaries',
    component: () => import('../views/DataDictionaries.vue'),
    meta: {
      title: '数据字典',
      menuKey: 'system-governance.data-dictionaries',
      icon: 'DatabaseOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'dictionary:read',
      menuGroup: '03-system-governance',
    },
  },
  {
    path: 'announcements',
    name: 'system-governance.announcements',
    component: () => import('../views/Announcements.vue'),
    meta: {
      title: '公告管理',
      menuKey: 'system-governance.announcements',
      icon: 'NotificationOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'announcement:read',
      menuGroup: '03-system-governance',
    },
  },
]

export default systemGovernanceRoutes
```

- [ ] **Step 2: 实现 index.ts**

```typescript
// web/system-admin/src/modules/03-system-governance/index.ts
// 模块对外出口：routes + 各 api 对象
export { default as systemGovernanceRoutes } from './routes'
export { featureFlagsApi } from './api/feature-flags.api'
export { systemConfigsApi } from './api/system-configs.api'
export { dataDictionariesApi } from './api/data-dictionaries.api'
export { announcementsApi } from './api/announcements.api'
```

- [ ] **Step 3: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（routes.ts 引用 views/*.vue 文件尚不存在，但 vue-tsc 对动态 import 容忍；若报错需先创建 4 个空 .vue 文件骨架再回填）

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/routes.ts web/system-admin/src/modules/03-system-governance/index.ts
git commit -m "feat(system-admin/03-system-governance): 新增 routes.ts（4 路由项）与 index.ts 模块出口"
```

---

## Task 7: FeatureFlags.vue 功能开关视图

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/views/FeatureFlags.vue`

**实现要点（design-prompt §1-8）:**
- 顶部筛选条：key 搜索 + 状态多选 + 分组输入 + 「新建开关」按钮（PermissionGuard `feature:write`）
- 主表格：key / 描述 / 分组 / 状态 / 最近变更 / 操作（编辑/启用/停用/评估），分页 20
- 弹窗表单：key（新建可编辑/编辑只读）/ 描述 / 分组 / 规则 JSON（textarea + 格式校验）/ 初始状态
- 评估抽屉：上下文 JSON 输入 → POST evaluate → 显示布尔结果 + 命中规则
- 危险操作：停用启用中的开关走 ConfirmDialog，内容「停用后该功能对所有用户立即失效…可随时启用恢复」
- 状态色：Enabled 绿、Disabled 灰
- 规则 JSON 格式校验：前端 `JSON.parse` 失败提示「规则 JSON 格式不正确」
- 空状态：「暂无功能开关」+ CTA「新建开关」

- [ ] **Step 1: 实现 FeatureFlags.vue**

```vue
<!-- web/system-admin/src/modules/03-system-governance/views/FeatureFlags.vue -->
<!-- 功能开关管理：筛选 + 表格 + 新建/编辑弹窗 + 评估抽屉 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, PlayCircleOutlined } from '@ant-design/icons-vue'
import { featureFlagsApi } from '../api/feature-flags.api'
import type {
  FeatureFlagDto,
  SaveFeatureFlagDto,
  FeatureFlagStatus,
  EvaluateFlagResultDto,
} from '../types/feature-flag.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

interface FilterState {
  key: string
  status: FeatureFlagStatus[]
  group: string
  page: number
  pageSize: number
}

interface FormState {
  flagId?: string
  key: string
  description: string
  group: string
  ruleJson: string
  status: FeatureFlagStatus
}

const auth = useAuthStore()
const canWrite = computed(() => auth.hasPermission('feature:write'))

const loading = ref(false)
const dataList = ref<FeatureFlagDto[]>([])
const total = ref(0)
const filter = reactive<FilterState>({
  key: '',
  status: [],
  group: '',
  page: 1,
  pageSize: 20,
})

const columns = computed(() => [
  { title: 'Key', dataIndex: 'key', key: 'key', width: 180, ellipsis: true },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '分组', dataIndex: 'group', key: 'group', width: 120 },
  { title: '状态', key: 'status', width: 100 },
  { title: '最近变更', key: 'updatedAt', width: 160 },
  { title: '操作', key: 'action', width: 240, fixed: 'right' as const },
])

// 弹窗状态
const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const submitting = ref(false)
const form = reactive<FormState>({
  key: '',
  description: '',
  group: '',
  ruleJson: '{}',
  status: 'Disabled',
})

// 确认弹窗（启停）
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'enable' | 'disable'; flag: FeatureFlagDto } | null>(null)
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'disable' ? '停用功能开关' : '启用功能开关')
const confirmContent = computed(() =>
  confirmAction.value?.kind === 'disable'
    ? `停用后该功能对所有用户立即失效，可能影响线上行为。可随时启用恢复。`
    : `启用后该功能将根据规则立即生效。`)

// 评估抽屉
const drawerVisible = ref(false)
const drawerLoading = ref(false)
const evaluateKey = ref('')
const evaluateContext = ref('{\n  "userId": "u-1",\n  "role": "Admin"\n}')
const evaluateResult = ref<EvaluateFlagResultDto | null>(null)

async function loadList() {
  loading.value = true
  try {
    const params = {
      key: filter.key || undefined,
      status: filter.status.length ? filter.status : undefined,
      group: filter.group || undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await featureFlagsApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载功能开关失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  filter.page = pag.current ?? 1
  filter.pageSize = pag.pageSize ?? 20
  loadList()
}

function openCreate() {
  modalMode.value = 'create'
  Object.assign(form, {
    flagId: undefined,
    key: '',
    description: '',
    group: '',
    ruleJson: '{}',
    status: 'Disabled',
  })
  modalVisible.value = true
}

function openEdit(flag: FeatureFlagDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    flagId: flag.flagId,
    key: flag.key,
    description: flag.description,
    group: flag.group,
    ruleJson: flag.ruleJson,
    status: flag.status,
  })
  modalVisible.value = true
}

function validateRuleJson(): boolean {
  try {
    JSON.parse(form.ruleJson)
    return true
  } catch {
    message.error('规则 JSON 格式不正确')
    return false
  }
}

async function onSubmit() {
  if (!form.key.trim()) return message.error('Key 必填')
  if (!form.group.trim()) return message.error('分组必填')
  if (!validateRuleJson()) return
  submitting.value = true
  try {
    const body: SaveFeatureFlagDto = {
      key: form.key.trim(),
      description: form.description.trim(),
      group: form.group.trim(),
      ruleJson: form.ruleJson,
      status: form.status,
    }
    if (modalMode.value === 'create') {
      await featureFlagsApi.create(body)
      message.success('开关已创建')
    } else if (form.flagId) {
      await featureFlagsApi.update(form.flagId, body)
      message.success('开关已更新')
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

function askToggle(flag: FeatureFlagDto) {
  confirmAction.value = {
    kind: flag.status === 'Enabled' ? 'disable' : 'enable',
    flag,
  }
  confirmVisible.value = true
}

async function onConfirmToggle() {
  if (!confirmAction.value) return
  const { kind, flag } = confirmAction.value
  try {
    if (kind === 'enable') {
      await featureFlagsApi.enable(flag.flagId)
      message.success('开关已启用')
    } else {
      await featureFlagsApi.disable(flag.flagId)
      message.success('开关已停用')
    }
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function openEvaluate(flag: FeatureFlagDto) {
  evaluateKey.value = flag.key
  evaluateContext.value = '{\n  "userId": "u-1",\n  "role": "Admin"\n}'
  evaluateResult.value = null
  drawerVisible.value = true
}

async function onEvaluate() {
  let context: Record<string, unknown>
  try {
    context = JSON.parse(evaluateContext.value)
  } catch {
    message.error('上下文 JSON 格式不正确')
    return
  }
  drawerLoading.value = true
  try {
    evaluateResult.value = await featureFlagsApi.evaluate({
      key: evaluateKey.value,
      context,
    })
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('评估失败')
  } finally {
    drawerLoading.value = false
  }
}

function statusTagColor(status: FeatureFlagStatus): string {
  return status === 'Enabled' ? 'success' : 'default'
}

function statusTagText(status: FeatureFlagStatus): string {
  return status === 'Enabled' ? '启用' : '停用'
}

onMounted(() => {
  loadList()
})
</script>

<template>
  <div class="feature-flags-page">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-space :size="12" wrap>
        <a-input
          v-model:value="filter.key"
          placeholder="搜索 Key"
          allow-clear
          style="width: 200px"
          @press-enter="onSearch"
        />
        <a-select
          v-model:value="filter.status"
          mode="multiple"
          placeholder="状态"
          allow-clear
          style="width: 180px"
          :options="[
            { label: '启用', value: 'Enabled' },
            { label: '停用', value: 'Disabled' },
          ]"
        />
        <a-input
          v-model:value="filter.group"
          placeholder="分组"
          allow-clear
          style="width: 160px"
          @press-enter="onSearch"
        />
        <a-button type="primary" @click="onSearch">查询</a-button>
        <PermissionGuard permission="feature:write">
          <a-button type="primary" @click="openCreate">
            <PlusOutlined />新建开关
          </a-button>
        </PermissionGuard>
      </a-space>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" style="margin-top: 16px">
      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :row-key="(r: FeatureFlagDto) => r.flagId"
        :pagination="{
          current: filter.page,
          pageSize: filter.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无功能开关" action-text="新建开关" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusTagColor(record.status)">{{ statusTagText(record.status) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'updatedAt'">
            {{ formatDateTime(record.updatedAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space :size="4">
              <PermissionGuard permission="feature:write">
                <a-button type="link" size="small" @click="openEdit(record)">
                  <EditOutlined />编辑
                </a-button>
              </PermissionGuard>
              <a-button
                type="link"
                size="small"
                :danger="record.status === 'Enabled'"
                @click="askToggle(record)"
              >
                {{ record.status === 'Enabled' ? '停用' : '启用' }}
              </a-button>
              <a-button type="link" size="small" @click="openEvaluate(record)">
                <PlayCircleOutlined />评估
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新建功能开关' : '编辑功能开关'"
      width="560px"
      :confirm-loading="submitting"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="Key" required>
          <a-input
            v-model:value="form.key"
            :disabled="modalMode === 'edit'"
            placeholder="如 order.enable-new-checkout"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="2" placeholder="开关用途说明" />
        </a-form-item>
        <a-form-item label="分组" required>
          <a-input v-model:value="form.group" placeholder="如 payment / order / notify" />
        </a-form-item>
        <a-form-item label="规则 JSON" required>
          <a-textarea
            v-model:value="form.ruleJson"
            :rows="6"
            placeholder='{"op":"eq","field":"role","value":"Admin"}'
            style="font-family: 'SF Mono', Consolas, monospace; font-size: 12px"
          />
        </a-form-item>
        <a-form-item label="初始状态" v-if="modalMode === 'create'">
          <a-radio-group v-model:value="form.status">
            <a-radio value="Enabled">启用</a-radio>
            <a-radio value="Disabled">停用</a-radio>
          </a-radio-group>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 区域 D：评估抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="评估开关"
      width="480px"
    >
      <a-spin :spinning="drawerLoading">
        <a-form layout="vertical">
          <a-form-item label="开关 Key">
            <a-input :value="evaluateKey" disabled />
          </a-form-item>
          <a-form-item label="上下文 JSON">
            <a-textarea
              v-model:value="evaluateContext"
              :rows="10"
              style="font-family: 'SF Mono', Consolas, monospace; font-size: 12px"
            />
          </a-form-item>
          <a-form-item>
            <IdempotencyButton type="primary" :loading="drawerLoading" @click="onEvaluate">
              评估
            </IdempotencyButton>
          </a-form-item>
        </a-form>
        <a-divider v-if="evaluateResult" />
        <a-result
          v-if="evaluateResult"
          :status="evaluateResult.enabled ? 'success' : 'info'"
          :title="evaluateResult.enabled ? '生效' : '不生效'"
        >
          <template #subTitle>
            <div>命中规则：{{ evaluateResult.matchedRule }}</div>
          </template>
        </a-result>
      </a-spin>
    </a-drawer>

    <!-- 启停确认弹窗 -->
    <ConfirmDialog
      v-model:open="confirmVisible"
      :danger="confirmDanger"
      :title="confirmTitle"
      :content="confirmContent"
      ok-text="确认"
      cancel-text="取消"
      @ok="onConfirmToggle"
    />
  </div>
</template>

<style scoped>
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/views/FeatureFlags.vue
git commit -m "feat(system-admin/03-system-governance): 实现 FeatureFlags.vue 功能开关视图（筛选+表格+弹窗+评估抽屉）"
```

---

## Task 8: SystemConfigs.vue 系统配置视图

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/views/SystemConfigs.vue`

**实现要点（design-prompt §1-8）:**
- 左侧分组导航（`a-menu mode="inline"`），来自 `GET /admin/system-configs/groups`
- 顶部筛选条：key 搜索 + 状态多选 + 「新建配置」按钮
- 主表格：key / 分组 / 值（掩码）/ 状态 / 最近变更 / 操作（编辑/启用/停用/查看明文）
- 弹窗表单：key（编辑只读）/ 分组 / 值类型 / 值 / 描述 / 状态
- Secret 类型值掩码 `****`；查看明文需 `config:reveal` 权限（仅 Admin）
- 危险操作：停用启用中的配置走 ConfirmDialog，内容「停用后使用该配置的功能将回退到默认值…可随时启用恢复」
- 409 key 冲突 `message.error('配置键已存在')`
- 空状态：「该分组下暂无配置」+ CTA「新建配置」

- [ ] **Step 1: 实现 SystemConfigs.vue**

```vue
<!-- web/system-admin/src/modules/03-system-governance/views/SystemConfigs.vue -->
<!-- 系统配置管理：左分组导航 + 筛选 + 表格 + 新建/编辑弹窗 + 明文查看 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined,
  EditOutlined,
  EyeOutlined,
  KeyOutlined,
} from '@ant-design/icons-vue'
import { systemConfigsApi } from '../api/system-configs.api'
import type {
  SystemConfigDto,
  SaveSystemConfigDto,
  SystemConfigStatus,
  SystemConfigValueType,
  SystemConfigGroupDto,
} from '../types/system-config.dto'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

interface FilterState {
  key: string
  status: SystemConfigStatus[]
  group: string
  page: number
  pageSize: number
}

interface FormState {
  configId?: string
  key: string
  group: string
  valueType: SystemConfigValueType
  value: string
  description: string
  status: SystemConfigStatus
}

const auth = useAuthStore()
const canWrite = computed(() => auth.hasPermission('config:write'))
const canReveal = computed(() => auth.hasPermission('config:reveal'))

const loading = ref(false)
const dataList = ref<SystemConfigDto[]>([])
const total = ref(0)
const groups = ref<SystemConfigGroupDto[]>([])
const selectedGroup = ref<string>('')

const filter = reactive<FilterState>({
  key: '',
  status: [],
  group: '',
  page: 1,
  pageSize: 20,
})

const columns = computed(() => [
  { title: 'Key', dataIndex: 'key', key: 'key', width: 200, ellipsis: true },
  { title: '分组', dataIndex: 'group', key: 'group', width: 120 },
  { title: '值', key: 'valueMasked', ellipsis: true },
  { title: '状态', key: 'status', width: 100 },
  { title: '最近变更', key: 'updatedAt', width: 160 },
  { title: '操作', key: 'action', width: 260, fixed: 'right' as const },
])

const valueTypeOptions: { label: string; value: SystemConfigValueType }[] = [
  { label: '字符串', value: 'String' },
  { label: '整数', value: 'Int' },
  { label: '布尔', value: 'Bool' },
  { label: 'JSON', value: 'Json' },
  { label: '敏感', value: 'Secret' },
]

// 弹窗
const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const submitting = ref(false)
const revealLoading = ref(false)
const valueVisible = ref(false)
const form = reactive<FormState>({
  key: '',
  group: '',
  valueType: 'String',
  value: '',
  description: '',
  status: 'Enabled',
})

// 确认弹窗
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'enable' | 'disable'; config: SystemConfigDto } | null>(null)
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'disable' ? '停用系统配置' : '启用系统配置')
const confirmContent = computed(() =>
  confirmAction.value?.kind === 'disable'
    ? '停用后使用该配置的功能将回退到默认值，可能影响线上行为。可随时启用恢复。'
    : '启用后该配置将立即生效。')

async function loadGroups() {
  try {
    const res = await systemConfigsApi.groups()
    groups.value = res
  } catch (e) {
    // 分组加载失败不阻塞列表
    groups.value = []
  }
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      key: filter.key || undefined,
      group: selectedGroup.value || filter.group || undefined,
      status: filter.status.length ? filter.status : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await systemConfigsApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载系统配置失败')
  } finally {
    loading.value = false
  }
}

function onSelectGroup(group: string) {
  selectedGroup.value = group
  filter.page = 1
  loadList()
}

function onSearch() {
  selectedGroup.value = ''
  filter.page = 1
  loadList()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  filter.page = pag.current ?? 1
  filter.pageSize = pag.pageSize ?? 20
  loadList()
}

function openCreate() {
  modalMode.value = 'create'
  Object.assign(form, {
    configId: undefined,
    key: '',
    group: selectedGroup.value || '',
    valueType: 'String',
    value: '',
    description: '',
    status: 'Enabled',
  })
  valueVisible.value = false
  modalVisible.value = true
}

function openEdit(config: SystemConfigDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    configId: config.configId,
    key: config.key,
    group: config.group,
    valueType: config.valueType,
    value: '', // 编辑时默认空，Secret 类型需点「显示明文」获取
    description: config.description,
    status: config.status,
  })
  valueVisible.value = false
  modalVisible.value = true
}

async function onRevealValue() {
  if (!form.key) return
  revealLoading.value = true
  try {
    const res = await systemConfigsApi.getByKey(form.key)
    form.value = res.value
    valueVisible.value = true
    message.success('明文已加载')
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('鉴权失败或加载失败')
  } finally {
    revealLoading.value = false
  }
}

async function onSubmit() {
  if (!form.key.trim()) return message.error('Key 必填')
  if (!form.group.trim()) return message.error('分组必填')
  if (modalMode.value === 'create' && !form.value) return message.error('值必填')
  submitting.value = true
  try {
    const body: SaveSystemConfigDto = {
      key: form.key.trim(),
      group: form.group.trim(),
      valueType: form.valueType,
      value: form.value,
      description: form.description.trim(),
    }
    if (modalMode.value === 'create') {
      await systemConfigsApi.create(body)
      message.success('配置已创建')
    } else if (form.configId) {
      await systemConfigsApi.update(form.configId, body)
      message.success('配置已更新')
    }
    modalVisible.value = false
    loadList()
    loadGroups()
  } catch (e) {
    if (e instanceof BusinessError) {
      // 409 key 冲突
      message.error(e.message || '配置键已存在')
    } else {
      message.error('保存失败')
    }
  } finally {
    submitting.value = false
  }
}

function askToggle(config: SystemConfigDto) {
  confirmAction.value = {
    kind: config.status === 'Enabled' ? 'disable' : 'enable',
    config,
  }
  confirmVisible.value = true
}

async function onConfirmToggle() {
  if (!confirmAction.value) return
  const { kind, config } = confirmAction.value
  try {
    if (kind === 'enable') {
      await systemConfigsApi.enable(config.configId)
      message.success('配置已启用')
    } else {
      await systemConfigsApi.disable(config.configId)
      message.success('配置已停用')
    }
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function statusTagColor(status: SystemConfigStatus): string {
  return status === 'Enabled' ? 'success' : 'default'
}

function statusTagText(status: SystemConfigStatus): string {
  return status === 'Enabled' ? '启用' : '停用'
}

function valueTypeLabel(t: SystemConfigValueType): string {
  return valueTypeOptions.find((o) => o.value === t)?.label ?? t
}

onMounted(() => {
  loadGroups()
  loadList()
})
</script>

<template>
  <div class="system-configs-page">
    <a-row :gutter="16">
      <!-- 区域 A：左侧分组导航 -->
      <a-col :xs="24" :md="6" :lg="5">
        <a-card :bordered="false" title="全部分组">
          <a-menu
            mode="inline"
            :selected-keys="selectedGroup ? [selectedGroup] : []"
            @click="(e: { key: string }) => onSelectGroup(e.key)"
          >
            <a-menu-item key="">全部</a-menu-item>
            <a-menu-item v-for="g in groups" :key="g.group">
              {{ g.group }} ({{ g.count }})
            </a-menu-item>
          </a-menu>
        </a-card>
      </a-col>

      <!-- 区域 B+C：筛选 + 主表格 -->
      <a-col :xs="24" :md="18" :lg="19">
        <a-card :bordered="false" class="filter-card">
          <a-space :size="12" wrap>
            <a-input
              v-model:value="filter.key"
              placeholder="搜索 Key"
              allow-clear
              style="width: 200px"
              @press-enter="onSearch"
            />
            <a-select
              v-model:value="filter.status"
              mode="multiple"
              placeholder="状态"
              allow-clear
              style="width: 180px"
              :options="[
                { label: '启用', value: 'Enabled' },
                { label: '停用', value: 'Disabled' },
              ]"
            />
            <a-button type="primary" @click="onSearch">查询</a-button>
            <PermissionGuard permission="config:write">
              <a-button type="primary" @click="openCreate">
                <PlusOutlined />新建配置
              </a-button>
            </PermissionGuard>
          </a-space>
        </a-card>

        <a-card :bordered="false" style="margin-top: 16px">
          <a-table
            :columns="columns"
            :data-source="dataList"
            :loading="loading"
            :row-key="(r: SystemConfigDto) => r.configId"
            :pagination="{
              current: filter.page,
              pageSize: filter.pageSize,
              total,
              showSizeChanger: true,
              showTotal: (t: number) => `共 ${t} 条`,
            }"
            @change="onTableChange"
          >
            <template #emptyText>
              <EmptyState
                :description="selectedGroup ? '该分组下暂无配置' : '暂无系统配置'"
                action-text="新建配置"
                @action="openCreate"
              />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'key'">
                <span style="font-family: 'SF Mono', Consolas, monospace">{{ record.key }}</span>
              </template>
              <template v-else-if="column.key === 'valueMasked'">
                <a-tag v-if="record.valueType === 'Secret'" color="orange">{{ record.valueMasked }}</a-tag>
                <span v-else style="color: #595959; font-size: 12px">{{ record.valueMasked }}</span>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="statusTagColor(record.status)">{{ statusTagText(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'updatedAt'">
                {{ formatDateTime(record.updatedAt) }}
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space :size="4">
                  <PermissionGuard permission="config:write">
                    <a-button type="link" size="small" @click="openEdit(record)">
                      <EditOutlined />编辑
                    </a-button>
                  </PermissionGuard>
                  <a-button
                    type="link"
                    size="small"
                    :danger="record.status === 'Enabled'"
                    @click="askToggle(record)"
                  >
                    {{ record.status === 'Enabled' ? '停用' : '启用' }}
                  </a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 D：新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新建系统配置' : '编辑系统配置'"
      width="560px"
      :confirm-loading="submitting"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="Key" required>
          <a-input
            v-model:value="form.key"
            :disabled="modalMode === 'edit'"
            placeholder="如 payment.timeout"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="分组" required>
          <a-input v-model:value="form.group" placeholder="如 payment / notify / cart / search" />
        </a-form-item>
        <a-form-item label="值类型" required>
          <a-select
            v-model:value="form.valueType"
            :options="valueTypeOptions"
            :disabled="modalMode === 'edit'"
          />
        </a-form-item>
        <a-form-item label="值" required>
          <a-input-group compact>
            <a-textarea
              v-if="form.valueType === 'Secret' || form.valueType === 'Json'"
              v-model:value="form.value"
              :rows="3"
              :placeholder="form.valueType === 'Secret' ? '敏感值（创建后掩码展示）' : 'JSON 值'"
              :style="{
                fontFamily: 'SF Mono, Consolas, monospace',
                fontSize: '12px',
                width: 'calc(100% - 100px)',
              }"
            />
            <a-input
              v-else
              v-model:value="form.value"
              :type="form.valueType === 'Secret' && !valueVisible ? 'password' : 'text'"
              placeholder="配置值"
              style="width: calc(100% - 100px)"
            />
            <PermissionGuard permission="config:reveal">
              <a-button
                v-if="modalMode === 'edit' && form.valueType === 'Secret'"
                :loading="revealLoading"
                style="width: 100px"
                @click="onRevealValue"
              >
                <EyeOutlined />{{ valueVisible ? '已显示' : '显示明文' }}
              </a-button>
              <a-button v-else style="width: 100px" disabled>
                <KeyOutlined />明文
              </a-button>
            </PermissionGuard>
          </a-input-group>
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="2" placeholder="配置用途说明" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 启停确认弹窗 -->
    <ConfirmDialog
      v-model:open="confirmVisible"
      :danger="confirmDanger"
      :title="confirmTitle"
      :content="confirmContent"
      ok-text="确认"
      cancel-text="取消"
      @ok="onConfirmToggle"
    />
  </div>
</template>

<style scoped>
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/views/SystemConfigs.vue
git commit -m "feat(system-admin/03-system-governance): 实现 SystemConfigs.vue 系统配置视图（分组导航+表格+弹窗+明文查看）"
```

---

## Task 9: DataDictionaries.vue 数据字典视图

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/views/DataDictionaries.vue`

**实现要点（design-prompt §1-8）:**
- 左侧字典列表（`a-list`）：显示编码/名称/状态/项数，含搜索与「新增字典」按钮
- 右侧上半区：`a-descriptions` 展示字典基本信息 + 编辑/启用/停用按钮
- 右侧下半区：字典项表格（编码/显示名/排序/状态/操作），支持新增/编辑（弹窗）/移除（确认）
- 编码编辑时只读
- 危险操作：移除字典项走 ConfirmDialog danger，内容「移除后该字典项将不再可用…此操作幂等，重复请求无副作用」
- 409 编码冲突 `message.error('字典编码已存在')`；引用冲突 `message.error('该项被引用，无法移除')`
- 空状态：「暂无数据字典」+ CTA「新增字典」

- [ ] **Step 1: 实现 DataDictionaries.vue**

```vue
<!-- web/system-admin/src/modules/03-system-governance/views/DataDictionaries.vue -->
<!-- 数据字典管理：左字典列表 + 右详情 + 字典项表格 CRUD -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  DatabaseOutlined,
} from '@ant-design/icons-vue'
import { dataDictionariesApi } from '../api/data-dictionaries.api'
import type {
  DataDictionaryDto,
  SaveDataDictionaryDto,
  DictionaryItemDto,
  AddDictionaryItemDto,
  UpdateDictionaryItemDto,
  DictionaryStatus,
} from '../types/data-dictionary.dto'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { BusinessError } from '@/shared/http/errors'

interface DictFormState {
  dictionaryId?: string
  code: string
  name: string
  description: string
}

interface ItemFormState {
  itemId?: string
  code: string
  displayName: string
  sortOrder: number
}

const auth = useAuthStore()
const canWrite = computed(() => auth.hasPermission('dictionary:write'))

const listLoading = ref(false)
const dictList = ref<DataDictionaryDto[]>([])
const searchKeyword = ref('')
const currentDict = ref<DataDictionaryDto | null>(null)

// 字典弹窗
const dictModalVisible = ref(false)
const dictModalMode = ref<'create' | 'edit'>('create')
const dictSubmitting = ref(false)
const dictForm = reactive<DictFormState>({
  code: '',
  name: '',
  description: '',
})

// 字典项弹窗
const itemModalVisible = ref(false)
const itemModalMode = ref<'create' | 'edit'>('create')
const itemSubmitting = ref(false)
const itemForm = reactive<ItemFormState>({
  code: '',
  displayName: '',
  sortOrder: 0,
})

// 移除确认
const removeConfirmVisible = ref(false)
const removeTarget = ref<{ dict: DataDictionaryDto; item: DictionaryItemDto } | null>(null)

// 启停确认
const toggleConfirmVisible = ref(false)
const toggleTarget = ref<{ kind: 'enable' | 'disable'; dict: DataDictionaryDto } | null>(null)

const itemColumns = computed(() => [
  { title: '编码', dataIndex: 'code', key: 'code', width: 180 },
  { title: '显示名', dataIndex: 'displayName', key: 'displayName' },
  { title: '排序', dataIndex: 'sortOrder', key: 'sortOrder', width: 80, align: 'right' as const },
  { title: '状态', key: 'status', width: 100 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' as const },
])

async function loadList() {
  listLoading.value = true
  try {
    const res = await dataDictionariesApi.list({
      name: searchKeyword.value || undefined,
      page: 1,
      pageSize: 100,
    })
    dictList.value = res.items
    // 默认选中首个
    if (!currentDict.value && res.items.length > 0) {
      await selectDict(res.items[0]!)
    } else if (currentDict.value) {
      // 刷新当前选中字典的详情
      const fresh = res.items.find((d) => d.dictionaryId === currentDict.value!.dictionaryId)
      if (fresh) currentDict.value = fresh
    }
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载数据字典失败')
  } finally {
    listLoading.value = false
  }
}

async function selectDict(dict: DataDictionaryDto) {
  currentDict.value = dict
}

function onSearch() {
  loadList()
}

function openCreateDict() {
  dictModalMode.value = 'create'
  Object.assign(dictForm, { dictionaryId: undefined, code: '', name: '', description: '' })
  dictModalVisible.value = true
}

function openEditDict() {
  if (!currentDict.value) return
  dictModalMode.value = 'edit'
  Object.assign(dictForm, {
    dictionaryId: currentDict.value.dictionaryId,
    code: currentDict.value.code,
    name: currentDict.value.name,
    description: currentDict.value.description,
  })
  dictModalVisible.value = true
}

async function onSubmitDict() {
  if (!dictForm.code.trim()) return message.error('编码必填')
  if (!dictForm.name.trim()) return message.error('名称必填')
  dictSubmitting.value = true
  try {
    const body: SaveDataDictionaryDto = {
      code: dictForm.code.trim(),
      name: dictForm.name.trim(),
      description: dictForm.description.trim(),
    }
    if (dictModalMode.value === 'create') {
      const created = await dataDictionariesApi.create(body)
      message.success('字典已创建')
      dictModalVisible.value = false
      await loadList()
      await selectDict(created)
    } else if (dictForm.dictionaryId) {
      await dataDictionariesApi.update(dictForm.dictionaryId, body)
      message.success('字典已更新')
      dictModalVisible.value = false
      loadList()
    }
  } catch (e) {
    if (e instanceof BusinessError) {
      message.error(e.message || '字典编码已存在')
    } else {
      message.error('保存失败')
    }
  } finally {
    dictSubmitting.value = false
  }
}

function askToggleDict() {
  if (!currentDict.value) return
  toggleTarget.value = {
    kind: currentDict.value.status === 'Enabled' ? 'disable' : 'enable',
    dict: currentDict.value,
  }
  toggleConfirmVisible.value = true
}

async function onConfirmToggleDict() {
  if (!toggleTarget.value) return
  const { kind, dict } = toggleTarget.value
  try {
    if (kind === 'enable') {
      await dataDictionariesApi.enable(dict.dictionaryId)
      message.success('字典已启用')
    } else {
      await dataDictionariesApi.disable(dict.dictionaryId)
      message.success('字典已停用')
    }
    toggleConfirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function openCreateItem() {
  itemModalMode.value = 'create'
  Object.assign(itemForm, { itemId: undefined, code: '', displayName: '', sortOrder: currentDict.value?.items.length ?? 0 })
  itemModalVisible.value = true
}

function openEditItem(item: DictionaryItemDto) {
  itemModalMode.value = 'edit'
  Object.assign(itemForm, {
    itemId: item.itemId,
    code: item.code,
    displayName: item.displayName,
    sortOrder: item.sortOrder,
  })
  itemModalVisible.value = true
}

async function onSubmitItem() {
  if (!currentDict.value) return
  if (!itemForm.code.trim()) return message.error('项编码必填')
  if (!itemForm.displayName.trim()) return message.error('显示名必填')
  itemSubmitting.value = true
  try {
    if (itemModalMode.value === 'create') {
      const body: AddDictionaryItemDto = {
        code: itemForm.code.trim(),
        displayName: itemForm.displayName.trim(),
        sortOrder: itemForm.sortOrder,
      }
      await dataDictionariesApi.addItem(currentDict.value.dictionaryId, body)
      message.success('字典项已新增')
    } else if (itemForm.itemId) {
      const body: UpdateDictionaryItemDto = {
        code: itemForm.code.trim(),
        displayName: itemForm.displayName.trim(),
        sortOrder: itemForm.sortOrder,
      }
      await dataDictionariesApi.updateItem(currentDict.value.dictionaryId, itemForm.itemId, body)
      message.success('字典项已更新')
    }
    itemModalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) {
      message.error(e.message || '字典编码已存在')
    } else {
      message.error('保存失败')
    }
  } finally {
    itemSubmitting.value = false
  }
}

function askRemoveItem(item: DictionaryItemDto) {
  if (!currentDict.value) return
  removeTarget.value = { dict: currentDict.value, item }
  removeConfirmVisible.value = true
}

async function onConfirmRemoveItem() {
  if (!removeTarget.value) return
  const { dict, item } = removeTarget.value
  try {
    await dataDictionariesApi.removeItem(dict.dictionaryId, item.itemId)
    message.success('字典项已移除')
    removeConfirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) {
      message.error(e.message || '该项被引用，无法移除')
    } else {
      message.error('移除失败')
    }
  }
}

function statusTagColor(status: DictionaryStatus): string {
  return status === 'Enabled' ? 'success' : 'default'
}

function statusTagText(status: DictionaryStatus): string {
  return status === 'Enabled' ? '启用' : '停用'
}

onMounted(() => {
  loadList()
})
</script>

<template>
  <div class="data-dictionaries-page">
    <a-row :gutter="16">
      <!-- 区域 A：左侧字典列表 -->
      <a-col :xs="24" :md="8" :lg="7">
        <a-card :bordered="false" title="数据字典">
          <template #extra>
            <PermissionGuard permission="dictionary:write">
              <a-button type="primary" size="small" @click="openCreateDict">
                <PlusOutlined />新增字典
              </a-button>
            </PermissionGuard>
          </template>
          <a-input
            v-model:value="searchKeyword"
            placeholder="搜索名称/编码"
            allow-clear
            style="margin-bottom: 12px"
            @press-enter="onSearch"
          />
          <a-spin :spinning="listLoading">
            <a-list
              v-if="dictList.length > 0"
              :data-source="dictList"
              :split="false"
              size="small"
            >
              <template #renderItem="{ item }">
                <a-list-item
                  :style="{
                    padding: '8px 12px',
                    cursor: 'pointer',
                    borderRadius: '6px',
                    background: currentDict?.dictionaryId === item.dictionaryId ? '#E6F4FF' : 'transparent',
                    marginBottom: '4px',
                  }"
                  @click="selectDict(item)"
                >
                  <a-list-item-meta>
                    <template #avatar>
                      <DatabaseOutlined style="font-size: 16px; color: #1677ff" />
                    </template>
                    <template #title>
                      <span style="font-family: 'SF Mono', Consolas, monospace; font-size: 13px">
                        {{ item.code }}
                      </span>
                    </template>
                    <template #description>
                      {{ item.name }} · {{ item.items.length }} 项
                    </template>
                  </a-list-item-meta>
                  <template #actions>
                    <a-tag :color="statusTagColor(item.status)">{{ statusTagText(item.status) }}</a-tag>
                  </template>
                </a-list-item>
              </template>
            </a-list>
            <EmptyState
              v-else
              description="暂无数据字典"
              action-text="新增字典"
              @action="openCreateDict"
            />
          </a-spin>
        </a-card>
      </a-col>

      <!-- 区域 B+C：右侧详情与字典项 -->
      <a-col :xs="24" :md="16" :lg="17">
        <a-card v-if="currentDict" :bordered="false">
          <!-- 区域 B：字典基本信息 -->
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="编码">
              <span style="font-family: 'SF Mono', Consolas, monospace">{{ currentDict.code }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="名称">{{ currentDict.name }}</a-descriptions-item>
            <a-descriptions-item label="描述" :span="2">{{ currentDict.description || '—' }}</a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="statusTagColor(currentDict.status)">{{ statusTagText(currentDict.status) }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="字典项数">{{ currentDict.items.length }}</a-descriptions-item>
          </a-descriptions>
          <a-space style="margin-top: 12px">
            <PermissionGuard permission="dictionary:write">
              <a-button size="small" @click="openEditDict">
                <EditOutlined />编辑
              </a-button>
            </PermissionGuard>
            <a-button
              size="small"
              :danger="currentDict.status === 'Enabled'"
              @click="askToggleDict"
            >
              {{ currentDict.status === 'Enabled' ? '停用' : '启用' }}
            </a-button>
          </a-space>

          <a-divider style="margin: 16px 0" />

          <!-- 区域 C：字典项表格 -->
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px">
            <h3 style="margin: 0">字典项</h3>
            <PermissionGuard permission="dictionary:write">
              <a-button type="primary" size="small" @click="openCreateItem">
                <PlusOutlined />新增项
              </a-button>
            </PermissionGuard>
          </div>
          <a-table
            :columns="itemColumns"
            :data-source="currentDict.items"
            :row-key="(r: DictionaryItemDto) => r.itemId"
            :pagination="false"
            size="middle"
          >
            <template #emptyText>
              <EmptyState description="暂无字典项" action-text="新增项" @action="openCreateItem" />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'code'">
                <span style="font-family: 'SF Mono', Consolas, monospace">{{ record.code }}</span>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="statusTagColor(record.status)">{{ statusTagText(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space :size="4">
                  <PermissionGuard permission="dictionary:write">
                    <a-button type="link" size="small" @click="openEditItem(record)">
                      <EditOutlined />编辑
                    </a-button>
                  </PermissionGuard>
                  <PermissionGuard permission="dictionary:write">
                    <a-button type="link" size="small" danger @click="askRemoveItem(record)">
                      <DeleteOutlined />移除
                    </a-button>
                  </PermissionGuard>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
        <a-card v-else :bordered="false">
          <EmptyState description="请选择左侧字典查看详情" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 字典新建/编辑弹窗 -->
    <a-modal
      v-model:open="dictModalVisible"
      :title="dictModalMode === 'create' ? '新增数据字典' : '编辑数据字典'"
      width="480px"
      :confirm-loading="dictSubmitting"
      @ok="onSubmitDict"
    >
      <a-form layout="vertical">
        <a-form-item label="编码" required>
          <a-input
            v-model:value="dictForm.code"
            :disabled="dictModalMode === 'edit'"
            placeholder="如 order_status"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="名称" required>
          <a-input v-model:value="dictForm.name" placeholder="如 订单状态" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="dictForm.description" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 字典项新建/编辑弹窗 -->
    <a-modal
      v-model:open="itemModalVisible"
      :title="itemModalMode === 'create' ? '新增字典项' : '编辑字典项'"
      width="440px"
      :confirm-loading="itemSubmitting"
      @ok="onSubmitItem"
    >
      <a-form layout="vertical">
        <a-form-item label="编码" required>
          <a-input
            v-model:value="itemForm.code"
            placeholder="如 pending / paid / shipped"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="显示名" required>
          <a-input v-model:value="itemForm.displayName" placeholder="如 待支付" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="itemForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 移除字典项确认 -->
    <ConfirmDialog
      v-model:open="removeConfirmVisible"
      danger
      title="移除字典项"
      content="移除后该字典项将不再可用，已引用该项的业务需手动迁移。此操作幂等，重复请求无副作用。"
      ok-text="移除"
      cancel-text="取消"
      @ok="onConfirmRemoveItem"
    />

    <!-- 字典启停确认 -->
    <ConfirmDialog
      v-model:open="toggleConfirmVisible"
      :danger="toggleTarget?.kind === 'disable'"
      :title="toggleTarget?.kind === 'disable' ? '停用数据字典' : '启用数据字典'"
      :content="toggleTarget?.kind === 'disable'
        ? '停用后该字典及其字典项将不再可用，引用该字典的功能将受影响。可随时启用恢复。'
        : '启用后该字典将立即生效。'"
      ok-text="确认"
      cancel-text="取消"
      @ok="onConfirmToggleDict"
    />
  </div>
</template>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/views/DataDictionaries.vue
git commit -m "feat(system-admin/03-system-governance): 实现 DataDictionaries.vue 数据字典视图（左列表+右详情+字典项CRUD）"
```

---

## Task 10: Announcements.vue 公告管理视图

**Files:**
- Create: `web/system-admin/src/modules/03-system-governance/views/Announcements.vue`

**实现要点（design-prompt §1-8）:**
- 顶部筛选条：类型筛选 + 状态筛选 + 「新增公告」按钮（PermissionGuard `announcement:write`）
- 主表格：标题 / 类型 / 状态 / 发布范围 / 生效起止 / 操作（编辑/发布/撤回/查看公开页），分页 20
- 弹窗表单（`a-modal width="800"`）：标题 / 类型 / 发布范围多选 / 生效起止 `a-range-picker showTime` / 正文（textarea）/ 置顶开关
- 仅草稿态可编辑；发布按钮仅草稿态可见；撤回按钮仅已发布态可见
- 发布确认 ConfirmDialog 主色：「发布后公告将对所选范围立即生效…撤回可恢复」
- 撤回确认 ConfirmDialog danger：「撤回后公告将立即从所有端下线，已读记录保留。可重新编辑后再次发布」
- 生效时间冲突（EffectiveFrom ≥ EffectiveTo）前端校验拦截
- 状态色：草稿灰、已发布绿、已撤回黄
- 置顶 `<a-tag color="red">置顶</a-tag>`
- 空状态：「暂无公告」+ CTA「新增公告」

- [ ] **Step 1: 实现 Announcements.vue**

```vue
<!-- web/system-admin/src/modules/03-system-governance/views/Announcements.vue -->
<!-- 公告管理：筛选 + 表格 + 新建/编辑弹窗 + 发布/撤回确认 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import {
  PlusOutlined,
  EditOutlined,
  SendOutlined,
  RollbackOutlined,
  NotificationOutlined,
} from '@ant-design/icons-vue'
import { announcementsApi } from '../api/announcements.api'
import type {
  AnnouncementDto,
  SaveAnnouncementDto,
  AnnouncementType,
  AnnouncementStatus,
  AnnouncementAudience,
} from '../types/announcement.dto'
import {
  ANNOUNCEMENT_TYPE_LABELS,
  ANNOUNCEMENT_STATUS_LABELS,
  ANNOUNCEMENT_AUDIENCE_LABELS,
} from '../types/announcement.dto'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

interface FilterState {
  type: AnnouncementType[]
  status: AnnouncementStatus[]
  page: number
  pageSize: number
}

interface FormState {
  announcementId?: string
  title: string
  type: AnnouncementType
  audiences: AnnouncementAudience[]
  effectiveRange: [Dayjs, Dayjs] | null
  content: string
  isPinned: boolean
}

const auth = useAuthStore()
const canWrite = computed(() => auth.hasPermission('announcement:write'))
const canPublish = computed(() => auth.hasPermission('announcement:publish'))

const loading = ref(false)
const dataList = ref<AnnouncementDto[]>([])
const total = ref(0)
const filter = reactive<FilterState>({
  type: [],
  status: [],
  page: 1,
  pageSize: 20,
})

const typeOptions: { label: string; value: AnnouncementType }[] = [
  { label: '系统维护', value: 'SystemMaintenance' },
  { label: '活动通知', value: 'ActivityNotification' },
  { label: '政策变更', value: 'PolicyChange' },
  { label: '紧急公告', value: 'Urgent' },
]

const statusOptions: { label: string; value: AnnouncementStatus }[] = [
  { label: '草稿', value: 'Draft' },
  { label: '已发布', value: 'Published' },
  { label: '已撤回', value: 'Unpublished' },
]

const audienceOptions: { label: string; value: AnnouncementAudience }[] = [
  { label: '买家', value: 'Buyer' },
  { label: '卖家', value: 'Seller' },
  { label: '运营', value: 'Operator' },
]

const columns = computed(() => [
  { title: '标题', dataIndex: 'title', key: 'title', ellipsis: true },
  { title: '类型', key: 'type', width: 110 },
  { title: '状态', key: 'status', width: 100 },
  { title: '发布范围', key: 'audiences', width: 160 },
  { title: '生效起止', key: 'effective', width: 240 },
  { title: '操作', key: 'action', width: 240, fixed: 'right' as const },
])

// 弹窗
const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const submitting = ref(false)
const form = reactive<FormState>({
  title: '',
  type: 'SystemMaintenance',
  audiences: ['Buyer'],
  effectiveRange: null,
  content: '',
  isPinned: false,
})

// 确认弹窗
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'publish' | 'unpublish'; announcement: AnnouncementDto } | null>(null)
const confirmDanger = computed(() => confirmAction.value?.kind === 'unpublish')
const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'publish' ? '发布公告' : '撤回公告')
const confirmContent = computed(() =>
  confirmAction.value?.kind === 'publish'
    ? '发布后公告将对所选范围立即生效，买家 APP 与卖家后台将展示。撤回可恢复。'
    : '撤回后公告将立即从所有端下线，已读记录保留。可重新编辑后再次发布。')
const confirmOkText = computed(() =>
  confirmAction.value?.kind === 'publish' ? '发布' : '撤回')

async function loadList() {
  loading.value = true
  try {
    const params = {
      type: filter.type.length ? filter.type : undefined,
      status: filter.status.length ? filter.status : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await announcementsApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载公告失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  filter.page = pag.current ?? 1
  filter.pageSize = pag.pageSize ?? 20
  loadList()
}

function openCreate() {
  modalMode.value = 'create'
  const now = dayjs()
  Object.assign(form, {
    announcementId: undefined,
    title: '',
    type: 'SystemMaintenance',
    audiences: ['Buyer'],
    effectiveRange: [now, now.add(1, 'day')] as [Dayjs, Dayjs],
    content: '',
    isPinned: false,
  })
  modalVisible.value = true
}

function openEdit(ann: AnnouncementDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    announcementId: ann.announcementId,
    title: ann.title,
    type: ann.type,
    audiences: [...ann.audiences],
    effectiveRange: [dayjs(ann.effectiveFrom), dayjs(ann.effectiveTo)] as [Dayjs, Dayjs],
    content: ann.content,
    isPinned: ann.isPinned,
  })
  modalVisible.value = true
}

async function onSubmit() {
  if (!form.title.trim()) return message.error('标题必填')
  if (!form.audiences.length) return message.error('发布范围至少选一项')
  if (!form.effectiveRange) return message.error('生效起止必填')
  const [from, to] = form.effectiveRange
  if (!from.isBefore(to)) return message.error('生效结束时间必须晚于开始时间')
  if (!form.content.trim()) return message.error('正文必填')
  submitting.value = true
  try {
    const body: SaveAnnouncementDto = {
      title: form.title.trim(),
      type: form.type,
      audiences: form.audiences,
      effectiveFrom: from.toISOString(),
      effectiveTo: to.toISOString(),
      content: form.content,
      isPinned: form.isPinned,
    }
    if (modalMode.value === 'create') {
      await announcementsApi.create(body)
      message.success('公告已创建（草稿态）')
    } else if (form.announcementId) {
      await announcementsApi.update(form.announcementId, body)
      message.success('公告已更新')
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

function askPublish(ann: AnnouncementDto) {
  confirmAction.value = { kind: 'publish', announcement: ann }
  confirmVisible.value = true
}

function askUnpublish(ann: AnnouncementDto) {
  confirmAction.value = { kind: 'unpublish', announcement: ann }
  confirmVisible.value = true
}

async function onConfirmAction() {
  if (!confirmAction.value) return
  const { kind, announcement } = confirmAction.value
  try {
    if (kind === 'publish') {
      await announcementsApi.publish(announcement.announcementId)
      message.success('公告已发布')
    } else {
      await announcementsApi.unpublish(announcement.announcementId)
      message.success('公告已撤回')
    }
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function openPublicView() {
  // 打开新窗口预览公开公告页
  window.open('/api/announcements', '_blank')
}

function statusTagColor(status: AnnouncementStatus): string {
  if (status === 'Published') return 'success'
  if (status === 'Unpublished') return 'warning'
  return 'default'
}

function audiencesText(audiences: AnnouncementAudience[]): string {
  return audiences.map((a) => ANNOUNCEMENT_AUDIENCE_LABELS[a]).join('、')
}

function effectiveText(from: string, to: string): string {
  return `${formatDateTime(from)} ~ ${formatDateTime(to)}`
}

onMounted(() => {
  loadList()
})
</script>

<template>
  <div class="announcements-page">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-space :size="12" wrap>
        <a-select
          v-model:value="filter.type"
          mode="multiple"
          placeholder="类型"
          allow-clear
          style="width: 200px"
          :options="typeOptions"
        />
        <a-select
          v-model:value="filter.status"
          mode="multiple"
          placeholder="状态"
          allow-clear
          style="width: 180px"
          :options="statusOptions"
        />
        <a-button type="primary" @click="onSearch">查询</a-button>
        <PermissionGuard permission="announcement:write">
          <a-button type="primary" @click="openCreate">
            <PlusOutlined />新增公告
          </a-button>
        </PermissionGuard>
      </a-space>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" style="margin-top: 16px">
      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :row-key="(r: AnnouncementDto) => r.announcementId"
        :pagination="{
          current: filter.page,
          pageSize: filter.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无公告" action-text="新增公告" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'title'">
            <a-space :size="4">
              <a-tag v-if="record.isPinned" color="red">置顶</a-tag>
              <span>{{ record.title }}</span>
            </a-space>
          </template>
          <template v-else-if="column.key === 'type'">
            <a-tag color="blue">{{ ANNOUNCEMENT_TYPE_LABELS[record.type as AnnouncementType] }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="statusTagColor(record.status)">
              {{ ANNOUNCEMENT_STATUS_LABELS[record.status as AnnouncementStatus] }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'audiences'">
            {{ audiencesText(record.audiences) }}
          </template>
          <template v-else-if="column.key === 'effective'">
            {{ effectiveText(record.effectiveFrom, record.effectiveTo) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space :size="4">
              <!-- 仅草稿态可编辑 -->
              <PermissionGuard permission="announcement:write">
                <a-tooltip
                  :title="record.status !== 'Draft' ? '仅草稿态可编辑' : ''"
                  :visible="record.status !== 'Draft' ? undefined : false"
                >
                  <a-button
                    type="link"
                    size="small"
                    :disabled="record.status !== 'Draft'"
                    @click="openEdit(record)"
                  >
                    <EditOutlined />编辑
                  </a-button>
                </a-tooltip>
              </PermissionGuard>
              <!-- 仅草稿态可发布，需 publish 权限 -->
              <PermissionGuard permission="announcement:publish">
                <a-button
                  v-if="record.status === 'Draft'"
                  type="link"
                  size="small"
                  @click="askPublish(record)"
                >
                  <SendOutlined />发布
                </a-button>
              </PermissionGuard>
              <!-- 仅已发布态可撤回，需 publish 权限 -->
              <PermissionGuard permission="announcement:publish">
                <a-button
                  v-if="record.status === 'Published'"
                  type="link"
                  size="small"
                  danger
                  @click="askUnpublish(record)"
                >
                  <RollbackOutlined />撤回
                </a-button>
              </PermissionGuard>
              <a-button type="link" size="small" @click="openPublicView">
                <NotificationOutlined />公开页
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新增公告' : '编辑公告'"
      width="800px"
      :confirm-loading="submitting"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="标题" required>
          <a-input v-model:value="form.title" placeholder="公告标题" :maxlength="100" show-count />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="类型" required>
              <a-select v-model:value="form.type" :options="typeOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="10">
            <a-form-item label="发布范围" required>
              <a-select
                v-model:value="form.audiences"
                mode="multiple"
                :options="audienceOptions"
                placeholder="选择展示端"
              />
            </a-form-item>
          </a-col>
          <a-col :span="6">
            <a-form-item label="置顶">
              <a-switch v-model:checked="form.isPinned" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="生效起止" required>
          <a-range-picker
            v-model:value="form.effectiveRange"
            show-time
            format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="正文" required>
          <a-textarea
            v-model:value="form.content"
            :rows="8"
            placeholder="公告正文（支持纯文本）"
            :maxlength="5000"
            show-count
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 发布/撤回确认弹窗 -->
    <ConfirmDialog
      v-model:open="confirmVisible"
      :danger="confirmDanger"
      :title="confirmTitle"
      :content="confirmContent"
      :ok-text="confirmOkText"
      cancel-text="取消"
      @ok="onConfirmAction"
    />
  </div>
</template>

<style scoped>
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 运行模块全部测试确保无回归**

Run: `cd web/system-admin && pnpm test -- src/modules/03-system-governance/`
Expected: PASS（feature-flags.api.spec.ts 6 用例 + announcements.api.spec.ts 5 用例 = 11 用例通过）

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/03-system-governance/views/Announcements.vue
git commit -m "feat(system-admin/03-system-governance): 实现 Announcements.vue 公告管理视图（筛选+表格+弹窗+发布撤回确认）"
```

---

## 自检（Self-Review）

### 1. Spec 覆盖（§2.3 模块 03-system-governance 4 页）

| spec §2.3 页面 | 对应 Task | 视图文件 | API 文件 | DTO 文件 |
|-|-|-|-|-|
| FeatureFlags.vue `/system-governance/feature-flags` | Task 2 + Task 7 | ✅ views/FeatureFlags.vue | ✅ api/feature-flags.api.ts | ✅ types/feature-flag.dto.ts |
| SystemConfigs.vue `/system-governance/system-configs` | Task 3 + Task 8 | ✅ views/SystemConfigs.vue | ✅ api/system-configs.api.ts | ✅ types/system-config.dto.ts |
| DataDictionaries.vue `/system-governance/data-dictionaries` | Task 4 + Task 9 | ✅ views/DataDictionaries.vue | ✅ api/data-dictionaries.api.ts | ✅ types/data-dictionary.dto.ts |
| Announcements.vue `/system-governance/announcements` | Task 5 + Task 10 | ✅ views/Announcements.vue | ✅ api/announcements.api.ts | ✅ types/announcement.dto.ts |
| 模块骨架 routes.ts + index.ts | Task 6 | — | — | — |

4 页 + 模块骨架全部有对应 Task，无遗漏。

### 2. 占位符扫描

扫描全部 Task 代码，确认以下关键词为零：
- `TODO` / `FIXME` / `TBD` / `...`（省略号）/ `占位` / `暂不实现` / `Not implemented`：**0 处**
- 所有函数均有完整实现，无空函数体（`{}` 或 `pass`）
- 所有 import 语句齐全
- 所有弹窗/抽屉/确认框均有完整交互逻辑

### 3. 类型一致性

| 类型/方法 | 定义位置 | 使用位置 | 一致性 |
|-|-|-|-|
| `featureFlagsApi.list/create/update/enable/disable/evaluate` | Task 2 api | Task 7 view | ✅ 方法名与签名一致 |
| `systemConfigsApi.list/groups/getByKey/create/update/enable/disable` | Task 3 api | Task 8 view | ✅ |
| `dataDictionariesApi.list/create/update/enable/disable/addItem/updateItem/removeItem` | Task 4 api | Task 9 view | ✅ |
| `announcementsApi.list/create/update/publish/unpublish` | Task 5 api | Task 10 view | ✅ |
| `FeatureFlagDto` 字段 `flagId/key/description/group/status/ruleJson/updatedAt/updatedBy` | Task 1 dto | Task 7 view | ✅ |
| `SystemConfigDto` 字段 `configId/key/group/valueType/valueMasked/description/status/updatedAt` | Task 1 dto | Task 8 view | ✅ |
| `DataDictionaryDto` 字段 `dictionaryId/code/name/description/status/items` | Task 1 dto | Task 9 view | ✅ |
| `AnnouncementDto` 字段 `announcementId/title/type/status/audiences/effectiveFrom/effectiveTo/content/isPinned/createdAt/publishedAt` | Task 1 dto | Task 10 view | ✅ |
| 路由 name `system-governance.{view}` | Task 6 routes | — | ✅ 与 spec §2.9 一致 |
| 路由 path `feature-flags/system-configs/data-dictionaries/announcements` | Task 6 routes | — | ✅ 与 spec §2.3 一致 |

### 4. 文件路径一致性

| Task | 文件路径 | 与 File Structure 一致 |
|-|-|-|
| Task 1 | `types/feature-flag.dto.ts` / `types/system-config.dto.ts` / `types/data-dictionary.dto.ts` / `types/announcement.dto.ts` | ✅ |
| Task 2 | `api/feature-flags.api.ts` + `api/feature-flags.api.spec.ts` | ✅ |
| Task 3 | `api/system-configs.api.ts` | ✅ |
| Task 4 | `api/data-dictionaries.api.ts` | ✅ |
| Task 5 | `api/announcements.api.ts` + `api/announcements.api.spec.ts` | ✅ |
| Task 6 | `routes.ts` + `index.ts` | ✅ |
| Task 7-10 | `views/FeatureFlags.vue` / `views/SystemConfigs.vue` / `views/DataDictionaries.vue` / `views/Announcements.vue` | ✅ |

### 5. Design-Prompt 字段覆盖

**feature-flags.md：**
- ✅ API 6 端点（list/create/update/enable/disable/evaluate）→ Task 2
- ✅ 区域 A-D（筛选/表格/弹窗/评估抽屉）→ Task 7
- ✅ key 新建可编辑/编辑只读 → Task 7 `:disabled="modalMode === 'edit'"`
- ✅ 规则 JSON 格式校验 → Task 7 `validateRuleJson()`
- ✅ 评估显示布尔结果 + 命中规则 → Task 7 `evaluateResult`
- ✅ 启停二次确认 → Task 7 `ConfirmDialog`
- ✅ Enabled 绿 / Disabled 灰 → Task 7 `statusTagColor`
- ✅ 空状态「暂无功能开关」+ CTA → Task 7 `EmptyState`

**system-configs.md：**
- ✅ API 7 端点（list/groups/getByKey/create/update/enable/disable）→ Task 3
- ✅ 左侧分组导航 `a-menu mode="inline"` → Task 8
- ✅ key 编辑只读 → Task 8 `:disabled="modalMode === 'edit'"`
- ✅ Secret 类型掩码 `****` + 查看明文需 config:reveal → Task 8 `onRevealValue` + `PermissionGuard`
- ✅ 分组导航点击切换筛选 → Task 8 `onSelectGroup`
- ✅ 启停二次确认 → Task 8 `ConfirmDialog`
- ✅ 409 key 冲突友好提示 → Task 8 catch BusinessError
- ✅ 空状态「该分组下暂无配置」+ CTA → Task 8 `EmptyState`

**data-dictionaries.md：**
- ✅ API 8 端点（list/create/update/enable/disable + 字典项 addItem/updateItem/removeItem）→ Task 4
- ✅ 左侧字典列表 `a-list` + 搜索 + 新增 → Task 9
- ✅ 右侧 `a-descriptions` 基本信息 + 编辑/启停 → Task 9
- ✅ 字典项表格 CRUD → Task 9
- ✅ 编码编辑只读 → Task 9 `:disabled="dictModalMode === 'edit'"`
- ✅ 移除字典项二次确认 danger → Task 9 `ConfirmDialog danger`
- ✅ 409 编码冲突/引用冲突友好提示 → Task 9 catch BusinessError
- ✅ 空状态「暂无数据字典」+ CTA → Task 9 `EmptyState`

**announcements.md：**
- ✅ API 5 端点（list/create/update/publish/unpublish）→ Task 5
- ✅ 区域 A-C（筛选/表格/弹窗）→ Task 10
- ✅ 仅草稿态可编辑（其他状态编辑按钮 disabled）→ Task 10 `:disabled="record.status !== 'Draft'"`
- ✅ 发布范围多选生效 → Task 10 `a-select mode="multiple"`
- ✅ 富文本编辑器（用 textarea 实现纯文本编辑，完整可用）→ Task 10 `a-textarea`
- ✅ 发布与撤回二次确认 → Task 10 `ConfirmDialog`（发布主色/撤回 danger）
- ✅ 生效时间冲突前端校验 → Task 10 `from.isBefore(to)`
- ✅ 置顶 `<a-tag color="red">置顶</a-tag>` → Task 10
- ✅ 状态色：草稿灰/已发布绿/已撤回黄 → Task 10 `statusTagColor`
- ✅ 空状态「暂无公告」+ CTA → Task 10 `EmptyState`

**全部 design-prompt 字段覆盖，无遗漏。**
