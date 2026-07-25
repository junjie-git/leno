# 通知送达率 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：01-dashboard 仪表盘
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：监控邮件/短信/站内信/推送四类渠道的送达率与失败原因，定位通知链路异常。
- **访问入口**：Sider「仪表盘 → 通知送达率」/ 运营总览跳转
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部时间筛选 + 4 渠道送达率 Gauge 网格 + 失败原因分布表 + 趋势折线。
- **关键区域**：
  - 区域 A（筛选条）：`DateTimeRangePicker` + 渠道多选（邮件/短信/站内信/推送）。
  - 区域 B（Gauge 网格）：4 个 `ChartGauge`（见 shared/components.md §7.4），阈值 [90, 95]，分别展示 4 渠道送达率。
  - 区域 C（失败原因表）：`<a-table>` 列含渠道/失败原因/失败数/占比/最近发生时间，按失败数倒序，分页 20。
  - 区域 D（趋势折线）：`ChartLine` 多系列按渠道按日送达率，高度 280px。
- **响应式断点**：≥1200px Gauge 4 列；992-1199px 2 列。
- **首屏内容**：4 渠道 Gauge + 失败原因 Top 10。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [时间范围 ▼] [渠道多选 ▼] [刷新]                │
├──────────┬──────────┬──────────┬──────────────┤
│ 邮件 Gauge│ 短信 Gauge│站内信Gauge│ 推送 Gauge   │
│  98.2%   │  95.5%   │  99.8%   │  88.1%       │
├──────────┴──────────┴──────────┴──────────────┤
│ 渠道送达率趋势（多系列折线）                     │
├────────────────────────────────────────────────┤
│ 失败原因分布表（渠道/原因/数/占比/最近时间）     │
└────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dashboard/notification-delivery` | 查询通知送达率统计 | Admin,Operator |

- **请求参数**：`start`、`end`；返回 `DashboardReportDto`，`ReportType=NotificationDelivery`。
- **响应字段**：`Metrics` 中 `Key` 含 `channelStats:[{channel,deliveryRate,totalCount,failedCount}]`/`failureReasons:[{channel,reason,count,lastOccurredAt}]`/`dailyTrend:[{date,channel,rate}]`。
- **数据加载策略**：进入页面立即加载全部；渠道多选仅前端过滤表格与趋势。
- **缓存策略**：缓存 5 分钟，键 `notification-delivery:{start}:{end}`。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dashboard/notification-delivery?start=...&end=...` → 4 Gauge + 表格 + 趋势同步。
  2. 渠道多选筛选 → 前端过滤表格与趋势曲线。
  3. 点击失败原因表某行 → `<a-drawer>` 显示该原因近 7 天分布与最近 10 条失败记录。
- **分支流程**：
  - 某 Gauge < 90%：仪表盘染红，KPI 角标显示 `<a-badge status="error" />`，触发 `notification.error` 告警。
  - 失败数为 0：表格显示 `<a-empty description="无失败记录" />`。
- **跨页面流转**：点击「最近时间」列跳 `/audit/audit-logs?resourceType=Notification&keyword={reason}`。
- **状态机可视化**：无状态字段。

## 5. 组件清单
- **基础组件**：`<a-card>`、`<a-table>`、`<a-select mode="multiple">`、`<a-drawer>`、`<a-badge>`
- **业务组件**：
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：`ChartGauge`（见 shared/components.md §7.4）、`ChartLine`（见 shared/components.md §7.1）
- **图标使用**：渠道图标（MailOutlined/MessageOutlined/BellOutlined/NotificationOutlined）16px。
- **空状态**：「所选渠道暂无失败记录」。

## 6. 视觉规范
- **主色应用**：Gauge 主色 `#1677FF`；趋势线 4 渠道分色（蓝/绿/橙/紫）。
- **状态色**：>95% 绿、90-95% 黄、<90% 红。
- **间距**：Gauge 卡片间距 24px；表格行高 48px；区块间距 32px。
- **字体**：Gauge 数值 24px semibold；表格 14px；辅助 12px。
- **图标尺寸**：渠道图标 16px。

## 7. 异常处理与边界
- **加载态**：Gauge 显示 `<a-spin>`；表格使用骨架行。
- **空数据**：表格 `EmptyState` 兜底。
- **错误态**：网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`。
- **并发与乐观锁**：只读无锁。
- **危险操作确认**：无危险操作。

## 8. 验收要点
- [ ] 4 个 Gauge 阈值染色正确（>95 绿 / 90-95 黄 / <90 红）
- [ ] 渠道多选实时过滤表格与趋势
- [ ] Gauge < 90% 触发 error 告警
- [ ] 失败原因表行点击展开 Drawer
- **性能要求**：首屏 < 1.5s；表格虚拟滚动阈值 100 行；Gauge 渲染 < 300ms。
- **可访问性**：Gauge 含 `aria-label`「{渠道} 送达率 {X}%」；表格支持键盘行选中。
