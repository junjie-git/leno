<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Tag,
  Alert,
  Skeleton,
  InputNumber,
  Space,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { WarningOutlined, ExclamationCircleOutlined } from '@ant-design/icons-vue'
import type { LowStockItemDto } from '../types/dashboard.dto'
import { EmptyState } from '@/shared/components'

/**
 * 库存预警页（后端端点 /api/seller/dashboard/low-stock 缺失，走 mock 兜底）
 *
 * 与 spec BE-2 对齐：UI 加「后端未就绪」徽标，数据使用本地 mock。
 */

const loading = ref(true)
const threshold = ref(10)
const dataSource = ref<LowStockItemDto[]>([])

/** mock 低库存 SKU 数据 */
const MOCK_LOW_STOCK: LowStockItemDto[] = [
  { productId: 'P001', productName: '无线蓝牙耳机 Pro', skuId: 'SKU001', skuName: '星空黑', stock: 2, threshold: 10 },
  { productId: 'P001', productName: '无线蓝牙耳机 Pro', skuId: 'SKU002', skuName: '冰川蓝', stock: 5, threshold: 10 },
  { productId: 'P002', productName: '智能手表 Series 6', skuId: 'SKU003', skuName: '银色 44mm', stock: 3, threshold: 10 },
  { productId: 'P003', productName: '便携充电宝 20000mAh', skuId: 'SKU004', skuName: '白色', stock: 8, threshold: 10 },
  { productId: 'P004', productName: '手机保护壳', skuId: 'SKU005', skuName: '透明款', stock: 4, threshold: 10 },
  { productId: 'P005', productName: 'USB-C 数据线 1m', skuId: 'SKU006', skuName: '黑色编织', stock: 6, threshold: 10 },
  { productId: 'P006', productName: '蓝牙音箱 Mini', skuId: 'SKU007', skuName: '深灰', stock: 9, threshold: 10 },
  { productId: 'P007', productName: '降噪入耳式耳机', skuId: 'SKU008', skuName: '白色', stock: 1, threshold: 10 },
]

interface StockStatus {
  label: string
  color: string
}

/** 根据当前库存与阈值派生库存状态 */
function deriveStatus(stock: number, thresholdVal: number): StockStatus {
  if (stock < 5) return { label: '紧急', color: 'error' }
  if (stock < 10) return { label: '警告', color: 'warning' }
  if (stock < thresholdVal) return { label: '偏低', color: 'processing' }
  return { label: '正常', color: 'success' }
}

const filteredData = computed<LowStockItemDto[]>(() => {
  return dataSource.value
    .filter((item) => item.stock < threshold.value)
    .sort((a, b) => a.stock - b.stock)
})

const alertCount = computed(() => filteredData.value.length)

const columns: TableColumnsType = [
  {
    title: '商品名称',
    dataIndex: 'productName',
    key: 'productName',
    width: 200,
    ellipsis: true,
  },
  {
    title: 'SKU',
    dataIndex: 'skuName',
    key: 'skuName',
    width: 140,
  },
  {
    title: '当前库存',
    dataIndex: 'stock',
    key: 'stock',
    width: 120,
    sorter: (a: LowStockItemDto, b: LowStockItemDto) => a.stock - b.stock,
    defaultSortOrder: 'ascend',
  },
  {
    title: '预警阈值',
    dataIndex: 'threshold',
    key: 'threshold',
    width: 120,
  },
  {
    title: '状态',
    key: 'status',
    width: 100,
  },
]

function loadMockData(): void {
  loading.value = true
  // 使用 setTimeout 模拟异步加载（后端端点缺失，mock 兜底）
  setTimeout(() => {
    dataSource.value = [...MOCK_LOW_STOCK]
    loading.value = false
  }, 400)
}

onMounted(() => {
  loadMockData()
})
</script>

<template>
  <div class="low-stock-page">
    <Breadcrumb class="low-stock-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>工作台</BreadcrumbItem>
      <BreadcrumbItem>库存预警</BreadcrumbItem>
    </Breadcrumb>

    <!-- 后端未就绪徽标 -->
    <div class="low-stock-backend-tag">
      <Tag color="warning">
        <ExclamationCircleOutlined />
        后端未就绪
      </Tag>
      <span class="low-stock-backend-hint">该端点（/api/seller/dashboard/low-stock）暂未提供，当前数据为 mock 兜底</span>
    </div>

    <!-- 预警统计条 -->
    <Alert
      v-if="!loading && alertCount > 0"
      type="warning"
      show-icon
      :message="`当前有 ${alertCount} 个 SKU 库存低于阈值 ${threshold}，建议尽快补货`"
      class="low-stock-alert"
    />

    <!-- 筛选栏 -->
    <Card class="low-stock-filter" :bordered="true">
      <Space :size="16" align="center">
        <span class="low-stock-filter-label">
          <WarningOutlined class="low-stock-filter-icon" />
          预警阈值
        </span>
        <InputNumber
          v-model:value="threshold"
          :min="1"
          :max="999"
          :step="1"
          aria-label="库存预警阈值"
        />
      </Space>
    </Card>

    <!-- 低库存表格 -->
    <Card class="low-stock-table-card" :bordered="true">
      <template #title>
        <span class="low-stock-table-title">低库存商品列表</span>
      </template>
      <Skeleton v-if="loading" :title="{ width: '100%' }" :paragraph="{ rows: 8 }" active />
      <EmptyState
        v-else-if="alertCount === 0"
        description="库存充足，当前无低库存商品"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="filteredData"
        :row-key="(record: LowStockItemDto) => record.skuId"
        :pagination="{ pageSize: 10, showSizeChanger: false }"
        size="middle"
        aria-label="低库存商品表格"
        class="low-stock-table"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <Tag :color="deriveStatus(record.stock, threshold).color">
              {{ deriveStatus(record.stock, threshold).label }}
            </Tag>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.low-stock-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.low-stock-breadcrumb {
  font-size: 14px;
}
.low-stock-backend-tag {
  display: flex;
  align-items: center;
  gap: 8px;
}
.low-stock-backend-hint {
  font-size: 13px;
  color: #8c8c8c;
}
.low-stock-alert {
  border-radius: 8px;
}
.low-stock-filter {
  border-radius: 8px;
}
.low-stock-filter-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  color: #595959;
}
.low-stock-filter-icon {
  color: #faad14;
  font-size: 16px;
}
.low-stock-table-card {
  border-radius: 8px;
}
.low-stock-table-title {
  font-size: 16px;
  font-weight: 500;
}
.low-stock-table {
  width: 100%;
}
</style>
