# 物流公司管理 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：05-order-ops 订单运营
- **页面类型**：列表管理页（CRUD + 启停）
- **目标用户**：运营管理员（Operator）
- **核心目标**：维护平台支持的物流公司库，支持创建、编辑、启停物流公司，为卖家发货与买家退货提供物流选项。
- **访问入口**：左侧菜单「订单运营 → 物流公司管理」；订单管理页物流列点击跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 物流公司列表表格 + 新增/编辑模态框。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含公司名称关键词、状态（启用/停用）、查询/重置
  - 区域 B（工具栏）：新增物流公司、刷新、导出
  - 区域 C（公司表格）：`<a-table>` 列含公司名称、公司代码、Logo、官方电话、官网链接、状态、排序值、操作列
  - 区域 D（新增/编辑模态框）：`<a-modal width="520">` 含公司名称（必填）、公司代码（必填，唯一）、Logo 上传、官方电话、官网链接、排序值、状态开关
- **响应式断点**：≥1200px 表格全展开；992-1199px 模态框宽度自适应。
- **首屏内容**：筛选条 + 启用状态下的物流公司列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [公司名称][状态▼] [查询][重置]                    │
├──────────────────────────────────────────────────┤
│ [新增物流公司]                          [刷新]    │
├──────────────────────────────────────────────────┤
│ Logo 公司名称  代码  电话 官网 排序 状态 操作    │
│ [图] 顺丰速运  SF   95338 sf.com 1   启用 [编辑][停用]│
│ [图] 中通快递  ZTO  95311 zto.com 2  启用 [编辑][停用]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/logistics-companies` | 分页查询物流公司列表 | Operator, Admin |
| POST | `/api/admin/logistics-companies` | 创建物流公司 | Operator, Admin |
| PUT | `/api/admin/logistics-companies/{id}` | 更新物流公司可编辑字段 | Operator, Admin |
| POST | `/api/admin/logistics-companies/{id}/enable` | 启用物流公司 | Operator, Admin |
| POST | `/api/admin/logistics-companies/{id}/disable` | 停用物流公司 | Operator, Admin |

- **请求参数**：`CreateLogisticsCompanyDto` 含 `Name`（必填，1-50 字）、`Code`（必填，唯一）、`LogoUrl`、`Phone`、`Website`、`SortOrder`；`UpdateLogisticsCompanyDto` 同构；查询参数 `keyword`（string?，按 Name/Code 模糊匹配）、`status`（LogisticsCompanyStatus?，按启停状态过滤）、`page`（int，默认 1）、`pageSize`（int，默认 20）。
- **响应字段**：`List<LogisticsCompanyDto>`，每项含 `Id`、`Name`、`Code`、`LogoUrl`、`Phone`、`Website`、`SortOrder`、`Status`（Active/Inactive）、`CreatedAt`。
- **数据加载策略**：进入页面加载启用物流公司；启停操作后局部更新状态列。
- **缓存策略**：物流公司选项常驻 Pinia，30 分钟过期，供卖家发货、买家退货下拉使用。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载启用物流公司列表 → 渲染表格
  2. 点击「新增物流公司」→ 打开模态框 → 填写表单 → `<IdempotencyButton>` 提交 → 列表新增行
  3. 点击「编辑」→ 回填表单 → 提交更新 → 局部刷新
  4. 点击「停用」→ `<ConfirmDialog>` 确认 → 调用 disable → 状态列更新为停用
- **分支流程**：
  - 公司代码唯一校验：后端返回 409，提示「公司代码已存在」
  - 停用被引用公司：被订单引用时仍可停用（停用后新订单不可选，历史订单不变）
  - Logo 上传：`<a-upload>` 限制 1 张，支持 PNG/SVG，最大 1MB
- **跨页面流转**：点击公司名称跳转订单管理（携带物流筛选）。
- **状态机可视化**：物流公司状态 Inactive ↔ Active（启用/停用双向切换）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-modal>`、`<a-upload>`、`<a-switch>`、`<a-input-number>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 物流公司状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/启停按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='logistics:manage'
  - `DataTable`（见 shared/components.md §6）— 物流公司列表
  - `ConfirmDialog`（见 shared/components.md §10）— 停用二次确认
  - `EmptyState`（见 shared/components.md §5）— 无物流公司时展示
- **图标使用**：`PlusOutlined` 新增、`EditOutlined` 编辑、`StopOutlined` 停用、`PlayCircleOutlined` 启用、`LinkOutlined` 官网
- **空状态**：`EmptyState` title="暂无物流公司" ctaText="新增物流公司"

## 6. 视觉规范
- **主色应用**：新增按钮主色 `#1677FF`，停用按钮默认色。
- **状态色**：启用 `#52C41A` 绿、停用 `#8C8C8C` 灰。
- **间距**：筛选条与表格 16px，表格行高 48px，模态框表单项 16px。
- **字体**：公司名称 14px medium，公司代码 14px mono `#8C8C8C`，电话 12px `#8C8C8C`。
- **Logo**：32×32px 圆角 4px，缺失显示首字母占位 `#F0F0F0`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；模态框提交 loading。
- **空数据**：列表空显示「暂无物流公司」+ 新增 CTA。
- **错误态**：公司代码重复 `message.error('公司代码已存在')`；上传失败 `message.error('Logo 上传失败')`。
- **权限控制**：Operator/Admin 可访问；增删改需 `logistics:manage` 权限。
- **并发与乐观锁**：编辑提交基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：停用需 `<ConfirmDialog>` 二次确认，说明历史订单不受影响。

## 8. 验收要点
- [ ] 列表支持按名称关键词与状态筛选
- [ ] 新增/编辑表单公司名称与代码必填，代码唯一校验
- [ ] Logo 上传限制 1 张、1MB、PNG/SVG
- [ ] 启停操作后状态列局部刷新
- [ ] 物流公司按排序值升序展示
- **性能要求**：列表分页 < 800ms，>100 行启用虚拟滚动。
- **可访问性**：Logo 缺失时 alt 显示公司名，表单 label 关联 input。
