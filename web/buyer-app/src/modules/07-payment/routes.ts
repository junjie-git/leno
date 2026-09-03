import type { RouteRecordRaw } from 'vue-router'

/**
 * 07-payment 支付路由（2 条，聚焦任务无 Tabbar）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'payment/initiate/:orderId',
    name: 'payment.initiate',
    component: () => import('./views/PaymentInitiate.vue'),
    meta: { title: '收银台' },
  },
  {
    path: 'payment/result/:orderId',
    name: 'payment.result',
    component: () => import('./views/PaymentResult.vue'),
    meta: { title: '支付结果' },
  },
]

export default routes
