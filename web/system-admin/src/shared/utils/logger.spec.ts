import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { logger } from './logger'

describe('shared/utils/logger', () => {
  let consoleSpy: { log: ReturnType<typeof vi.spyOn>; info: ReturnType<typeof vi.spyOn>; warn: ReturnType<typeof vi.spyOn>; error: ReturnType<typeof vi.spyOn> }

  beforeEach(() => {
    consoleSpy = {
      log: vi.spyOn(console, 'log').mockImplementation(() => {}),
      info: vi.spyOn(console, 'info').mockImplementation(() => {}),
      warn: vi.spyOn(console, 'warn').mockImplementation(() => {}),
      error: vi.spyOn(console, 'error').mockImplementation(() => {}),
    }
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('logger.info 在 dev 环境写 console.info', () => {
    logger.info('hello', { a: 1 })
    expect(consoleSpy.info).toHaveBeenCalled()
    const args = consoleSpy.info.mock.calls[0]
    expect(args[0]).toContain('hello')
  })

  it('logger.warn 写 console.warn', () => {
    logger.warn('warning')
    expect(consoleSpy.warn).toHaveBeenCalled()
  })

  it('logger.error 写 console.error', () => {
    logger.error('boom', new Error('x'))
    expect(consoleSpy.error).toHaveBeenCalled()
  })

  it('logger.debug 在 dev 环境写 console.log', () => {
    logger.debug('debug-msg')
    expect(consoleSpy.log).toHaveBeenCalled()
  })

  it('logger 设置 level=warn 后 debug 不输出', () => {
    logger.setLevel('warn')
    logger.debug('should-skip')
    expect(consoleSpy.log).not.toHaveBeenCalled()
    logger.warn('should-print')
    expect(consoleSpy.warn).toHaveBeenCalled()
  })
})
