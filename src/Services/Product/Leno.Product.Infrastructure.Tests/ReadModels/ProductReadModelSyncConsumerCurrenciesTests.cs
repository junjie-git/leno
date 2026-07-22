using System.Reflection;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

/// <summary>
/// P1-T15 单元测试：验证 <see cref="ProductPublishedReadModelSyncConsumer.BuildReadModelAsync"/>
/// 正确填充 <see cref="ProductReadModel.Currencies"/> 集合。
/// 修复审计 #15：原实现仅取首个 SKU 币种或硬编码 "CNY"，多币种店铺无法正确展示。
/// </summary>
public class ProductReadModelSyncConsumerCurrenciesTests
{
    /// <summary>
    /// 多币种 SKU 时，Currencies 应包含所有去重币种，Currency 取首个。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_MultiCurrencySkus_CurrenciesContainsAllDistinct()
    {
        // Arrange — 创建含 3 个不同币种 SKU 的 SPU
        var spu = CreateSpuWithSkus(
            ("SKU-CNY", 99.99m, "CNY"),
            ("SKU-USD", 19.99m, "USD"),
            ("SKU-EUR", 14.99m, "EUR"));

        var spuRepoMock = new Mock<ISPURepository>();
        spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var consumer = new ProductPublishedReadModelSyncConsumer(
            spuRepoMock.Object, esRepoMock.Object,
            NullLogger<ProductPublishedReadModelSyncConsumer>.Instance);

        var evt = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = spu.Id
        };

        // Act
        var (id, indexName, readModel) = await InvokeBuildReadModelAsync(consumer, evt);

        // Assert
        readModel.Should().NotBeNull();
        readModel!.Currencies.Should().HaveCount(3);
        readModel.Currencies.Should().Contain(new[] { "CNY", "USD", "EUR" });
        readModel.Currency.Should().Be("CNY", "Currency 取首个 SKU 币种作为默认展示");
    }

    /// <summary>
    /// 单币种多 SKU 时，Currencies 应仅包含一个去重币种。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_SingleCurrencyMultipleSkus_CurrenciesHasOneElement()
    {
        // Arrange
        var spu = CreateSpuWithSkus(
            ("SKU-001", 99.99m, "CNY"),
            ("SKU-002", 79.99m, "CNY"),
            ("SKU-003", 59.99m, "CNY"));

        var spuRepoMock = new Mock<ISPURepository>();
        spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var consumer = new ProductPublishedReadModelSyncConsumer(
            spuRepoMock.Object, esRepoMock.Object,
            NullLogger<ProductPublishedReadModelSyncConsumer>.Instance);

        var evt = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = spu.Id
        };

        // Act
        var (_, _, readModel) = await InvokeBuildReadModelAsync(consumer, evt);

        // Assert
        readModel.Should().NotBeNull();
        readModel!.Currencies.Should().HaveCount(1);
        readModel.Currencies.Should().ContainSingle().Which.Should().Be("CNY");
        readModel.Currency.Should().Be("CNY");
    }

    /// <summary>
    /// 无 SKU 时，Currencies 应为空集合，Currency 默认 "CNY"。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_NoSkus_CurrenciesEmptyAndCurrencyDefaultsToCNY()
    {
        // Arrange — 无 SKU 的草稿 SPU
        var spu = SPU.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "No-SKU Product",
            "https://img.example.com/1.jpg", Guid.NewGuid(), images: new List<ProductImage>());

        var spuRepoMock = new Mock<ISPURepository>();
        spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var consumer = new ProductPublishedReadModelSyncConsumer(
            spuRepoMock.Object, esRepoMock.Object,
            NullLogger<ProductPublishedReadModelSyncConsumer>.Instance);

        var evt = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = spu.Id
        };

        // Act
        var (_, _, readModel) = await InvokeBuildReadModelAsync(consumer, evt);

        // Assert
        readModel.Should().NotBeNull();
        readModel!.Currencies.Should().BeEmpty();
        readModel.Currency.Should().Be("CNY", "无 SKU 时默认 CNY");
    }

    /// <summary>
    /// 重复币种的 SKU 应去重后存入 Currencies。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_DuplicateCurrencySkus_CurrenciesAreDistinct()
    {
        // Arrange — 3 个 SKU 但 2 个币种相同
        var spu = CreateSpuWithSkus(
            ("SKU-001", 99.99m, "CNY"),
            ("SKU-002", 79.99m, "CNY"),
            ("SKU-003", 19.99m, "USD"));

        var spuRepoMock = new Mock<ISPURepository>();
        spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var consumer = new ProductPublishedReadModelSyncConsumer(
            spuRepoMock.Object, esRepoMock.Object,
            NullLogger<ProductPublishedReadModelSyncConsumer>.Instance);

        var evt = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = spu.Id
        };

        // Act
        var (_, _, readModel) = await InvokeBuildReadModelAsync(consumer, evt);

        // Assert — 去重后仅 CNY 和 USD
        readModel.Should().NotBeNull();
        readModel!.Currencies.Should().HaveCount(2);
        readModel.Currencies.Should().Contain(new[] { "CNY", "USD" });
    }

    /// <summary>
    /// SPU 不存在时返回 null 读模型（跳过同步）。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_SpuNotFound_ReturnsNullReadModel()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var spuRepoMock = new Mock<ISPURepository>();
        spuRepoMock.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SPU?)null);

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var consumer = new ProductPublishedReadModelSyncConsumer(
            spuRepoMock.Object, esRepoMock.Object,
            NullLogger<ProductPublishedReadModelSyncConsumer>.Instance);

        var evt = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = productId
        };

        // Act
        var (id, indexName, readModel) = await InvokeBuildReadModelAsync(consumer, evt);

        // Assert
        readModel.Should().BeNull();
        id.Should().BeEmpty();
        indexName.Should().BeEmpty();
    }

    /// <summary>
    /// 通过反射调用受保护的 BuildReadModelAsync 方法。
    /// </summary>
    private static async Task<(string Id, string IndexName, ProductReadModel? ReadModel)> InvokeBuildReadModelAsync(
        ProductPublishedReadModelSyncConsumer consumer, ProductPublishedEvent evt)
    {
        var method = typeof(ProductPublishedReadModelSyncConsumer)
            .GetMethod("BuildReadModelAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("BuildReadModelAsync 应为受保护的虚方法");
        var result = await (Task<(string, string, ProductReadModel?)>)method!.Invoke(
            consumer, [evt, CancellationToken.None])!;
        return result;
    }

    /// <summary>
    /// 创建含多个不同币种 SKU 的 SPU。
    /// </summary>
    private static SPU CreateSpuWithSkus(params (string Code, decimal Price, string Currency)[] skuDefs)
    {
        var spu = SPU.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Product",
            "https://img.example.com/1.jpg", Guid.NewGuid(), images: new List<ProductImage>());

        foreach (var (code, price, currency) in skuDefs)
        {
            var sku = SKU.Create(Guid.NewGuid(), spu.Id, code,
                Money.Create(price, currency), 100,
                SkuSpec.Create(new[] { SpecAttribute.Create("Color", "Red") }));
            spu.AddSku(sku);
        }

        return spu;
    }
}
