# 商品审核 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：02-product-ops 商品运营
- **页面类型**：列表审核页
- **目标用户**：运营管理员（Operator）
- **核心目标**：审核卖家提交的商品上架申请，支持通过/驳回，并对违规库存进行人工调整与补货。
- **访问入口**：左侧菜单「商品运营 → 商品审核」；待办工作台「待审核商品」徽标跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 商品列表表格 + 审核操作抽屉（含商品详情与 SKU 列表）。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含关键词输入、卖家 ID、商品状态（待审核/已上架/已驳回/已下架）、分类筛选、提交重置按钮
  - 区域 B（操作工具栏）：批量审核通过、批量驳回、导出列表、刷新
  - 区域 C（商品表格）：`<a-table>` 列含缩略图+商品名、SKU 数、所属分类、所属卖家、价格区间、库存总量、状态、提交时间、操作列（查看详情/通过/驳回/调整库存）
  - 区域 D（详情抽屉）：`<a-drawer width="640">` 展示商品主图轮播、SPU 基础信息、SKU 列表（含库存/价格/规格）、审核历史
  - 区域 E（驳回对话框）：`<a-modal>` 含驳回原因（必填，最多 200 字）与提交
- **响应式断点**：≥1200px 表格全展开；992-1199px 抽屉宽度自适应至 480px。
- **首屏内容**：筛选条 + 待审核状态下的商品列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [关键词][卖家ID][状态▼][分类▼] [查询][重置]       │
├──────────────────────────────────────────────────┤
│ [批量通过][批量驳回]            [导出][刷新]      │
├──────────────────────────────────────────────────┤
│ ☐ 图片 商品名   SKU 分类 卖家 价格 库存 状态 操作 │
│ ☐ [缩略图] 商品A 3  数码 卖家X ¥99 ¥799 200 待审 [详情][通过][驳回]│
│ ☐ [缩略图] 商品B 1  服饰 卖家Y ¥49      50 待审 [详情][通过][驳回]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/products/all` | 全量商品分页查询（跨店铺） | Admin, Operator |
| POST | `/api/admin/products/{id}/approve` | 审核通过并上架 | Admin, Operator |
| POST | `/api/admin/products/{id}/reject` | 审核驳回 | Admin, Operator |
| POST | `/api/admin/products/{id}/skus/{skuId}/stock` | 调整 SKU 库存（delta 方式） | Admin, Operator |
| POST | `/api/admin/products/skus/{skuId}/replenish` | 为指定 SKU 补货 | Admin, Operator |

- **请求参数**：`ProductQueryDto` 含 `Keyword`、`SellerId`、`Status`（ProductStatus）、`CategoryId`、`Page`、`PageSize`；驳回请求体 `ActionReasonDto` 含 `Reason`（必填）；库存调整请求体 `UpdateStockDto` 含 `Delta`、`Reason`。
- **响应字段**：`PageResult<ProductDto>`，每项含 `Id`、`Title`、`MainImageUrl`、`Status`、`CategoryId`、`SellerId`、`Skus`（`SkuDto[]`，含 `Id`、`Spec`、`Price`、`Stock`）、`SubmittedAt`、`RejectReason`。
- **数据加载策略**：进入页面默认查询 `Status=PendingAudit` 的前 20 条；切换状态筛选重新请求；详情抽屉复用列表行数据，SKU 库存调整后局部刷新。
- **缓存策略**：不缓存，每次查询强制请求后端。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载待审核商品列表 → 渲染表格
  2. 点击「查看详情」→ 打开抽屉展示 SPU/SKU 详情与审核历史
  3. 点击「通过」→ `<ConfirmDialog>` 二次确认 → 调用 approve → 列表移除该行并提示成功
  4. 点击「驳回」→ 弹出驳回对话框 → 填写原因 → 调用 reject → 列表状态更新
- **分支流程**：
  - 批量审核：勾选多行 → 点击批量通过/驳回 → 确认对话框显示影响条数 → 串行调用接口 → 汇总成功/失败
  - 库存调整：详情抽屉内 SKU 行点击「调整库存」→ 弹出 delta 输入框与原因 → 调用 stock 接口 → 局部刷新库存列
- **跨页面流转**：点击卖家名称跳转卖家运营-店铺治理（携带 SellerId）；点击分类跳转分类管理。
- **状态机可视化**：商品状态 PendingAudit → Approved（已上架）/ Rejected（已驳回）→ 下架 OffShelf。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-drawer>`、`<a-modal>`、`<a-image-preview>`、`<a-textarea>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 商品状态展示，type='product'
  - `IdempotencyButton`（见 shared/components.md §2）— 通过/驳回/补货按钮
  - `PermissionGuard`（见 shared/components.md §3）— 审核操作权限控制，permission='product:audit'
  - `DataTable`（见 shared/components.md §6）— 商品列表
  - `ConfirmDialog`（见 shared/components.md §10）— 通过/批量操作二次确认
  - `EmptyState`（见 shared/components.md §5）— 无待审核商品时展示
- **图标使用**：`CheckOutlined` 通过、`CloseOutlined` 驳回、`EditOutlined` 调整库存、`EyeOutlined` 详情
- **空状态**：`EmptyState` title="暂无待审核商品"

## 6. 视觉规范
- **主色应用**：通过按钮主色 `#1677FF`，驳回按钮危险色 `#FF4D4F`。
- **状态色**：待审核 `#FAAD14` 橙、已上架 `#52C41A` 绿、已驳回 `#FF4D4F` 红、已下架 `#8C8C8C` 灰。
- **间距**：筛选条与表格间距 16px，表格行高 56px（含缩略图），抽屉内区块间距 24px。
- **字体**：商品标题 14px medium，价格 14px `#FF4D4F`，库存数字 14px `#000000D9`，辅助文字 12px `#8C8C8C`。
- **缩略图**：48×48px 圆角 4px，缺失图显示 `#F0F0F0` 占位。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；抽屉打开时 Skeleton 占位。
- **空数据**：列表空显示「暂无商品」，按状态筛选时提示「该状态下暂无商品」。
- **错误态**：审核接口失败 `message.error('审核操作失败，请重试')`；并发驳回（商品已被他人处理）提示「商品状态已变更，请刷新列表」。
- **权限控制**：Operator/Admin 可访问；批量操作与库存调整需 `product:audit` 权限。
- **并发与乐观锁**：审核操作后端基于聚合版本校验，前端冲突时提示刷新。
- **危险操作确认**：驳回、批量操作、库存调整为危险操作，强制 `<ConfirmDialog>` 二次确认。

## 8. 验收要点
- [ ] 列表支持按状态/分类/卖家/关键词组合筛选
- [ ] 待审核商品行操作列含「详情/通过/驳回」三个入口
- [ ] 驳回必须填写原因，原因少于 5 字时禁用提交
- [ ] 详情抽屉展示 SKU 列表与库存调整入口
- [ ] 批量操作显示影响条数并串行执行
- **性能要求**：列表分页 < 1s，详情抽屉 < 800ms，>100 行启用虚拟滚动。
- **可访问性**：缩略图 alt 含商品名，操作按钮 aria-label 描述操作语义。
