<!-- web/operations/src/modules/07-notification-ops/views/RateLimits.vue -->
<template>
  <div class="rate-limits">
    <!-- 区域 A：左侧渠道列表 -->
    <a-card :bordered="false" class="channel-card" :body-style="{ padding: '12px' }">
      <div class="channel-toolbar">
        <span class="channel-toolbar-title">通知渠道</span>
        <a-button size="small" :loading="anyLoading" @click="loadAllChannels">刷新</a-button>
      </div>
      <a-spin :spinning="anyLoading">
        <div v-if="loadErrorMessage" class="channel-state">
          <EmptyState :description="`加载失败：${loadErrorMessage}`" action-text="重试" @action="loadAllChannels" />
        </div>
        <div
          v-for="channel in NOTIFICATION_CHANNELS"
          :key="channel"
          class="channel-item"
          :class="{ active: channel === selectedChannel, error: channelErrors[channel] }"
          :aria-label="`${NOTIFICATION_CHANNEL_META[channel].label}渠道`"
          @click="onSelectChannel(channel)"
        >
          <span
            class="status-dot"
            :class="channelDotClass(channel)"
            :aria-label="channelStatusText(channel)"
          />
          <div class="channel-item-body">
            <span class="channel-item-name">{{ NOTIFICATION_CHANNEL_META[channel].label }}</span>
            <span class="channel-item-meta">
              {{ channelErrors[channel] ? '加载失败' : channelStatusText(channel) }}
            </span>
          </div>
          <ThunderboltOutlined class="channel-icon" />
        </div>
      </a-spin>
    </a-card>

    <!-- 区域 B + C：右侧限流详情 / 编辑面板 -->
    <a-card :bordered="false" class="detail-card">
      <a-spin :spinning="detailLoading">
        <!-- 加载失败 -->
        <template v-if="channelErrors[selectedChannel]">
          <EmptyState
            :description="`「${selectedChannelLabel}」限流配置加载失败：${channelErrors[selectedChannel]}`"
            action-text="重试"
            @action="loadChannel(selectedChannel)"
          />
        </template>

        <!-- 详情模式 -->
        <template v-else-if="panelMode === 'detail'">
          <template v-if="currentConfig">
            <div class="detail-header">
              <h3 class="detail-title">{{ selectedChannelLabel }}限流配置</h3>
              <a-tag :color="RATE_LIMIT_STATUS_META[currentConfig.status].color">
                {{ RATE_LIMIT_STATUS_META[currentConfig.status].label }}
              </a-tag>
            </div>
            <div class="detail-updated">
              最后更新：{{ currentConfig.updatedBy || '—' }} · {{ formatDateTime(currentConfig.updatedAt) }}
            </div>

            <a-descriptions :column="2" bordered size="small">
              <a-descriptions-item label="每用户每日上限">{{ currentConfig.userDailyLimit }} 条</a-descriptions-item>
              <a-descriptions-item label="每用户每小时上限">{{ currentConfig.userHourlyLimit }} 条</a-descriptions-item>
              <a-descriptions-item label="全平台每分钟上限">{{ currentConfig.globalPerMinuteLimit }} 条</a-descriptions-item>
              <a-descriptions-item label="全平台每小时上限">{{ currentConfig.globalHourlyLimit }} 条</a-descriptions-item>
            </a-descriptions>

            <h3 class="section-title">当前用量</h3>
            <div class="usage-block">
              <div class="usage-row">
                <div class="usage-label">
                  <span>本分钟发送</span>
                  <WarningOutlined v-if="usagePercent('minute') >= 0.8" class="usage-warn" />
                </div>
                <a-progress
                  :percent="usagePercent('minute')"
                  :stroke-color="usageColor(usagePercent('minute'))"
                  :format="() => usageText('minute')"
                />
              </div>
              <div class="usage-row">
                <div class="usage-label">
                  <span>本小时发送</span>
                  <WarningOutlined v-if="usagePercent('hour') >= 0.8" class="usage-warn" />
                </div>
                <a-progress
                  :percent="usagePercent('hour')"
                  :stroke-color="usageColor(usagePercent('hour'))"
                  :format="() => usageText('hour')"
                />
              </div>
              <div class="usage-row">
                <div class="usage-label">
                  <span>今日发送</span>
                  <WarningOutlined v-if="usagePercent('today') >= 0.8" class="usage-warn" />
                </div>
                <a-progress
                  :percent="usagePercent('today')"
                  :stroke-color="usageColor(usagePercent('today'))"
                  :format="() => usageText('today')"
                />
              </div>
            </div>
            <div class="usage-note">用量占比：&lt;80% 绿色，80%-95% 橙色，&gt;95% 红色；每 30 秒自动刷新。</div>

            <a-space class="detail-actions">
              <a-button type="primary" @click="startEdit">编辑</a-button>
            </a-space>
          </template>
          <EmptyState v-else description="请在左侧选择通知渠道查看限流配置" />
        </template>

        <!-- 编辑模式 -->
        <template v-else>
          <div class="detail-header">
            <h3 class="detail-title">编辑{{ selectedChannelLabel }}限流配置</h3>
          </div>
          <a-form :label-col="{ span: 8 }" :wrapper-col="{ span: 12 }">
            <a-divider orientation="left" plain>用户级限流</a-divider>
            <a-form-item label="每用户每日上限" required>
              <a-input-number
                v-model:value="editForm.userDailyLimit"
                :min="1"
                :max="100"
                :precision="0"
                style="width: 160px"
                placeholder="1-100"
              />
              <span class="field-unit">条 / 日（1-100）</span>
            </a-form-item>
            <a-form-item label="每用户每小时上限" required>
              <a-input-number
                v-model:value="editForm.userHourlyLimit"
                :min="1"
                :max="20"
                :precision="0"
                style="width: 160px"
                placeholder="1-20"
              />
              <span class="field-unit">条 / 小时（1-20）</span>
            </a-form-item>
            <a-divider orientation="left" plain>全局级限流</a-divider>
            <a-form-item label="全平台每分钟上限" required>
              <a-input-number
                v-model:value="editForm.globalPerMinuteLimit"
                :min="10"
                :max="10000"
                :precision="0"
                style="width: 160px"
                placeholder="10-10000"
              />
              <span class="field-unit">条 / 分钟（10-10000）</span>
            </a-form-item>
            <a-form-item label="全平台每小时上限" required>
              <a-input-number
                v-model:value="editForm.globalHourlyLimit"
                :min="100"
                :max="100000"
                :precision="0"
                style="width: 160px"
                placeholder="100-100000"
              />
              <span class="field-unit">条 / 小时（100-100000）</span>
            </a-form-item>
            <a-form-item label="限流状态">
              <a-switch
                :checked="editForm.status === 'Active'"
                checked-children="启用"
                un-checked-children="禁用"
                @change="(checked: boolean | string | number) => onStatusSwitch(checked)"
              />
              <span class="field-unit">{{ editForm.status === 'Active' ? '启用限流' : '关闭限流（高危）' }}</span>
            </a-form-item>
            <a-form-item :wrapper-col="{ offset: 8, span: 12 }">
              <a-space>
                <IdempotencyButton :loading="editSubmitting" @click="onSubmitEdit">保存</IdempotencyButton>
                <a-button :disabled="editSubmitting" @click="cancelEdit">取消</a-button>
              </a-space>
            </a-form-item>
          </a-form>
        </template>
      </a-spin>
    </a-card>

    <!-- 关闭限流高危确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="关闭限流"
      :content="`关闭后「${selectedChannelLabel}」渠道将不受任何频率限制，可能导致用户被通知轰炸与渠道成本失控。确认关闭限流？`"
      @confirm="onConfirmDisableLimit"
      @cancel="disableConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { ThunderboltOutlined, WarningOutlined } from '@ant-design/icons-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime, formatNumber } from '@/shared/utils/format'
import { rateLimitApi } from '../api/rateLimit.api'
import { NOTIFICATION_CHANNELS, NOTIFICATION_CHANNEL_META } from '../types/template.dto'
import type { NotificationChannel } from '../types/template.dto'
import { RATE_LIMIT_STATUS_META } from '../types/rate-limit.dto'
import type { RateLimitConfigDto, RateLimitStatus } from '../types/rate-limit.dto'

/**
 * 通知限流页（07-notification-ops）
 *
 * 左侧渠道列表 + 右侧限流详情 / 编辑面板。
 * - 阈值校验：正整数、用户每小时 ≤ 用户每日、全局每分钟 ≤ 全局每小时、用户级 ≤ 全局级
 * - 用量进度条三色（<80% 绿 / 80-95% 橙 / >95% 红），30 秒轮询刷新
 * - 关闭限流为高危操作，强制二次确认
 */

const USAGE_POLL_INTERVAL = 30 * 1000

type ChannelErrorMap = Partial<Record<NotificationChannel, string>>
type ChannelConfigMap = Partial<Record<NotificationChannel, RateLimitConfigDto>>

const channelConfigs = reactive<ChannelConfigMap>({})
const channelErrors = reactive<ChannelErrorMap>({})
const channelLoading = reactive<Partial<Record<NotificationChannel, boolean>>>({})

const selectedChannel = ref<NotificationChannel>('Sms')
const panelMode = ref<'detail' | 'edit'>('detail')

const selectedChannelLabel = computed(() => NOTIFICATION_CHANNEL_META[selectedChannel.value].label)

const currentConfig = computed(() => channelConfigs[selectedChannel.value] ?? null)

const detailLoading = computed(() => Boolean(channelLoading[selectedChannel.value]))

const anyLoading = computed(() => NOTIFICATION_CHANNELS.some((c) => channelLoading[c]))

const loadErrorMessage = ref('')

function channelStatusText(channel: NotificationChannel): string {
  const config = channelConfigs[channel]
  if (channelErrors[channel]) return '加载失败'
  if (!config) return '未加载'
  return RATE_LIMIT_STATUS_META[config.status].label
}

function channelDotClass(channel: NotificationChannel): string {
  if (channelErrors[channel]) return 'dot-error'
  return channelConfigs[channel]?.status === 'Inactive' ? 'dot-inactive' : 'dot-active'
}

// ---------- 加载 ----------
async function loadChannel(channel: NotificationChannel, silent = false) {
  if (!silent) channelLoading[channel] = true
  channelErrors[channel] = undefined
  try {
    const { data } = await rateLimitApi.get(channel)
    channelConfigs[channel] = data
  } catch (e) {
    channelErrors[channel] = e instanceof Error ? e.message : '加载限流配置失败'
    if (silent) channelConfigs[channel] = undefined
  } finally {
    channelLoading[channel] = false
  }
}

async function loadAllChannels() {
  loadErrorMessage.value = ''
  await Promise.all(NOTIFICATION_CHANNELS.map((channel) => loadChannel(channel)))
  loadErrorMessage.value = NOTIFICATION_CHANNELS.every((c) => channelErrors[c]) ? '全部渠道限流配置加载失败' : ''
}

function onSelectChannel(channel: NotificationChannel) {
  selectedChannel.value = channel
  panelMode.value = 'detail'
  if (!channelConfigs[channel] && !channelErrors[channel]) {
    void loadChannel(channel)
  }
}

// ---------- 用量进度条 ----------
type UsageKey = 'minute' | 'hour' | 'today'

function usagePercent(key: UsageKey): number {
  const config = currentConfig.value
  if (!config) return 0
  const { currentUsage } = config
  let used: number
  let limit: number
  if (key === 'minute') {
    used = currentUsage.minuteCount
    limit = config.globalPerMinuteLimit
  } else if (key === 'hour') {
    used = currentUsage.hourCount
    limit = config.globalHourlyLimit
  } else {
    used = currentUsage.todayCount
    limit = config.globalHourlyLimit
  }
  if (limit <= 0) return 0
  return Math.min(100, Math.round((used / limit) * 1000) / 10)
}

function usageText(key: UsageKey): string {
  const config = currentConfig.value
  if (!config) return '—'
  const { currentUsage } = config
  if (key === 'minute') return `${formatNumber(currentUsage.minuteCount)} / ${formatNumber(config.globalPerMinuteLimit)}`
  if (key === 'hour') return `${formatNumber(currentUsage.hourCount)} / ${formatNumber(config.globalHourlyLimit)}`
  return `${formatNumber(currentUsage.todayCount)} / ${formatNumber(config.globalHourlyLimit)}`
}

function usageColor(percent: number): string {
  if (percent > 95) return '#FF4D4F'
  if (percent >= 80) return '#FAAD14'
  return '#52C41A'
}

// ---------- 编辑 ----------
interface EditFormState {
  userDailyLimit: number
  userHourlyLimit: number
  globalPerMinuteLimit: number
  globalHourlyLimit: number
  status: RateLimitStatus
}

const editForm = reactive<EditFormState>({
  userDailyLimit: 10,
  userHourlyLimit: 3,
  globalPerMinuteLimit: 100,
  globalHourlyLimit: 1000,
  status: 'Active',
})
const editSubmitting = ref(false)
const disableConfirmOpen = ref(false)

function startEdit() {
  const config = currentConfig.value
  if (!config) return
  editForm.userDailyLimit = config.userDailyLimit
  editForm.userHourlyLimit = config.userHourlyLimit
  editForm.globalPerMinuteLimit = config.globalPerMinuteLimit
  editForm.globalHourlyLimit = config.globalHourlyLimit
  editForm.status = config.status
  panelMode.value = 'edit'
}

function cancelEdit() {
  panelMode.value = 'detail'
}

function onStatusSwitch(checked: boolean | string | number) {
  editForm.status = checked ? 'Active' : 'Inactive'
}

/** 阈值校验：正整数 / 层级约束 / 用户级不超全局级 */
function validateEditForm(): string | null {
  const { userDailyLimit, userHourlyLimit, globalPerMinuteLimit, globalHourlyLimit } = editForm
  const values: [number, string, number, number][] = [
    [userDailyLimit, '每用户每日上限', 1, 100],
    [userHourlyLimit, '每用户每小时上限', 1, 20],
    [globalPerMinuteLimit, '全平台每分钟上限', 10, 10000],
    [globalHourlyLimit, '全平台每小时上限', 100, 100000],
  ]
  for (const [value, label, min, max] of values) {
    if (!Number.isInteger(value) || value < min || value > max) {
      return `${label}须为 ${min}-${max} 的正整数`
    }
  }
  if (userHourlyLimit > userDailyLimit) {
    return '用户级限流不可超过全局级：每用户每小时上限不可超过每用户每日上限'
  }
  if (globalPerMinuteLimit > globalHourlyLimit) {
    return '全平台每分钟上限不可超过全平台每小时上限'
  }
  if (userDailyLimit > globalHourlyLimit) {
    return '用户级限流不可超过全局级：每用户每日上限不可超过全平台每小时上限'
  }
  return null
}

async function onSubmitEdit() {
  const invalid = validateEditForm()
  if (invalid) {
    message.error(invalid)
    return
  }

  // 关闭限流（启用 → 禁用）为高危操作，强制二次确认
  const savedStatus = currentConfig.value?.status
  if (editForm.status === 'Inactive' && savedStatus === 'Active') {
    disableConfirmOpen.value = true
    return
  }
  await saveEdit()
}

async function onConfirmDisableLimit() {
  disableConfirmOpen.value = false
  await saveEdit()
}

async function saveEdit() {
  const channel = selectedChannel.value
  editSubmitting.value = true
  try {
    const { data } = await rateLimitApi.update({
      channel,
      userDailyLimit: editForm.userDailyLimit,
      userHourlyLimit: editForm.userHourlyLimit,
      globalPerMinuteLimit: editForm.globalPerMinuteLimit,
      globalHourlyLimit: editForm.globalHourlyLimit,
      status: editForm.status,
    })
    channelConfigs[channel] = data
    panelMode.value = 'detail'
    message.success(`${NOTIFICATION_CHANNEL_META[channel].label}渠道限流配置已更新`)
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('配置已被他人修改，请刷新后重试')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '配置更新失败，请重试')
    }
  } finally {
    editSubmitting.value = false
  }
}

// ---------- 用量 30s 轮询（编辑模式下暂停） ----------
let usageTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  void loadAllChannels()
  usageTimer = setInterval(() => {
    if (panelMode.value === 'detail') {
      void loadChannel(selectedChannel.value, true)
    }
  }, USAGE_POLL_INTERVAL)
})

onUnmounted(() => {
  if (usageTimer) {
    clearInterval(usageTimer)
    usageTimer = null
  }
})
</script>

<style scoped>
.rate-limits {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}

.channel-card {
  width: 280px;
  flex-shrink: 0;
}

.channel-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.channel-toolbar-title {
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.channel-state {
  padding: 16px 0;
}

.channel-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.channel-item:hover {
  border-color: #1677ff;
}

.channel-item.active {
  border-color: #1677ff;
  background: #e6f4ff;
}

.channel-item.error {
  border-color: #ffccc7;
}

.channel-item-body {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.channel-item-name {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.channel-item-meta {
  font-size: 12px;
  color: #8c8c8c;
}

.channel-icon {
  color: #1677ff;
  font-size: 16px;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot-active {
  background: #52c41a;
}

.dot-inactive {
  background: #ff4d4f;
}

.dot-error {
  background: #ff4d4f;
}

.detail-card {
  flex: 1;
  min-width: 0;
}

.detail-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.detail-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
}

.detail-updated {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 16px;
}

.section-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.usage-block {
  padding: 4px 0;
}

.usage-row {
  margin-bottom: 8px;
}

.usage-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #000000d9;
  margin-bottom: 2px;
}

.usage-warn {
  color: #faad14;
  font-size: 13px;
}

.usage-note {
  font-size: 12px;
  color: #8c8c8c;
}

.detail-actions {
  margin-top: 24px;
}

.field-unit {
  margin-left: 8px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
