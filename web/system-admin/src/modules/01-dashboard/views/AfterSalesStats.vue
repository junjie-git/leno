<template>
  <div class="after-sales-stats">
    <!-- 筛选条 -->
    <div class="after-sales-stats__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-select
        v-model:value="selectedTypes"
        mode="multiple"
        placeholder="选择售后类型"
        style="min-width: 280px"
        :options="typeOptions"
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
          title="售后单量"
          :value="(data?.kpi.afterSalesCount ?? 0).toLocaleString('zh-CN')"
          :loading="loading"
          :trend="buildTrend(data?.change.afterSalesCountChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="退款金额"
          :value="formatMoney(data?.kpi.refundAmount ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.refundAmountChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="售后率"
          :value="(data?.kpi.afterSalesRate ?? 0).toFixed(2)"
          unit="%"
          :loading="loading"
          :trend="buildTrend(data?.change.afterSalesRateChange)"
          :value-color="afterSalesRateColor"
        />
      </a-col>
    </a-row>

    <!-- 类型分布 + 趋势 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="10">
        <a-card title="售后类型分布" class="after-sales-stats__card">
          <a-spin :spinning="loading">
            <ChartPie
              v-if="hasTypeData"
              :data="typePieData"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="暂无售后类型数据" />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="14">
        <a-card title="售后单量与退款金额趋势" class="after-sales-stats__card">
          <a-spin :spinning="loading">
            <ChartLine
              v-if="hasTrendData"
              :series="trendSeries"
              :x-axis="trendXAxis"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="暂无趋势数据" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- Top 10 高售后店铺 -->
    <a-card title="Top 10 高售后店铺" class="after-sales-stats__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="topShops.length > 0"
          :columns="topShopColumns"
          :data-source="topShops"
          :pagination="false"
          row-key="shopId"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'shopName'">
              <a class="after-sales-stats__link" @click="navigateToAuditLogs(record.shopId)">
                {{ record.shopName }}
              </a>
            </template>
            <template v-else-if="column.key === 'afterSalesRate'">
              <span :style="{ color: rateColor(computeShopRate(record)) }">
                {{ computeShopRate(record).toFixed(2) }}%
              </span>
            </template>
            <template v-else-if="column.key === 'avgProcessHours'">
              {{ record.avgProcessHours.toFixed(1) }} 小时
            </template>
          </template>
        </a-table>
        <EmptyState
          v-else-if="!loading"
          description="所选时间范围暂无售后数据"
          action-text="刷新"
          @action="loadData"
        />
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseAfterSalesStatsData,
  type AfterSalesStatsData,
  type TopShopByAfterSales,
  type DateRangeParams,
} from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<AfterSalesStatsData | null>(null)
const selectedTypes = ref<string[]>([])

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

// 售后类型选项（来自返回数据）
const typeOptions = computed(() =>
  (data.value?.typeDistribution ?? []).map((t) => ({ label: t.type, value: t.type }))
)

// 按选中类型过滤后的类型分布
const filteredTypeDistribution = computed(() => {
  if (!data.value) return []
  if (selectedTypes.value.length === 0) return data.value.typeDistribution
  return data.value.typeDistribution.filter((t) => selectedTypes.value.includes(t.type))
})

const hasTypeData = computed(() => filteredTypeDistribution.value.length > 0)
const hasTrendData = computed(() => !!data.value && data.value.dailyTrend.length > 0)

// 售后率颜色：>5% 红、3-5% 黄、<3% 绿
function rateColor(rate: number): string {
  if (rate > 5) return '#FF4D4F'
  if (rate >= 3) return '#FAAD14'
  return '#52C41A'
}

const afterSalesRateColor = computed(() => {
  const rate = data.value?.kpi.afterSalesRate ?? 0
  return rateColor(rate)
})

// 格式化金额：≥1 万显示万单位
function formatMoney(value: number): string {
  if (value >= 10000) return `¥${(value / 10000).toFixed(1)}万`
  return `¥${value.toLocaleString('zh-CN')}`
}

function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 类型饼图数据
const typePieData = computed(() =>
  filteredTypeDistribution.value.map((t) => ({ name: t.type, value: t.count }))
)

// 趋势图 X 轴：日期列表
const trendXAxis = computed(() => {
  if (!data.value) return []
  return data.value.dailyTrend.map((p) => p.date.slice(0, 10))
})

// 趋势图 series：售后单量 + 退款金额双系列
const trendSeries = computed(() => {
  if (!data.value) return []
  return [
    {
      name: '售后单量',
      type: 'line' as const,
      smooth: true,
      data: data.value.dailyTrend.map((p) => p.count),
    },
    {
      name: '退款金额',
      type: 'line' as const,
      smooth: true,
      data: data.value.dailyTrend.map((p) => p.refundAmount),
    },
  ]
})

// 计算单店售后率
function computeShopRate(record: TopShopByAfterSales): number {
  if (record.orderCount === 0) return 0
  return (record.afterSalesCount / record.orderCount) * 100
}

// Top 10 高售后店铺，按售后率倒序
const topShops = computed<TopShopByAfterSales[]>(() => {
  if (!data.value) return []
  return data.value.topShops
    .slice()
    .sort((a, b) => computeShopRate(b) - computeShopRate(a))
    .slice(0, 10)
})

// Top 10 表列定义
const topShopColumns = [
  { title: '店铺名', key: 'shopName' },
  { title: '售后单量', dataIndex: 'afterSalesCount', key: 'afterSalesCount', width: 120, sorter: (a: TopShopByAfterSales, b: TopShopByAfterSales) => b.afterSalesCount - a.afterSalesCount },
  { title: '订单量', dataIndex: 'orderCount', key: 'orderCount', width: 120 },
  { title: '售后率', key: 'afterSalesRate', width: 120, sorter: (a: TopShopByAfterSales, b: TopShopByAfterSales) => computeShopRate(b) - computeShopRate(a), defaultSortOrder: 'descend' as const },
  { title: '平均处理时长', key: 'avgProcessHours', width: 140 },
]

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getAfterSalesStats(params)
    data.value = parseAfterSalesStatsData(report)
    // 售后率 > 5% 触发 warning 通知
    if (data.value.kpi.afterSalesRate > 5) {
      notification.warning({
        message: '售后率异常',
        description: `当前售后率 ${data.value.kpi.afterSalesRate.toFixed(2)}%，超过 5% 阈值，请关注售后处理情况`,
      })
    }
  } catch {
    message.error('售后统计加载失败')
  } finally {
    loading.value = false
  }
}

function navigateToAuditLogs(shopId: string) {
  router.push({
    path: '/audit/audit-logs',
    query: { resourceType: 'AfterSales', keyword: shopId },
  })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
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
.after-sales-stats__link {
  color: #1677FF;
  cursor: pointer;
}
.after-sales-stats__link:hover {
  text-decoration: underline;
}
</style>
