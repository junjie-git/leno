# 公告管理 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：03-system-governance 系统治理
- **页面类型**：列表页 + 表单页（弹窗）
- **目标用户**：系统管理员（Admin）、运营管理员（Operator）
- **核心目标**：管理平台公告，新建/编辑/发布/撤回公告，控制公告在买家 APP/卖家后台/运营后台的展示。
- **访问入口**：Sider「系统治理 → 公告管理」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部筛选 + 主表格 + 新建/编辑弹窗 + 富文本编辑器。
- **关键区域**：
  - 区域 A（筛选条）：类型筛选（系统维护/活动通知/政策变更/紧急公告）+ 状态筛选（草稿/已发布/已撤回）+ 「新增公告」按钮。
  - 区域 B（主表格）：列含标题/类型/状态/发布范围/生效起止/操作（编辑/发布/撤回/查看），分页 20。
  - 区域 C（弹窗表单）：`<a-modal width="800">` 含标题/类型/发布范围（多选：买家/卖家/运营）/生效起止 `DateTimeRangePicker`/正文（富文本）/置顶开关。
- **响应式断点**：≥1200px 表格 7 列；992-1199px 隐藏「生效起止」。
- **首屏内容**：全部公告列表（按创建时间倒序）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ [类型 ▼] [状态 ▼] [新增公告]                   │
├────────────────────────────────────────────────┤
│ 标题 │ 类型 │ 状态 │ 范围 │ 生效起止 │ 操作    │
│ 系统维护通知│系统维护│已发布│全员│07-26~07-27│撤回│
└────────────────────────────────────────────────┘
→ 弹窗：标题/类型/范围/起止/富文本正文/置顶
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/announcements` | 分页查询公告 | Admin,Operator |
| POST | `/api/admin/announcements` | 创建公告（初始草稿态） | Admin,Operator |
| PUT | `/api/admin/announcements/{announcementId}` | 更新公告（仅草稿态可更新） | Admin,Operator |
| POST | `/api/admin/announcements/{announcementId}/publish` | 发布公告并发布集成事件 | Admin,Operator |
| POST | `/api/admin/announcements/{announcementId}/unpublish` | 撤回公告（仅已发布态可撤回） | Admin,Operator |
| GET | `/api/announcements` | 分页查询当前有效公告（公开） | Buyer,Seller,Operator,Admin |

- **请求参数**：列表 `type/status/page/pageSize`；创建/编辑 `SaveAnnouncementDto`（title/type/audiences/effectiveFrom/effectiveTo/content/isPinned）。
- **响应字段**：`AnnouncementDto` 含 `AnnouncementId`、`Title`、`Type`、`Status`、`Audiences`、`EffectiveFrom`、`EffectiveTo`、`Content`、`IsPinned`、`CreatedAt`、`PublishedAt`。
- **数据加载策略**：进入页面加载首页；筛选重新请求。
- **缓存策略**：列表不缓存（状态变更需即时反映）；公开 `/api/announcements` 由后端缓存。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → GET `/api/admin/announcements?page=1&pageSize=20` → 表格渲染。
  2. 点击「新增公告」 → 弹窗填表（含富文本） → POST → `message.success('公告已创建（草稿态）')`。
  3. 点击「编辑」（仅草稿态可编辑） → 弹窗预填 → PUT → 刷新。
  4. 点击「发布」 → `ConfirmDialog` → POST publish → 状态变为已发布 → `message.success('公告已发布')`。
  5. 点击「撤回」（仅已发布态可撤回） → `ConfirmDialog` → POST unpublish → 状态变为已撤回。
- **分支流程**：
  - 编辑已发布公告：按钮 disabled，Tooltip「仅草稿态可编辑」。
  - 撤回非已发布公告：按钮 disabled。
  - 生效时间冲突（EffectiveFrom ≥ EffectiveTo）：前端校验拦截。
- **跨页面流转**：点击「查看公开页」打开新窗口 `/api/announcements` 预览。
- **状态机可视化**：草稿 → 已发布 → 已撤回，`StatusTag` 自定义 announcement 类型。草稿色 `#8C8C8C`、已发布 `#52C41A`、已撤回 `#FAAD14`。

## 5. 组件清单
- **基础组件**：`<a-table>`、`<a-modal>`、`<a-form>`、`<a-input>`、`<a-select mode="multiple">`、`<a-switch>`、`<a-range-picker showTime>`
- **业务组件**：
  - `DataTable`（见 shared/components.md §6）
  - `StatusTag`（见 shared/components.md §1）— 公告状态
  - `DateTimeRangePicker`（见 shared/components.md §4）— 生效起止
  - `IdempotencyButton`（见 shared/components.md §2）
  - `ConfirmDialog`（见 shared/components.md §10）— 发布/撤回确认
  - `PermissionGuard`（见 shared/components.md §3）
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`PlusOutlined`、`EditOutlined`、`SendOutlined`（发布）、`RollbackOutlined`（撤回）、`NotificationOutlined` 16px。
- **空状态**：「暂无公告」+ CTA「新增公告」。

## 6. 视觉规范
- **主色应用**：新增按钮主色；发布按钮主色；置顶 `<a-tag color="red">置顶</a-tag>`。
- **状态色**：草稿灰、已发布绿、已撤回黄。
- **间距**：筛选条与表格 16px；表格行高 48px；弹窗内边距 24px。
- **字体**：表格 14px；标题 14px medium；正文富文本 14px。
- **图标尺寸**：操作图标 16px。

## 7. 异常处理与边界
- **加载态**：表格 `<a-skeleton>`；弹窗 `<a-spin>`。
- **空数据**：`EmptyState` 兜底。
- **错误态**：编辑非草稿态后端 400 `message.error('仅草稿态可编辑')` 3s。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；写操作 `PermissionGuard permission="announcement:write"`；发布 `permission="announcement:publish"`（仅 Admin）。
- **并发与乐观锁**：无乐观锁（公告低频变更）。
- **危险操作确认**：
  - 发布 `ConfirmDialog` 内容「发布后公告将对所选范围立即生效，买家 APP 与卖家后台将展示。撤回可恢复。」确认按钮主色。
  - 撤回 `ConfirmDialog` 内容「撤回后公告将立即从所有端下线，已读记录保留。可重新编辑后再次发布。」确认按钮 danger 红色。

## 8. 验收要点
- [ ] 仅草稿态可编辑，其他状态编辑按钮 disabled
- [ ] 发布范围多选生效
- [ ] 富文本编辑器支持基础排版与图片上传
- [ ] 发布与撤回有二次确认
- **性能要求**：首屏 < 1.5s；富文本编辑器加载 < 1s；图片上传 < 3s。
- **可访问性**：表格键盘导航；富文本编辑器支持键盘操作；对话框聚焦管理。
