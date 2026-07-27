<!-- web/system-admin/src/modules/03-system-governance/views/Announcements.vue -->
<!-- 公告管理：筛选 + 表格 + 新建/编辑弹窗 + 发布/撤回确认 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import {
  PlusOutlined,
  EditOutlined,
  SendOutlined,
  RollbackOutlined,
  NotificationOutlined,
} from '@ant-design/icons-vue'
import { announcementsApi } from '../api/announcements.api'
import type {
  AnnouncementDto,
  SaveAnnouncementDto,
  AnnouncementType,
  AnnouncementStatus,
  AnnouncementAudience,
} from '../types/announcement.dto'
import {
  ANNOUNCEMENT_TYPE_LABELS,
  ANNOUNCEMENT_STATUS_LABELS,
  ANNOUNCEMENT_AUDIENCE_LABELS,
} from '../types/announcement.dto'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

interface FilterState {
  type: AnnouncementType[]
  status: AnnouncementStatus[]
  page: number
  pageSize: number
}

interface FormState {
  announcementId?: string
  title: string
  type: AnnouncementType
  audiences: AnnouncementAudience[]
  effectiveRange: [Dayjs, Dayjs] | null
  content: string
  isPinned: boolean
}

const loading = ref(false)
const dataList = ref<AnnouncementDto[]>([])
const total = ref(0)
const filter = reactive<FilterState>({
  type: [],
  status: [],
  page: 1,
  pageSize: 20,
})

const typeOptions: { label: string; value: AnnouncementType }[] = [
  { label: '系统维护', value: 'SystemMaintenance' },
  { label: '活动通知', value: 'ActivityNotification' },
  { label: '政策变更', value: 'PolicyChange' },
  { label: '紧急公告', value: 'Urgent' },
]

const statusOptions: { label: string; value: AnnouncementStatus }[] = [
  { label: '草稿', value: 'Draft' },
  { label: '已发布', value: 'Published' },
  { label: '已撤回', value: 'Unpublished' },
]

const audienceOptions: { label: string; value: AnnouncementAudience }[] = [
  { label: '买家', value: 'Buyer' },
  { label: '卖家', value: 'Seller' },
  { label: '运营', value: 'Operator' },
]

const columns = computed(() => [
  { title: '标题', dataIndex: 'title', key: 'title', ellipsis: true },
  { title: '类型', key: 'type', width: 110 },
  { title: '状态', key: 'status', width: 100 },
  { title: '发布范围', key: 'audiences', width: 160 },
  { title: '生效起止', key: 'effective', width: 240 },
  { title: '操作', key: 'action', width: 240, fixed: 'right' as const },
])

// 弹窗
const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const submitting = ref(false)
const form = reactive<FormState>({
  title: '',
  type: 'SystemMaintenance',
  audiences: ['Buyer'],
  effectiveRange: null,
  content: '',
  isPinned: false,
})

// 确认弹窗
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'publish' | 'unpublish'; announcement: AnnouncementDto } | null>(null)
const confirmDanger = computed(() => confirmAction.value?.kind === 'unpublish')
const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'publish' ? '发布公告' : '撤回公告')
const confirmContent = computed(() =>
  confirmAction.value?.kind === 'publish'
    ? '发布后公告将对所选范围立即生效，买家 APP 与卖家后台将展示。撤回可恢复。'
    : '撤回后公告将立即从所有端下线，已读记录保留。可重新编辑后再次发布。')

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const params = {
      type: filter.type.length ? filter.type : undefined,
      status: filter.status.length ? filter.status : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await announcementsApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载公告失败')
  } finally {
    loading.value = false
  }
}

function onSearch(): void {
  filter.page = 1
  loadList()
}

function onTableChange(pag: { current?: number; pageSize?: number }): void {
  filter.page = pag.current ?? 1
  filter.pageSize = pag.pageSize ?? 20
  loadList()
}

function openCreate(): void {
  modalMode.value = 'create'
  const now = dayjs()
  Object.assign(form, {
    announcementId: undefined,
    title: '',
    type: 'SystemMaintenance',
    audiences: ['Buyer'],
    effectiveRange: [now, now.add(1, 'day')] as [Dayjs, Dayjs],
    content: '',
    isPinned: false,
  })
  modalVisible.value = true
}

function openEdit(ann: AnnouncementDto): void {
  modalMode.value = 'edit'
  Object.assign(form, {
    announcementId: ann.announcementId,
    title: ann.title,
    type: ann.type,
    audiences: [...ann.audiences],
    effectiveRange: [dayjs(ann.effectiveFrom), dayjs(ann.effectiveTo)] as [Dayjs, Dayjs],
    content: ann.content,
    isPinned: ann.isPinned,
  })
  modalVisible.value = true
}

async function onSubmit(): Promise<void> {
  if (!form.title.trim()) {
    message.error('标题必填')
    return
  }
  if (!form.audiences.length) {
    message.error('发布范围至少选一项')
    return
  }
  if (!form.effectiveRange) {
    message.error('生效起止必填')
    return
  }
  const [from, to] = form.effectiveRange
  if (!from.isBefore(to)) {
    message.error('生效结束时间必须晚于开始时间')
    return
  }
  if (!form.content.trim()) {
    message.error('正文必填')
    return
  }
  submitting.value = true
  try {
    const body: SaveAnnouncementDto = {
      title: form.title.trim(),
      type: form.type,
      audiences: form.audiences,
      effectiveFrom: from.toISOString(),
      effectiveTo: to.toISOString(),
      content: form.content,
      isPinned: form.isPinned,
    }
    if (modalMode.value === 'create') {
      await announcementsApi.create(body)
      message.success('公告已创建（草稿态）')
    } else if (form.announcementId) {
      await announcementsApi.update(form.announcementId, body)
      message.success('公告已更新')
    }
    modalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('保存失败')
  } finally {
    submitting.value = false
  }
}

function askPublish(ann: AnnouncementDto): void {
  confirmAction.value = { kind: 'publish', announcement: ann }
  confirmVisible.value = true
}

function askUnpublish(ann: AnnouncementDto): void {
  confirmAction.value = { kind: 'unpublish', announcement: ann }
  confirmVisible.value = true
}

async function onConfirmAction(): Promise<void> {
  if (!confirmAction.value) return
  const { kind, announcement } = confirmAction.value
  try {
    if (kind === 'publish') {
      await announcementsApi.publish(announcement.announcementId)
      message.success('公告已发布')
    } else {
      await announcementsApi.unpublish(announcement.announcementId)
      message.success('公告已撤回')
    }
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function openPublicView(): void {
  // 打开新窗口预览公开公告页
  window.open('/api/announcements', '_blank')
}

function statusTagColor(status: AnnouncementStatus): string {
  if (status === 'Published') return 'success'
  if (status === 'Unpublished') return 'warning'
  return 'default'
}

function audiencesText(audiences: AnnouncementAudience[]): string {
  return audiences.map((a) => ANNOUNCEMENT_AUDIENCE_LABELS[a]).join('、')
}

function effectiveText(from: string, to: string): string {
  return `${formatDateTime(from)} ~ ${formatDateTime(to)}`
}

onMounted(() => {
  loadList()
})
</script>

<template>
  <div class="announcements-page">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-space :size="12" wrap>
        <a-select
          v-model:value="filter.type"
          mode="multiple"
          placeholder="类型"
          allow-clear
          style="width: 200px"
          :options="typeOptions"
        />
        <a-select
          v-model:value="filter.status"
          mode="multiple"
          placeholder="状态"
          allow-clear
          style="width: 180px"
          :options="statusOptions"
        />
        <a-button type="primary" @click="onSearch">查询</a-button>
        <PermissionGuard permission="announcement:write">
          <a-button type="primary" @click="openCreate">
            <PlusOutlined />新增公告
          </a-button>
        </PermissionGuard>
      </a-space>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" style="margin-top: 16px">
      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :row-key="(r: AnnouncementDto) => r.announcementId"
        :pagination="{
          current: filter.page,
          pageSize: filter.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无公告" action-text="新增公告" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'title'">
            <a-space :size="4">
              <a-tag v-if="record.isPinned" color="red">置顶</a-tag>
              <span>{{ record.title }}</span>
            </a-space>
          </template>
          <template v-else-if="column.key === 'type'">
            <a-tag color="blue">{{ ANNOUNCEMENT_TYPE_LABELS[record.type as AnnouncementType] }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="statusTagColor(record.status)">
              {{ ANNOUNCEMENT_STATUS_LABELS[record.status as AnnouncementStatus] }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'audiences'">
            {{ audiencesText(record.audiences) }}
          </template>
          <template v-else-if="column.key === 'effective'">
            {{ effectiveText(record.effectiveFrom, record.effectiveTo) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space :size="4">
              <!-- 仅草稿态可编辑 -->
              <PermissionGuard permission="announcement:write">
                <a-tooltip :title="record.status !== 'Draft' ? '仅草稿态可编辑' : ''">
                  <a-button
                    type="link"
                    size="small"
                    :disabled="record.status !== 'Draft'"
                    @click="openEdit(record)"
                  >
                    <EditOutlined />编辑
                  </a-button>
                </a-tooltip>
              </PermissionGuard>
              <!-- 仅草稿态可发布，需 publish 权限 -->
              <PermissionGuard permission="announcement:publish">
                <a-button
                  v-if="record.status === 'Draft'"
                  type="link"
                  size="small"
                  @click="askPublish(record)"
                >
                  <SendOutlined />发布
                </a-button>
              </PermissionGuard>
              <!-- 仅已发布态可撤回，需 publish 权限 -->
              <PermissionGuard permission="announcement:publish">
                <a-button
                  v-if="record.status === 'Published'"
                  type="link"
                  size="small"
                  danger
                  @click="askUnpublish(record)"
                >
                  <RollbackOutlined />撤回
                </a-button>
              </PermissionGuard>
              <a-button type="link" size="small" @click="openPublicView">
                <NotificationOutlined />公开页
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新增公告' : '编辑公告'"
      width="800px"
      :confirm-loading="submitting"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="标题" required>
          <a-input v-model:value="form.title" placeholder="公告标题" :maxlength="100" show-count />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item label="类型" required>
              <a-select v-model:value="form.type" :options="typeOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="10">
            <a-form-item label="发布范围" required>
              <a-select
                v-model:value="form.audiences"
                mode="multiple"
                :options="audienceOptions"
                placeholder="选择展示端"
              />
            </a-form-item>
          </a-col>
          <a-col :span="6">
            <a-form-item label="置顶">
              <a-switch v-model:checked="form.isPinned" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="生效起止" required>
          <a-range-picker
            v-model:value="form.effectiveRange"
            show-time
            format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item label="正文" required>
          <a-textarea
            v-model:value="form.content"
            :rows="8"
            placeholder="公告正文（支持纯文本）"
            :maxlength="5000"
            show-count
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 发布/撤回确认弹窗 -->
    <ConfirmDialog
      :open="confirmVisible"
      :danger="confirmDanger"
      :title="confirmTitle"
      :content="confirmContent"
      @confirm="onConfirmAction"
      @cancel="confirmVisible = false"
    />
  </div>
</template>

<style scoped>
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
</style>
