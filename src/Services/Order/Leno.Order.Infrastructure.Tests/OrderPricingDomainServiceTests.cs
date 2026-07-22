using Leno.Order.Application.Services;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Infrastructure.Services;
using Moq;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// OrderPricingDomainService 单元测试，验证 ValidatePricesAsync 使用预查的 SKU 当前售价字典，
/// 不再循环调用 IProductAntiCorruptionService.GetSkuInfoAsync（消除 N+1 远程调用）。
/// </summary>
public class OrderPricingDomainServiceTests
{
    [Fact]
    public async Task ValidatePricesAsync_With_PreQueried_SkuCurrentPrices_Should_Not_Call_ProductAntiCorruption()
    {
        // Arrange
        var productAntiCorruptionMock = new Mock<IProductAntiCorruptionService>();
        var sut = new OrderPricingDomainService(productAntiCorruptionMock.Object);

        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        var skuCurrentPrices = new Dictionary<Guid, decimal>
        {
            { skuId1, 100m },
            { skuId2, 50m }
        };
        var skuPrices = new List<(Guid SkuId, decimal ExpectedPrice)>
        {
            (skuId1, 100m),
            (skuId2, 50m)
        };

        // Act：使用预查的 skuCurrentPrices 字典，不应再次调用 ProductAntiCorruption
        await sut.ValidatePricesAsync(skuPrices, skuCurrentPrices, CancellationToken.None);

        // Assert：不应再次调用 ProductAntiCorruption（使用预查的字典）
        productAntiCorruptionMock.Verify(
            p => p.GetSkuInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidatePricesAsync_PriceChanged_Should_Throw()
    {
        // Arrange
        var productAntiCorruptionMock = new Mock<IProductAntiCorruptionService>();
        var sut = new OrderPricingDomainService(productAntiCorruptionMock.Object);

        var skuId = Guid.NewGuid();
        var skuCurrentPrices = new Dictionary<Guid, decimal>
        {
            { skuId, 100m }
        };
        var skuPrices = new List<(Guid SkuId, decimal ExpectedPrice)> { (skuId, 99m) };

        // Act & Assert：期望单价与预查当前售价不一致时抛 OrderDomainException（错误码 ORDER_PRICE_CHANGED）
        var act = () => sut.ValidatePricesAsync(skuPrices, skuCurrentPrices, CancellationToken.None);
        var thrown = await act.Should().ThrowAsync<OrderDomainException>();
        thrown.Which.ErrorCode.Should().Be("ORDER_PRICE_CHANGED");
    }
}
