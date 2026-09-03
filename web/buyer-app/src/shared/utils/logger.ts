/**
 * 轻量日志器
 *
 * - dev 环境输出完整日志
 * - 生产环境仅输出 warn/error，避免泄露调试信息
 */
const isDev = import.meta.env.DEV

function prefix(level: string): string {
  return `[Leno buyer-app][${level}]`
}

export const logger = {
  debug(...args: unknown[]): void {
    if (isDev) console.debug(prefix('debug'), ...args)
  },
  info(...args: unknown[]): void {
    if (isDev) console.info(prefix('info'), ...args)
  },
  warn(...args: unknown[]): void {
    console.warn(prefix('warn'), ...args)
  },
  error(...args: unknown[]): void {
    console.error(prefix('error'), ...args)
  },
}
