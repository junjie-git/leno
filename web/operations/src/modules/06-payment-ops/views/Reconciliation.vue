<!-- web/operations/src/modules/06-payment-ops/views/Reconciliation.vue -->
<template>
  <div class="reconciliation">
    <!-- 区域 A：统计概览卡 -->
    <a-spin :spinning="statsLoading">
      <div class="stat-row">
        <div v-for="stat in statCards" :key="stat.label" class="stat-card">
          <div class="stat-label" :style="{ color: stat.color }">{{ stat.label }}</div>
          <div class="stat-value" :style="{ color: stat.color }">{{ stat.value }}</div>
        </div>
      </div>
    </a-spin>

    <!-- 区域 B + C：工具栏 + 筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <div class="table-toolbar">
        <IdempotencyButton type="primary" :loading="triggerLoading" @click="openTrigger">
          <template #icon><ThunderboltOutlined /></template>
          手动触发对账
        </IdempotencyButton>
        <a-space>
          <a-button :loading="loading" @click="onRefresh">刷新</a-button>
          <a-button @click="onExportCsv">
            <template #icon><ExportOutlined /></template>
            导出差异清单
          </a-button>
        </a-space>
      </div>

      <a-form layout="inline" class="filter-form">
        <a-form-item label="账单日期">
          <a-date-picker
            v-model:value="filters.billDate"
            value-format="YYYY-MM-DD"
            placeholder="选择账单日期"
            style="width: 150px"
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
        <a-form-item label="差异类型">
          <a-select
            v-model:value="filters.diffType"
            placeholder="全部类型"
            allow-clear
            style="width: 140px"
            :options="diffTypeOptions"
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
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 D：对账差异表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <span class="table-info">共 {{ pagination.total }} 条差异记录</span>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState
          :description="`对账差异列表加载失败：${errorMessage}`"
          action-text="重试"
          @action="fetchDiffs"
        />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: ReconciliationDiffDto) => record.id"
        :scroll="{ x: 1560 }"
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
          <template v-if="column.key === 'billDate'">
            <span class="mono-cell">{{ record.billDate }}</span>
          </template>
          <template v-else-if="column.key === 'channel'">
            <span class="channel-cell">
              <span class="channel-dot" :style="{ background: CHANNEL_META[record.channel].color }" />
              {{ CHANNEL_META[record.channel].label }}
            </span>
          </template>
          <template v-else-if="column.key === 'diffType'">
            <a-tag
              :color="DIFF_TYPE_META[record.diffType].color"
              :aria-label="DIFF_TYPE_META[record.diffType].label"
            >
              {{ DIFF_TYPE_META[record.diffType].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'channelTransactionNo'">
            <a
              v-if="record.channelTransactionNo"
              class="mono-link"
              aria-label="复制渠道流水号"
              @click="copyText(record.channelTransactionNo)"
            >
              {{ record.channelTransactionNo }}
              <CopyOutlined class="copy-icon" />
            </a>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'channelAmount'">
            <span :class="{ 'amount-mismatch': record.diffType === 'AmountMismatch' }">
              {{ record.channelAmount != null ? formatMoney(record.channelAmount) : '—' }}
            </span>
          </template>
          <template v-else-if="column.key === 'systemTransactionNo'">
            <span v-if="record.systemTransactionNo" class="mono-cell">{{ record.systemTransactionNo }}</span>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'systemAmount'">
            <span :class="{ 'amount-mismatch': record.diffType === 'AmountMismatch' }">
              {{ record.systemAmount != null ? formatMoney(record.systemAmount) : '—' }}
            </span>
          </template>
          <template v-else-if="column.key === 'payment'">
            <a
              v-if="record.paymentNo || record.paymentId"
              class="mono-link"
              aria-label="跳转支付记录"
              @click="goPayment(record)"
            >
              {{ record.paymentNo || record.paymentId }}
            </a>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="DIFF_STATUS_META[record.status].color" :aria-label="DIFF_STATUS_META[record.status].label">
              {{ DIFF_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'remark'">
            <a-tooltip v-if="record.remark" :title="record.remark">
              <span class="remark-cell">{{ record.remark }}</span>
            </a-tooltip>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'createdAt'">
            <span class="cell-sub">{{ formatDateTime(record.createdAt) }}</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" aria-label="查看差异详情" @click="onViewDetail(record)">
              <template #icon><EyeOutlined /></template>
              详情
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 E：差异详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="detail ? `差异详情 - ${DIFF_TYPE_META[detail.diffType].label}` : '差异详情'"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 基础信息 -->
          <h3 class="drawer-section-title">基础信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="账单日期">{{ detail.billDate }}</a-descriptions-item>
            <a-descriptions-item label="渠道">
              {{ CHANNEL_META[detail.channel].label }}
            </a-descriptions-item>
            <a-descriptions-item label="差异类型">
              <a-tag :color="DIFF_TYPE_META[detail.diffType].color">
                {{ DIFF_TYPE_META[detail.diffType].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="DIFF_STATUS_META[detail.status].color">
                {{ DIFF_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="差异生成时间" :span="2">
              {{ formatDateTime(detail.createdAt) }}
            </a-descriptions-item>
          </a-descriptions>

          <!-- 渠道侧 / 系统侧对比 -->
          <h3 class="drawer-section-title">两侧对比</h3>
          <div class="side-compare">
            <div class="side-card">
              <div class="side-title">渠道侧</div>
              <a-descriptions :column="1" size="small" bordered>
                <a-descriptions-item label="流水号">
                  <a
                    v-if="detail.channelTransactionNo"
                    class="mono-link"
                    @click="copyText(detail.channelTransactionNo)"
                  >
                    {{ detail.channelTransactionNo }}
                  </a>
                  <span v-else class="cell-sub">无账单记录</span>
                </a-descriptions-item>
                <a-descriptions-item label="金额">
                  {{ detail.channelAmount != null ? formatMoney(detail.channelAmount) : '—' }}
                </a-descriptions-item>
                <a-descriptions-item label="交易时间">
                  {{ detail.channelTransactionTime ? formatDateTime(detail.channelTransactionTime) : '—' }}
                </a-descriptions-item>
              </a-descriptions>
            </div>
            <div class="side-card">
              <div class="side-title">系统侧</div>
              <a-descriptions :column="1" size="small" bordered>
                <a-descriptions-item label="流水号">
                  <span v-if="detail.systemTransactionNo" class="mono-cell">{{ detail.systemTransactionNo }}</span>
                  <span v-else class="cell-sub">无支付记录</span>
                </a-descriptions-item>
                <a-descriptions-item label="金额">
                  <span :class="{ 'amount-mismatch': detail.diffType === 'AmountMismatch' }">
                    {{ detail.systemAmount != null ? formatMoney(detail.systemAmount) : '—' }}
                  </span>
                </a-descriptions-item>
                <a-descriptions-item label="支付单">
                  <a v-if="detail.paymentNo || detail.paymentId" @click="goPayment(detail)">
                    {{ detail.paymentNo || detail.paymentId }}
                  </a>
                  <span v-else class="cell-sub">—</span>
                </a-descriptions-item>
              </a-descriptions>
            </div>
          </div>

          <!-- 处理建议与备注 -->
          <h3 class="drawer-section-title">处理建议</h3>
          <div class="suggestion-box">
            <div class="suggestion-text">{{ DIFF_TYPE_META[detail.diffType].suggestion }}</div>
            <div v-if="detail.remark" class="suggestion-remark">备注：{{ detail.remark }}</div>
          </div>

          <!-- 状态时间线 -->
          <h3 class="drawer-section-title">状态时间线</h3>
          <a-timeline class="status-timeline">
            <a-timeline-item
              v-for="(item, index) in diffTimeline"
              :key="index"
              :color="item.status === 'Resolved' ? 'green' : 'blue'"
            >
              <div class="timeline-label">{{ item.label }}</div>
              <div class="cell-sub">{{ formatDateTime(item.occurredAt) }}</div>
              <div v-if="item.description" class="timeline-desc">{{ item.description }}</div>
            </a-timeline-item>
          </a-timeline>
        </template>
      </a-spin>
    </a-drawer>

    <!-- 手动触发对账弹窗（日期选择 + 确认合一） -->
    <a-modal
      v-model:open="triggerOpen"
      title="手动触发对账"
      :confirm-loading="triggerLoading"
      ok-text="确认触发"
      cancel-text="取消"
      :destroy-on-close="true"
      @ok="onTrigger"
    >
      <a-form layout="vertical" class="trigger-form">
        <a-form-item label="对账账单日期" required>
          <a-date-picker
            v-model:value="triggerDate"
            value-format="YYYY-MM-DD"
            :disabled-date="disabledFutureDate"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
      <div class="trigger-hint">
        将对 {{ triggerDate || '前一天' }} 的全部渠道账单进行对账。任务异步执行（约 1-5 分钟），
        完成后将自动刷新差异列表；同一日期重复触发将被服务端拒绝。
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { CopyOutlined, EyeOutlined, ExportOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import dayjs, { type Dayjs } from 'dayjs'
import { EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime, formatMoney } from '@/shared/utils/format'
import { reconciliationApi } from '../api/reconciliation.api'
import type {
  DiffTimelineItemDto,
  ReconciliationDiffDto,
  ReconciliationDiffQueryParams,
  ReconciliationDiffStatus,
  ReconciliationDiffType,
} from '../types/reconciliation.dto'
import type { PaymentChannelType } from '../types/payment.dto'

/**
 * 渠道对账页（06-payment-ops，spec F-PAY-012）
 *
 * 五区布局：统计概览 / 工具栏 / 筛选条 / 差异表格 / 详情抽屉。
 * - 统计概览：待处理/长款/短款/金额不一致走并行轻量计数请求（pageSize=1 取 total）；
 *   近 7 天新增基于当前页数据统计（后端未提供 createdAt 筛选参数）
 * - 默认筛选 PendingResolve；支持首页对账告警跳转携带 status 查询参数
 * - 手动触发对账：日期选择 + 确认合一弹窗（默认前一天），成功后 2 秒自动刷新
 * - 渠道流水号点击复制；支付单号点击跳转支付记录页
 */

const route = useRoute()
const router = useRouter()

/** 渠道展示映射（与支付/退款/渠道配置页保持一致） */
const CHANNEL_META: Record<PaymentChannelType, { label: string; color: string }> = {
  WeChat: { label: '微信支付', color: '#07C160' },
  Alipay: { label: '支付宝', color: '#1677FF' },
  Other: { label: '其他', color: '#FAAD14' },
}

/** 差异类型展示映射（md §6：长款蓝 / 短款橙 / 金额不一致红，变体随主类型） */
const DIFF_TYPE_META: Record<
  ReconciliationDiffType,
  { label: string; color: string; suggestion: string }
> = {
  LongAmount: {
    label: '长款',
    color: 'processing',
    suggestion: '渠道有账但系统无记录，请检查支付回调日志，或确认为渠道侧测试交易。',
  },
  MissingSystem: {
    label: '系统侧缺失',
    color: 'processing',
    suggestion: '系统侧缺失该笔账单（长款变体），请检查回调日志或确认为测试交易。',
  },
  ShortAmount: {
    label: '短款',
    color: 'warning',
    suggestion: '系统有记录但渠道无账，请等待次日账单生成，或到渠道后台核实支付状态。',
  },
  MissingChannel: {
    label: '渠道侧缺失',
    color: 'warning',
    suggestion: '渠道侧账单缺失（短款变体），请等待次日账单或到渠道后台核实支付状态。',
  },
  AmountMismatch: {
    label: '金额不一致',
    color: 'error',
    suggestion: '两侧均有记录但金额不同，请人工核对退款流程与渠道手续费计算差异。',
  },
}

/** 差异状态展示映射（md §6：待处理橙 / 已修复绿） */
const DIFF_STATUS_META: Record<ReconciliationDiffStatus, { label: string; color: string }> = {
  PendingResolve: { label: '待处理', color: 'warning' },
  Resolved: { label: '已修复', color: 'success' },
}

const channelOptions = (Object.keys(CHANNEL_META) as PaymentChannelType[]).map((value) => ({
  value,
  label: CHANNEL_META[value].label,
}))

const diffTypeOptions = (Object.keys(DIFF_TYPE_META) as ReconciliationDiffType[]).map((value) => ({
  value,
  label: DIFF_TYPE_META[value].label,
}))

const statusOptions = (Object.keys(DIFF_STATUS_META) as ReconciliationDiffStatus[]).map((value) => ({
  value,
  label: DIFF_STATUS_META[value].label,
}))

// ---------- 筛选状态（默认 PendingResolve，md §4） ----------
interface FilterState {
  billDate?: string
  channel?: PaymentChannelType
  diffType?: ReconciliationDiffType
  status?: ReconciliationDiffStatus
}

/** 解析路由查询中的状态参数（首页告警跳转 status=Pending → PendingResolve） */
function normalizeStatusQuery(value: unknown): ReconciliationDiffStatus | undefined {
  const raw = Array.isArray(value) ? value[0] : value
  if (raw === 'PendingResolve' || raw === 'Pending') return 'PendingResolve'
  if (raw === 'Resolved') return 'Resolved'
  return undefined
}

function normalizeBillDateQuery(value: unknown): string | undefined {
  const raw = Array.isArray(value) ? value[0] : value
  return typeof raw === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(raw) ? raw : undefined
}

const filters = reactive<FilterState>({
  billDate: normalizeBillDateQuery(route.query.billDate),
  channel: undefined,
  diffType: undefined,
  status: normalizeStatusQuery(route.query.status) ?? 'PendingResolve',
})

const hasActiveFilters = computed(() =>
  Boolean(filters.billDate || filters.channel || filters.diffType || filters.status),
)

const emptyDescription = computed(() =>
  hasActiveFilters.value ? '当前筛选条件下暂无对账差异' : '暂无对账差异，所有账单均已对平',
)

// ---------- 列表加载 ----------
const tableData = ref<ReconciliationDiffDto[]>([])
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
  { title: '账单日期', key: 'billDate', width: 110 },
  { title: '渠道', key: 'channel', width: 105 },
  { title: '差异类型', key: 'diffType', width: 110 },
  { title: '渠道流水号', key: 'channelTransactionNo', width: 170, ellipsis: true },
  { title: '渠道金额', key: 'channelAmount', width: 100, align: 'right' },
  { title: '系统流水号', key: 'systemTransactionNo', width: 170, ellipsis: true },
  { title: '系统金额', key: 'systemAmount', width: 100, align: 'right' },
  { title: '支付单', key: 'payment', width: 160, ellipsis: true },
  { title: '状态', key: 'status', width: 90 },
  { title: '备注', key: 'remark', width: 150, ellipsis: true },
  { title: '创建时间', key: 'createdAt', width: 165 },
  { title: '操作', key: 'action', width: 90, fixed: 'right' },
]

async function fetchDiffs() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: ReconciliationDiffQueryParams = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.billDate) params.billDate = filters.billDate
    if (filters.channel) params.channel = filters.channel
    if (filters.diffType) params.diffType = filters.diffType
    if (filters.status) params.status = filters.status

    const { data } = await reconciliationApi.listDiffs(params)
    tableData.value = data.items
    pagination.total = data.total
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
  void fetchDiffs()
}

function onReset() {
  filters.billDate = undefined
  filters.channel = undefined
  filters.diffType = undefined
  filters.status = 'PendingResolve'
  onQuery()
}

function onRefresh() {
  void fetchDiffs()
  void fetchStats()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchDiffs()
}

// ---------- 统计概览 ----------
const statsLoading = ref(false)
const stats = reactive({ pending: 0, long: 0, short: 0, mismatch: 0 })

const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000

/** 近 7 天新增：基于当前页数据统计（后端未提供 createdAt 筛选参数） */
const recentCount = computed(() => {
  const now = Date.now()
  return tableData.value.filter(
    (row) => now - new Date(row.createdAt).getTime() <= SEVEN_DAYS_MS,
  ).length
})

const statCards = computed(() => [
  { label: '待处理差异', value: String(stats.pending), color: '#FAAD14' },
  { label: '近 7 天新增', value: String(recentCount.value), color: '#1677FF' },
  { label: '长款', value: String(stats.long), color: '#1677FF' },
  { label: '短款', value: String(stats.short), color: '#FAAD14' },
  { label: '金额不一致', value: String(stats.mismatch), color: '#FF4D4F' },
])

/** 并行轻量计数请求（pageSize=1 取 total）；失败静默保留上次数值，不阻塞列表 */
async function fetchStats() {
  statsLoading.value = true
  const countTotal = async (extra: ReconciliationDiffQueryParams) => {
    const { data } = await reconciliationApi.listDiffs({ page: 1, pageSize: 1, ...extra })
    return data.total
  }
  try {
    const [pending, long, short, mismatch] = await Promise.all([
      countTotal({ status: 'PendingResolve' }),
      countTotal({ diffType: 'LongAmount' }),
      countTotal({ diffType: 'ShortAmount' }),
      countTotal({ diffType: 'AmountMismatch' }),
    ])
    stats.pending = pending
    stats.long = long
    stats.short = short
    stats.mismatch = mismatch
  } catch {
    // 统计为辅助信息，加载失败不提示错误
  } finally {
    statsLoading.value = false
  }
}

// ---------- 手动触发对账 ----------
const triggerOpen = ref(false)
const triggerDate = ref(dayjs().subtract(1, 'day').format('YYYY-MM-DD'))
const triggerLoading = ref(false)

function openTrigger() {
  // 默认对前一天账单对账（md §4：缺省为前一天）
  triggerDate.value = dayjs().subtract(1, 'day').format('YYYY-MM-DD')
  triggerOpen.value = true
}

/** 仅允许选择今天及历史日期 */
function disabledFutureDate(current: Dayjs) {
  return current.isAfter(dayjs(), 'day')
}

async function onTrigger() {
  triggerLoading.value = true
  try {
    await reconciliationApi.trigger(triggerDate.value || undefined)
    triggerOpen.value = false
    message.success('对账任务已提交，请稍后刷新查看结果')
    // md §3：触发后等待 2 秒再刷新列表，等待异步任务入队
    window.setTimeout(() => {
      void fetchDiffs()
      void fetchStats()
    }, 2000)
  } catch (e) {
    message.error(`对账任务触发失败：${e instanceof Error ? e.message : '请稍后重试'}`)
  } finally {
    triggerLoading.value = false
  }
}

// ---------- 展示辅助 ----------
async function copyText(text: string) {
  try {
    await navigator.clipboard.writeText(text)
    message.success('流水号已复制到剪贴板')
  } catch {
    message.error('复制失败，请手动复制')
  }
}

function goPayment(record: ReconciliationDiffDto) {
  const paymentNo = record.paymentNo || record.paymentId
  if (!paymentNo) return
  void router.push({ path: '/payment-ops/payment-records', query: { paymentNo } })
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

  const header = [
    '账单日期',
    '渠道',
    '差异类型',
    '渠道流水号',
    '渠道金额',
    '系统流水号',
    '系统金额',
    '支付单号',
    '状态',
    '备注',
    '创建时间',
  ]
  const rows = tableData.value.map((r) => [
    r.billDate,
    CHANNEL_META[r.channel].label,
    DIFF_TYPE_META[r.diffType].label,
    r.channelTransactionNo || '',
    r.channelAmount != null ? formatMoney(r.channelAmount) : '',
    r.systemTransactionNo || '',
    r.systemAmount != null ? formatMoney(r.systemAmount) : '',
    r.paymentNo || r.paymentId || '',
    DIFF_STATUS_META[r.status].label,
    r.remark || '',
    formatDateTime(r.createdAt),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `对账差异清单_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<ReconciliationDiffDto | null>(null)

/** 后端未返回时间线时，按列表字段合成基础时间线 */
const diffTimeline = computed<DiffTimelineItemDto[]>(() => {
  const r = detail.value
  if (!r) return []
  if (r.timeline?.length) return r.timeline

  const items: DiffTimelineItemDto[] = [
    {
      status: 'Created',
      label: '对账差异生成',
      description: r.remark,
      occurredAt: r.createdAt,
    },
  ]
  if (r.status === 'Resolved') {
    items.push({
      status: 'Resolved',
      label: '差异已修复',
      description: r.resolvedBy ? `处理人：${r.resolvedBy}` : undefined,
      occurredAt: r.resolvedAt ?? r.createdAt,
    })
  }
  return items
})

function onViewDetail(record: ReconciliationDiffDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as ReconciliationDiffDto
  drawerOpen.value = true
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchDiffs()
  void fetchStats()
})
</script>

<style scoped>
.reconciliation {
  display: flex;
  flex-direction: column;
  gap: 16px;
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

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
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

.filter-form {
  flex-wrap: wrap;
  row-gap: 8px;
  padding-top: 4px;
  margin-top: 8px;
  border-top: 1px solid #f0f0f0;
}

.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.table-card .table-toolbar {
  margin-bottom: 0;
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

.copy-icon {
  margin-left: 4px;
  font-size: 12px;
  color: #8c8c8c;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
  font-family: 'SF Mono', Consolas, monospace;
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

/* 金额不一致红色加粗（md §6） */
.amount-mismatch {
  color: #ff4d4f;
  font-weight: 600;
}

.remark-cell {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  font-size: 12px;
  color: #8c8c8c;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;
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

.side-compare {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

@media (max-width: 600px) {
  .side-compare {
    grid-template-columns: 1fr;
  }
}

.side-card {
  padding: 12px;
  background: #fafafa;
  border-radius: 6px;
}

.side-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: #000000d9;
}

.suggestion-box {
  padding: 12px 16px;
  background: #fffbe6;
  border: 1px solid #ffe58f;
  border-radius: 6px;
}

.suggestion-text {
  font-size: 13px;
  color: #595959;
}

.suggestion-remark {
  margin-top: 6px;
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
  background: #fafafa;
  border-radius: 6px;
  word-break: break-all;
}

.trigger-form {
  margin-top: 8px;
}

.trigger-hint {
  padding: 10px 12px;
  font-size: 12px;
  color: #8c8c8c;
  background: #fafafa;
  border-radius: 6px;
}
</style>
