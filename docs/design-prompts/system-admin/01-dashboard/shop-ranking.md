# 店铺排行 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：01-dashboard 仪表盘
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：按销售额或订单量维度查看店铺排行 TopN，识别头部与异常店铺。
- **访问入口**：Sider「仪表盘 → 店铺排行」/ 运营总览跳转
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选（时间范围 + 维度切换 + TopN）+ Top 3 领奖台 + 主排行表。
- **关键区域**：
  - 区域 A（筛选条）：`DateTimeRangePicker` + 维度 `<a-segmented>`（销售额/订单量/客单价）+ TopN `<a-input-number min="5" max="50">`。
  - 区域 B（领奖台）：Top 3 店铺以卡片形式展示，第 1 名居中放大，含店铺 Logo、店名、指标值、增长率。
  - 区域 C（主排行表）：`<a-table>` 列含排名/店铺名/所在类目/指标值/环比增长率/状态，分页 20。
- **响应式断点**：≥1200px 领奖台 3 列；992-1199px 领奖台居中单列堆叠。
- **首屏内容**：Top 10 默认 + 销售额维度。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [时间范围 ▼] [销售额|订单量|客单价] [TopN 10]   │
├────────────────────────────────────────────────┤
│       🥇 第1名店铺      │                      │
│   🥈 第2名    🥉 第3名  │                      │
├────────────────────────────────────────────────┤
│ #  │ 店铺名 │ 类目 │ 销售额 │ 环比 │ 状态       │
│ 1  │ ...   │ ...  │ ¥120万│ ↑12%│ 营业中      │
└────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dashboard/shop-ranking` | 查询店铺排行 TopN | Admin,Operator |

- **请求参数**：`start`、`end`；返回 `DashboardReportDto`，`ReportType=ShopRanking`。
- **响应字段**：`Metrics` 中 `Key` 含 `items:[{shopId,shopName,category,salesAmount,orderCount,avgOrderAmount,growthRate,status}]`，`dimension` 标识当前维度。
- **数据加载策略**：进入页面加载默认 Top 10；切换维度或 TopN 时前端重新排序与截取（数据已全量返回）。
- **缓存策略**：缓存 10 分钟，键 `shop-ranking:{start}:{end}`。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dashboard/shop-ranking?start=...&end=...` → 领奖台 + 表格同步。
  2. 切换维度（销售额/订单量/客单价）→ 前端按新维度排序，领奖台与表格同步刷新。
  3. 调整 TopN → 前端截取前 N 项重新渲染。
  4. 点击表格店铺名 → 跳 `/audit/audit-logs?resourceType=Shop&keyword={shopId}`。
- **分支流程**：
  - 店铺数 < 3：领奖台仅显示已有店铺，空位显示 `<a-empty image="simple" />`。
  - 增长率为负：表格该列显示红色 ↓ 与绝对值。
- **跨页面流转**：跳转审计或店铺治理（运营后台）。
- **状态机可视化**：店铺状态使用 `StatusTag`（见 shared/components.md §1，type='shop'）。

## 5. 组件清单
- **基础组件**：`<a-card>`、`<a-table>`、`<a-segmented>`、`<a-input-number>`
- **业务组件**：
  - `DashboardCard`（见 shared/components.md §8）— 领奖台卡片
  - `DateTimeRangePicker`（见 shared/components.md §4）
  - `StatusTag`（见 shared/components.md §1）— 店铺状态
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无（领奖台为自定义卡片）
- **图标使用**：奖牌图标（TrophyOutlined，金银铜色 32px）。
- **空状态**：「所选时间范围暂无店铺排行数据」。

## 6. 视觉规范
- **主色应用**：第 1 名卡片边框 `#FAAD14`（金色）；表格第 1 行背景 `#FAFAFA`。
- **状态色**：店铺状态色见 glossary.md §6.4；增长率 ↑ 绿 ↓ 红。
- **间距**：领奖台卡片间距 24px；表格行高 48px。
- **字体**：第 1 名店铺名 20px semibold；表格 14px；指标值 16px semibold。
- **图标尺寸**：奖牌 32px；店铺 Logo 40px 圆形。

## 7. 异常处理与边界
- **加载态**：领奖台 3 个 `<a-skeleton :active="true" />` 卡片；表格骨架行。
- **空数据**：`EmptyState` 兜底。
- **错误态**：网络错误 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`。
- **并发与乐观锁**：只读无锁。
- **危险操作确认**：无危险操作。

## 8. 验收要点
- [ ] 维度切换实时排序，不发新请求
- [ ] Top 3 领奖台布局正确，第 1 名居中放大
- [ ] 店铺状态使用 StatusTag 正确染色
- [ ] TopN 输入限制 5-50
- **性能要求**：首屏 < 1.5s；表格行 < 50 无需虚拟滚动。
- **可访问性**：领奖台卡片支持键盘聚焦；表格行可键盘选中；对比度 ≥ 4.5:1。
