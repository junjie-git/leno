using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;

namespace Leno.SellerShop.Domain.Tests;

public class ShopDashboardDataTests
{
    private static readonly Guid ValidShopId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ==================== Create factory ====================

    [Fact]
    public void Create_WithValidShopId_ShouldSetAllPropertiesToZero()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.Id.Should().Be(ValidShopId);
        dashboard.ShopId.Should().Be(ValidShopId);
        dashboard.TotalOrders.Should().Be(0);
        dashboard.PendingOrders.Should().Be(0);
        dashboard.CompletedOrders.Should().Be(0);
        dashboard.TotalRevenue.Should().Be(0m);
        dashboard.Currency.Should().Be("CNY");
        dashboard.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyShopId_ShouldThrow()
    {
        var act = () => ShopDashboardData.Create(Guid.Empty);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("DASHBOARD_SHOP_EMPTY");
    }

    // ==================== OnOrderCreated ====================

    [Fact]
    public void OnOrderCreated_ShouldIncrementTotalAndPending()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.OnOrderCreated();

        dashboard.TotalOrders.Should().Be(1);
        dashboard.PendingOrders.Should().Be(1);
        dashboard.CompletedOrders.Should().Be(0);
        dashboard.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnOrderCreated_Multiple_ShouldAccumulate()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();

        dashboard.TotalOrders.Should().Be(3);
        dashboard.PendingOrders.Should().Be(3);
        dashboard.CompletedOrders.Should().Be(0);
    }

    // ==================== OnOrderPaid ====================

    [Fact]
    public void OnOrderPaid_ShouldIncreaseRevenue()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.OnOrderPaid(100.50m);

        dashboard.TotalRevenue.Should().Be(100.50m);
        dashboard.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnOrderPaid_Multiple_ShouldAccumulate()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.OnOrderPaid(100m);
        dashboard.OnOrderPaid(200m);
        dashboard.OnOrderPaid(50.5m);

        dashboard.TotalRevenue.Should().Be(350.5m);
    }

    [Fact]
    public void OnOrderPaid_WithZeroAmount_ShouldThrow()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);
        var act = () => dashboard.OnOrderPaid(0m);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("DASHBOARD_AMOUNT_INVALID");
    }

    [Fact]
    public void OnOrderPaid_WithNegativeAmount_ShouldThrow()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);
        var act = () => dashboard.OnOrderPaid(-10m);
        var ex = act.Should().Throw<SellerShopDomainException>().Which;
        ex.ErrorCode.Should().Be("DASHBOARD_AMOUNT_INVALID");
    }

    // ==================== OnOrderCancelled ====================

    [Fact]
    public void OnOrderCancelled_ShouldDecrementPending()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();

        dashboard.OnOrderCancelled();

        dashboard.PendingOrders.Should().Be(1);
        dashboard.TotalOrders.Should().Be(2);
        dashboard.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnOrderCancelled_WhenPendingIsZero_ShouldNotGoNegative()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.OnOrderCancelled();

        dashboard.PendingOrders.Should().Be(0);
    }

    [Fact]
    public void OnOrderCancelled_Multiple_ShouldDecrementCorrectly()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();

        dashboard.OnOrderCancelled();
        dashboard.OnOrderCancelled();

        dashboard.PendingOrders.Should().Be(1);
    }

    // ==================== OnOrderCompleted ====================

    [Fact]
    public void OnOrderCompleted_ShouldDecrementPendingAndIncrementCompleted()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();

        dashboard.OnOrderCompleted();

        dashboard.PendingOrders.Should().Be(1);
        dashboard.CompletedOrders.Should().Be(1);
        dashboard.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnOrderCompleted_WhenPendingIsZero_ShouldNotGoNegative()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        dashboard.OnOrderCompleted();

        dashboard.PendingOrders.Should().Be(0);
        dashboard.CompletedOrders.Should().Be(1);
    }

    [Fact]
    public void OnOrderCompleted_Multiple_ShouldAccumulateCorrectly()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();

        dashboard.OnOrderCompleted();
        dashboard.OnOrderCompleted();
        dashboard.OnOrderCompleted();

        dashboard.PendingOrders.Should().Be(1);
        dashboard.CompletedOrders.Should().Be(3);
    }

    // ==================== Full lifecycle ====================

    [Fact]
    public void FullLifecycle_ShouldMaintainConsistency()
    {
        var dashboard = ShopDashboardData.Create(ValidShopId);

        // 创建 5 个订单
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();
        dashboard.OnOrderCreated();

        dashboard.TotalOrders.Should().Be(5);
        dashboard.PendingOrders.Should().Be(5);
        dashboard.CompletedOrders.Should().Be(0);

        // 支付 3 个订单
        dashboard.OnOrderPaid(100m);
        dashboard.OnOrderPaid(200m);
        dashboard.OnOrderPaid(150m);

        dashboard.TotalRevenue.Should().Be(450m);

        // 取消 1 个订单
        dashboard.OnOrderCancelled();
        dashboard.PendingOrders.Should().Be(4);

        // 完成 3 个订单
        dashboard.OnOrderCompleted();
        dashboard.OnOrderCompleted();
        dashboard.OnOrderCompleted();

        dashboard.PendingOrders.Should().Be(1);
        dashboard.CompletedOrders.Should().Be(3);
        dashboard.TotalOrders.Should().Be(5);
        dashboard.TotalRevenue.Should().Be(450m);
    }
}