using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.ValueObjects;
using Leno.SharedKernel.ValueObjects;
using Moq;

namespace Leno.SellerShop.Application.Tests;

/// <summary>
/// 卖家工作台应用服务单元测试，覆盖销售趋势/指标查询用例。
/// <para>
/// 双轨下线 E2（2026-09-23）：原 GetDashboardAsync（SQL 累计查询）随双轨删除，
/// 工作台概览改由 ES 读模型承载（ShopDashboardQueryHandler，另有其测试）。
/// </para>
/// </summary>
public class SellerDashboardAppServiceTests
{
    private readonly Mock<IShopRepository> _shopRepoMock = new();
    private readonly Mock<IShopMetricsRepository> _metricsRepoMock = new();
    private readonly Mock<IProductAntiCorruptionService> _productAntiCorruptionMock = new();
    private readonly SellerDashboardAppService _sut;

    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ShopId = Guid.NewGuid();

    public SellerDashboardAppServiceTests()
    {
        _sut = new SellerDashboardAppService(
            _shopRepoMock.Object,
            _metricsRepoMock.Object,
            _productAntiCorruptionMock.Object);
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
