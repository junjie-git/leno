# 通知列表 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：12-notification 通知
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看站内信通知列表（订单/促销/系统/积分），支持按已读状态筛选、标记已读、全部已读，点击跳转对应业务页面。
- **访问入口**：「我的」页消息入口；首页右上角消息图标；Tabbar 红点提示。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「消息」+ 右侧「全部已读」）+ `van-tabs` 状态筛选（全部/未读/已读）+ `van-list` 通知卡片无限滚动，无 Tabbar。
- **关键区域**：
  - 区域 A（状态筛选 Tab）：`van-tabs` 3 个标签（全部/未读/已读），切换后重新加载对应状态通知。
  - 区域 B（通知卡片）：每张卡片左侧类型图标 + 右侧标题 + 摘要 + 时间；未读通知左侧红点 + 标题加粗；已读通知灰色。点击卡片标记已读并跳转业务页。
  - 区域 C（空状态）：`van-empty`「暂无消息」+ 「去逛逛」CTA。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、状态筛选 Tab、通知卡片首屏。
- **线框图描述**：
```
┌──────────────────┐
│ ←  消息  全部已读  │
├──────────────────┤
│全部 未读 已读     │
├──────────────────┤
│ ●🛒 订单已发货    │
│   您的订单已发出  │
│   07-26 10:00    │
├──────────────────┤
│ ●🎁 优惠券到账    │
│   满100减10券已   │
│   07-26 09:00    │
├──────────────────┤
│  💰 积分到账      │
│   购物获得120积分 │
│   07-25 10:00    │
├──────────────────┤
│  🔔 系统通知      │
│   平台维护通知    │
│   07-24 18:00    │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/notifications` | 分页查询我的站内信 | Buyer |
| GET | `/api/notifications/unread-count` | 获取未读计数 | Buyer |
| POST | `/api/notifications/read` | 批量标记已读 | Buyer |
| POST | `/api/notifications/read-all` | 全部标记已读 | Buyer |

- **请求参数**：`GET /api/notifications?isRead={bool}&page={page}&pageSize=20`；isRead 可空表示全部；标记已读 body `{ recordIds: [...] }`。
- **响应字段**：列表 `NotificationListResultDto` 含 `items`、`total`、`page`、`pageSize`；item 含 `recordId`、`title`、`content`、`type`（Order/Promotion/Points/System）、`isRead`、`actionUrl`（跳转路径）、`createdAt`；未读计数 `int`。
- **数据加载策略**：`van-list` 无限滚动，每页 20 条；切换 Tab 重置列表；下拉刷新。
- **缓存策略**：不缓存，每次进入页面重新拉取；未读计数在 Tabbar 红点展示，全局缓存 30s。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认「全部」Tab → `GET /api/notifications?page=1` → 渲染通知卡片。
  2. 切换状态 Tab → 重置列表 → `GET /api/notifications?isRead={bool}&page=1`。
  3. 滚动到底部 → `van-list` load → 追加下一页。
  4. 点击未读通知卡片 → `POST /api/notifications/read`（带 `recordIds`）→ 标记已读（移除红点，标题变常规）→ 跳转 `actionUrl` 对应业务页。
  5. 点击「全部已读」→ `POST /api/notifications/read-all` → 成功 `showToast` 「全部已读」→ 刷新列表。
  6. 点击已读通知卡片 → 直接跳转 `actionUrl`。
- **分支流程**：
  - 空列表：`van-empty`「暂无消息」+ 「去逛逛」CTA 跳首页。
  - 接口失败：`showToast` 「加载失败」+ 重试按钮。
  - 无 actionUrl：仅标记已读，不跳转。
- **跨页面流转**：跳转订单详情/优惠券/积分账户/公告详情等业务页。
- **状态机可视化**：未读（红点+加粗）→ 已读（无红点+常规）。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-tabs`、`van-tab`、`van-list`、`van-pull-refresh`、`van-cell`、`van-badge`（红点）、`van-icon`、`van-empty`、`van-skeleton`、`van-toast`（showToast）。
- **业务组件**：`NotificationCard` 通知卡片（含类型图标、标题、摘要、时间、未读红点）；`NotificationTypeIcon` 类型图标映射；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；订单 `shopping-cart-o`；促销 `gift-o`；积分 `gold-coin-o`；系统 `bell`；全部已读 `success`。
- **空状态**：`van-empty`「暂无消息」+ 「去逛逛」CTA 跳首页。

## 6. 视觉规范
- **主色应用**：「全部已读」按钮主色 `#1677FF`；Tab 激活态主色；未读红点 `#FF4D4F`。
- **状态色**：未读标题 `#000000D9` 加粗；已读标题 `#8C8C8C`；未读红点 `#FF4D4F`。
- **间距**：卡片间距 8px；卡片内边距 12px；图标 32×32px。
- **字体**：通知标题 14px medium（未读加粗）；通知摘要 13px `#595959`（1 行省略）；时间 12px `#8C8C8C`。
- **图标尺寸**：返回 20px；类型图标 20px；红点 8px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 5 张通知卡片。
- **空数据**：`van-empty`「暂无消息」+ 「去逛逛」CTA 跳首页。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/notifications`。
- **并发与乐观锁**：标记已读乐观更新（立即移除红点），失败回滚；全部已读按钮点击后立即 disabled + loading。
- **危险操作确认**：不涉及。

## 8. 验收要点
- [ ] 状态 Tab 切换后列表正确筛选（全部/未读/已读）。
- [ ] 通知卡片展示类型图标、标题、摘要、时间、未读红点。
- [ ] 未读通知标题加粗 + 红点，已读通知灰色。
- [ ] 点击未读通知标记已读并跳转业务页。
- [ ] 「全部已读」按钮成功后刷新列表。
- [ ] 列表无限滚动加载下一页。
- [ ] 空列表展示「去逛逛」CTA。
- **性能要求**：首屏 < 1s；列表无限滚动无卡顿；分页 pageSize=20；标记已读乐观更新。
- **可访问性**：Tab `role="tab"`；卡片 `role="article"`；红点 `aria-label="未读"`；按钮 `aria-label`。
