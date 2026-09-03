<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { orderApi } from '@/modules/06-order/api/order.api'
import type { OrderDto, OrderItemDto, OrderListTab, OrderStatus } from '../types/order.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 我的订单页（/orders）
 *
 * 结构（对齐设计稿 order-list）：
 * NavBar（返回 / 我的订单）→ 状态 Tab（全部/待付款/待发货/待收货/已完成/售后，切换重载）
 * → van-pull-refresh + van-list 无限滚动的订单卡片列表
 * → 订单卡（店铺头 + 状态标签 + 商品行 + 合计行 + 按状态渲染的操作按钮）
 *
 * 操作流：
 * - 待付款：取消订单（showConfirmDialog → orderApi.cancel）/ 去支付（/payment/initiate/:id）
 * - 待收货：查看物流（/order/:id/logistics）/ 确认收货（showConfirmDialog → orderApi.confirm）
 * - 已完成：评价（/review/submit/:orderLineId）/ 再次购买（/product/:spuId）
 * - 点击卡片 → /order/:id 订单详情
 */

const router = useRouter()

const pageSize = 10

/** 状态 Tab（全部 + OrderQueryParams.status 枚举） */
const TABS: Array<{ key: OrderListTab | ''; label: string }> = [
  { key: '', label: '全部' },
  { key: 'PendingPayment', label: '待付款' },
  { key: 'Paid', label: '待发货' },
  { key: 'Shipped', label: '待收货' },
  { key: 'Completed', label: '已完成' },
  { key: 'AfterSales', label: '售后' },
]

/** 订单状态 → 标签文案与配色（对齐设计稿状态色） */
const STATUS_META: Record<OrderStatus, { label: string; cls: string }> = {
  PendingPayment: { label: '待付款', cls: 'st-pending' },
  Paid: { label: '待发货', cls: 'st-shipment' },
  Shipped: { label: '待收货', cls: 'st-receipt' },
  Completed: { label: '已完成', cls: 'st-done' },
  Cancelled: { label: '已取消', cls: 'st-cancel' },
  Refunding: { label: '退款中', cls: 'st-pending' },
  Refunded: { label: '已退款', cls: 'st-cancel' },
  AfterSales: { label: '售后中', cls: 'st-receipt' },
}

// ---- 列表状态 ----
const activeTab = ref<OrderListTab | ''>('')
const firstLoading = ref(true)
const orders = ref<OrderDto[]>([])
const page = ref(1)
const finished = ref(false)
const listLoading = ref(false)
const listError = ref(false)
const refreshing = ref(false)

/** 列表请求序号（切换 Tab 时旧响应作废） */
let listSeq = 0

onMounted(() => {
  void reload()
})

/** 状态标签元信息 */
function statusMeta(order: OrderDto): { label: string; cls: string } {
  return STATUS_META[order.status]
}

/** 订单总件数 */
function totalQuantity(order: OrderDto): number {
  return order.items.reduce((acc, item) => acc + item.quantity, 0)
}

/** 首个未评价订单行（已完成订单「评价」入口） */
function firstUnreviewedLine(order: OrderDto): OrderItemDto | undefined {
  return order.items.find((item) => !item.reviewed)
}

/** 重置分页并加载第一页 */
async function reload(): Promise<void> {
  const seq = ++listSeq
  page.value = 1
  finished.value = false
  listError.value = false
  firstLoading.value = true
  try {
    const result = await orderApi.list({
      status: activeTab.value || undefined,
      page: 1,
      pageSize,
    })
    if (seq !== listSeq) return
    orders.value = result.items
    if (result.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    if (seq !== listSeq) return
    logger.error('订单列表加载失败', e)
    listError.value = true
  } finally {
    if (seq === listSeq) {
      firstLoading.value = false
      refreshing.value = false
    }
  }
}

/** van-list 无限加载 */
async function onLoad(): Promise<void> {
  if (finished.value || firstLoading.value) return
  const seq = listSeq
  listLoading.value = true
  listError.value = false
  try {
    const next = await orderApi.list({
      status: activeTab.value || undefined,
      page: page.value + 1,
      pageSize,
    })
    if (seq !== listSeq) return
    orders.value.push(...next.items)
    page.value += 1
    if (next.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    if (seq !== listSeq) return
    logger.warn('订单列表翻页加载失败', e)
    listError.value = true
  } finally {
    if (seq === listSeq) {
      listLoading.value = false
    }
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await reload()
}

/** 切换状态 Tab */
function setTab(key: OrderListTab | ''): void {
  if (activeTab.value === key) return
  activeTab.value = key
  void reload()
}

// ---- 跳转 ----
function goDetail(order: OrderDto): void {
  router.push(`/order/${order.id}`)
}

function goShop(order: OrderDto): void {
  router.push(`/shop/${order.shopId}`)
}

function goPay(order: OrderDto): void {
  router.push(`/payment/initiate/${order.id}`)
}

function goLogistics(order: OrderDto): void {
  router.push(`/order/${order.id}/logistics`)
}

function goReviewLine(line: OrderItemDto): void {
  router.push(`/review/submit/${line.orderLineId}`)
}

function goRepurchase(order: OrderDto): void {
  const spuId = order.items[0]?.spuId
  if (spuId) {
    router.push(`/product/${spuId}`)
  }
}

function goHome(): void {
  router.replace('/')
}

// ---- 订单操作 ----
/** 取消订单（待付款，二次确认） */
async function cancelOrder(order: OrderDto): Promise<void> {
  try {
    await showConfirmDialog({
      title: '确认取消',
      message: '取消后订单将关闭，如需购买请重新下单。此操作不可逆。',
      confirmButtonText: '确认取消',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  try {
    await orderApi.cancel(order.id)
    showToast('订单已取消')
    void reload()
  } catch (e) {
    logger.warn('取消订单失败', e)
    showFailToast(e instanceof Error ? e.message : '取消失败，请稍后重试')
  }
}

/** 确认收货（待收货，二次确认） */
async function confirmReceive(order: OrderDto): Promise<void> {
  try {
    await showConfirmDialog({
      title: '确认收货',
      message: '确认收货后交易完成，售后期开始计算。请确认您已收到商品。',
      confirmButtonText: '确认收货',
      confirmButtonColor: '#FF4D4F',
    })
  } catch {
    return
  }
  try {
    await orderApi.confirm(order.id)
    showToast('已确认收货')
    void reload()
  } catch (e) {
    logger.warn('确认收货失败', e)
    showFailToast(e instanceof Error ? e.message : '操作失败，请稍后重试')
  }
}

// ---- 返回 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}
</script>

<template>
  <div class="orders-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">我的订单</div>
    </header>

    <!-- 状态 Tab -->
    <nav class="tabs" role="tablist" aria-label="订单状态筛选">
      <div
        v-for="tab in TABS"
        :key="tab.key"
        class="tab"
        :class="{ active: activeTab === tab.key }"
        role="tab"
        :aria-selected="activeTab === tab.key"
        @click="setTab(tab.key)"
      >
        {{ tab.label }}
      </div>
    </nav>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="skeleton-list">
        <div v-for="i in 3" :key="i" class="sk-card">
          <div class="skeleton-block sk-head" />
          <div class="sk-row">
            <div class="skeleton-block sk-img" />
            <div class="sk-lines">
              <div class="skeleton-block sk-l1" />
              <div class="skeleton-block sk-l2" />
            </div>
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="listError && orders.length === 0"
        title="订单加载失败"
        description="网络异常，请稍后重试"
        @retry="reload"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="orders.length === 0"
        title="暂无相关订单"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 订单列表 -->
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
          <article
            v-for="order in orders"
            :key="order.id"
            class="order-card"
            role="article"
            :aria-label="`订单 ${statusMeta(order).label}`"
            @click="goDetail(order)"
          >
            <!-- 卡片头：店铺 + 状态 -->
            <div class="card-head">
              <div class="shop-name" @click.stop="goShop(order)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M3 9l1-5h16l1 5" />
                  <path d="M4 9v11a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
                  <path d="M9 13h6" />
                </svg>
                {{ order.shopName }}
                <svg class="arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M9 6l6 6-6 6" />
                </svg>
              </div>
              <span class="status-tag" :class="statusMeta(order).cls">{{ statusMeta(order).label }}</span>
            </div>

            <!-- 商品行 -->
            <div class="goods-list">
              <div v-for="item in order.items" :key="item.orderLineId" class="goods-row">
                <img class="goods-img" :src="item.image" :alt="item.name" loading="lazy">
                <div class="goods-info">
                  <div class="goods-title">{{ item.name }}</div>
                  <div class="goods-spec">{{ item.specs }}</div>
                  <div class="goods-price-qty">
                    <span class="goods-price">¥{{ (item.price / 100).toFixed(2) }}</span>
                    <span class="goods-qty">x{{ item.quantity }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- 卡片脚：合计 + 操作 -->
            <div class="card-foot">
              <div class="total-line">
                共{{ totalQuantity(order) }}件 实付
                <PriceText :amount="order.amounts.payableAmount" :size="16" />
              </div>
              <div class="action-bar">
                <template v-if="order.status === 'PendingPayment'">
                  <button class="btn btn-ghost" type="button" @click.stop="cancelOrder(order)">取消订单</button>
                  <button class="btn btn-primary" type="button" @click.stop="goPay(order)">去支付</button>
                </template>
                <template v-else-if="order.status === 'Shipped'">
                  <button class="btn btn-ghost" type="button" @click.stop="goLogistics(order)">查看物流</button>
                  <button class="btn btn-primary" type="button" @click.stop="confirmReceive(order)">确认收货</button>
                </template>
                <template v-else-if="order.status === 'Completed'">
                  <button
                    v-if="firstUnreviewedLine(order)"
                    class="btn btn-ghost"
                    type="button"
                    @click.stop="goReviewLine(firstUnreviewedLine(order)!)"
                  >
                    评价
                  </button>
                  <button class="btn btn-primary" type="button" @click.stop="goRepurchase(order)">再次购买</button>
                </template>
              </div>
            </div>
          </article>
        </van-list>
      </van-pull-refresh>
    </div>
  </div>
</template>

<style scoped>
.orders-page {
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

/* 状态 Tab */
.tabs {
  background: var(--n1);
  display: flex;
  height: 44px;
  border-bottom: 1px solid var(--n3);
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  flex-shrink: 0;
}

.tabs::-webkit-scrollbar {
  display: none;
}

.tab {
  flex: 1 0 auto;
  min-width: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
  white-space: nowrap;
  padding: 0 var(--s2);
  transition: color var(--d-mid) var(--ease-std);
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
  width: 20px;
  height: 2px;
  background: var(--c-primary);
  border-radius: 1px;
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
  background: var(--n3);
}

/* 骨架屏 */
.skeleton-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.sk-head {
  height: 18px;
  width: 50%;
  margin-bottom: var(--s2);
}

.sk-row {
  display: flex;
  gap: var(--s2);
}

.sk-img {
  width: 64px;
  height: 64px;
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
  justify-content: center;
}

.sk-l1 {
  width: 80%;
  height: 14px;
}

.sk-l2 {
  width: 50%;
  height: 12px;
}

/* 订单卡 */
.order-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
  overflow: hidden;
  cursor: pointer;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3) var(--s3) var(--s2);
  border-bottom: 1px solid var(--n3);
}

.shop-name {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  min-width: 0;
  overflow: hidden;
}

.shop-name svg {
  width: 14px;
  height: 14px;
  color: var(--n9);
  flex-shrink: 0;
}

.shop-name .arrow {
  width: 12px;
  height: 12px;
  color: var(--n7);
  flex-shrink: 0;
}

.status-tag {
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

.st-pending {
  color: var(--c-warning);
}

.st-shipment {
  color: var(--c-primary);
}

.st-receipt {
  color: var(--c-buyer);
}

.st-done {
  color: var(--c-success);
}

.st-cancel {
  color: var(--n7);
}

/* 商品行 */
.goods-list {
  padding: var(--s2) var(--s3);
}

.goods-row {
  display: flex;
  gap: var(--s2);
  padding: var(--s2) 0;
}

.goods-img {
  width: 64px;
  height: 64px;
  border-radius: var(--r-base);
  background: var(--n3);
  flex-shrink: 0;
  object-fit: cover;
}

.goods-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.goods-title {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.goods-spec {
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

.goods-price-qty {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
}

.goods-price {
  font-size: var(--fs-base);
  color: var(--n10);
}

.goods-qty {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 卡片脚 */
.card-foot {
  padding: var(--s2) var(--s3) var(--s3);
}

.total-line {
  display: flex;
  justify-content: flex-end;
  align-items: baseline;
  gap: 4px;
  padding: var(--s2) 0;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.action-bar {
  display: flex;
  justify-content: flex-end;
  gap: var(--s2);
  border-top: 1px solid var(--n3);
  padding-top: var(--s3);
}

.btn {
  height: 30px;
  padding: 0 14px;
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  border: 1px solid var(--n5);
  background: var(--n1);
  color: var(--n9);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-family: inherit;
  transition: opacity var(--d-fast) var(--ease-std);
}

.btn:active {
  opacity: 0.7;
}

.btn-primary {
  background: var(--c-primary);
  border-color: var(--c-primary);
  color: #fff;
}
</style>
