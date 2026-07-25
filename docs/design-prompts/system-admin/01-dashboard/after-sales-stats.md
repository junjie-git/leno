# 售后统计 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：01-dashboard 仪表盘
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：查看售后量、退款金额与售后类型分布，监控售后率异常与商家处理时效。
- **访问入口**：Sider「仪表盘 → 售后统计」/ 运营总览跳转
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部时间筛选 + 3 KPI + 售后类型分布饼图 + 趋势折线 + Top 10 高售后店铺表。
- **关键区域**：
  - 区域 A（筛选条）：`DateTimeRangePicker` + 售后类型多选（仅退款/退货退款/换货）。
  - 区域 B（KPI 行）：3 个 `DashboardCard` — 售后单量、退款金额、售后率（售后单/订单量）。
  - 区域 C（类型分布）：`ChartPie` 售后类型占比，环形。
  - 区域 D（趋势）：`ChartLine` 售后单量与退款金额双轴折线，高度 280px。
  - 区域 E（高售后店铺）：`<a-table>` 列含店铺名/售后单量/订单量/售后率/平均处理时长，按售后率倒序 Top 10。
- **响应式断点**：≥1200px C/D 双列、E 全宽；992-1199px 单列堆叠。
- **首屏内容**：3 KPI + 类型饼图 + Top 10 表。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [时间范围 ▼] [售后类型多选 ▼] [刷新]            │
├──────────┬──────────┬──────────────────────────┤
│ 售后单量  │ 退款金额  │     售后率               │
│  320     │ ¥48,200  │     2.5%                 │
│ ↑0.3%    │ ↑1.2%    │     ↓0.1%                │
├──────────┴──────────┴──────────────────────────┤
│ 售后类型分布 │ 售后单量与退款金额双轴趋势        │
├────────────────────────────────────────────────┤
│ Top 10 高售后店铺（店铺/售后量/订单量/售后率）   │
└────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dashboard/after-sales-stats` | 查询售后统计（售后量/退款金额） | Admin,Operator |

- **请求参数**：`start`、`end`；返回 `DashboardReportDto`，`ReportType=AfterSalesVolume`。
- **响应字段**：`Metrics` 中 `Key` 含 `afterSalesCount`/`refundAmount`/`afterSalesRate`/`typeDistribution:[{type,count}]`/`dailyTrend:[{date,count,refundAmount}]`/`topShopsByAfterSales:[{shopId,shopName,afterSalesCount,orderCount,avgProcessHours}]`。
- **数据加载策略**：进入页面立即加载；类型多选仅前端过滤饼图与趋势。
- **缓存策略**：缓存 5 分钟，键 `after-sales-stats:{start}:{end}`。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dashboard/after-sales-stats?start=...&end=...` → KPI + 饼图 + 趋势 + 表格同步。
  2. 类型多选筛选 → 前端过滤饼图与趋势。
  3. 点击 Top 10 表店铺名 → 跳 `/audit/audit-logs?resourceType=AfterSales&keyword={shopId}`。
- **分支流程**：
  - 售后率 > 5%：KPI 染红，触发 `notification.warning`「售后率异常，请关注」。
  - Top 10 为空：表格 `EmptyState` 提示「所选时间范围暂无售后数据」。
- **跨页面流转**：跳转审计日志查看相关售后操作。
- **状态机可视化**：无状态字段（状态机详见售后域术语表）。

## 5. 组件清单
- **基础组件**：`<a-card>`、`<a-table>`、`<a-select mode="multiple">`
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `StatusTag`（见 shared/components.md §1）— 售后类型标签
- **图表组件**：`ChartPie`（见 shared/components.md §7.2）、`ChartLine`（见 shared/components.md §7.1，双轴）
- **图标使用**：`ArrowUpOutlined`/`ArrowDownOutlined`。
- **空状态**：「暂无售后数据」+ 刷新按钮。

## 6. 视觉规范
- **主色应用**：折线主系列 `#1677FF`（售后单量）、副系列 `#FAAD14`（退款金额，右轴）。
- **状态色**：售后率 > 5% 红、3-5% 黄、< 3% 绿。
- **间距**：KPI 间距 24px；图表 32px；表格行高 48px。
- **字体**：KPI 24px semibold；店铺名 14px medium；数值 14px。
- **图标尺寸**：趋势箭头 16px。

## 7. 异常处理与边界
- **加载态**：KPI 骨架；表格 `<a-skeleton>` 行。
- **空数据**：`EmptyState` 兜底。
- **错误态**：网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`。
- **并发与乐观锁**：只读无锁。
- **危险操作确认**：无危险操作。

## 8. 验收要点
- [ ] 双轴折线左轴单量、右轴金额
- [ ] 售后率 > 5% 触发 warning 通知
- [ ] Top 10 表按售后率倒序，支持点击店铺名跳转审计
- [ ] 类型多选实时过滤饼图
- **性能要求**：首屏 < 1.5s；表格行 < 10 无需虚拟滚动。
- **可访问性**：图表 `aria-label`；表格支持键盘导航；对比度 ≥ 4.5:1。
