# 会员套餐 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：08-membership-ops 会员运营
- **页面类型**：列表管理页（CRUD + 启停）
- **目标用户**：运营管理员（Operator）
- **核心目标**：维护付费会员套餐（价格、时长、权益），控制套餐启停，为买家订阅会员提供选项。
- **访问入口**：左侧菜单「会员运营 → 会员套餐」；会员等级页权益跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部操作工具栏 + 套餐列表表格 + 新增/编辑模态框（含权益配置）。
- **关键区域**：
  - 区域 A（工具栏）：新增套餐、刷新、导出
  - 区域 B（套餐表格）：`<a-table>` 列含套餐名称、价格、时长（月）、关联等级、权益摘要、订阅数、状态、操作列
  - 区域 C（新增/编辑模态框）：`<a-modal width="640">` 含基础信息（名称、价格、时长）、关联会员等级（select）、权益配置（多选：专属客服/生日礼/折扣/积分加速/免费退换）、状态开关
- **响应式断点**：≥1200px 模态框 640px；992-1199px 模态框 480px。
- **首屏内容**：操作工具栏 + 启用状态下的套餐列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [新增套餐]                              [刷新]    │
├──────────────────────────────────────────────────┤
│ 套餐名称   价格  时长 关联等级 权益摘要 订阅 状态 操作│
│ 月度会员   ¥30   1    金卡    折扣+客服 156 启用 [编辑][停用]│
│ 年度会员   ¥288  12   金卡    折扣+客服+生日 892 启用 [编辑][停用]│
│ 钻石年卡   ¥888  12   钻石    全权益   56  启用 [编辑][停用]│
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/membership-packages` | 查询可购买套餐列表（运营复用） | Buyer（运营按需过滤启用） |
| POST | `/api/admin/membership-packages` | 创建会员套餐 | Operator, Admin |
| PUT | `/api/admin/membership-packages/{packageId}` | 更新会员套餐（名称、价格、时长、权益） | Operator, Admin |
| POST | `/api/admin/membership-packages/{packageId}/enable` | 启用会员套餐 | Operator, Admin |
| POST | `/api/admin/membership-packages/{packageId}/disable` | 停用会员套餐 | Operator, Admin |

- **请求参数**：`CreateMembershipPackageDto` 含 `Name`（必填）、`Price`、`DurationMonths`、`LinkedLevelId`、`Benefits`（权益码数组）、`Status`；`UpdateMembershipPackageDto` 同构。
- **响应字段**：`List<MembershipPackageDto>`，每项含 `Id`、`Name`、`Price`、`DurationMonths`、`LinkedLevelId`、`LinkedLevelName`、`Benefits`、`SubscriberCount`、`Status`（Active/Inactive）、`CreatedAt`。
- **数据加载策略**：进入页面加载启用套餐；启停操作后局部更新状态列。
- **缓存策略**：套餐选项常驻 Pinia，30 分钟过期，供买家端订阅页共享。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载启用套餐列表 → 渲染表格
  2. 点击「新增套餐」→ 打开模态框 → 配置基础信息与权益 → `<IdempotencyButton>` 提交 → 列表新增行
  3. 点击「编辑」→ 回填表单 → 提交更新 → 局部刷新
  4. 点击「停用」→ `<ConfirmDialog>` 确认 → 调用 disable → 状态列更新
- **分支流程**：
  - 关联等级校验：须选择已启用的会员等级
  - 价格校验：须大于 0、时长越长单价越优惠（年卡月均价 < 月卡价格）
  - 停用套餐：已订阅用户权益不受影响，新用户不可订阅
- **跨页面流转**：点击「关联等级」跳转会员等级页；点击「订阅数」跳转支付记录页（按套餐筛选）。
- **状态机可视化**：套餐状态 Inactive ↔ Active（启用/停用双向切换）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-input-number>`、`<a-select>`、`<a-checkbox-group>`、`<a-switch>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 套餐状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/启停按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='member:package'
  - `DataTable`（见 shared/components.md §6）— 套餐列表
  - `ConfirmDialog`（见 shared/components.md §10）— 停用二次确认
  - `EmptyState`（见 shared/components.md §5）— 无套餐时展示
- **图标使用**：`PlusOutlined` 新增、`EditOutlined` 编辑、`StopOutlined` 停用、`GiftOutlined` 权益
- **空状态**：`EmptyState` title="暂无会员套餐" ctaText="新增套餐"

## 6. 视觉规范
- **主色应用**：新增按钮主色 `#1677FF`，停用按钮默认色。
- **状态色**：启用 `#52C41A` 绿、停用 `#8C8C8C` 灰。
- **价格色**：`#FF4D4F` 16px semibold。
- **权益标签**：`<a-tag>` 蓝色 `#1677FF` 背景 `#E6F4FF`。
- **间距**：工具栏与表格 16px，表格行高 48px，模态框表单项 16px，权益多选 12px。
- **字体**：套餐名称 14px medium，价格 16px semibold `#FF4D4F`，时长 14px `#000000D9`，权益摘要 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；模态框提交 loading。
- **空数据**：列表空显示「暂无会员套餐」+ 新增 CTA。
- **错误态**：关联等级未启用 `message.error('关联会员等级未启用')`；价格校验失败按业务提示。
- **权限控制**：Operator/Admin 可访问；增删改需 `member:package` 权限。
- **并发与乐观锁**：编辑提交基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：停用需 `<ConfirmDialog>` 二次确认，说明已订阅用户不受影响。

## 8. 验收要点
- [ ] 列表展示套餐名称、价格、时长、关联等级、权益、订阅数
- [ ] 新增/编辑模态框含权益多选配置
- [ ] 关联等级须为已启用等级校验
- [ ] 停用套餐说明已订阅用户不受影响
- [ ] 启停操作后状态列局部刷新
- **性能要求**：列表加载 < 800ms。
- **可访问性**：价格 aria-label 含单位，权益标签 aria-label 含权益名。
