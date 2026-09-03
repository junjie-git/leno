<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { notificationApi } from '@/modules/12-notification/api/notification.api'
import type { NotificationPreferencesDto } from '../types/notification.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 通知偏好设置页（/notifications/preferences）
 *
 * 结构（对齐设计稿 preferences，按 dto 偏好结构分组）：
 * NavBar（返回 / 通知偏好）→ 顶部提示条
 * → 接收渠道分组（站内通知 / 短信 / 邮件；站内信默认开启不可关闭）
 * → 消息分类分组（订单 / 物流 / 优惠促销 / 积分 / 售后 / 系统）
 * → 底部固定保存栏（已修改 N 项 + 保存设置）
 *
 * 数据流：GET /users/me/notification-preferences 渲染开关矩阵；
 * 保存 PUT 全量偏好，成功 toast 并返回上一页。
 */

const router = useRouter()

/** 偏好默认值（加载失败兜底不使用，仅初始化 form 结构） */
function defaultPreferences(): NotificationPreferencesDto {
  return {
    inApp: true,
    sms: true,
    email: false,
    order: true,
    logistics: true,
    promotion: true,
    points: true,
    afterSales: true,
    system: true,
  }
}

/** 接收渠道分组（站内信锁定开启，保证关键通知触达） */
const CHANNELS: Array<{
  key: 'inApp' | 'sms' | 'email'
  name: string
  desc: string
  icon: string
  cls: string
  locked: boolean
}> = [
  { key: 'inApp', name: '站内通知', desc: '消息中心站内信，关键通知渠道', icon: 'bell', cls: 'ic-system', locked: true },
  { key: 'sms', name: '短信通知', desc: '发送至绑定手机号', icon: 'chat', cls: 'ic-order', locked: false },
  { key: 'email', name: '邮件通知', desc: '发送至绑定邮箱', icon: 'mail', cls: 'ic-logistics', locked: false },
]

/** 消息分类分组 */
const CATEGORIES: Array<{
  key: 'order' | 'logistics' | 'promotion' | 'points' | 'afterSales' | 'system'
  name: string
  desc: string
  icon: string
  cls: string
}> = [
  { key: 'order', name: '订单通知', desc: '下单、支付、发货、收货等订单状态变更', icon: 'cart', cls: 'ic-order' },
  { key: 'logistics', name: '物流通知', desc: '包裹揽收、运输、派送与签收提醒', icon: 'truck', cls: 'ic-logistics' },
  { key: 'promotion', name: '优惠促销', desc: '优惠券到账、限时秒杀与降价提醒', icon: 'gift', cls: 'ic-promo' },
  { key: 'points', name: '积分通知', desc: '积分到账、消耗与过期提醒', icon: 'coin', cls: 'ic-points' },
  { key: 'afterSales', name: '售后通知', desc: '售后申请、审核结果与退款到账', icon: 'refund', cls: 'ic-aftersales' },
  { key: 'system', name: '系统通知', desc: '平台公告与账户安全提醒（建议开启）', icon: 'shield', cls: 'ic-system' },
]

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const saving = ref(false)
const form = ref<NotificationPreferencesDto>(defaultPreferences())
/** 服务端初始快照（计算变更项数） */
const initial = ref<NotificationPreferencesDto | null>(null)

onMounted(() => {
  void loadPreferences()
})

/** 与初始快照对比的变更项数 */
const changedCount = computed(() => {
  const init = initial.value
  if (!init) {
    return 0
  }
  let count = 0
  for (const key of Object.keys(form.value) as Array<keyof NotificationPreferencesDto>) {
    if (form.value[key] !== init[key]) {
      count += 1
    }
  }
  return count
})

// ---- 数据加载 ----
async function loadPreferences(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    const prefs = await notificationApi.getPreferences()
    form.value = { ...prefs }
    initial.value = { ...prefs }
  } catch (e) {
    logger.error('通知偏好加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

// ---- 保存 ----
async function save(): Promise<void> {
  if (saving.value) {
    return
  }
  if (changedCount.value === 0) {
    showToast('设置未变更')
    return
  }
  saving.value = true
  try {
    const saved = await notificationApi.updatePreferences({ ...form.value })
    form.value = { ...saved }
    initial.value = { ...saved }
    showToast('保存成功')
    goBack()
  } catch (e) {
    logger.warn('通知偏好保存失败', e)
    showFailToast(e instanceof Error ? e.message : '保存失败，请稍后重试')
  } finally {
    saving.value = false
  }
}

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/notifications')
  }
}
</script>

<template>
  <div class="pref-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">通知偏好</div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="sk-group">
        <div class="skeleton-block sk-line short" />
        <div class="sk-card">
          <div v-for="i in 3" :key="i" class="sk-row">
            <div class="skeleton-block sk-icon-sq" />
            <div class="sk-lines">
              <div class="skeleton-block sk-line mid" />
              <div class="skeleton-block sk-line tiny" />
            </div>
            <div class="skeleton-block sk-switch" />
          </div>
        </div>
      </div>
      <div class="sk-group">
        <div class="skeleton-block sk-line short" />
        <div class="sk-card">
          <div v-for="i in 3" :key="i" class="sk-row">
            <div class="skeleton-block sk-icon-sq" />
            <div class="sk-lines">
              <div class="skeleton-block sk-line mid" />
              <div class="skeleton-block sk-line tiny" />
            </div>
            <div class="skeleton-block sk-switch" />
          </div>
        </div>
      </div>
    </main>

    <!-- 加载失败 -->
    <main v-else-if="loadError" class="body">
      <ErrorState title="偏好设置加载失败" description="网络异常，请稍后重试" @retry="loadPreferences" />
    </main>

    <!-- 开关矩阵 -->
    <main v-else class="body">
      <!-- 顶部提示条 -->
      <div class="tip-bar">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <circle cx="12" cy="12" r="10" />
          <line x1="12" y1="16" x2="12" y2="12" />
          <line x1="12" y1="8" x2="12.01" y2="8" />
        </svg>
        <span>关闭某类通知后，将不再接收该类站内信与推送消息</span>
      </div>

      <!-- 接收渠道 -->
      <section class="group">
        <div class="group-header">
          <span class="group-dot dot-channel" />
          <span class="group-title">接收渠道</span>
          <span class="group-count">{{ CHANNELS.length }} 项</span>
        </div>
        <div class="group-card">
          <div v-for="channel in CHANNELS" :key="channel.key" class="event-item">
            <span class="event-icon" :class="channel.cls" role="img" :aria-label="channel.name">
              <!-- 铃铛 -->
              <svg v-if="channel.icon === 'bell'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
              <!-- 短信 -->
              <svg v-else-if="channel.icon === 'chat'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
              </svg>
              <!-- 邮件 -->
              <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <rect x="2" y="4" width="20" height="16" rx="2" />
                <polyline points="22 6 12 13 2 6" />
              </svg>
            </span>
            <div class="event-body">
              <div class="event-name">
                {{ channel.name }}
                <span v-if="channel.locked" class="lock-tag">不可关闭</span>
              </div>
              <div class="event-desc">{{ channel.desc }}</div>
            </div>
            <van-switch
              v-model="form[channel.key]"
              :disabled="channel.locked"
              size="22px"
              :aria-label="channel.name"
            />
          </div>
        </div>
      </section>

      <!-- 消息分类 -->
      <section class="group">
        <div class="group-header">
          <span class="group-dot dot-category" />
          <span class="group-title">消息分类</span>
          <span class="group-count">{{ CATEGORIES.length }} 项</span>
        </div>
        <div class="group-card">
          <div v-for="category in CATEGORIES" :key="category.key" class="event-item">
            <span class="event-icon" :class="category.cls" role="img" :aria-label="category.name">
              <!-- 购物车 -->
              <svg v-if="category.icon === 'cart'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="9" cy="21" r="1" />
                <circle cx="20" cy="21" r="1" />
                <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
              </svg>
              <!-- 卡车 -->
              <svg v-else-if="category.icon === 'truck'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <rect x="1" y="5" width="13" height="11" rx="1" />
                <path d="M14 9h4l4 4v3h-8z" />
                <circle cx="6" cy="18.5" r="1.8" />
                <circle cx="17.5" cy="18.5" r="1.8" />
              </svg>
              <!-- 礼盒 -->
              <svg v-else-if="category.icon === 'gift'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 12 20 22 4 22 4 12" />
                <rect x="2" y="7" width="20" height="5" />
                <line x1="12" y1="22" x2="12" y2="7" />
                <path d="M12 7H7.5a2.5 2.5 0 0 1 0-5C11 2 12 7 12 7z" />
                <path d="M12 7h4.5a2.5 2.5 0 0 0 0-5C13 2 12 7 12 7z" />
              </svg>
              <!-- 金币 -->
              <svg v-else-if="category.icon === 'coin'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="9" />
                <path d="M9 8l3 4 3-4M12 12v5M9.5 13.5h5M9.5 15.5h5" />
              </svg>
              <!-- 退款 -->
              <svg v-else-if="category.icon === 'refund'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="1 4 1 10 7 10" />
                <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
              </svg>
              <!-- 盾牌 -->
              <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
              </svg>
            </span>
            <div class="event-body">
              <div class="event-name">{{ category.name }}</div>
              <div class="event-desc">{{ category.desc }}</div>
            </div>
            <van-switch v-model="form[category.key]" size="22px" :aria-label="category.name" />
          </div>
        </div>
      </section>
    </main>

    <!-- 底部保存栏 -->
    <footer class="save-bar">
      <div class="save-info">
        已修改 <span class="changed" :class="{ zero: changedCount === 0 }">{{ changedCount }}</span> 项设置
      </div>
      <button
        class="save-btn"
        :class="{ loading: saving }"
        type="button"
        :disabled="saving"
        aria-label="保存通知偏好设置"
        @click="save"
      >
        <span v-if="saving" class="spinner" />
        {{ saving ? '保存中' : '保存设置' }}
      </button>
    </footer>
  </div>
</template>

<style scoped>
.pref-page {
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

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s8) + env(safe-area-inset-bottom));
}

/* 提示条 */
.tip-bar {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px var(--s3);
  background: #e6f0ff;
  color: var(--c-primary);
  font-size: var(--fs-sm);
  border-radius: var(--r-card);
  margin-bottom: var(--s3);
}

.tip-bar svg {
  flex-shrink: 0;
}

/* 骨架屏 */
.sk-group {
  margin-bottom: var(--s3);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s2) var(--s3);
}

.sk-row {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: 10px 0;
}

.sk-row + .sk-row {
  border-top: 1px solid var(--n3);
}

.sk-icon-sq {
  width: 32px;
  height: 32px;
  border-radius: var(--r-base);
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.sk-line {
  height: 14px;
}

.sk-line.short {
  width: 30%;
  margin-bottom: var(--s2);
}

.sk-line.mid {
  width: 70%;
}

.sk-line.tiny {
  width: 45%;
  height: 11px;
}

.sk-switch {
  width: 44px;
  height: 24px;
  border-radius: 12px;
  flex-shrink: 0;
}

/* 分组 */
.group {
  margin-bottom: var(--s3);
}

.group-header {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s2) var(--s1);
}

.group-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot-channel {
  background: var(--c-primary);
}

.dot-category {
  background: var(--c-warning);
}

.group-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.group-count {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-left: auto;
}

.group-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

/* 事件项 */
.event-item {
  display: flex;
  align-items: center;
  padding: var(--s3);
  gap: var(--s3);
  position: relative;
}

.event-item + .event-item::before {
  content: "";
  position: absolute;
  top: 0;
  left: var(--s3);
  right: var(--s3);
  height: 1px;
  background: var(--n3);
}

.event-icon {
  width: 32px;
  height: 32px;
  border-radius: var(--r-base);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.event-icon svg {
  width: 18px;
  height: 18px;
}

.ic-order {
  background: #e6f0ff;
  color: var(--c-primary);
}

.ic-logistics {
  background: rgba(82, 196, 26, 0.12);
  color: var(--c-success);
}

.ic-promo {
  background: #fff7e6;
  color: var(--c-warning);
}

.ic-points {
  background: rgba(114, 46, 209, 0.1);
  color: var(--c-buyer);
}

.ic-aftersales {
  background: #fff1f0;
  color: var(--c-error);
}

.ic-system {
  background: #f0f0f0;
  color: var(--n7);
}

.event-body {
  flex: 1;
  min-width: 0;
}

.event-name {
  font-size: var(--fs-base);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: var(--s1);
  flex-wrap: wrap;
}

.lock-tag {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: var(--r-base);
  background: rgba(22, 119, 255, 0.08);
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.event-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
  line-height: 1.5;
}

/* 底部保存栏 */
.save-bar {
  flex-shrink: 0;
  min-height: 64px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
  gap: var(--s3);
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.04);
}

.save-info {
  flex: 1;
  font-size: var(--fs-sm);
  color: var(--n7);
}

.save-info .changed {
  color: var(--c-warning);
  font-weight: var(--fw-medium);
}

.save-info .changed.zero {
  color: var(--n7);
  font-weight: var(--fw-normal);
}

.save-btn {
  height: 44px;
  padding: 0 32px;
  background: var(--c-primary);
  color: #fff;
  border: none;
  border-radius: 22px;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  cursor: pointer;
  font-family: inherit;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  transition: opacity var(--d-fast) var(--ease-std);
  flex-shrink: 0;
}

.save-btn:active {
  opacity: 0.85;
}

.save-btn.loading {
  opacity: 0.7;
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.4);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
