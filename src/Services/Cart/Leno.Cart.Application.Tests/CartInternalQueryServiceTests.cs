using Leno.Cart.Application.InternalQueryServices;
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
/// CartInternalQueryService P1-10+P1-11 单元测试：
/// - P1-10：金额转分采用四舍五入（AwayFromZero），19.999m → 2000，0.005m → 1
/// - P1-11：购物车不存在时 GetCartSnapshotAsync 返回 null（不创建），gRPC NotFound 分支可达
/// </summary>
public class CartInternalQueryServiceTests
{
    private readonly Mock<ICartAppService> _cartAppServiceMock = new();
    private readonly CartInternalQueryService _sut;

    public CartInternalQueryServiceTests()
    {
        _sut = new CartInternalQueryService(_cartAppServiceMock.Object);
    }

    [Fact]
    public async Task GetCartSnapshotAsync_CartNotExists_ShouldReturnNull()
    {
        // P1-11：FindCartAsync 返回 null 时（购物车不存在），GetCartSnapshotAsync 返回 null
        _cartAppServiceMock.Setup(s => s.FindCartAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartDto?)null);

        var result = await _sut.GetCartSnapshotAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCartSnapshotAsync_EmptyCart_ShouldReturnNull()
    {
        // P1-11：购物车存在但无项时也返回 null（明确"无有效购物车"语义）
        var emptyCart = new CartDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Items = Array.Empty<CartItemDto>(),
            SelectedTotalAmount = 0m,
            Currency = "CNY",
            TotalCount = 0
        };
        _cartAppServiceMock.Setup(s => s.FindCartAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyCart);

        var result = await _sut.GetCartSnapshotAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCartSnapshotAsync_FractionalPrice_ShouldRoundToAwayFromZero()
    {
        // P1-10：19.999m * 100 = 1999.9 → 四舍五入为 2000（原截断实现为 1999）
        var cart = new CartDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Items = new[]
            {
                new CartItemDto
                {
                    Id = Guid.NewGuid(),
                    SkuId = Guid.NewGuid(),
                    SellerId = Guid.NewGuid(),
                    Quantity = 1,
                    IsSelected = true,
                    SourceCartItemId = Guid.NewGuid(),
                    UnitPrice = 19.999m,
                    Currency = "CNY",
                    Title = "测试商品",
                    MainImageUrl = string.Empty,
                    Available = true,
                    PriceUnavailable = false
                }
            },
            SelectedTotalAmount = 19.999m,
            Currency = "CNY",
            TotalCount = 1
        };
        _cartAppServiceMock.Setup(s => s.FindCartAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var result = await _sut.GetCartSnapshotAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Items[0].UnitPriceCents.Should().Be(2000L, "19.999m 应四舍五入为 2000 分而非截断为 1999");
        result.TotalCents.Should().Be(2000L);
    }

    [Fact]
    public async Task GetCartSnapshotAsync_HalfCent_ShouldRoundUp()
    {
        // P1-10：0.005m * 100 = 0.5 → 四舍五入为 1（原截断为 0）
        var cart = new CartDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Items = new[]
            {
                new CartItemDto
                {
                    Id = Guid.NewGuid(),
                    SkuId = Guid.NewGuid(),
                    SellerId = Guid.NewGuid(),
                    Quantity = 1,
                    IsSelected = true,
                    SourceCartItemId = Guid.NewGuid(),
                    UnitPrice = 0.005m,
                    Currency = "CNY",
                    Title = "分位测试",
                    MainImageUrl = string.Empty,
                    Available = true,
                    PriceUnavailable = false
                }
            },
            SelectedTotalAmount = 0.005m,
            Currency = "CNY",
            TotalCount = 1
        };
        _cartAppServiceMock.Setup(s => s.FindCartAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var result = await _sut.GetCartSnapshotAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Items[0].UnitPriceCents.Should().Be(1L, "0.005m 应四舍五入为 1 分而非截断为 0");
    }
}
