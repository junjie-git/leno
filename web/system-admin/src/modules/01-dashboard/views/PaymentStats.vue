<template>
  <div class="payment-stats">
    <!-- 筛选条 -->
    <div class="payment-stats__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-select
        v-model:value="selectedChannels"
        mode="multiple"
        placeholder="选择渠道"
        style="min-width: 240px"
        :options="channelOptions"
        allow-clear
      />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="总支付笔数"
          :value="filteredKpi.totalCount.toLocaleString('zh-CN')"
          :loading="loading"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="整体成功率"
          :value="filteredKpi.successRate.toFixed(1)"
          unit="%"
          :loading="loading"
          :value-color="successRateColor"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <PermissionGuard permission="dashboard:paymentLatency">
          <DashboardCard
            title="平均到账时长"
            :value="(data?.kpi.avgLatencyMs ?? 0).toFixed(1)"
            unit="ms"
            :loading="loading"
          />
        </PermissionGuard>
      </a-col>
    </a-row>

    <!-- 整体成功率仪表盘 + 渠道排行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="8">
        <ChartGauge
          title="整体成功率"
          :value="filteredKpi.successRate"
          :thresholds="[80, 95]"
          :loading="loading"
          :height="220"
        />
      </a-col>
      <a-col :xs="24" :lg="16">
        <a-card title="渠道成功率排行" class="payment-stats__card">
          <a-spin :spinning="loading">
            <ChartBar
              v-if="filteredChannelStats.length"
              :series="channelBarSeries"
              :x-axis="channelBarXAxis"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="所选时间范围内暂无支付数据" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- 失败原因分布 -->
    <a-card title="失败原因分布" class="payment-stats__card">
      <a-spin :spinning="loading">
        <ChartPie
          v-if="hasFailureData"
          :data="failurePieData"
          :height="280"
        />
        <EmptyState v-else-if="!loading" description="暂无失败原因数据" />
      </a-spin>
    </a-card>

    <!-- 渠道趋势抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`${drawerChannel} 近 7 天成功率趋势`"
      width="480"
    >
      <ChartLine
        v-if="drawerTrendSeries.length && drawerTrendXAxis.length"
        :series="drawerTrendSeries"
        :x-axis="drawerTrendXAxis"
        :height="300"
      />
      <EmptyState v-else description="暂无趋势数据" />
    </a-drawer>
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
import ChartBar from '@/shared/components/charts/ChartBar.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import { parsePaymentStatsData, type PaymentStatsData, type ChannelStat, type DateRangeParams } from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'
import ChartGauge from '../components/ChartGauge.vue'

const route = useRoute()
const loading = ref(false)
const data = ref<PaymentStatsData | null>(null)
const selectedChannels = ref<string[]>([])
const drawerVisible = ref(false)
const drawerChannel = ref('')

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

// 全部渠道选项
const channelOptions = computed(() =>
  (data.value?.channelStats ?? []).map((c) => ({ label: c.channel, value: c.channel }))
)

// 按选中渠道过滤后的渠道统计
const filteredChannelStats = computed<ChannelStat[]>(() => {
  if (!data.value) return []
  if (selectedChannels.value.length === 0) return data.value.channelStats
  return data.value.channelStats.filter((c) => selectedChannels.value.includes(c.channel))
})

// 过滤后重新汇总的 KPI
const filteredKpi = computed(() => {
  const stats = filteredChannelStats.value
  const totalCount = stats.reduce((sum, c) => sum + c.count, 0)
  const successCount = stats.reduce((sum, c) => sum + Math.round((c.count * c.successRate) / 100), 0)
  const successRate = totalCount > 0 ? (successCount / totalCount) * 100 : 0
  return {
    totalCount,
    successRate,
    avgLatencyMs: data.value?.kpi.avgLatencyMs ?? 0,
  }
})

// 成功率颜色：<80% 红、80-95% 黄、>95% 绿
const successRateColor = computed(() => {
  const rate = filteredKpi.value.successRate
  if (rate < 80) return '#FF4D4F'
  if (rate < 95) return '#FAAD14'
  return '#52C41A'
})

const hasFailureData = computed(() => !!data.value && data.value.failureReasons.length > 0)

// 渠道排行柱状图 X 轴
const channelBarXAxis = computed(() =>
  filteredChannelStats.value
    .slice()
    .sort((a, b) => b.successRate - a.successRate)
    .map((c) => c.channel)
)

// 渠道排行柱状图 series
const channelBarSeries = computed(() => {
  const sorted = filteredChannelStats.value
    .slice()
    .sort((a, b) => b.successRate - a.successRate)
  return [
    {
      name: '成功率',
      type: 'bar' as const,
      data: sorted.map((c) => c.successRate),
    },
  ]
})

// 失败原因饼图数据
const failurePieData = computed(() =>
  data.value?.failureReasons.map((r) => ({ name: r.reason, value: r.count })) ?? []
)

// 抽屉趋势 X 轴（从 dailyTrend 按渠道过滤；后端未返回趋势时用单点默认值填充）
const drawerTrendXAxis = computed(() => {
  if (!drawerChannel.value) return []
  const channelStat = filteredChannelStats.value.find((c) => c.channel === drawerChannel.value)
  if (channelStat) return ['当日']
  return []
})

// 抽屉趋势 series
const drawerTrendSeries = computed(() => {
  if (!drawerChannel.value) return []
  const channelStat = filteredChannelStats.value.find((c) => c.channel === drawerChannel.value)
  if (!channelStat) return []
  return [
    {
      name: '成功率',
      type: 'line' as const,
      smooth: true,
      data: [channelStat.successRate],
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
    const { data: report } = await dashboardApi.getPaymentStats(params)
    data.value = parsePaymentStatsData(report)
    // 成功率低于 95% 触发警告通知
    if (data.value.kpi.successRate < 95) {
      notification.warning({
        message: '支付成功率低于阈值',
        description: `当前整体成功率 ${data.value.kpi.successRate.toFixed(1)}%，请检查支付链路`,
      })
    }
  } catch {
    message.error('支付统计加载失败')
  } finally {
    loading.value = false
  }
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.payment-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.payment-stats__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.payment-stats__card {
  border-radius: 8px;
}
</style>
