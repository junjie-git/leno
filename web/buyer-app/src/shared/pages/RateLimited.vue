<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'

/**
 * 429 请求被限流
 *
 * 页面自身通过倒计时读秒引导用户稍后重试（对齐设计稿）。
 */
const router = useRouter()
const retrySeconds = ref(30)
let timer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  timer = setInterval(() => {
    if (retrySeconds.value > 0) {
      retrySeconds.value -= 1
    }
  }, 1000)
})

onUnmounted(() => {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
})

function backHome(): void {
  router.replace('/')
}

function refresh(): void {
  window.location.reload()
}
</script>

<template>
  <div class="page">
    <van-nav-bar title="操作过于频繁" left-arrow @click-left="router.back()" />
    <div class="content">
      <svg class="illustration" viewBox="0 0 180 160" fill="none" xmlns="http://www.w3.org/2000/svg">
        <ellipse cx="90" cy="144" rx="54" ry="7" fill="#F0F0F0" />
        <circle cx="90" cy="70" r="42" fill="#F6FFED" />
        <circle cx="90" cy="70" r="42" stroke="#D9F7BE" stroke-width="2" />
        <line x1="90" y1="70" x2="90" y2="48" stroke="#52C41A" stroke-width="4" stroke-linecap="round" />
        <line x1="90" y1="70" x2="106" y2="80" stroke="#52C41A" stroke-width="4" stroke-linecap="round" />
        <circle cx="90" cy="70" r="3.5" fill="#52C41A" />
        <circle cx="42" cy="48" r="3" fill="#FAAD14" opacity="0.5" />
        <circle cx="146" cy="58" r="3" fill="#1677FF" opacity="0.4" />
        <circle cx="52" cy="110" r="2.5" fill="#D9D9D9" />
        <circle cx="138" cy="102" r="2" fill="#D9D9D9" />
      </svg>
      <span class="status-code">ERROR 429</span>
      <h1 class="title">请求被限流</h1>
      <p class="desc">您的操作过于频繁，请休息一下<br />预计 <b class="seconds">{{ retrySeconds }}</b> 秒后可继续操作</p>
      <div class="actions">
        <van-button type="primary" round block :disabled="retrySeconds > 0" @click="refresh">
          {{ retrySeconds > 0 ? `${retrySeconds}s 后可重试` : '重新加载' }}
        </van-button>
        <van-button plain type="primary" round block @click="backHome">返回首页</van-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.page {
  min-height: 100vh;
  background: var(--n1);
  display: flex;
  flex-direction: column;
}

.content {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--s8) var(--s6);
}

.illustration {
  width: 180px;
  height: 160px;
}

.status-code {
  margin-top: var(--s6);
  font-size: var(--fs-sm);
  font-family: var(--ff-mono);
  color: var(--n7);
  letter-spacing: 2px;
}

.title {
  margin-top: var(--s2);
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.desc {
  margin-top: var(--s2);
  font-size: var(--fs-base);
  color: var(--n7);
  text-align: center;
  line-height: 1.6;
}

.seconds {
  color: var(--c-primary);
  font-family: var(--ff-mono);
}

.actions {
  width: 100%;
  max-width: 280px;
  margin-top: var(--s8);
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
</style>
