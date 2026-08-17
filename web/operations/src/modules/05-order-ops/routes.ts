import type { RouteRecordRaw } from 'vue-router'

/**
 * 05-order-ops 订单运营模块路由
 *
 * 挂载于 BasicLayout 子路由，供 app/router.ts 以
 * `import orderOpsRoutes from '@/modules/05-order-ops/routes'` 聚合。
 * 访问角色：Operator / Admin。
 */
const orderOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'order-ops/orders',
    name: 'orderOps.list',
    component: () => import('./views/OrderManagement.vue'),
    meta: {
      title: '订单管理',
      menuKey: 'orderOps.list',
      icon: 'OrderedListOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '05-order-ops',
    },
  },
  {
    path: 'order-ops/after-sales',
    name: 'orderOps.afterSales',
    component: () => import('./views/AfterSales.vue'),
    meta: {
      title: '售后处理',
      menuKey: 'orderOps.afterSales',
      icon: 'CustomerServiceOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '05-order-ops',
    },
  },
  {
    path: 'order-ops/review-audit',
    name: 'orderOps.reviews',
    component: () => import('./views/ReviewAudit.vue'),
    meta: {
      title: '评价审核',
      menuKey: 'orderOps.reviews',
      icon: 'StarOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '05-order-ops',
    },
  },
  {
    path: 'order-ops/logistics-companies',
    name: 'orderOps.logistics',
    component: () => import('./views/LogisticsCompanies.vue'),
    meta: {
      title: '物流公司',
      menuKey: 'orderOps.logistics',
      icon: 'CarOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '05-order-ops',
    },
  },
]

export default orderOpsRoutes
