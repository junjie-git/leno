<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast } from 'vant'
import { pointsApi } from '@/modules/11-points-membership/api/points.api'
import type { PointsAccountDto } from '../types/points.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatPoints } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 每日签到页（/points/check-in）
 *
 * 页面结构（对齐设计稿 check-in）：
 * NavBar（返回 / 每日签到）→ 滚动主体：
 *   签到状态卡（蓝色渐变：已连续签到天数 + 当前积分 + 今日签到状态文案）
 *   → 连续签到奖励进度（7 天一轮回，第 7 天额外 +20，已签 / 待签 / 当前高亮）
 *   → 本月签到日历（周一开头，已签日期主色圆点、今日高亮可点击签到，可翻月查看）
 *   → 签到规则说明
 * → 底部固定操作栏（今日签到 / 已签到，含安全区适配）
 *
 * 数据流：GET /points/account 获取连签天数与今日签到态（本月已签日期由连签天数前端推导）；
 * 签到 POST /points/check-in → 弹出签到成功浮层并更新状态卡 / 奖励进度 / 日历。
 */

/** 签到基础奖励（积分/天） */
const BASE_REWARD = 5
/** 连签额外奖励触发天（第 7 天） */
const BONUS_DAY = 7
/** 连签第 7 天额外奖励（积分） */
const BONUS_REWARD = 20

/** 连签奖励轮回长度（天） */
const CYCLE_LENGTH = 7

const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const account = ref<PointsAccountDto | null>(null)
const checkingIn = ref(false)
/** 本次会话内签到获得的积分（用于「已签到 +X」展示） */
const todayEarned = ref(0)

// ---- 签到成功浮层 ----
const showSuccess = ref(false)
const successInfo = ref({ earned: 0, streak: 0, bonus: false })

// ---- 日历视图月份 ----
const now = new Date()
const viewYear = ref(now.getFullYear())
/** 0-11 */
const viewMonth = ref(now.getMonth())

const WEEK_LABELS = ['一', '二', '三', '四', '五', '六', '日']

/** 日历单元格 */
interface CalendarCell {
  day: number
  signed: boolean
  isToday: boolean
  future: boolean
}

// ---- 派生态 ----
/** 当前连签轮回内已签天数（1-7；连签中断/满轮后重新计算） */
const signedInCycle = computed(() => {
  const streak = account.value?.checkInStreakDays ?? 0
  if (streak <= 0) return 0
  const rest = streak % CYCLE_LENGTH
  if (rest === 0) return account.value?.checkedInToday ? CYCLE_LENGTH : 0
  return rest
})

/** 下一次签到对应的轮回天数（1-7） */
const nextCycleDay = computed(() => (signedInCycle.value % CYCLE_LENGTH) + 1)

/** 今日签到可获得的积分（第 7 天含额外奖励） */
const todayReward = computed(() => BASE_REWARD + (nextCycleDay.value === BONUS_DAY ? BONUS_REWARD : 0))

/** 连签奖励进度数据（7 天） */
interface RewardDay {
  day: number
  points: number
  done: boolean
  current: boolean
  bonus: boolean
}

const rewardDays = computed<RewardDay[]>(() => {
  const checked = account.value?.checkedInToday ?? false
  return Array.from({ length: CYCLE_LENGTH }, (_, i) => {
    const day = i + 1
    return {
      day,
      points: BASE_REWARD + (day === BONUS_DAY ? BONUS_REWARD : 0),
      done: day <= signedInCycle.value,
      // 未签到时高亮下一次要签的天；已签到时高亮今天已签的天
      current: checked ? day === signedInCycle.value && day > 0 : day === nextCycleDay.value,
      bonus: day === BONUS_DAY,
    }
  })
})

/** 连签进度条填充宽度 */
const rewardFillWidth = computed(() => `${(signedInCycle.value / CYCLE_LENGTH) * 100}%`)

/** 状态卡副文案 */
const statusSub = computed(() => {
  const acc = account.value
  if (!acc) return ''
  if (acc.checkedInToday) {
    return todayEarned.value > 0
      ? `今日已签到，获得 +${todayEarned.value} 积分`
      : '今日已签到，明天再来吧'
  }
  return `今日签到可得 +${todayReward.value} 积分`
})

/** 日历月份标题 */
const monthLabel = computed(() => `${viewYear.value} 年 ${viewMonth.value + 1} 月`)

/** 是否已翻到当前月份（禁止翻到未来月份） */
const atCurrentMonth = computed(
  () => viewYear.value === now.getFullYear() && viewMonth.value === now.getMonth(),
)

/** 本月已签到日期集合（由连签天数 + 今日签到态推导） */
const signedDaySet = computed<Set<number>>(() => {
  const set = new Set<number>()
  const acc = account.value
  if (!acc || acc.checkInStreakDays <= 0) return set
  // 连签截止日：已签到含今天，未签到截止昨天
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  if (!acc.checkedInToday) end.setDate(end.getDate() - 1)
  const streak = Math.min(acc.checkInStreakDays, 62)
  for (let i = 0; i < streak; i++) {
    const d = new Date(end.getFullYear(), end.getMonth(), end.getDate() - i)
    if (d.getFullYear() === viewYear.value && d.getMonth() === viewMonth.value) {
      set.add(d.getDate())
    }
  }
  return set
})

/** 本月日历单元格（周内空白日期用 null 填充，周一开头） */
const calendarCells = computed<(CalendarCell | null)[]>(() => {
  const y = viewYear.value
  const m = viewMonth.value
  const firstDay = new Date(y, m, 1)
  const daysInMonth = new Date(y, m + 1, 0).getDate()
  const leading = (firstDay.getDay() + 6) % 7
  const signed = signedDaySet.value
  const today = new Date()
  const cells: (CalendarCell | null)[] = []
  for (let i = 0; i < leading; i++) cells.push(null)
  for (let day = 1; day <= daysInMonth; day++) {
    const isToday = y === today.getFullYear() && m === today.getMonth() && day === today.getDate()
    const future = new Date(y, m, day).getTime() > today.getTime() && !isToday
    cells.push({ day, signed: signed.has(day), isToday, future })
  }
  return cells
})

// ---- 数据加载 ----
async function loadAccount(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    account.value = await pointsApi.getAccount()
  } catch (e) {
    logger.error('签到页积分账户加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadAccount()
})

// ---- 签到 ----
async function doCheckIn(): Promise<void> {
  if (!account.value || account.value.checkedInToday || checkingIn.value) return
  checkingIn.value = true
  try {
    const result = await pointsApi.checkIn()
    account.value = {
      ...account.value,
      balance: result.balanceAfter,
      checkedInToday: true,
      checkInStreakDays: result.streakDays,
    }
    todayEarned.value = result.earnedPoints
    successInfo.value = {
      earned: result.earnedPoints,
      streak: result.streakDays,
      bonus: result.earnedPoints > BASE_REWARD,
    }
    showSuccess.value = true
  } catch (e) {
    logger.warn('每日签到失败', e)
    showFailToast(e instanceof Error ? e.message : '签到失败，请稍后重试')
  } finally {
    checkingIn.value = false
  }
}

/** 点击日历单元格（仅今日可触发签到） */
function onCellClick(cell: CalendarCell | null): void {
  if (cell?.isToday && !account.value?.checkedInToday) {
    void doCheckIn()
  }
}

// ---- 翻月 ----
function prevMonth(): void {
  if (viewMonth.value === 0) {
    viewYear.value -= 1
    viewMonth.value = 11
  } else {
    viewMonth.value -= 1
  }
}

function nextMonth(): void {
  if (atCurrentMonth.value) return
  if (viewMonth.value === 11) {
    viewYear.value += 1
    viewMonth.value = 0
  } else {
    viewMonth.value += 1
  }
}

// ---- 返回 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}
</script>

<template>
  <div class="checkin-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">每日签到</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeleton-wrap">
        <div class="skeleton-block sk-card" />
        <div class="skeleton-block sk-block" />
        <div class="skeleton-block sk-calendar" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError || !account"
        title="签到信息加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadAccount"
      />

      <!-- 内容 -->
      <template v-else>
        <!-- 签到状态卡 -->
        <section class="checkin-card" role="region" aria-label="签到状态">
          <div class="checkin-label">
            <van-icon name="calendar-o" size="14" />
            已连续签到
          </div>
          <div class="checkin-streak" aria-label="连续签到天数">
            {{ account.checkInStreakDays }}
            <small>天</small>
          </div>
          <div class="checkin-sub">{{ statusSub }}</div>
          <div class="checkin-balance">
            当前积分 <b>{{ formatPoints(account.balance) }}</b>
          </div>
        </section>

        <!-- 连续签到奖励进度 -->
        <section class="card">
          <div class="card-title">
            <van-icon name="star-o" size="16" class="title-star" />
            连续签到奖励
            <span class="card-sub">连签 7 天额外 +{{ BONUS_REWARD }}</span>
          </div>
          <div class="reward-row" role="list" aria-label="连签奖励进度">
            <div
              v-for="item in rewardDays"
              :key="item.day"
              class="reward-day"
              :class="{ 'reward-day--done': item.done, 'reward-day--current': item.current, 'reward-day--bonus': item.bonus }"
              role="listitem"
              :aria-label="`第 ${item.day} 天奖励 ${item.points} 积分${item.done ? '，已签到' : ''}`"
            >
              <span class="reward-num">
                {{ item.day }}
                <span v-if="item.done" class="reward-check">
                  <van-icon name="success" size="12" />
                </span>
              </span>
              <span class="reward-pts">+{{ item.points }}</span>
            </div>
          </div>
          <div class="reward-line" role="progressbar" :aria-valuenow="signedInCycle" aria-valuemin="0" :aria-valuemax="CYCLE_LENGTH">
            <div class="reward-fill" :style="{ width: rewardFillWidth }" />
          </div>
        </section>

        <!-- 本月日历 -->
        <section class="card">
          <div class="cal-head">
            <div class="cal-month">{{ monthLabel }}</div>
            <div class="cal-nav">
              <button class="cal-btn" type="button" aria-label="上一个月" @click="prevMonth">
                <van-icon name="arrow-left" size="16" />
              </button>
              <button
                class="cal-btn"
                :class="{ disabled: atCurrentMonth }"
                type="button"
                aria-label="下一个月"
                :disabled="atCurrentMonth"
                @click="nextMonth"
              >
                <van-icon name="arrow" size="16" />
              </button>
            </div>
          </div>
          <div class="cal-week" aria-hidden="true">
            <span v-for="w in WEEK_LABELS" :key="w">{{ w }}</span>
          </div>
          <div class="cal-grid" role="grid" :aria-label="`${monthLabel}签到日历`">
            <template v-for="(cell, index) in calendarCells" :key="index">
              <span v-if="cell === null" class="cal-cell cal-cell--empty" />
              <button
                v-else
                class="cal-cell"
                :class="{
                  'cal-cell--checked': cell.signed && !cell.isToday,
                  'cal-cell--today': cell.isToday,
                  'cal-cell--future': cell.future,
                }"
                type="button"
                :aria-label="`${viewMonth + 1} 月 ${cell.day} 日${cell.signed ? '，已签到' : ''}`"
                @click="onCellClick(cell)"
              >
                {{ cell.day }}
                <span v-if="cell.signed" class="cal-dot" />
              </button>
            </template>
          </div>
          <div class="cal-foot">
            <span class="cal-legend"><i class="dot dot-signed" />已签到</span>
            <span class="cal-legend"><i class="dot dot-today" />今日</span>
            <span class="cal-legend"><i class="dot dot-none" />未签到</span>
          </div>
        </section>

        <!-- 签到规则 -->
        <section class="rules" role="list" aria-label="签到规则">
          <div class="rules-title">签到规则</div>
          <div class="rules-item" role="listitem">每日签到获得 {{ BASE_REWARD }} 积分，积分即时到账</div>
          <div class="rules-item" role="listitem">连续签到 7 天，第 7 天额外奖励 {{ BONUS_REWARD }} 积分</div>
          <div class="rules-item" role="listitem">签到中断后连续天数重新计算，已获积分不受影响</div>
          <div class="rules-item" role="listitem">每日仅可签到一次，当日 24:00 后视为次日签到</div>
        </section>
      </template>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button
        class="btn-checkin"
        :class="{ done: account?.checkedInToday }"
        type="button"
        :disabled="account?.checkedInToday || checkingIn || loading || loadError"
        :aria-label="account?.checkedInToday ? '今日已签到' : `今日签到获得 ${todayReward} 积分`"
        @click="doCheckIn"
      >
        <van-icon v-if="account?.checkedInToday" name="checked" size="18" />
        <van-icon v-else name="calendar-o" size="18" />
        {{ checkingIn
          ? '签到中...'
          : account?.checkedInToday
            ? todayEarned > 0
              ? `已签到 +${todayEarned}`
              : '已签到'
            : `今日签到 +${todayReward}` }}
      </button>
    </footer>

    <!-- 签到成功浮层 -->
    <van-popup
      v-model:show="showSuccess"
      position="center"
      round
      role="dialog"
      aria-label="签到成功"
    >
      <div class="success-popup">
        <button class="popup-close" type="button" aria-label="关闭" @click="showSuccess = false">
          <van-icon name="cross" size="18" />
        </button>
        <svg class="popup-star" width="64" height="64" viewBox="0 0 64 64" fill="none">
          <circle cx="32" cy="32" r="30" fill="#FFF7E6" />
          <path d="M32 8l6.5 13.5L53 24l-10.5 10 2.5 15L32 41l-13 8 2.5-15L11 24l14.5-2.5L32 8z" fill="#FAAD14" stroke="#D48806" stroke-width="1.4" stroke-linejoin="round" />
          <path d="M24 32l6 6 10-12" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        <div class="popup-title">签到成功</div>
        <div class="popup-pts">
          <small>+</small>{{ successInfo.earned }}<small> 积分</small>
        </div>
        <div class="popup-desc">
          {{
            successInfo.bonus
              ? `连续签到第 ${successInfo.streak} 天，额外奖励 ${BONUS_REWARD} 积分已到账`
              : `连续签到第 ${successInfo.streak} 天`
          }}
        </div>
        <button class="popup-btn" type="button" @click="showSuccess = false">开心收下</button>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.checkin-page {
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

.sk-card {
  height: 160px;
  border-radius: var(--r-lg);
}

.sk-block {
  height: 110px;
  margin-top: var(--s3);
}

.sk-calendar {
  height: 260px;
  margin-top: var(--s3);
}

/* 签到状态卡 */
.checkin-card {
  background: linear-gradient(135deg, #1677FF 0%, #0958D9 100%);
  color: #fff;
  border-radius: var(--r-lg);
  padding: var(--s6) var(--s4);
  position: relative;
  overflow: hidden;
  box-shadow: 0 6px 16px rgba(9, 88, 217, 0.28);
}

.checkin-card::before {
  content: "";
  position: absolute;
  right: -40px;
  top: -40px;
  width: 160px;
  height: 160px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.18) 0%, rgba(255, 255, 255, 0) 70%);
}

.checkin-label {
  font-size: var(--fs-sm);
  opacity: 0.9;
  display: flex;
  align-items: center;
  gap: var(--s1);
  position: relative;
  z-index: 1;
}

.checkin-streak {
  font-size: var(--fs-3xl);
  font-weight: var(--fw-semibold);
  margin-top: var(--s1);
  line-height: 1.1;
  position: relative;
  z-index: 1;
}

.checkin-streak small {
  font-size: var(--fs-base);
  font-weight: var(--fw-normal);
  opacity: 0.9;
}

.checkin-sub {
  font-size: var(--fs-sm);
  opacity: 0.85;
  margin-top: var(--s1);
  position: relative;
  z-index: 1;
}

.checkin-balance {
  margin-top: var(--s3);
  font-size: var(--fs-sm);
  opacity: 0.9;
  display: inline-flex;
  align-items: center;
  gap: var(--s1);
  background: rgba(255, 255, 255, 0.2);
  border-radius: 999px;
  padding: 4px 12px;
  position: relative;
  z-index: 1;
}

.checkin-balance b {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
}

/* 通用卡片 */
.card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4);
  margin-top: var(--s3);
}

.card-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.title-star {
  color: var(--c-warning);
}

.card-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-left: auto;
  font-weight: var(--fw-normal);
}

/* 连签奖励进度 */
.reward-row {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: var(--s1);
  margin-top: var(--s3);
}

.reward-day {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--s1);
}

.reward-num {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  background: var(--n3);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--n7);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  position: relative;
}

.reward-pts {
  font-size: 10px;
  color: var(--n7);
}

.reward-day--done .reward-num {
  background: var(--c-primary);
  color: #fff;
}

.reward-day--done .reward-pts {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.reward-day--current .reward-num {
  background: var(--c-success);
  color: #fff;
  box-shadow: 0 0 0 3px rgba(82, 196, 26, 0.2);
}

.reward-day--current .reward-pts {
  color: var(--c-success);
  font-weight: var(--fw-semibold);
}

.reward-day--bonus .reward-num {
  background: linear-gradient(135deg, #FAAD14, #D48806);
  color: #fff;
}

.reward-day--bonus .reward-pts {
  color: #D48806;
  font-weight: var(--fw-semibold);
}

.reward-check {
  position: absolute;
  bottom: -2px;
  right: -2px;
  background: #fff;
  border-radius: 50%;
  display: flex;
  color: var(--c-primary);
}

.reward-line {
  height: 4px;
  background: var(--n3);
  border-radius: 2px;
  margin-top: var(--s3);
  overflow: hidden;
}

.reward-fill {
  height: 100%;
  background: var(--c-primary);
  border-radius: 2px;
  transition: width var(--d-mid) var(--ease-std);
}

/* 日历 */
.cal-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.cal-month {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
}

.cal-nav {
  display: flex;
  gap: var(--s2);
}

.cal-btn {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--n7);
  background: var(--n3);
  border-radius: var(--r-base);
  cursor: pointer;
}

.cal-btn.disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.cal-week {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  text-align: center;
  font-size: var(--fs-sm);
  color: var(--n7);
  margin: var(--s3) 0 var(--s2);
}

.cal-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 2px;
}

.cal-cell {
  aspect-ratio: 1 / 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-base);
  color: var(--n10);
  position: relative;
  border-radius: var(--r-base);
  background: none;
  border: none;
  font-family: inherit;
  cursor: default;
  padding: 0;
}

.cal-cell--empty {
  visibility: hidden;
}

.cal-cell--checked {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.cal-cell--checked .cal-dot,
.cal-cell--today .cal-dot {
  position: absolute;
  bottom: 5px;
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--c-primary);
}

.cal-cell--today {
  background: var(--c-primary);
  color: #fff;
  border-radius: var(--r-base);
  font-weight: var(--fw-semibold);
  cursor: pointer;
}

.cal-cell--today .cal-dot {
  background: #fff;
}

.cal-cell--future {
  color: var(--n5);
}

.cal-foot {
  margin-top: var(--s3);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s6);
  font-size: var(--fs-sm);
  color: var(--n7);
}

.cal-legend {
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.cal-legend .dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.dot-signed {
  background: var(--c-primary);
}

.dot-today {
  background: var(--c-primary);
  box-shadow: 0 0 0 2px rgba(22, 119, 255, 0.25);
}

.dot-none {
  background: var(--n5);
}

/* 签到规则 */
.rules {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4);
  margin-top: var(--s3);
}

.rules-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  margin-bottom: var(--s2);
}

.rules-item {
  display: flex;
  gap: var(--s2);
  font-size: var(--fs-sm);
  color: var(--n9);
  padding: 5px 0;
}

.rules-item::before {
  content: "";
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--c-primary);
  margin-top: 8px;
  flex-shrink: 0;
}

/* 底部固定操作栏 */
.action-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
}

.btn-checkin {
  width: 100%;
  border: none;
  border-radius: 999px;
  padding: 13px 0;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  background: linear-gradient(135deg, #1677FF, #0958D9);
  color: #fff;
  box-shadow: 0 4px 10px rgba(22, 119, 255, 0.3);
}

.btn-checkin:disabled {
  cursor: not-allowed;
}

.btn-checkin.done {
  background: var(--n3);
  color: var(--n7);
  box-shadow: none;
}

/* 签到成功浮层 */
.success-popup {
  width: 300px;
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  text-align: center;
  position: relative;
  padding: var(--s6) var(--s4) var(--s4);
}

.popup-close {
  position: absolute;
  top: var(--s2);
  right: var(--s2);
  color: var(--n7);
  background: var(--n3);
  border-radius: 50%;
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  border: none;
}

.popup-star {
  margin: 0 auto var(--s2);
}

.popup-title {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.popup-pts {
  color: #D48806;
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  margin: var(--s1) 0;
}

.popup-pts small {
  font-size: var(--fs-base);
}

.popup-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-bottom: var(--s3);
}

.popup-btn {
  background: linear-gradient(135deg, #FAAD14, #D48806);
  color: #fff;
  border: none;
  border-radius: 999px;
  padding: 10px 0;
  width: 100%;
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
}
</style>
