import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  CreateSeckillActivityDto,
  ListSeckillActivitiesParams,
  SeckillActivityDto,
} from '../types/seckill.dto'

/**
 * 秒杀活动 API
 *
 * 与后端 SeckillController（/api/admin/seckill/activities）对接：
 * - GET  /admin/seckill/activities                          分页查询（状态过滤）
 * - POST /admin/seckill/activities                          创建活动（待生效态，含 SKU 配置数组，幂等）
 * - POST /admin/seckill/activities/{activityId}/activate    激活（初始化各 SKU 的 Redis 库存，幂等）
 * - POST /admin/seckill/activities/{activityId}/close       关闭（Redis 剩余库存回写 DB，幂等，不可逆）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const seckillApi = {
  /**
   * 分页查询秒杀活动
   */
  list(params: ListSeckillActivitiesParams): Promise<PageResult<SeckillActivityDto>> {
    return client
      .get<PageResult<SeckillActivityDto>>('/seckill/activities', { params })
      .then((r) => r.data)
  },

  /**
   * 创建秒杀活动（幂等）：提交 SKU 配置数组，初始状态 Pending（待生效）
   */
  create(body: CreateSeckillActivityDto): Promise<SeckillActivityDto> {
    return client
      .post<SeckillActivityDto>('/seckill/activities', body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 更新秒杀活动（幂等，仅待生效态可更新）：整体替换 SKU 配置数组
   */
  update(activityId: string, body: CreateSeckillActivityDto): Promise<SeckillActivityDto> {
    return client
      .put<SeckillActivityDto>(`/seckill/activities/${activityId}`, body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 激活秒杀活动（幂等）：Pending → Active，激活时初始化全部 SKU 的 Redis 库存
   */
  activate(activityId: string): Promise<void> {
    return client
      .post<void>(`/seckill/activities/${activityId}/activate`, null, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 关闭秒杀活动（幂等，不可逆）：Active → Closed，关闭时 Redis 剩余库存回写 DB
   */
  close(activityId: string): Promise<void> {
    return client
      .post<void>(`/seckill/activities/${activityId}/close`, null, withIdempotency())
      .then((r) => r.data)
  },
}
