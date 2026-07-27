# 双因子认证 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：01-auth 认证
- **页面类型**：表单页
- **目标用户**：买家（Buyer）
- **核心目标**：买家在登录流程中或绑定双因子后输入 TOTP 验证码完成二次验证，或在账号安全页启用/禁用双因子。
- **访问入口**：登录后端返回需双因子时跳转；「账号安全」页双因子开关。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：NavBar（返回+标题「双因子验证」）+ 中部验证码输入区 + 底部提交按钮，无 Tabbar。两种场景共用页面：登录二次验证（匿名）、双因子管理（已登录）。
- **关键区域**：
  - 区域 A（提示区）：图标盾牌 + 文案「请输入身份验证器中的 6 位动态码」。
  - 区域 B（输入区）：6 位数字输入框，使用 `van-password-input`（6 格）或 6 个独立 `van-field`。
  - 区域 C（操作区）：「提交」按钮 + 「重新获取密钥」链接（仅启用场景）。
  - 区域 D（绑定场景，启用时）：QR 码图 + 密钥字符串 + 「我已扫码，确认启用」按钮。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：提示文案、6 位验证码输入框、提交按钮。
- **线框图描述**：
```
┌──────────────────┐
│ ← 双因子验证     │
├──────────────────┤
│      🛡️          │
│ 请输入身份验证器 │
│ 中的 6 位动态码  │
│                  │
│ [ ] [ ] [ ]      │
│ [ ] [ ] [ ]      │
│                  │
│ [   提交   ]     │
│                  │
│ 启用场景：        │
│ [QR码]  密钥:XXX │
│ [已扫码，确认]   │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（旧域 UserAuth 双轨兜底，端点路径不变）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/auth/two-factor/verify` | 登录二次验证，签发 JWT | 匿名 |
| POST | `/api/users/me/two-factor/enable` | 生成密钥与 QR 码 URI | Buyer |
| POST | `/api/users/me/two-factor/confirm` | 确认启用，验证 TOTP 码 | Buyer |
| POST | `/api/users/me/two-factor/disable` | 禁用双因子 | Buyer |

- **请求参数**：`TwoFactorVerifyDto` 含登录上下文临时凭证 + `code`；`TwoFactorConfirmDto` 含 `code`。
- **响应字段**：`verify` 返回 `TokenDto`；`enable` 返回 `TwoFactorEnableResponseDto` 含 `qrCodeUri`、`secret`、`manualEntryCode`；`confirm`/`disable` 返回 `ApiResponse`。
- **数据加载策略**：登录验证场景进入页面无加载；启用场景进入页面调用 `enable` 获取 QR 码。
- **缓存策略**：登录验证的临时凭证暂存内存；启用场景的 secret 暂存内存供 confirm 校验。

## 4. 交互流程
- **主流程（登录二次验证）**：
  1. 登录端点返回需双因子 + 临时凭证 → 跳 `/two-factor?mode=verify` 携带凭证。
  2. 用户在身份验证器 App 查看 6 位动态码 → 输入 6 格框（自动聚焦下一格）。
  3. 输满 6 位自动触发或点击「提交」→ 调用 `POST /api/auth/two-factor/verify` → 成功存 token → 跳首页。
- **主流程（启用双因子）**：
  1. 「账号安全」页开启双因子开关 → 跳 `/two-factor?mode=enable`。
  2. 页面调用 `POST /api/users/me/two-factor/enable` → 展示 QR 码与密钥。
  3. 用户扫码后在身份验证器输入确认码 → 点击「确认启用」→ 调用 `confirm` → 成功 `showToast('双因子已启用')` → 返回安全页。
- **分支流程**：
  - 验证码错误：`showToast('验证码错误')`，输入框清空重新聚焦。
  - 验证码过期：提示「验证码已过期，请重新获取」。
  - 禁用双因子：在安全页开关关闭时 `showConfirmDialog` 二次确认 → 调用 `disable`。
- **跨页面流转**：验证成功跳首页；启用/禁用成功返回安全页。
- **状态机可视化**：待输入 → 提交中(loading) → 验证通过（跳转）/ 验证失败（清空重输）。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-password-input`、`van-button`、`van-image`（QR 码）、`van-toast`、`van-dialog`。
- **业务组件**：`IdempotencyButton`（见 shared/components.md §2）— 提交/确认按钮。
- **图表组件**：无（QR 码为图片非图表）。
- **图标使用**：盾牌图标本地 SVG；返回 `arrow-left`。
- **空状态**：不涉及。

## 6. 视觉规范
- **主色应用**：提交按钮、QR 码边框主色 `#1677FF`；盾牌图标主色。
- **状态色**：验证码错误输入框边框 `#FF4D4F`；成功 toast `success`。
- **间距**：图标距顶 48px；输入框距图标 24px；按钮距输入框 32px。
- **字体**：标题 16px medium；提示文案 14px `#595959`；密钥 14px monospace。
- **图标尺寸**：盾牌 48px；QR 码 160×160。

## 7. 异常处理与边界
- **加载态**：提交按钮 loading；QR 码加载用 `van-image` placeholder。
- **空数据**：不涉及。
- **错误态**：验证码错误清空重输；网络异常 toast 3s。
- **权限控制**：verify 模式匿名（凭临时凭证）；enable/confirm/disable 模式需 Buyer 登录。
- **并发与乐观锁**：`IdempotencyButton` 防重复提交验证。
- **危险操作确认**：禁用双因子使用 `showConfirmDialog` 二次确认，文案「禁用后账号安全性将降低，确认禁用？」。

## 8. 验收要点
- [ ] 6 位验证码输入框自动聚焦下一格，输满可自动提交。
- [ ] 登录验证成功签发 token 并跳首页。
- [ ] 启用场景展示 QR 码与密钥，支持手动输入。
- [ ] 禁用双因子二次确认。
- [ ] 验证码错误清空输入框并重新聚焦。
- **性能要求**：QR 码生成 < 1s；验证响应 < 800ms。
- **可访问性**：输入框 `aria-label="6位验证码"`；QR 码提供 `alt` 描述；按钮键盘可达。
