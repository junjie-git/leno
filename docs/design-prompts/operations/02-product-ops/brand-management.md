# 品牌管理 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：02-product-ops 商品运营
- **页面类型**：列表管理页（CRUD）
- **目标用户**：运营管理员（Operator）
- **核心目标**：维护商品品牌库，支持品牌的创建、编辑、启停与查询，为卖家发布商品提供品牌选项。
- **访问入口**：左侧菜单「商品运营 → 品牌管理」；商品审核页分类列点击品牌名跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 品牌列表表格 + 新增/编辑品牌模态框。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含品牌名称关键词、状态（启用/停用）、查询/重置按钮
  - 区域 B（操作工具栏）：新增品牌、刷新、导出
  - 区域 C（品牌表格）：`<a-table>` 列含品牌 Logo、品牌名称、英文名、创建人、创建时间、状态、操作列（编辑/启用/停用）
  - 区域 D（新增/编辑模态框）：`<a-modal width="520">` 含品牌名称（必填）、英文名、Logo 上传（`<a-upload>` 单图）、品牌简介、排序值、状态开关
- **响应式断点**：≥1200px 表格全展开；992-1199px 模态框宽度自适应。
- **首屏内容**：筛选条 + 启用状态下的品牌列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [品牌名称][状态▼] [查询][重置]                    │
├──────────────────────────────────────────────────┤
│ [新增品牌]                              [刷新]    │
├──────────────────────────────────────────────────┤
│ Logo 品牌名称  英文名  创建人 创建时间 状态 操作  │
│ [图] Apple     Apple   admin 2026-01 启用 [编辑][停用]│
│ [图] 华为      HUAWEI  admin 2026-02 启用 [编辑][停用]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/brands` | 分页查询品牌列表 | 已认证用户 |
| GET | `/api/brands/{id}` | 查询品牌详情 | 已认证用户 |
| POST | `/api/admin/brands` | 创建品牌 | Admin, Operator |
| PUT | `/api/admin/brands/{id}` | 更新品牌 | Admin, Operator |
| POST | `/api/admin/brands/{id}/enable` | 启用品牌 | Admin, Operator |
| POST | `/api/admin/brands/{id}/disable` | 停用品牌 | Admin, Operator |

- **请求参数**：`BrandQueryDto` 含 `Keyword`、`Status`、`Page`、`PageSize`；创建/更新请求体 `CreateBrandDto`/`UpdateBrandDto` 含 `Name`（必填，1-50 字）、`EnglishName`、`LogoUrl`、`Description`、`SortOrder`、`Status`。
- **响应字段**：`PageResult<BrandDto>`，每项含 `Id`、`Name`、`EnglishName`、`LogoUrl`、`Description`、`SortOrder`、`Status`（Active/Inactive）、`CreatedBy`、`CreatedAt`。
- **数据加载策略**：进入页面加载启用品牌列表；编辑时调用详情接口补全字段；启停操作后局部更新状态列。
- **缓存策略**：品牌选项常驻 Pinia，5 分钟过期，供商品发布表单下拉使用。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载启用品牌列表 → 渲染表格
  2. 点击「新增品牌」→ 打开模态框 → 填写表单 → `<IdempotencyButton>` 提交 → 关闭模态框并刷新列表
  3. 点击「编辑」→ 调用详情接口 → 回填表单 → 提交更新 → 局部刷新
  4. 点击「停用」→ `<ConfirmDialog>` 确认 → 调用 disable → 状态列更新为停用
- **分支流程**：
  - 停用品牌时若被商品引用：后端返回 409 Conflict，提示「该品牌被 N 个商品引用，无法停用」
  - Logo 上传：`<a-upload>` 限制 1 张，支持 JPG/PNG/WebP，最大 2MB，上传后回填 LogoUrl
- **跨页面流转**：点击品牌名称跳转商品审核页（携带品牌筛选）。
- **状态机可视化**：品牌状态 Inactive ↔ Active（启用/停用双向切换）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-modal>`、`<a-upload>`、`<a-switch>`、`<a-input-number>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 品牌状态展示，type='product'
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/启停按钮
  - `PermissionGuard`（见 shared/components.md §3）— 新增/编辑/启停权限控制，permission='brand:manage'
  - `DataTable`（见 shared/components.md §6）— 品牌列表
  - `ConfirmDialog`（见 shared/components.md §10）— 停用二次确认
  - `EmptyState`（见 shared/components.md §5）— 无品牌时展示
- **图标使用**：`PlusOutlined` 新增、`EditOutlined` 编辑、`StopOutlined` 停用、`PlayCircleOutlined` 启用
- **空状态**：`EmptyState` title="暂无品牌" ctaText="新增品牌"

## 6. 视觉规范
- **主色应用**：新增按钮主色 `#1677FF`，停用按钮默认色，启用按钮主色链接样式。
- **状态色**：启用 `#52C41A` 绿、停用 `#8C8C8C` 灰。
- **间距**：筛选条与表格间距 16px，表格行高 56px（含 Logo），模态框表单项间距 16px。
- **字体**：品牌名称 14px medium，英文名 12px `#8C8C8C`，创建时间 12px `#8C8C8C`。
- **Logo**：40×40px 圆角 4px，缺失显示首字母占位 `#F0F0F0`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；模态框提交按钮 loading。
- **空数据**：列表空显示「暂无品牌」+ 新增 CTA。
- **错误态**：名称重复 `message.error('品牌名称已存在')`；上传失败 `message.error('Logo 上传失败')`；409 冲突按业务提示。
- **权限控制**：Operator/Admin 可访问；新增/编辑/启停需 `brand:manage` 权限。
- **并发与乐观锁**：编辑提交后端基于聚合版本校验，冲突时提示「品牌已被他人修改，请刷新」。
- **危险操作确认**：停用品牌需 `<ConfirmDialog>` 二次确认，说明被引用时无法停用。

## 8. 验收要点
- [ ] 列表支持按名称关键词与状态筛选
- [ ] 新增/编辑表单品牌名称必填且唯一校验
- [ ] Logo 上传限制 1 张、2MB、JPG/PNG/WebP
- [ ] 停用被引用品牌时返回 409 并提示
- [ ] 启停操作后状态列局部刷新不重新拉全量
- **性能要求**：列表分页 < 800ms，>100 行启用虚拟滚动。
- **可访问性**：Logo 缺失时 alt 显示品牌名，表单 label 关联 input。
