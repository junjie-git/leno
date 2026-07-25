# 登录 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：09-account 个人中心
- **页面类型**：认证页（登录 + 双因子验证 + 忘记密码）
- **目标用户**：运营管理员（Operator）/ 系统管理员（Admin）
- **核心目标**：提供安全的运营后台登录入口，支持账号密码登录、双因子验证（TOTP）、忘记密码重置，登录后跳转运营总览。
- **访问入口**：直接访问 `/login`；未登录访问任何页面重定向至此；登录态过期自动跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：居中登录卡片（左侧品牌区 + 右侧表单区），含登录/双因子验证/忘记密码三种状态切换。
- **关键区域**：
  - 区域 A（品牌区）：左侧 50% 宽，展示 Leno 运营管理后台 Logo、标语、安全提示（IP 白名单 + 操作审计）
  - 区域 B（登录表单）：右侧 50% 宽，含用户名/邮箱、密码（含显示/隐藏）、记住我、登录按钮、忘记密码链接
  - 区域 C（双因子验证）：登录后若启用双因子，切换为 TOTP 验证码输入（6 位数字）
  - 区域 D（忘记密码）：邮箱输入 → 发送重置链接 → 重置密码（新密码 + 确认密码）
- **响应式断点**：≥992px 左右分栏；<992px 仅展示表单区，品牌区隐藏。
- **首屏内容**：品牌区 + 登录表单。
- **线框图描述**：

```
┌─────────────────────┬─────────────────────────┐
│                     │ 运营管理后台              │
│   Leno              │ ┌─────────────────────┐ │
│   运营管理后台       │ │ 用户名/邮箱          │ │
│   简洁 · 安全 · 高效 │ │ 密码            [👁] │ │
│                     │ │ ☐ 记住我   忘记密码? │ │
│   🔒 IP白名单        │ │ [      登录      ]   │ │
│   📋 操作审计        │ └─────────────────────┘ │
└─────────────────────┴─────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/auth/login` | 账号密码登录并签发令牌 | 匿名 |
| POST | `/api/auth/two-factor/verify` | 双因子认证二次验证 | 匿名 |
| POST | `/api/auth/refresh-token` | 刷新令牌 | 匿名 |
| POST | `/api/auth/logout` | 登出并吊销 JWT | 已认证 |
| POST | `/api/auth/forgot-password` | 忘记密码发送重置链接 | 匿名 |
| POST | `/api/auth/reset-password` | 重置密码 | 匿名 |

- **请求参数**：`LoginDto` 含 `Account`（用户名/邮箱）、`Password`、`RememberMe`；`TwoFactorVerifyDto` 含 `TempToken`、`Code`；`ForgotPasswordDto` 含 `Email`；`ResetPasswordDto` 含 `Email`、`Token`、`NewPassword`。
- **响应字段**：登录/双因子返回 `TokenDto` 含 `AccessToken`、`RefreshToken`、`ExpiresIn`、`Roles`、`RequiresTwoFactor`（true 时需二次验证）；忘记/重置密码返回 `ApiResponse`。
- **数据加载策略**：登录成功后存储 Token 至 Pinia + localStorage；路由跳转 `/dashboard/overview`。
- **缓存策略**：Token 存储 localStorage（记住我勾选时），否则 sessionStorage。

## 4. 交互流程
- **主流程**：
  1. 进入登录页 → 输入用户名/密码 → 点击登录 → 调用 login
  2. 若 `RequiresTwoFactor=true` → 切换双因子验证界面 → 输入 TOTP 6 位码 → 调用 verify
  3. 登录成功 → 存储 Token → 跳转运营总览
- **分支流程**：
  - 凭证错误：`message.error('用户名或密码错误')`，密码输入框清空
  - 账号停用：`message.error('账号已停用，请联系管理员')`
  - 双因子验证失败：`message.error('验证码错误或已过期')`
  - 忘记密码：点击链接 → 输入邮箱 → 发送重置链接 → 邮箱跳转重置页
  - 令牌刷新：拦截器检测 401 自动调用 refresh-token，失败则跳登录
- **跨页面流转**：登录成功跳转 `/dashboard/overview`；登出跳转 `/login`。
- **状态机可视化**：未登录 → 登录中 → 双因子验证（可选）→ 已登录 → 登出。

## 5. 组件清单
- **基础组件**：`<a-form>`、`<a-input>`、`<a-input-password>`、`<a-checkbox>`、`<a-button>`、`<a-alert>`
- **业务组件**：
  - `IdempotencyButton`（见 shared/components.md §2）— 登录/提交按钮
  - `EmptyState`（见 shared/components.md §5）— 不使用
- **图标使用**：`UserOutlined` 用户名、`LockOutlined` 密码、`SafetyOutlined` 双因子、`EyeOutlined/EyeInvisibleOutlined` 密码显隐
- **空状态**：不适用

## 6. 视觉规范
- **主色应用**：登录按钮主色 `#1677FF`，品牌区背景渐变 `#001529` → `#003A8C`。
- **状态色**：错误提示 `#FF4D4F` 红，安全提示 `#52C41A` 绿。
- **间距**：卡片宽度 480px，表单项间距 20px，按钮高度 40px。
- **字体**：品牌标语 24px semibold 白色，表单标签 14px `#000000D9`，输入框 14px，安全提示 12px `#8C8C8C`。
- **Logo**：48×48px，品牌区居中。
- **图标尺寸**：输入框前缀图标 16px。

## 7. 异常处理与边界
- **加载态**：登录/双因子按钮 loading + disabled。
- **空数据**：不适用。
- **错误态**：凭证错误红色 `<a-alert>` 提示；网络错误 `message.error('登录失败，请检查网络')`；5 次错误后强制等待 5 分钟（前端计时）。
- **权限控制**：登录后路由守卫校验 Operator/Admin 角色，非运营角色提示「无权访问运营后台」。
- **并发与乐观锁**：登录幂等，重复点击由 `IdempotencyButton` 拦截。
- **危险操作确认**：登出为危险操作，用户头像下拉菜单点击后 `<ConfirmDialog>` 确认。
- **安全边界**：密码传输加密（HTTPS），Token 存储 localStorage 设置过期，IP 白名单由网关校验，操作审计记录登录日志。

## 8. 验收要点
- [ ] 登录表单含用户名/密码/记住我/忘记密码
- [ ] 双因子验证界面支持 6 位 TOTP 码输入
- [ ] 凭证错误提示且密码框清空
- [ ] 忘记密码流程：邮箱 → 重置链接 → 重置密码
- [ ] 登录成功跳转运营总览，登出跳转登录页
- [ ] 令牌过期自动刷新，失败跳登录
- **性能要求**：登录响应 < 1s，页面首屏 < 800ms。
- **可访问性**：表单 label 关联 input，错误提示 aria-live="polite"，按钮 aria-label 含操作语义。
