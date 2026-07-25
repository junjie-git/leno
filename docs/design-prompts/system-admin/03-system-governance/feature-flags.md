# 功能开关 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：03-system-governance 系统治理
- **页面类型**：列表页 + 表单页（弹窗）+ 评估调试
- **目标用户**：系统管理员（Admin）
- **核心目标**：管理功能开关（FeatureFlag），新增/编辑/启停开关，在线评估某上下文下开关是否生效。
- **访问入口**：Sider「系统治理 → 功能开关」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 新建/编辑弹窗 + 评估调试抽屉。
- **关键区域**：
  - 区域 A（筛选条）：key 搜索 + 状态多选（Enabled/Disabled）+ 分组筛选。
  - 区域 B（主表格）：列含 key/描述/分组/状态/最近变更/操作（编辑/启用/停用/评估），分页 20。
  - 区域 C（弹窗表单）：`<a-modal width="560">` 含 key（新建时可编辑，编辑时只读）、描述、分组、规则配置（JSON 文本域 + 校验）、初始状态。
  - 区域 D（评估抽屉）：`<a-drawer width="480">` 输入上下文 JSON（userId/role/shopId 等） → POST evaluate → 显示布尔结果 + 命中规则。
- **响应式断点**：≥1200px 表格 7 列；992-1199px 隐藏「最近变更」。
- **首屏内容**：全部开关列表（按 key 字母序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [搜索 key] [状态多选] [分组 ▼] [新建开关]      │
├────────────────────────────────────────────────┤
│ key │ 描述 │ 分组 │ 状态 │ 最近变更 │ 操作      │
└────────────────────────────────────────────────┘
→ 弹窗：key/描述/分组/规则 JSON/状态
→ 抽屉：上下文 JSON → 评估结果
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/feature-flags` | 分页查询特性开关 | Admin,Operator |
| POST | `/api/admin/feature-flags` | 创建特性开关 | Admin,Operator |
| PUT | `/api/admin/feature-flags/{flagId}` | 更新特性开关（key 不可变） | Admin,Operator |
| POST | `/api/admin/feature-flags/{flagId}/enable` | 启用开关 | Admin,Operator |
| POST | `/api/admin/feature-flags/{flagId}/disable` | 停用开关 | Admin,Operator |
| POST | `/api/admin/feature-flags/evaluate` | 按上下文评估开关是否生效 | Admin,Operator |

- **请求参数**：列表 `key/status/page/pageSize`；创建 `SaveFeatureFlagDto`（key/description/group/ruleJson）；编辑 `UpdateFeatureFlagDto`；评估 `EvaluateFlagDto`（key/context）。
- **响应字段**：`FeatureFlagDto` 含 `FlagId`、`Key`、`Description`、`Group`、`Status`、`RuleJson`、`UpdatedAt`、`UpdatedBy`。
- **数据加载策略**：进入页面加载首页；筛选重新请求；评估按需调用。
- **缓存策略**：列表缓存 2 分钟（开关变更需快速反映）；评估不缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/feature-flags?page=1&pageSize=20` → 表格渲染。
  2. 点击「新建开关」 → 弹窗填表 + 规则 JSON 校验 → POST → `message.success('开关已创建')`。
  3. 点击「评估」 → 抽屉输入上下文 JSON → POST `/api/admin/feature-flags/evaluate` → 显示「生效/不生效」+ 命中规则。
  4. 点击「启用/停用」 → `ConfirmDialog` → POST enable/disable → 状态更新。
- **分支流程**：
  - 规则 JSON 格式错误：前端 `JSON.parse` 失败提示「规则 JSON 格式不正确」。
  - 评估时 key 不存在：后端 404，`message.error('开关不存在')` 3s。
- **跨页面流转**：点击「查看审计」跳 `/audit/audit-logs?resourceType=FeatureFlag&keyword={key}`。
- **状态机可视化**：Disabled → Enabled → Disabled，`StatusTag` 自定义 feature 类型。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-textarea>`、`<a-drawer>`、`<a-tag>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 开关状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 启停确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`PlayCircleOutlined`（评估）、`CheckOutlined`/`StopOutlined` 16px。
- **空状态**：「暂无功能开关」+ CTA「新建开关」。

## 6. 视觉规范
- **主色应用**：新建按钮主色；Enabled `<a-tag color="green">`；评估结果「生效」绿色、「不生效」灰色。
- **状态色**：Enabled 绿、Disabled 灰。
- **间距**：筛选条与表格 16px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；key 14px medium 等宽字体；规则 JSON 12px monospace。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；抽屉 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：JSON 解析错误前端拦截；404 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；写操作 `PermissionGuard permission="feature:write"`。
- **并发与乐观锁**：无乐观锁（开关低频变更）。
- **危险操作确认**：停用启用中的开关 `ConfirmDialog` 内容「停用后该功能对所有用户立即失效，可能影响线上行为。可随时启用恢复。」确认按钮默认样式。

## 8. 验收要点
- [ ] key 新建时可编辑、编辑时只读
- [ ] 规则 JSON 提交前格式校验
- [ ] 评估抽屉显示布尔结果与命中规则
- [ ] 启停有二次确认
- **性能要求**：首屏 < 1.5s；搜索防抖 300ms；评估响应 < 500ms。
- **可访问性**：表格键盘导航；JSON 文本域支持 Tab 缩进；对话框聚焦管理。
