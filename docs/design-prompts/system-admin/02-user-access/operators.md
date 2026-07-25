# 运营人员 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：02-user-access 用户与权限
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）
- **核心目标**：管理运营管理员账号，创建运营人员、分配权限码、激活/停用账号。
- **访问入口**：Sider「用户与权限 → 运营人员」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 新建/编辑弹窗 + 权限分配弹窗。
- **关键区域**：
  - 区域 A（筛选条）：用户名搜索 + 角色筛选（Operator/SeniorOperator/Manager）+ 状态筛选（Active/Inactive）。
  - 区域 B（主表格）：列含运营人员ID/用户名/姓名/角色/状态/最近登录/操作（查看/权限/激活/停用），分页 20。
  - 区域 C（新建弹窗）：`<a-modal>` 含用户名/姓名/邮箱/初始密码/角色。
  - 区域 D（权限分配弹窗）：`<a-modal width="640">` 含权限树穿梭框，全量替换。
- **响应式断点**：≥1200px 表格 8 列；992-1199px 隐藏「最近登录」。
- **首屏内容**：全部运营人员列表（默认按创建时间倒序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [搜索] [角色 ▼] [状态 ▼] [新建运营人员]        │
├────────────────────────────────────────────────┤
│ ID │ 用户名 │ 姓名 │ 角色 │ 状态 │ 最近登录 │ 操作 │
└────────────────────────────────────────────────┘
→ 弹窗：新建表单 / 权限穿梭框
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/operators` | 分页查询运营人员 | Admin,Operator |
| GET | `/api/admin/operators/{operatorId}` | 按标识获取运营人员 | Admin |
| POST | `/api/admin/operators` | 创建运营人员 | Admin |
| PUT | `/api/admin/operators/{operatorId}/permissions` | 更新运营人员权限（合并新增权限码） | Admin |
| POST | `/api/admin/operators/{operatorId}/activate` | 启用运营人员 | Admin |
| POST | `/api/admin/operators/{operatorId}/deactivate` | 停用运营人员 | Admin |

- **请求参数**：列表 `role/status/page/pageSize`；创建 `SaveOperatorDto`（username/name/email/password/role）；权限 `AssignPermissionsDto`（permissions）。
- **响应字段**：`OperatorDto` 含 `OperatorId`、`Username`、`Name`、`Email`、`Role`、`Status`、`Permissions`、`CreatedAt`、`LastLoginAt`。
- **数据加载策略**：进入页面加载首页；筛选重新请求；详情按需加载。
- **缓存策略**：列表不缓存；详情缓存 1 分钟。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/operators?page=1&pageSize=20` → 表格渲染。
  2. 点击「新建运营人员」 → 弹窗表单 → POST → `message.success('运营人员已创建')`。
  3. 点击「权限」 → 弹窗穿梭框预选 → PUT `/api/admin/operators/{id}/permissions` → `message.success('权限已更新')`。
  4. 点击「停用」 → `ConfirmDialog` → POST deactivate → 状态更新。
- **分支流程**：
  - 创建时用户名重复：后端 409，`message.error('用户名已存在')` 3s。
  - 停用自己：前端拦截，`message.warning('不能停用自己的账号')` 2s。
- **跨页面流转**：点击「查看审计」跳 `/audit/audit-logs?operatorId={operatorId}`。
- **状态机可视化**：Active ↔ Inactive，使用 `StatusTag` 自定义 operator 类型。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-input-password>`、`<a-select>`、`<a-transfer>`、`<a-tree>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 运营人员状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 停用确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`KeyOutlined`（权限）、`CheckOutlined`/`StopOutlined`（激活/停用）16px。
- **空状态**：「暂无运营人员」+ CTA「新建运营人员」。

## 6. 视觉规范
- **主色应用**：新建按钮主色；权限按钮主色；角色 `<a-tag color="cyan">`。
- **状态色**：Active `#52C41A`、Inactive `#8C8C8C`。
- **间距**：筛选条与表格 16px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；用户名 14px medium；邮箱 12px `#8C8C8C`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；弹窗 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：409 用户名冲突 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；写操作 `PermissionGuard permission="operator:write"`；Operator 仅能查看不能写。
- **并发与乐观锁**：权限合并新增无乐观锁。
- **危险操作确认**：停用 `ConfirmDialog` 内容「停用后该运营人员将无法登录，已分配的待办任务需重新分配。可随时激活恢复。」确认按钮默认样式（可逆）。

## 8. 验收要点
- [ ] 不能停用自己（前端拦截 + 友好提示）
- [ ] 用户名重复 409 友好提示
- [ ] 权限穿梭框正确预选已有权限
- [ ] 状态筛选生效
- **性能要求**：首屏 < 1.5s；搜索防抖 300ms。
- **可访问性**：表格键盘导航；表单字段 label 关联；对话框聚焦管理。
