# 结算确认 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：05-cart 购物车
- **页面类型**：表单页
- **目标用户**：买家（Buyer）
- **核心目标**：买家在结算确认页选择收货地址、支付方式、优惠券与积分抵扣，确认金额后提交订单，按卖家自动拆单并跳转支付发起页。
- **访问入口**：结算预览页点击「提交订单」进入；立即购买流程直接进入。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「结算确认」）+ 可滚动主体（收货地址+商品分组+支付方式+优惠券+积分抵扣+金额汇总）+ 底部固定提交栏，无 Tabbar。
- **关键区域**：
  - 区域 A（收货地址卡）：默认地址展示收件人+电话+省市区+详细地址+「默认」标签，点击切换跳 `/profile/addresses?from=checkout`；无地址时展示「新增收货地址」CTA。
  - 区域 B（商品分组）：按卖家分组展示商品图+标题+规格+单价+数量+行小计，每组展示组小计。
  - 区域 C（支付方式）：`van-radio-group` 列出支付宝、微信；默认选中支付宝。
  - 区域 D（优惠券）：`van-cell` 「优惠券」点击弹出 `van-popup` 列出可用券，显示「-¥XX」或「无可用」。
  - 区域 E（积分抵扣）：`van-cell` 「积分抵扣」展示可用积分与抵扣金额，`van-switch` 开启后输入使用积分数，100 积分=1 元。
  - 区域 F（金额汇总）：商品总额、优惠总额、积分抵扣、运费、应付总额。
  - 区域 G（底部提交栏）：「应付总额 ¥{TotalAmount}」+「提交订单」主色按钮。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、收货地址卡、商品分组首屏、支付方式、底部提交栏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   结算确认      │
├──────────────────┤
│ 📍 张三 138****1234│
│ 福建省福州市...  >│
├──────────────────┤
│ 店铺A            │
│ [图] 商品1 ¥199×1│
│ 小计 ¥377        │
├──────────────────┤
│ 支付方式          │
│ ◉ 支付宝 ○ 微信   │
├──────────────────┤
│ 优惠券    -¥30  > │
│ 积分抵扣  -¥5  [开]│
├──────────────────┤
│ 商品总额  ¥427   │
│ 优惠总额  -¥30   │
│ 积分抵扣  -¥5    │
│ 运费      ¥0     │
│ 应付总额  ¥392   │
├──────────────────┤
│ 应付¥392  提交订单│
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/addresses` | 查询收货地址列表 | Buyer |
| POST | `/api/orders/preview` | 下单预览（试算金额） | Buyer |
| POST | `/api/orders` | 创建订单（按卖家自动拆单） | Buyer |
| POST | `/api/orders/buy-now` | 立即购买（单 SKU） | Buyer |
| GET | `/api/coupons/mine?status=Usable` | 查询可用优惠券 | Buyer |
| GET | `/api/points/account` | 查询积分余额 | Buyer |

- **请求参数**：`CreateOrderDto`（items: CheckoutItemDto[]、paymentMethod、pointsToUse、recipientName/phone/province/city/district/detail）；`BuyNowDto`（skuId、quantity、paymentMethod、pointsToUse、recipient*）。
- **响应字段**：`OrderDto` 含 `id`、`orderNo`、`orderType`、`status`、`totalAmount`、`expireAt`、`items`；`OrderPreviewResultDto` 含 `itemsAmount`、`discountAmount`、`pointsOffsetAmount`、`freightAmount`、`totalAmount`；`AddressDto` 含 `id`、`recipientName`、`phone`、`province/city/district/detail`、`isDefault`、`tag`。
- **数据加载策略**：进入页面并行加载地址列表、可用券、积分账户；用户切换地址/券/积分后调用 `POST /api/orders/preview` 重新试算。
- **缓存策略**：地址列表缓存 5 分钟；积分余额缓存 30 秒；预览金额不缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行加载地址、可用券、积分账户 → 默认选中默认地址与支付宝 → 调用 `POST /api/orders/preview` 试算金额。
  2. 点击地址卡 → 跳 `/profile/addresses?from=checkout` 选择地址 → 返回后重新试算运费。
  3. 切换支付方式 → `van-radio-group` change → 更新 `paymentMethod`。
  4. 点击优惠券 → `van-popup` 弹出可用券列表 → 选中后重新试算优惠。
  5. 开启积分抵扣 → 输入使用积分数 → 重新试算积分抵扣金额。
  6. 点击「提交订单」→ 按钮 disabled + loading → `POST /api/orders` → 返回 `OrderDto` → 跳 `/payment/initiate/:orderId`。
- **分支流程**：
  - 立即购买：从商品详情直接进入，携带 skuId+quantity，提交时调用 `POST /api/orders/buy-now`。
  - 无收货地址：地址区展示「新增收货地址」CTA 跳 `/profile/addresses`。
  - 积分不足：开关置灰，提示「积分不足」。
  - 订单超时：提交时若订单已过期，提示「订单已过期，请重新下单」。
- **跨页面流转**：提交成功跳支付发起页（`/payment/initiate/:orderId`）；选择地址跳地址管理页。
- **状态机可视化**：表单填写中 → 提交中(loading) → 成功(跳支付) / 失败(提示重试)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-card`、`van-cell`、`van-cell-group`、`van-radio-group`、`van-radio`、`van-switch`、`van-popup`、`van-button`、`van-field`（积分输入）、`van-image`（lazy-load）、`van-skeleton`、`van-toast`（showToast）、`van-dialog`（showConfirmDialog）。
- **业务组件**：`AddressSelectorCard` 地址卡；`CheckoutSellerGroup` 卖家分组；`CouponPickerPopup` 优惠券选择弹层；`PointsOffsetCell` 积分抵扣单元；`AmountSummaryCell` 金额汇总行。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；地址 `location-o`；箭头 `arrow`。
- **空状态**：无地址展示「新增收货地址」CTA；无可用券展示「暂无可用优惠券」。

## 6. 视觉规范
- **主色应用**：提交订单按钮主色 `#1677FF`；选中的支付方式主色；地址卡边框主色。
- **状态色**：应付总额 `#FF4D4F`；优惠金额 `#52C41A`；积分抵扣 `#52C41A`；默认地址标签主色。
- **间距**：区域间距 12px；卡片内边距 12px；底部提交栏高 50px。
- **字体**：应付总额 18px semibold `#FF4D4F`；商品标题 14px `#000000D9`；规格 12px `#8C8C8C`；地址 14px `#000000D9`；金额汇总行 14px `#595959`。
- **图标尺寸**：地址图标 20px；箭头 16px；支付方式图标 24px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟地址卡+商品分组+金额汇总布局。
- **空数据**：无地址展示 CTA；无可用券隐藏优惠券行。
- **错误态**：接口失败 `showToast` 「试算失败，请重试」+ 重试按钮；提交失败显示后端错误信息 3s。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/checkout/settle`。
- **并发与乐观锁**：提交按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复提交；积分抵扣以服务端校验为准。
- **危险操作确认**：不涉及（提交订单非危险操作，但需防重复点击）。

## 8. 验收要点
- [ ] 默认地址自动选中并展示完整地址。
- [ ] 切换地址/券/积分后金额实时重新试算。
- [ ] 支付方式默认选中支付宝，可切换。
- [ ] 积分抵扣开关开启后输入积分数并试算抵扣金额。
- [ ] 提交订单成功后跳支付发起页，按卖家拆单返回订单列表。
- [ ] 防重复提交（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 1.2s；预览试算 < 800ms；图片懒加载。
- **可访问性**：地址卡 `aria-label`；支付方式 `role="radiogroup"`；提交按钮 `aria-label`。
