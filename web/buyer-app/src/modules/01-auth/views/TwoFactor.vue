<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast } from 'vant'
import { useAuthStore } from '@/shared/auth'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import { logger } from '@/shared/utils/logger'

/**
 * 双因子验证页（登录二段验证）
 *
 * - 6 位验证码输入（逐格输入自动前进，支持粘贴）
 * - 演示验证码：123456
 * - 验证成功后完成登录并按 redirect 跳转
 */
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const cartStore = useCartStore()

const ticket = computed(() => (typeof route.query.ticket === 'string' ? route.query.ticket : ''))

const digits = ref<string[]>(['', '', '', '', '', ''])
const inputs = ref<HTMLInputElement[]>([])
const submitting = ref(false)
const codeCountdown = ref(60)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const code = computed(() => digits.value.join(''))

onMounted(() => {
  countdownTimer = setInterval(() => {
    if (codeCountdown.value > 0) {
      codeCountdown.value -= 1
    }
  }, 1000)
  if (!ticket.value) {
    showFailToast('登录会话缺失，请重新登录')
    router.replace('/login')
    return
  }
  inputs.value[0]?.focus()
})

onUnmounted(() => {
  if (countdownTimer) {
    clearInterval(countdownTimer)
  }
})

/** 单格输入：自动前进 */
function onDigitInput(index: number, event: Event): void {
  const target = event.target as HTMLInputElement
  const value = target.value.replace(/\D/g, '')
  if (value) {
    digits.value[index] = value.slice(-1)
    if (index < 5) {
      inputs.value[index + 1]?.focus()
    }
  } else {
    digits.value[index] = ''
  }
  if (code.value.length === 6 && !submitting.value) {
    void onSubmit()
  }
}

/** 删除：回退到上一格 */
function onDigitKeydown(index: number, event: KeyboardEvent): void {
  if (event.key === 'Backspace' && !digits.value[index] && index > 0) {
    inputs.value[index - 1]?.focus()
    digits.value[index - 1] = ''
    event.preventDefault()
  }
}

/** 整段粘贴验证码 */
function onPaste(event: ClipboardEvent): void {
  const text = event.clipboardData?.getData('text') ?? ''
  const cleaned = text.replace(/\D/g, '').slice(0, 6)
  if (cleaned.length === 6) {
    event.preventDefault()
    digits.value = cleaned.split('')
    void onSubmit()
  }
}

watch(code, (value) => {
  if (value.length === 6) {
    inputs.value[5]?.blur()
  }
})

/** 模板 ref 收集（避免模板作用域内使用全局类型） */
function setInputRef(index: number, el: unknown): void {
  if (el instanceof HTMLInputElement) {
    inputs.value[index] = el
  }
}

async function onSubmit(): Promise<void> {
  if (submitting.value) return
  if (code.value.length !== 6) {
    showFailToast('请输入 6 位验证码')
    return
  }
  submitting.value = true
  try {
    const result = await authStore.verifyTwoFactor({
      twoFactorTicket: ticket.value,
      code: code.value,
    })
    if (!result.token) {
      showFailToast('验证失败，请重试')
      return
    }
    await cartStore.refreshBadge()
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    router.replace(redirect)
  } catch (e) {
    logger.warn('双因子验证失败', e)
    showFailToast(e instanceof Error ? e.message : '验证失败，请重试')
    digits.value = ['', '', '', '', '', '']
    inputs.value[0]?.focus()
  } finally {
    submitting.value = false
  }
}

/** 重发（演示：重置倒计时） */
function resend(): void {
  if (codeCountdown.value > 0) return
  codeCountdown.value = 60
  showFailToast('验证码已重发（演示验证码 123456）')
}

function backToLogin(): void {
  router.replace('/login')
}
</script>

<template>
  <div class="twofactor-page">
    <van-nav-bar title="双因子验证" left-arrow @click-left="backToLogin" />

    <div class="content">
      <div class="head">
        <div class="icon-wrap">
          <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#1677FF" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            <path d="m9 12 2 2 4-4" />
          </svg>
        </div>
        <div class="title">安全验证</div>
        <div class="desc">请输入认证器 App 中的 6 位动态验证码<br>完成登录验证（演示验证码 123456）</div>
      </div>

      <div class="code-boxes" @paste="onPaste">
        <input
          v-for="(_, index) in digits"
          :key="index"
          :ref="(el) => setInputRef(index, el)"
          :value="digits[index]"
          type="tel"
          maxlength="2"
          class="code-box"
          :class="{ filled: !!digits[index] }"
          aria-label="验证码"
          inputmode="numeric"
          autocomplete="one-time-code"
          @input="onDigitInput(index, $event)"
          @keydown="onDigitKeydown(index, $event)"
        >
      </div>

      <div class="resend-row">
        <span v-if="codeCountdown > 0" class="resend-countdown">{{ codeCountdown }}s 后可重发</span>
        <button v-else class="resend-btn" type="button" @click="resend">重新发送</button>
      </div>

      <button class="verify-btn" type="button" :disabled="submitting || code.length !== 6" @click="onSubmit">
        <span v-if="submitting" class="spinner" />
        <span>{{ submitting ? '验证中...' : '验证并登录' }}</span>
      </button>

      <button class="switch-btn" type="button" @click="backToLogin">换个账号登录</button>
    </div>
  </div>
</template>

<style scoped>
.twofactor-page {
  min-height: 100vh;
  background: var(--n1);
  display: flex;
  flex-direction: column;
}

.content {
  padding: 48px 32px 32px;
  flex: 1;
}

.head {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.icon-wrap {
  width: 64px;
  height: 64px;
  border-radius: var(--r-lg);
  background: #e6f4ff;
  display: flex;
  align-items: center;
  justify-content: center;
}

.title {
  margin-top: var(--s4);
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.desc {
  margin-top: var(--s2);
  font-size: var(--fs-base);
  color: var(--n7);
  line-height: 1.7;
}

.code-boxes {
  display: flex;
  justify-content: space-between;
  gap: var(--s2);
  margin-top: 40px;
}

.code-box {
  width: 44px;
  height: 52px;
  border: 1.5px solid var(--n5);
  border-radius: var(--r-card);
  background: var(--n2);
  text-align: center;
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
  font-family: var(--ff-mono);
  outline: none;
  transition:
    border-color 0.2s,
    background 0.2s;
}

.code-box:focus {
  border-color: var(--c-primary);
  background: var(--n1);
}

.code-box.filled {
  border-color: var(--c-primary);
  background: #f0f7ff;
}

.resend-row {
  display: flex;
  justify-content: center;
  margin-top: var(--s4);
  min-height: 24px;
}

.resend-countdown {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.resend-btn {
  font-size: var(--fs-sm);
  color: var(--c-primary);
}

.verify-btn {
  width: 100%;
  height: 48px;
  border: none;
  border-radius: var(--r-card);
  background: var(--c-primary);
  color: #fff;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  margin-top: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  box-shadow: 0 4px 12px rgba(22, 119, 255, 0.3);
}

.verify-btn:disabled {
  background: var(--n5);
  color: var(--n7);
  box-shadow: none;
  cursor: not-allowed;
}

.switch-btn {
  width: 100%;
  margin-top: var(--s4);
  text-align: center;
  font-size: var(--fs-base);
  color: var(--n7);
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
