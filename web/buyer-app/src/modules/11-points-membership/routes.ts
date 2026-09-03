import type { RouteRecordRaw } from 'vue-router'

/**
 * 11-points-membership 积分会员路由（7 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'points/account',
    name: 'points.account',
    component: () => import('./views/PointsAccount.vue'),
    meta: { title: '积分账户' },
  },
  {
    path: 'points/check-in',
    name: 'points.checkIn',
    component: () => import('./views/CheckIn.vue'),
    meta: { title: '每日签到' },
  },
  {
    path: 'points/ledger',
    name: 'points.ledger',
    component: () => import('./views/PointsLedger.vue'),
    meta: { title: '积分流水' },
  },
  {
    path: 'points/tasks',
    name: 'points.tasks',
    component: () => import('./views/TasksCenter.vue'),
    meta: { title: '任务中心' },
  },
  {
    path: 'points/exchange',
    name: 'points.exchange',
    component: () => import('./views/PointsExchange.vue'),
    meta: { title: '积分兑换' },
  },
  {
    path: 'member/level',
    name: 'member.level',
    component: () => import('./views/MemberLevel.vue'),
    meta: { title: '会员等级' },
  },
  {
    path: 'member/packages',
    name: 'member.packages',
    component: () => import('./views/MembershipPackages.vue'),
    meta: { title: '会员套餐' },
  },
]

export default routes
