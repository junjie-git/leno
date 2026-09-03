<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { categoryApi } from '@/modules/03-catalog/api/category.api'
import { productApi as catalogProductApi } from '@/modules/03-catalog/api/product.api'
import { seckillApi } from '@/modules/08-promotion/api/seckill.api'
import { notificationApi } from '@/modules/12-notification/api/notification.api'
import { publicApi } from '@/modules/14-public/api/public.api'
import { useAuthStore } from '@/shared/auth'
import type { AnnouncementDto } from '@/modules/14-public/types/public.dto'
import type { CategoryDto, ProductSummaryDto } from '@/modules/03-catalog/types/product.dto'
import type { SeckillActivityDto } from '@/modules/08-promotion/types/promotion.dto'
import { bannerImage } from '@/shared/utils/svg-image'
import ProductCard from '@/shared/components/ProductCard.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 首页推荐流
 *
 * 结构（对齐设计稿 home-feed）：
 * 顶部搜索栏（Logo + 搜索框 + 通知铃铛）→ 公告条（跑马灯）→ Banner 轮播
 * → 秒杀入口（倒计时 + 横滑商品）→ 分类快捷入口 → 为你推荐（双列瀑布 + 无限加载）
 */

const router = useRouter()
const authStore = useAuthStore()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const announcements = ref<AnnouncementDto[]>([])
const categories = ref<CategoryDto[]>([])
const seckillActivity = ref<SeckillActivityDto | null>(null)
const unreadCount = ref(0)
const products = ref<ProductSummaryDto[]>([])
const page = ref(1)
const pageSize = 10
const finished = ref(false)
const listLoading = ref(false)
const listError = ref(false)

// ---- 秒杀倒计时 ----
const nowMs = ref(Date.now())
let ticker: ReturnType<typeof setInterval> | null = null

const countdown = computed(() => {
  const activity = seckillActivity.value
  if (!activity) return null
  const remain = Math.max(0, new Date(activity.endTime).getTime() - nowMs.value)
  const h = Math.floor(remain / 3_600_000)
  const m = Math.floor((remain % 3_600_000) / 60_000)
  const s = Math.floor((remain % 60_000) / 1000)
  const pad = (n: number) => String(n).padStart(2, '0')
  return { h: pad(h), m: pad(m), s: pad(s) }
})

// ---- Banner（公告置顶在前，取前 3 条生成渐变 Banner） ----
const BANNER_THEMES = [
  { from: '#1677FF', to: '#0952C9' },
  { from: '#FF6A3D', to: '#FF4D4F' },
  { from: '#52C41A', to: '#38A100' },
] as const

const banners = computed(() =>
  announcements.value.slice(0, 3).map((a, i) => ({
    id: a.id,
    title: a.title,
    subtitle: a.type === 'Maintenance' ? '查看维护时间安排' : '点击查看活动详情',
    image: bannerImage(a.title.length > 12 ? `${a.title.slice(0, 12)}…` : a.title, a.type === 'Maintenance' ? '平台维护公告' : '限时活动 进行中', BANNER_THEMES[i % BANNER_THEMES.length].from, BANNER_THEMES[i % BANNER_THEMES.length].to),
    announcement: a,
  })),
)

// ---- 公告跑马灯文案 ----
const noticeText = computed(() =>
  announcements.value.length > 0
    ? announcements.value.map((a) => a.title).join(' · ')
    : '品质生活 · 一触即达',
)

// ---- 分类快捷入口（前 7 个 + 更多） ----
const CATEGORY_ICONS: Record<string, { icon: string; color: string; bg: string }> = {
  'cat-1': { icon: 'phone-o', color: '#1677FF', bg: '#E6F4FF' },
  'cat-2': { icon: 'shirt-o', color: '#FF4D4F', bg: '#FFF1F0' },
  'cat-3': { icon: 'shopping-cart-o', color: '#52C41A', bg: '#F6FFED' },
  'cat-4': { icon: 'gem-o', color: '#FAAD14', bg: '#FFF7E6' },
  'cat-5': { icon: 'home-o', color: '#722ED1', bg: '#F9F0FF' },
  'cat-6': { icon: 'fitness-o', color: '#13C2C2', bg: '#E6FFFB' },
  'cat-7': { icon: 'gift-o', color: '#EB2F96', bg: '#FFF0F6' },
  'cat-8': { icon: 'notes-o', color: '#2F54EB', bg: '#F0F5FF' },
}

const categoryEntries = computed(() => categories.value.slice(0, 7))

// ---- 秒杀横滑商品（前 4 个） ----
const seckillItems = computed(() => seckillActivity.value?.items.slice(0, 6) ?? [])

onMounted(() => {
  ticker = setInterval(() => {
    nowMs.value = Date.now()
  }, 1000)
  void loadAll()
})

onUnmounted(() => {
  if (ticker) {
    clearInterval(ticker)
    ticker = null
  }
})

async function loadAll(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    const [announcementList, categoryTree, activities] = await Promise.all([
      publicApi.listAnnouncements(),
      categoryApi.getTree(),
      seckillApi.listActivities(),
    ])
    announcements.value = announcementList
    categories.value = categoryTree
    seckillActivity.value = activities.find((a) => a.status === 'Active') ?? activities[0] ?? null
    // 登录态下拉取未读数（匿名隐藏角标）
    if (authStore.isAuthenticated) {
      try {
        unreadCount.value = await notificationApi.getUnreadCount()
      } catch (e) {
        logger.warn('拉取未读数失败（忽略）', e)
      }
    }
    // 首屏推荐
    const firstPage = await catalogProductApi.search({ page: 1, pageSize, sort: 'default' })
    products.value = firstPage.items
    page.value = 1
    finished.value = firstPage.items.length < pageSize
  } catch (e) {
    logger.error('首页加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 推荐流无限加载 */
async function onLoad(): Promise<void> {
  if (finished.value) return
  listLoading.value = true
  listError.value = false
  try {
    const next = await catalogProductApi.search({ page: page.value + 1, pageSize, sort: 'default' })
    products.value.push(...next.items)
    page.value += 1
    if (next.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    logger.warn('推荐流加载失败', e)
    listError.value = true
  } finally {
    listLoading.value = false
  }
}

function goSearch(): void {
  router.push('/search')
}

function goNotifications(): void {
  router.push('/notifications')
}

function goAnnouncements(): void {
  router.push('/announcements')
}

function goSeckill(): void {
  if (seckillActivity.value) {
    router.push(`/seckill/order/${seckillActivity.value.id}`)
  }
}

function goCategory(categoryId?: string): void {
  router.push(categoryId ? `/category?categoryId=${categoryId}` : '/category')
}

function goProduct(spuId: string): void {
  router.push(`/product/${spuId}`)
}
</script>

<template>
  <div class="home-page">
    <!-- 顶部搜索栏 -->
    <header class="search-bar">
      <span class="logo">Leno</span>
      <div class="search-input" role="search" aria-label="搜索商品" @click="goSearch">
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
          <circle cx="7" cy="7" r="5" stroke="currentColor" stroke-width="1.4" />
          <path d="M11 11l3 3" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" />
        </svg>
        <span>搜索商品/品牌</span>
      </div>
      <div class="bell-wrap" aria-label="通知" @click="goNotifications">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
          <path d="M12 3a6 6 0 0 0-6 6v3.5L4.5 16h15L18 12.5V9a6 6 0 0 0-6-6Z" stroke="#595959" stroke-width="1.5" stroke-linejoin="round" />
          <path d="M10 19a2 2 0 0 0 4 0" stroke="#595959" stroke-width="1.5" stroke-linecap="round" />
        </svg>
        <span v-if="unreadCount > 0" class="bell-badge">{{ unreadCount > 99 ? '99+' : unreadCount }}</span>
      </div>
    </header>

    <!-- 公告条 -->
    <div v-if="!loading && announcements.length > 0" class="notice-bar" @click="goAnnouncements">
      <svg class="notice-icon" width="16" height="16" viewBox="0 0 24 24" fill="none">
        <path d="M9 3h6v2h1a2 2 0 0 1 2 2v2l3 1v2H4v-2l3-1V7a2 2 0 0 1 2-2h0V3Z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" />
        <path d="M10 19a2 2 0 0 0 4 0" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
      </svg>
      <div class="notice-text"><span>{{ noticeText }}</span></div>
      <span class="notice-more">查看</span>
    </div>

    <!-- 骨架屏 -->
    <main v-if="loading" class="content">
      <div class="skeleton-block sk-banner" />
      <div class="skeleton-block sk-seckill" />
      <div class="sk-grid">
        <div v-for="i in 8" :key="i" class="sk-cat">
          <div class="skeleton-block ic" />
          <div class="skeleton-block tx" />
        </div>
      </div>
      <div class="sk-rec">
        <div v-for="i in 4" :key="i" class="sk-card">
          <div class="skeleton-block img" />
          <div class="skeleton-block l1" />
          <div class="skeleton-block l2" />
        </div>
      </div>
    </main>

    <!-- 错误态 -->
    <main v-else-if="loadError" class="content">
      <ErrorState title="首页加载失败" description="网络异常，请检查网络连接后重试" @retry="loadAll" />
    </main>

    <!-- 内容区 -->
    <main v-else class="content">
      <!-- Banner 轮播 -->
      <van-swipe v-if="banners.length > 0" class="banner-swipe" :autoplay="3000" indicator-color="#1677FF">
        <van-swipe-item v-for="banner in banners" :key="banner.id" @click="goAnnouncements">
          <div class="banner-slide">
            <img class="banner-img" :src="banner.image" :alt="banner.title">
            <div class="banner-caption">
              <div class="t">{{ banner.title }}</div>
              <div class="s">{{ banner.subtitle }}</div>
            </div>
          </div>
        </van-swipe-item>
      </van-swipe>

      <!-- 秒杀入口 -->
      <div v-if="seckillActivity && seckillActivity.status === 'Active' && seckillItems.length > 0" class="seckill" @click="goSeckill">
        <div class="seckill-head">
          <div class="seckill-title">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="#FFD666">
              <path d="M13 2 4 14h6l-1 8 9-12h-6l1-8Z" />
            </svg>
            限时秒杀
          </div>
          <div class="seckill-more">
            查看更多
            <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
              <path d="M4 2l4 4-4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
          </div>
        </div>
        <div v-if="countdown" class="seckill-countdown">
          <span class="lbl">距结束</span>
          <div class="nums">
            <b>{{ countdown.h }}</b>:<b>{{ countdown.m }}</b>:<b>{{ countdown.s }}</b>
          </div>
        </div>
        <div class="seckill-list">
          <div v-for="item in seckillItems" :key="item.skuId" class="seckill-card" @click.stop="goProduct(item.spuId)">
            <img class="img" :src="item.image" :alt="item.name">
            <div class="name text-ellipsis">{{ item.name }}</div>
            <div class="price">¥{{ (item.seckillPrice / 100).toFixed(2).replace(/\.?0+$/, '') }}</div>
            <div class="origin">¥{{ (item.originalPrice / 100).toFixed(2).replace(/\.?0+$/, '') }}</div>
          </div>
        </div>
      </div>

      <!-- 分类快捷入口 -->
      <div class="category-grid">
        <div
          v-for="cat in categoryEntries"
          :key="cat.id"
          class="cat-item"
          @click="goCategory(cat.children[0]?.id ?? cat.id)"
        >
          <div class="cat-icon" :style="{ background: CATEGORY_ICONS[cat.id]?.bg ?? '#F5F5F5' }">
            <van-icon :name="CATEGORY_ICONS[cat.id]?.icon ?? 'apps-o'" :color="CATEGORY_ICONS[cat.id]?.color ?? '#8C8C8C'" size="24" />
          </div>
          <span class="cat-name">{{ cat.name }}</span>
        </div>
        <div class="cat-item" @click="goCategory()">
          <div class="cat-icon" style="background: #F0F5FF">
            <van-icon name="apps-o" color="#2F54EB" size="24" />
          </div>
          <span class="cat-name">更多分类</span>
        </div>
      </div>

      <!-- 为你推荐 -->
      <div class="rec-head">
        <span class="line" />
        <span class="t">为你推荐</span>
        <span class="line" />
      </div>

      <van-list
        v-model:loading="listLoading"
        :finished="finished"
        :error="listError"
        error-text="加载失败，点击重试"
        finished-text="没有更多了"
        loading-text="加载中..."
        @load="onLoad"
      >
        <div class="rec-list">
          <ProductCard v-for="product in products" :key="product.id" :product="product" />
        </div>
      </van-list>
    </main>
  </div>
</template>

<style scoped>
.home-page {
  min-height: 100vh;
  background: var(--n2);
  display: flex;
  flex-direction: column;
}

/* 顶部搜索栏 */
.search-bar {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s2) var(--s3);
  background: var(--n1);
  position: sticky;
  top: 0;
  z-index: 40;
}

.logo {
  flex-shrink: 0;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--c-primary);
  letter-spacing: 0.5px;
}

.search-input {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 var(--s3);
  background: var(--n3);
  border-radius: 16px;
  font-size: var(--fs-base);
  color: var(--n7);
}

.bell-wrap {
  position: relative;
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.bell-badge {
  position: absolute;
  top: -2px;
  right: -4px;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  background: var(--c-error);
  color: #fff;
  font-size: 10px;
  line-height: 16px;
  border-radius: 8px;
  text-align: center;
}

/* 公告条 */
.notice-bar {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 var(--s3);
  background: #fff7e6;
  border-bottom: 1px solid #ffe7ba;
}

.notice-icon {
  flex-shrink: 0;
  color: var(--c-warning);
}

.notice-text {
  flex: 1;
  overflow: hidden;
  white-space: nowrap;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.notice-text span {
  display: inline-block;
  padding-left: 100%;
  animation: marquee 14s linear infinite;
}

@keyframes marquee {
  0% {
    transform: translateX(0);
  }

  100% {
    transform: translateX(-100%);
  }
}

.notice-more {
  flex-shrink: 0;
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 内容区 */
.content {
  padding: var(--s3);
  padding-bottom: calc(var(--s12) + env(safe-area-inset-bottom));
  flex: 1;
}

/* 骨架屏 */
.sk-banner {
  height: 160px;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
}

.sk-seckill {
  height: 120px;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
}

.sk-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--s3) var(--s1);
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3) var(--s2);
  margin-bottom: var(--s3);
}

.sk-cat {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.sk-cat .ic {
  width: 44px;
  height: 44px;
  border-radius: var(--r-card);
}

.sk-cat .tx {
  width: 32px;
  height: 12px;
}

.sk-rec {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  padding: var(--s2);
}

.sk-card .img {
  width: 100%;
  aspect-ratio: 1;
  border-radius: var(--r-base);
}

.sk-card .l1 {
  width: 80%;
  height: 14px;
  margin-top: var(--s2);
}

.sk-card .l2 {
  width: 50%;
  height: 14px;
  margin-top: var(--s1);
}

/* Banner */
.banner-swipe {
  height: 160px;
  border-radius: var(--r-lg);
  overflow: hidden;
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
}

.banner-slide {
  position: relative;
  height: 100%;
}

.banner-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.banner-caption {
  position: absolute;
  left: var(--s3);
  bottom: 14px;
  right: var(--s3);
  color: #fff;
  text-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
}

.banner-caption .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
}

.banner-caption .s {
  font-size: var(--fs-sm);
  opacity: 0.9;
  margin-top: 2px;
}

/* 秒杀入口 */
.seckill {
  background: linear-gradient(135deg, #ff6a3d 0%, #ff4d4f 100%);
  border-radius: var(--r-lg);
  padding: var(--s3);
  margin-bottom: var(--s3);
  color: #fff;
  box-shadow: 0 4px 12px rgba(255, 77, 79, 0.25);
}

.seckill-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s2);
}

.seckill-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
}

.seckill-more {
  font-size: var(--fs-sm);
  opacity: 0.92;
  display: flex;
  align-items: center;
  gap: 2px;
}

.seckill-countdown {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--fs-sm);
  margin-bottom: var(--s3);
}

.seckill-countdown .lbl {
  opacity: 0.92;
}

.seckill-countdown .nums {
  display: flex;
  gap: 3px;
  align-items: center;
}

.seckill-countdown .nums b {
  display: inline-block;
  min-width: 22px;
  height: 22px;
  line-height: 22px;
  text-align: center;
  background: rgba(0, 0, 0, 0.22);
  border-radius: var(--r-base);
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
}

.seckill-list {
  display: flex;
  gap: var(--s2);
  overflow-x: auto;
  padding-bottom: 2px;
}

.seckill-list::-webkit-scrollbar {
  display: none;
}

.seckill-card {
  flex-shrink: 0;
  width: 84px;
  background: rgba(255, 255, 255, 0.16);
  border-radius: var(--r-card);
  padding: 6px;
}

.seckill-card .img {
  width: 72px;
  height: 72px;
  border-radius: var(--r-base);
  object-fit: cover;
  background: rgba(255, 255, 255, 0.3);
}

.seckill-card .name {
  font-size: 11px;
  color: #fff;
  margin-top: var(--s1);
}

.seckill-card .price {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: #fff;
  margin-top: 2px;
}

.seckill-card .origin {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.7);
  text-decoration: line-through;
}

/* 分类快捷入口 */
.category-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--s3) var(--s1);
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3) var(--s2);
  margin-bottom: var(--s3);
  box-shadow: var(--sh-card);
}

.cat-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.cat-icon {
  width: 44px;
  height: 44px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--n3);
}

.cat-name {
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* 为你推荐 */
.rec-head {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  margin: var(--s1) 0 var(--s2);
}

.rec-head .line {
  width: 24px;
  height: 1px;
  background: var(--n5);
}

.rec-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.rec-list {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
}
</style>
