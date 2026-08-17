/**
 * 04-seller-ops 卖家运营 DTO
 *
 * 字段与 docs/design-prompts/operations/04-seller-ops/*.md「数据模型与 API 对接」对齐：
 * - 店铺状态机：PendingReview → Active / Rejected；治理阶段 Active ↔ Suspended → Closed（终态不可逆）
 * - QualificationDto 覆盖营业执照 / 法人身份证 / 品牌授权三类资质的审核状态
 * - SellerStatsOverviewDto 为前端降级聚合结果（无独立后端端点，见 api/sellerStats.api.ts）
 */

import type { PageQuery } from '@/shared/types'

/** 店铺状态（以 md 为准：PendingReview/Active/Suspended/Closed + 审核分支 Rejected） */
export type ShopStatus = 'PendingReview' | 'Active' | 'Suspended' | 'Closed' | 'Rejected'

/** 资质审核状态 */
export type QualificationStatus = 'PendingReview' | 'Approved' | 'Rejected'

/** 资质类型（营业执照 / 法人身份证 / 品牌授权） */
export type QualificationType = '营业执照' | '法人身份证' | '品牌授权' | string

/**
 * 店铺 DTO
 *
 * - 列表端点返回摘要字段（含 qualifications 摘要，供资质前置校验）
 * - 详情端点返回完整字段（含联系方式 / 资质明细 / 治理时间）
 */
export interface ShopDto {
  id: string
  /** 店铺名称 */
  name: string
  /** 申请人（法人）姓名 */
  ownerName: string
  /** 卖家账号（登录账号） */
  sellerAccount: string
  /** 联系电话 */
  contactPhone?: string
  /** 主营类目 */
  mainCategory: string
  /** 在售商品数 */
  productCount: number
  /** 累计订单数 */
  orderCount: number
  /** 店铺评分（0-5） */
  rating: number
  /** 累计 GMV（治理抽屉经营指标用，列表可为空） */
  gmv?: number
  status: ShopStatus
  /** 入驻申请提交时间 */
  submittedAt: string
  /** 店铺档案创建时间（卖家统计「新增卖家」口径） */
  createdAt: string
  /** 最后治理时间 */
  lastGovernedAt?: string
  /** 资质列表（列表端点返回摘要，详情端点返回完整明细；统计端点可能缺省） */
  qualifications?: QualificationDto[]
}

/** 资质 DTO */
export interface QualificationDto {
  id: string
  /** 资质类型（营业执照 / 法人身份证 / 品牌授权） */
  type: QualificationType
  /** 资质文件名 */
  fileName: string
  /** 资质文件 URL（预览用） */
  fileUrl: string
  status: QualificationStatus
  /** 驳回原因（status=Rejected 时返回） */
  rejectReason?: string
  /** 提交时间 */
  submittedAt: string
}

/** 店铺列表查询参数（page/pageSize/keyword/status/category 与 md 对齐；applicant 为审核页扩展筛选） */
export interface ShopQueryParams extends PageQuery {
  /** 店铺名称关键词（模糊） */
  keyword?: string
  /** 申请人姓名（模糊） */
  applicant?: string
  status?: ShopStatus
  /** 主营类目 */
  category?: string
}

/** 审核与治理操作通用请求体（驳回 / 暂停 / 关闭必填 reason） */
export interface ActionReasonDto {
  /** 原因（必填，前端限制 5-200 字） */
  reason: string
}

/** 卖家统计查询参数（降级聚合：start/end 用于 shop-ranking 与新增卖家口径；category 透传店铺列表） */
export interface SellerStatsQueryParams {
  /** 统计起始时间（ISO 8601 UTC） */
  start: string
  /** 统计截止时间（ISO 8601 UTC） */
  end: string
  /** 主营类目筛选（可选） */
  category?: string
}

/** Top 卖家 GMV 柱状数据项 */
export interface SellerStatsTopShopDto {
  shopId: string
  shopName: string
  sellerAccount: string
  gmv: number
  orderCount: number
}

/** 类目分布聚合项 */
export interface SellerStatsCategoryDto {
  category: string
  count: number
}

/** 卖家明细表行（评分 <4.0 标记待治理） */
export interface SellerStatsSellerRowDto {
  shopId: string
  /** 店铺名称 */
  name: string
  /** 卖家账号 */
  sellerAccount: string
  category: string
  status: ShopStatus
  productCount: number
  orderCount: number
  /** 统计周期内 GMV（来自 shop-ranking 关联，未上榜店铺为 0） */
  gmv: number
  rating: number
  /** 评分 <4.0 待治理标记 */
  needsGovernance: boolean
}

/** 卖家统计聚合结果（前端降级聚合输出） */
export interface SellerStatsOverviewDto {
  /** 卖家总数（店铺列表 total） */
  totalSellers: number
  /** 活跃卖家数（Active 计数） */
  activeSellers: number
  /** 新增卖家数（createdAt 在 [start, end] 内计数） */
  newSellers: number
  /** 平均评分（店铺 rating 均值，1 位小数） */
  avgRating: number
  /** Top10 卖家 GMV 柱状数据（按 GMV 降序） */
  topShops: SellerStatsTopShopDto[]
  /** 类目分布聚合（按卖家数降序） */
  categoryDistribution: SellerStatsCategoryDto[]
  /** 卖家明细表数据 */
  items: SellerStatsSellerRowDto[]
}
