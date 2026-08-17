<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'
import { useRouter } from 'vue-router'

/**
 * 服务器错误页
 *
 * 后端返回 500 时展示。
 */
const router = useRouter()

function goHome() {
  void router.push('/dashboard/overview')
}
</script>

<template>
  <Result status="500" title="服务器错误" sub-title="服务器内部错误，请稍后重试或联系平台客服">
    <template #extra>
      <Button type="primary" @click="goHome">返回首页</Button>
    </template>
  </Result>
</template>
