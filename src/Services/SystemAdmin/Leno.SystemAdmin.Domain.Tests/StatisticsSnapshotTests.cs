using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class StatisticsSnapshotTests
{
    private static readonly ReportPeriod ValidPeriod = new(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
    private static readonly List<MetricItem> ValidAggregatedMetrics = new()
    {
        new("total_orders", 1000m, "单"),
        new("total_gmv", 50000m, "CNY")
    };
    private static readonly List<MetricItem> ValidDomainMetrics = new()
    {
        new("total_orders", 950m, "单"),
        new("total_gmv", 48000m, "CNY")
    };
    private static readonly List<MetricDiscrepancy> ValidDiscrepancies = new()
    {
        new("total_orders", 1000m, 950m),
        new("total_gmv", 50000m, 48000m)
    };

    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidParameters_ShouldSetAllProperties()
    {
        var snapshot = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);

        snapshot.ReportType.Should().Be(ReportType.OrderGmv);
        snapshot.Period.Should().Be(ValidPeriod);
        snapshot.AggregatedMetrics.Should().HaveCount(2);
        snapshot.DomainMetrics.Should().HaveCount(2);
        snapshot.Discrepancies.Should().HaveCount(2);
        snapshot.Status.Should().Be(ReconciliationStatus.DiscrepancyFound);
        snapshot.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyDiscrepancies_ShouldSetConsistentStatus()
    {
        var snapshot = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics,
            new List<MetricDiscrepancy>());

        snapshot.Status.Should().Be(ReconciliationStatus.Consistent);
    }

    [Fact]
    public void Constructor_WithOnlyAggregatedMetrics_ShouldSucceed()
    {
        var snapshot = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics,
            new List<MetricItem>(),
            new List<MetricDiscrepancy>());

        snapshot.AggregatedMetrics.Should().HaveCount(2);
        snapshot.DomainMetrics.Should().BeEmpty();
        snapshot.Status.Should().Be(ReconciliationStatus.Consistent);
    }

    [Fact]
    public void Constructor_WithOnlyDomainMetrics_ShouldSucceed()
    {
        var snapshot = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod,
            new List<MetricItem>(),
            ValidDomainMetrics,
            new List<MetricDiscrepancy>());

        snapshot.AggregatedMetrics.Should().BeEmpty();
        snapshot.DomainMetrics.Should().HaveCount(2);
        snapshot.Status.Should().Be(ReconciliationStatus.Consistent);
    }

    [Fact]
    public void Constructor_WithAllReportTypes_ShouldSucceed()
    {
        foreach (var reportType in Enum.GetValues<ReportType>())
        {
            var snapshot = new StatisticsSnapshot(
                reportType, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics,
                new List<MetricDiscrepancy>());

            snapshot.ReportType.Should().Be(reportType);
        }
    }

    #endregion

    #region Constructor - Validation

    [Fact]
    public void Constructor_WithNullPeriod_ShouldThrowArgumentNull()
    {
        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, null!, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAggregatedMetrics_ShouldThrowArgumentNull()
    {
        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, null!, ValidDomainMetrics, ValidDiscrepancies);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullDomainMetrics_ShouldThrowArgumentNull()
    {
        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, null!, ValidDiscrepancies);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullDiscrepancies_ShouldThrowArgumentNull()
    {
        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithBothMetricsEmpty_ShouldThrowSnapshotMetricsEmpty()
    {
        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, new List<MetricItem>(), new List<MetricItem>(),
            new List<MetricDiscrepancy>());

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("SNAPSHOT_METRICS_EMPTY");
    }

    [Fact]
    public void Constructor_WithTooManyAggregatedMetrics_ShouldThrowSnapshotMetricsTooMany()
    {
        var tooManyMetrics = new List<MetricItem>();
        for (var i = 0; i < 201; i++)
        {
            tooManyMetrics.Add(new MetricItem($"metric_{i}", i, "次"));
        }

        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, tooManyMetrics, ValidDomainMetrics, ValidDiscrepancies);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("SNAPSHOT_METRICS_TOO_MANY");
    }

    [Fact]
    public void Constructor_WithTooManyDomainMetrics_ShouldThrowSnapshotDomainMetricsTooMany()
    {
        var tooManyMetrics = new List<MetricItem>();
        for (var i = 0; i < 201; i++)
        {
            tooManyMetrics.Add(new MetricItem($"metric_{i}", i, "次"));
        }

        var act = () => new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, tooManyMetrics, ValidDiscrepancies);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("SNAPSHOT_DOMAIN_METRICS_TOO_MANY");
    }

    [Fact]
    public void Constructor_WithMaxMetricsCount_ShouldSucceed()
    {
        var maxMetrics = new List<MetricItem>();
        for (var i = 0; i < 200; i++)
        {
            maxMetrics.Add(new MetricItem($"metric_{i}", i, "次"));
        }

        var snapshot = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, maxMetrics, ValidDomainMetrics, ValidDiscrepancies);

        snapshot.AggregatedMetrics.Should().HaveCount(200);
    }

    #endregion

    #region CreateError

    [Fact]
    public void CreateError_WithValidMessage_ShouldSetErrorStatus()
    {
        var snapshot = StatisticsSnapshot.CreateError(
            ReportType.OrderGmv, ValidPeriod, "对账发生异常: 连接超时");

        snapshot.ReportType.Should().Be(ReportType.OrderGmv);
        snapshot.Period.Should().Be(ValidPeriod);
        snapshot.Status.Should().Be(ReconciliationStatus.Error);
        snapshot.ErrorMessage.Should().Be("对账发生异常: 连接超时");
        snapshot.AggregatedMetrics.Should().BeEmpty();
        snapshot.DomainMetrics.Should().BeEmpty();
        snapshot.Discrepancies.Should().BeEmpty();
    }

    [Fact]
    public void CreateError_WithNullMessage_ShouldThrowSnapshotErrorEmpty()
    {
        var act = () => StatisticsSnapshot.CreateError(ReportType.OrderGmv, ValidPeriod, null!);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("SNAPSHOT_ERROR_EMPTY");
    }

    [Fact]
    public void CreateError_WithEmptyMessage_ShouldThrowSnapshotErrorEmpty()
    {
        var act = () => StatisticsSnapshot.CreateError(ReportType.OrderGmv, ValidPeriod, "");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("SNAPSHOT_ERROR_EMPTY");
    }

    [Fact]
    public void CreateError_WithWhitespaceMessage_ShouldThrowSnapshotErrorEmpty()
    {
        var act = () => StatisticsSnapshot.CreateError(ReportType.OrderGmv, ValidPeriod, "   ");

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("SNAPSHOT_ERROR_EMPTY");
    }

    #endregion

    #region Equality

    [Fact]
    public void SameValues_ShouldBeEqual()
    {
        var s1 = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);
        var s2 = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);

        s1.Should().Be(s2);
        s1.GetHashCode().Should().Be(s2.GetHashCode());
    }

    [Fact]
    public void DifferentReportType_ShouldNotBeEqual()
    {
        var s1 = new StatisticsSnapshot(
            ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);
        var s2 = new StatisticsSnapshot(
            ReportType.PaymentSuccessRate, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);

        s1.Should().NotBe(s2);
    }

    #endregion

    #region Immutability

    [Fact]
    public void StatisticsSnapshot_ShouldBeImmutableRecord()
    {
        var snapshotType = typeof(StatisticsSnapshot);
        var properties = snapshotType.GetProperties();
        var settable = properties.Where(p => p.CanWrite).ToList();

        // Record properties should only have init accessors
        settable.Should().BeEmpty("StatisticsSnapshot should be immutable");
    }

    #endregion
}