<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { addressApi } from '@/modules/13-profile/api/address.api'
import type { AddressDto, SaveAddressRequestDto } from '../types/profile.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { maskPhone } from '@/shared/utils/format'
import {
  isValidAddressDetail,
  isValidPhone,
  isValidReceiverName,
} from '@/shared/utils/validators'
import { logger } from '@/shared/utils/logger'

/**
 * 收货地址页（/profile/addresses）
 *
 * 结构（对齐设计稿 addresses）：
 * NavBar（返回 + 收货地址）→ 地址卡片列表（默认地址置顶主色描边，含标签/编辑/删除/设为默认）
 * → 底部固定「新增地址」按钮（上限 20 条，适配 safe-area）
 * → 新增/编辑表单弹层（收件人 / 手机号 / 省市区 / 详细地址 / 标签 / 默认开关）
 *
 * 交互：
 * - 删除需二次确认（危险操作红色确认）
 * - 设为默认成功后刷新列表（服务端保证唯一默认）
 * - 下单场景进入（query from=checkout / buy-now）时，保存成功后返回来源页
 */
const router = useRouter()
const route = useRoute()

/** 地址数量上限（对齐后端约束） */
const ADDRESS_LIMIT = 20

/** 地址标签选项 */
const TAG_OPTIONS = ['家', '公司', '学校'] as const

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const addresses = ref<AddressDto[]>([])
const refreshing = ref(false)

// ---- 表单弹层 ----
const formVisible = ref(false)
const formSubmitting = ref(false)
/** 编辑中的地址 id（空表示新增） */
const editingId = ref('')

const form = ref({
  receiver: '',
  phone: '',
  province: '',
  city: '',
  district: '',
  detail: '',
  tag: '' as string,
  isDefault: false,
})

const receiverError = ref('')
const phoneError = ref('')
const regionError = ref('')
const detailError = ref('')

/** 默认地址优先排序（服务端已保证，前端兜底稳定排序） */
const sortedAddresses = computed(() => {
  const list = [...addresses.value]
  list.sort((a, b) => {
    if (a.isDefault !== b.isDefault) return a.isDefault ? -1 : 1
    return 0
  })
  return list
})

/** 是否为下单返回场景（保存成功后回到下单页） */
const isFromOrderFlow = computed(() => {
  const from = route.query.from
  return from === 'checkout' || from === 'buy-now'
})

/** 标签展示样式 */
const TAG_CLASS: Record<string, string> = {
  家: 'tag-home',
  公司: 'tag-company',
  学校: 'tag-school',
}

onMounted(() => {
  void loadAddresses()
})

/** 加载地址列表 */
async function loadAddresses(silent = false): Promise<void> {
  if (!silent) {
    loading.value = true
  }
  loadError.value = false
  try {
    addresses.value = await addressApi.list()
  } catch (e) {
    logger.error('地址列表加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
    refreshing.value = false
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await loadAddresses(true)
}

// ---- 表单弹层 ----
function openCreate(): void {
  if (addresses.value.length >= ADDRESS_LIMIT) {
    showToast('地址数量已达上限')
    return
  }
  editingId.value = ''
  form.value = {
    receiver: '',
    phone: '',
    province: '',
    city: '',
    district: '',
    detail: '',
    tag: '家',
    isDefault: addresses.value.length === 0,
  }
  clearFormErrors()
  formVisible.value = true
}

function openEdit(address: AddressDto): void {
  editingId.value = address.id
  form.value = {
    receiver: address.receiver,
    phone: address.phone,
    province: address.province,
    city: address.city,
    district: address.district,
    detail: address.detail,
    tag: address.tag ?? '',
    isDefault: address.isDefault,
  }
  clearFormErrors()
  formVisible.value = true
}

function clearFormErrors(): void {
  receiverError.value = ''
  phoneError.value = ''
  regionError.value = ''
  detailError.value = ''
}

/** 标签选择（再次点击取消选择，表示无标签） */
function toggleTag(tag: string): void {
  form.value.tag = form.value.tag === tag ? '' : tag
}

/** 表单校验（失焦字段 + 提交前整体校验） */
function validateForm(): boolean {
  clearFormErrors()
  if (!isValidReceiverName(form.value.receiver)) {
    receiverError.value = '收件人姓名需 2-20 个字符'
  }
  if (!isValidPhone(form.value.phone)) {
    phoneError.value = '手机号格式不正确'
  }
  if (!form.value.province.trim() || !form.value.city.trim() || !form.value.district.trim()) {
    regionError.value = '请完整填写省 / 市 / 区'
  }
  if (!isValidAddressDetail(form.value.detail)) {
    detailError.value = '详细地址需 5-100 个字符'
  }
  return (
    !receiverError.value && !phoneError.value && !regionError.value && !detailError.value
  )
}

/** 保存地址（新增或编辑） */
async function onSave(): Promise<void> {
  if (!validateForm() || formSubmitting.value) return
  formSubmitting.value = true
  const body: SaveAddressRequestDto = {
    id: editingId.value || undefined,
    receiver: form.value.receiver.trim(),
    phone: form.value.phone.trim(),
    province: form.value.province.trim(),
    city: form.value.city.trim(),
    district: form.value.district.trim(),
    detail: form.value.detail.trim(),
    isDefault: form.value.isDefault,
    tag: form.value.tag || undefined,
  }
  try {
    if (editingId.value) {
      await addressApi.update(editingId.value, body)
      showToast('保存成功')
    } else {
      await addressApi.create(body)
      showToast('新增成功')
    }
    formVisible.value = false
    if (isFromOrderFlow.value) {
      // 下单场景：保存完成后返回下单页（下单页重新挂载会拉取最新地址列表）
      router.back()
      return
    }
    await loadAddresses(true)
  } catch (e) {
    logger.error('地址保存失败', e)
    showFailToast(e instanceof Error ? e.message : '保存失败，请稍后重试')
  } finally {
    formSubmitting.value = false
  }
}

// ---- 卡片操作 ----
async function onSetDefault(address: AddressDto): Promise<void> {
  if (address.isDefault) return
  try {
    await addressApi.setDefault(address.id)
    showToast('已设为默认地址')
    await loadAddresses(true)
  } catch (e) {
    logger.error('设置默认地址失败', e)
    showFailToast(e instanceof Error ? e.message : '设置失败，请稍后重试')
  }
}

async function onDelete(address: AddressDto): Promise<void> {
  try {
    await showConfirmDialog({
      title: '确认删除',
      message: '删除后将无法恢复，关联订单的收货地址不受影响。',
      confirmButtonText: '删除',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await addressApi.remove(address.id)
    showToast('删除成功')
    await loadAddresses(true)
  } catch (e) {
    logger.error('地址删除失败', e)
    showFailToast(e instanceof Error ? e.message : '删除失败，请稍后重试')
  }
}

// ---- 返回 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/profile')
  }
}
</script>

<template>
  <div class="addresses-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">收货地址</div>
    </header>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <div v-if="loading" class="address-list">
        <div v-for="i in 3" :key="i" class="sk-card">
          <div class="sk-head">
            <div class="skeleton-block sk-name" />
            <div class="skeleton-block sk-phone" />
            <div class="skeleton-block sk-tag" />
          </div>
          <div class="skeleton-block sk-line-full" />
          <div class="skeleton-block sk-line-short" />
          <div class="sk-actions">
            <div class="skeleton-block sk-action" />
            <div class="skeleton-block sk-action" />
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError && addresses.length === 0"
        title="地址加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadAddresses()"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="addresses.length === 0"
        title="暂无收货地址"
        action-text="新增地址"
        @action="openCreate"
      />

      <!-- 地址列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <div class="address-list">
          <article
            v-for="address in sortedAddresses"
            :key="address.id"
            class="address-card"
            :class="{ default: address.isDefault }"
            role="article"
            :aria-label="`收货地址：${address.receiver}`"
          >
            <div class="address-header">
              <span class="address-name">{{ address.receiver }}</span>
              <span class="address-phone">{{ maskPhone(address.phone) }}</span>
              <span v-if="address.tag" class="address-tag" :class="TAG_CLASS[address.tag] ?? 'tag-home'">
                {{ address.tag }}
              </span>
            </div>
            <div class="address-detail">
              {{ address.province }}{{ address.city }}{{ address.district }}{{ address.detail }}
            </div>
            <div class="address-actions">
              <button
                v-if="!address.isDefault"
                class="address-action default-btn"
                type="button"
                aria-label="设为默认地址"
                @click="onSetDefault(address)"
              >
                <van-icon name="checked" size="14" />
                设为默认
              </button>
              <button class="address-action edit" type="button" aria-label="编辑地址" @click="openEdit(address)">
                <van-icon name="edit" size="14" />
                编辑
              </button>
              <button class="address-action delete" type="button" aria-label="删除地址" @click="onDelete(address)">
                <van-icon name="delete-o" size="14" />
                删除
              </button>
            </div>
          </article>
        </div>
        <div class="address-count">共 {{ addresses.length }} 个地址，最多可添加 {{ ADDRESS_LIMIT }} 个</div>
      </van-pull-refresh>
    </div>

    <!-- 底部新增栏 -->
    <footer class="bottom-bar">
      <button
        class="add-btn"
        type="button"
        :disabled="addresses.length >= ADDRESS_LIMIT"
        :aria-label="addresses.length >= ADDRESS_LIMIT ? '地址数量已达上限' : '新增地址'"
        @click="openCreate"
      >
        <van-icon name="add-o" size="18" color="#fff" />
        新增地址
      </button>
    </footer>

    <!-- 新增/编辑表单弹层 -->
    <van-popup
      v-model:show="formVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="地址表单"
      :style="{ maxHeight: '88%' }"
    >
      <div class="form-panel">
        <div class="form-head">
          <span class="t">{{ editingId ? '编辑地址' : '新增地址' }}</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="formVisible = false" />
        </div>

        <div class="form-body">
          <div class="field">
            <div class="field-label">收件人</div>
            <input
              v-model="form.receiver"
              class="field-input"
              type="text"
              placeholder="2-20 个字符"
              aria-label="收件人"
              maxlength="20"
              @blur="validateForm"
            >
            <div v-if="receiverError" class="field-error">{{ receiverError }}</div>
          </div>

          <div class="field">
            <div class="field-label">手机号</div>
            <input
              v-model="form.phone"
              class="field-input"
              type="tel"
              placeholder="11 位手机号"
              aria-label="手机号"
              maxlength="11"
              @blur="validateForm"
            >
            <div v-if="phoneError" class="field-error">{{ phoneError }}</div>
          </div>

          <div class="field-row">
            <div class="field">
              <div class="field-label">省份</div>
              <input
                v-model="form.province"
                class="field-input"
                type="text"
                placeholder="如：上海市"
                aria-label="省份"
                @blur="validateForm"
              >
            </div>
            <div class="field">
              <div class="field-label">城市</div>
              <input
                v-model="form.city"
                class="field-input"
                type="text"
                placeholder="如：上海市"
                aria-label="城市"
                @blur="validateForm"
              >
            </div>
            <div class="field">
              <div class="field-label">区县</div>
              <input
                v-model="form.district"
                class="field-input"
                type="text"
                placeholder="如：浦东新区"
                aria-label="区县"
                @blur="validateForm"
              >
            </div>
          </div>
          <div v-if="regionError" class="field-error region-error">{{ regionError }}</div>

          <div class="field">
            <div class="field-label">详细地址</div>
            <textarea
              v-model="form.detail"
              class="field-input field-textarea"
              placeholder="街道、门牌号、楼层等（5-100 个字符）"
              aria-label="详细地址"
              maxlength="100"
              rows="2"
              @blur="validateForm"
            />
            <div v-if="detailError" class="field-error">{{ detailError }}</div>
          </div>

          <div class="field">
            <div class="field-label">标签</div>
            <div class="tag-row">
              <button
                v-for="tag in TAG_OPTIONS"
                :key="tag"
                class="tag-chip"
                :class="{ on: form.tag === tag }"
                type="button"
                :aria-label="`标签 ${tag}`"
                @click="toggleTag(tag)"
              >
                {{ tag }}
              </button>
            </div>
          </div>

          <div class="switch-row">
            <div>
              <div class="switch-title">设为默认地址</div>
              <div class="switch-desc">下单时优先使用该地址</div>
            </div>
            <van-switch v-model="form.isDefault" size="22" aria-label="设为默认地址" />
          </div>
        </div>

        <div class="form-foot">
          <van-button plain type="primary" class="foot-btn" @click="formVisible = false">取消</van-button>
          <van-button type="primary" class="foot-btn" :loading="formSubmitting" @click="onSave">保存</van-button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.addresses-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n3);
}

/* NavBar */
.navbar {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  flex-shrink: 0;
}

.nav-back {
  display: flex;
  align-items: center;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}

.nav-title {
  flex: 1;
  text-align: center;
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
  margin-right: 20px;
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s12) + env(safe-area-inset-bottom));
}

.address-list {
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}

/* 地址卡片 */
.address-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  border: 2px solid transparent;
  position: relative;
}

.address-card.default {
  border-color: var(--c-primary);
}

.address-card.default::before {
  content: "";
  position: absolute;
  top: 0;
  left: 0;
  width: 0;
  height: 0;
  border-style: solid;
  border-width: 28px 28px 0 0;
  border-color: var(--c-primary) transparent transparent transparent;
  border-radius: var(--r-lg) 0 0 0;
}

.address-card.default::after {
  content: "默认";
  position: absolute;
  top: 4px;
  left: 5px;
  font-size: 10px;
  color: #fff;
  font-weight: var(--fw-medium);
  z-index: 1;
}

.address-header {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s2);
}

.address-name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.address-phone {
  font-size: var(--fs-base);
  color: var(--n9);
}

.address-tag {
  display: inline-flex;
  align-items: center;
  padding: 1px var(--s2);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  margin-left: auto;
}

.tag-home {
  background: #e6f4ff;
  color: var(--c-primary);
}

.tag-company {
  background: #f6ffed;
  color: var(--c-success);
}

.tag-school {
  background: #fff7e6;
  color: var(--c-warning);
}

.address-detail {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.5;
  margin-bottom: var(--s2);
}

.address-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--s3);
  padding-top: var(--s2);
  border-top: 1px solid var(--n3);
}

.address-action {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: var(--fs-sm);
  color: var(--n9);
  padding: var(--s1) var(--s2);
}

.address-action.edit {
  color: var(--c-primary);
}

.address-action.delete {
  color: var(--c-error);
}

.address-action.default-btn {
  color: var(--c-primary);
  margin-right: auto;
}

.address-count {
  text-align: center;
  padding: var(--s2);
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 底部新增栏 */
.bottom-bar {
  position: sticky;
  bottom: 0;
  background: var(--n1);
  padding: var(--s3);
  border-top: 1px solid var(--n3);
  padding-bottom: calc(var(--s3) + env(safe-area-inset-bottom));
  flex-shrink: 0;
}

.add-btn {
  width: 100%;
  height: 44px;
  background: var(--c-primary);
  color: #fff;
  border-radius: var(--r-lg);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
}

.add-btn:disabled {
  background: var(--n5);
  color: var(--n7);
}

/* 骨架屏 */
.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3);
}

.sk-head {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s2);
}

.sk-name {
  width: 60px;
  height: 16px;
}

.sk-phone {
  width: 80px;
  height: 14px;
}

.sk-tag {
  width: 32px;
  height: 18px;
  border-radius: var(--r-base);
  margin-left: auto;
}

.sk-line-full {
  width: 100%;
  height: 14px;
  margin-bottom: var(--s1);
}

.sk-line-short {
  width: 70%;
  height: 14px;
  margin-bottom: var(--s2);
}

.sk-actions {
  border-top: 1px solid var(--n3);
  padding-top: var(--s2);
  display: flex;
  justify-content: flex-end;
  gap: var(--s3);
}

.sk-action {
  width: 40px;
  height: 14px;
}

/* 表单弹层 */
.form-panel {
  display: flex;
  flex-direction: column;
  max-height: 88vh;
  padding-bottom: env(safe-area-inset-bottom);
}

.form-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s4) var(--s4) var(--s2);
}

.form-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.form-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 var(--s4);
}

.field {
  margin-bottom: var(--s3);
}

.field-label {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-bottom: var(--s1);
}

.field-input {
  width: 100%;
  height: 40px;
  border: 1px solid var(--n5);
  border-radius: var(--r-base);
  padding: 0 var(--s2);
  font-size: var(--fs-base);
  color: var(--n10);
  font-family: inherit;
  outline: none;
  background: var(--n1);
}

.field-input:focus {
  border-color: var(--c-primary);
}

.field-textarea {
  height: auto;
  padding: var(--s2);
  resize: none;
}

.field-row {
  display: flex;
  gap: var(--s2);
}

.field-row .field {
  flex: 1;
  min-width: 0;
}

.field-error {
  margin-top: var(--s1);
  font-size: var(--fs-sm);
  color: var(--c-error);
}

.region-error {
  margin: -4px 0 var(--s3);
}

.tag-row {
  display: flex;
  gap: var(--s2);
}

.tag-chip {
  padding: 6px var(--s4);
  background: var(--n3);
  border: 1px solid transparent;
  border-radius: var(--r-lg);
  font-size: var(--fs-base);
  color: var(--n9);
  font-family: inherit;
}

.tag-chip.on {
  background: #e6f4ff;
  border-color: var(--c-primary);
  color: var(--c-primary);
}

.switch-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3) 0;
}

.switch-title {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
}

.switch-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.form-foot {
  display: flex;
  gap: var(--s2);
  padding: var(--s3) var(--s4) var(--s4);
}

.foot-btn {
  flex: 1;
}
</style>
