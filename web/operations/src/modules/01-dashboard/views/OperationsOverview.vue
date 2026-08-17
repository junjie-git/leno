<template>
  <div class="operations-overview">
    <!-- 时间 / 刷新工具栏 -->
    <div class="operations-overview__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 4 指标卡：订单量 / GMV / 转化率 / 客单价（含同比 trend） -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="订单量"
          :value="(data?.kpi.orderCount ?? 0).toLocaleString('zh-CN')"
          :loading="loading"
          :trend="buildTrend(data?.change.orderCountChange)"
          tooltip="点击跳转订单管理"
          @click="navigateTo('/order-ops/order-management')"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="GMV"
          :value="formatGmv(data?.kpi.gmv ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.gmvChange)"
          tooltip="点击跳转支付统计"
          @click="navigateTo('/dashboard/payment-stats')"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="转化率"
          :value="(data?.kpi.conversionRate ?? 0).toFixed(1)"
          unit="%"
          :loading="loading"
          :trend="buildTrend(data?.change.conversionRateChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="客单价"
          :value="`¥${(data?.kpi.avgOrderAmount ?? 0).toFixed(2)}`"
          :loading="loading"
          :trend="buildTrend(data?.change.avgOrderAmountChange)"
        />
      </a-col>
    </a-row>

    <!-- GMV / 订单量双轴趋势 + 订单来源分布环形图 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="16">
        <a-card title="GMV 与订单量趋势" class="operations-overview__card">
          <ChartLineDual
            v-if="hasTrendData"
            :series="trendSeries"
            :x-axis="trendXAxis"
            left-name="GMV"
            right-name="订单量"
            :height="300"
          />
          <EmptyState
            v-else-if="!loading"
            :description="errorMessage"
            :action-text="errorMessage === '暂无运营数据' ? '刷新' : '重试'"
            @action="loadData"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="8">
        <a-card title="订单来源分布" class="operations-overview__card">
          <ChartDonut v-if="hasSourceData" :data="sourceDonutData" :height="300" />
          <EmptyState v-else-if="!loading" :description="errorMessage" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { DateTimeRangePicker, EmptyState, DashboardCard } from '@/shared/components'
import { dashboardApi } from '../api/dashboard.api'
import { parseOverviewData, type OverviewData, type DateRangeParams } from '../types/dashboard.dto'
import ChartLineDual from '../components/ChartLineDual.vue'
import ChartDonut from '../components/ChartDonut.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const error = ref(false)
const data = ref<OverviewData | null>(null)

// 初始化时间范围：优先读取路由 query，否则默认近 7 天
function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

function onDateRangeChange(val: [string, string]) {
  dateRange.value = val
}

const hasTrendData = computed(() => !!data.value && data.value.dailyTrend.length > 0)
const hasSourceData = computed(() => !!data.value && data.value.sourceDistribution.length > 0)

// 图表区空/错误态文案
const errorMessage = computed(() =>
  error.value ? '加载运营数据失败' : '暂无运营数据',
)

// GMV 格式化：≥1 万显示万单位
function formatGmv(value: number): string {
  if (value >= 10000) return `¥${(value / 10000).toFixed(1)}万`
  return `¥${value.toLocaleString('zh-CN')}`
}

// 同比变化 → DashboardCard trend 对象
function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 双轴趋势 X 轴：日期（yyyy-MM-dd）
const trendXAxis = computed(() => data.value?.dailyTrend.map((p) => p.date.slice(0, 10)) ?? [])

// 双轴趋势 series：GMV（左轴）+ 订单量（右轴）
const trendSeries = computed(() => {
  if (!data.value) return []
  return [
    { name: 'GMV', data: data.value.dailyTrend.map((p) => p.gmv) },
    { name: '订单量', data: data.value.dailyTrend.map((p) => p.orderCount) },
  ]
})

// 来源分布环形图数据
const sourceDonutData = computed(() =>
  data.value?.sourceDistribution.map((item) => ({ name: item.source, value: item.value })) ?? []
)

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  error.value = false
  try {
    const params: DateRangeParams = { start, end }
    const report = await dashboardApi.getOverview(params)
    data.value = parseOverviewData(report)
  } catch {
    error.value = true
    message.error('加载运营数据失败')
  } finally {
    loading.value = false
  }
}

// 指标卡跳转：携带当前时间范围
function navigateTo(path: string) {
  const [start, end] = dateRange.value
  router.push({ path, query: { start, end } })
}

// 时间变化：写入 route.query 并重新加载（含首次进入，默认近 7 天同步到地址栏）
watch(
  dateRange,
  ([start, end]) => {
    if (route.query.start !== start || route.query.end !== end) {
      router.replace({ query: { ...route.query, start, end } })
    }
    loadData()
  },
  { deep: true, immediate: true },
)
</script>

<style scoped>
.operations-overview {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.operations-overview__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.operations-overview__card {
  border-radius: 8px;
}
</style>
