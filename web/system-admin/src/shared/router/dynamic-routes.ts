import type { RouteRecordRaw } from 'vue-router'
import type { MenuDto } from '@/modules/02-user-access/types/menu.dto'
import { componentMap } from './component-map'

/**
 * 把 MenuDto[] 转换为 RouteRecordRaw[]
 *
 * - Directory 类型递归处理 children
 * - Menu 类型查 componentMap 转换为 lazy import
 * - Button 类型跳过（不生成路由）
 */
export function buildDynamicRoutes(menus: MenuDto[]): RouteRecordRaw[] {
  const routes: RouteRecordRaw[] = []
  for (const menu of menus) {
    if (menu.type === 'Button') continue
    if (!menu.path) continue
    if (menu.type === 'Menu' && menu.component) {
      const loader = componentMap[menu.component]
      if (!loader) {
        console.warn(`[dynamic-routes] 未找到 component 映射: ${menu.component}`)
        continue
      }
      routes.push({
        path: menu.path.replace(/^\//, ''),
        name: menu.path.replace(/\//g, '.').slice(1),
        component: loader as () => Promise<unknown> as any,
        meta: {
          title: menu.name,
          menuKey: menu.path.replace(/\//g, '.').slice(1),
          icon: menu.icon ?? undefined,
          roles: menu.roles,
          permission: menu.permission ?? undefined,
          menuGroup: deriveMenuGroup(menu),
          keepAlive: menu.cache,
        },
      })
    }
    if (menu.type === 'Directory' && menu.children?.length) {
      routes.push(...buildDynamicRoutes(menu.children))
    }
  }
  return routes
}

function deriveMenuGroup(menu: MenuDto): string {
  const prefix = menu.path.split('/')[1]
  const groupMap: Record<string, string> = {
    dashboard: '01-dashboard',
    'user-access': '02-user-access',
    'system-governance': '03-system-governance',
    'runtime-ops': '04-runtime-ops',
    audit: '05-audit',
    account: '06-account',
    monitoring: '07-monitoring',
  }
  return groupMap[prefix] ?? prefix
}
