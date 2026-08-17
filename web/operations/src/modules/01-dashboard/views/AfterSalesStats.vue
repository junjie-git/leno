<template>
  <div class="after-sales-stats">
    <!-- 区域 A：时间 / 刷新工具栏 -->
    <div class="after-sales-stats__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 区域 B：4 张指标卡片（售后单量 / 退款金额 / 售后率 / 平均处理时长） -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="售后单量"
          :value="(data?.kpi.afterSalesCount ?? 0).toLocaleString('zh-CN')"
          :loading="loading"
          :trend="buildTrend(data?.change.afterSalesCountChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="退款金额"
          :value="formatMoney(data?.kpi.refundAmount ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.refundAmountChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="售后率"
          :value="(data?.kpi.afterSalesRate ?? 0).toFixed(2)"
          unit="%"
          :loading="loading"
          :trend="buildTrend(data?.change.afterSalesRateChange)"
          :value-color="afterSalesRateColor"
          :tooltip="afterSalesRateTooltip"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="平均处理时长"
          :value="((data?.kpi.avgProcessHours ?? 0) / 24).toFixed(1)"
          unit="天"
          :loading="loading"
          :trend="buildTrend(data?.change.avgProcessHoursChange)"
          :value-color="avgProcessColor"
          :tooltip="avgProcessTooltip"
        />
      </a-col>
    </a-row>

    <!-- 区域 C/D：售后单量与退款金额双轴趋势 + 售后类型分布环形图 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="14">
        <a-card title="售后单量与退款金额趋势" class="after-sales-stats__card">
          <ChartLineDual
            v-if="hasTrendData"
            :series="trendSeries"
            :x-axis="trendXAxis"
            left-name="售后单量"
            right-name="退款金额"
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
        <a-card title="售后类型分布" class="after-sales-stats__card">
          <ChartDonut v-if="hasTypeData" :data="typeDonutData" :height="300" :loading="loading" />
          <EmptyState v-else-if="!loading" :description="error ? '加载售后统计失败' : '暂无售后类型数据'" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 E：状态分布表格 -->
    <a-card title="状态分布" class="after-sales-stats__card">
      <a-table
        v-if="!error"
        :columns="statusColumns"
        :data-source="statusRows"
        :loading="loading"
        :pagination="false"
        row-key="status"
        size="middle"
      >
        <template #emptyText>
          <EmptyState description="暂无售后数据" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusMeta(record.status).color">{{ statusMeta(record.status).label }}</a-tag>
          </template>
          <template v-else-if="column.key === 'proportion'">{{ computeProportion(record.count) }}%</template>
          <template v-else-if="column.key === 'avgProcessHours'">
            {{ (record.avgProcessHours / 24).toFixed(1) }} 天
          </template>
          <template v-else-if="column.key === 'refundAmount'">{{ formatMoney(record.refundAmount) }}</template>
        </template>
      </a-table>
      <EmptyState v-else description="加载售后统计失败" action-text="重试" @action="loadData" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { DateTimeRangePicker, EmptyState, DashboardCard } from '@/shared/components'
import { formatMoney } from '@/shared/utils/format'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseAfterSalesStatsData,
  type AfterSalesStatsData,
  type DateRangeParams,
} from '../types/dashboard.dto'
import ChartLineDual from '../components/ChartLineDual.vue'
import ChartDonut from '../components/ChartDonut.vue'

const route = useRoute()
const loading = ref(false)
const error = ref(false)
const data = ref<AfterSalesStatsData | null>(null)

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
const hasTypeData = computed(() => !!data.value && data.value.typeDistribution.length > 0)

const errorMessage = computed(() => (error.value ? '加载售后统计失败' : '暂无售后数据'))

// 售后率 > 10% 标红预警
const afterSalesRateColor = computed(() => ((data.value?.kpi.afterSalesRate ?? 0) > 10 ? '#FF4D4F' : ''))

const afterSalesRateTooltip = computed(() => {
  const rate = data.value?.kpi.afterSalesRate ?? 0
  return rate > 10 ? `售后率 ${rate.toFixed(2)}%，超过 10% 阈值，售后率偏高` : ''
})

// 平均处理时长 > 3 天（72 小时）标黄预警
const AVG_PROCESS_WARNING_HOURS = 72

const avgProcessColor = computed(() =>
  (data.value?.kpi.avgProcessHours ?? 0) > AVG_PROCESS_WARNING_HOURS ? '#FAAD14' : '',
)

const avgProcessTooltip = computed(() => {
  const hours = data.value?.kpi.avgProcessHours ?? 0
  return hours > AVG_PROCESS_WARNING_HOURS
    ? `平均处理时长 ${(hours / 24).toFixed(1)} 天，超过 3 天阈值，处理时效偏慢`
    : ''
})

// 售后状态元信息（shared StatusTag 无 afterSales 类型，模块内映射）
const STATUS_META: Record<string, { label: string; color: string }> = {
  Pending: { label: '待审核', color: 'warning' },
  AwaitReturn: { label: '待退货', color: 'warning' },
  ReturnPending: { label: '待退货', color: 'warning' },
  AwaitRefund: { label: '待退款', color: 'warning' },
  RefundPending: { label: '待退款', color: 'warning' },
  Processing: { label: '处理中', color: 'processing' },
  Completed: { label: '已完成', color: 'success' },
  Rejected: { label: '已拒绝', color: 'error' },
  Cancelled: { label: '已取消', color: 'default' },
}

function statusMeta(status: string): { label: string; color: string } {
  return STATUS_META[status] ?? { label: status, color: 'default' }
}

// 环比变化 → DashboardCard trend
function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 双轴趋势 X 轴：日期（yyyy-MM-dd）
const trendXAxis = computed(() => data.value?.dailyTrend.map((p) => p.date.slice(0, 10)) ?? [])

// 双轴趋势 series：售后单量（左轴）+ 退款金额（右轴）
const trendSeries = computed(() => {
  if (!data.value) return []
  return [
    { name: '售后单量', data: data.value.dailyTrend.map((p) => p.count) },
    { name: '退款金额', data: data.value.dailyTrend.map((p) => p.refundAmount) },
  ]
})

// 类型分布环形图数据（仅退款/退货退款/换货）
const typeDonutData = computed(() =>
  data.value?.typeDistribution.map((t) => ({ name: t.type, value: t.count })) ?? []
)

// 状态分布表数据
const statusRows = computed(() => data.value?.statusDistribution ?? [])

const statusColumns = [
  { title: '状态', key: 'status', width: 120 },
  { title: '售后单量', dataIndex: 'count', key: 'count', width: 120, align: 'right' as const },
  { title: '占比', key: 'proportion', width: 100, align: 'right' as const },
  { title: '平均处理时长', key: 'avgProcessHours', width: 140, align: 'right' as const },
  { title: '退款金额', key: 'refundAmount', width: 140, align: 'right' as const },
]

// 各状态单量占总量百分比
function computeProportion(count: number): string {
  const total = statusRows.value.reduce((sum, s) => sum + s.count, 0)
  if (total === 0) return '0.0'
  return ((count / total) * 100).toFixed(1)
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
    const report = await dashboardApi.getAfterSalesStats(params)
    data.value = parseAfterSalesStatsData(report)
  } catch {
    error.value = true
    message.error('加载售后统计失败')
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
.after-sales-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.after-sales-stats__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.after-sales-stats__card {
  border-radius: 8px;
}
</style>
