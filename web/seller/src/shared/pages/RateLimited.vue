<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

/**
 * 限流提示页
 *
 * 触发 429 限流保护时展示。
 */
const router = useRouter()

function goBack() {
  router.back()
}
</script>

<template>
  <Result status="warning" title="操作过于频繁" sub-title="您的操作触发了限流保护，请稍后重试">
    <template #extra>
      <Button type="primary" @click="goBack">返回上一页</Button>
    </template>
  </Result>
</template>
