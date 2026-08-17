<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'

/**
 * 饼图组件
 *
 * 包装 vue-echarts，预设主题色（与 design-tokens.css --c-primary 一致）。
 */
const props = withDefaults(
  defineProps<{
    /** 饼图数据，每项 { name, value } */
    data: { name: string; value: number }[]
    /** 加载中 */
    loading?: boolean
    /** 高度（px），默认 320 */
    height?: number
  }>(),
  {
    loading: false,
    height: 320,
  },
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'item', formatter: '{a} <br/>{b}: {c} ({d}%)' },
  legend: { orient: 'vertical', left: 'left' },
  series: [
    {
      name: '占比',
      type: 'pie',
      radius: '50%',
      data: props.data,
      emphasis: {
        itemStyle: {
          shadowBlur: 10,
          shadowOffsetX: 0,
          shadowColor: 'rgba(0, 0, 0, 0.5)',
        },
      },
    },
  ],
  color: ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1', '#13C2C2', '#FA541C', '#8C8C8C'],
}))

const hasData = computed(() => Array.isArray(props.data) && props.data.length > 0)
</script>

<template>
  <div class="chart-pie" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-pie {
  width: 100%;
  position: relative;
}
.chart-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
