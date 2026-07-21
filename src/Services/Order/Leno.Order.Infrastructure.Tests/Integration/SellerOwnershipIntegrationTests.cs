using FluentAssertions;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Persistence;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Repositories;
using Leno.Order.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using Leno.Testing.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests.Integration;

/// <summary>
/// 卖家越权集成测试：覆盖 OrderAppService.ShipAsync 经 RequireOwnedOrderAsync 校验卖家归属。
/// 依赖 Plan 1 F1.4 已落地越权校验（错误码 ORDER_NOT_OWNED，OrderDomainException）。
/// </summary>
public class SellerOwnershipIntegrationTests : CrossBcIntegrationTestBase<OrderDbContext>
{
    public SellerOwnershipIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<OrderDbContext>>();
        services.AddScoped<IOrderRepository, EfCoreOrderRepository>();

        // 防腐层 Mock：Ship 路径不调用，仅为构造函数注入
        services.AddScoped(_ => Mock.Of<IStockReservationDomainService>());
        services.AddScoped(_ => Mock.Of<IPromotionAntiCorruptionService>());
        services.AddScoped(_ => Mock.Of<IPointsAntiCorruptionService>());
        services.AddScoped(_ => Mock.Of<IEventBus>());
        services.AddScoped(_ => Mock.Of<IOrderNumberGenerator>());
        services.AddScoped(_ => Mock.Of<IOrderPricingDomainService>());
        services.AddScoped(_ => Mock.Of<IFreightCalculator>());
        services.AddScoped(_ => Mock.Of<IProductAntiCorruptionService>());
        services.AddScoped(_ => Mock.Of<ILogisticsTrackingService>());
        services.AddScoped(_ => Mock.Of<ILogisticsCompanyRepository>());
        services.AddScoped(_ => Mock.Of<IOrderSagaOrchestrator>());

        services.AddScoped<OrderAppService>();
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        // 本测试不注册消费者，仅验证应用层归属校验
    }

    [Fact]
    public async Task ShipAsync_WhenOperatorIsNotOwner_ShouldThrowOrderDomainException()
    {
        // Arrange：以归属卖家 sellerId 创建已支付订单
        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = CreatePaidOrder(orderId, paymentId, sellerId);
            seedDb.Orders.Add(order);
            await seedDb.SaveChangesAsync();
        }

        // Act：以非归属卖家调用 ShipAsync
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<OrderAppService>();
        var nonOwnerOperator = Guid.NewGuid();

        var act = async () => await appService.ShipAsync(
            orderId,
            operatorId: nonOwnerOperator,
            new ShipOrderDto { LogisticsNo = "SF001", LogisticsCompanyCode = "SF" },
            CancellationToken.None);

        // Assert：抛 OrderDomainException，错误码 ORDER_NOT_OWNED
        var ex = await act.Should().ThrowAsync<OrderDomainException>(
            "非归属卖家调用 ShipAsync 应被 RequireOwnedOrderAsync 拦截");
        ex.Which.ErrorCode.Should().Be("ORDER_NOT_OWNED", "错误码应为 ORDER_NOT_OWNED");
        ex.Which.Message.Should().Contain("无权操作此订单");
    }

    [Fact]
    public async Task ShipAsync_WhenOperatorIsOwner_ShouldSucceedAndMarkAsShipped()
    {
        // Arrange：以归属卖家 sellerId 创建已支付订单
        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = CreatePaidOrder(orderId, paymentId, sellerId);
            seedDb.Orders.Add(order);
            await seedDb.SaveChangesAsync();
        }

        // Act：以归属卖家调用 ShipAsync
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<OrderAppService>();
        await appService.ShipAsync(
            orderId,
            operatorId: sellerId,
            new ShipOrderDto { LogisticsNo = "SF002", LogisticsCompanyCode = "SF" },
            CancellationToken.None);

        // Assert：订单状态变为 Shipped
        await using var verifyScope = ServiceProvider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var verifyOrder = await verifyDb.Orders.FirstAsync(o => o.Id == orderId);
        verifyOrder.Status.Should().Be(OrderStatus.Shipped, "归属卖家发货后订单应转为 Shipped");
        verifyOrder.LogisticsNo.Should().Be("SF002");
        verifyOrder.LogisticsCompanyCode.Should().Be("SF");
    }

    /// <summary>
    /// 构造一个已支付订单：Order.Create 创建待支付态 → MarkAsPaid 转为已支付态（可发货）。
    /// </summary>
    private static OrderAggregate CreatePaidOrder(Guid orderId, Guid paymentId, Guid sellerId)
    {
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var spuId = Guid.NewGuid();

        var snapshot = ProductSnapshot.Create(skuId, spuId, "测试商品", "默认规格", null, sellerId);
        var item = OrderItem.Create(Guid.NewGuid(), skuId, snapshot, 99.9m, 1, null);
        var address = AddressSnapshot.Create("张三", "13800138000", "广东省", "深圳市", "南山区", "科技园路1号");

        var order = OrderAggregate.Create(
            orderId,
            $"LN{DateTime.UtcNow:yyyyMMddHHmmss}000001",
            OrderType.Normal,
            userId,
            sellerId,
            new List<OrderItem> { item },
            address,
            freightAmount: 0m,
            pointsOffsetAmount: 0m,
            expireAt: DateTime.UtcNow.AddHours(2));

        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(paymentId, "WeChatPay", DateTime.UtcNow, "TEST_TRADE_001", order.TotalAmount);
        return order;
    }
}
