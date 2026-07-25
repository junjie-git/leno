# 秒杀活动 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：03-promotion-ops 促销运营
- **页面类型**：列表管理页（CRUD + 生命周期 + Redis 库存）
- **目标用户**：运营管理员（Operator）
- **核心目标**：创建与控制秒杀活动生命周期（待生效/进行中/已关闭），活动激活时初始化 Redis 多 SKU 库存，关闭时回写 DB，保障高并发秒杀链路稳定。
- **访问入口**：左侧菜单「促销运营 → 秒杀活动」；运营总览页秒杀卡片跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 活动列表表格 + 新增/编辑活动抽屉（含 SKU 库存配置）。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含活动名称关键词、状态（待生效/进行中/已关闭）、查询/重置
  - 区域 B（工具栏）：新增活动、刷新、导出
  - 区域 C（活动表格）：`<a-table>` 列含活动名称、SKU 列表、秒杀价、原价、开始时间、结束时间、Redis 库存状态、活动状态、操作列
  - 区域 D（新增/编辑抽屉）：`<a-drawer width="800">` 含基础信息（名称、时间）、SKU 配置（多 SKU 选择、秒杀价、秒杀库存）、限购设置
- **响应式断点**：≥1200px 抽屉 800px；992-1199px 抽屉 600px。
- **首屏内容**：筛选条 + 进行中状态的活动列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [名称][状态▼] [查询][重置]                        │
├──────────────────────────────────────────────────┤
│ [新增活动]                              [刷新]    │
├──────────────────────────────────────────────────┤
│ 名称   SKU   秒杀价 原价  时间段   库存 状态 操作│
│ 12点秒杀 iPhone ¥3999 ¥4999 12-14点 已激活 进行 [关闭][详情]│
│ 20点秒杀 耳机 ¥199  ¥299  20-22点 待初始化 待生效[激活][编辑]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/seckill/activities` | 分页查询秒杀活动（按状态过滤） | Operator, Admin |
| POST | `/api/admin/seckill/activities` | 创建秒杀活动（待生效态） | Operator, Admin |
| POST | `/api/admin/seckill/activities/{activityId}/activate` | 激活活动（初始化 Redis 多 SKU 库存） | Operator, Admin |
| POST | `/api/admin/seckill/activities/{activityId}/close` | 关闭活动（含 Redis 库存回写 DB） | Operator, Admin |

- **请求参数**：`CreateSeckillActivityDto` 含 `Name`、`StartTime`、`EndTime`、`Items`（`SeckillItemDto[]`，含 `SkuId`、`SeckillPrice`、`Stock`）、`PerUserLimit`；查询参数 `status`（SeckillStatus）、`page`、`pageSize`。
- **响应字段**：`List<SeckillActivityDto>`，每项含 `Id`、`Name`、`Status`（Pending/Active/Closed）、`StartTime`、`EndTime`、`Items`（含 `SkuId`、`SkuName`、`SeckillPrice`、`OriginalPrice`、`Stock`、`RemainingStock`）、`PerUserLimit`、`CreatedAt`。
- **数据加载策略**：进入页面加载进行中活动；激活/关闭后状态实时更新；进行中活动剩余库存不轮询，仅详情抽屉按需查询。
- **缓存策略**：不缓存，活动状态与 Redis 库存实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载进行中活动列表 → 渲染表格
  2. 点击「新增活动」→ 打开抽屉 → 配置 SKU 与秒杀价 → `<IdempotencyButton>` 提交 → 列表新增待生效行
  3. 点击「激活」→ `<ConfirmDialog>` 确认（说明将初始化 Redis 库存）→ 调用 activate → 状态变更为进行中
  4. 点击「关闭」→ 危险确认（说明 Redis 库存回写 DB）→ 调用 close → 状态变更为已关闭
- **分支流程**：
  - 激活前置校验：活动时间须有效、SKU 库存须大于 0、秒杀价须低于原价
  - 进行中活动不可编辑，仅可关闭
  - 关闭后剩余库存回写 DB，买家端立即下架
- **跨页面流转**：点击 SKU 名称跳转商品审核页（携带 SKU 详情）；点击「剩余库存」查看实时 Redis 数值。
- **状态机可视化**：Pending（待生效）→ Active（进行中）→ Closed（已关闭，终态）。激活与关闭均不可逆。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-date-picker>`、`<a-drawer>`、`<a-input-number>`、`<a-tag>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 活动状态展示
  - `IdempotencyButton`（见 shared/components.md §2）— 提交/激活/关闭按钮
  - `PermissionGuard`（见 shared/components.md §3）— 操作权限控制，permission='seckill:manage'
  - `DataTable`（见 shared/components.md §6）— 活动列表
  - `DateTimeRangePicker`（见 shared/components.md §4）— 活动时间范围
  - `ConfirmDialog`（见 shared/components.md §10）— 激活/关闭二次确认
  - `EmptyState`（见 shared/components.md §5）— 无活动时展示
- **图标使用**：`PlusOutlined` 新增、`ThunderboltOutlined` 激活、`CloseCircleOutlined` 关闭、`EyeOutlined` 详情
- **空状态**：`EmptyState` title="暂无秒杀活动"

## 6. 视觉规范
- **主色应用**：激活按钮主色 `#1677FF`，关闭按钮危险色 `#FF4D4F`，秒杀价 `#FF4D4F`。
- **状态色**：待生效 `#FAAD14` 橙、进行中 `#52C41A` 绿、已关闭 `#BFBFBF` 深灰。
- **Redis 库存标识**：已初始化绿点 `#52C41A`，待初始化灰点 `#8C8C8C`。
- **间距**：筛选条与表格 16px，表格行高 48px，抽屉表单项 16px，SKU 配置项 12px。
- **字体**：活动名称 14px medium，秒杀价 16px semibold `#FF4D4F`，原价 12px `#8C8C8C` 删除线。
- **图标尺寸**：操作列图标 16px，闪电图标 14px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；激活/关闭操作 loading。
- **空数据**：列表空显示「暂无秒杀活动」+ 新增 CTA。
- **错误态**：激活失败 `message.error('Redis 库存初始化失败，请重试')`；关闭回写失败 `message.error('库存回写失败，请联系系统管理员')`。
- **权限控制**：Operator/Admin 可访问；增删改激活关闭需 `seckill:manage` 权限。
- **并发与乐观锁**：激活/关闭基于状态机校验，重复操作返回 409 提示「活动状态已变更」。
- **危险操作确认**：激活、关闭为危险操作，强制 `<ConfirmDialog>`，激活说明初始化 Redis 库存，关闭说明库存回写 DB 且不可逆。

## 8. 验收要点
- [ ] 列表支持按名称/状态筛选
- [ ] 新增抽屉支持多 SKU 配置（秒杀价、库存、限购）
- [ ] 秒杀价须低于原价、库存须大于 0 前端校验
- [ ] 激活操作明确提示「初始化 Redis 库存」
- [ ] 关闭操作明确提示「库存回写 DB 且不可逆」
- [ ] 进行中活动不可编辑，仅可关闭
- **性能要求**：列表分页 < 1s，激活/关闭 < 2s（含 Redis 操作）。
- **可访问性**：状态标签 aria-label 含中文状态名，SKU 列表 aria-label 含库存信息。
