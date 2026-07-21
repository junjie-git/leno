using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Tests;

/// <summary>
/// 验证 SKU/SPU 实体的 EF Core 唯一索引配置（审计 #2/#20）。
/// InMemory provider 不强制唯一约束，但模型元数据会反映 IEntityTypeConfiguration 中的 IsUnique() 配置。
/// </summary>
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
        var skuEntity = context.Model.FindEntityType(typeof(SKU));
        skuEntity.Should().NotBeNull();
        var skuCodeIndex = skuEntity!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(SKU.SkuCode)) && i.IsUnique);

        // Assert
        skuCodeIndex.Should().NotBeNull();
        skuCodeIndex!.IsUnique.Should().BeTrue();
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
        var spuEntity = context.Model.FindEntityType(typeof(SPU));
        spuEntity.Should().NotBeNull();
        var compositeIndex = spuEntity!.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(SPU.ShopId)) &&
                i.Properties.Any(p => p.Name == nameof(SPU.Title)) &&
                i.IsUnique);

        // Assert
        compositeIndex.Should().NotBeNull();
        compositeIndex!.IsUnique.Should().BeTrue();
    }
}
