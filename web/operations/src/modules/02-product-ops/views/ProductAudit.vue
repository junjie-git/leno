<!-- web/operations/src/modules/02-product-ops/views/ProductAudit.vue -->
<template>
  <div class="product-audit">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="关键词">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="商品名称 / SKU 编号"
            allow-clear
            style="width: 220px"
            @search="onQuery"
          />
        </a-form-item>
        <a-form-item label="卖家 ID">
          <a-input
            v-model:value="filters.sellerId"
            placeholder="如 SL2024001"
            allow-clear
            style="width: 160px"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="分类">
          <a-select
            v-model:value="filters.categoryId"
            placeholder="全部分类"
            allow-clear
            show-search
            option-filter-prop="label"
            style="width: 200px"
            :options="categoryOptions"
            :loading="categoryLoading"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + C：工具栏与商品表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <a-space>
          <a-button
            type="primary"
            :disabled="selectedRowKeys.length === 0"
            :loading="batchRunning"
            @click="onBatchApprove"
          >
            批量通过
          </a-button>
          <a-button
            danger
            :disabled="selectedRowKeys.length === 0"
            :loading="batchRunning"
            @click="onBatchReject"
          >
            批量驳回
          </a-button>
          <span v-if="selectedRowKeys.length > 0" class="selection-hint">已选 {{ selectedRowKeys.length }} 件</span>
        </a-space>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="fetchProducts">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchProducts" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: ProductDto) => record.id"
        :row-selection="rowSelection"
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
          <template v-if="column.key === 'product'">
            <div class="product-cell">
              <a-image
                :src="record.mainImageUrl || ''"
                :alt="record.title"
                :width="48"
                :height="48"
                style="border-radius: 4px; object-fit: cover"
              >
                <template #fallback>
                  <div class="thumb-fallback" :aria-label="record.title">{{ initials(record.title) }}</div>
                </template>
              </a-image>
              <div class="product-meta">
                <div class="product-title" :title="record.title">{{ record.title }}</div>
                <div class="product-sub">{{ record.id }}</div>
              </div>
            </div>
          </template>
          <template v-else-if="column.key === 'skuCount'">{{ record.skus.length }}</template>
          <template v-else-if="column.key === 'category'">
            <a @click="goCategoryManagement(record.categoryId)">{{ record.categoryName || record.categoryId }}</a>
          </template>
          <template v-else-if="column.key === 'seller'">
            <div class="seller-cell">
              <span>{{ record.sellerName || '—' }}</span>
              <span class="product-sub">{{ record.sellerId }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'priceRange'">{{ priceRange(record.skus) }}</template>
          <template v-else-if="column.key === 'totalStock'">{{ totalStock(record.skus) }}</template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="PRODUCT_STATUS_META[record.status as ProductStatus].color">
              {{ PRODUCT_STATUS_META[record.status as ProductStatus].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'submittedAt'">{{ formatDateTime(record.submittedAt) }}</template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看详情" @click="onViewDetail(record)">详情</a-button>
              <a-button
                type="link"
                size="small"
                :disabled="record.status !== 'PendingAudit'"
                aria-label="审核通过"
                @click="onApprove(record)"
              >
                通过
              </a-button>
              <a-button
                type="link"
                size="small"
                danger
                :disabled="record.status !== 'PendingAudit'"
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
      title="商品详情"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 主图区 -->
          <a-image-preview-group v-if="detailImages.length > 0">
            <a-image
              :src="detailImages[0]"
              :alt="detail.title"
              width="100%"
              style="border-radius: 8px; max-height: 280px; object-fit: cover"
            />
            <div v-if="detailImages.length > 1" class="drawer-thumbs">
              <a-image
                v-for="(img, i) in detailImages"
                :key="img"
                :src="img"
                :alt="`${detail.title} 图片 ${i + 1}`"
                :width="64"
                :height="64"
                style="border-radius: 4px; object-fit: cover"
              />
            </div>
          </a-image-preview-group>
          <div v-else class="img-placeholder">暂无图片</div>

          <!-- SPU 基础信息 -->
          <h3 class="drawer-section-title">SPU 信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="商品 ID">{{ detail.id }}</a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="PRODUCT_STATUS_META[detail.status].color">
                {{ PRODUCT_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="商品名称" :span="2">{{ detail.title }}</a-descriptions-item>
            <a-descriptions-item label="所属分类">{{ detail.categoryName || detail.categoryId }}</a-descriptions-item>
            <a-descriptions-item label="品牌">{{ detail.brandName || '—' }}</a-descriptions-item>
            <a-descriptions-item label="卖家">{{ detail.sellerName || detail.sellerId }}</a-descriptions-item>
            <a-descriptions-item label="卖家 ID">{{ detail.sellerId }}</a-descriptions-item>
            <a-descriptions-item label="提交时间">{{ formatDateTime(detail.submittedAt) }}</a-descriptions-item>
            <a-descriptions-item label="总库存">{{ totalStock(detail.skus) }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.rejectReason" label="驳回原因" :span="2">
              {{ detail.rejectReason }}
            </a-descriptions-item>
          </a-descriptions>

          <!-- SKU 列表（行内库存调整） -->
          <h3 class="drawer-section-title">SKU 列表</h3>
          <a-table
            :columns="skuColumns"
            :data-source="detail.skus"
            :pagination="false"
            size="small"
            row-key="id"
          >
            <template #bodyCell="{ column, record: sku }">
              <template v-if="column.key === 'price'">{{ formatMoney(sku.price) }}</template>
              <template v-else-if="column.key === 'stock'">{{ sku.stock }}</template>
              <template v-else-if="column.key === 'operate'">
                <a-space>
                  <a-input-number
                    v-model:value="stockInputs[sku.id]"
                    :min="-99999"
                    :max="99999"
                    size="small"
                    style="width: 104px"
                    placeholder="±数量"
                  />
                  <IdempotencyButton
                    type="primary"
                    size="small"
                    :loading="adjustingSkuId === sku.id"
                    aria-label="调整库存"
                    @click="onAdjustStock(sku)"
                  >
                    调整
                  </IdempotencyButton>
                  <IdempotencyButton
                    size="small"
                    :loading="replenishingSkuId === sku.id"
                    aria-label="补货"
                    @click="onReplenish(sku)"
                  >
                    补货
                  </IdempotencyButton>
                </a-space>
              </template>
            </template>
          </a-table>

          <!-- 审核历史 -->
          <h3 class="drawer-section-title">审核历史</h3>
          <a-timeline class="audit-timeline">
            <a-timeline-item v-for="(item, index) in auditTimeline" :key="index" :color="item.color">
              {{ item.children }}
            </a-timeline-item>
          </a-timeline>
        </template>
      </a-spin>
    </a-drawer>

    <!-- 区域 E：驳回对话框（单条 / 批量共用） -->
    <a-modal
      v-model:open="rejectModalOpen"
      :title="rejectMode === 'single' ? '驳回商品' : `批量驳回（${selectedRowKeys.length} 件）`"
      :confirm-loading="rejectSubmitting"
      :ok-button-props="{ disabled: !rejectReasonValid, danger: true }"
      ok-text="提交驳回"
      cancel-text="取消"
      @ok="onSubmitReject"
    >
      <p v-if="rejectMode === 'single' && rejectTarget" class="reject-target">
        商品：{{ rejectTarget.title }}（{{ rejectTarget.id }}）
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
          placeholder="请输入驳回原因（至少 5 个字，将通知卖家修改后重新提交）"
          @blur="rejectTouched = true"
        />
      </a-form-item>
    </a-modal>

    <!-- 通过确认（单条） -->
    <ConfirmDialog
      :open="approveConfirmOpen"
      title="审核通过"
      :content="`确认通过商品「${approveTarget?.title ?? ''}」并上架？`"
      @confirm="onConfirmApprove"
      @cancel="approveConfirmOpen = false"
    />

    <!-- 批量通过确认 -->
    <ConfirmDialog
      :open="batchApproveOpen"
      title="批量通过"
      :content="`确认批量通过选中的 ${selectedRowKeys.length} 件商品并上架？将逐件提交并汇总结果。`"
      @confirm="onConfirmBatchApprove"
      @cancel="batchApproveOpen = false"
    />

    <!-- 批量操作结果反馈 -->
    <a-modal
      v-model:open="batchResultOpen"
      :title="batchResultTitle"
      :footer="null"
      width="520"
    >
      <template v-if="batchResult">
        <a-alert
          v-if="batchResult.failed === 0"
          type="success"
          show-icon
          :message="`全部成功：${batchResult.succeeded}/${batchResult.total}`"
        />
        <a-alert
          v-else
          type="warning"
          show-icon
          :message="`部分成功：成功 ${batchResult.succeeded} 条，失败 ${batchResult.failed} 条（共 ${batchResult.total} 条）`"
        />
        <div v-if="batchResult.failures.length > 0" class="batch-failures">
          <div class="batch-failures-title">失败明细</div>
          <ul class="batch-failures-list">
            <li v-for="f in batchResult.failures" :key="f.id">
              <span class="product-sub">{{ f.id }}</span>
              <span>{{ f.reason }}</span>
            </li>
          </ul>
        </div>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime, formatMoney } from '@/shared/utils/format'
import { productApi } from '../api/product.api'
import { categoryApi } from '../api/category.api'
import type {
  BatchOperationResultDto,
  ProductDto,
  ProductStatus,
  ProductStatusOption,
  SkuDto,
} from '../types/product.dto'
import type { CategoryDto } from '../types/category.dto'

/**
 * 商品审核页（02-product-ops）
 *
 * 四区布局：筛选条 / 操作工具栏 / 商品表格 / 详情抽屉 + 驳回 Modal。
 * - 默认查询 Status=PendingAudit 前 20 条
 * - 通过 / 驳回后刷新当前页；批量操作串行提交并汇总 BatchOperationResultDto
 * - 详情抽屉支持 SKU 行内库存调整（delta）与补货，局部刷新库存
 */

const route = useRoute()
const router = useRouter()

/** 商品状态展示映射（md §6 状态色：待审核橙 / 已上架绿 / 已驳回红 / 已下架灰） */
const PRODUCT_STATUS_META: Record<ProductStatus, { label: string; color: string }> = {
  Draft: { label: '草稿', color: 'default' },
  PendingAudit: { label: '待审核', color: 'warning' },
  Active: { label: '已上架', color: 'success' },
  Rejected: { label: '已驳回', color: 'error' },
  OffShelf: { label: '已下架', color: 'default' },
}

const statusOptions: ProductStatusOption[] = [
  { label: '待审核', value: 'PendingAudit' },
  { label: '已上架', value: 'Active' },
  { label: '已驳回', value: 'Rejected' },
  { label: '已下架', value: 'OffShelf' },
  { label: '草稿', value: 'Draft' },
]

interface FilterState {
  keyword: string
  sellerId: string
  status?: ProductStatus
  categoryId?: string
}

const filters = reactive<FilterState>({
  keyword: '',
  sellerId: '',
  status: 'PendingAudit',
  categoryId: undefined,
})

const hasActiveFilters = computed(
  () => Boolean(filters.keyword || filters.sellerId || filters.categoryId || filters.status),
)

const emptyDescription = computed(() => (filters.status ? '该状态下暂无商品' : '暂无商品'))

// ---------- 分类筛选选项 ----------
const categoryOptions = ref<{ label: string; value: string }[]>([])
const categoryLoading = ref(false)

async function loadCategoryOptions() {
  categoryLoading.value = true
  try {
    const { data } = await categoryApi.tree()
    const options: { label: string; value: string }[] = []
    const walk = (nodes: CategoryDto[], depth: number) => {
      for (const node of nodes) {
        options.push({ label: `${'　'.repeat(depth)}${node.name}`, value: node.id })
        if (node.children?.length) walk(node.children, depth + 1)
      }
    }
    walk(data ?? [], 0)
    categoryOptions.value = options
  } catch {
    categoryOptions.value = []
  } finally {
    categoryLoading.value = false
  }
}

// ---------- 列表加载 ----------
const tableData = ref<ProductDto[]>([])
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
  { title: '商品信息', key: 'product', width: 260, ellipsis: true },
  { title: 'SKU 数', key: 'skuCount', width: 80, align: 'center' },
  { title: '所属分类', key: 'category', width: 120 },
  { title: '卖家', key: 'seller', width: 150, ellipsis: true },
  { title: '价格区间', key: 'priceRange', width: 160 },
  { title: '库存', key: 'totalStock', width: 90, align: 'right' },
  { title: '状态', key: 'status', width: 100 },
  { title: '提交时间', key: 'submittedAt', width: 170 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' },
]

const skuColumns: TableColumnsType = [
  { title: '规格', dataIndex: 'spec', key: 'spec' },
  { title: '价格', key: 'price', width: 100 },
  { title: '当前库存', key: 'stock', width: 90, align: 'right' },
  { title: '库存调整（delta 正补负扣）', key: 'operate', width: 260 },
]

async function fetchProducts() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof productApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const keyword = filters.keyword.trim()
    const sellerId = filters.sellerId.trim()
    if (keyword) params.keyword = keyword
    if (sellerId) params.sellerId = sellerId
    if (filters.status) params.status = filters.status
    if (filters.categoryId) params.categoryId = filters.categoryId

    const { data } = await productApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载商品列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchProducts()
}

function onReset() {
  filters.keyword = ''
  filters.sellerId = ''
  filters.status = 'PendingAudit'
  filters.categoryId = undefined
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchProducts()
}

// ---------- 展示辅助 ----------
function initials(title: string): string {
  return (title || '?').slice(0, 1)
}

function priceRange(skus: SkuDto[]): string {
  if (!skus.length) return '—'
  const prices = skus.map((s) => s.price)
  const min = Math.min(...prices)
  const max = Math.max(...prices)
  return min === max ? formatMoney(min) : `${formatMoney(min)} ~ ${formatMoney(max)}`
}

function totalStock(skus: SkuDto[]): number {
  return skus.reduce((sum, s) => sum + s.stock, 0)
}

function goCategoryManagement(categoryId: string) {
  void router.push({ path: '/product-ops/category-management', query: { categoryId } })
}

// ---------- 批量选择 ----------
const selectedRowKeys = ref<string[]>([])

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys.map(String)
}

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: onSelectChange,
}))

// ---------- 审核通过 ----------
const approveConfirmOpen = ref(false)
const approveTarget = ref<ProductDto | null>(null)

function onApprove(record: ProductDto) {
  approveTarget.value = record
  approveConfirmOpen.value = true
}

/** 审核类错误统一分流：并发冲突提示刷新，其余透出后端 message */
function showAuditError(e: unknown, fallback: string) {
  if (e instanceof ConcurrencyError) {
    message.warning('商品状态已变更，请刷新列表')
    return
  }
  message.error(e instanceof Error && e.message ? e.message : fallback)
}

async function onConfirmApprove() {
  if (!approveTarget.value) return
  const target = approveTarget.value
  try {
    await productApi.approve(target.id)
    approveConfirmOpen.value = false
    message.success(`商品「${target.title}」已审核通过并上架`)
    await fetchProducts()
  } catch (e) {
    approveConfirmOpen.value = false
    showAuditError(e, '审核操作失败，请重试')
  } finally {
    approveTarget.value = null
  }
}

// ---------- 审核驳回（单条 / 批量共用 Modal） ----------
const rejectModalOpen = ref(false)
const rejectMode = ref<'single' | 'batch'>('single')
const rejectTarget = ref<ProductDto | null>(null)
const rejectReason = ref('')
const rejectTouched = ref(false)
const rejectSubmitting = ref(false)

const rejectReasonValid = computed(() => {
  const len = rejectReason.value.trim().length
  return len >= 5 && len <= 200
})

function onOpenReject(record: ProductDto) {
  rejectMode.value = 'single'
  rejectTarget.value = record
  rejectReason.value = ''
  rejectTouched.value = false
  rejectModalOpen.value = true
}

function onBatchReject() {
  if (selectedRowKeys.value.length === 0) return
  rejectMode.value = 'batch'
  rejectTarget.value = null
  rejectReason.value = ''
  rejectTouched.value = false
  rejectModalOpen.value = true
}

async function onSubmitReject() {
  rejectTouched.value = true
  if (!rejectReasonValid.value) return

  rejectSubmitting.value = true
  try {
    const reason = rejectReason.value.trim()
    if (rejectMode.value === 'single' && rejectTarget.value) {
      await productApi.reject(rejectTarget.value.id, { reason })
      message.success('商品已驳回')
    } else {
      const result = await productApi.batchReject(selectedRowKeys.value, { reason })
      showBatchResult(result, '批量驳回')
    }
    rejectModalOpen.value = false
    selectedRowKeys.value = []
    await fetchProducts()
  } catch (e) {
    showAuditError(e, '驳回操作失败，请重试')
  } finally {
    rejectSubmitting.value = false
  }
}

// ---------- 批量通过 ----------
const batchApproveOpen = ref(false)
const batchRunning = ref(false)

function onBatchApprove() {
  if (selectedRowKeys.value.length === 0) return
  batchApproveOpen.value = true
}

async function onConfirmBatchApprove() {
  batchApproveOpen.value = false
  batchRunning.value = true
  try {
    const result = await productApi.batchApprove(selectedRowKeys.value)
    showBatchResult(result, '批量通过')
    selectedRowKeys.value = []
    await fetchProducts()
  } finally {
    batchRunning.value = false
  }
}

// ---------- 批量结果反馈 ----------
const batchResultOpen = ref(false)
const batchResultTitle = ref('批量操作结果')
const batchResult = ref<BatchOperationResultDto | null>(null)

function showBatchResult(result: BatchOperationResultDto, action: string) {
  batchResult.value = result
  batchResultTitle.value = `${action}结果`
  batchResultOpen.value = true
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<ProductDto | null>(null)
const stockInputs = reactive<Record<string, number | null>>({})
const adjustingSkuId = ref('')
const replenishingSkuId = ref('')

const detailImages = computed<string[]>(() => {
  const p = detail.value
  if (!p) return []
  if (p.imageUrls?.length) return p.imageUrls.filter(Boolean)
  return p.mainImageUrl ? [p.mainImageUrl] : []
})

/** 审核动作 → 时间线颜色与文案 */
const AUDIT_ACTION_META: Record<string, { label: string; color: string }> = {
  Submitted: { label: '提交审核', color: 'blue' },
  Approved: { label: '审核通过', color: 'green' },
  Rejected: { label: '审核驳回', color: 'red' },
  OffShelf: { label: '下架', color: 'gray' },
  StockAdjusted: { label: '库存调整', color: 'orange' },
  Replenished: { label: '补货', color: 'orange' },
}

const auditTimeline = computed<{ color: string; children: string }[]>(() => {
  const p = detail.value
  if (!p) return []

  if (p.auditLogs?.length) {
    return p.auditLogs.map((log) => {
      const meta = AUDIT_ACTION_META[log.action]
      return {
        color: meta?.color ?? 'gray',
        children: `${meta?.label ?? log.action} · ${log.operator} · ${formatDateTime(log.createdAt)}${
          log.reason ? `：${log.reason}` : ''
        }`,
      }
    })
  }

  // 后端未返回审核历史时，按列表字段合成基础时间线
  const items: { color: string; children: string }[] = [
    { color: 'blue', children: `提交审核 · ${formatDateTime(p.submittedAt)}` },
  ]
  if (p.status === 'Active') {
    items.unshift({ color: 'green', children: '审核通过 · 商品已上架' })
  }
  if (p.status === 'Rejected' || p.rejectReason) {
    items.unshift({ color: 'red', children: `审核驳回${p.rejectReason ? `：${p.rejectReason}` : ''}` })
  }
  if (p.status === 'OffShelf') {
    items.unshift({ color: 'gray', children: '商品已下架' })
  }
  return items
})

function onViewDetail(record: ProductDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as ProductDto
  Object.keys(stockInputs).forEach((key) => {
    delete stockInputs[key]
  })
  drawerOpen.value = true
}

/** 局部刷新：抽屉与列表行同步更新 SKU 库存 */
function applySkuChange(
  productId: string,
  skuId: string,
  updated: SkuDto | null | undefined,
  fallbackDelta?: number,
) {
  const applyTo = (product: ProductDto | null) => {
    if (!product) return
    const sku = product.skus.find((s) => s.id === skuId)
    if (!sku) return
    if (updated) {
      sku.stock = updated.stock
      sku.price = updated.price
    } else if (typeof fallbackDelta === 'number') {
      sku.stock += fallbackDelta
    }
  }
  applyTo(detail.value)
  applyTo(tableData.value.find((p) => p.id === productId) ?? null)
}

async function onAdjustStock(sku: SkuDto) {
  const product = detail.value
  if (!product) return
  const delta = stockInputs[sku.id]
  if (delta === null || delta === undefined || delta === 0) {
    message.warning('请输入非 0 的调整数量（正数补库存、负数扣库存）')
    return
  }

  adjustingSkuId.value = sku.id
  try {
    const { data } = await productApi.updateSkuStock(product.id, sku.id, {
      delta,
      reason: '运营后台人工调整',
    })
    applySkuChange(product.id, sku.id, data, delta)
    message.success(`SKU「${sku.spec || sku.id}」库存已调整 ${delta > 0 ? '+' : ''}${delta}`)
    stockInputs[sku.id] = null
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '库存调整失败，请重试')
  } finally {
    adjustingSkuId.value = ''
  }
}

async function onReplenish(sku: SkuDto) {
  const product = detail.value
  if (!product) return
  const quantity = stockInputs[sku.id]
  if (quantity === null || quantity === undefined || quantity <= 0) {
    message.warning('请输入大于 0 的补货数量')
    return
  }

  replenishingSkuId.value = sku.id
  try {
    const { data } = await productApi.replenishSku(sku.id, { quantity })
    applySkuChange(product.id, sku.id, data, quantity)
    message.success(`SKU「${sku.spec || sku.id}」已补货 ${quantity}`)
    stockInputs[sku.id] = null
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '补货失败，请重试')
  } finally {
    replenishingSkuId.value = ''
  }
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

  const header = ['商品ID', '商品名称', 'SKU数', '所属分类', '卖家', '价格区间', '总库存', '状态', '提交时间']
  const rows = tableData.value.map((p) => [
    p.id,
    p.title,
    String(p.skus.length),
    p.categoryName || p.categoryId,
    p.sellerName ? `${p.sellerName}(${p.sellerId})` : p.sellerId,
    priceRange(p.skus),
    String(totalStock(p.skus)),
    PRODUCT_STATUS_META[p.status].label,
    formatDateTime(p.submittedAt),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `商品审核导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化 ----------
onMounted(() => {
  // 支持分类管理页「关联商品数」跳转携带分类筛选
  const queryCategoryId = typeof route.query.categoryId === 'string' ? route.query.categoryId : ''
  if (queryCategoryId) filters.categoryId = queryCategoryId

  void loadCategoryOptions()
  void fetchProducts()
})
</script>

<style scoped>
.product-audit {
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

.selection-hint {
  font-size: 12px;
  color: #8c8c8c;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.product-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.thumb-fallback {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  background: #f0f0f0;
  border-radius: 4px;
  color: #8c8c8c;
  font-size: 16px;
}

.product-meta {
  min-width: 0;
}

.product-title {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.seller-cell {
  display: flex;
  flex-direction: column;
}

.drawer-thumbs {
  display: flex;
  gap: 8px;
  margin-top: 8px;
  flex-wrap: wrap;
}

.img-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 180px;
  background: #f0f0f0;
  border-radius: 8px;
  color: #8c8c8c;
}

.drawer-section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.audit-timeline {
  margin-top: 8px;
}

.reject-target {
  margin-bottom: 12px;
  font-weight: 500;
}

.batch-failures {
  margin-top: 12px;
}

.batch-failures-title {
  margin-bottom: 8px;
  font-weight: 500;
}

.batch-failures-list {
  max-height: 220px;
  padding-left: 20px;
  overflow-y: auto;
}

.batch-failures-list li {
  margin-bottom: 4px;
}
</style>
