# 登录 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：01-auth 认证
- **页面类型**：表单页
- **目标用户**：买家（Buyer）
- **核心目标**：买家通过账号密码或第三方账号登录，获取 JWT 并进入首页或原 redirect 页面。
- **访问入口**：未登录访问受保护路由自动跳转；「我的」页未登录态点击；启动 App 未携带有效 token。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 Logo 区 + 中部表单区 + 底部第三方登录与辅助链接区，无 NavBar 与 Tabbar，全屏聚焦登录任务。
- **关键区域**：
  - 区域 A（品牌区）：顶部居中 Logo（80×80）+ 应用名「Leno」（24px semibold），距顶 64px。
  - 区域 B（表单区）：`van-form` 含用户名 `van-field`、密码 `van-field`（type=password，右侧可切换明文）、登录按钮 `van-button`（type=primary，block）。表单宽度距左右各 24px。
  - 区域 C（辅助操作）：表单下方「忘记密码」右对齐、「注册账号」左对齐，同行排布。
  - 区域 D（第三方登录）：底部「其他登录方式」分隔线 + 微信/支付宝图标按钮（48×48，圆形）。
- **响应式断点**：375px 基准；≥768px 表单最大宽 480px 居中。
- **首屏内容**：Logo、用户名输入框、密码输入框、登录按钮。
- **线框图描述**：
```
┌──────────────────┐
│      [Logo]      │
│      Leno        │
│                  │
│ [用户名______]   │
│ [密码____👁]   │
│ [   登录   ]     │
│ 注册账号  忘记密码│
│                  │
│ ──其他方式──     │
│  [微信] [支付宝] │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（旧域 UserAuth 双轨兜底，端点路径不变）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/account/login` | 账号密码登录 | 匿名 |
| POST | `/api/auth/refresh` | 刷新令牌 | 匿名 |
| GET | `/api/auth/oauth/{provider}/login` | 获取第三方授权 URL | 匿名 |

- **请求参数**：`LoginDto` 含 `userName`/`email`/`phone`（任一）、`password`；`refresh` 端点需 `refreshToken`。
- **响应字段**：`TokenDto` 含 `accessToken`、`refreshToken`、`expiresIn`、`tokenType`；accessToken 写入 Pinia `useUserStore` 与 localStorage，refreshToken 写入 localStorage。
- **数据加载策略**：进入页面无数据加载；提交时调用登录端点。
- **缓存策略**：登录成功后 token 持久化至 localStorage（key: `leno_token`、`leno_refresh_token`），启动时读取并校验过期时间，过期则静默调用 refresh 续期。

## 4. 交互流程
- **主流程**：
  1. 用户输入用户名/邮箱/手机号 → 失焦校验非空 → 输入密码 → 失焦校验长度 6-32。
  2. 点击「登录」→ 按钮 loading + disabled → 调用 `POST /api/account/login` → 成功存 token → `showToast('登录成功')` → 跳转 `redirect` 或 `/`。
  3. 失败 → `showToast(err.message)` 显示 3s → 按钮恢复 → 密码框清空。
- **分支流程**：
  - 后端返回需双因子：跳 `/two-factor` 携带临时凭证。
  - 账号锁定：提示「账号已锁定，请联系客服」。
  - 点击「忘记密码」：跳 `/forgot-password`。
  - 点击「注册账号」：跳 `/register`。
  - 点击微信/支付宝：调用 `GET /api/auth/oauth/{provider}/login?redirectUri=...` → 拿到 `authorizationUrl` → `window.location.href` 跳转。
- **跨页面流转**：登录成功后若有 `redirect` query 则跳回原页面，否则跳首页。
- **状态机可视化**：未登录 → 登录中(loading) → 已登录(token 存在) / 登录失败(回到未登录)。

## 5. 组件清单
- **基础组件**：`van-form`、`van-field`、`van-button`、`van-divider`、`van-icon`、`van-toast`。
- **业务组件**：`IdempotencyButton`（见 shared/components.md §2）— 登录提交按钮，注入 `Idempotency-Key`。
- **图表组件**：无。
- **图标使用**：密码明文切换 `eye-o`/`closed-eye`（Vant 内置）；微信、支付宝图标使用本地 SVG。
- **空状态**：不涉及列表空状态。

## 6. 视觉规范
- **主色应用**：登录按钮 `van-button type="primary"` 使用主色 `#1677FF`；Logo 主题色。
- **状态色**：输入校验错误文字 `#FF4D4F` 12px；登录失败 toast 采用 `fail` 类型。
- **间距**：表单项间距 12px（`spacing/3`）；按钮与表单间距 24px（`spacing/6`）；品牌区距顶 64px。
- **字体**：应用名 24px semibold；输入框 16px；辅助链接 14px `#1677FF`。
- **图标尺寸**：明文切换 20px；第三方登录 32px。

## 7. 异常处理与边界
- **加载态**：登录按钮 `loading` prop，点击后立即 true，响应返回后 false。
- **空数据**：不涉及。
- **错误态**：网络错误 `showToast('网络异常，请稍后重试')`；401 凭证错误显示后端返回 message；429 限流提示「操作过于频繁，请稍后再试」。
- **权限控制**：本页匿名访问；已登录用户访问自动跳首页。
- **并发与乐观锁**：`IdempotencyButton` 300ms 防抖 + 重复点击拦截，避免重复提交生成多 token。
- **危险操作确认**：不涉及删除类操作。

## 8. 验收要点
- [ ] 输入框失焦校验，错误红色提示位于字段下方。
- [ ] 登录按钮点击后立即 loading+disabled，防重复提交。
- [ ] 登录成功后 token 持久化并跳转 redirect 或首页。
- [ ] 第三方登录按钮跳转授权 URL。
- [ ] 密码可切换明文/密文。
- **性能要求**：首屏 < 1s；登录响应 < 800ms；输入防抖 300ms。
- **可访问性**：输入框 `aria-label` 标注用途；按钮支持键盘 Enter 提交；对比度 ≥ 4.5:1。
