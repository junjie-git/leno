using Leno.Order.Application.Services;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.ValueObjects;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// <see cref="OrderInternalQueryService"/> 单元测试。
/// 使用 EF Core InMemory provider 构造真实 OrderDbContext + EfCoreOrderRepository，
/// 验证 <see cref="OrderInternalQueryService.GetOrderSellerIdAsync"/> 在订单存在/不存在两种场景下的行为。
/// 测试模式参考 <see cref="ProductUniquenessCheckerTests"/>（InMemory + 真实仓储）。
/// </summary>
public class OrderInternalQueryServiceTests
{
    /// <summary>
    /// 已存在的订单应返回其 SellerId。
    /// </summary>
    [Fact]
    public async Task GetOrderSellerId_ExistingOrder_ReturnsSellerId()
    {
        // 安排：构造 InMemory DbContext + 真实仓储 + 服务实例
        using var ctx = CreateInMemoryContext();
        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = CreateTestOrder(orderId, sellerId);
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var repository = new EfCoreOrderRepository(ctx);
        var sut = new OrderInternalQueryService(repository);

        // 行动
        var result = await sut.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        // 断言：返回与创建时一致的 SellerId
        result.Should().Be(sellerId);
    }

    /// <summary>
    /// 不存在的订单应返回 null（调用方按 NotFound 处理）。
    /// </summary>
    [Fact]
    public async Task GetOrderSellerId_UnknownOrder_ReturnsNull()
    {
        // 安排：空 DbContext + 服务实例
        using var ctx = CreateInMemoryContext();
        var repository = new EfCoreOrderRepository(ctx);
        var sut = new OrderInternalQueryService(repository);

        // 行动：查询不存在的 OrderId
        var result = await sut.GetOrderSellerIdAsync(Guid.NewGuid(), CancellationToken.None);

        // 断言：返回 null
        result.Should().BeNull();
    }

    /// <summary>
    /// 会员订阅订单（SellerId 为 null）应返回 null。
    /// 验证 <see cref="OrderAggregate.SellerId"/> 可空语义被正确传递。
    /// </summary>
    [Fact]
    public async Task GetOrderSellerId_MembershipOrderWithNullSeller_ReturnsNull()
    {
        // 安排：会员订阅订单的 SellerId 为 null
        using var ctx = CreateInMemoryContext();
        var orderId = Guid.NewGuid();
        var order = CreateMembershipOrder(orderId);
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var repository = new EfCoreOrderRepository(ctx);
        var sut = new OrderInternalQueryService(repository);

        // 行动
        var result = await sut.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        // 断言：返回 null（而非 Guid.Empty），保持可空语义
        result.Should().BeNull();
    }

    /// <summary>
    /// 构造 InMemory OrderDbContext，每次测试使用独立数据库名避免串扰。
    /// </summary>
    private static OrderDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase($"order-internal-query-{Guid.NewGuid()}")
            .Options;
        var context = new OrderDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// 构造一个普通订单（已支付态可省略），用于 SellerId 查询测试。
    /// </summary>
    private static OrderAggregate CreateTestOrder(Guid orderId, Guid sellerId)
    {
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var spuId = Guid.NewGuid();

        var snapshot = ProductSnapshot.Create(skuId, spuId, "测试商品", "默认规格", null, sellerId);
        var item = OrderItem.Create(Guid.NewGuid(), skuId, snapshot, 99.9m, 1, null);
        var address = AddressSnapshot.Create("张三", "13800138000", "广东省", "深圳市", "南山区", "科技园路1号");

        return OrderAggregate.Create(
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
    }

    /// <summary>
    /// 构造一个会员订阅订单（SellerId 为 null，OrderType=Membership）。
    /// </summary>
    private static OrderAggregate CreateMembershipOrder(Guid orderId)
    {
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid(); // ProductSnapshot 仍需 SellerId（快照固化卖家），但 Order.SellerId 在 Membership 类型下置 null

        var snapshot = ProductSnapshot.Create(skuId, spuId, "会员套餐", "月度会员", null, sellerId);
        var item = OrderItem.Create(Guid.NewGuid(), skuId, snapshot, 19.9m, 1, null);
        var address = AddressSnapshot.Create("李四", "13900139000", "北京市", "北京市", "海淀区", "中关村大街1号");

        // OrderType.Membership 时传入 sellerId=Guid.Empty，Order.Create 内部会将其转为 null
        return OrderAggregate.Create(
            orderId,
            $"LN{DateTime.UtcNow:yyyyMMddHHmmss}000002",
            OrderType.Membership,
            userId,
            Guid.Empty,
            new List<OrderItem> { item },
            address,
            freightAmount: 0m,
            pointsOffsetAmount: 0m,
            expireAt: DateTime.UtcNow.AddHours(2));
    }
}
