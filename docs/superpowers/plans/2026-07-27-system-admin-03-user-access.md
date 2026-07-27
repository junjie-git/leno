# 系统管理后台 - 02-user-access 模块实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 02-user-access 模块的 4 个页面（用户管理/角色管理/OAuth 客户端/运营人员）及其 DTO、API、共享组件、路由聚合，覆盖 Identity 域 AdminUsers/AdminOAuthClients、AccessControl 域 AdminRoles、SystemAdmin 域 Operators 全部端点。

**Architecture:** 模块内分 types/api/components/views 四层；API 层直连后端 `/api/admin/*` 端点并复用 shared/http 的 axios 实例与幂等键工具；视图层基于 Ant Design Vue 4.x + 共享组件（DataTable/StatusTag/IdempotencyButton/ConfirmDialog/PermissionGuard/EmptyState/DateTimeRangePicker）；RolePermissionMatrix 为模块内复用的权限矩阵编辑器，包裹 a-tree checkable 实现按模块分组的权限分配。

**Tech Stack:** Vue 3.5 `<script setup>` + TypeScript strict + Ant Design Vue 4.x + Pinia + Vue Router 4 + axios + Vitest 2.x + @vue/test-utils + jsdom

**关联 Spec：** `docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md` §2.2

**关联 Design Prompts：**
- `docs/design-prompts/system-admin/02-user-access/user-management.md`
- `docs/design-prompts/system-admin/02-user-access/role-management.md`
- `docs/design-prompts/system-admin/02-user-access/oauth-clients.md`
- `docs/design-prompts/system-admin/02-user-access/operators.md`

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
- API：导出为 `usersApi` / `rolesApi` / `oauthClientsApi` / `operatorsApi` 对象，方法 camelCase 动词开头
- DTO：PascalCase + `Dto` 后缀
- 路由 name：`user-access.{view}` kebab-case
- 路由 path：kebab-case

---

## 文件结构

### 新建文件（17 个，全部位于 `web/system-admin/src/modules/02-user-access/`）

**类型层（4）**
- `types/user.dto.ts` — 用户 DTO 与枚举（UserDto / ListUsersParams / AssignUserRolesDto / UpdateUserStatusDto / UserStatus / UserLoginHistoryDto）
- `types/role.dto.ts` — 角色 DTO（RoleDto / ListRolesParams / SaveRoleDto / UpdateRolePermissionsDto / PermissionGroupDto / PermissionItemDto）
- `types/oauth-client.dto.ts` — OAuth 客户端 DTO（OAuthClientDto / UpdateOAuthClientDto / ListOAuthClientsParams / SUPPORTED_OAUTH_PROVIDERS）
- `types/operator.dto.ts` — 运营人员 DTO（OperatorDto / ListOperatorsParams / SaveOperatorDto / AssignOperatorPermissionsDto / OperatorStatus / OperatorRole）

**API 层（4 + 2 测试）**
- `api/users.api.ts` — Identity 域 AdminUsersController（list/get/assignRoles/updateStatus）
- `api/users.api.spec.ts` — usersApi 单元测试
- `api/roles.api.ts` — AccessControl 域 AdminRolesController（list/get/create/update/remove/getPermissions/getPermissionCatalog/updatePermissions）
- `api/roles.api.spec.ts` — rolesApi 单元测试
- `api/oauth-clients.api.ts` — Identity 域 AdminOAuthClientsController（list/create/update/enable/disable）
- `api/operators.api.ts` — SystemAdmin OperatorsController（list/get/create/updatePermissions/activate/deactivate）

**组件层（1）**
- `components/RolePermissionMatrix.vue` — 角色-权限矩阵编辑器（包裹 a-tree checkable，按模块分组）

**视图层（4）**
- `views/UserManagement.vue` — 用户管理（筛选 + 表格 + 详情抽屉 + 角色分配弹窗）
- `views/RoleManagement.vue` — 角色管理（左列表 + 右详情 + 权限树 + CRUD 弹窗）
- `views/OAuthClients.vue` — OAuth 客户端（操作条 + 表格 + 新建/编辑弹窗）
- `views/Operators.vue` — 运营人员（筛选 + 表格 + 新建弹窗 + 权限分配弹窗）

**聚合层（2）**
- `routes.ts` — 4 条路由项，挂到 BasicLayout 子路由
- `index.ts` — 聚合导出 routes + 4 个 api 对象

### 依赖项（本 plan 假定 Plan 1 已就绪）
- `web/system-admin/src/shared/http/client.ts`（client + withIdempotency）
- `web/system-admin/src/shared/types/index.ts`（ApiResponse/PageResult/PageQuery）
- `web/system-admin/src/shared/auth/auth.store.ts`（useAuthStore）
- `web/system-admin/src/shared/utils/format.ts`（formatDateTime）
- `web/system-admin/src/shared/components/`（StatusTag/IdempotencyButton/PermissionGuard/DataTable/EmptyState/ConfirmDialog/DateTimeRangePicker）
- `web/system-admin/src/app/router.ts`（聚合入口，本 plan Task 11 在其后追加 userAccess 子路由数组）

---

## Task 1: 模块 DTO 类型层

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/types/user.dto.ts`
- Create: `web/system-admin/src/modules/02-user-access/types/role.dto.ts`
- Create: `web/system-admin/src/modules/02-user-access/types/oauth-client.dto.ts`
- Create: `web/system-admin/src/modules/02-user-access/types/operator.dto.ts`

- [ ] **Step 1: 创建 user.dto.ts**

```typescript
// web/system-admin/src/modules/02-user-access/types/user.dto.ts

// 用户状态：Active 正常 / Suspended 锁定 / Locked 系统锁定
export type UserStatus = 'Active' | 'Suspended' | 'Locked'

// 用户实体（对应后端 AdminUserDto）
export interface UserDto {
  id: string
  username: string
  email: string
  phone: string | null
  roles: string[]                 // 角色ID列表（用于分配角色穿梭框回填）
  status: UserStatus
  createdAt: string               // ISO 8601
  lastLoginAt: string | null
  lastLoginIp: string | null
}

// 列表查询参数（AdminUserQueryDto）
export interface ListUsersParams {
  keyword?: string                // 用户名/邮箱模糊匹配
  roles?: string[]                // 角色ID多选
  statuses?: UserStatus[]         // 状态多选
  fromTime?: string               // 注册时间起 ISO 8601 UTC
  toTime?: string                 // 注册时间止 ISO 8601 UTC
}

// 分配角色入参（PUT /admin/users/{id}/roles）
export interface AssignUserRolesDto {
  roleIds: string[]
}

// 状态变更入参（PUT /admin/users/{id}/status）
export interface UpdateUserStatusDto {
  status: 'Active' | 'Suspended'  // 仅允许在正常/锁定之间切换
  reason?: string                 // 锁定时必填，恢复时可选
}

// 登录历史条目（详情抽屉展示）
export interface UserLoginHistoryDto {
  loginAt: string
  loginIp: string
  success: boolean
  userAgent: string | null
}
```

- [ ] **Step 2: 创建 role.dto.ts**

```typescript
// web/system-admin/src/modules/02-user-access/types/role.dto.ts

// 角色实体（对应后端 RoleDto）
export interface RoleDto {
  id: string
  name: string
  description: string
  isBuiltIn: boolean               // 内置角色不可删、名不可改
  createdAt: string
  createdBy: string
  userCount: number                // 该角色下用户数
}

// 列表查询参数
export interface ListRolesParams {
  keyword?: string
}

// 创建/编辑入参（POST/PUT /admin/roles[/{id}]）
export interface SaveRoleDto {
  name: string
  description: string
}

// 权限更新入参（PUT /admin/roles/{id}/permissions，全量替换）
export interface UpdateRolePermissionsDto {
  permissions: string[]
}

// 权限目录中的单个权限项
export interface PermissionItemDto {
  code: string                     // 如 user:read
  label: string                    // 中文标签，如「查看用户」
}

// 权限目录按模块分组（GET /admin/roles/permissions/catalog 返回）
export interface PermissionGroupDto {
  module: string                   // 模块标识，如 user
  moduleLabel: string              // 模块中文名，如「用户管理」
  permissions: PermissionItemDto[]
}
```

- [ ] **Step 3: 创建 oauth-client.dto.ts**

```typescript
// web/system-admin/src/modules/02-user-access/types/oauth-client.dto.ts

// OAuth 客户端配置（对应后端 OAuthClientDto，Secret 始终掩码）
export interface OAuthClientDto {
  provider: string                 // github / google / wechat / qq / alipay
  clientId: string
  clientSecretMasked: string       // 形如 ******** 后4位
  scopes: string[]
  authorizationEndpoint: string
  tokenEndpoint: string
  userInfoEndpoint: string
  redirectUri: string              // 回调 URL
  enabled: boolean
}

// 新建/编辑入参（POST/PUT /admin/oauth-clients/{provider}）
export interface UpdateOAuthClientDto {
  clientId: string
  clientSecret: string             // 编辑时若留空则后端保留原密钥
  scopes: string[]
  authorizationEndpoint: string
  tokenEndpoint: string
  userInfoEndpoint: string
  redirectUri: string
}

// 列表筛选参数
export interface ListOAuthClientsParams {
  enabled?: boolean                // undefined=全部
}

// 受支持的 OAuth 提供方白名单（新建时下拉选项）
export const SUPPORTED_OAUTH_PROVIDERS = [
  'github',
  'google',
  'wechat',
  'qq',
  'alipay',
] as const

export type OAuthProvider = typeof SUPPORTED_OAUTH_PROVIDERS[number]

// 提供方中文标签映射（用于表格与下拉展示）
export const OAUTH_PROVIDER_LABELS: Record<string, string> = {
  github: 'GitHub',
  google: 'Google',
  wechat: '微信',
  qq: 'QQ',
  alipay: '支付宝',
}
```

- [ ] **Step 4: 创建 operator.dto.ts**

```typescript
// web/system-admin/src/modules/02-user-access/types/operator.dto.ts

// 运营人员状态
export type OperatorStatus = 'Active' | 'Inactive'

// 运营人员角色（后端枚举，对应 OperatorRole）
export type OperatorRole = 'Operator' | 'SeniorOperator' | 'Manager'

// 运营人员实体（对应后端 OperatorDto）
export interface OperatorDto {
  operatorId: string
  username: string
  name: string
  email: string
  role: OperatorRole
  status: OperatorStatus
  permissions: string[]           // 权限码列表
  createdAt: string
  lastLoginAt: string | null
}

// 列表查询参数
export interface ListOperatorsParams {
  role?: OperatorRole
  status?: OperatorStatus
}

// 创建运营人员入参（POST /admin/operators）
export interface SaveOperatorDto {
  username: string
  name: string
  email: string
  password: string                // 初始密码
  role: OperatorRole
}

// 权限分配入参（PUT /admin/operators/{id}/permissions，合并新增）
export interface AssignOperatorPermissionsDto {
  permissions: string[]
}

// 运营角色下拉选项（视图层复用）
export const OPERATOR_ROLE_OPTIONS: { label: string; value: OperatorRole }[] = [
  { label: '运营', value: 'Operator' },
  { label: '高级运营', value: 'SeniorOperator' },
  { label: '主管', value: 'Manager' },
]
```

- [ ] **Step 5: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（4 个 dto 文件无外部依赖，仅类型定义，可通过 strict 检查）

- [ ] **Step 6: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/types/
git commit -m "feat(system-admin/02-user-access): 新增用户/角色/OAuth/运营人员 DTO 类型定义"
```

---

## Task 2: users.api.ts + 单元测试（TDD）

**Files:**
- Test: `web/system-admin/src/modules/02-user-access/api/users.api.spec.ts`
- Create: `web/system-admin/src/modules/02-user-access/api/users.api.ts`

**目标端点（Identity 域 AdminUsersController）：**
- `GET /api/admin/users` 列表
- `GET /api/admin/users/{id}` 详情
- `PUT /api/admin/users/{id}/roles` 分配角色（幂等）
- `PUT /api/admin/users/{id}/status` 锁定/恢复（幂等）

- [ ] **Step 1: 写失败测试 users.api.spec.ts**

```typescript
// web/system-admin/src/modules/02-user-access/api/users.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { usersApi } from './users.api'
import type { ListUsersParams } from '../types/user.dto'

// 桩 shared/http：client 提供方法桩，withIdempotency 返回固定头
vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('usersApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 GET /admin/users 并透传筛选 params', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListUsersParams = {
      keyword: 'jack',
      roles: ['r-1'],
      statuses: ['Active'],
      fromTime: '2026-01-01T00:00:00Z',
      toTime: '2026-07-27T00:00:00Z',
      page: 1,
      pageSize: 20,
    }
    await usersApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/users', { params })
  })

  it('get 使用 GET /admin/users/{id}', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: {} })
    await usersApi.get('u-1')
    expect(client.get).toHaveBeenCalledWith('/admin/users/u-1')
  })

  it('assignRoles 使用 PUT /admin/users/{id}/roles 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    await usersApi.assignRoles('u-1', { roleIds: ['r-1', 'r-2'] })
    expect(client.put).toHaveBeenCalledWith(
      '/admin/users/u-1/roles',
      { roleIds: ['r-1', 'r-2'] },
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })

  it('updateStatus 使用 PUT /admin/users/{id}/status 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    await usersApi.updateStatus('u-1', { status: 'Suspended', reason: '违规操作' })
    expect(client.put).toHaveBeenCalledWith(
      '/admin/users/u-1/status',
      { status: 'Suspended', reason: '违规操作' },
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd web/system-admin && pnpm test -- src/modules/02-user-access/api/users.api.spec.ts`
Expected: FAIL，提示 `Failed to resolve import "./users.api"`（api 文件尚未创建）

- [ ] **Step 3: 实现 users.api.ts**

```typescript
// web/system-admin/src/modules/02-user-access/api/users.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  UserDto,
  ListUsersParams,
  AssignUserRolesDto,
  UpdateUserStatusDto,
} from '../types/user.dto'

// 用户管理 API（Identity 域 AdminUsersController）
export const usersApi = {
  // 分页查询用户列表
  list: (params: ListUsersParams & PageQuery) =>
    client.get<PageResult<UserDto>>('/admin/users', { params }),

  // 查询单个用户详情
  get: (id: string) =>
    client.get<UserDto>(`/admin/users/${id}`),

  // 为用户分配角色（幂等，全量替换）
  assignRoles: (id: string, body: AssignUserRolesDto) =>
    client.put<UserDto>(`/admin/users/${id}/roles`, body, withIdempotency()),

  // 锁定/恢复用户账户（幂等）
  updateStatus: (id: string, body: UpdateUserStatusDto) =>
    client.put<UserDto>(`/admin/users/${id}/status`, body, withIdempotency()),
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `cd web/system-admin && pnpm test -- src/modules/02-user-access/api/users.api.spec.ts`
Expected: PASS（4 个测试用例全部通过）

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/api/users.api.ts web/system-admin/src/modules/02-user-access/api/users.api.spec.ts
git commit -m "feat(system-admin/02-user-access): 实现 usersApi 并补充幂等键注入单元测试"
```

---

## Task 3: roles.api.ts + 单元测试（TDD）

**Files:**
- Test: `web/system-admin/src/modules/02-user-access/api/roles.api.spec.ts`
- Create: `web/system-admin/src/modules/02-user-access/api/roles.api.ts`

**目标端点（AccessControl 域 AdminRolesController，共 7 + 1 catalog）：**
- `GET /api/admin/roles` / `GET /api/admin/roles/{id}`
- `POST /api/admin/roles` / `PUT /api/admin/roles/{id}` / `DELETE /api/admin/roles/{id}`
- `GET /api/admin/roles/{id}/permissions` / `PUT /api/admin/roles/{id}/permissions`
- `GET /api/admin/roles/permissions/catalog` 权限目录（前端构建权限树所需）

- [ ] **Step 1: 写失败测试 roles.api.spec.ts**

```typescript
// web/system-admin/src/modules/02-user-access/api/roles.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { rolesApi } from './roles.api'
import type { ListRolesParams, SaveRoleDto, UpdateRolePermissionsDto } from '../types/role.dto'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), put: vi.fn(), post: vi.fn(), delete: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('rolesApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list 使用 GET /admin/roles 并透传 keyword', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListRolesParams = { keyword: '运营', page: 1, pageSize: 20 }
    await rolesApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/roles', { params })
  })

  it('get 使用 GET /admin/roles/{id}', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: {} })
    await rolesApi.get('r-1')
    expect(client.get).toHaveBeenCalledWith('/admin/roles/r-1')
  })

  it('create 使用 POST /admin/roles 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    const body: SaveRoleDto = { name: '运营经理', description: '负责日常运营' }
    await rolesApi.create(body)
    expect(client.post).toHaveBeenCalledWith(
      '/admin/roles',
      body,
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })

  it('update 使用 PUT /admin/roles/{id} 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    const body: SaveRoleDto = { name: '运营经理', description: '修改描述' }
    await rolesApi.update('r-1', body)
    expect(client.put).toHaveBeenCalledWith(
      '/admin/roles/r-1',
      body,
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })

  it('remove 使用 DELETE /admin/roles/{id}', async () => {
    vi.mocked(client.delete).mockResolvedValue({ data: undefined })
    await rolesApi.remove('r-1')
    expect(client.delete).toHaveBeenCalledWith('/admin/roles/r-1')
  })

  it('getPermissions 使用 GET /admin/roles/{id}/permissions', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: [] })
    await rolesApi.getPermissions('r-1')
    expect(client.get).toHaveBeenCalledWith('/admin/roles/r-1/permissions')
  })

  it('getPermissionCatalog 使用 GET /admin/roles/permissions/catalog', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: [] })
    await rolesApi.getPermissionCatalog()
    expect(client.get).toHaveBeenCalledWith('/admin/roles/permissions/catalog')
  })

  it('updatePermissions 使用 PUT /admin/roles/{id}/permissions 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: undefined })
    const body: UpdateRolePermissionsDto = { permissions: ['user:read', 'role:write'] }
    await rolesApi.updatePermissions('r-1', body)
    expect(client.put).toHaveBeenCalledWith(
      '/admin/roles/r-1/permissions',
      body,
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd web/system-admin && pnpm test -- src/modules/02-user-access/api/roles.api.spec.ts`
Expected: FAIL，提示 `Failed to resolve import "./roles.api"`

- [ ] **Step 3: 实现 roles.api.ts**

```typescript
// web/system-admin/src/modules/02-user-access/api/roles.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  RoleDto,
  ListRolesParams,
  SaveRoleDto,
  UpdateRolePermissionsDto,
  PermissionGroupDto,
} from '../types/role.dto'

// 角色管理 API（AccessControl 域 AdminRolesController）
export const rolesApi = {
  // 分页查询角色列表
  list: (params: ListRolesParams & PageQuery) =>
    client.get<PageResult<RoleDto>>('/admin/roles', { params }),

  // 查询角色详情
  get: (id: string) =>
    client.get<RoleDto>(`/admin/roles/${id}`),

  // 创建角色（幂等）
  create: (body: SaveRoleDto) =>
    client.post<RoleDto>('/admin/roles', body, withIdempotency()),

  // 编辑角色（幂等）
  update: (id: string, body: SaveRoleDto) =>
    client.put<RoleDto>(`/admin/roles/${id}`, body, withIdempotency()),

  // 删除角色（内置角色后端拒绝）
  remove: (id: string) =>
    client.delete<void>(`/admin/roles/${id}`),

  // 查看角色已分配的权限码列表
  getPermissions: (id: string) =>
    client.get<string[]>(`/admin/roles/${id}/permissions`),

  // 获取全量权限目录（按模块分组，用于权限树渲染）
  getPermissionCatalog: () =>
    client.get<PermissionGroupDto[]>('/admin/roles/permissions/catalog'),

  // 全量替换角色权限（幂等）
  updatePermissions: (id: string, body: UpdateRolePermissionsDto) =>
    client.put<void>(`/admin/roles/${id}/permissions`, body, withIdempotency()),
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `cd web/system-admin && pnpm test -- src/modules/02-user-access/api/roles.api.spec.ts`
Expected: PASS（8 个测试用例全部通过）

- [ ] **Step 5: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/api/roles.api.ts web/system-admin/src/modules/02-user-access/api/roles.api.spec.ts
git commit -m "feat(system-admin/02-user-access): 实现 rolesApi 7+1 端点并补充单元测试"
```

---

## Task 4: oauth-clients.api.ts

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/api/oauth-clients.api.ts`

**目标端点（Identity 域 AdminOAuthClientsController）：**
- `GET /api/admin/oauth-clients` 全量查询
- `POST /api/admin/oauth-clients/{provider}` 新建（默认禁用）
- `PUT /api/admin/oauth-clients/{provider}` 更新
- `POST /api/admin/oauth-clients/{provider}/enable` 启用
- `POST /api/admin/oauth-clients/{provider}/disable` 禁用

- [ ] **Step 1: 实现 oauth-clients.api.ts**

```typescript
// web/system-admin/src/modules/02-user-access/api/oauth-clients.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { OAuthClientDto, UpdateOAuthClientDto, ListOAuthClientsParams } from '../types/oauth-client.dto'

// OAuth 客户端管理 API（Identity 域 AdminOAuthClientsController）
export const oauthClientsApi = {
  // 查询所有 OAuth 客户端配置（Secret 掩码）
  list: (params?: ListOAuthClientsParams) =>
    client.get<OAuthClientDto[]>('/admin/oauth-clients', { params }),

  // 新建 OAuth 客户端配置（默认禁用，需显式调用 enable）
  create: (provider: string, body: UpdateOAuthClientDto) =>
    client.post<OAuthClientDto>(`/admin/oauth-clients/${provider}`, body, withIdempotency()),

  // 更新指定提供方配置
  update: (provider: string, body: UpdateOAuthClientDto) =>
    client.put<OAuthClientDto>(`/admin/oauth-clients/${provider}`, body, withIdempotency()),

  // 启用指定提供方（幂等）
  enable: (provider: string) =>
    client.post<OAuthClientDto>(`/admin/oauth-clients/${provider}/enable`, null, withIdempotency()),

  // 禁用指定提供方（幂等）
  disable: (provider: string) =>
    client.post<OAuthClientDto>(`/admin/oauth-clients/${provider}/disable`, null, withIdempotency()),
}
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（API 文件仅依赖已存在的 shared/http 与 types/oauth-client.dto.ts）

- [ ] **Step 3: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/api/oauth-clients.api.ts
git commit -m "feat(system-admin/02-user-access): 实现 oauthClientsApi 5 端点（含 enable/disable 幂等）"
```

---

## Task 5: operators.api.ts

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/api/operators.api.ts`

**目标端点（SystemAdmin 域 OperatorsController）：**
- `GET /api/admin/operators` 列表
- `GET /api/admin/operators/{id}` 详情
- `POST /api/admin/operators` 创建
- `PUT /api/admin/operators/{id}/permissions` 权限（合并新增）
- `POST /api/admin/operators/{id}/activate` 启用
- `POST /api/admin/operators/{id}/deactivate` 停用

- [ ] **Step 1: 实现 operators.api.ts**

```typescript
// web/system-admin/src/modules/02-user-access/api/operators.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  OperatorDto,
  ListOperatorsParams,
  SaveOperatorDto,
  AssignOperatorPermissionsDto,
} from '../types/operator.dto'

// 运营人员管理 API（SystemAdmin 域 OperatorsController）
export const operatorsApi = {
  // 分页查询运营人员
  list: (params: ListOperatorsParams & PageQuery) =>
    client.get<PageResult<OperatorDto>>('/admin/operators', { params }),

  // 按标识获取运营人员详情
  get: (operatorId: string) =>
    client.get<OperatorDto>(`/admin/operators/${operatorId}`),

  // 创建运营人员（幂等）
  create: (body: SaveOperatorDto) =>
    client.post<OperatorDto>('/admin/operators', body, withIdempotency()),

  // 更新运营人员权限（合并新增权限码，幂等）
  updatePermissions: (operatorId: string, body: AssignOperatorPermissionsDto) =>
    client.put<OperatorDto>(`/admin/operators/${operatorId}/permissions`, body, withIdempotency()),

  // 启用运营人员（幂等）
  activate: (operatorId: string) =>
    client.post<OperatorDto>(`/admin/operators/${operatorId}/activate`, null, withIdempotency()),

  // 停用运营人员（幂等）
  deactivate: (operatorId: string) =>
    client.post<OperatorDto>(`/admin/operators/${operatorId}/deactivate`, null, withIdempotency()),
}
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 运行全部 API 单元测试**

Run: `cd web/system-admin && pnpm test -- src/modules/02-user-access/api/`
Expected: PASS（users.api.spec.ts + roles.api.spec.ts 共 12 个测试用例通过，新文件无测试不影响）

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/api/operators.api.ts
git commit -m "feat(system-admin/02-user-access): 实现 operatorsApi 6 端点（含 activate/deactivate 幂等）"
```

---

## Task 6: RolePermissionMatrix.vue 角色-权限矩阵编辑器

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/components/RolePermissionMatrix.vue`

**职责：** 包裹 `<a-tree checkable>`，按模块分组渲染权限目录，支持父子联动勾选，对外暴露已选权限码数组（不含模块分组键）。

**Props：**
- `catalog: PermissionGroupDto[]` — 全量权限目录（来自 `rolesApi.getPermissionCatalog()`）
- `selected: string[]` — 当前角色已选权限码
- `loading?: boolean` — 加载态

**Emits：**
- `update:selected` — 用户勾选变化时回传纯权限码数组（已剔除 `module:` 前缀的分组键）

- [ ] **Step 1: 实现 RolePermissionMatrix.vue**

```vue
<!-- web/system-admin/src/modules/02-user-access/components/RolePermissionMatrix.vue -->
<template>
  <a-spin :spinning="loading">
    <EmptyState
      v-if="catalog.length === 0"
      description="暂无可分配权限"
      action-text="刷新"
      @action="emit('refresh')"
    />
    <a-tree
      v-else
      v-model:checked-keys="checkedKeys"
      :tree-data="treeData"
      checkable
      :default-expand-all="true"
      :selectable="false"
    >
      <template #title="{ key, title }">
        <span :class="{ 'permission-code': isPermissionLeaf(key) }">{{ title }}</span>
      </template>
    </a-tree>
  </a-spin>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { TreeProps } from 'ant-design-vue'
import type { PermissionGroupDto } from '../types/role.dto'
import EmptyState from '@/shared/components/EmptyState.vue'

interface Props {
  catalog: PermissionGroupDto[]
  selected: string[]
  loading?: boolean
}
const props = withDefaults(defineProps<Props>(), { loading: false })

const emit = defineEmits<{
  (e: 'update:selected', value: string[]): void
  (e: 'refresh'): void
}>()

// checkedKeys 同时包含「模块分组键」与「权限码」两种；
// 初始化时仅传入权限码，模块分组键由 a-tree 自动计算半选状态
const checkedKeys = ref<string[]>([...props.selected])

// 外部 selected 变化时同步（如切换角色后重新拉取权限）
watch(
  () => props.selected,
  (val) => {
    checkedKeys.value = [...val]
  },
)

// checkedKeys 变化时过滤掉分组键，仅回传权限码
watch(checkedKeys, (keys) => {
  const codes = keys.filter((k) => !k.startsWith('module:'))
  emit('update:selected', codes)
})

// 构造 a-tree 数据结构：模块为父节点（key 加 module: 前缀避免与权限码冲突），权限为叶子
const treeData = computed<TreeProps['treeData']>(() =>
  props.catalog.map((group) => ({
    key: `module:${group.module}`,
    title: group.moduleLabel,
    children: group.permissions.map((p) => ({
      key: p.code,
      title: p.label ? `${p.label} (${p.code})` : p.code,
    })),
  })),
)

function isPermissionLeaf(key: string | number): boolean {
  return typeof key === 'string' && !key.startsWith('module:')
}
</script>

<style scoped>
.permission-code {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  color: #595959;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（组件仅依赖已存在的 EmptyState 与 role.dto.ts）

- [ ] **Step 3: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/components/RolePermissionMatrix.vue
git commit -m "feat(system-admin/02-user-access): 实现 RolePermissionMatrix 权限矩阵编辑器组件"
```

---

## Task 7: UserManagement.vue 用户管理视图

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/views/UserManagement.vue`

**对应 design-prompt：** `02-user-access/user-management.md`

**布局：** 顶部筛选条（关键词/角色多选/状态多选/注册时间范围）+ 主表格 + 详情抽屉 + 角色分配弹窗（a-transfer）+ 锁定确认对话框。

- [ ] **Step 1: 实现 UserManagement.vue**

```vue
<!-- web/system-admin/src/modules/02-user-access/views/UserManagement.vue -->
<template>
  <div class="user-management">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="搜索">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="用户名/邮箱"
            allow-clear
            style="width: 220px"
            @search="onSearch"
          />
        </a-form-item>
        <a-form-item label="角色">
          <a-select
            v-model:value="filters.roles"
            mode="multiple"
            placeholder="全部角色"
            allow-clear
            style="width: 200px"
            :options="roleOptions"
            :field-names="{ label: 'label', value: 'value' }"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.statuses"
            mode="multiple"
            placeholder="全部状态"
            allow-clear
            style="width: 180px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="注册时间">
          <DateTimeRangePicker v-model="filters.dateRange" @change="onDateRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" class="table-card">
      <DataTable
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="未找到匹配用户" action-text="清空筛选条件" @action="onReset" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'roles'">
            <a-tag v-for="r in record.roles" :key="r" color="blue">{{ roleLabel(r) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="user" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'lastLoginAt'">
            {{ record.lastLoginAt ? formatDateTime(record.lastLoginAt) : '—' }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onView(record)">查看</a-button>
              <PermissionGuard permission="user:assign-role">
                <a-button type="link" size="small" @click="onAssignRoles(record)">分配角色</a-button>
              </PermissionGuard>
              <PermissionGuard permission="user:suspend">
                <IdempotencyButton
                  v-if="record.status !== 'Suspended'"
                  type="link"
                  size="small"
                  danger
                  @click="onLock(record)"
                >锁定</IdempotencyButton>
                <IdempotencyButton
                  v-else
                  type="link"
                  size="small"
                  @click="onUnlock(record)"
                >恢复</IdempotencyButton>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </DataTable>
    </a-card>

    <!-- 区域 C：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="用户详情"
      placement="right"
      width="600"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <a-descriptions v-if="detail" :column="1" bordered>
          <a-descriptions-item label="用户 ID">{{ detail.id }}</a-descriptions-item>
          <a-descriptions-item label="用户名">{{ detail.username }}</a-descriptions-item>
          <a-descriptions-item label="邮箱">{{ detail.email }}</a-descriptions-item>
          <a-descriptions-item label="手机">{{ detail.phone || '—' }}</a-descriptions-item>
          <a-descriptions-item label="角色">
            <a-tag v-for="r in detail.roles" :key="r" color="blue">{{ roleLabel(r) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <StatusTag type="user" :status="detail.status" />
          </a-descriptions-item>
          <a-descriptions-item label="注册时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
          <a-descriptions-item label="最近登录">
            {{ detail.lastLoginAt
              ? `${formatDateTime(detail.lastLoginAt)}（IP ${detail.lastLoginIp ?? '—'}）`
              : '从未登录' }}
          </a-descriptions-item>
        </a-descriptions>
        <a-divider>审计记录</a-divider>
        <a-button type="link" :disabled="!detail" @click="goToAuditLogs">查看审计记录</a-button>
      </a-spin>
    </a-drawer>

    <!-- 区域 D：角色分配弹窗 -->
    <a-modal
      v-model:open="rolesModalOpen"
      title="分配角色"
      :destroy-on-close="true"
      :confirm-loading="submitting"
      @ok="onSubmitRoles"
    >
      <a-transfer
        v-model:target-keys="targetRoleIds"
        :data-source="roleTransferData"
        :titles="['可分配角色', '已分配']"
        :render="(item: { key: string; title: string }) => item.title"
        row-key="key"
      />
    </a-modal>

    <!-- 锁定确认对话框 -->
    <ConfirmDialog
      :open="lockConfirmOpen"
      danger
      title="锁定用户"
      content="锁定后该用户将无法登录，关联的进行中订单不受影响。此操作可逆，可随时恢复。"
      @ok="onConfirmLock"
      @cancel="lockConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { usersApi } from '../api/users.api'
import { rolesApi } from '../api/roles.api'
import type { UserDto, UserStatus, ListUsersParams } from '../types/user.dto'
import type { RoleDto } from '../types/role.dto'
import { formatDateTime } from '@/shared/utils/format'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import DataTable from '@/shared/components/DataTable.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'

const router = useRouter()

interface FilterState {
  keyword: string
  roles: string[]
  statuses: UserStatus[]
  dateRange: [string, string] | null
}

const filters = reactive<FilterState>({
  keyword: '',
  roles: [],
  statuses: [],
  dateRange: null,
})

const statusOptions = [
  { label: 'Active', value: 'Active' },
  { label: 'Suspended', value: 'Suspended' },
  { label: 'Locked', value: 'Locked' },
]

const roleOptions = ref<{ label: string; value: string }[]>([])
const roleMap = ref<Map<string, string>>(new Map())

function roleLabel(id: string): string {
  return roleMap.value.get(id) ?? id
}

const columns: TableColumnsType = [
  { title: '用户 ID', dataIndex: 'id', key: 'id', width: 140, ellipsis: true },
  { title: '用户名', dataIndex: 'username', key: 'username', width: 140 },
  { title: '邮箱', dataIndex: 'email', key: 'email', width: 220, ellipsis: true },
  { title: '角色', key: 'roles', width: 180 },
  { title: '状态', key: 'status', width: 100 },
  { title: '注册时间', key: 'createdAt', width: 160, responsive: ['xl'] },
  { title: '最近登录', dataIndex: 'lastLoginAt', key: 'lastLoginAt', width: 160 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' },
]

const tableData = ref<UserDto[]>([])
const loading = ref(false)
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchUsers() {
  loading.value = true
  try {
    const params: ListUsersParams & { page: number; pageSize: number } = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.keyword) params.keyword = filters.keyword
    if (filters.roles.length) params.roles = filters.roles
    if (filters.statuses.length) params.statuses = filters.statuses
    if (filters.dateRange && filters.dateRange.length === 2) {
      params.fromTime = filters.dateRange[0]
      params.toTime = filters.dateRange[1]
    }
    const { data } = await usersApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch {
    message.error('加载用户列表失败')
  } finally {
    loading.value = false
  }
}

async function fetchRoleOptions() {
  try {
    const { data } = await rolesApi.list({ page: 1, pageSize: 100 })
    roleOptions.value = data.items.map((r: RoleDto) => ({ label: r.name, value: r.id }))
    roleMap.value = new Map(data.items.map((r: RoleDto) => [r.id, r.name]))
  } catch {
    roleOptions.value = []
    roleMap.value = new Map()
  }
}

function onQuery() {
  pagination.current = 1
  fetchUsers()
}

function onReset() {
  filters.keyword = ''
  filters.roles = []
  filters.statuses = []
  filters.dateRange = null
  onQuery()
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearch() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    onQuery()
  }, 300)
}

function onDateRangeChange(value: [string, string] | null) {
  filters.dateRange = value
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchUsers()
}

// 详情抽屉
const drawerOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<UserDto | null>(null)

async function onView(record: UserDto) {
  drawerOpen.value = true
  detailLoading.value = true
  try {
    const { data } = await usersApi.get(record.id)
    detail.value = data
  } catch {
    message.error('加载用户详情失败')
  } finally {
    detailLoading.value = false
  }
}

function goToAuditLogs() {
  if (detail.value) {
    router.push({ path: '/audit/audit-logs', query: { operatorId: detail.value.id } })
  }
}

// 分配角色
const rolesModalOpen = ref(false)
const submitting = ref(false)
const targetRoleIds = ref<string[]>([])
const roleTransferData = ref<{ key: string; title: string }[]>([])
const currentUser = ref<UserDto | null>(null)

async function onAssignRoles(record: UserDto) {
  currentUser.value = record
  rolesModalOpen.value = true
  try {
    const { data } = await rolesApi.list({ page: 1, pageSize: 100 })
    roleTransferData.value = data.items.map((r: RoleDto) => ({ key: r.id, title: r.name }))
    targetRoleIds.value = [...record.roles]
  } catch {
    message.error('加载角色列表失败')
  }
}

async function onSubmitRoles() {
  if (!currentUser.value) return
  submitting.value = true
  try {
    await usersApi.assignRoles(currentUser.value.id, { roleIds: targetRoleIds.value })
    message.success('角色已分配')
    rolesModalOpen.value = false
    await fetchUsers()
  } catch {
    message.error('角色分配失败')
  } finally {
    submitting.value = false
  }
}

// 锁定/恢复
const lockConfirmOpen = ref(false)
const pendingAction = ref<{ id: string; status: 'Active' | 'Suspended' } | null>(null)

function onLock(record: UserDto) {
  pendingAction.value = { id: record.id, status: 'Suspended' }
  lockConfirmOpen.value = true
}

function onUnlock(record: UserDto) {
  pendingAction.value = { id: record.id, status: 'Active' }
  void doUpdateStatus()
}

async function onConfirmLock() {
  lockConfirmOpen.value = false
  await doUpdateStatus()
}

async function doUpdateStatus() {
  if (!pendingAction.value) return
  const action = pendingAction.value
  try {
    const body =
      action.status === 'Suspended'
        ? { status: 'Suspended' as const, reason: '管理员手动锁定' }
        : { status: 'Active' as const }
    await usersApi.updateStatus(action.id, body)
    message.success(action.status === 'Suspended' ? '已锁定' : '已恢复')
    await fetchUsers()
  } catch {
    message.error('状态变更失败')
  } finally {
    pendingAction.value = null
  }
}

onMounted(() => {
  fetchRoleOptions()
  fetchUsers()
})
</script>

<style scoped>
.user-management {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 启动 dev 服务器人工校验**

Run: `cd web/system-admin && pnpm dev`
打开浏览器访问 `/user-access/users`：
- 筛选条 4 个控件正常渲染
- 表格展示用户列表，分页切换触发请求
- 点击「查看」打开抽屉展示详情
- 点击「锁定」弹出红色确认按钮的 ConfirmDialog
Expected: 页面无控制台报错，交互符合 design-prompt §4 主流程

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/views/UserManagement.vue
git commit -m "feat(system-admin/02-user-access): 实现 UserManagement 视图（筛选+表格+抽屉+角色分配+锁定）"
```

---

## Task 8: RoleManagement.vue 角色管理视图

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/views/RoleManagement.vue`

**对应 design-prompt：** `02-user-access/role-management.md`

**布局：** 左侧角色列表（含搜索/新增）+ 右侧角色详情（a-descriptions + 编辑/删除按钮）+ 权限矩阵（RolePermissionMatrix）+ 新建/编辑弹窗 + 删除确认。

- [ ] **Step 1: 实现 RoleManagement.vue**

```vue
<!-- web/system-admin/src/modules/02-user-access/views/RoleManagement.vue -->
<template>
  <div class="role-management">
    <a-row :gutter="24">
      <!-- 区域 A：左侧角色列表 -->
      <a-col :xs="24" :xl="8" :xxl="6">
        <a-card :bordered="false" class="role-list-card">
          <template #title>
            <span>角色列表</span>
          </template>
          <template #extra>
            <a-button type="primary" size="small" @click="onCreate">
              <template #icon><PlusOutlined /></template>
              新增角色
            </a-button>
          </template>
          <a-input-search
            v-model:value="listKeyword"
            placeholder="搜索角色名"
            allow-clear
            style="margin-bottom: 12px"
            @search="fetchRoles"
          />
          <a-spin :spinning="listLoading">
            <EmptyState
              v-if="roles.length === 0"
              description="暂无角色"
              action-text="新增角色"
              @action="onCreate"
            />
            <a-list v-else :data-source="roles" :split="true">
              <template #renderItem="{ item }">
                <a-list-item
                  :class="{ 'role-item-active': selectedRole?.id === item.id }"
                  @click="onSelectRole(item)"
                >
                  <a-list-item-meta>
                    <template #title>
                      <span>{{ item.name }}</span>
                      <a-tag v-if="item.isBuiltIn" color="purple" style="margin-left: 8px">内置</a-tag>
                      <a-tag v-else color="blue" style="margin-left: 8px">自定义</a-tag>
                    </template>
                    <template #description>
                      {{ item.description || '无描述' }} · 用户 {{ item.userCount }} 人
                    </template>
                  </a-list-item-meta>
                </a-list-item>
              </template>
            </a-list>
          </a-spin>
        </a-card>
      </a-col>

      <!-- 区域 B：右侧详情与权限 -->
      <a-col :xs="24" :xl="16" :xxl="18">
        <a-card :bordered="false">
          <a-spin :spinning="detailLoading">
            <EmptyState
              v-if="!selectedRole"
              description="请从左侧选择一个角色"
            />
            <template v-else>
              <a-descriptions :column="2" bordered>
                <a-descriptions-item label="角色名">{{ selectedRole.name }}</a-descriptions-item>
                <a-descriptions-item label="类型">
                  <a-tag v-if="selectedRole.isBuiltIn" color="purple">内置</a-tag>
                  <a-tag v-else color="blue">自定义</a-tag>
                </a-descriptions-item>
                <a-descriptions-item label="描述" :span="2">{{ selectedRole.description || '—' }}</a-descriptions-item>
                <a-descriptions-item label="创建人">{{ selectedRole.createdBy }}</a-descriptions-item>
                <a-descriptions-item label="创建时间">{{ formatDateTime(selectedRole.createdAt) }}</a-descriptions-item>
                <a-descriptions-item label="用户数" :span="2">
                  <a-button type="link" @click="goToUsersByRole">{{ selectedRole.userCount }} 人</a-button>
                </a-descriptions-item>
              </a-descriptions>

              <div class="role-actions">
                <a-space>
                  <a-button @click="onEdit">
                    <template #icon><EditOutlined /></template>
                    编辑
                  </a-button>
                  <a-tooltip :title="selectedRole.isBuiltIn ? '内置角色不可删除' : ''">
                    <a-button
                      danger
                      :disabled="selectedRole.isBuiltIn"
                      @click="onDelete"
                    >
                      <template #icon><DeleteOutlined /></template>
                      删除
                    </a-button>
                  </a-tooltip>
                </a-space>
              </div>

              <a-divider>权限分配</a-divider>
              <RolePermissionMatrix
                :catalog="permissionCatalog"
                :selected="selectedPermissions"
                :loading="permissionLoading"
                @update:selected="onPermissionsChange"
                @refresh="fetchPermissions"
              />
              <div class="permission-actions">
                <IdempotencyButton
                  type="primary"
                  :loading="savingPermissions"
                  :disabled="!permissionsDirty"
                  @click="onSavePermissions"
                >保存权限</IdempotencyButton>
              </div>
            </template>
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="formModalOpen"
      :title="formMode === 'create' ? '新增角色' : '编辑角色'"
      :destroy-on-close="true"
      :confirm-loading="formSubmitting"
      @ok="onSubmitForm"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item label="角色名" name="name">
          <a-input
            v-model:value="formData.name"
            :disabled="formMode === 'edit' && selectedRole?.isBuiltIn"
            placeholder="请输入角色名"
            :maxlength="32"
          />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea
            v-model:value="formData.description"
            placeholder="请输入角色描述"
            :rows="3"
            :maxlength="200"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 删除确认 -->
    <ConfirmDialog
      :open="deleteConfirmOpen"
      danger
      title="删除角色"
      content="删除后该角色的权限配置将丢失，已分配该角色的用户需重新分配。此操作不可逆。"
      @ok="onConfirmDelete"
      @cancel="deleteConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { rolesApi } from '../api/roles.api'
import type {
  RoleDto,
  ListRolesParams,
  SaveRoleDto,
  PermissionGroupDto,
} from '../types/role.dto'
import { formatDateTime } from '@/shared/utils/format'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import RolePermissionMatrix from '../components/RolePermissionMatrix.vue'

const router = useRouter()

const listKeyword = ref('')
const roles = ref<RoleDto[]>([])
const listLoading = ref(false)
const selectedRole = ref<RoleDto | null>(null)
const detailLoading = ref(false)

const permissionCatalog = ref<PermissionGroupDto[]>([])
const selectedPermissions = ref<string[]>([])
const originalPermissions = ref<string[]>([])
const permissionLoading = ref(false)
const savingPermissions = ref(false)

const permissionsDirty = computed(
  () => JSON.stringify([...selectedPermissions.value].sort())
    !== JSON.stringify([...originalPermissions.value].sort()),
)

async function fetchRoles() {
  listLoading.value = true
  try {
    const params: ListRolesParams & { page: number; pageSize: number } = {
      page: 1,
      pageSize: 100,
    }
    if (listKeyword.value) params.keyword = listKeyword.value
    const { data } = await rolesApi.list(params)
    roles.value = data.items
    if (roles.value.length > 0 && !selectedRole.value) {
      await onSelectRole(roles.value[0]!)
    }
  } catch {
    message.error('加载角色列表失败')
  } finally {
    listLoading.value = false
  }
}

async function onSelectRole(role: RoleDto) {
  selectedRole.value = role
  detailLoading.value = true
  await fetchPermissions()
  detailLoading.value = false
}

async function fetchPermissions() {
  if (!selectedRole.value) return
  permissionLoading.value = true
  try {
    const [permRes, catalogRes] = await Promise.all([
      rolesApi.getPermissions(selectedRole.value.id),
      rolesApi.getPermissionCatalog(),
    ])
    selectedPermissions.value = [...permRes.data]
    originalPermissions.value = [...permRes.data]
    permissionCatalog.value = catalogRes.data
  } catch {
    message.error('加载权限失败')
  } finally {
    permissionLoading.value = false
  }
}

function onPermissionsChange(codes: string[]) {
  selectedPermissions.value = codes
}

async function onSavePermissions() {
  if (!selectedRole.value) return
  savingPermissions.value = true
  try {
    await rolesApi.updatePermissions(selectedRole.value.id, {
      permissions: selectedPermissions.value,
    })
    message.success('权限已更新')
    originalPermissions.value = [...selectedPermissions.value]
  } catch {
    message.error('权限保存失败')
  } finally {
    savingPermissions.value = false
  }
}

function goToUsersByRole() {
  if (selectedRole.value) {
    router.push({ path: '/user-access/users', query: { roleId: selectedRole.value.id } })
  }
}

// 新建/编辑
const formModalOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()
const formData = reactive<SaveRoleDto>({ name: '', description: '' })
const formSubmitting = ref(false)

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入角色名', trigger: 'blur' }],
}

function onCreate() {
  formMode.value = 'create'
  formData.name = ''
  formData.description = ''
  formModalOpen.value = true
}

function onEdit() {
  if (!selectedRole.value) return
  formMode.value = 'edit'
  formData.name = selectedRole.value.name
  formData.description = selectedRole.value.description
  formModalOpen.value = true
}

async function onSubmitForm() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  formSubmitting.value = true
  try {
    if (formMode.value === 'create') {
      await rolesApi.create({ name: formData.name, description: formData.description })
      message.success('角色已创建')
    } else if (selectedRole.value) {
      await rolesApi.update(selectedRole.value.id, {
        name: formData.name,
        description: formData.description,
      })
      message.success('角色已更新')
    }
    formModalOpen.value = false
    await fetchRoles()
  } catch {
    message.error(formMode.value === 'create' ? '创建角色失败' : '更新角色失败')
  } finally {
    formSubmitting.value = false
  }
}

// 删除
const deleteConfirmOpen = ref(false)

function onDelete() {
  if (selectedRole.value?.isBuiltIn) return
  deleteConfirmOpen.value = true
}

async function onConfirmDelete() {
  deleteConfirmOpen.value = false
  if (!selectedRole.value) return
  try {
    await rolesApi.remove(selectedRole.value.id)
    message.success('角色已删除')
    selectedRole.value = null
    selectedPermissions.value = []
    originalPermissions.value = []
    await fetchRoles()
  } catch {
    message.error('删除失败：可能该角色下仍有用户，请先迁移')
  }
}

onMounted(() => {
  fetchRoles()
})
</script>

<style scoped>
.role-management {
  min-height: 100%;
}
.role-list-card :deep(.ant-list-item) {
  cursor: pointer;
  padding: 12px 16px;
}
.role-list-card :deep(.ant-list-item.role-item-active) {
  background-color: #e6f4ff;
}
.role-actions {
  margin-top: 16px;
}
.permission-actions {
  margin-top: 16px;
  text-align: right;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 启动 dev 服务器人工校验**

Run: `cd web/system-admin && pnpm dev`
访问 `/user-access/roles`：
- 左侧角色列表渲染，自动选中首个角色
- 右侧展示详情与权限树，已选权限预填
- 内置角色「删除」按钮 disabled 且 Tooltip 提示
- 勾选权限后「保存权限」按钮启用，保存后提示成功
Expected: 交互符合 design-prompt §4 主流程，权限树父子联动生效

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/views/RoleManagement.vue
git commit -m "feat(system-admin/02-user-access): 实现 RoleManagement 视图（列表+详情+权限矩阵+CRUD）"
```

---

## Task 9: OAuthClients.vue OAuth 客户端视图

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/views/OAuthClients.vue`

**对应 design-prompt：** `02-user-access/oauth-clients.md`

**布局：** 顶部操作条（新建/状态筛选/刷新）+ 主表格（Secret 掩码）+ 新建/编辑弹窗 + 启停确认对话框。

- [ ] **Step 1: 实现 OAuthClients.vue**

```vue
<!-- web/system-admin/src/modules/02-user-access/views/OAuthClients.vue -->
<template>
  <div class="oauth-clients">
    <!-- 区域 A：操作条 -->
    <a-card :bordered="false" class="action-card">
      <a-space>
        <a-button type="primary" @click="onCreate">
          <template #icon><PlusOutlined /></template>
          新建提供方
        </a-button>
        <a-select
          v-model:value="statusFilter"
          style="width: 140px"
          :options="statusFilterOptions"
          @change="onFilterChange"
        />
        <a-button @click="fetchList">刷新</a-button>
      </a-space>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" class="table-card">
      <DataTable
        :columns="columns"
        :data-source="filteredData"
        :loading="loading"
        row-key="provider"
        :pagination="false"
      >
        <template #emptyText>
          <EmptyState description="暂无 OAuth 提供方配置" action-text="新建提供方" @action="onCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'provider'">
            <span class="provider-name">{{ providerLabel(record.provider) }}</span>
          </template>
          <template v-else-if="column.key === 'clientSecretMasked'">
            <span class="secret-masked">{{ record.clientSecretMasked }}</span>
          </template>
          <template v-else-if="column.key === 'enabled'">
            <StatusTag type="oauth" :status="record.enabled ? 'Enabled' : 'Disabled'" />
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onEdit(record)">编辑</a-button>
              <PermissionGuard permission="oauth:write">
                <a-button
                  v-if="!record.enabled"
                  type="link"
                  size="small"
                  @click="onToggle(record, 'enable')"
                >启用</a-button>
                <a-button
                  v-else
                  type="link"
                  size="small"
                  @click="onToggle(record, 'disable')"
                >禁用</a-button>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </DataTable>
    </a-card>

    <!-- 区域 C：新建/编辑弹窗 -->
    <a-modal
      v-model:open="formModalOpen"
      :title="formMode === 'create' ? '新建 OAuth 提供方' : `编辑 ${providerLabel(formProvider)}`"
      width="560"
      :destroy-on-close="true"
      :confirm-loading="formSubmitting"
      @ok="onSubmitForm"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item label="提供方" name="provider">
          <a-select
            v-model:value="formData.provider"
            :disabled="formMode === 'edit'"
            :options="providerOptions"
            placeholder="请选择提供方"
          />
        </a-form-item>
        <a-form-item label="Client ID" name="clientId">
          <a-input v-model:value="formData.clientId" placeholder="请输入 Client ID" />
        </a-form-item>
        <a-form-item :label="formMode === 'edit' ? 'Client Secret（留空保留原密钥）' : 'Client Secret'" name="clientSecret">
          <a-input-password
            v-model:value="formData.clientSecret"
            autocomplete="new-password"
            :placeholder="formMode === 'edit' ? '留空则保留原密钥' : '请输入 Client Secret'"
          />
        </a-form-item>
        <a-form-item label="Scopes" name="scopes">
          <a-select
            v-model:value="formData.scopes"
            mode="tags"
            placeholder="输入 scope 后回车"
            :token-separators="[',', ' ']"
          />
        </a-form-item>
        <a-form-item label="Authorization Endpoint" name="authorizationEndpoint">
          <a-input v-model:value="formData.authorizationEndpoint" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="Token Endpoint" name="tokenEndpoint">
          <a-input v-model:value="formData.tokenEndpoint" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="UserInfo Endpoint" name="userInfoEndpoint">
          <a-input v-model:value="formData.userInfoEndpoint" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="回调 URL" name="redirectUri">
          <a-input v-model:value="formData.redirectUri" placeholder="/callback/{provider}" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 启停确认 -->
    <ConfirmDialog
      :open="toggleConfirmOpen"
      :title="toggleAction === 'disable' ? '禁用提供方' : '启用提供方'"
      :content="toggleAction === 'disable'
        ? '禁用后用户将无法通过该提供方登录，已绑定的账号不受影响。可随时重新启用。'
        : '启用后用户可通过该提供方登录。'"
      @ok="onConfirmToggle"
      @cancel="toggleConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { oauthClientsApi } from '../api/oauth-clients.api'
import type {
  OAuthClientDto,
  UpdateOAuthClientDto,
  OAuthProvider,
} from '../types/oauth-client.dto'
import {
  SUPPORTED_OAUTH_PROVIDERS,
  OAUTH_PROVIDER_LABELS,
} from '../types/oauth-client.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import DataTable from '@/shared/components/DataTable.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'

const tableData = ref<OAuthClientDto[]>([])
const loading = ref(false)
const statusFilter = ref<'all' | 'enabled' | 'disabled'>('all')

const statusFilterOptions = [
  { label: '全部', value: 'all' },
  { label: '启用', value: 'enabled' },
  { label: '禁用', value: 'disabled' },
]

const filteredData = computed(() => {
  if (statusFilter.value === 'all') return tableData.value
  if (statusFilter.value === 'enabled') return tableData.value.filter((c) => c.enabled)
  return tableData.value.filter((c) => !c.enabled)
})

const providerOptions = SUPPORTED_OAUTH_PROVIDERS.map((p) => ({
  label: OAUTH_PROVIDER_LABELS[p] ?? p,
  value: p,
}))

function providerLabel(provider: string): string {
  return OAUTH_PROVIDER_LABELS[provider] ?? provider
}

const columns: TableColumnsType = [
  { title: '提供方', key: 'provider', width: 120 },
  { title: 'Client ID', dataIndex: 'clientId', key: 'clientId', width: 200, ellipsis: true },
  { title: 'Secret', key: 'clientSecretMasked', width: 140, responsive: ['md'] },
  { title: 'Scopes', dataIndex: 'scopes', key: 'scopes', width: 160, responsive: ['xl'], customRender: ({ text }: { text: string[] }) => (text ?? []).join(', ') || '—' },
  { title: '回调 URL', dataIndex: 'redirectUri', key: 'redirectUri', width: 200, ellipsis: true },
  { title: '状态', key: 'enabled', width: 100 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' },
]

async function fetchList() {
  loading.value = true
  try {
    const { data } = await oauthClientsApi.list()
    tableData.value = data
  } catch {
    message.error('加载 OAuth 客户端列表失败')
  } finally {
    loading.value = false
  }
}

function onFilterChange() {
  // 前端过滤，无需重新请求
}

// 新建/编辑
const formModalOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const formProvider = ref<string>('')
const formRef = ref<FormInstance>()
const formSubmitting = ref(false)

const formData = reactive<{ provider: string } & UpdateOAuthClientDto>({
  provider: '',
  clientId: '',
  clientSecret: '',
  scopes: [],
  authorizationEndpoint: '',
  tokenEndpoint: '',
  userInfoEndpoint: '',
  redirectUri: '',
})

const formRules: Record<string, Rule[]> = {
  provider: [{ required: true, message: '请选择提供方', trigger: 'change' }],
  clientId: [{ required: true, message: '请输入 Client ID', trigger: 'blur' }],
  authorizationEndpoint: [{ required: true, message: '请输入 Authorization Endpoint', trigger: 'blur' }],
  tokenEndpoint: [{ required: true, message: '请输入 Token Endpoint', trigger: 'blur' }],
  userInfoEndpoint: [{ required: true, message: '请输入 UserInfo Endpoint', trigger: 'blur' }],
  redirectUri: [{ required: true, message: '请输入回调 URL', trigger: 'blur' }],
}

function resetForm() {
  formData.provider = ''
  formData.clientId = ''
  formData.clientSecret = ''
  formData.scopes = []
  formData.authorizationEndpoint = ''
  formData.tokenEndpoint = ''
  formData.userInfoEndpoint = ''
  formData.redirectUri = ''
}

function onCreate() {
  formMode.value = 'create'
  formProvider.value = ''
  resetForm()
  formModalOpen.value = true
}

function onEdit(record: OAuthClientDto) {
  formMode.value = 'edit'
  formProvider.value = record.provider
  formData.provider = record.provider
  formData.clientId = record.clientId
  formData.clientSecret = ''
  formData.scopes = [...record.scopes]
  formData.authorizationEndpoint = record.authorizationEndpoint
  formData.tokenEndpoint = record.tokenEndpoint
  formData.userInfoEndpoint = record.userInfoEndpoint
  formData.redirectUri = record.redirectUri
  formModalOpen.value = true
}

async function onSubmitForm() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  // 新建时 clientSecret 必填
  if (formMode.value === 'create' && !formData.clientSecret) {
    message.warning('请输入 Client Secret')
    return
  }
  // 启用前校验必要字段
  const body: UpdateOAuthClientDto = {
    clientId: formData.clientId,
    clientSecret: formData.clientSecret,
    scopes: formData.scopes,
    authorizationEndpoint: formData.authorizationEndpoint,
    tokenEndpoint: formData.tokenEndpoint,
    userInfoEndpoint: formData.userInfoEndpoint,
    redirectUri: formData.redirectUri,
  }
  formSubmitting.value = true
  try {
    if (formMode.value === 'create') {
      await oauthClientsApi.create(formData.provider, body)
      message.success('OAuth 客户端配置已创建（默认禁用，需显式启用）')
    } else {
      await oauthClientsApi.update(formData.provider, body)
      message.success('配置已更新')
    }
    formModalOpen.value = false
    await fetchList()
  } catch {
    message.error(formMode.value === 'create' ? '该提供方可能已存在配置' : '更新失败')
  } finally {
    formSubmitting.value = false
  }
}

// 启停
const toggleConfirmOpen = ref(false)
const toggleAction = ref<'enable' | 'disable'>('enable')
const toggleTarget = ref<OAuthClientDto | null>(null)

function onToggle(record: OAuthClientDto, action: 'enable' | 'disable') {
  if (action === 'enable' && (!record.clientId || record.clientSecretMasked === '')) {
    message.warning('启用前需填写 Client ID 与 Secret')
    return
  }
  toggleTarget.value = record
  toggleAction.value = action
  toggleConfirmOpen.value = true
}

async function onConfirmToggle() {
  toggleConfirmOpen.value = false
  if (!toggleTarget.value) return
  const target = toggleTarget.value
  try {
    if (toggleAction.value === 'enable') {
      await oauthClientsApi.enable(target.provider)
      message.success('已启用')
    } else {
      await oauthClientsApi.disable(target.provider)
      message.success('已禁用')
    }
    await fetchList()
  } catch {
    message.error('状态变更失败')
  } finally {
    toggleTarget.value = null
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped>
.oauth-clients {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.action-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
.provider-name {
  font-weight: 500;
}
.secret-masked {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 启动 dev 服务器人工校验**

Run: `cd web/system-admin && pnpm dev`
访问 `/user-access/oauth-clients`：
- 表格展示所有提供方，Secret 列掩码显示
- 「新建提供方」弹窗 provider 下拉可选，编辑时只读
- 启用/禁用切换弹出 ConfirmDialog
- 状态筛选前端过滤生效
Expected: 交互符合 design-prompt §4 主流程

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/views/OAuthClients.vue
git commit -m "feat(system-admin/02-user-access): 实现 OAuthClients 视图（操作条+表格+CRUD弹窗+启停）"
```

---

## Task 10: Operators.vue 运营人员视图

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/views/Operators.vue`

**对应 design-prompt：** `02-user-access/operators.md`

**布局：** 顶部筛选（搜索/角色/状态）+ 主表格 + 新建弹窗 + 权限分配弹窗（a-transfer）+ 停用确认。

- [ ] **Step 1: 实现 Operators.vue**

```vue
<!-- web/system-admin/src/modules/02-user-access/views/Operators.vue -->
<template>
  <div class="operators">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="搜索">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="用户名/姓名"
            allow-clear
            style="width: 220px"
            @search="onSearch"
          />
        </a-form-item>
        <a-form-item label="角色">
          <a-select
            v-model:value="filters.role"
            style="width: 160px"
            allow-clear
            placeholder="全部角色"
            :options="OPERATOR_ROLE_OPTIONS"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            style="width: 140px"
            allow-clear
            placeholder="全部状态"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
        <a-form-item>
          <PermissionGuard permission="operator:write">
            <a-button type="primary" @click="onCreate">
              <template #icon><PlusOutlined /></template>
              新建运营人员
            </a-button>
          </PermissionGuard>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" class="table-card">
      <DataTable
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="operatorId"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无运营人员" action-text="新建运营人员" @action="onCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'role'">
            <a-tag color="cyan">{{ roleLabel(record.role) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="operator" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'lastLoginAt'">
            {{ record.lastLoginAt ? formatDateTime(record.lastLoginAt) : '—' }}
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onView(record)">查看</a-button>
              <PermissionGuard permission="operator:write">
                <a-button type="link" size="small" @click="onAssignPermissions(record)">权限</a-button>
                <IdempotencyButton
                  v-if="record.status === 'Active'"
                  type="link"
                  size="small"
                  @click="onDeactivate(record)"
                >停用</IdempotencyButton>
                <IdempotencyButton
                  v-else
                  type="link"
                  size="small"
                  @click="onActivate(record)"
                >激活</IdempotencyButton>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </DataTable>
    </a-card>

    <!-- 区域 C：新建弹窗 -->
    <a-modal
      v-model:open="createModalOpen"
      title="新建运营人员"
      :destroy-on-close="true"
      :confirm-loading="creating"
      @ok="onSubmitCreate"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item label="用户名" name="username">
          <a-input v-model:value="formData.username" placeholder="登录用户名" :maxlength="32" />
        </a-form-item>
        <a-form-item label="姓名" name="name">
          <a-input v-model:value="formData.name" placeholder="真实姓名" :maxlength="32" />
        </a-form-item>
        <a-form-item label="邮箱" name="email">
          <a-input v-model:value="formData.email" placeholder="name@example.com" />
        </a-form-item>
        <a-form-item label="初始密码" name="password">
          <a-input-password
            v-model:value="formData.password"
            autocomplete="new-password"
            placeholder="至少 8 位"
          />
        </a-form-item>
        <a-form-item label="角色" name="role">
          <a-select v-model:value="formData.role" :options="OPERATOR_ROLE_OPTIONS" placeholder="请选择角色" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 区域 D：权限分配弹窗 -->
    <a-modal
      v-model:open="permModalOpen"
      title="分配权限"
      width="640"
      :destroy-on-close="true"
      :confirm-loading="permSubmitting"
      @ok="onSubmitPermissions"
    >
      <a-spin :spinning="permLoading">
        <a-transfer
          v-model:target-keys="targetPermissionKeys"
          :data-source="permissionTransferData"
          :titles="['可分配权限', '已分配']"
          :render="(item: { key: string; title: string }) => item.title"
          row-key="key"
          :list-style="{ width: '260px', height: '360px' }"
        />
      </a-spin>
    </a-modal>

    <!-- 停用确认 -->
    <ConfirmDialog
      :open="deactivateConfirmOpen"
      title="停用运营人员"
      content="停用后该运营人员将无法登录，已分配的待办任务需重新分配。可随时激活恢复。"
      @ok="onConfirmDeactivate"
      @cancel="deactivateConfirmOpen = false"
    />

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="运营人员详情"
      placement="right"
      width="560"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <a-descriptions v-if="detail" :column="1" bordered>
          <a-descriptions-item label="运营人员 ID">{{ detail.operatorId }}</a-descriptions-item>
          <a-descriptions-item label="用户名">{{ detail.username }}</a-descriptions-item>
          <a-descriptions-item label="姓名">{{ detail.name }}</a-descriptions-item>
          <a-descriptions-item label="邮箱">{{ detail.email }}</a-descriptions-item>
          <a-descriptions-item label="角色">
            <a-tag color="cyan">{{ roleLabel(detail.role) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <StatusTag type="operator" :status="detail.status" />
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
          <a-descriptions-item label="最近登录">{{ detail.lastLoginAt ? formatDateTime(detail.lastLoginAt) : '从未登录' }}</a-descriptions-item>
          <a-descriptions-item label="权限码">
            <a-tag v-for="p in detail.permissions" :key="p">{{ p }}</a-tag>
          </a-descriptions-item>
        </a-descriptions>
        <a-divider>审计</a-divider>
        <a-button type="link" :disabled="!detail" @click="goToAuditLogs">查看审计记录</a-button>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { operatorsApi } from '../api/operators.api'
import { rolesApi } from '../api/roles.api'
import type {
  OperatorDto,
  OperatorStatus,
  OperatorRole,
  ListOperatorsParams,
  SaveOperatorDto,
} from '../types/operator.dto'
import { OPERATOR_ROLE_OPTIONS } from '../types/operator.dto'
import { useAuthStore } from '@/shared/auth/auth.store'
import { formatDateTime } from '@/shared/utils/format'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import DataTable from '@/shared/components/DataTable.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'

const router = useRouter()
const auth = useAuthStore()

interface FilterState {
  keyword: string
  role?: OperatorRole
  status?: OperatorStatus
}

const filters = reactive<FilterState>({
  keyword: '',
  role: undefined,
  status: undefined,
})

const statusOptions = [
  { label: 'Active', value: 'Active' },
  { label: 'Inactive', value: 'Inactive' },
]

function roleLabel(role: OperatorRole): string {
  return OPERATOR_ROLE_OPTIONS.find((o) => o.value === role)?.label ?? role
}

const columns: TableColumnsType = [
  { title: '运营人员 ID', dataIndex: 'operatorId', key: 'operatorId', width: 140, ellipsis: true },
  { title: '用户名', dataIndex: 'username', key: 'username', width: 140 },
  { title: '姓名', dataIndex: 'name', key: 'name', width: 140 },
  { title: '角色', key: 'role', width: 120 },
  { title: '状态', key: 'status', width: 100 },
  { title: '创建时间', key: 'createdAt', width: 160, responsive: ['xl'] },
  { title: '最近登录', dataIndex: 'lastLoginAt', key: 'lastLoginAt', width: 160 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' },
]

const tableData = ref<OperatorDto[]>([])
const loading = ref(false)
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchList() {
  loading.value = true
  try {
    const params: ListOperatorsParams & { page: number; pageSize: number } = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.role) params.role = filters.role
    if (filters.status) params.status = filters.status
    const { data } = await operatorsApi.list(params)
    // 后端无 keyword 参数，前端二次过滤
    let items = data.items
    if (filters.keyword) {
      const kw = filters.keyword.toLowerCase()
      items = items.filter(
        (o) => o.username.toLowerCase().includes(kw) || o.name.toLowerCase().includes(kw),
      )
    }
    tableData.value = items
    pagination.total = items.length
  } catch {
    message.error('加载运营人员列表失败')
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  fetchList()
}

function onReset() {
  filters.keyword = ''
  filters.role = undefined
  filters.status = undefined
  onQuery()
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearch() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    onQuery()
  }, 300)
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

// 新建
const createModalOpen = ref(false)
const creating = ref(false)
const formRef = ref<FormInstance>()
const formData = reactive<SaveOperatorDto>({
  username: '',
  name: '',
  email: '',
  password: '',
  role: 'Operator',
})

const formRules: Record<string, Rule[]> = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
  password: [
    { required: true, message: '请输入初始密码', trigger: 'blur' },
    { min: 8, message: '至少 8 位', trigger: 'blur' },
  ],
  role: [{ required: true, message: '请选择角色', trigger: 'change' }],
}

function onCreate() {
  formData.username = ''
  formData.name = ''
  formData.email = ''
  formData.password = ''
  formData.role = 'Operator'
  createModalOpen.value = true
}

async function onSubmitCreate() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  creating.value = true
  try {
    await operatorsApi.create({ ...formData })
    message.success('运营人员已创建')
    createModalOpen.value = false
    await fetchList()
  } catch {
    message.error('创建失败：用户名可能已存在')
  } finally {
    creating.value = false
  }
}

// 权限分配
const permModalOpen = ref(false)
const permLoading = ref(false)
const permSubmitting = ref(false)
const targetPermissionKeys = ref<string[]>([])
const permissionTransferData = ref<{ key: string; title: string }[]>([])
const currentOperator = ref<OperatorDto | null>(null)

async function onAssignPermissions(record: OperatorDto) {
  currentOperator.value = record
  permModalOpen.value = true
  permLoading.value = true
  try {
    const { data: catalog } = await rolesApi.getPermissionCatalog()
    const all: { key: string; title: string }[] = []
    for (const group of catalog) {
      for (const p of group.permissions) {
        all.push({ key: p.code, title: p.label ? `${p.label} (${p.code})` : p.code })
      }
    }
    permissionTransferData.value = all
    targetPermissionKeys.value = [...record.permissions]
  } catch {
    message.error('加载权限目录失败')
  } finally {
    permLoading.value = false
  }
}

async function onSubmitPermissions() {
  if (!currentOperator.value) return
  permSubmitting.value = true
  try {
    await operatorsApi.updatePermissions(currentOperator.value.operatorId, {
      permissions: targetPermissionKeys.value,
    })
    message.success('权限已更新')
    permModalOpen.value = false
    await fetchList()
  } catch {
    message.error('权限更新失败')
  } finally {
    permSubmitting.value = false
  }
}

// 激活/停用
const deactivateConfirmOpen = ref(false)
const pendingOperator = ref<OperatorDto | null>(null)

function onDeactivate(record: OperatorDto) {
  // 前端拦截：不能停用自己
  if (auth.user && record.operatorId === auth.user.id) {
    message.warning('不能停用自己的账号')
    return
  }
  pendingOperator.value = record
  deactivateConfirmOpen.value = true
}

async function onConfirmDeactivate() {
  deactivateConfirmOpen.value = false
  if (!pendingOperator.value) return
  try {
    await operatorsApi.deactivate(pendingOperator.value.operatorId)
    message.success('已停用')
    await fetchList()
  } catch {
    message.error('停用失败')
  } finally {
    pendingOperator.value = null
  }
}

async function onActivate(record: OperatorDto) {
  try {
    await operatorsApi.activate(record.operatorId)
    message.success('已激活')
    await fetchList()
  } catch {
    message.error('激活失败')
  }
}

// 详情抽屉
const drawerOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<OperatorDto | null>(null)

async function onView(record: OperatorDto) {
  drawerOpen.value = true
  detailLoading.value = true
  try {
    const { data } = await operatorsApi.get(record.operatorId)
    detail.value = data
  } catch {
    message.error('加载详情失败')
  } finally {
    detailLoading.value = false
  }
}

function goToAuditLogs() {
  if (detail.value) {
    router.push({ path: '/audit/audit-logs', query: { operatorId: detail.value.operatorId } })
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped>
.operators {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
</style>
```

- [ ] **Step 2: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error

- [ ] **Step 3: 启动 dev 服务器人工校验**

Run: `cd web/system-admin && pnpm dev`
访问 `/user-access/operators`：
- 表格展示运营人员列表，角色筛选生效
- 「新建运营人员」弹窗表单校验生效
- 「权限」弹窗穿梭框正确预选已有权限
- 「停用」当前登录账号时前端拦截提示
Expected: 交互符合 design-prompt §4 主流程，不能停用自己分支生效

- [ ] **Step 4: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/views/Operators.vue
git commit -m "feat(system-admin/02-user-access): 实现 Operators 视图（筛选+表格+新建+权限分配+停用拦截）"
```

---

## Task 11: routes.ts + index.ts 聚合导出

**Files:**
- Create: `web/system-admin/src/modules/02-user-access/routes.ts`
- Create: `web/system-admin/src/modules/02-user-access/index.ts`
- Modify: `web/system-admin/src/app/router.ts`（追加 userAccess 子路由数组到 BasicLayout children）

**目标：** 定义 4 条路由项并挂到 BasicLayout 子路由，meta 含 title/menuKey/icon/roles/permission/menuGroup；index.ts 聚合导出 routes + 4 个 api 对象。

- [ ] **Step 1: 创建 routes.ts**

```typescript
// web/system-admin/src/modules/02-user-access/routes.ts

import type { RouteRecordRaw } from 'vue-router'

// 02-user-access 模块路由项（挂到 BasicLayout 子路由）
const userAccessRoutes: RouteRecordRaw[] = [
  {
    path: 'user-access/users',
    name: 'user-access.users',
    component: () => import('../views/UserManagement.vue'),
    meta: {
      title: '用户管理',
      menuKey: 'user-access.users',
      icon: 'UserOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'user:read',
      menuGroup: '02-user-access',
    },
  },
  {
    path: 'user-access/roles',
    name: 'user-access.roles',
    component: () => import('../views/RoleManagement.vue'),
    meta: {
      title: '角色管理',
      menuKey: 'user-access.roles',
      icon: 'SafetyOutlined',
      roles: ['Admin'],
      permission: 'role:read',
      menuGroup: '02-user-access',
    },
  },
  {
    path: 'user-access/oauth-clients',
    name: 'user-access.oauth-clients',
    component: () => import('../views/OAuthClients.vue'),
    meta: {
      title: 'OAuth 客户端',
      menuKey: 'user-access.oauth-clients',
      icon: 'SafetyOutlined',
      roles: ['Admin'],
      permission: 'oauth:read',
      menuGroup: '02-user-access',
    },
  },
  {
    path: 'user-access/operators',
    name: 'user-access.operators',
    component: () => import('../views/Operators.vue'),
    meta: {
      title: '运营人员',
      menuKey: 'user-access.operators',
      icon: 'TeamOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'operator:read',
      menuGroup: '02-user-access',
    },
  },
]

// 默认导出，供 app/router.ts 以 `import userAccess from '@/modules/02-user-access/routes'` 聚合
export default userAccessRoutes
```

- [ ] **Step 2: 创建 index.ts 聚合导出**

```typescript
// web/system-admin/src/modules/02-user-access/index.ts

export { default as routes } from './routes'
export { usersApi } from './api/users.api'
export { rolesApi } from './api/roles.api'
export { oauthClientsApi } from './api/oauth-clients.api'
export { operatorsApi } from './api/operators.api'
export { default as RolePermissionMatrix } from './components/RolePermissionMatrix.vue'
export type {
  UserDto,
  UserStatus,
  ListUsersParams,
  AssignUserRolesDto,
  UpdateUserStatusDto,
} from './types/user.dto'
export type {
  RoleDto,
  ListRolesParams,
  SaveRoleDto,
  UpdateRolePermissionsDto,
  PermissionGroupDto,
  PermissionItemDto,
} from './types/role.dto'
export type {
  OAuthClientDto,
  UpdateOAuthClientDto,
  ListOAuthClientsParams,
  OAuthProvider,
} from './types/oauth-client.dto'
export type {
  OperatorDto,
  OperatorStatus,
  OperatorRole,
  ListOperatorsParams,
  SaveOperatorDto,
  AssignOperatorPermissionsDto,
} from './types/operator.dto'
export { SUPPORTED_OAUTH_PROVIDERS, OAUTH_PROVIDER_LABELS } from './types/oauth-client.dto'
export { OPERATOR_ROLE_OPTIONS } from './types/operator.dto'
```

- [ ] **Step 3: 修改 app/router.ts 聚合 userAccess 路由**

读取 `web/system-admin/src/app/router.ts`，在 import 区追加：

```typescript
import userAccess from '@/modules/02-user-access/routes'
```

在 BasicLayout children 数组中追加 `...userAccess`（若 Plan 1 已存在 `import userAccess` 的临时引用，则替换为上述路径）。最终 BasicLayout children 形如：

```typescript
{
  path: '/',
  component: BasicLayout,
  children: [
    { path: '', redirect: '/dashboard/operations-overview' },
    ...dashboard,
    ...userAccess,
    // 其余 5 个模块（03-system-governance / 04-runtime-ops / 05-audit / 06-account / 07-monitoring）
    // 由对应 Plan 聚合注入，本 plan 不修改其导入
  ],
},
```

- [ ] **Step 4: 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 0 error（4 个视图组件均存在，路由项可解析）

- [ ] **Step 5: 启动 dev 服务器校验菜单与路由**

Run: `cd web/system-admin && pnpm dev`
- 登录后侧边栏「用户与权限」分组下出现 4 个菜单项
- 4 条路由可访问，页面正常渲染
- 未登录访问 `/user-access/users` 跳 `/login?redirect=/user-access/users`
- Operator 角色访问 `/user-access/roles` 跳 `/403`
Expected: 守卫与菜单渲染符合 spec §4.3/§4.5

- [ ] **Step 6: 运行全部模块测试**

Run: `cd web/system-admin && pnpm test -- src/modules/02-user-access/`
Expected: PASS（users.api.spec.ts 4 用例 + roles.api.spec.ts 8 用例 = 12 用例全部通过）

- [ ] **Step 7: 提交**

```bash
git add web/system-admin/src/modules/02-user-access/routes.ts web/system-admin/src/modules/02-user-access/index.ts web/system-admin/src/app/router.ts
git commit -m "feat(system-admin/02-user-access): 聚合 4 条路由项与模块导出并接入全局 router"
```

---

## 验收清单（对应 spec §7.3 与 4 个 design-prompt §8）

- [ ] 02-user-access 4 页全部可访问，CRUD 操作正常
- [ ] UserManagement：搜索 300ms 防抖、状态多选筛选、锁定二次确认 danger、角色穿梭框回填
- [ ] RoleManagement：内置角色删除 disabled + Tooltip、权限树父子联动、保存权限后缓存失效、409 友好提示
- [ ] OAuthClients：Secret 掩码、provider 新建可选编辑只读、启用前字段校验、409 提示
- [ ] Operators：不能停用自己、用户名 409 提示、权限穿梭框预选、状态筛选
- [ ] 所有写操作通过 IdempotencyButton 携带 Idempotency-Key
- [ ] users.api.spec.ts + roles.api.spec.ts 共 12 用例通过
- [ ] `pnpm typecheck` 0 error
- [ ] 路由守卫与权限校验生效（Operator 不能访问 roles/oauth-clients）

## 自检结果（写完后执行）

1. **spec 覆盖**：4 页（UserManagement/RoleManagement/OAuthClients/Operators）✓、模块骨架（4 DTO + 4 API + RolePermissionMatrix + routes + index）✓、users.api.spec.ts + roles.api.spec.ts ✓
2. **占位符扫描**：已扫描 TODO/TBD/FIXME/省略号，0 命中
3. **类型一致性**：UserDto/RoleDto/OAuthClientDto/OperatorDto 字段在 DTO/API/视图三层一致；usersApi/rolesApi/oauthClientsApi/operatorsApi 方法名在 Task 2-5 定义与 Task 7-10 视图调用一致；routes.ts 默认导出 + index.ts `export { default as routes }` + app/router.ts `import userAccess` 默认导入，三者链路贯通
4. **文件路径一致性**：所有 task 引用的路径与文件结构表一致
5. **design-prompt 字段覆盖**：UserManagement（keyword/roles/statuses/fromTime/toTime + Id/Username/Email/Phone/Roles/Status/CreatedAt/LastLoginAt/LastLoginIp）✓；RoleManagement（Id/Name/Description/IsBuiltIn/CreatedAt/CreatedBy/UserCount + 7 端点）✓；OAuthClients（Provider/ClientId/ClientSecretMasked/Scopes/AuthorizationEndpoint/TokenEndpoint/UserInfoEndpoint/RedirectUri/Enabled + 5 端点）✓；Operators（OperatorId/Username/Name/Email/Role/Status/Permissions/CreatedAt/LastLoginAt + 6 端点）✓

