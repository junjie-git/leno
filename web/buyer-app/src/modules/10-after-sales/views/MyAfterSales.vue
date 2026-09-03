<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { afterSalesApi } from '@/modules/10-after-sales/api/afterSales.api'
import type { AfterSalesDto, AfterSalesStatus, AfterSalesType } from '../types/after-sales.dto'
import EmptyState from '@/shared/components/EmptyState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import PriceText from '@/shared/components/PriceText.vue'
import { formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 我的售后页（/after-sales/mine）
 *
 * 结构（对齐设计稿 my-after-sales）：
 * NavBar（返回 / 我的售后）→ 状态 Tab（全部 + dto 七种状态，切换前端过滤）
 * → van-pull-refresh 售后卡片列表
 * → 卡片（售后单号 + 状态标签 / 商品图 + 标题 + 规格 / 类型标签 + 原因 + 退款金额 / 申请时间 + 快捷操作）
 *
 * 操作流：
 * - 待审核 → 「撤销」（showConfirmDialog 二次确认 → POST /after-sales/{id}/cancel）
 * - 待退货 → 「填写物流」（底部弹层填写快递公司与单号 → POST /after-sales/{id}/return-goods）
 * - 点卡片 / 「查看详情」→ /after-sales/:id 售后详情
 */

const router = useRouter()

/** 状态 Tab：全部 + AfterSalesStatus 枚举逐项 */
const TABS: Array<{ key: AfterSalesStatus | ''; label: string }> = [
  { key: '', label: '全部' },
  { key: 'PendingReview', label: '待审核' },
  { key: 'Approved', label: '待退货' },
  { key: 'Returning', label: '退货中' },
  { key: 'Refunding', label: '退款中' },
  { key: 'Completed', label: '已完成' },
  { key: 'Cancelled', label: '已撤销' },
  { key: 'Rejected', label: '已驳回' },
]

/** 售后状态 → 标签文案与配色（对齐设计稿状态色） */
const STATUS_META: Record<AfterSalesStatus, { label: string; cls: string; dot: boolean }> = {
  PendingReview: { label: '待卖家审核', cls: 'st-pending', dot: true },
  Approved: { label: '待退货', cls: 'st-process', dot: true },
  Returning: { label: '退货中', cls: 'st-process', dot: true },
  Refunding: { label: '退款中', cls: 'st-process', dot: true },
  Completed: { label: '已完成', cls: 'st-done', dot: false },
  Cancelled: { label: '已撤销', cls: 'st-cancel', dot: false },
  Rejected: { label: '已驳回', cls: 'st-reject', dot: false },
}

/** 售后类型 → 标签文案与配色 */
const TYPE_META: Record<AfterSalesType, { label: string; cls: string }> = {
  RefundOnly: { label: '仅退款', cls: 'tag-refund' },
  ReturnRefund: { label: '退货退款', cls: 'tag-return' },
  Exchange: { label: '换货', cls: 'tag-return' },
}

// ---- 列表状态 ----
const activeTab = ref<AfterSalesStatus | ''>('')
const firstLoading = ref(true)
const records = ref<AfterSalesDto[]>([])
const listError = ref(false)
const refreshing = ref(false)

// ---- 退货物流弹层 ----
const returnVisible = ref(false)
const returnTarget = ref<AfterSalesDto | null>(null)
const returnCompany = ref('')
const returnLogisticsNo = ref('')
const returnSubmitting = ref(false)

/** 撤销中的售后单 id（按钮防重复提交） */
const cancellingId = ref('')

onMounted(() => {
  void reload()
})

/** 当前 Tab 过滤后的列表 */
const filteredRecords = computed(() => {
  if (activeTab.value === '') {
    return records.value
  }
  return records.value.filter((item) => item.status === activeTab.value)
})

function statusMeta(item: AfterSalesDto): { label: string; cls: string; dot: boolean } {
  return STATUS_META[item.status]
}

function typeMeta(item: AfterSalesDto): { label: string; cls: string } {
  return TYPE_META[item.type]
}

// ---- 数据加载 ----
async function reload(): Promise<void> {
  firstLoading.value = true
  listError.value = false
  try {
    records.value = await afterSalesApi.listMine()
  } catch (e) {
    logger.error('我的售后列表加载失败', e)
    listError.value = true
  } finally {
    firstLoading.value = false
    refreshing.value = false
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await reload()
}

/** 切换状态 Tab（前端过滤，无需重拉） */
function setTab(key: AfterSalesStatus | ''): void {
  if (activeTab.value === key) {
    return
  }
  activeTab.value = key
}

// ---- 卡片操作 ----
/** 撤销售后（待审核，二次确认） */
async function cancelAfterSales(item: AfterSalesDto): Promise<void> {
  if (cancellingId.value) {
    return
  }
  try {
    await showConfirmDialog({
      title: '确认撤销',
      message: '撤销后无法恢复，确认撤销该售后申请吗？',
      confirmButtonText: '确认撤销',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  cancellingId.value = item.id
  try {
    await afterSalesApi.cancel(item.id)
    showToast('撤销成功')
    await reload()
  } catch (e) {
    logger.warn('撤销售后失败', e)
    showFailToast(e instanceof Error ? e.message : '撤销失败，请稍后重试')
  } finally {
    cancellingId.value = ''
  }
}

/** 打开退货物流弹层（待退货） */
function openReturnPopup(item: AfterSalesDto): void {
  returnTarget.value = item
  returnCompany.value = ''
  returnLogisticsNo.value = ''
  returnVisible.value = true
}

/** 提交退货物流 */
async function submitReturnLogistics(): Promise<void> {
  const target = returnTarget.value
  if (!target || returnSubmitting.value) {
    return
  }
  const company = returnCompany.value.trim()
  const logisticsNo = returnLogisticsNo.value.trim()
  if (!company) {
    showToast('请填写快递公司')
    return
  }
  if (!logisticsNo) {
    showToast('请填写物流单号')
    return
  }
  returnSubmitting.value = true
  try {
    await afterSalesApi.submitReturnLogistics(target.id, { company, logisticsNo })
    returnVisible.value = false
    showToast('提交成功，等待卖家确认收货')
    await reload()
  } catch (e) {
    logger.warn('提交退货物流失败', e)
    showFailToast(e instanceof Error ? e.message : '提交失败，请稍后重试')
  } finally {
    returnSubmitting.value = false
  }
}

// ---- 跳转 ----
function goDetail(item: AfterSalesDto): void {
  router.push(`/after-sales/${item.id}`)
}

function goHome(): void {
  router.replace('/')
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/profile')
  }
}
</script>

<template>
  <div class="mine-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">我的售后</div>
    </header>

    <!-- 状态 Tab -->
    <nav class="tabs" role="tablist" aria-label="售后状态筛选">
      <div
        v-for="tab in TABS"
        :key="tab.key || 'all'"
        class="tab"
        :class="{ active: activeTab === tab.key }"
        role="tab"
        :aria-selected="activeTab === tab.key"
        @click="setTab(tab.key)"
      >
        {{ tab.label }}
      </div>
    </nav>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="skeleton-list">
        <div v-for="i in 3" :key="i" class="sk-card">
          <div class="skeleton-block sk-head" />
          <div class="sk-row">
            <div class="skeleton-block sk-img" />
            <div class="sk-lines">
              <div class="skeleton-block sk-l1" />
              <div class="skeleton-block sk-l2" />
            </div>
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="listError && records.length === 0"
        title="售后记录加载失败"
        description="网络异常，请稍后重试"
        @retry="reload"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="filteredRecords.length === 0"
        :title="activeTab === '' ? '暂无售后记录' : '暂无相关售后记录'"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 售后列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <article
          v-for="item in filteredRecords"
          :key="item.id"
          class="as-card"
          role="article"
          :aria-label="`售后单 ${item.name} ${statusMeta(item).label}`"
          @click="goDetail(item)"
        >
          <!-- 卡片头：单号 + 状态 -->
          <div class="card-head">
            <div class="card-no">售后单号 <span class="no-value">{{ item.id }}</span></div>
            <span class="status-tag" :class="statusMeta(item).cls">
              <span v-if="statusMeta(item).dot" class="dot" />
              {{ statusMeta(item).label }}
            </span>
          </div>

          <!-- 商品信息 -->
          <div class="card-body">
            <img class="goods-img" :src="item.image" :alt="item.name" loading="lazy">
            <div class="goods-info">
              <div class="goods-name">{{ item.name }}</div>
              <div class="goods-sku">规格：{{ item.specs }}</div>
              <div class="goods-detail">
                <span class="goods-type">
                  <span class="type-tag" :class="typeMeta(item).cls">{{ typeMeta(item).label }}</span>
                  {{ item.reason }}
                </span>
                <PriceText
                  :amount="item.refundAmount"
                  :size="16"
                  :color="item.status === 'Cancelled' ? 'var(--n7)' : undefined"
                />
              </div>
            </div>
          </div>

          <!-- 卡片脚：时间 + 操作 -->
          <div class="card-foot">
            <span class="foot-time">{{ formatDateTime(item.applyAt) }}</span>
            <div class="foot-actions">
              <template v-if="item.status === 'PendingReview'">
                <button
                  class="btn btn-danger"
                  type="button"
                  :disabled="cancellingId === item.id"
                  @click.stop="cancelAfterSales(item)"
                >
                  {{ cancellingId === item.id ? '撤销中' : '撤销' }}
                </button>
                <button class="btn btn-outline" type="button" @click.stop="goDetail(item)">查看详情</button>
              </template>
              <template v-else-if="item.status === 'Approved'">
                <button class="btn btn-primary" type="button" @click.stop="openReturnPopup(item)">填写物流</button>
                <button class="btn btn-outline" type="button" @click.stop="goDetail(item)">查看详情</button>
              </template>
              <button v-else class="btn btn-default" type="button" @click.stop="goDetail(item)">查看详情</button>
            </div>
          </div>
        </article>
      </van-pull-refresh>
    </div>

    <!-- 退货物流弹层 -->
    <van-popup
      v-model:show="returnVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="填写退货物流"
    >
      <div class="return-panel">
        <div class="panel-head">
          <span class="panel-title">填写退货物流</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="returnVisible = false" />
        </div>
        <div class="panel-body">
          <div class="panel-field">
            <label class="field-label" for="return-company">快递公司</label>
            <input
              id="return-company"
              v-model="returnCompany"
              class="field-input"
              type="text"
              maxlength="20"
              placeholder="如：顺丰速运"
            >
          </div>
          <div class="panel-field">
            <label class="field-label" for="return-no">物流单号</label>
            <input
              id="return-no"
              v-model="returnLogisticsNo"
              class="field-input"
              type="text"
              maxlength="32"
              placeholder="请输入退货物流单号"
            >
          </div>
          <p class="panel-tip">请先与卖家确认退货地址后再寄回商品，并如实填写物流信息</p>
        </div>
        <div class="panel-foot">
          <button
            class="panel-submit"
            :class="{ loading: returnSubmitting }"
            type="button"
            :disabled="returnSubmitting"
            @click="submitReturnLogistics"
          >
            {{ returnSubmitting ? '提交中...' : '提交物流信息' }}
          </button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.mine-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n3);
}

/* NavBar */
.navbar {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  flex-shrink: 0;
  position: relative;
}

.nav-back {
  display: flex;
  align-items: center;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}

.nav-title {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

/* 状态 Tab */
.tabs {
  background: var(--n1);
  display: flex;
  height: 44px;
  border-bottom: 1px solid var(--n3);
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  flex-shrink: 0;
}

.tabs::-webkit-scrollbar {
  display: none;
}

.tab {
  flex: 1 0 auto;
  min-width: 62px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
  white-space: nowrap;
  padding: 0 var(--s2);
  transition: color var(--d-mid) var(--ease-std);
}

.tab.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.tab.active::after {
  content: "";
  position: absolute;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 20px;
  height: 2px;
  background: var(--c-primary);
  border-radius: 1px;
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
  background: var(--n3);
}

/* 骨架屏 */
.skeleton-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.sk-head {
  height: 16px;
  width: 55%;
  margin-bottom: var(--s3);
}

.sk-row {
  display: flex;
  gap: var(--s2);
}

.sk-img {
  width: 64px;
  height: 64px;
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
  justify-content: center;
}

.sk-l1 {
  width: 85%;
  height: 14px;
}

.sk-l2 {
  width: 50%;
  height: 12px;
}

/* 售后卡片 */
.as-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
  margin-bottom: var(--s3);
  cursor: pointer;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s2);
  padding: var(--s2) var(--s3);
  background: var(--n2);
  border-bottom: 1px solid var(--n3);
}

.card-no {
  font-size: var(--fs-sm);
  color: var(--n7);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-no .no-value {
  color: var(--n9);
  font-weight: var(--fw-medium);
}

.status-tag {
  display: inline-flex;
  align-items: center;
  flex-shrink: 0;
  padding: 2px var(--s2);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
}

.status-tag .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  margin-right: 4px;
  animation: pulse 1.5s ease infinite;
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.4;
  }
}

.st-pending {
  background: rgba(250, 173, 20, 0.1);
  color: var(--c-warning);
}

.st-pending .dot {
  background: var(--c-warning);
}

.st-process {
  background: rgba(22, 119, 255, 0.1);
  color: var(--c-primary);
}

.st-process .dot {
  background: var(--c-primary);
}

.st-done {
  background: rgba(82, 196, 26, 0.1);
  color: var(--c-success);
}

.st-cancel {
  background: rgba(140, 140, 140, 0.1);
  color: var(--n7);
}

.st-reject {
  background: rgba(255, 77, 79, 0.1);
  color: var(--c-error);
}

/* 商品信息 */
.card-body {
  padding: var(--s3);
  display: flex;
  gap: var(--s2);
}

.goods-img {
  width: 64px;
  height: 64px;
  border-radius: var(--r-card);
  object-fit: cover;
  background: var(--n3);
  flex-shrink: 0;
}

.goods-info {
  flex: 1;
  min-width: 0;
}

.goods-name {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.goods-sku {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.goods-detail {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s2);
  margin-top: var(--s2);
}

.goods-type {
  font-size: var(--fs-sm);
  color: var(--n9);
  display: flex;
  align-items: center;
  gap: var(--s1);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.type-tag {
  display: inline-block;
  padding: 1px 6px;
  border-radius: var(--r-base);
  font-size: 10px;
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

.tag-refund {
  background: rgba(22, 119, 255, 0.1);
  color: var(--c-primary);
}

.tag-return {
  background: rgba(250, 173, 20, 0.1);
  color: var(--c-warning);
}

/* 卡片脚 */
.card-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s2);
  padding: var(--s3);
  padding-top: 0;
}

.foot-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.foot-actions {
  display: flex;
  gap: var(--s2);
  flex-shrink: 0;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 5px 14px;
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  cursor: pointer;
  font-family: inherit;
  transition: opacity var(--d-fast) var(--ease-std);
}

.btn:active {
  opacity: 0.7;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: var(--c-primary);
  color: #fff;
  border: none;
}

.btn-outline {
  background: var(--n1);
  color: var(--c-primary);
  border: 1px solid var(--c-primary);
}

.btn-danger {
  background: var(--n1);
  color: var(--c-error);
  border: 1px solid var(--c-error);
}

.btn-default {
  background: var(--n2);
  color: var(--n9);
  border: none;
}

/* 退货物流弹层 */
.return-panel {
  padding: var(--s4) var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s3);
}

.panel-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.panel-body {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

.panel-field {
  display: flex;
  flex-direction: column;
  gap: var(--s1);
}

.field-label {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.field-input {
  height: 42px;
  border: 1px solid var(--n3);
  border-radius: var(--r-card);
  padding: 0 var(--s3);
  font-size: var(--fs-base);
  font-family: inherit;
  color: var(--n10);
  outline: none;
  transition: border-color var(--d-mid) var(--ease-std);
  background: var(--n1);
}

.field-input:focus {
  border-color: var(--c-primary);
}

.field-input::placeholder {
  color: var(--n7);
}

.panel-tip {
  font-size: var(--fs-sm);
  color: var(--n7);
  line-height: 1.6;
}

.panel-foot {
  margin-top: var(--s4);
}

.panel-submit {
  width: 100%;
  height: 44px;
  background: var(--c-primary);
  color: #fff;
  border: none;
  border-radius: var(--r-base);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  transition: opacity var(--d-fast) var(--ease-std);
}

.panel-submit:active {
  opacity: 0.85;
}

.panel-submit.loading {
  opacity: 0.7;
}
</style>
