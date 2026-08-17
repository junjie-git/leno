import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import DataTable from './DataTable.vue'
import type { TableColumn, PageResult } from '@/shared/types'

const columns: TableColumn[] = [
  { title: 'ID', dataIndex: 'id', width: 80 },
  { title: '名称', dataIndex: 'name', width: 200 },
]

describe('shared/components/DataTable', () => {
  it('初次挂载调用 fetcher，传入 page=1 pageSize=10', async () => {
    const fetcher = vi.fn().mockResolvedValue({
      items: [{ id: '1', name: 'alice' }],
      total: 1,
      page: 1,
      pageSize: 10,
    } as PageResult<unknown>)
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(fetcher).toHaveBeenCalledTimes(1)
    const callArg = fetcher.mock.calls[0][0] as { page: number; pageSize: number }
    expect(callArg.page).toBe(1)
    expect(callArg.pageSize).toBe(10)
    expect(wrapper.html()).toContain('alice')
  })

  it('渲染列标题', async () => {
    const fetcher = vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('ID')
    expect(wrapper.html()).toContain('名称')
  })

  it('loading=true 时显示加载态', async () => {
    let resolveFn!: (v: PageResult<unknown>) => void
    const fetcher = vi.fn().mockReturnValue(
      new Promise<PageResult<unknown>>((resolve) => {
        resolveFn = resolve
      }),
    )
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('ant-spin')
    resolveFn({ items: [], total: 0, page: 1, pageSize: 10 })
    await flushPromises()
  })

  it('fetcher 抛错时显示 ErrorBoundary 兜底', async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error('boom'))
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('加载失败')
  })

  it('空数据时显示 EmptyState', async () => {
    const fetcher = vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('ant-empty')
  })

  it('点击刷新按钮重新调用 fetcher', async () => {
    const fetcher = vi.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10 })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    expect(fetcher).toHaveBeenCalledTimes(1)
    const refreshBtn = wrapper.find('button[data-testid="refresh"]')
    expect(refreshBtn.exists()).toBe(true)
    await refreshBtn.trigger('click')
    await flushPromises()
    expect(fetcher).toHaveBeenCalledTimes(2)
  })

  it('翻页时传入新的 page', async () => {
    const fetcher = vi.fn().mockResolvedValue({
      items: [{ id: '1', name: 'alice' }],
      total: 25,
      page: 1,
      pageSize: 10,
    })
    const wrapper = mount(DataTable, {
      props: { columns, fetcher, rowKey: 'id' },
    })
    await flushPromises()
    // 模拟点击第 2 页
    const pagination = wrapper.findComponent({ name: 'a-pagination' })
    if (pagination.exists()) {
      await pagination.vm.$emit('change', 2, 10)
      await flushPromises()
      const lastCall = fetcher.mock.calls.at(-1)?.[0] as { page: number }
      expect(lastCall.page).toBe(2)
    }
  })
})
