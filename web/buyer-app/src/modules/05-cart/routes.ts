import type { RouteRecordRaw } from 'vue-router'

/**
 * 05-cart 购物车路由（3 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'cart',
    name: 'cart.list',
    component: () => import('./views/Cart.vue'),
    meta: { title: '购物车', tabbar: true },
  },
  {
    path: 'checkout/preview',
    name: 'cart.checkoutPreview',
    component: () => import('./views/CheckoutPreview.vue'),
    meta: { title: '确认订单' },
  },
  {
    path: 'checkout/settle',
    name: 'cart.checkoutSettle',
    component: () => import('./views/CheckoutSettle.vue'),
    meta: { title: '结算确认' },
  },
]

export default routes
