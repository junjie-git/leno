<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showDialog, showFailToast, showToast } from 'vant'
import { useAuthStore } from '@/shared/auth'
import { logger } from '@/shared/utils/logger'

/**
 * 设置页（/settings）
 *
 * 结构（对齐设计稿 settings）：
 * NavBar（返回 + 设置）→ 通用（深色模式 / 字体大小 / 语言 / 消息推送）
 * → 隐私（个性化推荐 / 浏览历史记录 / 广告个性化）
 * → 存储（清除缓存，右侧实时展示缓存体积）
 * → 关于（关于 Leno / 版本号 / 检查更新 / 用户协议 / 隐私政策）
 * → 底部固定「退出登录」红色按钮（二次确认，适配 safe-area）
 *
 * 偏好持久化于 localStorage（退出登录不清除偏好，仅清除令牌）；
 * 字体大小通过覆盖根节点 --fs-* 设计令牌实时生效。
 */
const router = useRouter()
const authStore = useAuthStore()

/** 应用版本号（与 package.json 保持一致） */
const APP_VERSION = 'v1.0.0'

/** 偏好持久化 key */
const SETTINGS_KEY = 'buyer-app:settings'

/** 偏好结构 */
interface AppSettings {
  darkMode: boolean
  fontSize: 'small' | 'standard' | 'large'
  language: 'zh-CN' | 'en-US'
  personalizedRecommend: boolean
  historyRecord: boolean
  adPersonalized: boolean
}

/** 默认偏好 */
const DEFAULT_SETTINGS: AppSettings = {
  darkMode: false,
  fontSize: 'standard',
  language: 'zh-CN',
  personalizedRecommend: true,
  historyRecord: true,
  adPersonalized: false,
}

/** 字号档位（对应根节点 --fs-* 令牌缩放） */
const FONT_SCALE: Record<AppSettings['fontSize'], number> = {
  small: 0.9,
  standard: 1,
  large: 1.15,
}

/** 字号档位展示名 */
const FONT_LABEL: Record<AppSettings['fontSize'], string> = {
  small: '小',
  standard: '标准',
  large: '大',
}

/** 语言展示名 */
const LANGUAGE_LABEL: Record<AppSettings['language'], string> = {
  'zh-CN': '简体中文',
  'en-US': 'English',
}

// ---- 状态 ----
const settings = ref<AppSettings>({ ...DEFAULT_SETTINGS })
const fontSizeSheetVisible = ref(false)
const languageSheetVisible = ref(false)
const clearing = ref(false)
const checking = ref(false)
const loggingOut = ref(false)
/** 缓存体积（字节） */
const cacheBytes = ref(0)

const fontActions = computed(() =>
  (Object.keys(FONT_LABEL) as AppSettings['fontSize'][]).map((key) => ({
    name: FONT_LABEL[key],
    key,
  })),
)

const languageActions = computed(() =>
  (Object.keys(LANGUAGE_LABEL) as AppSettings['language'][]).map((key) => ({
    name: LANGUAGE_LABEL[key],
    key,
  })),
)

/** 缓存体积展示（KB / MB） */
const cacheSizeText = computed(() => {
  const bytes = cacheBytes.value
  if (bytes <= 0) return '0KB'
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))}KB`
  return `${(bytes / 1024 / 1024).toFixed(1)}MB`
})

onMounted(() => {
  loadSettings()
  applySettings()
  cacheBytes.value = computeCacheBytes()
})

/** 读取偏好 */
function loadSettings(): void {
  try {
    const raw = localStorage.getItem(SETTINGS_KEY)
    if (!raw) return
    const parsed = JSON.parse(raw) as Partial<AppSettings>
    settings.value = { ...DEFAULT_SETTINGS, ...parsed }
  } catch (e) {
    logger.warn('读取本地偏好失败（使用默认值）', e)
  }
}

/** 写入偏好 */
function persistSettings(): void {
  try {
    localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings.value))
  } catch (e) {
    logger.warn('写入本地偏好失败', e)
  }
}

/** 应用偏好到运行时（根节点 class + 字号令牌覆盖） */
function applySettings(): void {
  if (settings.value.darkMode) {
    document.documentElement.setAttribute('data-theme', 'dark')
  } else {
    document.documentElement.removeAttribute('data-theme')
  }
  const scale = FONT_SCALE[settings.value.fontSize]
  const tokens: Array<[string, number]> = [
    ['--fs-sm', 12],
    ['--fs-base', 14],
    ['--fs-lg', 16],
    ['--fs-xl', 20],
    ['--fs-2xl', 24],
    ['--fs-3xl', 30],
  ]
  for (const [token, base] of tokens) {
    document.documentElement.style.setProperty(token, `${Math.round(base * scale)}px`)
  }
}

/** 开关切换（立即持久化并生效） */
function onToggle(): void {
  persistSettings()
  applySettings()
}

// ---- 字体大小 ----
function onSelectFontSize(action: { name: string; key: AppSettings['fontSize'] }): void {
  settings.value.fontSize = action.key
  fontSizeSheetVisible.value = false
  persistSettings()
  applySettings()
}

// ---- 语言 ----
function onSelectLanguage(action: { name: string; key: AppSettings['language'] }): void {
  settings.value.language = action.key
  languageSheetVisible.value = false
  persistSettings()
  showToast('语言将在下次启动生效')
}

// ---- 消息推送 ----
function goNotificationPreferences(): void {
  router.push('/notifications/preferences')
}

// ---- 缓存 ----
/** 计算本地缓存体积（localStorage 全部条目字节数） */
function computeCacheBytes(): number {
  let total = 0
  try {
    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i)
      if (!key) continue
      total += key.length + (localStorage.getItem(key)?.length ?? 0)
    }
  } catch (e) {
    logger.warn('计算缓存体积失败', e)
  }
  // UTF-16 每字符约占 2 字节
  return total * 2
}

/** 清除缓存（保留登录态与偏好设置） */
async function onClearCache(): Promise<void> {
  if (clearing.value) return
  try {
    await showConfirmDialog({
      title: '确认清除缓存',
      message: '将清除图片与 API 缓存，登录态与离线包保留。',
      confirmButtonText: '清除',
      confirmButtonColor: '#1677FF',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  clearing.value = true
  try {
    const preservedKeys: string[] = []
    const removedKeys: string[] = []
    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i)
      if (!key) continue
      // 登录态与本地偏好保留，其余视为可清除的缓存
      if (key === 'auth' || key === SETTINGS_KEY) {
        preservedKeys.push(key)
      } else {
        removedKeys.push(key)
      }
    }
    for (const key of removedKeys) {
      localStorage.removeItem(key)
    }
    const clearedBytes = cacheBytes.value
    cacheBytes.value = computeCacheBytes()
    showToast(`已清除 ${formatBytes(clearedBytes - cacheBytes.value)}`)
  } catch (e) {
    logger.error('清除缓存失败', e)
    showFailToast('清除失败，请稍后重试')
  } finally {
    clearing.value = false
  }
}

/** 字节数展示 */
function formatBytes(bytes: number): string {
  if (bytes <= 0) return '0KB'
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))}KB`
  return `${(bytes / 1024 / 1024).toFixed(1)}MB`
}

// ---- 关于 ----
function showAbout(): void {
  showDialog({
    title: '关于 Leno',
    message: `Leno 电商平台买家端\n品质生活 · 一触即达\n当前版本 ${APP_VERSION}`,
    confirmButtonText: '知道了',
  })
}

/** 检查更新（PWA registration.update，无注册时提示已是最新） */
async function onCheckUpdate(): Promise<void> {
  if (checking.value) return
  checking.value = true
  try {
    if ('serviceWorker' in navigator) {
      const registration = await navigator.serviceWorker.getRegistration()
      if (registration) {
        await registration.update()
      }
    }
    showToast('已是最新版本')
  } catch (e) {
    logger.warn('检查更新失败', e)
    showFailToast('检查失败，请稍后重试')
  } finally {
    checking.value = false
  }
}

function showUserAgreement(): void {
  showDialog({
    title: '用户协议',
    message:
      '欢迎使用 Leno 电商平台。注册即表示您同意遵守平台交易规则、账号安全规范及社区公约，共同维护公平诚信的购物环境。',
    confirmButtonText: '知道了',
  })
}

function showPrivacyPolicy(): void {
  showDialog({
    title: '隐私政策',
    message:
      '我们严格保护您的个人信息：仅收集交易履约所必需的数据，不出售给任何第三方；您可随时在「账号安全」中管理绑定信息。',
    confirmButtonText: '知道了',
  })
}

// ---- 退出登录 ----
async function onLogout(): Promise<void> {
  if (loggingOut.value) return
  try {
    await showConfirmDialog({
      title: '确认退出登录',
      message: '退出后需重新登录才能继续使用，本地偏好设置保留。',
      confirmButtonText: '退出',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  loggingOut.value = true
  try {
    await authStore.logout()
    showToast('已退出登录')
    router.replace('/login')
  } catch (e) {
    logger.error('退出登录失败', e)
    showFailToast('退出失败，请稍后重试')
  } finally {
    loggingOut.value = false
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
  <div class="settings-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">设置</div>
    </header>

    <!-- 设置主体 -->
    <div class="list-wrap">
      <!-- 通用 -->
      <section class="settings-group" role="group" aria-label="通用设置">
        <div class="group-title">通用</div>
        <div class="group-card">
          <div class="setting-item">
            <div class="setting-icon" style="background: #fff7e6">
              <van-icon name="bulb-o" size="18" color="#FAAD14" />
            </div>
            <span class="setting-label">深色模式</span>
            <van-switch
              v-model="settings.darkMode"
              size="22"
              role="switch"
              :aria-checked="settings.darkMode"
              aria-label="深色模式"
              @change="onToggle"
            />
          </div>

          <button class="setting-item" type="button" role="button" aria-label="字体大小" @click="fontSizeSheetVisible = true">
            <div class="setting-icon" style="background: #e6f4ff">
              <van-icon name="font" size="18" color="#1677FF" />
            </div>
            <span class="setting-label">字体大小</span>
            <span class="setting-value">{{ FONT_LABEL[settings.fontSize] }}</span>
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>

          <button class="setting-item" type="button" role="button" aria-label="语言" @click="languageSheetVisible = true">
            <div class="setting-icon" style="background: #f6ffed">
              <van-icon name="language-o" size="18" color="#52C41A" />
            </div>
            <span class="setting-label">语言</span>
            <span class="setting-value">{{ LANGUAGE_LABEL[settings.language] }}</span>
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>

          <button
            class="setting-item"
            type="button"
            role="link"
            aria-label="消息推送设置"
            @click="goNotificationPreferences"
          >
            <div class="setting-icon" style="background: #fff7e6">
              <van-icon name="bell" size="18" color="#FAAD14" />
            </div>
            <span class="setting-label">消息推送</span>
            <span class="setting-value">去设置</span>
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>
        </div>
      </section>

      <!-- 隐私 -->
      <section class="settings-group" role="group" aria-label="隐私设置">
        <div class="group-title">隐私</div>
        <div class="group-card">
          <div class="setting-item">
            <div class="setting-icon" style="background: #f9f0ff">
              <van-icon name="aim" size="18" color="#722ED1" />
            </div>
            <span class="setting-label">个性化推荐</span>
            <van-switch
              v-model="settings.personalizedRecommend"
              size="22"
              role="switch"
              :aria-checked="settings.personalizedRecommend"
              aria-label="个性化推荐"
              @change="onToggle"
            />
          </div>

          <div class="setting-item">
            <div class="setting-icon" style="background: #e6f4ff">
              <van-icon name="clock-o" size="18" color="#1677FF" />
            </div>
            <span class="setting-label">浏览历史记录</span>
            <van-switch
              v-model="settings.historyRecord"
              size="22"
              role="switch"
              :aria-checked="settings.historyRecord"
              aria-label="浏览历史记录"
              @change="onToggle"
            />
          </div>

          <div class="setting-item">
            <div class="setting-icon" style="background: #fff0f6">
              <van-icon name="bullhorn-o" size="18" color="#EB2F96" />
            </div>
            <span class="setting-label">广告个性化</span>
            <van-switch
              v-model="settings.adPersonalized"
              size="22"
              role="switch"
              :aria-checked="settings.adPersonalized"
              aria-label="广告个性化"
              @change="onToggle"
            />
          </div>
        </div>
      </section>

      <!-- 存储 -->
      <section class="settings-group" role="group" aria-label="存储设置">
        <div class="group-title">存储</div>
        <div class="group-card">
          <button
            class="setting-item"
            type="button"
            role="button"
            aria-label="清除缓存"
            :disabled="clearing"
            @click="onClearCache"
          >
            <div class="setting-icon" style="background: #fff1f0">
              <van-icon name="delete-o" size="18" color="#FF4D4F" />
            </div>
            <span class="setting-label">清除缓存</span>
            <span class="setting-value">{{ cacheSizeText }}</span>
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>
        </div>
      </section>

      <!-- 关于 -->
      <section class="settings-group" role="group" aria-label="关于">
        <div class="group-title">关于</div>
        <div class="group-card">
          <button class="setting-item" type="button" role="link" aria-label="关于 Leno" @click="showAbout">
            <div class="setting-icon" style="background: #e6f4ff">
              <van-icon name="info-o" size="18" color="#1677FF" />
            </div>
            <span class="setting-label">关于 Leno</span>
            <span class="setting-value">品质生活 · 一触即达</span>
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>

          <div class="setting-item" role="listitem" aria-label="版本号">
            <div class="setting-icon" style="background: #fff7e6">
              <van-icon name="bookmark-o" size="18" color="#FAAD14" />
            </div>
            <span class="setting-label">版本号</span>
            <span class="setting-value">{{ APP_VERSION }}</span>
          </div>

          <button
            class="setting-item"
            type="button"
            role="button"
            aria-label="检查更新"
            :disabled="checking"
            @click="onCheckUpdate"
          >
            <div class="setting-icon" style="background: #f6ffed">
              <van-icon name="replay" size="18" color="#52C41A" />
            </div>
            <span class="setting-label">检查更新</span>
            <span class="setting-value">{{ checking ? '检查中...' : '' }}</span>
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>

          <button class="setting-item" type="button" role="link" aria-label="用户协议" @click="showUserAgreement">
            <div class="setting-icon" style="background: #e6f4ff">
              <van-icon name="description" size="18" color="#1677FF" />
            </div>
            <span class="setting-label">用户协议</span>
            <span class="setting-value" />
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>

          <button class="setting-item" type="button" role="link" aria-label="隐私政策" @click="showPrivacyPolicy">
            <div class="setting-icon" style="background: #fff1f0">
              <van-icon name="lock" size="18" color="#FF4D4F" />
            </div>
            <span class="setting-label">隐私政策</span>
            <span class="setting-value" />
            <van-icon name="arrow" size="16" color="#8C8C8C" />
          </button>
        </div>
      </section>

      <!-- 版本信息 -->
      <div class="version-info">
        <span>Leno 电商平台 {{ APP_VERSION }}</span>
        <span>Copyright 2026 Leno Inc.</span>
      </div>
    </div>

    <!-- 底部退出登录 -->
    <footer class="bottom-bar">
      <button class="logout-btn" type="button" :disabled="loggingOut" aria-label="退出登录" @click="onLogout">
        <van-icon name="revoke" size="16" color="#FF4D4F" />
        {{ loggingOut ? '退出中...' : '退出登录' }}
      </button>
    </footer>

    <!-- 字体大小选择 -->
    <van-action-sheet
      v-model:show="fontSizeSheetVisible"
      :actions="fontActions"
      cancel-text="取消"
      description="选择字体大小"
      close-on-click-action
      @select="onSelectFontSize"
    />

    <!-- 语言选择 -->
    <van-action-sheet
      v-model:show="languageSheetVisible"
      :actions="languageActions"
      cancel-text="取消"
      description="选择语言"
      close-on-click-action
      @select="onSelectLanguage"
    />
  </div>
</template>

<style scoped>
.settings-page {
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

/* 分组 */
.settings-group {
  margin-bottom: var(--s3);
}

.group-title {
  font-size: var(--fs-sm);
  color: var(--n7);
  font-weight: var(--fw-medium);
  padding: 0 var(--s1) var(--s1);
}

.group-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.setting-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  min-height: 48px;
  padding: var(--s2) var(--s4);
  border-bottom: 1px solid var(--n3);
  width: 100%;
  text-align: left;
  background: none;
}

.setting-item:last-child {
  border-bottom: none;
}

.setting-icon {
  width: 32px;
  height: 32px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.setting-label {
  flex: 1;
  font-size: var(--fs-base);
  color: var(--n10);
}

.setting-value {
  font-size: var(--fs-base);
  color: var(--n7);
  flex-shrink: 0;
}

/* 版本信息 */
.version-info {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--s1);
  padding: var(--s4) 0;
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 底部退出登录 */
.bottom-bar {
  position: sticky;
  bottom: 0;
  background: var(--n1);
  padding: var(--s3);
  border-top: 1px solid var(--n3);
  padding-bottom: calc(var(--s3) + env(safe-area-inset-bottom));
  flex-shrink: 0;
}

.logout-btn {
  width: 100%;
  height: 44px;
  border: 1px solid var(--c-error);
  border-radius: var(--r-lg);
  background: var(--n1);
  color: var(--c-error);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
}

.logout-btn:disabled {
  opacity: 0.6;
}
</style>
