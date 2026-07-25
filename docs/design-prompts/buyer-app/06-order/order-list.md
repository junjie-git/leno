# 订单列表 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：06-order 订单交易
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家分页查看全部订单，按状态（全部/待支付/待发货/待收货/已完成）筛选，快速进入订单详情、支付、确认收货或查看物流。
- **访问入口**：底部 Tabbar「我的」→ 订单聚合入口；首页快捷入口待支付/待收货角标；订单操作后返回刷新。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「我的订单」）+ `van-tabs` 状态筛选（全部/待支付/待发货/待收货/已完成）+ `van-list` 无限滚动订单卡片列表，无 Tabbar。
- **关键区域**：
  - 区域 A（状态筛选 Tab）：`van-tabs` 5 个标签，切换后重新加载对应状态订单，角标显示待支付/待收货数量。
  - 区域 B（订单卡片）：每张 `van-card` 展示订单号+状态标签+商品图列+商品总额+操作按钮（待支付：去支付/取消；待收货：确认收货/查看物流；已完成：评价/再次购买）。
  - 区域 C（空状态）：`van-empty`「暂无订单」+ 「去逛逛」CTA 跳首页。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、状态筛选 Tab、订单卡片首屏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   我的订单      │
├──────────────────┤
│全部 待付 待发 待收 完成│
├──────────────────┤
│ NO:20260726001   待支付│
│ [图][图][图]      │
│ 共3件 实付 ¥486   │
│       取消  去支付 │
├──────────────────┤
│ NO:20260725002   待收货│
│ [图]              │
│ 共1件 实付 ¥199   │
│ 查看物流 确认收货  │
├──────────────────┤
│ 加载中...         │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/orders` | 分页查询订单（按状态过滤） | Buyer |
| POST | `/api/orders/{id}/cancel` | 取消订单（待支付态） | Buyer |
| POST | `/api/orders/{id}/confirm` | 确认收货 | Buyer |

- **请求参数**：`GET /api/orders?status={OrderStatus}&page={page}&pageSize=20`；status 可空表示全部，枚举 `PendingPayment/Paid/Shipped/Completed/Cancelled`；page 从 0 起。
- **响应字段**：`OrderListResult` 含 `items`（OrderDto: id、orderNo、status、totalAmount、items: OrderItemDto[]、createdAt、expireAt）、`total`、`pageIndex`、`pageSize`。
- **数据加载策略**：`van-list` 无限滚动，每页 20 条；切换 Tab 重置列表从 page=0 加载；下拉刷新 `van-pull-refresh`。
- **缓存策略**：不缓存，每次进入重新拉取；返回订单详情页时刷新当前列表项。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认「全部」Tab → `GET /api/orders?page=0` → 渲染订单卡片列表。
  2. 切换状态 Tab → 重置列表 → `GET /api/orders?status={status}&page=0`。
  3. 滚动到底部 → `van-list` load → `GET /api/orders?page={page+1}` 追加。
  4. 下拉刷新 → 重置从 page=0 加载。
  5. 点击订单卡片 → 跳 `/order/:id`。
  6. 待支付订单点击「去支付」→ 跳 `/payment/initiate/:orderId`。
  7. 待支付订单点击「取消」→ `showConfirmDialog` → `POST /api/orders/{id}/cancel` → 列表移除或状态更新。
  8. 待收货订单点击「确认收货」→ `showConfirmDialog` → `POST /api/orders/{id}/confirm` → 状态流转为已完成。
  9. 待收货订单点击「查看物流」→ 跳 `/order/:id/logistics`。
- **分支流程**：
  - 空订单：`van-empty`「暂无订单」+ 「去逛逛」CTA。
  - 订单超时：待支付订单超过 `expireAt` 自动取消，列表标记「已取消」。
  - 多卖家拆单：一个结算拆为多个订单，列表分别展示。
- **跨页面流转**：订单详情页、支付发起页、物流轨迹页。
- **状态机可视化**：待支付 → 已支付 → 已发货 → 已完成；待支付 → 已取消。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-tabs`、`van-tab`、`van-list`、`van-pull-refresh`、`van-card`、`van-image`（lazy-load）、`van-tag`、`van-button`、`van-empty`、`van-skeleton`、`van-dialog`（showConfirmDialog）、`van-toast`（showToast）。
- **业务组件**：`OrderStatusTag` 订单状态标签（颜色映射）；`OrderCard` 订单卡片；`OrderActionBar` 操作按钮组；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；箭头 `arrow`。
- **空状态**：`van-empty`「暂无订单」+ 「去逛逛」按钮跳首页。

## 6. 视觉规范
- **主色应用**：去支付/确认收货按钮主色 `#1677FF`；Tab 激活态主色。
- **状态色**：待支付 `#FAAD14`；待发货 `#1677FF`；待收货 `#1677FF`；已完成 `#52C41A`；已取消 `#8C8C8C`；实付金额 `#FF4D4F`；取消按钮 `#8C8C8C`。
- **间距**：订单卡片间距 12px；卡片内边距 12px。
- **字体**：订单号 12px `#8C8C8C`；状态标签 12px；商品标题 14px `#000000D9`（1 行省略）；实付金额 16px semibold `#FF4D4F`；操作按钮 12px。
- **图标尺寸**：返回 20px；箭头 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 3 张订单卡片；分页加载 `van-list` loading。
- **空数据**：`van-empty`「暂无订单」+ 「去逛逛」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/orders`。
- **并发与乐观锁**：确认收货/取消操作按钮点击后立即 disabled + loading；操作冲突以服务端返回为准。
- **危险操作确认**：取消订单使用 `showConfirmDialog`（标题「确认取消」，内容「取消后订单将关闭，如需购买请重新下单」，确认按钮红色「确认取消」）；确认收货使用 `showConfirmDialog`（标题「确认收货」，内容「确认收货后交易完成，售后期开始计算」）。

## 8. 验收要点
- [ ] 状态 Tab 切换后列表正确筛选。
- [ ] 无限滚动加载下一页，无重复数据。
- [ ] 待支付订单显示「去支付」「取消」按钮。
- [ ] 待收货订单显示「确认收货」「查看物流」按钮。
- [ ] 取消/确认收货操作二次确认后状态更新。
- [ ] 空订单展示「去逛逛」CTA。
- **性能要求**：首屏 < 1s；图片懒加载；列表虚拟滚动（>100 项）。
- **可访问性**：Tab `role="tab"`；订单卡片 `role="article"`；按钮 `aria-label`。
