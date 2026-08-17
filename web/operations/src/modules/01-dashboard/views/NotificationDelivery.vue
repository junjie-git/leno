<template>
  <div class="notification-delivery">
    <!-- 区域 A：时间 / 刷新工具栏 -->
    <div class="notification-delivery__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 区域 B：4 渠道送达率卡片（短信 / 邮件 / 站内信 / Push，各含仪表盘） -->
    <a-row :gutter="24">
      <a-col v-for="channel in channelCards" :key="channel.key" :xs="24" :sm="12" :lg="6">
        <div class="notification-delivery__gauge-wrapper">
          <a-tooltip v-if="channel.rate < 95" title="送达率低于 95% 阈值，请排查通知链路">
            <WarningOutlined class="notification-delivery__warning" />
          </a-tooltip>
          <ChartGauge
            :title="`${channel.label} 送达率`"
            :value="channel.rate"
            :thresholds="[95, 99]"
            :loading="loading"
            :height="200"
          />
        </div>
      </a-col>
      <a-col v-if="!loading && channelCards.length === 0" :span="24">
        <a-card class="notification-delivery__card">
          <EmptyState :description="errorMessage" :action-text="error ? '重试' : undefined" @action="loadData" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 C/D：多渠道送达率趋势 + 失败原因环形图 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="16">
        <a-card title="送达率趋势（多渠道）" class="notification-delivery__card">
          <ChartLine
            v-if="hasTrendData"
            :series="trendSeries"
            :x-axis="trendXAxis"
            :height="300"
            :loading="loading"
          />
          <EmptyState
            v-else-if="!loading"
            :description="error ? '加载通知送达率失败' : '暂无渠道趋势数据'"
            :action-text="error ? '重试' : undefined"
            @action="loadData"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="8">
        <a-card title="失败原因分布" class="notification-delivery__card">
          <ChartDonut v-if="hasFailureData" :data="failureDonutData" :height="300" :loading="loading" />
          <EmptyState v-else-if="!loading" :description="error ? '加载通知送达率失败' : '暂无失败原因数据'" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 E：渠道明细表（statistics 端点） -->
    <a-card title="渠道明细" class="notification-delivery__card">
      <a-table
        v-if="!error"
        :columns="statColumns"
        :data-source="statRows"
        :loading="loading"
        :pagination="false"
        row-key="channel"
        size="middle"
      >
        <template #emptyText>
          <EmptyState description="暂无通知数据" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'channel'">{{ channelLabel(record.channel) }}</template>
          <template v-else-if="column.key === 'deliveryRate'">
            <span :style="{ color: rateColor(record.deliveryRate) }">{{ record.deliveryRate.toFixed(1) }}%</span>
          </template>
          <template v-else-if="column.key === 'avgLatency'">{{ formatLatency(record.avgLatencyMs) }}</template>
        </template>
      </a-table>
      <EmptyState v-else description="加载通知送达率失败" action-text="重试" @action="loadData" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined, WarningOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { DateTimeRangePicker, EmptyState, ChartLine } from '@/shared/components'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseNotificationDeliveryData,
  parseNotificationStatisticsData,
  type NotificationDeliveryData,
  type NotificationStatisticsData,
  type DateRangeParams,
} from '../types/dashboard.dto'
import ChartGauge from '../components/ChartGauge.vue'
import ChartDonut from '../components/ChartDonut.vue'

const route = useRoute()
const loading = ref(false)
const error = ref(false)
const deliveryData = ref<NotificationDeliveryData | null>(null)
const statisticsData = ref<NotificationStatisticsData | null>(null)

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

const errorMessage = computed(() => (error.value ? '加载通知送达率失败' : '暂无通知数据'))

// 渠道元信息：dashboard 端点固定返回四渠道速率
const CHANNEL_META: { key: 'sms' | 'email' | 'inapp' | 'push'; label: string }[] = [
  { key: 'sms', label: '短信' },
  { key: 'email', label: '邮件' },
  { key: 'inapp', label: '站内信' },
  { key: 'push', label: 'Push' },
]

// 渠道原始值 → 中文标签（大小写不敏感，兼容 Sms/Inapp/Push 等）
const CHANNEL_LABELS: Record<string, string> = {
  sms: '短信',
  email: '邮件',
  inapp: '站内信',
  push: 'Push',
}

function channelLabel(channel: string): string {
  return CHANNEL_LABELS[channel.toLowerCase()] ?? channel
}

// 4 渠道卡片数据
const channelCards = computed(() =>
  CHANNEL_META.map((meta) => ({
    ...meta,
    rate: deliveryData.value?.channelRates[meta.key] ?? 0,
  })),
)

const hasTrendData = computed(() => !!deliveryData.value && deliveryData.value.dailyTrend.length > 0)
const hasFailureData = computed(() => !!deliveryData.value && deliveryData.value.failureReasons.length > 0)

// 送达率着色：>99% 绿、95~99% 黄、<95% 红
function rateColor(rate: number): string {
  if (rate < 95) return '#FF4D4F'
  if (rate < 99) return '#FAAD14'
  return '#52C41A'
}

// 趋势 X 轴：去重日期（升序）
const trendXAxis = computed(() => {
  const points = deliveryData.value?.dailyTrend ?? []
  const seen = new Set<string>()
  const dates: string[] = []
  for (const p of points) {
    const d = p.date.slice(0, 10)
    if (!seen.has(d)) {
      seen.add(d)
      dates.push(d)
    }
  }
  return dates.sort()
})

// 多渠道趋势 series：每渠道一条曲线
const trendSeries = computed(() => {
  const points = deliveryData.value?.dailyTrend ?? []
  if (points.length === 0) return []
  const dates = trendXAxis.value
  const channels = [...new Set(points.map((p) => p.channel))]
  return channels.map((channel) => ({
    name: channelLabel(channel),
    type: 'line' as const,
    smooth: true,
    data: dates.map((date) => {
      const point = points.find((p) => p.date.slice(0, 10) === date && p.channel === channel)
      return point ? point.rate : 0
    }),
  }))
})

// 失败原因环形图数据：按原因聚合全部渠道失败数
const failureDonutData = computed(() => {
  const reasons = deliveryData.value?.failureReasons ?? []
  const aggregated = new Map<string, number>()
  for (const item of reasons) {
    aggregated.set(item.reason, (aggregated.get(item.reason) ?? 0) + item.count)
  }
  return [...aggregated.entries()]
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value)
})

// 渠道明细表数据
const statRows = computed(() => statisticsData.value?.items ?? [])

const statColumns = [
  { title: '渠道', key: 'channel', width: 120 },
  { title: '发送数', dataIndex: 'totalCount', key: 'totalCount', width: 120, align: 'right' as const },
  { title: '送达数', dataIndex: 'deliveredCount', key: 'deliveredCount', width: 120, align: 'right' as const },
  { title: '失败数', dataIndex: 'failedCount', key: 'failedCount', width: 100, align: 'right' as const },
  { title: '送达率', key: 'deliveryRate', width: 100, align: 'right' as const },
  { title: '平均延迟', key: 'avgLatency', width: 110, align: 'right' as const },
]

// 平均延迟格式化：>=1000ms 转秒展示
function formatLatency(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`
  return `${ms}ms`
}

// 并行请求 dashboard 与 statistics 端点合并渲染
async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  error.value = false
  try {
    const dateParams: DateRangeParams = { start, end }
    const [report, statistics] = await Promise.all([
      dashboardApi.getNotificationDelivery(dateParams),
      dashboardApi.getNotificationStatistics({ from: start, to: end }),
    ])
    deliveryData.value = parseNotificationDeliveryData(report)
    statisticsData.value = parseNotificationStatisticsData(statistics)
  } catch {
    error.value = true
    message.error('加载通知送达率失败')
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
.notification-delivery {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.notification-delivery__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.notification-delivery__card {
  border-radius: 8px;
}
.notification-delivery__gauge-wrapper {
  position: relative;
}
.notification-delivery__warning {
  position: absolute;
  top: 16px;
  right: 24px;
  z-index: 1;
  font-size: 16px;
  color: #ff4d4f;
}
</style>
