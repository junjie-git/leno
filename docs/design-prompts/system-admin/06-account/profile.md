# 个人中心 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：06-account 个人账号
- **页面类型**：表单页 + 卡片展示
- **目标用户**：系统管理员（Admin）
- **核心目标**：查看与修改当前登录管理员的个人资料、修改密码、管理双因子认证（启用/确认/禁用），管理自身账号安全。
- **访问入口**：Header 用户菜单「个人中心」/ Header 用户菜单「修改密码」跳转锚点
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：左侧个人卡片 + 右侧 Tab 切换（资料/安全/双因子）。
- **关键区域**：
  - 区域 A（个人卡片）：左侧 280px 宽，展示头像 + 用户名 + 角色（Admin）+ 最后登录时间 + 最后登录 IP，`<a-card>` 包裹。
  - 区域 B（Tab 区）：右侧自适应，`<a-tabs>` 三个 Tab。
  - 区域 C（资料 Tab）：`<a-form>` 含用户名（只读）+ 邮箱 + 手机号 + 昵称 + 「保存」`IdempotencyButton`。
  - 区域 D（安全 Tab）：修改密码表单 — 当前密码 + 新密码 + 确认新密码 + 「修改密码」`IdempotencyButton`；密码强度指示器。
  - 区域 E（双因子 Tab）：双因子状态展示（已启用/未启用）+ QR 码区（启用时显示）+ TOTP 确认输入框 + 启用/禁用按钮。
- **响应式断点**：≥1200px 左右布局；992-1199px 上下布局（卡片在上，Tab 在下）。
- **首屏内容**：个人卡片 + 资料 Tab 表单。
- **线框图描述**：

```
┌──────────┬──────────────────────────────┐
│ [头像]    │ [资料] [安全] [双因子]         │
│ zhang    │ ┌──────────────────────────┐ │
│ Admin    │ │ 用户名(只读): zhang       │ │
│ 最后登录  │ │ 邮箱: [_______________]   │ │
│ 07-26    │ │ 手机: [_______________]   │ │
│ 1.2.3.4  │ │ 昵称: [_______________]   │ │
│          │ │ [保存]                    │ │
│          │ └──────────────────────────┘ │
└──────────┴──────────────────────────────┘
 安全：当前密码/新密码/确认 → [修改密码]
 双因子：状态 + QR码 + TOTP确认 → [启用]/[禁用]
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（旧域 UserAuth 双轨兜底，端点路径不变；由 `UsersController` 接管 `/api/users/me`、密码与双因子流程）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/users/me` | 查询当前用户资料 | Admin |
| PUT | `/api/users/me` | 修改当前用户资料 | Admin |
| PUT | `/api/users/me/password` | 修改当前用户密码 | Admin |
| POST | `/api/users/me/two-factor/enable` | 启用双因子：生成密钥与 QR 码 URI | Admin |
| POST | `/api/users/me/two-factor/confirm` | 确认双因子：验证 TOTP 码 | Admin |
| POST | `/api/users/me/two-factor/disable` | 禁用双因子 | Admin |

- **请求参数**：更新资料 `UpdateProfileDto`（email/phone/nickname）；修改密码 `ChangePasswordDto`（currentPassword/newPassword）；确认双因子 `TwoFactorConfirmDto`（code）。
- **响应字段**：`UserDto` 含 `userId`、`username`、`email`、`phone`、`nickname`、`roles`、`twoFactorEnabled`、`lastLoginAt`、`lastLoginIp`；启用双因子返回 `TwoFactorEnableResponseDto`（secret/qrCodeUri/manualEntryKey）。
- **数据加载策略**：进入页面 GET `/api/users/me` 加载资料；双因子 QR 码按需加载（点击启用时）。
- **缓存策略**：资料缓存至 Pinia `useUserStore`，修改后更新缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/users/me` → 个人卡片 + 资料表单渲染。
  2. 修改资料字段 → 点击「保存」 → `IdempotencyButton` → PUT `/api/users/me` → `message.success('资料保存成功')` 1.5s。
  3. 切换「安全」Tab → 填写当前密码 + 新密码 + 确认新密码 → 点击「修改密码」 → PUT `/api/users/me/password` → `message.success('密码修改成功，请重新登录')` 1.5s → 清除 Token → 跳 `/login`。
  4. 切换「双因子」Tab → 查看当前状态。
  5. 未启用 → 点击「启用」 → POST `/api/users/me/two-factor/enable` → 展示 QR 码 + 手动输入密钥 → 用户扫码后输入 TOTP 码 → 点击「确认」 → POST confirm → `message.success('双因子认证已启用')` 1.5s。
  6. 已启用 → 点击「禁用」 → `ConfirmDialog` → POST `/api/users/me/two-factor/disable` → `message.success('双因子认证已禁用')` 1.5s。
- **分支流程**：
  - 当前密码错误：后端 400，`message.error('当前密码错误')` 3s。
  - 新密码与确认不一致：前端校验拦截。
  - 新密码强度不足：前端校验拦截，强度指示器红色。
  - TOTP 码错误：后端 400，`message.error('验证码错误')` 3s。
  - 双因子已启用再次启用：按钮 disabled。
- **跨页面流转**：修改密码成功后跳 `/login` 重新登录；从 Header 用户菜单「修改密码」跳转带 `tab=security` 锚点自动切换安全 Tab。
- **状态机可视化**：双因子状态 `StatusTag` — 已启用绿、未启用灰。

## 5. 组件清单
- **基础组件**：`<a-card>`、`<a-tabs>`、`<a-form>`、`<a-form-item>`、`<a-input>`、`<a-input-password>`、`<a-avatar>`、`<a-tag>`、`<a-descriptions>`、`<a-image>`（QR 码）
- **业务组件**：
  - `IdempotencyButton`（见 shared/components.md §2）— 保存/修改密码/启用/禁用
  - `StatusTag`（见 shared/components.md §1）— 双因子状态
  - `ConfirmDialog`（见 shared/components.md §10）— 禁用双因子确认
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`UserOutlined`（头像占位）、`MailOutlined`（邮箱）、`PhoneOutlined`（手机）、`LockOutlined`（密码）、`SafetyOutlined`（双因子）16px。
- **空状态**：不适用（当前用户资料始终存在）。

## 6. 视觉规范
- **主色应用**：保存/启用按钮主色；修改密码按钮主色；禁用双因子按钮 danger。
- **状态色**：双因子已启用 `#52C41A`、未启用 `#8C8C8C`；密码强度弱红、中黄、强绿。
- **间距**：卡片内边距 24px；表单项间距 24px；Tab 与表单 16px；QR 码 200×200px。
- **字体**：用户名 20px semibold；角色标签 12px；表单标签 14px；输入框 14px；密钥 12px monospace。
- **图标尺寸**：表单图标 16px；头像 64px。

## 7. 异常处理与边界
- **加载态**：个人卡片 `<a-skeleton>`；表单 `<a-spin>`；QR 码加载 `<a-spin>`。
- **空数据**：不适用。
- **错误态**：密码错误/TOTP 错误 `message.error` 3s；网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin']`（仅当前用户自身）。
- **并发与乐观锁**：保存/修改密码/启用/禁用均 `IdempotencyButton` 幂等；QR 码密钥有效期由后端控制。
- **危险操作确认**：
  - 修改密码 `ConfirmDialog` 内容「修改密码后当前登录将失效，需要重新登录。是否继续？」确认按钮主色。
  - 禁用双因子 `ConfirmDialog` 内容「禁用双因子认证将降低账号安全性，攻击者仅需密码即可登录。强烈建议保持启用。是否确认禁用？」确认按钮 danger 红色。

## 8. 验收要点
- [ ] 资料保存成功后更新 Pinia 缓存
- [ ] 修改密码后清除 Token 跳转登录页
- [ ] 密码强度指示器实时反馈
- [ ] 双因子启用流程：QR 码 → 扫码 → TOTP 确认
- [ ] 禁用双因子有 danger 二次确认
- **性能要求**：页面加载 < 1s；QR 码生成 < 1s；表单提交 < 1s。
- **可访问性**：表单字段有 label；Tab 方向键切换；QR 码有 alt 文本「双因子认证二维码」；密码强度有 aria-live。
