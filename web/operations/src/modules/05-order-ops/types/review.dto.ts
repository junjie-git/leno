import type { PageQuery } from '@/shared/types'

/**
 * 05-order-ops 评价审核 DTO
 *
 * 对接 Review 域 AdminReviewsController（旧域 ReviewAfterSales 双轨兜底，端点不变）：
 * - GET  /api/admin/reviews                    分页查询全平台评价
 * - POST /api/admin/reviews/{id}/approve       审核通过（隐藏可逆，可重新通过）
 * - POST /api/admin/reviews/{id}/hide          隐藏违规评价（ModerateReviewDto）
 *
 * 状态机：Pending（待审核）→ Approved（已通过）/ Hidden（已隐藏）；Hidden → Approved 可逆。
 */

/** 评价状态 */
export type ReviewStatus = 'Pending' | 'Approved' | 'Hidden'

/** 评价状态展示元数据（md §6 状态色：待审核橙 / 已通过绿 / 已隐藏灰） */
export const REVIEW_STATUS_META: Record<ReviewStatus, { label: string; color: string }> = {
  Pending: { label: '待审核', color: 'warning' },
  Approved: { label: '已通过', color: 'success' },
  Hidden: { label: '已隐藏', color: 'default' },
}

/** 隐藏原因分类：Spam 垃圾广告 / Abuse 辱骂 / Fake 虚假 / Other 其他 */
export type ReviewReasonCategory = 'Spam' | 'Abuse' | 'Fake' | 'Other'

/** 隐藏原因分类展示映射（radio 选项） */
export const REVIEW_REASON_CATEGORY_META: Record<ReviewReasonCategory, string> = {
  Spam: '垃圾广告',
  Abuse: '辱骂',
  Fake: '虚假',
  Other: '其他',
}

/** 评价视图（列表行与详情抽屉共用） */
export interface ReviewDto {
  id: string
  /** 评价全文 */
  content: string
  /** 评分 1-5 */
  rating: number
  /** 评价图片 URL 列表 */
  imageUrls: string[]
  productId: string
  /** 商品名称 */
  productName: string
  /** 买家用户 ID */
  userId: string
  /** 买家昵称，可选 */
  buyerName?: string
  /** 卖家回复内容，可选（无则列表展示「无」） */
  sellerReply?: string
  /** 卖家回复时间（ISO 8601），可选 */
  sellerRepliedAt?: string
  status: ReviewStatus
  /** 评价时间（ISO 8601） */
  createdAt: string
}

/** GET /api/admin/reviews 查询参数 */
export interface ReviewQueryParams extends PageQuery {
  /** 商品名称关键词 */
  productName?: string
  /** 评价状态 */
  status?: ReviewStatus
  /** 评分 1-5 */
  rating?: number
  /** 评价时间下界（ISO 8601 UTC） */
  fromTime?: string
  /** 评价时间上界（ISO 8601 UTC） */
  toTime?: string
}

/** 隐藏评价请求体（ModerateReviewDto）：reasonCategory 必选，remark 可选 */
export interface ModerateReviewDto {
  reasonCategory: ReviewReasonCategory
  /** 详细原因说明，可选 */
  remark?: string
}

/** 批量操作失败明细项 */
export interface BatchReviewFailureDto {
  id: string
  reason: string
}

/** 批量操作汇总结果（前端串行循环单条接口聚合） */
export interface BatchReviewResultDto {
  total: number
  succeeded: number
  failed: number
  failures: BatchReviewFailureDto[]
}
