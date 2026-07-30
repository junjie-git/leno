import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { Upload } from 'ant-design-vue'
import ImageUploader from './ImageUploader.vue'

describe('shared/components/ImageUploader', () => {
  it('未设置 modelValue 时渲染上传触发区与 label', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 5 * 1024 * 1024, label: '上传 Logo' },
    })
    expect(wrapper.html()).toContain('上传 Logo')
    expect(wrapper.html()).toContain('ant-upload')
  })

  it('设置 modelValue 时回填预览（fileList 长度为 1）', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: 'data:image/png;base64,AAAA', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    expect(upload.props('fileList')).toHaveLength(1)
    expect(upload.props('fileList')[0].url).toBe('data:image/png;base64,AAAA')
  })

  it('modelValue 清空时恢复上传触发区', async () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: 'data:image/png;base64,AAAA', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    await wrapper.setProps({ modelValue: '' })
    const upload = wrapper.findComponent(Upload)
    expect(upload.props('fileList')).toHaveLength(0)
  })

  it('beforeUpload 拒绝超过 maxSize 的文件并 emit error', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const beforeUpload = upload.props('beforeUpload') as (f: { name: string; size: number; type: string }) => boolean
    const bigFile = { name: 'a.png', size: 10 * 1024, type: 'image/png' }
    expect(beforeUpload(bigFile)).toBe(false)
    expect(wrapper.emitted('error')).toBeTruthy()
  })

  it('beforeUpload 拒绝不匹配 accept 的文件类型并 emit error', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const beforeUpload = upload.props('beforeUpload') as (f: { name: string; size: number; type: string }) => boolean
    expect(beforeUpload({ name: 'a.txt', size: 100, type: 'text/plain' })).toBe(false)
    expect(wrapper.emitted('error')).toBeTruthy()
  })

  it('beforeUpload 接受合法文件', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png,.jpg', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const beforeUpload = upload.props('beforeUpload') as (f: { name: string; size: number; type: string }) => boolean
    expect(beforeUpload({ name: 'a.png', size: 1024, type: 'image/png' })).toBe(true)
    expect(wrapper.emitted('error')).toBeFalsy()
  })

  it('customRequest 读取文件为 data URL 并 emit update:modelValue', async () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const customRequest = upload.props('customRequest') as (o: {
      file: unknown
      onSuccess?: (resp: unknown, file: unknown) => void
      onError?: (e: Error) => void
    }) => void
    const file = new File(['data'], 'test.png', { type: 'image/png' })
    const onSuccess = vi.fn()
    customRequest({ file, onSuccess, onError: vi.fn() })
    // jsdom FileReader.readAsDataURL 在独立宏任务中触发 onload，需等待足够长
    await new Promise((resolve) => setTimeout(resolve, 50))
    await wrapper.vm.$nextTick()
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    expect(typeof emitted![0][0]).toBe('string')
    expect(emitted![0][0] as string).toMatch(/^data:image\/png;base64,/)
    expect(onSuccess).toHaveBeenCalled()
  })
})
