<!-- web/system-admin/src/modules/02-user-access/views/Operators.vue -->
<template>
  <div class="operators">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="搜索">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="用户名/姓名"
            allow-clear
            style="width: 220px"
            @search="onSearch"
          />
        </a-form-item>
        <a-form-item label="角色">
          <a-select
            v-model:value="filters.role"
            style="width: 160px"
            allow-clear
            placeholder="全部角色"
            :options="OPERATOR_ROLE_OPTIONS"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            style="width: 140px"
            allow-clear
            placeholder="全部状态"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
        <a-form-item>
          <PermissionGuard permission="operator:write">
            <a-button type="primary" @click="onCreate">
              <template #icon><PlusOutlined /></template>
              新建运营人员
            </a-button>
          </PermissionGuard>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="operatorId"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无运营人员" action-text="新建运营人员" @action="onCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'role'">
            <a-tag color="cyan">{{ roleLabel(record.role) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="operator" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'lastLoginAt'">
            {{ record.lastLoginAt ? formatDateTime(record.lastLoginAt) : '—' }}
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onView(record)">查看</a-button>
              <PermissionGuard permission="operator:write">
                <a-button type="link" size="small" @click="onAssignPermissions(record)">权限</a-button>
                <IdempotencyButton
                  v-if="record.status === 'Active'"
                  type="link"
                  size="small"
                  @click="onDeactivate(record)"
                >停用</IdempotencyButton>
                <IdempotencyButton
                  v-else
                  type="link"
                  size="small"
                  @click="onActivate(record)"
                >激活</IdempotencyButton>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新建弹窗 -->
    <a-modal
      v-model:open="createModalOpen"
      title="新建运营人员"
      :destroy-on-close="true"
      :confirm-loading="creating"
      @ok="onSubmitCreate"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item label="用户名" name="username">
          <a-input v-model:value="formData.username" placeholder="登录用户名" :maxlength="32" />
        </a-form-item>
        <a-form-item label="姓名" name="name">
          <a-input v-model:value="formData.name" placeholder="真实姓名" :maxlength="32" />
        </a-form-item>
        <a-form-item label="邮箱" name="email">
          <a-input v-model:value="formData.email" placeholder="name@example.com" />
        </a-form-item>
        <a-form-item label="初始密码" name="password">
          <a-input-password
            v-model:value="formData.password"
            autocomplete="new-password"
            placeholder="至少 8 位"
          />
        </a-form-item>
        <a-form-item label="角色" name="role">
          <a-select v-model:value="formData.role" :options="OPERATOR_ROLE_OPTIONS" placeholder="请选择角色" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 区域 D：权限分配弹窗 -->
    <a-modal
      v-model:open="permModalOpen"
      title="分配权限"
      width="640"
      :destroy-on-close="true"
      :confirm-loading="permSubmitting"
      @ok="onSubmitPermissions"
    >
      <a-spin :spinning="permLoading">
        <a-transfer
          v-model:target-keys="targetPermissionKeys"
          :data-source="permissionTransferData"
          :titles="['可分配权限', '已分配']"
          :render="(item: { key: string; title: string }) => item.title"
          row-key="key"
          :list-style="{ width: '260px', height: '360px' }"
        />
      </a-spin>
    </a-modal>

    <!-- 停用确认 -->
    <ConfirmDialog
      :open="deactivateConfirmOpen"
      title="停用运营人员"
      content="停用后该运营人员将无法登录，已分配的待办任务需重新分配。可随时激活恢复。"
      @confirm="onConfirmDeactivate"
      @cancel="deactivateConfirmOpen = false"
    />

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="运营人员详情"
      placement="right"
      width="560"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <a-descriptions v-if="detail" :column="1" bordered>
          <a-descriptions-item label="运营人员 ID">{{ detail.operatorId }}</a-descriptions-item>
          <a-descriptions-item label="用户名">{{ detail.username }}</a-descriptions-item>
          <a-descriptions-item label="姓名">{{ detail.name }}</a-descriptions-item>
          <a-descriptions-item label="邮箱">{{ detail.email }}</a-descriptions-item>
          <a-descriptions-item label="角色">
            <a-tag color="cyan">{{ roleLabel(detail.role) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <StatusTag type="operator" :status="detail.status" />
          </a-descriptions-item>
          <a-descriptions-item label="创建时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
          <a-descriptions-item label="最近登录">{{ detail.lastLoginAt ? formatDateTime(detail.lastLoginAt) : '从未登录' }}</a-descriptions-item>
          <a-descriptions-item label="权限码">
            <a-tag v-for="p in detail.permissions" :key="p">{{ p }}</a-tag>
          </a-descriptions-item>
        </a-descriptions>
        <a-divider>审计</a-divider>
        <a-button type="link" :disabled="!detail" @click="goToAuditLogs">查看审计记录</a-button>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { operatorsApi } from '../api/operators.api'
import { rolesApi } from '../api/roles.api'
import type {
  OperatorDto,
  OperatorStatus,
  OperatorRole,
  ListOperatorsParams,
  SaveOperatorDto,
} from '../types/operator.dto'
import { OPERATOR_ROLE_OPTIONS } from '../types/operator.dto'
import { useAuthStore } from '@/shared/auth/auth.store'
import { formatDateTime } from '@/shared/utils/format'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'

const router = useRouter()
const auth = useAuthStore()

interface FilterState {
  keyword: string
  role?: OperatorRole
  status?: OperatorStatus
}

const filters = reactive<FilterState>({
  keyword: '',
  role: undefined,
  status: undefined,
})

const statusOptions = [
  { label: 'Active', value: 'Active' },
  { label: 'Inactive', value: 'Inactive' },
]

function roleLabel(role: OperatorRole): string {
  return OPERATOR_ROLE_OPTIONS.find((o) => o.value === role)?.label ?? role
}

const columns: TableColumnsType = [
  { title: '运营人员 ID', dataIndex: 'operatorId', key: 'operatorId', width: 140, ellipsis: true },
  { title: '用户名', dataIndex: 'username', key: 'username', width: 140 },
  { title: '姓名', dataIndex: 'name', key: 'name', width: 140 },
  { title: '角色', key: 'role', width: 120 },
  { title: '状态', key: 'status', width: 100 },
  { title: '创建时间', key: 'createdAt', width: 160, responsive: ['xl'] },
  { title: '最近登录', dataIndex: 'lastLoginAt', key: 'lastLoginAt', width: 160 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' },
]

const tableData = ref<OperatorDto[]>([])
const loading = ref(false)
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchList() {
  loading.value = true
  try {
    const params: ListOperatorsParams & { page: number; pageSize: number } = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.role) params.role = filters.role
    if (filters.status) params.status = filters.status
    const { data } = await operatorsApi.list(params)
    // 后端无 keyword 参数，前端二次过滤
    let items = data.items
    if (filters.keyword) {
      const kw = filters.keyword.toLowerCase()
      items = items.filter(
        (o) => o.username.toLowerCase().includes(kw) || o.name.toLowerCase().includes(kw),
      )
    }
    tableData.value = items
    pagination.total = items.length
  } catch {
    message.error('加载运营人员列表失败')
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  fetchList()
}

function onReset() {
  filters.keyword = ''
  filters.role = undefined
  filters.status = undefined
  onQuery()
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearch() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    onQuery()
  }, 300)
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

// 新建
const createModalOpen = ref(false)
const creating = ref(false)
const formRef = ref<FormInstance>()
const formData = reactive<SaveOperatorDto>({
  username: '',
  name: '',
  email: '',
  password: '',
  role: 'Operator',
})

const formRules: Record<string, Rule[]> = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
  password: [
    { required: true, message: '请输入初始密码', trigger: 'blur' },
    { min: 8, message: '至少 8 位', trigger: 'blur' },
  ],
  role: [{ required: true, message: '请选择角色', trigger: 'change' }],
}

function onCreate() {
  formData.username = ''
  formData.name = ''
  formData.email = ''
  formData.password = ''
  formData.role = 'Operator'
  createModalOpen.value = true
}

async function onSubmitCreate() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  creating.value = true
  try {
    await operatorsApi.create({ ...formData })
    message.success('运营人员已创建')
    createModalOpen.value = false
    await fetchList()
  } catch {
    message.error('创建失败：用户名可能已存在')
  } finally {
    creating.value = false
  }
}

// 权限分配
const permModalOpen = ref(false)
const permLoading = ref(false)
const permSubmitting = ref(false)
const targetPermissionKeys = ref<string[]>([])
const permissionTransferData = ref<{ key: string; title: string }[]>([])
const currentOperator = ref<OperatorDto | null>(null)

async function onAssignPermissions(record: OperatorDto) {
  currentOperator.value = record
  permModalOpen.value = true
  permLoading.value = true
  try {
    const { data: catalog } = await rolesApi.getPermissionCatalog()
    const all: { key: string; title: string }[] = []
    for (const group of catalog) {
      for (const p of group.permissions) {
        all.push({ key: p.code, title: p.label ? `${p.label} (${p.code})` : p.code })
      }
    }
    permissionTransferData.value = all
    targetPermissionKeys.value = [...record.permissions]
  } catch {
    message.error('加载权限目录失败')
  } finally {
    permLoading.value = false
  }
}

async function onSubmitPermissions() {
  if (!currentOperator.value) return
  permSubmitting.value = true
  try {
    await operatorsApi.updatePermissions(currentOperator.value.operatorId, {
      permissions: targetPermissionKeys.value,
    })
    message.success('权限已更新')
    permModalOpen.value = false
    await fetchList()
  } catch {
    message.error('权限更新失败')
  } finally {
    permSubmitting.value = false
  }
}

// 激活/停用
const deactivateConfirmOpen = ref(false)
const pendingOperator = ref<OperatorDto | null>(null)

function onDeactivate(record: OperatorDto) {
  // 前端拦截：不能停用自己
  if (auth.user && record.operatorId === auth.user.id) {
    message.warning('不能停用自己的账号')
    return
  }
  pendingOperator.value = record
  deactivateConfirmOpen.value = true
}

async function onConfirmDeactivate() {
  deactivateConfirmOpen.value = false
  if (!pendingOperator.value) return
  try {
    await operatorsApi.deactivate(pendingOperator.value.operatorId)
    message.success('已停用')
    await fetchList()
  } catch {
    message.error('停用失败')
  } finally {
    pendingOperator.value = null
  }
}

async function onActivate(record: OperatorDto) {
  try {
    await operatorsApi.activate(record.operatorId)
    message.success('已激活')
    await fetchList()
  } catch {
    message.error('激活失败')
  }
}

// 详情抽屉
const drawerOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<OperatorDto | null>(null)

async function onView(record: OperatorDto) {
  drawerOpen.value = true
  detailLoading.value = true
  try {
    const { data } = await operatorsApi.get(record.operatorId)
    detail.value = data
  } catch {
    message.error('加载详情失败')
  } finally {
    detailLoading.value = false
  }
}

function goToAuditLogs() {
  if (detail.value) {
    router.push({ path: '/audit/audit-logs', query: { operatorId: detail.value.operatorId } })
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped>
.operators {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
</style>
