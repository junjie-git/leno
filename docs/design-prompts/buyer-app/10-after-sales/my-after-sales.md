# 我的售后 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：10-after-sales 售后
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看自己提交的全部售后单列表，按状态筛选，进入详情查看进度或执行退货/撤销操作。
- **访问入口**：「我的」页售后入口；订单详情「查看售后」；售后提交成功跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「我的售后」）+ `van-tabs` 状态筛选（全部/待审核/待退货/退款中/已完成/已取消）+ `van-list` 售后卡片无限滚动，无 Tabbar。
- **关键区域**：
  - 区域 A（状态筛选 Tab）：`van-tabs` 6 个标签，切换后重新加载对应状态售后单。
  - 区域 B（售后卡片）：每张卡片展示售后单号 + 商品图 + 标题 + 规格 + 售后类型 + 退款金额 + 状态标签 + 申请时间；状态相关快捷操作按钮（待退货显示「填写物流」、待审核显示「撤销」、已完成显示「查看详情」）。
  - 区域 C（空状态）：`van-empty`「暂无售后单」+ 「去逛逛」CTA。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、状态筛选 Tab、售后卡片首屏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   我的售后      │
├──────────────────┤
│全部 待审 待退 退款中│
│完成 取消          │
├──────────────────┤
│ 售后单 AS20260726  │
│ [图] 商品标题      │
│      规格 红色 L   │
│ 仅退款  ¥99.00    │
│ 待卖家审核         │
│ 07-26 10:00       │
│      撤销 查看详情 │
├──────────────────┤
│ 售后单 AS20260725  │
│ [图] 商品标题      │
│ 退货退款  ¥199.00 │
│ 待买家退货         │
│ 07-25 14:00       │
│      填写物流     │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：AfterSales 域（旧域 ReviewAfterSales 双轨兜底，端点路径不变）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/after-sales/mine` | 查询我的售后单分页列表 | Buyer |
| POST | `/api/after-sales/{id}/cancel` | 撤销售后申请 | Buyer |
| POST | `/api/after-sales/{id}/return-goods` | 买家退货填写物流单号 | Buyer |

- **请求参数**：`GET /api/after-sales/mine?page={page}&pageSize=20`；服务端按用户筛选，不支持状态过滤（前端过滤或全量拉取）；撤销售后 body `{ reason }`；退货 body `{ trackingNo }`。
- **响应字段**：`{ items, total, page, pageSize }`；item 含 `afterSalesId`、`afterSalesNo`、`orderId`、`orderLineId`、`productId`、`productName`、`skuId`、`skuName`、`mainImage`、`type`、`reason`、`amount`、`status`、`createdAt`、`updatedAt`。
- **数据加载策略**：`van-list` 无限滚动，每页 20 条；切换 Tab 前端过滤已加载数据 + 服务端分页；下拉刷新。
- **缓存策略**：不缓存，每次进入页面重新拉取。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认「全部」Tab → `GET /api/after-sales/mine?page=1` → 渲染售后卡片。
  2. 切换状态 Tab → 前端过滤已加载数据（如待审核 `status=PendingSellerReview`）→ 若不足一页则继续拉取下一页。
  3. 滚动到底部 → `van-list` load → 追加下一页。
  4. 点击售后卡片 → 跳售后详情页（带 `afterSalesId`）。
  5. 待审核状态点击「撤销」→ `van-dialog` 二次确认 → `POST /api/after-sales/{id}/cancel` → 成功 `showToast` 「撤销成功」+ 移除卡片或更新状态。
  6. 待买家退货状态点击「填写物流」→ `van-popup` 弹出物流单号输入 → `POST /api/after-sales/{id}/return-goods` → 成功 `showToast` 「提交成功」+ 更新状态为待卖家确认。
- **分支流程**：
  - 空列表：`van-empty`「暂无售后单」+ 「去逛逛」CTA 跳首页。
  - 撤销失败（状态已变更）：`showToast` 「售后单状态已变更」+ 刷新列表。
  - 退货失败（物流单号无效）：`showToast` 「物流单号无效」+ 留在弹层。
- **跨页面流转**：跳售后详情页（带 `afterSalesId`）；撤销/退货成功留在本页。
- **状态机可视化**：待卖家审核 → 待买家退货(退货退款) → 待卖家确认收货 → 退款中 → 已完成 / 已撤销 / 已驳回。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-tabs`、`van-tab`、`van-list`、`van-pull-refresh`、`van-card`、`van-image`（lazy-load）、`van-tag`、`van-button`、`van-popup`、`van-field`、`van-dialog`（showDialog）、`van-empty`、`van-skeleton`、`van-toast`（showToast）。
- **业务组件**：`MyAfterSalesCard` 我的售后卡片（含状态标签与快捷操作）；`AfterSalesStatusTag` 售后状态标签；`ReturnGoodsPopup` 退货物流弹层；`CancelAfterSalesDialog` 撤销确认弹层；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；箭头 `arrow`。
- **空状态**：`van-empty`「暂无售后单」+ 「去逛逛」CTA 跳首页。

## 6. 视觉规范
- **主色应用**：「填写物流」「查看详情」按钮主色 `#1677FF`；Tab 激活态主色。
- **状态色**：待审核 `#FAAD14`；待退货 `#1677FF`；退款中 `#1677FF`；已完成 `#52C41A`；已取消 `#8C8C8C`；已驳回 `#FF4D4F`；撤销按钮 `#FF4D4F`。
- **间距**：卡片间距 12px；卡片内边距 12px；商品图 72×72px；按钮组间距 8px。
- **字体**：售后单号 12px `#8C8C8C`；商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；售后类型 13px `#595959`；退款金额 16px semibold `#FF4D4F`；状态标签 12px；时间 12px `#8C8C8C`；按钮 12px。
- **图标尺寸**：返回 20px；箭头 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 3 张售后卡片。
- **空数据**：`van-empty`「暂无售后单」+ 「去逛逛」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/after-sales/mine`；只能查看本人售后单。
- **并发与乐观锁**：撤销/退货按钮点击后立即 disabled + loading；`Idempotency-Key` 头防重复提交。
- **危险操作确认**：撤销售后需 `van-dialog` 二次确认，确认按钮 `danger` 类型，文案「撤销后无法恢复，确认撤销？」。

## 8. 验收要点
- [ ] 状态 Tab 切换后列表正确筛选。
- [ ] 售后卡片展示商品、类型、金额、状态、时间。
- [ ] 待审核状态显示「撤销」按钮，二次确认后撤销成功。
- [ ] 待退货状态显示「填写物流」按钮，提交后状态更新。
- [ ] 撤销/退货操作防重复提交。
- [ ] 空列表展示「去逛逛」CTA。
- **性能要求**：首屏 < 1s；图片懒加载；列表无限滚动无卡顿；分页 pageSize=20。
- **可访问性**：Tab `role="tab"`；卡片 `role="article"`；按钮 `aria-label`；对话框 `role="dialog"`。
