<script setup lang="ts">
import { LayoutFooter } from 'ant-design-vue'
import { env } from '@/app/env'

/**
 * 底栏组件
 *
 * 显示版权与版本号。
 */
</script>

<template>
  <LayoutFooter class="footer-bar">
    <span>© Leno · v{{ env.appVersion }}</span>
  </LayoutFooter>
</template>

<style scoped>
.footer-bar {
  text-align: center;
  height: 32px;
  line-height: 32px;
  padding: 0;
  background: #ffffff;
  border-top: 1px solid #f0f0f0;
  color: #8c8c8c;
  font-size: 12px;
}
</style>
