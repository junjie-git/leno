<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { PieChart } from 'echarts/charts'
import { TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts'

use([PieChart, TooltipComponent, LegendComponent, CanvasRenderer])

/**
 * 环形图组件
 *
 * 与 shared ChartPie 的差异：donut 半径（内径 45% / 外径 70%）+ 图例右侧竖排，
 * 满足运营设计稿「环形图、图例位于右侧」的要求。
 */
const props = withDefaults(
  defineProps<{
    /** 环形图数据，每项 { name, value } */
    data: { name: string; value: number }[]
    /** 高度（px），默认 280 */
    height?: number
    /** 加载中 */
    loading?: boolean
  }>(),
  {
    height: 280,
    loading: false,
  },
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'item', formatter: '{a} <br/>{b}: {c} ({d}%)' },
  legend: { orient: 'vertical', right: 0, top: 'middle' },
  series: [
    {
      name: '占比',
      type: 'pie',
      radius: ['45%', '70%'],
      center: ['38%', '50%'],
      avoidLabelOverlap: true,
      itemStyle: { borderRadius: 4, borderColor: '#FFFFFF', borderWidth: 2 },
      label: { show: true, formatter: '{d}%' },
      emphasis: {
        itemStyle: {
          shadowBlur: 10,
          shadowOffsetX: 0,
          shadowColor: 'rgba(0, 0, 0, 0.5)',
        },
      },
      data: props.data,
    },
  ],
  color: ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1', '#13C2C2', '#FA541C', '#8C8C8C'],
}))

const hasData = computed(() => Array.isArray(props.data) && props.data.length > 0)
</script>

<template>
  <div class="chart-donut" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-donut__spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-donut {
  width: 100%;
  position: relative;
}
.chart-donut__spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
