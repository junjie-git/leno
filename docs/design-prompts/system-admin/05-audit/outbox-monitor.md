# Outbox 监控 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：05-audit 审计与对账
- **页面类型**：看板页 + 列表页
- **目标用户**：系统管理员（Admin）
- **核心目标**：监控各域 Outbox 发件箱积压情况（outbox_pending_count 指标），按限界上下文查看未发布事件数量与积压时长，触发积压告警处置（重投/归档），保障集成事件最终一致。
- **访问入口**：Sider「审计与对账 → Outbox 监控」/ 健康监控页跳转 / 告警管理页跳转
- **实现状态**：🚧 规划中

## 2. 页面布局与信息架构
- **整体布局**：顶部统计条 + 积压趋势图 + 按域分组表格 + 详情抽屉。
- **关键区域**：
  - 区域 A（统计条）：4 个 `<a-statistic>` — 总积压事件数 / 积压域数量 / 最大积压时长 / 今日重投次数。
  - 区域 B（积压趋势图）：`ChartLine` 展示近 24 小时 outbox_pending_count 趋势，按域分系列。
  - 区域 C（按域分组表格）：列含限界上下文/未发布事件数/最早事件时间/最大积压时长(分钟)/最近归档时间/状态/操作（详情/重投/归档），按积压数倒序。
  - 区域 D（详情抽屉）：`<a-drawer width="720">` 展示该域 Outbox 积压事件列表（事件ID/聚合ID/事件类型/Payload/创建时间/重试次数）+ 归档历史。
- **响应式断点**：≥1200px 表格 7 列；992-1199px 隐藏「最近归档时间」。
- **首屏内容**：各域 Outbox 积压概览 + 近 24 小时趋势。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ 总积压 156 │ 积压域 4 │ 最大时长 42m │ 重投 8  │
├────────────────────────────────────────────────┤
│ [近 24h 积压趋势折线图，按域分系列]              │
├────────────────────────────────────────────────┤
│ 上下文 │ 积压数 │ 最早时间 │ 时长 │ 状态 │ 操作  │
│ Order  │  82   │ 13:48  │ 42m  │ 积压 │详情/重投│
└────────────────────────────────────────────────┘
 抽屉：事件列表（ID/聚合ID/类型/Payload/时间/重试）+ 归档历史
```

## 3. 数据模型与 API 对接
- **主要 API**（规划中，待 SystemAdmin BC 实现 Outbox 监控端点）：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/outbox/summary` | 获取各域 Outbox 积压汇总（待实现） | Admin |
| GET | `/api/admin/outbox/trend` | 获取近 N 小时积压趋势（待实现） | Admin |
| GET | `/api/admin/outbox/{context}/messages` | 分页查询指定域积压事件详情（待实现） | Admin |
| POST | `/api/admin/outbox/{context}/republish` | 批量重投指定域积压事件（待实现） | Admin |
| POST | `/api/admin/outbox/{context}/archive` | 归档指定域陈旧积压事件（待实现） | Admin |
| GET | `/api/admin/outbox/{context}/archive-history` | 查询归档历史（待实现） | Admin |

- **请求参数**：趋势 `hours`（默认 24）；事件列表 `context/page/pageSize`；重投 `BatchRepublishDto`（messageIds?/maxCount?）；归档 `ArchiveDto`（olderThanMinutes/reason）。
- **响应字段**：`OutboxSummaryDto` 含 `Context`、`PendingCount`、`OldestEventAt`、`MaxAgeMinutes`、`LastArchivedAt`、`Status`；`OutboxTrendDto` 含 `Timestamp`、`Context`、`PendingCount`；`OutboxMessageDto` 含 `MessageId`、`AggregateId`、`EventType`、`Payload`、`CreatedAt`、`RetryCount`、`Status`。
- **数据加载策略**：进入页面并行加载汇总 + 趋势；详情按需加载；积压状态每 60s 轮询。
- **缓存策略**：不缓存（积压实时变化）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行 GET `/api/admin/outbox/summary` + GET `/api/admin/outbox/trend?hours=24` → 统计条 + 趋势图 + 表格渲染。
  2. 积压数据每 60s 轮询刷新汇总。
  3. 点击「详情」 → GET `/api/admin/outbox/{context}/messages` → 抽屉展示积压事件列表。
  4. 点击「重投」 → `ConfirmDialog` → POST `/api/admin/outbox/{context}/republish` → `message.success('已重投 N 条积压事件')` 1.5s。
  5. 点击「归档」 → 弹窗输入归档阈值（olderThanMinutes）+ 原因 → `ConfirmDialog` → POST archive → 状态更新。
- **分支流程**：
  - 积压数超阈值（> 1000）：状态自动标红 + `notification.warning` 通知。
  - 重投部分失败：弹窗显示成功/失败明细。
  - 归档后事件不可恢复：`ConfirmDialog` 强调不可逆。
- **跨页面流转**：从健康监控页跳转携带 `context={moduleName}`；点击「查看告警」跳 `/runtime-ops/alert-management?module={context}`；点击「查看死信」跳 `/runtime-ops/dead-letter-queue?sourceContext={context}`。
- **状态机可视化**：Outbox 域状态 `StatusTag` 自定义 outbox 类型：正常绿、积压黄、严重积压红、已归档灰。

## 5. 组件清单
- **基础组件**：`<a-statistic>`、`<a-table>`、`<a-drawer>`、`<a-modal>`、`<a-form>`、`<a-input-number>`、`<a-textarea>`、`<a-descriptions>`、`<a-tag>`、`<a-alert>`
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）— 统计卡片
  - `DataTable`（见 shared/components.md §6）
  - `ChartLine`（见 shared/components.md §7.1）— 积压趋势
  - `StatusTag`（见 shared/components.md §1）— 域状态
  - `IdempotencyButton`（见 shared/components.md §2）— 重投/归档
  - `ConfirmDialog`（见 shared/components.md §10）— 重投/归档确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：`ChartLine`（见 shared/components.md §7.1）— 近 24h 积压趋势
- **图标使用**：`InboxOutlined`（积压）、`ReloadOutlined`（重投）、`ArchiveOutlined`（归档）、`WarningOutlined`（严重）16px。
- **空状态**：「暂无积压事件，所有域 Outbox 正常」+ CTA「刷新」。

## 6. 视觉规范
- **主色应用**：统计数值 24px semibold；重投按钮主色；归档按钮默认色。
- **状态色**：正常 `#52C41A`、积压 `#FAAD14`、严重积压 `#FF4D4F`、已归档 `#8C8C8C`；趋势图按域分色，主色 `#1677FF`。
- **间距**：统计条间距 24px；趋势图高度 300px；表格行高 48px；抽屉内边距 24px；Payload 12px monospace。
- **字体**：表格 14px；上下文名 14px medium；事件ID 12px monospace。
- **图标尺寸**：状态图标 16px；操作图标 16px。

## 7. 异常处理与边界
- **加载态**：统计条与表格 `<a-skeleton>`；趋势图 `<a-skeleton :active="true" />`；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底，CTA「刷新」。
- **错误态**：API 未实现时显示 `<a-alert type="info" message="Outbox 监控功能规划中，API 待 SystemAdmin BC 实现发件箱积压查询端点" />`；网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin']`；重投/归档 `PermissionGuard permission="outbox:manage"`。
- **并发与乐观锁**：无乐观锁（积压状态后端保证）；重投幂等。
- **危险操作确认**：
  - 重投 `ConfirmDialog` 内容「重投后积压事件将重新发布到事件总线，可能触发重复消费。订阅者需保证幂等。是否继续？」确认按钮主色。
  - 归档 `ConfirmDialog` 内容「归档后陈旧积压事件将从监控视图移除并转入归档存储，不再自动重投。此操作可逆（可查询归档历史），但需手动恢复。请确认。」确认按钮 danger 红色，需填写归档原因。

## 8. 验收要点
- [ ] 顶部 4 个统计实时更新
- [ ] 积压趋势图按域分系列展示
- [ ] 积压数 > 1000 时状态自动标红
- [ ] 每 60s 轮询刷新汇总
- [ ] API 未实现时显示规划中提示
- **性能要求**：首屏 < 1.5s；轮询不阻塞 UI；趋势图加载 < 1s。
- **可访问性**：表格键盘导航；趋势图有 aria-label 描述；对话框聚焦管理。
