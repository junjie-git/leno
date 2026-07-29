import {
  createRouter,
  createWebHistory,
  type RouteRecordRaw,
  type Router,
} from 'vue-router'
import { message } from 'ant-design-vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { useShopStore } from '@/shared/shop'
import BasicLayout from '@/shared/layout/BasicLayout.vue'
import Forbidden from '@/shared/pages/Forbidden.vue'
import NotFound from '@/shared/pages/NotFound.vue'

// 模块路由
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
import account from '@/modules/08-account/routes'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'account.login',
    component: () => import('@/modules/08-account/views/Login.vue'),
    meta: { anonymous: true, title: '登录' },
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
      { path: '', redirect: '/dashboard/overview' },
      ...dashboard,
      ...product,
      ...order,
      ...afterSales,
      ...account,
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
  routes,
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  const shop = useShopStore()

  if (to.meta.anonymous) return true

  if (!auth.isAuthenticated) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  if (!auth.user) {
    try {
      await auth.fetchProfile()
      await shop.fetchMyShop()
    } catch {
      await auth.logout()
      return { path: '/login' }
    }
  }

  if (!auth.hasRole((to.meta.roles ?? []) as string[])) {
    return { path: '/403' }
  }

  if (to.meta.permission && !auth.hasPermission(to.meta.permission as string)) {
    return { path: '/403' }
  }

  if (to.meta.requiresActiveShop && !shop.canPublish) {
    message.warning('店铺当前状态不允许此操作，请先完成入驻或联系平台')
    return { path: '/shop/application' }
  }

  return true
})
