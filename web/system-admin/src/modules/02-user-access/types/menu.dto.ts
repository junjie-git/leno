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

// 空接口（extends 后无新增成员）等价于其父类型，会被 eslint no-empty-interface 拦截，
// 改用类型别名表达"Omit / Partial 映射"语义。
export type CreateMenuDto = Omit<MenuDto, 'id' | 'children'>

export type UpdateMenuDto = Partial<Omit<MenuDto, 'id' | 'children'>>

export interface MenuSortItemDto {
  id: string
  parentId: string | null
  sort: number
}
