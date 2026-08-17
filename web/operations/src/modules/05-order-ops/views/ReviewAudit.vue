<!-- web/operations/src/modules/05-order-ops/views/ReviewAudit.vue -->
<template>
  <div class="review-audit">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="商品名称">
          <a-input-search
            v-model:value="filters.productName"
            placeholder="输入商品名称关键词"
            allow-clear
            style="width: 220px"
            @search="onQuery"
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
        <a-form-item label="评分">
          <a-select
            v-model:value="filters.rating"
            placeholder="全部评分"
            allow-clear
            style="width: 110px"
            :options="ratingOptions"
          />
        </a-form-item>
        <a-form-item label="评价时间">
          <DateTimeRangePicker :value="timeRange" show-time @change="onTimeRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + C：工具栏与评价表格 -->
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
            @click="onOpenHide('batch')"
          >
            批量隐藏
          </a-button>
          <span v-if="selectedRowKeys.length > 0" class="selection-hint">
            已选 {{ selectedRowKeys.length }} 条
          </span>
        </a-space>
        <a-button :loading="loading" @click="fetchReviews">刷新</a-button>
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
        :row-key="(record: ReviewDto) => record.id"
        :row-selection="rowSelection"
        :row-class-name="rowClassName"
        :scroll="{ x: 1200 }"
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
          <template v-if="column.key === 'content'">
            <a-tooltip :title="record.content" placement="topLeft">
              <div class="content-cell">{{ record.content }}</div>
            </a-tooltip>
          </template>
          <template v-else-if="column.key === 'product'">
            <div class="product-cell" :title="record.productName">{{ record.productName }}</div>
          </template>
          <template v-else-if="column.key === 'buyer'">
            <div class="cell-stack">
              <span>{{ record.buyerName || '—' }}</span>
              <span class="cell-sub mono">{{ record.userId }}</span>
            </div>
          </template>
          <template v-else-if="column.key === 'rating'">
            <a-rate :value="record.rating" disabled :aria-label="`${record.rating} 星`" />
          </template>
          <template v-else-if="column.key === 'imageCount'">
            <span v-if="record.imageUrls.length > 0" class="image-count">
              <PictureOutlined /> {{ record.imageUrls.length }}
            </span>
            <span v-else class="cell-sub">0</span>
          </template>
          <template v-else-if="column.key === 'sellerReply'">
            <span :class="record.sellerReply ? 'reply-yes' : 'cell-sub'">
              {{ record.sellerReply ? '有' : '无' }}
            </span>
          </template>
          <template v-else-if="column.key === 'createdAt'">{{ formatDateTime(record.createdAt) }}</template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="REVIEW_STATUS_META[record.status].color">
              {{ REVIEW_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看详情" @click="onViewDetail(record)">详情</a-button>
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'Approved'"
                :loading="approvingId === record.id"
                aria-label="审核通过"
                @click="onApprove(record)"
              >
                通过
              </a-button>
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'Hidden'"
                aria-label="隐藏评价"
                @click="onOpenHide('single', record)"
              >
                隐藏
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="评价详情"
      placement="right"
      width="640"
      :destroy-on-close="true"
    >
      <a-spin :spinning="!detail">
        <template v-if="detail">
          <!-- 评价全文 -->
          <h3 class="drawer-section-title">评价内容</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="商品" :span="2">{{ detail.productName }}</a-descriptions-item>
            <a-descriptions-item label="买家">{{ detail.buyerName || detail.userId }}</a-descriptions-item>
            <a-descriptions-item label="评分">
              <a-rate :value="detail.rating" disabled />
            </a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="REVIEW_STATUS_META[detail.status].color">
                {{ REVIEW_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="评价时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
            <a-descriptions-item label="评价全文" :span="2">
              <div class="review-content">{{ detail.content }}</div>
            </a-descriptions-item>
          </a-descriptions>

          <!-- 图片预览组 -->
          <h3 class="drawer-section-title">
            <PictureOutlined /> 评价图片
          </h3>
          <a-image-preview-group v-if="detail.imageUrls.length > 0">
            <a-image
              v-for="(img, i) in detail.imageUrls"
              :key="img"
              :src="img"
              :alt="`${detail.content.slice(0, 12)} 图片 ${i + 1}`"
              :width="80"
              :height="80"
              style="border-radius: 4px; object-fit: cover; margin-right: 8px"
            />
          </a-image-preview-group>
          <EmptyState v-else description="暂无图片" />

          <!-- 卖家回复块 -->
          <h3 class="drawer-section-title">卖家回复</h3>
          <div v-if="detail.sellerReply" class="seller-reply-block">
            <div class="reply-content">{{ detail.sellerReply }}</div>
            <div class="cell-sub" :aria-label="detail.sellerRepliedAt">
              {{ formatDateTime(detail.sellerRepliedAt) }}
            </div>
          </div>
          <EmptyState v-else description="卖家暂未回复" />
        </template>
      </a-spin>
    </a-drawer>

    <!-- 区域 E：隐藏 Modal（单条 / 批量共用） -->
    <a-modal
      v-model:open="hideModalOpen"
      :title="hideMode === 'single' ? '隐藏评价' : `批量隐藏（${selectedRowKeys.length} 条）`"
      :confirm-loading="hideSubmitting"
      :ok-button-props="{ disabled: !hideFormValid, danger: true }"
      ok-text="确认隐藏"
      cancel-text="取消"
      @ok="onSubmitHide"
    >
      <p class="hide-impact">
        <ExclamationCircleOutlined /> 隐藏后该评价将对买家端不可见，仅运营可查看；隐藏可逆，可重新通过。
      </p>
      <a-form-item v-if="hideMode === 'single' && hideTarget" label="评价摘要">
        <div class="content-cell">{{ hideTarget.content }}</div>
      </a-form-item>
      <a-form-item
        label="原因分类"
        required
        :validate-status="hideTouched && !hideReasonCategory ? 'error' : ''"
        :help="hideTouched && !hideReasonCategory ? '请选择隐藏原因分类' : ''"
      >
        <a-radio-group v-model:value="hideReasonCategory">
          <a-radio v-for="(label, value) in REVIEW_REASON_CATEGORY_META" :key="value" :value="value">
            {{ label }}
          </a-radio>
        </a-radio-group>
      </a-form-item>
      <a-form-item label="详细原因">
        <a-textarea
          v-model:value="hideRemark"
          :rows="3"
          :maxlength="200"
          show-count
          placeholder="补充说明（选填，便于审核留档）"
        />
      </a-form-item>
    </a-modal>

    <!-- 单条通过确认 -->
    <ConfirmDialog
      :open="approveConfirmOpen"
      title="审核通过"
      :content="`确认通过该评价？通过后评价将对买家端可见。`"
      @confirm="onConfirmApprove"
      @cancel="approveConfirmOpen = false"
    />

    <!-- 批量通过确认 -->
    <ConfirmDialog
      :open="batchApproveOpen"
      title="批量通过"
      :content="`确认批量通过选中的 ${selectedRowKeys.length} 条评价？将逐条提交并汇总结果。`"
      @confirm="onConfirmBatchApprove"
      @cancel="batchApproveOpen = false"
    />

    <!-- 批量操作结果反馈 -->
    <a-modal v-model:open="batchResultOpen" :title="batchResultTitle" :footer="null" width="520">
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
              <span class="cell-sub mono">{{ f.id }}</span>
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
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ExclamationCircleOutlined, PictureOutlined } from '@ant-design/icons-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, DateTimeRangePicker, EmptyState } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { reviewApi } from '../api/review.api'
import type { BatchReviewResultDto, ReviewDto, ReviewStatus } from '../types/review.dto'
import { REVIEW_REASON_CATEGORY_META, REVIEW_STATUS_META } from '../types/review.dto'

/**
 * 评价审核页（05-order-ops）
 *
 * 布局：筛选条 / 工具栏（批量通过 / 批量隐藏）/ 评价表格 / 详情抽屉 + 隐藏 Modal。
 * - 默认查询待审核评价（Status=Pending）
 * - 隐藏需选择原因分类（垃圾广告 / 辱骂 / 虚假 / 其他），隐藏可逆可重新通过
 * - 批量操作串行提交并汇总 BatchReviewResultDto
 * - 低分（1-2 星）行背景浅红提示（md §6）
 */

interface FilterState {
  productName: string
  status?: ReviewStatus
  rating?: number
}

const filters = reactive<FilterState>({
  productName: '',
  status: 'Pending',
  rating: undefined,
})

const timeRange = ref<[string, string] | undefined>(undefined)

const statusOptions = (Object.keys(REVIEW_STATUS_META) as ReviewStatus[]).map((value) => ({
  label: REVIEW_STATUS_META[value].label,
  value,
}))

const ratingOptions = [1, 2, 3, 4, 5].map((value) => ({ label: `${value} 星`, value }))

const hasActiveFilters = computed(
  () => Boolean(filters.productName || filters.status || filters.rating || timeRange.value),
)

const emptyDescription = computed(() =>
  filters.status ? `该状态下暂无评价（${REVIEW_STATUS_META[filters.status].label}）` : '暂无评价',
)

// ---------- 列表加载 ----------
const tableData = ref<ReviewDto[]>([])
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
  { title: '评价摘要', key: 'content', width: 260, ellipsis: true },
  { title: '商品', key: 'product', width: 170, ellipsis: true },
  { title: '买家', key: 'buyer', width: 130, ellipsis: true },
  { title: '评分', key: 'rating', width: 130 },
  { title: '图片数', key: 'imageCount', width: 80, align: 'center' },
  { title: '卖家回复', key: 'sellerReply', width: 90, align: 'center' },
  { title: '状态', key: 'status', width: 90 },
  { title: '评价时间', key: 'createdAt', width: 170 },
  { title: '操作', key: 'action', width: 170, fixed: 'right' },
]

/** 低分（1-2 星）行背景浅红提示 */
function rowClassName(record: ReviewDto): string {
  return record.rating <= 2 ? 'low-rating-row' : ''
}

async function fetchReviews() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof reviewApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const productName = filters.productName.trim()
    if (productName) params.productName = productName
    if (filters.status) params.status = filters.status
    if (filters.rating) params.rating = filters.rating
    if (timeRange.value) {
      params.fromTime = timeRange.value[0]
      params.toTime = timeRange.value[1]
    }

    const { data } = await reviewApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载评价列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchReviews()
}

function onReset() {
  filters.productName = ''
  filters.status = 'Pending'
  filters.rating = undefined
  timeRange.value = undefined
  onQuery()
}

function onTimeRangeChange(value: [string, string]) {
  timeRange.value = value
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchReviews()
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

// ---------- 审核错误分流 ----------
function showAuditError(e: unknown) {
  if (e instanceof ConcurrencyError) {
    message.warning('评价状态已变更，请刷新列表')
    return
  }
  message.error(e instanceof Error && e.message ? e.message : '审核操作失败，请重试')
}

// ---------- 审核通过（单条） ----------
const approveConfirmOpen = ref(false)
const approveTarget = ref<ReviewDto | null>(null)
const approvingId = ref('')

function onApprove(record: ReviewDto) {
  approveTarget.value = record
  approveConfirmOpen.value = true
}

async function onConfirmApprove() {
  approveConfirmOpen.value = false
  const target = approveTarget.value
  if (!target) return

  approvingId.value = target.id
  try {
    await reviewApi.approve(target.id)
    message.success('评价已审核通过，买家端可见')
    target.status = 'Approved'
    if (detail.value?.id === target.id) detail.value.status = 'Approved'
  } catch (e) {
    showAuditError(e)
  } finally {
    approvingId.value = ''
    approveTarget.value = null
  }
}

// ---------- 隐藏（单条 / 批量共用 Modal） ----------
const hideModalOpen = ref(false)
const hideMode = ref<'single' | 'batch'>('single')
const hideTarget = ref<ReviewDto | null>(null)
const hideReasonCategory = ref<string | null>(null)
const hideRemark = ref('')
const hideTouched = ref(false)
const hideSubmitting = ref(false)

const hideFormValid = computed(() => hideReasonCategory.value !== null)

function onOpenHide(mode: 'single' | 'batch', record?: ReviewDto) {
  hideMode.value = mode
  hideTarget.value = record ?? null
  hideReasonCategory.value = null
  hideRemark.value = ''
  hideTouched.value = false
  hideModalOpen.value = true
}

async function onSubmitHide() {
  hideTouched.value = true
  if (!hideFormValid.value) return

  hideSubmitting.value = true
  const body = {
    reasonCategory: hideReasonCategory.value as keyof typeof REVIEW_REASON_CATEGORY_META,
    remark: hideRemark.value.trim() || undefined,
  }

  try {
    if (hideMode.value === 'single' && hideTarget.value) {
      const target = hideTarget.value
      await reviewApi.hide(target.id, body)
      message.success('评价已隐藏，买家端不可见')
      target.status = 'Hidden'
      if (detail.value?.id === target.id) detail.value.status = 'Hidden'
    } else {
      const result = await reviewApi.batchHide(selectedRowKeys.value, body)
      showBatchResult(result, '批量隐藏')
      selectedRowKeys.value = []
      await fetchReviews()
    }
    hideModalOpen.value = false
  } catch (e) {
    showAuditError(e)
  } finally {
    hideSubmitting.value = false
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
    const result = await reviewApi.batchApprove(selectedRowKeys.value)
    showBatchResult(result, '批量通过')
    selectedRowKeys.value = []
    await fetchReviews()
  } finally {
    batchRunning.value = false
  }
}

// ---------- 批量结果反馈 ----------
const batchResultOpen = ref(false)
const batchResultTitle = ref('批量操作结果')
const batchResult = ref<BatchReviewResultDto | null>(null)

function showBatchResult(result: BatchReviewResultDto, action: string) {
  batchResult.value = result
  batchResultTitle.value = `${action}结果`
  batchResultOpen.value = true
}

// ---------- 详情抽屉 ----------
const drawerOpen = ref(false)
const detail = ref<ReviewDto | null>(null)

function onViewDetail(record: ReviewDto) {
  detail.value = JSON.parse(JSON.stringify(record)) as ReviewDto
  drawerOpen.value = true
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchReviews()
})
</script>

<style scoped>
.review-audit {
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

.table-card :deep(.low-rating-row) {
  background: #fff1f0;
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

.mono {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 13px;
}

.cell-stack {
  display: flex;
  flex-direction: column;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.content-cell {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: 14px;
  color: #000000d9;
  max-width: 240px;
}

.product-cell {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.image-count {
  font-size: 13px;
  color: #1677ff;
}

.reply-yes {
  color: #52c41a;
}

.drawer-section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.review-content {
  white-space: pre-wrap;
  line-height: 1.7;
  color: #000000d9;
}

.seller-reply-block {
  padding: 12px;
  background: #f5f5f5;
  border-radius: 6px;
}

.reply-content {
  margin-bottom: 6px;
  color: #595959;
  line-height: 1.7;
}

.hide-impact {
  margin-bottom: 12px;
  font-size: 13px;
  color: #ff4d4f;
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
