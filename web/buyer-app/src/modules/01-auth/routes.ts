import type { RouteRecordRaw } from 'vue-router'

/**
 * 01-auth 认证路由（5 条，全部匿名可访问）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'login',
    name: 'auth.login',
    component: () => import('./views/Login.vue'),
    meta: { anonymous: true, title: '登录' },
  },
  {
    path: 'register',
    name: 'auth.register',
    component: () => import('./views/Register.vue'),
    meta: { anonymous: true, title: '注册' },
  },
  {
    path: 'forgot-password',
    name: 'auth.forgotPassword',
    component: () => import('./views/ForgotPassword.vue'),
    meta: { anonymous: true, title: '忘记密码' },
  },
  {
    path: 'oauth/:provider',
    name: 'auth.oauthLogin',
    component: () => import('./views/OauthLogin.vue'),
    meta: { anonymous: true, title: '三方登录' },
  },
  {
    path: 'two-factor',
    name: 'auth.twoFactor',
    component: () => import('./views/TwoFactor.vue'),
    meta: { anonymous: true, title: '双因子验证' },
  },
]

export default routes
