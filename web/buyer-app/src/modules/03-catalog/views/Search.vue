<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog } from 'vant'
import { productApi } from '@/modules/03-catalog/api/product.api'
import { categoryApi } from '@/modules/03-catalog/api/category.api'
import { publicApi } from '@/modules/14-public/api/public.api'
import type { CategoryDto } from '@/modules/03-catalog/types/product.dto'
import { logger } from '@/shared/utils/logger'

/**
 * 搜索页
 *
 * 结构（对齐设计稿 search）：
 * 顶部搜索栏（返回 + 输入框 + 搜索按钮）
 * → 搜索历史（localStorage，最多 10 条去重，最新在前，清空需二次确认）
 * → 热门搜索（字典 hot_search_keywords 配置，前 3 名红色高亮）
 * → 热门分类（分类树一级前 4）
 *
 * 输入 300ms 防抖触发联想（/products/search?pageSize=5 提取商品名），
 * 新请求发出后旧响应作废（序号守卫），失败静默。
 */

const router = useRouter()

const HISTORY_KEY = 'search_history'
const HISTORY_MAX = 10

// ---- 状态 ----
const keyword = ref('')
const history = ref<string[]>([])
const hotWords = ref<Array<{ label: string; value: string }>>([])
const hotCategories = ref<CategoryDto[]>([])
const suggestions = ref<string[]>([])
const suggesting = ref(false)

/** 联想请求序号（防抖 + 旧响应作废） */
let suggestSeq = 0
let debounceTimer: ReturnType<typeof setTimeout> | null = null

const hasInput = computed(() => keyword.value.trim().length > 0)

/** 热搜前 3 名标记：第 1 名「爆」、第 3 名「热」 */
function hotBadge(index: number): '' | '爆' | '热' {
  if (index === 0) return '爆'
  if (index === 2) return '热'
  return ''
}

// ---- 搜索历史（localStorage） ----
function loadHistory(): void {
  try {
    const raw = localStorage.getItem(HISTORY_KEY)
    if (!raw) return
    const parsed = JSON.parse(raw) as unknown
    if (Array.isArray(parsed)) {
      history.value = parsed.filter((x): x is string => typeof x === 'string').slice(0, HISTORY_MAX)
    }
  } catch (e) {
    logger.warn('搜索历史解析失败（忽略）', e)
    history.value = []
  }
}

function saveHistory(word: string): void {
  const w = word.trim()
  if (!w) return
  const next = [w, ...history.value.filter((x) => x !== w)].slice(0, HISTORY_MAX)
  history.value = next
  try {
    localStorage.setItem(HISTORY_KEY, JSON.stringify(next))
  } catch (e) {
    logger.warn('搜索历史写入失败（忽略）', e)
  }
}

function clearHistory(): void {
  showConfirmDialog({
    title: '清空搜索历史',
    message: '确定要清空全部搜索历史吗？',
    confirmButtonText: '清空',
    confirmButtonColor: '#FF4D4F',
  })
    .then(() => {
      history.value = []
      try {
        localStorage.removeItem(HISTORY_KEY)
      } catch (e) {
        logger.warn('搜索历史清除失败（忽略）', e)
      }
    })
    .catch(() => {
      // 用户取消，无需处理
    })
}

// ---- 联想（防抖 300ms + 序号守卫） ----
function onInput(): void {
  if (debounceTimer) {
    clearTimeout(debounceTimer)
    debounceTimer = null
  }
  const word = keyword.value.trim()
  if (!word) {
    suggestSeq += 1
    suggestions.value = []
    suggesting.value = false
    return
  }
  suggesting.value = true
  debounceTimer = setTimeout(() => {
    void fetchSuggestions(word)
  }, 300)
}

async function fetchSuggestions(word: string): Promise<void> {
  const seq = ++suggestSeq
  try {
    const result = await productApi.search({ keyword: word, page: 1, pageSize: 5 })
    if (seq !== suggestSeq) return
    suggestions.value = result.items.map((item) => item.name)
  } catch (e) {
    // 联想失败静默处理，不影响输入
    if (seq === suggestSeq) {
      logger.warn('搜索联想失败（忽略）', e)
      suggestions.value = []
    }
  } finally {
    if (seq === suggestSeq) {
      suggesting.value = false
    }
  }
}

/** 联想词高亮分段：命中的关键词片段加粗 */
function splitHighlight(text: string): Array<{ text: string; hit: boolean }> {
  const word = keyword.value.trim()
  if (!word) return [{ text, hit: false }]
  const idx = text.toLowerCase().indexOf(word.toLowerCase())
  if (idx < 0) return [{ text, hit: false }]
  return [
    { text: text.slice(0, idx), hit: false },
    { text: text.slice(idx, idx + word.length), hit: true },
    { text: text.slice(idx + word.length), hit: false },
  ].filter((part) => part.text.length > 0)
}

// ---- 搜索动作 ----
function doSearch(word?: string): void {
  const w = (word ?? keyword.value).trim()
  if (!w) {
    keyword.value = ''
    return
  }
  saveHistory(w)
  router.push({ path: '/search/results', query: { keyword: w } })
}

function onEnter(): void {
  doSearch()
}

function goCategory(category: CategoryDto): void {
  router.push(`/category?categoryId=${category.children[0]?.id ?? category.id}`)
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

onMounted(async () => {
  loadHistory()
  // 热搜词（字典配置，失败回退本地默认）
  try {
    const dict = await publicApi.getDictionary('hot_search_keywords')
    hotWords.value = dict.items
  } catch (e) {
    logger.warn('热搜词字典加载失败（使用本地默认）', e)
    hotWords.value = [
      { label: 'iPhone 15', value: '98.5万' },
      { label: '夏季短袖T恤', value: '76.2万' },
      { label: '蓝牙耳机', value: '65.8万' },
      { label: '运动鞋', value: '54.1万' },
      { label: '空调', value: '48.3万' },
      { label: '防晒霜', value: '42.7万' },
      { label: '每日坚果', value: '38.9万' },
      { label: '充电宝', value: '35.4万' },
    ]
  }
  // 热门分类（分类树一级前 4，失败静默）
  try {
    const tree = await categoryApi.getTree()
    hotCategories.value = tree.slice(0, 4)
  } catch (e) {
    logger.warn('热门分类加载失败（忽略）', e)
  }
})

onBeforeUnmount(() => {
  if (debounceTimer) {
    clearTimeout(debounceTimer)
    debounceTimer = null
  }
})
</script>

<template>
  <div class="search-page">
    <!-- 顶部搜索栏 -->
    <header class="search-top">
      <button class="back-btn" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="search-input">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <circle cx="11" cy="11" r="7" />
          <path d="M21 21l-4.3-4.3" />
        </svg>
        <input
          v-model="keyword"
          type="text"
          placeholder="搜索商品"
          aria-label="搜索商品"
          autofocus
          @input="onInput"
          @keydown.enter="onEnter"
        >
        <van-loading v-if="suggesting" size="14" color="#1677FF" />
      </div>
      <button class="search-btn" type="button" @click="doSearch()">搜索</button>
    </header>

    <!-- 内容区 -->
    <div class="content">
      <!-- 输入联想态 -->
      <template v-if="hasInput">
        <div v-if="suggesting" class="sugg-list">
          <div v-for="i in 5" :key="i" class="sugg-item">
            <div class="skeleton-block sk-ic" />
            <div class="skeleton-block sk-tx" :style="{ width: `${50 + ((i * 13) % 40)}%` }" />
          </div>
          <div class="sugg-loading">联想中…</div>
        </div>

        <div v-else-if="suggestions.length > 0" class="sugg-list">
          <div
            v-for="word in suggestions"
            :key="word"
            class="sugg-item"
            @click="doSearch(word)"
          >
            <svg class="sugg-ic" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="11" cy="11" r="7" />
              <path d="M21 21l-4.3-4.3" />
            </svg>
            <span class="sugg-txt">
              <template v-for="(part, pi) in splitHighlight(word)" :key="pi">
                <b v-if="part.hit">{{ part.text }}</b>
                <template v-else>{{ part.text }}</template>
              </template>
            </span>
          </div>
        </div>

        <div v-else class="sugg-empty">
          <svg width="44" height="44" viewBox="0 0 48 48" fill="none" stroke="#D9D9D9" stroke-width="2">
            <circle cx="22" cy="22" r="14" />
            <path d="M33 33l8 8" stroke-linecap="round" />
          </svg>
          <div class="sugg-empty-text">无匹配联想词<br>按回车搜索全部</div>
        </div>
      </template>

      <!-- 默认态：历史 + 热搜 + 热门分类 -->
      <template v-else>
        <!-- 搜索历史 -->
        <section v-if="history.length > 0" class="sec">
          <div class="sec-head">
            <div class="sec-title">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3 2" stroke-linecap="round" />
              </svg>
              搜索历史
            </div>
            <button class="sec-action" type="button" @click="clearHistory">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
                <path d="M4 7h16M9 7V5h6v2M6 7l1 13a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-13" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
              清空
            </button>
          </div>
          <div class="tag-row">
            <button v-for="word in history" :key="word" class="tag" type="button" @click="doSearch(word)">
              {{ word }}
            </button>
          </div>
        </section>

        <div class="divider" />

        <!-- 热门搜索 -->
        <section class="sec">
          <div class="sec-head">
            <div class="sec-title">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#FF4D4F" stroke-width="1.8" stroke-linejoin="round">
                <path d="M12 3s4 4.5 4 8a4 4 0 0 1-8 0c0-1 .5-2 1-2.5C9 11 8 13 8 15a4 4 0 0 0 8 0c0-4-4-7-4-12z" />
              </svg>
              热门搜索
            </div>
          </div>
          <div class="hot-list">
            <div
              v-for="(hot, index) in hotWords"
              :key="hot.label"
              class="hot-item"
              :class="{ top: index < 3 }"
              @click="doSearch(hot.label)"
            >
              <div class="hot-rank">{{ index + 1 }}</div>
              <div class="hot-word">
                {{ hot.label }}
                <span v-if="hotBadge(index)" class="hot-tag" :class="{ new: hotBadge(index) === '热' }">{{ hotBadge(index) }}</span>
              </div>
              <div class="hot-index">搜索指数 {{ hot.value }}</div>
            </div>
          </div>
        </section>

        <div class="divider" />

        <!-- 热门分类 -->
        <section v-if="hotCategories.length > 0" class="sec">
          <div class="sec-head">
            <div class="sec-title">热门分类</div>
          </div>
          <div class="cat-row">
            <button v-for="cat in hotCategories" :key="cat.id" class="cat-chip" type="button" @click="goCategory(cat)">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
                <rect x="3" y="3" width="7" height="7" rx="1.5" />
                <rect x="14" y="3" width="7" height="7" rx="1.5" />
                <rect x="3" y="14" width="7" height="7" rx="1.5" />
                <rect x="14" y="14" width="7" height="7" rx="1.5" />
              </svg>
              {{ cat.name }}
            </button>
          </div>
        </section>
      </template>
    </div>
  </div>
</template>

<style scoped>
.search-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n1);
}

/* 顶部搜索栏 */
.search-top {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s2);
  flex-shrink: 0;
}

.back-btn {
  display: flex;
  align-items: center;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}

.search-input {
  flex: 1;
  height: 32px;
  background: var(--n3);
  border-radius: 16px;
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

.search-input input::placeholder {
  color: var(--n7);
}

.search-btn {
  font-size: var(--fs-base);
  color: var(--c-primary);
  font-weight: var(--fw-medium);
  background: none;
  border: none;
  cursor: pointer;
  padding: 0 var(--s1);
  font-family: inherit;
  flex-shrink: 0;
}

/* 内容区 */
.content {
  flex: 1;
  overflow-y: auto;
  background: var(--n1);
}

.sec {
  padding: var(--s4) var(--s3);
}

.sec-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s3);
}

.sec-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n9);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.sec-action {
  color: var(--n7);
  display: flex;
  align-items: center;
  gap: 3px;
  font-size: var(--fs-sm);
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  padding: 0;
}

.divider {
  height: 8px;
  background: var(--n2);
}

/* 搜索历史 */
.tag-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}

.tag {
  background: var(--n3);
  border-radius: var(--r-lg);
  padding: 6px var(--s3);
  font-size: var(--fs-base);
  color: var(--n9);
  cursor: pointer;
  border: none;
  font-family: inherit;
}

/* 热门搜索 */
.hot-list {
  display: flex;
  flex-direction: column;
}

.hot-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: 10px 0;
  border-bottom: 1px solid var(--n3);
  cursor: pointer;
}

.hot-item:last-child {
  border-bottom: none;
}

.hot-rank {
  width: 20px;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n7);
  text-align: center;
  flex-shrink: 0;
}

.hot-item.top .hot-rank {
  color: var(--c-error);
}

.hot-word {
  flex: 1;
  font-size: var(--fs-base);
  color: var(--n10);
  min-width: 0;
}

.hot-item.top .hot-word {
  font-weight: var(--fw-medium);
}

.hot-index {
  font-size: var(--fs-sm);
  color: var(--n7);
  flex-shrink: 0;
}

.hot-tag {
  font-size: 10px;
  color: var(--c-error);
  border: 1px solid var(--c-error);
  padding: 1px 5px;
  border-radius: var(--r-base);
  margin-left: var(--s1);
}

.hot-tag.new {
  color: var(--c-warning);
  border-color: var(--c-warning);
}

/* 热门分类 */
.cat-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}

.cat-chip {
  display: flex;
  align-items: center;
  gap: var(--s1);
  background: var(--n3);
  border-radius: var(--r-lg);
  padding: 6px var(--s3);
  font-size: var(--fs-base);
  color: var(--n9);
  cursor: pointer;
  border: none;
  font-family: inherit;
}

.cat-chip svg {
  color: var(--c-primary);
}

/* 输入联想 */
.sugg-list {
  display: flex;
  flex-direction: column;
  padding: var(--s2) var(--s3);
}

.sugg-item {
  display: flex;
  align-items: center;
  gap: var(--s2);
  padding: 10px 0;
  border-bottom: 1px solid var(--n3);
  font-size: var(--fs-base);
  cursor: pointer;
}

.sugg-item:last-child {
  border-bottom: none;
}

.sugg-ic {
  color: var(--n7);
  flex-shrink: 0;
}

.sugg-txt {
  flex: 1;
  color: var(--n10);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sugg-txt b {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.sugg-loading {
  font-size: var(--fs-sm);
  color: var(--n7);
  text-align: center;
  padding: var(--s2) 0;
}

.sk-ic {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sk-tx {
  height: 12px;
  border-radius: 3px;
}

/* 联想空态 */
.sugg-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  color: var(--n7);
  padding: 64px 0;
}

.sugg-empty-text {
  font-size: var(--fs-sm);
  text-align: center;
  line-height: 1.8;
}
</style>
