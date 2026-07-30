<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Spin,
  RangePicker,
  message,
} from 'ant-design-vue'
import { CalendarOutlined } from '@ant-design/icons-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'
import dayjs, { type Dayjs } from 'dayjs'
import { dashboardApi } from '../api/dashboard.api'
import type { SalesTrendItemDto } from '../types/dashboard.dto'
import { EmptyState } from '@/shared/components'
import { formatMoney, formatNumber } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/** 最大支持 90 天范围 */
const MAX_RANGE_DAYS = 90

const loading = ref(false)
const trend = ref<SalesTrendItemDto[]>([])
const dateRange = ref<[Dayjs, Dayjs]>(defaultRange())

function defaultRange(): [Dayjs, Dayjs] {
  const to = dayjs().startOf('day')
  const from = to.subtract(29, 'day')
  return [from, to]
}

function toApiParams(range: [Dayjs, Dayjs]): { from: string; to: string } {
  return {
    from: range[0].format('YYYY-MM-DD'),
    to: range[1].format('YYYY-MM-DD'),
  }
}

const totalSales = computed(() =>
  trend.value.reduce((sum, t) => sum + t.salesAmount, 0),
)
const totalOrders = computed(() =>
  trend.value.reduce((sum, t) => sum + t.orderCount, 0),
)
const avgOrderValue = computed(() => {
  if (totalOrders.value === 0) return 0
  return totalSales.value / totalOrders.value
})

const trendOption = computed<EChartsOption>(() => {
  const dates = trend.value.map((t) => dayjs(t.date).format('MM-DD'))
  const sales = trend.value.map((t) => t.salesAmount)
  const orders = trend.value.map((t) => t.orderCount)
  return {
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross' },
    },
    legend: { data: ['销售额', '订单数'], top: 0 },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: dates,
    },
    yAxis: [
      {
        type: 'value',
        name: '销售额',
        position: 'left',
        axisLabel: {
          formatter: (val: number) => `¥${val.toLocaleString('zh-CN')}`,
        },
      },
      {
        type: 'value',
        name: '订单数',
        position: 'right',
        axisLabel: {
          formatter: (val: number) => `${val}`,
        },
      },
    ],
    series: [
      {
        name: '销售额',
        type: 'line',
        smooth: true,
        data: sales,
        itemStyle: { color: '#1677FF' },
        lineStyle: { color: '#1677FF' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(22, 119, 255, 0.25)' },
              { offset: 1, color: 'rgba(22, 119, 255, 0.02)' },
            ],
          },
        },
      },
      {
        name: '订单数',
        type: 'line',
        smooth: true,
        yAxisIndex: 1,
        data: orders,
        itemStyle: { color: '#52C41A' },
        lineStyle: { color: '#52C41A' },
      },
    ],
  }
})

const hasTrendData = computed(() => trend.value.length > 0)

async function loadTrend(): Promise<void> {
  loading.value = true
  try {
    trend.value = await dashboardApi.getSalesTrend(toApiParams(dateRange.value))
  } catch (e) {
    logger.error('加载销售趋势失败', e)
    message.error('网络异常')
  } finally {
    loading.value = false
  }
}

function onRangeChange(dates: [Dayjs, Dayjs] | null): void {
  if (!dates) return
  const [from, to] = dates
  if (to.isBefore(from)) {
    message.error('结束日期不能早于开始日期')
    return
  }
  const diffDays = to.diff(from, 'day') + 1
  if (diffDays > MAX_RANGE_DAYS) {
    const truncatedTo = from.add(MAX_RANGE_DAYS - 1, 'day')
    dateRange.value = [from.startOf('day'), truncatedTo.startOf('day')]
    message.warning(`最多支持 ${MAX_RANGE_DAYS} 天范围，已截断为近 ${MAX_RANGE_DAYS} 天`)
  } else {
    dateRange.value = [from.startOf('day'), to.startOf('day')]
  }
  loadTrend()
}

function disabledDate(current: Dayjs): boolean {
  return !!current && current > dayjs().endOf('day')
}

onMounted(() => {
  loadTrend()
})
</script>

<template>
  <div class="sales-trend-page">
    <Breadcrumb class="sales-trend-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>工作台</BreadcrumbItem>
      <BreadcrumbItem>销售趋势</BreadcrumbItem>
    </Breadcrumb>

    <!-- 筛选栏 -->
    <Card class="sales-trend-filter" :bordered="true">
      <div class="sales-trend-filter-bar">
        <div class="sales-trend-filter-label">
          <CalendarOutlined class="sales-trend-filter-icon" />
          <span>时间范围</span>
        </div>
        <RangePicker
          :value="dateRange"
          :disabled-date="disabledDate"
          :allow-clear="false"
          @change="onRangeChange as any"
        />
      </div>
    </Card>

    <!-- 汇总卡片 -->
    <div class="sales-trend-summary">
      <Card class="sales-trend-summary-card" :bordered="true">
        <div class="sales-trend-summary-title">总销售额</div>
        <div class="sales-trend-summary-value">{{ formatMoney(totalSales) }}</div>
      </Card>
      <Card class="sales-trend-summary-card" :bordered="true">
        <div class="sales-trend-summary-title">总订单数</div>
        <div class="sales-trend-summary-value">{{ formatNumber(totalOrders) }}</div>
      </Card>
      <Card class="sales-trend-summary-card" :bordered="true">
        <div class="sales-trend-summary-title">平均客单价</div>
        <div class="sales-trend-summary-value">{{ formatMoney(avgOrderValue) }}</div>
      </Card>
      <Card class="sales-trend-summary-card" :bordered="true">
        <div class="sales-trend-summary-title">数据天数</div>
        <div class="sales-trend-summary-value">{{ trend.length }}</div>
      </Card>
    </div>

    <!-- 主趋势折线图 -->
    <Card class="sales-trend-chart" :bordered="true">
      <template #title>
        <span class="sales-trend-chart-title">销售额 / 订单数趋势</span>
      </template>
      <div class="sales-trend-chart-body" aria-describedby="trend-desc">
        <Spin v-if="loading" tip="加载中..." class="sales-trend-spin" />
        <EmptyState
          v-else-if="!hasTrendData"
          description="所选时间范围内无销售记录"
        />
        <VChart
          v-else
          :option="trendOption"
          autoresize
          class="sales-trend-vchart"
        />
        <p id="trend-desc" class="sales-trend-desc">
          双 Y 轴折线图：左轴销售额（元），右轴订单数（笔）。悬停可查看每日明细。
        </p>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.sales-trend-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.sales-trend-breadcrumb {
  font-size: 14px;
}
.sales-trend-filter {
  border-radius: 8px;
}
.sales-trend-filter-bar {
  display: flex;
  align-items: center;
  gap: 12px;
}
.sales-trend-filter-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  color: #595959;
}
.sales-trend-filter-icon {
  font-size: 16px;
  color: #1677ff;
}
.sales-trend-summary {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.sales-trend-summary-card {
  border-radius: 8px;
}
.sales-trend-summary-title {
  font-size: 14px;
  color: #8c8c8c;
  margin-bottom: 8px;
}
.sales-trend-summary-value {
  font-size: 24px;
  font-weight: 600;
  color: #000000d9;
}
.sales-trend-chart {
  border-radius: 8px;
}
.sales-trend-chart-title {
  font-size: 16px;
  font-weight: 500;
}
.sales-trend-chart-body {
  display: flex;
  flex-direction: column;
  align-items: stretch;
}
.sales-trend-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 400px;
}
.sales-trend-vchart {
  width: 100%;
  height: 400px;
}
.sales-trend-desc {
  margin-top: 8px;
  font-size: 12px;
  color: #8c8c8c;
}

@media (max-width: 1199px) {
  .sales-trend-summary {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
