<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast } from 'vant'
import { useAuthStore } from '@/shared/auth'
import { isValidAccount, isValidPassword } from '@/shared/utils/validators'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import { logger } from '@/shared/utils/logger'

/**
 * 登录页
 *
 * - 账号密码登录（可能触发 2FA 二段验证）
 * - 三方登录入口（微信 / 支付宝）
 * - 演示账号：zhangxiaoya / Zhang123456；2FA 演示账号：demo2fa（验证码 123456）
 */
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const cartStore = useCartStore()

const account = ref('')
const password = ref('')
const passwordVisible = ref(false)
const submitting = ref(false)

const accountErrorMessage = ref('')
const passwordErrorMessage = ref('')

/** 登录后跳转：redirect query 优先 */
function redirectAfterLogin(): void {
  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
  router.replace(redirect)
}

async function onSubmit(): Promise<void> {
  accountErrorMessage.value = ''
  passwordErrorMessage.value = ''

  if (!account.value.trim()) {
    accountErrorMessage.value = '请输入用户名'
    return
  }
  if (!isValidAccount(account.value.trim())) {
    accountErrorMessage.value = '账号格式不正确'
    return
  }
  if (!password.value || !isValidPassword(password.value)) {
    passwordErrorMessage.value = '密码长度需 6-32 位，且包含字母和数字'
    return
  }

  submitting.value = true
  try {
    const result = await authStore.login({
      account: account.value.trim(),
      password: password.value,
    })
    if (result.requiresTwoFactor && result.twoFactorTicket) {
      router.push({
        path: '/two-factor',
        query: { ticket: result.twoFactorTicket, redirect: route.query.redirect },
      })
      return
    }
    await cartStore.refreshBadge()
    redirectAfterLogin()
  } catch (e) {
    logger.warn('登录失败', e)
    showFailToast(e instanceof Error ? e.message : '登录失败，请稍后重试')
    password.value = ''
  } finally {
    submitting.value = false
  }
}

function goRegister(): void {
  router.push('/register')
}

function goForgotPassword(): void {
  router.push('/forgot-password')
}

function goOauth(provider: 'wechat' | 'alipay'): void {
  router.push(`/oauth/${provider}`)
}
</script>

<template>
  <div class="login-page">
    <!-- 品牌区 -->
    <div class="brand">
      <div class="logo">L</div>
      <div class="brand-name">Leno</div>
      <div class="brand-slogan">品质生活 · 一触即达</div>
    </div>

    <!-- 表单区 -->
    <div class="form-area">
      <van-form @submit="onSubmit">
        <div class="field-group">
          <div class="field">
            <div class="field-input-wrap" :class="{ error: accountErrorMessage }">
              <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="8" r="4" />
                <path d="M4 20c0-4 4-6 8-6s8 2 8 6" />
              </svg>
              <input
                v-model="account"
                type="text"
                class="field-input"
                placeholder="用户名 / 手机号 / 邮箱"
                aria-label="用户名"
                autocomplete="off"
              >
            </div>
            <div v-if="accountErrorMessage" class="field-error">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" />
              </svg>
              <span>{{ accountErrorMessage }}</span>
            </div>
          </div>

          <div class="field">
            <div class="field-input-wrap" :class="{ error: passwordErrorMessage }">
              <svg class="field-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <rect x="4" y="10" width="16" height="11" rx="2" />
                <path d="M8 10V7a4 4 0 0 1 8 0v3" />
              </svg>
              <input
                v-model="password"
                :type="passwordVisible ? 'text' : 'password'"
                class="field-input"
                placeholder="请输入密码"
                aria-label="密码"
                autocomplete="off"
                @keydown.enter="onSubmit"
              >
              <button
                type="button"
                class="field-action"
                aria-label="切换密码可见"
                @click="passwordVisible = !passwordVisible"
              >
                <svg v-if="!passwordVisible" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" />
                  <circle cx="12" cy="12" r="3" />
                </svg>
                <svg v-else width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-10-8-10-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 10 8 10 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                  <line x1="1" y1="1" x2="23" y2="23" />
                </svg>
              </button>
            </div>
            <div v-if="passwordErrorMessage" class="field-error">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" />
              </svg>
              <span>{{ passwordErrorMessage }}</span>
            </div>
          </div>
        </div>

        <button class="login-btn" type="submit" :disabled="submitting">
          <span v-if="submitting" class="spinner" />
          <span>{{ submitting ? '登录中...' : '登录' }}</span>
        </button>
      </van-form>

      <div class="aux-links">
        <button class="aux-link" type="button" @click="goRegister">注册账号</button>
        <button class="aux-link" type="button" @click="goForgotPassword">忘记密码</button>
      </div>
    </div>

    <!-- 第三方登录 -->
    <div class="oauth-section">
      <div class="divider"><span>其他登录方式</span></div>
      <div class="oauth-buttons">
        <button class="oauth-btn wechat" type="button" aria-label="微信登录" @click="goOauth('wechat')">
          <svg width="28" height="28" viewBox="0 0 32 32" fill="#fff">
            <path d="M11.5 4C5.7 4 1 7.9 1 12.7c0 2.6 1.4 4.9 3.7 6.5L3.5 23l4.2-2.3c1.2.3 2.5.5 3.8.5.4 0 .8 0 1.2-.1-.4-.9-.6-1.9-.6-2.9 0-3.8 3.4-6.9 7.9-7.3C18.7 6.8 15.5 4 11.5 4zM8.3 9.5c-.7 0-1.3-.6-1.3-1.3s.6-1.3 1.3-1.3 1.3.6 1.3 1.3-.6 1.3-1.3 1.3zm6.4 0c-.7 0-1.3-.6-1.3-1.3s.6-1.3 1.3-1.3 1.3.6 1.3 1.3-.6 1.3-1.3 1.3z" />
            <path d="M30 17.2c0-4-3.9-7.2-8.7-7.2s-8.7 3.2-8.7 7.2 3.9 7.2 8.7 7.2c1 0 1.9-.1 2.8-.4l3.3 1.8-.9-2.9c2-1.2 3.5-3.2 3.5-5.7zm-11.5-1.2c-.5 0-1-.4-1-1s.4-1 1-1 1 .4 1 1-.4 1-1 1zm5.5 0c-.5 0-1-.4-1-1s.4-1 1-1 1 .4 1 1-.4 1-1 1z" />
          </svg>
        </button>
        <button class="oauth-btn alipay" type="button" aria-label="支付宝登录" @click="goOauth('alipay')">
          <svg width="28" height="28" viewBox="0 0 32 32" fill="#fff">
            <path d="M27 5H5C3.3 5 2 6.3 2 8v16c0 1.7 1.3 3 3 3h18.5c-2.1-1.2-4.8-2.7-7.6-4.3-1.4 1.6-3.2 2.6-5.1 2.6-3.2 0-4.3-2.1-2.8-4.3.5-.8 1.4-1.4 2.6-1.8 1.5-.4 3.4-.2 5.1.2.5-.7.9-1.5 1.3-2.3H8.5v-1.4h6.3v-1.8H7.2v-1.4h7.6v-2c0-.4.2-.6.6-.6h3.2v2.6h7.7v1.4h-7.7v1.8h6.3v1.4h-8.7c-.3.8-.7 1.6-1.1 2.3 1.8.6 3.5 1.2 4.8 1.6C24.5 20.5 28 22 28 22c0 .7-.2 1.3-.5 1.8H27c1.7 0 3-1.3 3-3V8c0-1.7-1.3-3-3-3z" />
            <path d="M10.7 21.1c-.6 1.1.1 2 1.4 1.7 1.1-.3 2.1-1 2.9-2-1.4-.4-3-.5-4.3.3z" />
          </svg>
        </button>
      </div>
      <div class="oauth-label">
        登录即代表您已阅读并同意 <a href="javascript:void(0)">《用户协议》</a> 与 <a href="javascript:void(0)">《隐私政策》</a>
      </div>
    </div>

    <!-- 演示账号提示（开发便利） -->
    <div class="demo-tip">
      演示账号：zhangxiaoya / Zhang123456<br>
      双因子演示账号：demo2fa（验证码 123456）
    </div>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  background: var(--n1);
  display: flex;
  flex-direction: column;
}

.brand {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding-top: 64px;
  padding-bottom: 40px;
}

.logo {
  width: 80px;
  height: 80px;
  border-radius: 16px;
  background: linear-gradient(135deg, #1677ff 0%, #4096ff 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 44px;
  font-weight: var(--fw-semibold);
  box-shadow: 0 8px 24px rgba(22, 119, 255, 0.35);
  letter-spacing: -2px;
}

.brand-name {
  margin-top: 16px;
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
  letter-spacing: 1px;
}

.brand-slogan {
  margin-top: 6px;
  font-size: var(--fs-sm);
  color: var(--n7);
  letter-spacing: 2px;
}

.form-area {
  padding: 0 24px;
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

.field-action {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--n7);
  flex-shrink: 0;
  margin-left: 8px;
}

.field-action:active {
  color: var(--n9);
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

.login-btn {
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
  transition: opacity 0.2s;
  box-shadow: 0 4px 12px rgba(22, 119, 255, 0.3);
}

.login-btn:active {
  opacity: 0.85;
}

.login-btn:disabled {
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

.aux-links {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 16px;
  padding: 0 4px;
}

.aux-link {
  font-size: var(--fs-base);
  color: var(--c-primary);
  font-family: inherit;
  padding: 4px 0;
}

.aux-link:active {
  opacity: 0.7;
}

.oauth-section {
  padding: 0 24px 24px;
}

.divider {
  display: flex;
  align-items: center;
  margin: 40px 0 24px;
  color: var(--n7);
  font-size: var(--fs-sm);
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--n3);
}

.divider span {
  padding: 0 12px;
}

.oauth-buttons {
  display: flex;
  justify-content: center;
  gap: 32px;
}

.oauth-btn {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: transform 0.15s;
}

.oauth-btn:active {
  transform: scale(0.92);
}

.oauth-btn.wechat {
  background: #07c160;
  box-shadow: 0 4px 12px rgba(7, 193, 96, 0.3);
}

.oauth-btn.alipay {
  background: #1677ff;
  box-shadow: 0 4px 12px rgba(22, 119, 255, 0.3);
}

.oauth-label {
  text-align: center;
  margin-top: 32px;
  font-size: var(--fs-sm);
  color: var(--n7);
  line-height: 1.6;
}

.oauth-label a {
  color: var(--c-primary);
}

.demo-tip {
  text-align: center;
  padding: 0 24px 32px;
  font-size: var(--fs-sm);
  color: var(--n7);
  line-height: 1.8;
}
</style>
