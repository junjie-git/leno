# 阶段四：长期架构 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**日期**：2026-07-23
**输入**：[00-architecture-upgrade-plan.md](./00-architecture-upgrade-plan.md) 第四章 4.1/4.4 + 第六章 6.4 节
**前置依赖**：[03-phase3-mid-term.md](./03-phase3-mid-term.md) 全部完成（BC拆分完成、Saga状态机上线、安全升级完成）
**目标**：Infrastructure 拆包、多级缓存、多租户/国际化预留、持续优化。健康度 9.3 → 9.6（L5 持续优化达成）
**架构**：10 项任务，3 波 + 持续编排（Wave 1: 1串行 → Wave 2: 3并行 → Wave 3: 3并行 + 穿插3独立任务），严格遵循依赖图
**Tech Stack**：.NET 10, IMemoryCache, Redis Pub/Sub, SELECT FOR UPDATE SKIP LOCKED, Pact, Roslyn Source Generator

---

## 1. 范围与约束

### 1.1 实施范围

**目标**：Infrastructure 拆包、多级缓存、多租户/国际化预留、持续优化。健康度 9.3 → 9.6（L5 持续优化达成）。
**任务数**：10 项（母方案 6.4 节步骤 1-10）
**前置依赖**：阶段三全部完成（BC 拆分完成、Saga 状态机上线、安全升级完成）
**预估周期**：3-6 月

| Task | 步骤 | 任务 | 周期 | 兼容性风险 | 验收标准 |
|------|------|------|------|-----------|---------|
| 4.1 | 1 | Infrastructure 模块化拆包：9 子包 + 元包门面 | 6 周 | 低 | 按需引用，启动加速 30%+，元包向后兼容门面可用 |
| 4.2 | 2 | ACL 防腐层可插拔策略链：`IAclChannel` + 优先级熔断选择 | 4 周 | 中 | 新协议接入零侵入，gRPC/HTTP 双轨迁移完成 |
| 4.3 | 3 | BFF 聚合层 DAG 编排引擎：声明式 `AggregateBuilder` + 拓扑排序 + 并行调度 + 超时级联 | 6 周 | 低 | 复杂聚合场景声明式表达，`Parallel.ForEachAsync` 作为特例保留 |
| 4.4 | 4 | Outbox 分片发布器：按聚合根 ID 哈希分片 + `SELECT ... FOR UPDATE SKIP LOCKED` | 4 周 | 中 | 发布吞吐随实例数线性扩展，无损水平扩展 |
| 4.5 | 5 | 多级缓存 L1 Local + L2 Redis：`IMemoryCache` L1（5s TTL）+ Redis L2 + Pub/Sub 跨实例失效 | 4 周 | 中 | 热点 Key Redis QPS 下降 80%+，L1 跨实例失效验证 |
| 4.6 | 6 | Consul 配置 Schema 版本化与灰度发布 | 3 周 | 中 | 配置变更版本化，灰度发布机制可用 |
| 4.7 | 7 | 多租户预留：聚合根 + EF Configuration 增 `tenant_id` + 全局查询过滤器（仅预留扩展位） | 4 周 | 高 | 领域模型扩展位就绪，业务驱动时可直接落地 |
| 4.8 | 8 | 国际化预留：通知模板多语言变体 + 错误码本地化资源 + `IStringLocalizer`（仅预留扩展位） | 4 周 | 高 | 国际化扩展位就绪，模板 `Culture` 维度支持 |
| 4.9 | 9 | 积分会员 BC 拆分：`Points` BC + `Membership` BC，经 `PointsEarnedEvent`/`MemberLevelChangedEvent` 协作 | 6 周 | 高 | 独立伸缩（积分高频写、会员低频评估），故障隔离，数据库拆分完成 |
| 4.10 | 10 | 跨 BC 契约评审机制 + Pact 契约测试（持续，贯穿全阶段） | 持续 | 中 | Pact Broker 上线，跨 BC 调用契约锁定，breaking 变更自动检测 |

### 1.2 关键约束

- **本地构建验证**：subagent 写代码后 `dotnet build` / `dotnet test` 验证，失败则修复后提交（非 `[unverified]` 模式）
- **代码完整性**：遵循用户代码完整性强制契约——禁止占位符、TODO、空实现、截断输出，每函数完整实现
- **5 并行限制**：每批最多 4 个 subagent（留 1 个 slot 给主 agent 操作）
- **元包门面始终可用**：Infrastructure 拆包期间元包 `Leno.Infrastructure` 始终编译通过，老服务零改动
- **预留扩展位不实际落地**：多租户/国际化仅预留扩展位，业务驱动时再实际落地（DG-7/DG-8 决策）
- **Pact 培训前置**：步骤10 Pact 契约测试 CI 强制集成前需完成团队培训（DG-10 决策）

### 1.3 前置依赖核验清单

阶段三全部产出已就绪，逐项确认：

- [ ] 库存独立 BC（步骤 3.1）已完成迁移，`StockReservation` 聚合在 `Leno.Inventory.Domain`
- [ ] MassTransit Saga 状态机（步骤 3.2）已上线，`order_saga_states` 表正常运行
- [ ] Process Manager 模式（步骤 3.3）已部署，`OrderPaymentProcessManager` 全局协调
- [ ] 促销规则引擎（步骤 3.4）已抽象，`IPromotionRule` + JSON 配置化
- [ ] 评价售后 BC 拆分（步骤 3.5）完成，`Review` BC + `AfterSales` BC 独立
- [ ] AuthN/AuthZ BC 拆分（步骤 3.6）完成，`Identity` BC + `AccessControl` BC 独立
- [ ] OAuth/SSO 通用化（步骤 3.7）完成，`IOAuth2ProviderAdapter` 接入
- [ ] 支付渠道插件化（步骤 3.8）完成，`IEnumerable<IPaymentChannelAdapter>` 注入
- [ ] 通知中心渠道注册表（步骤 3.9）完成，`INotificationChannelRegistry` 上线
- [ ] 安全技术栈升级（步骤 3.10）完成，Argon2id + RS256 + KMS 已落地
- [ ] Cart SKU 快照本地化（步骤 3.11）完成
- [ ] CQRS 读模型 snapshot + replay（步骤 3.12）完成
- [ ] 阶段三全部 commit 已合并到 `main` 分支，CI 全绿
- [ ] 阶段三 4 个 BC 拆分后生产运行 ≥ 1 月无 P0/P1 问题（DG-6 触发条件）

---

## 2. 总体架构

### 2.1 任务依赖图

```
步骤1 Infrastructure模块化拆包 ──────────────────────────┐
   (9 子包 + 元包门面，为后续任务提供基础)                │
                                                         ▼
步骤2 ACL防腐层可插拔策略链 ──────┐    步骤3 BFF聚合层DAG编排引擎 ────┐
   (依赖拆包后的 AntiCorruption │       (依赖拆包后的 ApiGateway)     │
    子包)                       │                                    │
                                 ▼                                    ▼
步骤4 Outbox分片发布器 ────────────────────────────────────────────────┤
   (独立，依赖拆包后的 Persistence 子包)                                │
                                                                       ▼
步骤5 多级缓存L1+L2 ──────────────────────────────────────────────────┤
   (依赖拆包后的 Caching 子包)                                          │
                                                                       ▼
步骤6 Consul配置Schema版本化 ──────────────────────────────────────────┤
   (独立)                                                               │
                                                                       ▼
步骤7 多租户预留 ──────────┐  步骤8 国际化预留 ──────────┐  步骤9 积分会员BC拆分 ──┐
   (高风险，领域模型扩展位) │     (高风险，模板多语言)     │     (阶段三延续，独立)  │
                          ▼                             ▼                        ▼
                          步骤10 跨BC契约评审机制 + Pact契约测试（持续，贯穿全阶段）
```

### 2.2 波次编排（3 波 + 1 持续）

```
Wave 1（1 串行，基础设施）  Wave 2（3 并行）          Wave 3（3 并行）
┌──────────────────┐       ┌──────────┬──────────┬──────────┐  ┌──────────┬──────────┬──────────┐
│步骤1             │       │步骤2     │步骤3     │步骤4     │  │步骤7     │步骤8     │步骤9     │
│Infrastructure    │ ────► │ACL策略链 │BFF DAG   │Outbox分片│  │多租户预留│国际化预留│积分会员BC│
│模块化拆包        │       │引擎      │编排      │发布器    │  │          │          │拆分      │
│9子包+元包门面    │       │4周       │6周       │4周       │  │4周       │4周       │6周       │
│6周               │       └──────────┴──────────┴──────────┘  └──────────┴──────────┴──────────┘
└──────────────────┘                ↓                              ↓
   ↓ git commit                 ──────────────────────────────────→ Wave 3 启动
                              ┌──────────┐
                              │步骤5     │  (依赖 Wave 2 Caching 子包)
                              │多级缓存  │  Wave 2 完成后启动，与 Wave 3 并行
                              │L1+L2     │
                              │4周       │
                              └──────────┘
                              ┌──────────────────────────────────────────┐
                              │步骤6 Consul配置Schema版本化（独立，任意时机）│
                              │3周                                        │
                              └──────────────────────────────────────────┘
                              ┌──────────────────────────────────────────┐
                              │步骤10 跨BC契约评审 + Pact契约测试（持续）  │
                              │贯穿全阶段，从 Wave 1 开始                  │
                              └──────────────────────────────────────────┘
```

**波次依赖**：
- Wave 1 → Wave 2：步骤 2/3/4 依赖拆包后的子包（AntiCorruption/ApiGateway/Persistence）
- Wave 2 → 步骤 5：多级缓存依赖 Caching 子包
- Wave 3：步骤 7/8/9 相互独立，可与步骤 5/6 并行

**subagent 总数**：10 个
- Wave 1：1 个串行 subagent（步骤1）
- Wave 2：3 个并行 subagent（步骤2/3/4）
- Wave 3：3 个并行 subagent（步骤7/8/9）
- 穿插：3 个独立 subagent（步骤5/6/10，可与 Wave 2/3 并行）

### 2.3 BC 目录互斥矩阵

| 任务 | 修改目录 | 互斥对象 |
|------|---------|---------|
| 4.1 Infrastructure 拆包 | `src/BuildingBlocks/Leno.Infrastructure/` + 9 新项目 | Wave 1 独占 |
| 4.2 ACL 策略链 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/` | 与 4.3/4.4 并行 |
| 4.3 BFF DAG | `src/ApiGateway/Leno.ApiGateway/Bff/` | 与 4.2/4.4 并行 |
| 4.4 Outbox 分片 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/` | 与 4.2/4.3 并行 |
| 4.5 多级缓存 | `src/BuildingBlocks/Leno.Infrastructure.Caching/` | 与 4.7/4.8/4.9 并行 |
| 4.6 Consul 版本化 | `src/BuildingBlocks/Leno.Infrastructure/Configuration/` | 任意时机 |
| 4.7 多租户预留 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/` + 全 BC Configuration | 与 4.8/4.9 并行（需协调 Persistence 子包） |
| 4.8 国际化预留 | `src/Services/Notification/` + `src/BuildingBlocks/Leno.SharedContracts/` | 与 4.7/4.9 并行 |
| 4.9 积分会员拆分 | `src/Services/PointsMembership/` 拆为 `Points/` + `Membership/` | 与 4.7/4.8 并行 |
| 4.10 Pact 契约测试 | `tests/Contracts/` + CI 配置 | 贯穿全阶段，新增文件不冲突 |

---

## 3. 决策门（执行前必须通过）

阶段四特有决策门 DG-6 至 DG-10，执行前需逐项评估前置条件：

| 决策门 | 评估内容 | 修订触发条件 | 核验状态 |
|--------|---------|------------|---------|
| **DG-6** | Infrastructure 拆包：阶段三 BC 拆分是否完全稳定 | BC 拆分后生产运行 ≥ 1 月无 P0/P1 问题 | - [ ] 通过 |
| **DG-7** | 多租户预留：业务是否有 SaaS 多租户需求确认 | 业务方确认需求后再实际落地，否则仅保留扩展位 | - [ ] 通过 |
| **DG-8** | 国际化预留：业务是否有海外扩展计划 | 业务方确认海外计划后再实际落地 | - [ ] 通过 |
| **DG-9** | 积分会员 BC 拆分：阶段三 AuthN/AuthZ 拆分经验复盘 | 复盘拆分模式，优化迁移策略 | - [ ] 通过 |
| **DG-10** | Pact 契约测试：团队是否完成 Pact 培训 | 培训完成后再强制 CI 集成 | - [ ] 通过 |

**修订触发条件**：
- DG-6 不通过 → 推迟 Wave 1 启动，先处理阶段三遗留问题
- DG-7 不通过 → 步骤7 仅保留扩展位，不实际落地（默认状态）
- DG-8 不通过 → 步骤8 仅保留扩展位，不实际落地（默认状态）
- DG-9 不通过 → 推迟步骤9 启动，先复盘阶段三 BC 拆分经验
- DG-10 不通过 → 步骤10 仅完成 Pact Broker 搭建与样例契约，CI 强制集成推迟到培训完成后

---

## 4. Wave 1 详细编排（1 串行 subagent，基础设施）

### 4.1 步骤1：Infrastructure 模块化拆包（6 周，9 子包 + 元包门面）

**目标**：将单项目 `Leno.Infrastructure` 拆分为 9 个独立子包 + 1 个聚合元包门面，实现按需引用、启动加速 30%+、依赖图清晰。

**修改范围**：

| 操作 | 文件/项目路径 |
|------|-------------|
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.Caching/Leno.Infrastructure.Caching.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.EventBus/Leno.Infrastructure.EventBus.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/Leno.Infrastructure.AntiCorruption.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Leno.Infrastructure.Persistence.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.Telemetry/Leno.Infrastructure.Telemetry.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.RateLimiting/Leno.Infrastructure.RateLimiting.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.Auth/Leno.Infrastructure.Auth.csproj` |
| 新建项目 | `src/BuildingBlocks/Leno.Infrastructure.ReadModel/Leno.Infrastructure.ReadModel.csproj` |
| 修改项目 | `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`（转为元包门面） |
| 修改 | `Leno.sln`（新增 8 个项目引用） |
| 修改 | 全 BC 的 `*.csproj`（按需引用子包，或保留元包引用） |

**子包结构（9 个子包 + 1 元包门面）**：

```
Leno.Infrastructure/                          # 聚合元包门面（向后兼容）
├── Leno.Infrastructure.csproj                # 仅 PackageReference 9 子包
└── /
    ├── Leno.Infrastructure.Abstractions/     # 已有，无依赖（保留原状）
    ├── Leno.Infrastructure.Caching/          # Redis 缓存 + 布隆过滤器
    │   ├── CacheService.cs                   # 从 Leno.Infrastructure/Caching/ 迁移
    │   ├── RedisBloomFilter.cs               # 从 Leno.Infrastructure/Caching/ 迁移
    │   └── DependencyInjection/             # AddRedisCache 扩展方法
    ├── Leno.Infrastructure.EventBus/         # RabbitMQ + 幂等基类
    │   ├── IntegrationEventConsumerBase.cs   # 从 Leno.Infrastructure/EventBus/ 迁移
    │   ├── IntegrationEventPublisher.cs      # 从 Leno.Infrastructure/EventBus/ 迁移
    │   └── MassTransitExtensions.cs
    ├── Leno.Infrastructure.AntiCorruption/   # ACL 双轨 + 熔断
    │   ├── AntiCorruptionDispatcher.cs       # 从 Leno.Infrastructure/AntiCorruption/ 迁移
    │   ├── AntiCorruptionMetrics.cs          # 从 Leno.Infrastructure/AntiCorruption/ 迁移
    │   ├── GuidProtoConverter.cs             # 从 Leno.Infrastructure/AntiCorruption/ 迁移
    │   └── GrpcChannelAdapter.cs
    ├── Leno.Infrastructure.Persistence/      # BaseDbContext + UnitOfWork + Outbox
    │   ├── BaseDbContext.cs                  # 从 Leno.Infrastructure/Persistence/ 迁移
    │   ├── EfCoreUnitOfWork.cs               # 从 Leno.Infrastructure/Persistence/ 迁移
    │   ├── OutboxMessage.cs                  # 从 Leno.Infrastructure/Persistence/ 迁移
    │   ├── EfCoreOutboxPublisher.cs          # 从 Leno.Infrastructure/Persistence/ 迁移
    │   ├── AuditableEntityInterceptor.cs     # 从 Leno.Infrastructure/Persistence/ 迁移
    │   ├── DesignTimeDbContextFactoryBase.cs # 从 Leno.Infrastructure/Persistence/ 迁移
    │   └── Configuration/ConsulConfigWatcher.cs  # 从 Leno.Infrastructure/Configuration/ 迁移
    ├── Leno.Infrastructure.Telemetry/        # OTel + Serilog
    │   ├── SerilogConfig.cs                  # 从 Leno.Infrastructure/Logging/ 迁移
    │   ├── OpenTelemetryExtensions.cs        # 从 Leno.Infrastructure/Telemetry/ 迁移
    │   └── TraceIdEnricher.cs                # 从 Leno.Infrastructure/Logging/ 迁移
    ├── Leno.Infrastructure.RateLimiting/     # 限流器
    │   ├── RedisSlidingWindowRateLimiter.cs  # 从 Leno.Infrastructure/RateLimiting/ 迁移
    │   └── IRateLimiter.cs
    ├── Leno.Infrastructure.Auth/             # JWT + IDOR 校验
    │   ├── JwtTokenGenerator.cs              # 从 Leno.Infrastructure/Auth/ 迁移
    │   ├── ResourceOwnershipChecker.cs       # 从 Leno.Infrastructure/Auth/ 迁移
    │   └── JwtBlacklistService.cs            # 从 Leno.Infrastructure/Auth/ 迁移
    └── Leno.Infrastructure.ReadModel/        # ES 读模型同步
        ├── ElasticsearchReadModelStore.cs    # 从 Leno.Infrastructure/ReadModel/ 迁移
        └── IReadModelStore.cs
```

**元包门面 csproj 示例**：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Description>Leno Infrastructure 聚合元包门面（向后兼容，按需引用子包可替代）</Description>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Leno.Infrastructure.Abstractions\Leno.Infrastructure.Abstractions.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.Caching\Leno.Infrastructure.Caching.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.EventBus\Leno.Infrastructure.EventBus.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.AntiCorruption\Leno.Infrastructure.AntiCorruption.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.Persistence\Leno.Infrastructure.Persistence.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.Telemetry\Leno.Infrastructure.Telemetry.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.RateLimiting\Leno.Infrastructure.RateLimiting.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.Auth\Leno.Infrastructure.Auth.csproj" />
    <ProjectReference Include="..\Leno.Infrastructure.ReadModel\Leno.Infrastructure.ReadModel.csproj" />
  </ItemGroup>
</Project>
```

**子包依赖关系**（无循环）：

```
Abstractions ←─ Caching ←─ Persistence
                ↑              ↑
            EventBus      AntiCorruption
                ↑              ↑
            ReadModel      RateLimiting
                ↑              ↑
            Telemetry         Auth
```

**subagent 指令要点**：

- 使用 `superpowers:subagent-driven-development` 执行，逐项勾选 checkbox
- 严格遵循"先建子包项目 → 迁移源文件 → 更新元包 → 全量 build 验证"流程
- 每个子包迁移完成后独立 `dotnet build` 子包项目，确保无编译错误
- 子包之间依赖通过 `ProjectReference` 声明，禁止反向依赖（Persistence 不可引用 Caching）
- 命名空间保持不变（`Leno.Infrastructure.Caching` 等），确保老服务零改动
- 元包 `Leno.Infrastructure` 仅保留 csproj 和 Directory.Build.props，源文件全部迁移到子包
- 全 BC 的 `*.csproj` 保持对元包的引用，仅在性能敏感服务（如 Product BC 启动慢）按需切到子包

**实施步骤**：

- [ ] 4.1.1 新建 8 个子包 csproj 项目文件（Caching/EventBus/AntiCorruption/Persistence/Telemetry/RateLimiting/Auth/ReadModel），TargetFramework=net10.0
- [ ] 4.1.2 为每个子包 csproj 配置 NuGet 包引用（从原 `Leno.Infrastructure.csproj` 拆分对应 PackageReference）
  - Caching: StackExchange.Redis, Microsoft.Extensions.Caching.Memory
  - EventBus: MassTransit.RabbitMQ, MassTransit.Extensions.DependencyInjection
  - AntiCorruption: Grpc.Net.Client, Polly
  - Persistence: Microsoft.EntityFrameworkCore.SqlServer, EFCore.BulkExtensions
  - Telemetry: OpenTelemetry.Extensions.Hosting, Serilog.AspNetCore
  - RateLimiting: StackExchange.Redis
  - Auth: Microsoft.IdentityModel.Tokens, System.IdentityModel.Tokens.Jwt
  - ReadModel: Elasticsearch.Net
- [ ] 4.1.3 迁移 Caching 子包源文件：`CacheService.cs`、`RedisBloomFilter.cs`、`ICacheService.cs`、`DependencyInjection/CacheServiceCollectionExtensions.cs`
- [ ] 4.1.4 迁移 EventBus 子包源文件：`IntegrationEventConsumerBase.cs`、`IntegrationEventPublisher.cs`、`MassTransitExtensions.cs`、`IntegrationEventBase.cs`（如在此处）
- [ ] 4.1.5 迁移 AntiCorruption 子包源文件：`AntiCorruptionDispatcher.cs`、`AntiCorruptionMetrics.cs`、`GuidProtoConverter.cs`、`GrpcChannelAdapter.cs`、`HttpChannelAdapter.cs`
- [ ] 4.1.6 迁移 Persistence 子包源文件：`BaseDbContext.cs`、`EfCoreUnitOfWork.cs`、`OutboxMessage.cs`、`EfCoreOutboxPublisher.cs`、`AuditableEntityInterceptor.cs`、`DesignTimeDbContextFactoryBase.cs`、`Configuration/ConsulConfigWatcher.cs`
- [ ] 4.1.7 迁移 Telemetry 子包源文件：`SerilogConfig.cs`、`OpenTelemetryExtensions.cs`、`TraceIdEnricher.cs`、`Logging/LogEnricherExtensions.cs`
- [ ] 4.1.8 迁移 RateLimiting 子包源文件：`RedisSlidingWindowRateLimiter.cs`、`IRateLimiter.cs`、`SlidingWindowRateLimiterOptions.cs`
- [ ] 4.1.9 迁移 Auth 子包源文件：`JwtTokenGenerator.cs`、`ResourceOwnershipChecker.cs`、`JwtBlacklistService.cs`、`IResourceOwnershipChecker.cs`
- [ ] 4.1.10 迁移 ReadModel 子包源文件：`ElasticsearchReadModelStore.cs`、`IReadModelStore.cs`、`ReadModelProjectorBase.cs`
- [ ] 4.1.11 修改 `Leno.Infrastructure.csproj` 为元包门面：删除所有 PackageReference，仅保留对 9 个子包的 ProjectReference
- [ ] 4.1.12 修改 `Leno.sln`，新增 8 个项目引用
- [ ] 4.1.13 全量 `dotnet build Leno.sln` 验证零错误零警告
- [ ] 4.1.14 依赖图分析：运行 `dotnet list package --include-transitive` 验证无循环依赖
- [ ] 4.1.15 选择性切换：将 Product BC（启动慢的服务）的 `Leno.Infrastructure` 引用改为按需引用 `Leno.Infrastructure.Caching` + `Leno.Infrastructure.Persistence` + `Leno.Infrastructure.Auth`
- [ ] 4.1.16 启动性能基准对比：Product BC 启动时间下降 30%+ 验证
- [ ] 4.1.17 单元测试：每个子包新增项目 `tests/BuildingBlocks/Leno.Infrastructure.{子包}.Tests/`，迁移原 `Leno.Infrastructure.Tests` 中对应测试
- [ ] 4.1.18 `dotnet test` 全绿，新增/修改代码覆盖率 ≥ 80%
- [ ] 4.1.19 commit：`[phase4][Infrastructure] 4.1: modularize into 9 sub-packages + meta-package facade`

**验收标准**：
- [ ] 9 个子包项目独立编译通过
- [ ] 元包 `Leno.Infrastructure` 编译通过，全 BC 零改动
- [ ] `dotnet build Leno.sln` 零错误零警告
- [ ] 依赖图无循环（`dotnet list package --include-transitive` 验证）
- [ ] Product BC 启动加速 ≥ 30%
- [ ] 全部测试通过，覆盖率 ≥ 80%

**风险与回滚**：
- 元包门面始终可用，子包迁移失败时回退到单项目结构（git revert 单次 commit）

---

## 5. Wave 2 详细编排（3 并行 subagent）

### 5.1 步骤2：ACL 防腐层可插拔策略链（4 周，依赖拆包后 AntiCorruption 子包）

**目标**：将 `AntiCorruptionDispatcher` 双轨调度硬编码演进为可插拔策略链，新增协议（消息总线异步化、本地内存缓存兜底）零侵入接入。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/IAclChannel.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AclChannelBase.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/GrpcAclChannel.cs`（重构自 `GrpcChannelAdapter.cs`） |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/HttpAclChannel.cs`（重构自 `HttpChannelAdapter.cs`） |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AclChannelRegistry.cs` |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruptionDispatcher.cs`（重构为策略链调度） |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/DependencyInjection/AntiCorruptionExtensions.cs`（注册 IAclChannel） |
| 新建测试 | `tests/BuildingBlocks/Leno.Infrastructure.AntiCorruption.Tests/AclChannelRegistryTests.cs` |

**关键代码抽象 — `IAclChannel` 接口**：

```csharp
namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 防腐层通道抽象：每个协议（gRPC/HTTP/消息总线）实现一个 channel，
/// 调度器按优先级 + 熔断状态选择可用 channel。
/// </summary>
public interface IAclChannel
{
    /// <summary>通道唯一标识（如 "grpc", "http", "message-bus"）</summary>
    string Name { get; }

    /// <summary>优先级（数值越小优先级越高，0 = 最高）</summary>
    int Priority { get; }

    /// <summary>是否支持同步请求-响应语义（消息总线异步通道返回 false）</summary>
    bool SupportsSynchronous { get; }

    /// <summary>
    /// 发送请求并返回响应。失败抛 AclChannelException。
    /// </summary>
    Task<AclResponse> SendAsync(AclRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查：用于调度器选择前判断通道可用性。
    /// 返回 false 则调度器跳过该通道并触发熔断评估。
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

public sealed record AclRequest(
    string OperationName,
    string TargetService,
    IReadOnlyDictionary<string, object> Payload,
    Guid TraceId = default);

public sealed record AclResponse(
    bool Success,
    IReadOnlyDictionary<string, object>? Body,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed class AclChannelException : Exception
{
    public string ChannelName { get; }
    public AclChannelException(string channelName, string message, Exception? inner = null)
        : base(message, inner) => ChannelName = channelName;
}
```

**策略链调度器重构（`AntiCorruptionDispatcher` 核心逻辑）**：

```csharp
public sealed class AntiCorruptionDispatcher
{
    private readonly IAclChannel[] _channels; // 按 Priority 升序排列
    private readonly CircuitBreakerState[] _breakerStates;
    private readonly ILogger<AntiCorruptionDispatcher> _logger;

    public AntiCorruptionDispatcher(IEnumerable<IAclChannel> channels, ILogger<AntiCorruptionDispatcher> logger)
    {
        _channels = channels.OrderBy(c => c.Priority).ToArray();
        _breakerStates = _channels.Select(_ => new CircuitBreakerState()).ToArray();
        _logger = logger;
    }

    public async Task<AclResponse> DispatchAsync(AclRequest request, CancellationToken ct = default)
    {
        for (int i = 0; i < _channels.Length; i++)
        {
            var channel = _channels[i];
            var breaker = _breakerStates[i];
            if (breaker.IsOpen) continue;
            if (!await channel.HealthCheckAsync(ct)) { breaker.RecordFailure(); continue; }

            try
            {
                var response = await channel.SendAsync(request, ct);
                breaker.RecordSuccess();
                return response;
            }
            catch (AclChannelException ex)
            {
                _logger.LogWarning(ex, "ACL channel {Channel} failed for {Operation}, trying next", channel.Name, request.OperationName);
                breaker.RecordFailure();
            }
        }
        throw new AclChannelException("all", $"All ACL channels exhausted for operation {request.OperationName}");
    }
}
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `IAclChannel` + `AclChannelRegistry` 单元测试 → 写接口实现 → 重构 `AntiCorruptionDispatcher`
- 保持对外 API 不变：`IAntiCorruptionService` 接口签名零修改，仅内部实现切换
- 现有 gRPC/HTTP 双轨迁移：将 `GrpcChannelAdapter` 重构为 `GrpcAclChannel : IAclChannel`，`HttpChannelAdapter` 重构为 `HttpAclChannel : IAclChannel`
- DI 注册：`services.AddSingleton<IAclChannel, GrpcAclChannel>(); services.AddSingleton<IAclChannel, HttpAclChannel>();`
- 双轨期：feature flag 按 BC 切流，旧 `AntiCorruptionDispatcher` 内部直接调用的路径保留 4 周过渡
- 熔断器：复用现有 `AntiCorruptionMetrics` 三态熔断逻辑（Closed/Open/HalfOpen）

**实施步骤**：

- [ ] 5.1.1 新建 `IAclChannel.cs` + `AclRequest.cs` + `AclResponse.cs` + `AclChannelException.cs`（按上方代码抽象）
- [ ] 5.1.2 新建 `AclChannelBase.cs` 抽象基类，封装公共逻辑（序列化、TraceId 注入、日志）
- [ ] 5.1.3 重构 `GrpcChannelAdapter.cs` → `GrpcAclChannel.cs` 实现 `IAclChannel`（Priority=0, SupportsSynchronous=true）
- [ ] 5.1.4 重构 `HttpChannelAdapter.cs` → `HttpAclChannel.cs` 实现 `IAclChannel`（Priority=1, SupportsSynchronous=true）
- [ ] 5.1.5 新建 `AclChannelRegistry.cs`：注入 `IEnumerable<IAclChannel>`，按 Priority 排序，提供 `GetAvailableChannels()` 查询
- [ ] 5.1.6 重构 `AntiCorruptionDispatcher.cs`：替换硬编码 gRPC+HTTP 调用为策略链遍历（按上方代码）
- [ ] 5.1.7 修改 `AntiCorruptionExtensions.cs`：注册 `IAclChannel` 实现到 DI 容器
- [ ] 5.1.8 修改 `AntiCorruptionMetrics.cs`：扩展为按 channel name 维度记录熔断状态
- [ ] 5.1.9 单元测试 `AclChannelRegistryTests.cs`：覆盖优先级排序、熔断跳过、健康检查失败回退、全通道耗尽异常
- [ ] 5.1.10 集成测试：`tests/Shared/AntiCorruptionIntegrationTests.cs`，Testcontainers + gRPC mock + HTTP mock 验证双轨切流
- [ ] 5.1.11 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 5.1.12 commit：`[phase4][AntiCorruption] 4.2: pluggable ACL strategy chain via IAclChannel`

**验收标准**：
- [ ] `IAclChannel` 接口实现 2 个通道（gRPC/HTTP），策略链按优先级调度
- [ ] 单通道失败自动降级到下一通道
- [ ] 全通道耗尽抛 `AclChannelException`
- [ ] 对外 `IAntiCorruptionService` 接口签名不变
- [ ] 单元测试覆盖率 ≥ 80%，集成测试验证双轨切流

### 5.2 步骤3：BFF 聚合层 DAG 编排引擎（6 周，依赖拆包后 ApiGateway）

**目标**：将 BFF 的 `Parallel.ForEachAsync` 无依赖并行聚合升级为 DAG 编排引擎，支持"先查用户再查用户订单"等依赖链场景，自动拓扑排序 + 最大化并行度。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `src/ApiGateway/Leno.ApiGateway/Bff/Dag/AggregateNode.cs` |
| 新建 | `src/ApiGateway/Leno.ApiGateway/Bff/Dag/AggregateBuilder.cs` |
| 新建 | `src/ApiGateway/Leno.ApiGateway/Bff/Dag/AggregateGraph.cs` |
| 新建 | `src/ApiGateway/Leno.ApiGateway/Bff/Dag/TopologicalSorter.cs` |
| 新建 | `src/ApiGateway/Leno.ApiGateway/Bff/Dag/DagOrchestrator.cs` |
| 新建 | `src/ApiGateway/Leno.ApiGateway/Bff/Dag/CascadeTimeoutPolicy.cs` |
| 修改 | `src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs`（保留作为特例） |
| 新建测试 | `tests/ApiGateway/Leno.ApiGateway.Bff.Dag.Tests/DagOrchestratorTests.cs` |

**关键代码抽象 — `AggregateBuilder` 声明式 API**：

```csharp
namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// 声明式聚合构建器：通过 AddNode + DependsOn 描述 DAG，
/// Build() 返回 AggregateGraph 交由 DagOrchestrator 执行。
/// </summary>
public sealed class AggregateBuilder
{
    private readonly Dictionary<string, AggregateNode> _nodes = new();

    public AggregateBuilder AddNode(
        string name,
        Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> executor,
        TimeSpan? timeout = null)
    {
        if (_nodes.ContainsKey(name))
            throw new InvalidOperationException($"Node '{name}' already exists in the aggregate graph.");
        _nodes[name] = new AggregateNode(name, executor, timeout ?? TimeSpan.FromSeconds(5));
        return this;
    }

    public AggregateBuilder DependsOn(string dependent, params string[] dependencies)
    {
        if (!_nodes.TryGetValue(dependent, out var node))
            throw new InvalidOperationException($"Node '{dependent}' not found.");
        foreach (var dep in dependencies)
        {
            if (!_nodes.ContainsKey(dep))
                throw new InvalidOperationException($"Dependency '{dep}' not found.");
            node.Dependencies.Add(dep);
        }
        return this;
    }

    public AggregateGraph Build()
    {
        // 拓扑排序验证无环
        var sorted = TopologicalSorter.Sort(_nodes.Values);
        return new AggregateGraph(_nodes, sorted);
    }
}

public sealed class AggregateNode
{
    public string Name { get; }
    public Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> Executor { get; }
    public TimeSpan Timeout { get; }
    public HashSet<string> Dependencies { get; } = new();

    public AggregateNode(string name, Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> executor, TimeSpan timeout)
    {
        Name = name; Executor = executor; Timeout = timeout;
    }
}
```

**DAG 编排引擎核心（`DagOrchestrator` 并行调度）**：

```csharp
public sealed class DagOrchestrator
{
    private readonly CascadeTimeoutPolicy _cascadePolicy;
    private readonly ILogger<DagOrchestrator> _logger;

    public DagOrchestrator(CascadeTimeoutPolicy cascadePolicy, ILogger<DagOrchestrator> logger)
    {
        _cascadePolicy = cascadePolicy;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(AggregateGraph graph, CancellationToken overallToken = default)
    {
        var results = new ConcurrentDictionary<string, object?>();
        var completed = new ConcurrentDictionary<string, byte>();
        var pending = new List<AggregateNode>(graph.SortedNodes);

        while (pending.Count > 0)
        {
            // 找出依赖已全部完成的节点
            var ready = pending.Where(n => n.Dependencies.All(d => completed.ContainsKey(d))).ToList();
            if (ready.Count == 0)
                throw new InvalidOperationException("DAG has unresolvable dependencies (cycle or missing).");

            // 并行执行就绪节点，每个节点独立超时 + 级联取消
            var cascadeCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
            try
            {
                await Parallel.ForEachAsync(ready, async (node, ct) =>
                {
                    using var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(cascadeCts.Token);
                    nodeCts.CancelAfter(node.Timeout);
                    try
                    {
                        var input = results.ToDictionary(kv => kv.Key, kv => kv.Value);
                        var result = await node.Executor(input, nodeCts.Token);
                        results[node.Name] = result;
                        completed.TryAdd(node.Name, 0);
                    }
                    catch (OperationCanceledException) when (nodeCts.IsCancellationRequested)
                    {
                        _logger.LogWarning("Node {Node} timed out, cascading cancellation to downstream", node.Name);
                        _cascadePolicy.OnNodeTimeout(node.Name);
                        throw;
                    }
                }, cascadeCts.Token);
            }
            finally
            {
                pending.RemoveAll(n => completed.ContainsKey(n.Name));
                cascadeCts.Dispose();
            }
        }
        return results;
    }
}
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `TopologicalSorter` + `DagOrchestrator` 单元测试 → 写实现 → 集成到 `BffForwarderService`
- `BffForwarderService` 现有 `Parallel.ForEachAsync` 路径保留为特例（无依赖场景下直接调用旧路径）
- 新增复杂聚合端点示例：`GET /api/aggregate/order-detail?orderId=xxx` 串联 user → order → order-items → product-snapshot
- 拓扑排序使用 Kahn 算法，验证有环图抛 `InvalidOperationException`
- 级联超时：节点超时自动取消下游节点，`CascadeTimeoutPolicy.OnNodeTimeout` 记录指标
- 现有 BFF 端点逐个迁移到 DAG 引擎，旧路径标注 `[Obsolete]` 4 周后删除

**实施步骤**：

- [ ] 5.2.1 新建 `AggregateNode.cs` + `AggregateBuilder.cs`（按上方代码抽象）
- [ ] 5.2.2 新建 `TopologicalSorter.cs`：实现 Kahn 算法，检测环并抛异常
- [ ] 5.2.3 新建 `AggregateGraph.cs`：持有节点字典 + 拓扑排序结果
- [ ] 5.2.4 新建 `CascadeTimeoutPolicy.cs`：节点超时级联取消下游，记录指标
- [ ] 5.2.5 新建 `DagOrchestrator.cs`（按上方代码）：并行调度就绪节点 + 级联超时
- [ ] 5.2.6 修改 `BffForwarderService.cs`：保留 `Parallel.ForEachAsync` 作为无依赖特例，新增 `ExecuteDagAsync(AggregateGraph)` 路径
- [ ] 5.2.7 新增端点示例 `GET /api/aggregate/order-detail`：声明式构建 user→order→items→snapshot DAG
- [ ] 5.2.8 单元测试 `DagOrchestratorTests.cs`：覆盖拓扑排序、并行执行、级联超时、有环异常、节点失败重试
- [ ] 5.2.9 集成测试：Testcontainers 模拟下游 BC，验证复杂聚合端点正确返回
- [ ] 5.2.10 性能基准：复杂聚合场景对比 `Parallel.ForEachAsync` 串行链式调用，验证并行度提升
- [ ] 5.2.11 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 5.2.12 commit：`[phase4][Bff] 4.3: DAG orchestration engine with AggregateBuilder + topological sort`

**验收标准**：
- [ ] 声明式 `AggregateBuilder` 支持节点声明 + 依赖关系
- [ ] 拓扑排序自动检测环
- [ ] 并行调度最大化并行度（无依赖节点同时执行）
- [ ] 节点超时级联取消下游
- [ ] 现有 `Parallel.ForEachAsync` 路径保留作为特例
- [ ] 单元测试覆盖率 ≥ 80%

### 5.3 步骤4：Outbox 分片发布器（4 周，依赖拆包后 Persistence 子包）

**目标**：将单实例 Outbox 发布器升级为多实例分片发布器，按聚合根 ID 哈希分片 + `SELECT FOR UPDATE SKIP LOCKED` 实现无损水平扩展。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Outbox/OutboxMessage.cs`（增 `ShardKey` 字段） |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Outbox/EfCoreOutboxPublisher.cs`（重构为分片发布） |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Outbox/IShardingStrategy.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Outbox/HashShardingStrategy.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Outbox/ShardedOutboxPublisher.cs` |
| 新建迁移 | 各 BC 的 `*.Infrastructure/Migrations/` 新增 `AddOutboxShardKeyColumn` 迁移 |
| 新建测试 | `tests/BuildingBlocks/Leno.Infrastructure.Persistence.Tests/ShardedOutboxPublisherTests.cs` |

**关键代码抽象 — 分片策略与发布器**：

```csharp
namespace Leno.Infrastructure.Persistence.Outbox;

/// <summary>分片策略：按聚合根 ID 计算分片键</summary>
public interface IShardingStrategy
{
    /// <summary>返回 0..ShardCount-1 的分片号</summary>
    int ComputeShard(Guid aggregateRootId, int shardCount);
}

public sealed class HashShardingStrategy : IShardingStrategy
{
    public int ComputeShard(Guid aggregateRootId, int shardCount)
    {
        // 一致性哈希：同一聚合根始终落在同一分片，保证事件顺序
        var hash = BitConverter.ToInt64(aggregateRootId.ToByteArray(), 0);
        return (int)(Math.Abs(hash) % shardCount);
    }
}

public sealed class ShardedOutboxPublisher
{
    private readonly IShardingStrategy _strategy;
    private readonly int _shardCount;
    private readonly int _instanceShard; // 当前实例负责的分片号
    private readonly IDbContextFactory<BaseDbContext> _dbContextFactory;
    private readonly ILogger<ShardedOutboxPublisher> _logger;

    public ShardedOutboxPublisher(
        IShardingStrategy strategy,
        int shardCount,
        int instanceShard,
        IDbContextFactory<BaseDbContext> dbContextFactory,
        ILogger<ShardedOutboxPublisher> logger)
    {
        _strategy = strategy; _shardCount = shardCount;
        _instanceShard = instanceShard;
        _dbContextFactory = dbContextFactory; _logger = logger;
    }

    public async Task PublishPendingAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        // SELECT FOR UPDATE SKIP LOCKED：多实例各管一片，无锁竞争
        var pending = await db.Set<OutboxMessage>()
            .FromSqlRaw(@"
                SELECT TOP ({0}) * FROM outbox_messages WITH (UPDLOCK, ROWLOCK, READPAST)
                WHERE shard_key = {1} AND processed_on IS NULL
                ORDER BY occurred_on
                FOR UPDATE SKIP LOCKED",
                _shardCount, _instanceShard)
            .ToListAsync(ct);

        foreach (var msg in pending)
        {
            try
            {
                await PublishToBusAsync(msg, ct);
                msg.ProcessedOn = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
                msg.RetryCount++;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
```

**OutboxMessage 表结构变更（增 `shard_key` 列）**：

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public int RetryCount { get; set; }
    public Guid AggregateRootId { get; set; }
    public int ShardKey { get; set; } // 新增：0..ShardCount-1，由 HashShardingStrategy 计算
}
```

**EF Configuration 新增**：

```csharp
builder.Property(o => o.ShardKey).HasColumnName("shard_key").IsRequired();
builder.HasIndex(o => new { o.ShardKey, o.ProcessedOn }).HasDatabaseName("ix_outbox_shard_processed");
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `HashShardingStrategy` + `ShardedOutboxPublisher` 单元测试 → 写实现 → 全 BC 迁移
- 实例分片号通过环境变量 `OUTBOX__SHARD_ID` 注入，`ShardCount` 通过 `OUTBOX__SHARD_COUNT` 配置
- SQL Server 2016+ 验证 `FOR UPDATE SKIP LOCKED` 语法（实际为 `WITH (UPDLOCK, ROWLOCK, READPAST)` + `FOR UPDATE`）
- 双轨期：feature flag 按 BC 切流，单实例发布器 `EfCoreOutboxPublisher` 保留 4 周过渡
- 数据迁移：为现有 `outbox_messages` 表回填 `shard_key`（按 `aggregate_root_id` 哈希）
- 全 BC 的 `*.Infrastructure/Migrations/` 新增 `AddOutboxShardKeyColumn` 迁移，Down 方法 DropColumn

**实施步骤**：

- [ ] 5.3.1 修改 `OutboxMessage.cs` 增 `ShardKey` 属性（int, Required）
- [ ] 5.3.2 修改各 BC 的 `OutboxMessageConfiguration.cs` 增 `shard_key` 列 + 复合索引 `ix_outbox_shard_processed`
- [ ] 5.3.3 新建迁移 `AddOutboxShardKeyColumn`：AddColumn + 回填 SQL `UPDATE outbox_messages SET shard_key = ABS(CONVERT(bigint, CONVERT(varbinary(8), aggregate_root_id))) % {ShardCount}`
- [ ] 5.3.4 新建 `IShardingStrategy.cs` + `HashShardingStrategy.cs`（按上方代码）
- [ ] 5.3.5 新建 `ShardedOutboxPublisher.cs`（按上方代码），`SELECT FOR UPDATE SKIP LOCKED` SQL
- [ ] 5.3.6 修改 `EfCoreOutboxPublisher.cs`：标注 `[Obsolete("Use ShardedOutboxPublisher")]`，保留 4 周过渡
- [ ] 5.3.7 修改 `OutboxPublishingHostedService.cs`：注入 `ShardedOutboxPublisher`，按 `OUTBOX__SHARD_ID`/`OUTBOX__SHARD_COUNT` 配置启动
- [ ] 5.3.8 各 BC `appsettings.json` 增 `Outbox:ShardId` / `Outbox:ShardCount` 配置项
- [ ] 5.3.9 单元测试 `ShardedOutboxPublisherTests.cs`：覆盖分片哈希一致性、SKIP LOCKED SQL 正确性、retry 计数
- [ ] 5.3.10 集成测试：Testcontainers SQL Server，多实例并发发布验证无重复无遗漏
- [ ] 5.3.11 性能压测：2/4/8 实例发布吞吐对比基线，验证线性扩展
- [ ] 5.3.12 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 5.3.13 commit：`[phase4][Persistence] 4.4: sharded Outbox publisher with SKIP LOCKED for horizontal scaling`

**验收标准**：
- [ ] `OutboxMessage.ShardKey` 字段存在，索引 `ix_outbox_shard_processed` 创建
- [ ] `HashShardingStrategy` 一致性哈希：同一聚合根始终落同一分片
- [ ] `SELECT FOR UPDATE SKIP LOCKED` SQL 正确，多实例无锁竞争
- [ ] 多实例发布吞吐随实例数线性扩展
- [ ] 单元测试覆盖率 ≥ 80%，集成测试验证无重复无遗漏

---

## 6. Wave 3 详细编排（3 并行 subagent）

### 6.1 步骤7：多租户预留（4 周，高风险，仅预留扩展位）

**目标**：在领域模型与数据库结构预留多租户扩展位（`tenant_id` 列 + 全局查询过滤器），业务驱动时可直接落地。**仅预留，不实际落地**（受 DG-7 决策门约束）。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Abstractions/MultiTenancy/ITenantEntity.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Abstractions/MultiTenancy/ITenantContext.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Abstractions/MultiTenancy/TenantContext.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/MultiTenancy/TenantQueryFilterInterceptor.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/MultiTenancy/MultiTenancyExtensions.cs` |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/BaseDbContext.cs`（注入全局查询过滤器） |
| 修改 | 各 BC 的聚合根实体类（实现 `ITenantEntity` 接口，标记而非实际落地） |
| 修改 | 各 BC 的 EF Configuration（声明 `tenant_id` 列，迁移仅新增 nullable 列） |
| 新建测试 | `tests/BuildingBlocks/Leno.Infrastructure.Persistence.Tests/MultiTenancy/TenantQueryFilterTests.cs` |

**关键代码抽象 — 多租户扩展位**：

```csharp
namespace Leno.Infrastructure.MultiTenancy;

/// <summary>支持多租户的实体标记接口（预留扩展位，DG-7 通过后实际启用）</summary>
public interface ITenantEntity
{
    /// <summary>租户 ID。当前阶段 nullable（默认 null = 单租户模式），DG-7 通过后改为 required。</summary>
    Guid? TenantId { get; set; }
}

/// <summary>租户上下文：从请求头/claim 中解析当前租户</summary>
public interface ITenantContext
{
    Guid? CurrentTenantId { get; }
    void SetTenant(Guid? tenantId);
}

public sealed class TenantContext : ITenantContext
{
    private readonly AsyncLocal<Guid?> _currentTenant = new();
    public Guid? CurrentTenantId => _currentTenant.Value;
    public void SetTenant(Guid? tenantId) => _currentTenant.Value = tenantId;
}
```

**全局查询过滤器（`BaseDbContext` 注入）**：

```csharp
// 在 BaseDbContext.OnModelCreating 中：
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
{
    if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
    {
        modelBuilder.Entity(entityType.ClrType)
            .Property(nameof(ITenantEntity.TenantId))
            .HasColumnName("tenant_id")
            .IsRequired(false); // 当前阶段 nullable，DG-7 通过后改 true

        modelBuilder.Entity(entityType.ClrType)
            .HasQueryFilter(e => EF.Property<Guid?>(e, nameof(ITenantEntity.TenantId)) == null
                                 || EF.Property<Guid?>(e, nameof(ITenantEntity.TenantId)) == _tenantContext.CurrentTenantId);
    }
}
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `ITenantEntity` + `TenantContext` 单元测试 → 写实现 → 选择性应用到 Notification/SystemAdmin BC
- **仅预留扩展位**：`tenant_id` 列 nullable（默认 null = 单租户模式），不修改业务逻辑
- 受 DG-7 决策门约束：业务方确认 SaaS 多租户需求后，将 `IsRequired(false)` 改为 `IsRequired(true)` 并回填租户 ID
- 全 BC 聚合根实现 `ITenantEntity` 接口（仅添加 `TenantId` 属性，不修改业务方法）
- Notification 偏好/模板 + SystemAdmin 审计日志/FeatureFlag 优先应用（按租户隔离预留）
- 迁移脚本：仅新增 `tenant_id` nullable 列 + 索引 `ix_{table}_tenant_id`，不回填数据

**实施步骤**：

- [ ] 6.1.1 新建 `ITenantEntity.cs` + `ITenantContext.cs` + `TenantContext.cs`（按上方代码）
- [ ] 6.1.2 新建 `TenantQueryFilterInterceptor.cs`：EF Core interceptor 在查询时自动注入租户过滤
- [ ] 6.1.3 新建 `MultiTenancyExtensions.cs`：DI 注册 `ITenantContext` 单例
- [ ] 6.1.4 修改 `BaseDbContext.cs`：注入 `ITenantContext`，`OnModelCreating` 中对 `ITenantEntity` 实体自动配置 `tenant_id` 列 + 全局查询过滤器
- [ ] 6.1.5 全 BC 聚合根实现 `ITenantEntity` 接口（Order/Cart/Product/Payment/Promotion/Review/AfterSales/Points/Membership/Notification/SellerShop/UserAuth/SystemAdmin）
- [ ] 6.1.6 各 BC 的 EF Configuration 增 `tenant_id` 列声明（IsRequired=false）+ 索引 `ix_{table}_tenant_id`
- [ ] 6.1.7 各 BC 新增迁移 `AddTenantIdColumn`：AddColumn nullable + CreateIndex
- [ ] 6.1.8 Notification BC 优先应用：`NotificationTemplate` / `NotificationPreference` / `NotificationRecord` 实现 `ITenantEntity`
- [ ] 6.1.9 SystemAdmin BC 优先应用：`AuditLog` / `FeatureFlag` / `SystemConfig` 实现 `ITenantEntity`
- [ ] 6.1.10 新建 `TenantMiddleware.cs`：从请求头 `X-Tenant-Id` 解析租户 ID 到 `ITenantContext`（当前阶段默认 null）
- [ ] 6.1.11 单元测试 `TenantQueryFilterTests.cs`：验证全局查询过滤器在 `TenantId=null` 时返回所有数据，`TenantId=具体值` 时只返回该租户数据
- [ ] 6.1.12 集成测试：Testcontainers SQL Server，验证多租户查询隔离
- [ ] 6.1.13 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 6.1.14 commit：`[phase4][MultiTenancy] 4.7: reserve tenant_id extension point + global query filter`

**验收标准**：
- [ ] `ITenantEntity` + `ITenantContext` 接口就绪
- [ ] 全 BC 聚合根实现 `ITenantEntity`（仅添加属性，不改业务方法）
- [ ] `tenant_id` nullable 列 + 索引创建，不回填数据
- [ ] 全局查询过滤器在单租户模式（TenantId=null）下返回所有数据
- [ ] 业务驱动时（DG-7 通过）可直接切换为多租户模式
- [ ] 单元测试覆盖率 ≥ 80%

### 6.2 步骤8：国际化预留（4 周，高风险，仅预留扩展位）

**目标**：在通知模板与错误码体系预留国际化扩展位（`Culture` 维度 + `IStringLocalizer` + 本地化资源文件），业务驱动时可直接落地。**仅预留，不实际落地**（受 DG-8 决策门约束）。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Abstractions/Localization/IStringLocalizerFactory.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Abstractions/Localization/LocalizationOptions.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure/Localization/ResourceManagerStringLocalizer.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure/Localization/LocalizationExtensions.cs` |
| 新建 | `src/BuildingBlocks/Leno.SharedContracts/Localization/ErrorCodeCatalog.cs` |
| 新建 | `src/Services/Notification/Leno.Notification.Domain/NotificationTemplateCulture.cs`（值对象） |
| 修改 | `src/Services/Notification/Leno.Notification.Domain/NotificationTemplate.cs`（增 `Culture` 维度） |
| 修改 | `src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationTemplateConfiguration.cs` |
| 新建资源 | `src/BuildingBlocks/Leno.SharedContracts/Localization/Resources/ErrorMessages.resx`（默认 en-US） |
| 新建资源 | `src/BuildingBlocks/Leno.SharedContracts/Localization/Resources/ErrorMessages.zh-CN.resx` |
| 新建测试 | `tests/Shared/Localization/ResourceManagerStringLocalizerTests.cs` |

**关键代码抽象 — 国际化扩展位**：

```csharp
namespace Leno.Infrastructure.Localization;

public interface IStringLocalizerFactory
{
    IStringLocalizer Create(string baseName);
}

public interface IStringLocalizer
{
    string this[string key] { get; }
    string this[string key, params object[] arguments] { get; }
}

public sealed class LocalizationOptions
{
    /// <summary>支持的文化列表，默认 ["en-US", "zh-CN"]</summary>
    public string[] SupportedCultures { get; set; } = { "en-US", "zh-CN" };
    /// <summary>默认文化</summary>
    public string DefaultCulture { get; set; } = "zh-CN";
}
```

**通知模板 Culture 维度（值对象）**：

```csharp
public sealed class NotificationTemplateCulture : ValueObject
{
    public string Culture { get; private set; } // e.g., "zh-CN", "en-US"

    private NotificationTemplateCulture(string culture) => Culture = culture;

    public static NotificationTemplateCulture Create(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            throw new ArgumentNullException(nameof(culture));
        try { _ = CultureInfo.GetCultureInfo(culture); }
        catch (CultureNotFoundException) { throw new ArgumentException($"Invalid culture: {culture}"); }
        return new NotificationTemplateCulture(culture);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Culture; }
    public static NotificationTemplateCulture Default => new("zh-CN");
}

// NotificationTemplate 聚合根增 Culture 维度（复合唯一约束 TemplateCode + Culture）
public class NotificationTemplate
{
    // ... 现有字段
    public NotificationTemplateCulture Culture { get; private set; } = NotificationTemplateCulture.Default;
}
```

**ErrorCodeCatalog（错误码 → 本地化消息映射预留）**：

```csharp
public static class ErrorCodeCatalog
{
    /// <summary>错误码 → 本地化资源 key 映射</summary>
    private static readonly Dictionary<string, string> _map = new()
    {
        ["CART_NOT_FOUND"] = "cart_not_found",
        ["ORDER_TIMEOUT"] = "order_timeout",
        ["PAYMENT_FAILED"] = "payment_failed",
        // ... 全 BC 错误码
    };

    public static string GetResourceKey(string errorCode) =>
        _map.TryGetValue(errorCode, out var key) ? key : "generic_error";
}
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `IStringLocalizer` + `NotificationTemplateCulture` 单元测试 → 写实现 → 选择性应用到 Notification BC
- **仅预留扩展位**：默认 `Culture = "zh-CN"`，不实际翻译多语言资源
- 受 DG-8 决策门约束：业务方确认海外扩展计划后，再实际翻译多语言资源
- `NotificationTemplate` 表增 `culture` 列 + 复合唯一索引 `uq_template_code_culture`
- 错误码本地化：建立 `ErrorCodeCatalog` 映射表，预留资源文件位置，资源文件仅含 zh-CN 默认值
- 现有 API 响应错误码走 `IStringLocalizer` 查询（默认 zh-CN，未来按 `Accept-Language` 头切换）

**实施步骤**：

- [ ] 6.2.1 新建 `IStringLocalizer.cs` + `IStringLocalizerFactory.cs` + `LocalizationOptions.cs`（按上方代码）
- [ ] 6.2.2 新建 `ResourceManagerStringLocalizer.cs`：基于 .resx 资源文件实现
- [ ] 6.2.3 新建 `LocalizationExtensions.cs`：DI 注册 `IStringLocalizer` 单例
- [ ] 6.2.4 新建 `ErrorCodeCatalog.cs`：错误码 → 本地化资源 key 映射
- [ ] 6.2.5 新建资源文件 `ErrorMessages.resx`（默认）+ `ErrorMessages.zh-CN.resx`，仅含 zh-CN 默认值
- [ ] 6.2.6 新建 `NotificationTemplateCulture.cs` 值对象（按上方代码）
- [ ] 6.2.7 修改 `NotificationTemplate.cs` 聚合根：增 `Culture` 属性（默认 `NotificationTemplateCulture.Default`）
- [ ] 6.2.8 修改 `NotificationTemplateConfiguration.cs`：增 `culture` 列 + 复合唯一索引 `uq_template_code_culture`
- [ ] 6.2.9 新增迁移 `AddNotificationTemplateCulture`：AddColumn + CreateIndex
- [ ] 6.2.10 修改 Notification BC 的 API 响应：错误消息走 `IStringLocalizer[ErrorCodeCatalog.GetResourceKey(errorCode)]`
- [ ] 6.2.11 新建 `CultureMiddleware.cs`：从 `Accept-Language` 头解析 Culture（当前阶段默认 zh-CN）
- [ ] 6.2.12 单元测试 `ResourceManagerStringLocalizerTests.cs`：覆盖 key 查找、缺省回退、参数格式化
- [ ] 6.2.13 单元测试 `NotificationTemplateCultureTests.cs`：覆盖合法/非法 culture、Default 值
- [ ] 6.2.14 集成测试：Notification BC 模板渲染按 Culture 查询
- [ ] 6.2.15 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 6.2.16 commit：`[phase4][i18n] 4.8: reserve Culture dimension + IStringLocalizer extension point`

**验收标准**：
- [ ] `IStringLocalizer` + `IStringLocalizerFactory` 接口就绪
- [ ] `NotificationTemplate.Culture` 维度 + 复合唯一索引就绪
- [ ] `ErrorCodeCatalog` 错误码映射表就绪
- [ ] 默认 zh-CN 资源文件就绪，en-US 占位
- [ ] 业务驱动时（DG-8 通过）可直接添加多语言资源
- [ ] 单元测试覆盖率 ≥ 80%

### 6.3 步骤9：积分会员 BC 拆分（6 周，高风险）

**目标**：将 `PointsMembership` 单 BC 拆分为 `Points` BC（账户/流水/兑换/对账）与 `Membership` BC（成长值/等级/权益包/评估），经 `PointsEarnedEvent`/`MemberLevelChangedEvent` 集成事件协作，实现独立伸缩与故障隔离。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建项目 | `src/Services/Points/Leno.Points.Domain/Leno.Points.Domain.csproj` |
| 新建项目 | `src/Services/Points/Leno.Points.Application/Leno.Points.Application.csproj` |
| 新建项目 | `src/Services/Points/Leno.Points.Infrastructure/Leno.Points.Infrastructure.csproj` |
| 新建项目 | `src/Services/Points/Leno.Points.Api/Leno.Points.Api.csproj` |
| 新建项目 | `src/Services/Membership/Leno.Membership.Domain/Leno.Membership.Domain.csproj` |
| 新建项目 | `src/Services/Membership/Leno.Membership.Application/Leno.Membership.Application.csproj` |
| 新建项目 | `src/Services/Membership/Leno.Membership.Infrastructure/Leno.Membership.Infrastructure.csproj` |
| 新建项目 | `src/Services/Membership/Leno.Membership.Api/Leno.Membership.Api.csproj` |
| 删除项目 | `src/Services/PointsMembership/`（迁移完成后） |
| 新建事件 | `src/BuildingBlocks/Leno.SharedContracts/Events/PointsEvents.cs`（`PointsEarnedEvent` 等） |
| 新建事件 | `src/BuildingBlocks/Leno.SharedContracts/Events/MembershipEvents.cs`（`MemberLevelChangedEvent` 等） |
| 修改 | `Leno.sln`（移除 PointsMembership 项目，新增 8 个项目） |
| 新建测试 | `tests/Services/Points/` + `tests/Services/Membership/` |

**Points BC 结构（账户/流水/兑换/对账）**：

```
Leno.Points.Domain/
├── Aggregates/
│   ├── PointsAccount/             # 积分账户聚合根
│   │   ├── PointsAccount.cs
│   │   ├── PointsBalance.cs       # 值对象
│   │   └── FrozenPoints.cs        # 值对象
│   ├── PointsFlow/                # 积分流水聚合根
│   │   └── PointsFlow.cs
│   └── ExchangeCoupon/            # 兑换券聚合根
│       └── ExchangeCoupon.cs
├── Events/
│   ├── PointsEarnedDomainEvent.cs
│   ├── PointsFrozenDomainEvent.cs
│   └── PointsExchangeCompletedDomainEvent.cs
└── Repositories/
    ├── IPointsAccountRepository.cs
    ├── IPointsFlowRepository.cs
    └── IExchangeCouponRepository.cs
```

**Membership BC 结构（成长值/等级/权益包/评估）**：

```
Leno.Membership.Domain/
├── Aggregates/
│   ├── Member/                    # 会员聚合根
│   │   ├── Member.cs
│   │   ├── GrowthValue.cs         # 值对象
│   │   └── MemberLevel.cs         # 值对象
│   └── MembershipPackage/         # 会员权益包聚合根
│       └── MembershipPackage.cs
├── Events/
│   ├── MemberLevelChangedDomainEvent.cs
│   └── MembershipUpgradedDomainEvent.cs
└── Repositories/
    ├── IMemberRepository.cs
    └── IMembershipPackageRepository.cs
```

**事件协作模型**：

```csharp
// Points BC 发布，Membership BC 消费（积分→成长值）
public sealed record PointsEarnedEvent(
    Guid UserId,
    int Points,
    string Source, // "OrderCompleted" / "ReviewApproved" / "Signin"
    Guid TransactionId,
    DateTime OccurredAt,
    int SchemaVersion = 1) : IntegrationEventBase;

// Membership BC 发布，Points BC 消费（等级提升→积分加成）
public sealed record MemberLevelChangedEvent(
    Guid UserId,
    int OldLevel,
    int NewLevel,
    int BonusPoints, // 等级提升奖励积分
    DateTime OccurredAt,
    int SchemaVersion = 1) : IntegrationEventBase;
```

**subagent 指令要点**：

- Read 本节结构定义，按 TDD 流程：先建 BC 项目骨架 → 迁移聚合根 → 迁移 Consumer → 双轨期切流 → 下线原 BC
- 阶段三 AuthN/AuthZ 拆分经验复盘（DG-9）：复用双轨期 8 周 + 事件双写 + 灰度按事件类型切流模式
- 双轨期 8 周：`PointsMembership` BC 与 `Points` + `Membership` 双 BC 并行，事件双写
- 数据库拆分：`points_membership_db` → `points_db` + `membership_db`，按聚合根迁移
- gRPC 端点重组：`PointsMembershipService` → `PointsService` + `MembershipService`，旧端点保留 8 周过渡
- Consumer 迁移：原 9 个 Consumer 按积分/会员分类，积分相关迁到 Points BC，会员相关迁到 Membership BC
- 数据迁移脚本：`points_accounts` / `points_flows` / `exchange_coupons` → `points_db`；`members` / `membership_packages` → `membership_db`

**实施步骤**：

- [ ] 6.3.1 新建 8 个 BC 项目（Points: Domain/Application/Infrastructure/Api；Membership: Domain/Application/Infrastructure/Api）
- [ ] 6.3.2 修改 `Leno.sln`：新增 8 个项目引用
- [ ] 6.3.3 迁移 `PointsAccount` 聚合根到 `Leno.Points.Domain/Aggregates/PointsAccount/`
- [ ] 6.3.4 迁移 `PointsFlow` 聚合根到 `Leno.Points.Domain/Aggregates/PointsFlow/`
- [ ] 6.3.5 迁移 `ExchangeCoupon` 聚合根到 `Leno.Points.Domain/Aggregates/ExchangeCoupon/`
- [ ] 6.3.6 迁移 `Member` 聚合根到 `Leno.Membership.Domain/Aggregates/Member/`
- [ ] 6.3.7 迁移 `MembershipPackage` 聚合根到 `Leno.Membership.Domain/Aggregates/MembershipPackage/`
- [ ] 6.3.8 新建 `PointsEvents.cs` + `MembershipEvents.cs`（按上方代码），定义 `PointsEarnedEvent` / `MemberLevelChangedEvent`
- [ ] 6.3.9 迁移积分相关 Consumer（`ReviewApprovedEventConsumer` / `OrderCompletedEventConsumer` 等）到 `Leno.Points.Infrastructure/Consumers/`
- [ ] 6.3.10 迁移会员相关 Consumer（`MemberLevelEvaluationJob` 等）到 `Leno.Membership.Infrastructure/`
- [ ] 6.3.11 新建 `Leno.Points.Infrastructure/Persistence/PointsDbContext.cs` + Configuration
- [ ] 6.3.12 新建 `Leno.Membership.Infrastructure/Persistence/MembershipDbContext.cs` + Configuration
- [ ] 6.3.13 数据迁移脚本：`scripts/migrate-points-membership-split.sql`，按聚合根迁移数据
- [ ] 6.3.14 新建 gRPC 服务 `PointsService.proto` + `MembershipService.proto`，从原 `PointsMembershipService.proto` 拆分
- [ ] 6.3.15 双轨期：`PointsMembership` BC 保留 8 周，事件双写到新 BC
- [ ] 6.3.16 feature flag 按 UserId 哈希切流：`PointsMembershipSplit:Enabled` 控制 0%→50%→100%
- [ ] 6.3.17 灰度验证：50% 流量切到新 BC，监控事件投递成功率、数据一致性
- [ ] 6.3.18 全量切流后下线 `PointsMembership` BC：从 `Leno.sln` 移除，删除 `src/Services/PointsMembership/`
- [ ] 6.3.19 单元测试：Points BC + Membership BC 各自聚合根测试覆盖率 ≥ 80%
- [ ] 6.3.20 集成测试：Testcontainers + MassTransit Test Harness 验证 `PointsEarnedEvent` → Membership BC 成长值更新 + `MemberLevelChangedEvent` → Points BC 奖励积分
- [ ] 6.3.21 `dotnet build` + `dotnet test` 验证零错误零警告
- [ ] 6.3.22 commit：`[phase4][Points+Membership] 4.9: split PointsMembership BC into Points BC + Membership BC`

**验收标准**：
- [ ] Points BC 与 Membership BC 独立部署，各自数据库
- [ ] `PointsEarnedEvent` / `MemberLevelChangedEvent` 事件协作正常
- [ ] 积分高频写与会员低频评估独立伸缩
- [ ] 故障隔离：Points BC 故障不影响 Membership BC
- [ ] 双轨期 8 周后 `PointsMembership` BC 完全下线
- [ ] 单元测试覆盖率 ≥ 80%，集成测试验证事件协作

---

## 7. 穿插独立任务（与 Wave 2/3 并行）

### 7.1 步骤5：多级缓存 L1+L2（4 周，依赖 Wave 2 Caching 子包）

**目标**：在 Redis L2 缓存基础上引入 `IMemoryCache` L1 本地缓存（5s TTL），配合 Redis Pub/Sub 跨实例失效，热点 Key Redis QPS 下降 80%+。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Caching/MultiLevelCache.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Caching/IMultiLevelCache.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Caching/CacheInvalidationSubscriber.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Caching/CacheInvalidationPublisher.cs` |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.Caching/DependencyInjection/CacheServiceCollectionExtensions.cs` |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.Caching/CacheService.cs`（标注 `[Obsolete]`，保留过渡） |
| 新建测试 | `tests/BuildingBlocks/Leno.Infrastructure.Caching.Tests/MultiLevelCacheTests.cs` |

**关键代码抽象 — 多级缓存 `GetAsync` 模式**：

```csharp
namespace Leno.Infrastructure.Caching;

public interface IMultiLevelCache
{
    Task<T?> GetAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? l2Ttl = null, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? l2Ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public sealed class MultiLevelCache : IMultiLevelCache
{
    private readonly IMemoryCache _l1; // 进程内 L1，短 TTL（默认 5s）
    private readonly ICacheService _l2; // Redis L2，长 TTL
    private readonly ICacheInvalidationPublisher _invalidationPublisher;
    private readonly CacheOptions _options;
    private readonly ILogger<MultiLevelCache> _logger;

    public MultiLevelCache(
        IMemoryCache l1,
        ICacheService l2,
        ICacheInvalidationPublisher invalidationPublisher,
        IOptions<CacheOptions> options,
        ILogger<MultiLevelCache> logger)
    {
        _l1 = l1; _l2 = l2; _invalidationPublisher = invalidationPublisher;
        _options = options.Value; _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? l2Ttl = null, CancellationToken ct = default)
    {
        // L1 命中：直接返回
        if (_l1.TryGetValue(key, out T? l1Value))
        {
            _logger.LogDebug("L1 cache hit: {Key}", key);
            return l1Value;
        }

        // L2 命中：回填 L1 并返回
        var l2Value = await _l2.GetAsync<T>(key, ct);
        if (l2Value is not null)
        {
            _logger.LogDebug("L2 cache hit: {Key}", key);
            _l1.Set(key, l2Value, _options.L1Ttl);
            return l2Value;
        }

        // 双miss：回源 + 互斥锁（复用 L2 SingleFlight 模式）+ 回填 L1+L2
        var value = await factory(ct);
        if (value is not null)
        {
            await _l2.SetAsync(key, value, l2Ttl ?? _options.L2Ttl, ct);
            _l1.Set(key, value, _options.L1Ttl);
        }
        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _l1.Remove(key);
        await _l2.RemoveAsync(key, ct);
        // Pub/Sub 通知其他实例失效 L1
        await _invalidationPublisher.PublishInvalidationAsync(key, ct);
    }
}
```

**Pub/Sub 跨实例失效**：

```csharp
public interface ICacheInvalidationPublisher
{
    Task PublishInvalidationAsync(string key, CancellationToken ct = default);
}

public interface ICacheInvalidationSubscriber
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}

// CacheInvalidationSubscriber 订阅 Redis channel "cache:invalidation"
// 收到消息时：_l1.Remove(key) 仅清进程内 L1，不影响 L2
// L1 TTL 5s 兜底：即使 Pub/Sub 消息丢失，5s 后 L1 自动过期回源 L2
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `MultiLevelCache` + `CacheInvalidationSubscriber` 单元测试 → 写实现 → 集成到 Product BC（热点 SPU 数据）
- L1 默认 TTL 5s（`CacheOptions.L1Ttl`），可通过配置调整
- L2 复用现有 `CacheService`（Redis + 布隆过滤器 + 互斥锁 + 抖动）
- Pub/Sub 通道 `cache:invalidation`，参考 `JwtBlacklistService` 模式
- feature flag 按 Key 前缀切流（如 `product:*` 启用 L1，`user:*` 仅 L2）
- 双轨期 4 周：`CacheService` 标注 `[Obsolete]`，新代码使用 `IMultiLevelCache`

**实施步骤**：

- [ ] 7.1.1 新建 `IMultiLevelCache.cs` + `MultiLevelCache.cs`（按上方代码）
- [ ] 7.1.2 新建 `ICacheInvalidationPublisher.cs` + `CacheInvalidationPublisher.cs`：Redis Pub/Sub 发布失效消息
- [ ] 7.1.3 新建 `ICacheInvalidationSubscriber.cs` + `CacheInvalidationSubscriber.cs`：`IHostedService` 订阅失效消息，收到时清 L1
- [ ] 7.1.4 修改 `CacheServiceCollectionExtensions.cs`：注册 `IMultiLevelCache` + `IMemoryCache` + `CacheInvalidationSubscriber`
- [ ] 7.1.5 修改 `CacheService.cs`：标注 `[Obsolete("Use IMultiLevelCache")]`，保留 4 周过渡
- [ ] 7.1.6 新建 `CacheOptions.cs`：`L1Ttl`（默认 5s）/ `L2Ttl`（默认 30min）/ `L1EnabledPrefixes`（启用 L1 的 Key 前缀列表）
- [ ] 7.1.7 修改 Product BC：`EfCoreSPURepository` 改用 `IMultiLevelCache.GetAsync("product:spu:{id}", ...)`
- [ ] 7.1.8 修改 Product BC：`ProductGrpcService.GetSpuAsync` 改用 `IMultiLevelCache`
- [ ] 7.1.9 feature flag 配置：`Cache:L1EnabledPrefixes: ["product:", "promotion:seckill:"]`
- [ ] 7.1.10 单元测试 `MultiLevelCacheTests.cs`：覆盖 L1 命中、L2 命中回填 L1、双miss 回源、Pub/Sub 失效
- [ ] 7.1.11 集成测试：Testcontainers Redis，多实例并发访问，验证 Pub/Sub 跨实例失效
- [ ] 7.1.12 性能基准：热点 Key QPS 对比基线（仅 L2），验证下降 80%+
- [ ] 7.1.13 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 7.1.14 commit：`[phase4][Caching] 4.5: multi-level cache L1 IMemoryCache + L2 Redis + Pub/Sub invalidation`

**验收标准**：
- [ ] L1 `IMemoryCache`（5s TTL）+ L2 Redis + Pub/Sub 跨实例失效就绪
- [ ] 热点 Key Redis QPS 下降 ≥ 80%
- [ ] L1 跨实例失效验证通过
- [ ] L1 TTL 5s 兜底（Pub/Sub 消息丢失时自动回源 L2）
- [ ] 单元测试覆盖率 ≥ 80%

### 7.2 步骤6：Consul 配置 Schema 版本化（3 周，独立）

**目标**：为 Consul KV 配置引入 Schema 版本化与灰度发布机制，配置变更可追溯、可回滚、可灰度。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulSchemaVersion.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulConfigSchemaValidator.cs` |
| 修改 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulConfigWatcher.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulConfigPublisher.cs` |
| 新建 | `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulGrayReleaseService.cs` |
| 新建测试 | `tests/BuildingBlocks/Leno.Infrastructure.Persistence.Tests/Configuration/ConsulConfigSchemaTests.cs` |

**关键代码抽象 — Schema 版本化**：

```csharp
public sealed class ConsulSchemaVersion
{
    public int Version { get; init; }
    public string SchemaHash { get; init; } = string.Empty; // JSON Schema hash
    public DateTime AppliedAt { get; init; }
    public string AppliedBy { get; init; } = string.Empty;
}

// 配置 JSON 增加 schemaVersion 字段
// {
//   "schemaVersion": 2,
//   "outbox": { "shardCount": 8, "shardId": 3 },
//   "cache": { "l1Ttl": "00:00:05", "l2Ttl": "00:30:00" }
// }
```

**灰度发布机制**：

```csharp
public sealed class ConsulGrayReleaseService
{
    /// <summary>按实例 ID 哈希切流：0%→25%→50%→100%</summary>
    public bool ShouldApplyConfig(string instanceId, int grayPercent)
    {
        if (grayPercent >= 100) return true;
        if (grayPercent <= 0) return false;
        var hash = instanceId.GetHashCode() & 0x7FFFFFFF;
        return (hash % 100) < grayPercent;
    }
}
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先写 `ConsulSchemaVersion` + `ConsulGrayReleaseService` 单元测试 → 写实现 → 集成到 `ConsulConfigWatcher`
- Consul KV 路径：`leno/config/{env}/{service}` + `leno/config/{env}/{service}/schema-version`
- 配置变更时校验 `schemaVersion`：版本不匹配触发告警，拒绝应用
- 灰度发布：通过 Consul KV `leno/config/{env}/{service}/gray-percent` 控制切流比例
- 配置回滚：记录历史版本到 `leno/config/{env}/{service}/history/{version}`，可一键回滚

**实施步骤**：

- [ ] 7.2.1 新建 `ConsulSchemaVersion.cs` + `ConsulConfigSchemaValidator.cs`（按上方代码）
- [ ] 7.2.2 新建 `ConsulConfigPublisher.cs`：发布配置时自动写入 `schemaVersion` + 历史版本
- [ ] 7.2.3 新建 `ConsulGrayReleaseService.cs`（按上方代码）：按实例 ID 哈希切流
- [ ] 7.2.4 修改 `ConsulConfigWatcher.cs`：拉取配置时校验 `schemaVersion`，按灰度比例决定是否应用
- [ ] 7.2.5 新增配置 `appsettings.json`：`Consul:GrayPercent` / `Consul:SchemaVersion`
- [ ] 7.2.6 单元测试 `ConsulConfigSchemaTests.cs`：覆盖版本校验、灰度切流、回滚
- [ ] 7.2.7 集成测试：Testcontainers Consul，验证配置发布 + 灰度 + 回滚流程
- [ ] 7.2.8 `dotnet build` + `dotnet test` 验证零错误零警告，覆盖率 ≥ 80%
- [ ] 7.2.9 commit：`[phase4][Configuration] 4.6: Consul schema versioning + gray release`

**验收标准**：
- [ ] 配置 JSON 含 `schemaVersion` 字段，变更可追溯
- [ ] 灰度发布机制按实例 ID 哈希切流
- [ ] 配置回滚可一键操作
- [ ] 单元测试覆盖率 ≥ 80%

### 7.3 步骤10：跨 BC 契约评审 + Pact 契约测试（持续，贯穿全阶段）

**目标**：搭建 Pact Broker，建立跨 BC 契约测试体系，CI 集成 breaking 变更自动检测，跨 BC 调用契约锁定。

**修改范围**：

| 操作 | 文件路径 |
|------|---------|
| 新建 | `tests/Contracts/Leno.Contracts.Consumer.Tests/Leno.Contracts.Consumer.Tests.csproj` |
| 新建 | `tests/Contracts/Leno.Contracts.Consumer.Tests/OrderBcConsumerTests.cs`（样例） |
| 新建 | `tests/Contracts/Leno.Contracts.Provider.Tests/Leno.Contracts.Provider.Tests.csproj` |
| 新建 | `tests/Contracts/Leno.Contracts.Provider.Tests/OrderBcProviderTests.cs`（样例） |
| 新建 | `tests/Contracts/Leno.Contracts.Provider.Tests/ProviderStateMiddleware.cs` |
| 新建 | `pact-broker/docker-compose.yml`（Pact Broker 部署） |
| 修改 | `.github/workflows/ci.yml`（增 Pact 契约测试 job） |
| 新建 | `docs/contracts/README.md`（契约测试指南） |

**Pact 契约测试结构**：

```csharp
// Consumer 端测试（如 Order BC 调用 Product BC 的 gRPC）
[Fact]
public async Task GetSpu_WithValidId_ReturnsSpuSnapshot()
{
    // 1. 配置 mock provider
    var pactBuilder = new PactBuilder(new PactConfig { PactDir = "../../../pacts/" });
    pactBuilder.ServiceConsumer("Order BC").HasPactWith("Product BC");

    await pactBuilder.UponReceiving("A request to get SPU by id")
        .Given("A SPU with id 'spu-001' exists")
        .WithRequest(HttpMethod.Get, "/api/spu/spu-001")
        .WillRespond()
        .WithStatus(200)
        .WithHeader("Content-Type", "application/json")
        .WithJsonBody(new { id = "spu-001", name = "Test Product", price = 99.99m });

    // 2. 执行实际调用
    var result = await _productClient.GetSpuAsync("spu-001");

    // 3. 验证响应
    result.Name.Should().Be("Test Product");
    await pactBuilder.VerifyAsync();
}

// Provider 端测试（Product BC 验证契约）
[Fact]
public async Task EnsureSpuContractIsSatisfied()
{
    var pactVerifier = new PactVerifier(new PactVerifierConfig());
    await pactVerifier.ServiceProvider("Product BC", _factory.Server)
        .HonoursPactWith("Order BC")
        .PactUri("http://pact-broker:9292/pacts/provider/Product%20BC/consumer/Order%20BC/latest")
        .VerifyAsync();
}
```

**Provider State Middleware（设置测试前置状态）**：

```csharp
public sealed class ProviderStateMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/provider-states"))
        {
            var state = await context.Request.ReadFromJsonAsync<ProviderState>();
            await SetupProviderStateAsync(state!.State);
            context.Response.StatusCode = 200;
            return;
        }
        await next(context);
    }

    private async Task SetupProviderStateAsync(string state)
    {
        // 根据 state 设置测试数据，如 "A SPU with id 'spu-001' exists"
        if (state == "A SPU with id 'spu-001' exists")
        {
            await _dbContext.Spus.AddAsync(new Spu { Id = Guid.Parse("spu-001"), Name = "Test Product", Price = 99.99m });
            await _dbContext.SaveChangesAsync();
        }
    }
}
```

**Pact Broker 部署（`pact-broker/docker-compose.yml`）**：

```yaml
version: '3.8'
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: pact
      POSTGRES_USER: pact
      POSTGRES_PASSWORD: pact_password
    volumes:
      - postgres-data:/var/lib/postgresql/data

  pact-broker:
    image: pactfoundation/pact-broker:2.108.0
    ports:
      - "9292:9292"
    environment:
      PACT_BROKER_DATABASE_URL: postgres://pact:pact_password@postgres:5432/pact
      PACT_BROKER_BASIC_AUTH_USERNAME: pact
      PACT_BROKER_BASIC_AUTH_PASSWORD: pact_password
    depends_on:
      - postgres

volumes:
  postgres-data:
```

**CI 集成（`.github/workflows/ci.yml` 增 Pact job）**：

```yaml
jobs:
  pact-contract-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Run consumer tests
        run: dotnet test tests/Contracts/Leno.Contracts.Consumer.Tests --logger trx
      - name: Publish pacts to broker
        env:
          PACT_BROKER_URL: ${{ secrets.PACT_BROKER_URL }}
          PACT_BROKER_TOKEN: ${{ secrets.PACT_BROKER_TOKEN }}
        run: |
          docker run --rm \
            -v $(pwd)/pacts:/pacts \
            pactfoundation/pact-cli:latest \
            publish /pacts \
            --broker-base-url $PACT_BROKER_URL \
            --broker-token $PACT_BROKER_TOKEN
      - name: Verify provider contracts
        run: dotnet test tests/Contracts/Leno.Contracts.Provider.Tests --logger trx
      - name: Check can-i-deploy
        env:
          PACT_BROKER_URL: ${{ secrets.PACT_BROKER_URL }}
          PACT_BROKER_TOKEN: ${{ secrets.PACT_BROKER_TOKEN }}
        run: |
          docker run --rm pactfoundation/pact-cli:latest \
            broker can-i-deploy \
            --pacticipant "Order BC" \
            --version ${{ github.sha }} \
            --broker-base-url $PACT_BROKER_URL \
            --broker-token $PACT_BROKER_TOKEN
```

**subagent 指令要点**：

- Read 本节代码抽象，按 TDD 流程：先搭 Pact Broker → 写样例 Consumer/Provider 测试 → CI 集成 → 逐 BC 推广
- **受 DG-10 决策门约束**：团队 Pact 培训完成后才强制 CI 集成，培训前仅完成样例
- 优先覆盖跨 BC 高频调用契约：Order→Product、Order→Inventory、Order→Points、Payment→Order、Notification→全 BC
- 契约覆盖率目标 ≥ 90%：所有跨 BC gRPC + HTTP 调用均有 Pact 契约
- breaking 变更检测：`can-i-deploy` 在 PR 阶段拦截破坏性变更
- 契约版本化：Pact Broker 自动管理版本，支持 `latest` 与 `tag` 查询

**实施步骤**：

- [ ] 7.3.1 新建 `pact-broker/docker-compose.yml`（按上方代码），部署 Pact Broker
- [ ] 7.3.2 新建 `tests/Contracts/Leno.Contracts.Consumer.Tests/` 项目，引用 `PactNet`
- [ ] 7.3.3 新建 `tests/Contracts/Leno.Contracts.Provider.Tests/` 项目，引用 `PactNet.Provider` + `WebApplicationFactory`
- [ ] 7.3.4 编写样例 Consumer 测试 `OrderBcConsumerTests.cs`：Order BC 调用 Product BC 的 SPU 查询契约（按上方代码）
- [ ] 7.3.5 编写样例 Provider 测试 `OrderBcProviderTests.cs`：Product BC 验证契约（按上方代码）
- [ ] 7.3.6 新建 `ProviderStateMiddleware.cs`（按上方代码）：设置测试前置状态
- [ ] 7.3.7 修改 `.github/workflows/ci.yml` 增 `pact-contract-tests` job（按上方代码）
- [ ] 7.3.8 优先覆盖跨 BC 高频契约：Order→Product（SPU 查询）、Order→Inventory（库存预占）、Order→Points（积分冻结）、Payment→Order（支付回调）
- [ ] 7.3.9 全 BC 推广：每个跨 BC 调用编写 Consumer + Provider 测试
- [ ] 7.3.10 契约覆盖率统计脚本：扫描 gRPC `.proto` + HTTP client 调用，对比 Pact 契约数量
- [ ] 7.3.11 `can-i-deploy` 门禁：PR 阶段拦截破坏性变更
- [ ] 7.3.12 团队 Pact 培训（DG-10 决策门）：完成后启用 CI 强制集成
- [ ] 7.3.13 `dotnet build` + `dotnet test` 验证零错误零警告
- [ ] 7.3.14 commit：`[phase4][Contracts] 4.10: Pact contract tests + Pact Broker + CI integration`

**验收标准**：
- [ ] Pact Broker 部署完成，可访问
- [ ] 跨 BC 契约覆盖率 ≥ 90%
- [ ] CI `pact-contract-tests` job 运行通过
- [ ] `can-i-deploy` 拦截破坏性变更
- [ ] 团队 Pact 培训完成（DG-10 通过）

---

## 8. 双轨期策略

阶段四高风险项的双轨期策略与切换机制：

| 场景 | 双轨期 | 切换机制 | 影响任务 |
|------|--------|---------|---------|
| Infrastructure 拆包 | 12 周 | 元包门面始终可用 + 子包按需迁移 | 4.1 |
| gRPC int64 → string（遗留） | 12 周 | deprecated 标注 + 客户端逐步升级 | 4.1/4.9 |
| 积分会员 BC 拆分 | 8 周 | 事件类型双写 + 灰度按 BC 切流 | 4.9 |
| 多级缓存 L1+L2 | 4 周 | feature flag 按 Key 前缀切流，L1 失效回源 L2 | 4.5 |
| ACL 策略链 vs 双轨调度 | 4 周 | feature flag 按 BC 切流 | 4.2 |
| Outbox 单实例 → 分片发布 | 4 周 | feature flag 按 BC 切流，单实例发布器保留过渡 | 4.4 |
| BFF `Parallel.ForEachAsync` → DAG | 4 周 | 旧路径标注 `[Obsolete]`，新端点使用 DAG，旧端点逐步迁移 | 4.3 |

**双轨期管理原则**：
- 双轨期 ≤ 12 周，超过需架构评审
- 双轨期内必须配套监控指标（双轨一致性、事件投递成功率）
- 全量切流后 2 周内下线旧实现，避免长期维护双份代码

---

## 9. 验证策略

### 9.1 本地构建验证流程

每任务独立 commit 后执行：
- [ ] `dotnet build Leno.sln` 零错误零警告（W0 目标）
- [ ] `dotnet test` 全绿，新增/修改代码覆盖率 ≥ 80%
- [ ] 集成测试通过（Testcontainers + MassTransit Test Harness）
- [ ] commit message 格式：`[phase4][{Module/BC}] {task-id}: {description}`

### 9.2 任务级验收标准汇总

| 任务 | 验证手段 |
|------|---------|
| 4.1 Infrastructure 拆包 | 依赖图分析工具验证无循环依赖；元包门面全 BC 编译通过；Product BC 启动加速 ≥ 30% |
| 4.2 ACL 策略链 | 双轨切流集成测试；新协议接入零侵入验证（mock 第三个 channel） |
| 4.3 BFF DAG | 复杂聚合端点性能基准；拓扑排序有环异常测试；级联超时测试 |
| 4.4 Outbox 分片 | 多实例发布吞吐压测（线性扩展验证）；`SKIP LOCKED` 正确性测试 |
| 4.5 多级缓存 | 性能基准测试（热点 Key QPS 对比基线下降 ≥ 80%）；L1 跨实例失效集成测试 |
| 4.6 Consul 版本化 | 配置灰度发布集成测试；回滚测试 |
| 4.7 多租户预留 | 扩展位单元测试（验证 `tenant_id` 列存在、全局查询过滤器生效） |
| 4.8 国际化预留 | 扩展位单元测试（验证 `Culture` 维度支持、`IStringLocalizer` 接口） |
| 4.9 积分会员拆分 | 跨 BC 集成测试（Testcontainers + MassTransit Test Harness）；数据迁移脚本验证 |
| 4.10 Pact 契约测试 | 跨 BC 契约覆盖率 ≥ 90%；CI breaking 变更检测；`can-i-deploy` 门禁 |

### 9.3 回归测试范围

每波次完成后执行全量回归：
- [ ] 全 BC 单元测试 + 集成测试
- [ ] 跨 BC 集成场景：下单全链路（Cart→Order→Payment→Inventory→Points→Notification）
- [ ] 性能基准对比：每波次后跑性能基准，退化超 5% 触发优化任务
- [ ] 安全回归：Argon2id 密码哈希、RS256 JWT 签名、KMS 密钥管理验证

### 9.4 性能基准

每波次配套性能基准测试，对比基线确保不退化：
- [ ] Infrastructure 拆包：BC 启动时间对比基线（目标 -30%）
- [ ] 多级缓存：热点 Key QPS 对比基线（目标 -80%）
- [ ] Outbox 分片：多实例发布吞吐对比基线（目标线性扩展）
- [ ] BFF DAG：复杂聚合端点延迟对比基线（目标 -20%）

---

## 10. 风险与回滚

### 10.1 风险矩阵

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| Infrastructure 拆包引发依赖循环 | 中 | 中 | 分批迁移 + 依赖图分析工具 + 元包门面兜底 |
| 多租户/国际化预留过度设计 | 中 | 中 | 业务驱动原则（DG-7/DG-8），仅预留扩展位不实际落地 |
| 多级缓存 L1 跨实例失效失败 | 中 | 中 | Pub/Sub 通道监控 + L1 TTL 短（5s）兜底 |
| 积分会员 BC 拆分数据迁移失败 | 中 | 高 | 双轨期并行 8 周 + 灰度按事件类型切流 + 回滚预案 |
| Outbox 分片 `SKIP LOCKED` 兼容性 | 低 | 中 | SQL Server 2016+ 验证；回退单实例发布器 |
| Pact 契约测试增加 CI 耗时 | 中 | 低 | 按需运行（PR 触发）+ 契约缓存 |
| BFF DAG 级联超时误杀正常请求 | 低 | 中 | 超时阈值可配置 + 监控级联取消率 |
| ACL 策略链降级路径失效 | 低 | 高 | 全通道耗尽异常监控 + 兜底熔断 |

### 10.2 回滚预案

每个高风险项配套回滚预案：

1. **Infrastructure 拆包回滚**：元包门面始终可用，子包迁移失败时 `git revert` 单次 commit 回退到单项目结构
2. **多级缓存回滚**：feature flag 关闭 L1，回退到仅 L2 Redis 模式；L1 TTL 5s 自动兜底
3. **Outbox 分片回滚**：feature flag 关闭分片，回退到单实例 `EfCoreOutboxPublisher`；迁移 Down 方法 DropColumn `shard_key`
4. **积分会员 BC 拆分回滚**：保留 `PointsMembership` BC 代码 8 周双轨期，feature flag 切回旧 BC；数据迁移脚本配套回滚脚本
5. **ACL 策略链回滚**：feature flag 关闭策略链，回退到原 `AntiCorruptionDispatcher` 硬编码双轨
6. **BFF DAG 回滚**：旧 `Parallel.ForEachAsync` 路径保留 4 周，feature flag 切回旧路径
7. **Consul 版本化回滚**：`schemaVersion` 校验可关闭；历史版本一键回滚
8. **Pact 契约测试回滚**：CI `pact-contract-tests` job 可禁用；契约缓存可清除

---

## 11. 持续优化（L5 达成）

阶段四完成后进入 L5 持续优化模式，目标维持健康度 ≥ 9.6。

### 11.1 健康度监控

- **季度复评**：每季度对 14 BC 执行健康度矩阵复评（功能正确性/DDD 合规/性能可靠性/安全性），目标维持 ≥ 9.6
- **退化告警**：任一 BC 健康度下降超 0.3 触发架构评审
- **P0/P1 清零**：持续保持 P0=0 / P1=0，新发现 P0 24 小时内修复

### 11.2 技术债看板

- **SystemAdmin 审计日志**：跟踪架构决策（拆包/拆分/升级），新增技术债入看板
- **技术债分类**：按"代码规范/性能/安全/可扩展性"分类，按业务影响排序
- **季度清债**：每季度拨出 20% 工程产能清理技术债 Top 10

### 11.3 契约稳定性

- **Pact 契约测试持续运行**：CI 每次提交触发契约测试，breaking 变更需架构评审
- **契约版本化**：跨 BC 调用契约 SchemaVersion 版本化，消费方按版本路由
- **契约覆盖率**：跨 BC 调用契约覆盖率 ≥ 90% 持续维持

### 11.4 性能基线

- **季度性能基准**：每季度执行性能基准对比，退化超 5% 触发优化任务
- **关键指标监控**：
  - 热点 Key Redis QPS（多级缓存生效后维持 -80%）
  - Outbox 发布吞吐（维持线性扩展）
  - BC 启动时间（Infrastructure 拆包后维持 -30%）
  - Saga 崩溃恢复时间（维持 100% 恢复率）
- **Grafana / Prometheus 仪表盘**：阶段三搭建的监控持续运营，每季度评审指标有效性

### 11.5 L5 持续优化达成标志

- [ ] 健康度 ≥ 9.6 持续 2 个季度
- [ ] P0=0 / P1=0 持续 2 个季度
- [ ] Pact 契约覆盖率 ≥ 90% 持续
- [ ] 性能基准无退化超 5%
- [ ] 技术债看板清债率 ≥ 80%

---

**阶段四实施计划完成**

本计划为阶段四 10 项任务定义了 3 波 + 持续编排的可执行级实施细节，严格前置依赖（DG-6 至 DG-10 决策门），配套双轨期策略、验证策略、风险回滚预案、L5 持续优化机制。预期健康度从 9.3 演进到 9.6，达成 L5 持续优化目标。
