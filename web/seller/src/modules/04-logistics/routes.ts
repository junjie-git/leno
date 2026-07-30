import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/logistics/freight-templates',
    name: 'logistics.freight-templates',
    component: () => import('./views/FreightTemplates.vue'),
    meta: {
      title: '运费模板',
      menuKey: 'logistics.freight-templates',
      roles: ['Seller'],
      permission: 'freight-template:list',
      menuGroup: '04-logistics',
    },
  },
  {
    path: '/logistics/companies',
    name: 'logistics.companies',
    component: () => import('./views/LogisticsCompanies.vue'),
    meta: {
      title: '物流公司',
      menuKey: 'logistics.companies',
      roles: ['Seller'],
      permission: 'logistics-company:list',
      menuGroup: '04-logistics',
    },
  },
]

export default routes
