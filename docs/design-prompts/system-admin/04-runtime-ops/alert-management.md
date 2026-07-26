# 告警管理 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：04-runtime-ops 运行时运维
- **页面类型**：列表页 + 详情抽屉
- **目标用户**：系统管理员（Admin）
- **核心目标**：查看 Alertmanager 告警事件，按模块与严重级别筛选，处置告警（确认/静默/转工单），追踪闭环。
- **访问入口**：Sider「运行时运维 → 告警管理」/ 健康监控页跳转
- **实现状态**：🚧 规划中

## 2. 页面布局与信息架构
- **整体布局**：顶部统计条 + 筛选 + 主表格 + 详情抽屉 + 静默规则弹窗。
- **关键区域**：
  - 区域 A（统计条）：4 个 `<a-statistic>` — 待处置告警数 / 严重告警数 / 今日告警总数 / 平均处置时长。
  - 区域 B（筛选条）：模块多选 + 严重级别多选（critical/warning/info）+ 状态多选（firing/acknowledged/resolved）+ 时间范围 `DateTimeRangePicker`。
  - 区域 C（主表格）：列含告警ID/名称/模块/级别/状态/触发时间/持续时长/操作（详情/确认/静默/转工单），分页 20。
  - 区域 D（详情抽屉）：`<a-drawer width="720">` 展示告警全字段 + 触发条件 + 标签 + 注释历史 + 关联指标图。
  - 区域 E（静默规则弹窗）：`<a-modal>` 创建静默规则（匹配器/持续时长/原因）。
- **响应式断点**：≥1200px 表格 8 列；992-1199px 隐藏「持续时长」。
- **首屏内容**：近 24 小时 firing 状态告警列表。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ 待处置 12 │ 严重 3 │ 今日 28 │ 平均处置 8m    │
├────────────────────────────────────────────────┤
│ [模块多选] [级别多选] [状态多选] [时间范围]     │
├────────────────────────────────────────────────┤
│ ID │ 名称 │ 模块 │ 级别 │ 状态 │ 触发 │ 操作   │
│ xxx│ HighErrorRate│ Payment│critical│firing│07-26 14:30│详情/确认/静默│
└────────────────────────────────────────────────┘
→ 抽屉：全字段 + 标签 + 注释 + 关联指标
→ 弹窗：静默匹配器/时长/原因
```

## 3. 数据模型与 API 对接
- **主要 API**（规划中，待 SystemAdmin BC 实现 Alertmanager 集成端点）：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/alerts` | 分页查询告警事件（待实现） | Admin |
| GET | `/api/admin/alerts/{id}` | 获取告警详情（待实现） | Admin |
| POST | `/api/admin/alerts/{id}/acknowledge` | 确认告警（待实现） | Admin |
| POST | `/api/admin/alerts/silences` | 创建静默规则（待实现） | Admin |
| GET | `/api/admin/alerts/silences` | 查询静默规则列表（待实现） | Admin |
| DELETE | `/api/admin/alerts/silences/{id}` | 删除静默规则（待实现） | Admin |

- **请求参数**：列表 `module/severity/status/start/end/page/pageSize`；确认 `comment`；静默 `CreateSilenceDto`（matchers/duration/reason）。
- **响应字段**：`AlertDto` 含 `AlertId`、`Name`、`Module`、`Severity`、`Status`、`TriggeredAt`、`DurationSeconds`、`Labels`、`Annotations`、`Summary`、`Description`、`RelatedMetric`；`SilenceDto` 含 `SilenceId`、`Matchers`、`StartsAt`、`EndsAt`、`Reason`、`CreatedBy`。
- **数据加载策略**：进入页面加载首页 + 统计；firing 状态每 30s 轮询刷新；详情按需加载。
- **缓存策略**：不缓存（告警实时变化）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行 GET `/api/admin/alerts?status=firing` + GET 统计 → 表格 + 统计条渲染。
  2. firing 告警每 30s 轮询刷新。
  3. 点击「详情」 → GET `/api/admin/alerts/{id}` → 抽屉展示全字段 + 关联指标图。
  4. 点击「确认」 → 弹窗输入注释 → POST acknowledge → 状态变为 acknowledged。
  5. 点击「静默」 → 弹窗配置匹配器 + 时长 + 原因 → POST silences → `message.success('静默规则已创建')`。
  6. 点击「转工单」 → 跳转外部工单系统（携带告警上下文）。
- **分支流程**：
  - 告警已 resolved：「确认」「静默」按钮 disabled。
  - 静默规则冲突：后端 409，`message.error('已存在匹配的静默规则')` 3s。
- **跨页面流转**：从健康监控页跳转携带 `module={moduleName}`；点击「关联指标」跳 `/monitoring/prometheus-dashboard?metric={relatedMetric}`。
- **状态机可视化**：firing → acknowledged → resolved，`StatusTag` 自定义 alert 类型：firing 红、acknowledged 黄、resolved 灰。

## 5. 组件清单
- **基础组件**：`<a-statistic>`、`<a-table>`、`<a-drawer>`、`<a-modal>`、`<a-form>`、`<a-select mode="multiple">`、`<a-textarea>`、`<a-tag>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 告警状态与级别
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 确认/静默确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：`ChartLine`（见 shared/components.md §7.1）— 关联指标图
- **图标使用**：`WarningOutlined`（critical）、`ExclamationCircleOutlined`（warning）、`InfoCircleOutlined`（info）、`BellMutedOutlined`（静默）16px。
- **空状态**：「暂无告警」+ CTA「刷新」。

## 6. 视觉规范
- **主色应用**：统计条数值 24px semibold；确认按钮主色；静默按钮默认色。
- **状态色**：firing `#FF4D4F`、acknowledged `#FAAD14`、resolved `#8C8C8C`；级别 critical 红、warning 黄、info 蓝。
- **间距**：统计条间距 24px；筛选条与表格 16px；表格行高 48px；抽屉内边距 24px。
- **字体**：表格 14px；告警名 14px medium；标签 12px monospace。
- **图标尺寸**：级别图标 16px；操作图标 16px。

## 7. 异常处理与边界
- **加载态**：统计条与表格 `<a-skeleton>`；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：API 未实现时显示 `<a-alert type="info" message="告警管理功能规划中，API 待 SystemAdmin BC 实现 Alertmanager 集成" />`；网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin']`；处置操作 `PermissionGuard permission="alert:manage"`。
- **并发与乐观锁**：无乐观锁（告警状态后端保证）。
- **危险操作确认**：
  - 确认告警 `ConfirmDialog` 内容「确认后告警状态变为已确认，不再触发通知（除非再次变为 firing）。」确认按钮主色。
  - 静默 `ConfirmDialog` 内容「静默期间匹配的告警将不再通知，可能遗漏关键事件。请确认静默时长。」确认按钮 danger 红色。

## 8. 验收要点
- [ ] 顶部 4 个统计实时更新
- [ ] firing 告警每 30s 轮询刷新
- [ ] 已 resolved 告警处置按钮 disabled
- [ ] 静默规则匹配器配置正确
- **性能要求**：首屏 < 1.5s；轮询不阻塞 UI；关联指标图加载 < 1s。
- **可访问性**：表格键盘导航；级别图标有 `aria-label`；对话框聚焦管理。
