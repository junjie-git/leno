import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  ActionReasonDto,
  QualificationDto,
  ShopDto,
  ShopQueryParams,
} from '../types/shop.dto'

/**
 * 店铺审核与治理 API
 *
 * 与 Shop 域 AdminShopsController 对接（baseURL 已含 /api）：
 * - GET  /admin/shops                                        分页查询店铺（入驻审核 / 店铺治理共用）
 * - GET  /admin/shops/{id}                                   店铺详情
 * - POST /admin/shops/{id}/approve                           审核通过入驻申请（Active）
 * - POST /admin/shops/{id}/reject                            驳回入驻申请（Rejected，reason 必填）
 * - POST /admin/shops/{id}/suspend                           暂停营业（Suspended，reason 必填）
 * - POST /admin/shops/{id}/resume                            恢复营业（Active）
 * - POST /admin/shops/{id}/close                             关闭店铺（Closed 终态，reason 必填）
 * - GET  /admin/shops/{id}/qualifications                    资质列表
 * - POST /admin/shops/{id}/qualifications/{qualId}/approve   资质审核通过
 * - POST /admin/shops/{id}/qualifications/{qualId}/reject    资质驳回（reason 必填）
 *
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载
 * （响应拦截器已完成 ApiResponse 信封解包）。
 * 状态机：PendingReview → Active/Rejected；Active ↔ Suspended → Closed（终态）。
 */
export const shopApi = {
  /** 分页查询店铺（入驻审核与治理列表共用，支持关键词 / 申请人 / 状态 / 类目筛选） */
  list(params: ShopQueryParams): Promise<AxiosResponse<PageResult<ShopDto>>> {
    return client.get<PageResult<ShopDto>>('/admin/shops', { params })
  },

  /** 店铺详情（含联系方式与资质明细） */
  get(id: string): Promise<AxiosResponse<ShopDto>> {
    return client.get<ShopDto>(`/admin/shops/${id}`)
  },

  /** 审核通过入驻申请（仅 PendingReview 可调用；店铺全部资质须先 Approved） */
  approve(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/shops/${id}/approve`, null, withIdempotency())
  },

  /** 驳回入驻申请（reason 必填，前端限制 5-200 字） */
  reject(id: string, body: ActionReasonDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/shops/${id}/reject`, body, withIdempotency())
  },

  /** 暂停店铺营业（仅 Active 可调用，reason 必填） */
  suspend(id: string, body: ActionReasonDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/shops/${id}/suspend`, body, withIdempotency())
  },

  /** 恢复店铺营业（仅 Suspended 可调用） */
  resume(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/shops/${id}/resume`, null, withIdempotency())
  },

  /** 关闭店铺（仅 Suspended 可调用；Closed 为终态不可逆，reason 必填） */
  close(id: string, body: ActionReasonDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/shops/${id}/close`, body, withIdempotency())
  },

  /** 查询店铺资质列表 */
  getQualifications(id: string): Promise<AxiosResponse<QualificationDto[]>> {
    return client.get<QualificationDto[]>(`/admin/shops/${id}/qualifications`)
  },

  /** 资质审核通过（单条复审） */
  approveQualification(id: string, qualId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(
      `/admin/shops/${id}/qualifications/${qualId}/approve`,
      null,
      withIdempotency(),
    )
  },

  /** 资质驳回（单条复审，reason 必填） */
  rejectQualification(
    id: string,
    qualId: string,
    body: ActionReasonDto,
  ): Promise<AxiosResponse<void>> {
    return client.post<void>(
      `/admin/shops/${id}/qualifications/${qualId}/reject`,
      body,
      withIdempotency(),
    )
  },
}
