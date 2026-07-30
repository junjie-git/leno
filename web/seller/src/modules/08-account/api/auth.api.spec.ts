/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { authApi } from './auth.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: {
    post: vi.fn(),
    get: vi.fn(),
  },
}))

describe('authApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('login 调用 POST /auth/login', async () => {
    vi.mocked(client.post).mockResolvedValue({
      data: { token: 't', expiresIn: 3600, user: {}, roles: [], permissions: [] },
    } as any)
    await authApi.login({ username: 'u', password: 'p' })
    expect(client.post).toHaveBeenCalledWith('/auth/login', { username: 'u', password: 'p' })
  })

  it('getProfile 调用 GET /users/me', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: { profile: {}, permissions: [] } } as any)
    await authApi.getProfile()
    expect(client.get).toHaveBeenCalledWith('/users/me')
  })

  it('logout 调用 POST /auth/logout', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: undefined } as any)
    await authApi.logout()
    expect(client.post).toHaveBeenCalledWith('/auth/logout')
  })
})
