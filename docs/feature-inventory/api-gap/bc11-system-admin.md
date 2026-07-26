# BC11 系统管理域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC11
- **中文名**：系统管理域
- **英文名**：SystemAdmin
- **涉及端**：system-admin（少量公开端点同时供 buyer-app / seller / operations 消费）
- **涉及页面数**：20 页（01-dashboard 7 页 + 03-system-governance 4 页 + 04-runtime-ops 6 页 + 05-audit 3 页，来自 feature-list）
- **已实现 API 端点数**：68 个（来自源码 Controller 扫描，其中 6 个属于 02-user-access/operators 页面，超出本次 20 页范围；本次 20 页对应端点 62 个）
- **差异统计**：缺失 12 / 闲置 0 / 路径不一致 0 / 能力不匹配 1

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| GET | /api/admin/system-configs | [SystemConfigsController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L28) | 分页查询系统配置（key/分组/状态） | Operator,Admin |
| GET | /api/admin/system-configs/groups | [SystemConfigsController.cs#L43](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L43) | 获取全部配置分组（去重） | Operator,Admin |
| POST | /api/admin/system-configs | [SystemConfigsController.cs#L52](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L52) | 创建系统配置 | Operator,Admin |
| PUT | /api/admin/system-configs/{configId} | [SystemConfigsController.cs#L61](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L61) | 更新系统配置（键不可变） | Operator,Admin |
| POST | /api/admin/system-configs/{configId}/enable | [SystemConfigsController.cs#L70](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L70) | 启用配置 | Operator,Admin |
| POST | /api/admin/system-configs/{configId}/disable | [SystemConfigsController.cs#L79](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L79) | 停用配置 | Operator,Admin |
| GET | /api/admin/system-configs/by-key/{key} | [SystemConfigsController.cs#L88](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs#L88) | 按键获取配置（加密值掩码） | Operator,Admin |
| GET | /api/admin/dashboard/overview | [DashboardController.cs#L41](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L41) | 运营总览（订单/GMV/转化率） | Operator,Admin |
| GET | /api/admin/dashboard/payment-stats | [DashboardController.cs#L56](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L56) | 支付成功率统计 | Operator,Admin |
| GET | /api/admin/dashboard/points-stats | [DashboardController.cs#L71](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L71) | 积分发放量统计 | Operator,Admin |
| GET | /api/admin/dashboard/notification-delivery | [DashboardController.cs#L86](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L86) | 通知送达率统计 | Operator,Admin |
| GET | /api/admin/dashboard/after-sales-stats | [DashboardController.cs#L101](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L101) | 售后统计 | Operator,Admin |
| GET | /api/admin/dashboard/shop-ranking | [DashboardController.cs#L116](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L116) | 店铺排行 TopN | Operator,Admin |
| GET | /api/admin/dashboard/reports | [DashboardController.cs#L131](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L131) | 报表快照列表（按类型与时间范围） | Operator,Admin |
| GET | /api/admin/dashboard/reports/{id} | [DashboardController.cs#L148](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L148) | 报表快照详情 | Operator,Admin |
| GET | /api/admin/health | [HealthController.cs#L30](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/HealthController.cs#L30) | 聚合健康状态（整体+各模块） | Operator,Admin |
| GET | /api/admin/health/modules | [HealthController.cs#L41](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/HealthController.cs#L41) | 各模块健康详情列表 | Operator,Admin |
| GET | /api/admin/statistics/reconciliation-status | [StatisticsController.cs#L42](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L42) | 最近一次对账状态 | Operator,Admin |
| POST | /api/admin/statistics/reconcile | [StatisticsController.cs#L76](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L76) | 手动触发对账 | Operator,Admin |
| GET | /api/admin/statistics/reconciliation-records | [StatisticsController.cs#L101](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/StatisticsController.cs#L101) | 对账记录列表 | Operator,Admin |
| GET | /api/admin/audit-logs | [AuditLogsController.cs#L34](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs#L34) | 分页查询审计日志 | Operator,Admin |
| GET | /api/admin/audit-logs/export | [AuditLogsController.cs#L50](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs#L50) | 导出审计日志 CSV | Operator,Admin |
| GET | /api/admin/operation-logs | [AuditLogsController.cs#L64](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs#L64) | 分页查询操作日志 | Operator,Admin |
| GET | /api/admin/audit-logs/{id} | [AuditLogsController.cs#L80](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs#L80) | 跨域审计日志条目详情 | Operator,Admin |
| GET | /api/admin/audit-log-entries | [AuditLogsController.cs#L95](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs#L95) | 分页查询跨域审计日志条目 | Operator,Admin |
| GET | /api/admin/scheduled-tasks | [ScheduledTasksController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs#L28) | 分页查询定时任务 | Operator,Admin |
| POST | /api/admin/scheduled-tasks | [ScheduledTasksController.cs#L42](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs#L42) | 创建定时任务（初始停用态） | Operator,Admin |
| PUT | /api/admin/scheduled-tasks/{taskId} | [ScheduledTasksController.cs#L51](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs#L51) | 更新定时任务 | Operator,Admin |
| POST | /api/admin/scheduled-tasks/{taskId}/enable | [ScheduledTasksController.cs#L60](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs#L60) | 启用任务并向调度器注册 | Operator,Admin |
| POST | /api/admin/scheduled-tasks/{taskId}/disable | [ScheduledTasksController.cs#L69](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs#L69) | 停用任务并从调度器注销 | Operator,Admin |
| POST | /api/admin/scheduled-tasks/{taskId}/run-now | [ScheduledTasksController.cs#L78](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs#L78) | 立即触发任务执行 | Operator,Admin |
| GET | /api/admin/feature-flags | [FeatureFlagsController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs#L28) | 分页查询特性开关 | Operator,Admin |
| POST | /api/admin/feature-flags | [FeatureFlagsController.cs#L42](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs#L42) | 创建特性开关 | Operator,Admin |
| PUT | /api/admin/feature-flags/{flagId} | [FeatureFlagsController.cs#L51](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs#L51) | 更新特性开关（key 不可变） | Operator,Admin |
| POST | /api/admin/feature-flags/{flagId}/enable | [FeatureFlagsController.cs#L60](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs#L60) | 启用开关 | Operator,Admin |
| POST | /api/admin/feature-flags/{flagId}/disable | [FeatureFlagsController.cs#L69](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs#L69) | 停用开关 | Operator,Admin |
| POST | /api/admin/feature-flags/evaluate | [FeatureFlagsController.cs#L78](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs#L78) | 按上下文评估开关 | Operator,Admin |
| GET | /api/admin/dictionaries | [DataDictionariesController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L28) | 分页查询数据字典 | Operator,Admin |
| POST | /api/admin/dictionaries | [DataDictionariesController.cs#L43](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L43) | 创建数据字典 | Operator,Admin |
| PUT | /api/admin/dictionaries/{dictionaryId} | [DataDictionariesController.cs#L53](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L53) | 更新数据字典（编码不可变） | Operator,Admin |
| POST | /api/admin/dictionaries/{dictionaryId}/enable | [DataDictionariesController.cs#L63](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L63) | 启用字典 | Operator,Admin |
| POST | /api/admin/dictionaries/{dictionaryId}/disable | [DataDictionariesController.cs#L73](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L73) | 停用字典 | Operator,Admin |
| POST | /api/admin/dictionaries/{dictionaryId}/items | [DataDictionariesController.cs#L83](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L83) | 新增字典项 | Operator,Admin |
| PUT | /api/admin/dictionaries/{dictionaryId}/items/{itemId} | [DataDictionariesController.cs#L93](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L93) | 更新字典项 | Operator,Admin |
| DELETE | /api/admin/dictionaries/{dictionaryId}/items/{itemId} | [DataDictionariesController.cs#L103](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L103) | 移除字典项（幂等） | Operator,Admin |
| GET | /api/dictionaries/{code} | [DataDictionariesController.cs#L113](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs#L113) | 按编码获取字典（公开） | Buyer,Seller,Operator,Admin |
| GET | /api/admin/announcements | [AnnouncementsController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs#L28) | 分页查询公告 | Operator,Admin |
| POST | /api/admin/announcements | [AnnouncementsController.cs#L43](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs#L43) | 创建公告（草稿态） | Operator,Admin |
| PUT | /api/admin/announcements/{announcementId} | [AnnouncementsController.cs#L53](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs#L53) | 更新公告（仅草稿态） | Operator,Admin |
| POST | /api/admin/announcements/{announcementId}/publish | [AnnouncementsController.cs#L63](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs#L63) | 发布公告 | Operator,Admin |
| POST | /api/admin/announcements/{announcementId}/unpublish | [AnnouncementsController.cs#L73](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs#L73) | 撤回公告 | Operator,Admin |
| GET | /api/announcements | [AnnouncementsController.cs#L83](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs#L83) | 公开查询当前有效公告 | Buyer,Seller,Operator,Admin |
| GET | /api/admin/rate-limit-rules | [RateLimitRulesController.cs#L27](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L27) | 分页查询限流规则 | Admin |
| POST | /api/admin/rate-limit-rules | [RateLimitRulesController.cs#L41](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L41) | 创建限流规则 | Admin |
| GET | /api/admin/rate-limit-rules/{id} | [RateLimitRulesController.cs#L51](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L51) | 限流规则详情 | Admin |
| PUT | /api/admin/rate-limit-rules/{id} | [RateLimitRulesController.cs#L66](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L66) | 更新限流规则（乐观并发） | Admin |
| POST | /api/admin/rate-limit-rules/{id}/enable | [RateLimitRulesController.cs#L88](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L88) | 启用限流规则 | Admin |
| POST | /api/admin/rate-limit-rules/{id}/disable | [RateLimitRulesController.cs#L105](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/RateLimitRulesController.cs#L105) | 停用限流规则 | Admin |
| GET | /api/admin/dead-letters | [DeadLetterController.cs#L29](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DeadLetterController.cs#L29) | 分页查询死信消息 | Operator,Admin |
| GET | /api/admin/dead-letters/{id} | [DeadLetterController.cs#L43](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DeadLetterController.cs#L43) | 死信消息详情 | Operator,Admin |
| POST | /api/admin/dead-letters/{id}/retry | [DeadLetterController.cs#L58](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DeadLetterController.cs#L58) | 重投死信消息（幂等） | Operator,Admin |
| POST | /api/admin/dead-letters/{id}/discard | [DeadLetterController.cs#L68](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DeadLetterController.cs#L68) | 丢弃死信消息（reason 必填） | Operator,Admin |
| POST | /api/admin/dead-letters/batch-retry | [DeadLetterController.cs#L80](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DeadLetterController.cs#L80) | 批量重投 | Operator,Admin |
| POST | /api/admin/dead-letters/batch-discard | [DeadLetterController.cs#L92](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DeadLetterController.cs#L92) | 批量丢弃 | Operator,Admin |
| GET | /api/admin/index-rebuild/tasks | [IndexRebuildController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/IndexRebuildController.cs#L28) | 分页查询索引重建任务 | Operator,Admin |
| POST | /api/admin/index-rebuild/trigger | [IndexRebuildController.cs#L42](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/IndexRebuildController.cs#L42) | 触发索引重建 | Operator,Admin |
| GET | /api/admin/index-rebuild/tasks/{id} | [IndexRebuildController.cs#L52](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/IndexRebuildController.cs#L52) | 任务详情/进度 | Operator,Admin |
| POST | /api/admin/index-rebuild/tasks/{id}/retry | [IndexRebuildController.cs#L66](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/IndexRebuildController.cs#L66) | 重试失败任务 | Operator,Admin |
| GET | /api/admin/operators | [OperatorsController.cs#L28](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs#L28) | 分页查询运营人员（超出本次 20 页范围，属 02-user-access/operators） | Operator,Admin |
| POST | /api/admin/operators | [OperatorsController.cs#L43](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs#L43) | 创建运营人员（超出本次 20 页范围） | 已认证用户 |
| PUT | /api/admin/operators/{operatorId}/permissions | [OperatorsController.cs#L53](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs#L53) | 更新运营人员权限（超出本次 20 页范围） | 已认证用户 |
| POST | /api/admin/operators/{operatorId}/activate | [OperatorsController.cs#L63](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs#L63) | 启用运营人员（超出本次 20 页范围） | 已认证用户 |
| POST | /api/admin/operators/{operatorId}/deactivate | [OperatorsController.cs#L73](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs#L73) | 停用运营人员（超出本次 20 页范围） | 已认证用户 |
| GET | /api/admin/operators/{operatorId} | [OperatorsController.cs#L83](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs#L83) | 运营人员详情（超出本次 20 页范围） | 已认证用户 |

> 来源：grep `src/Services/SystemAdmin/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」——本 BC 无 Internal*Controller.cs 文件
> OperatorsController 端点属于 BC11 但不在本次扫描的 20 页（01/03/04/05 模块）范围内，对应 design-prompts 页面 `02-user-access/operators.md`，不计入下方差异统计

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/admin/dashboard/overview | [operations-overview.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/operations-overview.md) | 查询运营总览 | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/payment-stats | [payment-stats.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/payment-stats.md) | 查询支付成功率统计 | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/points-stats | [points-stats.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/points-stats.md) | 查询积分发放量统计 | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/notification-delivery | [notification-delivery.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/notification-delivery.md) | 查询通知送达率统计 | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/after-sales-stats | [after-sales-stats.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/after-sales-stats.md) | 查询售后统计 | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/shop-ranking | [shop-ranking.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/shop-ranking.md) | 查询店铺排行 TopN | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/reports | [report-snapshots.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/report-snapshots.md) | 查询报表快照列表 | ✅ | Admin,Operator |
| GET | /api/admin/dashboard/reports/{id} | [report-snapshots.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/report-snapshots.md) | 查询报表快照详情 | ✅ | Admin,Operator |
| GET | /api/admin/announcements | [announcements.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/announcements.md) | 分页查询公告 | ✅ | Admin,Operator |
| POST | /api/admin/announcements | [announcements.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/announcements.md) | 创建公告 | ✅ | Admin,Operator |
| PUT | /api/admin/announcements/{announcementId} | [announcements.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/announcements.md) | 更新公告 | ✅ | Admin,Operator |
| POST | /api/admin/announcements/{announcementId}/publish | [announcements.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/announcements.md) | 发布公告 | ✅ | Admin,Operator |
| POST | /api/admin/announcements/{announcementId}/unpublish | [announcements.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/announcements.md) | 撤回公告 | ✅ | Admin,Operator |
| GET | /api/announcements | [announcements.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/announcements.md) | 公开查询当前有效公告 | ✅ | Buyer,Seller,Operator,Admin |
| GET | /api/admin/dictionaries | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 分页查询数据字典 | ✅ | Admin,Operator |
| POST | /api/admin/dictionaries | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 创建数据字典 | ✅ | Admin,Operator |
| PUT | /api/admin/dictionaries/{dictionaryId} | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 更新数据字典 | ✅ | Admin,Operator |
| POST | /api/admin/dictionaries/{dictionaryId}/enable | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 启用字典 | ✅ | Admin,Operator |
| POST | /api/admin/dictionaries/{dictionaryId}/disable | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 停用字典 | ✅ | Admin,Operator |
| POST | /api/admin/dictionaries/{dictionaryId}/items | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 新增字典项 | ✅ | Admin,Operator |
| PUT | /api/admin/dictionaries/{dictionaryId}/items/{itemId} | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 更新字典项 | ✅ | Admin,Operator |
| DELETE | /api/admin/dictionaries/{dictionaryId}/items/{itemId} | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 移除字典项 | ✅ | Admin,Operator |
| GET | /api/dictionaries/{code} | [data-dictionaries.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md) | 按编码获取字典（公开） | ✅ | Buyer,Seller,Operator,Admin |
| GET | /api/admin/feature-flags | [feature-flags.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/feature-flags.md) | 分页查询特性开关 | ✅ | Admin,Operator |
| POST | /api/admin/feature-flags | [feature-flags.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/feature-flags.md) | 创建特性开关 | ✅ | Admin,Operator |
| PUT | /api/admin/feature-flags/{flagId} | [feature-flags.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/feature-flags.md) | 更新特性开关 | ✅ | Admin,Operator |
| POST | /api/admin/feature-flags/{flagId}/enable | [feature-flags.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/feature-flags.md) | 启用开关 | ✅ | Admin,Operator |
| POST | /api/admin/feature-flags/{flagId}/disable | [feature-flags.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/feature-flags.md) | 停用开关 | ✅ | Admin,Operator |
| POST | /api/admin/feature-flags/evaluate | [feature-flags.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/feature-flags.md) | 按上下文评估开关 | ✅ | Admin,Operator |
| GET | /api/admin/system-configs | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 分页查询系统配置 | ✅ | Admin,Operator |
| GET | /api/admin/system-configs/groups | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 获取全部配置分组 | ✅ | Admin,Operator |
| GET | /api/admin/system-configs/by-key/{key} | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 按键获取配置 | ✅ | Admin,Operator |
| POST | /api/admin/system-configs | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 创建系统配置 | ✅ | Admin,Operator |
| PUT | /api/admin/system-configs/{configId} | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 更新系统配置 | ✅ | Admin,Operator |
| POST | /api/admin/system-configs/{configId}/enable | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 启用配置 | ✅ | Admin,Operator |
| POST | /api/admin/system-configs/{configId}/disable | [system-configs.md](file:///e:/Leno/docs/design-prompts/system-admin/03-system-governance/system-configs.md) | 停用配置 | ✅ | Admin,Operator |
| GET | /api/admin/alerts | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 分页查询告警事件 | 🚧 | Admin |
| GET | /api/admin/alerts/{id} | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 获取告警详情 | 🚧 | Admin |
| POST | /api/admin/alerts/{id}/acknowledge | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 确认告警 | 🚧 | Admin |
| POST | /api/admin/alerts/silences | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 创建静默规则 | 🚧 | Admin |
| GET | /api/admin/alerts/silences | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 查询静默规则列表 | 🚧 | Admin |
| DELETE | /api/admin/alerts/silences/{id} | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 删除静默规则 | 🚧 | Admin |
| GET | /api/admin/dead-letters | [dead-letter-queue.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md) | 分页查询死信消息 | ✅ | Admin,Operator |
| GET | /api/admin/dead-letters/{id} | [dead-letter-queue.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md) | 获取死信消息详情 | ✅ | Admin,Operator |
| POST | /api/admin/dead-letters/{id}/retry | [dead-letter-queue.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md) | 重投死信消息 | ✅ | Admin,Operator |
| POST | /api/admin/dead-letters/{id}/discard | [dead-letter-queue.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md) | 丢弃死信消息 | ✅ | Admin,Operator |
| POST | /api/admin/dead-letters/batch-retry | [dead-letter-queue.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md) | 批量重投死信消息 | ✅ | Admin,Operator |
| POST | /api/admin/dead-letters/batch-discard | [dead-letter-queue.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md) | 批量丢弃死信消息 | ✅ | Admin,Operator |
| GET | /api/admin/health | [health-monitoring.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/health-monitoring.md) | 获取聚合健康状态 | ✅ | Admin,Operator |
| GET | /api/admin/health/modules | [health-monitoring.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/health-monitoring.md) | 获取各模块健康详情 | ✅ | Admin,Operator |
| GET | /api/admin/index-rebuild/tasks | [index-rebuild.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/index-rebuild.md) | 分页查询索引重建任务 | ✅ | Admin,Operator |
| POST | /api/admin/index-rebuild/trigger | [index-rebuild.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/index-rebuild.md) | 触发索引重建 | ✅ | Admin,Operator |
| GET | /api/admin/index-rebuild/tasks/{id} | [index-rebuild.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/index-rebuild.md) | 获取任务详情/进度 | ✅ | Admin,Operator |
| POST | /api/admin/index-rebuild/tasks/{id}/retry | [index-rebuild.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/index-rebuild.md) | 重试失败任务 | ✅ | Admin,Operator |
| GET | /api/admin/rate-limit-rules | [rate-limit-rules.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md) | 分页查询限流规则 | ✅ | Admin |
| GET | /api/admin/rate-limit-rules/{id} | [rate-limit-rules.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md) | 限流规则详情 | ✅ | Admin |
| POST | /api/admin/rate-limit-rules | [rate-limit-rules.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md) | 创建限流规则 | ✅ | Admin |
| PUT | /api/admin/rate-limit-rules/{id} | [rate-limit-rules.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md) | 更新限流规则 | ✅ | Admin |
| POST | /api/admin/rate-limit-rules/{id}/enable | [rate-limit-rules.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md) | 启用限流规则 | ✅ | Admin |
| POST | /api/admin/rate-limit-rules/{id}/disable | [rate-limit-rules.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md) | 停用限流规则 | ✅ | Admin |
| GET | /api/admin/scheduled-tasks | [scheduled-tasks.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md) | 分页查询定时任务 | ✅ | Admin,Operator |
| POST | /api/admin/scheduled-tasks | [scheduled-tasks.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md) | 创建定时任务 | ✅ | Admin,Operator |
| PUT | /api/admin/scheduled-tasks/{taskId} | [scheduled-tasks.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md) | 更新定时任务 | ✅ | Admin,Operator |
| POST | /api/admin/scheduled-tasks/{taskId}/enable | [scheduled-tasks.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md) | 启用任务 | ✅ | Admin,Operator |
| POST | /api/admin/scheduled-tasks/{taskId}/disable | [scheduled-tasks.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md) | 停用任务 | ✅ | Admin,Operator |
| POST | /api/admin/scheduled-tasks/{taskId}/run-now | [scheduled-tasks.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md) | 立即触发任务执行 | ✅ | Admin,Operator |
| GET | /api/admin/audit-logs | [audit-logs.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/audit-logs.md) | 分页查询审计日志 | ✅ | Admin,Operator |
| GET | /api/admin/audit-logs/{id} | [audit-logs.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/audit-logs.md) | 获取审计日志条目详情 | ✅ | Admin,Operator |
| GET | /api/admin/audit-logs/export | [audit-logs.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/audit-logs.md) | 导出审计日志 CSV | ✅ | Admin,Operator |
| GET | /api/admin/operation-logs | [audit-logs.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/audit-logs.md) | 分页查询操作日志 | ✅ | Admin,Operator |
| GET | /api/admin/audit-log-entries | [audit-logs.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/audit-logs.md) | 分页查询跨域审计条目 | ✅ | Admin,Operator |
| GET | /api/admin/outbox/summary | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 获取各域 Outbox 积压汇总 | 🚧 | Admin |
| GET | /api/admin/outbox/trend | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 获取近 N 小时积压趋势 | 🚧 | Admin |
| GET | /api/admin/outbox/{context}/messages | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 分页查询指定域积压事件 | 🚧 | Admin |
| POST | /api/admin/outbox/{context}/republish | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 批量重投指定域积压事件 | 🚧 | Admin |
| POST | /api/admin/outbox/{context}/archive | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 归档指定域陈旧积压事件 | 🚧 | Admin |
| GET | /api/admin/outbox/{context}/archive-history | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 查询归档历史 | 🚧 | Admin |
| GET | /api/admin/statistics/reconciliation-status | [reconciliation.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/reconciliation.md) | 获取最近一次对账状态 | ✅ | Admin,Operator |
| POST | /api/admin/statistics/reconcile | [reconciliation.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/reconciliation.md) | 手动触发对账 | ✅ | Admin,Operator |
| GET | /api/admin/statistics/reconciliation-records | [reconciliation.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/reconciliation.md) | 获取对账记录列表 | ✅ | Admin,Operator |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 期望端点总数：74（已实现 62 + 规划中 12）

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| GET | /api/admin/alerts | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 分页查询告警事件（module/severity/status/start/end 筛选） | P1 | 新增 AlertsController + Alertmanager 集成 |
| GET | /api/admin/alerts/{id} | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 获取告警详情（标签/注释/关联指标） | P1 | 新增 AlertsController GetByIdAsync |
| POST | /api/admin/alerts/{id}/acknowledge | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 确认告警（带 comment） | P1 | 新增 AlertsController AcknowledgeAsync |
| POST | /api/admin/alerts/silences | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 创建静默规则（matchers/duration/reason） | P1 | 新增 AlertSilencesController CreateAsync |
| GET | /api/admin/alerts/silences | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 查询静默规则列表 | P1 | 新增 AlertSilencesController QueryAsync |
| DELETE | /api/admin/alerts/silences/{id} | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md) | 删除静默规则 | P1 | 新增 AlertSilencesController DeleteAsync |
| GET | /api/admin/outbox/summary | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 获取各域 Outbox 积压汇总 | P1 | 新增 OutboxMonitorController SummaryAsync |
| GET | /api/admin/outbox/trend | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 获取近 N 小时积压趋势 | P1 | 新增 OutboxMonitorController TrendAsync |
| GET | /api/admin/outbox/{context}/messages | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 分页查询指定域积压事件详情 | P1 | 新增 OutboxMonitorController MessagesAsync |
| POST | /api/admin/outbox/{context}/republish | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 批量重投指定域积压事件 | P1 | 新增 OutboxMonitorController RepublishAsync |
| POST | /api/admin/outbox/{context}/archive | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 归档指定域陈旧积压事件 | P1 | 新增 OutboxMonitorController ArchiveAsync |
| GET | /api/admin/outbox/{context}/archive-history | [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 查询归档历史 | P1 | 新增 OutboxMonitorController ArchiveHistoryAsync |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> 12 个缺失端点均属 design-prompts 已标 🚧 规划中页面（alert-management、outbox-monitor），需后端按规划补建

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| - | - | - | - | - |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> 本 BC 在本次 20 页范围内未发现闲置端点。OperatorsController 端点虽不在 20 页内，但对应 `02-user-access/operators.md` 页面引用，不算闲置

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| - | - | - | - | - |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> 本 BC 在本次 20 页范围内未发现路径或方法不一致

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 报表快照列表支持 CSV 导出 | 仅返回 JSON `List<DashboardReportDto>` | 缺少 CSV 导出能力（design-prompts 验收要点期望「导出 CSV 调用 /api/admin/dashboard/reports 并下载」） | [report-snapshots.md](file:///e:/Leno/docs/design-prompts/system-admin/01-dashboard/report-snapshots.md) | [DashboardController.cs#L131](file:///e:/Leno/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DashboardController.cs#L131) | 新增 `GET /api/admin/dashboard/reports/export` 端点或在现有端点加 `format=csv` query 参数 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> 审计日志已有 `/api/admin/audit-logs/export` 实现 CSV 导出，但报表快照列表未提供等价能力

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | - | - | - | - |
| P1 | GET /api/admin/alerts；GET /api/admin/alerts/{id}；POST /api/admin/alerts/{id}/acknowledge；POST /api/admin/alerts/silences；GET /api/admin/alerts/silences；DELETE /api/admin/alerts/silences/{id}；GET /api/admin/outbox/summary；GET /api/admin/outbox/trend；GET /api/admin/outbox/{context}/messages；POST /api/admin/outbox/{context}/republish；POST /api/admin/outbox/{context}/archive；GET /api/admin/outbox/{context}/archive-history | - | - | - |
| P2 | - | - | - | 报表快照列表 CSV 导出能力缺失（GET /api/admin/dashboard/reports） |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强
> 告警与 Outbox 监控缺失会影响运维一致性监控与故障定位能力，但不直接阻塞交易闭环

## 7. 跨 BC 依赖

- **上游依赖**：本 BC 以只读消费方式订阅各域集成事件做统计聚合，不回写各域写库
  - BC4 订单与交易域：订阅 `OrderCreatedEvent` / `OrderPaidEvent` / `OrderCancelledEvent`，统计订单量、GMV、转化率、店铺排行
  - BC8 支付集成域：订阅 `PaymentSucceededIntegrationEvent` / `PaymentFailedIntegrationEvent`，统计支付成功率
  - BC9 通知域：消费通知送达事件，统计通知送达率
  - 积分域：消费 `PointsEarnedEvent`，统计积分发放量
  - 售后域：消费售后事件，统计售后量与退款金额
  - 卖家与店铺管理域：消费 `ShopCreatedEvent` 与订单事件聚合店铺排行
  - BC1 用户与认证授权域：BC1 已持有账户审计日志，本域聚合跨域视角（`AuditLogEntry` 为只读投影）；限流规则下发至各角色网关
  - 各模块 /health 端点：本域聚合各模块健康状态
- **下游依赖**：
  - 各域网关：订阅本域发布的 `RateLimitRuleUpdatedEvent` 热加载限流规则
  - 各域 MQ 死信队列：本域经 `IDeadLetterQueueManager` 拉取并汇聚管理（重投/丢弃经基础设施抽象调用各域 MQ）
  - 各域 ES 读库：本域触发全量索引重建并跟踪进度
- **集成事件订阅/发布清单**：
  - 订阅（入站）：`OrderCreatedEvent`、`OrderPaidEvent`、`OrderCancelledEvent`、`PaymentSucceededIntegrationEvent`、`PaymentFailedIntegrationEvent`、`PointsEarnedEvent`、`ShopCreatedEvent`、各域审计事件
  - 发布（出站）：`RateLimitRuleUpdatedEvent`（限流规则变更）、索引重建相关事件、死信处置相关事件
  - 事件命名统一过去时；聚合保存与事件记录在同一事务写入（发件箱模式），后台进程轮询发件箱表发布到消息队列，消费失败进入死信队列重试

## 8. 行动建议

- **立即修复**（P0 缺失/不一致）
  - 无 P0 项；本 BC 不阻塞交易闭环，可按规划节奏推进
- **短期补充**（P1 缺失/不匹配）
  - 新建 `AlertsController` + `AlertSilencesController`：实现 6 个告警管理端点，对接 Alertmanager（design-prompts 已标 🚧 规划中），保留 30s 轮询与静默规则匹配器
  - 新建 `OutboxMonitorController`：实现 6 个 Outbox 监控端点，跨域聚合发件箱积压（design-prompts 已标 🚧 规划中），保留 60s 轮询、重投幂等与归档不可逆约束
  - 上述两组端点落地前，前端按 design-prompts §7 异常处理约定显示「功能规划中」提示，避免线上 404 体验断层
- **长期规划**（P2 闲置/废弃）
  - 为 `GET /api/admin/dashboard/reports` 增加 CSV 导出能力（新增 `GET /api/admin/dashboard/reports/export` 或加 `format=csv` 参数），与审计日志导出能力对齐
  - OperatorsController 端点鉴权统一为 `Operator,Admin`，与同 BC 其他 Controller 风格一致（当前 CreateAsync/UpdatePermissionsAsync 等仅 `[Authorize]` 无角色限制，存在越权风险，建议下次安全评审时一并收紧）
- **文档同步**（design-prompts API 引用对齐到源码）
  - alert-management.md 与 outbox-monitor.md 中端点状态从 🚧 同步为 ✅ 后再发布
  - report-snapshots.md §8 验收要点「导出 CSV 调用 `/api/admin/dashboard/reports`」需在后端补能力后保留，否则应改为「导出 CSV 调用 `/api/admin/dashboard/reports/export`」
