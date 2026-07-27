// web/system-admin/src/modules/03-system-governance/api/data-dictionaries.api.ts
// 数据字典管理 API（SystemAdmin 域 DataDictionariesController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  DataDictionaryDto,
  SaveDataDictionaryDto,
  AddDictionaryItemDto,
  UpdateDictionaryItemDto,
  DictionaryItemDto,
  ListDataDictionariesParams,
} from '../types/data-dictionary.dto'

// 数据字典 API：list/create/update/enable/disable + 字典项 CRUD
export const dataDictionariesApi = {
  // 分页查询数据字典（含 items 列表）
  list: (params: ListDataDictionariesParams & PageQuery): Promise<PageResult<DataDictionaryDto>> =>
    client.get<PageResult<DataDictionaryDto>>('/admin/dictionaries', { params }).then((r) => r.data),

  // 创建数据字典（幂等）
  create: (body: SaveDataDictionaryDto): Promise<DataDictionaryDto> =>
    client.post<DataDictionaryDto>('/admin/dictionaries', body, withIdempotency()).then((r) => r.data),

  // 更新数据字典（编码不可变，幂等）
  update: (dictionaryId: string, body: SaveDataDictionaryDto): Promise<DataDictionaryDto> =>
    client.put<DataDictionaryDto>(`/admin/dictionaries/${dictionaryId}`, body, withIdempotency()).then((r) => r.data),

  // 启用字典（幂等）
  enable: (dictionaryId: string): Promise<DataDictionaryDto> =>
    client.post<DataDictionaryDto>(`/admin/dictionaries/${dictionaryId}/enable`, null, withIdempotency()).then((r) => r.data),

  // 停用字典（幂等）
  disable: (dictionaryId: string): Promise<DataDictionaryDto> =>
    client.post<DataDictionaryDto>(`/admin/dictionaries/${dictionaryId}/disable`, null, withIdempotency()).then((r) => r.data),

  // 新增字典项（幂等）
  addItem: (dictionaryId: string, body: AddDictionaryItemDto): Promise<DictionaryItemDto> =>
    client.post<DictionaryItemDto>(`/admin/dictionaries/${dictionaryId}/items`, body, withIdempotency()).then((r) => r.data),

  // 更新字典项（幂等）
  updateItem: (dictionaryId: string, itemId: string, body: UpdateDictionaryItemDto): Promise<DictionaryItemDto> =>
    client.put<DictionaryItemDto>(`/admin/dictionaries/${dictionaryId}/items/${itemId}`, body, withIdempotency()).then((r) => r.data),

  // 移除字典项（幂等，后端保证幂等）
  removeItem: (dictionaryId: string, itemId: string): Promise<void> =>
    client.delete<void>(`/admin/dictionaries/${dictionaryId}/items/${itemId}`).then((r) => r.data),
}
