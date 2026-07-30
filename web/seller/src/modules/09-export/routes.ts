import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/export/sales',
    name: 'export.sales',
    component: () => import('./views/SalesExport.vue'),
    meta: {
      title: '销售报表',
      menuKey: 'export.sales',
      roles: ['Seller'],
      permission: 'export:sales',
      menuGroup: '09-export',
    },
  },
]

export default routes
