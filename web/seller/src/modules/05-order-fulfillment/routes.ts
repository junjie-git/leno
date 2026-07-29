import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/orders/pending-shipment',
    name: 'order.pending-shipment',
    component: () => import('./views/PendingShipment.vue'),
    meta: {
      title: '待发货',
      menuKey: 'order.pending-shipment',
      roles: ['Seller'],
      permission: 'order:list',
      menuGroup: '05-order-fulfillment',
    },
  },
  {
    path: '/orders',
    name: 'order.list',
    component: () => import('./views/OrderList.vue'),
    meta: {
      title: '订单列表',
      menuKey: 'order.list',
      roles: ['Seller'],
      permission: 'order:list',
      menuGroup: '05-order-fulfillment',
    },
  },
  {
    path: '/orders/:id/trace',
    name: 'order.trace',
    component: () => import('./views/LogisticsTrace.vue'),
    meta: {
      title: '物流轨迹',
      roles: ['Seller'],
      permission: 'order:trace:view',
    },
  },
]

export default routes
