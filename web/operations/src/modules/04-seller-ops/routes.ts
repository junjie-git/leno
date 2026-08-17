import type { RouteRecordRaw } from 'vue-router'

/**
 * 04-seller-ops 卖家运营模块路由
 *
 * 挂载于 BasicLayout 子路由，供 app/router.ts 以
 * `import sellerOpsRoutes from '@/modules/04-seller-ops/routes'` 聚合。
 * 访问角色：Operator / Admin。
 */
const sellerOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'seller-ops/application-audit',
    name: 'sellerOps.audit',
    component: () => import('./views/ApplicationAudit.vue'),
    meta: {
      title: '入驻审核',
      menuKey: 'sellerOps.audit',
      icon: 'AuditOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '04-seller-ops',
    },
  },
  {
    path: 'seller-ops/shop-governance',
    name: 'sellerOps.governance',
    component: () => import('./views/ShopGovernance.vue'),
    meta: {
      title: '店铺治理',
      menuKey: 'sellerOps.governance',
      icon: 'ShopOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '04-seller-ops',
    },
  },
  {
    path: 'seller-ops/seller-statistics',
    name: 'sellerOps.statistics',
    component: () => import('./views/SellerStatistics.vue'),
    meta: {
      title: '卖家统计',
      menuKey: 'sellerOps.statistics',
      icon: 'BarChartOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '04-seller-ops',
    },
  },
]

export default sellerOpsRoutes
