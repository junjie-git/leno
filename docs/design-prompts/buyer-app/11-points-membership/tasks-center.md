# 任务中心 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：11-points-membership 积分会员
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看任务列表（每日任务/新手任务/周期任务），按完成状态领取积分奖励，激励用户活跃行为。
- **访问入口**：积分账户页「任务中心」入口；首页任务提醒；「我的」页任务入口。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「任务中心」）+ `van-tabs` 任务类型（每日任务/新手任务/周期任务）+ `van-list` 任务卡片列表，无 Tabbar。
- **关键区域**：
  - 区域 A（任务类型 Tab）：`van-tabs` 3 个标签，切换后展示对应类型任务。
  - 区域 B（任务卡片）：每张卡片展示任务图标 + 任务名称 + 任务描述 + 积分奖励数 + 进度（如 3/5）+ 状态相关按钮（去完成/待领取/已完成）。
  - 区域 C（空状态）：`van-empty`「暂无任务」+ 「去逛逛」CTA。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、任务类型 Tab、任务卡片首屏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   任务中心      │
├──────────────────┤
│每日任务 新手 周期 │
├──────────────────┤
│ 🛒 完成首单       │
│ 下单即可获得积分  │
│ 奖励 +100  已完成 │
├──────────────────┤
│ 📅 每日签到       │
│ 每日签到得积分    │
│ 奖励 +5   待领取  │
├──────────────────┤
│ 👀 浏览5个商品    │
│ 浏览商品满5个     │
│ 奖励 +10  3/5     │
│         去完成    │
├──────────────────┤
│ ⭐ 收藏3个商品    │
│ 收藏商品满3个     │
│ 奖励 +15  1/3     │
│         去完成    │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/points/tasks` | 获取任务列表（含当前用户完成状态） | Buyer |
| POST | `/api/points/tasks/{taskId}/complete` | 完成任务领取积分奖励 | Buyer |

- **请求参数**：任务列表无参数（服务端按用户返回所有类型任务）；完成任务路径参数 `taskId`。
- **响应字段**：任务列表 `TaskDto` 含 `taskId`、`name`、`description`、`icon`、`rewardPoints`、`type`（Daily/Newbie/Periodic）、`progress`（当前进度）、`target`（目标进度）、`status`（InProgress/Claimable/Completed）、`actionUrl`（去完成跳转路径）；完成结果 `TaskCompleteResultDto` 含 `awardedPoints`、`newBalance`。
- **数据加载策略**：进入页面调 `GET /api/points/tasks` 渲染全部任务；前端按类型 Tab 过滤；下拉刷新。
- **缓存策略**：不缓存，每次进入页面重新拉取。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认「每日任务」Tab → `GET /api/points/tasks` → 前端过滤每日任务 → 渲染任务卡片。
  2. 切换任务类型 Tab → 前端过滤对应类型任务。
  3. 状态 `InProgress` 点击「去完成」→ 跳转 `actionUrl` 对应页面（如商品列表、签到页）。
  4. 状态 `Claimable` 点击「待领取」→ 按钮 disabled + loading → `POST /api/points/tasks/{taskId}/complete` → 成功 `showToast` 「领取成功，获得 X 积分」→ 更新任务状态为 `Completed`。
  5. 状态 `Completed` 按钮置灰显示「已完成」。
  6. 完成任务后返回本页 → 下拉刷新更新任务进度。
- **分支流程**：
  - 空列表：`van-empty`「暂无任务」+ 「去逛逛」CTA 跳首页。
  - 领取失败（任务未完成）：`showToast` 「任务未完成」+ 刷新状态。
  - 领取失败（已领取）：`showToast` 「已领取过」+ 刷新状态。
- **跨页面流转**：「去完成」跳转对应页面（商品列表/签到页/收藏页等）；领取成功留在本页。
- **状态机可视化**：InProgress（去完成）→ Claimable（待领取）→ Completed（已完成）。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-tabs`、`van-tab`、`van-list`、`van-pull-refresh`、`van-cell`、`van-button`、`van-tag`、`van-icon`、`van-image`、`van-empty`、`van-skeleton`、`van-toast`（showToast）、`van-progress`。
- **业务组件**：`TaskCard` 任务卡片（含图标、名称、描述、奖励、进度、状态按钮）；`TaskStatusTag` 任务状态标签；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无（进度用 `van-progress`）。
- **图标使用**：返回 `arrow-left`；订单 `shopping-cart-o`；签到 `calendar-o`；浏览 `eye-o`；收藏 `star-o`；分享 `share-o`。
- **空状态**：`van-empty`「暂无任务」+ 「去逛逛」CTA 跳首页。

## 6. 视觉规范
- **主色应用**：奖励积分数主色 `#1677FF`；「去完成」「待领取」按钮主色；Tab 激活态主色；进度条主色。
- **状态色**：进行中 `#FAAD14`；待领取 `#1677FF`；已完成 `#52C41A`；已完成按钮 `#8C8C8C` 背景。
- **间距**：卡片间距 12px；卡片内边距 12px；按钮内边距 8px 16px。
- **字体**：任务名称 15px medium `#000000D9`；任务描述 12px `#8C8C8C`；奖励积分数 14px semibold `#1677FF`；进度文字 12px `#8C8C8C`；按钮 13px。
- **图标尺寸**：返回 20px；任务图标 32px；状态图标 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 4 张任务卡片。
- **空数据**：`van-empty`「暂无任务」+ 「去逛逛」CTA 跳首页。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/points/tasks`。
- **并发与乐观锁**：领取按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复领取。
- **危险操作确认**：不涉及。

## 8. 验收要点
- [ ] 任务类型 Tab 切换后列表正确过滤（每日/新手/周期）。
- [ ] 任务卡片展示图标、名称、描述、奖励、进度、状态按钮。
- [ ] 进行中任务点击「去完成」跳转对应页面。
- [ ] 待领取任务点击「待领取」领取积分，成功后更新为已完成。
- [ ] 已完成任务按钮置灰显示「已完成」。
- [ ] 领取防重复（按钮 loading + Idempotency-Key）。
- [ ] 空列表展示「去逛逛」CTA。
- **性能要求**：首屏 < 1s；列表渲染无卡顿；领取响应 < 1.5s。
- **可访问性**：Tab `role="tab"`；卡片 `role="article"`；进度 `role="progressbar"`；按钮 `aria-label`。
