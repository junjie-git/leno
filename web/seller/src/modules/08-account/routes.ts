import type { RouteRecordRaw } from 'vue-router'

export const accountRoutes: RouteRecordRaw[] = [
  {
    path: '/account/profile',
    name: 'account.profile',
    component: () => import('./views/Profile.vue'),
    meta: {
      title: '账号信息',
      menuKey: 'account.profile',
      roles: ['Seller'],
      permission: 'account:profile:view',
      menuGroup: '08-account',
    },
  },
  {
    path: '/account/notifications',
    name: 'account.notifications',
    component: () => import('./views/Notifications.vue'),
    meta: {
      title: '消息通知',
      menuKey: 'account.notifications',
      roles: ['Seller'],
      permission: 'notification:list',
      menuGroup: '08-account',
    },
  },
]

export default accountRoutes
