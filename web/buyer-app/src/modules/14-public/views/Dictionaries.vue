<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { publicApi } from '@/modules/14-public/api/public.api'
import type { DictionaryDto } from '../types/public.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 数据字典页（/dictionaries/:code，游客可访问，开发者工具页）
 *
 * 结构（对齐设计稿 dictionaries）：
 * NavBar（返回 + 数据字典）→ 字典编码搜索框（跳转 /dictionaries/{code}）
 * → 字典卡片（名称 + 编码 + 条目数 + 启用状态）→ 字典项列表
 * （序号 / label / value 等宽展示 / 复制按钮）
 * → 底部说明（本页面仅供前端组件引用字典项使用）
 *
 * 路由参数 code 变化时自动重新查询；点击 value 行的复制按钮写入剪贴板。
 */
const route = useRoute()
const router = useRouter()

// ---- 状态 ----
const codeInput = ref('')
const loading = ref(false)
const loadError = ref(false)
const dictionary = ref<DictionaryDto | null>(null)

/** 当前路由的字典编码 */
function currentCode(): string {
  return typeof route.params.code === 'string' ? route.params.code : ''
}

onMounted(() => {
  codeInput.value = currentCode()
  void loadDictionary(currentCode())
})

// 路由参数变化（搜索跳转）时重新查询
watch(
  () => route.params.code,
  (code) => {
    if (route.name !== 'public.dictionaries' || typeof code !== 'string') return
    codeInput.value = code
    void loadDictionary(code)
  },
)

/** 查询字典 */
async function loadDictionary(code: string): Promise<void> {
  if (!code) {
    dictionary.value = null
    return
  }
  loading.value = true
  loadError.value = false
  try {
    dictionary.value = await publicApi.getDictionary(code)
  } catch (e) {
    logger.error('字典查询失败', e)
    dictionary.value = null
    loadError.value = true
    showFailToast(e instanceof Error ? e.message : '查询失败，请稍后重试')
  } finally {
    loading.value = false
  }
}

/** 搜索字典编码（跳转路由触发重新查询） */
function onSearch(): void {
  const code = codeInput.value.trim()
  if (!code) {
    showToast('请输入字典编码')
    return
  }
  if (code === currentCode()) {
    // 同编码直接重查
    void loadDictionary(code)
    return
  }
  router.push(`/dictionaries/${code}`)
}

/** 复制 value 到剪贴板 */
async function copyValue(value: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(value)
    showToast('已复制')
  } catch {
    showFailToast('复制失败，请手动复制')
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
  <div class="dictionaries-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">数据字典</div>
    </header>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 编码搜索框 -->
      <div class="search-bar" role="search">
        <div class="search-input">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
            <circle cx="11" cy="11" r="7" />
            <path d="M21 21l-4.3-4.3" />
          </svg>
          <input
            v-model="codeInput"
            type="text"
            placeholder="输入字典编码，如 refund-reason"
            aria-label="字典编码"
            @keydown.enter="onSearch"
          >
        </div>
        <button class="search-btn" type="button" @click="onSearch">搜索</button>
      </div>

      <!-- 查询骨架 -->
      <div v-if="loading" class="dict-card">
        <div class="sk-row">
          <div class="skeleton-block sk-circle" />
          <div class="sk-lines">
            <div class="skeleton-block sk-line mid" />
            <div class="skeleton-block sk-line short" />
          </div>
        </div>
        <div v-for="i in 4" :key="i" class="sk-row">
          <div class="skeleton-block sk-index" />
          <div class="sk-lines">
            <div class="skeleton-block sk-line mid" />
            <div class="skeleton-block sk-line short" />
          </div>
        </div>
      </div>

      <!-- 错误态（字典不存在 / 网络异常） -->
      <ErrorState
        v-else-if="loadError"
        title="字典查询失败"
        description="字典不存在或网络异常，请检查编码后重试"
        @retry="loadDictionary(currentCode())"
      />

      <!-- 未查询空态 -->
      <EmptyState v-else-if="!dictionary" title="请输入字典编码查询" />

      <!-- 字典无项目 -->
      <EmptyState v-else-if="dictionary.items.length === 0" title="字典无项目" />

      <!-- 字典内容 -->
      <template v-else>
        <!-- 字典卡片 -->
        <div class="dict-card">
          <div class="dict-header">
            <div class="dict-header-left">
              <div class="dict-name">
                <van-icon name="orders-o" size="16" color="#1677FF" />
                {{ dictionary.name }}
              </div>
              <div class="dict-code">
                <span class="code-label">编码：</span>
                <span class="code-value">{{ dictionary.code }}</span>
              </div>
              <div class="dict-meta">
                <span class="dict-meta-item">
                  <van-icon name="bars" size="12" />
                  共 {{ dictionary.items.length }} 项
                </span>
              </div>
            </div>
            <span class="dict-status">启用中</span>
          </div>

          <!-- 字典项表格 -->
          <div class="dict-items" role="list" aria-label="字典项列表">
            <div
              v-for="(item, index) in dictionary.items"
              :key="`${item.value}-${index}`"
              class="dict-item"
              role="listitem"
              :aria-label="`${item.label} ${item.value}`"
            >
              <span class="dict-item-index">{{ index + 1 }}</span>
              <div class="dict-item-body">
                <div class="dict-item-label">{{ item.label }}</div>
                <div class="dict-item-value">{{ item.value }}</div>
              </div>
              <button class="copy-btn" type="button" aria-label="复制 value" @click="copyValue(item.value)">
                <van-icon name="description" size="16" color="#1677FF" />
              </button>
            </div>
          </div>
        </div>

        <!-- 工具页说明 -->
        <div class="footer-note">
          本页面为开发者工具页，仅供前端组件（如选择器选项）引用字典项使用，普通用户无需直接访问。
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.dictionaries-page {
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
  margin-right: 20px;
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s12) + env(safe-area-inset-bottom));
}

/* 搜索框 */
.search-bar {
  display: flex;
  gap: var(--s2);
  padding: var(--s1) 0 var(--s3);
}

.search-input {
  flex: 1;
  height: 36px;
  background: var(--n1);
  border-radius: var(--r-lg);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s1);
  color: var(--n7);
}

.search-input input {
  border: none;
  background: transparent;
  outline: none;
  flex: 1;
  font-size: var(--fs-base);
  font-family: inherit;
  color: var(--n10);
  min-width: 0;
}

.search-btn {
  flex-shrink: 0;
  padding: 0 var(--s4);
  height: 36px;
  background: var(--c-primary);
  color: #fff;
  border-radius: var(--r-lg);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
}

/* 字典卡片 */
.dict-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.dict-header {
  display: flex;
  align-items: flex-start;
  padding: var(--s3) var(--s4);
  border-bottom: 1px solid var(--n3);
}

.dict-header-left {
  flex: 1;
  min-width: 0;
}

.dict-name {
  display: flex;
  align-items: center;
  gap: var(--s1);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.dict-code {
  margin-top: var(--s1);
  font-size: var(--fs-sm);
  color: var(--n7);
}

.code-value {
  font-family: var(--ff-mono);
  color: var(--c-primary);
}

.dict-meta {
  margin-top: var(--s1);
  display: flex;
  gap: var(--s3);
}

.dict-meta-item {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: var(--fs-sm);
  color: var(--n7);
}

.dict-status {
  flex-shrink: 0;
  font-size: var(--fs-sm);
  color: var(--c-success);
  background: #f6ffed;
  padding: 2px var(--s2);
  border-radius: var(--r-base);
}

/* 字典项列表 */
.dict-items {
  padding: var(--s2) 0;
}

.dict-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s2) var(--s4);
  border-bottom: 1px solid var(--n2);
}

.dict-item:last-child {
  border-bottom: none;
}

.dict-item-index {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--n3);
  color: var(--n7);
  font-size: var(--fs-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.dict-item-body {
  flex: 1;
  min-width: 0;
}

.dict-item-label {
  font-size: var(--fs-base);
  color: var(--n10);
}

.dict-item-value {
  margin-top: 2px;
  font-size: var(--fs-base);
  font-family: var(--ff-mono);
  color: var(--c-primary);
  word-break: break-all;
}

.copy-btn {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  border-radius: var(--r-card);
  background: #e6f4ff;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 工具页说明 */
.footer-note {
  margin-top: var(--s3);
  padding: var(--s3);
  background: #fff7e6;
  border: 1px solid #ffe7ba;
  border-radius: var(--r-lg);
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
}

/* 骨架屏 */
.sk-row {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s2) var(--s4);
}

.sk-circle {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sk-index {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s1);
}

.sk-line {
  height: 12px;
}

.sk-line.mid {
  width: 60%;
}

.sk-line.short {
  width: 35%;
}
</style>
