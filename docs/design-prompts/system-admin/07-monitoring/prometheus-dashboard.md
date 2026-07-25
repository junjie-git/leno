# Prometheus 监控大盘 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：07-monitoring 系统监控
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：整合 Prometheus 指标，按限界上下文分组展示 QPS、延迟（P50/P95/P99）、错误率、MQ 队列长度、Redis 命中率、JVM/CLR 内存与 GC，辅助故障定位与容量规划。
- **访问入口**：Sider「系统监控 → Prometheus 监控大盘」/ 健康监控页跳转 / 告警管理页「关联指标」跳转
- **实现状态**：➕ 补充功能

## 2. 页面布局与信息架构
- **整体布局**：顶部模块切换 + 全局指标卡片区 + 趋势图区 + 资源指标区 + 链路追踪入口。
- **关键区域**：
  - 区域 A（模块切换）：`<a-select>` 选择限界上下文（全部/Order/Product/Payment/Notification/SystemAdmin 等），切换后所有图表按模块过滤。
  - 区域 B（全局指标卡片区）：6 个 `DashboardCard` — 总 QPS / 平均延迟 P95 / 错误率 / MQ 队列积压 / Redis 命中率 / 活跃实例数，每卡含趋势 mini 折线图。
  - 区域 C（趋势图区）：2×2 网格，4 个 `ChartLine` — QPS 趋势（近 1h，按模块分系列）/ 延迟分位数趋势（P50/P95/P99 三线）/ 错误率趋势 / MQ 队列长度趋势。
  - 区域 D（资源指标区）：2 个 `ChartLine` + 1 个 `ChartGauge` — CLR 堆内存趋势 / GC Gen2 收集频率趋势 / Redis 命中率仪表盘。
  - 区域 E（链路追踪入口）：`<a-button>` 跳转外部 Grafana / Jaeger（携带当前模块与时间范围参数）。
- **响应式断点**：≥1200px 卡片 6 列 + 图表 2×2；992-1199px 卡片 3 列 + 图表 1 列。
- **首屏内容**：全部模块的全局指标卡片 + QPS 趋势图。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [模块: 全部 ▼]                  [Grafana] [Jaeger]│
├────────────────────────────────────────────────┤
│ QPS 1.2k │ P95 85ms │ 错误率 0.3% │ MQ 156 │ Redis 98% │ 实例 8 │
├──────────────────────┬─────────────────────────┤
│ [QPS 趋势折线图]      │ [延迟 P50/P95/P99 折线]  │
├──────────────────────┼─────────────────────────┤
│ [错误率趋势折线图]    │ [MQ 队列长度趋势折线]    │
├──────────────────────┼─────────────────────────┤
│ [CLR 堆内存趋势]      │ [GC Gen2 频率趋势]       │
├──────────────────────┴─────────────────────────┤
│           [Redis 命中率仪表盘]                   │
└────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**（补充功能，待 SystemAdmin BC 实现 Prometheus 查询代理端点）：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/monitoring/metrics/summary` | 获取全局指标汇总（QPS/P95/错误率/MQ/Redis/实例数，待实现） | Admin |
| GET | `/api/admin/monitoring/metrics/query` | 执行 PromQL 查询返回时序数据（待实现） | Admin |
| GET | `/api/admin/monitoring/metrics/trend` | 获取指定指标趋势（按模块与时间范围，待实现） | Admin |
| GET | `/api/admin/monitoring/instances` | 获取各模块活跃实例列表（待实现） | Admin |

- **请求参数**：汇总 `module?`（默认全部）；查询 `query`（PromQL）+ `start` + `end` + `step`（秒）；趋势 `metric`（qps/latency_p95/error_rate/mq_queue_length/redis_hit_rate）+ `module?` + `range`（默认 1h）；实例 `module?`。
- **响应字段**：`MetricsSummaryDto` 含 `totalQps`、`avgP95LatencyMs`、`errorRate`、`mqBacklog`、`redisHitRate`、`activeInstanceCount`，每项含 `trend`（mini 折线数据）；`MetricsQueryResultDto` 含 `metric`、`dataPoints`（timestamp+value 数组）+ `module`；`InstanceDto` 含 `instanceId`、`module`、`address`、`status`、`cpu`、`memory`。
- **数据加载策略**：进入页面并行加载汇总 + 4 个趋势 + 3 个资源指标；模块切换重新加载所有图表；趋势数据每 30s 轮询刷新。
- **缓存策略**：不缓存（指标实时变化）；趋势数据按时间范围请求。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 默认模块「全部」 → 并行 GET summary + 6 个 trend → 卡片 + 图表渲染。
  2. 趋势数据每 30s 轮询刷新。
  3. 切换模块下拉 → 所有卡片与图表按模块过滤重新请求。
  4. 点击卡片 mini 折线图 → 趋势图区对应大图自动滚动定位 + 高亮。
  5. 点击「Grafana」 → 新窗口打开 Grafana 看板（携带 module + timeRange 参数）。
  6. 点击「Jaeger」 → 新窗口打开 Jaeger 链路追踪（携带 module + timeRange 参数）。
- **分支流程**：
  - 指标超阈值（错误率 > 1% / P95 > 500ms / MQ 积压 > 1000）：对应卡片数值标红 + `notification.warning` 通知。
  - Prometheus 不可达：`<a-alert type="error">` 全局提示 + 重试按钮。
  - 模块无数据：该模块图表 `EmptyState`。
- **跨页面流转**：从健康监控页跳转携带 `module={moduleName}`；从告警管理页「关联指标」跳转携带 `metric={relatedMetric}`；点击「查看告警」跳 `/runtime-ops/alert-management?module={module}`。
- **状态机可视化**：无状态机；实例状态 `StatusTag` — Up 绿、Down 红。

## 5. 组件清单
- **基础组件**：`<a-select>`、`<a-row>`、`<a-col>`、`<a-card>`、`<a-button>`、`<a-alert>`、`<a-tooltip>`
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）— 6 个全局指标卡片
  - `ChartLine`（见 shared/components.md §7.1）— 6 个趋势图
  - `ChartGauge`（见 shared/components.md §7.4）— Redis 命中率仪表盘
  - `StatusTag`（见 shared/components.md §1）— 实例状态
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：`ChartLine`（QPS/延迟/错误率/MQ/CLR/GC）、`ChartGauge`（Redis 命中率）
- **图标使用**：`DashboardOutlined`（大盘）、`ThunderboltOutlined`（QPS）、`ClockCircleOutlined`（延迟）、`CloseCircleOutlined`（错误率）、`InboxOutlined`（MQ）、`DatabaseOutlined`（Redis）、`ClusterOutlined`（实例）、`LinkOutlined`（外链）16px。
- **空状态**：「暂无监控数据，Prometheus 可能未配置或不可达」+ CTA「重试」。

## 6. 视觉规范
- **主色应用**：卡片数值 24px semibold；趋势图主线主色 `#1677FF`；模块切换主色。
- **状态色**：正常 `#52C41A`、警告 `#FAAD14`、危险 `#FF4D4F`；延迟 P50 绿、P95 黄、P99 红；错误率红；MQ 积压黄。
- **间距**：卡片间距 16px；图表区 16px；图表高度 300px；卡片内边距 16px。
- **字体**：卡片标题 14px `#8C8C8C`；数值 24px semibold `#000000D9`；图表轴 12px；指标名 14px medium。
- **图标尺寸**：卡片图标 20px；操作图标 16px。

## 7. 异常处理与边界
- **加载态**：卡片 `<a-skeleton :active="true" />`；图表 `<a-skeleton :active="true" />`。
- **空数据**：图表 `EmptyState` 兜底，CTA「重试」。
- **错误态**：Prometheus 不可达 `<a-alert type="error" message="Prometheus 不可达，请检查服务状态" show-retry />`；单指标查询失败该图表 `<a-alert type="warning" />` 不影响其他图表；网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin']`。
- **并发与乐观锁**：无写操作；轮询请求 30s 间隔，切换模块取消上一个请求（axios cancelToken）。
- **危险操作确认**：无危险操作（只读看板）。外链跳转 Grafana/Jaeger 直接新窗口打开。

## 8. 验收要点
- [ ] 6 个全局指标卡片含 mini 趋势图
- [ ] 模块切换所有图表正确过滤
- [ ] 指标超阈值时卡片标红 + 通知
- [ ] 趋势数据每 30s 轮询刷新
- [ ] Grafana/Jaeger 外链携带模块与时间参数
- [ ] Prometheus 不可达时全屏错误提示与重试
- **性能要求**：首屏 < 2s（多指标并行请求）；轮询不阻塞 UI；图表渲染 < 500ms；单次趋势查询 < 1s。
- **可访问性**：图表有 aria-label 描述指标含义；卡片数值有 aria-live；模块切换有键盘导航；颜色非唯一区分（附文字标签）。
