# BC9 消息通知域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC9
- **中文名**：消息通知域
- **英文名**：Notification
- **涉及端**：buyer-app / operations / seller / system-admin（4 端均涉及）
- **涉及页面数**：9 页（来自 feature-list：buyer-app 12-notification 2 页 + buyer-app 13-profile/settings 1 页 + operations 07-notification-ops 4 页 + seller 08-account/notifications 1 页 + system-admin 06-account/notifications 1 页）
- **已实现 API 端点数**：26 个（来自源码 Controller 扫描，含 2 个内部接口、2 个回执回调、3 个死信管理）
- **差异统计**：缺失 0 / 闲置 7 / 路径不一致 0 / 能力不匹配 2

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| GET | /api/notifications | [NotificationsController.cs#L27](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs#L27) | 分页查询我的站内信 | Buyer,Seller,Operator,Admin |
| GET | /api/notifications/unread-count | [NotificationsController.cs#L42](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs#L42) | 获取未读计数 | Buyer,Seller,Operator,Admin |
| POST | /api/notifications/read | [NotificationsController.cs#L53](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs#L53) | 按 recordIds 批量标记已读 | Buyer,Seller,Operator,Admin |
| POST | /api/notifications/read-all | [NotificationsController.cs#L64](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs#L64) | 全部标记已读 | Buyer,Seller,Operator,Admin |
| GET | /api/users/me/notification-preferences | [NotificationPreferencesController.cs#L27](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationPreferencesController.cs#L27) | 查询我的通知偏好 | Buyer,Seller,Operator,Admin |
| PUT | /api/users/me/notification-preferences | [NotificationPreferencesController.cs#L38](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationPreferencesController.cs#L38) | 设置某事件渠道偏好 | Buyer,Seller,Operator,Admin |
| GET | /api/admin/notification-templates | [NotificationTemplatesController.cs#L79](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L79) | 分页查询模板列表 | Operator,Admin |
| POST | /api/admin/notification-templates | [NotificationTemplatesController.cs#L28](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L28) | 创建通知模板 | Operator,Admin |
| GET | /api/admin/notification-templates/{templateId:guid} | [NotificationTemplatesController.cs#L38](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L38) | 按标识查询模板详情 | Operator,Admin |
| PUT | /api/admin/notification-templates/{templateId:guid} | [NotificationTemplatesController.cs#L49](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L49) | 更新通知模板 | Operator,Admin |
| POST | /api/admin/notification-templates/{templateId:guid}/enable | [NotificationTemplatesController.cs#L59](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L59) | 启用模板 | Operator,Admin |
| POST | /api/admin/notification-templates/{templateId:guid}/disable | [NotificationTemplatesController.cs#L69](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L69) | 禁用模板 | Operator,Admin |
| POST | /api/admin/notification-templates/{templateId:guid}/preview | [NotificationTemplatesController.cs#L94](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L94) | 预览模板渲染结果 | Operator,Admin |
| GET | /api/notifications/records | [NotificationRecordsController.cs#L36](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L36) | 多维度分页查询通知记录 | Operator,Admin |
| GET | /api/notifications/records/{id:guid} | [NotificationRecordsController.cs#L57](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L57) | 获取通知记录详情 | Operator,Admin |
| GET | /api/notifications/records/by-business/{businessRef} | [NotificationRecordsController.cs#L73](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L73) | 按业务引用查询记录 | Operator,Admin |
| POST | /api/admin/notifications/records/{id:guid}/resend | [NotificationRecordsController.cs#L88](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L88) | 手工重发死信通知记录 | Operator,Admin |
| GET | /api/admin/notifications/statistics | [NotificationRecordsController.cs#L118](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRecordsController.cs#L118) | 获取送达率统计 | Operator,Admin |
| GET | /api/admin/notification-config | [NotificationConfigController.cs#L28](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationConfigController.cs#L28) | 获取指定渠道配置（脱敏） | Operator,Admin |
| PUT | /api/admin/notification-config | [NotificationConfigController.cs#L38](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationConfigController.cs#L38) | 更新指定渠道配置 | Operator,Admin |
| POST | /api/admin/notification-config/test | [NotificationConfigController.cs#L52](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationConfigController.cs#L52) | 测试发送验证配置 | Operator,Admin |
| GET | /api/admin/notification-rate-limits | [NotificationRateLimitsController.cs#L28](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRateLimitsController.cs#L28) | 获取指定渠道频率限制配置 | Operator,Admin |
| PUT | /api/admin/notification-rate-limits | [NotificationRateLimitsController.cs#L38](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationRateLimitsController.cs#L38) | 更新指定渠道频率限制配置 | Operator,Admin |
| GET | /api/admin/dead-letters | [DeadLetterController.cs#L27](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/DeadLetterController.cs#L27) | 分页查询死信列表 | Operator,Admin |
| POST | /api/admin/dead-letters/batch-resend | [DeadLetterController.cs#L40](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/DeadLetterController.cs#L40) | 批量重发死信通知 | Operator,Admin |
| POST | /api/admin/dead-letters/batch-discard | [DeadLetterController.cs#L51](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/DeadLetterController.cs#L51) | 批量丢弃死信通知 | Operator,Admin |
| POST | /api/notifications/callbacks/email | [NotificationCallbacksController.cs#L57](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L57) | 邮件渠道回执回调（HMAC 验签） | 无 JWT，签名验证 |
| POST | /api/notifications/callbacks/sms | [NotificationCallbacksController.cs#L80](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L80) | 短信渠道回执回调（HMAC 验签） | 无 JWT，签名验证 |
| POST | internal/v1/notifications/send（内部） | [NotificationSendController.cs#L39](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L39) | 内部服务间调用发送通知（当前路由） | InternalApiKey |
| POST | internal/notifications/send（内部） | [NotificationSendController.cs#L51](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L51) | 内部服务间调用发送通知（旧路由，2026-09-15 下线） | InternalApiKey |

> 来源：grep `src/Services/Notification/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/notifications | [buyer-app/12-notification/notifications.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/notifications.md) | 分页查询我的站内信 | ✅ | Buyer |
| GET | /api/notifications/unread-count | [buyer-app/12-notification/notifications.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/notifications.md) | 获取未读计数 | ✅ | Buyer |
| POST | /api/notifications/read | [buyer-app/12-notification/notifications.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/notifications.md) | 批量标记已读 | ✅ | Buyer |
| POST | /api/notifications/read-all | [buyer-app/12-notification/notifications.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/notifications.md) | 全部标记已读 | ✅ | Buyer |
| GET | /api/users/me/notification-preferences | [buyer-app/12-notification/preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/preferences.md) | 查询我的通知偏好 | ✅ | Buyer |
| PUT | /api/users/me/notification-preferences | [buyer-app/12-notification/preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/preferences.md) | 设置某事件渠道偏好 | ✅ | Buyer |
| GET | /api/users/me/notification-preferences | [buyer-app/13-profile/settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md) | 查询消息推送偏好（开关联动） | ✅ | Buyer |
| PUT | /api/users/me/notification-preferences | [buyer-app/13-profile/settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md) | 同步消息推送偏好到服务端 | ✅ | Buyer |
| GET | /api/admin/notification-templates | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 分页查询模板（按事件类型/渠道过滤） | ✅ | Operator,Admin |
| GET | /api/admin/notification-templates/{templateId} | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 查询模板详情 | ✅ | Operator,Admin |
| POST | /api/admin/notification-templates | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 创建模板 | ✅ | Operator,Admin |
| PUT | /api/admin/notification-templates/{templateId} | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 更新模板 | ✅ | Operator,Admin |
| POST | /api/admin/notification-templates/{templateId}/enable | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 启用模板 | ✅ | Operator,Admin |
| POST | /api/admin/notification-templates/{templateId}/disable | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 禁用模板 | ✅ | Operator,Admin |
| POST | /api/admin/notification-templates/{templateId}/preview | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | 预览模板渲染结果 | ✅ | Operator,Admin |
| GET | /api/notifications/records | [operations/07-notification-ops/records.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/records.md) | 多维度分页查询通知记录 | ✅ | Operator,Admin |
| GET | /api/notifications/records/{id} | [operations/07-notification-ops/records.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/records.md) | 获取通知记录详情 | ✅ | Operator,Admin |
| GET | /api/notifications/records/by-business/{businessRef} | [operations/07-notification-ops/records.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/records.md) | 按业务引用查询记录 | ✅ | Operator,Admin |
| POST | /api/admin/notifications/records/{id}/resend | [operations/07-notification-ops/records.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/records.md) | 手工重发死信通知记录 | ✅ | Operator,Admin |
| GET | /api/admin/notifications/statistics | [operations/07-notification-ops/records.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/records.md) | 获取送达率统计 | ✅ | Operator,Admin |
| GET | /api/admin/notification-config | [operations/07-notification-ops/config.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/config.md) | 获取指定渠道配置（脱敏） | ✅ | Operator,Admin |
| PUT | /api/admin/notification-config | [operations/07-notification-ops/config.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/config.md) | 更新指定渠道配置 | ✅ | Operator,Admin |
| POST | /api/admin/notification-config/test | [operations/07-notification-ops/config.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/config.md) | 测试发送验证配置 | ✅ | Operator,Admin |
| GET | /api/admin/notification-rate-limits | [operations/07-notification-ops/rate-limits.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/rate-limits.md) | 获取指定渠道频率限制配置 | ✅ | Operator,Admin |
| PUT | /api/admin/notification-rate-limits | [operations/07-notification-ops/rate-limits.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/rate-limits.md) | 更新指定渠道频率限制配置 | ✅ | Operator,Admin |
| GET | /api/notifications | [seller/08-account/notifications.md](file:///e:/Leno/docs/design-prompts/seller/08-account/notifications.md) | 分页查询站内信（按 isRead 过滤） | ✅ | Seller |
| GET | /api/notifications/unread-count | [seller/08-account/notifications.md](file:///e:/Leno/docs/design-prompts/seller/08-account/notifications.md) | 获取未读通知计数（Header 铃铛 Badge） | ✅ | Seller |
| POST | /api/notifications/read | [seller/08-account/notifications.md](file:///e:/Leno/docs/design-prompts/seller/08-account/notifications.md) | 按 recordIds 批量标记已读 | ✅ | Seller |
| POST | /api/notifications/read-all | [seller/08-account/notifications.md](file:///e:/Leno/docs/design-prompts/seller/08-account/notifications.md) | 全部标记已读 | ✅ | Seller |
| GET | /api/notifications | [system-admin/06-account/notifications.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/notifications.md) | 分页查询我的站内信（按已读状态） | ✅ | Admin |
| GET | /api/notifications/unread-count | [system-admin/06-account/notifications.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/notifications.md) | 获取未读计数 | ✅ | Admin |
| POST | /api/notifications/read | [system-admin/06-account/notifications.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/notifications.md) | 批量标记已读（按记录ID） | ✅ | Admin |
| POST | /api/notifications/read-all | [system-admin/06-account/notifications.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/notifications.md) | 全部标记已读 | ✅ | Admin |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 去重后期望端点共 23 个（buyer 6 + operations 19 - 跨页重复 + seller/system-admin 与 buyer 重复 4×2，共 23 个独立端点）

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> BC9 范围内 design-prompts 所有页面均为 ✅ 实现状态，无 🚧/➕ 标记端点，故无缺失项

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| POST（内部） | internal/v1/notifications/send | [NotificationSendController.cs#L39](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L39) | 内部服务间调用发送通知（当前路由） | 保留观察：内部接口，design-prompts 不直接引用属正常 |
| POST（内部） | internal/notifications/send | [NotificationSendController.cs#L51](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs#L51) | 内部服务间调用发送通知（旧路由，2026-09-15 下线） | 后端废弃：双路由期结束后删除旧路由 |
| POST | /api/notifications/callbacks/email | [NotificationCallbacksController.cs#L57](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L57) | 邮件渠道回执回调 | 保留观察：服务商回调端点，前端不调用属正常 |
| POST | /api/notifications/callbacks/sms | [NotificationCallbacksController.cs#L80](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationCallbacksController.cs#L80) | 短信渠道回执回调 | 保留观察：服务商回调端点，前端不调用属正常 |
| GET | /api/admin/dead-letters | [DeadLetterController.cs#L27](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/DeadLetterController.cs#L27) | 分页查询死信列表 | 设计稿补调用：operations/07-notification-ops 应补 dead-letters 子页面，与 records.md 死信筛选互补 |
| POST | /api/admin/dead-letters/batch-resend | [DeadLetterController.cs#L40](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/DeadLetterController.cs#L40) | 批量重发死信通知 | 设计稿补调用：operations/07-notification-ops/records.md 第 4 节提到「批量重发」但未列出端点，应补引用 |
| POST | /api/admin/dead-letters/batch-discard | [DeadLetterController.cs#L51](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/DeadLetterController.cs#L51) | 批量丢弃死信通知 | 设计稿补调用：operations/07-notification-ops 应补死信丢弃端点引用 |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> 其中内部接口与回执回调端点属合理闲置（前端不直接调用），死信管理 3 个端点建议补 design-prompts 页面引用

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> design-prompts 中所有路径与源码 URL 一致；源码路由约束（如 `{id:guid}`、`{templateId:guid}`）仅约束参数格式，不影响 URL 形态，不构成路径不一致
> 注：需求文档 09-消息通知集成.md 第 5 章期望 `POST /api/notifications/send`，实际实现为 `internal/v1/notifications/send` 与 `internal/notifications/send`；因 design-prompts 未引用此内部接口，不构成 design-prompts 层面路径不一致

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 列表查询支持按 type 筛选（订单/售后/评价/系统）+ isRead/page/pageSize | 仅支持 isRead/page/pageSize，无 type/templateCode 筛选参数 | 缺少通知类型筛选 query 参数 | [seller/08-account/notifications.md](file:///e:/Leno/docs/design-prompts/seller/08-account/notifications.md) | [NotificationsController.cs#L27](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs#L27) | 补 `type`/`templateCode` query 参数，应用服务与仓储查询同步扩展 |
| 列表查询支持按 type 筛选（告警/待办/公告）+ isRead/page/pageSize | 仅支持 isRead/page/pageSize，无 type 筛选参数 | 缺少通知类型筛选 query 参数 | [system-admin/06-account/notifications.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/notifications.md) | [NotificationsController.cs#L27](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationsController.cs#L27) | 补 `type` query 参数（与 seller 端 type 取值集不同，需统一类型字典或区分场景） |
| 模板列表支持按关键词/事件类型/渠道/状态筛选 | 仅支持 eventType/channel/page/pageSize，无 keyword 与 status 筛选参数 | 缺少关键词与状态筛选 query 参数 | [operations/07-notification-ops/templates.md](file:///e:/Leno/docs/design-prompts/operations/07-notification-ops/templates.md) | [NotificationTemplatesController.cs#L79](file:///e:/Leno/src/Services/Notification/Leno.Notification.Api/Controllers/NotificationTemplatesController.cs#L79) | 补 `keyword`/`status` query 参数，仓储 QueryAsync 同步扩展过滤条件 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> 共 3 项能力不匹配（涉及 3 个端点：GET /api/notifications 2 处、GET /api/admin/notification-templates 1 处），合并为 2 类差异条目（按端点归类）

## 5. 拆分过渡说明

本 BC 无拆分过渡。

> 仅 BC1 / BC6 / BC7 出现此节。其他 BC 写「本 BC 无拆分过渡」一句话。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | — | — | — | — |
| P1 | — | GET /api/admin/dead-letters、POST /api/admin/dead-letters/batch-resend、POST /api/admin/dead-letters/batch-discard（design-prompts 应补死信管理页面引用） | — | GET /api/notifications 补 type 筛选（影响 seller 与 system-admin 通知类型筛选体验）；GET /api/admin/notification-templates 补 keyword/status 筛选 |
| P2 | — | POST internal/v1/notifications/send、POST internal/notifications/send（旧路由 2026-09-15 下线）、POST /api/notifications/callbacks/email、POST /api/notifications/callbacks/sms（合理闲置） | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖

- **上游依赖**（本 BC 消费上游事件以触发通知发送）：
  - BC1 用户域：消费 `UserRegisteredEvent`（发送欢迎通知）、调用 `INotificationService.SendAsync` 发送验证码
  - BC4 订单与交易域：消费 `OrderCreatedEvent`、`OrderPaidEvent`、`OrderCancelledEvent`、`OrderShippedEvent`
  - BC5 促销域：消费 `SeckillOrderCreatedEvent`（秒杀成功通知）
  - BC6 评价与售后域：消费 `AfterSalesApprovedEvent`、`RefundCompletedEvent`
  - BC7 积分与会员域：消费 `PointsEarnedEvent`、`MemberLevelChangedEvent`、`PaidMemberSubscribedEvent`
  - BC8 支付集成域：消费 `PaymentFailedIntegrationEvent`
- **下游依赖**（本 BC 发布事件供下游消费）：
  - BC11 系统管理域：订阅 `NotificationSentEvent`、`NotificationFailedEvent`、`NotificationRetriedEvent`、`NotificationDeadLetteredEvent`，用于运营看板（如 operations 01-dashboard/notification-delivery）与告警；BC11 同时承载基础设施级 MQ 死信队列管理，与 BC9 业务级死信分层互补
- **集成事件订阅/发布清单**：
  - **订阅（入站）**：`UserRegisteredEvent`、`OrderCreatedEvent`、`OrderPaidEvent`、`OrderCancelledEvent`、`OrderShippedEvent`、`SeckillOrderCreatedEvent`、`PaymentFailedIntegrationEvent`、`AfterSalesApprovedEvent`、`RefundCompletedEvent`、`PointsEarnedEvent`、`MemberLevelChangedEvent`、`PaidMemberSubscribedEvent`
  - **发布（出站）**：`NotificationSentEvent`、`NotificationFailedEvent`、`NotificationRetriedEvent`、`NotificationDeadLetteredEvent`

## 8. 行动建议

- **立即修复**（P0 缺失/不一致）：本 BC 无 P0 级问题，所有 design-prompts ✅ 标记端点均已实现且路径一致，不阻塞交易闭环
- **短期补充**（P1 缺失/不匹配）：
  - **能力补齐**：`GET /api/notifications` 增加 `type`/`templateCode` query 筛选参数，同步扩展 `INotificationAppService.GetNotificationsAsync` 与 `INotificationRecordRepository.QueryAsync`，统一通知类型字典（合并 seller 的「订单/售后/评价/系统」与 system-admin 的「告警/待办/公告」两套分类，建议改为按 `templateCode` 前缀映射或引入 `category` 字段）
  - **能力补齐**：`GET /api/admin/notification-templates` 增加 `keyword`/`status` query 筛选参数，仓储 `NotificationTemplateQuery` 同步扩展过滤条件
  - **文档同步**：operations/07-notification-ops 应补充死信管理子页面（dead-letters.md），引用 `GET /api/admin/dead-letters`、`POST /api/admin/dead-letters/batch-resend`、`POST /api/admin/dead-letters/batch-discard` 三个端点；或在 records.md 第 3 节 API 表与第 4 节批量操作流程中补入端点引用
- **长期规划**（P2 闲置/废弃）：
  - **旧路由下线**：`POST internal/notifications/send` 标记 Obsolete，计划 2026-09-15 移除；监控调用方迁移进度，下线时间到达后删除 `SendLegacyAsync` 方法与 `LegacyRoute` 常量
  - **合理闲置保留观察**：内部发送接口 `internal/v1/notifications/send` 与回执回调 `/api/notifications/callbacks/{email,sms}` 属服务间/服务商接口，design-prompts 不引用属正常，保留观察即可
- **文档同步**（design-prompts API 引用对齐到源码）：
  - records.md 第 3 节 API 表补充 `GET /api/admin/dead-letters`、`POST /api/admin/dead-letters/batch-resend`、`POST /api/admin/dead-letters/batch-discard` 三个端点
  - seller/08-account/notifications.md 与 system-admin/06-account/notifications.md 的请求参数段补 `type` query 参数说明，与源码扩展后的能力对齐
  - operations/07-notification-ops/templates.md 请求参数段补 `keyword`/`status` query 参数说明
