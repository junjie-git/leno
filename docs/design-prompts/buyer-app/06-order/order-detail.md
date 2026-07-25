# 订单详情 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：06-order 订单交易
- **页面类型**：详情页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看订单完整信息（状态、商品、金额、地址、物流、支付），并执行状态相关操作（支付、取消、确认收货、评价、申请售后）。
- **访问入口**：订单列表卡片点击；支付结果页返回；物流轨迹页返回。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「订单详情」+联系客服）+ 可滚动主体（状态区+物流摘要+商品区+金额明细+收货地址+订单信息）+ 底部固定操作栏，无 Tabbar。
- **关键区域**：
  - 区域 A（状态区）：大字号状态文案+状态说明（如「待支付，剩余 14:23」倒计时）。
  - 区域 B（物流摘要）：已发货订单展示最新一条物流节点+「查看物流详情」跳 `/order/:id/logistics`。
  - 区域 C（商品区）：按 `OrderItemDto` 列出商品图+标题+规格+单价+数量+行小计，点击商品跳商品详情。
  - 区域 D（金额明细）：商品总额、优惠总额、积分抵扣、运费、应付总额。
  - 区域 E（收货地址）：收件人+电话+省市区+详细地址。
  - 区域 F（订单信息）：订单号+下单时间+支付方式+支付时间+物流单号。
  - 区域 G（底部操作栏）：按状态显示按钮（待支付：取消/去支付；待收货：查看物流/确认收货；已完成：评价/申请售后/再次购买）。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、状态区、物流摘要、商品区首屏、底部操作栏。
- **线框图描述**：
```
┌──────────────────┐
│ ←  订单详情   客服 │
├──────────────────┤
│ 待支付            │
│ 剩余 14:23        │
├──────────────────┤
│ 📍 张三 138****1234│
│ 福建省福州市...  │
├──────────────────┤
│ [图] 商品1 ¥199×1│
│ [图] 商品2 ¥89×2 │
├──────────────────┤
│ 商品总额  ¥377   │
│ 优惠总额  -¥30   │
│ 运费      ¥0     │
│ 应付总额  ¥347   │
├──────────────────┤
│ 订单号 20260726001│
│ 下单时间 07-26 10:│
├──────────────────┤
│ 取消     去支付   │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/orders/{id}` | 查询订单详情 | Buyer |
| POST | `/api/orders/{id}/cancel` | 取消订单（待支付态） | Buyer |
| POST | `/api/orders/{id}/confirm` | 确认收货 | Buyer |
| POST | `/api/payments?orderId={id}` | 发起支付 | Buyer |

- **请求参数**：`id` 路径参数；取消 `CancelOrderDto`（reason）；发起支付 `PayOrderDto`（paymentMethod），orderId 以 query 传递。
- **响应字段**：`OrderDetailResult` 含 `id`、`orderNo`、`orderType`、`status`、`itemsAmount`、`discountAmount`、`pointsOffsetAmount`、`freightAmount`、`totalAmount`、`paymentMethod`、`expireAt`、`paidAt`、`shippedAt`、`logisticsNo`、`logisticsCompanyCode`、`completedAt`、`cancelledAt`、`cancelReason`、`createdAt`、`items`（OrderItemDto: skuId、productName、skuName、mainImage、unitPrice、quantity、discountAllocation、subtotal）、`addressSnapshot`（recipientName、phone、province/city/district/detail）。
- **数据加载策略**：进入页面调用 `GET /api/orders/{id}` 全量加载；操作后局部刷新状态。
- **缓存策略**：不缓存，每次进入重新拉取；待支付订单倒计时基于 `expireAt` 客户端计算。

## 4. 交互流程
- **主流程**：
  1. 进入页面读取 `id` → `GET /api/orders/{id}` → 渲染状态区、商品区、金额、地址、订单信息。
  2. 待支付订单显示倒计时（基于 `expireAt`），倒计时结束标记「已超时」并刷新状态。
  3. 点击「去支付」→ `POST /api/payments?orderId={id}` 发起支付 → 跳 `/payment/initiate/:orderId`。
  4. 点击「取消」→ `showConfirmDialog` → `POST /api/orders/{id}/cancel` → 状态更新为已取消。
  5. 已发货订单点击「查看物流」→ 跳 `/order/:id/logistics`。
  6. 待收货订单点击「确认收货」→ `showConfirmDialog` → `POST /api/orders/{id}/confirm` → 状态流转为已完成。
  7. 已完成订单点击「评价」→ 跳 `/review/submit/:orderLineId`。
  8. 已完成订单点击「申请售后」→ 跳 `/after-sales/apply/:orderLineId`。
- **分支流程**：
  - 订单已取消：状态区展示取消原因，操作栏隐藏。
  - 会员订阅订单：商品区展示套餐名，无物流与售后入口。
  - 多商品订单：商品列表可滚动，每项独立可点击跳商品详情。
- **跨页面流转**：支付发起页、物流轨迹页、评价提交页、售后申请页。
- **状态机可视化**：待支付(倒计时) → 已支付 → 已发货 → 已完成；待支付 → 已取消。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-cell`、`van-cell-group`、`van-card`、`van-image`（lazy-load）、`van-tag`、`van-button`、`van-count-down`（倒计时）、`van-skeleton`、`van-dialog`（showConfirmDialog）、`van-toast`（showToast）。
- **业务组件**：`OrderStatusHeader` 状态区头部；`OrderItemRow` 商品行；`AmountSummaryCell` 金额汇总行；`AddressCard` 地址卡；`OrderInfoCell` 订单信息行；`OrderActionBar` 操作按钮组。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；客服 `service-o`；地址 `location-o`。
- **空状态**：订单不存在显示全屏错误 + 「返回订单列表」CTA。

## 6. 视觉规范
- **主色应用**：去支付/确认收货按钮主色 `#1677FF`；状态文案主色（待支付为警告色）。
- **状态色**：待支付 `#FAAD14`；待发货 `#1677FF`；待收货 `#1677FF`；已完成 `#52C41A`；已取消 `#8C8C8C`；应付总额 `#FF4D4F`；取消按钮 `#8C8C8C`。
- **间距**：区域间距 12px；卡片内边距 12px；底部操作栏高 50px。
- **字体**：状态文案 20px semibold；商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；应付总额 16px semibold `#FF4D4F`；订单信息 12px `#8C8C8C`。
- **图标尺寸**：返回 20px；客服 20px；地址 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟状态区+商品区+金额布局。
- **空数据**：订单不存在显示全屏错误 + 「返回订单列表」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮。
- **权限控制**：Buyer 可见；订单归属校验由服务端 `OrderDetailQuery.CurrentUserId` 完成，非本人订单返回 403。
- **并发与乐观锁**：操作按钮点击后立即 disabled + loading；倒计时客户端计算，超时后服务端校验为准。
- **危险操作确认**：取消订单 `showConfirmDialog`（标题「确认取消」，内容「取消后订单将关闭」，确认按钮红色「确认取消」）；确认收货 `showConfirmDialog`（标题「确认收货」，内容「确认收货后售后期开始计算」）。

## 8. 验收要点
- [ ] 待支付订单显示倒计时，超时后标记已取消。
- [ ] 按状态显示对应操作按钮。
- [ ] 金额明细含商品总额、优惠、积分抵扣、运费、应付总额。
- [ ] 已发货订单展示物流摘要并可跳物流详情。
- [ ] 取消/确认收货操作二次确认后状态更新。
- [ ] 已完成订单可跳评价与售后申请。
- **性能要求**：首屏 < 1s；图片懒加载；倒计时每秒更新不卡顿。
- **可访问性**：状态文案 `aria-label`；按钮 `aria-label`；商品图 `alt`。
