import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import App from './App.vue'

describe('App', () => {
  it('通过 Provider 包裹 RouterView 并渲染匹配路由', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/', component: { template: '<div class="home">home</div>' } }],
    })
    await router.push('/')
    await router.isReady()
    const wrapper = mount(App, { global: { plugins: [router] } })
    expect(wrapper.html()).toContain('home')
    expect(wrapper.html()).not.toContain('app-placeholder')
  })
})
