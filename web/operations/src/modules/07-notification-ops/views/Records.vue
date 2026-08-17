<!-- web/operations/src/modules/07-notification-ops/views/Records.vue -->
<template>
  <div class="notification-records">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="用户 ID">
          <a-input
            v-model:value="filters.userId"
            placeholder="接收用户 ID"
            allow-clear
            style="width: 180px"
          />
        </a-form-item>
        <a-form-item label="渠道">
          <a-select
            v-model:value="filters.channel"
            placeholder="全部渠道"
            allow-clear
            style="width: 120px"
            :options="channelOptions"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 120px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="模板编码">
          <a-input
            v-model:value="filters.templateCode"
            placeholder="如 ORDER_PAID"
            allow-clear
            style="width: 160px"
          />
        </a-form-item>
        <a-form-item label="业务引用">
          <a-input
            v-model:value="filters.businessRef"
            placeholder="如订单号"
            allow-clear
            style="width: 160px"
          />
        </a-form-item>
        <a-form-item label="时间范围">
          <DateTimeRangePicker :value="timeRange" show-time @change="onTimeRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：统计概览（各状态计数 + 送达率） -->
    <div class="stats-row">
      <StatisticCard title="已送达" :value="statistics?.deliveredCount ?? 0" status="success" :loading="statsLoading" />
      <StatisticCard title="已发送" :value="statistics?.sentCount ?? 0" status="default" :loading="statsLoading" />
      <StatisticCard title="失败" :value="statistics?.failedCount ?? 0" status="warning" :loading="statsLoading" />
      <StatisticCard title="死信" :value="statistics?.deadLetterCount ?? 0" status="danger" :loading="statsLoading" />
      <StatisticCard
        title="送达率"
        :value="statistics ? formatPercent(statistics.deliveryRate, { decimals: 1 }) : '-'"
        status="default"
        :loading="statsLoading"
      />
      <a-card :bordered="true" size="small" class="stats-action-card">
        <div class="stats-actions">
          <IdempotencyButton :loading="loading" @click="refreshAll">刷新</IdempotencyButton>
          <a-button @click="onExportCsv">导出 CSV</a-button>
        </div>
      </a-card>
    </div>

    <!-- 区域 C：通知记录表格 -->
    <a-card :bordered="false" class="table-card">
      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="refreshAll" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: NotificationRecordDto) => record.id"
        :row-class-name="rowClassName"
        :scroll="{ x: 1320 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState
            :description="hasActiveFilters ? '筛选条件下暂无记录' : '暂无通知记录'"
            :action-text="hasActiveFilters ? '清空筛选条件' : undefined"
            @action="onReset"
          />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'recipient'">
            <div class="recipient-cell">
              <span>{{ record.recipient || record.userId }}</span>
              <span class="sub-text">{{ record.userId }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'channel'">
            <a-tag :color="NOTIFICATION_CHANNEL_META[record.channel].color">
              {{ NOTIFICATION_CHANNEL_META[record.channel].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'templateCode'">
            <a class="code-link" :aria-label="record.templateCode" @click="goTemplates(record.templateCode)">{{ record.templateCode }}</a>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="NOTIFICATION_STATUS_META[record.status].color" :aria-label="NOTIFICATION_STATUS_META[record.status].label">
              {{ NOTIFICATION_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'businessRef'">
            <a v-if="record.businessRef" @click="goDeadLetters(record)">{{ record.businessRef }}</a>
            <span v-else>—</span>
          </template>
          <template v-else-if="column.key === 'sentAt'">{{ formatDateTime(record.sentAt) }}</template>
          <template v-else-if="column.key === 'deliveredAt'">{{ formatDateTime(record.deliveredAt) }}</template>
          <template v-else-if="column.key === 'retryCount'">
            <span class="retry-count" :class="{ over: record.retryCount > 3 }" :aria-label="`重试 ${record.retryCount} 次`">
              {{ record.retryCount }}
            </span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看详情" @click="onViewDetail(record)">详情</a-button>
              <a-button
                v-if="record.status === 'DeadLetter'"
                type="link"
                size="small"
                aria-label="重发死信"
                @click="onResend(record)"
              >
                重发
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉（渲染正文 + 渠道返回 + 状态时间线） -->
    <a-drawer
      v-model:open="drawerOpen"
      title="通知记录详情"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detailError">
          <EmptyState :description="`详情加载失败：${detailError}`" action-text="重试" @action="loadDetail" />
        </template>
        <template v-else-if="detail">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="接收人">{{ detail.recipient || detail.userId }}</a-descriptions-item>
            <a-descriptions-item label="用户 ID">{{ detail.userId }}</a-descriptions-item>
            <a-descriptions-item label="渠道">
              <a-tag :color="NOTIFICATION_CHANNEL_META[detail.channel].color">
                {{ NOTIFICATION_CHANNEL_META[detail.channel].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="NOTIFICATION_STATUS_META[detail.status].color">
                {{ NOTIFICATION_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="模板编码">
              <a class="code-link" @click="goTemplates(detail.templateCode)">{{ detail.templateCode }}</a>
            </a-descriptions-item>
            <a-descriptions-item label="业务引用">{{ detail.businessRef || '—' }}</a-descriptions-item>
            <a-descriptions-item label="重试次数">
              <span class="retry-count" :class="{ over: detail.retryCount > 3 }">{{ detail.retryCount }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="记录 ID">{{ detail.id }}</a-descriptions-item>
            <a-descriptions-item label="发送时间">{{ formatDateTime(detail.sentAt) }}</a-descriptions-item>
            <a-descriptions-item label="送达时间">{{ formatDateTime(detail.deliveredAt) }}</a-descriptions-item>
            <a-descriptions-item label="创建时间" :span="2">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
          </a-descriptions>

          <h3 class="drawer-section-title">渲染后内容</h3>
          <div class="rendered-block">
            <div class="rendered-title">{{ detail.title || '（无标题）' }}</div>
            <div class="rendered-body">{{ detail.content || '（无正文）' }}</div>
          </div>

          <h3 class="drawer-section-title">渠道返回结果</h3>
          <JsonViewer v-if="detail.providerResponse != null" :data="detail.providerResponse" :max-height="220" />
          <div v-else class="no-data-text">渠道未返回数据</div>

          <h3 class="drawer-section-title">状态时间线</h3>
          <a-timeline class="record-timeline">
            <a-timeline-item v-for="(node, index) in recordTimeline" :key="index" :color="node.color">
              {{ node.children }}
            </a-timeline-item>
          </a-timeline>
        </template>
        <div v-else class="no-data-text">加载中…</div>
      </a-spin>
    </a-drawer>

    <!-- 死信重发二次确认 -->
    <ConfirmDialog
      :open="resendConfirmOpen"
      title="重发死信通知"
      :content="`确认重发该条死信通知？重发后状态重置为待发送，将触发实际发送并可能产生渠道费用。`"
      @confirm="onConfirmResend"
      @cancel="resendConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, DateTimeRangePicker, EmptyState, IdempotencyButton, JsonViewer, StatisticCard } from '@/shared/components'
import { formatDateTime, formatPercent } from '@/shared/utils/format'
import { recordApi } from '../api/record.api'
import { NOTIFICATION_CHANNELS, NOTIFICATION_CHANNEL_META } from '../types/template.dto'
import type { NotificationChannel } from '../types/template.dto'
import { NOTIFICATION_STATUS_META } from '../types/record.dto'
import type {
  NotificationRecordDto,
  NotificationStatisticsDto,
  NotificationStatus,
  NotificationStatusTransitionDto,
} from '../types/record.dto'

/**
 * 通知记录页（07-notification-ops）
 *
 * 多维度筛选 + 状态统计概览（送达率）+ 记录表格 + 详情抽屉（渲染正文 / 渠道返回 / 时间线）。
 * - 默认查询近 7 天记录；支持模板编码 / 状态跳转参数
 * - 死信记录支持单个重发（ConfirmDialog 强制确认）
 * - 重试次数 > 3 行标红提示
 */

const route = useRoute()
const router = useRouter()

const channelOptions = NOTIFICATION_CHANNELS.map((value) => ({
  label: NOTIFICATION_CHANNEL_META[value].label,
  value,
}))

const statusOptions = (Object.keys(NOTIFICATION_STATUS_META) as NotificationStatus[]).map((value) => ({
  label: NOTIFICATION_STATUS_META[value].label,
  value,
}))

// ---------- 筛选 ----------
interface FilterState {
  userId: string
  channel?: NotificationChannel
  status?: NotificationStatus
  templateCode: string
  businessRef: string
}

const filters = reactive<FilterState>({
  userId: '',
  channel: undefined,
  status: undefined,
  templateCode: '',
  businessRef: '',
})

const timeRange = ref<[string, string] | undefined>(undefined)

const hasActiveFilters = computed(
  () =>
    Boolean(
      filters.userId ||
        filters.channel ||
        filters.status ||
        filters.templateCode ||
        filters.businessRef,
    ) || Boolean(timeRange.value),
)

function onTimeRangeChange(value: [string, string]) {
  timeRange.value = value
}

/** 默认时间范围：近 7 天 */
function defaultTimeRange(): [string, string] {
  const now = new Date()
  const from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000)
  return [from.toISOString(), now.toISOString()]
}

// ---------- 列表 ----------
const tableData = ref<NotificationRecordDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns: TableColumnsType = [
  { title: '接收人', key: 'recipient', width: 180, ellipsis: true },
  { title: '渠道', key: 'channel', width: 90 },
  { title: '模板编码', key: 'templateCode', width: 170 },
  { title: '状态', key: 'status', width: 100 },
  { title: '业务引用', key: 'businessRef', width: 150, ellipsis: true },
  { title: '发送时间', key: 'sentAt', width: 170 },
  { title: '送达时间', key: 'deliveredAt', width: 170 },
  { title: '重试', key: 'retryCount', width: 80, align: 'center' },
  { title: '操作', key: 'action', width: 130, fixed: 'right' },
]

function rowClassName(record: NotificationRecordDto): string {
  return record.retryCount > 3 ? 'retry-over-row' : ''
}

async function fetchRecords() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof recordApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const userId = filters.userId.trim()
    const templateCode = filters.templateCode.trim()
    const businessRef = filters.businessRef.trim()
    if (userId) params.userId = userId
    if (filters.channel) params.channel = filters.channel
    if (filters.status) params.status = filters.status
    if (templateCode) params.templateCode = templateCode
    if (businessRef) params.businessRef = businessRef
    if (timeRange.value) {
      params.fromTime = timeRange.value[0]
      params.toTime = timeRange.value[1]
    }

    const { data } = await recordApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载通知记录失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

// ---------- 统计 ----------
const statistics = ref<NotificationStatisticsDto | null>(null)
const statsLoading = ref(false)

async function fetchStatistics() {
  statsLoading.value = true
  try {
    const { data } = await recordApi.statistics()
    statistics.value = data
  } catch {
    statistics.value = null
  } finally {
    statsLoading.value = false
  }
}

function refreshAll() {
  void fetchRecords()
  void fetchStatistics()
}

function onQuery() {
  pagination.current = 1
  void fetchRecords()
}

function onReset() {
  filters.userId = ''
  filters.channel = undefined
  filters.status = undefined
  filters.templateCode = ''
  filters.businessRef = ''
  timeRange.value = defaultTimeRange()
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchRecords()
}

// ---------- 跨页面流转 ----------
function goTemplates(templateCode: string) {
  void router.push({ path: '/notification-ops/templates', query: { keyword: templateCode } })
}

function goDeadLetters(record: NotificationRecordDto) {
  void router.push({
    path: '/notification-ops/dead-letters',
    query: { templateCode: record.templateCode, channel: record.channel },
  })
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<NotificationRecordDto | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
const detailId = ref('')

async function loadDetail() {
  if (!detailId.value) return
  detailLoading.value = true
  detailError.value = ''
  try {
    const { data } = await recordApi.detail(detailId.value)
    detail.value = data
  } catch (e) {
    detailError.value = e instanceof Error ? e.message : '加载详情失败'
    detail.value = null
  } finally {
    detailLoading.value = false
  }
}

function onViewDetail(record: NotificationRecordDto) {
  detailId.value = record.id
  detail.value = null
  drawerOpen.value = true
  void loadDetail()
}

/** 时间线：优先后端 timeline；缺失时按基础字段合成 */
function buildTimeline(record: NotificationRecordDto): { color: string; children: string }[] {
  const colorOf = (status: string): string => {
    const meta = NOTIFICATION_STATUS_META[status as NotificationStatus]
    if (meta?.color === 'success') return 'green'
    if (meta?.color === 'error') return 'red'
    if (meta?.color === 'warning') return 'orange'
    if (meta?.color === 'processing' || meta?.color === 'blue') return 'blue'
    return 'gray'
  }
  const labelOf = (status: string): string => {
    const meta = NOTIFICATION_STATUS_META[status as NotificationStatus]
    return meta?.label ?? status
  }

  if (record.timeline?.length) {
    return record.timeline.map((node: NotificationStatusTransitionDto) => ({
      color: colorOf(String(node.status)),
      children: `${labelOf(String(node.status))} · ${formatDateTime(node.at)}${node.detail ? `：${node.detail}` : ''}`,
    }))
  }

  const items: { color: string; children: string }[] = []
  if (record.createdAt) items.push({ color: 'gray', children: `创建 · ${formatDateTime(record.createdAt)}` })
  if (record.sentAt) items.push({ color: 'blue', children: `已发送 · ${formatDateTime(record.sentAt)}` })
  if (record.deliveredAt) items.push({ color: 'green', children: `已送达 · ${formatDateTime(record.deliveredAt)}` })
  if (record.status === 'DeadLetter') {
    items.push({
      color: 'red',
      children: `进入死信 · 重试 ${record.retryCount} 次后仍失败`,
    })
  }
  if (items.length === 0) {
    items.push({ color: 'gray', children: `当前状态：${labelOf(record.status)}` })
  }
  return items.reverse()
}

const recordTimeline = computed(() => (detail.value ? buildTimeline(detail.value) : []))

// ---------- 死信重发 ----------
const resendConfirmOpen = ref(false)
const resendTarget = ref<NotificationRecordDto | null>(null)
const resending = ref(false)

function onResend(record: NotificationRecordDto) {
  resendTarget.value = record
  resendConfirmOpen.value = true
}

async function onConfirmResend() {
  const target = resendTarget.value
  resendConfirmOpen.value = false
  if (!target) return
  resending.value = true
  try {
    await recordApi.resend(target.id)
    message.success('已重发，记录状态重置为待发送')
    refreshAll()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('记录状态已变更，请刷新')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '重发失败，请重试')
    }
  } finally {
    resending.value = false
    resendTarget.value = null
  }
}

// ---------- CSV 导出（当前筛选页数据，前端生成） ----------
function csvEscape(value: string): string {
  const escaped = value.replace(/"/g, '""')
  return /[",\n]/.test(escaped) ? `"${escaped}"` : escaped
}

function onExportCsv() {
  if (tableData.value.length === 0) {
    message.warning('当前页无数据可导出')
    return
  }

  const header = ['记录ID', '接收人', '用户ID', '渠道', '模板编码', '状态', '业务引用', '发送时间', '送达时间', '重试次数']
  const rows = tableData.value.map((r) => [
    r.id,
    r.recipient,
    r.userId,
    NOTIFICATION_CHANNEL_META[r.channel].label,
    r.templateCode,
    NOTIFICATION_STATUS_META[r.status].label,
    r.businessRef ?? '',
    formatDateTime(r.sentAt),
    formatDateTime(r.deliveredAt),
    String(r.retryCount),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `通知记录导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化 ----------
onMounted(() => {
  // 支持通知模板页「模板编码」跳转与其他页死信筛选跳转
  const queryTemplateCode = typeof route.query.templateCode === 'string' ? route.query.templateCode : ''
  if (queryTemplateCode) filters.templateCode = queryTemplateCode
  const queryStatus = typeof route.query.status === 'string' ? route.query.status : ''
  if (queryStatus && queryStatus in NOTIFICATION_STATUS_META) {
    filters.status = queryStatus as NotificationStatus
  }
  const queryChannel = typeof route.query.channel === 'string' ? route.query.channel : ''
  if (queryChannel && NOTIFICATION_CHANNELS.includes(queryChannel as NotificationChannel)) {
    filters.channel = queryChannel as NotificationChannel
  }

  timeRange.value = defaultTimeRange()
  refreshAll()
})
</script>

<style scoped>
.notification-records {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}

.filter-form {
  flex-wrap: wrap;
  row-gap: 8px;
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 16px;
}

.stats-action-card :deep(.ant-card-body) {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 12px;
}

.stats-actions {
  display: flex;
  gap: 8px;
}

.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.recipient-cell {
  display: flex;
  flex-direction: column;
}

.sub-text {
  font-size: 12px;
  color: #8c8c8c;
}

.code-link {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 13px;
}

.retry-count {
  font-size: 12px;
  color: #595959;
}

.retry-count.over {
  color: #ff4d4f;
  font-weight: 600;
}

.table-card :deep(.retry-over-row) {
  background: #fff1f0;
}

.drawer-section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.rendered-block {
  padding: 12px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.rendered-title {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  margin-bottom: 8px;
}

.rendered-body {
  font-size: 13px;
  color: #595959;
  white-space: pre-wrap;
  word-break: break-all;
}

.no-data-text {
  font-size: 12px;
  color: #8c8c8c;
}

.record-timeline {
  margin-top: 8px;
}
</style>
