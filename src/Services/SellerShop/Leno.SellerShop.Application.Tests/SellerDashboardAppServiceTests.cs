using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.ValueObjects;
using Leno.SharedKernel.ValueObjects;
using Moq;

namespace Leno.SellerShop.Application.Tests;

/// <summary>
/// 卖家工作台应用服务单元测试，覆盖看板聚合与销售趋势/指标查询用例。
/// </summary>
public class SellerDashboardAppServiceTests
{
    private readonly Mock<IShopRepository> _shopRepoMock = new();
    private readonly Mock<IShopMetricsRepository> _metricsRepoMock = new();
    private readonly Mock<IShopDashboardRepository> _dashboardRepoMock = new();
    private readonly SellerDashboardAppService _sut;

    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ShopId = Guid.NewGuid();

    public SellerDashboardAppServiceTests()
    {
        _sut = new SellerDashboardAppService(
            _shopRepoMock.Object,
            _metricsRepoMock.Object,
            _dashboardRepoMock.Object);
    }

    [Fact]
    public async Task GetDashboardAsync_EmptySellerId_ShouldThrowDomainException()
    {
        var act = () => _sut.GetDashboardAsync(Guid.Empty);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*卖家账号标识不可为空*");
    }

    [Fact]
    public async Task GetDashboardAsync_ShopNotFound_ShouldThrowDomainException()
    {
        _shopRepoMock
            .Setup(r => r.GetBySellerIdAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shop?)null);

        var act = () => _sut.GetDashboardAsync(SellerId);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*店铺不存在*");
    }

    [Fact]
    public async Task GetDashboardAsync_ExistingShopWithMetrics_ShouldReturnAggregatedDto()
    {
        var shop = Shop.Create(ShopId, SellerId, "Leno 旗舰店", "13800000000");
        shop.Approve(Guid.NewGuid());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _shopRepoMock
            .Setup(r => r.GetBySellerIdAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);
        _metricsRepoMock
            .Setup(r => r.GetByShopIdAsync(ShopId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopMetrics?)null);
        _dashboardRepoMock
            .Setup(r => r.GetByShopIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopDashboardData?)null);

        var result = await _sut.GetDashboardAsync(SellerId);

        result.ShopId.Should().Be(ShopId);
        result.ShopName.Should().Be("Leno 旗舰店");
        result.Status.Should().Be(ShopStatus.Active);
        result.TotalOrders.Should().Be(0);
        result.TodayOrderCount.Should().Be(0);
        result.TodaySalesAmount.Should().Be(0m);
        result.TodaySalesCurrency.Should().Be("CNY");
    }

    [Fact]
    public async Task GetSalesTrendAsync_InvalidRange_ShouldThrowDomainException()
    {
        var fromDate = new DateOnly(2026, 7, 18);
        var toDate = new DateOnly(2026, 7, 17);

        var act = () => _sut.GetSalesTrendAsync(ShopId, fromDate, toDate);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*起始日期不可晚于结束日期*");
    }

    [Fact]
    public async Task GetSalesTrendAsync_ValidRange_ShouldReturnTrendList()
    {
        var fromDate = new DateOnly(2026, 7, 10);
        var toDate = new DateOnly(2026, 7, 12);
        var metrics = new List<ShopMetrics>
        {
            BuildMetrics(fromDate),
            BuildMetrics(fromDate.AddDays(1)),
            BuildMetrics(fromDate.AddDays(2))
        };
        _metricsRepoMock
            .Setup(r => r.GetByDateRangeAsync(ShopId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var result = await _sut.GetSalesTrendAsync(ShopId, fromDate, toDate);

        result.Should().HaveCount(3);
        result[0].Date.Should().Be(fromDate);
        result[0].OrderCount.Should().Be(1);
        result[0].SalesAmount.Should().Be(1000m);
        _metricsRepoMock.Verify(r => r.GetByDateRangeAsync(ShopId, fromDate, toDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShopMetricsAsync_InvalidRange_ShouldThrowDomainException()
    {
        var fromDate = new DateOnly(2026, 7, 18);
        var toDate = new DateOnly(2026, 7, 17);

        var act = () => _sut.GetShopMetricsAsync(ShopId, fromDate, toDate);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*起始日期不可晚于结束日期*");
    }

    [Fact]
    public async Task GetShopMetricsAsync_EmptyRange_ShouldReturnEmptyList()
    {
        var fromDate = new DateOnly(2026, 7, 10);
        var toDate = new DateOnly(2026, 7, 12);
        _metricsRepoMock
            .Setup(r => r.GetByDateRangeAsync(ShopId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopMetrics>());

        var result = await _sut.GetShopMetricsAsync(ShopId, fromDate, toDate);

        result.Should().BeEmpty();
    }

    private static ShopMetrics BuildMetrics(DateOnly date)
    {
        // 使用工厂方法构造零值后通过行为方法累加，避免直接 new 内部构造
        var metrics = ShopMetrics.Create(Guid.NewGuid(), ShopId, date, "CNY");
        metrics.RecordOrder(Money.Create(1000m, "CNY"));
        metrics.UpdateProductCount(10);
        metrics.RecordRating(5);
        metrics.RecordRating(4);
        metrics.RecordRefund();
        return metrics;
    }
}
