import { config } from '@vue/test-utils'
import { afterEach, vi } from 'vitest'

// 每个测试后清理挂载的 DOM（避免组件泄漏影响后续测试）
afterEach(() => {
  document.body.innerHTML = ''
})

// 全局 stub 配置：避免 Vant 组件 teleport 弹层带来的复杂度
config.global.stubs = {
  teleport: true,
}

// Mock matchMedia（Vant 4 在 jsdom 下需要）
if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    }),
  })
}

// Mock ResizeObserver（部分 Vant 组件依赖）
if (!window.ResizeObserver) {
  Object.defineProperty(window, 'ResizeObserver', {
    writable: true,
    value: class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  })
}

// Mock IntersectionObserver（van-list 无限滚动依赖）
if (!window.IntersectionObserver) {
  Object.defineProperty(window, 'IntersectionObserver', {
    writable: true,
    value: class {
      observe() {}
      unobserve() {}
      disconnect() {}
      takeRecords() {
        return []
      }
    },
  })
}

// Mock scrollTo（页面滚动逻辑在 jsdom 下不可用）
if (!window.scrollTo) {
  Object.defineProperty(window, 'scrollTo', {
    writable: true,
    value: () => {},
  })
}

// 静音 console.error 在测试中由各 case 自行决定是否恢复
const originalError = console.error
console.error = (...args: unknown[]) => {
  if (typeof args[0] === 'string' && args[0].includes('Vue warn')) {
    return
  }
  originalError(...args)
}

// 提供 vi 全局以便 spec 文件直接使用
export { vi }
