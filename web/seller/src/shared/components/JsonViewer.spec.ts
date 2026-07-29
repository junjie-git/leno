import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import JsonViewer from './JsonViewer.vue'

describe('shared/components/JsonViewer', () => {
  it('渲染对象 JSON 字符串', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { id: 1, name: 'alice' } },
    })
    expect(wrapper.html()).toContain('"id"')
    expect(wrapper.html()).toContain('1')
    expect(wrapper.html()).toContain('"name"')
    expect(wrapper.html()).toContain('alice')
  })

  it('渲染数组 JSON', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: [1, 2, 3] },
    })
    expect(wrapper.html()).toContain('1')
    expect(wrapper.html()).toContain('2')
    expect(wrapper.html()).toContain('3')
  })

  it('渲染字符串值', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: 'hello' },
    })
    expect(wrapper.html()).toContain('hello')
  })

  it('maxHeight 限制容器高度', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { a: 1 }, maxHeight: 200 },
    })
    const container = wrapper.find('.json-viewer')
    expect(container.element.style.maxHeight).toBe('200px')
  })

  it('嵌套对象正确缩进展示', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { outer: { inner: 'value' } } },
    })
    expect(wrapper.html()).toContain('outer')
    expect(wrapper.html()).toContain('inner')
    expect(wrapper.html()).toContain('value')
  })

  it('null 值正确展示', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { x: null } },
    })
    expect(wrapper.html()).toContain('null')
  })

  it('布尔值正确展示', () => {
    const wrapper = mount(JsonViewer, {
      props: { data: { active: true, deleted: false } },
    })
    expect(wrapper.html()).toContain('true')
    expect(wrapper.html()).toContain('false')
  })
})
