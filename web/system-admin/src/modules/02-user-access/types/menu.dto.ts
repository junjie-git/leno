/**
 * 菜单类型
 */
export type MenuType = 'Directory' | 'Menu' | 'Button'

/**
 * 菜单 DTO（与后端 MenusController 对齐，spec §3.3）
 */
export interface MenuDto {
  id: string
  parentId: string | null
  name: string
  type: MenuType
  path: string
  component: string | null
  icon: string | null
  sort: number
  permission: string | null
  roles: string[]
  visible: boolean
  cache: boolean
  children?: MenuDto[]
}

export interface MenuTreeResultDto {
  items: MenuDto[]
}

export interface CreateMenuDto extends Omit<MenuDto, 'id' | 'children'> {}

export interface UpdateMenuDto extends Partial<Omit<MenuDto, 'id' | 'children'>> {}

export interface MenuSortItemDto {
  id: string
  parentId: string | null
  sort: number
}
