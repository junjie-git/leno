<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { pointsApi } from '@/modules/11-points-membership/api/points.api'
import type {
  PointsAccountDto,
  PointsLedgerEntryDto,
  PointsLedgerType,
} from '../types/points.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate, formatPoints, formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 积分流水页（/points/ledger）
 *
 * 页面结构（对齐设计稿 points-ledger）：
 * NavBar（返回 / 积分流水）
 * → 余额汇总条（蓝色渐变：可用积分 + 元换算 + 累计收入 / 累计支出统计）
 * → 类型筛选 Tab（全部 / 收入 / 支出，前端过滤已加载数据）
 * → 滚动主体：按日期分组（今日 / 昨日 / 更早）的流水卡片（来源图标 + 类型标签 +
 *    描述 + 变动积分（+绿 / -红）+ 变动后余额 + 时间），van-list 无限滚动 + 下拉刷新
 * → 底部固定操作栏（去赚积分，含安全区适配）
 *
 * 数据流：并行 GET /points/account + GET /points/ledger（全量倒序）；
 * Tab 切换为前端过滤（收入 = 积分变动 > 0，支出 = 积分变动 < 0）；
 * 分页为本地切片（每页 20 条）。
 */

const router = useRouter()

/** 本地分页大小 */
const PAGE_SIZE = 20

/** 类型筛选 Tab */
const TABS = [
  { key: 'All', label: '全部' },
  { key: 'Earn', label: '收入' },
  { key: 'Spend', label: '支出' },
] as const

type TabKey = (typeof TABS)[number]['key']

/** 流水类型 → 标签文案与配色 */
const TYPE_META: Record<PointsLedgerType, { label: string; cls: string }> = {
  Earn: { label: '收入', cls: 'tg-earn' },
  Spend: { label: '支出', cls: 'tg-spend' },
  Expire: { label: '过期', cls: 'tg-expire' },
  Adjust: { label: '调整', cls: 'tg-adjust' },
}

// ---- 状态 ----
const activeTab = ref<TabKey>('All')
const firstLoading = ref(true)
const loadError = ref(false)
const account = ref<PointsAccountDto | null>(null)
const entries = ref<PointsLedgerEntryDto[]>([])
const visibleCount = ref(PAGE_SIZE)
const refreshing = ref(false)
const listLoading = ref(false)

// ---- 派生态 ----
/** 当前 Tab 过滤后的流水（收入 = 积分变动 > 0，支出 = 积分变动 < 0） */
const filteredEntries = computed(() => {
  if (activeTab.value === 'Earn') return entries.value.filter((e) => e.points > 0)
  if (activeTab.value === 'Spend') return entries.value.filter((e) => e.points < 0)
  return entries.value
})

/** 当前展示的流水（本地分页切片） */
const displayedEntries = computed(() => filteredEntries.value.slice(0, visibleCount.value))

/** 是否已全部加载 */
const listFinished = computed(() => visibleCount.value >= filteredEntries.value.length)

/** 日期分组的流水（今日 / 昨日 / 更早） */
interface LedgerGroup {
  label: string
  items: PointsLedgerEntryDto[]
}

const groupedEntries = computed<LedgerGroup[]>(() => {
  const today = formatDate(new Date().toISOString())
  const yesterday = formatDate(new Date(Date.now() - 86_400_000).toISOString())
  const groups: LedgerGroup[] = []
  for (const entry of displayedEntries.value) {
    const day = formatDate(entry.createdAt)
    const label = day === today ? '今日' : day === yesterday ? '昨日' : '更早'
    const last = groups[groups.length - 1]
    if (last && last.label === label) {
      last.items.push(entry)
    } else {
      groups.push({ label, items: [entry] })
    }
  }
  return groups
})

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  firstLoading.value = true
  loadError.value = false
  try {
    const [acc, list] = await Promise.all([pointsApi.getAccount(), pointsApi.getLedger()])
    account.value = acc
    entries.value = list
    visibleCount.value = PAGE_SIZE
  } catch (e) {
    logger.error('积分流水加载失败', e)
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

/** van-list 无限加载（本地分页） */
function onLoad(): void {
  visibleCount.value += PAGE_SIZE
  listLoading.value = false
}

/** 切换类型 Tab（重置本地分页） */
function setTab(key: TabKey): void {
  if (activeTab.value === key) return
  activeTab.value = key
  visibleCount.value = PAGE_SIZE
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

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goTasks(): void {
  router.push('/points/tasks')
}
</script>

<template>
  <div class="ledger-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">积分流水</div>
    </header>

    <!-- 余额汇总条 -->
    <section class="summary" role="region" aria-label="积分汇总">
      <template v-if="account">
        <div class="summary-balance">
          <span class="summary-label">可用积分</span>
          <span class="summary-value" aria-label="可用积分">{{ formatPoints(account.balance) }}</span>
          <span class="summary-conv">≈ ¥{{ formatPriceExact(account.balance) }}</span>
        </div>
        <div class="summary-stats">
          <div class="summary-stat">
            <div class="stat-label">累计收入</div>
            <div class="stat-value earn">+{{ formatPoints(account.totalEarned) }}</div>
          </div>
          <div class="summary-stat">
            <div class="stat-label">累计支出</div>
            <div class="stat-value spend">-{{ formatPoints(account.totalSpent) }}</div>
          </div>
        </div>
      </template>
      <div v-else class="skeleton-block sk-summary" />
    </section>

    <!-- 类型筛选 Tab -->
    <nav class="tabs" role="tablist" aria-label="流水类型筛选">
      <div
        v-for="tab in TABS"
        :key="tab.key"
        class="tab"
        :class="{ active: activeTab === tab.key }"
        role="tab"
        :aria-selected="activeTab === tab.key"
        @click="setTab(tab.key)"
      >
        {{ tab.label }}
      </div>
    </nav>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="skeleton-wrap">
        <div v-for="i in 5" :key="i" class="skeleton-block sk-item" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError && entries.length === 0"
        title="积分流水加载失败"
        description="网络异常，请稍后重试"
        @retry="loadAll"
      />

      <template v-else>
        <van-pull-refresh v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
          <!-- 空态 -->
          <EmptyState
            v-if="filteredEntries.length === 0"
            title="暂无积分记录"
            action-text="去赚积分"
            @action="goTasks"
          />

          <!-- 流水列表（日期分组） -->
          <van-list
            v-else
            v-model:loading="listLoading"
            :finished="listFinished"
            finished-text="没有更多了"
            loading-text="加载中..."
            @load="onLoad"
          >
            <section
              v-for="group in groupedEntries"
              :key="group.label"
              class="date-group"
            >
              <div class="date-title">{{ group.label }}</div>
              <div class="ledger-card">
                <div
                  v-for="entry in group.items"
                  :key="entry.id"
                  class="ledger-item"
                  role="article"
                  :aria-label="`${entry.description} ${entry.points > 0 ? '获取' : '消耗'} ${Math.abs(entry.points)} 积分`"
                >
                  <span class="ledger-ico" :class="ledgerIcon(entry).cls">
                    <van-icon :name="ledgerIcon(entry).name" size="20" />
                  </span>
                  <div class="ledger-main">
                    <div class="ledger-top">
                      <span class="ledger-desc">{{ entry.description }}</span>
                      <span class="ledger-tag" :class="TYPE_META[entry.type].cls">
                        {{ TYPE_META[entry.type].label }}
                      </span>
                    </div>
                    <div class="ledger-sub">{{ formatDate(entry.createdAt) }}</div>
                  </div>
                  <div class="ledger-right">
                    <div class="ledger-amt" :class="entry.points > 0 ? 'earn' : 'spend'">
                      {{ entry.points > 0 ? `+${formatPoints(entry.points)}` : formatPoints(entry.points) }}
                    </div>
                    <div class="ledger-after">余额 {{ formatPoints(entry.balanceAfter) }}</div>
                  </div>
                </div>
              </div>
            </section>
          </van-list>
        </van-pull-refresh>
      </template>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button class="btn-primary" type="button" @click="goTasks">去赚积分</button>
    </footer>
  </div>
</template>

<style scoped>
.ledger-page {
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

/* 余额汇总条 */
.summary {
  background: linear-gradient(135deg, #1677FF 0%, #0958D9 100%);
  color: #fff;
  padding: var(--s4);
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
}

.summary::after {
  content: "";
  position: absolute;
  right: -30px;
  top: -40px;
  width: 120px;
  height: 120px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.16) 0%, rgba(255, 255, 255, 0) 70%);
}

.summary-balance {
  display: flex;
  align-items: baseline;
  gap: var(--s2);
  position: relative;
  z-index: 1;
}

.summary-label {
  font-size: var(--fs-sm);
  opacity: 0.9;
}

.summary-value {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  letter-spacing: 0.5px;
}

.summary-conv {
  font-size: var(--fs-sm);
  opacity: 0.85;
}

.summary-stats {
  display: grid;
  grid-template-columns: 1fr 1fr;
  margin-top: var(--s4);
  gap: var(--s2);
  position: relative;
  z-index: 1;
}

.summary-stat {
  background: rgba(255, 255, 255, 0.16);
  border-radius: var(--r-card);
  padding: var(--s2) var(--s3);
}

.stat-label {
  font-size: var(--fs-sm);
  opacity: 0.9;
}

.stat-value {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  margin-top: 2px;
}

.stat-value.earn {
  color: #B7EB8F;
}

.stat-value.spend {
  color: #FFA39E;
}

.sk-summary {
  height: 88px;
  border-radius: 0;
  background: var(--n3);
}

/* 类型筛选 Tab */
.tabs {
  display: flex;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  flex-shrink: 0;
}

.tab {
  flex: 1;
  text-align: center;
  padding: 12px 0;
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
}

.tab.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.tab.active::after {
  content: "";
  position: absolute;
  left: 50%;
  bottom: 0;
  width: 20px;
  height: 2px;
  background: var(--c-primary);
  border-radius: 2px;
  transform: translateX(-50%);
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
  gap: var(--s2);
}

.sk-item {
  height: 64px;
  border-radius: var(--r-lg);
}

/* 日期分组 */
.date-group {
  margin-bottom: var(--s3);
}

.date-title {
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  color: var(--n7);
  padding: 0 var(--s1) var(--s2);
}

/* 流水卡片 */
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

.ledger-item:last-child {
  border-bottom: none;
}

.ledger-ico {
  width: 36px;
  height: 36px;
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

.ledger-top {
  display: flex;
  align-items: center;
  gap: var(--s1);
  min-width: 0;
}

.ledger-desc {
  font-size: var(--fs-base);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ledger-tag {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: var(--r-base);
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

.tg-earn {
  background: #F0FBEB;
  color: var(--c-success);
}

.tg-spend {
  background: #FFF1F0;
  color: var(--c-error);
}

.tg-expire {
  background: #FFF7E6;
  color: var(--c-warning);
}

.tg-adjust {
  background: #E6F0FF;
  color: var(--c-primary);
}

.ledger-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 3px;
}

.ledger-right {
  flex-shrink: 0;
  text-align: right;
}

.ledger-amt {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
}

.ledger-amt.earn {
  color: var(--c-success);
}

.ledger-amt.spend {
  color: var(--c-error);
}

.ledger-after {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

/* 底部固定操作栏 */
.action-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
}

.btn-primary {
  width: 100%;
  height: 40px;
  border: none;
  border-radius: 999px;
  background: var(--c-primary);
  color: #fff;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
