# 角色管理 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：02-user-access 用户与权限
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）
- **核心目标**：管理平台角色与权限码，新建/编辑角色、分配权限、删除自定义角色（内置角色不可删）。
- **访问入口**：Sider「用户与权限 → 角色管理」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：左侧角色列表 + 右侧角色详情与权限树。
- **关键区域**：
  - 区域 A（左侧角色列表）：`<a-list>` 显示角色名 + 标签（内置/自定义）+ 用户数，按创建时间倒序，含搜索框与「新增角色」按钮。
  - 区域 B（右侧详情）：上半区 `<a-descriptions>` 展示角色基本信息（名称/描述/创建人/创建时间）+ 编辑/删除按钮；下半区权限分配区。
  - 区域 C（权限分配）：`<a-tree checkable>` 权限树，按模块分组（用户/商品/订单/促销/售后/支付/通知/系统管理），全量替换模式，含「保存权限」`IdempotencyButton`。
- **响应式断点**：≥1200px 左右 30%/70%；992-1199px 上下堆叠。
- **首屏内容**：首个角色详情 + 权限树展开第一层。
- **线框图描述**：

```
┌──────────────┬───────────────────────────────┐
│ [搜索] [新增] │ 角色名 [内置]                  │
│ ├ Admin      │ 描述：系统管理员                │
│ ├ Operator   │ [编辑] [删除]                  │
│ ├ Seller     ├───────────────────────────────┤
│ └ Buyer      │ 权限分配                       │
│              │ □ 用户管理                    │
│              │   □ user:read  □ user:suspend │
│              │ [保存权限]                    │
└──────────────┴───────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：AccessControl 域（旧域 UserAuth 双轨兜底，端点路径不变；由 `AdminRolesController` 接管，含角色 CRUD 与权限分配共 7 端点）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/roles` | 分页查询角色列表 | Admin |
| GET | `/api/admin/roles/{roleId}` | 查询角色详情 | Admin |
| POST | `/api/admin/roles` | 创建角色 | Admin |
| PUT | `/api/admin/roles/{roleId}` | 编辑角色 | Admin |
| DELETE | `/api/admin/roles/{roleId}` | 删除角色（内置不可删） | Admin |
| GET | `/api/admin/roles/{roleId}/permissions` | 查看角色权限列表 | Admin |
| PUT | `/api/admin/roles/{roleId}/permissions` | 更新角色权限（全量替换） | Admin |

- **请求参数**：列表 `keyword/page/pageSize`；创建/编辑 `SaveRoleDto`（name/description）；权限更新 `UpdatePermissionsDto`（permissions:string[]）。
- **响应字段**：`RoleDto` 含 `Id`、`Name`、`Description`、`IsBuiltIn`、`CreatedAt`、`CreatedBy`、`UserCount`。
- **数据加载策略**：进入页面加载角色列表，默认选中首个角色；点击角色加载详情与权限。
- **缓存策略**：角色列表缓存 5 分钟；权限列表缓存 5 分钟（编辑后失效）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/roles` → 左侧列表渲染，自动选中首个 → GET 详情 + 权限。
  2. 点击「新增角色」 → 弹窗表单（名称/描述） → POST `/api/admin/roles` → 列表新增项。
  3. 点击「编辑」 → 弹窗预填 → PUT `/api/admin/roles/{roleId}` → 详情刷新。
  4. 勾选权限树 → 「保存权限」 → PUT `/api/admin/roles/{roleId}/permissions` → `message.success('权限已更新')`。
  5. 点击「删除」 → `ConfirmDialog` → DELETE → 列表移除。
- **分支流程**：
  - 内置角色（IsBuiltIn=true）：删除按钮 disabled，Tooltip「内置角色不可删除」；名称不可编辑。
  - 删除时角色下仍有用户：后端返回 409，`message.error('角色下仍有用户，请先迁移')` 3s。
- **跨页面流转**：点击用户数链接跳 `/user-access/users?roleId={roleId}`。
- **状态机可视化**：无状态字段，IsBuiltIn 标识内置。

## 5. 组件清单
- **基础组件**：`<a-list>`、`<a-tree checkable>`、`<a-descriptions>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-textarea>`
- **业务组件**：
  - `IdempotencyButton`（见 shared/components.md §2）— 保存权限
  - `ConfirmDialog`（见 shared/components.md §10）— 删除确认
  - `PermissionGuard`（见 shared/components.md §3）— 按钮权限
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`（新增）、`EditOutlined`、`DeleteOutlined`、`SafetyOutlined` 16px。
- **空状态**：「暂无角色，点击新增创建第一个角色」+ CTA「新增角色」。

## 6. 视觉规范
- **主色应用**：新增按钮主色；选中角色项背景 `#E6F4FF`；权限树已选项主色。
- **状态色**：内置角色 `<a-tag color="purple">`；自定义 `<a-tag color="blue">`。
- **间距**：左右分栏间距 24px；列表项高 48px；权限树节点间距 8px。
- **字体**：角色名 16px medium；描述 14px；权限码 12px `#595959`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：列表 `<a-skeleton>`；详情 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：删除 409 `message.error` 3s；网络错误重试。
- **权限控制**：页面级 `roles: ['Admin']`；按钮 `PermissionGuard permission="role:write"`。
- **并发与乐观锁**：权限全量替换无乐观锁；编辑名称无乐观锁（仅 Admin 可操作，冲突概率低）。
- **危险操作确认**：删除 `ConfirmDialog` 内容「删除后该角色的权限配置将丢失，已分配该角色的用户需重新分配。此操作不可逆。」确认按钮 danger。

## 8. 验收要点
- [ ] 内置角色删除按钮 disabled 且有 Tooltip
- [ ] 权限树支持全选/反选子节点
- [ ] 保存权限后权限列表缓存失效
- [ ] 删除有用户关联的角色返回 409 友好提示
- **性能要求**：首屏 < 1.5s；权限树节点 < 200 无需虚拟滚动。
- **可访问性**：列表支持键盘上下选择；权限树支持键盘勾选；对话框聚焦管理。
