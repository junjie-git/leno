using Leno.Infrastructure.Abstractions;
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
using System.Reflection;
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
    private readonly Mock<IEventBus> _eventBusMock = new();
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
            _productAcMock.Object, _eventBusMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSeckillOrderAsync_ValidEvent_ShouldCreateOrderAndPublishConfirmedEvent()
    {
        // Arrange
        var evt = CreateSeckillOrderCreatedEvent();
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
        var evt = CreateSeckillOrderCreatedEvent();
        _productAcMock.Setup(a => a.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SkuInfo?)null);

        // Act
        await _sut.CreateSeckillOrderAsync(evt, CancellationToken.None);

        // Assert: 不创建订单，发布 SeckillOrderCreationFailedIntegrationEvent（经 IEventBus 独立发布，无聚合可挂领域事件）
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishFailedEvent_OnSuccess_PublishesEventWithCorrectFields()
    {
        // Arrange：构造 mock IEventBus 捕获发布的事件实例
        var eventBus = new Mock<IEventBus>();
        SeckillOrderCreationFailedIntegrationEvent? publishedEvent = null;
        eventBus.Setup(e => e.PublishAsync(It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SeckillOrderCreationFailedIntegrationEvent, CancellationToken>((evt, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        var sut = CreateService(eventBus: eventBus.Object);
        var evt = CreateSeckillOrderCreatedEvent();

        // Act：通过反射调用 private PublishFailedEventAsync
        var method = typeof(SeckillOrderCreationService).GetMethod(
            "PublishFailedEventAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(sut, new object[] { evt, "测试原因", CancellationToken.None })!;
        await task;

        // Assert：PublishAsync 被调用一次且事件字段正确
        eventBus.Verify(e => e.PublishAsync(
            It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
        publishedEvent.Should().NotBeNull();
        publishedEvent!.OrderId.Should().Be(evt.OrderId);
        publishedEvent.SkuId.Should().Be(evt.SkuId);
        publishedEvent.UserId.Should().Be(evt.UserId);
        publishedEvent.ActivityId.Should().Be(evt.ActivityId);
        publishedEvent.Quantity.Should().Be(evt.Quantity);
        publishedEvent.Reason.Should().Be("测试原因");
    }

    [Fact]
    public async Task PublishFailedEvent_OnPublishFailure_DoesNotRethrow()
    {
        // Arrange：mock IEventBus 抛异常，模拟 MQ 不可达
        var eventBus = new Mock<IEventBus>();
        eventBus.Setup(e => e.PublishAsync(It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MQ 不可达"));

        var sut = CreateService(eventBus: eventBus.Object);
        var evt = CreateSeckillOrderCreatedEvent();

        var method = typeof(SeckillOrderCreationService).GetMethod(
            "PublishFailedEventAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act + Assert：不应抛出（避免吞掉原始创建异常）
        var act = async () =>
        {
            var task = (Task)method!.Invoke(sut, new object[] { evt, "测试原因", CancellationToken.None })!;
            await task;
        };
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// 构造 SeckillOrderCreationService 实例，允许覆盖特定依赖。
    /// </summary>
    private SeckillOrderCreationService CreateService(
        IOrderRepository? orderRepository = null,
        IUnitOfWork? unitOfWork = null,
        IOrderNumberGenerator? orderNumberGenerator = null,
        IProductAntiCorruptionService? productAntiCorruption = null,
        IEventBus? eventBus = null,
        ILogger<SeckillOrderCreationService>? logger = null)
    {
        return new SeckillOrderCreationService(
            orderRepository ?? _orderRepoMock.Object,
            unitOfWork ?? _uowMock.Object,
            orderNumberGenerator ?? _orderNoGenMock.Object,
            productAntiCorruption ?? _productAcMock.Object,
            eventBus ?? _eventBusMock.Object,
            logger ?? _loggerMock.Object);
    }

    /// <summary>
    /// 构造标准测试输入事件，使用本测试类固定的 Guid 常量。
    /// </summary>
    private static SeckillOrderCreatedIntegrationEvent CreateSeckillOrderCreatedEvent()
    {
        return new SeckillOrderCreatedIntegrationEvent(
            activityId: ActivityId,
            spuId: SpuId,
            skuId: SkuId,
            userId: UserId,
            orderId: OrderId,
            seckillPrice: 99m,
            quantity: 1);
    }
}
