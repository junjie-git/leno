# 系统配置 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：03-system-governance 系统治理
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）、运营管理员（Operator）
- **核心目标**：管理平台全局配置项（SystemConfig），按键/分组查询，新建/编辑/启停配置，敏感值掩码展示。
- **访问入口**：Sider「系统治理 → 系统配置」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 新建/编辑弹窗 + 分组侧边导航。
- **关键区域**：
  - 区域 A（分组导航）：左侧 `<a-menu mode="inline">` 列出全部分组（来自 `GET /api/admin/system-configs/groups`），点击切换分组筛选。
  - 区域 B（筛选条）：key 搜索 + 状态多选（Enabled/Disabled）+ 「新建配置」按钮。
  - 区域 C（主表格）：列含 key/分组/值（敏感掩码）/状态/最近变更/操作（编辑/启用/停用/查看明文）。
  - 区域 D（弹窗表单）：`<a-modal width="560">` 含 key（编辑时只读）、分组、值类型（string/int/bool/json/secret）、值（secret 类型用 `<a-textarea>` + 显示/隐藏切换）、描述、状态。
- **响应式断点**：≥1200px 左右 20%/80%；992-1199px 分组导航折叠为顶部下拉。
- **首屏内容**：全部配置（不选分组）按 key 字母序。
- **线框图描述**：

```
┌──────────┬─────────────────────────────────────┐
│ 全部分组  │ [搜索] [状态] [新建配置]            │
│ ├ payment├─────────────────────────────────────┤
│ ├ notify │ key │ 分组 │ 值 │ 状态 │ 最近 │ 操作 │
│ ├ cart   │ payment.timeout │ payment │ 30  │启用│编辑│
│ └ search │ smtp.password   │ notify  │ ****│启用│查看明文│
└──────────┴─────────────────────────────────────┘
→ 弹窗：key/分组/类型/值/描述/状态
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/system-configs` | 分页查询系统配置（支持 key/分组/状态） | Admin,Operator |
| GET | `/api/admin/system-configs/groups` | 获取全部配置分组（去重） | Admin,Operator |
| GET | `/api/admin/system-configs/by-key/{key}` | 按键获取配置（加密值掩码） | Admin,Operator |
| POST | `/api/admin/system-configs` | 创建系统配置 | Admin,Operator |
| PUT | `/api/admin/system-configs/{configId}` | 更新系统配置（键不可变） | Admin,Operator |
| POST | `/api/admin/system-configs/{configId}/enable` | 启用配置 | Admin,Operator |
| POST | `/api/admin/system-configs/{configId}/disable` | 停用配置 | Admin,Operator |

- **请求参数**：列表 `key/group/status/page/pageSize`；创建 `SaveSystemConfigDto`（key/group/valueType/value/description）；编辑 `UpdateSystemConfigDto`。
- **响应字段**：`SystemConfigDto` 含 `ConfigId`、`Key`、`Group`、`ValueType`、`ValueMasked`、`Description`、`Status`、`UpdatedAt`。
- **数据加载策略**：进入页面并行加载配置列表 + 分组列表；切换分组前端筛选或重新请求。
- **缓存策略**：列表缓存 1 分钟；分组列表缓存 5 分钟；编辑/启停后失效。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行 GET configs + GET groups → 表格 + 侧边导航渲染。
  2. 点击分组项 → 重新请求 `group={selected}` → 表格刷新。
  3. 点击「新建配置」 → 弹窗填表 → POST → `message.success('配置已创建')`。
  4. 点击「编辑」 → 弹窗预填（值显示掩码，secret 类型可点击「显示明文」二次鉴权后显示） → PUT → 刷新。
  5. 点击「启用/停用」 → `ConfirmDialog` → POST enable/disable → 状态更新。
- **分支流程**：
  - key 已存在：后端 409，`message.error('配置键已存在')` 3s。
  - 查看明文：弹窗内「显示明文」按钮触发二次鉴权（重新输入管理员密码），通过后 GET by-key 返回明文。
- **跨页面流转**：点击「查看审计」跳 `/audit/audit-logs?resourceType=SystemConfig&keyword={key}`。
- **状态机可视化**：Disabled → Enabled → Disabled，`StatusTag` 自定义 config 类型。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-menu mode="inline">`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-textarea>`、`<a-select>`、`<a-input-password>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 配置状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 启停确认
  - `PermissionGuard`（见 shared/components.md §3）— 查看明文权限
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`EyeOutlined`/`EyeInvisibleOutlined`（明文切换）、`KeyOutlined` 16px。
- **空状态**：「该分组下暂无配置」+ CTA「新建配置」。

## 6. 视觉规范
- **主色应用**：新建按钮主色；选中分组项背景 `#E6F4FF`；key 用等宽字体 14px。
- **状态色**：Enabled 绿、Disabled 灰；secret 类型值 `<a-tag color="orange">****</a-tag>`。
- **间距**：左右分栏 24px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；key 14px medium monospace；值 12px `#595959`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格与分组导航均 `<a-skeleton>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：409 key 冲突 `message.error` 3s；明文查看鉴权失败 `message.error('鉴权失败')` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；写操作 `PermissionGuard permission="config:write"`；查看明文 `permission="config:reveal"`（仅 Admin）。
- **并发与乐观锁**：无乐观锁（配置低频变更）。
- **危险操作确认**：停用启用中的配置 `ConfirmDialog` 内容「停用后使用该配置的功能将回退到默认值，可能影响线上行为。可随时启用恢复。」确认按钮默认样式。

## 8. 验收要点
- [ ] key 编辑时只读
- [ ] secret 类型值默认掩码，查看明文需二次鉴权
- [ ] 分组导航点击切换筛选
- [ ] 启停有二次确认
- **性能要求**：首屏 < 1.5s；分组列表 < 50 项；搜索防抖 300ms。
- **可访问性**：分组菜单键盘导航；表格键盘导航；明文切换有 aria-label。
