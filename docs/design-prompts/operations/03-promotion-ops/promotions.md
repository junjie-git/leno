# 促销活动 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：03-promotion-ops 促销运营
- **页面类型**：列表管理页（CRUD + 生命周期控制）
- **目标用户**：运营管理员（Operator）
- **核心目标**：创建与维护满减促销活动，控制活动生命周期（待生效/进行中/暂停/已关闭），保障促销规则的正确性与时效性。
- **访问入口**：左侧菜单「促销运营 → 促销活动」；运营总览页促销卡片跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 活动列表表格 + 新增/编辑活动抽屉。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含活动名称关键词、状态（待生效/进行中/暂停/已关闭）、时间范围、查询/重置
  - 区域 B（工具栏）：新增活动、刷新、导出
  - 区域 C（活动表格）：`<a-table>` 列含活动名称、类型（满减/满折/满赠）、门槛、优惠、适用范围、开始时间、结束时间、状态、操作列
  - 区域 D（新增/编辑抽屉）：`<a-drawer width="720">` 含基础信息（名称、类型、时间范围）、规则配置（门槛阶梯、优惠值、叠加规则）、适用范围（全品/指定分类/指定商品）
- **响应式断点**：≥1200px 抽屉 720px；992-1199px 抽屉 520px。
- **首屏内容**：筛选条 + 进行中状态的活动列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [名称][状态▼][时间范围] [查询][重置]              │
├──────────────────────────────────────────────────┤
│ [新增活动]                              [刷新]    │
├──────────────────────────────────────────────────┤
│ 名称    类型  门槛  优惠 范围  开始-结束 状态 操作│
│ 双11满减 满减 满300 减50 全品 11-11 进行 [暂停][详情]│
│ 年货节  满折 满500 8折  食品 01-28 待生效[激活][编辑]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/promotions` | 分页查询满减活动（按名称模糊/状态精确/时间区间过滤） | Operator, Admin |
| GET | `/api/admin/promotions/{activityId}` | 查询活动详情 | Operator, Admin |
| POST | `/api/admin/promotions` | 创建满减活动 | Operator, Admin |
| PUT | `/api/admin/promotions/{activityId}` | 更新活动规则 | Operator, Admin |
| POST | `/api/admin/promotions/{activityId}/activate` | 激活活动 | Operator, Admin |
| POST | `/api/admin/promotions/{activityId}/pause` | 暂停活动 | Operator, Admin |
| POST | `/api/admin/promotions/{activityId}/close` | 关闭活动（终态） | Operator, Admin |

- **请求参数**：`CreatePromotionActivityDto` 含 `Name`（必填）、`Type`（FullReduction/FullDiscount/FullGift）、`StartTime`、`EndTime`、`Rules`（阶梯规则数组）、`Scope`（All/Category/Product）、`ScopeIds`；查询参数 `name`（string?，名称模糊匹配）、`status`（PromotionStatus?，活动状态精确匹配）、`startTime`（DateTime?，活动开始时间下界）、`endTime`（DateTime?，活动结束时间上界）、`page`（int，默认 1）、`pageSize`（int，默认 20）。
- **响应字段**：`List<PromotionActivityDto>`，每项含 `Id`、`Name`、`Type`、`Status`（Pending/Active/Paused/Closed）、`StartTime`、`EndTime`、`Rules`、`Scope`、`CreatedBy`、`CreatedAt`。
- **数据加载策略**：进入页面加载进行中活动；切换状态重新请求；编辑时调用详情接口。
- **缓存策略**：不缓存，活动状态实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载进行中活动列表 → 渲染表格
  2. 点击「新增活动」→ 打开抽屉 → 配置规则与范围 → `<IdempotencyButton>` 提交 → 列表新增待生效行
  3. 点击「激活」→ `<ConfirmDialog>` 确认 → 调用 activate → 状态变更为进行中
  4. 点击「暂停」→ 确认 → 调用 pause → 状态变更为暂停
  5. 点击「关闭」→ 危险确认 → 调用 close → 状态变更为已关闭（终态）
- **分支流程**：
  - 待生效活动可编辑规则；进行中/暂停活动仅可查看与关闭
  - 时间范围校验：开始时间须晚于当前、结束时间须晚于开始
  - 阶梯规则校验：门槛值递增、优惠不超门槛
- **跨页面流转**：点击「适用范围」指定分类跳转分类管理；指定商品跳转商品审核。
- **状态机可视化**：Pending（待生效）→ Active（进行中）↔ Paused（暂停）→ Closed（已关闭，终态）。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-date-picker>`、`<a-drawer>`、`<a-input-number>`、`<a-radio-group>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 活动状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/激活/暂停/关闭按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='promotion:manage'
  - `DataTable`（见 shared/components.md §6）— 活动列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 活动时间范围
  - `ConfirmDialog`（见 shared/components.md §10）— 激活/暂停/关闭二次确认
  - `EmptyState`（见 shared/components.md §5）— 无活动时展示
- **图标使用**：`PlusOutlined` 新增、`PlayCircleOutlined` 激活、`PauseCircleOutlined` 暂停、`CloseCircleOutlined` 关闭
- **空状态**：`EmptyState` title="暂无促销活动"

## 6. 视觉规范
- **主色应用**：新增/激活按钮主色 `#1677FF`，关闭按钮危险色 `#FF4D4F`，暂停按钮默认色。
- **状态色**：待生效 `#FAAD14` 橙、进行中 `#52C41A` 绿、暂停 `#8C8C8C` 灰、已关闭 `#BFBFBF` 深灰。
- **间距**：筛选条与表格 16px，表格行高 48px，抽屉表单项 16px，阶梯规则项 12px。
- **字体**：活动名称 14px medium，优惠值 14px `#FF4D4F`，时间 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；抽屉提交 loading。
- **空数据**：列表空显示「暂无促销活动」+ 新增 CTA。
- **错误态**：时间冲突 `message.error('活动时间与现有活动重叠')`；激活失败按后端提示；关闭终态不可逆需强制确认。
- **权限控制**：Operator/Admin 可访问；增删改需 `promotion:manage` 权限。
- **并发与乐观锁**：状态变更基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：激活、暂停、关闭均为危险操作，强制 `<ConfirmDialog>`，关闭说明不可逆。

## 8. 验收要点
- [ ] 列表支持按名称/状态/时间范围筛选
- [ ] 新增抽屉支持满减/满折/满赠三类型与阶梯规则
- [ ] 时间范围与阶梯规则前端校验通过后才提交
- [ ] 状态机操作按钮按当前状态显隐（待生效可激活/编辑，进行中可暂停/关闭）
- [ ] 关闭操作不可逆，二次确认明确提示
- **性能要求**：列表分页 < 1s，>100 行启用虚拟滚动。
- **可访问性**：状态标签 aria-label 含中文状态名，抽屉表单 label 关联。
