<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Form, FormItem, Input, InputPassword, Checkbox, Alert } from 'ant-design-vue'
import { UserOutlined, LockOutlined, SafetyCertificateOutlined, AuditOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { IdempotencyButton } from '@/shared/components'
import {
  BusinessError,
  NetworkError,
  RateLimitedError,
  ServerError,
  UnauthorizedError,
} from '@/shared/http/errors'
import { logger } from '@/shared/utils/logger'

/**
 * 登录页（09-account）
 *
 * - 双栏布局：左品牌区（渐变 #001529 → #003A8C）+ 右表单区（480px 卡片）
 * - 错误类型分流：凭证错误 / 账号禁用 / 限流倒计时 / 网络异常 / 服务器错误
 * - RateLimited：按 retryAfter 倒计时禁用提交按钮
 * - 登录成功按 redirect 参数回跳（仅接受站内路径，防开放重定向）
 * - 角色校验：非 Operator/Admin 登录后提示无权访问并登出
 */

const REMEMBER_KEY = 'operations:login:remember-username'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

interface LoginFormState {
  username: string
  password: string
  remember: boolean
}

const formState = reactive<LoginFormState>({
  username: '',
  password: '',
  remember: false,
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

const formRef = ref()
const submitting = ref(false)
const errorMsg = ref('')
/** 限流倒计时剩余秒数（>0 时禁用提交按钮） */
const rateLimitRemaining = ref(0)

let countdownTimer: number | undefined

function startCountdown(seconds: number) {
  rateLimitRemaining.value = seconds > 0 ? seconds : 60
  if (countdownTimer !== undefined) window.clearInterval(countdownTimer)
  countdownTimer = window.setInterval(() => {
    rateLimitRemaining.value -= 1
    if (rateLimitRemaining.value <= 0) {
      rateLimitRemaining.value = 0
      window.clearInterval(countdownTimer)
      countdownTimer = undefined
    }
  }, 1000)
}

onMounted(() => {
  const remembered = localStorage.getItem(REMEMBER_KEY)
  if (remembered) {
    formState.username = remembered
    formState.remember = true
  }
})

onUnmounted(() => {
  if (countdownTimer !== undefined) window.clearInterval(countdownTimer)
})

const submitDisabled = computed(() => submitting.value || rateLimitRemaining.value > 0)

const submitText = computed(() => {
  if (submitting.value) return '登录中…'
  if (rateLimitRemaining.value > 0) return `操作过于频繁，请 ${rateLimitRemaining.value}s 后重试`
  return '登录'
})

/** 解析 redirect 参数：仅接受站内路径，防开放重定向 */
function resolveRedirect(): string {
  const raw = route.query.redirect
  if (typeof raw === 'string' && raw.startsWith('/') && !raw.startsWith('//')) {
    return raw
  }
  return '/dashboard/overview'
}

/** 错误分流：凭证/禁用/限流/网络/服务器分别提示 */
function handleLoginError(err: unknown) {
  if (err instanceof RateLimitedError) {
    errorMsg.value = `操作过于频繁，请 ${err.retryAfter > 0 ? err.retryAfter : 60}s 后重试`
    startCountdown(err.retryAfter)
    return
  }
  if (err instanceof BusinessError) {
    if (err.code === 40001) {
      errorMsg.value = '用户名或密码错误'
      formState.password = ''
      return
    }
    if (err.code === 40003) {
      errorMsg.value = '账号已被禁用，请联系管理员'
      return
    }
    errorMsg.value = err.message
    return
  }
  if (err instanceof UnauthorizedError) {
    errorMsg.value = err.message || '认证失败，请重新登录'
    return
  }
  if (err instanceof NetworkError) {
    errorMsg.value = '登录失败，请检查网络连接'
    return
  }
  if (err instanceof ServerError) {
    errorMsg.value = '服务器错误，请稍后重试'
    return
  }
  logger.error('登录未知异常', err)
  errorMsg.value = '登录失败，请稍后重试'
}

async function handleSubmit() {
  errorMsg.value = ''
  try {
    await formRef.value.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    await auth.login({ username: formState.username.trim(), password: formState.password })
    if (!auth.hasRole(['Operator', 'Admin'])) {
      await auth.logout()
      errorMsg.value = '当前账号无运营后台访问权限，请联系管理员'
      return
    }
    if (formState.remember) {
      localStorage.setItem(REMEMBER_KEY, formState.username.trim())
    } else {
      localStorage.removeItem(REMEMBER_KEY)
    }
    message.success(`欢迎回来，${auth.user?.nickname || auth.user?.username || '运营人员'}`)
    await router.push(resolveRedirect())
  } catch (err) {
    handleLoginError(err)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-page__brand">
      <div class="login-brand__logo">Leno</div>
      <h1 class="login-brand__title">运营管理后台</h1>
      <p class="login-brand__slogan">简洁 · 安全 · 高效</p>
      <ul class="login-brand__features">
        <li><SafetyCertificateOutlined class="login-brand__icon" /> IP 白名单接入保护</li>
        <li><AuditOutlined class="login-brand__icon" /> 全量操作审计留痕</li>
      </ul>
    </div>
    <div class="login-page__form-area">
      <div class="login-card">
        <h2 class="login-card__title">运营管理后台</h2>
        <p class="login-card__subtitle">请使用运营账号登录</p>
        <Alert
          v-if="errorMsg"
          type="error"
          :message="errorMsg"
          show-icon
          aria-live="polite"
          class="login-card__alert"
        />
        <Form
          ref="formRef"
          :model="formState"
          :rules="rules"
          layout="vertical"
          @finish="handleSubmit"
        >
          <FormItem name="username" label="用户名">
            <Input
              v-model:value="formState.username"
              placeholder="用户名"
              size="large"
              :maxlength="64"
              aria-label="用户名"
            >
              <template #prefix><UserOutlined style="color: rgba(0, 0, 0, 0.25)" /></template>
            </Input>
          </FormItem>
          <FormItem name="password" label="密码">
            <InputPassword
              v-model:value="formState.password"
              placeholder="密码"
              size="large"
              :maxlength="64"
              aria-label="密码"
              @pressEnter="handleSubmit"
            >
              <template #prefix><LockOutlined style="color: rgba(0, 0, 0, 0.25)" /></template>
            </InputPassword>
          </FormItem>
          <FormItem>
            <div class="login-card__options">
              <Checkbox v-model:checked="formState.remember">记住我</Checkbox>
            </div>
          </FormItem>
          <IdempotencyButton
            type="primary"
            size="large"
            block
            :loading="submitting"
            :disabled="submitDisabled"
            aria-label="登录"
            @click="handleSubmit"
          >
            {{ submitText }}
          </IdempotencyButton>
        </Form>
      </div>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  min-height: 100vh;
  background: #f5f5f5;
}

.login-page__brand {
  width: 50%;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  background: linear-gradient(135deg, #001529 0%, #003a8c 100%);
  color: #ffffff;
  padding: 48px;
}

.login-brand__logo {
  width: 48px;
  height: 48px;
  line-height: 48px;
  text-align: center;
  font-size: 18px;
  font-weight: 700;
  border: 2px solid rgba(255, 255, 255, 0.85);
  border-radius: 8px;
  margin-bottom: 24px;
}

.login-brand__title {
  font-size: 24px;
  font-weight: 600;
  margin: 0 0 8px;
}

.login-brand__slogan {
  font-size: 14px;
  color: rgba(255, 255, 255, 0.65);
  margin: 0 0 48px;
  letter-spacing: 2px;
}

.login-brand__features {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.65);
}

.login-brand__features li {
  display: flex;
  align-items: center;
  gap: 8px;
}

.login-brand__icon {
  color: #52c41a;
}

.login-page__form-area {
  width: 50%;
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 24px;
}

.login-card {
  width: 480px;
  max-width: 100%;
  background: #ffffff;
  border-radius: 8px;
  padding: 40px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
}

.login-card__title {
  font-size: 20px;
  font-weight: 600;
  color: #000000d9;
  margin: 0 0 4px;
}

.login-card__subtitle {
  font-size: 14px;
  color: #8c8c8c;
  margin: 0 0 24px;
}

.login-card__alert {
  margin-bottom: 16px;
}

.login-card__options {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

@media (max-width: 991px) {
  .login-page__brand {
    display: none;
  }

  .login-page__form-area {
    width: 100%;
  }
}
</style>
