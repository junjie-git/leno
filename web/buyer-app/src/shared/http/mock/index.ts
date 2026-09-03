import type { AxiosInstance } from 'axios'
import MockAdapter from 'axios-mock-adapter'
import { ensureSeedData, resetSeedData } from './data/seed'
import { registerAuthHandlers } from './handlers/auth'
import { registerProductHandlers } from './handlers/product'
import { registerCartHandlers } from './handlers/cart'
import { registerOrderHandlers } from './handlers/order'
import { registerPaymentHandlers } from './handlers/payment'
import { registerPromotionHandlers } from './handlers/promotion'
import { registerReviewHandlers } from './handlers/review'
import { registerAfterSalesHandlers } from './handlers/afterSales'
import { registerPointsHandlers } from './handlers/points'
import { registerMemberHandlers } from './handlers/member'
import { registerNotificationHandlers } from './handlers/notification'
import { registerUserCenterHandlers } from './handlers/userCenter'
import { registerPublicHandlers } from './handlers/public'

/**
 * 装配 MockAdapter
 *
 * - 启用条件：main.ts 中 DEV && VITE_USE_MOCK === 'true' 双重守卫后动态 import
 * - 覆盖买家端全部约 75 个端点（认证/商品/购物车/订单/支付/促销/评价/售后/积分/会员/通知/个人中心/公共）
 * - 未匹配请求透传到真实后端（mock.onAny().passThrough()）
 *
 * 生产环境保护：在非 dev 且未显式开启 mock 时直接抛错，避免误启用。
 */
export function setupMockAdapter(client: AxiosInstance): void {
  if (!import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'true') {
    throw new Error('Mock should not be loaded in production')
  }
  ensureSeedData()
  const mock = new MockAdapter(client, { delayResponse: 300 })

  // Mock 重置端点（仅开发联调用）
  mock.onPost('/mock/reset').reply(() => {
    resetSeedData()
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })

  // 认证与用户资料（Identity）
  registerAuthHandlers(mock)
  // 商品目录（Product）
  registerProductHandlers(mock)
  // 购物车（Cart）
  registerCartHandlers(mock)
  // 订单（Order）
  registerOrderHandlers(mock)
  // 支付（Payment）
  registerPaymentHandlers(mock)
  // 促销：优惠券 + 秒杀（Promotion）
  registerPromotionHandlers(mock)
  // 评价（Review）
  registerReviewHandlers(mock)
  // 售后（AfterSales）
  registerAfterSalesHandlers(mock)
  // 积分（Points）
  registerPointsHandlers(mock)
  // 会员（Membership）
  registerMemberHandlers(mock)
  // 通知（Notification + UserCenter 偏好）
  registerNotificationHandlers(mock)
  // 地址 / 收藏 / 浏览历史（UserCenter）
  registerUserCenterHandlers(mock)
  // 公告 / 字典（SystemAdmin 公开端点）
  registerPublicHandlers(mock)

  // 未匹配的请求透传到真实后端
  mock.onAny().passThrough()

  console.log('[Mock] buyer-app 已启用全量 handler，覆盖 14 个业务域端点（另含 mock/reset）')
}
