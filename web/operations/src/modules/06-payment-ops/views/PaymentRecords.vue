<!-- web/operations/src/modules/06-payment-ops/views/PaymentRecords.vue -->
<template>
  <div class="payment-records">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="支付单号">
          <a-input
            v-model:value="filters.paymentNo"
            placeholder="如 PAY2026081615"
            allow-clear
            style="width: 200px"
            @pressEnter="onQuery"
          />
        </a-form-item>
        <a-form-item label="订单号">
          <a-input
            v-model:value="filters.orderId"
            placeholder="订单编号 / 订单 ID"
            allow-clear
            style="width: 180px"
            @pressEnter="onQuery"
          />
        </a-form-item>
        <a-form-item label="用户">
          <a-input
            v-model:value="filters.userId"
            placeholder="买家用户 ID"
            allow-clear
            style="width: 150px"
            @pressEnter="onQuery"
          />
        </a-form-item>
        <a-form-item label="渠道">
          <a-select
            v-model:value="filters.channel"
            placeholder="全部渠道"
            allow-clear
            style="width: 130px"
            :options="channelOptions"
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

    <!-- 区域 B：统计概览卡（各状态计数 + 成功率） -->
    <div class="stat-row">
      <div v-for="stat in statCards" :key="stat.label" class="stat-card">
        <div class="stat-label" :style="{ color: stat.color }">{{ stat.label }}</div>
        <div class="stat-value" :style="{ color: stat.color }">{{ stat.value }}</div>
      </div>
    </div>

    <!-- 区域 C：工具栏 + 支付表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <span class="table-info">共 {{ pagination.total }} 条支付记录</span>
        <a-space>
          <a-button :loading="loading" @click="fetchPayments">刷新</a-button>
          <a-button @click="onExportCsv">导出 CSV</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载支付记录失败：${errorMessage}`" action-text="重试" @action="fetchPayments" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: PaymentDto) => record.id"
        :row-class-name="rowClassName"
        :scroll="{ x: 1200 }"
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
          <template v-if="column.key === 'paymentNo'">
            <div class="mono-cell" :aria-label="`支付单号 ${record.paymentNo}`">{{ record.paymentNo }}</div>
          </template>
          <template v-else-if="column.key === 'order'">
            <div class="order-cell">
              <span class="mono-cell">{{ record.orderNo || record.orderId }}</span>
            </div>
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
            <a-tag :color="PAYMENT_STATUS_META[record.status].color" :aria-label="PAYMENT_STATUS_META[record.status].label">
              {{ PAYMENT_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'createdAt'">
            <span class="cell-sub">{{ formatDateTime(record.createdAt) }}</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看支付详情" @click="onViewDetail(record)">详情</a-button>
              <a-button
                v-if="record.abnormal"
                type="link"
                size="small"
                class="link-warn"
                aria-label="排查异常支付"
                @click="onViewDetail(record)"
              >
                排查
              </a-button>
              <a-button
                v-if="record.status === 'Refunded' && record.afterSalesNo"
                type="link"
                size="small"
                aria-label="跳转关联售后"
                @click="goAfterSales(record.afterSalesNo)"
              >
                关联售后
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="detail ? `支付详情 - ${detail.paymentNo}` : '支付详情'"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 基础信息 -->
          <h3 class="drawer-section-title">基础信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="支付单号" :span="2">{{ detail.paymentNo }}</a-descriptions-item>
            <a-descriptions-item label="订单号" :span="2">{{ detail.orderNo || detail.orderId }}</a-descriptions-item>
            <a-descriptions-item label="买家">
              {{ detail.userName ? `${detail.userName}（${detail.userId}）` : detail.userId }}
            </a-descriptions-item>
            <a-descriptions-item label="支付金额">
              <span class="cell-amount">{{ formatMoney(detail.amount) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="支付渠道">
              {{ CHANNEL_META[detail.channel].label }}
            </a-descriptions-item>
            <a-descriptions-item label="支付状态">
              <a-tag :color="PAYMENT_STATUS_META[detail.status].color">
                {{ PAYMENT_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="渠道流水号" :span="2">
              {{ detail.channelTradeNo || '—' }}
            </a-descriptions-item>
            <a-descriptions-item label="创建时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
            <a-descriptions-item label="支付时间">{{ formatDateTime(detail.paidAt) }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.afterSalesNo" label="关联售后单" :span="2">
              <a @click="goAfterSales(detail.afterSalesNo)">{{ detail.afterSalesNo }}</a>
            </a-descriptions-item>
          </a-descriptions>

          <!-- 异常说明 -->
          <div v-if="detail.abnormal" class="abnormal-box">
            <div class="abnormal-title">异常支付告警</div>
            <div class="abnormal-desc">{{ detail.abnormalReason || '已支付但订单状态未按预期变更，请排查回调链路' }}</div>
          </div>

          <!-- 渠道参数快照 -->
          <h3 class="drawer-section-title">渠道参数（下单快照）</h3>
          <JsonViewer v-if="detail.channelParams && Object.keys(detail.channelParams).length > 0" :value="detail.channelParams" :max-height="200" />
          <div v-else class="no-data">暂无渠道参数快照</div>

          <!-- 回调记录 -->
          <h3 class="drawer-section-title">回调记录（{{ detail.callbackLogs?.length ?? 0 }}）</h3>
          <template v-if="detail.callbackLogs && detail.callbackLogs.length > 0">
            <div v-for="log in detail.callbackLogs" :key="log.id" class="callback-item">
              <div class="callback-head">
                <a-tag :color="log.success ? 'success' : 'error'">{{ log.success ? '成功' : '失败' }}</a-tag>
                <span class="callback-event">{{ log.event }}</span>
                <span class="cell-sub">{{ formatDateTime(log.receivedAt) }}</span>
              </div>
              <div v-if="log.detail" class="callback-detail">{{ log.detail }}</div>
              <JsonViewer v-if="log.payload" :value="log.payload" :max-height="140" />
            </div>
          </template>
          <div v-else class="no-data">暂无回调记录</div>

          <!-- 状态时间线 -->
          <h3 class="drawer-section-title">状态时间线</h3>
          <a-timeline class="status-timeline">
            <a-timeline-item v-for="(item, index) in paymentTimeline" :key="index" :color="timelineColor(item.status)">
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
import { paymentApi } from '../api/payment.api'
import type {
  PaymentChannelType,
  PaymentDto,
  PaymentStatus,
  PaymentTimelineItemDto,
} from '../types/payment.dto'

/**
 * 支付记录页（06-payment-ops）
 *
 * 四区布局：筛选条 / 统计概览卡 / 支付表格 / 详情抽屉。
 * - 支持支付单号/订单/用户/渠道/状态/时间范围组合筛选
 * - 异常支付（Success 且 abnormal=true）行标红 #FFF1F0 并提供排查入口
 * - 已退款记录提供「关联售后」跳转 /order-ops/after-sales?afterSalesNo=xxx
 */

const router = useRouter()

/** 支付状态展示映射（md §6 状态色：待支付橙 / 已支付绿 / 失败红 / 已退款紫） */
const PAYMENT_STATUS_META: Record<PaymentStatus, { label: string; color: string }> = {
  Pending: { label: '待支付', color: 'warning' },
  Success: { label: '已支付', color: 'success' },
  Failed: { label: '支付失败', color: 'error' },
  Refunded: { label: '已退款', color: 'purple' },
}

/** 渠道展示映射（微信绿点 #07C160 / 支付宝蓝点 #1677FF / 其他橙点） */
const CHANNEL_META: Record<PaymentChannelType, { label: string; color: string }> = {
  WeChat: { label: '微信支付', color: '#07C160' },
  Alipay: { label: '支付宝', color: '#1677FF' },
  Other: { label: '其他', color: '#FAAD14' },
}

const statusOptions = (Object.keys(PAYMENT_STATUS_META) as PaymentStatus[]).map((value) => ({
  value,
  label: PAYMENT_STATUS_META[value].label,
}))

const channelOptions = (Object.keys(CHANNEL_META) as PaymentChannelType[]).map((value) => ({
  value,
  label: CHANNEL_META[value].label,
}))

interface FilterState {
  paymentNo: string
  orderId: string
  userId: string
  channel?: PaymentChannelType
  status?: PaymentStatus
  range?: [string, string]
}

const filters = reactive<FilterState>({
  paymentNo: '',
  orderId: '',
  userId: '',
  channel: undefined,
  status: undefined,
  range: undefined,
})

const hasActiveFilters = computed(() =>
  Boolean(filters.paymentNo || filters.orderId || filters.userId || filters.channel || filters.status || filters.range),
)

const emptyDescription = computed(() => (hasActiveFilters.value ? '当前筛选条件下暂无支付记录' : '暂无支付记录'))

// ---------- 列表加载 ----------
const tableData = ref<PaymentDto[]>([])
const loading = ref(false)
const errorMessage = ref('')
const statusCounts = ref<Record<PaymentStatus, number>>({
  Pending: 0,
  Success: 0,
  Failed: 0,
  Refunded: 0,
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
  { title: '支付单号', key: 'paymentNo', width: 210 },
  { title: '订单号', key: 'order', width: 180, ellipsis: true },
  { title: '用户', key: 'user', width: 150, ellipsis: true },
  { title: '金额', key: 'amount', width: 110, align: 'right' },
  { title: '渠道', key: 'channel', width: 110 },
  { title: '状态', key: 'status', width: 100 },
  { title: '创建时间', key: 'createdAt', width: 170 },
  { title: '操作', key: 'action', width: 210, fixed: 'right' },
]

/** 统计概览卡：各状态计数 + 成功率 */
const statCards = computed(() => [
  { label: '待支付', value: String(statusCounts.value.Pending), color: '#FAAD14' },
  { label: '已支付', value: String(statusCounts.value.Success), color: '#52C41A' },
  { label: '支付失败', value: String(statusCounts.value.Failed), color: '#FF4D4F' },
  { label: '已退款', value: String(statusCounts.value.Refunded), color: '#722ED1' },
  { label: '支付成功率', value: formatPercent(successRate.value), color: '#1677FF' },
])

async function fetchPayments() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof paymentApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const paymentNo = filters.paymentNo.trim()
    const orderId = filters.orderId.trim()
    const userId = filters.userId.trim()
    if (paymentNo) params.paymentNo = paymentNo
    if (orderId) params.orderId = orderId
    if (userId) params.userId = userId
    if (filters.channel) params.channel = filters.channel
    if (filters.status) params.status = filters.status
    if (filters.range) {
      params.fromTime = filters.range[0]
      params.toTime = filters.range[1]
    }

    const { data } = await paymentApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
    statusCounts.value = {
      Pending: data.statusCounts?.Pending ?? 0,
      Success: data.statusCounts?.Success ?? 0,
      Failed: data.statusCounts?.Failed ?? 0,
      Refunded: data.statusCounts?.Refunded ?? 0,
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
  void fetchPayments()
}

function onReset() {
  filters.paymentNo = ''
  filters.orderId = ''
  filters.userId = ''
  filters.channel = undefined
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
  void fetchPayments()
}

/** 异常支付行（Success 且 abnormal=true）标红 */
function rowClassName(record: PaymentDto): string {
  return record.status === 'Success' && record.abnormal ? 'row-abnormal' : ''
}

// ---------- 展示辅助 ----------
function timelineColor(status: string): string {
  switch (status) {
    case 'Success':
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

  const header = ['支付单号', '订单号', '用户ID', '金额', '渠道', '状态', '创建时间', '渠道流水号']
  const rows = tableData.value.map((p) => [
    p.paymentNo,
    p.orderNo || p.orderId,
    p.userId,
    formatMoney(p.amount),
    CHANNEL_META[p.channel].label,
    PAYMENT_STATUS_META[p.status].label,
    formatDateTime(p.createdAt),
    p.channelTradeNo || '',
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `支付记录导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<PaymentDto | null>(null)

/** 后端未返回时间线时，按列表字段合成基础时间线 */
const paymentTimeline = computed<PaymentTimelineItemDto[]>(() => {
  const p = detail.value
  if (!p) return []
  if (p.timeline?.length) return p.timeline

  const items: PaymentTimelineItemDto[] = [
    { status: 'Pending', label: '发起支付请求', occurredAt: p.createdAt },
  ]
  if (p.status === 'Success' || p.paidAt) {
    items.push({
      status: 'Success',
      label: '支付成功',
      description: p.channelTradeNo ? `渠道流水号 ${p.channelTradeNo}` : undefined,
      occurredAt: p.paidAt ?? p.createdAt,
    })
  }
  if (p.status === 'Failed') {
    items.push({
      status: 'Failed',
      label: '支付失败',
      description: p.abnormalReason,
      occurredAt: p.paidAt ?? p.createdAt,
    })
  }
  if (p.status === 'Refunded') {
    items.push({
      status: 'Refunded',
      label: '已退款',
      description: p.afterSalesNo ? `关联售后单 ${p.afterSalesNo}` : undefined,
      occurredAt: p.paidAt ?? p.createdAt,
    })
  }
  return items
})

function onViewDetail(record: PaymentDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as PaymentDto
  drawerOpen.value = true
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchPayments()
})
</script>

<style scoped>
.payment-records {
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
  grid-template-columns: repeat(5, 1fr);
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

.link-warn {
  color: #faad14;
}

/* 异常支付行红色底色（md §4：#FFF1F0） */
:deep(.row-abnormal) {
  background: #fff1f0;
}

:deep(.row-abnormal:hover > td) {
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

.abnormal-box {
  margin-top: 16px;
  padding: 12px 16px;
  background: #fff2f0;
  border: 1px solid #ffccc7;
  border-radius: 6px;
}

.abnormal-title {
  margin-bottom: 4px;
  font-size: 14px;
  font-weight: 500;
  color: #ff4d4f;
}

.abnormal-desc {
  font-size: 12px;
  color: #595959;
}

.no-data {
  padding: 16px;
  font-size: 12px;
  color: #8c8c8c;
  text-align: center;
  background: #fafafa;
  border-radius: 6px;
}

.callback-item {
  margin-bottom: 12px;
  padding: 12px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.callback-head {
  display: flex;
  align-items: center;
  gap: 8px;
}

.callback-event {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.callback-detail {
  margin-top: 8px;
  font-size: 12px;
  color: #595959;
  font-family: 'SF Mono', Consolas, monospace;
  word-break: break-all;
}

.callback-item :deep(.json-viewer) {
  margin-top: 8px;
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
