<template>
  <div class="operations-overview">
    <!-- 筛选条 -->
    <div class="operations-overview__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 卡片网格 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="订单量"
          :value="data?.kpi.orderCount ?? 0"
          :loading="loading"
          :trend="buildTrend(data?.change.orderCountChange)"
          @click="navigateTo('/dashboard/after-sales-stats')"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="GMV"
          :value="formatGmv(data?.kpi.gmv ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.gmvChange)"
          @click="navigateTo('/dashboard/payment-stats')"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <PermissionGuard permission="dashboard:conversion">
          <DashboardCard
            title="转化率"
            :value="(data?.kpi.conversionRate ?? 0).toFixed(1)"
            unit="%"
            :loading="loading"
            :trend="buildTrend(data?.change.conversionRateChange)"
            :value-color="conversionColor"
            @click="navigateTo('/dashboard/shop-ranking')"
          />
        </PermissionGuard>
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="客单价"
          :value="(data?.kpi.avgOrderAmount ?? 0).toLocaleString('zh-CN')"
          unit="¥"
          :loading="loading"
          :trend="buildTrend(data?.change.avgOrderAmountChange)"
        />
      </a-col>
    </a-row>

    <!-- 主趋势图 -->
    <a-card title="GMV 与订单量趋势" class="operations-overview__card">
      <a-spin :spinning="loading">
        <ChartLine
          v-if="hasTrendData"
          :series="trendSeries"
          :x-axis="trendXAxis"
          :height="320"
        />
        <EmptyState
          v-else-if="!loading"
          description="暂无运营数据，请稍后重试"
          action-text="刷新"
          @action="loadData"
        />
      </a-spin>
    </a-card>

    <!-- 辅助图区 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="12">
        <a-card title="订单来源分布" class="operations-overview__card">
          <a-spin :spinning="loading">
            <ChartPie
              v-if="hasSourceData"
              :data="sourcePieData"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="暂无来源数据" />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="12">
        <a-card title="转化漏斗" class="operations-overview__card">
          <a-spin :spinning="loading">
            <ChartBar
              v-if="hasFunnelData"
              :series="funnelSeries"
              :x-axis="funnelXAxis"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="暂无漏斗数据" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import ChartBar from '@/shared/components/charts/ChartBar.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import { parseOverviewData, type OverviewData, type DateRangeParams } from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
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
const hasFunnelData = computed(() => !!data.value && data.value.funnel.length > 0)

// 转化率低于 5% 显示警告色
const conversionColor = computed(() => {
  const rate = data.value?.kpi.conversionRate ?? 0
  if (rate < 5) return '#FAAD14'
  return ''
})

// 格式化 GMV：≥1 万显示万单位
function formatGmv(value: number): string {
  if (value >= 10000) return `${(value / 10000).toFixed(1)}万`
  return value.toLocaleString('zh-CN')
}

// 构造趋势对象
function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 折线图 X 轴：去重的日期列表
const trendXAxis = computed(() => {
  if (!data.value) return []
  return data.value.dailyTrend.map((p) => p.date.slice(0, 10))
})

// 折线图 series：双系列 GMV + 订单量
const trendSeries = computed(() => {
  if (!data.value) return []
  return [
    {
      name: 'GMV',
      type: 'line' as const,
      smooth: true,
      data: data.value.dailyTrend.map((p) => p.gmv),
    },
    {
      name: '订单量',
      type: 'line' as const,
      smooth: true,
      data: data.value.dailyTrend.map((p) => p.orderCount),
    },
  ]
})

// 饼图数据：订单来源分布
const sourcePieData = computed(() =>
  data.value?.sourceDistribution.map((item) => ({ name: item.source, value: item.value })) ?? []
)

// 柱状图 X 轴：漏斗阶段
const funnelXAxis = computed(() =>
  data.value?.funnel.map((item) => item.stage) ?? []
)

// 柱状图 series：转化漏斗
const funnelSeries = computed(() => {
  if (!data.value) return []
  return [
    {
      name: '转化漏斗',
      type: 'bar' as const,
      data: data.value.funnel.map((item) => item.value),
    },
  ]
})

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getOverview(params)
    data.value = parseOverviewData(report)
  } catch {
    message.error('运营总览加载失败')
  } finally {
    loading.value = false
  }
}

function navigateTo(path: string) {
  const [start, end] = dateRange.value
  router.push({ path, query: { start, end } })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
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
}
.operations-overview__card {
  border-radius: 8px;
}
</style>
