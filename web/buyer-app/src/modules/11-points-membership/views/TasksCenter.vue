<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { pointsApi } from '@/modules/11-points-membership/api/points.api'
import type { PointsAccountDto, PointsTaskDto } from '../types/points.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatRelativeTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 任务中心页（/points/tasks）
 *
 * 页面结构（对齐设计稿 tasks-center）：
 * NavBar（返回 / 任务中心）
 * → 头部奖励横幅（金色渐变：今日还可获得积分 + 查看积分入口）
 * → 任务类型 Tab（每日任务 / 新手任务 / 成长任务，按任务动作语义分组，前端过滤）
 * → 滚动主体：任务卡片（图标 + 名称 + 状态标签 + 描述 + 积分奖励 + 完成时间 + 状态按钮）
 * → 底部固定操作栏（查看我的积分，含安全区适配）
 *
 * 任务状态机（按 PointsTaskDto.status 派生）：
 * - Completed → 已完成（按钮「已领取」置灰）
 * - Pending + 签到任务且今日已签到 → 待领取（按钮「领取」→ POST /points/tasks/{id}/complete 领取积分）
 * - Pending → 进行中（按钮「去完成」→ 按任务动作跳转对应页面）
 *
 * 数据流：并行 GET /points/tasks + GET /points/account（签到态用于派生「待领取」）。
 */

const router = useRouter()

/** 任务动作语义分组 */
type TaskGroupKey = 'Daily' | 'Newbie' | 'Growth'

interface TaskGroup {
  key: TaskGroupKey
  label: string
  sub: string
  actions: PointsTaskDto['action'][]
}

const TASK_GROUPS: TaskGroup[] = [
  { key: 'Daily', label: '每日任务', sub: '每日 00:00 重置', actions: ['CheckIn', 'Browse', 'Search', 'Share'] },
  { key: 'Newbie', label: '新手任务', sub: '一次性奖励', actions: ['Profile', 'Order'] },
  { key: 'Growth', label: '成长任务', sub: '每月 1 日重置', actions: ['Review'] },
]

/** 任务动作 → 去完成跳转路径 */
const ACTION_ROUTES: Record<PointsTaskDto['action'], string> = {
  CheckIn: '/points/check-in',
  Browse: '/',
  Search: '/search',
  Share: '/',
  Review: '/orders',
  Order: '/',
  Profile: '/profile',
}

/** 任务分组图标底色 */
const GROUP_ICON_CLS: Record<TaskGroupKey, string> = {
  Daily: 'ti-gold',
  Newbie: 'ti-green',
  Growth: 'ti-purple',
}

// ---- 状态 ----
const activeGroup = ref<TaskGroupKey>('Daily')
const firstLoading = ref(true)
const loadError = ref(false)
const tasks = ref<PointsTaskDto[]>([])
const account = ref<PointsAccountDto | null>(null)
const refreshing = ref(false)
/** 正在领取奖励的任务 ID（防重复提交） */
const claimingId = ref('')

// ---- 派生态 ----
/** 任务所属分组（未知动作归入成长任务） */
function groupOf(task: PointsTaskDto): TaskGroup {
  return TASK_GROUPS.find((g) => g.actions.includes(task.action)) ?? TASK_GROUPS[2]
}

/** 当前分组任务列表 */
const groupTasks = computed(() => tasks.value.filter((t) => groupOf(t).key === activeGroup.value))

/** 今日（每日任务）还可获得的积分 */
const todayEarnable = computed(() =>
  tasks.value
    .filter((t) => groupOf(t).key === 'Daily' && t.status === 'Pending')
    .reduce((sum, t) => sum + t.points, 0),
)

/** 任务是否可领取（签到任务且今日已签到） */
function isClaimable(task: PointsTaskDto): boolean {
  return task.status === 'Pending' && task.action === 'CheckIn' && account.value?.checkedInToday === true
}

/** 任务展示状态：done 已完成 / claim 待领取 / doing 进行中 */
function statusOf(task: PointsTaskDto): 'done' | 'claim' | 'doing' {
  if (task.status === 'Completed') return 'done'
  return isClaimable(task) ? 'claim' : 'doing'
}

/** 状态标签文案 */
const STATUS_LABEL: Record<'done' | 'claim' | 'doing', string> = {
  done: '已完成',
  claim: '待领取',
  doing: '进行中',
}

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  firstLoading.value = true
  loadError.value = false
  try {
    const [list, acc] = await Promise.all([pointsApi.listTasks(), pointsApi.getAccount()])
    tasks.value = list
    account.value = acc
  } catch (e) {
    logger.error('任务中心加载失败', e)
    loadError.value = true
  } finally {
    firstLoading.value = false
    refreshing.value = false
  }
}

onMounted(() => {
  void loadAll()
})

/** 下拉刷新（完成任务返回本页后刷新进度） */
async function onRefresh(): Promise<void> {
  await loadAll()
}

/** 切换任务分组 Tab */
function setGroup(key: TaskGroupKey): void {
  if (activeGroup.value === key) return
  activeGroup.value = key
}

// ---- 任务操作 ----
/** 领取任务奖励（待领取状态） */
async function claim(task: PointsTaskDto): Promise<void> {
  if (claimingId.value) return
  claimingId.value = task.id
  try {
    const updated = await pointsApi.completeTask(task.id)
    const index = tasks.value.findIndex((t) => t.id === task.id)
    if (index >= 0) tasks.value.splice(index, 1, updated)
    showToast(`领取成功，获得 ${updated.points} 积分`)
  } catch (e) {
    logger.warn('任务奖励领取失败', e)
    showFailToast(e instanceof Error ? e.message : '领取失败，请稍后重试')
  } finally {
    claimingId.value = ''
  }
}

/** 去完成（跳转任务动作对应页面） */
function goTask(task: PointsTaskDto): void {
  router.push(ACTION_ROUTES[task.action])
}

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goAccount(): void {
  router.push('/points/account')
}

function goHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="tasks-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">任务中心</div>
    </header>

    <!-- 头部奖励横幅 -->
    <section class="head-banner" role="region" aria-label="任务奖励">
      <template v-if="!firstLoading && !loadError">
        <div class="banner-text">
          今日还可获得 <b>+{{ todayEarnable }}</b> 积分
        </div>
        <button class="banner-btn" type="button" @click="goAccount">查看积分</button>
      </template>
      <div v-else class="skeleton-block sk-banner" />
    </section>

    <!-- 任务类型 Tab -->
    <nav class="tabs" role="tablist" aria-label="任务类型">
      <div
        v-for="group in TASK_GROUPS"
        :key="group.key"
        class="tab"
        :class="{ active: activeGroup === group.key }"
        role="tab"
        :aria-selected="activeGroup === group.key"
        @click="setGroup(group.key)"
      >
        {{ group.label }}
      </div>
    </nav>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="skeleton-wrap">
        <div v-for="i in 4" :key="i" class="skeleton-block sk-task" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="任务列表加载失败"
        description="网络异常，请稍后重试"
        @retry="loadAll"
      />

      <template v-else>
        <van-pull-refresh v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
          <!-- 空态 -->
          <EmptyState
            v-if="groupTasks.length === 0"
            title="暂无任务"
            action-text="去逛逛"
            @action="goHome"
          />

          <!-- 分组标签 + 任务列表 -->
          <template v-else>
            <div class="section-label">
              <span class="section-dot" :class="`dot-${activeGroup}`" />
              {{ TASK_GROUPS.find((g) => g.key === activeGroup)?.label }}
              <span class="section-sub">{{ TASK_GROUPS.find((g) => g.key === activeGroup)?.sub }}</span>
            </div>

            <article
              v-for="task in groupTasks"
              :key="task.id"
              class="task-card"
              role="article"
              :aria-label="`任务 ${task.name}`"
            >
              <span class="task-ico" :class="GROUP_ICON_CLS[groupOf(task).key]">
                <van-icon :name="task.icon" size="24" />
              </span>
              <div class="task-main">
                <div class="task-name">
                  {{ task.name }}
                  <span class="task-tag" :class="`tag-${statusOf(task)}`">{{ STATUS_LABEL[statusOf(task)] }}</span>
                </div>
                <div class="task-desc">{{ task.description }}</div>
                <div class="task-reward">
                  <span class="task-points"><b>+{{ task.points }}</b> 积分</span>
                  <span v-if="task.status === 'Completed' && task.completedAt" class="task-done-at">
                    完成于 {{ formatRelativeTime(task.completedAt) }}
                  </span>
                </div>
              </div>
              <!-- 状态按钮 -->
              <button
                v-if="statusOf(task) === 'done'"
                class="task-btn btn-done"
                type="button"
                disabled
              >
                已领取
              </button>
              <button
                v-else-if="statusOf(task) === 'claim'"
                class="task-btn btn-claim"
                type="button"
                :disabled="claimingId === task.id"
                :aria-label="`领取 ${task.name} 奖励 ${task.points} 积分`"
                @click="claim(task)"
              >
                {{ claimingId === task.id ? '领取中...' : '领取' }}
              </button>
              <button
                v-else
                class="task-btn btn-go"
                type="button"
                :aria-label="`去完成 ${task.name}`"
                @click="goTask(task)"
              >
                去完成
              </button>
            </article>
          </template>
        </van-pull-refresh>
      </template>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button class="btn-primary" type="button" @click="goAccount">查看我的积分</button>
    </footer>
  </div>
</template>

<style scoped>
.tasks-page {
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

/* 头部奖励横幅 */
.head-banner {
  background: linear-gradient(135deg, #FAAD14 0%, #D48806 100%);
  color: #fff;
  padding: var(--s4);
  display: flex;
  align-items: center;
  justify-content: space-between;
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
}

.head-banner::after {
  content: "";
  position: absolute;
  right: -20px;
  top: -30px;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.18) 0%, rgba(255, 255, 255, 0) 70%);
}

.banner-text {
  font-size: var(--fs-sm);
  position: relative;
  z-index: 1;
}

.banner-text b {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
}

.banner-btn {
  background: rgba(255, 255, 255, 0.25);
  border: 1px solid rgba(255, 255, 255, 0.5);
  color: #fff;
  border-radius: 999px;
  padding: 5px 12px;
  font-size: var(--fs-sm);
  font-family: inherit;
  cursor: pointer;
  position: relative;
  z-index: 1;
}

.sk-banner {
  height: 20px;
  width: 60%;
  background: rgba(255, 255, 255, 0.3);
}

/* 任务类型 Tab */
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

.sk-task {
  height: 76px;
  border-radius: var(--r-lg);
}

/* 分组标签 */
.section-label {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin: 0 var(--s1) var(--s3);
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
}

.section-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.dot-daily {
  background: var(--c-primary);
}

.dot-newbie {
  background: var(--c-success);
}

.dot-growth {
  background: var(--c-buyer);
}

.section-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  font-weight: var(--fw-normal);
  margin-left: auto;
}

/* 任务卡片 */
.task-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  margin-bottom: var(--s2);
  display: flex;
  align-items: center;
  gap: var(--s3);
}

.task-ico {
  width: 44px;
  height: 44px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.ti-gold {
  background: #FFF7E6;
  color: var(--c-warning);
}

.ti-green {
  background: #F0FBEB;
  color: var(--c-success);
}

.ti-purple {
  background: #F3E8FF;
  color: var(--c-buyer);
}

.task-main {
  flex: 1;
  min-width: 0;
}

.task-name {
  font-size: 15px;
  font-weight: var(--fw-medium);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.task-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 3px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-reward {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-top: var(--s2);
}

.task-points {
  color: #D48806;
  font-size: var(--fs-sm);
  font-weight: var(--fw-semibold);
}

.task-points b {
  font-size: var(--fs-base);
}

.task-done-at {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 状态标签 */
.task-tag {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: var(--r-base);
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

.tag-doing {
  background: #FFF7E6;
  color: var(--c-warning);
}

.tag-claim {
  background: #E6F0FF;
  color: var(--c-primary);
}

.tag-done {
  background: #F0FBEB;
  color: var(--c-success);
}

/* 状态按钮 */
.task-btn {
  border: none;
  border-radius: 999px;
  padding: 7px 14px;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  flex-shrink: 0;
}

.btn-go {
  background: #FFF7E6;
  color: var(--c-warning);
  border: 1px solid #FFE7BA;
}

.btn-claim {
  background: var(--c-primary);
  color: #fff;
}

.btn-claim:disabled {
  opacity: 0.7;
}

.btn-done {
  background: var(--n3);
  color: var(--n7);
  cursor: not-allowed;
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
