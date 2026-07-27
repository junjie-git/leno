<!-- web/system-admin/src/modules/02-user-access/views/OAuthClients.vue -->
<template>
  <div class="oauth-clients">
    <!-- 区域 A：操作条 -->
    <a-card :bordered="false" class="action-card">
      <a-space>
        <a-button type="primary" @click="onCreate">
          <template #icon><PlusOutlined /></template>
          新建提供方
        </a-button>
        <a-select
          v-model:value="statusFilter"
          style="width: 140px"
          :options="statusFilterOptions"
          @change="onFilterChange"
        />
        <a-button @click="fetchList">刷新</a-button>
      </a-space>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :data-source="filteredData"
        :loading="loading"
        row-key="provider"
        :pagination="false"
      >
        <template #emptyText>
          <EmptyState description="暂无 OAuth 提供方配置" action-text="新建提供方" @action="onCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'provider'">
            <span class="provider-name">{{ providerLabel(record.provider) }}</span>
          </template>
          <template v-else-if="column.key === 'clientSecretMasked'">
            <span class="secret-masked">{{ record.clientSecretMasked }}</span>
          </template>
          <template v-else-if="column.key === 'enabled'">
            <StatusTag type="oauth" :status="record.enabled ? 'Enabled' : 'Disabled'" />
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onEdit(record)">编辑</a-button>
              <PermissionGuard permission="oauth:write">
                <a-button
                  v-if="!record.enabled"
                  type="link"
                  size="small"
                  @click="onToggle(record, 'enable')"
                >启用</a-button>
                <a-button
                  v-else
                  type="link"
                  size="small"
                  @click="onToggle(record, 'disable')"
                >禁用</a-button>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新建/编辑弹窗 -->
    <a-modal
      v-model:open="formModalOpen"
      :title="formMode === 'create' ? '新建 OAuth 提供方' : `编辑 ${providerLabel(formProvider)}`"
      width="560"
      :destroy-on-close="true"
      :confirm-loading="formSubmitting"
      @ok="onSubmitForm"
    >
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item label="提供方" name="provider">
          <a-select
            v-model:value="formData.provider"
            :disabled="formMode === 'edit'"
            :options="providerOptions"
            placeholder="请选择提供方"
          />
        </a-form-item>
        <a-form-item label="Client ID" name="clientId">
          <a-input v-model:value="formData.clientId" placeholder="请输入 Client ID" />
        </a-form-item>
        <a-form-item :label="formMode === 'edit' ? 'Client Secret（留空保留原密钥）' : 'Client Secret'" name="clientSecret">
          <a-input-password
            v-model:value="formData.clientSecret"
            autocomplete="new-password"
            :placeholder="formMode === 'edit' ? '留空则保留原密钥' : '请输入 Client Secret'"
          />
        </a-form-item>
        <a-form-item label="Scopes" name="scopes">
          <a-select
            v-model:value="formData.scopes"
            mode="tags"
            placeholder="输入 scope 后回车"
            :token-separators="[',', ' ']"
          />
        </a-form-item>
        <a-form-item label="Authorization Endpoint" name="authorizationEndpoint">
          <a-input v-model:value="formData.authorizationEndpoint" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="Token Endpoint" name="tokenEndpoint">
          <a-input v-model:value="formData.tokenEndpoint" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="UserInfo Endpoint" name="userInfoEndpoint">
          <a-input v-model:value="formData.userInfoEndpoint" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="回调 URL" name="redirectUri">
          <a-input v-model:value="formData.redirectUri" placeholder="/callback/{provider}" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 启停确认 -->
    <ConfirmDialog
      :open="toggleConfirmOpen"
      :title="toggleAction === 'disable' ? '禁用提供方' : '启用提供方'"
      :content="toggleAction === 'disable'
        ? '禁用后用户将无法通过该提供方登录，已绑定的账号不受影响。可随时重新启用。'
        : '启用后用户可通过该提供方登录。'"
      @confirm="onConfirmToggle"
      @cancel="toggleConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { oauthClientsApi } from '../api/oauth-clients.api'
import type {
  OAuthClientDto,
  UpdateOAuthClientDto,
} from '../types/oauth-client.dto'
import {
  SUPPORTED_OAUTH_PROVIDERS,
  OAUTH_PROVIDER_LABELS,
} from '../types/oauth-client.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'

const tableData = ref<OAuthClientDto[]>([])
const loading = ref(false)
const statusFilter = ref<'all' | 'enabled' | 'disabled'>('all')

const statusFilterOptions = [
  { label: '全部', value: 'all' },
  { label: '启用', value: 'enabled' },
  { label: '禁用', value: 'disabled' },
]

const filteredData = computed(() => {
  if (statusFilter.value === 'all') return tableData.value
  if (statusFilter.value === 'enabled') return tableData.value.filter((c) => c.enabled)
  return tableData.value.filter((c) => !c.enabled)
})

const providerOptions = SUPPORTED_OAUTH_PROVIDERS.map((p) => ({
  label: OAUTH_PROVIDER_LABELS[p] ?? p,
  value: p,
}))

function providerLabel(provider: string): string {
  return OAUTH_PROVIDER_LABELS[provider] ?? provider
}

const columns: TableColumnsType = [
  { title: '提供方', key: 'provider', width: 120 },
  { title: 'Client ID', dataIndex: 'clientId', key: 'clientId', width: 200, ellipsis: true },
  { title: 'Secret', key: 'clientSecretMasked', width: 140, responsive: ['md'] },
  { title: 'Scopes', dataIndex: 'scopes', key: 'scopes', width: 160, responsive: ['xl'], customRender: ({ text }: { text: string[] }) => (text ?? []).join(', ') || '—' },
  { title: '回调 URL', dataIndex: 'redirectUri', key: 'redirectUri', width: 200, ellipsis: true },
  { title: '状态', key: 'enabled', width: 100 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' },
]

async function fetchList() {
  loading.value = true
  try {
    const { data } = await oauthClientsApi.list()
    tableData.value = data
  } catch {
    message.error('加载 OAuth 客户端列表失败')
  } finally {
    loading.value = false
  }
}

function onFilterChange() {
  // 前端过滤，无需重新请求
}

// 新建/编辑
const formModalOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const formProvider = ref<string>('')
const formRef = ref<FormInstance>()
const formSubmitting = ref(false)

const formData = reactive<{ provider: string } & UpdateOAuthClientDto>({
  provider: '',
  clientId: '',
  clientSecret: '',
  scopes: [],
  authorizationEndpoint: '',
  tokenEndpoint: '',
  userInfoEndpoint: '',
  redirectUri: '',
})

const formRules: Record<string, Rule[]> = {
  provider: [{ required: true, message: '请选择提供方', trigger: 'change' }],
  clientId: [{ required: true, message: '请输入 Client ID', trigger: 'blur' }],
  authorizationEndpoint: [{ required: true, message: '请输入 Authorization Endpoint', trigger: 'blur' }],
  tokenEndpoint: [{ required: true, message: '请输入 Token Endpoint', trigger: 'blur' }],
  userInfoEndpoint: [{ required: true, message: '请输入 UserInfo Endpoint', trigger: 'blur' }],
  redirectUri: [{ required: true, message: '请输入回调 URL', trigger: 'blur' }],
}

function resetForm() {
  formData.provider = ''
  formData.clientId = ''
  formData.clientSecret = ''
  formData.scopes = []
  formData.authorizationEndpoint = ''
  formData.tokenEndpoint = ''
  formData.userInfoEndpoint = ''
  formData.redirectUri = ''
}

function onCreate() {
  formMode.value = 'create'
  formProvider.value = ''
  resetForm()
  formModalOpen.value = true
}

function onEdit(record: OAuthClientDto) {
  formMode.value = 'edit'
  formProvider.value = record.provider
  formData.provider = record.provider
  formData.clientId = record.clientId
  formData.clientSecret = ''
  formData.scopes = [...record.scopes]
  formData.authorizationEndpoint = record.authorizationEndpoint
  formData.tokenEndpoint = record.tokenEndpoint
  formData.userInfoEndpoint = record.userInfoEndpoint
  formData.redirectUri = record.redirectUri
  formModalOpen.value = true
}

async function onSubmitForm() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  // 新建时 clientSecret 必填
  if (formMode.value === 'create' && !formData.clientSecret) {
    message.warning('请输入 Client Secret')
    return
  }
  // 构造请求体（编辑时 clientSecret 留空则后端保留原密钥）
  const body: UpdateOAuthClientDto = {
    clientId: formData.clientId,
    clientSecret: formData.clientSecret,
    scopes: formData.scopes,
    authorizationEndpoint: formData.authorizationEndpoint,
    tokenEndpoint: formData.tokenEndpoint,
    userInfoEndpoint: formData.userInfoEndpoint,
    redirectUri: formData.redirectUri,
  }
  formSubmitting.value = true
  try {
    if (formMode.value === 'create') {
      await oauthClientsApi.create(formData.provider, body)
      message.success('OAuth 客户端配置已创建（默认禁用，需显式启用）')
    } else {
      await oauthClientsApi.update(formData.provider, body)
      message.success('配置已更新')
    }
    formModalOpen.value = false
    await fetchList()
  } catch {
    message.error(formMode.value === 'create' ? '该提供方可能已存在配置' : '更新失败')
  } finally {
    formSubmitting.value = false
  }
}

// 启停
const toggleConfirmOpen = ref(false)
const toggleAction = ref<'enable' | 'disable'>('enable')
const toggleTarget = ref<OAuthClientDto | null>(null)

function onToggle(record: OAuthClientDto, action: 'enable' | 'disable') {
  if (action === 'enable' && (!record.clientId || record.clientSecretMasked === '')) {
    message.warning('启用前需填写 Client ID 与 Secret')
    return
  }
  toggleTarget.value = record
  toggleAction.value = action
  toggleConfirmOpen.value = true
}

async function onConfirmToggle() {
  toggleConfirmOpen.value = false
  if (!toggleTarget.value) return
  const target = toggleTarget.value
  try {
    if (toggleAction.value === 'enable') {
      await oauthClientsApi.enable(target.provider)
      message.success('已启用')
    } else {
      await oauthClientsApi.disable(target.provider)
      message.success('已禁用')
    }
    await fetchList()
  } catch {
    message.error('状态变更失败')
  } finally {
    toggleTarget.value = null
  }
}

onMounted(() => {
  fetchList()
})
</script>

<style scoped>
.oauth-clients {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.action-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
.provider-name {
  font-weight: 500;
}
.secret-masked {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
