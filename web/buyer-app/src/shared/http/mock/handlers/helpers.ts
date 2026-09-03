import type MockAdapter from 'axios-mock-adapter'

/**
 * Mock handlers 公共工具
 */

/** 后端 ApiResponse 成功结构（与响应拦截器的解包逻辑对应） */
export function ok<T>(data: T): [number, { code: number; message: string; data: T }] {
  return [200, { code: 200, message: 'OK', data }]
}

/** 业务失败（HTTP 200 + code !== 200 → BusinessError） */
export function fail(code: number, message: string): [number, { code: number; message: string; data: null }] {
  return [200, { code, message, data: null }]
}

/** HTTP 错误（触发响应拦截器错误分支） */
export function httpError(status: number, message: string): [number, { message: string }] {
  return [status, { message }]
}

/** 解析请求体 JSON 字符串 */
export function parseBody<T>(body: unknown): T {
  if (typeof body === 'string') {
    return JSON.parse(body) as T
  }
  return body as T
}

/** 提取 axios config.params 中的查询参数 */
export function queryParams(config: {
  params?: Record<string, unknown>
}): Record<string, string | undefined> {
  const result: Record<string, string | undefined> = {}
  const params = config.params ?? {}
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      result[key] = String(value)
    }
  }
  return result
}

/** 分页切片 */
export function paginate<T>(items: T[], page = 1, pageSize = 10): { items: T[]; total: number; page: number; pageSize: number } {
  const p = Math.max(1, page)
  const size = Math.max(1, pageSize)
  return {
    items: items.slice((p - 1) * size, p * size),
    total: items.length,
    page: p,
    pageSize: size,
  }
}

/** 从 URL 正则捕获组中取路径参数 */
export function pathParam(match: RegExpMatchArray | null, group = 1): string {
  return match?.[group] ?? ''
}

export type Mock = MockAdapter
