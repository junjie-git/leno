# SystemAdmin P0 API ↔ 前端对接设计

- 日期：2026-07-29
- 主题：把 5 个新增 P0 后端控制器（Menus / OnlineUsers / LoginLogs / Cache / ServerMonitor）与 Identity 的 `PUT /api/users/me/password` 从前端 mock 切换到真实后端，并补齐发现的不匹配缺口
- 方案：A — 单次完整对齐
- 关联文档：
  - 后端来源 spec：`docs/superpowers/specs/2026-07-27-system-admin-p0-backend-features-design.md`
  - 前端来源 spec：`docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md`、`docs/superpowers/specs/2026-07-27-system-admin-p0-features-supplement-design.md`

## 1. 背景与问题陈述

最近一次提交 `653f4fe feat: 新增P0级功能后端实现` 为 SystemAdmin BC 增加了 5 个 P0 控制器（共 19 个端点），全部完整实现，无占位。前端 Vue 3 SPA（`web/system-admin/`）已为这 5 个功能搭建了完整的 API/DTO/View，但**当前运行在 `axios-mock-adapter` 上**（`.env.development` 中 `VITE_USE_MOCK=true`）。

本设计回答两个问题：
1. **是否缺少 API？** —— 端点层面**无缺失**：前端调用的每个端点后端都有，后端暴露的每个端点前端都有调用方。无需新增 API。
2. **如何对接？** —— 存在 1 个全局阻塞 + 3 个严重 DTO 不匹配 + 2 个主要语义缺口 + 若干次要项，需对齐后才能切换到真实后端。

### 1.1 缺口总表

| 级别 | 问题 | 影响 |
|---|---|---|
| 全局阻塞 | `ApiResponse.code` 约定分歧：后端成功 `code=200`，前端拦截器要求 `code===0` 否则抛 `BusinessError` | 阻断全部 5 个 P0 功能的所有成功响应 |
| 严重 | `OnlineUser.id`(前端) vs `sessionId`(后端) | 在线用户踢下线/详情全部 400 |
| 严重 | `MetricPoint.t/v`(前端) vs `timestamp/value`(后端) | CPU/内存/磁盘历史图表全空 |
| 严重 | `ChangePassword.currentPassword`(前端) vs `oldPassword`(后端) | 改密永远报"旧密码错误" |
| 主要 | `MenuSortItem.parentId` 前端发送、后端忽略 | 拖拽跨父节点移动静默丢失 |
| 主要 | `RedisKeyDetail.value` 后端是 JSON 字符串、前端期望已解析对象；缺 `truncated`；多 `db` | 缓存键详情显示原始字符串 |
| 次要 | `server-monitor` 历史参数 `range='5m'`(前端) vs `rangeSeconds=300`(后端) | 历史查询失败 |
| 次要 | `LoginLogDto` 前端缺 `userId` | 无法显示用户 ID 列（非阻断） |

## 2. 已确认的关键决策

| # | 决策点 | 选择 |
|---|---|---|
| D1 | `ApiResponse.code` 成功约定 | 前端成功判定改为 `code === 200`（不动后端） |
| D2 | `MenuSortItem.parentId` 语义缺口 | 扩展后端支持跨父拖拽（`SortAsync` 在 parentId 变化时调 `MoveTo`） |
| D3 | `RedisKeyDetail.value` 类型 | 前端 `JSON.parse` 字符串，补 `truncated`，移除 `db` |
| D4 | `Idempotency-Key` 头 | 保持现状，后端不消费（UI 防重复点击体验保留） |
| D5 | 执行方案 | A — 单次完整对齐 |

## 3. 架构与范围

### 3.1 范围内
- 5 个 P0 后端控制器对应的前端功能：菜单管理、在线用户、登录日志、缓存监控、服务器监控
- Identity BC 的修改密码端点
- 全局 `ApiResponse` 约定对齐
- Mock 退役

### 3.2 范围外（YAGNI）
- 后端幂等去重中间件（D4 决策保持现状）
- `UpdateProfileDto` 的 `email/phone/remark/avatar` 不匹配（与 5 个 P0 功能无关，属 Profile 编辑功能，单独处理）
- `RedisKeyDto.Type` 枚举宽度收敛（低风险，不阻断）
- mock 处理器业务码（40001 等）与真实后端 HTTP 状态码的分支审计（切真实后端后按需清理）

### 3.3 架构原则
- 后端是事实来源（已完整实现），前端跟随调整
- 唯一后端改动是菜单排序扩展（D2）
- 所有改动集中在现有文件，不新增抽象层、不引入新模式
- 遵循 SOLID / KISS / YAGNI / DRY

## 4. 后端改动（仅 1 处，2 文件）

### 4.1 `MenuSortItemDto` 增字段
文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/DTOs/MenuDtos.cs`

`MenuSortItemDto` 当前仅有 `{ Id; Sort }`。新增可空字段：

```csharp
public Guid? ParentId { get; set; }
```

可空语义：`null` 表示该节点仅重排、不改变父节点；非空表示移动到指定父节点下。`null` 与"仅重排"等价，保持向后兼容。

### 4.2 `MenuAppService.SortAsync` 扩展
文件：`src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/MenuAppService.cs`

现有循环对每个 `item` 仅调用 `menu.ChangeSort(item.Sort)`。改为：

```csharp
foreach (var item in items)
{
    var menu = await _menuRepository.GetByIdAsync(item.Id, ct)
        ?? throw new NotFoundException("MENU_NOT_FOUND", item.Id.ToString());

    if (item.ParentId.HasValue && menu.ParentId != item.ParentId.Value)
    {
        menu.MoveTo(item.ParentId.Value);
    }

    menu.ChangeSort(item.Sort);
}
```

**复用现有聚合方法**：`MoveTo` 已包含环引用检测、根节点限制等所有领域不变式校验，`SortAsync` 不重复校验。`NotFoundException` 已由全局中间件映射为 404。`SaveEntitiesAsync` 统一提交 + outbox 派发逻辑不变。

## 5. 前端改动

### 5.1 全局约定（2 文件）
- `web/system-admin/src/shared/http/client.ts`（行 93-100）：成功判定从 `body.code !== 0` 改为 `body.code !== 200`；抛 `BusinessError` 分支同步更新。
- `web/system-admin/src/shared/types/index.ts`（行 8-20）：约定文档从"code: 0 表示成功"改为"code: 200 表示成功"。
- CSV 导出（字符串响应）不被 unwrap 的逻辑保持不变（`typeof response.data === 'object' && 'code' in response.data` 判定已正确放行字符串）。

### 5.2 在线用户 — `sessionId` 对齐（3 文件）
- `web/system-admin/src/modules/02-user-access/types/online-user.dto.ts`：`id: string` → `sessionId: string`。
- `web/system-admin/src/modules/02-user-access/api/online-users.api.ts`：`get(id)` / `kick(id)` 形参 `id` → `sessionId`；路径变量名对齐。
- `web/system-admin/src/modules/02-user-access/views/OnlineUsers.vue`（行 144/393/426/428/430）：`record.id` / `target.id` → `record.sessionId` / `target.sessionId`；行过滤 `u.id !== target.id` → `u.sessionId !== target.sessionId`。

### 5.3 服务器监控 — `timestamp/value` + `rangeSeconds`（3 文件）
- `web/system-admin/src/modules/07-monitoring/types/server-monitor.dto.ts`：`MetricPointDto` 从 `{ t: string; v: number }` → `{ timestamp: string; value: number }`。
- `web/system-admin/src/modules/07-monitoring/views/ServerMonitor.vue`（行 198-212）：`p.t` / `p.v` → `p.timestamp` / `p.value`。
- `web/system-admin/src/modules/07-monitoring/api/server-monitor.api.ts`：`history(metric, range='5m')` → `history(metric, rangeSeconds=300)`。
- `ServerMonitor.vue` 的范围选择器：从 `'5m' / '1h'` 改为秒数 `300 / 1800 / 3600`（与后端 `rangeSeconds` int 1-3600 约束一致）。

### 5.4 修改密码 — `oldPassword` 对齐（2 文件）
- `web/system-admin/src/modules/06-account/types/auth.dto.ts`：`ChangePasswordDto.currentPassword` → `oldPassword`。
- `web/system-admin/src/modules/06-account/views/Profile.vue`：表单字段绑定 `currentPassword` → `oldPassword`（密码强度校验逻辑不变）。

### 5.5 缓存键详情 — value 字符串解析（2 文件）
- `web/system-admin/src/modules/04-runtime-ops/types/cache.dto.ts`：`RedisKeyDetailDto` 的 `value: unknown` → `value: string`；新增 `truncated: boolean`；移除 `db: number`（db 由查询参数传入，前端在 view 层透传）。
- `web/system-admin/src/modules/04-runtime-ops/views/CacheMonitor.vue`：渲染前 `JSON.parse(detail.value)`，try/catch 回退原字符串；`truncated` 为真时显示截断提示徽标。

### 5.6 菜单 — 无前端改动
D2 已扩展后端匹配前端 `parentId`，前端 `MenuSortItemDto` 保持现状。

### 5.7 次要项（保守处理）
- `web/system-admin/src/modules/05-audit/types/login-log.dto.ts`：补 `userId?: string`（additive，后端已返回）。
- 各 `void` 返回类型（`menu.remove` / `menu.sort` / `onlineUsers.kick` / `cache.deleteKey`）：**保持 `void`**，忽略后端返回体（无消费方，YAGNI）。

## 6. Mock 退役

### 6.1 关闭全局开关
- `web/system-admin/.env.development`：`VITE_USE_MOCK=true` → `false`。

### 6.2 Mock 处理器存档对齐
5 个 P0 功能的 mock 处理器（`web/system-admin/src/shared/http/mock/handlers/{menu,online-users,login-logs,cache,server}.ts`）**保留不删除**，作为存档便于将来临时切回 mock 调试。逐个同步：
- 响应体 `code: 0` → `code: 200`
- `shared/http/mock/data/seed.ts`：
  - online-users 段：`id` → `sessionId`
  - server 段：`{ t, v }` → `{ timestamp, value }`
  - account 段：`currentPassword` → `oldPassword`
  - cache 段：`RedisKeyDetail.value` 保持字符串（与后端一致）

### 6.3 兜底保留
`web/system-admin/src/shared/http/mock/index.ts` 的 `passThrough()` 兜底逻辑保留不变。

## 7. 测试策略

### 7.1 后端测试（xUnit，`Leno.SystemAdmin.Api.Tests`）
- 新增 `MenuSortReparentTests`：验证 `SortAsync` 在 `parentId` 变化时调用 `MoveTo`（聚合 `ParentId` 实际变更）；同父时仅 `ChangeSort`；根节点移动、环引用拒绝（`MoveTo` 已有校验）。
- 现有 `P0SystemAdminFeaturesE2ETests.cs` 已覆盖 19 端点，回归即可。
- Mock 数据仅用于测试，绝不在开发或生产环境中 Mock 数据（遵循用户规则 10.4）。

### 7.2 前端测试（Vitest + `@vue/test-utils`）
- `web/system-admin/src/shared/http/client.spec.ts`：新增用例 `code === 200` 视为成功、`code !== 200` 抛 `BusinessError`；CSV 导出（字符串响应）不被 unwrap。
- `online-users.api.spec.ts` / `server-monitor.api.spec.ts` / `auth.api.spec.ts` / `cache.api.spec.ts`：断言请求体/参数字段名（`sessionId` / `oldPassword` / `rangeSeconds`）与路径变量。
- `OnlineUsers.vue` / `ServerMonitor.vue` / `CacheMonitor.vue` / `Profile.vue` 的现有 spec 跟随 DTO 字段重命名更新断言。
- E2E `web/system-admin/tests/e2e/login.smoke.spec.ts` 不依赖 mock（走真实后端或独立 fixture），无需改。

## 8. 验收标准

1. 后端 `dotnet test` 全绿，`MenuSortReparentTests` 通过。
2. 前端 `pnpm --filter system-admin typecheck` 通过（DTO 字段重命名无残留旧引用）。
3. 前端 `pnpm --filter system-admin test` 全绿，覆盖率不低于既有阈值（lines/functions/statements ≥ 70%，branches ≥ 60%）。
4. 前端 `pnpm --filter system-admin build` 成功。
5. 手动联调：`VITE_USE_MOCK=false` 启动前端 + 后端 SystemAdmin + Identity 服务，5 个功能页面（菜单管理、在线用户、登录日志、缓存监控、服务器监控）+ 修改密码全部走真实后端，无 `BusinessError(200)` 报错，CPU/内存历史图表有数据，踢下线/改密/菜单跨父拖拽/缓存键详情解析均生效。

## 9. 数据流

```
[Vue View] → [module api/*.ts (axios, baseURL=/api)]
   ↓ Idempotency-Key 头（cosmetic, 后端不消费）
[Vite dev proxy /api → http://localhost:5001]
   ↓
[SystemAdmin.Api Controllers] → [Application Services] → [Domain Aggregates + Repos]
   ↓
[ApiResponse<T> { code: 200, message, data }]  ← 成功约定
   ↓ axios 响应拦截器（code === 200 视为成功，unwrap data）
[Vue View 渲染]

例外：GET /api/admin/login-logs/export 返回 FileResult (text/csv)，字符串响应不被 unwrap，直通 client。
```

## 10. 风险与缓解

| 风险 | 缓解 |
|---|---|
| 前端 DTO 字段重命名遗漏旧引用 | `pnpm typecheck` 强校验兜底（验收 #2） |
| mock 处理器存档与新约定不同步导致切回 mock 时失效 | 6.2 已要求 mock 处理器同步 `code: 200` + seed 字段对齐 |
| 后端 `MoveTo` 在 `SortAsync` 批量场景下的环引用检测时机 | 复用 `MoveTo` 现有校验；`MenuSortReparentTests` 覆盖环引用拒绝用例 |
| dev proxy 同时路由 SystemAdmin 与 Identity 两个服务 | 现有 `VITE_API_TARGET=http://localhost:5001` 已统一指向 SystemAdmin；Identity `PUT /api/users/me/password` 走同一端口前缀 `/api/users`（若实际为独立服务需确认网关路由，属环境配置，不阻断本次代码改动） |
