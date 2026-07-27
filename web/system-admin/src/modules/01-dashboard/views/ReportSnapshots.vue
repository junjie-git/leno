<template>
  <div class="report-snapshots">
    <!-- 筛选条 -->
    <div class="report-snapshots__toolbar">
      <a-select
        v-model:value="reportType"
        style="width: 200px"
        :options="reportTypeOptions"
        placeholder="选择报表类型"
      />
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-button :loading="loading" @click="loadList">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
      <PermissionGuard permission="dashboard:export">
        <a-button :loading="exporting" @click="exportCsv">
          <template #icon><DownloadOutlined /></template>
          导出 CSV
        </a-button>
      </PermissionGuard>
    </div>

    <!-- 主列表 -->
    <a-card title="报表快照列表" class="report-snapshots__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="list.length > 0"
          :columns="listColumns"
          :data-source="list"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          row-key="ReportId"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'reportType'">
              {{ reportTypeLabel(record.ReportType) }}
            </template>
            <template v-else-if="column.key === 'periodStart'">
              {{ formatDate(record.PeriodStart) }}
            </template>
            <template v-else-if="column.key === 'periodEnd'">
              {{ formatDate(record.PeriodEnd) }}
            </template>
            <template v-else-if="column.key === 'dataVersion'">
              <a-tag color="blue">v{{ record.DataVersion ?? 0 }}</a-tag>
            </template>
            <template v-else-if="column.key === 'generatedAt'">
              {{ formatDateTime(record.GeneratedAt) }}
            </template>
            <template v-else-if="column.key === 'actions'">
              <a class="report-snapshots__link" @click="viewDetail(record)">
                <EyeOutlined /> 查看
              </a>
              <a-divider type="vertical" />
              <a
                class="report-snapshots__link"
                :class="{ 'report-snapshots__link--disabled': !findPreviousVersion(record) }"
                @click="compareVersion(record)"
              >
                <DiffOutlined /> 对比
              </a>
            </template>
          </template>
        </a-table>
        <EmptyState
          v-else-if="!loading"
          description="暂无快照记录"
          action-text="调整筛选条件"
          @action="loadList"
        />
      </a-spin>
    </a-card>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="报表快照详情"
      width="640"
      @after-open="focusFirstDescription"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item label="报表 ID">{{ detail.ReportId }}</a-descriptions-item>
            <a-descriptions-item label="报表类型">{{ reportTypeLabel(detail.ReportType) }}</a-descriptions-item>
            <a-descriptions-item label="周期起">{{ formatDateTime(detail.PeriodStart) }}</a-descriptions-item>
            <a-descriptions-item label="周期止">{{ formatDateTime(detail.PeriodEnd) }}</a-descriptions-item>
            <a-descriptions-item label="粒度">{{ granularityLabel(detail.Granularity) }}</a-descriptions-item>
            <a-descriptions-item label="数据版本">v{{ detail.DataVersion ?? 0 }}</a-descriptions-item>
            <a-descriptions-item label="生成时间">{{ formatDateTime(detail.GeneratedAt) }}</a-descriptions-item>
          </a-descriptions>

          <div class="report-snapshots__metrics-title">Metrics 指标</div>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item
              v-for="metric in detail.Metrics"
              :key="metric.Key"
              :label="metric.Key"
            >
              {{ formatMetricValue(metric.Value) }}
              <span v-if="metric.Unit" class="report-snapshots__unit">{{ metric.Unit }}</span>
            </a-descriptions-item>
          </a-descriptions>

          <!-- 版本对比 -->
          <div class="report-snapshots__compare-header">
            <span>与上一版本对比</span>
            <a-tooltip :title="compareSwitchTooltip">
              <a-switch
                v-model:checked="compareEnabled"
                :disabled="!previousVersionForDetail"
              />
            </a-tooltip>
          </div>
          <a-table
            v-if="compareEnabled && diffRows.length > 0"
            :columns="diffColumns"
            :data-source="diffRows"
            :pagination="false"
            row-key="key"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'changePercent'">
                <span :style="{ color: record.changePercent > 0 ? '#52C41A' : '#FF4D4F' }">
                  {{ formatChangePercent(record.changePercent) }}
                </span>
              </template>
            </template>
          </a-table>
          <a-empty
            v-else-if="compareEnabled"
            description="无差异指标"
            image="simple"
          />
        </template>
        <EmptyState v-else description="快照不存在或已归档" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  ReloadOutlined,
  DownloadOutlined,
  EyeOutlined,
  DiffOutlined,
} from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import { dashboardApi } from '../api/dashboard.api'
import type {
  DashboardReportDto,
  DashboardMetricDto,
  ReportType,
  Granularity,
  ReportListParams,
} from '../types/dashboard.dto'

const route = useRoute()
const loading = ref(false)
const exporting = ref(false)
const detailLoading = ref(false)
const list = ref<DashboardReportDto[]>([])
const detail = ref<DashboardReportDto | null>(null)
const drawerVisible = ref(false)
const compareEnabled = ref(false)

const reportTypeOptions: { label: string; value: ReportType }[] = [
  { label: '订单 GMV', value: 'OrderGmv' },
  { label: '支付成功率', value: 'PaymentSuccessRate' },
  { label: '积分发放量', value: 'PointsIssued' },
  { label: '通知送达率', value: 'NotificationDelivery' },
  { label: '售后量', value: 'AfterSalesVolume' },
  { label: '店铺排行', value: 'ShopRanking' },
]

// 初始化报表类型：优先从路由 query 读取
function initReportType(): ReportType {
  const query = route.query.reportType as string | undefined
  if (query && reportTypeOptions.some((o) => o.value === query)) {
    return query as ReportType
  }
  return 'OrderGmv'
}

const reportType = ref<ReportType>(initReportType())

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

// 报表类型中文标签映射
function reportTypeLabel(type: ReportType): string {
  const option = reportTypeOptions.find((o) => o.value === type)
  return option ? option.label : type
}

// 粒度中文标签映射
function granularityLabel(g: Granularity): string {
  const map: Record<Granularity, string> = {
    Hour: '小时',
    Day: '日',
    Week: '周',
    Month: '月',
  }
  return map[g] ?? g
}

function formatDate(iso: string): string {
  return dayjs(iso).format('YYYY-MM-DD')
}

function formatDateTime(iso: string): string {
  return dayjs(iso).format('YYYY-MM-DD HH:mm')
}

// 格式化 Metric 值：数字/字符串/数组/对象分别处理
function formatMetricValue(value: unknown): string {
  if (value === null || value === undefined) return '-'
  if (typeof value === 'number') return value.toLocaleString('zh-CN')
  if (typeof value === 'string') return value
  if (Array.isArray(value)) return `[${value.length} 项]`
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

// 列表列定义
const listColumns = [
  { title: '报表类型', key: 'reportType', width: 140 },
  { title: '周期起', key: 'periodStart', width: 120 },
  { title: '周期止', key: 'periodEnd', width: 120 },
  { title: '粒度', dataIndex: 'Granularity', key: 'granularity', width: 80 },
  { title: '数据版本', key: 'dataVersion', width: 100 },
  { title: '生成时间', key: 'generatedAt', width: 160 },
  { title: '操作', key: 'actions', width: 200, fixed: 'right' as const },
]

// 找到同周期前一版本（PeriodStart/PeriodEnd 相同且 DataVersion 较小的最近一个）
function findPreviousVersion(record: DashboardReportDto): DashboardReportDto | undefined {
  return list.value
    .filter(
      (r) =>
        r.PeriodStart === record.PeriodStart &&
        r.PeriodEnd === record.PeriodEnd &&
        r.ReportType === record.ReportType &&
        (r.DataVersion ?? 0) < (record.DataVersion ?? 0),
    )
    .sort((a, b) => (b.DataVersion ?? 0) - (a.DataVersion ?? 0))[0]
}

// 详情抽屉的「上一版本」（基于当前详情）
const previousVersionForDetail = computed<DashboardReportDto | null>(() => {
  if (!detail.value) return null
  return findPreviousVersion(detail.value) ?? null
})

const compareSwitchTooltip = computed(() => {
  if (previousVersionForDetail.value) return '开启后将显示与上一版本的差异'
  return '无历史版本可对比'
})

// 差异行：Key/旧值/新值/变化%
const diffColumns = [
  { title: '指标 Key', dataIndex: 'key', key: 'key' },
  { title: '旧值', dataIndex: 'oldValue', key: 'oldValue' },
  { title: '新值', dataIndex: 'newValue', key: 'newValue' },
  { title: '变化%', key: 'changePercent', width: 120 },
]

const diffRows = computed<{ key: string; oldValue: string; newValue: string; changePercent: number }[]>(() => {
  if (!detail.value || !previousVersionForDetail.value) return []
  const current = detail.value
  const previous = previousVersionForDetail.value
  const result: { key: string; oldValue: string; newValue: string; changePercent: number }[] = []
  const allKeys = new Set<string>([
    ...current.Metrics.map((m) => m.Key),
    ...previous.Metrics.map((m) => m.Key),
  ])
  for (const key of allKeys) {
    const curMetric = current.Metrics.find((m) => m.Key === key)
    const prevMetric = previous.Metrics.find((m) => m.Key === key)
    const curValue = extractNumber(curMetric)
    const prevValue = extractNumber(prevMetric)
    const changePercent = prevValue === 0 ? 0 : ((curValue - prevValue) / Math.abs(prevValue)) * 100
    result.push({
      key,
      oldValue: prevMetric ? formatMetricValue(prevMetric.Value) : '-',
      newValue: curMetric ? formatMetricValue(curMetric.Value) : '-',
      changePercent,
    })
  }
  return result
})

// 提取 Metric 的数值（仅对数值型返回数字，其他返回 0）
function extractNumber(metric: DashboardMetricDto | undefined): number {
  if (!metric) return 0
  if (typeof metric.Value === 'number') return metric.Value
  return 0
}

function formatChangePercent(percent: number): string {
  const sign = percent > 0 ? '+' : ''
  return `${sign}${percent.toFixed(1)}%`
}

async function loadList() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: ReportListParams = { start, end, reportType: reportType.value }
    const { data: reports } = await dashboardApi.getReports(params)
    list.value = reports
  } catch {
    message.error('报表快照列表加载失败')
  } finally {
    loading.value = false
  }
}

async function viewDetail(record: DashboardReportDto) {
  drawerVisible.value = true
  detailLoading.value = true
  detail.value = null
  compareEnabled.value = false
  try {
    const { data: report } = await dashboardApi.getReport(record.ReportId)
    detail.value = report
  } catch {
    message.error('快照不存在或已归档')
  } finally {
    detailLoading.value = false
  }
}

function compareVersion(record: DashboardReportDto) {
  const previous = findPreviousVersion(record)
  if (!previous) {
    message.info('无历史版本可对比')
    return
  }
  void viewDetail(record)
  // 详情加载完成后自动开启对比开关
  setTimeout(() => {
    compareEnabled.value = true
  }, 300)
}

// 抽屉打开后聚焦首个描述项（可访问性）
function focusFirstDescription() {
  // descriptions 实例可能未挂载 ref，此处仅作焦点尝试
  setTimeout(() => {
    const el = document.querySelector('.report-snapshots .ant-drawer-body .ant-descriptions-item-content')
    if (el instanceof HTMLElement) el.focus()
  }, 100)
}

// 导出 CSV：基于列表数据生成并下载
async function exportCsv() {
  exporting.value = true
  try {
    const [start, end] = dateRange.value
    const params: ReportListParams = { start, end, reportType: reportType.value }
    const { data: reports } = await dashboardApi.getReports(params)
    const headers = ['ReportId', 'ReportType', 'PeriodStart', 'PeriodEnd', 'Granularity', 'DataVersion', 'GeneratedAt']
    const rows = reports.map((r) => [
      r.ReportId,
      r.ReportType,
      r.PeriodStart,
      r.PeriodEnd,
      r.Granularity,
      String(r.DataVersion ?? 0),
      r.GeneratedAt,
    ])
    const csv = [headers, ...rows]
      .map((row) => row.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(','))
      .join('\n')
    // 添加 BOM 头以兼容 Excel 中文
    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `reports-${reportType.value}-${dayjs().format('YYYYMMDD-HHmmss')}.csv`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    message.success(`已导出 ${reports.length} 条快照`)
  } catch {
    message.error('CSV 导出失败')
  } finally {
    exporting.value = false
  }
}

watch(dateRange, () => loadList())
watch(reportType, () => loadList())

onMounted(() => loadList())
</script>

<style scoped>
.report-snapshots {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.report-snapshots__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.report-snapshots__card {
  border-radius: 8px;
}
.report-snapshots__link {
  color: #1677FF;
  cursor: pointer;
}
.report-snapshots__link:hover {
  text-decoration: underline;
}
.report-snapshots__link--disabled {
  color: #BFBFBF;
  cursor: not-allowed;
}
.report-snapshots__metrics-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #000000D9;
}
.report-snapshots__compare-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #000000D9;
}
.report-snapshots__unit {
  margin-left: 4px;
  color: #8C8C8C;
  font-size: 12px;
}
</style>
