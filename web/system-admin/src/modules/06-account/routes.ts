import type { RouteRecordRaw } from 'vue-router'

/**
 * 登录路由（顶层，匿名访问）
 *
 * spec §1.5：/login 为顶层路由，不挂在 BasicLayout 下。
 */
export const loginRoute: RouteRecordRaw = {
  path: '/login',
  name: 'account.login',
  component: () => import('./views/Login2fa.vue'),
  meta: {
    anonymous: true,
    title: '登录',
    menuKey: 'account.login',
  },
}

/**
 * 06-account 模块挂载在 BasicLayout 下的子路由
 *
 * Plan 1 范围内无 BasicLayout 子路由（profile/notifications 页面不在本 Plan）。
 */
export const accountRoutes: RouteRecordRaw[] = []
