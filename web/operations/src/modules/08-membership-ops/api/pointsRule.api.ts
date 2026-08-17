import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  AwardPointsDto,
  CreatePointsRuleDto,
  PointsRuleDto,
  UpdatePointsRuleDto,
} from '../types/pointsRule.dto'

/**
 * 积分规则 API
 *
 * 与 Points 域 PointsRulesController / AwardPointsController 对接（baseURL 已含 /api）。
 * - GET /admin/points/rules：全量规则列表
 * - POST/PUT /admin/points/rules*：规则写操作（Operator, Admin）
 * - POST /admin/points/award：运营手动发放积分（不可撤销，强制二次确认）
 * - 规则编码重复时后端返回 409，message 透出「规则编码已存在」
 */
export const pointsRuleApi = {
  /**
   * 查询全部积分规则
   */
  list(): Promise<AxiosResponse<PointsRuleDto[]>> {
    return client.get<PointsRuleDto[]>('/admin/points/rules')
  },

  /**
   * 创建积分规则（编码唯一，重复时后端 409）
   */
  create(body: CreatePointsRuleDto): Promise<AxiosResponse<PointsRuleDto>> {
    return client.post<PointsRuleDto>('/admin/points/rules', body, withIdempotency())
  },

  /**
   * 更新积分规则（编码不可修改）
   */
  update(ruleId: string, body: UpdatePointsRuleDto): Promise<AxiosResponse<PointsRuleDto>> {
    return client.put<PointsRuleDto>(`/admin/points/rules/${ruleId}`, body)
  },

  /**
   * 启用积分规则
   */
  enable(ruleId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/points/rules/${ruleId}/enable`, null, withIdempotency())
  },

  /**
   * 停用积分规则（该行为不再发放积分）
   */
  disable(ruleId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/points/rules/${ruleId}/disable`, null, withIdempotency())
  },

  /**
   * 运营手动发放积分（发放后不可撤销）
   */
  award(body: AwardPointsDto): Promise<AxiosResponse<void>> {
    return client.post<void>('/admin/points/award', body, withIdempotency())
  },
}
