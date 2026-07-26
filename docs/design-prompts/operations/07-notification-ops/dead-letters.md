# 死信管理 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：07-notification-ops 通知运营
- **页面类型**：列表管理页（死信查询 + 批量重发 + 批量丢弃）
- **目标用户**：运营管理员（Operator）、系统管理员（Admin）
- **核心目标**：分页查询通知子系统产生的死信记录（持续失败超过最大重试次数的通知），支持批量重发以触发再次投递，或批量丢弃以结束生命周期，避免死信堆积影响监控指标与系统容量。
- **访问入口**：左侧菜单「通知运营 → 死信管理」；通知记录页死信状态点击「查看死信列表」跳转；通知送达率看板死信计数跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部统计概览 + 操作工具栏 + 死信记录表格 + 记录详情抽屉（含失败链路）+ 批量操作确认对话框。
- **关键区域**：
  - 区域 A（统计概览）：`<a-statistic>` 展示死信总数、近 7 天新增、待处理数、本月已丢弃数，用于运营快速评估死信积压情况。
  - 区域 B（工具栏）：左侧「批量重发」「批量丢弃」按钮（基于表格选中行启用，未选中时 disabled），右侧「刷新」「导出」按钮。
  - 区域 C（筛选条）：`<a-form inline>` 含渠道（短信/邮件/站内信/推送）、模板编码、失败时间范围、查询/重置；本页固定按 `Status = DeadLetter` 过滤，无需状态筛选。
  - 区域 D（死信表格）：`<a-table row-selection>` 列含接收人 UserId、渠道、模板编码、标题、重试次数、错误码、错误消息、失败时间、创建时间、操作列（详情）。
  - 区域 E（详情抽屉）：`<a-drawer width="640">` 展示记录基础信息、渲染后标题与正文、渠道返回错误码与错误消息、状态变更时间线、重试历史。
- **响应式断点**：≥1200px 抽屉 640px；992-1199px 抽屉 480px；<992px 表格横向滚动。
- **首屏内容**：统计概览 + 死信记录近 7 天前 20 条（按 FailedAt 倒序）。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ 死信总数:128  近7天新增:18  待处理:96  本月丢弃:12 │
├──────────────────────────────────────────────────┤
│ [批量重发] [批量丢弃]              [刷新] [导出]   │
├──────────────────────────────────────────────────┤
│ [渠道▼][模板编码][失败时间范围]      [查询][重置]   │
├──────────────────────────────────────────────────┤
│ ☐ 接收人  渠道 模板编码  标题  重试 错误码 失败时间 │
│ ☑ 买家A  短信 ORDER_PAID 订单支付 5  TIMEOUT 14:30│
│ ☐ 买家B  邮件 REFUND_OK  退款到账 5  SMTP_535 14:28│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dead-letters` | 分页查询死信列表 | Operator, Admin |
| POST | `/api/admin/dead-letters/batch-resend` | 批量重发死信通知 | Operator, Admin |
| POST | `/api/admin/dead-letters/batch-discard` | 批量丢弃死信通知 | Operator, Admin |

- **请求参数**：
  - `GET /api/admin/dead-letters`：`page`（int，默认 1）、`pageSize`（int，默认 20）；后端固定按 `Status = DeadLetter` 过滤，前端无需传 status。
  - `POST /api/admin/dead-letters/batch-resend`：body `BatchDeadLetterRequestDto`，含 `RecordIds`（`List<Guid>` 必填，待重发记录标识列表）。
  - `POST /api/admin/dead-letters/batch-discard`：body `BatchDeadLetterRequestDto`，含 `RecordIds`（`List<Guid>` 必填，待丢弃记录标识列表）、`DiscardReason`（string?，丢弃原因，建议必填用于审计）。
- **响应字段**：
  - 列表：`DeadLetterListResultDto`，含 `Items`（每项为 `DeadLetterRecordDto`：`RecordId`、`UserId`、`TemplateCode`、`Channel`（Sms/Email/InApp/Push）、`Title`、`Content`、`Status`（固定 DeadLetter）、`RetryCount`、`ErrorMessage`、`ErrorCode`、`FailedAt`、`CreatedAt`）、`Total`、`Page`、`PageSize`。
  - 批量操作：`BatchOperationResultDto`，含 `SuccessCount`、`FailureCount`、`Errors`（`List<string>`，失败原因清单，与 RecordIds 顺序对应）。
- **数据加载策略**：进入页面加载近 7 天死信记录（按 FailedAt 倒序）；切换筛选条件重新请求；批量操作完成后刷新当前页以反映最新状态。
- **缓存策略**：不缓存，死信状态实时性强；批量操作后强制刷新避免脏数据。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 调用 `GET /api/admin/dead-letters` 加载第一页死信记录 → 渲染统计概览与表格。
  2. 用户勾选一行或多行死信记录 → 工具栏「批量重发」「批量丢弃」按钮启用。
  3. 点击「批量重发」→ `<ConfirmDialog>` 确认（标题「确认批量重发」，内容「将触发 {N} 条死信重新发送，可能产生渠道费用」，确认按钮主色「确认重发」）→ 调用 `POST /api/admin/dead-letters/batch-resend` → 显示结果（成功 N 条，失败 M 条）→ 刷新列表。
  4. 点击「批量丢弃」→ `<ConfirmDialog>` 强制确认（标题「确认批量丢弃」，内容「丢弃后死信将不再重发，请确认已人工排查原因」，确认按钮红色「确认丢弃」）→ 必填「丢弃原因」输入框 → 调用 `POST /api/admin/dead-letters/batch-discard` → 显示结果 → 刷新列表。
  5. 点击行「详情」→ 打开抽屉展示渲染正文、错误码与错误消息、状态时间线、重试历史。
- **分支流程**：
  - 批量操作部分失败：返回 `BatchOperationResultDto` 含 `FailureCount > 0`，前端 `message.warning` 提示「成功 N 条，失败 M 条」，并展示 `Errors` 清单；失败记录保持选中态便于二次操作。
  - 选中记录已被其他运营处理（状态变更）：后端返回失败原因「记录状态已变更，非死信状态」，前端提示刷新并清除该行选中。
  - 空选中：批量按钮 disabled，鼠标悬停提示「请先选择死信记录」。
- **跨页面流转**：
  - 从「通知记录」页死信状态跳转：携带 `templateCode`、`channel` 筛选参数跳入本页。
  - 点击「模板编码」跳转通知模板页查看模板配置。
  - 点击「接收人」跳转用户管理页查看用户详情（跨 BC1）。
- **状态机可视化**：DeadLetter（死信）→ batch-resend 成功 → Pending（待发送，由 DispatchJob 接管）；DeadLetter → batch-discard → Discarded（已丢弃，终态）。本页仅展示 DeadLetter 状态记录，操作后记录离开本视图。

## 5. 组件清单
- **基础组件**：`<a-table>`（row-selection）、`<a-form>`、`<a-input>`、`<a-select>`、`<a-drawer>`、`<a-statistic>`、`<a-timeline>`、`<a-descriptions>`、`<a-tag>`、`<a-button>`、`<a-popconfirm>`。
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 死信状态展示（红色 DeadLetter）
  - `IdempotencyButton`（见 shared/components.md §2）— 批量重发/丢弃按钮，防重复提交
  - `PermissionGuard`（见 shared/components.md §3）— 批量操作权限控制，permission='notification:dead-letter:manage'
  - `DataTable`（见 shared/components.md §6）— 死信记录列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 失败时间范围筛选
  - `ConfirmDialog`（见 shared/components.md §10）— 批量操作二次确认，丢弃操作强制输入原因
  - `EmptyState`（见 shared/components.md §5）— 无死信记录时展示
- **图表组件**：无（统计概览使用 `<a-statistic>` 数值展示，不使用图表）。
- **图标使用**：`RedoOutlined` 批量重发、`DeleteOutlined` 批量丢弃、`EyeOutlined` 详情、`ExportOutlined` 导出、`WarningOutlined` 死信告警。
- **空状态**：`EmptyState` title="暂无死信记录" description="所有通知均已成功投递或正在重试"。

## 6. 视觉规范
- **主色应用**：批量重发按钮主色 `#1677FF`；导出按钮默认色；统计概览数值主色。
- **状态色**：死信状态 `#FF4D4F` 红；重试次数 ≥ 3 `#FAAD14` 橙；错误码 `#FF4D4F`；错误消息 `#8C8C8C`。
- **渠道色**：短信 `#52C41A`、邮件 `#1677FF`、站内信 `#722ED1`、推送 `#FAAD14`。
- **间距**：统计概览卡片间距 24px；筛选条与表格 16px；表格行高 48px；抽屉时间线节点 16px；批量按钮间距 8px。
- **字体**：接收人 14px；模板编码 14px mono `#8C8C8C`；标题 14px `#000000D9`（1 行省略）；重试次数 12px `#FAAD14`；错误码 12px mono `#FF4D4F`；错误消息 12px `#8C8C8C`（2 行省略）；时间 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px；批量按钮图标 14px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；统计概览 `<a-skeleton active />`；抽屉加载 Skeleton。
- **空数据**：列表空显示「暂无死信记录」+ 描述「所有通知均已成功投递或正在重试」。
- **错误态**：
  - 列表加载失败：`message.error('死信列表加载失败')` + 重试按钮。
  - 批量重发失败：`message.error('批量重发失败，请重试')`，保留选中态。
  - 批量丢弃失败：`message.error('批量丢弃失败，请重试')`，保留选中态。
  - 部分失败：`message.warning('成功 N 条，失败 M 条')` + 展开失败清单。
- **权限控制**：Operator/Admin 可访问；批量重发需 `notification:dead-letter:manage` 权限；批量丢弃需 `notification:dead-letter:discard` 权限（建议比 reend 更严格，仅 Admin 或高级 Operator 可丢弃）。
- **并发与乐观锁**：
  - 批量操作基于记录状态校验（仅 DeadLetter 可重发或丢弃），后端在事务内校验状态，冲突返回错误。
  - 同一记录被多个运营同时操作时，先提交者成功，后提交者收到「记录状态已变更」错误并提示刷新。
  - 批量操作按钮点击后立即 disabled + loading，防止重复提交。
- **危险操作确认**：
  - 批量重发：`<ConfirmDialog>` 二次确认，说明将触发实际发送并可能产生渠道费用。
  - 批量丢弃：`<ConfirmDialog>` 强制确认 + 必填「丢弃原因」输入框（最少 10 字符），说明丢弃后不可恢复且记录进入终态。

## 8. 验收要点
- [ ] 统计概览展示死信总数、近 7 天新增、待处理数、本月已丢弃数。
- [ ] 列表支持按渠道、模板编码、失败时间范围组合筛选。
- [ ] 表格支持多选，未选中时批量按钮 disabled。
- [ ] 批量重发调用 `POST /api/admin/dead-letters/batch-resend`，展示成功/失败计数。
- [ ] 批量丢弃调用 `POST /api/admin/dead-letters/batch-discard`，强制输入丢弃原因（≥10 字符）。
- [ ] 详情抽屉展示渲染正文、错误码、错误消息、状态时间线。
- [ ] 批量操作完成后自动刷新当前页。
- [ ] 部分失败时展示失败清单并保留失败记录选中态。
- **性能要求**：列表分页 < 1s；批量操作 ≤ 100 条 < 3s；> 100 条限制并提示「单次最多 100 条」。
- **可访问性**：状态标签 `aria-label` 含中文状态名；批量按钮 `aria-label` 含操作与数量（如「批量重发 3 条死信」）；表格行 `role="row"`；抽屉 `role="dialog"`。
