# 审计日志 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：05-audit 审计与对账
- **页面类型**：列表页 + 详情抽屉 + 导出
- **目标用户**：系统管理员（Admin）、运营管理员（Operator）
- **核心目标**：查询跨域审计日志条目（AuditLogEntry）与操作日志，按操作人、模块、资源类型、时间区间筛选，查看详情并导出 CSV 用于合规追溯。
- **访问入口**：Sider「审计与对账 → 审计日志」/ 死信队列/索引重建等页面跳转
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 Tab 切换 + 筛选条 + 主表格 + 详情抽屉 + 导出按钮。
- **关键区域**：
  - 区域 A（Tab 切换）：`<a-tabs>` 切换「审计日志」「操作日志」「跨域审计条目」三个视图。
  - 区域 B（筛选条）：操作人下拉（OperatorId）+ 资源类型/模块下拉（ResourceType/Module）+ 操作动作输入（Action）+ 时间范围 `DateTimeRangePicker` + 「查询」「导出 CSV」按钮。
  - 区域 C（主表格）：列含日志ID/操作人/角色/来源上下文/操作类型/资源类型/资源ID/响应状态/IP/发生时间/操作（详情），分页 20。
  - 区域 D（详情抽屉）：`<a-drawer width="720">` 展示 `AuditLogViewer`（见 shared/components.md §9）— 结构化字段 + 请求摘要 + 操作前后快照 JSON 高亮 + 链路追踪 TraceId。
- **响应式断点**：≥1200px 表格 11 列横向滚动；992-1199px 隐藏「角色」「资源ID」「IP」。
- **首屏内容**：近 24 小时审计日志列表（默认按发生时间倒序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [审计日志] [操作日志] [跨域审计条目]             │
├────────────────────────────────────────────────┤
│ [操作人] [资源类型] [动作] [时间范围] [查询][导出]│
├────────────────────────────────────────────────┤
│ ID │ 操作人 │ 模块 │ 动作 │ 资源 │ 状态 │ 时间 │ 操作 │
│ xxx│ zhang │ Order│ 创建 │ Shop │ 200 │ 14:30│ 详情 │
└────────────────────────────────────────────────┘
 抽屉：全字段 + 请求摘要 + 前后快照 JSON + TraceId
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/audit-logs` | 分页查询审计日志（按操作人/资源类型/时间） | Admin,Operator |
| GET | `/api/admin/audit-logs/{id}` | 获取审计日志条目详情 | Admin,Operator |
| GET | `/api/admin/audit-logs/export` | 导出审计日志为 CSV 文件 | Admin,Operator |
| GET | `/api/admin/operation-logs` | 分页查询操作日志（按操作人/模块/时间） | Admin,Operator |
| GET | `/api/admin/audit-log-entries` | 分页查询跨域审计日志条目（按模块/动作/时间/操作人） | Admin,Operator |

- **请求参数**：审计日志 `operatorId/resourceType/fromTime/toTime/page/pageSize`；操作日志 `operatorId/module/fromTime/toTime/page/pageSize`；跨域条目 `module/action/fromTime/toTime/operatorId/page/pageSize`；导出 `operatorId/resourceType/fromTime/toTime`。
- **响应字段**：`AuditLogListResultDto`（items + total）；`AuditLogEntryDto` 含 `LogId`、`OperatorId`、`OperatorName`、`OperatorRole`、`SourceContext`、`Action`、`ResourceType`、`ResourceId`、`RequestSummary`、`ResponseStatus`、`IpAddress`、`UserAgent`、`TraceId`、`BeforeSnapshot`、`AfterSnapshot`、`OccurredAt`；`OperationLogListResultDto` 含操作日志条目。
- **数据加载策略**：进入页面加载首页；Tab 切换重新请求；筛选重新请求；详情按需加载。
- **缓存策略**：不缓存（审计日志只读且实时）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认 Tab「审计日志」→ GET `/api/admin/audit-logs?fromTime={近24h}&page=1&pageSize=20` → 表格渲染。
  2. 切换 Tab → 按对应端点重新请求 → 表格刷新。
  3. 配置筛选条件 → 点击「查询」 → 携带参数重新请求。
  4. 点击「导出 CSV」 → GET `/api/admin/audit-logs/export?...` → 浏览器下载 `audit-logs.csv`。
  5. 点击「详情」 → GET `/api/admin/audit-logs/{id}` → 抽屉展示 `AuditLogViewer` 全字段 + 前后快照 JSON。
- **分支流程**：
  - 审计日志条目不存在：后端 404，`message.error('审计日志条目不存在')` 3s。
  - 导出数据量过大（> 10 万条）：后端限制，`message.warning('导出数据量过大，请缩小时间范围')` 2s。
  - 时间范围未选：默认近 24 小时。
- **跨页面流转**：从死信队列页跳转携带 `resourceType=DeadLetter&keyword={messageId}` 自动筛选；从索引重建页跳转携带 `resourceType=IndexRebuild`；点击 TraceId 跳 `/monitoring/prometheus-dashboard?traceId={traceId}`（预留）。
- **状态机可视化**：无状态机（只读日志）。响应状态码用 `<a-tag>` 着色：2xx 绿、4xx 黄、5xx 红。

## 5. 组件清单
- **基础组件**：`<a-tabs>`、`<a-table>`、`<a-drawer>`、`<a-select>`、`<a-input>`、`<a-tag>`、`<a-button>`、`<a-descriptions>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `AuditLogViewer`（见 shared/components.md §9）— 详情展示
  - `PermissionGuard`（见 shared/components.md §3）— 导出权限
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`FileSearchOutlined`（详情）、`DownloadOutlined`（导出）、`SearchOutlined`（查询）16px。
- **空状态**：「暂无审计日志」+ CTA「清空筛选条件」。

## 6. 视觉规范
- **主色应用**：查询按钮主色；导出按钮默认色；Tab 激活态主色下划线。
- **状态色**：响应状态 2xx `#52C41A`、4xx `#FAAD14`、5xx `#FF4D4F`；操作人角色 Admin 红、Operator 蓝、Seller 绿、Buyer 灰。
- **间距**：Tab 与表格 16px；筛选条与表格 16px；表格行高 48px；抽屉内边距 24px；JSON 体 12px monospace。
- **字体**：表格 14px；日志ID/TraceId 12px monospace；请求摘要 12px `#595959`。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；抽屉 `<a-spin>`；导出按钮 loading。
- **空数据**：`EmptyState` 兜底，CTA「清空筛选条件」重置筛选。
- **错误态**：网络错误 `message.error` 3s；404 详情不存在提示并关闭抽屉。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；导出 `PermissionGuard permission="audit-log:export"`。
- **并发与乐观锁**：无（只读页面）。
- **危险操作确认**：导出为只读操作无需二次确认；但导出超过 10 万条时 `Modal.confirm` 提示「导出数据量较大，可能耗时较长，是否继续？」。

## 8. 验收要点
- [ ] 三个 Tab 切换正确加载对应数据
- [ ] 筛选条件组合查询正确
- [ ] 导出 CSV 文件名含日期
- [ ] 详情抽屉展示前后快照 JSON 高亮且可折叠
- [ ] TraceId 展示且可复制
- **性能要求**：首屏 < 1.5s；表格 > 100 行启用虚拟滚动；导出异步不阻塞 UI。
- **可访问性**：表格键盘导航；Tab 支持方向键切换；JSON 体可折叠；对话框聚焦管理。
