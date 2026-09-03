import type { RouteRecordRaw } from 'vue-router'

/**
 * 13-profile 我的路由（6 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'profile',
    name: 'profile.home',
    component: () => import('./views/Profile.vue'),
    meta: { title: '我的', tabbar: true },
  },
  {
    path: 'profile/addresses',
    name: 'profile.addresses',
    component: () => import('./views/Addresses.vue'),
    meta: { title: '收货地址' },
  },
  {
    path: 'profile/security',
    name: 'profile.security',
    component: () => import('./views/Security.vue'),
    meta: { title: '账号安全' },
  },
  {
    path: 'profile/favorites',
    name: 'profile.favorites',
    component: () => import('./views/Favorites.vue'),
    meta: { title: '我的收藏' },
  },
  {
    path: 'profile/history',
    name: 'profile.history',
    component: () => import('./views/History.vue'),
    meta: { title: '浏览历史' },
  },
  {
    path: 'settings',
    name: 'profile.settings',
    component: () => import('./views/Settings.vue'),
    meta: { title: '设置' },
  },
]

export default routes
