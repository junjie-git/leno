import type { PageQuery } from '@/shared/types'

/**
 * 03-promotion-ops 秒杀活动 DTO
 *
 * 对接后端 SeckillController（/api/admin/seckill/activities）：
 * - 状态机：Pending（待生效）→ Active（进行中）→ Closed（已关闭，终态）；激活与关闭均不可逆
 * - 激活时初始化各 SKU 的 Redis 库存；关闭时 Redis 剩余库存回写 DB
 */

/** 秒杀活动状态机：Pending → Active → Closed（激活与关闭均不可逆） */
export type SeckillStatus = 'Pending' | 'Active' | 'Closed'

/** 秒杀 SKU 配置（创建活动时提交） */
export interface SeckillSkuConfigDto {
  skuId: string
  skuName: string
  /** 原价（元，>0） */
  originalPrice: number
  /** 秒杀价（元，>0 且不高于原价） */
  seckillPrice: number
  /** 秒杀库存（≥1，激活时写入 Redis） */
  stock: number
  /** 每人限购（≥1） */
  perUserLimit: number
}

/** 秒杀 SKU 视图（含 Redis 库存初始化状态与剩余库存） */
export interface SeckillItemDto {
  skuId: string
  skuName: string
  seckillPrice: number
  originalPrice: number
  /** 秒杀库存（激活时初始化到 Redis 的总量） */
  stock: number
  /** 剩余库存（关闭前为 Redis 实时值） */
  remainingStock: number
  perUserLimit: number
  /** Redis 库存是否已初始化（激活后为 true，关闭回写 DB 后仍保留 true 标识已初始化过） */
  redisInitialized: boolean
}

/** 秒杀活动视图（GET /admin/seckill/activities 列表项） */
export interface SeckillActivityDto {
  id: string
  name: string
  status: SeckillStatus
  /** ISO 8601 UTC 字符串 */
  startTime: string
  /** ISO 8601 UTC 字符串 */
  endTime: string
  /** SKU 配置列表（≥1） */
  items: SeckillItemDto[]
  createdAt: string
}

/** 创建秒杀活动请求体（POST /admin/seckill/activities） */
export interface CreateSeckillActivityDto {
  name: string
  startTime: string
  endTime: string
  items: SeckillSkuConfigDto[]
}

/** GET /admin/seckill/activities 查询参数 */
export interface ListSeckillActivitiesParams extends PageQuery {
  /** 活动状态精确匹配 */
  status?: SeckillStatus
}
