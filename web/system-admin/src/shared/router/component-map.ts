/**
 * 自动扫描所有 modules 下 views/*.vue，建立 path → lazy import 映射
 *
 * key 规范化：'/src/modules/02-user-access/views/UserManagement.vue' → '02-user-access/views/UserManagement'
 * 菜单 DTO 的 component 字段存储此 key，由 dynamic-routes.ts 查找转换。
 */
const modules = import.meta.glob('@/modules/**/views/*.vue')

export const componentMap: Record<string, () => Promise<unknown>> = {}
for (const fullKey in modules) {
  const key = fullKey
    .replace('/src/modules/', '')
    .replace('.vue', '')
  componentMap[key] = modules[fullKey] as () => Promise<unknown>
}
