import type { MockAccount, MockSeed, MockSession } from './types'

const SEED_KEY = 'operations_mock_seed_v1'

/**
 * 内置运营账号：
 * - admin / Admin123!   → Admin 角色，通配权限 *
 * - operator / Operator123! → Operator 角色，运营常规权限
 */
function buildAccountSeed(): MockAccount[] {
  return [
    {
      id: 'u-0001',
      username: 'admin',
      password: 'Admin123!',
      email: 'admin@leno.com',
      nickname: '运营管理员',
      status: 'Active',
      roles: ['Admin'],
      permissions: ['*'],
    },
    {
      id: 'u-0002',
      username: 'operator',
      password: 'Operator123!',
      email: 'operator@leno.com',
      nickname: '运营专员',
      status: 'Active',
      roles: ['Operator'],
      permissions: [
        'dashboard:view',
        'product:audit',
        'promotion:manage',
        'seller:manage',
        'order:read',
        'payment:read',
        'notification:read',
        'member:read',
        'export:run',
        'account:profile:view',
      ],
    },
  ]
}

/**
 * 确保 localStorage 中存在种子数据；若不存在则初始化。
 */
export function ensureSeedData(): void {
  if (localStorage.getItem(SEED_KEY)) return
  const seed: MockSeed = {
    accounts: buildAccountSeed(),
    sessions: [],
    nextId: 1000,
  }
  localStorage.setItem(SEED_KEY, JSON.stringify(seed))
}

export function loadSeedData(): MockSeed {
  ensureSeedData()
  return JSON.parse(localStorage.getItem(SEED_KEY)!) as MockSeed
}

export function saveSeedData(seed: MockSeed): void {
  localStorage.setItem(SEED_KEY, JSON.stringify(seed))
}

export function resetSeedData(): void {
  localStorage.removeItem(SEED_KEY)
  ensureSeedData()
}

/**
 * 生成自增 ID
 */
export function nextTokenId(seed: MockSeed): number {
  seed.nextId += 1
  return seed.nextId
}

/**
 * 按用户名查找账号
 */
export function findAccountByUsername(seed: MockSeed, username: string): MockAccount | undefined {
  return seed.accounts.find((a) => a.username === username)
}

/**
 * 按 token 查找会话与对应账号
 */
export function findSessionByToken(
  seed: MockSeed,
  token: string,
): { session: MockSession; account: MockAccount } | undefined {
  const session = seed.sessions.find((s) => s.token === token)
  if (!session) return undefined
  const account = findAccountByUsername(seed, session.username)
  if (!account) return undefined
  return { session, account }
}

/**
 * 写入新会话（同账号旧会话保留，支持多端登录）
 */
export function addSession(seed: MockSeed, token: string, username: string): void {
  seed.sessions.push({ token, username, loginAt: new Date().toISOString() })
}

/**
 * 移除会话（登出）
 */
export function removeSession(seed: MockSeed, token: string): void {
  seed.sessions = seed.sessions.filter((s) => s.token !== token)
}
