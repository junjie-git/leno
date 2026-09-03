<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { pointsApi } from '@/modules/11-points-membership/api/points.api'
import { couponApi } from '@/modules/08-promotion/api/coupon.api'
import type { PointsAccountDto } from '../types/points.dto'
import type { AvailableCouponDto } from '@/modules/08-promotion/types/promotion.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatPoints, formatPrice, formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 积分兑换页（/points/exchange）
 *
 * 页面结构（对齐设计稿 points-exchange）：
 * NavBar（返回 / 积分兑换 / 我的券入口）
 * → 余额条（蓝色渐变：可用积分 + 元换算 + 100 积分 = 1 元换算胶囊）
 * → 滚动主体：可兑换优惠券双列网格（券面视觉 + 名称 + 适用范围/有效期 +
 *    积分价 + 兑换按钮：可兑换 / 积分不足 / 已抢完 / 已兑换）
 * → 底部固定操作栏（我的优惠券 / 去赚积分，含安全区适配）
 *
 * 数据流：并行 GET /points/account + GET /coupons/claimable（积分可兑换券）；
 * 积分价按契约规则计算（满 N 减 M 券消耗 M × 25 积分，即每 1 元面额 = 25 积分）；
 * 兑换：确认弹层（面额 / 门槛 / 范围 / 有效期 / 兑换后余额）→
 * POST /points/exchange-coupon（couponId + points，服务端校验积分价）→
 * 更新余额条与卡片「已兑换」状态。
 */

const router = useRouter()

/** 积分兑换价规则：每 1 元券面额消耗 25 积分（契约：满 N 减 M 券消耗 M × 25 积分） */
const POINTS_PER_YUAN = 25

// ---- 状态 ----
const firstLoading = ref(true)
const loadError = ref(false)
const account = ref<PointsAccountDto | null>(null)
const coupons = ref<AvailableCouponDto[]>([])
const refreshing = ref(false)
/** 正在提交兑换的券 ID（防重复提交） */
const exchangingId = ref('')
/** 已成功兑换的券 ID 集合 */
const exchangedIds = ref<Set<string>>(new Set())

// ---- 兑换确认弹层 ----
const confirmVisible = ref(false)
const confirmTarget = ref<AvailableCouponDto | null>(null)

// ---- 派生态 ----
/** 当前可用积分 */
const balance = computed(() => account.value?.balance ?? 0)

/** 券的积分兑换价（面额元 × 25） */
function pointsCost(coupon: AvailableCouponDto): number {
  return Math.round((coupon.discount / 100) * POINTS_PER_YUAN)
}

/** 券的兑换按钮状态 */
function exchangeState(coupon: AvailableCouponDto): 'ok' | 'insufficient' | 'sold' | 'exchanged' {
  if (exchangedIds.value.has(coupon.couponId)) return 'exchanged'
  if (coupon.remainCount <= 0) return 'sold'
  if (balance.value < pointsCost(coupon)) return 'insufficient'
  return 'ok'
}

/** 确认弹层：兑换后余额 */
const confirmBalanceAfter = computed(() =>
  confirmTarget.value ? Math.max(0, balance.value - pointsCost(confirmTarget.value)) : 0,
)

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  firstLoading.value = true
  loadError.value = false
  try {
    const [acc, list] = await Promise.all([pointsApi.getAccount(), couponApi.listClaimable()])
    account.value = acc
    coupons.value = list
  } catch (e) {
    logger.error('积分兑换页加载失败', e)
    loadError.value = true
  } finally {
    firstLoading.value = false
    refreshing.value = false
  }
}

onMounted(() => {
  void loadAll()
})

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await loadAll()
}

// ---- 兑换流程 ----
/** 打开兑换确认弹层 */
function openConfirm(coupon: AvailableCouponDto): void {
  if (exchangeState(coupon) !== 'ok' || exchangingId.value) return
  confirmTarget.value = coupon
  confirmVisible.value = true
}

/** 确认兑换 */
async function confirmExchange(): Promise<void> {
  const target = confirmTarget.value
  if (!target || exchangingId.value) return
  exchangingId.value = target.couponId
  try {
    const result = await pointsApi.exchangeCoupon({
      couponId: target.couponId,
      points: pointsCost(target),
    })
    if (account.value) {
      account.value = { ...account.value, balance: result.balanceAfter }
    }
    exchangedIds.value = new Set([...exchangedIds.value, target.couponId])
    confirmVisible.value = false
    showToast(`兑换成功，「${result.couponName}」已放入我的优惠券`)
  } catch (e) {
    logger.warn('积分兑换优惠券失败', e)
    showFailToast(e instanceof Error ? e.message : '兑换失败，请稍后重试')
  } finally {
    exchangingId.value = ''
  }
}

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goMyCoupons(): void {
  router.push('/coupons/mine')
}

function goTasks(): void {
  router.push('/points/tasks')
}
</script>

<template>
  <div class="exchange-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">积分兑换</div>
      <button class="nav-right" type="button" @click="goMyCoupons">
        <van-icon name="coupon-o" size="14" />
        我的券
      </button>
    </header>

    <!-- 余额条 -->
    <section class="balance-bar" role="region" aria-label="积分余额">
      <template v-if="account">
        <div class="bar-left">
          <span class="bar-value" aria-label="积分余额">{{ formatPoints(balance) }}</span>
          <span class="bar-unit">积分</span>
          <span class="bar-conv">≈ ¥{{ formatPriceExact(balance) }}</span>
        </div>
        <div class="bar-right">100 积分 = 1 元</div>
      </template>
      <div v-else class="skeleton-block sk-bar" />
    </section>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="skeleton-wrap">
        <div class="sk-grid">
          <div v-for="i in 4" :key="i" class="skeleton-block sk-card" />
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="兑换列表加载失败"
        description="网络异常，请稍后重试"
        @retry="loadAll"
      />

      <template v-else>
        <van-pull-refresh v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
          <!-- 空态 -->
          <EmptyState
            v-if="coupons.length === 0"
            title="暂无可兑换优惠券"
            action-text="去赚积分"
            @action="goTasks"
          />

          <!-- 兑换网格 -->
          <template v-else>
            <div class="section-title">
              <span class="title-bar" />
              优惠券
              <span class="title-sub">兑换后进入「我的优惠券」</span>
            </div>

            <div class="grid">
              <article
                v-for="coupon in coupons"
                :key="coupon.couponId"
                class="ex-card"
                :class="{ 'ex-card--dim': exchangeState(coupon) === 'sold' }"
                role="article"
                :aria-label="`可兑换优惠券 ${coupon.name}`"
              >
                <!-- 券面视觉 -->
                <div class="ex-img">
                  <svg width="84" height="56" viewBox="0 0 84 56" fill="none">
                    <path d="M6 8h72v14a6 6 0 0 0 0 12v14H6V34a6 6 0 0 0 0-12V8z" fill="#fff" stroke="#FF4D4F" stroke-width="1.6" />
                    <path d="M42 8v40" stroke="#FFCCC7" stroke-width="1.4" stroke-dasharray="3 3" />
                    <text x="24" y="34" font-size="20" font-weight="700" fill="#FF4D4F" text-anchor="middle">¥{{ formatPrice(coupon.discount) }}</text>
                  </svg>
                  <div v-if="exchangeState(coupon) === 'sold'" class="sold-mask">
                    <span class="sold-text">已抢完</span>
                  </div>
                </div>

                <div class="ex-body">
                  <div class="ex-name">{{ coupon.name }}</div>
                  <div class="ex-meta">
                    {{ coupon.scopeText }} · 有效期 {{ coupon.validDays }} 天 · 剩余 {{ coupon.remainCount }} 张
                  </div>
                  <div class="ex-cost">
                    <span class="cost-num">{{ formatPoints(pointsCost(coupon)) }}</span>
                    <span class="cost-unit">积分</span>
                  </div>
                  <button
                    class="ex-btn"
                    :class="{
                      'ex-btn--ok': exchangeState(coupon) === 'ok',
                      'ex-btn--insufficient': exchangeState(coupon) === 'insufficient',
                      'ex-btn--sold': exchangeState(coupon) === 'sold',
                      'ex-btn--exchanged': exchangeState(coupon) === 'exchanged',
                    }"
                    type="button"
                    :disabled="exchangeState(coupon) !== 'ok'"
                    :aria-label="`兑换 ${coupon.name} 消耗 ${pointsCost(coupon)} 积分`"
                    @click="openConfirm(coupon)"
                  >
                    {{
                      exchangingId === coupon.couponId
                        ? '兑换中...'
                        : exchangeState(coupon) === 'exchanged'
                          ? '已兑换'
                          : exchangeState(coupon) === 'sold'
                            ? '已抢完'
                            : exchangeState(coupon) === 'insufficient'
                              ? '积分不足'
                              : '立即兑换'
                    }}
                  </button>
                </div>
              </article>
            </div>
          </template>
        </van-pull-refresh>
      </template>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button class="btn-ghost" type="button" @click="goMyCoupons">我的优惠券</button>
      <button class="btn-primary" type="button" @click="goTasks">去赚积分</button>
    </footer>

    <!-- 兑换确认弹层 -->
    <van-popup
      v-model:show="confirmVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="确认兑换"
    >
      <div v-if="confirmTarget" class="confirm-popup">
        <div class="popup-head">
          <div class="popup-title">确认兑换</div>
          <button class="popup-close" type="button" aria-label="关闭" @click="confirmVisible = false">
            <van-icon name="cross" size="18" />
          </button>
        </div>
        <div class="popup-body">
          <div class="popup-name">{{ confirmTarget.name }}</div>
          <div class="popup-amt">
            <small>消耗</small>
            {{ formatPoints(pointsCost(confirmTarget)) }}
            <small>积分</small>
          </div>
          <div class="popup-cost">兑换后余额 {{ formatPoints(confirmBalanceAfter) }} 积分</div>
          <div class="popup-rows">
            <div class="popup-row">
              <span>面额</span>
              <span>¥{{ formatPrice(confirmTarget.discount) }}</span>
            </div>
            <div class="popup-row">
              <span>使用门槛</span>
              <span>满 ¥{{ formatPrice(confirmTarget.threshold) }} 可用</span>
            </div>
            <div class="popup-row">
              <span>适用范围</span>
              <span>{{ confirmTarget.scopeText }}</span>
            </div>
            <div class="popup-row">
              <span>有效期</span>
              <span>{{ confirmTarget.validDays }} 天</span>
            </div>
          </div>
          <div class="popup-actions">
            <button class="btn-cancel" type="button" @click="confirmVisible = false">取消</button>
            <button
              class="btn-confirm"
              type="button"
              :disabled="!!exchangingId"
              @click="confirmExchange"
            >
              {{ exchangingId ? '兑换中...' : '确认兑换' }}
            </button>
          </div>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.exchange-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n2);
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

.nav-right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: var(--s1);
  font-size: var(--fs-sm);
  color: var(--c-primary);
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
}

/* 余额条 */
.balance-bar {
  background: linear-gradient(135deg, #1677FF 0%, #0958D9 100%);
  color: #fff;
  padding: var(--s4);
  display: flex;
  align-items: center;
  justify-content: space-between;
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
}

.balance-bar::after {
  content: "";
  position: absolute;
  right: -30px;
  top: -30px;
  width: 110px;
  height: 110px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.18) 0%, rgba(255, 255, 255, 0) 70%);
}

.bar-left {
  display: flex;
  align-items: baseline;
  gap: var(--s1);
  position: relative;
  z-index: 1;
}

.bar-value {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
}

.bar-unit {
  font-size: var(--fs-sm);
  opacity: 0.9;
}

.bar-conv {
  font-size: var(--fs-sm);
  opacity: 0.85;
  margin-left: var(--s1);
}

.bar-right {
  display: flex;
  align-items: center;
  gap: var(--s1);
  background: rgba(255, 255, 255, 0.2);
  border-radius: 999px;
  padding: 5px 12px;
  font-size: var(--fs-sm);
  position: relative;
  z-index: 1;
}

.sk-bar {
  height: 22px;
  width: 50%;
  background: rgba(255, 255, 255, 0.3);
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.sk-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s3);
}

.sk-card {
  height: 230px;
  border-radius: var(--r-lg);
}

/* 分类标题 */
.section-title {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin: var(--s2) var(--s1) var(--s3);
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
}

.title-bar {
  width: 3px;
  height: 14px;
  background: var(--c-primary);
  border-radius: 2px;
}

.title-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  font-weight: var(--fw-normal);
  margin-left: auto;
}

/* 兑换网格 */
.grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s3);
}

.ex-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.ex-card--dim {
  opacity: 0.7;
}

.ex-img {
  height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  background: linear-gradient(135deg, #FFF1F0 0%, #FFE7E6 100%);
}

.sold-mask {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
}

.sold-text {
  color: #fff;
  font-size: var(--fs-sm);
  font-weight: var(--fw-semibold);
  background: rgba(0, 0, 0, 0.4);
  padding: 4px 12px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.6);
}

.ex-body {
  padding: var(--s2) var(--s3) var(--s3);
  display: flex;
  flex-direction: column;
  gap: var(--s1);
  flex: 1;
}

.ex-name {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
  line-height: 1.3;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.ex-meta {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.ex-cost {
  display: flex;
  align-items: baseline;
  gap: 2px;
  margin-top: var(--s1);
}

.cost-num {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: #D48806;
}

.cost-unit {
  font-size: var(--fs-sm);
  color: #D48806;
}

.ex-btn {
  margin-top: var(--s2);
  border: none;
  border-radius: 999px;
  padding: 7px 0;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  text-align: center;
}

.ex-btn--ok {
  background: linear-gradient(135deg, #FAAD14, #D48806);
  color: #fff;
}

.ex-btn--insufficient {
  background: var(--n3);
  color: var(--n7);
  cursor: not-allowed;
}

.ex-btn--sold {
  background: var(--n5);
  color: #fff;
  cursor: not-allowed;
}

.ex-btn--exchanged {
  background: #F0FBEB;
  color: var(--c-success);
  cursor: not-allowed;
}

/* 底部固定操作栏 */
.action-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
}

.btn-ghost,
.btn-primary {
  flex: 1;
  height: 40px;
  border-radius: 999px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-ghost {
  border: 1.5px solid var(--c-primary);
  background: var(--n1);
  color: var(--c-primary);
}

.btn-primary {
  border: none;
  background: var(--c-primary);
  color: #fff;
}

/* 兑换确认弹层 */
.confirm-popup {
  padding-bottom: calc(var(--s4) + env(safe-area-inset-bottom));
}

.popup-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s4);
  border-bottom: 1px solid var(--n3);
}

.popup-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
}

.popup-close {
  color: var(--n7);
  display: flex;
  background: none;
  border: none;
  cursor: pointer;
  padding: 0;
}

.popup-body {
  padding: var(--s4);
}

.popup-name {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
  text-align: center;
}

.popup-amt {
  text-align: center;
  color: var(--c-error);
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  margin: var(--s2) 0;
}

.popup-amt small {
  font-size: var(--fs-base);
}

.popup-cost {
  text-align: center;
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-bottom: var(--s3);
}

.popup-rows {
  background: var(--n2);
  border-radius: var(--r-card);
  padding: var(--s2) var(--s3);
}

.popup-row {
  display: flex;
  justify-content: space-between;
  font-size: var(--fs-sm);
  padding: 6px 0;
  border-bottom: 1px solid var(--n3);
}

.popup-row:last-child {
  border-bottom: none;
}

.popup-row span:first-child {
  color: var(--n7);
}

.popup-actions {
  display: flex;
  gap: var(--s2);
  margin-top: var(--s4);
}

.btn-cancel,
.btn-confirm {
  flex: 1;
  border: none;
  border-radius: var(--r-base);
  padding: 11px 0;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
}

.btn-cancel {
  background: var(--n3);
  color: var(--n9);
}

.btn-confirm {
  background: linear-gradient(135deg, #FAAD14, #D48806);
  color: #fff;
}

.btn-confirm:disabled {
  opacity: 0.7;
}
</style>
