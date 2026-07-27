// web/system-admin/src/modules/07-monitoring/routes.ts
// 07-monitoring 模块路由项：1 个视图，meta 含 title/menuKey/icon/roles/menuGroup
// 鉴权：Admin 与 Operator 角色均可访问（只读看板）
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
]

export default monitoringRoutes
