<!-- web/system-admin/src/modules/06-account/views/Profile.vue -->
<!-- 个人中心：个人信息 / 修改密码 / 安全设置 三标签页 -->
<script setup lang="ts">
import { ref, reactive, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { Form, Modal, message } from 'ant-design-vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { authApi } from '../api/auth.api'
import { AppError } from '@/shared/http/errors'
import { PasswordStrengthIndicator } from '@/shared/components'
import { loginLogsApi } from '@/modules/05-audit/api/login-logs.api'
import type { LoginLogDto } from '@/modules/05-audit/types/login-log.dto'
import { formatDateTime } from '@/shared/utils/format'

type FormInstance = InstanceType<typeof Form>

const router = useRouter()
const auth = useAuthStore()

/** 当前激活标签页 */
const activeTab = ref<'profile' | 'password' | 'security'>('profile')

// ==================== Tab 1：个人信息 ====================

const profileFormRef = ref<FormInstance>()
const profileSaving = ref(false)

const profileForm = reactive({
  username: '',
  email: '',
  phone: '',
  nickname: '',
  avatar: '',
  remark: '',
})

const profileRules = {
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email' as const, message: '邮箱格式不正确', trigger: 'blur' },
  ],
  phone: [
    { pattern: /^1[3-9]\d{9}$/, message: '手机号格式不正确', trigger: 'blur' },
  ],
}

/** 从 authStore.user 同步个人信息到表单 */
function syncProfileForm(): void {
  const u = auth.user
  if (!u) return
  profileForm.username = u.username
  profileForm.email = u.email
  profileForm.phone = u.phone ?? ''
  profileForm.nickname = u.nickname ?? ''
  profileForm.avatar = u.avatar ?? ''
  profileForm.remark = u.remark ?? ''
}

async function onSaveProfile(): Promise<void> {
  try {
    await profileFormRef.value?.validate()
  } catch {
    return
  }
  profileSaving.value = true
  try {
    await authApi.updateProfile({
      email: profileForm.email,
      phone: profileForm.phone,
      nickname: profileForm.nickname,
      avatar: profileForm.avatar,
      remark: profileForm.remark,
    })
    await auth.fetchProfile()
    message.success('资料已保存')
  } catch (e) {
    message.error(e instanceof AppError ? e.message : '保存资料失败')
  } finally {
    profileSaving.value = false
  }
}

// ==================== Tab 2：修改密码 ====================

const passwordFormRef = ref<FormInstance>()
const passwordSaving = ref(false)

const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const passwordRules = {
  currentPassword: [{ required: true, message: '请输入当前密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 8, message: '密码长度不少于 8 位', trigger: 'blur' },
    {
      validator: (_rule: unknown, value: string) => {
        if (!value) return Promise.resolve()
        if (!/[A-Z]/.test(value)) return Promise.reject('需包含大写字母')
        if (!/[a-z]/.test(value)) return Promise.reject('需包含小写字母')
        if (!/[0-9]/.test(value)) return Promise.reject('需包含数字')
        if (value === passwordForm.currentPassword) return Promise.reject('新密码不能与当前密码相同')
        return Promise.resolve()
      },
      trigger: 'blur',
    },
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (_rule: unknown, value: string) => {
        if (!value) return Promise.resolve()
        if (value !== passwordForm.newPassword) return Promise.reject('两次输入的密码不一致')
        return Promise.resolve()
      },
      trigger: 'blur',
    },
  ],
}

/** Modal.info 实例（含 destroy 方法） */
let modalInstance: { destroy: () => void } | null = null
/** 3s 后自动登出跳转的定时器 */
let redirectTimer: ReturnType<typeof setTimeout> | null = null
/** 防止重复跳转标志 */
let isRedirecting = false

async function onChangePassword(): Promise<void> {
  try {
    await passwordFormRef.value?.validate()
  } catch {
    return
  }
  passwordSaving.value = true
  try {
    await authApi.changePassword({
      currentPassword: passwordForm.currentPassword,
      newPassword: passwordForm.newPassword,
    })
    passwordFormRef.value?.resetFields()
    modalInstance = Modal.info({
      title: '密码已修改',
      content: '即将重新登录',
      okText: '知道了',
      onOk: () => {
        void redirectAfterLogout()
      },
    })
    redirectTimer = setTimeout(() => {
      void redirectAfterLogout()
    }, 3000)
  } catch (e) {
    message.error(e instanceof AppError ? e.message : '修改密码失败')
  } finally {
    passwordSaving.value = false
  }
}

/** 登出并跳转登录页（防重入） */
async function redirectAfterLogout(): Promise<void> {
  if (isRedirecting) return
  isRedirecting = true
  if (redirectTimer !== null) {
    clearTimeout(redirectTimer)
    redirectTimer = null
  }
  if (modalInstance) {
    modalInstance.destroy()
    modalInstance = null
  }
  await auth.logout()
  await router.push('/login')
}

// ==================== Tab 3：安全设置 ====================

/** 2FA 开关（始终禁用，仅展示） */
const twoFactorEnabled = ref(false)
const loginLogs = ref<LoginLogDto[]>([])
const loginLogsLoading = ref(false)

async function fetchLoginLogs(): Promise<void> {
  const username = auth.user?.username
  if (!username) return
  loginLogsLoading.value = true
  try {
    const res = await loginLogsApi.list({ username, page: 1, pageSize: 5 })
    loginLogs.value = res.items
  } catch {
    loginLogs.value = []
  } finally {
    loginLogsLoading.value = false
  }
}

// ==================== 生命周期 ====================

onMounted(() => {
  syncProfileForm()
  void fetchLoginLogs()
})

onBeforeUnmount(() => {
  if (redirectTimer !== null) {
    clearTimeout(redirectTimer)
    redirectTimer = null
  }
  if (modalInstance) {
    modalInstance.destroy()
    modalInstance = null
  }
})
</script>

<template>
  <div class="profile-page">
    <a-card :bordered="false">
      <a-tabs v-model:active-key="activeTab">
        <!-- Tab 1：个人信息 -->
        <a-tab-pane key="profile" tab="个人信息">
          <a-form
            ref="profileFormRef"
            :model="profileForm"
            :rules="profileRules"
            layout="vertical"
            class="profile-form"
          >
            <a-form-item label="用户名" name="username">
              <a-input v-model:value="profileForm.username" disabled />
            </a-form-item>
            <a-form-item label="邮箱" name="email">
              <a-input v-model:value="profileForm.email" placeholder="请输入邮箱" />
            </a-form-item>
            <a-form-item label="手机号" name="phone">
              <a-input v-model:value="profileForm.phone" placeholder="请输入手机号" />
            </a-form-item>
            <a-form-item label="昵称" name="nickname">
              <a-input v-model:value="profileForm.nickname" placeholder="请输入昵称" />
            </a-form-item>
            <a-form-item label="头像" name="avatar">
              <a-input v-model:value="profileForm.avatar" placeholder="请输入头像 URL" />
            </a-form-item>
            <a-form-item label="备注" name="remark">
              <a-textarea v-model:value="profileForm.remark" :rows="3" placeholder="请输入备注" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" :loading="profileSaving" @click="onSaveProfile">保存</a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <!-- Tab 2：修改密码 -->
        <a-tab-pane key="password" tab="修改密码">
          <a-form
            ref="passwordFormRef"
            :model="passwordForm"
            :rules="passwordRules"
            layout="vertical"
            class="password-form"
          >
            <a-form-item label="当前密码" name="currentPassword">
              <a-input-password
                v-model:value="passwordForm.currentPassword"
                placeholder="请输入当前密码"
                autocomplete="current-password"
              />
            </a-form-item>
            <a-form-item label="新密码" name="newPassword">
              <a-input-password
                v-model:value="passwordForm.newPassword"
                placeholder="至少 8 位，需含大小写字母与数字"
                autocomplete="new-password"
              />
              <PasswordStrengthIndicator :password="passwordForm.newPassword" />
            </a-form-item>
            <a-form-item label="确认新密码" name="confirmPassword">
              <a-input-password
                v-model:value="passwordForm.confirmPassword"
                placeholder="请再次输入新密码"
                autocomplete="new-password"
              />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" :loading="passwordSaving" @click="onChangePassword">
                提交
              </a-button>
            </a-form-item>
          </a-form>
        </a-tab-pane>

        <!-- Tab 3：安全设置 -->
        <a-tab-pane key="security" tab="安全设置">
          <div class="security-section">
            <a-form layout="vertical">
              <a-form-item label="2FA 双因子认证">
                <a-tooltip title="2FA 暂未启用，敬请期待">
                  <a-switch v-model:checked="twoFactorEnabled" disabled />
                </a-tooltip>
              </a-form-item>
            </a-form>
            <a-divider>最近登录记录</a-divider>
            <a-list
              :data-source="loginLogs"
              :loading="loginLogsLoading"
              item-layout="horizontal"
              bordered
            >
              <template #renderItem="{ item }">
                <a-list-item>
                  <a-list-item-meta>
                    <template #title>
                      <span class="login-time">{{ formatDateTime(item.loginAt) }}</span>
                      <a-tag :color="item.result === 'Success' ? 'green' : 'red'" class="login-tag">
                        {{ item.result === 'Success' ? '成功' : '失败' }}
                      </a-tag>
                    </template>
                    <template #description>
                      <span>IP：{{ item.ipAddress }}</span>
                      <span class="login-geo">地理位置：{{ item.geoLocation || '—' }}</span>
                    </template>
                  </a-list-item-meta>
                </a-list-item>
              </template>
              <template #emptyText>
                <span>暂无登录记录</span>
              </template>
            </a-list>
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<style scoped>
.profile-page {
  max-width: 720px;
  margin: 0 auto;
}
.profile-form,
.password-form {
  max-width: 480px;
}
.security-section {
  max-width: 640px;
}
.login-time {
  font-weight: 500;
}
.login-tag {
  margin-left: 8px;
}
.login-geo {
  margin-left: 16px;
}
</style>
