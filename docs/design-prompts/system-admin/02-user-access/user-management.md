# 用户管理 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：02-user-access 用户与权限
- **页面类型**：列表页 + 详情抽屉
- **目标用户**：系统管理员（Admin）
- **核心目标**：分页查询平台用户，查看用户详情、分配角色、锁定/恢复账户，处置异常账号。
- **访问入口**：Sider「用户与权限 → 用户管理」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 详情抽屉 + 角色分配弹窗。
- **关键区域**：
  - 区域 A（筛选条）：用户名/邮箱搜索 `<a-input-search>` + 角色多选 `<a-select>` + 状态多选（Active/Suspended/Locked）+ 注册时间 `DateTimeRangePicker`。
  - 区域 B（主表格）：列含用户ID/用户名/邮箱/角色/状态/注册时间/最近登录/操作（查看/锁定/恢复），分页 20。
  - 区域 C（详情抽屉）：`<a-drawer width="600">` 展示用户基本信息、角色列表、登录历史、审计记录链接。
  - 区域 D（角色分配弹窗）：`<a-modal>` 含角色穿梭框 `<a-transfer>`，确认后调用分配 API。
- **响应式断点**：≥1200px 表格 8 列；992-1199px 隐藏「注册时间」列。
- **首屏内容**：近 100 注册用户列表（默认按注册时间倒序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [搜索] [角色多选] [状态多选] [注册时间] [查询]   │
├────────────────────────────────────────────────┤
│ ID │ 用户名 │ 邮箱 │ 角色 │ 状态 │ 最近登录 │ 操作 │
└────────────────────────────────────────────────┘
→ 抽屉：基本信息 + 角色 + 登录历史
→ 弹窗：角色穿梭框 + 确认/取消
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/users` | 分页查询用户列表 | Admin,Operator |
| GET | `/api/admin/users/{id}` | 查询用户详情 | Admin,Operator |
| POST | `/api/admin/users/{id}/roles` | 为用户分配角色（幂等） | Admin,Operator |
| POST | `/api/admin/users/{id}/suspend` | 锁定用户账户 | Admin,Operator |
| POST | `/api/admin/users/{id}/resume` | 恢复用户账户 | Admin,Operator |

- **请求参数**：列表 `AdminUserQueryDto`（keyword/roles/statuses/fromTime/toTime/page/pageSize）；锁定 `SuspendUserDto`（reason）。
- **响应字段**：`AdminUserDto` 含 `Id`、`Username`、`Email`、`Phone`、`Roles`、`Status`、`CreatedAt`、`LastLoginAt`、`LastLoginIp`。
- **数据加载策略**：进入页面加载首页；分页/筛选重新请求；详情按需点击加载。
- **缓存策略**：列表不缓存（数据敏感且高频变化）；详情缓存 1 分钟。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/users?page=1&pageSize=20` → 表格渲染。
  2. 输入搜索 + 筛选 → 300ms 防抖后重新请求。
  3. 点击「查看」 → GET `/api/admin/users/{id}` → 抽屉展示。
  4. 点击「分配角色」 → 弹窗穿梭框 → 提交 POST `/api/admin/users/{id}/roles` → `message.success('角色已分配')`。
  5. 点击「锁定」 → `ConfirmDialog`（见 shared/components.md §10）→ POST `/api/admin/users/{id}/suspend` → 表格状态更新。
- **分支流程**：
  - 用户已是 Suspended 状态：「锁定」按钮 disabled，显示「恢复」按钮。
  - 分配角色幂等冲突：后端返回成功但提示「角色无变化」。
- **跨页面流转**：抽屉中点击「查看审计记录」跳 `/audit/audit-logs?operatorId={id}`。
- **状态机可视化**：用户状态 Active → Suspended（锁定）→ Active（恢复），使用 `StatusTag`（type 自定义 user）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-input-search>`、`<a-select mode="multiple">`、`<a-drawer>`、`<a-modal>`、`<a-transfer>`、`<a-descriptions>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 用户状态
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `IdempotencyButton`（见 shared/components.md §2）— 锁定/恢复按钮
  - `ConfirmDialog`（见 shared/components.md §10）— 锁定确认
  - `PermissionGuard`（见 shared/components.md §3）— 按钮权限
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`LockOutlined`（锁定）、`UnlockOutlined`（恢复）、`UserOutlined`、`TeamOutlined` 16px。
- **空状态**：「未找到匹配用户」+ CTA「清空筛选条件」。

## 6. 视觉规范
- **主色应用**：操作链接主色；角色 `<a-tag color="blue">`；分配角色按钮主色。
- **状态色**：Active `#52C41A`、Suspended `#FF4D4F`、Locked `#FAAD14`。
- **间距**：筛选条与表格 16px；表格行高 48px；抽屉内边距 24px。
- **字体**：表格 14px；用户名 14px medium；邮箱 12px `#8C8C8C`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>` 5 行；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 提示清空筛选。
- **错误态**：网络错误 `message.error` 3s；403 跳权限页。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；锁定/恢复操作 `PermissionGuard permission="user:suspend"`。
- **并发与乐观锁**：分配角色幂等，无乐观锁。
- **危险操作确认**：锁定操作 `ConfirmDialog` 内容「锁定后该用户将无法登录，关联的进行中订单不受影响。此操作可逆，可随时恢复。」确认按钮 danger 红色。

## 8. 验收要点
- [ ] 搜索输入 300ms 防抖
- [ ] 状态多选筛选生效
- [ ] 锁定操作二次确认且确认按钮 danger
- [ ] 角色分配穿梭框正确显示已选角色
- **性能要求**：首屏 < 1.5s；表格 > 100 行启用虚拟滚动；搜索防抖 300ms。
- **可访问性**：表格支持键盘导航；抽屉聚焦管理；锁定确认对话框可键盘确认/取消。
