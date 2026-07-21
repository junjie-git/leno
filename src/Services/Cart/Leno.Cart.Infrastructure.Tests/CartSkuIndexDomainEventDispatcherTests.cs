using Leno.Cart.Domain.Events;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Services;
using Moq;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// CartSkuIndexDomainEventDispatcher 单元测试。
/// 验证 SKU 加入/移除购物车领域事件被正确分发到反向索引服务，无关事件忽略，索引异常上抛。
/// </summary>
public class CartSkuIndexDomainEventDispatcherTests
{
    private readonly Mock<ICartSkuIndexService> _indexServiceMock = new();

    [Fact]
    public async Task DispatchAsync_SkuAddedToCartEvent_ShouldCallIndexServiceAddAsync()
    {
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuAddedToCartEvent(cartId, skuId)
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(
            s => s.AddAsync(skuId, cartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_SkuRemovedFromCartEvent_ShouldCallIndexServiceRemoveAsync()
    {
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuRemovedFromCartEvent(cartId, skuId)
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(
            s => s.RemoveAsync(skuId, cartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_MixedEvents_ShouldDispatchEachToCorrectHandler()
    {
        var cartId = Guid.NewGuid();
        var skuAdd = Guid.NewGuid();
        var skuRemove = Guid.NewGuid();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuAddedToCartEvent(cartId, skuAdd),
            new SkuRemovedFromCartEvent(cartId, skuRemove),
            new SkuAddedToCartEvent(cartId, skuAdd) // 重复也应再次调用
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(s => s.AddAsync(skuAdd, cartId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _indexServiceMock.Verify(s => s.RemoveAsync(skuRemove, cartId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_UnrelatedEvent_ShouldSkipSilently()
    {
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            Guid.NewGuid(),
            "not-an-event"
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(
            s => s.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _indexServiceMock.Verify(
            s => s.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_IndexServiceThrows_ShouldPropagateToCaller()
    {
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        _indexServiceMock
            .Setup(s => s.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuAddedToCartEvent(cartId, skuId)
        };

        var act = () => dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*redis down*");
    }
}
