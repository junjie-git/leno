<!-- web/system-admin/src/modules/02-user-access/views/UserManagement.vue -->
<template>
  <div class="user-management">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="搜索">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="用户名/邮箱"
            allow-clear
            style="width: 220px"
            @search="onSearch"
          />
        </a-form-item>
        <a-form-item label="角色">
          <a-select
            v-model:value="filters.roles"
            mode="multiple"
            placeholder="全部角色"
            allow-clear
            style="width: 200px"
            :options="roleOptions"
            :field-names="{ label: 'label', value: 'value' }"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.statuses"
            mode="multiple"
            placeholder="全部状态"
            allow-clear
            style="width: 180px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item label="注册时间">
          <DateTimeRangePicker v-model:value="filters.dateRange" @change="onDateRangeChange" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
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
        row-key="id"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="未找到匹配用户" action-text="清空筛选条件" @action="onReset" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'roles'">
            <a-tag v-for="r in record.roles" :key="r" color="blue">{{ roleLabel(r) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="user" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'lastLoginAt'">
            {{ record.lastLoginAt ? formatDateTime(record.lastLoginAt) : '—' }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onView(record)">查看</a-button>
              <PermissionGuard permission="user:assign-role">
                <a-button type="link" size="small" @click="onAssignRoles(record)">分配角色</a-button>
              </PermissionGuard>
              <PermissionGuard permission="user:suspend">
                <IdempotencyButton
                  v-if="record.status !== 'Suspended'"
                  type="link"
                  size="small"
                  danger
                  @click="onLock(record)"
                >锁定</IdempotencyButton>
                <IdempotencyButton
                  v-else
                  type="link"
                  size="small"
                  @click="onUnlock(record)"
                >恢复</IdempotencyButton>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="用户详情"
      placement="right"
      width="600"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <a-descriptions v-if="detail" :column="1" bordered>
          <a-descriptions-item label="用户 ID">{{ detail.id }}</a-descriptions-item>
          <a-descriptions-item label="用户名">{{ detail.username }}</a-descriptions-item>
          <a-descriptions-item label="邮箱">{{ detail.email }}</a-descriptions-item>
          <a-descriptions-item label="手机">{{ detail.phone || '—' }}</a-descriptions-item>
          <a-descriptions-item label="角色">
            <a-tag v-for="r in detail.roles" :key="r" color="blue">{{ roleLabel(r) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="状态">
            <StatusTag type="user" :status="detail.status" />
          </a-descriptions-item>
          <a-descriptions-item label="注册时间">{{ formatDateTime(detail.createdAt) }}</a-descriptions-item>
          <a-descriptions-item label="最近登录">
            {{ detail.lastLoginAt
              ? `${formatDateTime(detail.lastLoginAt)}（IP ${detail.lastLoginIp ?? '—'}）`
              : '从未登录' }}
          </a-descriptions-item>
        </a-descriptions>
        <a-divider>审计记录</a-divider>
        <a-button type="link" :disabled="!detail" @click="goToAuditLogs">查看审计记录</a-button>
      </a-spin>
    </a-drawer>

    <!-- 区域 D：角色分配弹窗 -->
    <a-modal
      v-model:open="rolesModalOpen"
      title="分配角色"
      :destroy-on-close="true"
      :confirm-loading="submitting"
      @ok="onSubmitRoles"
    >
      <a-transfer
        v-model:target-keys="targetRoleIds"
        :data-source="roleTransferData"
        :titles="['可分配角色', '已分配']"
        :render="(item: { key: string; title: string }) => item.title"
        row-key="key"
      />
    </a-modal>

    <!-- 锁定确认对话框 -->
    <ConfirmDialog
      :open="lockConfirmOpen"
      danger
      title="锁定用户"
      content="锁定后该用户将无法登录，关联的进行中订单不受影响。此操作可逆，可随时恢复。"
      @confirm="onConfirmLock"
      @cancel="lockConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { usersApi } from '../api/users.api'
import { rolesApi } from '../api/roles.api'
import type { UserDto, UserStatus, ListUsersParams } from '../types/user.dto'
import type { RoleDto } from '../types/role.dto'
import { formatDateTime } from '@/shared/utils/format'
import StatusTag from '@/shared/components/StatusTag.vue'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'

const router = useRouter()

interface FilterState {
  keyword: string
  roles: string[]
  statuses: UserStatus[]
  dateRange: [string, string] | null
}

const filters = reactive<FilterState>({
  keyword: '',
  roles: [],
  statuses: [],
  dateRange: null,
})

const statusOptions = [
  { label: 'Active', value: 'Active' },
  { label: 'Suspended', value: 'Suspended' },
  { label: 'Locked', value: 'Locked' },
]

const roleOptions = ref<{ label: string; value: string }[]>([])
const roleMap = ref<Map<string, string>>(new Map())

function roleLabel(id: string): string {
  return roleMap.value.get(id) ?? id
}

const columns: TableColumnsType = [
  { title: '用户 ID', dataIndex: 'id', key: 'id', width: 140, ellipsis: true },
  { title: '用户名', dataIndex: 'username', key: 'username', width: 140 },
  { title: '邮箱', dataIndex: 'email', key: 'email', width: 220, ellipsis: true },
  { title: '角色', key: 'roles', width: 180 },
  { title: '状态', key: 'status', width: 100 },
  { title: '注册时间', key: 'createdAt', width: 160, responsive: ['xl'] },
  { title: '最近登录', dataIndex: 'lastLoginAt', key: 'lastLoginAt', width: 160 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' },
]

const tableData = ref<UserDto[]>([])
const loading = ref(false)
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchUsers() {
  loading.value = true
  try {
    const params: ListUsersParams & { page: number; pageSize: number } = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.keyword) params.keyword = filters.keyword
    if (filters.roles.length) params.roles = filters.roles
    if (filters.statuses.length) params.statuses = filters.statuses
    if (filters.dateRange && filters.dateRange.length === 2) {
      params.fromTime = filters.dateRange[0]
      params.toTime = filters.dateRange[1]
    }
    const { data } = await usersApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch {
    message.error('加载用户列表失败')
  } finally {
    loading.value = false
  }
}

async function fetchRoleOptions() {
  try {
    const { data } = await rolesApi.list({ page: 1, pageSize: 100 })
    roleOptions.value = data.items.map((r: RoleDto) => ({ label: r.name, value: r.id }))
    roleMap.value = new Map(data.items.map((r: RoleDto) => [r.id, r.name]))
  } catch {
    roleOptions.value = []
    roleMap.value = new Map()
  }
}

function onQuery() {
  pagination.current = 1
  fetchUsers()
}

function onReset() {
  filters.keyword = ''
  filters.roles = []
  filters.statuses = []
  filters.dateRange = null
  onQuery()
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearch() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    onQuery()
  }, 300)
}

function onDateRangeChange(value: [string, string]) {
  filters.dateRange = value
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchUsers()
}

// 详情抽屉
const drawerOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<UserDto | null>(null)

async function onView(record: UserDto) {
  drawerOpen.value = true
  detailLoading.value = true
  try {
    const { data } = await usersApi.get(record.id)
    detail.value = data
  } catch {
    message.error('加载用户详情失败')
  } finally {
    detailLoading.value = false
  }
}

function goToAuditLogs() {
  if (detail.value) {
    router.push({ path: '/audit/audit-logs', query: { operatorId: detail.value.id } })
  }
}

// 分配角色
const rolesModalOpen = ref(false)
const submitting = ref(false)
const targetRoleIds = ref<string[]>([])
const roleTransferData = ref<{ key: string; title: string }[]>([])
const currentUser = ref<UserDto | null>(null)

async function onAssignRoles(record: UserDto) {
  currentUser.value = record
  rolesModalOpen.value = true
  try {
    const { data } = await rolesApi.list({ page: 1, pageSize: 100 })
    roleTransferData.value = data.items.map((r: RoleDto) => ({ key: r.id, title: r.name }))
    targetRoleIds.value = [...record.roles]
  } catch {
    message.error('加载角色列表失败')
  }
}

async function onSubmitRoles() {
  if (!currentUser.value) return
  submitting.value = true
  try {
    await usersApi.assignRoles(currentUser.value.id, { roleIds: targetRoleIds.value })
    message.success('角色已分配')
    rolesModalOpen.value = false
    await fetchUsers()
  } catch {
    message.error('角色分配失败')
  } finally {
    submitting.value = false
  }
}

// 锁定/恢复
const lockConfirmOpen = ref(false)
const pendingAction = ref<{ id: string; status: 'Active' | 'Suspended' } | null>(null)

function onLock(record: UserDto) {
  pendingAction.value = { id: record.id, status: 'Suspended' }
  lockConfirmOpen.value = true
}

function onUnlock(record: UserDto) {
  pendingAction.value = { id: record.id, status: 'Active' }
  void doUpdateStatus()
}

async function onConfirmLock() {
  lockConfirmOpen.value = false
  await doUpdateStatus()
}

async function doUpdateStatus() {
  if (!pendingAction.value) return
  const action = pendingAction.value
  try {
    const body =
      action.status === 'Suspended'
        ? { status: 'Suspended' as const, reason: '管理员手动锁定' }
        : { status: 'Active' as const }
    await usersApi.updateStatus(action.id, body)
    message.success(action.status === 'Suspended' ? '已锁定' : '已恢复')
    await fetchUsers()
  } catch {
    message.error('状态变更失败')
  } finally {
    pendingAction.value = null
  }
}

onMounted(() => {
  fetchRoleOptions()
  fetchUsers()
})
</script>

<style scoped>
.user-management {
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
