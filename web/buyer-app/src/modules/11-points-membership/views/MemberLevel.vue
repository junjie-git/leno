<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { memberApi } from '@/modules/11-points-membership/api/member.api'
import type { MemberLevelInfoDto, MemberProfileDto } from '../types/member.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatNumber, formatPoints } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 会员等级页（/member/level）
 *
 * 页面结构（对齐设计稿 member-level）：
 * NavBar（返回 / 会员等级）→ 滚动主体：
 *   当前等级卡（金色渐变：等级盾徽 + 等级名 + 成长值 / 下一等级门槛 + 进度条）
 *   → 成长值进度卡（进度百分比 + 距下一等级还需成长值提示，最高等级满格）
 *   → 当前等级权益列表（benefits 逐项展示）
 *   → 等级权益对比表（全部等级 × 权益矩阵，当前等级列高亮，横向滚动）
 *   → 升级攻略 / 等级规则说明
 * → 底部固定操作栏（去消费升级，含安全区适配）
 *
 * 数据流：并行 GET /members/me + GET /members/levels；
 * 权益对比矩阵由各等级 benefits 取并集（按首次出现顺序）动态生成。
 */

const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const refreshing = ref(false)
const profile = ref<MemberProfileDto | null>(null)
const levels = ref<MemberLevelInfoDto[]>([])

// ---- 派生态 ----
/** 是否已达最高等级（无下一等级门槛） */
const isMaxLevel = computed(() => !profile.value?.nextLevelPoints)

/** 成长值进度百分比（当前成长值 / 下一等级门槛，0-100） */
const progressPercent = computed(() => {
  const p = profile.value
  if (!p) return 0
  if (isMaxLevel.value || !p.nextLevelPoints) return 100
  return Math.min(100, Math.max(0, Math.round((p.points / p.nextLevelPoints) * 100)))
})

/** 距下一等级还需成长值 */
const growthGap = computed(() => {
  const p = profile.value
  if (!p || isMaxLevel.value || !p.nextLevelPoints) return 0
  return Math.max(0, p.nextLevelPoints - p.points)
})

/** 等级对比表列（按等级升序） */
const levelColumns = computed(() => [...levels.value].sort((a, b) => a.level - b.level))

/** 权益对比矩阵行（全等级 benefits 并集，按首次出现顺序） */
interface BenefitRow {
  name: string
  /** 各等级（levelColumns 顺序）是否享有该权益 */
  included: boolean[]
}

const benefitRows = computed<BenefitRow[]>(() => {
  const names: string[] = []
  for (const level of levelColumns.value) {
    for (const benefit of level.benefits) {
      if (!names.includes(benefit)) names.push(benefit)
    }
  }
  return names.map((name) => ({
    name,
    included: levelColumns.value.map((level) => level.benefits.includes(name)),
  }))
})

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    const [me, list] = await Promise.all([memberApi.getMyMembership(), memberApi.listLevels()])
    profile.value = me
    levels.value = list
  } catch (e) {
    logger.error('会员等级页加载失败', e)
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

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

/** 去消费升级（1 元消费 = 1 成长值） */
function goUpgrade(): void {
  router.push('/')
}
</script>

<template>
  <div class="level-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">会员等级</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeleton-wrap">
        <div class="skeleton-block sk-card" />
        <div class="skeleton-block sk-block" />
        <div class="skeleton-block sk-table" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError || !profile"
        title="会员信息加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadAll"
      />

      <!-- 内容 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <!-- 当前等级卡 -->
        <section class="level-card" role="region" aria-label="当前会员等级">
          <div class="level-top">
            <span class="level-badge">
              <svg width="56" height="56" viewBox="0 0 56 56" fill="none">
                <path d="M28 4l18 9v15c0 11-7.5 18.5-18 24C17.5 46.5 10 39 10 28V13l18-9z" fill="rgba(255,255,255,.22)" stroke="#fff" stroke-width="1.6" stroke-linejoin="round" />
                <path d="M28 14l10 5v8c0 6-4 10-10 13-6-3-10-7-10-13v-8l10-5z" fill="#FFD700" stroke="#fff" stroke-width="1.4" stroke-linejoin="round" />
                <path d="M22 28l4 4 8-9" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
                <text x="28" y="50" font-size="8" font-weight="700" fill="#fff" text-anchor="middle">V{{ profile.level }}</text>
              </svg>
            </span>
            <div>
              <div class="level-name">{{ profile.levelName }}</div>
              <div class="level-tag">
                <van-icon name="star" size="12" />
                V{{ profile.level }} 等级
              </div>
            </div>
          </div>
          <div class="growth-row">
            <div class="growth">
              成长值 <b>{{ formatPoints(profile.points) }}</b>
              <template v-if="!isMaxLevel && profile.nextLevelPoints">
                / {{ formatPoints(profile.nextLevelPoints) }}
              </template>
            </div>
            <div v-if="!isMaxLevel && profile.nextLevelName" class="next">
              下一等级<br>{{ profile.nextLevelName }}
            </div>
            <div v-else class="next">已达<br>最高等级</div>
          </div>
          <div class="level-progress">
            <div class="level-progress-bar" :style="{ width: `${progressPercent}%` }" />
          </div>
        </section>

        <!-- 成长值进度卡 -->
        <section class="progress-card">
          <div class="progress-row">
            <div class="progress-pct" role="progressbar" :aria-valuenow="progressPercent" aria-valuemin="0" aria-valuemax="100">
              {{ progressPercent }}%<small>{{ isMaxLevel ? '已达最高等级' : `距${profile.nextLevelName ?? '下一等级'}` }}</small>
            </div>
            <div class="progress-nums">
              {{ formatPoints(profile.points) }}
              <template v-if="!isMaxLevel && profile.nextLevelPoints">
                / {{ formatPoints(profile.nextLevelPoints) }}
              </template>
            </div>
          </div>
          <div class="progress-track">
            <div class="progress-fill" :style="{ width: `${progressPercent}%` }" />
          </div>
          <div class="progress-tip">
            <van-icon name="info-o" size="14" />
            <template v-if="isMaxLevel">
              已达最高等级，感谢您的一路陪伴
            </template>
            <template v-else>
              距{{ profile.nextLevelName ?? '下一等级' }}还需 <b>{{ formatNumber(growthGap) }}</b> 成长值（约消费 ¥{{ formatNumber(growthGap) }}）
            </template>
          </div>
        </section>

        <!-- 当前等级权益 -->
        <section class="section">
          <div class="section-title">
            <van-icon name="gem-o" size="16" class="title-gold" />
            {{ profile.levelName }}专享权益
            <span class="section-sub">共 {{ profile.benefits.length }} 项</span>
          </div>
          <div class="benefit-list">
            <div
              v-for="benefit in profile.benefits"
              :key="benefit"
              class="benefit-item"
              role="listitem"
            >
              <span class="benefit-ico">
                <van-icon name="checked" size="20" />
              </span>
              <span class="benefit-name">{{ benefit }}</span>
            </div>
          </div>
        </section>

        <!-- 等级权益对比表 -->
        <section class="section">
          <div class="section-title">
            <van-icon name="bars" size="16" class="title-blue" />
            等级权益对比
            <span class="section-sub">当前 V{{ profile.level }}</span>
          </div>
          <div class="table-wrap">
            <table class="tbl" role="table" aria-label="等级权益对比表">
              <thead>
                <tr>
                  <th class="col-benefit">权益</th>
                  <th
                    v-for="level in levelColumns"
                    :key="level.level"
                    :class="{ 'col-cur': level.level === profile.level }"
                  >
                    V{{ level.level }}<br>{{ level.name.replace('会员', '') }}
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in benefitRows" :key="row.name">
                  <td class="col-benefit">{{ row.name }}</td>
                  <td
                    v-for="(included, index) in row.included"
                    :key="index"
                    :class="{ 'col-cur': levelColumns[index]?.level === profile.level, yes: included, no: !included }"
                  >
                    <van-icon v-if="included" name="success" size="14" :aria-label="`V${levelColumns[index]?.level} 已享 ${row.name}`" />
                    <van-icon v-else name="cross" size="14" :aria-label="`V${levelColumns[index]?.level} 未享 ${row.name}`" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- 升级攻略 -->
        <section class="section">
          <div class="section-title">
            <van-icon name="star-o" size="16" class="title-blue" />
            升级攻略
          </div>
          <div class="guide-list" role="list">
            <div class="guide-item" role="listitem"><span class="guide-dot" />每消费 <b>1 元</b> 获得 1 成长值（退款扣减）</div>
            <div class="guide-item" role="listitem"><span class="guide-dot" />每日签到获得 <b>+5</b> 成长值</div>
            <div class="guide-item" role="listitem"><span class="guide-dot" />完成任务中心任务获得对应成长值</div>
            <div class="guide-item" role="listitem"><span class="guide-dot" />成长值按近 12 个月累计计算，达阈值自动升级</div>
            <div class="guide-item" role="listitem"><span class="guide-dot" />近 12 个月成长值不足将触发等级降级</div>
          </div>
        </section>

        <!-- 等级规则说明 -->
        <section class="section">
          <div class="section-title">
            <van-icon name="info-o" size="16" class="title-blue" />
            等级规则说明
          </div>
          <div class="guide-list" role="list">
            <div class="guide-item" role="listitem"><span class="guide-dot" />会员等级由近 12 个月累计成长值决定，达到门槛自动升级</div>
            <div class="guide-item" role="listitem"><span class="guide-dot" />等级权益自升级当日起生效，降级次日生效</div>
            <div class="guide-item" role="listitem"><span class="guide-dot" />成长值与积分相互独立，等级变动不影响积分余额</div>
          </div>
        </section>
      </van-pull-refresh>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button
        class="btn-primary"
        type="button"
        :aria-label="isMaxLevel ? '去逛逛' : '去消费升级'"
        @click="goUpgrade"
      >
        {{ isMaxLevel ? '去逛逛' : '去消费升级' }}
      </button>
    </footer>
  </div>
</template>

<style scoped>
.level-page {
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
  height: 170px;
  border-radius: var(--r-lg);
}

.sk-block {
  height: 110px;
  margin-top: var(--s3);
}

.sk-table {
  height: 220px;
  margin-top: var(--s3);
}

/* 当前等级卡 */
.level-card {
  background: linear-gradient(135deg, #FFD700 0%, #D48806 100%);
  color: #fff;
  border-radius: var(--r-lg);
  padding: var(--s6) var(--s4) var(--s4);
  position: relative;
  overflow: hidden;
  box-shadow: 0 8px 20px rgba(212, 136, 6, 0.32);
}

.level-card::before {
  content: "";
  position: absolute;
  right: -50px;
  top: -50px;
  width: 180px;
  height: 180px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.22) 0%, rgba(255, 255, 255, 0) 70%);
}

.level-top {
  display: flex;
  align-items: center;
  gap: var(--s3);
  position: relative;
  z-index: 1;
}

.level-badge {
  width: 60px;
  height: 60px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.level-name {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  line-height: 1.2;
}

.level-tag {
  display: inline-flex;
  align-items: center;
  gap: var(--s1);
  background: rgba(255, 255, 255, 0.25);
  border-radius: 999px;
  padding: 3px 10px;
  font-size: var(--fs-sm);
  margin-top: var(--s1);
}

.growth-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-top: var(--s4);
  position: relative;
  z-index: 1;
}

.growth {
  font-size: var(--fs-base);
  opacity: 0.95;
}

.growth b {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
}

.next {
  font-size: var(--fs-sm);
  opacity: 0.9;
  text-align: right;
  line-height: 1.4;
}

.level-progress {
  height: 8px;
  background: rgba(255, 255, 255, 0.3);
  border-radius: 999px;
  overflow: hidden;
  margin-top: var(--s2);
  position: relative;
  z-index: 1;
}

.level-progress-bar {
  height: 100%;
  background: #fff;
  border-radius: 999px;
  transition: width var(--d-mid) var(--ease-std);
}

/* 成长值进度卡 */
.progress-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4);
  margin-top: var(--s3);
}

.progress-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.progress-pct {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--c-primary);
}

.progress-pct small {
  font-size: var(--fs-sm);
  color: var(--n7);
  font-weight: var(--fw-normal);
  margin-left: var(--s1);
}

.progress-nums {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.progress-track {
  height: 8px;
  background: var(--n3);
  border-radius: 999px;
  overflow: hidden;
  margin-top: var(--s2);
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #FFD700, #FAAD14);
  border-radius: 999px;
  transition: width var(--d-mid) var(--ease-std);
}

.progress-tip {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-top: var(--s2);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.progress-tip :deep(.van-icon) {
  color: var(--c-primary);
}

.progress-tip b {
  color: var(--c-primary);
  font-weight: var(--fw-semibold);
}

/* 通用区块 */
.section {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4);
  margin-top: var(--s3);
}

.section-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.title-gold {
  color: #D48806;
}

.title-blue {
  color: var(--c-primary);
}

.section-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-left: auto;
  font-weight: var(--fw-normal);
}

/* 当前等级权益 */
.benefit-list {
  margin-top: var(--s2);
}

.benefit-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s2) 0;
  border-bottom: 1px solid var(--n3);
}

.benefit-item:last-child {
  border-bottom: none;
}

.benefit-ico {
  width: 36px;
  height: 36px;
  border-radius: var(--r-card);
  background: #FFF7E6;
  color: #D48806;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.benefit-name {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
}

/* 等级权益对比表 */
.table-wrap {
  overflow-x: auto;
  margin-top: var(--s3);
  border-radius: var(--r-card);
  border: 1px solid var(--n3);
  -webkit-overflow-scrolling: touch;
}

.tbl {
  width: 100%;
  min-width: 340px;
  border-collapse: collapse;
  font-size: var(--fs-sm);
}

.tbl th,
.tbl td {
  padding: 9px 4px;
  text-align: center;
  border-bottom: 1px solid var(--n3);
  white-space: nowrap;
}

.tbl thead th {
  background: var(--n2);
  color: var(--n7);
  font-weight: var(--fw-medium);
  font-size: 10px;
  line-height: 1.4;
}

.tbl .col-benefit {
  text-align: left;
  padding-left: var(--s3);
  color: var(--n9);
  font-size: 11px;
}

.tbl thead .col-benefit {
  color: var(--n7);
}

.tbl .col-cur {
  background: #FFFBE6;
  color: #D48806;
  font-weight: var(--fw-semibold);
}

.tbl thead .col-cur {
  background: #FFF7E6;
}

.tbl .yes {
  color: var(--c-success);
}

.tbl .no {
  color: var(--n5);
}

.tbl tbody tr:last-child td {
  border-bottom: none;
}

/* 攻略与规则 */
.guide-list {
  margin-top: var(--s2);
}

.guide-item {
  display: flex;
  gap: var(--s2);
  font-size: var(--fs-sm);
  color: var(--n9);
  padding: 7px 0;
}

.guide-item b {
  color: var(--c-primary);
  font-weight: var(--fw-semibold);
}

.guide-dot {
  width: 5px;
  height: 5px;
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

.btn-primary {
  width: 100%;
  height: 40px;
  border: none;
  border-radius: 999px;
  background: linear-gradient(135deg, #FAAD14, #D48806);
  color: #fff;
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 10px rgba(212, 136, 6, 0.3);
}
</style>
