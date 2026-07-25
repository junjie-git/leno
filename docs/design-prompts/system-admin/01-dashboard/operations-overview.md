# 运营总览 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：01-dashboard 仪表盘
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：在一个页面查看订单量、GMV、转化率等核心运营指标的当前值与趋势，快速发现异常并下钻到对应子看板。
- **访问入口**：登录后默认首页 / Sider「仪表盘 → 运营总览」/ 全局搜索「运营总览」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部时间范围筛选条 + 4 个 KPI 卡片网格 + 主趋势图 + 双列辅助图。
- **关键区域**：
  - 区域 A（筛选条）：`DateTimeRangePicker`（见 shared/components.md §4），预设今日/昨日/近 7 天/近 30 天/本月，默认近 7 天。
  - 区域 B（KPI 网格）：4 个 `DashboardCard`（见 shared/components.md §8）— 订单量、GMV、转化率、客单价，含同比趋势 ↑↓。
  - 区域 C（主趋势图）：`ChartLine`（见 shared/components.md §7.1）— GMV 与订单量双系列按日趋势，高度 320px。
  - 区域 D（辅助图区）：左 `ChartPie` 订单来源分布，右 `ChartBar` 转化漏斗（浏览→加购→下单→支付）。
- **响应式断点**：≥1200px 4 列 KPI；992-1199px 2 列 KPI；<992px 不支持。
- **首屏内容**：默认近 7 天的 4 个 KPI 数值 + GMV 趋势曲线骨架。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [时间范围 ▼] [刷新] [导出 CSV]                  │
├────────┬────────┬────────┬─────────────────────┤
│ 订单量  │  GMV   │ 转化率  │      客单价         │
│ 12,560 │ ¥128万 │  8.5%  │     ¥102            │
│ ↑12.5% │ ↑9.8%  │ ↓0.3%  │     ↑2.1%           │
├────────┴────────┴────────┴─────────────────────┤
│           GMV 与订单量趋势（双系列折线）          │
├────────────────────┬───────────────────────────┤
│ 订单来源分布（饼图） │   转化漏斗（柱状图）       │
└────────────────────┴───────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dashboard/overview` | 查询运营总览（订单量/GMV/转化率） | Admin,Operator |

- **请求参数**：`start`、`end`（ISO 8601，默认 now-7d 至 now）；返回 `DashboardReportDto`。
- **响应字段**：`ReportId`、`ReportType=OrderGmv`、`PeriodStart/PeriodEnd`、`Granularity`、`GeneratedAt`、`Metrics: [{ Key, Value, Unit }]`。`Metrics` 中 `Key` 含 `orderCount`/`gmv`/`conversionRate`/`avgOrderAmount`/`dailyTrend`/`sourceDistribution`/`funnel`，分别用于 KPI 卡片、趋势线、饼图、漏斗。
- **数据加载策略**：进入页面立即并行加载（带 `start/end` query）；切换时间范围重新请求；趋势与分布可独立刷新。
- **缓存策略**：Pinia `useDashboardStore` 缓存 5 分钟，键 `overview:{start}:{end}`，过期或切换范围失效。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → store 检查缓存 → 命中直接渲染，未命中发起 GET `/api/admin/dashboard/overview?start=...&end=...` → KPI/图表骨架替换为数据。
  2. 切换时间范围预设 → 重新请求 → 4 个 KPI 卡片与 3 张图表同步刷新。
  3. 点击 KPI 卡片标题 → 路由跳转对应子看板（订单量→售后统计、GMV→支付统计、转化率→店铺排行）。
- **分支流程**：
  - 时间范围 start ≥ end：前端拦截提示「结束时间需晚于开始时间」，不发请求。
  - 后端返回空 Metrics：KPI 显示 `--`，图表显示 EmptyState（见 shared/components.md §5）。
- **跨页面流转**：跳转 `/dashboard/payment-stats`、`/dashboard/after-sales-stats`、`/dashboard/shop-ranking` 时携带当前 `start/end` 作为 query。
- **状态机可视化**：无状态字段，纯只读快照。

## 5. 组件清单
- **基础组件**：`<a-card>`、`<a-row>`/`<a-col>`、`<a-spin>`、`<a-segmented>`（时间预设切换）
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）— 4 个 KPI 卡片
  - `DateTimeRangePicker`（见 shared/components.md §4）— 时间筛选
  - `EmptyState`（见 shared/components.md §5）— 无数据兜底
- **图表组件**：`ChartLine`（见 shared/components.md §7.1）、`ChartPie`（见 shared/components.md §7.2）、`ChartBar`（见 shared/components.md §7.3）
- **图标使用**：`@ant-design/icons-vue` 的 `ArrowUpOutlined`（绿）/`ArrowDownOutlined`（红）渲染趋势。
- **空状态**：「暂无运营数据，请稍后重试」+ 「刷新」按钮。

## 6. 视觉规范
- **主色应用**：KPI 数值 24px `#000000D9` semibold；趋势上升 `#52C41A`，下降 `#FF4D4F`；折线主系列 `#1677FF`。
- **状态色**：成功 `#52C41A` 用于 ↑；危险 `#FF4D4F` 用于 ↓；警告 `#FAAD14` 用于转化率低于阈值。
- **间距**：KPI 卡片间距 `spacing/6=24px`；卡片内边距 `spacing/4=16px`；区块间距 `spacing/8=32px`。
- **字体**：标题 16px medium；KPI 数值 24px semibold；辅助 12px `#8C8C8C`。
- **图标尺寸**：趋势箭头 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `<a-skeleton :active="true" />` 模拟 4 卡片 + 图表布局；切换范围时仅图表区显示 `<a-spin>`。
- **空数据**：`EmptyState` 居中展示，CTA「刷新」按钮重新拉取。
- **错误态**：网络错误 `message.error('运营总览加载失败')` 显示 3s；权限不足 403 跳 `/403`。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；Admin 可见全部指标，Operator 仅可见订单量与 GMV（通过 `PermissionGuard` 见 shared/components.md §3 控制转化率卡片）。
- **并发与乐观锁**：只读页面，无乐观锁。
- **危险操作确认**：无危险操作。

## 8. 验收要点
- [ ] 4 个 KPI 卡片首屏可见且数值格式化（GMV 万单位、百分比保留 1 位小数）
- [ ] 切换时间范围 300ms 内开始请求，加载中显示 Spin
- [ ] 趋势线双系列图例可点击切换显隐
- [ ] KPI 卡片点击可跳转对应子看板并携带时间范围
- **性能要求**：首屏加载 < 1.5s；图表渲染 < 500ms；数据量 > 100 点时折线启用 `large` 模式。
- **可访问性**：所有图表附 `aria-label` 描述；颜色对比度 ≥ 4.5:1；键盘可遍历 KPI 卡片。
