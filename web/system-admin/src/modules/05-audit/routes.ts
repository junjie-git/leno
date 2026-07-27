// web/system-admin/src/modules/05-audit/routes.ts
// 05-audit 模块路由项：3 个视图，meta 含 title/menuKey/icon/roles/permission/menuGroup
import type { RouteRecordRaw } from 'vue-router'

export const auditRoutes: RouteRecordRaw[] = [
  {
    path: 'audit-logs',
    name: 'audit.audit-logs',
    component: () => import('./views/AuditLogs.vue'),
    meta: {
      title: '审计日志',
      menuKey: 'audit.audit-logs',
      icon: 'FileSearchOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'audit-log:read',
      menuGroup: '05-audit',
    },
  },
  {
    path: 'reconciliation',
    name: 'audit.reconciliation',
    component: () => import('./views/Reconciliation.vue'),
    meta: {
      title: '对账管理',
      menuKey: 'audit.reconciliation',
      icon: 'AuditOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'reconciliation:trigger',
      menuGroup: '05-audit',
    },
  },
  {
    path: 'outbox-monitor',
    name: 'audit.outbox-monitor',
    component: () => import('./views/OutboxMonitor.vue'),
    meta: {
      title: 'Outbox 监控',
      menuKey: 'audit.outbox-monitor',
      icon: 'InboxOutlined',
      roles: ['Admin'],
      permission: 'outbox:manage',
      menuGroup: '05-audit',
    },
  },
]

export default auditRoutes
