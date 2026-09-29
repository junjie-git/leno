using FluentAssertions;
using Grpc.Net.Client;
using Leno.Infrastructure.Persistence;
using Leno.Inventory.Application;
using Leno.Inventory.Application.Services;
using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Repositories;
using Leno.Inventory.Infrastructure;
using Leno.Inventory.Infrastructure.Consumers;
using Leno.Inventory.Infrastructure.Repositories;
using Leno.Order.Application.Abstractions;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.AntiCorruption;
using Leno.Order.Infrastructure.Consumers;
using Leno.Order.Infrastructure.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedContracts.Grpc.Inventory.V1;
using Leno.SharedContracts.Integration.Inventory;
using ReserveStockItem = Leno.SharedContracts.Integration.Inventory.ReserveStockItem;
using Leno.SharedKernel.Abstractions;
using Leno.Testing.Fixtures;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Inventory.Infrastructure.Tests.Integration;

/// <summary>
/// F8 核心链路段②：支付成功 → 库存确认（跨 BC 真实集成）。
/// 链路：ReserveStockCommand（下单预占）→ 台账 Reserved
///     → PaymentSucceededEvent → Order BC StockConfirmConsumer（真实）
///     → GrpcInventoryGateway.ConfirmBatchAsync（真实，confirm 路径经 MassTransit 发布命令）
///     → Inventory BC ConfirmStockCommandConsumer（真实）
///     → InventoryAppService.ConfirmAsync → 台账 Reserved→Confirmed + 基线扣减。
/// 仅 gRPC 同步预占通道不在此覆盖（预占经同款 ReserveStockCommand 命令路径验证）。
/// </summary>
public class PaymentToStockConfirmFlowIntegrationTests : CrossBcIntegrationTestBase<InventoryDbContext>
{
    public PaymentToStockConfirmFlowIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        // ===== Inventory 侧（库存权威）=====
        services.AddDbContext<InventoryDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<InventoryDbContext>>();
        services.AddScoped<IStockReservationRepository, EfCoreStockReservationRepository>();
        services.AddScoped<IStockBaselineRepository, EfCoreStockBaselineRepository>();
        services.AddScoped<IInventoryAppService, InventoryAppService>();
        services.AddScoped<ReserveStockCommandConsumer>();
        services.AddScoped<ConfirmStockCommandConsumer>();

        // ===== Order 侧（触发方）=====
        // 独立数据库：生产按 BC 分库，两库各自的 outbox_messages 同名表不可共存于同一库
        var orderSqlConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlConnectionString)
        {
            InitialCatalog = "leno_order_f8_it"
        }.ConnectionString;
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(orderSqlConnectionString));
        services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
        services.AddScoped<StockConfirmConsumer>();

        // 真实网关实现：ConfirmBatchAsync 仅经 IBus 发布 ConfirmStockCommand（gRPC 通道在本链路不触达，
        // 指向不可达地址以在误用时快速失败而非静默悬挂）。
        // 注意 IIdempotencyStore 由基类注册（真实 Redis），StockConfirmConsumer 的
        // stock-confirm-{PaymentId} 幂等键真实生效。
        services.AddSingleton<IInventoryGateway>(sp =>
        {
            var channel = GrpcChannel.ForAddress("http://localhost:9901");
            var client = new InventoryInternalService.InventoryInternalServiceClient(channel);
            return new GrpcInventoryGateway(
                client,
                sp.GetRequiredService<IBus>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<GrpcInventoryGateway>());
        });
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<ReserveStockCommandConsumer>();
        configurator.AddConsumer<ConfirmStockCommandConsumer>();
        configurator.AddConsumer<StockConfirmConsumer>();
    }

    [Fact]
    public async Task ReserveThenPaymentSucceeded_ShouldTransitionLedgerToConfirmed_AndDeductBaseline()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        const int quantity = 2;

        // Order 侧库迁移（基类仅迁移 TDbContext=InventoryDbContext；迁移幂等且带分布式锁）
        await ServiceProvider.MigrateWithLockAsync<OrderDbContext>();
        await SeedBaselineAsync(skuId, initialQty: 100);
        await SeedPaidOrderAsync(orderId, paymentId, userId, skuId, quantity);

        // Act 1：下单预占（Inventory 按订单写台账 Reserved）
        await TestHarness.Bus.Publish(new ReserveStockCommand(
            orderId, [new ReserveStockItem(skuId, quantity)], Guid.NewGuid()));

        using var reserveCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        (await TestHarness.Consumed.Any<ReserveStockCommand>(reserveCts.Token))
            .Should().BeTrue("ReserveStockCommandConsumer 应消费预占命令");
        await PollUntilAsync(
            async () => (await GetReservationStatusAsync(orderId, skuId)) == StockReservationStatus.Reserved,
            "预占后台账应写入 Reserved 态");

        // Act 2：支付成功事件（跨 BC 触发库存确认）
        await TestHarness.Bus.Publish(new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = orderId,
            PaymentId = paymentId,
            UserId = userId,
            Channel = "WeChatPay",
            TradeNo = "TEST_TRADE_F8_001",
            Amount = 199.8m
        });

        using var confirmCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        (await TestHarness.Consumed.Any<PaymentSucceededEvent>(confirmCts.Token))
            .Should().BeTrue("StockConfirmConsumer 应消费支付成功事件");
        (await TestHarness.Consumed.Any<ConfirmStockCommand>(confirmCts.Token))
            .Should().BeTrue("GrpcInventoryGateway.ConfirmBatchAsync 应经总线发布确认命令");

        // Assert：台账 Reserved→Confirmed + 基线扣减（异步两跳，轮询等待）
        await PollUntilAsync(
            async () => (await GetReservationStatusAsync(orderId, skuId)) == StockReservationStatus.Confirmed,
            "支付成功后台账应迁移为 Confirmed");

        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var baseline = await db.StockBaselines.FirstAsync(b => b.SkuId == skuId);
        baseline.DeductedQty.Should().Be(quantity, "确认后基线已扣减数量应等于预占数量");
        baseline.AvailableQty.Should().Be(100 - quantity, "确认后可用库存应扣减");
    }

    [Fact]
    public async Task ReplayPaymentSucceeded_ShouldStayConfirmed_WithoutDoubleDeduction()
    {
        // Arrange：同 Test1 完成预占+确认
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        const int quantity = 1;

        await ServiceProvider.MigrateWithLockAsync<OrderDbContext>();
        await SeedBaselineAsync(skuId, initialQty: 50);
        await SeedPaidOrderAsync(orderId, paymentId, userId, skuId, quantity);

        await TestHarness.Bus.Publish(new ReserveStockCommand(
            orderId, [new ReserveStockItem(skuId, quantity)], Guid.NewGuid()));
        await PollUntilAsync(
            async () => (await GetReservationStatusAsync(orderId, skuId)) == StockReservationStatus.Reserved,
            "预占后台账应写入 Reserved 态");

        await TestHarness.Bus.Publish(new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = orderId,
            PaymentId = paymentId,
            UserId = userId,
            Channel = "WeChatPay",
            TradeNo = "TEST_TRADE_F8_002",
            Amount = 99.9m
        });
        await PollUntilAsync(
            async () => (await GetReservationStatusAsync(orderId, skuId)) == StockReservationStatus.Confirmed,
            "首次支付确认后台账应为 Confirmed");

        // Act：同一支付单重放支付成功事件（Outbox 重发/重试场景）
        // StockConfirmConsumer 幂等键 stock-confirm-{PaymentId} 应跳过重复确认
        await TestHarness.Bus.Publish(new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = orderId,
            PaymentId = paymentId,
            UserId = userId,
            Channel = "WeChatPay",
            TradeNo = "TEST_TRADE_F8_002",
            Amount = 99.9m
        });
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert：台账仍 Confirmed，基线无二次扣减
        (await GetReservationStatusAsync(orderId, skuId)).Should().Be(StockReservationStatus.Confirmed);
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var baseline = await db.StockBaselines.FirstAsync(b => b.SkuId == skuId);
        baseline.DeductedQty.Should().Be(quantity, "重放不得二次扣减基线");
        baseline.AvailableQty.Should().Be(50 - quantity);
    }

    #region 种子与断言辅助

    private async Task SeedBaselineAsync(Guid skuId, int initialQty)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        db.StockBaselines.Add(StockBaseline.Create(Guid.NewGuid(), skuId, initialQty, Guid.NewGuid()));
        await db.SaveChangesAsync();
    }

    private async Task SeedPaidOrderAsync(Guid orderId, Guid paymentId, Guid userId, Guid skuId, int quantity)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var sellerId = Guid.NewGuid();

        var snapshot = ProductSnapshot.Create(skuId, Guid.NewGuid(), "F8 链路测试商品", "默认规格", null, sellerId);
        var item = OrderItem.Create(Guid.NewGuid(), skuId, snapshot, 99.9m, quantity, null);
        var address = AddressSnapshot.Create("张三", "+8613800138000", "广东省", "深圳市", "南山区", "科技园路 1 号 A 栋 1001 室");

        var order = OrderAggregate.Create(
            orderId,
            $"LN{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(1000, 9999)}",
            OrderType.Normal,
            userId,
            sellerId,
            new List<OrderItem> { item },
            address,
            freightAmount: 0m,
            pointsOffsetAmount: 0m,
            expireAt: DateTime.UtcNow.AddHours(2));

        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(paymentId, "WeChatPay", DateTime.UtcNow, $"TRADE_{paymentId:N}", order.TotalAmount);

        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }

    private async Task<StockReservationStatus> GetReservationStatusAsync(Guid orderId, Guid skuId)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var entry = await db.StockReservations.FirstOrDefaultAsync(r => r.OrderId == orderId && r.SkuId == skuId);
        return entry?.Status ?? throw new InvalidOperationException("台账条目尚未创建");
    }

    private static async Task PollUntilAsync(Func<Task<bool>> condition, string because, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition()) return;
            }
            catch (InvalidOperationException)
            {
                // 条目尚未出现：继续轮询
            }

            await Task.Delay(200);
        }

        (await condition()).Should().BeTrue(because);
    }

    #endregion
}
