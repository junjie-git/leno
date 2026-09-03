<script setup lang="ts">
/**
 * 错误态（对齐设计稿 error-state：插画 + 标题 + 描述 + 重试按钮）
 */
withDefaults(
  defineProps<{
    title?: string
    description?: string
    retryText?: string
  }>(),
  {
    title: '加载失败',
    description: '网络异常，请检查网络连接后重试',
    retryText: '重新加载',
  },
)

const emit = defineEmits<{
  retry: []
}>()
</script>

<template>
  <div class="error-state">
    <svg class="illustration" viewBox="0 0 100 100" fill="none">
      <circle cx="50" cy="50" r="34" fill="#FFF1F0" stroke="#FFCCC7" stroke-width="1.5" />
      <path d="M50 32v20" stroke="#FF4D4F" stroke-width="2.4" stroke-linecap="round" />
      <circle cx="50" cy="66" r="2.4" fill="#FF4D4F" />
    </svg>
    <div class="title">{{ title }}</div>
    <div class="desc">{{ description }}</div>
    <van-button plain type="primary" size="small" round class="retry" @click="emit('retry')">
      {{ retryText }}
    </van-button>
  </div>
</template>

<style scoped>
.error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 64px 0;
}

.illustration {
  width: 100px;
  height: 100px;
}

.title {
  margin-top: var(--s4);
  font-size: var(--fs-base);
  color: var(--n9);
}

.desc {
  margin-top: var(--s1);
  font-size: var(--fs-sm);
  color: var(--n7);
  text-align: center;
  line-height: 1.6;
}

.retry {
  margin-top: var(--s4);
  padding: 0 20px;
}
</style>
