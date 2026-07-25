# 通知中心 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：06-account 个人账号
- **页面类型**：列表页 + 批量操作
- **目标用户**：系统管理员（Admin）
- **核心目标**：查看当前管理员接收的站内通知（系统告警、待办提醒、公告），按已读/未读筛选，单条或全部标记已读，快速跳转关联业务页面。
- **访问入口**：Header 铃铛图标「查看全部」/ Sider「个人账号 → 通知中心」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部统计条 + 筛选 + 通知列表 + 批量操作。
- **关键区域**：
  - 区域 A（统计条）：2 个 `<a-statistic>` — 未读数 / 通知总数。
  - 区域 B（筛选条）：已读状态单选（全部/未读/已读）+ 通知类型多选（告警/待办/公告）+ 「全部标记已读」`IdempotencyButton`。
  - 区域 C（通知列表）：列表项含图标 + 标题 + 摘要 + 时间 + 已读标记（未读左侧蓝点）+ 操作（查看/标记已读），按时间倒序，分页 20。
  - 区域 D（详情弹窗）：`<a-modal>` 展示通知全字段 + 关联业务链接。
- **响应式断点**：≥1200px 列表单列全宽；992-1199px 同；<992px 不支持。
- **首屏内容**：未读通知列表（默认筛选 isRead=false）。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ 未读 8 │ 总数 42              [全部标记已读]    │
├────────────────────────────────────────────────┤
│ [全部] [未读] [已读]  [类型多选]                │
├────────────────────────────────────────────────┤
│ ● [告警] Payment 服务错误率超 1%    14:30 [查看]│
│ ● [待办] 3 条死信消息待处置         13:15 [查看]│
│   [公告] 系统将于 07-27 凌晨维护    10:00 [查看]│
└────────────────────────────────────────────────┘
 弹窗：通知全字段 + 关联业务链接
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/notifications` | 分页查询我的站内信（按已读状态） | Admin |
| GET | `/api/notifications/unread-count` | 获取未读计数 | Admin |
| POST | `/api/notifications/read` | 批量标记已读（按记录ID） | Admin |
| POST | `/api/notifications/read-all` | 全部标记已读 | Admin |

- **请求参数**：列表 `isRead?/page/pageSize`；标记已读 `MarkAsReadDto`（recordIds: Guid[]）。
- **响应字段**：`NotificationListResultDto`（items + total）；通知项含 `recordId`、`title`、`content`、`type`（告警/待办/公告）、`isRead`、`createdAt`、`relatedEntityType`、`relatedEntityId`；未读计数返回 `int`。
- **数据加载策略**：进入页面并行 GET 列表 + GET 未读数；筛选重新请求；标记已读后局部更新。
- **缓存策略**：未读数缓存至 Pinia `useUserStore`（Header 铃铛复用），每 60s 轮询刷新。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行 GET `/api/notifications?isRead=false&page=1&pageSize=20` + GET `/api/notifications/unread-count` → 统计条 + 列表渲染。
  2. 切换已读状态筛选 → 重新请求列表。
  3. 点击「标记已读」（单条） → POST `/api/notifications/read`（recordIds: [id]） → 该项蓝点消失 → 未读数 -1。
  4. 点击「全部标记已读」 → `ConfirmDialog` → POST `/api/notifications/read-all` → `message.success('已全部标记已读')` 1.5s → 列表刷新 + 未读数清零。
  5. 点击「查看」 → 弹窗展示通知全字段 + 若有 `relatedEntityId` 显示「查看详情」跳转按钮。
- **分支流程**：
  - 通知已读：「标记已读」按钮隐藏。
  - 无关联业务：弹窗不显示「查看详情」按钮。
  - 未读数轮询变化：Header 铃铛与统计条同步更新。
- **跨页面流转**：点击告警类通知「查看详情」跳 `/runtime-ops/alert-management`；待办类跳对应业务页（死信/索引重建/审计）；公告类跳 `/system-governance/announcements`。
- **状态机可视化**：通知状态 `StatusTag` — 未读蓝点、已读无标记；类型图标：告警红、待办黄、公告蓝。

## 5. 组件清单
- **基础组件**：`<a-statistic>`、`<a-list>`、`<a-list-item>`、`<a-radio-group>`、`<a-select mode="multiple">`、`<a-modal>`、`<a-badge>`、`<a-tag>`、`<a-button>`
- **业务组件**：
  - `IdempotencyButton`（见 shared/components.md §2）— 全部标记已读
  - `StatusTag`（见 shared/components.md §1）— 通知类型
  - `ConfirmDialog`（见 shared/components.md §10）— 全部标记已读确认
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`WarningOutlined`（告警红）、`ClockCircleOutlined`（待办黄）、`NotificationOutlined`（公告蓝）、`CheckOutlined`（标记已读）16px。
- **空状态**：「暂无通知」+ CTA「查看全部通知」（切换至全部筛选）。

## 6. 视觉规范
- **主色应用**：未读蓝点主色 `#1677FF`；全部标记已读按钮主色。
- **状态色**：告警 `#FF4D4F`、待办 `#FAAD14`、公告 `#1677FF`；未读蓝点、已读无标记。
- **间距**：统计条间距 24px；列表项间距 12px；列表项内边距 16px；弹窗内边距 24px。
- **字体**：标题 14px medium；摘要 12px `#595959`；时间 12px `#8C8C8C`。
- **图标尺寸**：类型图标 16px；操作图标 16px。

## 7. 异常处理与边界
- **加载态**：列表 `<a-skeleton>`（5 项占位）；统计条 `<a-skeleton>`。
- **空数据**：`EmptyState` 兜底，CTA「查看全部通知」。
- **错误态**：网络错误 `message.error` 3s；标记已读失败 `message.error('标记已读失败，请重试')` 3s。
- **权限控制**：页面级 `roles: ['Admin']`（仅当前用户自身通知）。
- **并发与乐观锁**：标记已读幂等（已读再标记返回成功不报错）；全部标记已读 `IdempotencyButton` 防抖。
- **危险操作确认**：全部标记已读 `ConfirmDialog` 内容「将当前所有未读通知标记为已读，标记后未读数清零。此操作不可撤销。是否继续？」确认按钮主色。

## 8. 验收要点
- [ ] 未读数与 Header 铃铛同步
- [ ] 未读通知左侧蓝点标记
- [ ] 单条与全部标记已读正确更新未读数
- [ ] 通知类型筛选正确
- [ ] 关联业务跳转正确
- **性能要求**：首屏 < 1s；未读数轮询 60s 不阻塞 UI；列表分页加载 < 500ms。
- **可访问性**：列表项键盘导航；未读蓝点有 aria-label「未读」；弹窗聚焦管理。
