import type { RouteRecordRaw } from 'vue-router'

/**
 * 07-notification-ops 通知运营模块路由
 *
 * 挂载于 BasicLayout 子路由，供 app/router.ts 以
 * `import notificationOpsRoutes from '@/modules/07-notification-ops/routes'` 聚合。
 * 访问角色：Operator / Admin。
 */
const notificationOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'notification-ops/templates',
    name: 'notificationOps.templates',
    component: () => import('./views/Templates.vue'),
    meta: {
      title: '通知模板',
      menuKey: 'notificationOps.templates',
      icon: 'FileTextOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '07-notification-ops',
    },
  },
  {
    path: 'notification-ops/records',
    name: 'notificationOps.records',
    component: () => import('./views/Records.vue'),
    meta: {
      title: '通知记录',
      menuKey: 'notificationOps.records',
      icon: 'MailOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '07-notification-ops',
    },
  },
  {
    path: 'notification-ops/config',
    name: 'notificationOps.config',
    component: () => import('./views/Config.vue'),
    meta: {
      title: '通知配置',
      menuKey: 'notificationOps.config',
      icon: 'SettingOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '07-notification-ops',
    },
  },
  {
    path: 'notification-ops/rate-limits',
    name: 'notificationOps.rateLimits',
    component: () => import('./views/RateLimits.vue'),
    meta: {
      title: '通知限流',
      menuKey: 'notificationOps.rateLimits',
      icon: 'ThunderboltOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '07-notification-ops',
    },
  },
  {
    path: 'notification-ops/dead-letters',
    name: 'notificationOps.deadLetters',
    component: () => import('./views/DeadLetters.vue'),
    meta: {
      title: '死信管理',
      menuKey: 'notificationOps.deadLetters',
      icon: 'WarningOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '07-notification-ops',
    },
  },
]

export default notificationOpsRoutes
