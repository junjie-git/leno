using Leno.Product.Application.Services;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.ValueObjects;
using Leno.Product.Infrastructure;
using Leno.Product.Infrastructure.Repositories;
using Leno.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Leno.Product.Infrastructure.Tests;

/// <summary>
/// <see cref="ProductInternalQueryService.GetSkuStockAsync"/> 与 <see cref="ProductInternalQueryService.GetSpuDetailAsync"/> 单元测试。
/// 使用 EF Core InMemory provider 构造真实仓储，覆盖存在/不存在/空集合等场景。
/// </summary>
public class ProductInternalQueryServiceTests
{
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public async Task GetSkuStock_ExistingSku_ReturnsAvailableAndReserved()
    {
        // Arrange
        var skuId = Guid.NewGuid();
        await using var context = await CreateContextAsync();
        var baseline = StockBaseline.Create(Guid.NewGuid(), skuId, initialQty: 100, productId: Guid.NewGuid());
        baseline.SyncReserved(30);
        context.StockBaselines.Add(baseline);
        await context.SaveChangesAsync();

        var sut = CreateService(context);

        // Act
        var result = await sut.GetSkuStockAsync(skuId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SkuId.Should().Be(skuId);
        result.Available.Should().Be(100);
        result.Reserved.Should().Be(30);
    }

    [Fact]
    public async Task GetSkuStock_UnknownSku_ReturnsNull()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetSkuStockAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpuDetail_ExistingSpu_ReturnsWithSkus()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var spu = CreateSpuWithSku("SKU-DETAIL-001", "Test Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetSpuDetailAsync(spu.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SpuId.Should().Be(spu.Id);
        result.SellerId.Should().Be(spu.SellerId);
        result.ShopId.Should().Be(spu.ShopId);
        result.Title.Should().Be(spu.Title);
        result.Subtitle.Should().Be(spu.Subtitle ?? string.Empty);
        result.MainImageUrl.Should().Be(spu.MainImageUrl);
        result.Description.Should().BeEmpty();
        result.Skus.Should().HaveCount(1);

        var skuDto = result.Skus.Single();
        var sku = spu.SKUs.Single();
        skuDto.SkuId.Should().Be(sku.Id);
        skuDto.SkuCode.Should().Be(sku.SkuCode);
        skuDto.Title.Should().Be(spu.Title);
        skuDto.MainImageUrl.Should().Be(sku.ImageUrl ?? spu.MainImageUrl);
        skuDto.Price.Should().Be(sku.Price.Amount);
        skuDto.Currency.Should().Be(sku.Price.Currency);
        skuDto.Stock.Should().Be(sku.StockQty);
        skuDto.Status.Should().Be(sku.Status.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task GetSpuDetail_UnknownSpu_ReturnsNull()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetSpuDetailAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpuDetail_WithNoSkus_ReturnsEmptyList()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var spu = CreateDraftSpu("Lonely Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetSpuDetailAsync(spu.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Skus.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSkuStock_EmptyGuid_ReturnsNull()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetSkuStockAsync(Guid.Empty, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpuDetail_EmptyGuid_ReturnsNull()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var sut = CreateService(context);

        // Act
        var result = await sut.GetSpuDetailAsync(Guid.Empty, CancellationToken.None);

        // Assert
        result.Should().BeNull();
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

    private static ProductInternalQueryService CreateService(ProductDbContext context)
    {
        var spuRepo = new EfCoreSPURepository(context);
        var stockRepo = new EfCoreStockBaselineRepository(context);
        return new ProductInternalQueryService(spuRepo, stockRepo);
    }

    private static SPU CreateDraftSpu(string title)
    {
        return SPU.Create(Guid.NewGuid(), ShopId, SellerId, title,
            "https://img.example.com/1.jpg", CategoryId, images: []);
    }

    private static SPU CreateSpuWithSku(string skuCode, string title)
    {
        var spu = CreateDraftSpu(title);
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, skuCode,
            Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);
        return spu;
    }
}
