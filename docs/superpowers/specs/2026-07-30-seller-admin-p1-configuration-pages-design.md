# 卖家管理后台 P1 阶段 — 配置类页面设计文档

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成 P1 阶段卖家端后台管理系统的配置类页面开发，包含店铺设置、物流管理、评价回复、账号补全、数据导出共 10 个页面。

**Architecture:** 延续 P0 的五段式模块结构（`api/ + types/ + views/ + routes.ts + index.ts`），新增 4 个模块目录（01-onboarding / 04-logistics / 07-review / 09-export），补全 1 个已有模块（08-account 的 Profile/Notifications 占位页）。shared 层新增 2 个通用组件（ImageUploader / TemplateRuleEditor）和 5 个 mock handler 文件。按优先级分 3 批交付，每批完成后执行全量验证（lint + typecheck + test + build）并提交推送。

**Tech Stack:** Vue 3.5 + TypeScript 5.7 + Vite 6 + Ant Design Vue 4.2 + Pinia 2.3 + Vue Router 4.5 + axios 1.7 + ECharts 5.5 + Vitest 2.1

---

## 1. 背景与范围

### 1.1 P0 阶段成果（已完成）

P0 阶段已完成 5 个核心业务模块：
- 02-dashboard（工作台 3 页）
- 03-product-management（商品管理 4 页）
- 05-order-fulfillment（订单履约 3 页）
- 06-after-sales（售后处理 2 页）
- 08-account（仅 Login 完整，Profile/Notifications 占位）

共 15 个页面、24 个 API 方法、15 条业务路由。shared 层含 9 个子目录、16 个通用组件。全量验证通过（lint 0 errors / typecheck 0 errors / 197 tests passed / build 成功）。

### 1.2 P1 阶段范围

P1 聚焦配置类页面，覆盖菜单中已定义但未实现的 4 个模块分组 + 1 个占位页补全：

| 模块 | 目录 | 页面数 | 后端状态 |
|------|------|--------|----------|
| 01-onboarding 店铺设置 | `modules/01-onboarding/` | 4 | ✅ 就绪 |
| 04-logistics 物流管理 | `modules/04-logistics/` | 2 | ✅ 就绪 |
| 07-review 评价回复 | `modules/07-review/` | 1 | ✅ 就绪 |
| 08-account 账号补全 | `modules/08-account/` | 2 | ✅ 就绪（Notifications 待确认） |
| 09-export 数据导出 | `modules/09-export/` | 1 | ❌ 缺失（BE-3 标记） |
| **合计** | | **10** | |

### 1.3 关键决策

1. **客服管理**：无独立客服模块。客服联系方式（电话/邮箱/在线客服账号）是店铺资料的字段，通过 `PUT /api/shops/me` 维护，集成在 ShopProfile.vue 页面中。
2. **系统设置**：系统配置/数据字典/特性开关/公告均为 system-admin 端功能，seller 端无此模块。seller 端仅有个人账号设置（Profile/Notifications）。
3. **数据导出**：后端 3 个端点全部未实现，采用"仅 UI + BE-3 标记"策略。API 客户端完整实现（方法签名 + axios 调用），mock 拦截返回 501 + BE-3 提示。
4. **评价路径**：使用新 BC 路径 `/api/seller/reviews`（含列表/详情/回复），不使用旧 BC 路径 `/api/reviews/{id}/reply`。
5. **Mock 策略**：新增 5 个 mock handler 文件覆盖全部 P1 模块，开发时 `VITE_USE_MOCK=true` 可独立运行。

---

## 2. 批次划分

按优先级分 3 批交付：

### 批次 1：店铺设置 + 账号补全（6 页）

**优先级最高的理由**：路由守卫 `requiresActiveShop` 失败时跳转 `/shop/application`，但该路由 P0 未注册，存在运行时断链风险（触发门禁会进 NotFound）。同时 Profile/Notifications 为 P0 占位页，需尽快补全。

| 页面 | 路由 | 权限 | API |
|------|------|------|-----|
| ShopApplication.vue | `/shop/application` | `shop:application:submit` | `POST /api/shops/application` |
| ShopQualifications.vue | `/shop/qualifications` | `shop:qualification:upload` | `POST /api/shops/me/qualifications` |
| ShopProfile.vue | `/shop/profile` | `shop:profile:view` | `GET/PUT /api/shops/me` |
| ShopPreview.vue | `/shop/preview` | `shop:profile:view` | `GET /api/shops/me` |
| Profile.vue | `/account/profile` | `account:profile:view` | `GET /users/me` |
| Notifications.vue | `/account/notifications` | `notification:list` | 待确认（缺失则 BE-4 标记） |

### 批次 2：物流管理 + 评价回复（3 页）

后端全部就绪，可直接对接。

| 页面 | 路由 | 权限 | API 端点数 |
|------|------|------|-----------|
| FreightTemplates.vue | `/logistics/freight-templates` | `freight-template:list` | 5 |
| LogisticsCompanies.vue | `/logistics/companies` | `logistics-company:list` | 1 |
| ReviewReply.vue | `/reviews` | `review:list` | 3 |

### 批次 3：数据导出（1 页）

仅 UI + BE-3 标记，依赖最少。

| 页面 | 路由 | 权限 | API 端点数 |
|------|------|------|-----------|
| SalesExport.vue | `/export/sales` | `export:sales` | 3（全部 BE-3） |

---

## 3. 模块详细设计

### 3.1 01-onboarding 店铺设置（4 页）

#### 3.1.1 模块结构

```
src/modules/01-onboarding/
├── api/shop.api.ts              # 店铺 API 客户端
├── types/shop.dto.ts            # DTO 类型定义
├── views/ShopApplication.vue    # 入驻申请
├── views/ShopQualifications.vue # 资质管理
├── views/ShopProfile.vue        # 店铺资料
├── views/ShopPreview.vue        # 店铺预览
├── routes.ts                    # 模块路由
├── index.ts                     # 模块出口
└── api/shop.api.spec.ts         # API 测试
```

#### 3.1.2 API 客户端（shop.api.ts）

```typescript
import { http } from '@/shared/http'
import { withIdempotency } from '@/shared/http/idempotency'
import type {
  ShopApplicationDto,
  ShopInfoDto,
  UpdateShopInfoDto,
  QualificationDto,
  UploadQualificationDto,
} from '../types/shop.dto'

export const shopApi = {
  /** 提交入驻申请 */
  submitApplication(body: ShopApplicationDto): Promise<ShopInfoDto> {
    return http.post<ShopInfoDto>('/api/shops/application', body, withIdempotency())
      .then(r => r.data)
  },

  /** 查询当前卖家店铺资料 */
  getMyShop(): Promise<ShopInfoDto> {
    return http.get<ShopInfoDto>('/api/shops/me').then(r => r.data)
  },

  /** 更新店铺基础信息（含客服联系方式，乐观锁） */
  updateMyShop(body: UpdateShopInfoDto): Promise<ShopInfoDto> {
    return http.put<ShopInfoDto>('/api/shops/me', body).then(r => r.data)
  },

  /** 上传店铺资质（multipart/form-data） */
  uploadQualification(body: UploadQualificationDto): Promise<QualificationDto> {
    const formData = new FormData()
    formData.append('file', body.file)
    formData.append('type', body.type)
    return http.post<QualificationDto>('/api/shops/me/qualifications', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then(r => r.data)
  },
}
```

#### 3.1.3 DTO 类型（shop.dto.ts）

```typescript
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

/** 资质文件 */
export interface QualificationDto {
  id: string
  type: QualificationType
  fileName: string
  fileUrl: string
  status: 'Pending' | 'Approved' | 'Rejected'
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

#### 3.1.4 ShopApplication.vue — 入驻申请

- **路由**：`/shop/application`，权限 `shop:application:submit`，`requiresActiveShop: false`
- **API**：`POST /api/shops/application`
- **设计**：Ant Design Steps 分步表单
  - 步骤 1：店铺基础信息（名称 2-32 字 / 主营类目 Select / 描述 ≤500 字 / 联系电话 / 联系邮箱）
  - 步骤 2：确认信息（只读展示步骤 1 数据）
  - 步骤 3：提交（IdempotencyButton + 幂等键），成功后跳转 `/shop/qualifications`
- **校验**：步骤 1 必填项校验（名称/类目/电话），通过后激活下一步
- **错误处理**：409 冲突 → Modal.confirm 刷新；其他 → message.error

#### 3.1.5 ShopQualifications.vue — 资质管理

- **路由**：`/shop/qualifications`，权限 `shop:qualification:upload`
- **API**：`POST /api/shops/me/qualifications`（multipart/form-data）
- **设计**：
  - 资质列表表格（类型 / 文件名 / 状态 Tag / 提交时间 / 审核时间 / 操作）
  - 上传按钮（Upload 组件，accept `.jpg,.png,.pdf`，maxSize 5MB，beforeUpload 校验）
  - 资质类型选择（Select：营业执照 / 身份证 / 银行账户信息 / 其他）
  - 审核状态：Pending（黄色）/ Approved（绿色）/ Rejected（红色 + 原因 Tooltip）
- **新增 shared 组件**：`ImageUploader.vue`（封装 Upload + FileReader + 预览 + 大小校验）

#### 3.1.6 ShopProfile.vue — 店铺资料

- **路由**：`/shop/profile`，权限 `shop:profile:view`
- **API**：`GET /api/shops/me` + `PUT /api/shops/me`（含 `version` 乐观锁）
- **设计**：表单分区
  - 基础信息区：名称（2-32 字）/ Logo 上传（ImageUploader，JPG/PNG/WebP ≤5MB）/ 描述（≤1000 字）
  - 客服联系方式区：电话（必填）/ 邮箱（选填）/ 在线客服账号（选填）
  - 底部保存按钮（IdempotencyButton）
- **乐观锁**：PUT 携带 `version`，409 冲突 → Modal.confirm「资源已被他人修改，是否刷新后重试？」
- **客服管理即此页面的字段组**

#### 3.1.7 ShopPreview.vue — 店铺前台预览

- **路由**：`/shop/preview`，权限 `shop:profile:view`
- **API**：`GET /api/shops/me`（只读）
- **设计**：以卡片形式模拟买家视角
  - 店铺头部：Logo + 名称 + 状态 Tag
  - 店铺描述区
  - 客服联系方式区（电话 / 邮箱 / 在线客服账号）
  - 非iframe，纯组件渲染

#### 3.1.8 路由（routes.ts）

```typescript
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/shop/application',
    name: 'shop.application',
    component: () => import('../views/ShopApplication.vue'),
    meta: { permission: 'shop:application:submit', requiresActiveShop: false, title: '入驻申请' },
  },
  {
    path: '/shop/qualifications',
    name: 'shop.qualifications',
    component: () => import('../views/ShopQualifications.vue'),
    meta: { permission: 'shop:qualification:upload', title: '资质管理' },
  },
  {
    path: '/shop/profile',
    name: 'shop.profile',
    component: () => import('../views/ShopProfile.vue'),
    meta: { permission: 'shop:profile:view', title: '店铺资料' },
  },
  {
    path: '/shop/preview',
    name: 'shop.preview',
    component: () => import('../views/ShopPreview.vue'),
    meta: { permission: 'shop:profile:view', title: '店铺预览' },
  },
]

export default routes
```

---

### 3.2 04-logistics 物流管理（2 页）

#### 3.2.1 模块结构

```
src/modules/04-logistics/
├── api/freight-template.api.ts          # 运费模板 API
├── api/logistics-company.api.ts         # 物流公司 API
├── types/freight-template.dto.ts        # 运费模板 DTO
├── types/logistics-company.dto.ts       # 物流公司 DTO
├── views/FreightTemplates.vue           # 运费模板
├── views/LogisticsCompanies.vue         # 物流公司（只读）
├── routes.ts
├── index.ts
├── api/freight-template.api.spec.ts
└── api/logistics-company.api.spec.ts
```

#### 3.2.2 API 客户端

**freight-template.api.ts**（5 端点）：

```typescript
import { http } from '@/shared/http'
import { withIdempotency } from '@/shared/http/idempotency'
import type {
  FreightTemplateDto,
  CreateFreightTemplateDto,
  UpdateFreightRulesDto,
} from '../types/freight-template.dto'

export const freightTemplateApi = {
  /** 查询当前卖家运费模板列表 */
  listMine(): Promise<FreightTemplateDto[]> {
    return http.get<FreightTemplateDto[]>('/api/seller/freight-templates/mine').then(r => r.data)
  },

  /** 创建运费模板 */
  create(body: CreateFreightTemplateDto): Promise<FreightTemplateDto> {
    return http.post<FreightTemplateDto>('/api/seller/freight-templates', body, withIdempotency())
      .then(r => r.data)
  },

  /** 更新区域规则（整体替换，带 version 乐观锁） */
  updateRules(id: string, body: UpdateFreightRulesDto): Promise<FreightTemplateDto> {
    return http.put<FreightTemplateDto>(`/api/seller/freight-templates/${id}/rules`, body)
      .then(r => r.data)
  },

  /** 启用模板 */
  enable(id: string): Promise<void> {
    return http.post<void>(`/api/seller/freight-templates/${id}/enable`, {}, withIdempotency())
      .then(r => r.data)
  },

  /** 停用模板 */
  disable(id: string): Promise<void> {
    return http.post<void>(`/api/seller/freight-templates/${id}/disable`, {}, withIdempotency())
      .then(r => r.data)
  },
}
```

**logistics-company.api.ts**（1 端点，卖家只读）：

```typescript
import { http } from '@/shared/http'
import type { LogisticsCompanyDto } from '../types/logistics-company.dto'

export const logisticsCompanyApi = {
  /** 查询启用态物流公司（卖家只读） */
  listEnabled(): Promise<LogisticsCompanyDto[]> {
    return http.get<LogisticsCompanyDto[]>('/api/seller/logistics-companies').then(r => r.data)
  },
}
```

#### 3.2.3 DTO 类型

**freight-template.dto.ts**：

```typescript
/** 计费类型 */
export type PricingType = 'ByWeight' | 'ByPiece' | 'Fixed'

/** 区域规则 */
export interface RegionRuleDto {
  id: string
  regionCode: string
  regionName: string
  firstUnit: number
  firstPrice: number
  nextUnit: number
  nextPrice: number
}

/** 运费模板 */
export interface FreightTemplateDto {
  id: string
  name: string
  pricingType: PricingType
  fixedFee?: number
  freeShippingThreshold?: number
  regionRules: RegionRuleDto[]
  isEnabled: boolean
  version: number
  createdAt: string
  updatedAt: string
}

/** 创建运费模板 */
export interface CreateFreightTemplateDto {
  name: string
  pricingType: PricingType
  fixedFee?: number
  freeShippingThreshold?: number
}

/** 更新区域规则 */
export interface UpdateFreightRulesDto {
  regionRules: RegionRuleDto[]
  version: number
}
```

**logistics-company.dto.ts**：

```typescript
/** 物流公司（卖家只读视图） */
export interface LogisticsCompanyDto {
  id: string
  name: string
  code: string
  servicePhone?: string
  website?: string
  supportsTracking: boolean
  sortOrder: number
}
```

#### 3.2.4 FreightTemplates.vue — 运费模板

- **路由**：`/logistics/freight-templates`，权限 `freight-template:list`
- **设计**：
  - 模板列表表格（名称 / 计费类型 Tag / 满额包邮 / 状态 Switch / 操作）
  - 新建模板按钮 → 弹窗（名称 + 计费类型 Select + 固定运费 InputNumber + 满额包邮 InputNumber）
  - 编辑规则按钮 → 抽屉（TemplateRuleEditor 地区规则表格编辑器）
  - 启停 Switch（调用 enable/disable）
- **计费类型行为**：
  - `Fixed`（固定运费）：显示 fixedFee 输入，隐藏地区规则表
  - `ByWeight`（按重量）：显示地区规则表（首重 kg / 首价 / 续重 kg / 续价）
  - `ByPiece`（按件数）：显示地区规则表（首件数 / 首价 / 续件数 / 续价）
- **乐观锁**：updateRules 携带 version，409 冲突处理

#### 3.2.5 LogisticsCompanies.vue — 物流公司（只读）

- **路由**：`/logistics/companies`，权限 `logistics-company:list`
- **设计**：
  - 只读表格（名称 / 编码 / 客服电话 / 是否支持轨迹查询 Tag / 官网链接）
  - 10 分钟前端缓存：首次加载存 `localStorage` + 时间戳，10 分钟内直接读缓存
  - 复制编码功能（点击编码复制到剪贴板，用于发货时填入）
  - Skeleton 加载态 + EmptyState 空状态

#### 3.2.6 新增 shared 组件：TemplateRuleEditor.vue

- **职责**：运费模板地区规则表格的增删改编辑器
- **Props**：`modelValue: RegionRuleDto[]`、`pricingType: PricingType`、`disabled: boolean`
- **Emits**：`update:modelValue`
- **设计**：Table 组件 + 可编辑行（地区编码 Input / 首单位 InputNumber / 首价 InputNumber / 续单位 InputNumber / 续价 InputNumber）+ 添加行/删除行按钮
- **校验**：所有字段必填，价格 ≥ 0

---

### 3.3 07-review 评价回复（1 页）

#### 3.3.1 模块结构

```
src/modules/07-review/
├── api/review.api.ts            # 评价 API
├── types/review.dto.ts          # 评价 DTO
├── views/ReviewReply.vue        # 评价列表 + 回复抽屉
├── routes.ts
├── index.ts
└── api/review.api.spec.ts
```

#### 3.3.2 API 客户端（review.api.ts）

```typescript
import { http } from '@/shared/http'
import { withIdempotency } from '@/shared/http/idempotency'
import type {
  ReviewDto,
  ReviewListResultDto,
  ReviewQueryParams,
  SellerReplyDto,
} from '../types/review.dto'

export const reviewApi = {
  /** 查询卖家评价列表 */
  list(params: ReviewQueryParams): Promise<ReviewListResultDto> {
    return http.get<ReviewListResultDto>('/api/seller/reviews', { params }).then(r => r.data)
  },

  /** 查询评价详情 */
  get(id: string): Promise<ReviewDto> {
    return http.get<ReviewDto>(`/api/seller/reviews/${id}`).then(r => r.data)
  },

  /** 回复评价（覆盖式编辑，1-500 字） */
  reply(id: string, body: SellerReplyDto): Promise<ReviewDto> {
    return http.post<ReviewDto>(`/api/seller/reviews/${id}/reply`, body, withIdempotency())
      .then(r => r.data)
  },
}
```

#### 3.3.3 DTO 类型（review.dto.ts）

```typescript
/** 评价状态（卖家仅可见 Approved） */
export type ReviewStatus = 'Approved' | 'Hidden'

/** 评价查询参数 */
export interface ReviewQueryParams {
  rating?: number
  replied?: boolean
  productName?: string
  startDate?: string
  endDate?: string
  page: number
  pageSize: number
}

/** 评价列表结果 */
export interface ReviewListResultDto {
  items: ReviewDto[]
  total: number
  page: number
  pageSize: number
}

/** 评价详情 */
export interface ReviewDto {
  reviewId: string
  orderId: string
  orderLineId: string
  spuId: string
  skuId: string
  userId: string
  userMaskedName: string
  rating: number
  content: string
  images: string[]
  status: ReviewStatus
  sellerReplyContent?: string
  sellerReplyBy?: string
  sellerReplyAt?: string
  submittedAt: string
  auditedAt?: string
  productName?: string
  productImage?: string
  skuSpec?: string
}

/** 卖家回复 DTO */
export interface SellerReplyDto {
  content: string
}
```

#### 3.3.4 ReviewReply.vue — 评价列表 + 回复抽屉

- **路由**：`/reviews`，权限 `review:list`
- **API**（新 BC 路径）：`GET /api/seller/reviews` + `GET /api/seller/reviews/{id}` + `POST /api/seller/reviews/{id}/reply`
- **布局**（严格遵循设计稿 `07-review/review-reply.html`）：
  - **顶部统计区**：好评率（Statistic 组件）+ 待回复 N 条（Tag）
  - **筛选栏**：评分 Select（全部/5-1 星）/ 回复状态 Select（全部/待回复/已回复）/ 商品名称 Search（300ms 防抖）/ 时间范围 DateTimeRangePicker
  - **评价卡片列表**（非表格，Card 布局，每张卡片含 4 区）：
    - C1 头部：买家头像（Avatar 占位）+ 脱敏名称 + Rate 只读 + 时间 + StatusTag(type="review")
    - C2 商品快照：商品主图 48×48 + 标题 + SKU 规格，点击跳 `/products/:spuId/edit`
    - C3 正文：文字（超 3 行折叠，展开/收起）+ 凭证图片组（ImagePreviewGroup 80×80 缩略图）
    - C4 回复区：已回复 → 回复内容 + 时间 + 「编辑回复」按钮；未回复 → 「回复」主按钮
  - **右侧回复抽屉**（Drawer 480px）：评价摘要（只读）+ textarea（rows=4，1-500 字，实时字数统计）+ IdempotencyButton 提交
- **交互流程**：
  1. 进入页面 → `GET /api/seller/reviews?page=1&pageSize=20` → 渲染统计 + 卡片列表
  2. 筛选变更 → 重新查询（商品名 300ms 防抖）
  3. 点「回复」→ 抽屉滑出 + 表单空 → 输入 → 提交 → `message.success('回复成功')` → 关闭抽屉 + 刷新
  4. 点「编辑回复」→ 抽屉滑出 + 回填原 `sellerReplyContent` → 修改 → 提交（覆盖式）→ `message.success('回复已更新')`
  5. 编辑时有改动，关闭抽屉前 `Modal.confirm`「确认放弃当前编辑内容？」
- **复用组件**：StatusTag(type="review") / IdempotencyButton / EmptyState / DateTimeRangePicker
- **加载态**：Skeleton 模拟 3 张卡片；筛选时 Spin
- **空状态**：EmptyState「暂无评价」

---

### 3.4 08-account 账号补全（2 页）

#### 3.4.1 模块结构（已有，补全 views）

```
src/modules/08-account/
├── api/auth.api.ts              # 已有（getProfile / login / logout）
├── types/auth.dto.ts            # 已有
├── views/Login.vue              # P0 已完成
├── views/Profile.vue            # P0 占位 → P1 完整实现
├── views/Notifications.vue     # P0 占位 → P1 完整实现
├── routes.ts                    # 已有
└── index.ts                     # 已有
```

#### 3.4.2 Profile.vue — 个人资料

- **路由**：`/account/profile`（P0 已注册），权限 `account:profile:view`
- **API**：`GET /users/me`（P0 已有 `authApi.getProfile()`）
- **设计**：只读 Descriptions 组件
  - 基本信息：用户名 / 角色 / 店铺名称 / 店铺状态 StatusTag
  - 安全信息：最后登录时间 / 登录 IP
  - 权限信息：权限列表（Tag 列表，来自 `authStore.permissions`）
- **无新增 API**，数据来自 `authStore.user` + `shopStore`

#### 3.4.3 Notifications.vue — 消息通知

- **路由**：`/account/notifications`（P0 已注册），权限 `notification:list`
- **API**：待确认后端通知端点
  - 若后端有通知端点（如 `GET /api/seller/notifications`）→ 完整实现列表 + 已读/未读
  - 若后端缺失 → 仅 UI + BE-4 标记（同导出策略：表单完整、列表空状态、"后端接口未就绪（BE-4）"提示）
- **设计**：消息列表（List 组件）+ 已读/未读 Tab 筛选 + 标记已读按钮

---

### 3.5 09-export 数据导出（1 页，仅 UI + BE-3）

#### 3.5.1 模块结构

```
src/modules/09-export/
├── api/export.api.ts             # 导出 API（完整实现，BE-3 标记）
├── types/export.dto.ts           # 导出 DTO
├── views/SalesExport.vue         # 销售报表导出
├── routes.ts
├── index.ts
└── api/export.api.spec.ts
```

#### 3.5.2 API 客户端（export.api.ts）

```typescript
import { http } from '@/shared/http'
import { withIdempotency } from '@/shared/http/idempotency'
import type {
  CreateExportTaskDto,
  ExportTaskDto,
  ExportTaskQueryParams,
} from '../types/export.dto'

export const exportApi = {
  /** 创建导出任务（BE-3 待后端实现） */
  createTask(body: CreateExportTaskDto): Promise<ExportTaskDto> {
    return http.post<ExportTaskDto>('/api/seller/export/sales', body, withIdempotency())
      .then(r => r.data)
  },

  /** 查询导出任务列表（BE-3 待后端实现） */
  listTasks(params: ExportTaskQueryParams): Promise<{ items: ExportTaskDto[]; total: number }> {
    return http.get('/api/seller/export/tasks', { params }).then(r => r.data)
  },

  /** 下载导出文件（BE-3 待后端实现） */
  getDownloadUrl(taskId: string): string {
    return `/api/seller/export/tasks/${taskId}/download`
  },
}
```

#### 3.5.3 DTO 类型（export.dto.ts）

```typescript
/** 报表类型 */
export type ReportType = 'SalesSummary' | 'OrderDetail' | 'ProductSales'

/** 导出格式 */
export type ExportFormat = 'Excel' | 'CSV'

/** 任务状态 */
export type ExportTaskStatus = 'Processing' | 'Completed' | 'Failed'

/** 创建导出任务 */
export interface CreateExportTaskDto {
  reportType: ReportType
  startDate: string
  endDate: string
  format: ExportFormat
}

/** 导出任务 */
export interface ExportTaskDto {
  id: string
  reportType: ReportType
  startDate: string
  endDate: string
  format: ExportFormat
  status: ExportTaskStatus
  recordCount?: number
  fileSize?: number
  downloadUrl?: string
  errorMessage?: string
  createdAt: string
  completedAt?: string
}

/** 任务查询参数 */
export interface ExportTaskQueryParams {
  page: number
  pageSize: number
  status?: ExportTaskStatus
}
```

#### 3.5.4 SalesExport.vue — 销售报表导出

- **路由**：`/export/sales`，权限 `export:sales`
- **API**：3 端点全部 BE-3 标记（mock 拦截返回 501）
- **设计**（遵循设计稿 `09-export/sales-export.html`）：
  - 左右两栏布局（Row + Col，左 8/24 右 16/24）
  - **左栏 — 新建导出任务**：
    - 报表类型 Select（销售汇总 / 订单明细 / 商品销量）
    - 时间范围 RangePicker（≤90 天校验）
    - 格式 Radio（Excel / CSV）
    - 提交按钮（IdempotencyButton）→ 调用 `exportApi.createTask` → mock 返回 501 → message.warning('后端接口未就绪（BE-3）')
  - **右栏 — 历史任务列表**：
    - 表格（类型 / 时间范围 / 格式 / 状态 Tag / 记录数 / 创建时间 / 操作）
    - 状态：Processing（蓝色 Spin 图标）/ Completed（绿色 + 下载按钮）/ Failed（红色 + 重试按钮）
    - 下载按钮 → `exportApi.getDownloadUrl(id)` → mock 501 → message.warning
    - 空状态：EmptyState「暂无导出任务」
  - **轮询**：有 Processing 状态任务时每 3 秒刷新列表（BE-3 后就绪后生效，当前 mock 返回空列表）

---

## 4. Mock 策略

### 4.1 新增 Mock Handler 文件

在 `src/shared/http/mock/handlers/` 下新增 5 个文件：

| 文件 | 拦截前缀 | 覆盖端点 | 种子数据 |
|------|----------|----------|----------|
| `shop.ts` | `/shops` | application / me / qualifications | 1 个店铺 + 3 个资质 |
| `freight.ts` | `/seller/freight-templates` | listMine / create / updateRules / enable / disable | 2 个模板（固定运费 + 按重量） |
| `logistics.ts` | `/seller/logistics-companies` | listEnabled | 5 个物流公司 |
| `review.ts` | `/seller/reviews` | list / get / reply | 10 条评价（含已回复/未回复） |
| `export.ts` | `/seller/export` | createTask / listTasks / download | 返回 501 + BE-3 标记 |

### 4.2 种子数据扩展

`data/seed.ts` 扩展以下种子数据：

```typescript
// 店铺种子
export const seedShop: ShopInfoDto = {
  id: 'shop-001',
  name: '示例服饰旗舰店',
  logo: '',
  description: '专注高品质男女装，20年匠心工艺',
  status: 'Active',
  mainCategory: '服装',
  customerService: { phone: '13800138000', email: 'service@example.com', onlineAccount: 'wx_shop001' },
  version: 1,
  createdAt: '2026-01-15T10:00:00Z',
  updatedAt: '2026-07-01T12:00:00Z',
}

// 资质种子
export const seedQualifications: QualificationDto[] = [
  { id: 'qual-001', type: 'BusinessLicense', fileName: '营业执照.pdf', fileUrl: '', status: 'Approved', submittedAt: '2026-01-15T10:00:00Z', auditedAt: '2026-01-16T09:00:00Z' },
  { id: 'qual-002', type: 'IdCard', fileName: '身份证.jpg', fileUrl: '', status: 'Approved', submittedAt: '2026-01-15T10:00:00Z', auditedAt: '2026-01-16T09:00:00Z' },
  { id: 'qual-003', type: 'BankAccount', fileName: '银行账户信息.pdf', fileUrl: '', status: 'Pending', submittedAt: '2026-07-20T14:00:00Z' },
]

// 运费模板种子
export const seedFreightTemplates: FreightTemplateDto[] = [
  { id: 'ft-001', name: '全国统一运费', pricingType: 'Fixed', fixedFee: 10, regionRules: [], isEnabled: true, version: 1, createdAt: '2026-02-01T00:00:00Z', updatedAt: '2026-02-01T00:00:00Z' },
  { id: 'ft-002', name: '按重量计费', pricingType: 'ByWeight', freeShippingThreshold: 99, regionRules: [{ id: 'r-001', regionCode: 'CN', regionName: '全国', firstUnit: 1, firstPrice: 8, nextUnit: 1, nextPrice: 2 }], isEnabled: true, version: 1, createdAt: '2026-02-01T00:00:00Z', updatedAt: '2026-02-01T00:00:00Z' },
]

// 物流公司种子
export const seedLogisticsCompanies: LogisticsCompanyDto[] = [
  { id: 'lc-001', name: '顺丰速运', code: 'SF', servicePhone: '95338', website: 'https://www.sf-express.com', supportsTracking: true, sortOrder: 1 },
  { id: 'lc-002', name: '中通快递', code: 'ZTO', servicePhone: '95311', website: 'https://www.zto.com', supportsTracking: true, sortOrder: 2 },
  { id: 'lc-003', name: '圆通速递', code: 'YTO', servicePhone: '95554', website: 'https://www.yto.net.cn', supportsTracking: true, sortOrder: 3 },
  { id: 'lc-004', name: '韵达快递', code: 'YUNDA', servicePhone: '95546', website: 'https://www.yundaex.com', supportsTracking: true, sortOrder: 4 },
  { id: 'lc-005', name: 'EMS', code: 'EMS', servicePhone: '11183', website: 'https://www.ems.com.cn', supportsTracking: true, sortOrder: 5 },
]

// 评价种子（10 条，5 条已回复 + 5 条未回复，评分 1-5 星分布）
export const seedReviews: ReviewDto[] = [
  { reviewId: 'rev-001', orderId: 'ord-101', orderLineId: 'ol-101', spuId: 'spu-001', skuId: 'sku-001', userId: 'u-001', userMaskedName: '13****5678', rating: 5, content: '质量非常好，面料舒适，做工精细，物流也很快！', images: [], status: 'Approved', sellerReplyContent: '感谢您的支持，欢迎再次光临！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-15T10:00:00Z', submittedAt: '2026-07-14T15:30:00Z', auditedAt: '2026-07-14T16:00:00Z', productName: '纯棉圆领T恤 白色 L', productImage: '', skuSpec: '白色 / L' },
  { reviewId: 'rev-002', orderId: 'ord-102', orderLineId: 'ol-102', spuId: 'spu-002', skuId: 'sku-002', userId: 'u-002', userMaskedName: '18****1234', rating: 4, content: '整体不错，就是尺码偏小，建议买大一码。', images: ['img-001.jpg'], status: 'Approved', sellerReplyContent: '感谢反馈，我们会优化尺码表。', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-16T09:00:00Z', submittedAt: '2026-07-15T20:00:00Z', auditedAt: '2026-07-15T21:00:00Z', productName: '修身衬衫 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
  { reviewId: 'rev-003', orderId: 'ord-103', orderLineId: 'ol-103', spuId: 'spu-001', skuId: 'sku-003', userId: 'u-003', userMaskedName: '15****8888', rating: 5, content: '回购第三次了，一如既往的好！', images: [], status: 'Approved', sellerReplyContent: '感恩老客户，已为您发放优惠券！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-17T14:00:00Z', submittedAt: '2026-07-16T11:00:00Z', auditedAt: '2026-07-16T12:00:00Z', productName: '纯棉圆领T恤 黑色 XL', productImage: '', skuSpec: '黑色 / XL' },
  { reviewId: 'rev-004', orderId: 'ord-104', orderLineId: 'ol-104', spuId: 'spu-003', skuId: 'sku-004', userId: 'u-004', userMaskedName: '19****6666', rating: 3, content: '一般般，性价比还行，但颜色和图片有点色差。', images: ['img-002.jpg', 'img-003.jpg'], status: 'Approved', sellerReplyContent: '抱歉给您带来不便，我们会改进拍摄。', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-18T10:00:00Z', submittedAt: '2026-07-17T16:00:00Z', auditedAt: '2026-07-17T17:00:00Z', productName: '雪纺连衣裙 粉色 S', productImage: '', skuSpec: '粉色 / S' },
  { reviewId: 'rev-005', orderId: 'ord-105', orderLineId: 'ol-105', spuId: 'spu-002', skuId: 'sku-005', userId: 'u-005', userMaskedName: '17****3333', rating: 4, content: '衬衫质量不错，包装也很好。', images: [], status: 'Approved', sellerReplyContent: '谢谢好评！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-19T08:00:00Z', submittedAt: '2026-07-18T09:00:00Z', auditedAt: '2026-07-18T10:00:00Z', productName: '修身衬衫 白色 L', productImage: '', skuSpec: '白色 / L' },
  { reviewId: 'rev-006', orderId: 'ord-106', orderLineId: 'ol-106', spuId: 'spu-001', skuId: 'sku-001', userId: 'u-006', userMaskedName: '13****9999', rating: 2, content: '面料有点硬，洗了一次就起球了，不太满意。', images: ['img-004.jpg'], status: 'Approved', submittedAt: '2026-07-20T13:00:00Z', auditedAt: '2026-07-20T14:00:00Z', productName: '纯棉圆领T恤 白色 L', productImage: '', skuSpec: '白色 / L' },
  { reviewId: 'rev-007', orderId: 'ord-107', orderLineId: 'ol-107', spuId: 'spu-003', skuId: 'sku-006', userId: 'u-007', userMaskedName: '18****7777', rating: 5, content: '裙子很漂亮，版型好，朋友都说好看！', images: [], status: 'Approved', submittedAt: '2026-07-21T10:00:00Z', auditedAt: '2026-07-21T11:00:00Z', productName: '雪纺连衣裙 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
  { reviewId: 'rev-008', orderId: 'ord-108', orderLineId: 'ol-108', spuId: 'spu-002', skuId: 'sku-002', userId: 'u-008', userMaskedName: '15****2222', rating: 1, content: '扣子掉了两个，质量太差了，要求退款。', images: ['img-005.jpg', 'img-006.jpg', 'img-007.jpg'], status: 'Approved', submittedAt: '2026-07-22T17:00:00Z', auditedAt: '2026-07-22T18:00:00Z', productName: '修身衬衫 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
  { reviewId: 'rev-009', orderId: 'ord-109', orderLineId: 'ol-109', spuId: 'spu-001', skuId: 'sku-003', userId: 'u-009', userMaskedName: '19****4444', rating: 4, content: '不错，穿着很舒服，就是快递有点慢。', images: [], status: 'Approved', submittedAt: '2026-07-23T08:00:00Z', auditedAt: '2026-07-23T09:00:00Z', productName: '纯棉圆领T恤 黑色 XL', productImage: '', skuSpec: '黑色 / XL' },
  { reviewId: 'rev-010', orderId: 'ord-110', orderLineId: 'ol-110', spuId: 'spu-003', skuId: 'sku-004', userId: 'u-010', userMaskedName: '17****0000', rating: 3, content: '裙子颜色不错但偏短，身高170穿刚好。', images: [], status: 'Approved', submittedAt: '2026-07-24T12:00:00Z', auditedAt: '2026-07-24T13:00:00Z', productName: '雪纺连衣裙 粉色 S', productImage: '', skuSpec: '粉色 / S' },
]
```

### 4.3 Mock Handler 实现要点

- **分页**：list 端点支持 `page` / `pageSize` 参数，返回 `{ items, total, page, pageSize }`
- **筛选**：支持各模块的筛选参数（如评价的 rating/replied/productName/startDate/endDate）
- **写操作**：create/update/enable/disable 修改种子数据并返回更新后的对象
- **乐观锁**：update 端点校验 version，不匹配返回 409
- **幂等键**：POST 操作检查 `X-Idempotency-Key` 头，重复请求返回缓存结果
- **导出端点**：返回 HTTP 501 + `{ message: 'BE-3 待后端实现', code: 'BE-3' }`
- **持久化**：种子数据变更存入 `localStorage`（与 P0 seed.ts 一致）

---

## 5. 新增 Shared 组件

### 5.1 ImageUploader.vue

- **职责**：封装 Upload + FileReader + 预览 + 大小校验，用于店铺 Logo 和资质文件上传
- **Props**：
  ```typescript
  interface ImageUploaderProps {
    modelValue: string                    // 当前 URL（data URL 或远程 URL）
    accept: string                        // 接受的文件类型，如 '.jpg,.png,.webp'
    maxSize: number                       // 最大字节数
    label?: string                        // 上传区域提示文字
    disabled?: boolean
  }
  ```
- **Emits**：`update:modelValue` / `error`
- **设计**：Upload 组件（listType picture-card）+ customRequest（FileReader 转 data URL）+ beforeUpload（大小/类型校验）+ 预览
- **位置**：`src/shared/components/ImageUploader.vue`
- **测试**：`src/shared/components/ImageUploader.spec.ts`

### 5.2 TemplateRuleEditor.vue

- **职责**：运费模板地区规则表格的增删改编辑器
- **Props**：
  ```typescript
  interface TemplateRuleEditorProps {
    modelValue: RegionRuleDto[]
    pricingType: PricingType              // 影响列标题（重量 kg / 件数 个）
    disabled?: boolean
  }
  ```
- **Emits**：`update:modelValue`
- **设计**：Table 可编辑行 + 添加行按钮 + 删除行按钮
- **位置**：`src/shared/components/TemplateRuleEditor.vue`
- **测试**：`src/shared/components/TemplateRuleEditor.spec.ts`

---

## 6. 路由更新

### 6.1 app/router.ts 新增路由

在 `app/router.ts` 的 BasicLayout children 中新增以下路由（按模块编号排列）：

```typescript
// 01-onboarding 店铺设置
{ path: '/shop/application', name: 'shop.application', component: () => import('@/modules/01-onboarding/views/ShopApplication.vue'), meta: { permission: 'shop:application:submit', requiresActiveShop: false, title: '入驻申请' } },
{ path: '/shop/qualifications', name: 'shop.qualifications', component: () => import('@/modules/01-onboarding/views/ShopQualifications.vue'), meta: { permission: 'shop:qualification:upload', title: '资质管理' } },
{ path: '/shop/profile', name: 'shop.profile', component: () => import('@/modules/01-onboarding/views/ShopProfile.vue'), meta: { permission: 'shop:profile:view', title: '店铺资料' } },
{ path: '/shop/preview', name: 'shop.preview', component: () => import('@/modules/01-onboarding/views/ShopPreview.vue'), meta: { permission: 'shop:profile:view', title: '店铺预览' } },

// 04-logistics 物流管理
{ path: '/logistics/freight-templates', name: 'logistics.freight-templates', component: () => import('@/modules/04-logistics/views/FreightTemplates.vue'), meta: { permission: 'freight-template:list', title: '运费模板' } },
{ path: '/logistics/companies', name: 'logistics.companies', component: () => import('@/modules/04-logistics/views/LogisticsCompanies.vue'), meta: { permission: 'logistics-company:list', title: '物流公司' } },

// 07-review 评价回复
{ path: '/reviews', name: 'review.reply', component: () => import('@/modules/07-review/views/ReviewReply.vue'), meta: { permission: 'review:list', title: '评价回复' } },

// 09-export 数据导出
{ path: '/export/sales', name: 'export.sales', component: () => import('@/modules/09-export/views/SalesExport.vue'), meta: { permission: 'export:sales', title: '销售报表' } },
```

### 6.2 菜单更新

`SiderMenu.vue` 中菜单已在 P0 定义，路由注册后自动生效，无需修改菜单结构。

---

## 7. 后端端点状态汇总

| 模块 | 端点 | 方法 | 路径 | 后端状态 |
|------|------|------|------|----------|
| 01-onboarding | 提交入驻申请 | POST | `/api/shops/application` | ✅ |
| 01-onboarding | 查询店铺资料 | GET | `/api/shops/me` | ✅ |
| 01-onboarding | 更新店铺资料 | PUT | `/api/shops/me` | ✅ |
| 01-onboarding | 上传资质 | POST | `/api/shops/me/qualifications` | ✅ |
| 04-logistics | 查询模板列表 | GET | `/api/seller/freight-templates/mine` | ✅ |
| 04-logistics | 创建模板 | POST | `/api/seller/freight-templates` | ✅ |
| 04-logistics | 更新规则 | PUT | `/api/seller/freight-templates/{id}/rules` | ✅ |
| 04-logistics | 启用模板 | POST | `/api/seller/freight-templates/{id}/enable` | ✅ |
| 04-logistics | 停用模板 | POST | `/api/seller/freight-templates/{id}/disable` | ✅ |
| 04-logistics | 查询物流公司 | GET | `/api/seller/logistics-companies` | ✅ |
| 07-review | 评价列表 | GET | `/api/seller/reviews` | ✅ |
| 07-review | 评价详情 | GET | `/api/seller/reviews/{id}` | ✅ |
| 07-review | 回复评价 | POST | `/api/seller/reviews/{id}/reply` | ✅ |
| 08-account | 获取资料 | GET | `/users/me` | ✅ |
| 08-account | 通知列表 | GET | `/api/seller/notifications` | ❓ 待确认（缺失则 BE-4） |
| 09-export | 创建导出 | POST | `/api/seller/export/sales` | ❌ BE-3 |
| 09-export | 任务列表 | GET | `/api/seller/export/tasks` | ❌ BE-3 |
| 09-export | 下载文件 | GET | `/api/seller/export/tasks/{id}/download` | ❌ BE-3 |

---

## 8. 测试策略

### 8.1 API 客户端测试

每个 `.api.ts` 配套 `.spec.ts`，使用 axios-mock-adapter 模拟 HTTP 响应：
- 正常请求 → 验证 URL / method / params / body
- 错误响应 → 验证错误处理（401/403/404/409/500）
- 幂等键 → 验证 `X-Idempotency-Key` 头注入

| 测试文件 | 覆盖端点数 |
|----------|-----------|
| `shop.api.spec.ts` | 4 |
| `freight-template.api.spec.ts` | 5 |
| `logistics-company.api.spec.ts` | 1 |
| `review.api.spec.ts` | 3 |
| `export.api.spec.ts` | 3 |

### 8.2 组件测试

| 测试文件 | 覆盖场景 |
|----------|----------|
| `ImageUploader.spec.ts` | 上传成功 / 大小超限 / 类型不匹配 / 预览 / 删除 |
| `TemplateRuleEditor.spec.ts` | 添加行 / 删除行 / 编辑值 / 校验必填 |

### 8.3 覆盖率目标

- P1 新增代码测试覆盖率 ≥ 80%
- 运行 `pnpm test:coverage` 验证

---

## 9. 验收标准

每批完成后执行全量验证：

| 验证项 | 标准 | 命令 |
|--------|------|------|
| Lint | 0 errors / 0 warnings | `pnpm lint` |
| TypeCheck | 0 errors | `pnpm typecheck` |
| Test | 全部通过 | `pnpm test` |
| Build | 构建成功 | `pnpm build` |

每批完成后提交并推送到远程仓库 `origin/dev`。

---

## 10. P0 遗留问题处理

| 遗留项 | 处理方式 | 批次 |
|--------|----------|------|
| `/shop/application` 路由未注册（断链风险） | 批次 1 优先实现 | 1 |
| Profile.vue 占位 | 批次 1 完整实现 | 1 |
| Notifications.vue 占位 | 批次 1 完整实现（或 BE-4 标记） | 1 |
| seller mock 不覆盖 P1 | 新增 5 个 handler 文件 | 1-3 |
| BE-1 订单分页 page=0 | 不在 P1 范围，保持 TODO | - |
| BE-2 库存预警后端 | 不在 P1 范围，保持 mock | - |
