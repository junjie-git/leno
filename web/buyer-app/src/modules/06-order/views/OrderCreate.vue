<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { orderApi } from '@/modules/06-order/api/order.api'
import type { AddressDto } from '@/modules/13-profile/types/profile.dto'
import type { CheckoutPreviewDto } from '@/modules/05-cart/types/cart.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatPoints, formatPriceExact, maskPhone } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 立即购买下单页（/order/create）
 *
 * 结构（对齐设计稿 order-create）：
 * NavBar（返回 / 确认订单）→ 收货地址卡（主色左侧色条 + 收件人/电话/标签/完整地址，点击弹层切换）
 * → 商品卡（店铺头 + 图/标题/规格/单价 + 数量步进器，可售库存为上限）
 * → 积分抵扣（可用积分/最多抵扣/规则文案 + 开关，切换后服务端重新试算）
 * → 金额明细（商品总额/优惠券抵扣/积分抵扣/运费/应付总额）
 * → 底部固定提交栏（应付总额 + 提交订单，loading + 序号守卫防重复提交）
 *
 * 数据流：上游 ProductDetail 以 query { from: 'buyNow', skuId, quantity } 跳入；
 * orderApi.preview 服务端试算（数量/地址/积分变化后重新试算）；
 * 提交 orderApi.buyNow 成功后跳 /payment/initiate/:orderId。
 */

const route = useRoute()
const router = useRouter()

// ---- 路由参数（立即购买：skuId + quantity）----
const skuId = typeof route.query.skuId === 'string' ? route.query.skuId : ''
const quantityQuery = Number(route.query.quantity ?? 1)
const initialQuantity = Number.isFinite(quantityQuery) && quantityQuery >= 1 ? Math.floor(quantityQuery) : 1

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const invalidParam = ref(false)

// ---- 地址 ----
const addresses = ref<AddressDto[]>([])
const selectedAddressId = ref('')
const addressVisible = ref(false)

// ---- 试算与提交 ----
const preview = ref<CheckoutPreviewDto | null>(null)
const quantity = ref(initialQuantity)
const usePoints = ref(false)
const calculating = ref(false)
const submitting = ref(false)

/** 试算请求序号（数量/积分/地址变化时旧响应作废） */
let previewSeq = 0

// ---- 派生数据 ----
const selectedAddress = computed(
  () => addresses.value.find((a) => a.id === selectedAddressId.value) ?? preview.value?.address ?? null,
)

const previewItem = computed(() => preview.value?.shopGroups.flatMap((g) => g.items)[0] ?? null)
const shopName = computed(() => preview.value?.shopGroups[0]?.shopName ?? '')
const pointsInfo = computed(() => preview.value?.points)
const maxQuantity = computed(() => Math.max(1, previewItem.value?.stock ?? 1))

const amountsView = computed(() => ({
  goodsAmount: preview.value?.amounts.goodsAmount ?? 0,
  freight: preview.value?.amounts.freight ?? 0,
  couponDiscount: preview.value?.amounts.couponDiscount ?? 0,
  pointsDiscount: preview.value?.amounts.pointsDiscount ?? 0,
  payableAmount: preview.value?.amounts.payableAmount ?? 0,
}))

/** 地址完整文案（省市区 + 详细地址） */
function fullAddress(addr: AddressDto): string {
  return `${addr.province}${addr.city}${addr.district}${addr.detail}`
}

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  if (!skuId) {
    invalidParam.value = true
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = false
  try {
    const [previewResult, addressList] = await Promise.all([
      orderApi.preview({
        addressId: selectedAddressId.value,
        couponId: null,
        usePoints: false,
        from: 'buyNow',
        skuId,
        quantity: quantity.value,
      }),
      orderApi.getAddresses().catch((e: unknown) => {
        logger.warn('地址列表加载失败（使用试算返回的默认地址）', e)
        return [] as AddressDto[]
      }),
    ])
    preview.value = previewResult
    addresses.value = addressList
    const fallback =
      addressList.find((a) => a.id === previewResult.address?.id) ??
      addressList.find((a) => a.isDefault) ??
      addressList[0]
    selectedAddressId.value = fallback?.id ?? ''
    // 上游带入数量超出可售库存时收敛并重新试算
    const firstItem = previewResult.shopGroups.flatMap((g) => g.items)[0]
    if (firstItem && quantity.value > firstItem.stock) {
      quantity.value = Math.max(1, firstItem.stock)
      await refreshPreview()
    }
  } catch (e) {
    logger.error('下单页加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadAll()
})

/** 服务端试算（数量/积分/地址变化后调用，金额以服务端为准） */
async function refreshPreview(): Promise<void> {
  if (!skuId) return
  const seq = ++previewSeq
  calculating.value = true
  try {
    const result = await orderApi.preview({
      addressId: selectedAddressId.value,
      couponId: null,
      usePoints: usePoints.value,
      from: 'buyNow',
      skuId,
      quantity: quantity.value,
    })
    if (seq !== previewSeq) return
    preview.value = result
  } catch (e) {
    if (seq !== previewSeq) return
    logger.error('下单试算失败', e)
    showFailToast(e instanceof Error ? e.message : '金额试算失败，请稍后重试')
  } finally {
    if (seq === previewSeq) {
      calculating.value = false
    }
  }
}

// ---- 数量 ----
function onQuantityChange(): void {
  void refreshPreview()
}

function onStepOverlimit(action: 'plus' | 'minus'): void {
  showToast(action === 'plus' ? '已达库存上限' : '至少购买 1 件')
}

// ---- 积分开关 ----
function onPointsToggle(): void {
  void refreshPreview()
}

// ---- 地址选择 ----
function openAddressPopup(): void {
  if (addresses.value.length === 0) {
    showToast('暂无可用地址，请先在个人中心添加')
    return
  }
  addressVisible.value = true
}

function chooseAddress(addr: AddressDto): void {
  if (addr.id !== selectedAddressId.value) {
    selectedAddressId.value = addr.id
    void refreshPreview()
  }
  addressVisible.value = false
}

// ---- 提交订单 ----
async function submitOrder(): Promise<void> {
  if (submitting.value || calculating.value) return
  if (!previewItem.value) {
    showToast('商品信息尚未加载完成')
    return
  }
  const addressId = selectedAddressId.value || preview.value?.address?.id || ''
  if (!addressId) {
    showToast('请先选择收货地址')
    return
  }
  submitting.value = true
  try {
    const order = await orderApi.buyNow({
      skuId,
      quantity: quantity.value,
      addressId,
      couponId: null,
      usePoints: usePoints.value,
    })
    showToast('订单提交成功')
    router.replace(`/payment/initiate/${order.id}`)
  } catch (e) {
    logger.error('立即购买下单失败', e)
    showFailToast(e instanceof Error ? e.message : '下单失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}

// ---- 导航 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}
</script>

<template>
  <div class="create-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">确认订单</div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-address" />
      <div class="skeleton-block sk-product" />
      <div class="skeleton-block sk-points" />
      <div class="skeleton-block sk-amount" />
    </main>

    <!-- 参数错误 / 加载失败 -->
    <main v-else-if="invalidParam || loadError" class="body">
      <ErrorState
        :title="invalidParam ? '页面参数错误' : '加载失败'"
        :description="invalidParam ? '缺少商品参数，请从商品详情页重新发起购买' : '网络异常，请稍后重试'"
        :retry-text="invalidParam ? '返回上一页' : '重新加载'"
        @retry="invalidParam ? goBack() : loadAll()"
      />
    </main>

    <!-- 内容 -->
    <main v-else class="body">
      <!-- 收货地址卡 -->
      <section
        v-if="selectedAddress"
        class="address-card"
        role="button"
        aria-label="切换收货地址"
        @click="openAddressPopup"
      >
        <svg class="addr-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
          <circle cx="12" cy="10" r="3" />
        </svg>
        <div class="addr-content">
          <div class="addr-head">
            <span class="recipient">{{ selectedAddress.receiver }}</span>
            <span class="phone">{{ maskPhone(selectedAddress.phone) }}</span>
            <span v-if="selectedAddress.tag" class="tag">{{ selectedAddress.tag }}</span>
          </div>
          <div class="addr-detail">{{ fullAddress(selectedAddress) }}</div>
        </div>
        <svg class="arrow-right" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M9 6l6 6-6 6" />
        </svg>
      </section>
      <section v-else class="address-card address-empty" role="button" aria-label="选择收货地址" @click="openAddressPopup">
        <span class="empty-plus">+</span>
        <span class="empty-text">请选择收货地址</span>
        <svg class="arrow-right" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M9 6l6 6-6 6" />
        </svg>
      </section>

      <!-- 商品卡 -->
      <section v-if="previewItem" class="section">
        <div class="section-title">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 9l1-5h16l1 5" />
            <path d="M4 9v11a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
          </svg>
          {{ shopName }}
        </div>
        <div class="product-card">
          <div class="product-row">
            <img class="product-img" :src="previewItem.image" :alt="previewItem.name" loading="lazy">
            <div class="product-info">
              <div class="product-title">{{ previewItem.name }}</div>
              <div class="product-spec">{{ previewItem.specs }}</div>
              <div class="product-price-row">
                <PriceText :amount="previewItem.price" :size="16" />
              </div>
            </div>
          </div>
        </div>
        <div class="stepper-row">
          <span class="stepper-label">购买数量（库存 {{ previewItem.stock }} 件）</span>
          <van-stepper
            v-model="quantity"
            :min="1"
            :max="maxQuantity"
            disable-input
            @change="onQuantityChange"
            @overlimit="onStepOverlimit"
          />
        </div>
      </section>

      <!-- 积分抵扣 -->
      <section class="section">
        <div class="points-row">
          <div class="points-info">
            <span class="points-label">积分抵扣</span>
            <span class="points-desc">
              可用 {{ formatPoints(pointsInfo?.available ?? 0) }} 积分，最多抵
              <span class="points-value">¥{{ formatPriceExact(pointsInfo?.maxDeductiblePoints ?? 0) }}</span>
            </span>
          </div>
          <van-switch
            v-model="usePoints"
            :disabled="(pointsInfo?.maxDeductiblePoints ?? 0) <= 0"
            size="22px"
            @change="onPointsToggle"
          />
        </div>
        <div v-if="usePoints && pointsInfo" class="points-tip">
          {{ pointsInfo.ruleText }}，本单抵扣 ¥{{ formatPriceExact(amountsView.pointsDiscount) }}
        </div>
      </section>

      <!-- 金额明细 -->
      <section class="section">
        <div class="section-title">金额明细</div>
        <div class="amount-list">
          <div class="amount-row">
            <span>商品总额</span>
            <span class="val">¥{{ formatPriceExact(amountsView.goodsAmount) }}</span>
          </div>
          <div v-if="amountsView.couponDiscount > 0" class="amount-row discount">
            <span>优惠券抵扣</span>
            <span class="val">-¥{{ formatPriceExact(amountsView.couponDiscount) }}</span>
          </div>
          <div v-if="amountsView.pointsDiscount > 0" class="amount-row discount">
            <span>积分抵扣</span>
            <span class="val">-¥{{ formatPriceExact(amountsView.pointsDiscount) }}</span>
          </div>
          <div class="amount-row">
            <span>运费（预估）</span>
            <span class="val">¥{{ formatPriceExact(amountsView.freight) }}</span>
          </div>
          <div class="amount-row total">
            <span class="label">应付总额</span>
            <PriceText :amount="amountsView.payableAmount" :size="20" />
          </div>
        </div>
      </section>
    </main>

    <!-- 底部提交栏 -->
    <footer v-if="preview" class="submit-bar">
      <div class="submit-total">
        <span class="label">应付总额</span>
        <PriceText :amount="amountsView.payableAmount" :size="20" />
      </div>
      <button
        class="submit-btn"
        :class="{ loading: submitting || calculating }"
        type="button"
        :disabled="submitting || calculating || !previewItem"
        @click="submitOrder"
      >
        <span v-if="submitting" class="spinner" />
        {{ submitting ? '提交中' : '提交订单' }}
      </button>
    </footer>

    <!-- 地址选择弹层 -->
    <van-popup v-model:show="addressVisible" position="bottom" round role="dialog" aria-label="选择收货地址">
      <div class="address-panel">
        <div class="panel-head">
          <span class="t">选择收货地址</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="addressVisible = false" />
        </div>
        <div class="panel-body">
          <div
            v-for="addr in addresses"
            :key="addr.id"
            class="addr-row"
            :class="{ on: addr.id === selectedAddressId }"
            role="button"
            aria-label="选择地址"
            @click="chooseAddress(addr)"
          >
            <div class="addr-main">
              <div class="addr-head">
                <span class="recipient">{{ addr.receiver }}</span>
                <span class="phone">{{ maskPhone(addr.phone) }}</span>
                <span v-if="addr.tag" class="tag">{{ addr.tag }}</span>
              </div>
              <div class="addr-detail">{{ fullAddress(addr) }}</div>
            </div>
            <svg v-if="addr.id === selectedAddressId" class="check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M20 6L9 17l-5-5" />
            </svg>
          </div>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.create-page {
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
  background: var(--n3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.sk-address {
  height: 88px;
  margin: var(--s3);
}

.sk-product {
  height: 176px;
  margin: var(--s3);
}

.sk-points {
  height: 72px;
  margin: var(--s3);
}

.sk-amount {
  height: 168px;
  margin: var(--s3);
}

/* 收货地址卡 */
.address-card {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  position: relative;
  overflow: hidden;
  cursor: pointer;
  display: flex;
  align-items: flex-start;
  gap: var(--s2);
}

.address-card::before {
  content: "";
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background: var(--c-primary);
}

.addr-icon {
  width: 20px;
  height: 20px;
  color: var(--c-primary);
  flex-shrink: 0;
  margin-top: 2px;
}

.addr-content {
  flex: 1;
  min-width: 0;
  padding-right: var(--s4);
}

.addr-head {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s1);
}

.recipient {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.phone {
  font-size: var(--fs-base);
  color: var(--n9);
}

.tag {
  font-size: 10px;
  color: var(--c-primary);
  border: 1px solid var(--c-primary);
  border-radius: 3px;
  padding: 0 var(--s1);
}

.addr-detail {
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
}

.arrow-right {
  width: 16px;
  height: 16px;
  color: var(--n7);
  flex-shrink: 0;
  align-self: center;
}

.address-empty {
  align-items: center;
  color: var(--c-primary);
  font-size: var(--fs-base);
}

.empty-plus {
  font-size: var(--fs-xl);
  font-weight: var(--fw-normal);
  line-height: 1;
}

.empty-text {
  flex: 1;
}

/* 卡片区块 */
.section {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.section-title {
  padding: var(--s3) var(--s3) var(--s2);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: 6px;
}

.section-title svg {
  width: 16px;
  height: 16px;
  color: var(--c-primary);
}

/* 商品卡 */
.product-card {
  padding: 0 var(--s3);
}

.product-row {
  display: flex;
  gap: var(--s2);
}

.product-img {
  width: 96px;
  height: 96px;
  border-radius: var(--r-base);
  background: var(--n3);
  flex-shrink: 0;
  object-fit: cover;
}

.product-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.product-title {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.product-spec {
  font-size: var(--fs-sm);
  color: var(--n7);
  background: var(--n3);
  padding: 2px 6px;
  border-radius: var(--r-base);
  align-self: flex-start;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-price-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
}

/* 数量步进 */
.stepper-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s2) var(--s3) var(--s3);
  border-top: 1px solid var(--n3);
  margin-top: var(--s2);
}

.stepper-label {
  flex: 1;
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* 积分抵扣 */
.points-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3);
}

.points-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.points-label {
  font-size: var(--fs-base);
  color: var(--n10);
}

.points-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.points-value {
  color: var(--c-success);
  font-weight: var(--fw-medium);
}

.points-tip {
  padding: 0 var(--s3) var(--s3);
  font-size: var(--fs-sm);
  color: var(--n7);
  line-height: 1.6;
}

/* 金额明细 */
.amount-list {
  padding: var(--s2) var(--s3) var(--s3);
}

.amount-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  font-size: var(--fs-base);
  color: var(--n9);
}

.amount-row .val {
  color: var(--n10);
}

.amount-row.discount .val {
  color: var(--c-success);
}

.amount-row.total {
  border-top: 1px dashed var(--n5);
  margin-top: var(--s2);
  padding-top: var(--s3);
}

.amount-row.total .label {
  color: var(--n10);
  font-weight: var(--fw-medium);
}

/* 底部提交栏 */
.submit-bar {
  height: 50px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s3);
  padding-bottom: env(safe-area-inset-bottom);
}

.submit-total {
  display: flex;
  flex-direction: column;
  justify-content: center;
  flex: 1;
  gap: 1px;
}

.submit-total .label {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.submit-btn {
  height: 40px;
  padding: 0 32px;
  border-radius: 20px;
  background: var(--c-primary);
  color: #fff;
  border: none;
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  font-family: inherit;
  transition: opacity var(--d-mid) var(--ease-std);
}

.submit-btn:active {
  opacity: 0.85;
}

.submit-btn.loading,
.submit-btn:disabled {
  opacity: 0.6;
  pointer-events: none;
}

.spinner {
  width: 14px;
  height: 14px;
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

/* 地址选择弹层 */
.address-panel {
  display: flex;
  flex-direction: column;
  max-height: 60vh;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s4) var(--s4) var(--s2);
}

.panel-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
}

.addr-row {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3) 0;
  border-bottom: 1px solid var(--n3);
  cursor: pointer;
}

.addr-row:last-child {
  border-bottom: none;
}

.addr-row.on .addr-detail {
  color: var(--c-primary);
}

.addr-main {
  flex: 1;
  min-width: 0;
}

.check {
  width: 18px;
  height: 18px;
  color: var(--c-primary);
  flex-shrink: 0;
}
</style>
