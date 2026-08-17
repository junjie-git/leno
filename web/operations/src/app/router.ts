import {
  createRouter,
  createWebHistory,
  type NavigationGuardWithThis,
  type RouteRecordRaw,
  type Router,
} from 'vue-router'
import { useAuthStore } from '@/shared/auth/auth.store'
import BasicLayout from '@/shared/layout/BasicLayout.vue'
import Forbidden from '@/shared/pages/Forbidden.vue'
import NotFound from '@/shared/pages/NotFound.vue'
import ServerError from '@/shared/pages/ServerError.vue'
import Maintenance from '@/shared/pages/Maintenance.vue'
import RateLimited from '@/shared/pages/RateLimited.vue'
import { logger } from '@/shared/utils/logger'
import dashboardRoutes from '@/modules/01-dashboard/routes'
import productOpsRoutes from '@/modules/02-product-ops/routes'
import promotionOpsRoutes from '@/modules/03-promotion-ops/routes'
import sellerOpsRoutes from '@/modules/04-seller-ops/routes'
import orderOpsRoutes from '@/modules/05-order-ops/routes'
import paymentOpsRoutes from '@/modules/06-payment-ops/routes'
import notificationOpsRoutes from '@/modules/07-notification-ops/routes'
import membershipOpsRoutes from '@/modules/08-membership-ops/routes'
import accountRoutes from '@/modules/09-account/routes'
import dataExportRoutes from '@/modules/10-data-export/routes'

/**
 * 静态路由（seller 风格静态聚合，无动态菜单）—— 路由聚合已完成
 *
 * 包含 /login、5 个框架页（403/404/500/维护/限流）、BasicLayout 容器与 catch-all。
 * BasicLayout children 已聚合 01-dashboard ~ 10-data-export 全部 10 个业务模块
 * 的 routes.ts（按模块编号顺序展开），侧栏菜单（SiderMenu）按 meta.menuGroup
 * 自动分组渲染，无需动态菜单。
 */
const staticRoutes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'account.login',
    component: () => import('@/modules/09-account/views/Login.vue'),
    meta: { anonymous: true, title: '登录', menuKey: 'account.login' },
  },
  {
    path: '/403',
    name: 'forbidden',
    component: Forbidden,
    meta: { anonymous: true, title: '无权访问' },
  },
  {
    path: '/404',
    name: 'not-found',
    component: NotFound,
    meta: { anonymous: true, title: '页面不存在' },
  },
  {
    path: '/500',
    name: 'server-error',
    component: ServerError,
    meta: { anonymous: true, title: '服务器错误' },
  },
  {
    path: '/maintenance',
    name: 'maintenance',
    component: Maintenance,
    meta: { anonymous: true, title: '系统维护中' },
  },
  {
    path: '/rate-limited',
    name: 'rate-limited',
    component: RateLimited,
    meta: { anonymous: true, title: '操作过于频繁' },
  },
  {
    path: '/',
    name: 'basic',
    component: BasicLayout,
    children: [
      // 默认首页重定向在前，随后按 01 → 10 顺序展开各业务模块路由
      { path: '', redirect: '/dashboard/overview' },
      ...dashboardRoutes,
      ...productOpsRoutes,
      ...promotionOpsRoutes,
      ...sellerOpsRoutes,
      ...orderOpsRoutes,
      ...paymentOpsRoutes,
      ...notificationOpsRoutes,
      ...membershipOpsRoutes,
      ...accountRoutes,
      ...dataExportRoutes,
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'catch-all',
    component: NotFound,
    meta: { anonymous: true, title: '页面不存在' },
  },
]

export const router: Router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: staticRoutes,
})

/**
 * 创建鉴权守卫（三层校验）
 *
 * 1. 已登录访问 /login → 跳首页
 * 2. meta.anonymous 路由直接放行
 * 3. 未登录跳 /login?redirect=to.fullPath
 * 4. 首次进入 user 为空时拉取 profile，失败登出并跳 /login
 * 5. meta.roles 角色校验（Operator/Admin），不足跳 /403
 * 6. meta.permission 权限校验，不足跳 /403
 */
export function createAuthGuard(): NavigationGuardWithThis<undefined> {
  return async (to, from, next) => {
    const auth = useAuthStore()

    if (to.path === '/login' && auth.isAuthenticated) {
      return next({ path: '/' })
    }

    if (to.meta.anonymous) {
      return next()
    }

    if (!auth.isAuthenticated) {
      return next({ path: '/login', query: { redirect: to.fullPath } })
    }

    if (!auth.user) {
      try {
        await auth.fetchProfile()
      } catch (e) {
        logger.warn('fetchProfile 失败，登出并跳转登录', e)
        await auth.logout()
        return next({ path: '/login' })
      }
    }

    const requiredRoles = (to.meta.roles ?? []) as string[]
    if (requiredRoles.length > 0 && !auth.hasRole(requiredRoles)) {
      return next({ path: '/403' })
    }

    if (to.meta.permission && !auth.hasPermission(to.meta.permission as string)) {
      return next({ path: '/403' })
    }

    return next()
  }
}

router.beforeEach(createAuthGuard())
