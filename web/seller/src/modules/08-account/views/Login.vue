<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Form, FormItem, Input, InputPassword, Button, Alert, Card } from 'ant-design-vue'
import { UserOutlined, LockOutlined } from '@ant-design/icons-vue'
import { useAuthStore } from '@/shared/auth'
import { useShopStore } from '@/shared/shop'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const shop = useShopStore()

const loading = ref(false)
const errorMsg = ref('')

const form = reactive({
  username: '',
  password: '',
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function onSubmit() {
  errorMsg.value = ''
  loading.value = true
  try {
    await auth.login(form)
    await shop.fetchMyShop()
    const redirect = (route.query.redirect as string) || '/dashboard/overview'
    if (shop.shopStatus === 'PendingReview' || shop.shopStatus === 'Rejected') {
      router.push('/shop/application')
    } else {
      router.push(redirect)
    }
  } catch (e: any) {
    if (e?.status === 401) errorMsg.value = '账号或密码错误'
    else if (e?.status === 403) errorMsg.value = '账号已禁用'
    else if (e?.status === 429) errorMsg.value = '操作过于频繁，请稍后重试'
    else errorMsg.value = e?.message || '登录失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-container">
    <Card class="login-card">
      <div class="login-header">
        <h1>Leno 卖家管理后台</h1>
        <p>请登录您的卖家账号</p>
      </div>

      <Alert v-if="errorMsg" type="error" :message="errorMsg" show-icon class="login-alert" />

      <Form :model="form" :rules="rules" layout="vertical" @finish="onSubmit">
        <FormItem name="username">
          <Input
            v-model:value="form.username"
            size="large"
            placeholder="用户名"
            @pressEnter="onSubmit"
          >
            <template #prefix><UserOutlined /></template>
          </Input>
        </FormItem>
        <FormItem name="password">
          <InputPassword
            v-model:value="form.password"
            size="large"
            placeholder="密码"
            @pressEnter="onSubmit"
          >
            <template #prefix><LockOutlined /></template>
          </InputPassword>
        </FormItem>
        <FormItem>
          <Button type="primary" html-type="submit" size="large" block :loading="loading">
            登录
          </Button>
        </FormItem>
      </Form>

      <div class="two-factor-placeholder">
        <Input disabled size="large" placeholder="两步验证（暂未启用）">
          <template #prefix><LockOutlined /></template>
        </Input>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.login-container {
  min-height: 100vh; display: flex; align-items: center; justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
.login-card { width: 400px; padding: 24px; }
.login-header { text-align: center; margin-bottom: 24px; }
.login-header h1 { font-size: 24px; color: #1677ff; margin-bottom: 8px; }
.login-header p { color: #8c8c8c; font-size: 14px; }
.login-alert { margin-bottom: 16px; }
.two-factor-placeholder { margin-top: 16px; opacity: 0.5; }
</style>
