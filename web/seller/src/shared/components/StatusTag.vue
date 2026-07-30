<script setup lang="ts">
import { computed } from 'vue'
import { Tag } from 'ant-design-vue'

/**
 * StatusTag 类型
 *
 * 通用状态映射（继承自 system-admin）：
 * - deadLetter: 死信状态
 * - orderPayment: 订单支付状态
 * - user: 管理员用户状态
 * - oauth: OAuth 客户端状态
 * - operator: 运营人员状态
 * - loginResult: 登录结果
 * - cacheType: 缓存类型
 * - menuType: 菜单类型
 * - onlineUser: 在线用户状态
 *
 * 卖家业务状态映射：
 * - shop: 卖家店铺状态（Active/Suspended/PendingReview/Rejected）
 * - product: 商品状态
 * - order: 卖家订单状态
 * - aftersales: 售后单状态
 * - freightTemplate: 运费模板状态
 */
type StatusTagType =
  | 'deadLetter'
  | 'orderPayment'
  | 'shop'
  | 'user'
  | 'oauth'
  | 'operator'
  | 'loginResult'
  | 'cacheType'
  | 'menuType'
  | 'onlineUser'
  | 'product'
  | 'order'
  | 'aftersales'
  | 'freightTemplate'

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
 * 通用部分与 spec §5.5 状态色映射保持一致：
 * - 待处理（死信/任务）→ warning
 * - 已重投/已支付/审核通过/启用 → success
 * - 已丢弃/已封禁/失败/不健康 → error
 * - 进行中/执行中 → processing
 * - 已取消/默认/已关闭 → default
 *
 * 卖家业务部分：
 * - shop: Active 为正常(success)，Suspended/Rejected 为 error，PendingReview/Pending 为 warning，Closed 为 default
 * - product: 草稿/已下架为 default，待审核为 warning，已上架为 success，已驳回为 error
 * - order: 待发货为 warning，已发货/已送达为 processing，已完成为 success，已取消/已退款为 default
 * - aftersales: 待处理为 warning，已同意/退货中为 processing，已退款为 success，已拒绝为 error，已关闭为 default
 * - freightTemplate: 启用为 success，禁用为 default
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
    PendingReview: { label: '审核中', color: 'warning' },
    Pending: { label: '待审核', color: 'warning' },
    Active: { label: '正常', color: 'success' },
    Suspended: { label: '暂停', color: 'error' },
    Rejected: { label: '已驳回', color: 'error' },
    Closed: { label: '已关闭', color: 'default' },
  },
  user: {
    Active: { label: '正常', color: 'success' },
    Suspended: { label: '已锁定', color: 'error' },
    Locked: { label: '系统锁定', color: 'warning' },
  },
  oauth: {
    Enabled: { label: '已启用', color: 'success' },
    Disabled: { label: '已禁用', color: 'default' },
  },
  operator: {
    Active: { label: '在职', color: 'success' },
    Inactive: { label: '已停用', color: 'default' },
  },
  loginResult: {
    Success: { label: '成功', color: 'success' },
    Failed: { label: '失败', color: 'error' },
  },
  cacheType: {
    string: { label: 'string', color: 'processing' },
    hash: { label: 'hash', color: 'warning' },
    list: { label: 'list', color: 'default' },
    set: { label: 'set', color: 'success' },
    zset: { label: 'zset', color: 'error' },
  },
  menuType: {
    Directory: { label: '目录', color: 'processing' },
    Menu: { label: '菜单', color: 'success' },
    Button: { label: '按钮', color: 'default' },
  },
  onlineUser: {
    Normal: { label: '正常', color: 'success' },
    Anomaly: { label: '异常', color: 'error' },
  },
  product: {
    Draft: { label: '草稿', color: 'default' },
    PendingReview: { label: '待审核', color: 'warning' },
    Approved: { label: '已上架', color: 'success' },
    TakenDown: { label: '已下架', color: 'default' },
    Rejected: { label: '已驳回', color: 'error' },
  },
  order: {
    PendingShipment: { label: '待发货', color: 'warning' },
    Shipped: { label: '已发货', color: 'processing' },
    Delivered: { label: '已送达', color: 'processing' },
    Completed: { label: '已完成', color: 'success' },
    Cancelled: { label: '已取消', color: 'default' },
    Refunded: { label: '已退款', color: 'default' },
  },
  aftersales: {
    Pending: { label: '待处理', color: 'warning' },
    Approved: { label: '已同意', color: 'processing' },
    Rejected: { label: '已拒绝', color: 'error' },
    ReturnInProgress: { label: '退货中', color: 'processing' },
    Refunded: { label: '已退款', color: 'success' },
    Closed: { label: '已关闭', color: 'default' },
  },
  freightTemplate: {
    Enabled: { label: '启用', color: 'success' },
    Disabled: { label: '禁用', color: 'default' },
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
