<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Input,
  InputSearch,
  Button,
  Table,
  Select,
  Drawer,
  Descriptions,
  DescriptionsItem,
  Image as AImage,
  Empty,
  Spin,
  message,
} from 'ant-design-vue'
import { EyeOutlined, NodeIndexOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import type { TableColumnsType, TablePaginationConfig } from 'ant-design-vue'
import { orderApi } from '../api/order.api'
import type { OrderListItemDto, OrderDetailDto, OrderStatus } from '../types/order.dto'
import { StatusTag, EmptyState, DateTimeRangePicker } from '@/shared/components'
import { vPermission } from '@/shared/auth'
import { formatMoney, formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 订单列表页
 *
 * 列出当前卖家全部订单，支持订单号 / 状态 / 下单日期范围 / 买家姓名筛选。
 * 「查看详情」打开 Drawer 展示订单明细；「物流轨迹」跳转轨迹页。
 *
 * 分页：BE-1 待 Order BC 统一 page 从 1 起（当前从 0 起）。
 */

const router = useRouter()

const dataSource = ref<OrderListItemDto[]>([])
const loading = ref(false)
// BE-1: 后端 Order 列表 page 从 0 起，首页传 0
const page = ref(0)
const pageSize = ref(20)
const total = ref(0)

const filters = reactive({
  orderNo: '',
  status: '' as OrderStatus | '',
  dateRange: undefined as [string, string] | undefined,
  buyerName: '',
})

interface StatusOption {
  label: string
  value: OrderStatus | ''
}

const statusOptions: StatusOption[] = [
  { label: '全部', value: '' },
  { label: '待发货', value: 'PendingShipment' },
  { label: '已发货', value: 'Shipped' },
  { label: '已送达', value: 'Delivered' },
  { label: '已完成', value: 'Completed' },
  { label: '已取消', value: 'Cancelled' },
  { label: '已退款', value: 'Refunded' },
]

const pagination = computed<TablePaginationConfig>(() => ({
  // a-table current 为 1 起，BE-1 后端 page 为 0 起，做 +1 适配展示
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
  { title: '状态', key: 'status', width: 100, align: 'center' },
  { title: '下单时间', dataIndex: 'createdAt', key: 'createdAt', width: 170 },
  { title: '付款时间', dataIndex: 'paidAt', key: 'paidAt', width: 170 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' },
]

function buildParams() {
  return {
    status: filters.status === '' ? undefined : filters.status,
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
    logger.error('加载订单列表失败', e)
    message.error('加载订单列表失败')
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
  filters.status = ''
  filters.dateRange = undefined
  filters.buyerName = ''
  page.value = 0
  void loadList()
}

function onDateRangeChange(value: [string, string]): void {
  filters.dateRange = value
}

// ===== 详情 Drawer =====
const detailOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<OrderDetailDto | null>(null)

async function openDetail(record: OrderListItemDto): Promise<void> {
  detailOpen.value = true
  detail.value = null
  detailLoading.value = true
  try {
    const res = await orderApi.get(record.id)
    detail.value = res.data
  } catch (e) {
    logger.error('加载订单详情失败', e)
    message.error('加载订单详情失败')
    detailOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

function goTrace(record: OrderListItemDto): void {
  void router.push(`/orders/${record.id}/trace`)
}

function currencySymbol(currency: string): string {
  return currency === 'CNY' ? '¥' : '$'
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="order-list-page">
    <Breadcrumb class="page-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>订单履约</BreadcrumbItem>
      <BreadcrumbItem>订单列表</BreadcrumbItem>
    </Breadcrumb>

    <Card :bordered="true" class="filter-card">
      <div class="filter-bar">
        <div class="filter-item">
          <span class="filter-label">订单号</span>
          <InputSearch
            v-model:value="filters.orderNo"
            placeholder="请输入订单编号"
            allow-clear
            style="width: 200px"
            @search="onSearch"
          />
        </div>
        <div class="filter-item">
          <span class="filter-label">订单状态</span>
          <Select
            v-model:value="filters.status"
            :options="statusOptions"
            style="width: 140px"
            @change="onSearch"
          />
        </div>
        <div class="filter-item">
          <span class="filter-label">下单时间</span>
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
            style="width: 160px"
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
          共 <strong>{{ total }}</strong> 条订单
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
        description="暂无订单"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="dataSource"
        row-key="id"
        :loading="loading"
        :pagination="pagination"
        :scroll="{ x: 1380 }"
        size="middle"
        aria-label="订单列表"
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
          <template v-else-if="column.key === 'status'">
            <StatusTag type="order" :status="(record as OrderListItemDto).status" />
          </template>
          <template v-else-if="column.key === 'createdAt'">
            <span class="cell-time">{{ formatDateTime((record as OrderListItemDto).createdAt) }}</span>
          </template>
          <template v-else-if="column.key === 'paidAt'">
            <span class="cell-time">{{ formatDateTime((record as OrderListItemDto).paidAt) }}</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <Button type="link" size="small" @click="openDetail(record as OrderListItemDto)">
              <template #icon>
                <EyeOutlined />
              </template>
              查看详情
            </Button>
            <Button
              v-permission="'order:trace:view'"
              type="link"
              size="small"
              @click="goTrace(record as OrderListItemDto)"
            >
              <template #icon>
                <NodeIndexOutlined />
              </template>
              物流轨迹
            </Button>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 订单详情 Drawer -->
    <Drawer
      v-model:open="detailOpen"
      title="订单详情"
      placement="right"
      width="520"
      :destroy-on-close="true"
    >
      <div v-if="detailLoading" class="detail-loading">
        <Spin tip="加载中..." />
      </div>
      <div v-else-if="detail" class="detail-body">
        <Descriptions :column="2" bordered size="small">
          <DescriptionsItem label="订单号" :span="2">
            <span class="order-no">{{ detail.orderNo }}</span>
          </DescriptionsItem>
          <DescriptionsItem label="订单状态">
            <StatusTag type="order" :status="detail.status" />
          </DescriptionsItem>
          <DescriptionsItem label="商品数">
            {{ detail.itemCount }}
          </DescriptionsItem>
          <DescriptionsItem label="订单金额" :span="2">
            <span class="cell-amount">
              {{ formatMoney(detail.totalAmount, { symbol: currencySymbol(detail.currency) }) }}
            </span>
          </DescriptionsItem>
          <DescriptionsItem label="买家">
            {{ detail.buyerName }}
            <span class="cell-muted">{{ detail.buyerPhone }}</span>
          </DescriptionsItem>
          <DescriptionsItem label="收货人">
            {{ detail.receiverName }}
            <span class="cell-muted">{{ detail.receiverPhone }}</span>
          </DescriptionsItem>
          <DescriptionsItem label="收货地址" :span="2">
            {{ detail.receiverAddress }}
          </DescriptionsItem>
          <DescriptionsItem label="下单时间">
            {{ formatDateTime(detail.createdAt) }}
          </DescriptionsItem>
          <DescriptionsItem label="付款时间">
            {{ formatDateTime(detail.paidAt) }}
          </DescriptionsItem>
          <DescriptionsItem v-if="detail.logisticsCompany" label="物流公司">
            {{ detail.logisticsCompany }}
          </DescriptionsItem>
          <DescriptionsItem v-if="detail.logisticsNo" label="物流单号">
            {{ detail.logisticsNo }}
          </DescriptionsItem>
          <DescriptionsItem v-if="detail.remark" label="备注" :span="2">
            {{ detail.remark }}
          </DescriptionsItem>
        </Descriptions>

        <div class="detail-section-title">商品明细</div>
        <div v-if="detail.items.length === 0">
          <Empty description="暂无商品明细" />
        </div>
        <div v-else class="item-list">
          <div v-for="item in detail.items" :key="item.id" class="item-row">
            <AImage
              v-if="item.coverImage"
              :src="item.coverImage"
              :width="48"
              :height="48"
              class="item-thumb"
            />
            <div v-else class="item-thumb item-thumb-placeholder">商品</div>
            <div class="item-info">
              <div class="item-name">{{ item.productName }}</div>
              <div class="item-sku">{{ item.skuName }} · {{ item.skuCode }}</div>
            </div>
            <div class="item-qty">×{{ item.quantity }}</div>
            <div class="item-subtotal">
              {{ formatMoney(item.subtotal, { symbol: currencySymbol(detail.currency) }) }}
            </div>
          </div>
        </div>
      </div>
      <EmptyState v-else description="暂无订单详情" />
    </Drawer>
  </div>
</template>

<style scoped>
.order-list-page {
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
  margin-left: 4px;
}
.cell-amount {
  font-weight: 600;
  color: #000000d9;
}
.cell-time {
  font-size: 13px;
  color: #595959;
}
.detail-loading {
  display: flex;
  justify-content: center;
  padding: 48px 0;
}
.detail-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.detail-section-title {
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
  margin-top: 8px;
}
.item-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.item-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}
.item-thumb {
  width: 48px;
  height: 48px;
  border-radius: 4px;
  flex-shrink: 0;
}
.item-thumb-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #f0f5ff, #e6f4ff);
  color: #1677ff;
  font-size: 11px;
  font-weight: 500;
  border: 1px solid #d6e4ff;
}
.item-info {
  flex: 1;
  min-width: 0;
}
.item-name {
  font-size: 13px;
  color: #000000d9;
  line-height: 1.4;
}
.item-sku {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 2px;
}
.item-qty {
  font-size: 13px;
  color: #595959;
  flex-shrink: 0;
}
.item-subtotal {
  font-size: 13px;
  font-weight: 600;
  color: #000000d9;
  flex-shrink: 0;
}
</style>
