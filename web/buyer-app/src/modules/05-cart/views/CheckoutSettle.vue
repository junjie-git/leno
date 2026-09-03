<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { cartApi } from '@/modules/05-cart/api/cart.api'
import { orderApi } from '@/modules/06-order/api/order.api'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import type { CheckoutPreviewDto, CartItemDto } from '@/modules/05-cart/types/cart.dto'
import type { AddressDto } from '@/modules/13-profile/types/profile.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'
import { formatDate, formatPoints, formatPrice, formatPriceExact, maskPhone } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 结算确认页（/checkout/settle）
 *
 * 结构（对齐设计稿 checkout-preview 的表单区 + checkout-settle 提示词）：
 * NavBar（返回 + 结算确认）→ 收货地址卡（默认地址 + 弹层切换 + 无地址 CTA）
 * → 按卖家分组的商品卡片（图片 + 标题 + 规格 + 单价 × 数量 + 组小计）
 * → 优惠卡（优惠券行 + 弹层选择 / 积分抵扣开关）→ 金额明细卡（试算实时刷新）
 * → 订单备注 → 底部提交栏（应付总额 + 提交订单）
 *
 * 进入页面并行加载 cartApi.preview({ from: 'cart' }) 与 orderApi.getAddresses()；
 * 切换地址 / 优惠券 / 积分后调用 cartApi.preview 带 addressId、couponId、usePoints
 * 增量重新试算（请求序号守卫防止旧响应覆盖）；
 * 提交订单 orderApi.create({ addressId, couponId, usePoints, remark }) →
 * 成功后 cartStore.refreshBadge() 并跳 /payment/initiate/{orderId}（多单拆分跳第一个）。
 */

const router = useRouter()
const cartStore = useCartStore()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const empty = ref(false)
const preview = ref<CheckoutPreviewDto | null>(null)
const addresses = ref<AddressDto[]>([])

// ---- 表单状态 ----
const selectedAddressId = ref('')
const couponId = ref<string | null>(null)
const usePoints = ref(false)
const remark = ref('')

// ---- 弹层状态 ----
const couponVisible = ref(false)
const addressVisible = ref(false)

// ---- 试算 / 提交状态 ----
const recalcing = ref(false)
const submitting = ref(false)

/** 试算请求序号（切换条件时旧响应作废） */
let previewSeq = 0

/** 卖家分组 */
const shopGroups = computed(() => preview.value?.shopGroups ?? [])

/** 金额明细（试算实时刷新） */
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

/** 积分抵扣信息 */
const points = computed(
  () =>
    preview.value?.points ?? {
      available: 0,
      maxDeductiblePoints: 0,
      ruleText: '100 积分抵 1 元',
    },
)

/** 可用优惠券 */
const availableCoupons = computed(() => preview.value?.availableCoupons ?? [])

/** 当前选中地址（地址列表 > 试算返回的默认地址） */
const selectedAddress = computed(
  () => addresses.value.find((a) => a.id === selectedAddressId.value) ?? preview.value?.address ?? null,
)

/** 当前选中优惠券 */
const selectedCoupon = computed(
  () => availableCoupons.value.find((c) => c.couponId === couponId.value) ?? null,
)

/** 优惠券行展示文案 */
const couponDisplay = computed(() => {
  if (selectedCoupon.value) {
    if (selectedCoupon.value.type === 'Shipping') return '免运费'
    return `-¥${formatPrice(selectedCoupon.value.discount)}`
  }
  return availableCoupons.value.length > 0 ? `${availableCoupons.value.length} 张可用` : '暂无可用'
})

/** 积分抵扣是否可用（余额或可抵扣额为 0 时置灰） */
const pointsDisabled = computed(
  () => points.value.available <= 0 || points.value.maxDeductiblePoints <= 0,
)

/** 优惠合计（优惠券 + 积分） */
const discountSum = computed(
  () => amounts.value.couponDiscount + amounts.value.pointsDiscount,
)

/** 失效或库存不足的条目数（提示条） */
const invalidCount = computed(
  () => shopGroups.value.flatMap((g) => g.items).filter((i) => i.stock <= 0).length,
)

onMounted(() => {
  void init()
})

/** 初始化：并行加载结算预览与地址列表 */
async function init(): Promise<void> {
  loading.value = true
  loadError.value = false
  empty.value = false
  const seq = ++previewSeq
  try {
    const [previewResult, addressList] = await Promise.all([
      cartApi.preview({ from: 'cart' }),
      orderApi.getAddresses().catch((e: unknown) => {
        logger.warn('地址列表加载失败（忽略）', e)
        return [] as AddressDto[]
      }),
    ])
    if (seq !== previewSeq) return
    preview.value = previewResult
    addresses.value = addressList
    selectedAddressId.value =
      previewResult.address?.id ?? addressList.find((a) => a.isDefault)?.id ?? ''
    couponId.value = null
    usePoints.value = false
  } catch (e) {
    if (seq !== previewSeq) return
    if (e instanceof BusinessError && e.code === 40404) {
      empty.value = true
    } else {
      logger.error('结算确认加载失败', e)
      loadError.value = true
    }
  } finally {
    if (seq === previewSeq) {
      loading.value = false
    }
  }
}

/** 条件变化后增量重新试算（地址 / 优惠券 / 积分） */
watch([selectedAddressId, couponId, usePoints], () => {
  void refreshPreview()
})

async function refreshPreview(): Promise<void> {
  if (loading.value || empty.value || loadError.value) return
  recalcing.value = true
  const seq = ++previewSeq
  try {
    const next = await cartApi.preview({
      from: 'cart',
      addressId: selectedAddressId.value || undefined,
      couponId: couponId.value,
      usePoints: usePoints.value,
    })
    if (seq !== previewSeq) return
    preview.value = next
    // 未显式选择地址时，服务端回填的默认地址与本地选择保持同步
    if (!selectedAddressId.value && next.address) {
      selectedAddressId.value = next.address.id
    }
  } catch (e) {
    if (seq !== previewSeq) return
    logger.warn('结算试算失败', e)
    showFailToast(e instanceof Error ? e.message : '试算失败，请重试')
  } finally {
    if (seq === previewSeq) {
      recalcing.value = false
    }
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

/** 优惠券门槛文案 */
function couponThresholdText(threshold: number): string {
  return threshold > 0 ? `满 ¥${formatPrice(threshold)}可用` : '无门槛'
}

/** 优惠券有效期文案 */
function couponValidText(validTo: string): string {
  return `有效期至 ${formatDate(validTo)}`
}

// ---- 地址弹层 ----

function openAddressPopup(): void {
  addressVisible.value = true
}

function selectAddress(id: string): void {
  selectedAddressId.value = id
  addressVisible.value = false
}

function goAddressManage(): void {
  router.push({ path: '/profile/addresses', query: { from: 'checkout' } })
}

// ---- 优惠券弹层 ----

function openCouponPopup(): void {
  couponVisible.value = true
}

function selectCoupon(id: string | null): void {
  couponId.value = id
  couponVisible.value = false
  if (id) {
    showToast('已使用优惠券')
  } else {
    showToast('已取消使用优惠券')
  }
}

// ---- 提交订单 ----

async function submitOrder(): Promise<void> {
  if (submitting.value || recalcing.value) return
  if (!selectedAddressId.value) {
    showToast('请先选择收货地址')
    return
  }
  submitting.value = true
  try {
    const order = await orderApi.create({
      addressId: selectedAddressId.value,
      couponId: couponId.value,
      usePoints: usePoints.value,
      remark: remark.value.trim() || undefined,
    })
    await cartStore.refreshBadge()
    showToast('订单提交成功')
    // 多单拆分时服务端按卖家生成订单，收银台以第一笔订单发起支付
    router.replace(`/payment/initiate/${order.id}`)
  } catch (e) {
    if (e instanceof BusinessError && e.code === 40404) {
      // 购物车勾选项已被结算（如其他端已下单），转空态
      empty.value = true
      showToast(e.message)
      return
    }
    logger.error('提交订单失败', e)
    showFailToast(e instanceof Error ? e.message : '提交订单失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ---- 其它跳转 ----

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
  <div class="settle-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">结算确认</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="skeletons">
        <div class="skeleton-block sk-addr" />
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
        <div class="skeleton-block sk-form" />
        <div class="skeleton-block sk-amount" />
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="结算加载失败"
        description="网络异常，请稍后重试，或返回购物车检查勾选项"
        @retry="init"
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

        <!-- 收货地址卡 -->
        <section
          v-if="selectedAddress"
          class="card addr-card"
          :aria-label="`收货地址：${selectedAddress.receiver}`"
          @click="openAddressPopup"
        >
          <div class="addr-icon">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round">
              <path d="M12 21s-7-7-7-12a7 7 0 0 1 14 0c0 5-7 12-7 12z" />
              <circle cx="12" cy="9" r="2.5" />
            </svg>
          </div>
          <div class="addr-info">
            <div class="addr-line1">
              <span class="addr-name">{{ selectedAddress.receiver }}</span>
              <span class="addr-phone">{{ maskPhone(selectedAddress.phone) }}</span>
              <span v-if="selectedAddress.isDefault" class="addr-default">默认</span>
              <span v-else-if="selectedAddress.tag" class="addr-tag">{{ selectedAddress.tag }}</span>
            </div>
            <div class="addr-detail">
              {{ selectedAddress.province }}{{ selectedAddress.city }}{{ selectedAddress.district }}{{ selectedAddress.detail }}
            </div>
          </div>
          <div class="addr-arrow">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <path d="M9 6l6 6-6 6" />
            </svg>
          </div>
        </section>
        <!-- 无地址：新增 CTA -->
        <section v-else class="card addr-card addr-empty" @click="goAddressManage">
          <div class="addr-icon">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
              <path d="M12 5v14M5 12h14" />
            </svg>
          </div>
          <div class="addr-info">
            <div class="addr-add-text">请添加收货地址</div>
          </div>
          <div class="addr-arrow">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <path d="M9 6l6 6-6 6" />
            </svg>
          </div>
        </section>

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
            <span class="lbl">本组小计</span>
            <PriceText :amount="groupSubtotal(group.items)" :size="14" />
          </div>
        </section>

        <!-- 优惠卡：优惠券 + 积分抵扣 -->
        <section class="card">
          <div class="cell coupon-cell" role="button" aria-label="选择优惠券" @click="openCouponPopup">
            <span class="lbl">优惠券</span>
            <span class="val" :class="{ discount: selectedCoupon, muted: !selectedCoupon && availableCoupons.length === 0 }">
              {{ couponDisplay }}
            </span>
            <svg class="arrow" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <path d="M9 6l6 6-6 6" />
            </svg>
          </div>
          <div class="cell points-cell">
            <div class="points-main">
              <div class="points-title">
                积分抵扣
                <span v-if="usePoints && amounts.pointsDiscount > 0" class="points-save">
                  -¥{{ formatPriceExact(amounts.pointsDiscount) }}
                </span>
              </div>
              <div class="points-sub">
                <template v-if="pointsDisabled">积分不足，暂不可用</template>
                <template v-else>
                  可用 {{ formatPoints(points.available) }} 积分，本次最多抵
                  {{ formatPoints(points.maxDeductiblePoints) }} 积分（{{ points.ruleText }}）
                </template>
              </div>
            </div>
            <van-switch v-model="usePoints" :disabled="pointsDisabled" size="20" aria-label="积分抵扣开关" />
          </div>
        </section>

        <!-- 金额明细卡（试算实时刷新） -->
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
              <span class="lbl">积分抵扣（{{ formatPoints(points.maxDeductiblePoints) }} 积分）</span>
              <span class="val discount">-¥{{ formatPriceExact(amounts.pointsDiscount) }}</span>
            </div>
            <div class="amount-row">
              <span class="lbl">运费</span>
              <span class="val">{{ amounts.freight > 0 ? `¥${formatPriceExact(amounts.freight)}` : '包邮' }}</span>
            </div>
            <div class="amount-row total">
              <span class="lbl">应付总额</span>
              <PriceText :amount="amounts.payableAmount" :size="20" />
            </div>
          </div>
        </section>

        <!-- 订单备注 -->
        <section class="card">
          <div class="remark-wrap">
            <div class="lbl">订单备注</div>
            <textarea
              v-model="remark"
              class="remark-input"
              placeholder="选填，对订单的特殊说明（50 字以内）"
              maxlength="50"
              aria-label="订单备注"
            />
          </div>
        </section>
      </template>
    </main>

    <!-- 底部提交栏 -->
    <footer v-if="!loading && !loadError && shopGroups.length > 0" class="submit-bar">
      <div class="sum-wrap">
        <div class="sum-label">应付总额</div>
        <PriceText :amount="amounts.payableAmount" :size="20" />
        <div v-if="discountSum > 0" class="sum-detail">已优惠 ¥{{ formatPriceExact(discountSum) }}</div>
      </div>
      <button
        class="submit-btn"
        :class="{ loading: submitting || recalcing }"
        type="button"
        :disabled="submitting || recalcing"
        aria-label="提交订单"
        @click="submitOrder"
      >
        {{ submitting ? '提交中...' : recalcing ? '试算中...' : '提交订单' }}
      </button>
    </footer>

    <!-- 优惠券弹层 -->
    <van-popup
      v-model:show="couponVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="选择优惠券"
      :style="{ maxHeight: '70%' }"
    >
      <div class="coupon-panel">
        <div class="popup-head">
          <span class="t">选择优惠券</span>
          <button class="close" type="button" @click="couponVisible = false">关闭</button>
        </div>
        <div class="popup-body">
          <div
            class="coupon-item"
            :class="{ selected: couponId === null }"
            role="radio"
            :aria-checked="couponId === null"
            @click="selectCoupon(null)"
          >
            <div class="coupon-amount none">
              <div class="v">不使用</div>
            </div>
            <div class="coupon-meta">
              <div class="n">不使用优惠券</div>
              <div class="s">本次结算不抵扣优惠券</div>
            </div>
            <span class="coupon-radio" :class="{ selected: couponId === null }">
              <svg viewBox="0 0 24 24" fill="none">
                <path d="M6 12l4 4 8-9" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </span>
          </div>
          <div
            v-for="coupon in availableCoupons"
            :key="coupon.couponId"
            class="coupon-item"
            :class="{ selected: couponId === coupon.couponId }"
            role="radio"
            :aria-checked="couponId === coupon.couponId"
            @click="selectCoupon(coupon.couponId)"
          >
            <div class="coupon-amount">
              <div class="v">
                <template v-if="coupon.type === 'Shipping'">包邮</template>
                <template v-else><em>¥</em>{{ formatPrice(coupon.discount) }}</template>
              </div>
              <div class="c">{{ couponThresholdText(coupon.threshold) }}</div>
            </div>
            <div class="coupon-meta">
              <div class="n">{{ coupon.name }}</div>
              <div class="s">{{ coupon.type === 'Shipping' ? '免运费券' : '满减现金券' }}</div>
              <div class="d">{{ couponValidText(coupon.validTo) }}</div>
            </div>
            <span class="coupon-radio" :class="{ selected: couponId === coupon.couponId }">
              <svg viewBox="0 0 24 24" fill="none">
                <path d="M6 12l4 4 8-9" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </span>
          </div>
          <div v-if="availableCoupons.length === 0" class="coupon-empty">暂无可用优惠券</div>
        </div>
      </div>
    </van-popup>

    <!-- 地址选择弹层 -->
    <van-popup
      v-model:show="addressVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="选择收货地址"
      :style="{ maxHeight: '70%' }"
    >
      <div class="addr-panel">
        <div class="popup-head">
          <span class="t">选择收货地址</span>
          <button class="close" type="button" @click="addressVisible = false">关闭</button>
        </div>
        <div class="popup-body">
          <div
            v-for="addr in addresses"
            :key="addr.id"
            class="addr-item"
            :class="{ selected: selectedAddressId === addr.id }"
            role="radio"
            :aria-checked="selectedAddressId === addr.id"
            @click="selectAddress(addr.id)"
          >
            <div class="addr-main">
              <div class="addr-line1">
                <span class="addr-name">{{ addr.receiver }}</span>
                <span class="addr-phone">{{ maskPhone(addr.phone) }}</span>
                <span v-if="addr.isDefault" class="addr-default">默认</span>
                <span v-else-if="addr.tag" class="addr-tag">{{ addr.tag }}</span>
              </div>
              <div class="addr-detail">
                {{ addr.province }}{{ addr.city }}{{ addr.district }}{{ addr.detail }}
              </div>
            </div>
            <span class="coupon-radio" :class="{ selected: selectedAddressId === addr.id }">
              <svg viewBox="0 0 24 24" fill="none">
                <path d="M6 12l4 4 8-9" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </span>
          </div>
          <div v-if="addresses.length === 0" class="coupon-empty">暂无收货地址</div>
        </div>
        <div class="addr-foot">
          <button class="addr-manage-btn" type="button" @click="goAddressManage">管理收货地址</button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.settle-page {
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
.sk-addr {
  height: 90px;
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

.sk-form {
  height: 110px;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
}

.sk-amount {
  height: 160px;
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

/* 收货地址卡 */
.addr-card {
  position: relative;
  padding: 14px var(--s3);
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
}

.addr-card::after {
  content: "";
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  height: 3px;
  background: repeating-linear-gradient(
    90deg,
    var(--c-primary) 0,
    var(--c-primary) 12px,
    #fff 12px,
    #fff 18px,
    var(--c-warning) 18px,
    var(--c-warning) 30px,
    #fff 30px,
    #fff 36px,
    var(--c-error) 36px,
    var(--c-error) 48px,
    #fff 48px,
    #fff 54px
  );
  background-size: 54px 100%;
}

.addr-icon {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--c-primary);
  background: #e6f4ff;
  border-radius: 50%;
}

.addr-info {
  flex: 1;
  min-width: 0;
}

.addr-line1 {
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.addr-name {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.addr-phone {
  font-size: var(--fs-base);
  color: var(--n9);
}

.addr-default {
  font-size: 10px;
  padding: 1px 5px;
  background: var(--c-primary);
  color: #fff;
  border-radius: var(--r-base);
}

.addr-tag {
  font-size: 10px;
  padding: 1px 5px;
  background: #e6f4ff;
  color: var(--c-primary);
  border-radius: var(--r-base);
}

.addr-detail {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-top: var(--s1);
  line-height: 1.4;
}

.addr-add-text {
  font-size: var(--fs-base);
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.addr-arrow {
  flex-shrink: 0;
  color: var(--n7);
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

/* 通用 Cell 行 */
.cell {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3);
  border-top: 1px solid var(--n3);
}

.cell:first-child {
  border-top: none;
}

.cell .lbl {
  flex-shrink: 0;
  font-size: var(--fs-base);
  color: var(--n9);
  width: 84px;
}

.coupon-cell {
  cursor: pointer;
}

.coupon-cell .val {
  flex: 1;
  font-size: var(--fs-base);
  text-align: right;
}

.coupon-cell .val.discount {
  color: var(--c-error);
  font-weight: var(--fw-medium);
}

.coupon-cell .val.muted {
  color: var(--n7);
}

.coupon-cell .arrow {
  flex-shrink: 0;
  color: var(--n7);
}

/* 积分抵扣 */
.points-cell {
  align-items: flex-start;
}

.points-main {
  flex: 1;
  min-width: 0;
}

.points-title {
  font-size: var(--fs-base);
  color: var(--n9);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.points-save {
  color: var(--c-success);
  font-weight: var(--fw-medium);
}

.points-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
  line-height: 1.5;
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

/* 备注 */
.remark-wrap {
  padding: var(--s3);
}

.remark-wrap .lbl {
  font-size: var(--fs-base);
  color: var(--n9);
  margin-bottom: var(--s2);
}

.remark-input {
  width: 100%;
  height: 60px;
  padding: var(--s2) 10px;
  border: 1px solid var(--n3);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: var(--n10);
  resize: none;
  outline: none;
  background: var(--n2);
  font-family: inherit;
}

.remark-input:focus {
  border-color: var(--c-primary);
  background: var(--n1);
}

.remark-input::placeholder {
  color: var(--n7);
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

.submit-btn.loading {
  opacity: 0.7;
}

.submit-btn:disabled {
  cursor: not-allowed;
}

/* 弹层通用 */
.popup-head {
  padding: var(--s4) var(--s3) var(--s2);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.popup-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.popup-head .close {
  font-size: var(--fs-sm);
  color: var(--n7);
  padding: var(--s1) var(--s2);
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
}

.popup-body {
  max-height: 52vh;
  overflow-y: auto;
  padding: var(--s3);
}

/* 优惠券弹层 */
.coupon-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s3);
  border: 1px solid var(--n3);
  border-radius: var(--r-base);
  margin-bottom: var(--s2);
  cursor: pointer;
}

.coupon-item.selected {
  border-color: var(--c-primary);
  background: #e6f4ff;
}

.coupon-amount {
  flex-shrink: 0;
  width: 80px;
  text-align: center;
  padding: var(--s2) 0;
  background: linear-gradient(135deg, #ff6a3d 0%, #ff4d4f 100%);
  color: #fff;
  border-radius: var(--r-base);
}

.coupon-amount.none {
  background: var(--n3);
  color: var(--n9);
}

.coupon-amount .v {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
}

.coupon-amount .v em {
  font-style: normal;
  font-size: var(--fs-sm);
}

.coupon-amount .c {
  font-size: 11px;
  opacity: 0.9;
}

.coupon-meta {
  flex: 1;
  min-width: 0;
}

.coupon-meta .n {
  font-size: var(--fs-base);
  color: var(--n10);
}

.coupon-meta .s {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.coupon-meta .d {
  font-size: 11px;
  color: var(--n9);
  margin-top: var(--s1);
}

.coupon-radio {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 2px solid var(--n5);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
}

.coupon-radio.selected {
  background: var(--c-primary);
  border-color: var(--c-primary);
}

.coupon-radio svg {
  display: none;
  width: 12px;
  height: 12px;
}

.coupon-radio.selected svg {
  display: block;
}

.coupon-empty {
  padding: var(--s6) 0;
  text-align: center;
  font-size: var(--fs-base);
  color: var(--n7);
}

/* 地址弹层 */
.addr-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s3);
  border: 1px solid var(--n3);
  border-radius: var(--r-base);
  margin-bottom: var(--s2);
  cursor: pointer;
}

.addr-item.selected {
  border-color: var(--c-primary);
  background: #e6f4ff;
}

.addr-main {
  flex: 1;
  min-width: 0;
}

.addr-foot {
  padding: var(--s2) var(--s3) calc(var(--s4) + env(safe-area-inset-bottom));
}

.addr-manage-btn {
  width: 100%;
  height: 40px;
  border: 1px solid var(--c-primary);
  color: var(--c-primary);
  background: var(--n1);
  border-radius: 20px;
  font-size: var(--fs-base);
  cursor: pointer;
  font-family: inherit;
}
</style>
