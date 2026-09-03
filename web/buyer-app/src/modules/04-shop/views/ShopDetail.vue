<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import { productApi } from '@/modules/03-catalog/api/product.api'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import { useAuthStore } from '@/shared/auth'
import type { ProductSort, ProductSummaryDto } from '@/modules/03-catalog/types/product.dto'
import ProductCard from '@/shared/components/ProductCard.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatSales } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 店铺详情页（/shop/:shopId）
 *
 * 结构（对齐设计稿 shop-detail）：
 * NavBar（返回 + 店铺名 + 关注）→ 店铺头部卡（渐变底、Logo、名称、在售/月销统计、关注按钮）
 * → 商品 Tab（全部商品/新品/爆款）+ 排序条（综合/销量/价格升降）
 * → 双列商品瀑布（ProductCard + van-list 无限加载 + van-pull-refresh 下拉刷新）
 * → 底部浮动条（客服 / 购物车角标入口）
 *
 * 店铺域买家端详情端点暂未开放（见 docs/design-prompts/buyer-app/04-shop/shop-detail.md），
 * 店铺基础信息由商品域 search({ shopId }) 聚合推导：名称/Logo 取首个在售商品，经营数据取
 * 在售总数与已加载商品月销合计；关注为本地态交互（未登录引导登录，取消关注需二次确认）。
 */

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const cartStore = useCartStore()

const pageSize = 10

// ---- 路由参数 ----
const shopId = computed(() => String(route.params.shopId ?? ''))

// ---- 列表状态 ----
const loading = ref(true)
const loadError = ref(false)
const products = ref<ProductSummaryDto[]>([])
const total = ref(0)
const page = ref(1)
const finished = ref(false)
const listLoading = ref(false)
const listError = ref(false)
const refreshing = ref(false)

// ---- 筛选状态：Tab 预设与排序条共享同一 sort 参数 ----
const sort = ref<ProductSort>('default')

// ---- 关注状态 ----
const followed = ref(false)
const followPending = ref(false)

/** 请求序号（切换 Tab/排序/店铺时旧响应作废） */
let searchSeq = 0

/** Tab 定义：全部/新品/爆款映射到商品域排序参数 */
const TABS = [
  { key: 'all', label: '全部商品', sort: 'default' as ProductSort },
  { key: 'new', label: '新品', sort: 'newest' as ProductSort },
  { key: 'hot', label: '爆款', sort: 'sales' as ProductSort },
]

/** 当前命中的 Tab（价格排序时回退「全部商品」） */
const activeTabKey = computed(() => {
  if (sort.value === 'newest') return 'new'
  if (sort.value === 'sales') return 'hot'
  return 'all'
})

/** 价格排序方向箭头 */
const priceSortIcon = computed(() => {
  if (sort.value === 'priceAsc') return '▲'
  if (sort.value === 'priceDesc') return '▼'
  return '▲▼'
})

/** 店铺信息（由首个在售商品推导） */
const shopBrief = computed(() => {
  const first = products.value[0]
  return first ? { name: first.shopName, logo: first.mainImage } : null
})

/** 已加载商品月销合计 */
const salesSum = computed(() => products.value.reduce((acc, p) => acc + p.sales, 0))

onMounted(() => {
  void reload()
})

// 同组件复用（店铺间互跳）时重载
watch(
  () => route.params.shopId,
  (id, prev) => {
    if (id && id !== prev) {
      followed.value = false
      void reload()
    }
  },
)

/** 重置分页并加载第一页 */
async function reload(): Promise<void> {
  const seq = ++searchSeq
  page.value = 1
  finished.value = false
  listError.value = false
  loading.value = true
  try {
    const result = await productApi.search(buildParams(1))
    if (seq !== searchSeq) return
    products.value = result.items
    total.value = result.total
    if (result.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    if (seq !== searchSeq) return
    logger.error('店铺商品加载失败', e)
    loadError.value = true
  } finally {
    if (seq === searchSeq) {
      loading.value = false
      refreshing.value = false
    }
  }
}

function buildParams(targetPage: number): {
  shopId: string
  sort: ProductSort
  page: number
  pageSize: number
} {
  return { shopId: shopId.value, sort: sort.value, page: targetPage, pageSize }
}

/** van-list 无限加载 */
async function onLoad(): Promise<void> {
  if (finished.value || loading.value) return
  const seq = searchSeq
  listLoading.value = true
  listError.value = false
  try {
    const next = await productApi.search(buildParams(page.value + 1))
    if (seq !== searchSeq) return
    products.value.push(...next.items)
    page.value += 1
    if (next.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    if (seq !== searchSeq) return
    logger.warn('店铺商品加载下一页失败', e)
    listError.value = true
  } finally {
    if (seq === searchSeq) {
      listLoading.value = false
    }
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await reload()
}

// ---- Tab / 排序切换 ----
function setTab(tab: (typeof TABS)[number]): void {
  if (sort.value === tab.sort) return
  sort.value = tab.sort
  void reload()
}

function setSortItem(next: 'default' | 'sales'): void {
  if (sort.value === next) return
  sort.value = next
  void reload()
}

function togglePriceSort(): void {
  sort.value = sort.value === 'priceAsc' ? 'priceDesc' : 'priceAsc'
  void reload()
}

// ---- 关注 ----
async function toggleFollow(): Promise<void> {
  if (followPending.value) return
  if (!authStore.isAuthenticated) {
    showToast('请先登录后关注店铺')
    router.push({ path: '/login', query: { redirect: route.fullPath } })
    return
  }
  if (followed.value) {
    followPending.value = true
    try {
      await showConfirmDialog({
        title: '取消关注',
        message: '取消后将不再接收该店铺的动态提醒，确定要取消关注吗？',
        confirmButtonText: '取消关注',
        cancelButtonText: '再想想',
      })
      followed.value = false
      showToast('已取消关注')
    } catch {
      // 用户放弃取消，保持关注态
    } finally {
      followPending.value = false
    }
    return
  }
  followed.value = true
  showToast('已关注')
}

// ---- 其它跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goHome(): void {
  router.replace('/')
}

function goCart(): void {
  router.push('/cart')
}

function goService(): void {
  showToast('客服功能即将上线')
}
</script>

<template>
  <div class="shop-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">{{ shopBrief?.name ?? '店铺详情' }}</div>
      <button class="nav-follow" :class="{ followed }" type="button" @click="toggleFollow">
        {{ followed ? '已关注' : '关注' }}
      </button>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeletons">
        <div class="skeleton-block sk-header" />
        <div class="skeleton-block sk-tabs" />
        <div class="sk-grid">
          <div v-for="i in 4" :key="i" class="sk-card">
            <div class="skeleton-block img" />
            <div class="skeleton-block l1" />
            <div class="skeleton-block l2" />
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError && products.length === 0"
        title="店铺加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="reload"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="products.length === 0"
        title="店铺暂无在售商品"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 内容 -->
      <template v-else>
        <!-- 店铺头部 -->
        <section class="shop-header">
          <div class="shop-top">
            <img class="shop-logo" :src="shopBrief?.logo" :alt="shopBrief?.name">
            <div class="shop-meta">
              <div class="shop-name">
                {{ shopBrief?.name }}
                <span class="verified" title="品牌认证" aria-label="品牌认证">
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none">
                    <path d="M12 2l2.4 1.8 3 .2.9 2.9 2.2 2-1 2.8 1 2.8-2.2 2-.9 2.9-3 .2L12 22l-2.4-1.8-3-.2-.9-2.9-2.2-2 1-2.8-1-2.8 2.2-2 .9-2.9 3-.2L12 2z" fill="#fff" />
                    <path d="M8.5 12l2.2 2.2L15.5 9.5" stroke="#1677FF" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
                  </svg>
                </span>
              </div>
              <div class="shop-desc">共 {{ total }} 件在售商品</div>
            </div>
            <button
              class="shop-follow"
              :class="{ followed }"
              type="button"
              :aria-pressed="followed"
              @click="toggleFollow"
            >
              <svg v-if="!followed" width="12" height="12" viewBox="0 0 24 24" fill="none">
                <path d="M12 5v14M5 12h14" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" />
              </svg>
              <svg v-else width="12" height="12" viewBox="0 0 24 24" fill="none">
                <path d="M6 12l4 4 8-9" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
              <span>{{ followed ? '已关注' : '关注' }}</span>
            </button>
          </div>
          <div class="shop-stats">
            <div class="stat-item">
              <div class="stat-num">{{ total }}</div>
              <div class="stat-lbl">在售商品</div>
            </div>
            <div class="stat-item">
              <div class="stat-num">{{ formatSales(salesSum) }}</div>
              <div class="stat-lbl">商品月销</div>
            </div>
          </div>
        </section>

        <!-- Tabs + 排序条 -->
        <div class="tabs-wrap">
          <div class="tabs" role="tablist" aria-label="商品范围">
            <button
              v-for="tab in TABS"
              :key="tab.key"
              class="tab"
              :class="{ active: activeTabKey === tab.key }"
              type="button"
              role="tab"
              :aria-selected="activeTabKey === tab.key"
              @click="setTab(tab)"
            >
              {{ tab.label }}
              <span v-if="tab.key === 'all'" class="badge">{{ total }}</span>
            </button>
          </div>
          <nav class="sort-bar" role="tablist" aria-label="排序方式">
            <button
              class="sort-item"
              :class="{ active: sort === 'default' }"
              type="button"
              role="tab"
              :aria-selected="sort === 'default'"
              @click="setSortItem('default')"
            >
              综合
            </button>
            <button
              class="sort-item"
              :class="{ active: sort === 'sales' }"
              type="button"
              role="tab"
              :aria-selected="sort === 'sales'"
              @click="setSortItem('sales')"
            >
              销量
            </button>
            <button
              class="sort-item"
              :class="{ active: sort === 'priceAsc' || sort === 'priceDesc' }"
              type="button"
              role="tab"
              :aria-selected="sort === 'priceAsc' || sort === 'priceDesc'"
              @click="togglePriceSort"
            >
              价格
              <span class="sort-arrows" :class="{ up: sort === 'priceAsc', down: sort === 'priceDesc' }">{{ priceSortIcon }}</span>
            </button>
          </nav>
        </div>

        <!-- 商品瀑布 -->
        <van-pull-refresh v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
          <van-list
            v-model:loading="listLoading"
            :finished="finished"
            :error="listError"
            error-text="加载失败，点击重试"
            finished-text="没有更多了"
            loading-text="加载中..."
            @load="onLoad"
          >
            <div class="product-grid">
              <ProductCard v-for="product in products" :key="product.id" :product="product" />
            </div>
          </van-list>
        </van-pull-refresh>
      </template>
    </main>

    <!-- 底部浮动条 -->
    <footer class="float-bar">
      <button class="float-btn chat" type="button" aria-label="联系客服" @click="goService">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
          <path d="M4 5a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H9l-5 4V5z" />
          <path d="M8 9h8M8 13h5" />
        </svg>
      </button>
      <button class="float-btn primary" type="button" @click="goCart">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 9l9-6 9 6v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9z" />
          <path d="M9 22V12h6v10" />
        </svg>
        <span>购物车</span>
        <span v-if="cartStore.badge > 0" class="float-badge">{{ cartStore.badge > 99 ? '99+' : cartStore.badge }}</span>
      </button>
    </footer>
  </div>
</template>

<style scoped>
.shop-page {
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
  max-width: 50%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nav-follow {
  margin-left: auto;
  font-size: var(--fs-sm);
  color: var(--c-primary);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  font-family: inherit;
}

.nav-follow.followed {
  color: var(--n7);
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  background: var(--n2);
  padding-bottom: calc(80px + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.skeletons {
  min-height: 100%;
}

.sk-header {
  height: 180px;
  border-radius: 0;
}

.sk-tabs {
  height: 84px;
  border-radius: 0;
  margin-top: 0;
}

.sk-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
  padding: var(--s3);
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
}

.sk-card .l1 {
  width: 90%;
  height: 14px;
  margin-top: var(--s2);
}

.sk-card .l2 {
  width: 50%;
  height: 16px;
  margin-top: var(--s1);
}

/* 店铺头部 */
.shop-header {
  position: relative;
  padding: 20px var(--s3) var(--s4);
  background: linear-gradient(135deg, #1677ff 0%, #0e5bd8 50%, #093ca8 100%);
  color: #fff;
  overflow: hidden;
}

.shop-header::before {
  content: "";
  position: absolute;
  top: -60px;
  right: -40px;
  width: 200px;
  height: 200px;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.18) 0%, transparent 70%);
  pointer-events: none;
}

.shop-header::after {
  content: "";
  position: absolute;
  bottom: -40px;
  left: -30px;
  width: 160px;
  height: 160px;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.12) 0%, transparent 70%);
  pointer-events: none;
}

.shop-top {
  display: flex;
  align-items: center;
  gap: var(--s3);
  position: relative;
  z-index: 1;
}

.shop-logo {
  width: 64px;
  height: 64px;
  border-radius: var(--r-card);
  background: #fff;
  border: 2px solid rgba(255, 255, 255, 0.6);
  object-fit: cover;
  flex-shrink: 0;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.shop-meta {
  flex: 1;
  min-width: 0;
}

.shop-name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: #fff;
  display: flex;
  align-items: center;
  gap: 6px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.shop-name .verified {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  background: rgba(255, 255, 255, 0.24);
  border-radius: 50%;
  flex-shrink: 0;
}

.shop-desc {
  font-size: var(--fs-sm);
  color: rgba(255, 255, 255, 0.85);
  margin-top: var(--s1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.shop-follow {
  flex-shrink: 0;
  padding: 6px 14px;
  background: #fff;
  color: var(--c-primary);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  display: flex;
  align-items: center;
  gap: var(--s1);
  cursor: pointer;
  font-family: inherit;
  transition: all 0.2s;
}

.shop-follow.followed {
  background: rgba(255, 255, 255, 0.2);
  color: #fff;
  border: 1px solid rgba(255, 255, 255, 0.5);
}

.shop-stats {
  display: flex;
  gap: var(--s6);
  margin-top: 14px;
  position: relative;
  z-index: 1;
}

.stat-item {
  display: flex;
  flex-direction: column;
}

.stat-num {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: #fff;
  line-height: 1.2;
}

.stat-lbl {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.75);
  margin-top: 2px;
}

/* Tabs + 排序条 */
.tabs-wrap {
  background: var(--n1);
  position: sticky;
  top: 0;
  z-index: 10;
}

.tabs {
  display: flex;
  height: 44px;
  border-bottom: 1px solid var(--n3);
}

.tab {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
  font-family: inherit;
  background: none;
  border: none;
  transition: color 0.2s;
}

.tab.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.tab.active::after {
  content: "";
  position: absolute;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 24px;
  height: 2px;
  background: var(--c-primary);
  border-radius: 1px;
}

.tab .badge {
  font-size: 11px;
  color: var(--n7);
}

.tab.active .badge {
  color: var(--c-primary);
}

.sort-bar {
  display: flex;
  align-items: center;
  gap: var(--s4);
  padding: 10px var(--s3);
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
}

.sort-item {
  font-size: var(--fs-sm);
  color: var(--n9);
  display: flex;
  align-items: center;
  gap: 2px;
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  font-family: inherit;
}

.sort-item.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.sort-arrows {
  font-size: 8px;
  line-height: 1;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
}

/* 商品瀑布 */
.product-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
  padding: var(--s3);
}

/* 底部浮动条 */
.float-bar {
  position: fixed;
  left: 50%;
  transform: translateX(-50%);
  bottom: calc(16px + env(safe-area-inset-bottom));
  width: calc(100% - 32px);
  max-width: 343px;
  display: flex;
  gap: var(--s2);
  z-index: 60;
}

.float-btn {
  height: 44px;
  border-radius: 22px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
  cursor: pointer;
  font-family: inherit;
}

.float-btn.primary {
  flex: 1;
  background: linear-gradient(135deg, #1677ff 0%, #0e5bd8 100%);
  color: #fff;
}

.float-btn.chat {
  flex: 0 0 44px;
  background: var(--n1);
  color: var(--n9);
  border: 1px solid var(--n3);
}

.float-badge {
  min-width: 16px;
  height: 16px;
  padding: 0 var(--s1);
  background: var(--c-error);
  color: #fff;
  font-size: 10px;
  line-height: 16px;
  border-radius: 8px;
  text-align: center;
}
</style>
