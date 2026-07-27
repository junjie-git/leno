<template>
  <div class="points-stats">
    <!-- 筛选条 -->
    <div class="points-stats__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="发放量"
          :value="formatPoints(data?.kpi.issued ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.issuedChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="消耗量"
          :value="formatPoints(data?.kpi.consumed ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.consumedChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="净增"
          :value="formatPoints(data?.kpi.net ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.netChange)"
          :value-color="netColor"
          :tooltip="netTooltip"
        />
      </a-col>
    </a-row>

    <!-- 主趋势图 -->
    <a-card title="发放 vs 消耗 双系列趋势" class="points-stats__card">
      <a-spin :spinning="loading">
        <ChartLine
          v-if="hasTrendData"
          :series="trendSeries"
          :x-axis="trendXAxis"
          :height="320"
        />
        <EmptyState
          v-else-if="!loading"
          description="暂无积分数据"
          action-text="刷新"
          @action="loadData"
        />
      </a-spin>
    </a-card>

    <!-- 来源分布 -->
    <a-card title="发放来源分布" class="points-stats__card">
      <a-spin :spinning="loading">
        <ChartPie
          v-if="hasSourceData"
          :data="sourcePieData"
          :height="280"
        />
        <EmptyState v-else-if="!loading" description="暂无来源分布数据" />
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import { parsePointsStatsData, type PointsStatsData, type DateRangeParams } from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'

const route = useRoute()
const loading = ref(false)
const data = ref<PointsStatsData | null>(null)

// 积分统计默认近 30 天
function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast30DaysRange()
}

function getLast30DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 30)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

function onDateRangeChange(val: [string, string]) {
  dateRange.value = val
}

const hasTrendData = computed(() => !!data.value && data.value.dailyTrend.length > 0)
const hasSourceData = computed(() => !!data.value && data.value.sourceDistribution.length > 0)

// 净增为负时显示红色
const netColor = computed(() => {
  const net = data.value?.kpi.net ?? 0
  return net < 0 ? '#FF4D4F' : ''
})

const netTooltip = computed(() => {
  const net = data.value?.kpi.net ?? 0
  if (net < 0) return '消耗超过发放，请检查营销活动配置'
  return ''
})

// 格式化积分数值：≥10000 显示万单位
function formatPoints(value: number): string {
  if (value >= 10000) return `${(value / 10000).toFixed(1)}万`
  return value.toLocaleString('zh-CN')
}

function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 折线图 X 轴：日期列表
const trendXAxis = computed(() => {
  if (!data.value) return []
  return data.value.dailyTrend.map((p) => p.date.slice(0, 10))
})

// 折线图 series：发放 + 消耗双系列
const trendSeries = computed(() => {
  if (!data.value) return []
  return [
    {
      name: '发放',
      type: 'line' as const,
      smooth: true,
      data: data.value.dailyTrend.map((p) => p.issued),
    },
    {
      name: '消耗',
      type: 'line' as const,
      smooth: true,
      data: data.value.dailyTrend.map((p) => p.consumed),
    },
  ]
})

// 饼图数据：发放来源分布
const sourcePieData = computed(() =>
  data.value?.sourceDistribution.map((item) => ({ name: item.source, value: item.value })) ?? []
)

// 检测异常峰值：单日发放量 > 均值 3 倍
function detectAnomaly() {
  if (!data.value || data.value.dailyTrend.length === 0) return
  const trend = data.value.dailyTrend
  const avgIssued = trend.reduce((sum, p) => sum + p.issued, 0) / trend.length
  const threshold = avgIssued * 3
  const anomaly = trend.find((p) => p.issued > threshold && avgIssued > 0)
  if (anomaly) {
    notification.warning({
      message: '检测到积分发放异常峰值',
      description: `${anomaly.date.slice(0, 10)} 发放量 ${anomaly.issued.toLocaleString('zh-CN')}，超过均值 3 倍，请检查营销活动配置`,
    })
  }
}

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getPointsStats(params)
    data.value = parsePointsStatsData(report)
    detectAnomaly()
  } catch {
    message.error('积分统计加载失败')
  } finally {
    loading.value = false
  }
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.points-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.points-stats__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
}
.points-stats__card {
  border-radius: 8px;
}
</style>
