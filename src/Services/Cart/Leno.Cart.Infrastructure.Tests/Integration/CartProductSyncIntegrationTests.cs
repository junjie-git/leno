using Leno.Cart.Application.Abstractions;
using Leno.Cart.Application.DTOs;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Consumers;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Cart.Infrastructure.Services;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Persistence;
using Leno.Testing.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Tests.Integration;

/// <summary>
/// 购物车商品同步集成测试：覆盖商品域事件经 MassTransit 流转到购物车 BC，
/// 触发 ProductTakenDownEventConsumer/ProductPublishedEventConsumer/ProductUpdatedEventConsumer 同步购物车状态。
/// 依赖 Plan 1 F1.3 已落地 3 个消费者（基于 ProductTakenDownEvent/ProductPublishedEvent/ProductUpdatedEvent 含 SkuIds 列表）。
/// </summary>
public class CartProductSyncIntegrationTests : CrossBcIntegrationTestBase<CartDbContext>
{
    public CartProductSyncIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<CartDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<CartDbContext>>();
        services.AddScoped<ICartRepository, EfCoreCartRepository>();

        // 反向索引：使用真实 Redis 实现（CrossBcIntegrationTestBase 已注册 IConnectionMultiplexer）
        services.AddScoped<ICartSkuIndexService, CartSkuIndexService>();
        services.AddSingleton<ILogger<CartSkuIndexService>>(LoggerFactory.Create(b => b.AddDebug()).CreateLogger<CartSkuIndexService>());

        // 商品快照防腐层 Mock：ProductUpdatedEventConsumer 调用 GetSkuSnapshotAsync 刷新展示
        var snapshotAcMock = new Mock<IProductSnapshotAntiCorruption>();
        snapshotAcMock.Setup(x => x.GetSkuSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid skuId, CancellationToken _) => new SkuSnapshotDto
            {
                SkuId = skuId,
                Title = "更新后的标题",
                MainImageUrl = "https://cdn.example.com/updated.png",
                UnitPrice = 88.8m,
                IsOnSale = true
            });
        services.AddScoped(_ => snapshotAcMock.Object);

        // 消费器日志
        services.AddSingleton<ILogger<ProductTakenDownEventConsumer>>(LoggerFactory.Create(b => b.AddDebug()).CreateLogger<ProductTakenDownEventConsumer>());
        services.AddSingleton<ILogger<ProductPublishedEventConsumer>>(LoggerFactory.Create(b => b.AddDebug()).CreateLogger<ProductPublishedEventConsumer>());
        services.AddSingleton<ILogger<ProductUpdatedEventConsumer>>(LoggerFactory.Create(b => b.AddDebug()).CreateLogger<ProductUpdatedEventConsumer>());
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<ProductTakenDownEventConsumer>();
        configurator.AddConsumer<ProductPublishedEventConsumer>();
        configurator.AddConsumer<ProductUpdatedEventConsumer>();
    }

    [Fact]
    public async Task ProductTakenDownEvent_Published_ShouldMarkCartItemInvalid()
    {
        // Arrange：创建购物车并加入 SKU，写入 DB 与反向索引
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<CartDbContext>();
            var indexService = seedScope.ServiceProvider.GetRequiredService<ICartSkuIndexService>();

            var cart = CartAggregate.Create(cartId, userId);
            cart.AddItem(skuId, quantity: 1, sellerId);
            seedDb.Carts.Add(cart);
            await seedDb.SaveChangesAsync();

            await indexService.AddAsync(skuId, cartId, CancellationToken.None);
        }

        // Act：发布 ProductTakenDownEvent
        await TestHarness.Bus.Publish(new ProductTakenDownEvent
        {
            ProductId = Guid.NewGuid(),
            SellerId = sellerId,
            SkuIds = new List<Guid> { skuId }
        });

        // Assert：消费者收到事件
        using var consumedCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumed = await TestHarness.Consumed.Any<ProductTakenDownEvent>(consumedCts.Token);
        consumed.Should().BeTrue("ProductTakenDownEventConsumer 应消费 ProductTakenDownEvent");

        // Assert：购物车项被标记为无效
        await using var verifyScope = ServiceProvider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CartDbContext>();
        var verifyCart = await verifyDb.Carts.Include(c => c.Items).FirstAsync(c => c.Id == cartId);
        var item = verifyCart.Items.Single(i => i.SkuId == skuId);
        item.IsValid.Should().BeFalse("商品下架后购物车项应标记无效");
        item.InvalidReason.Should().NotBeNullOrEmpty("应记录失效原因");
    }

    [Fact]
    public async Task ProductPublishedEvent_Published_ShouldMarkCartItemValid()
    {
        // Arrange：创建购物车并加入 SKU（标记为无效模拟下架态），写入 DB 与反向索引
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<CartDbContext>();
            var indexService = seedScope.ServiceProvider.GetRequiredService<ICartSkuIndexService>();

            var cart = CartAggregate.Create(cartId, userId);
            cart.AddItem(skuId, quantity: 1, sellerId);
            cart.MarkInvalid(skuId, "商品已下架"); // 预置为下架态
            seedDb.Carts.Add(cart);
            await seedDb.SaveChangesAsync();

            await indexService.AddAsync(skuId, cartId, CancellationToken.None);
        }

        // Act：发布 ProductPublishedEvent
        await TestHarness.Bus.Publish(new ProductPublishedEvent
        {
            ProductId = Guid.NewGuid(),
            SellerId = sellerId,
            SkuIds = new List<Guid> { skuId }
        });

        // Assert：消费者收到事件
        using var consumedCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumed = await TestHarness.Consumed.Any<ProductPublishedEvent>(consumedCts.Token);
        consumed.Should().BeTrue("ProductPublishedEventConsumer 应消费 ProductPublishedEvent");

        // Assert：购物车项被恢复为有效
        await using var verifyScope = ServiceProvider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CartDbContext>();
        var verifyCart = await verifyDb.Carts.Include(c => c.Items).FirstAsync(c => c.Id == cartId);
        var item = verifyCart.Items.Single(i => i.SkuId == skuId);
        item.IsValid.Should().BeTrue("商品上架后购物车项应恢复有效");
        item.InvalidReason.Should().BeNull("恢复有效后失效原因应清空");
    }

    [Fact]
    public async Task ProductUpdatedEvent_Published_ShouldRefreshDisplaySnapshot()
    {
        // Arrange：创建购物车并加入 SKU（携带初始展示快照），写入 DB 与反向索引
        var cartId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<CartDbContext>();
            var indexService = seedScope.ServiceProvider.GetRequiredService<ICartSkuIndexService>();

            var cart = CartAggregate.Create(cartId, userId);
            cart.AddItem(skuId, "原标题", "https://cdn.example.com/origin.png", unitPrice: 99.9m, quantity: 1, sellerId);
            seedDb.Carts.Add(cart);
            await seedDb.SaveChangesAsync();

            await indexService.AddAsync(skuId, cartId, CancellationToken.None);
        }

        // Act：发布 ProductUpdatedEvent
        await TestHarness.Bus.Publish(new ProductUpdatedEvent
        {
            ProductId = Guid.NewGuid(),
            SellerId = sellerId,
            Title = "更新后的标题",
            MainImageUrl = "https://cdn.example.com/updated.png",
            SkuIds = new List<Guid> { skuId }
        });

        // Assert：消费者收到事件
        using var consumedCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumed = await TestHarness.Consumed.Any<ProductUpdatedEvent>(consumedCts.Token);
        consumed.Should().BeTrue("ProductUpdatedEventConsumer 应消费 ProductUpdatedEvent");

        // Assert：购物车项展示快照已刷新
        await using var verifyScope = ServiceProvider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CartDbContext>();
        var verifyCart = await verifyDb.Carts.Include(c => c.Items).FirstAsync(c => c.Id == cartId);
        var item = verifyCart.Items.Single(i => i.SkuId == skuId);
        item.DisplayTitle.Should().Be("更新后的标题", "展示标题应被刷新");
        item.DisplayImageUrl.Should().Be("https://cdn.example.com/updated.png", "展示主图应被刷新");
    }
}
