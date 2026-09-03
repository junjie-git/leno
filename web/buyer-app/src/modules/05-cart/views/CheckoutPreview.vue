<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { cartApi } from '@/modules/05-cart/api/cart.api'
import type { CheckoutPreviewDto, CartItemDto } from '@/modules/05-cart/types/cart.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'
import { formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 结算预览页（/checkout/preview）
 *
 * 结构（对齐设计稿 checkout-preview 的分组与金额区）：
 * NavBar（返回 + 结算预览）→ 失效/库存不足提示条 → 按卖家分组的商品卡片
 * （图片 + 标题 + 规格 + 单价 × 数量 + 行小计 + 组小计 + 进店入口）
 * → 金额汇总卡（商品金额 / 优惠券优惠 / 运费 / 积分抵扣 / 应付总额）
 * → 底部提交栏（合计 + 已优惠 + 提交订单 → 跳结算确认页）
 *
 * 数据来自 POST /cart/preview（from=cart，基于购物车勾选项）；
 * 勾选为空时后端返回业务错误 40404 → 转空态引导返回购物车。
 */

const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const empty = ref(false)
const preview = ref<CheckoutPreviewDto | null>(null)

/** 卖家分组 */
const shopGroups = computed(() => preview.value?.shopGroups ?? [])

/** 金额明细 */
const amounts = computed(
  () =>
    preview.value?.amounts ?? {
      goodsAmount: 0,
      freight: 0,
      couponDiscount: 0,
      pointsDiscount: 0,
      payableAmount: 0,
    },
)

/** 失效或库存不足的条目数（提示条） */
const invalidCount = computed(
  () => shopGroups.value.flatMap((g) => g.items).filter((i) => i.stock <= 0).length,
)

/** 结算商品总件数 */
const totalQuantity = computed(() =>
  shopGroups.value.flatMap((g) => g.items).reduce((acc, i) => acc + i.quantity, 0),
)

/** 优惠合计（优惠券 + 积分） */
const discountSum = computed(
  () => amounts.value.couponDiscount + amounts.value.pointsDiscount,
)

onMounted(() => {
  void loadPreview()
})

/** 加载结算预览 */
async function loadPreview(): Promise<void> {
  loading.value = true
  loadError.value = false
  empty.value = false
  try {
    preview.value = await cartApi.preview({ from: 'cart' })
  } catch (e) {
    if (e instanceof BusinessError && e.code === 40404) {
      empty.value = true
    } else {
      logger.error('结算预览加载失败', e)
      loadError.value = true
    }
  } finally {
    loading.value = false
  }
}

/** 行小计（分） */
function lineSubtotal(item: CartItemDto): number {
  return item.price * item.quantity
}

/** 组小计（分） */
function groupSubtotal(items: CartItemDto[]): number {
  return items.reduce((acc, i) => acc + lineSubtotal(i), 0)
}

/** 提交订单 → 结算确认页 */
function goSettle(): void {
  router.push('/checkout/settle')
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.push('/cart')
  }
}

function goCart(): void {
  router.push('/cart')
}

function goShop(shopId: string): void {
  router.push(`/shop/${shopId}`)
}
</script>

<template>
  <div class="preview-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">结算预览</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeletons">
        <div class="skeleton-block sk-notice" />
        <div class="sk-card">
          <div class="skeleton-block sk-line" style="width: 40%; height: 16px" />
          <div class="sk-row">
            <div class="skeleton-block img" />
            <div class="sk-info">
              <div class="skeleton-block l1" />
              <div class="skeleton-block l2" />
              <div class="skeleton-block l3" />
            </div>
          </div>
          <div class="sk-row">
            <div class="skeleton-block img" />
            <div class="sk-info">
              <div class="skeleton-block l1" />
              <div class="skeleton-block l2" />
              <div class="skeleton-block l3" />
            </div>
          </div>
        </div>
        <div class="skeleton-block sk-amount" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="预览失败"
        description="网络异常，请稍后重试，或返回购物车检查勾选项"
        @retry="loadPreview"
      />

      <!-- 空态：无勾选结算商品 -->
      <EmptyState
        v-else-if="empty || shopGroups.length === 0"
        title="无结算商品"
        action-text="返回购物车"
        @action="goCart"
      />

      <!-- 内容 -->
      <template v-else>
        <!-- 失效/库存不足提示条 -->
        <div v-if="invalidCount > 0" class="notice-bar" role="alert">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
            <path d="M12 3l9 16H3L12 3z" />
            <path d="M12 10v4M12 17v.5" />
          </svg>
          <span class="text"><b>{{ invalidCount }} 件</b>商品已失效或库存不足，请返回购物车处理</span>
        </div>

        <!-- 卖家分组卡片 -->
        <section
          v-for="group in shopGroups"
          :key="group.shopId"
          class="card seller-block"
          role="group"
          :aria-label="`${group.shopName} 商品分组`"
        >
          <div class="seller-head">
            <span class="shop-icon">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round">
                <path d="M3 9l9-6 9 6v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9z" />
                <path d="M9 22V12h6v10" />
              </svg>
            </span>
            <span class="name">{{ group.shopName }}</span>
            <button class="enter" type="button" @click="goShop(group.shopId)">进店 ›</button>
          </div>
          <div v-for="item in group.items" :key="item.skuId" class="item-line">
            <div class="item-img">
              <img :src="item.image" :alt="item.name" loading="lazy">
              <span v-if="item.stock <= 0" class="invalid-tag">已失效</span>
            </div>
            <div class="item-info">
              <div class="item-name">{{ item.name }}</div>
              <div class="item-sku">{{ item.specs }}</div>
              <div class="item-bottom">
                <span class="item-price">¥{{ formatPriceExact(item.price) }}</span>
                <span class="item-qty">×{{ item.quantity }}</span>
                <span class="item-subtotal">小计 ¥{{ formatPriceExact(lineSubtotal(item)) }}</span>
              </div>
            </div>
          </div>
          <div class="seller-foot">
            <span class="lbl">本组小计（{{ group.items.reduce((acc, i) => acc + i.quantity, 0) }} 件）</span>
            <PriceText :amount="groupSubtotal(group.items)" :size="14" />
          </div>
        </section>

        <!-- 金额汇总卡 -->
        <section class="card">
          <div class="amount-list">
            <div class="amount-row">
              <span class="lbl">商品金额</span>
              <span class="val">¥{{ formatPriceExact(amounts.goodsAmount) }}</span>
            </div>
            <div v-if="amounts.couponDiscount > 0" class="amount-row">
              <span class="lbl">优惠券优惠</span>
              <span class="val discount">-¥{{ formatPriceExact(amounts.couponDiscount) }}</span>
            </div>
            <div v-if="amounts.pointsDiscount > 0" class="amount-row">
              <span class="lbl">积分抵扣</span>
              <span class="val discount">-¥{{ formatPriceExact(amounts.pointsDiscount) }}</span>
            </div>
            <div class="amount-row">
              <span class="lbl">运费预估</span>
              <span class="val">{{ amounts.freight > 0 ? `¥${formatPriceExact(amounts.freight)}` : '包邮' }}</span>
            </div>
            <div class="amount-row total">
              <span class="lbl">应付总额</span>
              <PriceText :amount="amounts.payableAmount" :size="20" />
            </div>
          </div>
        </section>
      </template>
    </main>

    <!-- 底部提交栏 -->
    <footer v-if="!loading && !loadError && shopGroups.length > 0" class="submit-bar">
      <div class="sum-wrap">
        <div class="sum-label">合计（{{ totalQuantity }} 件）</div>
        <PriceText :amount="amounts.payableAmount" :size="20" />
        <div v-if="discountSum > 0" class="sum-detail">已优惠 ¥{{ formatPriceExact(discountSum) }}</div>
      </div>
      <button class="submit-btn" type="button" aria-label="提交订单" @click="goSettle">
        提交订单
      </button>
    </footer>
  </div>
</template>

<style scoped>
.preview-page {
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
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
  background: var(--n3);
}

/* 骨架屏 */
.sk-notice {
  height: 36px;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3);
  margin-bottom: var(--s3);
}

.sk-row {
  display: flex;
  gap: 10px;
  padding: var(--s2) 0;
}

.sk-row .img {
  width: 72px;
  height: 72px;
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
  height: 14px;
  margin-top: 16px;
}

.sk-amount {
  height: 180px;
  border-radius: var(--r-lg);
}

/* 提示条 */
.notice-bar {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s2) var(--s3);
  background: #fff7e6;
  border: 1px solid #ffe7ba;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
  color: var(--c-warning);
}

.notice-bar .text {
  flex: 1;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.notice-bar .text b {
  color: var(--c-error);
  font-weight: var(--fw-medium);
}

/* 卡片通用 */
.card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
  overflow: hidden;
}

/* 卖家分组 */
.seller-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: var(--s3) var(--s3) var(--s2);
}

.seller-head .shop-icon {
  flex-shrink: 0;
  width: 18px;
  height: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--c-primary);
}

.seller-head .name {
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

.item-line {
  display: flex;
  gap: 10px;
  padding: var(--s2) var(--s3);
}

.item-img {
  flex-shrink: 0;
  width: 72px;
  height: 72px;
  border-radius: var(--r-base);
  overflow: hidden;
  background: var(--n3);
  position: relative;
}

.item-img img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.item-img .invalid-tag {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.45);
  color: #fff;
  font-size: 11px;
}

.item-info {
  flex: 1;
  min-width: 0;
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
}

.item-sku {
  display: inline-flex;
  align-items: center;
  margin-top: 6px;
  padding: 2px 6px;
  background: var(--n3);
  border-radius: var(--r-base);
  font-size: 11px;
  color: var(--n9);
}

.item-bottom {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--s2);
  margin-top: 6px;
}

.item-price {
  font-size: var(--fs-base);
  color: var(--n10);
}

.item-qty {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.item-subtotal {
  font-size: var(--fs-sm);
  color: var(--c-error);
  font-weight: var(--fw-medium);
}

.seller-foot {
  padding: var(--s2) var(--s3) var(--s3);
  border-top: 1px solid var(--n3);
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: var(--s1);
}

.seller-foot .lbl {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 金额明细 */
.amount-list {
  padding: var(--s3);
}

.amount-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s2) 0;
}

.amount-row .lbl {
  font-size: var(--fs-base);
  color: var(--n9);
}

.amount-row .val {
  font-size: var(--fs-base);
  color: var(--n10);
}

.amount-row .val.discount {
  color: var(--c-success);
}

.amount-row.total {
  padding-top: var(--s3);
  border-top: 1px solid var(--n3);
  margin-top: var(--s1);
}

.amount-row.total .lbl {
  color: var(--n10);
  font-weight: var(--fw-medium);
}

/* 底部提交栏 */
.submit-bar {
  height: 56px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s3);
  flex-shrink: 0;
  box-shadow: 0 -2px 12px rgba(0, 0, 0, 0.04);
  padding-bottom: env(safe-area-inset-bottom);
}

.submit-bar .sum-wrap {
  flex: 1;
  min-width: 0;
}

.submit-bar .sum-label {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.submit-bar .sum-detail {
  font-size: 11px;
  color: var(--c-success);
}

.submit-btn {
  height: 44px;
  padding: 0 32px;
  background: linear-gradient(135deg, #ff6a3d 0%, #ff4d4f 100%);
  color: #fff;
  border-radius: 22px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
  box-shadow: 0 4px 12px rgba(255, 77, 79, 0.3);
  transition: all 0.15s;
}

.submit-btn:active {
  transform: scale(0.96);
}
</style>
