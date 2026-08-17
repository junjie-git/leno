<!-- web/operations/src/modules/06-payment-ops/views/RefundRecords.vue -->
<template>
  <div class="refund-records">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="退款编号">
          <a-input
            v-model:value="filters.refundNo"
            placeholder="如 RF2026081600"
            allow-clear
            style="width: 190px"
            @pressEnter="onQuery"
          />
        </a-form-item>
        <a-form-item label="订单号">
          <a-input
            v-model:value="filters.orderId"
            placeholder="订单编号 / 订单 ID"
            allow-clear
            style="width: 190px"
            @pressEnter="onQuery"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 130px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="时间范围">
          <DateTimeRangePicker :value="filters.range" :show-time="true" @change="onRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：统计概览卡（各状态计数 + 退款成功率） -->
    <div class="stat-row">
      <div v-for="stat in statCards" :key="stat.label" class="stat-card">
        <div class="stat-label" :style="{ color: stat.color }">{{ stat.label }}</div>
        <div class="stat-value" :style="{ color: stat.color }">{{ stat.value }}</div>
      </div>
    </div>

    <!-- 区域 C：工具栏 + 退款表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <span class="table-info">共 {{ pagination.total }} 条退款记录</span>
        <a-space>
          <a-button :loading="loading" @click="fetchRefunds">刷新</a-button>
          <a-button @click="onExportCsv">导出 CSV</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载退款记录失败：${errorMessage}`" action-text="重试" @action="fetchRefunds" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: RefundDto) => record.id"
        :row-class-name="rowClassName"
        :scroll="{ x: 1300 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState
            :description="emptyDescription"
            :action-text="hasActiveFilters ? '清空筛选条件' : undefined"
            @action="onReset"
          />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'refundNo'">
            <div class="mono-cell" :aria-label="`退款编号 ${record.refundNo}`">{{ record.refundNo }}</div>
          </template>
          <template v-else-if="column.key === 'order'">
            <span class="mono-cell">{{ record.orderNo || record.orderId }}</span>
          </template>
          <template v-else-if="column.key === 'user'">
            <div class="user-cell">
              <span>{{ record.userName || '—' }}</span>
              <span class="cell-sub">{{ record.userId }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'amount'">
            <span class="cell-amount">{{ formatMoney(record.amount) }}</span>
          </template>
          <template v-else-if="column.key === 'channel'">
            <span class="channel-cell">
              <span class="channel-dot" :style="{ background: CHANNEL_META[record.channel].color }" />
              {{ CHANNEL_META[record.channel].label }}
            </span>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="REFUND_STATUS_META[record.status].color" :aria-label="REFUND_STATUS_META[record.status].label">
              {{ REFUND_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'afterSales'">
            <a
              v-if="record.afterSalesNo"
              class="mono-link"
              aria-label="跳转关联售后"
              @click="goAfterSales(record.afterSalesNo)"
            >
              {{ record.afterSalesNo }}
            </a>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'requestedAt'">
            <span class="cell-sub">{{ formatDateTime(record.requestedAt) }}</span>
          </template>
          <template v-else-if="column.key === 'completedAt'">
            <span class="cell-sub">{{ record.completedAt ? formatDateTime(record.completedAt) : '—' }}</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" aria-label="查看退款详情" @click="onViewDetail(record)">详情</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="detail ? `退款详情 - ${detail.refundNo}` : '退款详情'"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 基础信息 -->
          <h3 class="drawer-section-title">基础信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="退款编号" :span="2">{{ detail.refundNo }}</a-descriptions-item>
            <a-descriptions-item label="订单号" :span="2">{{ detail.orderNo || detail.orderId }}</a-descriptions-item>
            <a-descriptions-item label="买家">
              {{ detail.userName ? `${detail.userName}（${detail.userId}）` : detail.userId }}
            </a-descriptions-item>
            <a-descriptions-item label="退款金额">
              <span class="cell-amount">{{ formatMoney(detail.amount) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="退款渠道">
              {{ CHANNEL_META[detail.channel].label }} · 原路退回
            </a-descriptions-item>
            <a-descriptions-item label="退款状态">
              <a-tag :color="REFUND_STATUS_META[detail.status].color">
                {{ REFUND_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="退款原因" :span="2">{{ detail.reason || '—' }}</a-descriptions-item>
            <a-descriptions-item label="申请时间">{{ formatDateTime(detail.requestedAt) }}</a-descriptions-item>
            <a-descriptions-item label="完成时间">{{ formatDateTime(detail.completedAt) }}</a-descriptions-item>
          </a-descriptions>

          <!-- 关联售后信息 -->
          <h3 class="drawer-section-title">关联售后</h3>
          <a-descriptions v-if="detail.afterSalesNo" :column="1" bordered size="small">
            <a-descriptions-item label="售后单号">
              <a @click="goAfterSales(detail.afterSalesNo ?? '')">{{ detail.afterSalesNo }}</a>
            </a-descriptions-item>
            <a-descriptions-item label="售后 ID">{{ detail.afterSalesId || '—' }}</a-descriptions-item>
          </a-descriptions>
          <div v-else class="no-data">未关联售后单（可能为平台主动退款）</div>

          <!-- 失败原因（Failed 时展示） -->
          <div v-if="detail.status === 'Failed' && detail.failReason" class="fail-box">
            <div class="fail-title">退款失败原因</div>
            <div class="fail-desc">{{ detail.failReason }}</div>
            <div class="fail-hint">当前版本未提供渠道重试端点，请人工核对后在渠道后台处理</div>
          </div>

          <!-- 渠道回写信息 -->
          <h3 class="drawer-section-title">渠道回写信息</h3>
          <JsonViewer
            v-if="detail.channelWriteBack && Object.keys(detail.channelWriteBack).length > 0"
            :value="detail.channelWriteBack"
            :max-height="200"
          />
          <div v-else class="no-data">暂无渠道回写记录</div>

          <!-- 状态时间线 -->
          <h3 class="drawer-section-title">状态时间线</h3>
          <a-timeline class="status-timeline">
            <a-timeline-item v-for="(item, index) in refundTimeline" :key="index" :color="timelineColor(item.status)">
              <div class="timeline-label">{{ item.label }}</div>
              <div class="cell-sub">{{ formatDateTime(item.occurredAt) }}</div>
              <div v-if="item.description" class="timeline-desc">{{ item.description }}</div>
            </a-timeline-item>
          </a-timeline>
        </template>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { EmptyState, JsonViewer, DateTimeRangePicker } from '@/shared/components'
import { formatDateTime, formatMoney, formatPercent } from '@/shared/utils/format'
import { refundApi } from '../api/refund.api'
import type {
  RefundDto,
  RefundStatus,
  RefundTimelineItemDto,
} from '../types/refund.dto'
import type { PaymentChannelType } from '../types/payment.dto'

/**
 * 退款记录页（06-payment-ops）
 *
 * 四区布局：筛选条 / 统计概览卡 / 退款表格 / 详情抽屉。
 * - 支持退款编号/订单/状态/申请时间范围组合筛选
 * - 退款失败行标红 #FFF1F0，抽屉展示失败原因
 * - md 未定义退款重试端点：失败退款不渲染重试按钮，提示人工处理
 * - 关联售后单号可跳转 /order-ops/after-sales?afterSalesNo=xxx
 */

const router = useRouter()

/** 退款状态展示映射（md §6 状态色：待退款橙 / 已退款绿 / 退款失败红） */
const REFUND_STATUS_META: Record<RefundStatus, { label: string; color: string }> = {
  Pending: { label: '待退款', color: 'warning' },
  Refunded: { label: '已退款', color: 'success' },
  Failed: { label: '退款失败', color: 'error' },
}

/** 渠道展示映射（与支付记录页保持一致） */
const CHANNEL_META: Record<PaymentChannelType, { label: string; color: string }> = {
  WeChat: { label: '微信支付', color: '#07C160' },
  Alipay: { label: '支付宝', color: '#1677FF' },
  Other: { label: '其他', color: '#FAAD14' },
}

const statusOptions = (Object.keys(REFUND_STATUS_META) as RefundStatus[]).map((value) => ({
  value,
  label: REFUND_STATUS_META[value].label,
}))

interface FilterState {
  refundNo: string
  orderId: string
  status?: RefundStatus
  range?: [string, string]
}

const filters = reactive<FilterState>({
  refundNo: '',
  orderId: '',
  status: undefined,
  range: undefined,
})

const hasActiveFilters = computed(() =>
  Boolean(filters.refundNo || filters.orderId || filters.status || filters.range),
)

const emptyDescription = computed(() => (hasActiveFilters.value ? '当前筛选条件下暂无退款记录' : '暂无退款记录'))

// ---------- 列表加载 ----------
const tableData = ref<RefundDto[]>([])
const loading = ref(false)
const errorMessage = ref('')
const statusCounts = ref<Record<RefundStatus, number>>({
  Pending: 0,
  Refunded: 0,
  Failed: 0,
})
const successRate = ref(0)

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns: TableColumnsType = [
  { title: '退款编号', key: 'refundNo', width: 170 },
  { title: '订单号', key: 'order', width: 175, ellipsis: true },
  { title: '买家', key: 'user', width: 140, ellipsis: true },
  { title: '金额', key: 'amount', width: 100, align: 'right' },
  { title: '渠道', key: 'channel', width: 105 },
  { title: '状态', key: 'status', width: 100 },
  { title: '关联售后单号', key: 'afterSales', width: 160 },
  { title: '申请时间', key: 'requestedAt', width: 165 },
  { title: '完成时间', key: 'completedAt', width: 165 },
  { title: '操作', key: 'action', width: 80, fixed: 'right' },
]

/** 统计概览卡：各状态计数 + 退款成功率 */
const statCards = computed(() => [
  { label: '待退款', value: String(statusCounts.value.Pending), color: '#FAAD14' },
  { label: '已退款', value: String(statusCounts.value.Refunded), color: '#52C41A' },
  { label: '退款失败', value: String(statusCounts.value.Failed), color: '#FF4D4F' },
  { label: '退款成功率', value: formatPercent(successRate.value), color: '#1677FF' },
])

async function fetchRefunds() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof refundApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const refundNo = filters.refundNo.trim()
    const orderId = filters.orderId.trim()
    if (refundNo) params.refundNo = refundNo
    if (orderId) params.orderId = orderId
    if (filters.status) params.status = filters.status
    if (filters.range) {
      params.fromTime = filters.range[0]
      params.toTime = filters.range[1]
    }

    const { data } = await refundApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
    statusCounts.value = {
      Pending: data.statusCounts?.Pending ?? 0,
      Refunded: data.statusCounts?.Refunded ?? 0,
      Failed: data.statusCounts?.Failed ?? 0,
    }
    successRate.value = data.successRate ?? 0
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '网络异常'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchRefunds()
}

function onReset() {
  filters.refundNo = ''
  filters.orderId = ''
  filters.status = undefined
  filters.range = undefined
  onQuery()
}

function onRangeChange(value: [string, string]) {
  filters.range = value
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchRefunds()
}

/** 退款失败行标红 */
function rowClassName(record: RefundDto): string {
  return record.status === 'Failed' ? 'row-failed' : ''
}

// ---------- 展示辅助 ----------
function timelineColor(status: string): string {
  switch (status) {
    case 'Refunded':
      return 'green'
    case 'Failed':
      return 'red'
    case 'Pending':
      return 'orange'
    default:
      return 'blue'
  }
}

function goAfterSales(afterSalesNo: string) {
  void router.push({ path: '/order-ops/after-sales', query: { afterSalesNo } })
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

  const header = ['退款编号', '订单号', '买家用户ID', '金额', '渠道', '状态', '关联售后单号', '申请时间', '完成时间']
  const rows = tableData.value.map((r) => [
    r.refundNo,
    r.orderNo || r.orderId,
    r.userId,
    formatMoney(r.amount),
    CHANNEL_META[r.channel].label,
    REFUND_STATUS_META[r.status].label,
    r.afterSalesNo || '',
    formatDateTime(r.requestedAt),
    r.completedAt ? formatDateTime(r.completedAt) : '',
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `退款记录导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<RefundDto | null>(null)

/** 后端未返回时间线时，按列表字段合成基础时间线 */
const refundTimeline = computed<RefundTimelineItemDto[]>(() => {
  const r = detail.value
  if (!r) return []
  if (r.timeline?.length) return r.timeline

  const items: RefundTimelineItemDto[] = [
    {
      status: 'Requested',
      label: '买家发起退款申请',
      description: r.reason ? `退款原因：${r.reason}` : undefined,
      occurredAt: r.requestedAt,
    },
  ]
  if (r.status === 'Refunded' || r.completedAt) {
    items.push({
      status: 'Refunded',
      label: '渠道回写成功，退款完成',
      description: r.afterSalesNo ? `关联售后单 ${r.afterSalesNo}` : undefined,
      occurredAt: r.completedAt ?? r.requestedAt,
    })
  }
  if (r.status === 'Failed') {
    items.push({
      status: 'Failed',
      label: '退款失败',
      description: r.failReason,
      occurredAt: r.completedAt ?? r.requestedAt,
    })
  }
  return items
})

function onViewDetail(record: RefundDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as RefundDto
  drawerOpen.value = true
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchRefunds()
})
</script>

<style scoped>
.refund-records {
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

.stat-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

@media (max-width: 992px) {
  .stat-row {
    grid-template-columns: repeat(2, 1fr);
  }
}

.stat-card {
  padding: 16px 24px;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.03), 0 1px 6px -1px rgba(0, 0, 0, 0.02),
    0 2px 4px 0 rgba(0, 0, 0, 0.02);
}

.stat-label {
  margin-bottom: 8px;
  font-size: 12px;
  color: #8c8c8c;
}

.stat-value {
  font-size: 24px;
  font-weight: 600;
  line-height: 1.2;
  color: #000000d9;
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

.table-info {
  font-size: 12px;
  color: #8c8c8c;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.mono-cell {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 13px;
}

.mono-link {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 12px;
  cursor: pointer;
}

.user-cell {
  display: flex;
  flex-direction: column;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
  font-family: 'SF Mono', Consolas, monospace;
}

.cell-amount {
  color: #ff4d4f;
  font-weight: 500;
}

.channel-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.channel-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

/* 退款失败行红色底色（md §6：#FFF1F0） */
:deep(.row-failed) {
  background: #fff1f0;
}

:deep(.row-failed:hover > td) {
  background: #ffe7e5 !important;
}

.drawer-section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.drawer-section-title:first-child {
  margin-top: 0;
}

.no-data {
  padding: 16px;
  font-size: 12px;
  color: #8c8c8c;
  text-align: center;
  background: #fafafa;
  border-radius: 6px;
}

.fail-box {
  margin-top: 16px;
  padding: 12px 16px;
  background: #fff2f0;
  border: 1px solid #ffccc7;
  border-radius: 6px;
}

.fail-title {
  margin-bottom: 4px;
  font-size: 14px;
  font-weight: 500;
  color: #ff4d4f;
}

.fail-desc {
  font-size: 12px;
  color: #595959;
  font-family: 'SF Mono', Consolas, monospace;
  word-break: break-all;
}

.fail-hint {
  margin-top: 4px;
  font-size: 12px;
  color: #8c8c8c;
}

.status-timeline {
  margin-top: 8px;
}

.timeline-label {
  font-size: 14px;
  color: #000000d9;
}

.timeline-desc {
  margin-top: 4px;
  padding: 8px 12px;
  font-size: 12px;
  color: #595959;
  font-family: 'SF Mono', Consolas, monospace;
  background: #fafafa;
  border-radius: 6px;
  word-break: break-all;
}
</style>
