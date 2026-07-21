using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// CartAppService.BuildCartDtoAsync 价格服务故障降级测试。
/// 验证防腐层实际抛出 AntiCorruptionException（继承 DomainException）时，BuildCartDtoAsync 能命中降级分支，
/// 不向控制器冒泡异常、标记 PriceUnavailable=true，避免误导性 0 元可结算。
/// </summary>
public class CartAppServiceAntiCorruptionDegradeTests
{
    private readonly Mock<ICartRepository> _cartRepoMock = new();
    private readonly Mock<ICartPriceService> _priceServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IAnonymousCartRepository> _anonymousCartRepoMock = new();
    private readonly Mock<ILogger<CartAppService>> _loggerMock = new();
    private readonly CartAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    public CartAppServiceAntiCorruptionDegradeTests()
    {
        _sut = new CartAppService(
            _cartRepoMock.Object,
            _priceServiceMock.Object,
            _uowMock.Object,
            _anonymousCartRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegradeAndMarkPriceUnavailable()
    {
        // Arrange：AntiCorruptionBase 实际抛 AntiCorruptionException，旧 catch(CartDomainException) 不会命中
        var cart = CreateCart();
        cart.AddItem(SkuId, 2, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AntiCorruptionException("product 网络故障", "PRODUCT_UNAVAILABLE"));

        // Act
        var result = await _sut.GetCartAsync(UserId);

        // Assert：进入降级分支，标记 PriceUnavailable=true，不向控制器冒泡
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.Items[0].Available.Should().BeFalse();
        result.Items[0].Title.Should().Be("[价格加载失败]");
        result.SelectedTotalAmount.Should().Be(0m);
    }

    private static CartAggregate CreateCart()
    {
        return CartAggregate.Create(Guid.NewGuid(), UserId);
    }
}
