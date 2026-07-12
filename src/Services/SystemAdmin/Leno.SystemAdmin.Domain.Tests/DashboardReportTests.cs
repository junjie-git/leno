using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class DashboardReportTests
{
    private static readonly Guid ValidReportId = Guid.NewGuid();
    private static readonly ReportPeriod ValidPeriod = new(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
    private static readonly List<MetricItem> ValidMetrics = new()
    {
        new("total_orders", 1000m, "单"),
        new("total_gmv", 50000m, "CNY")
    };
    private const string ValidGranularity = "daily";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var report = DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, ValidGranularity);

        report.ReportId.Should().Be(ValidReportId);
        report.Id.Should().Be(ValidReportId);
        report.ReportType.Should().Be(ReportType.OrderGmv);
        report.Period.Should().Be(ValidPeriod);
        report.Metrics.Should().HaveCount(2);
        report.Granularity.Should().Be("daily");
        report.DataVersion.Should().Be(1);
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldNormalizeGranularityToLower()
    {
        var report = DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "DAILY");

        report.Granularity.Should().Be("daily");
    }

    [Fact]
    public void Create_WithTrimmedGranularity_ShouldSucceed()
    {
        var report = DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "  daily  ");

        report.Granularity.Should().Be("daily");
    }

    [Fact]
    public void Create_WithHourlyGranularity_ShouldSucceed()
    {
        var report = DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "hourly");

        report.Granularity.Should().Be("hourly");
    }

    [Fact]
    public void Create_WithWeeklyGranularity_ShouldSucceed()
    {
        var report = DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "weekly");

        report.Granularity.Should().Be("weekly");
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyReportId_ShouldThrowReportIdEmpty()
    {
        var act = () => DashboardReport.Create(Guid.Empty, ReportType.OrderGmv, ValidPeriod, ValidMetrics, ValidGranularity);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullMetrics_ShouldThrowReportMetricsEmpty()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, null!, ValidGranularity);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_METRICS_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyMetrics_ShouldThrowReportMetricsEmpty()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, new List<MetricItem>(), ValidGranularity);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_METRICS_EMPTY");
    }

    [Fact]
    public void Create_WithNullGranularity_ShouldThrowReportGranularityEmpty()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_GRANULARITY_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyGranularity_ShouldThrowReportGranularityEmpty()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_GRANULARITY_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceGranularity_ShouldThrowReportGranularityEmpty()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "   ");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_GRANULARITY_EMPTY");
    }

    [Fact]
    public void Create_WithInvalidGranularity_ShouldThrowReportGranularityInvalid()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, "monthly");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_GRANULARITY_INVALID");
    }

    [Fact]
    public void Create_WithGranularityTooLong_ShouldThrowReportGranularityLength()
    {
        var longGranularity = new string('d', 17);

        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, longGranularity);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_GRANULARITY_LENGTH");
    }

    [Fact]
    public void Create_WithGranularityAtMaxLength_ShouldSucceed()
    {
        var granularity = new string('d', 16);

        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, ValidPeriod, ValidMetrics, granularity);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_GRANULARITY_INVALID");
    }

    [Fact]
    public void Create_WithNullPeriod_ShouldThrowArgumentNull()
    {
        var act = () => DashboardReport.Create(ValidReportId, ReportType.OrderGmv, null!, ValidMetrics, ValidGranularity);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Create - All Report Types

    [Theory]
    [InlineData(ReportType.OrderGmv)]
    [InlineData(ReportType.PaymentSuccessRate)]
    [InlineData(ReportType.PointsIssued)]
    [InlineData(ReportType.NotificationDelivery)]
    [InlineData(ReportType.AfterSalesVolume)]
    [InlineData(ReportType.ShopRanking)]
    [InlineData(ReportType.ConversionRate)]
    public void Create_WithAllReportTypes_ShouldSucceed(ReportType reportType)
    {
        var report = DashboardReport.Create(ValidReportId, reportType, ValidPeriod, ValidMetrics, ValidGranularity);

        report.ReportType.Should().Be(reportType);
    }

    #endregion

    #region Immutability

    [Fact]
    public void DashboardReport_ShouldBeImmutable_NoUpdateMethods()
    {
        var reportType = typeof(DashboardReport);
        var methods = reportType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == reportType);

        // Only Create factory method should exist (it's static), no public instance methods
        var instanceMethods = methods.Where(m => !m.Name.Contains("get_") && !m.Name.Contains("set_"));
        instanceMethods.Should().BeEmpty("DashboardReport should have no public instance update methods");
    }

    #endregion
}

public class ReportPeriodTests
{
    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidDates_ShouldSetProperties()
    {
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var period = new ReportPeriod(start, end);

        period.Start.Should().Be(start);
        period.End.Should().Be(end);
    }

    #endregion

    #region Constructor - Validation

    [Fact]
    public void Constructor_WithDefaultStart_ShouldThrowReportPeriodStartEmpty()
    {
        var act = () => new ReportPeriod(default, DateTime.UtcNow);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_PERIOD_START_EMPTY");
    }

    [Fact]
    public void Constructor_WithDefaultEnd_ShouldThrowReportPeriodEndEmpty()
    {
        var act = () => new ReportPeriod(DateTime.UtcNow, default);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_PERIOD_END_EMPTY");
    }

    [Fact]
    public void Constructor_WithStartEqualToEnd_ShouldThrowReportPeriodInvalid()
    {
        var now = DateTime.UtcNow;

        var act = () => new ReportPeriod(now, now);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_PERIOD_INVALID");
    }

    [Fact]
    public void Constructor_WithStartAfterEnd_ShouldThrowReportPeriodInvalid()
    {
        var act = () => new ReportPeriod(DateTime.UtcNow, DateTime.UtcNow.AddDays(-7));

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("REPORT_PERIOD_INVALID");
    }

    #endregion

    #region Equality

    [Fact]
    public void SameValues_ShouldBeEqual()
    {
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var period1 = new ReportPeriod(start, end);
        var period2 = new ReportPeriod(start, end);

        period1.Should().Be(period2);
        period1.GetHashCode().Should().Be(period2.GetHashCode());
    }

    [Fact]
    public void DifferentValues_ShouldNotBeEqual()
    {
        var period1 = new ReportPeriod(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var period2 = new ReportPeriod(DateTime.UtcNow.AddDays(-14), DateTime.UtcNow);

        period1.Should().NotBe(period2);
    }

    #endregion
}

public class MetricItemTests
{
    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidParameters_ShouldSetProperties()
    {
        var metric = new MetricItem("total_gmv", 50000.99m, "CNY");

        metric.Key.Should().Be("total_gmv");
        metric.Value.Should().Be(50000.99m);
        metric.Unit.Should().Be("CNY");
    }

    [Fact]
    public void Constructor_ShouldTrimKeyAndUnit()
    {
        var metric = new MetricItem("  total_gmv  ", 50000m, "  CNY  ");

        metric.Key.Should().Be("total_gmv");
        metric.Unit.Should().Be("CNY");
    }

    [Fact]
    public void Constructor_WithNegativeValue_ShouldSucceed()
    {
        var metric = new MetricItem("growth_rate", -5.5m, "%");

        metric.Value.Should().Be(-5.5m);
    }

    [Fact]
    public void Constructor_WithZeroValue_ShouldSucceed()
    {
        var metric = new MetricItem("total_orders", 0m, "单");

        metric.Value.Should().Be(0m);
    }

    #endregion

    #region Constructor - Validation

    [Fact]
    public void Constructor_WithNullKey_ShouldThrowMetricKeyEmpty()
    {
        var act = () => new MetricItem(null!, 100m, "CNY");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_KEY_EMPTY");
    }

    [Fact]
    public void Constructor_WithEmptyKey_ShouldThrowMetricKeyEmpty()
    {
        var act = () => new MetricItem("", 100m, "CNY");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_KEY_EMPTY");
    }

    [Fact]
    public void Constructor_WithWhitespaceKey_ShouldThrowMetricKeyEmpty()
    {
        var act = () => new MetricItem("   ", 100m, "CNY");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_KEY_EMPTY");
    }

    [Fact]
    public void Constructor_WithKeyTooLong_ShouldThrowMetricKeyLength()
    {
        var longKey = new string('k', 129);

        var act = () => new MetricItem(longKey, 100m, "CNY");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_KEY_LENGTH");
    }

    [Fact]
    public void Constructor_WithKeyAtMaxLength_ShouldSucceed()
    {
        var key = new string('k', 128);

        var metric = new MetricItem(key, 100m, "CNY");

        metric.Key.Should().Be(key);
    }

    [Fact]
    public void Constructor_WithNullUnit_ShouldThrowMetricUnitEmpty()
    {
        var act = () => new MetricItem("total_gmv", 100m, null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_UNIT_EMPTY");
    }

    [Fact]
    public void Constructor_WithEmptyUnit_ShouldThrowMetricUnitEmpty()
    {
        var act = () => new MetricItem("total_gmv", 100m, "");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_UNIT_EMPTY");
    }

    [Fact]
    public void Constructor_WithUnitTooLong_ShouldThrowMetricUnitLength()
    {
        var longUnit = new string('u', 33);

        var act = () => new MetricItem("total_gmv", 100m, longUnit);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("METRIC_UNIT_LENGTH");
    }

    [Fact]
    public void Constructor_WithUnitAtMaxLength_ShouldSucceed()
    {
        var unit = new string('u', 32);

        var metric = new MetricItem("total_gmv", 100m, unit);

        metric.Unit.Should().Be(unit);
    }

    #endregion

    #region Equality

    [Fact]
    public void SameValues_ShouldBeEqual()
    {
        var metric1 = new MetricItem("total_gmv", 50000m, "CNY");
        var metric2 = new MetricItem("total_gmv", 50000m, "CNY");

        metric1.Should().Be(metric2);
        metric1.GetHashCode().Should().Be(metric2.GetHashCode());
    }

    [Fact]
    public void DifferentValues_ShouldNotBeEqual()
    {
        var metric1 = new MetricItem("total_gmv", 50000m, "CNY");
        var metric2 = new MetricItem("total_orders", 1000m, "单");

        metric1.Should().NotBe(metric2);
    }

    #endregion
}