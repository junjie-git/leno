<script setup lang="ts">
import { ConfigProvider } from 'ant-design-vue'
import zhCN from 'ant-design-vue/es/locale/zh_CN'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import { antdTheme } from '@/shared/tokens/antd-theme'

dayjs.locale('zh-cn')
</script>

<template>
  <ConfigProvider :theme="antdTheme" :locale="zhCN">
    <slot />
  </ConfigProvider>
</template>
