<script setup lang="ts">
import { ref, watch } from 'vue'
import { Upload, message } from 'ant-design-vue'
import type { UploadFile, UploadProps } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'

/**
 * 通用图片上传组件
 *
 * 封装 ant-design-vue Upload（picture-card）+ FileReader 转 data URL + 预览 + 大小/类型校验。
 * 用于店铺 Logo 等图片字段：modelValue 为 data URL 或远程 URL。
 */
const props = withDefaults(
  defineProps<{
    /** 当前 URL（data URL 或远程 URL） */
    modelValue: string
    /** 接受的文件类型，如 '.jpg,.png,.webp' */
    accept: string
    /** 最大字节数 */
    maxSize: number
    /** 上传区域提示文字 */
    label?: string
    /** 禁用 */
    disabled?: boolean
  }>(),
  {
    label: '上传图片',
    disabled: false,
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'error', message: string): void
}>()

const fileList = ref<UploadFile[]>([])

watch(
  () => props.modelValue,
  (url) => {
    if (url) {
      fileList.value = [
        { uid: '-1', name: 'image', status: 'done', url } as UploadFile,
      ]
    } else {
      fileList.value = []
    }
  },
  { immediate: true },
)

const beforeUpload: UploadProps['beforeUpload'] = (file) => {
  const acceptList = props.accept
    .split(',')
    .map((s) => s.trim().toLowerCase())
    .filter(Boolean)
  const ext = '.' + (file.name.split('.').pop() ?? '').toLowerCase()
  const typeOk =
    acceptList.includes(ext) || acceptList.some((a) => file.type === a)
  if (!typeOk) {
    const msg = `不支持的文件类型，仅支持 ${props.accept}`
    message.error(msg)
    emit('error', msg)
    return false
  }
  if (file.size > props.maxSize) {
    const mb = (props.maxSize / 1024 / 1024).toFixed(1)
    const msg = `文件大小超过限制（最大 ${mb}MB）`
    message.error(msg)
    emit('error', msg)
    return false
  }
  return true
}

const customRequest: UploadProps['customRequest'] = (options) => {
  const { file, onSuccess, onError } = options
  const raw = file as File
  const reader = new FileReader()
  reader.onload = () => {
    const dataUrl = reader.result as string
    emit('update:modelValue', dataUrl)
    onSuccess?.({ url: dataUrl }, file)
  }
  reader.onerror = () => {
    const msg = '读取文件失败'
    message.error(msg)
    emit('error', msg)
    onError?.(new Error(msg))
  }
  reader.readAsDataURL(raw)
}

function onRemove(): boolean {
  emit('update:modelValue', '')
  fileList.value = []
  return false
}
</script>

<template>
  <Upload
    :file-list="fileList"
    list-type="picture-card"
    :max-count="1"
    :accept="accept"
    :disabled="disabled"
    :before-upload="beforeUpload"
    :custom-request="customRequest"
    @remove="onRemove"
  >
    <div v-if="fileList.length === 0" class="image-uploader-trigger">
      <PlusOutlined />
      <div class="image-uploader-text">{{ label }}</div>
    </div>
  </Upload>
</template>

<style scoped>
.image-uploader-trigger {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #8c8c8c;
}
.image-uploader-text {
  font-size: 12px;
  margin-top: 4px;
}
</style>
