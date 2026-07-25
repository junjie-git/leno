# 支付统计 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：01-dashboard 仪表盘
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：查看支付成功率、分渠道支付量与失败原因分布，定位支付链路异常渠道。
- **访问入口**：Sider「仪表盘 → 支付统计」/ 运营总览 KPI 跳转 / 全局搜索
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部时间筛选 + 概览 KPI 行 + 渠道成功率排行 + 失败原因分布。
- **关键区域**：
  - 区域 A（筛选条）：`DateTimeRangePicker` + 渠道多选 `<a-select mode="multiple">`（支付宝/微信/银联/Apple Pay）。
  - 区域 B（KPI 行）：3 个 `DashboardCard` — 总支付笔数、整体成功率、平均到账时长。
  - 区域 C（渠道成功率）：`ChartBar` 横向排行，每条渠道显示成功率百分比与笔数，颜色按阈值（<80% 红、80-95% 黄、>95% 绿）。
  - 区域 D（失败原因分布）：`ChartPie` 环形图，按错误码聚合（如 timeout、insufficient_balance、channel_error）。
- **响应式断点**：≥1200px C/D 双列；992-1199px 单列堆叠。
- **首屏内容**：近 7 天整体成功率 Gauge + 渠道排行 Top 5。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [时间范围 ▼] [渠道多选 ▼] [刷新]                │
├──────────┬──────────┬──────────────────────────┤
│ 总笔数    │ 成功率    │     平均到账时长          │
│ 8,420    │ 96.2%    │     1.8s                 │
├──────────┴──────────┴──────────────────────────┤
│ 渠道成功率排行（横向柱状，按成功率降序）          │
├────────────────────────────────────────────────┤
│           失败原因分布（环形饼图）                │
└────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dashboard/payment-stats` | 查询支付成功率统计（按渠道） | Admin,Operator |

- **请求参数**：`start`、`end`（ISO 8601）；返回 `DashboardReportDto`，`ReportType=PaymentSuccessRate`。
- **响应字段**：`Metrics` 中 `Key` 含 `totalCount`/`successRate`/`avgLatencyMs`/`channelStats:[{channel,successRate,count}]`/`failureReasons:[{reason,count}]`。`channelStats` 用于横向 Bar，`failureReasons` 用于 Pie。
- **数据加载策略**：进入页面立即加载；切换渠道多选时前端过滤 `channelStats`，不发新请求（避免后端不支持渠道过滤时无数据）。
- **缓存策略**：缓存 5 分钟，键 `payment-stats:{start}:{end}`。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dashboard/payment-stats?start=...&end=...` → 3 KPI + Bar + Pie 同步渲染。
  2. 渠道多选筛选 → 前端过滤 Bar 数据，KPI 重新汇总可见渠道。
  3. 点击 Bar 单条渠道 → 弹出 `<a-drawer>` 显示该渠道近 7 天成功率趋势小图。
- **分支流程**：
  - 整体成功率 < 95%：KPI 数值显示警告色 `#FAAD14`，并触发 `notification.warning`「支付成功率低于阈值，请检查」。
  - 某渠道无数据：Bar 该项显示灰色 0%，悬浮 Tooltip 提示「该渠道在所选时间范围内无支付记录」。
- **跨页面流转**：点击渠道名链接跳 `/audit/audit-logs?resourceType=Payment&keyword={channel}` 查看相关审计。
- **状态机可视化**：无状态字段。

## 5. 组件清单
- **基础组件**：`<a-card>`、`<a-select mode="multiple">`、`<a-drawer>`、`<a-tooltip>`
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `ChartGauge`（见 shared/components.md §7.4）— 整体成功率仪表盘，阈值 [80, 95]
- **图表组件**：`ChartBar`（横向）、`ChartPie`（环形）
- **图标使用**：渠道图标由业务图标库提供（Alipay/WeChat 等品牌色 16px）。
- **空状态**：「所选时间范围内暂无支付数据」+ CTA「调整时间范围」。

## 6. 视觉规范
- **主色应用**：Bar 默认 `#1677FF`；阈值染色覆盖（绿/黄/红）；KPI 数值 24px semibold。
- **状态色**：成功 `#52C41A`（>95%）、警告 `#FAAD14`（80-95%）、危险 `#FF4D4F`（<80%）。
- **间距**：KPI 间距 24px；图表区块间距 32px；Bar 行高 32px。
- **字体**：渠道名 14px；数值 16px semibold；错误码 12px `#8C8C8C`。
- **图标尺寸**：渠道图标 16px。

## 7. 异常处理与边界
- **加载态**：KPI 骨架屏；图表区 `<a-skeleton :active="true" />`。
- **空数据**：`EmptyState` 提示调整时间范围。
- **错误态**：网络错误 `message.error` 3s；保留上次成功数据不闪烁清空。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；Operator 不显示 `avgLatencyMs`（敏感运维指标）。
- **并发与乐观锁**：只读无锁。
- **危险操作确认**：无危险操作。

## 8. 验收要点
- [ ] 渠道成功率按阈值染色正确
- [ ] 渠道多选筛选实时生效，不发请求
- [ ] 整体成功率 < 95% 时触发警告通知
- [ ] 点击渠道弹出 Drawer 显示趋势小图
- **性能要求**：首屏 < 1.5s；Pie 数据点 < 20；Bar 项 < 10。
- **可访问性**：图表 `aria-label` 含「支付成功率统计，整体 X%」；色盲友好（除颜色外加图案区分）。
