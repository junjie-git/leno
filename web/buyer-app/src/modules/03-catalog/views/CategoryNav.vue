<script setup lang="ts">
import { computed, onActivated, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { categoryApi } from '@/modules/03-catalog/api/category.api'
import { productApi } from '@/modules/03-catalog/api/product.api'
import type { CategoryDto, ProductSummaryDto } from '@/modules/03-catalog/types/product.dto'
import ProductCard from '@/shared/components/ProductCard.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 分类导航页（Tabbar 分类入口，KeepAlive 缓存）
 *
 * 结构（对齐设计稿 category-nav）：
 * 顶部搜索框 → 左侧一级分类 sidebar（80px，激活态左侧蓝条）
 * → 右侧二级分类网格 + 为你推荐瀑布流（按当前选中分类过滤）
 *
 * 支持 /category?categoryId= 深链：一级或二级分类 id 均可定位。
 */

const route = useRoute()
const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const categories = ref<CategoryDto[]>([])
const activeTopId = ref('')
const selectedSubId = ref('')

const productLoading = ref(false)
const productError = ref(false)
const products = ref<ProductSummaryDto[]>([])

// ---- 二级分类图标映射（按名称关键词匹配，兜底 apps-o） ----
const SUB_ICON_RULES: Array<{ keywords: string[]; icon: string; color: string; bg: string }> = [
  { keywords: ['手机'], icon: 'phone-o', color: '#1677FF', bg: '#E6F4FF' },
  { keywords: ['智能穿戴', '手环', '手表'], icon: 'clock-o', color: '#13C2C2', bg: '#E6FFFB' },
  { keywords: ['耳机', '音箱', '影音'], icon: 'music-o', color: '#722ED1', bg: '#F9F0FF' },
  { keywords: ['男装', '女装', '内衣'], icon: 'shirt-o', color: '#FF4D4F', bg: '#FFF1F0' },
  { keywords: ['零食', '坚果'], icon: 'gift-o', color: '#FAAD14', bg: '#FFF7E6' },
  { keywords: ['乳品', '饮料', '粮油', '调味'], icon: 'shopping-cart-o', color: '#52C41A', bg: '#F6FFED' },
  { keywords: ['面护', '面部', '个护', '美妆'], icon: 'gem-o', color: '#EB2F96', bg: '#FFF0F6' },
  { keywords: ['家清', '纸品', '厨具', '厨房', '家纺', '家居'], icon: 'home-o', color: '#2F54EB', bg: '#F0F5FF' },
  { keywords: ['运动', '户外'], icon: 'fitness-o', color: '#13C2C2', bg: '#E6FFFB' },
  { keywords: ['玩具', '积木', '图书', '文具'], icon: 'gift-card-o', color: '#FAAD14', bg: '#FFF7E6' },
  { keywords: ['保健', '营养'], icon: 'medal-o', color: '#52C41A', bg: '#F6FFED' },
]

function subStyle(name: string): { icon: string; color: string; bg: string } {
  const rule = SUB_ICON_RULES.find((r) => r.keywords.some((k) => name.includes(k)))
  return rule ?? { icon: 'apps-o', color: '#595959', bg: '#F5F5F5' }
}

// ---- 派生 ----
const activeTop = computed(() => categories.value.find((c) => c.id === activeTopId.value) ?? null)
const subCategories = computed(() => activeTop.value?.children ?? [])
/** 推荐流过滤 id：选中二级分类时用二级，否则用一级（mock 端点支持父子级联过滤） */
const searchCategoryId = computed(() => selectedSubId.value || activeTopId.value)

// ---- 生命周期 ----
onMounted(async () => {
  loading.value = true
  loadError.value = false
  try {
    categories.value = await categoryApi.getTree()
    if (categories.value.length > 0) {
      activeTopId.value = categories.value[0].id
    }
    applyQueryCategory()
    await reloadProducts()
  } catch (e) {
    logger.error('分类树加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
})

/** KeepAlive 重新激活时同步深链 query（首页分类入口跳转场景） */
onActivated(() => {
  applyQueryCategory()
})

watch(
  () => route.query.categoryId,
  () => applyQueryCategory(),
)

watch(searchCategoryId, () => {
  void reloadProducts()
})

/** 解析 ?categoryId=：一级 id 直接激活；二级 id 激活父级并选中该二级 */
function applyQueryCategory(): void {
  const q = route.query.categoryId
  const id = typeof q === 'string' ? q : ''
  if (!id || categories.value.length === 0) return

  const top = categories.value.find((c) => c.id === id)
  if (top) {
    activeTopId.value = top.id
    selectedSubId.value = ''
    return
  }
  const parent = categories.value.find((c) => c.children.some((s) => s.id === id))
  if (parent) {
    activeTopId.value = parent.id
    selectedSubId.value = id
  }
}

/** 推荐流加载（按当前分类过滤） */
async function reloadProducts(): Promise<void> {
  if (!searchCategoryId.value) {
    products.value = []
    return
  }
  productLoading.value = true
  productError.value = false
  try {
    const result = await productApi.search({ categoryId: searchCategoryId.value, page: 1, pageSize: 20 })
    products.value = result.items
  } catch (e) {
    logger.warn('分类推荐流加载失败', e)
    productError.value = true
  } finally {
    productLoading.value = false
  }
}

function selectTop(id: string): void {
  if (activeTopId.value === id) return
  activeTopId.value = id
  selectedSubId.value = ''
}

/** 二级分类：点击跳转搜索结果页（按该分类过滤）；再次点击已选中的则取消过滤 */
function selectSub(id: string): void {
  if (selectedSubId.value === id) {
    selectedSubId.value = ''
    return
  }
  selectedSubId.value = id
}

function goSearch(): void {
  router.push('/search')
}

async function retryAll(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    categories.value = await categoryApi.getTree()
    if (categories.value.length > 0 && !categories.value.some((c) => c.id === activeTopId.value)) {
      activeTopId.value = categories.value[0].id
      selectedSubId.value = ''
    }
    applyQueryCategory()
    await reloadProducts()
  } catch (e) {
    logger.error('分类树重试失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="category-page">
    <!-- 顶部搜索框 -->
    <header class="search-header">
      <div class="search-box" role="search" aria-label="搜索商品" @click="goSearch">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <circle cx="11" cy="11" r="7" />
          <path d="M21 21l-4.3-4.3" />
        </svg>
        <span>搜索商品</span>
      </div>
    </header>

    <!-- 骨架屏 -->
    <div v-if="loading" class="split">
      <div class="sidebar">
        <div v-for="i in 6" :key="i" class="skeleton-block sk-side" />
      </div>
      <div class="content">
        <div class="sub-grid">
          <div v-for="i in 4" :key="i" class="sk-sub">
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
      </div>
    </div>

    <!-- 错误态 -->
    <div v-else-if="loadError" class="split">
      <div class="sidebar" />
      <div class="content">
        <ErrorState title="分类加载失败" description="网络异常，请稍后重试" @retry="retryAll" />
      </div>
    </div>

    <!-- 左右分栏 -->
    <div v-else class="split">
      <!-- 左侧一级分类 -->
      <nav class="sidebar" role="navigation" aria-label="一级分类">
        <div
          v-for="cat in categories"
          :key="cat.id"
          class="cat-item"
          :class="{ active: cat.id === activeTopId }"
          :aria-current="cat.id === activeTopId ? 'page' : undefined"
          @click="selectTop(cat.id)"
        >
          {{ cat.name }}
        </div>
      </nav>

      <!-- 右侧内容 -->
      <div class="content">
        <!-- 二级分类网格 -->
        <div v-if="subCategories.length > 0" class="sub-grid">
          <div
            v-for="sub in subCategories"
            :key="sub.id"
            class="sub-cell"
            :class="{ selected: sub.id === selectedSubId }"
            @click="selectSub(sub.id)"
          >
            <div class="sub-icon" :style="{ background: subStyle(sub.name).bg }">
              <van-icon :name="subStyle(sub.name).icon" :color="subStyle(sub.name).color" size="24" />
            </div>
            <span class="sub-name">{{ sub.name }}</span>
          </div>
        </div>

        <!-- 为你推荐 -->
        <div class="rec-title">为你推荐</div>

        <!-- 推荐流骨架 -->
        <div v-if="productLoading" class="sk-rec">
          <div v-for="i in 4" :key="i" class="sk-card">
            <div class="skeleton-block img" />
            <div class="skeleton-block l1" />
            <div class="skeleton-block l2" />
          </div>
        </div>

        <!-- 推荐流错误 -->
        <ErrorState
          v-else-if="productError"
          title="商品加载失败"
          description="网络异常，请稍后重试"
          @retry="reloadProducts"
        />

        <!-- 推荐流空态 -->
        <div v-else-if="products.length === 0" class="empty-wrap">
          <svg width="56" height="56" viewBox="0 0 48 48" fill="none" stroke="#D9D9D9" stroke-width="2">
            <rect x="8" y="12" width="32" height="26" rx="3" />
            <path d="M16 12V8a8 8 0 0 1 16 0v4" stroke-opacity=".5" />
          </svg>
          <div class="empty-text">该分类下暂无商品</div>
          <van-button size="small" round type="primary" @click="selectedSubId = ''">去逛逛</van-button>
        </div>

        <!-- 推荐流瀑布 -->
        <div v-else class="waterfall">
          <ProductCard v-for="product in products" :key="product.id" :product="product" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.category-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n1);
}

/* 顶部搜索框 */
.search-header {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  flex-shrink: 0;
}

.search-box {
  flex: 1;
  height: 32px;
  background: var(--n3);
  border-radius: 16px;
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s1);
  color: var(--n7);
  font-size: var(--fs-base);
}

/* 左右分栏 */
.split {
  flex: 1;
  display: flex;
  overflow: hidden;
  min-height: 0;
}

/* 左侧 sidebar */
.sidebar {
  width: 80px;
  background: var(--n3);
  flex-shrink: 0;
  overflow-y: auto;
  padding: var(--s1) 0;
}

.cat-item {
  height: 46px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
}

.cat-item.active {
  background: var(--n1);
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.cat-item.active::before {
  content: "";
  position: absolute;
  left: 0;
  top: 13px;
  bottom: 13px;
  width: 3px;
  background: var(--c-primary);
  border-radius: 0 2px 2px 0;
}

/* 右侧内容 */
.content {
  flex: 1;
  overflow-y: auto;
  background: var(--n1);
  padding: var(--s3);
}

/* 二级分类网格 */
.sub-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s3);
  margin-bottom: var(--s4);
}

.sub-cell {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--s1);
  padding: var(--s2) 0;
  border-radius: var(--r-base);
  cursor: pointer;
}

.sub-cell.selected .sub-icon {
  box-shadow: 0 0 0 2px var(--c-primary);
}

.sub-cell.selected .sub-name {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.sub-icon {
  width: 48px;
  height: 48px;
  border-radius: var(--r-lg);
  background: #e6f4ff;
  display: flex;
  align-items: center;
  justify-content: center;
}

.sub-name {
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* 为你推荐标题 */
.rec-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin: var(--s2) 0 var(--s3);
}

.rec-title::before,
.rec-title::after {
  content: "";
  flex: 1;
  height: 1px;
  background: var(--n3);
}

/* 瀑布流 */
.waterfall {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
}

/* 空态 */
.empty-wrap {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  padding: 48px 0;
  color: var(--n7);
}

.empty-text {
  font-size: var(--fs-base);
}

/* 骨架屏 */
.sk-side {
  height: 46px;
  margin: var(--s1) var(--s2);
  border-radius: var(--r-base);
}

.sk-sub {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.sk-sub .ic {
  width: 48px;
  height: 48px;
  border-radius: var(--r-lg);
}

.sk-sub .tx {
  width: 36px;
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
</style>
