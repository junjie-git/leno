import { client, withIdempotency } from '@/shared/http'
import type {
  MenuDto,
  CreateMenuDto,
  UpdateMenuDto,
  MenuSortItemDto,
} from '../types/menu.dto'

/**
 * 菜单管理 API
 *
 * Mock 模式下由 axios-mock-adapter 拦截；
 * 真实后端由 MenusController 提供（spec §3.8）。
 */
export const menuApi = {
  /** 拉取菜单树 */
  getTree(): Promise<MenuDto[]> {
    return client.get<MenuDto[]>('/admin/menus/tree').then((r) => r.data)
  },

  /** 新增菜单（幂等） */
  create(body: CreateMenuDto): Promise<MenuDto> {
    return client.post<MenuDto>('/admin/menus', body, withIdempotency()).then((r) => r.data)
  },

  /** 更新菜单（幂等） */
  update(id: string, body: UpdateMenuDto): Promise<MenuDto> {
    return client.put<MenuDto>(`/admin/menus/${id}`, body, withIdempotency()).then((r) => r.data)
  },

  /** 删除菜单（递归删除子节点，幂等） */
  remove(id: string): Promise<void> {
    return client.delete<void>(`/admin/menus/${id}`, withIdempotency()).then(() => undefined)
  },

  /** 批量排序（幂等） */
  sort(updates: MenuSortItemDto[]): Promise<void> {
    return client.put<void>('/admin/menus/sort', updates, withIdempotency()).then(() => undefined)
  },
}
