import type { RouteRecordRaw } from 'vue-router'

/**
 * 12-notification 通知路由（2 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'notifications',
    name: 'notification.list',
    component: () => import('./views/Notifications.vue'),
    meta: { title: '消息中心' },
  },
  {
    path: 'notifications/preferences',
    name: 'notification.preferences',
    component: () => import('./views/Preferences.vue'),
    meta: { title: '通知偏好' },
  },
]

export default routes
