<script setup lang="ts">
import { computed } from 'vue'
import { Tag } from 'ant-design-vue'

/**
 * StatusTag 类型
 * - deadLetter: 死信状态
 * - orderPayment: 订单支付状态
 * - shop: 店铺审核状态
 */
type StatusTagType = 'deadLetter' | 'orderPayment' | 'shop'

/** Ant Design Vue Tag 颜色值 */
type TagColor = 'success' | 'processing' | 'error' | 'warning' | 'default'

const props = defineProps<{
  /** 业务类型 */
  type: StatusTagType
  /** 状态原始值（来自后端枚举字符串） */
  status: string
}>()

interface StatusMeta {
  label: string
  color: TagColor
}

/**
 * 状态映射表
 *
 * 与 spec §5.5 状态色映射保持一致：
 * - 待处理（死信/任务）→ warning
 * - 已重投/已支付/审核通过/启用 → success
 * - 已丢弃/已封禁/失败/不健康 → error
 * - 进行中/执行中 → processing
 * - 已取消/默认/已关闭 → default
 */
const STATUS_MAP: Record<StatusTagType, Record<string, StatusMeta>> = {
  deadLetter: {
    Pending: { label: '待处理', color: 'warning' },
    Retried: { label: '已重投', color: 'success' },
    Discarded: { label: '已丢弃', color: 'error' },
    Processing: { label: '重投中', color: 'processing' },
  },
  orderPayment: {
    Pending: { label: '待支付', color: 'warning' },
    Paid: { label: '已支付', color: 'success' },
    Refunded: { label: '已退款', color: 'error' },
    Cancelled: { label: '已取消', color: 'default' },
    Failed: { label: '失败', color: 'error' },
  },
  shop: {
    Pending: { label: '待审核', color: 'warning' },
    Approved: { label: '已通过', color: 'success' },
    Rejected: { label: '已拒绝', color: 'error' },
    Banned: { label: '已封禁', color: 'error' },
    Active: { label: '已启用', color: 'success' },
    Inactive: { label: '已停用', color: 'default' },
  },
}

const meta = computed<StatusMeta>(() => {
  const sub = STATUS_MAP[props.type]
  return sub[props.status] ?? { label: props.status, color: 'default' }
})
</script>

<template>
  <Tag :color="meta.color">{{ meta.label }}</Tag>
</template>
