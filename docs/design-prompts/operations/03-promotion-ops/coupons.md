# 优惠券管理 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：03-promotion-ops 促销运营
- **页面类型**：列表管理页（CRUD + 发放）
- **目标用户**：运营管理员（Operator）
- **核心目标**：维护优惠券模板，控制券模板启停，并支持批量发放优惠券以提升买家活跃度。
- **访问入口**：左侧菜单「促销运营 → 优惠券管理」；运营总览页优惠券领取卡片跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 券模板列表表格 + 新增/编辑券模板抽屉 + 发放对话框。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含券名称关键词、状态（启用/停用）、券类型（满减券/折扣型优惠券/无门槛券）、查询/重置
  - 区域 B（工具栏）：新增券模板、刷新、导出
  - 区域 C（券表格）：`<a-table>` 列含券名称、类型、面额/折扣、门槛、有效期、已领/总量、状态、操作列
  - 区域 D（新增/编辑抽屉）：`<a-drawer width="640">` 含基础信息（名称、类型、面额、门槛、有效期）、库存（总量、每人限领）、使用范围
  - 区域 E（发放对话框）：`<a-modal>` 含发放数量输入、目标用户范围（全部/指定用户）、确认发放
- **响应式断点**：≥1200px 抽屉 640px；992-1199px 抽屉 480px。
- **首屏内容**：筛选条 + 启用状态下的券模板列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [券名称][状态▼][类型▼] [查询][重置]               │
├──────────────────────────────────────────────────┤
│ [新增券模板]                            [刷新]    │
├──────────────────────────────────────────────────┤
│ 名称   类型  面额  门槛 有效期 已领/总量 状态 操作│
│ 新人券 满减 ¥10  满50 30天  1200/5000 启用 [发放][编辑][停用]│
│ 9折券  折扣 9折  无   7天   856/2000  启用 [发放][编辑][停用]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/coupons` | 分页查询券模板（按状态过滤） | Operator, Admin |
| POST | `/api/admin/coupons` | 创建券模板 | Operator, Admin |
| PUT | `/api/admin/coupons/{couponId}` | 更新券模板 | Operator, Admin |
| POST | `/api/admin/coupons/{couponId}/publish` | 发布券模板（启用） | Operator, Admin |
| POST | `/api/admin/coupons/{couponId}/stop` | 停用券模板 | Operator, Admin |
| POST | `/api/admin/coupons/{couponId}/issue` | 批量发放优惠券（增加发放量） | Operator, Admin |

- **请求参数**：`CreateCouponDto` 含 `Name`、`Type`（FullReduction/Discount/NoThreshold）、`FaceValue`、`Threshold`、`ValidDays`、`TotalQuantity`、`PerUserLimit`、`Scope`；查询参数 `status`（CouponTemplateStatus）、`page`、`pageSize`；发放参数 `quantity`（int，query）。
- **响应字段**：`List<CouponDto>`，每项含 `Id`、`Name`、`Type`、`FaceValue`、`Threshold`、`ValidDays`、`TotalQuantity`、`IssuedQuantity`、`PerUserLimit`、`Status`（Active/Inactive）、`CreatedAt`。
- **数据加载策略**：进入页面加载启用券模板；发放后局部更新已领/总量列。
- **缓存策略**：不缓存，发放量实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载启用券模板列表 → 渲染表格
  2. 点击「新增券模板」→ 打开抽屉 → 配置券信息 → `<IdempotencyButton>` 提交 → 列表新增行
  3. 点击「发放」→ 弹出发放对话框 → 输入数量 → 确认 → 调用 issue → 已领/总量列更新
  4. 点击「停用」→ `<ConfirmDialog>` 确认 → 调用 stop → 状态列更新
- **分支流程**：
  - 发放数量校验：须为正整数且不超剩余库存（TotalQuantity - IssuedQuantity）
  - 停用券模板后买家端不可见，已领取的券仍有效
  - 已发放量 > 0 的券模板不可删除，仅可停用
- **跨页面流转**：点击「已领/总量」跳转通知记录页（按券 ID 筛选发放通知）。
- **状态机可视化**：券模板状态 Inactive ↔ Active；停用后买家端不可领取。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-input-number>`、`<a-drawer>`、`<a-modal>`、`<a-radio-group>`、`<a-date-picker>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 券模板状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/发放/启停按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='coupon:manage'
  - `DataTable`（见 shared/components.md §6）— 券模板列表
  - `ConfirmDialog`（见 shared/components.md §10）— 发放/停用二次确认
  - `EmptyState`（见 shared/components.md §5）— 无券模板时展示
- **图标使用**：`PlusOutlined` 新增、`SendOutlined` 发放、`EditOutlined` 编辑、`StopOutlined` 停用
- **空状态**：`EmptyState` title="暂无优惠券模板"

## 6. 视觉规范
- **主色应用**：新增/发放按钮主色 `#1677FF`，停用按钮默认色。
- **状态色**：启用 `#52C41A` 绿、停用 `#8C8C8C` 灰。
- **券面额视觉**：满减券面额 `#FF4D4F` 红色 16px semibold，折扣型优惠券 9 折样式同色。
- **间距**：筛选条与表格 16px，表格行高 48px，抽屉表单项 16px。
- **字体**：券名称 14px medium，面额 16px semibold，已领/总量 12px `#8C8C8C`。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；发放对话框 loading。
- **空数据**：列表空显示「暂无优惠券模板」+ 新增 CTA。
- **错误态**：发放超量 `message.error('发放数量超过剩余库存')`；面额超门槛 `message.error('面额不能大于门槛')`。
- **权限控制**：Operator/Admin 可访问；增删改发放需 `coupon:manage` 权限。
- **并发与乐观锁**：发放基于原子计数，并发安全；停用基于版本校验。
- **危险操作确认**：发放、停用需 `<ConfirmDialog>` 二次确认，发放说明影响用户范围。

## 8. 验收要点
- [ ] 列表支持按名称/状态/类型筛选
- [ ] 新增抽屉支持满减/折扣/无门槛三类型
- [ ] 发放数量校验为正整数且不超剩余库存
- [ ] 面额不大于门槛（满减券）
- [ ] 已发放的券模板仅可停用不可删除
- **性能要求**：列表分页 < 800ms，>100 行启用虚拟滚动。
- **可访问性**：面额数值 aria-label 含单位，表单 label 关联。
