import { client } from '@/shared/http'
import type {
  CheckInResultDto,
  ExchangeCouponRequestDto,
  ExchangeCouponResultDto,
  PointsAccountDto,
  PointsLedgerEntryDto,
  PointsLedgerType,
  PointsTaskDto,
} from '../types/points.dto'

/**
 * 积分 API（Points 域 / 旧 PointsMembership 双轨兜底）
 *
 * - GET  /points/account                    积分账户
 * - GET  /points/ledger                     积分流水
 * - POST /points/check-in                   每日签到
 * - GET  /points/tasks                      任务中心
 * - POST /points/tasks/{taskId}/complete    完成任务
 * - POST /points/exchange-coupon            积分兑换优惠券
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const pointsApi = {
  /** 积分账户 */
  getAccount(): Promise<PointsAccountDto> {
    return client.get<PointsAccountDto>('/points/account').then((r) => r.data)
  },

  /** 积分流水（type 筛选，倒序） */
  getLedger(params?: { type?: PointsLedgerType }): Promise<PointsLedgerEntryDto[]> {
    return client.get<PointsLedgerEntryDto[]>('/points/ledger', { params }).then((r) => r.data)
  },

  /** 每日签到 */
  checkIn(): Promise<CheckInResultDto> {
    return client.post<CheckInResultDto>('/points/check-in').then((r) => r.data)
  },

  /** 任务中心 */
  listTasks(): Promise<PointsTaskDto[]> {
    return client.get<PointsTaskDto[]>('/points/tasks').then((r) => r.data)
  },

  /** 完成任务 */
  completeTask(taskId: string): Promise<PointsTaskDto> {
    return client.post<PointsTaskDto>(`/points/tasks/${taskId}/complete`).then((r) => r.data)
  },

  /** 积分兑换优惠券 */
  exchangeCoupon(body: ExchangeCouponRequestDto): Promise<ExchangeCouponResultDto> {
    return client.post<ExchangeCouponResultDto>('/points/exchange-coupon', body).then((r) => r.data)
  },
}
