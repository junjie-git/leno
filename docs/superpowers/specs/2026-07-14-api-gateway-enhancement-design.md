# API 网关增强设计

> **⚠️ SUPERSEDED**: 本 spec 已被 [2026-07-17-comprehensive-optimization-v2-design.md](./2026-07-17-comprehensive-optimization-v2-design.md) §10 接管。
>
> 本文档保留作为历史参考，不再代表当前架构决策。当前实施请以 V2 spec 为准。
>
> **接管关系**：
> - 网关增强 spec 的缓存、限流、熔断、JWT 鉴权等内容已整合到 V2 spec §10 网关增强
> - 既有实现保留，新增功能按 V2 spec 实施
>
> **接管日期**: 2026-07-17

> 日期: 2026-07-14
> 状态: Draft
> 范围: 独立新方案（不依赖已有全面优化方案主线 7）

## 1. 背景与目标

### 1.1 现状

Leno 电商平台已有基于 YARP 2.2.0 的 API 网关 (`src/ApiGateway/Leno.ApiGateway/`)，配置了 44 条路由覆盖 11 个微服务。当前网关能力较基础：

- 仅做路由转发 + 手工健康聚合（轮询后端 `/health/ready`）+ HealthChecksUI 仪表盘
- 认证为 JWT 透传模式（网关不验签，各后端服务自行校验 JWT）
- 服务发现为静态配置（YARP `appsettings.json` + Docker DNS）
- 未启用 YARP 原生限流、熔断、超时等能力

### 1.2 目标

将网关从基础路由转发升级为具备安全守门、流量治理、可观测性和高级特性的完整 API 网关：

- **安全集中化**：网关本地验签 + 黑名单校验，后端服务信任网关注入的用户上下文 Header
- **服务动态化**：通过 Consul 服务注册实现动态路由，自动感知实例上下线
- **流量可控化**：多维度限流、熔断降级、超时重试，保障高并发下的系统稳定性
- **全链可观测**：分布式追踪、结构化访问日志、Prometheus 指标暴露
- **高级能力**：请求/响应转换、响应缓存、统一 CORS、协议转换预留接口

### 1.3 不在本次范围内

- SSL/TLS 终结（由前端负载均衡器或 Nginx 处理）
- WAF 防御（SQL 注入/XSS/CSRF 拦截，后续迭代）
- 协议转换具体实现（仅预留接口，待 gRPC 迁移后启用）

### 1.4 技术选型

- **网关框架**：YARP 2.2.0（已有，扩展使用）
- **服务发现**：Consul（已有 KV 配置中心，增加服务注册功能）
- **黑名单同步**：Redis Pub/Sub + Caffeine 本地缓存
- **分布式追踪**：OpenTelemetry
- **日志**：Serilog 结构化日志
- **指标**：prometheus-net
- **部署**：Docker Compose（设计考虑 K8s 可迁移性）

## 2. 架构总览

### 2.1 中间件管道顺序

请求进入网关后，按以下顺序依次处理：

```
HTTP Request
  |
  +-- 1. IP黑白名单过滤 (IpFilterMiddleware)     -- 最早拦截，拒绝恶意IP
  +-- 2. CORS 中间件                              -- 处理预检 OPTIONS 请求
  +-- 3. 全局异常处理 (GlobalExceptionMiddleware)  -- 已有，复用
  +-- 4. 访问日志记录 (AccessLoggingMiddleware)    -- 记录请求元数据
  +-- 5. 分布式追踪 (TracingMiddleware)            -- 生成/透传 TraceId
  +-- 6. JWT本地验签 (JwtAuthMiddleware)           -- 校验签名+过期时间
  +-- 7. 黑名单校验 (TokenBlacklistMiddleware)     -- 校验JTI是否在黑名单
  |
  +-- YARP Proxy Pipeline:
  |    +-- 请求头注入 Transform (X-User-Id等)     -- 验签通过后注入用户上下文
  |    +-- 灰度路由 Transform (TrafficTagging)     -- 基于Header/Cookie路由
  |    +-- RateLimiterPolicy (YARP原生)            -- 多维度限流
  |    +-- CircuitBreaker (YARP原生)               -- 熔断降级
  |    +-- Timeout (YARP原生)                      -- 超时控制
  |    +-- LoadBalancing (YARP原生)                -- 负载均衡策略
  |    +-- 缓存 Transform (可选)                   -- GET幂等响应缓存
  |
  +-- Backend Microservice
```

### 2.2 项目结构

在现有 `src/ApiGateway/Leno.ApiGateway/` 项目内扩展，不新建项目：

```
src/ApiGateway/Leno.ApiGateway/
|-- Program.cs                          -- 增强：注册所有中间件和 Consul 集成
|-- appsettings.json                    -- 增强：YARP 限流/熔断/超时配置
|-- Middleware/
|   |-- IpFilterMiddleware.cs           -- IP 黑白名单
|   |-- AccessLoggingMiddleware.cs       -- 统一访问日志
|   |-- JwtAuthMiddleware.cs            -- JWT 本地验签
|   `-- TokenBlacklistMiddleware.cs     -- 黑名单 JTI 校验
|-- Transforms/
|   |-- UserContextTransform.cs          -- 注入 X-User-Id/X-Role/X-Shop-Id
|   |-- CanaryReleaseTransform.cs        -- 灰度流量染色
|   `-- IProtocolTranslator.cs           -- 协议转换预留接口
|-- Services/
|   |-- ConsulServiceDiscovery.cs        -- IClusterChangeListener 动态更新
|   |-- TokenBlacklistSyncService.cs     -- Redis Pub/Sub + 定时拉取 + 启动预热
|   `-- GatewayMetricsService.cs         -- Prometheus 指标暴露
|-- Options/
|   |-- GatewayOptions.cs
|   |-- BlacklistOptions.cs
|   `-- CanaryOptions.cs
`-- Extensions/
    `-- ServiceCollectionExtensions.cs   -- 网关服务注册扩展
```

### 2.3 设计原则

1. **中间件分层清晰**：安全过滤 -> 日志追踪 -> 认证鉴权 -> YARP 代理，每层只做一件事
2. **YARP 原生优先**：限流、熔断、超时、负载均衡用 YARP 内建能力，不自造轮子
3. **自定义中间件补位**：IP 过滤、JWT 验签、黑名单、灰度路由用自定义中间件/Transform
4. **后端服务减负**：网关集中验签后注入 `X-User-Id` 等头，后端服务从 Header 读取用户上下文，不再各自验签

## 3. 核心基础功能

### 3.1 动态路由 (Consul 服务发现集成)

**现状问题**：YARP `appsettings.json` 硬编码 11 个 Cluster 的目标地址，实例上下线需手动修改配置。

**改造方案**：

- `ConsulServiceDiscovery` 实现 YARP 的 `IClusterChangeListener`，订阅 Consul Health API
- 每个 Leno 微服务启动时向 Consul 注册自身实例（IP + Port + Health Check URL）
- YARP Cluster 配置从静态 Address 改为 `ConsulServiceName`，由 `ConsulServiceDiscovery` 动态解析 Destination 列表
- 服务实例上下线时 Consul 推送变更，网关自动更新 Destination 无需重启

**配置变更**：

```json
// 改造前 (appsettings.json)
"product": {
  "Destinations": {
    "d1": { "Address": "http://localhost:5150/" }
  }
}

// 改造后
"product": {
  "LoadBalancingPolicy": "PowerOfTwoChoices",
  "ConsulServiceName": "leno-product-api",
  "HealthCheck": {
    "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" }
  }
}
```

**微服务侧改造**：在各服务的 `Program.cs` 中添加 `AddConsulServiceRegistration()`，启动时注册到 Consul，关闭时注销。

### 3.2 负载均衡

利用 YARP 内建的负载均衡策略，通过配置切换：

| 策略 | YARP 配置值 | 适用场景 |
|------|------------|---------|
| 轮询 | `RoundRobin` | 均匀分配 |
| 最少连接 | `LeastRequests` | 长连接场景 |
| 随机 | `Random` | 简单快速 |
| PowerOfTwo | `PowerOfTwoChoices` | **推荐默认**，选择当前请求数最少的两个实例中较小者 |

### 3.3 灰度发布与流量染色

`CanaryReleaseTransform` 实现 YARP 自定义 Transform，基于请求特征路由到不同版本实例：

**染色维度**：
- **Header 染色**：`X-Canary-Version: v2` -> 路由到 v2 实例
- **Cookie 染色**：`canary=true` -> 灰度用户
- **IP 染色**：配置灰度 IP 列表 -> 特定用户灰度

**Consul 集成**：灰度实例向 Consul 注册时携带 `version` tag（如 `v2`），`CanaryReleaseTransform` 读取请求染色标记后，过滤 Consul 返回的实例列表只路由到匹配 tag 的实例。

**配置示例**：

```json
"canary-routes": {
  "ClusterId": "product",
  "Transforms": [
    { "CanaryVersion": "v2", "MatchHeader": "X-Canary-Version" }
  ]
}
```

## 4. 安全与认证

### 4.1 JWT 本地验签 (JwtAuthMiddleware)

网关从"JWT 透传"转变为"集中验签"，在本地完成全部 JWT 校验：

**校验流程**：
1. 从 `Authorization` 头提取 Bearer Token
2. 使用配置的 `Jwt:SecretKey`（HS256）本地验签，无需远程调用认证服务
3. 校验 `exp`（过期时间），过期返回 401
4. 校验通过后，从 Claims 提取 `Sub`(UserId)、`Role`、`shop_id` 等
5. 白名单路由（如 `/api/auth/login`、`/health`、`/metrics`）跳过验签

**验签配置**：

```json
"Jwt": {
  "Issuer": "Leno.UserAuth",
  "Audience": "Leno.ApiGateway",
  "SecretKey": "<from-consul>",
  "ClockSkewSeconds": 30
}
```

**TokenBlacklistMiddleware 紧随其后**：
1. 从 JWT Claims 提取 `jti`（JWT ID）
2. 查询本地 Caffeine 缓存（内存黑名单）
3. 命中则返回 401 + "Token已撤销"

### 4.2 动态黑名单更新机制 (TokenBlacklistSyncService)

三层保障，确保黑名单毫秒级生效且不丢失：

| 机制 | 触发方式 | 延迟 | 作用 |
|------|---------|------|------|
| **Redis Pub/Sub 实时推送** | 用户注销/改密时 UserAuth 服务发布 `TokenRevoked` 事件 | 毫秒级 | 主力机制，实时同步 |
| **定时兜底拉取** | 后台 `HostedService` 每 5 分钟从 Redis 全量拉取黑名单 | 5 分钟 | 防消息丢失 |
| **启动预热** | 网关启动时先从 Redis 全量拉取黑名单，完成前拒绝流量 | 启动时 | 新实例无安全窗口 |

**数据结构**：
- Redis Key: `leno:token:blacklist`（Set 结构，存储被撤销的 JTI）
- Caffeine 缓存: 本地 LRU 缓存，TTL 与 Token 最大有效期一致（120 分钟），避免内存无限增长

**TokenRevoked 事件格式**：

```json
{
  "eventType": "TokenRevoked",
  "jti": "abc123-def456",
  "userId": 12345,
  "reason": "logout",
  "timestamp": "2026-07-14T10:30:00Z"
}
```

### 4.3 IP 黑白名单 (IpFilterMiddleware)

管道最前置的过滤层：

- **白名单**：配置可信 IP（如运维网段、内部服务 IP），白名单内直接放行
- **黑名单**：恶意 IP 自动加入黑名单（如限流触发后自动封禁 N 分钟）
- **配置来源**：Consul KV（`leno/gateway/ip-filter`），支持热更新
- **匹配方式**：支持单个 IP 和 CIDR 网段（如 `192.168.1.0/24`）

**配置示例**：

```json
"IpFilter": {
  "Whitelist": ["10.0.0.0/8", "172.16.0.0/12"],
  "Blacklist": ["203.0.113.50"],
  "AutoBan": {
    "Enabled": true,
    "Threshold": 100,
    "WindowSeconds": 60,
    "BanDurationMinutes": 30
  }
}
```

### 4.4 后端服务适配改造

网关集中鉴权后，后端服务需相应调整：

**改造前（各服务自行验签）**：

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* 验签配置 */ });
```

**改造后（信任网关头）**：

```csharp
builder.Services.AddAuthentication("GatewayHeader")
    .AddScheme<GatewayAuthOptions, GatewayAuthHandler>("GatewayHeader", options => { });

// CurrentUserContext 从 X-User-Id/X-Role/X-Shop-Id 头读取
// 替代从 JWT Claims 解析
```

**安全边界保障**：
- 后端服务仅监听容器内网（不对外暴露端口），确保请求必须经过网关
- `X-User-Id` 等头由网关注入，后端服务可配置 `TrustedProxy` 校验请求来源
- 内部服务间调用保持现有 `X-Internal-Key` 机制不变

### 4.5 改造影响范围

需改造的 11 个微服务 `Program.cs`：

| 服务 | 改造内容 |
|------|---------|
| UserAuth.Api | 移除 JwtBearer 验签，改为 GatewayHeader 认证 |
| Product.Api | 同上 |
| Cart.Api | 同上 |
| Order.Api | 同上 |
| Promotion.Api | 同上 |
| Payment.Api | 同上 |
| PointsMembership.Api | 同上 |
| ReviewAfterSales.Api | 同上 |
| SellerShop.Api | 同上 |
| Notification.Api | 同上 |
| SystemAdmin.Api | 同上 |

同时需改造 `CurrentUserContext.cs`，从 Header 而非 JWT Claims 提取用户上下文。

## 5. 流量治理与高可用

### 5.1 限流 (Rate Limiting)

利用 YARP 2.x 内建的 `RateLimiterPolicy`，通过 ASP.NET Core `System.Threading.RateLimiting` 实现多维度限流：

**三层限流策略**：

| 维度 | 算法 | 配置示例 | 适用场景 |
|------|------|---------|---------|
| **全局** | 令牌桶 | 5000 req/s | 保护网关整体容量 |
| **按路由** | 滑动窗口 | 秒杀接口 50 req/s，普通接口 200 req/s | 防止单接口过载 |
| **按用户** | 滑动窗口 | 100 req/min per UserId | 防止单用户刷接口 |

**配置方式**：在 YARP Cluster/Route 配置中指定 `RateLimiterPolicy` 名称：

```json
"routes": {
  "seckill-route": {
    "ClusterId": "promotion",
    "RateLimiterPolicy": "seckill-policy",
    "Match": { "Path": "/api/promotion/seckill/{**catch-all}" }
  }
}
```

**Redis 分布式限流**：多网关实例部署时，限流计数器存入 Redis，保证全局限流准确。单实例时降级为本地内存计数。

**限流策略注册**：

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("seckill-policy", context => 
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.FindFirst("Sub")?.Value,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromSeconds(1),
                SegmentsPerWindow = 4
            }));
});
```

### 5.2 熔断与降级 (Circuit Breaker & Fallback)

利用 YARP 内建的 `CircuitBreaker` 策略：

**熔断规则**：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `MaxConcurrentRequests` | 100 | 最大并发请求数 |
| `FailureRateThreshold` | 0.5 | 失败率达到 50% 开启熔断 |
| `SamplingDuration` | 30s | 采样窗口 |
| `MinimumThroughput` | 10 | 采样窗口内最少请求数 |
| `BreakDuration` | 30s | 熔断持续时间 |

**降级响应 (Fallback)**：熔断开启时返回预设降级响应，而非 502：

```json
{
  "code": 503,
  "message": "服务暂时不可用，请稍后重试",
  "data": null
}
```

### 5.3 超时与重试 (Timeouts & Retries)

**超时配置（按路由差异化）**：

| 路由类型 | 连接超时 | 读取超时 | 说明 |
|---------|---------|---------|------|
| 默认 | 5s | 30s | 常规 API |
| 秒杀 | 2s | 5s | 高时效性场景 |
| 文件上传 | 10s | 120s | 大文件传输 |
| 内部调用 | 3s | 15s | 服务间通信 |

**重试策略**：
- 仅对**幂等**方法（GET、PUT、DELETE）重试，POST 不重试
- 重试条件：连接超时、503（服务不可用）
- 最大重试次数：2 次
- 重试间隔：指数退避（500ms -> 1000ms）

## 6. 可观测性

### 6.1 全链路追踪 (Distributed Tracing)

采用 OpenTelemetry 标准，生成/透传 TraceId 和 SpanId：

**实现方式**：
- 网关作为入口点生成 Root Span，注入 `traceparent` 和 `tracestate` Header
- YARP 的 `RequestTransform` 将 TraceId 透传到后端服务
- 后端服务通过 OpenTelemetry SDK 自动提取 `traceparent` Header，延续调用链

**Header 标准**：

```
traceparent: 00-{trace-id}-{span-id}-{flags}
```

**集成方式**：
- 使用 `OpenTelemetry.Extensions.Hosting` NuGet 包
- Exporter 可配置为 OTLP（对接 Jaeger/Tempo）或 Zipkin
- 网关自动为每个请求创建 Span，记录：路由名称、目标服务、HTTP 状态码、耗时

**NuGet 依赖**：
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`（或 `Zipkin`）

### 6.2 统一访问日志 (AccessLoggingMiddleware)

结构化 JSON 日志，记录每次请求的完整元数据：

**日志字段**：

| 字段 | 示例 | 说明 |
|------|------|------|
| `timestamp` | 2026-07-14T10:30:00Z | 请求时间 |
| `traceId` | a1b2c3d4... | 关联追踪 ID |
| `method` | POST | HTTP 方法 |
| `path` | /api/order/create | 请求路径 |
| `statusCode` | 200 | 响应状态码 |
| `duration` | 125ms | 请求耗时 |
| `clientIp` | 192.168.1.100 | 客户端 IP |
| `userId` | 12345 | 用户 ID（验签后填充） |
| `targetService` | order-api | 目标微服务 |
| `userAgent` | Mozilla/... | 客户端标识 |

**输出方式**：Serilog 结构化日志，输出到 Console（容器环境 stdout）+ 文件（持久化），支持对接 ELK/Loki。

**NuGet 依赖**：
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`

### 6.3 实时监控指标 (Metrics)

使用 `prometheus-net` 暴露 Prometheus 格式指标：

**暴露端点**：`/metrics`（仅内网访问）

**核心指标**：

| 指标名 | 类型 | 标签 | 说明 |
|--------|------|------|------|
| `gateway_requests_total` | Counter | route, method, status_code | 请求总数 |
| `gateway_request_duration` | Histogram | route, method | 请求耗时分布(P50/P99) |
| `gateway_active_requests` | Gauge | - | 当前活跃请求数 |
| `gateway_circuit_breaker_state` | Gauge | cluster | 熔断器状态(0=closed,1=open) |
| `gateway_rate_limit_rejected` | Counter | route, policy | 限流拒绝数 |
| `gateway_blacklist_hits` | Counter | - | 黑名单命中数 |

**Grafana 仪表盘**：提供预置的 Grafana Dashboard JSON 模板，展示 QPS、成功率、P99 延迟、熔断状态等黄金指标。

**NuGet 依赖**：
- `prometheus-net.AspNetCore`

## 7. 高级特性

### 7.1 请求/响应转换 (Request/Response Transformation)

利用 YARP Transforms 机制，在路由配置中声明式定义转换规则：

**请求转换**：
- **用户上下文注入**：`UserContextTransform` 将验签后的用户信息注入下游请求头
  - `X-User-Id`: 用户 ID
  - `X-Role`: 用户角色
  - `X-Shop-Id`: 店铺 ID（卖家场景）
  - `X-Internal-Call`: 标记请求来源（网关注入，后端可校验）
- **Header 增删改**：通过 YARP `RequestHeader` Transform 配置
- **路径重写**：支持路径前缀剥离和重写

**响应转换**：
- **统一响应包装**：后端返回的裸数据可由网关统一包装为 `{code, message, data}` 格式
- **Header 清理**：移除内部 Header（如 `X-Internal-Call`）后再返回客户端
- **响应压缩**：对大响应体自动 gzip 压缩

**配置示例**：

```json
"routes": {
  "order-route": {
    "Transforms": [
      { "RequestHeader": "X-User-Id", "Set": "{UserId}" },
      { "RequestHeader": "X-Role", "Set": "{Role}" },
      { "PathRemovePrefix": "/api/order" },
      { "ResponseHeader": "X-Internal-Call", "Remove": "true" }
    ]
  }
}
```

### 7.2 缓存策略 (Caching)

对 GET 等幂等请求的响应进行缓存：

**实现方式**：
- 自定义 YARP `ResponseTransform`，检查请求是否满足缓存条件
- 命中缓存时直接返回，不转发到后端
- 缓存存储：Redis（分布式缓存，多网关实例共享）

**缓存规则**：

| 规则 | 配置 | 说明 |
|------|------|------|
| 可缓存方法 | GET, HEAD | 仅幂等方法 |
| 缓存时长 | 路由级别配置（默认 60s） | 如商品详情 300s，列表 60s |
| 缓存 Key | `method:path:querystring:userId` | 按用户隔离 |
| 不可缓存响应 | 状态码非 200、含 `Cache-Control: no-store` | 仅缓存成功响应 |
| 主动失效 | 后端通过 Redis Pub/Sub 发布缓存失效事件 | 如商品更新后通知网关清除该商品缓存 |

**缓存失效事件格式**：

```json
{
  "eventType": "CacheInvalidated",
  "cacheKey": "GET:/api/product/sku/123::",
  "pattern": "/api/product/sku/123*"
}
```

### 7.3 跨域支持 (CORS)

在网关层统一配置 CORS，后端服务不再单独配置：

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("https://leno.example.com", "https://admin.leno.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});
```

- 允许的 Origin 列表从 Consul KV 读取，支持热更新
- 预检 OPTIONS 请求在 CORS 中间件直接返回，不进入后续管道

### 7.4 协议转换预留接口

定义 `IProtocolTranslator` 抽象接口，当前不实现具体转换逻辑，待 gRPC 迁移后填充：

```csharp
public interface IProtocolTranslator
{
    string SourceProtocol { get; }   // 如 "HTTP"
    string TargetProtocol { get; }   // 如 "gRPC"

    Task<HttpRequestMessage> TranslateRequestAsync(HttpContext context);
    Task TranslateResponseAsync(HttpContext context, HttpResponseMessage response);
}
```

在 YARP 管道中预留注入点，当后端服务提供 gRPC 端点后，注册对应 `IProtocolTranslator` 实现即可启用。

## 8. 健康检查改进

### 8.1 移除手工健康轮询

现有 `Program.cs` 中的手工 `/health` 端点轮询所有后端 `/health/ready`，改为：

- 利用 YARP 配合 Consul 的主动健康检查（`HealthCheck.Active`）
- YARP 自动排除不健康实例，不再转发流量
- 网关自身 `/health/live` 和 `/health/ready` 保留
- HealthChecksUI 仪表盘保留，数据源改为各服务的 Consul 健康状态

### 8.2 网关自身健康检查

- `/health/live`：网关进程存活检查（已有）
- `/health/ready`：检查 Consul 连通性 + Redis 连通性 + 关键中间件初始化状态
- 启动预热完成前 `/health/ready` 返回 503，避免过早接收流量

## 9. 配置管理

### 9.1 配置来源

| 配置项 | 来源 | 热更新 | 说明 |
|--------|------|--------|------|
| YARP 路由/Cluster | `appsettings.json` | 重启生效 | 路由规则较少变更 |
| JWT SecretKey | Consul KV | 实时 | 敏感配置 |
| IP 黑白名单 | Consul KV | 实时 | 运维频繁调整 |
| CORS Origins | Consul KV | 实时 | 前端域名变更 |
| 限流策略阈值 | Consul KV | 实时 | 运营活动期间调整 |
| 熔断/超时参数 | `appsettings.json` | 重启生效 | 相对稳定 |
| 黑名单数据 | Redis | 实时 | 动态同步 |

### 9.2 配置节结构

```json
{
  "Gateway": {
    "IpFilter": { "Whitelist": [], "Blacklist": [], "AutoBan": {} },
    "Canary": { "Enabled": true, "DefaultVersion": "v1" },
    "Cache": { "Enabled": true, "DefaultTtl": "00:01:00" },
    "Cors": { "Origins": [] }
  },
  "Jwt": { "Issuer": "", "Audience": "", "SecretKey": "", "ClockSkewSeconds": 30 },
  "Blacklist": { "SyncInterval": "00:05:00", "CacheTtl": "02:00:00" },
  "OpenTelemetry": { "Exporter": "otlp", "Endpoint": "http://jaeger:4317" },
  "Metrics": { "Enabled": true, "Path": "/metrics" }
}
```

## 10. 测试策略

### 10.1 单元测试

| 组件 | 测试要点 |
|------|---------|
| `JwtAuthMiddleware` | 有效/无效/过期 Token 校验，白名单路由跳过 |
| `TokenBlacklistMiddleware` | JTI 命中/未命中黑名单 |
| `TokenBlacklistSyncService` | Pub/Sub 接收、定时拉取、启动预热 |
| `IpFilterMiddleware` | IP 匹配、CIDR 匹配、白名单优先 |
| `CanaryReleaseTransform` | Header/Cookie/IP 染色路由 |
| `ConsulServiceDiscovery` | 实例上下线、健康状态变更 |

### 10.2 集成测试

- 网关 + 后端服务端到端请求链路
- 限流触发后 429 响应
- 熔断开启后 503 降级响应
- 黑名单推送后请求被拒
- Consul 实例下线后流量自动转移

### 10.3 测试项目

新建 `Leno.ApiGateway.Tests` 测试项目，引用 `Leno.ApiGateway`，使用 xUnit + FluentAssertions。

## 11. 实施顺序建议

按依赖关系分阶段实施：

1. **阶段一：基础设施** - Consul 服务注册 + YARP 动态路由 + 负载均衡
2. **阶段二：安全认证** - JWT 本地验签 + 黑名单同步 + IP 过滤
3. **阶段三：后端适配** - 11 个微服务改为 GatewayHeader 认证
4. **阶段四：流量治理** - 限流 + 熔断 + 超时重试
5. **阶段五：可观测性** - 追踪 + 日志 + 指标
6. **阶段六：高级特性** - 请求转换 + 缓存 + CORS + 协议预留接口

## 12. NuGet 依赖清单

| 包名 | 版本 | 用途 |
|------|------|------|
| `Yarp.ReverseProxy` | 2.2.0 | 已有，网关核心 |
| `Consul` | 最新稳定 | 服务注册与发现 |
| `Caffeine` | 最新稳定 | 本地黑名单缓存 |
| `StackExchange.Redis` | 已有(Infrastructure) | 黑名单数据存储 + Pub/Sub |
| `OpenTelemetry.Extensions.Hosting` | 最新稳定 | 分布式追踪 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 最新稳定 | ASP.NET Core 追踪 |
| `OpenTelemetry.Instrumentation.Http` | 最新稳定 | HTTP 追踪 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 最新稳定 | OTLP 导出 |
| `Serilog.AspNetCore` | 最新稳定 | 结构化日志 |
| `Serilog.Sinks.Console` | 最新稳定 | 控制台日志输出 |
| `prometheus-net.AspNetCore` | 最新稳定 | Prometheus 指标 |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 已有 | JWT 验签（网关侧使用） |
