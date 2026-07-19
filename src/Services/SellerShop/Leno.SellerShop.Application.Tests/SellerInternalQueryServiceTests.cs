using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.InternalQueryServices;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.SellerShop.Application.Tests;

/// <summary>
/// 卖家内部查询服务单元测试，覆盖 ValidateOwnershipAsync 资源归属校验的 7 个核心场景：
/// shop / spu / order 三类资源归属命中与未命中、防腐层失败 fail-closed、未知 resourceType fail-closed。
/// </summary>
public class SellerInternalQueryServiceTests
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ResourceId = Guid.NewGuid();

    [Fact]
    public async Task ValidateOwnership_ShopOwned_ReturnsTrue()
    {
        var shopAppService = new Mock<IShopAppService>();
        shopAppService.Setup(s => s.GetMyShopAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopDto { Id = ResourceId, SellerId = SellerId });
        var sut = CreateService(shopAppService: shopAppService.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "shop", ResourceId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOwnership_ShopNotOwned_ReturnsFalse()
    {
        var shopAppService = new Mock<IShopAppService>();
        shopAppService.Setup(s => s.GetMyShopAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopDto { Id = Guid.NewGuid(), SellerId = SellerId });
        var sut = CreateService(shopAppService: shopAppService.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "shop", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_ShopMissing_ThrowsShopNotFound_ReturnsFalse()
    {
        // 卖家未关联店铺时 IShopAppService.GetMyShopAsync 抛 SHOP_NOT_FOUND，应判 false（fail-closed）
        var shopAppService = new Mock<IShopAppService>();
        shopAppService.Setup(s => s.GetMyShopAsync(SellerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SellerShopDomainException("店铺不存在", "SHOP_NOT_FOUND"));
        var sut = CreateService(shopAppService: shopAppService.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "shop", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_SpuOwned_ReturnsTrue()
    {
        var productAntiCorruption = new Mock<IProductAntiCorruptionService>();
        productAntiCorruption.Setup(p => p.GetSpuSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SellerId);
        var sut = CreateService(productAntiCorruption: productAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "spu", ResourceId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOwnership_SpuAntiCorruptionNull_ReturnsFalse()
    {
        var productAntiCorruption = new Mock<IProductAntiCorruptionService>();
        productAntiCorruption.Setup(p => p.GetSpuSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var sut = CreateService(productAntiCorruption: productAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "spu", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_OrderOwned_ReturnsTrue()
    {
        var orderAntiCorruption = new Mock<IOrderAntiCorruptionService>();
        orderAntiCorruption.Setup(o => o.GetOrderSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SellerId);
        var sut = CreateService(orderAntiCorruption: orderAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "order", ResourceId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOwnership_OrderAntiCorruptionNull_ReturnsFalse()
    {
        var orderAntiCorruption = new Mock<IOrderAntiCorruptionService>();
        orderAntiCorruption.Setup(o => o.GetOrderSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var sut = CreateService(orderAntiCorruption: orderAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "order", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_UnknownResourceType_ReturnsFalse()
    {
        var sut = CreateService();

        var result = await sut.ValidateOwnershipAsync(SellerId, "unknown", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    private static SellerInternalQueryService CreateService(
        IShopAppService? shopAppService = null,
        IProductAntiCorruptionService? productAntiCorruption = null,
        IOrderAntiCorruptionService? orderAntiCorruption = null)
    {
        return new SellerInternalQueryService(
            Mock.Of<ISellerAppService>(),
            shopAppService ?? Mock.Of<IShopAppService>(),
            productAntiCorruption ?? Mock.Of<IProductAntiCorruptionService>(),
            orderAntiCorruption ?? Mock.Of<IOrderAntiCorruptionService>(),
            Mock.Of<ILogger<SellerInternalQueryService>>());
    }
}
