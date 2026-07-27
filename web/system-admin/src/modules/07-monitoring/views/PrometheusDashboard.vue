<!-- web/system-admin/src/modules/07-monitoring/views/PrometheusDashboard.vue -->
<!-- Prometheus 监控看板：iframe 嵌入 Grafana/Prometheus URL，URL 来自 SystemConfigs 配置项 -->
<!-- 跨域降级：始终提供「在新窗口打开」按钮；iframe 加载失败时显示错误兜底 -->
<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  ReloadOutlined,
  LinkOutlined,
  MonitorOutlined,
} from '@ant-design/icons-vue'
import { monitoringApi } from '../api/monitoring.api'
import {
  PROMETHEUS_URL_CACHE_KEY,
  PROMETHEUS_URL_CACHE_TTL_MS,
  IFRAME_LOAD_TIMEOUT_MS,
} from '../types/monitoring.dto'
import { BusinessError } from '@/shared/http/errors'

/** sessionStorage 缓存结构 */
interface CachedUrl {
  url: string
  cachedAt: number
}

/** URL 加载阶段状态 */
const urlLoading = ref(false)
/** iframe 渲染阶段加载状态（拿到 URL 后等待 iframe load 事件） */
const iframeLoading = ref(false)
/** 当前看板 URL（明文，来自后端配置） */
const dashboardUrl = ref('')
/** URL 加载失败错误信息（null 表示无错误） */
const urlLoadError = ref<string | null>(null)
/** iframe 加载失败标志（跨域或不可达） */
const iframeError = ref(false)
/** iframe 加载超时定时器句柄 */
let iframeLoadTimer: number | null = null

const hasUrl = computed(() => !!dashboardUrl.value)

/**
 * 从 sessionStorage 读取缓存的 URL（5 分钟内有效）
 * @returns 缓存的 URL；缓存不存在、过期或解析失败时返回 null
 */
function readCachedUrl(): string | null {
  try {
    const raw = sessionStorage.getItem(PROMETHEUS_URL_CACHE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as CachedUrl
    if (!parsed.url || typeof parsed.cachedAt !== 'number') {
      sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
      return null
    }
    if (Date.now() - parsed.cachedAt > PROMETHEUS_URL_CACHE_TTL_MS) {
      sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
      return null
    }
    return parsed.url
  } catch {
    // JSON 解析失败或 sessionStorage 不可用，清理后回退到远程获取
    try {
      sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
    } catch {
      // sessionStorage 完全不可用时忽略，不影响主流程
    }
    return null
  }
}

/**
 * 将 URL 写入 sessionStorage 缓存
 * @param url 明文看板 URL
 */
function writeCachedUrl(url: string): void {
  try {
    const payload: CachedUrl = { url, cachedAt: Date.now() }
    sessionStorage.setItem(PROMETHEUS_URL_CACHE_KEY, JSON.stringify(payload))
  } catch {
    // sessionStorage 写入失败（隐私模式或空间不足）不阻塞功能，下次仍走远程获取
  }
}

/**
 * 清除 sessionStorage 缓存
 */
function clearCachedUrl(): void {
  try {
    sessionStorage.removeItem(PROMETHEUS_URL_CACHE_KEY)
  } catch {
    // 忽略 sessionStorage 不可用错误
  }
}

/**
 * 启动 iframe 加载超时定时器
 * 跨域场景下 iframe 的 @error 事件可能不触发，超时检测作为兜底
 */
function startIframeLoadTimer(): void {
  clearIframeLoadTimer()
  iframeLoadTimer = window.setTimeout(() => {
    if (iframeLoading.value) {
      iframeLoading.value = false
      iframeError.value = true
    }
  }, IFRAME_LOAD_TIMEOUT_MS)
}

/**
 * 清除 iframe 加载超时定时器
 */
function clearIframeLoadTimer(): void {
  if (iframeLoadTimer !== null) {
    clearTimeout(iframeLoadTimer)
    iframeLoadTimer = null
  }
}

/**
 * 获取看板 URL（优先读缓存，缓存未命中或强制刷新时调用后端）
 * @param forceRefresh 是否强制刷新（清除缓存并重新请求）
 * @returns 明文 URL；获取失败时返回 null 并设置 urlLoadError
 */
async function fetchDashboardUrl(forceRefresh: boolean): Promise<string | null> {
  if (!forceRefresh) {
    const cached = readCachedUrl()
    if (cached) return cached
  } else {
    clearCachedUrl()
  }

  urlLoading.value = true
  urlLoadError.value = null
  try {
    const res = await monitoringApi.getPrometheusUrl()
    const url = res.data?.value?.trim()
    if (!url) {
      urlLoadError.value =
        '未配置 Prometheus 看板 URL，请在「系统治理 → 系统配置」中设置键 monitoring.prometheus.dashboard-url'
      return null
    }
    writeCachedUrl(url)
    return url
  } catch (e) {
    if (e instanceof BusinessError) {
      urlLoadError.value = `加载 Prometheus 看板地址失败：${e.message}`
    } else {
      urlLoadError.value = '加载 Prometheus 看板地址失败，请检查网络连接或后端服务状态'
    }
    return null
  } finally {
    urlLoading.value = false
  }
}

/**
 * 初始化看板：获取 URL → 渲染 iframe → 启动加载超时检测
 */
async function initDashboard(): Promise<void> {
  const url = await fetchDashboardUrl(false)
  if (url) {
    dashboardUrl.value = url
    iframeLoading.value = true
    iframeError.value = false
    startIframeLoadTimer()
  }
}

/**
 * 刷新看板：清缓存 + 重新加载
 */
async function onRefresh(): Promise<void> {
  clearIframeLoadTimer()
  dashboardUrl.value = ''
  iframeError.value = false
  urlLoadError.value = null
  await initDashboard()
  if (dashboardUrl.value) {
    message.success('已重新加载 Prometheus 看板')
  }
}

/**
 * iframe load 事件回调：加载成功，清除超时定时器
 */
function onIframeLoad(): void {
  clearIframeLoadTimer()
  iframeLoading.value = false
  iframeError.value = false
}

/**
 * iframe error 事件回调：加载失败（跨域或不可达）
 * 跨域场景下此事件可能不触发，由超时定时器兜底
 */
function onIframeError(): void {
  clearIframeLoadTimer()
  iframeLoading.value = false
  iframeError.value = true
}

/**
 * 在新窗口打开看板 URL（跨域降级方案，spec §9）
 */
function openInNewWindow(): void {
  if (dashboardUrl.value) {
    window.open(dashboardUrl.value, '_blank', 'noopener,noreferrer')
  }
}

onMounted(() => {
  initDashboard()
})

onBeforeUnmount(() => {
  clearIframeLoadTimer()
})
</script>

<template>
  <div class="monitoring-dashboard">
    <!-- 顶部工具栏：标题 + 刷新 + 在新窗口打开（始终可用，跨域降级方案） -->
    <div class="monitoring-toolbar">
      <div class="monitoring-title">
        <MonitorOutlined class="monitoring-title-icon" />
        <span>Prometheus 监控看板</span>
      </div>
      <a-space>
        <a-button :loading="urlLoading" @click="onRefresh">
          <template #icon><ReloadOutlined /></template>
          刷新
        </a-button>
        <a-button
          type="primary"
          :disabled="!hasUrl"
          @click="openInNewWindow"
        >
          <template #icon><LinkOutlined /></template>
          在新窗口打开
        </a-button>
      </a-space>
    </div>

    <!-- 阶段 1：URL 加载中（首次或刷新时获取看板地址） -->
    <div v-if="urlLoading" class="monitoring-loading">
      <a-spin tip="正在加载 Prometheus 看板地址...">
        <div class="monitoring-loading-placeholder" />
      </a-spin>
    </div>

    <!-- 阶段 2：URL 加载失败（API 错误或未配置） -->
    <div v-else-if="urlLoadError" class="monitoring-error">
      <a-result status="warning" title="无法加载 Prometheus 看板" :sub-title="urlLoadError">
        <template #extra>
          <a-button type="primary" :loading="urlLoading" @click="onRefresh">重试</a-button>
        </template>
      </a-result>
    </div>

    <!-- 阶段 3：URL 加载成功，渲染 iframe -->
    <div v-else-if="hasUrl" class="monitoring-frame-wrapper">
      <!-- iframe 加载中遮罩（拿到 URL 后等待 iframe load 事件） -->
      <div v-if="iframeLoading" class="monitoring-frame-loading">
        <a-spin tip="看板加载中...">
          <div class="monitoring-loading-placeholder" />
        </a-spin>
      </div>

      <!-- iframe 嵌入（跨域可能无法访问内容，但 src 仍会加载页面） -->
      <iframe
        v-if="!iframeError"
        :src="dashboardUrl"
        class="monitoring-frame"
        frameborder="0"
        allowfullscreen
        sandbox="allow-same-origin allow-scripts allow-forms allow-popups allow-presentation"
        @load="onIframeLoad"
        @error="onIframeError"
      />

      <!-- iframe 加载失败兜底（跨域被阻止或 URL 不可达，spec §9 缓解方案） -->
      <div v-if="iframeError" class="monitoring-frame-error">
        <a-result
          status="error"
          title="看板嵌入失败"
          sub-title="可能是跨域策略阻止了 iframe 嵌入，或看板地址不可达。可尝试在新窗口中打开。"
        >
          <template #extra>
            <a-button type="primary" @click="openInNewWindow">
              <template #icon><LinkOutlined /></template>
              在新窗口打开
            </a-button>
            <a-button @click="onRefresh">重试</a-button>
          </template>
        </a-result>
      </div>
    </div>

    <!-- 兜底：无 URL 且无错误（理论上不应到达，防御性渲染） -->
    <div v-else class="monitoring-empty">
      <a-empty description="暂无 Prometheus 看板配置">
        <a-button type="primary" @click="onRefresh">重新加载</a-button>
      </a-empty>
    </div>
  </div>
</template>

<style scoped>
.monitoring-dashboard {
  display: flex;
  flex-direction: column;
  /* 减去 header(64px) + footer(32px) + content padding(24px*2) */
  height: calc(100vh - 64px - 32px - 48px);
  background: var(--n2, #FAFAFA);
}

.monitoring-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: var(--n1, #FFFFFF);
  border-bottom: 1px solid var(--n5, #D9D9D9);
  flex-shrink: 0;
}

.monitoring-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 600;
  color: var(--n10, #000000D9);
}

.monitoring-title-icon {
  font-size: 20px;
  color: var(--c-primary, #1677FF);
}

.monitoring-loading,
.monitoring-error,
.monitoring-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  padding: 24px;
}

.monitoring-loading-placeholder {
  width: 480px;
  height: 320px;
}

.monitoring-frame-wrapper {
  position: relative;
  flex: 1;
  margin: 0 16px 16px;
  background: var(--n1, #FFFFFF);
  border-radius: var(--r-card, 8px);
  overflow: hidden;
  box-shadow: var(--sh-card, 0 1px 2px 0 rgba(0, 0, 0, 0.03));
}

.monitoring-frame-loading,
.monitoring-frame-error {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--n2, #FAFAFA);
  z-index: 1;
}

.monitoring-frame {
  width: 100%;
  height: 100%;
  border: none;
  display: block;
}
</style>
