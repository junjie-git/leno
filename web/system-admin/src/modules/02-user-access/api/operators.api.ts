// web/system-admin/src/modules/02-user-access/api/operators.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  OperatorDto,
  ListOperatorsParams,
  SaveOperatorDto,
  AssignOperatorPermissionsDto,
} from '../types/operator.dto'

// 运营人员管理 API（SystemAdmin 域 OperatorsController）
export const operatorsApi = {
  // 分页查询运营人员
  list: (params: ListOperatorsParams & PageQuery) =>
    client.get<PageResult<OperatorDto>>('/admin/operators', { params }),

  // 按标识获取运营人员详情
  get: (operatorId: string) =>
    client.get<OperatorDto>(`/admin/operators/${operatorId}`),

  // 创建运营人员（幂等）
  create: (body: SaveOperatorDto) =>
    client.post<OperatorDto>('/admin/operators', body, withIdempotency()),

  // 更新运营人员权限（合并新增权限码，幂等）
  updatePermissions: (operatorId: string, body: AssignOperatorPermissionsDto) =>
    client.put<OperatorDto>(`/admin/operators/${operatorId}/permissions`, body, withIdempotency()),

  // 启用运营人员（幂等）
  activate: (operatorId: string) =>
    client.post<OperatorDto>(`/admin/operators/${operatorId}/activate`, null, withIdempotency()),

  // 停用运营人员（幂等）
  deactivate: (operatorId: string) =>
    client.post<OperatorDto>(`/admin/operators/${operatorId}/deactivate`, null, withIdempotency()),
}
