<template>
  <div class="points-stats">
    <!-- 区域 A：时间 / 刷新工具栏 -->
    <div class="points-stats__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 区域 B：3 张指标卡片（发放量 / 消耗量 / 净增量） -->
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
          title="净增量"
          :value="formatPoints(data?.kpi.net ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.netChange)"
          :value-color="netColor"
          :tooltip="netTooltip"
        />
      </a-col>
    </a-row>

    <!-- 区域 C/D：发放 / 消耗双系列趋势 + 发放来源分布环形图 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="14">
        <a-card title="发放 / 消耗趋势" class="points-stats__card">
          <ChartLine
            v-if="hasTrendData"
            :series="trendSeries"
            :x-axis="trendXAxis"
            :height="300"
            :loading="loading"
          />
          <EmptyState
            v-else-if="!loading"
            :description="errorMessage"
            :action-text="error ? '重试' : '刷新'"
            @action="loadData"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="10">
        <a-card title="发放来源分布" class="points-stats__card">
          <ChartDonut v-if="hasSourceData" :data="sourceDonutData" :height="300" :loading="loading" />
          <EmptyState v-else-if="!loading" :description="error ? '加载积分统计失败' : '暂无来源分布数据'" />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { DateTimeRangePicker, EmptyState, DashboardCard, ChartLine } from '@/shared/components'
import { dashboardApi } from '../api/dashboard.api'
import { parsePointsStatsData, type PointsStatsData, type DateRangeParams } from '../types/dashboard.dto'
import ChartDonut from '../components/ChartDonut.vue'

const route = useRoute()
const loading = ref(false)
const error = ref(false)
const data = ref<PointsStatsData | null>(null)

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

const errorMessage = computed(() => (error.value ? '加载积分统计失败' : '暂无积分数据'))

// 净增为负标红，提示消耗超过发放
const netColor = computed(() => ((data.value?.kpi.net ?? 0) < 0 ? '#FF4D4F' : ''))

const netTooltip = computed(() =>
  (data.value?.kpi.net ?? 0) < 0 ? '消耗超过发放，请检查营销活动配置' : '',
)

// 积分数值格式化：≥1 万显示万单位
function formatPoints(value: number): string {
  if (value >= 10000) return `${(value / 10000).toFixed(1)}万`
  return value.toLocaleString('zh-CN')
}

// 环比变化 → DashboardCard trend
function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 趋势 X 轴：日期（yyyy-MM-dd）
const trendXAxis = computed(() => data.value?.dailyTrend.map((p) => p.date.slice(0, 10)) ?? [])

// 双系列趋势：发放（蓝 #1677FF）+ 消耗（黄 #FAAD14），同轴同单位
const trendSeries = computed(() => {
  if (!data.value) return []
  return [
    {
      name: '发放',
      type: 'line' as const,
      smooth: true,
      itemStyle: { color: '#1677FF' },
      data: data.value.dailyTrend.map((p) => p.issued),
    },
    {
      name: '消耗',
      type: 'line' as const,
      smooth: true,
      itemStyle: { color: '#FAAD14' },
      data: data.value.dailyTrend.map((p) => p.consumed),
    },
  ]
})

// 来源分布环形图数据（签到/购物返积分/任务/活动奖励）
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
    const report = await dashboardApi.getPointsStats(params)
    data.value = parsePointsStatsData(report)
  } catch {
    error.value = true
    message.error('加载积分统计失败')
  } finally {
    loading.value = false
  }
}

// 时间变化即重新加载（含首次进入）
watch(
  dateRange,
  () => {
    loadData()
  },
  { deep: true, immediate: true },
)
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
  flex-wrap: wrap;
}
.points-stats__card {
  border-radius: 8px;
}
</style>
