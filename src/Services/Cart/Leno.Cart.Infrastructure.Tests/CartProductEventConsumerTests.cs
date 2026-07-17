using System.Reflection;
using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// Cart 商品事件消费者测试（P0-3）：
/// 验证商品下架/上架/更新事件经反向索引定位购物车后，
/// 正确调用 MarkInvalid/MarkValid/RefreshDisplaySnapshot 同步购物车状态。
/// 使用反射直接调用受保护的 HandleAsync，跳过基类幂等去重，聚焦业务逻辑验证。
/// </summary>
public class CartProductEventConsumerTests
{
    private readonly Mock<ICartRepository> _cartRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IProductSnapshotAntiCorruption> _snapshotAcMock = new();
    private readonly Mock<ICartSkuIndexService> _indexSvcMock = new();
    private readonly Mock<ILogger<ProductTakenDownEventConsumer>> _takenDownLoggerMock = new();
    private readonly Mock<ILogger<ProductPublishedEventConsumer>> _publishedLoggerMock = new();
    private readonly Mock<ILogger<ProductUpdatedEventConsumer>> _updatedLoggerMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyMock = new();

    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CartId1 = Guid.NewGuid();
    private static readonly Guid CartId2 = Guid.NewGuid();

    [Fact]
    public async Task ProductTakenDown_Consumer_ShouldMarkSkuInvalidInAllCarts()
    {
        // Arrange: 反向索引返回 2 个购物车
        _indexSvcMock.Setup(s => s.GetCartIdsBySkuAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1, CartId2 });

        var cart1 = CreateCartWithSku(CartId1, SkuId);
        var cart2 = CreateCartWithSku(CartId2, SkuId);
        _cartRepoMock.Setup(r => r.GetByIdAsync(CartId1, It.IsAny<CancellationToken>())).ReturnsAsync(cart1);
        _cartRepoMock.Setup(r => r.GetByIdAsync(CartId2, It.IsAny<CancellationToken>())).ReturnsAsync(cart2);

        var consumer = new ProductTakenDownEventConsumer(
            _cartRepoMock.Object, _uowMock.Object, _indexSvcMock.Object,
            _takenDownLoggerMock.Object, _idempotencyMock.Object);

        var evt = new ProductTakenDownEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SkuIds = new List<Guid> { SkuId }
        };

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert: 两个购物车的 SKU 都被标记无效
        cart1.Items.First(i => i.SkuId == SkuId).IsValid.Should().BeFalse();
        cart2.Items.First(i => i.SkuId == SkuId).IsValid.Should().BeFalse();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductTakenDown_Consumer_EmptyIndex_ShouldDoNothing()
    {
        // Arrange: 反向索引为空
        _indexSvcMock.Setup(s => s.GetCartIdsBySkuAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var consumer = new ProductTakenDownEventConsumer(
            _cartRepoMock.Object, _uowMock.Object, _indexSvcMock.Object,
            _takenDownLoggerMock.Object, _idempotencyMock.Object);

        var evt = new ProductTakenDownEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SkuIds = new List<Guid> { SkuId }
        };

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert: 不调用仓储与 UnitOfWork
        _cartRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProductPublished_Consumer_ShouldMarkSkuValidInAllCarts()
    {
        // Arrange
        _indexSvcMock.Setup(s => s.GetCartIdsBySkuAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1 });

        var cart = CreateCartWithInvalidSku(CartId1, SkuId);
        _cartRepoMock.Setup(r => r.GetByIdAsync(CartId1, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var consumer = new ProductPublishedEventConsumer(
            _cartRepoMock.Object, _uowMock.Object, _indexSvcMock.Object,
            _publishedLoggerMock.Object, _idempotencyMock.Object);

        var evt = new ProductPublishedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SkuIds = new List<Guid> { SkuId }
        };

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        cart.Items.First(i => i.SkuId == SkuId).IsValid.Should().BeTrue();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductUpdated_Consumer_ShouldRefreshDisplaySnapshot()
    {
        // Arrange
        _indexSvcMock.Setup(s => s.GetCartIdsBySkuAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { CartId1 });

        var newSnapshot = new SkuSnapshotDto { Title = "新标题", MainImageUrl = "new.jpg", UnitPrice = 199m };
        _snapshotAcMock.Setup(a => a.GetSkuSnapshotAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newSnapshot);

        var cart = CreateCartWithSku(CartId1, SkuId);
        _cartRepoMock.Setup(r => r.GetByIdAsync(CartId1, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var consumer = new ProductUpdatedEventConsumer(
            _cartRepoMock.Object, _uowMock.Object, _indexSvcMock.Object, _snapshotAcMock.Object,
            _updatedLoggerMock.Object, _idempotencyMock.Object);

        var evt = new ProductUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = ProductId,
            SkuIds = new List<Guid> { SkuId },
            Title = "新标题"
        };

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert: 购物车项的展示快照已刷新
        cart.Items.First(i => i.SkuId == SkuId).DisplayTitle.Should().Be("新标题");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CartAggregate CreateCartWithSku(Guid cartId, Guid skuId)
    {
        var cart = CartAggregate.Create(cartId, Guid.NewGuid());
        cart.AddItem(skuId, "原标题", "old.jpg", 99m, 1, Guid.NewGuid());
        return cart;
    }

    private static CartAggregate CreateCartWithInvalidSku(Guid cartId, Guid skuId)
    {
        var cart = CreateCartWithSku(cartId, skuId);
        cart.MarkInvalid(skuId, "商品下架");
        return cart;
    }

    private static async Task InvokeHandleAsync<TConsumer, TEvent>(TConsumer consumer, TEvent integrationEvent)
        where TConsumer : class
        where TEvent : class
    {
        var handleMethod = typeof(TConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handleMethod);

        await (Task)handleMethod!.Invoke(consumer, [integrationEvent, CancellationToken.None])!;
    }
}
