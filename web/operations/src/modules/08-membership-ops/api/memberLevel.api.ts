import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  CreateMemberLevelDto,
  MemberLevelDto,
  UpdateMemberLevelDto,
} from '../types/memberLevel.dto'

/**
 * 会员等级 API
 *
 * 与 Membership 域（旧域 PointsMembership 双轨兜底）AdminMembersController 对接
 * （baseURL 已含 /api）。
 * - GET /admin/members/levels：全量列表，后端按等级编号升序返回
 * - POST/PUT /admin/members/levels*：管理端写操作（Operator, Admin）
 * - 启停为幂等写操作，自动携带 Idempotency-Key
 */
export const memberLevelApi = {
  /**
   * 查询全部会员等级（按等级编号升序）
   */
  list(): Promise<AxiosResponse<MemberLevelDto[]>> {
    return client.get<MemberLevelDto[]>('/admin/members/levels')
  },

  /**
   * 创建会员等级（等级编号由后端自动递增分配）
   */
  create(body: CreateMemberLevelDto): Promise<AxiosResponse<MemberLevelDto>> {
    return client.post<MemberLevelDto>('/admin/members/levels', body, withIdempotency())
  },

  /**
   * 更新会员等级（名称、成长值门槛、折扣率、权益说明、状态）
   */
  update(levelId: string, body: UpdateMemberLevelDto): Promise<AxiosResponse<MemberLevelDto>> {
    return client.put<MemberLevelDto>(`/admin/members/levels/${levelId}`, body)
  },

  /**
   * 启用会员等级（新会员可达到该等级）
   */
  enable(levelId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/members/levels/${levelId}/enable`, null, withIdempotency())
  },

  /**
   * 停用会员等级（已有该等级的会员不受影响，新会员不可达）
   */
  disable(levelId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/members/levels/${levelId}/disable`, null, withIdempotency())
  },
}
