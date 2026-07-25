# 店铺治理 - 运营管理后台

## 1. 页面定位
- **所属端**：运营管理后台
- **所属模块**：04-seller-ops 卖家运营
- **页面类型**：列表治理页（状态管理 + 资质维护）
- **目标用户**：运营管理员（Operator）
- **核心目标**：对已上线店铺进行治理，支持暂停/恢复/关闭店铺，复审资质，处置违规店铺。
- **访问入口**：左侧菜单「卖家运营 → 店铺治理」；入驻审核通过后跳转。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选条 + 操作工具栏 + 店铺列表表格 + 店铺治理抽屉（含状态变更与资质复审）。
- **关键区域**：
  - 区域 A（筛选条）：`<a-form inline>` 含店铺名称关键词、店铺状态（已通过/已暂停/已关闭）、主营类目、查询/重置
  - 区域 B（工具栏）：导出列表、刷新、统计概览（已通过/已暂停/已关闭计数）
  - 区域 C（店铺表格）：`<a-table>` 列含店铺名称、卖家、主营类目、商品数、订单数、店铺评分、状态、最后治理时间、操作列
  - 区域 D（治理抽屉）：`<a-drawer width="720">` 展示店铺概览、经营指标（GMV/订单/评分）、资质列表（复审入口）、状态变更操作区
  - 区域 E（状态变更对话框）：`<a-modal>` 含变更原因（必填）、影响说明
- **响应式断点**：≥1200px 抽屉 720px；992-1199px 抽屉 520px。
- **首屏内容**：筛选条 + 已通过状态的店铺列表前 20 条。
- **线框图描述**：

```
┌──────────────────────────────────────────────────┐
│ [店铺名称][状态▼][类目▼] [查询][重置]             │
├──────────────────────────────────────────────────┤
│ 已通过: 156  已暂停: 8  已关闭: 12    [刷新][导出]│
├──────────────────────────────────────────────────┤
│ 店铺名称  卖家 类目 商品数 订单 评分 状态 操作    │
│ 数码旗舰 张三 数码  120   1560 4.8 已通过 [治理][详情]│
│ 服饰专场 李四 服饰  80    820  4.5 已暂停 [恢复][详情]│
├──────────────────────────────────────────────────┤
│ 分页器                                            │
└──────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/shops` | 分页查询店铺（按状态/类目过滤） | Admin, Operator |
| GET | `/api/admin/shops/{id}` | 查询店铺详情 | Admin, Operator |
| POST | `/api/admin/shops/{id}/suspend` | 暂停店铺营业 | Admin, Operator |
| POST | `/api/admin/shops/{id}/resume` | 恢复店铺营业 | Admin, Operator |
| POST | `/api/admin/shops/{id}/close` | 关闭店铺（终态） | Admin, Operator |
| GET | `/api/admin/shops/{id}/qualifications` | 查询资质列表 | Admin, Operator |
| POST | `/api/admin/shops/{id}/qualifications/{qualId}/approve` | 资质复审通过 | Admin, Operator |
| POST | `/api/admin/shops/{id}/qualifications/{qualId}/reject` | 资质复审驳回 | Admin, Operator |

- **请求参数**：`AdminShopQueryDto` 含 `Keyword`、`Status`、`MainCategory`、`Page`、`PageSize`；暂停/关闭请求体 `ActionReasonDto` 含 `Reason`（必填）。
- **响应字段**：`PageResult<ShopDto>`，每项含 `Id`、`Name`、`OwnerName`、`MainCategory`、`ProductCount`、`OrderCount`、`Rating`、`Status`（Active/Suspended/Closed）、`LastGovernedAt`。
- **数据加载策略**：进入页面加载已通过店铺；统计概览从列表分页元数据聚合；治理抽屉并行加载详情与资质。
- **缓存策略**：不缓存，店铺状态实时性强。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 加载已通过店铺列表 → 渲染表格与统计概览
  2. 点击「治理」→ 打开抽屉展示经营指标与资质列表
  3. 已通过店铺点击「暂停」→ `<ConfirmDialog>` + 原因输入 → 调用 suspend → 状态变更为已暂停
  4. 已暂停店铺点击「恢复」→ 确认 → 调用 resume → 状态变更为已通过
  5. 点击「关闭」→ 危险确认（说明终态不可逆）→ 调用 close → 状态变更为已关闭
- **分支流程**：
  - 资质复审：抽屉内资质列表点击「复审」→ 重新审核某项资质
  - 暂停原因分类：违规/资质过期/主动申请，分类影响审计记录
  - 关闭店铺前置：须先暂停，关闭为终态不可恢复
- **跨页面流转**：点击「商品数」跳转商品审核页（携带卖家筛选）；点击「订单数」跳转订单管理（携带卖家筛选）。
- **状态机可视化**：Active（已通过）↔ Suspended（已暂停）→ Closed（已关闭，终态）。关闭不可逆。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-form>`、`<a-input>`、`<a-select>`、`<a-drawer>`、`<a-modal>`、`<a-textarea>`、`<a-statistic>`、`<a-descriptions>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 店铺状态展示，type='shop'
  - `IdempotencyButton`（见 shared/components.md §2）— 暂停/恢复/关闭按钮
  - `PermissionGuard`（见 shared/components.md §3）— 治理权限控制，permission='seller:govern'
  - `DataTable`（见 shared/components.md §6）— 店铺列表
  - `ConfirmDialog`（见 shared/components.md §10）— 暂停/恢复/关闭二次确认
  - `EmptyState`（见 shared/components.md §5）— 无店铺时展示
- **图标使用**：`PauseCircleOutlined` 暂停、`PlayCircleOutlined` 恢复、`CloseCircleOutlined` 关闭、`SafetyOutlined` 资质复审
- **空状态**：`EmptyState` title="暂无店铺"

## 6. 视觉规范
- **主色应用**：恢复按钮主色 `#1677FF`，暂停按钮警告色 `#FAAD14`，关闭按钮危险色 `#FF4D4F`。
- **状态色**：已通过 `#52C41A` 绿、已暂停 `#FAAD14` 橙、已关闭 `#BFBFBF` 深灰。
- **评分色**：≥4.5 `#52C41A` 绿、4.0-4.5 `#FAAD14` 橙、<4.0 `#FF4D4F` 红。
- **间距**：筛选条与表格 16px，统计概览卡片间距 24px，表格行高 48px，抽屉区块 24px。
- **字体**：店铺名称 14px medium，评分 16px semibold，统计数字 24px semibold。
- **图标尺寸**：操作列图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-spin>` 包裹；抽屉加载 Skeleton。
- **空数据**：列表空显示「暂无店铺」，按状态筛选提示「该状态下暂无店铺」。
- **错误态**：状态变更失败 `message.error('操作失败，请重试')`；并发冲突提示「店铺状态已变更，请刷新」。
- **权限控制**：Operator/Admin 可访问；治理操作需 `seller:govern` 权限，关闭操作建议 Admin 角色。
- **并发与乐观锁**：状态变更基于聚合版本校验，冲突提示刷新。
- **危险操作确认**：暂停、关闭为危险操作，强制 `<ConfirmDialog>`，关闭说明终态不可逆且影响在售商品。

## 8. 验收要点
- [ ] 列表支持按名称/状态/类目筛选
- [ ] 统计概览展示各状态店铺计数
- [ ] 治理抽屉展示经营指标与资质复审入口
- [ ] 暂停/关闭必须填写原因
- [ ] 关闭操作明确提示终态不可逆
- [ ] 已关闭店铺不可恢复，操作按钮隐藏
- **性能要求**：列表分页 < 1s，治理抽屉 < 800ms。
- **可访问性**：状态标签 aria-label 含中文状态名，评分 aria-label 含数值。
