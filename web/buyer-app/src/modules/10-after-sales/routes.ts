import type { RouteRecordRaw } from 'vue-router'

/**
 * 10-after-sales 售后路由（3 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'after-sales/apply/:orderLineId',
    name: 'afterSales.apply',
    component: () => import('./views/AfterSalesApply.vue'),
    meta: { title: '申请售后' },
  },
  {
    path: 'after-sales/mine',
    name: 'afterSales.mine',
    component: () => import('./views/MyAfterSales.vue'),
    meta: { title: '我的售后' },
  },
  {
    path: 'after-sales/:id',
    name: 'afterSales.detail',
    component: () => import('./views/AfterSalesDetail.vue'),
    meta: { title: '售后详情' },
  },
]

export default routes
