import { describe, it, expect } from 'vitest'
import type { ApiResponse, PageResult, PageQuery, TableColumn } from './index'

describe('shared/types', () => {
  it('ApiResponse<T> 接受成功响应结构', () => {
    const resp: ApiResponse<string> = { code: 0, message: 'ok', data: 'hello', traceId: 't-1' }
    expect(resp.code).toBe(0)
    expect(resp.data).toBe('hello')
    expect(resp.traceId).toBe('t-1')
  })

  it('ApiResponse<T> 允许 data 为 null', () => {
    const resp: ApiResponse<unknown> = { code: 0, message: 'deleted', data: null }
    expect(resp.data).toBeNull()
  })

  it('PageResult<T> 包含 items 与分页字段', () => {
    const page: PageResult<number> = { items: [1, 2, 3], total: 3, page: 1, pageSize: 10 }
    expect(page.items).toHaveLength(3)
    expect(page.total).toBe(3)
  })

  it('PageQuery 允许缺省分页参数', () => {
    const query: PageQuery = {}
    expect(query.page).toBeUndefined()
    expect(query.pageSize).toBeUndefined()
  })

  it('TableColumn 必须包含 title 与 dataIndex', () => {
    const col: TableColumn = { title: '名称', dataIndex: 'name', width: 120 }
    expect(col.title).toBe('名称')
    expect(col.dataIndex).toBe('name')
  })
})
