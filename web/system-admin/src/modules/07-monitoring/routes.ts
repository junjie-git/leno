// web/system-admin/src/modules/07-monitoring/routes.ts
// 07-monitoring 模块路由项：2 个视图，meta 含 title/menuKey/icon/roles/menuGroup
// 鉴权：
// - prometheus-dashboard：Admin 与 Operator 角色均可访问（只读看板）
// - server-monitor：仅 Admin 角色可访问（服务器实时监控，需 server-monitor:read 权限）
import type { RouteRecordRaw } from 'vue-router'

export const monitoringRoutes: RouteRecordRaw[] = [
  {
    path: 'prometheus-dashboard',
    name: 'monitoring.prometheus-dashboard',
    component: () => import('./views/PrometheusDashboard.vue'),
    meta: {
      title: 'Prometheus 监控看板',
      menuKey: 'monitoring.prometheus-dashboard',
      icon: 'MonitorOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '07-monitoring',
    },
  },
  {
    path: 'server-monitor',
    name: 'monitoring.server-monitor',
    component: () => import('./views/ServerMonitor.vue'),
    meta: {
      title: '服务器监控',
      menuKey: 'monitoring.server-monitor',
      icon: 'DesktopOutlined',
      roles: ['Admin'],
      permission: 'server-monitor:read',
      menuGroup: '07-monitoring',
    },
  },
]

export default monitoringRoutes
