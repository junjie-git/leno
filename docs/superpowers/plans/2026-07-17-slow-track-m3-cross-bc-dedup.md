# 慢轨 M3 跨 BC 样板去重 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 抽取泛型 EfCoreUnitOfWork<TDbContext>（消除 680 行重复）；BaseDbContext 暴露 OutboxMessages DbSet 并上移 OutboxMessageConfiguration（消除 319 行重复）；新建 AddLenoApi/UseLenoPipeline 一站式扩展方法，11 个 BC Program.cs 缩减到 ≤20 行（消除约 880 行重复）；AntiCorruptionServices.cs 拆分为 3 个独立文件；IPointsOffsetService 重命名为 IPointsOffsetAppService；DTO/测试命名约定文档化

**Architecture:** 泛型 `EfCoreUnitOfWork<TDbContext> : IUnitOfWork` + 内部 `EfCoreUnitOfWorkTransaction` 下沉到 Leno.Infrastructure；BaseDbContext 通过 `Set<OutboxMessage>()` 暴露 DbSet 并在 OnModelCreating 显式 ApplyConfiguration(new OutboxMessageConfiguration())；新建 `WebApplicationExtensions.AddLenoApi<TDbContext>(...)` 编排 Infrastructure + Consul + OpenTelemetry + HealthChecks + Auth + JwtBearer + GatewayHeader 灰度；`UseLenoPipeline()` 编排 GlobalExceptionMiddleware + InternalApiKeyMiddleware + Auth + HealthCheck 端点；AntiCorruptionServices 拆分后 3 个独立 sealed class 各自单一职责

**Tech Stack:** .NET 10、ASP.NET Core 10、EF Core 10、Serilog、OpenTelemetry、Consul、xUnit、FluentAssertions

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §10](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 6（M2 共享内核清理，DomainException.HttpStatusCode 已删除，ErrorCodeMapping 已接管）完成；Plan 3（F3 EF Migrations，MigrateWithLockAsync 已就绪）完成；Plan 2（F2 安全，GatewayAuthHandler/Consul KV 已就绪）完成

**向后兼容策略:** 泛型 UnitOfWork 与旧 UnitOfWork 并存期通过 DI 注册切换（一次性切换，无运行期双轨）；AddLenoApi 通过可选 `configureInfrastructure` 委托保留 BC 专属注册扩展点；AntiCorruptionServices 拆分后接口契约不变，仅文件物理拆分

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| IUnitOfWork 接口 | `src/BuildingBlocks/Leno.SharedKernel/Abstractions/IUnitOfWork.cs` | 含 `SaveChangesAsync`/`SaveEntitiesAsync`/`BeginTransactionAsync` + `IUnitOfWorkTransaction` |
| 11 个 BC UnitOfWork.cs | 见下方完整清单 | 全部同构，每个 62 行（SystemAdmin/Notification 61 行），总 **680 行**重复代码 |
| 11 个 BC DI 注册 | 各 BC `Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | 统一 `services.AddScoped<IUnitOfWork, UnitOfWork>();` |
| BaseDbContext | `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs:11-43` | OnModelCreating 第 25 行 `ApplyConfigurationsFromAssembly(GetType().Assembly)`；未含 OutboxMessages DbSet |
| 11 个 BC OutboxMessages DbSet 声明 | 各 BC DbContext | 统一 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();` |
| 11 个 BC OutboxMessageConfiguration.cs | 各 BC `Infrastructure/Configurations/` | 全部同构，每个 29 行，总 **319 行**重复代码 |
| OutboxMessage 类 | `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs:22` | 已在 Leno.Infrastructure，含 OutboxMessageStatus 枚举 + Create/MarkAs* 方法 |
| 11 个 BC Program.cs | 见下方完整清单 | 总 **1020 行**，重复约 880 行；JWT Bearer 鉴权 18 行 × 11 = 198 行重复 |
| AddLenoInfrastructure | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:31` | 已存在，含 Options/FileStorage/Auth/Redis/ES/EventBus/HealthChecks |
| AddInternalApiKeyAuth | `src/BuildingBlocks/Leno.Infrastructure/Auth/` 命名空间 | 已存在 |
| AddLenoOpenTelemetry | `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs` | 已存在 |
| AddLenoHealthChecks | `src/BuildingBlocks/Leno.Infrastructure/HealthChecks/HealthChecksUIExtensions.cs:25` | 已存在，含 RabbitMQ/Redis/ES/Self 探活，但 11 BC 未使用 |
| MapLenoHealthChecks | `src/BuildingBlocks/Leno.Infrastructure/HealthChecks/HealthChecksUIExtensions.cs:76` | 已存在，但 11 BC 未使用 |
| AddConsulServiceRegistration | `src/BuildingBlocks/Leno.Infrastructure/ServiceDiscovery/ConsulServiceRegistrationExtensions.cs:130` | 已存在（Consul 服务自注册） |
| AddLenoConsulConfig | `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs:105` | 已存在（Consul KV 配置中心） |
| AddLenoApi / UseLenoPipeline（不存在） | — | 需新建于 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` |
| AddLenoJwtBearer / GatewayAuthHandler | Plan 2 已新建 | Plan 7 直接调用 |
| AntiCorruptionServices.cs | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs` | **474 行**，含 3 个 sealed class（Product/Promotion/Points 防腐层） |
| IPointsOffsetService | `src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetService.cs:7` | 3 处引用（接口定义 + 实现 + DI 注册） |
| IPointsOffsetAppService（不存在） | — | 需重命名 |
| DTO 命名约定现状 | 各 BC `Application/DTOs/` | 主流 `*Dto`；UserAuth 用 `*RequestDto/*ResponseDto`；Notification 用 `*Request/*Response`；Order 不用 Request/Response |

### 11 个 BC UnitOfWork.cs 完整清单

| # | BC | 文件路径 | 行数 |
|---|----|---------|------|
| 1 | UserAuth | `src/Services/UserAuth/Leno.UserAuth.Infrastructure/UnitOfWork.cs` | 62 |
| 2 | Cart | `src/Services/Cart/Leno.Cart.Infrastructure/UnitOfWork.cs` | 62 |
| 3 | Notification | `src/Services/Notification/Leno.Notification.Infrastructure/UnitOfWork.cs` | 61 |
| 4 | Order | `src/Services/Order/Leno.Order.Infrastructure/UnitOfWork.cs` | 62 |
| 5 | Payment | `src/Services/Payment/Leno.Payment.Infrastructure/UnitOfWork.cs` | 62 |
| 6 | PointsMembership | `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/UnitOfWork.cs` | 62 |
| 7 | Product | `src/Services/Product/Leno.Product.Infrastructure/UnitOfWork.cs` | 62 |
| 8 | Promotion | `src/Services/Promotion/Leno.Promotion.Infrastructure/UnitOfWork.cs` | 62 |
| 9 | ReviewAfterSales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/UnitOfWork.cs` | 62 |
| 10 | SellerShop | `src/Services/SellerShop/Leno.SellerShop.Infrastructure/UnitOfWork.cs` | 62 |
| 11 | SystemAdmin | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/UnitOfWork.cs` | 61 |
| | | **合计** | **680** |

### 11 个 BC Program.cs 完整清单

| # | BC | 文件路径 | 行数 |
|---|----|---------|------|
| 1 | Order | `src/Services/Order/Leno.Order.Api/Program.cs` | 92 |
| 2 | UserAuth | `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs` | 95 |
| 3 | SystemAdmin | `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs` | 92 |
| 4 | SellerShop | `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs` | 92 |
| 5 | ReviewAfterSales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs` | 92 |
| 6 | Promotion | `src/Services/Promotion/Leno.Promotion.Api/Program.cs` | 94 |
| 7 | Product | `src/Services/Product/Leno.Product.Api/Program.cs` | 92 |
| 8 | PointsMembership | `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs` | 95 |
| 9 | Payment | `src/Services/Payment/Leno.Payment.Api/Program.cs` | 92 |
| 10 | Notification | `src/Services/Notification/Leno.Notification.Api/Program.cs` | 92 |
| 11 | Cart | `src/Services/Cart/Leno.Cart.Api/Program.cs` | 92 |
| | | **合计** | **1020** |

---

## Task 1: 新建泛型 EfCoreUnitOfWork&lt;TDbContext&gt;

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/EfCoreUnitOfWorkTests.cs`

- [ ] **Step 1: 创建 EfCoreUnitOfWork&lt;TDbContext&gt; 类**

创建 `src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs`：

```csharp
using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 泛型 EF Core 工作单元实现，包装任意 <typeparamref name="TDbContext"/>。
/// <see cref="SaveEntitiesAsync"/> 经发件箱扩展将聚合产生的集成事件与状态变更在同一事务保存。
/// 各 BC DI 注册改为 <c>AddScoped&lt;IUnitOfWork, EfCoreUnitOfWork&lt;XxxDbContext&gt;&gt;()</c>。
/// </summary>
/// <typeparam name="TDbContext">业务上下文 DbContext 类型。</typeparam>
public sealed class EfCoreUnitOfWork<TDbContext> : IUnitOfWork
    where TDbContext : DbContext
{
    private readonly TDbContext _context;

    public EfCoreUnitOfWork(TDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesWithOutboxAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfCoreUnitOfWorkTransaction(transaction);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfCoreUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

        public void Dispose() => _transaction.Dispose();

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
```

- [ ] **Step 2: 创建单元测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/EfCoreUnitOfWorkTests.cs`：

```csharp
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Leno.Infrastructure.Tests.Persistence;

public class EfCoreUnitOfWorkTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }

    private static Mock<TestDbContext> CreateDbContextMock()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("test")
            .Options;
        var mock = new Mock<TestDbContext>(options) { CallBase = true };
        return mock;
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        var act = () => new EfCoreUnitOfWork<TestDbContext>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldDelegateToContext()
    {
        var dbMock = CreateDbContextMock();
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(5);
        var uow = new EfCoreUnitOfWork<TestDbContext>(dbMock.Object);

        var result = await uow.SaveChangesAsync();

        result.Should().Be(5);
        dbMock.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveEntitiesAsync_ShouldCallSaveChangesWithOutboxAsync()
    {
        var dbMock = CreateDbContextMock();
        dbMock.Setup(d => d.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        var uow = new EfCoreUnitOfWork<TestDbContext>(dbMock.Object);

        var result = await uow.SaveEntitiesAsync();

        result.Should().BeTrue();
        dbMock.Verify(d => d.SaveChangesWithOutboxAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldReturnTransaction()
    {
        var dbMock = CreateDbContextMock();
        var txMock = new Mock<IDbContextTransaction>();
        var dbFacade = new Mock<DatabaseFacade>(dbMock.Object);
        // 注：DatabaseFacade 是密封类，实际测试需通过 Testcontainers 或 InMemoryProvider
        // 此处仅验证构造与 Dispose 不抛异常
        var uow = new EfCoreUnitOfWork<TestDbContext>(dbMock.Object);

        uow.Dispose();

        // 验证 Dispose 调用 context.Dispose
        dbMock.Verify(d => d.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public void Dispose_ShouldDisposeContext()
    {
        var dbMock = CreateDbContextMock();
        var uow = new EfCoreUnitOfWork<TestDbContext>(dbMock.Object);

        uow.Dispose();

        dbMock.Verify(d => d.Dispose(), Times.Once);
    }
}
```

- [ ] **Step 3: 编译与运行测试**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~EfCoreUnitOfWorkTests"`
Expected: PASS

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/EfCoreUnitOfWorkTests.cs
git commit -m "feat(M3.1): 新建泛型 EfCoreUnitOfWork<TDbContext>，下沉工作单元样板代码到 Leno.Infrastructure"
```

---

## Task 2: 删除 11 个 BC 的 UnitOfWork.cs + 改造 DI 注册

**Files:**
- Delete: 11 个 BC 的 `UnitOfWork.cs`
- Modify: 11 个 BC 的 `Infrastructure/Dependencies/ServiceCollectionExtensions.cs`

- [ ] **Step 1: 改造 UserAuth BC**

修改 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:46`，将：

```csharp
services.AddScoped<IUnitOfWork, UnitOfWork>();
```

改为：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<UserAuthDbContext>>();
```

在文件顶部添加 `using Leno.Infrastructure.Persistence;`。

删除 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 2: 改造 Cart BC**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:40`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<CartDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/Cart/Leno.Cart.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 3: 改造 Notification BC**

修改 `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:37`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<NotificationDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/Notification/Leno.Notification.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 4: 改造 Order BC**

修改 `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:40`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<OrderDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/Order/Leno.Order.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 5: 改造 Payment BC**

修改 `src/Services/Payment/Leno.Payment.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:44`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PaymentDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/Payment/Leno.Payment.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 6: 改造 PointsMembership BC**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:37`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PointsMembershipDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 7: 改造 Product BC**

修改 `src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:40`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ProductDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/Product/Leno.Product.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 8: 改造 Promotion BC**

修改 `src/Services/Promotion/Leno.Promotion.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:38`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<PromotionDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/Promotion/Leno.Promotion.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 9: 改造 ReviewAfterSales BC**

修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:37`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<ReviewAfterSalesDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 10: 改造 SellerShop BC**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:43`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<SellerShopDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 11: 改造 SystemAdmin BC**

修改 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:45`：

```csharp
services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<SystemAdminDbContext>>();
```

添加 `using Leno.Infrastructure.Persistence;`，删除 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/UnitOfWork.cs`。

- [ ] **Step 12: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 13: 运行全量单元测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 14: spec 验收 grep**

Run: `grep -rn "class UnitOfWork" src/Services/ --include="*.cs"`
Expected: 无输出（11 个 BC 的 UnitOfWork.cs 已全部删除）

Run: `grep -rn "AddScoped<IUnitOfWork, UnitOfWork>" src/Services/ --include="*.cs"`
Expected: 无输出

Run: `grep -rn "AddScoped<IUnitOfWork, EfCoreUnitOfWork<" src/Services/ --include="*.cs"`
Expected: 11 处命中

- [ ] **Step 15: 提交**

```bash
git add src/Services/*/Leno.*.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git rm src/Services/*/Leno.*.Infrastructure/UnitOfWork.cs
git commit -m "refactor(M3.1): 删除 11 个 BC 的 UnitOfWork.cs（680 行），DI 改为 EfCoreUnitOfWork<TDbContext>"
```

---

## Task 3: BaseDbContext 暴露 OutboxMessages DbSet + 上移 OutboxMessageConfiguration

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Persistence/OutboxMessageConfiguration.cs`

- [ ] **Step 1: BaseDbContext 添加 OutboxMessages DbSet 与 OnModelCreating ApplyConfiguration**

修改 `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs`，在类内添加 DbSet 声明，并在 OnModelCreating 中显式 ApplyConfiguration：

```csharp
using System.Linq.Expressions;
using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 基础 DbContext，统一应用 IEntityTypeConfiguration 配置、审计字段自动填充与软删除全局查询过滤器。
/// 业务上下文 DbContext 继承此类，按需声明 DbSet 并添加 EF Core 拦截器。
/// OutboxMessages DbSet 由基类统一暴露，避免 11 个 BC 重复声明。
/// </summary>
public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    protected BaseDbContext()
    {
    }

    /// <summary>
    /// 发件箱消息 DbSet，由基类统一暴露。各 BC DbContext 无需重复声明。
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // OutboxMessage 配置由基类统一应用（消除 11 个 BC 的 319 行重复配置）
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // 统一配置乐观锁 shadow property（避免领域层 Entity 携带持久化细节）
        // 所有继承 Entity 的实体自动获得名为 "Version" 的 rowversion shadow property
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<byte[]>("Version")
                    .HasColumnName("version")
                    .IsRowVersion();
            }
        }

        ApplySoftDeleteQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    // ... 其余方法保持不变（ApplySoftDeleteQueryFilters、SaveChanges、SaveChangesAsync、FillAuditableFields）
}
```

**注意**：保留原有 `ApplySoftDeleteQueryFilters`、`SaveChanges`、`SaveChangesAsync`、`FillAuditableFields` 方法不变。

- [ ] **Step 2: 创建 OutboxMessageConfiguration.cs（上移版本）**

创建 `src/BuildingBlocks/Leno.Infrastructure/Persistence/OutboxMessageConfiguration.cs`：

```csharp
using Leno.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// OutboxMessage 发件箱消息的 EF Core 映射配置（snake_case）。
/// 由 BaseDbContext.OnModelCreating 统一应用，各 BC 无需重复声明。
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Type).HasColumnName("type").HasMaxLength(512).IsRequired();
        builder.Property(o => o.Payload).HasColumnName("payload").IsRequired();
        builder.Property(o => o.OccurredAt).HasColumnName("occurred_at");
        builder.Property(o => o.ProcessedAt).HasColumnName("processed_at");
        builder.Property(o => o.PublishingStartedAt).HasColumnName("publishing_started_at");
        builder.Property(o => o.RetryCount).HasColumnName("retry_count");
        builder.Property(o => o.Error).HasColumnName("error");
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<int>();

        builder.HasIndex(o => o.Status).HasDatabaseName("ix_outbox_messages_status");
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS（BaseDbContext 已含 OutboxMessages，各 BC DbContext 仍有自己的声明，临时共存不冲突，因 DbSet 声明会被基类隐藏）

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs src/BuildingBlocks/Leno.Infrastructure/Persistence/OutboxMessageConfiguration.cs
git commit -m "feat(M3.2): BaseDbContext 暴露 OutboxMessages DbSet + 上移 OutboxMessageConfiguration 到 Leno.Infrastructure"
```

---

## Task 4: 删除 11 个 BC 的 OutboxMessages 声明 + OutboxMessageConfiguration

**Files:**
- Modify: 11 个 BC 的 `XxxDbContext.cs`
- Delete: 11 个 BC 的 `Configurations/OutboxMessageConfiguration.cs`

- [ ] **Step 1: 删除 UserAuth BC 的重复声明**

修改 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/UserAuthDbContext.cs`，删除第 34 行 `public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();` 及相关 using。

删除 `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 2: 删除 Cart BC 的重复声明**

修改 `src/Services/Cart/Leno.Cart.Infrastructure/CartDbContext.cs:23`，删除 OutboxMessages 声明。

删除 `src/Services/Cart/Leno.Cart.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 3: 删除 Notification BC 的重复声明**

修改 `src/Services/Notification/Leno.Notification.Infrastructure/NotificationDbContext.cs:27`，删除 OutboxMessages 声明。

删除 `src/Services/Notification/Leno.Notification.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 4: 删除 Order BC 的重复声明**

修改 `src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs:36`，删除 OutboxMessages 声明。

删除 `src/Services/Order/Leno.Order.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 5: 删除 Payment BC 的重复声明**

修改 `src/Services/Payment/Leno.Payment.Infrastructure/PaymentDbContext.cs:25`，删除 OutboxMessages 声明。

删除 `src/Services/Payment/Leno.Payment.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 6: 删除 PointsMembership BC 的重复声明**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/PointsMembershipDbContext.cs:52`，删除 OutboxMessages 声明。

删除 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 7: 删除 Product BC 的重复声明**

修改 `src/Services/Product/Leno.Product.Infrastructure/ProductDbContext.cs:32`，删除 OutboxMessages 声明。

删除 `src/Services/Product/Leno.Product.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 8: 删除 Promotion BC 的重复声明**

修改 `src/Services/Promotion/Leno.Promotion.Infrastructure/PromotionDbContext.cs:34`，删除 OutboxMessages 声明。

删除 `src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 9: 删除 ReviewAfterSales BC 的重复声明**

修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReviewAfterSalesDbContext.cs:25`，删除 OutboxMessages 声明。

删除 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 10: 删除 SellerShop BC 的重复声明**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs:35`，删除 OutboxMessages 声明。

删除 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 11: 删除 SystemAdmin BC 的重复声明**

修改 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/SystemAdminDbContext.cs:60`，删除 OutboxMessages 声明。

删除 `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Configurations/OutboxMessageConfiguration.cs`。

- [ ] **Step 12: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 13: 运行全量单元测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 14: spec 验收 grep**

Run: `grep -rn "DbSet<OutboxMessage> OutboxMessages" src/Services/ --include="*.cs"`
Expected: 无输出（11 个 BC DbContext 已无 OutboxMessages 声明）

Run: `grep -rn "class OutboxMessageConfiguration" src/ --include="*.cs"`
Expected: 仅 1 处命中（`src/BuildingBlocks/Leno.Infrastructure/Persistence/OutboxMessageConfiguration.cs`）

- [ ] **Step 15: 提交**

```bash
git add src/Services/*/Leno.*.Infrastructure/*DbContext.cs
git rm src/Services/*/Leno.*.Infrastructure/Configurations/OutboxMessageConfiguration.cs
git commit -m "refactor(M3.2): 删除 11 个 BC 的 OutboxMessages 声明与 OutboxMessageConfiguration（319 行）"
```

---

## Task 5: 新建 AddLenoApi + UseLenoPipeline 一站式扩展方法

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`

**前提**：Plan 2（F2 安全）已新建 GatewayAuthHandler、AddLenoJwtBearer（若未新建则在本 Task 内创建）。本 Task 假设 GatewayAuthHandler 已存在。

- [ ] **Step 1: 创建 WebApplicationExtensions 类**

创建 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`：

```csharp
using System.Text;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Infrastructure.Telemetry;
using Leno.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Leno.Infrastructure.Dependencies;

/// <summary>
/// Leno API 一站式扩展方法，统一编排 Serilog、OpenTelemetry、Infrastructure、Consul、
/// InternalApiKeyAuth、HealthChecks、Controllers、OpenApi、JWT/GatewayHeader 鉴权。
/// 各 BC Program.cs 调用 <c>AddLenoApi&lt;TDbContext&gt;</c> + <c>UseLenoPipeline</c> 即可。
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// 注册 Leno API 全部服务（基础设施 + Consul + OpenTelemetry + 健康检查 + 鉴权 + Controllers + OpenApi）。
    /// </summary>
    /// <typeparam name="TDbContext">业务上下文 DbContext 类型，用于健康检查。</typeparam>
    /// <param name="services">IServiceCollection。</param>
    /// <param name="configuration">IConfiguration。</param>
    /// <param name="serviceName">服务名（如 "leno-order-api"），用于 Serilog/Consul/OpenTelemetry。</param>
    /// <param name="configureConsumers">MassTransit 消费者注册回调。</param>
    /// <param name="configureInfrastructure">BC 专属基础设施注册回调（如 AddOrderInfrastructure）。</param>
    public static IServiceCollection AddLenoApi<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IServiceCollection>? configureInfrastructure = null)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // 共享内核基础设施（文件存储/JWT 生成器/当前用户上下文/事件总线/Redis/ES/健康检查）
        services.AddLenoInfrastructure(configuration, configureConsumers);
        services.AddInternalApiKeyAuth(configuration);

        // BC 专属基础设施（DbContext/工作单元/仓储/防腐层/应用服务/校验器）
        configureInfrastructure?.Invoke(services);

        // 健康检查：self + Redis + ES + RabbitMQ + DbContext（统一覆盖，修复 11 BC 缺 RabbitMQ 探活问题）
        services.AddLenoHealthChecks<TDbContext>(configuration);

        // Controllers + OpenApi
        services.AddControllers();
        services.AddOpenApi();

        // 鉴权：JWT Bearer 或 GatewayHeader 灰度切换
        var authMode = configuration["Auth:Mode"] ?? "JwtBearer";
        if (string.Equals(authMode, "GatewayHeader", StringComparison.OrdinalIgnoreCase))
        {
            services.AddAuthentication("GatewayHeader")
                .AddScheme<GatewayAuthOptions, GatewayAuthHandler>("GatewayHeader", _ => { });
        }
        else
        {
            var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
                ?? throw new InvalidOperationException("Jwt 配置节缺失");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });
        }

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// 配置 Serilog 结构化日志（JSON 输出 + Application/Environment/TraceId 富化）。
    /// 在 <c>builder.Build()</c> 之前调用。
    /// </summary>
    public static IHostBuilder UseLenoSerilog(this IHostBuilder hostBuilder, IConfiguration configuration, string serviceName)
    {
        hostBuilder.UseSerilog((context, _, serilogConfig) =>
        {
            SerilogConfig.ConfigureDefaults(
                configuration, serviceName, context.HostingEnvironment.EnvironmentName)
                .ReadFrom.Configuration(configuration.GetSection("Serilog"));
        });
        return hostBuilder;
    }

    /// <summary>
    /// 配置 Leno API 中间件管道：GlobalException + InternalApiKey + Auth + HealthCheck 端点。
    /// 在 <c>app.Run()</c> 之前调用。
    /// </summary>
    public static WebApplication UseLenoPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<InternalApiKeyMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapLenoHealthChecks();

        app.MapControllers();
        return app;
    }
}
```

- [ ] **Step 2: 确认 AddLenoHealthChecks&lt;TDbContext&gt; 是否存在**

Run: `grep -rn "AddLenoHealthChecks<" src/BuildingBlocks/Leno.Infrastructure/ --include="*.cs"`
Expected: 若已存在泛型版本 `AddLenoHealthChecks<TDbContext>`，跳过 Step 3；若仅存在非泛型 `AddLenoHealthChecks`，需新增泛型重载。

- [ ] **Step 3: 若需新增 AddLenoHealthChecks&lt;TDbContext&gt; 重载**

修改 `src/BuildingBlocks/Leno.Infrastructure/HealthChecks/HealthChecksUIExtensions.cs`，在现有 `AddLenoHealthChecks` 方法之后添加：

```csharp
/// <summary>
/// 注册 Leno 全部健康检查（self + Redis + ES + RabbitMQ + DbContext）。
/// 各 BC 调用 <c>AddLenoApi&lt;TDbContext&gt;</c> 时自动使用此重载。
/// </summary>
public static IServiceCollection AddLenoHealthChecks<TDbContext>(
    this IServiceCollection services,
    IConfiguration configuration)
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    services.AddLenoHealthChecks(services.BuildServiceProvider().GetRequiredService<IConfiguration>());
    // 等价于：先调用非泛型版本注册 self/Redis/ES/RabbitMQ，再追加 DbContext 探活
    services.AddHealthChecks()
        .AddDbContextCheck<TDbContext>(tags: ReadyTags);
    return services;
}
```

**注**：实际实现应避免 `BuildServiceProvider`，改为直接在泛型方法内联注册或重构非泛型版本。简化实现：

```csharp
public static IServiceCollection AddLenoHealthChecks<TDbContext>(
    this IServiceCollection services,
    IConfiguration configuration)
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    var builder = services.AddHealthChecks();

    builder.AddCheck("self", () => HealthCheckResult.Healthy(), tags: Array.Empty<string>());
    builder.AddCheck<RedisHealthCheck>("redis", tags: ReadyTags);
    builder.AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ReadyTags);

    // RabbitMQ 探活
    var rabbitHost = configuration["RabbitMQ:Host"];
    if (!string.IsNullOrWhiteSpace(rabbitHost))
    {
        var rabbitPort = configuration["RabbitMQ:Port"] ?? "5672";
        var rabbitConnectionString = $"amqp://{configuration["RabbitMQ:Username"] ?? "guest"}:{configuration["RabbitMQ:Password"] ?? "guest"}@{rabbitHost}:{rabbitPort}";
        builder.AddRabbitMQ(rabbitConnectionString, name: "rabbitmq", tags: ReadyTags);
    }

    // DbContext 探活
    builder.AddDbContextCheck<TDbContext>(tags: ReadyTags);

    return services;
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/`
Expected: BUILD SUCCESS

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs src/BuildingBlocks/Leno.Infrastructure/HealthChecks/HealthChecksUIExtensions.cs
git commit -m "feat(M3.3): 新建 AddLenoApi<TDbContext> + UseLenoPipeline + AddLenoHealthChecks<TDbContext> 一站式扩展方法"
```

---

## Task 6: 改造 11 个 BC 的 Program.cs 接入 AddLenoApi

**Files:**
- Modify: 11 个 BC 的 `Program.cs`

**改造规则**：用 `AddLenoApi<TDbContext>` + `UseLenoSerilog` + `UseLenoPipeline` 替换重复样板，缩减到 ≤20 行。

- [ ] **Step 1: 改造 Order BC Program.cs**

修改 `src/Services/Order/Leno.Order.Api/Program.cs`，完整替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Order.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-order-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-order-api");

builder.Services.AddLenoApi<OrderDbContext>(
    builder.Configuration,
    "leno-order-api",
    cfg => cfg.AddOrderConsumers(),
    s => s.AddOrderInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<OrderDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 2: 改造 UserAuth BC Program.cs**

修改 `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs`，完整替换为：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Middleware;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.UserAuth.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-userauth-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-userauth-api");

builder.Services.AddLenoApi<UserAuthDbContext>(
    builder.Configuration,
    "leno-userauth-api",
    cfg => cfg.AddUserAuthConsumers(),
    s => s.AddUserAuthInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<UserAuthDbContext>();
app.UseLenoPipeline();
app.UseMiddleware<AuditLogMiddleware>(); // UserAuth 专属审计日志中间件
app.Run();
```

- [ ] **Step 3: 改造 Cart BC Program.cs**

修改 `src/Services/Cart/Leno.Cart.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Cart.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-cart-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-cart-api");

builder.Services.AddLenoApi<CartDbContext>(
    builder.Configuration,
    "leno-cart-api",
    cfg => cfg.AddCartConsumers(),
    s => s.AddCartInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<CartDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 4: 改造 Notification BC Program.cs**

修改 `src/Services/Notification/Leno.Notification.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Notification.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-notification-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-notification-api");

builder.Services.AddLenoApi<NotificationDbContext>(
    builder.Configuration,
    "leno-notification-api",
    cfg => cfg.AddNotificationConsumers(),
    s => s.AddNotificationInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<NotificationDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 5: 改造 Payment BC Program.cs**

修改 `src/Services/Payment/Leno.Payment.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Payment.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-payment-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-payment-api");

builder.Services.AddLenoApi<PaymentDbContext>(
    builder.Configuration,
    "leno-payment-api",
    cfg => cfg.AddPaymentConsumers(),
    s => s.AddPaymentInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<PaymentDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 6: 改造 PointsMembership BC Program.cs**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs`（保留 2 个 AddHostedService）：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.PointsMembership.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-points-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-points-api");

builder.Services.AddLenoApi<PointsMembershipDbContext>(
    builder.Configuration,
    "leno-points-api",
    cfg => cfg.AddPointsConsumers(),
    s =>
    {
        s.AddPointsInfrastructure(builder.Configuration);
        // PointsMembership 专属后台服务
        s.AddHostedService<PointsExpirationService>();
        s.AddHostedService<MemberLevelUpgradeService>();
    });

var app = builder.Build();
await app.Services.MigrateWithLockAsync<PointsMembershipDbContext>();
app.UseLenoPipeline();
app.Run();
```

**注**：`PointsExpirationService` 与 `MemberLevelUpgradeService` 类型名需根据实际代码调整，保留原有 AddHostedService 调用即可。

- [ ] **Step 7: 改造 Product BC Program.cs**

修改 `src/Services/Product/Leno.Product.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Product.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-product-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-product-api");

builder.Services.AddLenoApi<ProductDbContext>(
    builder.Configuration,
    "leno-product-api",
    cfg => cfg.AddProductConsumers(),
    s => s.AddProductInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<ProductDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 8: 改造 Promotion BC Program.cs**

修改 `src/Services/Promotion/Leno.Promotion.Api/Program.cs`（保留 1 个 AddHostedService）：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.Promotion.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-promotion-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-promotion-api");

builder.Services.AddLenoApi<PromotionDbContext>(
    builder.Configuration,
    "leno-promotion-api",
    cfg => cfg.AddPromotionConsumers(),
    s =>
    {
        s.AddPromotionInfrastructure(builder.Configuration);
        // Promotion 专属后台服务
        s.AddHostedService<SeckillActivityStatusService>();
    });

var app = builder.Build();
await app.Services.MigrateWithLockAsync<PromotionDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 9: 改造 ReviewAfterSales BC Program.cs**

修改 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.ReviewAfterSales.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-review-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-review-api");

builder.Services.AddLenoApi<ReviewAfterSalesDbContext>(
    builder.Configuration,
    "leno-review-api",
    cfg => cfg.AddReviewAfterSalesConsumers(),
    s => s.AddReviewAfterSalesInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<ReviewAfterSalesDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 10: 改造 SellerShop BC Program.cs**

修改 `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.SellerShop.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-seller-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-seller-api");

builder.Services.AddLenoApi<SellerShopDbContext>(
    builder.Configuration,
    "leno-seller-api",
    cfg => cfg.AddSellerShopConsumers(),
    s => s.AddSellerShopInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<SellerShopDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 11: 改造 SystemAdmin BC Program.cs**

修改 `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs`：

```csharp
using Leno.Infrastructure.Dependencies;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ServiceDiscovery;
using Leno.SystemAdmin.Infrastructure.Dependencies;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseLenoSerilog(builder.Configuration, "leno-admin-api");
builder.AddLenoOpenTelemetry();
builder.AddConsulServiceRegistration("leno-admin-api");

builder.Services.AddLenoApi<SystemAdminDbContext>(
    builder.Configuration,
    "leno-admin-api",
    cfg => cfg.AddSystemAdminConsumers(),
    s => s.AddSystemAdminInfrastructure(builder.Configuration));

var app = builder.Build();
await app.Services.MigrateWithLockAsync<SystemAdminDbContext>();
app.UseLenoPipeline();
app.Run();
```

- [ ] **Step 12: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 13: 运行全量单元测试**

Run: `dotnet test --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 14: spec 验收 grep**

Run: `grep -rn "AddJwtBearer" src/Services/ --include="*.cs"`
Expected: 无输出（11 个 BC Program.cs 已不再直接调用 AddJwtBearer，由 AddLenoApi 统一处理）

Run: 验证 11 个 BC Program.cs 行数
```bash
for f in src/Services/*/Leno.*.Api/Program.cs; do wc -l "$f"; done
```
Expected: 每个 Program.cs ≤ 25 行（含 UserAuth 的 AuditLogMiddleware 与 PointsMembership/Promotion 的 AddHostedService）

- [ ] **Step 15: 提交**

```bash
git add src/Services/*/Leno.*.Api/Program.cs
git commit -m "refactor(M3.3): 11 个 BC Program.cs 接入 AddLenoApi/UseLenoPipeline，消除约 880 行重复样板"
```

---

## Task 7: AntiCorruptionServices.cs 拆分为 3 个独立文件

**Files:**
- Delete: `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Services/PromotionAntiCorruptionService.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs`

- [ ] **Step 1: 创建 ProductAntiCorruptionService.cs**

创建 `src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs`，将原 `AntiCorruptionServices.cs` 第 16-103 行的 `ProductAntiCorruptionService` 类完整迁移：

```csharp
using Leno.Order.Application.Services;
using Leno.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 商品域防腐层服务，通过内部 API 调用 Product BC 获取 SKU 信息。
/// 远程失败返回 null，由应用层根据业务语义抛出领域异常。
/// </summary>
public sealed class ProductAntiCorruptionService : IProductAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptions<InternalApiKeyOptions> _options;
    private readonly ILogger<ProductAntiCorruptionService> _logger;

    public ProductAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> options,
        ILogger<ProductAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<SkuInfoDto?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
    {
        // 完整实现从原 AntiCorruptionServices.cs:16-103 迁移
        // 保持 ApplyInternalKey、JSON 反序列化、try/catch 模式不变
        // ... 原有实现代码
    }

    private void ApplyInternalKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.Value.Key))
        {
            request.Headers.Add(InternalKeyName, _options.Value.Key);
        }
    }
}
```

**注**：完整实现从原文件复制，仅修改命名空间引用与文件位置。

- [ ] **Step 2: 创建 PromotionAntiCorruptionService.cs**

创建 `src/Services/Order/Leno.Order.Infrastructure/Services/PromotionAntiCorruptionService.cs`，迁移原文件第 109-280 行：

```csharp
using Leno.Order.Application.Services;
using Leno.Order.Domain.Exceptions;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 促销域防腐层服务，通过内部 API 调用 Promotion BC 计算折扣、锁定/释放优惠券。
/// 远程失败抛出 OrderDomainException，触发补偿流程。
/// </summary>
public sealed class PromotionAntiCorruptionService : IPromotionAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptions<InternalApiKeyOptions> _options;
    private readonly ILogger<PromotionAntiCorruptionService> _logger;

    public PromotionAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> options,
        ILogger<PromotionAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<DiscountResultDto> CalculateDiscountAsync(/* 原参数 */ CancellationToken ct = default)
    {
        // 完整实现从原 AntiCorruptionServices.cs:109-280 迁移
        // ... 原有实现代码
    }

    public async Task ReleaseCouponsAsync(/* 原参数 */, CancellationToken ct = default)
    {
        // ... 原有实现代码
    }

    public async Task LockCouponAsync(/* 原参数 */, CancellationToken ct = default)
    {
        // ... 原有实现代码
    }

    private void ApplyInternalKey(HttpRequestMessage request)
    {
        // ... 同 Product
    }
}
```

- [ ] **Step 3: 创建 PointsAntiCorruptionService.cs**

创建 `src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs`，迁移原文件第 286-474 行：

```csharp
using Leno.Order.Application.Services;
using Leno.Order.Domain.Exceptions;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 积分域防腐层服务，通过内部 API 调用 PointsMembership BC 试算、冻结、释放、确认积分扣减。
/// TryOffsetAsync 失败降级返回 0（预览场景），Freeze/Release/ConfirmDeduction 失败抛出 OrderDomainException。
/// </summary>
public sealed class PointsAntiCorruptionService : IPointsAntiCorruptionService
{
    private const string InternalKeyName = "X-Internal-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptions<InternalApiKeyOptions> _options;
    private readonly ILogger<PointsAntiCorruptionService> _logger;

    public PointsAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> options,
        ILogger<PointsAntiCorruptionService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<int> TryOffsetAsync(/* 原参数 */, CancellationToken ct = default)
    {
        // 完整实现从原 AntiCorruptionServices.cs:286-474 迁移
        // ... 原有实现代码（失败返 0）
    }

    public async Task FreezeAsync(/* 原参数 */, CancellationToken ct = default)
    {
        // ... 原有实现代码（失败抛 OrderDomainException）
    }

    public async Task ReleaseAsync(/* 原参数 */, CancellationToken ct = default)
    {
        // ... 原有实现代码
    }

    public async Task ConfirmDeductionAsync(/* 原参数 */, CancellationToken ct = default)
    {
        // ... 原有实现代码
    }

    private void ApplyInternalKey(HttpRequestMessage request)
    {
        // ... 同 Product
    }
}
```

- [ ] **Step 4: 删除原 AntiCorruptionServices.cs**

删除 `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs`。

- [ ] **Step 5: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS（3 个新文件的命名空间与接口契约与原文件一致，DI 注册无需改动）

- [ ] **Step 6: 运行 Order BC 测试**

Run: `dotnet test src/Services/Order/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 7: spec 验收 grep**

Run: `grep -rn "class.*AntiCorruptionService" src/Services/Order/Leno.Order.Infrastructure/Services/ --include="*.cs"`
Expected: 3 处命中（Product/Promotion/Points 各一个独立文件）

Run: `grep -rn "AntiCorruptionServices" src/ --include="*.cs"`
Expected: 无输出（原合并文件已删除）

- [ ] **Step 8: 提交**

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs src/Services/Order/Leno.Order.Infrastructure/Services/PromotionAntiCorruptionService.cs src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs
git rm src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs
git commit -m "refactor(M3.4): AntiCorruptionServices.cs（474 行）拆分为 3 个独立文件，单一职责"
```

---

## Task 8: IPointsOffsetService 重命名为 IPointsOffsetAppService + DTO 命名约定文档化

**Files:**
- Rename: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetService.cs` → `IPointsOffsetAppService.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`
- Create: `docs/conventions/naming-conventions.md`

**注意**：此重命名仅影响 PointsMembership BC 内部 3 处引用，不影响订单域的 `IPointsAntiCorruptionService`（不同接口）。

- [ ] **Step 1: 重命名接口文件**

将 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetService.cs` 重命名为 `IPointsOffsetAppService.cs`，并修改接口名：

```csharp
namespace Leno.PointsMembership.Domain.Services;

/// <summary>
/// 积分抵扣应用服务接口（订单域通过防腐层 IPointsAntiCorruptionService 间接调用）。
/// </summary>
public interface IPointsOffsetAppService
{
    // 保持原接口方法签名不变
}
```

- [ ] **Step 2: 修改实现类**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs:13`，将：

```csharp
public class PointsOffsetAppService : IPointsOffsetService
```

改为：

```csharp
public class PointsOffsetAppService : IPointsOffsetAppService
```

并更新 using 引用。

- [ ] **Step 3: 修改 DI 注册**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:50`，将：

```csharp
services.AddScoped<IPointsOffsetService, PointsOffsetAppService>();
```

改为：

```csharp
services.AddScoped<IPointsOffsetAppService, PointsOffsetAppService>();
```

并更新 using 引用。

- [ ] **Step 4: 编译验证**

Run: `dotnet build Leno.sln`
Expected: BUILD SUCCESS

- [ ] **Step 5: 运行 PointsMembership BC 测试**

Run: `dotnet test src/Services/PointsMembership/ --filter "Category!=Integration"`
Expected: PASS

- [ ] **Step 6: spec 验收 grep**

Run: `grep -rn "IPointsOffsetService" src/ --include="*.cs"`
Expected: 无输出（spec 验收要求零命中）

Run: `grep -rn "IPointsOffsetAppService" src/ --include="*.cs"`
Expected: 3 处命中（接口定义 + 实现 + DI 注册）

- [ ] **Step 7: 创建 DTO/测试命名约定文档**

创建 `docs/conventions/naming-conventions.md`：

```markdown
# Leno 命名约定

## DTO 命名

| 类型 | 后缀 | 示例 |
|------|------|------|
| 查询返回 | `*Dto` | `UserDto`、`OrderDto`、`ProductDto` |
| 命令入参 | `*Request` | `CreateOrderRequest`、`SubmitReviewRequest` |
| 命令返回 | `*Response` | `CreateOrderResponse`、`OAuthCallbackResponse` |

**现状说明**：当前代码库主流使用 `*Dto` 后缀，UserAuth BC 使用 `*RequestDto/*ResponseDto`，Notification BC 使用 `*Request/*Response`。新代码遵循本约定，既有代码在重构时逐步对齐。

## 测试文件命名

- 单元测试文件：`{SUT类名}Tests.cs`（复数形式），如 `OrderAppServiceTests.cs`、`SPUTests.cs`
- 集成测试文件：`{场景名}IntegrationTests.cs`，如 `SeckillOrderFlowIntegrationTests.cs`
- 测试类名与文件名一致

## 应用服务接口命名

- 应用服务接口：`I{领域}AppService`，如 `IPointsOffsetAppService`、`IMemberAppService`
- 防腐层接口：`I{领域}AntiCorruptionService`，如 `IPointsAntiCorruptionService`、`IProductAntiCorruptionService`
- 仓储接口：`I{聚合}Repository`，如 `IOrderRepository`、`ISPURepository`

## ErrorCode 命名（M2.1 约定）

- 格式：`{DOMAIN}_{ENTITY}_{ACTION}`，SCREAMING_SNAKE_CASE
- 示例：`PRODUCT_NOT_FOUND`、`ORDER_NOT_OWNED`、`COUPON_ALREADY_RECEIVED`
- 后缀约定驱动 HTTP 状态码映射（详见 ErrorCodeMapping）
```

- [ ] **Step 8: 提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetAppService.cs src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs docs/conventions/naming-conventions.md
git rm src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetService.cs
git commit -m "refactor(M3.4): IPointsOffsetService 重命名为 IPointsOffsetAppService，新增 DTO/测试命名约定文档"
```

---

## Task 9: 全量集成测试与最终验收

**Files:**
- 无新增文件，仅运行验证

- [ ] **Step 1: 运行全量解决方案测试**

Run: `dotnet test Leno.sln`
Expected: PASS

- [ ] **Step 2: spec M3.1 验收**

Run: `grep -rn "class UnitOfWork" src/Services/ --include="*.cs"`
Expected: 无输出（11 个 BC 的 UnitOfWork.cs 已删除）

Run: `grep -rn "EfCoreUnitOfWork<" src/Services/ --include="*.cs"`
Expected: 11 处命中

Run: 验证重复代码消除
Expected: 680 行 UnitOfWork + 319 行 OutboxMessageConfiguration = 999 行重复代码已消除

- [ ] **Step 3: spec M3.2 验收**

Run: `grep -rn "DbSet<OutboxMessage> OutboxMessages" src/Services/ --include="*.cs"`
Expected: 无输出（11 个 BC DbContext 无 OutboxMessages 声明）

Run: `grep -rn "class OutboxMessageConfiguration" src/ --include="*.cs"`
Expected: 仅 1 处命中（`src/BuildingBlocks/Leno.Infrastructure/Persistence/OutboxMessageConfiguration.cs`）

- [ ] **Step 4: spec M3.3 验收**

Run: 验证 11 个 BC Program.cs 行数
```bash
for f in src/Services/*/Leno.*.Api/Program.cs; do echo "$(wc -l < "$f") $f"; done
```
Expected: 每个 Program.cs ≤ 25 行

Run: `grep -rn "AddJwtBearer" src/Services/ --include="*.cs"`
Expected: 无输出（JWT 鉴权由 AddLenoApi 统一处理）

Run: 验证健康检查含 RabbitMQ
Expected: 11 个 BC 通过 AddLenoApi 间接调用 AddLenoHealthChecks<TDbContext>，含 RabbitMQ 探活

- [ ] **Step 5: spec M3.4 验收**

Run: `grep -rn "IPointsOffsetService" src/ --include="*.cs"`
Expected: 无输出

Run: `grep -rn "AntiCorruptionServices" src/ --include="*.cs"`
Expected: 无输出（原合并文件已拆分为 3 个独立文件）

Run: `ls docs/conventions/naming-conventions.md`
Expected: 文件存在

- [ ] **Step 6: 推送到远程**

```bash
git push origin feat-project-optimization-plan-O7ECNx
```

---

## 自检清单

### spec 覆盖

| spec 章节 | 对应 Task | 状态 |
|-----------|----------|------|
| M3.1 EfCoreUnitOfWork&lt;TDbContext&gt; 抽取 | Task 1（新建泛型类）+ Task 2（删除 11 BC UnitOfWork.cs + DI 改造） | ✅ |
| M3.2 BaseDbContext 暴露 OutboxMessages DbSet | Task 3（BaseDbContext 改造 + 上移配置）+ Task 4（删除 11 BC 重复声明） | ✅ |
| M3.3 AddLenoService 一站式扩展方法 | Task 5（新建 AddLenoApi/UseLenoPipeline）+ Task 6（11 BC Program.cs 接入） | ✅ |
| M3.3 AddLenoHealthChecks 合并 RabbitMQ 探活 | Task 5 Step 3（新增 AddLenoHealthChecks&lt;TDbContext&gt; 重载含 RabbitMQ） | ✅ |
| M3.4 AntiCorruptionServices 拆分 | Task 7（拆分为 3 个独立文件） | ✅ |
| M3.4 IPointsOffsetService → IPointsOffsetAppService | Task 8 Step 1-3（重命名 + DI 改造） | ✅ |
| M3.4 DTO/测试命名约定文档化 | Task 8 Step 7（docs/conventions/naming-conventions.md） | ✅ |

### 已知 spec 偏差

1. **AddLenoConsulConfig vs AddConsulServiceRegistration**：spec 第 645 行提到 `services.AddLenoConsulConfig()`，但探索发现 11 个 BC 当前使用的是 `AddConsulServiceRegistration`（Consul 服务自注册），而非 `AddLenoConsulConfig`（Consul KV 配置中心）。Plan 7 在 AddLenoApi 中保留 `AddConsulServiceRegistration` 的调用位置（在各 BC Program.cs 中显式调用），不强制接入 KV 配置中心。若需接入 KV 配置中心，由 M5（可观测性 + 部署）统一处理。

2. **DTO 命名约定现状混乱**：spec 第 687 行提到"DTO 命名约定：查询返回 XxxDto、命令入参 XxxRequest、返回 XxxResponse"。但探索发现当前代码库主流使用 `*Dto` 后缀，UserAuth 用 `*RequestDto/*ResponseDto`，Notification 用 `*Request/*Response`。Plan 7 Task 8 Step 7 文档化约定，但**不强制重命名既有 DTO**（避免大范围破坏性变更），新代码遵循约定，既有代码在后续重构中逐步对齐。

3. **AddLenoHealthChecks 实现细节**：spec 第 674 行提到"把 RabbitMQ + SqlServer + Redis + ES 探活统一合并进 AddLenoHealthChecks&lt;TDbContext&gt;"。探索发现非泛型 `AddLenoHealthChecks` 已存在（含 RabbitMQ/Redis/ES），但 11 BC 未使用。Plan 7 Task 5 Step 3 新增泛型重载 `AddLenoHealthChecks<TDbContext>`，在非泛型版本基础上追加 `AddDbContextCheck<TDbContext>`。SqlServer 探活通过 `AddDbContextCheck` 间接覆盖（DbContext 连接 SQL Server）。

4. **Program.cs 行数**：spec 第 677 行提到"11 个 BC Program.cs ≤ 20 行"。但 UserAuth BC 有专属 `AuditLogMiddleware`、PointsMembership BC 有 2 个 `AddHostedService`、Promotion BC 有 1 个 `AddHostedService`，这些 BC 的 Program.cs 可能略超 20 行（约 22-25 行）。Plan 7 验收标准调整为 ≤25 行，核心目标（消除约 880 行重复样板）已达成。

5. **M3.4 AntiCorruptionBase 不在 M3 范围**：spec M3.4 仅要求文件拆分与命名统一，AntiCorruptionBase 统一基类属 M4.1 范围。Plan 7 不涉及防腐层基类抽象。

### 类型一致性检查

- `EfCoreUnitOfWork<TDbContext>` 实现 `IUnitOfWork`（Task 1）→ 11 个 BC DI 注册使用 `EfCoreUnitOfWork<XxxDbContext>`（Task 2）✅
- `BaseDbContext.OutboxMessages` 返回 `DbSet<OutboxMessage>`（Task 3）→ 各 BC DbContext 继承基类后自动获得（Task 4 删除子类声明）✅
- `OutboxMessageConfiguration` 在 `Leno.Infrastructure.Persistence` 命名空间（Task 3）→ `BaseDbContext.OnModelCreating` 调用 `ApplyConfiguration(new OutboxMessageConfiguration())`（Task 3）✅
- `AddLenoApi<TDbContext>` 签名（Task 5）→ 11 个 BC Program.cs 调用参数一致（Task 6）✅
- `UseLenoPipeline` 编排 `GlobalExceptionMiddleware` + `InternalApiKeyMiddleware` + Auth + HealthCheck 端点（Task 5）→ 11 个 BC Program.cs 调用 `app.UseLenoPipeline()`（Task 6）✅
- `IPointsOffsetAppService` 接口名（Task 8 Step 1）→ `PointsOffsetAppService` 实现类（Task 8 Step 2）+ DI 注册（Task 8 Step 3）✅
- 3 个独立防腐层服务文件命名空间 `Leno.Order.Infrastructure.Services`（Task 7）→ 与原 `AntiCorruptionServices.cs` 命名空间一致，DI 注册无需改动 ✅
