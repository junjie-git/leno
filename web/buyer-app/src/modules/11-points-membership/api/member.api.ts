import { client } from '@/shared/http'
import type {
  MemberLevelInfoDto,
  MemberProfileDto,
  MembershipPackageDto,
  SubscribeResultDto,
} from '../types/member.dto'

/**
 * 会员 API（Membership 域 / 旧 PointsMembership 双轨兜底）
 *
 * - GET  /members/me                          我的会员信息
 * - GET  /members/levels                      会员等级体系（V1-V6）
 * - GET  /membership-packages                 付费会员套餐
 * - POST /membership-packages/{packageId}/subscribe 订阅套餐
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const memberApi = {
  /** 我的会员信息 */
  getMyMembership(): Promise<MemberProfileDto> {
    return client.get<MemberProfileDto>('/members/me').then((r) => r.data)
  },

  /** 会员等级体系 */
  listLevels(): Promise<MemberLevelInfoDto[]> {
    return client.get<MemberLevelInfoDto[]>('/members/levels').then((r) => r.data)
  },

  /** 付费会员套餐列表 */
  listPackages(): Promise<MembershipPackageDto[]> {
    return client.get<MembershipPackageDto[]>('/membership-packages').then((r) => r.data)
  },

  /** 订阅会员套餐 */
  subscribe(packageId: string): Promise<SubscribeResultDto> {
    return client.post<SubscribeResultDto>(`/membership-packages/${packageId}/subscribe`).then((r) => r.data)
  },
}
