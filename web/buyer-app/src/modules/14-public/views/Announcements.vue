<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { publicApi } from '@/modules/14-public/api/public.api'
import type { AnnouncementDto, AnnouncementType } from '../types/public.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatRelativeTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 平台公告页（/announcements，游客可访问）
 *
 * 结构（对齐设计稿 announcements）：
 * NavBar（返回 + 平台公告）→ 公告卡片列表（下拉刷新）
 * → 公告卡（类型标签 + 置顶标识 + 标题 + 摘要 + 发布时间，点击折叠展开正文）
 *
 * 类型映射：Promotion → 活动（红）、Maintenance → 维护（黄）、System → 公告（绿）；
 * 置顶公告由服务端排在列表顶部并展示「置顶」标签。
 */
const router = useRouter()

/** 公告类型展示元信息（标签文案 + 配色） */
const TYPE_META: Record<AnnouncementType, { label: string; cls: string; icon: string }> = {
  Promotion: { label: '活动', cls: 'tag-promotion', icon: 'gift-o' },
  Maintenance: { label: '维护', cls: 'tag-maintenance', icon: 'setting-o' },
  System: { label: '公告', cls: 'tag-system', icon: 'bell' },
}

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const announcements = ref<AnnouncementDto[]>([])
const refreshing = ref(false)

/** 当前展开正文的公告 id（空表示全部折叠） */
const expandedId = ref('')

onMounted(() => {
  void loadAnnouncements()
})

/** 加载公告列表 */
async function loadAnnouncements(silent = false): Promise<void> {
  if (!silent) {
    loading.value = true
  }
  loadError.value = false
  try {
    announcements.value = await publicApi.listAnnouncements()
  } catch (e) {
    logger.error('公告列表加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await loadAnnouncements(true)
}

/** 类型元信息 */
function typeMeta(type: AnnouncementType): { label: string; cls: string; icon: string } {
  return TYPE_META[type] ?? TYPE_META.System
}

/** 展开 / 折叠正文 */
function toggleExpand(id: string): void {
  expandedId.value = expandedId.value === id ? '' : id
}

function goHome(): void {
  router.push('/')
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
  <div class="announcements-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">平台公告</div>
    </header>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="ann-list">
        <div v-for="i in 5" :key="i" class="sk-card">
          <div class="skeleton-block sk-tag" />
          <div class="skeleton-block sk-line long" />
          <div class="skeleton-block sk-line mid" />
          <div class="skeleton-block sk-line short" />
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="公告加载失败"
        description="网络异常，请稍后重试"
        @retry="loadAnnouncements()"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="announcements.length === 0"
        title="暂无公告"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 公告列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <div class="ann-list">
          <article
            v-for="item in announcements"
            :key="item.id"
            class="ann-card"
            :class="{ expanded: expandedId === item.id }"
            role="article"
            :aria-label="item.title"
            @click="toggleExpand(item.id)"
          >
            <div class="ann-head">
              <span class="ann-tag" :class="typeMeta(item.type).cls">
                <van-icon :name="typeMeta(item.type).icon" size="12" />
                {{ typeMeta(item.type).label }}
              </span>
              <span v-if="item.pinned" class="ann-tag pinned">
                <van-icon name="top-o" size="12" />
                置顶
              </span>
            </div>

            <div class="ann-title">{{ item.title }}</div>

            <!-- 折叠态：摘要两行截断 -->
            <div v-if="expandedId !== item.id" class="ann-summary">{{ item.content }}</div>

            <!-- 展开态：完整正文 -->
            <div v-else class="ann-content">{{ item.content }}</div>

            <div class="ann-foot">
              <span class="ann-time">
                <van-icon name="clock-o" size="12" />
                {{ formatRelativeTime(item.publishedAt) }}
              </span>
              <span class="ann-toggle">
                {{ expandedId === item.id ? '收起' : '展开正文' }}
                <van-icon
                  name="down"
                  size="12"
                  :class="{ up: expandedId === item.id }"
                />
              </span>
            </div>
          </article>
        </div>
        <div class="list-end">— 没有更多公告了 —</div>
      </van-pull-refresh>
    </div>
  </div>
</template>

<style scoped>
.announcements-page {
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

.ann-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

/* 公告卡片 */
.ann-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.ann-head {
  display: flex;
  align-items: center;
  gap: var(--s1);
  margin-bottom: var(--s2);
}

.ann-tag {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 11px;
  padding: 1px var(--s2);
  border-radius: var(--r-base);
  font-weight: var(--fw-medium);
}

.tag-promotion {
  background: #fff1f0;
  color: var(--c-error);
}

.tag-maintenance {
  background: #fff7e6;
  color: var(--c-warning);
}

.tag-system {
  background: #f6ffed;
  color: var(--c-success);
}

.ann-tag.pinned {
  background: #fff1f0;
  color: var(--c-error);
}

.ann-title {
  font-size: 15px;
  font-weight: var(--fw-medium);
  color: var(--n10);
  line-height: 1.5;
}

.ann-summary {
  margin-top: var(--s1);
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.ann-content {
  margin-top: var(--s2);
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.7;
  background: var(--n2);
  border-radius: var(--r-base);
  padding: var(--s2) var(--s3);
}

.ann-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--s2);
}

.ann-time {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: var(--fs-sm);
  color: var(--n7);
}

.ann-toggle {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  font-size: var(--fs-sm);
  color: var(--c-primary);
}

.ann-toggle :deep(.van-icon.up) {
  transform: rotate(180deg);
}

.list-end {
  text-align: center;
  padding: var(--s3) 0;
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 骨架屏 */
.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3);
}

.sk-tag {
  width: 44px;
  height: 18px;
  border-radius: var(--r-base);
  margin-bottom: var(--s2);
}

.sk-line {
  height: 14px;
  margin-bottom: var(--s1);
}

.sk-line.long {
  width: 90%;
}

.sk-line.mid {
  width: 70%;
}

.sk-line.short {
  width: 45%;
}
</style>
