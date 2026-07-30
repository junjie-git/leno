# 卖家管理后台 P1 批次 1（店铺设置 + 账号补全）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成 P1 批次 1 的 6 个页面（01-onboarding 4 页 + 08-account 2 页）、ImageUploader 通用组件、shop mock handler 与 01-onboarding 路由注册，全量验证通过后提交推送。

**Architecture:** 延续 P0 五段式模块结构（`api/ + types/ + views/ + routes.ts + index.ts`）。新增 `01-onboarding` 模块，补全 `08-account` 占位页。shared 层新增 `ImageUploader.vue` 并扩展 `StatusTag` 的 shop 状态映射。为满足"API 客户端使用 `http` 命名"的约定，在 `shared/http/index.ts` 追加 `client as http` 别名导出（非破坏性，P0 仍可用 `client`）。Mock 层新增 `handlers/shop.ts` 与种子数据，`/shops/me` 返回同时携带 `id`/`shopId`、`name`/`shopName` 的双形态对象，兼容 P0 `shop.store.ts` 与新 `shopApi`。乐观锁统一通过 `ConcurrencyError` + `Modal.confirm` 处理（参考 `ProductEdit.vue`）。

**Tech Stack:** Vue 3.5 + TypeScript 5.7 + Vite 6 + Ant Design Vue 4.2 + Pinia 2.3 + Vue Router 4.5 + axios 1.7 + Vitest 2.1 + axios-mock-adapter 2.1

---

## 关键设计决策（实施前必读）

1. **`http` 别名**：`shared/http/index.ts` 新增 `export { client as http } from './client'`。本批次所有新 API 客户端使用 `import { http, withIdempotency } from '@/shared/http'`，与 P0 `client` 并存。
2. **`/shops/me` 双形态 mock**：P0 `shop.store.ts` 读取 `shopId`/`shopName`/`status`/`qualificationsStatus`，新 `ShopInfoDto` 读取 `id`/`name`/`version`/`customerService`。mock 种子店铺对象同时包含两套字段，避免改动 P0 store。
3. **`StatusTag` shop 映射扩展**：新 `ShopInfoDto.status` 含 `Pending`/`Closed`，现有 shop 映射无这两项。在 `StatusTag.vue` 的 shop 表中追加 `Pending`（待审核，warning）与 `Closed`（已关闭，default），保留 P0 既有项不动。
4. **补充 `listQualifications` 端点**：spec 的 `shop.api.ts` 仅含 `uploadQualification`，但 `ShopQualifications.vue` 需要列表。新增 `GET /api/shops/me/qualifications`（API + mock + 测试），属必要的 spec 细化。
5. **写操作幂等键**：`submitApplication`/`updateMyShop`/`uploadQualification` 均注入 `withIdempotency()`，与 P0 `product.api` 一致并满足任务约束。
6. **Notifications.vue 采用 BE-4 策略**：后端通知端点待确认，本批次不创建通知 mock handler（不在批次 1 范围）。页面为完整 UI + BE-4 提示 + 空列表，不调用任何 API。
7. **响应解包**：所有 API 函数内部 `.then(r => r.data)` 解包（响应拦截器已 unwrap `ApiResponse.data`）。
8. **验证命令工作目录**：除特别说明外，所有 `pnpm` 命令在 `/workspace/web/seller` 下执行。

---

## File Structure

### 新建文件
| 文件 | 职责 |
|------|------|
| `web/seller/src/shared/components/ImageUploader.vue` | 通用图片上传（Upload + FileReader + 预览 + 大小/类型校验） |
| `web/seller/src/shared/components/ImageUploader.spec.ts` | ImageUploader 组件测试 |
| `web/seller/src/modules/01-onboarding/types/shop.dto.ts` | 店铺/资质 DTO |
| `web/seller/src/modules/01-onboarding/api/shop.api.ts` | 店铺 API 客户端 |
| `web/seller/src/modules/01-onboarding/api/shop.api.spec.ts` | 店铺 API 测试 |
| `web/seller/src/modules/01-onboarding/views/ShopApplication.vue` | 入驻申请（Steps 分步表单） |
| `web/seller/src/modules/01-onboarding/views/ShopQualifications.vue` | 资质管理（Upload + 列表） |
| `web/seller/src/modules/01-onboarding/views/ShopProfile.vue` | 店铺资料（乐观锁） |
| `web/seller/src/modules/01-onboarding/views/ShopPreview.vue` | 店铺前台预览（只读） |
| `web/seller/src/modules/01-onboarding/routes.ts` | 模块路由 |
| `web/seller/src/modules/01-onboarding/index.ts` | 模块出口 |
| `web/seller/src/shared/http/mock/handlers/shop.ts` | shop mock handler |

### 修改文件
| 文件 | 改动 |
|------|------|
| `web/seller/src/shared/http/index.ts` | 追加 `http` 别名导出 |
| `web/seller/src/shared/components/index.ts` | 导出 `ImageUploader` |
| `web/seller/src/shared/components/StatusTag.vue` | shop 映射追加 `Pending`/`Closed` |
| `web/seller/src/shared/components/StatusTag.spec.ts` | 追加新映射测试 |
| `web/seller/src/shared/http/mock/data/types.ts` | `MockSeed` 追加 `shop`/`qualifications` |
| `web/seller/src/shared/http/mock/data/seed.ts` | 追加店铺/资质种子与 builder |
| `web/seller/src/shared/http/mock/index.ts` | 注册 `registerShopHandlers` |
| `web/seller/src/modules/08-account/views/Profile.vue` | 占位 → 完整 Descriptions |
| `web/seller/src/modules/08-account/views/Notifications.vue` | 占位 → BE-4 完整 UI |
| `web/seller/src/app/router.ts` | 注册 01-onboarding 路由 |

---

## Task 1: shared 组件 ImageUploader + StatusTag shop 映射扩展

**Files:**
- Create: `web/seller/src/shared/components/ImageUploader.vue`
- Create: `web/seller/src/shared/components/ImageUploader.spec.ts`
- Modify: `web/seller/src/shared/components/index.ts`
- Modify: `web/seller/src/shared/components/StatusTag.vue`
- Modify: `web/seller/src/shared/components/StatusTag.spec.ts`

- [ ] **Step 1: 先写 ImageUploader 失败测试**

创建 `web/seller/src/shared/components/ImageUploader.spec.ts`：

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { Upload } from 'ant-design-vue'
import ImageUploader from './ImageUploader.vue'

describe('shared/components/ImageUploader', () => {
  it('未设置 modelValue 时渲染上传触发区与 label', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 5 * 1024 * 1024, label: '上传 Logo' },
    })
    expect(wrapper.html()).toContain('上传 Logo')
    expect(wrapper.html()).toContain('ant-upload')
  })

  it('设置 modelValue 时回填预览（fileList 长度为 1）', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: 'data:image/png;base64,AAAA', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    expect(upload.props('fileList')).toHaveLength(1)
    expect(upload.props('fileList')[0].url).toBe('data:image/png;base64,AAAA')
  })

  it('modelValue 清空时恢复上传触发区', async () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: 'data:image/png;base64,AAAA', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    await wrapper.setProps({ modelValue: '' })
    const upload = wrapper.findComponent(Upload)
    expect(upload.props('fileList')).toHaveLength(0)
  })

  it('beforeUpload 拒绝超过 maxSize 的文件并 emit error', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const beforeUpload = upload.props('beforeUpload') as (f: { name: string; size: number; type: string }) => boolean
    const bigFile = { name: 'a.png', size: 10 * 1024, type: 'image/png' }
    expect(beforeUpload(bigFile)).toBe(false)
    expect(wrapper.emitted('error')).toBeTruthy()
  })

  it('beforeUpload 拒绝不匹配 accept 的文件类型并 emit error', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const beforeUpload = upload.props('beforeUpload') as (f: { name: string; size: number; type: string }) => boolean
    expect(beforeUpload({ name: 'a.txt', size: 100, type: 'text/plain' })).toBe(false)
    expect(wrapper.emitted('error')).toBeTruthy()
  })

  it('beforeUpload 接受合法文件', () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png,.jpg', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const beforeUpload = upload.props('beforeUpload') as (f: { name: string; size: number; type: string }) => boolean
    expect(beforeUpload({ name: 'a.png', size: 1024, type: 'image/png' })).toBe(true)
    expect(wrapper.emitted('error')).toBeFalsy()
  })

  it('customRequest 读取文件为 data URL 并 emit update:modelValue', async () => {
    const wrapper = mount(ImageUploader, {
      props: { modelValue: '', accept: '.png', maxSize: 5 * 1024 * 1024 },
    })
    const upload = wrapper.findComponent(Upload)
    const customRequest = upload.props('customRequest') as (o: {
      file: unknown
      onSuccess?: (resp: unknown, file: unknown) => void
      onError?: (e: Error) => void
    }) => void
    const file = new File(['data'], 'test.png', { type: 'image/png' })
    const onSuccess = vi.fn()
    customRequest({ file, onSuccess, onError: vi.fn() })
    await new Promise((resolve) => setTimeout(resolve, 0))
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    expect(typeof emitted![0][0]).toBe('string')
    expect(emitted![0][0] as string).toMatch(/^data:image\/png;base64,/)
    expect(onSuccess).toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/ImageUploader.spec.ts`
Expected: FAIL（`Cannot find module './ImageUploader.vue'`）

- [ ] **Step 3: 实现 ImageUploader.vue**

创建 `web/seller/src/shared/components/ImageUploader.vue`：

```vue
<script setup lang="ts">
import { ref, watch } from 'vue'
import { Upload, message } from 'ant-design-vue'
import type { UploadFile, UploadProps } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'

/**
 * 通用图片上传组件
 *
 * 封装 ant-design-vue Upload（picture-card）+ FileReader 转 data URL + 预览 + 大小/类型校验。
 * 用于店铺 Logo 等图片字段：modelValue 为 data URL 或远程 URL。
 */
const props = withDefaults(
  defineProps<{
    /** 当前 URL（data URL 或远程 URL） */
    modelValue: string
    /** 接受的文件类型，如 '.jpg,.png,.webp' */
    accept: string
    /** 最大字节数 */
    maxSize: number
    /** 上传区域提示文字 */
    label?: string
    /** 禁用 */
    disabled?: boolean
  }>(),
  {
    label: '上传图片',
    disabled: false,
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'error', message: string): void
}>()

const fileList = ref<UploadFile[]>([])

watch(
  () => props.modelValue,
  (url) => {
    if (url) {
      fileList.value = [
        { uid: '-1', name: 'image', status: 'done', url } as UploadFile,
      ]
    } else {
      fileList.value = []
    }
  },
  { immediate: true },
)

const beforeUpload: UploadProps['beforeUpload'] = (file) => {
  const acceptList = props.accept
    .split(',')
    .map((s) => s.trim().toLowerCase())
    .filter(Boolean)
  const ext = '.' + (file.name.split('.').pop() ?? '').toLowerCase()
  const typeOk =
    acceptList.includes(ext) || acceptList.some((a) => file.type === a)
  if (!typeOk) {
    const msg = `不支持的文件类型，仅支持 ${props.accept}`
    message.error(msg)
    emit('error', msg)
    return false
  }
  if (file.size > props.maxSize) {
    const mb = (props.maxSize / 1024 / 1024).toFixed(1)
    const msg = `文件大小超过限制（最大 ${mb}MB）`
    message.error(msg)
    emit('error', msg)
    return false
  }
  return true
}

const customRequest: UploadProps['customRequest'] = (options) => {
  const { file, onSuccess, onError } = options
  const raw = file as File
  const reader = new FileReader()
  reader.onload = () => {
    const dataUrl = reader.result as string
    emit('update:modelValue', dataUrl)
    onSuccess?.({ url: dataUrl }, file)
  }
  reader.onerror = () => {
    const msg = '读取文件失败'
    message.error(msg)
    emit('error', msg)
    onError?.(new Error(msg))
  }
  reader.readAsDataURL(raw)
}

function onRemove(): boolean {
  emit('update:modelValue', '')
  fileList.value = []
  return false
}
</script>

<template>
  <Upload
    :file-list="fileList"
    list-type="picture-card"
    :max-count="1"
    :accept="accept"
    :disabled="disabled"
    :before-upload="beforeUpload"
    :custom-request="customRequest"
    @remove="onRemove"
  >
    <div v-if="fileList.length === 0" class="image-uploader-trigger">
      <PlusOutlined />
      <div class="image-uploader-text">{{ label }}</div>
    </div>
  </Upload>
</template>

<style scoped>
.image-uploader-trigger {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #8c8c8c;
}
.image-uploader-text {
  font-size: 12px;
  margin-top: 4px;
}
</style>
```

- [ ] **Step 4: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/ImageUploader.spec.ts`
Expected: PASS（7 tests passed）

- [ ] **Step 5: 导出 ImageUploader**

修改 `web/seller/src/shared/components/index.ts`，在末尾追加一行：

```typescript
export { default as ImageUploader } from './ImageUploader.vue'
```

- [ ] **Step 6: 扩展 StatusTag shop 映射 — 先写失败测试**

修改 `web/seller/src/shared/components/StatusTag.spec.ts`，在 shop 类型测试块末尾（`Rejected` 用例之后）追加：

```typescript
  it('shop 类型 + Pending 状态渲染 warning tag（待审核）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Pending' } })
    expect(wrapper.html()).toContain('待审核')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('shop 类型 + Closed 状态渲染 default tag（已关闭）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Closed' } })
    expect(wrapper.html()).toContain('已关闭')
  })
```

- [ ] **Step 7: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/StatusTag.spec.ts`
Expected: FAIL（`Pending` 渲染为原始文本而非"待审核"）

- [ ] **Step 8: 扩展 StatusTag.vue shop 映射**

修改 `web/seller/src/shared/components/StatusTag.vue`，将 `shop` 映射块替换为（追加 `Pending` 与 `Closed`，保留既有项）：

```typescript
  shop: {
    PendingReview: { label: '审核中', color: 'warning' },
    Pending: { label: '待审核', color: 'warning' },
    Active: { label: '正常', color: 'success' },
    Suspended: { label: '暂停', color: 'error' },
    Rejected: { label: '已驳回', color: 'error' },
    Closed: { label: '已关闭', color: 'default' },
  },
```

- [ ] **Step 9: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/StatusTag.spec.ts`
Expected: PASS（全部用例通过，含新增 2 个）

- [ ] **Step 10: 提交**

```bash
git add web/seller/src/shared/components/ImageUploader.vue web/seller/src/shared/components/ImageUploader.spec.ts web/seller/src/shared/components/index.ts web/seller/src/shared/components/StatusTag.vue web/seller/src/shared/components/StatusTag.spec.ts
git commit -m "feat(seller): add ImageUploader component and extend StatusTag shop mapping"
```

---

## Task 2: 01-onboarding 类型与 API 客户端（含 http 别名）

**Files:**
- Modify: `web/seller/src/shared/http/index.ts`
- Create: `web/seller/src/modules/01-onboarding/types/shop.dto.ts`
- Create: `web/seller/src/modules/01-onboarding/api/shop.api.spec.ts`
- Create: `web/seller/src/modules/01-onboarding/api/shop.api.ts`

- [ ] **Step 1: 在 shared/http/index.ts 追加 http 别名**

修改 `web/seller/src/shared/http/index.ts`，在 `export { client, withIdempotency } from './client'` 行之后追加：

```typescript
export { client as http } from './client'
```

修改后该文件前部应为：

```typescript
export { client, withIdempotency } from './client'
export { client as http } from './client'
export { withIdempotency as withIdempotencyKey, generateIdempotencyKey } from './idempotency'
```

- [ ] **Step 2: 创建 shop.dto.ts**

创建 `web/seller/src/modules/01-onboarding/types/shop.dto.ts`：

```typescript
/**
 * 01-onboarding 店铺设置 DTO
 *
 * 与后端 ShopController 对接：
 * - POST /api/shops/application          提交入驻申请
 * - GET  /api/shops/me                   查询当前卖家店铺资料
 * - PUT  /api/shops/me                   更新店铺资料（含客服联系方式 + version 乐观锁）
 * - GET  /api/shops/me/qualifications    资质列表
 * - POST /api/shops/me/qualifications    上传资质（multipart/form-data）
 */

/** 店铺状态 */
export type ShopStatus = 'Pending' | 'Active' | 'Suspended' | 'Closed'

/** 客服联系方式 */
export interface CustomerServiceDto {
  phone: string
  email?: string
  onlineAccount?: string
}

/** 店铺基础信息 */
export interface ShopInfoDto {
  id: string
  name: string
  logo?: string
  description?: string
  status: ShopStatus
  mainCategory?: string
  customerService: CustomerServiceDto
  version: number
  createdAt: string
  updatedAt: string
}

/** 入驻申请 DTO */
export interface ShopApplicationDto {
  name: string
  mainCategory: string
  description?: string
  contactPhone: string
  contactEmail?: string
}

/** 更新店铺信息 DTO（含乐观锁 version） */
export interface UpdateShopInfoDto {
  name: string
  logo?: string
  description?: string
  customerService: CustomerServiceDto
  version: number
}

/** 资质类型 */
export type QualificationType = 'BusinessLicense' | 'IdCard' | 'BankAccount' | 'Other'

/** 资质状态 */
export type QualificationStatus = 'Pending' | 'Approved' | 'Rejected'

/** 资质文件 */
export interface QualificationDto {
  id: string
  type: QualificationType
  fileName: string
  fileUrl: string
  status: QualificationStatus
  submittedAt: string
  auditedAt?: string
  rejectReason?: string
}

/** 上传资质 DTO */
export interface UploadQualificationDto {
  file: File
  type: QualificationType
}
```

- [ ] **Step 3: 先写 shop.api 失败测试**

创建 `web/seller/src/modules/01-onboarding/api/shop.api.spec.ts`：

```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { shopApi } from './shop.api'
import { http, withIdempotency } from '@/shared/http'

/**
 * shopApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('shopApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  describe('submitApplication', () => {
    it('调用 POST /shops/application 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(mockResponse({ id: 'shop-001', version: 1 }))
      const body = { name: '示例店', mainCategory: '服装', contactPhone: '13800138000' }
      await shopApi.submitApplication(body)

      expect(http.post).toHaveBeenCalledWith('/shops/application', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })

    it('返回解包后的 ShopInfoDto', async () => {
      const shop = {
        id: 'shop-001',
        name: '示例店',
        status: 'Pending',
        customerService: { phone: '13800138000' },
        version: 1,
        createdAt: '2026-01-15T10:00:00Z',
        updatedAt: '2026-01-15T10:00:00Z',
      }
      vi.mocked(http.post).mockResolvedValue(mockResponse(shop))
      const result = await shopApi.submitApplication({
        name: '示例店',
        mainCategory: '服装',
        contactPhone: '13800138000',
      })
      expect(result).toEqual(shop)
    })
  })

  describe('getMyShop', () => {
    it('调用 GET /shops/me', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse({ id: 'shop-001' }))
      await shopApi.getMyShop()
      expect(http.get).toHaveBeenCalledWith('/shops/me')
    })
  })

  describe('updateMyShop', () => {
    it('调用 PUT /shops/me 带 Idempotency-Key 与 version', async () => {
      vi.mocked(http.put).mockResolvedValue(mockResponse({ id: 'shop-001', version: 2 }))
      const body = {
        name: '示例店',
        customerService: { phone: '13800138000' },
        version: 1,
      }
      await shopApi.updateMyShop(body)

      expect(http.put).toHaveBeenCalledWith('/shops/me', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('listQualifications', () => {
    it('调用 GET /shops/me/qualifications', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse([]))
      await shopApi.listQualifications()
      expect(http.get).toHaveBeenCalledWith('/shops/me/qualifications')
    })

    it('返回解包后的资质数组', async () => {
      const list = [
        {
          id: 'qual-001',
          type: 'BusinessLicense',
          fileName: '营业执照.pdf',
          fileUrl: '',
          status: 'Approved',
          submittedAt: '2026-01-15T10:00:00Z',
        },
      ]
      vi.mocked(http.get).mockResolvedValue(mockResponse(list))
      const result = await shopApi.listQualifications()
      expect(result).toEqual(list)
    })
  })

  describe('uploadQualification', () => {
    it('调用 POST /shops/me/qualifications 带 FormData 与 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(
        mockResponse({
          id: 'qual-004',
          type: 'IdCard',
          fileName: '身份证.jpg',
          fileUrl: '',
          status: 'Pending',
          submittedAt: '2026-07-30T10:00:00Z',
        }),
      )
      const file = new File(['x'], '身份证.jpg', { type: 'image/jpeg' })
      await shopApi.uploadQualification({ file, type: 'IdCard' })

      expect(http.post).toHaveBeenCalledTimes(1)
      const [url, data, config] = vi.mocked(http.post).mock.calls[0]
      expect(url).toBe('/shops/me/qualifications')
      expect(data).toBeInstanceOf(FormData)
      expect(config).toEqual({ headers: { 'Idempotency-Key': 'mock-key' } })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })
})
```

- [ ] **Step 4: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/modules/01-onboarding/api/shop.api.spec.ts`
Expected: FAIL（`Cannot find module './shop.api'`）

- [ ] **Step 5: 实现 shop.api.ts**

创建 `web/seller/src/modules/01-onboarding/api/shop.api.ts`：

```typescript
import { http, withIdempotency } from '@/shared/http'
import type {
  ShopApplicationDto,
  ShopInfoDto,
  UpdateShopInfoDto,
  QualificationDto,
  UploadQualificationDto,
} from '../types/shop.dto'

/**
 * 店铺 API 客户端
 *
 * 与后端 ShopController 对接（响应拦截器已解包 ApiResponse.data，
 * 调用方拿到的就是业务负载）：
 * - POST /api/shops/application          提交入驻申请（幂等）
 * - GET  /api/shops/me                   查询当前卖家店铺资料
 * - PUT  /api/shops/me                   更新店铺资料（幂等 + version 乐观锁）
 * - GET  /api/shops/me/qualifications    资质列表
 * - POST /api/shops/me/qualifications    上传资质（multipart，幂等）
 */
export const shopApi = {
  /** 提交入驻申请 */
  submitApplication(body: ShopApplicationDto): Promise<ShopInfoDto> {
    return http
      .post<ShopInfoDto>('/shops/application', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 查询当前卖家店铺资料 */
  getMyShop(): Promise<ShopInfoDto> {
    return http.get<ShopInfoDto>('/shops/me').then((r) => r.data)
  },

  /** 更新店铺基础信息（含客服联系方式 + version 乐观锁） */
  updateMyShop(body: UpdateShopInfoDto): Promise<ShopInfoDto> {
    return http
      .put<ShopInfoDto>('/shops/me', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 资质列表 */
  listQualifications(): Promise<QualificationDto[]> {
    return http
      .get<QualificationDto[]>('/shops/me/qualifications')
      .then((r) => r.data)
  },

  /** 上传店铺资质（multipart/form-data） */
  uploadQualification(body: UploadQualificationDto): Promise<QualificationDto> {
    const formData = new FormData()
    formData.append('file', body.file)
    formData.append('type', body.type)
    return http
      .post<QualificationDto>('/shops/me/qualifications', formData, withIdempotency())
      .then((r) => r.data)
  },
}
```

- [ ] **Step 6: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/modules/01-onboarding/api/shop.api.spec.ts`
Expected: PASS（8 tests passed）

- [ ] **Step 7: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 8: 提交**

```bash
git add web/seller/src/shared/http/index.ts web/seller/src/modules/01-onboarding/types/shop.dto.ts web/seller/src/modules/01-onboarding/api/shop.api.ts web/seller/src/modules/01-onboarding/api/shop.api.spec.ts
git commit -m "feat(seller): add shop DTO and API client for 01-onboarding module"
```

---

## Task 3: shop mock handler + 种子数据扩展 + 装配注册

**Files:**
- Modify: `web/seller/src/shared/http/mock/data/types.ts`
- Modify: `web/seller/src/shared/http/mock/data/seed.ts`
- Create: `web/seller/src/shared/http/mock/handlers/shop.ts`
- Modify: `web/seller/src/shared/http/mock/index.ts`

- [ ] **Step 1: 扩展 MockSeed 类型**

修改 `web/seller/src/shared/http/mock/data/types.ts`，在 `MockSeed` 接口中追加两个字段（保留既有字段）：

```typescript
export interface MockSeed {
  menus: unknown[]
  onlineUsers: unknown[]
  loginLogs: unknown[]
  redisKeys: unknown[]
  redisInfo: unknown
  keyspaces: unknown[]
  serverSnapshot: unknown
  serverHistory: { cpu: unknown[]; memory: unknown[]; diskIo: unknown[] }
  shop: unknown
  qualifications: unknown[]
  nextId: number
}
```

- [ ] **Step 2: 扩展 seed.ts — 注入 shop/qualifications 种子**

修改 `web/seller/src/shared/http/mock/data/seed.ts`：

2a. 在 `ensureSeedData` 函数的 seed 初始化对象中，`serverHistory` 之后、`nextId` 之前追加两行：

```typescript
    shop: buildShopSeed(),
    qualifications: buildQualificationSeed(),
```

修改后 `ensureSeedData` 内 seed 对象片段：

```typescript
  const seed: MockSeed = {
    menus: buildMenuSeed(),
    onlineUsers: buildOnlineUserSeed(),
    loginLogs: buildLoginLogSeed(),
    redisKeys: buildRedisKeySeed(),
    redisInfo: buildRedisInfoSeed(),
    keyspaces: buildKeyspaceSeed(),
    serverSnapshot: buildServerSnapshotSeed(),
    serverHistory: { cpu: [], memory: [], diskIo: [] },
    shop: buildShopSeed(),
    qualifications: buildQualificationSeed(),
    nextId: 1000,
  }
```

2b. 在文件末尾（`advanceServerHistory` 函数之后）追加两个 builder：

```typescript
// ===== 店铺种子（双形态：兼容 P0 shop.store 与 P1 ShopInfoDto）=====

function buildShopSeed(): unknown {
  return {
    // P1 ShopInfoDto 形态
    id: 'shop-001',
    name: '示例服饰旗舰店',
    logo: '',
    description: '专注高品质男女装，20年匠心工艺',
    status: 'Active',
    mainCategory: '服装',
    customerService: {
      phone: '13800138000',
      email: 'service@example.com',
      onlineAccount: 'wx_shop001',
    },
    version: 1,
    createdAt: '2026-01-15T10:00:00Z',
    updatedAt: '2026-07-01T12:00:00Z',
    // P0 shop.store ShopDto 形态（兼容字段）
    shopId: 'shop-001',
    shopName: '示例服饰旗舰店',
    qualificationsStatus: {
      BusinessLicense: 'Approved',
      IdCard: 'Approved',
      BankAccount: 'Pending',
    },
  }
}

// ===== 资质种子（3 条）=====

function buildQualificationSeed(): unknown[] {
  return [
    {
      id: 'qual-001',
      type: 'BusinessLicense',
      fileName: '营业执照.pdf',
      fileUrl: '',
      status: 'Approved',
      submittedAt: '2026-01-15T10:00:00Z',
      auditedAt: '2026-01-16T09:00:00Z',
    },
    {
      id: 'qual-002',
      type: 'IdCard',
      fileName: '身份证.jpg',
      fileUrl: '',
      status: 'Approved',
      submittedAt: '2026-01-15T10:00:00Z',
      auditedAt: '2026-01-16T09:00:00Z',
    },
    {
      id: 'qual-003',
      type: 'BankAccount',
      fileName: '银行账户信息.pdf',
      fileUrl: '',
      status: 'Pending',
      submittedAt: '2026-07-20T14:00:00Z',
    },
  ]
}
```

- [ ] **Step 3: 实现 handlers/shop.ts**

创建 `web/seller/src/shared/http/mock/handlers/shop.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData, nextId } from '../data/seed'

/**
 * 店铺 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /shops/...）：
 * - POST   /shops/application          提交入驻申请
 * - GET    /shops/me                   查询当前卖家店铺资料
 * - PUT    /shops/me                   更新店铺资料（乐观锁 version）
 * - GET    /shops/me/qualifications    资质列表
 * - POST   /shops/me/qualifications    上传资质（multipart/form-data）
 *
 * 店铺对象为双形态：同时含 id/name/version/customerService（P1）与
 * shopId/shopName/qualificationsStatus（P0 shop.store），兼容两个消费方。
 */
export function registerShopHandlers(mock: MockAdapter): void {
  // 提交入驻申请
  mock.onPost('/shops/application').reply((config) => {
    const seed = loadSeedData()
    const body = JSON.parse(config.data || '{}')
    if (!body.name || !body.mainCategory || !body.contactPhone) {
      return [200, { code: 40001, message: '店铺名称、主营类目、联系电话必填', data: null }]
    }
    const now = new Date().toISOString()
    const shop = seed.shop as any
    shop.name = body.name
    shop.shopName = body.name
    shop.mainCategory = body.mainCategory
    if (body.description) shop.description = body.description
    shop.status = 'Pending'
    shop.customerService = {
      phone: body.contactPhone,
      email: body.contactEmail,
    }
    shop.version = 1
    shop.createdAt = now
    shop.updatedAt = now
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: shop }]
  })

  // 查询当前卖家店铺资料
  mock.onGet('/shops/me').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.shop }]
  })

  // 更新店铺资料（乐观锁）
  mock.onPut('/shops/me').reply((config) => {
    const seed = loadSeedData()
    const shop = seed.shop as any
    const body = JSON.parse(config.data || '{}')
    if (typeof body.version === 'number' && body.version !== shop.version) {
      return [
        409,
        {
          code: 409,
          message: '店铺资料已被他人修改',
          currentVersion: shop.version,
          data: null,
        },
      ]
    }
    if (body.name) {
      shop.name = body.name
      shop.shopName = body.name
    }
    if (body.logo !== undefined) shop.logo = body.logo
    if (body.description !== undefined) shop.description = body.description
    if (body.customerService) shop.customerService = body.customerService
    shop.version = (shop.version ?? 1) + 1
    shop.updatedAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: shop }]
  })

  // 资质列表
  mock.onGet('/shops/me/qualifications').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.qualifications }]
  })

  // 上传资质（multipart/form-data）
  mock.onPost('/shops/me/qualifications').reply((config) => {
    const seed = loadSeedData()
    const data = config.data
    let type = 'Other'
    let fileName = 'unknown'
    if (typeof FormData !== 'undefined' && data instanceof FormData) {
      type = String(data.get('type') || 'Other')
      const file = data.get('file') as File | null
      fileName = file?.name ?? 'unknown'
    }
    const now = new Date().toISOString()
    const qual = {
      id: nextId(seed, 'qual'),
      type,
      fileName,
      fileUrl: '',
      status: 'Pending',
      submittedAt: now,
    }
    ;(seed.qualifications as any[]).push(qual)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: qual }]
  })
}
```

- [ ] **Step 4: 在 mock/index.ts 注册 shop handler**

修改 `web/seller/src/shared/http/mock/index.ts`：

4a. 在 import 区追加（`registerServerMonitorHandlers` 之后）：

```typescript
import { registerShopHandlers } from './handlers/shop'
```

4b. 在 `registerServerMonitorHandlers(mock)` 之后追加一行：

```typescript
  registerShopHandlers(mock)
```

4c. 将启动日志行改为：

```typescript
  console.log('[Mock] 已启用 6 个 handler，共 24 个 endpoint')
```

- [ ] **Step 5: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 6: 提交**

```bash
git add web/seller/src/shared/http/mock/data/types.ts web/seller/src/shared/http/mock/data/seed.ts web/seller/src/shared/http/mock/handlers/shop.ts web/seller/src/shared/http/mock/index.ts
git commit -m "feat(seller): add shop mock handler and seed data for 01-onboarding"
```

---

## Task 4: ShopApplication.vue — 入驻申请（Steps 分步表单）

**Files:**
- Create: `web/seller/src/modules/01-onboarding/views/ShopApplication.vue`

- [ ] **Step 1: 实现 ShopApplication.vue**

创建 `web/seller/src/modules/01-onboarding/views/ShopApplication.vue`：

```vue
<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Steps,
  Step,
  Form,
  FormItem,
  Input,
  Select,
  Button,
  Space,
  Descriptions,
  DescriptionsItem,
  message,
  Modal,
} from 'ant-design-vue'
import { shopApi } from '../api/shop.api'
import type { ShopApplicationDto } from '../types/shop.dto'
import { IdempotencyButton } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * 入驻申请页
 *
 * 路由 /shop/application，权限 shop:application:submit，requiresActiveShop: false
 * 三步式表单：1) 基础信息 2) 确认信息 3) 提交（IdempotencyButton + 幂等键）
 * 成功后跳转 /shop/qualifications。
 */

const router = useRouter()

const current = ref(0)
const submitting = ref(false)
const formRef = ref()

const form = reactive({
  name: '',
  mainCategory: '' as string,
  description: '',
  contactPhone: '',
  contactEmail: '',
})

const rules = {
  name: [
    { required: true, message: '请输入店铺名称', trigger: 'blur' },
    { min: 2, max: 32, message: '店铺名称长度为 2-32 字', trigger: 'blur' },
  ],
  mainCategory: [{ required: true, message: '请选择主营类目', trigger: 'change' }],
  contactPhone: [
    { required: true, message: '请输入联系电话', trigger: 'blur' },
    { pattern: /^1[3-9]\d{9}$/, message: '请输入有效的手机号', trigger: 'blur' },
  ],
  contactEmail: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
  description: [{ max: 500, message: '描述最长 500 字', trigger: 'blur' }],
}

const categoryOptions: Array<{ label: string; value: string }> = [
  { label: '服装', value: '服装' },
  { label: '数码', value: '数码' },
  { label: '家居', value: '家居' },
  { label: '美妆', value: '美妆' },
  { label: '食品', value: '食品' },
  { label: '母婴', value: '母婴' },
  { label: '其他', value: '其他' },
]

const canNext = computed(() => {
  return (
    form.name.trim().length >= 2 &&
    form.name.trim().length <= 32 &&
    !!form.mainCategory &&
    /^1[3-9]\d{9}$/.test(form.contactPhone)
  )
})

async function next(): Promise<void> {
  try {
    await formRef.value?.validate()
    current.value = 1
  } catch {
    message.warning('请完善必填项后再进入下一步')
  }
}

function prev(): void {
  current.value = 0
}

function buildBody(): ShopApplicationDto {
  const body: ShopApplicationDto = {
    name: form.name.trim(),
    mainCategory: form.mainCategory,
    contactPhone: form.contactPhone.trim(),
  }
  if (form.description.trim()) body.description = form.description.trim()
  if (form.contactEmail.trim()) body.contactEmail = form.contactEmail.trim()
  return body
}

function resetForm(): void {
  form.name = ''
  form.mainCategory = ''
  form.description = ''
  form.contactPhone = ''
  form.contactEmail = ''
  current.value = 0
}

function handleConcurrencyError(): void {
  Modal.confirm({
    title: '资源冲突',
    content: '检测到已存在入驻申请或店铺信息已变更，是否重置表单后重新填写？',
    okText: '重置表单',
    cancelText: '返回首页',
    onOk: () => {
      resetForm()
    },
    onCancel: () => {
      router.push('/dashboard/overview')
    },
  })
}

async function onSubmit(): Promise<void> {
  submitting.value = true
  try {
    await shopApi.submitApplication(buildBody())
    message.success('入驻申请已提交，请继续上传资质文件')
    router.push('/shop/qualifications')
  } catch (e) {
    logger.error('提交入驻申请失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('提交失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="shop-application-page">
    <Breadcrumb class="shop-application-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>入驻申请</BreadcrumbItem>
    </Breadcrumb>

    <Card class="shop-application-card" :bordered="true">
      <Steps :current="current" class="shop-application-steps">
        <Step title="基础信息" description="填写店铺与联系方式" />
        <Step title="确认信息" description="核对填写内容" />
        <Step title="提交申请" description="提交后进入资质上传" />
      </Steps>

      <!-- 步骤 1：基础信息 -->
      <div v-if="current === 0" class="shop-application-step-body">
        <Form
          ref="formRef"
          :model="form"
          :rules="rules"
          layout="vertical"
          :label-col="{ style: { width: '120px' } }"
        >
          <FormItem label="店铺名称" name="name" required>
            <Input
              v-model:value="form.name"
              placeholder="请输入店铺名称（2-32 字）"
              :maxlength="32"
              show-count
            />
          </FormItem>
          <FormItem label="主营类目" name="mainCategory" required>
            <Select
              v-model:value="form.mainCategory"
              placeholder="请选择主营类目"
              :options="categoryOptions"
            />
          </FormItem>
          <FormItem label="店铺描述" name="description">
            <Input
              v-model:value="form.description"
              type="textarea"
              :rows="4"
              placeholder="请输入店铺描述（选填，最长 500 字）"
              :maxlength="500"
              show-count
            />
          </FormItem>
          <FormItem label="联系电话" name="contactPhone" required>
            <Input
              v-model:value="form.contactPhone"
              placeholder="请输入手机号"
              :maxlength="11"
            />
          </FormItem>
          <FormItem label="联系邮箱" name="contactEmail">
            <Input
              v-model:value="form.contactEmail"
              placeholder="请输入邮箱（选填）"
            />
          </FormItem>
        </Form>
        <div class="shop-application-actions">
          <Button type="primary" :disabled="!canNext" @click="next">下一步</Button>
        </div>
      </div>

      <!-- 步骤 2：确认信息 -->
      <div v-else-if="current === 1" class="shop-application-step-body">
        <Descriptions :column="1" bordered>
          <DescriptionsItem label="店铺名称">{{ form.name }}</DescriptionsItem>
          <DescriptionsItem label="主营类目">{{ form.mainCategory }}</DescriptionsItem>
          <DescriptionsItem label="店铺描述">{{ form.description || '—' }}</DescriptionsItem>
          <DescriptionsItem label="联系电话">{{ form.contactPhone }}</DescriptionsItem>
          <DescriptionsItem label="联系邮箱">{{ form.contactEmail || '—' }}</DescriptionsItem>
        </Descriptions>
        <div class="shop-application-actions">
          <Space>
            <Button @click="prev">上一步</Button>
            <Button type="primary" @click="current = 2">下一步</Button>
          </Space>
        </div>
      </div>

      <!-- 步骤 3：提交 -->
      <div v-else class="shop-application-step-body">
        <div class="shop-application-confirm-text">
          请确认以上信息无误，点击提交后将进入资质上传环节。
        </div>
        <div class="shop-application-actions">
          <Space>
            <Button @click="prev">上一步</Button>
            <IdempotencyButton :loading="submitting" @click="onSubmit">
              提交申请
            </IdempotencyButton>
          </Space>
        </div>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.shop-application-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-application-breadcrumb {
  font-size: 14px;
}
.shop-application-card {
  border-radius: 8px;
}
.shop-application-steps {
  margin-bottom: 32px;
}
.shop-application-step-body {
  max-width: 640px;
  margin: 0 auto;
}
.shop-application-actions {
  margin-top: 24px;
  display: flex;
  justify-content: flex-end;
}
.shop-application-confirm-text {
  padding: 24px;
  background: #fafafa;
  border-radius: 6px;
  color: #595959;
  font-size: 14px;
  line-height: 1.8;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/01-onboarding/views/ShopApplication.vue
git commit -m "feat(seller): add ShopApplication page with steps form"
```

---

## Task 5: ShopQualifications.vue — 资质管理（Upload + 列表）

**Files:**
- Create: `web/seller/src/modules/01-onboarding/views/ShopQualifications.vue`

- [ ] **Step 1: 实现 ShopQualifications.vue**

创建 `web/seller/src/modules/01-onboarding/views/ShopQualifications.vue`：

```vue
<script setup lang="ts">
import { ref, onMounted, h } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Button,
  Upload,
  Select,
  Tag,
  Tooltip,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import type { UploadProps } from 'ant-design-vue'
import { UploadOutlined } from '@ant-design/icons-vue'
import { shopApi } from '../api/shop.api'
import type {
  QualificationDto,
  QualificationType,
  QualificationStatus,
} from '../types/shop.dto'
import { EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 资质管理页
 *
 * 路由 /shop/qualifications，权限 shop:qualification:upload
 * - 资质列表表格（类型 / 文件名 / 状态 / 提交时间 / 审核时间 / 操作）
 * - 上传按钮（Upload，accept .jpg,.png,.pdf，maxSize 5MB，beforeUpload 校验）
 * - 资质类型选择（Select：营业执照 / 身份证 / 银行账户信息 / 其他）
 */

const loading = ref(false)
const uploading = ref(false)
const qualifications = ref<QualificationDto[]>([])
const uploadType = ref<QualificationType>('BusinessLicense')

const MAX_SIZE = 5 * 1024 * 1024

const typeLabels: Record<QualificationType, string> = {
  BusinessLicense: '营业执照',
  IdCard: '身份证',
  BankAccount: '银行账户信息',
  Other: '其他',
}

const statusMeta: Record<QualificationStatus, { color: string; label: string }> = {
  Pending: { color: 'warning', label: '待审核' },
  Approved: { color: 'success', label: '已通过' },
  Rejected: { color: 'error', label: '已驳回' },
}

const typeOptions: Array<{ label: string; value: QualificationType }> = [
  { label: '营业执照', value: 'BusinessLicense' },
  { label: '身份证', value: 'IdCard' },
  { label: '银行账户信息', value: 'BankAccount' },
  { label: '其他', value: 'Other' },
]

const columns: TableColumnsType = [
  { title: '类型', dataIndex: 'type', key: 'type', width: 140 },
  { title: '文件名', dataIndex: 'fileName', key: 'fileName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 120 },
  { title: '提交时间', dataIndex: 'submittedAt', key: 'submittedAt', width: 180 },
  { title: '审核时间', dataIndex: 'auditedAt', key: 'auditedAt', width: 180 },
  { title: '操作', key: 'action', width: 100 },
]

async function loadList(): Promise<void> {
  loading.value = true
  try {
    qualifications.value = await shopApi.listQualifications()
  } catch (e) {
    logger.error('加载资质列表失败', e)
    message.error('加载资质列表失败')
  } finally {
    loading.value = false
  }
}

const beforeUpload: UploadProps['beforeUpload'] = (file) => {
  const acceptList = ['.jpg', '.png', '.pdf']
  const ext = '.' + (file.name.split('.').pop() ?? '').toLowerCase()
  if (!acceptList.includes(ext)) {
    message.error('仅支持 .jpg / .png / .pdf 文件')
    return false
  }
  if (file.size > MAX_SIZE) {
    message.error('文件大小超过 5MB 限制')
    return false
  }
  return false // 阻止自动上传，由 customRequest 处理
}

const customRequest: UploadProps['customRequest'] = async (options) => {
  const { file, onSuccess, onError } = options
  const raw = file as File
  uploading.value = true
  try {
    const qual = await shopApi.uploadQualification({ file: raw, type: uploadType.value })
    qualifications.value = [...qualifications.value, qual]
    message.success('资质上传成功，等待审核')
    onSuccess?.({ url: qual.fileUrl }, file)
  } catch (e) {
    logger.error('上传资质失败', e)
    message.error('上传资质失败')
    onError?.(new Error('上传资质失败'))
  } finally {
    uploading.value = false
  }
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="shop-qualifications-page">
    <Breadcrumb class="shop-qualifications-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>资质管理</BreadcrumbItem>
    </Breadcrumb>

    <Card class="shop-qualifications-card" :bordered="true">
      <template #title>
        <span class="shop-qualifications-title">资质管理</span>
      </template>
      <template #extra>
        <div class="shop-qualifications-upload-bar">
          <Select
            v-model:value="uploadType"
            :options="typeOptions"
            style="width: 160px"
            placeholder="选择资质类型"
          />
          <Upload
            :before-upload="beforeUpload"
            :custom-request="customRequest"
            :show-upload-list="false"
            accept=".jpg,.png,.pdf"
          >
            <Button :icon="h(UploadOutlined)" :loading="uploading">上传资质</Button>
          </Upload>
        </div>
      </template>

      <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
      <EmptyState
        v-else-if="qualifications.length === 0"
        description="暂无资质文件，请点击右上角「上传资质」"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="qualifications"
        row-key="id"
        :pagination="false"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'type'">
            {{ typeLabels[record.type as QualificationType] || record.type }}
          </template>
          <template v-else-if="column.key === 'status'">
            <Tooltip v-if="record.status === 'Rejected' && record.rejectReason">
              <template #title>驳回原因：{{ record.rejectReason }}</template>
              <Tag :color="statusMeta[record.status as QualificationStatus].color">
                {{ statusMeta[record.status as QualificationStatus].label }}
              </Tag>
            </Tooltip>
            <Tag v-else :color="statusMeta[record.status as QualificationStatus].color">
              {{ statusMeta[record.status as QualificationStatus].label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'submittedAt'">
            {{ formatDateTime(record.submittedAt) }}
          </template>
          <template v-else-if="column.key === 'auditedAt'">
            {{ formatDateTime(record.auditedAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Button type="link" size="small" disabled>查看</Button>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.shop-qualifications-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-qualifications-breadcrumb {
  font-size: 14px;
}
.shop-qualifications-card {
  border-radius: 8px;
}
.shop-qualifications-title {
  font-size: 15px;
  font-weight: 500;
}
.shop-qualifications-upload-bar {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/01-onboarding/views/ShopQualifications.vue
git commit -m "feat(seller): add ShopQualifications page with upload and list"
```

---

## Task 6: ShopProfile.vue — 店铺资料（乐观锁）

**Files:**
- Create: `web/seller/src/modules/01-onboarding/views/ShopProfile.vue`

- [ ] **Step 1: 实现 ShopProfile.vue**

创建 `web/seller/src/modules/01-onboarding/views/ShopProfile.vue`：

```vue
<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Form,
  FormItem,
  Input,
  message,
  Modal,
  Skeleton,
} from 'ant-design-vue'
import { shopApi } from '../api/shop.api'
import type { UpdateShopInfoDto } from '../types/shop.dto'
import { ImageUploader, IdempotencyButton, StatusTag } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * 店铺资料页
 *
 * 路由 /shop/profile，权限 shop:profile:view
 * - GET /api/shops/me 拉取资料回填表单，记录 version
 * - PUT /api/shops/me 更新（含客服联系方式 + version 乐观锁）
 * - 409 冲突 → Modal.confirm「资源已被他人修改，是否刷新后重试？」
 */

const loading = ref(false)
const submitting = ref(false)
const currentVersion = ref(0)
const currentStatus = ref<string | null>(null)
const formRef = ref()

const form = reactive({
  name: '',
  logo: '' as string,
  description: '',
  customerService: {
    phone: '',
    email: '' as string,
    onlineAccount: '' as string,
  },
})

const rules = {
  name: [
    { required: true, message: '请输入店铺名称', trigger: 'blur' },
    { min: 2, max: 32, message: '店铺名称长度为 2-32 字', trigger: 'blur' },
  ],
  description: [{ max: 1000, message: '描述最长 1000 字', trigger: 'blur' }],
}

async function loadShop(): Promise<void> {
  loading.value = true
  try {
    const shop = await shopApi.getMyShop()
    form.name = shop.name
    form.logo = shop.logo ?? ''
    form.description = shop.description ?? ''
    form.customerService.phone = shop.customerService?.phone ?? ''
    form.customerService.email = shop.customerService?.email ?? ''
    form.customerService.onlineAccount = shop.customerService?.onlineAccount ?? ''
    currentVersion.value = shop.version
    currentStatus.value = shop.status
  } catch (e) {
    logger.error('加载店铺资料失败', e)
    message.error('加载店铺资料失败')
  } finally {
    loading.value = false
  }
}

function buildBody(): UpdateShopInfoDto {
  const body: UpdateShopInfoDto = {
    name: form.name.trim(),
    customerService: {
      phone: form.customerService.phone.trim(),
    },
    version: currentVersion.value,
  }
  if (form.logo) body.logo = form.logo
  if (form.description.trim()) body.description = form.description.trim()
  if (form.customerService.email.trim()) {
    body.customerService.email = form.customerService.email.trim()
  }
  if (form.customerService.onlineAccount.trim()) {
    body.customerService.onlineAccount = form.customerService.onlineAccount.trim()
  }
  return body
}

function handleConcurrencyError(): void {
  Modal.confirm({
    title: '资源已被他人修改',
    content: '该店铺资料已被他人修改，是否刷新后重试？',
    okText: '刷新后重试',
    cancelText: '取消',
    onOk: () => {
      return loadShop()
    },
  })
}

async function onSubmit(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    message.warning('请完善表单必填项')
    return
  }
  submitting.value = true
  try {
    const updated = await shopApi.updateMyShop(buildBody())
    currentVersion.value = updated.version
    currentStatus.value = updated.status
    message.success('保存成功')
  } catch (e) {
    logger.error('保存店铺资料失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('保存失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadShop()
})
</script>

<template>
  <div class="shop-profile-page">
    <Breadcrumb class="shop-profile-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>店铺资料</BreadcrumbItem>
    </Breadcrumb>

    <Card class="shop-profile-header" :bordered="true" size="small">
      <div class="shop-profile-header-inner">
        <span class="shop-profile-title">店铺资料</span>
        <template v-if="currentStatus">
          <span class="shop-profile-status-label">状态：</span>
          <StatusTag type="shop" :status="currentStatus" />
        </template>
      </div>
    </Card>

    <Skeleton v-if="loading" active :paragraph="{ rows: 8 }" />
    <div v-else class="shop-profile-body">
      <!-- 基础信息 -->
      <Card class="shop-profile-section" :bordered="true">
        <template #title>
          <span class="shop-profile-section-title">基础信息</span>
        </template>
        <Form
          ref="formRef"
          :model="form"
          :rules="rules"
          layout="vertical"
          :label-col="{ style: { width: '120px' } }"
        >
          <FormItem label="店铺名称" name="name" required>
            <Input
              v-model:value="form.name"
              placeholder="请输入店铺名称（2-32 字）"
              :maxlength="32"
              show-count
            />
          </FormItem>
          <FormItem label="店铺 Logo">
            <ImageUploader
              v-model="form.logo"
              accept=".jpg,.png,.webp"
              :max-size="5 * 1024 * 1024"
              label="上传 Logo"
            />
            <div class="shop-profile-hint">建议尺寸 200×200px，≤5MB，仅支持 JPG/PNG/WebP</div>
          </FormItem>
          <FormItem label="店铺描述" name="description">
            <Input
              v-model:value="form.description"
              type="textarea"
              :rows="4"
              placeholder="请输入店铺描述（选填，最长 1000 字）"
              :maxlength="1000"
              show-count
            />
          </FormItem>
        </Form>
      </Card>

      <!-- 客服联系方式 -->
      <Card class="shop-profile-section" :bordered="true">
        <template #title>
          <span class="shop-profile-section-title">客服联系方式</span>
        </template>
        <Form layout="vertical" :label-col="{ style: { width: '120px' } }">
          <FormItem label="客服电话" required>
            <Input
              v-model:value="form.customerService.phone"
              placeholder="请输入客服电话"
            />
          </FormItem>
          <FormItem label="客服邮箱">
            <Input
              v-model:value="form.customerService.email"
              placeholder="请输入客服邮箱（选填）"
            />
          </FormItem>
          <FormItem label="在线客服账号">
            <Input
              v-model:value="form.customerService.onlineAccount"
              placeholder="请输入在线客服账号（选填，如微信号）"
            />
          </FormItem>
        </Form>
      </Card>

      <!-- 底部保存 -->
      <div class="shop-profile-actions">
        <IdempotencyButton :loading="submitting" @click="onSubmit">保存</IdempotencyButton>
      </div>
    </div>
  </div>
</template>

<style scoped>
.shop-profile-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-profile-breadcrumb {
  font-size: 14px;
}
.shop-profile-header {
  border-radius: 8px;
}
.shop-profile-header-inner {
  display: flex;
  align-items: center;
  gap: 12px;
}
.shop-profile-title {
  font-size: 16px;
  font-weight: 500;
  color: #000000d9;
}
.shop-profile-status-label {
  font-size: 13px;
  color: #8c8c8c;
}
.shop-profile-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-profile-section {
  border-radius: 8px;
}
.shop-profile-section-title {
  font-size: 15px;
  font-weight: 500;
}
.shop-profile-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}
.shop-profile-actions {
  display: flex;
  justify-content: flex-end;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/01-onboarding/views/ShopProfile.vue
git commit -m "feat(seller): add ShopProfile page with optimistic lock"
```

---

## Task 7: ShopPreview.vue — 店铺前台预览（只读）

**Files:**
- Create: `web/seller/src/modules/01-onboarding/views/ShopPreview.vue`

- [ ] **Step 1: 实现 ShopPreview.vue**

创建 `web/seller/src/modules/01-onboarding/views/ShopPreview.vue`：

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Skeleton,
  Avatar,
  Typography,
} from 'ant-design-vue'
import { ShopOutlined } from '@ant-design/icons-vue'
import { shopApi } from '../api/shop.api'
import type { ShopInfoDto } from '../types/shop.dto'
import { StatusTag, EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'

/**
 * 店铺前台预览页（只读）
 *
 * 路由 /shop/preview，权限 shop:profile:view
 * GET /api/shops/me 拉取资料，以卡片形式模拟买家视角展示。
 */

const loading = ref(false)
const shop = ref<ShopInfoDto | null>(null)

async function loadShop(): Promise<void> {
  loading.value = true
  try {
    shop.value = await shopApi.getMyShop()
  } catch (e) {
    logger.error('加载店铺预览失败', e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadShop()
})
</script>

<template>
  <div class="shop-preview-page">
    <Breadcrumb class="shop-preview-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>店铺预览</BreadcrumbItem>
    </Breadcrumb>

    <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />
    <EmptyState
      v-else-if="!shop"
      description="暂无店铺资料，无法预览"
      action-text="去完善资料"
      @action="$router.push('/shop/profile')"
    />
    <div v-else class="shop-preview-body">
      <!-- 店铺头部 -->
      <Card class="shop-preview-card" :bordered="true">
        <div class="shop-preview-header">
          <Avatar
            :size="72"
            :src="shop.logo || undefined"
            class="shop-preview-logo"
          >
            <ShopOutlined v-if="!shop.logo" />
          </Avatar>
          <div class="shop-preview-header-info">
            <div class="shop-preview-name-row">
              <span class="shop-preview-name">{{ shop.name }}</span>
              <StatusTag type="shop" :status="shop.status" />
            </div>
            <div class="shop-preview-category">
              主营类目：{{ shop.mainCategory || '—' }}
            </div>
          </div>
        </div>
      </Card>

      <!-- 店铺描述 -->
      <Card class="shop-preview-card" :bordered="true">
        <template #title>
          <span class="shop-preview-section-title">店铺描述</span>
        </template>
        <Typography.Paragraph>
          {{ shop.description || '该店铺暂未填写描述。' }}
        </Typography.Paragraph>
      </Card>

      <!-- 客服联系方式 -->
      <Card class="shop-preview-card" :bordered="true">
        <template #title>
          <span class="shop-preview-section-title">客服联系方式</span>
        </template>
        <div class="shop-preview-cs-list">
          <div class="shop-preview-cs-row">
            <span class="shop-preview-cs-label">客服电话</span>
            <span class="shop-preview-cs-value">
              {{ shop.customerService?.phone || '—' }}
            </span>
          </div>
          <div class="shop-preview-cs-row">
            <span class="shop-preview-cs-label">客服邮箱</span>
            <span class="shop-preview-cs-value">
              {{ shop.customerService?.email || '—' }}
            </span>
          </div>
          <div class="shop-preview-cs-row">
            <span class="shop-preview-cs-label">在线客服账号</span>
            <span class="shop-preview-cs-value">
              {{ shop.customerService?.onlineAccount || '—' }}
            </span>
          </div>
        </div>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.shop-preview-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-preview-breadcrumb {
  font-size: 14px;
}
.shop-preview-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-preview-card {
  border-radius: 8px;
}
.shop-preview-header {
  display: flex;
  align-items: center;
  gap: 16px;
}
.shop-preview-logo {
  background: #fafafa;
  color: #8c8c8c;
  flex-shrink: 0;
}
.shop-preview-header-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.shop-preview-name-row {
  display: flex;
  align-items: center;
  gap: 12px;
}
.shop-preview-name {
  font-size: 20px;
  font-weight: 500;
  color: #000000d9;
}
.shop-preview-category {
  font-size: 13px;
  color: #8c8c8c;
}
.shop-preview-section-title {
  font-size: 15px;
  font-weight: 500;
}
.shop-preview-cs-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.shop-preview-cs-row {
  display: flex;
  align-items: center;
  gap: 16px;
}
.shop-preview-cs-label {
  width: 120px;
  color: #8c8c8c;
  font-size: 13px;
}
.shop-preview-cs-value {
  color: #000000d9;
  font-size: 14px;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/01-onboarding/views/ShopPreview.vue
git commit -m "feat(seller): add ShopPreview readonly page"
```

---

## Task 8: 01-onboarding routes.ts + index.ts

**Files:**
- Create: `web/seller/src/modules/01-onboarding/routes.ts`
- Create: `web/seller/src/modules/01-onboarding/index.ts`

- [ ] **Step 1: 实现 routes.ts**

创建 `web/seller/src/modules/01-onboarding/routes.ts`：

```typescript
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/shop/application',
    name: 'shop.application',
    component: () => import('./views/ShopApplication.vue'),
    meta: {
      title: '入驻申请',
      menuKey: 'shop.application',
      roles: ['Seller'],
      permission: 'shop:application:submit',
      requiresActiveShop: false,
      menuGroup: '01-onboarding',
    },
  },
  {
    path: '/shop/qualifications',
    name: 'shop.qualifications',
    component: () => import('./views/ShopQualifications.vue'),
    meta: {
      title: '资质管理',
      menuKey: 'shop.qualifications',
      roles: ['Seller'],
      permission: 'shop:qualification:upload',
      menuGroup: '01-onboarding',
    },
  },
  {
    path: '/shop/profile',
    name: 'shop.profile',
    component: () => import('./views/ShopProfile.vue'),
    meta: {
      title: '店铺资料',
      menuKey: 'shop.profile',
      roles: ['Seller'],
      permission: 'shop:profile:view',
      menuGroup: '01-onboarding',
    },
  },
  {
    path: '/shop/preview',
    name: 'shop.preview',
    component: () => import('./views/ShopPreview.vue'),
    meta: {
      title: '店铺预览',
      menuKey: 'shop.preview',
      roles: ['Seller'],
      permission: 'shop:profile:view',
      menuGroup: '01-onboarding',
    },
  },
]

export default routes
```

- [ ] **Step 2: 实现 index.ts**

创建 `web/seller/src/modules/01-onboarding/index.ts`：

```typescript
export { default } from './routes'
export { shopApi } from './api/shop.api'
```

- [ ] **Step 3: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors（views 已全部存在，懒加载可解析）

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/modules/01-onboarding/routes.ts web/seller/src/modules/01-onboarding/index.ts
git commit -m "feat(seller): add 01-onboarding module routes and entry"
```

---

## Task 9: 08-account Profile.vue — 个人资料（Descriptions 只读）

**Files:**
- Modify: `web/seller/src/modules/08-account/views/Profile.vue`

- [ ] **Step 1: 实现 Profile.vue（替换占位页）**

用以下完整内容覆盖 `web/seller/src/modules/08-account/views/Profile.vue`：

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Descriptions,
  DescriptionsItem,
  Tag,
  Avatar,
  Skeleton,
} from 'ant-design-vue'
import { UserOutlined } from '@ant-design/icons-vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { useShopStore } from '@/shared/shop'
import { StatusTag } from '@/shared/components'

/**
 * 个人资料页（只读）
 *
 * 路由 /account/profile（P0 已注册），权限 account:profile:view
 * 数据来自 authStore.user + shopStore，无新增 API。
 * onMounted 主动刷新 profile + 店铺信息以保证最新。
 */

const authStore = useAuthStore()
const shopStore = useShopStore()

const loading = computed(() => !authStore.user)
const user = computed(() => authStore.user)
const roles = computed(() => authStore.roles)
const permissions = computed(() => authStore.permissions)

onMounted(async () => {
  try {
    await authStore.fetchProfile()
    await shopStore.fetchMyShop()
  } catch {
    // fetchProfile 失败由路由守卫/拦截器统一处理，此处静默
  }
})
</script>

<template>
  <div class="account-profile-page">
    <Breadcrumb class="account-profile-breadcrumb">
      <BreadcrumbItem>个人账号</BreadcrumbItem>
      <BreadcrumbItem>账号信息</BreadcrumbItem>
    </Breadcrumb>

    <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />
    <div v-else class="account-profile-body">
      <!-- 基本信息 -->
      <Card class="account-profile-card" :bordered="true">
        <template #title>
          <span class="account-profile-title">基本信息</span>
        </template>
        <div class="account-profile-user">
          <Avatar :size="64" :src="user?.avatar || undefined">
            <UserOutlined v-if="!user?.avatar" />
          </Avatar>
          <Descriptions :column="2" bordered size="middle">
            <DescriptionsItem label="用户名">{{ user?.username || '—' }}</DescriptionsItem>
            <DescriptionsItem label="昵称">{{ user?.nickname || '—' }}</DescriptionsItem>
            <DescriptionsItem label="邮箱">{{ user?.email || '—' }}</DescriptionsItem>
            <DescriptionsItem label="手机号">{{ user?.phone || '—' }}</DescriptionsItem>
            <DescriptionsItem label="角色" :span="2">
              <Tag v-for="r in roles" :key="r" color="blue">{{ r }}</Tag>
              <span v-if="roles.length === 0">—</span>
            </DescriptionsItem>
          </Descriptions>
        </div>
      </Card>

      <!-- 店铺信息 -->
      <Card class="account-profile-card" :bordered="true">
        <template #title>
          <span class="account-profile-title">店铺信息</span>
        </template>
        <Descriptions :column="2" bordered size="middle">
          <DescriptionsItem label="店铺名称">
            {{ shopStore.shopName || user?.shopName || '—' }}
          </DescriptionsItem>
          <DescriptionsItem label="店铺状态">
            <StatusTag
              v-if="shopStore.shopStatus || user?.shopStatus"
              type="shop"
              :status="(shopStore.shopStatus || user?.shopStatus) as string"
            />
            <span v-else>—</span>
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <!-- 权限信息 -->
      <Card class="account-profile-card" :bordered="true">
        <template #title>
          <span class="account-profile-title">权限信息</span>
        </template>
        <div class="account-profile-perms">
          <Tag v-for="p in permissions" :key="p" color="geekblue">{{ p }}</Tag>
          <span v-if="permissions.length === 0" class="account-profile-empty">暂无权限</span>
        </div>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.account-profile-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-profile-breadcrumb {
  font-size: 14px;
}
.account-profile-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-profile-card {
  border-radius: 8px;
}
.account-profile-title {
  font-size: 15px;
  font-weight: 500;
}
.account-profile-user {
  display: flex;
  align-items: flex-start;
  gap: 24px;
}
.account-profile-user :deep(.ant-descriptions) {
  flex: 1;
}
.account-profile-perms {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.account-profile-empty {
  color: #8c8c8c;
  font-size: 13px;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/08-account/views/Profile.vue
git commit -m "feat(seller): implement account Profile page with descriptions"
```

---

## Task 10: 08-account Notifications.vue — 消息通知（BE-4 标记）

**Files:**
- Modify: `web/seller/src/modules/08-account/views/Notifications.vue`

> **BE-4 说明**：后端通知端点待确认，本批次不创建通知 mock handler（不在批次 1 范围）。
> 采用"仅 UI + BE-4 标记"策略：完整 UI（Tabs + List + 标记已读），不调用任何 API，
> 顶部 Alert 提示"后端接口未就绪（BE-4）"，列表展示空状态。

- [ ] **Step 1: 实现 Notifications.vue（替换占位页）**

用以下完整内容覆盖 `web/seller/src/modules/08-account/views/Notifications.vue`：

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Tabs,
  TabPane,
  List,
  ListItem,
  ListItemMeta,
  Button,
  Alert,
  Tag,
  Space,
  message,
} from 'ant-design-vue'
import { BellOutlined, CheckOutlined } from '@ant-design/icons-vue'
import { EmptyState } from '@/shared/components'

/**
 * 消息通知页
 *
 * 路由 /account/notifications（P0 已注册），权限 notification:list
 * 后端通知端点待确认（BE-4），本页采用"仅 UI + BE-4 标记"策略：
 * 完整 UI 但不调用 API，展示空列表与 BE-4 提示。
 */

interface NotificationItem {
  id: string
  title: string
  content: string
  read: boolean
  createdAt: string
}

const activeTab = ref<'all' | 'unread' | 'read'>('all')
const notifications = ref<NotificationItem[]>([])

const filtered = computed(() => {
  if (activeTab.value === 'unread') return notifications.value.filter((n) => !n.read)
  if (activeTab.value === 'read') return notifications.value.filter((n) => n.read)
  return notifications.value
})

const unreadCount = computed(() => notifications.value.filter((n) => !n.read).length)

function onMarkAllRead(): void {
  // BE-4：后端未就绪，仅提示
  message.warning('后端通知接口未就绪（BE-4），暂无法标记已读')
}
</script>

<template>
  <div class="account-notifications-page">
    <Breadcrumb class="account-notifications-breadcrumb">
      <BreadcrumbItem>个人账号</BreadcrumbItem>
      <BreadcrumbItem>消息通知</BreadcrumbItem>
    </Breadcrumb>

    <Alert
      type="warning"
      show-icon
      message="后端通知接口未就绪（BE-4）"
      description="通知端点待后端确认，当前展示空列表占位。后端就绪后将自动接入真实数据。"
      class="account-notifications-alert"
    />

    <Card class="account-notifications-card" :bordered="true">
      <template #title>
        <Space>
          <BellOutlined />
          <span class="account-notifications-title">消息通知</span>
          <Tag v-if="unreadCount > 0" color="red">{{ unreadCount }} 未读</Tag>
        </Space>
      </template>
      <template #extra>
        <Button :icon="h(CheckOutlined)" size="small" @click="onMarkAllRead">
          全部标记已读
        </Button>
      </template>

      <Tabs v-model:active-key="activeTab">
        <TabPane key="all" tab="全部" />
        <TabPane key="unread" tab="未读" />
        <TabPane key="read" tab="已读" />
      </Tabs>

      <EmptyState
        v-if="filtered.length === 0"
        description="暂无通知"
      />
      <List v-else :data-source="filtered" item-layout="horizontal">
        <template #renderItem="{ item }">
          <ListItem>
            <ListItemMeta>
              <template #title>
                <Space>
                  <span>{{ item.title }}</span>
                  <Tag v-if="!item.read" color="red">未读</Tag>
                </Space>
              </template>
              <template #description>{{ item.content }}</template>
            </ListItemMeta>
          </ListItem>
        </template>
      </List>
    </Card>
  </div>
</template>

<script lang="ts">
import { h } from 'vue'
</script>

<style scoped>
.account-notifications-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-notifications-breadcrumb {
  font-size: 14px;
}
.account-notifications-alert {
  border-radius: 8px;
}
.account-notifications-card {
  border-radius: 8px;
}
.account-notifications-title {
  font-size: 15px;
  font-weight: 500;
}
</style>
```

> **注**：上方 `<script lang="ts">` 块用于导入 `h`（模板中 `:icon="h(CheckOutlined)"` 需要）。
> 若实施时 eslint 报"混合 script 块"，可改为在 `<script setup>` 中直接 `import { h, ref, computed } from 'vue'` 并删除独立 script 块。推荐采用后者，等价且更简洁。

- [ ] **Step 2: 简化 script 块（推荐）**

为避免双 `<script>` 块，将 `<script setup>` 的第一行改为同时导入 `h`，并删除独立的 `<script lang="ts">` 块：

将：
```typescript
import { ref, computed } from 'vue'
```
改为：
```typescript
import { ref, computed, h } from 'vue'
```

并删除文件中独立的：
```vue
<script lang="ts">
import { h } from 'vue'
</script>
```

- [ ] **Step 3: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/modules/08-account/views/Notifications.vue
git commit -m "feat(seller): implement account Notifications page with BE-4 marker"
```

---

## Task 11: app/router.ts 注册 01-onboarding 路由

**Files:**
- Modify: `web/seller/src/app/router.ts`

> **背景**：路由守卫 `requiresActiveShop` 失败时跳转 `/shop/application`，但该路由 P0 未注册，
> 存在运行时断链风险。本任务补齐注册，消除 P0 遗留问题。
> 08-account 路由 P0 已注册，无需改动。

- [ ] **Step 1: 添加 onboarding 路由 import**

修改 `web/seller/src/app/router.ts`，在模块路由 import 区（`import account from '@/modules/08-account/routes'` 之前）追加：

```typescript
import onboarding from '@/modules/01-onboarding/routes'
```

修改后 import 区顺序为：

```typescript
// 模块路由
import onboarding from '@/modules/01-onboarding/routes'
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
import account from '@/modules/08-account/routes'
```

- [ ] **Step 2: 将 onboarding 路由注入 BasicLayout children**

在 `app/router.ts` 的 BasicLayout `children` 数组中，`...dashboard` 之前追加 `...onboarding`：

```typescript
    children: [
      { path: '', redirect: '/dashboard/overview' },
      ...onboarding,
      ...dashboard,
      ...product,
      ...order,
      ...afterSales,
      ...account,
    ],
```

- [ ] **Step 3: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/app/router.ts
git commit -m "feat(seller): register 01-onboarding routes to fix /shop/application broken link"
```

---

## Task 12: 全量验证 + 提交推送

**Files:**
- 无（仅验证与推送）

- [ ] **Step 1: Lint 全量检查**

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 2: TypeCheck 全量检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 3: 全量单元测试**

Run (cwd: `web/seller`): `pnpm test`
Expected: 全部通过（P0 既有用例 + 本批次新增 ImageUploader.spec.ts / StatusTag.spec.ts 新增用例 / shop.api.spec.ts 全部 PASS）

- [ ] **Step 4: 生产构建**

Run (cwd: `web/seller`): `pnpm build`
Expected: 构建成功（`vue-tsc --noEmit` 通过 + `vite build` 产出 `dist`）

- [ ] **Step 5: 推送到远程仓库**

```bash
git push origin dev
```
Expected: 推送成功，远程 `origin/dev` 包含本批次全部 12 个 commit。

- [ ] **Step 6: 人工冒烟（可选，mock 模式）**

启动 `VITE_USE_MOCK=true pnpm dev`，逐页验证：
- `/shop/application` 三步表单可走完，提交后跳转 `/shop/qualifications`
- `/shop/qualifications` 列出 3 条资质，上传新资质后列表新增（状态待审核）
- `/shop/profile` 回填资料，保存成功；模拟 409（手动改 localStorage version）后弹刷新确认框
- `/shop/preview` 只读展示店铺信息
- `/account/profile` 展示用户/店铺/权限信息
- `/account/notifications` 展示 BE-4 提示 + 空列表

---

## Self-Review（计划自检）

**1. Spec 覆盖检查（对照批次 1 范围 11 项）**

| 批次 1 范围项 | 覆盖 Task |
|---|---|
| 1. ImageUploader.vue + spec | Task 1 |
| 2. 01-onboarding: shop.dto + shop.api + spec + routes + index | Task 2（dto/api/spec）+ Task 8（routes/index） |
| 3. ShopApplication.vue（Steps 分步表单） | Task 4 |
| 4. ShopQualifications.vue（Upload 上传） | Task 5 |
| 5. ShopProfile.vue（含客服联系方式 + 乐观锁） | Task 6 |
| 6. ShopPreview.vue（只读展示） | Task 7 |
| 7. 08-account Profile.vue（Descriptions 只读） | Task 9 |
| 8. 08-account Notifications.vue（BE-4 标记） | Task 10 |
| 9. Mock handler handlers/shop.ts + seed 扩展 | Task 3 |
| 10. 路由更新 app/router.ts 注册 01-onboarding | Task 11 |
| 11. 全量验证 + 提交推送 | Task 12 |

无遗漏。补充项：`listQualifications` API + mock GET 端点（spec 细化，Task 2/3 已含）；`http` 别名导出（Task 2 Step 1）；StatusTag shop 映射扩展（Task 1 Step 6-9）。

**2. 占位符扫描**

- 全文未出现 `TODO`/`FIXME`/`...省略`/`Similar to Task` 等占位符。
- 所有 Vue SFC 均含完整 `<script setup lang="ts">` + `<template>` + `<style scoped>`。
- 所有 API/mock/组件代码为可直接编译运行的完整实现。

**3. 类型一致性检查**

- `ShopInfoDto` / `ShopApplicationDto` / `UpdateShopInfoDto` / `QualificationDto` / `UploadQualificationDto` 在 `shop.dto.ts`（Task 2）定义，被 `shop.api.ts`、`ShopApplication.vue`、`ShopProfile.vue`、`ShopPreview.vue`、`ShopQualifications.vue` 一致引用。
- `shopApi` 方法名（`submitApplication`/`getMyShop`/`updateMyShop`/`listQualifications`/`uploadQualification`）在 API、测试、各页面中一致。
- `ShopStatus` 含 `Pending`/`Active`/`Suspended`/`Closed`，与 StatusTag shop 映射（Task 1 扩展后）一致。
- 乐观锁 `version` 字段在 `ShopInfoDto.version`、`UpdateShopInfoDto.version`、`ShopProfile.vue` 的 `currentVersion`、mock PUT 校验中一致。
- mock `/shops/me` 双形态字段（`id`/`shopId`、`name`/`shopName`）同时满足 P0 `shop.store.ts` 与 P1 `shopApi`。

**4. 已知限制**

- `Notifications.vue` 为 BE-4 占位，不调用 API；后端通知端点就绪后需补充 `notification.api.ts` + mock handler + 真实数据绑定（归入后续批次）。
- `ShopQualifications.vue` 的"查看"按钮为 disabled 占位（无下载端点）；资质文件 `fileUrl` 在 mock 中为空字符串。

---

## 执行交接

计划已完成并保存至 `docs/superpowers/plans/2026-07-30-seller-admin-p1-batch1-onboarding-account.md`。两种执行方式可选：

**1. Subagent 驱动（推荐）** — 每个 Task 派发独立 subagent，任务间审查，迭代快速。

**2. 内联执行** — 在当前会话使用 executing-plans 批量执行，设检查点审查。

选择哪种方式？