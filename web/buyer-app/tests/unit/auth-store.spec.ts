import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { LoginResultDto } from '@/modules/01-auth/types/auth.dto'

const loginMock = vi.hoisted(() => vi.fn())
const verifyTwoFactorMock = vi.hoisted(() => vi.fn())
const getProfileMock = vi.hoisted(() => vi.fn())
const logoutMock = vi.hoisted(() => vi.fn())

vi.mock('@/modules/01-auth/api/auth.api', () => ({
  authApi: {
    login: loginMock,
    verifyTwoFactor: verifyTwoFactorMock,
    getProfile: getProfileMock,
    logout: logoutMock,
  },
}))

const { useAuthStore } = await import('@/shared/auth/auth.store')

function buildLoginResult(overrides: Partial<LoginResultDto> = {}): LoginResultDto {
  return {
    token: 'jwt-token',
    user: {
      id: 'u-1001',
      username: 'zhangxiaoya',
      nickname: '张小雅',
      phone: '13812345678',
      email: 'zhangxiaoya@example.com',
      avatar: '',
      memberLevelName: '黄金会员 V3',
      points: 2860,
      gender: 'Female',
      birthday: '1996-05-20',
      createdAt: '2025-06-18T10:24:00.000Z',
      twoFactorEnabled: false,
    },
    roles: ['Buyer'],
    permissions: [],
    expiresIn: 7200,
    ...overrides,
  } as LoginResultDto
}

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('初始态未登录', () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)
    expect(auth.nickname).toBe('未登录')
    expect(auth.isBuyer).toBe(true)
  })

  it('login 成功后填充登录态', async () => {
    loginMock.mockResolvedValue(buildLoginResult())
    const auth = useAuthStore()
    const result = await auth.login({ account: 'zhangxiaoya', password: 'Zhang123456' })

    expect(result.requiresTwoFactor).toBeFalsy()
    expect(auth.token).toBe('jwt-token')
    expect(auth.user?.nickname).toBe('张小雅')
    expect(auth.roles).toEqual(['Buyer'])
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.nickname).toBe('张小雅')
    expect(auth.isBuyer).toBe(true)
  })

  it('login 触发 2FA 时不落地登录态，返回票据', async () => {
    loginMock.mockResolvedValue(
      buildLoginResult({
        token: undefined,
        user: undefined,
        requiresTwoFactor: true,
        twoFactorTicket: 'ticket-abc',
      }),
    )
    const auth = useAuthStore()
    const result = await auth.login({ account: 'demo2fa', password: 'Zhang123456' })

    expect(result.requiresTwoFactor).toBe(true)
    expect(result.twoFactorTicket).toBe('ticket-abc')
    expect(auth.token).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('verifyTwoFactor 完成二段登录', async () => {
    verifyTwoFactorMock.mockResolvedValue(buildLoginResult())
    const auth = useAuthStore()
    await auth.verifyTwoFactor({ twoFactorTicket: 'ticket-abc', code: '123456' })
    expect(auth.isAuthenticated).toBe(true)
  })

  it('token 过期后 isAuthenticated 为 false', () => {
    const auth = useAuthStore()
    auth.applyLoginResult(buildLoginResult({ expiresIn: -1 }))
    expect(auth.token).toBe('jwt-token')
    expect(auth.isAuthenticated).toBe(false)
  })

  it('fetchProfile 刷新用户信息', async () => {
    getProfileMock.mockResolvedValue(
      Object.assign(buildLoginResult().user!, { nickname: '新昵称' }),
    )
    const auth = useAuthStore()
    await auth.fetchProfile()
    expect(auth.user?.nickname).toBe('新昵称')
  })

  it('logout 后端失败也清空本地登录态', async () => {
    logoutMock.mockRejectedValue(new Error('network down'))
    const auth = useAuthStore()
    auth.applyLoginResult(buildLoginResult())
    expect(auth.isAuthenticated).toBe(true)

    await auth.logout()
    expect(auth.token).toBeNull()
    expect(auth.user).toBeNull()
    expect(auth.roles).toEqual([])
    expect(auth.isAuthenticated).toBe(false)
  })
})
