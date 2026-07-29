import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/dashboard/overview',
    name: 'dashboard.overview',
    component: () => import('./views/Overview.vue'),
    meta: {
      title: '经营概览',
      menuKey: 'dashboard.overview',
      roles: ['Seller'],
      permission: 'dashboard:view',
      menuGroup: '02-dashboard',
    },
  },
  {
    path: '/dashboard/sales-trend',
    name: 'dashboard.sales-trend',
    component: () => import('./views/SalesTrend.vue'),
    meta: {
      title: '销售趋势',
      menuKey: 'dashboard.sales-trend',
      roles: ['Seller'],
      permission: 'dashboard:sales-trend',
      menuGroup: '02-dashboard',
    },
  },
  {
    path: '/dashboard/low-stock',
    name: 'dashboard.low-stock',
    component: () => import('./views/LowStockAlert.vue'),
    meta: {
      title: '库存预警',
      menuKey: 'dashboard.low-stock',
      roles: ['Seller'],
      permission: 'dashboard:low-stock',
      menuGroup: '02-dashboard',
    },
  },
]

export default routes
