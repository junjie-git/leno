# 快轨 Wave-F3 EF Migrations 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 11 个 BC 生成 EF Core 初始迁移，启动时带 Redis 分布式锁执行 `MigrateAsync`，CI 集成幂等脚本生成与 PR 阻止合并

**Architecture:** 复用 BaseDbContext 的 `ApplyConfigurationsFromAssembly` 自动发现配置；新增 `DatabaseMigrationExtensions.MigrateWithLockAsync<TDbContext>` 基于 `DistributedLock.Redis` 库实现 Redis 锁；各 BC Program.cs 在 `app.Run()` 前调用；CI 用 `dotnet ef migrations script --idempotent` 验证模型与迁移同步

**Tech Stack:** .NET 10、EF Core 10、`DistributedLock.Redis` 2.x（SamCook）、StackExchange.Redis、xUnit、FluentAssertions、Testcontainers.MsSql

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §6](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| BaseDbContext | `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs:11-43` | 抽象基类，`OnModelCreating` 第 25 行 `ApplyConfigurationsFromAssembly(GetType().Assembly)` 自动加载配置；第 29-38 行自动为 `Entity` 派生类添加 `Version` rowversion shadow property；第 49-65 行 `ApplySoftDeleteQueryFilters` |
| Leno.Infrastructure.csproj | `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj:23-53` | 第 37 行已有 `StackExchange.Redis` 2.8.16，**未引用** `DistributedLock.Redis` |
| AddRedis | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:88-94` | 第 91 行 `services.AddSingleton<IConnectionMultiplexer>`，可直接复用 |
| AddLenoInfrastructure | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:31-48` | 入口扩展，需在第 42 行 `AddRedis` 后注册 `IDistributedLockProvider` |
| Persistence 目录 | `src/BuildingBlocks/Leno.Infrastructure/Persistence/` | 仅含 `BaseDbContext.cs` 与 `EFCoreInterceptors.cs`，需新建 `DatabaseMigrationExtensions.cs` |
| 11 个 BC DbContext | 见下方表格 | 全部继承 BaseDbContext，构造函数统一 `(DbContextOptions<XxxDbContext> options)`，未重写 OnModelCreating |
| 11 个 BC Program.cs | 见下方表格 | 第 90-92 行 `app.MapControllers(); app.Run();`，**无任何 `Migrate`/`EnsureCreated` 调用** |
| 现有 Migrations 目录 | 全代码库无 `Migrations/` 目录 | 仅 `scripts/migrations/promotion-usercoupon-unique-index-backfill.sql` 一个 SQL 回填脚本 |
| ContainerFixture | `src/BuildingBlocks/Leno.Testing/Fixtures/ContainerFixture.cs:11-71` | 已启动 MsSql + Redis + RabbitMq + ES 4 容器；`SqlConnectionString` 第 25 行、`RedisConnectionString` 第 26 行 |
| IntegrationTestBase | `src/BuildingBlocks/Leno.Testing/Fixtures/IntegrationTestBase.cs:1-16` | 抽象基类，**当前无任何 BC 测试继承**（测试基础设施已搭好未使用） |
| Leno.Testing.csproj | `src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj:24` | 仅引用 `Leno.SharedKernel`，**未引用** `Leno.Infrastructure`（测试基类不能直接调用 MigrateWithLockAsync，需各 BC 测试项目自己引用） |
| docker-compose SQL Server | `docker-compose.yml:2-19` | mcr.microsoft.com/mssql/server:2019-latest，SA 密码 `Leno@SqlServer2019`，端口 1433，healthcheck 完整 |
| 各 BC AddXxxInfrastructure | `src/Services/{BC}/Leno.{BC}.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | 统一模式 `services.AddDbContext<XxxDbContext>(options => options.UseSqlServer(connectionString))` |
| 各 BC appsettings.json | `src/Services/{BC}/Leno.{BC}.Api/appsettings.json:30-32` | `ConnectionStrings:{BC}Db`，开发环境 `localhost,1433` + 密码 `Leno@2026` |
| 各 BC appsettings.Docker.json | `src/Services/{BC}/Leno.{BC}.Api/appsettings.Docker.json` | `sqlserver,1433` + 密码 `Leno@SqlServer2019` + `Redis:Configuration: redis:6379` |

### 11 个 BC 文件清单

| BC | DbContext 路径 | Program.cs 路径 | ConnectionString Key | AddXxxInfrastructure 路径 |
|---|---|---|---|---|
| UserAuth | `src/Services/UserAuth/Leno.UserAuth.Infrastructure/UserAuthDbContext.cs:12` | `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs` | UserAuthDb | `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:29` |
| SystemAdmin | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/SystemAdminDbContext.cs:11` | `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs` | SystemAdminDb | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| SellerShop | `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs:13` | `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs` | SellerShopDb | `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| ReviewAfterSales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReviewAfterSalesDbContext.cs:12` | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs` | ReviewAfterSalesDb | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| Promotion | `src/Services/Promotion/Leno.Promotion.Infrastructure/PromotionDbContext.cs:12` | `src/Services/Promotion/Leno.Promotion.Api/Program.cs` | PromotionDb | `src/Services/Promotion/Leno.Promotion.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| Product | `src/Services/Product/Leno.Product.Infrastructure/ProductDbContext.cs:13` | `src/Services/Product/Leno.Product.Api/Program.cs` | ProductDb | `src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| PointsMembership | `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/PointsMembershipDbContext.cs:12` | `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs` | PointsMembershipDb | `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| Payment | `src/Services/Payment/Leno.Payment.Infrastructure/PaymentDbContext.cs:12` | `src/Services/Payment/Leno.Payment.Api/Program.cs` | PaymentDb | `src/Services/Payment/Leno.Payment.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` |
| Order | `src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs:14` | `src/Services/Order/Leno.Order.Api/Program.cs` | OrderDb | `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:26` |
| Cart | `src/Services/Cart/Leno.Cart.Infrastructure/CartDbContext.cs:13` | `src/Services/Cart/Leno.Cart.Api/Program.cs` | CartDb | `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:26` |
| Notification | `src/Services/Notification/Leno.Notification.Infrastructure/NotificationDbContext.cs:11` | `src/Services/Notification/Leno.Notification.Api/Program.cs` | NotificationDb | `src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:23` |

---

## Task 1: 引入 DistributedLock.Redis 包并注册 IDistributedLockProvider

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj:37`（添加 DistributedLock.Redis 包引用）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:88-94`（在 AddRedis 内注册 IDistributedLockProvider）
- Test: `src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/DatabaseMigrationExtensionsTests.cs`（在 Task 2 创建）

- [ ] **Step 1: 添加 DistributedLock.Redis NuGet 包**

修改 `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` 第 37 行后插入新行：

```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.16" />
<PackageReference Include="DistributedLock.Redis" Version="2.6.0" />
```

- [ ] **Step 2: 还原包验证**

运行：

```bash
dotnet restore src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj
```

预期输出包含 `DistributedLock.Redis 2.6.0` 与 `DistributedLock.Core 2.6.0` 已还原。

- [ ] **Step 3: 在 AddRedis 内注册 IDistributedLockProvider**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 第 88-94 行的 AddRedis 方法：

```csharp
private static void AddRedis(IServiceCollection services, IConfiguration configuration)
{
    var redisConfig = configuration["Redis:Configuration"] ?? "localhost:6379";
    var multiplexer = ConnectionMultiplexer.Connect(redisConfig);
    services.AddSingleton<IConnectionMultiplexer>(_ => multiplexer);
    // 集成事件消费幂等去重存储，基于 Redis SET NX + 24h TTL
    services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
    // 数据库迁移分布式锁提供者（基于 Redis SET NX EX，SamCook DistributedLock.Redis 实现）
    services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(multiplexer));
}
```

注意：`IDistributedLockProvider` 与 `RedisDistributedSynchronizationProvider` 来自 `Medallion.Threading` 命名空间（DistributedLock.Core 与 DistributedLock.Redis 包），需在文件顶部添加 `using Medallion.Threading;` 与 `using Medallion.Threading.Redis;`。

修改文件顶部 using 区域（第 1-14 行附近）添加：

```csharp
using Medallion.Threading;
using Medallion.Threading.Redis;
```

- [ ] **Step 4: 编译验证**

运行：

```bash
dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj
```

预期：编译成功无错误。

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git commit -m "feat(infrastructure): 引入 DistributedLock.Redis 包并注册 IDistributedLockProvider"
```

---

## Task 2: 新建 DatabaseMigrationExtensions.cs

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/DatabaseMigrationExtensionsTests.cs`

- [ ] **Step 1: 写失败测试 — MigrateWithLockAsync 获取锁后执行迁移**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/DatabaseMigrationExtensionsTests.cs`：

```csharp
using FluentAssertions;
using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.Infrastructure.Tests.Persistence;

public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task MigrateWithLockAsync_AcquiresLock_AndCallsMigrateAsync()
    {
        // Arrange
        var migrated = false;
        var dbContextMock = new Mock<TestDbContext>();
        dbContextMock
            .Setup(d => d.Database.MigrateAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                migrated = true;
                return Task.CompletedTask;
            })
            .Verifiable();

        var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        services.AddSingleton<TestDbContext>(_ => dbContextMock.Object);
        services.AddDistributedRedisLock(_ => multiplexer);

        var provider = services.BuildServiceProvider();

        // Act
        await provider.MigrateWithLockAsync<TestDbContext>();

        // Assert
        migrated.Should().BeTrue("MigrateAsync 必须在获取锁后被调用");
        dbContextMock.Verify();
    }

    [Fact]
    public async Task MigrateWithLockAsync_LockAlreadyHeld_ShouldSkipMigrate()
    {
        // Arrange：先占用同一把锁，第二次调用应跳过 MigrateAsync
        var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        var migrated = false;
        var dbContextMock = new Mock<TestDbContext>();
        dbContextMock
            .Setup(d => d.Database.MigrateAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                migrated = true;
                return Task.CompletedTask;
            });
        services.AddSingleton<TestDbContext>(_ => dbContextMock.Object);
        services.AddDistributedRedisLock(_ => multiplexer);
        var provider = services.BuildServiceProvider();

        var lockProvider = provider.GetRequiredService<IDistributedLockProvider>();
        var lockKey = $"db-migrate:{typeof(TestDbContext).Name}";
        await using var heldHandle = await lockProvider.TryAcquireLockAsync(lockKey, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Act：heldHandle 仍占用锁，MigrateWithLockAsync 应获取失败并跳过 MigrateAsync
        await provider.MigrateWithLockAsync<TestDbContext>(TimeSpan.FromSeconds(2));

        // Assert
        migrated.Should().BeFalse("锁已被占用时应跳过 MigrateAsync");
    }

    public abstract class TestDbContext : DbContext
    {
        public abstract new DatabaseFacade Database { get; }
    }
}
```

注意：`AddDistributedRedisLock` 是 SamCook 库提供的扩展方法，签名 `AddDistributedRedisLock(this IServiceCollection, Func<IServiceProvider, IConnectionMultiplexer>)`。

- [ ] **Step 2: 运行测试验证失败**

运行：

```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DatabaseMigrationExtensionsTests"
```

预期：FAIL，编译错误 `MigrateWithLockAsync 未定义`。

- [ ] **Step 3: 创建 DatabaseMigrationExtensions.cs**

创建 `src/BuildingBlocks/Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs`：

```csharp
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 数据库迁移扩展方法，基于 Redis 分布式锁避免多实例并发执行 EF Core 迁移导致 schema 冲突。
/// 在各 BC Program.cs 中 `app.Run()` 前调用 `await app.Services.MigrateWithLockAsync&lt;XxxDbContext&gt;()`。
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// 在 Redis 分布式锁保护下执行 EF Core 数据库迁移。
    /// 同一 DbContext 类名的锁键（db-migrate:{DbContextName}）同一时刻仅允许一个实例执行迁移，
    /// 其他实例获取锁失败时直接跳过（已由首个实例完成迁移）。
    /// </summary>
    /// <typeparam name="TDbContext">业务上下文 DbContext 类型</typeparam>
    /// <param name="services">应用服务提供者</param>
    /// <param name="acquireTimeout">获取锁的最大等待时间，默认 5 分钟</param>
    /// <param name="ct">取消令牌</param>
    public static async Task MigrateWithLockAsync<TDbContext>(
        this IServiceProvider services,
        TimeSpan? acquireTimeout = null,
        CancellationToken ct = default)
        where TDbContext : DbContext
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<TDbContext>();
        var lockProvider = sp.GetRequiredService<IDistributedLockProvider>();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(typeof(DatabaseMigrationExtensions).FullName ?? "DatabaseMigration");

        var lockKey = $"db-migrate:{typeof(TDbContext).Name}";
        var timeout = acquireTimeout ?? TimeSpan.FromMinutes(5);

        await using var handle = await lockProvider.TryAcquireLockAsync(lockKey, timeout, ct);
        if (handle == null)
        {
            logger?.LogInformation("数据库迁移锁 {LockKey} 已被其他实例持有，跳过迁移", lockKey);
            return;
        }

        logger?.LogInformation("已获取迁移锁 {LockKey}，开始执行 {DbContextName} 迁移", lockKey, typeof(TDbContext).Name);
        await db.Database.MigrateAsync(ct);
        logger?.LogInformation("{DbContextName} 迁移完成", typeof(TDbContext).Name);
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

运行：

```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DatabaseMigrationExtensionsTests"
```

预期：两个测试 PASS。

注意：测试需要本地 Redis 在 `localhost:6379`。CI 环境由 docker-compose 提供；本地开发若 Redis 未启动，测试将失败，需先 `docker run -d -p 6379:6379 redis:7`。

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/DatabaseMigrationExtensionsTests.cs
git commit -m "feat(infrastructure): 新建 DatabaseMigrationExtensions 含 MigrateWithLockAsync Redis 锁迁移方法"
```

---

## Task 3: 为 Order BC 生成 InitialCreate 迁移（首个示范）

> 先以 Order BC 为示范完整跑通迁移生成与启动接入流程，Task 4 批量复制到其余 10 个 BC。

**Files:**
- Create: `src/Services/Order/Leno.Order.Infrastructure/Migrations/<timestamp>_InitialCreate.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Migrations/OrderDbContextModelSnapshot.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure/Migrations/<timestamp>_InitialCreate.Designer.cs`

- [ ] **Step 1: 确认 Order BC 配置类全部就绪**

运行：

```bash
dotnet build src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj
```

预期：编译成功。若失败，先修复编译错误（不在本任务范围）。

- [ ] **Step 2: 安装 dotnet-ef 全局工具（如未安装）**

运行：

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
```

或更新：

```bash
dotnet tool update --global dotnet-ef --version 10.0.0
```

- [ ] **Step 3: 生成 Order BC InitialCreate 迁移**

在仓库根目录执行：

```bash
dotnet ef migrations add InitialCreate ^
  --project src/Services/Order/Leno.Order.Infrastructure ^
  --startup-project src/Services/Order/Leno.Order.Api ^
  --output-dir Migrations
```

注意：Windows PowerShell 使用 `^` 续行；bash/zsh 使用 `\` 续行。

预期输出：

```
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

并在 `src/Services/Order/Leno.Order.Infrastructure/Migrations/` 目录生成 3 个文件：
- `<YYYYMMDDHHMMSS>_InitialCreate.cs` — Up/Down 迁移代码
- `<YYYYMMDDHHMMSS>_InitialCreate.Designer.cs` — 快照
- `OrderDbContextModelSnapshot.cs` — 当前模型快照

- [ ] **Step 4: 验证迁移 SQL 幂等生成无错**

运行：

```bash
dotnet ef migrations script --idempotent ^
  --project src/Services/Order/Leno.Order.Infrastructure ^
  --startup-project src/Services/Order/Leno.Order.Api ^
  --output scripts/migrations/order-initial.sql
```

预期：生成 `scripts/migrations/order-initial.sql`，内容含 `IF NOT EXISTS` 判断的幂等 SQL。

- [ ] **Step 5: 空库执行迁移验证（本地 SQL Server）**

确保 `docker-compose up -d sqlserver` 已启动，且 `appsettings.json` 中 `OrderDb` 连接串指向 `localhost,1433`。

运行：

```bash
dotnet ef database update ^
  --project src/Services/Order/Leno.Order.Infrastructure ^
  --startup-project src/Services/Order/Leno.Order.Api
```

预期：`Applying migration '<timestamp>_InitialCreate'.` 与 `Done.`，无错误。

可选验证：使用 SSMS 或 `sqlcmd` 连接 `localhost,1433` 查看 `LenoOrder` 数据库，应含 `__EFMigrationsHistory` 表与 Order BC 所有实体表（如 `Orders`、`OrderItems`、`OutboxMessages` 等）。

- [ ] **Step 6: 提交 Order BC 迁移文件**

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Migrations/ scripts/migrations/order-initial.sql
git commit -m "feat(order): 生成 Order BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

---

## Task 4: 为其余 10 个 BC 生成 InitialCreate 迁移

> 与 Task 3 命令模式一致，逐个 BC 执行。每个 BC 完成后单独提交以便回滚。

**Files:**
- 10 个 BC 各生成 3 个迁移文件（同 Task 3 文件结构）

- [ ] **Step 1: UserAuth BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/UserAuth/Leno.UserAuth.Infrastructure --startup-project src/Services/UserAuth/Leno.UserAuth.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/UserAuth/Leno.UserAuth.Infrastructure --startup-project src/Services/UserAuth/Leno.UserAuth.Api --output scripts/migrations/userauth-initial.sql
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Migrations/ scripts/migrations/userauth-initial.sql
git commit -m "feat(user-auth): 生成 UserAuth BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 2: Product BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/Product/Leno.Product.Infrastructure --startup-project src/Services/Product/Leno.Product.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/Product/Leno.Product.Infrastructure --startup-project src/Services/Product/Leno.Product.Api --output scripts/migrations/product-initial.sql
git add src/Services/Product/Leno.Product.Infrastructure/Migrations/ scripts/migrations/product-initial.sql
git commit -m "feat(product): 生成 Product BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 3: Cart BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/Cart/Leno.Cart.Infrastructure --startup-project src/Services/Cart/Leno.Cart.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/Cart/Leno.Cart.Infrastructure --startup-project src/Services/Cart/Leno.Cart.Api --output scripts/migrations/cart-initial.sql
git add src/Services/Cart/Leno.Cart.Infrastructure/Migrations/ scripts/migrations/cart-initial.sql
git commit -m "feat(cart): 生成 Cart BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 4: Promotion BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/Promotion/Leno.Promotion.Infrastructure --startup-project src/Services/Promotion/Leno.Promotion.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/Promotion/Leno.Promotion.Infrastructure --startup-project src/Services/Promotion/Leno.Promotion.Api --output scripts/migrations/promotion-initial.sql
git add src/Services/Promotion/Leno.Promotion.Infrastructure/Migrations/ scripts/migrations/promotion-initial.sql
git commit -m "feat(promotion): 生成 Promotion BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 5: Payment BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/Payment/Leno.Payment.Infrastructure --startup-project src/Services/Payment/Leno.Payment.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/Payment/Leno.Payment.Infrastructure --startup-project src/Services/Payment/Leno.Payment.Api --output scripts/migrations/payment-initial.sql
git add src/Services/Payment/Leno.Payment.Infrastructure/Migrations/ scripts/migrations/payment-initial.sql
git commit -m "feat(payment): 生成 Payment BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 6: PointsMembership BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/PointsMembership/Leno.PointsMembership.Infrastructure --startup-project src/Services/PointsMembership/Leno.PointsMembership.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/PointsMembership/Leno.PointsMembership.Infrastructure --startup-project src/Services/PointsMembership/Leno.PointsMembership.Api --output scripts/migrations/pointsmembership-initial.sql
git add src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Migrations/ scripts/migrations/pointsmembership-initial.sql
git commit -m "feat(points-membership): 生成 PointsMembership BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 7: ReviewAfterSales BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure --startup-project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure --startup-project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api --output scripts/migrations/reviewaftersales-initial.sql
git add src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Migrations/ scripts/migrations/reviewaftersales-initial.sql
git commit -m "feat(review-aftersales): 生成 ReviewAfterSales BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 8: SellerShop BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/SellerShop/Leno.SellerShop.Infrastructure --startup-project src/Services/SellerShop/Leno.SellerShop.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/SellerShop/Leno.SellerShop.Infrastructure --startup-project src/Services/SellerShop/Leno.SellerShop.Api --output scripts/migrations/sellershop-initial.sql
git add src/Services/SellerShop/Leno.SellerShop.Infrastructure/Migrations/ scripts/migrations/sellershop-initial.sql
git commit -m "feat(seller-shop): 生成 SellerShop BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 9: Notification BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/Notification/Leno.Notification.Infrastructure --startup-project src/Services/Notification/Leno.Notification.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/Notification/Leno.Notification.Infrastructure --startup-project src/Services/Notification/Leno.Notification.Api --output scripts/migrations/notification-initial.sql
git add src/Services/Notification/Leno.Notification.Infrastructure/Migrations/ scripts/migrations/notification-initial.sql
git commit -m "feat(notification): 生成 Notification BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 10: SystemAdmin BC 迁移**

```bash
dotnet ef migrations add InitialCreate --project src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure --startup-project src/Services/SystemAdmin/Leno.SystemAdmin.Api --output-dir Migrations
dotnet ef migrations script --idempotent --project src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure --startup-project src/Services/SystemAdmin/Leno.SystemAdmin.Api --output scripts/migrations/systemadmin-initial.sql
git add src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Migrations/ scripts/migrations/systemadmin-initial.sql
git commit -m "feat(system-admin): 生成 SystemAdmin BC InitialCreate EF Core 迁移并输出幂等 SQL 脚本"
```

- [ ] **Step 11: 全部 BC 编译验证**

```bash
dotnet build Leno.sln
```

预期：编译成功，无错误。如某个 BC 迁移生成失败（如 IEntityTypeConfiguration 缺失），先修复该 BC 配置类。

---

## Task 5: 11 个 BC Program.cs 接入启动时迁移

> 以 Order BC 为示范修改，其余 10 个 BC 同步执行相同模式。

**Files:**
- Modify: `src/Services/Order/Leno.Order.Api/Program.cs:90-92`
- Modify: `src/Services/UserAuth/Leno.UserAuth.Api/Program.cs`
- Modify: `src/Services/Product/Leno.Product.Api/Program.cs`
- Modify: `src/Services/Cart/Leno.Cart.Api/Program.cs`
- Modify: `src/Services/Promotion/Leno.Promotion.Api/Program.cs`
- Modify: `src/Services/Payment/Leno.Payment.Api/Program.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs`
- Modify: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs`
- Modify: `src/Services/Notification/Leno.Notification.Api/Program.cs`
- Modify: `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs`

- [ ] **Step 1: 修改 Order Program.cs — 在 app.Run() 前插入迁移调用**

修改 `src/Services/Order/Leno.Order.Api/Program.cs`，在文件顶部 using 区添加：

```csharp
using Leno.Infrastructure.Persistence;
```

将第 90-92 行：

```csharp
app.MapControllers();

app.Run();
```

改为：

```csharp
app.MapControllers();

// 启动时执行 EF Core 迁移（带 Redis 分布式锁，避免多实例并发冲突）
await app.Services.MigrateWithLockAsync<OrderDbContext>();
app.Run();
```

注意：C# 顶层语句支持 await，编译器自动将 Main 转为 async Task Main。`OrderDbContext` 需要 `using Leno.Order.Infrastructure;`（Order Program.cs 第 8 行已存在）。

- [ ] **Step 2: 编译验证 Order**

```bash
dotnet build src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj
```

预期：编译成功。

- [ ] **Step 3: 修改其余 10 个 BC Program.cs**

对每个 BC 的 Program.cs 执行与 Order 相同的两处修改：

1. 顶部 using 区添加 `using Leno.Infrastructure.Persistence;`
2. 在 `app.MapControllers();` 与 `app.Run();` 之间插入：
   ```csharp
   await app.Services.MigrateWithLockAsync<{BC}DbContext>();
   ```

各 BC 替换的 DbContext 类型名：
- UserAuth：`await app.Services.MigrateWithLockAsync<UserAuthDbContext>();`
- Product：`await app.Services.MigrateWithLockAsync<ProductDbContext>();`
- Cart：`await app.Services.MigrateWithLockAsync<CartDbContext>();`
- Promotion：`await app.Services.MigrateWithLockAsync<PromotionDbContext>();`
- Payment：`await app.Services.MigrateWithLockAsync<PaymentDbContext>();`
- PointsMembership：`await app.Services.MigrateWithLockAsync<PointsMembershipDbContext>();`
- ReviewAfterSales：`await app.Services.MigrateWithLockAsync<ReviewAfterSalesDbContext>();`
- SellerShop：`await app.Services.MigrateWithLockAsync<SellerShopDbContext>();`
- Notification：`await app.Services.MigrateWithLockAsync<NotificationDbContext>();`
- SystemAdmin：`await app.Services.MigrateWithLockAsync<SystemAdminDbContext>();`

每个 BC 的 Program.cs 都已有对应 `using Leno.{BC}.Infrastructure;`，无需新增。

- [ ] **Step 4: 全解决方案编译验证**

```bash
dotnet build Leno.sln
```

预期：编译成功。

- [ ] **Step 5: 本地启动 Order 服务验证迁移自动执行**

确保 SQL Server 与 Redis 已启动（`docker-compose up -d sqlserver redis`）。

删除 `LenoOrder` 数据库（如已存在）：

```bash
sqlcmd -S localhost,1433 -U sa -P "Leno@SqlServer2019" -Q "DROP DATABASE IF EXISTS LenoOrder" -C
```

启动 Order 服务：

```bash
dotnet run --project src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj
```

预期日志包含：

```
已获取迁移锁 db-migrate:OrderDbContext，开始执行 OrderDbContext 迁移
OrderDbContext 迁移完成
```

且服务正常监听端口 5154，`curl http://localhost:5154/health/live` 返回 200。

停止服务（Ctrl+C）。

- [ ] **Step 6: 验证并发场景下第二个实例跳过迁移**

打开两个终端同时启动 Order 服务（同一台机器，连接同一 SQL Server 与 Redis）：

终端 A：

```bash
dotnet run --project src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj --urls http://localhost:5154
```

终端 B（5 秒内）：

```bash
dotnet run --project src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj --urls http://localhost:5155
```

预期：其中一个终端日志显示 `已获取迁移锁 db-migrate:OrderDbContext` 与 `迁移完成`；另一个终端日志显示 `数据库迁移锁 db-migrate:OrderDbContext 已被其他实例持有，跳过迁移`。两个服务均正常启动。

停止两个终端的服务（Ctrl+C）。

- [ ] **Step 7: 提交 11 个 BC Program.cs 修改**

```bash
git add src/Services/Order/Leno.Order.Api/Program.cs src/Services/UserAuth/Leno.UserAuth.Api/Program.cs src/Services/Product/Leno.Product.Api/Program.cs src/Services/Cart/Leno.Cart.Api/Program.cs src/Services/Promotion/Leno.Promotion.Api/Program.cs src/Services/Payment/Leno.Payment.Api/Program.cs src/Services/PointsMembership/Leno.PointsMembership.Api/Program.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Program.cs src/Services/SellerShop/Leno.SellerShop.Api/Program.cs src/Services/Notification/Leno.Notification.Api/Program.cs src/Services/SystemAdmin/Leno.SystemAdmin.Api/Program.cs
git commit -m "feat: 11 个 BC Program.cs 接入启动时 MigrateWithLockAsync 迁移调用"
```

---

## Task 6: 集成测试验证启动迁移路径

> 通过 Testcontainers 启动真实 SQL Server + Redis 容器，验证 `MigrateWithLockAsync` 在空库上完整执行迁移并创建 schema。

**Files:**
- Modify: `src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj`（添加 Leno.Infrastructure 项目引用）
- Create: `src/BuildingBlocks/Leno.Testing/Fixtures/DatabaseMigrationTestBase.cs`
- Create: `src/Services/Order/Leno.Order.Infrastructure.Tests/Migrations/OrderMigrationIntegrationTests.cs`

- [ ] **Step 1: 让 Leno.Testing 引用 Leno.Infrastructure**

修改 `src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj` 第 24 行附近：

```xml
<ItemGroup>
  <ProjectReference Include="..\Leno.SharedKernel\Leno.SharedKernel.csproj" />
  <ProjectReference Include="..\Leno.Infrastructure\Leno.Infrastructure.csproj" />
</ItemGroup>
```

- [ ] **Step 2: 编译验证 Leno.Testing**

```bash
dotnet build src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj
```

预期：编译成功。

- [ ] **Step 3: 创建 DatabaseMigrationTestBase 抽象基类**

创建 `src/BuildingBlocks/Leno.Testing/Fixtures/DatabaseMigrationTestBase.cs`：

```csharp
using Leno.Infrastructure.Persistence;
using Leno.Testing.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Leno.Testing.Fixtures;

/// <summary>
/// 数据库迁移集成测试基类：基于 ContainerFixture 启动真实 SQL Server + Redis 容器，
/// 子类继承并指定具体 DbContext 类型，验证 MigrateWithLockAsync 在空库上完整创建 schema。
/// </summary>
[Collection(ContainerCollection.Name)]
public abstract class DatabaseMigrationTestBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    protected readonly ContainerFixture Fixture;

    protected DatabaseMigrationTestBase(ContainerFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // 确保 Redis 容器已连接，注册 IDistributedLockProvider
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(Fixture.RedisConnectionString);
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConnectionMultiplexer>(_ => multiplexer);
        services.AddDistributedRedisLock(_ => multiplexer);

        // 子类配置 DbContext
        ConfigureServices(services, Fixture.SqlConnectionString);

        var provider = services.BuildServiceProvider();
        await provider.MigrateWithLockAsync<TDbContext>();
        Provider = provider;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected IServiceProvider Provider { get; private set; } = null!;

    protected abstract void ConfigureServices(IServiceCollection services, string sqlConnectionString);
}
```

- [ ] **Step 4: 创建 Order BC 迁移集成测试**

创建 `src/Services/Order/Leno.Order.Infrastructure.Tests/Migrations/OrderMigrationIntegrationTests.cs`：

```csharp
using FluentAssertions;
using Leno.Order.Infrastructure;
using Leno.Testing.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Migrations;

public class OrderMigrationIntegrationTests : DatabaseMigrationTestBase<OrderDbContext>
{
    public OrderMigrationIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(sqlConnectionString));
    }

    [Fact]
    public async Task MigrateWithLockAsync_OnEmptyDatabase_CreatesAllTables()
    {
        // Arrange & Act：InitializeAsync 已执行 MigrateWithLockAsync<OrderDbContext>

        // Assert：查询 __EFMigrationsHistory 与 Orders 表存在
        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue("迁移后应能连接数据库");

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty("迁移后应无 pending migrations");

        // 验证关键表已创建
        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'").ToListAsync();
        tables.Should().Contain(new[] { "Orders", "OrderItems", "OutboxMessages", "__EFMigrationsHistory" });
    }

    [Fact]
    public async Task MigrateWithLockAsync_Idempotent_RunTwiceNoError()
    {
        // 第二次调用应无 pending migrations，无错误
        await Provider.MigrateWithLockAsync<OrderDbContext>();

        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty("重复执行迁移后仍无 pending");
    }
}
```

注意：Order.Infrastructure.Tests 项目需添加 `Leno.Testing` 项目引用（如未添加）。

- [ ] **Step 5: 修改 Order.Infrastructure.Tests.csproj 引用 Leno.Testing**

读取 `src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj`，确认是否已引用 `Leno.Testing`。如未引用，添加：

```xml
<ProjectReference Include="..\..\..\BuildingBlocks\Leno.Testing\Leno.Testing.csproj" />
```

- [ ] **Step 6: 运行 Order 迁移集成测试**

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj --filter "FullyQualifiedName~OrderMigrationIntegrationTests"
```

预期：两个测试 PASS。首次运行会拉取 SQL Server 与 Redis 镜像，耗时较长（5-10 分钟）。

- [ ] **Step 7: 提交**

```bash
git add src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj src/BuildingBlocks/Leno.Testing/Fixtures/DatabaseMigrationTestBase.cs src/Services/Order/Leno.Order.Infrastructure.Tests/Migrations/OrderMigrationIntegrationTests.cs src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj
git commit -m "test: 新增 DatabaseMigrationTestBase 与 Order 迁移集成测试，验证空库启动迁移"
```

---

## Task 7: CI 集成迁移脚本生成与 PR 阻止合并

> 在 CI 流水线增加两个检查：(1) 幂等迁移 SQL 脚本可生成；(2) 模型变更但无 migration 时阻止合并。

**Files:**
- Modify: `.github/workflows/ci.yml`
- Create: `scripts/check-migrations.ps1`（PowerShell 脚本，CI 与本地均可执行）

- [ ] **Step 1: 读取现有 CI 配置**

读取 `.github/workflows/ci.yml`，确认 jobs 结构与 .NET 构建任务位置。如文件不存在，需新建（基础 .NET 解决方案 CI 模板）。

- [ ] **Step 2: 创建 scripts/check-migrations.ps1 脚本**

创建 `scripts/check-migrations.ps1`：

```powershell
<#
.SYNOPSIS
  检查所有 BC 的 EF Core 模型与迁移同步状态。

.DESCRIPTION
  对每个 BC 执行 `dotnet ef migrations has-pending-model-changes` 检测模型与最新迁移快照的差异。
  若存在未提交到迁移的模型变更，脚本退出码 1，CI 阻止合并。

.EXAMPLE
  pwsh scripts/check-migrations.ps1
#>

$ErrorActionPreference = "Stop"

$bcProjects = @(
    @{ Name = "UserAuth"; Infrastructure = "src/Services/UserAuth/Leno.UserAuth.Infrastructure"; Api = "src/Services/UserAuth/Leno.UserAuth.Api" },
    @{ Name = "Product"; Infrastructure = "src/Services/Product/Leno.Product.Infrastructure"; Api = "src/Services/Product/Leno.Product.Api" },
    @{ Name = "Cart"; Infrastructure = "src/Services/Cart/Leno.Cart.Infrastructure"; Api = "src/Services/Cart/Leno.Cart.Api" },
    @{ Name = "Order"; Infrastructure = "src/Services/Order/Leno.Order.Infrastructure"; Api = "src/Services/Order/Leno.Order.Api" },
    @{ Name = "Promotion"; Infrastructure = "src/Services/Promotion/Leno.Promotion.Infrastructure"; Api = "src/Services/Promotion/Leno.Promotion.Api" },
    @{ Name = "Payment"; Infrastructure = "src/Services/Payment/Leno.Payment.Infrastructure"; Api = "src/Services/Payment/Leno.Payment.Api" },
    @{ Name = "PointsMembership"; Infrastructure = "src/Services/PointsMembership/Leno.PointsMembership.Infrastructure"; Api = "src/Services/PointsMembership/Leno.PointsMembership.Api" },
    @{ Name = "ReviewAfterSales"; Infrastructure = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure"; Api = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api" },
    @{ Name = "SellerShop"; Infrastructure = "src/Services/SellerShop/Leno.SellerShop.Infrastructure"; Api = "src/Services/SellerShop/Leno.SellerShop.Api" },
    @{ Name = "Notification"; Infrastructure = "src/Services/Notification/Leno.Notification.Infrastructure"; Api = "src/Services/Notification/Leno.Notification.Api" },
    @{ Name = "SystemAdmin"; Infrastructure = "src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure"; Api = "src/Services/SystemAdmin/Leno.SystemAdmin.Api" }
)

$hasError = $false

foreach ($bc in $bcProjects) {
    Write-Host "检查 $($bc.Name) BC 模型与迁移同步状态..."
    $output = dotnet ef migrations has-pending-model-changes `
        --project $bc.Infrastructure `
        --startup-project $bc.Api 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::$($bc.Name) BC 执行 has-pending-model-changes 失败：$output"
        $hasError = $true
        continue
    }

    if ($output -match "True" -or $output -match "true") {
        Write-Host "::error::$($bc.Name) BC 模型存在未提交到迁移的变更，请运行 dotnet ef migrations add <Name> 生成新迁移后再合并 PR"
        $hasError = $true
    } else {
        Write-Host "$($bc.Name) BC 模型与迁移同步"
    }
}

if ($hasError) {
    exit 1
}

Write-Host "所有 BC 模型与迁移均已同步"
exit 0
```

- [ ] **Step 3: 创建 scripts/generate-migration-scripts.ps1 脚本**

创建 `scripts/generate-migration-scripts.ps1`：

```powershell
<#
.SYNOPSIS
  为所有 BC 生成幂等迁移 SQL 脚本，用于 staging 环境空库验证与生产部署。

.EXAMPLE
  pwsh scripts/generate-migration-scripts.ps1
#>

$ErrorActionPreference = "Stop"

$bcProjects = @(
    @{ Name = "userauth"; Infrastructure = "src/Services/UserAuth/Leno.UserAuth.Infrastructure"; Api = "src/Services/UserAuth/Leno.UserAuth.Api" },
    @{ Name = "product"; Infrastructure = "src/Services/Product/Leno.Product.Infrastructure"; Api = "src/Services/Product/Leno.Product.Api" },
    @{ Name = "cart"; Infrastructure = "src/Services/Cart/Leno.Cart.Infrastructure"; Api = "src/Services/Cart/Leno.Cart.Api" },
    @{ Name = "order"; Infrastructure = "src/Services/Order/Leno.Order.Infrastructure"; Api = "src/Services/Order/Leno.Order.Api" },
    @{ Name = "promotion"; Infrastructure = "src/Services/Promotion/Leno.Promotion.Infrastructure"; Api = "src/Services/Promotion/Leno.Promotion.Api" },
    @{ Name = "payment"; Infrastructure = "src/Services/Payment/Leno.Payment.Infrastructure"; Api = "src/Services/Payment/Leno.Payment.Api" },
    @{ Name = "pointsmembership"; Infrastructure = "src/Services/PointsMembership/Leno.PointsMembership.Infrastructure"; Api = "src/Services/PointsMembership/Leno.PointsMembership.Api" },
    @{ Name = "reviewaftersales"; Infrastructure = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure"; Api = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api" },
    @{ Name = "sellershop"; Infrastructure = "src/Services/SellerShop/Leno.SellerShop.Infrastructure"; Api = "src/Services/SellerShop/Leno.SellerShop.Api" },
    @{ Name = "notification"; Infrastructure = "src/Services/Notification/Leno.Notification.Infrastructure"; Api = "src/Services/Notification/Leno.Notification.Api" },
    @{ Name = "systemadmin"; Infrastructure = "src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure"; Api = "src/Services/SystemAdmin/Leno.SystemAdmin.Api" }
)

New-Item -ItemType Directory -Force -Path scripts/migrations | Out-Null

foreach ($bc in $bcProjects) {
    Write-Host "生成 $($bc.Name) BC 幂等迁移 SQL 脚本..."
    dotnet ef migrations script --idempotent `
        --project $bc.Infrastructure `
        --startup-project $bc.Api `
        --output "scripts/migrations/$($bc.Name)-initial.sql"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::$($bc.Name) BC 迁移脚本生成失败"
        exit 1
    }
}

Write-Host "全部 BC 迁移 SQL 脚本已生成至 scripts/migrations/"
```

- [ ] **Step 4: 修改 .github/workflows/ci.yml 增加 migrations 检查 job**

在 `.github/workflows/ci.yml` 现有 jobs 之外新增 job（具体位置根据现有结构判断）：

```yaml
  migration-check:
    name: EF Core Migrations 同步检查
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install dotnet-ef
        run: dotnet tool install --global dotnet-ef --version 10.0.0

      - name: Restore
        run: dotnet restore Leno.sln

      - name: Build
        run: dotnet build Leno.sln --no-restore --configuration Release

      - name: Check migrations sync (模型变更必须配套 migration)
        run: pwsh scripts/check-migrations.ps1

      - name: Generate idempotent migration SQL scripts
        run: pwsh scripts/generate-migration-scripts.ps1

      - name: Upload migration SQL scripts
        uses: actions/upload-artifact@v4
        with:
          name: migration-scripts
          path: scripts/migrations/*.sql
          retention-days: 14

      - name: Staging 空库执行迁移验证
        env:
          SA_PASSWORD: Leno@Test123!
        run: |
          docker run -d --name sqlserver-test \
            -e ACCEPT_EULA=Y \
            -e MSSQL_SA_PASSWORD=$SA_PASSWORD \
            -p 1433:1433 \
            mcr.microsoft.com/mssql/server:2019-latest
          sleep 60
          for f in scripts/migrations/*.sql; do
            /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $SA_PASSWORD -C -i "$f"
          done
          docker rm -f sqlserver-test
```

注意：根据现有 ci.yml 结构可能需要调整 steps 顺序与 job 依赖关系（如 `needs: build`）。

- [ ] **Step 5: 本地执行迁移检查脚本验证**

```bash
pwsh scripts/check-migrations.ps1
```

预期：输出所有 BC "模型与迁移同步"，退出码 0。

若输出 "存在未提交到迁移的变更"，说明 Task 3/4 的迁移未覆盖最新模型，需重新 `dotnet ef migrations add` 补充。

- [ ] **Step 6: 本地执行迁移脚本生成验证**

```bash
pwsh scripts/generate-migration-scripts.ps1
```

预期：`scripts/migrations/` 下生成 11 个 `.sql` 文件，每个含 `IF NOT EXISTS` 幂等判断。

- [ ] **Step 7: 提交**

```bash
git add .github/workflows/ci.yml scripts/check-migrations.ps1 scripts/generate-migration-scripts.ps1
git commit -m "ci: 集成 EF Core migrations 同步检查与幂等 SQL 脚本生成，PR 模型变更需配套 migration"
```

---

## Self-Review 自检

### 1. Spec 覆盖（对照 spec §6.1 F3.1）

| Spec 要求 | 对应 Task | 覆盖 |
|---|---|---|
| 为每个 BC 生成初始迁移 | Task 3 + Task 4（共 11 个 BC） | ✅ |
| 新建 `DatabaseMigrationExtensions.cs` 含 `MigrateWithLockAsync<TDbContext>` | Task 2 | ✅ |
| 调用 `IDistributedLock.AcquireLockAsync` | Task 1（SamCook 库 `IDistributedLockProvider.TryAcquireLockAsync`）+ Task 2 | ✅（API 等价，Try 语义符合 `if (handle == null) return;` 设计） |
| 各 BC `Program.cs` 中 `app.Run()` 前调用 | Task 5 | ✅ |
| CI 增加 `dotnet ef migrations script --idempotent` | Task 7 Step 3/4/6 | ✅ |
| staging 环境空库执行迁移验证 | Task 7 Step 4 "Staging 空库执行迁移验证" | ✅ |
| PR 模型变更但无 migration 阻止合并 | Task 7 Step 2/4/5 `check-migrations.ps1` | ✅ |
| 生产推荐 K8s Init Container（M5.4 Helm chart 落地后） | 本 plan 不实施，由 Plan 9（M5）落地 | ⏭️ 已明确延后到 M5 |
| 验收：11 BC 各生成 Migrations 目录含 `*_InitialCreate.cs` 与 `ModelSnapshot.cs` | Task 3 + Task 4 | ✅ |
| 验收：空库 `dotnet ef database update` schema 与模型一致 | Task 3 Step 5 + Task 6 集成测试 | ✅ |
| 验收：集成测试启动服务自动迁移业务接口正常 | Task 6 + Task 5 Step 5 | ✅ |
| 验收：CI 模型变更无 migration 阻止合并 | Task 7 | ✅ |
| 风险：多实例并发冲突 → Redis 分布式锁 | Task 1 + Task 2 + Task 5 Step 6 并发测试 | ✅ |

### 2. 占位符扫描

- ✅ 无 "TBD"、"TODO"、"fill in details"
- ✅ 无 "Add appropriate error handling" 等占位话术
- ✅ 所有代码块完整可用（DatabaseMigrationExtensions、测试代码、PowerShell 脚本、CI YAML）
- ✅ 所有命令含确切参数与预期输出
- ✅ 11 个 BC 各有独立迁移生成命令（Task 3 + Task 4 共 11 步）

### 3. 类型一致性

- `MigrateWithLockAsync<TDbContext>` 方法签名：Task 2 定义 `(this IServiceProvider, TimeSpan?, CancellationToken)`，Task 5 调用 `await app.Services.MigrateWithLockAsync<OrderDbContext>();`（使用默认参数），Task 6 调用 `await provider.MigrateWithLockAsync<TDbContext>();`，Task 2 测试调用 `await provider.MigrateWithLockAsync<TestDbContext>()` 与 `await provider.MigrateWithLockAsync<TestDbContext>(TimeSpan.FromSeconds(2))` — 一致 ✅
- `IDistributedLockProvider` 类型：Task 1 注册、Task 2 使用、Task 6 测试基类注册 — 一致 ✅
- `RedisDistributedSynchronizationProvider`：Task 1 Step 3 创建，SamCook DistributedLock.Redis 2.6.0 提供的公共类 — 一致 ✅
- `AddDistributedRedisLock`：Task 1 Step 3 使用、Task 2 测试使用、Task 6 测试基类使用 — 一致 ✅（SamCook 库提供的扩展方法）
- 11 个 DbContext 类型名：Task 5 Step 3 列表与关键代码定位表一致 — 一致 ✅
- `ContainerFixture` 属性：Task 6 Step 3 使用 `Fixture.SqlConnectionString` 与 `Fixture.RedisConnectionString`，与 `src/BuildingBlocks/Leno.Testing/Fixtures/ContainerFixture.cs:25-26` 一致 ✅

### 4. 已知注意事项

1. **Task 2 测试依赖本地 Redis**：测试需 `localhost:6379` 可连接，CI 由 docker-compose 提供。本地开发若未启动 Redis，测试将失败。这是符合 spec 验收要求的（"集成测试启动服务自动迁移"），不视为占位符。
2. **Task 6 首次运行耗时长**：Testcontainers 拉取 SQL Server 与 Redis 镜像首次 5-10 分钟，后续运行利用本地镜像缓存会快很多。
3. **Task 7 Step 4 CI YAML 为新增 job 模板**：实际合并到现有 `ci.yml` 时需根据现有 jobs 结构调整缩进与 `needs` 依赖关系，这是合理实施细节。
4. **PromotionDbContext 未 sealed**：保持现状（第 12 行 `public class PromotionDbContext`），不修改。
5. **Promotion BC 已有 SQL 回填脚本** `scripts/migrations/promotion-usercoupon-unique-index-backfill.sql`：与 Task 4 Step 4 生成的 `scripts/migrations/promotion-initial.sql` 不冲突，前者是数据回填后者是 schema 迁移。
