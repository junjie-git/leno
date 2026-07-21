using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

public sealed class StatisticsAggregationServiceTests
{
    private static readonly ReportPeriod Period =
        new(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

    private readonly Mock<IStatisticsDataSource> _dataSourceMock = new();
    private readonly StatisticsAggregationService _service;

    public StatisticsAggregationServiceTests()
    {
        _service = new StatisticsAggregationService(
            _dataSourceMock.Object,
            NullLogger<StatisticsAggregationService>.Instance);
    }

    [Fact]
    public async Task AggregateAsync_OrderGmv_Should_Return_Metrics_From_DataSource_Not_Random()
    {
        var expectedMetrics = new List<MetricItem>
        {
            new("total_orders", 1200m, "单"),
            new("total_gmv", 96000m, "CNY"),
            new("avg_order_value", 80m, "CNY"),
            new("order_growth_rate", 5.5m, "%")
        };
        _dataSourceMock
            .Setup(d => d.GetMetricsAsync(ReportType.OrderGmv, Period, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        var report = await _service.AggregateAsync(ReportType.OrderGmv, Period, CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(ReportType.OrderGmv, report.ReportType);
        Assert.Equal(expectedMetrics.Count, report.Metrics.Count);
        Assert.Equal(1200m, report.Metrics[0].Value);
        Assert.Equal(96000m, report.Metrics[1].Value);
        _dataSourceMock.Verify(
            d => d.GetMetricsAsync(ReportType.OrderGmv, Period, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AggregateAsync_ShopRanking_Should_Not_Contain_Hardcoded_Shop_Names()
    {
        var expectedMetrics = new List<MetricItem>
        {
            new("shop_1_sales", 50000m, "CNY"),
            new("shop_1_name", 0, "官方旗舰店")
        };
        _dataSourceMock
            .Setup(d => d.GetMetricsAsync(ReportType.ShopRanking, Period, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        var report = await _service.AggregateAsync(ReportType.ShopRanking, Period, CancellationToken.None);

        Assert.Equal(2, report.Metrics.Count);
        Assert.DoesNotContain(report.Metrics, m => m.Key.StartsWith("shop_2_"));
    }

    [Fact]
    public async Task AggregateAsync_Should_Throw_When_DataSource_Returns_Empty_Metrics()
    {
        _dataSourceMock
            .Setup(d => d.GetMetricsAsync(It.IsAny<ReportType>(), It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MetricItem>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AggregateAsync(ReportType.OrderGmv, Period, CancellationToken.None));
    }
}
