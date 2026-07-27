# OAuth 客户端 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：02-user-access 用户与权限
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）
- **核心目标**：管理第三方 OAuth2 提供方（GitHub/Google/微信等）的客户端配置，新建/编辑/启停提供方。
- **访问入口**：Sider「用户与权限 → OAuth 客户端」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部操作条 + 主表格 + 新建/编辑弹窗。
- **关键区域**：
  - 区域 A（操作条）：「新建提供方」按钮 + 状态筛选（启用/禁用/全部）+ 刷新。
  - 区域 B（主表格）：列含提供方 provider/Client ID/Client Secret（掩码）/回调 URL/状态/操作（编辑/启用/禁用），分页 20。
  - 区域 C（弹窗表单）：`<a-modal width="560">` 含字段 provider（新建时可选下拉，编辑时只读）、clientId、clientSecret、scopes、authorizationEndpoint、tokenEndpoint、userInfoEndpoint、回调 URL。
- **响应式断点**：≥1200px 表格 7 列；992-1199px 隐藏「scopes」列。
- **首屏内容**：全部提供方列表。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [新建提供方] [状态筛选 ▼] [刷新]                │
├────────────────────────────────────────────────┤
│ provider │ Client ID │ Secret │ 回调URL │ 状态 │ 操作 │
│ github   │ Iv1.xxx   │ ****   │ /callback│启用 │ 编辑/禁用 │
└────────────────────────────────────────────────┘
→ 弹窗：provider/clientId/secret/scopes/endpoints/回调URL
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（旧域 UserAuth 双轨兜底，端点路径不变；由 `AdminOAuthClientsController` 接管）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/oauth-clients` | 查询所有 OAuth 客户端配置（Secret 掩码） | Admin |
| POST | `/api/admin/oauth-clients/{provider}` | 新建 OAuth 客户端配置（默认禁用） | Admin |
| PUT | `/api/admin/oauth-clients/{provider}` | 更新指定提供方配置 | Admin |
| POST | `/api/admin/oauth-clients/{provider}/enable` | 启用指定提供方 | Admin |
| POST | `/api/admin/oauth-clients/{provider}/disable` | 禁用指定提供方 | Admin |

- **请求参数**：新建/编辑 `UpdateOAuthClientDto`（clientId/clientSecret/scopes/authorizationEndpoint/tokenEndpoint/userInfoEndpoint/redirectUri）。
- **响应字段**：`OAuthClientDto` 含 `Provider`、`ClientId`、`ClientSecretMasked`、`Scopes`、`AuthorizationEndpoint`、`TokenEndpoint`、`UserInfoEndpoint`、`RedirectUri`、`Enabled`。
- **数据加载策略**：进入页面加载全部；状态筛选前端过滤。
- **缓存策略**：列表缓存 5 分钟；启停/编辑后失效。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/oauth-clients` → 表格渲染。
  2. 点击「新建提供方」 → 弹窗选择 provider 下拉 → 填写表单 → POST → `message.success('OAuth 客户端配置已创建（默认禁用，需显式启用）')`。
  3. 点击「编辑」 → 弹窗预填（Secret 显示掩码，可清空重填） → PUT → 刷新。
  4. 点击「启用/禁用」 → `ConfirmDialog` → POST enable/disable → 状态更新。
- **分支流程**：
  - 新建时 provider 已存在：后端返回 409，`message.error('该提供方已存在配置')` 3s。
  - 编辑时 provider 不存在：后端返回 404，`message.error('提供方不存在')` 3s。
  - 启用前未填写必要字段（clientId/secret）：前端校验拦截，提示「启用前需填写 Client ID 与 Secret」。
- **跨页面流转**：点击「查看登录历史」跳 `/audit/audit-logs?resourceType=OAuth&keyword={provider}`。
- **状态机可视化**：禁用 → 启用 → 禁用，使用 `StatusTag` 自定义 oauth 类型。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-input-password>`、`<a-select>`、`<a-switch>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 启用状态
  - `IdempotencyButton`（见 shared/components.md §2）— 提交
  - `ConfirmDialog`（见 shared/components.md §10）— 启停确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`CheckCircleOutlined`、`StopOutlined` 16px。
- **空状态**：「暂无 OAuth 提供方配置」+ CTA「新建提供方」。

## 6. 视觉规范
- **主色应用**：新建按钮主色；provider 名 14px medium；启用状态 `<a-tag color="green">`。
- **状态色**：启用绿、禁用灰 `#8C8C8C`。
- **间距**：操作条与表格 16px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；provider 14px medium；Secret 掩码 12px `#8C8C8C`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：409/404 `message.error` 3s；网络错误重试。
- **权限控制**：页面级 `roles: ['Admin']`；按钮 `PermissionGuard permission="oauth:write"`。
- **并发与乐观锁**：无乐观锁；provider 唯一性由后端保证。
- **危险操作确认**：禁用启用中的提供方 `ConfirmDialog` 内容「禁用后用户将无法通过该提供方登录，已绑定的账号不受影响。可随时重新启用。」确认按钮默认样式（非 danger，因可逆）。

## 8. 验收要点
- [ ] Secret 字段始终掩码显示，编辑时清空才可重填
- [ ] provider 新建时下拉可选，编辑时只读
- [ ] 启用前校验必要字段
- [ ] 409 冲突友好提示
- **性能要求**：首屏 < 1.5s；表格行 < 20 无需虚拟滚动。
- **可访问性**：表格键盘导航；表单字段 label 关联 input；Secret 字段 `autocomplete="new-password"`。
