<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Input,
  InputSearch,
  Button,
  Table,
  Modal,
  Form,
  FormItem,
  Spin,
  message,
} from 'ant-design-vue'
import { CarOutlined, ReloadOutlined, EnvironmentOutlined } from '@ant-design/icons-vue'
import type { TableColumnsType, TablePaginationConfig, FormInstance } from 'ant-design-vue'
import { orderApi } from '../api/order.api'
import type { OrderListItemDto, OrderDetailDto, ShipOrderDto } from '../types/order.dto'
import { EmptyState, DateTimeRangePicker } from '@/shared/components'
import { vPermission } from '@/shared/auth'
import { formatMoney, formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 待发货订单页
 *
 * 列出 status=PendingShipment 的订单，支持订单号 / 买家姓名 / 付款日期范围筛选，
 * 点击「发货」打开弹窗，录入物流公司与单号后调用发货接口（携带乐观锁 version）。
 *
 * 分页：BE-1 待 Order BC 统一 page 从 1 起（当前从 0 起）。
 */

const dataSource = ref<OrderListItemDto[]>([])
const loading = ref(false)
// BE-1: 后端 Order 列表 page 从 0 起，首页传 0
const page = ref(0)
const pageSize = ref(20)
const total = ref(0)

const filters = reactive({
  orderNo: '',
  buyerName: '',
  dateRange: undefined as [string, string] | undefined,
})

const pagination = computed<TablePaginationConfig>(() => ({
  // a-table 的 current 为 1 起，BE-1 后端 page 为 0 起，做 +1 适配展示
  current: page.value + 1,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
  showTotal: (t: number) => `共 ${t} 条`,
}))

const columns: TableColumnsType = [
  { title: '订单号', dataIndex: 'orderNo', key: 'orderNo', width: 200, ellipsis: true },
  { title: '买家', key: 'buyer', width: 160 },
  { title: '收货人', key: 'receiver', width: 180 },
  { title: '商品数', dataIndex: 'itemCount', key: 'itemCount', width: 90, align: 'center' },
  { title: '订单金额', key: 'totalAmount', width: 130, align: 'right' },
  { title: '付款时间', dataIndex: 'paidAt', key: 'paidAt', width: 170 },
  { title: '操作', key: 'action', width: 110, fixed: 'right' },
]

function buildParams() {
  return {
    status: 'PendingShipment' as const,
    orderNo: filters.orderNo.trim() || undefined,
    buyerName: filters.buyerName.trim() || undefined,
    startDate: filters.dateRange?.[0],
    endDate: filters.dateRange?.[1],
    // BE-1: 后端 Order 列表 page 从 0 起
    page: page.value,
    pageSize: pageSize.value,
  }
}

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const res = await orderApi.list(buildParams())
    // client 响应拦截器已解包 ApiResponse.data，res.data 即 PageResult
    const data = res.data
    dataSource.value = data.items
    total.value = data.total
  } catch (e) {
    logger.error('加载待发货订单失败', e)
    message.error('加载待发货订单失败')
    dataSource.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function onTableChange(pag: TablePaginationConfig): void {
  // a-table current 为 1 起，BE-1 后端 page 为 0 起，做 -1 适配
  page.value = (pag.current ?? 1) - 1
  pageSize.value = pag.pageSize ?? 20
  void loadList()
}

function onSearch(): void {
  // BE-1: 后端 Order 列表 page 从 0 起，搜索时回到首页 0
  page.value = 0
  void loadList()
}

function onReset(): void {
  filters.orderNo = ''
  filters.buyerName = ''
  filters.dateRange = undefined
  page.value = 0
  void loadList()
}

function onDateRangeChange(value: [string, string]): void {
  filters.dateRange = value
}

// ===== 发货弹窗 =====
const shipModalOpen = ref(false)
const shipSubmitting = ref(false)
const shipLoadingDetail = ref(false)
const shipFormRef = ref<FormInstance>()
const shipForm = reactive({
  logisticsCompany: '',
  logisticsNo: '',
})
const currentOrder = ref<OrderListItemDto | null>(null)
const currentDetail = ref<OrderDetailDto | null>(null)

const shipRules = {
  logisticsCompany: [{ required: true, message: '请输入物流公司', trigger: 'blur' }],
  logisticsNo: [{ required: true, message: '请输入物流单号', trigger: 'blur' }],
}

const modalTitle = computed(() =>
  currentOrder.value ? `发货 - ${currentOrder.value.orderNo}` : '发货',
)

async function openShipModal(record: OrderListItemDto): Promise<void> {
  currentOrder.value = record
  currentDetail.value = null
  shipForm.logisticsCompany = ''
  shipForm.logisticsNo = ''
  shipModalOpen.value = true
  // 拉取订单详情以获取乐观锁 version 与商品明细（用于弹窗摘要）
  shipLoadingDetail.value = true
  try {
    const res = await orderApi.get(record.id)
    currentDetail.value = res.data
  } catch (e) {
    logger.error('加载订单详情失败', e)
    message.error('加载订单详情失败，请稍后重试')
    shipModalOpen.value = false
  } finally {
    shipLoadingDetail.value = false
  }
}

function closeShipModal(): void {
  if (shipSubmitting.value) return
  shipModalOpen.value = false
  currentOrder.value = null
  currentDetail.value = null
  shipFormRef.value?.resetFields()
}

async function handleShip(): Promise<void> {
  if (!currentOrder.value || !currentDetail.value) return
  try {
    await shipFormRef.value?.validate()
  } catch {
    return
  }
  shipSubmitting.value = true
  const body: ShipOrderDto = {
    logisticsCompany: shipForm.logisticsCompany.trim(),
    logisticsNo: shipForm.logisticsNo.trim(),
    version: currentDetail.value.version,
  }
  try {
    await orderApi.ship(currentOrder.value.id, body)
    message.success('发货成功，订单已移交物流')
    shipModalOpen.value = false
    currentOrder.value = null
    currentDetail.value = null
    await loadList()
  } catch (e) {
    logger.error('发货失败', e)
    message.error('发货失败，请稍后重试')
  } finally {
    shipSubmitting.value = false
  }
}

function currencySymbol(currency: string): string {
  return currency === 'CNY' ? '¥' : '$'
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="pending-shipment-page">
    <Breadcrumb class="page-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>订单履约</BreadcrumbItem>
      <BreadcrumbItem>待发货</BreadcrumbItem>
    </Breadcrumb>

    <Card :bordered="true" class="filter-card">
      <div class="filter-bar">
        <div class="filter-item">
          <span class="filter-label">订单号</span>
          <InputSearch
            v-model:value="filters.orderNo"
            placeholder="请输入订单编号"
            allow-clear
            style="width: 220px"
            @search="onSearch"
          />
        </div>
        <div class="filter-item">
          <span class="filter-label">付款时间</span>
          <DateTimeRangePicker
            :value="filters.dateRange"
            :show-time="true"
            style="width: 320px"
            @change="onDateRangeChange"
          />
        </div>
        <div class="filter-item">
          <span class="filter-label">买家姓名</span>
          <Input
            v-model:value="filters.buyerName"
            placeholder="请输入买家姓名"
            allow-clear
            style="width: 180px"
            @press-enter="onSearch"
          />
        </div>
        <div class="filter-actions">
          <Button type="primary" @click="onSearch">查询</Button>
          <Button @click="onReset">重置</Button>
        </div>
      </div>

      <div class="toolbar">
        <span class="toolbar-total">
          共 <strong>{{ total }}</strong> 条待发货
        </span>
        <Button size="small" @click="loadList">
          <template #icon>
            <ReloadOutlined />
          </template>
          刷新
        </Button>
      </div>

      <EmptyState
        v-if="!loading && dataSource.length === 0"
        description="暂无待发货订单"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="dataSource"
        row-key="id"
        :loading="loading"
        :pagination="pagination"
        :scroll="{ x: 1040 }"
        size="middle"
        aria-label="待发货订单列表"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'orderNo'">
            <span class="order-no">{{ (record as OrderListItemDto).orderNo }}</span>
          </template>
          <template v-else-if="column.key === 'buyer'">
            <div class="cell-stack">
              <span class="cell-strong">{{ (record as OrderListItemDto).buyerName }}</span>
              <span class="cell-muted">{{ (record as OrderListItemDto).buyerPhone }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'receiver'">
            <div class="cell-stack">
              <span class="cell-strong">{{ (record as OrderListItemDto).receiverName }}</span>
              <span class="cell-muted">{{ (record as OrderListItemDto).receiverPhone }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'totalAmount'">
            <span class="cell-amount">
              {{ formatMoney((record as OrderListItemDto).totalAmount, { symbol: currencySymbol((record as OrderListItemDto).currency) }) }}
            </span>
          </template>
          <template v-else-if="column.key === 'paidAt'">
            <span class="cell-time">{{ formatDateTime((record as OrderListItemDto).paidAt) }}</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <Button
              v-permission="'order:ship'"
              type="primary"
              size="small"
              @click="openShipModal(record as OrderListItemDto)"
            >
              <template #icon>
                <CarOutlined />
              </template>
              发货
            </Button>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 发货弹窗 -->
    <Modal
      :open="shipModalOpen"
      :title="modalTitle"
      :confirm-loading="shipSubmitting"
      :mask-closable="false"
      width="480px"
      ok-text="确认发货"
      cancel-text="取消"
      @ok="handleShip"
      @cancel="closeShipModal"
    >
      <Spin :spinning="shipLoadingDetail" tip="加载订单信息...">
        <div v-if="currentDetail" class="order-summary">
          <div class="os-row">
            <div class="os-info">
              <div class="os-name">
                {{ currentDetail.items[0]?.productName ?? '订单商品' }}
              </div>
              <div class="os-meta">
                {{ currentDetail.items[0]?.skuName ?? '-' }} · 共 {{ currentDetail.itemCount }} 件
              </div>
              <div class="os-meta">
                <EnvironmentOutlined /> {{ currentOrder?.receiverAddress }}
              </div>
            </div>
            <div class="os-amount">
              {{ formatMoney(currentDetail.totalAmount, { symbol: currencySymbol(currentDetail.currency) }) }}
            </div>
          </div>
        </div>

        <Form
          ref="shipFormRef"
          :model="shipForm"
          :rules="shipRules"
          layout="vertical"
          class="ship-form"
        >
          <FormItem label="物流公司" name="logisticsCompany">
            <Input
              v-model:value="shipForm.logisticsCompany"
              placeholder="请输入物流公司，如 顺丰速运"
              :maxlength="64"
              allow-clear
            />
          </FormItem>
          <FormItem label="物流单号" name="logisticsNo">
            <Input
              v-model:value="shipForm.logisticsNo"
              placeholder="请输入物流单号，如 SF1234567890"
              :maxlength="64"
              allow-clear
            />
          </FormItem>
        </Form>
      </Spin>
    </Modal>
  </div>
</template>

<style scoped>
.pending-shipment-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.page-breadcrumb {
  font-size: 14px;
}
.filter-card {
  border-radius: 8px;
}
.filter-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px;
  margin-bottom: 16px;
}
.filter-item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.filter-label {
  font-size: 14px;
  color: #595959;
  white-space: nowrap;
}
.filter-actions {
  display: flex;
  gap: 8px;
  margin-left: auto;
}
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 0 12px;
  border-top: 1px solid #f0f0f0;
}
.toolbar-total {
  font-size: 13px;
  color: #8c8c8c;
}
.toolbar-total strong {
  color: #000000d9;
}
.order-no {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 13px;
  color: #1677ff;
}
.cell-stack {
  display: flex;
  flex-direction: column;
  line-height: 1.5;
}
.cell-strong {
  font-size: 13px;
  color: #000000d9;
}
.cell-muted {
  font-size: 12px;
  color: #8c8c8c;
}
.cell-amount {
  font-weight: 600;
  color: #000000d9;
}
.cell-time {
  font-size: 13px;
  color: #595959;
}
.order-summary {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 12px 16px;
  margin-bottom: 16px;
}
.os-row {
  display: flex;
  align-items: center;
  gap: 12px;
}
.os-info {
  flex: 1;
  min-width: 0;
}
.os-name {
  font-size: 14px;
  color: #000000d9;
  font-weight: 500;
}
.os-meta {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 2px;
}
.os-amount {
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
  white-space: nowrap;
}
.ship-form {
  margin-top: 4px;
}
</style>
