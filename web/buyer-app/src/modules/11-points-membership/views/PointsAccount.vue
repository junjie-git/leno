<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { pointsApi } from '@/modules/11-points-membership/api/points.api'
import type { PointsAccountDto, PointsLedgerEntryDto } from '../types/points.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate, formatDateTime, formatPoints, formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 积分账户页（/points/account）
 *
 * 页面结构（对齐设计稿 points-account）：
 * NavBar（返回 / 我的积分）→ 滚动主体：
 *   积分余额卡（金色渐变：可用积分大字 + 元换算 + 连签天数胶囊 + 去兑换按钮）
 *   → 累计统计三宫格（累计获取 / 累计消耗 / 即将过期）
 *   → 快捷入口六宫格（签到 / 任务 / 兑换 / 明细 / 等级 / 套餐）
 *   → 过期提醒条（expiringPoints > 0 时展示）
 *   → 近期流水预览（最近 3 条 + 查看全部）
 * → 底部固定操作栏（去兑换 / 每日签到，含安全区适配）
 *
 * 数据流：并行 GET /points/account + GET /points/ledger（取前 3 条预览）；
 * 签到 POST /points/check-in → 更新余额与签到态并刷新流水预览；
 * 各入口跳转对应模块页面（签到页 / 任务中心 / 积分兑换 / 积分流水 / 会员等级 / 会员套餐）。
 */

const router = useRouter()

// ---- 快捷入口（六宫格） ----
interface QuickEntry {
  label: string
  icon: string
  cls: string
  to: string
}

const QUICK_ENTRIES: QuickEntry[] = [
  { label: '每日签到', icon: 'calendar-o', cls: 'q-gold', to: '/points/check-in' },
  { label: '任务中心', icon: 'medal-o', cls: 'q-purple', to: '/points/tasks' },
  { label: '积分兑换', icon: 'gift-o', cls: 'q-blue', to: '/points/exchange' },
  { label: '流水明细', icon: 'bill-o', cls: 'q-green', to: '/points/ledger' },
  { label: '会员等级', icon: 'diamond-o', cls: 'q-gold', to: '/member/level' },
  { label: '会员套餐', icon: 'vip-card-o', cls: 'q-purple', to: '/member/packages' },
]

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const account = ref<PointsAccountDto | null>(null)
const ledger = ref<PointsLedgerEntryDto[]>([])
const refreshing = ref(false)
const checkingIn = ref(false)

// ---- 派生态 ----
/** 可用积分（千分位） */
const balanceText = computed(() => formatPoints(account.value?.balance ?? 0))

/** 换算金额（100 积分 = 1 元，积分值即分数） */
const convText = computed(() => `≈ ¥${formatPriceExact(account.value?.balance ?? 0)}`)

/** 近期流水预览（最近 3 条） */
const previewEntries = computed(() => ledger.value.slice(0, 3))

/** 是否展示过期提醒条 */
const showExpiringNotice = computed(
  () => !!account.value && account.value.expiringPoints > 0 && !!account.value.expiringAt,
)

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    const [acc, list] = await Promise.all([pointsApi.getAccount(), pointsApi.getLedger()])
    account.value = acc
    ledger.value = list
  } catch (e) {
    logger.error('积分账户加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
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

/** 流水来源图标（按描述关键词推导：订单 / 签到 / 兑换 / 任务 / 过期） */
function ledgerIcon(entry: PointsLedgerEntryDto): { name: string; cls: string } {
  if (entry.type === 'Expire') return { name: 'clock-o', cls: 'li-gray' }
  const d = entry.description
  if (d.includes('签到')) return { name: 'calendar-o', cls: 'li-gold' }
  if (d.includes('兑换')) return { name: 'coupon-o', cls: 'li-red' }
  if (d.includes('订单') || d.includes('下单')) return { name: 'shopping-cart-o', cls: 'li-blue' }
  if (d.includes('任务') || d.includes('评价') || d.includes('分享') || d.includes('浏览')) {
    return { name: 'medal-o', cls: 'li-purple' }
  }
  return { name: 'bill-o', cls: 'li-blue' }
}

// ---- 签到 ----
async function doCheckIn(): Promise<void> {
  if (!account.value || account.value.checkedInToday || checkingIn.value) return
  checkingIn.value = true
  try {
    const result = await pointsApi.checkIn()
    account.value = {
      ...account.value,
      balance: result.balanceAfter,
      totalEarned: account.value.totalEarned + result.earnedPoints,
      checkedInToday: true,
      checkInStreakDays: result.streakDays,
    }
    showToast(`签到成功，获得 ${result.earnedPoints} 积分`)
    // 刷新近期流水预览（失败静默，保留旧数据）
    try {
      ledger.value = await pointsApi.getLedger()
    } catch (e) {
      logger.warn('签到后流水预览刷新失败（忽略）', e)
    }
  } catch (e) {
    logger.warn('每日签到失败', e)
    showFailToast(e instanceof Error ? e.message : '签到失败，请稍后重试')
  } finally {
    checkingIn.value = false
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

function goEntry(entry: QuickEntry): void {
  router.push(entry.to)
}

function goLedger(): void {
  router.push('/points/ledger')
}

function goTasks(): void {
  router.push('/points/tasks')
}

function goExchange(): void {
  router.push('/points/exchange')
}
</script>

<template>
  <div class="points-account-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">我的积分</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeleton-wrap">
        <div class="skeleton-block sk-balance" />
        <div class="sk-stats">
          <div v-for="i in 3" :key="i" class="skeleton-block sk-stat" />
        </div>
        <div class="skeleton-block sk-quick" />
        <div class="skeleton-block sk-preview" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError || !account"
        title="积分信息加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadAll"
      />

      <!-- 内容 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <!-- 积分余额卡 -->
        <section class="balance-card" role="region" aria-label="积分余额">
          <div class="balance-label">
            <van-icon name="gold-o" size="14" />
            可用积分
          </div>
          <div class="balance-amount" aria-label="当前积分">{{ balanceText }}</div>
          <div class="balance-conv">{{ convText }}（100 积分 = 1 元）</div>
          <div class="balance-row">
            <span class="balance-pill">
              <van-icon name="calendar-o" size="12" />
              连续签到 {{ account.checkInStreakDays }} 天
            </span>
            <button class="btn-exchange" type="button" @click="goExchange">
              去兑换
              <van-icon name="arrow" size="12" />
            </button>
          </div>
        </section>

        <!-- 累计统计 -->
        <section class="stats">
          <div class="stat-card">
            <div class="stat-label">累计获取</div>
            <div class="stat-value earn">+{{ formatPoints(account.totalEarned) }}</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">累计消耗</div>
            <div class="stat-value spend">-{{ formatPoints(account.totalSpent) }}</div>
          </div>
          <div class="stat-card">
            <div class="stat-label">即将过期</div>
            <div class="stat-value warn">{{ formatPoints(account.expiringPoints) }}</div>
          </div>
        </section>

        <!-- 快捷入口 -->
        <section class="quick-grid" role="grid" aria-label="积分快捷入口">
          <button
            v-for="entry in QUICK_ENTRIES"
            :key="entry.label"
            class="quick-item"
            type="button"
            role="gridcell"
            :aria-label="entry.label"
            @click="goEntry(entry)"
          >
            <span class="quick-icon" :class="entry.cls">
              <van-icon :name="entry.icon" size="22" />
            </span>
            <span class="quick-text">{{ entry.label }}</span>
          </button>
        </section>

        <!-- 过期提醒条 -->
        <div v-if="showExpiringNotice" class="notice" role="alert">
          <van-icon name="warning-o" size="16" class="notice-icon" />
          <span class="notice-text">
            {{ formatPoints(account.expiringPoints) }} 积分将于
            {{ formatDate(account.expiringAt ?? '') }} 过期，记得及时使用
          </span>
          <button class="notice-link" type="button" @click="goExchange">去兑换</button>
        </div>

        <!-- 近期流水 -->
        <div class="section-head">
          <div class="section-title">
            <van-icon name="bill-o" size="16" />
            近期流水
          </div>
          <button class="section-more" type="button" @click="goLedger">
            查看全部
            <van-icon name="arrow" size="12" />
          </button>
        </div>

        <EmptyState
          v-if="previewEntries.length === 0"
          title="暂无流水"
          action-text="去赚积分"
          @action="goTasks"
        />
        <section v-else class="ledger-card">
          <div
            v-for="entry in previewEntries"
            :key="entry.id"
            class="ledger-item"
            role="article"
            :aria-label="`${entry.description} ${entry.points > 0 ? '+' : ''}${entry.points} 积分`"
          >
            <span class="ledger-ico" :class="ledgerIcon(entry).cls">
              <van-icon :name="ledgerIcon(entry).name" size="18" />
            </span>
            <div class="ledger-main">
              <div class="ledger-desc">{{ entry.description }}</div>
              <div class="ledger-time">{{ formatDateTime(entry.createdAt) }}</div>
            </div>
            <div class="ledger-amt" :class="entry.points > 0 ? 'earn' : 'spend'">
              {{ entry.points > 0 ? `+${formatPoints(entry.points)}` : formatPoints(entry.points) }}
            </div>
          </div>
          <button class="ledger-foot" type="button" @click="goLedger">查看全部流水</button>
        </section>
      </van-pull-refresh>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button class="btn-ghost" type="button" @click="goExchange">去兑换</button>
      <button
        class="btn-primary"
        :class="{ disabled: account?.checkedInToday }"
        type="button"
        :disabled="account?.checkedInToday || checkingIn"
        :aria-label="account?.checkedInToday ? '今日已签到' : '每日签到'"
        @click="doCheckIn"
      >
        {{ checkingIn ? '签到中...' : account?.checkedInToday ? '已签到' : '每日签到' }}
      </button>
    </footer>
  </div>
</template>

<style scoped>
.points-account-page {
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

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.skeleton-wrap {
  display: flex;
  flex-direction: column;
}

.sk-balance {
  height: 140px;
  border-radius: var(--r-lg);
}

.sk-stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--s2);
  margin-top: var(--s3);
}

.sk-stat {
  height: 64px;
  border-radius: var(--r-lg);
}

.sk-quick {
  height: 150px;
  margin-top: var(--s3);
  border-radius: var(--r-lg);
}

.sk-preview {
  height: 180px;
  margin-top: var(--s3);
  border-radius: var(--r-lg);
}

/* 积分余额卡 */
.balance-card {
  border-radius: var(--r-lg);
  background: linear-gradient(135deg, #FAAD14 0%, #D48806 100%);
  padding: var(--s6) var(--s4);
  color: #fff;
  position: relative;
  overflow: hidden;
  box-shadow: 0 6px 16px rgba(212, 136, 6, 0.25);
}

.balance-card::before {
  content: "";
  position: absolute;
  right: -40px;
  top: -40px;
  width: 160px;
  height: 160px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.22) 0%, rgba(255, 255, 255, 0) 70%);
}

.balance-label {
  font-size: var(--fs-sm);
  opacity: 0.9;
  display: flex;
  align-items: center;
  gap: var(--s1);
  position: relative;
  z-index: 1;
}

.balance-amount {
  font-size: var(--fs-3xl);
  font-weight: var(--fw-semibold);
  line-height: 1.2;
  margin-top: var(--s1);
  letter-spacing: 0.5px;
  position: relative;
  z-index: 1;
}

.balance-conv {
  font-size: var(--fs-sm);
  opacity: 0.85;
  margin-top: var(--s1);
  position: relative;
  z-index: 1;
}

.balance-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--s4);
  position: relative;
  z-index: 1;
}

.balance-pill {
  display: inline-flex;
  align-items: center;
  gap: var(--s1);
  background: rgba(255, 255, 255, 0.22);
  border-radius: 999px;
  padding: 4px 10px;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
}

.btn-exchange {
  background: var(--n1);
  color: #D48806;
  border: none;
  border-radius: 999px;
  padding: 8px 18px;
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  display: inline-flex;
  align-items: center;
  gap: var(--s1);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.12);
  cursor: pointer;
  font-family: inherit;
}

/* 累计统计 */
.stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--s2);
  margin-top: var(--s3);
}

.stat-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3) var(--s2);
  box-shadow: var(--sh-card);
  text-align: center;
}

.stat-label {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.stat-value {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  margin-top: var(--s1);
}

.stat-value.earn {
  color: var(--c-success);
}

.stat-value.spend {
  color: var(--c-error);
}

.stat-value.warn {
  color: var(--c-warning);
}

/* 快捷入口 */
.quick-grid {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4) var(--s2);
  margin-top: var(--s3);
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--s2);
}

.quick-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--s2);
  padding: var(--s2) var(--s1);
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
}

.quick-icon {
  width: 44px;
  height: 44px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
}

.q-gold {
  background: #FFF7E6;
  color: var(--c-warning);
}

.q-purple {
  background: #F3E8FF;
  color: var(--c-buyer);
}

.q-blue {
  background: #E6F0FF;
  color: var(--c-primary);
}

.q-green {
  background: #F0FBEB;
  color: var(--c-success);
}

.quick-text {
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* 提示条 */
.notice {
  display: flex;
  align-items: center;
  gap: var(--s2);
  background: #FFF7E6;
  border: 1px solid #FFE7BA;
  border-radius: var(--r-lg);
  padding: var(--s2) var(--s3);
  margin-top: var(--s3);
}

.notice-icon {
  color: var(--c-warning);
  flex-shrink: 0;
}

.notice-text {
  flex: 1;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.notice-link {
  color: var(--c-primary);
  font-size: var(--fs-sm);
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
}

/* 区块标题 */
.section-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: var(--s4) var(--s1) var(--s2);
}

.section-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.section-title :deep(.van-icon) {
  color: var(--c-primary);
}

.section-more {
  font-size: var(--fs-sm);
  color: var(--n7);
  display: flex;
  align-items: center;
  gap: 2px;
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  padding: 0;
}

/* 流水预览 */
.ledger-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.ledger-item {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3) var(--s4);
  border-bottom: 1px solid var(--n3);
}

.ledger-item:last-of-type {
  border-bottom: none;
}

.ledger-ico {
  width: 32px;
  height: 32px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.li-gold {
  background: #FFF7E6;
  color: var(--c-warning);
}

.li-red {
  background: #FFF1F0;
  color: var(--c-error);
}

.li-blue {
  background: #E6F0FF;
  color: var(--c-primary);
}

.li-purple {
  background: #F3E8FF;
  color: var(--c-buyer);
}

.li-gray {
  background: var(--n3);
  color: var(--n7);
}

.ledger-main {
  flex: 1;
  min-width: 0;
}

.ledger-desc {
  font-size: var(--fs-base);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ledger-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.ledger-amt {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  flex-shrink: 0;
}

.ledger-amt.earn {
  color: var(--c-success);
}

.ledger-amt.spend {
  color: var(--c-error);
}

.ledger-foot {
  display: block;
  width: 100%;
  text-align: center;
  padding: var(--s3);
  font-size: var(--fs-sm);
  color: var(--c-primary);
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
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

.btn-primary.disabled {
  background: var(--n5);
  cursor: not-allowed;
}
</style>
