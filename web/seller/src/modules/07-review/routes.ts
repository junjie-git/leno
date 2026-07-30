import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/reviews',
    name: 'review.reply',
    component: () => import('./views/ReviewReply.vue'),
    meta: {
      title: '评价回复',
      menuKey: 'review.reply',
      roles: ['Seller'],
      permission: 'review:list',
      menuGroup: '07-review',
    },
  },
]

export default routes
