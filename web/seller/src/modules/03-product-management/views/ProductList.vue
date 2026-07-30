<script setup lang="ts">
import { ref, computed, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Button,
  Space,
  Input,
  Select,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import {
  PlusOutlined,
  SearchOutlined,
  ReloadOutlined,
  EditOutlined,
  AppstoreOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
} from '@ant-design/icons-vue'
import { productApi } from '../api/product.api'
import type { ProductListItemDto, ProductStatus, ListProductsParams } from '../types/product.dto'
import { StatusTag, EmptyState, ShopStatusGuard, ConfirmDialog } from '@/shared/components'
import { formatDateTime, formatNumber } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * 商品列表页
 *
 * 路由：/products
 * 功能：关键字搜索 / 状态筛选 / 分类筛选 / 分页 / 上下架 / 提交审核 / 重新上架。
 */

const router = useRouter()

const loading = ref(true)
const submitting = ref(false)
const dataSource = ref<ProductListItemDto[]>([])
const total = ref(0)
const currentPage = ref(1)
const currentPageSize = ref(10)

/** 查询参数 */
const keyword = ref('')
const statusFilter = ref<ProductStatus | undefined>(undefined)
const categoryFilter = ref<string | undefined>(undefined)

/** 下架确认弹窗 */
const takeDownModalOpen = ref(false)
const takeDownTarget = ref<ProductListItemDto | null>(null)
const takeDownVersion = ref(0)
const takeDownLoading = ref(false)

/** 状态筛选选项 */
const statusOptions: Array<{ label: string; value: ProductStatus }> = [
  { label: '草稿', value: 'Draft' },
  { label: '待审核', value: 'PendingReview' },
  { label: '已上架', value: 'Approved' },
  { label: '已下架', value: 'TakenDown' },
  { label: '已驳回', value: 'Rejected' },
]

/** 分类筛选选项（叶子分类，由后端分类服务提供，当前为静态选项） */
const categoryOptions: Array<{ label: string; value: string }> = [
  { label: '服装 / 男装 / T恤', value: 'cat-tshirt' },
  { label: '服装 / 男装 / 衬衫', value: 'cat-shirt' },
  { label: '服装 / 女装 / 连衣裙', value: 'cat-dress' },
  { label: '数码 / 手机', value: 'cat-phone' },
  { label: '数码 / 配件 / 数据线', value: 'cat-cable' },
  { label: '家居 / 家纺', value: 'cat-home' },
]

/** 统计卡片数据 */
const stats = computed(() => {
  const all = dataSource.value
  return {
    total: total.value,
    approved: all.filter((p) => p.status === 'Approved').length,
    pending: all.filter((p) => p.status === 'PendingReview').length,
    takenDown: all.filter((p) => p.status === 'TakenDown').length,
  }
})

/** 表格列定义 */
const columns: TableColumnsType = [
  {
    title: '商品信息',
    dataIndex: 'name',
    key: 'name',
    width: 280,
    ellipsis: true,
  },
  {
    title: '分类',
    dataIndex: 'categoryName',
    key: 'categoryName',
    width: 140,
    ellipsis: true,
  },
  {
    title: '价格区间',
    dataIndex: 'priceRange',
    key: 'priceRange',
    width: 140,
  },
  {
    title: 'SKU 数',
    dataIndex: 'skuCount',
    key: 'skuCount',
    width: 90,
    align: 'right',
  },
  {
    title: '库存',
    dataIndex: 'totalStock',
    key: 'totalStock',
    width: 100,
    align: 'right',
  },
  {
    title: '销量',
    dataIndex: 'salesCount',
    key: 'salesCount',
    width: 100,
    align: 'right',
  },
  {
    title: '状态',
    dataIndex: 'status',
    key: 'status',
    width: 100,
  },
  {
    title: '创建时间',
    dataIndex: 'createdAt',
    key: 'createdAt',
    width: 170,
  },
  {
    title: '操作',
    key: 'action',
    width: 240,
    fixed: 'right',
  },
]

/** 库存预警样式 */
function stockClass(stock: number): string {
  if (stock === 0) return 'stock-danger'
  if (stock < 50) return 'stock-warn'
  return ''
}

/** 构造查询参数 */
function buildParams(): ListProductsParams {
  const params: ListProductsParams = {
    page: currentPage.value,
    pageSize: currentPageSize.value,
  }
  if (keyword.value.trim()) params.keyword = keyword.value.trim()
  if (statusFilter.value) params.status = statusFilter.value
  if (categoryFilter.value) params.categoryId = categoryFilter.value
  return params
}

/** 加载商品列表 */
async function loadList(): Promise<void> {
  loading.value = true
  try {
    const result = await productApi.list(buildParams())
    dataSource.value = result.items
    total.value = result.total
  } catch (e) {
    logger.error('加载商品列表失败', e)
    message.error('加载商品列表失败，请稍后重试')
    dataSource.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

/** 查询按钮 */
function onSearch(): void {
  currentPage.value = 1
  void loadList()
}

/** 重置筛选 */
function onReset(): void {
  keyword.value = ''
  statusFilter.value = undefined
  categoryFilter.value = undefined
  currentPage.value = 1
  void loadList()
}

/** 分页变化 */
function onTableChange(pagination: { current?: number; pageSize?: number }): void {
  if (pagination.current) currentPage.value = pagination.current
  if (pagination.pageSize) currentPageSize.value = pagination.pageSize
  void loadList()
}

/** 跳转新增 */
function goCreate(): void {
  router.push('/products/new')
}

/** 跳转编辑 */
function goEdit(record: ProductListItemDto): void {
  router.push(`/products/${record.id}/edit`)
}

/** 跳转 SKU 管理 */
function goSku(record: ProductListItemDto): void {
  router.push(`/products/${record.id}/skus`)
}

/** 提交审核 */
async function onSubmitReview(record: ProductListItemDto): Promise<void> {
  submitting.value = true
  try {
    await productApi.submitForReview(record.id)
    message.success(`「${record.name}」已提交审核`)
    await loadList()
  } catch (e) {
    logger.error('提交审核失败', e)
    if (e instanceof ConcurrencyError) {
      message.warning('商品已被他人修改，已自动刷新')
      await loadList()
    } else {
      message.error('提交审核失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}

/** 打开下架弹窗（先拉详情获取 version 用于乐观锁） */
async function openTakeDownModal(record: ProductListItemDto): Promise<void> {
  takeDownTarget.value = record
  takeDownLoading.value = true
  try {
    const detail = await productApi.get(record.id)
    takeDownVersion.value = detail.version
    takeDownModalOpen.value = true
  } catch (e) {
    logger.error('加载商品详情失败', e)
    message.error('加载商品详情失败，请稍后重试')
    takeDownTarget.value = null
  } finally {
    takeDownLoading.value = false
  }
}

/** 确认下架（ConfirmDialog emit confirm 传入 reason） */
async function confirmTakeDown(reason?: string): Promise<void> {
  const target = takeDownTarget.value
  if (!target) return
  const trimmed = (reason ?? '').trim()
  if (!trimmed) {
    message.warning('请输入下架原因')
    return
  }
  takeDownLoading.value = true
  try {
    await productApi.takeDown(target.id, { reason: trimmed, version: takeDownVersion.value })
    message.success(`「${target.name}」已下架`)
    takeDownModalOpen.value = false
    takeDownTarget.value = null
    await loadList()
  } catch (e) {
    logger.error('下架失败', e)
    if (e instanceof ConcurrencyError) {
      message.warning('商品已被他人修改，已自动刷新')
      takeDownModalOpen.value = false
      await loadList()
    } else {
      message.error('下架失败，请稍后重试')
    }
  } finally {
    takeDownLoading.value = false
  }
}

/** 取消下架 */
function cancelTakeDown(): void {
  takeDownModalOpen.value = false
  takeDownTarget.value = null
}

/** 重新上架 */
async function onRepublish(record: ProductListItemDto): Promise<void> {
  submitting.value = true
  try {
    await productApi.republish(record.id)
    message.success(`「${record.name}」已重新上架`)
    await loadList()
  } catch (e) {
    logger.error('重新上架失败', e)
    if (e instanceof ConcurrencyError) {
      message.warning('商品已被他人修改，已自动刷新')
      await loadList()
    } else {
      message.error('重新上架失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="product-list-page">
    <Breadcrumb class="product-list-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>商品管理</BreadcrumbItem>
      <BreadcrumbItem>商品列表</BreadcrumbItem>
    </Breadcrumb>

    <!-- 统计卡片 -->
    <div class="product-list-stats">
      <Card class="stat-card" :bordered="true" size="small">
        <Skeleton v-if="loading" active :paragraph="{ rows: 1 }" />
        <div v-else>
          <div class="stat-label">商品总数</div>
          <div class="stat-value">{{ formatNumber(stats.total) }}</div>
        </div>
      </Card>
      <Card class="stat-card stat-success" :bordered="true" size="small">
        <Skeleton v-if="loading" active :paragraph="{ rows: 1 }" />
        <div v-else>
          <div class="stat-label">在售中</div>
          <div class="stat-value">{{ formatNumber(stats.approved) }}</div>
        </div>
      </Card>
      <Card class="stat-card stat-warning" :bordered="true" size="small">
        <Skeleton v-if="loading" active :paragraph="{ rows: 1 }" />
        <div v-else>
          <div class="stat-label">待审核</div>
          <div class="stat-value">{{ formatNumber(stats.pending) }}</div>
        </div>
      </Card>
      <Card class="stat-card stat-default" :bordered="true" size="small">
        <Skeleton v-if="loading" active :paragraph="{ rows: 1 }" />
        <div v-else>
          <div class="stat-label">已下架</div>
          <div class="stat-value">{{ formatNumber(stats.takenDown) }}</div>
        </div>
      </Card>
    </div>

    <!-- 筛选栏 -->
    <Card class="product-list-filter" :bordered="true">
      <Space :size="12" wrap>
        <Input
          v-model:value="keyword"
          placeholder="请输入商品名称"
          allow-clear
          style="width: 240px"
          @press-enter="onSearch"
        >
          <template #suffix>
            <SearchOutlined class="product-list-search-icon" @click="onSearch" />
          </template>
        </Input>
        <div class="filter-item">
          <span class="filter-label">状态</span>
          <Select
            v-model:value="statusFilter"
            placeholder="全部状态"
            allow-clear
            style="width: 160px"
            :options="statusOptions"
            @change="onSearch"
          />
        </div>
        <div class="filter-item">
          <span class="filter-label">分类</span>
          <Select
            v-model:value="categoryFilter"
            placeholder="全部分类"
            allow-clear
            style="width: 200px"
            :options="categoryOptions"
            @change="onSearch"
          />
        </div>
        <Button @click="onReset">重置</Button>
        <div class="filter-spacer" />
        <ShopStatusGuard requires="canPublish" fallback-text="店铺当前状态不允许上架新商品">
          <Button
            v-permission="'product:create'"
            type="primary"
            :icon="h(PlusOutlined)"
            @click="goCreate"
          >
            新增商品
          </Button>
        </ShopStatusGuard>
      </Space>
    </Card>

    <!-- 表格 -->
    <Card class="product-list-table-card" :bordered="true">
      <template #title>
        <Space>
          <span class="product-list-table-title">商品列表</span>
          <Button size="small" :icon="h(ReloadOutlined)" @click="loadList">刷新</Button>
        </Space>
      </template>

      <EmptyState
        v-if="!loading && dataSource.length === 0"
        description="暂无商品，点击「新增商品」开始上架"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="dataSource"
        :row-key="(record: ProductListItemDto) => record.id"
        :loading="loading"
        :pagination="{
          current: currentPage,
          pageSize: currentPageSize,
          total,
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        :scroll="{ x: 1300 }"
        size="middle"
        @change="onTableChange as any"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <div class="cell-product">
              <img
                v-if="record.coverImage"
                class="cell-thumb"
                :src="record.coverImage"
                :alt="record.name"
              />
              <div v-else class="cell-thumb cell-thumb-placeholder">
                {{ record.name.slice(0, 1) }}
              </div>
              <div class="cell-product-info">
                <a class="cell-product-name" @click="goEdit(record)">{{ record.name }}</a>
                <div class="cell-product-id">ID: {{ record.id }}</div>
              </div>
            </div>
          </template>

          <template v-else-if="column.key === 'categoryName'">
            {{ record.categoryName || '-' }}
          </template>

          <template v-else-if="column.key === 'priceRange'">
            <span class="price-range">{{ record.priceRange || '-' }}</span>
          </template>

          <template v-else-if="column.key === 'skuCount'">
            {{ formatNumber(record.skuCount) }}
          </template>

          <template v-else-if="column.key === 'totalStock'">
            <span :class="['stock-num', stockClass(record.totalStock)]">
              {{ formatNumber(record.totalStock) }}
            </span>
          </template>

          <template v-else-if="column.key === 'salesCount'">
            <span class="sales-num">{{ formatNumber(record.salesCount) }} 件</span>
          </template>

          <template v-else-if="column.key === 'status'">
            <StatusTag type="product" :status="record.status" />
          </template>

          <template v-else-if="column.key === 'createdAt'">
            <span class="cell-time">{{ formatDateTime(record.createdAt) }}</span>
          </template>

          <template v-else-if="column.key === 'action'">
            <Space :size="4" wrap>
              <Button
                v-permission="'product:edit'"
                type="link"
                size="small"
                :icon="h(EditOutlined)"
                @click="goEdit(record)"
              >
                编辑
              </Button>
              <Button
                type="link"
                size="small"
                :icon="h(AppstoreOutlined)"
                @click="goSku(record)"
              >
                SKU
              </Button>
              <Button
                v-if="record.status === 'Draft' || record.status === 'Rejected'"
                v-permission="'product:submit-review'"
                type="link"
                size="small"
                :loading="submitting"
                @click="onSubmitReview(record)"
              >
                提交审核
              </Button>
              <Button
                v-if="record.status === 'Approved'"
                v-permission="'product:take-down'"
                type="link"
                size="small"
                danger
                :loading="submitting"
                @click="openTakeDownModal(record)"
              >
                <ArrowDownOutlined />
                下架
              </Button>
              <Button
                v-if="record.status === 'TakenDown'"
                v-permission="'product:republish'"
                type="link"
                size="small"
                :loading="submitting"
                @click="onRepublish(record)"
              >
                <ArrowUpOutlined />
                重新上架
              </Button>
            </Space>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 下架确认弹窗 -->
    <ConfirmDialog
      :open="takeDownModalOpen"
      danger
      title="确认下架"
      :content="`确认下架「${takeDownTarget?.name ?? ''}」？下架后商品将不再对买家可见，已生成订单不受影响。此操作可逆，可重新上架。`"
      :require-input="{ label: '下架原因', min: 1, max: 200 }"
      @confirm="confirmTakeDown"
      @cancel="cancelTakeDown"
    />
  </div>
</template>

<style scoped>
.product-list-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.product-list-breadcrumb {
  font-size: 14px;
}
.product-list-stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.stat-card {
  border-radius: 8px;
}
.stat-card.stat-success .stat-value {
  color: #52c41a;
}
.stat-card.stat-warning .stat-value {
  color: #faad14;
}
.stat-card.stat-default .stat-value {
  color: #8c8c8c;
}
.stat-label {
  font-size: 13px;
  color: #8c8c8c;
  margin-bottom: 6px;
}
.stat-value {
  font-size: 26px;
  font-weight: 600;
  color: #1677ff;
  line-height: 1.2;
}
.product-list-filter {
  border-radius: 8px;
}
.filter-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.filter-label {
  font-size: 14px;
  color: #595959;
  white-space: nowrap;
}
.filter-spacer {
  flex: 1;
}
.product-list-search-icon {
  color: #8c8c8c;
  cursor: pointer;
}
.product-list-table-card {
  border-radius: 8px;
}
.product-list-table-title {
  font-size: 16px;
  font-weight: 500;
}
.cell-product {
  display: flex;
  align-items: center;
  gap: 12px;
}
.cell-thumb {
  width: 48px;
  height: 48px;
  border-radius: 6px;
  object-fit: cover;
  background: #f5f5f5;
  flex-shrink: 0;
  border: 1px solid #f0f0f0;
}
.cell-thumb-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  color: #8c8c8c;
  font-size: 18px;
  font-weight: 500;
}
.cell-product-info {
  min-width: 0;
}
.cell-product-name {
  font-size: 14px;
  color: #000000d9;
  font-weight: 500;
  line-height: 1.4;
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 220px;
  cursor: pointer;
}
.cell-product-name:hover {
  color: #1677ff;
}
.cell-product-id {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 2px;
  font-family: 'SF Mono', Consolas, monospace;
}
.price-range {
  font-weight: 600;
  color: #000000d9;
}
.stock-num {
  font-weight: 500;
}
.stock-num.stock-warn {
  color: #faad14;
}
.stock-num.stock-danger {
  color: #ff4d4f;
}
.sales-num {
  color: #595959;
}
.cell-time {
  color: #595959;
  font-size: 13px;
}
.take-down-confirm-text {
  font-size: 14px;
  color: #000000d9;
  margin-bottom: 8px;
}
.take-down-confirm-desc {
  font-size: 13px;
  color: #595959;
  line-height: 1.6;
  margin-bottom: 12px;
}
.take-down-reason-input {
  margin-top: 4px;
}

@media (max-width: 1199px) {
  .product-list-stats {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
