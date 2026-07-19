# 防腐层模式（M4 gRPC 双轨方案）

> 本文档描述 Leno 平台跨 BC 同步调用的防腐层（Anti-Corruption Layer, ACL）模式，基于 M4 gRPC 双轨方案实际代码梳理。
> 落地日期：2026-07-19（Plan 8 M4.3 Task 26 文档收尾）。
> 配套契约：`docs/contracts/internal-api-contracts.md` 第 9 节；编码规范：`docs/编码规范.md` 第 18 节；Runbook：`docs/runbooks/m4-grpc-poc-verification.md`。

---

## 1 模式概述

跨 BC 同步调用通过防腐层隔离，支持 HTTP 与 gRPC **双轨运行**，由 `AntiCorruptionDispatcher<TService>` 在运行时根据 `UseGrpc` 开关与熔断器状态选择传输方式。

**核心目标**：

- **隔离**：业务层仅依赖 `IXxxAntiCorruptionService` 接口，对底层传输方式（HttpClient/gRPC）无感。
- **双轨**：HttpClient 与 gRPC 同时存在，gRPC 不可用时自动降级到 HttpClient。
- **熔断**：连续失败触发熔断，避免雪崩；半开放探测恢复。
- **热更新**：`UseGrpc` 开关通过 Consul KV 热更新，1-2 秒生效，无需重启。

**关键决策**：

- **HttpClient 与 gRPC 分离为两个类**（而非 Plan 期设想的单一类内部 `UseGrpc` 分支），便于单一职责与测试隔离。
- **Dispatcher 不实现 TService 接口**，仅提供 `ExecuteAsync<TResult>` 模板方法；由 `{Service}DispatcherAdapter` 适配器实现 TService 接口委托 Dispatcher。

---

## 2 组件清单（基于实际代码）

| 组件 | 职责 | 源文件 |
|---|---|---|
| `AntiCorruptionBase` | HttpClient 模式基类，统一 try/catch + Metrics + HTTP 状态码映射 | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs` |
| `GrpcAntiCorruptionClientBase` | gRPC 模式基类，保留 `RpcException` 作为 `AntiCorruptionException.InnerException` | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs` |
| `AntiCorruptionDispatcher<TService>` | 双轨调度器，含熔断器与降级逻辑（**不实现 TService 接口**） | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs` |
| `{Service}DispatcherAdapter` | 适配器，实现 TService 接口，委托 Dispatcher | 各 BC `Infrastructure/Services/Grpc/` |
| `CircuitBreakerState` | 三状态机（Closed/Open/HalfOpen），线程安全 | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs` |
| `GrpcInternalKeyInterceptor` | 服务端鉴权拦截器，校验 metadata `x-internal-key` | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcInternalKeyInterceptor.cs` |
| `ConsulConfigWatcher` | 配置热更新后台服务，长轮询 Consul KV | `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs` |
| `AntiCorruptionMetrics` | Prometheus 指标（Meter 名 `Leno.AntiCorruption`） | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs` |
| `AntiCorruptionOptions` | 配置选项（`UseGrpc`/`GrpcEndpoints`/`TargetInternalApiKeys`/`CircuitBreaker`/`InternalApiKey`/`ServiceName`） | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs` |

### 2.1 HttpClient 模式组件

- `AntiCorruptionBase`：所有 HttpClient 防腐层基类，提供 `ExecuteAsync` 模板方法。
- `{BC}AntiCorruptionService` / `{功能}Service` / `{功能}Provider`：具体 HttpClient 实现，如 `ProductAntiCorruptionService`、`CartPriceService`、`HttpOrderStatusProvider`。
- `AddAntiCorruptionPolicies()`：Polly 策略链扩展方法（重试 + 熔断 + Timeout），通过 `AddHttpClient<T>().AddAntiCorruptionPolicies()` 注入。

### 2.2 gRPC 模式组件

- `GrpcAntiCorruptionClientBase`：所有 gRPC 防腐层基类，提供 `ExecuteAsync` 模板方法（与 `AntiCorruptionBase` 错误处理策略一致）。
- `Grpc{BC}AntiCorruptionClient` / `Grpc{功能}Provider` / `Grpc{功能}Service`：具体 gRPC 实现，如 `GrpcProductAntiCorruptionClient`、`GrpcOrderStatusProvider`、`GrpcCartPriceService`。
- `{BC}GrpcService`：服务端实现，继承 `{BC}InternalService.{BC}InternalServiceBase`，复用 `IXxxInternalQueryService`。

### 2.3 调度与适配组件

- `AntiCorruptionDispatcher<TService>`：双轨调度核心，持有 HttpClient 实现、gRPC 实现、`CircuitBreakerState`、`IOptionsMonitor<AntiCorruptionOptions>`。
- `{Service}DispatcherAdapter`：TService 接口的具体实现，委托 Dispatcher。

---

## 3 调度流程

### 3.1 调用入口

业务层通过 DI 注入 `IXxxAntiCorruptionService`，实际解析到 `{Service}DispatcherAdapter`，调用流向：

```
业务层
  │ IProductAntiCorruptionService.GetSkuInfoAsync(skuId, ct)
  ▼
ProductAntiCorruptionDispatcherAdapter
  │ _dispatcher.ExecuteAsync(s => s.GetSkuInfoAsync(skuId, ct), ct)
  ▼
AntiCorruptionDispatcher<IProductAntiCorruptionService>.ExecuteAsync
  │ 1. 读取 IOptionsMonitor.CurrentValue.UseGrpc
  │ 2. 检查 CircuitBreakerState.GetState()
  │ 3. 选择实现：HttpClient / gRPC
  ▼
ProductAntiCorruptionService (HttpClient)  或  GrpcProductAntiCorruptionClient (gRPC)
```

### 3.2 决策矩阵

| UseGrpc | gRPC 实现 | 熔断状态 | 选择实现 |
|---|---|---|---|
| `false` | 任意 | 任意 | HttpClient |
| `true` | `null` | 任意 | HttpClient |
| `true` | 已注册 | `Closed` | gRPC |
| `true` | 已注册 | `HalfOpen` | gRPC（探测） |
| `true` | 已注册 | `Open` | HttpClient（直接降级） |

### 3.3 降级流程

1. gRPC 调用失败（`Unavailable`/`DeadlineExceeded`/`Internal`/`ResourceExhausted`）。
2. `GrpcAntiCorruptionClientBase.ExecuteAsync` 捕获 `RpcException`，包装为 `AntiCorruptionException`（保留 `RpcException` 作为 `InnerException`），埋点 `RecordFailure` + `RecordGrpcRequest`。
3. `AntiCorruptionDispatcher` 捕获 `AntiCorruptionException`，调用 `IsGrpcUnavailable(ex)` 判断 `InnerException is RpcException` 且 StatusCode 属于不可用分类。
4. `CircuitBreakerState.RecordFailure()` 累计失败次数。
5. **若本次失败导致状态从 Closed 切到 Open**：本次调用直接抛 `AntiCorruptionException`（不降级），下次调用开始降级。
6. **若熔断未触发**（失败次数未达阈值）：本次调用降级到 HttpClient，调用 `operation(_httpImplementation)`。
7. 熔断 Open 期间（默认 30 秒）：所有 gRPC 调用直接降级到 HttpClient，不调 gRPC。
8. 30 秒后切 HalfOpen：下次调用尝试 gRPC 探测。
9. HalfOpen 期间连续 `SuccessThreshold`（默认 2 次）成功后切 Closed，恢复 gRPC 优先；任一失败切 Open。

### 3.4 业务异常处理

- `NotFound`/`PermissionDenied`/`InvalidArgument`/`AlreadyExists` 等业务异常的 `RpcException` **不会**被 `IsGrpcUnavailable` 判定为不可用。
- 这些异常在 `GrpcAntiCorruptionClientBase.ExecuteAsync` 中被包装为 `AntiCorruptionException`（`ErrorCode = {SERVICE}_REMOTE_FAILED`），但 `InnerException` 仍为 `RpcException`。
- `AntiCorruptionDispatcher` 的 `catch (AntiCorruptionException ex) when (IsGrpcUnavailable(ex))` 不匹配，异常直接向上抛出。
- **不触发熔断失败计数**，**不降级到 HttpClient**。

---

## 4 监控指标（`AntiCorruptionMetrics`）

所有指标由 `Leno.AntiCorruption` Meter 发布，各 BC 启动时通过 `AddLenoOpenTelemetry` 回调 `.AddMeter("Leno.AntiCorruption")` 订阅。

| 指标名 | 类型 | 标签 | 含义 |
|---|---|---|---|
| `anticorruption_failure_total` | Counter | `service`、`operation`、`path` | 防腐层远程调用失败次数（`path=http` 或 `grpc`） |
| `anticorruption_fallback_total` | Counter | `service`、`reason` | gRPC 降级到 HttpClient 的次数（`reason=circuit_open`/`grpc_Unavailable`/`grpc_DeadlineExceeded`/`grpc_Internal`/`grpc_ResourceExhausted`/`grpc_unknown`） |
| `anticorruption_circuit_open` | Gauge | `service` | 熔断器是否 Open（1=Open，0=Closed/HalfOpen） |
| `anticorruption_grpc_request_total` | Counter | `service`、`status_code` | gRPC 调用计数（`status_code=OK`/`Unavailable`/`DeadlineExceeded`/`Internal`/`ResourceExhausted`/`NotFound`/...） |
| `anticorruption_grpc_duration_seconds` | Histogram | `service`、`status_code` | gRPC 调用延迟分布（秒） |

**关联查询示例（Prometheus）**：

- gRPC 调用成功率：`1 - sum(rate(anticorruption_grpc_request_total{status_code!="OK"}[5m])) / sum(rate(anticorruption_grpc_request_total[5m]))`
- 熔断 Open 实时状态：`anticorruption_circuit_open{service="product"}`
- 降级原因分布：`sum by (reason) (rate(anticorruption_fallback_total{service="product"}[5m]))`

---

## 5 DI 注册模式

每个调用方 BC 的 `{BC}.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 按以下模式注册双轨防腐层：

```csharp
// 1. HttpClient 实现（始终注册，作为降级备份）
services.AddHttpClient<ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl))
    .AddAntiCorruptionPolicies();

// 2. UseGrpc=true 时注册 gRPC 链路
var antiCorruptionOptions = configuration.GetSection("AntiCorruption").Get<AntiCorruptionOptions>() ?? new();
if (antiCorruptionOptions.UseGrpc)
{
    // 2.1 gRPC 客户端工厂（Grpc.Net.Client）
    var productGrpcEndpoint = antiCorruptionOptions.GrpcEndpoints.GetValueOrDefault("Product")
        ?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Product 配置缺失");
    services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(options =>
    {
        options.Address = new Uri(productGrpcEndpoint);
    });
    services.AddScoped<GrpcProductAntiCorruptionClient>();

    // 2.2 熔断器 Keyed Singleton（serviceName 与 Metrics 标签一致）
    services.AddKeyedSingleton<CircuitBreakerState>("product", (sp, _) =>
    {
        var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
        var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
        return new CircuitBreakerState(
            "product",
            cbOpts.FailureThreshold,
            cbOpts.SuccessThreshold,
            TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
    });

    // 2.3 Dispatcher（Scoped，组合 HttpClient + gRPC + 熔断器 + IOptionsMonitor）
    services.AddScoped<AntiCorruptionDispatcher<IProductAntiCorruptionService>>(sp =>
    {
        var httpImpl = sp.GetRequiredService<ProductAntiCorruptionService>();
        var grpcImpl = sp.GetService<GrpcProductAntiCorruptionClient>();
        var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
        var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IProductAntiCorruptionService>>>();
        var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
        return new AntiCorruptionDispatcher<IProductAntiCorruptionService>(
            httpImpl, grpcImpl, options, logger, "product", cb);
    });

    // 2.4 适配器作为 TService 的具体实现
    services.AddScoped<ProductAntiCorruptionDispatcherAdapter>();
    services.AddScoped<IProductAntiCorruptionService>(sp =>
        sp.GetRequiredService<ProductAntiCorruptionDispatcherAdapter>());
}
else
{
    // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
    services.AddScoped<IProductAntiCorruptionService>(sp =>
        sp.GetRequiredService<ProductAntiCorruptionService>());
}
```

**DI 生命周期**：

| 组件 | 生命周期 | 说明 |
|---|---|---|
| `{BC}AntiCorruptionService`（HttpClient 实现） | Transient（由 `AddHttpClient` 隐式注册） | HttpClientFactory 管理 `HttpMessageHandler` 池 |
| `Grpc{BC}AntiCorruptionClient` | Scoped | 每次 DI 范围新建，复用 gRPC 连接池 |
| `CircuitBreakerState` | Keyed Singleton | 跨请求累积失败计数，serviceName 为 key |
| `AntiCorruptionDispatcher<TService>` | Scoped | 组合多个 Scoped 依赖 |
| `{Service}DispatcherAdapter` | Scoped | 每次 DI 范围新建 |
| `IXxxAntiCorruptionService` | Scoped | 解析到 Adapter（UseGrpc=true）或 HttpClient 实现（UseGrpc=false） |

---

## 6 熔断器状态机（`CircuitBreakerState`）

### 6.1 状态定义

```csharp
public enum CircuitState
{
    Closed,    // 正常：gRPC 优先，累计失败计数
    Open,      // 熔断：直接降级 HttpClient，不调 gRPC
    HalfOpen   // 半开放：下次调用走 gRPC 探测
}
```

### 6.2 状态转移

```
Closed（_consecutiveFailures < FailureThreshold）
    │ RecordFailure() → _consecutiveFailures++ ≥ FailureThreshold(3)
    │ _openedAt = DateTime.UtcNow
    ▼
Open（DateTime.UtcNow - _openedAt < OpenDuration(30s)）
    │ GetState() 返回 Open
    │ DateTime.UtcNow - _openedAt ≥ OpenDuration(30s)
    ▼
HalfOpen
    │ RecordSuccess() → _halfOpenSuccesses++ ≥ SuccessThreshold(2)
    │ → ResetToClosed()
    ▼
Closed（_consecutiveFailures=0, _halfOpenSuccesses=0, _openedAt=DateTime.MinValue）

HalfOpen
    │ RecordFailure() → _halfOpenSuccesses=0
    │ _consecutiveFailures++ ≥ FailureThreshold(3) → _openedAt 重置
    ▼
Open（继续熔断 30s）
```

### 6.3 关键约束

- **线程安全**：所有状态读写通过 `lock(_lock)` 串行化，避免并发计数错乱。
- **业务异常不计入失败计数**：`AntiCorruptionDispatcher` 仅在 `IsGrpcUnavailable(ex) == true` 时调用 `RecordFailure()`，业务异常直接抛出不进入 catch 分支。
- **熔断触发瞬间本次调用直接抛**：若 `RecordFailure()` 后状态切到 Open（`GetState() == CircuitState.Open`），本次调用抛 `AntiCorruptionException`，**下次调用开始降级**。
- **状态变更同步推送到 Metrics**：`UpdateMetrics()` 调用 `AntiCorruptionMetrics.UpdateCircuitOpenState(serviceName, isOpen)` 更新 Gauge。
- **Dispose 时清理 Metrics**：`CircuitBreakerState.Dispose()` 调用 `UpdateCircuitOpenState(serviceName, false)` 避免残留。

### 6.4 配置参数

```csharp
public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; init; } = 3;       // 连续失败次数阈值
    public int SuccessThreshold { get; init; } = 2;       // HalfOpen 连续成功次数阈值
    public int OpenDurationSeconds { get; init; } = 30;   // Open 状态持续时间（秒）
}
```

通过 `AntiCorruption:CircuitBreaker` 配置节绑定，`IOptionsMonitor` 推送热更新（注：Keyed Singleton 实例创建后参数不可变，热更新仅影响新建实例）。

---

## 7 Consul 配置热更新（`ConsulConfigWatcher`）

### 7.1 工作机制

- **后台服务**：`ConsulConfigWatcher` 继承 `BackgroundService`，应用启动时自动启动。
- **长轮询**：调用 `IConsulClient.KV.Get(key, QueryOptions { WaitIndex, WaitTime })`，无变更时阻塞 5 分钟返回。
- **变更检测**：`queryResult.LastIndex != waitIndex` 表示 KV 有变更，更新 `IConfiguration["AntiCorruption:UseGrpc"]`。
- **WaitIndex 推进**：每次成功处理后更新 `waitIndex = queryResult.LastIndex`。

### 7.2 关键参数

```csharp
private const string UseGrpcKeyPrefix = "leno/anticorruption/use-grpc/";
private static readonly TimeSpan WaitTime = TimeSpan.FromMinutes(5);
private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
```

- **KV 路径**：`leno/anticorruption/use-grpc/{BC}`（`{BC}` 从 `Service:Name` 配置读取，如 `order`）。
- **WaitTime**：5 分钟（Consul 长轮询阻塞超时）。
- **RetryDelay**：10 秒（Consul 不可达时重试间隔）。

### 7.3 配置推送链路

```
Consul KV: leno/anticorruption/use-grpc/order = "true"
    │ ConsulConfigWatcher 长轮询检测到变更
    ▼
IConfiguration["AntiCorruption:UseGrpc"] = "true"
    │ IOptionsMonitor<AntiCorruptionOptions> 监听 IConfiguration 变更
    ▼
AntiCorruptionDispatcher.ExecuteAsync 读取 _optionsMonitor.CurrentValue.UseGrpc
    │ 下次调用生效（1-2 秒延迟）
    ▼
选择 gRPC 实现（若已注册且熔断未 Open）
```

### 7.4 异常处理

- **Service:Name 缺失**：日志 warning 并退出 watcher。
- **Consul 不可达**：日志 warning，10 秒后重试，应用继续使用本地配置。
- **取消令牌**：`OperationCanceledException` 时优雅退出。

---

## 8 服务端鉴权（`GrpcInternalKeyInterceptor`）

### 8.1 拦截位置

- 注册于各 BC 的 `Program.cs` 中 `builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcInternalKeyInterceptor>())`。
- 拦截所有 unary RPC 调用，在业务逻辑执行前完成鉴权。

### 8.2 鉴权流程

1. 从 `IOptionsMonitor<AntiCorruptionOptions>.CurrentValue.InternalApiKey` 读取被调用方 BC 的 key。
2. 若 `InternalApiKey` 为空：日志 Error，抛 `RpcException(Unauthenticated, "Internal API key not configured on server")`（fail-closed）。
3. 从 `ServerCallContext.RequestHeaders` 读取 `x-internal-key`（大小写不敏感）。
4. 若缺失或值不匹配：日志 Warning，抛 `RpcException(Unauthenticated, "Invalid or missing x-internal-key")`。
5. 校验通过：`await continuation(request, context)` 继续执行业务逻辑。

### 8.3 客户端配合

- gRPC 客户端在每次调用前通过 `BuildMetadata()` 注入 `x-internal-key`：

  ```csharp
  private Metadata BuildMetadata()
  {
      var metadata = new Metadata();
      var currentOptions = _options.CurrentValue;
      if (currentOptions.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) && !string.IsNullOrEmpty(key))
      {
          metadata.Add("x-internal-key", key);
      }
      return metadata;
  }
  ```

- **`TargetInternalApiKeys` 键名约定**：使用 BC 名（如 `Product`、`Promotion`、`PointsMembership`、`UserAuth`、`Order`、`Payment`、`Notification`），与 `appsettings.json` 配置一致。

### 8.4 与 HTTP 鉴权的关系

| 维度 | HTTP `InternalApiKeyMiddleware` | gRPC `GrpcInternalKeyInterceptor` |
|---|---|---|
| 鉴权对象 | `/internal/` 前缀端点 | 所有 unary gRPC 调用 |
| 鉴权字段 | `X-Internal-Key` 请求头（HTTP/1.1） | `x-internal-key` metadata（HTTP/2） |
| Key 来源 | `InternalApiKeyOptions.Shared` 或 Consul KV | `AntiCorruptionOptions.InternalApiKey`（即本 BC 的 InternalApiKey） |
| 失败响应 | HTTP 401 + `INTERNAL_API_KEY_INVALID` | `RpcException(Unauthenticated)` |
| 关系 | 两者并行独立，双轨期间均生效 | 两者并行独立，双轨期间均生效 |

---

## 9 设计要点与约束

### 9.1 为什么 Dispatcher 不实现 TService 接口？

- `TService` 是业务接口（如 `IProductAntiCorruptionService`），方法签名多样（`Task<T>`、`Task`、不同参数）。
- `AntiCorruptionDispatcher<TService>` 仅提供 `ExecuteAsync<TResult>(Func<TService, Task<TResult>>, ct)` 模板方法，无法统一实现所有业务方法。
- 由 `{Service}DispatcherAdapter` 适配器实现 TService 接口，每个方法委托 Dispatcher，保留类型安全。

### 9.2 为什么熔断触发瞬间本次不降级？

- 避免半开放探测期间被无效请求冲击：若本次失败导致 Open，说明 gRPC 已严重不可用，本次降级可能再次失败，浪费资源。
- 直接抛出 `AntiCorruptionException` 让上层业务感知，下次调用降级到 HttpClient。

### 9.3 为什么 POC 阶段 Guid 用 int64？

- gRPC `int64` 字段比 `string` 节省 wire 字节，性能更优。
- POC 阶段不需要还原原始 Guid，仅用于跨 BC 标识关联。
- 生产化阶段需新增 `string` 字段（如 `sku_id_str = 11`）保持 wire 兼容，逐步迁移。

### 9.4 为什么不使用 Polly 熔断？

- Polly 熔断器基于 `IAsyncPolicy`，难以暴露内部状态（Open/Closed/HalfOpen）给 Prometheus Gauge。
- `CircuitBreakerState` 自定义实现，可直接调用 `UpdateMetrics()` 推送状态。
- Polly 仍用于 HttpClient 链路（`AddAntiCorruptionPolicies()`），gRPC 链路使用自定义熔断器。

---

## 10 相关文档

- 内部 API 契约：`docs/contracts/internal-api-contracts.md`（第 9 节 M4 gRPC 双轨契约）
- 编码规范：`docs/编码规范.md`（第 18 节 gRPC 双轨规范）
- Runbook：`docs/runbooks/m4-grpc-poc-verification.md`（POC 验证步骤）
- 设计文档：`docs/superpowers/specs/2026-07-19-m4-grpc-dual-track-design.md`
- 实施计划：`docs/superpowers/plans/2026-07-19-m4-grpc-dual-track-implementation.md`
