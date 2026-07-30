<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Segmented,
  Spin,
  Skeleton,
  message,
} from 'ant-design-vue'
import {
  ShoppingOutlined,
  DollarOutlined,
  TruckOutlined,
  CustomerServiceOutlined,
} from '@ant-design/icons-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'
import dayjs from 'dayjs'
import { dashboardApi } from '../api/dashboard.api'
import type { SellerDashboardDto, SalesTrendItemDto } from '../types/dashboard.dto'
import { DashboardCard, EmptyState } from '@/shared/components'
import { formatMoney } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

const router = useRouter()

const loading = ref(true)
const trendLoading = ref(false)
const dashboard = ref<SellerDashboardDto | null>(null)
const trend = ref<SalesTrendItemDto[]>([])
const rangeType = ref<'7d' | '30d'>('7d')

const rangeOptions = [
  { label: '近 7 天', value: '7d' },
  { label: '近 30 天', value: '30d' },
]

const currencySymbol = computed(() => {
  const c = dashboard.value?.todaySalesCurrency ?? 'CNY'
  return c === 'CNY' ? '¥' : '$'
})

const todayOrderCount = computed(() => dashboard.value?.todayOrderCount ?? 0)
const todaySalesAmount = computed(() => dashboard.value?.todaySalesAmount ?? 0)
const pendingOrders = computed(() => dashboard.value?.pendingOrders ?? 0)
const todayRefundCount = computed(() => dashboard.value?.todayRefundCount ?? 0)

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

function buildRange(type: '7d' | '30d'): { from: string; to: string } {
  const days = type === '7d' ? 7 : 30
  const to = dayjs()
  const from = to.subtract(days - 1, 'day')
  return { from: from.format('YYYY-MM-DD'), to: to.format('YYYY-MM-DD') }
}

async function loadDashboard(): Promise<void> {
  try {
    dashboard.value = await dashboardApi.getDashboard()
  } catch (e) {
    logger.error('加载工作台概览失败', e)
    message.error('网络异常')
  }
}

async function loadTrend(): Promise<void> {
  trendLoading.value = true
  try {
    trend.value = await dashboardApi.getSalesTrend(buildRange(rangeType.value))
  } catch (e) {
    logger.error('加载销售趋势失败', e)
    message.error('网络异常')
  } finally {
    trendLoading.value = false
  }
}

function goPendingShipment(): void {
  router.push('/orders/pending-shipment')
}

function goAfterSales(): void {
  router.push('/after-sales')
}

onMounted(async () => {
  loading.value = true
  await Promise.all([loadDashboard(), loadTrend()])
  loading.value = false
})
</script>

<template>
  <div class="overview-page">
    <Breadcrumb class="overview-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>工作台</BreadcrumbItem>
      <BreadcrumbItem>经营概览</BreadcrumbItem>
    </Breadcrumb>

    <!-- 区域 A：统计卡片 -->
    <div class="overview-cards">
      <DashboardCard
        title="今日订单数"
        :value="todayOrderCount"
        :loading="loading"
        tooltip="今日 0 点至今的订单总数"
      />
      <DashboardCard
        title="今日销售额"
        :value="formatMoney(todaySalesAmount, { symbol: currencySymbol })"
        :loading="loading"
        tooltip="今日 0 点至今的已支付销售额"
      />
      <DashboardCard
        title="待发货"
        :value="pendingOrders"
        :loading="loading"
        value-color="#FAAD14"
        tooltip="当前待发货订单总数"
      />
      <DashboardCard
        title="售后待处理"
        :value="todayRefundCount"
        :loading="loading"
        value-color="#FAAD14"
        tooltip="今日待处理的售后申请数"
      />
    </div>

    <!-- 区域 B：销售趋势 -->
    <Card class="overview-trend" :bordered="true">
      <template #title>
        <span class="overview-trend-title">销售趋势</span>
      </template>
      <template #extra>
        <Segmented v-model:value="rangeType" :options="rangeOptions" @change="loadTrend" />
      </template>
      <div class="overview-trend-body" aria-describedby="trend-desc">
        <Spin v-if="trendLoading" tip="加载中..." class="overview-spin" />
        <EmptyState
          v-else-if="!hasTrendData"
          description="暂无销售数据，有订单后将自动生成趋势"
        />
        <VChart
          v-else
          :option="trendOption"
          autoresize
          class="overview-chart"
        />
        <p id="trend-desc" class="overview-trend-desc">
          双 Y 轴折线图：左轴销售额（元），右轴订单数（笔）。
        </p>
      </div>
    </Card>

    <!-- 区域 C/D：待办列表 -->
    <div class="overview-todos">
      <Card class="overview-todo-panel" :bordered="true">
        <template #title>
          <span class="overview-todo-title">
            <TruckOutlined class="overview-todo-icon" />
            待发货
          </span>
        </template>
        <template #extra>
          <a class="overview-link" @click="goPendingShipment">查看全部</a>
        </template>
        <Skeleton v-if="loading" :title="{ width: '60%' }" :paragraph="{ rows: 3 }" active />
        <template v-else>
          <div v-if="pendingOrders > 0" class="overview-todo-summary">
            <ShoppingOutlined class="overview-todo-summary-icon" />
            <span class="overview-todo-count">{{ pendingOrders }}</span>
            <span class="overview-todo-unit">笔待发货订单</span>
          </div>
          <EmptyState v-else description="暂无待发货订单" />
          <div class="overview-todo-footer">
            <a class="overview-link" @click="goPendingShipment">前往处理 →</a>
          </div>
        </template>
      </Card>

      <Card class="overview-todo-panel" :bordered="true">
        <template #title>
          <span class="overview-todo-title">
            <CustomerServiceOutlined class="overview-todo-icon" />
            售后待处理
          </span>
        </template>
        <template #extra>
          <a class="overview-link" @click="goAfterSales">查看全部</a>
        </template>
        <Skeleton v-if="loading" :title="{ width: '60%' }" :paragraph="{ rows: 3 }" active />
        <template v-else>
          <div v-if="todayRefundCount > 0" class="overview-todo-summary">
            <CustomerServiceOutlined class="overview-todo-summary-icon" />
            <span class="overview-todo-count">{{ todayRefundCount }}</span>
            <span class="overview-todo-unit">笔售后待处理</span>
          </div>
          <EmptyState v-else description="暂无售后待处理" />
          <div class="overview-todo-footer">
            <a class="overview-link" @click="goAfterSales">前往处理 →</a>
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.overview-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.overview-breadcrumb {
  font-size: 14px;
}
.overview-cards {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.overview-trend {
  border-radius: 8px;
}
.overview-trend-title {
  font-size: 16px;
  font-weight: 500;
}
.overview-trend-body {
  display: flex;
  flex-direction: column;
  align-items: stretch;
}
.overview-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 320px;
}
.overview-chart {
  width: 100%;
  height: 320px;
}
.overview-trend-desc {
  margin-top: 8px;
  font-size: 12px;
  color: #8c8c8c;
}
.overview-todos {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}
.overview-todo-panel {
  border-radius: 8px;
}
.overview-todo-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 500;
}
.overview-todo-icon {
  color: #faad14;
  font-size: 16px;
}
.overview-todo-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 0;
}
.overview-todo-summary-icon {
  font-size: 20px;
  color: #faad14;
}
.overview-todo-count {
  font-size: 28px;
  font-weight: 600;
  color: #faad14;
}
.overview-todo-unit {
  font-size: 14px;
  color: #595959;
}
.overview-todo-footer {
  margin-top: 12px;
  text-align: right;
}
.overview-link {
  color: #1677ff;
  cursor: pointer;
  font-size: 14px;
}
.overview-link:hover {
  text-decoration: underline;
}

@media (max-width: 1199px) {
  .overview-cards {
    grid-template-columns: repeat(2, 1fr);
  }
  .overview-todos {
    grid-template-columns: 1fr;
  }
}
</style>
