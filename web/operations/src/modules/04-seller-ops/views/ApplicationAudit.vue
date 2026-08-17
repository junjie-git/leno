<!-- web/operations/src/modules/04-seller-ops/views/ApplicationAudit.vue -->
<template>
  <div class="application-audit">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="店铺名称">
          <a-input
            v-model:value="filters.keyword"
            placeholder="请输入店铺名称"
            allow-clear
            style="width: 200px"
            @press-enter="onQuery"
          />
        </a-form-item>
        <a-form-item label="申请人">
          <a-input
            v-model:value="filters.applicant"
            placeholder="请输入申请人姓名"
            allow-clear
            style="width: 160px"
            @press-enter="onQuery"
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
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + C：工具栏与申请表格 -->
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
          <span v-if="selectedRowKeys.length > 0" class="selection-hint">
            已选 {{ selectedRowKeys.length }} 项
          </span>
        </a-space>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="fetchShops">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchShops" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: ShopDto) => record.id"
        :row-selection="rowSelection"
        :scroll="{ x: 1180 }"
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
          <template v-if="column.key === 'shop'">
            <div class="shop-cell">
              <div class="cell-main" :title="record.name">{{ record.name }}</div>
              <div class="cell-sub">{{ record.sellerAccount }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'applicant'">
            <div class="shop-cell">
              <span>{{ record.ownerName }}</span>
              <div class="cell-sub">{{ record.contactPhone || '—' }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'qualCount'">
            <a-tag v-if="record.qualifications?.length" :color="qualCountColor(record)">
              {{ qualSummary(record) }}
            </a-tag>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'submittedAt'">
            {{ formatDateTime(record.submittedAt) }}
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="SHOP_STATUS_META[record.status as ShopStatus].color">
              {{ SHOP_STATUS_META[record.status as ShopStatus].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="查看审核详情" @click="onOpenDrawer(record)">
                详情
              </a-button>
              <template v-if="record.status === 'PendingReview'">
                <a-tooltip
                  :title="isQualificationsReady(record) ? '' : '资质未全部审核通过，暂不能通过入驻申请'"
                >
                  <span>
                    <a-button
                      type="link"
                      size="small"
                      :disabled="!isQualificationsReady(record)"
                      aria-label="审核通过"
                      @click="onApprove(record)"
                    >
                      通过
                    </a-button>
                  </span>
                </a-tooltip>
                <a-button
                  type="link"
                  size="small"
                  danger
                  aria-label="驳回申请"
                  @click="onOpenReject(record)"
                >
                  驳回
                </a-button>
              </template>
              <a-button
                v-else-if="record.status === 'Active'"
                type="link"
                size="small"
                aria-label="查看店铺治理"
                @click="goGovernance(record.id)"
              >
                查看治理
              </a-button>
              <span v-else class="cell-sub">已终审</span>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：审核抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="detail ? `入驻审核详情 · ${detail.name}` : '入驻审核详情'"
      placement="right"
      width="720"
      :destroy-on-close="true"
    >
      <a-spin :spinning="drawerLoading">
        <template v-if="detail">
          <!-- 店铺信息 -->
          <h3 class="drawer-section-title">店铺信息</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="店铺 ID">{{ detail.id }}</a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="SHOP_STATUS_META[detail.status].color">
                {{ SHOP_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="店铺名称" :span="2">{{ detail.name }}</a-descriptions-item>
            <a-descriptions-item label="申请人">{{ detail.ownerName }}</a-descriptions-item>
            <a-descriptions-item label="卖家账号">{{ detail.sellerAccount }}</a-descriptions-item>
            <a-descriptions-item label="联系电话">{{ detail.contactPhone || '—' }}</a-descriptions-item>
            <a-descriptions-item label="主营类目">{{ detail.mainCategory }}</a-descriptions-item>
            <a-descriptions-item label="申请提交时间" :span="2">
              {{ formatDateTime(detail.submittedAt) }}
            </a-descriptions-item>
          </a-descriptions>

          <!-- 资质列表 -->
          <h3 class="drawer-section-title">资质材料（{{ approvedQualCount }} / {{ qualifications.length }} 已审核）</h3>
          <div v-if="qualifications.length === 0" class="qual-empty">
            <EmptyState description="该店铺未上传资质" />
          </div>
          <div v-for="qual in qualifications" :key="qual.id" class="qual-item">
            <div class="qual-info">
              <div class="qual-type" :aria-label="`资质类型 ${qual.type}`">{{ qual.type }}</div>
              <div class="qual-meta">
                {{ qual.fileName }} ｜ {{ formatDateTime(qual.submittedAt) }}
              </div>
              <div v-if="qual.rejectReason" class="qual-reject-reason">驳回原因：{{ qual.rejectReason }}</div>
            </div>
            <div class="qual-actions">
              <a-tag :color="QUAL_STATUS_META[qual.status].color">
                {{ QUAL_STATUS_META[qual.status].label }}
              </a-tag>
              <a
                :href="qual.fileUrl"
                target="_blank"
                rel="noopener noreferrer"
                class="qual-preview-link"
                :aria-label="`预览资质 ${qual.type}`"
              >
                预览
              </a>
              <template v-if="detail.status === 'PendingReview'">
                <IdempotencyButton
                  size="small"
                  :loading="approvingQualId === qual.id"
                  aria-label="资质审核通过"
                  @click="onApproveQualification(qual)"
                >
                  通过
                </IdempotencyButton>
                <IdempotencyButton
                  size="small"
                  danger
                  :loading="rejectingQualId === qual.id"
                  aria-label="资质驳回"
                  @click="onOpenQualReject(qual)"
                >
                  驳回
                </IdempotencyButton>
              </template>
            </div>
          </div>
        </template>
      </a-spin>
      <template #footer>
        <a-space v-if="detail?.status === 'PendingReview'">
          <a-button danger aria-label="驳回入驻申请" @click="onOpenReject(detail)">驳回申请</a-button>
          <a-tooltip
            :title="allQualificationsApproved ? '' : `请先完成所有资质审核（${approvedQualCount}/${qualifications.length} 已通过）`"
          >
            <span>
              <IdempotencyButton
                :loading="approveSubmitting"
                :disabled="!allQualificationsApproved"
                aria-label="通过入驻申请"
                @click="onApprove(detail)"
              >
                通过审核
              </IdempotencyButton>
            </span>
          </a-tooltip>
        </a-space>
        <a-space v-else-if="detail">
          <a-button
            v-if="detail.status === 'Active'"
            type="primary"
            aria-label="查看店铺治理"
            @click="goGovernance(detail.id)"
          >
            查看店铺治理
          </a-button>
          <span class="cell-sub">该申请已完成审核</span>
        </a-space>
      </template>
    </a-drawer>

    <!-- 区域 E：驳回对话框（单条 / 批量共用） -->
    <a-modal
      v-model:open="rejectModalOpen"
      :title="rejectMode === 'single' ? '驳回入驻申请' : `批量驳回（${selectedRowKeys.length} 项）`"
      :confirm-loading="rejectSubmitting"
      :ok-button-props="{ disabled: !rejectReasonValid, danger: true }"
      ok-text="提交驳回"
      cancel-text="取消"
      @ok="onSubmitReject"
    >
      <p v-if="rejectMode === 'single' && rejectTarget" class="reject-target">
        店铺：{{ rejectTarget.name }}（{{ rejectTarget.ownerName }}）
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
          placeholder="请输入驳回原因（至少 5 个字，将通知申请人修改后重新提交）"
          @blur="rejectTouched = true"
        />
      </a-form-item>
    </a-modal>

    <!-- 单条通过确认 -->
    <ConfirmDialog
      :open="approveConfirmOpen"
      title="审核通过"
      :content="`确认通过「${approveTarget?.name ?? ''}」的入驻申请？店铺将进入营业状态。`"
      @confirm="onConfirmApprove"
      @cancel="approveConfirmOpen = false"
    />

    <!-- 批量通过确认 -->
    <ConfirmDialog
      :open="batchApproveOpen"
      title="批量通过"
      :content="`确认批量通过选中的 ${selectedRowKeys.length} 项入驻申请？将逐项提交并汇总结果。`"
      @confirm="onConfirmBatchApprove"
      @cancel="batchApproveOpen = false"
    />

    <!-- 资质驳回（填写原因） -->
    <ConfirmDialog
      :open="qualRejectOpen"
      danger
      title="驳回资质"
      :content="`确认驳回资质「${qualRejectTarget?.type ?? ''}」？驳回原因将通知申请人。`"
      :require-input="{ label: '驳回原因', min: 5, max: 200 }"
      @confirm="onConfirmQualReject"
      @cancel="qualRejectOpen = false"
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
          :message="`部分成功：成功 ${batchResult.succeeded} 项，失败 ${batchResult.failed} 项（共 ${batchResult.total} 项）`"
        />
        <div v-if="batchResult.failures.length > 0" class="batch-failures">
          <div class="batch-failures-title">失败明细</div>
          <ul class="batch-failures-list">
            <li v-for="f in batchResult.failures" :key="f.id">
              <span class="cell-sub">{{ f.id }}</span>
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
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { shopApi } from '../api/shop.api'
import type {
  QualificationDto,
  QualificationStatus,
  ShopDto,
  ShopStatus,
} from '../types/shop.dto'

/**
 * 入驻审核页（04-seller-ops）
 *
 * 四区布局：筛选条 / 操作工具栏 / 申请表格 / 审核抽屉 + 驳回 Modal。
 * - 默认查询 Status=PendingReview 前 20 条
 * - 通过前置校验：全部资质 Approved 才允许店铺级通过（disabled + tooltip 说明）
 * - 批量操作串行提交并汇总结果（BatchOperationResultDto 复用 02-product-ops 结构）
 */
interface BatchOperationFailureDto {
  id: string
  reason: string
}

interface BatchOperationResultDto {
  total: number
  succeeded: number
  failed: number
  failures: BatchOperationFailureDto[]
}

const router = useRouter()

/** 店铺状态展示映射（md §6 状态色） */
const SHOP_STATUS_META: Record<ShopStatus, { label: string; color: string }> = {
  PendingReview: { label: '待审核', color: 'warning' },
  Active: { label: '已通过', color: 'success' },
  Rejected: { label: '已驳回', color: 'error' },
  Suspended: { label: '已暂停', color: 'warning' },
  Closed: { label: '已关闭', color: 'default' },
}

/** 资质状态展示映射 */
const QUAL_STATUS_META: Record<QualificationStatus, { label: string; color: string }> = {
  PendingReview: { label: '待审核', color: 'warning' },
  Approved: { label: '已通过', color: 'success' },
  Rejected: { label: '已驳回', color: 'error' },
}

const statusOptions: { label: string; value: ShopStatus }[] = [
  { label: '待审核', value: 'PendingReview' },
  { label: '已通过', value: 'Active' },
  { label: '已驳回', value: 'Rejected' },
  { label: '已暂停', value: 'Suspended' },
  { label: '已关闭', value: 'Closed' },
]

interface FilterState {
  keyword: string
  applicant: string
  status?: ShopStatus
}

const filters = reactive<FilterState>({
  keyword: '',
  applicant: '',
  status: 'PendingReview',
})

const hasActiveFilters = computed(() => Boolean(filters.keyword || filters.applicant || filters.status))

const emptyDescription = computed(() => (filters.status ? '该状态下暂无入驻申请' : '暂无入驻申请'))

// ---------- 列表加载 ----------
const tableData = ref<ShopDto[]>([])
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
  { title: '店铺名称', key: 'shop', width: 220, ellipsis: true },
  { title: '申请人', key: 'applicant', width: 160, ellipsis: true },
  { title: '主营类目', dataIndex: 'mainCategory', key: 'mainCategory', width: 120 },
  { title: '资质数', key: 'qualCount', width: 100, align: 'center' },
  { title: '申请时间', key: 'submittedAt', width: 170 },
  { title: '状态', key: 'status', width: 100 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' },
]

async function fetchShops() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof shopApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const keyword = filters.keyword.trim()
    const applicant = filters.applicant.trim()
    if (keyword) params.keyword = keyword
    if (applicant) params.applicant = applicant
    if (filters.status) params.status = filters.status

    const { data } = await shopApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载入驻申请失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchShops()
}

function onReset() {
  filters.keyword = ''
  filters.applicant = ''
  filters.status = 'PendingReview'
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchShops()
}

// ---------- 资质摘要与前置校验 ----------
function approvedCount(quals: QualificationDto[]): number {
  return quals.filter((q) => q.status === 'Approved').length
}

function qualSummary(record: ShopDto): string {
  const quals = record.qualifications ?? []
  return `${approvedCount(quals)} / ${quals.length}`
}

function qualCountColor(record: ShopDto): string {
  const quals = record.qualifications ?? []
  return approvedCount(quals) === quals.length ? 'processing' : 'warning'
}

/** 店铺级通过前置校验：资质列表非空且全部 Approved（列表缺省资质数据时放行，抽屉内强校验） */
function isQualificationsReady(record: ShopDto): boolean {
  if (!record.qualifications) return true
  return record.qualifications.length > 0 && approvedCount(record.qualifications) === record.qualifications.length
}

function goGovernance(shopId: string) {
  void router.push({ path: '/seller-ops/shop-governance', query: { shopId } })
}

// ---------- 批量选择（仅待审核行可勾选） ----------
const selectedRowKeys = ref<string[]>([])

function onSelectChange(keys: (string | number)[]) {
  selectedRowKeys.value = keys.map(String)
}

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: onSelectChange,
  getCheckboxProps: (record: ShopDto) => ({ disabled: record.status !== 'PendingReview' }),
}))

// ---------- 审核错误统一分流 ----------
function showAuditError(e: unknown, fallback: string) {
  if (e instanceof ConcurrencyError) {
    message.warning('申请状态已变更，请刷新列表')
    return
  }
  message.error(e instanceof Error && e.message ? e.message : fallback)
}

// ---------- 审核通过 ----------
const approveConfirmOpen = ref(false)
const approveTarget = ref<ShopDto | null>(null)
const approveSubmitting = ref(false)

function onApprove(record: ShopDto) {
  approveTarget.value = record
  approveConfirmOpen.value = true
}

async function onConfirmApprove() {
  const target = approveTarget.value
  if (!target) return
  approveConfirmOpen.value = false
  approveSubmitting.value = true
  try {
    await shopApi.approve(target.id)
    message.success(`店铺「${target.name}」入驻申请已通过`)
    drawerOpen.value = false
    selectedRowKeys.value = selectedRowKeys.value.filter((key) => key !== target.id)
    await fetchShops()
  } catch (e) {
    showAuditError(e, '审核操作失败，请重试')
  } finally {
    approveSubmitting.value = false
    approveTarget.value = null
  }
}

// ---------- 审核驳回（单条 / 批量共用 Modal） ----------
const rejectModalOpen = ref(false)
const rejectMode = ref<'single' | 'batch'>('single')
const rejectTarget = ref<ShopDto | null>(null)
const rejectReason = ref('')
const rejectTouched = ref(false)
const rejectSubmitting = ref(false)

const rejectReasonValid = computed(() => {
  const len = rejectReason.value.trim().length
  return len >= 5 && len <= 200
})

function onOpenReject(record: ShopDto) {
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
      await shopApi.reject(rejectTarget.value.id, { reason })
      message.success('已驳回申请，已通知申请人')
    } else {
      const result = await runBatch(selectedRowKeys.value, (id) => shopApi.reject(id, { reason }))
      showBatchResult(result, '批量驳回')
    }
    rejectModalOpen.value = false
    selectedRowKeys.value = []
    drawerOpen.value = false
    await fetchShops()
  } catch (e) {
    showAuditError(e, '驳回操作失败，请重试')
  } finally {
    rejectSubmitting.value = false
    rejectTarget.value = null
  }
}

// ---------- 批量通过（串行执行并汇总） ----------
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
    const result = await runBatch(selectedRowKeys.value, (id) => shopApi.approve(id))
    showBatchResult(result, '批量通过')
    selectedRowKeys.value = []
    await fetchShops()
  } finally {
    batchRunning.value = false
  }
}

/** 串行执行批量动作并汇总成功 / 失败明细（单条失败不中断整体） */
async function runBatch(
  ids: string[],
  action: (id: string) => Promise<unknown>,
): Promise<BatchOperationResultDto> {
  const failures: BatchOperationFailureDto[] = []
  let succeeded = 0

  for (const id of ids) {
    try {
      await action(id)
      succeeded += 1
    } catch (e) {
      failures.push({ id, reason: e instanceof Error ? e.message : '操作失败' })
    }
  }

  return { total: ids.length, succeeded, failed: failures.length, failures }
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

// ---------- 审核抽屉（详情 + 资质并行加载） ----------
const drawerOpen = ref(false)
const detail = ref<ShopDto | null>(null)
const qualifications = ref<QualificationDto[]>([])
const drawerLoading = ref(false)
const approvingQualId = ref('')
const rejectingQualId = ref('')

const approvedQualCount = computed(() => approvedCount(qualifications.value))

/** 抽屉内强校验：资质非空且全部 Approved 才允许店铺级通过 */
const allQualificationsApproved = computed(
  () => qualifications.value.length > 0 && approvedQualCount.value === qualifications.value.length,
)

async function onOpenDrawer(record: ShopDto) {
  drawerOpen.value = true
  drawerLoading.value = true
  detail.value = null
  qualifications.value = []
  try {
    const [shopRes, qualRes] = await Promise.all([
      shopApi.get(record.id),
      shopApi.getQualifications(record.id),
    ])
    detail.value = shopRes.data
    qualifications.value = qualRes.data ?? []
  } catch (e) {
    drawerOpen.value = false
    showAuditError(e, '加载审核详情失败，请重试')
  } finally {
    drawerLoading.value = false
  }
}

/** 资质操作后局部刷新：抽屉列表与列表行资质摘要同步 */
async function reloadQualifications(shopId: string) {
  const { data } = await shopApi.getQualifications(shopId)
  qualifications.value = data ?? []
  const row = tableData.value.find((item) => item.id === shopId)
  if (row) row.qualifications = qualifications.value
  const current = detail.value
  if (current?.id === shopId) current.qualifications = qualifications.value
}

async function onApproveQualification(qual: QualificationDto) {
  const shop = detail.value
  if (!shop) return
  approvingQualId.value = qual.id
  try {
    await shopApi.approveQualification(shop.id, qual.id)
    message.success(`资质「${qual.type}」已审核通过`)
    await reloadQualifications(shop.id)
  } catch (e) {
    showAuditError(e, '资质审核失败，请重试')
  } finally {
    approvingQualId.value = ''
  }
}

// ---------- 资质驳回（ConfirmDialog 填写原因） ----------
const qualRejectOpen = ref(false)
const qualRejectTarget = ref<QualificationDto | null>(null)

function onOpenQualReject(qual: QualificationDto) {
  qualRejectTarget.value = qual
  qualRejectOpen.value = true
}

async function onConfirmQualReject(reason?: string) {
  const shop = detail.value
  const qual = qualRejectTarget.value
  if (!shop || !qual || !reason) return
  rejectingQualId.value = qual.id
  try {
    await shopApi.rejectQualification(shop.id, qual.id, { reason })
    message.success(`资质「${qual.type}」已驳回`)
    qualRejectOpen.value = false
    await reloadQualifications(shop.id)
  } catch (e) {
    showAuditError(e, '资质驳回失败，请重试')
  } finally {
    rejectingQualId.value = ''
    qualRejectTarget.value = null
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

  const header = ['店铺ID', '店铺名称', '申请人', '卖家账号', '主营类目', '资质数(已通过/总数)', '状态', '申请时间']
  const rows = tableData.value.map((shop) => [
    shop.id,
    shop.name,
    shop.ownerName,
    shop.sellerAccount,
    shop.mainCategory,
    shop.qualifications?.length ? qualSummary(shop) : '—',
    SHOP_STATUS_META[shop.status].label,
    formatDateTime(shop.submittedAt),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `入驻审核导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchShops()
})
</script>

<style scoped>
.application-audit {
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

.shop-cell {
  display: flex;
  flex-direction: column;
}

.cell-main {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.drawer-section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.drawer-section-title:first-child {
  margin-top: 0;
}

.qual-empty {
  padding: 8px 0 16px;
}

.qual-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  margin-bottom: 8px;
}

.qual-info {
  flex: 1;
  min-width: 0;
}

.qual-type {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  margin-bottom: 2px;
}

.qual-meta {
  font-size: 12px;
  color: #8c8c8c;
}

.qual-reject-reason {
  margin-top: 4px;
  font-size: 12px;
  color: #cf1322;
}

.qual-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.qual-preview-link {
  font-size: 12px;
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
