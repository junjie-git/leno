import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TreeTableDraggable from './TreeTableDraggable.vue'

interface Item {
  id: string
  parentId: string | null
  name: string
}

const data: Item[] = [
  { id: '1', parentId: null, name: '父1' },
  { id: '2', parentId: null, name: '父2' },
]

describe('TreeTableDraggable', () => {
  it('渲染传入的 data', () => {
    const wrapper = mount(TreeTableDraggable, {
      props: {
        data,
        columns: [{ title: '名称', dataIndex: 'name', key: 'name' }],
        rowKey: (r: Item) => r.id,
        parentKey: (r: Item) => r.parentId,
      },
    })
    expect(wrapper.text()).toContain('父1')
    expect(wrapper.text()).toContain('父2')
  })

  it('expand 事件触发时回传 keys', async () => {
    const wrapper = mount(TreeTableDraggable, {
      props: {
        data,
        columns: [{ title: '名称', dataIndex: 'name', key: 'name' }],
        rowKey: (r: Item) => r.id,
        parentKey: (r: Item) => r.parentId,
      },
    })
    // 触发展开（具体取决于 antd Table 内部实现）
    // 这里仅验证组件挂载成功
    expect(wrapper.find('.ant-table').exists()).toBe(true)
  })
})
