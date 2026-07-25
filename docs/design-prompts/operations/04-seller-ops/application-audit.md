# 入驻审核 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：04-seller-ops 卖家运营
- **页面类型**：列表审核页（含资质审核）
- **目标用户**：运营管理员（Operator）
- **核心目标**：审核卖家提交的店铺入驻申请与资质材料，支持通过/驳回，保障平台卖家资质合规。
- **访问入口**：左侧菜单「卖家运营 → 入驻审核」；待办工作台「待审核入驻」徽标跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 申请列表表格 + 审核详情抽屉（含店铺信息与资质列表）。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含店铺名称关键词、申请人、状态（待审核/已通过/已驳回/已暂停/已关闭）、查询/重置
  - 区域 B（工具栏）：批量通过、批量驳回、刷新、导出
  - 区域 C（申请表格）：`<a-table>` 列含店铺名称、申请人、主营类目、资质数、提交时间、状态、操作列
  - 区域 D（审核抽屉）：`<a-drawer width="720">` 展示店铺基础信息、联系方式、主营类目、资质列表（含文件预览、审核状态、单独通过/驳回）
  - 区域 E（驳回对话框）：`<a-modal>` 含驳回原因（必填，最多 200 字）
- **响应式断点**：≥1200px 抽屉 720px；992-1199px 抽屉 520px。
- **首屏内容**：筛选条 + 待审核状态的入驻申请前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [店铺名称][申请人][状态▼] [查询][重置]            │
├──────────────────────────────────────────────────┤
│ [批量通过][批量驳回]                    [刷新]    │
├──────────────────────────────────────────────────┤
│ 店铺名称  申请人 主营类目 资质数 提交时间 状态 操作│
│ 数码旗舰店 张三   数码     3    2026-07 待审 [详情][通过][驳回]│
│ 服饰专场  李四   服饰     2    2026-07 待审 [详情][通过][驳回]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/shops` | 分页查询店铺（按状态过滤、关键词模糊） | Admin, Operator |
| GET | `/api/admin/shops/{id}` | 查询店铺详情 | Admin, Operator |
| POST | `/api/admin/shops/{id}/approve` | 审核通过入驻申请 | Admin, Operator |
| POST | `/api/admin/shops/{id}/reject` | 驳回入驻申请 | Admin, Operator |
| GET | `/api/admin/shops/{id}/qualifications` | 查询店铺资质列表 | Admin, Operator |
| POST | `/api/admin/shops/{id}/qualifications/{qualId}/approve` | 审核通过资质 | Admin, Operator |
| POST | `/api/admin/shops/{id}/qualifications/{qualId}/reject` | 驳回资质 | Admin, Operator |

- **请求参数**：`AdminShopQueryDto` 含 `Keyword`、`Status`、`Page`、`PageSize`；驳回请求体 `ActionReasonDto` 含 `Reason`（必填）。
- **响应字段**：`PageResult<ShopDto>`，每项含 `Id`、`Name`、`OwnerName`、`ContactPhone`、`MainCategory`、`Status`（PendingReview/Active/Suspended/Closed/Rejected）、`SubmittedAt`；`QualificationDto` 含 `Id`、`Type`（营业执照/法人身份证/品牌授权）、`FileUrl`、`Status`、`RejectReason`。
- **数据加载策略**：进入页面加载待审核申请；点击详情调用店铺详情与资质列表两个接口并行加载。
- **缓存策略**：不缓存，审核状态实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载待审核入驻申请 → 渲染表格
  2. 点击「详情」→ 打开抽屉展示店铺信息与资质列表
  3. 点击资质「预览」→ `<a-image-preview>` 展示资质文件
  4. 点击资质「通过」/「驳回」→ 单独审核某项资质
  5. 全部资质通过后点击「通过」→ `<ConfirmDialog>` 确认 → 调用 approve → 店铺状态变更为已通过
  6. 点击「驳回」→ 弹出对话框 → 填写原因 → 调用 reject → 状态变更为已驳回
- **分支流程**：
  - 资质未全部审核通过时，店铺通过按钮置灰提示「请先完成所有资质审核」
  - 批量审核：勾选多行 → 批量通过/驳回 → 串行调用 → 汇总结果
- **跨页面流转**：点击「主营类目」跳转分类管理；通过后点击「查看店铺」跳转店铺治理。
- **状态机可视化**：PendingReview（待审核）→ Active（已通过）/ Rejected（已驳回）→ 治理阶段可 Suspend/Close。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-drawer>`、`<a-modal>`、`<a-image-preview>`、`<a-textarea>`、`<a-descriptions>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 店铺状态展示，type='shop'
  - `IdempotencyButton`（见 shared/components.md §2）— 通过/驳回按钮
  - `PermissionGuard`（见 shared/components.md §3）— 审核权限控制，permission='seller:audit'
  - `DataTable`（见 shared/components.md §6）— 申请列表
  - `ConfirmDialog`（见 shared/components.md §10）— 通过/批量操作二次确认
  - `EmptyState`（见 shared/components.md §5）— 无待审核申请时展示
- **图标使用**：`CheckOutlined` 通过、`CloseOutlined` 驳回、`EyeOutlined` 详情预览、`FileOutlined` 资质文件
- **空状态**：`EmptyState` title="暂无待审核入驻申请"

## 6. 视觉规范
- **主色应用**：通过按钮主色 `#1677FF`，驳回按钮危险色 `#FF4D4F`。
- **状态色**：待审核 `#FAAD14` 橙、已通过 `#52C41A` 绿、已驳回 `#FF4D4F` 红、已暂停 `#FAAD14` 橙、已关闭 `#BFBFBF` 深灰。
- **资质状态色**：已通过绿点、待审核橙点、已驳回红点。
- **间距**：筛选条与表格 16px，表格行高 48px，抽屉区块间距 24px，资质项间距 12px。
- **字体**：店铺名称 14px medium，资质类型 14px，提交时间 12px `#8C8C8C`。
- **资质文件预览**：缩略图 80×80px 圆角 4px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；抽屉打开时资质列表 Skeleton。
- **空数据**：列表空显示「暂无入驻申请」，资质空显示「该店铺未上传资质」。
- **错误态**：审核失败 `message.error('审核操作失败，请重试')`；资质文件加载失败显示占位图。
- **权限控制**：Operator/Admin 可访问；审核操作需 `seller:audit` 权限。
- **并发与乐观锁**：审核基于聚合版本校验，冲突提示「申请状态已变更，请刷新」。
- **危险操作确认**：驳回、批量操作为危险操作，强制 `<ConfirmDialog>` 二次确认。

## 8. 验收要点
- [ ] 列表支持按名称/申请人/状态筛选
- [ ] 详情抽屉展示店铺信息与资质列表
- [ ] 资质支持单独审核与文件预览
- [ ] 全部资质通过前店铺通过按钮置灰
- [ ] 驳回必须填写原因
- [ ] 批量操作显示影响条数并串行执行
- **性能要求**：列表分页 < 1s，资质预览 < 500ms。
- **可访问性**：资质文件 alt 含类型描述，审核按钮 aria-label 含操作语义。
