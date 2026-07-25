# 售后详情 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：10-after-sales 售后
- **页面类型**：详情页
- **目标用户**：买家（Buyer）
- **核心目标**：买家查看售后单完整信息、进度时间轴、卖家/运营审核记录，并在不同状态下执行退货物流填写、撤销、查看退款结果等操作。
- **访问入口**：我的售后列表点击卡片；售后提交成功跳转；订单详情「查看售后」。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「售后详情」）+ 可滚动主体（状态头 + 进度时间轴 + 商品卡 + 申请信息 + 凭证 + 卖家回复 + 退款信息）+ 底部固定操作栏（状态相关按钮），无 Tabbar。
- **关键区域**：
  - 区域 A（状态头）：大字展示当前状态（如「待卖家审核」），下方简短说明文案（如「卖家将在 48 小时内处理」）。
  - 区域 B（进度时间轴）：`van-steps` 竖向展示售后流转节点（申请提交 → 卖家审核 → 买家退货 → 卖家确认 → 退款中 → 已完成），每个节点含时间与状态说明；驳回/撤销分支单独标注。
  - 区域 C（商品卡）：`van-card` 展示商品图 + 标题 + 规格 + 单价 + 数量。
  - 区域 D（申请信息）：`van-cell-group` 展示售后类型、原因、退款金额、问题描述、申请时间、售后单号（可复制）。
  - 区域 E（凭证图片）：`van-image` 网格展示上传的凭证图片，点击全屏预览。
  - 区域 F（卖家回复）：若卖家驳回，展示驳回原因 + 时间；若卖家同意，展示同意时间 + 应退金额。
  - 区域 G（退款信息）：退款中/已完成状态展示退款渠道 + 退款金额 + 退款到账时间。
  - 区域 H（底部操作栏）：状态相关按钮（待审核「撤销申请」、待退货「填写物流」、已完成「查看订单」）。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：状态头、进度时间轴首屏、商品卡、申请信息。
- **线框图描述**：
```
┌──────────────────┐
│ ←   售后详情      │
├──────────────────┤
│ 待卖家审核        │
│ 卖家将在48小时内处理│
├──────────────────┤
│ ● 申请提交        │
│ │ 07-26 10:00    │
│ ● 待卖家审核      │
│ │ 进行中          │
│ ○ 退款中          │
│ ○ 已完成          │
├──────────────────┤
│ [图] 商品标题     │
│      规格 红色 L  │
│      ¥99 × 1     │
├──────────────────┤
│ 类型 仅退款       │
│ 原因 商品损坏     │
│ 金额 ¥99.00      │
│ 描述 收到商品有划痕│
│ 单号 AS20260726 复制│
├──────────────────┤
│ 凭证 [图][图][图] │
├──────────────────┤
│   撤销申请        │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/after-sales/order/{orderId}` | 按订单查询售后单（含详情） | Buyer |
| POST | `/api/after-sales/{id}/cancel` | 撤销售后申请 | Buyer |
| POST | `/api/after-sales/{id}/return-goods` | 买家退货填写物流单号 | Buyer |
| GET | `/api/refunds/{afterSalesId}` | 查询退款结果 | Buyer |

- **请求参数**：`orderId` 路径参数（按订单查询）；撤销售后 body `{ reason }`；退货 body `{ trackingNo }`；退款查询路径参数 `afterSalesId`。
- **响应字段**：售后详情含 `afterSalesId`、`afterSalesNo`、`orderId`、`orderLineId`、`productId`、`productName`、`skuId`、`skuName`、`mainImage`、`price`、`quantity`、`type`、`reason`、`amount`、`description`、`images`、`status`、`timeline`（节点数组）、`sellerReply`、`sellerReplyAt`、`refundChannel`、`refundAmount`、`refundAt`、`createdAt`。
- **数据加载策略**：进入页面带 `orderId` 调 `GET /api/after-sales/order/{orderId}` 渲染详情；退款中状态轮询 `GET /api/refunds/{afterSalesId}`（每 5s，最多 12 次）。
- **缓存策略**：不缓存，每次进入页面重新拉取。

## 4. 交互流程
- **主流程**：
  1. 进入页面带 `orderId` → `GET /api/after-sales/order/{orderId}` → 渲染状态头、进度时间轴、商品卡、申请信息、凭证。
  2. 点击凭证图片 → `van-image-preview` 全屏预览，支持滑动切换。
  3. 点击售后单号「复制」→ `clipboard` 写入剪贴板 → `showToast` 「已复制」。
  4. 待审核状态点击「撤销申请」→ `van-dialog` 二次确认 → 输入撤销原因 → `POST /api/after-sales/{id}/cancel` → 成功 `showToast` 「撤销成功」→ 刷新详情。
  5. 待退货状态点击「填写物流」→ `van-popup` 弹出物流单号输入 → `POST /api/after-sales/{id}/return-goods` → 成功 `showToast` 「提交成功」→ 刷新详情。
  6. 退款中状态 → 轮询 `GET /api/refunds/{afterSalesId}` → 检测到退款成功 → 更新状态为已完成。
  7. 已完成状态点击「查看订单」→ 跳订单详情页。
- **分支流程**：
  - 售后单不存在：全屏错误 + 「返回」CTA。
  - 卖家驳回：时间轴标注驳回节点 + 卖家回复区展示驳回原因；底部显示「重新申请」按钮（跳售后申请页）。
  - 退款失败：状态头展示「退款失败」+ 失败原因 + 「联系客服」CTA。
- **跨页面流转**：跳订单详情页（已完成状态）；跳售后申请页（重新申请）；无其他跳转。
- **状态机可视化**：待卖家审核 → 待买家退货(退货退款) → 待卖家确认收货 → 退款中 → 已完成 / 已撤销 / 已驳回 / 退款失败。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-steps`、`van-step`、`van-card`、`van-cell-group`、`van-cell`、`van-image`（lazy-load + preview）、`van-image-preview`（showImagePreview）、`van-tag`、`van-button`、`van-popup`、`van-field`、`van-dialog`（showDialog）、`van-skeleton`、`van-toast`（showToast）、`van-empty`。
- **业务组件**：`AfterSalesStatusHeader` 状态头（含状态文案与说明）；`AfterSalesTimeline` 进度时间轴；`AfterSalesProductCard` 商品卡；`AfterSalesInfoGroup` 申请信息组；`EvidenceGallery` 凭证图片画廊；`SellerReplyCard` 卖家回复卡；`ReturnGoodsPopup` 退货物流弹层；`CancelAfterSalesDialog` 撤销确认弹层；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；复制 `description`；图片 `photo-o`。
- **空状态**：售后单不存在显示全屏错误 + 「返回」CTA。

## 6. 视觉规范
- **主色应用**：状态头大字主色 `#1677FF`；时间轴已完成节点主色；主操作按钮主色。
- **状态色**：待审核 `#FAAD14`；待退货/退款中 `#1677FF`；已完成 `#52C41A`；已撤销 `#8C8C8C`；已驳回/退款失败 `#FF4D4F`；撤销按钮 `#FF4D4F`。
- **间距**：区域间距 12px；卡片内边距 12px；凭证图片 96×96px；底部操作栏高 50px。
- **字体**：状态头大字 20px semibold；状态说明 13px `#595959`；商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；cell 标题 14px `#000000D9`；cell 值 14px `#595959`；时间 12px `#8C8C8C`；按钮 16px semibold `#FFFFFF`。
- **图标尺寸**：返回 20px；复制 16px；时间轴节点 14px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟状态头 + 时间轴 + 商品卡 + 信息组。
- **空数据**：售后单不存在显示全屏错误 + 「返回」CTA。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；退款查询失败不阻塞页面，仅静默重试。
- **权限控制**：Buyer 可见；售后单归属校验由服务端完成；只能查看本人售后单。
- **并发与乐观锁**：撤销/退货按钮点击后立即 disabled + loading；`Idempotency-Key` 头防重复提交；退款轮询失败不阻塞页面。
- **危险操作确认**：撤销售后需 `van-dialog` 二次确认，确认按钮 `danger` 类型，文案「撤销后无法恢复，确认撤销？」。

## 8. 验收要点
- [ ] 状态头展示当前状态与说明文案。
- [ ] 进度时间轴展示售后流转节点与时间。
- [ ] 商品卡展示商品图、标题、规格、单价、数量。
- [ ] 申请信息展示类型、原因、金额、描述、单号（可复制）。
- [ ] 凭证图片点击全屏预览，支持滑动切换。
- [ ] 卖家驳回展示驳回原因与时间。
- [ ] 待审核状态显示「撤销申请」按钮，二次确认后撤销成功。
- [ ] 待退货状态显示「填写物流」按钮，提交后状态更新。
- [ ] 退款中状态轮询退款结果，成功后更新为已完成。
- **性能要求**：首屏 < 1s；图片懒加载；退款轮询间隔 5s，最多 12 次。
- **可访问性**：时间轴 `role="list"`；图片 `alt` 文本；按钮 `aria-label`；对话框 `role="dialog"`。
