// web/system-admin/src/modules/03-system-governance/routes.ts
// 03-system-governance 模块路由项：4 个视图，meta 含 title/menuKey/icon/roles/permission/menuGroup
import type { RouteRecordRaw } from 'vue-router'

export const systemGovernanceRoutes: RouteRecordRaw[] = [
  {
    path: 'feature-flags',
    name: 'system-governance.feature-flags',
    component: () => import('./views/FeatureFlags.vue'),
    meta: {
      title: '功能开关',
      menuKey: 'system-governance.feature-flags',
      icon: 'FlagOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'feature:read',
      menuGroup: '03-system-governance',
    },
  },
  {
    path: 'system-configs',
    name: 'system-governance.system-configs',
    component: () => import('./views/SystemConfigs.vue'),
    meta: {
      title: '系统配置',
      menuKey: 'system-governance.system-configs',
      icon: 'SettingOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'config:read',
      menuGroup: '03-system-governance',
    },
  },
  {
    path: 'data-dictionaries',
    name: 'system-governance.data-dictionaries',
    component: () => import('./views/DataDictionaries.vue'),
    meta: {
      title: '数据字典',
      menuKey: 'system-governance.data-dictionaries',
      icon: 'DatabaseOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'dictionary:read',
      menuGroup: '03-system-governance',
    },
  },
  {
    path: 'announcements',
    name: 'system-governance.announcements',
    component: () => import('./views/Announcements.vue'),
    meta: {
      title: '公告管理',
      menuKey: 'system-governance.announcements',
      icon: 'NotificationOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'announcement:read',
      menuGroup: '03-system-governance',
    },
  },
]

export default systemGovernanceRoutes
