<script setup lang="ts">
import { ref, computed, onMounted, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Button,
  Space,
  Select,
  Skeleton,
  Tag,
  Spin,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'
import {
  ArrowLeftOutlined,
  ReloadOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
  LineChartOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { productApi } from '../api/product.api'
import type {
  ProductDetailDto,
  ProductSkuDto,
  PriceChangeRecordDto,
} from '../types/product.dto'
import { StatusTag, EmptyState } from '@/shared/components'
import { formatMoney, formatDateTime, formatNumber } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 价格历史页
 *
 * 路由：/products/:id/price-history
 * 功能：
 *  - 顶部展示商品摘要（名称 / 状态 / 当前价格区间）
 *  - SKU 筛选下拉（全部 SKU 或指定 SKU）
 *  - 价格趋势折线图（多 SKU 序列，按变更时间排列）
 *  - 价格变更记录表格（旧价 / 新价 / 变动幅度 / 变更人 / 原因）
 *
 * 数据来源：
 *  - GET /products/{id}            商品详情（含 SKU 列表，用于筛选选项）
 *  - GET /products/{id}/price-history?skuId=xxx  价格变更记录
 */

const route = useRoute()
const router = useRouter()

const productId = computed(() => route.params.id as string)

const loadingDetail = ref(true)
const loadingHistory = ref(true)
const detail = ref<ProductDetailDto | null>(null)
const records = ref<PriceChangeRecordDto[]>([])

/** 选中的 SKU ID（空字符串表示全部） */
const selectedSkuId = ref<string>('')

/** ECharts 系列颜色（与 ChartLine 一致） */
const SERIES_COLORS = ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1', '#13C2C2', '#EB2F96', '#FA8C16']

/** 表格列定义 */
const columns: TableColumnsType = [
  {
    title: '变更时间',
    dataIndex: 'createdAt',
    key: 'createdAt',
    width: 170,
    sorter: (a: PriceChangeRecordDto, b: PriceChangeRecordDto) =>
      dayjs(a.createdAt).valueOf() - dayjs(b.createdAt).valueOf(),
    defaultSortOrder: 'descend',
  },
  {
    title: 'SKU 编码',
    dataIndex: 'skuCode',
    key: 'skuCode',
    width: 140,
  },
  {
    title: '规格组合',
    dataIndex: 'skuName',
    key: 'skuName',
    width: 160,
    ellipsis: true,
  },
  {
    title: '旧价格',
    dataIndex: 'oldPrice',
    key: 'oldPrice',
    width: 110,
    align: 'right',
  },
  {
    title: '新价格',
    dataIndex: 'newPrice',
    key: 'newPrice',
    width: 110,
    align: 'right',
  },
  {
    title: '变动幅度',
    key: 'change',
    width: 120,
    align: 'right',
  },
  {
    title: '变更人',
    dataIndex: 'operator',
    key: 'operator',
    width: 100,
  },
  {
    title: '变更原因',
    dataIndex: 'reason',
    key: 'reason',
    ellipsis: true,
  },
]

/** SKU 选项列表 */
const skuOptions = computed(() => {
  const skus = detail.value?.skus ?? []
  return [
    { label: `全部 SKU（${formatNumber(skus.length)} 个）`, value: '' },
    ...skus.map((s) => ({
      label: `${s.skuCode} ${s.skuName}`,
      value: s.id,
    })),
  ]
})

/** 过滤后的记录（按 selectedSkuId） */
const filteredRecords = computed<PriceChangeRecordDto[]>(() => {
  if (!selectedSkuId.value) return records.value
  return records.value.filter((r) => r.skuId === selectedSkuId.value)
})

/** 传给 API 的 skuId（空字符串转 undefined） */
const apiSkuId = computed<string | undefined>(() =>
  selectedSkuId.value ? selectedSkuId.value : undefined,
)

/** 排序后的记录（按时间倒序） */
const sortedRecords = computed<PriceChangeRecordDto[]>(() =>
  [...filteredRecords.value].sort(
    (a, b) => dayjs(b.createdAt).valueOf() - dayjs(a.createdAt).valueOf(),
  ),
)

/** 图表中需要展示的 SKU 列表（仅出现在记录中的 SKU） */
const chartSkus = computed<ProductSkuDto[]>(() => {
  const skus = detail.value?.skus ?? []
  const usedSkuIds = new Set(filteredRecords.value.map((r) => r.skuId))
  return skus.filter((s) => usedSkuIds.has(s.id))
})

/** 计算价格变动幅度（百分比） */
function priceChangePercent(oldPrice: number, newPrice: number): number {
  if (oldPrice <= 0) return 0
  return ((newPrice - oldPrice) / oldPrice) * 100
}

/** 变动幅度样式 */
function changeClass(percent: number): string {
  if (percent > 0) return 'change-up'
  if (percent < 0) return 'change-down'
  return 'change-flat'
}

/** 变动幅度文案 */
function changeText(percent: number): string {
  const sign = percent > 0 ? '+' : ''
  return `${sign}${percent.toFixed(1)}%`
}

/** 图表 X 轴：所有变更时间点（去重 + 升序） */
const chartXAxis = computed<string[]>(() => {
  const times = new Set<string>()
  filteredRecords.value.forEach((r) => {
    times.add(dayjs(r.createdAt).format('MM-DD HH:mm'))
  })
  return Array.from(times).sort((a, b) => {
    const ta = dayjs(a, 'MM-DD HH:mm').valueOf()
    const tb = dayjs(b, 'MM-DD HH:mm').valueOf()
    return ta - tb
  })
})

/** 图表 series：每个 SKU 一条折线 */
const chartSeries = computed<EChartsOption['series']>(() => {
  const xAxis = chartXAxis.value
  return chartSkus.value.map((sku, idx) => {
    const skuRecords = filteredRecords.value
      .filter((r) => r.skuId === sku.id)
      .sort(
        (a, b) => dayjs(a.createdAt).valueOf() - dayjs(b.createdAt).valueOf(),
      )
    // 在每个时间点填充该 SKU 的最新价格（前向填充）
    const data: Array<{ value: number; metaData: PriceChangeRecordDto | null }> = xAxis.map(
      (x) => {
        const xTime = dayjs(x, 'MM-DD HH:mm').valueOf()
        // 找出该时间点之前（含）最近一条该 SKU 的记录
        const matched = skuRecords
          .filter((r) => dayjs(r.createdAt).valueOf() <= xTime)
          .pop()
        return { value: matched ? matched.newPrice : 0, metaData: matched ?? null }
      },
    )
    return {
      name: `${sku.skuCode} ${sku.skuName}`,
      type: 'line',
      smooth: true,
      data,
      itemStyle: { color: SERIES_COLORS[idx % SERIES_COLORS.length] },
      lineStyle: { color: SERIES_COLORS[idx % SERIES_COLORS.length] },
      connectNulls: true,
    }
  })
})

/** 图表完整配置 */
const chartOption = computed<EChartsOption>(() => ({
  tooltip: {
    trigger: 'axis',
    formatter: (params: unknown) => {
      const list = params as Array<{
        seriesName: string
        value: { value: number; metaData: PriceChangeRecordDto | null }
        color: string
      }>
      if (!Array.isArray(list) || list.length === 0) return ''
      const lines: string[] = []
      const first = list[0]
      const time = dayjs(first.value.metaData?.createdAt).format('YYYY-MM-DD HH:mm') ?? ''
      lines.push(`<div style="font-weight:600;margin-bottom:4px">${time}</div>`)
      list.forEach((p) => {
        const rec = p.value.metaData
        if (!rec) return
        lines.push(
          `<div style="display:flex;justify-content:space-between;gap:12px">
            <span><span style="display:inline-block;width:8px;height:8px;border-radius:50%;background:${p.color};margin-right:6px"></span>${p.seriesName}</span>
            <span style="font-weight:600">${formatMoney(rec.newPrice)}</span>
          </div>`,
        )
        if (rec.reason) {
          lines.push(`<div style="color:#8c8c8c;font-size:12px;margin-left:14px">原因：${rec.reason}</div>`)
        }
      })
      return lines.join('')
    },
  },
  legend: { top: 0, type: 'scroll' },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: chartXAxis.value,
    axisLabel: { color: '#8c8c8c' },
  },
  yAxis: {
    type: 'value',
    axisLabel: {
      color: '#8c8c8c',
      formatter: (val: number) => `¥${val}`,
    },
    splitLine: { lineStyle: { color: '#f0f0f0' } },
  },
  series: chartSeries.value,
}))

/** 是否有图表数据 */
const hasChartData = computed(
  () => chartSkus.value.length > 0 && filteredRecords.value.length > 0,
)

/** 加载商品详情 */
async function loadDetail(): Promise<void> {
  loadingDetail.value = true
  try {
    detail.value = await productApi.get(productId.value)
  } catch (e) {
    logger.error('加载商品详情失败', e)
    message.error('加载商品详情失败，将返回列表')
    router.push('/products')
  } finally {
    loadingDetail.value = false
  }
}

/** 加载价格历史记录 */
async function loadHistory(): Promise<void> {
  loadingHistory.value = true
  try {
    records.value = await productApi.getPriceHistory(productId.value, apiSkuId.value)
  } catch (e) {
    logger.error('加载价格历史失败', e)
    message.error('加载价格历史失败，请稍后重试')
    records.value = []
  } finally {
    loadingHistory.value = false
  }
}

/** SKU 筛选变化 */
function onSkuChange(value: string | undefined): void {
  selectedSkuId.value = value ?? ''
  void loadHistory()
}

/** 刷新 */
async function onRefresh(): Promise<void> {
  await Promise.all([loadDetail(), loadHistory()])
}

/** 返回 SKU 管理 */
function goBack(): void {
  router.push(`/products/${productId.value}/skus`)
}

onMounted(() => {
  void Promise.all([loadDetail(), loadHistory()])
})
</script>

<template>
  <div class="price-history-page">
    <Breadcrumb class="price-history-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>商品管理</BreadcrumbItem>
      <BreadcrumbItem>价格历史</BreadcrumbItem>
    </Breadcrumb>

    <!-- 商品摘要 -->
    <Card class="price-history-summary" :bordered="true">
      <Skeleton v-if="loadingDetail" active :paragraph="{ rows: 1 }" />
      <div v-else-if="detail" class="price-history-summary-grid">
        <div class="price-history-summary-item">
          <span class="price-history-summary-label">商品名称：</span>
          <span class="price-history-summary-value">{{ detail.name }}</span>
        </div>
        <div class="price-history-summary-item">
          <span class="price-history-summary-label">商品状态：</span>
          <StatusTag type="product" :status="detail.status" />
        </div>
        <div class="price-history-summary-item">
          <span class="price-history-summary-label">SKU 数：</span>
          <span class="price-history-summary-value">{{ formatNumber(detail.skus.length) }}</span>
        </div>
        <div class="price-history-summary-item">
          <span class="price-history-summary-label">价格区间：</span>
          <span class="price-history-summary-value price-history-summary-price">
            {{ detail.priceRange || '-' }}
          </span>
        </div>
      </div>
    </Card>

    <!-- 筛选栏 -->
    <Card class="price-history-filter" :bordered="true">
      <Space :size="12" wrap>
        <div class="price-history-filter-item">
          <span class="price-history-filter-label">SKU 筛选：</span>
          <Select
            v-model:value="selectedSkuId"
            placeholder="全部 SKU"
            allow-clear
            style="width: 280px"
            :options="skuOptions"
            :loading="loadingDetail"
            @change="onSkuChange"
          />
        </div>
        <div class="price-history-filter-spacer" />
        <Button :icon="h(ReloadOutlined)" :loading="loadingHistory" @click="onRefresh">
          刷新
        </Button>
        <Button :icon="h(ArrowLeftOutlined)" @click="goBack">返回 SKU 管理</Button>
      </Space>
    </Card>

    <!-- 价格趋势图 -->
    <Card class="price-history-chart-card" :bordered="true">
      <template #title>
        <Space>
          <LineChartOutlined class="price-history-chart-icon" />
          <span class="price-history-chart-title">价格趋势</span>
        </Space>
      </template>
      <template #extra>
        <span class="price-history-chart-hint">
          共 {{ formatNumber(filteredRecords.length) }} 条变更记录
        </span>
      </template>
      <div class="price-history-chart-body">
        <Spin v-if="loadingHistory" tip="加载中..." class="price-history-spin" />
        <EmptyState
          v-else-if="!hasChartData"
          description="暂无价格变更记录，调价后将在此展示"
        />
        <VChart
          v-else
          :option="chartOption"
          autoresize
          class="price-history-vchart"
        />
      </div>
    </Card>

    <!-- 价格变更记录表格 -->
    <Card class="price-history-table-card" :bordered="true">
      <template #title>
        <span class="price-history-table-title">价格变更记录</span>
      </template>
      <template #extra>
        <span class="price-history-table-hint">
          共 {{ formatNumber(sortedRecords.length) }} 条，按时间倒序
        </span>
      </template>

      <Skeleton v-if="loadingHistory" :title="{ width: '100%' }" :paragraph="{ rows: 6 }" active />
      <EmptyState
        v-else-if="sortedRecords.length === 0"
        description="暂无价格变更记录"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="sortedRecords"
        :row-key="(record: PriceChangeRecordDto) => record.id"
        :pagination="{ pageSize: 10, showSizeChanger: true, showQuickJumper: true, showTotal: (t: number) => `共 ${t} 条` }"
        size="middle"
        :scroll="{ x: 1100 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'createdAt'">
            <span class="cell-time">{{ formatDateTime(record.createdAt) }}</span>
          </template>

          <template v-else-if="column.key === 'skuCode'">
            <span class="sku-code">{{ record.skuCode }}</span>
          </template>

          <template v-else-if="column.key === 'oldPrice'">
            <span class="price-old">{{ formatMoney(record.oldPrice) }}</span>
          </template>

          <template v-else-if="column.key === 'newPrice'">
            <span class="price-new">{{ formatMoney(record.newPrice) }}</span>
          </template>

          <template v-else-if="column.key === 'change'">
            <span :class="['change-pill', changeClass(priceChangePercent(record.oldPrice, record.newPrice))]">
              <ArrowUpOutlined v-if="priceChangePercent(record.oldPrice, record.newPrice) > 0" />
              <ArrowDownOutlined v-else-if="priceChangePercent(record.oldPrice, record.newPrice) < 0" />
              {{ changeText(priceChangePercent(record.oldPrice, record.newPrice)) }}
            </span>
          </template>

          <template v-else-if="column.key === 'reason'">
            <Tag v-if="record.reason" class="reason-tag">{{ record.reason }}</Tag>
            <span v-else class="reason-empty">-</span>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.price-history-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.price-history-breadcrumb {
  font-size: 14px;
}
.price-history-summary {
  border-radius: 8px;
}
.price-history-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.price-history-summary-item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.price-history-summary-label {
  font-size: 13px;
  color: #8c8c8c;
  white-space: nowrap;
}
.price-history-summary-value {
  font-size: 14px;
  color: #000000d9;
  font-weight: 500;
}
.price-history-summary-price {
  color: #ff4d4f;
  font-weight: 600;
}
.price-history-filter {
  border-radius: 8px;
}
.price-history-filter-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.price-history-filter-label {
  font-size: 14px;
  color: #595959;
  white-space: nowrap;
}
.price-history-filter-spacer {
  flex: 1;
}
.price-history-chart-card {
  border-radius: 8px;
}
.price-history-chart-icon {
  color: #1677ff;
  font-size: 16px;
}
.price-history-chart-title {
  font-size: 16px;
  font-weight: 500;
}
.price-history-chart-hint {
  font-size: 13px;
  color: #8c8c8c;
}
.price-history-chart-body {
  display: flex;
  flex-direction: column;
}
.price-history-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 360px;
}
.price-history-vchart {
  width: 100%;
  height: 360px;
}
.price-history-table-card {
  border-radius: 8px;
}
.price-history-table-title {
  font-size: 16px;
  font-weight: 500;
}
.price-history-table-hint {
  font-size: 13px;
  color: #8c8c8c;
}
.cell-time {
  color: #595959;
  font-size: 13px;
}
.sku-code {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 13px;
  color: #1677ff;
}
.price-old {
  color: #8c8c8c;
  text-decoration: line-through;
  font-size: 13px;
}
.price-new {
  color: #000000d9;
  font-weight: 600;
}
.change-pill {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  font-size: 13px;
  font-weight: 500;
}
.change-pill.change-up {
  color: #ff4d4f;
}
.change-pill.change-down {
  color: #52c41a;
}
.change-pill.change-flat {
  color: #8c8c8c;
}
.reason-tag {
  border-radius: 4px;
  font-size: 12px;
}
.reason-empty {
  color: #8c8c8c;
}

@media (max-width: 1199px) {
  .price-history-summary-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
