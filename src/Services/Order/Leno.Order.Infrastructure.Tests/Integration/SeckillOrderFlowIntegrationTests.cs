using FluentAssertions;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Consumers;
using Leno.Order.Infrastructure.Repositories;
using Leno.Order.Infrastructure.Services;
using Leno.Promotion.Domain.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Testing.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Integration;

/// <summary>
/// 秒杀下单全流程集成测试：覆盖 Promotion 发布 SeckillOrderCreatedEvent → Order BC 消费 → 创建订单。
/// 依赖 Plan 1 F1.1 已补建 SeckillOrderCreatedEventConsumer。
/// </summary>
public class SeckillOrderFlowIntegrationTests : CrossBcIntegrationTestBase<OrderDbContext>
{
    public SeckillOrderFlowIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();

        // 防腐层 Mock：返回测试用 SKU 信息（IsOnSale=true 使秒杀订单创建流程通过校验）
        var productAcMock = new Mock<IProductAntiCorruptionService>();
        productAcMock.Setup(x => x.GetSkuInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid skuId, CancellationToken _) => new SkuInfo
            {
                SkuId = skuId,
                SpuId = Guid.NewGuid(),
                SellerId = Guid.NewGuid(),
                ProductName = "秒杀测试商品",
                SkuName = "默认规格",
                MainImage = null,
                UnitPrice = 99.9m,
                AvailableQty = 100,
                IsOnSale = true
            });
        services.AddScoped(_ => productAcMock.Object);

        services.AddScoped<SeckillOrderCreationService>();
        services.AddScoped<SeckillOrderCreatedEventConsumer>();
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<SeckillOrderCreatedEventConsumer>();
    }

    [Fact]
    public async Task SeckillOrderCreatedEvent_Published_ShouldCreateOrderInOrderDbContext()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var expectedOrderId = Guid.NewGuid();
        var seckillPrice = 99.9m;
        var quantity = 1;

        // Act：发布 SeckillOrderCreatedEvent 到 TestHarness
        await TestHarness.Bus.Publish(new SeckillOrderCreatedEvent(
            activityId, spuId, skuId, userId, expectedOrderId, seckillPrice, quantity));

        // Assert：消费者收到事件
        using var consumedCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumed = await TestHarness.Consumed.Any<SeckillOrderCreatedEvent>(consumedCts.Token);
        consumed.Should().BeTrue("SeckillOrderCreatedEventConsumer 应消费 SeckillOrderCreatedEvent");

        // Assert：订单已创建
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == expectedOrderId);
        order.Should().NotBeNull("Order BC 应创建秒杀订单");
        order!.OrderType.Should().Be(OrderType.Seckill, "秒杀订单 OrderType 应为 Seckill");
    }
}
