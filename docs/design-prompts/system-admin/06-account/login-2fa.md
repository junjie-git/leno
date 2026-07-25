# 登录与双因子 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：06-account 个人账号
- **页面类型**：流程页（两步登录）
- **目标用户**：系统管理员（Admin）
- **核心目标**：完成账号密码登录与双因子认证（TOTP）二次验证，通过后挂载动态路由进入主界面。系统管理员强制双因子，未通过不可进入业务页面。
- **访问入口**：直接访问 `/login` / 未登录或 token 过期时路由守卫重定向
- **实现状态**：➕ 补充功能

## 2. 页面布局与信息架构
- **整体布局**：居中卡片式两步流程，左侧品牌区 + 右侧表单区，步骤指示器顶部。
- **关键区域**：
  - 区域 A（品牌区）：左侧 50% 宽度，深色背景 `#001529`，展示 Logo + 平台名「Leno 系统管理后台」+ 安全提示文案「JWT + 双因子 + IP 白名单 + 全操作审计」。
  - 区域 B（步骤指示器）：`<a-steps :current="step">` 两步 — 「账号密码」「双因子验证」。
  - 区域 C（步骤一表单）：用户名 `<a-input>` + 密码 `<a-input-password>` + 「登录」`IdempotencyButton` + 「忘记密码」链接。
  - 区域 D（步骤二表单）：6 位 TOTP 码 `<a-input-otp>`（6 格）+ 「验证」`IdempotencyButton` + 「返回上一步」链接 + 提示文案「请打开 Authenticator App 获取验证码」。
- **响应式断点**：≥1200px 品牌区 + 表单区左右布局；992-1199px 仅表单区居中；<992px 不支持（提示切换桌面端）。
- **首屏内容**：步骤一账号密码表单。
- **线框图描述**：

```
┌──────────────────────┬──────────────────────┐
│                      │ [步骤一] 账号密码      │
│   Leno 系统管理后台   │ ┌──────────────────┐ │
│   安全：JWT+2FA+IP   │ │ 用户名            │ │
│   白名单+全操作审计  │ │ [______________]  │ │
│                      │ │ 密码              │ │
│                      │ │ [______________]  │ │
│                      │ │ [登录]  忘记密码?  │ │
│                      │ └──────────────────┘ │
└──────────────────────┴──────────────────────┘
 步骤二：[ _ ][ _ ][ _ ][ _ ][ _ ][ _ ]  [验证]  返回
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/auth/login` | 账号密码登录（验证成功后返回需双因子标记） | 匿名 |
| POST | `/api/auth/two-factor/verify` | 双因子二次验证，验证 TOTP 码并签发 JWT | 匿名 |
| POST | `/api/auth/forgot-password` | 忘记密码：发送重置链接 | 匿名 |
| POST | `/api/auth/reset-password` | 重置密码 | 匿名 |

- **请求参数**：登录 `LoginDto`（username/password）；双因子验证 `TwoFactorVerifyDto`（含登录返回的临时凭证 + TOTP 码）；忘记密码 `ForgotPasswordDto`（email）；重置密码 `ResetPasswordDto`（token + newPassword）。
- **响应字段**：登录成功返回 `TokenDto`（accessToken/refreshToken/expiresIn）；若需双因子，后端返回需双因子标记与临时凭证（`requiresTwoFactor: true` + `twoFactorToken`）；双因子验证成功返回 `TokenDto`。
- **数据加载策略**：无初始数据加载（纯表单流程）。
- **缓存策略**：登录成功后 Token 存入 Pinia `useUserStore` + localStorage；双因子临时凭证存内存（不持久化）。

## 4. 交互流程
- **主流程**：
  1. 进入 `/login` → 渲染步骤一表单。
  2. 输入用户名 + 密码 → 点击「登录」 → POST `/api/auth/login` → 后端校验密码。
  3. 若返回需双因子标记 → 步骤指示器前进到步骤二 → 渲染 TOTP 输入框。
  4. 用户输入 6 位 TOTP 码 → 点击「验证」 → POST `/api/auth/two-factor/verify` → 返回 TokenDto。
  5. 存储 Token → 路由跳转 `/dashboard/operations-overview` → `message.success('登录成功')` 1.5s。
- **分支流程**：
  - 账号未开启双因子：登录直接返回 TokenDto，跳过步骤二。
  - 密码错误：后端 401，`message.error('用户名或密码错误')` 3s，密码框清空聚焦。
  - TOTP 码错误：后端 401，`message.error('验证码错误或已过期')` 3s，输入框清空聚焦。
  - TOTP 码格式不正确（非 6 位数字）：前端校验拦截，`message.error('请输入 6 位数字验证码')`。
  - 账号被锁定：后端 423，`message.error('账号已锁定，请联系超级管理员')` 3s。
  - IP 不在白名单：后端 403，`message.error('当前 IP 不在白名单，禁止访问')` 3s。
- **跨页面流转**：登录成功跳 `/dashboard/operations-overview`（默认首页）；未登录访问业务页 → 路由守卫重定向 `/login?redirect={原路径}`，登录后跳回原路径。
- **状态机可视化**：步骤指示器 `current` 0 → 1；登录态：未登录 → 登录中 → 待双因子 → 已登录。

## 5. 组件清单
- **基础组件**：`<a-steps>`、`<a-form>`、`<a-form-item>`、`<a-input>`、`<a-input-password>`、`<a-input-otp>`、`<a-button>`、`<a-alert>`
- **业务组件**：
  - `IdempotencyButton`（见 shared/components.md §2）— 登录/验证按钮
  - `EmptyState`（见 shared/components.md §5）— IP 白名单拒绝时展示
- **图表组件**：无
- **图标使用**：`LockOutlined`（密码）、`SafetyOutlined`（双因子）、`UserOutlined`（用户名）16px；Logo 32px。
- **空状态**：IP 不在白名单时 `<a-alert type="error">` 全屏提示。

## 6. 视觉规范
- **主色应用**：登录/验证按钮主色 `#1677FF`；步骤指示器完成态主色；品牌区深色 `#001529`。
- **状态色**：错误提示 `#FF4D4F`；安全提示文案 `#52C41A`（强调安全）。
- **间距**：卡片宽 400px，内边距 32px；表单项间距 24px；按钮全宽；步骤指示器与表单 24px。
- **字体**：品牌名 24px semibold `#FFFFFF`；表单标签 14px；输入框 14px；安全提示 12px `#8C8C8C`。
- **图标尺寸**：表单图标 16px；Logo 32px。

## 7. 异常处理与边界
- **加载态**：登录/验证按钮 `loading`（请求期间 disabled）。
- **空数据**：不适用（表单页）。
- **错误态**：密码错误/TOTP 错误/账号锁定/IP 拒绝，分别 `message.error` 提示；网络错误 `message.error('网络异常，请稍后重试')` 3s。
- **权限控制**：`/login` 路由 `meta.requiresAuth: false`；已登录访问 `/login` 自动跳转首页。
- **并发与乐观锁**：登录/验证按钮 `IdempotencyButton` 防抖 300ms + 重复点击拦截；双因子临时凭证有效期由后端控制，过期需重新登录。
- **危险操作确认**：无危险操作。但登出（在用户菜单）使用 `Modal.confirm`「确认登出当前账号？」。

## 8. 验收要点
- [ ] 密码登录成功后正确进入双因子步骤
- [ ] TOTP 码 6 位数字输入框自动聚焦与逐格输入
- [ ] 各类错误（密码错/TOTP 错/锁定/IP 拒绝）有明确提示
- [ ] 登录按钮防抖与重复点击拦截
- [ ] 登录成功后 Token 存储并跳转 redirect 路径
- **性能要求**：页面加载 < 1s（无数据请求）；登录响应 < 2s；TOTP 验证响应 < 1s。
- **可访问性**：表单字段有 label 与 aria-label；输入框自动聚焦；键盘 Tab 导航；错误提示 aria-live。
