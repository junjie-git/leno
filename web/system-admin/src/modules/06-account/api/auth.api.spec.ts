import { describe, it, expect, beforeEach, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '@/shared/http'
import { authApi } from './auth.api'

vi.mock('@/shared/http', () => ({
  client: { post: vi.fn(), get: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'k' } })),
}))

describe('modules/06-account/api/auth.api', () => {
  beforeEach(() => {
    ;(client.post as Mock).mockReset()
    ;(client.get as Mock).mockReset()
  })

  it('login: POST /auth/login 并返回解包数据', async () => {
    const data = {
      token: 'tok-1',
      expiresIn: 3600,
      user: { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] },
      roles: ['Admin'],
      permissions: ['*'],
    }
    ;(client.post as Mock).mockResolvedValue({ data })
    const result = await authApi.login({ username: 'admin', password: 'Admin123' })
    expect(client.post).toHaveBeenCalledWith('/auth/login', {
      username: 'admin',
      password: 'Admin123',
    })
    expect(result).toEqual(data)
  })

  it('logout: POST /auth/logout', async () => {
    ;(client.post as Mock).mockResolvedValue({ data: null })
    await authApi.logout()
    expect(client.post).toHaveBeenCalledWith('/auth/logout', null)
  })

  it('getProfile: GET /users/me 并返回解包数据', async () => {
    const data = {
      profile: { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] },
      permissions: ['role:read', 'role:write'],
    }
    ;(client.get as Mock).mockResolvedValue({ data })
    const result = await authApi.getProfile()
    expect(client.get).toHaveBeenCalledWith('/users/me')
    expect(result).toEqual(data)
  })
})
