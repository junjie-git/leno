<!-- web/operations/src/modules/05-order-ops/views/OrderManagement.vue -->
<template>
  <div class="order-management">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="订单号">
          <a-input-search
            v-model:value="filters.orderNo"
            placeholder="如 NO202607261523001"
            allow-clear
            style="width: 220px"
            @search="onQuery"
          />
        </a-form-item>
        <a-form-item label="买家 ID">
          <a-input
            v-model:value="filters.userId"
            placeholder="如 U20240345"
            allow-clear
            style="width: 150px"
          />
        </a-form-item>
        <a-form-item label="卖家 ID">
          <a-input
            v-model:value="filters.sellerId"
            placeholder="如 SL2024088"
            allow-clear
            style="width: 150px"
          />
        </a-form-item>
        <a-form-item label="订单状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="下单时间">
          <DateTimeRangePicker :value="timeRange" show-time @change="onTimeRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：状态计数概览卡 -->
    <a-row :gutter="16" class="stats-row">
      <a-col v-for="card in statCards" :key="card.key" :xs="12" :sm="8" :lg="spanOfStatCard">
        <StatisticCard
          :title="card.title"
          :value="card.value"
          :status="card.status"
          :loading="statsLoading"
        />
      </a-col>
    </a-row>

    <!-- 区域 C：订单表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <span class="toolbar-title">订单列表</span>
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
        :row-key="(record: OrderDto) => record.id"
        :scroll="{ x: 1280 }"
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
          <template v-if="column.key === 'orderNo'">
            <div class="mono order-no" :aria-label="`订单编号 ${record.orderNo}`">{{ record.orderNo }}</div>
          </template>
          <template v-else-if="column.key === 'itemSummary'">
            <div class="item-summary" :title="record.itemSummary">{{ record.itemSummary }}</div>
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
          <template v-else-if="column.key === 'totalAmount'">
            <span class="amount">{{ formatMoney(record.totalAmount) }}</span>
          </template>
          <template v-else-if="column.key === 'payStatus'">
            <div class="cell-stack">
              <a-tag :color="PAY_STATUS_META[payStatusOf(record)].color">
                {{ PAY_STATUS_META[payStatusOf(record)].label }}
              </a-tag>
              <span v-if="record.paymentMethod" class="cell-sub">{{ paymentMethodLabel(record.paymentMethod) }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag
              :color="ORDER_STATUS_META[record.status].color"
              :aria-label="ORDER_STATUS_META[record.status].label"
            >
              {{ ORDER_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'createdAt'">{{ formatDateTime(record.createdAt) }}</template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看详情" @click="onViewDetail(record)">详情</a-button>
              <a-button
                v-if="auth.isAdmin"
                type="link"
                size="small"
                danger
                :disabled="!FORCE_CANCELLABLE_STATUSES.includes(record.status)"
                aria-label="强制取消订单"
                @click="onOpenForceCancel(record)"
              >
                强制取消
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="订单详情"
      placement="right"
      width="800"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 基础信息 -->
          <h3 class="drawer-section-title">基础信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="订单号" :span="2">
              <span class="mono">{{ detail.orderNo }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="订单状态">
              <a-tag :color="ORDER_STATUS_META[detail.status].color">
                {{ ORDER_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="订单金额">
              <span class="amount">{{ formatMoney(detail.totalAmount) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="买家">{{ detail.buyerName || detail.userId }}</a-descriptions-item>
            <a-descriptions-item label="卖家">{{ detail.sellerName || detail.sellerId }}</a-descriptions-item>
            <a-descriptions-item label="下单时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
            <a-descriptions-item label="支付方式">
              {{ detail.paymentMethod ? paymentMethodLabel(detail.paymentMethod) : '—' }}
            </a-descriptions-item>
            <a-descriptions-item v-if="detail.cancelReason" label="取消原因" :span="2">
              {{ detail.cancelReason }}
            </a-descriptions-item>
          </a-descriptions>

          <!-- 订单行 -->
          <h3 class="drawer-section-title">订单行</h3>
          <a-table
            v-if="(detail.lines?.length ?? 0) > 0"
            :columns="lineColumns"
            :data-source="detail.lines"
            :pagination="false"
            size="small"
            row-key="id"
          >
            <template #bodyCell="{ column, record: line }">
              <template v-if="column.key === 'product'">
                <div class="line-product">
                  <a-image
                    v-if="line.imageUrl"
                    :src="line.imageUrl"
                    :alt="line.productName"
                    :width="40"
                    :height="40"
                    style="border-radius: 4px; object-fit: cover"
                  />
                  <div class="line-meta">
                    <div class="line-name" :title="line.productName">{{ line.productName }}</div>
                    <div v-if="line.skuSpec" class="cell-sub">{{ line.skuSpec }}</div>
                  </div>
                </div>
              </template>
              <template v-else-if="column.key === 'unitPrice'">{{ formatMoney(line.unitPrice) }}</template>
              <template v-else-if="column.key === 'quantity'">x{{ line.quantity }}</template>
              <template v-else-if="column.key === 'subtotal'">
                <span class="amount">{{ formatMoney(line.subtotal) }}</span>
              </template>
            </template>
          </a-table>
          <EmptyState v-else description="暂无订单行信息" />

          <!-- 收货地址 -->
          <h3 class="drawer-section-title">
            <EnvironmentOutlined /> 收货地址
          </h3>
          <a-descriptions v-if="detail.address" :column="2" bordered size="small">
            <a-descriptions-item label="收货人">{{ detail.address.receiver }}</a-descriptions-item>
            <a-descriptions-item label="联系电话">{{ detail.address.phone }}</a-descriptions-item>
            <a-descriptions-item label="所在地区">
              {{ detail.address.province }}{{ detail.address.city }}{{ detail.address.district ?? '' }}
            </a-descriptions-item>
            <a-descriptions-item label="详细地址">{{ detail.address.detail }}</a-descriptions-item>
          </a-descriptions>
          <EmptyState v-else description="暂无收货地址" />

          <!-- 支付信息 -->
          <h3 class="drawer-section-title">
            <TransactionOutlined /> 支付信息
          </h3>
          <a-descriptions v-if="detail.payment" :column="2" bordered size="small">
            <a-descriptions-item label="支付方式">
              {{ paymentMethodLabel(detail.payment.method) }}
            </a-descriptions-item>
            <a-descriptions-item label="支付状态">{{ detail.payment.status }}</a-descriptions-item>
            <a-descriptions-item label="支付流水号">
              <span class="mono">{{ detail.payment.transactionNo ?? '—' }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="实付金额">
              <span class="amount">{{ formatMoney(detail.payment.paidAmount ?? detail.totalAmount) }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="支付时间" :span="2">
              {{ formatDateTime(detail.payment.paidAt) }}
            </a-descriptions-item>
          </a-descriptions>
          <EmptyState v-else description="暂无支付信息" />

          <!-- 物流轨迹 -->
          <h3 class="drawer-section-title">物流轨迹</h3>
          <template v-if="(detail.logisticsTrack?.length ?? 0) > 0">
            <p v-if="detail.trackingNo" class="tracking-no">
              物流单号：<span class="mono">{{ detail.trackingNo }}</span>
            </p>
            <a-timeline class="detail-timeline">
              <a-timeline-item
                v-for="(node, index) in detail.logisticsTrack"
                :key="index"
                :color="index === 0 ? 'blue' : 'gray'"
                :aria-label="`${formatDateTime(node.time)} ${node.description}`"
              >
                {{ node.description }}
                <div class="cell-sub">
                  {{ formatDateTime(node.time) }}<template v-if="node.operator"> · {{ node.operator }}</template>
                </div>
              </a-timeline-item>
            </a-timeline>
          </template>
          <EmptyState v-else description="暂无物流轨迹" />

          <!-- 状态历史 -->
          <h3 class="drawer-section-title">状态历史</h3>
          <a-timeline v-if="(detail.statusHistory?.length ?? 0) > 0" class="detail-timeline">
            <a-timeline-item
              v-for="(item, index) in detail.statusHistory"
              :key="index"
              :color="ORDER_STATUS_META[item.status].color === 'default' ? 'gray' : ORDER_STATUS_META[item.status].color"
            >
              {{ ORDER_STATUS_META[item.status].label }} · {{ item.operator }}
              <div class="cell-sub">
                {{ formatDateTime(item.createdAt) }}<template v-if="item.remark">：{{ item.remark }}</template>
              </div>
            </a-timeline-item>
          </a-timeline>
          <EmptyState v-else description="暂无状态历史" />
        </template>
      </a-spin>
    </a-drawer>

    <!-- 区域 E：强制取消对话框 -->
    <a-modal
      v-model:open="cancelModalOpen"
      title="强制取消订单"
      :confirm-loading="cancelSubmitting"
      :ok-button-props="{ disabled: !cancelReasonValid, danger: true }"
      ok-text="确认强制取消"
      cancel-text="取消"
      @ok="onSubmitForceCancel"
    >
      <p v-if="cancelTarget" class="cancel-target">
        订单：<span class="mono">{{ cancelTarget.orderNo }}</span>
      </p>
      <p v-if="cancelTarget?.status === 'Paid'" class="cancel-alert">
        <ExclamationCircleOutlined /> 该订单已支付，强制取消将触发自动退款！
      </p>
      <p v-else-if="cancelTarget?.status === 'Shipped'" class="cancel-alert">
        <ExclamationCircleOutlined /> 该订单已发货，强制取消将触发自动退款并回写库存！
      </p>
      <p class="cancel-impact">强制取消为不可逆危险操作，将关闭订单并通知买卖双方。</p>
      <a-form-item
        label="取消原因"
        required
        :validate-status="cancelTouched && !cancelReasonValid ? 'error' : ''"
        :help="cancelTouched && !cancelReasonValid ? '取消原因必填，且长度为 2-200 字' : ''"
      >
        <a-textarea
          v-model:value="cancelReason"
          :rows="4"
          :maxlength="200"
          show-count
          placeholder="请输入强制取消原因（将记录到状态历史并通知买卖双方）"
          @blur="cancelTouched = true"
        />
      </a-form-item>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  EnvironmentOutlined,
  ExclamationCircleOutlined,
  TransactionOutlined,
} from '@ant-design/icons-vue'
import { ConcurrencyError } from '@/shared/http'
import { useAuthStore } from '@/shared/auth'
import { DateTimeRangePicker, EmptyState, StatisticCard } from '@/shared/components'
import { formatDateTime, formatMoney } from '@/shared/utils/format'
import { countOrdersByStatus, orderApi } from '../api/order.api'
import type { OrderDto, OrderStatus } from '../types/order.dto'
import { FORCE_CANCELLABLE_STATUSES, ORDER_STATUS_META } from '../types/order.dto'

/**
 * 订单管理页（05-order-ops）
 *
 * 布局：筛选条 / 状态计数概览卡 / 订单表格 / 详情抽屉 + 强制取消 Modal。
 * - 强制取消仅 Admin 可见，仅待支付 / 已支付 / 已发货态可用
 * - 已支付订单强制取消触发自动退款；已发货订单同时回写库存
 * - 状态计数基于列表端点按状态聚合（md 未定义独立统计端点）
 */

const auth = useAuthStore()

const statusOptions = (Object.keys(ORDER_STATUS_META) as OrderStatus[]).map((value) => ({
  label: ORDER_STATUS_META[value].label,
  value,
}))

interface FilterState {
  orderNo: string
  userId: string
  sellerId: string
  status?: OrderStatus
}

const filters = reactive<FilterState>({
  orderNo: '',
  userId: '',
  sellerId: '',
  status: undefined,
})

const timeRange = ref<[string, string] | undefined>(undefined)

const hasActiveFilters = computed(
  () => Boolean(filters.orderNo || filters.userId || filters.sellerId || filters.status || timeRange.value),
)

const emptyDescription = computed(() =>
  filters.status ? `该状态下暂无订单（${ORDER_STATUS_META[filters.status].label}）` : '暂无订单',
)

// ---------- 支付状态推导 ----------
const PAY_STATUS_META = {
  Unpaid: { label: '未支付', color: 'default' },
  Paid: { label: '已支付', color: 'success' },
  Refunded: { label: '已退款', color: 'error' },
} as const

type PayStatus = keyof typeof PAY_STATUS_META

/** 由订单状态 + 支付方式推导支付状态：待支付未支付；有效流转已支付；取消后有支付方式说明已退款 */
function payStatusOf(order: OrderDto): PayStatus {
  switch (order.status) {
    case 'PendingPayment':
      return 'Unpaid'
    case 'Paid':
    case 'Shipped':
    case 'Delivered':
    case 'Completed':
      return 'Paid'
    default:
      return order.paymentMethod ? 'Refunded' : 'Unpaid'
  }
}

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  WeChatPay: '微信支付',
  Alipay: '支付宝',
  UnionPay: '银联',
}

function paymentMethodLabel(method: string): string {
  return PAYMENT_METHOD_LABELS[method] ?? method
}

// ---------- 列表加载 ----------
const tableData = ref<OrderDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

const pagination = reactive({
  current: 0,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns: TableColumnsType = [
  { title: '订单号', key: 'orderNo', width: 190 },
  { title: '商品摘要', key: 'itemSummary', width: 200, ellipsis: true },
  { title: '买家', key: 'buyer', width: 140, ellipsis: true },
  { title: '卖家', key: 'seller', width: 150, ellipsis: true },
  { title: '金额', key: 'totalAmount', width: 110, align: 'right' },
  { title: '支付状态', key: 'payStatus', width: 110 },
  { title: '订单状态', key: 'status', width: 100 },
  { title: '下单时间', key: 'createdAt', width: 170 },
  { title: '操作', key: 'action', width: auth.isAdmin ? 160 : 90, fixed: 'right' },
]

const lineColumns: TableColumnsType = [
  { title: '商品', key: 'product' },
  { title: '单价', key: 'unitPrice', width: 100, align: 'right' },
  { title: '数量', key: 'quantity', width: 80, align: 'center' },
  { title: '小计', key: 'subtotal', width: 110, align: 'right' },
]

async function fetchOrders() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof orderApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const orderNo = filters.orderNo.trim()
    const userId = filters.userId.trim()
    const sellerId = filters.sellerId.trim()
    if (orderNo) params.orderNo = orderNo
    if (userId) params.userId = userId
    if (sellerId) params.sellerId = sellerId
    if (filters.status) params.status = filters.status
    if (timeRange.value) {
      params.fromTime = timeRange.value[0]
      params.toTime = timeRange.value[1]
    }

    const { data } = await orderApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载订单列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 0
  void fetchOrders()
  void loadStatusCounts()
}

function onReset() {
  filters.orderNo = ''
  filters.userId = ''
  filters.sellerId = ''
  filters.status = undefined
  timeRange.value = undefined
  onQuery()
}

function onRefresh() {
  void fetchOrders()
  void loadStatusCounts()
}

function onTimeRangeChange(value: [string, string]) {
  timeRange.value = value
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchOrders()
}

// ---------- 状态计数概览 ----------
const statsLoading = ref(false)
const statusCounts = ref<Partial<Record<OrderStatus, number>>>({})

/** 概览卡配置：已取消卡合并 Cancelled + ForceCancelled（与设计稿一致） */
const statCards = computed(() => [
  {
    key: 'PendingPayment',
    title: '待支付',
    value: statusCounts.value.PendingPayment ?? 0,
    status: 'warning' as const,
  },
  {
    key: 'Paid',
    title: '已支付',
    value: statusCounts.value.Paid ?? 0,
    status: 'default' as const,
  },
  {
    key: 'Shipped',
    title: '已发货',
    value: statusCounts.value.Shipped ?? 0,
    status: 'default' as const,
  },
  {
    key: 'Completed',
    title: '已完成',
    value: statusCounts.value.Completed ?? 0,
    status: 'success' as const,
  },
  {
    key: 'Cancelled',
    title: '已取消',
    value: (statusCounts.value.Cancelled ?? 0) + (statusCounts.value.ForceCancelled ?? 0),
    status: 'default' as const,
  },
])

const spanOfStatCard = computed(() => {
  const count = statCards.value.length
  return Math.max(Math.floor(24 / count), 4)
})

async function loadStatusCounts() {
  statsLoading.value = true
  try {
    statusCounts.value = await countOrdersByStatus([
      'PendingPayment',
      'Paid',
      'Shipped',
      'Completed',
      'Cancelled',
      'ForceCancelled',
    ])
  } finally {
    statsLoading.value = false
  }
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<OrderDto | null>(null)

function onViewDetail(record: OrderDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as OrderDto
  drawerOpen.value = true
}

// ---------- 强制取消（仅 Admin） ----------
const cancelModalOpen = ref(false)
const cancelTarget = ref<OrderDto | null>(null)
const cancelReason = ref('')
const cancelTouched = ref(false)
const cancelSubmitting = ref(false)

const cancelReasonValid = computed(() => {
  const len = cancelReason.value.trim().length
  return len >= 2 && len <= 200
})

function onOpenForceCancel(record: OrderDto) {
  cancelTarget.value = record
  cancelReason.value = ''
  cancelTouched.value = false
  cancelModalOpen.value = true
}

/** 审核类错误统一分流：并发冲突提示刷新，其余透出后端 message */
function showCancelError(e: unknown) {
  if (e instanceof ConcurrencyError) {
    message.warning('订单状态已变更，请刷新列表')
    return
  }
  message.error(e instanceof Error && e.message ? e.message : '取消失败，请重试')
}

async function onSubmitForceCancel() {
  cancelTouched.value = true
  const target = cancelTarget.value
  if (!target || !cancelReasonValid.value) return

  cancelSubmitting.value = true
  try {
    await orderApi.forceCancel(target.id, { reason: cancelReason.value.trim() })
    cancelModalOpen.value = false
    message.success(`订单 ${target.orderNo} 已强制取消`)
    // 局部更新行状态并同步抽屉与统计概览
    target.status = 'ForceCancelled'
    target.cancelReason = cancelReason.value.trim()
    if (detail.value?.id === target.id) {
      detail.value.status = 'ForceCancelled'
      detail.value.cancelReason = cancelReason.value.trim()
    }
    await loadStatusCounts()
  } catch (e) {
    showCancelError(e)
  } finally {
    cancelSubmitting.value = false
  }
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchOrders()
  void loadStatusCounts()
})
</script>

<style scoped>
.order-management {
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
  margin-top: 0 !important;
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

.order-no {
  color: #000000d9;
}

.item-summary {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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

.line-product {
  display: flex;
  align-items: center;
  gap: 8px;
}

.line-meta {
  min-width: 0;
}

.line-name {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tracking-no {
  margin-bottom: 12px;
  font-size: 13px;
  color: #595959;
}

.detail-timeline {
  margin-top: 8px;
}

.cancel-target {
  margin-bottom: 12px;
  font-weight: 500;
}

.cancel-alert {
  margin-bottom: 8px;
  color: #ff4d4f;
  font-weight: 500;
}

.cancel-impact {
  margin-bottom: 12px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
