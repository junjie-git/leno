<template>
  <div class="notification-delivery">
    <!-- 筛选条 -->
    <div class="notification-delivery__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-select
        v-model:value="selectedChannels"
        mode="multiple"
        placeholder="选择渠道"
        style="min-width: 280px"
        :options="channelOptions"
        allow-clear
      />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 4 渠道 Gauge 网格 -->
    <a-row :gutter="24">
      <a-col v-for="stat in filteredChannelStats" :key="stat.channel" :xs="24" :sm="12" :lg="6">
        <div class="notification-delivery__gauge-wrapper">
          <a-badge v-if="stat.deliveryRate < 90" status="error" class="notification-delivery__badge" />
          <ChartGauge
            :title="`${stat.channel} 送达率`"
            :value="stat.deliveryRate"
            :thresholds="[90, 95]"
            :loading="loading"
            :height="220"
          />
        </div>
      </a-col>
      <template v-if="!loading && filteredChannelStats.length === 0">
        <a-col :span="24">
          <EmptyState description="暂无渠道送达数据" />
        </a-col>
      </template>
    </a-row>

    <!-- 趋势折线 -->
    <a-card title="渠道送达率趋势" class="notification-delivery__card">
      <a-spin :spinning="loading">
        <ChartLine
          v-if="hasTrendData"
          :series="trendSeries"
          :x-axis="trendXAxis"
          :height="280"
        />
        <EmptyState v-else-if="!loading" description="所选渠道暂无趋势数据" />
      </a-spin>
    </a-card>

    <!-- 失败原因表 -->
    <a-card title="失败原因分布" class="notification-delivery__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="filteredFailureReasons.length > 0"
          :columns="failureColumns"
          :data-source="filteredFailureReasons"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          row-key="reason"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'reason'">
              <a class="notification-delivery__link" @click="openFailureDrawer(record)">{{ record.reason }}</a>
            </template>
            <template v-else-if="column.key === 'proportion'">
              {{ computeProportion(record.count) }}%
            </template>
            <template v-else-if="column.key === 'lastOccurredAt'">
              <a class="notification-delivery__link" @click="navigateToAuditLogs(record.reason)">
                {{ formatDateTime(record.lastOccurredAt) }}
              </a>
            </template>
          </template>
        </a-table>
        <a-empty v-else-if="!loading" description="无失败记录" />
      </a-spin>
    </a-card>

    <!-- 失败原因详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`${drawerReason} — 近 7 天分布`"
      width="480"
    >
      <a-spin :spinning="drawerLoading">
        <ChartLine
          v-if="drawerTrendSeries.length && drawerTrendXAxis.length"
          :series="drawerTrendSeries"
          :x-axis="drawerTrendXAxis"
          :height="300"
        />
        <EmptyState v-else description="暂无该失败原因的趋势数据" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import dayjs from 'dayjs'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseNotificationDeliveryData,
  type NotificationDeliveryData,
  type NotificationFailureReason,
  type DateRangeParams,
} from '../types/dashboard.dto'
import ChartGauge from '../components/ChartGauge.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<NotificationDeliveryData | null>(null)
const selectedChannels = ref<string[]>([])
const drawerVisible = ref(false)
const drawerReason = ref('')
const drawerLoading = ref(false)

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

// 全部渠道选项（来自返回数据）
const channelOptions = computed(() =>
  (data.value?.channelStats ?? []).map((c) => ({ label: c.channel, value: c.channel }))
)

// 按选中渠道过滤后的渠道统计
const filteredChannelStats = computed(() => {
  if (!data.value) return []
  if (selectedChannels.value.length === 0) return data.value.channelStats
  return data.value.channelStats.filter((c) => selectedChannels.value.includes(c.channel))
})

// 按选中渠道过滤后的失败原因
const filteredFailureReasons = computed<NotificationFailureReason[]>(() => {
  if (!data.value) return []
  if (selectedChannels.value.length === 0) return data.value.failureReasons
  return data.value.failureReasons.filter((r) => selectedChannels.value.includes(r.channel))
})

// 过滤后的趋势数据
const filteredTrend = computed(() => {
  if (!data.value) return []
  const trend = data.value.dailyTrend
  if (selectedChannels.value.length === 0) return trend
  return trend.filter((p) => selectedChannels.value.includes(p.channel))
})

const hasTrendData = computed(() => filteredTrend.value.length > 0)

// 趋势图 X 轴：去重日期
const trendXAxis = computed(() => {
  const seen = new Set<string>()
  const dates: string[] = []
  for (const p of filteredTrend.value) {
    const d = p.date.slice(0, 10)
    if (!seen.has(d)) {
      seen.add(d)
      dates.push(d)
    }
  }
  return dates
})

// 趋势图 series：多系列按渠道
const trendSeries = computed(() => {
  if (!data.value) return []
  const dates = trendXAxis.value
  const channels = [...new Set(filteredTrend.value.map((p) => p.channel))]
  return channels.map((channel) => ({
    name: channel,
    type: 'line' as const,
    smooth: true,
    data: dates.map((date) => {
      const point = filteredTrend.value.find((p) => p.date.slice(0, 10) === date && p.channel === channel)
      return point ? point.rate : 0
    }),
  }))
})

// 失败原因表列定义
const failureColumns = [
  { title: '渠道', dataIndex: 'channel', key: 'channel', width: 120 },
  { title: '失败原因', dataIndex: 'reason', key: 'reason' },
  { title: '失败数', dataIndex: 'count', key: 'count', width: 100, sorter: (a: NotificationFailureReason, b: NotificationFailureReason) => b.count - a.count, defaultSortOrder: 'descend' as const },
  { title: '占比', key: 'proportion', width: 100 },
  { title: '最近发生时间', key: 'lastOccurredAt', width: 200 },
]

// 计算失败占比（相对全部失败数）
function computeProportion(count: number): string {
  const total = filteredFailureReasons.value.reduce((sum, r) => sum + r.count, 0)
  if (total === 0) return '0.0'
  return ((count / total) * 100).toFixed(1)
}

// 格式化日期时间
function formatDateTime(iso: string): string {
  return dayjs(iso).format('YYYY-MM-DD HH:mm')
}

// 抽屉趋势 X 轴：按 drawerReason 所在渠道过滤日期
const drawerTrendXAxis = computed(() => {
  if (!drawerReason.value || !data.value) return []
  const reason = data.value.failureReasons.find((r) => r.reason === drawerReason.value)
  if (!reason) return []
  const channelTrend = data.value.dailyTrend.filter((p) => p.channel === reason.channel)
  const seen = new Set<string>()
  const dates: string[] = []
  for (const p of channelTrend) {
    const d = p.date.slice(0, 10)
    if (!seen.has(d)) {
      seen.add(d)
      dates.push(d)
    }
  }
  return dates
})

// 抽屉趋势 series
const drawerTrendSeries = computed(() => {
  if (!drawerReason.value || !data.value) return []
  const reason = data.value.failureReasons.find((r) => r.reason === drawerReason.value)
  if (!reason) return []
  const channelTrend = data.value.dailyTrend.filter((p) => p.channel === reason.channel)
  const dates = drawerTrendXAxis.value
  return [
    {
      name: reason.channel,
      type: 'line' as const,
      smooth: true,
      data: dates.map((date) => {
        const point = channelTrend.find((p) => p.date.slice(0, 10) === date)
        return point ? point.rate : 0
      }),
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
    const { data: report } = await dashboardApi.getNotificationDelivery(params)
    data.value = parseNotificationDeliveryData(report)
    // 检查是否有渠道 < 90% 触发 error 告警
    const lowRateChannel = data.value.channelStats.find((c) => c.deliveryRate < 90)
    if (lowRateChannel) {
      notification.error({
        message: '通知送达率严重偏低',
        description: `${lowRateChannel.channel} 渠道送达率 ${lowRateChannel.deliveryRate.toFixed(1)}%，低于 90% 阈值，请立即排查通知链路`,
      })
    }
  } catch {
    message.error('通知送达率加载失败')
  } finally {
    loading.value = false
  }
}

function openFailureDrawer(record: NotificationFailureReason) {
  drawerReason.value = record.reason
  drawerLoading.value = true
  drawerVisible.value = true
  // 模拟详情加载延迟
  setTimeout(() => {
    drawerLoading.value = false
  }, 200)
}

function navigateToAuditLogs(reason: string) {
  router.push({
    path: '/audit/audit-logs',
    query: { resourceType: 'Notification', keyword: reason },
  })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
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
.notification-delivery__badge {
  position: absolute;
  top: 16px;
  right: 24px;
  z-index: 1;
}
.notification-delivery__link {
  color: #1677FF;
  cursor: pointer;
}
.notification-delivery__link:hover {
  text-decoration: underline;
}
</style>
