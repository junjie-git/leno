<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from './auth.store'

/**
 * 区域级权限包裹组件
 *
 * 与 `v-permission` 指令互补：本组件用于包裹整块区域，
 * 无权限时整块不渲染（slot 不执行），避免无权限用户触发不可见 slot 内的副作用。
 *
 * 用法：
 * ```vue
 * <PermissionGuard permission="role:write">
 *   <RoleEditForm />
 * </PermissionGuard>
 * ```
 */
const props = defineProps<{
  /** 需要的权限标识 */
  permission: string
}>()

const auth = useAuthStore()

const allowed = computed(() => {
  if (!props.permission) return true
  return auth.permissions.includes(props.permission) || auth.permissions.includes('*')
})
</script>

<template>
  <slot v-if="allowed" />
</template>
