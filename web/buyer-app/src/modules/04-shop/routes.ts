import type { RouteRecordRaw } from 'vue-router'

/**
 * 04-shop 店铺路由（1 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'shop/:shopId',
    name: 'shop.detail',
    component: () => import('./views/ShopDetail.vue'),
    meta: { title: '店铺详情' },
  },
]

export default routes
