import type { Directive, DirectiveBinding } from 'vue'
import { useAuthStore } from './auth.store'

/**
 * v-permission 指令
 *
 * 用法：
 * ```vue
 * <IdempotencyButton v-permission="'dead-letter:dispose'" danger @click="onDiscard">丢弃</IdempotencyButton>
 * ```
 *
 * 无权限时设置 `display: none`，不删 DOM（避免 hydration 问题）。
 * 空字符串权限视为「无需权限」，不隐藏。
 */
export const vPermission: Directive<HTMLElement, string> = {
  mounted(el: HTMLElement, binding: DirectiveBinding<string>) {
    applyPermission(el, binding.value)
  },
  updated(el: HTMLElement, binding: DirectiveBinding<string>) {
    applyPermission(el, binding.value)
  },
}

function applyPermission(el: HTMLElement, perm: string): void {
  // 空权限字符串视为无需权限
  if (!perm) {
    el.style.display = ''
    return
  }
  const auth = useAuthStore()
  const has = auth.permissions.includes(perm) || auth.permissions.includes('*')
  el.style.display = has ? '' : 'none'
}
