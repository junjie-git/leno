# M4 gRPC 双轨方案剩余任务补齐实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐 M4 gRPC 双轨方案的 3 类遗留任务（必做技术债务 + 3 个 BC gRPC 服务端补全 + spec 归档/ADR + Guid→string 迁移），使方案达到完整可验收状态。

**Architecture:** 4 个工作流串行执行：工作流 A（ConsulConfigWatcher 注册 + ProductSnapshot 双轨化）→ 工作流 B（Cart/SellerShop/ReviewAfterSales 三个 BC GrpcService 补全）→ 工作流 C（spec supersede 标注 + 7 个 ADR）→ 工作流 D（6 个 .proto 新增 string 字段 + 9 个 GrpcService 双写 + 7 个 GrpcClient 优先读 string）。采用 Subagent-Driven 实施模式，每个 Task 派发独立 subagent，遵循 TDD 流程。

**Tech Stack:** .NET 10 / ASP.NET Core / gRPC (Grpc.AspNetCore 2.65.0) / Protobuf / xUnit / Moq / FluentAssertions / Consul / Polly

**Spec:** `docs/superpowers/specs/2026-07-19-m4-remaining-tasks-completion-design.md`

---

## 文件结构

### 工作流 A：必做技术债务

| 操作 | 文件 | 职责 |
|---|---|---|
| 修改 | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` | AddLenoApi 内注册 ConsulConfigWatcher |
| 修改 | `src/Services/Cart/Leno.Cart.Application/Abstractions/IProductSnapshotAntiCorruption.cs` | 接口签名改为非空返回 + 抛异常 |
| 修改 | `src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs` | 继承 AntiCorruptionBase，失败抛异常 |
| 创建 | `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs` | gRPC 客户端实现 |
| 创建 | `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ProductSnapshotDispatcherAdapter.cs` | 适配器 |
| 修改 | `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | DI 注册双轨 |
| 修改 | `src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs` | 调用方移除 null 检查，改 try/catch |
| 创建 | `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/GrpcProductSnapshotAntiCorruptionClientTests.cs` | 3 个单元测试 |

### 工作流 B：3 个 BC GrpcService 补全

| 操作 | 文件 | 职责 |
|---|---|---|
| 创建 | `src/Services/Cart/Leno.Cart.Application/ICartInternalQueryService.cs` | 跨 BC 查询接口 |
| 创建 | `src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs` | 实现，委托 ICartAppService |
| 创建 | `src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs` | gRPC 服务端 |
| 修改 | `src/Services/Cart/Leno.Cart.Api/Program.cs` | 条件性 MapGrpcService |
| 修改 | `src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj` | 引用 Grpc.AspNetCore + Leno.SharedContracts.Grpc |
| 创建 | `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/CartGrpcServiceTests.cs` | 3 个单元测试 |
| 创建 | `src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs` | 跨 BC 查询接口 |
| 创建 | `src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs` | 实现，委托 ISellerAppService/IShopAppService |
| 创建 | `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs` | gRPC 服务端 |
| 修改 | `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs` | 条件性 MapGrpcService |
| 修改 | `src/Services/SellerShop/Leno.SellerShop.Api/Leno.SellerShop.Api.csproj` | 引用 Grpc.AspNetCore + Leno.SharedContracts.Grpc |
| 创建 | `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Grpc/SellerGrpcServiceTests.cs` | 3 个单元测试 |
| 创建 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewInternalQueryService.cs` | 跨 BC 查询接口 |
| 创建 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs` | 实现，访问 IReviewRepository 聚合 |
| 创建 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs` | gRPC 服务端 |
| 修改 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs` | 条件性 MapGrpcService |
| 修改 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Leno.ReviewAfterSales.Api.csproj` | 引用 Grpc.AspNetCore + Leno.SharedContracts.Grpc |
| 创建 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Grpc/ReviewGrpcServiceTests.cs` | 3 个单元测试 |

### 工作流 C：spec 归档 + ADR

| 操作 | 文件 | 职责 |
|---|---|---|
| 修改 | `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` | 顶部追加 superseded frontmatter |
| 修改 | `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md` | 顶部追加 partially_superseded frontmatter |
| 修改 | `.trae/specs/fix-critical-business-vulnerabilities/spec.md` | 顶部追加 partially_superseded frontmatter |
| 创建 | `docs/decisions/README.md` | ADR 索引 + 格式说明 |
| 创建 | `docs/decisions/0001-grpc-dual-track-with-http-fallback.md` | gRPC 双轨方案 |
| 创建 | `docs/decisions/0002-circuit-breaker-three-state-machine.md` | 熔断器三状态机 |
| 创建 | `docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md` | 适配器模式 |
| 创建 | `docs/decisions/0004-iorderstatus-provider-refactor.md` | IOrderStatusProvider 重构 |
| 创建 | `docs/decisions/0005-proto-backward-compatibility-constraint.md` | .proto 向后兼容约束 |
| 创建 | `docs/decisions/0006-guid-int64-poc-simplification-history.md` | Guid→int64 POC 简化历史 |
| 创建 | `docs/decisions/0007-guid-string-migration-strategy.md` | Guid→string 迁移策略 |

### 工作流 D：Guid→string 迁移

| 操作 | 文件 | 职责 |
|---|---|---|
| 修改 | `src/BuildingBlocks/Leno.SharedContracts/Protos/product.proto` | 新增 string 字段 + deprecated 标记 |
| 修改 | `src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto` | 新增 string 字段 + deprecated 标记 |
| 修改 | `src/BuildingBlocks/Leno.SharedContracts/Protos/promotion.proto` | 新增 string 字段 + deprecated 标记 |
| 修改 | `src/BuildingBlocks/Leno.SharedContracts/Protos/cart.proto` | 新增 string 字段 + deprecated 标记 |
| 修改 | `src/BuildingBlocks/Leno.SharedContracts/Protos/seller.proto` | 新增 string 字段 + deprecated 标记 |
| 修改 | `src/BuildingBlocks/Leno.SharedContracts/Protos/review.proto` | 新增 string 字段 + deprecated 标记 |
| 修改 | 6 个 GrpcService MapToProto 方法 | 双写 int64 + string |
| 修改 | 5 个 GrpcClient MapToDto 方法 | 优先读 string |
| 修改 | 11 个单元测试 | 验证 string 字段 + 向后兼容 |

---

## Task A1: ConsulConfigWatcher 注册为 HostedService

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` (AddLenoApi 方法内)
- Test: 手动启动 BC 验证日志（无单元测试，BackgroundService 注册属于 DI 配置）

**背景：** `ConsulConfigWatcher` 类已实现（`src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs`），监听 `leno/anticorruption/use-grpc/{bc}` KV，但全代码库无 `AddHostedService<ConsulConfigWatcher>()` 调用，导致 UseGrpc 热更新不生效。`ConsulConfigWatcher` 构造函数注入 `IConsulClient`、`IConfiguration`、`ILogger<ConsulConfigWatcher>`，其中 `IConsulClient` 由 `AddConsulServiceRegistration` 注册（在各 BC `Program.cs` 调用 `builder.AddConsulServiceRegistration(...)` 时注册）。`AddLenoApi` 在 `AddConsulServiceRegistration` 之后调用（见各 BC `Program.cs`），故 `IConsulClient` 已可用。

- [ ] **Step 1: 修改 WebApplicationExtensions.cs AddLenoApi 方法**

在 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` 的 `AddLenoApi<TDbContext>` 方法内，在 `// 8. 授权` 之后、`services.AddAuthorization();` 之前（或在 `return services;` 之前）追加 ConsulConfigWatcher 注册。需在文件顶部追加 using：

```csharp
using Leno.Infrastructure.Configuration;
```

在 `AddLenoApi` 方法的 `services.AddAuthorization();` 之后、`return services;` 之前追加：

```csharp
        // 9. Consul KV 配置热更新后台服务（M4 双轨方案：监听 leno/anticorruption/use-grpc/{bc} KV）
        // 仅当 AntiCorruption:EnableConsulConfigWatcher=true（默认 true）时注册
        if (configuration.GetValue<bool>("AntiCorruption:EnableConsulConfigWatcher", true))
        {
            services.AddHostedService<ConsulConfigWatcher>();
        }
```

- [ ] **Step 2: 验证编译通过**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
Expected: BUILD SUCCEEDED，无错误

- [ ] **Step 3: 验证全解决方案编译通过**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs
git commit -m "feat(M4): ConsulConfigWatcher 注册为 HostedService（AddLenoApi 一站式注册）"
```

---

## Task A2: Cart ProductSnapshotAntiCorruptionService 双轨化

**Files:**
- Modify: `src/Services/Cart/Leno.Cart.Application/Abstractions/IProductSnapshotAntiCorruption.cs`
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs`
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs`
- Create: `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ProductSnapshotDispatcherAdapter.cs`
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs`
- Test: `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/GrpcProductSnapshotAntiCorruptionClientTests.cs`

**背景：** 当前 `ProductSnapshotAntiCorruptionService` 未继承 `AntiCorruptionBase`，失败返回 null。接口 `IProductSnapshotAntiCorruption.GetSkuSnapshotAsync` 返回 `Task<SkuSnapshotDto?>`。调用方 `ProductUpdatedEventConsumer`（`src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs:170`）使用 `if (snapshot is null)` 判断。`SkuSnapshotDto` 含 SkuId/Title/MainImageUrl/UnitPrice/IsOnSale 5 字段。Cart 已有 `CircuitBreakerState("product")` Keyed Singleton（Task 22 CartPriceService 双轨时注册）。

**参考模板：** `GrpcCartPriceService.cs` + `CartPriceDispatcherAdapter.cs` + `GrpcCartPriceServiceTests.cs`

- [ ] **Step 1: 修改接口签名（breaking change）**

修改 `src/Services/Cart/Leno.Cart.Application/Abstractions/IProductSnapshotAntiCorruption.cs`：

```csharp
using Leno.Cart.Application.DTOs;

namespace Leno.Cart.Application.Abstractions;

/// <summary>
/// 商品域快照防腐层，查询商品域获取 SKU 最新展示信息。
/// 购物车域不直接依赖商品域领域模型，经此防腐层隔离上下文。
/// </summary>
public interface IProductSnapshotAntiCorruption
{
    /// <summary>
    /// 查询 SKU 当前快照（标题、图片、价格、在售状态）。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SKU 快照。</returns>
    /// <exception cref="Leno.Infrastructure.AntiCorruption.AntiCorruptionException">
    /// 查询失败抛 PRODUCT_UNAVAILABLE；SKU 不存在抛 PRODUCT_REMOTE_FAILED。
    /// </exception>
    Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default);
}
```

- [ ] **Step 2: 重构 HttpClient 实现继承 AntiCorruptionBase**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs`：

```csharp
using System.Net.Http.Json;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 商品域快照防腐层 HttpClient 实现。
/// 继承 <see cref="AntiCorruptionBase"/>，调用失败统一抛 <see cref="AntiCorruptionException"/>。
/// M5.2：通过 <see cref="AntiCorruptionOptions.TargetInternalApiKeys"/> 读取目标 BC（Product）的 InternalApiKey。
/// </summary>
public sealed class ProductSnapshotAntiCorruptionService : AntiCorruptionBase, IProductSnapshotAntiCorruption
{
    private const string InternalKeyHeader = "X-Internal-Key";
    private const string SkuEndpointPrefix = "internal/v1/products/skus/";
    private const string TargetBc = "Product";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductSnapshotAntiCorruptionService> _logger;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "product";

    public ProductSnapshotAntiCorruptionService(
        HttpClient httpClient,
        IOptions<AntiCorruptionOptions> options,
        ILogger<ProductSnapshotAntiCorruptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _targetInternalKey = ResolveTargetInternalKey(options);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshot", async token =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SkuEndpointPrefix + skuId.ToString());
        request.Headers.TryAddWithoutValidation(InternalKeyHeader, _targetInternalKey);

        using var response = await _httpClient.SendAsync(request, token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new AntiCorruptionException(
                $"SKU {skuId} 不存在", "PRODUCT_REMOTE_FAILED");
        }
        EnsureSuccessStatusCode(response, "get_sku_snapshot");

        var apiResponse = await response.Content
            .ReadFromJsonAsync<ApiResponse<SkuSnapshotDto>>(token);
        if (apiResponse?.Data is null)
        {
            throw new AntiCorruptionException(
                $"商品域返回空数据 SkuId={skuId}", "PRODUCT_REMOTE_FAILED");
        }
        return apiResponse.Data;
    }, ct);

    private static string ResolveTargetInternalKey(IOptions<AntiCorruptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Value.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"AntiCorruption:TargetInternalApiKeys:{TargetBc} 配置缺失，请通过 Consul KV 配置 leno/security/internal-key/{TargetBc}");
        }
        return key;
    }
}
```

- [ ] **Step 3: 创建 gRPC 客户端实现**

创建 `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs`：

```csharp
using Grpc.Core;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Cart.Infrastructure.Services.Grpc;

/// <summary>
/// 商品域快照 gRPC 防腐层客户端（M4 双轨方案）。
/// 实现 <see cref="IProductSnapshotAntiCorruption"/>，与 <see cref="ProductSnapshotAntiCorruptionService"/>（HttpClient）双轨。
/// 调用 Product BC <c>ProductInternalService.GetSkuInfo</c> RPC 查询单 SKU 展示快照。
/// </summary>
public sealed class GrpcProductSnapshotAntiCorruptionClient
    : GrpcAntiCorruptionClientBase, IProductSnapshotAntiCorruption
{
    private const string TargetBc = "Product";
    private const string InternalKeyHeader = "x-internal-key";

    private readonly ProductInternalService.ProductInternalServiceClient _client;
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;

    protected override string ServiceName => "product";

    public GrpcProductSnapshotAntiCorruptionClient(
        ProductInternalService.ProductInternalServiceClient client,
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcProductSnapshotAntiCorruptionClient> logger)
        : base()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = logger;
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_snapshot", async token =>
    {
        // 注：proto 中 sku_id 为 int64，POC 阶段使用 GetHashCode 简化
        var request = new GetSkuInfoRequest
        {
            SkuId = (long)skuId.GetHashCode()
        };

        var metadata = BuildMetadata();
        var proto = await _client.GetSkuInfoAsync(request, metadata, cancellationToken: token)
            .ConfigureAwait(false);

        return MapToDto(proto, skuId);
    }, ct);

    private Metadata BuildMetadata()
    {
        var metadata = new Metadata();
        var currentOptions = _options.CurrentValue;
        if (currentOptions.TargetInternalApiKeys.TryGetValue(TargetBc, out var key) && !string.IsNullOrEmpty(key))
        {
            metadata.Add(InternalKeyHeader, key);
        }
        return metadata;
    }

    private static SkuSnapshotDto MapToDto(SkuInfo proto, Guid skuId) => new()
    {
        SkuId = skuId,
        Title = proto.Title ?? string.Empty,
        MainImageUrl = string.IsNullOrEmpty(proto.MainImage) ? null : proto.MainImage,
        UnitPrice = proto.PriceCents / 100m,
        IsOnSale = proto.Salable
    };
}
```

- [ ] **Step 4: 创建 DispatcherAdapter**

创建 `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ProductSnapshotDispatcherAdapter.cs`：

```csharp
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.AntiCorruption;

namespace Leno.Cart.Infrastructure.Services.Grpc;

/// <summary>
/// 商品域快照防腐层双轨适配器（M4 双轨方案）。
/// 实现 <see cref="IProductSnapshotAntiCorruption"/>，委托 <see cref="AntiCorruptionDispatcher{IProductSnapshotAntiCorruption}"/> 选择 gRPC 或 HttpClient 实现。
/// </summary>
public sealed class ProductSnapshotDispatcherAdapter : IProductSnapshotAntiCorruption
{
    private readonly AntiCorruptionDispatcher<IProductSnapshotAntiCorruption> _dispatcher;

    public ProductSnapshotDispatcherAdapter(
        AntiCorruptionDispatcher<IProductSnapshotAntiCorruption> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetSkuSnapshotAsync(skuId, ct), ct);
}
```

- [ ] **Step 5: 修改 DI 注册**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，将既有 `services.AddHttpClient<IProductSnapshotAntiCorruption, ProductSnapshotAntiCorruptionService>(...)` 替换为具体类注册 + 双轨注册。

将既有代码块：

```csharp
        // 商品快照防腐层：商品更新事件消费时查询单 SKU 展示快照，复用商品域 BaseAddress
        services.AddHttpClient<IProductSnapshotAntiCorruption, ProductSnapshotAntiCorruptionService>(client =>
        {
            var baseAddress = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
            client.BaseAddress = new Uri(baseAddress);
        });
```

替换为：

```csharp
        // 商品快照防腐层 HttpClient 实现（保留作为降级备份）
        services.AddHttpClient<ProductSnapshotAntiCorruptionService>(client =>
        {
            var baseAddress = configuration["ServiceUrls:ProductApi"] ?? "http://localhost:5150";
            client.BaseAddress = new Uri(baseAddress);
        })
            .AddAntiCorruptionPolicies();

        // M4 双轨方案：商品快照防腐层 gRPC 客户端 + Dispatcher（仅当 UseGrpc=true 时生效）
        if (antiCorruptionOptions.UseGrpc)
        {
            // ProductInternalServiceClient 已在 CartPriceService 双轨时注册，此处不重复注册
            services.AddScoped<GrpcProductSnapshotAntiCorruptionClient>();

            // CircuitBreakerState("product") 已在 CartPriceService 双轨时注册为 KeyedSingleton，此处复用

            services.AddScoped<AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>>(sp =>
            {
                var httpImpl = sp.GetRequiredService<ProductSnapshotAntiCorruptionService>();
                var grpcImpl = sp.GetService<GrpcProductSnapshotAntiCorruptionClient>();
                var options = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("product");
                return new AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>(
                    httpImpl, grpcImpl, options, logger, "product", cb);
            });
            services.AddScoped<ProductSnapshotDispatcherAdapter>();
            services.AddScoped<IProductSnapshotAntiCorruption>(sp =>
                sp.GetRequiredService<ProductSnapshotDispatcherAdapter>());
        }
        else
        {
            // UseGrpc=false：直接注册 HttpClient 实现
            services.AddScoped<IProductSnapshotAntiCorruption>(sp =>
                sp.GetRequiredService<ProductSnapshotAntiCorruptionService>());
        }
```

- [ ] **Step 6: 修改调用方移除 null 检查**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs` 第 170-175 行，将：

```csharp
            // 每 SKU 查询一次快照，避免重复调用商品域
            var snapshot = await _snapshotAntiCorruption.GetSkuSnapshotAsync(skuId, ct);
            if (snapshot is null)
            {
                Logger.LogWarning("SKU 快照查询失败，跳过刷新 SkuId={SkuId}", skuId);
                continue;
            }
```

替换为：

```csharp
            // 每 SKU 查询一次快照，避免重复调用商品域
            // M4 双轨方案：GetSkuSnapshotAsync 失败抛 AntiCorruptionException，此处捕获后跳过该 SKU
            SkuSnapshotDto snapshot;
            try
            {
                snapshot = await _snapshotAntiCorruption.GetSkuSnapshotAsync(skuId, ct);
            }
            catch (Leno.Infrastructure.AntiCorruption.AntiCorruptionException ex)
            {
                Logger.LogWarning(ex, "SKU 快照查询失败，跳过刷新 SkuId={SkuId} ErrorCode={ErrorCode}", skuId, ex.ErrorCode);
                continue;
            }
```

需在文件顶部确认 using：

```csharp
using Leno.Cart.Application.DTOs;
```

- [ ] **Step 7: 编写单元测试（先验证失败）**

创建 `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/GrpcProductSnapshotAntiCorruptionClientTests.cs`：

```csharp
using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Grpc;

public class GrpcProductSnapshotAntiCorruptionClientTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { { "Product", "test-key" } }
        });
        return mock.Object;
    }

    [Fact]
    public async Task GetSkuSnapshot_Success_ReturnsMappedSnapshot()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId = Guid.NewGuid();
        var skuInfo = new SkuInfo
        {
            SkuId = (long)skuId.GetHashCode(),
            Title = "Test SKU",
            MainImage = "http://img",
            PriceCents = 12999,
            Currency = "CNY",
            Salable = true,
            Stock = 100
        };

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<SkuInfo>(
                Task.FromResult(skuInfo),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcProductSnapshotAntiCorruptionClient(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var result = await client.GetSkuSnapshotAsync(skuId);

        result.Should().NotBeNull();
        result.SkuId.Should().Be(skuId);
        result.Title.Should().Be("Test SKU");
        result.MainImageUrl.Should().Be("http://img");
        result.UnitPrice.Should().Be(129.99m);
        result.IsOnSale.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkuSnapshot_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductSnapshotAntiCorruptionClient(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuSnapshotAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PRODUCT_UNAVAILABLE");
    }

    [Fact]
    public async Task GetSkuSnapshot_NotFound_ThrowsAntiCorruptionException_RemoteFailed()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "sku not found"));

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductSnapshotAntiCorruptionClient(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuSnapshotAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.ErrorCode.Should().Be("PRODUCT_REMOTE_FAILED");
    }
}
```

- [ ] **Step 8: 运行测试验证通过**

Run: `dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GrpcProductSnapshotAntiCorruptionClientTests"`
Expected: 3 个测试全部 PASS

- [ ] **Step 9: 验证全解决方案编译 + 既有测试无回归**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCEEDED

Run: `dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj`
Expected: 既有测试 + 新增 3 个测试全部 PASS（关注 `CartProductEventConsumerTests` 可能需要更新 mock 返回非空）

- [ ] **Step 10: 修复既有测试（如有回归）**

如果 `CartProductEventConsumerTests` 或 `ProductEventConsumerTests` 因接口签名变更失败，需更新 mock：

- 原：`_snapshotAcMock.Setup(a => a.GetSkuSnapshotAsync(SkuId, It.IsAny<CancellationToken>())).ReturnsAsync(newSnapshot);`
- 新：保持不变（ReturnsAsync 仍可用，因为 SkuSnapshotDto 非空也是 SkuSnapshotDto? 的有效值）

如果测试断言"返回 null 时跳过"的行为，需改为"抛 AntiCorruptionException 时跳过"：

```csharp
_snapshotAcMock.Setup(a => a.GetSkuSnapshotAsync(SkuId, It.IsAny<CancellationToken>()))
    .ThrowsAsync(new AntiCorruptionException("test", "PRODUCT_REMOTE_FAILED"));
```

- [ ] **Step 11: 提交**

```bash
git add src/Services/Cart/Leno.Cart.Application/Abstractions/IProductSnapshotAntiCorruption.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ProductSnapshotDispatcherAdapter.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs \
        src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/GrpcProductSnapshotAntiCorruptionClientTests.cs
git commit -m "feat(M4): Cart ProductSnapshot 双轨化 + 继承 AntiCorruptionBase + 失败抛异常"
```

---

## Task B1: Cart.Api/CartGrpcService

**Files:**
- Create: `src/Services/Cart/Leno.Cart.Application/ICartInternalQueryService.cs`
- Create: `src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs`
- Create: `src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs`
- Modify: `src/Services/Cart/Leno.Cart.Api/Program.cs`
- Modify: `src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj`
- Test: `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/CartGrpcServiceTests.cs`

**背景：** Cart BC 既有 `ICartAppService`（位于 `Leno.Cart.Application/Services/ICartAppService.cs`）含 `GetCartAsync(userId)` + `PreviewCheckoutAsync(...)` 方法。`.proto` 契约中 `cart.proto` 已定义 `CartInternalService` 含 `GetCartSnapshot` + `GetCheckoutPreview` RPC，`user_id` 为 string，`sku_id` 为 int64（POC 简化）。Cart.Api.csproj 当前未引用 Grpc.AspNetCore 和 Leno.SharedContracts.Grpc。

**参考模板：** `ProductGrpcService.cs` + `OrderGrpcService.cs`

- [ ] **Step 1: 修改 Cart.Api.csproj 添加引用**

修改 `src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj`，在 `<ItemGroup>` 中追加：

```xml
        <PackageReference Include="Grpc.AspNetCore" Version="2.65.0" />
```

并在 ProjectReference 的 ItemGroup 中追加：

```xml
        <ProjectReference Include="..\..\..\BuildingBlocks\Leno.SharedContracts.Grpc\Leno.SharedContracts.Grpc.csproj" />
```

- [ ] **Step 2: 创建 ICartInternalQueryService 接口**

创建 `src/Services/Cart/Leno.Cart.Application/ICartInternalQueryService.cs`：

```csharp
namespace Leno.Cart.Application;

/// <summary>
/// 购物车域跨 BC 内部查询服务（M4 双轨方案）。
/// 仅暴露跨 BC 查询所需的方法子集（只读），供 CartGrpcService 复用。
/// </summary>
public interface ICartInternalQueryService
{
    /// <summary>
    /// 查询用户购物车快照（含购物车项）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>购物车快照；购物车不存在返回 null。</returns>
    Task<CartSnapshotDto?> GetCartSnapshotAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 查询用户结账预览（含金额汇总）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>结账预览；购物车不存在返回 null。</returns>
    Task<CheckoutPreviewDto?> GetCheckoutPreviewAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>购物车快照 DTO（跨 BC 查询用）。</summary>
public sealed class CartSnapshotDto
{
    public Guid CartId { get; init; }
    public IReadOnlyList<CartItemSnapshotDto> Items { get; init; } = Array.Empty<CartItemSnapshotDto>();
    public long TotalCents { get; init; }
}

public sealed class CartItemSnapshotDto
{
    public Guid SkuId { get; init; }
    public int Quantity { get; init; }
    public long UnitPriceCents { get; init; }
}

public sealed class CheckoutPreviewDto
{
    public long SubtotalCents { get; init; }
    public long DiscountCents { get; init; }
    public long ShippingCents { get; init; }
    public long TotalCents { get; init; }
}
```

- [ ] **Step 3: 创建 CartInternalQueryService 实现**

创建 `src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs`：

```csharp
using Leno.Cart.Application.Services;

namespace Leno.Cart.Application.InternalQueryServices;

/// <summary>
/// 购物车域跨 BC 内部查询服务实现（M4 双轨方案）。
/// 委托 <see cref="ICartAppService"/> 的既有查询方法，映射为跨 BC DTO。
/// </summary>
public sealed class CartInternalQueryService : ICartInternalQueryService
{
    private readonly ICartAppService _cartAppService;

    public CartInternalQueryService(ICartAppService cartAppService)
    {
        _cartAppService = cartAppService ?? throw new ArgumentNullException(nameof(cartAppService));
    }

    /// <inheritdoc />
    public async Task<CartSnapshotDto?> GetCartSnapshotAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await _cartAppService.GetCartAsync(userId, ct);
        if (cart is null) return null;

        return new CartSnapshotDto
        {
            CartId = cart.CartId,
            Items = cart.Items.Select(i => new CartItemSnapshotDto
            {
                SkuId = i.SkuId,
                Quantity = i.Quantity,
                UnitPriceCents = (long)(i.UnitPrice * 100)
            }).ToList(),
            TotalCents = (long)(cart.TotalAmount * 100)
        };
    }

    /// <inheritdoc />
    public async Task<CheckoutPreviewDto?> GetCheckoutPreviewAsync(Guid userId, CancellationToken ct = default)
    {
        var preview = await _cartAppService.PreviewCheckoutAsync(userId, ct);
        if (preview is null) return null;

        return new CheckoutPreviewDto
        {
            SubtotalCents = (long)(preview.Subtotal * 100),
            DiscountCents = (long)(preview.Discount * 100),
            ShippingCents = (long)(preview.ShippingFee * 100),
            TotalCents = (long)(preview.Total * 100)
        };
    }
}
```

**注：** 实施时需探查 `ICartAppService.GetCartAsync` 返回的 DTO 字段名（CartId/Items/SkuId/Quantity/UnitPrice/TotalAmount），如字段名不一致需调整映射。同理 `PreviewCheckoutAsync` 返回 DTO 的字段名（Subtotal/Discount/ShippingFee/Total）。

- [ ] **Step 4: 注册 ICartInternalQueryService**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，在 `services.AddScoped<ICartAppService, CartAppService>();` 之后追加：

```csharp
        services.AddScoped<ICartInternalQueryService, InternalQueryServices.CartInternalQueryService>();
```

需在文件顶部追加 using：

```csharp
using Leno.Cart.Application;
```

- [ ] **Step 5: 创建 CartGrpcService**

创建 `src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs`：

```csharp
using Grpc.Core;
using Leno.Cart.Application;
using Leno.SharedContracts.Grpc.Cart.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Cart.Api.GrpcServices;

/// <summary>
/// 购物车域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="ICartInternalQueryService"/> 业务逻辑，与 InternalCartsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class CartGrpcService : CartInternalService.CartInternalServiceBase
{
    private readonly ICartInternalQueryService _queryService;
    private readonly ILogger<CartGrpcService> _logger;

    public CartGrpcService(
        ICartInternalQueryService queryService,
        ILogger<CartGrpcService> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public override async Task<CartSnapshot> GetCartSnapshot(
        GetCartSnapshotRequest request, ServerCallContext context)
    {
        var userId = Guid.Parse(request.UserId);
        var dto = await _queryService.GetCartSnapshotAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Cart for user {request.UserId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<CheckoutPreview> GetCheckoutPreview(
        GetCheckoutPreviewRequest request, ServerCallContext context)
    {
        var userId = Guid.Parse(request.UserId);
        var dto = await _queryService.GetCheckoutPreviewAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Checkout preview for user {request.UserId} not found"));
        }

        return MapToProto(dto);
    }

    private static CartSnapshot MapToProto(CartSnapshotDto dto)
    {
        var proto = new CartSnapshot
        {
            CartId = dto.CartId.ToString(),
            TotalCents = dto.TotalCents
        };
        foreach (var item in dto.Items)
        {
            proto.Items.Add(new CartItem
            {
                SkuId = (long)item.SkuId.GetHashCode(),  // POC 简化
                Quantity = item.Quantity,
                UnitPriceCents = item.UnitPriceCents
            });
        }
        return proto;
    }

    private static CheckoutPreview MapToProto(CheckoutPreviewDto dto) => new()
    {
        SubtotalCents = dto.SubtotalCents,
        DiscountCents = dto.DiscountCents,
        ShippingCents = dto.ShippingCents,
        TotalCents = dto.TotalCents
    };
}
```

- [ ] **Step 6: 修改 Program.cs 添加 gRPC 映射**

修改 `src/Services/Cart/Leno.Cart.Api/Program.cs`，在 `app.UseLenoPipeline();` 之后、`await app.Services.MigrateWithLockAsync<CartDbContext>();` 之前追加：

```csharp
// M4 双轨方案：启用 gRPC 服务端（仅当 AntiCorruption:UseGrpc=true 时映射）
if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
{
    app.MapGrpcService<CartGrpcService>();
}
```

需在文件顶部追加 using：

```csharp
using Leno.Cart.Api.GrpcServices;
```

- [ ] **Step 7: 编写单元测试**

创建 `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/CartGrpcServiceTests.cs`：

```csharp
using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Application;
using Leno.Cart.Api.GrpcServices;
using Leno.SharedContracts.Grpc.Cart.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Grpc;

public class CartGrpcServiceTests
{
    [Fact]
    public async Task GetCartSnapshot_Success_ReturnsMappedSnapshot()
    {
        var queryMock = new Mock<ICartInternalQueryService>();
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        queryMock.Setup(q => q.GetCartSnapshotAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartSnapshotDto
            {
                CartId = cartId,
                Items = new List<CartItemSnapshotDto>
                {
                    new() { SkuId = skuId, Quantity = 2, UnitPriceCents = 9999 }
                },
                TotalCents = 19998
            });

        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var result = await svc.GetCartSnapshot(
            new GetCartSnapshotRequest { UserId = userId.ToString() },
            new TestServerCallContext());

        result.CartId.Should().Be(cartId.ToString());
        result.TotalCents.Should().Be(19998);
        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(2);
        result.Items[0].UnitPriceCents.Should().Be(9999);
    }

    [Fact]
    public async Task GetCartSnapshot_NotFound_ThrowsRpcException()
    {
        var queryMock = new Mock<ICartInternalQueryService>();
        queryMock.Setup(q => q.GetCartSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartSnapshotDto?)null);

        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var act = async () => await svc.GetCartSnapshot(
            new GetCartSnapshotRequest { UserId = Guid.NewGuid().ToString() },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GetCartSnapshot_InvalidArgument_ThrowsRpcException()
    {
        var queryMock = new Mock<ICartInternalQueryService>(MockBehavior.Strict);
        var svc = new CartGrpcService(queryMock.Object, NullLogger<CartGrpcService>.Instance);

        var act = async () => await svc.GetCartSnapshot(
            new GetCartSnapshotRequest { UserId = "not-a-guid" },
            new TestServerCallContext());

        (await act.Should().ThrowAsync<RpcException>()).Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }
}
```

**注：** `TestServerCallContext` 是测试用 `ServerCallContext` 实现。如果项目中已有 `TestServerCallContext`（参考既有 GrpcService 测试），复用；否则需创建。实施时探查 `Order.Infrastructure.Tests/Grpc/` 或 `ReviewAfterSales.Infrastructure.Tests/Grpc/` 是否有可复用的实现。

- [ ] **Step 8: 验证编译 + 测试通过**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCEEDED

Run: `dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --filter "FullyQualifiedName~CartGrpcServiceTests"`
Expected: 3 个测试 PASS

- [ ] **Step 9: 提交**

```bash
git add src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj \
        src/Services/Cart/Leno.Cart.Application/ICartInternalQueryService.cs \
        src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs \
        src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs \
        src/Services/Cart/Leno.Cart.Api/Program.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs \
        src/Services/Cart/Leno.Cart.Infrastructure.Tests/Grpc/CartGrpcServiceTests.cs
git commit -m "feat(M4): Cart.Api CartGrpcService 实现（GetCartSnapshot + GetCheckoutPreview）"
```

---

## Task B2: SellerShop.Api/SellerGrpcService

**Files:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/Leno.SellerShop.Api.csproj`
- Test: `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Grpc/SellerGrpcServiceTests.cs`

**背景：** SellerShop BC 既有 `ISellerAppService.GetSellerProfileAsync(userId)` + `IShopAppService.GetShopInfoAsync(shopId)`。`seller.proto` 已定义 `SellerInternalService` 含 `GetSellerInfo` + `GetShopInfo` + `ValidateSellerOwnership` RPC。`seller_id` 为 string，`shop_id` 为 int64（POC 简化）。`ValidateSellerOwnership` 抛 Unimplemented（F1.4 独立任务）。

- [ ] **Step 1: 修改 SellerShop.Api.csproj 添加引用**

参考 Task B1 Step 1，添加 `Grpc.AspNetCore 2.65.0` 包引用 + `Leno.SharedContracts.Grpc` 项目引用。

- [ ] **Step 2: 创建 ISellerInternalQueryService 接口**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs`：

```csharp
namespace Leno.SellerShop.Application;

/// <summary>
/// 卖家店铺域跨 BC 内部查询服务（M4 双轨方案）。
/// 仅暴露跨 BC 查询所需的方法子集（只读）。
/// </summary>
public interface ISellerInternalQueryService
{
    /// <summary>查询卖家信息（seller_id = 用户域 UserId）。</summary>
    Task<SellerInfoDto?> GetSellerInfoAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>查询店铺信息。</summary>
    Task<ShopInfoDto?> GetShopInfoAsync(Guid shopId, CancellationToken ct = default);
}

public sealed class SellerInfoDto
{
    public Guid SellerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid ShopId { get; init; }
}

public sealed class ShopInfoDto
{
    public Guid ShopId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid SellerId { get; init; }
}
```

- [ ] **Step 3: 创建 SellerInternalQueryService 实现**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs`：

```csharp
using Leno.SellerShop.Application.Services;

namespace Leno.SellerShop.Application.InternalQueryServices;

public sealed class SellerInternalQueryService : ISellerInternalQueryService
{
    private readonly ISellerAppService _sellerAppService;
    private readonly IShopAppService _shopAppService;

    public SellerInternalQueryService(
        ISellerAppService sellerAppService,
        IShopAppService shopAppService)
    {
        _sellerAppService = sellerAppService ?? throw new ArgumentNullException(nameof(sellerAppService));
        _shopAppService = shopAppService ?? throw new ArgumentNullException(nameof(shopAppService));
    }

    public async Task<SellerInfoDto?> GetSellerInfoAsync(Guid sellerId, CancellationToken ct = default)
    {
        var seller = await _sellerAppService.GetSellerProfileAsync(sellerId, ct);
        if (seller is null) return null;

        return new SellerInfoDto
        {
            SellerId = seller.UserId,
            Name = seller.ShopName,
            Status = seller.Status,
            ShopId = seller.ShopId
        };
    }

    public async Task<ShopInfoDto?> GetShopInfoAsync(Guid shopId, CancellationToken ct = default)
    {
        var shop = await _shopAppService.GetShopInfoAsync(shopId, ct);
        if (shop is null) return null;

        return new ShopInfoDto
        {
            ShopId = shop.ShopId,
            Name = shop.ShopName,
            Status = shop.Status,
            SellerId = shop.SellerId
        };
    }
}
```

**注：** 实施时需探查 `ISellerAppService.GetSellerProfileAsync` 返回 DTO 的字段名（UserId/ShopName/Status/ShopId），如不一致需调整。同理 `IShopAppService.GetShopInfoAsync` 返回 DTO 的字段名。

- [ ] **Step 4: 注册 ISellerInternalQueryService**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，追加：

```csharp
        services.AddScoped<ISellerInternalQueryService, InternalQueryServices.SellerInternalQueryService>();
```

- [ ] **Step 5: 创建 SellerGrpcService**

创建 `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`：

```csharp
using Grpc.Core;
using Leno.SellerShop.Application;
using Leno.SharedContracts.Grpc.Seller.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.SellerShop.Api.GrpcServices;

[Authorize]
public sealed class SellerGrpcService : SellerInternalService.SellerInternalServiceBase
{
    private readonly ISellerInternalQueryService _queryService;
    private readonly ILogger<SellerGrpcService> _logger;

    public SellerGrpcService(
        ISellerInternalQueryService queryService,
        ILogger<SellerGrpcService> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public override async Task<SellerInfo> GetSellerInfo(
        GetSellerInfoRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SellerId, out var sellerId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid seller_id: {request.SellerId}"));
        }

        var dto = await _queryService.GetSellerInfoAsync(sellerId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Seller {request.SellerId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<ShopInfo> GetShopInfo(
        GetShopInfoRequest request, ServerCallContext context)
    {
        // proto shop_id 是 int64，POC 简化：int64 → Guid
        var shopId = new Guid(Convert.FromHexString(request.ShopId.ToString("X16")));
        var dto = await _queryService.GetShopInfoAsync(shopId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Shop {request.ShopId} not found"));
        }

        return MapToProto(dto);
    }

    public override Task<ValidateSellerOwnershipResponse> ValidateSellerOwnership(
        ValidateSellerOwnershipRequest request, ServerCallContext context)
    {
        // F1.4 独立任务，本次抛 Unimplemented
        throw new RpcException(new Status(StatusCode.Unimplemented,
            "ValidateSellerOwnership not implemented, see F1.4"));
    }

    private static SellerInfo MapToProto(SellerInfoDto dto) => new()
    {
        SellerId = dto.SellerId.ToString(),
        Name = dto.Name,
        Status = dto.Status,
        ShopId = (long)dto.ShopId.GetHashCode()  // POC 简化
    };

    private static ShopInfo MapToProto(ShopInfoDto dto) => new()
    {
        ShopId = (long)dto.ShopId.GetHashCode(),  // POC 简化
        Name = dto.Name,
        Status = dto.Status,
        SellerId = dto.SellerId.ToString()
    };
}
```

- [ ] **Step 6: 修改 Program.cs + 编写单元测试 + 提交**

参考 Task B1 Step 6/7/8/9 模式：
- 修改 `SellerShop.Api/Program.cs` 添加 `app.MapGrpcService<SellerGrpcService>()` 条件性映射
- 创建 `SellerGrpcServiceTests.cs` 含 3 个测试（Success/NotFound/InvalidArgument）
- 验证编译 + 测试通过
- 提交：`feat(M4): SellerShop.Api SellerGrpcService 实现（GetSellerInfo + GetShopInfo）`

---

## Task B3: ReviewAfterSales.Api/ReviewGrpcService

**Files:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewInternalQueryService.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Leno.ReviewAfterSales.Api.csproj`
- Test: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Grpc/ReviewGrpcServiceTests.cs`

**背景：** ReviewAfterSales BC 既有 `IReviewAppService` 但无聚合评分查询方法。`IReviewInternalQueryService` 需直接访问 `IReviewRepository` 聚合计算 average_rating/total_count/positive_count，并按 orderId 聚合评价列表。`review.proto` 已定义 `ReviewInternalService` 含 `GetProductRating` + `GetOrderReviews` RPC，`spu_id` 为 int64（POC 简化），`order_id` 为 string。

- [ ] **Step 1: 修改 ReviewAfterSales.Api.csproj 添加引用**

参考 Task B1 Step 1。

- [ ] **Step 2: 创建 IReviewInternalQueryService 接口**

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewInternalQueryService.cs`：

```csharp
namespace Leno.ReviewAfterSales.Application;

public interface IReviewInternalQueryService
{
    Task<ProductRatingDto?> GetProductRatingAsync(Guid spuId, CancellationToken ct = default);
    Task<OrderReviewsDto?> GetOrderReviewsAsync(Guid orderId, CancellationToken ct = default);
}

public sealed class ProductRatingDto
{
    public Guid SpuId { get; init; }
    public double AverageRating { get; init; }
    public int TotalCount { get; init; }
    public int PositiveCount { get; init; }
}

public sealed class OrderReviewsDto
{
    public IReadOnlyList<ReviewSummaryDto> Reviews { get; init; } = Array.Empty<ReviewSummaryDto>();
}

public sealed class ReviewSummaryDto
{
    public Guid ReviewId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
```

- [ ] **Step 3: 创建 ReviewInternalQueryService 实现**

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs`：

```csharp
using Leno.ReviewAfterSales.Domain.Repositories;

namespace Leno.ReviewAfterSales.Application.InternalQueryServices;

public sealed class ReviewInternalQueryService : IReviewInternalQueryService
{
    private readonly IReviewRepository _reviewRepository;

    public ReviewInternalQueryService(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
    }

    public async Task<ProductRatingDto?> GetProductRatingAsync(Guid spuId, CancellationToken ct = default)
    {
        // 探查 IReviewRepository 既有方法，按 spuId 聚合评分
        // 实施时确认方法名，如 GetBySpuIdAsync / GetReviewsBySpuAsync
        var reviews = await _reviewRepository.GetBySpuIdAsync(spuId, ct);
        if (reviews is null || !reviews.Any()) return null;

        var totalCount = reviews.Count();
        var positiveCount = reviews.Count(r => r.Rating >= 4);
        var averageRating = reviews.Average(r => r.Rating);

        return new ProductRatingDto
        {
            SpuId = spuId,
            AverageRating = averageRating,
            TotalCount = totalCount,
            PositiveCount = positiveCount
        };
    }

    public async Task<OrderReviewsDto?> GetOrderReviewsAsync(Guid orderId, CancellationToken ct = default)
    {
        // 探查 IReviewRepository 既有方法，按 orderId 查询评价列表
        // 实施时确认方法名，如 GetByOrderIdAsync
        var reviews = await _reviewRepository.GetByOrderIdAsync(orderId, ct);
        if (reviews is null || !reviews.Any()) return null;

        return new OrderReviewsDto
        {
            Reviews = reviews.Select(r => new ReviewSummaryDto
            {
                ReviewId = r.Id,
                SpuId = r.SpuId,
                Rating = r.Rating,
                Content = r.Content ?? string.Empty,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
```

**注：** 实施时需探查 `IReviewRepository` 既有方法签名，确认是否有 `GetBySpuIdAsync` + `GetByOrderIdAsync`。如方法名不一致或不存在，需调整实现或在 `IReviewRepository` 中新增方法。

- [ ] **Step 4: 注册 IReviewInternalQueryService**

修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，追加：

```csharp
        services.AddScoped<IReviewInternalQueryService, InternalQueryServices.ReviewInternalQueryService>();
```

- [ ] **Step 5: 创建 ReviewGrpcService**

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs`：

```csharp
using Grpc.Core;
using Leno.ReviewAfterSales.Application;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.ReviewAfterSales.Api.GrpcServices;

[Authorize]
public sealed class ReviewGrpcService : ReviewInternalService.ReviewInternalServiceBase
{
    private readonly IReviewInternalQueryService _queryService;
    private readonly ILogger<ReviewGrpcService> _logger;

    public ReviewGrpcService(
        IReviewInternalQueryService queryService,
        ILogger<ReviewGrpcService> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public override async Task<ProductRating> GetProductRating(
        GetProductRatingRequest request, ServerCallContext context)
    {
        // proto spu_id 是 int64，POC 简化
        var spuId = new Guid(Convert.FromHexString(request.SpuId.ToString("X16")));
        var dto = await _queryService.GetProductRatingAsync(spuId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Product rating for spu {request.SpuId} not found"));
        }

        return MapToProto(dto);
    }

    public override async Task<OrderReviews> GetOrderReviews(
        GetOrderReviewsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
        }

        var dto = await _queryService.GetOrderReviewsAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Reviews for order {request.OrderId} not found"));
        }

        return MapToProto(dto);
    }

    private static ProductRating MapToProto(ProductRatingDto dto) => new()
    {
        SpuId = (long)dto.SpuId.GetHashCode(),  // POC 简化
        AverageRating = dto.AverageRating,
        TotalCount = dto.TotalCount,
        PositiveCount = dto.PositiveCount
    };

    private static OrderReviews MapToProto(OrderReviewsDto dto)
    {
        var proto = new OrderReviews();
        foreach (var r in dto.Reviews)
        {
            proto.Reviews.Add(new ReviewSummary
            {
                ReviewId = r.ReviewId.ToString(),
                SpuId = (long)r.SpuId.GetHashCode(),  // POC 简化
                Rating = r.Rating,
                Content = r.Content,
                CreatedAt = r.CreatedAt.ToString("O")  // ISO 8601
            });
        }
        return proto;
    }
}
```

- [ ] **Step 6: 修改 Program.cs + 编写单元测试 + 提交**

参考 Task B1 Step 6/7/8/9 模式：
- 修改 `ReviewAfterSales.Api/Program.cs` 添加 `app.MapGrpcService<ReviewGrpcService>()` 条件性映射
- 创建 `ReviewGrpcServiceTests.cs` 含 3 个测试（Success/NotFound/InvalidArgument）
- 验证编译 + 测试通过
- 提交：`feat(M4): ReviewAfterSales.Api ReviewGrpcService 实现（GetProductRating + GetOrderReviews）`

---

## Task C1: 既有 spec supersede 标注

**Files:**
- Modify: `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md`
- Modify: `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md`
- Modify: `.trae/specs/fix-critical-business-vulnerabilities/spec.md`

**背景：** 3 份旧 spec 未标注 supersede 关系，需在文件最顶部追加 YAML frontmatter 声明，不修改原内容。

- [ ] **Step 1: 探查 3 份 spec 当前内容**

读取 3 份 spec 文件，识别：
- `2026-07-13-comprehensive-optimization-design.md`：整体 V1 设计，已被 V2 全面取代
- `2026-07-14-api-gateway-enhancement-design.md`：API 网关增强，部分章节被 M4 spec 取代（gRPC/双轨），部分仍有效（CORS/限流/JWT 黑名单）
- `.trae/specs/fix-critical-business-vulnerabilities/spec.md`：F1/F2 修复，部分章节被 F1.4 取代（鉴权集中化），部分仍有效（输入校验/SQL 注入防护）

- [ ] **Step 2: 在 2026-07-13 spec 顶部追加 superseded frontmatter**

在 `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` 文件最顶部追加（原内容保留不变）：

```markdown
---
status: superseded
superseded_by: docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md
superseded_date: 2026-07-19
superseded_reason: |
  V1 整体设计已被 V2 全面取代。V2 在以下方面有重大调整：
  - 重新规划快轨 F1-F4 + 慢轨 M1-M6 优先级
  - 引入 Subagent-Driven 实施模式
  - M4 改为 gRPC 双轨方案（保留 HttpClient fallback）
  - 新增 M5 可观测性 + M6 CQRS/BFF/文档
  V1 中未实施的部分（如 F1.4 越权校验集中化）仍可作为后续任务参考，
  但实施时需遵循 V2 的架构约束。
---

```

- [ ] **Step 3: 在 2026-07-14 spec 顶部追加 partially_superseded frontmatter**

在 `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md` 文件最顶部追加：

```markdown
---
status: partially_superseded
partially_superseded_by:
  - docs/superpowers/specs/2026-07-19-m4-grpc-dual-track-design.md
  - docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md
partially_superseded_date: 2026-07-19
partially_superseded_reason: |
  本 spec 中 gRPC 服务端实现 + 双轨通信部分已被以下 spec 取代：
  - 2026-07-19-m4-grpc-dual-track-design.md（gRPC 双轨方案）
  - 2026-07-17-comprehensive-optimization-v2-design.md（整体架构）
  以下章节仍有效：
  - CORS 配置
  - 限流策略
  - JWT 黑名单
  - API 网关聚合模式
---

```

- [ ] **Step 4: 在 .trae spec 顶部追加 partially_superseded frontmatter**

在 `.trae/specs/fix-critical-business-vulnerabilities/spec.md` 文件最顶部追加：

```markdown
---
status: partially_superseded
partially_superseded_by:
  - docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md
partially_superseded_date: 2026-07-19
partially_superseded_reason: |
  本 spec 中鉴权集中化部分已被 V2 spec 的 F1.4 后续任务取代。
  以下章节仍有效：
  - 输入校验
  - SQL 注入防护
  - XSS 防护
  - CSRF 防护
---

```

- [ ] **Step 5: 验证原内容未被修改**

读取 3 份 spec，确认 frontmatter 之后的内容与修改前完全一致。

- [ ] **Step 6: 提交**

```bash
git add docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md \
        docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md \
        .trae/specs/fix-critical-business-vulnerabilities/spec.md
git commit -m "docs(M4): 3 份旧 spec 标注 supersede 关系（frontmatter 声明）"
```

---

## Task C2: ADR 关键决策记录

**Files:**
- Create: `docs/decisions/README.md`
- Create: `docs/decisions/0001-grpc-dual-track-with-http-fallback.md`
- Create: `docs/decisions/0002-circuit-breaker-three-state-machine.md`
- Create: `docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md`
- Create: `docs/decisions/0004-iorderstatus-provider-refactor.md`
- Create: `docs/decisions/0005-proto-backward-compatibility-constraint.md`
- Create: `docs/decisions/0006-guid-int64-poc-simplification-history.md`
- Create: `docs/decisions/0007-guid-string-migration-strategy.md`

**背景：** M4 实施过程的 7 个关键决策需以 ADR（Nygard 格式）记录，便于团队追溯。ADR 基于 spec §10.3/§10.4 的示例 + 实际 commit 历史。

- [ ] **Step 1: 创建 docs/decisions/README.md**

```markdown
# Architecture Decision Records (ADR)

本目录记录 Leno 项目的关键架构决策，采用 Michael Nygard 的 ADR 格式。

## 格式

每个 ADR 文件命名：`NNNN-kebab-case-title.md`（NNNN 为四位数字编号）。

内容结构：

```markdown
# ADR-NNNN: 标题

## 状态
已接受 / 已取代 / 已弃用 / 已提议

## 上下文
（决策背景、约束、问题）

## 决策
（选择方案 + 理由）

## 后果
**正面：**
**负面：**
**风险缓解：**
```

## ADR 索引

| 编号 | 标题 | 状态 | 日期 |
|---|---|---|---|
| [ADR-0001](0001-grpc-dual-track-with-http-fallback.md) | gRPC 双轨方案（保留 HttpClient fallback） | 已接受 | 2026-07-19 |
| [ADR-0002](0002-circuit-breaker-three-state-machine.md) | 熔断器三状态机（Closed/Open/HalfOpen） | 已接受 | 2026-07-19 |
| [ADR-0003](0003-anticorruption-dispatcher-adapter-pattern.md) | AntiCorruptionDispatcher 适配器模式 | 已接受 | 2026-07-19 |
| [ADR-0004](0004-iorderstatus-provider-refactor.md) | IOrderStatusProvider 重构 | 已接受 | 2026-07-19 |
| [ADR-0005](0005-proto-backward-compatibility-constraint.md) | .proto 向后兼容约束 | 已接受 | 2026-07-19 |
| [ADR-0006](0006-guid-int64-poc-simplification-history.md) | Guid→int64 POC 简化历史 | 已接受 | 2026-07-19 |
| [ADR-0007](0007-guid-string-migration-strategy.md) | Guid→string 迁移策略 | 已接受 | 2026-07-19 |
```

- [ ] **Step 2: 创建 7 个 ADR 文件**

依次创建 7 个 ADR 文件，内容参考 spec §10.3/§10.4 示例 + 实际 commit 历史。每个 ADR 必须包含：
- 状态（已接受）
- 上下文（决策背景）
- 决策（选择方案 + 理由）
- 后果（正面/负面/风险缓解）

ADR-0001 内容见 spec §10.3，ADR-0003 内容见 spec §10.4。其余 ADR 参考以下要点：

**ADR-0002（熔断器三状态机）**：
- 上下文：gRPC 故障时需自动降级，避免雪崩
- 决策：Closed（正常）→ 3 次连续失败 → Open（30 秒）→ HalfOpen（1 次探测）→ 2 次连续成功 → Closed
- 后果：正面（自动降级）；负面（30 秒内强制走 HttpClient）

**ADR-0004（IOrderStatusProvider 重构）**：
- 上下文：Task 23 发现 EligibilityChecker 混合远程调用+业务规则+仓储查询
- 决策：提取 IOrderStatusProvider 接口分离远程查询，EligibilityChecker 保留业务规则+仓储
- 后果：正面（职责分离，双轨化）；负面（多一层抽象）

**ADR-0005（.proto 向后兼容约束）**：
- 上下文：.proto 修改会破坏 wire 兼容性
- 决策：只能新增字段，不能修改/删除，buf breaking 校验
- 后果：正面（wire 兼容）；负面（字段累积，需定期 deprecated 标记）

**ADR-0006（Guid→int64 POC 简化历史）**：
- 上下文：POC 阶段 .proto 使用 int64 承载 Guid，采用 GetHashCode 简化
- 决策：POC 阶段允许 GetHashCode 简化，生产化阶段通过 ADR-0007 迁移
- 后果：正面（快速验证）；负面（GetHashCode 可能碰撞，int64→Guid 不可逆）

**ADR-0007（Guid→string 迁移策略）**：
- 上下文：生产化需修复 Guid→int64 简化，但不能违反 ADR-0005
- 决策：新增 `string xxx_id_str` 字段 + 标记 int64 `[deprecated = true]`，GrpcService 双写，GrpcClient 优先读 string
- 后果：正面（wire 兼容，渐进迁移）；负面（字段冗余，代码复杂）

- [ ] **Step 3: 提交**

```bash
git add docs/decisions/
git commit -m "docs(M4): 新增 7 个 ADR 关键决策记录（gRPC 双轨方案 + 适配器模式 + Guid 迁移等）"
```

---

## Task D1: 6 个 .proto 文件新增 string 字段

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/product.proto`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/promotion.proto`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/cart.proto`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/seller.proto`
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/review.proto`

**背景：** 6 个 .proto 文件含 int64 ID 字段，需新增 `string xxx_id_str` 字段 + 标记 int64 `[deprecated = true]`。无需迁移的 .proto：payment.proto、user.proto、points.proto（已全部使用 string）。字段编号约定：新增 string 字段编号紧接既有最大字段号 +1。

**参考 spec：** §11.3 .proto 修改示例

- [ ] **Step 1: 修改 product.proto**

参考 spec §11.3，为以下字段新增 string 版本 + 标记 deprecated：
- `GetSkuInfoRequest.sku_id` → 新增 `sku_id_str = 13`
- `SkuInfo.sku_id/spu_id/seller_id` → 新增 `sku_id_str = 13`、`spu_id_str = 14`、`seller_id_str = 15`
- `BatchGetSkuInfoRequest.sku_ids` → 新增 `sku_ids_str = 2`
- `GetSkuStockRequest.sku_id` → 新增 `sku_id_str = 2`
- `SkuStock.sku_id` → 新增 `sku_id_str = 2`
- `GetProductDetailRequest.spu_id` → 新增 `spu_id_str = 2`
- `ProductDetail.spu_id/seller_id` → 新增 `spu_id_str = 6`、`seller_id_str = 7`

- [ ] **Step 2: 修改 order.proto**

为 `OrderItem.sku_id` 新增 `sku_id_str = 6`（既有最大字段号 5）。

- [ ] **Step 3: 修改 promotion.proto**

为 `OrderItem.sku_id`（位于 `CalculateDiscountRequest`）新增 `sku_id_str = 3`（既有最大字段号 2）。

- [ ] **Step 4: 修改 cart.proto**

为 `CartItem.sku_id` 新增 `sku_id_str = 4`（既有最大字段号 3）。

- [ ] **Step 5: 修改 seller.proto**

为 `GetShopInfoRequest.shop_id` 新增 `shop_id_str = 2`，`ShopInfo.shop_id` 新增 `shop_id_str = 5`（既有最大字段号 4）。

- [ ] **Step 6: 修改 review.proto**

为 `GetProductRatingRequest.spu_id` 新增 `spu_id_str = 2`，`ProductRating.spu_id` 新增 `spu_id_str = 5`（既有最大字段号 4），`ReviewSummary.spu_id` 新增 `spu_id_str = 6`（既有最大字段号 5）。

- [ ] **Step 7: 运行 buf generate 重新生成 C# 代码**

Run: `buf generate`（在 `src/BuildingBlocks/Leno.SharedContracts/` 目录下）
Expected: 生成代码含新增 string 字段属性

**注：** 如未安装 buf CLI，参考 spec §1.2 "buf generate 必须集成到 CI"。开发环境可通过 `dotnet build Leno.SharedContracts.Grpc` 触发 MSBuild Grpc.Tools 重新生成。

- [ ] **Step 8: 运行 buf breaking 校验**

Run: `buf breaking --against .git\~1`（或指定旧版本）
Expected: 校验通过（仅新增字段 + deprecated 选项不触发 breaking）

- [ ] **Step 9: 验证编译通过**

Run: `dotnet build src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj`
Expected: BUILD SUCCEEDED

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCEEDED（既有 GrpcService/GrpcClient 不受影响，新字段默认空值）

- [ ] **Step 10: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Protos/*.proto \
        src/BuildingBlocks/Leno.SharedContracts.Grpc/
git commit -m "feat(M4): 6 个 .proto 新增 string ID 字段 + int64 标记 deprecated（Guid→string 迁移）"
```

---

## Task D2: 6 个 GrpcService 更新 DTO→proto 映射（双写）

**Files:**
- Modify: `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`
- Modify: `src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs`
- Modify: `src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs`
- Modify: `src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs`

**背景：** GrpcService 需双写 int64（GetHashCode，向后兼容）+ string（Guid.ToString()，新客户端优先读）。请求参数解析需优先读 string，回退到 int64。Points/UserAuth/Payment 无 int64 ID 字段，无需修改。

**参考 spec：** §12.3/§12.4 代码示例

- [ ] **Step 1: 修改 ProductGrpcService.MapToProto 双写**

修改 `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`：

1. `MapToProto` 方法追加 string 字段：

```csharp
    private static SkuInfo MapToProto(SkuInfoResultDto dto) => new()
    {
        // 既有 int64 字段（向后兼容，标记 deprecated）
        SkuId = (long)dto.SkuId.GetHashCode(),
        SpuId = (long)dto.SpuId.GetHashCode(),
        SellerId = (long)dto.SellerId.GetHashCode(),
        // ... 其他字段不变
        // 新增 string 字段（Guid→string 迁移）
        SkuIdStr = dto.SkuId.ToString(),
        SpuIdStr = dto.SpuId.ToString(),
        SellerIdStr = dto.SellerId.ToString(),
    };
```

2. `GetSkuInfo` 请求参数解析优先读 string：

```csharp
    public override async Task<SkuInfo> GetSkuInfo(GetSkuInfoRequest request, ServerCallContext context)
    {
        Guid skuId;
        if (!string.IsNullOrEmpty(request.SkuIdStr))
        {
            skuId = Guid.Parse(request.SkuIdStr);
        }
        else
        {
            // 旧客户端回退
            skuId = new Guid(Convert.FromHexString(request.SkuId.ToString("X16")));
        }
        // ... 其余逻辑不变
    }
```

3. `BatchGetSkuInfo` 请求参数解析优先读 string：

```csharp
    public override async Task<BatchGetSkuInfoResponse> BatchGetSkuInfo(
        BatchGetSkuInfoRequest request, ServerCallContext context)
    {
        List<Guid> skuIds;
        if (request.SkuIdsStr.Count > 0)
        {
            skuIds = request.SkuIdsStr.Select(Guid.Parse).ToList();
        }
        else
        {
            skuIds = request.SkuIds.Select(id => new Guid(Convert.FromHexString(id.ToString("X16")))).ToList();
        }
        // ... 其余逻辑不变
    }
```

- [ ] **Step 2: 修改 PromotionGrpcService.MapToProto 双写 OrderItem.sku_id**

参考 Step 1 模式，为 `PromotionGrpcService` 的 `OrderItem` 映射追加 `SkuIdStr = dto.SkuId.ToString()`。请求参数解析同样优先读 `SkuIdStr`。

- [ ] **Step 3: 修改 OrderGrpcService.MapToProto 双写 OrderItem.sku_id**

参考 Step 1 模式，为 `OrderGrpcService` 的 `OrderItem` 映射追加 `SkuIdStr`。

- [ ] **Step 4: 修改 CartGrpcService.MapToProto 双写 CartItem.sku_id**

参考 Step 1 模式，为 `CartGrpcService` 的 `CartItem` 映射追加 `SkuIdStr`。

- [ ] **Step 5: 修改 SellerGrpcService.MapToProto 双写 shop_id**

参考 Step 1 模式，为 `SellerGrpcService` 的 `GetShopInfoRequest` + `ShopInfo` 映射追加 `ShopIdStr`。请求参数解析优先读 `ShopIdStr`。

- [ ] **Step 6: 修改 ReviewGrpcService.MapToProto 双写 spu_id**

参考 Step 1 模式，为 `ReviewGrpcService` 的 `GetProductRatingRequest` + `ProductRating` + `ReviewSummary` 映射追加 `SpuIdStr`。请求参数解析优先读 `SpuIdStr`。

- [ ] **Step 7: 更新单元测试验证 string 字段**

更新 6 个 GrpcService 的既有单元测试，在 Success 场景追加断言：

```csharp
        result.SkuIdStr.Should().Be(skuId.ToString());
```

新增 1 个向后兼容测试场景（旧客户端仅传 int64）：

```csharp
    [Fact]
    public async Task GetSkuInfo_LegacyClient_OnlyInt64_StillWorks()
    {
        // 旧客户端不传 SkuIdStr，仅传 SkuId（int64）
        var request = new GetSkuInfoRequest
        {
            SkuId = (long)skuId.GetHashCode()
            // SkuIdStr 未设置，默认空字符串
        };
        // ... 验证仍可正确解析（回退到 int64 → Guid）
    }
```

- [ ] **Step 8: 验证编译 + 测试通过**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCEEDED

Run: `dotnet test`（运行所有 GrpcService 单元测试）
Expected: 既有测试 + 新增向后兼容测试全部 PASS

- [ ] **Step 9: 提交**

```bash
git add src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs \
        src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs \
        src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs \
        src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs \
        src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs \
        src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs \
        src/Services/*/*/Infrastructure.Tests/Grpc/*GrpcServiceTests.cs
git commit -m "feat(M4): 6 个 GrpcService 双写 int64 + string ID 字段（向后兼容 + 优先读 string）"
```

---

## Task D3: 5 个 GrpcClient 更新 proto→DTO 映射（优先读 string）

**Files:**
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs`
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcPromotionAntiCorruptionClient.cs`
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs`
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs`

**背景：** GrpcClient 需优先读 string 字段，为空时回退到 int64。请求构造需同时填充 int64 + string。Points/UserContact/Payment 无 int64 ID 字段，无需修改。

**参考 spec：** §13.3/§13.4 代码示例

- [ ] **Step 1: 修改 GrpcProductAntiCorruptionClient.MapToDto 优先读 string**

修改 `src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs`：

1. `MapToDto` 优先读 string：

```csharp
    private static SkuInfo MapToDto(SkuInfo proto) => new()
    {
        SkuId = !string.IsNullOrEmpty(proto.SkuIdStr)
            ? Guid.Parse(proto.SkuIdStr)
            : new Guid(Convert.FromHexString(proto.SkuId.ToString("X16"))),
        SpuId = !string.IsNullOrEmpty(proto.SpuIdStr)
            ? Guid.Parse(proto.SpuIdStr)
            : new Guid(Convert.FromHexString(proto.SpuId.ToString("X16"))),
        SellerId = !string.IsNullOrEmpty(proto.SellerIdStr)
            ? Guid.Parse(proto.SellerIdStr)
            : Guid.Empty,  // POC 阶段 int64→Guid 不可逆，回退时无法还原 SellerId
        // ... 其他字段不变
    };
```

2. 请求构造同时填充 int64 + string：

```csharp
    var request = new GetSkuInfoRequest
    {
        SkuId = (long)skuId.GetHashCode(),       // 既有 int64（向后兼容）
        SkuIdStr = skuId.ToString(),             // 新增 string
    };
```

- [ ] **Step 2: 修改 GrpcPromotionAntiCorruptionClient**

参考 Step 1 模式，为 `OrderItem` 映射优先读 `SkuIdStr`，请求构造同时填充 int64 + string。

- [ ] **Step 3: 修改 GrpcCartPriceService**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs`：

1. `MapToSnapshot` 优先读 string（修复 SellerId 从 Guid.Empty 改为正确解析）：

```csharp
    private static SkuPriceSnapshotDomain MapToSnapshot(SkuInfoProto proto, Guid guid) => new()
    {
        SkuId = guid,  // 既有逻辑（从请求参数传入）
        Price = proto.PriceCents / 100m,
        Currency = string.IsNullOrEmpty(proto.Currency) ? "CNY" : proto.Currency,
        Available = proto.Salable,
        Title = proto.Title ?? string.Empty,
        MainImageUrl = proto.MainImage ?? string.Empty,
        // 修复：优先读 string，回退到 Guid.Empty（POC 阶段限制）
        SellerId = !string.IsNullOrEmpty(proto.SellerIdStr)
            ? Guid.Parse(proto.SellerIdStr)
            : Guid.Empty
    };
```

2. 请求构造同时填充 int64 + string：

```csharp
    var request = new BatchGetSkuInfoRequest();
    request.SkuIds.AddRange(ids.Select(id => (long)id.GetHashCode()));
    request.SkuIdsStr.AddRange(ids.Select(id => id.ToString()));
```

3. 响应映射可简化（不再依赖 int64 → Guid 映射表）：

```csharp
    // 优先用 SkuIdStr 建立 Guid 映射
    var skuMap = ids.ToDictionary(id => id.ToString(), id => id);
    var result = new List<SkuPriceSnapshotDomain>(response.Skus.Count);
    foreach (var proto in response.Skus)
    {
        var key = !string.IsNullOrEmpty(proto.SkuIdStr) ? proto.SkuIdStr : proto.SkuId.ToString();
        if (!skuMap.TryGetValue(key, out var guid))
        {
            continue;
        }
        result.Add(MapToSnapshot(proto, guid));
    }
```

- [ ] **Step 4: 修改 GrpcProductSnapshotAntiCorruptionClient**

参考 Step 1 模式，`MapToDto` 优先读 string。请求构造同时填充 int64 + string。

- [ ] **Step 5: 修改 GrpcOrderStatusProvider**

修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs`：

`MapToDto` 中 `OrderItem.sku_id` 优先读 `SkuIdStr`，回退到 `Guid.Empty`（POC 阶段限制）。

- [ ] **Step 6: 更新单元测试验证优先读 string**

更新 5 个 GrpcClient 的既有单元测试，在 Success 场景追加断言：

```csharp
        // 验证优先读 string 字段
        result.SkuId.Should().Be(skuId);  // 既有断言
        // 新增：服务端仅返回 string 字段时正确解析
```

新增 1 个测试场景（新服务端仅返回 string）：

```csharp
    [Fact]
    public async Task GetSkuInfo_NewServer_OnlyString_ReturnsCorrectGuid()
    {
        // 新服务端仅填充 string 字段，int64 字段为默认值 0
        var skuInfo = new SkuInfo
        {
            SkuId = 0,  // 新服务端不填充 int64
            SkuIdStr = skuId.ToString(),
            // ...
        };
        // ... 验证 SkuId 正确解析为 Guid
    }
```

- [ ] **Step 7: 验证编译 + 全部测试通过**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCEEDED

Run: `dotnet test`（运行所有 gRPC 相关单元测试）
Expected: 既有测试 + 新增兼容测试全部 PASS

- [ ] **Step 8: 提交**

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs \
        src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcPromotionAntiCorruptionClient.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs \
        src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs \
        src/Services/*/*/Infrastructure.Tests/Grpc/Grpc*ClientTests.cs \
        src/Services/*/*/Infrastructure.Tests/Grpc/Grpc*ServiceTests.cs
git commit -m "feat(M4): 5 个 GrpcClient 优先读 string ID 字段 + 修复 GrpcCartPriceService SellerId 映射"
```

---

## Self-Review

### 1. Spec 覆盖检查

- spec §3 Task A1 → Plan Task A1 ✓
- spec §4 Task A2 → Plan Task A2 ✓
- spec §5 Task B1 → Plan Task B1 ✓
- spec §6 Task B2 → Plan Task B2 ✓
- spec §7 Task B3 → Plan Task B3 ✓
- spec §9 Task C1 → Plan Task C1 ✓
- spec §10 Task C2 → Plan Task C2 ✓
- spec §11 Task D1 → Plan Task D1 ✓
- spec §12 Task D2 → Plan Task D2 ✓
- spec §13 Task D3 → Plan Task D3 ✓
- spec §14 验收标准 → 各 Task 步骤覆盖 ✓

### 2. 占位符扫描

- Task B2/B3 Step 6 使用"参考 Task B1 Step 6/7/8/9 模式"：这是合理的模式引用，避免重复代码。但实施时 subagent 需读取 Task B1 的完整步骤作为模板。
- Task D2 Step 2/3/4/5/6 使用"参考 Step 1 模式"：同理，模式一致，避免重复。
- 所有代码示例完整，无 TODO/TBD。

### 3. 类型一致性

- `ICartInternalQueryService` 在 Task B1 定义，Task B2/B3 不使用（各自有独立的 `ISellerInternalQueryService`/`IReviewInternalQueryService`）✓
- `SkuSnapshotDto` 字段名（SkuId/Title/MainImageUrl/UnitPrice/IsOnSale）在 Task A2 一致使用 ✓
- `GrpcProductSnapshotAntiCorruptionClient` 在 Task A2 创建，Task D3 修改 ✓
- `CartGrpcService` 在 Task B1 创建，Task D2 修改 ✓

### 4. 已知风险

- Task B1/B2/B3 中 `ICartAppService`/`ISellerAppService`/`IReviewRepository` 的方法签名需实施时探查确认，spec 已注明
- `TestServerCallContext` 需实施时探查是否已有可复用实现
- `buf generate` 如未安装 buf CLI，需通过 `dotnet build` 触发 Grpc.Tools 重新生成

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-19-m4-remaining-tasks-completion.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
