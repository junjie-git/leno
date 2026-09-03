<script setup lang="ts">
import { useRouter } from 'vue-router'

/**
 * 404 页面未找到
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
    <van-nav-bar title="页面未找到" left-arrow @click-left="router.back()" />
    <div class="content">
      <svg class="illustration" viewBox="0 0 180 160" fill="none" xmlns="http://www.w3.org/2000/svg">
        <ellipse cx="92" cy="144" rx="54" ry="7" fill="#F0F0F0" />
        <rect x="58" y="32" width="66" height="84" rx="8" fill="#FFFFFF" stroke="#D9D9D9" stroke-width="2" />
        <path d="M106 32 L124 50 L106 50 Z" fill="#FAFAFA" stroke="#D9D9D9" stroke-width="2" stroke-linejoin="round" />
        <line x1="70" y1="64" x2="112" y2="64" stroke="#EAEAEA" stroke-width="3.5" stroke-linecap="round" />
        <line x1="70" y1="76" x2="112" y2="76" stroke="#EAEAEA" stroke-width="3.5" stroke-linecap="round" />
        <line x1="70" y1="88" x2="100" y2="88" stroke="#EAEAEA" stroke-width="3.5" stroke-linecap="round" />
        <circle cx="126" cy="104" r="25" fill="#FF4D4F" fill-opacity="0.06" />
        <circle cx="126" cy="104" r="25" fill="none" stroke="#FF4D4F" stroke-width="3" />
        <line x1="143.5" y1="121.5" x2="158" y2="136" stroke="#FF4D4F" stroke-width="4" stroke-linecap="round" />
        <text x="126" y="113" text-anchor="middle" font-size="24" font-weight="700" fill="#FF4D4F">?</text>
        <circle cx="38" cy="50" r="3" fill="#FAAD14" opacity="0.5" />
        <circle cx="160" cy="58" r="3" fill="#1677FF" opacity="0.4" />
        <circle cx="34" cy="108" r="2.5" fill="#D9D9D9" />
        <circle cx="150" cy="92" r="2" fill="#D9D9D9" />
      </svg>
      <span class="status-code">ERROR 404</span>
      <h1 class="title">页面走丢了</h1>
      <p class="desc">您访问的页面可能已被删除或暂时不可用</p>
      <div class="actions">
        <van-button type="primary" round block class="btn-primary" @click="backHome">返回首页</van-button>
        <van-button plain type="primary" round block class="btn-secondary" @click="refresh">刷新试试</van-button>
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
