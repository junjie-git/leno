using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Moq;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// AnonymousCartAppService 故障传播测试。
/// 验证 Redis 故障（CartInfrastructureException）经仓储抛出后不再被 AppService 掩盖为"购物车不存在 → 创建新空购物车覆盖"，
/// 即 GetOrCreateCartAsync 不再误用 catch 后返回 null 的语义。
/// </summary>
public class AnonymousCartAppServiceFailurePropagationTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();

    [Fact]
    public async Task GetCartAsync_WhenRepoThrowsCartInfrastructureException_ShouldPropagateNotSilentlyCreateNew()
    {
        // Arrange：Redis 故障应向上抛，而非被掩盖为"购物车不存在 → 创建新空购物车覆盖"
        _repoMock
            .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CartInfrastructureException("匿名购物车暂不可用", "CART_REDIS_UNAVAILABLE"));
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());
        var sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);

        // Act
        var act = () => sut.GetCartAsync("session-1");

        // Assert
        await act.Should().ThrowAsync<CartInfrastructureException>();
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.RefreshTtlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
