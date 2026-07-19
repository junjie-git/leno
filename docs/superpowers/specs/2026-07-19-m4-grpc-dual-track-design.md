# M4 gRPC 双轨与自动降级方案设计

**版本:** v1.0
**日期:** 2026-07-19
**作者:** Leno 团队
**关联 Plan:** [Plan 8 Task 8-12](../plans/2026-07-17-slow-track-m4-communication-upgrade.md)
**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §11](./2026-07-17-comprehensive-optimization-v2-design.md)
**状态:** Draft — 待用户审阅

---

## 1. 背景与目标

### 1.1 背景

Plan 8（M4 通信升级）已完成 Task 1-7，包括 11 个 `.proto` 契约文件、`buf.yaml` / `buf.gen.yaml` 工具链、`GrpcAntiCorruptionClientBase` 抽象基类、`AntiCorruptionOptions.UseGrpc` 配置开关、9 个 HTTP 防腐层服务继承 `AntiCorruptionBase`、Polly 策略链、11 条 internal 路由 `/v1/` 前缀双路由期、`IntegrationEventBase.SchemaVersion` 字段持久化。

Plan 8 Task 8-12（gRPC 服务端实现、客户端迁移、HttpClient 下线）尚未实施。原 spec §11.3 设计为"配置开关灰度切换 + 单 BC 独立回滚"，**不是运行时自动降级**。本设计补充运行时自动降级能力，形成"混合策略"：配置层灰度切换 + 熔断器自动降级。

### 1.2 目标

在不删除既有 HttpClient 防腐层代码的前提下，为 10 个防腐层服务新增 gRPC 双轨能力：

1. 通过 `AntiCorruption:UseGrpc` 配置开关切换主路径（Consul KV 热更新，单 BC 独立控制）
2. gRPC 连续失败 3 次时自动熔断，30 秒后半开放探测，连续 2 次成功恢复
3. 熔断打开期间自动降级到 HttpClient，业务无感知
4. 业务异常（NotFound/PermissionDenied 等）不触发降级，直接抛出

### 1.3 范围

**纳入范围：**
- 9 个可迁移防腐层（Order 3 + Notification 1 + Cart 2 + ReviewAfterSales 3；Logistics 不迁移）
- 9 个被调用方 BC.Api 的 gRPC 服务端
- `AntiCorruptionDispatcher<TService>` 双轨调度器与 `CircuitBreakerState` 三状态机
- 4 个 `.proto` 文件扩展（order/payment/user/product）
- `buf generate` CI 集成与首次生成
- `GrpcEndpoints` Consul KV 配置注入与 `ConsulConfigWatcher` 热更新
- gRPC 鉴权拦截器（metadata header `x-internal-key`）

**不纳入范围：**
- BFF 聚合层（继续使用 HttpClient 调用各 BC 对外 HTTP API，语义不同）
- Plan 8 Task 11 下线 HttpClient 代码（永久保留双轨，作为降级备份）
- 第三方物流 API（`LogisticsTrackingService` 保留 HttpClient，无对应 .proto）
- mTLS 双向证书认证（本次使用 metadata header，mTLS 作为后续优化方向）

### 1.4 关键设计决策

| 决策项 | 选择 | 理由 |
|---|---|---|
| 降级策略 | 混合策略（配置切换 + 熔断自动降级） | 兼顾可控性与故障自愈 |
| proto 契约 | 扩展 .proto 补齐缺失字段 | 向后兼容，`buf breaking` 校验通过 |
| gRPC 鉴权 | metadata header `x-internal-key` | 与 HttpClient 模式语义一致 |
| Cart 第 10 防腐层 | 纳入迁移 | 与 CartPriceService 同属 Cart→Product 调用，统一迁移避免实现不一致 |
| buf generate | CI 生成并提交 `Generated/` 目录 | 开发者无需安装 buf CLI |
| BFF | 不纳入 | 语义不同（前端聚合 vs BC 间调用） |
| 迁移批次 | POC 先行 + 批量迁移 | 风险分散，先验证混合策略 |
| Kestrel 端口 | 同端口复用 HTTP/1.1 + HTTP/2 | 配置简单，不增端口管理负担 |
| GrpcEndpoints | Consul KV `leno/grpc/endpoints/{bc}` | 与 InternalApiKey 收敛策略一致 |
| Task 11 | 不实施 | 保留 HttpClient 作为降级备份 |
| 实施方案 | 方案 B 双轨调度器 | 职责分离，可独立测试 |

### 1.5 不变性约束

1. **业务层零感知**：业务层注入 `IXxxAntiCorruptionService`，不感知底层是 gRPC 还是 HttpClient
2. **错误码统一**：gRPC 与 HttpClient 路径均抛 `AntiCorruptionException`，错误码 `{SERVICE}_UNAVAILABLE` / `{SERVICE}_REMOTE_FAILED` 一致
3. **可观测性统一**：gRPC 与 HttpClient 路径均通过 `AntiCorruptionMetrics.RecordFailure` 埋点，Prometheus 指标加 `path="grpc"` / `path="http"` 标签区分
4. **配置层灰度**：`UseGrpc` 开关可通过 Consul KV 热更新，单 BC 独立切换，无需重启
5. **向后兼容**：`.proto` 扩展仅新增字段，不修改/删除既有字段，`buf breaking` 校验通过

---

## 2. 总体架构

### 2.1 架构图

```
┌──────────────────────────────────────────────────────────────────────┐
│  调用方 BC (Order/Notification/Cart/ReviewAfterSales)                │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  业务层 → IXxxAntiCorruptionService (DI 注入 Dispatcher)     │   │
│  └────────────────────────┬─────────────────────────────────────┘   │
└───────────────────────────┼──────────────────────────────────────────┘
                            ↓
┌──────────────────────────────────────────────────────────────────────┐
│  AntiCorruptionDispatcher<IProductAntiCorruptionService>             │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  - UseGrpc=false → HttpProductAntiCorruptionService          │   │
│  │  - UseGrpc=true & 熔断关闭 → GrpcProductAntiCorruptionClient │   │
│  │  - UseGrpc=true & 熔断打开 → HttpProductAntiCorruptionService│   │
│  │  - 半开放 → 探测 gRPC 一次，成功关闭熔断，失败重开           │   │
│  └──────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
                            ↓
┌──────────────────────┐               ┌──────────────────────────────┐
│  HttpClient 路径     │               │  gRPC 路径                   │
│  HttpXxxService      │               │  GrpcXxxClient               │
│  (AntiCorruptionBase)│               │  (GrpcAntiCorruptionClientBase)│
│  → POST internal/v1/ │               │  → gRPC metadata:            │
│    X-Internal-Key    │               │    x-internal-key            │
└──────────────────────┘               └──────────────────────────────┘
                            ↓
┌──────────────────────────────────────────────────────────────────────┐
│  下游 BC.Api (Product/Promotion/Points/User/Order/Payment/Cart/      │
│  SellerShop/ReviewAfterSales/Notification/SystemAdmin)               │
│  ┌────────────────────────────┐  ┌────────────────────────────────┐ │
│  │ InternalXxxController      │  │ XxxGrpcService                 │ │
│  │ (既有，保留)               │  │ (新增，复用 IXxxInternalQuery  │ │
│  │                            │  │  Service 业务逻辑)             │ │
│  └────────────────────────────┘  └────────────────────────────────┘ │
│  Kestrel 同端口复用 HTTP/1.1 + HTTP/2 (5151-5161)                    │
└──────────────────────────────────────────────────────────────────────┘
```

### 2.2 组件清单

| 组件 | 类型 | 职责 |
|---|---|---|
| `AntiCorruptionDispatcher<TService>` | 新建 | 双轨调度，根据 `UseGrpc` 开关与熔断状态选择实现 |
| `CircuitBreakerState` | 新建 | 三状态机（Closed/Open/HalfOpen），单例 per 防腐层 |
| `GrpcInternalKeyInterceptor` | 新建 | gRPC 服务端鉴权拦截器，校验 `x-internal-key` metadata |
| `ConsulConfigWatcher` | 新建 | 后台服务，长轮询 Consul KV，热更新 `UseGrpc` 与 `GrpcEndpoints` |
| `GrpcXxxAntiCorruptionClient` | 新建（9 个，对应 9 个可迁移防腐层） | gRPC 客户端适配器，实现与 HttpClient 相同的接口 |
| `XxxGrpcService` | 新建（9 个被调用方） | gRPC 服务端，复用 `IXxxInternalQueryService` 业务逻辑 |
| `IXxxInternalQueryService` | 新建（5 个 BC） | 内部查询服务接口（4 个 BC 已存在：User/Product/Payment/Order） |

---

## 3. 双轨调度器与熔断降级机制

### 3.1 AntiCorruptionDispatcher 设计

```csharp
public sealed class AntiCorruptionDispatcher<TService> : IDisposable
    where TService : class
{
    private readonly TService _httpImplementation;
    private readonly TService? _grpcImplementation;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _optionsMonitor;
    private readonly ILogger<AntiCorruptionDispatcher<TService>> _logger;
    private readonly CircuitBreakerState _circuitBreaker;
    private readonly string _serviceName;

    public AntiCorruptionDispatcher(
        TService httpImplementation,
        TService? grpcImplementation,
        IOptionsMonitor<AntiCorruptionOptions> optionsMonitor,
        ILogger<AntiCorruptionDispatcher<TService>> logger,
        string serviceName,
        CircuitBreakerState circuitBreaker)
    {
        _httpImplementation = httpImplementation;
        _grpcImplementation = grpcImplementation;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _serviceName = serviceName;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        // 每次请求读取最新配置（支持 ConsulConfigWatcher 热更新）
        var currentOptions = _optionsMonitor.CurrentValue;

        if (!currentOptions.UseGrpc || _grpcImplementation is null)
        {
            return await operation(_httpImplementation).ConfigureAwait(false);
        }

        var state = _circuitBreaker.GetState();
        if (state == CircuitState.Open)
        {
            _logger.LogWarning("AntiCorruption {Service} gRPC circuit open, falling back to HTTP", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, "circuit_open");
            return await operation(_httpImplementation).ConfigureAwait(false);
        }

        try
        {
            var result = await operation(_grpcImplementation).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (AntiCorruptionException ex) when (IsGrpcUnavailable(ex))
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "AntiCorruption {Service} gRPC unavailable, falling back to HTTP", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, ExtractReason(ex));

            // 熔断因本次失败触发 → 本次直接抛（下次走 HTTP）
            if (_circuitBreaker.GetState() == CircuitState.Open)
            {
                throw;
            }

            // 熔断未触发 → 本次降级到 HttpClient
            return await operation(_httpImplementation).ConfigureAwait(false);
        }
    }

    private static bool IsGrpcUnavailable(AntiCorruptionException ex)
    {
        if (ex.InnerException is not RpcException rpc) return false;
        return rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded
            or StatusCode.Internal or StatusCode.ResourceExhausted;
    }

    private static string ExtractReason(AntiCorruptionException ex)
        => ex.InnerException is RpcException rpc ? $"grpc_{rpc.StatusCode}" : "grpc_unknown";

    public void Dispose() => _circuitBreaker?.Dispose();
}
```

### 3.2 CircuitBreakerState 三状态机

```csharp
internal enum CircuitState { Closed, Open, HalfOpen }

internal sealed class CircuitBreakerState : IDisposable
{
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly TimeSpan _openDuration;
    private int _consecutiveFailures;
    private int _halfOpenSuccesses;
    private DateTime _openedAt = DateTime.MinValue;
    private readonly object _lock = new();

    public CircuitBreakerState(int failureThreshold, int successThreshold, TimeSpan openDuration)
    {
        _failureThreshold = failureThreshold;
        _successThreshold = successThreshold;
        _openDuration = openDuration;
    }

    public CircuitState GetState()
    {
        lock (_lock)
        {
            if (_consecutiveFailures < _failureThreshold) return CircuitState.Closed;
            if (DateTime.UtcNow - _openedAt < _openDuration) return CircuitState.Open;
            return CircuitState.HalfOpen;
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            var state = GetState();
            if (state == CircuitState.HalfOpen)
            {
                _halfOpenSuccesses++;
                if (_halfOpenSuccesses >= _successThreshold)
                {
                    _consecutiveFailures = 0;
                    _halfOpenSuccesses = 0;
                    _openedAt = DateTime.MinValue;
                }
            }
            else
            {
                _consecutiveFailures = 0;
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            _halfOpenSuccesses = 0;
            if (_consecutiveFailures >= _failureThreshold)
            {
                _openedAt = DateTime.UtcNow;
            }
        }
    }

    public void Dispose() { }
}
```

### 3.3 状态转换规则

| 当前状态 | 事件 | 新状态 | 行为 |
|---|---|---|---|
| Closed | gRPC 调用成功 | Closed | 重置 `_consecutiveFailures=0` |
| Closed | gRPC 调用失败 | Closed 或 Open | 失败计数 +1，达到 3 次切 Open |
| Open | 任何请求 | Open | 走 HttpClient 降级路径，不调 gRPC |
| Open | 30 秒过去 | HalfOpen | 下次请求允许探测 gRPC |
| HalfOpen | gRPC 探测成功 | HalfOpen 或 Closed | 成功计数 +1，达到 2 次切 Closed |
| HalfOpen | gRPC 探测失败 | Open | 重置 30 秒计时 |

### 3.4 降级路径决策矩阵

| UseGrpc | 熔断状态 | gRPC 实现 | 实际路径 | 行为 |
|---|---|---|---|---|
| false | N/A | N/A | HttpClient | 直接走 HttpClient |
| true | Closed | 已注入 | gRPC | 调 gRPC，失败降级 HttpClient（仅当熔断未触发） |
| true | Open | 已注入 | HttpClient | 走 HttpClient，记 `circuit_open` 指标 |
| true | HalfOpen | 已注入 | gRPC 探测 | 探测成功累计计数，失败重开熔断 |
| true | 任意 | 未注入 | HttpClient | 配置异常，降级到 HttpClient 并记 warning |

### 3.5 业务异常与不可用异常的分类

| gRPC StatusCode | 分类 | 行为 |
|---|---|---|
| Unavailable | 不可用 | 触发降级 |
| DeadlineExceeded | 不可用 | 触发降级 |
| Internal | 不可用 | 触发降级 |
| ResourceExhausted | 不可用 | 触发降级 |
| NotFound | 业务异常 | 直接抛，不降级 |
| InvalidArgument | 业务异常 | 直接抛，不降级 |
| PermissionDenied | 业务异常 | 直接抛，不降级 |
| Unauthenticated | 业务异常 | 直接抛，不降级 |
| AlreadyExists | 业务异常 | 直接抛，不降级 |

---

## 4. .proto 契约扩展与 gRPC 服务端

### 4.1 扩展原则

1. 仅新增字段，不修改/删除既有字段
2. 所有新增字段使用 `optional` 关键字（proto3 语义）
3. `buf breaking` 配置为 `FILE` 模式（除 `EXTENSION_NO_DELETE`）
4. 所有 ID 字段统一使用 `string` 类型承载 `Guid` 的字符串形式

### 4.2 4 个 .proto 文件扩展清单

#### 4.2.1 order.proto 扩展

```proto
message OrderStatus {
  string order_id = 1;
  string status = 2;
  string payment_status = 3;
  string shipping_status = 4;
  // M4 双轨方案新增字段
  optional string user_id = 5;
  optional int64 completed_at = 6;
  optional int64 created_at = 7;
  repeated OrderItem items = 8;
  optional int64 cancelled_at = 9;
  optional string seller_id = 10;
}

message OrderItem {
  string sku_id = 1;
  string sku_name = 2;
  int32 quantity = 3;
  int64 unit_price_cents = 4;
  int64 sub_total_cents = 5;
}
```

#### 4.2.2 payment.proto 扩展

```proto
message PaymentInfo {
  string order_id = 1;
  string payment_status = 2;
  int64 amount_cents = 3;
  optional int64 paid_at = 4;
  // M4 双轨方案新增字段
  optional string channel = 5;
  optional string transaction_id = 6;
  optional int64 refunded_amount_cents = 7;
}
```

#### 4.2.3 user.proto 扩展

```proto
message UserContacts {
  string user_id = 1;
  optional string email = 2;
  optional string phone = 3;
  optional string nickname = 4;
  // M4 双轨方案新增字段
  optional bool email_verified = 5;
  optional bool phone_verified = 6;
  optional string preferred_language = 7;
}
```

#### 4.2.4 product.proto 扩展

```proto
message SkuInfo {
  string sku_id = 1;
  string spu_id = 2;
  string title = 3;
  int64 price_cents = 4;
  int32 stock = 5;
  optional string status = 6;
  // M4 双轨方案新增字段
  optional string shop_id = 7;
  optional string main_image_url = 8;
  optional int64 updated_at = 9;
}

message SkuBatchRequest {
  repeated string sku_ids = 1;
}
```

> **实施前必读**：扩展 .proto 前先用 Read 工具读取每个 .proto 文件，核对既有字段编号，避免冲突。本节示例仅说明扩展方向，实际实施时按既有字段编号追加。若 `user_id` 编号与既有字段冲突，改用下一个可用编号。

### 4.3 GrpcService 实现模板

每个 BC.Api 新建 `GrpcServices/XxxGrpcService.cs`，继承 .proto 生成的 `XxxInternalService.XxxInternalServiceBase`，注入既有 `IXxxInternalQueryService` 复用业务逻辑。

**ProductGrpcService 示例：**

```csharp
public sealed class ProductGrpcService : ProductInternalService.ProductInternalServiceBase
{
    private readonly IProductInternalQueryService _queryService;
    private readonly ILogger<ProductGrpcService> _logger;

    public ProductGrpcService(
        IProductInternalQueryService queryService,
        ILogger<ProductGrpcService> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public override async Task<SkuInfo> GetSkuInfo(GetSkuInfoRequest request, ServerCallContext context)
    {
        var skuId = Guid.Parse(request.SkuId);
        var dto = await _queryService.GetSkuInfoAsync(skuId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"SKU {request.SkuId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<SkuBatchResponse> GetSkuInfosBatch(
        SkuBatchRequest request, ServerCallContext context)
    {
        var skuIds = request.SkuIds.Select(Guid.Parse).ToList();
        var dtos = await _queryService.GetSkuInfosBatchAsync(skuIds, context.CancellationToken)
            .ConfigureAwait(false);

        var response = new SkuBatchResponse();
        response.Skus.AddRange(dtos.Select(MapToProto));
        return response;
    }

    private static SkuInfo MapToProto(SkuInfoDto dto) => new()
    {
        SkuId = dto.SkuId.ToString(),
        SpuId = dto.SpuId.ToString(),
        Title = dto.Title,
        PriceCents = (long)(dto.Price * 100),
        Stock = dto.Stock,
        Status = dto.Status,
        ShopId = dto.ShopId?.ToString() ?? string.Empty,
        MainImageUrl = dto.MainImageUrl ?? string.Empty,
        UpdatedAt = dto.UpdatedAt?.ToUnixTimeSeconds() ?? 0L
    };
}
```

### 4.4 GrpcInternalKeyInterceptor 鉴权

```csharp
public sealed class GrpcInternalKeyInterceptor : Interceptor
{
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcInternalKeyInterceptor> _logger;

    public GrpcInternalKeyInterceptor(
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcInternalKeyInterceptor> logger)
    {
        _options = options;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var expectedKey = _options.CurrentValue.InternalApiKey;

        var providedKey = context.RequestHeaders
            .FirstOrDefault(h => h.Key.Equals("x-internal-key", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (string.IsNullOrEmpty(providedKey) || providedKey != expectedKey)
        {
            _logger.LogWarning("gRPC call rejected: invalid or missing x-internal-key");
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Invalid or missing x-internal-key"));
        }

        return await continuation(request, context).ConfigureAwait(false);
    }
}
```

**注册**：在 `AddLenoApi` 内补充：

```csharp
if (antiCorruptionOptions.UseGrpc)
{
    services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = false;
        options.Interceptors.Add<GrpcInternalKeyInterceptor>();
    });
    services.AddSingleton<GrpcInternalKeyInterceptor>();
}
```

### 4.5 Kestrel 同端口复用

ASP.NET Core 10 默认 `HttpProtocols.Http1AndHttp2`，通过 ALPN 协商协议。11 个 BC.Api 的 `Program.cs` 无需额外配置 Kestrel。

可选显式声明（仅文档化目的）：

```json
{
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http1AndHttp2"
    }
  }
}
```

docker-compose.yml 与 Helm Chart 无需新增端口映射，5151-5161 既有端口同时承载 HTTP/1.1 与 HTTP/2。

### 4.6 11 个 BC.Api 启用 gRPC

每个 BC.Api 的 `Program.cs` 在 `app.UseLenoPipeline()` 之前追加：

```csharp
if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
{
    app.MapGrpcService<ProductGrpcService>();
}
```

### 4.7 GrpcService 实现清单

| BC.Api | GrpcService 类 | .proto service | 复用接口 |
|---|---|---|---|
| Product.Api | ProductGrpcService | ProductInternalService | IProductInternalQueryService（既有） |
| Promotion.Api | PromotionGrpcService | PromotionInternalService | IPromotionInternalQueryService（新建） |
| PointsMembership.Api | PointsGrpcService | PointsInternalService | IPointsInternalQueryService（新建） |
| UserAuth.Api | UserGrpcService | UserInternalService | IUserInternalQueryService（既有） |
| Order.Api | OrderGrpcService | OrderInternalService | IOrderInternalQueryService（既有，需扩展返回字段） |
| Payment.Api | PaymentGrpcService | PaymentInternalService | IPaymentInternalQueryService（既有，需扩展返回字段） |
| Cart.Api | CartGrpcService | CartInternalService | ICartInternalQueryService（新建） |
| SellerShop.Api | SellerGrpcService | SellerInternalService | ISellerInternalQueryService（新建） |
| ReviewAfterSales.Api | ReviewGrpcService | ReviewInternalService | IReviewInternalQueryService（新建） |

> **说明**：Notification 与 SystemAdmin 是调用方而非被调用方，本次不实现 GrpcService。

---

## 5. gRPC 客户端适配器与双轨注册

### 5.1 GrpcXxxClient 适配器模板

```csharp
public sealed class GrpcProductAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IProductAntiCorruptionService
{
    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    public GrpcProductAntiCorruptionClient(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcProductAntiCorruptionClient> logger)
        : base(logger)
    {
        _client = client;
        _options = options;
    }

    protected override string ServiceName => "product";

    public Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync(async () =>
        {
            var request = new GetSkuInfoRequest { SkuId = skuId.ToString() };
            var metadata = BuildMetadata("product");
            var response = await _client.GetSkuInfoAsync(request, metadata, cancellationToken: ct)
                .ConfigureAwait(false);
            return MapToDto(response);
        }, ct);

    private Metadata BuildMetadata(string targetBc)
    {
        var metadata = new Metadata();
        var currentOptions = _options.CurrentValue;
        if (currentOptions.TargetInternalApiKeys.TryGetValue(targetBc, out var key))
        {
            metadata.Add("x-internal-key", key);
        }
        return metadata;
    }

    private static SkuInfo? MapToDto(ProductSkuInfo proto) => new()
    {
        SkuId = Guid.Parse(proto.SkuId),
        SpuId = Guid.Parse(proto.SpuId),
        Title = proto.Title,
        Price = proto.PriceCents / 100m,
        Stock = proto.Stock,
        Status = proto.Status,
        ShopId = string.IsNullOrEmpty(proto.ShopId) ? null : Guid.Parse(proto.ShopId),
        MainImageUrl = string.IsNullOrEmpty(proto.MainImageUrl) ? null : proto.MainImageUrl,
        UpdatedAt = proto.UpdatedAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(proto.UpdatedAt).UtcDateTime
            : null
    };
}
```

### 5.2 GrpcAntiCorruptionClientBase 增强

修改既有 `GrpcAntiCorruptionClientBase`，保留 `RpcException` 作为 `InnerException`，供 Dispatcher 判断是否降级：

```csharp
protected async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
{
    try
    {
        return await action().ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        throw;
    }
    catch (RpcException ex) when (IsUnavailable(ex.StatusCode))
    {
        _logger.LogWarning(ex, "gRPC call to {Service} failed: {Status}", ServiceName, ex.StatusCode);
        AntiCorruptionMetrics.RecordFailure(ServiceName, $"{ServiceName}_UNAVAILABLE", "grpc");
        throw new AntiCorruptionException(
            $"{ServiceName}_UNAVAILABLE",
            $"gRPC call to {ServiceName} failed: {ex.StatusCode}",
            ex);  // InnerException 保留 RpcException
    }
    catch (RpcException ex)
    {
        _logger.LogWarning(ex, "gRPC call to {Service} remote failed: {Status}", ServiceName, ex.StatusCode);
        AntiCorruptionMetrics.RecordFailure(ServiceName, $"{ServiceName}_REMOTE_FAILED", "grpc");
        throw new AntiCorruptionException(
            $"{ServiceName}_REMOTE_FAILED",
            $"gRPC call to {ServiceName} remote failed: {ex.StatusCode}",
            ex);
    }
    catch (DomainException)
    {
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error calling gRPC service {Service}", ServiceName);
        AntiCorruptionMetrics.RecordFailure(ServiceName, $"{ServiceName}_REMOTE_FAILED", "grpc");
        throw new AntiCorruptionException(
            $"{ServiceName}_REMOTE_FAILED",
            $"Unexpected error calling {ServiceName}",
            ex);
    }
}

private static bool IsUnavailable(StatusCode code)
    => code is StatusCode.Unavailable or StatusCode.DeadlineExceeded
        or StatusCode.Internal or StatusCode.ResourceExhausted;
```

### 5.3 10 个 gRPC 客户端适配器清单

| # | 调用方 BC | 适配器类名 | 实现接口 | 下游 BC | 下游 GrpcService |
|---|---|---|---|---|---|
| 1 | Order | GrpcProductAntiCorruptionClient | IProductAntiCorruptionService | Product | ProductGrpcService |
| 2 | Order | GrpcPromotionAntiCorruptionClient | IPromotionAntiCorruptionService | Promotion | PromotionGrpcService |
| 3 | Order | GrpcPointsAntiCorruptionClient | IPointsAntiCorruptionService | PointsMembership | PointsGrpcService |
| 4 | Notification | GrpcUserContactAntiCorruptionClient | IUserContactAntiCorruptionService | UserAuth | UserGrpcService |
| 5 | Cart | GrpcCartPriceClient | ICartPriceService | Product | ProductGrpcService |
| 6 | Cart | GrpcProductSnapshotClient | IProductSnapshotAntiCorruptionService | Product | ProductGrpcService |
| 7 | ReviewAfterSales | GrpcPaymentInfoQueryClient | IPaymentInfoQueryService | Payment | PaymentGrpcService |
| 8 | ReviewAfterSales | GrpcAfterSalesEligibilityClient | IAfterSalesEligibilityChecker | Order | OrderGrpcService |
| 9 | ReviewAfterSales | GrpcReviewEligibilityClient | IReviewEligibilityChecker | Order | OrderGrpcService |

> **LogisticsTrackingService 不迁移**：调用第三方物流 API（kdniao），无对应 .proto。

### 5.4 DI 注册模板（Order BC 示例）

```csharp
// 既有 HttpClient 注册（保留，作为降级备份）
services.AddHttpClient<ProductAntiCorruptionService>(client =>
{
    client.BaseAddress = new Uri(configuration["AntiCorruption:Endpoints:product"]
        ?? throw new InvalidOperationException("Product endpoint not configured"));
}).AddAntiCorruptionPolicies();

// M4 双轨方案：gRPC 客户端注册（仅当 UseGrpc=true 时生效）
services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(sp =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
    var endpoint = options.GrpcEndpoints["product"]
        ?? throw new InvalidOperationException("gRPC endpoint for product not configured");
    return new GrpcClientEndpointOptions(endpoint);
});
services.AddScoped<GrpcProductAntiCorruptionClient>();

// 熔断器（单例 per 防腐层）
services.AddKeyedSingleton<CircuitBreakerState>("product", (sp, _) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
    return new CircuitBreakerState(
        failureThreshold: options.CircuitBreaker?.FailureThreshold ?? 3,
        successThreshold: options.CircuitBreaker?.SuccessThreshold ?? 2,
        openDuration: TimeSpan.FromSeconds(options.CircuitBreaker?.OpenDurationSeconds ?? 30));
});

// Dispatcher（替换既有直接注册）
services.AddScoped<IProductAntiCorruptionService>(sp =>
{
    var httpImpl = sp.GetRequiredService<ProductAntiCorruptionService>();
    var grpcImpl = sp.GetService<GrpcProductAntiCorruptionClient>();  // null when UseGrpc=false
    var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
    var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IProductAntiCorruptionService>>>();
    var circuitBreaker = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
    return new AntiCorruptionDispatcher<IProductAntiCorruptionService>(
        httpImpl, grpcImpl, options, logger, "product", circuitBreaker);
});
```

### 5.5 生命周期

| 组件 | 生命周期 | 说明 |
|---|---|---|
| `ProductAntiCorruptionService`（HttpClient 实现） | Scoped | 每请求新建 |
| `GrpcProductAntiCorruptionClient`（gRPC 实现） | Scoped | 每请求新建 |
| `AntiCorruptionDispatcher<IProductAntiCorruptionService>` | Scoped | 每请求新建 |
| `GrpcChannel` | Singleton | 由 `AddGrpcClient<T>` 工厂管理，连接池复用 |
| `CircuitBreakerState` | Keyed Singleton | 每个防腐层一个实例，跨请求累积失败计数 |

### 5.6 gRPC Channel 管理

使用 `Grpc.AspNetCore.Server.ClientFactory` 包提供的 `AddGrpcClient<TClient>` 扩展方法管理 Channel 池，与 `IHttpClientFactory` 语义一致。

`Leno.Infrastructure.csproj` 新增引用：

```xml
<PackageReference Include="Grpc.AspNetCore.Server.ClientFactory" Version="2.65.0" />
```

---

## 6. buf generate CI 集成与配置注入

### 6.1 CI Workflow 新增 job

修改 `.github/workflows/ci.yml`：

```yaml
jobs:
  generate-grpc-contracts:
    name: Verify gRPC C# Contracts
    runs-on: ubuntu-latest
    paths:
      - "src/BuildingBlocks/Leno.SharedContracts/Protos/**"
      - "src/BuildingBlocks/Leno.SharedContracts/buf.yaml"
      - "src/BuildingBlocks/Leno.SharedContracts/buf.gen.yaml"
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup Buf CLI
        uses: bufbuild/buf-action@v1
        with:
          setup_only: true

      - name: Generate C# code
        working-directory: src/BuildingBlocks/Leno.SharedContracts
        run: buf generate

      - name: Check for uncommitted generated files
        run: |
          cd src/BuildingBlocks/Leno.SharedContracts.Grpc
          if [ -n "$(git status --porcelain Generated/)" ]; then
            echo "::error::Generated/ files are out of date. Run 'buf generate' and commit changes."
            exit 1
          fi

      - name: Verify build compiles
        run: dotnet build src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj
```

### 6.2 首次生成流程

由于 `buf generate` 从未运行过，首次生成需由开发者本地执行：

```bash
# 安装 buf CLI（macOS）
brew install bufbuild/buf/buf

# 安装 buf CLI（Windows）
powershell -c "irm https://github.com/bufbuild/buf/releases/latest/download/buf-Windows-x86_64.exe -OutFile buf.exe"

# 在 Leno.SharedContracts 目录下生成
cd src/BuildingBlocks/Leno.SharedContracts
buf generate

# 提交生成代码
git add ../Leno.SharedContracts.Grpc/Generated
git commit -m "feat(M4): 首次运行 buf generate 生成 11 个 BC 的 gRPC C# 客户端代码"
```

### 6.3 csproj 调整

修改 `src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.27.0" />
    <PackageReference Include="Grpc.Core" Version="2.46.*" />
    <PackageReference Include="Grpc.Net.Client" Version="2.63.0" />
    <PackageReference Include="Grpc.Tools" Version="2.65.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="Generated/**/*.cs" />
  </ItemGroup>
</Project>
```

删除 `src/BuildingBlocks/Leno.SharedContracts.Grpc/_Placeholder.cs`。

`Generated/` 目录**纳入版本控制**（不忽略）。

### 6.4 AntiCorruptionOptions 扩展

```csharp
public sealed class AntiCorruptionOptions
{
    public bool UseGrpc { get; init; } = false;
    public Dictionary<string, string> GrpcEndpoints { get; init; } = new();
    public PollyOptions Polly { get; init; } = new();
    public Dictionary<string, string> TargetInternalApiKeys { get; init; } = new();

    // M4 双轨方案新增
    public CircuitBreakerOptions? CircuitBreaker { get; init; } = new();
    public string? ServiceName { get; init; }
    public string? InternalApiKey { get; init; }
}

public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; init; } = 3;
    public int SuccessThreshold { get; init; } = 2;
    public int OpenDurationSeconds { get; init; } = 30;
}
```

### 6.5 Consul KV 路径设计

| KV 路径 | 值示例 | 说明 |
|---|---|---|
| `leno/grpc/endpoints/product` | `https://leno-product-api:5152` | Product BC gRPC 端点 |
| `leno/grpc/endpoints/promotion` | `https://leno-promotion-api:5155` | Promotion BC gRPC 端点 |
| `leno/grpc/endpoints/points` | `https://leno-pointsmembership-api:5157` | PointsMembership BC gRPC 端点 |
| `leno/grpc/endpoints/user` | `https://leno-userauth-api:5151` | UserAuth BC gRPC 端点 |
| `leno/grpc/endpoints/order` | `https://leno-order-api:5154` | Order BC gRPC 端点 |
| `leno/grpc/endpoints/payment` | `https://leno-payment-api:5158` | Payment BC gRPC 端点 |
| `leno/grpc/endpoints/cart` | `https://leno-cart-api:5153` | Cart BC gRPC 端点 |
| `leno/grpc/endpoints/seller` | `https://leno-sellershop-api:5160` | SellerShop BC gRPC 端点 |
| `leno/grpc/endpoints/review` | `https://leno-reviewaftersales-api:5156` | ReviewAfterSales BC gRPC 端点 |
| `leno/anticorruption/use-grpc/order` | `false` | Order BC 灰度开关（默认 false） |
| `leno/anticorruption/use-grpc/notification` | `false` | Notification BC 灰度开关 |
| `leno/anticorruption/use-grpc/cart` | `false` | Cart BC 灰度开关 |
| `leno/anticorruption/use-grpc/reviewaftersales` | `false` | ReviewAfterSales BC 灰度开关 |

### 6.6 ConsulConfigWatcher 热更新

新建 `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs`：

```csharp
public sealed class ConsulConfigWatcher : BackgroundService
{
    private readonly IConsulClient _consul;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConsulConfigWatcher> _logger;

    public ConsulConfigWatcher(
        IConsulClient consul,
        IConfiguration configuration,
        ILogger<ConsulConfigWatcher> logger)
    {
        _consul = consul;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var bcName = _configuration["Service:Name"];
        var useGrpcKey = $"leno/anticorruption/use-grpc/{bcName}";

        ulong? waitIndex = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var queryResult = await _consul.KV.Get(useGrpcKey, new QueryOptions
                {
                    WaitIndex = waitIndex ?? 0,
                    WaitTime = TimeSpan.FromMinutes(5)
                }, ct).ConfigureAwait(false);

                if (queryResult.Response != null && queryResult.LastIndex != waitIndex)
                {
                    waitIndex = queryResult.LastIndex;
                    var newValue = Encoding.UTF8.GetString(queryResult.Response.Value);
                    _configuration["AntiCorruption:UseGrpc"] = newValue;
                    _logger.LogInformation("UseGrpc changed to {Value} for {BC}", newValue, bcName);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Consul KV watch failed, retry in 10s");
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            }
        }
    }
}
```

所有消费方（Dispatcher、GrpcServices）注入 `IOptionsMonitor<AntiCorruptionOptions>` 而非 `IOptions<AntiCorruptionOptions>`，Consul KV 变更后自动获取最新值，延迟约 1-2 秒。

### 6.7 配置层级优先级

```
1. Consul KV（最高，运行时热更新）
   ↓ 覆盖
2. appsettings.{Environment}.json
   ↓ 覆盖
3. appsettings.json（默认值）
   ↓ 覆盖
4. AntiCorruptionOptions 默认值（C# 代码 init）
```

### 6.8 appsettings.json 默认配置示例（Order BC）

```json
{
  "Service": { "Name": "order" },
  "AntiCorruption": {
    "UseGrpc": false,
    "Polly": {
      "RetryCount": 3,
      "CircuitBreakerFailureThreshold": 0.5,
      "CircuitBreakerSamplingDurationSeconds": 30,
      "TimeoutSeconds": 10
    },
    "CircuitBreaker": {
      "FailureThreshold": 3,
      "SuccessThreshold": 2,
      "OpenDurationSeconds": 30
    },
    "Endpoints": {
      "product": "http://leno-product-api:8080",
      "promotion": "http://leno-promotion-api:8080",
      "pointsmembership": "http://leno-pointsmembership-api:8080"
    },
    "GrpcEndpoints": {
      "product": "https://leno-product-api:5152",
      "promotion": "https://leno-promotion-api:5155",
      "pointsmembership": "https://leno-pointsmembership-api:5157"
    },
    "TargetInternalApiKeys": {
      "product": "${LENO_INTERNAL_API_KEY_PRODUCT}",
      "promotion": "${LENO_INTERNAL_API_KEY_PROMOTION}",
      "pointsmembership": "${LENO_INTERNAL_API_KEY_POINTS}"
    },
    "InternalApiKey": "${LENO_INTERNAL_API_KEY_ORDER}",
    "ServiceName": "order"
  }
}
```

### 6.9 Helm Chart 配置

修改 `deploy/helm/leno/values.yaml`：

```yaml
antiCorruption:
  useGrpc:
    product: false
    order: false
    promotion: false
    pointsMembership: false
    userAuth: false
    payment: false
    cart: false
    sellerShop: false
    reviewAfterSales: false
    notification: false
    systemAdmin: false
  grpcEndpoints:
    product: "https://leno-product-api:5152"
    promotion: "https://leno-promotion-api:5155"
    pointsMembership: "https://leno-pointsmembership-api:5157"
    userAuth: "https://leno-userauth-api:5151"
    order: "https://leno-order-api:5154"
    payment: "https://leno-payment-api:5158"
    cart: "https://leno-cart-api:5153"
    sellerShop: "https://leno-sellershop-api:5160"
    reviewAfterSales: "https://leno-reviewaftersales-api:5156"
    notification: "https://leno-notification-api:5159"
    systemAdmin: "https://leno-systemadmin-api:5161"
  circuitBreaker:
    failureThreshold: 3
    successThreshold: 2
    openDurationSeconds: 30
```

---

## 7. 迁移批次与 POC 计划

### 7.1 迁移阶段总览

```
阶段 0: 基础设施准备（一次性，无灰度）
   ├─ buf generate 首次运行
   ├─ .proto 文件扩展（4 个）
   ├─ AntiCorruptionDispatcher + CircuitBreakerState 基础设施
   ├─ GrpcInternalKeyInterceptor 鉴权拦截器
   └─ ConsulConfigWatcher 热更新机制
   ↓
阶段 1: POC（Order BC 调 Product BC，1 个防腐层）
   ├─ Product.Api 新建 ProductGrpcService
   ├─ Order 新建 GrpcProductAntiCorruptionClient
   ├─ Order DI 注册双轨（HttpClient + gRPC + Dispatcher）
   ├─ 灰度验证 1 周（UseGrpc=true）
   └─ 验收：gRPC 调用成功率 ≥ 99.9%，熔断降级机制生效
   ↓
阶段 2: Order BC 剩余 2 个防腐层（Promotion/Points）
   ├─ Promotion.Api + PointsMembership.Api 新建 GrpcService
   ├─ Order 新建 2 个 GrpcClient 适配器
   ├─ 灰度验证 1 周
   └─ 验收：3 个防腐层全部 gRPC 化
   ↓
阶段 3: Notification + Cart BC（3 个防腐层）
   ├─ UserAuth.Api 新建 UserGrpcService
   ├─ Cart.Api 新建 CartGrpcService（被调用方）
   ├─ Notification 新建 GrpcUserContactAntiCorruptionClient
   ├─ Cart 新建 GrpcCartPriceClient + GrpcProductSnapshotClient
   └─ 灰度验证 1 周
   ↓
阶段 4: ReviewAfterSales BC（3 个防腐层）
   ├─ Order.Api + Payment.Api 新建 GrpcService
   ├─ ReviewAfterSales 新建 3 个 GrpcClient 适配器
   └─ 灰度验证 1 周
   ↓
阶段 5: 全量稳定运行（4 周观察期）
   └─ 4 周内熔断降级触发率 < 0.1%
```

### 7.2 阶段 0：基础设施准备

| # | 任务 | 文件 |
|---|---|---|
| 1 | 扩展 4 个 .proto 文件 | `src/BuildingBlocks/Leno.SharedContracts/Protos/{order,payment,user,product}.proto` |
| 2 | 首次运行 buf generate | `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/` |
| 3 | 修改 csproj 引入 Generated | `Leno.SharedContracts.Grpc.csproj` |
| 4 | CI 集成 generate-grpc-contracts job | `.github/workflows/ci.yml` |
| 5 | 新建 CircuitBreakerState | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs` |
| 6 | 新建 AntiCorruptionDispatcher | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs` |
| 7 | 修改 GrpcAntiCorruptionClientBase | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs` |
| 8 | 新建 GrpcInternalKeyInterceptor | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcInternalKeyInterceptor.cs` |
| 9 | 扩展 AntiCorruptionOptions | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs` |
| 10 | 扩展 AntiCorruptionMetrics | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs` |
| 11 | 新建 ConsulConfigWatcher | `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs` |
| 12 | 修改 ConfigCenterExtensions | `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs` |
| 13 | 修改 AddLenoApi | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` |
| 14 | 新建单元测试 | `tests/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/` |

**验收标准**：
- `dotnet build Leno.sln` 0 错误 0 警告
- 新增单元测试全部 PASS（覆盖率 ≥ 80%）
- `buf lint` + `buf breaking` 通过
- `buf generate` 本地运行成功，`Generated/` 提交
- Consul KV 写入 11 个 BC 的 gRPC 端点

### 7.3 阶段 1：POC（Order → Product）

**选型理由**：
1. 高频调用：OrderSaga 每次下单调用 `GetSkuInfoAsync`，QPS 最高
2. 影响范围可控：仅 Order BC 受影响
3. 业务场景明确：失败降级到 HttpClient 后用户体验无明显差异
4. 既有接口成熟：`IProductInternalQueryService` + `IProductAntiCorruptionService` 已稳定

**实施清单**：
| # | 任务 | 文件 |
|---|---|---|
| 1 | Product.Api 新建 ProductGrpcService | `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs` |
| 2 | Product.Api Program.cs 启用 gRPC | `src/Services/Product/Leno.Product.Api/Program.cs` |
| 3 | Order 新建 GrpcProductAntiCorruptionClient | `src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs` |
| 4 | Order 修改 ServiceCollectionExtensions | `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| 5 | 新建集成测试 | `tests/Services/Order/Leno.Order.Infrastructure.Tests/Grpc/GrpcProductAntiCorruptionClientTests.cs` |
| 6 | 新建熔断降级集成测试 | `tests/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionDispatcherFallbackTests.cs` |

**POC 验证指标（1 周观察期）**：

| 指标 | 目标 | 数据源 |
|---|---|---|
| gRPC 调用成功率 | ≥ 99.9% | `anticorruption_failure_total{path="grpc"}` |
| 熔断降级触发次数 | < 10 次/天 | `anticorruption_fallback_total{reason="circuit_open"}` |
| gRPC P99 延迟 | < 10ms | Jaeger trace |
| HttpClient P99 延迟（降级时） | < 50ms | Jaeger trace |
| OrderSaga 下单 P99 | 从 30-60ms 降到 10-20ms | Jaeger trace |
| 业务错误率 | 0（无新增业务异常） | Application Insights |

**POC 回滚预案**：
- gRPC 调用成功率 < 99%
- 熔断降级触发 > 100 次/天
- 业务错误率 > 0.01%
- OrderSaga 下单 P99 > 100ms

**回滚操作**：`curl -X PUT "${CONSUL}/v1/kv/leno/anticorruption/use-grpc/order" -d 'false'`，1-2 秒内生效，无需重启。

### 7.4 阶段 2-4 实施清单

**阶段 2：Order 剩余 2 个防腐层**

| 调用方 | 下游 BC | 防腐层接口 | gRPC 适配器 |
|---|---|---|---|
| Order | Promotion | IPromotionAntiCorruptionService | GrpcPromotionAntiCorruptionClient |
| Order | PointsMembership | IPointsAntiCorruptionService | GrpcPointsAntiCorruptionClient |

新建 GrpcService：Promotion.Api/PromotionGrpcService.cs（需新建 IPromotionInternalQueryService）；PointsMembership.Api/PointsGrpcService.cs（需新建 IPointsInternalQueryService）。

**阶段 3：Notification + Cart**

| 调用方 | 下游 BC | 防腐层接口 | gRPC 适配器 |
|---|---|---|---|
| Notification | UserAuth | IUserContactAntiCorruptionService | GrpcUserContactAntiCorruptionClient |
| Cart | Product | ICartPriceService | GrpcCartPriceClient |
| Cart | Product | IProductSnapshotAntiCorruptionService | GrpcProductSnapshotClient |

新建 GrpcService：UserAuth.Api/UserGrpcService.cs（复用 IUserInternalQueryService）；Cart.Api/CartGrpcService.cs（被调用方，需新建 ICartInternalQueryService）。

**阶段 4：ReviewAfterSales**

| 调用方 | 下游 BC | 防腐层接口 | gRPC 适配器 |
|---|---|---|---|
| ReviewAfterSales | Payment | IPaymentInfoQueryService | GrpcPaymentInfoQueryClient |
| ReviewAfterSales | Order | IAfterSalesEligibilityChecker | GrpcAfterSalesEligibilityClient |
| ReviewAfterSales | Order | IReviewEligibilityChecker | GrpcReviewEligibilityClient |

新建 GrpcService：Payment.Api/PaymentGrpcService.cs（复用 IPaymentInternalQueryService，需扩展返回字段）；Order.Api/OrderGrpcService.cs（复用 IOrderInternalQueryService，需扩展返回字段）。

### 7.5 阶段 5：全量稳定运行

**目标**：所有 4 个调用方 BC 在 `UseGrpc=true` 模式下稳定运行 4 周。

**监控指标**（每周回顾）：

| 指标 | 目标 | 回滚阈值 |
|---|---|---|
| 熔断降级触发率 | < 0.1% | > 1% |
| gRPC 调用成功率 | ≥ 99.9% | < 99% |
| OrderSaga 下单 P99 | < 20ms | > 100ms |
| 业务错误率 | 0 | > 0.01% |

4 周后写入 retrospective 文档，归档经验，关闭 M4 gRPC 双轨方案。

### 7.6 总工期估算

| 阶段 | 工作量 | 灰度观察期 | 总周期 |
|---|---|---|---|
| 阶段 0 | 基础设施 14 项任务 | 无 | 1-2 天 |
| 阶段 1 (POC) | 6 项任务 | 1 周 | 1 周 |
| 阶段 2 | 4 项任务 | 1 周 | 1 周 |
| 阶段 3 | 5 项任务 | 1 周 | 1 周 |
| 阶段 4 | 4 项任务 | 1 周 | 1 周 |
| 阶段 5 | 无开发任务 | 4 周 | 4 周 |
| **合计** | | | **约 8-9 周** |

### 7.7 灰度切换操作手册

**启用 gRPC（单 BC）**：

```bash
# 1. 检查目标 BC 的 gRPC 端点是否可达
curl -k https://leno-product-api:5152 -H "Content-Type: application/grpc" -v

# 2. 切换 Consul KV
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/order" -d 'true'

# 3. 观察 5 分钟指标
# - anticorruption_fallback_total{service="product"} 是否激增
# - anticorruption_circuit_open{service="product"} 是否为 1
# - Jaeger trace 是否出现 gRPC 调用
```

**紧急回滚**：

```bash
# 立即切换回 HTTP（1-2 秒生效）
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/order" -d 'false'

# 验证生效
kubectl logs deployment/leno-order-api -f | grep "UseGrpc changed"
```

**熔断器手动重置**：

```bash
# 方案 A：重启调用方 BC（清除内存中的 CircuitBreakerState）
kubectl rollout restart deployment/leno-order-api

# 方案 B：等待 30 秒自动进入 HalfOpen 探测（推荐）
```

---

## 8. 测试策略

### 8.1 测试金字塔

```
              ┌──────────────────┐
              │  E2E 验收测试     │  阶段 1-4 各 1 个，验证完整下单流程
              └──────────────────┘
            ┌──────────────────────┐
            │  集成测试            │  Testcontainers 启动 gRPC 服务端 + 客户端
            │  (3 个场景)          │  验证熔断降级、鉴权、配置热更新
            └──────────────────────┘
          ┌──────────────────────────┐
          │  组件测试                │  Dispatcher + 真实 CircuitBreakerState
          │  (5 个场景)              │  + Mock IHttpImplementation/IGrpcImplementation
          └──────────────────────────┘
        ┌──────────────────────────────┐
        │  单元测试                    │  CircuitBreakerState 状态机
        │  (8 个场景)                  │  GrpcInternalKeyInterceptor 鉴权
        └──────────────────────────────┘
```

### 8.2 单元测试清单

#### 8.2.1 CircuitBreakerState 单元测试

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `Initial_State_Is_Closed` | 新建实例 | `GetState() == Closed` |
| 2 | `RecordFailure_BelowThreshold_StaysClosed` | 失败 2 次（阈值 3） | `Closed` |
| 3 | `RecordFailure_AtThreshold_TransitionsToOpen` | 失败 3 次 | `Open` |
| 4 | `Open_AfterDuration_TransitionsToHalfOpen` | Open 后等待 30s | `HalfOpen` |
| 5 | `HalfOpen_SuccessBelowThreshold_StaysHalfOpen` | HalfOpen + 1 次成功（阈值 2） | `HalfOpen` |
| 6 | `HalfOpen_SuccessAtThreshold_TransitionsToClosed` | HalfOpen + 2 次成功 | `Closed` |
| 7 | `HalfOpen_Failure_TransitionsToOpen` | HalfOpen + 1 次失败 | `Open`，重置 30s 计时 |
| 8 | `RecordSuccess_InClosed_ResetsFailureCount` | Closed + 失败 2 次 + 成功 1 次 + 失败 1 次 | `Closed`（计数重置后重新累计） |

#### 8.2.2 GrpcInternalKeyInterceptor 单元测试

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `Valid_InternalKey_CallsContinuation` | metadata 含正确 `x-internal-key` | 调用 `continuation`，返回响应 |
| 2 | `Missing_InternalKey_ThrowsUnauthenticated` | metadata 无 `x-internal-key` | 抛 `RpcException(StatusCode.Unauthenticated)` |
| 3 | `Wrong_InternalKey_ThrowsUnauthenticated` | metadata 含错误 key | 抛 `RpcException(StatusCode.Unauthenticated)` |
| 4 | `CaseInsensitive_HeaderMatching` | `X-Internal-Key`（大写） | 调用 `continuation` |

#### 8.2.3 AntiCorruptionDispatcher 单元测试

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `UseGrpc_False_AlwaysCallsHttp` | `UseGrpc=false` | 调用 `IHttpImplementation`，从不调 `IGrpcImplementation` |
| 2 | `UseGrpc_True_Closed_CallsGrpc` | `UseGrpc=true`，熔断 `Closed` | 调用 `IGrpcImplementation` |
| 3 | `UseGrpc_True_Open_FallsBackToHttp` | `UseGrpc=true`，熔断 `Open` | 调用 `IHttpImplementation`，记 `circuit_open` 指标 |
| 4 | `UseGrpc_True_GrpcFailure_FallsBackToHttp` | gRPC 抛 `Unavailable` | 降级到 `IHttpImplementation`，记 `grpc_Unavailable` 指标 |
| 5 | `UseGrpc_True_GrpcFailure_BusinessException_NoFallback` | gRPC 抛 `NotFound` | 不降级，直接抛异常 |
| 6 | `UseGrpc_True_HalfOpen_ProbeSuccess_ClosesCircuit` | HalfOpen + 2 次成功 | 熔断 `Closed` |
| 7 | `UseGrpc_True_HalfOpen_ProbeFailure_ReopensCircuit` | HalfOpen + 1 次失败 | 熔断 `Open` |
| 8 | `GrpcImpl_Null_FallsBackToHttp` | `UseGrpc=true` 但 gRPC 实现未注入 | 调用 `IHttpImplementation`，记 warning |
| 9 | `GrpcFailure_ReachesThreshold_ThrowsAfterFallback` | gRPC 连续失败 3 次 | 第 3 次失败后熔断 `Open`，本次抛异常（不降级） |
| 10 | `CircuitOpen_RecordFallbackMetric` | 熔断 `Open` | `AntiCorruptionMetrics.RecordFallback` 被调用 1 次 |

#### 8.2.4 GrpcProductAntiCorruptionClient 单元测试（POC 代表）

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `GetSkuInfo_Success_ReturnsMappedDto` | gRPC 返回正常 SkuInfo | 返回 `SkuInfo` DTO，字段映射正确 |
| 2 | `GetSkuInfo_Unavailable_ThrowsAntiCorruptionException` | gRPC 抛 `Unavailable` | 抛 `AntiCorruptionException`，InnerException 为 `RpcException` |
| 3 | `GetSkuInfo_NotFound_ThrowsAntiCorruptionException` | gRPC 抛 `NotFound` | 抛 `AntiCorruptionException`，错误码 `PRODUCT_REMOTE_FAILED` |
| 4 | `GetSkuInfo_PassesInternalKeyMetadata` | 调用 gRPC | metadata 含 `x-internal-key` header |
| 5 | `GetSkuInfosBatch_Success_ReturnsMappedList` | gRPC 返回多条 | 返回 `List<SkuInfo>` |
| 6 | `GetSkuInfo_Cancellation_Propagates` | CancellationToken 触发 | 抛 `OperationCanceledException` |

### 8.3 集成测试清单

#### 8.3.1 Testcontainers gRPC 集成测试

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `EndToEnd_GrpcCall_Success` | Order 调 Product gRPC | 返回正确 SkuInfo |
| 2 | `EndToEnd_GrpcDown_FallbackToHttp` | Product gRPC 容器停止 | 自动降级到 HttpClient，调用成功 |
| 3 | `EndToEnd_CircuitBreaker_OpensAfter3Failures` | gRPC 连续失败 3 次 | 熔断 Open，后续请求直接走 HttpClient |
| 4 | `EndToEnd_Auth_RejectedWithoutInternalKey` | Order 不传 `x-internal-key` | Product gRPC 返回 `Unauthenticated` |

#### 8.3.2 ConsulConfigWatcher 集成测试

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `ConfigChange_UseGrpc_UpdatesWithin2Seconds` | Consul KV 修改 `UseGrpc=true` | `IOptionsMonitor.CurrentValue.UseGrpc` 在 2 秒内变为 true |
| 2 | `ConfigChange_GrpcEndpoints_Updates` | Consul KV 修改 `GrpcEndpoints:product` | 端点地址更新 |
| 3 | `ConsulDown_RetriesAndReconnects` | Consul 容器停止后重启 | Watcher 自动重连，配置不丢失 |

### 8.4 E2E 验收测试

#### 8.4.1 POC 阶段 E2E 验收

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `OrderSaga_WithGrpc_CompletesSuccessfully` | `UseGrpc=true`，下单流程 | OrderSaga 完成，gRPC 调用 Product 成功 |
| 2 | `OrderSaga_GrpcDown_FallbacksAndCompletes` | Product gRPC 不可用 | OrderSaga 通过 HttpClient 降级完成 |
| 3 | `OrderSaga_CircuitOpen_RetriesWithHttp` | 熔断 Open 状态 | OrderSaga 通过 HttpClient 完成，gRPC 不被调用 |

#### 8.4.2 阶段 4 全量 E2E 验收

| # | 测试方法 | 场景 | 预期 |
|---|---|---|---|
| 1 | `AllBc_GrpcEnabled_NormalFlow` | 4 个调用方 BC 全部 `UseGrpc=true` | 完整下单流程通过 |
| 2 | `AllBc_PartialGrpcDown_FallbacksWork` | 部分下游 gRPC 不可用 | 各调用方独立降级，业务流程不中断 |
| 3 | `AllBc_ConsulHotSwitch_AllBcSwitchToHttp` | Consul KV 全部切回 false | 4 个调用方 1-2 秒内切回 HttpClient |

### 8.5 测试基础设施

新建 `tests/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/TestHelpers/`：

- `FakeAntiCorruptionService.cs`：实现 `IXxxAntiCorruptionService` 接口的 mock 类，可控返回值与异常
- `TestCircuitBreakerState.cs`：暴露内部状态，便于断言
- `GrpcServerFixture.cs`：启动内存 gRPC 服务端，注入 mock `IXxxInternalQueryService`

```csharp
public sealed class GrpcServerFixture : IDisposable
{
    public string Endpoint { get; }
    private readonly Server _server;

    public GrpcServerFixture(IProductInternalQueryService queryService)
    {
        _server = new Server
        {
            Services = { ProductInternalService.BindService(new ProductGrpcService(
                queryService,
                NullLogger<ProductGrpcService>.Instance)) },
            Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
        };
        _server.Start();
        Endpoint = $"localhost:{_server.Ports.Single().BoundPort}";
    }

    public void Dispose()
    {
        _server.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
    }
}
```

### 8.6 既有测试影响

| 既有测试 | 影响 | 处理方式 |
|---|---|---|
| 9 个 HttpClient 防腐层单元测试 | 无（HttpClient 实现不变） | 保留 |
| 7 个 `Internal*ControllerTests.cs` | 无（双轨期保留 Internal REST） | 保留 |
| 4 个 `IXxxInternalQueryService` 测试 | 无（接口不变，仅扩展 .proto） | 保留 |
| Order/Notification/Cart/ReviewAfterSales 集成测试 | 需补充 `UseGrpc=true` 场景 | 新增测试方法 |

### 8.7 覆盖率要求

| 层 | 覆盖率门槛 | 本次新增代码预期 |
|---|---|---|
| Domain | ≥ 80% | 不涉及 |
| Application | ≥ 60% | 不涉及 |
| Infrastructure | ≥ 40% | 新增 CircuitBreakerState/Dispatcher/Interceptor 覆盖率 ≥ 80% |
| Api | 无强制 | 新增 GrpcService 覆盖率 ≥ 60% |

### 8.8 验收清单

#### 8.8.1 阶段 0 验收

- [ ] `dotnet build Leno.sln` 0 错误 0 警告
- [ ] `buf lint` + `buf breaking` 通过
- [ ] `buf generate` 生成 11 个 BC 的 C# 客户端代码
- [ ] CircuitBreakerState 单元测试 8 个全部 PASS
- [ ] GrpcInternalKeyInterceptor 单元测试 4 个全部 PASS
- [ ] AntiCorruptionDispatcher 单元测试 10 个全部 PASS
- [ ] ConsulConfigWatcher 集成测试 3 个全部 PASS
- [ ] 覆盖率 ≥ 80%

#### 8.8.2 阶段 1 (POC) 验收

- [ ] Product.Api 启动后 gRPC 端点可调
- [ ] Order BC `UseGrpc=true` 后通过 gRPC 调用 Product 成功
- [ ] 熔断降级机制验证：手动停 Product gRPC 后 Order 自动降级到 HttpClient
- [ ] gRPC 鉴权验证：无 `x-internal-key` 的调用被拒绝
- [ ] GrpcProductAntiCorruptionClient 单元测试 6 个全部 PASS
- [ ] AntiCorruptionDispatcherIntegrationTests 4 个全部 PASS
- [ ] OrderSagaGrpcFallbackE2eTests 3 个全部 PASS
- [ ] 1 周灰度观察期指标达标

#### 8.8.3 阶段 4 全量验收

- [ ] 10 个防腐层全部 gRPC 化
- [ ] 4 个调用方 BC `UseGrpc=true` 稳定运行 1 周
- [ ] 阶段 4 全量 E2E 测试 3 个全部 PASS
- [ ] 熔断降级触发率 < 1%/天
- [ ] gRPC 调用成功率 ≥ 99.9%

#### 8.8.4 阶段 5 稳定运行验收

- [ ] 4 周稳定运行，熔断降级触发率 < 0.1%
- [ ] gRPC P99 延迟 < 10ms
- [ ] HttpClient P99 延迟（降级时） < 50ms
- [ ] OrderSaga 下单 P99 < 20ms
- [ ] 业务错误率 = 0

---

## 9. 风险与运维

### 9.1 风险登记册

| # | 风险 | 概率 | 影响 | 缓解措施 | 监控指标 |
|---|---|---|---|---|---|
| R1 | .proto 扩展破坏既有客户端 | 低 | 高 | `buf breaking` CI 校验 + 仅新增字段 | CI 通过率 |
| R2 | gRPC 服务端启动失败 | 中 | 中 | Kestrel HTTP/2 配置错误时立即回滚 `UseGrpc=false` | 服务启动成功率 |
| R3 | Consul KV 配置错误或不可达 | 中 | 中 | `ValidateSensitiveConfig` 启动期校验 + warning 降级 + appsettings.json 兜底默认值 | Consul KV 读失败率 |
| R4 | 熔断器状态丢失（服务重启） | 高 | 低 | 重启后从 Closed 开始，最多损失 30 秒历史数据 | 服务重启次数 |
| R5 | gRPC 客户端 Channel 泄漏 | 低 | 中 | `AddGrpcClient<T>` 工厂管理 Channel 池，自动 dispose | gRPC 连接数 |
| R6 | 双轨期配置漂移 | 中 | 中 | Consul KV 单一真相源 + `IOptionsMonitor` 热更新 + 启动期校验 | 配置一致性检查 |
| R7 | 业务异常被误判为不可用 | 低 | 高 | `IsGrpcUnavailable` 严格限定 4 个 StatusCode | 误降级次数 |
| R8 | gRPC 与 HttpClient 返回值不一致 | 中 | 高 | 双轨实现共用 DTO 映射单元测试 + 阶段 1 POC 1 周观察期对比 | 返回值差异告警 |
| R9 | ConsulConfigWatcher 长轮询阻塞 | 低 | 中 | 5 分钟超时 + 异常重试 10 秒间隔 | Watcher 心跳 |
| R10 | gRPC 调用链路追踪缺失 | 中 | 中 | `AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation` 已订阅，需补充 gRPC instrumentation | Jaeger trace 完整度 |
| R11 | 灰度切换期间请求丢失 | 低 | 高 | `IOptionsMonitor` 原子读取，Dispatcher 内每次请求独立判断 | 切换期错误率 |
| R12 | 10 个防腐层 gRPC 适配器重复代码 | 高 | 低 | 抽取 `GrpcAntiCorruptionClientBase` 公共方法 + metadata 构造辅助方法 | 代码重复率 |

### 9.2 Prometheus 指标

| 指标 | 类型 | 标签 | 说明 |
|---|---|---|---|
| `anticorruption_failure_total` | Counter | service, error_code, path | 防腐层失败计数（既有，加 path 标签） |
| `anticorruption_fallback_total` | Counter | service, reason | 降级次数（新增） |
| `anticorruption_circuit_open` | Gauge | service | 熔断是否打开（1=Open，0=Closed/HalfOpen） |
| `anticorruption_grpc_request_total` | Counter | service, status_code | gRPC 调用计数（新增） |
| `anticorruption_grpc_duration_seconds` | Histogram | service, status_code | gRPC 调用延迟分布（新增） |

### 9.3 Grafana 仪表板

文件：`grafana/leno-anticorruption-dashboard.json`（新建）

| # | 面板 | 类型 | 查询 |
|---|---|---|---|
| 1 | gRPC 调用成功率 | Stat | `1 - rate(anticorruption_failure_total{path="grpc"}[5m]) / rate(anticorruption_grpc_request_total[5m])` |
| 2 | 降级触发率 | Stat | `rate(anticorruption_fallback_total[5m])` |
| 3 | 熔断状态 | Stat | `anticorruption_circuit_open` |
| 4 | gRPC P99 延迟 | Graph | `histogram_quantile(0.99, rate(anticorruption_grpc_duration_seconds_bucket[5m]))` |
| 5 | 各服务降级分布 | Pie | `sum by (service) (anticorruption_fallback_total)` |
| 6 | 降级原因分布 | Pie | `sum by (reason) (anticorruption_fallback_total)` |

### 9.4 Alertmanager 告警规则

文件：`grafana/provisioning/alerting/leno-anticorruption-alerts.yml`（新建）

```yaml
groups:
  - name: anticorruption
    rules:
      - alert: AntiCorruptionHighFallbackRate
        expr: |
          sum(rate(anticorruption_fallback_total[5m])) by (service)
          / sum(rate(anticorruption_grpc_request_total[5m])) by (service) > 0.05
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "AntiCorruption {{ $labels.service }} gRPC fallback rate > 5%"
          description: "gRPC 降级到 HTTP 的比例超过 5%，持续 10 分钟"

      - alert: AntiCorruptionCircuitOpenLong
        expr: anticorruption_circuit_open == 1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "AntiCorruption {{ $labels.service }} circuit breaker open > 5min"
          description: "熔断器持续打开超过 5 分钟，gRPC 服务端可能不可用"

      - alert: AntiCorruptionGrpcFailureSpike
        expr: |
          sum(rate(anticorruption_failure_total{path="grpc"}[5m])) by (service) > 0.1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "AntiCorruption {{ $labels.service }} gRPC failure rate > 10%"
```

### 9.5 灾备与高可用

| 组件 | 单点风险 | 高可用方案 |
|---|---|---|
| Consul KV | 配置中心不可达 | 3 节点 Consul 集群 + appsettings.json 兜底默认值 |
| gRPC 服务端 | 单 Pod 不可用 | Kubernetes Deployment replicas ≥ 2 + HPA |
| 熔断器状态 | 进程内状态，重启丢失 | 可接受（重启后从 Closed 开始，最多 30 秒历史损失） |
| GrpcChannel | 连接池耗尽 | `AddGrpcClient<T>` 工厂管理，自动复用 |
| ConsulConfigWatcher | 长轮询阻塞 | 5 分钟超时 + 异常重试 10 秒间隔 |

### 9.6 故障演练计划

| 演练场景 | 触发方式 | 预期行为 | 频率 |
|---|---|---|---|
| gRPC 服务端不可用 | `kubectl scale deployment leno-product-api --replicas=0` | 熔断 3 次后 Open，降级到 HttpClient | 每月 1 次 |
| Consul 不可用 | `docker stop consul` | 服务使用 appsettings.json 兜底配置运行 | 每月 1 次 |
| 网络分区 | NetworkPolicy 阻止 HTTP/2 | gRPC 调用 Unavailable，熔断降级 | 每季度 1 次 |
| 配置错误 | Consul KV 写入错误端点 | gRPC 调用失败，熔断降级到 HttpClient | 每季度 1 次 |

### 9.7 退出标准

#### 9.7.1 阶段 5 结束后评估

| 指标 | 目标 | 评估方式 |
|---|---|---|
| 4 周稳定运行 | 0 次回滚 | 部署记录 |
| 熔断降级触发率 | < 0.1% | Prometheus 查询 |
| gRPC P99 延迟 | < 10ms | Grafana 仪表板 |
| 业务错误率 | 0 | Application Insights |
| OrderSaga P99 | < 20ms | Jaeger trace |

#### 9.7.2 后续优化方向（不在本次范围）

- **Task 11 评估**：4 周稳定后评估是否下线 HttpClient 代码与 Internal REST 控制器
- **mTLS 升级**：从 metadata header 鉴权升级到双向 TLS 证书认证
- **服务发现集成**：从静态 Consul KV 升级到 Consul 服务发现动态解析
- **gRPC 流式调用**：批量场景从 unary 升级到 server streaming
- **跨集群 gRPC**：多区域部署时 gRPC 调用的延迟优化

---

## 10. 附录

### 10.1 既有代码定位

| 位置 | 路径 | 关键发现 |
|---|---|---|
| ReadModelSyncConsumerBase | `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs` | 抽象基类，仅支持索引场景 |
| AntiCorruptionBase | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs` | HttpClient 模式基类 |
| GrpcAntiCorruptionClientBase | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs` | gRPC 模式基类（已实现） |
| AntiCorruptionOptions | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs` | 已含 UseGrpc 开关 |
| AntiCorruptionMetrics | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs` | 已含 RecordFailure，需扩展 |
| AntiCorruptionPollyExtensions | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs` | HttpClient Polly 策略链 |
| 11 个 .proto 文件 | `src/BuildingBlocks/Leno.SharedContracts/Protos/*.proto` | 已生成，4 个需扩展 |
| buf.yaml / buf.gen.yaml | `src/BuildingBlocks/Leno.SharedContracts/` | 已就绪 |
| Leno.SharedContracts.Grpc.csproj | `src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj` | 仅 _Placeholder.cs，未运行 buf generate |
| 9-10 个防腐层服务 | `src/Services/*/Leno.*.Infrastructure/Services/` | 全部继承 AntiCorruptionBase |
| 4 个 IXxxInternalQueryService | `src/Services/*/Leno.*.Application/I*InternalQueryService.cs` | User/Product/Payment/Order BC 存在 |
| 11 个 BC.Api Program.cs | `src/Services/*/Leno.*.Api/Program.cs` | 均未调用 AddGrpc/MapGrpcService |
| BffForwarderService | `src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs` | 不纳入 gRPC 迁移 |
| WebApplicationExtensions | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` | AddLenoApi 内已含 gRPC 公共注册 |

### 10.2 10 个防腐层完整清单

| # | 类名 | 文件路径 | 继承 AntiCorruptionBase | 调用下游 BC | 是否迁移 |
|---|---|---|---|---|---|
| 1 | ProductAntiCorruptionService | `src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs` | ✓ | Product | ✓ |
| 2 | PromotionAntiCorruptionService | `src/Services/Order/Leno.Order.Infrastructure/Services/PromotionAntiCorruptionService.cs` | ✓ | Promotion | ✓ |
| 3 | PointsAntiCorruptionService | `src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs` | ✓ | PointsMembership | ✓ |
| 4 | LogisticsTrackingService | `src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs` | ✓ | 第三方物流 | ✗（不迁移） |
| 5 | UserContactAntiCorruptionService | `src/Services/Notification/Leno.Notification.Infrastructure/Services/UserContactAntiCorruptionService.cs` | ✓ | UserAuth | ✓ |
| 6 | CartPriceService | `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs` | ✓ | Product | ✓ |
| 7 | ProductSnapshotAntiCorruptionService | `src/Services/Cart/Leno.Cart.Infrastructure/Services/` | ✓ | Product | ✓ |
| 8 | PaymentInfoQueryService | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs` | ✓ | Payment | ✓ |
| 9 | AfterSalesEligibilityChecker | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs` | ✓ | Order | ✓ |
| 10 | ReviewEligibilityChecker | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs` | ✓ | Order | ✓ |

### 10.3 术语表

| 术语 | 说明 |
|---|---|
| 双轨 | HttpClient 与 gRPC 两种实现同时存在，由 Dispatcher 选择 |
| 熔断器 | 三状态机（Closed/Open/HalfOpen），防止 gRPC 故障级联 |
| 降级 | gRPC 调用失败时自动切换到 HttpClient |
| 灰度切换 | 通过 Consul KV `UseGrpc` 开关单 BC 独立控制 |
| 半开放探测 | 熔断 30 秒后允许一次 gRPC 探测，验证服务恢复 |
| metadata header | gRPC 调用附带的元数据头，本次用于 `x-internal-key` 鉴权 |
| ALPN | Application-Layer Protocol Negotiation，HTTP/2 协商机制 |
| POC | Proof of Concept，本次为 Order → Product 单防腐层验证 |

### 10.4 变更历史

| 版本 | 日期 | 变更 |
|---|---|---|
| v1.0 | 2026-07-19 | 初版：混合策略 + 双轨调度器 + 5 阶段迁移计划 |
