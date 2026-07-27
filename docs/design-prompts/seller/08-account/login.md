# 登录 - 商家管理后台

## 1. 页面定位
- **所属端**：商家管理后台
- **所属模块**：08-account（个人账号）
- **页面类型**：表单页（全屏布局，无 Sider/Header）
- **目标用户**：卖家（Seller）
- **核心目标**：卖家通过账号密码登录商家管理后台，登录成功后跳转工作台；支持双因子认证二次验证与刷新令牌自动续期。
- **访问入口**：URL `/login`；未登录访问任意受保护路由自动跳转此页；登录态过期后跳转此页并携带 `redirect` 参数。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：全屏居中布局，左侧品牌展示区（占 50%，主色渐变背景）+ 右侧登录表单区（占 50%，白色背景）。无 Sider、Header、Footer。
- **关键区域**：
  - 区域 A（品牌展示区，左侧）：Leno Logo + 平台名称「Leno 商家管理后台」+ 经营标语「让经营更高效」+ 装饰插画。
  - 区域 B（登录表单区，右侧）：标题「卖家登录」+ 登录表单 + 辅助链接（忘记密码、注册入口）。
  - 区域 C（双因子验证态，条件显示）：当后端返回 `twoFactorRequired=true` 时，登录表单切换为 6 位 TOTP 验证码输入表单。
- **响应式断点**：≥1200px 左右两栏；992-1199px 仅显示右侧登录表单居中；<992px 不支持（提示请在桌面端访问）。
- **首屏内容**：品牌展示区 + 登录表单（账号、密码、记住我、登录按钮）。
- **线框图描述**：
```
┌────────────────────────────────┬─────────────────────────────────┐
│                                │                                 │
│                                │         Leno 商家管理后台         │
│         Leno Logo              │                                 │
│                                │         卖家登录                 │
│      让经营更高效               │                                 │
│      [装饰插画]                 │  账号 [用户名/邮箱/手机号]        │
│                                │  密码 [********] [👁]            │
│                                │  ☑ 记住我        忘记密码？       │
│                                │                                 │
│                                │  [          登录          ]      │
│                                │                                 │
│                                │  还没有账号？立即入驻            │
│                                │                                 │
└────────────────────────────────┴─────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（旧域 UserAuth 双轨兜底，端点路径不变；由 `AuthController` 接管）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/auth/login` | 账号密码登录，签发 JWT 与刷新令牌 | 公开 |
| POST | `/api/auth/two-factor/verify` | 双因子认证二次验证，提交 TOTP 码签发 JWT | 公开 |
| POST | `/api/auth/refresh-token` | 刷新令牌换取新令牌对 | 公开 |
| POST | `/api/auth/logout` | 登出并吊销当前 JWT | 已认证 |

- **请求参数**：
  - 登录：`LoginDto { account: string, password: string }`，`account` 可为用户名、邮箱或手机号。
  - 双因子验证：`TwoFactorVerifyDto { tempToken: string, code: string }`，`tempToken` 来自登录响应，`code` 为 6 位 TOTP 码。
  - 刷新令牌：`RefreshTokenDto { refreshToken: string }`。
  - 登出：无请求体，从 JWT 提取 `jti`。
- **响应字段**：登录/双因子/刷新均返回 `TokenDto { userId, username, accessToken, refreshToken, expiresIn, tokenType: "Bearer", twoFactorRequired, tempToken? }`。登录成功后前端需校验用户角色为 Seller（通过 JWT 解析或调用 `GET /api/users/me` 获取 `roles[]`），非卖家角色拒绝登录。
- **数据加载策略**：登录页无数据加载；登录成功后将 `accessToken`、`refreshToken`、`expiresIn` 存入 Pinia `useAuthStore` + localStorage（仅「记住我」勾选时持久化）。
- **缓存策略**：登录态缓存于 localStorage（记住我）或 sessionStorage（不记住）；令牌过期前 5 分钟自动调用 `refresh-token` 续期。

## 4. 交互流程
- **主流程**：
  1. 卖家在登录页输入账号与密码 → 点击「登录」→ 按钮立即 loading + disabled → 调用 `POST /api/auth/login`。
  2. 响应 `twoFactorRequired=false` → 校验用户角色为 Seller（非卖家 `message.error('该账号无卖家权限')` 并清除令牌）→ 存储令牌 → 跳转 `redirect` 参数指向的页面或默认 `/dashboard/overview`。
  3. 响应 `twoFactorRequired=true` → 表单切换为 TOTP 验证码输入（6 位数字，自动聚焦）→ 输入完成调用 `POST /api/auth/two-factor/verify` 携带 `tempToken` 与 `code` → 成功后同步骤 2 处理。
  4. 卖家勾选「记住我」→ 令牌持久化到 localStorage；不勾选 → 持久化到 sessionStorage。
- **分支流程**：
  - 账号或密码错误：`message.error('账号或密码错误')`，密码框清空聚焦。
  - 双因子验证码错误：`message.error('验证码错误或已过期')`，验证码框清空聚焦。
  - 账号被暂停/封禁：后端返回错误码，`message.error('账号已被暂停，请联系平台')`。
  - 非卖家角色：`message.error('该账号无卖家权限，请前往买家端')`。
  - 点击「忘记密码」→ 跳转忘记密码流程（独立页面，本页不实现）。
  - 点击「立即入驻」→ 跳转入驻申请页 `/shop/application`。
- **跨页面流转**：登录成功跳转 `redirect` 或工作台；登出后跳转登录页。
- **状态机可视化**：登录态流转：未登录 →（登录）→ 双因子待验证 →（验证码）→ 已登录；或 未登录 →（登录）→ 已登录；已登录 →（令牌过期）→ 自动刷新；已登录 →（登出）→ 未登录。

## 5. 组件清单
- **基础组件**：`<a-form>`、`<a-form-item>`、`<a-input>`、`<a-input-password>`、`<a-checkbox>`、`<a-button>`、`<a-input-otp>`（6 位 TOTP 输入，若组件库无则用 6 个 `<a-input>` 拼接）、`<a-typography-link>`。
- **业务组件**：`IdempotencyButton`（见 shared/components.md §2）— 登录按钮防重。
- **图表组件**：无。
- **图标使用**：`UserOutlined`（账号前缀）、`LockOutlined`（密码前缀）、`SafetyOutlined`（双因子验证图标）。
- **空状态**：无（表单页）。

## 6. 视觉规范
- **主色应用**：品牌展示区背景使用主色 `#1677FF` 渐变（`linear-gradient(135deg, #1677FF 0%, #0958D9 100%)`）；登录按钮主色 `#1677FF`；「立即入驻」链接主色。
- **状态色**：错误提示红 `#FF4D4F`；成功跳转绿 `#52C41A`（短暂提示）。
- **间距**：表单宽度 `400px` 居中，表单项间距 `24px`，按钮内边距 `8px 16px`，品牌区与表单区间无间距（全屏分割）。
- **字体**：平台名称 `24px` semibold 白色，标语 `16px` normal 白色，表单标题 `20px` medium，表单标签 `14px` normal，按钮文案 `14px` medium，辅助链接 `12px` `#595959`。
- **图标尺寸**：表单前缀图标 `16px`，Logo `48×48px`。

## 7. 异常处理与边界
- **加载态**：登录按钮点击后 `<a-spin>` 内嵌按钮 loading + disabled，直至响应返回。
- **空数据**：账号或密码为空时，提交按钮禁用；失焦校验提示「请输入账号」「请输入密码」。
- **错误态**：网络错误 `message.error('网络异常，请稍后重试')`；账号密码错误 `message.error('账号或密码错误')`；双因子验证码错误 `message.error('验证码错误或已过期')`；账号被暂停 `message.error('账号已被暂停，请联系平台')`；非卖家角色 `message.error('该账号无卖家权限')`。
- **权限控制**：登录页公开访问；登录成功后校验 Seller 角色；非卖家角色清除令牌拒绝登录。
- **并发与乐观锁**：登录按钮防抖 300ms + 防重（loading 期间点击无效），避免重复提交。
- **危险操作确认**：登录操作非危险操作无需二次确认；登出操作在 Header 用户菜单触发，需 `Modal.confirm` 二次确认。

## 8. 验收要点
- [ ] 登录表单正确展示账号、密码、记住我、登录按钮
- [ ] 账号或密码为空时提交按钮禁用，失焦校验提示
- [ ] 登录按钮点击后 loading + disabled，防重复提交
- [ ] 登录成功后跳转 `redirect` 或工作台
- [ ] 双因子认证场景正确切换为 TOTP 验证码输入表单
- [ ] 双因子验证码错误清空聚焦
- [ ] 非卖家角色登录被拒绝并提示
- [ ] 「记住我」勾选时令牌持久化到 localStorage，不勾选到 sessionStorage
- [ ] 令牌过期前 5 分钟自动刷新
- [ ] 「忘记密码」「立即入驻」链接跳转正确
- **性能要求**：登录页首屏加载 < 1s；登录响应 < 1.5s；双因子验证响应 < 1s。
- **可访问性**：表单字段有 `label` 与 `aria-label`；按钮有 `aria-label`；密码输入框有 `aria-label="密码"`；对比度满足 WCAG AA；支持 Tab 键导航。
