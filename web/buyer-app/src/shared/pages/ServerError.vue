<script setup lang="ts">
import { useRouter } from 'vue-router'

/**
 * 500 服务器错误
 */
const router = useRouter()

function backHome(): void {
  router.replace('/')
}

function refresh(): void {
  window.location.reload()
}
</script>

<template>
  <div class="page">
    <van-nav-bar title="服务器错误" left-arrow @click-left="router.back()" />
    <div class="content">
      <svg class="illustration" viewBox="0 0 180 160" fill="none" xmlns="http://www.w3.org/2000/svg">
        <ellipse cx="90" cy="144" rx="54" ry="7" fill="#F0F0F0" />
        <rect x="46" y="30" width="88" height="66" rx="10" fill="#FFFFFF" stroke="#D9D9D9" stroke-width="2" />
        <rect x="46" y="30" width="88" height="18" rx="9" fill="#FAFAFA" />
        <circle cx="58" cy="39" r="3" fill="#FF4D4F" />
        <circle cx="70" cy="39" r="3" fill="#FAAD14" />
        <circle cx="82" cy="39" r="3" fill="#52C41A" />
        <line x1="58" y1="64" x2="122" y2="64" stroke="#EAEAEA" stroke-width="3.5" stroke-linecap="round" />
        <line x1="58" y1="78" x2="106" y2="78" stroke="#EAEAEA" stroke-width="3.5" stroke-linecap="round" />
        <path d="M70 108 h40 l-6 14 h-28 Z" fill="#FFF1F0" stroke="#FF4D4F" stroke-width="2" stroke-linejoin="round" />
        <text x="90" y="119" text-anchor="middle" font-size="10" font-weight="600" fill="#FF4D4F">500</text>
        <circle cx="40" cy="60" r="3" fill="#FAAD14" opacity="0.5" />
        <circle cx="152" cy="70" r="3" fill="#1677FF" opacity="0.4" />
      </svg>
      <span class="status-code">ERROR 500</span>
      <h1 class="title">服务器开小差了</h1>
      <p class="desc">服务暂时不可用，工程师正在紧急抢修，请稍后重试</p>
      <div class="actions">
        <van-button type="primary" round block class="btn-primary" @click="refresh">重新加载</van-button>
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

.actions {
  width: 100%;
  max-width: 280px;
  margin-top: var(--s8);
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
</style>
