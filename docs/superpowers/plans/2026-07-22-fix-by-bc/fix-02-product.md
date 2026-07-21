# Product（商品域）修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md]
- 问题总数：🔴 5 / 🟡 10 / 🟢 5
- 已修复（跳过）：1 项
- 本计划覆盖：20 项

## 问题清单总表
| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 1 | 🔴 高 | ProductUpdatedDomainEvent 已注册翻译但 SPU 聚合永不抛出 — 集成事件契约断裂 | 02-product.md §1 | P0 | 待修复 |
| 2 | 🔴 高 | ProductUniquenessChecker TOCTOU 竞态 + DB 缺唯一约束 — SKU 编码与店铺内标题可重复 | 02-product.md §2 | P0 | 待修复 |
| 3 | 🔴 高 | StockBaseline.Replenish 发布事件时 ProductId=Guid.Empty — 下游同步失效 | 02-product.md §3 | P0 | 待修复 |
| 4 | 🔴 高 | EfCoreSPURepository.UpdateAsync 仅 Attach 不标记 Modified — ShopEventConsumer 流程下状态变更不持久化 | 02-product.md §4 | P0 | 待修复 |
| 5 | 🔴 高 | ProductGrpcService 使用 Guid.GetHashCode() 做 int64 映射 — 跨进程碰撞 | 02-product.md §5 | P0 | 待修复 |
| 6 | 🟡 中 | ProductSearchService 价格区间过滤仅校验 MinPrice，未校验 MaxPrice 重叠 | 02-product.md §6 | P1 | 待修复 |
| 7 | 🟡 中 | ProductSearchService sort 参数被静默忽略 | 02-product.md §7 | P1 | 待修复 |
| 8 | 🟡 中 | ProductInternalQueryService.GetSkuInfosBatchAsync 循环逐条查询 — N+1 | 02-product.md §8 | P1 | 待修复 |
| 9 | 🟡 中 | SpuReviewSummaryConsumer 增量评分计算浮点漂移 | 02-product.md §9 | P1 | 待修复 |
| 10 | 🟡 中 | SpuReviewSummaryConsumer.cs:151 TODO 占位 — ReviewModeratedEvent 评分同步未实现 | 02-product.md §10 | P1 | 待修复 |
| 11 | 🟡 中 | Money 值对象允许 amount=0 与 SKU 域 price>0 不一致 | 02-product.md §11 | P1 | 待修复 |
| 12 | 🟡 中 | ProductGrpcService PriceCents 截断非四舍五入 | 02-product.md §12 | P1 | 待修复 |
| 13 | 🟡 中 | PriceHistory.Create reason 永远为 null，ChangedBy 永远为 string.Empty — 审计信息缺失 | 02-product.md §13 | P1 | 待修复 |
| 14 | 🟡 中 | StockBaseline.SyncDeducted 异常在状态赋值后抛出 — 聚合内存状态不一致 | 02-product.md §14 | P1 | 待修复 |
| 15 | 🟡 中 | ProductReadModelSyncConsumer 默认 "CNY" 币种硬编码 — 多币种场景出错 | 02-product.md §15 | P1 | 待修复 |
| 16 | 🟢 低 | 多处 [Obsolete] 双轨方法/路由未明确下线时间点 | 02-product.md §16 | P2 | 待修复 |
| 17 | 🟢 低 | SearchController 绕过 CQRS QueryHandler 直接调用 SearchService | 02-product.md §17 | P2 | 待修复 |
| 18 | 🟢 低 | ProductReadModelAccessor 不返回 SKU 列表 — 读侧 vs 写侧信息丢失 | 02-product.md §18 | P2 | 待修复 |
| 19 | 🟢 低 | ToPriceChangeRecordDto 字段映射不完整 | 02-product.md §19 | P2 | 待修复 |
| 20 | 🟢 低 | SKU 表 ix_skus_sku_code 是非唯一索引（与高风险 #2 关联） | 02-product.md §20 | P2 | 与 #2 合并修复 |

---

## P0 详细修复计划（TDD bite-sized 格式，5 步：测试→验证失败→实现→验证通过→提交）

### P0-T1：ProductUpdatedDomainEvent 已注册翻译但 SPU 聚合永不抛出（审计 #1）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L18-L34]
**代码位置**：
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/EventBus/ProductIntegrationEventMapper.cs#L27-L29]（mapper 注册了翻译器）
- [file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs#L262-L324]（UpdateInfo/UpdateSpecs/AddSku 均未抛出该事件）
- [file:///workspace/src/Services/Product/Leno.Product.Domain/Events/ProductUpdatedDomainEvent.cs#L1-L32]（事件类已定义但成为孤儿）

**根因**：`ProductIntegrationEventMapper` 第 27-29 行注册了 `ProductUpdatedDomainEvent → ProductUpdatedEvent` 翻译器，但 SPU 聚合的 `UpdateInfo`（262-288）、`UpdateSpecs`（293-298）、`AddSku`（303-324）三个变更方法均未调用 `AddDomainEvent(new ProductUpdatedDomainEvent(...))`。

---

#### 步骤 1：测试

在 `Leno.Product.Domain.Tests/SPUTests.cs` 中追加测试，验证三个变更方法发布 `ProductUpdatedDomainEvent`。

```csharp
// 文件：src/Services/Product/Leno.Product.Domain.Tests/SPUTests.cs
// 在 SPUTests 类内追加以下测试方法

using Leno.Product.Domain.Events;

[Fact]
public void UpdateInfo_Should_Publish_ProductUpdatedDomainEvent()
{
    // Arrange
    var spu = CreateDefaultSpu();
    var newImages = new[] { ProductImage.Create("https://cdn.example.com/new.png", 0, true) };

    // Act
    spu.UpdateInfo(
        title: "更新后的标题",
        mainImageUrl: "https://cdn.example.com/new.png",
        categoryId: TestCategoryId,
        subtitle: "新副标题",
        brandId: null,
        images: newImages);

    // Assert
    var domainEvent = spu.GetDomainEvents().OfType<ProductUpdatedDomainEvent>().SingleOrDefault();
    Assert.NotNull(domainEvent);
    Assert.Equal(spu.Id, domainEvent!.ProductId);
    Assert.Equal(spu.ShopId, domainEvent.SellerId);
    Assert.Equal("更新后的标题", domainEvent.Title);
    Assert.Equal("https://cdn.example.com/new.png", domainEvent.MainImageUrl);
}

[Fact]
public void UpdateSpecs_Should_Publish_ProductUpdatedDomainEvent()
{
    // Arrange
    var spu = CreateDefaultSpu();
    var newSpecs = new[] { "颜色", "尺码" };

    // Act
    spu.UpdateSpecs(newSpecs);

    // Assert
    var domainEvent = spu.GetDomainEvents().OfType<ProductUpdatedDomainEvent>().SingleOrDefault();
    Assert.NotNull(domainEvent);
    Assert.Equal(spu.Id, domainEvent!.ProductId);
    Assert.Equal(spu.ShopId, domainEvent.SellerId);
}

[Fact]
public void AddSku_Should_Publish_ProductUpdatedDomainEvent()
{
    // Arrange
    var spu = CreateDefaultSpu();
    var sku = CreateDefaultSku(spu.Id);

    // Act
    spu.AddSku(sku);

    // Assert
    var domainEvent = spu.GetDomainEvents().OfType<ProductUpdatedDomainEvent>().SingleOrDefault();
    Assert.NotNull(domainEvent);
    Assert.Equal(spu.Id, domainEvent!.ProductId);
    Assert.Equal(spu.ShopId, domainEvent.SellerId);
}
```

> 注：`CreateDefaultSpu()`、`CreateDefaultSku()` 为测试辅助方法，若 SPUTests.cs 中已有则复用，否则按既有模式补充。`GetDomainEvents()` 为 `AggregateRoot` 基类提供的领域事件集合访问方法。

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj \
  --filter "FullyQualifiedName~UpdateInfo_Should_Publish_ProductUpdatedDomainEvent|FullyQualifiedName~UpdateSpecs_Should_Publish_ProductUpdatedDomainEvent|FullyQualifiedName~AddSku_Should_Publish_ProductUpdatedDomainEvent"
```

预期：3 个测试全部失败，断言 `Assert.NotNull(domainEvent)` 失败，因为当前 `UpdateInfo`/`UpdateSpecs`/`AddSku` 均未发布 `ProductUpdatedDomainEvent`。

#### 步骤 3：实现

修改 `SPU.cs`，在三个变更方法末尾追加 `AddDomainEvent` 调用。

```csharp
// 文件：src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs
// 修改 UpdateInfo 方法（第 262-288 行），在方法末尾（return 前）追加：

    /// <summary>
    /// 更新商品基础信息（标题、副标题、主图、分类、品牌），任意非下架终态可调用。
    /// </summary>
    public void UpdateInfo(
        string title,
        string mainImageUrl,
        Guid categoryId,
        string? subtitle = null,
        Guid? brandId = null,
        IEnumerable<ProductImage>? images = null)
    {
        EnsureEditable();

        ValidateTitle(title);
        ValidateMainImageUrl(mainImageUrl);
        ValidateSubtitle(subtitle);
        if (categoryId == Guid.Empty)
        {
            throw new ProductDomainException("分类标识不可为空", "SPU_CATEGORY_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(images);

        Title = title.Trim();
        MainImageUrl = mainImageUrl.Trim();
        CategoryId = categoryId;
        Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim();
        BrandId = brandId == Guid.Empty ? null : brandId;
        Images = images.ToList();

        AddDomainEvent(new ProductUpdatedDomainEvent(Id, ShopId, Title, MainImageUrl));
    }

// 修改 UpdateSpecs 方法（第 293-298 行），在方法末尾追加：

    /// <summary>
    /// 更新规格维度名集合。
    /// </summary>
    public void UpdateSpecs(IEnumerable<string> specs)
    {
        EnsureEditable();
        ValidateSpecs(specs);
        Specs = specs.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();

        AddDomainEvent(new ProductUpdatedDomainEvent(Id, ShopId, Title, MainImageUrl));
    }

// 修改 AddSku 方法（第 303-324 行），在 `_skus.Add(sku);` 之后追加：

    /// <summary>
    /// 新增 SKU，校验 SkuCode 与规格组合在同 SPU 下唯一。
    /// </summary>
    public void AddSku(SKU sku)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(sku);

        if (_skus.Count >= MaxSkuCount)
        {
            throw new ProductDomainException($"SKU 数量不可超过 {MaxSkuCount}", "SPU_SKU_LIMIT");
        }

        if (_skus.Any(s => string.Equals(s.SkuCode, sku.SkuCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProductDomainException($"SKU 编码已存在: {sku.SkuCode}", "SPU_SKU_CODE_DUPLICATE");
        }

        if (_skus.Any(s => s.SpecAttributes.Equals(sku.SpecAttributes)))
        {
            throw new ProductDomainException("SKU 规格组合已存在", "SPU_SKU_SPEC_DUPLICATE");
        }

        _skus.Add(sku);

        AddDomainEvent(new ProductUpdatedDomainEvent(Id, ShopId, Title, MainImageUrl));
    }
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj \
  --filter "FullyQualifiedName~UpdateInfo_Should_Publish_ProductUpdatedDomainEvent|FullyQualifiedName~UpdateSpecs_Should_Publish_ProductUpdatedDomainEvent|FullyQualifiedName~AddSku_Should_Publish_ProductUpdatedDomainEvent"
```

预期：3 个测试全部通过。

同时运行全量测试确保无回归：

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj
```

#### 步骤 5：提交

```bash
git add src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs \
        src/Services/Product/Leno.Product.Domain.Tests/SPUTests.cs
git commit -m "fix(product): SPU 聚合 UpdateInfo/UpdateSpecs/AddSku 发布 ProductUpdatedDomainEvent

审计 #1：ProductUpdatedDomainEvent 已在 mapper 注册翻译为 ProductUpdatedEvent，
但 SPU 三个变更方法均未调用 AddDomainEvent，导致购物车域展示快照与搜索域 ES
读模型永不刷新。在三个方法末尾追加 AddDomainEvent 调用，事件携带 ProductId/
SellerId/Title/MainImageUrl 字段。"
```

---

### P0-T2：ProductUniquenessChecker TOCTOU 竞态 + DB 缺唯一约束（审计 #2 + #20）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L36-L54]
**代码位置**：
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Services/ProductUniquenessChecker.cs#L20-L57]（check-then-act 竞态）
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Migrations/20260717174853_InitialCreate.cs#L192-L210]（`ix_skus_sku_code` 与 `ix_spus_shop_id` 均非唯一索引）
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Migrations/ProductDbContextModelSnapshot.cs#L326-L327]（ModelSnapshot 确认非唯一）

**根因**：
1. `IsSkuCodeUniqueAsync` 与 `IsTitleUniqueInShopAsync` 是 check-then-act 模式，并发请求可同时通过检查然后双双 Insert。
2. 迁移文件 `ix_skus_sku_code`（192-195）是非唯一索引，无 `(shop_id, title)` 复合唯一索引。

---

#### 步骤 1：测试

在 `Leno.Product.Infrastructure.Tests/ProductUniquenessCheckerTests.cs` 中追加并发竞态测试，并在 `SPUConfiguration` 与 `SKUConfiguration` 中验证唯一索引配置。

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure.Tests/ProductUniquenessCheckerTests.cs
// 在 ProductUniquenessCheckerTests 类内追加以下测试方法

[Fact]
public async Task IsSkuCodeUniqueAsync_Concurrent_With_DbUniqueConstraint_Should_Prevent_Duplicate()
{
    // Arrange：使用 InMemoryDbContext 或 TestContainers SqlServer 验证唯一约束
    // 此测试验证应用层捕获 DbUpdateException 并转换为领域异常
    var options = new DbContextOptionsBuilder<ProductDbContext>()
        .UseInMemoryDatabase(databaseName: "unique_test_" + Guid.NewGuid())
        .Options;
    await using var context = new ProductDbContext(options);
    // InMemory 不支持唯一索引，此测试仅验证 Checker 逻辑；
    // 唯一约束的集成测试需用 SqlServer TestContainer，此处验证应用层异常转换

    var checker = new ProductUniquenessChecker(context);
    var spu = SPU.Create(
        Guid.NewGuid(), TestShopId, TestSellerId, "测试商品",
        "https://cdn.example.com/img.png", TestCategoryId);
    var sku = SKU.Create(
        Guid.NewGuid(), spu.Id, "SKU-001",
        Money.Create(10m, "CNY"), 100,
        SkuSpec.Create(new[] { SpecAttribute.Create("颜色", "红") }));
    spu.AddSku(sku);
    context.SPUs.Add(spu);
    await context.SaveChangesAsync();

    // Act：相同 SkuCode 应返回 false（不唯一）
    var result = await checker.IsSkuCodeUniqueAsync("SKU-001", ct: CancellationToken.None);

    // Assert
    Assert.False(result);
}
```

新增 `SKUConfiguration` 唯一索引单元测试：

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure.Tests/SkuConfigurationTests.cs（新建）

using Leno.Product.Infrastructure.Configurations;
using Leno.Product.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Leno.Product.Infrastructure.Tests;

public sealed class SkuConfigurationTests
{
    [Fact]
    public void SKUConfiguration_Should_Have_Unique_Index_On_SkuCode()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(databaseName: "sku_config_test_" + Guid.NewGuid())
            .Options;
        using var context = new ProductDbContext(options);

        // Act
        var skuEntity = context.Model.FindEntityType(typeof(Leno.Product.Domain.Aggregates.SKU));
        Assert.NotNull(skuEntity);
        var skuCodeIndex = skuEntity!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "SkuCode") && i.IsUnique);

        // Assert
        Assert.NotNull(skuCodeIndex);
        Assert.True(skuCodeIndex!.IsUnique);
    }

    [Fact]
    public void SPUConfiguration_Should_Have_Unique_Composite_Index_On_ShopId_Title()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(databaseName: "spu_config_test_" + Guid.NewGuid())
            .Options;
        using var context = new ProductDbContext(options);

        // Act
        var spuEntity = context.Model.FindEntityType(typeof(Leno.Product.Domain.Aggregates.SPU));
        Assert.NotNull(spuEntity);
        var compositeIndex = spuEntity!.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == "ShopId") &&
                i.Properties.Any(p => p.Name == "Title") &&
                i.IsUnique);

        // Assert
        Assert.NotNull(compositeIndex);
        Assert.True(compositeIndex!.IsUnique);
    }
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~SKUConfiguration_Should_Have_Unique_Index_On_SkuCode|FullyQualifiedName~SPUConfiguration_Should_Have_Unique_Composite_Index_On_ShopId_Title"
```

预期：2 个测试失败，因为当前 `SKUConfiguration` 与 `SPUConfiguration` 未配置唯一索引。

#### 步骤 3：实现

**3a. 修改 `SKUConfiguration.cs`，将 SkuCode 索引改为唯一**：

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure/Configurations/SKUConfiguration.cs
// 找到 HasIndex 行，改为唯一索引

using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// SKU 实体 EF Core 映射配置。
/// </summary>
public sealed class SKUConfiguration : IEntityTypeConfiguration<SKU>
{
    public void Configure(EntityTypeBuilder<SKU> builder)
    {
        builder.ToTable("skus");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasColumnName("id");
        builder.Property(k => k.SpuId).HasColumnName("spu_id");
        builder.Property(k => k.SkuCode).HasColumnName("sku_code").HasMaxLength(64).IsRequired();
        builder.Property(k => k.Price).HasColumnName("price").HasPrecision(18, 2);
        builder.Property(k => k.StockQty).HasColumnName("stock_qty");
        builder.Property(k => k.Status).HasColumnName("status");
        builder.Property(k => k.ImageUrl).HasColumnName("image_url").HasMaxLength(512);
        builder.Property(k => k.SpecAttributes).HasColumnName("spec_attributes").HasConversion(
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Type?)null),
            v => Leno.Product.Domain.ValueObjects.SkuSpec.FromJson(v))
            .HasMaxLength(2000);

        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at");

        // SkuCode 全局唯一索引（修复审计 #2：原为非唯一索引）
        builder.HasIndex(k => k.SkuCode)
            .HasDatabaseName("ix_skus_sku_code")
            .IsUnique();

        builder.HasIndex(k => k.SpuId)
            .HasDatabaseName("ix_skus_spu_id");
    }
}
```

> 注：以上配置内容需与既有 `SKUConfiguration.cs` 实际内容对齐，仅将 `HasIndex(k => k.SkuCode)` 追加 `.IsUnique()`。如既有文件结构与上述不完全一致，保留既有属性映射，仅修改索引行。

**3b. 修改 `SPUConfiguration.cs`，新增 (ShopId, Title) 复合唯一索引**：

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure/Configurations/SPUConfiguration.cs
// 在现有索引配置后追加复合唯一索引

// 在 builder.HasIndex(s => s.ShopId).HasDatabaseName("ix_spus_shop_id"); 之后追加：

        // 同店铺内标题唯一复合索引（修复审计 #2）
        builder.HasIndex(s => new { s.ShopId, s.Title })
            .HasDatabaseName("ix_spus_shop_id_title")
            .IsUnique();
```

**3c. 新增 EF Core 迁移**：

```bash
dotnet ef migrations add AddUniqueIndexesOnSkuCodeAndShopTitle \
  --project src/Services/Product/Leno.Product.Infrastructure \
  --startup-project src/Services/Product/Leno.Product.Api
```

迁移文件会自动生成 `Up` 与 `Down` 方法，包含：
```csharp
migrationBuilder.CreateIndex(
    name: "ix_skus_sku_code",
    table: "skus",
    column: "sku_code",
    unique: true);

migrationBuilder.DropIndex(
    name: "ix_skus_sku_code",
    table: "skus");

// 重新创建为唯一索引
migrationBuilder.CreateIndex(
    name: "ix_skus_sku_code",
    table: "skus",
    column: "sku_code",
    unique: true);

migrationBuilder.CreateIndex(
    name: "ix_spus_shop_id_title",
    table: "spus",
    columns: new[] { "shop_id", "title" },
    unique: true);
```

**3d. 应用层捕获 `DbUpdateException` 转换为领域异常**：

```csharp
// 文件：src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs
// 修改 CreateAsync 与 AddSkuAsync，在 SaveEntitiesAsync 外层增加 DbUpdateException 捕获

// 在 CreateAsync 方法中（第 81-82 行），替换为：

        await _spuRepository.AddAsync(spu, ct);
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
            sqlEx.Number == 2601) // 唯一约束违反
        {
            throw new ProductDomainException("商品标题在同店铺内已存在或 SKU 编码已存在",
                "SPU_UNIQUE_CONSTRAINT_VIOLATION");
        }

// 在 AddSkuAsync 方法中（第 131-132 行），同样替换为：

        await _spuRepository.UpdateAsync(spu, ct);
        try
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
            sqlEx.Number == 2601)
        {
            throw new ProductDomainException("SKU 编码全局已存在",
                "SPU_SKU_CODE_GLOBAL_DUPLICATE");
        }
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~SKUConfiguration_Should_Have_Unique_Index_On_SkuCode|FullyQualifiedName~SPUConfiguration_Should_Have_Unique_Composite_Index_On_ShopId_Title"
```

预期：2 个测试通过，确认 EF Core 模型配置了唯一索引。

同时验证迁移生成正确：

```bash
dotnet ef migrations list \
  --project src/Services/Product/Leno.Product.Infrastructure \
  --startup-project src/Services/Product/Leno.Product.Api
```

确认 `AddUniqueIndexesOnSkuCodeAndShopTitle` 迁移已生成。

#### 步骤 5：提交

```bash
git add src/Services/Product/Leno.Product.Infrastructure/Configurations/SKUConfiguration.cs \
        src/Services/Product/Leno.Product.Infrastructure/Configurations/SPUConfiguration.cs \
        src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs \
        src/Services/Product/Leno.Product.Infrastructure.Tests/SkuConfigurationTests.cs \
        src/Services/Product/Leno.Product.Infrastructure/Migrations/*AddUniqueIndexes*
git commit -m "fix(product): SKU 编码全局唯一索引 + 同店铺标题复合唯一索引

审计 #2/#20：ix_skus_sku_code 原为非唯一索引，无 (shop_id, title) 复合唯一
索引。TOCTOU 竞态下并发请求可同时通过唯一性检查然后双双 Insert，导致 SKU
编码与店铺内标题重复。将 ix_skus_sku_code 改为唯一索引，新增
ix_spus_shop_id_title 复合唯一索引，应用层捕获 DbUpdateException(2601)
转换为友好领域异常。"
```

---

### P0-T3：StockBaseline.Replenish 发布事件时 ProductId=Guid.Empty（审计 #3）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L56-L71]
**代码位置**：
- [file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs#L76]（`Guid.Empty` 传第三参数）
- [file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs#L454]（正确传 `Id`）
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Consumers/StockAdjustedEventConsumer.cs#L37]（消费侧用 `integrationEvent.ProductId` 反查 SPU）

**根因**：`StockBaseline` 聚合不持有 `ProductId` 字段，`Replenish` 发布事件时第三参数 `productId` 传 `Guid.Empty`。下游 `StockAdjustedEventConsumer` 用 `Guid.Empty` 调 `GetByIdAsync` 永远返回 null，ES 读模型价格区间永不因补货更新。

---

#### 步骤 1：测试

在 `Leno.Product.Domain.Tests/StockBaselineTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Product/Leno.Product.Domain.Tests/StockBaselineTests.cs
// 在 StockBaselineTests 类内追加以下测试方法

using Leno.Product.Domain.Events;

[Fact]
public void Replenish_Should_Publish_StockAdjustedDomainEvent_With_Real_ProductId()
{
    // Arrange
    var productId = Guid.NewGuid();
    var skuId = Guid.NewGuid();
    var baseline = StockBaseline.Create(Guid.NewGuid(), skuId, 100, productId);

    // Act
    baseline.Replenish(50);

    // Assert
    var domainEvent = baseline.GetDomainEvents().OfType<StockAdjustedDomainEvent>().SingleOrDefault();
    Assert.NotNull(domainEvent);
    Assert.Equal(productId, domainEvent!.ProductId);
    Assert.NotEqual(Guid.Empty, domainEvent.ProductId);
    Assert.Equal(150, domainEvent.AvailableQty);
    Assert.Equal(50, domainEvent.Delta);
}

[Fact]
public void Create_Should_Require_ProductId_Not_Empty()
{
    // Arrange & Act & Assert
    Assert.Throws<ProductDomainException>(() =>
        StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.Empty));
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Replenish_Should_Publish_StockAdjustedDomainEvent_With_Real_ProductId|FullyQualifiedName~Create_Should_Require_ProductId_Not_Empty"
```

预期：
- `Replenish_Should_Publish_StockAdjustedDomainEvent_With_Real_ProductId` 编译失败（`StockBaseline.Create` 当前不接受 `productId` 参数）
- `Create_Should_Require_ProductId_Not_Empty` 编译失败（同上）

#### 步骤 3：实现

**3a. 修改 `StockBaseline.cs`，增加 `ProductId` 字段**：

```csharp
// 文件：src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs
// 完整替换为以下内容：

using Leno.Product.Domain.Events;
using Leno.Product.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Aggregates;

/// <summary>
/// SKU 库存基线聚合根，权威持有 SKU 的可用、预占与扣减库存。
/// 高频预占由订单域在 Redis 完成，本聚合通过消费订单域库存事件同步基线（最终一致）。
/// 卖家补货/盘点修正直接操作本聚合并发布 <see cref="StockAdjustedDomainEvent"/>。
/// </summary>
public sealed class StockBaseline : AggregateRoot
{
    /// <summary>所属 SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>所属商品（SPU）标识，用于发布库存调整事件时填充 ProductId。</summary>
    public Guid ProductId { get; private set; }

    /// <summary>可用库存（物理在库，可被预占）。</summary>
    public int AvailableQty { get; private set; }

    /// <summary>预占库存（已被未支付订单锁定，待支付扣减或取消释放）。</summary>
    public int ReservedQty { get; private set; }

    /// <summary>已扣减库存（已支付发货，永久移出可用）。</summary>
    public int DeductedQty { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private StockBaseline() { }

    private StockBaseline(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建库存基线，初始预占与扣减均为 0。
    /// </summary>
    /// <param name="baselineId">基线标识，由应用层生成。</param>
    /// <param name="skuId">所属 SKU 标识。</param>
    /// <param name="initialQty">初始可用库存，须 ≥ 0。</param>
    /// <param name="productId">所属商品（SPU）标识，须非空。</param>
    public static StockBaseline Create(Guid baselineId, Guid skuId, int initialQty, Guid productId)
    {
        if (baselineId == Guid.Empty)
        {
            throw new ProductDomainException("库存基线标识不可为空", "STOCK_BASELINE_ID_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new ProductDomainException("SKU 标识不可为空", "STOCK_SKU_EMPTY");
        }

        if (productId == Guid.Empty)
        {
            throw new ProductDomainException("商品标识不可为空", "STOCK_PRODUCT_EMPTY");
        }

        if (initialQty < 0)
        {
            throw new ProductDomainException("初始库存不可为负", "STOCK_INITIAL_NEGATIVE");
        }

        return new StockBaseline(baselineId)
        {
            SkuId = skuId,
            ProductId = productId,
            AvailableQty = initialQty,
            ReservedQty = 0,
            DeductedQty = 0
        };
    }

    /// <summary>
    /// 补货，可用库存上调并发布 <see cref="StockAdjustedDomainEvent"/> 通知订单域同步。
    /// </summary>
    /// <param name="qty">补货数量，须 > 0。</param>
    public void Replenish(int qty)
    {
        if (qty <= 0)
        {
            throw new ProductDomainException("补货数量须大于 0", "STOCK_REPLENISH_INVALID");
        }

        AvailableQty += qty;

        AddDomainEvent(new StockAdjustedDomainEvent(Id, SkuId, ProductId, AvailableQty, qty, DateTime.UtcNow));
    }

    /// <summary>
    /// 同步预占库存（消费订单域预占事件，将订单域 Redis 权威值镜像到基线）。
    /// </summary>
    /// <param name="reservedQty">订单域当前预占总量，须 ≥ 0 且 ≤ 可用库存。</param>
    public void SyncReserved(int reservedQty)
    {
        if (reservedQty < 0)
        {
            throw new ProductDomainException("预占库存不可为负", "STOCK_RESERVED_NEGATIVE");
        }

        if (reservedQty > AvailableQty)
        {
            throw new ProductDomainException("预占库存不可超过可用库存", "STOCK_RESERVED_EXCEED");
        }

        ReservedQty = reservedQty;
    }

    /// <summary>
    /// 同步扣减库存（消费订单域支付事件，将预占转为扣减并移出可用）。
    /// </summary>
    /// <param name="deductedQty">订单域当前累计扣减总量，须 ≥ 0。</param>
    public void SyncDeducted(int deductedQty)
    {
        if (deductedQty < 0)
        {
            throw new ProductDomainException("扣减库存不可为负", "STOCK_DEDUCTED_NEGATIVE");
        }

        var delta = deductedQty - DeductedQty;
        if (delta > 0)
        {
            AvailableQty -= delta;
            ReservedQty = Math.Max(0, ReservedQty - delta);
        }

        DeductedQty = deductedQty;

        if (AvailableQty < 0)
        {
            throw new ProductDomainException("可用库存不可为负", "STOCK_AVAILABLE_NEGATIVE");
        }
    }

    /// <summary>
    /// 同步释放库存（消费订单域取消事件，释放对应预占）。
    /// </summary>
    /// <param name="releasedQty">本次释放数量，须 ≥ 0 且 ≤ 当前预占。</param>
    public void SyncReleased(int releasedQty)
    {
        if (releasedQty < 0)
        {
            throw new ProductDomainException("释放数量不可为负", "STOCK_RELEASED_NEGATIVE");
        }

        if (releasedQty > ReservedQty)
        {
            throw new ProductDomainException("释放数量不可超过预占库存", "STOCK_RELEASED_EXCEED");
        }

        ReservedQty -= releasedQty;
    }
}
```

**3b. 修改 `StockBaselineConfiguration.cs`，映射新字段 `ProductId`**：

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure/Configurations/StockBaselineConfiguration.cs
// 在 builder.Property(b => b.SkuId) 之后追加：

        builder.Property(b => b.ProductId).HasColumnName("product_id");

// 在现有索引后追加 ProductId 索引（非唯一，用于按商品查询库存）：

        builder.HasIndex(b => b.ProductId)
            .HasDatabaseName("ix_stock_baselines_product_id");
```

**3c. 修改调用 `StockBaseline.Create` 的应用层，传入真实 ProductId**：

```bash
# 搜索所有调用 StockBaseline.Create 的位置
grep -rn "StockBaseline.Create" src/Services/Product/
```

在 `InventoryAppService.cs`（或实际调用处）中，将 `StockBaseline.Create(baselineId, skuId, initialQty)` 改为 `StockBaseline.Create(baselineId, skuId, initialQty, productId)`，`productId` 通过 `ISPURepository.GetBySkuIdAsync` 反查获得。

**3d. 新增 EF Core 迁移**：

```bash
dotnet ef migrations add AddProductIdToStockBaseline \
  --project src/Services/Product/Leno.Product.Infrastructure \
  --startup-project src/Services/Product/Leno.Product.Api
```

**3e. 更新 `StockBaselineTests.cs` 中既有测试**，所有调用 `StockBaseline.Create` 的位置增加 `productId` 参数。

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Replenish_Should_Publish_StockAdjustedDomainEvent_With_Real_ProductId|FullyQualifiedName~Create_Should_Require_ProductId_Not_Empty"
```

预期：2 个测试通过。

同时运行全量测试确保无回归：

```bash
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj
dotnet build src/Services/Product/Leno.Product.Infrastructure/Leno.Product.Infrastructure.csproj
```

#### 步骤 5：提交

```bash
git add src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs \
        src/Services/Product/Leno.Product.Infrastructure/Configurations/StockBaselineConfiguration.cs \
        src/Services/Product/Leno.Product.Infrastructure/Migrations/*AddProductIdToStockBaseline* \
        src/Services/Product/Leno.Product.Domain.Tests/StockBaselineTests.cs \
        src/Services/Product/Leno.Product.Application/Services/InventoryAppService.cs
git commit -m "fix(product): StockBaseline 增加 ProductId 字段，Replenish 发布事件传入真实 ProductId

审计 #3：StockBaseline.Replenish 发布 StockAdjustedDomainEvent 时第三参数
ProductId 传 Guid.Empty，导致 StockAdjustedEventConsumer 用 Guid.Empty 反查
SPU 永远返回 null，ES 读模型价格区间永不因补货更新。StockBaseline 聚合增加
ProductId 字段，Create 工厂方法要求传入，Replenish 发布事件时填充真实值。"
```

---

### P0-T4：EfCoreSPURepository.UpdateAsync 仅 Attach 不标记 Modified（审计 #4）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L73-L95]
**代码位置**：
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs#L105-L114]（仅 Attach）
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Consumers/ShopEventConsumer.cs#L52-L57]（QueryAsync AsNoTracking 加载后变更状态）
- [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs#L55]（QueryAsync 使用 AsNoTracking）

**根因**：`UpdateAsync` 仅在实体 `Detached` 时调用 `Attach`，Attach 后当前值被快照为原始值，ChangeTracker 检测不到差异。在 `ShopEventConsumer` 流程下（通过 `QueryAsync` 的 `AsNoTracking` 加载），`SaveEntitiesAsync` 不发出 UPDATE 语句，SPU 状态变更不持久化。

---

#### 步骤 1：测试

在 `Leno.Product.Infrastructure.Tests/ShopEventConsumerTests.cs` 中追加测试，验证 AsNoTracking 加载的 SPU 变更后能持久化。

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure.Tests/ShopEventConsumerTests.cs
// 在 ShopEventConsumerTests 类内追加以下测试方法

[Fact]
public async Task UpdateAsync_With_AsNoTracking_Entity_Should_Mark_Modified_And_Persist()
{
    // Arrange：使用 InMemory 或 SQLite 验证变更跟踪行为
    var options = new DbContextOptionsBuilder<ProductDbContext>()
        .UseInMemoryDatabase(databaseName: "update_test_" + Guid.NewGuid())
        .Options;
    await using var context = new ProductDbContext(options);
    var repo = new EfCoreSPURepository(context);

    var spu = SPU.Create(
        Guid.NewGuid(), TestShopId, TestSellerId, "测试商品",
        "https://cdn.example.com/img.png", TestCategoryId);
    var sku = SKU.Create(
        Guid.NewGuid(), spu.Id, "SKU-001",
        Money.Create(10m, "CNY"), 100,
        SkuSpec.Create(new[] { SpecAttribute.Create("颜色", "红") }));
    spu.AddSku(sku);
    context.SPUs.Add(spu);
    await context.SaveChangesAsync();
    context.ChangeTracker.Clear(); // 模拟 AsNoTracking 加载后的 Detached 状态

    // Act：模拟 ShopEventConsumer 流程：AsNoTracking 加载 → 变更 → UpdateAsync
    var (items, _) = await repo.QueryAsync(shopId: TestShopId, ct: CancellationToken.None);
    var loaded = items.Single();
    loaded.SuspendByShop(); // 变更状态

    await repo.UpdateAsync(loaded, CancellationToken.None);
    await context.SaveChangesAsync();

    // Assert：重新查询验证状态已持久化
    context.ChangeTracker.Clear();
    var persisted = await context.SPUs.AsNoTracking().FirstAsync(s => s.Id == spu.Id);
    Assert.Equal(Leno.Product.Domain.ValueObjects.ProductStatus.ShopSuspended, persisted.Status);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~UpdateAsync_With_AsNoTracking_Entity_Should_Mark_Modified_And_Persist"
```

预期：测试失败，`Assert.Equal` 失败，因为 `persisted.Status` 仍为 `OnSale`（变更未持久化）。

> 注：InMemory provider 的变更跟踪行为与 SQLite/SqlServer 不同（InMemory 对 Detached 实体的 Attach 不会自动标记 Modified）。此测试在 InMemory 下可能需要调整。若 InMemory 无法复现，改用 SQLite in-memory：
> ```csharp
> var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
> connection.Open();
> var options = new DbContextOptionsBuilder<ProductDbContext>()
>     .UseSqlite(connection)
>     .Options;
> ```

#### 步骤 3：实现

修改 `EfCoreSPURepository.UpdateAsync`，强制标记 Modified。

```csharp
// 文件：src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs
// 替换 UpdateAsync 方法（第 105-114 行）为：

    /// <inheritdoc />
    public Task UpdateAsync(SPU aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _context.SPUs.Update(aggregate);
        return Task.CompletedTask;
    }
```

> 注：`DbContext.Update(entity)` 会将实体及所有关联实体标记为 `Modified`，确保 AsNoTracking 加载的实体变更能被持久化。此模式与 Generic Repository 通用模式对齐。对乐观并发（rowversion）的影响：若 SPUConfiguration 配置了 `IsConcurrencyToken()`，Update 会包含 rowversion 列在 WHERE 子句中，并发冲突时抛 `DbUpdateConcurrencyException`，需调用方处理重试。

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~UpdateAsync_With_AsNoTracking_Entity_Should_Mark_Modified_And_Persist"
```

预期：测试通过，`persisted.Status` 为 `ShopSuspended`。

同时运行全量测试确保无回归：

```bash
dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj
dotnet test src/Services/Product/Leno.Product.Domain.Tests/Leno.Product.Domain.Tests.csproj
```

#### 步骤 5：提交

```bash
git add src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs \
        src/Services/Product/Leno.Product.Infrastructure.Tests/ShopEventConsumerTests.cs
git commit -m "fix(product): EfCoreSPURepository.UpdateAsync 改用 DbContext.Update 强制标记 Modified

审计 #4：UpdateAsync 仅在 Detached 时 Attach，Attach 后当前值被快照为原始值，
ChangeTracker 检测不到差异。ShopEventConsumer 通过 QueryAsync(AsNoTracking)
加载 SPU 后调用 SuspendByShop/ResumeByShop/TakeDownForShopClosure 变更状态，
SaveEntitiesAsync 不发出 UPDATE 语句，SPU 状态变更不持久化，店铺暂停/关闭
语义失效。改为 _context.SPUs.Update(aggregate) 强制标记所有属性为 Modified。"
```

---

### P0-T5：ProductGrpcService 使用 Guid.GetHashCode() 做 int64 映射（审计 #5）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L97-L114]
**代码位置**：
- [file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L142]（`SellerId = (long)dto.SellerId.GetHashCode()`）
- [file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L150]（`SkuId = (long)sku.SkuId.GetHashCode()`）
- [file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L167-L168]（`SkuId`/`SpuId` GetHashCode）
- [file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L174]（`SellerId` GetHashCode）

**根因**：`(long)dto.SkuId.GetHashCode()` 将 Guid 映射为 int64，`GetHashCode()` 返回 32 位 int，不同 Guid 哈希碰撞概率为 1/2^32，百万级 SKU 规模下必然碰撞。代码已新增 `SkuIdStr`/`SpuIdStr`/`SellerIdStr` string 字段，但 int64 字段仍使用 `GetHashCode()` 填充，碰撞风险未消除。

---

#### 步骤 1：测试

在 `Leno.Product.Api.Tests/ProductGrpcServiceTests.cs`（或新建）中追加测试，验证 int64 字段使用稳定算法（Guid 前 8 字节）而非 GetHashCode。

```csharp
// 文件：src/Services/Product/Leno.Product.Api.Tests/ProductGrpcServiceTests.cs
// 在 ProductGrpcServiceTests 类内追加以下测试方法

using Leno.Product.Application;
using Leno.SharedContracts.Grpc.Product.V1;

[Fact]
public async Task GetSkuInfo_Int64_Field_Should_Use_Stable_Mapping_Not_GetHashCode()
{
    // Arrange
    var skuId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    var expectedInt64 = BitConverter.ToInt64(skuId.ToByteArray(), 0);

    var dto = new SkuInfoResultDto
    {
        SkuId = skuId,
        SpuId = Guid.NewGuid(),
        Price = 19.99m,
        Currency = "CNY",
        Stock = 100,
        Status = "active",
        Title = "测试 SKU",
        MainImageUrl = "https://cdn.example.com/sku.png",
        SellerId = Guid.NewGuid(),
        ShopId = Guid.NewGuid()
    };

    var mockQueryService = new Mock<IProductInternalQueryService>();
    mockQueryService
        .Setup(s => s.GetSkuInfoAsync(skuId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(dto);

    var logger = new Mock<ILogger<ProductGrpcService>>();
    var service = new ProductGrpcService(mockQueryService.Object, logger.Object);

    var request = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
    var context = new Mock<Grpc.Core.ServerCallContext>().Object;

    // Act
    var result = await service.GetSkuInfo(request, context);

    // Assert：int64 字段应使用稳定算法（前 8 字节），而非 GetHashCode
    Assert.Equal(expectedInt64, result.SkuId);
    Assert.NotEqual((long)skuId.GetHashCode(), result.SkuId);
    Assert.Equal(skuId.ToString(), result.SkuIdStr);
}

[Fact]
public async Task GetSkuInfo_Int64_Field_Should_Be_Deterministic_For_Same_Guid()
{
    // Arrange
    var skuId = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
    var expectedInt64 = BitConverter.ToInt64(skuId.ToByteArray(), 0);

    var dto1 = new SkuInfoResultDto
    {
        SkuId = skuId, SpuId = Guid.NewGuid(), Price = 10m, Currency = "CNY",
        Stock = 50, Status = "active", Title = "SKU1",
        MainImageUrl = "https://cdn.example.com/1.png",
        SellerId = Guid.NewGuid(), ShopId = Guid.NewGuid()
    };

    var mockQueryService = new Mock<IProductInternalQueryService>();
    mockQueryService
        .Setup(s => s.GetSkuInfoAsync(skuId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(dto1);

    var logger = new Mock<ILogger<ProductGrpcService>>();
    var service = new ProductGrpcService(mockQueryService.Object, logger.Object);

    // Act：两次调用相同 Guid
    var request1 = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
    var context = new Mock<Grpc.Core.ServerCallContext>().Object;
    var result1 = await service.GetSkuInfo(request1, context);

    var request2 = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
    var result2 = await service.GetSkuInfo(request2, context);

    // Assert：相同 Guid 产生相同 int64（确定性）
    Assert.Equal(result1.SkuId, result2.SkuId);
    Assert.Equal(expectedInt64, result1.SkuId);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Product/Leno.Product.Api.Tests/Leno.Product.Api.Tests.csproj \
  --filter "FullyQualifiedName~GetSkuInfo_Int64_Field_Should_Use_Stable_Mapping_Not_GetHashCode|FullyQualifiedName~GetSkuInfo_Int64_Field_Should_Be_Deterministic_For_Same_Guid"
```

预期：2 个测试失败，因为当前 `MapToProto` 使用 `(long)dto.SkuId.GetHashCode()`，`result.SkuId` 等于 GetHashCode 值而非 `BitConverter.ToInt64(skuId.ToByteArray(), 0)`。

#### 步骤 3：实现

修改 `ProductGrpcService.cs`，将所有 `(long)xxx.GetHashCode()` 替换为稳定算法 `GuidToInt64Stable`。

```csharp
// 文件：src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs
// 在文件顶部（namespace 内）追加静态辅助方法：

    /// <summary>
    /// 将 Guid 映射为 int64 的稳定算法：取 Guid 字节序列前 8 字节转 int64。
    /// 替代 GetHashCode()（32 位，碰撞率高），确保相同 Guid 始终映射到相同 int64。
    /// 注：此映射不可逆（int64 仅 8 字节，Guid 16 字节），仅用于 deprecated int64 字段的向后兼容。
    /// 新客户端应使用 XxxIdStr 字段。
    /// </summary>
    private static long GuidToInt64Stable(Guid guid)
        => BitConverter.ToInt64(guid.ToByteArray(), 0);

// 修改 GetProductDetail 方法中的 MapToProto 调用（第 142-158 行）：

        var detail = new ProductDetail
        {
            SpuId = request.SpuId,
            SpuIdStr = dto.SpuId.ToString(),
            Title = dto.Title,
            Description = dto.Description,
            // 修复审计 #5：使用稳定算法替代 GetHashCode()
            SellerId = GuidToInt64Stable(dto.SellerId),
            SellerIdStr = dto.SellerId.ToString()
        };

        foreach (var sku in dto.Skus)
        {
            detail.Skus.Add(new SkuInfo
            {
                SkuId = GuidToInt64Stable(sku.SkuId),
                SkuIdStr = sku.SkuId.ToString(),
                Title = sku.Title,
                MainImage = sku.MainImageUrl,
                PriceCents = (long)Math.Round(sku.Price * 100m, MidpointRounding.AwayFromZero),
                Currency = sku.Currency,
                Stock = sku.Stock,
                Status = sku.Status
            });
        }

// 修改 MapToProto 方法（第 164-183 行）：

    private static SkuInfo MapToProto(SkuInfoResultDto dto) => new()
    {
        // 修复审计 #5：使用稳定算法替代 GetHashCode()
        SkuId = GuidToInt64Stable(dto.SkuId),
        SpuId = GuidToInt64Stable(dto.SpuId),
        Title = dto.Title,
        MainImage = dto.MainImageUrl,
        PriceCents = (long)Math.Round(dto.Price * 100m, MidpointRounding.AwayFromZero),
        Currency = dto.Currency,
        Salable = dto.Available,
        SellerId = GuidToInt64Stable(dto.SellerId),
        Stock = dto.Stock,
        Status = dto.Status,
        ShopId = dto.ShopId?.ToString() ?? string.Empty,
        UpdatedAt = dto.UpdatedAt?.ToUnixTimeSeconds() ?? 0L,
        SkuIdStr = dto.SkuId.ToString(),
        SpuIdStr = dto.SpuId.ToString(),
        SellerIdStr = dto.SellerId.ToString()
    };
```

> 注：同时修复了审计 #12（PriceCents 截断），将 `(long)(sku.Price * 100)` 改为 `(long)Math.Round(sku.Price * 100m, MidpointRounding.AwayFromZero)`。

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Product/Leno.Product.Api.Tests/Leno.Product.Api.Tests.csproj \
  --filter "FullyQualifiedName~GetSkuInfo_Int64_Field_Should_Use_Stable_Mapping_Not_GetHashCode|FullyQualifiedName~GetSkuInfo_Int64_Field_Should_Be_Deterministic_For_Same_Guid"
```

预期：2 个测试通过。

同时确认无 `GetHashCode()` 残留：

```bash
grep -rn "GetHashCode" src/Services/Product/Leno.Product.Api/GrpcServices/
```

预期：零命中。

#### 步骤 5：提交

```bash
git add src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs \
        src/Services/Product/Leno.Product.Api.Tests/ProductGrpcServiceTests.cs
git commit -m "fix(product): gRPC int64 字段改用稳定算法替代 GetHashCode，顺带修复 PriceCents 截断

审计 #5：ProductGrpcService 使用 (long)Guid.GetHashCode() 映射 Guid→int64，
GetHashCode 返回 32 位 int，百万级 SKU 下必然碰撞。新增 GuidToInt64Stable
辅助方法，取 Guid 字节序列前 8 字节转 int64，确保相同 Guid 始终映射到相同
int64 值。新客户端应使用 XxxIdStr 字段，int64 字段仅向后兼容。
审计 #12：PriceCents 从 (long)(price*100) 截断改为 Math.Round(AwayFromZero)。"
```

---

## P1 修复清单（任务清单格式：审计位置/代码位置/根因/修复步骤/影响范围/验证方法）

### P1-T6：ProductSearchService 价格区间过滤仅校验 MinPrice（审计 #6）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L120-L133]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs#L109-L123]
- **根因**：`BuildFilters` 对 `MinPrice` 字段做 `NumberRangeQuery`，`maxPrice=150` 过滤 `MinPrice <= 150`，但商品 `MaxPrice=200` 时实际超出用户期望。应使用区间相交逻辑 `MinPrice <= maxPrice && MaxPrice >= minPrice`。
- **修复步骤**：
  1. 修改 `BuildFilters` 方法，将单一 `NumberRangeQuery(MinPrice)` 拆分为两个 range query 放入 `bool.filter`：
     - `MinPrice` range: `Gte = minPrice`（若 minPrice 有值），`Lte = maxPrice`（若 maxPrice 有值）
     - `MaxPrice` range: `Gte = minPrice`（若 minPrice 有值），`Lte = maxPrice`（若 maxPrice 有值）
  2. 或更精确：使用 `bool.must` 组合两个 range query：`MinPrice <= maxPrice` AND `MaxPrice >= minPrice`（区间相交）
  3. 补充单元测试：验证 `minPrice=100, maxPrice=150` 时，`MinPrice=50, MaxPrice=200` 的商品被排除
- **影响范围**：买家端搜索体验
- **验证方法**：单元测试验证 ES query 构建逻辑；集成测试验证搜索结果价格区间正确

### P1-T7：ProductSearchService sort 参数被静默忽略（审计 #7）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L135-L142]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs#L40]
- **根因**：第 40 行 `_ = sort;` 显式丢弃 sort 参数，前端传入 `price_asc`/`price_desc`/`sales_desc` 被静默吞掉。
- **修复步骤**：
  1. 修改 `SearchAsync` 方法签名，将 `sort` 参数传递给 `_repository.SearchAsync` 的排序回调
  2. 在 `SearchAsync` 内根据 `sort` 值构建 `SortOptions`：
     - `price_asc` → 按 `MinPrice` 升序
     - `price_desc` → 按 `MinPrice` 降序
     - `sales_desc` → 按销量字段降序（若读模型有 `SalesCount` 字段）
     - `relevance` 或空 → 默认相关性得分
  3. 无效 sort 值 log warning 并回退到 relevance
  4. 补充单元测试验证 sort 参数被正确传递
- **影响范围**：买家端搜索、运营后台
- **验证方法**：单元测试验证不同 sort 值生成正确的 ES SortOptions

### P1-T8：ProductInternalQueryService.GetSkuInfosBatchAsync N+1 查询（审计 #8）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L144-L151]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Application/Services/ProductInternalQueryService.cs#L52-L67]
- **根因**：`GetSkuInfosBatchAsync` 遍历 `skuIds`，对每个 skuId 调用 `GetSkuInfoAsync`（内部 `GetBySkuIdAsync` 触发一次 DB 查询），100 个 SKU 触发 100 次 DB round-trip。
- **修复步骤**：
  1. 在 `ISPURepository` 增加 `GetBySkuIdsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct)` 方法，单次 SQL `WHERE s.Id IN @skuIds`（Include SKU），返回 `Dictionary<SkuId, SPU>`
  2. 在 `EfCoreSPURepository` 实现该方法
  3. 修改 `GetSkuInfosBatchAsync`，调用 `GetBySkuIdsAsync` 单次批量查询，内存中匹配 SKU
  4. 补充单元测试验证批量查询只触发一次 DB 调用
- **影响范围**：所有跨域批量查询 SKU 的场景（订单、购物车）
- **验证方法**：单元测试验证批量查询返回正确结果；性能测试验证 DB 调用次数为 1

### P1-T9：SpuReviewSummaryConsumer 增量评分计算浮点漂移（审计 #9）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L153-L165]
- **代码位置**：
  - [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs#L52-L56]（增量更新公式）
  - [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs#L121-L131]（隐藏评价反推）
- **根因**：增量更新公式 `existing.Score * existing.ReviewCount + integrationEvent.Rating`，每次 `Math.Round(..., 2)` 后存回，加权累计值不等于真实总评分，千次评价后漂移 ±0.05。
- **修复步骤**：
  1. `ProductReadModel` 增加 `TotalScore` 字段（double，不 round），存储累计原始总评分
  2. `SpuReviewSubmittedSummaryConsumer` 增量更新 `TotalScore += Rating`、`ReviewCount += 1`，展示时计算 `Math.Round(TotalScore / ReviewCount, 2)`
  3. `SpuReviewHiddenSummaryConsumer` 移除时 `TotalScore -= Rating`、`ReviewCount -= 1`
  4. 新增 EF/ES 映射更新 `TotalScore` 字段
  5. 补充单元测试验证千次增量后 Score 无漂移
- **影响范围**：商品读模型评分
- **验证方法**：单元测试验证 1000 次增量后 Score 与全量重算一致

### P1-T10：SpuReviewSummaryConsumer.cs:151 TODO 占位（审计 #10）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L167-L180]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/SpuReviewSummaryConsumer.cs#L151-L154]
- **根因**：第 151-154 行 TODO 注释明确说明 `ReviewModeratedEvent` 未实现评分同步消费者，违反"零占位容忍度"规则。评价被审核驳回后商品评分读模型仍包含该评价。
- **修复步骤**：
  1. 与评价域对齐 `ReviewModeratedEvent` schema，补充 `SpuId` 与 `Rating` 字段（跨 BC 协调）
  2. 在 Product 域新建 `SpuReviewModeratedSummaryConsumer`，消费 `ReviewModeratedEvent`
  3. 根据 `Action`（Approve/Reject/Appeal）分别走 Hidden/Submitted 流程：
     - Approve：`TotalScore += Rating`、`ReviewCount += 1`
     - Reject：`TotalScore -= Rating`、`ReviewCount -= 1`
     - Appeal（恢复）：`TotalScore += Rating`、`ReviewCount += 1`
  4. 删除 TODO 注释
  5. 补充单元测试覆盖三种 Action
- **影响范围**：商品读模型、评价审核流程
- **验证方法**：单元测试验证三种 Action 的评分更新正确

### P1-T11：Money 值对象允许 amount=0 与 SKU 域 price>0 不一致（审计 #11）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L182-L191]
- **代码位置**：
  - [file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L32-L35]（仅校验 `< 0`，允许 `= 0`）
  - [file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs#L413-L416]（`AdjustPrice` 校验 `<= 0`）
- **根因**：共享内核 `Money.Create` 允许 `amount=0`，但 SPU 的 `AdjustPrice` 校验 `<= 0` 抛异常，两个 BC 对 0 元语义不一致。
- **修复步骤**：
  1. **明确语义**：Money 允许 0（语义为"免费/赠品"），各 BC 自行决定是否拒绝 0
  2. 在 `Money` 增加 `RequirePositive()` 方法，返回 `Money` 或抛异常
  3. SPU 的 `AdjustPrice` 改为调用 `newPrice.RequirePositive()` 替代手动 `<= 0` 校验
  4. SKU 的 `Create` 工厂方法同样调用 `price.RequirePositive()`
  5. 补充单元测试验证 `Money.Create(0, "CNY")` 合法但 `RequirePositive()` 抛异常
- **影响范围**：共享内核、商品域、订单域
- **验证方法**：单元测试验证 Money 允许 0、SKU 价格场景拒绝 0

### P1-T12：ProductGrpcService PriceCents 截断非四舍五入（审计 #12）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L193-L202]
- **代码位置**：
  - [file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L154]（`(long)(sku.Price * 100)`）
  - [file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L171]（`(long)(dto.Price * 100)`）
- **根因**：`(long)(sku.Price * 100)` 显式 cast 截断小数，`19.99 * 100 = 1998.999...` 截断为 1998 而非 1999。
- **修复步骤**：已在 P0-T5 中一并修复，将 `(long)(price * 100)` 改为 `(long)Math.Round(price * 100m, MidpointRounding.AwayFromZero)`
- **影响范围**：订单域金额计算
- **验证方法**：已在 P0-T5 测试中覆盖

### P1-T13：PriceHistory.Create reason 永远为 null，ChangedBy 永远为 string.Empty（审计 #13）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L204-L215]
- **代码位置**：
  - [file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L237]（`reason: null`）
  - [file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L366-L374]（`ChangedBy = string.Empty`）
  - [file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/PriceHistory.cs#L47-L85]（Create 无 changedBy 参数）
- **根因**：`PriceHistory.Create` 不接受 `changedBy` 参数；`SPUAppService.AdjustPriceAsync` 显式传 `reason: null`；`ToPriceChangeRecordDto` 硬编码 `ChangedBy = string.Empty`。
- **修复步骤**：
  1. `PriceHistory.Create` 增加 `string changedBy` 参数，校验非空
  2. `PriceHistory` 聚合增加 `ChangedBy` 属性
  3. `AdjustPriceDto` 增加 `Reason` 字段（可空，但鼓励填写）
  4. `SPUAppService.AdjustPriceAsync` 将 `dto.Reason` 与 `changedBy` 透传给 `PriceHistory.Create`
  5. `ToPriceChangeRecordDto` 返回 `history.ChangedBy` 而非 `string.Empty`
  6. 新增 EF Core 迁移增加 `changed_by` 列
  7. 补充单元测试验证审计字段正确填充
- **影响范围**：商品域价格审计、合规
- **验证方法**：单元测试验证 PriceHistory 记录的 ChangedBy 与 Reason 字段非空

### P1-T14：StockBaseline.SyncDeducted 异常在状态赋值后抛出（审计 #14）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L217-L231]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs#L102-L122]
- **根因**：`SyncDeducted` 在第 112 行 `AvailableQty -= delta`、第 116 行 `DeductedQty = deductedQty` 之后，第 118-121 行才校验 `AvailableQty < 0` 并抛异常。异常抛出前聚合状态已被修改。
- **修复步骤**：
  1. 改为先校验后赋值：
     ```csharp
     var newAvailable = AvailableQty - delta;
     if (newAvailable < 0) throw new ProductDomainException(...);
     AvailableQty = newAvailable;
     ReservedQty = Math.Max(0, ReservedQty - delta);
     DeductedQty = deductedQty;
     ```
  2. 同样模式检查 `Replenish`（当前 `AvailableQty += qty` 前已校验 `qty <= 0`，但 int 溢出场景未覆盖，增加溢出检查或保持现状）
  3. 同样模式检查 `SyncReserved`、`SyncReleased`（当前已是先校验后赋值，确认无需修改）
  4. 补充单元测试验证异常抛出后聚合状态不变
- **影响范围**：商品域库存聚合
- **验证方法**：单元测试验证 `SyncDeducted` 抛异常后 `AvailableQty`/`DeductedQty` 保持原值

### P1-T15：ProductReadModelSyncConsumer 默认 "CNY" 币种硬编码（审计 #15）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L233-L240]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs#L42]
- **根因**：第 42 行 `var currency = spu.SKUs.FirstOrDefault()?.Price.Currency ?? "CNY"`，当 SPU 无 SKU 时 fallback 到 "CNY"；多币种店铺仅取首个 SKU 币种。
- **修复步骤**：
  1. `ProductReadModel` 增加 `Currencies` 数组字段（`IReadOnlyList<string>`），列出所有 SKU 的币种集合
  2. `ProductPublishedReadModelSyncConsumer.BuildReadModelAsync` 填充 `Currencies = spu.SKUs.Select(s => s.Price.Currency).Distinct().ToList()`
  3. `Currency` 字段保留首个币种作为默认展示（向后兼容），但 `Currencies` 提供完整集合
  4. 或更严格：SPU 聚合 `Create`/`AddSku` 校验所有 SKU 同币种（聚合不变量）
  5. 补充单元测试验证多币种场景
- **影响范围**：商品读模型、跨境交易
- **验证方法**：单元测试验证多币种 SKU 的读模型 Currencies 字段完整

---

## P2 修复清单（任务清单格式，可简化）

### P2-T16：多处 [Obsolete] 双轨方法/路由未明确下线时间点（审计 #16）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L246-L255]
- **代码位置**：
  - [file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L166]（`GetByIdAsync`，2026-08-01 下线，已明确）
  - [file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L174]（`QueryProductsAsync`，2026-08-01 下线，已明确）
  - [file:///workspace/src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs#L24-L25]（"1 周后下线"，未给具体日期）
  - [file:///workspace/src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs#L41-L42]（同上）
- **修复步骤**：
  1. `InternalProductsController` 的 `[Obsolete("双路由期保留，1 周后下线")]` 改为具体日期，如 `[Obsolete("双路由期保留，2026-08-15 下线，请使用 internal/v1/... 路由")]`
  2. CI 增加 Obsolete 检测告警，`TreatWarningsAsErrors` 配置确保按计划下线
- **影响范围**：接口契约维护

### P2-T17：SearchController 绕过 CQRS QueryHandler 直接调用 SearchService（审计 #17）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L257-L264]
- **代码位置**：
  - [file:///workspace/src/Services/Product/Leno.Product.Api/Controllers/SearchController.cs#L35-L44]（直接调 `IProductSearchService`）
  - [file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L72]（`AddQueryHandlers` 已注册但未被使用）
- **修复步骤**：
  1. `SearchController` 改为注入 `IQueryHandler<ProductSearchQuery, ProductSearchResult>` 而非 `IProductSearchService`
  2. `ProductSearchQueryHandler`（已注册）作为 CQRS 读侧入口
  3. 删除 `SearchController` 对 `IProductSearchService` 的直接依赖
- **影响范围**：CQRS 职责统一

### P2-T18：ProductReadModelAccessor 不返回 SKU 列表（审计 #18）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L266-L271]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelAccessor.cs#L32-L50]
- **根因**：`ProductDetailResult` 仅含 MinPrice/MaxPrice，无 SKU 列表；买家端商品详情页走 CQRS 读侧无法展示 SKU 选择器。
- **修复步骤**：
  1. `ProductReadModel` 增加 `Skus` 嵌套文档列表（`IReadOnlyList<SkuReadModel>`）
  2. `ProductPublishedReadModelSyncConsumer` 投影时填充 Skus 列表
  3. `ProductDetailResult` 增加 `Skus` 字段
  4. `ProductReadModelAccessor.ToResult` 映射 Skus
- **影响范围**：买家端商品详情页

### P2-T19：ToPriceChangeRecordDto 字段映射不完整（审计 #19）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L273-L278]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Application/Services/SPUAppService.cs#L366-L374]
- **根因**：`SkuId` 转为 string，但 `OldPrice`/`NewPrice` 直接返回 decimal；与 API 响应中其他 DTO 风格不一致。
- **修复步骤**：
  1. `PriceChangeRecordDto.SkuId` 保留 Guid 类型而非 string
  2. 或统一所有 DTO 的 ID 字段为 string（与 P2-T18 一并评审）
  3. 此项与 P1-T13（ChangedBy 修复）一并实施
- **影响范围**：DTO 契约一致性

### P2-T20：SKU 表 ix_skus_sku_code 是非唯一索引（审计 #20）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md#L280-L285]
- **代码位置**：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/Migrations/20260717174853_InitialCreate.cs#L192-L195]
- **根因**：与 #2 关联，单独看是索引唯一性设计缺陷。
- **修复步骤**：已在 P0-T2 中合并修复（将 `ix_skus_sku_code` 改为唯一索引）
- **影响范围**：与 #2 相同

---

## 已修复项（标注 [ALREADY-FIXED] 或 [VERIFIED-NOT-REPRODUCIBLE]）

| # | 问题标题 | 状态 | 说明 |
|---|---------|------|------|
| p0a-T4 | GetSkuStock/GetProductDetail 内部查询接口与 gRPC 实现占位 | [ALREADY-FIXED] | 来自 `2026-07-20-p0a-placeholder-implementation.md` Task 4。验证当前代码：`ProductInternalQueryService.GetSkuStockAsync`（[file:///workspace/src/Services/Product/Leno.Product.Application/Services/ProductInternalQueryService.cs#L70-L85]）已完整实现，从 `IStockBaselineRepository.GetBySkuIdAsync` 读取库存基线并映射为 `SkuStockResultDto`；`GetSpuDetailAsync`（[file:///workspace/src/Services/Product/Leno.Product.Application/Services/ProductInternalQueryService.cs#L88-L123]）已完整实现，从 `ISPURepository.GetByIdAsync` 读取 SPU 聚合并映射为 `SpuDetailResultDto` 含 SKU 列表；`ProductGrpcService.GetSkuStock`（[file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L77-L109]）与 `GetProductDetail`（[file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L111-L162]）gRPC 端点已完整实现并支持 string/int64 双轨 ID。无占位符残留，跳过详细修复计划。 |

---

## 附录：跨 BC 关联说明

本计划仅覆盖 Product BC 内部修复。以下问题涉及跨 BC 协调，需在跨 BC 修复计划中跟踪：

1. **审计 #1 ProductUpdatedEvent 消费方**：购物车域（刷新展示快照）、搜索域（同步 ES 读模型）需确认已订阅 `ProductUpdatedEvent`。参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D1 章节。
2. **审计 #5 Guid→int64 迁移**：4 个 BC（Product/Order/ReviewAfterSales/SellerShop）均有此问题，跨 BC 统一治理参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D5.1 章节与 ADR-0007。本计划仅修复 Product BC 的 int64 写入路径，proto 字段标 deprecated 需跨 BC 协调。
3. **审计 #10 ReviewModeratedEvent schema**：需评价与售后域补全 `SpuId` 与 `Rating` 字段，参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D1 章节。
4. **审计 #11 Money 共享内核**：`Money` 值对象在 Product/Promotion/Order/Cart 多 BC 使用，共享内核修复需跨 BC 评审，参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D3.1 章节与 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md] G3.4 章节。
