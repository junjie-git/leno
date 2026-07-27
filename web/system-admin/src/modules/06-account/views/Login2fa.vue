<script setup lang="ts">
import { ref, reactive, computed, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  Form,
  FormItem,
  Input,
  InputPassword,
  Steps,
  Step,
  Button,
  Alert,
  Badge,
  Modal,
  message,
} from 'ant-design-vue'
import { UserOutlined, LockOutlined, SafetyOutlined } from '@ant-design/icons-vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import {
  UnauthorizedError,
  ForbiddenError,
  RateLimitedError,
  NetworkError,
  AppError,
} from '@/shared/http/errors'
import { logger } from '@/shared/utils/logger'

type FormInstance = InstanceType<typeof Form>

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const formRef = ref<FormInstance>()
const loading = ref(false)
const errorMsg = ref<string | null>(null)
const retryCountdown = ref(0)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const formModel = reactive({
  username: '',
  password: '',
})

const fieldErrors = reactive<{ username: string; password: string }>({
  username: '',
  password: '',
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不少于 6 位', trigger: 'blur' },
  ],
}

const redirectTarget = computed(() => {
  const r = route.query.redirect
  return typeof r === 'string' && r.length > 0 ? r : '/dashboard/operations-overview'
})

function startCountdown(seconds: number) {
  retryCountdown.value = seconds
  if (countdownTimer) clearInterval(countdownTimer)
  countdownTimer = setInterval(() => {
    retryCountdown.value -= 1
    if (retryCountdown.value <= 0) {
      retryCountdown.value = 0
      if (countdownTimer) {
        clearInterval(countdownTimer)
        countdownTimer = null
      }
    }
  }, 1000)
}

onUnmounted(() => {
  if (countdownTimer) clearInterval(countdownTimer)
})

function clearFieldError(field: 'username' | 'password') {
  fieldErrors[field] = ''
}

function validateForm(): boolean {
  let valid = true
  if (!formModel.username) {
    fieldErrors.username = '请输入用户名'
    valid = false
  } else {
    fieldErrors.username = ''
  }
  if (!formModel.password) {
    fieldErrors.password = '请输入密码'
    valid = false
  } else if (formModel.password.length < 6) {
    fieldErrors.password = '密码长度不少于 6 位'
    valid = false
  } else {
    fieldErrors.password = ''
  }
  return valid
}

async function onSubmit() {
  errorMsg.value = null
  if (!validateForm()) return
  loading.value = true
  try {
    await auth.login({ username: formModel.username, password: formModel.password })
    message.success('登录成功', 1.5)
    await router.push(redirectTarget.value)
  } catch (e) {
    if (e instanceof UnauthorizedError) {
      errorMsg.value = '账号或密码错误'
    } else if (e instanceof ForbiddenError) {
      errorMsg.value = '账号已禁用'
    } else if (e instanceof RateLimitedError) {
      errorMsg.value = `操作过于频繁，请 ${e.retryAfter} 秒后重试`
      startCountdown(e.retryAfter)
    } else if (e instanceof NetworkError) {
      errorMsg.value = '网络异常，请稍后重试'
    } else if (e instanceof AppError) {
      errorMsg.value = e.message
    } else {
      errorMsg.value = '登录失败，请稍后重试'
      logger.error('登录未知错误', e)
    }
  } finally {
    loading.value = false
  }
}

function showForgotPassword() {
  Modal.info({
    title: '忘记密码',
    content: '请联系超级管理员通过审批流程重置密码。',
    okText: '知道了',
  })
}
</script>

<template>
  <div class="login-page">
    <section class="login-brand">
      <div class="login-brand-inner">
        <div class="login-brand-logo">Leno</div>
        <h1 class="login-brand-title">Leno 系统管理后台</h1>
        <p class="login-brand-security">JWT + 双因子 + IP 白名单 + 全操作审计</p>
      </div>
    </section>
    <section class="login-form-area">
      <div class="login-card">
        <Steps :current="0" size="small" class="login-steps">
          <Step title="账号密码" />
          <Step title="双因子验证" />
        </Steps>

        <Alert
          v-if="errorMsg"
          :message="errorMsg"
          type="error"
          show-icon
          class="login-alert"
        />

        <Form
          ref="formRef"
          :model="formModel"
          :rules="rules"
          layout="vertical"
          @submit.prevent="onSubmit"
        >
          <FormItem
            name="username"
            :validate-status="fieldErrors.username ? 'error' : undefined"
            :help="fieldErrors.username || undefined"
          >
            <Input
              v-model:value="formModel.username"
              size="large"
              placeholder="请输入用户名"
              :disabled="loading"
              aria-label="用户名"
              @change="clearFieldError('username')"
            >
              <template #prefix><UserOutlined /></template>
            </Input>
          </FormItem>
          <FormItem
            name="password"
            :validate-status="fieldErrors.password ? 'error' : undefined"
            :help="fieldErrors.password || undefined"
          >
            <InputPassword
              v-model:value="formModel.password"
              size="large"
              placeholder="请输入密码"
              :disabled="loading"
              aria-label="密码"
              @change="clearFieldError('password')"
            >
              <template #prefix><LockOutlined /></template>
            </InputPassword>
          </FormItem>
          <FormItem>
            <IdempotencyButton
              type="primary"
              size="large"
              block
              :loading="loading"
              :disabled="retryCountdown > 0"
              @click="onSubmit"
            >
              {{ retryCountdown > 0 ? `${retryCountdown}s 后重试` : '登录' }}
            </IdempotencyButton>
          </FormItem>
          <FormItem>
            <Button type="link" class="login-forgot" @click="showForgotPassword">忘记密码？</Button>
          </FormItem>
        </Form>

        <div class="login-otp-preview">
          <div class="login-otp-head">
            <SafetyOutlined />
            <span class="login-otp-title">双因子验证</span>
            <Badge status="default" text="2FA 暂未启用" />
          </div>
          <p class="login-otp-hint">请打开 Authenticator App 获取验证码（2FA 暂未启用）</p>
          <div class="login-otp-boxes">
            <Input
              v-for="i in 6"
              :key="i"
              :value="''"
              size="large"
              :maxlength="1"
              disabled
              class="login-otp-box"
              :aria-label="`验证码第 ${i} 位`"
            />
          </div>
          <IdempotencyButton type="primary" block disabled>验证</IdempotencyButton>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  min-height: 100vh;
  background: var(--n2, #fafafa);
}
.login-brand {
  width: 50%;
  background: #001529;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
}
.login-brand-inner {
  text-align: center;
  padding: 48px;
}
.login-brand-logo {
  font-size: 32px;
  font-weight: 600;
  margin-bottom: 16px;
}
.login-brand-title {
  font-size: 24px;
  font-weight: 600;
  color: #fff;
  margin: 0 0 12px;
}
.login-brand-security {
  font-size: 12px;
  color: #52c41a;
  margin: 0;
}
.login-form-area {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
}
.login-card {
  width: 400px;
  max-width: 100%;
}
.login-steps {
  margin-bottom: 24px;
}
.login-alert {
  margin-bottom: 16px;
}
.login-forgot {
  padding: 0;
  float: right;
}
.login-otp-preview {
  margin-top: 24px;
  padding: 16px;
  background: var(--n3, #f5f5f5);
  border-radius: var(--r-base, 6px);
  opacity: 0.75;
}
.login-otp-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.login-otp-title {
  font-size: 14px;
  font-weight: 500;
}
.login-otp-hint {
  font-size: 12px;
  color: var(--n7, #8c8c8c);
  margin: 0 0 12px;
}
.login-otp-boxes {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}
.login-otp-box {
  flex: 1;
  text-align: center;
}
@media (max-width: 1199px) {
  .login-brand {
    display: none;
  }
}
</style>
