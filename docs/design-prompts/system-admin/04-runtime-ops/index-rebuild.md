# 索引重建 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：04-runtime-ops 运行时运维
- **页面类型**：列表页 + 详情抽屉 + 触发弹窗
- **目标用户**：系统管理员（Admin）
- **核心目标**：触发各域 ES 读库全量索引重建（IndexRebuildTask），跟踪任务进度，重试失败任务。
- **访问入口**：Sider「运行时运维 → 索引重建」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 触发弹窗 + 详情抽屉（含进度条）。
- **关键区域**：
  - 区域 A（筛选条）：目标上下文多选 + 状态多选（待执行/执行中/成功/失败）+ 「触发重建」按钮。
  - 区域 B（主表格）：列含任务ID/目标上下文/索引名/状态/进度/触发人/触发时间/操作（详情/重试），分页 20。
  - 区域 C（触发弹窗）：`<a-modal>` 含目标上下文下拉（Product/Order/Shop 等）、索引名输入。
  - 区域 D（详情抽屉）：`<a-drawer width="640">` 展示任务全字段 + 进度条 `<a-progress>` + 处理文档数/总文档数 + 失败原因（如失败）。
- **响应式断点**：≥1200px 表格 8 列；992-1199px 隐藏「触发时间」。
- **首屏内容**：近 7 天重建任务列表（按触发时间倒序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [上下文多选] [状态多选] [触发重建]             │
├────────────────────────────────────────────────┤
│ ID │ 上下文 │ 索引名 │ 状态 │ 进度 │ 触发人 │ 操作 │
│ xxx│ Product│ products│执行中│ 65% │ Admin │ 详情│
└────────────────────────────────────────────────┘
→ 弹窗：上下文/索引名
→ 抽屉：进度条 + 文档数 + 失败原因
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/index-rebuild/tasks` | 分页查询索引重建任务 | Admin,Operator |
| POST | `/api/admin/index-rebuild/trigger` | 触发索引重建 | Admin,Operator |
| GET | `/api/admin/index-rebuild/tasks/{id}` | 获取任务详情/进度 | Admin,Operator |
| POST | `/api/admin/index-rebuild/tasks/{id}/retry` | 重试失败任务 | Admin,Operator |

- **请求参数**：列表 `targetContext/status/page/pageSize`；触发 `TriggerIndexRebuildDto`（targetContext/indexName）。
- **响应字段**：`IndexRebuildTaskDto` 含 `TaskId`、`TargetContext`、`IndexName`、`Status`、`TriggeredBy`、`TriggeredAt`、`StartedAt`、`FinishedAt`、`TotalDocs`、`ProcessedDocs`、`ErrorMessage`、`RetryCount`、`EsTaskId`。
- **数据加载策略**：进入页面加载首页；执行中任务每 5s 轮询进度（前端 setInterval）；详情抽屉打开后停止轮询列表。
- **缓存策略**：列表不缓存（任务状态实时变化）；详情不缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/index-rebuild/tasks?page=1&pageSize=20` → 表格渲染。
  2. 执行中任务自动每 5s 轮询刷新进度。
  3. 点击「触发重建」 → 弹窗选择上下文 + 索引名 → POST trigger → `message.success('重建任务已触发')`。
  4. 点击「详情」 → GET `/api/admin/index-rebuild/tasks/{id}` → 抽屉展示进度条与字段。
  5. 点击「重试」（仅失败态可重试） → `ConfirmDialog` → POST retry → 任务回到待执行态。
- **分支流程**：
  - 重建期间查询走旧索引：抽屉提示「重建期间查询走旧索引，切换瞬间有秒级双读窗口」。
  - 触发时索引已有进行中任务：后端 409，`message.error('该索引已有进行中任务')` 3s。
- **跨页面流转**：点击「查看审计」跳 `/audit/audit-logs?resourceType=IndexRebuild&keyword={taskId}`。
- **状态机可视化**：待执行 → 执行中 → 成功/失败；失败 → 重试 → 待执行。`StatusTag` 自定义 indexRebuild 类型：待执行灰、执行中蓝、成功绿、失败红。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-select>`、`<a-input>`、`<a-drawer>`、`<a-progress>`、`<a-descriptions>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 任务状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 重试确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无（进度条用 `<a-progress>`）
- **图标使用**：`PlusOutlined`、`EyeOutlined`、`ReloadOutlined`（重试）、`DatabaseOutlined` 16px。
- **空状态**：「暂无重建任务」+ CTA「触发重建」。

## 6. 视觉规范
- **主色应用**：触发按钮主色；进度条主色；执行中状态 `<a-tag color="processing">`。
- **状态色**：待执行灰、执行中蓝、成功绿、失败红。
- **间距**：筛选条与表格 16px；表格行高 48px；抽屉内边距 24px。
- **字体**：表格 14px；任务ID 12px monospace；进度数值 16px semibold。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：409 重复触发 `message.error` 3s；触发超时后端置失败，列表显示失败状态。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；触发与重试 `PermissionGuard permission="index-rebuild:trigger"`。
- **并发与乐观锁**：无乐观锁（任务状态机后端保证）。
- **危险操作确认**：重试失败任务 `ConfirmDialog` 内容「重试将重新执行索引重建，期间查询走旧索引。原任务记录保留。」确认按钮主色。触发重建本身也确认：「重建期间产生增量事件暂存补偿，重建完成后回放。」

## 8. 验收要点
- [ ] 执行中任务每 5s 自动轮询进度
- [ ] 进度条正确显示 ProcessedDocs/TotalDocs 百分比
- [ ] 仅失败态显示「重试」按钮
- [ ] 触发与重试有二次确认
- **性能要求**：首屏 < 1.5s；轮询不阻塞 UI；进度更新局部刷新。
- **可访问性**：表格键盘导航；进度条 `aria-valuenow`；对话框聚焦管理。
