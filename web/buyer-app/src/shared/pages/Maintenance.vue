<script setup lang="ts">
/**
 * 系统维护中
 */
function refresh(): void {
  window.location.reload()
}
</script>

<template>
  <div class="page">
    <van-nav-bar title="系统维护中" />
    <div class="content">
      <svg class="illustration" viewBox="0 0 180 160" fill="none" xmlns="http://www.w3.org/2000/svg">
        <ellipse cx="90" cy="144" rx="54" ry="7" fill="#F0F0F0" />
        <circle cx="90" cy="72" r="44" fill="#FFF7E6" />
        <circle cx="90" cy="72" r="44" stroke="#FFE7BA" stroke-width="2" />
        <path d="M90 46 a26 26 0 0 1 22.5 39 l-8-4.6 A17 17 0 0 0 90 55 Z" fill="#FAAD14" />
        <path d="M90 98 a26 26 0 0 1 -22.5 -39 l8 4.6 A17 17 0 0 0 90 89 Z" fill="#FAAD14" />
        <line x1="90" y1="40" x2="90" y2="30" stroke="#FAAD14" stroke-width="3" stroke-linecap="round" />
        <line x1="90" y1="114" x2="90" y2="124" stroke="#FAAD14" stroke-width="3" stroke-linecap="round" />
        <line x1="58" y1="72" x2="48" y2="72" stroke="#FAAD14" stroke-width="3" stroke-linecap="round" />
        <line x1="122" y1="72" x2="132" y2="72" stroke="#FAAD14" stroke-width="3" stroke-linecap="round" />
        <circle cx="42" cy="46" r="3" fill="#1677FF" opacity="0.4" />
        <circle cx="148" cy="100" r="3" fill="#FAAD14" opacity="0.5" />
      </svg>
      <span class="status-code">MAINTENANCE</span>
      <h1 class="title">系统维护中</h1>
      <p class="desc">我们正在升级系统以提供更好的服务<br />预计 30 分钟后恢复，感谢您的耐心等待</p>
      <div class="actions">
        <van-button type="primary" round block @click="refresh">稍后重试</van-button>
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
