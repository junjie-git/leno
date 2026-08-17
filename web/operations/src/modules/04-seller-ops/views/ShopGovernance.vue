<!-- web/operations/src/modules/04-seller-ops/views/ShopGovernance.vue -->
<template>
  <div class="shop-governance">
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
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="主营类目">
          <a-select
            v-model:value="filters.category"
            placeholder="全部类目"
            allow-clear
            style="width: 150px"
            :options="categoryOptions"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + C：统计概览工具栏与店铺表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <a-space size="large" class="status-overview">
          <span class="status-stat" aria-label="已通过店铺数">
            <span class="status-dot status-dot--active" />
            已通过
            <span class="status-stat-value">{{ statusCounts ? statusCounts.Active : '—' }}</span>
          </span>
          <span class="status-stat" aria-label="已暂停店铺数">
            <span class="status-dot status-dot--suspended" />
            已暂停
            <span class="status-stat-value">{{ statusCounts ? statusCounts.Suspended : '—' }}</span>
          </span>
          <span class="status-stat" aria-label="已关闭店铺数">
            <span class="status-dot status-dot--closed" />
            已关闭
            <span class="status-stat-value">{{ statusCounts ? statusCounts.Closed : '—' }}</span>
          </span>
        </a-space>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="refreshAll">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="refreshAll" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: ShopDto) => record.id"
        :scroll="{ x: 1240 }"
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
          <template v-else-if="column.key === 'seller'">
            <div class="shop-cell">
              <span>{{ record.ownerName }}</span>
              <div class="cell-sub">{{ record.contactPhone || '—' }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'productCount'">
            <a-button
              type="link"
              size="small"
              class="link-number"
              :aria-label="`查看 ${record.name} 的商品`"
              @click="goProductAudit(record)"
            >
              {{ record.productCount }}
            </a-button>
          </template>
          <template v-else-if="column.key === 'orderCount'">
            <a-button
              type="link"
              size="small"
              class="link-number"
              :aria-label="`查看 ${record.name} 的订单`"
              @click="goOrderManagement(record)"
            >
              {{ record.orderCount }}
            </a-button>
          </template>
          <template v-else-if="column.key === 'rating'">
            <span
              class="rating-value"
              :style="{ color: ratingColor(record.rating as number) }"
              :aria-label="`店铺评分 ${record.rating}`"
            >
              {{ Number(record.rating).toFixed(1) }}
            </span>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="SHOP_STATUS_META[record.status as ShopStatus].color">
              {{ SHOP_STATUS_META[record.status as ShopStatus].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'lastGovernedAt'">
            {{ formatDateTime(record.lastGovernedAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button
                type="link"
                size="small"
                aria-label="打开治理抽屉"
                @click="onOpenDrawer(record)"
              >
                {{ record.status === 'Active' || record.status === 'Suspended' ? '治理' : '查看' }}
              </a-button>
              <a-button
                v-if="record.status === 'Active'"
                type="link"
                size="small"
                aria-label="暂停店铺营业"
                @click="onOpenSuspend(record)"
              >
                暂停
              </a-button>
              <template v-else-if="record.status === 'Suspended'">
                <a-button
                  type="link"
                  size="small"
                  aria-label="恢复店铺营业"
                  @click="onOpenResume(record)"
                >
                  恢复
                </a-button>
                <a-button
                  type="link"
                  size="small"
                  danger
                  aria-label="关闭店铺（终态）"
                  @click="onOpenClose(record)"
                >
                  关闭
                </a-button>
              </template>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：治理抽屉（店铺概览 / 经营指标 / 资质复审 / 状态变更） -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="detail ? `店铺治理 · ${detail.name}` : '店铺治理'"
      placement="right"
      :width="drawerWidth"
      :destroy-on-close="true"
    >
      <a-spin :spinning="drawerLoading">
        <template v-if="detail">
          <!-- 店铺概览 -->
          <h3 class="drawer-section-title">店铺概览</h3>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="店铺 ID">{{ detail.id }}</a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="SHOP_STATUS_META[detail.status].color">
                {{ SHOP_STATUS_META[detail.status].label }}
              </a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="店铺名称" :span="2">{{ detail.name }}</a-descriptions-item>
            <a-descriptions-item label="卖家账号">{{ detail.sellerAccount }}</a-descriptions-item>
            <a-descriptions-item label="主营类目">{{ detail.mainCategory }}</a-descriptions-item>
            <a-descriptions-item label="在售商品">{{ detail.productCount }}</a-descriptions-item>
            <a-descriptions-item label="累计订单">{{ detail.orderCount }}</a-descriptions-item>
            <a-descriptions-item label="店铺创建时间">
              {{ formatDateTime(detail.createdAt) }}
            </a-descriptions-item>
            <a-descriptions-item label="最后治理时间">
              {{ formatDateTime(detail.lastGovernedAt) }}
            </a-descriptions-item>
          </a-descriptions>

          <!-- 经营指标 -->
          <h3 class="drawer-section-title">经营指标</h3>
          <div class="metric-grid">
            <div class="metric-item">
              <div class="metric-value">{{ formatMoney(detail.gmv ?? 0) }}</div>
              <div class="metric-label">累计 GMV</div>
            </div>
            <div class="metric-item">
              <div class="metric-value">{{ formatNumber(detail.orderCount) }}</div>
              <div class="metric-label">累计订单</div>
            </div>
            <div class="metric-item">
              <div
                class="metric-value"
                :style="{ color: ratingColor(detail.rating) }"
                :aria-label="`店铺评分 ${detail.rating}`"
              >
                {{ detail.rating.toFixed(1) }}
              </div>
              <div class="metric-label">店铺评分</div>
            </div>
          </div>

          <!-- 资质列表（复审入口） -->
          <h3 class="drawer-section-title">
            资质复审（{{ approvedQualCount }} / {{ qualifications.length }} 已通过）
          </h3>
          <div v-if="qualifications.length === 0" class="qual-empty">
            <EmptyState description="该店铺未上传资质" />
          </div>
          <div v-for="qual in qualifications" :key="qual.id" class="qual-item">
            <div class="qual-info">
              <div class="qual-type" :aria-label="`资质类型 ${qual.type}`">{{ qual.type }}</div>
              <div class="qual-meta">
                {{ qual.fileName }} ｜ {{ formatDateTime(qual.submittedAt) }}
              </div>
              <div v-if="qual.rejectReason" class="qual-reject-reason">
                驳回原因：{{ qual.rejectReason }}
              </div>
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
              <IdempotencyButton
                size="small"
                :loading="approvingQualId === qual.id"
                aria-label="资质复审通过"
                @click="onApproveQualification(qual)"
              >
                复审通过
              </IdempotencyButton>
              <IdempotencyButton
                size="small"
                danger
                :loading="rejectingQualId === qual.id"
                aria-label="资质复审驳回"
                @click="onOpenQualReject(qual)"
              >
                复审驳回
              </IdempotencyButton>
            </div>
          </div>
        </template>
      </a-spin>
      <template #footer>
        <a-space v-if="detail">
          <template v-if="detail.status === 'Active'">
            <IdempotencyButton danger aria-label="暂停店铺营业" @click="onOpenSuspend(detail)">
              暂停营业
            </IdempotencyButton>
            <span class="govern-hint">暂停后店铺商品下架、暂停接单</span>
          </template>
          <template v-else-if="detail.status === 'Suspended'">
            <IdempotencyButton aria-label="恢复店铺营业" @click="onOpenResume(detail)">
              恢复营业
            </IdempotencyButton>
            <IdempotencyButton danger aria-label="关闭店铺（终态）" @click="onOpenClose(detail)">
              关闭店铺
            </IdempotencyButton>
            <span class="govern-hint">关闭为终态操作，不可恢复</span>
          </template>
          <span v-else-if="detail.status === 'Closed'" class="govern-hint">
            店铺已关闭（终态），不支持恢复
          </span>
          <span v-else class="govern-hint">当前状态无治理操作</span>
        </a-space>
      </template>
    </a-drawer>

    <!-- 区域 E-1：暂停对话框（分类 + 原因必填） -->
    <a-modal
      v-model:open="suspendModalOpen"
      title="暂停店铺营业"
      :confirm-loading="suspendSubmitting"
      :ok-button-props="{ disabled: !suspendReasonValid }"
      ok-text="确认暂停"
      cancel-text="取消"
      @ok="onSubmitSuspend"
    >
      <p v-if="suspendTarget" class="modal-target">
        店铺：{{ suspendTarget.name }}（{{ suspendTarget.ownerName }}）
      </p>
      <a-form layout="vertical">
        <a-form-item label="暂停分类" required>
          <a-select v-model:value="suspendCategory" :options="suspendCategoryOptions" />
        </a-form-item>
        <a-form-item
          label="暂停原因"
          required
          :validate-status="suspendTouched && !suspendReasonValid ? 'error' : ''"
          :help="suspendTouched && !suspendReasonValid ? '暂停原因必填，且长度为 5-200 字' : ''"
        >
          <a-textarea
            v-model:value="suspendReason"
            :rows="4"
            :maxlength="200"
            show-count
            placeholder="请输入暂停原因（至少 5 个字，将记入审计记录）"
            @blur="suspendTouched = true"
          />
        </a-form-item>
      </a-form>
      <a-alert
        type="warning"
        show-icon
        message="暂停影响：店铺全部商品下架、买家无法下单，已支付订单继续正常履约。"
      />
    </a-modal>

    <!-- 区域 E-2：关闭对话框（终态危险确认 + 原因必填） -->
    <a-modal
      v-model:open="closeModalOpen"
      title="关闭店铺（终态操作）"
      :confirm-loading="closeSubmitting"
      :ok-button-props="{ disabled: !closeReasonValid, danger: true }"
      ok-text="确认关闭"
      cancel-text="取消"
      @ok="onSubmitClose"
    >
      <p v-if="closeTarget" class="modal-target">
        店铺：{{ closeTarget.name }}（{{ closeTarget.ownerName }}）
      </p>
      <a-form layout="vertical">
        <a-form-item
          label="关闭原因"
          required
          :validate-status="closeTouched && !closeReasonValid ? 'error' : ''"
          :help="closeTouched && !closeReasonValid ? '关闭原因必填，且长度为 5-200 字' : ''"
        >
          <a-textarea
            v-model:value="closeReason"
            :rows="4"
            :maxlength="200"
            show-count
            placeholder="请输入关闭原因（至少 5 个字，将记入审计记录）"
            @blur="closeTouched = true"
          />
        </a-form-item>
      </a-form>
      <a-alert
        type="error"
        show-icon
        message="终态警示：关闭后店铺不可恢复，在售商品全部下架，卖家将无法再次启用该店铺。"
      />
    </a-modal>

    <!-- 恢复营业确认 -->
    <ConfirmDialog
      :open="resumeConfirmOpen"
      title="恢复店铺营业"
      :content="`确认恢复「${resumeTarget?.name ?? ''}」营业？店铺商品将重新上架。`"
      @confirm="onConfirmResume"
      @cancel="resumeConfirmOpen = false"
    />

    <!-- 资质复审驳回（填写原因） -->
    <ConfirmDialog
      :open="qualRejectOpen"
      danger
      title="资质复审驳回"
      :content="`确认驳回资质「${qualRejectTarget?.type ?? ''}」？驳回原因将通知卖家。`"
      :require-input="{ label: '驳回原因', min: 5, max: 200 }"
      @confirm="onConfirmQualReject"
      @cancel="qualRejectOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime, formatMoney, formatNumber } from '@/shared/utils/format'
import { shopApi } from '../api/shop.api'
import type { QualificationDto, QualificationStatus, ShopDto, ShopStatus } from '../types/shop.dto'

/**
 * 店铺治理页（04-seller-ops）
 *
 * 状态机：Active ↔ Suspended → Closed（终态不可逆；关闭前置须先暂停）。
 * - 首屏默认查询 Status=Active 前 20 条，统计概览并行拉取三状态计数
 * - 治理抽屉：店铺概览 / 经营指标 / 资质复审 / 状态变更操作区
 * - 支持 query.shopId 直达（入驻审核页「查看治理」跳转）
 * - 暂停需分类（违规/资质过期/主动申请）+ 原因；关闭为终态危险确认
 */

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

/** 治理阶段状态筛选（md：已通过/已暂停/已关闭） */
const statusOptions: { label: string; value: ShopStatus }[] = [
  { label: '已通过', value: 'Active' },
  { label: '已暂停', value: 'Suspended' },
  { label: '已关闭', value: 'Closed' },
]

/** 主营类目筛选（与种子数据类目口径对齐） */
const categoryOptions: { label: string; value: string }[] = [
  { label: '数码电器', value: '数码电器' },
  { label: '服饰鞋包', value: '服饰鞋包' },
  { label: '美妆个护', value: '美妆个护' },
  { label: '食品生鲜', value: '食品生鲜' },
  { label: '家居日用', value: '家居日用' },
]

/** 暂停原因分类（md §4：分类影响审计记录） */
const SUSPEND_CATEGORIES = ['违规经营', '资质过期', '主动申请'] as const

const suspendCategoryOptions = SUSPEND_CATEGORIES.map((c) => ({ label: c, value: c }))

const router = useRouter()
const route = useRoute()

interface FilterState {
  keyword: string
  status?: ShopStatus
  category?: string
}

const filters = reactive<FilterState>({
  keyword: '',
  status: 'Active',
  category: undefined,
})

const hasActiveFilters = computed(() => Boolean(filters.keyword || filters.status || filters.category))

const emptyDescription = computed(() =>
  filters.status ? `该状态下暂无店铺（${SHOP_STATUS_META[filters.status].label}）` : '暂无店铺',
)

/** 评分色阶（md §6：≥4.5 绿 / 4.0-4.5 橙 / <4.0 红） */
function ratingColor(rating: number): string {
  if (rating >= 4.5) return '#52C41A'
  if (rating >= 4.0) return '#FAAD14'
  return '#FF4D4F'
}

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
  { title: '店铺名称', key: 'shop', width: 200, ellipsis: true },
  { title: '卖家', key: 'seller', width: 140, ellipsis: true },
  { title: '主营类目', dataIndex: 'mainCategory', key: 'mainCategory', width: 110 },
  { title: '商品数', key: 'productCount', width: 90, align: 'center' },
  { title: '订单数', key: 'orderCount', width: 90, align: 'center' },
  { title: '评分', key: 'rating', width: 80, align: 'center' },
  { title: '状态', key: 'status', width: 100 },
  { title: '最后治理时间', key: 'lastGovernedAt', width: 170 },
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
    if (keyword) params.keyword = keyword
    if (filters.status) params.status = filters.status
    if (filters.category) params.category = filters.category

    const { data } = await shopApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载店铺列表失败'
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
  filters.status = 'Active'
  filters.category = undefined
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchShops()
}

// ---------- 统计概览（三状态计数并行拉取） ----------
interface StatusCounts {
  Active: number
  Suspended: number
  Closed: number
}

const statusCounts = ref<StatusCounts | null>(null)

async function fetchStatusCounts() {
  try {
    const [activeRes, suspendedRes, closedRes] = await Promise.all([
      shopApi.list({ page: 1, pageSize: 1, status: 'Active' }),
      shopApi.list({ page: 1, pageSize: 1, status: 'Suspended' }),
      shopApi.list({ page: 1, pageSize: 1, status: 'Closed' }),
    ])
    statusCounts.value = {
      Active: activeRes.data.total,
      Suspended: suspendedRes.data.total,
      Closed: closedRes.data.total,
    }
  } catch {
    statusCounts.value = null
  }
}

function refreshAll() {
  void fetchShops()
  void fetchStatusCounts()
}

// ---------- 跨页面流转（携带卖家筛选） ----------
function goProductAudit(shop: ShopDto) {
  void router.push({ path: '/product-ops/product-audit', query: { sellerId: shop.id } })
}

function goOrderManagement(shop: ShopDto) {
  void router.push({ path: '/order-ops/order-management', query: { sellerId: shop.id } })
}

// ---------- 治理错误统一分流 ----------
function showGovernError(e: unknown, fallback: string) {
  if (e instanceof ConcurrencyError) {
    message.warning('店铺状态已变更，请刷新列表')
    return
  }
  message.error(e instanceof Error && e.message ? e.message : fallback)
}

/** 状态变更成功后联动刷新：列表 + 统计 + 抽屉详情 */
async function afterGovernanceChanged(shopId: string, keepDrawer: boolean) {
  if (!keepDrawer) drawerOpen.value = false
  refreshAll()
  if (keepDrawer && detail.value?.id === shopId) {
    try {
      const { data } = await shopApi.get(shopId)
      detail.value = data
    } catch {
      // 详情刷新失败不阻断列表刷新结果
    }
  }
}

// ---------- 治理抽屉（详情 + 资质并行加载） ----------
const drawerOpen = ref(false)
const detail = ref<ShopDto | null>(null)
const qualifications = ref<QualificationDto[]>([])
const drawerLoading = ref(false)
const approvingQualId = ref('')
const rejectingQualId = ref('')

/** 抽屉响应式宽度（md：≥1200px 720px；<1200px 520px） */
const drawerWidth = ref(720)

function updateDrawerWidth() {
  drawerWidth.value = window.innerWidth < 1200 ? 520 : 720
}

const approvedQualCount = computed(
  () => qualifications.value.filter((q) => q.status === 'Approved').length,
)

async function loadGovernanceDetail(shopId: string) {
  drawerOpen.value = true
  drawerLoading.value = true
  detail.value = null
  qualifications.value = []
  try {
    const [shopRes, qualRes] = await Promise.all([
      shopApi.get(shopId),
      shopApi.getQualifications(shopId),
    ])
    detail.value = shopRes.data
    qualifications.value = qualRes.data ?? []
  } catch (e) {
    drawerOpen.value = false
    showGovernError(e, '加载治理详情失败，请重试')
  } finally {
    drawerLoading.value = false
  }
}

function onOpenDrawer(record: ShopDto) {
  void loadGovernanceDetail(record.id)
}

/** 资质复审后局部刷新抽屉列表 */
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
    message.success(`资质「${qual.type}」复审通过`)
    await reloadQualifications(shop.id)
  } catch (e) {
    showGovernError(e, '资质复审失败，请重试')
  } finally {
    approvingQualId.value = ''
  }
}

// ---------- 资质复审驳回（ConfirmDialog 填写原因） ----------
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
    showGovernError(e, '资质复审驳回失败，请重试')
  } finally {
    rejectingQualId.value = ''
    qualRejectTarget.value = null
  }
}

// ---------- 暂停营业（分类 + 原因必填） ----------
const suspendModalOpen = ref(false)
const suspendTarget = ref<ShopDto | null>(null)
const suspendCategory = ref<(typeof SUSPEND_CATEGORIES)[number]>('违规经营')
const suspendReason = ref('')
const suspendTouched = ref(false)
const suspendSubmitting = ref(false)

const suspendReasonValid = computed(() => {
  const len = suspendReason.value.trim().length
  return len >= 5 && len <= 200
})

function onOpenSuspend(record: ShopDto) {
  suspendTarget.value = record
  suspendCategory.value = '违规经营'
  suspendReason.value = ''
  suspendTouched.value = false
  suspendModalOpen.value = true
}

async function onSubmitSuspend() {
  suspendTouched.value = true
  const target = suspendTarget.value
  if (!target || !suspendReasonValid.value) return

  suspendSubmitting.value = true
  try {
    const reason = `[${suspendCategory.value}] ${suspendReason.value.trim()}`
    await shopApi.suspend(target.id, { reason })
    message.success(`店铺「${target.name}」已暂停营业`)
    suspendModalOpen.value = false
    await afterGovernanceChanged(target.id, true)
  } catch (e) {
    showGovernError(e, '暂停操作失败，请重试')
  } finally {
    suspendSubmitting.value = false
    suspendTarget.value = null
  }
}

// ---------- 恢复营业（普通确认，无需原因） ----------
const resumeConfirmOpen = ref(false)
const resumeTarget = ref<ShopDto | null>(null)

function onOpenResume(record: ShopDto) {
  resumeTarget.value = record
  resumeConfirmOpen.value = true
}

async function onConfirmResume() {
  const target = resumeTarget.value
  if (!target) return
  resumeConfirmOpen.value = false
  try {
    await shopApi.resume(target.id)
    message.success(`店铺「${target.name}」已恢复营业`)
    await afterGovernanceChanged(target.id, true)
  } catch (e) {
    showGovernError(e, '恢复操作失败，请重试')
  } finally {
    resumeTarget.value = null
  }
}

// ---------- 关闭店铺（终态危险确认 + 原因必填） ----------
const closeModalOpen = ref(false)
const closeTarget = ref<ShopDto | null>(null)
const closeReason = ref('')
const closeTouched = ref(false)
const closeSubmitting = ref(false)

const closeReasonValid = computed(() => {
  const len = closeReason.value.trim().length
  return len >= 5 && len <= 200
})

function onOpenClose(record: ShopDto) {
  closeTarget.value = record
  closeReason.value = ''
  closeTouched.value = false
  closeModalOpen.value = true
}

async function onSubmitClose() {
  closeTouched.value = true
  const target = closeTarget.value
  if (!target || !closeReasonValid.value) return

  closeSubmitting.value = true
  try {
    await shopApi.close(target.id, { reason: closeReason.value.trim() })
    message.success(`店铺「${target.name}」已关闭（终态，不可恢复）`)
    closeModalOpen.value = false
    await afterGovernanceChanged(target.id, true)
  } catch (e) {
    showGovernError(e, '关闭操作失败，请重试')
  } finally {
    closeSubmitting.value = false
    closeTarget.value = null
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

  const header = [
    '店铺ID',
    '店铺名称',
    '卖家账号',
    '主营类目',
    '商品数',
    '订单数',
    '评分',
    '状态',
    '最后治理时间',
  ]
  const rows = tableData.value.map((shop) => [
    shop.id,
    shop.name,
    shop.sellerAccount,
    shop.mainCategory,
    String(shop.productCount),
    String(shop.orderCount),
    shop.rating.toFixed(1),
    SHOP_STATUS_META[shop.status].label,
    formatDateTime(shop.lastGovernedAt),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `店铺治理导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化（query.shopId 直达治理抽屉） ----------
onMounted(() => {
  updateDrawerWidth()
  window.addEventListener('resize', updateDrawerWidth)

  const shopId = typeof route.query.shopId === 'string' ? route.query.shopId : ''
  if (shopId) {
    void loadGovernanceDetail(shopId)
  }
  refreshAll()
})

onUnmounted(() => {
  window.removeEventListener('resize', updateDrawerWidth)
})
</script>

<style scoped>
.shop-governance {
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

.status-overview {
  flex-wrap: wrap;
}

.status-stat {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #595959;
}

.status-stat-value {
  font-size: 18px;
  font-weight: 600;
  color: #000000d9;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-dot--active {
  background: #52c41a;
}

.status-dot--suspended {
  background: #faad14;
}

.status-dot--closed {
  background: #bfbfbf;
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

.link-number {
  padding: 0;
}

.rating-value {
  font-size: 14px;
  font-weight: 600;
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

.metric-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
}

.metric-item {
  padding: 12px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  text-align: center;
}

.metric-value {
  font-size: 18px;
  font-weight: 600;
  color: #000000d9;
  line-height: 1.4;
}

.metric-label {
  margin-top: 4px;
  font-size: 12px;
  color: #8c8c8c;
}

.govern-hint {
  font-size: 12px;
  color: #8c8c8c;
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

.modal-target {
  margin-bottom: 12px;
  font-weight: 500;
}
</style>
