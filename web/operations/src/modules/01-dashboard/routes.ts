import type { RouteRecordRaw } from 'vue-router'

/**
 * 01-dashboard 数据看板路由表（6 条，挂载在 BasicLayout children 下）
 *
 * 相对路径以 '/' 布局为根：dashboard/overview → /dashboard/overview。
 * meta：title 菜单标题 / menuKey 菜单键 / icon 菜单图标 / roles 可访问角色 / menuGroup 菜单分组。
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'dashboard/overview',
    name: 'dashboard.overview',
    component: () => import('./views/OperationsOverview.vue'),
    meta: {
      title: '运营总览',
      menuKey: 'dashboard.overview',
      icon: 'DashboardOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'dashboard/payment-stats',
    name: 'dashboard.paymentStats',
    component: () => import('./views/PaymentStats.vue'),
    meta: {
      title: '支付统计',
      menuKey: 'dashboard.paymentStats',
      icon: 'PayCircleOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'dashboard/points-stats',
    name: 'dashboard.pointsStats',
    component: () => import('./views/PointsStats.vue'),
    meta: {
      title: '积分统计',
      menuKey: 'dashboard.pointsStats',
      icon: 'GoldOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'dashboard/notification-delivery',
    name: 'dashboard.notificationDelivery',
    component: () => import('./views/NotificationDelivery.vue'),
    meta: {
      title: '通知送达率',
      menuKey: 'dashboard.notificationDelivery',
      icon: 'SoundOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'dashboard/after-sales-stats',
    name: 'dashboard.afterSalesStats',
    component: () => import('./views/AfterSalesStats.vue'),
    meta: {
      title: '售后统计',
      menuKey: 'dashboard.afterSalesStats',
      icon: 'CompassOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'dashboard/shop-ranking',
    name: 'dashboard.shopRanking',
    component: () => import('./views/ShopRanking.vue'),
    meta: {
      title: '店铺排行',
      menuKey: 'dashboard.shopRanking',
      icon: 'TrophyOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '01-dashboard',
    },
  },
]

export default routes
