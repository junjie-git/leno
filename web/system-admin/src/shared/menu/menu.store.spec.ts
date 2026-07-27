import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useMenuStore } from './menu.store'
import * as menuApiModule from '@/modules/02-user-access/api/menu.api'

vi.mock('@/modules/02-user-access/api/menu.api', () => ({
  menuApi: {
    getTree: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    sort: vi.fn(),
  },
}))

describe('menu.store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('初始状态：空菜单 + 未加载', () => {
    const store = useMenuStore()
    expect(store.menus).toEqual([])
    expect(store.loaded).toBe(false)
  })

  it('fetchMenus: 调用 api.getTree 并填充 state', async () => {
    const mockTree = [{ id: 'm-01', name: '仪表盘' }]
    vi.mocked(menuApiModule.menuApi.getTree).mockResolvedValueOnce(mockTree as any)
    const store = useMenuStore()
    await store.fetchMenus()
    expect(menuApiModule.menuApi.getTree).toHaveBeenCalled()
    expect(store.menus).toEqual(mockTree)
    expect(store.loaded).toBe(true)
  })

  it('createMenu: 调用 api.create 后重新 fetchMenus', async () => {
    vi.mocked(menuApiModule.menuApi.create).mockResolvedValueOnce({ id: 'm-new' } as any)
    vi.mocked(menuApiModule.menuApi.getTree).mockResolvedValueOnce([{ id: 'm-new' }] as any)
    const store = useMenuStore()
    const body = { name: '新菜单', type: 'Menu' as const, path: '/x', component: null, icon: null, sort: 1, permission: null, roles: ['Admin'], visible: true, cache: false, parentId: null }
    const result = await store.createMenu(body)
    expect(menuApiModule.menuApi.create).toHaveBeenCalledWith(body)
    expect(result).toEqual({ id: 'm-new' })
    expect(store.menus).toEqual([{ id: 'm-new' }])
  })

  it('deleteMenu: 调用 api.remove 后重新 fetchMenus', async () => {
    vi.mocked(menuApiModule.menuApi.remove).mockResolvedValueOnce(undefined)
    vi.mocked(menuApiModule.menuApi.getTree).mockResolvedValueOnce([] as any)
    const store = useMenuStore()
    await store.deleteMenu('m-01')
    expect(menuApiModule.menuApi.remove).toHaveBeenCalledWith('m-01')
    expect(store.menus).toEqual([])
  })

  it('reset: 清空 state', () => {
    const store = useMenuStore()
    store.menus = [{ id: 'x' }] as any
    store.loaded = true
    store.reset()
    expect(store.menus).toEqual([])
    expect(store.loaded).toBe(false)
  })
})
