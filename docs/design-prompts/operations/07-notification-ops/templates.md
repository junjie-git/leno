# 通知模板 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：07-notification-ops 通知运营
- **页面类型**：列表管理页（CRUD + 预览）
- **目标用户**：运营管理员（Operator）
- **核心目标**：维护通知模板（按事件类型与渠道），支持创建、编辑、启停与渲染预览，保障通知文案与变量正确性。
- **访问入口**：左侧菜单「通知运营 → 通知模板」；通知记录页模板编码跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 模板列表表格 + 新增/编辑模态框（含变量与预览）。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含模板名称/编码关键词、事件类型（订单/支付/售后/营销）、渠道（短信/邮件/站内信/推送）、状态、查询/重置
  - 区域 B（工具栏）：新增模板、刷新、导出
  - 区域 C（模板表格）：`<a-table>` 列含模板编码、模板名称、事件类型、渠道、变量数、状态、更新时间、操作列
  - 区域 D（新增/编辑模态框）：`<a-modal width="720">` 含基础信息（编码、名称、事件类型、渠道）、标题模板、正文模板（含变量插值提示）、变量列表、状态开关
  - 区域 E（预览面板）：模态框右侧实时预览渲染结果（输入测试变量值）
- **响应式断点**：≥1200px 模态框 720px；992-1199px 模态框 520px。
- **首屏内容**：筛选条 + 启用状态下的模板列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [关键词][事件类型▼][渠道▼][状态▼] [查询][重置]    │
├──────────────────────────────────────────────────┤
│ [新增模板]                              [刷新]    │
├──────────────────────────────────────────────────┤
│ 编码    名称    事件  渠道  变量 状态 更新 操作   │
│ ORDER_PAID 订单已支付 订单 短信 3 启用 07-26 [编辑][预览][停用]│
│ REFUND_OK  退款成功   退款 站内 2 启用 07-25 [编辑][预览][停用]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/notification-templates` | 分页查询模板（按事件类型/渠道过滤） | Operator, Admin |
| GET | `/api/admin/notification-templates/{templateId}` | 查询模板详情 | Operator, Admin |
| POST | `/api/admin/notification-templates` | 创建模板 | Operator, Admin |
| PUT | `/api/admin/notification-templates/{templateId}` | 更新模板 | Operator, Admin |
| POST | `/api/admin/notification-templates/{templateId}/enable` | 启用模板 | Operator, Admin |
| POST | `/api/admin/notification-templates/{templateId}/disable` | 禁用模板 | Operator, Admin |
| POST | `/api/admin/notification-templates/{templateId}/preview` | 预览模板渲染结果 | Operator, Admin |

- **请求参数**：`SaveNotificationTemplateDto` 含 `Code`（必填，唯一）、`Name`、`EventType`、`Channel`（SMS/Email/InApp/Push）、`TitleTemplate`、`BodyTemplate`（含 `{{变量}}` 插值）、`Variables`（变量定义数组）、`Status`；查询参数 `eventType`、`channel`、`page`、`pageSize`；预览请求体 `PreviewTemplateDto` 含测试变量值字典。
- **响应字段**：`NotificationTemplateListResultDto`，含 `Items`（每项含 `TemplateId`、`Code`、`Name`、`EventType`、`Channel`、`Variables`、`Status`、`UpdatedAt`）、`Total`。
- **数据加载策略**：进入页面加载启用模板；预览实时调用预览接口渲染。
- **缓存策略**：模板常驻 Pinia，10 分钟过期，供通知发送链路共享。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载启用模板列表 → 渲染表格
  2. 点击「新增模板」→ 打开模态框 → 配置基础信息与模板内容 → `<IdempotencyButton>` 提交 → 列表新增行
  3. 点击「编辑」→ 调用详情接口回填 → 修改 → 提交更新 → 局部刷新
  4. 点击「预览」→ 弹出预览面板 → 输入测试变量值 → 调用 preview → 展示渲染结果
  5. 点击「停用」→ `<ConfirmDialog>` 确认 → 调用 disable → 状态列更新
- **分支流程**：
  - 模板编码唯一校验：后端返回 409，提示「模板编码已存在」
  - 变量插值校验：模板中 `{{变量}}` 须在变量列表中定义，否则提示未定义变量
  - 渠道限制：短信模板正文长度限制 70 字（含变量），超长提示
- **跨页面流转**：点击「模板编码」跳转通知记录页（按模板编码筛选）。
- **状态机可视化**：模板状态 Inactive ↔ Active（启用/禁用双向切换）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-modal>`、`<a-textarea>`、`<a-switch>`、`<a-tag>`、`<a-list>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 模板状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/启停按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='notification:template'
  - `DataTable`（见 shared/components.md §6）— 模板列表
  - `ConfirmDialog`（见 shared/components.md §10）— 停用二次确认
  - `EmptyState`（见 shared/components.md §5）— 无模板时展示
- **图标使用**：`PlusOutlined` 新增、`EditOutlined` 编辑、`EyeOutlined` 预览、`StopOutlined` 停用
- **空状态**：`EmptyState` title="暂无通知模板"

## 6. 视觉规范
- **主色应用**：新增/预览按钮主色 `#1677FF`，停用按钮默认色。
- **状态色**：启用 `#52C41A` 绿、禁用 `#8C8C8C` 灰。
- **渠道色**：短信 `#52C41A`、邮件 `#1677FF`、站内信 `#722ED1`、推送 `#FAAD14`（`<a-tag>` 区分）。
- **间距**：筛选条与表格 16px，表格行高 48px，模态框表单项 16px，变量列表项 8px。
- **字体**：模板编码 14px mono，模板名称 14px medium，变量插值 `#1677FF` 高亮，更新时间 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；模态框提交 loading；预览渲染 loading。
- **空数据**：列表空显示「暂无通知模板」+ 新增 CTA。
- **错误态**：编码重复 `message.error('模板编码已存在')`；变量未定义 `message.error('模板含未定义变量：xxx')`；预览失败展示错误。
- **权限控制**：Operator/Admin 可访问；增删改需 `notification:template` 权限。
- **并发与乐观锁**：编辑提交基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：停用需 `<ConfirmDialog>` 二次确认，说明停用后该事件不发送通知。

## 8. 验收要点
- [ ] 列表支持按关键词/事件类型/渠道/状态筛选
- [ ] 新增/编辑模态框含变量插值提示与变量列表
- [ ] 预览面板支持输入测试变量值实时渲染
- [ ] 模板编码唯一校验
- [ ] 变量插值与变量列表一致性校验
- [ ] 短信模板正文长度限制 70 字
- **性能要求**：列表分页 < 800ms，预览渲染 < 500ms。
- **可访问性**：模板编码 aria-label 含编码，变量插值 aria-label 含变量名。
