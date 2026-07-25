# 通知记录 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：07-notification-ops 通知运营
- **页面类型**：列表查询页（多维度筛选 + 死信重发）
- **目标用户**：运营管理员（Operator）
- **核心目标**：多维度分页查询通知发送记录，定位发送失败/死信记录，支持手工重发死信，监控送达率。
- **访问入口**：左侧菜单「通知运营 → 通知记录」；通知送达率看板跳转；通知模板页编码跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 通知记录表格 + 记录详情抽屉（含投递链路）+ 死信重发确认。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含用户 ID、渠道（短信/邮件/站内信/推送）、状态（待发送/已发送/已送达/失败/死信）、模板编码、业务引用、时间范围、查询/重置
  - 区域 B（工具栏）：批量重发死信、刷新、导出、统计概览（各状态计数与送达率）
  - 区域 C（记录表格）：`<a-table>` 列含接收人、渠道、模板编码、状态、业务引用、发送时间、送达时间、重试次数、操作列
  - 区域 D（详情抽屉）：`<a-drawer width="640">` 展示记录基础信息、渲染后标题与正文、渠道返回结果、状态变更时间线
- **响应式断点**：≥1200px 抽屉 640px；992-1199px 抽屉 480px。
- **首屏内容**：筛选条 + 全状态通知记录近7天前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [用户][渠道▼][状态▼][模板编码][业务引用][时间范围] [查询]│
├──────────────────────────────────────────────────┤
│ 已送达:1280 失败:18 死信:3 送达率:98.5% [刷新][导出]│
├──────────────────────────────────────────────────┤
│ 接收人  渠道 模板编码  状态 业务引用 发送时间 操作 │
│ 买家A  短信 ORDER_PAID 已送达 ORDER1 14:30 [详情]│
│ 买家B  邮件 REFUND_OK  死信  ORDER2 14:28 [详情][重发]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/notifications/records` | 多维度分页查询通知记录 | Operator, Admin |
| GET | `/api/notifications/records/{id}` | 获取通知记录详情 | Operator, Admin |
| GET | `/api/notifications/records/by-business/{businessRef}` | 按业务引用查询记录 | Operator, Admin |
| POST | `/api/admin/notifications/records/{id}/resend` | 手工重发死信通知记录 | Operator, Admin |
| GET | `/api/admin/notifications/statistics` | 获取送达率统计 | Operator, Admin |

- **请求参数**：查询参数 `userId`（Guid?）、`channel`（NotificationChannel?）、`status`（NotificationStatus?）、`templateCode`、`businessRef`、`from`（DateTime?）、`to`（DateTime?）、`page`、`pageSize`。
- **响应字段**：`NotificationRecordListResultDto`，含 `Items`（每项含 `Id`、`UserId`、`Channel`、`TemplateCode`、`Status`（Pending/Sending/Sent/Delivered/Failed/DeadLetter）、`BusinessRef`、`SentAt`、`DeliveredAt`、`RetryCount`）、`Total`。
- **数据加载策略**：进入页面加载近7天记录；切换筛选重新请求；重发后局部更新状态列。
- **缓存策略**：不缓存，通知状态实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载近7天通知记录 → 渲染表格与统计概览
  2. 点击「详情」→ 打开抽屉展示记录信息、渲染正文、渠道返回、状态时间线
  3. 死信记录点击「重发」→ `<ConfirmDialog>` 确认 → 调用 resend → 状态变更为待发送
  4. 切换状态筛选为「死信」→ 查看全部死信记录 → 批量重发
- **分支流程**：
  - 死信重发：状态置为 Pending 由 DispatchJob 接管实际发送
  - 业务引用查询：在筛选条输入业务引用（如订单号）查询关联通知
  - 重试次数过多记录（>3 次）：行标红背景提示
- **跨页面流转**：点击「模板编码」跳转通知模板页；点击「业务引用」跳转订单管理（按订单号）。
- **状态机可视化**：Pending（待发送）→ Sending（发送中）→ Sent（已发送）→ Delivered（已送达）/ Failed（失败）→ DeadLetter（死信）。死信可重发回 Pending。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-drawer>`、`<a-statistic>`、`<a-timeline>`、`<a-descriptions>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 通知状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 重发按钮
  - `PermissionGuard`（见 shared/components.md §3）— 重发权限控制，permission='notification:resend'
  - `DataTable`（见 shared/components.md §6）— 通知记录列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 时间范围筛选
  - `ConfirmDialog`（见 shared/components.md §10）— 重发二次确认
  - `EmptyState`（见 shared/components.md §5）— 无记录时展示
- **图标使用**：`EyeOutlined` 详情、`RedoOutlined` 重发、`WarningOutlined` 死信告警、`ExportOutlined` 导出
- **空状态**：`EmptyState` title="暂无通知记录"

## 6. 视觉规范
- **主色应用**：重发按钮主色 `#1677FF`，导出按钮默认色。
- **状态色**：待发送 `#8C8C8C` 灰、已发送 `#1677FF` 蓝、已送达 `#52C41A` 绿、失败 `#FAAD14` 橙、死信 `#FF4D4F` 红。
- **渠道色**：短信 `#52C41A`、邮件 `#1677FF`、站内信 `#722ED1`、推送 `#FAAD14`。
- **间距**：筛选条与表格 16px，统计概览卡片间距 24px，表格行高 48px，抽屉时间线节点 16px。
- **字体**：接收人 14px，模板编码 14px mono `#8C8C8C`，时间 12px `#8C8C8C`，重试次数 12px `#FAAD14`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；抽屉加载 Skeleton。
- **空数据**：列表空显示「暂无通知记录」，按状态筛选提示「该状态下暂无记录」。
- **错误态**：重发失败 `message.error('重发失败，请重试')`；死信记录重发后状态未变更提示「记录状态已变更，请刷新」。
- **权限控制**：Operator/Admin 可访问；重发需 `notification:resend` 权限。
- **并发与乐观锁**：重发基于状态校验（仅死信可重发），冲突提示刷新。
- **危险操作确认**：重发为危险操作，强制 `<ConfirmDialog>`，说明将触发实际发送。

## 8. 验收要点
- [ ] 列表支持按用户/渠道/状态/模板编码/业务引用/时间组合筛选
- [ ] 统计概览展示各状态计数与送达率
- [ ] 详情抽屉展示渲染正文与状态时间线
- [ ] 死信记录支持单个重发与批量重发
- [ ] 重试次数过多记录行标红提示
- **性能要求**：列表分页 < 1s，>100 行启用虚拟滚动。
- **可访问性**：状态标签 aria-label 含中文状态名，重试次数 aria-label 含数值。
