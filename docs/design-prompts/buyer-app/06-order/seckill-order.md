# 秒杀下单 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：06-order 订单交易
- **页面类型**：流程页
- **目标用户**：买家（Buyer）
- **核心目标**：买家在秒杀活动页选择 SKU 与数量后秒杀下单，高并发场景下原子预扣库存，下单成功跳支付发起页，下单失败提示原因。
- **访问入口**：秒杀活动详情页点击「立即抢购」进入，携带 activityId。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「秒杀下单」）+ 可滚动主体（活动倒计时+商品卡+收货地址+数量选择）+ 底部固定抢购栏，无 Tabbar。
- **关键区域**：
  - 区域 A（活动倒计时）：`van-count-down` 显示活动剩余时间或距离开始时间，倒计时结束自动刷新状态。
  - 区域 B（商品卡）：`van-card` 展示商品图+标题+秒杀价（划线原价）+剩余库存；多 SKU 时展示 `van-radio-group` 规格选择。
  - 区域 C（收货地址卡）：默认地址展示，点击切换跳 `/profile/addresses?from=seckill`。
  - 区域 D（数量选择）：`van-stepper` 数量选择，限购数量为上限（如每人限购 1 件）。
  - 区域 E（底部抢购栏）：「秒杀价 ¥{Price}」+「立即抢购」红色高对比按钮。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、倒计时、商品卡、底部抢购按钮。
- **线框图描述**：
```
┌──────────────────┐
│ ←   秒杀下单      │
├──────────────────┤
│ 距结束 02:30:15   │
├──────────────────┤
│ [图] 商品标题     │
│      ¥99 ¥199    │
│      库存 50      │
│ 规格: ◉红 ○蓝     │
├──────────────────┤
│ 📍 张三 138****1234│
│ 福建省福州市...  >│
├──────────────────┤
│ 数量 [-] 1 [+]   │
│ (限购1件)         │
├──────────────────┤
│¥99     立即抢购   │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/seckill/activities/{activityId}` | 查询秒杀活动详情（含实时库存） | Buyer |
| POST | `/api/seckill/activities/{activityId}/place` | 秒杀下单（异步预扣库存） | Buyer |
| GET | `/api/addresses` | 查询收货地址列表 | Buyer |

- **请求参数**：`activityId` 路径参数；秒杀下单 `SeckillPlaceOrderDto`（skuId、quantity）。
- **响应字段**：`SeckillActivityDto` 含 `id`、`productId`、`title`、`seckillPrice`、`originalPrice`、`stockQty`、`limitPerUser`、`startTime`、`endTime`、`status`、`skus`（skuId、seckillPrice、stockQty）；`SeckillPlaceOrderResultDto` 含 `orderId`、`success`、`failReason`。
- **数据加载策略**：进入页面调用 `GET /api/seckill/activities/{activityId}` 获取详情与实时库存；库存每 10 秒轮询刷新。
- **缓存策略**：活动详情不缓存（库存实时性）；地址列表缓存 5 分钟。

## 4. 交互流程
- **主流程**：
  1. 进入页面读取 `activityId` → `GET /api/seckill/activities/{activityId}` → 渲染倒计时、商品卡、库存。
  2. 选择 SKU（多 SKU 场景）→ 更新选中规格与对应库存。
  3. 选择数量 → `van-stepper` 限购上限为 `limitPerUser`。
  4. 点击「立即抢购」→ 按钮 disabled + loading → `POST /api/seckill/activities/{activityId}/place` → 返回 `SeckillPlaceOrderResultDto`。
  5. 下单成功 → 跳 `/payment/initiate/:orderId`。
  6. 下单失败 → 提示原因（库存不足/重复下单/活动结束/限购超限）。
- **分支流程**：
  - 活动未开始：按钮置灰「即将开始」，倒计时显示距离开始时间。
  - 活动已结束：按钮置灰「已结束」，提示「活动已结束」。
  - 库存为 0：按钮置灰「已抢完」，提示「库存不足」。
  - 限购超限：提示「已达限购上限」。
- **跨页面流转**：下单成功跳支付发起页；返回活动详情页。
- **状态机可视化**：未开始(置灰) → 进行中(可抢购) → 已结束(置灰)；抢购中(loading) → 成功(跳支付) / 失败(提示)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-card`、`van-cell`、`van-radio-group`、`van-radio`、`van-stepper`、`van-button`、`van-image`（lazy-load）、`van-count-down`、`van-tag`、`van-skeleton`、`van-toast`（showToast）、`van-dialog`（showConfirmDialog）。
- **业务组件**：`SeckillCountdown` 倒计时组件；`SeckillProductCard` 商品卡（含秒杀价与库存）；`SeckillActionBar` 底部抢购栏；`AddressSelectorCard` 地址卡。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；地址 `location-o`；箭头 `arrow`；闪电 `fire`。
- **空状态**：活动不存在显示全屏错误 + 「返回首页」CTA。

## 6. 视觉规范
- **主色应用**：抢购按钮红色 `#FF4D4F`（制造紧迫感）；秒杀价红色；倒计时主色背景。
- **状态色**：秒杀价 `#FF4D4F`；原价划线 `#8C8C8C`；库存预警 `#FAAD14`；已结束 `#8C8C8C`；抢购按钮 `#FF4D4F`。
- **间距**：区域间距 12px；卡片内边距 12px；底部抢购栏高 54px。
- **字体**：倒计时 20px semibold `#FFFFFF`（主色背景）；秒杀价 24px semibold `#FF4D4F`；商品标题 14px `#000000D9`（2 行省略）；库存 12px `#8C8C8C`；抢购按钮 16px semibold `#FFFFFF`。
- **图标尺寸**：返回 20px；地址 20px；闪电 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟商品卡与倒计时布局。
- **空数据**：活动不存在显示全屏错误 + 「返回首页」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；下单失败显示后端返回的具体原因 3s。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/seckill/order/:activityId`。
- **并发与乐观锁**：抢购按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复提交；库存以 Redis 原子预扣为准。
- **危险操作确认**：不涉及（秒杀下单非危险操作，但需防重复点击与高并发）。

## 8. 验收要点
- [ ] 倒计时实时更新，结束自动刷新状态。
- [ ] 多 SKU 选择后实时显示对应库存与价格。
- [ ] 数量步进器上限为限购数量。
- [ ] 抢购按钮防重复点击（loading + Idempotency-Key）。
- [ ] 下单成功跳支付发起页，失败提示具体原因。
- [ ] 库存为 0 或活动结束时按钮置灰。
- **性能要求**：首屏 < 1s；库存轮询 10 秒；抢购响应 < 1.5s。
- **可访问性**：倒计时 `aria-label`；按钮 `aria-label`；SKU 选择 `role="radiogroup"`。
