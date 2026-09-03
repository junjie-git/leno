import type { RouteRecordRaw } from 'vue-router'

/**
 * 08-promotion 优惠路由（2 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'coupons/available',
    name: 'promotion.couponsAvailable',
    component: () => import('./views/CouponsAvailable.vue'),
    meta: { title: '领券中心' },
  },
  {
    path: 'coupons/mine',
    name: 'promotion.myCoupons',
    component: () => import('./views/MyCoupons.vue'),
    meta: { title: '我的优惠券' },
  },
]

export default routes
