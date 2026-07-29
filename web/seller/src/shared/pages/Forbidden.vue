<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

/**
 * 403 无权访问页
 *
 * 路由守卫鉴权失败时跳转此处。
 */
const router = useRouter()

function goHome() {
  void router.push('/')
}
</script>

<template>
  <Result
    status="403"
    title="403"
    sub-title="抱歉，您无权访问该页面。"
  >
    <template #extra>
      <Button type="primary" @click="goHome">返回首页</Button>
    </template>
  </Result>
</template>
