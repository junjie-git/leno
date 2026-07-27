# OAuth 登录 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：01-auth 认证
- **页面类型**：流程页
- **目标用户**：买家（Buyer）
- **核心目标**：买家通过第三方账号（微信/支付宝等）授权登录或绑定外部登录，完成 OAuth2 授权码交换并签发 JWT。
- **访问入口**：登录页第三方图标按钮；个人中心「账号安全」绑定外部账号。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：NavBar（返回+标题「第三方登录」/「绑定{provider}」）+ 中部加载状态区 + 底部说明文案，无 Tabbar。本页主要承载授权跳转与回调处理，内容简洁。
- **关键区域**：
  - 区域 A（NavBar）：返回箭头 + 标题。
  - 区域 B（加载区）：`van-loading` 居中 + 文案「正在跳转至{provider}...」或「正在处理授权...」。
  - 区域 C（错误区，异常时）：`van-empty` 错误插画 + 错误描述 + 「重试」按钮。
  - 区域 D（说明区）：底部 12px 灰字「授权后将使用第三方账号登录 Leno」。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：加载动画与跳转提示。
- **线框图描述**：
```
┌──────────────────┐
│ ← 第三方登录     │
├──────────────────┤
│                  │
│      ◌ 加载      │
│  正在跳转至微信  │
│                  │
│  [失败时：重试]  │
│                  │
│ 授权后将使用第三方│
│ 账号登录 Leno    │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（旧域 UserAuth 双轨兜底，端点路径不变）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/auth/oauth/{provider}/login` | 获取第三方授权 URL | 匿名 |
| GET | `/api/auth/oauth/{provider}/callback` | 第三方回调，code 换 token | 匿名 |
| POST | `/api/account/external-logins` | 绑定外部登录（已登录态） | Buyer |
| DELETE | `/api/account/external-logins/{provider}` | 解绑外部登录 | Buyer |

- **请求参数**：`/login` 需 `provider`（wechat/alipay）、`redirectUri`；`/callback` 需 `provider`、`code`、`state`、`redirectUri`；绑定需 `BindExternalLoginDto`（含 provider、code、state）。
- **响应字段**：`/login` 返回 `OAuthLoginResponseDto` 含 `authorizationUrl`；`/callback` 返回 `TokenDto`；绑定/解绑返回 `ApiResponse`。
- **数据加载策略**：进入页面立即调用 `/login` 获取授权 URL 并跳转；回调 URL 被唤起后读取 `code`/`state` 调用 `/callback`。
- **缓存策略**：`state` 暂存 sessionStorage 用于回调校验；token 持久化规则同登录。

## 4. 交互流程
- **主流程**：
  1. 用户在登录页点击第三方图标 → 跳 `/oauth/:provider` → 页面 loading → 调用 `GET /api/auth/oauth/{provider}/login?redirectUri={callbackUrl}`。
  2. 拿到 `authorizationUrl` → `window.location.href` 跳转第三方授权页。
  3. 用户授权后第三方回调至 `redirectUri`（PWA 场景为 App 内路由 `/oauth/:provider/callback`）→ 读取 `code`/`state`。
  4. 调用 `GET /api/auth/oauth/{provider}/callback?code=...&state=...` → 返回 `TokenDto` → 存 token → `showToast('登录成功')` → 跳 `/`。
- **分支流程**：
  - 用户取消授权：回调携带 `error=access_denied` → `showToast('已取消授权')` → 跳 `/login`。
  - state 不匹配：提示「授权状态异常，请重试」→ 跳 `/login`。
  - 已登录态绑定：从「账号安全」进入，回调成功后调用 `POST /api/account/external-logins` → `showToast('绑定成功')` → 返回安全页。
- **跨页面流转**：登录成功跳首页或 redirect；绑定成功返回上一页。
- **状态机可视化**：初始化 → 跳转中 → 第三方授权页（外部）→ 回调处理中 → 已登录/已绑定 / 失败重试。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-loading`、`van-empty`、`van-button`、`van-toast`。
- **业务组件**：无额外业务组件。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；错误插画使用 Vant 内置 `error`。
- **空状态**：错误态使用 `van-empty image="error"`。

## 6. 视觉规范
- **主色应用**：loading 圆环主色 `#1677FF`；重试按钮主色。
- **状态色**：错误态描述 `#FF4D4F`；说明文案 `#8C8C8C`。
- **间距**：loading 距顶 30%；说明文案距底 24px。
- **字体**：标题 16px medium；加载文案 14px `#595959`；说明 12px `#8C8C8C`。
- **图标尺寸**：loading 32px；返回 20px。

## 7. 异常处理与边界
- **加载态**：全屏 `van-loading`，跳转与回调期间禁止操作。
- **空数据**：不涉及。
- **错误态**：获取授权 URL 失败 → `van-empty error` + 重试按钮；回调失败 → toast 错误信息 + 跳登录。
- **权限控制**：登录场景匿名；绑定场景需 Buyer 已登录，路由守卫拦截。
- **并发与乐观锁**：不涉及重复提交（跳转由浏览器接管）。
- **危险操作确认**：不涉及。

## 8. 验收要点
- [ ] 进入页面立即获取授权 URL 并跳转第三方。
- [ ] 回调正确读取 code/state 并交换 token。
- [ ] state 不匹配时拦截并提示。
- [ ] 用户取消授权时友好提示并跳登录。
- [ ] 绑定场景下已登录态调用绑定端点。
- **性能要求**：授权 URL 获取 < 800ms；回调处理 < 1s。
- **可访问性**：loading 有 `aria-live="polite"`；重试按钮可键盘聚焦。
