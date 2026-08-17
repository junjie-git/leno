<!-- web/operations/src/modules/07-notification-ops/views/Config.vue -->
<template>
  <div class="notification-config">
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
            :class="channelErrors[channel] ? 'dot-error' : isConfigured(channel) ? 'dot-configured' : 'dot-unconfigured'"
            :aria-label="channelStatusText(channel)"
          />
          <div class="channel-item-body">
            <span class="channel-item-name">{{ NOTIFICATION_CHANNEL_META[channel].label }}</span>
            <span class="channel-item-meta">
              {{ channelErrors[channel] ? '加载失败' : `${channelConfigs[channel]?.configs.length ?? 0} 项配置` }}
            </span>
          </div>
          <CheckCircleOutlined v-if="isConfigured(channel)" class="channel-check" />
        </div>
      </a-spin>
    </a-card>

    <!-- 区域 B + C：右侧配置详情 / 编辑面板 -->
    <a-card :bordered="false" class="detail-card">
      <a-spin :spinning="detailLoading">
        <!-- 加载失败 -->
        <template v-if="channelErrors[selectedChannel]">
          <EmptyState
            :description="`「${selectedChannelLabel}」配置加载失败：${channelErrors[selectedChannel]}`"
            action-text="重试"
            @action="loadChannel(selectedChannel)"
          />
        </template>

        <!-- 详情模式 -->
        <template v-else-if="panelMode === 'detail'">
          <template v-if="currentConfig">
            <div class="detail-header">
              <h3 class="detail-title">{{ selectedChannelLabel }}渠道配置</h3>
              <a-tag :color="isConfigured(selectedChannel) ? 'success' : 'warning'">
                {{ isConfigured(selectedChannel) ? '已配置' : '未配置' }}
              </a-tag>
            </div>
            <div class="detail-updated">
              最后更新：{{ currentConfig.updatedBy || '—' }} · {{ formatDateTime(currentConfig.updatedAt) }}
            </div>

            <template v-if="currentConfig.configs.length > 0">
              <div class="config-list">
                <div
                  v-for="item in currentConfig.configs"
                  :key="item.key"
                  class="config-row"
                  :aria-label="item.isSensitive ? `${item.key}（敏感字段，已脱敏）` : item.key"
                >
                  <span class="config-key">{{ item.key }}</span>
                  <span class="config-value" :class="{ sensitive: item.isSensitive }">
                    {{ item.value || '—' }}
                  </span>
                  <a-tag v-if="item.isSensitive" class="config-flag">敏感</a-tag>
                </div>
              </div>
              <div v-if="currentConfig.configs.some((i) => i.description)" class="config-notes">
                <div v-for="item in currentConfig.configs.filter((i) => i.description)" :key="`note-${item.key}`" class="config-note">
                  <span class="config-key">{{ item.key }}</span>：{{ item.description }}
                </div>
              </div>
            </template>
            <EmptyState v-else description="该渠道尚未配置任何项目" />

            <a-space class="detail-actions">
              <a-button type="primary" @click="startEdit">编辑</a-button>
              <a-tooltip v-if="!isConfigured(selectedChannel)" title="请先完成配置再测试发送">
                <a-button disabled>测试发送</a-button>
              </a-tooltip>
              <a-button v-else @click="onOpenTest">测试发送</a-button>
            </a-space>
          </template>
          <EmptyState v-else description="请在左侧选择通知渠道查看配置" />
        </template>

        <!-- 编辑模式（敏感字段留空表示不修改） -->
        <template v-else>
          <div class="detail-header">
            <h3 class="detail-title">编辑{{ selectedChannelLabel }}渠道配置</h3>
          </div>
          <a-alert
            class="edit-hint"
            type="info"
            show-icon
            message="敏感字段脱敏显示：留空表示不修改原值；非敏感字段以当前值回填，修改后提交。"
          />
          <a-form :label-col="{ span: 7 }" :wrapper-col="{ span: 14 }">
            <a-form-item
              v-for="item in currentConfig?.configs ?? []"
              :key="item.key"
              :label="item.key"
            >
              <a-input-password
                v-if="item.isSensitive"
                v-model:value="editValues[item.key]"
                :placeholder="`${item.value || '原值已脱敏'}（留空表示不修改）`"
                :maxlength="200"
              />
              <a-input
                v-else
                v-model:value="editValues[item.key]"
                :placeholder="`请输入${item.key}`"
                :maxlength="200"
              />
            </a-form-item>
            <a-form-item v-if="(currentConfig?.configs.length ?? 0) === 0" label="配置项">
              <span class="sub-text">该渠道暂无配置项定义，请联系平台补充配置模板</span>
            </a-form-item>
            <a-form-item :wrapper-col="{ offset: 7, span: 14 }">
              <a-space>
                <IdempotencyButton :loading="editSubmitting" @click="onSubmitEdit">保存</IdempotencyButton>
                <a-button :disabled="editSubmitting" @click="cancelEdit">取消</a-button>
              </a-space>
            </a-form-item>
          </a-form>
        </template>
      </a-spin>
    </a-card>

    <!-- 区域 D：测试发送对话框 -->
    <a-modal
      v-model:open="testModalOpen"
      :title="`测试发送：${selectedChannelLabel}`"
      :confirm-loading="testSending"
      ok-text="发送测试"
      cancel-text="取消"
      @ok="onSubmitTest"
    >
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="测试接收人" required>
          <a-input
            v-model:value="testForm.recipient"
            placeholder="手机号 / 邮箱 / 用户标识"
            :maxlength="100"
          />
        </a-form-item>
        <a-form-item label="测试内容" required>
          <a-textarea
            v-model:value="testForm.content"
            :rows="3"
            :maxlength="200"
            show-count
            placeholder="【Leno】这是一条测试消息，用于验证渠道配置"
          />
        </a-form-item>
      </a-form>

      <template v-if="testResult">
        <a-alert
          :type="testResult.success ? 'success' : 'error'"
          show-icon
          :message="testResult.success ? '发送成功' : '发送失败'"
          :description="testResult.message"
          class="test-result-alert"
        >
          <template #icon>
            <CheckCircleOutlined v-if="testResult.success" />
            <CloseCircleOutlined v-else />
          </template>
        </a-alert>
        <div v-if="testResult.providerResponse != null" class="test-provider">
          <div class="test-provider-title">渠道返回</div>
          <JsonViewer :data="testResult.providerResponse" :max-height="180" />
        </div>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { CheckCircleOutlined, CloseCircleOutlined } from '@ant-design/icons-vue'
import { ConcurrencyError } from '@/shared/http'
import { EmptyState, IdempotencyButton, JsonViewer } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { notificationConfigApi } from '../api/config.api'
import { NOTIFICATION_CHANNELS, NOTIFICATION_CHANNEL_META } from '../types/template.dto'
import type { NotificationChannel } from '../types/template.dto'
import type { NotificationConfigDto, TestSendResultDto } from '../types/config.dto'

/**
 * 通知配置页（07-notification-ops）
 *
 * 左侧渠道列表（配置状态点）+ 右侧配置详情 / 编辑面板 + 测试发送对话框。
 * - 敏感字段后端脱敏返回，编辑留空表示不修改
 * - 未配置渠道「测试发送」按钮置灰提示
 * - 测试发送返回成功 / 失败与渠道原始响应
 */

type ChannelErrorMap = Partial<Record<NotificationChannel, string>>
type ChannelConfigMap = Partial<Record<NotificationChannel, NotificationConfigDto>>

const channelConfigs = reactive<ChannelConfigMap>({})
const channelErrors = reactive<ChannelErrorMap>({})
const channelLoading = reactive<Partial<Record<NotificationChannel, boolean>>>({})

const selectedChannel = ref<NotificationChannel>('Sms')
const panelMode = ref<'detail' | 'edit'>('detail')

const selectedChannelLabel = computed(
  () => NOTIFICATION_CHANNEL_META[selectedChannel.value].label,
)

const currentConfig = computed(() => channelConfigs[selectedChannel.value] ?? null)

const detailLoading = computed(() => Boolean(channelLoading[selectedChannel.value]))

const anyLoading = computed(() => NOTIFICATION_CHANNELS.some((c) => channelLoading[c]))

const loadErrorMessage = ref('')

function isConfigured(channel: NotificationChannel): boolean {
  return (channelConfigs[channel]?.configs.length ?? 0) > 0
}

function channelStatusText(channel: NotificationChannel): string {
  if (channelErrors[channel]) return '加载失败'
  return isConfigured(channel) ? '已配置' : '未配置'
}

// ---------- 加载渠道配置 ----------
async function loadChannel(channel: NotificationChannel) {
  channelLoading[channel] = true
  channelErrors[channel] = undefined
  try {
    const { data } = await notificationConfigApi.get(channel)
    channelConfigs[channel] = data
  } catch (e) {
    channelErrors[channel] = e instanceof Error ? e.message : '加载配置失败'
    channelConfigs[channel] = undefined
  } finally {
    channelLoading[channel] = false
  }
}

async function loadAllChannels() {
  loadErrorMessage.value = ''
  await Promise.all(NOTIFICATION_CHANNELS.map((channel) => loadChannel(channel)))
  loadErrorMessage.value = NOTIFICATION_CHANNELS.every((c) => channelErrors[c]) ? '全部渠道配置加载失败' : ''
}

function onSelectChannel(channel: NotificationChannel) {
  selectedChannel.value = channel
  panelMode.value = 'detail'
  if (!channelConfigs[channel] && !channelErrors[channel]) {
    void loadChannel(channel)
  }
}

// ---------- 编辑模式 ----------
const editValues = reactive<Record<string, string>>({})
const editSubmitting = ref(false)

function startEdit() {
  const config = currentConfig.value
  Object.keys(editValues).forEach((key) => {
    delete editValues[key]
  })
  config?.configs.forEach((item) => {
    editValues[item.key] = item.isSensitive ? '' : item.value
  })
  panelMode.value = 'edit'
}

function cancelEdit() {
  panelMode.value = 'detail'
}

async function onSubmitEdit() {
  const channel = selectedChannel.value
  const configs: Record<string, string> = {}
  for (const item of currentConfig.value?.configs ?? []) {
    const value = (editValues[item.key] ?? '').trim()
    if (item.isSensitive) {
      // 敏感项留空 / 缺省表示不修改
      if (value) configs[item.key] = value
    } else {
      configs[item.key] = value
    }
  }

  editSubmitting.value = true
  try {
    const { data } = await notificationConfigApi.update({ channel, configs })
    channelConfigs[channel] = data
    panelMode.value = 'detail'
    message.success(`${NOTIFICATION_CHANNEL_META[channel].label}渠道配置已更新`)
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

// ---------- 测试发送 ----------
const testModalOpen = ref(false)
const testSending = ref(false)
const testResult = ref<TestSendResultDto | null>(null)
const testForm = reactive({
  recipient: '',
  content: '【Leno】这是一条测试消息，用于验证渠道配置',
})

function onOpenTest() {
  testResult.value = null
  testForm.recipient = ''
  testModalOpen.value = true
}

async function onSubmitTest() {
  const recipient = testForm.recipient.trim()
  const content = testForm.content.trim()
  if (!recipient) {
    message.error('请输入测试接收人')
    return
  }
  if (!content) {
    message.error('请输入测试内容')
    return
  }

  testSending.value = true
  try {
    const { data } = await notificationConfigApi.test({
      channel: selectedChannel.value,
      recipient,
      content,
    })
    testResult.value = data
    if (data.success) {
      message.success('测试消息已发送，请注意查收')
    } else {
      message.error(`测试发送失败：${data.message}`)
    }
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '测试发送失败，请重试')
  } finally {
    testSending.value = false
  }
}

// ---------- 初始化 ----------
onMounted(() => {
  void loadAllChannels()
})
</script>

<style scoped>
.notification-config {
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

.channel-check {
  color: #52c41a;
  font-size: 16px;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot-configured {
  background: #52c41a;
}

.dot-unconfigured {
  background: #faad14;
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

.config-list {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  overflow: hidden;
}

.config-row {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 48px;
  padding: 8px 16px;
  border-bottom: 1px solid #f0f0f0;
}

.config-row:last-child {
  border-bottom: none;
}

.config-key {
  width: 200px;
  flex-shrink: 0;
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 13px;
  color: #595959;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.config-value {
  flex: 1;
  min-width: 0;
  font-size: 14px;
  color: #000000d9;
  word-break: break-all;
}

.config-value.sensitive {
  color: #8c8c8c;
}

.config-flag {
  flex-shrink: 0;
  margin-right: 0;
}

.config-notes {
  margin-top: 12px;
  padding: 12px 16px;
  background: #fafafa;
  border-radius: 6px;
}

.config-note {
  font-size: 12px;
  color: #8c8c8c;
  line-height: 1.8;
}

.config-note .config-key {
  width: auto;
}

.detail-actions {
  margin-top: 24px;
}

.edit-hint {
  margin-bottom: 16px;
}

.sub-text {
  font-size: 12px;
  color: #8c8c8c;
}

.test-result-alert {
  margin-top: 16px;
}

.test-provider {
  margin-top: 12px;
}

.test-provider-title {
  font-size: 13px;
  font-weight: 500;
  color: #000000d9;
  margin-bottom: 8px;
}
</style>
