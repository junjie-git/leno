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
 * 静态回退时 accountRoutes 不带前缀直接注入 BasicLayout children，
 * 因此 path 需写全相对路径（account/profile），最终 URL 为 /account/profile。
 */
export const accountRoutes: RouteRecordRaw[] = [
  {
    path: 'account/profile',
    name: 'account.profile',
    component: () => import('./views/Profile.vue'),
    meta: {
      title: '个人中心',
      menuKey: 'account.profile',
      icon: 'UserOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '06-account',
    },
  },
]
