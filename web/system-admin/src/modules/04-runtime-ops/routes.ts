// web/system-admin/src/modules/04-runtime-ops/routes.ts
// 04-runtime-ops 模块路由项：6 个视图，meta 含 title/menuKey/icon/roles/permission/menuGroup
import type { RouteRecordRaw } from 'vue-router'

export const runtimeOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'rate-limit-rules',
    name: 'runtime-ops.rate-limit-rules',
    component: () => import('./views/RateLimitRules.vue'),
    meta: {
      title: '限流规则',
      menuKey: 'runtime-ops.rate-limit-rules',
      icon: 'ThunderboltOutlined',
      roles: ['Admin'],
      permission: 'rate-limit:write',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'index-rebuild',
    name: 'runtime-ops.index-rebuild',
    component: () => import('./views/IndexRebuild.vue'),
    meta: {
      title: '索引重建',
      menuKey: 'runtime-ops.index-rebuild',
      icon: 'DatabaseOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'index-rebuild:trigger',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'dead-letter-queue',
    name: 'runtime-ops.dead-letter-queue',
    component: () => import('./views/DeadLetterQueue.vue'),
    meta: {
      title: '死信队列',
      menuKey: 'runtime-ops.dead-letter-queue',
      icon: 'WarningOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'dead-letter:dispose',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'scheduled-tasks',
    name: 'runtime-ops.scheduled-tasks',
    component: () => import('./views/ScheduledTasks.vue'),
    meta: {
      title: '定时任务',
      menuKey: 'runtime-ops.scheduled-tasks',
      icon: 'ClockCircleOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'scheduled-task:write',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'health-monitoring',
    name: 'runtime-ops.health-monitoring',
    component: () => import('./views/HealthMonitoring.vue'),
    meta: {
      title: '健康监控',
      menuKey: 'runtime-ops.health-monitoring',
      icon: 'HeartOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'alert-management',
    name: 'runtime-ops.alert-management',
    component: () => import('./views/AlertManagement.vue'),
    meta: {
      title: '告警管理',
      menuKey: 'runtime-ops.alert-management',
      icon: 'BellOutlined',
      roles: ['Admin'],
      permission: 'alert:manage',
      menuGroup: '04-runtime-ops',
    },
  },
  {
    path: 'cache-monitor',
    name: 'runtime-ops.cache-monitor',
    component: () => import('./views/CacheMonitor.vue'),
    meta: {
      title: '缓存监控',
      menuKey: 'runtime-ops.cache-monitor',
      icon: 'DatabaseOutlined',
      roles: ['Admin'],
      permission: 'cache:read',
      menuGroup: '04-runtime-ops',
    },
  },
]

export default runtimeOpsRoutes
