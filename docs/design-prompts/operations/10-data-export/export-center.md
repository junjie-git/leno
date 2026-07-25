# 导出中心 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：10-data-export 数据导出
- **页面类型**：工作台页（导出任务创建 + 历史任务管理）
- **目标用户**：运营管理员（Operator）
- **核心目标**：统一管理各业务域数据导出任务（订单/支付/售后/商品/通知等），支持创建异步导出任务、查看任务进度、下载导出文件，避免大数据量同步导出阻塞。
- **访问入口**：左侧菜单「数据导出 → 导出中心」；各列表页「导出」按钮跳转（携带业务类型预设）。
- **实现状态**：➕ 补充功能（导出任务端点待补充，当前基于各业务域列表端点同步导出）

## 2. 页面布局与信息架构
- **整体布局**：顶部新建导出任务区 + 中部任务列表表格 + 底部任务详情抽屉（含进度与下载）。
- **关键区域**：
  - 区域 A（新建任务）：`<a-form inline>` 含业务类型（订单/支付/退款/售后/商品/通知/评价/卖家）、时间范围、筛选条件（按业务类型动态展示）、`<IdempotencyButton>` 创建导出任务
  - 区域 B（任务列表）：`<a-table>` 列含任务名称、业务类型、时间范围、记录数、状态（排队中/处理中/已完成/失败）、创建人、创建时间、操作列
  - 区域 C（任务详情抽屉）：`<a-drawer width="640">` 展示任务参数、处理进度、记录数、文件下载链接、失败原因、处理日志
  - 区域 D（下载区）：已完成任务提供 Excel/CSV 下载链接，含有效期提示（7 天）
- **响应式断点**：≥1200px 表格全展开；992-1199px 抽屉宽度自适应。
- **首屏内容**：新建任务区 + 近7天导出任务列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ 业务类型:[订单▼] 时间范围:[近7天] 筛选:[状态▼]   │
│ [创建导出任务]                                    │
├──────────────────────────────────────────────────┤
│ 任务名称  类型  时间范围  记录数 状态 创建时间 操作│
│ 订单导出  订单  07-19~07-26 1280 已完成 14:30 [下载][详情]│
│ 支付导出  支付  07-19~07-26 -    处理中 14:28 [详情]│
│ 售后导出  售后  07-19~07-26 -    排队中 14:25 [详情]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **规划补充端点**（➕）：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| POST | `/api/admin/data-exports` | 创建异步导出任务 | Operator, Admin |
| GET | `/api/admin/data-exports` | 分页查询导出任务 | Operator, Admin |
| GET | `/api/admin/data-exports/{taskId}` | 查询导出任务详情（含进度） | Operator, Admin |
| GET | `/api/admin/data-exports/{taskId}/download` | 下载导出文件 | Operator, Admin |
| DELETE | `/api/admin/data-exports/{taskId}` | 删除导出任务与文件 | Operator, Admin |

- **当前降级方案**：导出端点上线前，各业务域列表页「导出」按钮基于现有列表端点（如 `/api/admin/orders`、`/api/admin/payments`、`/api/admin/after-sales` 等）同步拉取数据并前端生成 Excel，仅支持小数据量（<10000 行）。
- **请求参数**：`CreateDataExportTaskDto` 含 `BusinessType`（Order/Payment/Refund/AfterSales/Product/Notification/Review/Seller）、`DateRange`、`Filters`（业务特定筛选 JSON）；查询参数 `businessType`、`status`、`page`、`pageSize`。
- **响应字段**：`DataExportTaskDto`，含 `Id`、`TaskName`、`BusinessType`、`DateRange`、`Filters`、`RecordCount`、`Status`（Queued/Processing/Completed/Failed）、`FileUrl`、`FileExpiresAt`、`ErrorMessage`、`CreatedBy`、`CreatedAt`、`CompletedAt`。
- **数据加载策略**：进入页面加载近7天导出任务；处理中任务每 10 秒轮询进度；完成后停止轮询。
- **缓存策略**：不缓存，任务状态实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载近7天导出任务 → 渲染表格
  2. 选择业务类型与时间范围 → 配置筛选条件 → 点击「创建导出任务」→ 调用创建接口 → 任务列表新增排队中行
  3. 任务处理中 → 每 10 秒轮询进度 → 状态变更为已完成
  4. 已完成任务点击「下载」→ 调用下载接口 → 浏览器下载文件
  5. 点击「详情」→ 打开抽屉展示任务参数、进度、日志
- **分支流程**：
  - 任务失败：状态变更为失败，详情抽屉展示错误信息，操作列增加「重试」
  - 文件过期：下载链接失效，提示「文件已过期，请重新创建导出任务」
  - 大数据量导出：记录数 >100000 拆分为多个文件，详情抽屉展示分片下载链接
- **跨页面流转**：各列表页「导出」按钮跳转本页并预填业务类型与筛选条件。
- **状态机可视化**：Queued（排队中）→ Processing（处理中）→ Completed（已完成）/ Failed（失败）。失败可重试回 Queued。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-select>`、`<a-date-picker>`、`<a-drawer>`、`<a-progress>`、`<a-button>`、`<a-statistic>`
- **业务组件**：
  - `IdempotencyButton`（见 shared/components.md §2）— 创建导出任务按钮
  - `PermissionGuard`（见 shared/components.md §3）— 创建/删除权限控制，permission='data:export'
  - `DataTable`（见 shared/components.md §6）— 任务列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 时间范围
  - `ConfirmDialog`（见 shared/components.md §10）— 删除任务二次确认
  - `EmptyState`（见 shared/components.md §5）— 无任务时展示
- **图标使用**：`DownloadOutlined` 下载、`PlusOutlined` 创建、`EyeOutlined` 详情、`DeleteOutlined` 删除、`RedoOutlined` 重试、`FileExcelOutlined` 文件
- **空状态**：`EmptyState` title="暂无导出任务" ctaText="创建导出任务"

## 6. 视觉规范
- **主色应用**：创建/下载按钮主色 `#1677FF`，删除按钮危险色 `#FF4D4F`，重试按钮默认色。
- **状态色**：排队中 `#8C8C8C` 灰、处理中 `#1677FF` 蓝、已完成 `#52C41A` 绿、失败 `#FF4D4F` 红。
- **进度条**：`<a-progress>` 主色 `#1677FF`，失败 `#FF4D4F`。
- **间距**：新建任务区与表格 16px，表格行高 48px，抽屉区块 24px。
- **字体**：任务名称 14px medium，记录数 14px `#000000D9`，时间 12px `#8C8C8C`，进度百分比 14px semibold。
- **图标尺寸**：操作列图标 16px，文件图标 20px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；创建任务按钮 loading；处理中任务进度条动画。
- **空数据**：列表空显示「暂无导出任务」+ 创建 CTA。
- **错误态**：创建失败 `message.error('创建导出任务失败，请重试')`；下载失败 `message.error('文件下载失败，请重试')`；任务失败详情展示错误信息。
- **权限控制**：Operator/Admin 可访问；创建/删除需 `data:export` 权限。
- **并发与乐观锁**：创建任务幂等（`IdempotencyButton` + Idempotency-Key）；同一业务类型同一时间范围 5 分钟内不可重复创建。
- **危险操作确认**：删除任务为危险操作，强制 `<ConfirmDialog>`，说明文件将一并删除。
- **资源限制**：单任务最大导出 100 万行，超限提示拆分时间范围；文件保留 7 天自动清理。

## 8. 验收要点
- [ ] 新建任务区支持业务类型/时间范围/筛选条件配置
- [ ] 任务列表展示状态、记录数、创建时间、操作列
- [ ] 处理中任务每 10 秒轮询进度
- [ ] 已完成任务支持下载，过期提示重新创建
- [ ] 失败任务支持重试
- [ ] 同业务同时间范围 5 分钟内不可重复创建
- **性能要求**：任务列表 < 800ms，进度轮询 10 秒间隔，单任务最大 100 万行。
- **可访问性**：进度条 aria-valuenow 含百分比，状态标签 aria-label 含中文状态名。
- **待补充**：➕ 后端实现 `/api/admin/data-exports/*` 异步导出端点后，替换当前同步导出降级方案，支持大数据量与后台处理。
