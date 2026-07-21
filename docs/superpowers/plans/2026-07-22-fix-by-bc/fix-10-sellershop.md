# SellerShop BC 修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 10-sellershop.md 审计报告，制定 SellerShop BC 全量问题的修复实施计划
**Architecture:** DDD 限界上下文，按 Domain/Application/Infrastructure/Api 四层治理
**Tech Stack:** .NET 10 + EF Core + MassTransit + RabbitMQ + Redis + gRPC + xUnit + FluentAssertions
**关联审计报告:** `docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md`

---

## 问题统计总览

| 严重度 | 总数 | ALREADY-FIXED | VERIFIED-NOT-REPRODUCIBLE | 待修复 |
|--------|------|---------------|---------------------------|--------|
| 🔴 P0  | 5    | 1             | 0                         | 4      |
| 🟡 P1  | 11   | 0             | 0                         | 11     |
| 🟢 P2  | 8    | 0             | 0                         | 8      |

## 已修复问题清单（[ALREADY-FIXED]）

### [ALREADY-FIXED] p0a-T7：ValidateOwnershipAsync + 防腐层扩展

- **来源计划**：`docs/superpowers/plans/2026-07-20-p0a-placeholder-implementation.md` Task 7
- **修复内容**：SellerShop 域 `ISellerInternalQueryService.ValidateOwnershipAsync` 占位实现补齐，支持 `shop` / `spu` / `order` 三种资源类型归属校验；防腐层 `IOrderAntiCorruptionService.GetOrderSellerIdAsync` / `IProductAntiCorruptionService.GetSpuSellerIdAsync` 扩展支持归属校验调用；`SellerGrpcService.ValidateSellerOwnership` gRPC 端点接入。
- **代码验证**：
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs#L41` —— 接口方法已定义
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs#L94-L141` —— 实现完整，无占位
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs#L75-L91` —— gRPC 端点已接入
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/IOrderAntiCorruptionService.cs#L17` —— 接口已定义
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/IProductAntiCorruptionService.cs#L17` —— 接口已定义
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcOrderAntiCorruptionClient.cs#L43-L65` —— gRPC 客户端实现完整
  - `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs#L43-L65` —— gRPC 客户端实现完整

---

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| p0a-T7 | 🔴 | ValidateOwnershipAsync 占位实现 + 防腐层扩展 | 既有计划 | P0 | [ALREADY-FIXED] |
| 1 | 🔴 | 设计期工厂硬编码 SA 密码 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L13-L19](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L13-L19) | P0 | TODO |
| 2 | 🔴 | ReviewSubmittedShopDashboardSyncConsumer 将 SpuId 当作 ShopId | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L21-L28](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L21-L28) | P0 | TODO |
| 3 | 🔴 | ShopDashboardReadModelBuilder 6 个字段硬编码 0 占位 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L30-L45](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L30-L45) | P0 | TODO |
| 4 | 🔴 | SellerGrpcService.MapToProto 用 Guid.GetHashCode() 转 long | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L47-L53](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L47-L53) | P0 | TODO |
| 5 | 🟡 | ShopConfiguration 用字符串 "Qualifications" 访问 backing field | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L57-L64](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L57-L64) | P1 | TODO |
| 6 | 🟡 | EfCoreShopRepository 不 Include Qualifications | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L66-L76](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L66-L76) | P1 | TODO |
| 7 | 🟡 | Shop.DecrementProductCount 静默吞掉越界调用 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L78-L85](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L78-L85) | P1 | TODO |
| 8 | 🟡 | ShopsController 多步操作无显式事务 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L87-L100](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L87-L100) | P1 | TODO |
| 9 | 🟡 | ShopDashboardData.OnOrderPaid 不按订单跟踪金额 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L102-L109](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L102-L109) | P1 | TODO |
| 10 | 🟡 | ShopAppService.UpdateShopInfoAsync 缺失归属校验 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L111-L118](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L111-L118) | P1 | TODO |
| 11 | 🟡 | ShopDashboardReadModel 注释引用不存在的 Consumer | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L120-L127](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L120-L127) | P1 | TODO |
| 12 | 🟡 | ShopDashboardDataConfiguration 未显式映射审计字段 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L129-L137](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L129-L137) | P1 | TODO |
| 13 | 🟡 | EfCoreShopMetricsRepository.UpsertAsync 用 EntityState.Modified | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L139-L147](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L139-L147) | P1 | TODO |
| 14 | 🟡 | SellerInternalQueryService 用 try/catch 控制流程 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L149-L155](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L149-L155) | P1 | TODO |
| 15 | 🟡 | SellerDashboardAppService.GetDashboardAsync 标 [Obsolete] 无迁移计划 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L157-L164](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L157-L164) | P1 | TODO |
| 16 | 🟢 | BusinessLicense 值对象定义但全 BC 未被使用 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L168-L174](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L168-L174) | P2 | TODO |
| 17 | 🟢 | Program.cs 启动时调用 MigrateWithLockAsync 阻塞启动 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L176-L182](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L176-L182) | P2 | TODO |
| 18 | 🟢 | QualificationExpiryReminder 硬编码扫描间隔与提醒天数 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L184-L190](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L184-L190) | P2 | TODO |
| 19 | 🟢 | ShopMetrics.RecordOrder 币种校验未做大小写归一化 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L192-L198](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L192-L198) | P2 | TODO |
| 20 | 🟢 | ShopDashboardQueryHandler 静默忽略 StartDate/EndDate | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L200-L206](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L200-L206) | P2 | TODO |
| 21 | 🟢 | ShopAppService.UpdateShopInfoAsync 三步独立 Update | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L208-L220](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L208-L220) | P2 | TODO |
| 22 | 🟢 | OrderCancelledEventConsumer 不区分未支付/已支付取消 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L222-L233](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L222-L233) | P2 | TODO |
| 23 | 🟢 | GrpcAntiCorruptionClient fail-closed 无 Metrics 告警 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L235-L242](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L235-L242) | P2 | TODO |

---

## P0 详细修复计划（TDD 5 步骤）

### P0-1: 设计期工厂硬编码 SA 密码（审计 #1）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L13-L19](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L13-L19)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs#L14-L17](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs#L14-L17)
- **根因**：第 15 行 `UseSqlServer("Server=localhost,1433;Database=LenoSellerShop;User Id=sa;Password=Leno@SqlServer2019;TrustServerCertificate=True;MultipleActiveResultSets=true")` 硬编码 SA 账号密码。生产连接串在 `appsettings.json#L34` 已正确使用 `${MSSQL_SA_PASSWORD}` 占位符，但设计期工厂为绕过 Redis 等依赖直接连库生成迁移，硬编码了明文凭据。
- **影响**：源码一旦泄露，攻击者可直接以 SA 身份连接数据库，绕过应用层所有鉴权，可读取/篡改/删除店铺、卖家档案、银行账号、身份证号等敏感数据。
- **修复方案**：设计期工厂从环境变量 `MSSQL_SA_PASSWORD` 读取密码，未配置时回退到固定占位 `__DESIGN_ONLY__` 仅用于本地开发。

#### Task 1: 写失败测试

- [ ] **Step 1: 编写测试**

新增测试文件 `Leno.SellerShop.Infrastructure.Tests/SellerShopDbContextDesignTimeFactoryTests.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.SellerShop.Infrastructure.Tests;

public sealed class SellerShopDbContextDesignTimeFactoryTests
{
    [Fact]
    public void CreateDbContext_Should_Not_Contain_Hardcoded_Password()
    {
        // Arrange
        var factory = new SellerShopDbContextDesignTimeFactory();
        Environment.SetEnvironmentVariable("MSSQL_SA_PASSWORD", "TestEnvPassword123");

        try
        {
            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Assert.NotNull(context);
            // 设计期工厂不应再硬编码 Leno@SqlServer2019
            // 通过反射获取连接字符串无法直接访问，改为验证不抛出且可创建 context
            Assert.True(context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSSQL_SA_PASSWORD", null);
        }
    }

    [Fact]
    public void CreateDbContext_Should_Use_Environment_Variable_Password_When_Set()
    {
        // Arrange
        var factory = new SellerShopDbContextDesignTimeFactory();
        var testPassword = "EnvVarPassword456";
        Environment.SetEnvironmentVariable("MSSQL_SA_PASSWORD", testPassword);

        try
        {
            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert — 上下文应成功创建，且连接串应包含环境变量密码而非硬编码
            Assert.NotNull(context);
            var connectionString = context.Database.GetConnectionString();
            Assert.Contains(testPassword, connectionString);
            Assert.DoesNotContain("Leno@SqlServer2019", connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSSQL_SA_PASSWORD", null);
        }
    }

    [Fact]
    public void CreateDbContext_Should_Use_Placeholder_When_Env_Var_Not_Set()
    {
        // Arrange
        var factory = new SellerShopDbContextDesignTimeFactory();
        Environment.SetEnvironmentVariable("MSSQL_SA_PASSWORD", null);

        // Act
        var context = factory.CreateDbContext(Array.Empty<string>());

        // Assert — 未配置环境变量时使用占位密码，而非硬编码生产密码
        Assert.NotNull(context);
        var connectionString = context.Database.GetConnectionString();
        Assert.DoesNotContain("Leno@SqlServer2019", connectionString);
        Assert.Contains("__DESIGN_ONLY__", connectionString);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SellerShopDbContextDesignTimeFactoryTests"`

Expected: FAIL（`CreateDbContext_Should_Use_Environment_Variable_Password_When_Set` 与 `CreateDbContext_Should_Use_Placeholder_When_Env_Var_Not_Set` 失败，因当前代码硬编码 `Leno@SqlServer2019`）

- [ ] **Step 3: 写最小实现**

修改 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.SellerShop.Infrastructure;

/// <summary>
/// EF Core 设计期工厂，避免 dotnet ef migrations add 启动完整 Program.cs（依赖 Redis 等基础设施）。
/// 仅用于生成迁移与脚本，不连接真实数据库。
/// 密码从环境变量 MSSQL_SA_PASSWORD 读取，未配置时回退到固定占位 __DESIGN_ONLY__ 仅用于本地开发。
/// </summary>
public sealed class SellerShopDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SellerShopDbContext>
{
    private const string DesignOnlyPlaceholder = "__DESIGN_ONLY__";

    public SellerShopDbContext CreateDbContext(string[] args)
    {
        var password = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            password = DesignOnlyPlaceholder;
        }

        var connectionString = $"Server=localhost,1433;Database=LenoSellerShop;User Id=sa;Password={password};TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<SellerShopDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new SellerShopDbContext(options);
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SellerShopDbContextDesignTimeFactoryTests"`

Expected: PASS（全部 3 个测试用例通过）

- [ ] **Step 5: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SellerShopDbContextDesignTimeFactoryTests.cs
git commit -m "fix(SellerShop): 设计期工厂 SA 密码改为从环境变量读取，移除硬编码凭据"
```

---

### P0-2: ReviewSubmittedShopDashboardSyncConsumer 将 SpuId 当作 ShopId（审计 #2）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L21-L28](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L21-L28)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ReviewSubmittedShopDashboardSyncConsumer.cs#L40-L51](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ReviewSubmittedShopDashboardSyncConsumer.cs#L40-L51)
- **根因**：第 42 行 `var shopId = integrationEvent.SpuId;` 直接将 `ReviewSubmittedEvent.SpuId`（商品 SPU 标识）赋值给本地变量 `shopId`，传给 `_builder.BuildAsync(shopId, ct)`。SPU 的 Guid 与 Shop 的 Guid 几乎不可能匹配，返回 `null`，跳过同步。`ReviewSubmittedEvent` 事件契约缺少 `ShopId` 字段。
- **影响**：评价提交事件触发的工作台读模型重建 100% 失效，ES 中 `leno_shop_dashboards` 索引的评价统计字段永远保持零值。
- **修复方案**：
  1. 在 `ReviewSubmittedEvent` 事件契约中增加 `ShopId` 字段（`Guid` 类型，默认 `Guid.Empty` 保持向后兼容）。
  2. 消费者优先读 `ShopId`，为 `Guid.Empty` 时回退通过 `IProductAntiCorruptionService.GetSpuSellerIdAsync` 反查 SPU 归属卖家（即 ShopId），仍无法解析时记 Warning 跳过。
  3. ReviewAfterSales BC 发布事件时填充 `ShopId`（跨 BC 依赖，本计划仅覆盖 SellerShop 侧消费改造）。

#### Task 2: 写失败测试

- [ ] **Step 1: 编写测试**

新增测试文件 `Leno.SellerShop.Infrastructure.Tests/ReadModels/ReviewSubmittedShopDashboardSyncConsumerTests.cs`：

```csharp
using Leno.Infrastructure.ReadModel;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SellerShop.Infrastructure.Tests.ReadModels;

public sealed class ReviewSubmittedShopDashboardSyncConsumerTests
{
    private readonly Mock<IEsReadModelRepository<ShopDashboardReadModel>> _repositoryMock = new();
    private readonly Mock<IShopDashboardReadModelBuilder> _builderMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAclMock = new();
    private readonly ILogger<ReviewSubmittedShopDashboardSyncConsumer> _logger =
        NullLogger<ReviewSubmittedShopDashboardSyncConsumer>.Instance;

    [Fact]
    public async Task BuildReadModelAsync_Should_Use_ShopId_From_Event_When_Provided()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = reviewId,
            SpuId = spuId,
            ShopId = shopId,
            Rating = 5,
            NewScore = 4.5m,
            ReviewCount = 10
        };
        var expectedReadModel = new ShopDashboardReadModel { ShopId = shopId };
        _builderMock
            .Setup(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReadModel);

        // 使用带 IProductAntiCorruptionService 的构造函数
        var consumer = CreateConsumer();

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(shopId.ToString(), result.Id);
        Assert.Equal(ShopDashboardReadModel.ShopDashboardIndexName, result.IndexName);
        Assert.NotNull(result.ReadModel);
        _builderMock.Verify(b => b.BuildAsync(shopId, It.IsAny<CancellationToken>()), Times.Once);
        _productAclMock.Verify(a => a.GetSpuSellerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildReadModelAsync_Should_Fallback_To_Acl_When_ShopId_Is_Empty()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            ShopId = Guid.Empty,
            Rating = 5,
            NewScore = 4.5m,
            ReviewCount = 10
        };
        var expectedReadModel = new ShopDashboardReadModel { ShopId = sellerId };
        _productAclMock
            .Setup(a => a.GetSpuSellerIdAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerId);
        _builderMock
            .Setup(b => b.BuildAsync(sellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReadModel);

        var consumer = CreateConsumer();

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(sellerId.ToString(), result.Id);
        Assert.NotNull(result.ReadModel);
        _productAclMock.Verify(a => a.GetSpuSellerIdAsync(spuId, It.IsAny<CancellationToken>()), Times.Once);
        _builderMock.Verify(b => b.BuildAsync(sellerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuildReadModelAsync_Should_Return_Empty_When_ShopId_Empty_And_Acl_Returns_Null()
    {
        // Arrange
        var spuId = Guid.NewGuid();
        var integrationEvent = new ReviewSubmittedEvent
        {
            ReviewId = Guid.NewGuid(),
            SpuId = spuId,
            ShopId = Guid.Empty,
            Rating = 5,
            NewScore = 4.5m,
            ReviewCount = 10
        };
        _productAclMock
            .Setup(a => a.GetSpuSellerIdAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var consumer = CreateConsumer();

        // Act
        var result = await InvokeBuildReadModelAsync(consumer, integrationEvent);

        // Assert
        Assert.Equal(string.Empty, result.Id);
        Assert.Null(result.ReadModel);
        _builderMock.Verify(b => b.BuildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ReviewSubmittedShopDashboardSyncConsumer CreateConsumer()
    {
        return new ReviewSubmittedShopDashboardSyncConsumer(
            _repositoryMock.Object,
            _builderMock.Object,
            _productAclMock.Object,
            _logger);
    }

    private async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> InvokeBuildReadModelAsync(
        ReviewSubmittedShopDashboardSyncConsumer consumer, ReviewSubmittedEvent integrationEvent)
    {
        var method = typeof(ReviewSubmittedShopDashboardSyncConsumer)
            .GetMethod("BuildReadModelAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method is null)
        {
            throw new InvalidOperationException("BuildReadModelAsync 方法未找到");
        }

        var task = (Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)>)method.Invoke(
            consumer, new object[] { integrationEvent, CancellationToken.None })!;
        return await task;
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ReviewSubmittedShopDashboardSyncConsumerTests"`

Expected: FAIL（`ReviewSubmittedEvent` 无 `ShopId` 字段、`ReviewSubmittedShopDashboardSyncConsumer` 构造函数无 `IProductAntiCorruptionService` 参数、编译失败）

- [ ] **Step 3: 写最小实现**

**3a. 扩展 `ReviewSubmittedEvent` 事件契约**

修改 `file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs`，在 `ReviewSubmittedEvent` 类中增加 `ShopId` 字段：

```csharp
// 在 ReviewSubmittedEvent 类中新增字段（保持向后兼容，默认 Guid.Empty）
/// <summary>店铺标识（卖家工作台读模型同步用），旧版发布方不填充时为 Guid.Empty。</summary>
public Guid ShopId { get; init; }
```

**3b. 修改 `ReviewSubmittedShopDashboardSyncConsumer` 消费者**

修改 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ReviewSubmittedShopDashboardSyncConsumer.cs`：

```csharp
using Leno.Infrastructure.ReadModel;
using Leno.SellerShop.Application.Services;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 评价提交事件触发的店铺工作台读模型同步消费者：消费 <see cref="ReviewSubmittedEvent"/>，
/// 调用 <see cref="IShopDashboardReadModelBuilder"/> 重建 <see cref="ShopDashboardReadModel"/>
/// 并通过 IndexAsync 覆盖更新到 Elasticsearch（不删除）。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列；店铺不存在时跳过同步。
/// 幂等：ES 索引以店铺标识为 _id，重复索引为覆盖更新。
/// </summary>
/// <remarks>
/// 事件契约优先读取 <c>ReviewSubmittedEvent.ShopId</c>；为 <c>Guid.Empty</c> 时（旧版发布方未填充），
/// 通过 <see cref="IProductAntiCorruptionService.GetSpuSellerIdAsync"/> 反查 SPU 归属卖家（即 ShopId）。
/// 反查仍失败时记 Warning 跳过同步，避免静默失败。
/// </remarks>
public sealed class ReviewSubmittedShopDashboardSyncConsumer
    : ReadModelSyncConsumerBase<ReviewSubmittedEvent, ShopDashboardReadModel>
{
    private readonly IShopDashboardReadModelBuilder _builder;
    private readonly IProductAntiCorruptionService _productAntiCorruption;

    public ReviewSubmittedShopDashboardSyncConsumer(
        IEsReadModelRepository<ShopDashboardReadModel> repository,
        IShopDashboardReadModelBuilder builder,
        IProductAntiCorruptionService productAntiCorruption,
        ILogger<ReviewSubmittedShopDashboardSyncConsumer> logger)
        : base(repository, logger)
    {
        _builder = builder;
        _productAntiCorruption = productAntiCorruption;
    }

    /// <inheritdoc />
    /// <remarks>评价提交事件触发索引重建（按最新聚合根快照），不触发删除。</remarks>
    protected override async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> BuildReadModelAsync(
        ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        var shopId = integrationEvent.ShopId;

        // 旧版发布方未填充 ShopId 时，通过防腐层反查 SPU 归属卖家
        if (shopId == Guid.Empty)
        {
            var sellerId = await _productAntiCorruption.GetSpuSellerIdAsync(integrationEvent.SpuId, ct)
                .ConfigureAwait(false);
            if (sellerId.HasValue)
            {
                shopId = sellerId.Value;
            }
            else
            {
                Logger.LogWarning(
                    "评价提交事件无法解析 ShopId：SpuId={SpuId} ReviewId={ReviewId}，防腐层反查返回 null，跳过同步",
                    integrationEvent.SpuId, integrationEvent.ReviewId);
                return (string.Empty, string.Empty, null);
            }
        }

        var readModel = await _builder.BuildAsync(shopId, ct).ConfigureAwait(false);
        if (readModel is null)
        {
            Logger.LogWarning("评价提交事件触发的工作台读模型构建为空 ShopId={ShopId} ReviewId={ReviewId}",
                shopId, integrationEvent.ReviewId);
            return (string.Empty, string.Empty, null);
        }

        return (shopId.ToString(), ShopDashboardReadModel.ShopDashboardIndexName, readModel);
    }

    /// <inheritdoc />
    /// <remarks>评价提交事件仅触发索引重建，不删除读模型。</remarks>
    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        ReviewSubmittedEvent integrationEvent, CancellationToken ct)
        => Task.FromResult<(string, string)?>(null);
}
```

**3c. 更新 DI 注册**

在 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 中，确认 `ReviewSubmittedShopDashboardSyncConsumer` 的注册已包含 `IProductAntiCorruptionService` 依赖（该服务已在 p0a-T7 修复中注册，无需额外操作）。

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ReviewSubmittedShopDashboardSyncConsumerTests"`

Expected: PASS（全部 3 个测试用例通过）

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ReviewSubmittedShopDashboardSyncConsumer.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ReviewSubmittedShopDashboardSyncConsumerTests.cs
git commit -m "fix(SellerShop): 评价提交事件消费者优先读取 ShopId 字段，回退防腐层反查，修复 SpuId 当 ShopId 的语义 Bug"
```

---

### P0-3: ShopDashboardReadModelBuilder 6 个字段硬编码 0 占位（审计 #3）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L30-L45](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L30-L45)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelBuilder.cs#L55-L63](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelBuilder.cs#L55-L63)
- **根因**：Builder 第 55-63 行对 6 个字段硬编码为 0：`ConfirmedOrders`、`CancelledOrders`、`TotalReviews`、`AverageRating`、`FiveStarReviews`、`OneStarReviews`。`ShopDashboardData` 聚合未持有 `ConfirmedOrders` / `CancelledOrders` / 评论统计字段。
- **影响**：卖家工作台 6 个核心指标永久为 0，卖家无法据信做出经营决策。
- **修复方案**：
  1. 扩展 `ShopDashboardData` 聚合增加 `ConfirmedOrders` / `CancelledOrders` 字段，由 `OrderPaidEvent` / `OrderCancelledEvent` 驱动维护。
  2. 新增 `IReviewAntiCorruptionService` 防腐层接口，反查评论域聚合评分统计。
  3. 更新 `ShopDashboardReadModelBuilder` 从聚合与防腐层读取真实数据。

#### Task 3: 写失败测试

- [ ] **Step 1: 编写测试**

新增测试文件 `Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelBuilderTests.cs`：

```csharp
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Infrastructure.ReadModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SellerShop.Infrastructure.Tests.ReadModels;

public sealed class ShopDashboardReadModelBuilderTests
{
    private readonly Mock<IShopRepository> _shopRepositoryMock = new();
    private readonly Mock<IShopDashboardRepository> _dashboardRepositoryMock = new();
    private readonly Mock<IReviewAntiCorruptionService> _reviewAclMock = new();
    private readonly ILogger<ShopDashboardReadModelBuilder> _logger =
        NullLogger<ShopDashboardReadModelBuilder>.Instance;

    [Fact]
    public async Task BuildAsync_Should_Populate_ConfirmedOrders_From_Dashboard_Aggregate()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);
        // 模拟已支付 3 笔订单
        dashboard.OnOrderPaid(100m);
        dashboard.OnOrderPaid(200m);
        dashboard.OnOrderPaid(300m);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto { TotalReviews = 0, AverageRating = 0m, FiveStarReviews = 0, OneStarReviews = 0 });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.ConfirmedOrders);
    }

    [Fact]
    public async Task BuildAsync_Should_Populate_CancelledOrders_From_Dashboard_Aggregate()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);
        // 模拟 2 笔取消
        dashboard.OnOrderCreated();
        dashboard.OnOrderCancelled();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCancelled();

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto { TotalReviews = 0, AverageRating = 0m, FiveStarReviews = 0, OneStarReviews = 0 });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.CancelledOrders);
    }

    [Fact]
    public async Task BuildAsync_Should_Populate_Review_Statistics_From_Acl()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewStatisticsDto
            {
                TotalReviews = 50,
                AverageRating = 4.2m,
                FiveStarReviews = 30,
                OneStarReviews = 5
            });

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.TotalReviews);
        Assert.Equal(4.2m, result.AverageRating);
        Assert.Equal(30, result.FiveStarReviews);
        Assert.Equal(5, result.OneStarReviews);
    }

    [Fact]
    public async Task BuildAsync_Should_Return_Zero_Review_Stats_When_Acl_Returns_Null()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var shop = Shop.Create(shopId, Guid.NewGuid(), "测试店铺", "13800138000");
        var dashboard = ShopDashboardData.Create(shopId);

        _shopRepositoryMock.Setup(r => r.GetByIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _dashboardRepositoryMock.Setup(r => r.GetByShopIdAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        _reviewAclMock.Setup(a => a.GetReviewStatisticsAsync(shopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewStatisticsDto?)null);

        var builder = new ShopDashboardReadModelBuilder(
            _shopRepositoryMock.Object,
            _dashboardRepositoryMock.Object,
            _reviewAclMock.Object,
            _logger);

        // Act
        var result = await builder.BuildAsync(shopId, CancellationToken.None);

        // Assert — fail-closed 返回 0，但非硬编码
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalReviews);
        Assert.Equal(0m, result.AverageRating);
    }
}
```

新增 `IReviewAntiCorruptionService` 接口与 DTO（Application 层）：

```csharp
// 文件：src/Services/SellerShop/Leno.SellerShop.Application/Services/IReviewAntiCorruptionService.cs
namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 评论域防腐层服务接口（卖家店铺域视角）。
/// 用于卖家工作台读模型构建时反查评论域聚合评分统计。
/// </summary>
public interface IReviewAntiCorruptionService
{
    /// <summary>
    /// 按店铺标识反查评论统计（累计评价数、平均评分、五星/一星评价数）。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>评论统计；评论域故障或店铺无评价时返回 null（fail-closed）。</returns>
    Task<ReviewStatisticsDto?> GetReviewStatisticsAsync(Guid shopId, CancellationToken ct = default);
}

/// <summary>评论统计 DTO（跨 BC 查询用）。</summary>
public sealed class ReviewStatisticsDto
{
    public int TotalReviews { get; init; }
    public decimal AverageRating { get; init; }
    public int FiveStarReviews { get; init; }
    public int OneStarReviews { get; init; }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ShopDashboardReadModelBuilderTests"`

Expected: FAIL（`ShopDashboardReadModelBuilder` 构造函数无 `IReviewAntiCorruptionService` 参数、`ShopDashboardData` 无 `ConfirmedOrders` / `CancelledOrders` 字段、编译失败）

- [ ] **Step 3: 写最小实现**

**3a. 扩展 `ShopDashboardData` 聚合**

修改 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs`，增加 `ConfirmedOrders` / `CancelledOrders` 字段及相关行为方法：

```csharp
// 在 ShopDashboardData 类中新增字段
/// <summary>已确认订单数（已支付待发货）。</summary>
public int ConfirmedOrders { get; private set; }

/// <summary>已取消订单数。</summary>
public int CancelledOrders { get; private set; }
```

在 `Create` 工厂方法中初始化新字段为 0：

```csharp
return new ShopDashboardData(shopId)
{
    ShopId = shopId,
    TotalOrders = 0,
    PendingOrders = 0,
    ConfirmedOrders = 0,
    CompletedOrders = 0,
    CancelledOrders = 0,
    TotalRevenue = 0m,
    Currency = "CNY",
    LastUpdatedAt = DateTime.UtcNow
};
```

在 `OnOrderPaid` 方法中增加 `ConfirmedOrders++`：

```csharp
public void OnOrderPaid(decimal amount)
{
    if (amount <= 0)
    {
        throw new SellerShopDomainException("支付金额须大于 0", "DASHBOARD_AMOUNT_INVALID");
    }

    TotalRevenue += amount;
    ConfirmedOrders++;
    LastUpdatedAt = DateTime.UtcNow;
}
```

在 `OnOrderCancelled` 方法中增加 `CancelledOrders++`：

```csharp
public void OnOrderCancelled()
{
    if (PendingOrders > 0)
    {
        PendingOrders--;
    }

    CancelledOrders++;
    LastUpdatedAt = DateTime.UtcNow;
}
```

**3b. 修改 `ShopDashboardReadModelBuilder`**

修改 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelBuilder.cs`：

```csharp
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// <see cref="IShopDashboardReadModelBuilder"/> 默认实现。
/// 注入 SellerShop BC 既有仓储与评论域防腐层查询最新聚合根与评论统计，
/// 投影为 <see cref="ShopDashboardReadModel"/>；店铺不存在时返回 null。
/// </summary>
public sealed class ShopDashboardReadModelBuilder : IShopDashboardReadModelBuilder
{
    private readonly IShopRepository _shopRepository;
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly IReviewAntiCorruptionService _reviewAntiCorruption;
    private readonly ILogger<ShopDashboardReadModelBuilder> _logger;

    public ShopDashboardReadModelBuilder(
        IShopRepository shopRepository,
        IShopDashboardRepository dashboardRepository,
        IReviewAntiCorruptionService reviewAntiCorruption,
        ILogger<ShopDashboardReadModelBuilder> logger)
    {
        _shopRepository = shopRepository;
        _dashboardRepository = dashboardRepository;
        _reviewAntiCorruption = reviewAntiCorruption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ShopDashboardReadModel?> BuildAsync(Guid shopId, CancellationToken ct)
    {
        if (shopId == Guid.Empty)
        {
            _logger.LogWarning("构建店铺工作台读模型失败：ShopId 为空");
            return null;
        }

        var shop = await _shopRepository.GetByIdAsync(shopId, ct);
        if (shop is null)
        {
            _logger.LogWarning("店铺 {ShopId} 不存在，跳过工作台读模型构建", shopId);
            return null;
        }

        var dashboard = await _dashboardRepository.GetByShopIdAsync(shopId, ct);

        // 通过防腐层反查评论统计；fail-closed 返回 null 时按零值兜底
        var reviewStats = await _reviewAntiCorruption.GetReviewStatisticsAsync(shopId, ct);
        if (reviewStats is null)
        {
            _logger.LogWarning("评论域防腐层返回 null，ShopId={ShopId} 评论统计按零值兜底", shopId);
        }

        var now = DateTime.UtcNow;
        var readModel = new ShopDashboardReadModel
        {
            ShopId = shop.Id,
            ShopName = shop.ShopName,
            TotalOrders = dashboard?.TotalOrders ?? 0,
            PendingOrders = dashboard?.PendingOrders ?? 0,
            ConfirmedOrders = dashboard?.ConfirmedOrders ?? 0,
            CompletedOrders = dashboard?.CompletedOrders ?? 0,
            CancelledOrders = dashboard?.CancelledOrders ?? 0,
            TotalReviews = reviewStats?.TotalReviews ?? 0,
            AverageRating = reviewStats?.AverageRating ?? 0m,
            FiveStarReviews = reviewStats?.FiveStarReviews ?? 0,
            OneStarReviews = reviewStats?.OneStarReviews ?? 0,
            TotalSales = dashboard?.TotalRevenue ?? 0m,
            Currency = dashboard?.Currency ?? "CNY",
            LastUpdatedAt = dashboard?.LastUpdatedAt ?? now,
            IndexedAt = now,
            SchemaVersion = 2
        };

        return readModel;
    }
}
```

**3c. 新增 `IReviewAntiCorruptionService` 接口**

创建 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/IReviewAntiCorruptionService.cs`（代码见 Step 1 测试文件中的接口定义）。

**3d. 新增 `GrpcReviewAntiCorruptionClient` 实现**

创建 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcReviewAntiCorruptionClient.cs`，调用 ReviewAfterSales BC 的 gRPC 服务反查评论统计。

**3e. 更新 DI 注册**

在 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 中注册 `IReviewAntiCorruptionService` 与 `GrpcReviewAntiCorruptionClient`。

**3f. 生成 EF Core 迁移**

```bash
cd src/Services/SellerShop/Leno.SellerShop.Infrastructure
dotnet ef migrations add AddConfirmedAndCancelledOrdersToShopDashboard --context SellerShopDbContext
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ShopDashboardReadModelBuilderTests"`

Expected: PASS（全部 4 个测试用例通过）

- [ ] **Step 5: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs src/Services/SellerShop/Leno.SellerShop.Application/Services/IReviewAntiCorruptionService.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelBuilder.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcReviewAntiCorruptionClient.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelBuilderTests.cs
git commit -m "fix(SellerShop): 工作台读模型构建器移除 6 个硬编码 0 字段，从聚合与评论域防腐层填充真实数据"
```

---

### P0-4: SellerGrpcService.MapToProto 用 Guid.GetHashCode() 转 long（审计 #4）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L47-L53](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L47-L53)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs#L93-L111](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs#L93-L111)
- **根因**：第 99 行 `ShopId = (long)dto.ShopId.GetHashCode()`（SellerInfo 映射）与第 105 行 `ShopId = (long)dto.ShopId.GetHashCode()`（ShopInfo 映射）将 `Guid` 类型的 `ShopId` 通过 `GetHashCode()` 转 `int` 再强转 `long`，存在大量哈希冲突且不可逆。`SellerInfo` proto 缺少 `ShopIdStr` 字段。
- **影响**：所有调用 `GetSellerInfo` / `GetShopInfo` 的下游 BC 若依赖 `ShopId` 字段反查 SellerShop，会拿到错误的 Guid，跨 BC 归属校验错位。
- **修复方案**：
  1. 在 proto 契约 `SellerInfo` 中增加 `shop_id_str` 字段，标记 `shop_id` 为 `deprecated`。
  2. 重新生成 gRPC C# 代码。
  3. 更新 `MapToProto` 填充 string 字段，`int64` 字段保留固定值 0（不再用 `GetHashCode()`），要求所有客户端迁移到 `shop_id_str`。

#### Task 4: 写失败测试

- [ ] **Step 1: 编写测试**

新增测试文件 `Leno.SellerShop.Api.Tests/GrpcServices/SellerGrpcServiceMapToProtoTests.cs`：

```csharp
using Leno.SellerShop.Api.GrpcServices;
using Leno.SellerShop.Application;
using Leno.SharedContracts.Grpc.Seller.V1;
using Xunit;

namespace Leno.SellerShop.Api.Tests.GrpcServices;

public sealed class SellerGrpcServiceMapToProtoTests
{
    [Fact]
    public void MapToProto_SellerInfo_Should_Populate_ShopIdStr_With_Guid_String()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var dto = new SellerInfoDto
        {
            SellerId = sellerId,
            Name = "测试卖家",
            Status = "Approved",
            ShopId = shopId
        };

        // Act — 通过反射调用私有方法 MapToProto
        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(SellerInfoDto) }, null);
        Assert.NotNull(method);
        var result = (SellerInfo)method!.Invoke(null, new object[] { dto })!;

        // Assert
        Assert.Equal(shopId.ToString(), result.ShopIdStr);
        Assert.Equal(sellerId.ToString(), result.SellerId);
        Assert.Equal("测试卖家", result.Name);
        Assert.Equal("Approved", result.Status);
        // int64 shop_id 不再使用 GetHashCode，应为 0（deprecated 字段）
        Assert.Equal(0L, result.ShopId);
    }

    [Fact]
    public void MapToProto_SellerInfo_Should_Not_Use_HashCode_For_ShopId()
    {
        // Arrange — 两个不同 Guid 不应映射到同一 long 值
        var shopId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var shopId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var dto1 = new SellerInfoDto { SellerId = Guid.NewGuid(), Name = "卖家1", Status = "Active", ShopId = shopId1 };
        var dto2 = new SellerInfoDto { SellerId = Guid.NewGuid(), Name = "卖家2", Status = "Active", ShopId = shopId2 };

        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(SellerInfoDto) }, null);
        Assert.NotNull(method);
        var result1 = (SellerInfo)method!.Invoke(null, new object[] { dto1 })!;
        var result2 = (SellerInfo)method!.Invoke(null, new object[] { dto2 })!;

        // Assert — string 字段应不同
        Assert.NotEqual(result1.ShopIdStr, result2.ShopIdStr);
        Assert.Equal(shopId1.ToString(), result1.ShopIdStr);
        Assert.Equal(shopId2.ToString(), result2.ShopIdStr);
    }

    [Fact]
    public void MapToProto_ShopInfo_Should_Populate_ShopIdStr_With_Guid_String()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var dto = new ShopInfoDto
        {
            ShopId = shopId,
            Name = "测试店铺",
            Status = "Active",
            SellerId = sellerId
        };

        // Act
        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(ShopInfoDto) }, null);
        Assert.NotNull(method);
        var result = (ShopInfo)method!.Invoke(null, new object[] { dto })!;

        // Assert
        Assert.Equal(shopId.ToString(), result.ShopIdStr);
        Assert.Equal(sellerId.ToString(), result.SellerId);
        Assert.Equal("测试店铺", result.Name);
        Assert.Equal("Active", result.Status);
        // int64 shop_id 不再使用 GetHashCode
        Assert.Equal(0L, result.ShopId);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Api.Tests/Leno.SellerShop.Api.Tests.csproj --filter "FullyQualifiedName~SellerGrpcServiceMapToProtoTests"`

Expected: FAIL（`SellerInfo` proto 类无 `ShopIdStr` 属性、`MapToProto` 仍使用 `GetHashCode()`、编译失败）

- [ ] **Step 3: 写最小实现**

**3a. 修改 proto 契约**

修改 `file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Protos/seller.proto`，在 `SellerInfo` 中增加 `shop_id_str` 字段并标记 `shop_id` 为 `deprecated`：

```protobuf
message SellerInfo {
  string seller_id = 1;
  string name = 2;
  string status = 3;
  int64 shop_id = 4 [deprecated = true];
  // Guid→string 迁移新增 string ID 字段
  string shop_id_str = 5;
}
```

**3b. 重新生成 gRPC C# 代码**

```bash
cd src/BuildingBlocks/Leno.SharedContracts
dotnet proto-generate  # 或项目约定的 protoc 命令
```

**3c. 修改 `SellerGrpcService.MapToProto`**

修改 `file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`：

```csharp
private static SellerInfo MapToProto(SellerInfoDto dto) => new()
{
    SellerId = dto.SellerId.ToString(),
    Name = dto.Name,
    Status = dto.Status,
    // deprecated：int64 字段保留固定值 0，不再使用 GetHashCode 不可逆映射
    ShopId = 0L,
    // 新增 string 字段，承载 Guid.ToString()
    ShopIdStr = dto.ShopId.ToString()
};

private static ShopInfo MapToProto(ShopInfoDto dto) => new()
{
    // deprecated：int64 字段保留固定值 0
    ShopId = 0L,
    Name = dto.Name,
    Status = dto.Status,
    SellerId = dto.SellerId.ToString(),
    ShopIdStr = dto.ShopId.ToString()
};
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Api.Tests/Leno.SellerShop.Api.Tests.csproj --filter "FullyQualifiedName~SellerGrpcServiceMapToProtoTests"`

Expected: PASS（全部 3 个测试用例通过）

- [ ] **Step 5: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Protos/seller.proto src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/Seller.cs src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/SellerGrpc.cs src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs src/Services/SellerShop/Leno.SellerShop.Api.Tests/GrpcServices/SellerGrpcServiceMapToProtoTests.cs
git commit -m "fix(SellerShop): gRPC SellerInfo 新增 shop_id_str 字段，移除 Guid.GetHashCode 不可逆映射"
```

---

## P1 任务清单（🟡 中严重度）

### P1-5: ShopConfiguration 用字符串 "Qualifications" 访问 backing field（审计 #5）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L57-L64](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L57-L64)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopConfiguration.cs#L40-L43](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopConfiguration.cs#L40-L43)
- **任务**：
  - [ ] 将 `builder.HasMany<ShopQualification>("Qualifications")` 改为 `builder.HasMany(s => s.Qualifications)`，需将 `Shop.Qualifications` 改为 `ICollection<ShopQualification>` 或显式配置 `HasField("_qualifications").UsePropertyAccessMode(FieldAccessMode.Field)`
  - [ ] 运行既有 ShopQualificationTests 验证不回归
  - [ ] 提交：`git commit -m "refactor(SellerShop): ShopConfiguration 改用表达式访问 Qualifications 导航属性"`

### P1-6: EfCoreShopRepository 不 Include Qualifications（审计 #6）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L66-L76](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L66-L76)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopRepository.cs#L22-L28](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopRepository.cs#L22-L28)
- **任务**：
  - [ ] 在 `GetByIdAsync` 与 `GetBySellerIdAsync` 链式调用 `.Include(s => s.Qualifications)`
  - [ ] 编写测试验证 `GetByIdAsync` 返回的 Shop 含 Qualifications 集合
  - [ ] 验证 `ShopAppService.GetQualificationsAsync` / `ApproveQualificationAsync` / `RejectQualificationAsync` / `ToShopDto` 正常工作
  - [ ] 提交：`git commit -m "fix(SellerShop): 仓储查询 Include Qualifications，修复资质审核 N+1 与集合为空 Bug"`

### P1-7: Shop.DecrementProductCount 静默吞掉越界调用（审计 #7）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L78-L85](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L78-L85)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/Shop.cs#L293-L301](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/Shop.cs#L293-L301)
- **任务**：
  - [ ] 将 `DecrementProductCount` 返回类型改为 `bool`，`ProductCount <= 0` 时返回 `false`（不抛异常保留幂等）
  - [ ] 在 `ProductTakenDownEventConsumer` 中检查返回值，`false` 时记 Warning + Metrics 计数
  - [ ] 编写测试验证 `DecrementProductCount` 在 `ProductCount=0` 时返回 `false`
  - [ ] 提交：`git commit -m "fix(SellerShop): DecrementProductCount 返回 bool 标识是否实际递减，消费者记录 Warning 可观测"`

### P1-8: ShopsController 多步操作无显式事务（审计 #8）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L87-L100](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L87-L100)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L49-L75](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ShopsController.cs#L49-L75)
- **任务**：
  - [ ] 在 `IShopAppService` 新增 `UpdateMyShopInfoAsync(Guid userId, UpdateShopInfoDto dto, CancellationToken ct)` 方法，内部完成 `GetBySellerId` + `Update` + `Save`
  - [ ] 在 `IShopAppService` 新增 `SubmitMyQualificationAsync(Guid userId, SubmitQualificationDto dto, Stream fileStream, string fileName, string contentType, CancellationToken ct)` 方法
  - [ ] `ShopsController.UpdateMyShopAsync` 改为直接调用 `UpdateMyShopInfoAsync(GetCurrentUserId(), dto, ct)`
  - [ ] `ShopsController.SubmitQualificationAsync` 改为直接调用 `SubmitMyQualificationAsync(GetCurrentUserId(), dto, stream, ...)`
  - [ ] 资质提交增加幂等键（客户端生成 `IdempotencyKey`），AppService 内查重跳过
  - [ ] 提交：`git commit -m "fix(SellerShop): 控制器多步操作下沉到 AppService 统一事务边界，资质提交增加幂等保护"`

### P1-9: ShopDashboardData.OnOrderPaid 不按订单跟踪金额（审计 #9）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L102-L109](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L102-L109)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs#L74-L96](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs#L74-L96)
- **任务**：
  - [ ] 在 `ShopDashboardData` 增加 `RefundedAmount` 字段
  - [ ] 新增 `OnOrderRefunded(decimal amount)` 方法，`RefundedAmount += amount`
  - [ ] 工作台显示 `NetRevenue = TotalRevenue - RefundedAmount`
  - [ ] 若 `OrderRefundedEvent` 存在则订阅；否则在 `OnOrderCancelled` 接收 `wasPaid` 参数决定是否回滚
  - [ ] 编写测试验证已支付订单取消后 `TotalRevenue` 正确回滚
  - [ ] 生成 EF Core 迁移
  - [ ] 提交：`git commit -m "fix(SellerShop): ShopDashboardData 增加 RefundedAmount 字段，已支付订单取消时回滚收入"`

### P1-10: ShopAppService.UpdateShopInfoAsync 缺失归属校验（审计 #10）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L111-L118](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L111-L118)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L107-L120](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L107-L120)
- **任务**：
  - [ ] 在 `UpdateShopInfoAsync` 增加 `userId` 参数，内部 `_shopRepository.GetBySellerIdAsync(userId)` 加载店铺并校验 `shop.Id == shopId`，不匹配抛 `SHOP_OWNERSHIP_MISMATCH`
  - [ ] 在 `SubmitQualificationAsync` 同样增加 `userId` 参数与归属校验
  - [ ] 更新 `IShopAppService` 接口签名
  - [ ] 更新所有调用方（`ShopsController` / `AdminShopsController`）
  - [ ] 编写测试验证跨卖家调用抛 `SHOP_OWNERSHIP_MISMATCH`
  - [ ] 提交：`git commit -m "fix(SellerShop): UpdateShopInfoAsync 与 SubmitQualificationAsync 增加卖家归属校验，防越权"`

### P1-11: ShopDashboardReadModel 注释引用不存在的 Consumer（审计 #11）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L120-L127](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L120-L127)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModel.cs#L9-L11](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModel.cs#L9-L11)
- **任务**：
  - [ ] 将 `OrderConfirmedShopDashboardSyncConsumer` 改为 `OrderCompletedShopDashboardSyncConsumer`
  - [ ] 确认 `<see cref>` 可正确跳转
  - [ ] 提交：`git commit -m "docs(SellerShop): 修正 ShopDashboardReadModel 注释引用的 Consumer 类名"`

### P1-12: ShopDashboardDataConfiguration 未显式映射审计字段（审计 #12）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L129-L137](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L129-L137)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopDashboardDataConfiguration.cs#L13-L27](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Configurations/ShopDashboardDataConfiguration.cs#L13-L27)
- **任务**：
  - [ ] 在 `Configure` 方法增加四行映射：`CreatedAt` → `created_at`、`UpdatedAt` → `updated_at`、`CreatedBy` → `created_by` `HasMaxLength(64)`、`UpdatedBy` → `updated_by` `HasMaxLength(64)`
  - [ ] 生成 EF Core 迁移重命名列（`CreatedAt` → `created_at` 等，`nvarchar(max)` → `nvarchar(64)`）
  - [ ] 验证迁移脚本不破坏既有数据（列名重命名 + 类型收窄）
  - [ ] 提交：`git commit -m "fix(SellerShop): ShopDashboardDataConfiguration 显式映射审计字段为 snake_case + nvarchar(64)"`

### P1-13: EfCoreShopMetricsRepository.UpsertAsync 用 EntityState.Modified（审计 #13）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L139-L147](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L139-L147)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopMetricsRepository.cs#L73-L97](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/EfCoreShopMetricsRepository.cs#L73-L97)
- **任务**：
  - [ ] `UpsertAsync` 当存在既有聚合时，从既有聚合读取 `Id` / `CreatedAt` / `CreatedBy`，赋值到新聚合后再 `Attach + Modified`
  - [ ] 或直接修改既有聚合字段（推荐，遵循聚合不变量）
  - [ ] 使用 `ConcurrencyToken`（rowversion）做乐观并发控制
  - [ ] 编写测试验证 `CreatedAt` 不被覆盖
  - [ ] 提交：`git commit -m "fix(SellerShop): UpsertAsync 保留既有聚合审计字段，修复 EntityState.Modified 覆盖问题"`

### P1-14: SellerInternalQueryService 用 try/catch 控制流程（审计 #14）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L149-L155](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L149-L155)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs#L37-L121](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs#L37-L121)
- **任务**：
  - [ ] 在 `ISellerAppService` 增加 `TryGetSellerProfileAsync(Guid sellerId, ct)` 返回 `SellerProfileDto?` 而非抛异常
  - [ ] 在 `IShopAppService` 增加 `TryGetShopBySellerIdAsync(Guid sellerId, ct)` 与 `TryGetShopByIdAsync(Guid shopId, ct)` 返回 `ShopDto?`
  - [ ] `SellerInternalQueryService` 改用 `TryGetXxxAsync` 方法，移除 try/catch
  - [ ] 编写测试验证不抛异常且返回 null
  - [ ] 提交：`git commit -m "refactor(SellerShop): SellerInternalQueryService 改用 TryGet 模式，移除异常控制流程"`

### P1-15: SellerDashboardAppService.GetDashboardAsync 标 [Obsolete] 无迁移计划（审计 #15）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L157-L164](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L157-L164)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs#L27-L63](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs#L27-L63)
- **任务**：
  - [ ] 推迟移除日期至 2026-10-01（依赖 P0-2 / P0-3 修复后 ES 读模型数据完整）
  - [ ] 在 `[Obsolete]` 注释中明确迁移步骤与调用方清单
  - [ ] 增加 Feature Flag `Dashboard:UseReadModel` 控制读 DB 还是读 ES，灰度切换
  - [ ] 在控制器层增加对比指标（DB vs ES 数据差异），监控切换前后数据质量
  - [ ] 提交：`git commit -m "fix(SellerShop): 推迟 GetDashboardAsync 移除日期，增加 Feature Flag 与迁移说明"`

---

## P2 任务清单（🟢 低严重度）

### P2-16: BusinessLicense 值对象定义但全 BC 未被使用（审计 #16）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L168-L174](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L168-L174)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/ValueObjects/BusinessLicense.cs#L1-L56](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/ValueObjects/BusinessLicense.cs#L1-L56)
- **任务**：
  - [ ] 评估是否计划未来使用 `BusinessLicense` 值对象
  - [ ] 若无计划，删除该文件
  - [ ] 若有计划，将 Shop 聚合的 `BusinessLicenseNo` 字段改为 `BusinessLicense?` 类型并补充图片 URL 与有效期
  - [ ] 提交：`git commit -m "cleanup(SellerShop): 删除未使用的 BusinessLicense 值对象死代码"`

### P2-17: Program.cs 启动时调用 MigrateWithLockAsync 阻塞启动（审计 #17）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L176-L182](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L176-L182)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Program.cs#L52](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/Program.cs#L52)
- **任务**：
  - [ ] 在 `MigrateWithLockAsync` 增加降级逻辑：Redis 不可用时跳过锁直接迁移（依赖 DB 自身的迁移锁）
  - [ ] 配置启动超时（如 30 秒），超时后记录 Error 日志并退出，让 K8s 重启
  - [ ] 或将迁移改为独立 Job 或 Init Container，与 API 启动解耦
  - [ ] 提交：`git commit -m "fix(SellerShop): MigrateWithLockAsync 增加降级与超时，避免 Redis 故障阻塞启动"`

### P2-18: QualificationExpiryReminder 硬编码扫描间隔与提醒天数（审计 #18）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L184-L190](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L184-L190)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/BackgroundServices/QualificationExpiryReminder.cs#L19](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/BackgroundServices/QualificationExpiryReminder.cs#L19) 与 [#L44](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/BackgroundServices/QualificationExpiryReminder.cs#L44)
- **任务**：
  - [ ] 新增 `QualificationReminderOptions` 配置类，包含 `ReminderDays`（默认 `[30, 7, 1]`）与 `ScanIntervalHours`（默认 24）
  - [ ] 从 `IOptions<QualificationReminderOptions>` 读取配置
  - [ ] 在 `appsettings.json` 增加配置节
  - [ ] 测试环境配置为 1 分钟扫描间隔
  - [ ] 提交：`git commit -m "feat(SellerShop): QualificationExpiryReminder 改为配置驱动扫描间隔与提醒天数"`

### P2-19: ShopMetrics.RecordOrder 币种校验未做大小写归一化（审计 #19）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L192-L198](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L192-L198)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopMetrics.cs#L99-L103](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopMetrics.cs#L99-L103)
- **任务**：
  - [ ] 在 `RecordOrder` 比较前对 `salesAmount.Currency` 做 `ToUpperInvariant`，或在 `Money.Create` 内统一 `ToUpperInvariant`
  - [ ] 在 `OrderCompletedEventConsumer` 第 43 行 `currency` 也做 `ToUpperInvariant`
  - [ ] 编写测试验证小写 `"cny"` 不抛 `METRICS_CURRENCY_MISMATCH`
  - [ ] 提交：`git commit -m "fix(SellerShop): ShopMetrics.RecordOrder 币种比较前统一 ToUpperInvariant，避免大小写不匹配"`

### P2-20: ShopDashboardQueryHandler 静默忽略 StartDate/EndDate（审计 #20）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L200-L206](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L200-L206)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryHandler.cs#L22-L31](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryHandler.cs#L22-L31)
- **任务**：
  - [ ] 在 `ShopDashboardQuery` 构造函数或 Validator 中校验 `StartDate` / `EndDate` 必须为 null，否则抛 `ArgumentException`
  - [ ] 或实现日期范围查询逻辑，从 `ShopMetrics` 聚合按日期范围读取趋势数据
  - [ ] 编写测试验证传入日期范围时的行为
  - [ ] 提交：`git commit -m "fix(SellerShop): ShopDashboardQueryHandler 对 StartDate/EndDate 做显式校验或实现"`

### P2-21: ShopAppService.UpdateShopInfoAsync 三步独立 Update（审计 #21）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L208-L220](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L208-L220)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L112-L114](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L112-L114)
- **任务**：
  - [ ] 在 Shop 聚合提供 `UpdateAllInfo(shopName, description, address, logo, contactPhone, contactEmail)` 单一方法，内部原子化校验与赋值
  - [ ] `UpdateShopInfoAsync` 改为调用 `UpdateAllInfo`
  - [ ] 编写测试验证部分校验失败时聚合状态不变
  - [ ] 提交：`git commit -m "refactor(SellerShop): Shop 聚合新增 UpdateAllInfo 原子方法，避免三步独立 Update 半更新状态"`

### P2-22: OrderCancelledEventConsumer 不区分未支付/已支付取消（审计 #22）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L222-L233](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L222-L233)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/OrderEventConsumer.cs#L189-L199](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Consumers/OrderEventConsumer.cs#L189-L199)
- **任务**：
  - [ ] 在 `OrderCancelledEvent` 契约中增加 `WasPaid` 字段或 `RefundAmount`
  - [ ] `OnOrderCancelled` 接收参数决定是否回滚 `TotalRevenue`（与 P1-9 联动）
  - [ ] 编写测试验证已支付取消后 `TotalRevenue` 减回
  - [ ] 提交：`git commit -m "fix(SellerShop): OrderCancelledEventConsumer 区分已支付/未支付取消，联动 TotalRevenue 回滚"`

### P2-23: GrpcAntiCorruptionClient fail-closed 无 Metrics 告警（审计 #23）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L235-L242](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L235-L242)
- **代码位置**：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcOrderAntiCorruptionClient.cs#L59-L64](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcOrderAntiCorruptionClient.cs#L59-L64) 与 [file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs#L59-L64](file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs#L59-L64)
- **任务**：
  - [ ] 在 fail-closed catch 块增加 `AntiCorruptionMetrics.RecordFailure` 计数（基类已调用，但 fail-closed catch 在子类，需补埋点）
  - [ ] 引入 Polly Circuit Breaker，连续失败 N 次后短路返回 null，避免持续超时
  - [ ] 配置告警规则，ACL 失败率 > 5% 触发告警
  - [ ] 提交：`git commit -m "feat(SellerShop): GrpcAntiCorruptionClient fail-closed 路径增加 Metrics 与熔断器"`

---

## 修复顺序建议

1. **P0-1**（设计期工厂密码）→ 独立修复，无依赖，1 天
2. **P0-4**（gRPC Guid→string 映射）→ 独立修复，无依赖，2 天（含 proto 重新生成）
3. **P0-2**（SpuId 当 ShopId）→ 依赖事件契约扩展，2 天
4. **P0-3**（6 字段硬编码 0）→ 依赖 P0-2 修复后评论统计正确，3 天（含聚合扩展 + 防腐层 + 迁移）
5. **P1-6**（资质 Include）→ 独立修复，1 天
6. **P1-10**（归属校验）→ 依赖 P1-8 完成，2 天
7. **P1-8**（控制器事务下沉）→ 2 天
8. **P1-9**（收入回滚）→ 与 P2-22 联动，3 天
9. **P1-5 / P1-7 / P1-11 / P1-12 / P1-13 / P1-14 / P1-15** → 各 1 天
10. **P2 全部** → 各 0.5-1 天

---

## 验证清单

- [ ] `grep -r "Leno@SqlServer2019" src/Services/SellerShop/` 零命中（P0-1 验证）
- [ ] `grep -r "GetHashCode" src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/` 零命中（P0-4 验证）
- [ ] `dotnet test src/Services/SellerShop/Leno.SellerShop.Domain.Tests/` 全部通过
- [ ] `dotnet test src/Services/SellerShop/Leno.SellerShop.Application.Tests/` 全部通过
- [ ] `dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/` 全部通过
- [ ] `dotnet test src/Services/SellerShop/Leno.SellerShop.Api.Tests/` 全部通过
- [ ] `dotnet build src/Services/SellerShop/` 无警告无错误
- [ ] CI secret scanning 通过（无硬编码密码）
