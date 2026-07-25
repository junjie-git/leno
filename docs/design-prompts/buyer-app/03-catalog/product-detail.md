# 商品详情 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：03-catalog 商品目录
- **页面类型**：详情页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看商品完整信息（图片、规格、价格、详情图、评价），选择 SKU 规格与数量后加入购物车或立即购买。
- **访问入口**：搜索结果卡片点击；首页/分类页商品卡片点击；购物车商品点击；商品评价页跳回。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+分享+客服）+ 可滚动主体（轮播图+价格区+规格区+详情图+评价摘要）+ 底部固定操作栏（客服+店铺+收藏+加购+立即购买），无 Tabbar。
- **关键区域**：
  - 区域 A（商品图轮播）：`van-swipe` 高度 375px，含主图与详情图，指示器圆点底部居中，支持双指放大。
  - 区域 B（价格区）：现价 24px semibold `#FF4D4F` + 原价划线 14px `#8C8C8C` + 角标「省 ¥XX」+ 月销与标题。
  - 区域 C（规格选择条）：`van-cell` 「选择 颜色/尺码/数量」点击弹出 `van-action-bar` SKU 选择面板。
  - 区域 D（详情图区）：标题「商品详情」+ 长图 `van-image` lazy-load 列表。
  - 区域 E（评价摘要区）：评分 + 评价数 + 前 2 条已通过评价，点击「查看全部 N 条」跳 `/product/:spuId/reviews`。
  - 区域 F（底部操作栏）：客服、店铺、收藏（心形切换）、加购（主色描边）、立即购买（主色填充）。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：轮播图首张、价格、标题、规格选择条、底部操作栏。
- **线框图描述**：
```
┌──────────────────┐
│ ←    商品详情  ⏤📞│
├──────────────────┤
│ [图片轮播]        │
├──────────────────┤
│ ¥199 ¥299 省100  │
│ 商品标题...       │
│ 月销 1200  福建福州│
├──────────────────┤
│ 选择 颜色 尺码 数量>│
├──────────────────┤
│ 商品详情          │
│ [长图][长图]      │
├──────────────────┤
│ 评价 4.8 | 235条 >│
│ [评价摘要][评价]  │
├──────────────────┤
│💬 🏪 🤍  加购 立即买│
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/products/{id}` | 查询商品详情（含 SKU 与图片） | Buyer |
| GET | `/api/products/{id}/price-history` | 查询价格变更历史 | Buyer |
| GET | `/api/products/{spuId}/reviews` | 分页查询已通过评价 | 匿名 |
| POST | `/api/cart/items` | 加入购物车 | Buyer |
| POST | `/api/orders/buy-now` | 立即购买（单 SKU 创建订单） | Buyer |

- **请求参数**：商品详情 `id` 路径参数；评价 `spuId`、`page=1`、`pageSize=2`；加购 `AddCartItemDto`（skuId、quantity）；立即购买 `BuyNowDto`（skuId、quantity、paymentMethod、recipient*、province/city/district/detail）。
- **响应字段**：`ProductDto` 含 `id`、`title`、`subtitle`、`mainImageUrl`、`images`、`skus`（id、price、stockQty、specAttributes、status）、`specs`、`shopId`、`sellerId`、`status`；`PriceChangeRecordDto` 含 `oldPrice`、`newPrice`、`changedAt`；`ReviewListResultDto` 含 `items`（rating、content、images、createdAt、userNick）。
- **数据加载策略**：进入页面并行加载商品详情、价格历史、评价摘要前 2 条；SKU 实时库存与价格以下单时校验为准。
- **缓存策略**：商品详情缓存 5 分钟（key: `product_detail_{id}`）；评价摘要缓存 5 分钟；价格历史不缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面读取 `id` query → 调用 `GET /api/products/{id}` → 渲染轮播、价格、标题、规格条、详情图。
  2. 并行调用评价摘要与价格历史接口 → 渲染评价区与「降价提醒」标签。
  3. 点击「选择规格」→ `van-popup` 底部弹出 SKU 面板 → 选择规格属性 → 实时显示对应 SKU 价格与库存 → 选择数量 → 点击「加入购物车」/「立即购买」。
  4. 加购 → `POST /api/cart/items` → 成功 `showToast` 「已加入购物车」+ 购物车角标 +1。
  5. 立即购买 → `POST /api/orders/buy-now` → 返回 `OrderDto` → 跳 `/payment/initiate/:orderId`。
  6. 点击「查看全部评价」→ 跳 `/product/:spuId/reviews`。
  7. 点击轮播图 → 全屏预览支持双指放大滑动。
  8. 点击「分享」→ 调用浏览器原生 `navigator.share` 或复制链接 `showToast`。
- **分支流程**：
  - 商品已下架：价格区显示「已下架」灰色标签，底部操作栏「加购」「立即购买」置灰禁用。
  - SKU 库存为 0：面板对应规格置灰，按钮显示「无货」。
  - 收藏切换：点击心形图标切换收藏态，调用收藏接口（➕ 补充功能，本地 localStorage 暂存）。
- **跨页面流转**：加购后可继续浏览；立即购买跳支付发起页；评价跳评价列表页；店铺跳店铺详情页（🚧）。
- **状态机可视化**：加载中(skeleton) → 在售(可购买) / 已下架(禁用购买) / 缺货(置灰)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-swipe`、`van-swipe-item`、`van-image`（lazy-load + preview）、`van-cell`、`van-popup`（底部弹出 SKU 面板）、`van-stepper`（数量选择）、`van-button`、`van-icon`、`van-skeleton`、`van-tag`。
- **业务组件**：`SkuSelector` 自研规格选择面板；`PriceTag` 价格展示（含原价划线与省金额）；`ReviewSummaryCard` 评价摘要卡；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；分享 `share-o`；客服 `service-o`；店铺 `shop-o`；收藏 `like`/`like-o`。
- **空状态**：评价为空隐藏评价区；详情图为空隐藏详情区。

## 6. 视觉规范
- **主色应用**：立即购买按钮主色 `#1677FF`；规格选中边框主色；轮播指示器主色。
- **状态色**：现价 `#FF4D4F`；原价划线 `#8C8C8C`；省金额角标 `#FF4D4F`；下架标签 `#8C8C8C`；收藏激活 `#FF4D4F`。
- **间距**：区域间距 12px；价格区内边距 12px；底部操作栏高 50px。
- **字体**：现价 24px semibold `#FF4D4F`；标题 16px medium `#000000D9`（2 行省略）；副标题 14px `#595959`；规格条 14px `#000000D9`；详情正文 14px `#595959`。
- **图标尺寸**：底部操作栏图标 24px；返回 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟轮播+价格+规格布局；详情图区单独 skeleton。
- **空数据**：评价空隐藏评价区；详情图空隐藏详情区；SKU 列表空提示「暂无可售规格」。
- **错误态**：商品不存在显示全屏错误 + 「返回首页」CTA；接口失败显示重试按钮。
- **权限控制**：Buyer 可见；加购与立即购买需登录态，未登录跳 `/login?redirect=当前路径`。
- **并发与乐观锁**：SKU 库存与价格以下单瞬间服务端校验为准，前端不锁定；加购请求 300ms 防抖避免重复。
- **危险操作确认**：不涉及（加购与立即购买非危险操作）。

## 8. 验收要点
- [ ] 轮播图双指放大、左右滑动、指示器切换正常。
- [ ] SKU 面板选择规格后实时显示价格与库存。
- [ ] 加购成功显示 Toast 并购物车角标 +1。
- [ ] 立即购买创建订单后跳支付发起页。
- [ ] 评价摘要点击「查看全部」跳评价列表页。
- [ ] 已下架商品禁用购买按钮并显示下架标签。
- **性能要求**：首屏 < 1s；详情图懒加载；轮播图首屏仅加载首张，其余懒加载。
- **可访问性**：轮播图 `aria-label`；底部按钮 `aria-label`；SKU 面板 `role="dialog"`；规格可键盘操作。
