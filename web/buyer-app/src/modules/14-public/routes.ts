import type { RouteRecordRaw } from 'vue-router'

/**
 * 14-public 公共路由（2 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'announcements',
    name: 'public.announcements',
    component: () => import('./views/Announcements.vue'),
    meta: { title: '平台公告' },
  },
  {
    path: 'dictionaries/:code',
    name: 'public.dictionaries',
    component: () => import('./views/Dictionaries.vue'),
    meta: { title: '字典数据' },
  },
]

export default routes
