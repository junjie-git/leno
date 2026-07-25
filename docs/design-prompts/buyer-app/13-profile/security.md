# 账号安全 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：13-profile 我的
- **页面类型**：列表页 + 表单弹层组合
- **目标用户**：买家（Buyer）
- **核心目标**：买家管理账号安全，包括修改登录密码、启用/禁用双因子认证、绑定/解绑外部登录（微信/支付宝/Google），查看账号风险状态。
- **访问入口**：「我的」页 → 账号安全；个人资料页「账号安全」；URL `/profile/security`。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「账号安全」）+ 可滚动主体（账号保护级别卡 + 安全项列表 + 外部登录绑定区），无 Tabbar。
- **关键区域**：
  - 区域 A（账号保护级别卡）：渐变背景卡，展示保护级别（低/中/高）+ 完成度进度条（如 3/5 项已启用）+ 提升建议文案。
  - 区域 B（安全项列表）：`van-cell-group` 列出「登录密码」「双因子认证」「绑定手机」「绑定邮箱」，每项右侧状态标签（已设置/未设置/已启用/未启用）+ 箭头。
  - 区域 C（外部登录绑定区）：`van-cell-group` 列出「微信」「支付宝」「Google」三个第三方登录，每项右侧「绑定/解绑」按钮或「已绑定」标签。
  - 区域 D（密码修改弹层）：`van-popup` 自底部弹出，含旧密码、新密码、确认新密码、密码强度提示。
  - 区域 E（双因子弹层）：`van-popup` 展示 QR 码与密钥 + TOTP 验证码输入框。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、保护级别卡、安全项列表、外部登录绑定区。
- **线框图描述**：
```
┌──────────────────┐
│ ←   账号安全      │
├──────────────────┐
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓  │
│ ▓ 保护级别：中   │ │
│ ▓ 已完成 3/5 项  │ │
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓  │
├──────────────────┤
│ 登录密码   已设置>│
│ 双因子认证 未启用>│
│ 绑定手机 138****>│
│ 绑定邮箱  未绑定>│
├──────────────────┤
│ 第三方账号        │
│ 微信     [绑定]   │
│ 支付宝   [已绑定] │
│ Google   [绑定]   │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/users/me` | 查询当前用户资料（含邮箱、手机号、双因子状态） | Buyer |
| PUT | `/api/users/me/password` | 修改当前用户密码 | Buyer |
| POST | `/api/users/me/two-factor/enable` | 启用双因子，生成密钥与 QR 码 | Buyer |
| POST | `/api/users/me/two-factor/confirm` | 确认启用双因子，验证 TOTP 码 | Buyer |
| POST | `/api/users/me/two-factor/disable` | 禁用双因子认证 | Buyer |
| POST | `/api/account/external-logins` | 绑定外部登录 | Buyer |
| DELETE | `/api/account/external-logins/{provider}` | 解绑外部登录 | Buyer |

- **请求参数**：
  - 修改密码 body `{ oldPassword, newPassword }`，newPassword 8-64 位含字母与数字。
  - 启用双因子无 body，返回 `TwoFactorEnableResponseDto`（含 `qrCodeUri`、`manualKey`）。
  - 确认双因子 body `{ code: string }`（6 位 TOTP）。
  - 禁用双因子无 body。
  - 绑定外部登录 body `{ provider, code, redirectUri }`。
  - 解绑外部登录 path 参数 `{provider}`（Wechat/Alipay/Google）。
- **响应字段**：`UserDto` 含 `email?`、`phoneNumber?`、`twoFactorEnabled`（推断字段，由后端扩展）；密码修改返回 `ApiResponse`；双因子启用返回 `TwoFactorEnableResponseDto`；其他返回 `ApiResponse`。
- **数据加载策略**：进入页面调 `GET /api/users/me` 渲染安全项状态与外部登录绑定状态。
- **缓存策略**：用户资料缓存于 Pinia `useAuthStore`，安全页不缓存每次进入重新拉取。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → `GET /api/users/me` → 渲染保护级别卡、安全项列表、外部登录绑定区。
  2. 点击「登录密码」→ `van-popup` 弹出密码修改表单 → 输入旧密码、新密码、确认新密码 → 实时密码强度提示 → 点击「确认修改」→ `showConfirmDialog` 二次确认 → `PUT /api/users/me/password` → 成功 `showToast` 「密码修改成功，请重新登录」→ 清除令牌跳登录页。
  3. 点击「双因子认证」（未启用）→ `POST /api/users/me/two-factor/enable` → 弹层展示 QR 码与密钥 → 用户用 Authenticator App 扫码 → 输入 6 位 TOTP → 点击「确认启用」→ `POST /api/users/me/two-factor/confirm` → 成功 `showToast` 「双因子已启用」→ 关闭弹层 + 刷新状态。
  4. 点击「双因子认证」（已启用）→ `showConfirmDialog` 二次确认（危险操作）→ `POST /api/users/me/two-factor/disable` → 成功 `showToast` 「双因子已禁用」→ 刷新状态。
  5. 点击「绑定手机/邮箱」（未绑定）→ 跳绑定流程页（验证码校验后写入）。
  6. 点击第三方「绑定」→ 跳 OAuth 授权页 → 回调获得 `code` → `POST /api/account/external-logins` → 成功 `showToast` 「绑定成功」→ 刷新状态。
  7. 点击第三方「解绑」→ `showConfirmDialog` 二次确认 → `DELETE /api/account/external-logins/{provider}` → 成功 `showToast` 「解绑成功」→ 刷新状态。
- **分支流程**：
  - 旧密码错误：`showToast` 「旧密码错误」+ 旧密码框清空聚焦。
  - 新密码与确认不一致：确认框红色提示「两次输入的密码不一致」。
  - 密码强度弱：新密码框下方黄色提示「建议包含大小写字母与数字」。
  - TOTP 验证码错误：`showToast` 「验证码错误或已过期」+ 验证码框清空聚焦。
  - 至少保留一种登录方式：解绑外部登录时若用户无密码且无其他绑定，拒绝解绑并提示「至少保留一种登录方式」。
- **跨页面流转**：修改密码成功跳登录页；其他操作停留在本页。
- **状态机可视化**：双因子未启用 →（启用，扫码+验证）→ 已启用；已启用 →（禁用，二次确认）→ 未启用。外部登录未绑定 →（OAuth 授权）→ 已绑定；已绑定 →（解绑，二次确认）→ 未绑定。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-cell`、`van-cell-group`、`van-button`、`van-popup`、`van-field`、`van-image`（QR 码）、`van-progress`（保护级别 + 密码强度）、`van-tag`、`van-dialog`（showConfirmDialog）、`van-skeleton`、`van-toast`（showToast）、`van-icon`、`van-pull-refresh`。
- **业务组件**：`SecurityLevelCard` 保护级别卡（渐变背景 + 级别 + 进度条 + 建议）；`SecurityItemCell` 安全项（含图标、标题、状态标签、箭头）；`ExternalLoginCell` 第三方登录项（含图标、标题、绑定/解绑按钮）；`PasswordChangePopup` 密码修改弹层（含密码强度）；`TwoFactorSetupPopup` 双因子启用弹层（含 QR、密钥、TOTP 输入）。
- **图表组件**：无（QR 码用 `van-image` 展示后端返回的 URI 生成的图片）。
- **图标使用**：返回 `arrow-left`；密码 `lock`；双因子 `shield-o`；手机 `phone-o`；邮箱 `envelop-o`；微信 `chat-o`；支付宝 `gold-coin-o`；Google `desktop-o`；箭头 `arrow`。
- **空状态**：不涉及（首屏必有用户资料）。

## 6. 视觉规范
- **主色应用**：保护级别卡渐变 `linear-gradient(135deg, #1677FF, #0958D9)`；已设置/已启用状态主色 `#1677FF`；绑定按钮主色。
- **状态色**：保护级别低红 `#FF4D4F`、中黄 `#FAAD14`、高绿 `#52C41A`；已设置/已启用绿 `#52C41A`；未设置/未启用灰 `#8C8C8C`；解绑按钮红 `#FF4D4F`；密码强度弱红、中黄、强绿。
- **间距**：保护级别卡内边距 16px；安全项高 56px；第三方登录项高 56px；模块间距 12px。
- **字体**：保护级别卡标题 16px medium `#FFFFFF`；级别数值 24px semibold `#FFFFFF`；进度条标签 12px `rgba(255,255,255,0.8)`；安全项标题 14px `#000000D9`；状态标签 12px；第三方登录标题 14px `#000000D9`。
- **图标尺寸**：返回 20px；安全项图标 20px；第三方登录图标 20px；箭头 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟保护级别卡 + 安全项列表 + 第三方登录区。
- **空数据**：不涉及（已登录用户必有资料）；未绑定项显示「未绑定」灰色标签。
- **错误态**：查询失败 `showToast` 「加载失败」+ 重试按钮；密码/TOTP/绑定操作失败 `showToast` 错误信息；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/profile/security`。
- **并发与乐观锁**：所有操作按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复提交；双因子操作后端校验当前状态防止并发冲突。
- **危险操作确认**：
  - 修改密码：`showConfirmDialog` 标题「确认修改密码」、内容「修改密码后将退出当前登录，需使用新密码重新登录。」、确认按钮主色。
  - 禁用双因子：`showConfirmDialog` 标题「确认禁用双因子」、内容「禁用后账号安全性将降低，建议保持启用。此操作可逆。」、确认按钮红色 `#FF4D4F`。
  - 解绑外部登录：`showConfirmDialog` 标题「确认解绑」、内容「解绑后将无法使用该方式登录，此操作可逆。」、确认按钮红色 `#FF4D4F`。

## 8. 验收要点
- [ ] 保护级别卡展示级别（低/中/高）、完成度进度条、提升建议。
- [ ] 安全项列表含登录密码、双因子认证、绑定手机、绑定邮箱，状态正确。
- [ ] 密码修改弹层含旧密码、新密码、确认新密码、实时密码强度。
- [ ] 新密码校验 8-64 位含字母与数字，两次一致校验。
- [ ] 修改密码成功后清除令牌跳登录页。
- [ ] 双因子启用流程含 QR 码展示与 TOTP 验证。
- [ ] 禁用双因子需二次确认。
- [ ] 第三方登录展示绑定状态，绑定/解绑流程正常。
- [ ] 至少保留一种登录方式（解绑最后一个外部登录时拒绝）。
- [ ] 操作防重复（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 1s；密码修改响应 < 1s；双因子启用响应 < 1.5s。
- **可访问性**：表单字段 `label` 与 `aria-label`；状态标签 `aria-label`；弹层 `role="dialog"`；进度条 `aria-valuenow`。
