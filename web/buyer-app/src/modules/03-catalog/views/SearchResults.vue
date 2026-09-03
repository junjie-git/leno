<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { productApi } from '@/modules/03-catalog/api/product.api'
import { categoryApi, brandApi } from '@/modules/03-catalog/api/category.api'
import type { BrandDto, CategoryDto, ProductSort, ProductSummaryDto } from '@/modules/03-catalog/types/product.dto'
import ProductCard from '@/shared/components/ProductCard.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 搜索结果页
 *
 * 结构（对齐设计稿 search-results）：
 * 顶部搜索栏（返回 + 关键词可改 + 搜索）→ 排序/筛选栏（综合/销量/价格升降/筛选）
 * → 双列瀑布商品列表（van-list 无限加载 + 下拉刷新）
 *
 * 筛选面板（底部弹层）：分类（一级树）+ 品牌，确定后重新加载。
 * 排序/筛选切换会重置分页；请求序号守卫防止旧响应覆盖新结果。
 */

const route = useRoute()
const router = useRouter()

const pageSize = 10

// ---- 搜索条件 ----
const keywordInput = ref('')
const keyword = ref('')
const categoryId = ref('')
const brandId = ref('')
const sort = ref<ProductSort>('default')

// ---- 筛选面板 ----
const filterVisible = ref(false)
const categories = ref<CategoryDto[]>([])
const brands = ref<BrandDto[]>([])
const draftCategoryId = ref('')
const draftBrandId = ref('')

// ---- 列表状态 ----
const firstLoading = ref(true)
const products = ref<ProductSummaryDto[]>([])
const total = ref(0)
const page = ref(1)
const finished = ref(false)
const listLoading = ref(false)
const listError = ref(false)
const refreshing = ref(false)

/** 搜索请求序号（切换条件时旧响应作废） */
let searchSeq = 0

/** 是否存在筛选条件（空态 CTA 用） */
const hasFilter = computed(() => categoryId.value !== '' || brandId.value !== '')

/** 价格排序方向图标 */
const priceSortIcon = computed(() => {
  if (sort.value === 'priceAsc') return '▲'
  if (sort.value === 'priceDesc') return '▼'
  return '▲▼'
})

/** 选中分类名（筛选栏展示） */
const selectedCategoryName = computed(
  () => categories.value.find((c) => c.id === categoryId.value)?.name ?? '',
)

/** 选中品牌名 */
const selectedBrandName = computed(() => brands.value.find((b) => b.id === brandId.value)?.name ?? '')

onMounted(async () => {
  keywordInput.value = typeof route.query.keyword === 'string' ? route.query.keyword : ''
  keyword.value = keywordInput.value
  if (typeof route.query.categoryId === 'string') {
    categoryId.value = route.query.categoryId
  }
  if (typeof route.query.brandId === 'string') {
    brandId.value = route.query.brandId
  }
  // 筛选面板数据（分类树 + 品牌，失败静默降级）
  try {
    const [tree, brandList] = await Promise.all([categoryApi.getTree(), brandApi.list()])
    categories.value = tree
    brands.value = brandList
  } catch (e) {
    logger.warn('筛选面板数据加载失败（忽略）', e)
  }
  await reload()
})

// 从搜索页再次进入（同路由不同 query）时重新加载
watch(
  () => route.query,
  () => {
    if (route.name !== 'catalog.searchResults') return
    keywordInput.value = typeof route.query.keyword === 'string' ? route.query.keyword : ''
    keyword.value = keywordInput.value
    categoryId.value = typeof route.query.categoryId === 'string' ? route.query.categoryId : ''
    brandId.value = typeof route.query.brandId === 'string' ? route.query.brandId : ''
    void reload()
  },
)

/** 重置分页并加载第一页 */
async function reload(): Promise<void> {
  const seq = ++searchSeq
  page.value = 1
  finished.value = false
  listError.value = false
  firstLoading.value = true
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
    logger.error('搜索失败', e)
    listError.value = true
  } finally {
    if (seq === searchSeq) {
      firstLoading.value = false
      refreshing.value = false
    }
  }
}

function buildParams(targetPage: number): {
  keyword?: string
  categoryId?: string
  brandId?: string
  sort: ProductSort
  page: number
  pageSize: number
} {
  return {
    keyword: keyword.value || undefined,
    categoryId: categoryId.value || undefined,
    brandId: brandId.value || undefined,
    sort: sort.value,
    page: targetPage,
    pageSize,
  }
}

/** van-list 无限加载 */
async function onLoad(): Promise<void> {
  if (finished.value || firstLoading.value) return
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
    logger.warn('搜索结果加载失败', e)
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

// ---- 排序切换 ----
function setSort(next: 'default' | 'sales'): void {
  if (sort.value === next) return
  sort.value = next
  void reload()
}

function togglePriceSort(): void {
  if (sort.value === 'priceAsc') {
    sort.value = 'priceDesc'
  } else {
    sort.value = 'priceAsc'
  }
  void reload()
}

// ---- 筛选面板 ----
function openFilter(): void {
  draftCategoryId.value = categoryId.value
  draftBrandId.value = brandId.value
  filterVisible.value = true
}

function applyFilter(): void {
  categoryId.value = draftCategoryId.value
  brandId.value = draftBrandId.value
  filterVisible.value = false
  void reload()
}

function resetFilter(): void {
  draftCategoryId.value = ''
  draftBrandId.value = ''
}

function clearAllFilter(): void {
  categoryId.value = ''
  brandId.value = ''
  void reload()
}

// ---- 搜索动作 ----
function doSearch(): void {
  const w = keywordInput.value.trim()
  if (!w) return
  keyword.value = w
  router.replace({ path: '/search/results', query: { keyword: w } })
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}
</script>

<template>
  <div class="results-page">
    <!-- 顶部搜索栏 -->
    <header class="search-top">
      <button class="back-btn" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="search-input">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <circle cx="11" cy="11" r="7" />
          <path d="M21 21l-4.3-4.3" />
        </svg>
        <input
          v-model="keywordInput"
          type="text"
          aria-label="搜索关键词"
          @keydown.enter="doSearch"
        >
      </div>
      <button class="search-btn" type="button" @click="doSearch">搜索</button>
    </header>

    <!-- 排序 / 筛选栏 -->
    <nav class="sortbar" role="tablist" aria-label="排序筛选">
      <div
        class="sort-item"
        :class="{ active: sort === 'default' }"
        role="tab"
        :aria-selected="sort === 'default'"
        @click="setSort('default')"
      >
        综合
      </div>
      <div
        class="sort-item"
        :class="{ active: sort === 'sales' }"
        role="tab"
        :aria-selected="sort === 'sales'"
        @click="setSort('sales')"
      >
        销量
      </div>
      <div
        class="sort-item"
        :class="{ active: sort === 'priceAsc' || sort === 'priceDesc' }"
        role="tab"
        :aria-selected="sort === 'priceAsc' || sort === 'priceDesc'"
        @click="togglePriceSort"
      >
        价格
        <span class="sort-arrows" :class="{ up: sort === 'priceAsc', down: sort === 'priceDesc' }">{{ priceSortIcon }}</span>
      </div>
      <div class="sort-item sort-filter" :class="{ active: hasFilter }" role="button" @click="openFilter">
        筛选
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
          <path d="M4 6h16M7 12h10M10 18h4" />
        </svg>
      </div>
    </nav>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="waterfall">
        <div v-for="i in 6" :key="i" class="sk-card">
          <div class="skeleton-block img" />
          <div class="skeleton-block l1" />
          <div class="skeleton-block l2" />
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="listError && products.length === 0"
        title="搜索失败"
        description="网络异常，请稍后重试"
        @retry="reload"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="products.length === 0"
        title="未找到相关商品"
        :action-text="hasFilter ? '清空筛选条件' : '逛逛热搜好物'"
        @action="hasFilter ? clearAllFilter() : doSearch()"
      />

      <!-- 商品列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <van-list
          v-model:loading="listLoading"
          :finished="finished"
          :error="listError"
          error-text="加载失败，点击重试"
          finished-text="没有更多了"
          loading-text="加载中..."
          @load="onLoad"
        >
          <div class="result-meta">
            <span>共 {{ total }} 件相关商品</span>
            <span v-if="selectedCategoryName" class="meta-chip">分类：{{ selectedCategoryName }}</span>
            <span v-if="selectedBrandName" class="meta-chip">品牌：{{ selectedBrandName }}</span>
          </div>
          <div class="waterfall">
            <ProductCard v-for="product in products" :key="product.id" :product="product" />
          </div>
        </van-list>
      </van-pull-refresh>
    </div>

    <!-- 筛选面板（底部弹层） -->
    <van-popup
      v-model:show="filterVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="筛选面板"
      :style="{ maxHeight: '70%' }"
    >
      <div class="filter-panel">
        <div class="filter-head">
          <span class="t">筛选</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="filterVisible = false" />
        </div>

        <div class="filter-body">
          <!-- 分类 -->
          <div class="filter-sec">
            <div class="sec-title">分类</div>
            <div class="chip-row">
              <button class="chip" :class="{ on: draftCategoryId === '' }" type="button" @click="draftCategoryId = ''">
                全部
              </button>
              <button
                v-for="cat in categories"
                :key="cat.id"
                class="chip"
                :class="{ on: draftCategoryId === cat.id }"
                type="button"
                @click="draftCategoryId = cat.id"
              >
                {{ cat.name }}
              </button>
            </div>
          </div>

          <!-- 品牌 -->
          <div class="filter-sec">
            <div class="sec-title">品牌</div>
            <div class="chip-row">
              <button class="chip" :class="{ on: draftBrandId === '' }" type="button" @click="draftBrandId = ''">
                全部
              </button>
              <button
                v-for="brand in brands"
                :key="brand.id"
                class="chip"
                :class="{ on: draftBrandId === brand.id }"
                type="button"
                @click="draftBrandId = brand.id"
              >
                {{ brand.name }}
              </button>
            </div>
          </div>
        </div>

        <div class="filter-foot">
          <van-button plain type="primary" class="foot-btn" @click="resetFilter">重置</van-button>
          <van-button type="primary" class="foot-btn" @click="applyFilter">确定</van-button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.results-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n2);
}

/* 顶部搜索栏 */
.search-top {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s2);
  flex-shrink: 0;
}

.back-btn {
  display: flex;
  align-items: center;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}

.search-input {
  flex: 1;
  height: 32px;
  background: var(--n3);
  border-radius: 16px;
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s1);
  color: var(--n7);
}

.search-input input {
  border: none;
  background: transparent;
  outline: none;
  flex: 1;
  font-size: var(--fs-base);
  font-family: inherit;
  color: var(--n10);
  min-width: 0;
}

.search-btn {
  font-size: var(--fs-base);
  color: var(--c-primary);
  font-weight: var(--fw-medium);
  background: none;
  border: none;
  cursor: pointer;
  padding: 0 var(--s1);
  font-family: inherit;
  flex-shrink: 0;
}

/* 排序 / 筛选栏 */
.sortbar {
  height: 40px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.sort-item {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  font-size: var(--fs-base);
  color: var(--n9);
  cursor: pointer;
  position: relative;
}

.sort-item.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.sort-item.active::after {
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

.sort-arrows {
  font-size: 8px;
  line-height: 1;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
}

.sort-arrows.up {
  color: var(--c-primary);
}

.sort-arrows.down {
  color: var(--c-primary);
}

.sort-filter {
  color: var(--n9);
}

.sort-filter.active {
  color: var(--c-primary);
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s2);
  background: var(--n2);
}

.result-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--s2);
  padding: var(--s1) var(--s2) var(--s2);
  font-size: var(--fs-sm);
  color: var(--n7);
}

.meta-chip {
  background: var(--n1);
  border-radius: var(--r-base);
  padding: 2px var(--s2);
  color: var(--c-primary);
}

/* 瀑布流 */
.waterfall {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
}

/* 骨架屏 */
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
  width: 90%;
  height: 14px;
  margin-top: var(--s2);
}

.sk-card .l2 {
  width: 50%;
  height: 16px;
  margin-top: var(--s1);
}

/* 筛选面板 */
.filter-panel {
  display: flex;
  flex-direction: column;
  max-height: 70vh;
}

.filter-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s4) var(--s4) var(--s2);
}

.filter-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.filter-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 var(--s4);
}

.filter-sec {
  margin-bottom: var(--s4);
}

.filter-sec .sec-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n9);
  margin-bottom: var(--s2);
}

.chip-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}

.chip {
  background: var(--n3);
  border: 1px solid transparent;
  border-radius: var(--r-lg);
  padding: 6px var(--s3);
  font-size: var(--fs-base);
  color: var(--n9);
  cursor: pointer;
  font-family: inherit;
}

.chip.on {
  background: #e6f4ff;
  border-color: var(--c-primary);
  color: var(--c-primary);
}

.filter-foot {
  display: flex;
  gap: var(--s2);
  padding: var(--s3) var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
}

.foot-btn {
  flex: 1;
}
</style>
