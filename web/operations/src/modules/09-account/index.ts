/**
 * 09-account 个人中心模块桶导出（routes / api / types）
 */

// 路由：默认导出聚合为 accountRoutes，供 app/router.ts BasicLayout children 展开
export { default as accountRoutes } from './routes'

// API
export { authApi } from './api/auth.api'
export { profileApi } from './api/profile.api'
export { notificationApi } from './api/notification.api'
export {
  fetchTodoBoard,
  fetchPendingProducts,
  fetchPendingShops,
  fetchPendingAfterSales,
  fetchPendingReviews,
  fetchDeadLetterNotifications,
} from './api/todo.api'

// 类型
export * from './types/account.dto'
export * from './types/auth.dto'
