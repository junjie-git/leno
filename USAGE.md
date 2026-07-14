# Leno 电商平台使用说明

> **版本**：v1.0 | **更新日期**：2026-07-14 | **目标框架**：.NET 10.0.301

Leno 是一个基于 DDD（领域驱动设计）+ CQRS（命令查询职责分离）+ 事件驱动架构的微服务电商平台，覆盖 11 个限界上下文（用户认证、商品、购物车、订单、促销、支付、评价售后、积分会员、消息通知、卖家店铺、系统管理）。本文档面向开发者、测试与运维人员，介绍如何构建、运行、测试、调试与部署本项目。

---

## 目录

- [1. 环境要求](#1-环境要求)
- [2. 项目结构总览](#2-项目结构总览)
- [3. 快速开始（Docker Compose 一键启动）](#3-快速开始docker-compose-一键启动)
- [4. 本地开发指南](#4-本地开发指南)
- [5. API 网关](#5-api-网关)
- [6. 微服务一览](#6-微服务一览)
- [7. 基础设施组件](#7-基础设施组件)
- [8. 测试](#8-测试)
- [9. 可观测性](#9-可观测性)
- [10. CI/CD](#10-cicd)
- [11. 编码规范](#11-编码规范)
- [12. 常见问题](#12-常见问题)

---

## 1. 环境要求

| 项 | 版本 / 说明 |
|---|---|
| .NET SDK | **10.0.301**（见 `mise.toml`，建议用 [mise](https://mise.jdx.dev/) 自动管理） |
| Docker Engine | 24+ 与 Docker Compose v2 |
| IDE | Visual Studio 2026 / Rider / VS Code（推荐 C# Dev Kit） |
| Git | 2.30+ |
| 内存 | 完整 docker compose 启动约需 8GB 可用内存 |
| 端口 | 确保 1433/5151-5161/6379/8080/8500/9200/16686/3000/9090 等端口未被占用 |

安装 .NET SDK（任选其一）：

```bash
# 方式 A：mise（推荐，自动匹配仓库版本）
mise install

# 方式 B：手动安装
# 参考 https://dotnet.microsoft.com/download/dotnet/10.0
```

验证：

```bash
dotnet --version   # 应输出 10.0.301 或兼容版本
```

---

## 2. 项目结构总览

```
/workspace
├── Leno.slnx                    # 解决方案（XML 格式，.NET 10 新格式）
├── Directory.Build.props        # 统一构建属性（net10.0、TreatWarningsAsErrors=true）
├── docker-compose.yml           # 全套环境编排
├── mise.toml                    # .NET SDK 版本锁定
├── .editorconfig                # 代码风格
├── .github/workflows/ci.yml     # CI 流水线
├── docs/                        # 需求规格 / 设计 / 计划文档
│   ├── spec/                    # 11 个限界上下文需求文档
│   ├── superpowers/specs/       # 架构设计文档
│   ├── superpowers/plans/       # 分阶段实施计划
│   ├── 技术选型方案.md
│   └── 编码规范.md
├── grafana/                     # 监控配置（Prometheus + Grafana）
├── scripts/                     # 辅助脚本
└── src/
    ├── ApiGateway/              # API 网关（YARP 反向代理）
    │   ├── Leno.ApiGateway/
    │   └── Leno.ApiGateway.Tests/
    ├── BuildingBlocks/          # 共享构建块
    │   ├── Leno.SharedKernel/              # 领域内核（Entity/VO/Money）
    │   ├── Leno.Infrastructure.Abstractions/  # 抽象接口（ICacheService/IEventBus/IFileStorageService）
    │   ├── Leno.Infrastructure/            # 基础设施实现（DbContext/Outbox/RabbitMq/Redis/ES/OTel/Serilog）
    │   ├── Leno.Infrastructure.Tests/
    │   ├── Leno.SharedContracts/          # 集成事件契约 + 统一响应 DTO
    │   └── Leno.Testing/                   # 测试辅助（Testcontainers Fixture）
    └── Services/                # 11 个限界上下文微服务
        ├── UserAuth/            # BC1 用户与认证授权
        ├── Product/             # BC2 商品
        ├── Cart/                # BC3 购物车
        ├── Order/               # BC4 订单与交易
        ├── Promotion/           # BC5 促销
        ├── Payment/             # BC6 支付集成（按文档编号为 BC8）
        ├── PointsMembership/    # BC7 积分与会员
        ├── ReviewAfterSales/    # BC6 评价与售后
        ├── SellerShop/          # BC10 卖家与店铺
        ├── Notification/        # BC9 消息通知
        └── SystemAdmin/         # BC11 系统管理
```

### DDD 四层架构（每个微服务统一遵循）

```
src/Services/{Domain}/
├── {X}.Domain/             # 领域层：Entity / ValueObject / Aggregate / IRepository 接口 / DomainEvent
├── {X}.Application/        # 应用层：IAppService 接口 / DTO / Validators
├── {X}.Infrastructure/     # 基础设施层：DbContext / Repository / UnitOfWork / EventBus Consumers
└── {X}.Api/                # 表现层：Controllers / Program.cs / appsettings.json / Dockerfile
```

依赖方向：`Api → Application → Domain ← Infrastructure`（领域层不引用任何外层）。

---

## 3. 快速开始（Docker Compose 一键启动）

最简单的运行方式是使用 `docker compose`，它会自动拉起全部基础设施 + 11 个微服务 + API 网关。

### 3.1 启动

```bash
cd /workspace
docker compose up -d
```

首次启动会构建所有服务镜像并拉取基础设施镜像，耗时约 10-20 分钟。`api-gateway` 依赖所有微服务 `service_healthy` 后才启动，因此启动顺序由 Compose 自动编排。

### 3.2 查看启动状态

```bash
# 查看所有容器状态
docker compose ps

# 跟踪网关日志
docker compose logs -f api-gateway

# 查看某服务日志
docker compose logs -f product-api
```

### 3.3 健康检查

```bash
# 网关存活探针
curl http://localhost:8080/health/live
# 返回: {"status":"Healthy"}

# 网关就绪探针（包含 Consul 连通性）
curl http://localhost:8080/health/ready

# HealthChecksUI 仪表盘（浏览器访问）
# http://localhost:8080/health-ui
```

### 3.4 停止与清理

```bash
# 停止全部容器（保留数据卷）
docker compose down

# 停止并删除数据卷（清空数据库/Redis/RabbitMQ 等全部持久化数据）
docker compose down -v
```

---

## 4. 本地开发指南

### 4.1 构建与还原

```bash
# 还原全部依赖
dotnet restore Leno.slnx

# 编译整个解决方案（Release）
dotnet build Leno.slnx --configuration Release

# 仅编译网关
dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj
```

> **注意**：`Directory.Build.props` 中 `TreatWarningsAsErrors=true`，任何分析器警告都会导致编译失败，请保持代码规范。

### 4.2 仅启动基础设施（用于本地 IDE 调试）

通常开发单个微服务时不需要启动整套服务，只起依赖的基础设施即可：

```bash
# 仅启动 SQL Server / Redis / RabbitMQ / ES / Consul / Jaeger / Prometheus / Grafana
docker compose up -d sqlserver redis rabbitmq elasticsearch consul jaeger prometheus grafana
```

然后在 IDE 中以 `ASPNETCORE_ENVIRONMENT=Development` 运行目标服务的 `Program.cs`。

### 4.3 连接字符串（开发环境）

| 资源 | 开发地址 | 凭据 |
|---|---|---|
| SQL Server | `localhost,1433` | `sa` / `Leno@2026`（Product 服务本地配置）或 `Leno@SqlServer2019`（compose） |
| Redis | `localhost:6379` | 无密码 |
| RabbitMQ | `localhost:5672` | `leno` / `Leno@RabbitMQ2026`，管理 UI `http://localhost:15672` |
| Elasticsearch | `http://localhost:9200` | 无安全 |
| Consul | `http://localhost:8500` | 无 token |
| Jaeger UI | `http://localhost:16686` | - |
| Grafana | `http://localhost:3000` | `leno` / `Leno@Grafana2026` |
| Prometheus | `http://localhost:9090` | - |

各服务的 `appsettings.json` 中 `ConnectionStrings` 节定义了开发环境的连接字符串，`appsettings.Docker.json` 覆盖为容器名（如 `sqlserver,1433`、`redis:6379`）。

### 4.4 数据库迁移

每个微服务拥有独立的 `DbContext`（按上下文分库），使用 EF Core Code First 迁移。迁移命令示例（以 Product 为例）：

```bash
# 添加迁移
dotnet ef migrations add InitProduct \
  -c ProductDbContext \
  -p src/Services/Product/Leno.Product.Infrastructure \
  -s src/Services/Product/Leno.Product.Api

# 应用迁移
dotnet ef database update \
  -c ProductDbContext \
  -p src/Services/Product/Leno.Product.Infrastructure \
  -s src/Services/Product/Leno.Product.Api
```

迁移规范：仅追加式变更（Add Column）；破坏性变更（Drop/Rename）需分多版本灰度。

### 4.5 占位实现检查

提交前建议运行占位实现检查脚本，确保未提交未实现的业务逻辑：

```bash
./scripts/check-placeholders.sh
```

脚本会扫描 `src/` 下所有 `.cs` 文件，检测 `NotImplementedException`、SmokeTest 占位、非测试代码中的 `return default!`/`return null!`，发现则 `exit 1`。

---

## 5. API 网关

API 网关基于 **YARP 2.2.0**（Yet Another Reverse Proxy），是整个平台的统一入口，承担路由、限流、熔断、超时、重试、CORS、缓存、可观测性等职责。

### 5.1 网关地址

| 环境 | 地址 |
|---|---|
| Docker Compose | `http://localhost:8080` |
| 本地开发（launchSettings） | `http://localhost:5180` / `https://localhost:7288` |

### 5.2 中间件管道顺序

```
请求 → UseObservability（访问日志+指标+/metrics）
     → UseCors（OPTIONS 预检先于缓存处理）
     → FallbackResponseMiddleware（503 降级 JSON）
     → UseResponseCompression（响应压缩，含 HTTPS）
     → CacheMiddleware（Redis 缓存，命中即短路，未命中透传）
     → UseRateLimiter（路由级限流）
     → UseRequestTimeouts（路由级超时）
     → MapReverseProxy（YARP 路由 + Transforms）
```

### 5.3 路由与集群

`appsettings.json` 中 `ReverseProxy.Routes` 定义了 **44 条路由**，分发到 **11 个集群**：

| 集群 | 路由数 | 主要资源前缀 | Consul 服务名 |
|---|---|---|---|
| user-auth | 3 | `/api/auth`、`/api/users`、`/api/admin/users` | `leno-user-auth-api` |
| product | 4 | `/api/products`、`/api/categories`、`/api/brands`、`/api/admin/products` | `leno-product-api` |
| cart | 1 | `/api/cart` | `leno-cart-api` |
| order | 5 | `/api/orders`、`/api/seller/orders`、`/api/freight-templates`、`/api/logistics-companies` | `leno-order-api` |
| promotion | 6 | `/api/promotions`、`/api/coupons`、`/api/seckill` | `leno-promotion-api` |
| payment | 3 | `/api/payments`、`/api/admin/payments` | `leno-payment-api` |
| points | 5 | `/api/points`、`/api/members`、`/api/membership-packages` | `leno-points-api` |
| review-aftersales | 4 | `/api/reviews`、`/api/after-sales` | `leno-review-aftersales-api` |
| seller-shop | 3 | `/api/shops`、`/api/seller` | `leno-seller-shop-api` |
| notification | 3 | `/api/notifications`、`/api/notification-templates`、`/api/notification-preferences` | `leno-notification-api` |
| system-admin | 7 | `/api/operators`、`/api/system-configs`、`/api/feature-flags`、`/api/announcements`、`/api/data-dictionaries`、`/api/scheduled-tasks`、`/api/audit-logs` | `leno-system-admin-api` |

每个集群配置一致：
- `LoadBalancingPolicy`: PowerOfTwoChoices
- `CircuitBreaker`: MaxConcurrentRequests=100, FailureRateThreshold=0.5, SamplingDuration=30s, BreakDuration=30s
- `Retry`: MaxRetries=2, Exponential backoff 500ms-1s, retry on 503
- `HealthCheck.Active`: Interval=10s, Path=`/health/ready`
- `HealthCheck.Passive`: TransportFailureRate, ReactivationPeriod=30s
- Destinations 通过 Consul 动态解析（`Metadata.ConsulServiceName`）

### 5.4 限流策略（三层）

| 策略名 | 适用 | 限制 |
|---|---|---|
| Global | 全局 | 令牌桶 5000/1s |
| `leno-default` | 默认路由 | 滑动窗口 200/1s（4 段） |
| `seckill` | 秒杀路由 | 滑动窗口 50/1s（4 段） |
| `per-user` | 用户级 | 滑动窗口 100/1min（6 段），匿名按 client-ip 分区 |

Redis 启用时使用 `RedisSlidingWindowRateLimiter`（分布式滑动窗口），Key 前缀 `leno:ratelimit:`。超限返回 `429 Too Many Requests`。

### 5.5 超时策略

| 策略名 | 适用 | 请求超时 | 连接超时 | 读取超时 |
|---|---|---|---|---|
| `leno-default` | 常规 | 30s | 5s | 30s |
| `seckill` | 秒杀 | 5s | 2s | 5s |
| `upload` | 文件上传 | 120s | 10s | 120s |
| `internal` | 服务间调用 | 15s | 3s | 15s |

### 5.6 Transforms（请求/响应转换）

- `TracingTransform`：注入 `X-Trace-Id` 头（值为 `Activity.TraceId`）
- `UserContextTransformProvider`：从 JWT Claims 提取 `X-User-Id`/`X-Role`/`X-Shop-Id`/`X-Internal-Call`，响应时清理 `X-Internal-Call`
- 路由级 `Transforms`（appsettings.json）：支持 YARP 内置的 RequestHeader/ResponseHeader/PathRemovePrefix

### 5.7 缓存

- 仅 GET/HEAD 请求缓存，Key 格式 `method:path:query:userId`
- Redis 存储，路径级 TTL（`/api/products/` 300s、`/api/categories/` 60s、默认 60s）
- 失效通过 Redis Pub/Sub `leno:cache:invalidated` 通道，支持精确 Key 和 Glob 模式

### 5.8 CORS

- Origin 列表从 Consul KV `leno/gateway/cors-origins` 读取，每分钟刷新
- 预检 OPTIONS 在缓存之前处理，直接返回

### 5.9 网关暴露的端点

| 端点 | 说明 |
|---|---|
| `/health/live` | 存活探针，仅检查网关进程 |
| `/health/ready` | 就绪探针，含 Consul 连通性检查 |
| `/health-ui` | HealthChecksUI 仪表盘 |
| `/metrics` | Prometheus 指标端点 |
| `/api/{resource}` | 44 条 YARP 反向代理路由 |

---

## 6. 微服务一览

所有微服务容器内监听 `8080`，主机端口映射如下：

| 服务 | 主机端口 | 限界上下文 | 主要职责 |
|---|---|---|---|
| user-auth-api | 5151 | BC1 | 账户、JWT/OAuth2、RBAC、地址 |
| product-api | 5152 | BC2 | SPU/SKU、分类品牌、商品搜索（ES） |
| cart-api | 5153 | BC3 | 购物车聚合、匿名/登录合并 |
| order-api | 5154 | BC4 | 订单状态机、库存预占（Redis Lua）、延迟取消 |
| promotion-api | 5155 | BC5 | 优惠券、秒杀、满减 |
| payment-api | 5156 | BC8 | 微信/支付宝适配、退款、对账 |
| points-api | 5157 | BC7 | 积分账户、会员等级、付费会员 |
| review-aftersales-api | 5158 | BC6 | 评价、售后单、退款请求 |
| seller-shop-api | 5159 | BC10 | 卖家入驻、店铺资质 |
| notification-api | 5160 | BC9 | 邮件/短信、模板渲染、重试死信 |
| system-admin-api | 5161 | BC11 | 数据看板、死信管理、索引重建、限流配置 |
| **api-gateway** | **8080** | - | 统一入口 |

### 微服务统一模式

所有 11 个微服务的 `Program.cs` 遵循一致的模式：

1. `AddLenoInfrastructure(...)` — 注册共享基础设施（JWT 生成器、Redis、ES 读模型仓储、文件存储、健康检查）
2. `Add{Xxx}Consumers(...)` — 注册 MassTransit 事件消费者
3. `AddInternalApiKeyAuth(...)` — 内部 API 鉴权（`X-Internal-Key` 头）
4. `Add{Xxx}Infrastructure(...)` — 注册 DbContext、IUnitOfWork、仓储
5. JWT Bearer 认证（Issuer=`Leno.UserAuth`, Audience=`Leno.Clients`）
6. 健康检查：`/health/live`（self/redis/ES）+ `/health/ready`（DbContext + redis + ES）
7. 中间件：`GlobalExceptionMiddleware` → `InternalApiKeyMiddleware` → `UseAuthentication` → `UseAuthorization` → `MapControllers`

### 内部服务调用

- 前缀 `internal/` 的路由受 `X-Internal-Key` 头保护，不走 JWT
- 开发环境默认 `InternalAuth:ApiKey = leno-internal-key-dev`
- 示例：`GET internal/products/skus/{skuId}` 供订单/购物车服务调用

### 数据库分库

每个限界上下文独立数据库：

| 服务 | 数据库 | DbContext |
|---|---|---|
| UserAuth | LenoUserAuth | UserDbContext |
| Product | LenoProduct | ProductDbContext |
| Cart | LenoCart | CartDbContext |
| Order | LenoOrder | OrderDbContext |
| Promotion | LenoPromotion | PromotionDbContext |
| Payment | LenoPayment | PaymentDbContext |
| PointsMembership | LenoPointsMembership | PointsMembershipDbContext |
| ReviewAfterSales | LenoReviewAfterSales | ReviewAfterSalesDbContext |
| SellerShop | LenoSellerShop | SellerShopDbContext |
| Notification | LenoNotification | NotificationDbContext |
| SystemAdmin | LenoSystemAdmin | SystemAdminDbContext |

所有 DbContext 继承 `BaseDbContext`（统一审计字段、软删除全局过滤器、`Version` rowversion 乐观锁）。

---

## 7. 基础设施组件

`docker-compose.yml` 定义的全部组件：

| 组件 | 镜像 | 主机端口 | 容器端口 | 账号/密码 | 用途 |
|---|---|---|---|---|---|
| sqlserver | mssql/server:2019-latest | 1433 | 1433 | sa / `Leno@SqlServer2019` | 写库（所有微服务分库） |
| redis | redis:7-alpine | 6379 | 6379 | - | 缓存/限流/库存预占/会话 |
| consul | hashicorp/consul:1.18 | 8500 | 8500 | - | 服务发现/配置中心 |
| rabbitmq | rabbitmq:3.12-management | 5672, 15672 | 5672, 15672 | leno / `Leno@RabbitMQ2026` | 事件总线 |
| elasticsearch | elasticsearch:8.13.0 | 9200 | 9200 | - | 读库/搜索 |
| jaeger | jaegertracing/all-in-one:1.55 | 4317, 4318, 16686 | 4317, 4318, 16686 | - | 链路追踪 |
| prometheus | prom/prometheus:v2.55.1 | 9090 | 9090 | - | 指标采集 |
| grafana | grafana/grafana:11.2.0 | 3000 | 3000 | leno / `Leno@Grafana2026` | 监控可视化 |

所有组件接入 `leno-net` 桥接网络，均配置 healthcheck。

### Consul 服务发现

网关通过 `ConsulDestinationResolver` 替换 YARP 默认解析器，每个请求动态查询 Consul 健康实例。集群目的地地址为占位符，靠 `Metadata.ConsulServiceName` 解析为真实实例。

> **注意**：当前版本中各业务微服务尚未接入 Consul 自注册（`AddConsulServiceRegistration` 扩展已实现但未启用），网关依赖 Consul 中的服务注册条目。如需启用，在各服务 `Program.cs` 中追加 `builder.Services.AddConsulServiceRegistration(...)` 并配置 `Consul` 节。

### RabbitMQ 拓扑

- Topic Exchange: `ecommerce.events`
- 业务队列: `q.{consumer}.{event}`
- 死信 Exchange: `ecommerce.events.dlx`
- 死信队列: `q.dlq.{consumer}`
- 延迟队列: DLX + TTL 实现（订单超时取消等）

---

## 8. 测试

### 8.1 测试框架

| 框架 | 版本 | 用途 |
|---|---|---|
| xUnit | 2.9.0 | 测试框架 |
| FluentAssertions | 7.0.0 | 断言 |
| Moq | 4.20.72 | Mock |
| Testcontainers | 4.0.0 | 集成测试容器 |
| Microsoft.AspNetCore.TestHost | - | WebApplicationFactory |

### 8.2 测试分类

- **单元测试**：默认类别，CI 默认运行
- **集成测试**：标注 `[Trait("Category", "Integration")]`，CI 单独运行

### 8.3 运行测试

```bash
# 运行全部单元测试（排除集成测试）
dotnet test Leno.slnx --filter "Category!=Integration"

# 仅运行集成测试
dotnet test Leno.slnx --filter "Category=Integration"

# 运行单个项目测试
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj

# 按测试名过滤
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "UserContextTransformProviderTests"

# 带覆盖率
dotnet test Leno.slnx --collect:"XPlat Code Coverage" --results-directory ./TestResults
reportgenerator -reports:./TestResults/**/coverage.cobertura.xml -targetdir:./CoverageReport -reporttypes:Html
```

### 8.4 集成测试容器

`Leno.Testing.ContainerFixture` 使用 Testcontainers 自动启动 4 个容器（共享实例）：

- MsSqlContainer（密码 `Leno@Test123!`，端口随机）
- RedisContainer（6379）
- RabbitMqContainer（guest:guest）
- ElasticsearchContainer（9200）

Consul/Jaeger 在集成测试中以 Moq mock 替代。网关集成测试使用 `WebApplicationFactory<Program>`，通过 `AddInMemoryCollection` 注入测试配置。

### 8.5 当前测试状态

- API 网关测试：**134 个通过**（含 Phase 1-6 单元测试 + 集成测试）
- 各微服务测试：分布在各自 `*.Tests` 项目

---

## 9. 可观测性

### 9.1 三支柱

| 支柱 | 技术 | 后端 | 访问地址 |
|---|---|---|---|
| 日志 | Serilog（结构化 JSON） | Console + File（`logs/gateway-.log` 按天滚动保留 7 天） | 文件 / stdout |
| 追踪 | OpenTelemetry | Jaeger | `http://localhost:16686` |
| 指标 | prometheus-net | Prometheus + Grafana | `http://localhost:9090`（Prometheus）/ `http://localhost:3000`（Grafana） |

> **注意**：当前仅 **API 网关** 启用了完整的 Serilog + OpenTelemetry + Prometheus。各业务微服务的可观测性扩展（`AddLenoOpenTelemetry`）已实现但尚未接入，未来启用时在各服务 `Program.cs` 调用即可。

### 9.2 网关指标

`/metrics` 端点暴露 6 个核心 Prometheus 指标：

| 指标 | 类型 | Labels |
|---|---|---|
| `gateway_requests_total` | Counter | route, method, status_code |
| `gateway_request_duration` | Histogram | route, method（buckets: 5ms-10s） |
| `gateway_active_requests` | Gauge | - |
| `gateway_circuit_breaker_state` | Gauge | cluster |
| `gateway_rate_limit_rejected` | Counter | route, policy |
| `gateway_blacklist_hits` | Counter | - |

### 9.3 Grafana 仪表盘

- 数据源：Prometheus（`http://prometheus:9090`，provisioning 自动配置）
- 仪表盘：`leno-gateway-dashboard.json`（10 面板，含请求 QPS、延迟分位、熔断状态、限流拒绝、活跃请求等）
- 访问：`http://localhost:3000`，账号 `leno` / `Leno@Grafana2026`

### 9.4 健康检查

- `/health/live` — 存活探针（进程级）
- `/health/ready` — 就绪探针（DB + Redis + ES + Consul 连通性）
- `/health-ui` — HealthChecksUI 仪表盘（10s 评估周期）

---

## 10. CI/CD

`.github/workflows/ci.yml` 定义了 5 个 job，触发条件为 push/PR 到 `main` 或 `develop`：

```
build-solution (restore + Release build + 单元测试 + 覆盖率报告)
       ↓
integration-tests (仅运行 Category=Integration)
build-services (matrix: 12 个项目并行 Release build)
       ↓
docker-build (matrix: 12 个 Dockerfile 并行 docker build)
       ↓
validate-compose (docker-compose config 校验)
```

- 单元测试：`--filter "Category!=Integration"`，收集 XPlat Code Coverage，生成 HTML 报告上传为 artifact
- 集成测试：`--filter "Category=Integration"`，依赖 build-solution 完成
- 覆盖率报告：使用 `dotnet-reportgenerator-globaltool`

---

## 11. 编码规范

详见 `docs/编码规范.md` 与 `.editorconfig`。要点：

### 11.1 命名

| 类型 | 规范 | 示例 |
|---|---|---|
| 类/接口/方法/公共属性/枚举/事件 | PascalCase | `OrderAppService` |
| 接口 | `I` 前缀 | `IOrderRepository` |
| 私有字段 | `_camelCase` | `_logger` |
| 局部变量 | camelCase | `orderId` |

### 11.2 分层依赖

```
Api → Application → Domain ← Infrastructure
                  ↓
              SharedKernel
```

- Domain 层不引用任何外层（不引用 Infrastructure/Application/Api）
- Infrastructure 实现 Domain 层定义的接口（依赖倒置）

### 11.3 构建属性

- `TreatWarningsAsErrors=true` — 警告即错误
- `AnalysisLevel=latest` / `AnalysisMode=Recommended`
- `EnforceCodeStyleInBuild=true`
- `Nullable=enable` / `ImplicitUsings=enable`
- 抑制的警告：CS1591（XML 注释）、CA1707（下划线）、CA1711（命名）、CA1051（可见实例构造函数）、CA1863（字符串比较）、CA1305（区域敏感）、CA1822（静态成员）

### 11.4 提交信息

建议遵循 Conventional Commits：

```
feat(gateway): 添加 Prometheus 6 核心指标服务
fix(order): 修复库存预占竞态
docs(spec): 新增 Leno 电商平台全面优化方案设计
chore(ci): 调整 .NET SDK 版本
```

---

## 12. 常见问题

### Q1: `docker compose up` 后网关返回 503

**原因**：后端微服务未注册到 Consul，网关找不到健康实例。

**解决**：
1. 确认所有微服务容器健康：`docker compose ps`
2. 查看某服务日志：`docker compose logs product-api`
3. 当前版本微服务尚未自动注册 Consul，可临时直连后端服务端口（如 `http://localhost:5152/api/products/...`）验证
4. 启用 Consul 自注册：在各服务 `Program.cs` 追加 `builder.Services.AddConsulServiceRegistration(...)` 并配置 `Consul` 节

### Q2: 编译失败提示 CAxxxx 警告

**原因**：`TreatWarningsAsErrors=true`，分析器警告会导致编译失败。

**解决**：
- CA1310：字符串比较使用 `StringComparison.Ordinal`
- CA1859：使用具体类型而非接口返回
- CA1861：避免在参数中使用 `new[]` 数组字面量（加 `#pragma warning disable CA1861`）
- 其他：参考 .NET 分析器文档修复

### Q3: 测试因 Redis 连接失败返回 500

**原因**：集成测试启用了 `CacheMiddleware` 但未 mock Redis。

**解决**：在测试配置覆盖中禁用缓存：
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Gateway:Cache:Enabled"] = "false"
});
```

### Q4: SQL Server 连接失败

**原因**：开发与 Docker 的密码不同。

| 环境 | 连接字符串密码 |
|---|---|
| 开发（appsettings.json） | `Leno@2026`（Product 服务） |
| Docker（appsettings.Docker.json） | `Leno@SqlServer2019` |

**解决**：确保 `ASPNETCORE_ENVIRONMENT` 正确设置。Docker 运行时设为 `Docker` 加载 `appsettings.Docker.json`。

### Q5: 如何添加新的微服务

1. 在 `src/Services/{NewService}/` 下创建四个项目：`{X}.Domain`、`{X}.Application`、`{X}.Infrastructure`、`{X}.Api`
2. 在 `Leno.slnx` 中注册项目
3. `{X}.Api` 创建 `Dockerfile`（参考现有服务，多阶段构建 `sdk:10.0` → `aspnet:10.0`）
4. 在 `docker-compose.yml` 添加服务定义，端口分配从 5162 开始
5. 在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 的 `ReverseProxy.Routes` 和 `Clusters` 添加新集群
6. 在 Consul 中注册服务名 `leno-{new-service}-api`
7. 参考现有服务实现 `Program.cs`（统一模式：`AddLenoInfrastructure` → `Add{X}Consumers` → JWT → 健康检查）

### Q6: 如何切换数据库为 PostgreSQL

`docs/技术选型方案.md` 支持 SQL Server 或 PostgreSQL 双选。切换步骤：

1. 各服务 Infrastructure 项目替换 EF Core Provider 包：`Microsoft.EntityFrameworkCore.SqlServer` → `Npgsql.EntityFrameworkCore.PostgreSQL`
2. `appsettings.json` 的 `ConnectionStrings` 改为 PostgreSQL 格式
3. `Program.cs` 中 `UseSqlServer` 改为 `UseNpgsql`
4. docker-compose 替换 SQL Server 为 PostgreSQL 镜像

---

## 附录：关键文档索引

| 文档 | 路径 |
|---|---|
| 技术选型方案 | `docs/技术选型方案.md` |
| 编码规范 | `docs/编码规范.md` |
| 需求文档总览（DDD 架构） | `docs/spec/00-需求文档总览与DDD架构.md` |
| 各限界上下文需求（01-12） | `docs/spec/01-用户与认证授权域.md` 等 |
| API 网关增强设计 | `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md` |
| API 网关分阶段实施计划 | `docs/superpowers/plans/2026-07-14-api-gateway-phase{1-6}-*.md` |
| 任务进度跟踪 | `docs/tasks/progress.md` |
| Setup Guide（设置指南） | `docs/todo/setup-guide.md` |

---

> **反馈与贡献**：请遵循 `docs/编码规范.md` 与 Conventional Commits 提交规范。提交前运行 `./scripts/check-placeholders.sh` 确保无占位实现。
