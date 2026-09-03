import type { RouteRecordRaw } from 'vue-router'

/**
 * 02-home 首页路由（Tabbar 首页入口；banner/秒杀入口为页面内嵌区块，无独立路由）
 */
const routes: RouteRecordRaw[] = [
  {
    path: '',
    name: 'home.feed',
    component: () => import('./views/HomeFeed.vue'),
    meta: { title: '首页', tabbar: true },
  },
]

export default routes
