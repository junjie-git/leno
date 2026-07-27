import {
  createRouter,
  createWebHistory,
  type NavigationGuard,
  type RouteRecordRaw,
} from 'vue-router'
import { useAuthStore } from '@/shared/auth/auth.store'
import { loginRoute, accountRoutes } from '@/modules/06-account/routes'
import BasicLayout from '@/shared/layout/BasicLayout.vue'
import Forbidden from '@/shared/pages/Forbidden.vue'
import NotFound from '@/shared/pages/NotFound.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 创建鉴权守卫（spec §4.3）
 *
 * 1. 已登录访问 /login → 跳首页
 * 2. meta.anonymous 路由直接放行
 * 3. 未登录跳 /login?redirect=to.fullPath
 * 4. 首次进入 user 为空时拉取 profile，失败登出并跳 /login
 * 5. meta.roles 角色校验，不足跳 /403
 * 6. meta.permission 权限校验，不足跳 /403
 */
export function createAuthGuard(): NavigationGuard {
  return async (to) => {
    const auth = useAuthStore()

    if (to.path === '/login' && auth.isAuthenticated) {
      return { path: '/' }
    }

    if (to.meta.anonymous) {
      return true
    }

    if (!auth.isAuthenticated) {
      return { path: '/login', query: { redirect: to.fullPath } }
    }

    if (!auth.user) {
      try {
        await auth.fetchProfile()
      } catch (e) {
        logger.warn('fetchProfile 失败，登出并跳转登录', e)
        await auth.logout()
        return { path: '/login' }
      }
    }

    const requiredRoles = (to.meta.roles ?? []) as string[]
    if (requiredRoles.length > 0 && !auth.hasRole(requiredRoles)) {
      return { path: '/403' }
    }

    if (to.meta.permission && !auth.hasPermission(to.meta.permission as string)) {
      return { path: '/403' }
    }

    return true
  }
}

/**
 * 路由表（Plan 1 范围：06-account + 基础设施）
 */
const routes: RouteRecordRaw[] = [
  loginRoute,
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
    path: '/',
    component: BasicLayout,
    children: [
      { path: '', redirect: '/dashboard/operations-overview' },
      ...accountRoutes,
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'catch-all',
    component: NotFound,
    meta: { anonymous: true, title: '页面不存在' },
  },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

router.beforeEach(createAuthGuard())
