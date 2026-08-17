<template>
  <div class="payment-stats">
    <!-- 区域 A：时间 / 刷新工具栏 -->
    <div class="payment-stats__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 区域 B：3 张指标卡片（支付成功率 / 支付笔数 / 失败笔数） -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="支付成功率"
          :value="(data?.kpi.successRate ?? 0).toFixed(1)"
          unit="%"
          :loading="loading"
          :value-color="successRateColor"
          :tooltip="successRateTooltip"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="支付笔数"
          :value="(data?.kpi.paymentCount ?? 0).toLocaleString('zh-CN')"
          :loading="loading"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="失败笔数"
          :value="(data?.kpi.failedCount ?? 0).toLocaleString('zh-CN')"
          :loading="loading"
          :value-color="data && data.kpi.failedCount > 0 ? '#FAAD14' : ''"
        />
      </a-col>
    </a-row>

    <!-- 区域 C/D：成功率仪表盘 + 渠道分布柱状图 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="8">
        <ChartGauge
          title="支付成功率"
          :value="data?.kpi.successRate ?? 0"
          :thresholds="[95, 99]"
          :loading="loading"
          :height="300"
        />
      </a-col>
      <a-col :xs="24" :lg="16">
        <a-card title="渠道分布（按笔数降序）" class="payment-stats__card">
          <ChartBarHorizontal
            v-if="hasChannelData"
            :categories="channelBarCategories"
            :values="channelBarValues"
            series-name="支付笔数"
            :height="300"
            :loading="loading"
          />
          <EmptyState
            v-else-if="!loading"
            :description="errorMessage"
            :action-text="error ? '重试' : undefined"
            @action="loadData"
          />
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 D/E：失败原因 Top5 环形图 + 渠道明细表 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="10">
        <a-card title="失败原因 Top5" class="payment-stats__card">
          <ChartDonut v-if="hasFailureData" :data="failureDonutData" :height="300" :loading="loading" />
          <EmptyState v-else-if="!loading" :description="error ? '加载支付统计失败' : '暂无失败原因数据'" />
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="14">
        <a-card title="渠道明细" class="payment-stats__card">
          <template #extra>
            <span class="payment-stats__hint">点击行查看渠道失败明细</span>
          </template>
          <a-table
            v-if="!error"
            :columns="channelColumns"
            :data-source="channelRows"
            :loading="loading"
            :pagination="false"
            row-key="channel"
            size="middle"
            :custom-row="channelRowProps"
          >
            <template #emptyText>
              <EmptyState description="暂无支付数据" />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'amount'">{{ formatMoney(record.amount) }}</template>
              <template v-else-if="column.key === 'successRate'">
                <span :style="{ color: channelRateColor(record.successRate) }">
                  {{ record.successRate.toFixed(1) }}%
                </span>
              </template>
              <template v-else-if="column.key === 'change'">
                <span :class="record.change >= 0 ? 'payment-stats__up' : 'payment-stats__down'">
                  {{ record.change >= 0 ? '↑' : '↓' }} {{ Math.abs(record.change).toFixed(1) }}%
                </span>
              </template>
            </template>
          </a-table>
          <EmptyState v-else description="加载支付统计失败" action-text="重试" @action="loadData" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 渠道失败明细抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`${drawerChannel ?? ''} 失败明细`"
      placement="right"
      width="520"
    >
      <div class="payment-stats__drawer">
        <a-descriptions v-if="drawerStat" :column="1" size="small" bordered class="payment-stats__descriptions">
          <a-descriptions-item label="支付笔数">
            {{ drawerStat.count.toLocaleString('zh-CN') }}
          </a-descriptions-item>
          <a-descriptions-item label="支付金额">{{ formatMoney(drawerStat.amount) }}</a-descriptions-item>
          <a-descriptions-item label="成功率">
            <span :style="{ color: channelRateColor(drawerStat.successRate) }">
              {{ drawerStat.successRate.toFixed(1) }}%
            </span>
          </a-descriptions-item>
        </a-descriptions>
        <h3 class="payment-stats__drawer-title">失败原因</h3>
        <a-table
          v-if="drawerFailureReasons.length > 0"
          :columns="drawerColumns"
          :data-source="drawerFailureReasons"
          :pagination="false"
          row-key="reason"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'lastOccurredAt'">{{ formatDateTime(record.lastOccurredAt) }}</template>
          </template>
        </a-table>
        <EmptyState v-else description="该渠道暂无失败记录" />
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { DateTimeRangePicker, EmptyState, DashboardCard } from '@/shared/components'
import { formatMoney, formatDateTime } from '@/shared/utils/format'
import { dashboardApi } from '../api/dashboard.api'
import {
  parsePaymentStatsData,
  type PaymentStatsData,
  type PaymentChannelStat,
  type DateRangeParams,
} from '../types/dashboard.dto'
import ChartGauge from '../components/ChartGauge.vue'
import ChartDonut from '../components/ChartDonut.vue'
import ChartBarHorizontal from '../components/ChartBarHorizontal.vue'

const route = useRoute()
const loading = ref(false)
const error = ref(false)
const data = ref<PaymentStatsData | null>(null)
const drawerVisible = ref(false)
const drawerChannel = ref<string | null>(null)

// 初始化时间范围：优先读取路由 query（运营总览 GMV 卡跳转携带），否则默认近 7 天
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

const hasChannelData = computed(() => !!data.value && data.value.channelDistribution.length > 0)
const hasFailureData = computed(() => !!data.value && data.value.failureReasons.length > 0)

const errorMessage = computed(() => (error.value ? '加载支付统计失败' : '暂无支付数据'))

// 成功率卡片着色：<95% 红、95~99% 黄、>=99% 绿
const successRateColor = computed(() => channelRateColor(data.value?.kpi.successRate ?? 0))

const successRateTooltip = computed(() => {
  const rate = data.value?.kpi.successRate
  if (rate === undefined || rate >= 95) return ''
  return `支付成功率 ${rate.toFixed(1)}%，低于 95% 阈值，请检查支付链路`
})

// 渠道成功率着色（与仪表盘阈值一致）
function channelRateColor(rate: number): string {
  if (rate < 95) return '#FF4D4F'
  if (rate < 99) return '#FAAD14'
  return '#52C41A'
}

// 渠道分布柱状图：按笔数降序
const channelBarCategories = computed(() =>
  data.value?.channelDistribution.slice().sort((a, b) => b.count - a.count).map((c) => c.channel) ?? []
)

const channelBarValues = computed(() =>
  data.value?.channelDistribution.slice().sort((a, b) => b.count - a.count).map((c) => c.count) ?? []
)

// 失败原因 Top5 环形图数据
const failureDonutData = computed(() =>
  data.value?.failureReasons
    .slice()
    .sort((a, b) => b.count - a.count)
    .slice(0, 5)
    .map((r) => ({ name: r.reason, value: r.count })) ?? []
)

// 渠道明细表数据
const channelRows = computed(() => data.value?.channelDistribution ?? [])

const channelColumns = [
  { title: '渠道', dataIndex: 'channel', key: 'channel' },
  { title: '笔数', dataIndex: 'count', key: 'count', width: 100, align: 'right' as const },
  { title: '金额', key: 'amount', width: 120, align: 'right' as const },
  { title: '成功率', key: 'successRate', width: 100, align: 'right' as const },
  { title: '环比', key: 'change', width: 100, align: 'right' as const },
]

// 抽屉内失败原因表列
const drawerColumns = [
  { title: '失败原因', dataIndex: 'reason', key: 'reason' },
  { title: '失败笔数', dataIndex: 'count', key: 'count', width: 90, align: 'right' as const },
  { title: '最近发生', key: 'lastOccurredAt', width: 150 },
]

// 抽屉对应渠道统计
const drawerStat = computed<PaymentChannelStat | null>(
  () => data.value?.channelDistribution.find((c) => c.channel === drawerChannel.value) ?? null
)

// 抽屉内该渠道的失败原因列表（按失败数降序）
const drawerFailureReasons = computed(() =>
  data.value?.failureReasons
    .filter((r) => r.channel === drawerChannel.value)
    .slice()
    .sort((a, b) => b.count - a.count) ?? []
)

// 行点击打开渠道失败明细抽屉
function channelRowProps(record: PaymentChannelStat) {
  return {
    onClick: () => {
      drawerChannel.value = record.channel
      drawerVisible.value = true
    },
    style: { cursor: 'pointer' },
  }
}

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
    const report = await dashboardApi.getPaymentStats(params)
    data.value = parsePaymentStatsData(report)
  } catch {
    error.value = true
    message.error('加载支付统计失败')
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
.payment-stats__hint {
  font-size: 12px;
  color: #8c8c8c;
}
.payment-stats__up {
  color: #52c41a;
}
.payment-stats__down {
  color: #ff4d4f;
}
.payment-stats__drawer-title {
  margin: 16px 0 8px;
  font-size: 14px;
  font-weight: 600;
}
.payment-stats__descriptions {
  margin-bottom: 8px;
}
</style>
