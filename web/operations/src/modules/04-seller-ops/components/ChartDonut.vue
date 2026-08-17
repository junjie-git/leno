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
 * 类目分布环形图组件（04-seller-ops 模块内自建）
 *
 * - donut 半径（内径 45% / 外径 70%）+ 图例右侧竖排
 * - 中心展示总卖家数，满足卖家统计「类目分布环形图」的视觉要求
 */
const props = withDefaults(
  defineProps<{
    /** 环形图数据，每项 { name, value } */
    data: { name: string; value: number }[]
    /** 中心汇总数值 */
    centerValue?: number
    /** 中心标题 */
    centerLabel?: string
    /** 高度（px），默认 280 */
    height?: number
    /** 加载中 */
    loading?: boolean
  }>(),
  {
    centerValue: undefined,
    centerLabel: '总卖家',
    height: 280,
    loading: false,
  },
)

const hasData = computed(() => Array.isArray(props.data) && props.data.length > 0)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'item', formatter: '{a} <br/>{b}: {c} ({d}%)' },
  legend: { orient: 'vertical', right: 0, top: 'middle' },
  title: {
    text: props.centerValue === undefined ? '' : props.centerValue.toLocaleString('zh-CN'),
    subtext: props.centerValue === undefined ? '' : props.centerLabel,
    left: '30%',
    top: '40%',
    textAlign: 'center',
    textStyle: { fontSize: 20, fontWeight: 600, color: '#000000D9' },
    subtextStyle: { fontSize: 12, color: '#8C8C8C' },
  },
  series: [
    {
      name: '卖家数',
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
  color: ['#1677FF', '#722ED1', '#13C2C2', '#FAAD14', '#FA8C16', '#52C41A', '#FF4D4F', '#8C8C8C'],
}))
</script>

<template>
  <div class="seller-chart-donut" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="seller-chart-donut__spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.seller-chart-donut {
  width: 100%;
  position: relative;
}
.seller-chart-donut__spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
