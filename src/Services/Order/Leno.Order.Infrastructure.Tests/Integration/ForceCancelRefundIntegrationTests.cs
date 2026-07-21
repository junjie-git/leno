using FluentAssertions;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Order.Application.DTOs;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Repositories;
using Leno.Order.Infrastructure.Services;
using Leno.SharedContracts.Events;
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
/// ForceCancel 退款流程集成测试：覆盖 OrderAppService.ForceCancelAsync 通过 Outbox 发布 RefundRequestedIntegrationEvent。
/// 依赖 Plan 1 F1.2 已修复 Outbox 模式（不再直接 _eventBus.PublishAsync）。
/// </summary>
public class ForceCancelRefundIntegrationTests : CrossBcIntegrationTestBase<OrderDbContext>
{
    public ForceCancelRefundIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<OrderDbContext>>();
        services.AddScoped<IOrderRepository, EfCoreOrderRepository>();

        // 防腐层 Mock：ForceCancel 调用 ReleaseBatch/ReleaseCoupons/Release 等无返回值方法，Mock.Of 返回 Task.CompletedTask
        services.AddScoped(_ => Mock.Of<IStockReservationDomainService>());
        services.AddScoped(_ => Mock.Of<IPromotionAntiCorruptionService>());
        services.AddScoped(_ => Mock.Of<IPointsAntiCorruptionService>());
        services.AddScoped(_ => Mock.Of<IEventBus>());

        // OrderAppService 其他依赖（ForceCancel 路径不使用，仅为构造函数注入）
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
        // 本测试不注册消费者，仅验证 Outbox 表内有待发布消息
    }

    [Fact]
    public async Task ForceCancelAsync_ShouldWriteRefundEventToOutbox_NotDirectlyPublish()
    {
        // Arrange：创建并插入一个已支付订单
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = CreatePaidOrder(orderId, paymentId);
            seedDb.Orders.Add(order);
            await seedDb.SaveChangesAsync();
        }

        // Act：通过 OrderAppService 调用 ForceCancel（运营强制取消触发退款）
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<OrderAppService>();
        await appService.ForceCancelAsync(
            orderId,
            operatorId: Guid.NewGuid(),
            new ForceCancelOrderDto { Reason = "测试强制取消" },
            CancellationToken.None);

        // Assert：OutboxMessages 表应包含 RefundRequestedIntegrationEvent 记录
        await using var verifyScope = ServiceProvider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var refundEventType = typeof(RefundRequestedIntegrationEvent).FullName;
        var outboxMessage = await verifyDb.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == refundEventType);

        outboxMessage.Should().NotBeNull("ForceCancel 应通过 Outbox 发布 RefundRequestedIntegrationEvent");
        outboxMessage!.ProcessedAt.Should().BeNull("Outbox 消息初始状态应为未发布");

        // Assert：TestHarness 不应直接收到事件（因为未通过 _eventBus.PublishAsync，应由 OutboxPublisher 后台处理）
        using var publishedCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var directPublished = await TestHarness.Published.Any<RefundRequestedIntegrationEvent>(publishedCts.Token);
        directPublished.Should().BeFalse("ForceCancel 不应直接通过 EventBus 发布，应由 OutboxPublisher 后台处理");
    }

    /// <summary>
    /// 构造一个已支付订单：Order.Create 创建待支付态 → MarkAsPaid 转为已支付态。
    /// </summary>
    private static OrderAggregate CreatePaidOrder(Guid orderId, Guid paymentId)
    {
        var sellerId = Guid.NewGuid();
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
