# 设置 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：13-profile 我的
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家管理应用偏好（语言/深色模式/字体大小/消息推送）、隐私设置（个性化推荐/浏览历史记录）、清除缓存、查看关于信息（版本号/用户协议/隐私政策/检查更新）、退出登录。
- **访问入口**：「我的」页 → 设置；URL `/settings`。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「设置」）+ 可滚动主体（通用设置组 + 隐私设置组 + 缓存与存储组 + 关于组 + 退出登录按钮），无 Tabbar。
- **关键区域**：
  - 区域 A（通用设置组）：`van-cell-group` 含「深色模式」（`van-switch` 或三态选择）、「字体大小」（小/标准/大 三档滑块）、「语言」（简体中文/English）、「消息推送」（`van-switch`）。
  - 区域 B（隐私设置组）：`van-cell-group` 含「个性化推荐」（`van-switch`）、「浏览历史记录」（`van-switch`，关闭后不写入历史）、「广告个性化」（`van-switch`）。
  - 区域 C（缓存与存储组）：`van-cell-group` 含「清除缓存」（右侧显示缓存大小，点击清除）、「离线包管理」（跳转离线资源管理页）。
  - 区域 D（关于组）：`van-cell-group` 含「关于 Leno」（跳关于页）、「版本号」（右侧显示当前版本如 v1.0.0）、「检查更新」（点击检查新版本）、「用户协议」（跳协议页）、「隐私政策」（跳政策页）。
  - 区域 E（退出登录）：底部固定「退出登录」红色文字按钮，适配 `safe-area-inset-bottom`。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、通用设置组、隐私设置组、缓存与存储组首屏可见。
- **线框图描述**：
```
┌──────────────────┐
│ ←     设置        │
├──────────────────┤
│ 通用              │
│ 深色模式    [开]  │
│ 字体大小  标准 >  │
│ 语言    简体中文 >│
│ 消息推送    [开]  │
├──────────────────┤
│ 隐私              │
│ 个性化推荐  [开]  │
│ 浏览历史记录[开] │
│ 广告个性化  [关]  │
├──────────────────┤
│ 存储              │
│ 清除缓存  12.3MB>│
│ 离线包管理      >│
├──────────────────┤
│ 关于              │
│ 关于 Leno        >│
│ 版本号    v1.0.0 │
│ 检查更新        >│
│ 用户协议        >│
│ 隐私政策        >│
├──────────────────┤
│    退出登录       │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：Identity 域（`/api/auth/logout`）+ UserCenter 域（`/api/users/me/notification-preferences` HTTP 端点）；旧域 UserAuth 双轨兜底，端点路径不变；通知偏好内部协作由 Notification 域承接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/auth/logout` | 退出登录，吊销当前 JWT | Buyer |
| GET | `/api/users/me/notification-preferences` | 查询消息推送偏好（消息推送开关联动） | Buyer |
| PUT | `/api/users/me/notification-preferences` | 同步消息推送偏好到服务端 | Buyer |

- **请求参数**：退出登录无 body；通知偏好查询无参数；通知偏好设置 body `{ eventType, channel, enabled }`（消息推送开关映射到 InApp 渠道全局启用/禁用）。
- **响应字段**：退出登录返回 `ApiResponse`；通知偏好返回 `NotificationPreferenceDto`（详见 12-notification/preferences.md）。
- **数据加载策略**：进入页面读取本地 `localStorage` 渲染深色模式/字体大小/语言/隐私开关；消息推送开关调 `GET /api/users/me/notification-preferences` 同步状态；缓存大小由客户端计算（图片缓存 + API 缓存 + 离线包）。
- **缓存策略**：所有偏好缓存于 `localStorage`（持久化）+ Pinia `useSettingsStore`（运行时）；退出登录不清除偏好设置（仅清除令牌）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 读 `localStorage` 渲染深色模式/字体大小/语言/隐私开关 + 调 `GET /api/users/me/notification-preferences` 同步消息推送状态 + 计算缓存大小。
  2. 切换「深色模式」→ `van-switch` change → 立即写入 `localStorage` + `useSettingsStore` → 通过 Vant `ConfigProvider` 切换主题（无需刷新）。
  3. 点击「字体大小」→ `van-action-sheet` 弹出三档选择（小/标准/大）→ 选中后写入 `localStorage` + 应用根字号 rem 比例。
  4. 点击「语言」→ `van-action-sheet` 弹出语言选择 → 选中后写入 `localStorage` + 切换 i18n locale → `showToast` 「语言将在下次启动生效」。
  5. 切换「消息推送」→ `van-switch` change → `PUT /api/users/me/notification-preferences`（乐观更新）→ 失败回滚 + `showToast` 「保存失败」。
  6. 切换隐私开关（个性化推荐/浏览历史记录/广告个性化）→ `van-switch` change → 写入 `localStorage` + `useSettingsStore` → 立即生效（推荐流与历史写入逻辑读取该配置）。
  7. 点击「清除缓存」→ `showConfirmDialog` 二次确认 → 清除图片缓存 + API 缓存 + 临时文件（保留离线包与登录态）→ `showToast` 「已清除 X MB」→ 更新缓存大小显示。
  8. 点击「离线包管理」→ 跳离线资源管理页（展示已下载的离线包大小与清除入口）。
  9. 点击「关于 Leno」→ 跳关于页（公司介绍、联系方式）。
  10. 点击「检查更新」→ 调用 PWA `registration.update()` + 比对服务端版本 → 有更新 `showConfirmDialog` 提示更新 → 无更新 `showToast` 「已是最新版本」。
  11. 点击「用户协议」/「隐私政策」→ 跳对应协议页（HTML 渲染）。
  12. 点击「退出登录」→ `showConfirmDialog` 二次确认（危险操作）→ `POST /api/auth/logout` → 成功清除 `useAuthStore` 令牌与用户信息 → 跳登录页。
- **分支流程**：
  - 退出登录失败：`showToast` 「退出失败」+ 重试（客户端仍可强制清令牌跳登录）。
  - 检查更新失败：`showToast` 「检查失败，请稍后重试」。
  - 清除缓存失败：`showToast` 「清除失败」+ 重试。
  - 消息推送关闭后：服务端不再推送，已存站内信仍可在消息列表查看。
- **跨页面流转**：跳关于页、离线包管理页、用户协议页、隐私政策页；退出登录跳登录页。
- **状态机可视化**：偏好关闭 →（切换）→ 偏好开启；登录态 →（退出确认）→ 退出中(loading) → 已退出(跳登录)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-cell`、`van-cell-group`、`van-switch`、`van-button`、`van-action-sheet`、`van-dialog`（showConfirmDialog）、`van-toast`（showToast）、`van-icon`、`van-tag`（版本号）。
- **业务组件**：`SettingsGroup` 设置分组容器（含标题 + 列表）；`SettingsSwitchCell` 开关项（含图标、标题、副标题、`van-switch`）；`SettingsArrowCell` 箭头项（含图标、标题、右侧值、箭头）；`FontSizeActionSheet` 字体大小选择面板；`LanguageActionSheet` 语言选择面板；`LogoutButton` 退出登录按钮（红色文字 + 二次确认）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；深色模式 `bulb-o`；字体 `font`；语言 `language-o`；推送 `bell`；隐私 `shield-o`；缓存 `delete-o`；离线包 `downloaded`；关于 `info-o`；版本 `bookmark-o`；更新 `refresh`；协议 `description`；政策 `lock`；箭头 `arrow`。
- **空状态**：不涉及（首屏必有默认偏好）。

## 6. 视觉规范
- **主色应用**：开关激活态主色 `#1677FF`；箭头项图标主色；分组标题主色 `#1677FF`。
- **状态色**：开关开启 `#1677FF`；开关关闭 `#8C8C8C`；退出登录按钮红 `#FF4D4F`；缓存大小 `#8C8C8C`。
- **间距**：分组间距 12px；分组内边距 0（`van-cell-group` 默认）；列表项高 48px；退出登录按钮高 50px；底部 `safe-area-inset-bottom`。
- **字体**：分组标题 13px medium `#8C8C8C`；列表项标题 14px `#000000D9`；列表项右侧值 14px `#8C8C8C`；版本号 14px `#8C8C8C`；退出登录按钮 16px medium `#FF4D4F`。
- **图标尺寸**：返回 20px；列表项图标 20px；箭头 16px。

## 7. 异常处理与边界
- **加载态**：首屏无网络加载（本地渲染）；消息推送开关调接口时显示 `van-switch` loading 态。
- **空数据**：不涉及（首屏必有默认偏好）。
- **错误态**：消息推送查询失败 `showToast` 「加载偏好失败」（开关保持上次状态）；退出登录失败 `showToast` 「退出失败」+ 重试；检查更新失败 `showToast` 「检查失败」；清除缓存失败 `showToast` 「清除失败」+ 重试。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/settings`；退出登录后所有受保护路由跳登录页。
- **并发与乐观锁**：所有开关切换乐观更新（立即生效），失败回滚 + `showToast`；退出登录按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复退出；清除缓存按钮 disabled + loading。
- **危险操作确认**：
  - 清除缓存：`showConfirmDialog` 标题「确认清除缓存」、内容「将清除图片与 API 缓存，登录态与离线包保留。」、确认按钮主色。
  - 退出登录：`showConfirmDialog` 标题「确认退出登录」、内容「退出后需重新登录才能继续使用，本地偏好设置保留。」、确认按钮红色 `#FF4D4F`。

## 8. 验收要点
- [ ] 通用设置组含深色模式、字体大小、语言、消息推送。
- [ ] 深色模式切换立即生效（ConfigProvider 主题切换）。
- [ ] 字体大小三档选择（小/标准/大），写入 `localStorage` 生效。
- [ ] 消息推送开关调 `/api/users/me/notification-preferences` 同步，失败回滚。
- [ ] 隐私设置组含个性化推荐、浏览历史记录、广告个性化开关。
- [ ] 隐私开关写入 `localStorage` 立即生效，推荐流与历史写入逻辑读取该配置。
- [ ] 清除缓存需二次确认，确认后清除图片 + API 缓存，保留登录态与离线包。
- [ ] 缓存大小显示正确，清除后更新。
- [ ] 关于组含关于 Leno、版本号、检查更新、用户协议、隐私政策。
- [ ] 检查更新支持 PWA `registration.update()` 与版本比对。
- [ ] 退出登录需二次确认，确认按钮红色危险色。
- [ ] 退出登录成功清除令牌跳登录页，本地偏好保留。
- [ ] 操作防重复（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 800ms（本地渲染）；开关切换响应 < 200ms；退出登录响应 < 1s。
- **可访问性**：开关 `role="switch" aria-checked="true"`；列表项 `role="listitem"`；按钮 `aria-label`；分组 `role="group"`。
