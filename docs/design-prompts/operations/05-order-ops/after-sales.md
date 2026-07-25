# 售后处理 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：05-order-ops 订单运营
- **页面类型**：列表审核页（运营介入售后）
- **目标用户**：运营管理员（Operator）
- **核心目标**：分页查询全平台售后单，对买卖家未达成一致的售后单进行运营介入审核（通过/驳回），保障售后链路公平。
- **访问入口**：左侧菜单「订单运营 → 售后处理」；待办工作台「待运营介入售后」徽标跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 售后单列表表格 + 售后详情抽屉（含协商记录）+ 审核对话框。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含售后单号、订单号、买家 ID、卖家 ID、售后状态、售后类型（退款/退货退款）、时间范围、查询/重置
  - 区域 B（工具栏）：导出、刷新、统计概览（各状态计数）
  - 区域 C（售后表格）：`<a-table>` 列含售后单号、订单号、买家、卖家、商品、申请金额、售后类型、状态、申请时间、操作列
  - 区域 D（详情抽屉）：`<a-drawer width="800">` 展示售后基础信息、商品信息、申请原因、凭证图片、协商记录时间线（买家发起/卖家审核/买家退货/卖家收货）、运营介入操作区
  - 区域 E（审核对话框）：`<a-modal>` 含审核结果（通过/驳回）、审核金额（通过时可调整）、原因（驳回必填）
- **响应式断点**：≥1200px 抽屉 800px；992-1199px 抽屉 600px。
- **首屏内容**：筛选条 + 待运营介入状态的售后单前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [售后单号][订单号][买家][卖家][状态▼][类型▼] [查询]│
├──────────────────────────────────────────────────┤
│ 待审核:8 退款中:15 已完成:156         [刷新][导出]│
├──────────────────────────────────────────────────┤
│ 售后号  订单号  买家 卖家 商品 金额 类型 状态 操作│
│ AS2026 NO2026 买家A 卖家X iPhone ¥3999 退货退款 待介入[详情][通过][驳回]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/after-sales` | 分页查询全平台售后单（多维度过滤） | Operator, Admin |
| POST | `/api/admin/after-sales/{id}/approve` | 运营审核通过售后 | Operator, Admin |
| POST | `/api/admin/after-sales/{id}/reject` | 运营驳回售后 | Operator, Admin |

- **请求参数**：查询参数 `orderId`（Guid?）、`userId`（Guid?）、`sellerId`（Guid?）、`status`（AfterSalesStatus?）、`page`、`pageSize`；通过请求体 `ApproveAfterSalesDto` 含 `ApprovedAmount`；驳回请求体 `RejectAfterSalesDto` 含 `Reason`（必填）。
- **响应字段**：`AfterSalesListResultDto`，含 `Items`（每项含 `Id`、`AfterSalesNo`、`OrderId`、`UserId`、`SellerId`、`Type`、`Status`、`ApplyAmount`、`Reason`、`CreatedAt`）、`Total`。
- **数据加载策略**：进入页面加载待运营介入售后单；切换筛选重新请求；审核后局部更新状态列。
- **缓存策略**：不缓存，售后状态实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载待运营介入售后单 → 渲染表格与统计概览
  2. 点击「详情」→ 打开抽屉展示协商记录时间线与凭证图片
  3. 点击「通过」→ 弹出对话框 → 输入审核金额（可调整，不超申请金额）→ 调用 approve → 触发退款流程
  4. 点击「驳回」→ 弹出对话框 → 填写原因 → 调用 reject → 状态变更为已驳回
- **分支流程**：
  - 审核金额校验：须大于 0 且不超申请金额
  - 凭证图片预览：`<a-image-preview>` 支持多图轮播
  - 协商记录：时间线展示买家/卖家/运营各节点操作
- **跨页面流转**：点击「订单号」跳转订单管理（携带订单筛选）；点击「买家」跳转用户管理。
- **状态机可视化**：Pending（待审核）→ SellerApproved（卖家同意）→ ReturnShipping（退货中）→ SellerReceived（卖家收货）→ 运营介入 AdminApproved/AdminRejected → Refunded（已退款）/ Rejected（已驳回）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-drawer>`、`<a-modal>`、`<a-textarea>`、`<a-input-number>`、`<a-timeline>`、`<a-image-preview>`、`<a-statistic>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 售后状态展示，type='afterSales'
  - `IdempotencyButton`（见 shared/components.md §2）— 通过/驳回按钮
  - `PermissionGuard`（见 shared/components.md §3）— 审核权限控制，permission='after-sales:audit'
  - `DataTable`（见 shared/components.md §6）— 售后单列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 时间范围筛选
  - `ConfirmDialog`（见 shared/components.md §10）— 通过/驳回二次确认
  - `EmptyState`（见 shared/components.md §5）— 无售后单时展示
- **图标使用**：`CheckOutlined` 通过、`CloseOutlined` 驳回、`EyeOutlined` 详情、`PictureOutlined` 凭证图片
- **空状态**：`EmptyState` title="暂无售后单"

## 6. 视觉规范
- **主色应用**：通过按钮主色 `#1677FF`，驳回按钮危险色 `#FF4D4F`。
- **状态色**：待审核 `#FAAD14` 橙、待介入 `#722ED1` 紫、已退款 `#52C41A` 绿、已驳回 `#FF4D4F` 红。
- **金额色**：`#FF4D4F` 14px medium。
- **间距**：筛选条与表格 16px，统计概览卡片间距 24px，表格行高 48px，抽屉时间线节点间距 16px。
- **字体**：售后单号 14px mono，金额 14px `#FF4D4F`，时间 12px `#8C8C8C`。
- **凭证图片**：80×80px 圆角 4px，多图支持轮播。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；抽屉加载 Skeleton。
- **空数据**：列表空显示「暂无售后单」，按状态筛选提示「该状态下暂无售后单」。
- **错误态**：审核失败 `message.error('审核操作失败，请重试')`；并发冲突提示「售后单状态已变更，请刷新」。
- **权限控制**：Operator/Admin 可访问；审核操作需 `after-sales:audit` 权限。
- **并发与乐观锁**：审核基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：通过（触发退款）、驳回为危险操作，强制 `<ConfirmDialog>` 二次确认。

## 8. 验收要点
- [ ] 列表支持按售后单号/订单号/买家/卖家/状态/类型/时间组合筛选
- [ ] 统计概览展示各状态售后计数
- [ ] 详情抽屉展示协商记录时间线与凭证图片
- [ ] 通过时审核金额可调整但不超申请金额
- [ ] 驳回必须填写原因
- **性能要求**：列表分页 < 1s，>100 行启用虚拟滚动。
- **可访问性**：凭证图片 alt 含售后单号，时间线 aria-label 含节点描述。
