/**
 * 自动扫描所有 modules 下 views/*.vue，建立 path → lazy import 映射
 *
 * key 规范化：'/src/modules/02-user-access/views/UserManagement.vue' → '02-user-access/views/UserManagement'
 * 菜单 DTO 的 component 字段存储此 key，由 dynamic-routes.ts 查找转换。
 */
import type { Component } from 'vue'

const modules = import.meta.glob('@/modules/**/views/*.vue')

// 值类型与 vue-router RouteComponent 的 lazy 分支（() => Promise<Component>）对齐，
// 使 dynamic-routes.ts 无需再做断言转换即可赋给 RouteRecordRaw.component。
export const componentMap: Record<string, () => Promise<Component>> = {}
for (const fullKey in modules) {
  const key = fullKey
    .replace('/src/modules/', '')
    .replace('.vue', '')
  componentMap[key] = modules[fullKey] as () => Promise<Component>
}
