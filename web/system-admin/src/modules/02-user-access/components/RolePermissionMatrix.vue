<!-- web/system-admin/src/modules/02-user-access/components/RolePermissionMatrix.vue -->
<template>
  <a-spin :spinning="loading">
    <EmptyState
      v-if="catalog.length === 0"
      description="暂无可分配权限"
      action-text="刷新"
      @action="emit('refresh')"
    />
    <a-tree
      v-else
      v-model:checked-keys="checkedKeys"
      :tree-data="treeData"
      checkable
      :default-expand-all="true"
      :selectable="false"
    >
      <template #title="{ key, title }">
        <span :class="{ 'permission-code': isPermissionLeaf(key) }">{{ title }}</span>
      </template>
    </a-tree>
  </a-spin>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { TreeProps } from 'ant-design-vue'
import type { PermissionGroupDto } from '../types/role.dto'
import EmptyState from '@/shared/components/EmptyState.vue'

interface Props {
  catalog: PermissionGroupDto[]
  selected: string[]
  loading?: boolean
}
const props = withDefaults(defineProps<Props>(), { loading: false })

const emit = defineEmits<{
  (e: 'update:selected', value: string[]): void
  (e: 'refresh'): void
}>()

// checkedKeys 同时包含「模块分组键」与「权限码」两种；
// 初始化时仅传入权限码，模块分组键由 a-tree 自动计算半选状态
const checkedKeys = ref<string[]>([...props.selected])

// 外部 selected 变化时同步（如切换角色后重新拉取权限）
watch(
  () => props.selected,
  (val) => {
    checkedKeys.value = [...val]
  },
)

// checkedKeys 变化时过滤掉分组键，仅回传权限码
watch(checkedKeys, (keys) => {
  const codes = keys.filter((k) => !k.startsWith('module:'))
  emit('update:selected', codes)
})

// 构造 a-tree 数据结构：模块为父节点（key 加 module: 前缀避免与权限码冲突），权限为叶子
const treeData = computed<TreeProps['treeData']>(() =>
  props.catalog.map((group) => ({
    key: `module:${group.module}`,
    title: group.moduleLabel,
    children: group.permissions.map((p) => ({
      key: p.code,
      title: p.label ? `${p.label} (${p.code})` : p.code,
    })),
  })),
)

function isPermissionLeaf(key: string | number): boolean {
  return typeof key === 'string' && !key.startsWith('module:')
}
</script>

<style scoped>
.permission-code {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  color: #595959;
}
</style>
