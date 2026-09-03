<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { memberApi } from '@/modules/11-points-membership/api/member.api'
import type { MemberProfileDto, MembershipPackageDto } from '../types/member.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate, formatPrice } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 会员套餐页（/member/packages）
 *
 * 页面结构（对齐设计稿 membership-packages）：
 * NavBar（返回 / 会员套餐）→ 滚动主体：
 *   当前会员状态条（未开通 / 生效中 / 即将到期，含剩余天数与续费入口）
 *   → 页头标题 → 套餐卡片（名称 / 时长 / 价格 / 原价 / 立省 / 权益清单 / 订阅按钮，
 *      带 tag 的套餐金色渐变高亮 + 角标）
 *   → 权益说明（使用规则 / 有效期 / 退订规则 / 自动续费）
 * → 底部固定操作栏（查看会员等级，含安全区适配）
 *
 * 数据流：并行 GET /membership-packages + GET /members/me；
 * 订阅：showConfirmDialog 二次确认 → POST /membership-packages/{id}/subscribe →
 * 契约直接返回开通结果（orderId + premiumExpireAt）→ 提示成功并刷新会员状态条。
 */

const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const refreshing = ref(false)
const packages = ref<MembershipPackageDto[]>([])
const profile = ref<MemberProfileDto | null>(null)
/** 正在提交订阅的套餐 ID（防重复提交） */
const subscribingId = ref('')

// ---- 派生态 ----
/** 付费会员剩余天数（未开通 / 已过期为 0） */
const premiumRemainingDays = computed(() => {
  const expireAt = profile.value?.premiumExpireAt
  if (!profile.value?.isPremium || !expireAt) return 0
  return Math.max(0, Math.ceil((new Date(expireAt).getTime() - Date.now()) / 86_400_000))
})

/** 是否即将到期（剩余不足 7 天） */
const expiringSoon = computed(
  () => (profile.value?.isPremium ?? false) && premiumRemainingDays.value > 0 && premiumRemainingDays.value <= 7,
)

/** 状态条展示信息 */
const statusBar = computed(() => {
  const p = profile.value
  if (!p) {
    return { title: '未开通付费会员', sub: '开通后尊享专享折扣、积分翻倍等权益', cls: 'sb-normal', btn: '去开通' }
  }
  if (!p.isPremium || premiumRemainingDays.value <= 0) {
    return { title: '未开通付费会员', sub: '开通后尊享专享折扣、积分翻倍等权益', cls: 'sb-normal', btn: '去开通' }
  }
  if (expiringSoon.value) {
    return {
      title: '即将到期',
      sub: `有效期至 ${formatDate(p.premiumExpireAt ?? '')}，剩余 ${premiumRemainingDays.value} 天`,
      cls: 'sb-danger',
      btn: '立即续费',
    }
  }
  return {
    title: '付费会员 · 生效中',
    sub: `有效期至 ${formatDate(p.premiumExpireAt ?? '')}，剩余 ${premiumRemainingDays.value} 天`,
    cls: 'sb-active',
    btn: '续费',
  }
})

/** 套餐时长单位文案 */
function periodLabel(durationDays: number): string {
  if (durationDays >= 365) return '/ 年'
  if (durationDays >= 90) return '/ 季'
  if (durationDays >= 30) return '/ 月'
  return `/${durationDays} 天`
}

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    const [list, me] = await Promise.all([memberApi.listPackages(), memberApi.getMyMembership()])
    packages.value = list
    profile.value = me
  } catch (e) {
    logger.error('会员套餐页加载失败', e)
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

// ---- 订阅 ----
/** 状态条按钮：滚动到套餐列表 */
function scrollToList(): void {
  document.getElementById('package-list')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

/** 订阅套餐（二次确认 → 提交 → 刷新会员状态） */
async function subscribe(pkg: MembershipPackageDto): Promise<void> {
  if (subscribingId.value) return
  try {
    await showConfirmDialog({
      title: '确认订阅',
      message: `确认订阅「${pkg.name}」，支付 ¥${formatPrice(pkg.price)}？`,
      confirmButtonText: '确认支付',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  subscribingId.value = pkg.id
  try {
    const result = await memberApi.subscribe(pkg.id)
    showToast(`订阅成功，会员权益已生效（有效期至 ${formatDate(result.premiumExpireAt)}）`)
    // 刷新会员状态条（订阅成功后立即生效）
    try {
      profile.value = await memberApi.getMyMembership()
    } catch (e) {
      logger.warn('订阅后会员状态刷新失败（忽略）', e)
    }
  } catch (e) {
    logger.warn('订阅会员套餐失败', e)
    showFailToast(e instanceof Error ? e.message : '订阅失败，请稍后重试')
  } finally {
    subscribingId.value = ''
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

function goLevel(): void {
  router.push('/member/level')
}

function goHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="packages-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">会员套餐</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeleton-wrap">
        <div class="skeleton-block sk-status" />
        <div v-for="i in 3" :key="i" class="skeleton-block sk-pkg" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="会员套餐加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadAll"
      />

      <!-- 内容 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <!-- 当前会员状态条 -->
        <div class="status-bar" :class="statusBar.cls" role="region" aria-label="当前付费会员状态">
          <span class="status-ico">
            <van-icon :name="expiringSoon ? 'warning-o' : 'gem-o'" size="22" />
          </span>
          <div class="status-main">
            <div class="status-title">{{ statusBar.title }}</div>
            <div class="status-sub">{{ statusBar.sub }}</div>
          </div>
          <button class="status-btn" type="button" @click="scrollToList">{{ statusBar.btn }}</button>
        </div>

        <!-- 空态 -->
        <EmptyState
          v-if="packages.length === 0"
          title="暂无可购买套餐"
          action-text="去逛逛"
          @action="goHome"
        />

        <!-- 套餐列表 -->
        <template v-else>
          <div class="page-title">选择适合你的会员套餐</div>
          <div class="page-subtitle">付费会员与免费会员等级权益叠加，开通立即生效</div>

          <div id="package-list" class="pkg-list">
            <article
              v-for="pkg in packages"
              :key="pkg.id"
              class="pkg"
              :class="pkg.tag ? 'pkg--hot' : 'pkg--normal'"
              role="article"
              :aria-label="`会员套餐 ${pkg.name}`"
            >
              <!-- 推荐角标 -->
              <span v-if="pkg.tag" class="pkg-rec">
                <van-icon name="fire-o" size="10" />
                {{ pkg.tag }}
              </span>

              <div class="pkg-head">
                <div class="pkg-name">{{ pkg.name }}</div>
                <div class="pkg-period">{{ pkg.durationDays }} 天</div>
              </div>

              <div class="pkg-price-row">
                <span class="pkg-price" :class="{ hot: !!pkg.tag }">
                  <i>¥</i>{{ formatPrice(pkg.price) }}
                </span>
                <span class="pkg-unit">{{ periodLabel(pkg.durationDays) }}</span>
                <span v-if="pkg.originalPrice > pkg.price" class="pkg-orig">
                  ¥{{ formatPrice(pkg.originalPrice) }}
                </span>
                <span v-if="pkg.originalPrice > pkg.price" class="pkg-save">
                  立省 ¥{{ formatPrice(pkg.originalPrice - pkg.price) }}
                </span>
              </div>

              <div class="pkg-benefits">
                <div v-for="benefit in pkg.benefits" :key="benefit" class="pkg-benefit">
                  <van-icon name="checked" size="16" />
                  {{ benefit }}
                </div>
              </div>

              <button
                class="pkg-btn"
                type="button"
                :disabled="subscribingId === pkg.id"
                :aria-label="`订阅 ${pkg.name} 支付 ${formatPrice(pkg.price)} 元`"
                @click="subscribe(pkg)"
              >
                {{ subscribingId === pkg.id ? '开通中...' : '立即开通' }}
              </button>
            </article>
          </div>
        </template>

        <!-- 权益说明 -->
        <section class="section">
          <div class="section-title">
            <van-icon name="info-o" size="16" class="title-blue" />
            权益说明
          </div>
          <div class="rule-item">
            <span class="rule-label">使用规则</span>
            <span>开通后立即生效，与免费会员等级权益叠加享受；专享折扣不与商品秒杀价叠加。</span>
          </div>
          <div class="rule-item">
            <span class="rule-label">有效期</span>
            <span>自开通之日起按套餐时长计算，到期自动失效，可在到期前续费延长。</span>
          </div>
          <div class="rule-item">
            <span class="rule-label">退订规则</span>
            <span>开通 7 天内未使用任何权益可申请全额退款；已使用权益不支持退款。</span>
          </div>
          <div class="rule-item">
            <span class="rule-label">自动续费</span>
            <span>可在「设置 - 支付与订阅」中关闭自动续费，关闭不影响当前有效期权益。</span>
          </div>
        </section>
      </van-pull-refresh>
    </main>

    <!-- 底部固定操作栏 -->
    <footer class="action-bar">
      <button class="btn-ghost" type="button" @click="goLevel">查看会员等级</button>
    </footer>
  </div>
</template>

<style scoped>
.packages-page {
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
  gap: var(--s3);
}

.sk-status {
  height: 64px;
  border-radius: var(--r-lg);
}

.sk-pkg {
  height: 190px;
  border-radius: var(--r-lg);
}

/* 当前会员状态条 */
.status-bar {
  display: flex;
  align-items: center;
  gap: var(--s3);
  border-radius: var(--r-lg);
  padding: var(--s3) var(--s4);
}

.sb-normal {
  background: var(--n1);
  box-shadow: var(--sh-card);
}

.sb-active {
  background: linear-gradient(135deg, #FFF7E6, #FFE7BA);
  border: 1px solid #FFD666;
}

.sb-danger {
  background: #FFF1F0;
  border: 1px solid #FFA39E;
}

.status-ico {
  width: 40px;
  height: 40px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  background: #FFF7E6;
  color: #D48806;
}

.sb-danger .status-ico {
  background: linear-gradient(135deg, #FF7875, #FF4D4F);
  color: #fff;
}

.status-main {
  flex: 1;
  min-width: 0;
}

.status-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.sb-danger .status-title {
  color: #CF1322;
}

.status-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.sb-danger .status-sub {
  color: #CF1322;
  opacity: 0.85;
}

.status-btn {
  background: #fff;
  color: #D48806;
  border: 1px solid #FAAD14;
  border-radius: 999px;
  padding: 6px 14px;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  flex-shrink: 0;
}

.sb-danger .status-btn {
  background: var(--c-error);
  color: #fff;
  border-color: var(--c-error);
}

/* 页头标题 */
.page-title {
  margin: var(--s4) var(--s1) var(--s2);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  text-align: center;
}

.page-subtitle {
  text-align: center;
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-bottom: var(--s4);
}

/* 套餐卡片 */
.pkg-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

.pkg {
  position: relative;
  border-radius: var(--r-lg);
  padding: var(--s4);
  overflow: hidden;
}

.pkg--normal {
  background: linear-gradient(135deg, #FFF7E6 0%, #FFE7BA 100%);
  color: #8C6A1F;
  border: 1px solid #FFE7BA;
}

.pkg--hot {
  background: linear-gradient(135deg, #FFD666 0%, #D48806 100%);
  color: #fff;
  box-shadow: 0 8px 20px rgba(212, 136, 6, 0.32);
}

.pkg-rec {
  position: absolute;
  top: 0;
  right: 0;
  background: var(--c-error);
  color: #fff;
  font-size: 10px;
  font-weight: var(--fw-semibold);
  padding: 3px 12px;
  border-bottom-left-radius: var(--r-base);
  display: flex;
  align-items: center;
  gap: 3px;
}

.pkg-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.pkg-name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
}

.pkg-period {
  font-size: var(--fs-sm);
  opacity: 0.85;
}

.pkg-price-row {
  display: flex;
  align-items: baseline;
  gap: var(--s2);
  margin-top: var(--s2);
  flex-wrap: wrap;
}

.pkg-price {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  color: #D48806;
  line-height: 1;
}

.pkg-price.hot {
  color: var(--c-error);
}

.pkg-price i {
  font-style: normal;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
}

.pkg-unit {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.pkg--hot .pkg-unit {
  color: rgba(255, 255, 255, 0.85);
}

.pkg-orig {
  font-size: var(--fs-sm);
  color: var(--n7);
  text-decoration: line-through;
}

.pkg--hot .pkg-orig {
  color: rgba(255, 255, 255, 0.7);
}

.pkg-save {
  display: inline-flex;
  align-items: center;
  background: rgba(255, 77, 79, 0.12);
  color: var(--c-error);
  border-radius: var(--r-base);
  padding: 2px 8px;
  font-size: 10px;
  font-weight: var(--fw-medium);
}

.pkg--hot .pkg-save {
  background: rgba(255, 255, 255, 0.28);
  color: #fff;
}

.pkg-benefits {
  margin-top: var(--s3);
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.pkg-benefit {
  display: flex;
  align-items: center;
  gap: var(--s2);
  font-size: var(--fs-sm);
}

.pkg--normal .pkg-benefit :deep(.van-icon) {
  color: var(--c-success);
}

.pkg--hot .pkg-benefit :deep(.van-icon) {
  color: #fff;
}

.pkg-btn {
  margin-top: var(--s3);
  width: 100%;
  border: none;
  border-radius: 999px;
  padding: 11px 0;
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
  background: #fff;
  color: #D48806;
}

.pkg--normal .pkg-btn {
  border: 1px solid #FAAD14;
}

.pkg--hot .pkg-btn {
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.12);
}

.pkg-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

/* 权益说明 */
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
  margin-bottom: var(--s2);
}

.title-blue {
  color: var(--c-primary);
}

.rule-item {
  display: flex;
  gap: var(--s2);
  padding: 7px 0;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.rule-label {
  color: var(--n7);
  min-width: 64px;
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

.btn-ghost {
  width: 100%;
  height: 40px;
  border: 1.5px solid var(--c-primary);
  border-radius: 999px;
  background: var(--n1);
  color: var(--c-primary);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
