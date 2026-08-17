<!-- web/operations/src/modules/05-order-ops/views/AfterSales.vue -->
<template>
  <div class="after-sales">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="售后单号">
          <a-input-search
            v-model:value="filters.afterSalesNo"
            placeholder="如 AS20260801001"
            allow-clear
            style="width: 200px"
            @search="onQuery"
          />
        </a-form-item>
        <a-form-item label="订单号">
          <a-input
            v-model:value="filters.orderId"
            placeholder="订单 ID 或订单号"
            allow-clear
            style="width: 190px"
          />
        </a-form-item>
        <a-form-item label="买家 ID">
          <a-input
            v-model:value="filters.userId"
            placeholder="如 U20240345"
            allow-clear
            style="width: 140px"
          />
        </a-form-item>
        <a-form-item label="卖家 ID">
          <a-input
            v-model:value="filters.sellerId"
            placeholder="如 SL2024088"
            allow-clear
            style="width: 140px"
          />
        </a-form-item>
        <a-form-item label="售后状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="售后类型">
          <a-select
            v-model:value="filters.type"
            placeholder="全部类型"
            allow-clear
            style="width: 130px"
            :options="typeOptions"
          />
        </a-form-item>
        <a-form-item label="申请时间">
          <DateTimeRangePicker :value="timeRange" show-time @change="onTimeRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：统计概览卡 -->
    <a-row :gutter="16">
      <a-col v-for="card in statCards" :key="card.key" :xs="12" :sm="6">
        <StatisticCard
          :title="card.title"
          :value="card.value"
          :status="card.status"
          :loading="statsLoading"
        />
      </a-col>
    </a-row>

    <!-- 区域 C：售后表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <span class="toolbar-title">售后单列表</span>
        <a-button :loading="loading" @click="onRefresh">刷新</a-button>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="onQuery" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: AfterSalesDto) => record.id"
        :scroll="{ x: 1360 }"
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
          <template v-if="column.key === 'afterSalesNo'">
            <div class="mono no-cell" :aria-label="`售后单编号 ${record.afterSalesNo}`">
              {{ record.afterSalesNo }}
            </div>
          </template>
          <template v-else-if="column.key === 'orderNo'">
            <div class="mono no-cell">{{ record.orderNo || record.orderId }}</div>
          </template>
          <template v-else-if="column.key === 'buyer'">
            <div class="cell-stack">
              <span>{{ record.buyerName || '—' }}</span>
              <span class="cell-sub mono">{{ record.userId }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'seller'">
            <div class="cell-stack">
              <span>{{ record.sellerName || '—' }}</span>
              <span class="cell-sub mono">{{ record.sellerId }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'type'">
            <a-tag :color="AFTER_SALES_TYPE_META[record.type].color">
              {{ AFTER_SALES_TYPE_META[record.type].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'applyAmount'">
            <span class="amount">{{ formatMoney(record.applyAmount) }}</span>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag
              :color="AFTER_SALES_STATUS_META[record.status].color"
              :aria-label="AFTER_SALES_STATUS_META[record.status].label"
            >
              {{ AFTER_SALES_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'createdAt'">{{ formatDateTime(record.createdAt) }}</template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看详情" @click="onViewDetail(record)">详情</a-button>
              <a-button
                type="link"
                size="small"
                :disabled="!AUDITABLE_AFTER_SALES_STATUSES.includes(record.status)"
                :loading="approvingId === record.id"
                aria-label="审核通过"
                @click="onOpenApprove(record)"
              >
                通过
              </a-button>
              <a-button
                type="link"
                size="small"
                danger
                :disabled="!AUDITABLE_AFTER_SALES_STATUSES.includes(record.status)"
                aria-label="审核驳回"
                @click="onOpenReject(record)"
              >
                驳回
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="售后详情"
      placement="right"
      width="800"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 申请信息 -->
          <h3 class="drawer-section-title">申请信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="售后单号" :span="2">
              <span class="mono">{{ detail.afterSalesNo }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="关联订单" :span="2">
              <span class="mono">{{ detail.orderNo || detail.orderId }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="买家">{{ detail.buyerName || detail.userId }}</a-descriptions-item>
            <a-descriptions-item label="卖家">{{ detail.sellerName || detail.sellerId }}</a-descriptions-item>
            <a-descriptions-item label="售后类型">
              {{ AFTER_SALES_TYPE_META[detail.type].label }}
            </a-descriptions-item>
            <a-descriptions-item label="售后状态">
              <a-tag :color="AFTER_SALES_STATUS_META[detail.status].color">
                {{ AFTER_SALES_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item v-if="detail.productName" label="售后商品" :span="2">
              {{ detail.productName }}<template v-if="detail.quantity"> x{{ detail.quantity }}</template>
            </a-descriptions-item>
            <a-descriptions-item label="申请金额">
              <span class="amount">{{ formatMoney(detail.applyAmount) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="申请时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
            <a-descriptions-item label="申请原因" :span="2">{{ detail.reason }}</a-descriptions-item>
          </a-descriptions>

          <!-- 凭证图片 -->
          <h3 class="drawer-section-title">
            <PictureOutlined /> 凭证图片
          </h3>
          <a-image-preview-group v-if="(detail.evidenceImageUrls?.length ?? 0) > 0">
            <a-image
              v-for="(img, i) in detail.evidenceImageUrls"
              :key="img"
              :src="img"
              :alt="`${detail.afterSalesNo} 凭证 ${i + 1}`"
              :width="80"
              :height="80"
              style="border-radius: 4px; object-fit: cover; margin-right: 8px"
            />
          </a-image-preview-group>
          <EmptyState v-else description="暂无凭证图片" />

          <!-- 协商记录 -->
          <h3 class="drawer-section-title">协商记录</h3>
          <a-timeline v-if="(detail.negotiationRecords?.length ?? 0) > 0" class="detail-timeline">
            <a-timeline-item
              v-for="(item, index) in detail.negotiationRecords"
              :key="index"
              :color="NEGOTIATION_ROLE_META[item.role].color"
              :aria-label="`${NEGOTIATION_ROLE_META[item.role].label} ${item.action}`"
            >
              <span class="record-role">{{ NEGOTIATION_ROLE_META[item.role].label }}</span>
              {{ item.action }}
              <div v-if="item.content" class="record-content">{{ item.content }}</div>
              <div class="cell-sub">{{ formatDateTime(item.createdAt) }}</div>
            </a-timeline-item>
          </a-timeline>
          <EmptyState v-else description="暂无协商记录" />
        </template>
      </a-spin>
    </a-drawer>

    <!-- 区域 E-1：审核通过 Modal -->
    <a-modal
      v-model:open="approveModalOpen"
      title="审核通过"
      :confirm-loading="approveSubmitting"
      :ok-button-props="{ disabled: !approveAmountValid }"
      ok-text="确认通过"
      cancel-text="取消"
      @ok="onSubmitApprove"
    >
      <p v-if="approveTarget" class="modal-target">
        售后单：<span class="mono">{{ approveTarget.afterSalesNo }}</span>
      </p>
      <p class="approve-alert">
        <ExclamationCircleOutlined /> 通过后将触发退款流程，请确认审核金额。
      </p>
      <a-form-item label="申请金额">
        <span class="amount">{{ formatMoney(approveTarget?.applyAmount ?? 0) }}</span>
      </a-form-item>
      <a-form-item
        label="审核金额"
        required
        :validate-status="approveTouched && !approveAmountValid ? 'error' : ''"
        :help="
          approveTouched && !approveAmountValid
            ? `审核金额必须大于 0 且不超过申请金额 ${approveTarget?.applyAmount ?? 0} 元`
            : '默认全额退款，可下调审核金额'
        "
      >
        <a-input-number
          v-model:value="approveAmount"
          :min="0"
          :max="approveTarget?.applyAmount ?? 0"
          :precision="2"
          style="width: 200px"
          placeholder="请输入审核金额"
          @blur="approveTouched = true"
        />
      </a-form-item>
      <a-form-item label="备注">
        <a-textarea
          v-model:value="approveRemark"
          :rows="3"
          :maxlength="200"
          show-count
          placeholder="审核备注（选填，将记录到协商记录）"
        />
      </a-form-item>
    </a-modal>

    <!-- 区域 E-2：驳回 Modal -->
    <a-modal
      v-model:open="rejectModalOpen"
      title="驳回售后"
      :confirm-loading="rejectSubmitting"
      :ok-button-props="{ disabled: !rejectReasonValid, danger: true }"
      ok-text="确认驳回"
      cancel-text="取消"
      @ok="onSubmitReject"
    >
      <p v-if="rejectTarget" class="modal-target">
        售后单：<span class="mono">{{ rejectTarget.afterSalesNo }}</span>
      </p>
      <a-form-item
        label="驳回原因"
        required
        :validate-status="rejectTouched && !rejectReasonValid ? 'error' : ''"
        :help="rejectTouched && !rejectReasonValid ? '驳回原因必填，且长度为 5-200 字' : ''"
      >
        <a-textarea
          v-model:value="rejectReason"
          :rows="4"
          :maxlength="200"
          show-count
          placeholder="请输入驳回原因（至少 5 个字，将通知买家）"
          @blur="rejectTouched = true"
        />
      </a-form-item>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ExclamationCircleOutlined, PictureOutlined } from '@ant-design/icons-vue'
import { ConcurrencyError } from '@/shared/http'
import { DateTimeRangePicker, EmptyState, StatisticCard } from '@/shared/components'
import { formatDateTime, formatMoney } from '@/shared/utils/format'
import { afterSalesApi, countAfterSalesByStatus } from '../api/afterSales.api'
import type { AfterSalesDto, AfterSalesStatus, AfterSalesType } from '../types/afterSales.dto'
import {
  AFTER_SALES_STATUS_META,
  AFTER_SALES_TYPE_META,
  AUDITABLE_AFTER_SALES_STATUSES,
  NEGOTIATION_ROLE_META,
} from '../types/afterSales.dto'

/**
 * 售后处理页（05-order-ops）
 *
 * 布局：筛选条 / 状态统计概览 / 售后表格 / 详情抽屉 + 通过 / 驳回 Modal。
 * - 通过时审核金额默认申请金额，校验 0 < 金额 ≤ 申请金额，通过后触发退款流程
 * - 驳回原因必填（≥5 字）；终态售后单（已退款 / 已驳回）操作按钮禁用
 */

interface FilterState {
  afterSalesNo: string
  orderId: string
  userId: string
  sellerId: string
  status?: AfterSalesStatus
  type?: AfterSalesType
}

const filters = reactive<FilterState>({
  afterSalesNo: '',
  orderId: '',
  userId: '',
  sellerId: '',
  status: undefined,
  type: undefined,
})

const timeRange = ref<[string, string] | undefined>(undefined)

const statusOptions = (Object.keys(AFTER_SALES_STATUS_META) as AfterSalesStatus[]).map((value) => ({
  label: AFTER_SALES_STATUS_META[value].label,
  value,
}))

const typeOptions = (Object.keys(AFTER_SALES_TYPE_META) as AfterSalesType[]).map((value) => ({
  label: AFTER_SALES_TYPE_META[value].label,
  value,
}))

const hasActiveFilters = computed(
  () =>
    Boolean(
      filters.afterSalesNo ||
        filters.orderId ||
        filters.userId ||
        filters.sellerId ||
        filters.status ||
        filters.type ||
        timeRange.value,
    ),
)

const emptyDescription = computed(() =>
  filters.status ? `该状态下暂无售后单（${AFTER_SALES_STATUS_META[filters.status].label}）` : '暂无售后单',
)

// ---------- 列表加载 ----------
const tableData = ref<AfterSalesDto[]>([])
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
  { title: '售后单号', key: 'afterSalesNo', width: 160 },
  { title: '订单号', key: 'orderNo', width: 180 },
  { title: '买家', key: 'buyer', width: 130, ellipsis: true },
  { title: '卖家', key: 'seller', width: 140, ellipsis: true },
  { title: '类型', key: 'type', width: 100 },
  { title: '申请金额', key: 'applyAmount', width: 110, align: 'right' },
  { title: '状态', key: 'status', width: 110 },
  { title: '申请时间', key: 'createdAt', width: 170 },
  { title: '操作', key: 'action', width: 170, fixed: 'right' },
]

async function fetchAfterSales() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof afterSalesApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const afterSalesNo = filters.afterSalesNo.trim()
    const orderId = filters.orderId.trim()
    const userId = filters.userId.trim()
    const sellerId = filters.sellerId.trim()
    if (afterSalesNo) params.afterSalesNo = afterSalesNo
    if (orderId) params.orderId = orderId
    if (userId) params.userId = userId
    if (sellerId) params.sellerId = sellerId
    if (filters.status) params.status = filters.status
    if (filters.type) params.type = filters.type
    if (timeRange.value) {
      params.fromTime = timeRange.value[0]
      params.toTime = timeRange.value[1]
    }

    const { data } = await afterSalesApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载售后单列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchAfterSales()
  void loadStatusCounts()
}

function onReset() {
  filters.afterSalesNo = ''
  filters.orderId = ''
  filters.userId = ''
  filters.sellerId = ''
  filters.status = undefined
  filters.type = undefined
  timeRange.value = undefined
  onQuery()
}

function onRefresh() {
  void fetchAfterSales()
  void loadStatusCounts()
}

function onTimeRangeChange(value: [string, string]) {
  timeRange.value = value
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchAfterSales()
}

// ---------- 状态统计概览 ----------
const statsLoading = ref(false)
const statusCounts = ref<Partial<Record<AfterSalesStatus, number>>>({})

const statCards = computed(() => [
  {
    key: 'Pending',
    title: '待审核',
    value: statusCounts.value.Pending ?? 0,
    status: 'warning' as const,
  },
  {
    key: 'ReturnShipping',
    title: '退货中',
    value: statusCounts.value.ReturnShipping ?? 0,
    status: 'default' as const,
  },
  {
    key: 'Refunded',
    title: '已退款',
    value: statusCounts.value.Refunded ?? 0,
    status: 'success' as const,
  },
  {
    key: 'Rejected',
    title: '已驳回',
    value: statusCounts.value.Rejected ?? 0,
    status: 'danger' as const,
  },
])

async function loadStatusCounts() {
  statsLoading.value = true
  try {
    statusCounts.value = await countAfterSalesByStatus(['Pending', 'ReturnShipping', 'Refunded', 'Rejected'])
  } finally {
    statsLoading.value = false
  }
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<AfterSalesDto | null>(null)

function onViewDetail(record: AfterSalesDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as AfterSalesDto
  drawerOpen.value = true
}

// ---------- 审核错误分流 ----------
function showAuditError(e: unknown, fallback: string) {
  if (e instanceof ConcurrencyError) {
    message.warning('售后单状态已变更，请刷新列表')
    return
  }
  message.error(e instanceof Error && e.message ? e.message : fallback)
}

// ---------- 审核通过 ----------
const approveModalOpen = ref(false)
const approveTarget = ref<AfterSalesDto | null>(null)
const approveAmount = ref<number | null>(null)
const approveRemark = ref('')
const approveTouched = ref(false)
const approveSubmitting = ref(false)
const approvingId = ref('')

const approveAmountValid = computed(() => {
  const amount = approveAmount.value
  const applyAmount = approveTarget.value?.applyAmount ?? 0
  return amount !== null && Number.isFinite(amount) && amount > 0 && amount <= applyAmount
})

function onOpenApprove(record: AfterSalesDto) {
  approveTarget.value = record
  approveAmount.value = record.applyAmount
  approveRemark.value = ''
  approveTouched.value = false
  approveModalOpen.value = true
}

async function onSubmitApprove() {
  approveTouched.value = true
  const target = approveTarget.value
  if (!target || !approveAmountValid.value) return

  approvingId.value = target.id
  approveSubmitting.value = true
  try {
    await afterSalesApi.approve(target.id, {
      approvedAmount: approveAmount.value ?? undefined,
      remark: approveRemark.value.trim() || undefined,
    })
    approveModalOpen.value = false
    message.success(`售后单 ${target.afterSalesNo} 已通过，退款流程已触发`)
    // 局部更新行状态并同步抽屉
    target.status = 'AdminApproved'
    if (detail.value?.id === target.id) detail.value.status = 'AdminApproved'
    await loadStatusCounts()
  } catch (e) {
    showAuditError(e, '审核操作失败，请重试')
  } finally {
    approveSubmitting.value = false
    approvingId.value = ''
  }
}

// ---------- 审核驳回 ----------
const rejectModalOpen = ref(false)
const rejectTarget = ref<AfterSalesDto | null>(null)
const rejectReason = ref('')
const rejectTouched = ref(false)
const rejectSubmitting = ref(false)

const rejectReasonValid = computed(() => {
  const len = rejectReason.value.trim().length
  return len >= 5 && len <= 200
})

function onOpenReject(record: AfterSalesDto) {
  rejectTarget.value = record
  rejectReason.value = ''
  rejectTouched.value = false
  rejectModalOpen.value = true
}

async function onSubmitReject() {
  rejectTouched.value = true
  const target = rejectTarget.value
  if (!target || !rejectReasonValid.value) return

  rejectSubmitting.value = true
  try {
    await afterSalesApi.reject(target.id, { reason: rejectReason.value.trim() })
    rejectModalOpen.value = false
    message.success(`售后单 ${target.afterSalesNo} 已驳回`)
    target.status = 'AdminRejected'
    if (detail.value?.id === target.id) detail.value.status = 'AdminRejected'
    await loadStatusCounts()
  } catch (e) {
    showAuditError(e, '驳回操作失败，请重试')
  } finally {
    rejectSubmitting.value = false
  }
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchAfterSales()
  void loadStatusCounts()
})
</script>

<style scoped>
.after-sales {
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

.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.toolbar-title {
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.mono {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 13px;
}

.no-cell {
  color: #000000d9;
}

.cell-stack {
  display: flex;
  flex-direction: column;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.amount {
  color: #ff4d4f;
  font-size: 14px;
  font-weight: 500;
}

.drawer-section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.record-role {
  font-weight: 500;
  margin-right: 4px;
}

.record-content {
  margin: 4px 0;
  color: #595959;
}

.detail-timeline {
  margin-top: 8px;
}

.modal-target {
  margin-bottom: 12px;
  font-weight: 500;
}

.approve-alert {
  margin-bottom: 12px;
  color: #1677ff;
}
</style>
