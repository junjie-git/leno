# 立即购买下单 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：06-order 订单交易
- **页面类型**：表单页
- **目标用户**：买家（Buyer）
- **核心目标**：买家从商品详情「立即购买」进入，对单 SKU 直接下单，选择收货地址、支付方式、积分抵扣后提交订单并跳转支付发起页。
- **访问入口**：商品详情页点击「立即购买」按钮进入，携带 skuId+quantity。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「立即购买」）+ 可滚动主体（商品卡+收货地址+支付方式+积分抵扣+金额明细）+ 底部固定提交栏，无 Tabbar。
- **关键区域**：
  - 区域 A（商品卡）：`van-card` 展示商品图+标题+规格+单价+数量步进器，数量可调整。
  - 区域 B（收货地址卡）：默认地址展示，点击切换跳 `/profile/addresses?from=buy-now`。
  - 区域 C（支付方式）：`van-radio-group` 列出支付宝、微信，默认支付宝。
  - 区域 D（积分抵扣）：`van-cell` 展示可用积分，`van-switch` 开启后输入使用积分数。
  - 区域 E（金额明细）：商品总额、积分抵扣、运费预估、应付总额。
  - 区域 F（底部提交栏）：「应付总额 ¥{TotalAmount}」+「提交订单」主色按钮。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、商品卡、收货地址、支付方式、底部提交栏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   立即购买      │
├──────────────────┤
│ [图] 商品标题     │
│      规格 ¥199    │
│      [-] 1 [+]    │
├──────────────────┤
│ 📍 张三 138****1234│
│ 福建省福州市...  >│
├──────────────────┤
│ 支付方式          │
│ ◉ 支付宝 ○ 微信   │
├──────────────────┤
│ 积分抵扣  -¥5 [开]│
├──────────────────┤
│ 商品总额  ¥199   │
│ 积分抵扣  -¥5    │
│ 运费      ¥0     │
│ 应付总额  ¥194   │
├──────────────────┤
│ 应付¥194  提交订单│
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/products/{id}` | 查询商品详情（含 SKU 价格库存） | Buyer |
| GET | `/api/addresses` | 查询收货地址列表 | Buyer |
| GET | `/api/points/account` | 查询积分余额 | Buyer |
| POST | `/api/orders/buy-now` | 立即购买创建订单 | Buyer |

- **请求参数**：`BuyNowDto`（skuId、quantity、paymentMethod、pointsToUse、recipientName/phone/province/city/district/detail）。
- **响应字段**：`OrderDto` 含 `id`、`orderNo`、`status`、`totalAmount`、`expireAt`、`items`；`ProductDto` 含 `id`、`title`、`mainImageUrl`、`skus`（id、price、stockQty、specAttributes、status）；`AddressDto` 含 `id`、`recipientName`、`phone`、`province/city/district/detail`、`isDefault`。
- **数据加载策略**：进入页面并行加载商品详情、地址列表、积分账户；数量或积分变化后客户端预估金额（以下单时服务端校验为准）。
- **缓存策略**：商品详情缓存 5 分钟；地址列表缓存 5 分钟；积分余额缓存 30 秒。

## 4. 交互流程
- **主流程**：
  1. 进入页面读取 skuId+quantity → 并行加载商品详情、地址、积分 → 渲染商品卡与默认地址。
  2. 调整数量 → `van-stepper` change → 客户端重算商品总额与应付总额。
  3. 点击地址卡 → 跳 `/profile/addresses?from=buy-now` 选择地址 → 返回后更新。
  4. 切换支付方式 → 更新 `paymentMethod`。
  5. 开启积分抵扣 → 输入积分数 → 客户端重算抵扣金额。
  6. 点击「提交订单」→ 按钮 disabled + loading → `POST /api/orders/buy-now` → 返回 `OrderDto` → 跳 `/payment/initiate/:orderId`。
- **分支流程**：
  - SKU 已下架：商品卡展示「已下架」，提交按钮置灰禁用。
  - 库存不足：`van-stepper` 上限调整为可售库存，提示「库存不足」。
  - 无收货地址：地址区展示「新增收货地址」CTA。
  - 提交失败：显示后端错误信息（如积分不足、价格已变）3s。
- **跨页面流转**：提交成功跳支付发起页；选择地址跳地址管理页。
- **状态机可视化**：表单填写 → 提交中(loading) → 成功(跳支付) / 失败(提示)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-card`、`van-cell`、`van-cell-group`、`van-radio-group`、`van-radio`、`van-switch`、`van-stepper`、`van-field`（积分输入）、`van-button`、`van-image`（lazy-load）、`van-skeleton`、`van-toast`（showToast）。
- **业务组件**：`BuyNowProductCard` 商品卡（含数量步进器）；`AddressSelectorCard` 地址卡；`PointsOffsetCell` 积分抵扣单元；`AmountSummaryCell` 金额汇总行。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；地址 `location-o`；箭头 `arrow`。
- **空状态**：无地址展示「新增收货地址」CTA。

## 6. 视觉规范
- **主色应用**：提交订单按钮主色 `#1677FF`；选中的支付方式主色；地址卡边框主色。
- **状态色**：应付总额 `#FF4D4F`；积分抵扣 `#52C41A`；下架标签 `#8C8C8C`；库存不足 `#FAAD14`。
- **间距**：区域间距 12px；卡片内边距 12px；底部提交栏高 50px。
- **字体**：应付总额 18px semibold `#FF4D4F`；商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；地址 14px `#000000D9`；金额明细行 14px `#595959`。
- **图标尺寸**：地址图标 20px；箭头 16px；步进器按钮 28px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟商品卡+地址+金额布局。
- **空数据**：无地址展示 CTA；商品不存在显示全屏错误 + 「返回」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；提交失败显示后端错误信息 3s。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/order/create`。
- **并发与乐观锁**：提交按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复提交；价格与库存以下单瞬间服务端校验为准。
- **危险操作确认**：不涉及（提交订单非危险操作，但需防重复点击）。

## 8. 验收要点
- [ ] 商品卡展示 SKU 信息与数量步进器。
- [ ] 默认地址自动选中并展示完整地址。
- [ ] 调整数量后金额实时重算。
- [ ] 积分抵扣开关开启后输入积分数并重算抵扣。
- [ ] 提交订单成功后跳支付发起页。
- [ ] 防重复提交（按钮 loading + Idempotency-Key）。
- [ ] SKU 下架或库存不足时禁用提交按钮。
- **性能要求**：首屏 < 1s；图片懒加载；提交响应 < 1.5s。
- **可访问性**：地址卡 `aria-label`；支付方式 `role="radiogroup"`；提交按钮 `aria-label`。
