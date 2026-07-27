import type { RouteRecordRaw } from 'vue-router'

// 01-dashboard 模块路由表（7 条，挂载在 BasicLayout children 下）
const routes: RouteRecordRaw[] = [
  {
    path: 'operations-overview',
    name: 'dashboard.operations-overview',
    component: () => import('./views/OperationsOverview.vue'),
    meta: {
      title: '运营总览',
      menuKey: 'dashboard.operations-overview',
      icon: 'DashboardOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'payment-stats',
    name: 'dashboard.payment-stats',
    component: () => import('./views/PaymentStats.vue'),
    meta: {
      title: '支付统计',
      menuKey: 'dashboard.payment-stats',
      icon: 'PayCircleOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'points-stats',
    name: 'dashboard.points-stats',
    component: () => import('./views/PointsStats.vue'),
    meta: {
      title: '积分统计',
      menuKey: 'dashboard.points-stats',
      icon: 'GiftOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'notification-delivery',
    name: 'dashboard.notification-delivery',
    component: () => import('./views/NotificationDelivery.vue'),
    meta: {
      title: '通知送达率',
      menuKey: 'dashboard.notification-delivery',
      icon: 'NotificationOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'after-sales-stats',
    name: 'dashboard.after-sales-stats',
    component: () => import('./views/AfterSalesStats.vue'),
    meta: {
      title: '售后统计',
      menuKey: 'dashboard.after-sales-stats',
      icon: 'RollbackOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'shop-ranking',
    name: 'dashboard.shop-ranking',
    component: () => import('./views/ShopRanking.vue'),
    meta: {
      title: '店铺排行',
      menuKey: 'dashboard.shop-ranking',
      icon: 'ShopOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'report-snapshots',
    name: 'dashboard.report-snapshots',
    component: () => import('./views/ReportSnapshots.vue'),
    meta: {
      title: '报表快照',
      menuKey: 'dashboard.report-snapshots',
      icon: 'FileTextOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
]

export default routes
