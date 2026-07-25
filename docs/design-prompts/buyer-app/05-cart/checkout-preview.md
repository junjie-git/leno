# 结算预览 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：05-cart 购物车
- **页面类型**：流程页
- **目标用户**：买家（Buyer）
- **核心目标**：买家在结算前预览按卖家分组的选中商品、小计与合计金额，确认无误后进入结算确认页选择地址、优惠券与积分抵扣并提交订单。
- **访问入口**：购物车页点击「结算(N)」按钮进入。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「结算预览」）+ 可滚动主体（按卖家分组的预览卡片+金额汇总）+ 底部固定操作栏（合计+「提交订单」），无 Tabbar。
- **关键区域**：
  - 区域 A（卖家分组卡片）：每个 `CheckoutGroupDto` 渲染为 `van-card` 组，组头展示卖家名，组内列出 `CartItemDto`（图片+标题+规格+单价+数量+行小计），组尾展示该组小计 `SubtotalAmount`。
  - 区域 B（金额汇总区）：`van-cell-group` 列出商品总额、优惠总额、运费预估、积分抵扣预估、应付总额；混币种时按 `SubtotalsByCurrency` 分别展示各币种小计。
  - 区域 C（提示条）：若含失效项或库存不足项，顶部 `van-notice-bar` 提示「N 件商品已失效/库存不足，已自动排除」。
  - 区域 D（底部操作栏）：左侧「合计 ¥{TotalAmount}」+ 右侧「提交订单」主色按钮。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、按卖家分组的预览卡片、金额汇总、底部提交按钮。
- **线框图描述**：
```
┌──────────────────┐
│ ←   结算预览      │
├──────────────────┤
│ ⚠ 1件已失效已排除 │
├──────────────────┤
│ 店铺A            │
│ [图] 商品1 ¥199×1│
│ [图] 商品2 ¥89×2 │
│ 小计 ¥377        │
├──────────────────┤
│ 店铺B            │
│ [图] 商品3 ¥50×1 │
│ 小计 ¥50         │
├──────────────────┤
│ 商品总额  ¥427   │
│ 优惠总额  -¥30   │
│ 运费预估  ¥0     │
│ 积分抵扣  -¥5    │
│ 应付总额  ¥392   │
├──────────────────┤
│ 合计 ¥392  提交订单│
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/cart/preview` | 结算预览（按卖家分组返回选中项） | Buyer |
| POST | `/api/orders/preview` | 下单预览（计算预估金额不落库） | Buyer |

- **请求参数**：`POST /api/cart/preview` 无 body（基于当前用户购物车选中项）；`POST /api/orders/preview` 入参 `CreateOrderDto`（items: CheckoutItemDto[]、paymentMethod、pointsToUse、recipientName/phone/province/city/district/detail）。
- **响应字段**：`CheckoutPreviewDto` 含 `groups`（sellerId、items: CartItemDto[]、subtotalAmount、currency）、`totalAmount`、`currency`、`totalCount`、`subtotalsByCurrency`；`OrderPreviewResultDto` 含 `itemsAmount`、`discountAmount`、`pointsOffsetAmount`、`freightAmount`、`totalAmount`、`items`（skuId、productName、unitPrice、quantity、subtotal）。
- **数据加载策略**：进入页面调用 `POST /api/cart/preview` 获取分组预览；用户调整积分或优惠券后调用 `POST /api/orders/preview` 重新试算金额。
- **缓存策略**：不缓存，每次进入重新预览，确保价格与库存实时性。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 调用 `POST /api/cart/preview` → 渲染按卖家分组的预览卡片与金额汇总。
  2. 若含失效或库存不足项，顶部 `van-notice-bar` 提示已自动排除。
  3. 用户点击「提交订单」→ 跳 `/checkout/settle` 进入结算确认页（携带 groups 数据）。
  4. 在结算确认页选择地址、优惠券、积分后返回本页时，调用 `POST /api/orders/preview` 重新试算金额。
- **分支流程**：
  - 选中项为空：`van-empty`「无结算商品」+ 「返回购物车」CTA。
  - 混币种：金额汇总按 `SubtotalsByCurrency` 分别展示各币种小计，`TotalAmount` 不展示。
  - 价格变更：商品域价格已调整时，预览以现价为准，提示「价格可能已变」。
- **跨页面流转**：提交订单跳结算确认页（`/checkout/settle`）；返回购物车页修改选中项。
- **状态机可视化**：预览加载中(skeleton) → 有选中项(可提交) / 无选中项(空状态)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-card`、`van-cell`、`van-cell-group`、`van-button`、`van-image`（lazy-load）、`van-notice-bar`、`van-empty`、`van-skeleton`、`van-tag`。
- **业务组件**：`CheckoutSellerGroup` 卖家分组卡片；`AmountSummaryCell` 金额汇总行；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；警告 `warning-o`。
- **空状态**：无选中项 `van-empty`「无结算商品」+ 「返回购物车」CTA。

## 6. 视觉规范
- **主色应用**：提交订单按钮主色 `#1677FF`；卖家名链接主色。
- **状态色**：应付总额 `#FF4D4F`；优惠金额 `#52C41A`；警告条 `#FAAD14`；失效标签 `#8C8C8C`。
- **间距**：卖家组间距 12px；卡片内边距 12px；底部操作栏高 50px。
- **字体**：应付总额 18px semibold `#FF4D4F`；商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；单价 14px `#000000D9`；金额汇总行 14px `#595959`。
- **图标尺寸**：返回 20px；警告 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟分组卡片与金额汇总布局。
- **空数据**：`van-empty`「无结算商品」+ 「返回购物车」CTA。
- **错误态**：接口失败 `showToast` 「预览失败，请重试」+ 重试按钮；返回购物车页检查选中项。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/checkout/preview`。
- **并发与乐观锁**：预览金额不锁定，下单瞬间以服务端校验为准；提交按钮点击后立即 disabled + loading 防重复。
- **危险操作确认**：不涉及（预览非危险操作）。

## 8. 验收要点
- [ ] 按卖家分组展示选中项及各组小计。
- [ ] 金额汇总含商品总额、优惠、运费、积分抵扣、应付总额。
- [ ] 失效或库存不足项自动排除并提示。
- [ ] 混币种按各币种分别展示小计。
- [ ] 提交订单按钮跳结算确认页。
- **性能要求**：首屏 < 1s；图片懒加载；预览请求 < 800ms。
- **可访问性**：金额文本 `aria-label`；提交按钮 `aria-label`；分组卡片 `role="group"`。
