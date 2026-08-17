<script setup lang="ts">
import { computed } from 'vue'
import { Tag } from 'ant-design-vue'

/**
 * 促销运营模块状态标签
 *
 * shared/components/StatusTag 的类型表面向用户/订单等域，促销域三套状态机
 * （促销活动 / 券模板 / 秒杀活动）在模块内本地维护映射，视觉规范与
 * StatusTag 一致（Tag 预设色），并按 md 可访问性要求附带 aria-label。
 */

/** 支持的状态域 */
export type PromoStatusKind = 'promotion' | 'coupon' | 'seckill'

/** 状态展示元数据 */
interface StatusMeta {
  label: string
  color: 'success' | 'processing' | 'error' | 'warning' | 'default'
}

/** 三套状态机的展示映射（与各页 md §6 状态色保持一致） */
const STATUS_MAP: Record<PromoStatusKind, Record<string, StatusMeta>> = {
  promotion: {
    Pending: { label: '待生效', color: 'warning' },
    Active: { label: '进行中', color: 'success' },
    Paused: { label: '已暂停', color: 'default' },
    Closed: { label: '已关闭', color: 'default' },
  },
  coupon: {
    Draft: { label: '草稿', color: 'warning' },
    Published: { label: '已发布', color: 'success' },
    Stopped: { label: '已停用', color: 'default' },
  },
  seckill: {
    Pending: { label: '待生效', color: 'warning' },
    Active: { label: '进行中', color: 'success' },
    Closed: { label: '已关闭', color: 'default' },
  },
}

const props = defineProps<{
  /** 状态域 */
  kind: PromoStatusKind
  /** 状态原始值（来自后端枚举字符串） */
  status: string
}>()

const meta = computed<StatusMeta>(
  () => STATUS_MAP[props.kind][props.status] ?? { label: props.status, color: 'default' },
)
</script>

<template>
  <Tag :color="meta.color" :aria-label="meta.label">{{ meta.label }}</Tag>
</template>
