<script setup lang="ts">
import { computed, onActivated, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { cartApi } from '@/modules/05-cart/api/cart.api'
import { productApi } from '@/modules/03-catalog/api/product.api'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import type { CartDto, CartItemDto } from '@/modules/05-cart/types/cart.dto'
import type { ProductSummaryDto } from '@/modules/03-catalog/types/product.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ProductCard from '@/shared/components/ProductCard.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 购物车页（/cart，Tabbar 页，KeepAlive 缓存名 Cart）
 *
 * 结构（对齐设计稿 cart）：
 * 顶部 NavBar（标题 + 管理态切换）→ 滚动主体（按卖家分组的商品卡片：复选框 + 图片 +
 * 标题 + 规格 + 单价 + 步进器；失效商品区置灰不可选不计入合计；底部「为你推荐」瀑布）
 * → 底部结算栏（全选 / 合计 / 结算(N)，管理态切换为删除按钮）
 *
 * 勾选、全选、数量、删除均为服务端持久化操作，以服务端返回的 CartDto 回填；
 * 数量修改 300ms 防抖，失败时重新拉取服务端数据回滚；
 * 库存不足的商品在图片上标注「仅剩 N 件」，步进器上限为可售库存；
 * 失效（无库存）条目置灰展示，单独提供删除与清空入口。
 */
defineOptions({ name: 'Cart' })

const router = useRouter()
const cartStore = useCartStore()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const cart = ref<CartDto | null>(null)
const manageMode = ref(false)
const recProducts = ref<ProductSummaryDto[]>([])

/** KeepAlive 激活标记：首次激活与 onMounted 同步，跳过重复刷新 */
let firstActivation = true

/** 数量同步防抖计时器（skuId → timer） */
const stepperTimers = new Map<string, ReturnType<typeof setTimeout>>()

/** 卖家分组 */
interface SellerGroup {
  shopId: string
  shopName: string
  items: CartItemDto[]
}

/** 有效条目（有库存可购买） */
const validItems = computed<CartItemDto[]>(() =>
  (cart.value?.items ?? []).filter((i) => i.stock > 0),
)

/** 失效条目（下架/无库存） */
const invalidItems = computed<CartItemDto[]>(() =>
  (cart.value?.items ?? []).filter((i) => i.stock <= 0),
)

/** 按卖家分组（前端聚合，保持后端返回顺序） */
const sellerGroups = computed<SellerGroup[]>(() => {
  const groups = new Map<string, SellerGroup>()
  for (const item of validItems.value) {
    const group = groups.get(item.shopId) ?? { shopId: item.shopId, shopName: item.shopName, items: [] }
    group.items.push(item)
    groups.set(item.shopId, group)
  }
  return Array.from(groups.values())
})

/** 已勾选条目（仅有效项） */
const selectedItems = computed<CartItemDto[]>(() => validItems.value.filter((i) => i.selected))

/** 勾选件数 */
const selectedCount = computed(() => selectedItems.value.reduce((acc, i) => acc + i.quantity, 0))

/** 勾选商品总额（分） */
const selectedAmount = computed(() => selectedItems.value.reduce((acc, i) => acc + i.price * i.quantity, 0))

/** 是否全选（仅有效项） */
const isAllSelected = computed(
  () => validItems.value.length > 0 && validItems.value.every((i) => i.selected),
)

/** 分组是否全选 */
function isGroupAllSelected(group: SellerGroup): boolean {
  return group.items.length > 0 && group.items.every((i) => i.selected)
}

onMounted(() => {
  void loadCart()
  void loadRecommendations()
})

// 返回本页时静默刷新（下单/加购后购物车可能已变化）
onActivated(() => {
  if (firstActivation) {
    firstActivation = false
    return
  }
  void loadCart(true)
})

onUnmounted(() => {
  for (const timer of stepperTimers.values()) {
    clearTimeout(timer)
  }
  stepperTimers.clear()
})

/** 加载购物车（silent = 操作后的静默刷新，不展示骨架） */
async function loadCart(silent = false): Promise<void> {
  if (!silent) {
    loading.value = true
  }
  loadError.value = false
  try {
    const dto = await cartApi.getCart()
    cart.value = dto
    cartStore.badge = dto.totalCount
  } catch (e) {
    logger.error('购物车加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 加载底部推荐位（失败静默隐藏） */
async function loadRecommendations(): Promise<void> {
  try {
    const result = await productApi.search({ page: 1, pageSize: 6, sort: 'sales' })
    recProducts.value = result.items
  } catch (e) {
    logger.warn('购物车推荐加载失败（忽略）', e)
  }
}

// ---- 勾选操作 ----

/** 单个条目勾选/取消 */
async function toggleItem(item: CartItemDto, checked: boolean): Promise<void> {
  try {
    cart.value = await cartApi.selectItems({ skuIds: [item.skuId], selected: checked })
  } catch (e) {
    logger.warn('购物车勾选失败', e)
    showFailToast(e instanceof Error ? e.message : '操作失败，请重试')
    void loadCart(true)
  }
}

/** 店铺分组全选/取消 */
async function toggleGroup(group: SellerGroup, checked: boolean): Promise<void> {
  try {
    cart.value = await cartApi.selectItems({
      skuIds: group.items.map((i) => i.skuId),
      selected: checked,
    })
  } catch (e) {
    logger.warn('购物车分组勾选失败', e)
    showFailToast(e instanceof Error ? e.message : '操作失败，请重试')
    void loadCart(true)
  }
}

/** 全选/取消全选（仅影响有效项，失效项保持不选中） */
async function toggleSelectAll(checked: boolean): Promise<void> {
  try {
    cart.value = await cartApi.selectItems({
      skuIds: validItems.value.map((i) => i.skuId),
      selected: checked,
    })
  } catch (e) {
    logger.warn('购物车全选失败', e)
    showFailToast(e instanceof Error ? e.message : '操作失败，请重试')
    void loadCart(true)
  }
}

// ---- 数量操作（300ms 防抖同步服务端） ----

function onQuantityChange(item: CartItemDto): void {
  const prev = stepperTimers.get(item.skuId)
  if (prev) {
    clearTimeout(prev)
  }
  stepperTimers.set(
    item.skuId,
    setTimeout(() => {
      stepperTimers.delete(item.skuId)
      void syncQuantity(item)
    }, 300),
  )
}

async function syncQuantity(item: CartItemDto): Promise<void> {
  try {
    const updated = await cartApi.updateQuantity(item.skuId, { quantity: item.quantity })
    item.quantity = updated.quantity
    item.price = updated.price
    syncBadge()
  } catch (e) {
    logger.warn('购物车数量同步失败', e)
    showFailToast(e instanceof Error ? e.message : '数量修改失败，请重试')
    void loadCart(true)
  }
}

/** 角标 = 购物车总件数（本地推导与服务端一致） */
function syncBadge(): void {
  const totalCount = (cart.value?.items ?? []).reduce((acc, i) => acc + i.quantity, 0)
  if (cartStore.badge !== totalCount) {
    cartStore.badge = totalCount
  }
}

// ---- 删除操作 ----

/** 批量删除条目并回填服务端状态 */
async function removeItems(items: CartItemDto[]): Promise<void> {
  try {
    await Promise.all(items.map((i) => cartApi.removeItem(i.skuId)))
    showToast('已删除')
  } catch (e) {
    logger.warn('购物车删除失败', e)
    showFailToast(e instanceof Error ? e.message : '删除失败，请重试')
  } finally {
    await loadCart(true)
  }
}

/** 管理态：删除已勾选的有效商品 */
async function confirmRemoveSelected(): Promise<void> {
  const selected = selectedItems.value
  if (selected.length === 0) {
    showToast('请先选择要删除的商品')
    return
  }
  const count = selected.reduce((acc, i) => acc + i.quantity, 0)
  try {
    await showConfirmDialog({
      title: '确认删除',
      message: `删除后将无法恢复，已选 ${count} 件商品将一并删除。`,
      confirmButtonText: '确认删除',
      confirmButtonColor: '#FF4D4F',
    })
    await removeItems(selected)
  } catch {
    // 用户取消删除
  }
}

/** 删除单个失效商品 */
async function confirmRemoveInvalid(item: CartItemDto): Promise<void> {
  try {
    await showConfirmDialog({
      title: '确认删除',
      message: '删除后将无法恢复，该失效商品将从购物车移除。',
      confirmButtonText: '确认删除',
      confirmButtonColor: '#FF4D4F',
    })
    await removeItems([item])
  } catch {
    // 用户取消删除
  }
}

/** 清空失效商品 */
async function confirmClearInvalid(): Promise<void> {
  const items = invalidItems.value
  if (items.length === 0) return
  try {
    await showConfirmDialog({
      title: '清空失效商品',
      message: `将清空 ${items.length} 件失效商品，删除后无法恢复。`,
      confirmButtonText: '清空',
      confirmButtonColor: '#FF4D4F',
    })
    await removeItems(items)
  } catch {
    // 用户取消清空
  }
}

// ---- 管理态 / 结算 ----

function toggleManage(): void {
  manageMode.value = !manageMode.value
}

function goCheckout(): void {
  if (selectedCount.value === 0) {
    showToast('请选择要结算的商品')
    return
  }
  router.push('/checkout/preview')
}

// ---- 其它跳转 ----

function goHome(): void {
  router.push('/')
}

function goShop(group: SellerGroup): void {
  router.push(`/shop/${group.shopId}`)
}

function goProduct(item: CartItemDto): void {
  router.push(`/product/${item.spuId}`)
}
</script>

<template>
  <div class="cart-page">
    <!-- NavBar -->
    <header class="cart-top">
      <div class="title">购物车</div>
      <button
        class="manage-btn"
        :class="{ active: manageMode }"
        type="button"
        @click="toggleManage"
      >
        {{ manageMode ? '完成' : '管理' }}
      </button>
    </header>

    <!-- 滚动主体 -->
    <main class="cart-body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeletons">
        <div class="skeleton-block sk-tip" />
        <div v-for="i in 2" :key="i" class="sk-card">
          <div class="sk-head">
            <div class="skeleton-block c" />
            <div class="skeleton-block n" />
          </div>
          <div class="sk-row">
            <div class="skeleton-block c" />
            <div class="skeleton-block img" />
            <div class="sk-info">
              <div class="skeleton-block l1" />
              <div class="skeleton-block l2" />
              <div class="skeleton-block l3" />
            </div>
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="购物车加载失败"
        description="网络异常，请下拉刷新或点击重试"
        @retry="loadCart()"
      />

      <!-- 有商品 -->
      <template v-else-if="cart && cart.items.length > 0">
        <!-- 卖家分组卡片 -->
        <section
          v-for="group in sellerGroups"
          :key="group.shopId"
          class="seller-card"
          role="group"
          :aria-label="`${group.shopName} 商品分组`"
        >
          <div class="seller-head">
            <van-checkbox
              :model-value="isGroupAllSelected(group)"
              shape="round"
              aria-label="选中本店全部商品"
              @update:model-value="toggleGroup(group, $event)"
            />
            <span class="shop-icon">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round">
                <path d="M3 9l9-6 9 6v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9z" />
                <path d="M9 22V12h6v10" />
              </svg>
            </span>
            <span class="shop-name">{{ group.shopName }}</span>
            <button class="enter" type="button" @click="goShop(group)">进店 ›</button>
          </div>

          <div v-for="item in group.items" :key="item.skuId" class="item-row">
            <van-checkbox
              :model-value="item.selected"
              shape="round"
              class="item-check"
              :aria-label="`选中 ${item.name}`"
              @update:model-value="toggleItem(item, $event)"
            />
            <div class="item-img-wrap" @click="goProduct(item)">
              <img :src="item.image" :alt="item.name" loading="lazy">
              <div v-if="item.stock <= 10" class="stock-warn">仅剩 {{ item.stock }} 件</div>
            </div>
            <div class="item-info">
              <div class="item-name" @click="goProduct(item)">{{ item.name }}</div>
              <div class="item-sku">
                <span>{{ item.specs }}</span>
                <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
                  <path d="M9 6l6 6-6 6" />
                </svg>
              </div>
              <div class="item-bottom">
                <PriceText :amount="item.price" :size="16" />
                <van-stepper
                  v-model="item.quantity"
                  :min="1"
                  :max="item.stock"
                  integer
                  :disable-input="true"
                  aria-label="商品数量"
                  @change="onQuantityChange(item)"
                />
              </div>
            </div>
          </div>
        </section>

        <!-- 失效商品区 -->
        <section v-if="invalidItems.length > 0" class="invalid-section">
          <div class="invalid-head">
            <span class="t">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v6" />
                <circle cx="12" cy="16" r="1" fill="currentColor" />
              </svg>
              失效商品 ({{ invalidItems.length }})
            </span>
            <button class="clear" type="button" @click="confirmClearInvalid">清空失效</button>
          </div>
          <div v-for="item in invalidItems" :key="item.skuId" class="invalid-item">
            <div class="img-wrap">
              <img :src="item.image" :alt="item.name" loading="lazy">
              <div class="invalid-tag">已失效</div>
            </div>
            <div class="info">
              <div class="name">{{ item.name }}</div>
              <div class="reason">商品已失效，暂时无法购买</div>
            </div>
            <button class="del-btn" type="button" @click="confirmRemoveInvalid(item)">删除</button>
          </div>
        </section>

        <!-- 推荐位 -->
        <template v-if="recProducts.length > 0">
          <div class="rec-title">
            <span class="line" />
            <span class="t">为你推荐</span>
            <span class="line" />
          </div>
          <div class="rec-list">
            <ProductCard v-for="product in recProducts" :key="product.id" :product="product" />
          </div>
        </template>
      </template>

      <!-- 空购物车 -->
      <template v-else>
        <EmptyState title="购物车空空如也" action-text="去购物" @action="goHome" />
        <template v-if="recProducts.length > 0">
          <div class="rec-title">
            <span class="line" />
            <span class="t">猜你喜欢</span>
            <span class="line" />
          </div>
          <div class="rec-list">
            <ProductCard v-for="product in recProducts" :key="product.id" :product="product" />
          </div>
        </template>
      </template>
    </main>

    <!-- 底部结算栏（空车/加载失败时隐藏） -->
    <footer
      v-if="!loading && !loadError && cart && cart.items.length > 0"
      class="settle-bar"
    >
      <div class="select-all">
        <van-checkbox
          :model-value="isAllSelected"
          shape="round"
          aria-label="全选有效商品"
          @update:model-value="toggleSelectAll"
        />
        <span class="lbl">全选</span>
      </div>
      <div class="sum-wrap">
        <div class="sum-label">合计</div>
        <PriceText :amount="selectedAmount" :size="20" />
        <div class="sum-detail">已选 {{ selectedCount }} 件</div>
      </div>
      <!-- 管理态：删除；普通态：结算 -->
      <button v-if="manageMode" class="del-btn" type="button" @click="confirmRemoveSelected">
        删除
      </button>
      <button
        v-else
        class="settle-btn"
        :class="{ disabled: selectedCount === 0 }"
        type="button"
        aria-label="去结算"
        @click="goCheckout"
      >
        结算<span class="count">({{ selectedCount }})</span>
      </button>
    </footer>
  </div>
</template>

<style scoped>
.cart-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n3);
}

/* NavBar */
.cart-top {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 var(--s3);
  flex-shrink: 0;
}

.cart-top .title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.manage-btn {
  font-size: var(--fs-sm);
  color: var(--n9);
  background: none;
  border: none;
  padding: var(--s1) var(--s1);
  cursor: pointer;
  font-family: inherit;
}

.manage-btn.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

/* 滚动主体 */
.cart-body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  background: var(--n3);
}

/* 骨架屏 */
.sk-tip {
  height: 32px;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3);
  margin-bottom: var(--s3);
}

.sk-head {
  display: flex;
  gap: var(--s2);
  align-items: center;
  margin-bottom: var(--s3);
}

.sk-head .c {
  width: 20px;
  height: 20px;
  border-radius: 50%;
}

.sk-head .n {
  width: 120px;
  height: 14px;
}

.sk-row {
  display: flex;
  gap: 10px;
  padding: var(--s2) 0;
  border-top: 1px solid var(--n3);
}

.sk-row .c {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  margin-top: 32px;
}

.sk-row .img {
  width: 80px;
  height: 80px;
}

.sk-info {
  flex: 1;
}

.sk-info .l1 {
  width: 80%;
  height: 14px;
  margin-top: var(--s1);
}

.sk-info .l2 {
  width: 50%;
  height: 12px;
  margin-top: var(--s2);
}

.sk-info .l3 {
  width: 30%;
  height: 16px;
  margin-top: 16px;
}

/* 卖家分组卡片 */
.seller-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
}

.seller-head {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3);
}

.seller-head .shop-icon {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--c-primary);
}

.seller-head .shop-name {
  flex: 1;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.seller-head .enter {
  font-size: var(--fs-sm);
  color: var(--c-primary);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
}

/* 商品行 */
.item-row {
  display: flex;
  gap: 10px;
  padding: var(--s2) var(--s3) var(--s3);
  border-top: 1px solid var(--n3);
}

.item-row .item-check {
  flex-shrink: 0;
  align-self: center;
}

.item-img-wrap {
  flex-shrink: 0;
  width: 80px;
  height: 80px;
  border-radius: var(--r-base);
  overflow: hidden;
  background: var(--n3);
  position: relative;
  cursor: pointer;
}

.item-img-wrap img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.item-img-wrap .stock-warn {
  position: absolute;
  left: 0;
  bottom: 0;
  right: 0;
  background: rgba(250, 173, 20, 0.92);
  color: #fff;
  font-size: 10px;
  text-align: center;
  padding: 1px 0;
}

.item-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.item-name {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  height: 38px;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  cursor: pointer;
}

.item-sku {
  display: inline-flex;
  align-items: center;
  gap: var(--s1);
  margin-top: 6px;
  padding: 2px 6px;
  background: var(--n3);
  border-radius: var(--r-base);
  font-size: 11px;
  color: var(--n9);
  align-self: flex-start;
  max-width: 100%;
}

.item-sku span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.item-bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--s2);
  gap: var(--s2);
}

.item-bottom .price-text {
  flex: 1;
  min-width: 0;
}

/* 失效商品区 */
.invalid-section {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
}

.invalid-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3);
}

.invalid-head .t {
  font-size: var(--fs-base);
  color: var(--n9);
  display: flex;
  align-items: center;
  gap: 6px;
}

.invalid-head .clear {
  font-size: var(--fs-sm);
  color: var(--c-error);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  font-family: inherit;
}

.invalid-item {
  display: flex;
  gap: 10px;
  padding: var(--s2) var(--s3) var(--s3);
  border-top: 1px solid var(--n3);
  opacity: 0.7;
}

.invalid-item .img-wrap {
  flex-shrink: 0;
  width: 80px;
  height: 80px;
  border-radius: var(--r-base);
  overflow: hidden;
  background: var(--n3);
  position: relative;
}

.invalid-item .img-wrap img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  filter: grayscale(0.6);
}

.invalid-item .img-wrap .invalid-tag {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.45);
  color: #fff;
  font-size: 11px;
}

.invalid-item .info {
  flex: 1;
  min-width: 0;
}

.invalid-item .name {
  font-size: var(--fs-base);
  color: var(--n9);
  line-height: 1.4;
  height: 38px;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.invalid-item .reason {
  font-size: 11px;
  color: var(--n7);
  margin-top: var(--s1);
}

.invalid-item .del-btn {
  align-self: flex-start;
  margin-top: auto;
  padding: var(--s1) 10px;
  border: 1px solid var(--n5);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: var(--n9);
  background: none;
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
}

/* 推荐位 */
.rec-title {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  padding: var(--s2) 0 var(--s3);
}

.rec-title .line {
  width: 24px;
  height: 1px;
  background: var(--n5);
}

.rec-title .t {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.rec-list {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--s2);
  padding-bottom: var(--s3);
}

/* 底部结算栏 */
.settle-bar {
  height: 56px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: 10px;
  flex-shrink: 0;
  box-shadow: 0 -2px 12px rgba(0, 0, 0, 0.04);
}

.settle-bar .select-all {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--fs-base);
  color: var(--n10);
  flex-shrink: 0;
}

.settle-bar .sum-wrap {
  flex: 1;
  text-align: right;
  min-width: 0;
}

.settle-bar .sum-label {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.settle-bar .sum-detail {
  font-size: 11px;
  color: var(--n7);
}

.settle-btn {
  height: 40px;
  padding: 0 var(--s6);
  background: var(--c-error);
  color: #fff;
  border-radius: 20px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  display: flex;
  align-items: center;
  gap: var(--s1);
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
  transition: all 0.15s;
}

.settle-btn:active {
  transform: scale(0.96);
}

.settle-btn.disabled {
  background: var(--n5);
}

.settle-btn .count {
  font-weight: var(--fw-normal);
}

.settle-bar .del-btn {
  height: 40px;
  padding: 0 var(--s6);
  background: var(--n1);
  border: 1px solid var(--c-error);
  color: var(--c-error);
  border-radius: 20px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
}
</style>
