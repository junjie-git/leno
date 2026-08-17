import type { RouteRecordRaw } from 'vue-router'

/**
 * 09-account 个人中心模块路由
 *
 * 以 BasicLayout（path: '/'）children 形式定义，路径为相对路径：
 * - /account/todo          待办工作台（登录后默认首页，Operator/Admin）
 * - /account/profile       个人资料（已认证用户）
 * - /account/notifications 通知中心（已认证用户）
 *
 * meta 约定（与 app/router.ts、shared/layout/SiderMenu.vue 对齐）：
 * - title     文档标题与菜单显示名
 * - menuKey   菜单高亮键（全局唯一）
 * - menuGroup 侧栏菜单分组（'09-account' → 个人中心）
 * - roles     角色白名单，缺省表示仅需登录
 *
 * 注：/login 已由 app/router.ts 静态注册（name: account.login），此处不重复。
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'account/todo',
    name: 'account.todo',
    component: () => import('./views/TodoWorkbench.vue'),
    meta: {
      title: '待办工作台',
      menuKey: 'account.todo',
      menuGroup: '09-account',
      roles: ['Operator', 'Admin'],
    },
  },
  {
    path: 'account/profile',
    name: 'account.profile',
    component: () => import('./views/Profile.vue'),
    meta: {
      title: '个人资料',
      menuKey: 'account.profile',
      menuGroup: '09-account',
    },
  },
  {
    path: 'account/notifications',
    name: 'account.notifications',
    component: () => import('./views/Notifications.vue'),
    meta: {
      title: '通知中心',
      menuKey: 'account.notifications',
      menuGroup: '09-account',
    },
  },
]

export default routes
