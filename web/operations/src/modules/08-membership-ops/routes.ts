import type { RouteRecordRaw } from 'vue-router'
import { CrownOutlined, GiftOutlined, GoldOutlined } from '@ant-design/icons-vue'

/**
 * 08-membership-ops 会员运营模块路由
 *
 * 菜单组：会员运营（menuGroup: '08-membership-ops'）
 * 访问角色：Operator / Admin
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/membership-ops/levels',
    name: 'membershipOps.levels',
    component: () => import('./views/MemberLevels.vue'),
    meta: {
      title: '会员等级',
      menuKey: 'membershipOps.levels',
      icon: CrownOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '08-membership-ops',
    },
  },
  {
    path: '/membership-ops/packages',
    name: 'membershipOps.packages',
    component: () => import('./views/MembershipPackages.vue'),
    meta: {
      title: '会员套餐',
      menuKey: 'membershipOps.packages',
      icon: GiftOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '08-membership-ops',
    },
  },
  {
    path: '/membership-ops/points-rules',
    name: 'membershipOps.pointsRules',
    component: () => import('./views/PointsRules.vue'),
    meta: {
      title: '积分规则',
      menuKey: 'membershipOps.pointsRules',
      icon: GoldOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '08-membership-ops',
    },
  },
]

export default routes
