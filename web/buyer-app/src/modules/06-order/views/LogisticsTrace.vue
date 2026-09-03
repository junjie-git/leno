<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showToast } from 'vant'
import { orderApi } from '@/modules/06-order/api/order.api'
import type { LogisticsTraceDto, OrderDto } from '../types/order.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 物流跟踪页（/order/:id/logistics）
 *
 * 结构（对齐设计稿 logistics-trace）：
 * NavBar（返回 / 物流轨迹）→ 状态横幅（主色渐变 + 最新节点状态与描述）
 * → 物流公司卡（公司名 + 客服电话说明 + 运单号一键复制）
 * → 商品摘要卡（订单商品行，可点跳商品详情）
 * → 自绘时间轴（倒序展示轨迹节点，最新节点主色高亮大圆点，历史节点灰色）
 * → 底部固定操作栏（刷新轨迹，重新拉取）
 *
 * 数据流：orderApi.getLogistics(id) 拉取轨迹；
 * orderApi.getDetail(id) 并行拉取订单摘要（商品行展示，失败静默降级隐藏）；
 * 下拉刷新与底部按钮均重新拉取（保留内容不闪骨架）。
 */

const route = useRoute()
const router = useRouter()

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const refreshing = ref(false)
const trace = ref<LogisticsTraceDto | null>(null)
const order = ref<OrderDto | null>(null)

// ---- 数据加载 ----
async function loadTrace(showSkeleton = true): Promise<void> {
  const id = String(route.params.id ?? '')
  if (showSkeleton) {
    loading.value = true
  }
  loadError.value = false
  try {
    const [traceResult, orderResult] = await Promise.all([
      orderApi.getLogistics(id),
      orderApi.getDetail(id).catch((e: unknown) => {
        logger.warn('订单摘要加载失败（忽略，隐藏商品摘要）', e)
        return null
      }),
    ])
    trace.value = traceResult
    order.value = orderResult
  } catch (e) {
    logger.error('物流轨迹加载失败', e)
    trace.value = null
    order.value = null
    loadError.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

onMounted(() => {
  void loadTrace()
})

// 同组件复用（订单互跳）时重载
watch(
  () => route.params.id,
  (id, prev) => {
    if (id && id !== prev) {
      void loadTrace()
    }
  },
)

// ---- 交互 ----
/** 下拉刷新（保留内容，不闪骨架） */
function onPullRefresh(): void {
  void loadTrace(false)
}

/** 底部按钮刷新 */
function onRefreshClick(): void {
  if (refreshing.value || loading.value) return
  refreshing.value = true
  void loadTrace(false)
}

/** 复制运单号 */
async function copyLogisticsNo(): Promise<void> {
  const no = trace.value?.logisticsNo
  if (!no) return
  try {
    await navigator.clipboard.writeText(no)
    showToast('运单号已复制')
  } catch {
    showToast('复制失败，请手动复制')
  }
}

/** 返回订单详情 */
function goOrderDetail(): void {
  router.push(`/order/${String(route.params.id ?? '')}`)
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    goOrderDetail()
  }
}

function goProduct(spuId: string): void {
  router.push(`/product/${spuId}`)
}
</script>

<template>
  <div class="trace-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">物流轨迹</div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-banner" />
      <div class="skeleton-block sk-company" />
      <div class="skeleton-block sk-timeline" />
    </main>

    <!-- 错误态 -->
    <main v-else-if="loadError" class="body">
      <ErrorState title="物流查询失败" description="网络异常，请稍后重试" @retry="loadTrace" />
    </main>

    <!-- 空态（暂无轨迹） -->
    <main v-else-if="!trace || trace.traces.length === 0" class="body">
      <EmptyState title="暂无物流信息" action-text="返回订单详情" @action="goOrderDetail" />
    </main>

    <!-- 内容 -->
    <main v-else class="body">
      <van-pull-refresh v-model="refreshing" success-text="刷新成功" @refresh="onPullRefresh">
        <!-- 状态横幅（最新节点） -->
        <section class="status-banner">
          <svg class="status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect x="1" y="3" width="15" height="13" />
            <polygon points="16 8 20 8 23 11 23 16 16 16 16 8" />
            <circle cx="5.5" cy="18.5" r="2.5" />
            <circle cx="18.5" cy="18.5" r="2.5" />
          </svg>
          <div class="status-info">
            <div class="status-text">{{ trace.traces[0]?.status ?? '运输中' }}</div>
            <div class="status-desc">{{ trace.traces[0]?.description ?? '物流信息更新中' }}</div>
          </div>
        </section>

        <!-- 物流公司卡 -->
        <section class="company-card">
          <div class="company-row">
            <div class="company-logo">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <rect x="1" y="3" width="15" height="13" />
                <polygon points="16 8 20 8 23 11 23 16 16 16 16 8" />
                <circle cx="5.5" cy="18.5" r="2.5" />
                <circle cx="18.5" cy="18.5" r="2.5" />
              </svg>
            </div>
            <div class="company-info">
              <div class="company-name">{{ trace.logisticsCompany }}</div>
              <div class="company-sub">承运快递公司</div>
            </div>
          </div>
          <div class="tracking-row">
            <span class="tracking-label">运单号</span>
            <span class="tracking-no">{{ trace.logisticsNo || '—' }}</span>
            <button v-if="trace.logisticsNo" class="copy-btn" type="button" @click="copyLogisticsNo">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </svg>
              复制
            </button>
          </div>
        </section>

        <!-- 商品摘要 -->
        <section v-if="order" class="section">
          <div
            v-for="item in order.items"
            :key="item.orderLineId"
            class="goods-mini"
            role="button"
            aria-label="查看商品详情"
            @click="goProduct(item.spuId)"
          >
            <img :src="item.image" :alt="item.name" loading="lazy">
            <div class="info">
              <div class="title">{{ item.name }}</div>
              <div class="spec">{{ item.specs }}；x{{ item.quantity }}</div>
            </div>
            <span class="link">查看商品</span>
          </div>
        </section>

        <!-- 物流时间轴（倒序，最新在上） -->
        <section class="timeline">
          <div class="timeline-title">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="10" />
              <path d="M12 6v6l4 2" />
            </svg>
            物流轨迹
          </div>
          <div class="timeline-list" role="list" aria-label="物流轨迹节点">
            <div
              v-for="(node, index) in trace.traces"
              :key="index"
              class="timeline-item"
              :class="{ active: index === 0 }"
              role="listitem"
            >
              <div class="node-desc">{{ node.description }}</div>
              <div class="node-meta">
                <span class="node-status">{{ node.status }}</span>
                <span class="node-time">{{ formatDateTime(node.time) }}</span>
              </div>
            </div>
          </div>
        </section>
      </van-pull-refresh>
    </main>

    <!-- 底部操作栏 -->
    <footer class="bottom-bar">
      <button
        class="bar-btn bar-btn-primary"
        type="button"
        :disabled="loading || refreshing"
        @click="onRefreshClick"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M23 4v6h-6" />
          <path d="M1 20v-6h6" />
          <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" />
        </svg>
        {{ refreshing ? '刷新中...' : '刷新轨迹' }}
      </button>
    </footer>
  </div>
</template>

<style scoped>
.trace-page {
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
.sk-banner {
  height: 100px;
  margin: var(--s3);
  border-radius: var(--r-lg);
}

.sk-company {
  height: 110px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

.sk-timeline {
  height: 300px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

/* 状态横幅 */
.status-banner {
  background: linear-gradient(135deg, var(--c-primary) 0%, #4096ff 100%);
  padding: var(--s6) var(--s4);
  color: #fff;
  display: flex;
  align-items: center;
  gap: var(--s3);
}

.status-banner .status-icon {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  opacity: 0.95;
}

.status-banner .status-info {
  flex: 1;
  min-width: 0;
}

.status-banner .status-text {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  margin-bottom: 2px;
}

.status-banner .status-desc {
  font-size: var(--fs-sm);
  opacity: 0.95;
  line-height: 1.6;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

/* 物流公司卡 */
.company-card {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.company-row {
  display: flex;
  align-items: center;
  padding: var(--s3);
  gap: var(--s3);
}

.company-logo {
  width: 40px;
  height: 40px;
  border-radius: var(--r-base);
  background: linear-gradient(135deg, #1677ff, #4096ff);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  flex-shrink: 0;
}

.company-logo svg {
  width: 24px;
  height: 24px;
}

.company-info {
  flex: 1;
  min-width: 0;
}

.company-name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
  margin-bottom: 2px;
}

.company-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.tracking-row {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s2) var(--s3) var(--s3);
  border-top: 1px solid var(--n3);
}

.tracking-label {
  font-size: var(--fs-sm);
  color: var(--n7);
  flex-shrink: 0;
}

.tracking-no {
  font-size: var(--fs-base);
  color: var(--n9);
  font-family: var(--ff-mono);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.copy-btn {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  color: var(--c-primary);
  font-size: var(--fs-sm);
  cursor: pointer;
  border: 1px solid var(--c-primary);
  border-radius: var(--r-base);
  padding: 2px var(--s2);
  background: var(--n1);
  font-family: inherit;
  flex-shrink: 0;
}

.copy-btn svg {
  width: 12px;
  height: 12px;
}

/* 商品摘要 */
.section {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.goods-mini {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3);
  cursor: pointer;
}

.goods-mini img {
  width: 48px;
  height: 48px;
  border-radius: var(--r-base);
  object-fit: cover;
  flex-shrink: 0;
  background: var(--n3);
}

.goods-mini .info {
  flex: 1;
  min-width: 0;
}

.goods-mini .title {
  font-size: var(--fs-sm);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.goods-mini .spec {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.goods-mini .link {
  font-size: var(--fs-sm);
  color: var(--c-primary);
  flex-shrink: 0;
}

/* 时间轴 */
.timeline {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4) var(--s3);
}

.timeline-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  margin-bottom: var(--s3);
  display: flex;
  align-items: center;
  gap: 6px;
}

.timeline-title svg {
  width: 16px;
  height: 16px;
  color: var(--c-primary);
}

.timeline-list {
  position: relative;
  padding-left: var(--s2);
}

.timeline-item {
  position: relative;
  padding: 0 0 var(--s4) 24px;
}

.timeline-item:last-child {
  padding-bottom: 0;
}

.timeline-item::before {
  content: "";
  position: absolute;
  left: 5px;
  top: 4px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--n5);
  border: 2px solid var(--n1);
  z-index: 1;
}

.timeline-item::after {
  content: "";
  position: absolute;
  left: 9px;
  top: 14px;
  bottom: -4px;
  width: 2px;
  background: var(--n3);
}

.timeline-item:last-child::after {
  display: none;
}

.timeline-item.active::before {
  background: var(--c-primary);
  width: 14px;
  height: 14px;
  left: 3px;
  top: 2px;
  box-shadow: 0 0 0 4px rgba(22, 119, 255, 0.15);
}

.timeline-item .node-desc {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
  line-height: 1.4;
  margin-bottom: var(--s1);
}

.timeline-item.active .node-desc {
  color: var(--c-primary);
}

.timeline-item .node-meta {
  display: flex;
  align-items: center;
  gap: var(--s2);
  flex-wrap: wrap;
}

.timeline-item .node-status {
  font-size: var(--fs-sm);
  color: var(--n7);
  background: var(--n3);
  border-radius: var(--r-base);
  padding: 1px 6px;
}

.timeline-item.active .node-status {
  color: var(--c-primary);
  background: #e6f4ff;
}

.timeline-item .node-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  font-family: var(--ff-mono);
}

/* 底部操作栏 */
.bottom-bar {
  height: 60px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  padding-bottom: env(safe-area-inset-bottom);
}

.bar-btn {
  flex: 1;
  height: 40px;
  border-radius: 20px;
  font-size: var(--fs-base);
  border: 1px solid var(--n5);
  background: var(--n1);
  color: var(--n9);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  font-family: inherit;
}

.bar-btn:disabled {
  opacity: 0.6;
  pointer-events: none;
}

.bar-btn-primary {
  background: var(--c-primary);
  border-color: var(--c-primary);
  color: #fff;
}
</style>
