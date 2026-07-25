# 限流规则 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：04-runtime-ops 运行时运维
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）
- **核心目标**：管理各域 API 限流规则（RateLimitRule），配置阈值/窗口/算法/维度，启停规则并热生效。
- **访问入口**：Sider「运行时运维 → 限流规则」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 新建/编辑弹窗。
- **关键区域**：
  - 区域 A（筛选条）：目标 API 路径搜索 + 启用状态筛选 + 目标上下文多选（按 BC 分治：UserAuth/Product/Order/Payment 等）+ 「新增规则」按钮。
  - 区域 B（主表格）：列含目标 API/目标上下文/阈值/窗口/算法/维度/状态/操作（编辑/启用/停用），分页 20。
  - 区域 C（弹窗表单）：`<a-modal width="560">` 含 targetApi、targetContext、limit、windowSeconds、algorithm（滑动窗口/令牌桶/固定窗口）、scope（IP/用户/全局/店铺）。
- **响应式断点**：≥1200px 表格 8 列；992-1199px 隐藏「窗口」列。
- **首屏内容**：全部规则列表（按目标上下文分组排序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [搜索 API] [状态 ▼] [上下文多选] [新增规则]    │
├────────────────────────────────────────────────┤
│ API │ 上下文 │ 阈值 │ 窗口 │ 算法 │ 维度 │ 状态 │ 操作 │
│ /api/orders │ Order │ 100 │ 60s │ 滑动 │ 用户 │启用│编辑/停用│
└────────────────────────────────────────────────┘
→ 弹窗：targetApi/targetContext/limit/window/algorithm/scope
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/rate-limit-rules` | 分页查询限流规则 | Admin |
| GET | `/api/admin/rate-limit-rules/{id}` | 按标识获取限流规则详情 | Admin |
| POST | `/api/admin/rate-limit-rules` | 创建限流规则 | Admin |
| PUT | `/api/admin/rate-limit-rules/{id}` | 更新限流规则（乐观并发控制） | Admin |
| POST | `/api/admin/rate-limit-rules/{id}/enable` | 启用限流规则 | Admin |
| POST | `/api/admin/rate-limit-rules/{id}/disable` | 停用限流规则 | Admin |

- **请求参数**：列表 `targetApi/enabled/page/pageSize`；创建 `SaveRateLimitRuleDto`（targetApi/targetContext/limit/windowSeconds/algorithm/scope）；编辑同 DTO。
- **响应字段**：`RateLimitRuleDto` 含 `RuleId`、`TargetApi`、`TargetContext`、`Limit`、`WindowSeconds`、`Algorithm`、`Scope`、`Enabled`、`UpdatedBy`、`UpdatedAt`、`Version`。
- **数据加载策略**：进入页面加载首页；筛选重新请求；编辑按需加载详情。
- **缓存策略**：列表缓存 1 分钟（规则变更需快速反映）；详情不缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/rate-limit-rules?page=1&pageSize=20` → 表格渲染。
  2. 点击「新增规则」 → 弹窗填表 → POST → `message.success('规则已创建')`。
  3. 点击「编辑」 → 弹窗预填 → PUT → 处理 409 乐观锁冲突 → 刷新。
  4. 点击「启用/停用」 → `ConfirmDialog` → POST enable/disable → 状态更新。
- **分支流程**：
  - 409 乐观锁冲突：`message.error('数据已被其他用户修改，请刷新后重试')` 3s，自动重新加载详情。
  - 目标 API 已存在规则：后端 409，`message.error('该 API 已有限流规则')` 3s。
  - 阈值或窗口 ≤ 0：前端校验拦截。
- **跨页面流转**：点击「查看生效情况」跳 `/monitoring/prometheus-dashboard?metric=rate_limit&api={targetApi}`。
- **状态机可视化**：Disabled → Enabled → Disabled，`StatusTag` 自定义 rateLimit 类型。算法与维度用 `<a-tag>` 标签展示。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-input-number>`、`<a-select>`、`<a-tag>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 启用状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 启停确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`ThunderboltOutlined` 16px。
- **空状态**：「暂无限流规则」+ CTA「新增规则」。

## 6. 视觉规范
- **主色应用**：新增按钮主色；算法 `<a-tag color="blue">`；维度 `<a-tag color="cyan">`。
- **状态色**：Enabled 绿、Disabled 灰。
- **间距**：筛选条与表格 16px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；targetApi 14px medium monospace；阈值与窗口 14px semibold。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：409 乐观锁或重复规则 `message.error` 3s，自动刷新。
- **权限控制**：页面级 `roles: ['Admin']`（仅 Admin 可配置限流）；按钮 `PermissionGuard permission="rate-limit:write"`。
- **并发与乐观锁**：编辑使用 `Version` 字段乐观锁；冲突返回 409，前端自动重新加载。
- **危险操作确认**：停用启用中的规则 `ConfirmDialog` 内容「停用后该 API 将不再受限流保护，可能在高并发下被击穿。可随时启用恢复。」确认按钮 danger 红色。

## 8. 验收要点
- [ ] 阈值与窗口必须 > 0（前端校验）
- [ ] 409 乐观锁冲突自动刷新
- [ ] 启停有二次确认
- [ ] 算法与维度用标签展示
- **性能要求**：首屏 < 1.5s；搜索防抖 300ms。
- **可访问性**：表格键盘导航；表单字段 label 关联；对话框聚焦管理。
