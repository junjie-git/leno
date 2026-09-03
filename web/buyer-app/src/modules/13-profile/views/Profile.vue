<script setup lang="ts">
import { computed, onActivated, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { profileApi } from '@/modules/13-profile/api/profile.api'
import { favoriteApi } from '@/modules/13-profile/api/favorite.api'
import { historyApi } from '@/modules/13-profile/api/history.api'
import { useAuthStore } from '@/shared/auth'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatPoints, maskPhone } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 我的页（/profile，Tabbar「我的」页，KeepAlive 缓存名 Profile）
 *
 * 结构（对齐设计稿 profile）：
 * 蓝色渐变用户信息头部（设置/消息入口 + 头像 + 昵称 + 会员徽章 + 脱敏手机号）
 * → 资产卡片（积分 / 收藏 / 足迹）
 * → 我的订单（全部订单 + 待付款/待发货/待收货/待评价四状态图标，跳 /orders?tab=xxx）
 * → 常用功能宫格（收藏/足迹/地址/客服/会员中心/签到/任务中心/安全中心）
 * → 更多服务快捷入口 → 退出登录（二次确认后清空登录态回登录页）
 */
defineOptions({ name: 'Profile' })

const router = useRouter()
const authStore = useAuthStore()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const favoriteCount = ref(0)
const historyCount = ref(0)

/** KeepAlive 激活标记：首次激活与 onMounted 同步，跳过重复刷新 */
let firstActivation = true

/** 会员徽章（memberLevelName 缺失时回退「普通会员」） */
const memberBadge = computed(() => authStore.user?.memberLevelName ?? '普通会员')

/** 脱敏手机号（未绑定为空） */
const maskedPhone = computed(() =>
  authStore.user?.phone ? maskPhone(authStore.user.phone) : '',
)

/** 昵称首字（无头像时的占位字母） */
const avatarLetter = computed(() => (authStore.user?.nickname ?? '友').slice(0, 1))

/** 我的订单四状态入口（对齐 OrderListTab 状态枚举） */
const ORDER_ENTRIES = [
  { icon: 'paid', label: '待付款', tab: 'PendingPayment' },
  { icon: 'send-gift-o', label: '待发货', tab: 'Paid' },
  { icon: 'logistics', label: '待收货', tab: 'Shipped' },
  { icon: 'star-o', label: '待评价', tab: 'Completed' },
] as const

/** 功能宫格（仅保留可跳转的真实入口；客服暂无独立页面，提示联系方式） */
const FUNC_ENTRIES = [
  { icon: 'like-o', label: '收藏', color: '#FF4D4F', bg: '#FFF0F0', to: '/profile/favorites', badge: true },
  { icon: 'clock-o', label: '足迹', color: '#1677FF', bg: '#E6F4FF', to: '/profile/history', badge: false },
  { icon: 'location-o', label: '地址', color: '#52C41A', bg: '#F6FFED', to: '/profile/addresses', badge: false },
  { icon: 'service-o', label: '客服', color: '#FAAD14', bg: '#FFF7E6', to: '', badge: false },
  { icon: 'gem-o', label: '会员中心', color: '#722ED1', bg: '#F9F0FF', to: '/member/level', badge: false },
  { icon: 'medal-o', label: '签到', color: '#EB2F96', bg: '#FFF0F6', to: '/points/check-in', badge: false },
  { icon: 'flag-o', label: '任务中心', color: '#13C2C2', bg: '#E6FFFB', to: '/points/tasks', badge: false },
  { icon: 'shield-o', label: '安全中心', color: '#FF4D4F', bg: '#FFF1F0', to: '/profile/security', badge: false },
] as const

/** 更多服务快捷入口 */
const QUICK_ENTRIES = [
  { icon: 'wallet-o', label: '积分账户', to: '/points/account' },
  { icon: 'gift-o', label: '积分商城', to: '/points/exchange' },
  { icon: 'gem-o', label: '会员特权', to: '/member/packages' },
  { icon: 'chat-o', label: '消息中心', to: '/notifications' },
] as const

onMounted(() => {
  void loadAll()
})

// 返回本页时静默刷新（收藏/足迹/资料可能已变化）
onActivated(() => {
  if (firstActivation) {
    firstActivation = false
    return
  }
  void loadAll(true)
})

/**
 * 加载资料与资产数据
 *
 * @param silent 静默刷新（已有资料时不展示骨架屏）
 */
async function loadAll(silent = false): Promise<void> {
  if (!silent || !authStore.user) {
    loading.value = true
  }
  loadError.value = false
  try {
    const [profile, favCount, historyList] = await Promise.all([
      profileApi.getProfile(),
      favoriteApi.count().catch((e) => {
        logger.warn('拉取收藏数失败（忽略）', e)
        return 0
      }),
      historyApi.list().catch((e) => {
        logger.warn('拉取足迹数失败（忽略）', e)
        return [] as unknown[]
      }),
    ])
    authStore.user = profile
    favoriteCount.value = favCount
    historyCount.value = historyList.length
  } catch (e) {
    logger.error('我的页资料加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

// ---- 跳转 ----
function goOrders(tab?: string): void {
  router.push(tab ? `/orders?tab=${tab}` : '/orders')
}

function goFuncEntry(entry: (typeof FUNC_ENTRIES)[number]): void {
  if (entry.to) {
    router.push(entry.to)
    return
  }
  showToast('客服热线 400-800-1234（9:00-21:00）')
}

function goQuickEntry(entry: (typeof QUICK_ENTRIES)[number]): void {
  router.push(entry.to)
}

function goSettings(): void {
  router.push('/settings')
}

function goNotifications(): void {
  router.push('/notifications')
}

// ---- 退出登录 ----
async function onLogout(): Promise<void> {
  try {
    await showConfirmDialog({
      title: '确认退出登录',
      message: '退出后需重新登录才能继续使用，本地偏好设置保留。',
      confirmButtonText: '退出',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  try {
    await authStore.logout()
    showToast('已退出登录')
    router.replace('/login')
  } catch (e) {
    logger.error('退出登录失败', e)
    showFailToast('退出失败，请稍后重试')
  }
}
</script>

<template>
  <div class="profile-page">
    <!-- 骨架屏 -->
    <template v-if="loading && !authStore.user">
      <div class="profile-header sk-header">
        <div class="sk-header-actions">
          <div class="skeleton-block sk-action" />
          <div class="skeleton-block sk-action" />
        </div>
        <div class="sk-user">
          <div class="skeleton-block sk-avatar" />
          <div class="sk-user-info">
            <div class="skeleton-block sk-name" />
            <div class="skeleton-block sk-phone" />
          </div>
        </div>
      </div>
      <div class="content">
        <div class="sk-asset">
          <div v-for="i in 3" :key="i" class="sk-asset-item">
            <div class="skeleton-block sk-asset-value" />
            <div class="skeleton-block sk-asset-label" />
          </div>
        </div>
        <div class="sk-section">
          <div class="skeleton-block sk-section-title" />
          <div class="sk-order-grid">
            <div v-for="i in 5" :key="i" class="sk-order-item">
              <div class="skeleton-block sk-order-icon" />
              <div class="skeleton-block sk-order-label" />
            </div>
          </div>
        </div>
        <div class="sk-section">
          <div class="skeleton-block sk-section-title" />
          <div class="sk-func-grid">
            <div v-for="i in 8" :key="i" class="sk-func-item">
              <div class="skeleton-block sk-func-icon" />
              <div class="skeleton-block sk-func-label" />
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- 错误态（无本地资料时） -->
    <template v-else-if="loadError && !authStore.user">
      <div class="profile-header error-header">
        <div class="user-card">
          <div class="avatar avatar-placeholder">
            <van-icon name="user-o" size="34" color="#1677FF" />
          </div>
          <div class="user-info">
            <div class="user-name">未登录</div>
          </div>
        </div>
      </div>
      <div class="content">
        <ErrorState title="加载失败" description="网络异常，请检查网络连接后重试" @retry="loadAll()" />
      </div>
    </template>

    <!-- 内容区 -->
    <template v-else>
      <!-- 用户信息头部 -->
      <header class="profile-header">
        <div class="header-actions">
          <button class="header-action" type="button" aria-label="设置" @click="goSettings">
            <van-icon name="setting-o" size="20" color="#fff" />
          </button>
          <button class="header-action" type="button" aria-label="消息" @click="goNotifications">
            <van-icon name="bell" size="20" color="#fff" />
          </button>
        </div>
        <div class="user-card">
          <div class="avatar-wrap" role="link" aria-label="查看个人资料" @click="router.push('/profile/security')">
            <img v-if="authStore.user?.avatar" class="avatar" :src="authStore.user.avatar" :alt="authStore.user.nickname">
            <div v-else class="avatar avatar-placeholder">
              <span class="avatar-letter">{{ avatarLetter }}</span>
            </div>
          </div>
          <div class="user-info">
            <div class="user-name">{{ authStore.user?.nickname ?? '未登录' }}</div>
            <span class="member-badge">
              <van-icon name="medal-o" size="12" color="#fff" />
              {{ memberBadge }}
            </span>
            <div v-if="maskedPhone" class="user-phone">{{ maskedPhone }}</div>
          </div>
        </div>
      </header>

      <div class="content">
        <!-- 资产卡片 -->
        <div class="asset-card">
          <div class="asset-item" role="link" @click="router.push('/points/account')">
            <div class="asset-value">{{ formatPoints(authStore.user?.points ?? 0) }}</div>
            <div class="asset-label">积分</div>
          </div>
          <div class="asset-item" role="link" @click="router.push('/profile/favorites')">
            <div class="asset-value">{{ formatPoints(favoriteCount) }}</div>
            <div class="asset-label">收藏</div>
          </div>
          <div class="asset-item" role="link" @click="router.push('/profile/history')">
            <div class="asset-value">{{ formatPoints(historyCount) }}</div>
            <div class="asset-label">足迹</div>
          </div>
        </div>

        <!-- 我的订单 -->
        <section class="section">
          <div class="section-header">
            <span class="section-title">我的订单</span>
            <button class="section-more" type="button" @click="goOrders()">
              全部订单
              <van-icon name="arrow" size="12" color="#8C8C8C" />
            </button>
          </div>
          <div class="divider" />
          <div class="order-grid">
            <button
              v-for="entry in ORDER_ENTRIES"
              :key="entry.tab"
              class="order-item"
              type="button"
              role="link"
              :aria-label="entry.label"
              @click="goOrders(entry.tab)"
            >
              <span class="order-icon-wrap">
                <van-icon :name="entry.icon" size="26" color="#595959" />
              </span>
              <span class="order-label">{{ entry.label }}</span>
            </button>
          </div>
        </section>

        <!-- 常用功能宫格 -->
        <section class="section">
          <div class="section-header">
            <span class="section-title">常用功能</span>
          </div>
          <div class="divider" />
          <div class="func-grid">
            <button
              v-for="entry in FUNC_ENTRIES"
              :key="entry.label"
              class="func-item"
              type="button"
              role="link"
              :aria-label="entry.label"
              @click="goFuncEntry(entry)"
            >
              <span class="func-icon" :style="{ background: entry.bg }">
                <van-icon :name="entry.icon" size="22" :color="entry.color" />
              </span>
              <span class="func-label">{{ entry.label }}</span>
              <span v-if="entry.badge && favoriteCount > 0" class="func-badge">
                {{ favoriteCount > 99 ? '99+' : favoriteCount }}
              </span>
            </button>
          </div>
        </section>

        <!-- 更多服务 -->
        <section class="quick-entry">
          <div class="section-header">
            <span class="section-title">更多服务</span>
          </div>
          <div class="divider" />
          <div class="quick-entry-list">
            <button
              v-for="entry in QUICK_ENTRIES"
              :key="entry.label"
              class="quick-entry-item"
              type="button"
              role="link"
              :aria-label="entry.label"
              @click="goQuickEntry(entry)"
            >
              <van-icon :name="entry.icon" size="14" color="#1677FF" />
              {{ entry.label }}
            </button>
          </div>
        </section>

        <!-- 退出登录 -->
        <button class="logout-btn" type="button" aria-label="退出登录" @click="onLogout">
          <van-icon name="revoke" size="16" color="#FF4D4F" />
          退出登录
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.profile-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n3);
  overflow-y: auto;
}

/* ===== 用户信息头部 ===== */
.profile-header {
  background: linear-gradient(135deg, #1677ff 0%, #0958d9 100%);
  padding: var(--s4) var(--s4) var(--s6);
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
}

.profile-header::before {
  content: "";
  position: absolute;
  top: -40px;
  right: -30px;
  width: 160px;
  height: 160px;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.12) 0%, transparent 70%);
  border-radius: 50%;
}

.header-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--s4);
  margin-bottom: var(--s4);
  position: relative;
}

.header-action {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.18);
  display: flex;
  align-items: center;
  justify-content: center;
}

.user-card {
  display: flex;
  align-items: center;
  gap: var(--s4);
  position: relative;
}

.avatar-wrap {
  flex-shrink: 0;
}

.avatar {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  border: 3px solid rgba(255, 255, 255, 0.3);
  background: var(--n1);
  overflow: hidden;
  object-fit: cover;
  display: flex;
  align-items: center;
  justify-content: center;
}

.avatar-placeholder {
  background: #e6f4ff;
}

.avatar-letter {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  color: var(--c-primary);
}

.user-info {
  flex: 1;
  min-width: 0;
}

.user-name {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: #fff;
  margin-bottom: var(--s1);
}

.member-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: linear-gradient(90deg, #f7b500, #ff8800);
  color: #fff;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  padding: 2px var(--s2);
  border-radius: var(--r-base);
}

.user-phone {
  font-size: var(--fs-sm);
  color: rgba(255, 255, 255, 0.75);
  margin-top: var(--s1);
}

.error-header {
  padding-bottom: var(--s4);
}

/* ===== 内容区 ===== */
.content {
  flex: 1;
  padding: 0 0 calc(var(--s12) + env(safe-area-inset-bottom));
}

/* ===== 资产卡片 ===== */
.asset-card {
  background: var(--n1);
  margin: -16px var(--s3) 0;
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4) 0;
  display: flex;
  position: relative;
  z-index: 2;
}

.asset-item {
  flex: 1;
  text-align: center;
  position: relative;
  background: none;
}

.asset-item:not(:last-child)::after {
  content: "";
  position: absolute;
  right: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 1px;
  height: 28px;
  background: var(--n3);
}

.asset-value {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
  line-height: 1.2;
}

.asset-label {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
}

/* ===== 区块 ===== */
.section {
  background: var(--n1);
  margin: var(--s3);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3) var(--s4);
}

.section-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.section-more {
  font-size: var(--fs-sm);
  color: var(--n7);
  display: flex;
  align-items: center;
  gap: 2px;
}

.divider {
  height: 1px;
  background: var(--n3);
  margin: 0 var(--s4);
}

/* ===== 订单宫格 ===== */
.order-grid {
  display: flex;
  padding: var(--s2) var(--s2) var(--s4);
}

.order-item {
  flex: 1;
  text-align: center;
  padding: var(--s2) 0;
}

.order-icon-wrap {
  display: inline-flex;
  margin-bottom: var(--s1);
}

.order-label {
  display: block;
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* ===== 功能宫格 ===== */
.func-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  padding: var(--s2) var(--s2) var(--s4);
}

.func-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: var(--s3) 0;
  position: relative;
}

.func-icon {
  width: 40px;
  height: 40px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: var(--s1);
}

.func-label {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.func-badge {
  position: absolute;
  top: 6px;
  right: 50%;
  transform: translateX(20px);
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  background: var(--c-error);
  color: #fff;
  font-size: 10px;
  font-weight: var(--fw-medium);
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
}

/* ===== 更多服务 ===== */
.quick-entry {
  background: var(--n1);
  margin: var(--s3);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.quick-entry-list {
  display: flex;
  padding: var(--s3);
  gap: var(--s2);
  flex-wrap: wrap;
}

.quick-entry-item {
  display: flex;
  align-items: center;
  gap: var(--s1);
  padding: var(--s1) var(--s2);
  background: var(--n2);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* ===== 退出登录 ===== */
.logout-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
  width: calc(100% - var(--s6));
  margin: var(--s2) var(--s3);
  height: 44px;
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--c-error);
}

/* ===== 骨架屏 ===== */
.sk-header {
  min-height: 150px;
}

.sk-header-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--s4);
  margin-bottom: var(--s4);
}

.sk-action {
  width: 32px;
  height: 32px;
  border-radius: 50%;
}

.sk-user {
  display: flex;
  align-items: center;
  gap: var(--s4);
}

.sk-avatar {
  width: 64px;
  height: 64px;
  border-radius: 50%;
}

.sk-user-info {
  flex: 1;
}

.sk-name {
  width: 100px;
  height: 20px;
  margin-bottom: var(--s2);
}

.sk-phone {
  width: 80px;
  height: 14px;
}

.sk-asset {
  background: var(--n1);
  margin: -16px var(--s3) 0;
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4) 0;
  display: flex;
}

.sk-asset-item {
  flex: 1;
  text-align: center;
}

.sk-asset-value {
  width: 40px;
  height: 20px;
  margin: 0 auto var(--s1);
}

.sk-asset-label {
  width: 30px;
  height: 12px;
  margin: 0 auto;
}

.sk-section {
  background: var(--n1);
  margin: var(--s3);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3) var(--s4) var(--s4);
}

.sk-section-title {
  width: 80px;
  height: 16px;
  margin-bottom: var(--s3);
}

.sk-order-grid {
  display: flex;
}

.sk-order-item {
  flex: 1;
  text-align: center;
}

.sk-order-icon {
  width: 26px;
  height: 26px;
  border-radius: var(--r-card);
  margin: 0 auto var(--s1);
}

.sk-order-label {
  width: 32px;
  height: 12px;
  margin: 0 auto;
}

.sk-func-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  row-gap: var(--s3);
}

.sk-func-item {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.sk-func-icon {
  width: 40px;
  height: 40px;
  border-radius: var(--r-card);
  margin-bottom: var(--s1);
}

.sk-func-label {
  width: 28px;
  height: 12px;
}
</style>
