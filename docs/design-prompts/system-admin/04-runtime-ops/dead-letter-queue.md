# 死信队列 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：04-runtime-ops 运行时运维
- **页面类型**：列表页 + 详情抽屉 + 批量操作
- **目标用户**：系统管理员（Admin）
- **核心目标**：跨域汇聚各 MQ 死信消息（DeadLetterMessage），查看详情、单条/批量重投或丢弃，处置失败消息。
- **访问入口**：Sider「运行时运维 → 死信队列」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 批量操作条 + 主表格 + 详情抽屉。
- **关键区域**：
  - 区域 A（筛选条）：来源上下文多选（Order/Payment/Notification 等）+ 状态多选（待处理/已重投/已丢弃）+ 进入死信时间 `DateTimeRangePicker` + 「刷新」按钮。
  - 区域 B（批量操作条）：选中 N 条后显示，含「批量重投」「批量丢弃」按钮。
  - 区域 C（主表格）：含选择列 + 列含原始消息ID/来源上下文/原始主题/失败原因/重投次数/状态/进入时间/操作（详情/重投/丢弃），分页 20。
  - 区域 D（详情抽屉）：`<a-drawer width="720">` 展示全字段 + 原始消息体 JSON 高亮 + 消息头 + 处置历史。
- **响应式断点**：≥1200px 表格 9 列；992-1199px 隐藏「原始主题」与「进入时间」。
- **首屏内容**：待处理死信列表（默认筛选 status=待处理，按进入时间倒序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [上下文多选] [状态多选] [时间范围] [刷新]       │
│ ☑ 选中 3 条 [批量重投] [批量丢弃]              │
├────────────────────────────────────────────────┤
│ ☐ │ 消息ID │ 来源 │ 主题 │ 原因 │ 次数 │ 状态 │ 操作 │
│ ☑ │ xxx   │ Order│ order.created │ 超时 │ 2 │待处理│详情/重投/丢弃│
└────────────────────────────────────────────────┘
→ 抽屉：全字段 + Payload JSON + 处置历史
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dead-letters` | 分页查询死信消息 | Admin,Operator |
| GET | `/api/admin/dead-letters/{id}` | 获取死信消息详情 | Admin,Operator |
| POST | `/api/admin/dead-letters/{id}/retry` | 重投指定死信消息（幂等） | Admin,Operator |
| POST | `/api/admin/dead-letters/{id}/discard` | 丢弃指定死信消息（reason 必填） | Admin,Operator |
| POST | `/api/admin/dead-letters/batch-retry` | 批量重投死信消息 | Admin,Operator |
| POST | `/api/admin/dead-letters/batch-discard` | 批量丢弃死信消息 | Admin,Operator |

- **请求参数**：列表 `sourceContext/status/page/pageSize`；丢弃 `DiscardDeadLetterDto`（reason）；批量 `BatchOperationDto`（messageIds）/`BatchDiscardDto`（messageIds+reason）。
- **响应字段**：`DeadLetterMessageDto` 含 `MessageId`、`OriginalMessageId`、`SourceContext`、`OriginalTopic`、`OriginalQueue`、`Payload`、`Headers`、`ErrorReason`、`FailedAt`、`RetryCount`、`Status`、`OperatorId`、`OperatedAt`、`DiscardReason`；批量结果 `BatchOperationResultDto`（succeeded/failed）。
- **数据加载策略**：进入页面加载首页；筛选重新请求；详情按需加载。
- **缓存策略**：不缓存（死信实时变化）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dead-letters?status=Pending&page=1&pageSize=20` → 表格渲染。
  2. 勾选多行 → 批量操作条显示 → 点击「批量重投」 → `ConfirmDialog` → POST batch-retry → 显示成功/失败明细。
  3. 点击「详情」 → GET `/api/admin/dead-letters/{id}` → 抽屉展示全字段。
  4. 点击「重投」（仅待处理态） → `ConfirmDialog` → POST retry → 状态变为已重投。
  5. 点击「丢弃」（仅待处理态） → 弹窗输入丢弃原因（必填） → `ConfirmDialog` → POST discard → 状态变为已丢弃。
- **分支流程**：
  - 重投已重投或已丢弃消息：后端幂等返回当前状态，`message.info('消息已处置，未重复执行')` 1.5s。
  - 批量操作部分失败：弹窗显示 `BatchOperationResultDto` 明细，成功 N 条、失败 M 条 + 失败原因列表。
  - 丢弃原因未填：前端校验拦截。
- **跨页面流转**：点击「查看相关审计」跳 `/audit/audit-logs?resourceType=DeadLetter&keyword={messageId}`。
- **状态机可视化**：待处理 → 已重投 / 已丢弃，`StatusTag` 自定义 deadLetter 类型：待处理黄、已重投蓝、已丢弃灰。

## 5. 组件清单
- **基础组件**：`<a-table>`（rowSelection）、`<a-drawer>`、`<a-modal>`、`<a-form>`、`<a-textarea>`、`<a-descriptions>`、`<a-alert>`（批量结果）
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 死信状态
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 重投/丢弃确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`ReloadOutlined`（重投）、`DeleteOutlined`（丢弃）、`EyeOutlined`、`WarningOutlined` 16px。
- **空状态**：「暂无死信消息」+ CTA「刷新」。

## 6. 视觉规范
- **主色应用**：批量重投按钮主色；批量丢弃按钮 danger；待处理状态 `<a-tag color="warning">`。
- **状态色**：待处理黄、已重投蓝、已丢弃灰。
- **间距**：筛选条与表格 16px；表格行高 48px；抽屉内边距 24px；JSON 体 12px monospace。
- **字体**：表格 14px；消息ID 12px monospace；失败原因 12px `#FF4D4F`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：批量部分失败用 `<a-alert type="warning">` 展示明细；网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；处置操作 `PermissionGuard permission="dead-letter:dispose"`。
- **并发与乐观锁**：重投/丢弃幂等，无乐观锁。
- **危险操作确认**：
  - 重投 `ConfirmDialog` 内容「重投后消息将重新投递到原队列，可能触发重复业务逻辑。已重投/已丢弃的消息幂等返回当前状态。」确认按钮主色。
  - 丢弃 `ConfirmDialog` 内容「丢弃后该消息将永久不再处理，关联业务可能丢失。此操作不可逆，请确认。」确认按钮 danger 红色，需填写丢弃原因。

## 8. 验收要点
- [ ] 仅待处理态显示「重投」「丢弃」按钮
- [ ] 批量操作部分失败显示明细
- [ ] 丢弃原因必填校验
- [ ] 重投/丢弃有二次确认，丢弃按钮 danger
- **性能要求**：首屏 < 1.5s；表格 > 100 行启用虚拟滚动；批量操作 ≤ 100 条/次。
- **可访问性**：表格行选择支持键盘；JSON 体可折叠；对话框聚焦管理。
