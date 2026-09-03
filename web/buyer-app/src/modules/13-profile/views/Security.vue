<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { profileApi } from '@/modules/13-profile/api/profile.api'
import { authApi } from '@/modules/01-auth/api/auth.api'
import type { BuyerProfileDto } from '../types/profile.dto'
import { useAuthStore } from '@/shared/auth'
import ErrorState from '@/shared/components/ErrorState.vue'
import { maskPhone } from '@/shared/utils/format'
import { isValidPassword, isValidVerifyCode } from '@/shared/utils/validators'
import { logger } from '@/shared/utils/logger'

/**
 * 账号安全页（/profile/security）
 *
 * 结构（对齐设计稿 security）：
 * NavBar（返回 + 账号安全）→ 账号保护级别卡（渐变 + 完成度 + 提升建议）
 * → 风险提示条（双因子未启用 / 邮箱未绑定时展示）
 * → 安全项列表（登录密码 / 双因子认证 / 绑定手机 / 绑定邮箱）
 *
 * 交互：
 * - 登录密码：弹层修改（旧密码 + 新密码 + 确认新密码 + 强度提示），成功后强制重新登录
 * - 双因子认证：未启用 → 弹层展示 TOTP 密钥与 otpauth 内容 + 验证码确认启用；
 *   已启用 → 弹层输入当前密码确认禁用
 * - 绑定手机 / 邮箱：脱敏展示绑定状态
 */
const router = useRouter()
const authStore = useAuthStore()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const profile = ref<BuyerProfileDto | null>(null)

// ---- 密码修改弹层 ----
const passwordVisible = ref(false)
const passwordSubmitting = ref(false)
const passwordForm = ref({ oldPassword: '', newPassword: '', confirmPassword: '' })
const oldPasswordError = ref('')
const newPasswordError = ref('')
const confirmPasswordError = ref('')

// ---- 双因子启用弹层 ----
const twoFactorVisible = ref(false)
const twoFactorLoading = ref(false)
const twoFactorSubmitting = ref(false)
/** enable 接口返回的密钥与 otpauth 内容 */
const twoFactorSecret = ref('')
const twoFactorQrUri = ref('')
const twoFactorCode = ref('')
const twoFactorError = ref('')

// ---- 双因子禁用弹层 ----
const disableVisible = ref(false)
const disableSubmitting = ref(false)
const disablePassword = ref('')
const disableError = ref('')

/** 邮箱脱敏：zhangxiaoya@example.com → z***@example.com */
function maskEmail(email: string): string {
  const at = email.indexOf('@')
  if (at <= 0) return email
  const prefix = email.slice(0, Math.min(1, at))
  return `${prefix}***${email.slice(at)}`
}

/** 安全项完成情况（4 项） */
const securityChecks = computed(() => [
  { key: 'password', label: '登录密码', done: true },
  { key: 'twoFactor', label: '双因子认证', done: profile.value?.twoFactorEnabled ?? false },
  { key: 'phone', label: '绑定手机', done: !!profile.value?.phone },
  { key: 'email', label: '绑定邮箱', done: !!profile.value?.email },
])

/** 已完成项数 */
const doneCount = computed(() => securityChecks.value.filter((c) => c.done).length)

/** 完成度百分比（0-100） */
const donePercent = computed(() => Math.round((doneCount.value / securityChecks.value.length) * 100))

/** 保护级别：低 / 中 / 高 */
const protectionLevel = computed(() => {
  if (donePercent.value >= 100) return '高'
  if (donePercent.value >= 50) return '中'
  return '低'
})

/** 级别配色（低红 / 中黄 / 高绿） */
const levelColor = computed(() => {
  if (protectionLevel.value === '高') return '#52C41A'
  if (protectionLevel.value === '中') return '#FAAD14'
  return '#FF4D4F'
})

/** 提升建议（按缺失项优先级给出第一条） */
const levelSuggestion = computed(() => {
  if (donePercent.value >= 100) return '账号防护已全部开启，请保持良好的安全习惯'
  if (!profile.value?.twoFactorEnabled) return '建议启用双因子认证，防止账号被盗'
  if (!profile.value?.email) return '建议绑定邮箱，用于找回密码与接收安全通知'
  if (!profile.value?.phone) return '建议绑定手机号，提升账号可找回性'
  return '建议开启更多安全设置，提升账号安全性'
})

/** 是否展示风险提示 */
const hasRisk = computed(
  () => donePercent.value < 100 && (!profile.value || !profile.value.twoFactorEnabled || !profile.value.email),
)

/** 新密码强度（弱 / 中 / 强） */
const passwordStrength = computed(() => {
  const pwd = passwordForm.value.newPassword
  if (!pwd) return ''
  let score = 0
  if (pwd.length >= 8) score += 1
  if (/[a-z]/.test(pwd) && /[A-Z]/.test(pwd)) score += 1
  if (/\d/.test(pwd)) score += 1
  if (/[^A-Za-z0-9]/.test(pwd)) score += 1
  if (score <= 2) return '弱'
  if (score === 3) return '中'
  return '强'
})

const strengthColor = computed(() => {
  if (passwordStrength.value === '强') return '#52C41A'
  if (passwordStrength.value === '中') return '#FAAD14'
  return '#FF4D4F'
})

onMounted(() => {
  void loadProfile()
})

/** 加载安全信息（每次进入重新拉取） */
async function loadProfile(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    profile.value = await profileApi.getProfile()
  } catch (e) {
    logger.error('账号安全信息加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

// ---- 修改密码 ----
function openPasswordPopup(): void {
  passwordForm.value = { oldPassword: '', newPassword: '', confirmPassword: '' }
  oldPasswordError.value = ''
  newPasswordError.value = ''
  confirmPasswordError.value = ''
  passwordVisible.value = true
}

/** 密码表单校验 */
function validatePasswordForm(): boolean {
  oldPasswordError.value = ''
  newPasswordError.value = ''
  confirmPasswordError.value = ''
  if (!passwordForm.value.oldPassword) {
    oldPasswordError.value = '请输入旧密码'
  }
  if (!isValidPassword(passwordForm.value.newPassword)) {
    newPasswordError.value = '新密码需 6-32 位，且包含字母和数字'
  }
  if (passwordForm.value.confirmPassword !== passwordForm.value.newPassword) {
    confirmPasswordError.value = '两次输入的密码不一致'
  }
  return !oldPasswordError.value && !newPasswordError.value && !confirmPasswordError.value
}

async function onSubmitPassword(): Promise<void> {
  if (!validatePasswordForm() || passwordSubmitting.value) return
  passwordSubmitting.value = true
  try {
    await profileApi.changePassword({
      oldPassword: passwordForm.value.oldPassword,
      newPassword: passwordForm.value.newPassword,
    })
    passwordVisible.value = false
    showToast('密码修改成功，请重新登录')
    await authStore.logout()
    router.replace('/login')
  } catch (e) {
    logger.warn('修改密码失败', e)
    if (e instanceof Error && e.message.includes('原密码')) {
      oldPasswordError.value = e.message
    }
    showFailToast(e instanceof Error ? e.message : '修改失败，请稍后重试')
  } finally {
    passwordSubmitting.value = false
  }
}

// ---- 启用双因子 ----
async function openTwoFactorPopup(): Promise<void> {
  twoFactorCode.value = ''
  twoFactorError.value = ''
  twoFactorSecret.value = ''
  twoFactorQrUri.value = ''
  twoFactorVisible.value = true
  twoFactorLoading.value = true
  try {
    const result = await authApi.enableTwoFactor()
    twoFactorSecret.value = result.secret
    twoFactorQrUri.value = result.qrCodeUri
  } catch (e) {
    logger.error('开启双因子失败', e)
    twoFactorVisible.value = false
    showFailToast(e instanceof Error ? e.message : '开启失败，请稍后重试')
  } finally {
    twoFactorLoading.value = false
  }
}

/** 复制密钥 / otpauth 内容 */
async function copyText(text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text)
    showToast('已复制')
  } catch {
    showFailToast('复制失败，请手动复制')
  }
}

async function onConfirmTwoFactor(): Promise<void> {
  twoFactorError.value = ''
  if (!isValidVerifyCode(twoFactorCode.value)) {
    twoFactorError.value = '请输入 6 位数字验证码'
    return
  }
  if (twoFactorSubmitting.value) return
  twoFactorSubmitting.value = true
  try {
    await authApi.confirmTwoFactor({ code: twoFactorCode.value })
    twoFactorVisible.value = false
    showToast('双因子已启用')
    await loadProfile()
  } catch (e) {
    logger.warn('确认双因子失败', e)
    twoFactorError.value = e instanceof Error ? e.message : '验证码错误或已过期'
    twoFactorCode.value = ''
  } finally {
    twoFactorSubmitting.value = false
  }
}

// ---- 禁用双因子 ----
function openDisablePopup(): void {
  disablePassword.value = ''
  disableError.value = ''
  disableVisible.value = true
}

async function onConfirmDisable(): Promise<void> {
  disableError.value = ''
  if (!disablePassword.value) {
    disableError.value = '请输入当前登录密码'
    return
  }
  if (disableSubmitting.value) return
  disableSubmitting.value = true
  try {
    await authApi.disableTwoFactor({ password: disablePassword.value })
    disableVisible.value = false
    showToast('双因子已禁用')
    await loadProfile()
  } catch (e) {
    logger.warn('禁用双因子失败', e)
    disableError.value = e instanceof Error ? e.message : '禁用失败，请稍后重试'
  } finally {
    disableSubmitting.value = false
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
  <div class="security-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">账号安全</div>
    </header>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <template v-if="loading">
        <div class="sk-level">
          <div class="skeleton-block sk-level-line1" />
          <div class="skeleton-block sk-level-line2" />
          <div class="skeleton-block sk-level-line3" />
        </div>
        <div class="sk-section">
          <div v-for="i in 4" :key="i" class="sk-item">
            <div class="skeleton-block sk-item-icon" />
            <div class="sk-item-text">
              <div class="skeleton-block sk-item-title" />
              <div class="skeleton-block sk-item-desc" />
            </div>
          </div>
        </div>
      </template>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="安全信息加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadProfile"
      />

      <!-- 内容区 -->
      <template v-else>
        <!-- 账号保护级别卡 -->
        <div class="security-level-card">
          <div class="level-header">
            <span class="level-title">账号保护级别</span>
            <span class="level-value" :style="{ color: levelColor }">{{ protectionLevel }}</span>
          </div>
          <div class="level-progress-wrap">
            <div class="level-progress-bar" aria-hidden="true">
              <div class="level-progress-fill" :style="{ width: `${donePercent}%` }" />
            </div>
            <div class="level-progress-label">
              <span>已完成 {{ doneCount }}/{{ securityChecks.length }} 项安全设置</span>
              <span>{{ donePercent }}%</span>
            </div>
          </div>
          <div class="level-suggestion">
            <van-icon name="info-o" size="14" />
            {{ levelSuggestion }}
          </div>
        </div>

        <!-- 风险提示 -->
        <div v-if="hasRisk" class="risk-alert" role="alert">
          <van-icon name="warning-o" size="20" color="#FAAD14" />
          <div class="risk-content">
            <div class="risk-title">检测到账号存在安全风险</div>
            <div class="risk-desc">{{ levelSuggestion }}</div>
          </div>
        </div>

        <!-- 安全项列表 -->
        <section class="section">
          <div class="section-title-bar">安全设置</div>

          <!-- 登录密码 -->
          <button class="security-item" type="button" role="link" aria-label="修改登录密码" @click="openPasswordPopup">
            <div class="item-icon" style="background: #e6f4ff">
              <van-icon name="lock" size="18" color="#1677FF" />
            </div>
            <div class="item-content">
              <div class="item-title">登录密码</div>
              <div class="item-desc">建议定期更换密码以保障安全</div>
            </div>
            <div class="item-status">
              <span class="status-tag set">已设置</span>
              <van-icon name="arrow" size="16" color="#8C8C8C" />
            </div>
          </button>

          <!-- 双因子认证 -->
          <button
            v-if="!profile?.twoFactorEnabled"
            class="security-item"
            type="button"
            role="link"
            aria-label="启用双因子认证"
            @click="openTwoFactorPopup"
          >
            <div class="item-icon" style="background: #f9f0ff">
              <van-icon name="shield-o" size="18" color="#722ED1" />
            </div>
            <div class="item-content">
              <div class="item-title">双因子认证</div>
              <div class="item-desc">登录时需输入动态验证码，防盗号</div>
            </div>
            <div class="item-status">
              <span class="status-tag unset">未启用</span>
              <van-icon name="arrow" size="16" color="#8C8C8C" />
            </div>
          </button>
          <button
            v-else
            class="security-item"
            type="button"
            role="link"
            aria-label="禁用双因子认证"
            @click="openDisablePopup"
          >
            <div class="item-icon" style="background: #f9f0ff">
              <van-icon name="shield-o" size="18" color="#722ED1" />
            </div>
            <div class="item-content">
              <div class="item-title">双因子认证</div>
              <div class="item-desc">登录时需输入动态验证码，防盗号</div>
            </div>
            <div class="item-status">
              <span class="status-tag set">已启用</span>
              <van-icon name="arrow" size="16" color="#8C8C8C" />
            </div>
          </button>

          <!-- 绑定手机 -->
          <div class="security-item" role="listitem" aria-label="绑定手机">
            <div class="item-icon" style="background: #f6ffed">
              <van-icon name="phone-o" size="18" color="#52C41A" />
            </div>
            <div class="item-content">
              <div class="item-title">绑定手机</div>
              <div class="item-desc">用于登录、找回密码与安全验证</div>
            </div>
            <div class="item-status">
              <span v-if="profile?.phone" class="status-tag set">{{ maskPhone(profile.phone) }}</span>
              <span v-else class="status-tag unbound">未绑定</span>
            </div>
          </div>

          <!-- 绑定邮箱 -->
          <div class="security-item" role="listitem" aria-label="绑定邮箱">
            <div class="item-icon" style="background: #fff7e6">
              <van-icon name="envelop-o" size="18" color="#FAAD14" />
            </div>
            <div class="item-content">
              <div class="item-title">绑定邮箱</div>
              <div class="item-desc">用于接收安全通知与账单提醒</div>
            </div>
            <div class="item-status">
              <span v-if="profile?.email" class="status-tag set">{{ maskEmail(profile.email) }}</span>
              <span v-else class="status-tag unbound">未绑定</span>
            </div>
          </div>
        </section>
      </template>
    </div>

    <!-- 修改密码弹层 -->
    <van-popup
      v-model:show="passwordVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="修改登录密码"
    >
      <div class="form-panel">
        <div class="form-head">
          <span class="t">修改登录密码</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="passwordVisible = false" />
        </div>
        <div class="form-body">
          <div class="field">
            <div class="field-label">旧密码</div>
            <input
              v-model="passwordForm.oldPassword"
              class="field-input"
              type="password"
              placeholder="请输入当前登录密码"
              aria-label="旧密码"
              autocomplete="off"
              @blur="oldPasswordError = ''"
            >
            <div v-if="oldPasswordError" class="field-error">{{ oldPasswordError }}</div>
          </div>
          <div class="field">
            <div class="field-label">新密码</div>
            <input
              v-model="passwordForm.newPassword"
              class="field-input"
              type="password"
              placeholder="6-32 位，须包含字母和数字"
              aria-label="新密码"
              autocomplete="off"
              maxlength="32"
            >
            <div v-if="passwordStrength" class="strength-row">
              <span class="strength-bar" :class="{ fill: passwordStrength !== '弱' }" :style="{ background: passwordStrength !== '弱' ? strengthColor : undefined }" />
              <span class="strength-text" :style="{ color: strengthColor }">强度：{{ passwordStrength }}</span>
            </div>
            <div v-if="newPasswordError" class="field-error">{{ newPasswordError }}</div>
          </div>
          <div class="field">
            <div class="field-label">确认新密码</div>
            <input
              v-model="passwordForm.confirmPassword"
              class="field-input"
              type="password"
              placeholder="请再次输入新密码"
              aria-label="确认新密码"
              autocomplete="off"
              maxlength="32"
            >
            <div v-if="confirmPasswordError" class="field-error">{{ confirmPasswordError }}</div>
          </div>
        </div>
        <div class="form-foot">
          <van-button plain type="primary" class="foot-btn" @click="passwordVisible = false">取消</van-button>
          <van-button type="primary" class="foot-btn" :loading="passwordSubmitting" @click="onSubmitPassword">
            确认修改
          </van-button>
        </div>
      </div>
    </van-popup>

    <!-- 启用双因子弹层 -->
    <van-popup
      v-model:show="twoFactorVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="启用双因子认证"
    >
      <div class="form-panel">
        <div class="form-head">
          <span class="t">启用双因子认证</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="twoFactorVisible = false" />
        </div>

        <div class="form-body">
          <template v-if="twoFactorLoading">
            <div class="tf-loading">
              <div class="skeleton-block tf-sk-line" />
              <div class="skeleton-block tf-sk-line short" />
              <div class="skeleton-block tf-sk-code" />
            </div>
          </template>
          <template v-else>
            <div class="tf-step">1. 使用验证器 App（如 Google Authenticator）扫描或手动录入以下密钥</div>
            <div class="tf-key-row">
              <code class="tf-key">{{ twoFactorSecret }}</code>
              <button class="tf-copy" type="button" aria-label="复制密钥" @click="copyText(twoFactorSecret)">
                <van-icon name="description" size="16" color="#1677FF" />
              </button>
            </div>
            <div class="tf-step">2. 验证器将生成 6 位动态验证码，在下方输入完成绑定</div>
            <div class="field">
              <div class="field-label">动态验证码</div>
              <input
                v-model="twoFactorCode"
                class="field-input tf-code-input"
                type="text"
                inputmode="numeric"
                placeholder="6 位数字"
                aria-label="动态验证码"
                maxlength="6"
              >
              <div v-if="twoFactorError" class="field-error">{{ twoFactorError }}</div>
            </div>
            <button class="tf-uri" type="button" aria-label="复制二维码内容" @click="copyText(twoFactorQrUri)">
              <span class="tf-uri-label">二维码内容（otpauth）</span>
              <span class="tf-uri-text">{{ twoFactorQrUri }}</span>
            </button>
          </template>
        </div>

        <div class="form-foot">
          <van-button plain type="primary" class="foot-btn" @click="twoFactorVisible = false">取消</van-button>
          <van-button
            type="primary"
            class="foot-btn"
            :loading="twoFactorSubmitting"
            :disabled="twoFactorLoading"
            @click="onConfirmTwoFactor"
          >
            确认启用
          </van-button>
        </div>
      </div>
    </van-popup>

    <!-- 禁用双因子弹层 -->
    <van-popup
      v-model:show="disableVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="禁用双因子认证"
    >
      <div class="form-panel">
        <div class="form-head">
          <span class="t">禁用双因子认证</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="disableVisible = false" />
        </div>
        <div class="form-body">
          <div class="disable-tip">
            <van-icon name="warning-o" size="16" color="#FF4D4F" />
            禁用后账号安全性将降低，建议保持启用。此操作可逆。
          </div>
          <div class="field">
            <div class="field-label">当前登录密码</div>
            <input
              v-model="disablePassword"
              class="field-input"
              type="password"
              placeholder="请输入当前登录密码以确认"
              aria-label="当前登录密码"
              autocomplete="off"
            >
            <div v-if="disableError" class="field-error">{{ disableError }}</div>
          </div>
        </div>
        <div class="form-foot">
          <van-button plain type="primary" class="foot-btn" @click="disableVisible = false">取消</van-button>
          <van-button type="danger" class="foot-btn" :loading="disableSubmitting" @click="onConfirmDisable">
            确认禁用
          </van-button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.security-page {
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

/* 保护级别卡 */
.security-level-card {
  background: linear-gradient(135deg, #1677ff 0%, #0958d9 100%);
  border-radius: var(--r-lg);
  padding: var(--s4);
  color: #fff;
  position: relative;
  overflow: hidden;
  margin-bottom: var(--s3);
}

.security-level-card::before {
  content: "";
  position: absolute;
  top: -20px;
  right: -20px;
  width: 120px;
  height: 120px;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.1) 0%, transparent 70%);
  border-radius: 50%;
}

.level-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s3);
  position: relative;
}

.level-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
}

.level-value {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  text-shadow: 0 1px 4px rgba(0, 0, 0, 0.2);
}

.level-progress-bar {
  height: 6px;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: var(--s2);
}

.level-progress-fill {
  height: 100%;
  background: #fff;
  border-radius: 3px;
  transition: width 0.3s var(--ease-std);
}

.level-progress-label {
  font-size: var(--fs-sm);
  color: rgba(255, 255, 255, 0.8);
  display: flex;
  justify-content: space-between;
}

.level-suggestion {
  margin-top: var(--s3);
  padding: var(--s2) var(--s3);
  background: rgba(255, 255, 255, 0.15);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: rgba(255, 255, 255, 0.9);
  display: flex;
  align-items: center;
  gap: var(--s1);
}

/* 风险提示 */
.risk-alert {
  background: #fff7e6;
  border-radius: var(--r-lg);
  padding: var(--s3);
  display: flex;
  align-items: flex-start;
  gap: var(--s2);
  border: 1px solid #ffe58f;
  margin-bottom: var(--s3);
}

.risk-content {
  flex: 1;
}

.risk-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  margin-bottom: 2px;
}

.risk-desc {
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.5;
}

/* 安全项分组 */
.section {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.section-title-bar {
  padding: var(--s3) var(--s4) var(--s1);
  font-size: var(--fs-sm);
  color: var(--n7);
  font-weight: var(--fw-medium);
}

.security-item {
  display: flex;
  align-items: center;
  padding: var(--s3) var(--s4);
  gap: var(--s3);
  border-bottom: 1px solid var(--n3);
  min-height: 56px;
  width: 100%;
  text-align: left;
  background: none;
}

.security-item:last-child {
  border-bottom: none;
}

.item-icon {
  width: 32px;
  height: 32px;
  border-radius: var(--r-card);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.item-content {
  flex: 1;
  min-width: 0;
}

.item-title {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
}

.item-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.item-status {
  display: flex;
  align-items: center;
  gap: var(--s1);
  flex-shrink: 0;
}

.status-tag {
  font-size: var(--fs-sm);
  padding: 2px var(--s2);
  border-radius: var(--r-base);
}

.status-tag.set {
  color: var(--c-success);
  background: #f6ffed;
}

.status-tag.unset {
  color: var(--n7);
  background: var(--n3);
}

.status-tag.unbound {
  color: var(--c-error);
  background: #fff1f0;
}

/* 骨架屏 */
.sk-level {
  background: linear-gradient(135deg, #1677ff 0%, #0958d9 100%);
  border-radius: var(--r-lg);
  padding: var(--s4);
  margin-bottom: var(--s3);
}

.sk-level-line1 {
  width: 120px;
  height: 18px;
  margin-bottom: var(--s3);
}

.sk-level-line2 {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  margin-bottom: var(--s2);
}

.sk-level-line3 {
  width: 60%;
  height: 12px;
}

.sk-section {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3) var(--s4);
}

.sk-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s2) 0;
}

.sk-item-icon {
  width: 32px;
  height: 32px;
  border-radius: var(--r-card);
}

.sk-item-text {
  flex: 1;
}

.sk-item-title {
  width: 90px;
  height: 14px;
  margin-bottom: var(--s1);
}

.sk-item-desc {
  width: 140px;
  height: 12px;
}

/* 弹层表单 */
.form-panel {
  display: flex;
  flex-direction: column;
  max-height: 80vh;
  padding-bottom: env(safe-area-inset-bottom);
}

.form-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s4) var(--s4) var(--s2);
}

.form-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.form-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 var(--s4);
}

.field {
  margin-bottom: var(--s3);
}

.field-label {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-bottom: var(--s1);
}

.field-input {
  width: 100%;
  height: 40px;
  border: 1px solid var(--n5);
  border-radius: var(--r-base);
  padding: 0 var(--s2);
  font-size: var(--fs-base);
  color: var(--n10);
  font-family: inherit;
  outline: none;
  background: var(--n1);
}

.field-input:focus {
  border-color: var(--c-primary);
}

.field-error {
  margin-top: var(--s1);
  font-size: var(--fs-sm);
  color: var(--c-error);
}

.strength-row {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-top: var(--s1);
}

.strength-bar {
  width: 48px;
  height: 4px;
  border-radius: 2px;
  background: var(--n5);
}

.strength-bar.fill {
  background: var(--c-success);
}

.strength-text {
  font-size: var(--fs-sm);
}

.form-foot {
  display: flex;
  gap: var(--s2);
  padding: var(--s3) var(--s4) var(--s4);
}

.foot-btn {
  flex: 1;
}

/* 双因子弹层 */
.tf-step {
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
  margin-bottom: var(--s2);
}

.tf-key-row {
  display: flex;
  align-items: center;
  gap: var(--s2);
  background: var(--n2);
  border: 1px solid var(--n3);
  border-radius: var(--r-base);
  padding: var(--s2) var(--s3);
  margin-bottom: var(--s3);
}

.tf-key {
  flex: 1;
  font-family: var(--ff-mono);
  font-size: var(--fs-base);
  color: var(--c-primary);
  letter-spacing: 1px;
  word-break: break-all;
}

.tf-copy {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  background: none;
  border: none;
  padding: var(--s1);
}

.tf-code-input {
  font-family: var(--ff-mono);
  letter-spacing: 6px;
  text-align: center;
}

.tf-uri {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  width: 100%;
  background: var(--n2);
  border: 1px solid var(--n3);
  border-radius: var(--r-base);
  padding: var(--s2) var(--s3);
  margin-bottom: var(--s2);
}

.tf-uri-label {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.tf-uri-text {
  font-size: var(--fs-sm);
  color: var(--n9);
  word-break: break-all;
  line-height: 1.5;
  text-align: left;
}

.tf-loading {
  padding: var(--s2) 0 var(--s3);
}

.tf-sk-line {
  width: 100%;
  height: 14px;
  margin-bottom: var(--s2);
}

.tf-sk-line.short {
  width: 60%;
}

.tf-sk-code {
  width: 100%;
  height: 40px;
}

/* 禁用双因子 */
.disable-tip {
  display: flex;
  align-items: flex-start;
  gap: var(--s1);
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
  background: #fff1f0;
  border-radius: var(--r-base);
  padding: var(--s2) var(--s3);
  margin-bottom: var(--s3);
}
</style>
