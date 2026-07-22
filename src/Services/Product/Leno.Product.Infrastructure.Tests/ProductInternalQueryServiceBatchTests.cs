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
/// P1-T8 单元测试：验证 <see cref="ProductInternalQueryService.GetSkuInfosBatchAsync"/>
/// 使用批量查询替代 N+1 逐条查询，正确返回匹配的 SKU 概要信息。
/// 使用 EF Core InMemory provider 构造真实仓储，覆盖多 SPU/空集合/不存在 SKU/重复 ID 等场景。
/// </summary>
public class ProductInternalQueryServiceBatchTests
{
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    /// <summary>
    /// 跨多个 SPU 批量查询 SKU，应返回所有匹配的 SKU 概要信息（单次 DB 查询替代 N+1）。
    /// </summary>
    [Fact]
    public async Task GetSkuInfosBatch_MultipleSpuMultipleSku_ReturnsAllMatches()
    {
        // Arrange — 两个 SPU 各含 2 个 SKU，查询其中 3 个
        await using var context = await CreateContextAsync();
        var spu1 = CreateSpuWithSku("SKU-BATCH-001", "SKU-BATCH-002", "Product A");
        var spu2 = CreateSpuWithSku("SKU-BATCH-003", "SKU-BATCH-004", "Product B");
        context.SPUs.AddRange(spu1, spu2);
        await context.SaveChangesAsync();

        var sku1Id = spu1.SKUs.First().Id;
        var sku2Id = spu1.SKUs.Last().Id;
        var sku3Id = spu2.SKUs.First().Id;

        var sut = CreateService(context);

        // Act — 查询跨两个 SPU 的 3 个 SKU
        var results = await sut.GetSkuInfosBatchAsync(
            new List<Guid> { sku1Id, sku2Id, sku3Id },
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(3);
        results.Select(r => r.SkuId).Should().BeEquivalentTo(new[] { sku1Id, sku2Id, sku3Id });

        // 验证每个 DTO 映射了正确的 SPU 信息
        var dto1 = results.Single(r => r.SkuId == sku1Id);
        dto1.SpuId.Should().Be(spu1.Id);
        dto1.Title.Should().Be("Product A");

        var dto3 = results.Single(r => r.SkuId == sku3Id);
        dto3.SpuId.Should().Be(spu2.Id);
        dto3.Title.Should().Be("Product B");
    }

    /// <summary>
    /// 空列表应返回空结果，不触发任何 DB 查询。
    /// </summary>
    [Fact]
    public async Task GetSkuInfosBatch_EmptyList_ReturnsEmpty()
    {
        await using var context = await CreateContextAsync();
        var sut = CreateService(context);

        var results = await sut.GetSkuInfosBatchAsync(new List<Guid>(), CancellationToken.None);

        results.Should().BeEmpty();
    }

    /// <summary>
    /// 混合已存在与不存在的 SKU ID，应仅返回已存在的 SKU。
    /// </summary>
    [Fact]
    public async Task GetSkuInfosBatch_MixedExistingAndNonExistent_ReturnsOnlyExisting()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var spu = CreateSpuWithSku("SKU-MIX-001", "SKU-MIX-002", "Mixed Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();

        var existingSkuId = spu.SKUs.First().Id;
        var nonExistentSkuId = Guid.NewGuid();

        var sut = CreateService(context);

        // Act
        var results = await sut.GetSkuInfosBatchAsync(
            new List<Guid> { existingSkuId, nonExistentSkuId },
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results.Single().SkuId.Should().Be(existingSkuId);
    }

    /// <summary>
    /// 传入重复的 SKU ID，应去重后返回唯一结果（不产生重复 DTO）。
    /// </summary>
    [Fact]
    public async Task GetSkuInfosBatch_DuplicateSkuIds_ReturnsDeduplicatedResults()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var spu = CreateSpuWithSku("SKU-DUP-001", "SKU-DUP-002", "Dup Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();

        var skuId = spu.SKUs.First().Id;

        var sut = CreateService(context);

        // Act — 同一 skuId 传入 3 次
        var results = await sut.GetSkuInfosBatchAsync(
            new List<Guid> { skuId, skuId, skuId },
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1, "重复的 SKU ID 应去重");
        results.Single().SkuId.Should().Be(skuId);
    }

    /// <summary>
    /// SPU 含多个 SKU，仅查询其中一个时，不应返回该 SPU 的其他 SKU。
    /// </summary>
    [Fact]
    public async Task GetSkuInfosBatch_PartialSpuSkus_ReturnsOnlyQueriedSku()
    {
        // Arrange
        await using var context = await CreateContextAsync();
        var spu = CreateSpuWithSku("SKU-PART-001", "SKU-PART-002", "Partial Product");
        context.SPUs.Add(spu);
        await context.SaveChangesAsync();

        var queriedSkuId = spu.SKUs.First().Id;
        var otherSkuId = spu.SKUs.Last().Id;

        var sut = CreateService(context);

        // Act — 仅查询 SPU 的第一个 SKU
        var results = await sut.GetSkuInfosBatchAsync(
            new List<Guid> { queriedSkuId },
            CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results.Single().SkuId.Should().Be(queriedSkuId);
        results.Should().NotContain(r => r.SkuId == otherSkuId);
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

    private static SPU CreateSpuWithSku(string skuCode1, string skuCode2, string title)
    {
        var spu = CreateDraftSpu(title);
        var sku1 = SKU.Create(Guid.NewGuid(), spu.Id, skuCode1,
            Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        var sku2 = SKU.Create(Guid.NewGuid(), spu.Id, skuCode2,
            Money.Create(149.99m, "CNY"), 50,
            SkuSpec.Create([SpecAttribute.Create("Color", "Blue")]));
        spu.AddSku(sku1);
        spu.AddSku(sku2);
        return spu;
    }
}
