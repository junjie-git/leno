<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast } from 'vant'
import { authApi } from '../api/auth.api'
import type { OAuthProvider } from '../types/auth.dto'
import { useAuthStore } from '@/shared/auth'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'
import { logger } from '@/shared/utils/logger'
import ErrorState from '@/shared/components/ErrorState.vue'

/**
 * 三方授权登录页（微信 / 支付宝）
 *
 * - 进入时获取授权跳转信息（authorizeUrl + state）
 * - 「同意授权」→ 模拟回调换取本站令牌 → 登录成功跳首页
 * - 「拒绝」→ 返回登录页
 */
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const cartStore = useCartStore()

const provider = computed(() => {
  const p = String(route.params.provider ?? 'wechat')
  return (p === 'alipay' ? 'alipay' : 'wechat') as OAuthProvider
})

const providerName = computed(() => (provider.value === 'wechat' ? '微信' : '支付宝'))

const authorizeUrl = ref('')
const state = ref('')
const loading = ref(true)
const loadError = ref(false)
const authorizing = ref(false)

onMounted(async () => {
  try {
    const result = await authApi.getOAuthLoginUrl(provider.value)
    authorizeUrl.value = result.authorizeUrl
    state.value = result.state
  } catch (e) {
    logger.warn('获取授权地址失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
})

async function authorize(): Promise<void> {
  if (authorizing.value) return
  authorizing.value = true
  try {
    // 模拟三方授权回调（真实场景为三方重定向回 /oauth/callback 携带 code）
    const code = `mock-code-${provider.value}-${Date.now()}`
    const result = await authApi.oauthCallback(provider.value, { code, state: state.value })
    authStore.applyLoginResult(result)
    await cartStore.refreshBadge()
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    router.replace(redirect)
  } catch (e) {
    logger.warn('三方授权失败', e)
    showFailToast(e instanceof Error ? e.message : '授权失败，请重试')
  } finally {
    authorizing.value = false
  }
}

function deny(): void {
  router.replace('/login')
}

function retryLoad(): void {
  loadError.value = false
  loading.value = true
  void authApi
    .getOAuthLoginUrl(provider.value)
    .then((result) => {
      authorizeUrl.value = result.authorizeUrl
      state.value = result.state
    })
    .catch((e) => {
      logger.warn('获取授权地址失败', e)
      loadError.value = true
    })
    .finally(() => {
      loading.value = false
    })
}
</script>

<template>
  <div class="oauth-page">
    <van-nav-bar title="授权登录" left-arrow @click-left="router.back()" />

    <div class="content">
      <ErrorState
        v-if="loadError"
        title="授权服务暂不可用"
        description="无法获取授权跳转信息，请检查网络后重试"
        @retry="retryLoad"
      />

      <template v-else>
        <!-- 应用卡 -->
        <div class="app-card">
          <div class="app-icons">
            <div class="app-icon" :class="provider">
              <svg v-if="provider === 'wechat'" width="32" height="32" viewBox="0 0 32 32" fill="#fff">
                <path d="M11.5 4C5.7 4 1 7.9 1 12.7c0 2.6 1.4 4.9 3.7 6.5L3.5 23l4.2-2.3c1.2.3 2.5.5 3.8.5.4 0 .8 0 1.2-.1-.4-.9-.6-1.9-.6-2.9 0-3.8 3.4-6.9 7.9-7.3C18.7 6.8 15.5 4 11.5 4zM8.3 9.5c-.7 0-1.3-.6-1.3-1.3s.6-1.3 1.3-1.3 1.3.6 1.3 1.3-.6 1.3-1.3 1.3zm6.4 0c-.7 0-1.3-.6-1.3-1.3s.6-1.3 1.3-1.3 1.3.6 1.3 1.3-.6 1.3-1.3 1.3z" />
                <path d="M30 17.2c0-4-3.9-7.2-8.7-7.2s-8.7 3.2-8.7 7.2 3.9 7.2 8.7 7.2c1 0 1.9-.1 2.8-.4l3.3 1.8-.9-2.9c2-1.2 3.5-3.2 3.5-5.7zm-11.5-1.2c-.5 0-1-.4-1-1s.4-1 1-1 1 .4 1 1-.4 1-1 1zm5.5 0c-.5 0-1-.4-1-1s.4-1 1-1 1 .4 1 1-.4 1-1 1z" />
              </svg>
              <svg v-else width="32" height="32" viewBox="0 0 32 32" fill="#fff">
                <path d="M27 5H5C3.3 5 2 6.3 2 8v16c0 1.7 1.3 3 3 3h18.5c-2.1-1.2-4.8-2.7-7.6-4.3-1.4 1.6-3.2 2.6-5.1 2.6-3.2 0-4.3-2.1-2.8-4.3.5-.8 1.4-1.4 2.6-1.8 1.5-.4 3.4-.2 5.1.2.5-.7.9-1.5 1.3-2.3H8.5v-1.4h6.3v-1.8H7.2v-1.4h7.6v-2c0-.4.2-.6.6-.6h3.2v2.6h7.7v1.4h-7.7v1.8h6.3v1.4h-8.7c-.3.8-.7 1.6-1.1 2.3 1.8.6 3.5 1.2 4.8 1.6C24.5 20.5 28 22 28 22c0 .7-.2 1.3-.5 1.8H27c1.7 0 3-1.3 3-3V8c0-1.7-1.3-3-3-3z" />
                <path d="M10.7 21.1c-.6 1.1.1 2 1.4 1.7 1.1-.3 2.1-1 2.9-2-1.4-.4-3-.5-4.3.3z" />
              </svg>
            </div>
            <svg class="link-icon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <line x1="5" y1="12" x2="19" y2="12" />
              <polyline points="12 5 19 12 12 19" />
            </svg>
            <div class="app-icon leno">L</div>
          </div>
          <div class="app-name">Leno 买家端</div>
          <div class="app-desc">将使用您的{{ providerName }}账号登录 Leno</div>
          <div class="requester">
            <div class="requester-row"><span class="label">授权应用</span><span class="value">Leno 买家端</span></div>
            <div class="requester-row"><span class="label">开发商</span><span class="value">Leno 科技</span></div>
            <div class="requester-row"><span class="label">回调地址</span><span class="value">leno.app/oauth/callback</span></div>
            <div class="requester-row"><span class="label">授权类型</span><span class="value">OAuth 2.0</span></div>
          </div>
        </div>

        <!-- 授权范围 -->
        <div class="scope-section">
          <div class="scope-title">该应用将获得以下权限</div>
          <div class="scope-list">
            <div class="scope-item">
              <div class="scope-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <circle cx="12" cy="8" r="4" />
                  <path d="M4 20c0-4 4-6 8-6s8 2 8 6" />
                </svg>
              </div>
              <div class="scope-info">
                <div class="scope-name">获取用户基本信息</div>
                <div class="scope-desc">昵称、头像、性别、地区</div>
              </div>
              <div class="scope-required">必要</div>
            </div>
            <div class="scope-item">
              <div class="scope-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <rect x="6" y="2" width="12" height="20" rx="2" />
                  <line x1="12" y1="18" x2="12" y2="18" />
                </svg>
              </div>
              <div class="scope-info">
                <div class="scope-name">获取手机号</div>
                <div class="scope-desc">用于账号绑定与登录验证</div>
              </div>
              <div class="scope-required">必要</div>
            </div>
            <div class="scope-item">
              <div class="scope-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
                </svg>
              </div>
              <div class="scope-info">
                <div class="scope-name">接收消息通知</div>
                <div class="scope-desc">订单、物流、优惠消息推送</div>
              </div>
              <div class="scope-required optional">可选</div>
            </div>
            <div class="scope-item">
              <div class="scope-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M20 12V8H6a2 2 0 0 1-2-2c0-1.1.9-2 2-2h12v4" />
                  <path d="M4 6v12a2 2 0 0 0 2 2h14v-4" />
                  <path d="M18 12a2 2 0 0 0-2 2c0 1.1.9 2 2 2h4v-4h-4z" />
                </svg>
              </div>
              <div class="scope-info">
                <div class="scope-name">获取支付权限</div>
                <div class="scope-desc">用于订单支付与退款</div>
              </div>
              <div class="scope-required optional">可选</div>
            </div>
          </div>
        </div>

        <!-- 提示 -->
        <div class="notice">
          <svg class="notice-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12" y2="17" />
          </svg>
          <div class="notice-text">授权后，Leno 买家端可在 30 天内免再次授权。你可在「账号安全」中随时解除授权。</div>
        </div>
      </template>
    </div>

    <!-- 底部操作 -->
    <div class="footer">
      <button class="btn btn-default" type="button" @click="deny">拒绝</button>
      <button class="btn btn-primary" type="button" :disabled="loading || authorizing" @click="authorize">
        <span v-if="authorizing" class="spinner" />
        <span>{{ authorizing ? '授权中...' : '同意授权' }}</span>
      </button>
      <div class="footer-tip">
        授权即代表同意 <a href="javascript:void(0)">《Leno 授权协议》</a> 与 <a href="javascript:void(0)">《隐私政策》</a>
      </div>
    </div>
  </div>
</template>

<style scoped>
.oauth-page {
  min-height: 100vh;
  background: var(--n2);
  display: flex;
  flex-direction: column;
}

.content {
  flex: 1;
  padding: var(--s3);
}

.app-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s4);
  display: flex;
  flex-direction: column;
  align-items: center;
}

.app-icons {
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.app-icon {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.app-icon.wechat {
  background: #07c160;
}

.app-icon.alipay {
  background: #1677ff;
}

.app-icon.leno {
  background: linear-gradient(135deg, #1677ff 0%, #4096ff 100%);
  color: #fff;
  font-size: 26px;
  font-weight: var(--fw-semibold);
}

.link-icon {
  color: var(--n7);
}

.app-name {
  margin-top: var(--s2);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.app-desc {
  margin-top: var(--s1);
  font-size: var(--fs-base);
  color: var(--n9);
}

.requester {
  width: 100%;
  margin-top: var(--s3);
  padding: var(--s3);
  background: var(--n2);
  border-radius: var(--r-card);
}

.requester-row {
  display: flex;
  justify-content: space-between;
  padding: var(--s1) 0;
  font-size: var(--fs-sm);
}

.requester-row .label {
  color: var(--n7);
}

.requester-row .value {
  color: var(--n9);
}

.scope-section {
  margin-top: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3) var(--s4);
}

.scope-title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  padding: var(--s1) 0 var(--s2);
}

.scope-item {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s3) 0;
  border-bottom: 1px solid var(--n3);
}

.scope-item:last-child {
  border-bottom: none;
}

.scope-icon {
  width: 36px;
  height: 36px;
  border-radius: var(--r-card);
  background: #e6f4ff;
  color: var(--c-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.scope-info {
  flex: 1;
}

.scope-name {
  font-size: var(--fs-base);
  color: var(--n10);
}

.scope-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.scope-required {
  flex-shrink: 0;
  font-size: var(--fs-sm);
  color: var(--c-error);
  background: #fff1f0;
  border-radius: var(--r-base);
  padding: 2px 8px;
}

.scope-required.optional {
  color: var(--n7);
  background: var(--n3);
}

.notice {
  display: flex;
  gap: var(--s2);
  margin-top: var(--s3);
  padding: 0 var(--s1);
}

.notice-icon {
  color: var(--c-warning);
  flex-shrink: 0;
  margin-top: 2px;
}

.notice-text {
  font-size: var(--fs-sm);
  color: var(--n7);
  line-height: 1.6;
}

.footer {
  padding: var(--s3) var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
  background: var(--n1);
  display: flex;
  gap: var(--s3);
}

.btn {
  flex: 1;
  height: 44px;
  border-radius: var(--r-card);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
}

.btn-default {
  background: var(--n3);
  color: var(--n9);
}

.btn-primary {
  background: var(--c-primary);
  color: #fff;
  box-shadow: 0 4px 12px rgba(22, 119, 255, 0.3);
}

.btn-primary:disabled {
  opacity: 0.7;
}

.btn:active {
  opacity: 0.85;
}

.footer-tip {
  margin-top: var(--s2);
  text-align: center;
  font-size: var(--fs-sm);
  color: var(--n7);
  width: 100%;
}

.footer-tip a {
  color: var(--c-primary);
}

.spinner {
  width: 16px;
  height: 16px;
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
