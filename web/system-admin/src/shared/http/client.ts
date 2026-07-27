import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig, type AxiosError } from 'axios'
import {
  AppError,
  BusinessError,
  ConcurrencyError,
  ForbiddenError,
  NetworkError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  UnauthorizedError,
} from './errors'
import { generateIdempotencyKey } from './idempotency'

/**
 * 全局 axios 实例
 *
 * - baseURL: `/api`（Vite dev proxy 转发到后端 5001）
 * - timeout: 15s
 * - 请求拦截器：鉴权 / Idempotency-Key / X-Request-Id
 * - 响应拦截器：HTTP 层错误转换 → ApiResponse 解包 → 业务层错误转换
 */
export const client: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 15_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

/**
 * 读取持久化 AuthState 中的 token
 *
 * 这里直接读 localStorage 而非导入 useAuthStore，避免循环依赖：
 * useAuthStore 内部依赖 client（登录/拉 profile），client 又依赖 store 会形成环。
 */
function readTokenFromStorage(): string | null {
  try {
    const raw = localStorage.getItem('auth')
    if (!raw) return null
    const parsed = JSON.parse(raw) as { token?: string | null; expiresAt?: number | null }
    if (!parsed.token) return null
    if (typeof parsed.expiresAt === 'number' && parsed.expiresAt <= Date.now()) return null
    return parsed.token
  } catch {
    return null
  }
}

// 请求拦截器：鉴权 + traceId
client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  // 1. 鉴权
  const token = readTokenFromStorage()
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  // 2. traceId（X-Request-Id），便于后端日志关联
  if (!config.headers.has('X-Request-Id')) {
    config.headers.set('X-Request-Id', generateIdempotencyKey())
  }
  return config
})

/**
 * 从后端响应体提取 traceId
 */
function extractTraceId(data: unknown, headers: AxiosResponse['headers']): string | undefined {
  if (data && typeof data === 'object' && 'traceId' in data) {
    const t = (data as { traceId?: unknown }).traceId
    if (typeof t === 'string') return t
  }
  const headerTrace = headers?.['x-trace-id'] ?? headers?.['X-Trace-Id']
  if (typeof headerTrace === 'string') return headerTrace
  return undefined
}

/**
 * 从后端响应体提取 message
 */
function extractMessage(data: unknown, fallback: string): string {
  if (data && typeof data === 'object' && 'message' in data) {
    const m = (data as { message?: unknown }).message
    if (typeof m === 'string' && m.length > 0) return m
  }
  return fallback
}

// 响应拦截器：错误转换 + 数据解包
client.interceptors.response.use(
  (response: AxiosResponse) => {
    const traceId = extractTraceId(response.data, response.headers)
    // 业务层错误：HTTP 200 但 code !== 0
    if (response.data && typeof response.data === 'object' && 'code' in response.data) {
      const body = response.data as { code: number; message: string; data: unknown }
      if (body.code !== 0) {
        throw new BusinessError(body.code, body.message || '业务错误', traceId)
      }
      // 解包：调用方拿到的就是 data 字段
      response.data = body.data
    }
    return response
  },
  (error: AxiosError) => {
    // 网络层错误：无 response
    if (!error.response) {
      return Promise.reject(new NetworkError(error.message || '网络异常'))
    }

    const { status, data, headers } = error.response
    const traceId = extractTraceId(data, headers)
    const message = extractMessage(data, error.message)

    let appError: AppError
    switch (status) {
      case 401:
        appError = new UnauthorizedError(message, traceId)
        break
      case 403:
        appError = new ForbiddenError(message, traceId)
        break
      case 404:
        appError = new NotFoundError(message, traceId)
        break
      case 409: {
        const currentVersion =
          data && typeof data === 'object' && 'currentVersion' in data
            ? Number((data as { currentVersion: unknown }).currentVersion)
            : 0
        appError = new ConcurrencyError(message, currentVersion, traceId)
        break
      }
      case 429: {
        const retryAfterHeader = headers?.['retry-after'] ?? headers?.['Retry-After']
        const retryAfter = typeof retryAfterHeader === 'string' ? Number(retryAfterHeader) || 0 : 0
        appError = new RateLimitedError(message, retryAfter, traceId)
        break
      }
      default:
        if (status >= 500) {
          appError = new ServerError(message, traceId)
        } else {
          // 其他 4xx 归为业务错误
          appError = new BusinessError(status, message, traceId)
        }
    }
    return Promise.reject(appError)
  },
)

/**
 * 重新导出 withIdempotency，方便调用方从 client 模块统一引入
 */
export { withIdempotency } from './idempotency'
