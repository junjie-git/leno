import { config } from '@vue/test-utils'

config.global.mocks = {
  $t: (key: string) => key,
}

// Mock window.matchMedia（Ant Design Vue 响应式需要）
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

// Mock IntersectionObserver
class MockIntersectionObserver {
  observe = () => {}
  unobserve = () => {}
  disconnect = () => {}
  takeRecords = () => []
}
Object.defineProperty(window, 'IntersectionObserver', {
  writable: true,
  value: MockIntersectionObserver,
})

// Mock ResizeObserver（ECharts 需要）
class MockResizeObserver {
  observe = () => {}
  unobserve = () => {}
  disconnect = () => {}
}
Object.defineProperty(window, 'ResizeObserver', {
  writable: true,
  value: MockResizeObserver,
})
