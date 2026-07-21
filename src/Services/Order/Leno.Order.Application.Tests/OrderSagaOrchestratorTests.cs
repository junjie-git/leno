using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using MassTransit.Scheduling;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Application.Tests;

/// <summary>
/// OrderSagaOrchestrator 单元测试，验证 Saga 补偿失败时抛 SagaCompensationFailedException 触发告警而非静默吞掉。
/// </summary>
public class OrderSagaOrchestratorTests
{
    [Fact]
    public async Task CompensateAsync_WhenStockReleaseFails_Should_Throw_SagaCompensationFailedException()
    {
        // Arrange
        var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
            out var stockServiceMock, out var pricingMock, out var freightMock,
            out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

        // 第一组成功预占库存并冻结积分；第二组预占失败触发补偿
        stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // 第一组成功
            .ReturnsAsync(false); // 第二组失败
        pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // 补偿时释放积分正常返回（本测试仅模拟释放库存失败）
        pointsMock.Setup(p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotionMock.Setup(p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-001");
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotionMock.Setup(p => p.CalculateDiscountAsync(It.IsAny<Guid>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        freightMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        orderRepoMock.Setup(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderRepoMock.Setup(r => r.RemoveAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 补偿时释放库存失败（模拟 redis 宕机）
        stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var context = CreateSagaContextWithTwoGroups();

        // Act
        var act = async () => await sut.ExecuteAsync(context, CancellationToken.None);

        // Assert：应抛出 SagaCompensationFailedException 而非静默吞掉
        await act.Should().ThrowAsync<SagaCompensationFailedException>();
    }

    [Fact]
    public async Task CompensateAsync_WhenPointsReleaseFails_Should_Throw_SagaCompensationFailedException()
    {
        // Arrange
        var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
            out var stockServiceMock, out var pricingMock, out var freightMock,
            out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

        stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // 第一组成功
            .ReturnsAsync(false); // 第二组失败
        pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // 补偿时释放积分失败（模拟积分服务宕机）
        pointsMock.Setup(p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("points service down"));
        orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-001");
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotionMock.Setup(p => p.CalculateDiscountAsync(It.IsAny<Guid>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        freightMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        orderRepoMock.Setup(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderRepoMock.Setup(r => r.RemoveAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = CreateSagaContextWithTwoGroups();

        // Act & Assert
        await FluentActions.Invoking(() => sut.ExecuteAsync(context, CancellationToken.None))
            .Should().ThrowAsync<SagaCompensationFailedException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithPointsAndDiscount_Should_Use_Aggregate_Invariants()
    {
        // Arrange：积分抵现 + 优惠，验证 TotalAmount 不为负
        var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
            out var stockServiceMock, out var pricingMock, out var freightMock,
            out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

        var skuInfo = CreateSkuInfo(unitPrice: 100m);
        var checkoutItem = new CheckoutItemDto { SkuId = skuInfo.SkuId, Quantity = 1 };

        stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotionMock.Setup(p => p.CalculateDiscountAsync(It.IsAny<Guid>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(80m); // 优惠 80 元
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pricingMock.Setup(p => p.CalculateAndAllocateAsync(It.IsAny<decimal>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, decimal)> { (skuInfo.SkuId, 80m) });
        freightMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-001");
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 积分抵现原始金额 50 元（超过 ItemsAmount - Discount = 100 - 80 = 20，应被聚合裁剪到 20）
        var context = new OrderSagaContext
        {
            UserId = Guid.NewGuid(),
            Address = CreateTestAddress(),
            Groups = new List<OrderSagaGroupInput>
            {
                new()
                {
                    SellerId = Guid.NewGuid(),
                    Items = new List<CheckoutItemDto> { checkoutItem },
                    SkuInfos = new Dictionary<Guid, SkuInfo> { { skuInfo.SkuId, skuInfo } },
                    GroupPointsOffsetRaw = 50m,
                    UsePoints = true
                }
            }
        };

        OrderAggregate capturedOrder = null!;
        orderRepoMock.Setup(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<OrderAggregate, CancellationToken>((o, _) => capturedOrder = o)
            .Returns(Task.CompletedTask);

        // Act
        await sut.ExecuteAsync(context, CancellationToken.None);

        // Assert：积分抵现应被聚合根裁剪到 ItemsAmount - DiscountAmount = 20
        capturedOrder.PointsOffsetAmount.Should().Be(20m);
        capturedOrder.DiscountAmount.Should().Be(80m);
        // TotalAmount = 100 - 80 - 20 + 10 = 10，不为负
        capturedOrder.TotalAmount.Should().Be(10m);
    }

    [Fact]
    public async Task ExecuteAsync_AllSuccess_Should_Schedule_Timeout_After_SaveEntitiesAsync()
    {
        // Arrange
        var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
            out var stockServiceMock, out var pricingMock, out var freightMock,
            out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

        stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        promotionMock.Setup(p => p.CalculateDiscountAsync(It.IsAny<Guid>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        freightMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-001");
        orderRepoMock.Setup(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 记录 SaveEntitiesAsync 与 ScheduleSend（通过 bus.Publish<ScheduleMessage>）的调用顺序
        var callOrder = new List<string>();
        uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => { callOrder.Add("SaveEntitiesAsync"); return Task.CompletedTask; });
        busMock.Setup(b => b.Publish(It.IsAny<ScheduleMessage>(), It.IsAny<IPipe<PublishContext<ScheduleMessage>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("ScheduleSend"));

        var context = CreateSagaContextWithSingleGroup();

        // Act
        await sut.ExecuteAsync(context, CancellationToken.None);

        // Assert：SaveEntitiesAsync 应在 ScheduleSend 之前执行（保证订单已持久化后再调度延迟消息）
        callOrder.IndexOf("SaveEntitiesAsync").Should().BeLessThan(callOrder.IndexOf("ScheduleSend"));
    }

    [Fact]
    public async Task ExecuteAsync_SecondGroupFails_Should_Not_Schedule_Timeout()
    {
        // Arrange
        var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
            out var stockServiceMock, out var pricingMock, out var freightMock,
            out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

        stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // 第一组成功
            .ReturnsAsync(false); // 第二组失败
        pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // 补偿时各释放动作正常返回（保证 Saga 失败时补偿成功，原始异常向上抛）
        pointsMock.Setup(p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotionMock.Setup(p => p.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-001");
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<IReadOnlyDictionary<Guid, decimal>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        promotionMock.Setup(p => p.CalculateDiscountAsync(It.IsAny<Guid>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        freightMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        orderRepoMock.Setup(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        orderRepoMock.Setup(r => r.RemoveAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = CreateSagaContextWithTwoGroups();

        // Act
        var act = async () => await sut.ExecuteAsync(context, CancellationToken.None);

        // Assert：Saga 失败时不应调度任何超时消息（ScheduleSend 在 SaveEntitiesAsync 之后，Saga 失败未到达 SaveEntitiesAsync）
        await act.Should().ThrowAsync<OrderDomainException>();
        busMock.Verify(
            b => b.Publish(It.IsAny<ScheduleMessage>(), It.IsAny<IPipe<PublishContext<ScheduleMessage>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// 构造 OrderSagaOrchestrator 被测对象，并通过 out 参数返回各依赖 Mock。
    /// </summary>
    private static IOrderSagaOrchestrator CreateSut(
        out Mock<IOrderRepository> orderRepoMock,
        out Mock<IUnitOfWork> uowMock,
        out Mock<IOrderNumberGenerator> orderNoGenMock,
        out Mock<IStockReservationDomainService> stockServiceMock,
        out Mock<IOrderPricingDomainService> pricingMock,
        out Mock<IFreightCalculator> freightMock,
        out Mock<IPromotionAntiCorruptionService> promotionMock,
        out Mock<IPointsAntiCorruptionService> pointsMock,
        out Mock<IBus> busMock,
        out Mock<ILogger<OrderSagaOrchestrator>> loggerMock)
    {
        orderRepoMock = new Mock<IOrderRepository>();
        uowMock = new Mock<IUnitOfWork>();
        orderNoGenMock = new Mock<IOrderNumberGenerator>();
        stockServiceMock = new Mock<IStockReservationDomainService>();
        pricingMock = new Mock<IOrderPricingDomainService>();
        freightMock = new Mock<IFreightCalculator>();
        promotionMock = new Mock<IPromotionAntiCorruptionService>();
        pointsMock = new Mock<IPointsAntiCorruptionService>();
        busMock = new Mock<IBus>();
        loggerMock = new Mock<ILogger<OrderSagaOrchestrator>>();

        return new OrderSagaOrchestrator(
            orderRepoMock.Object,
            uowMock.Object,
            orderNoGenMock.Object,
            stockServiceMock.Object,
            pricingMock.Object,
            freightMock.Object,
            promotionMock.Object,
            pointsMock.Object,
            busMock.Object,
            loggerMock.Object);
    }

    /// <summary>
    /// 构造包含两个卖家分组的 Saga 上下文：第一组下单成功后会进入补偿列表，第二组触发失败。
    /// 每组启用积分抵现以触发积分冻结路径，便于补偿阶段覆盖积分释放。
    /// </summary>
    private static OrderSagaContext CreateSagaContextWithTwoGroups()
    {
        var userId = Guid.NewGuid();
        var address = AddressSnapshot.Create("李四", "13900139000", "广东", "深圳", "南山区", "科技园路2号");

        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var seller1 = Guid.NewGuid();
        var seller2 = Guid.NewGuid();

        var skuInfos = new Dictionary<Guid, SkuInfo>
        {
            [sku1] = new SkuInfo
            {
                SkuId = sku1,
                SpuId = Guid.NewGuid(),
                SellerId = seller1,
                ProductName = "商品A",
                SkuName = "规格A1",
                MainImage = null,
                UnitPrice = 99.99m,
                AvailableQty = 10,
                IsOnSale = true
            },
            [sku2] = new SkuInfo
            {
                SkuId = sku2,
                SpuId = Guid.NewGuid(),
                SellerId = seller2,
                ProductName = "商品B",
                SkuName = "规格B1",
                MainImage = null,
                UnitPrice = 50m,
                AvailableQty = 10,
                IsOnSale = true
            }
        };

        var groups = new List<OrderSagaGroupInput>
        {
            new()
            {
                SellerId = seller1,
                Items = new List<CheckoutItemDto>
                {
                    new() { SkuId = sku1, Quantity = 1 }
                },
                SkuInfos = skuInfos,
                GroupPointsOffsetRaw = 5m,
                UsePoints = true
            },
            new()
            {
                SellerId = seller2,
                Items = new List<CheckoutItemDto>
                {
                    new() { SkuId = sku2, Quantity = 1 }
                },
                SkuInfos = skuInfos,
                GroupPointsOffsetRaw = 3m,
                UsePoints = true
            }
        };

        return new OrderSagaContext
        {
            UserId = userId,
            Address = address,
            Groups = groups
        };
    }

    /// <summary>
    /// 构造包含单个卖家分组的 Saga 上下文，不使用积分抵现，用于验证全部成功场景下
    /// 延迟消息调度在 SaveEntitiesAsync 之后执行。
    /// </summary>
    private static OrderSagaContext CreateSagaContextWithSingleGroup()
    {
        var userId = Guid.NewGuid();
        var address = AddressSnapshot.Create("李四", "13900139000", "广东", "深圳", "南山区", "科技园路2号");

        var sku1 = Guid.NewGuid();
        var seller1 = Guid.NewGuid();

        var skuInfos = new Dictionary<Guid, SkuInfo>
        {
            [sku1] = new SkuInfo
            {
                SkuId = sku1,
                SpuId = Guid.NewGuid(),
                SellerId = seller1,
                ProductName = "商品A",
                SkuName = "规格A1",
                MainImage = null,
                UnitPrice = 99.99m,
                AvailableQty = 10,
                IsOnSale = true
            }
        };

        var groups = new List<OrderSagaGroupInput>
        {
            new()
            {
                SellerId = seller1,
                Items = new List<CheckoutItemDto>
                {
                    new() { SkuId = sku1, Quantity = 1 }
                },
                SkuInfos = skuInfos,
                GroupPointsOffsetRaw = 0m,
                UsePoints = false
            }
        };

        return new OrderSagaContext
        {
            UserId = userId,
            Address = address,
            Groups = groups
        };
    }

    /// <summary>
    /// 构造指定单价的 SkuInfo，自动生成 SkuId/SpuId/SellerId。
    /// </summary>
    private static SkuInfo CreateSkuInfo(decimal unitPrice)
    {
        return new SkuInfo
        {
            SkuId = Guid.NewGuid(),
            SpuId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            ProductName = "测试商品",
            SkuName = "默认规格",
            MainImage = null,
            UnitPrice = unitPrice,
            AvailableQty = 100,
            IsOnSale = true
        };
    }

    /// <summary>
    /// 构造测试用收货地址快照。
    /// </summary>
    private static AddressSnapshot CreateTestAddress()
    {
        return AddressSnapshot.Create("测试用户", "13800138000", "广东", "深圳", "南山区", "科技园路1号");
    }
}
