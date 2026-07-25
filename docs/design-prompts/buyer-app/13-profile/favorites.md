# 商品收藏 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：13-profile 我的
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看已收藏的商品列表，支持快速加入购物车、取消收藏、跳转商品详情，并按价格/销量/收藏时间排序与筛选。
- **访问入口**：「我的」页 → 商品收藏；个人资料页「商品收藏」；商品详情页收藏后跳转；URL `/profile/favorites`。
- **实现状态**：➕ 补充功能（API 未提供，需补充）

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「商品收藏」+ 右侧「管理」）+ `van-tabs` 排序筛选（综合/价格/销量/最新）+ `van-list` 商品卡片瀑布流，无 Tabbar。
- **关键区域**：
  - 区域 A（排序筛选 Tab）：`van-tabs` 4 个标签（综合/价格/销量/最新），价格 Tab 切换升序/降序；右上角「管理」按钮进入批量编辑模式。
  - 区域 B（商品卡片）：双列瀑布流卡片，展示商品主图 + 标题（2 行省略）+ 价格（主色）+ 店铺名 + 收藏图标（已收藏红色心形）；批量模式下卡片左上角显示 `van-checkbox`。
  - 区域 C（底部操作栏）：批量模式下固定底部「全选」「取消收藏」「加入购物车」按钮，适配 `safe-area-inset-bottom`。
  - 区域 D（空状态）：`van-empty`「暂无收藏」+ 「去逛逛」CTA 跳首页。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、排序筛选 Tab、商品卡片首屏（双列）。
- **线框图描述**：
```
┌──────────────────┐
│ ←  商品收藏  管理 │
├──────────────────┤
│综合 价格 销量 最新│
├──────────────────┤
│ ┌────┐  ┌────┐  │
│ │图 │  │图 │  │
│ │    │  │    │  │
│ │商品│  │商品│  │
│ │¥99 │  │¥199│  │
│ │❤️  │  │❤️  │  │
│ └────┘  └────┘  │
│ ┌────┐  ┌────┐  │
│ │图 │  │图 │  │
│ │    │  │    │  │
│ │商品│  │商品│  │
│ │¥49 │  │¥299│  │
│ └────┘  └────┘  │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**（➕ 补充，需后端新增收藏控制器 `FavoritesController`）：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/users/me/favorites` | 分页查询我的收藏列表 | Buyer |
| POST | `/api/users/me/favorites` | 收藏商品（body 含 `spuId`） | Buyer |
| DELETE | `/api/users/me/favorites/{spuId}` | 取消收藏单个商品 | Buyer |
| POST | `/api/users/me/favorites/batch-delete` | 批量取消收藏 | Buyer |
| GET | `/api/users/me/favorites/count` | 查询收藏总数（角标） | Buyer |

- **请求参数**：`GET /api/users/me/favorites?sort={sort}&order={order}&page={page}&pageSize=20`，sort 取 `comprehensive/price/sales/created`，order 取 `asc/desc`；收藏 body `{ spuId }`；批量取消 body `{ spuIds: [...] }`。
- **响应字段**：列表含 `items`（每项含 `favoriteId`、`spuId`、`spuTitle`、`mainImageUrl`、`price`、`originalPrice?`、`shopId`、`shopName`、`salesCount`、`stockStatus`）、`total`、`page`、`pageSize`；计数返回 `int`。
- **数据加载策略**：`van-list` 无限滚动，每页 20 条；切换 Tab 重置列表；下拉刷新。
- **缓存策略**：不缓存列表，每次进入页面重新拉取；收藏总数缓存于 Pinia `useFavoritesStore` 30s，用于「我的」页角标。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认「综合」排序 → `GET /api/users/me/favorites?sort=comprehensive&page=1` → 渲染双列商品卡片。
  2. 切换排序 Tab → 重置列表 → `GET /api/users/me/favorites?sort={sort}&order={order}&page=1`。
  3. 滚动到底部 → `van-list` load → 追加下一页。
  4. 点击商品卡片 → 跳 `/product/{spuId}` 商品详情页。
  5. 点击卡片右下角「心形」→ `DELETE /api/users/me/favorites/{spuId}` → 乐观移除卡片 + `showToast` 「已取消收藏」；失败回滚。
  6. 点击「管理」→ 进入批量模式 → 卡片左上角显示 `van-checkbox` + 底部操作栏出现。
  7. 勾选若干商品 → 点击「全选」全选/取消全选 → 点击「取消收藏」→ `showConfirmDialog` 二次确认 → `POST /api/users/me/favorites/batch-delete` → 刷新列表。
  8. 勾选商品 → 点击「加入购物车」→ 调用购物车添加接口（默认 SKU）→ `showToast` 「已加入购物车」。
  9. 点击「完成」退出批量模式。
- **分支流程**：
  - 商品已下架：卡片置灰，主图加「已下架」水印，禁用「加入购物车」按钮。
  - 商品价格变更：展示新价 + 划线原价。
  - 收藏为空：`van-empty`「暂无收藏」+ 「去逛逛」CTA 跳首页。
  - 批量取消失败：`showToast` 「取消失败」+ 重试。
- **跨页面流转**：跳商品详情页；加入购物车后可跳购物车页或停留。
- **状态机可视化**：未收藏 →（收藏）→ 已收藏(红心)；已收藏 →（取消）→ 未收藏。批量模式：浏览态 →（管理）→ 批量态 →（完成）→ 浏览态。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-tabs`、`van-tab`、`van-list`、`van-pull-refresh`、`van-card`（或自定义卡片）、`van-image`、`van-checkbox`、`van-checkbox-group`、`van-button`、`van-dialog`（showConfirmDialog）、`van-empty`、`van-skeleton`、`van-toast`（showToast）、`van-icon`、`van-tag`（已下架）。
- **业务组件**：`FavoriteProductCard` 收藏商品卡片（双列瀑布流，含主图、标题、价格、店铺、心形按钮、批量复选框）；`BatchActionBar` 批量操作栏（全选 + 取消收藏 + 加入购物车 + 完成）；`SortTabs` 排序 Tab（含价格升降序切换）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；管理 `setting-o`；完成 `success`；心形已收藏 `like`（红）、未收藏 `like-o`；箭头 `arrow`。
- **空状态**：`van-empty`「暂无收藏」+ 「去逛逛」CTA 跳首页。

## 6. 视觉规范
- **主色应用**：价格主色 `#FF4D4F`（电商价格惯例红）；已收藏心形 `#FF4D4F`；批量模式操作栏「取消收藏」红色「加入购物车」主色。
- **状态色**：已收藏红 `#FF4D4F`；已下架灰 `#8C8C8C`；批量勾选主色 `#1677FF`。
- **间距**：双列卡片间距 8px；卡片内边距 8px；底部操作栏高 56px + `safe-area-inset-bottom`。
- **字体**：商品标题 13px `#000000D9`（2 行省略）；价格 16px semibold `#FF4D4F`；原价 12px line-through `#8C8C8C`；店铺名 12px `#8C8C8C`。
- **图标尺寸**：返回 20px；管理 20px；心形 20px；批量勾选 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 4 张商品卡片（2×2 网格）。
- **空数据**：`van-empty`「暂无收藏」+ 「去逛逛」CTA 跳首页。
- **错误态**：查询失败 `showToast` 「加载失败」+ 重试按钮；取消收藏失败回滚 + `showToast` 「取消失败」；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/profile/favorites`。
- **并发与乐观锁**：单条取消收藏乐观更新（立即移除卡片），失败回滚；批量取消按钮点击后立即 disabled + loading；`Idempotency-Key` 头防重复提交。
- **危险操作确认**：批量取消收藏 `showConfirmDialog` 标题「确认取消收藏」、内容「将取消已选 {N} 件商品的收藏，此操作可重新收藏。」、确认按钮红色 `#FF4D4F`。

## 8. 验收要点
- [ ] 商品收藏列表双列瀑布流展示，支持无限滚动。
- [ ] 排序 Tab 切换（综合/价格升降/销量/最新）正确刷新列表。
- [ ] 点击商品卡片跳商品详情页。
- [ ] 单条取消收藏乐观移除，失败回滚。
- [ ] 批量模式勾选、全选、批量取消收藏、批量加入购物车。
- [ ] 批量取消收藏需二次确认。
- [ ] 已下架商品置灰显示「已下架」水印，禁用「加入购物车」。
- [ ] 空列表展示「去逛逛」CTA。
- [ ] 收藏总数角标缓存 30s 同步「我的」页。
- [ ] 操作防重复（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 1s；列表无限滚动无卡顿；分页 pageSize=20；单条取消响应 < 500ms。
- **可访问性**：卡片 `role="article"`；心形按钮 `aria-label="取消收藏"`；批量复选框 `aria-label`；Tab `role="tab"`。
