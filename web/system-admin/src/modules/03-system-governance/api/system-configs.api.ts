// web/system-admin/src/modules/03-system-governance/api/system-configs.api.ts
// 系统配置管理 API（SystemAdmin 域 SystemConfigsController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  SystemConfigDto,
  SaveSystemConfigDto,
  SystemConfigRevealDto,
  SystemConfigGroupDto,
  ListSystemConfigsParams,
} from '../types/system-config.dto'

// 系统配置 API：list/groups/getByKey/create/update/enable/disable
export const systemConfigsApi = {
  // 分页查询系统配置（值掩码返回）
  list: (params: ListSystemConfigsParams & PageQuery): Promise<PageResult<SystemConfigDto>> =>
    client.get<PageResult<SystemConfigDto>>('/admin/system-configs', { params }).then((r) => r.data),

  // 获取全部配置分组（去重，含每组配置数）
  groups: (): Promise<SystemConfigGroupDto[]> =>
    client.get<SystemConfigGroupDto[]>('/admin/system-configs/groups').then((r) => r.data),

  // 按键获取配置明文（需 config:reveal 权限，仅 Admin）
  getByKey: (key: string): Promise<SystemConfigRevealDto> =>
    client.get<SystemConfigRevealDto>(`/admin/system-configs/by-key/${encodeURIComponent(key)}`).then((r) => r.data),

  // 创建系统配置（幂等）
  create: (body: SaveSystemConfigDto): Promise<SystemConfigDto> =>
    client.post<SystemConfigDto>('/admin/system-configs', body, withIdempotency()).then((r) => r.data),

  // 更新系统配置（键不可变，幂等）
  update: (configId: string, body: SaveSystemConfigDto): Promise<SystemConfigDto> =>
    client.put<SystemConfigDto>(`/admin/system-configs/${configId}`, body, withIdempotency()).then((r) => r.data),

  // 启用配置（幂等）
  enable: (configId: string): Promise<SystemConfigDto> =>
    client.post<SystemConfigDto>(`/admin/system-configs/${configId}/enable`, null, withIdempotency()).then((r) => r.data),

  // 停用配置（幂等）
  disable: (configId: string): Promise<SystemConfigDto> =>
    client.post<SystemConfigDto>(`/admin/system-configs/${configId}/disable`, null, withIdempotency()).then((r) => r.data),
}
