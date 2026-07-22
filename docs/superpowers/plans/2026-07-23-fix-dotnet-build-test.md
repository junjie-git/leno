# 修复 dotnet build + dotnet test 全量编译错误实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 Leno.slnx 解决方案中全部编译错误（原始 build 输出 54 行，归并为 50 个独立错误），使 `dotnet build Leno.slnx` 和 `dotnet test Leno.slnx` 全部通过。

**Architecture:** .NET 10 微服务 DDD 架构，12 个限界上下文 + BuildingBlocks + ApiGateway。错误分布于 15 个集群，根因包括：缺少 using 指令、.NET 10 API 变更、架构违规（Application 引用 Infrastructure）、gRPC 生成代码过期、测试语法错误、迁移委托推断失败。

**Tech Stack:** .NET 10.0.302 SDK, EF Core 10.0.0, MassTransit 8.3.6, StackExchange.Redis 2.8.16, xUnit 2.9.0, Moq 4.20.72, FluentAssertions 7.0.0, Grpc.Tools 2.65.0, Google.Protobuf 3.27.0

---

## 错误分布概览

| 集群 | 限界上下文 | 错误数 | 根因类型 |
|------|-----------|--------|---------|
| 1 | BuildingBlocks.Infrastructure.Tests | 3 | 缺少 using + 接口方法未实现 |
| 2 | ApiGateway | 1 | 缺少 using |
| 3 | PointsMembership.Infrastructure | 1 | 引用不存在的命名空间 |
| 4 | Cart.Infrastructure.Tests | 2 | 缺少 using |
| 5 | SellerShop.Infrastructure.Tests | 1 | 缺少 using |
| 6 | Product.Infrastructure.Tests | 9 | 缺少 using |
| 7 | Product.Api.Tests | 4 | 缺少 using |
| 8 | Payment.Infrastructure | 1 | 缺少 using + 类型名错误 |
| 9 | UserAuth.Infrastructure | 5 | 缺少 using + .NET 10 API 移除 |
| 10 | UserAuth.Domain.Tests | 3 | Moq Verify 语法错误 |
| 11 | UserAuth.Application.Tests | 3 | 参数缺失 + 类型不匹配 |
| 12 | Order.Infrastructure | 2 | 迁移 bool? 委托推断 |
| 13 | Order.Application.Tests | 3 | 多余 > 字符 |
| 14 | SystemAdmin.Application | 6 | 架构违规（Application 引用 Infrastructure） |
| 15 | ReviewAfterSales.Infrastructure | 6 | 静态类做类型参数 + gRPC 代码过期 |
| **合计** | | **50** | （原始 build 输出 54 行错误，其中 4 行为同一根因的级联 CS0246/CS0103，已合并至对应集群） |

## 文件结构映射

### 需修改的文件（按任务顺序）

| 任务 | 文件 | 职责 | 修改类型 |
|------|------|------|---------|
| T1 | `src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/CacheServiceInvalidatePatternPrefixTests.cs` | 缓存失效测试 | 添加 using |
| T2 | `src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventConsumerAtomicityTests.cs` | 集成事件消费原子性测试 | 添加 using |
| T3 | `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/CircuitBreakerStateHotReloadTests.cs` | 熔断器热重载测试 | 补充接口方法 |
| T4 | `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs` | Redis 滑窗限流器 | 添加 using |
| T5 | `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs` | 订单事件消费者 | 删除无效 using |
| T6 | `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartSkuIndexIntegrationTests.cs` | 购物车 SKU 索引集成测试 | 添加 using |
| T7 | `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelBuilderTests.cs` | 店铺仪表盘读模型测试 | 添加 using |
| T8 | `src/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductReadModelSkusTests.cs` | 商品读模型 SKU 测试 | 添加 using |
| T9 | `src/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductReadModelSyncConsumerCurrenciesTests.cs` | 商品读模型同步消费币种测试 | 添加 using |
| T10 | `src/Services/Product/Leno.Product.Api.Tests/SearchControllerCqrsTests.cs` | 搜索控制器 CQRS 测试 | 添加 using |
| T11 | `src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs` | 微信支付通知处理器 | 添加 using + 修复类型名 |
| T12 | `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | UserAuth DI 注册 | 添加 using |
| T13 | `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs` | 内存 RefreshToken 存储 | 重构枚举逻辑 |
| T14 | `src/Services/UserAuth/Leno.UserAuth.Domain.Tests/UserTests.cs` | User 领域测试 | 修复 Moq Verify 语法 |
| T15 | `src/Services/UserAuth/Leno.UserAuth.Application.Tests/SecureTokenGeneratorTests.cs` | 安全令牌生成测试 | 修复 char→string |
| T16 | `src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAdminAppServiceTests.cs` | 用户管理应用服务测试 | 补充构造函数参数 |
| T17 | `src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs` | 用户应用服务测试 | 修复 Task→Task\<bool\> |
| T18 | `src/Services/Order/Leno.Order.Infrastructure/Migrations/OrderDbContextModelSnapshot.cs` | EF Core 模型快照 | 修复 bool? 委托 |
| T19 | `src/Services/Order/Leno.Order.Infrastructure/Migrations/20260722000002_AddOrderRowVersionAndSoftDelete.Designer.cs` | 迁移设计器 | 修复 bool? 委托 |
| T20 | `src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs` | 订单 Saga 编排器测试 | 删除多余 > 字符 |
| T21-T24 | `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Abstractions/IFeatureFlagCache.cs` (新建), `ISystemConfigCache.cs` (新建), `FeatureFlagAppService.cs`, `SystemConfigAppService.cs` | SystemAdmin 架构修复 | 引入接口抽象 |
| T25 | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/FeatureFlagCache.cs` | 特性开关缓存 | 实现接口 |
| T26 | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/SystemConfigCache.cs` | 系统配置缓存 | 实现接口 |
| T27 | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | SystemAdmin DI 注册 | 注册接口映射 |
| T28 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | ReviewAfterSales DI 注册 | 修复静态类类型参数 |
| T29 | `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/Order.cs` | gRPC 生成代码 | 重新生成 |

---

## Phase 1: 快速修复 — 缺少 using 指令（T1-T10）

### Task 1: 修复 CacheServiceInvalidatePatternPrefixTests.cs — 添加 IBloomFilter using

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/CacheServiceInvalidatePatternPrefixTests.cs:1-4`

- [ ] **Step 1: 添加 using 指令**

在文件第 1 行（`using Leno.Infrastructure.Caching;`）之前添加：

```csharp
using Leno.Infrastructure.Abstractions;
```

修改后的 using 区（第 1-5 行）应为：
```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --no-incremental`
Expected: 3 个错误减少为 2 个（CS0246 IBloomFilter 消除）

- [ ] **Step 3: Commit**

```bash
git add src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/CacheServiceInvalidatePatternPrefixTests.cs
git commit -m "fix(buildingblocks): 添加 IBloomFilter using 指令修复 CS0246"
```

---

### Task 2: 修复 IntegrationEventConsumerAtomicityTests.cs — 添加 MassTransit using

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventConsumerAtomicityTests.cs:1-6`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using MassTransit;`。修改后的 using 区应为：
```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --no-incremental`
Expected: 错误减少为 1 个（CS0246 ConsumeContext 消除）

- [ ] **Step 3: Commit**

```bash
git add src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventConsumerAtomicityTests.cs
git commit -m "fix(buildingblocks): 添加 MassTransit using 指令修复 ConsumeContext CS0246"
```

---

### Task 3: 修复 CircuitBreakerStateHotReloadTests.cs — 补充 IOptionsMonitor.Get 方法

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/CircuitBreakerStateHotReloadTests.cs:19-28`

- [ ] **Step 1: 添加 Get 方法实现**

在 `MutableOptionsMonitor` 类中，第 28 行 `OnChange` 方法之后添加 `Get` 方法：

```csharp
public AntiCorruptionOptions Get(string? name) => CurrentValue;
```

完整的 `MutableOptionsMonitor` 类应为：
```csharp
private sealed class MutableOptionsMonitor : IOptionsMonitor<AntiCorruptionOptions>
{
    public AntiCorruptionOptions CurrentValue { get; set; } = new();

    public AntiCorruptionOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<AntiCorruptionOptions, string?> listener) => null;
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --no-incremental`
Expected: BuildingBlocks.Infrastructure.Tests 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/CircuitBreakerStateHotReloadTests.cs
git commit -m "fix(buildingblocks): 补充 MutableOptionsMonitor.Get 方法实现 IOptionsMonitor 接口"
```

---

### Task 4: 修复 RedisSlidingWindowRateLimiter.cs — 添加 NullLogger using

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs:1-3`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using Microsoft.Extensions.Logging.Abstractions;`。修改后的 using 区应为：
```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using System.Threading.RateLimiting;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj --no-incremental`
Expected: ApiGateway 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs
git commit -m "fix(apigateway): 添加 NullLogger using 指令修复 CS0103"
```

---

### Task 5: 修复 OrderEventConsumer.cs — 删除无效 using

**Files:**
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs:4`

- [ ] **Step 1: 删除第 4 行无效 using**

删除以下行：
```csharp
using Leno.PointsMembership.Domain.Services;
```

修改后的 using 区（第 1-9 行）应为：
```csharp
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Leno.PointsMembership.Infrastructure.csproj --no-incremental`
Expected: PointsMembership.Infrastructure 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs
git commit -m "fix(points): 删除 OrderEventConsumer 中不存在的 Domain.Services using"
```

---

### Task 6: 修复 CartSkuIndexIntegrationTests.cs — 添加 IIntegrationEvent using

**Files:**
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartSkuIndexIntegrationTests.cs:1-10`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using Leno.SharedContracts.Events;`。修改后的 using 区应为：
```csharp
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --no-incremental`
Expected: Cart.Infrastructure.Tests 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartSkuIndexIntegrationTests.cs
git commit -m "fix(cart): 添加 IIntegrationEvent using 指令修复 CS0246 和 CS0738"
```

---

### Task 7: 修复 ShopDashboardReadModelBuilderTests.cs — 添加 ILogger using

**Files:**
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelBuilderTests.cs:1-6`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using Microsoft.Extensions.Logging;`。修改后的 using 区应为：
```csharp
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Infrastructure.ReadModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --no-incremental`
Expected: SellerShop.Infrastructure.Tests 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelBuilderTests.cs
git commit -m "fix(sellershop): 添加 ILogger using 指令修复 CS0246"
```

---

### Task 8: 修复 ProductReadModelSkusTests.cs — 添加 ProductImage/SkuSpec using

**Files:**
- Modify: `src/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductReadModelSkusTests.cs:1-10`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using Leno.Product.Domain.ValueObjects;`。修改后的 using 区应为：
```csharp
using System.Reflection;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application.Queries;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj --no-incremental`
Expected: Product.Infrastructure.Tests 项目错误数减少（ProductReadModelSkusTests 文件的 CS0246/CS0103 消除，剩余错误来自 ProductReadModelSyncConsumerCurrenciesTests.cs，由 Task 9 修复）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductReadModelSkusTests.cs
git commit -m "fix(product): ProductReadModelSkusTests 添加 ProductImage/SkuSpec using 修复 CS0246 和 CS0103"
```

---

### Task 9: 修复 ProductReadModelSyncConsumerCurrenciesTests.cs — 添加 ProductImage/SkuSpec using

**Files:**
- Modify: `src/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductReadModelSyncConsumerCurrenciesTests.cs:1-10`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using Leno.Product.Domain.ValueObjects;`。修改后的 using 区应为：

```csharp
using System.Reflection;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application.Queries;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj --no-incremental`
Expected: Product.Infrastructure.Tests 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductReadModelSyncConsumerCurrenciesTests.cs
git commit -m "fix(product): ProductReadModelSyncConsumer 添加 ProductImage/SkuSpec using 修复 CS0246 和 CS0103"
```

---

### Task 10: 修复 SearchControllerCqrsTests.cs — 添加 SearchController using

**Files:**
- Modify: `src/Services/Product/Leno.Product.Api.Tests/SearchControllerCqrsTests.cs:1-7`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using Leno.Product.Api.Controllers;`。修改后的 using 区应为：
```csharp
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Product.Api.Controllers;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Queries;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Product/Leno.Product.Api.Tests/Leno.Product.Api.Tests.csproj --no-incremental`
Expected: Product.Api.Tests 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Product/Leno.Product.Api.Tests/SearchControllerCqrsTests.cs
git commit -m "fix(product): 添加 SearchController using 指令修复 CS0246"
```

---

## Phase 2: 类型名与语法修复（T11-T20）

### Task 11: 修复 WeChatPayNotifyHandler.cs — NullLoggerFactory 类型名

**Files:**
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs:1-43`

- [ ] **Step 1: 添加 using 并修复类型名**

第 5 行后添加 using 指令：
```csharp
using Microsoft.Extensions.Logging.Abstractions;
```

第 43 行将 `InternalNullLoggerFactory.CreateLogger<WeChatPayNotifyHandler>()` 改为 `NullLogger<WeChatPayNotifyHandler>.Instance`。

修改后的 using 区（第 1-6 行）应为：
```csharp
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
```

修改后的第 43 行应为：
```csharp
        _logger = logger ?? NullLogger<WeChatPayNotifyHandler>.Instance;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Payment/Leno.Payment.Infrastructure/Leno.Payment.Infrastructure.csproj --no-incremental`
Expected: Payment.Infrastructure 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs
git commit -m "fix(payment): 修复 InternalNullLoggerFactory 为 NullLogger<T> 并添加 using 指令"
```

---

### Task 12: 修复 UserAuth ServiceCollectionExtensions.cs — 添加 StackExchange.Redis using

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:1-21`

- [ ] **Step 1: 添加 using 指令**

在文件 using 区添加 `using StackExchange.Redis;`。在第 21 行（`using Microsoft.Extensions.Options;`）之后添加：
```csharp
using StackExchange.Redis;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Infrastructure/Leno.UserAuth.Infrastructure.csproj --no-incremental`
Expected: 5 个错误减少为 1 个（CS0246 IConnectionMultiplexer 消除，CS1579 MemoryCache.GetEnumerator 仍存在）

- [ ] **Step 3: Commit**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "fix(userauth): 添加 StackExchange.Redis using 修复 IConnectionMultiplexer CS0246"
```

---

### Task 13: 修复 InMemoryRefreshTokenStore.cs — 替换 MemoryCache 枚举为 ConcurrentDictionary 索引

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs`

- [ ] **Step 1: 添加 ConcurrentDictionary 索引字段**

在 using 区添加：
```csharp
using System.Collections.Concurrent;
```

在类中（`private readonly MemoryCache _store;` 字段后）添加：
```csharp
private readonly ConcurrentDictionary<string, Guid> _tokenIndex = new();
```

- [ ] **Step 2: 在 IssueAsync 中维护索引**

在 `IssueAsync` 方法中，`_store.Set(...)` 调用之后添加：
```csharp
_tokenIndex[token] = userId;
```

- [ ] **Step 3: 在 ValidateAndRotateAsync 中维护索引**

在 `ValidateAndRotateAsync` 方法中，`_store.Remove(refreshToken);` 调用之后添加：
```csharp
_tokenIndex.TryRemove(refreshToken, out _);
```

- [ ] **Step 4: 重写 RevokeAllAsync 的枚举逻辑**

将 `RevokeAllAsync` 中的 `foreach (var kvp in _store)` 替换为遍历 `_tokenIndex`：

```csharp
var keysToRemove = _tokenIndex
    .Where(kvp => kvp.Value == userId)
    .Select(kvp => kvp.Key)
    .ToList();

foreach (var key in keysToRemove)
{
    _store.Remove(key);
    _tokenIndex.TryRemove(key, out _);
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Infrastructure/Leno.UserAuth.Infrastructure.csproj --no-incremental`
Expected: UserAuth.Infrastructure 项目编译通过（0 个错误）

- [ ] **Step 6: Commit**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs
git commit -m "fix(userauth): 用 ConcurrentDictionary 索引替代 MemoryCache.GetEnumerator 适配 .NET 10"
```

---

### Task 14: 修复 UserTests.cs — Moq Verify 括号位置

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Domain.Tests/UserTests.cs:135,163,174`

- [ ] **Step 1: 修复第 135 行 Moq Verify 语法**

将：
```csharp
_hasherMock.Verify(h => h.Verify("AnyPassword123", It.Is<string>(s => s.StartsWith("$2a$12$")), Times.Once));
```

改为（`Times.Once` 移到 lambda 外部作为 Moq Verify 第二参数）：
```csharp
_hasherMock.Verify(h => h.Verify("AnyPassword123", It.Is<string>(s => s.StartsWith("$2a$12$"))), Times.Once);
```

- [ ] **Step 2: 修复第 163 行 Moq Verify 语法**

将：
```csharp
_hasherMock.Verify(h => h.Verify("\x00", It.Is<string>(s => s.StartsWith("$2a$12$")), Times.Once));
```

改为：
```csharp
_hasherMock.Verify(h => h.Verify("\x00", It.Is<string>(s => s.StartsWith("$2a$12$"))), Times.Once);
```

- [ ] **Step 3: 修复第 174 行 Moq Verify 语法**

将：
```csharp
_hasherMock.Verify(h => h.Verify("\x00", It.Is<string>(s => s.StartsWith("$2a$12$")), Times.Once));
```

改为：
```csharp
_hasherMock.Verify(h => h.Verify("\x00", It.Is<string>(s => s.StartsWith("$2a$12$"))), Times.Once);
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Domain.Tests/Leno.UserAuth.Domain.Tests.csproj --no-incremental`
Expected: UserAuth.Domain.Tests 项目编译通过（0 个错误）

- [ ] **Step 5: Commit**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Domain.Tests/UserTests.cs
git commit -m "fix(userauth): 修复 Moq Verify 括号位置消除 IPasswordHasher.Verify 三参数错误"
```

---

### Task 15: 修复 SecureTokenGeneratorTests.cs — char 转 string

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Application.Tests/SecureTokenGeneratorTests.cs:35`

- [ ] **Step 1: 修复 char 到 string 转换**

将第 35 行：
```csharp
        AllowedChars.Should().Contain(c,
```

改为：
```csharp
        AllowedChars.Should().Contain(c.ToString(),
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Application.Tests/Leno.UserAuth.Application.Tests.csproj --no-incremental`
Expected: UserAuth.Application.Tests 项目错误数 3 → 2（SecureTokenGeneratorTests 的 CS1503 char→string 消除）

- [ ] **Step 3: Commit**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/SecureTokenGeneratorTests.cs
git commit -m "fix(userauth): SecureTokenGenerator 测试 char.ToString() 修复 Contain 类型不匹配"
```

---

### Task 16: 修复 UserAdminAppServiceTests.cs — 补充 IJwtRevocationService 参数

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAdminAppServiceTests.cs`

- [ ] **Step 1: 添加 Mock 字段**

在测试类字段区（第 18-22 行附近，其他 Mock 字段之后）添加：
```csharp
private readonly Mock<IJwtRevocationService> _jwtRevocationMock = new();
```

- [ ] **Step 2: 补充构造函数第 4 参数**

在 `CreateSut()` 方法中，`_refreshTokenStoreMock.Object,` 之后、`_unitOfWorkMock.Object` 之前插入 `_jwtRevocationMock.Object,`：

```csharp
private UserAdminAppService CreateSut()
{
    return new UserAdminAppService(
        _userRepositoryMock.Object,
        _auditLogRepositoryMock.Object,
        _refreshTokenStoreMock.Object,
        _jwtRevocationMock.Object,
        _unitOfWorkMock.Object);
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Application.Tests/Leno.UserAuth.Application.Tests.csproj --no-incremental`
Expected: UserAuth.Application.Tests 项目错误数 2 → 1（UserAdminAppServiceTests 的 CS7036 构造函数参数缺失消除）

- [ ] **Step 4: Commit**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAdminAppServiceTests.cs
git commit -m "fix(userauth): UserAdminAppService 测试补充 IJwtRevocationService mock 参数"
```

---

### Task 17: 修复 UserAppServiceTests.cs — Task→Task\<bool\> 类型不匹配

**Files:**
- Modify: `src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs:304-313,527-529`

- [ ] **Step 1: 修复第 304-313 行 SaveEntitiesAsync 返回值**

将第 312 行：
```csharp
                return Task.CompletedTask;
```

改为：
```csharp
                return Task.FromResult(true);
```

- [ ] **Step 2: 修复第 527-529 行 SaveEntitiesAsync 返回值**

将第 529 行：
```csharp
            .Returns(Task.CompletedTask);
```

改为：
```csharp
            .ReturnsAsync(true);
```

- [ ] **Step 3: 全文件搜索其他 SaveEntitiesAsync + Task.CompletedTask 组合**

Run: `findstr /N "Task.CompletedTask" src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`

如有其他与 `SaveEntitiesAsync` 关联的 `Task.CompletedTask`，统一改为 `ReturnsAsync(true)` 或 `Task.FromResult(true)`。

注意：第 526 行 `_userRepoMock.Setup(r => r.UpdateAsync(...)).Returns(Task.CompletedTask)` 中 `UpdateAsync` 返回 `Task`（非 `Task<bool>`），**不需要修改**。

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/Services/UserAuth/Leno.UserAuth.Application.Tests/Leno.UserAuth.Application.Tests.csproj --no-incremental`
Expected: UserAuth.Application.Tests 项目编译通过（0 个错误）

- [ ] **Step 5: Commit**

```bash
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): SaveEntitiesAsync mock 返回 ReturnsAsync(true) 适配 Task<bool>"
```

---

### Task 18: 修复 OrderDbContextModelSnapshot.cs — bool? 委托推断

**Files:**
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Migrations/OrderDbContextModelSnapshot.cs:348`

- [ ] **Step 1: 修复 HasQueryFilter 委托**

将第 348 行：
```csharp
                    b.HasQueryFilter(e => !((bool?)e.IsDeleted));
```

改为：
```csharp
                    b.HasQueryFilter(e => !e.IsDeleted);
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Order/Leno.Order.Infrastructure/Leno.Order.Infrastructure.csproj --no-incremental`
Expected: 2 个错误减少为 1 个（此文件修复后仅剩 Designer.cs）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/OrderDbContextModelSnapshot.cs
git commit -m "fix(order): 移除 HasQueryFilter 中 bool? 强制转换修复 CS8917 委托推断"
```

---

### Task 19: 修复 AddOrderRowVersionAndSoftDelete.Designer.cs — bool? 委托推断

**Files:**
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Migrations/20260722000002_AddOrderRowVersionAndSoftDelete.Designer.cs:351`

- [ ] **Step 1: 修复 HasQueryFilter 委托**

将第 351 行：
```csharp
                    b.HasQueryFilter(e => !((bool?)e.IsDeleted));
```

改为：
```csharp
                    b.HasQueryFilter(e => !e.IsDeleted);
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/Order/Leno.Order.Infrastructure/Leno.Order.Infrastructure.csproj --no-incremental`
Expected: Order.Infrastructure 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/20260722000002_AddOrderRowVersionAndSoftDelete.Designer.cs
git commit -m "fix(order): 移除迁移 Designer 中 bool? 强制转换修复 CS8917 委托推断"
```

---

### Task 20: 修复 OrderSagaOrchestratorTests.cs — 删除多余 > 字符

**Files:**
- Modify: `src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs:43,87,126`

**根因说明：** 三处 `It.IsAny<IReadOnlyDictionary<Guid, decimal>>` 调用末尾多了一个 `>` 字符，导致出现 `>>>()`（三个右尖括号 + 空括号对），C# 编译器报 CS1525。正确写法应为 `>>() `（两个右尖括号 + 空括号对）：第一个 `>` 闭合 `decimal` 泛型参数，第二个 `>` 闭合 `IReadOnlyDictionary<...>` 泛型，紧接着 `()` 调用方法。

- [ ] **Step 1: 修复第 43 行多余 > 字符**

将第 43 行末尾的 `IReadOnlyDictionary<Guid, decimal>>>()` （注意这里有三个连续的 `>`）改为 `IReadOnlyDictionary<Guid, decimal>>()`（两个连续的 `>`）。

修改后的第 43 行完整内容：
```csharp
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
```

- [ ] **Step 2: 修复第 87 行多余 > 字符**

将第 87 行末尾的 `IReadOnlyDictionary<Guid, decimal>>>()`（三个 `>`）改为 `IReadOnlyDictionary<Guid, decimal>>()`（两个 `>`）。

修改后的第 87 行完整内容：
```csharp
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
```

- [ ] **Step 3: 修复第 126 行多余 > 字符**

将第 126 行末尾的 `IReadOnlyDictionary<Guid, decimal>>>()`（三个 `>`）改为 `IReadOnlyDictionary<Guid, decimal>>()`（两个 `>`）。

修改后的第 126 行完整内容：
```csharp
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj --no-incremental`
Expected: Order.Application.Tests 项目编译通过（0 个错误）

- [ ] **Step 5: Commit**

```bash
git add src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
git commit -m "fix(order): 删除 It.IsAny<IReadOnlyDictionary> 多余 > 字符修复 CS1525"
```

---

## Phase 3: SystemAdmin 架构修复（T21-T27）

### Task 21: 创建 IFeatureFlagCache 接口

**Files:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Abstractions/IFeatureFlagCache.cs`

- [ ] **Step 1: 创建接口文件**

```csharp
namespace Leno.SystemAdmin.Application.Abstractions;

/// <summary>
/// 特性开关缓存抽象，供应用层读侧加速。
/// 实现位于基础设施层（Redis），写操作后主动失效缓存避免脏读。
/// </summary>
public interface IFeatureFlagCache
{
    /// <summary>按开关键读取缓存值，缓存缺失返回 null。</summary>
    Task<string?> GetAsync(string flagKey, CancellationToken ct = default);

    /// <summary>写入开关缓存并刷新 TTL。</summary>
    Task SetAsync(string flagKey, string value, CancellationToken ct = default);

    /// <summary>按开关键删除缓存。</summary>
    Task RemoveAsync(string flagKey, CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Application/Abstractions/IFeatureFlagCache.cs
git commit -m "feat(systemadmin): 引入 IFeatureFlagCache 接口解耦 Application 对 Infrastructure 的依赖"
```

---

### Task 22: 创建 ISystemConfigCache 接口

**Files:**
- Create: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Abstractions/ISystemConfigCache.cs`

- [ ] **Step 1: 创建接口文件**

```csharp
namespace Leno.SystemAdmin.Application.Abstractions;

/// <summary>
/// 系统配置缓存抽象，供应用层读侧加速。
/// 实现位于基础设施层（Redis），写操作后主动失效缓存避免脏读。
/// </summary>
public interface ISystemConfigCache
{
    /// <summary>按配置键读取缓存值，缓存缺失返回 null。</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>写入配置缓存并刷新 TTL。</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>按配置键删除缓存。</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Application/Abstractions/ISystemConfigCache.cs
git commit -m "feat(systemadmin): 引入 ISystemConfigCache 接口解耦 Application 对 Infrastructure 的依赖"
```

---

### Task 23: 修改 FeatureFlagAppService 使用接口

**Files:**
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs`

- [ ] **Step 1: 替换 using 和字段类型**

将第 6 行 `using Leno.SystemAdmin.Infrastructure.Cache;` 改为 `using Leno.SystemAdmin.Application.Abstractions;`。

将第 23 行 `private readonly FeatureFlagCache _cache;` 改为 `private readonly IFeatureFlagCache _cache;`。

将第 30 行构造函数参数 `FeatureFlagCache cache` 改为 `IFeatureFlagCache cache`。

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/SystemAdmin/Leno.SystemAdmin.Application/Leno.SystemAdmin.Application.csproj --no-incremental`
Expected: 6 个错误减少为 4 个（FeatureFlagAppService 的 3 个错误消除）

- [ ] **Step 3: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs
git commit -m "fix(systemadmin): FeatureFlagAppService 依赖 IFeatureFlagCache 接口而非 Infrastructure 具体类"
```

---

### Task 24: 修改 SystemConfigAppService 使用接口

**Files:**
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs`

- [ ] **Step 1: 替换 using 和字段类型**

将第 5 行 `using Leno.SystemAdmin.Infrastructure.Cache;` 改为 `using Leno.SystemAdmin.Application.Abstractions;`。

将第 23 行 `private readonly SystemConfigCache _cache;` 改为 `private readonly ISystemConfigCache _cache;`。

将第 29 行构造函数参数 `SystemConfigCache cache` 改为 `ISystemConfigCache cache`。

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/SystemAdmin/Leno.SystemAdmin.Application/Leno.SystemAdmin.Application.csproj --no-incremental`
Expected: SystemAdmin.Application 项目编译通过（0 个错误）

- [ ] **Step 3: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs
git commit -m "fix(systemadmin): SystemConfigAppService 依赖 ISystemConfigCache 接口而非 Infrastructure 具体类"
```

---

### Task 25: FeatureFlagCache 实现接口

**Files:**
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/FeatureFlagCache.cs:10`

- [ ] **Step 1: 添加接口实现**

在类声明中添加 `: IFeatureFlagCache`。在第 1 行添加 `using Leno.SystemAdmin.Application.Abstractions;`。

修改后的类声明（第 10-11 行）应为：
```csharp
public sealed class FeatureFlagCache : IFeatureFlagCache
```

修改后的 using 区（第 1-3 行）应为：
```csharp
using Leno.SystemAdmin.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Leno.SystemAdmin.Infrastructure.csproj --no-incremental`
Expected: 编译通过

- [ ] **Step 3: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/FeatureFlagCache.cs
git commit -m "fix(systemadmin): FeatureFlagCache 实现 IFeatureFlagCache 接口"
```

---

### Task 26: SystemConfigCache 实现接口

**Files:**
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/SystemConfigCache.cs:10`

- [ ] **Step 1: 添加接口实现**

在类声明中添加 `: ISystemConfigCache`。在第 1 行添加 `using Leno.SystemAdmin.Application.Abstractions;`。

修改后的类声明（第 10-11 行）应为：
```csharp
public sealed class SystemConfigCache : ISystemConfigCache
```

修改后的 using 区（第 1-3 行）应为：
```csharp
using Leno.SystemAdmin.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Leno.SystemAdmin.Infrastructure.csproj --no-incremental`
Expected: 编译通过

- [ ] **Step 3: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Cache/SystemConfigCache.cs
git commit -m "fix(systemadmin): SystemConfigCache 实现 ISystemConfigCache 接口"
```

---

### Task 27: 注册 SystemAdmin 缓存接口 DI 映射

**Files:**
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:79-80`

- [ ] **Step 1: 替换具体类注册为接口注册**

将第 79-80 行：

```csharp
        services.AddSingleton<SystemConfigCache>();
        services.AddSingleton<FeatureFlagCache>();
```

替换为（保持 Singleton 生命周期不变，增加接口映射）：

```csharp
        services.AddSingleton<SystemConfigCache>();
        services.AddSingleton<FeatureFlagCache>();
        services.AddSingleton<ISystemConfigCache>(sp => sp.GetRequiredService<SystemConfigCache>());
        services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>());
```

说明：保留具体类注册（内部其他服务可能直接依赖具体类），同时增加接口注册使 Application 层可通过构造函数注入接口。`using Leno.SystemAdmin.Application.Abstractions;` 已存在于第 5 行，无需额外添加。

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Leno.SystemAdmin.Infrastructure.csproj --no-incremental`
Expected: 编译通过

- [ ] **Step 3: Commit**

```bash
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "fix(systemadmin): 注册 IFeatureFlagCache/ISystemConfigCache 接口映射到 DI 容器"
```

---

## Phase 4: ReviewAfterSales 修复（T28-T29）

### Task 28: 修复 ServiceCollectionExtensions.cs — 静态类不能做类型参数

**Files:**
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:115,161`

- [ ] **Step 1: 修复第 115 行 ILogger 类型参数**

将第 115 行：
```csharp
                    sp.GetRequiredService<ILogger<ServiceCollectionExtensions>>(),
```

改为：
```csharp
                    sp.GetRequiredService<ILogger<GrpcDegradationWarningHostedService>>(),
```

- [ ] **Step 2: 修复第 161 行 ILogger 类型参数**

将第 161 行：
```csharp
                    sp.GetRequiredService<ILogger<ServiceCollectionExtensions>>(),
```

改为：
```csharp
                    sp.GetRequiredService<ILogger<GrpcDegradationWarningHostedService>>(),
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Leno.ReviewAfterSales.Infrastructure.csproj --no-incremental`
Expected: 6 个错误减少为 4 个（CS0718 静态类类型参数 2 个错误消除）

- [ ] **Step 4: Commit**

```bash
git add src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "fix(reviewaftersales): 用 GrpcDegradationWarningHostedService 替代静态类做 ILogger 类型参数"
```

---

### Task 29: 重新生成 gRPC Order.cs — 补全 OrderLineId/SpuId 字段

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/Order.cs`

**根因分析：** proto 文件 `order.proto` 第 44-45 行定义了 `optional string order_line_id = 7;` 和 `optional string spu_id = 8;`，但预生成的 `Order.cs` 中 `OrderItem` 类缺少这两个字段及其 `HasOrderLineId`/`HasSpuId` 属性。项目已引用 `Grpc.Tools 2.65.0`，但未配置 `<Protobuf>` 项进行构建时生成。

- [ ] **Step 1: 定位 protoc 工具**

Run:
```powershell
$protoc = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\grpc.tools\2.65.0\tools" -Filter "protoc.exe" -Recurse | Select-Object -First 1
Write-Host $protoc.FullName
```

- [ ] **Step 2: 备份现有 Order.cs**

```powershell
Copy-Item "src\BuildingBlocks\Leno.SharedContracts.Grpc\Generated\Order.cs" "src\BuildingBlocks\Leno.SharedContracts.Grpc\Generated\Order.cs.bak"
```

- [ ] **Step 3: 使用 protoc 重新生成 Order.cs**

```powershell
$protoPath = "src\BuildingBlocks\Leno.SharedContracts\Protos"
$outPath = "src\BuildingBlocks\Leno.SharedContracts.Grpc\Generated"
& $protoc.FullName --csharp_out="$outPath" --proto_path="$protoPath" "$protoPath\order.proto"
```

这会生成新的 `Order.cs` 覆盖旧文件，包含 `OrderLineId`/`SpuId`/`HasOrderLineId`/`HasSpuId` 属性。

- [ ] **Step 4: 验证生成结果**

Run: `findstr /N "OrderLineId" src\BuildingBlocks\Leno.SharedContracts.Grpc\Generated\Order.cs`
Expected: 应输出多行匹配，确认 `OrderLineId` 属性已生成。

- [ ] **Step 5: 验证编译**

Run: `dotnet build src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj --no-incremental`
Expected: SharedContracts.Grpc 项目编译通过

然后验证 ReviewAfterSales.Infrastructure：
Run: `dotnet build src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Leno.ReviewAfterSales.Infrastructure.csproj --no-incremental`
Expected: ReviewAfterSales.Infrastructure 项目编译通过（0 个错误）

- [ ] **Step 6: 清理备份**

```powershell
Remove-Item "src\BuildingBlocks\Leno.SharedContracts.Grpc\Generated\Order.cs.bak"
```

- [ ] **Step 7: Commit**

```bash
git add src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/Order.cs
git commit -m "fix(grpc): 重新生成 Order.cs 补全 OrderLineId/SpuId 字段同步 proto 定义"
```

---

## Phase 5: 全量验证（T30）

### Task 30: 全量构建与测试验证

**Files:**
- None (verification only)

- [ ] **Step 1: 全量构建**

Run: `dotnet build Leno.slnx --no-incremental`
Expected: 0 个错误，0 个警告（或仅有已知的非阻塞警告）

- [ ] **Step 2: 如果构建仍有错误，逐一修复**

如果仍有错误，根据错误信息定位文件并修复。常见的遗留问题可能包括：
- 漏修的 using 指令
- 接口实现遗漏
- DI 注册不完整

修复后重新构建直到 0 错误。

- [ ] **Step 3: 全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: 所有测试通过（或仅有已知的跳过测试）

- [ ] **Step 4: 如果测试失败，分析并修复**

如果测试失败：
1. 记录失败测试的完全限定名和错误信息
2. 分析失败原因（可能是 mock 配置、断言值、异步时序等）
3. 修复测试代码
4. 重新运行失败的测试：`dotnet test Leno.slnx --no-build --filter "FullyQualifiedName~失败测试名"`

- [ ] **Step 5: 推送到远程仓库**

```bash
git push origin HEAD
```

- [ ] **Step 6: 最终验证**

Run: `dotnet build Leno.slnx && dotnet test Leno.slnx`
Expected: 构建成功 + 全部测试通过

---

## 附录：错误根因分类汇总

### A. 缺少 using 指令（24 个错误，占 44%）

| 文件 | 缺少的 using | 影响类型 |
|------|-------------|---------|
| CacheServiceInvalidatePatternPrefixTests.cs | `Leno.Infrastructure.Abstractions` | IBloomFilter |
| IntegrationEventConsumerAtomicityTests.cs | `MassTransit` | ConsumeContext<> |
| RedisSlidingWindowRateLimiter.cs | `Microsoft.Extensions.Logging.Abstractions` | NullLogger<T> |
| ServiceCollectionExtensions.cs (UserAuth) | `StackExchange.Redis` | IConnectionMultiplexer |
| ProductReadModelSkusTests.cs | `Leno.Product.Domain.ValueObjects` | ProductImage, SkuSpec |
| ProductReadModelSyncConsumerCurrenciesTests.cs | `Leno.Product.Domain.ValueObjects` | ProductImage, SkuSpec |
| SearchControllerCqrsTests.cs | `Leno.Product.Api.Controllers` | SearchController |
| CartSkuIndexIntegrationTests.cs | `Leno.SharedContracts.Events` | IIntegrationEvent |
| ShopDashboardReadModelBuilderTests.cs | `Microsoft.Extensions.Logging` | ILogger<> |

### B. .NET 10 API 变更（1 个错误）
- MemoryCache.GetEnumerator 在 .NET 10 中移除

### C. 架构违规（6 个错误）
- SystemAdmin.Application 引用 Infrastructure.Cache 具体类

### D. gRPC 生成代码过期（4 个错误）
- Order.cs 缺少 OrderLineId/SpuId 字段

### E. 测试代码语法错误（9 个错误）
- Moq Verify 括号位置错误（3 个）
- 多余 > 字符（3 个）
- char→string 转换（1 个）
- 构造函数参数缺失（1 个）
- Task→Task<bool> 类型不匹配（1 个）

### F. 迁移代码问题（2 个错误）
- EF Core 10 迁移生成器对 bool? 类型的委托推断失败

### G. 类型名错误（1 个错误）
- InternalNullLoggerFactory 不存在

### H. 静态类做类型参数（2 个错误）
- ILogger<ServiceCollectionExtensions> 中 ServiceCollectionExtensions 是 static class

### I. 无效命名空间引用（1 个错误）
- Leno.PointsMembership.Domain.Services 不存在

### J. 接口方法未实现（1 个错误）
- MutableOptionsMonitor 缺少 IOptionsMonitor<T>.Get(string?) 实现
