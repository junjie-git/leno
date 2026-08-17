<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { Modal, Input, Typography, ConfigProvider } from 'ant-design-vue'

/**
 * 危险操作二次确认对话框
 *
 * 与 spec §5.7 配套：
 * - 删除/丢弃/重投/封禁/重建触发 → 必走本组件，danger=true 时确认按钮红色
 * - 丢弃/封禁类需填理由 → requireInput 配置 { label, min, max }，未达 min 长度禁用确认按钮
 */
const props = withDefaults(
  defineProps<{
    /** 是否打开 */
    open: boolean
    /** 危险样式（红色确认按钮） */
    danger?: boolean
    /** 标题 */
    title: string
    /** 正文内容 */
    content: string
    /** 需要用户输入的提示配置（如丢弃原因） */
    requireInput?: { label: string; min: number; max: number }
  }>(),
  {
    danger: false,
    requireInput: undefined,
  },
)

const emit = defineEmits<{
  (e: 'confirm', value?: string): void
  (e: 'cancel'): void
}>()

const inputValue = ref('')

// open 切换为 true 时重置输入
watch(
  () => props.open,
  (open) => {
    if (open) inputValue.value = ''
  },
)

const inputValid = computed(() => {
  if (!props.requireInput) return true
  return (
    inputValue.value.length >= props.requireInput.min &&
    inputValue.value.length <= props.requireInput.max
  )
})

const okButtonProps = computed(() => ({
  disabled: !inputValid.value,
  danger: props.danger,
}))

function onOk() {
  if (!inputValid.value) return
  emit('confirm', props.requireInput ? inputValue.value : undefined)
}

function onCancel() {
  emit('cancel')
}
</script>

<template>
  <ConfigProvider :auto-insert-space-in-button="false">
    <Modal
      v-if="open"
      :open="open"
      :title="title"
      ok-text="确认"
      cancel-text="取消"
      :ok-button-props="okButtonProps"
      :get-container="false"
      @ok="onOk"
      @cancel="onCancel"
    >
      <Typography.Paragraph>{{ content }}</Typography.Paragraph>
      <div v-if="requireInput" class="confirm-input-wrap">
        <label class="confirm-input-label">{{ requireInput.label }}</label>
        <Input
          v-model:value="inputValue"
          :placeholder="`请输入${requireInput.label}（${requireInput.min}-${requireInput.max} 字）`"
          :maxlength="requireInput.max"
          allow-clear
        />
      </div>
    </Modal>
  </ConfigProvider>
</template>

<style scoped>
.confirm-input-wrap {
  margin-top: 12px;
}
.confirm-input-label {
  display: block;
  margin-bottom: 6px;
  font-size: 14px;
  color: #595959;
}
</style>
