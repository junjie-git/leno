<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { UploadProps } from 'ant-design-vue'
import {
  UserOutlined,
  LockOutlined,
  SafetyCertificateOutlined,
  LinkOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons-vue'
import { profileApi } from '../api/profile.api'
import type {
  AccountProfileDto,
  TwoFactorEnableResultDto,
} from '../types/account.dto'
import { useAuthStore } from '@/shared/auth/auth.store'
import { IdempotencyButton, PasswordStrengthIndicator, ConfirmDialog, EmptyState } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 个人资料页（09-account）
 *
 * - 左侧锚点导航 + 右侧四区：基础资料 / 修改密码 / 双因子认证 / 外部登录
 * - 基础资料：头像上传（JPG/PNG <2MB 转 dataURL）+ 姓名/邮箱/手机号可编辑保存
 * - 修改密码：强度条 + 旧密码校验 + 二次确认，成功后登出重新登录
 * - 双因子：启用（二维码 URI 文案区 + 手动密钥 + TOTP 确认）/ 关闭（危险确认）
 * - 外部登录：绑定（OAuth 授权码）/ 解绑（最后一个且未设密码时禁止）
 * - 三态：loading（a-spin）/ error（加载失败重试）/ empty（外部登录未绑定）
 */

type SectionKey = 'basic' | 'password' | 'twofactor' | 'external'

const router = useRouter()
const auth = useAuthStore()

/* ============================== 页面状态 ============================== */

const profile = ref<AccountProfileDto | null>(null)
const loading = ref(false)
const loadError = ref(false)
const activeSection = ref<SectionKey[]>(['basic'])

const sections: { key: SectionKey; label: string; icon: typeof UserOutlined }[] = [
  { key: 'basic', label: '基础资料', icon: UserOutlined },
  { key: 'password', label: '修改密码', icon: LockOutlined },
  { key: 'twofactor', label: '双因子认证', icon: SafetyCertificateOutlined },
  { key: 'external', label: '外部登录', icon: LinkOutlined },
]

async function loadProfile() {
  loading.value = true
  loadError.value = false
  try {
    profile.value = await profileApi.getMyProfile()
    fillBasicForm(profile.value)
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/* ============================== 区域 A/B：基础资料 ============================== */

interface BasicFormState {
  fullName: string
  email: string
  phone: string
  avatarUrl: string | null
}

const basicFormRef = ref()
const basicForm = reactive<BasicFormState>({
  fullName: '',
  email: '',
  phone: '',
  avatarUrl: null,
})
const savingProfile = ref(false)

const basicRules = {
  fullName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email' as const, message: '邮箱格式不正确', trigger: 'blur' },
  ],
  phone: [
    { required: true, message: '请输入手机号', trigger: 'blur' },
    { pattern: /^1\d{10}$/, message: '手机号格式不正确（11 位）', trigger: 'blur' },
  ],
}

function fillBasicForm(p: AccountProfileDto) {
  basicForm.fullName = p.fullName ?? ''
  basicForm.email = p.email ?? ''
  basicForm.phone = p.phone ?? ''
  basicForm.avatarUrl = p.avatarUrl
}

/** 头像首字母占位（无头像时） */
const avatarLetter = computed(() => {
  const name = basicForm.fullName || profile.value?.username || '运'
  return name.charAt(0).toUpperCase()
})

const beforeAvatarUpload: UploadProps['beforeUpload'] = (file) => {
  const isImage = file.type === 'image/jpeg' || file.type === 'image/png'
  if (!isImage) {
    message.error('仅支持 JPG/PNG 格式')
    return false
  }
  const within2MB = file.size / 1024 / 1024 < 2
  if (!within2MB) {
    message.error('头像文件需小于 2MB')
    return false
  }
  const reader = new window.FileReader()
  reader.readAsDataURL(file)
  reader.onload = () => {
    basicForm.avatarUrl = typeof reader.result === 'string' ? reader.result : null
  }
  return false
}

function removeAvatar() {
  basicForm.avatarUrl = null
}

function resetBasicForm() {
  if (profile.value) fillBasicForm(profile.value)
}

async function onSaveProfile() {
  try {
    await basicFormRef.value.validate()
  } catch {
    return
  }
  savingProfile.value = true
  try {
    const updated = await profileApi.updateProfile({
      fullName: basicForm.fullName.trim(),
      email: basicForm.email.trim(),
      phone: basicForm.phone.trim(),
      avatarUrl: basicForm.avatarUrl,
    })
    profile.value = updated
    // 同步 Header 用户信息（nickname / avatar）
    if (auth.user) {
      auth.user = { ...auth.user, nickname: updated.fullName ?? auth.user.nickname, avatar: updated.avatarUrl ?? auth.user.avatar }
    }
    message.success('资料保存成功')
  } catch (err) {
    message.error(err instanceof Error && err.message ? err.message : '资料保存失败，请重试')
  } finally {
    savingProfile.value = false
  }
}

/* ============================== 区域 C：修改密码 ============================== */

interface PasswordFormState {
  oldPassword: string
  newPassword: string
  confirmPassword: string
}

const passwordFormRef = ref()
const passwordForm = reactive<PasswordFormState>({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
})
const changingPassword = ref(false)
const pwdConfirmOpen = ref(false)

/** 密码强度规则：至少 8 位，须含大小写字母、数字、特殊字符 */
const strongPasswordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$/

const passwordRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    {
      pattern: strongPasswordPattern,
      message: '至少 8 位，须含大小写字母、数字、特殊字符',
      trigger: 'blur',
    },
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    {
      validator: (_rule: unknown, value: string) =>
        value === passwordForm.newPassword
          ? Promise.resolve()
          : Promise.reject(new Error('两次输入的新密码不一致')),
      trigger: 'blur',
    },
  ],
}

function resetPasswordForm() {
  passwordForm.oldPassword = ''
  passwordForm.newPassword = ''
  passwordForm.confirmPassword = ''
  passwordFormRef.value?.clearValidate()
}

function onSubmitPassword() {
  passwordFormRef.value?.validate().then(
    () => {
      pwdConfirmOpen.value = true
    },
    () => undefined,
  )
}

async function onConfirmChangePassword() {
  pwdConfirmOpen.value = false
  changingPassword.value = true
  try {
    await profileApi.changePassword({
      oldPassword: passwordForm.oldPassword,
      newPassword: passwordForm.newPassword,
    })
    message.success('密码修改成功，请重新登录')
    await auth.logout()
    await router.push({ path: '/login', query: { redirect: '/account/profile' } })
  } catch (err) {
    // 原密码错误（40002）/ 强度不符等业务错误按后端 message 提示，表单不清空
    message.error(err instanceof Error && err.message ? err.message : '密码修改失败，请重试')
  } finally {
    changingPassword.value = false
  }
}

/* ============================== 区域 D：双因子认证 ============================== */

const twoFactorStatus = computed(() => profile.value?.twoFactorEnabled ?? false)
/** 启用流程中间态：enable 已调用、待 TOTP 确认 */
const enabling = ref(false)
const enableResult = ref<TwoFactorEnableResultDto | null>(null)
const totpCode = ref('')
const enablingTwoFactor = ref(false)
const confirmingTwoFactor = ref(false)
const disableConfirmOpen = ref(false)
const disablingTwoFactor = ref(false)

/** TOTP 码是否为合法 6 位数字 */
const totpValid = computed(() => /^\d{6}$/.test(totpCode.value))

async function onStartEnableTwoFactor() {
  enablingTwoFactor.value = true
  try {
    enableResult.value = await profileApi.enableTwoFactor()
    totpCode.value = ''
    enabling.value = true
  } catch (err) {
    message.error(err instanceof Error && err.message ? err.message : '生成双因子密钥失败，请重试')
  } finally {
    enablingTwoFactor.value = false
  }
}

async function onConfirmTwoFactor() {
  if (!totpValid.value) {
    message.warning('请输入 6 位数字验证码')
    return
  }
  confirmingTwoFactor.value = true
  try {
    await profileApi.confirmTwoFactor({ totpCode: totpCode.value })
    message.success('双因子认证已启用')
    enabling.value = false
    enableResult.value = null
    totpCode.value = ''
    await loadProfile()
  } catch (err) {
    message.error(err instanceof Error && err.message ? err.message : 'TOTP 码错误，请重试')
  } finally {
    confirmingTwoFactor.value = false
  }
}

function onCancelEnableTwoFactor() {
  enabling.value = false
  enableResult.value = null
  totpCode.value = ''
}

function onDisableTwoFactor() {
  disableConfirmOpen.value = true
}

async function onConfirmDisableTwoFactor() {
  disableConfirmOpen.value = false
  disablingTwoFactor.value = true
  try {
    await profileApi.disableTwoFactor()
    message.success('双因子认证已禁用')
    await loadProfile()
  } catch (err) {
    message.error(err instanceof Error && err.message ? err.message : '禁用双因子失败，请重试')
  } finally {
    disablingTwoFactor.value = false
  }
}

/* ============================== 区域 E：外部登录 ============================== */

interface ProviderMeta {
  key: string
  name: string
  color: string
  letter: string
}

/** 支持的外部登录提供方（图标用品牌色 + 首字母占位） */
const providerMetaList: ProviderMeta[] = [
  { key: 'Google', name: 'Google', color: '#DB4437', letter: 'G' },
  { key: 'GitHub', name: 'GitHub', color: '#24292E', letter: 'GH' },
  { key: 'WeChat', name: '微信', color: '#07C160', letter: '微' },
]

function providerMeta(provider: string): ProviderMeta {
  return (
    providerMetaList.find((p) => p.key === provider) ?? {
      key: provider,
      name: provider,
      color: '#1677FF',
      letter: provider.charAt(0).toUpperCase(),
    }
  )
}

/** 已绑定的外部登录 */
const boundLogins = computed(() => profile.value?.externalLogins ?? [])
/** 未绑定的可绑定提供方 */
const unboundProviders = computed(() =>
  providerMetaList.filter((p) => !boundLogins.value.some((b) => b.provider === p.key)),
)

/** 解绑最后一个外部登录且账号未设置密码时禁止（防止无法登录） */
const pendingUnbind = ref<string | null>(null)
const unbindConfirmOpen = ref(false)
const unbinding = ref(false)

function onUnbind(provider: string) {
  if (boundLogins.value.length === 1 && profile.value && !profile.value.hasPassword) {
    message.warning('该账号未设置密码，不能解绑唯一外部登录，请先设置登录密码')
    return
  }
  pendingUnbind.value = provider
  unbindConfirmOpen.value = true
}

async function onConfirmUnbind() {
  unbindConfirmOpen.value = false
  if (!pendingUnbind.value) return
  const provider = pendingUnbind.value
  unbinding.value = true
  try {
    await profileApi.unbindExternalLogin(provider)
    message.success(`已解绑 ${providerMeta(provider).name} 账号`)
    await loadProfile()
  } catch (err) {
    message.error(err instanceof Error && err.message ? err.message : '解绑失败，请重试')
  } finally {
    unbinding.value = false
    pendingUnbind.value = null
  }
}

/** 绑定弹窗（OAuth 授权码模拟回调） */
const bindModalOpen = ref(false)
const bindProvider = ref<ProviderMeta | null>(null)
const bindAuthCode = ref('')
const binding = ref(false)

function onBind(meta: ProviderMeta) {
  bindProvider.value = meta
  bindAuthCode.value = ''
  bindModalOpen.value = true
}

async function onConfirmBind() {
  if (!bindProvider.value) return
  if (!bindAuthCode.value.trim()) {
    message.warning('请输入 OAuth 授权码')
    return
  }
  binding.value = true
  try {
    await profileApi.bindExternalLogin({
      provider: bindProvider.value.key,
      authorizationCode: bindAuthCode.value.trim(),
    })
    message.success(`${bindProvider.value.name} 账号绑定成功`)
    bindModalOpen.value = false
    await loadProfile()
  } catch (err) {
    message.error(err instanceof Error && err.message ? err.message : '绑定失败，请重试')
  } finally {
    binding.value = false
  }
}

/* ============================== 生命周期 ============================== */

onMounted(() => {
  loadProfile()
})
</script>

<template>
  <div class="profile-page">
    <div class="page-header">
      <h1>个人资料</h1>
      <p class="sub">维护当前运营账号的个人资料、修改密码、管理双因子认证与外部登录绑定，保障账号安全。</p>
    </div>

    <a-spin :spinning="loading">
      <!-- 加载失败：错误态 + 重试 -->
      <div v-if="loadError" class="state-block">
        <EmptyState description="加载个人资料失败" action-text="重新加载" @action="loadProfile" />
      </div>

      <div v-else class="profile-layout">
        <!-- 区域 A：左侧锚点导航 -->
        <div class="profile-sider">
          <a-menu v-model:selectedKeys="activeSection" mode="inline" class="profile-menu">
            <a-menu-item v-for="s in sections" :key="s.key">
              <component :is="s.icon" />
              <span>{{ s.label }}</span>
            </a-menu-item>
          </a-menu>
        </div>

        <!-- 右侧内容区 -->
        <div class="profile-main">
          <!-- 区域 B：基础资料 -->
          <a-card v-show="activeSection[0] === 'basic'" :bordered="false" class="section-card">
            <template #title>
              <UserOutlined class="section-icon" />
              基础资料
            </template>
            <div class="avatar-upload">
              <div class="avatar-preview">
                <img v-if="basicForm.avatarUrl" :src="basicForm.avatarUrl" alt="头像" />
                <span v-else>{{ avatarLetter }}</span>
              </div>
              <div class="avatar-upload-info">
                <div class="au-title">头像</div>
                <div class="au-hint">支持 JPG/PNG 格式，文件小于 2MB，建议尺寸 200×200px</div>
                <div class="au-actions">
                  <a-upload
                    :show-upload-list="false"
                    :before-upload="beforeAvatarUpload"
                    accept="image/jpeg,image/png"
                  >
                    <a-button size="small" type="primary">上传头像</a-button>
                  </a-upload>
                  <a-button size="small" :disabled="!basicForm.avatarUrl" @click="removeAvatar">
                    移除
                  </a-button>
                </div>
              </div>
            </div>

            <a-form
              ref="basicFormRef"
              :model="basicForm"
              :rules="basicRules"
              layout="vertical"
              class="basic-form"
            >
              <div class="form-grid">
                <a-form-item label="用户名" name="username">
                  <a-input :value="profile?.username" disabled />
                  <span class="req-hint">用户名不可修改</span>
                </a-form-item>
                <a-form-item label="姓名" name="fullName">
                  <a-input v-model:value="basicForm.fullName" placeholder="请输入姓名" :maxlength="32" />
                </a-form-item>
              </div>
              <div class="form-grid">
                <a-form-item label="邮箱" name="email">
                  <a-input v-model:value="basicForm.email" placeholder="请输入邮箱" :maxlength="128" />
                  <span class="req-hint">用于接收系统通知与重置密码</span>
                </a-form-item>
                <a-form-item label="手机号" name="phone">
                  <a-input v-model:value="basicForm.phone" placeholder="请输入手机号" :maxlength="11" />
                </a-form-item>
              </div>
              <div class="form-actions">
                <IdempotencyButton type="primary" :loading="savingProfile" @click="onSaveProfile">
                  保存资料
                </IdempotencyButton>
                <a-button @click="resetBasicForm">重置</a-button>
              </div>
            </a-form>
          </a-card>

          <!-- 区域 C：修改密码 -->
          <a-card v-show="activeSection[0] === 'password'" :bordered="false" class="section-card">
            <template #title>
              <LockOutlined class="section-icon" />
              修改密码
            </template>
            <a-form
              ref="passwordFormRef"
              :model="passwordForm"
              :rules="passwordRules"
              layout="vertical"
              class="password-form"
            >
              <a-form-item label="原密码" name="oldPassword">
                <a-input-password v-model:value="passwordForm.oldPassword" placeholder="请输入原密码" />
                <span class="req-hint">请输入当前账号的登录密码</span>
              </a-form-item>
              <a-form-item label="新密码" name="newPassword">
                <a-input-password
                  v-model:value="passwordForm.newPassword"
                  placeholder="至少 8 位含大小写字母数字特殊字符"
                  :maxlength="64"
                />
                <PasswordStrengthIndicator :password="passwordForm.newPassword" />
              </a-form-item>
              <a-form-item label="确认新密码" name="confirmPassword">
                <a-input-password
                  v-model:value="passwordForm.confirmPassword"
                  placeholder="请再次输入新密码"
                  :maxlength="64"
                />
              </a-form-item>
              <div class="form-actions">
                <IdempotencyButton
                  type="primary"
                  :loading="changingPassword"
                  @click="onSubmitPassword"
                >
                  提交修改
                </IdempotencyButton>
                <a-button @click="resetPasswordForm">重置</a-button>
              </div>
            </a-form>
          </a-card>

          <!-- 区域 D：双因子认证 -->
          <a-card v-show="activeSection[0] === 'twofactor'" :bordered="false" class="section-card">
            <template #title>
              <SafetyCertificateOutlined class="section-icon" />
              双因子认证
            </template>

            <!-- 状态条 -->
            <div v-if="twoFactorStatus" class="tf-status tf-status--on" aria-live="polite">
              <div class="tf-status__main">
                <CheckCircleOutlined class="tf-status__icon" />
                <div>
                  <div class="tf-status__title">双因子认证已启用</div>
                  <div class="tf-status__desc">
                    启用时间：{{ formatDateTime(profile?.twoFactorEnabledAt ?? null) }}
                  </div>
                </div>
              </div>
              <a-button danger :loading="disablingTwoFactor" @click="onDisableTwoFactor">禁用</a-button>
            </div>
            <div v-else class="tf-status tf-status--off" aria-live="polite">
              <div class="tf-status__main">
                <SafetyCertificateOutlined class="tf-status__icon" />
                <div>
                  <div class="tf-status__title">双因子认证未启用</div>
                  <div class="tf-status__desc">启用后登录需额外输入动态验证码，可显著提升账号安全性</div>
                </div>
              </div>
              <IdempotencyButton
                v-if="!enabling"
                type="primary"
                :loading="enablingTwoFactor"
                @click="onStartEnableTwoFactor"
              >
                启用双因子认证
              </IdempotencyButton>
            </div>

            <!-- 启用流程：密钥 + TOTP 确认 -->
            <div v-if="enabling && enableResult" class="tf-enabling">
              <div class="tf-enabling__title">扫码绑定设备</div>
              <div class="qr-wrap">
                <!-- 二维码文案区：展示 otpauth URI 与手动密钥（无法扫码时的回退方式） -->
                <div class="qr-code-text">
                  <p class="qr-code-text__hint">请使用 Authenticator App（如 Google Authenticator）扫描二维码绑定</p>
                  <p class="qr-code-text__uri">{{ enableResult.qrCodeUri }}</p>
                </div>
                <div class="qr-manual-key">
                  无法扫码？手动输入密钥：<code>{{ enableResult.manualEntryKey }}</code>
                </div>
                <p class="qr-code-text__hint">输入 App 展示的 6 位验证码完成绑定</p>
                <a-input
                  v-model:value="totpCode"
                  class="otp-input"
                  :maxlength="6"
                  placeholder="6 位验证码"
                  aria-label="TOTP 验证码"
                  @pressEnter="onConfirmTwoFactor"
                />
                <div class="tf-enabling__actions">
                  <IdempotencyButton
                    type="primary"
                    :loading="confirmingTwoFactor"
                    :disabled="!totpValid"
                    @click="onConfirmTwoFactor"
                  >
                    确认绑定
                  </IdempotencyButton>
                  <a-button @click="onCancelEnableTwoFactor">取消</a-button>
                </div>
              </div>
            </div>
          </a-card>

          <!-- 区域 E：外部登录 -->
          <a-card v-show="activeSection[0] === 'external'" :bordered="false" class="section-card">
            <template #title>
              <LinkOutlined class="section-icon" />
              外部登录绑定
            </template>

            <template v-if="boundLogins.length || unboundProviders.length">
              <div class="ext-list">
                <!-- 已绑定 -->
                <div v-for="login in boundLogins" :key="login.provider" class="ext-item">
                  <div
                    class="ext-icon"
                    :style="{ backgroundColor: providerMeta(login.provider).color }"
                  >
                    {{ providerMeta(login.provider).letter }}
                  </div>
                  <div class="ext-info">
                    <div class="ext-name">{{ providerMeta(login.provider).name }}</div>
                    <div class="ext-desc">
                      绑定账号：{{ login.externalUserName ?? '—' }} · 绑定时间：{{ formatDateTime(login.boundAt) }}
                    </div>
                  </div>
                  <a-tag color="success">已绑定</a-tag>
                  <a-button size="small" danger :loading="unbinding" @click="onUnbind(login.provider)">
                    解绑
                  </a-button>
                </div>

                <!-- 未绑定（可绑定） -->
                <div v-for="meta in unboundProviders" :key="meta.key" class="ext-item">
                  <div class="ext-icon" :style="{ backgroundColor: meta.color }">{{ meta.letter }}</div>
                  <div class="ext-info">
                    <div class="ext-name">{{ meta.name }}</div>
                    <div class="ext-desc">未绑定 · 支持{{ meta.name }}快捷登录</div>
                  </div>
                  <a-tag>未绑定</a-tag>
                  <a-button size="small" type="primary" @click="onBind(meta)">绑定</a-button>
                </div>
              </div>
            </template>
            <EmptyState v-else description="未绑定外部登录" />
          </a-card>
        </div>
      </div>
    </a-spin>

    <!-- 修改密码二次确认 -->
    <ConfirmDialog
      :open="pwdConfirmOpen"
      title="确认修改密码"
      content="修改密码后需重新登录，其他设备将自动登出。确定继续吗？"
      ok-text="确认修改"
      @confirm="onConfirmChangePassword"
      @cancel="pwdConfirmOpen = false"
    />

    <!-- 禁用双因子二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="确认禁用双因子认证"
      content="禁用后账号安全等级将降低，登录时不再需要动态验证码，建议仅在更换设备时操作。"
      ok-text="确认禁用"
      @confirm="onConfirmDisableTwoFactor"
      @cancel="disableConfirmOpen = false"
    />

    <!-- 解绑外部登录二次确认 -->
    <ConfirmDialog
      :open="unbindConfirmOpen"
      danger
      :title="`确认解绑${pendingUnbind ? providerMeta(pendingUnbind).name : ''}账号`"
      :content="`解绑后将无法使用该账号快捷登录。确定解绑${pendingUnbind ? providerMeta(pendingUnbind).name : ''}吗？`"
      ok-text="确认解绑"
      @confirm="onConfirmUnbind"
      @cancel="unbindConfirmOpen = false"
    />

    <!-- 绑定外部登录（OAuth 授权码） -->
    <a-modal
      v-model:open="bindModalOpen"
      :title="`绑定${bindProvider?.name ?? ''}账号`"
      :confirm-loading="binding"
      ok-text="确认绑定"
      cancel-text="取消"
      @ok="onConfirmBind"
    >
      <p class="bind-hint">
        请完成 {{ bindProvider?.name }} OAuth 授权，并将回调返回的授权码粘贴到下方完成绑定。
      </p>
      <a-input
        v-model:value="bindAuthCode"
        placeholder="请输入 OAuth 授权码"
        allow-clear
        aria-label="OAuth 授权码"
      />
    </a-modal>
  </div>
</template>

<style scoped>
.profile-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.page-header h1 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #000000d9;
}

.page-header .sub {
  margin: 4px 0 0;
  font-size: 12px;
  color: #8c8c8c;
}

.state-block {
  padding: 48px 0;
  background: #ffffff;
  border-radius: 8px;
}

.profile-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}

.profile-sider {
  width: 200px;
  flex-shrink: 0;
  background: #ffffff;
  border-radius: 8px;
  overflow: hidden;
}

.profile-menu {
  border-inline-end: none !important;
}

.profile-main {
  flex: 1;
  min-width: 0;
}

.section-card {
  border-radius: 8px;
}

.section-icon {
  margin-right: 8px;
  color: #1677ff;
}

/* 头像上传 */
.avatar-upload {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 24px;
}

.avatar-preview {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  background: linear-gradient(135deg, #1677ff, #4096ff);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  font-weight: 600;
  overflow: hidden;
  flex-shrink: 0;
}

.avatar-preview img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-upload-info .au-title {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  margin-bottom: 4px;
}

.avatar-upload-info .au-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 8px;
}

.au-actions {
  display: flex;
  gap: 8px;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 24px;
}

.basic-form,
.password-form {
  max-width: 640px;
}

.password-form {
  max-width: 480px;
}

.req-hint {
  display: block;
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}

.form-actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

/* 双因子 */
.tf-status {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 24px;
  border: 1px solid;
}

.tf-status--on {
  background: #f6ffed;
  border-color: #b7eb8f;
}

.tf-status--off {
  background: #fafafa;
  border-color: #d9d9d9;
}

.tf-status__main {
  display: flex;
  align-items: center;
  gap: 12px;
}

.tf-status__icon {
  font-size: 20px;
}

.tf-status--on .tf-status__icon {
  color: #52c41a;
}

.tf-status--off .tf-status__icon {
  color: #8c8c8c;
}

.tf-status__title {
  font-size: 14px;
  font-weight: 500;
}

.tf-status--on .tf-status__title {
  color: #389e0d;
}

.tf-status__desc {
  font-size: 12px;
  color: #595959;
  margin-top: 2px;
}

.tf-enabling {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
}

.tf-enabling__title {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  margin-bottom: 12px;
}

.qr-wrap {
  text-align: center;
}

.qr-code-text__hint {
  font-size: 12px;
  color: #595959;
  margin: 8px 0;
}

.qr-code-text__uri {
  font-size: 12px;
  color: #0958d9;
  background: #e6f4ff;
  border-radius: 6px;
  padding: 8px 12px;
  word-break: break-all;
  margin: 0;
}

.qr-manual-key {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 12px;
}

.qr-manual-key code {
  font-family: 'SF Mono', Consolas, monospace;
  color: #1677ff;
  background: #e6f4ff;
  padding: 2px 6px;
  border-radius: 4px;
  margin-left: 4px;
}

.otp-input {
  max-width: 200px;
  font-size: 20px;
  font-weight: 600;
  letter-spacing: 8px;
  text-align: center;
}

.tf-enabling__actions {
  display: flex;
  justify-content: center;
  gap: 8px;
  margin-top: 16px;
}

/* 外部登录 */
.ext-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.ext-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  transition: border-color 0.15s;
}

.ext-item:hover {
  border-color: #1677ff;
}

.ext-icon {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 13px;
  font-weight: 600;
  color: #ffffff;
}

.ext-info {
  flex: 1;
  min-width: 0;
}

.ext-name {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.ext-desc {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 2px;
}

.bind-hint {
  font-size: 13px;
  color: #595959;
  margin-bottom: 12px;
}

@media (max-width: 991px) {
  .profile-layout {
    flex-direction: column;
  }

  .profile-sider {
    width: 100%;
  }

  .profile-menu {
    display: flex;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
