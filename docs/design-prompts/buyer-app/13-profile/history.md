# 浏览历史 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：13-profile 我的
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看商品浏览历史记录，按时间倒序分组展示，支持快速加购、清除单条/全部历史、跳转商品详情，便于回溯购物决策。
- **访问入口**：「我的」页 → 浏览历史；个人资料页「浏览历史」；URL `/profile/history`。
- **实现状态**：➕ 补充功能（API 未提供，需补充）

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「浏览历史」+ 右侧「管理」）+ 按日期分组的时间轴列表 + 底部固定「清空全部」按钮，无 Tabbar。
- **关键区域**：
  - 区域 A（日期分组头部）：每组以日期标签开头（今天/昨天/前天/具体日期如 07-24），右侧显示当天浏览数量。
  - 区域 B（商品横向滑动卡片）：每条历史为单列卡片，展示商品主图（左）+ 标题 + 价格 + 浏览时间 + 店铺名 + 「加入购物车」按钮；批量模式下卡片左上角显示 `van-checkbox`。
  - 区域 C（底部操作栏）：固定底部「清空全部」按钮（非批量模式）或「全选 + 删除 + 完成」（批量模式），适配 `safe-area-inset-bottom`。
  - 区域 D（空状态）：`van-empty`「暂无浏览记录」+ 「去逛逛」CTA。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、今天分组头部与首批浏览记录。
- **线框图描述**：
```
┌──────────────────┐
│ ←  浏览历史  管理 │
├──────────────────┤
│ 今天  3 件        │
├──────────────────┤
│ ┌──────────────┐ │
│ │[图] 商品标题  │ │
│ │     ¥99      │ │
│ │     10:30    │ │
│ │     [加购]    │ │
│ └──────────────┘ │
│ ┌──────────────┐ │
│ │[图] 商品标题  │ │
│ │     ¥199     │ │
│ │     09:15    │ │
│ └──────────────┘ │
├──────────────────┤
│ 昨天  2 件        │
├──────────────────┤
│ ┌──────────────┐ │
│ │[图] 商品标题  │ │
│ │     ¥49      │ │
│ │     21:00    │ │
│ └──────────────┘ │
├──────────────────┤
│   清空全部历史    │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：UserCenter 域（旧域 UserAuth 双轨兜底，端点路径不变；新域 `BrowseHistoryController` 已就绪，原 ➕ 状态更新为 ✅）
- **主要 API**（✅ 已实现，由 UserCenter 域 `BrowseHistoryController` 提供）：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/users/me/browse-history` | 分页查询浏览历史（按日期分组） | Buyer |
| POST | `/api/users/me/browse-history` | 记录一条浏览历史（body 含 `spuId`） | Buyer |
| DELETE | `/api/users/me/browse-history/{id}` | 删除单条浏览历史 | Buyer |
| POST | `/api/users/me/browse-history/batch-delete` | 批量删除浏览历史 | Buyer |
| DELETE | `/api/users/me/browse-history` | 清空全部浏览历史 | Buyer |

- **请求参数**：`GET /api/users/me/browse-history?page={page}&pageSize=20`，按 `viewedAt` 倒序；记录 body `{ spuId, source? }`；批量删除 body `{ ids: [...] }`。
- **响应字段**：列表含 `groups`（数组：`{ date, label, count, items }`），item 含 `historyId`、`spuId`、`spuTitle`、`mainImageUrl`、`price`、`shopId`、`shopName`、`viewedAt`；分页含 `total`、`page`、`pageSize`、`hasMore`。
- **数据加载策略**：`van-list` 无限滚动按分页加载，前端按 `date` 字段聚合为分组；下拉刷新。
- **缓存策略**：列表不缓存；浏览历史写入由商品详情页触发（用户进入商品详情时自动 POST 一条），客户端防抖 5s 内同一 SPU 不重复记录。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → `GET /api/users/me/browse-history?page=1` → 按 `date` 分组渲染（今天/昨天/具体日期）。
  2. 滚动到底部 → `van-list` load → 追加下一页并合并到对应日期分组。
  3. 点击商品卡片 → 跳 `/product/{spuId}` 商品详情页（同时自动 POST 一条新历史）。
  4. 点击卡片「加入购物车」→ 调用购物车添加接口（默认 SKU）→ `showToast` 「已加入购物车」。
  5. 点击「管理」→ 进入批量模式 → 卡片左上角显示 `van-checkbox` + 底部操作栏切换。
  6. 勾选若干历史 → 点击「全选」/「删除」→ `showConfirmDialog` 二次确认 → `POST /api/users/me/browse-history/batch-delete` → 刷新列表。
  7. 点击「清空全部」→ `showConfirmDialog` 二次确认（危险操作）→ `DELETE /api/users/me/browse-history` → 成功 `showToast` 「已清空」→ 显示空状态。
  8. 点击「完成」退出批量模式。
- **分支流程**：
  - 商品已下架：卡片置灰，主图加「已下架」水印，禁用「加入购物车」。
  - 历史为空：`van-empty`「暂无浏览记录」+ 「去逛逛」CTA 跳首页。
  - 批量删除失败：`showToast` 「删除失败」+ 重试。
  - 同一商品多次浏览：聚合到最新一条记录，原记录删除（去重）。
- **跨页面流转**：跳商品详情页（自动新增历史）。
- **状态机可视化**：浏览态 →（管理）→ 批量态 →（完成）→ 浏览态。历史存在 →（删除单条/批量/清空）→ 不存在。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-list`、`van-pull-refresh`、`van-card`（或自定义卡片）、`van-image`、`van-checkbox`、`van-checkbox-group`、`van-button`、`van-dialog`（showConfirmDialog）、`van-empty`、`van-skeleton`、`van-toast`（showToast）、`van-icon`、`van-tag`（已下架）、`van-sticky`（分组头部吸顶）。
- **业务组件**：`HistoryGroupHeader` 日期分组头部（含日期标签 + 浏览数量）；`HistoryProductCard` 历史商品卡片（含主图、标题、价格、时间、店铺、加购按钮、批量复选框）；`HistoryBatchBar` 批量操作栏（全选 + 删除 + 完成）；`ClearAllBar` 清空全部栏。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；管理 `setting-o`；完成 `success`；加购 `shopping-cart-o`；删除 `delete-o`；箭头 `arrow`。
- **空状态**：`van-empty`「暂无浏览记录」+ 「去逛逛」CTA 跳首页。

## 6. 视觉规范
- **主色应用**：价格主色 `#FF4D4F`；「加入购物车」按钮主色 `#1677FF`；分组日期标签主色 `#1677FF`。
- **状态色**：已下架灰 `#8C8C8C`；批量勾选主色 `#1677FF`；删除按钮红 `#FF4D4F`。
- **间距**：分组间距 12px；分组头部高 32px；卡片内边距 12px；卡片间距 8px；底部栏高 56px + `safe-area-inset-bottom`。
- **字体**：分组日期 14px medium `#1677FF`；浏览数量 12px `#8C8C8C`；商品标题 14px `#000000D9`（1 行省略）；价格 16px semibold `#FF4D4F`；浏览时间 12px `#8C8C8C`；店铺名 12px `#8C8C8C`。
- **图标尺寸**：返回 20px；管理 20px；加购 16px；删除 16px；批量勾选 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 3 张历史卡片。
- **空数据**：`van-empty`「暂无浏览记录」+ 「去逛逛」CTA 跳首页。
- **错误态**：查询失败 `showToast` 「加载失败」+ 重试按钮；删除失败 `showToast` 「删除失败」+ 重试；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/profile/history`。
- **并发与乐观锁**：单条删除乐观更新（立即移除卡片），失败回滚；批量删除与清空按钮点击后立即 disabled + loading；`Idempotency-Key` 头防重复提交；写入历史客户端防抖 5s。
- **危险操作确认**：
  - 批量删除：`showConfirmDialog` 标题「确认删除」、内容「将删除已选 {N} 条浏览记录，此操作不可恢复。」、确认按钮红色 `#FF4D4F`。
  - 清空全部：`showConfirmDialog` 标题「确认清空」、内容「将清空全部浏览历史记录，此操作不可恢复。」、确认按钮红色 `#FF4D4F`。

## 8. 验收要点
- [ ] 浏览历史按日期分组展示（今天/昨天/前天/具体日期）。
- [ ] 分组头部展示日期标签与浏览数量。
- [ ] 商品卡片展示主图、标题、价格、浏览时间、店铺、加购按钮。
- [ ] 点击商品卡片跳商品详情页并自动新增一条历史。
- [ ] 单条删除乐观移除，失败回滚。
- [ ] 批量模式勾选、全选、批量删除。
- [ ] 「清空全部」二次确认，确认按钮红色危险色。
- [ ] 已下架商品置灰显示「已下架」水印，禁用「加入购物车」。
- [ ] 空列表展示「去逛逛」CTA。
- [ ] 同一商品 5s 内重复浏览不重复记录。
- [ ] 操作防重复（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 1s；列表无限滚动无卡顿；分页 pageSize=20；单条删除响应 < 500ms。
- **可访问性**：卡片 `role="article"`；按钮 `aria-label`；批量复选框 `aria-label`；分组 `role="group"`。
