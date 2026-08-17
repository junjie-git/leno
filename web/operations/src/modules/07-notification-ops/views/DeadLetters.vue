<!-- web/operations/src/modules/07-notification-ops/views/DeadLetters.vue -->
<template>
  <div class="dead-letters">
    <!-- 区域 A：统计概览 -->
    <div class="stats-row">
      <StatisticCard title="死信总数" :value="stats.total" status="danger" :loading="statsLoading" />
      <StatisticCard title="近 7 天新增" :value="stats.last7Days" status="warning" :loading="statsLoading" />
      <StatisticCard title="待处理数" :value="stats.total" status="default" :loading="statsLoading" />
      <StatisticCard title="本月已丢弃" value="—" status="default" :loading="statsLoading" />
    </div>

    <!-- 区域 C：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="渠道">
          <a-select
            v-model:value="filters.channel"
            placeholder="全部渠道"
            allow-clear
            style="width: 130px"
            :options="channelOptions"
          />
        </a-form-item>
        <a-form-item label="模板编码">
          <a-input
            v-model:value="filters.templateCode"
            placeholder="如 ORDER_PAID"
            allow-clear
            style="width: 180px"
          />
        </a-form-item>
        <a-form-item label="失败时间">
          <DateTimeRangePicker :value="timeRange" show-time @change="onTimeRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + D：工具栏与死信表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <a-space>
          <IdempotencyButton
            type="primary"
            :disabled="selectedRowKeys.length === 0"
            :loading="resending"
            :aria-label="`批量重发 ${selectedRowKeys.length} 条死信`"
            @click="onBatchResend"
          >
            <RedoOutlined /> 批量重发
          </IdempotencyButton>
          <IdempotencyButton
            danger
            :disabled="selectedRowKeys.length === 0"
            :loading="discarding"
            :aria-label="`批量丢弃 ${selectedRowKeys.length} 条死信`"
            @click="onBatchDiscard"
          >
            <DeleteOutlined /> 批量丢弃
          </IdempotencyButton>
          <a-tooltip v-if="selectedRowKeys.length === 0" title="请先选择死信记录">
            <span class="selection-hint">未选中记录</span>
          </a-tooltip>
          <span v-else class="selection-hint">已选 {{ selectedRowKeys.length }} 条（单次最多 100 条）</span>
        </a-space>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="refreshAll">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`死信列表加载失败：${errorMessage}`" action-text="重试" @action="refreshAll" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: DeadLetterRecordDto) => record.recordId"
        :row-selection="rowSelection"
        :scroll="{ x: 1480 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState
            description="暂无死信记录"
            :action-text="hasActiveFilters ? '清空筛选条件' : undefined"
            @action="onReset"
          />
          <div class="empty-sub">所有通知均已成功投递或正在重试</div>
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
          <template v-else-if="column.key === 'title'">
            <span class="dead-title" :title="record.title">{{ record.title || '—' }}</span>
          </template>
          <template v-else-if="column.key === 'retryCount'">
            <span class="retry-count" :class="{ over: record.retryCount >= 3 }" :aria-label="`重试 ${record.retryCount} 次`">
              {{ record.retryCount }}
            </span>
          </template>
          <template v-else-if="column.key === 'errorCode'">
            <span class="error-code">{{ record.errorCode || '—' }}</span>
          </template>
          <template v-else-if="column.key === 'errorMessage'">
            <span class="error-message" :title="record.errorMessage">{{ record.errorMessage || '—' }}</span>
          </template>
          <template v-else-if="column.key === 'failedAt'">{{ formatDateTime(record.failedAt) }}</template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" aria-label="查看详情" @click="onViewDetail(record)">详情</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 E：详情抽屉（含失败链路与重试历史） -->
    <a-drawer
      v-model:open="drawerOpen"
      title="死信记录详情"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <template v-if="detail">
        <a-descriptions :column="2" bordered size="small">
          <a-descriptions-item label="接收人">{{ detail.recipient || detail.userId }}</a-descriptions-item>
          <a-descriptions-item label="用户 ID">{{ detail.userId }}</a-descriptions-item>
          <a-descriptions-item label="渠道">
            <a-tag :color="NOTIFICATION_CHANNEL_META[detail.channel].color">
              {{ NOTIFICATION_CHANNEL_META[detail.channel].label }}
            </a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag color="error">死信</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="模板编码">
            <a class="code-link" @click="goTemplates(detail.templateCode)">{{ detail.templateCode }}</a>
          </a-descriptions-item>
          <a-descriptions-item label="重试次数">
            <span class="retry-count" :class="{ over: detail.retryCount >= 3 }">{{ detail.retryCount }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="最后失败时间">{{ formatDateTime(detail.failedAt) }}</a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
          <a-descriptions-item label="错误码" :span="2">
            <span class="error-code">{{ detail.errorCode || '—' }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="错误消息" :span="2">{{ detail.errorMessage || '—' }}</a-descriptions-item>
        </a-descriptions>

        <h3 class="drawer-section-title">渲染后内容</h3>
        <div class="rendered-block">
          <div class="rendered-title">{{ detail.title || '（无标题）' }}</div>
          <div class="rendered-body">{{ detail.content || '（无正文）' }}</div>
        </div>

        <h3 class="drawer-section-title">重试历史</h3>
        <a-timeline class="dead-timeline">
          <a-timeline-item v-for="(node, index) in retryTimeline" :key="index" :color="node.color">
            {{ node.children }}
          </a-timeline-item>
        </a-timeline>

        <h3 class="drawer-section-title">状态时间线</h3>
        <a-timeline class="dead-timeline">
          <a-timeline-item v-for="(node, index) in statusTimeline" :key="index" :color="node.color">
            {{ node.children }}
          </a-timeline-item>
        </a-timeline>
      </template>
    </a-drawer>

    <!-- 批量重发确认 -->
    <ConfirmDialog
      :open="resendConfirmOpen"
      title="确认批量重发"
      :content="`将触发 ${selectedRowKeys.length} 条死信重新发送，可能产生渠道费用。确认重发？`"
      @confirm="onConfirmResend"
      @cancel="resendConfirmOpen = false"
    />

    <!-- 批量丢弃确认（强制填写 ≥10 字符原因） -->
    <ConfirmDialog
      :open="discardConfirmOpen"
      danger
      title="确认批量丢弃"
      :content="`丢弃后 ${selectedRowKeys.length} 条死信将不再重发并进入终态（不可恢复），请确认已人工排查原因。`"
      :require-input="{ label: '丢弃原因', min: 10, max: 200 }"
      @confirm="onConfirmDiscard"
      @cancel="discardConfirmOpen = false"
    />

    <!-- 批量操作结果反馈（部分失败展示清单） -->
    <a-modal v-model:open="resultOpen" :title="resultTitle" :footer="null" width="520">
      <template v-if="batchResult">
        <a-alert
          v-if="batchResult.failureCount === 0"
          type="success"
          show-icon
          :message="`全部成功：${batchResult.successCount} 条`"
        />
        <a-alert
          v-else
          type="warning"
          show-icon
          :message="`部分成功：成功 ${batchResult.successCount} 条，失败 ${batchResult.failureCount} 条`"
        />
        <div v-if="batchResult.errors.length > 0" class="batch-failures">
          <div class="batch-failures-title">失败明细</div>
          <ul class="batch-failures-list">
            <li v-for="(reason, index) in batchResult.errors" :key="index">
              <span class="sub-text">{{ failedRecordLabels[index] ?? '' }}</span>
              <span>{{ reason }}</span>
            </li>
          </ul>
        </div>
        <div class="result-tip">失败记录已保留选中态，可调整后再次操作。</div>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { DeleteOutlined, RedoOutlined } from '@ant-design/icons-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, DateTimeRangePicker, EmptyState, IdempotencyButton, StatisticCard } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { deadLetterApi } from '../api/deadLetter.api'
import { NOTIFICATION_CHANNELS, NOTIFICATION_CHANNEL_META } from '../types/template.dto'
import type { NotificationChannel } from '../types/template.dto'
import type { NotificationStatusTransitionDto } from '../types/record.dto'
import type {
  DeadLetterRecordDto,
  DeadLetterRetryAttemptDto,
  NotificationBatchResultDto,
} from '../types/dead-letter.dto'

/**
 * 死信管理页（07-notification-ops）
 *
 * 统计概览 + 筛选 + 多选表格 + 批量重发 / 丢弃 + 详情抽屉。
 * - 列表固定 Status=DeadLetter（后端过滤，前端不传 status）
 * - 批量操作单次最多 100 条；丢弃原因必填 ≥10 字符
 * - 部分失败展示失败清单并保留失败记录选中态（重发成功的记录自动退出死信视图）
 */

const MAX_BATCH_SIZE = 100

const route = useRoute()
const router = useRouter()

const channelOptions = NOTIFICATION_CHANNELS.map((value) => ({
  label: NOTIFICATION_CHANNEL_META[value].label,
  value,
}))

// ---------- 筛选 ----------
interface FilterState {
  channel?: NotificationChannel
  templateCode: string
}

const filters = reactive<FilterState>({
  channel: undefined,
  templateCode: '',
})

const timeRange = ref<[string, string] | undefined>(undefined)

const hasActiveFilters = computed(
  () => Boolean(filters.channel || filters.templateCode) || Boolean(timeRange.value),
)

function onTimeRangeChange(value: [string, string]) {
  timeRange.value = value
}

// ---------- 列表 ----------
const tableData = ref<DeadLetterRecordDto[]>([])
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
  { title: '标题', key: 'title', width: 170, ellipsis: true },
  { title: '重试', key: 'retryCount', width: 80, align: 'center' },
  { title: '错误码', key: 'errorCode', width: 120 },
  { title: '错误消息', key: 'errorMessage', width: 200, ellipsis: true },
  { title: '失败时间', key: 'failedAt', width: 170 },
  { title: '操作', key: 'action', width: 90, fixed: 'right' },
]

function buildListParams() {
  const params: Parameters<typeof deadLetterApi.list>[0] = {
    page: pagination.current,
    pageSize: pagination.pageSize,
  }
  if (filters.channel) params.channel = filters.channel
  const templateCode = filters.templateCode.trim()
  if (templateCode) params.templateCode = templateCode
  if (timeRange.value) {
    params.fromTime = timeRange.value[0]
    params.toTime = timeRange.value[1]
  }
  return params
}

async function fetchList() {
  loading.value = true
  errorMessage.value = ''
  try {
    const { data } = await deadLetterApi.list(buildListParams())
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '死信列表加载失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchList()
}

function onReset() {
  filters.channel = undefined
  filters.templateCode = ''
  timeRange.value = undefined
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchList()
}

// ---------- 统计概览 ----------
const stats = reactive({ total: 0, last7Days: 0 })
const statsLoading = ref(false)

async function fetchStats() {
  statsLoading.value = true
  try {
    const [allResult, recentResult] = await Promise.all([
      deadLetterApi.list({ page: 1, pageSize: 1 }),
      deadLetterApi.list({
        page: 1,
        pageSize: 1,
        fromTime: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
      }),
    ])
    stats.total = allResult.data.total
    stats.last7Days = recentResult.data.total
  } catch {
    stats.total = 0
    stats.last7Days = 0
  } finally {
    statsLoading.value = false
  }
}

function refreshAll() {
  void fetchList()
  void fetchStats()
}

// ---------- 多选 ----------
const selectedRowKeys = ref<string[]>([])

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys.map(String)
}

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: onSelectChange,
}))

/** 批量操作后刷新：成功记录离开死信视图，失败记录保留选中态便于二次操作 */
function retainFailedSelection() {
  const remaining = new Set(tableData.value.map((r) => r.recordId))
  selectedRowKeys.value = selectedRowKeys.value.filter((id) => remaining.has(id))
}

function goTemplates(templateCode: string) {
  void router.push({ path: '/notification-ops/templates', query: { keyword: templateCode } })
}

// ---------- 批量重发 ----------
const resendConfirmOpen = ref(false)
const resending = ref(false)

function checkBatchSize(): boolean {
  if (selectedRowKeys.value.length > MAX_BATCH_SIZE) {
    message.warning(`单次最多 ${MAX_BATCH_SIZE} 条，请调整选择`)
    return false
  }
  return true
}

function onBatchResend() {
  if (selectedRowKeys.value.length === 0 || !checkBatchSize()) return
  resendConfirmOpen.value = true
}

async function onConfirmResend() {
  resendConfirmOpen.value = false
  resending.value = true
  try {
    const { data } = await deadLetterApi.batchResend({ recordIds: [...selectedRowKeys.value] })
    await fetchList()
    retainFailedSelection()
    await fetchStats()
    showBatchResult(data, '批量重发')
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('部分记录状态已变更（非死信状态），请刷新后重试')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '批量重发失败，请重试')
    }
  } finally {
    resending.value = false
  }
}

// ---------- 批量丢弃 ----------
const discardConfirmOpen = ref(false)
const discarding = ref(false)

function onBatchDiscard() {
  if (selectedRowKeys.value.length === 0 || !checkBatchSize()) return
  discardConfirmOpen.value = true
}

async function onConfirmDiscard(reason?: string) {
  discardConfirmOpen.value = false
  discarding.value = true
  try {
    const { data } = await deadLetterApi.batchDiscard({
      recordIds: [...selectedRowKeys.value],
      discardReason: reason ?? '',
    })
    await fetchList()
    retainFailedSelection()
    await fetchStats()
    showBatchResult(data, '批量丢弃')
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('部分记录状态已变更（非死信状态），请刷新后重试')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '批量丢弃失败，请重试')
    }
  } finally {
    discarding.value = false
  }
}

// ---------- 批量结果反馈 ----------
const resultOpen = ref(false)
const resultTitle = ref('批量操作结果')
const batchResult = ref<NotificationBatchResultDto | null>(null)
const failedRecordLabels = ref<string[]>([])

function showBatchResult(result: NotificationBatchResultDto, action: string) {
  batchResult.value = result
  resultTitle.value = `${action}结果`
  const selected = new Set(selectedRowKeys.value)
  failedRecordLabels.value = tableData.value
    .filter((r) => selected.has(r.recordId))
    .map((r) => `${r.templateCode} · ${r.recipient || r.userId}`)
  if (result.failureCount === 0) {
    message.success(`${action}全部成功：${result.successCount} 条`)
    selectedRowKeys.value = []
  } else {
    message.warning(`成功 ${result.successCount} 条，失败 ${result.failureCount} 条`)
  }
  resultOpen.value = true
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<DeadLetterRecordDto | null>(null)

function onViewDetail(record: DeadLetterRecordDto) {
  detail.value = record
  drawerOpen.value = true
}

/** 重试历史：优先后端 retryHistory；缺失时按基础字段合成最后一次失败节点 */
const retryTimeline = computed<{ color: string; children: string }[]>(() => {
  const record = detail.value
  if (!record) return []
  const history: DeadLetterRetryAttemptDto[] = record.retryHistory?.length
    ? record.retryHistory
    : [
        {
          attemptNo: Math.max(record.retryCount, 1),
          at: record.failedAt,
          errorCode: record.errorCode,
          errorMessage: record.errorMessage,
        },
      ]
  return history
    .slice()
    .sort((a, b) => b.attemptNo - a.attemptNo)
    .map((node) => ({
      color: 'red',
      children: `第 ${node.attemptNo} 次重试失败 · ${formatDateTime(node.at)}${
        node.errorCode ? ` · ${node.errorCode}` : ''
      }${node.errorMessage ? `：${node.errorMessage}` : ''}`,
    }))
})

/** 状态时间线：优先后端 timeline；缺失时按创建 → 死信合成 */
const statusTimeline = computed<{ color: string; children: string }[]>(() => {
  const record = detail.value
  if (!record) return []
  if (record.timeline?.length) {
    return record.timeline.map((node: NotificationStatusTransitionDto) => ({
      color: 'red',
      children: `${String(node.status)} · ${formatDateTime(node.at)}${node.detail ? `：${node.detail}` : ''}`,
    }))
  }
  return [
    { color: 'red', children: `进入死信 · ${formatDateTime(record.failedAt)}（重试 ${record.retryCount} 次后仍失败）` },
    { color: 'gray', children: `创建 · ${formatDateTime(record.createdAt)}` },
  ]
})

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

  const header = ['记录ID', '接收人', '用户ID', '渠道', '模板编码', '标题', '重试次数', '错误码', '错误消息', '失败时间']
  const rows = tableData.value.map((r) => [
    r.recordId,
    r.recipient ?? '',
    r.userId,
    NOTIFICATION_CHANNEL_META[r.channel].label,
    r.templateCode,
    r.title ?? '',
    String(r.retryCount),
    r.errorCode ?? '',
    r.errorMessage ?? '',
    formatDateTime(r.failedAt),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `死信记录导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化 ----------
onMounted(() => {
  // 支持通知记录页死信跳转携带筛选参数
  const queryTemplateCode = typeof route.query.templateCode === 'string' ? route.query.templateCode : ''
  if (queryTemplateCode) filters.templateCode = queryTemplateCode
  const queryChannel = typeof route.query.channel === 'string' ? route.query.channel : ''
  if (queryChannel && NOTIFICATION_CHANNELS.includes(queryChannel as NotificationChannel)) {
    filters.channel = queryChannel as NotificationChannel
  }

  refreshAll()
})
</script>

<style scoped>
.dead-letters {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 24px;
}

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}

.filter-form {
  flex-wrap: wrap;
  row-gap: 8px;
}

.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.selection-hint {
  font-size: 12px;
  color: #8c8c8c;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.empty-sub {
  margin-top: 8px;
  font-size: 12px;
  color: #8c8c8c;
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

.dead-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.retry-count {
  font-size: 12px;
  color: #595959;
}

.retry-count.over {
  color: #faad14;
  font-weight: 600;
}

.error-code {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  color: #ff4d4f;
}

.error-message {
  font-size: 12px;
  color: #8c8c8c;
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

.dead-timeline {
  margin-top: 8px;
}

.batch-failures {
  margin-top: 12px;
}

.batch-failures-title {
  margin-bottom: 8px;
  font-weight: 500;
}

.batch-failures-list {
  max-height: 220px;
  padding-left: 20px;
  overflow-y: auto;
}

.batch-failures-list li {
  margin-bottom: 4px;
  display: flex;
  flex-direction: column;
}

.result-tip {
  margin-top: 12px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
