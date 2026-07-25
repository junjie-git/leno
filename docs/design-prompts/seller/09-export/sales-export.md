# 销售报表导出 - 商家管理后台

## 1. 页面定位
- **所属端**：商家管理后台
- **所属模块**：09-export（报表导出）
- **页面类型**：表单页 + 列表页（导出任务管理）
- **目标用户**：卖家（Seller）
- **核心目标**：卖家按时间范围与数据维度（销售趋势、运营指标明细）导出 Excel/CSV 报表，查看历史导出任务状态并下载已完成的报表文件。
- **访问入口**：Sider「报表导出 → 销售报表导出」；URL `/export/sales`；工作台「导出报表」快捷入口。
- **实现状态**：➕ 补充功能

## 2. 页面布局与信息架构
- **整体布局**：顶部面包屑 + 左右两栏（左侧导出配置表单 + 右侧历史导出任务列表）。
- **关键区域**：
  - 区域 A（面包屑）：首页 / 报表导出 / 销售报表导出。
  - 区域 B（左侧导出配置 `<a-card>` 标题「新建导出任务」）：
    - B1 报表类型（`<a-radio-group>`）：销售趋势汇总（按日）/ 运营指标明细（按日）。
    - B2 时间范围（`<a-range-picker>`）：必填，预设近 7 天 / 近 30 天 / 近 90 天 / 本月 / 上月，单次最大 90 天。
    - B3 数据维度（`<a-checkbox-group>`）：订单数、销售金额、商品数、平均评分、评分数、退款数（按报表类型动态可选）。
    - B4 文件格式（`<a-radio-group>`）：Excel（.xlsx）/ CSV（.csv，UTF-8 BOM）。
    - B5 导出按钮：「立即导出」主按钮 + 预估数据量提示（如「预计导出 90 行数据」）。
  - 区域 C（右侧历史导出任务 `<a-card>` 标题「历史导出任务」）：
    - C1 任务列表 `<a-table>`：列含任务 ID、报表类型、时间范围、文件格式、状态（StatusTag：处理中/已完成/失败）、创建时间、操作（下载/重试/删除）。
    - C2 状态说明：处理中显示进度条（小数据量前端导出 < 10s；大数据量后端异步任务轮询进度），已完成显示文件大小与下载按钮，失败显示失败原因与重试按钮。
- **响应式断点**：≥1200px 左右两栏（左 12 / 右 12 栅格）；992-1199px 单栏堆叠，导出配置在上；<992px 不支持。
- **首屏内容**：导出配置表单 + 历史任务列表前 10 条。
- **线框图描述**：
```
┌────────────────────────────────────────┬────────────────────────────────────┐
│ 面包屑：首页 / 报表导出 / 销售报表导出     │                                    │
├────────────────────────────────────────┼────────────────────────────────────┤
│ 新建导出任务                              │ 历史导出任务                          │
│                                        │                                    │
│ 报表类型                                │ 任务ID  | 类型   | 时间范围   | 状态  │
│ ◉ 销售趋势汇总  ○ 运营指标明细            │ EXP001 | 趋势汇总| 07-01~07-26| 已完成│
│                                        │ EXP002 | 指标明细| 07-20~07-26| 处理中│
│ 时间范围                                │ EXP003 | 趋势汇总| 06-26~07-25| 失败  │
│ [2026-07-01] ~ [2026-07-26]             │                                    │
│ [近7天][近30天][近90天][本月][上月]        │                                    │
│                                        │                                    │
│ 数据维度                                │                                    │
│ ☑订单数 ☑销售金额 ☐商品数                │                                    │
│ ☐平均评分 ☐评分数 ☐退款数                 │                                    │
│                                        │                                    │
│ 文件格式                                │                                    │
│ ◉ Excel(.xlsx)  ○ CSV(.csv)            │                                    │
│                                        │                                    │
│ 预计导出 26 行数据                       │                                    │
│ [立即导出]                              │                                    │
└────────────────────────────────────────┴────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/seller/sales-trend` | 查询销售趋势（按日），用于前端导出（≤90 天） | Seller |
| GET | `/api/seller/metrics` | 查询运营指标明细（按日），用于前端导出（≤90 天） | Seller |
| POST | `/api/seller/export/sales` | 创建异步导出任务（大数据量或后端导出场景，补充端点） | Seller（补充） |
| GET | `/api/seller/export/tasks` | 查询导出任务列表（补充端点） | Seller（补充） |
| GET | `/api/seller/export/tasks/{id}/download` | 下载已完成的导出文件（补充端点） | Seller（补充） |

- **请求参数**：
  - 销售趋势查询：`DateOnly from`、`DateOnly to`，后端从 JWT 注入 `sellerId` 关联店铺。
  - 运营指标查询：`DateOnly from`、`DateOnly to`。
  - 创建导出任务：`CreateExportTaskDto { reportType: 'SalesTrend' | 'ShopMetrics', from: DateOnly, to: DateOnly, dimensions: string[], fileFormat: 'Excel' | 'Csv' }`。
  - 任务列表查询：`int page`（1-based，默认 1）、`int pageSize`（默认 10）、`string? status`（可选状态过滤）。
- **响应字段**：
  - 销售趋势返回 `SalesTrendDto[]`（每个含 `date`、`orderCount`、`salesAmount`、`salesCurrency`、`avgRating`）。
  - 运营指标返回 `ShopMetricsDto[]`（每个含 `shopId`、`date`、`orderCount`、`salesAmount`、`salesCurrency`、`productCount`、`avgRating`、`ratingCount`、`refundCount`）。
  - 创建导出任务返回 `ExportTaskDto { taskId, status: 'Processing' | 'Completed' | 'Failed', createdAt }`。
  - 任务列表返回 `ExportTaskListResultDto { items[], total, page, pageSize }`，每个任务含 `taskId`、`reportType`、`from`、`to`、`fileFormat`、`status`、`fileUrl?`、`fileSize?`、`errorMessage?`、`createdAt`、`completedAt?`。
- **数据加载策略**：
  - 小数据量（≤90 天）：点击「立即导出」前端调用 `GET /api/seller/sales-trend` 或 `GET /api/seller/metrics` 拉取数据，使用 `xlsx` 库生成 Excel 或 `papaparse` 生成 CSV，浏览器直接下载。
  - 大数据量（>90 天 或前端导出超时）：调用 `POST /api/seller/export/sales` 创建后端异步任务，轮询任务状态（每 3 秒），完成后下载。
  - 进入页面加载历史任务列表第一页。
- **缓存策略**：导出任务列表不缓存；已下载文件浏览器缓存 5 分钟。

## 4. 交互流程
- **主流程**：
  1. 卖家进入页面 → 加载历史导出任务列表第一页 → 渲染任务表格。
  2. 卖家选择报表类型（销售趋势汇总 / 运营指标明细）→ 数据维度 checkbox 按类型动态启用/禁用 → 选择时间范围（预设或自定义，单次 ≤90 天）→ 选择文件格式 → 预估数据量实时更新（按时间范围天数计算）。
  3. 卖家点击「立即导出」→ 按钮立即 loading + disabled → 判断数据量：
     - ≤90 天：前端调用对应查询接口拉取数据 → 生成 Excel/CSV → `message.success('导出成功')` → 浏览器自动下载文件 → 在历史任务列表插入一条「已完成」记录（前端生成）。
     - 异常或大数据量：调用 `POST /api/seller/export/sales` 创建后端任务 → 历史列表新增「处理中」任务 → 每 3 秒轮询 `GET /api/seller/export/tasks` → 状态变为「已完成」后停止轮询。
  4. 卖家点击「下载」（已完成态）→ 调用 `GET /api/seller/export/tasks/{id}/download` → 浏览器下载文件。
  5. 卖家点击「重试」（失败态）→ `Modal.confirm` 二次确认 → 调用 `POST /api/seller/export/sales` 重新创建任务。
  6. 卖家点击「删除」（任意态）→ `Modal.confirm` 二次确认 → 调用删除端点 → 从列表移除。
- **分支流程**：
  - 时间范围超过 90 天：表单校验红色提示「单次导出最大 90 天，请缩小范围或拆分多次」。
  - 时间范围未选：导出按钮禁用。
  - 数据维度未勾选：导出按钮禁用，提示「请至少选择一个数据维度」。
  - 前端导出失败（网络异常或数据量过大）：降级调用后端异步任务。
  - 后端任务失败：状态变「失败」，显示 `errorMessage`，提供「重试」按钮。
- **跨页面流转**：导出操作停留在本页；下载文件不跳转页面。
- **状态机可视化**：导出任务状态机：处理中 →（生成成功）→ 已完成 /（生成失败）→ 失败；失败 →（重试）→ 处理中；已完成/失败 →（删除）→ 已删除（终态）。

## 5. 组件清单
- **基础组件**：`<a-page-header>`、`<a-card>`、`<a-form>`、`<a-form-item>`、`<a-radio-group>`、`<a-radio>`、`<a-range-picker>`、`<a-checkbox-group>`、`<a-checkbox>`、`<a-button>`、`<a-table>`、`<a-pagination>`、`<a-tooltip>`、`<a-progress>`（任务进度）、`<a-statistic>`（数据量预估）。
- **业务组件**：`StatusTag`（见 shared/components.md §1，type="export"）— 任务状态展示；`IdempotencyButton`（见 shared/components.md §2）— 立即导出按钮防重；`ConfirmDialog`（见 shared/components.md §10）— 重试/删除二次确认；`EmptyState`（见 shared/components.md §5）— 无历史任务占位；`DateTimeRangePicker`（见 shared/components.md §4）— 时间范围选择。
- **图表组件**：无（导出页不展示图表，仅数据导出）。
- **图标使用**：`DownloadOutlined`（下载）、`ReloadOutlined`（重试）、`DeleteOutlined`（删除）、`FileExcelOutlined`（Excel 文件）、`FileTextOutlined`（CSV 文件）、`ExportOutlined`（导出）。
- **空状态**：`<EmptyState title="暂无导出任务" description="点击左侧配置并导出第一份报表" />`。

## 6. 视觉规范
- **主色应用**：立即导出按钮、下载链接、分页激活态主色 `#1677FF`；Excel 文件图标绿色 `#52C41A`；CSV 文件图标蓝色 `#1677FF`。
- **状态色**：处理中蓝 `#1677FF`、已完成绿 `#52C41A`、失败红 `#FF4D4F`（由 StatusTag 映射）。
- **间距**：左右两栏间距 `24px`，卡片内边距 `24px`，表单项间距 `24px`，任务表格行高 `56px`，单元格内边距 `12px`。
- **字体**：页面标题 `20px` medium，卡片标题 `16px` medium，表单标签 `14px` normal，正文 `14px` normal，数据量预估 `12px` `#8C8C8C`，文件大小 `12px` `#595959`。
- **图标尺寸**：操作图标 `16px`，文件类型图标 `20px`，按钮图标 `16px`。

## 7. 异常处理与边界
- **加载态**：进入页面 `<a-skeleton>` 模拟任务表格 5 行；导出按钮 loading；任务轮询时表格行 `<a-spin>` 内嵌进度。
- **空数据**：历史任务列表为空时展示 `EmptyState`。
- **错误态**：网络错误 `message.error('网络异常')`；401 跳转登录页；403 `message.error('无权限访问')`；查询数据失败 `message.error('数据加载失败，请稍后重试')`；导出任务创建失败展示后端 `response.data.message`。
- **权限控制**：需卖家登录态；后端按 JWT `sellerId` 过滤，仅返回当前卖家的导出任务；非本店任务下载返回 403。
- **并发与乐观锁**：导出任务创建幂等（同参数 5 分钟内重复创建返回同一任务）；任务轮询每 3 秒一次，状态变更后停止。
- **危险操作确认**：
  - 重试失败任务：`Modal.confirm` 标题「确认重试导出任务」，内容「将重新生成该报表，原失败记录将被覆盖。」，确认按钮主色。
  - 删除任务：`Modal.confirm` 标题「确认删除导出任务」，内容「删除后任务记录与文件将无法恢复。」，确认按钮危险色 `#FF4D4F`。

## 8. 验收要点
- [ ] 报表类型切换正确联动数据维度 checkbox 可选状态
- [ ] 时间范围选择支持预设与自定义，单次最大 90 天校验
- [ ] 数据维度至少勾选一项，否则导出按钮禁用
- [ ] 文件格式选择 Excel/CSV 生效
- [ ] 预估数据量按时间范围天数实时更新
- [ ] 立即导出按钮 loading + 防重，小数据量前端生成文件下载
- [ ] 大数据量或前端导出异常降级为后端异步任务
- [ ] 历史任务列表正确展示任务 ID、类型、时间范围、格式、状态、创建时间
- [ ] 处理中任务每 3 秒轮询状态，完成后停止
- [ ] 已完成任务可下载，失败任务可重试，任意任务可删除
- [ ] 删除与重试操作需二次确认
- **性能要求**：首屏加载 < 1.5s；前端导出（≤90 天）< 10s；后端异步任务轮询间隔 3s；下载响应 < 5s。
- **可访问性**：表单字段有 `label` 与 `aria-label`；按钮有 `aria-label`；任务状态有 `aria-label`；进度条有 `aria-label`；对比度满足 WCAG AA；支持 Tab 键导航。

> **补充功能说明**：当前后端 `SellerDashboardController` 已实现 `GET /api/seller/sales-trend` 与 `GET /api/seller/metrics` 两个数据查询端点，可作为前端导出的数据源（≤90 天场景）。本页面需后端新增三个导出相关端点：
> 1. `POST /api/seller/export/sales` —— 创建异步导出任务，接收 `CreateExportTaskDto`（报表类型、时间范围、维度、格式），后台 Job 调用现有 `ISellerDashboardAppService.GetSalesTrendAsync` 或 `GetShopMetricsAsync` 拉取数据，使用 `EPPlus` 或 `CsvHelper` 生成文件，上传至文件存储后返回 `ExportTaskDto`。
> 2. `GET /api/seller/export/tasks` —— 分页查询当前卖家的导出任务列表，按 `createdAt` 倒序。
> 3. `GET /api/seller/export/tasks/{id}/download` —— 下载已完成的导出文件，校验任务归属与状态，返回文件流。
> 前端导出依赖 `xlsx`（SheetJS）与 `papaparse`（CSV）库，已在 `package.json` 中声明。
