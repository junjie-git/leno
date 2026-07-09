# 系统管理域 (System Admin) 开发任务

> **限界上下文**: BC11 系统管理域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis / Serilog  
> **依赖**: `shared-kernel`  
> **对应文档**: `12-系统管理域.md`

---

## 模块概述

系统管理域为运营团队提供平台级管理能力，包括运营人员管理、系统配置、审计日志查询、操作日志、数据字典、系统公告、定时任务管理与功能开关。审计日志由各域写入（通过共享内核审计拦截器），本域负责聚合查询与导出。

---

## Task 1: 项目初始化与领域层 — Operator 聚合

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Leno.SystemAdmin.Domain.csproj`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/Operator.cs`

- [ ] 创建 Leno.SystemAdmin.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `Operator` 聚合根（OperatorId、UserId、DisplayName、Role、Permissions、Status、LastLoginAt、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `Operator.Create` 工厂方法（关联用户域 UserId，分配运营角色与权限）
- [ ] 实现 `Operator.AssignPermissions`/`Operator.RevokePermissions` 方法
- [ ] 实现 `Operator.Activate`/`Operator.Deactivate` 状态流转
- [ ] 实现 `Operator.RecordLogin(loginAt)`（记录最后登录时间）
- [ ] 定义 `OperatorRole`（SuperAdmin/Admin/Operator）、`OperatorStatus`（Active/Inactive）
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add Operator aggregate root`

---

## Task 2: 领域层 — 系统配置与数据字典聚合

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/SystemConfig.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/DataDictionary.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/DictionaryItem.cs`

- [ ] 实现 `SystemConfig` 聚合根（ConfigId、Key、Value、Group、Description、IsEncrypted、Status、UpdatedBy、UpdatedAt、Version）
- [ ] 实现 `SystemConfig.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现加密配置值存储（IsEncrypted=true 时加密存储，查询时解密）
- [ ] 实现 `DataDictionary` 聚合根（DictionaryId、Code、Name、Description、Status、Version）
- [ ] 实现 `DataDictionary.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现 `DictionaryItem` 实体（ItemId、DictionaryId、Code、Label、Value、SortOrder、Status）
- [ ] 实现 `DataDictionary.AddItem`/`RemoveItem`/`UpdateItem` 方法
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add SystemConfig and DataDictionary aggregates`

---

## Task 3: 领域层 — 审计日志与操作日志查询模型

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/AuditLog.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/OperationLog.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/SystemAnnouncement.cs`

- [ ] 实现 `AuditLog` 聚合（LogId、OperatorId、Action、ResourceType、ResourceId、RequestSummary、ResponseStatus、IpAddress、TraceId、OccurredAt）— 各域写入，本域查询
- [ ] 实现 `OperationLog` 聚合（LogId、OperatorId、OperationType、Module、Description、BeforeSnapshot、AfterSnapshot、IpAddress、OccurredAt）
- [ ] 实现 `SystemAnnouncement` 聚合根（AnnouncementId、Title、Content、Type、TargetAudience、PublishAt、ExpireAt、Status、CreatedBy、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `SystemAnnouncement.Create`/`Publish`/`Unpublish`/`Update` 方法
- [ ] 定义 `AnnouncementType`（System/Maintenance/Promotion）、`AnnouncementStatus`（Draft/Published/Expired）
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add AuditLog, OperationLog and Announcement aggregates`

---

## Task 4: 领域层 — 功能开关与定时任务管理

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/FeatureFlag.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/ScheduledTask.cs`

- [ ] 实现 `FeatureFlag` 聚合根（FlagId、Key、Name、Description、IsEnabled、Strategy、Rules、UpdatedBy、UpdatedAt、Version）
- [ ] 实现 `FeatureFlag.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现规则策略（按用户/角色/百分比灰度发布）
- [ ] 实现 `ScheduledTask` 聚合根（TaskId、Name、JobType、CronExpression、Parameters、Status、LastRunAt、LastRunStatus、NextRunAt、Version）
- [ ] 实现 `ScheduledTask.Create`/`Update`/`Enable`/`Disable`/`RunNow` 方法
- [ ] 实现 `ScheduledTask.RecordExecution(runAt, status, result)`（记录执行结果）
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add FeatureFlag and ScheduledTask aggregates`

---

## Task 5: 领域层 — 仓储接口与领域服务

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IOperatorRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/ISystemConfigRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IDataDictionaryRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IAuditLogRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IOperationLogRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/ISystemAnnouncementRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IFeatureFlagRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Repositories/IScheduledTaskRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Services/IFeatureFlagEvaluator.cs`

- [ ] 定义各仓储接口（含分页查询、多条件过滤）
- [ ] 定义 `IFeatureFlagEvaluator` 接口（EvaluateAsync(flagKey, context) 判断功能开关是否生效）
- [ ] 提交：`feat(system-admin): add repository interfaces and feature flag evaluator`

---

## Task 6: 领域事件定义

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Events/AnnouncementPublishedEvent.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Events/FeatureFlagChangedEvent.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Events/ConfigChangedEvent.cs`

- [ ] 定义 `AnnouncementPublishedEvent`（announcementId、title、type）— 消费方：通知域（站内信推送）
- [ ] 定义 `FeatureFlagChangedEvent`（flagKey、isEnabled、strategy）— 消费方：各域（刷新功能开关缓存）
- [ ] 定义 `ConfigChangedEvent`（configKey、configValue）— 消费方：各域（刷新配置缓存）
- [ ] 提交：`feat(system-admin): add domain integration events`

---

## Task 7: 基础设施层 — EF Core 仓储与缓存

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/SystemAdminDbContext.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreOperatorRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreSystemConfigRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreDataDictionaryRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreAuditLogRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreOperationLogRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreSystemAnnouncementRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreFeatureFlagRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Repositories/EfCoreScheduledTaskRepository.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/SystemConfigCache.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/FeatureFlagCache.cs`

- [ ] 实现 `SystemAdminDbContext`（各 DbSet 配置）
- [ ] 实现各 EF Core 仓储（审计日志支持按操作人、资源类型、时间区间分页查询）
- [ ] 实现 `SystemConfigCache`（Redis 缓存系统配置，ConfigChangedEvent 驱动失效）
- [ ] 实现 `FeatureFlagCache`（Redis 缓存功能开关状态，FeatureFlagChangedEvent 驱动失效）
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试
- [ ] 提交：`feat(system-admin): add EF Core repositories and Redis cache`

---

## Task 8: 基础设施层 — 功能开关评估器与定时任务调度

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/FeatureFlagEvaluator.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/QuartzJobScheduler.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Jobs/ScheduledTaskDispatcher.cs`

- [ ] 实现 `FeatureFlagEvaluator`（从缓存读取开关状态，按策略评估：全局开关/用户白名单/角色/百分比灰度）
- [ ] 实现 `QuartzJobScheduler`（基于 Quartz.NET 调度定时任务，支持 Cron 表达式）
- [ ] 实现 `ScheduledTaskDispatcher`（从 DB 加载启用的定时任务，注册到 Quartz 调度器）
- [ ] 实现任务执行结果记录（更新 ScheduledTask.LastRunAt/LastRunStatus）
- [ ] 编写单元测试与集成测试
- [ ] 提交：`feat(system-admin): add feature flag evaluator and Quartz job scheduler`

---

## Task 9: 基础设施层 — 事件消费者

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AfterSalesEventConsumer.cs`

- [ ] 实现 `AuditLogConsumer`（消费各域审计日志事件，统一写入 AuditLog 表）
- [ ] 实现 `AfterSalesEventConsumer`（消费 AfterSalesApprovedEvent/RefundCompletedEvent 记录运营操作日志）
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试
- [ ] 提交：`feat(system-admin): add event consumers for audit log aggregation`

---

## Task 10: 应用层 — 运营人员与系统配置管理用例

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/IOperatorAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/ISystemConfigAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/OperatorAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs`

- [ ] 实现运营人员管理用例（创建运营账号、分配权限、启停、查询列表）
- [ ] 实现系统配置管理用例（CRUD 配置项、按 Group 分组查询、加密配置脱敏返回）
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add operator and config application services`

---

## Task 11: 应用层 — 审计日志查询与数据字典用例

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/IAuditLogAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/IDataDictionaryAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AuditLogAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/DataDictionaryAppService.cs`

- [ ] 实现审计日志查询用例（按操作人、资源类型、时间区间分页查询，支持导出 CSV）
- [ ] 实现操作日志查询用例
- [ ] 实现数据字典管理用例（CRUD 字典与字典项，供各域引用枚举值）
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add audit log and dictionary application services`

---

## Task 12: 应用层 — 公告、功能开关与定时任务用例

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/IAnnouncementAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/IFeatureFlagAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/IScheduledTaskAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AnnouncementAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/ScheduledTaskAppService.cs`

- [ ] 实现系统公告管理用例（创建草稿→发布→撤回→过期，发布时触发 AnnouncementPublishedEvent）
- [ ] 实现功能开关管理用例（CRUD 开关、配置灰度策略、变更时发布 FeatureFlagChangedEvent）
- [ ] 实现定时任务管理用例（CRUD 任务、启停、手动触发执行、查询执行历史）
- [ ] 实现系统监控面板用例（各服务健康状态、关键指标概览）
- [ ] 编写单元测试
- [ ] 提交：`feat(system-admin): add announcement, feature flag and scheduled task services`

---

## Task 13: 表现层 — API 控制器

**文件:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/OperatorsController.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/SystemConfigsController.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/DataDictionariesController.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AnnouncementsController.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/FeatureFlagsController.cs`
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/ScheduledTasksController.cs`

- [ ] 实现 `OperatorsController`（GET/POST/PUT /api/admin/operators、POST .../{id}/activate、POST .../{id}/deactivate）
- [ ] 实现 `SystemConfigsController`（GET/POST/PUT /api/admin/system-configs、GET /api/admin/system-configs/groups）
- [ ] 实现 `AuditLogsController`（GET /api/admin/audit-logs、GET /api/admin/audit-logs/export）
- [ ] 实现 `DataDictionariesController`（GET/POST/PUT /api/admin/dictionaries、GET /api/dictionaries/{code}/items 公开查询）
- [ ] 实现 `AnnouncementsController`（GET/POST/PUT /api/admin/announcements、POST .../{id}/publish、POST .../{id}/unpublish）
- [ ] 实现 `FeatureFlagsController`（GET/POST/PUT /api/admin/feature-flags、POST .../{id}/enable、POST .../{id}/disable）
- [ ] 实现 `ScheduledTasksController`（GET/POST/PUT /api/admin/scheduled-tasks、POST .../{id}/enable、POST .../{id}/disable、POST .../{id}/run-now）
- [ ] 配置 JWT 鉴权与运营角色策略
- [ ] 编写 API 集成测试覆盖各管理功能
- [ ] 提交：`feat(system-admin): add API controllers`
