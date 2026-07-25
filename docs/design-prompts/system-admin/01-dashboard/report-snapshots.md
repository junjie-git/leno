# 报表快照 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：01-dashboard 仪表盘
- **页面类型**：列表页 + 详情抽屉
- **目标用户**：系统管理员（Admin）
- **核心目标**：按报表类型与时间范围查看历史快照列表，对比同周期不同 DataVersion 的差异，回溯指标变更。
- **访问入口**：Sider「仪表盘 → 报表快照」/ 各子看板「查看历史快照」链接
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主列表表格 + 详情抽屉（含版本对比）。
- **关键区域**：
  - 区域 A（筛选条）：`<a-select>` 报表类型（订单GMV/支付成功率/积分发放量/通知送达率/售后量/店铺排行/转化率）+ `DateTimeRangePicker` + 状态筛选。
  - 区域 B（主表格）：列含报表类型/周期起止/粒度/数据版本 DataVersion/生成时间/操作（查看/对比），按生成时间倒序，分页 20。
  - 区域 C（详情抽屉）：`<a-drawer width="640">` 展示 Metrics 全量字段（`<a-descriptions>`），含「与上一版本对比」开关，开启后显示差异表格。
- **响应式断点**：≥1200px 表格 8 列全展开；992-1199px 隐藏「粒度」列。
- **首屏内容**：近 7 天订单GMV 报表快照列表。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [报表类型 ▼] [时间范围 ▼] [刷新] [导出 CSV]    │
├────────────────────────────────────────────────┤
│ 类型 │ 周期起 │ 周期止 │ 粒度 │ 版本 │ 生成时间 │ 操作 │
│ GMV  │ 07-19 │ 07-25 │ 日  │ v3   │ 07-26 02:00│ 查看 │
│ GMV  │ 07-19 │ 07-25 │ 日  │ v2   │ 07-26 01:00│ 查看/对比 │
└────────────────────────────────────────────────┘
→ 抽屉：Metrics 描述列表 + 版本对比差异表
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dashboard/reports` | 查询报表快照列表（按类型和时间范围） | Admin,Operator |
| GET | `/api/admin/dashboard/reports/{id}` | 查询报表快照详情 | Admin,Operator |

- **请求参数**：列表 `reportType`（必填）、`start`、`end`；详情 `id`（Guid）。
- **响应字段**：列表返回 `List<DashboardReportDto>`；详情返回单个 `DashboardReportDto`，含 `ReportId`、`ReportType`、`Granularity`、`GeneratedAt`、`PeriodStart/PeriodEnd`、`Metrics:[{Key,Value,Unit}]`。
- **数据加载策略**：进入页面加载默认类型；切换类型/时间重新请求；详情按需点击加载。
- **缓存策略**：列表缓存 5 分钟，键 `reports:{type}:{start}:{end}`；详情缓存 10 分钟（不可变快照），键 `report:{id}`。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dashboard/reports?reportType=OrderGmv&start=...&end=...` → 表格渲染。
  2. 切换报表类型 → 重新请求 → 表格刷新。
  3. 点击「查看」 → GET `/api/admin/dashboard/reports/{id}` → 抽屉展示 Metrics。
  4. 在抽屉开启「与上一版本对比」 → 取列表中同周期前一版本详情 → 渲染差异表（Key/旧值/新值/变化%）。
- **分支流程**：
  - 同周期仅一个版本：对比开关 disabled，Tooltip「无历史版本可对比」。
  - 报表详情 404：`message.error('快照不存在或已归档')` 3s。
- **跨页面流转**：从子看板（如支付统计）「查看历史快照」链接携带 `reportType=PaymentSuccessRate` 跳本页。
- **状态机可视化**：无状态字段，DataVersion 为版本号递增。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-select>`、`<a-drawer>`、`<a-descriptions>`、`<a-switch>`
- **业务组件**：
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `EmptyState`（见 shared/components.md §5）
  - `DataTable`（见 shared/components.md §6）— 主列表
- **图表组件**：无（详情以描述列表展示）
- **图标使用**：`EyeOutlined`（查看）、`DiffOutlined`（对比）、`DownloadOutlined`（导出）16px。
- **空状态**：「暂无快照记录」+ CTA「调整筛选条件」。

## 6. 视觉规范
- **主色应用**：操作列「查看」链接主色；版本号 `<a-tag color="blue">`；差异表中新增绿色、删除红色。
- **状态色**：差异正值 `#52C41A`、负值 `#FF4D4F`。
- **间距**：表格行高 48px；抽屉内边距 24px；描述列表项间距 16px。
- **字体**：表格 14px；抽屉标题 16px medium；Metrics 值 14px semibold。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>` 行；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：详情 404 `message.error` 3s；网络错误重试按钮。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；导出按钮 `PermissionGuard permission="dashboard:export"`。
- **并发与乐观锁**：只读无锁。
- **危险操作确认**：无危险操作（仅查看与导出）。

## 8. 验收要点
- [ ] 报表类型切换实时刷新列表
- [ ] 详情抽屉展示 Metrics 全量字段
- [ ] 版本对比开关在有历史版本时可启用
- [ ] 导出 CSV 调用 `/api/admin/dashboard/reports` 并下载
- **性能要求**：首屏 < 1.5s；列表分页 20；详情加载 < 800ms。
- **可访问性**：表格支持键盘导航；抽屉聚焦管理（打开后聚焦首个描述项）；对比度 ≥ 4.5:1。
