# 定时任务 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：04-runtime-ops 运行时运维
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）
- **核心目标**：管理定时任务（ScheduledTask），CRUD/启停/立即触发，监控任务执行状态。
- **访问入口**：Sider「运行时运维 → 定时任务」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 新建/编辑弹窗 + 执行历史抽屉。
- **关键区域**：
  - 区域 A（筛选条）：任务名搜索 + 状态多选（启用/停用）+ 作业类型筛选 + 「新增任务」按钮。
  - 区域 B（主表格）：列含任务名/Cron 表达式/作业类型/状态/最近执行/下次执行/操作（编辑/启用/停用/立即执行/历史），分页 20。
  - 区域 C（弹窗表单）：`<a-modal width="560">` 含任务名、作业类型（下拉，编辑时只读）、Cron 表达式（含下次执行预览）、参数 JSON、状态。
  - 区域 D（执行历史抽屉）：`<a-drawer width="640">` 展示最近 20 次执行记录（开始/结束/状态/错误信息）。
- **响应式断点**：≥1200px 表格 8 列；992-1199px 隐藏「下次执行」。
- **首屏内容**：全部任务列表（按任务名字母序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [搜索] [状态多选] [作业类型 ▼] [新增任务]      │
├────────────────────────────────────────────────┤
│ 名称 │ Cron │ 类型 │ 状态 │ 最近 │ 下次 │ 操作  │
│ 对账任务│0 2 * * *│Reconciliation│启用│07-26 02:00│07-27 02:00│编辑/停用/立即执行/历史│
└────────────────────────────────────────────────┘
→ 弹窗：名称/类型/Cron/参数/状态
→ 抽屉：执行历史列表
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/scheduled-tasks` | 分页查询定时任务 | Admin,Operator |
| POST | `/api/admin/scheduled-tasks` | 创建定时任务（初始停用态） | Admin,Operator |
| PUT | `/api/admin/scheduled-tasks/{taskId}` | 更新定时任务（作业类型不可变） | Admin,Operator |
| POST | `/api/admin/scheduled-tasks/{taskId}/enable` | 启用任务并向调度器注册 | Admin,Operator |
| POST | `/api/admin/scheduled-tasks/{taskId}/disable` | 停用任务并从调度器注销 | Admin,Operator |
| POST | `/api/admin/scheduled-tasks/{taskId}/run-now` | 立即触发任务执行 | Admin,Operator |

- **请求参数**：列表 `name/status/page/pageSize`；创建 `SaveScheduledTaskDto`（name/jobType/cronExpression/parameters）；编辑 `UpdateScheduledTaskDto`。
- **响应字段**：`ScheduledTaskDto` 含 `TaskId`、`Name`、`JobType`、`CronExpression`、`Parameters`、`Status`、`LastRunAt`、`NextRunAt`、`CreatedAt`。
- **数据加载策略**：进入页面加载首页；筛选重新请求；启用/停用/立即执行后刷新当前页。
- **缓存策略**：列表不缓存（任务状态实时变化）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/scheduled-tasks?page=1&pageSize=20` → 表格渲染。
  2. 点击「新增任务」 → 弹窗填表（含 Cron 校验） → POST → `message.success('任务已创建（停用态）')`。
  3. 点击「编辑」 → 弹窗预填（作业类型只读） → PUT → 刷新。
  4. 点击「启用」 → `ConfirmDialog` → POST enable → 状态变为启用，下次执行时间显示。
  5. 点击「立即执行」 → `ConfirmDialog` → POST run-now → `message.success('已触发立即执行')`。
  6. 点击「历史」 → 抽屉展示最近 20 次执行记录。
- **分支流程**：
  - Cron 表达式非法：前端校验 + 后端 400，`message.error('Cron 表达式不合法')` 3s。
  - 立即执行时任务已停用：后端 400，`message.error('任务已停用，请先启用')` 3s。
  - 作业类型编辑：前端 disabled，Tooltip「作业类型不可变」。
- **跨页面流转**：点击「查看执行审计」跳 `/audit/audit-logs?resourceType=ScheduledTask&keyword={taskId}`。
- **状态机可视化**：停用 → 启用 → 停用，`StatusTag` 自定义 scheduledTask 类型。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-textarea>`、`<a-drawer>`、`<a-tooltip>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 任务状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 启停/立即执行确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`PlayCircleOutlined`（立即执行）、`HistoryOutlined`、`ClockCircleOutlined` 16px。
- **空状态**：「暂无定时任务」+ CTA「新增任务」。

## 6. 视觉规范
- **主色应用**：新增按钮主色；启用状态 `<a-tag color="green">`；Cron 表达式等宽字体。
- **状态色**：启用绿、停用灰。
- **间距**：筛选条与表格 16px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；任务名 14px medium；Cron 12px monospace；参数 JSON 12px monospace。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：Cron 非法 400 `message.error` 3s；立即执行停用任务 400 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；写操作 `PermissionGuard permission="scheduled-task:write"`；立即执行 `permission="scheduled-task:run-now"`。
- **并发与乐观锁**：无乐观锁（任务低频变更）。
- **危险操作确认**：
  - 停用启用中的任务 `ConfirmDialog` 内容「停用后任务将从调度器注销，不再按 Cron 执行。已注册的下一次执行取消。可随时启用恢复。」确认按钮 danger 红色。
  - 立即执行 `ConfirmDialog` 内容「立即执行将忽略 Cron 调度，立即触发一次任务。请确认非高峰时段。」确认按钮主色。

## 8. 验收要点
- [ ] Cron 表达式提交前校验
- [ ] 作业类型编辑时只读
- [ ] 启用后显示下次执行时间
- [ ] 立即执行停用任务有友好提示
- **性能要求**：首屏 < 1.5s；搜索防抖 300ms；执行历史加载 < 800ms。
- **可访问性**：表格键盘导航；Cron 输入有 aria-label；对话框聚焦管理。
