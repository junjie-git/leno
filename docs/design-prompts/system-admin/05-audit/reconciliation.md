# 对账管理 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：05-audit 审计与对账
- **页面类型**：看板页 + 列表页 + 触发操作
- **目标用户**：系统管理员（Admin）、运营管理员（Operator）
- **核心目标**：查看最近一次对账状态（ReconciliationStatus），手动触发按报表类型与时间范围的对账，查看历史对账记录与差异项，确保跨域统计指标一致。
- **访问入口**：Sider「审计与对账 → 对账管理」/ 仪表盘报表快照页跳转
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部状态卡片区 + 触发对账区 + 历史记录表格 + 详情抽屉。
- **关键区域**：
  - 区域 A（状态卡片区）：4 个 `<a-statistic>` — 对账状态（一致/有差异）、差异项数量、最近对账时间、是否触发告警/修正。
  - 区域 B（触发对账区）：报表类型下拉（ReportType：OrderGmv/PaymentSuccessRate/PointsIssued/NotificationDelivery/AfterSalesVolume/ShopRanking/ConversionRate，支持「全部」）+ 时间范围 `DateTimeRangePicker` + 「触发对账」`IdempotencyButton`。
  - 区域 C（历史记录表格）：列含记录ID/报表类型/对账时间/状态/差异项数/是否告警/是否修正/错误信息/操作（详情），按时间倒序。
  - 区域 D（详情抽屉）：`<a-drawer width="720">` 展示对账记录全字段 + 差异项明细列表（报表类型、指标名、期望值、实际值、差异值）。
- **响应式断点**：≥1200px 统计卡片 4 列；992-1199px 统计卡片 2 列；表格 8 列横向滚动。
- **首屏内容**：最近一次对账状态 + 近 7 天对账记录列表。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ 状态: 一致 │ 差异: 0 │ 最近: 07-26 14:00 │ 告警: 否│
├────────────────────────────────────────────────┤
│ [报表类型] [时间范围] [触发对账]                │
├────────────────────────────────────────────────┤
│ ID │ 类型 │ 时间 │ 状态 │ 差异 │ 告警 │ 修正 │ 操作│
│ xxx│ GMV │14:00│ 一致 │  0  │  否  │  否  │ 详情│
└────────────────────────────────────────────────┘
 抽屉：全字段 + 差异项明细（指标/期望/实际/差异）
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/statistics/reconciliation-status` | 获取最近一次对账状态 | Admin,Operator |
| POST | `/api/admin/statistics/reconcile` | 手动触发对账（按报表类型与时间范围） | Admin,Operator |
| GET | `/api/admin/statistics/reconciliation-records` | 获取对账记录列表（按报表类型与时间范围） | Admin,Operator |

- **请求参数**：触发对账 `reportType?/start?/end?`（query 参数，reportType 未传则对账全部类型）；记录列表 `reportType?/start?/end?`（默认近 7 天，默认 OrderGmv）。
- **响应字段**：`ReconciliationStatusDto` 含 `HasRun`、`Status`、`ReportType`、`ReconciledAt`、`DiscrepancyCount`、`IsConsistent`、`AlertTriggered`、`CorrectionTriggered`；`ReconciliationRecordDto` 含 `RecordId`、`ReportType`、`ReconciledAt`、`Status`、`DiscrepancyCount`、`AlertTriggered`、`CorrectionTriggered`、`ErrorMessage`；触发对账返回单条 `ReconciliationRecordDto` 或列表。
- **数据加载策略**：进入页面并行 GET 状态 + GET 记录列表；触发后刷新两者。
- **缓存策略**：不缓存（对账结果实时）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行 GET `/api/admin/statistics/reconciliation-status` + GET `/api/admin/statistics/reconciliation-records?start={近7天}` → 状态卡片 + 表格渲染。
  2. 选择报表类型与时间范围 → 点击「触发对账」 → `ConfirmDialog` → POST `/api/admin/statistics/reconcile?reportType=...&start=...&end=...` → `message.success('对账已触发')` 1.5s。
  3. 对账完成（同步返回） → 刷新状态卡片 + 记录列表。
  4. 点击「详情」 → 抽屉展示对账记录全字段 + 差异项明细。
- **分支流程**：
  - 对账有差异：状态卡片显示「有差异」红色 + 差异项数量橙色；记录列表差异项 > 0 行高亮。
  - 对账触发告警：状态卡片「告警」红色；记录列表对应行 `AlertTriggered` 标签红。
  - 对账失败：`ErrorMessage` 非空，详情抽屉展示错误信息；`message.error` 3s。
  - 未选报表类型（全部对账）：返回多条记录，`message.info('已对账全部报表类型，共 N 条记录')` 1.5s。
- **跨页面流转**：从报表快照页跳转携带 `reportType={type}` 自动筛选；点击差异项「查看相关审计」跳 `/audit/audit-logs?resourceType=Reconciliation`。
- **状态机可视化**：对账状态 `StatusTag` 自定义 reconciliation 类型：Consistent 绿、Discrepancy 黄、Failed 红。

## 5. 组件清单
- **基础组件**：`<a-statistic>`、`<a-table>`、`<a-drawer>`、`<a-select>`、`<a-descriptions>`、`<a-tag>`、`<a-alert>`
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）— 状态卡片
  - `DataTable`（见 shared/components.md §6）
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `IdempotencyButton`（见 shared/components.md §2）— 触发对账
  - `StatusTag`（见 shared/components.md §1）— 对账状态
  - `ConfirmDialog`（见 shared/components.md §10）— 触发确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlayCircleOutlined`（触发）、`CheckCircleOutlined`（一致）、`WarningOutlined`（差异）、`ExclamationCircleOutlined`（告警）16px。
- **空状态**：「暂无对账记录」+ CTA「触发首次对账」。

## 6. 视觉规范
- **主色应用**：状态数值 24px semibold；触发按钮主色；一致状态绿色 `#52C41A`。
- **状态色**：Consistent `#52C41A`、Discrepancy `#FAAD14`、Failed `#FF4D4F`；告警红、修正橙。
- **间距**：状态卡片间距 24px；卡片与表格 16px；表格行高 48px；抽屉内边距 24px。
- **字体**：表格 14px；报表类型 14px medium；差异值 12px monospace；错误信息 12px `#FF4D4F`。
- **图标尺寸**：状态图标 16px。

## 7. 异常处理与边界
- **加载态**：状态卡片 `<a-skeleton>`；表格 `<a-skeleton>`；抽屉 `<a-spin>`；触发按钮 loading。
- **空数据**：状态显示「尚未执行对账」；记录列表 `EmptyState` CTA「触发首次对账」。
- **错误态**：对账失败 `message.error(ErrorMessage)` 3s；网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；触发对账 `PermissionGuard permission="reconciliation:trigger"`。
- **并发与乐观锁**：无乐观锁；触发对账幂等（`IdempotencyButton` 携带 `Idempotency-Key`），重复触发 300ms 内拦截。
- **危险操作确认**：触发对账 `ConfirmDialog` 内容「触发对账将重新计算指定报表类型的统计指标并与各域数据比对，可能耗时较长（视数据量而定）。是否继续？」确认按钮主色。

## 8. 验收要点
- [ ] 状态卡片实时反映最近一次对账结果
- [ ] 触发对账携带 Idempotency-Key 幂等
- [ ] 差异项 > 0 时状态卡片与表格行高亮
- [ ] 详情抽屉展示差异项明细
- [ ] 时间范围默认近 7 天
- **性能要求**：首屏 < 1.5s；对账触发不阻塞 UI（按钮 loading）；表格 > 100 行启用虚拟滚动。
- **可访问性**：表格键盘导航；状态卡片有 aria-label；对话框聚焦管理。
