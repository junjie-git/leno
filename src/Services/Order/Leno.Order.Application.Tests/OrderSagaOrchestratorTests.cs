using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
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
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
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
        pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
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
}
