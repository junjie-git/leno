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
import authRoutes from '@/modules/01-auth/routes'
import homeRoutes from '@/modules/02-home/routes'
import catalogRoutes from '@/modules/03-catalog/routes'
import shopRoutes from '@/modules/04-shop/routes'
import cartRoutes from '@/modules/05-cart/routes'
import orderRoutes from '@/modules/06-order/routes'
import paymentRoutes from '@/modules/07-payment/routes'
import promotionRoutes from '@/modules/08-promotion/routes'
import reviewRoutes from '@/modules/09-review/routes'
import afterSalesRoutes from '@/modules/10-after-sales/routes'
import pointsMembershipRoutes from '@/modules/11-points-membership/routes'
import notificationRoutes from '@/modules/12-notification/routes'
import profileRoutes from '@/modules/13-profile/routes'
import publicRoutes from '@/modules/14-public/routes'

/**
 * 静态路由聚合（01-auth ~ 14-public 全部 14 个业务模块 + 5 个框架页）
 *
 * - BasicLayout 提供 app-shell（375px 基准）+ TabBar（仅 meta.tabbar 页显示）
 * - 登录态守卫：未登录访问受保护路由跳 /login 并携带 redirect query
 * - 已登录访问 /login 等匿名页跳首页
 */
const staticRoutes: RouteRecordRaw[] = [
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
      // 首页在首位（path: ''），随后按 01 → 14 顺序展开各业务模块路由
      ...homeRoutes,
      ...authRoutes,
      ...catalogRoutes,
      ...shopRoutes,
      ...cartRoutes,
      ...orderRoutes,
      ...paymentRoutes,
      ...promotionRoutes,
      ...reviewRoutes,
      ...afterSalesRoutes,
      ...pointsMembershipRoutes,
      ...notificationRoutes,
      ...profileRoutes,
      ...publicRoutes,
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
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) {
      return savedPosition
    }
    if (to.path !== from.path) {
      return { top: 0 }
    }
    return {}
  },
})

/**
 * 创建鉴权守卫
 *
 * 1. meta.anonymous 路由直接放行（登录/注册/商品评价等）
 * 2. 未登录跳 /login?redirect=to.fullPath
 * 3. 首次进入 user 为空时拉取 profile，失败登出并跳 /login
 */
export function createAuthGuard(): NavigationGuardWithThis<undefined> {
  return async (to, _from, next) => {
    const auth = useAuthStore()

    // 已登录访问登录/注册等匿名页 → 跳首页
    const anonymousAuthPages = ['/login', '/register', '/forgot-password']
    if (anonymousAuthPages.includes(to.path) && auth.isAuthenticated) {
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

    return next()
  }
}

router.beforeEach(createAuthGuard())

/** 路由标题同步到 document.title */
router.afterEach((to) => {
  const title = (to.meta.title as string | undefined) ?? ''
  document.title = title ? `${title} - Leno 买家端` : 'Leno 买家端'
})
