# 首页推荐流 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：02-home 首页
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家在首页一站式浏览 Banner、秒杀入口、分类快捷入口、公告条与推荐商品流，快速进入商品详情或秒杀活动。
- **访问入口**：底部 Tabbar「首页」；启动 App 默认页；其他页面返回首页。
- **实现状态**：➕ 补充功能（推荐流整合为首页，复用已实现端点）

## 2. 页面布局与信息架构
- **整体布局**：顶部固定搜索栏 + 公告条 + `van-swipe` Banner + 秒杀入口横滑 + 分类快捷入口 + 推荐商品 `van-list` 无限滚动，底部 Tabbar。下拉刷新整体。
- **关键区域**：
  - 区域 A（搜索栏）：固定顶部，左侧 Logo 小图标 + `van-search` 占位「搜索商品」点击跳 `/search`，右侧通知铃铛带未读 `van-badge`。
  - 区域 B（公告条）：`van-notice-bar` 滚动展示已发布公告，点击跳 `/announcements`。
  - 区域 C（Banner）：`van-swipe` 高度 160px，自动播放 3s，指示器主色，点击跳对应活动/商品页。
  - 区域 D（秒杀入口）：标题「限时秒杀」+「查看更多」+ 横滑 `van-swipe` 卡片（含倒计时、商品图、秒杀价），点击跳 `/seckill/order/:activityId`。
  - 区域 E（分类快捷入口）：4×2 网格图标入口，点击跳 `/category?categoryId=xxx`。
  - 区域 F（推荐流）：`van-list` 双列瀑布流商品卡片，下拉刷新 + 上拉加载。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：搜索栏、公告条、Banner、秒杀入口前 4 个、分类入口、推荐流前 6 个商品。
- **线框图描述**：
```
┌──────────────────┐
│ 🔍搜索商品   🔔(3)│
│ 📢 公告滚动...   │
├──────────────────┤
│ [Banner 轮播]    │
├──────────────────┤
│ 限时秒杀  查看更多│
│ [卡][卡][卡][卡]→│
├──────────────────┤
│ [📱][👗][💻][🏠] │
│ [💄][👟][🎁][📚] │
├──────────────────┤
│ 为你推荐         │
│ [商品] [商品]    │
│ [商品] [商品]    │
│ ... 加载中       │
├──────────────────┤
│首页 分类 购物车 我的│
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/products/search` | 推荐商品流（按热度/综合排序） | Buyer |
| GET | `/api/seckill/activities` | 进行中秒杀活动列表 | Buyer |
| GET | `/api/categories/tree` | 分类快捷入口数据 | Buyer |
| GET | `/api/announcements` | 公告条滚动内容 | Buyer |
| GET | `/api/notifications/unread-count` | 通知铃铛未读数 | Buyer |

- **请求参数**：推荐流 `ProductSearchQueryDto`（page、pageSize=10、sort=hot）；秒杀 `GetActiveAsync` 无参；公告 page=1、pageSize=5。
- **响应字段**：`ProductSearchResult` 含 `items`（id、name、mainImageUrl、price、salesCount）、`total`；`SeckillActivityDto` 含 `id`、`name`、`endTime`、`seckillPrice`、`originalPrice`、`imageUrl`；`CategoryDto` 含 `id`、`name`、`iconUrl`、`children`；`AnnouncementListResultDto` 含 `items`（id、title、type）。
- **数据加载策略**：进入页面并行加载秒杀、分类、公告、推荐流首批；推荐流 `van-list` 上拉加载下一页；下拉刷新重置首页。
- **缓存策略**：分类树缓存 Pinia 1 小时（key: `home_categories`）；公告缓存 5 分钟；推荐流不缓存（实时性要求）。

## 4. 交互流程
- **主流程**：
  1. 进入首页 → 显示骨架屏 → 并行调用 5 个端点 → 数据返回渲染各区域。
  2. 点击搜索栏 → 跳 `/search`。
  3. 点击公告条 → 跳 `/announcements`。
  4. 点击 Banner → 按 linkType 跳活动/商品页。
  5. 点击秒杀卡片 → 跳 `/seckill/order/:activityId`。
  6. 点击分类入口 → 跳 `/category?categoryId=xxx`。
  7. 滚动到底 → `van-list` 加载下一页推荐商品。
  8. 下拉刷新 → 重置所有数据。
- **分支流程**：
  - 秒杀活动为空：隐藏秒杀入口区。
  - 推荐流加载完：`van-list` 显示「没有更多了」。
  - 通知未读数 0：铃铛不显示 badge。
- **跨页面流转**：点击商品卡片跳 `/product/:id`；点击秒杀跳秒杀下单页。
- **状态机可视化**：加载中(skeleton) → 加载完成(渲染) / 加载失败(错误重试)。

## 5. 组件清单
- **基础组件**：`van-search`、`van-notice-bar`、`van-swipe`、`van-swipe-item`、`van-list`、`van-pull-refresh`、`van-badge`、`van-skeleton`、`van-image`（lazy-load）。
- **业务组件**：`SeckillCard` 自研秒杀卡片（含倒计时）；`ProductCard` 自研商品卡片；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：分类图标来自 `CategoryDto.iconUrl`；通知 `bell`；搜索 `search`。
- **空状态**：推荐流为空使用 `EmptyState title="暂无推荐" ctaText="去逛逛"`。

## 6. 视觉规范
- **主色应用**：搜索栏图标、Banner 指示器、秒杀价、分类入口图标背景主色 `#1677FF`。
- **状态色**：秒杀倒计时 `#FF4D4F`；秒杀价 `#FF4D4F`；原价划线 `#8C8C8C`。
- **间距**：区域间距 12px；卡片内边距 12px；商品卡片间距 8px。
- **字体**：区域标题 16px semibold；商品名 14px `#000000D9`；价格 16px semibold `#FF4D4F`；辅助 12px `#8C8C8C`。
- **图标尺寸**：分类入口 48px；秒杀卡片商品图 100×100。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟各区域布局；推荐流底部 `van-list` loading。
- **空数据**：推荐流空用 `EmptyState`；秒杀空隐藏区域。
- **错误态**：单个区域加载失败显示该区域错误 + 重试按钮，不影响其他区域。
- **权限控制**：全页需 Buyer 登录；未登录跳 `/login`。
- **并发与乐观锁**：5 个端点并行请求互不阻塞；下拉刷新取消未完成请求。
- **危险操作确认**：不涉及。

## 8. 验收要点
- [ ] 首屏骨架屏显示，数据返回后平滑替换。
- [ ] 5 个区域并行加载，单区域失败不影响其他。
- [ ] 推荐流 `van-list` 上拉加载、下拉刷新正常。
- [ ] 秒杀倒计时实时刷新，结束自动隐藏。
- [ ] 通知铃铛未读数实时显示。
- **性能要求**：首屏 < 1.5s；推荐流分页加载 < 800ms；图片懒加载。
- **可访问性**：搜索框 `aria-label`；Banner 有 `alt`；图标按钮可键盘聚焦。
