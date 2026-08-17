/**
 * 前端日志器
 *
 * - dev 环境：写 console
 * - prod 环境：批量 POST 到 `/api/admin/audit-logs/frontend`（best-effort）
 *
 * 设计参考 spec §6.8 可观测性。
 */

export type LogLevel = 'debug' | 'info' | 'warn' | 'error'

const LEVEL_PRIORITY: Record<LogLevel, number> = {
  debug: 10,
  info: 20,
  warn: 30,
  error: 40,
}

interface LoggerOptions {
  level: LogLevel
  env: 'dev' | 'prod'
}

const isProd = import.meta.env?.PROD ?? false

const defaultOptions: LoggerOptions = {
  level: 'debug',
  env: isProd ? 'prod' : 'dev',
}

class Logger {
  private options: LoggerOptions = { ...defaultOptions }
  /** prod 环境下批量缓冲 */
  private buffer: Array<{ level: LogLevel; message: string; context?: unknown; ts: number }> = []
  /** 缓冲区满大小，达到后触发 flush */
  private readonly bufferSize = 10

  /**
   * 设置日志级别
   */
  setLevel(level: LogLevel): void {
    this.options.level = level
  }

  /**
   * DEBUG 级别日志
   */
  debug(message: string, context?: unknown): void {
    this.write('debug', message, context)
  }

  /**
   * INFO 级别日志
   */
  info(message: string, context?: unknown): void {
    this.write('info', message, context)
  }

  /**
   * WARN 级别日志
   */
  warn(message: string, context?: unknown): void {
    this.write('warn', message, context)
  }

  /**
   * ERROR 级别日志
   */
  error(message: string, context?: unknown): void {
    this.write('error', message, context)
  }

  /**
   * 强制刷新 prod 缓冲区
   */
  async flush(): Promise<void> {
    if (this.options.env !== 'prod' || this.buffer.length === 0) return
    const payload = this.buffer.slice()
    this.buffer = []
    try {
      await fetch('/api/admin/audit-logs/frontend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ entries: payload }),
        keepalive: true,
      })
    } catch {
      // best-effort，丢弃不重试
    }
  }

  private write(level: LogLevel, message: string, context?: unknown): void {
    if (LEVEL_PRIORITY[level] < LEVEL_PRIORITY[this.options.level]) return

    if (this.options.env === 'dev') {
      this.writeToConsole(level, message, context)
      return
    }

    // prod：缓冲 + 批量
    this.buffer.push({ level, message, context, ts: Date.now() })
    if (this.buffer.length >= this.bufferSize || level === 'error') {
      void this.flush()
    }
  }

  private writeToConsole(level: LogLevel, message: string, context?: unknown): void {
    const prefix = `[${level.toUpperCase()}]`
    switch (level) {
      case 'debug':
        console.log(`${prefix} ${message}`, context ?? '')
        break
      case 'info':
        console.info(`${prefix} ${message}`, context ?? '')
        break
      case 'warn':
        console.warn(`${prefix} ${message}`, context ?? '')
        break
      case 'error':
        console.error(`${prefix} ${message}`, context ?? '')
        break
    }
  }
}

/** 全局 logger 单例 */
export const logger = new Logger()
