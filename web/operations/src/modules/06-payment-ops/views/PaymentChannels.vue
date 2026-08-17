<!-- web/operations/src/modules/06-payment-ops/views/PaymentChannels.vue -->
<template>
  <div class="payment-channels">
    <!-- 加载失败兜底 -->
    <a-card v-if="errorMessage" :bordered="false">
      <EmptyState
        :description="`加载支付渠道配置失败：${errorMessage}`"
        action-text="重试"
        @action="fetchChannels"
      />
    </a-card>

    <template v-else>
      <!-- 区域 A：渠道列表（按渠道分组卡片，含状态点与配置项数） -->
      <a-card :bordered="false" class="channel-panel" title="支付渠道">
        <a-spin :spinning="loading">
          <EmptyState
            v-if="!loading && groups.length === 0"
            description="暂无支付渠道配置，请联系系统管理员初始化"
          />
          <ul v-else class="channel-list">
            <li
              v-for="group in groups"
              :key="group.channel"
              class="channel-item"
              :class="{ active: group.channel === selectedChannel }"
              :aria-label="`${CHANNEL_META[group.channel].label}，${group.items.length} 项配置，${group.allEnabled ? '已启用' : '部分停用'}`"
              @click="onSelectChannel(group.channel)"
            >
              <span
                class="channel-badge"
                :style="{ background: CHANNEL_META[group.channel].color }"
                :aria-hidden="true"
              >
                {{ CHANNEL_META[group.channel].short }}
              </span>
              <div class="channel-meta">
                <div class="channel-name">
                  <span
                    class="status-dot"
                    :style="{ background: group.allEnabled ? '#52C41A' : '#8C8C8C' }"
                    :aria-label="group.allEnabled ? '启用' : '停用'"
                  />
                  {{ CHANNEL_META[group.channel].label }}
                </div>
                <div class="channel-sub">
                  {{ group.items.length }} 项配置 · {{ group.enabledCount }}/{{ group.items.length }} 启用
                </div>
              </div>
            </li>
          </ul>
        </a-spin>
      </a-card>

      <!-- 区域 B/C：配置详情面板 + 编辑/启停操作 -->
      <a-card :bordered="false" class="detail-panel">
        <template v-if="currentGroup">
          <div class="detail-header">
            <span
              class="channel-badge channel-badge-lg"
              :style="{ background: CHANNEL_META[currentGroup.channel].color }"
              :aria-hidden="true"
            >
              {{ CHANNEL_META[currentGroup.channel].short }}
            </span>
            <div class="detail-title">
              <div class="detail-name">
                {{ CHANNEL_META[currentGroup.channel].label }}配置
                <a-tag :color="currentGroup.allEnabled ? 'success' : 'default'">
                  {{ currentGroup.allEnabled ? '已启用' : '部分停用' }}
                </a-tag>
              </div>
              <div class="channel-sub">
                最后更新：{{ currentGroup.lastUpdatedBy || '—' }} ·
                {{ formatDateTime(currentGroup.lastUpdatedAt) }}
              </div>
            </div>
            <a-button :loading="loading" @click="fetchChannels">刷新</a-button>
          </div>

          <a-table
            :columns="columns"
            :data-source="currentGroup.items"
            :loading="loading"
            :pagination="false"
            :row-key="(record: ChannelConfigItemDto) => record.id"
            size="middle"
          >
            <template #emptyText>
              <EmptyState description="该渠道暂无配置项" />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'key'">
                <div class="mono-cell">{{ record.key }}</div>
                <div v-if="record.description" class="cell-sub">{{ record.description }}</div>
              </template>
              <template v-else-if="column.key === 'value'">
                <a-tooltip v-if="!record.isSensitive && record.value.length > 32" :title="record.value">
                  <span class="value-cell ellipsis">{{ record.value }}</span>
                </a-tooltip>
                <span v-else-if="!record.isSensitive" class="value-cell">{{ record.value }}</span>
                <span v-else class="value-cell masked" :aria-label="`${record.key} 已脱敏`">
                  <LockOutlined class="masked-icon" />
                  {{ record.value || '••••••' }}
                </span>
              </template>
              <template v-else-if="column.key === 'enabled'">
                <a-tag :color="record.enabled ? 'success' : 'default'" :aria-label="record.enabled ? '已启用' : '已停用'">
                  {{ record.enabled ? '已启用' : '已停用' }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'updatedAt'">
                <div>{{ record.updatedBy || '—' }}</div>
                <div class="cell-sub">{{ formatDateTime(record.updatedAt) }}</div>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space :size="4">
                  <a-button type="link" size="small" :aria-label="`编辑 ${record.key}`" @click="onOpenEdit(record)">
                    <template #icon><EditOutlined /></template>
                    编辑
                  </a-button>
                  <a-button
                    v-if="record.enabled"
                    type="link"
                    size="small"
                    danger
                    :aria-label="`停用 ${record.key}`"
                    @click="onConfirmDisable(record)"
                  >
                    <template #icon><StopOutlined /></template>
                    停用
                  </a-button>
                  <a-button
                    v-else
                    type="link"
                    size="small"
                    :loading="togglingId === record.id"
                    :aria-label="`启用 ${record.key}`"
                    @click="onEnable(record)"
                  >
                    <template #icon><PlayCircleOutlined /></template>
                    启用
                  </a-button>
                </a-space>
              </template>
            </template>
          </a-table>

          <div class="panel-footer-hint">
            敏感字段脱敏展示，编辑时留空表示保留原值；当前版本未提供渠道测试连接端点，配置正确性请通过渠道后台核对。
          </div>
        </template>
        <EmptyState v-else description="请选择左侧渠道查看配置详情" />
      </a-card>
    </template>

    <!-- 区域 C：编辑配置项弹窗 -->
    <a-modal
      v-model:open="editOpen"
      :title="editingItem ? `编辑配置 - ${editingItem.key}` : '编辑配置'"
      :confirm-loading="saving"
      ok-text="保存"
      cancel-text="取消"
      :destroy-on-close="true"
      @ok="onSubmitEdit"
    >
      <a-form v-if="editingItem" layout="vertical" class="edit-form">
        <a-form-item label="所属渠道">
          <a-input :value="CHANNEL_META[editingItem.channel].label" disabled />
        </a-form-item>
        <a-form-item label="当前值">
          <a-input :value="editingItem.isSensitive ? (editingItem.value || '••••••') : editingItem.value" disabled />
        </a-form-item>
        <a-form-item label="新配置值" required>
          <a-input-password
            v-if="editingItem.isSensitive"
            v-model:value="editForm.value"
            :placeholder="`输入新的 ${editingItem.key}，留空表示不修改原值`"
            autocomplete="new-password"
          />
          <a-input
            v-else
            v-model:value="editForm.value"
            :placeholder="`输入新的 ${editingItem.key}`"
            allow-clear
          />
          <div v-if="editingItem.isSensitive" class="field-hint">敏感字段已脱敏展示，留空提交将保留原值</div>
        </a-form-item>
        <a-form-item label="配置说明">
          <a-input
            v-model:value="editForm.description"
            placeholder="配置项用途说明（可选）"
            :maxlength="100"
            allow-clear
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 区域 D：停用二次确认（危险操作） -->
    <ConfirmDialog
      :open="disableOpen"
      danger
      title="确认停用配置项"
      :content="disableContent"
      @confirm="onDisable"
      @cancel="disableOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { EditOutlined, PlayCircleOutlined, StopOutlined, LockOutlined } from '@ant-design/icons-vue'
import { EmptyState, ConfirmDialog } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { channelApi } from '../api/channel.api'
import type { ChannelConfigItemDto } from '../types/channel.dto'
import type { PaymentChannelType } from '../types/payment.dto'

/**
 * 支付渠道配置页（06-payment-ops）
 *
 * 左右分栏布局（md §2）：
 * - 左侧渠道列表：按渠道分组卡片（徽标/状态点/配置项数），≥1200px 侧栏、窄屏横向滚动条带
 * - 右侧详情面板：选中渠道的配置项表格（敏感字段脱敏 + 启停 + 编辑）
 *
 * 交互要点：
 * - 编辑敏感字段留空提交 = 保留原值（后端跳过空值）
 * - 停用为危险操作，强制 ConfirmDialog 二次确认
 * - 更新/启停后基于响应局部刷新对应配置项，不重新拉取全量列表
 * - md 未定义测试连接端点：不渲染「测试连接」按钮，以页脚提示说明
 */

/** 渠道展示映射（徽标单字 + 状态色，与支付/退款页保持一致） */
const CHANNEL_META: Record<PaymentChannelType, { label: string; color: string; short: string }> = {
  WeChat: { label: '微信支付', color: '#07C160', short: '微' },
  Alipay: { label: '支付宝', color: '#1677FF', short: '支' },
  Other: { label: '其他渠道', color: '#FAAD14', short: '他' },
}

/** 渠道分组固定展示顺序 */
const CHANNEL_ORDER: PaymentChannelType[] = ['WeChat', 'Alipay', 'Other']

/** 渠道分组视图（左侧渠道卡数据源） */
interface ChannelGroup {
  channel: PaymentChannelType
  items: ChannelConfigItemDto[]
  enabledCount: number
  /** 组内全部配置项启用且至少存在一项时视为启用 */
  allEnabled: boolean
  lastUpdatedBy?: string
  lastUpdatedAt?: string
}

// ---------- 列表加载 ----------
const allItems = ref<ChannelConfigItemDto[]>([])
const loading = ref(false)
const errorMessage = ref('')
const selectedChannel = ref<PaymentChannelType>('WeChat')

const columns: TableColumnsType = [
  { title: '配置键', key: 'key', width: 200 },
  { title: '配置值', key: 'value', ellipsis: true },
  { title: '状态', key: 'enabled', width: 90 },
  { title: '最后更新', key: 'updatedAt', width: 170 },
  { title: '操作', key: 'action', width: 170, fixed: 'right' },
]

/** 按渠道分组并计算组级状态（启用数 / 是否全部启用 / 最后更新信息） */
const groups = computed<ChannelGroup[]>(() => {
  const byChannel = new Map<PaymentChannelType, ChannelConfigItemDto[]>()
  for (const item of allItems.value) {
    const bucket = byChannel.get(item.channel)
    if (bucket) {
      bucket.push(item)
    } else {
      byChannel.set(item.channel, [item])
    }
  }

  return CHANNEL_ORDER.filter((channel) => byChannel.has(channel)).map((channel) => {
    const items = byChannel.get(channel) ?? []
    const enabledCount = items.filter((item) => item.enabled).length
    const lastUpdated = items.reduce<ChannelConfigItemDto | null>((latest, item) => {
      if (!item.updatedAt) return latest
      if (!latest?.updatedAt) return item
      return item.updatedAt > latest.updatedAt ? item : latest
    }, null)

    return {
      channel,
      items,
      enabledCount,
      allEnabled: items.length > 0 && enabledCount === items.length,
      lastUpdatedBy: lastUpdated?.updatedBy,
      lastUpdatedAt: lastUpdated?.updatedAt,
    }
  })
})

/** 当前选中渠道的分组 */
const currentGroup = computed<ChannelGroup | null>(
  () => groups.value.find((group) => group.channel === selectedChannel.value) ?? null,
)

async function fetchChannels() {
  loading.value = true
  errorMessage.value = ''
  try {
    const { data } = await channelApi.list()
    allItems.value = data
    // 默认选中微信渠道；数据中不存在时回退到第一个分组
    if (!groups.value.some((group) => group.channel === selectedChannel.value)) {
      selectedChannel.value = groups.value[0]?.channel ?? 'WeChat'
    }
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '网络异常'
    allItems.value = []
  } finally {
    loading.value = false
  }
}

function onSelectChannel(channel: PaymentChannelType) {
  selectedChannel.value = channel
}

/** 用服务端返回的配置项替换内存中同 id 项（局部刷新，不重拉全量） */
function replaceItem(updated: ChannelConfigItemDto) {
  const index = allItems.value.findIndex((item) => item.id === updated.id)
  if (index >= 0) {
    allItems.value.splice(index, 1, updated)
  } else {
    allItems.value.push(updated)
  }
}

// ---------- 编辑配置项 ----------
const editOpen = ref(false)
const editingItem = ref<ChannelConfigItemDto | null>(null)
const editForm = reactive({ value: '', description: '' })
const saving = ref(false)

function onOpenEdit(item: ChannelConfigItemDto) {
  editingItem.value = item
  // 敏感字段清空重填，留空提交表示不修改；非敏感回填当前值便于修改
  editForm.value = item.isSensitive ? '' : item.value
  editForm.description = item.description ?? ''
  editOpen.value = true
}

async function onSubmitEdit() {
  const item = editingItem.value
  if (!item) return

  const newValue = editForm.value.trim()
  if (!item.isSensitive && !newValue) {
    message.warning(`非敏感配置项 ${item.key} 的值不能为空`)
    return
  }

  saving.value = true
  try {
    const { data } = await channelApi.update(item.id, {
      configs: { [item.key]: newValue },
      description: editForm.description.trim() || undefined,
    })
    replaceItem(data)
    editOpen.value = false
    message.success(`配置 ${item.key} 已更新`)
  } catch (e) {
    message.error(e instanceof Error ? `配置更新失败：${e.message}` : '配置更新失败，请重试')
  } finally {
    saving.value = false
  }
}

// ---------- 启用 / 停用 ----------
const togglingId = ref('')
const toggling = ref(false)
const disableOpen = ref(false)
const disableTarget = ref<ChannelConfigItemDto | null>(null)

const disableContent = computed(() => {
  const target = disableTarget.value
  if (!target) return ''
  const channelLabel = CHANNEL_META[target.channel].label
  return `将停用 ${channelLabel} 的「${target.key}」配置项。停用后该渠道支付可能不可用，请确认无进行中的支付。`
})

function onConfirmDisable(item: ChannelConfigItemDto) {
  disableTarget.value = item
  disableOpen.value = true
}

async function onDisable() {
  const target = disableTarget.value
  if (!target) return

  toggling.value = true
  try {
    await channelApi.disable(target.id)
    const { data } = await channelApi.get(target.id)
    replaceItem(data)
    disableOpen.value = false
    message.success(`配置 ${target.key} 已停用`)
  } catch (e) {
    message.error(e instanceof Error ? `停用失败：${e.message}` : '停用失败，请重试')
  } finally {
    toggling.value = false
  }
}

async function onEnable(item: ChannelConfigItemDto) {
  togglingId.value = item.id
  try {
    await channelApi.enable(item.id)
    const { data } = await channelApi.get(item.id)
    replaceItem(data)
    message.success(`配置 ${item.key} 已启用`)
  } catch (e) {
    message.error(e instanceof Error ? `启用失败：${e.message}` : '启用失败，请重试')
  } finally {
    togglingId.value = ''
  }
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchChannels()
})
</script>

<style scoped>
.payment-channels {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 16px;
  align-items: start;
}

@media (max-width: 1199px) {
  .payment-channels {
    grid-template-columns: 1fr;
  }
}

.channel-panel :deep(.ant-card-body) {
  padding: 12px;
}

.channel-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 0;
  margin: 0;
  list-style: none;
}

@media (max-width: 1199px) {
  .channel-list {
    flex-direction: row;
    gap: 12px;
    overflow-x: auto;
    padding-bottom: 4px;
  }

  .channel-item {
    flex: 0 0 auto;
    min-width: 200px;
  }
}

.channel-item {
  display: flex;
  gap: 10px;
  align-items: center;
  padding: 10px 12px;
  cursor: pointer;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  transition: border-color 0.2s, background 0.2s;
}

.channel-item:hover {
  border-color: #91caff;
}

.channel-item.active {
  background: #e6f4ff;
  border-color: #1677ff;
}

.channel-badge {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  font-size: 12px;
  font-weight: 600;
  color: #fff;
  border-radius: 6px;
}

.channel-badge-lg {
  width: 36px;
  height: 36px;
  font-size: 16px;
  border-radius: 8px;
}

.channel-meta {
  min-width: 0;
}

.channel-name {
  display: flex;
  gap: 6px;
  align-items: center;
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.status-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.channel-sub {
  margin-top: 2px;
  font-size: 12px;
  color: #8c8c8c;
}

.detail-panel :deep(.ant-card-body) {
  padding: 16px 24px 24px;
}

.detail-header {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 16px;
}

.detail-title {
  flex: 1;
  min-width: 0;
}

.detail-name {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
}

.mono-cell {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 13px;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.value-cell {
  color: #000000d9;
  word-break: break-all;
}

.value-cell.ellipsis {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;
}

/* 敏感字段脱敏展示（md §6：灰色 #8C8C8C） */
.masked {
  color: #8c8c8c;
}

.masked-icon {
  margin-right: 4px;
  font-size: 12px;
}

.panel-footer-hint {
  padding-top: 12px;
  margin-top: 4px;
  font-size: 12px;
  color: #8c8c8c;
  border-top: 1px solid #f0f0f0;
}

.edit-form {
  margin-top: 8px;
}

.field-hint {
  margin-top: 4px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
