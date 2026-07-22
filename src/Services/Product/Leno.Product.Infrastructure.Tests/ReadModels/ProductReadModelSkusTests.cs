using System.Reflection;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Application.Queries;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

/// <summary>
/// P2-T18 单元测试：验证 <see cref="ProductPublishedReadModelSyncConsumer"/> 投影时填充
/// <see cref="ProductReadModel.Skus"/> 嵌套文档，且 <see cref="ProductReadModelAccessor"/>
/// 将其映射到 <see cref="ProductDetailResult.Skus"/>。
/// 修复审计 #18：原读模型仅含 MinPrice/MaxPrice 价格区间，买家端详情页走 CQRS 读侧无法展示 SKU 选择器。
/// </summary>
public class ProductReadModelSkusTests
{
    /// <summary>
    /// 含多 SKU 的 SPU 投影时，Skus 应包含全部 SKU 的完整字段（SkuId/SkuCode/Price/Currency/StockQty/Status/ImageUrl/SpecAttributes）。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_WithMultipleSkus_SkusContainsAllFields()
    {
        // Arrange — 创建含 2 个 SKU 的 SPU
        var spu = CreateSpuWithSkus(
            ("SKU-RED", 99.99m, "CNY", "Color", "Red", "https://img.example.com/red.png"),
            ("SKU-BLUE", 79.50m, "CNY", "Color", "Blue", null));

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
        readModel!.Skus.Should().HaveCount(2);

        var redSku = readModel.Skus.First(s => s.SkuCode == "SKU-RED");
        redSku.SkuId.Should().Be(spu.SKUs.First(s => s.SkuCode == "SKU-RED").Id);
        redSku.Price.Should().Be(99.99m);
        redSku.Currency.Should().Be("CNY");
        redSku.StockQty.Should().Be(100);
        redSku.Status.Should().Be("Active");
        redSku.ImageUrl.Should().Be("https://img.example.com/red.png");
        redSku.SpecAttributes.Should().HaveCount(1);
        redSku.SpecAttributes[0].Name.Should().Be("Color");
        redSku.SpecAttributes[0].Value.Should().Be("Red");

        var blueSku = readModel.Skus.First(s => s.SkuCode == "SKU-BLUE");
        blueSku.Price.Should().Be(79.50m);
        blueSku.ImageUrl.Should().BeNull();
        blueSku.SpecAttributes.Should().HaveCount(1);
        blueSku.SpecAttributes[0].Value.Should().Be("Blue");
    }

    /// <summary>
    /// 无 SKU 的 SPU 投影时，Skus 应为空集合。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_NoSkus_SkusEmpty()
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
        readModel!.Skus.Should().BeEmpty();
    }

    /// <summary>
    /// 多规格属性 SKU 投影时，SpecAttributes 应完整保留全部规格。
    /// </summary>
    [Fact]
    public async Task BuildReadModelAsync_MultiSpecSku_SpecAttributesAllPreserved()
    {
        // Arrange — 含 2 项规格属性的 SKU
        var spu = SPU.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Multi-Spec Product",
            "https://img.example.com/1.jpg", Guid.NewGuid(), images: new List<ProductImage>());

        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-MULTI",
            Money.Create(50m, "CNY"), 200,
            SkuSpec.Create(new[]
            {
                SpecAttribute.Create("Color", "Green"),
                SpecAttribute.Create("Size", "XL")
            }));
        spu.AddSku(sku);

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
        readModel!.Skus.Should().HaveCount(1);
        readModel.Skus[0].SpecAttributes.Should().HaveCount(2);
        readModel.Skus[0].SpecAttributes.Should().Contain(a => a.Name == "Color" && a.Value == "Green");
        readModel.Skus[0].SpecAttributes.Should().Contain(a => a.Name == "Size" && a.Value == "XL");
    }

    /// <summary>
    /// Accessor.GetByIdAsync 应将 ProductReadModel.Skus 映射到 ProductDetailResult.Skus，
    /// 保留全部字段。
    /// </summary>
    [Fact]
    public async Task Accessor_GetByIdAsync_ShouldMapSkusToProductDetailResult()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();

        var readModel = new ProductReadModel
        {
            Id = productId,
            Title = "测试商品",
            MainImageUrl = "https://img.example.com/main.png",
            CategoryId = Guid.NewGuid(),
            ShopId = Guid.NewGuid(),
            Status = "OnSale",
            Specs = new List<string> { "Color" },
            MinPrice = 79.50m,
            MaxPrice = 99.99m,
            Currency = "CNY",
            Skus = new List<SkuReadModel>
            {
                new()
                {
                    SkuId = skuId1,
                    SkuCode = "SKU-001",
                    Price = 99.99m,
                    Currency = "CNY",
                    StockQty = 100,
                    Status = "Active",
                    ImageUrl = "https://img.example.com/sku1.png",
                    SpecAttributes = new List<SkuSpecAttributeReadModel>
                    {
                        new() { Name = "Color", Value = "Red" }
                    }
                },
                new()
                {
                    SkuId = skuId2,
                    SkuCode = "SKU-002",
                    Price = 79.50m,
                    Currency = "CNY",
                    StockQty = 50,
                    Status = "Inactive",
                    ImageUrl = null,
                    SpecAttributes = new List<SkuSpecAttributeReadModel>
                    {
                        new() { Name = "Color", Value = "Blue" }
                    }
                }
            },
            IndexedAt = new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc)
        };

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        esRepoMock.Setup(r => r.GetByIdAsync(productId.ToString(), ProductSearchService.ProductIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readModel);

        var accessor = new ProductReadModelAccessor(esRepoMock.Object);

        // Act
        var result = await accessor.GetByIdAsync(productId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Skus.Should().HaveCount(2);

        var sku1 = result.Skus.First(s => s.SkuId == skuId1);
        sku1.SkuCode.Should().Be("SKU-001");
        sku1.Price.Should().Be(99.99m);
        sku1.Currency.Should().Be("CNY");
        sku1.StockQty.Should().Be(100);
        sku1.Status.Should().Be("Active");
        sku1.ImageUrl.Should().Be("https://img.example.com/sku1.png");
        sku1.SpecAttributes.Should().HaveCount(1);
        sku1.SpecAttributes[0].Name.Should().Be("Color");
        sku1.SpecAttributes[0].Value.Should().Be("Red");

        var sku2 = result.Skus.First(s => s.SkuId == skuId2);
        sku2.SkuCode.Should().Be("SKU-002");
        sku2.Status.Should().Be("Inactive");
        sku2.ImageUrl.Should().BeNull();
        sku2.SpecAttributes[0].Value.Should().Be("Blue");
    }

    /// <summary>
    /// Accessor 收到无 SKU 的读模型时，ProductDetailResult.Skus 应为空集合。
    /// </summary>
    [Fact]
    public async Task Accessor_GetByIdAsync_WithEmptySkus_ReturnsEmptySkusList()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var readModel = new ProductReadModel
        {
            Id = productId,
            Title = "无 SKU 商品",
            MainImageUrl = "https://img.example.com/main.png",
            CategoryId = Guid.NewGuid(),
            ShopId = Guid.NewGuid(),
            Status = "OnSale",
            MinPrice = 0m,
            MaxPrice = 0m,
            Currency = "CNY",
            Skus = Array.Empty<SkuReadModel>(),
            IndexedAt = new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc)
        };

        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        esRepoMock.Setup(r => r.GetByIdAsync(productId.ToString(), ProductSearchService.ProductIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readModel);

        var accessor = new ProductReadModelAccessor(esRepoMock.Object);

        // Act
        var result = await accessor.GetByIdAsync(productId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Skus.Should().BeEmpty();
    }

    /// <summary>
    /// Accessor 收到 productId == Guid.Empty 时应返回 null，不查询 ES。
    /// </summary>
    [Fact]
    public async Task Accessor_GetByIdAsync_WithEmptyProductId_ReturnsNullWithoutQuery()
    {
        // Arrange
        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        var accessor = new ProductReadModelAccessor(esRepoMock.Object);

        // Act
        var result = await accessor.GetByIdAsync(Guid.Empty, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        esRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Accessor 收到 ES 返回 null 时应返回 null。
    /// </summary>
    [Fact]
    public async Task Accessor_GetByIdAsync_WhenEsReturnsNull_ReturnsNull()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var esRepoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        esRepoMock.Setup(r => r.GetByIdAsync(productId.ToString(), ProductSearchService.ProductIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReadModel?)null);

        var accessor = new ProductReadModelAccessor(esRepoMock.Object);

        // Act
        var result = await accessor.GetByIdAsync(productId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
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
    /// 创建含多个 SKU 的 SPU，每个 SKU 可指定规格属性与图片。
    /// </summary>
    private static SPU CreateSpuWithSkus(
        params (string Code, decimal Price, string Currency, string SpecName, string SpecValue, string? ImageUrl)[] skuDefs)
    {
        var spu = SPU.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Product",
            "https://img.example.com/1.jpg", Guid.NewGuid(), images: new List<ProductImage>());

        foreach (var (code, price, currency, specName, specValue, imageUrl) in skuDefs)
        {
            var sku = SKU.Create(Guid.NewGuid(), spu.Id, code,
                Money.Create(price, currency), 100,
                SkuSpec.Create(new[] { SpecAttribute.Create(specName, specValue) }),
                imageUrl);
            spu.AddSku(sku);
        }

        return spu;
    }
}
