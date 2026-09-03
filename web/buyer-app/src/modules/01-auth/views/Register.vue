<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showSuccessToast } from 'vant'
import { authApi } from '../api/auth.api'
import { isValidPassword, isValidPhone, isValidUsername } from '@/shared/utils/validators'
import { logger } from '@/shared/utils/logger'

/**
 * 注册页
 *
 * - 用户名（4-20 位）+ 手机号 + 短信验证码 + 密码 + 确认密码
 * - 协议勾选后允许提交
 * - 演示验证码：123456
 */
const router = useRouter()

const username = ref('')
const phone = ref('')
const verifyCode = ref('')
const password = ref('')
const confirmPassword = ref('')
const agreed = ref(false)
const submitting = ref(false)

const errors = ref<Record<string, string>>({})
const codeCountdown = ref(0)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const registerDisabled = computed(() => !agreed.value || submitting.value)

onUnmounted(() => {
  if (countdownTimer) {
    clearInterval(countdownTimer)
  }
})

/** 发送短信验证码（演示：不实际发送，直接进入 60s 倒计时） */
function sendVerifyCode(): void {
  if (codeCountdown.value > 0) return
  if (!isValidPhone(phone.value)) {
    errors.value = { ...errors.value, phone: '请输入正确的 11 位手机号' }
    return
  }
  codeCountdown.value = 60
  countdownTimer = setInterval(() => {
    codeCountdown.value -= 1
    if (codeCountdown.value <= 0 && countdownTimer) {
      clearInterval(countdownTimer)
      countdownTimer = null
    }
  }, 1000)
  showSuccessToast('验证码已发送（演示验证码 123456）')
}

function validate(): boolean {
  const next: Record<string, string> = {}
  if (!isValidUsername(username.value.trim()) || username.value.trim().length < 4 || username.value.trim().length > 20) {
    next.username = '用户名长度需 4-20 位字符'
  }
  if (!isValidPhone(phone.value)) {
    next.phone = '请输入正确的 11 位手机号'
  }
  if (!/^\d{6}$/.test(verifyCode.value)) {
    next.verifyCode = '请输入 6 位验证码'
  }
  if (!isValidPassword(password.value)) {
    next.password = '密码需 6-32 位，且包含字母和数字'
  }
  if (confirmPassword.value !== password.value || !confirmPassword.value) {
    next.confirmPassword = '两次输入的密码不一致'
  }
  errors.value = next
  return Object.keys(next).length === 0
}

async function onSubmit(): Promise<void> {
  if (!agreed.value) {
    showFailToast('请先阅读并同意用户协议')
    return
  }
  if (!validate()) return

  submitting.value = true
  try {
    await authApi.register({
      username: username.value.trim(),
      nickname: username.value.trim(),
      phone: phone.value,
      password: password.value,
      verifyCode: verifyCode.value,
    })
    showSuccessToast('注册成功，请登录')
    router.replace('/login')
  } catch (e) {
    logger.warn('注册失败', e)
    showFailToast(e instanceof Error ? e.message : '注册失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}

function goLogin(): void {
  router.push('/login')
}
</script>

<template>
  <div class="register-page">
    <van-nav-bar title="创建账号" left-arrow @click-left="router.back()" />

    <div class="form-area">
      <div class="field-group">
        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.username }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="8" r="4" />
              <path d="M4 20c0-4 4-6 8-6s8 2 8 6" />
            </svg>
            <input v-model="username" type="text" class="field-input" placeholder="用户名（4-20 位字符）" aria-label="用户名" autocomplete="off">
          </div>
          <div v-if="errors.username" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.username }}</span>
          </div>
        </div>

        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.phone }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="6" y="2" width="12" height="20" rx="2" />
              <path d="M10 18h4" />
            </svg>
            <input v-model="phone" type="tel" class="field-input" placeholder="手机号" maxlength="11" aria-label="手机号" autocomplete="off">
          </div>
          <div v-if="errors.phone" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.phone }}</span>
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

        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.password }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="4" y="10" width="16" height="11" rx="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" />
            </svg>
            <input v-model="password" type="password" class="field-input" placeholder="密码（6-32 位）" aria-label="密码" autocomplete="off">
          </div>
          <div v-if="errors.password" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.password }}</span>
          </div>
        </div>

        <div class="field">
          <div class="field-input-wrap" :class="{ error: errors.confirmPassword }">
            <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="4" y="10" width="16" height="11" rx="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" />
              <path d="m9 15 2 2 4-4" />
            </svg>
            <input v-model="confirmPassword" type="password" class="field-input" placeholder="确认密码" aria-label="确认密码" autocomplete="off" @keydown.enter="onSubmit">
          </div>
          <div v-if="errors.confirmPassword" class="field-error">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" /></svg>
            <span>{{ errors.confirmPassword }}</span>
          </div>
        </div>
      </div>

      <!-- 协议 -->
      <div class="agreement">
        <van-checkbox v-model="agreed" shape="square" icon-size="14px" />
        <div class="agreement-text">
          我已阅读并同意 <a href="javascript:void(0)">《用户协议》</a> 与 <a href="javascript:void(0)">《隐私政策》</a>
        </div>
      </div>

      <button class="register-btn" type="button" :disabled="registerDisabled" @click="onSubmit">
        <span v-if="submitting" class="spinner" />
        <span>{{ submitting ? '注册中...' : '注册' }}</span>
      </button>

      <div class="login-tip">
        已有账号？<a @click.prevent="goLogin">立即登录</a>
      </div>
    </div>
  </div>
</template>

<style scoped>
.register-page {
  min-height: 100vh;
  background: var(--n1);
  display: flex;
  flex-direction: column;
}

.form-area {
  padding: 24px 24px 32px;
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
  background: var(--n3);
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

.agreement {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 20px;
  padding: 0 4px;
}

.agreement-text {
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
}

.agreement-text a {
  color: var(--c-primary);
}

.register-btn {
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

.register-btn:active {
  opacity: 0.85;
}

.register-btn:disabled {
  background: var(--n5);
  color: var(--n7);
  box-shadow: none;
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

.login-tip {
  text-align: center;
  margin-top: 20px;
  font-size: var(--fs-base);
  color: var(--n7);
}

.login-tip a {
  color: var(--c-primary);
}
</style>
