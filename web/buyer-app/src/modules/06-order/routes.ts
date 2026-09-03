import type { RouteRecordRaw } from 'vue-router'

/**
 * 06-order 订单交易路由（5 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'order/create',
    name: 'order.create',
    component: () => import('./views/OrderCreate.vue'),
    meta: { title: '确认订单' },
  },
  {
    path: 'orders',
    name: 'order.list',
    component: () => import('./views/OrderList.vue'),
    meta: { title: '我的订单' },
  },
  {
    path: 'order/:id',
    name: 'order.detail',
    component: () => import('./views/OrderDetail.vue'),
    meta: { title: '订单详情' },
  },
  {
    path: 'order/:id/logistics',
    name: 'order.logisticsTrace',
    component: () => import('./views/LogisticsTrace.vue'),
    meta: { title: '物流跟踪' },
  },
  {
    path: 'seckill/order/:activityId',
    name: 'order.seckillOrder',
    component: () => import('./views/SeckillOrder.vue'),
    meta: { title: '秒杀下单' },
  },
]

export default routes
