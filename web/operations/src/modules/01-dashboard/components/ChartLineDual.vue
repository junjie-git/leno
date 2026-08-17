<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts'

use([LineChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

/**
 * 双轴折线组件
 *
 * 与 shared ChartLine 的差异：双 y 轴（左轴/右轴各挂一条曲线），
 * 满足「GMV 左轴 + 订单量右轴」「售后单量左轴 + 退款金额右轴」的设计要求。
 * series 中第 1 条挂左轴（yAxisIndex 0），第 2 条挂右轴（yAxisIndex 1）。
 */
const props = withDefaults(
  defineProps<{
    /** 折线数据序列（两条，第 1 条左轴、第 2 条右轴） */
    series: { name: string; data: number[] }[]
    /** X 轴标签数组 */
    xAxis: string[]
    /** 左轴名称 */
    leftName: string
    /** 右轴名称 */
    rightName: string
    /** 高度（px），默认 320 */
    height?: number
    /** 加载中 */
    loading?: boolean
  }>(),
  {
    height: 320,
    loading: false,
  },
)

const hasData = computed(
  () => Array.isArray(props.series) && props.series.length > 0 && props.xAxis.length > 0,
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'axis' },
  legend: { top: 0 },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: props.xAxis,
  },
  yAxis: [
    { type: 'value', name: props.leftName },
    { type: 'value', name: props.rightName, splitLine: { show: false } },
  ],
  series: props.series.map((s, index) => ({
    name: s.name,
    type: 'line' as const,
    smooth: true,
    yAxisIndex: index === 1 ? 1 : 0,
    data: s.data,
  })),
  color: ['#1677FF', '#FAAD14'],
}))
</script>

<template>
  <div class="chart-line-dual" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-line-dual__spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-line-dual {
  width: 100%;
  position: relative;
}
.chart-line-dual__spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
