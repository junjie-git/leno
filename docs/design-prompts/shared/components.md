# 跨端共享业务组件清单

**文档版本**：V1.0
**适用范围**：4 端所有页面提示词
**最后更新**：2026-07-26

本文件定义跨端可复用的业务组件。页面提示词的「组件清单」段引用本文件中定义的组件，不重新定义。

---

## 1. StatusTag 通用状态标签

**用途**：统一展示订单、售后、商品、店铺、支付等状态，根据状态自动匹配颜色与文案。

**Props**：

```typescript
interface StatusTagProps {
  status: string;        // 状态值，如 'PendingPayment'、'Active'
  type?: 'order' | 'afterSales' | 'product' | 'shop' | 'payment';  // 状态类型
  size?: 'small' | 'default';  // 尺寸，默认 default
}
```

**实现要点**：
- 内置状态-颜色映射表（参考 glossary.md 第 6 节）
- 三端后台使用 `<a-tag :color="color">`，用户 APP 使用 `<van-tag :color="color">`
- 状态值未匹配时降级为默认灰色 `#8C8C8C`

**使用示例**：

```vue
<StatusTag status="PendingPayment" type="order" />
<!-- 渲染为橙色标签"待支付" -->
```

**引用场景**：订单列表、售后列表、商品列表、店铺列表、支付记录

---

## 2. IdempotencyButton 幂等提交按钮

**用途**：表单提交按钮，内置防抖与重复点击拦截，保证幂等性。

**Props**：

```typescript
interface IdempotencyButtonProps {
  loading?: boolean;       // 外部控制的 loading 状态
  disabled?: boolean;      // 是否禁用
  type?: 'primary' | 'default' | 'danger';  // 按钮类型
  debounceMs?: number;     // 防抖时间，默认 300ms
  idempotencyKey?: string; // 幂等键，未传则自动生成 UUID
}
```

**Emits**：`@click` — 用户点击且通过防抖与重复拦截后触发

**实现要点**：
- 点击后立即 disabled + loading，直至 loading 变为 false
- 内部维护 `lastClickTime`，300ms 内的重复点击被忽略
- 自动生成 `Idempotency-Key` UUID 注入请求头（由请求拦截器读取）
- 三端后台基于 `<a-button :loading="loading">`，用户 APP 基于 `<van-button :loading="loading">`

**使用示例**：

```vue
<IdempotencyButton type="primary" :loading="submitting" @click="handleSubmit">
  提交
</IdempotencyButton>
```

**引用场景**：所有表单提交按钮、危险操作执行按钮

---

## 3. PermissionGuard 权限守卫

**用途**：按钮级权限控制，根据当前用户角色决定是否渲染子组件。

**Props**：

```typescript
interface PermissionGuardProps {
  permission: string | string[];  // 权限标识，如 'product:audit' 或 ['admin', 'operator']
  fallback?: string;              // 无权限时的降级展示，默认空字符串
}
```

**Slots**：默认 slot — 有权限时渲染的内容

**实现要点**：
- 从 Pinia 的 `useUserStore` 读取当前用户角色与权限列表
- permission 为数组时，任一匹配即通过（OR 逻辑）
- 无权限时渲染 fallback 或空内容

**使用示例**：

```vue
<PermissionGuard permission="product:audit">
  <a-button @click="handleAudit">审核</a-button>
</PermissionGuard>
```

**引用场景**：所有需要角色/权限控制的按钮、链接、操作列

---

## 4. DateTimeRangePicker 日期时间范围选择器

**用途**：统一日期时间范围选择，内置常用预设。

**Props**：

```typescript
interface DateTimeRangePickerProps {
  modelValue: [string, string] | null;  // v-model，ISO 8601 字符串数组
  presets?: ('today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth')[];  // 预设
  showTime?: boolean;  // 是否显示时间，默认 false
}
```

**Emits**：`@update:modelValue` — 值变化时触发

**实现要点**：
- 三端后台基于 `<a-range-picker>`，用户 APP 基于 `<van-date-picker>` + popup
- 预设按钮：今日、昨日、近 7 天、近 30 天、本月
- 输出 ISO 8601 格式字符串数组

**使用示例**：

```vue
<DateTimeRangePicker
  v-model="dateRange"
  :presets="['today', 'last7days', 'last30days']"
  showTime
/>
```

**引用场景**：所有看板、报表、日志列表的时间筛选

---

## 5. EmptyState 空状态

**用途**：统一的空状态展示，含图标、描述与 CTA 按钮。

**Props**：

```typescript
interface EmptyStateProps {
  title?: string;           // 标题，默认"暂无数据"
  description?: string;     // 描述
  ctaText?: string;         // CTA 按钮文案
  icon?: string;            // 自定义图标
}
```

**Emits**：`@cta-click` — CTA 按钮点击

**Slots**：`#icon` — 自定义图标插槽

**实现要点**：
- 三端后台基于 `<a-empty>`，用户 APP 基于 `<van-empty>`
- 图标居中，描述文字 14px `#8C8C8C`
- CTA 按钮主色 `#1677FF`，圆角 `6px`

**使用示例**：

```vue
<EmptyState
  title="暂无商品"
  description="点击下方按钮新增第一个商品"
  ctaText="新增商品"
  @cta-click="goCreate"
/>
```

**引用场景**：所有列表页、搜索结果页、通知中心的空数据展示

---

## 6. DataTable 数据表格

**用途**：统一数据表格，封装分页、排序、虚拟滚动、加载态。

**Props**：

```typescript
interface DataTableProps {
  columns: TableColumn[];       // 列定义
  data: any[];                  // 数据源
  loading?: boolean;            // 加载态
  total?: number;               // 总记录数
  pageSize?: number;            // 每页条数，默认 20
  currentPage?: number;         // 当前页
  rowKey?: string | ((record) => string);  // 行 key
  virtualScroll?: boolean;      // 是否虚拟滚动，>100 行自动启用
}
```

**Emits**：
- `@change` — 分页/排序变化
- `@row-click` — 行点击

**实现要点**：
- 三端后台基于 `<a-table>`，封装分页器与排序逻辑
- 数据量 > 100 行时自动启用虚拟滚动 `:scroll="{ y: 500 }"`
- 列定义支持 `customRender` 自定义渲染（如状态列使用 StatusTag）
- 用户 APP 不使用此组件，改用 `van-list` + 卡片

**使用示例**：

```vue
<DataTable
  :columns="columns"
  :data="data"
  :loading="loading"
  :total="total"
  :current-page="page"
  @change="handlePageChange"
/>
```

**引用场景**：三端后台所有列表页（用户列表、订单列表、商品列表等）

---

## 7. 图表组件（ChartLine / ChartPie / ChartBar / ChartGauge）

**用途**：统一图表封装，基于 @vue-echarts。

### 7.1 ChartLine 折线图

```typescript
interface ChartLineProps {
  data: { date: string; value: number; series?: string }[];  // 数据
  xField?: string;   // x 轴字段，默认 'date'
  yField?: string;   // y 轴字段，默认 'value'
  seriesField?: string;  // 系列字段，用于多系列
  height?: number;   // 高度，默认 300
  smooth?: boolean;  // 是否平滑曲线，默认 true
}
```

### 7.2 ChartPie 饼图

```typescript
interface ChartPieProps {
  data: { name: string; value: number }[];
  height?: number;      // 默认 300
  legendPosition?: 'top' | 'right' | 'bottom';  // 默认 'right'
  donut?: boolean;      // 是否环形，默认 true
}
```

### 7.3 ChartBar 柱状图

```typescript
interface ChartBarProps {
  data: { name: string; value: number; series?: string }[];
  horizontal?: boolean;  // 是否横向，默认 false
  height?: number;       // 默认 300
  seriesField?: string;  // 系列字段
}
```

### 7.4 ChartGauge 仪表盘

```typescript
interface ChartGaugeProps {
  value: number;        // 当前值，0-100
  title?: string;       // 标题
  height?: number;      // 默认 200
  thresholds?: [number, number];  // 阈值，如 [60, 80] 表示 <60 红、60-80 黄、>80 绿
}
```

**实现要点**：
- 所有图表配色使用设计令牌：主色 `#1677FF`、成功 `#52C41A`、警告 `#FAAD14`、危险 `#FF4D4F`
- 响应式：监听容器 resize 自动重绘
- 加载态：数据未加载时显示 `<a-skeleton :active="true" />`
- 仅三端后台使用，用户 APP 不使用图表

**引用场景**：所有看板、报表、统计页

---

## 8. DashboardCard 看板卡片

**用途**：看板页的标准卡片，含标题、数值、趋势与图表插槽。

**Props**：

```typescript
interface DashboardCardProps {
  title: string;           // 卡片标题
  value: number | string;  // 主数值
  unit?: string;           // 单位，如 '元'、'笔'、'%'
  trend?: {                // 趋势
    value: number;         // 变化值
    direction: 'up' | 'down';
  };
  loading?: boolean;
}
```

**Slots**：`#chart` — 图表插槽，放置 ChartLine/ChartPie 等

**实现要点**：
- 卡片圆角 `8px`，内边距 `16px`
- 标题 14px `#8C8C8C`，数值 24px `#000000D9` font-weight 600
- 趋势上升绿色 `#52C41A` + ↑ 箭头，下降红色 `#FF4D4F` + ↓ 箭头
- 三端后台使用 `<a-card>`，用户 APP 使用 `<van-card>`

**使用示例**：

```vue
<DashboardCard
  title="今日 GMV"
  :value="128560"
  unit="元"
  :trend="{ value: 12.5, direction: 'up' }"
>
  <template #chart>
    <ChartLine :data="gmvTrendData" height="200" />
  </template>
</DashboardCard>
```

**引用场景**：所有看板页（系统管理仪表盘、运营数据看板、卖家工作台）

---

## 9. AuditLogViewer 审计日志查看器

**用途**：只读展示审计日志详情，支持 JSON 展开查看。

**Props**：

```typescript
interface AuditLogViewerProps {
  logId: string;  // 日志 ID
}
```

**实现要点**：
- 内部调用 `GET /api/admin/audit-logs/{id}` 获取详情
- 使用 `<a-descriptions>` 展示结构化字段（操作人、操作时间、操作类型、IP、User-Agent）
- 操作前后数据使用 `<pre>` + JSON 高亮展示，支持折叠/展开
- 只读，无编辑功能

**引用场景**：系统管理后台审计日志详情页

---

## 10. ConfirmDialog 危险操作确认对话框

**用途**：封装危险操作的二次确认，统一交互与视觉。

**Props**：

```typescript
interface ConfirmDialogProps {
  title: string;        // 标题，如"确认删除"
  content: string;      // 内容，说明后果与是否可逆
  danger?: boolean;     // 是否危险操作，默认 true
  okText?: string;      // 确认按钮文案，默认"确认"
  cancelText?: string;  // 取消按钮文案，默认"取消"
}
```

**Emits**：`@confirm` — 用户确认时触发

**实现要点**：
- 三端后台基于 `Modal.confirm`，danger 为 true 时 `okType: 'danger'`（红色按钮）
- 用户 APP 基于 `showConfirmDialog`，danger 为 true 时 `confirmButtonColor: '#FF4D4F'`
- 内容必须说明后果与是否可逆（由调用方传入 content）
- 不直接执行操作，仅触发 `@confirm` 事件，由父组件处理

**使用示例**：

```vue
<ConfirmDialog
  v-model:visible="confirmVisible"
  title="确认删除"
  content="删除后将无法恢复，关联的 3 条数据将一并删除。"
  :danger="true"
  okText="确认删除"
  @confirm="handleDelete"
/>
```

**引用场景**：所有危险操作（删除、暂停、关闭、驳回、强制取消、丢弃、重投、封禁、下架）

---

## 引用规范

页面提示词的「组件清单」段引用本文件中的组件时，格式如下：

```markdown
## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-button>`、`<a-form>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 订单状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交按钮
  - `PermissionGuard`（见 shared/components.md §3）— 审核按钮权限控制
- **图表组件**：`ChartLine`（见 shared/components.md §7.1）— 销售趋势
```

引用时标注 `§章节号`，便于快速定位。
