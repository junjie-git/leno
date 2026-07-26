# 渠道对账 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：06-payment-ops 支付运营
- **页面类型**：列表管理页（对账差异查询 + 手动触发对账）
- **目标用户**：系统管理员（Admin）
- **核心目标**：分页查询支付渠道对账差异（长款/短款/金额不一致），定位支付单与渠道账单不匹配的记录，支持手动触发指定日期对账，确保财务数据一致性与可追溯。对应 spec F-PAY-012 渠道对账功能。
- **访问入口**：左侧菜单「支付运营 → 渠道对账」；首页对账差异告警跳转；定时对账任务失败通知跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部统计概览 + 操作工具栏 + 筛选条 + 对账差异表格 + 差异详情抽屉。
- **关键区域**：
  - 区域 A（统计概览）：`<a-statistic>` 展示待处理差异数、近 7 天新增差异、长款数、短款数、金额不一致数，用于管理员快速评估对账健康度。
  - 区域 B（工具栏）：左侧「手动触发对账」按钮（主操作，触发指定日期对账），右侧「刷新」「导出差异清单」按钮。
  - 区域 C（筛选条）：`<a-form inline>` 含账单日期（DateTime）、渠道（WeChatPay/Alipay）、差异类型（长款/短款/金额不一致）、状态（待处理/已修复/已忽略）、查询/重置。
  - 区域 D（差异表格）：`<a-table>` 列含账单日期、渠道、差异类型、渠道流水号、渠道金额、系统流水号、系统金额、支付单 ID、状态、备注、创建时间、操作列（详情）。
  - 区域 E（详情抽屉）：`<a-drawer width="640">` 展示差异完整信息、渠道侧与系统侧对比、备注、状态变更时间线。
- **响应式断点**：≥1200px 抽屉 640px；992-1199px 抽屉 480px；<992px 表格横向滚动。
- **首屏内容**：统计概览 + 待处理差异前 20 条（按 CreatedAt 倒序）。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ 待处理:18  近7天新增:5  长款:8  短款:7  金额不一致:3│
├──────────────────────────────────────────────────┤
│ [手动触发对账]                    [刷新] [导出]    │
├──────────────────────────────────────────────────┤
│ [账单日期][渠道▼][差异类型▼][状态▼]   [查询][重置]  │
├──────────────────────────────────────────────────┤
│ 账单日期 渠道 差异类型 渠道流水号 渠道金额 状态 操作│
│ 07-25   微信 长款    WX001     ¥199  待处理 [详情]│
│ 07-25   支付 短款    -         -     待处理 [详情]│
│ 07-24   微信 金额不一致 WX002   ¥299  待处理 [详情]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/reconciliation/diffs` | 分页查询对账差异列表 | Admin |
| POST | `/api/admin/reconciliation/trigger` | 手动触发对账（指定日期） | Admin |

- **请求参数**：
  - `GET /api/admin/reconciliation/diffs`：`billDate`（DateTime?，账单日期）、`channel`（PaymentChannel?，WeChatPay/Alipay）、`diffType`（ReconciliationDiffType?，ChannelOnly 长款/SystemOnly 短款/AmountMismatch 金额不一致）、`status`（ReconciliationDiffStatus?，Pending 待处理/Resolved 已修复/Ignored 已忽略）、`page`（int，默认 1）、`pageSize`（int，默认 20）。
  - `POST /api/admin/reconciliation/trigger`：query 参数 `billDate`（DateTime?，指定对账日期，缺省时后端默认取 `UtcNow.Date.AddDays(-1)` 即前一天）。
- **响应字段**：
  - 列表：`ReconciliationDiffListResultDto`，含 `Items`（每项为 `ReconciliationDiffDto`：`Id`、`BillDate`、`Channel`、`DiffType`、`ChannelTransactionNo`、`ChannelAmount`、`ChannelTransactionTime`、`SystemTransactionNo`、`SystemAmount`、`PaymentId`、`Remark`、`Status`、`CreatedAt`）、`Total`、`Page`、`PageSize`。
  - 触发对账：`ApiResponse`（无 Data，仅返回成功/失败状态码与消息）。
- **数据加载策略**：进入页面加载待处理差异前 20 条；切换筛选条件重新请求；手动触发对账后等待 2 秒再刷新列表以等待任务入队。
- **缓存策略**：不缓存，对账差异状态实时性强；手动触发后强制刷新。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 调用 `GET /api/admin/reconciliation/diffs` 加载待处理差异 → 渲染统计概览与表格。
  2. 用户设置筛选条件（账单日期、渠道、差异类型、状态）→ 点击「查询」→ 重新请求并渲染。
  3. 点击「手动触发对账」→ `<DatePicker>` 选择账单日期（默认前一天）→ `<ConfirmDialog>` 确认（标题「确认触发对账」，内容「将对 {date} 的 {channel 或 全部渠道} 账单进行对账，任务异步执行，完成后自动刷新差异列表」，确认按钮主色「确认触发」）→ 调用 `POST /api/admin/reconciliation/trigger?billDate={date}` → 提示「对账任务已提交，请稍后刷新查看结果」→ 2 秒后自动刷新列表。
  4. 点击行「详情」→ 打开抽屉展示差异完整信息、渠道侧与系统侧字段对比、备注、状态变更时间线。
- **分支流程**：
  - 长款差异（ChannelOnly）：渠道有记录但系统无记录，可能原因：支付回调丢失或第三方测试交易。详情抽屉展示渠道流水号与金额，备注建议「检查回调日志或确认为测试交易」。
  - 短款差异（SystemOnly）：系统有记录但渠道无记录，可能原因：渠道账单延迟或支付未实际完成。详情抽屉展示系统支付单 ID 与金额，备注建议「等待次日账单或检查渠道状态」。
  - 金额不一致（AmountMismatch）：系统与渠道均有记录但金额不同，可能原因：退款金额错误或渠道手续费计算差异。详情抽屉并排展示系统金额与渠道金额，备注建议「人工核对退款流程」。
  - 触发对账失败：渠道 API 不可用或账单未生成时，后端返回错误消息，前端 `message.error` 提示具体原因。
- **跨页面流转**：
  - 点击「支付单 ID」跳转支付记录页（payment-records.md）定位对应支付单。
  - 点击「渠道流水号」复制到剪贴板便于在渠道后台查询。
  - 首页对账告警跳转携带 `status=Pending` 筛选参数。
- **状态机可视化**：对账任务运行中 → 生成差异记录（Pending 待处理）→ 人工修复（Resolved 已修复）/ 人工忽略（Ignored 已忽略）。本页支持查看全部状态，默认筛选 Pending。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-date-picker>`、`<a-drawer>`、`<a-statistic>`、`<a-descriptions>`、`<a-tag>`、`<a-button>`、`<a-popconfirm>`、`<a-tooltip>`。
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 对账差异状态展示（待处理橙色、已修复绿色、已忽略灰色）
  - `IdempotencyButton`（见 shared/components.md §2）— 手动触发对账按钮，防重复提交
  - `PermissionGuard`（见 shared/components.md §3）— 触发对账权限控制，permission='payment:reconciliation:trigger'
  - `DataTable`（见 shared/components.md §6）— 对账差异列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 账单日期筛选（单日期选择，复用组件）
  - `ConfirmDialog`（见 shared/components.md §10）— 触发对账二次确认
  - `EmptyState`（见 shared/components.md §5）— 无差异记录时展示
- **图表组件**：无（统计概览使用 `<a-statistic>` 数值展示，不使用图表）。
- **图标使用**：`ThunderboltOutlined` 手动触发对账、`EyeOutlined` 详情、`ExportOutlined` 导出、`WarningOutlined` 差异告警、`CopyOutlined` 复制流水号。
- **空状态**：`EmptyState` title="暂无对账差异" description="所有账单均已对平"。

## 6. 视觉规范
- **主色应用**：手动触发对账按钮主色 `#1677FF`；导出按钮默认色；统计概览数值主色。
- **状态色**：待处理 `#FAAD14` 橙；已修复 `#52C41A` 绿；已忽略 `#8C8C8C` 灰；金额不一致 `#FF4D4F` 红。
- **差异类型色**：长款（ChannelOnly）`#1677FF` 蓝；短款（SystemOnly）`#FAAD14` 橙；金额不一致（AmountMismatch）`#FF4D4F` 红。
- **渠道色**：微信支付 `#52C41A`、支付宝 `#1677FF`。
- **间距**：统计概览卡片间距 24px；筛选条与表格 16px；表格行高 48px；抽屉描述列表项间距 16px。
- **字体**：账单日期 14px；渠道 14px；差异类型 14px；流水号 12px mono `#8C8C8C`；金额 14px `#000000D9`（金额不一致时红色加粗）；备注 12px `#8C8C8C`（2 行省略）；时间 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px；触发对账按钮图标 14px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；统计概览 `<a-skeleton active />`；抽屉加载 Skeleton。
- **空数据**：列表空显示「暂无对账差异」+ 描述「所有账单均已对平」。
- **错误态**：
  - 列表加载失败：`message.error('对账差异列表加载失败')` + 重试按钮。
  - 触发对账失败：`message.error('对账任务触发失败：{后端返回原因}')`。
  - 渠道 API 不可用：后端返回「渠道账单接口暂不可用，请稍后重试」，前端展示原始错误。
- **权限控制**：仅 Admin 可访问本页（Controller 标 `[Authorize(Roles = "Admin")]`）；手动触发对账需 `payment:reconciliation:trigger` 权限。
- **并发与乐观锁**：
  - 手动触发对账为异步任务，后端幂等设计：同一账单日期重复触发时后端返回「对账任务进行中，请勿重复触发」。
  - 触发按钮点击后立即 disabled + loading 5 秒，防止用户短时间多次点击。
  - 同一账单日期对账任务串行执行，避免并发产生重复差异记录。
- **危险操作确认**：
  - 手动触发对账：`<ConfirmDialog>` 二次确认，说明将对指定日期账单进行对账，任务异步执行可能耗时 1-5 分钟。
  - 查看差异详情：非危险操作，无需确认。

## 8. 验收要点
- [ ] 统计概览展示待处理差异数、近 7 天新增、长款数、短款数、金额不一致数。
- [ ] 列表支持按账单日期、渠道、差异类型、状态组合筛选。
- [ ] 手动触发对账通过 `<DatePicker>` 选择日期，缺省为前一天。
- [ ] 触发对账调用 `POST /api/admin/reconciliation/trigger?billDate={date}`，成功后 2 秒自动刷新列表。
- [ ] 详情抽屉展示渠道侧与系统侧字段对比、备注、状态时间线。
- [ ] 金额不一致差异在表格中红色加粗展示金额。
- [ ] 流水号支持点击复制到剪贴板。
- [ ] 仅 Admin 角色可访问本页，非 Admin 跳转 403 页面。
- **性能要求**：列表分页 < 1s；手动触发对账 API 响应 < 500ms（任务异步执行）；导出差异清单 < 5s（≤ 1000 条）。
- **可访问性**：状态标签 `aria-label` 含中文状态名；差异类型 `aria-label` 含中文类型名；触发对账按钮 `aria-label` 含「手动触发对账」；表格行 `role="row"`；抽屉 `role="dialog"`。
