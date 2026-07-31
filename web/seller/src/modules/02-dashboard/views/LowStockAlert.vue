<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
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
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { WarningOutlined } from '@ant-design/icons-vue'
import type { LowStockItemDto } from '../types/dashboard.dto'
import { dashboardApi } from '../api/dashboard.api'
import { EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'

/**
 * 库存预警页
 *
 * 路由 /dashboard/low-stock，调真实后端 GET /api/seller/dashboard/low-stock?threshold=
 * 数据经 SellerShop ACL 从 Product 域 gRPC 获取。
 */

const loading = ref(true)
const threshold = ref(10)
const dataSource = ref<LowStockItemDto[]>([])

interface StockStatus {
  label: string
  color: string
}

function deriveStatus(stock: number, thresholdVal: number): StockStatus {
  if (stock < 5) return { label: '紧急', color: 'error' }
  if (stock < 10) return { label: '警告', color: 'warning' }
  if (stock < thresholdVal) return { label: '偏低', color: 'processing' }
  return { label: '正常', color: 'success' }
}

const filteredData = computed<LowStockItemDto[]>(() => {
  return [...dataSource.value].sort((a, b) => a.stock - b.stock)
})

const alertCount = computed(() => filteredData.value.length)

const columns: TableColumnsType = [
  { title: '商品名称', dataIndex: 'productName', key: 'productName', width: 200, ellipsis: true },
  { title: 'SKU', dataIndex: 'skuName', key: 'skuName', width: 140 },
  {
    title: '当前库存',
    dataIndex: 'stock',
    key: 'stock',
    width: 120,
    sorter: (a: LowStockItemDto, b: LowStockItemDto) => a.stock - b.stock,
    defaultSortOrder: 'ascend',
  },
  { title: '预警阈值', dataIndex: 'threshold', key: 'threshold', width: 120 },
  { title: '状态', key: 'status', width: 100 },
]

async function loadData(): Promise<void> {
  loading.value = true
  try {
    dataSource.value = await dashboardApi.getLowStock(threshold.value)
  } catch (e) {
    logger.error('加载低库存列表失败', e)
    message.error('加载低库存列表失败')
    dataSource.value = []
  } finally {
    loading.value = false
  }
}

watch(threshold, () => {
  void loadData()
})

onMounted(() => {
  void loadData()
})
</script>

<template>
  <div class="low-stock-page">
    <Breadcrumb class="low-stock-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>工作台</BreadcrumbItem>
      <BreadcrumbItem>库存预警</BreadcrumbItem>
    </Breadcrumb>

    <Alert
      v-if="!loading && alertCount > 0"
      type="warning"
      show-icon
      :message="`当前有 ${alertCount} 个 SKU 库存低于阈值 ${threshold}，建议尽快补货`"
      class="low-stock-alert"
    />

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
