using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// CartAppService.FindCartAsync 单元测试（P1-11）。
/// 验证购物车不存在时返回 null（不创建），与 GetCartAsync（GetOrCreateCartAsync）的"不存在则创建"语义区分。
/// </summary>
public class CartAppServiceFindCartTests
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

    public CartAppServiceFindCartTests()
    {
        _sut = new CartAppService(
            _cartRepoMock.Object,
            _priceServiceMock.Object,
            _uowMock.Object,
            _anonymousCartRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task FindCartAsync_CartNotExists_ShouldReturnNullWithoutCreating()
    {
        // 购物车不存在时返回 null，不应触发 AddAsync 创建空购物车
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        var result = await _sut.FindCartAsync(UserId);

        result.Should().BeNull();
        _cartRepoMock.Verify(r => r.AddAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindCartAsync_CartExists_ShouldReturnCartDto()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
        cart.AddItem(SkuId, 2, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SkuPriceSnapshot
                {
                    SkuId = SkuId,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售",
                    MainImageUrl = "https://img.example.com/a.jpg",
                    SellerId = SellerId
                }
            });

        var result = await _sut.FindCartAsync(UserId);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].SkuId.Should().Be(SkuId);
        result.Items[0].UnitPrice.Should().Be(19.9m);
        _cartRepoMock.Verify(r => r.AddAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindCartAsync_ShouldNotCallGetOrCreateCartAsync()
    {
        // 与 GetCartAsync 区分：FindCartAsync 不应在 null 时创建空购物车
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        await _sut.FindCartAsync(UserId);

        _cartRepoMock.Verify(r => r.AddAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
