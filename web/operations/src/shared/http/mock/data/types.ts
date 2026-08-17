/**
 * Mock 种子数据类型
 *
 * operations 工程仅保留鉴权（auth/login、auth/logout、users/me）
 * 与通用 /api/admin/* 基础 mock，业务模块上线后按需扩展。
 */

/**
 * Mock 账号
 */
export interface MockAccount {
  id: string
  username: string
  password: string
  email: string
  nickname: string
  status: 'Active' | 'Disabled'
  roles: string[]
  permissions: string[]
}

/**
 * Mock 会话：token → 用户名
 */
export interface MockSession {
  token: string
  username: string
  loginAt: string
}

/**
 * Mock 种子数据聚合
 */
export interface MockSeed {
  accounts: MockAccount[]
  sessions: MockSession[]
  nextId: number
}
