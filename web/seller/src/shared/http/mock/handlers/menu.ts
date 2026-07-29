import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData, nextId } from '../data/seed'
import type { MockSeed } from '../data/types'

/**
 * 菜单 handler 注册
 *
 * 端点：
 * - GET    /admin/menus/tree
 * - POST   /admin/menus
 * - PUT    /admin/menus/{id}
 * - DELETE /admin/menus/{id}
 * - PUT    /admin/menus/sort
 */
export function registerMenuHandlers(mock: MockAdapter): void {
  mock.onGet('/admin/menus/tree').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.menus }]
  })

  mock.onPost('/admin/menus').reply((config) => {
    const seed = loadSeedData()
    const body = JSON.parse(config.data || '{}')
    if (!body.name || !body.type) {
      return [200, { code: 40001, message: '菜单名称与类型必填', data: null }]
    }
    const newMenu = {
      ...body,
      id: nextId(seed, 'm'),
      children: body.type === 'Directory' ? [] : undefined,
    }
    seed.menus.push(newMenu)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: newMenu }]
  })

  mock.onPut(/\/admin\/menus\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    const body = JSON.parse(config.data || '{}')
    const updated = updateMenuById(seed.menus as any[], id, body)
    if (!updated) {
      return [200, { code: 40400, message: `菜单 ${id} 不存在`, data: null }]
    }
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: updated }]
  })

  mock.onDelete(/\/admin\/menus\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    // 检查是否有子菜单
    if (hasChildren(seed.menus as any[], id)) {
      return [200, { code: 40001, message: '存在子菜单，请先删除子菜单', data: null }]
    }
    const removed = removeMenuById(seed.menus as any[], id)
    if (!removed) {
      return [200, { code: 40400, message: `菜单 ${id} 不存在`, data: null }]
    }
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })

  mock.onPut('/admin/menus/sort').reply((config) => {
    const seed = loadSeedData()
    const updates = JSON.parse(config.data || '[]') as Array<{ id: string; parentId: string | null; sort: number }>
    for (const u of updates) {
      const menu = findMenuById(seed.menus as any[], u.id)
      if (menu) {
        menu.sort = u.sort
        menu.parentId = u.parentId
      }
    }
    // 重新组装树（按 parentId 移动节点）
    rebuildMenuTree(seed)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })
}

function findMenuById(menus: any[], id: string): any | null {
  for (const m of menus) {
    if (m.id === id) return m
    if (m.children) {
      const found = findMenuById(m.children, id)
      if (found) return found
    }
  }
  return null
}

function updateMenuById(menus: any[], id: string, patch: any): any | null {
  const menu = findMenuById(menus, id)
  if (!menu) return null
  Object.assign(menu, patch)
  return menu
}

function hasChildren(menus: any[], id: string): boolean {
  const menu = findMenuById(menus, id)
  return !!(menu?.children && menu.children.length > 0)
}

function removeMenuById(menus: any[], id: string): boolean {
  for (let i = 0; i < menus.length; i++) {
    if (menus[i].id === id) {
      menus.splice(i, 1)
      return true
    }
    if (menus[i].children) {
      if (removeMenuById(menus[i].children, id)) return true
    }
  }
  return false
}

function rebuildMenuTree(seed: MockSeed): void {
  // 简化实现：仅按 sort 排序每个父级的 children
  const sortChildren = (menus: any[]) => {
    menus.sort((a, b) => a.sort - b.sort)
    for (const m of menus) {
      if (m.children) sortChildren(m.children)
    }
  }
  sortChildren(seed.menus as any[])
}
