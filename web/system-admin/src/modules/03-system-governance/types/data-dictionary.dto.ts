// web/system-admin/src/modules/03-system-governance/types/data-dictionary.dto.ts
// 数据字典 DTO 类型定义（对应后端 DataDictionariesController）

// 字典/字典项状态：Enabled 启用 / Disabled 停用
export type DictionaryStatus = 'Enabled' | 'Disabled'

// 字典项 DTO
export interface DictionaryItemDto {
  itemId: string
  code: string                   // 项编码，如 pending / paid / shipped
  displayName: string            // 显示名，如 待支付 / 已支付 / 已发货
  sortOrder: number              // 排序值，升序
  status: DictionaryStatus
}

// 数据字典响应 DTO
export interface DataDictionaryDto {
  dictionaryId: string
  code: string                   // 字典编码，如 order_status，编辑时只读
  name: string                   // 字典名称，如 订单状态
  description: string
  status: DictionaryStatus
  items: DictionaryItemDto[]     // 字典项列表
}

// 创建/更新字典请求 DTO（POST/PUT /admin/dictionaries[/{dictionaryId}]）
export interface SaveDataDictionaryDto {
  code: string
  name: string
  description: string
}

// 新增字典项请求 DTO（POST /admin/dictionaries/{dictionaryId}/items）
export interface AddDictionaryItemDto {
  code: string
  displayName: string
  sortOrder: number
}

// 更新字典项请求 DTO（PUT /admin/dictionaries/{dictionaryId}/items/{itemId}）
export interface UpdateDictionaryItemDto {
  code: string
  displayName: string
  sortOrder: number
}

// 列表查询参数（GET /admin/dictionaries）
export interface ListDataDictionariesParams {
  name?: string                  // 名称/编码模糊搜索
  status?: DictionaryStatus[]    // 状态多选
}
