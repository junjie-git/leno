<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast, showImagePreview, showToast } from 'vant'
import { productApi } from '@/modules/03-catalog/api/product.api'
import { cartApi } from '@/modules/05-cart/api/cart.api'
import { reviewApi } from '@/modules/09-review/api/review.api'
import { favoriteApi } from '@/modules/13-profile/api/favorite.api'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import type { ProductDetailDto, ProductSkuDto } from '../types/product.dto'
import type { ReviewDto } from '@/modules/09-review/types/review.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatPrice, formatSales } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 商品详情页
 *
 * 结构（对齐设计稿 product-detail）：
 * NavBar（返回/标题/分享）→ 图片轮播（1/N 角标 + 全屏预览）
 * → 价格区（现价/划线价/省金额/近期降价标签 + 标题/副标题/月销评价库存）
 * → 规格条（已选规格 + 数量，打开 SKU 面板）
 * → Tab 区（商品详情图文 / 规格参数 / 评价摘要 + 前 2 条）
 * → 底部操作栏（客服/店铺/收藏/加购/立即购买）
 *
 * SKU 面板：规格分组选择 + 实时价格库存 + 数量步进；
 * 加购 → POST /cart/items；立即购买 → 跳确认订单页（buyNow）。
 */

const route = useRoute()
const router = useRouter()
const cartStore = useCartStore()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const notFound = ref(false)
const detail = ref<ProductDetailDto | null>(null)
const reviews = ref<ReviewDto[]>([])

const activeTab = ref(0)
const currentSwipe = ref(0)
const favorite = ref(false)
const favoriteLoading = ref(false)

// ---- SKU 面板状态 ----
const skuVisible = ref(false)
const skuMode = ref<'cart' | 'buy'>('cart')
const selectedSpecs = ref<Record<string, string>>({})
const quantity = ref(1)

/** 解析 SKU 规格（"颜色:黑;尺码:41" → 分组） */
function parseSpecs(specs: string): Array<[string, string]> {
  return specs
    .split(';')
    .map((pair) => pair.trim())
    .filter(Boolean)
    .map((pair) => {
      const idx = pair.indexOf(':')
      if (idx < 0) return ['规格', pair] as [string, string]
      return [pair.slice(0, idx), pair.slice(idx + 1)] as [string, string]
    })
}

/** 规格分组（保持 SKU 出现顺序） */
const specGroups = computed(() => {
  const groups: Array<{ name: string; values: string[] }> = []
  for (const sku of detail.value?.skus ?? []) {
    for (const [name, value] of parseSpecs(sku.specs)) {
      const group = groups.find((g) => g.name === name)
      if (!group) {
        groups.push({ name, values: [value] })
      } else if (!group.values.includes(value)) {
        group.values.push(value)
      }
    }
  }
  return groups
})

/** 当前选中规格对应的 SKU（无匹配组合时为 null） */
const currentSku = computed<ProductSkuDto | null>(() => {
  const d = detail.value
  if (!d) return null
  return (
    d.skus.find((sku) =>
      parseSpecs(sku.specs).every(([name, value]) => selectedSpecs.value[name] === value),
    ) ?? null
  )
})

/** 已选规格描述文案 */
const selectedSpecsText = computed(() => {
  const groups = specGroups.value
    .map((g) => selectedSpecs.value[g.name])
    .filter(Boolean)
    .join(' · ')
  return groups || '默认规格'
})

/** 展示价格（未选完整规格时显示价格区间起点） */
const displayPrice = computed(() => currentSku.value?.price ?? detail.value?.priceMin ?? 0)
const displayOriginal = computed(() =>
  currentSku.value?.originalPrice ?? detail.value?.skus[0]?.originalPrice ?? 0,
)
const saveAmount = computed(() => Math.max(0, displayOriginal.value - displayPrice.value))

/** 近期降价（价格历史最后两点比较） */
const priceDropped = computed(() => {
  const history = detail.value?.priceHistory ?? []
  if (history.length < 2) return false
  const last = history[history.length - 1]
  const prev = history[history.length - 2]
  return last.price < prev.price
})

/** 库存（当前 SKU 或总库存） */
const displayStock = computed(() => currentSku.value?.stock ?? detail.value?.stock ?? 0)

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  const id = String(route.params.id ?? '')
  loading.value = true
  loadError.value = false
  notFound.value = false
  try {
    const d = await productApi.getDetail(id)
    detail.value = d
    // 预选第一个有货 SKU 的规格
    const firstSku = d.skus.find((s) => s.stock > 0) ?? d.skus[0]
    if (firstSku) {
      selectedSpecs.value = Object.fromEntries(parseSpecs(firstSku.specs))
    }
    quantity.value = 1
    // 评价摘要前 2 条（失败静默，评价区隐藏）
    try {
      const result = await reviewApi.listProductReviews(id, { page: 1, pageSize: 2 })
      reviews.value = result.items
    } catch (e) {
      logger.warn('商品评价加载失败（忽略）', e)
      reviews.value = []
    }
    // 收藏态（失败静默按未收藏处理）
    try {
      const list = await favoriteApi.list()
      favorite.value = list.some((x) => x.spuId === id)
    } catch (e) {
      logger.warn('收藏态查询失败（忽略）', e)
      favorite.value = false
    }
  } catch (e) {
    logger.error('商品详情加载失败', e)
    if (e instanceof Error && e.message.includes('下架')) {
      notFound.value = true
    } else {
      loadError.value = true
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadAll()
})

// 同组件复用（商品卡互跳）时重载
watch(
  () => route.params.id,
  (id, prev) => {
    if (id && id !== prev) {
      void loadAll()
    }
  },
)

// ---- 轮播 ----
function onSwipeChange(index: number): void {
  currentSwipe.value = index
}

function previewImage(index: number): void {
  const images = detail.value?.images ?? []
  if (images.length === 0) return
  showImagePreview({ images, startPosition: index })
}

// ---- 分享 ----
async function share(): Promise<void> {
  const text = detail.value ? `${detail.value.name} - Leno 买家端` : 'Leno 买家端'
  const url = window.location.href
  if (navigator.share) {
    try {
      await navigator.share({ title: 'Leno 买家端', text, url })
    } catch {
      // 用户取消分享，无需处理
    }
    return
  }
  try {
    await navigator.clipboard.writeText(`${text} ${url}`)
    showToast('链接已复制')
  } catch {
    showToast('复制链接失败')
  }
}

// ---- 收藏 ----
async function toggleFavorite(): Promise<void> {
  if (!detail.value || favoriteLoading.value) return
  favoriteLoading.value = true
  try {
    if (favorite.value) {
      await favoriteApi.remove(detail.value.id)
      favorite.value = false
      showToast('已取消收藏')
    } else {
      await favoriteApi.add(detail.value.id)
      favorite.value = true
      showToast('已收藏')
    }
  } catch (e) {
    logger.warn('收藏操作失败', e)
    showFailToast(e instanceof Error ? e.message : '操作失败，请稍后重试')
  } finally {
    favoriteLoading.value = false
  }
}

// ---- SKU 面板 ----
function openSku(mode: 'cart' | 'buy'): void {
  skuMode.value = mode
  skuVisible.value = true
}

function selectSpec(groupName: string, value: string): void {
  selectedSpecs.value = { ...selectedSpecs.value, [groupName]: value }
}

/** 规格值是否可选（存在含该值的 SKU） */
function specValueAvailable(groupName: string, value: string): boolean {
  return (detail.value?.skus ?? []).some((sku) =>
    parseSpecs(sku.specs).some(([n, v]) => n === groupName && v === value),
  )
}

async function confirmSku(): Promise<void> {
  const sku = currentSku.value
  if (!sku) {
    showToast('该规格组合暂无现货')
    return
  }
  if (sku.stock <= 0) {
    showToast('该规格已无货')
    return
  }
  if (skuMode.value === 'cart') {
    try {
      await cartApi.addItem({ skuId: sku.id, quantity: quantity.value })
      await cartStore.refreshBadge()
      skuVisible.value = false
      showToast('已加入购物车')
    } catch (e) {
      logger.warn('加购失败', e)
      showFailToast(e instanceof Error ? e.message : '加购失败，请稍后重试')
    }
    return
  }
  // 立即购买 → 确认订单页（buyNow 单 SKU）
  skuVisible.value = false
  router.push({
    path: '/order/create',
    query: { from: 'buyNow', skuId: sku.id, quantity: String(quantity.value) },
  })
}

// ---- 其它跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goShop(): void {
  if (detail.value) {
    router.push(`/shop/${detail.value.shopId}`)
  }
}

function goService(): void {
  showToast('客服功能即将上线')
}

function goAllReviews(): void {
  if (detail.value) {
    router.push(`/product/${detail.value.id}/reviews`)
  }
}

function goHome(): void {
  router.replace('/')
}

/** 评分 → ★ 字符串 */
function stars(rating: number): string {
  return '★★★★★'.slice(0, Math.max(0, Math.min(5, rating)))
}
</script>

<template>
  <div class="detail-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">商品详情</div>
      <div class="nav-actions">
        <button type="button" aria-label="分享" @click="share">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
            <path d="M4 12v6a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-6" />
            <path d="M16 6l-4-4-4 4M12 2v13" />
          </svg>
        </button>
      </div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-carousel" />
      <div class="sk-price-area">
        <div class="skeleton-block sk-price" />
        <div class="skeleton-block sk-l1" />
        <div class="skeleton-block sk-l2" />
      </div>
      <div class="skeleton-block sk-spec" />
      <div class="skeleton-block sk-tab" />
      <div class="skeleton-block sk-detail" />
    </main>

    <!-- 错误 / 下架态 -->
    <main v-else-if="loadError || notFound || !detail" class="body">
      <ErrorState
        :title="notFound ? '商品已下架' : '商品信息加载失败'"
        :description="notFound ? '该商品不存在或已下架，去看看其他商品吧' : '网络异常，请稍后重试'"
        :retry-text="notFound ? '返回首页' : '重新加载'"
        @retry="notFound ? goHome() : loadAll()"
      />
    </main>

    <!-- 内容 -->
    <main v-else class="body">
      <!-- 图片轮播 -->
      <div class="carousel" role="region" aria-label="商品图片轮播">
        <van-swipe class="swipe" :show-indicators="false" @change="onSwipeChange">
          <van-swipe-item v-for="(img, index) in detail.images" :key="index" @click="previewImage(index)">
            <img class="carousel-img" :src="img" :alt="`${detail.name} 图 ${index + 1}`" loading="lazy">
          </van-swipe-item>
        </van-swipe>
        <div class="carousel-tag">{{ currentSwipe + 1 }}/{{ detail.images.length }}</div>
      </div>

      <!-- 价格区 -->
      <section class="price-area">
        <div class="price-row">
          <PriceText :amount="displayPrice" :size="24" />
          <span v-if="displayOriginal > displayPrice" class="price-old">¥{{ formatPrice(displayOriginal) }}</span>
          <span v-if="saveAmount > 0" class="save-tag">省 ¥{{ formatPrice(saveAmount) }}</span>
          <span v-if="priceDropped" class="drop-tag">近期降价</span>
        </div>
        <h1 class="prod-title">{{ detail.name }}</h1>
        <p class="prod-sub">{{ detail.subtitle }}</p>
        <div class="prod-meta">
          <span>月销 {{ formatSales(detail.sales) }}</span>
          <span class="sep">|</span>
          <span>累计评价 {{ detail.reviewSummary.count }}</span>
          <span class="sep">|</span>
          <span>库存 {{ detail.stock }}</span>
        </div>
        <div v-if="detail.tags.length > 0" class="prod-tags">
          <span v-for="tag in detail.tags" :key="tag" class="tag">{{ tag }}</span>
        </div>
      </section>

      <!-- 规格条 -->
      <button class="spec-bar" type="button" @click="openSku('cart')">
        <span class="lbl">已选</span>
        <span class="val">{{ selectedSpecsText }} · {{ quantity }}件</span>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M9 6l6 6-6 6" />
        </svg>
      </button>

      <!-- Tab 区 -->
      <section class="detail-section">
        <div class="tabs" role="tablist">
          <div
            class="tab"
            :class="{ active: activeTab === 0 }"
            role="tab"
            :aria-selected="activeTab === 0"
            @click="activeTab = 0"
          >
            商品详情
          </div>
          <div
            class="tab"
            :class="{ active: activeTab === 1 }"
            role="tab"
            :aria-selected="activeTab === 1"
            @click="activeTab = 1"
          >
            规格参数
          </div>
          <div
            class="tab"
            :class="{ active: activeTab === 2 }"
            role="tab"
            :aria-selected="activeTab === 2"
            @click="activeTab = 2"
          >
            评价<span class="cnt">（{{ detail.reviewSummary.count }}）</span>
          </div>
        </div>

        <!-- 商品详情 -->
        <div v-if="activeTab === 0" class="detail-imgs">
          <p class="desc-text">{{ detail.description }}</p>
          <img
            v-for="(img, index) in detail.images"
            :key="index"
            class="detail-img"
            :src="img"
            :alt="`${detail.name} 详情图 ${index + 1}`"
            loading="lazy"
          >
        </div>

        <!-- 规格参数 -->
        <div v-else-if="activeTab === 1" class="attr-list">
          <div v-for="attr in detail.attributes" :key="attr.name" class="attr-row">
            <span class="k">{{ attr.name }}</span>
            <span class="v">{{ attr.value }}</span>
          </div>
          <div class="attr-row">
            <span class="k">品牌</span>
            <span class="v">{{ detail.brandName }}</span>
          </div>
          <div class="attr-row">
            <span class="k">分类</span>
            <span class="v">{{ detail.categoryName }}</span>
          </div>
        </div>

        <!-- 评价 -->
        <div v-else class="review-sec">
          <template v-if="reviews.length > 0">
            <div class="rev-head">
              <div class="rev-score">
                <span class="num">{{ detail.reviewSummary.averageRating.toFixed(1) }}</span>
                <span class="lbl">分</span>
                <span class="good-rate">好评率 {{ detail.reviewSummary.goodRate }}%</span>
              </div>
              <button class="rev-all" type="button" @click="goAllReviews">
                查看全部 {{ detail.reviewSummary.count }} 条
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M9 6l6 6-6 6" />
                </svg>
              </button>
            </div>
            <div v-for="review in reviews" :key="review.id" class="rev-item">
              <div class="rev-top">
                <div class="rev-avatar">{{ review.nickname.charAt(0) }}</div>
                <span class="rev-name">{{ review.nickname }}</span>
                <span class="rev-stars">{{ stars(review.rating) }}</span>
              </div>
              <div class="rev-content">{{ review.content }}</div>
              <div class="rev-spec">规格：{{ review.skuSpecs }}</div>
            </div>
          </template>
          <div v-else class="rev-empty">暂无评价，购买后快来抢首评吧</div>
        </div>
      </section>
    </main>

    <!-- 底部操作栏 -->
    <footer v-if="detail" class="action-bar">
      <button class="act-icon" type="button" @click="goService">
        <span class="ic">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
            <path d="M4 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z" />
            <path d="M8 9h8M8 13h5" />
            <circle cx="18" cy="18" r="1.5" />
          </svg>
        </span>
        <span>客服</span>
      </button>
      <button class="act-icon" type="button" @click="goShop">
        <span class="ic">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
            <path d="M4 9l1-4h14l1 4" />
            <path d="M4 9v10a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
            <path d="M4 9h16M9 13h6" />
          </svg>
        </span>
        <span>店铺</span>
      </button>
      <button class="act-icon" :class="{ fav: favorite }" type="button" :aria-pressed="favorite" @click="toggleFavorite">
        <span class="ic">
          <svg width="22" height="22" viewBox="0 0 24 24" :fill="favorite ? 'currentColor' : 'none'" stroke="currentColor" stroke-width="1.4" stroke-linejoin="round">
            <path d="M12 21s-7-4.5-9.5-9C1 9 2.5 5.5 6 5.5c2 0 3.2 1.2 4 2.3.8-1.1 2-2.3 4-2.3 3.5 0 5 3.5 3.5 6.5C19 16.5 12 21 12 21z" />
          </svg>
        </span>
        <span>{{ favorite ? '已收藏' : '收藏' }}</span>
      </button>
      <button class="btn-cart" type="button" @click="openSku('cart')">加入购物车</button>
      <button class="btn-buy" type="button" @click="openSku('buy')">立即购买</button>
    </footer>

    <!-- SKU 面板 -->
    <van-popup
      v-model:show="skuVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="规格选择面板"
    >
      <div v-if="detail" class="sku-panel">
        <!-- 面板头部：图 + 价格 + 库存 -->
        <div class="sku-head">
          <img class="sku-img" :src="currentSku?.image ?? detail.mainImage" :alt="detail.name">
          <div class="sku-info">
            <PriceText :amount="displayPrice" :size="20" />
            <div class="sku-stock">库存 {{ displayStock }} 件</div>
            <div class="sku-selected">已选：{{ selectedSpecsText }}</div>
          </div>
          <van-icon name="cross" size="18" color="#8C8C8C" class="sku-close" @click="skuVisible = false" />
        </div>

        <!-- 规格分组 -->
        <div class="sku-body">
          <div v-for="group in specGroups" :key="group.name" class="spec-group">
            <div class="group-name">{{ group.name }}</div>
            <div class="value-row">
              <button
                v-for="value in group.values"
                :key="value"
                class="value-chip"
                :class="{
                  on: selectedSpecs[group.name] === value,
                  disabled: !specValueAvailable(group.name, value),
                }"
                type="button"
                :disabled="!specValueAvailable(group.name, value)"
                @click="selectSpec(group.name, value)"
              >
                {{ value }}
              </button>
            </div>
          </div>

          <!-- 数量 -->
          <div class="qty-row">
            <span class="group-name">数量</span>
            <van-stepper
              v-model="quantity"
              :min="1"
              :max="Math.max(1, displayStock)"
              :disable-input="true"
            />
          </div>
        </div>

        <!-- 确认按钮 -->
        <div class="sku-foot">
          <button
            class="btn-confirm"
            :class="{ disabled: !currentSku || currentSku.stock <= 0 }"
            type="button"
            :disabled="!currentSku || currentSku.stock <= 0"
            @click="confirmSku"
          >
            {{ !currentSku ? '该规格组合暂无现货' : currentSku.stock <= 0 ? '已无货' : skuMode === 'cart' ? '加入购物车' : '立即购买' }}
          </button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.detail-page {
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

.nav-actions {
  margin-left: auto;
  display: flex;
  align-items: center;
}

.nav-actions button {
  display: flex;
  align-items: center;
  background: none;
  border: none;
  padding: 0;
  color: var(--n10);
  cursor: pointer;
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  background: var(--n2);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.sk-carousel {
  width: 100%;
  height: 345px;
  border-radius: 0;
}

.sk-price-area {
  background: var(--n1);
  padding: var(--s4) var(--s3);
}

.sk-price {
  width: 40%;
  height: 26px;
}

.sk-l1 {
  width: 90%;
  height: 16px;
  margin-top: var(--s3);
}

.sk-l2 {
  width: 60%;
  height: 14px;
  margin-top: var(--s2);
}

.sk-spec {
  height: 48px;
  margin-top: var(--s3);
  border-radius: 0;
}

.sk-tab {
  height: 44px;
  margin-top: var(--s3);
  border-radius: 0;
}

.sk-detail {
  height: 200px;
  margin-top: var(--s2);
  border-radius: 0;
}

/* 轮播 */
.carousel {
  position: relative;
  background: var(--n1);
}

.swipe {
  height: 345px;
}

.carousel-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.carousel-tag {
  position: absolute;
  bottom: 12px;
  right: 12px;
  background: rgba(0, 0, 0, 0.5);
  color: #fff;
  font-size: var(--fs-sm);
  padding: 3px var(--s2);
  border-radius: var(--r-base);
}

/* 价格区 */
.price-area {
  background: var(--n1);
  padding: var(--s4) var(--s3);
}

.price-row {
  display: flex;
  align-items: baseline;
  gap: var(--s2);
  flex-wrap: wrap;
}

.price-old {
  font-size: var(--fs-base);
  color: var(--n7);
  text-decoration: line-through;
}

.save-tag {
  font-size: var(--fs-sm);
  color: var(--c-error);
  background: #fff1f0;
  border: 1px solid #ffccc7;
  padding: 2px var(--s1);
  border-radius: var(--r-base);
}

.drop-tag {
  font-size: var(--fs-sm);
  color: var(--c-warning);
  background: #fff7e6;
  border: 1px solid #ffe7ba;
  padding: 2px var(--s1);
  border-radius: var(--r-base);
}

.prod-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
  line-height: 1.4;
  margin-top: var(--s3);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.prod-sub {
  font-size: var(--fs-base);
  color: var(--n9);
  margin-top: var(--s1);
}

.prod-meta {
  display: flex;
  align-items: center;
  gap: var(--s3);
  margin-top: var(--s3);
  font-size: var(--fs-sm);
  color: var(--n7);
}

.prod-meta .sep {
  color: var(--n5);
}

.prod-tags {
  display: flex;
  gap: var(--s1);
  margin-top: var(--s2);
}

.prod-tags .tag {
  font-size: 10px;
  padding: 1px 5px;
  border-radius: var(--r-base);
  background: #fff1f0;
  color: var(--c-error);
}

/* 规格条 */
.spec-bar {
  width: 100%;
  background: var(--n1);
  margin-top: var(--s3);
  padding: var(--s3);
  display: flex;
  align-items: center;
  gap: var(--s2);
  cursor: pointer;
  border: none;
  font-family: inherit;
  text-align: left;
}

.spec-bar .lbl {
  font-size: var(--fs-base);
  color: var(--n10);
  flex-shrink: 0;
}

.spec-bar .val {
  font-size: var(--fs-base);
  color: var(--n9);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.spec-bar svg {
  color: var(--n7);
  flex-shrink: 0;
}

/* Tab 区 */
.detail-section {
  background: var(--n1);
  margin-top: var(--s3);
}

.tabs {
  display: flex;
  border-bottom: 1px solid var(--n3);
  position: sticky;
  top: 0;
  background: var(--n1);
  z-index: 2;
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
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 24px;
  height: 2px;
  background: var(--c-primary);
  border-radius: 1px;
}

.tab .cnt {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.tab.active .cnt {
  color: var(--c-primary);
}

/* 商品详情 */
.detail-imgs {
  padding: var(--s3);
}

.desc-text {
  font-size: var(--fs-base);
  color: var(--n9);
  line-height: 1.7;
  margin-bottom: var(--s3);
}

.detail-img {
  width: 100%;
  border-radius: var(--r-base);
  margin-bottom: var(--s2);
  display: block;
}

/* 规格参数 */
.attr-list {
  padding: var(--s3);
}

.attr-row {
  display: flex;
  padding: 10px 0;
  border-bottom: 1px solid var(--n3);
  font-size: var(--fs-base);
}

.attr-row:last-child {
  border-bottom: none;
}

.attr-row .k {
  width: 88px;
  flex-shrink: 0;
  color: var(--n7);
}

.attr-row .v {
  flex: 1;
  color: var(--n10);
}

/* 评价区 */
.review-sec {
  padding: var(--s3);
}

.rev-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.rev-score {
  display: flex;
  align-items: baseline;
  gap: var(--s1);
}

.rev-score .num {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--c-warning);
}

.rev-score .lbl {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.rev-score .good-rate {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-left: var(--s2);
}

.rev-all {
  font-size: var(--fs-base);
  color: var(--n9);
  display: flex;
  align-items: center;
  gap: 2px;
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  padding: 0;
}

.rev-item {
  padding: var(--s3) 0;
  border-top: 1px solid var(--n3);
}

.rev-top {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s1);
}

.rev-avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--c-primary);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-sm);
  flex-shrink: 0;
}

.rev-name {
  font-size: var(--fs-sm);
  color: var(--n9);
  flex: 1;
}

.rev-stars {
  color: var(--c-warning);
  font-size: var(--fs-sm);
}

.rev-content {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.5;
}

.rev-spec {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
}

.rev-empty {
  padding: var(--s6) 0;
  text-align: center;
  font-size: var(--fs-base);
  color: var(--n7);
}

/* 底部操作栏 */
.action-bar {
  height: 50px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 0 var(--s2);
  gap: var(--s2);
  padding-bottom: env(safe-area-inset-bottom);
}

.act-icon {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1px;
  color: var(--n9);
  width: 44px;
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  font-size: 10px;
  flex-shrink: 0;
  padding: 0;
}

.act-icon .ic {
  display: flex;
  align-items: center;
  height: 22px;
}

.act-icon.fav {
  color: var(--c-error);
}

.btn-cart {
  flex: 1;
  height: 36px;
  border-radius: 18px 0 0 18px;
  border: 1.5px solid var(--c-primary);
  background: #fff;
  color: var(--c-primary);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
}

.btn-buy {
  flex: 1;
  height: 36px;
  border-radius: 0 18px 18px 0;
  border: 1.5px solid var(--c-primary);
  background: var(--c-primary);
  color: #fff;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
}

/* SKU 面板 */
.sku-panel {
  padding: var(--s4) var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
  max-height: 75vh;
  display: flex;
  flex-direction: column;
}

.sku-head {
  display: flex;
  gap: var(--s3);
  position: relative;
}

.sku-img {
  width: 96px;
  height: 96px;
  border-radius: var(--r-card);
  object-fit: cover;
  background: var(--n3);
  flex-shrink: 0;
}

.sku-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: var(--s1);
}

.sku-stock {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.sku-selected {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.sku-close {
  position: absolute;
  top: 0;
  right: 0;
  cursor: pointer;
}

.sku-body {
  flex: 1;
  overflow-y: auto;
  margin-top: var(--s4);
}

.spec-group {
  margin-bottom: var(--s4);
}

.group-name {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  margin-bottom: var(--s2);
}

.value-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}

.value-chip {
  background: var(--n3);
  border: 1px solid transparent;
  border-radius: var(--r-lg);
  padding: 6px var(--s3);
  font-size: var(--fs-base);
  color: var(--n9);
  cursor: pointer;
  font-family: inherit;
}

.value-chip.on {
  background: #e6f4ff;
  border-color: var(--c-primary);
  color: var(--c-primary);
}

.value-chip.disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.qty-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s2) 0 var(--s4);
}

.sku-foot {
  margin-top: var(--s2);
}

.btn-confirm {
  width: 100%;
  height: 40px;
  border-radius: 20px;
  border: none;
  background: var(--c-primary);
  color: #fff;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
}

.btn-confirm.disabled {
  background: var(--n5);
  cursor: not-allowed;
}
</style>
