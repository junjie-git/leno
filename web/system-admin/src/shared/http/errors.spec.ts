import { describe, it, expect } from 'vitest'
import {
  AppError,
  NetworkError,
  BusinessError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  ConcurrencyError,
} from './errors'

describe('shared/http/errors', () => {
  it('NetworkError 包含 kind 与 message', () => {
    const err = new NetworkError('网络异常')
    expect(err).toBeInstanceOf(AppError)
    expect(err.kind).toBe('NetworkError')
    expect(err.message).toBe('网络异常')
  })

  it('BusinessError 携带业务码', () => {
    const err = new BusinessError(40001, '账号已禁用', 'trace-1')
    expect(err.kind).toBe('BusinessError')
    expect(err.code).toBe(40001)
    expect(err.traceId).toBe('trace-1')
  })

  it('UnauthorizedError 默认消息', () => {
    const err = new UnauthorizedError()
    expect(err.kind).toBe('UnauthorizedError')
    expect(err.message).toBe('未登录或登录已过期')
  })

  it('ForbiddenError 默认消息', () => {
    const err = new ForbiddenError()
    expect(err.kind).toBe('ForbiddenError')
    expect(err.message).toBe('无权访问')
  })

  it('NotFoundError 接受自定义消息', () => {
    const err = new NotFoundError('规则不存在')
    expect(err.kind).toBe('NotFoundError')
    expect(err.message).toBe('规则不存在')
  })

  it('RateLimitedError 携带 retryAfter', () => {
    const err = new RateLimitedError('操作过于频繁', 30)
    expect(err.kind).toBe('RateLimitedError')
    expect(err.retryAfter).toBe(30)
  })

  it('ServerError 默认消息', () => {
    const err = new ServerError()
    expect(err.kind).toBe('ServerError')
    expect(err.message).toBe('服务器异常，请稍后重试')
  })

  it('ConcurrencyError 携带 currentVersion', () => {
    const err = new ConcurrencyError('资源已被他人修改', 4, 'trace-2')
    expect(err.kind).toBe('ConcurrencyError')
    expect(err.currentVersion).toBe(4)
    expect(err.traceId).toBe('trace-2')
  })

  it('所有错误可被 instanceof 区分', () => {
    const errors = [
      new NetworkError(),
      new BusinessError(1, 'x'),
      new UnauthorizedError(),
      new ForbiddenError(),
      new NotFoundError(),
      new RateLimitedError('x', 1),
      new ServerError(),
      new ConcurrencyError('x', 1),
    ]
    for (const e of errors) {
      expect(e).toBeInstanceOf(AppError)
      expect(e).toBeInstanceOf(Error)
    }
  })

  it('所有错误可被 throw/catch', () => {
    try {
      throw new BusinessError(40001, '账号已禁用')
    } catch (e) {
      expect(e).toBeInstanceOf(BusinessError)
      if (e instanceof BusinessError) {
        expect(e.code).toBe(40001)
      }
    }
  })
})
