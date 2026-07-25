# 购物车 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：05-cart 购物车
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看购物车商品，按卖家分组管理选中状态、修改数量、删除商品，并查看实时价格与失效提示，确认后进入结算预览。
- **访问入口**：底部 Tabbar「购物车」入口；商品详情加购后跳转；首页快捷入口。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（标题「购物车」+ 右侧「管理」切换态）+ 可滚动主体（按卖家分组的商品列表）+ 底部固定结算栏（全选/合计/结算按钮），Tabbar 在本页保留。
- **关键区域**：
  - 区域 A（卖家分组卡片）：每组以 `van-cell` 展示卖家店铺名+「进店 」跳转，组内每项为 `van-card` 左侧复选框+商品图+标题+规格+单价+`van-stepper` 数量。
  - 区域 B（失效商品区）：置灰展示，标注失效原因「已下架」，提供「删除」操作，不计入合计。
  - 区域 C（底部结算栏）：左侧全选 `van-checkbox` + 「合计」+ 应付金额，右侧「结算(N)」主色按钮；管理态切换为「删除」按钮。
  - 区域 D（推荐位）：购物车底部下方接入首页推荐流复用组件，空购物车时占位展示。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、按卖家分组的购物车项、底部结算栏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   购物车    管理 │
├──────────────────┤
│ ☑ 店铺A      进店>│
│  ☑ [图] 商品标题  │
│       规格 ¥199   │
│       [-] 1 [+]   │
├──────────────────┤
│ ☑ 店铺B      进店>│
│  ☐ [图] 商品标题  │
│       规格 ¥89    │
│       [-] 2 [+]   │
├──────────────────┤
│ 失效商品 (1)      │
│  [图] 已下架  删除│
├──────────────────┤
│ 推荐商品          │
│ [卡][卡][卡]      │
├──────────────────┤
│全选  合计 ¥486 结算│
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/cart` | 查询购物车（含实时价格、失效标记） | Buyer |
| POST | `/api/cart/items` | 添加购物车项 | Buyer |
| PUT | `/api/cart/items/{skuId}` | 修改购物车项数量 | Buyer |
| DELETE | `/api/cart/items/{skuId}` | 删除购物车项 | Buyer |
| POST | `/api/cart/items/select` | 批量选中/取消选中 | Buyer |
| PATCH | `/api/cart/selection` | 全选/取消全选 | Buyer |
| POST | `/api/cart/merge` | 登录时合并匿名购物车 | Buyer |
| POST | `/api/cart/preview` | 结算预览（按卖家分组） | Buyer |

- **请求参数**：加购 `AddCartItemDto`（skuId、quantity）；改数量 `UpdateCartItemQuantityDto`（quantity）；批量选中 `SelectCartItemsDto`（skuIds、isSelected）；全选 `ToggleAllSelectionDto`（isSelected）；合并 `MergeCartRequestDto`（anonymousId）。
- **响应字段**：`CartDto` 含 `items`（cartItemId、skuId、productName、skuCode、mainImage、unitPrice、quantity、isSelected、isValid、invalidReason、sellerId、sellerName、stockStatus）、`totalAmount`、`totalCount`、`selectedCount`、`selectedAmount`。
- **数据加载策略**：进入页面调用 `GET /api/cart` 全量加载；操作后局部更新对应项，避免整页刷新。
- **缓存策略**：购物车不缓存（价格实时性要求），登录态下每次进入重新拉取；登录后自动调用 `POST /api/cart/merge` 合并匿名购物车。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 登录态校验 → 调用 `GET /api/cart` → 按卖家分组渲染，默认选中有效项。
  2. 切换单项选中 → `POST /api/cart/items/select` → 实时更新底部合计与结算按钮数量。
  3. 点击全选 → `PATCH /api/cart/selection` → 批量切换有效项选中态，失效项不受影响。
  4. 修改数量 → `van-stepper` change → 防抖 300ms 后 `PUT /api/cart/items/{skuId}` → 更新小计与合计。
  5. 删除商品 → 管理态下勾选 → 点击「删除」→ `showConfirmDialog` 二次确认 → `DELETE /api/cart/items/{skuId}` → 列表移除。
  6. 点击「结算(N)」→ `POST /api/cart/preview` 获取按卖家分组预览 → 跳 `/checkout/preview`。
  7. 点击「进店」→ 跳 `/shop/:shopId`。
- **分支流程**：
  - 空购物车：展示 `van-empty`「购物车空空如也」+ 「去购物」CTA 跳首页。
  - 失效商品：置灰展示，不可选中，不计入合计，提供单独删除入口。
  - 库存不足：商品卡片标记「库存不足」，`van-stepper` 上限调整为可售库存。
- **跨页面流转**：结算跳结算预览页；加购后从商品详情返回可刷新购物车角标。
- **状态机可视化**：有效(可选/取消) ↔ 失效(置灰不可选)；有效项可删除，失效项可删除。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-checkbox`、`van-checkbox-group`、`van-card`、`van-stepper`、`van-button`、`van-cell`、`van-tag`、`van-empty`、`van-skeleton`、`van-image`（lazy-load）、`van-dialog`（showConfirmDialog）、`van-toast`（showToast）。
- **业务组件**：`CartSellerGroup` 按卖家分组卡片；`CartItemRow` 单行商品（含选中+卡片+步进器）；`CartSettleBar` 底部结算栏；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：管理 `setting-o`；进店 `arrow`；删除 `delete-o`。
- **空状态**：空购物车 `van-empty`「购物车空空如也」+ 「去购物」按钮跳首页。

## 6. 视觉规范
- **主色应用**：结算按钮主色 `#1677FF`；选中框激活主色；店铺名链接主色。
- **状态色**：应付金额 `#FF4D4F`；失效标签 `#8C8C8C`；库存不足警告 `#FAAD14`；删除按钮 `#FF4D4F`。
- **间距**：卖家组间距 12px；卡片内边距 12px；底部结算栏高 50px。
- **字体**：应付金额 18px semibold `#FF4D4F`；商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；单价 14px `#000000D9`；失效原因 12px `#8C8C8C`。
- **图标尺寸**：复选框 20px；步进器按钮 28px；管理图标 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 3 行购物车卡片布局。
- **空数据**：`van-empty`「购物车空空如也」+ 「去购物」CTA。
- **错误态**：接口失败 `showToast` 「加载失败，下拉重试」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/cart`，登录成功后自动合并匿名购物车。
- **并发与乐观锁**：数量修改 300ms 防抖，避免频繁请求；操作冲突以服务端返回为准回滚前端状态。
- **危险操作确认**：删除商品使用 `showConfirmDialog`（标题「确认删除」，内容「删除后将无法恢复」，确认按钮红色「确认删除」）。

## 8. 验收要点
- [ ] 按卖家分组展示，组内商品可独立选中。
- [ ] 全选仅影响有效项，失效项保持不选中。
- [ ] 修改数量后小计与合计实时更新。
- [ ] 失效商品置灰且不可选中，不计入合计。
- [ ] 结算按钮显示选中数量并跳结算预览页。
- [ ] 空购物车展示「去购物」CTA。
- **性能要求**：首屏 < 1s；`van-stepper` 防抖 300ms；图片懒加载。
- **可访问性**：复选框 `aria-label`；结算按钮 `aria-label`；删除操作可键盘触发。
