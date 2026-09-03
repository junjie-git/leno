import type { RouteRecordRaw } from 'vue-router'

/**
 * 09-review 评价路由（3 条；商品评价匿名可访问）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'review/submit/:orderLineId',
    name: 'review.submit',
    component: () => import('./views/ReviewSubmit.vue'),
    meta: { title: '提交评价' },
  },
  {
    path: 'reviews/mine',
    name: 'review.mine',
    component: () => import('./views/MyReviews.vue'),
    meta: { title: '我的评价' },
  },
  {
    path: 'product/:spuId/reviews',
    name: 'review.productReviews',
    component: () => import('./views/ProductReviews.vue'),
    meta: { title: '商品评价', anonymous: true },
  },
]

export default routes
