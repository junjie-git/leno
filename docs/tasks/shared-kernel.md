# 共享内核与基础设施 (Shared Kernel & Infrastructure) 开发任务

> **限界上下文**: 跨上下文共享内核  
> **技术栈**: .NET 10 / ASP.NET Core / EF Core / SQL Server / Redis / RabbitMQ / Elasticsearch / Serilog / MediatR  
> **依赖**: 无（所有模块的基础）  
> **对应文档**: `00-需求文档总览与DDD架构.md`、`技术选型方案.md`、`编码规范.md`

---

## 模块概述

共享内核提供所有限界上下文复用的值对象、接口契约、基础抽象与横切关注点。基础设施层提供通用技术实现（数据库、缓存、消息队列、日志、配置）。本模块是所有业务域开发的前置依赖。

---

## Task 1: 解决方案骨架与项目结构

**文件:**
- Create: `src/BuildingBlocks/Leno.SharedKernel/Leno.SharedKernel.csproj`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Leno.SharedContracts.csproj`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
- Create: `Leno.sln`

- [ ] 创建解决方案文件与目录结构（按模块化部署架构文档第2节划分）
- [ ] 创建 `SharedKernel` 类库项目（领域基础：值对象、实体基类、领域事件接口）
- [ ] 创建 `SharedContracts` 类库项目（集成事件契约、DTO 基类、通用响应格式）
- [ ] 创建 `Infrastructure` 类库项目（EF Core DbContext 基类、仓储基类、发件箱）
- [ ] 配置 `Directory.Build.props` 统一 Nullable Enable、TreatWarningsAsErrors
- [ ] 添加 `.editorconfig` 对齐编码规范（命名、格式、分析器规则）
- [ ] 提交：`chore: init solution skeleton with shared kernel projects`

---

## Task 2: 共享内核值对象

**文件:**
- Create: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/SpecAttribute.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageRequest.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageResult.cs`

- [ ] 实现 `Money` 值对象（Amount + Currency，四舍五入到两位小数，运算符重载 +、-、比较）
- [ ] 实现 `MoneyJsonConverter`（EF Core 值转换器与 JSON 序列化）
- [ ] 实现 `SpecAttribute` 值对象（Name + Value，商品规格复用）
- [ ] 实现 `PageRequest`/`PageResult<T>` 分页值对象（page 从 1 开始，pageSize 默认 20 最大 100）
- [ ] 编写单元测试覆盖 Money 运算与边界
- [ ] 提交：`feat: add shared kernel value objects`

---

## Task 3: 领域基础抽象

**文件:**
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IDomainEvent.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IHasDomainEvents.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs`

- [ ] 实现 `Entity` 基类（Id、CreatedAt、UpdatedAt、Version 乐观锁）
- [ ] 实现 `AggregateRoot` 基类（继承 Entity，持有 `_domainEvents` 列表，提供 `AddDomainEvent`/`ClearDomainEvents`）
- [ ] 定义 `IDomainEvent` 接口（EventId、OccurredAt、AggregateId）
- [ ] 定义 `IHasDomainEvents` 接口
- [ ] 实现 `DomainException`（携带错误码与消息，映射 HTTP 状态码）
- [ ] 编写单元测试验证领域事件收集与清除
- [ ] 提交：`feat: add domain base abstractions`

---

## Task 4: 集成事件与发件箱模式

**文件:**
- Create: `src/BuildingBlocks/Leno.SharedContracts/Events/IIntegrationEvent.cs`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs`

- [ ] 定义 `IIntegrationEvent` 接口（EventId、OccurredAt、IdempotencyKey）
- [ ] 实现 `IntegrationEventBase` 抽象基类
- [ ] 实现 `OutboxMessage` 实体（Id、Type、Payload、OccurredAt、ProcessedAt、RetryCount、Error）
- [ ] 实现 `OutboxDbContextExtensions`（`SaveChangesWithOutboxAsync` 在同一事务写入聚合与发件箱消息）
- [ ] 实现 `OutboxPublisher` 后台服务（轮询发件箱表，发布到 RabbitMQ，标记已处理，失败重试）
- [ ] 配置死信队列重试策略（指数退避，超阈值告警）
- [ ] 编写集成测试验证发件箱原子性
- [ ] 提交：`feat: add integration event contract and outbox pattern`

---

## Task 5: 仓储与工作单元抽象

**文件:**
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IRepository.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IUnitOfWork.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Persistence/EFCoreInterceptors.cs`

- [ ] 定义 `IRepository<T>` 泛型接口（GetByIdAsync、AddAsync、UpdateAsync）
- [ ] 定义 `IUnitOfWork` 接口（SaveChangesAsync、BeginTransactionAsync）
- [ ] 实现 `BaseDbContext`（配置审计字段自动填充、软删除过滤器、乐观锁拦截器）
- [ ] 实现 `AuditableEntityInterceptor`（自动填充 CreatedAt、UpdatedAt）
- [ ] 实现 `SoftDeleteInterceptor`（全局查询过滤器排除已软删除记录）
- [ ] 实现 `OptimisticLockInterceptor`（处理 Version 字段并发冲突）
- [ ] 编写集成测试验证拦截器行为
- [ ] 提交：`feat: add repository, unit of work and EF Core interceptors`

---

## Task 6: 外部能力配置驱动抽象

**文件:**
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IExternalChannelOptions.cs`
- Create: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IFileStorageService.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Storage/FileStorageOptions.cs`

- [ ] 定义 `IExternalChannelOptions` 配置抽象接口（渠道参数配置驱动契约，对应总览 4.8 节）
- [ ] 定义 `IFileStorageService` 接口（UploadAsync、GetUrlAsync、ValidateUrlAsync）
- [ ] 实现 `FileStorageOptions`（Provider 配置：Local/MinIO/OSS）
- [ ] 实现 `LocalFileStorageService`（本地磁盘存储，URL 校验）
- [ ] 预留 `ObjectStorageService` 接口位置（MinIO/OSS 适配器后续按需实现）
- [ ] 编写单元测试验证文件上传与 URL 校验
- [ ] 提交：`feat: add external channel options and file storage abstraction`

---

## Task 7: 横切关注点（日志、鉴权、异常处理、API 规范）

**文件:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Logging/SensitiveDataDestructurer.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Auth/CurrentUserContext.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Middleware/GlobalExceptionMiddleware.cs`
- Create: `src/BuildingBlocks/Leno.SharedContracts/Responses/ApiResponse.cs`

- [ ] 配置 Serilog 结构化日志（JSON 输出、RequestId、TraceId 贯穿）
- [ ] 实现 `SensitiveDataDestructurer`（密码、密钥、Token 日志脱敏）
- [ ] 实现 `JwtTokenGenerator`（JWT 生成与校验，Claim 携带 UserId、Role、ShopId）
- [ ] 实现 `ICurrentUserContext` 接口与实现（从 JWT 提取当前用户信息）
- [ ] 实现 `GlobalExceptionMiddleware`（DomainException→400/409，未授权→401，统一 ApiResponse 格式）
- [ ] 实现 `ApiResponse<T>` 统一响应（code/message/data，对应总览第8章 RESTful 规范）
- [ ] 实现幂等键中间件（`Idempotency-Key` 头处理，Redis 缓存首次结果）
- [ ] 编写集成测试验证异常映射与响应格式
- [ ] 提交：`feat: add cross-cutting concerns (logging, auth, exception handling)`

---

## Task 8: 消息总线与事件消费者基类

**文件:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/EventBus/RabbitMqEventBus.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/EventBus/IEventBus.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs`

- [ ] 定义 `IEventBus` 接口（PublishAsync、SubscribeAsync）
- [ ] 实现 `RabbitMqEventBus`（基于 RabbitMQ.Client，Topic 交换机，按事件类型路由）
- [ ] 实现 `IntegrationEventConsumerBase<T>`（消费幂等去重以 EventId，消费失败进死信队列）
- [ ] 配置 RabbitMQ 拓扑（交换机命名规则、队列命名规则、死信队列绑定）
- [ ] 编写集成测试验证事件发布与消费
- [ ] 提交：`feat: add RabbitMQ event bus and consumer base`

---

## Task 9: CQRS 读库同步基础设施

**文件:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/ReadModel/IEsReadModelRepository.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/ReadModel/EsReadModelRepository.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs`

- [ ] 定义 `IEsReadModelRepository<T>` 接口（IndexAsync、GetByIdAsync、SearchAsync、DeleteByIdAsync）
- [ ] 实现 `EsReadModelRepository<T>`（基于 Elasticsearch.Net，索引 CRUD 与搜索）
- [ ] 实现 `ReadModelSyncConsumerBase<TEvent, TReadModel>`（消费领域事件同步 ES 读模型）
- [ ] 配置 ES 索引映射模板（按域分索引，字段类型与分词器）
- [ ] 编写集成测试验证读模型同步
- [ ] 提交：`feat: add Elasticsearch read model sync infrastructure`

---

## Task 10: 配置中心与健康检查

**文件:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/HealthChecks/DependencyHealthCheck.cs`

- [ ] 配置 Consul/Apollo 配置中心集成（`IConfiguration` 扩展，热更新监听）
- [ ] 实现配置占位符 `${ENV_VAR}` 解析（环境变量注入敏感参数）
- [ ] 实现各模块 `/health` 端点（检查 DB、Redis、ES、MQ、外部渠道依赖）
- [ ] 配置 OpenTelemetry 链路追踪（traceId 贯穿网关到各服务到事件消费）
- [ ] 提交：`feat: add config center integration and health checks`
