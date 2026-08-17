import type { RouteRecordRaw } from 'vue-router'

/**
 * 06-payment-ops 支付运营模块路由
 *
 * 挂载于 BasicLayout 子路由，供 app/router.ts 以
 * `import paymentOpsRoutes from '@/modules/06-payment-ops/routes'` 聚合。
 * 访问角色：支付记录 / 退款记录 / 支付渠道配置为 Operator + Admin；
 * 渠道对账仅 Admin（md reconciliation §7：Controller 标 [Authorize(Roles = "Admin")]）。
 */
const paymentOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'payment-ops/payment-records',
    name: 'paymentOps.records',
    component: () => import('./views/PaymentRecords.vue'),
    meta: {
      title: '支付记录',
      menuKey: 'paymentOps.records',
      icon: 'PayCircleOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '06-payment-ops',
    },
  },
  {
    path: 'payment-ops/refund-records',
    name: 'paymentOps.refunds',
    component: () => import('./views/RefundRecords.vue'),
    meta: {
      title: '退款记录',
      menuKey: 'paymentOps.refunds',
      icon: 'RollbackOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '06-payment-ops',
    },
  },
  {
    path: 'payment-ops/payment-channels',
    name: 'paymentOps.channels',
    component: () => import('./views/PaymentChannels.vue'),
    meta: {
      title: '支付渠道配置',
      menuKey: 'paymentOps.channels',
      icon: 'ApiOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '06-payment-ops',
    },
  },
  {
    path: 'payment-ops/reconciliation',
    name: 'paymentOps.reconciliation',
    component: () => import('./views/Reconciliation.vue'),
    meta: {
      title: '渠道对账',
      menuKey: 'paymentOps.reconciliation',
      icon: 'ReconciliationOutlined',
      roles: ['Admin'],
      menuGroup: '06-payment-ops',
    },
  },
]

export default paymentOpsRoutes
