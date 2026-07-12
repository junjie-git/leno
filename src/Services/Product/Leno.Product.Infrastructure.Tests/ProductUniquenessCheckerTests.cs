using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.ValueObjects;
using Leno.Product.Infrastructure.Services;
using Leno.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Tests;

public class ProductUniquenessCheckerTests
{
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task IsSkuCodeUnique_SkuCodeNotExists_ShouldReturnTrue()
    {
        // Arrange
        var context = await CreateContextAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsSkuCodeUniqueAsync("SKU-UNIQUE-001");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSkuCodeUnique_SkuCodeExists_ShouldReturnFalse()
    {
        // Arrange
        var context = await CreateContextAsync();
        var spu = CreateSpuWithSku("SKU-001");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsSkuCodeUniqueAsync("SKU-001");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSkuCodeUnique_SkuCodeExists_ExcludeProduct_ShouldReturnTrue()
    {
        // Arrange
        var context = await CreateContextAsync();
        var spu = CreateSpuWithSku("SKU-001");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsSkuCodeUniqueAsync("SKU-001", spu.Id);

        // Assert
        result.Should().BeTrue(); // Excluded the current product
    }

    [Fact]
    public async Task IsSkuCodeUnique_EmptyCode_ShouldReturnFalse()
    {
        // Arrange
        var context = await CreateContextAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsSkuCodeUniqueAsync("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTitleUniqueInShop_TitleNotExists_ShouldReturnTrue()
    {
        // Arrange
        var context = await CreateContextAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsTitleUniqueInShopAsync("Unique Title", ShopId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTitleUniqueInShop_TitleExists_ShouldReturnFalse()
    {
        // Arrange
        var context = await CreateContextAsync();
        var spu = CreateDraftSpu("Test Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsTitleUniqueInShopAsync("Test Product", spu.ShopId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTitleUniqueInShop_TitleExists_ExcludeProduct_ShouldReturnTrue()
    {
        // Arrange
        var context = await CreateContextAsync();
        var spu = CreateDraftSpu("Test Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsTitleUniqueInShopAsync("Test Product", spu.ShopId, spu.Id);

        // Assert
        result.Should().BeTrue(); // Excluded the current product
    }

    [Fact]
    public async Task IsTitleUniqueInShop_EmptyTitle_ShouldReturnFalse()
    {
        // Arrange
        var context = await CreateContextAsync();
        var checker = new ProductUniquenessChecker(context);

        // Act
        var result = await checker.IsTitleUniqueInShopAsync("", ShopId);

        // Assert
        result.Should().BeFalse();
    }

    private static async Task<ProductDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ProductDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static SPU CreateDraftSpu(string title)
    {
        return SPU.Create(Guid.NewGuid(), ShopId, SellerId, title,
            "https://img.example.com/1.jpg", CategoryId, images: []);
    }

    private static SPU CreateSpuWithSku(string skuCode)
    {
        var spu = CreateDraftSpu("Test Product");
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, skuCode,
            Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);
        return spu;
    }
}