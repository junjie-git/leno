# 系统管理后台 P0 通用功能补齐设计文档

**文档版本**：V1.0
**创建日期**：2026-07-27
**所属项目**：Leno 电商平台
**文档类型**：前端实现设计 spec（补充）
**关联文档**：
- [docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md](./2026-07-27-system-admin-frontend-design.md) — 系统管理后台前端主 spec
- [docs/design-prompts/system-admin/00-overview.md](../../design-prompts/system-admin/00-overview.md) — 28 页 UI 提示词总览
- [docs/design-prompts/shared/design-system.md](../../design-prompts/shared/design-system.md) — 共享设计系统

## 0 摘要

本 spec 在已完成的「系统管理后台前端 SPA」基础上，横向对比业界开源后台（Ant Design Pro / RuoYi / vben-admin 等），补齐 6 项 P0 通用功能：菜单管理、登录日志、在线用户、修改密码、缓存监控、服务器监控。其中仅「修改密码」后端 Identity 域已实装（`PUT /api/users/me/password`、`POST /api/auth/forgot-password`、`POST /api/auth/reset-password`），其余 5 项后端无 API，采用「前端 Mock + 文档化后端需求」策略：前端通过 `axios-mock-adapter` 实现 UI 与交互可演示，同时在本 spec §3.8 明文列出后端需新增的 5 个 Controller / 19 个 Endpoint 契约，供后端开发依据。

**交付物**：仅前端 Vue 3 SPA 改造，页面总数 28 → 34（+6），新增 Mock 基础设施与动态路由能力，修复 HeaderBar 跳转 `/account/profile` 落 404 的已知问题。

**关键决策汇总**：

| 决策项 | 选择 |
|---|---|
| 后端缺失策略 | 前端 Mock + 文档化后端需求 |
| 模块归属 | 全部归现有模块（不新增一级菜单） |
| 菜单管理深度 | 动态菜单 + 动态路由（C 方案） |
| 修改密码页位置 | Profile.vue + Tab 布局 |
| 在线用户能力 | 查看 + 强制下线 |
| 缓存监控能力 | 查看 + 清理 |
| 服务器监控深度 | 折线图轮询 |
| 登录日志深度 | 基础列表 + 筛选 + 详情 |
| 实现方案 | axios-mock-adapter + import.meta.glob（方案 A） |

## 1 总体架构与改造范围

### 1.1 改造前后对比

**改造前**：7 个模块 28 页，静态路由聚合，SiderMenu 从 `router.options.routes` 读取渲染，HeaderBar 跳转 `/account/profile` 落 404。

**改造后**：7 个模块 34 页（+6 页），新增 Mock 基础设施与动态路由能力，菜单管理驱动 SiderMenu 动态渲染，Profile.vue 落地修复 404。

### 1.2 新增 6 页归属

| 模块 | 新增页面 | 路由 path | 数据来源 |
|---|---|---|---|
| 02-user-access | MenuManagement.vue | `/user-access/menus` | Mock |
| 02-user-access | OnlineUsers.vue | `/user-access/online-users` | Mock |
| 05-audit | LoginLogs.vue | `/audit/login-logs` | Mock |
| 04-runtime-ops | CacheMonitor.vue | `/runtime-ops/cache-monitor` | Mock |
| 07-monitoring | ServerMonitor.vue | `/monitoring/server-monitor` | Mock |
| 06-account | Profile.vue（含修改密码 Tab） | `/account/profile` | Identity 真实 API |

页面总数：28 → 34；模块数量不变（仍 7 个）。

### 1.3 Mock 基础设施

**位置**：`shared/http/mock/`

```
shared/http/mock/
├── index.ts                # createMockAdapter(client) + VITE_USE_MOCK 开关
├── handlers/
│   ├── menu.ts             # /admin/menus/* handlers
│   ├── online-users.ts     # /admin/online-users/* handlers
│   ├── login-logs.ts       # /admin/login-logs/* handlers
│   ├── cache.ts            # /admin/cache/* handlers
│   └── server.ts           # /admin/server-monitor/* handlers
└── data/
    └── seed.ts             # 集中种子数据生成器（手写，无外部 faker 依赖）
```

**启用方式**：
- `.env.development` 增加 `VITE_USE_MOCK=true`
- `.env.production` 保持 `VITE_USE_MOCK=false`
- `main.ts` 中：`if (import.meta.env.VITE_USE_MOCK === 'true') setupMockAdapter(client)`

**新增依赖**：`axios-mock-adapter@^2.1.0`（devDependencies，约 12KB gzip）

### 1.4 动态路由基础设施

**位置**：`shared/router/`

```
shared/router/
├── component-map.ts        # import.meta.glob 自动扫描建立 path → lazy import 映射
├── dynamic-routes.ts       # 从 menuStore 构建 RouteRecordRaw[]
└── async-route-guard.ts    # 首次登录后拉取菜单 → addRoute → 跳目标页（集成到 auth-guard）
```

**`component-map.ts` 实现**：

```ts
const modules = import.meta.glob('@/modules/**/views/*.vue')

export const componentMap: Record<string, () => Promise<unknown>> = {}
for (const fullKey in modules) {
  // '/src/modules/02-user-access/views/UserManagement.vue' → '02-user-access/views/UserManagement'
  const key = fullKey
    .replace('/src/modules/', '')
    .replace('.vue', '')
  componentMap[key] = modules[fullKey] as () => Promise<unknown>
}
```

**菜单数据 component 字段约定**：菜单 DTO 的 `component` 字段存储相对路径字符串，如 `'02-user-access/views/UserManagement'`，由 `dynamic-routes.ts` 查 `componentMap` 转换为 `() => import()`。

### 1.5 改造现有静态路由聚合

**改造前**（`app/router.ts`）：静态导入 7 个模块 routes.ts，concat 后挂上守卫。

**改造后**：
- 静态部分仅保留：`/login`、`/403`、`/404`、`/`（BasicLayout 容器，children 初始为空）、catch-all
- BasicLayout children 在登录后通过 `router.addRoute()` 动态注入
- 现有 7 个模块 routes.ts **保留并追加新增页面**，作为「菜单加载失败时的静态回退」（兼容 `VITE_USE_MOCK=false` 且后端菜单 API 未上线场景）
- 启用逻辑：登录成功后判断 `useAuthStore().dynamicMenuEnabled`，true 走动态注入，false 走静态聚合（默认 true）

### 1.6 全局状态新增

`shared/auth/auth.store.ts` 增加：

```ts
state: (): AuthState => ({
  // ... 现有字段 ...
  dynamicMenuEnabled: true,    // 是否启用动态菜单
  menusLoaded: false,          // 菜单是否已加载（与 menuStore.loaded 区别：auth 层标记流程完成）
})
```

新增 `shared/menu/menu.store.ts`：

```ts
import { defineStore } from 'pinia'
import { menuApi } from '@/modules/02-user-access/api/menu.api'
import type { MenuDto, CreateMenuDto, UpdateMenuDto, MenuSortItemDto } from '@/modules/02-user-access/types/menu.dto'

interface MenuState {
  menus: MenuDto[]
  loaded: boolean
}

export const useMenuStore = defineStore('menu', {
  state: (): MenuState => ({
    menus: [],
    loaded: false,
  }),
  actions: {
    async fetchMenus() {
      this.menus = await menuApi.getTree()
      this.loaded = true
    },
    async createMenu(body: CreateMenuDto) {
      const created = await menuApi.create(body)
      await this.fetchMenus()
      return created
    },
    async updateMenu(id: string, body: UpdateMenuDto) {
      await menuApi.update(id, body)
      await this.fetchMenus()
    },
    async deleteMenu(id: string) {
      await menuApi.remove(id)
      await this.fetchMenus()
    },
    async sortMenus(updates: MenuSortItemDto[]) {
      await menuApi.sort(updates)
      await this.fetchMenus()
    },
    reset() {
      this.menus = []
      this.loaded = false
    },
  },
  persist: {
    storage: localStorage,
    pick: ['menus', 'loaded'],
  },
})
```

## 2 模块详细设计

### 2.1 02-user-access 新增 2 页

#### 2.1.1 MenuManagement.vue（菜单管理）

**路由**：`/user-access/menus`，meta `{ title: '菜单管理', roles: ['Admin'], permission: 'menu:write', menuGroup: '02-user-access', icon: 'MenuOutlined' }`

**功能**：
- 顶部工具栏：「新增根菜单」按钮 + 「展开/折叠全部」切换
- 主体：`TreeTableDraggable` 树形展示，每节点含 名称 / 路径 / 图标 / 类型徽标（Directory/Menu/Button）/ 排序号 / 状态（启用/禁用）/ 操作列
- 节点操作：编辑、新增子菜单、删除（带二次确认）、拖拽排序
- 拖拽排序后调 `PUT /admin/menus/sort` 批量更新 `sort` 与 `parentId`
- 右侧抽屉：新增/编辑表单

**表单字段**：

| 字段 | 类型 | 校验 |
|---|---|---|
| parentId | TreeSelect（可选，根菜单为空） | — |
| name | Input | 必填，1-32 字符 |
| type | Radio（Directory/Menu/Button） | 必填 |
| path | Input（Directory/Menu 必填） | 路径格式 `^/[a-z0-9-]+$` |
| component | Input（Menu 必填，从 componentMap 自动补全） | 仅 Menu 类型显示 |
| icon | Input（Ant Design 图标名，可选） | — |
| sort | InputNumber | 必填，≥ 0 |
| permission | Input（可选） | — |
| roles | Checkbox Group（Admin/Operator） | 默认 ['Admin'] |
| visible | Switch | 默认 true |
| cache | Switch（KeepAlive） | 默认 false |

**Mock API**：

| 方法 | 路径 | Mock 行为 |
|---|---|---|
| GET | `/admin/menus/tree` | 返回 seed.ts 中 7 模块 34 页的菜单树 |
| POST | `/admin/menus` | 写入 localStorage.menus，返回新 id |
| PUT | `/admin/menus/{id}` | 更新对应记录 |
| DELETE | `/admin/menus/{id}` | 递归删除子节点 |
| PUT | `/admin/menus/sort` | 批量更新 `{id, parentId, sort}[]` |

**Mock 数据**：seed.ts 初始化时从现有 7 个模块 routes.ts 提取菜单结构（一次性脚本生成，避免手写 34 条），写入 `localStorage.mock_seed_v1.menus`。

#### 2.1.2 OnlineUsers.vue（在线用户）

**路由**：`/user-access/online-users`，meta `{ title: '在线用户', roles: ['Admin'], permission: 'online-user:read', menuGroup: '02-user-access', icon: 'TeamOutlined' }`

**功能**：
- 顶部筛选：用户名搜索 + IP 搜索 + 登录时间范围
- 主体 `a-table`：用户名 / 角色 / IP（地理位置 Mock） / 浏览器 / OS / 登录时间 / 最后活动时间 / 会话时长 / 操作
- 操作：「查看详情」抽屉 + 「强制下线」按钮（带二次确认）
- 强制下线后从列表移除该行，并 `message.success('已下线 xxx')`
- 自动刷新：30s 轮询 + 手动「刷新」按钮
- 顶部统计：在线总数 / 24h 登录总数 / 异常会话数（IP 异常或多设备），使用 `StatisticCard` 组件

**详情抽屉**：会话 Token 前 8 位 / 设备指纹 / 地理位置（基于 IP Mock 解析）/ 历史登录次数 / 当前会话发起请求次数

**Mock API**：

| 方法 | 路径 | Mock 行为 |
|---|---|---|
| GET | `/admin/online-users` | 返回 8-15 条随机在线用户列表，支持分页与筛选 |
| GET | `/admin/online-users/{id}` | 返回会话详情 |
| DELETE | `/admin/online-users/{id}` | 从内存数组移除，返回 `{success: true}` |
| GET | `/admin/online-users/stats` | 返回 `{total, logins24h, anomalies}` |

**Mock 数据**：seed.ts 生成 12 个虚拟用户会话（用户名 admin/operator/test01-10），每次刷新随机变化最后活动时间。

### 2.2 05-audit 新增 1 页

#### 2.2.1 LoginLogs.vue（登录日志）

**路由**：`/audit/login-logs`，meta `{ title: '登录日志', roles: ['Admin', 'Operator'], permission: 'login-log:read', menuGroup: '05-audit', icon: 'LoginOutlined' }`

**功能**：
- 顶部筛选：用户名搜索 + 登录结果（全部/成功/失败）+ 时间范围（默认最近 24h）
- 主体 `a-table`：时间 / 用户名 / IP / 地理位置 / 浏览器 / OS / 结果（StatusTag 绿色成功/红色失败）/ 失败原因 / 耗时(ms) / 详情按钮
- 详情抽屉：完整记录 + User-Agent 解析 / 设备指纹 / 登录前 URL / 服务端 traceId
- 操作：「导出 CSV」按钮（前端生成 Blob 下载）
- 分页：默认每页 20，支持 10/20/50/100 切换

**Mock API**：

| 方法 | 路径 | Mock 行为 |
|---|---|---|
| GET | `/admin/login-logs` | 返回最近 7 天 × 100 条随机记录，支持筛选与分页 |
| GET | `/admin/login-logs/{id}` | 返回单条详情 |
| GET | `/admin/login-logs/export` | 返回 CSV 字符串（前端直接基于列表生成 Blob） |

**Mock 数据**：seed.ts 生成 100 条记录，时间分布按对数衰减（近期多、远期少），结果按 80% 成功 / 20% 失败分布，失败原因从 `['密码错误', '账号锁定', '验证码错误', 'IP 黑名单']` 按权重 `[0.6, 0.15, 0.2, 0.05]` 随机抽取。

### 2.3 04-runtime-ops 新增 1 页

#### 2.3.1 CacheMonitor.vue（缓存监控）

**路由**：`/runtime-ops/cache-monitor`，meta `{ title: '缓存监控', roles: ['Admin'], permission: 'cache:read', menuGroup: '04-runtime-ops', icon: 'DatabaseOutlined' }`

**功能**：
- 顶部 `a-tabs`：「Redis 信息」/「Keyspace」/「Key 浏览」三标签
- **Redis 信息 Tab**：`a-descriptions` 展示 redis_version/redis_mode/os/arch_bits/tcp_port/uptime_in_days/connected_clients/used_memory_human/used_memory_peak_human/maxmemory_human/mem_fragmentation_ratio/total_connections_received/total_commands_processed/keyspace_hits/keyspace_misses/evicted_keys
- **Keyspace Tab**：`a-table` 列出 db0-db15 的 keys/expires/avg_ttl，顶部 3 个 `StatisticCard`「总 keys」「带 TTL keys」「平均 TTL」
- **Key 浏览 Tab**：搜索框（pattern `*user*`）+ db 选择 + 类型筛选（string/hash/list/set/zset）+ 表格列出 key / type / size / ttl / 操作（查看 / 删除）
  - 「查看」打开 `a-modal`，value 用 `JsonViewer` 渲染（非 JSON 用 `<pre>` 文本展示）
  - 「删除」带二次确认，调 `DELETE /admin/cache/keys/{key}`，刷新列表
- 顶部工具栏：「刷新」按钮（重新拉取全部 Tab 数据）+ 「自动刷新」开关（默认关，开启后 30s 轮询 Redis 信息与 Keyspace，不轮询 Key 浏览）

**Mock API**：

| 方法 | 路径 | Mock 行为 |
|---|---|---|
| GET | `/admin/cache/info` | 返回 Redis INFO 字符串解析后的对象 |
| GET | `/admin/cache/keyspaces` | 返回 `[{db:0, keys:1243, expires:120, avg_ttl:3600000}, ...]` |
| GET | `/admin/cache/keys?db=0&pattern=*&type=&page=1&size=20` | 返回分页 key 列表 |
| GET | `/admin/cache/keys/{key}?db=0` | 返回 `{key, type, value, ttl, size}` |
| DELETE | `/admin/cache/keys/{key}?db=0` | 从内存移除，返回 `{success: true}` |

**Mock 数据**：seed.ts 生成 50 个 Redis key，覆盖 user:* / cart:* / order:* / rate_limit:* / feature_flag:* 等前缀，类型分布 string 60% / hash 25% / list 10% / set 5%。

### 2.4 07-monitoring 新增 1 页

#### 2.4.1 ServerMonitor.vue（服务器监控）

**路由**：`/monitoring/server-monitor`，meta `{ title: '服务器监控', roles: ['Admin'], permission: 'server-monitor:read', menuGroup: '07-monitoring', icon: 'DesktopOutlined' }`

**功能**：
- 顶部 6 个 `StatisticCard`：CPU 使用率（%） / 总内存（GB） / 已用内存（GB / %） / 磁盘总量（GB） / 磁盘已用（GB / %） / 系统负载（1/5/15 分钟平均）
- 中部 3 个 `ChartLine`：
  - CPU 使用率（最近 5 分钟，1s 采样）
  - 内存使用（已用 / 缓存 / 空闲，堆叠面积图）
  - 磁盘 I/O（读/写速率 MB/s，双折线）
- 底部 `a-descriptions`：主机名 / OS / 内核版本 / CPU 型号 / CPU 核数 / 总进程数 / 启动时间 / .NET Runtime 版本 / GC 总回收次数
- 自动刷新：5s 轮询拉取最新指标，ChartLine 滚动追加新点（最多 300 点）

**Mock API**：

| 方法 | 路径 | Mock 行为 |
|---|---|---|
| GET | `/admin/server-monitor/snapshot` | 返回当前快照（CPU/内存/磁盘/负载/进程数等） |
| GET | `/admin/server-monitor/history?metric=cpu&range=5m` | 返回时间序列点数组 `[{t, v}, ...]` |

**Mock 数据**：seed.ts 维护内存中的滚动窗口（300 点），每次 snapshot 基于上一个点小幅波动生成新值，符合真实波形。

### 2.5 06-account 新增 1 页

#### 2.5.1 Profile.vue（个人中心）

**路由**：`/account/profile`，meta `{ title: '个人中心', roles: ['Admin', 'Operator'], menuGroup: '06-account', icon: 'UserOutlined' }`

**功能**：`a-tabs` 三标签

**Tab 1 个人信息**：
- `a-form` 展示：用户名（只读）/ 邮箱（可编辑）/ 手机号（可编辑）/ 昵称（可编辑）/ 头像（上传，Mock）/ 备注
- 「保存」按钮调 `PUT /api/users/me`（Identity 真实 API，已实装）
- 保存成功后调 `authStore.fetchProfile()` 刷新本地用户信息

**Tab 2 修改密码**：
- `a-form` 字段：当前密码 / 新密码 / 确认新密码
- 校验：新密码 ≥ 8 位含大小写数字、与当前密码不同、两次输入一致
- `PasswordStrengthIndicator` 实时反馈密码强度
- 「提交」按钮调 `PUT /api/users/me/password`（Identity 真实 API）
- 成功后 `Modal.info` 提示「密码已修改，即将重新登录」，3s 后调 `authStore.logout()`

**Tab 3 安全设置（预留）**：
- 显示「2FA 双因子认证」开关（disabled，提示「2FA 暂未启用，敬请期待」）
- 显示「最近登录记录」`a-list`（5 条，调用登录日志 Mock API `GET /admin/login-logs?username={current}&size=5`）

**新增 authApi 方法**（在 `06-account/api/auth.api.ts`）：

```ts
import { client, withIdempotency } from '@/shared/http'
import type { LoginDto, LoginResultDto, UserProfileResultDto } from '../types/auth.dto'
import type { UpdateProfileDto, ChangePasswordDto } from '../types/auth.dto'

export const authApi = {
  login(body: LoginDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/login', body).then((r) => r.data)
  },
  logout(): Promise<void> {
    return client.post<void>('/auth/logout').then((r) => r.data)
  },
  getProfile(): Promise<UserProfileResultDto> {
    return client.get<UserProfileResultDto>('/users/me').then((r) => r.data)
  },
  updateProfile(body: UpdateProfileDto): Promise<UserProfileResultDto> {
    return withIdempotency(() =>
      client.put<UserProfileResultDto>('/users/me', body),
    ).then((r) => r.data)
  },
  changePassword(body: ChangePasswordDto): Promise<void> {
    return withIdempotency(() =>
      client.put<void>('/users/me/password', body),
    ).then((r) => r.data)
  },
}
```

**新增 DTO**（在 `06-account/types/auth.dto.ts`）：

```ts
export interface UpdateProfileDto {
  email?: string
  phone?: string
  nickname?: string
  avatar?: string
  remark?: string
}

export interface ChangePasswordDto {
  currentPassword: string
  newPassword: string
}
```

### 2.6 SiderMenu 改造

**改造前**：从 `router.options.routes` 读取所有带 `menuGroup` 的子路由，按 7 个模块组渲染。

**改造后**：

```ts
import { computed } from 'vue'
import { useMenuStore } from '@/shared/menu'
import { useAuthStore } from '@/shared/auth'
import { router } from '@/app/router'
import type { MenuDto } from '@/modules/02-user-access/types/menu.dto'

const menuStore = useMenuStore()
const authStore = useAuthStore()

const menuSource = computed<MenuDto[]>(() => {
  // 优先用动态菜单；未加载完成时回退静态路由
  if (menuStore.loaded && menuStore.menus.length) {
    return filterMenusByAuth(menuStore.menus)
  }
  return readMenusFromStaticRoutes()
})

function filterMenusByAuth(menus: MenuDto[]): MenuDto[] {
  return menus
    .filter((m) => m.visible)
    .filter((m) => authStore.hasRole(m.roles))
    .filter((m) => !m.permission || authStore.hasPermission(m.permission))
    .map((m) => ({
      ...m,
      children: m.children ? filterMenusByAuth(m.children) : undefined,
    }))
}

function readMenusFromStaticRoutes(): MenuDto[] {
  // 从 router.options.routes 读取 BasicLayout 下带 meta.menuGroup 的子路由
  // 按一级菜单组（menuGroup）聚合为 Directory，二级路由作为 Menu 子节点
  // 字段映射：route.meta.title → name；route.meta.icon → icon；
  //          route.meta.menuGroup → 派生 Directory；route.path → path；
  //          route.meta.roles → roles；route.meta.permission → permission
  // 该回退分支保证菜单 API 失败时 SiderMenu 仍可渲染（仅缺动态能力）
  const basicRoute = router.options.routes.find((r) => r.name === 'basic')
  const children = (basicRoute?.children ?? []).filter((c) => c.meta?.menuGroup)
  const groupMap = new Map<string, MenuDto>()
  const result: MenuDto[] = []
  for (const child of children) {
    const groupName = child.meta!.menuGroup as string
    if (!groupMap.has(groupName)) {
      const directory: MenuDto = {
        id: `static-${groupName}`,
        parentId: null,
        name: deriveGroupName(groupName),
        type: 'Directory',
        path: '',
        component: null,
        icon: deriveGroupIcon(groupName),
        sort: Number(groupName.split('-')[0]),
        permission: null,
        roles: ['Admin'],
        visible: true,
        cache: false,
        children: [],
      }
      groupMap.set(groupName, directory)
      result.push(directory)
    }
    const directory = groupMap.get(groupName)!
    directory.children!.push({
      id: `static-${child.path}`,
      parentId: directory.id,
      name: child.meta!.title as string,
      type: 'Menu',
      path: `/${child.path}`,
      component: null,
      icon: (child.meta!.icon as string) ?? null,
      sort: directory.children!.length + 1,
      permission: (child.meta!.permission as string) ?? null,
      roles: (child.meta!.roles as string[]) ?? ['Admin'],
      visible: true,
      cache: false,
    })
  }
  return result
}

function deriveGroupName(group: string): string {
  const names: Record<string, string> = {
    '01-dashboard': '仪表盘',
    '02-user-access': '用户与权限',
    '03-system-governance': '系统治理',
    '04-runtime-ops': '运行时运维',
    '05-audit': '审计与对账',
    '06-account': '个人账号',
    '07-monitoring': '系统监控',
  }
  return names[group] ?? group
}

function deriveGroupIcon(group: string): string {
  const icons: Record<string, string> = {
    '01-dashboard': 'DashboardOutlined',
    '02-user-access': 'TeamOutlined',
    '03-system-governance': 'SettingOutlined',
    '04-runtime-ops': 'ToolOutlined',
    '05-audit': 'AuditOutlined',
    '06-account': 'UserOutlined',
    '07-monitoring': 'MonitorOutlined',
  }
  return icons[group] ?? 'AppstoreOutlined'
}
```

**渲染**：仍按 `menuGroup` 分组（7 组），每组用 `<a-sub-menu>` 包裹，菜单项用 `<a-menu-item>`。Directory 类型展开为 sub-menu，Menu 类型作为 menu-item，Button 类型不渲染。

### 2.7 HeaderBar 修复

- `onProfile()` 已实现 `router.push('/account/profile')`，Profile.vue 落地后即修复 404
- 用户菜单新增「修改密码」项，跳转 `/account/profile?tab=password`
- Profile.vue `onMounted` 读取 `route.query.tab`，设置 `activeTab` 初值（`'info' | 'password' | 'security'`）

## 3 数据流与 API 契约

### 3.1 整体数据流

```
用户登录成功
  ├─ authStore.fetchProfile()           // 拉用户信息与权限
  └─ menuStore.fetchMenus()             // 拉菜单树（Mock 阶段从 mock handler 返回）
       └─ dynamic-routes.buildRoutes(menus)   // 把 menus 转成 RouteRecordRaw[]
            └─ router.addRoute('basic', route) // 动态注入到 BasicLayout children
                 └─ router.push(redirectPath)  // 跳目标页
```

### 3.2 Mock 启用判断

**判断逻辑**（在 `shared/http/mock/index.ts`）：

```ts
import type { AxiosInstance } from 'axios'
import MockAdapter from 'axios-mock-adapter'
import { ensureSeedData } from './data/seed'
import { registerMenuHandlers } from './handlers/menu'
import { registerOnlineUserHandlers } from './handlers/online-users'
import { registerLoginLogHandlers } from './handlers/login-logs'
import { registerCacheHandlers } from './handlers/cache'
import { registerServerMonitorHandlers } from './handlers/server'

export function setupMockAdapter(client: AxiosInstance): void {
  ensureSeedData()
  const mock = new MockAdapter(client, { delayResponse: 300 })

  // Mock 重置端点（仅开发联调用）
  mock.onPost('/admin/mock/reset').reply(() => {
    localStorage.removeItem('mock_seed_v1')
    ensureSeedData()
    return [200, { code: 0, message: 'OK', data: { success: true } }]
  })

  registerMenuHandlers(mock)
  registerOnlineUserHandlers(mock)
  registerLoginLogHandlers(mock)
  registerCacheHandlers(mock)
  registerServerMonitorHandlers(mock)

  // 未匹配的请求透传到真实后端
  mock.onAny().passThrough()
}
```

**启用点**（`main.ts`）：

```ts
import { createApp } from 'vue'
import App from './App.vue'
import { router } from './app/router'
import { pinia } from './app/pinia'
import Antd from 'ant-design-vue'
import EChartsVue from 'vue-echarts'
import { client } from '@/shared/http'
import { setupMockAdapter } from '@/shared/http/mock'

const app = createApp(App)
app.use(pinia)
app.use(router)
app.use(Antd)
app.component('ECharts', EChartsVue)

if (import.meta.env.VITE_USE_MOCK === 'true') {
  setupMockAdapter(client)
}

app.mount('#app')
```

**Mock handler 命中规则**：
- 只 mock `/admin/menus`、`/admin/online-users`、`/admin/login-logs`、`/admin/cache`、`/admin/server-monitor` 5 个前缀
- 其他请求（如 `/auth/login`、`/users/me`）透传到真实后端，保证修改密码走真实 API

### 3.3 菜单 DTO 契约

**MenuDto**（`02-user-access/types/menu.dto.ts`）：

```ts
export type MenuType = 'Directory' | 'Menu' | 'Button'

export interface MenuDto {
  id: string
  parentId: string | null       // null 表示根菜单
  name: string                  // 显示名称
  type: MenuType
  path: string                  // Directory/Menu 必填，Button 可空
  component: string | null      // 仅 Menu 类型，如 '02-user-access/views/UserManagement'
  icon: string | null           // Ant Design 图标名
  sort: number                  // 同级排序，从小到大
  permission: string | null     // 如 'menu:write'
  roles: string[]               // ['Admin'] 或 ['Admin', 'Operator']
  visible: boolean              // 是否在 Sider 显示（false 仍可路由访问）
  cache: boolean                // 是否 KeepAlive
  children?: MenuDto[]          // 树形结构
}

export interface MenuTreeResultDto {
  items: MenuDto[]
}

export interface CreateMenuDto extends Omit<MenuDto, 'id' | 'children'> {}

export interface UpdateMenuDto extends Partial<Omit<MenuDto, 'id' | 'children'>> {}

export interface MenuSortItemDto {
  id: string
  parentId: string | null
  sort: number
}
```

### 3.4 在线用户 DTO 契约

**OnlineUserDto**（`02-user-access/types/online-user.dto.ts`）：

```ts
export interface OnlineUserDto {
  id: string                    // 会话 ID
  userId: string
  username: string
  roles: string[]
  ipAddress: string
  geoLocation: string           // 如 '中国·上海'
  browser: string               // 如 'Chrome 120'
  os: string                    // 如 'Windows 11'
  loginAt: string               // ISO 8601
  lastActivityAt: string        // ISO 8601
  sessionDurationMs: number     // 派生字段
  tokenPreview: string          // 前 8 位
  deviceFingerprint: string
  requestCount: number
  isAnomaly: boolean            // IP 异常或多设备标记
}

export interface OnlineUserStatsDto {
  total: number
  logins24h: number
  anomalies: number
}

export interface OnlineUserQueryDto {
  username?: string
  ipAddress?: string
  loginAtFrom?: string
  loginAtTo?: string
  page: number
  pageSize: number
}
```

### 3.5 登录日志 DTO 契约

**LoginLogDto**（`05-audit/types/login-log.dto.ts`）：

```ts
export type LoginResult = 'Success' | 'Failed'

export interface LoginLogDto {
  id: string
  username: string
  ipAddress: string
  geoLocation: string
  browser: string
  os: string
  result: LoginResult
  failureReason: string | null  // '密码错误' | '账号锁定' | '验证码错误' | 'IP 黑名单' | null
  durationMs: number            // 登录处理耗时
  userAgent: string             // 原始 UA
  deviceFingerprint: string
  refererUrl: string | null
  traceId: string
  loginAt: string               // ISO 8601
}

export interface LoginLogQueryDto {
  username?: string
  result?: LoginResult
  loginAtFrom?: string
  loginAtTo?: string
  page: number
  pageSize: number
}
```

### 3.6 缓存监控 DTO 契约

**CacheDto**（`04-runtime-ops/types/cache.dto.ts`）：

```ts
export interface RedisInfoDto {
  redisVersion: string
  redisMode: string
  os: string
  archBits: string
  tcpPort: number
  uptimeInDays: number
  connectedClients: number
  usedMemoryHuman: string
  usedMemoryPeakHuman: string
  maxmemoryHuman: string
  memFragmentationRatio: number
  totalConnectionsReceived: number
  totalCommandsProcessed: number
  keyspaceHits: number
  keyspaceMisses: number
  evictedKeys: number
}

export interface KeyspaceDto {
  db: number                    // 0-15
  keys: number
  expires: number
  avgTtl: number                // 毫秒
}

export type RedisKeyType = 'string' | 'hash' | 'list' | 'set' | 'zset'

export interface RedisKeyDto {
  key: string
  type: RedisKeyType
  size: number                  // string 长度 / hash 字段数 / list 长度等
  ttl: number                   // 秒，-1 表示无过期
}

export interface RedisKeyDetailDto extends RedisKeyDto {
  value: unknown                // 已解析为对应 JS 类型
  db: number
}

export interface CacheKeyQueryDto {
  db: number
  pattern: string               // 默认 '*'
  type?: RedisKeyType
  page: number
  pageSize: number
}
```

### 3.7 服务器监控 DTO 契约

**ServerMonitorDto**（`07-monitoring/types/server-monitor.dto.ts`）：

```ts
export interface ServerSnapshotDto {
  hostname: string
  os: string
  kernelVersion: string
  cpuModel: string
  cpuCores: number
  cpuUsagePercent: number       // 0-100
  memoryTotalBytes: number
  memoryUsedBytes: number
  memoryCachedBytes: number
  diskTotalBytes: number
  diskUsedBytes: number
  diskReadBytesPerSec: number
  diskWriteBytesPerSec: number
  loadAvg1: number
  loadAvg5: number
  loadAvg15: number
  processCount: number
  uptimeSeconds: number
  bootTime: string              // ISO 8601
  dotnetRuntimeVersion: string
  gcTotalCollections: number
  sampledAt: string             // ISO 8601
}

export type MetricName = 'cpu' | 'memory' | 'disk-io'

export interface MetricPointDto {
  t: string                     // ISO 8601
  v: number
}

export interface MetricHistoryDto {
  metric: MetricName
  points: MetricPointDto[]
}
```

### 3.8 后端 API 需求清单（文档化）

本 spec 同时声明后端需新增的 Controller/Endpoint，作为后端开发依据（不在本 spec 实现范围内）：

| 后端 Controller | 路由前缀 | Endpoint 数 | Endpoint 明细 |
|---|---|---|---|
| MenusController | `/api/admin/menus` | 5 | GET /tree、POST、PUT /{id}、DELETE /{id}、PUT /sort |
| OnlineUsersController | `/api/admin/online-users` | 4 | GET、GET /{id}、DELETE /{id}、GET /stats |
| LoginLogsController | `/api/admin/login-logs` | 3 | GET、GET /{id}、GET /export |
| CacheController | `/api/admin/cache` | 5 | GET /info、GET /keyspaces、GET /keys、GET /keys/{key}、DELETE /keys/{key} |
| ServerMonitorController | `/api/admin/server-monitor` | 2 | GET /snapshot、GET /history |

**总计**：5 个 Controller，19 个 Endpoint。每个 Endpoint 的 DTO 即 §3.3-3.7 定义。

**前端 Mock 切换策略**：
- `VITE_USE_MOCK=true`：所有 19 个 endpoint 由 MockAdapter 响应
- `VITE_USE_MOCK=false`：所有 19 个 endpoint 走真实 HTTP，由后端实装
- 切换无需改业务代码，仅改环境变量

### 3.9 错误处理

- Mock handler 统一返回 `ApiResponse<T>` 信封（`{ code: 0, message: 'OK', data: T }`），与真实后端一致
- Mock 模拟业务错误：
  - 删除菜单时若存在子菜单返回 `code: 40001`（BusinessError）
  - 强制下线自己返回 `code: 40003`（ForbiddenError）
  - 删除不存在的缓存 key 返回 `code: 40400`（NotFoundError）
- 真实 API 切换后，错误处理路径完全复用现有 `shared/http/errors.ts`

## 4 路由与守卫

### 4.1 路由表改造

**改造后**：分为「静态路由」与「动态路由」两部分。

```ts
// app/router.ts
import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { createAuthGuard } from '@/shared/auth/auth-guard'

// 静态路由：始终注册，不参与动态菜单
const staticRoutes: RouteRecordRaw[] = [
  {
    path: '/login',
    component: () => import('@/modules/06-account/views/Login2fa.vue'),
    meta: { anonymous: true, title: '登录' },
  },
  {
    path: '/403',
    component: () => import('@/shared/pages/Forbidden.vue'),
    meta: { anonymous: true, title: '无权访问' },
  },
  {
    path: '/404',
    component: () => import('@/shared/pages/NotFound.vue'),
    meta: { anonymous: true, title: '页面不存在' },
  },
  {
    path: '/',
    name: 'basic',
    component: () => import('@/shared/layout/BasicLayout.vue'),
    children: [
      { path: '', redirect: '/dashboard/operations-overview' },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    component: () => import('@/shared/pages/NotFound.vue'),
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes: staticRoutes,
})

router.beforeEach(createAuthGuard(router))

export default router
```

### 4.2 动态路由构建

**`shared/router/dynamic-routes.ts`**：

```ts
import type { RouteRecordRaw } from 'vue-router'
import type { MenuDto } from '@/modules/02-user-access/types/menu.dto'
import { componentMap } from './component-map'

export function buildDynamicRoutes(menus: MenuDto[]): RouteRecordRaw[] {
  const routes: RouteRecordRaw[] = []
  for (const menu of menus) {
    if (menu.type === 'Button') continue           // Button 类型不生成路由
    if (!menu.path) continue
    if (menu.type === 'Menu' && menu.component) {
      const loader = componentMap[menu.component]
      if (!loader) {
        console.warn(`[dynamic-routes] 未找到 component 映射: ${menu.component}`)
        continue
      }
      routes.push({
        path: menu.path.replace(/^\//, ''),        // 相对 BasicLayout
        name: menu.path.replace(/\//g, '.').slice(1),
        component: loader,
        meta: {
          title: menu.name,
          menuKey: menu.path.replace(/\//g, '.').slice(1),
          icon: menu.icon ?? undefined,
          roles: menu.roles,
          permission: menu.permission ?? undefined,
          menuGroup: deriveMenuGroup(menu),
          keepAlive: menu.cache,
        },
      })
    }
    if (menu.type === 'Directory' && menu.children?.length) {
      routes.push(...buildDynamicRoutes(menu.children))
    }
  }
  return routes
}

function deriveMenuGroup(menu: MenuDto): string {
  // 从 path 提取一级前缀，如 '/user-access/users' → '02-user-access'
  const prefix = menu.path.split('/')[1]
  const groupMap: Record<string, string> = {
    dashboard: '01-dashboard',
    'user-access': '02-user-access',
    'system-governance': '03-system-governance',
    'runtime-ops': '04-runtime-ops',
    audit: '05-audit',
    account: '06-account',
    monitoring: '07-monitoring',
  }
  return groupMap[prefix] ?? prefix
}
```

**`shared/router/component-map.ts`**：

```ts
const modules = import.meta.glob('@/modules/**/views/*.vue')

export const componentMap: Record<string, () => Promise<unknown>> = {}
for (const fullKey in modules) {
  // '/src/modules/02-user-access/views/UserManagement.vue' → '02-user-access/views/UserManagement'
  const key = fullKey
    .replace('/src/modules/', '')
    .replace('.vue', '')
  componentMap[key] = modules[fullKey] as () => Promise<unknown>
}
```

### 4.3 守卫改造

**`shared/auth/auth-guard.ts`**：

```ts
import type { NavigationGuardWithThis } from 'vue-router'
import { useAuthStore } from './auth.store'
import { useMenuStore } from '@/shared/menu'
import { buildDynamicRoutes } from '@/shared/router/dynamic-routes'
import type { Router } from 'vue-router'

// 通过参数注入 router，避免循环依赖
export function createAuthGuard(router: Router): NavigationGuardWithThis<undefined> {
  return async (to, from, next) => {
    const auth = useAuthStore()
    const menu = useMenuStore()

    // 1. 已登录访问 /login → 跳首页
    if (to.path === '/login' && auth.isAuthenticated) {
      return next('/')
    }

    // 2. 匿名路由直接放行
    if (to.meta.anonymous) return next()

    // 3. 未登录跳 /login
    if (!auth.isAuthenticated) {
      return next({ path: '/login', query: { redirect: to.fullPath } })
    }

    // 4. user 为空时拉 profile
    if (!auth.user) {
      try {
        await auth.fetchProfile()
      } catch {
        await auth.logout()
        return next({ path: '/login', query: { redirect: to.fullPath } })
      }
    }

    // 5. 角色校验
    if (to.meta.roles && !auth.hasRole(to.meta.roles as string[])) {
      return next('/403')
    }

    // 6. 权限校验
    if (to.meta.permission && !auth.hasPermission(to.meta.permission as string)) {
      return next('/403')
    }

    // 7. 动态菜单首次加载
    if (auth.dynamicMenuEnabled && !menu.loaded) {
      try {
        await menu.fetchMenus()
        const routes = buildDynamicRoutes(menu.menus)
        routes.forEach((r) => router.addRoute('basic', r))
        // 重新匹配目标路由（动态路由刚注入，原 to 可能未命中）
        if (!to.matched.length || to.matched[0].path === '/:pathMatch(.*)*') {
          return next({ ...to, replace: true })
        }
      } catch (e) {
        console.warn('[auth-guard] 菜单加载失败，回退静态路由', e)
        await loadStaticFallbackRoutes(router)
        return next({ ...to, replace: true })
      }
    }

    return next()
  }
}

async function loadStaticFallbackRoutes(router: Router): Promise<void> {
  const dashboard = (await import('@/modules/01-dashboard/routes')).default
  const userAccess = (await import('@/modules/02-user-access/routes')).default
  const systemGovernance = (await import('@/modules/03-system-governance/routes')).default
  const runtimeOps = (await import('@/modules/04-runtime-ops/routes')).default
  const audit = (await import('@/modules/05-audit/routes')).default
  const account = (await import('@/modules/06-account/routes')).default
  const monitoring = (await import('@/modules/07-monitoring/routes')).default
  ;[
    ...dashboard,
    ...userAccess,
    ...systemGovernance,
    ...runtimeOps,
    ...audit,
    ...account,
    ...monitoring,
  ].forEach((r) => router.addRoute('basic', r))
}
```

`app/router.ts` 调整守卫注册为 `router.beforeEach(createAuthGuard(router))`。

### 4.4 7 个模块 routes.ts 追加新增页面

每个模块的 `routes.ts` 保持原有结构，仅追加新增页面项，作为静态回退使用。

**02-user-access/routes.ts 追加**：

```ts
{
  path: '/user-access/menus',
  name: 'user-access.menus',
  component: () => import('./views/MenuManagement.vue'),
  meta: {
    title: '菜单管理',
    menuKey: 'user-access.menus',
    icon: 'MenuOutlined',
    roles: ['Admin'],
    permission: 'menu:write',
    menuGroup: '02-user-access',
  },
},
{
  path: '/user-access/online-users',
  name: 'user-access.online-users',
  component: () => import('./views/OnlineUsers.vue'),
  meta: {
    title: '在线用户',
    menuKey: 'user-access.online-users',
    icon: 'TeamOutlined',
    roles: ['Admin'],
    permission: 'online-user:read',
    menuGroup: '02-user-access',
  },
},
```

**05-audit/routes.ts 追加**：

```ts
{
  path: '/audit/login-logs',
  name: 'audit.login-logs',
  component: () => import('./views/LoginLogs.vue'),
  meta: {
    title: '登录日志',
    menuKey: 'audit.login-logs',
    icon: 'LoginOutlined',
    roles: ['Admin', 'Operator'],
    permission: 'login-log:read',
    menuGroup: '05-audit',
  },
},
```

**04-runtime-ops/routes.ts 追加**：

```ts
{
  path: '/runtime-ops/cache-monitor',
  name: 'runtime-ops.cache-monitor',
  component: () => import('./views/CacheMonitor.vue'),
  meta: {
    title: '缓存监控',
    menuKey: 'runtime-ops.cache-monitor',
    icon: 'DatabaseOutlined',
    roles: ['Admin'],
    permission: 'cache:read',
    menuGroup: '04-runtime-ops',
  },
},
```

**07-monitoring/routes.ts 追加**：

```ts
{
  path: '/monitoring/server-monitor',
  name: 'monitoring.server-monitor',
  component: () => import('./views/ServerMonitor.vue'),
  meta: {
    title: '服务器监控',
    menuKey: 'monitoring.server-monitor',
    icon: 'DesktopOutlined',
    roles: ['Admin'],
    permission: 'server-monitor:read',
    menuGroup: '07-monitoring',
  },
},
```

**06-account/routes.ts 改造**（替换原空数组）：

```ts
export const accountRoutes: RouteRecordRaw[] = [
  {
    path: '/account/profile',
    name: 'account.profile',
    component: () => import('./views/Profile.vue'),
    meta: {
      title: '个人中心',
      menuKey: 'account.profile',
      icon: 'UserOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '06-account',
    },
  },
]
```

### 4.5 SiderMenu 改造

`shared/layout/SiderMenu.vue` 改造逻辑见 §2.6，渲染仍按 `menuGroup` 分组（7 组），每组用 `<a-sub-menu>` 包裹。Directory 类型展开为 sub-menu，Menu 类型作为 menu-item，Button 类型不渲染。Sider 折叠后仅显示图标。

### 4.6 路由跳转保护

- 动态路由注入后，`router.hasRoute(name)` 判断目标路由是否已注册
- 未注册时跳 `/404`
- `router.addRoute()` 后必须 `next({ ...to, replace: true })` 重新触发导航（Vue Router 4 要求）

### 4.7 KeepAlive 配置

- BasicLayout 的 `<RouterView>` 包裹 `<KeepAlive :include="cachedRouteNames">`
- `cachedRouteNames` 从 `menuStore.menus` 中筛选 `cache: true` 的菜单 `name` 派生
- 默认仅「仪表盘」「列表页」开启 cache，详情页不缓存

## 5 视觉规范与组件复用

### 5.1 视觉规范沿用

完全遵循 `docs/design-prompts/shared/design-system.md` 与现有 frontend-design spec §5：

- 主色 `#1677FF`，圆角 `6px`，字体栈 PingFang SC 优先
- 表格密度 `size="middle"`，减少视觉噪声
- 危险操作（强制下线 / 删除菜单 / 删除缓存 key）统一走 `ConfirmDialog` 二次确认
- 所有写操作通过 `IdempotencyButton` 携带 `Idempotency-Key` 头（修改密码、强制下线、删除菜单、清理缓存）

### 5.2 新增页面布局规范

6 个新页面统一采用「顶部工具栏 + 主体内容」结构，与现有 28 页保持视觉一致：

```
┌─ PageContainer (padding 24px) ─────────────────────────┐
│  ┌─ PageHeader ─────────────────────────────────────┐  │
│  │  标题  |  统计卡片(可选)  |  操作按钮组             │  │
│  └──────────────────────────────────────────────────┘  │
│  ┌─ FilterBar (可选) ───────────────────────────────┐  │
│  │  搜索框  |  状态筛选  |  时间范围  |  重置          │  │
│  └──────────────────────────────────────────────────┘  │
│  ┌─ MainContent ────────────────────────────────────┐  │
│  │  Table / Tree / Cards / ChartLine                │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

### 5.3 共享组件复用

| 新页面 | 复用组件 | 用途 |
|---|---|---|
| MenuManagement | ConfirmDialog、IdempotencyButton、EmptyState、TreeTableDraggable | 树形菜单展示、二次确认、新增/编辑表单提交 |
| OnlineUsers | DataTable、StatusTag、ConfirmDialog、IdempotencyButton、DateTimeRangePicker、StatisticCard | 列表展示、异常会话标记、强制下线确认、登录时间筛选、顶部统计 |
| LoginLogs | DataTable、StatusTag、DateTimeRangePicker、JsonViewer | 列表展示、登录结果徽标、时间筛选、详情 UA 解析 |
| CacheMonitor | JsonViewer、ConfirmDialog、IdempotencyButton、EmptyState、StatisticCard | Key 值渲染、删除确认、刷新按钮、空 keyspace 占位、顶部统计 |
| ServerMonitor | ChartLine、StatisticCard、EmptyState | 折线图轮询、指标卡片、首屏加载占位 |
| Profile | IdempotencyButton、ErrorBoundary、PasswordStrengthIndicator | 表单提交、Tab 切换错误隔离、密码强度反馈 |

### 5.4 新增共享组件

#### 5.4.1 `StatisticCard.vue`

**位置**：`shared/components/StatisticCard.vue`

**职责**：统一的指标卡片，支持标题、数值、单位、趋势箭头、颜色映射。

**Props**：

```ts
interface StatisticCardProps {
  title: string
  value: number | string
  unit?: string                  // '%', 'GB', 'ms'
  precision?: number             // 小数位
  trend?: 'up' | 'down' | 'flat' // 趋势箭头
  trendValue?: number            // 较上次的差值
  status?: 'success' | 'warning' | 'danger' | 'default'  // 颜色映射
  loading?: boolean
  suffix?: string                // 自定义后缀
}
```

**模板结构**：`a-card` + `a-statistic` + 趋势角标。颜色映射：success 绿、warning 橙、danger 红、default 蓝。

**使用场景**：ServerMonitor（6 卡片）、OnlineUsers（3 卡片）、CacheMonitor（3 卡片）。

#### 5.4.2 `PasswordStrengthIndicator.vue`

**位置**：`shared/components/PasswordStrengthIndicator.vue`

**职责**：密码强度指示器，实时反馈新密码强度。

**Props**：

```ts
interface PasswordStrengthIndicatorProps {
  password: string
}
```

**算法**（与 Identity 后端校验对齐）：
- 长度 < 8：弱
- 长度 ≥ 8 且仅一种字符类别（数字 / 小写 / 大写 / 符号）：弱
- 长度 ≥ 8 且含两种字符类别：中
- 长度 ≥ 12 且含三种及以上字符类别：强

**渲染**：3 段进度条 + 文字标签「弱 / 中 / 强」，颜色红 / 橙 / 绿。

**使用场景**：Profile.vue 修改密码 Tab。

#### 5.4.3 `TreeTableDraggable.vue`

**位置**：`shared/components/TreeTableDraggable.vue`

**职责**：可拖拽排序的树形表格，封装 `a-table` 的 `draggable` 与树形数据展开。

**Props**：

```ts
interface TreeTableDraggableProps<T> {
  data: T[]
  columns: TableColumn<T>[]
  rowKey: (record: T) => string
  parentKey: (record: T) => string | null
  draggable?: boolean             // 默认 true
  expandedKeys?: string[]
}
```

**Events**：
- `drop({ dragKey, dropKey, position: 'before' | 'after' | 'inside' })` —— 拖拽完成
- `expand(keys: string[])` —— 展开状态变更

**使用场景**：MenuManagement 菜单排序。

### 5.5 状态色映射补充

`shared/components/StatusTag.vue` 增加新 type：

| type | 状态 | 颜色 |
|---|---|---|
| loginResult | Success | green |
| loginResult | Failed | red |
| cacheType | string | blue |
| cacheType | hash | purple |
| cacheType | list | cyan |
| cacheType | set | gold |
| cacheType | zset | magenta |
| menuType | Directory | blue |
| menuType | Menu | green |
| menuType | Button | default |
| onlineUser | Normal | green |
| onlineUser | Anomaly | red |

### 5.6 图标使用

新增页面使用的 Ant Design 图标统一从 `@ant-design/icons-vue` 导入：
- MenuManagement: `MenuOutlined`
- OnlineUsers: `TeamOutlined`
- LoginLogs: `LoginOutlined`
- CacheMonitor: `DatabaseOutlined`
- ServerMonitor: `DesktopOutlined`
- Profile: `UserOutlined`、`LockOutlined`（修改密码 Tab）、`SafetyOutlined`（安全设置 Tab）

### 5.7 空状态与加载态

- 所有列表页首次加载用 `a-spin` 包裹，配合 `EmptyState` 组件
- 列表数据为空时统一显示「暂无数据」+ 操作引导（如「新增菜单」「无在线用户」）
- ChartLine 加载中显示骨架屏（`a-skeleton` active）

### 5.8 响应式与断点

沿用现有规范：
- ≥ 1200px：Sider 全展开 + 主体最大宽度利用
- 992-1199px：Sider 自动折叠
- < 992px：显示「请使用桌面端访问」提示

新增页面统计卡片栅格：
- 6 卡片（ServerMonitor）：`a-row :gutter="16"` + `a-col :xs="24" :sm="12" :md="8" :lg="4"`
- 3 卡片（OnlineUsers、CacheMonitor）：`a-col :xs="24" :sm="8"`

## 6 Mock 数据与种子生成

### 6.1 种子数据生成策略

**位置**：`shared/http/mock/data/seed.ts`

**职责**：集中生成 5 类 Mock 数据的初始状态，写入 `localStorage.mock_seed_v1`，刷新页面后保持数据连续性（避免每次刷新重置）。

**初始化流程**：

```ts
const SEED_KEY = 'mock_seed_v1'

export function ensureSeedData(): void {
  if (localStorage.getItem(SEED_KEY)) return

  const seed = {
    menus: buildMenuSeed(),              // 34 条菜单
    onlineUsers: buildOnlineUserSeed(),  // 12 条会话
    loginLogs: buildLoginLogSeed(),      // 100 条日志
    redisKeys: buildRedisKeySeed(),      // 50 个 key
    redisInfo: buildRedisInfoSeed(),     // 1 份 Redis INFO
    keyspaces: buildKeyspaceSeed(),      // 16 个 db 状态
    serverSnapshot: buildServerSnapshotSeed(),  // 1 份快照
    serverHistory: { cpu: [], memory: [], diskIo: [] },
    nextId: 1000,                         // 自增 ID 起点
  }
  localStorage.setItem(SEED_KEY, JSON.stringify(seed))
}

export function loadSeedData(): MockSeed {
  ensureSeedData()
  return JSON.parse(localStorage.getItem(SEED_KEY)!)
}

export function saveSeedData(seed: MockSeed): void {
  localStorage.setItem(SEED_KEY, JSON.stringify(seed))
}

export function nextId(seed: MockSeed, prefix: string): string {
  seed.nextId += 1
  return `${prefix}-${seed.nextId}`
}

interface MockSeed {
  menus: MenuDto[]
  onlineUsers: OnlineUserDto[]
  loginLogs: LoginLogDto[]
  redisKeys: RedisKeyDetailDto[]
  redisInfo: RedisInfoDto
  keyspaces: KeyspaceDto[]
  serverSnapshot: ServerSnapshotDto
  serverHistory: { cpu: MetricPointDto[]; memory: MetricPointDto[]; diskIo: MetricPointDto[] }
  nextId: number
}
```

**重置入口**：Mock handler 暴露 `POST /admin/mock/reset` 端点（仅 Mock 模式可用），清空 `localStorage.mock_seed_v1` 后重新初始化。供开发联调时使用。

### 6.2 菜单种子数据

**生成方式**：从现有 7 个模块 routes.ts 的 `meta` 字段提取，结构化为 MenuDto[]。

**关键约定**：
- 7 个一级目录（Directory 类型）：仪表盘 / 用户与权限 / 系统治理 / 运行时运维 / 审计与对账 / 个人账号 / 系统监控
- 34 个二级菜单（Menu 类型）：原 28 页 + 新增 6 页
- 不含 Button 类型（暂不生成按钮权限节点，保持种子简洁）

**示例结构**（节选 02-user-access 目录）：

```ts
{
  id: 'm-01',
  parentId: null,
  name: '用户与权限',
  type: 'Directory',
  path: '/user-access',
  component: null,
  icon: 'TeamOutlined',
  sort: 2,
  permission: null,
  roles: ['Admin'],
  visible: true,
  cache: false,
  children: [
    { id: 'm-02-01', parentId: 'm-01', name: '用户管理', type: 'Menu', path: '/user-access/users', component: '02-user-access/views/UserManagement', icon: 'UserOutlined', sort: 1, permission: 'user:read', roles: ['Admin'], visible: true, cache: true },
    { id: 'm-02-02', parentId: 'm-01', name: '角色管理', type: 'Menu', path: '/user-access/roles', component: '02-user-access/views/RoleManagement', icon: 'SafetyOutlined', sort: 2, permission: 'role:read', roles: ['Admin'], visible: true, cache: true },
    { id: 'm-02-03', parentId: 'm-01', name: 'OAuth 客户端', type: 'Menu', path: '/user-access/oauth-clients', component: '02-user-access/views/OAuthClients', icon: 'KeyOutlined', sort: 3, permission: 'oauth-client:read', roles: ['Admin'], visible: true, cache: true },
    { id: 'm-02-04', parentId: 'm-01', name: '运营人员', type: 'Menu', path: '/user-access/operators', component: '02-user-access/views/Operators', icon: 'SolutionOutlined', sort: 4, permission: 'operator:read', roles: ['Admin'], visible: true, cache: true },
    { id: 'm-02-05', parentId: 'm-01', name: '菜单管理', type: 'Menu', path: '/user-access/menus', component: '02-user-access/views/MenuManagement', icon: 'MenuOutlined', sort: 5, permission: 'menu:write', roles: ['Admin'], visible: true, cache: false },
    { id: 'm-02-06', parentId: 'm-01', name: '在线用户', type: 'Menu', path: '/user-access/online-users', component: '02-user-access/views/OnlineUsers', icon: 'TeamOutlined', sort: 6, permission: 'online-user:read', roles: ['Admin'], visible: true, cache: false },
  ],
},
```

其余 6 个目录同理生成，完整数据在 seed.ts 中硬编码。

### 6.3 在线用户种子数据

**生成规则**：
- 12 条记录，覆盖 admin (1) / operator (1) / test01-test10 (10)
- IP 地址池：`['192.168.1.100', '192.168.1.101', '10.0.0.50', '172.16.0.20', '114.114.114.114', '8.8.8.8']`
- 地理位置映射：内网 IP → `'内网·本地'`，公网 IP → `'中国·上海'` / `'美国·加州'`
- 浏览器池：`['Chrome 120', 'Firefox 121', 'Safari 17', 'Edge 120']`
- OS 池：`['Windows 11', 'macOS 14', 'Ubuntu 22.04', 'CentOS 7']`
- 登录时间：最近 1-8 小时内随机
- 最后活动时间：最近 1-5 分钟内随机
- 异常标记：2 条记录 `isAnomaly = true`（test03 多设备登录、test07 异地登录）

**派生字段计算**：`sessionDurationMs = Date.now() - new Date(loginAt).getTime()`，每次 GET 请求实时计算。

### 6.4 登录日志种子数据

**生成规则**：
- 100 条记录，时间跨度最近 7 天
- 时间分布：对数衰减，近 24h 占 40%、24-72h 占 35%、72-168h 占 25%
- 结果分布：80% 成功 / 20% 失败
- 失败原因池：`['密码错误', '账号锁定', '验证码错误', 'IP 黑名单']`，权重 `[0.6, 0.15, 0.2, 0.05]`
- 用户名池：`['admin', 'operator', 'test01', 'test02', 'test03', 'unknown']`（unknown 表示不存在用户尝试）
- IP / 浏览器 / OS 池同 6.3
- durationMs：成功 80-300ms，失败 50-150ms
- traceId：`uuidv4()` 截取前 16 位（用 `crypto.randomUUID()` 实现，无需新增依赖）

**导出 CSV 实现**：handler 内基于内存数组生成 CSV 字符串，前端 `Blob` 下载，不依赖后端。CSV 列：`id,loginAt,username,ipAddress,geoLocation,browser,os,result,failureReason,durationMs,traceId`。

### 6.5 缓存监控种子数据

**RedisInfoDto 种子**：

```ts
const redisInfoSeed: RedisInfoDto = {
  redisVersion: '7.2.3',
  redisMode: 'standalone',
  os: 'Linux 6.5.0-14-generic x86_64',
  archBits: '64',
  tcpPort: 6379,
  uptimeInDays: 45,
  connectedClients: 24,
  usedMemoryHuman: '512.45M',
  usedMemoryPeakHuman: '780.12M',
  maxmemoryHuman: '2.00G',
  memFragmentationRatio: 1.12,
  totalConnectionsReceived: 152340,
  totalCommandsProcessed: 1859234,
  keyspaceHits: 1245890,
  keyspaceMisses: 45320,
  evictedKeys: 12,
}
```

**Keyspace 种子**：db0 有数据（1243 keys / 120 expires / avg_ttl 3600000ms），db1 有数据（87 keys / 50 expires / avg_ttl 7200000ms），db2 有数据（12 keys / 0 expires / avg_ttl 0），其余 db 为空（keys: 0）。

**Redis Key 种子（50 个）**：
- 前缀分布：`user:*` (15) / `cart:*` (10) / `order:*` (8) / `rate_limit:*` (7) / `feature_flag:*` (5) / `lock:*` (5)
- 类型分布：string 30 / hash 13 / list 5 / set 2
- TTL 分布：-1 永久（20）、3600s（15）、86400s（10）、60s（5）
- value 生成：
  - string：JSON 字符串或纯文本（如用户 profile JSON）
  - hash：`{ field1: value1, ... }` 5-10 字段
  - list：字符串数组 5-20 项
  - set：字符串集合 3-10 项

### 6.6 服务器监控种子数据

**ServerSnapshotDto 种子**：

```ts
const serverSnapshotSeed: ServerSnapshotDto = {
  hostname: 'leno-prod-systemadmin-01',
  os: 'Linux 6.5.0-14-generic',
  kernelVersion: '6.5.0-14-generic',
  cpuModel: 'Intel Xeon E5-2680 v4 @ 2.40GHz',
  cpuCores: 8,
  cpuUsagePercent: 32.5,
  memoryTotalBytes: 17179869184,      // 16 GB
  memoryUsedBytes: 8589934592,        // 8 GB
  memoryCachedBytes: 2147483648,      // 2 GB
  diskTotalBytes: 107374182400,       // 100 GB
  diskUsedBytes: 53687091200,         // 50 GB
  diskReadBytesPerSec: 1048576,       // 1 MB/s
  diskWriteBytesPerSec: 2097152,      // 2 MB/s
  loadAvg1: 1.25,
  loadAvg5: 1.10,
  loadAvg15: 0.95,
  processCount: 184,
  uptimeSeconds: 3888000,             // 45 天
  bootTime: '2026-06-12T08:00:00Z',
  dotnetRuntimeVersion: '8.0.11',
  gcTotalCollections: 12450,
  sampledAt: new Date().toISOString(),
}
```

**历史数据生成**：
- 初始化时生成 300 个 CPU 点（5 分钟 × 1s 间隔），基于正弦波 + 随机噪声模拟
- 内存与磁盘 I/O 同理生成 300 点
- 每次 GET /snapshot 时追加新点，移除最旧点（保持 300 点滚动窗口）

**波动算法**：

```ts
function nextCpuValue(prev: number): number {
  const base = prev + (Math.random() - 0.5) * 10
  const sine = Math.sin(Date.now() / 60000) * 5    // 1 分钟周期波动
  return Math.max(5, Math.min(95, base + sine))
}
```

### 6.7 Mock handler 注册顺序

见 §3.2 `setupMockAdapter` 实现。注册顺序：先注册 mock 重置端点，再注册 5 类业务 handler，最后 `mock.onAny().passThrough()` 透传未匹配请求。

### 6.8 Mock 数据持久化与并发

**持久化**：所有写操作（POST/PUT/DELETE）直接修改 `localStorage.mock_seed_v1` 中的对应数组，保证刷新后状态连续。

**并发保护**：MockAdapter 是同步执行的，无并发问题。但 `localStorage` 读写需注意：
- 读：每次请求开始时 `JSON.parse(localStorage.getItem(KEY))`
- 写：修改后 `localStorage.setItem(KEY, JSON.stringify(seed))`
- 单次请求内读改写闭环，无中间态泄漏

**性能**：100 条登录日志 + 50 个 Redis key + 12 个在线用户，单次 JSON 序列化 < 5ms，可接受。

### 6.9 Mock handler 单测

每个 handler 文件配套 `.spec.ts`：
- `menu.spec.ts`：测试 CRUD / 排序 / 子节点递归删除
- `online-users.spec.ts`：测试列表筛选 / 强制下线 / 统计
- `login-logs.spec.ts`：测试筛选 / 分页 / 导出 CSV
- `cache.spec.ts`：测试 info / keyspaces / key 浏览 / 删除
- `server-monitor.spec.ts`：测试 snapshot / history 滚动窗口

测试方式：直接调用 handler 函数（不通过 axios），断言返回 `ApiResponse<T>` 结构。

## 7 测试、构建与可观测

### 7.1 测试分层

| 层级 | 工具 | 范围 | 目标覆盖 |
|---|---|---|---|
| 单元测试 | Vitest + @vue/test-utils | 共享组件、Mock handler、stores、utils | 新增共享组件 100%、Mock handler 关键路径 100% |
| 组件测试 | Vitest + jsdom | 6 个新页面组件 | 关键交互（提交、删除、拖拽、轮询）覆盖 |
| E2E（可选） | Playwright | 登录 → 菜单加载 → 6 页面访问 | 冒烟通过，不阻塞交付 |

### 7.2 单元测试清单

**新增共享组件测试**：

| 测试文件 | 被测对象 | 关键用例 |
|---|---|---|
| `StatisticCard.spec.ts` | StatisticCard.vue | 渲染数值与单位；trend 箭头方向；status 颜色映射；loading 显示骨架 |
| `PasswordStrengthIndicator.spec.ts` | PasswordStrengthIndicator.vue | 弱密码（< 8 位）；中密码（2 类字符）；强密码（≥ 12 位 + 3 类）；空值不渲染 |
| `TreeTableDraggable.spec.ts` | TreeTableDraggable.vue | 树形展开；拖拽 drop 事件 payload；expandedKeys 双向绑定 |

**Mock handler 测试**（直接调用 handler 函数，不走 axios）：

| 测试文件 | 关键用例 |
|---|---|
| `menu.spec.ts` | GET /tree 返回 7 目录 34 菜单；POST 新增根菜单；PUT 更新菜单；DELETE 递归删除子节点；PUT /sort 批量更新；删除有子菜单的目录返回 code 40001 |
| `online-users.spec.ts` | GET 列表带筛选；GET /{id} 详情；DELETE 强制下线；DELETE 自己返回 code 40003；GET /stats 返回 3 个统计值 |
| `login-logs.spec.ts` | GET 列表分页；按 result 筛选；按时间范围筛选；GET /{id} 详情；GET /export 返回 CSV 字符串 |
| `cache.spec.ts` | GET /info；GET /keyspaces；GET /keys 分页与 pattern；GET /keys/{key} 详情；DELETE /keys/{key}；删除不存在 key 返回 code 40400 |
| `server-monitor.spec.ts` | GET /snapshot 字段完整；GET /history?metric=cpu 返回 300 点；连续 snapshot 后历史窗口保持 300 点 |

**Stores 测试**：

| 测试文件 | 关键用例 |
|---|---|
| `menu.store.spec.ts` | fetchMenus 填充 state；createMenu 后 state.menus 更新；deleteMenu 后子节点同步移除；persist 字段正确 |
| `auth.store.spec.ts`（补充） | dynamicMenuEnabled 默认 true；menusLoaded 状态流转 |

### 7.3 组件测试清单

每个新页面配套 `.spec.ts`，使用 `mount` + `mock` axios：

| 测试文件 | 关键用例 |
|---|---|
| `MenuManagement.spec.ts` | 首次加载调 GET /tree；新增菜单表单校验；拖拽排序触发 PUT /sort；删除二次确认 |
| `OnlineUsers.spec.ts` | 列表加载；强制下线确认后调 DELETE；统计卡片渲染；30s 轮询启动与销毁 |
| `LoginLogs.spec.ts` | 列表加载；result 筛选；时间范围筛选；导出 CSV 触发 Blob 下载 |
| `CacheMonitor.spec.ts` | Tab 切换加载对应数据；Key 浏览搜索；删除 Key 确认；自动刷新开关 |
| `ServerMonitor.spec.ts` | 6 统计卡片渲染；3 ChartLine 初始化；5s 轮询追加新点；卸载清理定时器 |
| `Profile.spec.ts` | Tab 切换；个人信息保存调 PUT /users/me；修改密码校验；密码强度指示器联动；修改成功后 logout |

### 7.4 测试基础设施

**Mock axios 方式**：

```ts
// tests/setup.ts
import { vi } from 'vitest'
import { config } from '@vue/test-utils'
import { createPinia } from 'pinia'

vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
  withIdempotency: (fn: () => Promise<unknown>) => fn,
}))

config.global.plugins = [createPinia()]
```

**localStorage polyfill**：jsdom 已内置，无需额外配置。

**定时器 mock**：

```ts
// 轮询测试用 vi.useFakeTimers() + vi.advanceTimersByTime(30000)
beforeEach(() => vi.useFakeTimers())
afterEach(() => vi.useRealTimers())
```

### 7.5 构建配置

**`vite.config.ts` 调整**：

```ts
export default defineConfig({
  // ... 现有配置 ...
  define: {
    __USE_MOCK__: JSON.stringify(process.env.VITE_USE_MOCK === 'true'),
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          mock: ['./src/shared/http/mock'],
          monitoring: ['./src/modules/07-monitoring', './src/modules/04-runtime-ops/views/CacheMonitor.vue'],
        },
      },
    },
  },
})
```

**Mock 代码生产排除**：
- `main.ts` 中 `if (import.meta.env.VITE_USE_MOCK === 'true')` 已是条件分支
- Vite production build 默认会 tree-shake 未使用代码
- 进一步保险：`shared/http/mock/index.ts` 顶部加生产环境保护：

```ts
if (!import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'true') {
  throw new Error('Mock should not be loaded in production')
}
```

**环境变量文件**：

```bash
# .env.development
VITE_API_BASE=/api
VITE_USE_MOCK=true

# .env.production
VITE_API_BASE=/api
VITE_USE_MOCK=false
```

### 7.6 可观测性

**Mock 模式标识**：
- HeaderBar 右上角增加「Mock 模式」徽标（橙色 `a-tag`），仅 `VITE_USE_MOCK=true` 时显示
- 浏览器控制台启动时输出 `[Mock] 已启用 5 个 handler，共 19 个 endpoint`

**请求日志**（dev 模式）：
- 现有 `shared/utils/logger.ts` 增加 `logRequest(method, url, status, durationMs)`
- Mock 响应也走 logger，标记 `[Mock]` 前缀

**错误监控**：
- 沿用现有 `app.config.errorHandler` 全局错误处理
- Mock handler 抛出的错误同样走 `BusinessError` / `ForbiddenError` 路径
- 测试覆盖率：`tests/coverage/` 输出 HTML 报告，CI 中检查新增文件覆盖率 ≥ 80%

### 7.7 性能基线

| 指标 | 目标 |
|---|---|
| 首屏加载（Mock 模式） | < 2s（含 Mock 初始化） |
| 菜单树渲染 | < 100ms（34 节点） |
| 列表页加载 | < 500ms（含 300ms mock 延迟） |
| ServerMonitor 轮询 | 单次 snapshot < 50ms |
| Mock seed 初始化 | < 50ms（100 条日志 + 50 key） |

### 7.8 验收清单

**功能验收**：
- 6 个新页面可访问，路由无 404
- HeaderBar「个人中心」跳转 `/account/profile` 正常
- HeaderBar「修改密码」跳转 `/account/profile?tab=password`
- 动态菜单加载成功，SiderMenu 渲染 7 目录 34 菜单
- Mock 模式下 5 类 API 返回正确数据
- 修改密码走真实 Identity API，成功后跳登录页
- 强制下线 / 删除菜单 / 删除缓存 key 均有二次确认
- 6 页面响应式布局在 1366/1440/1920 三档正常

**代码验收**：
- 新增组件单测覆盖率 ≥ 80%
- Mock handler 单测全部通过
- 6 页面组件测试关键用例通过
- 生产构建产物不含 Mock 代码
- `VITE_USE_MOCK=false` 时回退静态路由，6 新页面仍可访问（依赖后端 API）

**文档验收**：
- spec 文档自检通过（无占位符、无歧义）
- 后端 API 需求清单（5 Controller / 19 Endpoint）已文档化

## 8 实施顺序建议

建议按以下顺序分阶段实施（具体拆分由 writing-plans skill 决定）：

1. **基础设施层**：Mock 基础设施（seed + handlers 骨架）+ 动态路由基础设施（component-map + dynamic-routes + auth-guard 改造）
2. **共享组件层**：StatisticCard、PasswordStrengthIndicator、TreeTableDraggable + 单测
3. **菜单管理页**：先打通动态菜单闭环（MenuManagement + menuStore + SiderMenu 改造），其他页面依赖菜单加载
4. **其余 5 页**：可并行实现，每页 = api + types + view + spec
5. **Profile.vue**：修复 404，与 06-account 模块整合
6. **联调与验收**：Mock 模式端到端验证 + 生产构建验证

## 9 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| 动态菜单加载失败导致黑屏 | 用户无法访问任何页面 | auth-guard §4.3 步骤 7 catch 回退静态路由聚合 |
| Mock 数据与真实后端 DTO 不一致 | 切换真实 API 后字段缺失 | spec §3.3-3.7 DTO 定义同时作为前后端契约，后端实装时严格对齐 |
| import.meta.glob 路径匹配遗漏 | 部分页面路由注入失败 | component-map 加载时 console.warn 未匹配项；单测覆盖 34 个 component 全部命中 |
| localStorage 容量限制 | Mock 数据被截断 | seed 总量约 50KB，远低于 5MB 限制；超出时 Mock handler 捕获 QuotaExceededError 提示重置 |
| axios-mock-adapter 与真实请求冲突 | 修改密码等真实 API 被 mock 拦截 | handler 仅注册 5 个前缀，其余 `passThrough()`；单测验证 `/users/me/password` 不被拦截 |
