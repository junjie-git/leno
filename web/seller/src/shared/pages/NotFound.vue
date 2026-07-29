<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

/**
 * 404 页面不存在
 *
 * 未匹配路由时跳转此处。
 */
const router = useRouter()

function goHome() {
  void router.push('/')
}
</script>

<template>
  <Result
    status="404"
    title="404"
    sub-title="抱歉，您访问的页面不存在。"
  >
    <template #extra>
      <Button type="primary" @click="goHome">返回首页</Button>
    </template>
  </Result>
</template>
