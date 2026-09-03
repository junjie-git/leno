<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { historyApi } from '@/modules/13-profile/api/history.api'
import type { BrowseHistoryDto } from '../types/profile.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 浏览历史页（/profile/history）
 *
 * 结构（对齐设计稿 history）：
 * NavBar（返回 + 浏览历史 + 管理态切换）→ 按日期分组的时间轴列表
 * （今天 / 昨天 / 具体 MM-DD，分组头部展示当天件数；卡片含主图 + 标题 + 店铺 + 价格 + 浏览时间）
 * → 底部固定操作栏（浏览态「清空全部」；管理态 全选 / 删除(N) / 完成，适配 safe-area）
 *
 * 交互：
 * - 点击卡片跳商品详情 /product/{spuId}（详情页自动上报一条新历史）
 * - 管理态支持勾选后批量删除（二次确认）与单条删除（乐观移除，失败回滚）
 * - 清空全部需二次确认（危险操作红色确认）
 */
const router = useRouter()

/** 日期分组结构 */
interface HistoryGroup {
  /** 分组日期（YYYY-MM-DD） */
  key: string
  /** 展示标签（今天 / 昨天 / MM-DD） */
  label: string
  items: BrowseHistoryDto[]
}

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const history = ref<BrowseHistoryDto[]>([])
const refreshing = ref(false)

// ---- 管理态 ----
const manageMode = ref(false)
const selectedIds = ref<string[]>([])
const batchRemoving = ref(false)
const clearing = ref(false)

/** 今天的日期串（本地时区） */
const todayKey = formatDate(new Date().toISOString())

/** 昨天的日期串（本地时区） */
const yesterdayKey = formatDate(new Date(Date.now() - 86_400_000).toISOString())

/** 分组标签 */
function groupLabel(key: string): string {
  if (key === todayKey) return '今天'
  if (key === yesterdayKey) return '昨天'
  return key.slice(5)
}

/** 浏览时间（HH:mm） */
function formatTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** 按日期分组（保持列表倒序） */
const groups = computed<HistoryGroup[]>(() => {
  const result: HistoryGroup[] = []
  const index = new Map<string, HistoryGroup>()
  for (const item of history.value) {
    const key = formatDate(item.viewedAt)
    let group = index.get(key)
    if (!group) {
      group = { key, label: groupLabel(key), items: [] }
      index.set(key, group)
      result.push(group)
    }
    group.items.push(item)
  }
  return result
})

/** 是否全选 */
const isAllSelected = computed(
  () => history.value.length > 0 && selectedIds.value.length === history.value.length,
)

/** 已选数量 */
const selectedCount = computed(() => selectedIds.value.length)

onMounted(() => {
  void loadHistory()
})

/** 加载浏览历史 */
async function loadHistory(silent = false): Promise<void> {
  if (!silent) {
    loading.value = true
  }
  loadError.value = false
  try {
    history.value = await historyApi.list()
    // 列表重置后清理已失效的勾选
    const ids = new Set(history.value.map((h) => h.id))
    selectedIds.value = selectedIds.value.filter((id) => ids.has(id))
  } catch (e) {
    logger.error('浏览历史加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await loadHistory(true)
}

// ---- 跳转 ----
function goProduct(spuId: string): void {
  if (manageMode.value) return
  router.push(`/product/${spuId}`)
}

function goHome(): void {
  router.push('/')
}

// ---- 管理态 ----
function toggleManage(): void {
  manageMode.value = !manageMode.value
  selectedIds.value = []
}

function toggleSelect(id: string): void {
  const idx = selectedIds.value.indexOf(id)
  if (idx >= 0) {
    selectedIds.value.splice(idx, 1)
  } else {
    selectedIds.value.push(id)
  }
}

function toggleSelectAll(): void {
  if (isAllSelected.value) {
    selectedIds.value = []
  } else {
    selectedIds.value = history.value.map((h) => h.id)
  }
}

/** 单条删除（乐观移除，失败回滚） */
async function removeOne(item: BrowseHistoryDto): Promise<void> {
  const idx = history.value.findIndex((h) => h.id === item.id)
  if (idx < 0) return
  const backup = history.value[idx]
  history.value.splice(idx, 1)
  selectedIds.value = selectedIds.value.filter((id) => id !== item.id)
  try {
    await historyApi.remove(item.id)
    showToast('已删除')
  } catch (e) {
    history.value.splice(idx, 0, backup)
    logger.error('删除浏览记录失败', e)
    showFailToast('删除失败，请稍后重试')
  }
}

/** 批量删除（二次确认） */
async function removeSelected(): Promise<void> {
  if (selectedCount.value === 0 || batchRemoving.value) return
  try {
    await showConfirmDialog({
      title: '确认删除',
      message: `将删除已选 ${selectedCount.value} 条浏览记录，此操作不可恢复。`,
      confirmButtonText: '删除',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  batchRemoving.value = true
  const ids = [...selectedIds.value]
  try {
    await historyApi.batchRemove(ids)
    showToast('已删除')
    selectedIds.value = []
    if (history.value.length === ids.length) {
      manageMode.value = false
      history.value = []
    } else {
      await loadHistory(true)
    }
  } catch (e) {
    logger.error('批量删除浏览记录失败', e)
    showFailToast(e instanceof Error ? e.message : '删除失败，请稍后重试')
  } finally {
    batchRemoving.value = false
  }
}

/** 清空全部（二次确认） */
async function clearAll(): Promise<void> {
  if (history.value.length === 0 || clearing.value) return
  try {
    await showConfirmDialog({
      title: '确认清空',
      message: '将清空全部浏览历史记录，此操作不可恢复。',
      confirmButtonText: '清空',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  clearing.value = true
  try {
    await historyApi.clear()
    showToast('已清空')
    history.value = []
    selectedIds.value = []
    manageMode.value = false
  } catch (e) {
    logger.error('清空浏览历史失败', e)
    showFailToast(e instanceof Error ? e.message : '清空失败，请稍后重试')
  } finally {
    clearing.value = false
  }
}

// ---- 返回 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/profile')
  }
}
</script>

<template>
  <div class="history-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">浏览历史</div>
      <button class="manage-btn" type="button" @click="toggleManage">
        {{ manageMode ? '完成' : '管理' }}
      </button>
    </header>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <template v-if="loading">
        <div v-for="g in 2" :key="g" class="date-group">
          <div class="date-header">
            <div class="skeleton-block sk-date-label" />
            <div class="skeleton-block sk-date-count" />
          </div>
          <div v-for="i in 2" :key="i" class="sk-card">
            <div class="skeleton-block sk-img" />
            <div class="sk-lines">
              <div class="skeleton-block sk-l1" />
              <div class="skeleton-block sk-l2" />
              <div class="sk-bottom">
                <div class="skeleton-block sk-price" />
                <div class="skeleton-block sk-time" />
              </div>
            </div>
          </div>
        </div>
      </template>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError && history.length === 0"
        title="浏览历史加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadHistory()"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="history.length === 0"
        title="暂无浏览记录"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 分组列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <div
          v-for="group in groups"
          :key="group.key"
          class="date-group"
          role="group"
          :aria-label="`浏览记录 ${group.label}`"
        >
          <div class="date-header">
            <span class="date-label">
              <van-icon name="clock-o" size="14" color="#1677FF" />
              {{ group.label }}
            </span>
            <span class="date-count">{{ group.items.length }} 件</span>
          </div>

          <div class="card-list">
            <article
              v-for="item in group.items"
              :key="item.id"
              class="history-card"
              role="article"
              :aria-label="item.name"
              @click="manageMode ? toggleSelect(item.id) : goProduct(item.spuId)"
            >
              <!-- 管理态勾选框 -->
              <div
                v-if="manageMode"
                class="check-box"
                :class="{ checked: selectedIds.includes(item.id) }"
                role="checkbox"
                :aria-checked="selectedIds.includes(item.id)"
                :aria-label="`选择 ${item.name}`"
                @click.stop="toggleSelect(item.id)"
              >
                <van-icon v-if="selectedIds.includes(item.id)" name="success" size="12" color="#fff" />
              </div>

              <img class="img" :src="item.mainImage" :alt="item.name" loading="lazy">

              <div class="info">
                <div class="name text-ellipsis">{{ item.name }}</div>
                <div class="shop text-ellipsis">{{ item.shopName }}</div>
                <div class="meta-row">
                  <PriceText :amount="item.price" :size="16" />
                  <div class="meta-right">
                    <span class="time">{{ formatTime(item.viewedAt) }}</span>
                    <!-- 管理态单条删除 -->
                    <button
                      v-if="manageMode"
                      class="item-delete"
                      type="button"
                      aria-label="删除该记录"
                      @click.stop="removeOne(item)"
                    >
                      <van-icon name="delete-o" size="14" color="#FF4D4F" />
                    </button>
                  </div>
                </div>
              </div>
            </article>
          </div>
        </div>
      </van-pull-refresh>
    </div>

    <!-- 底部操作栏 -->
    <footer class="bottom-bar">
      <!-- 管理态：全选 + 删除 -->
      <template v-if="manageMode">
        <button class="batch-select-all" type="button" role="checkbox" :aria-checked="isAllSelected" @click="toggleSelectAll">
          <span class="batch-checkbox" :class="{ checked: isAllSelected }">
            <van-icon v-if="isAllSelected" name="success" size="12" color="#fff" />
          </span>
          全选
        </button>
        <button
          class="batch-btn delete"
          type="button"
          :disabled="selectedCount === 0 || batchRemoving"
          @click="removeSelected"
        >
          删除{{ selectedCount > 0 ? `(${selectedCount})` : '' }}
        </button>
      </template>

      <!-- 浏览态：清空全部 -->
      <button
        v-else
        class="clear-btn"
        type="button"
        :disabled="history.length === 0 || clearing"
        @click="clearAll"
      >
        <van-icon name="delete-o" size="16" color="#FF4D4F" />
        清空全部历史
      </button>
    </footer>
  </div>
</template>

<style scoped>
.history-page {
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
}

.manage-btn {
  font-size: var(--fs-base);
  color: var(--c-primary);
  background: none;
  border: none;
  padding: var(--s1) 0;
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s12) + env(safe-area-inset-bottom));
}

/* 日期分组 */
.date-group {
  margin-bottom: var(--s3);
}

.date-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 32px;
  padding: 0 var(--s1);
}

.date-label {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--c-primary);
}

.date-count {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.card-list {
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}

/* 历史卡片 */
.history-card {
  display: flex;
  gap: var(--s2);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s2);
  position: relative;
}

.history-card .img {
  width: 80px;
  height: 80px;
  border-radius: var(--r-card);
  object-fit: cover;
  background: var(--n3);
  flex-shrink: 0;
}

.history-card .info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.name {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
}

.shop {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
}

.meta-row {
  margin-top: auto;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--s2);
}

.meta-right {
  display: flex;
  align-items: center;
  gap: var(--s2);
  flex-shrink: 0;
}

.time {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.item-delete {
  width: 24px;
  height: 24px;
  border-radius: var(--r-base);
  background: #fff1f0;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 管理态勾选框 */
.check-box {
  position: absolute;
  top: var(--s2);
  left: var(--s2);
  width: 20px;
  height: 20px;
  border: 2px solid var(--n5);
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.85);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1;
}

.check-box.checked {
  background: var(--c-primary);
  border-color: var(--c-primary);
}

/* 骨架屏 */
.sk-date-label {
  width: 50px;
  height: 14px;
}

.sk-date-count {
  width: 40px;
  height: 12px;
}

.sk-card {
  display: flex;
  gap: var(--s2);
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s2);
  margin-bottom: var(--s2);
}

.sk-img {
  width: 80px;
  height: 80px;
  border-radius: var(--r-card);
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s1);
}

.sk-l1 {
  width: 90%;
  height: 14px;
}

.sk-l2 {
  width: 60%;
  height: 12px;
}

.sk-bottom {
  margin-top: auto;
  display: flex;
  justify-content: space-between;
}

.sk-price {
  width: 50px;
  height: 16px;
}

.sk-time {
  width: 40px;
  height: 12px;
}

/* 底部操作栏 */
.bottom-bar {
  position: sticky;
  bottom: 0;
  background: var(--n1);
  display: flex;
  align-items: center;
  padding: var(--s2) var(--s3);
  border-top: 1px solid var(--n3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
  flex-shrink: 0;
}

.clear-btn {
  flex: 1;
  height: 44px;
  border: 1px solid var(--c-error);
  border-radius: var(--r-lg);
  color: var(--c-error);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
  background: var(--n1);
}

.clear-btn:disabled {
  border-color: var(--n5);
  color: var(--n7);
}

.batch-select-all {
  display: flex;
  align-items: center;
  gap: var(--s1);
  font-size: var(--fs-base);
  color: var(--n10);
  padding: var(--s2);
}

.batch-checkbox {
  width: 20px;
  height: 20px;
  border: 2px solid var(--n5);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.batch-checkbox.checked {
  background: var(--c-primary);
  border-color: var(--c-primary);
}

.batch-btn {
  margin-left: auto;
  padding: var(--s2) var(--s6);
  border: none;
  border-radius: var(--r-lg);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
}

.batch-btn.delete {
  background: var(--c-error);
  color: #fff;
}

.batch-btn.delete:disabled {
  background: var(--n3);
  color: var(--n7);
}
</style>
