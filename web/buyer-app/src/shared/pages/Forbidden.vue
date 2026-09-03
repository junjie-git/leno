<script setup lang="ts">
import { useRouter } from 'vue-router'

/**
 * 403 无访问权限
 */
const router = useRouter()

function backHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="page">
    <van-nav-bar title="无访问权限" left-arrow @click-left="router.back()" />
    <div class="content">
      <svg class="illustration" viewBox="0 0 180 160" fill="none" xmlns="http://www.w3.org/2000/svg">
        <ellipse cx="90" cy="144" rx="54" ry="7" fill="#F0F0F0" />
        <circle cx="90" cy="70" r="42" fill="#FFF1F0" />
        <circle cx="90" cy="70" r="42" stroke="#FFCCC7" stroke-width="2" />
        <rect x="72" y="62" width="36" height="30" rx="6" fill="#FFFFFF" stroke="#FF4D4F" stroke-width="2.5" />
        <path d="M78 62 V54 a12 12 0 0 1 24 0 V62" stroke="#FF4D4F" stroke-width="2.5" fill="none" />
        <circle cx="90" cy="75" r="3" fill="#FF4D4F" />
        <rect x="88.75" y="76" width="2.5" height="8" rx="1.25" fill="#FF4D4F" />
        <circle cx="42" cy="46" r="3" fill="#FAAD14" opacity="0.5" />
        <circle cx="146" cy="52" r="3" fill="#1677FF" opacity="0.4" />
        <circle cx="50" cy="112" r="2.5" fill="#D9D9D9" />
        <circle cx="140" cy="104" r="2" fill="#D9D9D9" />
      </svg>
      <span class="status-code">ERROR 403</span>
      <h1 class="title">无权访问</h1>
      <p class="desc">您的账号没有访问该页面的权限，如需帮助请联系客服</p>
      <div class="actions">
        <van-button type="primary" round block @click="backHome">返回首页</van-button>
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
}
</style>
