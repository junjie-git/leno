<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { seckillApi } from '@/modules/08-promotion/api/seckill.api'
import { orderApi } from '@/modules/06-order/api/order.api'
import type { AddressDto } from '@/modules/13-profile/types/profile.dto'
import type { SeckillActivityDto, SeckillActivityStatus } from '@/modules/08-promotion/types/promotion.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatPrice, formatPriceExact, maskPhone } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 秒杀下单页（/seckill/order/:activityId）
 *
 * 结构（对齐设计稿 seckill-order）：
 * NavBar（返回 / 火焰图标 + 秒杀下单）→ 倒计时横幅（红渐变，距开始/距结束实时倒数，结束自动禁抢）
 * → 库存条（剩余库存 + 限购说明，10 秒轮询刷新实时库存）
 * → 秒杀商品卡（图 + 秒杀角标 + 标题 + 秒杀价/划线原价/直降标签 + SKU 规格选择 + 数量步进，上限为限购与库存的较小值）
 * → 收货地址卡（点击弹层切换，红左侧色条）
 * → 金额明细（商品总额/秒杀优惠/运费/应付总额）→ 规则提示
 * → 底部固定抢购栏（秒杀价 + 已省 + 立即抢购红渐变按钮，防重复提交）
 *
 * 数据流：HomeFeed 首页秒杀入口跳入（activityId）；
 * seckillApi.getActivity 拉活动详情（含实时库存），每 10 秒静默轮询；
 * 提交 seckillApi.place(activityId, { skuId, quantity, addressId }) 成功跳 /payment/initiate/:orderId。
 */

const route = useRoute()
const router = useRouter()

const activityId = String(route.params.activityId ?? '')

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const submitting = ref(false)

// ---- 活动与地址 ----
const activity = ref<SeckillActivityDto | null>(null)
const addresses = ref<AddressDto[]>([])
const selectedAddressId = ref('')
const addressVisible = ref(false)

// ---- 选择 ----
const selectedSkuId = ref('')
const quantity = ref(1)

// ---- 倒计时与轮询 ----
const now = ref(Date.now())
const countdown = ref({ h: '00', m: '00', s: '00' })
let tickTimer: ReturnType<typeof setInterval> | null = null
let pollTimer: ReturnType<typeof setInterval> | null = null

// ---- 派生数据 ----
/** 当前选中秒杀商品（SKU） */
const selectedItem = computed(() => activity.value?.items.find((i) => i.skuId === selectedSkuId.value) ?? null)

/** 实时活动状态（基于 startTime/endTime 与当前时间计算） */
const liveStatus = computed<SeckillActivityStatus>(() => {
  const a = activity.value
  if (!a) return 'Ended'
  const start = new Date(a.startTime).getTime()
  const end = new Date(a.endTime).getTime()
  if (Number.isNaN(start) || Number.isNaN(end)) return 'Ended'
  if (now.value < start) return 'Upcoming'
  if (now.value > end) return 'Ended'
  return 'Active'
})

/** 倒计时目标时间（未开始 → 开始时间；进行中 → 结束时间） */
const countdownTarget = computed(() => {
  const a = activity.value
  if (!a) return 0
  const target = liveStatus.value === 'Upcoming' ? new Date(a.startTime).getTime() : new Date(a.endTime).getTime()
  return Number.isNaN(target) ? 0 : target
})

/** 倒计时标签文案 */
const countdownLabel = computed(() => {
  if (liveStatus.value === 'Upcoming') return '距活动开始'
  if (liveStatus.value === 'Active') return '距活动结束仅剩'
  return ''
})

/** 数量上限（限购与剩余库存取小） */
const maxQuantity = computed(() => {
  const item = selectedItem.value
  if (!item) return 1
  return Math.max(1, Math.min(item.limitPerUser, item.stock))
})

/** 金额（分） */
const goodsAmount = computed(() => (selectedItem.value ? selectedItem.value.originalPrice * quantity.value : 0))
const saveAmount = computed(() =>
  selectedItem.value ? Math.max(0, selectedItem.value.originalPrice - selectedItem.value.seckillPrice) * quantity.value : 0,
)
const payableAmount = computed(() => (selectedItem.value ? selectedItem.value.seckillPrice * quantity.value : 0))

/** 抢购按钮可用与文案 */
const submitDisabled = computed(() => {
  if (liveStatus.value !== 'Active') return true
  return !selectedItem.value || selectedItem.value.stock <= 0
})

const submitText = computed(() => {
  if (submitting.value) return '抢购中'
  if (liveStatus.value === 'Upcoming') return '即将开始'
  if (liveStatus.value === 'Ended') return '活动已结束'
  if (!selectedItem.value || selectedItem.value.stock <= 0) return '已抢完'
  return '立即抢购'
})

const selectedAddress = computed(() => addresses.value.find((a) => a.id === selectedAddressId.value) ?? null)

/** 地址完整文案（省市区 + 详细地址） */
function fullAddress(addr: AddressDto): string {
  return `${addr.province}${addr.city}${addr.district}${addr.detail}`
}

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  if (!activityId) {
    loadError.value = true
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = false
  try {
    const [act, addrList] = await Promise.all([
      seckillApi.getActivity(activityId),
      orderApi.getAddresses().catch((e: unknown) => {
        logger.warn('地址列表加载失败（忽略）', e)
        return [] as AddressDto[]
      }),
    ])
    activity.value = act
    addresses.value = addrList
    const def = addrList.find((a) => a.isDefault) ?? addrList[0]
    selectedAddressId.value = def?.id ?? ''
    const first = act.items.find((i) => i.stock > 0) ?? act.items[0]
    selectedSkuId.value = first?.skuId ?? ''
    quantity.value = 1
  } catch (e) {
    logger.error('秒杀活动加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadAll()
  tickTimer = setInterval(tick, 1000)
  pollTimer = setInterval(() => void pollActivity(), 10_000)
})

onBeforeUnmount(() => {
  if (tickTimer) {
    clearInterval(tickTimer)
    tickTimer = null
  }
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
})

/** 每秒心跳：刷新当前时间（联动状态与倒计时） */
function tick(): void {
  now.value = Date.now()
  const remain = Math.max(0, countdownTarget.value - now.value)
  const total = Math.floor(remain / 1000)
  countdown.value = {
    h: String(Math.floor(total / 3600)).padStart(2, '0'),
    m: String(Math.floor((total % 3600) / 60)).padStart(2, '0'),
    s: String(total % 60).padStart(2, '0'),
  }
}

/** 10 秒轮询：静默刷新实时库存（失败保留当前数据） */
async function pollActivity(): Promise<void> {
  if (!activityId || loading.value) return
  try {
    activity.value = await seckillApi.getActivity(activityId)
  } catch (e) {
    logger.warn('秒杀库存轮询失败（忽略）', e)
  }
}

// 活动结束时提示一次
watch(liveStatus, (status, prev) => {
  if (status === 'Ended' && prev === 'Active') {
    showToast('活动已结束')
  }
})

// 库存收紧时收敛数量
watch(maxQuantity, (max) => {
  if (quantity.value > max) {
    quantity.value = max
  }
})

// ---- 选择交互 ----
function selectSku(skuId: string): void {
  if (selectedSkuId.value === skuId) return
  selectedSkuId.value = skuId
  quantity.value = 1
}

function onStepOverlimit(action: 'plus' | 'minus'): void {
  const item = selectedItem.value
  if (action === 'plus') {
    showToast(item && item.stock < item.limitPerUser ? `仅剩 ${item.stock} 件` : `每人限购 ${item?.limitPerUser ?? 1} 件`)
  } else {
    showToast('至少购买 1 件')
  }
}

function openAddressPopup(): void {
  if (addresses.value.length === 0) {
    showToast('暂无可用地址，请先在个人中心添加')
    return
  }
  addressVisible.value = true
}

function chooseAddress(addr: AddressDto): void {
  selectedAddressId.value = addr.id
  addressVisible.value = false
}

// ---- 抢购提交 ----
async function submitSeckill(): Promise<void> {
  if (submitting.value) return
  if (submitDisabled.value) {
    if (liveStatus.value === 'Upcoming') {
      showToast('活动尚未开始')
    } else if (liveStatus.value === 'Ended') {
      showToast('活动已结束')
    } else {
      showToast('该商品已抢完')
    }
    return
  }
  const item = selectedItem.value
  if (!item) return
  if (!selectedAddressId.value) {
    showToast('请先选择收货地址')
    return
  }
  submitting.value = true
  try {
    const order = await seckillApi.place(activityId, {
      skuId: item.skuId,
      quantity: quantity.value,
      addressId: selectedAddressId.value,
    })
    showToast('抢购成功')
    router.replace(`/payment/initiate/${order.id}`)
  } catch (e) {
    logger.warn('秒杀下单失败', e)
    showFailToast(e instanceof Error ? e.message : '抢购失败，请稍后重试')
    // 失败后立即刷新库存（可能已被抢空）
    void pollActivity()
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

function goHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="seckill-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">
        <svg class="fire" viewBox="0 0 24 24" fill="currentColor">
          <path d="M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z" />
        </svg>
        秒杀下单
      </div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-banner" />
      <div class="skeleton-block sk-stock" />
      <div class="skeleton-block sk-product" />
      <div class="skeleton-block sk-amount" />
    </main>

    <!-- 错误态（活动不存在 / 加载失败） -->
    <main v-else-if="loadError || !activity" class="body">
      <ErrorState
        title="秒杀活动加载失败"
        description="活动可能不存在或已结束，请返回首页查看其他活动"
        :retry-text="'返回首页'"
        @retry="goHome"
      />
    </main>

    <!-- 内容 -->
    <main v-else class="body">
      <!-- 倒计时横幅 -->
      <section class="countdown-banner" :class="{ ended: liveStatus === 'Ended' }">
        <svg class="fire-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z" />
        </svg>
        <span v-if="countdownLabel" class="cd-label">{{ countdownLabel }}</span>
        <div v-if="liveStatus !== 'Ended'" class="cd-time" aria-label="活动倒计时">
          <span class="cd-num">{{ countdown.h }}</span>
          <span class="cd-sep">:</span>
          <span class="cd-num">{{ countdown.m }}</span>
          <span class="cd-sep">:</span>
          <span class="cd-num">{{ countdown.s }}</span>
        </div>
        <span v-else class="cd-label">活动已结束</span>
      </section>

      <!-- 库存与限购 -->
      <div class="stock-wrap">
        <div class="stock-text">
          <span>剩余 <span class="sold">{{ selectedItem?.stock ?? 0 }}</span> 件</span>
          <span v-if="selectedItem">每人限购 {{ selectedItem.limitPerUser }} 件</span>
        </div>
      </div>

      <!-- 秒杀商品卡 -->
      <section v-if="selectedItem" class="section">
        <div class="section-title">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 9l1-5h16l1 5" />
            <path d="M4 9v11a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
          </svg>
          秒杀商品
          <span class="tag-hot">HOT</span>
        </div>
        <div class="seckill-product">
          <div class="product-row">
            <div class="product-img-wrap">
              <img class="product-img" :src="selectedItem.image" :alt="selectedItem.name" loading="lazy">
              <span class="seckill-badge">秒杀</span>
            </div>
            <div class="product-info">
              <div class="product-title">{{ selectedItem.name }}</div>
              <div>
                <div class="price-block">
                  <PriceText :amount="selectedItem.seckillPrice" :size="24" />
                  <span class="original-price">¥{{ formatPrice(selectedItem.originalPrice) }}</span>
                </div>
                <div v-if="saveAmount > 0" class="discount-tag">直降 ¥{{ formatPrice(saveAmount) }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- SKU 选择 -->
        <div class="sku-section">
          <div class="sku-label">选择规格</div>
          <div class="sku-list" role="radiogroup" aria-label="选择规格">
            <button
              v-for="item in activity.items"
              :key="item.skuId"
              class="sku-item"
              :class="{ selected: item.skuId === selectedSkuId }"
              type="button"
              :disabled="item.stock <= 0"
              @click="selectSku(item.skuId)"
            >
              {{ item.specs }}（剩{{ item.stock }}件）
            </button>
          </div>
        </div>

        <!-- 数量 -->
        <div class="stepper-row">
          <div class="stepper-label">
            购买数量
            <span class="limit">（每人限购 {{ selectedItem.limitPerUser }} 件）</span>
          </div>
          <van-stepper
            v-model="quantity"
            :min="1"
            :max="maxQuantity"
            disable-input
            @overlimit="onStepOverlimit"
          />
        </div>
      </section>

      <!-- 收货地址 -->
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

      <!-- 金额明细 -->
      <section class="section">
        <div class="section-title">金额明细</div>
        <div class="amount-list">
          <div class="amount-row">
            <span>商品总额</span>
            <span class="val">¥{{ formatPriceExact(goodsAmount) }}</span>
          </div>
          <div v-if="saveAmount > 0" class="amount-row discount">
            <span>秒杀优惠</span>
            <span class="val">-¥{{ formatPriceExact(saveAmount) }}</span>
          </div>
          <div class="amount-row">
            <span>运费（秒杀包邮）</span>
            <span class="val">¥0.00</span>
          </div>
          <div class="amount-row total">
            <span class="label">应付总额</span>
            <PriceText :amount="payableAmount" :size="20" />
          </div>
        </div>
      </section>

      <!-- 规则提示 -->
      <div class="rule-tip">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10" />
          <path d="M12 16v-4" />
          <path d="M12 8h.01" />
        </svg>
        <span>秒杀商品下单后需在 15 分钟内完成支付，超时订单将自动取消。秒杀价不与优惠券、积分抵扣同享，库存以实际支付顺序为准。</span>
      </div>
    </main>

    <!-- 底部抢购栏 -->
    <footer v-if="activity" class="seckill-bar">
      <div class="bar-total">
        <span class="label">秒杀价</span>
        <PriceText :amount="payableAmount" :size="20" />
        <span v-if="saveAmount > 0" class="save">已省 ¥{{ formatPrice(saveAmount) }}</span>
      </div>
      <button
        class="seckill-btn"
        :class="{ disabled: submitDisabled }"
        type="button"
        :disabled="submitDisabled || submitting"
        @click="submitSeckill"
      >
        <span v-if="submitting" class="spinner" />
        {{ submitText }}
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
.seckill-page {
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
  display: flex;
  align-items: center;
  gap: 6px;
}

.nav-title .fire {
  width: 16px;
  height: 16px;
  color: var(--c-error);
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  background: var(--n3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.sk-banner {
  height: 72px;
}

.sk-stock {
  height: 48px;
  margin: var(--s3);
  border-radius: 0;
}

.sk-product {
  height: 320px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

.sk-amount {
  height: 176px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

/* 倒计时横幅 */
.countdown-banner {
  background: linear-gradient(135deg, #ff4d4f 0%, #ff7875 50%, #ffa39e 100%);
  padding: var(--s4);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s3);
  position: relative;
  overflow: hidden;
}

.countdown-banner::after {
  content: "";
  position: absolute;
  inset: 0;
  background: radial-gradient(circle at 20% 50%, rgba(255, 255, 255, 0.15), transparent 40%);
  pointer-events: none;
}

.countdown-banner.ended {
  background: linear-gradient(135deg, #8c8c8c 0%, #bfbfbf 100%);
}

.fire-icon {
  width: 24px;
  height: 24px;
  flex-shrink: 0;
}

.cd-label {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  opacity: 0.95;
}

.cd-time {
  display: flex;
  align-items: center;
  gap: 4px;
  font-family: var(--ff-mono);
}

.cd-num {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 28px;
  height: 28px;
  padding: 0 6px;
  background: rgba(0, 0, 0, 0.25);
  border-radius: var(--r-base);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: #fff;
}

.cd-sep {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  opacity: 0.9;
}

/* 库存与限购 */
.stock-wrap {
  background: var(--n1);
  padding: var(--s3);
  border-bottom: 1px solid var(--n3);
}

.stock-text {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.stock-text .sold {
  color: var(--c-error);
  font-weight: var(--fw-medium);
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

.tag-hot {
  margin-left: auto;
  font-size: 10px;
  color: #fff;
  background: var(--c-error);
  padding: 1px 6px;
  border-radius: 3px;
  font-weight: var(--fw-normal);
}

/* 秒杀商品卡 */
.seckill-product {
  padding: 0 var(--s3);
}

.product-row {
  display: flex;
  gap: var(--s3);
}

.product-img-wrap {
  position: relative;
  flex-shrink: 0;
}

.product-img {
  width: 110px;
  height: 110px;
  border-radius: var(--r-base);
  background: var(--n3);
  object-fit: cover;
}

.seckill-badge {
  position: absolute;
  top: 0;
  left: 0;
  background: linear-gradient(135deg, #ff4d4f, #cf1322);
  color: #fff;
  font-size: 10px;
  padding: 2px 6px;
  border-radius: var(--r-base) 0 var(--r-base) 0;
  font-weight: var(--fw-medium);
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

.price-block {
  display: flex;
  align-items: baseline;
  gap: var(--s2);
}

.original-price {
  font-size: var(--fs-sm);
  color: var(--n7);
  text-decoration: line-through;
}

.discount-tag {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  font-size: var(--fs-sm);
  color: var(--c-error);
  background: #fff1f0;
  border: 1px solid #ffccc7;
  border-radius: var(--r-base);
  padding: 1px 6px;
  align-self: flex-start;
  margin-top: 6px;
}

/* SKU 选择 */
.sku-section {
  padding: 0 var(--s3) var(--s3);
}

.sku-label {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-bottom: var(--s2);
}

.sku-list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}

.sku-item {
  padding: 6px 12px;
  border: 1px solid var(--n5);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: var(--n9);
  cursor: pointer;
  background: var(--n1);
  font-family: inherit;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sku-item.selected {
  border-color: var(--c-error);
  color: var(--c-error);
  background: #fff1f0;
}

.sku-item:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

/* 数量步进 */
.stepper-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3);
  border-top: 1px solid var(--n3);
}

.stepper-label {
  font-size: var(--fs-base);
  color: var(--n10);
}

.stepper-label .limit {
  font-size: var(--fs-sm);
  color: var(--c-warning);
  margin-left: var(--s1);
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
  background: var(--c-error);
}

.addr-icon {
  width: 20px;
  height: 20px;
  color: var(--c-error);
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
  color: var(--c-error);
  border: 1px solid var(--c-error);
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
  color: var(--c-error);
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

/* 规则提示 */
.rule-tip {
  margin: 0 var(--s3) var(--s3);
  background: #fffbe6;
  border: 1px solid #ffe58f;
  border-radius: var(--r-base);
  padding: var(--s2) var(--s3);
  font-size: var(--fs-sm);
  color: #ad6800;
  display: flex;
  align-items: flex-start;
  gap: 6px;
  line-height: 1.6;
}

.rule-tip svg {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
  margin-top: 3px;
  color: var(--c-warning);
}

/* 底部抢购栏 */
.seckill-bar {
  height: 54px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s3);
  padding-bottom: env(safe-area-inset-bottom);
}

.bar-total {
  display: flex;
  flex-direction: column;
  justify-content: center;
  flex: 1;
  gap: 1px;
}

.bar-total .label {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.bar-total .save {
  font-size: 10px;
  color: var(--c-success);
}

.seckill-btn {
  height: 42px;
  padding: 0 36px;
  border-radius: 21px;
  background: linear-gradient(135deg, #ff4d4f 0%, #cf1322 100%);
  color: #fff;
  border: none;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  box-shadow: 0 2px 8px rgba(255, 77, 79, 0.35);
  transition: opacity var(--d-mid) var(--ease-std);
  font-family: inherit;
  flex-shrink: 0;
}

.seckill-btn:active {
  opacity: 0.9;
}

.seckill-btn:disabled,
.seckill-btn.disabled {
  background: var(--n5);
  box-shadow: none;
  color: var(--n1);
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
  color: var(--c-error);
}

.addr-main {
  flex: 1;
  min-width: 0;
}

.check {
  width: 18px;
  height: 18px;
  color: var(--c-error);
  flex-shrink: 0;
}
</style>
