<script setup lang="ts">
import { ref, reactive, computed, onMounted, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Tag,
  Select,
  SelectOption,
  Input,
  Button,
  Space,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { EyeOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons-vue'
import { aftersalesApi } from '../api/aftersales.api'
import type {
  AfterSalesListItemDto,
  AfterSalesStatus,
  AfterSalesType,
  ListAfterSalesParams,
} from '../types/aftersales.dto'
import { StatusTag, EmptyState, DateTimeRangePicker } from '@/shared/components'
import { formatDateTime, formatMoney } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 售后列表页
 *
 * 路由：/after-sales
 * - 支持 URL query ?status=Pending 自动应用状态筛选（Header 待办徽标跳转入口）
 * - 默认查询参数：{ page: 1, pageSize: 20 }（注意：page 从 1 起，与 Order BC 的 0 起不同）
 */

defineOptions({ name: 'AfterSalesList' })

const route = useRoute()
const router = useRouter()

const STATUS_OPTIONS: { label: string; value: AfterSalesStatus | '' }[] = [
  { label: '全部', value: '' },
  { label: '待处理', value: 'Pending' },
  { label: '已同意', value: 'Approved' },
  { label: '已拒绝', value: 'Rejected' },
  { label: '退货中', value: 'ReturnInProgress' },
  { label: '已退款', value: 'Refunded' },
  { label: '已关闭', value: 'Closed' },
]

const TYPE_OPTIONS: { label: string; value: AfterSalesType | '' }[] = [
  { label: '全部', value: '' },
  { label: '仅退款', value: 'RefundOnly' },
  { label: '退货退款', value: 'ReturnRefund' },
  { label: '换货', value: 'Exchange' },
]

const TYPE_TAG_COLOR: Record<AfterSalesType, 'blue' | 'orange' | 'purple'> = {
  RefundOnly: 'blue',
  ReturnRefund: 'orange',
  Exchange: 'purple',
}

const TYPE_LABEL: Record<AfterSalesType, string> = {
  RefundOnly: '仅退款',
  ReturnRefund: '退货退款',
  Exchange: '换货',
}

const ALL_STATUSES: AfterSalesStatus[] = [
  'Pending',
  'Approved',
  'Rejected',
  'ReturnInProgress',
  'Refunded',
  'Closed',
]

function isAfterSalesStatus(v: unknown): v is AfterSalesStatus {
  return typeof v === 'string' && ALL_STATUSES.includes(v as AfterSalesStatus)
}

const filters = reactive({
  afterSalesNo: '',
  orderNo: '',
  status: '' as AfterSalesStatus | '',
  type: '' as AfterSalesType | '',
  buyerName: '',
  dateRange: undefined as [string, string] | undefined,
})

const dataSource = ref<AfterSalesListItemDto[]>([])
const loading = ref(false)
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (t: number) => `共 ${t} 条`,
})

const columns: TableColumnsType = [
  { title: '售后单号', dataIndex: 'afterSalesNo', key: 'afterSalesNo', width: 160, ellipsis: true },
  { title: '订单号', dataIndex: 'orderNo', key: 'orderNo', width: 160, ellipsis: true },
  { title: '买家', dataIndex: 'buyerName', key: 'buyerName', width: 100, ellipsis: true },
  { title: '商品', dataIndex: 'productName', key: 'productName', width: 180, ellipsis: true },
  { title: 'SKU', dataIndex: 'skuName', key: 'skuName', width: 120, ellipsis: true },
  { title: '数量', dataIndex: 'quantity', key: 'quantity', width: 80, align: 'right' },
  { title: '类型', key: 'type', width: 110 },
  { title: '金额', key: 'amount', width: 120, align: 'right' },
  { title: '状态', key: 'status', width: 110 },
  { title: '申请时间', dataIndex: 'createdAt', key: 'createdAt', width: 170 },
  { title: '操作', key: 'action', width: 110, fixed: 'right' },
]

const hasData = computed(() => dataSource.value.length > 0)

function buildParams(): ListAfterSalesParams {
  const params: ListAfterSalesParams = {
    page: pagination.current,
    pageSize: pagination.pageSize,
  }
  if (filters.afterSalesNo.trim()) params.afterSalesNo = filters.afterSalesNo.trim()
  if (filters.orderNo.trim()) params.orderNo = filters.orderNo.trim()
  if (filters.status) params.status = filters.status
  if (filters.type) params.type = filters.type
  if (filters.buyerName.trim()) params.buyerName = filters.buyerName.trim()
  if (filters.dateRange) {
    const [start, end] = filters.dateRange
    params.startDate = start
    params.endDate = end
  }
  return params
}

async function loadData(): Promise<void> {
  loading.value = true
  try {
    const res = await aftersalesApi.list(buildParams())
    const result = res.data
    dataSource.value = result.items
    pagination.total = result.total
  } catch (e) {
    logger.error('加载售后列表失败', e)
    message.error('加载售后列表失败')
    dataSource.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onSearch(): void {
  pagination.current = 1
  void loadData()
}

function onReset(): void {
  filters.afterSalesNo = ''
  filters.orderNo = ''
  filters.status = ''
  filters.type = ''
  filters.buyerName = ''
  filters.dateRange = undefined
  pagination.current = 1
  void loadData()
}

function onPageChange(p: { current?: number; pageSize?: number }): void {
  if (p.current !== undefined) pagination.current = p.current
  if (p.pageSize !== undefined) {
    pagination.pageSize = p.pageSize
    pagination.current = 1
  }
  void loadData()
}

function onRefresh(): void {
  void loadData()
}

function onDateRangeChange(value: [string, string]): void {
  filters.dateRange = value
}

function currencySymbol(currency: string): string {
  return currency === 'CNY' ? '¥' : '$'
}

function goDetail(record: AfterSalesListItemDto): void {
  router.push(`/after-sales/${record.id}`)
}

function applyStatusFromQuery(): void {
  const q = route.query.status
  if (isAfterSalesStatus(q)) {
    filters.status = q
  }
}

onMounted(() => {
  applyStatusFromQuery()
  void loadData()
})
</script>

<template>
  <div class="aftersales-list-page">
    <Breadcrumb class="aftersales-list-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>售后处理</BreadcrumbItem>
      <BreadcrumbItem>售后列表</BreadcrumbItem>
    </Breadcrumb>

    <!-- 查询区 -->
    <Card class="aftersales-list-filter" :bordered="true">
      <Space :size="12" wrap>
        <div class="aftersales-list-filter-item">
          <label class="aftersales-list-filter-label">售后单号</label>
          <Input
            v-model:value="filters.afterSalesNo"
            placeholder="请输入售后单号"
            allow-clear
            style="width: 200px"
            @press-enter="onSearch"
          />
        </div>
        <div class="aftersales-list-filter-item">
          <label class="aftersales-list-filter-label">订单号</label>
          <Input
            v-model:value="filters.orderNo"
            placeholder="请输入订单号"
            allow-clear
            style="width: 200px"
            @press-enter="onSearch"
          />
        </div>
        <div class="aftersales-list-filter-item">
          <label class="aftersales-list-filter-label">状态</label>
          <Select
            v-model:value="filters.status"
            style="width: 140px"
            placeholder="全部"
          >
            <SelectOption
              v-for="opt in STATUS_OPTIONS"
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </SelectOption>
          </Select>
        </div>
        <div class="aftersales-list-filter-item">
          <label class="aftersales-list-filter-label">类型</label>
          <Select
            v-model:value="filters.type"
            style="width: 140px"
            placeholder="全部"
          >
            <SelectOption
              v-for="opt in TYPE_OPTIONS"
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </SelectOption>
          </Select>
        </div>
        <div class="aftersales-list-filter-item">
          <label class="aftersales-list-filter-label">买家姓名</label>
          <Input
            v-model:value="filters.buyerName"
            placeholder="请输入买家姓名"
            allow-clear
            style="width: 160px"
            @press-enter="onSearch"
          />
        </div>
        <div class="aftersales-list-filter-item">
          <label class="aftersales-list-filter-label">申请时间</label>
          <DateTimeRangePicker
            :value="filters.dateRange"
            show-time
            @change="onDateRangeChange"
          />
        </div>
        <div class="aftersales-list-filter-actions">
          <Button type="primary" :icon="h(SearchOutlined)" @click="onSearch">查询</Button>
          <Button @click="onReset">重置</Button>
          <Button :icon="h(ReloadOutlined)" @click="onRefresh">刷新</Button>
        </div>
      </Space>
    </Card>

    <!-- 售后表格 -->
    <Card class="aftersales-list-table-card" :bordered="true">
      <template #title>
        <span class="aftersales-list-table-title">售后单列表</span>
      </template>
      <Skeleton v-if="loading && !hasData" :title="{ width: '100%' }" :paragraph="{ rows: 8 }" active />
      <EmptyState
        v-else-if="!hasData && !loading"
        description="暂无售后单"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="dataSource"
        :row-key="(record: AfterSalesListItemDto) => record.id"
        :loading="loading"
        :pagination="pagination"
        :scroll="{ x: 1400 }"
        size="middle"
        aria-label="售后单列表"
        class="aftersales-list-table"
        @change="onPageChange as any"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'type'">
            <Tag :color="TYPE_TAG_COLOR[record.type as AfterSalesType]">
              {{ TYPE_LABEL[record.type as AfterSalesType] }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'amount'">
            <span class="aftersales-list-amount">
              {{ formatMoney(record.amount, { symbol: currencySymbol(record.currency) }) }}
            </span>
          </template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="aftersales" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Button
              type="link"
              size="small"
              :icon="h(EyeOutlined)"
              aria-label="查看详情"
              @click="goDetail(record as AfterSalesListItemDto)"
            >
              查看详情
            </Button>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.aftersales-list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.aftersales-list-breadcrumb {
  font-size: 14px;
}
.aftersales-list-filter {
  border-radius: 8px;
}
.aftersales-list-filter-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.aftersales-list-filter-label {
  font-size: 14px;
  color: #595959;
  white-space: nowrap;
}
.aftersales-list-filter-actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.aftersales-list-table-card {
  border-radius: 8px;
}
.aftersales-list-table-title {
  font-size: 16px;
  font-weight: 500;
}
.aftersales-list-table {
  width: 100%;
}
.aftersales-list-amount {
  font-weight: 600;
  color: #fa541c;
}
</style>
