import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/after-sales',
    name: 'aftersales.list',
    component: () => import('./views/AfterSalesList.vue'),
    meta: {
      title: '售后列表',
      menuKey: 'aftersales.list',
      roles: ['Seller'],
      permission: 'aftersales:list',
      menuGroup: '06-after-sales',
    },
  },
  {
    path: '/after-sales/:id',
    name: 'aftersales.detail',
    component: () => import('./views/AfterSalesDetail.vue'),
    meta: {
      title: '售后详情',
      menuKey: 'aftersales.detail',
      roles: ['Seller'],
      permission: 'aftersales:list',
      menuGroup: '06-after-sales',
    },
  },
]

export default routes
