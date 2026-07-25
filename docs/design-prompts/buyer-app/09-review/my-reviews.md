# 我的评价 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：09-review 评价
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看自己已提交的评价列表，了解审核状态（待审核/已通过/已驳回），并可对已通过评价追评。
- **访问入口**：「我的」页评价入口；评价提交成功后跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回+标题「我的评价」）+ `van-tabs` 状态筛选（全部/待审核/已通过/已驳回）+ `van-list` 评价卡片列表，无 Tabbar。
- **关键区域**：
  - 区域 A（状态筛选 Tab）：`van-tabs` 4 个标签，切换后重新加载对应状态评价。
  - 区域 B（评价卡片）：每张卡片展示商品图+标题+规格+评分+评价内容+图片缩略图+审核状态标签+提交时间；已通过评价显示「追评」按钮。
  - 区域 C（空状态）：`van-empty`「暂无评价」+ 「去逛逛」CTA。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、状态筛选 Tab、评价卡片首屏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   我的评价      │
├──────────────────┤
│全部 待审 已通过 驳回│
├──────────────────┤
│ [图] 商品标题     │
│      规格 红色 L  │
│ ★★★★★ 已通过     │
│ 商品质量很好...   │
│ [图][图]          │
│ 07-26 10:00  追评 │
├──────────────────┤
│ [图] 商品标题     │
│      规格 蓝色 M  │
│ ★★★   待审核     │
│ 一般般...         │
│ 07-25 14:00       │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/reviews/mine` | 查询我的评价列表（按状态过滤） | Buyer |
| POST | `/api/reviews/{reviewId}/append` | 追评 | Buyer |

- **请求参数**：`GET /api/reviews/mine?status={ReviewStatus}&page={page}&pageSize=20`；status 可空表示全部，枚举 `PendingReview/Approved/Rejected`；追评 body `{ content, images }`。
- **响应字段**：`{ items, total, page, pageSize }`；item 含 `reviewId`、`productId`、`productName`、`skuId`、`skuName`、`mainImage`、`rating`、`content`、`images`、`status`、`createdAt`、`appendContent`、`appendAt`。
- **数据加载策略**：`van-list` 无限滚动，每页 20 条；切换 Tab 重置列表；下拉刷新。
- **缓存策略**：不缓存，每次切换 Tab 重新拉取。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认「全部」Tab → `GET /api/reviews/mine?page=0` → 渲染评价卡片。
  2. 切换状态 Tab → 重置列表 → `GET /api/reviews/mine?status={status}&page=0`。
  3. 滚动到底部 → `van-list` load → 追加下一页。
  4. 点击图片缩略图 → 全屏预览。
  5. 已通过评价点击「追评」→ `van-popup` 弹出追评输入框 → 输入内容 → `POST /api/reviews/{reviewId}/append` → 成功 `showToast` 「追评成功」+ 更新卡片。
  6. 点击商品图 → 跳商品详情页。
- **分支流程**：
  - 空列表：`van-empty`「暂无评价」+ 「去逛逛」CTA。
  - 已驳回评价：展示驳回原因，无追评按钮。
  - 待审核评价：无追评按钮，提示「审核通过后可追评」。
- **跨页面流转**：商品详情页；评价追评成功留在本页。
- **状态机可视化**：待审核 → 已通过(可追评) / 已驳回(展示原因)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-tabs`、`van-tab`、`van-list`、`van-pull-refresh`、`van-card`、`van-image`（lazy-load + preview）、`van-rate`（只读）、`van-tag`、`van-button`、`van-popup`、`van-field`、`van-empty`、`van-skeleton`、`van-toast`（showToast）。
- **业务组件**：`MyReviewCard` 我的评价卡片（含状态标签与追评按钮）；`AppendReviewPopup` 追评弹层；`ReviewStatusTag` 审核状态标签；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；箭头 `arrow`；图片 `photo-o`。
- **空状态**：`van-empty`「暂无评价」+ 「去逛逛」CTA 跳首页。

## 6. 视觉规范
- **主色应用**：追评按钮主色 `#1677FF`；Tab 激活态主色；评分激活态主色。
- **状态色**：已通过 `#52C41A`；待审核 `#FAAD14`；已驳回 `#FF4D4F`；评分星 `#FAAD14`；商品标题 `#000000D9`。
- **间距**：卡片间距 12px；卡片内边距 12px；图片缩略图 72×72px。
- **字体**：商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；评价内容 14px `#595959`；时间 12px `#8C8C8C`；状态标签 12px；按钮 12px。
- **图标尺寸**：返回 20px；箭头 16px；图片 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 3 张评价卡片。
- **空数据**：`van-empty`「暂无评价」+ 「去逛逛」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/reviews/mine`。
- **并发与乐观锁**：追评按钮点击后立即 disabled + loading；`Idempotency-Key` 头防重复追评。
- **危险操作确认**：不涉及。

## 8. 验收要点
- [ ] 状态 Tab 切换后列表正确筛选。
- [ ] 评价卡片展示商品、评分、内容、图片、状态。
- [ ] 图片缩略图点击全屏预览。
- [ ] 已通过评价显示「追评」按钮，追评成功更新卡片。
- [ ] 已驳回评价展示驳回原因，无追评按钮。
- [ ] 空列表展示「去逛逛」CTA。
- **性能要求**：首屏 < 1s；图片懒加载；列表无限滚动无卡顿。
- **可访问性**：Tab `role="tab"`；卡片 `role="article"`；按钮 `aria-label`。
