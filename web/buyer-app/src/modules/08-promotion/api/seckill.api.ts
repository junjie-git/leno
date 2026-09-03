import { client } from '@/shared/http'
import type {
  SeckillActivityDto,
  SeckillPlaceRequestDto,
} from '../types/promotion.dto'
import type { OrderDto } from '@/modules/06-order/types/order.dto'

/**
 * 秒杀 API（Promotion BC 买家端）
 *
 * - GET  /seckill/activities                  秒杀活动列表（首页入口）
 * - GET  /seckill/activities/{activityId}     秒杀活动详情
 * - POST /seckill/activities/{activityId}/place 秒杀下单
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const seckillApi = {
  /** 秒杀活动列表（含进行中 + 预告场次） */
  listActivities(): Promise<SeckillActivityDto[]> {
    return client.get<SeckillActivityDto[]>('/seckill/activities').then((r) => r.data)
  },

  /** 秒杀活动详情 */
  getActivity(activityId: string): Promise<SeckillActivityDto> {
    return client.get<SeckillActivityDto>(`/seckill/activities/${activityId}`).then((r) => r.data)
  },

  /** 秒杀下单（成功返回秒杀订单） */
  place(activityId: string, body: SeckillPlaceRequestDto): Promise<OrderDto> {
    return client.post<OrderDto>(`/seckill/activities/${activityId}/place`, body).then((r) => r.data)
  },
}
