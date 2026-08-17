import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  CreateMembershipPackageDto,
  MembershipPackageDto,
  MembershipPackageQueryParams,
  UpdateMembershipPackageDto,
} from '../types/membershipPackage.dto'

/**
 * 会员套餐 API
 *
 * - GET /membership-packages：共享字典（买家端订阅页与运营后台共用，可按状态过滤）
 * - POST/PUT /admin/membership-packages*：管理端写操作（Operator, Admin）
 * - 启停为幂等写操作，自动携带 Idempotency-Key
 */
export const membershipPackageApi = {
  /**
   * 查询会员套餐列表（共享字典，可过滤启用）
   */
  list(
    params?: MembershipPackageQueryParams,
  ): Promise<AxiosResponse<MembershipPackageDto[]>> {
    return client.get<MembershipPackageDto[]>('/membership-packages', { params })
  },

  /**
   * 创建会员套餐
   */
  create(body: CreateMembershipPackageDto): Promise<AxiosResponse<MembershipPackageDto>> {
    return client.post<MembershipPackageDto>('/admin/membership-packages', body, withIdempotency())
  },

  /**
   * 更新会员套餐（名称、价格、时长、关联等级、权益、状态）
   */
  update(
    packageId: string,
    body: UpdateMembershipPackageDto,
  ): Promise<AxiosResponse<MembershipPackageDto>> {
    return client.put<MembershipPackageDto>(`/admin/membership-packages/${packageId}`, body)
  },

  /**
   * 启用会员套餐（新用户可订阅）
   */
  enable(packageId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(
      `/admin/membership-packages/${packageId}/enable`,
      null,
      withIdempotency(),
    )
  },

  /**
   * 停用会员套餐（已订阅用户权益不受影响，新用户不可订阅）
   */
  disable(packageId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(
      `/admin/membership-packages/${packageId}/disable`,
      null,
      withIdempotency(),
    )
  },
}
