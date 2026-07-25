# 数据字典 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：03-system-governance 系统治理
- **页面类型**：列表页 + 详情页（字典项管理）
- **目标用户**：系统管理员（Admin）、运营管理员（Operator）
- **核心目标**：管理数据字典（DataDictionary）与字典项，新建/编辑/启停字典，维护字典项的增删改。
- **访问入口**：Sider「系统治理 → 数据字典」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：左侧字典列表 + 右侧字典详情与字典项管理。
- **关键区域**：
  - 区域 A（左侧字典列表）：`<a-list>` 显示字典编码/名称/状态/项数，含搜索与「新增字典」按钮。
  - 区域 B（右侧详情）：上半区 `<a-descriptions>` 展示字典基本信息（编码/名称/描述/状态）+ 编辑/启用/停用按钮；下半区字典项管理。
  - 区域 C（字典项表格）：`<a-table>` 列含项编码/显示名/排序/状态/操作（编辑/移除），可新增项。
- **响应式断点**：≥1200px 左右 30%/70%；992-1199px 上下堆叠。
- **首屏内容**：首个字典详情 + 字典项列表。
- **线框图描述**：

```
┌──────────────┬───────────────────────────────┐
│ [搜索] [新增] │ 字典编码：order_status        │
│ ├ order_status│ 名称：订单状态               │
│ ├ payment_ch  │ 状态：启用 [编辑] [停用]      │
│ └ after_sales├───────────────────────────────┤
│              │ 字典项 [新增项]               │
│              │ 编码 │ 显示名 │ 排序 │ 操作   │
│              │ pending │ 待支付 │ 1   │ 编辑/移除 │
└──────────────┴───────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/dictionaries` | 分页查询数据字典 | Admin,Operator |
| POST | `/api/admin/dictionaries` | 创建数据字典 | Admin,Operator |
| PUT | `/api/admin/dictionaries/{dictionaryId}` | 更新数据字典（编码不可变） | Admin,Operator |
| POST | `/api/admin/dictionaries/{dictionaryId}/enable` | 启用字典 | Admin,Operator |
| POST | `/api/admin/dictionaries/{dictionaryId}/disable` | 停用字典 | Admin,Operator |
| POST | `/api/admin/dictionaries/{dictionaryId}/items` | 新增字典项 | Admin,Operator |
| PUT | `/api/admin/dictionaries/{dictionaryId}/items/{itemId}` | 更新字典项 | Admin,Operator |
| DELETE | `/api/admin/dictionaries/{dictionaryId}/items/{itemId}` | 移除字典项（幂等） | Admin,Operator |
| GET | `/api/dictionaries/{code}` | 按编码获取字典（公开查询） | Buyer,Seller,Operator,Admin |

- **请求参数**：列表 `name/status/page/pageSize`；创建 `SaveDataDictionaryDto`（code/name/description）；项操作 `AddDictionaryItemDto`/`UpdateDictionaryItemDto`（code/displayName/sortOrder）。
- **响应字段**：`DataDictionaryDto` 含 `DictionaryId`、`Code`、`Name`、`Description`、`Status`、`Items:[{ItemId,Code,DisplayName,SortOrder,Status}]`。
- **数据加载策略**：进入页面加载字典列表，默认选中首个 → 加载详情与项；切换字典重新加载。
- **缓存策略**：列表缓存 5 分钟；详情缓存 5 分钟（编辑后失效）；公开 `/api/dictionaries/{code}` 由后端缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/dictionaries` → 列表渲染，自动选中首个 → 详情加载。
  2. 点击「新增字典」 → 弹窗表单（编码/名称/描述） → POST → 列表新增项。
  3. 点击「编辑」 → 弹窗预填 → PUT → 详情刷新。
  4. 在字典项表格点击「新增项」 → 行内编辑（编码/显示名/排序） → POST items → 项列表刷新。
  5. 点击「移除」 → `ConfirmDialog` → DELETE items/{itemId} → 项移除。
- **分支流程**：
  - 编码已存在：后端 409，`message.error('字典编码已存在')` 3s。
  - 移除字典项被引用：后端返回 409，`message.error('该项被引用，无法移除')` 3s。
- **跨页面流转**：点击「查看引用」跳 `/audit/audit-logs?resourceType=Dictionary&keyword={code}`。
- **状态机可视化**：字典与项均 Disabled ↔ Enabled，`StatusTag` 自定义 dictionary 类型。

## 5. 组件清单
- **基础组件**：`<a-list>`、`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-input-number>`、`<a-descriptions>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）— 字典项表格
  - `StatusTag`（见 shared/components.md §1）— 字典与项状态
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 移除确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`DeleteOutlined`、`DatabaseOutlined` 16px。
- **空状态**：「暂无数据字典」+ CTA「新增字典」。

## 6. 视觉规范
- **主色应用**：新增按钮主色；选中字典项背景 `#E6F4FF`；编码用等宽字体。
- **状态色**：Enabled 绿、Disabled 灰。
- **间距**：左右分栏 24px；列表项高 48px；字典项表格行高 40px。
- **字体**：表格 14px；编码 14px medium monospace；显示名 14px。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：列表与详情均 `<a-skeleton>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：409 编码冲突或引用冲突 `message.error` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；写操作 `PermissionGuard permission="dictionary:write"`。
- **并发与乐观锁**：无乐观锁（字典低频变更）。
- **危险操作确认**：移除字典项 `ConfirmDialog` 内容「移除后该字典项将不再可用，已引用该项的业务需手动迁移。此操作幂等，重复请求无副作用。」确认按钮 danger 红色。

## 8. 验收要点
- [ ] 编码编辑时只读
- [ ] 字典项支持行内新增/编辑
- [ ] 移除字典项有二次确认
- [ ] 引用冲突 409 友好提示
- **性能要求**：首屏 < 1.5s；字典项 < 100 无需虚拟滚动。
- **可访问性**：列表键盘导航；表格行可编辑；对话框聚焦管理。
