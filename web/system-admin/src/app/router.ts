import {
  createRouter,
  createWebHistory,
  type NavigationGuardWithThis,
  type RouteRecordRaw,
  type Router,
} from 'vue-router'
import { useAuthStore } from '@/shared/auth/auth.store'
import { useMenuStore } from '@/shared/menu'
import { buildDynamicRoutes } from '@/shared/router/dynamic-routes'
import BasicLayout from '@/shared/layout/BasicLayout.vue'
import Forbidden from '@/shared/pages/Forbidden.vue'
import NotFound from '@/shared/pages/NotFound.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 静态路由：始终注册，不参与动态菜单
 *
 * 包含 /login、/403、/404、BasicLayout 容器（children 初始为空）、catch-all。
 * BasicLayout children 在登录后由 auth-guard 动态注入。
 */
const staticRoutes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'account.login',
    component: () => import('@/modules/06-account/views/Login2fa.vue'),
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
    path: '/',
    name: 'basic',
    component: BasicLayout,
    children: [
      { path: '', redirect: '/dashboard/operations-overview' },
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
 * 创建鉴权守卫（spec §4.3）
 *
 * 1. 已登录访问 /login → 跳首页
 * 2. meta.anonymous 路由直接放行
 * 3. 未登录跳 /login?redirect=to.fullPath
 * 4. 首次进入 user 为空时拉取 profile，失败登出并跳 /login
 * 5. meta.roles 角色校验，不足跳 /403
 * 6. meta.permission 权限校验，不足跳 /403
 * 7. 动态菜单首次加载：fetchMenus → buildDynamicRoutes → addRoute → 重新匹配
 *    失败时回退静态路由聚合，避免黑屏
 *
 * 重入保护：使用 auth.menusLoaded 而非 menu.loaded 作为门控，确保成功与回退
 * 两条路径都能阻断重导航时的二次进入，避免死循环。
 */
export function createAuthGuard(router: Router): NavigationGuardWithThis<undefined> {
  return async (to, from, next) => {
    const auth = useAuthStore()
    const menu = useMenuStore()

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

    // 动态菜单首次加载：以 auth.menusLoaded 作门控，成功/回退均置位，避免重入死循环
    if (auth.dynamicMenuEnabled && !auth.menusLoaded) {
      try {
        await menu.fetchMenus()
        const routes = buildDynamicRoutes(menu.menus)
        routes.forEach((r) => router.addRoute('basic', r))
        auth.menusLoaded = true
        // 重新匹配目标路由
        if (!to.matched.length || to.matched[0].path === '/:pathMatch(.*)*') {
          return next({ ...to, replace: true })
        }
      } catch (e) {
        logger.warn('菜单加载失败，回退静态路由聚合', e)
        await loadStaticFallbackRoutes(router)
        auth.menusLoaded = true
        return next({ ...to, replace: true })
      }
    }

    return next()
  }
}

/**
 * 静态回退：菜单 API 失败时加载所有模块 routes.ts
 */
async function loadStaticFallbackRoutes(router: Router): Promise<void> {
  const dashboard = (await import('@/modules/01-dashboard/routes')).default
  const userAccess = (await import('@/modules/02-user-access/routes')).default
  const systemGovernance = (await import('@/modules/03-system-governance/routes')).default
  const runtimeOps = (await import('@/modules/04-runtime-ops/routes')).default
  const audit = (await import('@/modules/05-audit/routes')).default
  // 06-account 仅导出命名成员 accountRoutes（无 default），其余模块均有 default
  const account = (await import('@/modules/06-account/routes')).accountRoutes
  const monitoring = (await import('@/modules/07-monitoring/routes')).default

  const withPrefix = (prefix: string, routes: RouteRecordRaw[]): RouteRecordRaw[] =>
    routes.map((r) => ({ ...r, path: `${prefix}/${r.path}` }))

  const allRoutes: RouteRecordRaw[] = [
    ...account,
    ...withPrefix('dashboard', dashboard),
    ...userAccess,
    ...withPrefix('system-governance', systemGovernance),
    ...withPrefix('runtime-ops', runtimeOps),
    ...withPrefix('audit', audit),
    ...withPrefix('monitoring', monitoring),
  ]
  allRoutes.forEach((r) => router.addRoute('basic', r))
}

router.beforeEach(createAuthGuard(router))
