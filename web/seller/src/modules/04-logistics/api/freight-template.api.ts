import { http, withIdempotency } from '@/shared/http'
import type {
  FreightTemplateDto,
  CreateFreightTemplateDto,
  UpdateFreightRulesDto,
} from '../types/freight-template.dto'

/**
 * 运费模板 API 客户端
 *
 * 与后端 FreightTemplateController 对接（响应拦截器已解包 ApiResponse.data，
 * 调用方拿到的就是业务负载）：
 * - GET    /seller/freight-templates/mine        查询当前卖家运费模板列表
 * - POST   /seller/freight-templates            创建运费模板（幂等）
 * - PUT    /seller/freight-templates/{id}/rules 更新区域规则（version 乐观锁）
 * - POST   /seller/freight-templates/{id}/enable  启用模板（幂等）
 * - POST   /seller/freight-templates/{id}/disable 停用模板（幂等）
 */
export const freightTemplateApi = {
  /** 查询当前卖家运费模板列表 */
  listMine(): Promise<FreightTemplateDto[]> {
    return http
      .get<FreightTemplateDto[]>('/seller/freight-templates/mine')
      .then((r) => r.data)
  },

  /** 创建运费模板 */
  create(body: CreateFreightTemplateDto): Promise<FreightTemplateDto> {
    return http
      .post<FreightTemplateDto>('/seller/freight-templates', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 更新区域规则（整体替换，带 version 乐观锁） */
  updateRules(id: string, body: UpdateFreightRulesDto): Promise<FreightTemplateDto> {
    return http
      .put<FreightTemplateDto>(`/seller/freight-templates/${id}/rules`, body)
      .then((r) => r.data)
  },

  /** 启用模板 */
  enable(id: string): Promise<void> {
    return http
      .post<void>(`/seller/freight-templates/${id}/enable`, {}, withIdempotency())
      .then((r) => r.data)
  },

  /** 停用模板 */
  disable(id: string): Promise<void> {
    return http
      .post<void>(`/seller/freight-templates/${id}/disable`, {}, withIdempotency())
      .then((r) => r.data)
  },
}
