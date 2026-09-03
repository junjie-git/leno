<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showSuccessToast } from 'vant'
import { authApi } from '../api/auth.api'
import { isValidPassword, isValidPhone } from '@/shared/utils/validators'
import { logger } from '@/shared/utils/logger'

/**
 * 忘记密码（两步：验证身份 → 重置密码）
 *
 * - 第 1 步：手机号/邮箱 + 短信验证码（演示验证码 123456）
 * - 第 2 步：新密码 + 确认新密码
 * - 重置成功后引导登录
 */
const router = useRouter()

const step = ref(0)
const account = ref('')
const verifyCode = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const submitting = ref(false)
const codeCountdown = ref(0)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const errors = ref<Record<string, string>>({})

const stepTitle = computed(() => (step.value === 0 ? '验证身份' : '重置密码'))

onUnmounted(() => {
  if (countdownTimer) {
    clearInterval(countdownTimer)
  }
})

/** 发送重置验证码 */
async function sendVerifyCode(): Promise<void> {
  if (codeCountdown.value > 0) return
  if (!account.value.trim()) {
    errors.value = { ...errors.value, account: '请输入注册手机号或邮箱' }
    return
  }
  try {
    await authApi.forgotPassword({ account: account.value.trim() })
    codeCountdown.value = 60
    countdownTimer = setInterval(() => {
      codeCountdown.value -= 1
      if (codeCountdown.value <= 0 && countdownTimer) {
        clearInterval(countdownTimer)
        countdownTimer = null
      }
    }, 1000)
    showSuccessToast('验证码已发送（演示验证码 123456）')
  } catch (e) {
    logger.warn('发送重置验证码失败', e)
    showFailToast(e instanceof Error ? e.message : '验证码发送失败')
  }
}

/** 第 1 步提交：校验手机号与验证码格式 */
function submitStepOne(): boolean {
  const next: Record<string, string> = {}
  const isPhone = isValidPhone(account.value.trim())
  const isEmail = account.value.includes('@') && /^[\w.+-]+@[\w-]+(\.[\w-]+)+$/.test(account.value.trim())
  if (!isPhone && !isEmail) {
    next.account = '请输入注册手机号或邮箱'
  }
  if (!/^\d{6}$/.test(verifyCode.value)) {
    next.verifyCode = '请输入 6 位验证码'
  }
  errors.value = next
  return Object.keys(next).length === 0
}

async function onSubmit(): Promise<void> {
  if (step.value === 0) {
    if (!submitStepOne()) return
    // 验证码格式正确即进入第 2 步（重置时服务端再校验）
    step.value = 1
    return
  }

  const next: Record<string, string> = {}
  if (!isValidPassword(newPassword.value)) {
    next.newPassword = '密码需 6-32 位，且包含字母和数字'
  }
  if (confirmPassword.value !== newPassword.value || !confirmPassword.value) {
    next.confirmPassword = '两次输入的密码不一致'
  }
  errors.value = next
  if (Object.keys(next).length > 0) return

  submitting.value = true
  try {
    await authApi.resetPassword({
      account: account.value.trim(),
      verifyCode: verifyCode.value,
      newPassword: newPassword.value,
    })
    showSuccessToast('密码重置成功，请使用新密码登录')
    router.replace('/login')
  } catch (e) {
    logger.warn('重置密码失败', e)
    showFailToast(e instanceof Error ? e.message : '重置失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="forgot-page">
    <van-nav-bar :title="`忘记密码 · ${stepTitle}`" left-arrow @click-left="step === 1 ? (step = 0) : router.back()" />

    <!-- 步骤条 -->
    <div class="steps">
      <div class="step-item" :class="step === 0 ? 'active' : 'done'">
        <div class="step-dot">
          <svg v-if="step > 0" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="20 6 9 17 4 12" />
          </svg>
          <template v-else>1</template>
        </div>
        <div class="step-label">验证身份</div>
      </div>
      <div class="step-line" :class="{ done: step > 0 }" />
      <div class="step-item" :class="{ active: step === 1 }">
        <div class="step-dot">2</div>
        <div class="step-label">重置密码</div>
      </div>
    </div>

    <div class="form-area">
      <!-- 第 1 步：验证身份 -->
      <div v-if="step === 0" class="field-group">
        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.account }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="8" r="4" />
              <path d="M4 20c0-4 4-6 8-6s8 2 8 6" />
            </svg>
            <input v-model="account" type="text" class="field-input" placeholder="注册手机号 / 邮箱" aria-label="账号" autocomplete="off">
          </div>
          <div v-if="errors.account" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.account }}</span>
          </div>
        </div>

        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.verifyCode }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="3" y="11" width="18" height="10" rx="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
            <input v-model="verifyCode" type="tel" class="field-input" placeholder="短信验证码" maxlength="6" aria-label="验证码" autocomplete="off">
            <button class="code-btn" type="button" :disabled="codeCountdown > 0" @click="sendVerifyCode">
              {{ codeCountdown > 0 ? `${codeCountdown}s 后重发` : '获取验证码' }}
            </button>
          </div>
          <div v-if="errors.verifyCode" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.verifyCode }}</span>
          </div>
        </div>
      </div>

      <!-- 第 2 步：重置密码 -->
      <div v-else class="field-group">
        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.newPassword }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="4" y="10" width="16" height="11" rx="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" />
            </svg>
            <input v-model="newPassword" type="password" class="field-input" placeholder="新密码（6-32 位）" aria-label="新密码" autocomplete="off">
          </div>
          <div v-if="errors.newPassword" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.newPassword }}</span>
          </div>
        </div>

        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.confirmPassword }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="4" y="10" width="16" height="11" rx="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" />
              <path d="m9 15 2 2 4-4" />
            </svg>
            <input v-model="confirmPassword" type="password" class="field-input" placeholder="确认新密码" aria-label="确认密码" autocomplete="off" @keydown.enter="onSubmit">
          </div>
          <div v-if="errors.confirmPassword" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.confirmPassword }}</span>
          </div>
        </div>
      </div>

      <button class="submit-btn" type="button" :disabled="submitting" @click="onSubmit">
        <span v-if="submitting" class="spinner" />
        <span>{{ submitting ? '提交中...' : step === 0 ? '下一步' : '重置密码' }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.forgot-page {
  min-height: 100vh;
  background: var(--n1);
  display: flex;
  flex-direction: column;
}

.steps {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px 24px 8px;
  gap: 8px;
}

.step-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.step-dot {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: var(--n3);
  color: var(--n7);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-sm);
  font-weight: var(--fw-semibold);
}

.step-item.active .step-dot {
  background: var(--c-primary);
  color: #fff;
  box-shadow: 0 0 0 4px rgba(22, 119, 255, 0.12);
}

.step-item.done .step-dot {
  background: var(--c-primary);
  color: #fff;
}

.step-label {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.step-item.active .step-label,
.step-item.done .step-label {
  color: var(--n10);
}

.step-line {
  width: 48px;
  height: 2px;
  background: var(--n5);
  border-radius: 1px;
}

.step-line.done {
  background: var(--c-primary);
}

.form-area {
  padding: 16px 24px 32px;
  flex: 1;
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.field-input-wrap {
  display: flex;
  align-items: center;
  height: 48px;
  background: var(--n2);
  border: 1px solid var(--n5);
  border-radius: var(--r-card);
  padding: 0 12px;
  transition:
    border-color 0.2s,
    background 0.2s;
}

.field-input-wrap:focus-within {
  border-color: var(--c-primary);
  background: var(--n1);
}

.field-input-wrap.error {
  border-color: var(--c-error);
  background: #fff1f0;
}

.field-icon {
  width: 20px;
  height: 20px;
  color: var(--n7);
  flex-shrink: 0;
  margin-right: 10px;
}

.field-input-wrap:focus-within .field-icon {
  color: var(--c-primary);
}

.field-input {
  flex: 1;
  border: none;
  outline: none;
  background: transparent;
  font-size: var(--fs-lg);
  color: var(--n10);
  height: 100%;
  font-family: inherit;
  min-width: 0;
}

.field-input::placeholder {
  color: var(--n7);
  font-size: var(--fs-base);
}

.code-btn {
  border: none;
  background: var(--n3);
  color: var(--n9);
  font-size: var(--fs-sm);
  font-family: inherit;
  padding: 6px 10px;
  border-radius: var(--r-base);
  white-space: nowrap;
  flex-shrink: 0;
  margin-left: 8px;
  font-weight: var(--fw-medium);
}

.code-btn:disabled {
  color: var(--n7);
  cursor: not-allowed;
}

.field-error {
  margin-top: 6px;
  padding-left: 4px;
  font-size: var(--fs-sm);
  color: var(--c-error);
  display: flex;
  align-items: center;
  gap: 4px;
}

.field-error svg {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.submit-btn {
  width: 100%;
  height: 48px;
  border: none;
  border-radius: var(--r-card);
  background: var(--c-primary);
  color: #fff;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  margin-top: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  box-shadow: 0 4px 12px rgba(22, 119, 255, 0.3);
}

.submit-btn:active {
  opacity: 0.85;
}

.submit-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.spinner {
  width: 18px;
  height: 18px;
  border: 2px solid rgba(255, 255, 255, 0.4);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
