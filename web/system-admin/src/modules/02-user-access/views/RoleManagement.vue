<!-- web/system-admin/src/modules/02-user-access/views/RoleManagement.vue -->
<template>
  <div class="role-management">
    <a-row :gutter="24">
      <!-- 区域 A：左侧角色列表 -->
      <a-col :xs="24" :xl="8" :xxl="6">
        <a-card :bordered="false" class="role-list-card">
          <template #title>
            <span>角色列表</span>
          </template>
          <template #extra>
            <a-button type="primary" size="small" @click="onCreate">
              <template #icon><PlusOutlined /></template>
              新增角色
            </a-button>
          </template>
          <a-input-search
            v-model:value="listKeyword"
            placeholder="搜索角色名"
            allow-clear
            style="margin-bottom: 12px"
            @search="fetchRoles"
          />
          <a-spin :spinning="listLoading">
            <EmptyState
              v-if="roles.length === 0"
              description="暂无角色"
              action-text="新增角色"
              @action="onCreate"
            />
            <a-list v-else :data-source="roles" :split="true">
              <template #renderItem="{ item }">
                <a-list-item
                  :class="{ 'role-item-active': selectedRole?.id === item.id }"
                  @click="onSelectRole(item)"
                >
                  <a-list-item-meta>
                    <template #title>
                      <span>{{ item.name }}</span>
                      <a-tag v-if="item.isBuiltIn" color="purple" style="margin-left: 8px">内置</a-tag>
                      <a-tag v-else color="blue" style="margin-left: 8px">自定义</a-tag>
                    </template>
                    <template #description>
                      {{ item.description || '无描述' }} · 用户 {{ item.userCount }} 人
                    </template>
                  </a-list-item-meta>
                </a-list-item>
              </template>
            </a-list>
          </a-spin>
        </a-card>
      </a-col>

      <!-- 区域 B：右侧详情与权限 -->
      <a-col :xs="24" :xl="16" :xxl="18">
        <a-card :bordered="false">
          <a-spin :spinning="detailLoading">
            <EmptyState
              v-if="!selectedRole"
              description="请从左侧选择一个角色"
            />
            <template v-else>
              <a-descriptions :column="2" bordered>
                <a-descriptions-item label="角色名">{{ selectedRole.name }}</a-descriptions-item>
                <a-descriptions-item label="类型">
                  <a-tag v-if="selectedRole.isBuiltIn" color="purple">内置</a-tag>
                  <a-tag v-else color="blue">自定义</a-tag>
                </a-descriptions-item>
                <a-descriptions-item label="描述" :span="2">{{ selectedRole.description || '—' }}</a-descriptions-item>
                <a-descriptions-item label="创建人">{{ selectedRole.createdBy }}</a-descriptions-item>
                <a-descriptions-item label="创建时间">{{ formatDateTime(selectedRole.createdAt) }}</a-descriptions-item>
                <a-descriptions-item label="用户数" :span="2">
                  <a-button type="link" @click="goToUsersByRole">{{ selectedRole.userCount }} 人</a-button>
                </a-descriptions-item>
              </a-descriptions>

              <div class="role-actions">
                <a-space>
                  <a-button @click="onEdit">
                    <template #icon><EditOutlined /></template>
                    编辑
                  </a-button>
                  <a-tooltip :title="selectedRole.isBuiltIn ? '内置角色不可删除' : ''">
                    <a-button
                      danger
                      :disabled="selectedRole.isBuiltIn"
                      @click="onDelete"
                    >
                      <template #icon><DeleteOutlined /></template>
                      删除
                    </a-button>
                  </a-tooltip>
                </a-space>
              </div>

              <a-divider>权限分配</a-divider>
              <RolePermissionMatrix
                :catalog="permissionCatalog"
                :selected="selectedPermissions"
                :loading="permissionLoading"
                @update:selected="onPermissionsChange"
                @refresh="fetchPermissions"
              />
              <div class="permission-actions">
                <IdempotencyButton
                  type="primary"
                  :loading="savingPermissions"
                  :disabled="!permissionsDirty"
                  @click="onSavePermissions"
                >保存权限</IdempotencyButton>
              </div>
            </template>
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- 新建/编辑弹窗 -->
    <a-modal
      v-model:open="formModalOpen"
      :title="formMode === 'create' ? '新增角色' : '编辑角色'"
      :destroy-on-close="true"
      :confirm-loading="formSubmitting"
      @ok="onSubmitForm"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item label="角色名" name="name">
          <a-input
            v-model:value="formData.name"
            :disabled="formMode === 'edit' && selectedRole?.isBuiltIn"
            placeholder="请输入角色名"
            :maxlength="32"
          />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea
            v-model:value="formData.description"
            placeholder="请输入角色描述"
            :rows="3"
            :maxlength="200"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 删除确认 -->
    <ConfirmDialog
      :open="deleteConfirmOpen"
      danger
      title="删除角色"
      content="删除后该角色的权限配置将丢失，已分配该角色的用户需重新分配。此操作不可逆。"
      @confirm="onConfirmDelete"
      @cancel="deleteConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { rolesApi } from '../api/roles.api'
import type {
  RoleDto,
  ListRolesParams,
  SaveRoleDto,
  PermissionGroupDto,
} from '../types/role.dto'
import { formatDateTime } from '@/shared/utils/format'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import RolePermissionMatrix from '../components/RolePermissionMatrix.vue'

const router = useRouter()

const listKeyword = ref('')
const roles = ref<RoleDto[]>([])
const listLoading = ref(false)
const selectedRole = ref<RoleDto | null>(null)
const detailLoading = ref(false)

const permissionCatalog = ref<PermissionGroupDto[]>([])
const selectedPermissions = ref<string[]>([])
const originalPermissions = ref<string[]>([])
const permissionLoading = ref(false)
const savingPermissions = ref(false)

const permissionsDirty = computed(
  () => JSON.stringify([...selectedPermissions.value].sort())
    !== JSON.stringify([...originalPermissions.value].sort()),
)

async function fetchRoles() {
  listLoading.value = true
  try {
    const params: ListRolesParams & { page: number; pageSize: number } = {
      page: 1,
      pageSize: 100,
    }
    if (listKeyword.value) params.keyword = listKeyword.value
    const { data } = await rolesApi.list(params)
    roles.value = data.items
    if (roles.value.length > 0 && !selectedRole.value) {
      await onSelectRole(roles.value[0]!)
    }
  } catch {
    message.error('加载角色列表失败')
  } finally {
    listLoading.value = false
  }
}

async function onSelectRole(role: RoleDto) {
  selectedRole.value = role
  detailLoading.value = true
  await fetchPermissions()
  detailLoading.value = false
}

async function fetchPermissions() {
  if (!selectedRole.value) return
  permissionLoading.value = true
  try {
    const [permRes, catalogRes] = await Promise.all([
      rolesApi.getPermissions(selectedRole.value.id),
      rolesApi.getPermissionCatalog(),
    ])
    selectedPermissions.value = [...permRes.data]
    originalPermissions.value = [...permRes.data]
    permissionCatalog.value = catalogRes.data
  } catch {
    message.error('加载权限失败')
  } finally {
    permissionLoading.value = false
  }
}

function onPermissionsChange(codes: string[]) {
  selectedPermissions.value = codes
}

async function onSavePermissions() {
  if (!selectedRole.value) return
  savingPermissions.value = true
  try {
    await rolesApi.updatePermissions(selectedRole.value.id, {
      permissions: selectedPermissions.value,
    })
    message.success('权限已更新')
    originalPermissions.value = [...selectedPermissions.value]
  } catch {
    message.error('权限保存失败')
  } finally {
    savingPermissions.value = false
  }
}

function goToUsersByRole() {
  if (selectedRole.value) {
    router.push({ path: '/user-access/users', query: { roleId: selectedRole.value.id } })
  }
}

// 新建/编辑
const formModalOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()
const formData = reactive<SaveRoleDto>({ name: '', description: '' })
const formSubmitting = ref(false)

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: '请输入角色名', trigger: 'blur' }],
}

function onCreate() {
  formMode.value = 'create'
  formData.name = ''
  formData.description = ''
  formModalOpen.value = true
}

function onEdit() {
  if (!selectedRole.value) return
  formMode.value = 'edit'
  formData.name = selectedRole.value.name
  formData.description = selectedRole.value.description
  formModalOpen.value = true
}

async function onSubmitForm() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  formSubmitting.value = true
  try {
    if (formMode.value === 'create') {
      await rolesApi.create({ name: formData.name, description: formData.description })
      message.success('角色已创建')
    } else if (selectedRole.value) {
      await rolesApi.update(selectedRole.value.id, {
        name: formData.name,
        description: formData.description,
      })
      message.success('角色已更新')
    }
    formModalOpen.value = false
    await fetchRoles()
  } catch {
    message.error(formMode.value === 'create' ? '创建角色失败' : '更新角色失败')
  } finally {
    formSubmitting.value = false
  }
}

// 删除
const deleteConfirmOpen = ref(false)

function onDelete() {
  if (selectedRole.value?.isBuiltIn) return
  deleteConfirmOpen.value = true
}

async function onConfirmDelete() {
  deleteConfirmOpen.value = false
  if (!selectedRole.value) return
  try {
    await rolesApi.remove(selectedRole.value.id)
    message.success('角色已删除')
    selectedRole.value = null
    selectedPermissions.value = []
    originalPermissions.value = []
    await fetchRoles()
  } catch {
    message.error('删除失败：可能该角色下仍有用户，请先迁移')
  }
}

onMounted(() => {
  fetchRoles()
})
</script>

<style scoped>
.role-management {
  min-height: 100%;
}
.role-list-card :deep(.ant-list-item) {
  cursor: pointer;
  padding: 12px 16px;
}
.role-list-card :deep(.ant-list-item.role-item-active) {
  background-color: #e6f4ff;
}
.role-actions {
  margin-top: 16px;
}
.permission-actions {
  margin-top: 16px;
  text-align: right;
}
</style>
