import { describe, expect, it } from 'vitest'
import { fail, ok, paginate, parseBody, pathParam, queryParams } from '@/shared/http/mock/handlers/helpers'

describe('ok / fail（ApiResponse 信封）', () => {
  it('ok 返回 200 + code 200 + data', () => {
    const [status, body] = ok({ list: [1, 2] })
    expect(status).toBe(200)
    expect(body.code).toBe(200)
    expect(body.data).toEqual({ list: [1, 2] })
  })

  it('fail 返回 200 + 业务错误码', () => {
    const [status, body] = fail(40404, '请先勾选要结算的商品')
    expect(status).toBe(200)
    expect(body.code).toBe(40404)
    expect(body.message).toBe('请先勾选要结算的商品')
    expect(body.data).toBeNull()
  })
})

describe('parseBody', () => {
  it('JSON 字符串解析为对象', () => {
    expect(parseBody<{ a: number }>('{"a":1}')).toEqual({ a: 1 })
  })

  it('非字符串原样返回', () => {
    expect(parseBody({ b: 2 })).toEqual({ b: 2 })
  })
})

describe('queryParams', () => {
  it('过滤 undefined/null/空串并转为字符串', () => {
    const params = queryParams({
      params: {
        keyword: '短袖',
        categoryId: 'cat-2-1',
        brandId: undefined,
        shopId: null,
        sort: '',
        page: 2,
      },
    })
    expect(params).toEqual({ keyword: '短袖', categoryId: 'cat-2-1', page: '2' })
  })

  it('空 params 返回空对象', () => {
    expect(queryParams({})).toEqual({})
  })
})

describe('paginate', () => {
  const items = Array.from({ length: 25 }, (_, i) => i + 1)

  it('首页切片', () => {
    const result = paginate(items, 1, 10)
    expect(result.items).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])
    expect(result.total).toBe(25)
    expect(result.page).toBe(1)
    expect(result.pageSize).toBe(10)
  })

  it('末页不足一页', () => {
    const result = paginate(items, 3, 10)
    expect(result.items).toEqual([21, 22, 23, 24, 25])
  })

  it('页码与页大小下限保护', () => {
    expect(paginate(items, 0, 0).page).toBe(1)
    expect(paginate(items, 0, 0).pageSize).toBe(1)
  })

  it('越界页返回空数组但 total 不变', () => {
    const result = paginate(items, 99, 10)
    expect(result.items).toEqual([])
    expect(result.total).toBe(25)
  })
})

describe('pathParam', () => {
  it('从正则捕获组提取参数', () => {
    const match = '/products/spu-101'.match(/\/products\/(spu-\d+)$/)
    expect(pathParam(match)).toBe('spu-101')
    expect(pathParam(null)).toBe('')
  })
})
