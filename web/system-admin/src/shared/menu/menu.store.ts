import { defineStore } from 'pinia'
import { menuApi } from '@/modules/02-user-access/api/menu.api'
import type { MenuDto, CreateMenuDto, UpdateMenuDto, MenuSortItemDto } from '@/modules/02-user-access/types/menu.dto'

interface MenuState {
  menus: MenuDto[]
  loaded: boolean
}

export const useMenuStore = defineStore('menu', {
  state: (): MenuState => ({
    menus: [],
    loaded: false,
  }),
  actions: {
    async fetchMenus(): Promise<void> {
      this.menus = await menuApi.getTree()
      this.loaded = true
    },
    async createMenu(body: CreateMenuDto): Promise<MenuDto> {
      const created = await menuApi.create(body)
      await this.fetchMenus()
      return created
    },
    async updateMenu(id: string, body: UpdateMenuDto): Promise<void> {
      await menuApi.update(id, body)
      await this.fetchMenus()
    },
    async deleteMenu(id: string): Promise<void> {
      await menuApi.remove(id)
      await this.fetchMenus()
    },
    async sortMenus(updates: MenuSortItemDto[]): Promise<void> {
      await menuApi.sort(updates)
      await this.fetchMenus()
    },
    reset(): void {
      this.menus = []
      this.loaded = false
    },
  },
  persist: {
    storage: localStorage,
    pick: ['menus', 'loaded'],
  },
})
