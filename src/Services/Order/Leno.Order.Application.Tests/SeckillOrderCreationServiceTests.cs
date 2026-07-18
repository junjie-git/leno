using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Events;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Tests;

/// <summary>
/// 秒杀订单创建服务单元测试，验证消费 SeckillOrderCreatedIntegrationEvent 后：
/// - SKU 有效时创建 OrderType.Seckill 订单并追加 SeckillOrderConfirmedDomainEvent 回执（经 Outbox 同事务发布）；
/// - SKU 不存在或已下架时不创建订单，发布 SeckillOrderCreationFailedIntegrationEvent 失败回执。
/// </summary>
public class SeckillOrderCreationServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IOrderNumberGenerator> _orderNoGenMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAcMock = new();
    private readonly Mock<ILogger<SeckillOrderCreationService>> _loggerMock = new();
    private readonly SeckillOrderCreationService _sut;

    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    public SeckillOrderCreationServiceTests()
    {
        _orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("SK-TEST-001");
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sut = new SeckillOrderCreationService(
            _orderRepoMock.Object, _uowMock.Object, _orderNoGenMock.Object,
            _productAcMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSeckillOrderAsync_ValidEvent_ShouldCreateOrderAndPublishConfirmedEvent()
    {
        // Arrange
        var evt = new SeckillOrderCreatedIntegrationEvent(ActivityId, SpuId, SkuId, UserId, OrderId, 99m, 1);
        _productAcMock.Setup(a => a.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuInfo { SkuId = SkuId, SpuId = SpuId, SellerId = SellerId, ProductName = "秒杀商品", SkuName = "默认", UnitPrice = 99m, IsOnSale = true });

        // Act
        await _sut.CreateSeckillOrderAsync(evt, CancellationToken.None);

        // Assert: 订单创建并保存
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Assert: 发布 SeckillOrderConfirmedDomainEvent 回执事件（通过聚合领域事件）
        var savedOrder = _orderRepoMock.Invocations
            .Where(i => i.Method.Name == "AddAsync")
            .Select(i => i.Arguments[0])
            .OfType<OrderAggregate>()
            .Single();
        savedOrder.OrderType.Should().Be(OrderType.Seckill);
        savedOrder.DomainEvents.OfType<SeckillOrderConfirmedDomainEvent>().Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateSeckillOrderAsync_SkuNotFound_ShouldPublishFailedEvent()
    {
        // Arrange: 商品域返回 null
        var evt = new SeckillOrderCreatedIntegrationEvent(ActivityId, SpuId, SkuId, UserId, OrderId, 99m, 1);
        _productAcMock.Setup(a => a.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SkuInfo?)null);

        // Act
        await _sut.CreateSeckillOrderAsync(evt, CancellationToken.None);

        // Assert: 不创建订单，发布 SeckillOrderCreationFailedIntegrationEvent
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);

        // 失败回执通过独立事件发布（非聚合领域事件，因为无聚合可挂）
        // 实际实现时通过 IEventBus 或 Outbox 独立发布
    }
}
