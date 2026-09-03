<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { favoriteApi } from '@/modules/13-profile/api/favorite.api'
import type { FavoriteDto } from '../types/profile.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatSales } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 我的收藏页（/profile/favorites）
 *
 * 结构（对齐设计稿 favorites）：
 * NavBar（返回 + 我的收藏 + 管理态切换）→ 排序 Tab（综合 / 价格升降 / 销量 / 最新，前端排序）
 * → 双列商品卡片（主图 + 标题 + 价格 + 店铺 + 心形取消收藏按钮）
 * → 管理态底部操作栏（全选 / 取消收藏 / 完成，适配 safe-area）
 *
 * 交互：
 * - 点击卡片跳商品详情 /product/{spuId}
 * - 心形单条取消收藏：乐观移除，失败回滚并提示
 * - 管理态勾选后批量取消收藏：二次确认（红色确认按钮）
 */
const router = useRouter()

/** 排序方式 */
type SortKey = 'comprehensive' | 'price' | 'sales' | 'created'

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const favorites = ref<FavoriteDto[]>([])
const refreshing = ref(false)

/** 排序 */
const sortKey = ref<SortKey>('comprehensive')
/** 价格排序方向（price 时生效，默认降序） */
const priceOrder = ref<'asc' | 'desc'>('desc')

// ---- 管理态 ----
const manageMode = ref(false)
const selectedIds = ref<string[]>([])

const SORT_TABS: Array<{ key: SortKey; label: string }> = [
  { key: 'comprehensive', label: '综合' },
  { key: 'price', label: '价格' },
  { key: 'sales', label: '销量' },
  { key: 'created', label: '最新' },
]

/** 排序后的展示列表（保持后端返回顺序为综合排序） */
const sortedFavorites = computed(() => {
  const list = [...favorites.value]
  switch (sortKey.value) {
    case 'price':
      list.sort((a, b) => (priceOrder.value === 'asc' ? a.price - b.price : b.price - a.price))
      break
    case 'sales':
      list.sort((a, b) => b.sales - a.sales)
      break
    case 'created':
      list.sort((a, b) => new Date(b.favoritedAt).getTime() - new Date(a.favoritedAt).getTime())
      break
    default:
      break
  }
  return list
})

/** 是否全选 */
const isAllSelected = computed(
  () => favorites.value.length > 0 && selectedIds.value.length === favorites.value.length,
)

/** 已选数量 */
const selectedCount = computed(() => selectedIds.value.length)

onMounted(() => {
  void loadFavorites()
})

/** 加载收藏列表 */
async function loadFavorites(silent = false): Promise<void> {
  if (!silent) {
    loading.value = true
  }
  loadError.value = false
  try {
    favorites.value = await favoriteApi.list()
    // 列表重置后清理已失效的勾选
    const ids = new Set(favorites.value.map((f) => f.spuId))
    selectedIds.value = selectedIds.value.filter((id) => ids.has(id))
  } catch (e) {
    logger.error('收藏列表加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await loadFavorites(true)
}

// ---- 排序切换 ----
function setSort(key: SortKey): void {
  if (key === 'price' && sortKey.value === 'price') {
    // 价格 Tab 重复点击切换升降序
    priceOrder.value = priceOrder.value === 'asc' ? 'desc' : 'asc'
    return
  }
  if (sortKey.value === key) return
  sortKey.value = key
}

// ---- 跳转 ----
function goProduct(spuId: string): void {
  if (manageMode.value) return
  router.push(`/product/${spuId}`)
}

function goHome(): void {
  router.push('/')
}

// ---- 管理态 ----
function toggleManage(): void {
  manageMode.value = !manageMode.value
  selectedIds.value = []
}

function toggleSelect(spuId: string): void {
  const idx = selectedIds.value.indexOf(spuId)
  if (idx >= 0) {
    selectedIds.value.splice(idx, 1)
  } else {
    selectedIds.value.push(spuId)
  }
}

function toggleSelectAll(): void {
  if (isAllSelected.value) {
    selectedIds.value = []
  } else {
    selectedIds.value = favorites.value.map((f) => f.spuId)
  }
}

// ---- 取消收藏 ----
/** 单条取消收藏（乐观移除，失败回滚） */
async function removeOne(item: FavoriteDto): Promise<void> {
  const idx = favorites.value.findIndex((f) => f.spuId === item.spuId)
  if (idx < 0) return
  const backup = favorites.value[idx]
  favorites.value.splice(idx, 1)
  selectedIds.value = selectedIds.value.filter((id) => id !== item.spuId)
  try {
    await favoriteApi.remove(item.spuId)
    showToast('已取消收藏')
  } catch (e) {
    // 回滚
    favorites.value.splice(idx, 0, backup)
    logger.error('取消收藏失败', e)
    showFailToast('取消失败，请稍后重试')
  }
}

/** 批量取消收藏（二次确认） */
async function removeSelected(): Promise<void> {
  if (selectedCount.value === 0 || batchRemoving.value) return
  try {
    await showConfirmDialog({
      title: '确认取消收藏',
      message: `将取消已选 ${selectedCount.value} 件商品的收藏，此操作可重新收藏。`,
      confirmButtonText: '取消收藏',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  batchRemoving.value = true
  const spuIds = [...selectedIds.value]
  try {
    await favoriteApi.batchRemove(spuIds)
    showToast('已取消收藏')
    selectedIds.value = []
    if (favorites.value.length === spuIds.length) {
      // 全部移除后回到空态
      manageMode.value = false
      favorites.value = []
    } else {
      await loadFavorites(true)
    }
  } catch (e) {
    logger.error('批量取消收藏失败', e)
    showFailToast(e instanceof Error ? e.message : '取消失败，请稍后重试')
  } finally {
    batchRemoving.value = false
  }
}

const batchRemoving = ref(false)

// ---- 返回 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/profile')
  }
}
</script>

<template>
  <div class="favorites-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">我的收藏</div>
      <button class="manage-btn" type="button" @click="toggleManage">
        {{ manageMode ? '完成' : '管理' }}
      </button>
    </header>

    <!-- 排序 Tab -->
    <nav class="sortbar" role="tablist" aria-label="收藏排序">
      <div
        v-for="tab in SORT_TABS"
        :key="tab.key"
        class="sort-item"
        :class="{ active: sortKey === tab.key }"
        role="tab"
        :aria-selected="sortKey === tab.key"
        @click="setSort(tab.key)"
      >
        {{ tab.label }}
        <span v-if="tab.key === 'price' && sortKey === 'price'" class="sort-arrow">
          {{ priceOrder === 'asc' ? '↑' : '↓' }}
        </span>
      </div>
    </nav>

    <!-- 列表区 -->
    <div class="list-wrap" :class="{ managing: manageMode }">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="product-grid">
        <div v-for="i in 4" :key="i" class="sk-card">
          <div class="skeleton-block sk-img" />
          <div class="skeleton-block sk-l1" />
          <div class="skeleton-block sk-l2" />
          <div class="skeleton-block sk-l3" />
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError && favorites.length === 0"
        title="收藏加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadFavorites()"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="favorites.length === 0"
        title="暂无收藏"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 收藏列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <div class="product-grid">
          <article
            v-for="item in sortedFavorites"
            :key="item.spuId"
            class="product-card"
            role="article"
            :aria-label="item.name"
            @click="goProduct(item.spuId)"
          >
            <div class="img-wrap">
              <img class="img" :src="item.mainImage" :alt="item.name" loading="lazy">
              <!-- 管理态勾选框 -->
              <div
                v-if="manageMode"
                class="check-box"
                :class="{ checked: selectedIds.includes(item.spuId) }"
                role="checkbox"
                :aria-checked="selectedIds.includes(item.spuId)"
                :aria-label="`选择 ${item.name}`"
                @click.stop="toggleSelect(item.spuId)"
              >
                <van-icon v-if="selectedIds.includes(item.spuId)" name="success" size="12" color="#fff" />
              </div>
              <!-- 取消收藏按钮（非管理态） -->
              <button
                v-else
                class="unfav-btn"
                type="button"
                aria-label="取消收藏"
                @click.stop="removeOne(item)"
              >
                <van-icon name="like" size="18" color="#FF4D4F" />
              </button>
            </div>
            <div class="info">
              <div class="name text-ellipsis-2">{{ item.name }}</div>
              <PriceText :amount="item.price" :size="16" />
              <div class="meta-row">
                <span class="shop text-ellipsis">{{ item.shopName }}</span>
                <span class="sales">月销 {{ formatSales(item.sales) }}</span>
              </div>
            </div>
          </article>
        </div>
      </van-pull-refresh>
    </div>

    <!-- 管理态底部操作栏 -->
    <footer v-if="manageMode" class="batch-bar">
      <button class="batch-select-all" type="button" role="checkbox" :aria-checked="isAllSelected" @click="toggleSelectAll">
        <span class="batch-checkbox" :class="{ checked: isAllSelected }">
          <van-icon v-if="isAllSelected" name="success" size="12" color="#fff" />
        </span>
        全选
      </button>
      <div class="batch-actions">
        <button
          class="batch-btn delete"
          type="button"
          :disabled="selectedCount === 0 || batchRemoving"
          @click="removeSelected"
        >
          取消收藏{{ selectedCount > 0 ? `(${selectedCount})` : '' }}
        </button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.favorites-page {
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
  flex: 1;
  text-align: center;
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.manage-btn {
  font-size: var(--fs-base);
  color: var(--c-primary);
  background: none;
  border: none;
  padding: var(--s1) 0;
}

/* 排序栏 */
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
  gap: 2px;
  font-size: var(--fs-base);
  color: var(--n9);
  cursor: pointer;
}

.sort-item.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.sort-arrow {
  font-size: 10px;
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s2);
}

.list-wrap.managing {
  padding-bottom: calc(var(--s12) + env(safe-area-inset-bottom));
}

/* 商品网格 */
.product-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
}

.product-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  box-shadow: var(--sh-card);
}

.img-wrap {
  position: relative;
}

.img {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  background: var(--n3);
  display: block;
}

.unfav-btn {
  position: absolute;
  right: var(--s1);
  bottom: var(--s1);
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.9);
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: var(--sh-card);
}

.check-box {
  position: absolute;
  top: var(--s1);
  left: var(--s1);
  width: 20px;
  height: 20px;
  border: 2px solid rgba(255, 255, 255, 0.9);
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.3);
  display: flex;
  align-items: center;
  justify-content: center;
}

.check-box.checked {
  background: var(--c-primary);
  border-color: var(--c-primary);
}

.info {
  padding: var(--s2);
}

.name {
  font-size: var(--fs-sm);
  color: var(--n10);
  line-height: 1.4;
  height: 34px;
  margin-bottom: var(--s1);
}

.meta-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s1);
  margin-top: var(--s1);
}

.shop {
  font-size: var(--fs-sm);
  color: var(--n7);
  flex: 1;
  min-width: 0;
}

.sales {
  font-size: var(--fs-sm);
  color: var(--n7);
  flex-shrink: 0;
}

/* 骨架屏 */
.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  padding-bottom: var(--s2);
}

.sk-img {
  width: 100%;
  aspect-ratio: 1;
  border-radius: 0;
}

.sk-l1 {
  width: 90%;
  height: 14px;
  margin: var(--s2) var(--s2) 0;
}

.sk-l2 {
  width: 40%;
  height: 16px;
  margin: var(--s1) var(--s2) 0;
}

.sk-l3 {
  width: 70%;
  height: 12px;
  margin: var(--s1) var(--s2) 0;
}

/* 管理态底部操作栏 */
.batch-bar {
  position: sticky;
  bottom: 0;
  background: var(--n1);
  display: flex;
  align-items: center;
  padding: var(--s2) var(--s3);
  border-top: 1px solid var(--n3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
  flex-shrink: 0;
}

.batch-select-all {
  display: flex;
  align-items: center;
  gap: var(--s1);
  font-size: var(--fs-base);
  color: var(--n10);
  padding: var(--s2);
}

.batch-checkbox {
  width: 20px;
  height: 20px;
  border: 2px solid var(--n5);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.batch-checkbox.checked {
  background: var(--c-primary);
  border-color: var(--c-primary);
}

.batch-actions {
  margin-left: auto;
  display: flex;
  gap: var(--s2);
}

.batch-btn {
  padding: var(--s2) var(--s4);
  border: none;
  border-radius: var(--r-lg);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
}

.batch-btn.delete {
  background: var(--c-error);
  color: #fff;
}

.batch-btn.delete:disabled {
  background: var(--n3);
  color: var(--n7);
}
</style>
