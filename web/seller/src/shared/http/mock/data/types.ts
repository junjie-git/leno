/**
 * Mock 种子数据聚合类型（骨架版本）
 *
 * 注：MenuDto / OnlineUserDto 等类型在后续 Task 中创建，
 * 本文件先以 `unknown[]` 占位，Task 13（联调）时统一替换为强类型。
 */
export interface MockSeed {
  menus: unknown[]
  onlineUsers: unknown[]
  loginLogs: unknown[]
  redisKeys: unknown[]
  redisInfo: unknown
  keyspaces: unknown[]
  serverSnapshot: unknown
  serverHistory: { cpu: unknown[]; memory: unknown[]; diskIo: unknown[] }
  nextId: number
}
