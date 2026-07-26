# 会员等级 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：08-membership-ops 会员运营
- **页面类型**：列表管理页（CRUD + 启停）
- **目标用户**：运营管理员（Operator）
- **核心目标**：维护会员等级体系（等级名称、门槛、折扣率），控制等级启停，为买家会员权益分层提供基础。
- **访问入口**：左侧菜单「会员运营 → 会员等级」；积分统计页等级分布跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部操作工具栏 + 会员等级列表表格 + 新增/编辑模态框。
- **关键区域**：
  - 区域 A（工具栏）：新增等级、刷新、导出、等级体系说明（按等级编号升序）
  - 区域 B（等级表格）：`<a-table>` 列含等级编号、等级名称、成长值门槛、折扣率、权益说明、状态、操作列
  - 区域 C（新增/编辑模态框）：`<a-modal width="520">` 含等级编号（自动递增）、等级名称（必填）、成长值门槛、折扣率（0-1）、权益说明、状态开关
- **响应式断点**：≥1200px 表格全展开；992-1199px 模态框宽度自适应。
- **首屏内容**：操作工具栏 + 全部会员等级列表（按等级编号升序）。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [新增等级]                              [刷新]    │
├──────────────────────────────────────────────────┤
│ 编号 等级名称  门槛   折扣  权益说明  状态 操作   │
│ 1   普通会员  0      1.0  基础权益   启用 [编辑][停用]│
│ 2   V2 会员  1000   0.95 95折+生日礼 启用 [编辑][停用]│
│ 3   V3 会员  5000   0.9  9折+专属客服 启用 [编辑][停用]│
│ 4   钻石会员  20000  0.85 85折+付费会员权益   启用 [编辑][停用]│
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/members/levels` | 查询全部会员等级（按等级编号升序） | Operator, Admin |
| POST | `/api/admin/members/levels` | 创建会员等级 | Operator, Admin |
| PUT | `/api/admin/members/levels/{levelId}` | 更新会员等级（名称、门槛、折扣率） | Operator, Admin |
| POST | `/api/admin/members/levels/{levelId}/enable` | 启用会员等级 | Operator, Admin |
| POST | `/api/admin/members/levels/{levelId}/disable` | 停用会员等级 | Operator, Admin |

- **请求参数**：`CreateMembershipLevelDto` 含 `Name`（必填，1-20 字）、`GrowthThreshold`、`DiscountRate`（0-1）、`Benefits`、`Status`；`UpdateMembershipLevelDto` 同构。
- **响应字段**：`List<MembershipLevelDto>`，每项含 `Id`、`LevelNo`、`Name`、`GrowthThreshold`、`DiscountRate`、`Benefits`、`Status`（Active/Inactive）、`MemberCount`、`CreatedAt`。
- **数据加载策略**：进入页面加载全部等级（按等级编号升序）；启停操作后局部更新状态列。
- **缓存策略**：等级配置常驻 Pinia，30 分钟过期，供买家端会员页与下单折扣计算共享。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载全部会员等级 → 渲染表格
  2. 点击「新增等级」→ 打开模态框 → 填写表单 → `<IdempotencyButton>` 提交 → 列表新增行
  3. 点击「编辑」→ 回填表单 → 提交更新 → 局部刷新
  4. 点击「停用」→ `<ConfirmDialog>` 确认 → 调用 disable → 状态列更新
- **分支流程**：
  - 等级编号自动递增，不可手动修改
  - 成长值门槛校验：须大于上一等级门槛、小于下一等级门槛
  - 折扣率校验：0-1 之间，须优于上一等级（折扣率递减）
  - 停用等级：已有该等级的会员不受影响，新会员不可达该等级
- **跨页面流转**：点击「会员数」跳转用户管理（按等级筛选，系统管理后台）。
- **状态机可视化**：等级状态 Inactive ↔ Active（启用/停用双向切换）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-input-number>`、`<a-switch>`、`<a-textarea>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 等级状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/启停按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='member:level'
  - `DataTable`（见 shared/components.md §6）— 等级列表
  - `ConfirmDialog`（见 shared/components.md §10）— 停用二次确认
  - `EmptyState`（见 shared/components.md §5）— 无等级时展示
- **图标使用**：`PlusOutlined` 新增、`EditOutlined` 编辑、`StopOutlined` 停用、`CrownOutlined` 等级
- **空状态**：`EmptyState` title="暂无会员等级" ctaText="新增等级"

## 6. 视觉规范
- **主色应用**：新增按钮主色 `#1677FF`，停用按钮默认色。
- **状态色**：启用 `#52C41A` 绿、停用 `#8C8C8C` 灰。
- **等级色**：普通 `#8C8C8C`、V2 `#8C8C8C`、V3 `#FAAD14` 金、钻石 `#722ED1` 紫（等级图标区分）。
- **间距**：工具栏与表格 16px，表格行高 48px，模态框表单项 16px。
- **字体**：等级名称 14px medium，折扣率 14px `#FF4D4F`，门槛 14px `#000000D9`，权益说明 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px，等级图标 20px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；模态框提交 loading。
- **空数据**：列表空显示「暂无会员等级」+ 新增 CTA。
- **错误态**：门槛不递增 `message.error('成长值门槛须大于上一等级')`；折扣率不递减 `message.error('折扣率须优于上一等级')`。
- **权限控制**：Operator/Admin 可访问；增删改需 `member:level` 权限。
- **并发与乐观锁**：编辑提交基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：停用需 `<ConfirmDialog>` 二次确认，说明已有会员不受影响。

## 8. 验收要点
- [ ] 列表按等级编号升序展示
- [ ] 新增等级编号自动递增不可修改
- [ ] 成长值门槛递增校验、折扣率递减校验
- [ ] 停用等级说明已有会员不受影响
- [ ] 启停操作后状态列局部刷新
- **性能要求**：列表加载 < 800ms。
- **可访问性**：等级名称 aria-label 含等级编号，折扣率 aria-label 含数值。
