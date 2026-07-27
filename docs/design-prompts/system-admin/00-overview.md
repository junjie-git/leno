# 系统管理后台总览

## 1. 端定位与角色画像
- **目标用户**：系统管理员（Admin）。承担平台技术运维职责，典型场景包括限流规则下发、死信消息处置、索引重建、审计回溯、健康巡检、配置治理。具备一定后端与中间件（Redis、RabbitMQ、Elasticsearch、Prometheus）背景，能理解阈值、窗口、状态机等抽象。
- **核心目标**：在严肃专业、低频重操作的界面下，保障平台稳定、可观测、可追溯，所有关键操作可审计、可回滚。
- **使用频率**：低频次、高时长、单任务深度强。日常巡检 + 突发故障响应双模式并存。
- **设备特征**：桌面 1440+ 优先，1366 兼容；不支持移动端。多 Tab 并行操作常见。

## 2. 信息架构与导航
- **一级菜单**：仪表盘、用户与权限、系统治理、运行时运维、审计与对账、个人账号、系统监控。
- **二级菜单**：
  - 仪表盘 → [运营总览](./01-dashboard/operations-overview.md)、[支付统计](./01-dashboard/payment-stats.md)、[积分统计](./01-dashboard/points-stats.md)、[通知送达率](./01-dashboard/notification-delivery.md)、[售后统计](./01-dashboard/after-sales-stats.md)、[店铺排行](./01-dashboard/shop-ranking.md)、[报表快照](./01-dashboard/report-snapshots.md)
  - 用户与权限 → [用户管理](./02-user-access/user-management.md)、[角色管理](./02-user-access/role-management.md)、[OAuth 客户端](./02-user-access/oauth-clients.md)、[运营人员](./02-user-access/operators.md)
  - 系统治理 → [功能开关](./03-system-governance/feature-flags.md)、[系统配置](./03-system-governance/system-configs.md)、[数据字典](./03-system-governance/data-dictionaries.md)、[公告管理](./03-system-governance/announcements.md)
  - 运行时运维 → [限流规则](./04-runtime-ops/rate-limit-rules.md)、[索引重建](./04-runtime-ops/index-rebuild.md)、[死信队列](./04-runtime-ops/dead-letter-queue.md)、[定时任务](./04-runtime-ops/scheduled-tasks.md)、[健康监控](./04-runtime-ops/health-monitoring.md)、[告警管理](./04-runtime-ops/alert-management.md)
  - 审计与对账 → [审计日志](./05-audit/audit-logs.md)、[对账管理](./05-audit/reconciliation.md)、[Outbox 监控](./05-audit/outbox-monitor.md)
  - 个人账号 → [登录与双因子](./06-account/login-2fa.md)、[个人中心](./06-account/profile.md)、[通知中心](./06-account/notifications.md)
  - 系统监控 → [Prometheus 监控大盘](./07-monitoring/prometheus-dashboard.md)
- **菜单组织原则**：按业务域聚合（仪表盘 / 用户 / 治理 / 运维 / 审计）+ 角色高频路径优先。Sider 折叠后仅显示图标。
- **快捷入口**：Header 右上角铃铛通知、用户菜单（个人中心、修改密码、登出）、全局搜索（Cmd/Ctrl+K，支持菜单与端点跳转）。

## 3. 页面路由规划
- **路由表**：

| path | component | 鉴权 |
|-|-|-|
| `/login` | `account/Login2fa.vue` | 匿名 |
| `/dashboard/operations-overview` | `dashboard/OperationsOverview.vue` | Admin |
| `/dashboard/payment-stats` | `dashboard/PaymentStats.vue` | Admin |
| `/dashboard/points-stats` | `dashboard/PointsStats.vue` | Admin |
| `/dashboard/notification-delivery` | `dashboard/NotificationDelivery.vue` | Admin |
| `/dashboard/after-sales-stats` | `dashboard/AfterSalesStats.vue` | Admin |
| `/dashboard/shop-ranking` | `dashboard/ShopRanking.vue` | Admin |
| `/dashboard/report-snapshots` | `dashboard/ReportSnapshots.vue` | Admin |
| `/user-access/users` | `user-access/UserManagement.vue` | Admin |
| `/user-access/roles` | `user-access/RoleManagement.vue` | Admin |
| `/user-access/oauth-clients` | `user-access/OAuthClients.vue` | Admin |
| `/user-access/operators` | `user-access/Operators.vue` | Admin |
| `/system-governance/feature-flags` | `governance/FeatureFlags.vue` | Admin |
| `/system-governance/system-configs` | `governance/SystemConfigs.vue` | Admin,Operator |
| `/system-governance/data-dictionaries` | `governance/DataDictionaries.vue` | Admin,Operator |
| `/system-governance/announcements` | `governance/Announcements.vue` | Admin,Operator |
| `/runtime-ops/rate-limit-rules` | `runtime/RateLimitRules.vue` | Admin |
| `/runtime-ops/index-rebuild` | `runtime/IndexRebuild.vue` | Admin,Operator |
| `/runtime-ops/dead-letter-queue` | `runtime/DeadLetterQueue.vue` | Admin,Operator |
| `/runtime-ops/scheduled-tasks` | `runtime/ScheduledTasks.vue` | Admin,Operator |
| `/runtime-ops/health-monitoring` | `runtime/HealthMonitoring.vue` | Admin,Operator |
| `/runtime-ops/alert-management` | `runtime/AlertManagement.vue` | Admin |
| `/audit/audit-logs` | `audit/AuditLogs.vue` | Admin,Operator |
| `/audit/reconciliation` | `audit/Reconciliation.vue` | Admin,Operator |
| `/audit/outbox-monitor` | `audit/OutboxMonitor.vue` | Admin |
| `/account/profile` | `account/Profile.vue` | Admin |
| `/account/notifications` | `account/Notifications.vue` | Admin |
| `/monitoring/prometheus-dashboard` | `monitoring/PrometheusDashboard.vue` | Admin |

- **路由守卫**：`beforeEach` 校验登录态（无 token 跳 `/login`）；`requiresAuth + roles: ['Admin']` 校验角色（不足跳 403 页）；登录后强制双因子通过后才挂载动态路由；菜单按 `meta.menuKey` 动态渲染。

## 4. 全局布局
- **布局结构**：Ant Design Vue `BasicLayout`：Header 64px（Logo + Breadcrumb + 全局搜索 + 通知铃铛 + 用户菜单）+ Sider 200px（可折叠至 80px，深色 `#001529`）+ Content（24 栅格，padding 24px）+ Footer 32px（版权与版本号）。
- **全局组件**：
  - Header 用户菜单：个人中心、修改密码、切换主题（预留暗色切换点）、登出
  - 通知铃铛：`<a-badge :count="unread" />`，下拉显示最近 5 条告警/待办，点击进入通知中心
  - Breadcrumb：基于路由 `meta.title` 自动生成
  - ConfigProvider：注入主色 `#1677FF`、圆角 `6px`、字体栈 PingFang SC 优先
- **断点**：≥1200px Sider 全展开；992-1199px Sider 自动折叠；<992px 不支持（提示用户切换桌面端）。

## 5. 设计风格基调
- **整体气质**：严肃专业、低频重操作。色彩克制，以中性色阶为主，主色与状态色仅用于强调与状态指示。表格密度偏紧凑（`size="middle"`），减少视觉噪声。
- **与共享设计系统的关系**：完全遵循 `shared/design-system.md`，无偏离。差异点仅在于：危险操作密度更高（删除/丢弃/重投/封禁等），统一使用 `ConfirmDialog`（见 `shared/components.md §10`）二次确认；所有写操作通过 `IdempotencyButton`（见 `shared/components.md §2`）携带 `Idempotency-Key` 头。

## 6. 模块清单
- **模块表**：

| 模块 | 页面数 | 实现状态分布 | 优先级 |
|-|-|-|-|
| 01-dashboard 仪表盘 | 7 | ✅×7 | P0 |
| 02-user-access 用户与权限 | 4 | ✅×4 | P0 |
| 03-system-governance 系统治理 | 4 | ✅×4 | P0 |
| 04-runtime-ops 运行时运维 | 6 | ✅×5 / 🚧×1 | P0 |
| 05-audit 审计与对账 | 3 | ✅×2 / 🚧×1 | P1 |
| 06-account 个人账号 | 3 | ✅×2 / ➕×1 | P1 |
| 07-monitoring 系统监控 | 1 | ➕×1 | P2 |

- **优先级**：P0（已实现且日常巡检/治理必用）= dashboard / user-access / system-governance / runtime-ops 核心；P1（按需与个人侧）= audit / account；P2（外接 Prometheus）= monitoring。
- **API 来源**：SystemAdmin BC 13 控制器 74 端点（含 Operators 运营人员管理）+ **Identity 域**（AdminUsers / AdminOAuthClients / UsersMe 共享端点；旧域 UserAuth 双轨兜底）+ **AccessControl 域**（AdminRoles 角色与权限管理；旧域 UserAuth 双轨兜底）+ Notification 域（通知中心内部协作）。所有引用见各页面提示词「数据模型与 API 对接」段。

> **域拆分迁移双轨期说明（2026-07-26 起）**：阶段1-2 已完成，新域已就绪并经网关双轨挂载，端点路径不变，仅服务归属更新。旧域代码保留作回滚兜底，待阶段3观察期结束后下线。详见 `docs/feature-inventory/domain-migration-status.md`。

## 7. 与后端 API 的对应关系

### 7.1 域拆分映射表

| 模块 | 旧域 | 新域 | 备注 |
|-|-|-|-|
| 02-user-access 用户管理 | UserAuth | **Identity** | `/api/admin/users/*` 由 Identity 域 `AdminUsersController` 接管 |
| 02-user-access 角色与权限 | UserAuth | **AccessControl** | `/api/admin/roles/*`、`/api/admin/roles/{roleId}/permissions` 由 AccessControl 域 `AdminRolesController` 接管（7 端点） |
| 02-user-access OAuth 客户端 | UserAuth | **Identity** | `/api/admin/oauth-clients/*` 由 Identity 域 `AdminOAuthClientsController` 接管 |
| 02-user-access 运营人员 | SystemAdmin BC | SystemAdmin BC（未迁移） | `/api/admin/operators/*` 由 SystemAdmin BC `OperatorsController` 提供，不涉及域拆分 |
| 06-account 登录与双因子 | UserAuth | **Identity** | `/api/auth/*` 由 Identity 域 `AuthController` 接管 |
| 06-account 个人资料 | UserAuth | **Identity** | `/api/users/me`、密码、双因子由 Identity 域 `UsersController` 接管 |
| 06-account 通知中心 | Notification 域 | Notification 域（未迁移） | `/api/notifications/*` 由 Notification 域提供，不涉及域拆分 |
| 04-runtime-ops 健康监控 | 多 BC 聚合 | 多 BC 聚合（含新域） | `/api/admin/health*` 由 SystemAdmin BC 聚合，模块卡片含 Identity / AccessControl / UserCenter / Points / Membership / Review / AfterSales 新域 |
| 04-runtime-ops 限流规则 | 多 BC 分治 | 多 BC 分治（含新域） | `/api/admin/rate-limit-rules/*` 由 SystemAdmin BC 提供，筛选上下文含新域 |
