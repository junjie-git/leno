using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Events;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Tests;

/// <summary>
/// 秒杀订单创建服务单元测试，验证消费 SeckillOrderCreatedIntegrationEvent 后：
/// - SKU 有效时先落库存台账预占，再创建 OrderType.Seckill 订单并追加 SeckillOrderConfirmedDomainEvent 回执；
/// - SKU 不存在或已下架时不创建订单，发布 SeckillOrderCreationFailedIntegrationEvent 失败回执；
/// - 台账预占不足时不创建订单并发失败回执（由 Promotion 回退 Redis 配额）；
/// - 预占成功但订单落库失败时释放台账预占，避免"无主预占"永久占用库存。
/// </summary>
public class SeckillOrderCreationServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IOrderNumberGenerator> _orderNoGenMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAcMock = new();
    private readonly Mock<IInventoryGateway> _inventoryGwMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<IMessageScheduler> _schedulerMock = new();
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
        // 默认：台账预占成功
        _inventoryGwMock.Setup(g => g.ReserveBatchAsync(
                It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.ScheduleSend(
                It.IsAny<Uri>(), It.IsAny<DateTime>(), It.IsAny<OrderTimeoutMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ScheduledMessage<OrderTimeoutMessage>>(null!));

        _sut = new SeckillOrderCreationService(
            _orderRepoMock.Object, _uowMock.Object, _orderNoGenMock.Object,
            _productAcMock.Object, _inventoryGwMock.Object, _eventBusMock.Object,
            _schedulerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSeckillOrderAsync_ValidEvent_ShouldReserveLedgerThenCreateOrderAndScheduleTimeout()
    {
        // Arrange
        var evt = CreateSeckillOrderCreatedEvent();
        _productAcMock.Setup(a => a.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuInfo { SkuId = SkuId, SpuId = SpuId, SellerId = SellerId, ProductName = "秒杀商品", SkuName = "默认", UnitPrice = 99m, IsOnSale = true });

        // Act
        await _sut.CreateSeckillOrderAsync(evt, CancellationToken.None);

        // Assert: 先落库存台账预占（秒杀结算收口：Redis 只做准入，台账为库存权威）
        _inventoryGwMock.Verify(g => g.ReserveBatchAsync(
            OrderId,
            It.Is<Dictionary<Guid, int>>(d => d.Count == 1 && d[SkuId] == evt.Quantity),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert: 订单创建并保存
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Assert: 调度支付超时（未支付到期由 OrderTimeoutDelayMessageConsumer 释放台账预占）
        _schedulerMock.Verify(s => s.ScheduleSend(
            It.Is<Uri>(u => u.ToString() == "queue:order-timeout"),
            It.IsAny<DateTime>(),
            It.Is<OrderTimeoutMessage>(m => m.OrderId == OrderId),
            It.IsAny<CancellationToken>()), Times.Once);

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
    public async Task CreateSeckillOrderAsync_ReserveFailed_ShouldPublishFailedEventAndNotCreateOrder()
    {
        // Arrange: 台账预占不足（配额已放行但库存不足，如普通订单已占用该 SKU）
        var evt = CreateSeckillOrderCreatedEvent();
        _productAcMock.Setup(a => a.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuInfo { SkuId = SkuId, SpuId = SpuId, SellerId = SellerId, ProductName = "秒杀商品", SkuName = "默认", UnitPrice = 99m, IsOnSale = true });
        _inventoryGwMock.Setup(g => g.ReserveBatchAsync(
                It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.CreateSeckillOrderAsync(evt, CancellationToken.None);

        // Assert: 不建单、不调度超时、发失败回执（Promotion 据此回退 Redis 配额与 DB 基线）
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _schedulerMock.Verify(s => s.ScheduleSend(
            It.IsAny<Uri>(), It.IsAny<DateTime>(), It.IsAny<OrderTimeoutMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSeckillOrderAsync_SaveFailed_ShouldReleaseReservationThenPublishFailedEvent()
    {
        // Arrange: 台账预占成功但订单落库失败
        var evt = CreateSeckillOrderCreatedEvent();
        _productAcMock.Setup(a => a.GetSkuInfoAsync(SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkuInfo { SkuId = SkuId, SpuId = SpuId, SellerId = SellerId, ProductName = "秒杀商品", SkuName = "默认", UnitPrice = 99m, IsOnSale = true });
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB 不可用"));

        // Act + Assert: 异常向上抛（供消费者重试）
        var act = async () => await _sut.CreateSeckillOrderAsync(evt, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert: 释放台账预占（避免无主预占永久占用库存）+ 发失败回执 + 不调度超时
        _inventoryGwMock.Verify(g => g.ReleaseBatchAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _schedulerMock.Verify(s => s.ScheduleSend(
            It.IsAny<Uri>(), It.IsAny<DateTime>(), It.IsAny<OrderTimeoutMessage>(), It.IsAny<CancellationToken>()), Times.Never);
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

        // Assert: 不预占、不建单，发布 SeckillOrderCreationFailedIntegrationEvent（经 IEventBus 独立发布，无聚合可挂领域事件）
        _inventoryGwMock.Verify(g => g.ReserveBatchAsync(
            It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        IInventoryGateway? inventoryGateway = null,
        IEventBus? eventBus = null,
        IMessageScheduler? messageScheduler = null,
        ILogger<SeckillOrderCreationService>? logger = null)
    {
        return new SeckillOrderCreationService(
            orderRepository ?? _orderRepoMock.Object,
            unitOfWork ?? _uowMock.Object,
            orderNumberGenerator ?? _orderNoGenMock.Object,
            productAntiCorruption ?? _productAcMock.Object,
            inventoryGateway ?? _inventoryGwMock.Object,
            eventBus ?? _eventBusMock.Object,
            messageScheduler ?? _schedulerMock.Object,
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
