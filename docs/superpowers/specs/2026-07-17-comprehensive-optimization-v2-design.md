# Leno 电商平台全面优化方案 V2（双轨并行）

> **实施进度**: 本 spec 的实施计划已分解为 10 个 Plan（F1-F4 + M1-M6）。
>
> - Plan 1-4（F1-F4 快轨）: 已完成
> - Plan 5-9（M1-M5 慢轨）: 已完成
> - Plan 10（M6 CQRS + BFF + 文档）: 进行中
>
> 详见 `docs/superpowers/plans/` 目录下各 Plan 文档。

**文档版本**：V2.0
**创建日期**：2026-07-17
**适用范围**：Leno 电商平台全部 11 个限界上下文（BC1-BC11）及共享内核、API 网关、部署运维
**方案定位**：从零全面重审仓库后形成的自洽优化方案，supersede 既有 3 份 spec（2026-07-13 全面优化、2026-07-14 网关增强、2026-07-17 漏洞修复），既有 spec 中已完成项标记 `[x]`，未完成项由本方案接管并标注 `[→ 本方案 Mx/Fx]`
**触发原因**：对仓库全面分析后识别出 9 个 P0 阻塞生产问题、约 18 个 P1 重大问题、约 10 个 P2 改进问题，跨架构、业务逻辑、安全、代码质量、通信、可观测性、部署、测试 8 大维度

---

## 1 背景与目标

### 1.1 项目现状

Leno 是基于 .NET 10 的 DDD 微服务电商平台，按 11 个限界上下文拆分：

- 6 个核心域：用户认证（BC1）、商品（BC2）、购物车（BC3）、订单交易（BC4）、促销（BC5）、评价售后（BC6）
- 3 个支撑域：积分会员（BC7）、支付集成（BC8）、卖家店铺（BC10）
- 2 个通用子域：消息通知（BC9）、系统管理（BC11）

文档体系完善（13 篇 spec + 编码规范），代码层面已有 1648+ 个单元测试。既有 3 份优化 spec 已落地部分工作（YARP 限流/熔断/超时/重试、OpenTelemetry 端到端追踪、Outbox 两阶段提交、消费幂等、CacheService 三防+双删、P0/P1 业务漏洞修复 5 个 Wave 等）。

### 1.2 既有优化工作与本方案关系

| 既有 spec | 状态 | 本方案关系 |
|---|---|---|
| [2026-07-13 全面优化](./2026-07-13-comprehensive-optimization-design.md) | 部分落地 | supersede；已完成项标记 `[x]`，未完成项由本方案慢轨 M1-M6 接管 |
| [2026-07-14 网关增强](./2026-07-14-api-gateway-enhancement-design.md) | 基本落地 | supersede；网关限流/熔断/CORS/Consul 已落地，本方案 M4/M5 增量 gRPC、BFF、告警 |
| [.trae/specs/fix-critical-business-vulnerabilities](../../../.trae/specs/fix-critical-business-vulnerabilities/) | 已落地 | supersede；P0/P1 漏洞已修复，本方案快轨 F1/F2 增量秒杀流程、ForceCancel、越权、密钥管理 |

### 1.3 优化目标

1. **消除生产阻塞风险**（快轨）：9 个 P0 问题数天内消除，系统具备生产部署条件
2. **恢复 DDD 架构合规**（慢轨 M1-M2）：BC 边界、事件契约分离、共享内核清理
3. **消除跨 BC 样板**（慢轨 M3）：约 1870 行重复代码消除
4. **升级通信机制**（慢轨 M4）：HttpClient + Polly 短期、11 个 BC gRPC 迁移长期
5. **补齐可观测性与部署**（慢轨 M5）：Prometheus、Consul KV、Alertmanager、Helm chart
6. **落地 CQRS 与 BFF**（慢轨 M6）：读多写少 BC 分离、4 个 BFF 聚合端点
7. **文档与规范同步**（慢轨 M6）：编码规范、spec、契约清单、PR 模板

---

## 2 问题分析（全面重审发现）

### 2.1 P0 阻塞生产问题（9 项）

| # | 维度 | 问题 | 证据 |
|---|---|---|---|
| P0-1 | 业务逻辑 | 秒杀下单流程断裂，`SeckillAppService.PlaceOrderAsync` 未发布 `SeckillOrderCreatedEvent`，秒杀功能完全不可用 | [SeckillAppService.cs:92-163](../../../src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs) |
| P0-2 | 业务逻辑 | `OrderAppService.ForceCancelAsync` 绕过 Outbox 直接 `_eventBus.PublishAsync`，可能重复退款或退款丢失 | [OrderAppService.cs:358](../../../src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs) |
| P0-3 | 业务逻辑 | Cart 域 3 个商品事件消费者全为空实现，商品下架后购物车仍可下单 | [ProductEventConsumer.cs:34-47](../../../src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs) |
| P0-4 | 安全 | API 网关未启用 JWT 本地验签，客户端可伪造 `X-User-Id`/`X-Role` 头 | [ApiGateway Program.cs](../../../src/ApiGateway/Leno.ApiGateway/Program.cs) |
| P0-5 | 安全 | 11 个服务 `appsettings.json` 明文硬编码 JWT SecretKey/SA 密码/RabbitMQ 密码/InternalApiKey，且全部共用同一密钥 | 各服务 appsettings.json |
| P0-6 | 部署 | EF Core Migrations 完全缺失，`Database.MigrateAsync` 无调用，生产无法部署 | 全代码库 |
| P0-7 | 部署 | docker-compose 明文密钥提交 git | [docker-compose.yml:7,56,131](../../../docker-compose.yml) |
| P0-8 | 测试 | PointsMembership.Application.Tests 0 个测试（仅 GlobalUsings.cs） | PointsMembership.Application.Tests 目录 |
| P0-9 | 测试 | ReviewAfterSales 与 SellerShop 几乎无业务测试 | 两 BC 测试目录 |

### 2.2 P1 重大问题（约 18 项，摘选）

**架构合规**：
- Notification 跨 BC 引用 Promotion/PointsMembership.Domain（[Notification.Infrastructure.csproj:8-9](../../../src/Services/Notification/Leno.Notification.Infrastructure/Leno.Notification.Infrastructure.csproj)）
- 65 个"领域事件+集成事件"双重身份事件混用（Outbox 通过 `domainEvent is IIntegrationEvent` 直接发布领域事件到 MQ）
- SharedKernel 泄漏 HTTP 状态码（[DomainException.cs:13](../../../src/BuildingBlocks/Leno.SharedKernel/Exceptions/DomainException.cs)）与 EF Core 存储格式（[MoneyJsonConverter.cs:67-90](../../../src/BuildingBlocks/Leno.SharedKernel/ValueObjects/MoneyJsonConverter.cs)）
- SPU 聚合 631 行职责过载（[SPU.cs](../../../src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs)）

**业务正确性**：
- 售后审核/确认收货/订单发货端点未校验卖家归属（横向越权）
- 防腐层静默兜底掩盖网络故障（3 处 `return null`）

**代码质量**：
- 11 份 UnitOfWork 重复（680 行）、11 份 Program.cs 重复（880 行）、11 份 OutboxMessageConfiguration 重复（308 行）

**通信**：
- HttpClient 防腐层无 Polly 重试/熔断；Internal 端点无版本治理

**可观测性**：
- 11 个业务服务未暴露 Prometheus `/metrics`、未启用 Consul KV 配置中心、健康检查缺 RabbitMQ 探活、无告警通知渠道

**部署**：
- K8s/Helm chart 缺失；CI 无覆盖率门槛；跨 BC 集成测试缺失

### 2.3 P2 改进问题（约 10 项）

BFF 聚合未实现、gRPC 迁移未启动、CQRS 未落地、ES 读模型仅 3 BC 落地、缓存 Key 未含 Role（越权风险）、集成事件无 SchemaVersion、Internal 端点无版本号等。

### 2.4 已良好实现（无需重写）

YARP 限流/熔断/超时/重试/Consul 动态发现、OpenTelemetry 端到端追踪、Outbox 两阶段提交、消费幂等（Redis SET NX + 24h TTL）、CacheService 三防+双删+批量失效、Saga 编排+对账任务、InternalApiKey timing-safe 比较+生产 fail-closed、支付回调金额强校验、退款防超退校验、积分抵现上限、Redis Lua 原子操作（秒杀/库存预占）。

---

## 3 设计原则与双轨框架

### 3.1 设计原则

1. **不破坏既有功能** — 所有重构保持现有 1648+ 个测试通过
2. **增量演进** — 双轨并行，每个 Wave/里程碑可独立验证、独立回滚
3. **遵循既有 spec 与编码规范** — 复用 `docs/spec/` 与 `docs/编码规范.md` 已确立的 DDD/CQRS/事件驱动约束
4. **测试先行** — 任何重构前先补关键路径测试，重构后再验证
5. **安全默认 fail-closed** — 鉴权、金额校验、幂等等安全相关逻辑默认拒绝

### 3.2 双轨结构

```
┌─────────────────────────────────────────────────────────────────┐
│  快轨 (Fast Track) — 业务正确性 + 安全 + 部署阻塞点              │
│  目标: 数天内消除生产阻塞风险，可独立上线                        │
│  原则: 最小改动、不引入架构变更、每项可独立验证回滚              │
├─────────────────────────────────────────────────────────────────┤
│  Wave-F1: 业务流程断裂修复    (P0-1/2/3 + P1 越权)               │
│  Wave-F2: 安全默认修复        (P0-4/5/7)                         │
│  Wave-F3: 部署阻塞点          (P0-6 EF Migrations)               │
│  Wave-F4: 关键测试补齐        (P0-8/9 + CI 守护)                 │
└─────────────────────────────────────────────────────────────────┘
                            ↓ (快轨完成后回归收敛)
┌─────────────────────────────────────────────────────────────────┐
│  慢轨 (Slow Track) — 架构演进 + 通用能力重构                     │
│  目标: 数周内完成 DDD 合规、样板消除、通信升级                   │
│  原则: 增量演进、契约稳定后启动、每里程碑可独立验证              │
├─────────────────────────────────────────────────────────────────┤
│  M1: BC 边界 + 事件契约分离    (P1 架构)                         │
│  M2: 共享内核清理              (P1 架构)                         │
│  M3: 跨 BC 样板去重            (P1 代码质量)                     │
│  M4: 通信升级 (Polly + gRPC)   (P1/P2 通信)                      │
│  M5: 可观测性 + 部署补齐       (P1 运维)                         │
│  M6: CQRS + BFF + 文档同步     (P2 改进)                         │
└─────────────────────────────────────────────────────────────────┘
```

### 3.3 双轨协调机制

- **依赖规则**：慢轨 M1（事件契约分离）必须先于快轨 Wave-F1 中的"秒杀事件发布"完成。F1.1 临时保持双身份事件，M1 完成后重构。其余快轨项不依赖慢轨。
- **资源分配**：快轨 1 人即可推进（小改动密集）；慢轨建议 2 人协作（架构改动需评审）。
- **验收独立性**：每个 Wave/里程碑有独立验收清单，可独立合并、独立回滚，互不阻塞。
- **回归门槛**：快轨每个 Wave 合并前跑全量 1648 个既有测试；慢轨每个里程碑合并前同样跑全量测试 + 该里程碑专项集成测试。

### 3.4 不在本方案范围

- 业务功能新增（如新促销类型、新支付渠道）
- 前端优化
- 数据库引擎更换（保持 SQL Server）
- 已良好实现的能力重写

---

## 4 快轨 Wave-F1 — 业务流程断裂修复

### 4.1 F1.1：秒杀下单流程贯通（P0-1）

**问题**：`SeckillAppService.PlaceOrderAsync` 完成预占库存后未发布 `SeckillOrderCreatedEvent`，导致 Order 域无法创建正式订单。

**修复方案**：
- 在 `SeckillOrder` 聚合新增 `MarkOrderCreatedAsync(orderId)` 方法，内部 `AddDomainEvent(new SeckillOrderCreatedEvent(...))`
- `PlaceOrderAsync` 成功创建预占记录后调用该方法
- 事件通过 Outbox 发布（遵循 `SaveChangesWithOutboxAsync` 原子性）
- Order 域 `SeckillOrderEventConsumer` 已就绪，无需改动

**契约约束**：`SeckillOrderCreatedEvent` 当前为双重身份事件（属慢轨 M1 范围）。快轨临时保持现状，M1 完成后改为纯集成事件 `SeckillOrderCreatedIntegrationEvent`，Order 域消费者同步切换。代码注释明确标注"待 M1 重构"。

**验收**：
- 单元测试：`PlaceOrderAsync` 成功后聚合 `DomainEvents` 包含 `SeckillOrderCreatedEvent`
- 集成测试：秒杀下单 → 30 秒内 Order 域出现对应正式订单 → 订单状态为"待支付"

### 4.2 F1.2：ForceCancel 改走 Outbox（P0-2）

**问题**：`OrderAppService.ForceCancelAsync:358` 直接 `_eventBus.PublishAsync(refundEvent)`，绕过 Outbox。

**修复方案**（最小改动）：
- 在 `Order` 聚合新增 `AddForceCancelRefundRequestedEvent(RefundRequestedIntegrationEvent)` 方法
- 移除 `OrderAppService` 对 `IEventBus` 的依赖（若该服务无其他直接发布场景）
- `ForceCancelAsync` 改为：`order.AddForceCancelRefundRequestedEvent(refundEvent); await _unitOfWork.SaveEntitiesAsync(ct);`

**验收**：
- 单元测试：`ForceCancelAsync` 调用后 `Order.DomainEvents` 包含 `RefundRequestedIntegrationEvent`
- 单元测试：`SaveEntitiesAsync` 失败时（mock 抛异常）Outbox 表无记录、订单状态未变更

### 4.3 F1.3：Cart 商品事件消费者实现（P0-3）

**问题**：`ProductEventConsumer` 三个消费者 `HandleAsync` 仅记日志返回。

**修复方案**：
- 新增 `IProductSnapshotAntiCorruption` 防腐层接口（HttpClient 调 Product 域 `internal/v1/products/skus/{skuId}`）
- 维护 Redis 反向索引 `cart:sku:{skuId}` (Set) 记录包含该 SKU 的购物车 ID
- 三个消费者实现：
  - `ProductTakenDownEventConsumer`：查反向索引 → 批量 `cart.MarkSkuInvalid(skuId)` → `SaveEntitiesAsync`
  - `ProductPublishedEventConsumer`：同理 `cart.MarkSkuValid(skuId)`
  - `ProductUpdatedEventConsumer`：刷新购物车中 SKU 的展示快照（标题/价格/图片）

**性能考量**：热门 SKU 下架时批量处理 + 限流（每批 100 个购物车）+ 异步执行不阻塞消费者。

**幂等**：聚合方法幂等；消费者基类 `IntegrationEventConsumerBase` 已有 EventId 去重。

**验收**：
- 单元测试：3 个消费者分别覆盖正常路径、SKU 不在购物车、反向索引为空
- 集成测试：商品下架 → 5 秒内相关购物车 SKU 标记为 Invalid → 结算预览显示"商品已下架"

### 4.4 F1.4：横向越权修复（P1，与 F1 同批）

**问题**：售后审核/确认收货/订单发货端点仅 `[Authorize(Roles="Seller")]`，未校验卖家归属。

**修复方案**（参照 Product 域 `RequireOwnedSpuAsync` 良好模式）：
- Order 域：`OrderAppService.ShipAsync` 增加 `RequireOwnedOrderAsync(orderId, currentSellerId)`；`ConfirmReceiptAsync` 增加 `RequireOwnedOrderAsync(orderId, currentUserId)`
- ReviewAfterSales 域：`AfterSalesAppService.SellerApproveAsync`/`SellerConfirmReturnAsync` 增加 `RequireOwnedAfterSalesAsync(afterSalesId, currentSellerId)`
- 校验失败抛 `XxxDomainException("无权操作", "XXX_NOT_OWNED")`（ErrorCode 遵循 M2.1 约定）

**前提**：`Order`/`AfterSales` 聚合需暴露 `SellerId`（已存在）。

**验收**：
- 单元测试：非归属卖家调用 → 抛异常
- 单元测试：归属卖家调用 → 正常执行

### 4.5 Wave-F1 风险与缓解

| 风险 | 缓解 |
|---|---|
| Cart 反向索引一致性 | 索引维护与购物车操作同事务；后台对账任务定期校验 |
| 秒杀事件双身份问题（F1.1 临时方案） | M1 完成后统一重构，F1.1 代码注释明确标注 |
| ForceCancel 改动影响既有退款流程 | 灰度上线，观察 1 周退款事件投递成功率 |

---

## 5 快轨 Wave-F2 — 安全默认修复

### 5.1 F2.1：API 网关 JWT 本地验签（P0-4）

**问题**：网关未调用 `AddAuthentication`/`AddJwtBearer`，客户端可伪造头。

**修复方案**：
- 网关 `Program.cs` 增加 `AddAuthentication().AddJwtBearer()`，复用 `JwtTokenGenerator.BuildValidationParameters()`
- 管道中 `app.UseAuthentication()` 置于 `UseCors()` 之后、`UseRouting()` 之前
- 白名单路由（`/api/auth/login`、`/health`、`/metrics`、`/api/auth/register`）跳过验签

**JWT SecretKey 来源**：从 Consul KV `leno/security/jwt` 读取（与各业务服务共用同一密钥，JWT 需跨服务验签）。

**验收**：
- 单元测试：无效/过期/伪造 token 返回 401
- 单元测试：白名单路由无 token 返回 200
- 集成测试：伪造 `X-User-Id: 999` 头但 JWT 中 Sub=123 → 网关覆盖为 123

### 5.2 F2.2：JWT 黑名单拦截（P1）

**问题**：登出后 token 仍有效，无服务端吊销能力。

**修复方案**：
- 新建 `Middleware/JwtBlacklistMiddleware.cs`，紧随 `UseAuthentication()` 之后
- 查 Redis `leno:jwt:blacklist:{jti}`，命中返回 401 + `gateway_blacklist_hits.Inc()`
- UserAuth 域登出接口调用 `IJwtBlacklistService.RevokeAsync(jti, ttl)`
- 三层保障：Redis Pub/Sub 实时推送（毫秒级）+ 定时拉取（5 分钟兜底）+ 启动预热（无安全窗口）

**验收**：
- 单元测试：jti 命中黑名单返回 401
- 集成测试：登出 → 1 秒内用同 token 访问 → 401

### 5.3 F2.3：后端服务信任网关头适配（P0-4 配套）

**问题**：网关验签后注入头，但 11 个后端服务仍各自验签，且 `CurrentUserContext` 从 JWT Claims 解析。

**修复方案**：
- 新建 `Leno.Infrastructure/Auth/GatewayAuthHandler.cs`：从 `X-User-Id`/`X-Role`/`X-Shop-Id` 头构造 `ClaimsPrincipal`
- `CurrentUserContext` 改为从 `IHttpContextAccessor.HttpContext.User.Claims` 读取
- 各 BC `Program.cs` 的 `AddJwtBearer(...)` 替换为 `AddAuthentication("GatewayHeader").AddScheme<GatewayAuthOptions, GatewayAuthHandler>(...)`

**安全边界**：后端服务容器仅监听内网；可选校验 `X-Internal-Call: gateway` 头；Internal 端点保持 `X-Internal-Key` 机制。

**迁移策略**：灰度切换，配置开关 `Auth:Mode = JwtBearer | GatewayHeader`，默认 `JwtBearer`，验证一周后切 `GatewayHeader`。

**验收**：
- 单元测试：`GatewayAuthHandler` 正确解析头构造 Claims
- 单元测试：缺失 `X-User-Id` 头 → 401
- 集成测试：网关 → 后端全链路，用户上下文正确传递

### 5.4 F2.4：密钥管理与配置中心启用（P0-5/7）

**问题**：11 个服务明文硬编码密钥，且全部共用同一 InternalApiKey。

**修复方案**（分层处理）：

**第一层：立即移除明文密钥**
- `appsettings.json` 中 `Password=`、`SecretKey`、`ApiKey` 改为占位符 `${ENV_VAR}`
- `docker-compose.yml` 改为 `${ENV_VAR}`，密钥由 `.env`（gitignored）或 docker secrets 注入
- `.gitignore` 增加 `appsettings.Production.json`、`.env`

**第二层：启用 Consul KV 配置中心**
- 各 BC `Program.cs` 调用 `builder.AddLenoConsulConfig()`（代码已实现未启用）
- 启动时调用 `builder.Configuration.ValidateSensitiveConfig()` 拒绝缺失关键密钥
- Consul KV 路径约定：`leno/security/jwt`、`leno/security/internal-key/{bc}`、`leno/db/connection-strings/{bc}`、`leno/mq/rabbitmq`

**第三层：各 BC 独立 InternalApiKey**（慢轨 M5.2 收敛，快轨临时共用）
- 快轨：保持临时共用同一 InternalApiKey
- 慢轨 M5.2：11 个 BC 各生成独立 32 字节随机 InternalApiKey，调用方配置目标 BC 的 key

**第四层：JWT SecretKey 强化**
- 至少 64 字节随机串，各环境独立，通过 Consul KV 管理定期轮换

**git 历史清理**：明文密钥已提交历史，需用 `git filter-repo` 清除（运维操作）。

**验收**：
- 单元测试：`ValidateSensitiveConfig` 缺失 key 时抛异常
- 配置审查：grep `appsettings*.json` 无 `Password=`/`SecretKey=`/`ApiKey=` 字面量
- 集成测试：从 Consul KV 拉取配置后服务正常启动

---

## 6 快轨 Wave-F3 — 部署阻塞点

### 6.1 F3.1：EF Core Migrations 生成与启动迁移（P0-6）

**问题**：全代码库无任何 EF Core migration 文件，无 `Database.MigrateAsync` 调用。

**修复方案**：

**第一步：为每个 BC 生成初始迁移**
```bash
dotnet ef migrations add InitialCreate \
  --project src/Services/Order/Leno.Order.Infrastructure \
  --startup-project src/Services/Order/Leno.Order.Api \
  --output-dir Migrations
```
对 11 个 BC 逐一执行。`BaseDbContext.OnModelCreating` 中 `ApplyConfigurationsFromAssembly` 会自动加载所有 `IEntityTypeConfiguration`。

**第二步：启动时迁移（带分布式锁）**
- 新建 `Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs`：
  ```csharp
  public static async Task MigrateWithLockAsync<TDbContext>(
      this IServiceProvider services, CancellationToken ct = default)
      where TDbContext : DbContext
  {
      using var scope = services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
      var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
      await using var handle = await lockService.AcquireLockAsync(
          $"db-migrate:{typeof(TDbContext).Name}", TimeSpan.FromMinutes(5), ct);
      if (handle == null) return;
      await db.Database.MigrateAsync(ct);
  }
  ```
- 各 BC `Program.cs` 中 `app.Run()` 前调用 `await app.Services.MigrateWithLockAsync<XxxDbContext>()`

**第三步：CI 集成**
- CI 流水线增加 `dotnet ef migrations script --idempotent --output migration.sql`
- staging 环境用空库执行验证
- PR 中若包含模型变更但无对应 migration，CI 阻止合并

**生产部署建议**：推荐 K8s Init Container 独立执行迁移（M5.4 Helm chart 落地后）。

**验收**：
- 11 个 BC 各生成 `Migrations/` 目录含 `*_InitialCreate.cs` 与 `ModelSnapshot.cs`
- 空库执行 `dotnet ef database update` 后 schema 与模型一致
- 集成测试：启动服务 → 自动迁移 → 业务接口正常工作
- CI：模型变更但无 migration 时阻止合并

### 6.2 Wave-F3 风险与缓解

| 风险 | 缓解 |
|---|---|
| EF Migrations 多实例并发执行冲突 | Redis 分布式锁；生产推荐 Init Container 模式 |
| git 历史密钥清理可能破坏 fork | 实施前通知所有 fork 持有者；提供密钥轮换脚本 |

---

## 7 快轨 Wave-F4 — 关键测试补齐

### 7.1 F4.1：PointsMembership.Application.Tests 补齐（P0-8）

**修复方案**（优先"写操作"AppService）：
- **MemberAppService**：会员等级升级、订阅激活、等级回滚
- **PointsOffsetAppService**：积分抵现试算、冻结、释放、确认扣减
- **TaskAppService**：任务完成奖励、任务重置、防重复完成
- **MembershipPackageAppService**：套餐订阅、续费、取消

**测试模式**：遵循既有 `PointsAppServiceTests` 模式（xUnit + FluentAssertions + Moq）。

**目标覆盖**：4 个 AppService 每个至少 5 个测试方法，合计 ≥ 20 个。

**验收**：
- `PointsMembership.Application.Tests` 含 ≥ 20 个 `[Fact]/[Theory]`
- 覆盖率：PointsMembership.Application 层 ≥ 60%

### 7.2 F4.2：ReviewAfterSales 与 SellerShop 业务测试补齐（P0-9）

**ReviewAfterSales**：
- **ReviewAppService**：评价提交、审核、回复、追评、统计更新
- **AfterSalesAppService**：售后申请、卖家审核（含 F1.4 越权）、退款发起、退款确认、超时关闭

**SellerShop**：
- **SellerAppService**：入驻申请、审核、状态流转
- **ShopAppService**：店铺创建、状态变更、信息更新
- **SellerDashboardAppService**：看板数据聚合

**目标覆盖**：每个 BC 至少 15 个测试方法。

**验收**：
- `ReviewAfterSales.Application.Tests` 含 ≥ 15 个测试方法
- `SellerShop.Application.Tests` 含 ≥ 15 个测试方法
- 覆盖率：两 BC Application 层 ≥ 60%

### 7.3 F4.3：快轨关键路径集成测试（P0 配套）

**修复方案**：
- 复用 `Leno.Testing/Fixtures/IntegrationTestBase.cs`
- 新增 Testcontainers（RabbitMQ + Redis + SQL Server）
- 4 个关键路径集成测试：

| 测试 | 覆盖流程 | 涉及 BC |
|---|---|---|
| `SeckillOrderFlowIntegrationTests` | 秒杀下单 → 事件 → Order 创建 → 支付 → 秒杀确认 | Promotion + Order + Payment |
| `ForceCancelRefundIntegrationTests` | 强制取消 → Outbox 退款事件 → Payment 处理退款 | Order + Payment |
| `CartProductSyncIntegrationTests` | 商品下架 → 事件 → Cart 标记 SKU 无效 → 结算拦截 | Product + Cart |
| `SellerOwnershipIntegrationTests` | 非归属卖家调用 → 403 | Order + ReviewAfterSales |

**实现策略**：MassTransit InMemory test harness 或 Testcontainers RabbitMQ；防腐层用 WireMock.Net 模拟下游。

**验收**：
- 4 个集成测试文件存在并通过
- CI 中 `Category=Integration` 测试在 staging 环境运行

### 7.4 F4.4：CI 占位零容忍与覆盖率门槛（P1）

**占位零容忍**（复用 `scripts/check-placeholders.sh`）：
- `.github/workflows/ci.yml` 增加步骤扫描 `throw new NotImplementedException`、`return default!`、`return null!`、`Assert.True(true)` 等
- 命中即 `exit 1` 阻止合并

**覆盖率门槛**：
- Domain 层 ≥ 80%、Application 层 ≥ 60%、Infrastructure 层 ≥ 40%
- `reportgenerator` 生成 JSON summary，CI 解析 line coverage，低于阈值 `exit 1`

**临时豁免**：F4.1/F4.2 补齐前，CI 警告但不阻止；F4 合并后转为阻止。

**验收**：
- CI 流水线含 `Scan placeholders` 步骤
- CI 流水线含覆盖率阈值校验
- 覆盖率报告 artifact 上传成功

### 7.5 快轨汇总

| Wave | 任务 | 优先级 | 验收方式 |
|---|---|---|---|
| F1.1 | 秒杀下单流程贯通 | P0 | 单测 + 集成测试 |
| F1.2 | ForceCancel 改走 Outbox | P0 | 单测 |
| F1.3 | Cart 商品事件消费者实现 | P0 | 单测 + 集成测试 |
| F1.4 | 横向越权修复 | P1 | 单测 |
| F2.1 | 网关 JWT 本地验签 | P0 | 单测 + 集成测试 |
| F2.2 | JWT 黑名单拦截 | P1 | 单测 + 集成测试 |
| F2.3 | 后端 GatewayHeader 适配 | P0 | 单测 + 集成测试 |
| F2.4 | 密钥管理 + Consul KV 启用 | P0 | 配置审查 + 启动校验 |
| F3.1 | EF Core Migrations | P0 | 空库迁移 + 集成测试 |
| F4.1 | PointsMembership.Application.Tests | P0 | 覆盖率 ≥ 60% |
| F4.2 | ReviewAfterSales + SellerShop 测试 | P0 | 覆盖率 ≥ 60% |
| F4.3 | 关键路径集成测试 | P0 配套 | CI staging 通过 |
| F4.4 | CI 占位零容忍 + 覆盖率门槛 | P1 | CI 阻止合并 |

**快轨完成后状态**：9 个 P0 全部消除，关键 P1 消除，系统具备生产部署条件。回归 1648 个既有测试 + 新增 ≥ 50 个测试。

---

## 8 慢轨 M1 — BC 边界修复 + 事件契约分离

### 8.1 M1.1：领域事件与集成事件类型分离（P1）

**问题**：65 个事件为双重身份——SharedContracts 中 38 个集成事件实现 `IDomainEvent`，各 BC Domain/Events 中 27 个领域事件继承 `IntegrationEventBase`。Outbox 通过 `domainEvent is IIntegrationEvent` 直接发布领域事件到 MQ。

**修复方案**（类型分离 + Outbox 翻译器）：

**第一步：建立两类基类**
- `Leno.SharedKernel/Abstractions/DomainEventBase.cs`（新建）：仅实现 `IDomainEvent`，携带 `OccurredOn`、`EventId`
- `Leno.SharedContracts/Events/IntegrationEventBase.cs`（修改）：移除 `IDomainEvent` 实现，仅保留 `IIntegrationEvent` + `SchemaVersion`（新增，M4.2 契约治理用）

**第二步：拆分双重身份事件**
- SharedContracts 中 38 个集成事件：去除 `IDomainEvent` 实现；BC 若需领域事件语义，新建 Domain/Events 对应 DomainEvent
- 各 BC Domain/Events 中 27 个领域事件：改继承 `DomainEventBase`；若需对外发布，新建对应集成事件到 SharedContracts

**第三步：引入 IIntegrationEventMapper 翻译器**
- 新建 `Leno.Infrastructure/EventBus/IIntegrationEventMapper.cs`：
  ```csharp
  public interface IIntegrationEventMapper
  {
      IIntegrationEvent? Map(IDomainEvent domainEvent);
  }
  ```
- 修改 `OutboxDbContextExtensions.SaveChangesWithOutboxAsync`：从 `if (domainEvent is IIntegrationEvent)` 改为通过 mapper 翻译
- 各 BC Infrastructure 注册自己的 mapper

**第四步：领域事件契约下沉**
- 各 BC Domain.csproj 移除对 `Leno.SharedContracts` 的引用
- Application/Infrastructure 层可见 SharedContracts，由 mapper 完成翻译

**向后兼容**（双发期 1 周）：
- Outbox 翻译器同时发布新旧两种格式
- 消费者同时订阅两种类型，基于 `EventId` 去重
- 验证 1 周后下线旧格式

**验收**：
- Grep `class.*: IntegrationEventBase, IDomainEvent` 与 `class.*: DomainEventBase, IIntegrationEvent` 零命中
- 各 BC Domain.csproj 不再引用 SharedContracts
- `OutboxDbContextExtensions` 通过 `IIntegrationEventMapper` 翻译
- 全量测试通过

### 8.2 M1.2：Notification BC 跨 BC 引用移除（P1）

**修复方案**（依赖 M1.1 完成）：

**第一步：SharedContracts 新增事件契约**
- `Leno.SharedContracts/Events/PromotionEvents.cs`：`SeckillOrderCreatedIntegrationEvent`、`SeckillStockPreOccupiedIntegrationEvent`
- `Leno.SharedContracts/Events/PointsMembershipEvents.cs`：`PointsEarnedIntegrationEvent`、`PointsConsumedIntegrationEvent`、`PointsRevertedIntegrationEvent`、`MemberLevelChangedIntegrationEvent`、`PaidMemberSubscribedIntegrationEvent`

**第二步：Promotion/PointsMembership 注册 mapper**
- `PromotionIntegrationEventMapper`：`SeckillOrderCreatedDomainEvent → SeckillOrderCreatedIntegrationEvent`
- `PointsMembershipIntegrationEventMapper`：5 个事件同理映射

**第三步：Notification 消费者改订阅集成事件**
- `NotificationEventConsumer.cs`、`PromotionEventConsumer.cs`、`PointsEventConsumer.cs` 改 `using Leno.SharedContracts.Events`

**第四步：移除跨 BC 引用**
- `Notification.Infrastructure.csproj` 删除 2 处 `ProjectReference`

**验收**：
- `Notification.Infrastructure.csproj` 不引用任何其他 BC 的 Domain/Application/Infrastructure
- Grep `using Leno\.(Promotion|PointsMembership)\.Domain` 在 Notification 目录零命中
- 全量测试通过

### 8.3 M1.3：防腐层 ACL 契约清单文档化（P2）

**修复方案**：
- 新建 `docs/contracts/internal-api-contracts.md`，按 BC 列出每个 internal 端点：路径、方法、入参、返回、错误码、调用方 BC、契约版本
- 为 M4.2 Internal API 版本治理做准备

**验收**：契约清单文档存在且覆盖所有 internal 端点。

### 8.4 M1.4：SPU 聚合职责拆分（P1）

**问题**：`SPU.cs` 631 行，承担商品 + 审核 + 价格历史 + 库存操作历史 + 评价评分五类职责。

**修复方案**：
- **保留 SPU 核心**：商品基础信息、SKU 集合、状态机、审核历史
- **拆出价格历史**：新建 `PriceHistory` 聚合（或领域服务），SPU 仅维护当前价格
- **拆出库存操作历史**：归并到 `StockBaseline` 聚合（已存在）
- **评价评分外移**：由 `ReviewReadModel`（ES）维护评分摘要，SPU 通过查询读模型获取

**迁移策略**：先在新聚合/读模型实现等价能力并验证 → 再从 SPU 移除方法 → 评价评分迁移到 ES 读模型后，ReviewAfterSales 域发布评价事件时同步更新 ES 评分摘要。

**验收**：
- `SPU.cs` 行数降至 ≤ 300 行
- SPU 仅含 4 类核心职责
- 库存操作统一由 `StockBaseline` 聚合管理
- 评价评分由 ES 读模型提供
- 全量测试通过

### 8.5 M1 风险与缓解

| 风险 | 缓解 |
|---|---|
| 事件类型拆分影响面广（65 个事件、Outbox、所有消费者） | 双发期 1 周观察 MQ 消费 lag；下线前通过 MassTransit 拓扑检查确认无消费者订阅旧格式；保留回滚开关 |
| mapper 注册遗漏导致事件未发布 | 每个 BC mapper 注册后跑专项测试；CI 静态扫描所有 BC Infrastructure 确认 mapper 已注册 |
| SPU 聚合拆分破坏既有商品域测试 | 先补 SPU 现有行为的快照测试；分步拆分，每步独立验证 |

---

## 9 慢轨 M2 — 共享内核清理

### 9.1 M2.1：DomainException 移除 HttpStatusCode（P1）

**修复方案**：
- `DomainException` 仅保留 `ErrorCode`（string）+ `Message`，移除 `HttpStatusCode`
- 新建 `Leno.Infrastructure/Middleware/ErrorCodeMapping.cs`：ErrorCode → HTTP 状态码映射
- `GlobalExceptionMiddleware` 改为查 `ErrorCodeMapping.GetStatusCode(ex.ErrorCode)`
- ErrorCode 命名约定：`DOMAIN_ENTITY_ACTION` 格式（如 `PRODUCT_NOT_FOUND`、`ORDER_NOT_OWNED`、`COUPON_ALREADY_RECEIVED`）

**迁移步骤**：
1. 建 `ErrorCodeMapping` 并让 `GlobalExceptionMiddleware` 优先查映射表，未命中回退 `DomainException.HttpStatusCode`（兼容期）
2. 逐 BC 改造异常抛出（删 HttpStatusCode 参数，加 ErrorCode）
3. 全部改造完成后删除 `DomainException.HttpStatusCode` 字段

**验收**：
- `DomainException` 无 `HttpStatusCode` 字段
- Grep `new.*DomainException.*40[0-9]|new.*DomainException.*50[0-9]` 零命中
- 全量测试通过

### 9.2 M2.2：MoneyJsonConverter 存储格式外迁（P1）

**修复方案**：
- 删除 `MoneyJsonConverter.ToStorage`/`FromStorage` 静态方法
- 新建 `Leno.Infrastructure/Persistence/MoneyValueConverter.cs`：`ValueConverter<Money, string>`，存储格式保持 `amount|currency` 不变
- 各 BC `IEntityTypeConfiguration<T>` 中 `OwnsOne` Money 改为 `Property(...).HasConversion<MoneyValueConverter>()`
- `MoneyJsonConverter` 仅保留 JSON 序列化职责
- 移除 `SpecAttribute.cs:17` 的 `[JsonConstructor]` 标注

**验收**：
- `MoneyJsonConverter` 无 `ToStorage`/`FromStorage` 方法
- `MoneyValueConverter` 存在于 `Leno.Infrastructure/Persistence/`
- 全量测试通过

### 9.3 M2.3：PageResult 双定义合并（P1）

**修复方案**：
- 删除 `SharedKernel/ValueObjects/PageResult.cs`
- 领域层如需分页结果，复用 `SharedContracts.Responses.PageResult<T>` 或使用 `(IReadOnlyList<T> Items, int Total)` 元组

**验收**：
- `PageResult<T>` 仅在 SharedContracts 存在一份
- 全量测试通过

---

## 10 慢轨 M3 — 跨 BC 样板去重

### 10.1 M3.1：泛型 EfCoreUnitOfWork&lt;TDbContext&gt; 抽取（P1）

**修复方案**：
- 新建 `Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs`：泛型 `EfCoreUnitOfWork<TDbContext> : IUnitOfWork` + 内部 `EfCoreUnitOfWorkTransaction`
- 删除 11 个 BC 的 `UnitOfWork.cs`
- 各 BC DI 注册改为 `services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<XxxDbContext>>()`

**验收**：
- 11 个 BC 的 `UnitOfWork.cs` 删除
- 全量测试通过
- 消除约 680 行重复代码

### 10.2 M3.2：BaseDbContext 暴露 OutboxMessages DbSet（P1）

**修复方案**：
- `BaseDbContext.cs` 添加 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();`
- 11 个 BC DbContext 删除该声明
- `OutboxMessageConfiguration` 上移到 `Leno.Infrastructure`（11 份重复约 308 行），由 `BaseDbContext.OnModelCreating` 显式 `modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration())`

**验收**：
- 11 个 BC DbContext 无 `OutboxMessages` 声明
- `OutboxMessageConfiguration` 仅在 `Leno.Infrastructure` 一份
- 全量测试通过

### 10.3 M3.3：AddLenoService 一站式扩展方法（P1）

**修复方案**：
- 新建 `Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`：
  ```csharp
  public static IServiceCollection AddLenoApi<TDbContext>(
      this IServiceCollection services,
      IConfiguration configuration,
      string serviceName,
      Action<IBusRegistrationConfigurator>? configureConsumers = null,
      Action<IServiceCollection>? configureInfrastructure = null)
      where TDbContext : DbContext
  {
      services.AddLenoInfrastructure(configuration, configureConsumers);
      services.AddLenoConsulConfig();
      services.AddInternalApiKeyAuth(configuration);
      services.AddLenoOpenTelemetry(serviceName);
      services.AddLenoHealthChecks<TDbContext>(configuration);
      services.AddControllers();
      services.AddOpenApi();
      var authMode = configuration["Auth:Mode"] ?? "JwtBearer";
      if (authMode == "GatewayHeader")
          services.AddAuthentication("GatewayHeader").AddScheme<GatewayAuthOptions, GatewayAuthHandler>(...);
      else
          services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...);
      return services;
  }

  public static WebApplication UseLenoPipeline(this WebApplication app) { /* ... */ }
  ```
- 各 BC `Program.cs` 缩减到 ~15 行：
  ```csharp
  var builder = WebApplication.CreateBuilder(args);
  builder.Services.AddLenoApi<OrderDbContext>(
      builder.Configuration, "leno-order-api",
      cfg => cfg.AddOrderConsumers(),
      s => s.AddOrderInfrastructure(builder.Configuration));
  var app = builder.Build();
  await app.Services.MigrateWithLockAsync<OrderDbContext>();
  app.UseLenoPipeline();
  app.Run();
  ```

**AddLenoHealthChecks 合并**：把 RabbitMQ + SqlServer + Redis + ES 探活统一合并进 `AddLenoHealthChecks<TDbContext>`（修复健康检查缺 RabbitMQ 问题）。

**验收**：
- 11 个 BC `Program.cs` ≤ 20 行
- 11 个 BC 健康检查含 RabbitMQ 探活
- 全量测试通过
- 消除约 880 行重复代码

### 10.4 M3.4：AntiCorruptionServices 拆分与命名统一（P1）

**修复方案**：
- 拆分 `AntiCorruptionServices.cs` 为 3 个独立文件：`ProductAntiCorruptionService.cs`、`PromotionAntiCorruptionService.cs`、`PointsAntiCorruptionService.cs`
- `IPointsOffsetService` → `IPointsOffsetAppService`
- DTO 命名约定：查询返回 `XxxDto`、命令入参 `XxxRequest`、返回 `XxxResponse`
- 测试文件统一 `XxxTests.cs` 复数形式

**验收**：
- `AntiCorruptionServices.cs` 拆分为 3 个文件
- Grep `IPointsOffsetService` 零命中
- 编码规范含 DTO/测试命名约定

---

## 11 慢轨 M4 — 通信升级

### 11.1 M4.1：HttpClient 防腐层 Polly 策略统一（P1）

**修复方案**：
- 新建 `Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs` 抽象基类，统一 `ExecuteAsync` 错误处理与 `Metrics.RecordFailure`
- 所有防腐层服务继承 `AntiCorruptionBase`
- `AddHttpClient<T>` 链上接入 Polly：重试 3 次（指数退避）+ 熔断（失败率 50% 断 30s）+ Timeout 10s

**错误处理策略统一**：
- **写操作**（冻结积分、锁券、释放库存）：`throwOnFailure=true`，抛 `Xxx_UNAVAILABLE` 异常
- **读操作**（查询 SKU、查询支付信息）：`throwOnFailure=true`，抛异常由调用方决定降级，不再返回 null
- 网络故障统一映射 HTTP 503

**验收**：
- 所有防腐层服务继承 `AntiCorruptionBase`
- 所有 `AddHttpClient<T>` 配置 Polly
- Grep 防腐层 `return null` / `return default` 零命中（除 `ExecuteAsync` 内部）
- 全量测试通过

### 11.2 M4.2：Internal API 版本治理（P1）

**修复方案**：
- 所有 internal 路由加 `/v1/` 前缀：`/internal/v1/products/skus/{skuId}`
- 防腐层调用方同步改为 `/internal/v1/...`
- `IntegrationEventBase` 增加 `SchemaVersion` 字段（默认 1），Outbox 持久化版本号
- 消费者可按 `SchemaVersion` 路由 handler

**迁移策略**：双路由期 1 周，验证后下线旧路由。

**验收**：
- Grep `RouteAttribute.*"internal/` 全部含 `/v1/` 前缀
- `IntegrationEventBase` 含 `SchemaVersion` 字段
- 全量测试通过

### 11.3 M4.3：gRPC 契约定义与服务端实现（P2，覆盖所有同步跨 BC 调用）

**修复方案**（覆盖 11 个 BC 的所有同步跨 BC 调用）：

| 域 | .proto 服务 | 方法 | 调用方 |
|---|---|---|---|
| Product | `ProductInternalService` | `GetSkuInfo`、`BatchGetSkuInfo`、`GetSkuStock`、`GetProductDetail` | Order、Cart、ReviewAfterSales |
| Promotion | `PromotionInternalService` | `CalculateDiscount`、`LockCoupon`、`ReleaseCoupons`、`GetCouponInfo` | Order、Cart |
| Points | `PointsInternalService` | `TrialOffset`、`Freeze`、`Release`、`Confirm`、`GetPointsBalance` | Order、Payment |
| User | `UserInternalService` | `GetUserContacts`、`GetUserInfo`、`GetUserAddresses` | Notification、Order、ReviewAfterSales |
| Order | `OrderInternalService` | `GetOrderStatus`、`GetOrderDetail`、`GetSellerOrders` | Payment、ReviewAfterSales、SellerShop |
| Payment | `PaymentInternalService` | `GetPaymentInfo`、`GetRefundStatus` | ReviewAfterSales、Order |
| Cart | `CartInternalService` | `GetCartSnapshot`、`GetCheckoutPreview` | Order |
| SellerShop | `SellerInternalService` | `GetSellerInfo`、`GetShopInfo`、`ValidateSellerOwnership` | Product、Order、ReviewAfterSales |
| ReviewAfterSales | `ReviewInternalService` | `GetProductRating`、`GetOrderReviews` | Product、Order |
| Notification | `NotificationInternalService` | `GetNotificationPreference`、`SendNotification` | 各 BC |
| SystemAdmin | `SystemInternalService` | `GetFeatureFlag`、`GetSystemConfig`、`RecordAuditLog` | 各 BC |

**契约治理**：
- 11 个 .proto 文件统一放 `Leno.SharedContracts/Protos/`
- package 含版本 `leno.<bc>.v1`
- buf CLI 校验向后兼容（CI 集成 `buf lint` + `buf breaking`）
- .proto 文件作为跨 BC 契约唯一真相源，Internal REST 控制器最终下线

**迁移批次**（按依赖与风险递增）：
1. 批次 1（M4.3a）：6 个高频防腐层（Product/Promotion/Points/User/Order/Payment）→ 验证稳定 1 周
2. 批次 2（M4.3b）：Cart、SellerShop（含 `ValidateSellerOwnership` 越权校验复用，F1.4 应用层校验迁移到 SellerShop 域集中提供）
3. 批次 3（M4.3c）：ReviewAfterSales、Notification、SystemAdmin

**服务端实现**：各 BC.Api 新增 `GrpcServices/`，复用既有 `IXxxInternalQueryService` 业务逻辑；gRPC 端口 5251-5261（HTTP 端口 +100）。

**客户端迁移**：新建 `Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs`，各 BC 防腐层服务改为注入类型化 gRPC 客户端。

**过渡兼容**：配置开关 `AntiCorruption:UseGrpc`（默认 false），灰度切换；全量验证后删除各 BC 的 `internal/*` REST 控制器与 HttpClient 防腐层代码。

**性能预期**：序列化 + 网络往返从 5-10ms 降到 1-2ms，OrderSaga 单次下单 30-60ms 降到 5-10ms。

**验收**：
- `Leno.SharedContracts/Protos/` 含 11 个 .proto 文件
- CI 集成 `buf lint` + `buf breaking` 校验
- 11 个 BC.Api 含 `GrpcServices/` 实现
- 所有同步跨 BC 调用通过 gRPC（HttpClient 防腐层全部下线）
- `SellerShop.ValidateSellerOwnership` 被各调用方复用
- 性能基准：gRPC 调用 P99 < HttpClient 的 30%

---

## 12 慢轨 M5 — 可观测性与部署补齐

### 12.1 M5.1：业务服务 Prometheus 指标暴露（P1）

**修复方案**：
- 各 BC `AddLenoApi` 统一增加 `services.AddMetricServer(o => o.Port = 8080)` 与 `UseLenoPipeline` 中 `app.UseMetricServer("/metrics")`
- 各 BC `AddLenoOpenTelemetry` 回调追加 `.AddMeter("Leno.<BC>.AntiCorruption").AddMeter("Leno.SystemAdmin.DeadLetter")`
- `grafana/prometheus.yml` 增加 11 个业务服务 scrape_configs
- 统一指标命名前缀 `leno_<bc>_<metric>`

**验收**：
- 11 个 BC 暴露 `/metrics` 端点
- Prometheus 抓取配置含 11 个业务服务
- Grafana dashboard 新增业务服务指标面板

### 12.2 M5.2：Consul KV 配置中心启用收敛（P1）

**修复方案**（F2.4 基础上收敛）：
- F2.4 已通过 `AddLenoApi` 统一调用 `AddLenoConsulConfig`
- M5.2 补充：所有敏感配置（JWT/DB/MQ/InternalApiKey）完全迁移到 Consul KV
- 各 BC 独立 InternalApiKey（11 个 BC 各生成独立 32 字节随机 key）
- 启动校验 `ValidateSensitiveConfig` 在 Consul 不可达时降级为 warning

**验收**：
- 所有敏感配置通过 Consul KV 管理
- `appsettings*.json` 无明文密钥
- 11 个 BC 各有独立 InternalApiKey

### 12.3 M5.3：告警通知渠道接入（P1）

**修复方案**：
- 部署 Alertmanager 容器（docker-compose 新增服务）
- `grafana/provisioning/alerting/` 新增告警规则：
  - `outbox_pending_count > 100` 持续 5min
  - `leno_systemadmin_deadletter_count > 50` 持续 5min
  - `rate(leno_order_anticorruption_failure_total[5m]) > 0.1` 持续 5min
  - `up{job="leno-services"} == 0` 持续 1min
- Outbox 积压新增 Prometheus 指标 `outbox_pending_count`（gauge）
- Alertmanager 接入钉钉/企业微信 webhook

**验收**：
- Alertmanager 容器启动
- 告警规则文件存在
- `outbox_pending_count` 指标暴露
- 测试告警触发后通知送达

### 12.4 M5.4：K8s Helm Chart（P1）

**修复方案**：
- 新建 `deploy/helm/leno/` Helm chart（umbrella chart 结构）：
  - `Chart.yaml`、`values.yaml`
  - `templates/`：每个 BC 一个 Deployment + Service + ConfigMap + Secret 引用
  - 通用模板：`_helpers.tpl`、`deployment.yaml`、`service.yaml`、`hpa.yaml`、`ingress.yaml`
- `values.yaml` 区分 dev/staging/prod
- 敏感配置通过 K8s Secret 引用（External Secrets Operator 对接 Vault/Consul）
- 数据库迁移用 Init Container 执行（生产推荐）
- 每个服务配置 readiness/liveness probe（指向 `/health/ready`、`/health/live`）
- HPA 基于 CPU/内存 + 自定义指标（QPS）

**验收**：
- `deploy/helm/leno/` 目录存在且 `helm lint` 通过
- `helm template` 渲染出 11 个 BC + 网关的 Deployment/Service
- values 文件区分 dev/staging/prod
- Init Container 执行迁移
- readiness/liveness probe 配置正确

### 12.5 M5.5：CI 覆盖率门槛与集成测试收敛（P1）

**修复方案**（F4.4 基础上收敛）：
- F4.3 的 4 个集成测试在 CI staging 环境（非 PR 检查）运行
- CI 流水线新增 staging 集成测试 job：部署 docker-compose → 跑 `dotnet test --filter "Category=Integration"` → 销毁环境
- 覆盖率报告发布到 SonarCloud 或 Codecov（可选）

**验收**：
- CI staging job 运行 4 个集成测试通过
- 覆盖率报告持续追踪

---

## 13 慢轨 M6 — CQRS + BFF + 文档同步

### 13.1 M6.1：ES 读模型同步补齐（P2）

**修复方案**（读多写少场景优先）：

**Promotion 域**：
- `SeckillActivityReadModel`（秒杀活动读模型）
- `CouponReadModel`（优惠券读模型）

**PointsMembership 域**：
- `PointsAccountReadModel`（积分账户读模型）
- `MemberReadModel`（会员读模型）

**SellerShop 域**（M6.3 店铺看板配套）：
- `ShopDashboardReadModel`（店铺看板读模型）

**基类增强**：扩展 `ReadModelSyncConsumerBase` 支持删除场景（当前 `ProductTakenDownReadModelSyncConsumer` 裸实现 `IConsumer`）。

**不引入**：Cart（天然读写同一 Redis 缓存）、Notification、SystemAdmin、Payment、UserAuth（无读多写少场景）。

**验收**：
- Promotion、PointsMembership、SellerShop 三个 BC 含 `ReadModels/` 目录与 `ReadModelSyncConsumerBase` 实现
- `ReadModelSyncConsumerBase` 支持删除场景
- 全量测试通过

### 13.2 M6.2：显式 Query Handler 分离（P2）

**修复方案**（仅 Product/Order/SellerShop 落地，不引入 MediatR）：

**Product 域**：
- `ProductSearchQuery` + `ProductSearchQueryHandler`（走 ES）
- `ProductDetailQuery` + `ProductDetailQueryHandler`（走 ES）
- `IProductSearchService` 拆为 Query + Handler，原 AppService 保留写操作

**Order 域**：
- `OrderListQuery` + `OrderListQueryHandler`（走 ES）
- `OrderDetailQuery` + `OrderDetailQueryHandler`（走 ES）
- `LogisticsTraceQuery` + `LogisticsTraceQueryHandler`
- 写操作（PlaceOrder、Cancel、ConfirmReceipt、Ship）保留在 `OrderAppService`

**SellerShop 域**：
- `ShopDashboardQuery` + `ShopDashboardQueryHandler`（走 M6.1 ShopDashboardReadModel）

**Query Handler 约定**（写入编码规范）：
- 命名：`XxxQueryHandler : IQueryHandler<XxxQuery, XxxResult>`
- 只读标注：`[ReadOnly]` 特性，禁止调用 `SaveChangesAsync`
- DI 注册：`services.AddScoped<IQueryHandler<XxxQuery, XxxResult>, XxxQueryHandler>()`
- 不引入 MediatR（用接口 + DI 即可）

**不强制全 BC 落地**：Cart、Notification、SystemAdmin、Payment、UserAuth 等维持单一 AppService 模式可接受。

**验收**：
- `Leno.Product.Application/Queries/`、`Leno.Order.Application/Queries/`、`Leno.SellerShop.Application/Queries/` 目录存在
- 各含 ≥ 2 个 Query + QueryHandler
- QueryHandler 走 ES 读模型
- 全量测试通过

### 13.3 M6.3：BFF 聚合层（P2）

**修复方案**（4 个高频聚合端点）：

| BFF 端点 | 聚合内容 | 调用方 |
|---|---|---|
| `GET /api/bff/order-detail/{orderId}` | 订单详情 + 商品快照 + 物流轨迹 + 评价摘要 | 订单详情页 |
| `GET /api/bff/product-detail/{spuId}` | SPU 详情 + SKU 列表 + 评价评分 + 店铺信息 | 商品详情页 |
| `GET /api/bff/cart-checkout-preview` | 购物车 + SKU 价格 + 优惠试算 + 积分试算 | 结算预览页 |
| `GET /api/bff/seller-dashboard` | 订单数 + 销售额 + 商品数 + 待处理售后 | 卖家工作台 |

**实现方式**：
- 使用 YARP `IForwarder` + 并行 HttpClient（或 gRPC 客户端，M4.3 完成后）聚合
- `Parallel.ForEachAsync` 并行调用下游，超时 3 秒，部分失败返回部分数据 + `partial: true` 标记
- 缓存策略：订单详情缓存 30 秒，商品详情缓存 5 分钟（复用网关 CacheMiddleware）

**验收**：
- `Leno.ApiGateway/Bff/` 目录存在
- 4 个 BFF 端点可访问并返回聚合数据
- 单元测试：部分下游失败时返回 `partial: true`
- 全量测试通过

### 13.4 M6.4：缓存 Key 安全加固（P2）

**修复方案**：
- 网关 `CacheMiddleware.GenerateCacheKey` 增加 `Role` 维度：`method:path:querystring:userId:role`
- 敏感数据端点（如卖家工作台）增加 `ShopId` 维度
- 缓存失效广播同步包含 `role`/`shopId` 维度

**验收**：
- `GenerateCacheKey` 含 `role` 维度
- 单元测试：不同 role 相同 path 生成不同 key
- 全量测试通过

### 13.5 M6.5：文档与规范同步（P2）

**更新 `docs/编码规范.md`**：
- 第 4.1 节"聚合根基类"：移除 `Version` 字段示例（M2.1 已完成）
- 第 6.2 节"集成事件"：补充"领域事件 → 集成事件翻译"约定（`IIntegrationEventMapper`）
- 第 2.2 节"项目命名与依赖关系"：补充 `Leno.Infrastructure.Abstractions` 子命名空间
- 新增第 13.4 节"占位实现禁止"（F4.4 已落地）
- 新增第 14 章"安全编码约定"：密钥管理（Consul KV）、JWT 黑名单、InternalApiKey 各 BC 独立、ErrorCode 命名约定 `DOMAIN_ENTITY_ACTION`、防腐层错误处理
- 新增第 15 章"gRPC 内部服务通信"：.proto 治理、版本化（`leno.<bc>.v1`）、buf CLI 校验、错误映射
- 新增第 16 章"CQRS Query Handler 约定"：Query/QueryHandler 命名、`[ReadOnly]` 标注、DI 注册模式

**更新 `docs/spec/00-需求文档总览与DDD架构.md`**：同步事件契约分离决策、Internal API 版本治理、gRPC 通信决策。

**新增 `docs/contracts/internal-api-contracts.md`**（M1.3 已落地，M6.5 补充 gRPC 契约）：11 个 BC 的 internal 端点契约（REST + gRPC）、版本演进记录。

**PR 模板更新**：
- 新增 checklist 项："本 PR 不含任何占位实现"
- 新增 checklist 项："本 PR 含模型变更时已生成 EF migration"
- 新增 checklist 项："本 PR 含跨 BC 调用变更时已更新 .proto 契约"

**验收**：
- `docs/编码规范.md` 含第 14/15/16 章新增内容
- `docs/spec/00-需求文档总览与DDD架构.md` 同步架构决策
- `docs/contracts/internal-api-contracts.md` 覆盖 REST + gRPC 契约
- PR 模板含 3 个新 checklist 项

### 13.6 M6.6：慢轨最终回归与既有 spec 整合（P2）

**修复方案**：

**整合既有 spec 状态**：
- `2026-07-13-comprehensive-optimization-design.md`：标注本方案 supersede 关系，已完成项标记 `[x]`，未完成项由本方案接管标记 `[→ 本方案 Mx]`
- `2026-07-14-api-gateway-enhancement-design.md`：标注本方案 M4/M5 的增量（gRPC、BFF、告警）
- `.trae/specs/fix-critical-business-vulnerabilities/`：标注本方案 F1/F2 的增量

**全局回归测试**：
- 全量 1648+ 既有测试 + 快轨新增 ≥ 50 个 + 慢轨新增测试全部通过
- 4 个跨 BC 集成测试（F4.3）通过
- 性能基准：gRPC 调用 P99 < HttpClient 的 30%
- 部署验证：Helm chart `helm install` 后全链路可用

**文档归档**：
- 本方案最终状态归档到 `docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md`
- 实施过程的关键决策记录到 `docs/decisions/`（ADR 风格）

**验收**：
- 3 份既有 spec 含 supersede/接管标注
- 全量测试通过
- Helm chart 部署验证通过
- 本方案文档归档完成

---

## 14 验收标准汇总

### 14.1 快轨验收

- [ ] F1.1 秒杀下单 → Order 域 30 秒内出现正式订单
- [ ] F1.2 ForceCancel 通过 Outbox 发布，`SaveEntitiesAsync` 失败时无 Outbox 记录
- [ ] F1.3 商品下架 → 5 秒内购物车 SKU 标记 Invalid
- [ ] F1.4 非归属卖家调用 → 抛异常
- [ ] F2.1 无效 token 返回 401，白名单路由放行
- [ ] F2.2 登出 → 1 秒内同 token 返回 401
- [ ] F2.3 `Auth:Mode=GatewayHeader` 切换后全链路用户上下文正确
- [ ] F2.4 grep `appsettings*.json` 无明文密钥
- [ ] F3.1 空库 `dotnet ef database update` 后 schema 与模型一致
- [ ] F4.1 PointsMembership.Application.Tests ≥ 20 个测试
- [ ] F4.2 ReviewAfterSales + SellerShop 各 ≥ 15 个测试
- [ ] F4.3 4 个集成测试通过
- [ ] F4.4 CI 占位零容忍 + 覆盖率门槛生效

### 14.2 慢轨验收

**M1**：
- [ ] 65 个双重身份事件全部拆分
- [ ] `IIntegrationEventMapper` 引入，Outbox 通过 mapper 翻译
- [ ] 各 BC Domain.csproj 不引用 SharedContracts
- [ ] Notification.Infrastructure 不引用任何 BC 的 Domain
- [ ] SPU.cs ≤ 300 行

**M2**：
- [ ] `DomainException` 无 `HttpStatusCode`，`ErrorCodeMapping` 接管
- [ ] `MoneyJsonConverter` 无 `ToStorage/FromStorage`，`MoneyValueConverter` 外迁
- [ ] `PageResult<T>` 仅 SharedContracts 一份

**M3**：
- [ ] 11 个 BC 无 `UnitOfWork.cs`，统一 `EfCoreUnitOfWork<TDbContext>`
- [ ] `BaseDbContext` 暴露 `OutboxMessages`，`OutboxMessageConfiguration` 仅一份
- [ ] 11 个 BC `Program.cs` ≤ 20 行
- [ ] 消除约 1870 行重复代码

**M4**：
- [ ] 所有防腐层继承 `AntiCorruptionBase`，Polly 统一配置
- [ ] Internal API 全部 `/v1/` 前缀，`IntegrationEventBase` 含 `SchemaVersion`
- [ ] `Leno.SharedContracts/Protos/` 含 11 个 .proto，CI 集成 buf 校验
- [ ] 所有同步跨 BC 调用通过 gRPC，HttpClient 防腐层下线
- [ ] gRPC 调用 P99 < HttpClient 的 30%

**M5**：
- [ ] 11 个 BC 暴露 `/metrics`，Prometheus 抓取完整
- [ ] Consul KV 配置中心全量启用，11 个 BC 独立 InternalApiKey
- [ ] Alertmanager 容器 + 告警规则 + 通知渠道
- [ ] `deploy/helm/leno/` Helm chart 可渲染
- [ ] CI staging 集成测试 job 通过

**M6**：
- [ ] Promotion、PointsMembership、SellerShop 三个 BC 含 ES 读模型同步
- [ ] Product、Order、SellerShop 含 Query Handler 分离
- [ ] `Leno.ApiGateway/Bff/` 含 4 个聚合端点
- [ ] 缓存 Key 含 `role` 维度
- [ ] `docs/编码规范.md` 含第 14/15/16 章
- [ ] 3 份既有 spec 含 supersede 标注
- [ ] 全量测试通过 + 部署验证通过

---

## 15 风险与缓解

### 15.1 快轨风险

| 风险 | 缓解 |
|---|---|
| 秒杀事件双身份问题（F1.1 临时方案） | M1 完成后统一重构，代码注释标注 |
| ForceCancel 改动影响既有退款流程 | 灰度上线，观察 1 周退款事件投递成功率 |
| Cart 反向索引一致性 | 索引维护与购物车操作同事务；后台对账任务 |
| 启用 GatewayHeader 认证后既有 JWT 调用失败 | `Auth:Mode` 灰度切换；先 staging 验证一周 |
| Consul KV 故障导致服务无法启动 | `AddLenoConsulConfig` 已 `Optional=true`，故障时回退本地 appsettings |
| EF Migrations 多实例并发冲突 | Redis 分布式锁；生产推荐 Init Container |
| git 历史密钥清理可能破坏 fork | 实施前通知 fork 持有者；提供密钥轮换脚本 |

### 15.2 慢轨风险

| 风险 | 缓解 |
|---|---|
| 事件类型拆分影响面广（65 个事件） | 双发期 1 周观察 MQ 消费 lag；保留回滚开关 |
| mapper 注册遗漏导致事件未发布 | 专项测试 + CI 静态扫描 |
| SPU 聚合拆分破坏既有测试 | 先补快照测试；分步拆分，每步独立验证 |
| DomainException 改造影响各 BC | 三步迁移（建映射表 → 逐 BC 改造 → 删字段） |
| MoneyValueConverter 迁移后存储格式变化 | 存储格式保持不变，仅迁移代码位置 |
| gRPC 迁移影响生产 | `AntiCorruption:UseGrpc` 默认 false，灰度切换；失败 fallback HttpClient |
| Internal API 双路由期配置遗漏 | 防腐层同时支持新旧路由；CI 静态扫描 |
| Prometheus 抓取 11 个服务增加负载 | 抓取间隔 15s；指标标签数 ≤ 5 |
| Helm chart 与 docker-compose 配置漂移 | values 文件复用环境变量；CI 校验一致性 |

---

## 16 实施顺序

```
快轨 (Fast Track):
  Wave-F1: 业务流程断裂修复（F1.1/F1.2/F1.3/F1.4，约 1 周）
  Wave-F2: 安全默认修复（F2.1/F2.2/F2.3/F2.4，约 1-2 周）
  Wave-F3: 部署阻塞点（F3.1 EF Migrations，约 3 天）
  Wave-F4: 关键测试补齐（F4.1/F4.2/F4.3/F4.4，约 1-2 周）

慢轨 (Slow Track):
  M1: BC 边界 + 事件契约分离（约 2-3 周，含双发期 1 周）
  M2: 共享内核清理（约 1 周）
  M3: 跨 BC 样板去重（约 1-2 周）
  M4: 通信升级（M4.1 Polly 约 1 周；M4.2 版本治理约 3 天；M4.3 gRPC 3 批次约 4-6 周）
  M5: 可观测性 + 部署补齐（约 2-3 周）
  M6: CQRS + BFF + 文档同步（约 2-3 周）
```

**双轨协调**：快轨 F1.1 完成后即可启动慢轨 M1（事件契约分离），M1 完成后回头重构 F1.1 的临时双身份事件。其余快轨项与慢轨无依赖，可并行推进。

---

## 附录 A：既有 spec 引用

| Spec | 路径 | 与本方案关系 |
|---|---|---|
| 全面优化 V1 | [2026-07-13-comprehensive-optimization-design.md](./2026-07-13-comprehensive-optimization-design.md) | supersede；M1-M6 接管未完成项 |
| API 网关增强 | [2026-07-14-api-gateway-enhancement-design.md](./2026-07-14-api-gateway-enhancement-design.md) | supersede；M4/M5 增量 gRPC、BFF、告警 |
| 关键业务与安全漏洞修复 | [.trae/specs/fix-critical-business-vulnerabilities/](../../../.trae/specs/fix-critical-business-vulnerabilities/) | supersede；F1/F2 增量秒杀流程、ForceCancel、越权、密钥管理 |

---

## 附录 B：术语表

| 术语 | 定义 |
|---|---|
| 限界上下文（BC） | 领域模型的显式边界，拥有独立聚合、统一语言与持久化模型 |
| 防腐层（ACL） | 隔离外部上下文或遗留系统的翻译层 |
| 集成事件 | 跨上下文传递的事件，经事件总线发布订阅，纯契约 |
| 领域事件 | 上下文内部已发生的重要业务事实，可携带丰富状态 |
| IIntegrationEventMapper | 领域事件 → 集成事件的翻译器，Outbox 落库时调用 |
| 发件箱模式 | 聚合保存与事件记录在同一事务写入，后台进程轮询发布 |
| CQRS | 读写职责分离，Command 走写库、Query 走读库 |
| gRPC | 基于 HTTP/2 + Protobuf 的高性能 RPC 协议 |
| YARP | .NET 反向代理库，用于 API 网关 |
| BFF | Backend for Frontend，为前端定制的聚合层 |
| Outbox 两阶段标记 | Pending → Publishing → Processed，防重复发布 |
| AntiCorruptionBase | 防腐层抽象基类，统一错误处理与指标上报 |
| ErrorCodeMapping | ErrorCode → HTTP 状态码映射，解除领域层对 HTTP 的依赖 |
| buf CLI | Protobuf 契约治理工具，lint + breaking 兼容性校验 |

---

**文档结束。本方案为 Leno 电商平台全面优化 V2 的纲领性设计，采用双轨并行结构：快轨消除 9 个 P0 生产阻塞风险，慢轨完成架构演进与通用能力重构。所有子任务实施前需通过架构评审，确保与既有 spec 不冲突。**
