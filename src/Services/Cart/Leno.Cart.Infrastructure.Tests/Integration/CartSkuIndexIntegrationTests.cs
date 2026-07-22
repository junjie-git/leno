using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Leno.Cart.Infrastructure.Tests.Integration;

/// <summary>
/// 购物车-SKU 反向索引集成测试。
/// 验证 <see cref="CartUnitOfWork.SaveEntitiesAsync"/> 在落库前分发 SkuAddedToCartEvent/SkuRemovedFromCartEvent
/// 到 <see cref="ICartSkuIndexService"/>，保证索引与聚合状态一致。
/// 使用 EF Core InMemory provider 避免依赖真实数据库。
/// </summary>
public class CartSkuIndexIntegrationTests
{
    [Fact]
    public async Task SaveEntitiesAsync_WhenAddItemRaised_ShouldUpdateReverseIndexBeforeCommit()
    {
        // Arrange
        var indexServiceMock = new Mock<ICartSkuIndexService>();
        var capturedAddCalls = new List<(Guid SkuId, Guid CartId)>();
        indexServiceMock
            .Setup(s => s.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CancellationToken>((sku, cart, _) => capturedAddCalls.Add((sku, cart)))
            .Returns(Task.CompletedTask);

        await using var context = CreateInMemoryContext();
        var mapper = new EmptyIntegrationEventMapper();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(indexServiceMock.Object);
        var uow = new CartUnitOfWork(context, mapper, dispatcher);
        var cartRepo = new EfCoreCartRepository(context);

        var userId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid(), userId);
        await cartRepo.AddAsync(cart, default);
        await context.SaveChangesAsync(default);

        // Act
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 1, Guid.NewGuid());
        await uow.SaveEntitiesAsync(default);

        // Assert：领域事件被分发到反向索引
        capturedAddCalls.Should().ContainSingle(c => c.SkuId == skuId && c.CartId == cart.Id);
    }

    [Fact]
    public async Task SaveEntitiesAsync_WhenRemoveItemRaised_ShouldUpdateReverseIndex()
    {
        var indexServiceMock = new Mock<ICartSkuIndexService>();
        var capturedRemoveCalls = new List<(Guid SkuId, Guid CartId)>();
        indexServiceMock
            .Setup(s => s.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CancellationToken>((sku, cart, _) => capturedRemoveCalls.Add((sku, cart)))
            .Returns(Task.CompletedTask);

        await using var context = CreateInMemoryContext();
        var mapper = new EmptyIntegrationEventMapper();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(indexServiceMock.Object);
        var uow = new CartUnitOfWork(context, mapper, dispatcher);
        var cartRepo = new EfCoreCartRepository(context);

        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid(), userId);
        cart.AddItem(skuId, 1, Guid.NewGuid());
        await cartRepo.AddAsync(cart, default);
        await context.SaveChangesAsync(default);
        cart.ClearDomainEvents();

        // Act
        cart.RemoveItem(skuId);
        await uow.SaveEntitiesAsync(default);

        // Assert
        capturedRemoveCalls.Should().ContainSingle(c => c.SkuId == skuId && c.CartId == cart.Id);
    }

    private static CartDbContext CreateInMemoryContext()
    {
        var dbName = $"cart-index-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new CartDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

/// <summary>
/// 测试用空集成事件映射器，对所有领域事件返回 null（不产生 Outbox 消息），
/// 仅用于验证 CartUnitOfWork 路径对 SKU 索引事件分发的处理。
/// </summary>
internal sealed class EmptyIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent) => null;
}
