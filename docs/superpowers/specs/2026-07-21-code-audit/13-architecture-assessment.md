# 阶段 3：系统架构整体评估报告（G1-G7）

> 评估对象：Leno 电商平台（11 个 BC + 1 个 BFF 网关 + 共享内核/共享契约/基础设施三套 BuildingBlocks）
> 评估依据：阶段 1 各 BC 代码审计报告（01-12）、阶段 2 跨 BC 聚合报告（00-summary）、设计文档与 ADR 0001-0007、架构手册 03/05/06 章、源码 `src/BuildingBlocks/Leno.Infrastructure/*` 与 `src/Services/*`
> 评估目标：对系统整体架构相对 DDD+CQRS+事件驱动+微服务目标架构的达成度进行量化评估，识别系统级问题、技术债、生产风险，并与业界参考架构对比
> 评估时间：2026-07-21

---

## G1 架构定位与成熟度

### G1.1 整体架构定位

Leno 电商平台采用 **DDD（领域驱动设计）+ CQRS（命令查询职责分离）+ 事件驱动 + 微服务** 四位一体的目标架构，按业务子域拆分为 11 个 BC（限界上下文），每个 BC 独立部署、独立数据库、独立演进，通过集成事件（异步）与防腐层 ACL（同步）两类受控通道协作。架构落地路径清晰：M1-M6 六个里程碑从战略设计到战术实现再到运维部署层层递进，并通过 7 份 ADR（0001-0007）沉淀关键技术决策的来龙去脉。

架构骨架定义于 [00-需求文档总览与DDD架构.md](file:///workspace/docs/spec/00-需求文档总览与DDD架构.md)，11 个 BC 划分与端口规划详见 [handbook/03-architecture-overview.md#L66-L78](file:///workspace/docs/handbook/03-architecture-overview.md#L66-L78)，跨 BC 通信规范详见 [handbook/05-cross-bc-communication.md#L42-L89](file:///workspace/docs/handbook/05-cross-bc-communication.md#L42-L89)。

### G1.2 五维度量化评估

#### 维度 1：DDD 战略设计达成度（85%）

| 评估项 | 达成情况 | 评分 | 证据 |
|---|---|---|---|
| 限界上下文划分 | 11 个 BC 按业务子域划分，6 核心 / 3 支撑 / 2 通用，边界清晰 | 优 | [handbook/03-architecture-overview.md#L66-L78](file:///workspace/docs/handbook/03-architecture-overview.md#L66-L78) |
| 上下文映射 | 6 类映射关系齐全（共享内核/客户-供应商/遵奉者/ACL/OHS+PL/各行其道），mermaid 图可视化 | 优 | [handbook/03-architecture-overview.md#L93-L137](file:///workspace/docs/handbook/03-architecture-overview.md#L93-L137) |
| 共享内核约束 | `Leno.SharedKernel` 只放基础抽象（Entity/AggregateRoot/Money/IUnitOfWork），不放业务模型 | 优 | [handbook/03-architecture-overview.md#L251-L268](file:///workspace/docs/handbook/03-architecture-overview.md#L251-L268) |
| 共享契约分层 | `Leno.SharedContracts` 分 Events/Grpc/Dtos/Responses 四子目录，与共享内核区分清晰 | 优 | [handbook/03-architecture-overview.md#L307-L326](file:///workspace/docs/handbook/03-architecture-overview.md#L307-L326) |
| 战术设计落地 | 7 个概念全部映射到代码（实体/值对象/聚合根/领域服务/领域事件/仓储/工厂） | 优 | [handbook/03-architecture-overview.md#L141-L231](file:///workspace/docs/handbook/03-architecture-overview.md#L141-L231) |
| 统一语言一致性 | BC 内部术语基本一致，但部分 BC（如 ReviewAfterSales）混合"评价"与"售后"两个子域 | 良 | [06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md) |
| 共享内核污染 | D3 模式（共享内核污染）：`Money` 值对象在 Product/Promotion/Order/Cart 多处出现截断 vs 取舍不一致 | 中 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D3 章节） |
| 重复实现 | D6 模式（重复实现）：UserAuth InMemoryRefreshTokenStore 生产误注册、CacheService 非线程安全 Random | 中 | [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md) |

**维度 1 评分：85/100**。战略设计达成度高，限界上下文与上下文映射按 DDD 经典方法落地，但共享内核污染（D3）与跨 BC 重复实现（D6）两个系统级问题拉低分数。

#### 维度 2：CQRS 读写分离达成度（80%）

| 评估项 | 达成情况 | 评分 | 证据 |
|---|---|---|---|
| 命令侧实现 | 聚合根 + AppService + Repository + EF Core 写库，11 个 BC 一致执行 | 优 | [handbook/03-architecture-overview.md#L436-L470](file:///workspace/docs/handbook/03-architecture-overview.md#L436-L470) |
| 查询侧实现 | `IQueryHandler<TQuery, TResult>` 接口 + DI 反射注册（不依赖 MediatR），轻量清爽 | 优 | [handbook/03-architecture-overview.md#L453-L470](file:///workspace/docs/handbook/03-architecture-overview.md#L453-L470) |
| 读模型同步 | `ReadModelSyncConsumerBase<TEvent, TReadModel>` 消费集成事件投影到 Elasticsearch，统一抽象 | 优 | [src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs#L16-L80](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs#L16-L80) |
| 读模型死消费者 | PM-H03：4 个 ReadModel 同步消费者订阅的事件从未发布（积分账本/会员等级等读模型从未物化） | 差 | [07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md)（PM-H03） |
| 读模型数据正确性 | ShopDashboardReadModelBuilder 6 个字段硬编码为 0，Dashboard 数据失真 | 差 | [10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |
| BFF 聚合层 | BFF 网关（端口 8080）聚合 11 BC 数据，含 JWT 验签 | 良 | [handbook/03-architecture-overview.md#L78](file:///workspace/docs/handbook/03-architecture-overview.md#L78) |

**维度 2 评分：80/100**。CQRS 框架设计精良（IQueryHandler + ReadModelSyncConsumerBase），但 4 个死消费者与读模型数据失真问题使"读侧"实际可用性打折。

#### 维度 3：事件驱动一致性达成度（70%）

| 评估项 | 达成情况 | 评分 | 证据 |
|---|---|---|---|
| 领域事件 vs 集成事件区分 | 4 条规则严格约束（领域事件不跨 BC、IIntegrationEventMapper 翻译、SchemaVersion 版本化、Outbox 发布） | 优 | [handbook/05-cross-bc-communication.md#L102-L155](file:///workspace/docs/handbook/05-cross-bc-communication.md#L102-L155) |
| Outbox 模式 | 两阶段标记（Pending→Publishing→Processed）+ 超时回退 + 并行发布（DOP=4）+ 积压告警 | 优 | [src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L13-L51](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L13-L51) |
| 幂等基类 | `IntegrationEventConsumerBase<T>` 强制注入 `IIdempotencyStore`，处理前检查处理后标记 | 优 | [src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L8-L74](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L8-L74) |
| Outbox 旁路 | D2 模式：5 个 BC（UserAuth/Promotion/SystemAdmin/PointsMembership/Cart）存在 `SaveChangesAsync` 或 `PublishAsync` 旁路 Outbox 的代码 | 差 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D2 章节） |
| 死信处理 | DLQ 队列 + `DeadLetterQueueManager`，但 H-05 用 `SaveChangesAsync` 而非 `SaveEntitiesAsync`，死信状态可能丢失 | 中 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)（H-05） |
| 事件契约对齐 | D1 模式：跨 BC 集成事件契约不对齐（如 RefundSucceededEventConsumer 缺 ChannelRefundNo 字段） | 差 | [06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)（RefundSucceededEventConsumer 缺 ChannelRefundNo） |
| 事件循环风险 | ReviewAfterSales.RefundCompleted 事件循环（L43-L46, L15-L74） | 差 | [06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)（RefundCompleted 事件循环） |
| 双发风险 | PM-H07：OrderCompleted + OrderAfterSalesWindowClosed 双发积分风险 | 差 | [07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md)（PM-H07） |

**维度 3 评分：70/100**。Outbox + 幂等基类的"骨架"是工业级水准，但 Outbox 旁路（D2）、契约不对齐（D1）、事件循环与双发三个"血肉"级问题使实际一致性打折，是当前架构最薄弱的环节。

#### 维度 4：微服务边界与 ACL 达成度（82%）

| 评估项 | 达成情况 | 评分 | 证据 |
|---|---|---|---|
| 一 BC 一库分库 | 11 个 BC 各自独立 SQL Server 数据库，禁止跨库直接访问 | 优 | [handbook/06-storage-and-cache.md#L57-L69](file:///workspace/docs/handbook/06-storage-and-cache.md#L57-L69) |
| 防腐层 ACL 模式 | `AntiCorruptionBase` 模板方法 + `AntiCorruptionDispatcher<TService>` 双轨调度 + `GrpcAntiCorruptionClientBase` | 优 | [src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L7-L80](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L7-L80) |
| gRPC 双轨 + 三态熔断 | ADR-0001/0002/0003 三 ADR 配套，Consul KV 热切换 UseGrpc，熔断 Closed/Open/HalfOpen 状态机 | 优 | [decisions/0001-grpc-dual-track-with-http-fallback.md](file:///workspace/docs/decisions/0001-grpc-dual-track-with-http-fallback.md)、[decisions/0002-circuit-breaker-three-state-machine.md](file:///workspace/docs/decisions/0002-circuit-breaker-three-state-machine.md)、[decisions/0003-anticorruption-dispatcher-adapter-pattern.md](file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md) |
| 端口与命名规范 | BC 端口 5101-5110，BFF 8080，snake_case 列名，`ix_{table}_{column}` 索引命名 | 优 | [handbook/03-architecture-overview.md#L66-L78](file:///workspace/docs/handbook/03-architecture-overview.md#L66-L78)、[handbook/06-storage-and-cache.md#L198-L258](file:///workspace/docs/handbook/06-storage-and-cache.md#L198-L258) |
| ACL 重复实现 | D2 模式：防腐层基类抽象到位，但各 BC `{Service}DispatcherAdapter` 重复样板代码 | 良 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D2 章节） |
| ACL 失败闭合无指标 | SellerShop gRPC 客户端 fail-closed 但无 metrics，故障不可观测 | 中 | [10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |
| gRPC Guid→int64 POC 残留 | D5 模式：4 个 BC（Order/Product/ReviewAfterSales/SellerShop）`Guid.GetHashCode()` 碰撞风险残留生产 | 差 | [decisions/0006-guid-int64-poc-simplification-history.md](file:///workspace/docs/decisions/0006-guid-int64-poc-simplification-history.md)、[decisions/0007-guid-string-migration-strategy.md](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md) |
| InternalApiKey 鉴权 | `/internal/v1/` 路径 + `X-Internal-Key` 头鉴权，每 BC 独立 InternalApiKey，Consul KV 注入 | 优 | [handbook/05-cross-bc-communication.md#L37-L38](file:///workspace/docs/handbook/05-cross-bc-communication.md#L37-L38) |
| IDOR 越权风险 | PaymentsController IDOR（订单 ID 直查无归属校验）、ReviewAfterSales 多处缺归属校验 | 差 | [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)、[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md) |

**维度 4 评分：82/100**。ACL 双轨 + 三态熔断 + Consul 配置中心是架构的"金牌能力"，但 gRPC Guid→int64 POC 残留（D5）与 IDOR 越权风险两个安全/正确性问题拉低分数。

#### 维度 5：可观测性达成度（75%）

| 评估项 | 达成情况 | 评分 | 证据 |
|---|---|---|---|
| OpenTelemetry Tracing | M5.1 全链路追踪接入，跨 BC 上下文透传 | 优 | [handbook/03-architecture-overview.md](file:///workspace/docs/handbook/03-architecture-overview.md)（M5.1 章节） |
| Prometheus Metrics | AntiCorruptionMetrics 5 个指标（failure_total/fallback_total/circuit_open/grpc_request_total/grpc_duration_seconds） | 优 | [architecture/anticorruption-pattern.md](file:///workspace/docs/architecture/anticorruption-pattern.md)（Prometheus metrics 章节） |
| Serilog 结构化日志 | 统一 Serilog 配置 + SensitiveDataDestructurer 脱敏 | 优 | [src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs) |
| 健康检查 | DependencyHealthCheck + HealthChecksUI 扩展，K8s liveness/readiness 探针 | 优 | [src/BuildingBlocks/Leno.Infrastructure/HealthChecks/](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/HealthChecks/) |
| 静态状态竞态 | AntiCorruptionMetrics 静态 Dictionary 竞态、CacheService 非线程安全 Random | 差 | [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md) |
| 缓存失效缺位 | FeatureFlagCache/SystemConfigCache 永不失效（H-03），配置变更不生效 | 差 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)（H-03） |
| 死信队列可观测 | DeadLetterQueueManager 存在但用错 SaveChanges 方法 | 中 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)（H-05） |
| 指标误用 | H-01：SystemAdmin StatisticsAggregationService 用 `new Random()` 生成所有指标值，Dashboard 数据完全失真 | 差 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)（H-01） |

**维度 5 评分：75/100**。可观测性"骨架"（OTel + Prometheus + Serilog + HealthChecks）齐全，但静态状态竞态与指标误用（H-01 用 Random 生成指标）两个"血肉"级问题使实际可观测性打折。

### G1.3 总体成熟度评分

| 维度 | 评分 | 权重 | 加权得分 |
|---|---|---|---|
| DDD 战略设计 | 85 | 0.20 | 17.0 |
| CQRS 读写分离 | 80 | 0.15 | 12.0 |
| 事件驱动一致性 | 70 | 0.25 | 17.5 |
| 微服务边界与 ACL | 82 | 0.25 | 20.5 |
| 可观测性 | 75 | 0.15 | 11.25 |
| **总体成熟度** | - | **1.00** | **78.25 / 100** |

### G1.4 成熟度等级评定

按业界通用成熟度模型分级：

| 等级 | 分数区间 | 含义 | Leno 当前 |
|---|---|---|---|
| L1 初始 | 0-40 | 无架构，事务脚本 | - |
| L2 受管 | 40-60 | 有分层但边界模糊 | - |
| L3 已定义 | 60-75 | 架构清晰但执行不彻底 | - |
| **L4 量化管理** | **75-90** | **架构清晰且执行到位，少量系统级问题** | **✅ 当前（78.25）** |
| L5 持续优化 | 90-100 | 自适应、自愈、持续演进 | - |

**结论**：Leno 电商平台处于 **L4 量化管理** 级别，距 L5 持续优化尚有约 12 分差距，主要短板集中在事件驱动一致性（70 分）与可观测性（75 分）两个维度。

---

## G2 架构优点

### G2.1 ACL 双轨调度 + 三态熔断状态机（金牌能力）

| 项 | 内容 |
|---|---|
| 名称 | AntiCorruptionDispatcher 双轨调度 + CircuitBreakerState 三态熔断 |
| 价值 | 在 gRPC 高性能与 HTTP 兜底可用性之间取得平衡，支持灰度切换与自动降级，是分布式系统弹性设计的工业级实现 |
| 适用场景 | BC 间同步调用（如 Cart→Product 查 SKU、Order→Promotion 算优惠、Order→Payment 查支付）需要实时结果但容忍短时降级的场景 |
| 证据 | [src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L49-L80](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L49-L80)：每次请求读取 `IOptionsMonitor<AntiCorruptionOptions>` 最新配置，按 `UseGrpc` 开关与熔断状态决策，Open 态直接降级 HTTP；[decisions/0002-circuit-breaker-three-state-machine.md](file:///workspace/docs/decisions/0002-circuit-breaker-three-state-machine.md)：Closed→Open（连续 3 次失败）→HalfOpen（30s 后单探针）状态机；[decisions/0003-anticorruption-dispatcher-adapter-pattern.md](file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md)：Dispatcher 不实现 TService 而用 `{Service}DispatcherAdapter` 包装，避免接口污染；[architecture/anticorruption-pattern.md](file:///workspace/docs/architecture/anticorruption-pattern.md)：完整组件清单与决策矩阵 |

### G2.2 Outbox 模式两阶段标记 + 幂等消费基类（金牌能力）

| 项 | 内容 |
|---|---|
| 名称 | OutboxPublisher 两阶段标记 + IntegrationEventConsumerBase 幂等基类 |
| 价值 | 把"业务事务+消息发送"原子性难题降维为单库事务，两阶段标记（Pending→Publishing→Processed）+ 超时回退 + 并行发布（DOP=4）+ 积压告警，配合消费端 `IIdempotencyStore` 强制幂等去重，构成端到端的"恰好一次"语义骨架 |
| 适用场景 | 所有跨 BC 异步事件（订单创建、支付完成、库存扣减、积分发放等）的发布与消费 |
| 证据 | [src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L13-L51](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L13-L51)：两阶段标记防重复发布 + 并行处理 + 积压告警 + 类型解析；[src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L8-L74](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L8-L74)：`IIdempotencyStore` 强制注入，处理前检查 EventId 是否已处理，处理后标记；[handbook/05-cross-bc-communication.md#L186-L323](file:///workspace/docs/handbook/05-cross-bc-communication.md#L186-L323)：Outbox 模式详解与状态机语义 |

### G2.3 Consul 配置中心 KV 热更新（金牌能力）

| 项 | 内容 |
|---|---|
| 名称 | ConsulConfigWatcher 长轮询 + IOptionsMonitor 热更新 |
| 价值 | AntiCorruption 配置（UseGrpc 开关、熔断阈值）等关键运行时参数无需重启即可热切换，配合 `IOptionsMonitor<AntiCorruptionOptions>` 每次请求读取最新值，支持灰度发布与故障快速切换 |
| 适用场景 | gRPC/HTTP 双轨切换、熔断参数调优、InternalApiKey 轮换、限流规则调整 |
| 证据 | [handbook/05-cross-bc-communication.md#L36](file:///workspace/docs/handbook/05-cross-bc-communication.md#L36)：Consul KV 用作配置中心；[architecture/anticorruption-pattern.md](file:///workspace/docs/architecture/anticorruption-pattern.md)：ConsulConfigWatcher 长轮询（5min WaitTime + 10s RetryDelay）；[src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L55-L56](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L55-L56)：`_optionsMonitor.CurrentValue` 每次请求读取最新配置 |

### G2.4 Helm Chart 部署 + HPA + 探针（金牌能力）

| 项 | 内容 |
|---|---|
| 名称 | Helm Chart 标准化部署 + HPA 自动扩缩 + K8s liveness/readiness 探针 |
| 价值 | 11 个 BC 一键部署、按负载自动扩缩、不健康实例自动重启，是云原生微服务部署的工业级实践 |
| 适用场景 | 生产环境 K8s 集群部署、流量高峰自动扩容、实例故障自愈 |
| 证据 | [handbook/03-architecture-overview.md](file:///workspace/docs/handbook/03-architecture-overview.md)（M5.4 Helm Chart 章节）；[src/BuildingBlocks/Leno.Infrastructure/HealthChecks/](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/HealthChecks/)：DependencyHealthCheck + HealthChecksUIExtensions |

### G2.5 IQueryHandler 轻量 CQRS 实现（无 MediatR 依赖）

| 项 | 内容 |
|---|---|
| 名称 | IQueryHandler<TQuery, TResult> + DI 反射注册 |
| 价值 | 不引入 MediatR 等重量级框架，用极简接口 + DI 反射注册实现 CQRS 查询侧，降低学习成本与依赖复杂度，符合"用最简单的方式解决问题"的工程哲学 |
| 适用场景 | BC 内部查询场景（如查订单列表、查商品详情、查积分流水） |
| 证据 | [handbook/03-architecture-overview.md#L453-L470](file:///workspace/docs/handbook/03-architecture-overview.md#L453-L470)：IQueryHandler 接口定义与 DI 注册；[src/BuildingBlocks/Leno.Infrastructure/Cqrs/QueryHandlerExtensions.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Cqrs/QueryHandlerExtensions.cs) |

### G2.6 ADR 决策追溯体系（金牌能力）

| 项 | 内容 |
|---|---|
| 名称 | 7 份 ADR（0001-0007）覆盖关键架构决策 |
| 价值 | 每个关键架构决策（gRPC 双轨、熔断状态机、Dispatcher 适配器、IOrderStatusProvider 重构、proto 兼容约束、Guid→int64 POC、Guid→string 迁移）都有"状态/上下文/决策/后果/风险缓解"五段式文档，新人可快速理解决策来龙去脉，避免"为什么这么设计"的反复讨论 |
| 适用场景 | 架构评审、新人 onboarding、技术债追溯、决策回滚评估 |
| 证据 | [decisions/0001-grpc-dual-track-with-http-fallback.md](file:///workspace/docs/decisions/0001-grpc-dual-track-with-http-fallback.md)、[decisions/0002-circuit-breaker-three-state-machine.md](file:///workspace/docs/decisions/0002-circuit-breaker-three-state-machine.md)、[decisions/0003-anticorruption-dispatcher-adapter-pattern.md](file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md)、[decisions/0004-iorderstatus-provider-refactor.md](file:///workspace/docs/decisions/0004-iorderstatus-provider-refactor.md)、[decisions/0005-proto-backward-compatibility-constraint.md](file:///workspace/docs/decisions/0005-proto-backward-compatibility-constraint.md)、[decisions/0006-guid-int64-poc-simplification-history.md](file:///workspace/docs/decisions/0006-guid-int64-poc-simplification-history.md)、[decisions/0007-guid-string-migration-strategy.md](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md) |

### G2.7 BaseDbContext 统一基础设施（消除重复）

| 项 | 内容 |
|---|---|
| 名称 | BaseDbContext 公共特性（OutboxMessages DbSet + Version 乐观锁 + 软删除全局过滤器 + ApplyConfigurationsFromAssembly） |
| 价值 | 把 11 个 BC 重复的 Outbox 表声明、乐观锁 shadow property、软删除查询过滤器、配置自动注册等横切关注点抽取到基类，消除约 680 行重复代码（与 EfCoreUnitOfWork 一起） |
| 适用场景 | 所有 BC 的 DbContext 都继承 BaseDbContext，无需各自重新实现 |
| 证据 | [handbook/06-storage-and-cache.md#L73-L155](file:///workspace/docs/handbook/06-storage-and-cache.md#L73-L155)：BaseDbContext 公共特性详解；[src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs)；[src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs#L33-L53](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs#L33-L53)：泛型 UnitOfWork 消除各 BC 100% 同构副本 |

### G2.8 ReadModelSyncConsumerBase 读模型同步抽象

| 项 | 内容 |
|---|---|
| 名称 | ReadModelSyncConsumerBase<TEvent, TReadModel> 消费集成事件投影到 ES |
| 价值 | 把"消费集成事件→构建读模型→索引到 Elasticsearch"的通用流程抽象到基类，子类只需实现 `BuildReadModelAsync` 与可选的 `BuildDeleteActionAsync`，统一了读模型同步的编程模型 |
| 适用场景 | 所有需要从写库同步到 ES 读库的场景（商品搜索、订单查询、店铺 Dashboard 等） |
| 证据 | [src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs#L16-L80](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs#L16-L80)：删除分支优先于索引分支，失败抛异常触发 MassTransit 重试与死信 |

### G2.9 共享内核 vs 共享契约清晰分层

| 项 | 内容 |
|---|---|
| 名称 | Leno.SharedKernel（代码共享）与 Leno.SharedContracts（契约共享）分层 |
| 价值 | 严格区分"实现共享"（基础抽象类、值对象）与"契约共享"（DTO/Event schema），避免业务模型污染共享内核，符合 DDD 共享内核的"双刃剑"约束 |
| 适用场景 | 跨 BC 复用基础类与契约 |
| 证据 | [handbook/03-architecture-overview.md#L307-L326](file:///workspace/docs/handbook/03-architecture-overview.md#L307-L326)：共享内核 vs 共享契约对比表 |

### G2.10 M5.1/M5.3 可观测性增强

| 项 | 内容 |
|---|---|
| 名称 | OpenTelemetry 全链路追踪 + Prometheus 指标 + Serilog 结构化日志三件套 |
| 价值 | 跨 BC 调用链可追溯、关键指标可监控、日志可脱敏可检索，构成"可观测性三支柱" |
| 适用场景 | 生产环境故障排查、性能分析、容量规划 |
| 证据 | [src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs)；[src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs)；[src/BuildingBlocks/Leno.Infrastructure/Logging/SensitiveDataDestructurer.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Logging/SensitiveDataDestructurer.cs) |

---

## G3 架构缺点

### G3.1 Outbox 旁路（系统级问题 D2）

| 项 | 内容 |
|---|---|
| 名称 | 5 个 BC 存在 SaveChangesAsync 或 PublishAsync 旁路 Outbox 的代码 |
| 根因 | 开发者对"必须经 Outbox 发布集成事件"的约束理解不彻底，部分代码直接调 `_context.SaveChangesAsync()` 或 `_eventBus.PublishAsync()`，绕开 `IUnitOfWork.SaveEntitiesAsync` |
| 影响 | 业务事务与消息发送原子性被破坏，可能出现"业务提交了但消息丢失"（库存不扣、积分不发）或"业务回滚了但消息已发"（下游误扣库存）的分布式一致性故障 |
| 证据 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D2 章节）；[01-userauth.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md)；[05-promotion.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md)；[07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md)（PM-H05 ExchangeCouponAppService 旁路 Outbox）；[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)（H-02 SystemConfigAppService/AnnouncementAppService 旁路 Outbox）；[03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md) |

### G3.2 gRPC Guid→int64 POC 残留生产环境（系统级问题 D5）

| 项 | 内容 |
|---|---|
| 名称 | 4 个 BC（Order/Product/ReviewAfterSales/SellerShop）的 GrpcService 仍用 `(long)guid.GetHashCode()` 序列化 Guid |
| 根因 | ADR-0006 记录的 POC 阶段简化策略本应仅用于验证 gRPC 通信链路，但生产化阶段未及时按 ADR-0007 迁移到 `string xxx_id_str` 字段 |
| 影响 | `GetHashCode()` 存在碰撞风险（不同 Guid 可能产生相同 int64），导致跨 BC ID 透传时可能错配对象，引发数据错乱（如订单查到错误 SKU、评价归属错用户） |
| 证据 | [decisions/0006-guid-int64-poc-simplification-history.md#L17-L33](file:///workspace/docs/decisions/0006-guid-int64-poc-simplification-history.md#L17-L33)：明确"仅 POC 阶段使用，不进入生产环境"；[decisions/0007-guid-string-migration-strategy.md#L6-L9](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md#L6-L9)：待迁移 .proto 清单（6 个文件）记录在 plan §11.2，按文件逐步推进；[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)、[02-product.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md)、[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)（ReviewGrpcService L75-L103）、[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |

### G3.3 跨 BC 集成事件契约不对齐（系统级问题 D1）

| 项 | 内容 |
|---|---|
| 名称 | 跨 BC 集成事件契约字段不对齐，消费方缺关键字段 |
| 根因 | 集成事件 schema 由发布方独立维护，缺少跨 BC 契约评审机制，发布方新增字段后未同步通知消费方 |
| 影响 | 消费方因缺字段无法完成业务逻辑（如 RefundSucceededEventConsumer 缺 ChannelRefundNo 无法发起退款），或被迫硬编码兜底值引发数据错误 |
| 证据 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D1 章节）；[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)（RefundSucceededEventConsumer 缺 ChannelRefundNo，L67, L107-L163） |

### G3.4 共享内核污染（系统级问题 D3）

| 项 | 内容 |
|---|---|
| 名称 | `Money` 值对象在 Product/Promotion/Order/Cart 多处出现截断 vs 取舍不一致 |
| 根因 | 共享内核的 `Money` 值对象未约束小数位处理策略，各 BC 各自定义截断（`Truncate`）或取舍（`Round`）逻辑，导致跨 BC 金额传递时出现"分位差" |
| 影响 | 订单金额、促销优惠、支付金额三方对账时可能出现 1-2 分差异，长期累积引发财务对账失败 |
| 证据 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D3 章节）；[02-product.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md)、[05-promotion.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md)、[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)、[03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md) |

### G3.5 重复实现（系统级问题 D6）

| 项 | 内容 |
|---|---|
| 名称 | 多 BC 重复实现相同能力（InMemoryRefreshTokenStore 生产误注册、CacheService 非线程安全 Random、AntiCorruptionMetrics 静态 Dictionary 竞态） |
| 根因 | 缺少跨 BC 的"公共能力下沉"机制，各 BC 各自实现一份相似代码，未及时抽取到 BuildingBlocks |
| 影响 | 重复代码增加维护成本，且各副本可能有不同 bug（如 Random 非线程安全、Dictionary 竞态），修复时容易遗漏某个副本 |
| 证据 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D6 章节）；[12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md)（CacheService Random 非线程安全、AntiCorruptionMetrics 静态 Dictionary 竞态）；[01-userauth.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md)（InMemoryRefreshTokenStore 生产误注册） |

### G3.6 跨域事务边界不清（系统级问题 D4）

| 项 | 内容 |
|---|---|
| 名称 | Order Saga 编排、Payment 消费者、Notification 回执等跨域操作缺原子性保证 |
| 根因 | 跨聚合事务被设计为"最终一致"，但部分场景未明确 Saga 协调器与补偿动作，或补偿动作本身不幂等 |
| 影响 | 跨域操作失败时可能出现"半完成"状态（订单已创建但库存未扣、支付已成功但订单状态未推进、通知已发送但回执未持久化） |
| 证据 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)（D4 章节）；[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)（StockReservation 聚合旁路、ForceCancel 错误库存类型、PaymentSucceededEventConsumer 原子性）；[08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md)（通知回执未持久化） |

### G3.7 静态状态竞态（系统级问题）

| 项 | 内容 |
|---|---|
| 名称 | AntiCorruptionMetrics 静态 Dictionary 竞态、CacheService 非线程安全 Random |
| 根因 | 共享状态用静态字段持有但未加锁或用 `ConcurrentDictionary`/`ThreadLocal<Random>` |
| 影响 | 高并发下指标数据丢失或 Random 退化（多个线程拿到相同种子），指标可信度下降 |
| 证据 | [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md) |

### G3.8 设计期工厂硬编码 SA 密码（系统级问题）

| 项 | 内容 |
|---|---|
| 名称 | Cart/SellerShop/Notification 三个 BC 的 DesignTimeFactory 硬编码 `Password=Leno@SqlServer2019` |
| 根因 | 设计期工厂连接字符串"只用于生成迁移不实际连库"被理解为"硬编码可接受"，但密码硬编码仍是安全风险 |
| 影响 | 密码泄露到源码仓库，攻击者拿到源码即可尝试用该密码连接生产数据库（若生产密码相同或派生） |
| 证据 | [03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md)、[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md)（DesignTimeFactory 硬编码 SA 密码）、[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |

### G3.9 缓存失效缺位

| 项 | 内容 |
|---|---|
| 名称 | FeatureFlagCache/SystemConfigCache 永不失效，配置变更不生效 |
| 根因 | 缓存只写不删，缺少主动失效机制（如配置变更事件订阅、TTL 过期） |
| 影响 | 运维修改配置后需重启服务才生效，失去 Consul 配置中心"热更新"的核心价值 |
| 证据 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)（H-03 FeatureFlagCache/SystemConfigCache 永不失效） |

### G3.10 IDOR 越权风险集中

| 项 | 内容 |
|---|---|
| 名称 | PaymentsController IDOR（订单 ID 直查无归属校验）、ReviewAfterSales 多处缺归属校验 |
| 根因 | API 端点直接接收资源 ID 但不校验当前用户是否拥有该资源，依赖前端不传他人 ID 的"君子协定" |
| 影响 | 攻击者可遍历或猜测他人订单 ID/评价 ID，越权查询或操作他人数据，属于 OWASP Top 10 的 A01:2021 Broken Access Control |
| 证据 | [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)（PaymentsController IDOR）；[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)（Reject/ReturnGoods/Cancel/SellerReply 多处缺归属校验） |

---

## G4 技术债清单（Top 10，按"业务影响 × 修复成本"四象限分类）

### 四象限分类矩阵

```
                    修复成本 低                          修复成本 高
                ┌──────────────────────────┬──────────────────────────┐
                │  象限 I：速赢（高影响低成本的)  │  象限 II：战略性（高影响高成本）  │
  业务影响 高     │  - TD1 Outbox 旁路修复     │  - TD5 Guid→string 迁移          │
                │  - TD2 静态状态竞态加锁     │  - TD6 跨域 Saga 编排补全        │
                │  - TD3 DesignTime 密码外部化 │  - TD7 共享内核 Money 标准化     │
                │  - TD4 IDOR 归属校验补全     │                                  │
                ├──────────────────────────┼──────────────────────────┤
                │  象限 III：顺手做（低影响低成本的) │  象限 IV：暂缓（低影响高成本）   │
  业务影响 低     │  - TD8 死消费者清理         │  - TD9 ACL 适配器样板代码生成    │
                │  - TD9 ACL 适配器样板代码生成 │  - TD10 BFF 聚合层重构           │
                │                            │                                  │
                └──────────────────────────┴──────────────────────────┘
```

### Top 10 技术债详表

| 编号 | 名称 | 业务影响 | 修复成本 | 象限 | 证据 |
|---|---|---|---|---|---|
| TD1 | Outbox 旁路修复（5 个 BC） | 高（分布式一致性故障） | 低（改 SaveChangesAsync→SaveEntitiesAsync） | I 速赢 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D2；[07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md) PM-H05；[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) H-02；[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) H-05（DeadLetterQueueManager） |
| TD2 | 静态状态竞态加锁（AntiCorruptionMetrics、CacheService Random） | 高（指标失真、Random 退化） | 低（改 ConcurrentDictionary / ThreadLocal<Random>） | I 速赢 | [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md) |
| TD3 | DesignTimeFactory SA 密码外部化（3 个 BC） | 高（安全风险） | 低（改读环境变量） | I 速赢 | [03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md)、[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md)、[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |
| TD4 | IDOR 归属校验补全（PaymentsController、ReviewAfterSales 多处） | 高（OWASP A01 越权） | 低（每端点加 `userId == resource.OwnerId` 校验） | I 速赢 | [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md) |
| TD5 | Guid→string 迁移（6 个 .proto 文件 + 4 个 BC GrpcService/GrpcClient） | 高（ID 碰撞数据错乱） | 高（双写过渡 + 客户端逐步升级 + 旧字段废弃） | II 战略性 | [decisions/0007-guid-string-migration-strategy.md#L18-L29](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md#L18-L29)；[decisions/0006-guid-int64-poc-simplification-history.md](file:///workspace/docs/decisions/0006-guid-int64-poc-simplification-history.md) |
| TD6 | 跨域 Saga 编排补全（Order Saga、Payment 消费者、Notification 回执） | 高（跨域半完成状态） | 高（需设计 Saga 协调器 + 补偿动作 + 幂等保证） | II 战略性 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D4；[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)；[08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |
| TD7 | 共享内核 Money 标准化（小数位策略统一） | 高（财务对账分位差） | 高（需评审 4 个 BC 的小数位策略 + 统一迁移） | II 战略性 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D3 |
| TD8 | 死消费者清理（4 个 ReadModel 同步消费者） | 中（资源浪费 + 误导排查） | 低（删除或修复事件发布） | III 顺手做 | [07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md) PM-H03 |
| TD9 | ACL 适配器样板代码生成 | 低（维护成本） | 低（T4 模板或 Source Generator） | III 顺手做 | [decisions/0003-anticorruption-dispatcher-adapter-pattern.md](file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md) |
| TD10 | BFF 聚合层重构（ShopDashboardReadModelBuilder 6 字段硬编码 0） | 低（Dashboard 数据失真） | 高（需重新设计聚合查询 + 读模型） | IV 暂缓 | [10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |

---

## G5 优化方案

### G5.1 短期（1-2 周）：速赢修复

#### S1：Outbox 旁路全面修复（1 周）

**目标**：消除 5 个 BC 的 Outbox 旁路代码，所有集成事件发布强制经 `IUnitOfWork.SaveEntitiesAsync`。

**步骤**：
1. 全局搜索 `_context.SaveChangesAsync(` 与 `_eventBus.PublishAsync(`，逐处替换为 `_unitOfWork.SaveEntitiesAsync(ct)`
2. 对 DeadLetterQueueManager（H-05）改用 `SaveEntitiesAsync` 保证死信状态落库
3. CI 增加 Roslyn 分析器：禁止在 Infrastructure 层直接调 `SaveChangesAsync`，必须经 `IUnitOfWork`
4. 单元测试覆盖：验证 Outbox 表有对应记录

**证据**：[07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md) PM-H05；[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) H-02/H-05

#### S2：静态状态竞态加锁（3 天）

**目标**：AntiCorruptionMetrics 静态 Dictionary 改 `ConcurrentDictionary`，CacheService Random 改 `ThreadLocal<Random>` 或 `Random.Shared`。

**步骤**：
1. AntiCorruptionMetrics：`Dictionary<string, Counter>` → `ConcurrentDictionary<string, Counter>`，`TryGetValue + TryAdd` 组合或 `GetOrAdd`
2. CacheService：`new Random()` → `Random.Shared`（.NET 6+ 线程安全）
3. 单元测试：多线程并发压测验证无异常

**证据**：[12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md)

#### S3：DesignTimeFactory 密码外部化（1 天）

**目标**：3 个 BC 的 DesignTimeFactory 改读环境变量 `LENO_DESIGNTIME_CONNECTION_STRING`。

**步骤**：
1. Cart/SellerShop/Notification 三个 DesignTimeFactory 改用 `Environment.GetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING") ?? throw`
2. 开发文档说明本地需设置该环境变量
3. CI 流水线注入该环境变量

**证据**：[handbook/06-storage-and-cache.md#L346-L355](file:///workspace/docs/handbook/06-storage-and-cache.md#L346-L355)；[03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md)、[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md)、[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md)

#### S4：IDOR 归属校验补全（1 周）

**目标**：PaymentsController 与 ReviewAfterSales 所有写操作端点加归属校验。

**步骤**：
1. PaymentsController：所有按 orderId 查询/操作的端点先校验 `order.UserId == currentUser.Id`
2. ReviewAfterSales：Reject/ReturnGoods/Cancel/SellerReply/GetAfterSalesByOrder/GetReviewByOrderLine 全部加归属校验
3. 单元测试：用例覆盖"他人资源访问返回 403"

**证据**：[08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)

#### S5：SystemAdmin 指标误用修复（3 天）

**目标**：H-01 StatisticsAggregationService 用 `new Random()` 生成所有指标值的代码替换为真实查询。

**步骤**：
1. StatisticsAggregationService 改用真实 EF Core 查询聚合各指标
2. 单元测试：验证指标值与底层数据一致

**证据**：[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) H-01

### G5.2 中期（1-2 月）：战略性修复

#### M1：Guid→string 迁移启动（6 周）

**目标**：按 ADR-0007 完成 6 个 .proto 文件的 `string xxx_id_str` 字段新增与 GrpcService/GrpcClient 双写改造。

**步骤**：
1. 第 1 周：order.proto、product.proto 新增 `string xxx_id_str = N;`，原 `int64 xxx_id` 标记 `[deprecated = true]`
2. 第 2-3 周：Order/Product BC 的 GrpcService 双写 `int64` + `string`，GrpcClient 优先读 `string` 回退 `int64`
3. 第 4 周：review.proto、seller.proto、cart.proto、payment.proto 同步改造
4. 第 5-6 周：ReviewAfterSales/SellerShop/Cart/Payment BC 的 GrpcService/GrpcClient 改造
5. CI 监控 deprecated 字段使用情况，跟踪迁移进度

**证据**：[decisions/0007-guid-string-migration-strategy.md#L18-L29](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md#L18-L29)

#### M2：跨域 Saga 编排补全（6 周）

**目标**：为 Order Saga、Payment 消费者、Notification 回执设计明确的 Saga 协调器与补偿动作。

**步骤**：
1. 第 1-2 周：梳理 Order 创建→库存扣减→支付→发货的完整 Saga 状态机，绘制状态图
2. 第 3-4 周：实现 OrderSagaOrchestrator（基于状态机 + Outbox 事件驱动），定义补偿动作（如 ForceCancel 的库存类型修正、StockReservation 的回滚）
3. 第 5 周：Notification 回执持久化（消费发送结果事件，落库 NotificationRecord）
4. 第 6 周：Payment 消费者原子性保证（同一事务内更新支付单 + 发 PaymentSucceededEvent）
5. 集成测试：模拟各 BC 故障，验证补偿动作触发与最终一致

**证据**：[00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D4；[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)；[08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md)

#### M3：共享内核 Money 标准化（4 周）

**目标**：统一 4 个 BC 的 `Money` 小数位处理策略为"银行家舍入（Banker's Rounding）+ 2 位小数"。

**步骤**：
1. 第 1 周：评审会议确定统一策略（建议 `MidpointRounding.ToEven` + 2 位小数）
2. 第 2-3 周：`Money` 值对象增加 `Round(decimal, int, MidpointRounding)` 工厂方法，废弃直接构造
3. 第 4 周：Product/Promotion/Order/Cart 4 个 BC 的金额计算改用统一工厂方法
4. 财务对账测试：验证订单金额、促销优惠、支付金额三方一致

**证据**：[00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D3

#### M4：缓存失效机制补全（2 周）

**目标**：FeatureFlagCache/SystemConfigCache 订阅配置变更事件主动失效，或加合理 TTL。

**步骤**：
1. SystemConfigAppService 修改配置时发布 `SystemConfigChangedIntegrationEvent`
2. FeatureFlagCache/SystemConfigCache 订阅该事件，收到后清除对应 key
3. 兜底 TTL：FeatureFlag 5 分钟、SystemConfig 1 分钟
4. 集成测试：修改配置后验证 5 秒内生效

**证据**：[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) H-03

### G5.3 长期（3-6 月）：架构演进

#### L1：Guid→string 迁移完成 + int64 字段废弃（3 月）

**目标**：所有客户端升级到读 `string` 字段后，下一版 .proto 删除 `int64 xxx_id` 字段（符合 ADR-0005 major version 例外）。

**步骤**：
1. 监控 deprecated 字段使用情况，待所有客户端都读 `string` 后启动下线
2. .proto v2.0 版本删除 `int64 xxx_id` 字段，`buf breaking` 配置允许 major version 删除
3. GrpcService 移除 `int64` 写入逻辑，GrpcClient 移除回退逻辑
4. 文档更新：ADR-0006 标记为"已完全 superseded"

**证据**：[decisions/0007-guid-string-migration-strategy.md#L44-L47](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md#L44-L47)

#### L2：ACL 适配器样板代码自动化生成（2 月）

**目标**：用 Roslyn Source Generator 自动生成 `{Service}DispatcherAdapter`，消除各 BC 重复样板代码。

**步骤**：
1. 设计 Source Generator：扫描 `IAntiCorruptionService` 接口，自动生成 `XxxDispatcherAdapter` 包装类
2. 各 BC 删除手写的 Adapter 类，改用生成代码
3. 单元测试验证生成代码与手写代码行为一致

**证据**：[decisions/0003-anticorruption-dispatcher-adapter-pattern.md](file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md)

#### L3：BFF 聚合层重构（4 月）

**目标**：重新设计 ShopDashboardReadModelBuilder 等 6 字段硬编码 0 的聚合查询，基于真实读模型物化。

**步骤**：
1. 梳理 Dashboard 真实数据需求（订单数、销售额、商品数、评价数、退款数、活跃度）
2. 各 BC 发布对应聚合事件，BFF 订阅物化 Dashboard 读模型
3. ShopDashboardReadModelBuilder 改为读 ES 读模型而非硬编码 0
4. 前端联调验证 Dashboard 数据真实

**证据**：[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md)

#### L4：跨 BC 契约评审机制建立（持续）

**目标**：建立跨 BC 集成事件契约的评审与版本化机制，避免 D1 契约不对齐问题复发。

**步骤**：
1. 所有集成事件 schema 集中到 `Leno.SharedContracts/Events/` 目录，PR 修改需触发跨 BC 评审
2. CI 校验：消费方代码引用的集成事件字段必须在 schema 中存在（基于 Roslyn 分析或反射）
3. 集成事件 schema 版本号（`SchemaVersion`）演进规则文档化，新增字段递增版本号，消费方按版本路由
4. 跨 BC 契约变更周会：每周评审本周集成事件 schema 变更，确保消费方知晓

**证据**：[handbook/05-cross-bc-communication.md#L108-L109](file:///workspace/docs/handbook/05-cross-bc-communication.md#L108-L109)（集成事件必须版本化规则）；[00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D1

---

## G6 风险评估（Top 5 生产风险）

| 编号 | 风险名称 | 严重度 | 触发条件 | 影响 | 缓解措施 | 证据 |
|---|---|---|---|---|---|---|
| R1 | gRPC Guid→int64 碰撞导致跨 BC ID 错配 | 🔴 高 | 4 个 BC（Order/Product/ReviewAfterSales/SellerShop）的 GrpcService 仍用 `(long)guid.GetHashCode()`，碰撞概率虽低但量大时必然发生 | 跨 BC ID 错配引发订单查到错误 SKU、评价归属错用户、店铺数据错乱等数据错乱，且难以排查（int64 字段值看起来"正常"） | 立即启动 M1 迁移（中期方案），短期在 GrpcClient 增加碰撞日志告警 | [decisions/0006-guid-int64-poc-simplification-history.md#L17-L33](file:///workspace/docs/decisions/0006-guid-int64-poc-simplification-history.md#L17-L33)；[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)、[02-product.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md)、[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)、[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |
| R2 | Outbox 旁路导致分布式一致性故障 | 🔴 高 | 5 个 BC 存在 `SaveChangesAsync` 或 `PublishAsync` 旁路 Outbox 的代码，业务事务与消息发送非原子 | "业务提交了但消息丢失"（库存不扣、积分不发）或"业务回滚了但消息已发"（下游误扣库存），引发超卖、错发积分、错扣库存等生产事故 | 立即执行 S1（短期方案），CI 增加 Roslyn 分析器禁止旁路 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D2；[07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md) PM-H05；[11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) H-02/H-05 |
| R3 | IDOR 越权导致用户数据泄露 | 🔴 高 | PaymentsController 直查 orderId 无归属校验、ReviewAfterSales 多处缺归属校验 | 攻击者遍历他人订单 ID/评价 ID，越权查询或操作他人数据，属于 OWASP A01:2021 Broken Access Control，可能触发合规风险（如个人信息保护法） | 立即执行 S4（短期方案），所有按资源 ID 查询/操作的端点加归属校验 | [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md) |
| R4 | 跨域 Saga 缺补偿动作导致半完成状态 | 🟡 中 | Order Saga、Payment 消费者、Notification 回执等跨域操作缺原子性保证，部分场景未明确 Saga 协调器与补偿动作 | 跨域操作失败时出现"半完成"状态（订单已创建但库存未扣、支付已成功但订单状态未推进、通知已发送但回执未持久化），需人工介入修复数据 | 中期执行 M2（Saga 编排补全），短期增加跨域操作失败告警 + 人工对账脚本 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) D4；[04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)；[08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)；[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |
| R5 | DesignTimeFactory SA 密码泄露 | 🟡 中 | Cart/SellerShop/Notification 三个 BC 的 DesignTimeFactory 硬编码 `Password=Leno@SqlServer2019`，源码仓库可见 | 密码泄露到源码仓库，攻击者拿到源码即可尝试用该密码连接生产数据库（若生产密码相同或派生），构成横向移动风险 | 立即执行 S3（短期方案），改读环境变量；若该密码曾用于生产，立即轮换生产 SA 密码 | [03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md)、[10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md)、[09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |

---

## G7 与业界实践对比

### G7.1 与 Microsoft eShopOnContainers 对比

eShopOnContainers 是微软官方的 .NET 微服务参考架构，采用 DDD + CQRS + 事件驱动 + 微服务四件套，与 Leno 目标架构高度一致。

| 维度 | eShopOnContainers | Leno | Leno 差距 | Leno 优势 |
|---|---|---|---|---|
| BC 划分 | 6 个 BC（Basket/Catalog/Identity/Ordering/Payment/WebShoppingAgg） | 11 个 BC + 1 BFF | 业务子域划分更细，但部分 BC（ReviewAfterSales）混合两个子域 | BC 颗粒度更细，业务边界更清晰 |
| CQRS 实现 | MediatR（IRequestHandler）+ Dapper 读侧 | IQueryHandler<,> + Elasticsearch 读侧（无 MediatR） | Leno 无 MediatR 依赖更轻量，但需自维护 DI 反射注册 | 依赖更少，学习成本更低 |
| 事件驱动 | Outbox 模式 + RabbitMQ + MassTransit + IntegrationEventLog | Outbox 两阶段标记 + RabbitMQ + MassTransit + IntegrationEventConsumerBase 幂等基类 | eShopOnContainers 的 IntegrationEventLog 较简单，Leno 增加两阶段标记与并行发布 | Leno 的 Outbox 实现更工业级（两阶段 + 并行 + 告警） |
| ACL 模式 | 无显式防腐层抽象，直接 HttpClient 调用 | AntiCorruptionBase + AntiCorruptionDispatcher 双轨 + 三态熔断 | eShopOnContainers 无 ACL 抽象 | Leno 的 ACL 双轨 + 熔断是显著优势 |
| gRPC | 部分服务支持 gRPC | gRPC + HTTP 双轨 + Consul KV 热切换 + ADR-0001/0002/0003 配套 | eShopOnContainers 的 gRPC 较简单 | Leno 的 gRPC 双轨方案更成熟 |
| 服务发现 | 不使用 Consul，依赖 K8s Service | Consul 服务发现 + KV 配置中心 | eShopOnContainers 依赖 K8s 原生能力 | Leno 的 Consul 配置中心支持热更新 |
| 可观测性 | Application Insights + Serilog | OpenTelemetry + Prometheus + Serilog | 两者均齐全 | Leno 用 OTel 更云原生中立 |
| 部署 | K8s + Helm | K8s + Helm + HPA + 探针 | 两者基本一致 | 两者持平 |
| 共享内核 | Shared kernel 较少 | Leno.SharedKernel + Leno.SharedContracts 分层 | 两者均有共享内核 | Leno 的共享内核 vs 共享契约分层更清晰 |
| ADR 追溯 | 无 ADR 文档 | 7 份 ADR 覆盖关键决策 | eShopOnContainers 缺决策追溯 | Leno 的 ADR 体系是显著优势 |

**对比结论**：Leno 在 ACL 双轨、Outbox 工业化、gRPC 双轨、Consul 配置中心、ADR 追溯五个方面**优于** eShopOnContainers；在 BC 颗粒度上更细但需注意子域混合问题；在 CQRS 实现上更轻量但需自维护 DI 注册。整体架构成熟度与 eShopOnContainers 持平或略优。

### G7.2 与 Amazon 电商参考架构对比

Amazon 电商参考架构（以 AWS Well-Architected Framework 的 E-commerce Lens 为代表）强调"小而自治的服务 + 事件驱动 + 最终一致 + 多级缓存 + 可观测性"。

| 维度 | Amazon 参考 | Leno | Leno 差距 | Leno 优势 |
|---|---|---|---|---|
| 服务拆分 | 按业务能力拆分，服务数量多（数十到上百） | 11 个 BC + 1 BFF | Amazon 颗粒度更细，Leno 偏粗 | Leno 颗粒度对中型电商合适 |
| 事件驱动 | EventBridge + SNS/SQS + Lambda | RabbitMQ + MassTransit + Outbox | Amazon 用云原生事件总线，Leno 自建消息总线 | Leno 自建方案可跨云部署 |
| 一致性 | 最终一致为主，Saga 编排补偿 | Outbox + 幂等消费，但 Saga 编排不完整 | Leno 的 Saga 编排（Order/Payment/Notification）缺补偿动作 | Leno 的 Outbox 骨架优于 Amazon 简单 SQS |
| 多级缓存 | CloudFront + ElastiCache + DAX | Redis 缓存 + 布隆过滤器 + 双删一致性 | Leno 缓存策略齐全（三防 + 双删） | Leno 缓存策略文档化更完整 |
| 可观测性 | CloudWatch + X-Ray + ServiceLens | OpenTelemetry + Prometheus + Serilog | 两者均齐全 | 两者持平 |
| 弹性 | 多可用区 + 自动扩缩 + 熔断 | HPA + 三态熔断 + Consul 配置热切换 | Leno 的三态熔断状态机设计更精细 | Leno 熔断设计更工业级 |
| 数据所有权 | 一服务一库（Database-per-Service） | 一 BC 一库（11 个独立 SQL Server） | 两者一致 | 两者持平 |
| 安全 | IAM + KMS + VPC + WAF | InternalApiKey + JWT + AES（部分 BC 弱） | Leno 的加密实践（如 UserAuth AES CBC 无 MAC）弱于 Amazon KMS | Leno 安全实践需加强 |

**对比结论**：Leno 在 Outbox 工业化、三态熔断、缓存策略三方面与 Amazon 持平甚至略优；在 Saga 编排完整性与安全实践两方面**弱于** Amazon；在服务颗粒度上偏粗但适合中型电商场景。整体架构与 Amazon 参考架构的差距主要在"运维成熟度"（Amazon 有云原生可观测性与安全托管服务）而非"架构设计"。

### G7.3 与 Alibaba COLA 对比

COLA（Clean Object-oriented and Layered Architecture）是阿里巴巴开源的应用架构框架，强调"分层 + 域 + 扩展点 + CQRS + 事件驱动"，在国内电商有较多落地。

| 维度 | COLA | Leno | Leno 差距 | Leno 优势 |
|---|---|---|---|---|
| 分层架构 | Controller + Application + Domain + Infrastructure（4 层） | Api + Application + Domain + Infrastructure（4 层） | 两者一致 | 两者持平 |
| 域设计 | 支持但不强制 DDD 战略设计 | 强制 DDD 战略设计（11 BC + 上下文映射） | COLA 较灵活，Leno 更规范 | Leno 的 DDD 战略设计落地更彻底 |
| CQRS | CommandBus + QueryService | IQueryHandler<,> + Elasticsearch 读模型 | COLA 的 CommandBus 较重，Leno 的 IQueryHandler 更轻量 | Leno 的 CQRS 实现更轻量 |
| 事件驱动 | EventBus + 事务消息（RocketMQ） | Outbox + RabbitMQ + MassTransit | COLA 用 RocketMQ 事务消息原生支持原子性，Leno 用 Outbox 模拟 | 两者目标一致，Leno 的 Outbox 更通用（不依赖 RocketMQ） |
| 扩展点 | ExtensionPoint + Extension（业务策略扩展） | 无显式扩展点抽象 | Leno 缺扩展点机制，业务策略用策略模式硬编码 | COLA 的扩展点更适合 SaaS 多租户 |
| 防腐层 | 无显式 ACL 抽象 | AntiCorruptionBase + AntiCorruptionDispatcher 双轨 + 熔断 | COLA 无 ACL 抽象 | Leno 的 ACL 双轨是显著优势 |
| 共享内核 | shared 模块（基础工具） | Leno.SharedKernel + Leno.SharedContracts 分层 | 两者均有共享模块 | Leno 的共享内核 vs 共享契约分层更清晰 |
| 部署 | 支持 K8s 但不强制 | K8s + Helm + HPA 强制 | Leno 部署更标准化 | Leno 部署更云原生 |
| ADR 追溯 | 无 ADR 文档 | 7 份 ADR 覆盖关键决策 | COLA 缺决策追溯 | Leno 的 ADR 体系是显著优势 |
| 工具链 | COLA Archetype 脚手架 + 代码生成 | 手写为主（无脚手架） | COLA 有脚手架提速新项目 | COLA 的脚手架更适合快速孵化新项目 |

**对比结论**：Leno 在 ACL 双轨、Outbox 工业化、ADR 追溯、DDD 战略设计落地四方面**优于** COLA；在扩展点机制与脚手架工具链两方面**弱于** COLA；在分层架构、CQRS、事件驱动三方面与 COLA 持平。整体架构成熟度与 COLA 持平或略优，但可借鉴 COLA 的扩展点机制与脚手架思路。

### G7.4 综合对比矩阵

| 维度 | eShopOnContainers | Amazon 参考 | COLA | Leno |
|---|---|---|---|---|
| DDD 战略设计 | 良 | 良 | 良（灵活） | **优（强制）** |
| CQRS 实现 | 良（MediatR） | 良 | 良（CommandBus） | **优（轻量 IQueryHandler）** |
| 事件驱动一致性 | 良 | 优（云原生） | 优（RocketMQ 事务消息） | 良（Outbox 骨架优，Saga 编排缺） |
| ACL 防腐层 | 差（无抽象） | 良 | 差（无抽象） | **优（双轨 + 熔断）** |
| 服务发现/配置 | 差（依赖 K8s） | 优（云原生） | 良 | **优（Consul KV 热更新）** |
| 可观测性 | 良 | 优（云原生） | 良 | 良 |
| 部署 | 良 | 优（云原生） | 良 | 优（Helm + HPA + 探针） |
| ADR 决策追溯 | 差 | 良 | 差 | **优（7 份 ADR）** |
| 安全实践 | 良 | **优** | 良 | 中（部分 BC 弱） |
| 扩展点机制 | 差 | 良 | **优** | 差 |
| 工具链/脚手架 | 良 | 优 | **优** | 中 |

### G7.5 Leno 的差异化优势与可借鉴方向

**Leno 的差异化优势**（业界少见的强项）：
1. ACL 双轨调度 + 三态熔断状态机（eShopOnContainers/COLA 均无）
2. Outbox 两阶段标记 + 并行发布 + 积压告警（比 eShopOnContainers 的 IntegrationEventLog 更工业级）
3. 7 份 ADR 决策追溯体系（业界少见）
4. 共享内核 vs 共享契约清晰分层（COLA 未明确区分）

**Leno 可借鉴的方向**：
1. 从 COLA 借鉴扩展点机制（ExtensionPoint + Extension）以支持业务策略灵活扩展
2. 从 Amazon 借鉴云原生安全实践（KMS 托管密钥替代 UserAuth AES CBC 无 MAC）
3. 从 Amazon 借鉴完整 Saga 编排模式（Step Functions 风格的状态机协调器）
4. 从 COLA 借鉴脚手架思路（COLA Archetype）以提速新 BC 孵化
5. 从 eShopOnContainers 借鉴 MediatR 的 Pipeline Behavior（如日志、验证、重试横切关注点）增强 IQueryHandler

---

## 附录：评估依据文件清单

### 阶段 1 BC 审计报告（12 份）

- [01-userauth.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md)
- [02-product.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md)
- [03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md)
- [04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md)
- [05-promotion.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md)
- [06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md)
- [07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md)
- [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md)
- [09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md)
- [10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md)
- [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md)
- [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md)

### 阶段 2 跨 BC 聚合报告

- [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md)

### 设计文档与 ADR

- [2026-07-21-code-audit-design.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit-design.md)
- [decisions/0001-grpc-dual-track-with-http-fallback.md](file:///workspace/docs/decisions/0001-grpc-dual-track-with-http-fallback.md)
- [decisions/0002-circuit-breaker-three-state-machine.md](file:///workspace/docs/decisions/0002-circuit-breaker-three-state-machine.md)
- [decisions/0003-anticorruption-dispatcher-adapter-pattern.md](file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md)
- [decisions/0004-iorderstatus-provider-refactor.md](file:///workspace/docs/decisions/0004-iorderstatus-provider-refactor.md)
- [decisions/0005-proto-backward-compatibility-constraint.md](file:///workspace/docs/decisions/0005-proto-backward-compatibility-constraint.md)
- [decisions/0006-guid-int64-poc-simplification-history.md](file:///workspace/docs/decisions/0006-guid-int64-poc-simplification-history.md)
- [decisions/0007-guid-string-migration-strategy.md](file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md)
- [architecture/anticorruption-pattern.md](file:///workspace/docs/architecture/anticorruption-pattern.md)

### 架构手册

- [handbook/03-architecture-overview.md](file:///workspace/docs/handbook/03-architecture-overview.md)
- [handbook/05-cross-bc-communication.md](file:///workspace/docs/handbook/05-cross-bc-communication.md)
- [handbook/06-storage-and-cache.md](file:///workspace/docs/handbook/06-storage-and-cache.md)
- [spec/00-需求文档总览与DDD架构.md](file:///workspace/docs/spec/00-需求文档总览与DDD架构.md)

### 源码

- [src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs)
- [src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs)
- [src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs)
- [src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs)
- [src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs)
- [src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs)

---

## 评估结论摘要

Leno 电商平台架构成熟度评分 **78.25/100**，处于 **L4 量化管理** 级别，距 L5 持续优化尚有约 12 分差距。架构骨架（DDD 战略设计、CQRS 读写分离、ACL 双轨 + 三态熔断、Outbox 两阶段标记、Consul 配置中心、Helm 部署、ADR 追溯）达到工业级水准，与 Microsoft eShopOnContainers、Alibaba COLA 等业界参考架构对比整体持平或略优。

主要短板集中在三个系统级问题：
1. **事件驱动一致性（70 分）**：Outbox 旁路（D2）、契约不对齐（D1）、事件循环与双发
2. **可观测性（75 分）**：静态状态竞态、缓存失效缺位、指标误用（H-01 Random 生成指标）
3. **安全与正确性**：gRPC Guid→int64 POC 残留（D5）、IDOR 越权（R3）、DesignTime 密码泄露（R5）

建议按短期（1-2 周）速赢修复 → 中期（1-2 月）战略性修复 → 长期（3-6 月）架构演进的节奏推进，优先处理 R1/R2/R3 三个 🔴 高严重度生产风险。
