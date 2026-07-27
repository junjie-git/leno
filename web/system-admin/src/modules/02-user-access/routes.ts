// web/system-admin/src/modules/02-user-access/routes.ts

import type { RouteRecordRaw } from 'vue-router'

// 02-user-access 模块路由项（挂到 BasicLayout 子路由）
const userAccessRoutes: RouteRecordRaw[] = [
  {
    path: 'user-access/users',
    name: 'user-access.users',
    component: () => import('./views/UserManagement.vue'),
    meta: {
      title: '用户管理',
      menuKey: 'user-access.users',
      icon: 'UserOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'user:read',
      menuGroup: '02-user-access',
    },
  },
  {
    path: 'user-access/roles',
    name: 'user-access.roles',
    component: () => import('./views/RoleManagement.vue'),
    meta: {
      title: '角色管理',
      menuKey: 'user-access.roles',
      icon: 'SafetyOutlined',
      roles: ['Admin'],
      permission: 'role:read',
      menuGroup: '02-user-access',
    },
  },
  {
    path: 'user-access/oauth-clients',
    name: 'user-access.oauth-clients',
    component: () => import('./views/OAuthClients.vue'),
    meta: {
      title: 'OAuth 客户端',
      menuKey: 'user-access.oauth-clients',
      icon: 'SafetyOutlined',
      roles: ['Admin'],
      permission: 'oauth:read',
      menuGroup: '02-user-access',
    },
  },
  {
    path: 'user-access/operators',
    name: 'user-access.operators',
    component: () => import('./views/Operators.vue'),
    meta: {
      title: '运营人员',
      menuKey: 'user-access.operators',
      icon: 'TeamOutlined',
      roles: ['Admin', 'Operator'],
      permission: 'operator:read',
      menuGroup: '02-user-access',
    },
  },
]

// 默认导出，供 app/router.ts 以 `import userAccess from '@/modules/02-user-access/routes'` 聚合
export default userAccessRoutes
