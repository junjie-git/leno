using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class MetricDiscrepancyTests
{
    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidParameters_ShouldSetAllProperties()
    {
        var discrepancy = new MetricDiscrepancy("total_gmv", 1000m, 950m);

        discrepancy.MetricKey.Should().Be("total_gmv");
        discrepancy.AggregatedValue.Should().Be(1000m);
        discrepancy.DomainValue.Should().Be(950m);
        discrepancy.Difference.Should().Be(50m);
        discrepancy.DifferencePercentage.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_WithEqualValues_ShouldHaveZeroDifference()
    {
        var discrepancy = new MetricDiscrepancy("total_gmv", 1000m, 1000m);

        discrepancy.Difference.Should().Be(0m);
        discrepancy.DifferencePercentage.Should().Be(0m);
    }

    [Fact]
    public void Constructor_WithZeroDomainValue_ShouldHaveZeroPercentage()
    {
        var discrepancy = new MetricDiscrepancy("total_gmv", 100m, 0m);

        discrepancy.Difference.Should().Be(100m);
        discrepancy.DifferencePercentage.Should().Be(0m);
    }

    [Fact]
    public void Constructor_ShouldTrimMetricKey()
    {
        var discrepancy = new MetricDiscrepancy("  total_gmv  ", 1000m, 950m);

        discrepancy.MetricKey.Should().Be("total_gmv");
    }

    [Fact]
    public void Constructor_WithNegativeAggregatedValue_ShouldSucceed()
    {
        var discrepancy = new MetricDiscrepancy("growth_rate", -5m, 10m);

        discrepancy.AggregatedValue.Should().Be(-5m);
        discrepancy.Difference.Should().Be(15m);
    }

    [Fact]
    public void Constructor_WithNegativeDomainValue_ShouldSucceed()
    {
        var discrepancy = new MetricDiscrepancy("growth_rate", 10m, -5m);

        discrepancy.DomainValue.Should().Be(-5m);
        discrepancy.DifferencePercentage.Should().Be(300m); // |10 - (-5)| / |-5| * 100 = 15/5 * 100 = 300
    }

    [Fact]
    public void Constructor_WithLargeValues_ShouldCalculateCorrectly()
    {
        var discrepancy = new MetricDiscrepancy("total_gmv", 1000000m, 999000m);

        discrepancy.Difference.Should().Be(1000m);
        discrepancy.DifferencePercentage.Should().BeGreaterThan(0);
    }

    #endregion

    #region Constructor - Validation

    [Fact]
    public void Constructor_WithNullKey_ShouldThrowDiscrepancyKeyEmpty()
    {
        var act = () => new MetricDiscrepancy(null!, 100m, 100m);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DISCREPANCY_KEY_EMPTY");
    }

    [Fact]
    public void Constructor_WithEmptyKey_ShouldThrowDiscrepancyKeyEmpty()
    {
        var act = () => new MetricDiscrepancy("", 100m, 100m);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DISCREPANCY_KEY_EMPTY");
    }

    [Fact]
    public void Constructor_WithWhitespaceKey_ShouldThrowDiscrepancyKeyEmpty()
    {
        var act = () => new MetricDiscrepancy("   ", 100m, 100m);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DISCREPANCY_KEY_EMPTY");
    }

    [Fact]
    public void Constructor_WithKeyTooLong_ShouldThrowDiscrepancyKeyLength()
    {
        var longKey = new string('k', 129);

        var act = () => new MetricDiscrepancy(longKey, 100m, 100m);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DISCREPANCY_KEY_LENGTH");
    }

    [Fact]
    public void Constructor_WithKeyAtMaxLength_ShouldSucceed()
    {
        var key = new string('k', 128);

        var discrepancy = new MetricDiscrepancy(key, 100m, 100m);

        discrepancy.MetricKey.Should().Be(key);
    }

    #endregion

    #region Equality

    [Fact]
    public void SameValues_ShouldBeEqual()
    {
        var d1 = new MetricDiscrepancy("total_gmv", 1000m, 950m);
        var d2 = new MetricDiscrepancy("total_gmv", 1000m, 950m);

        d1.Should().Be(d2);
        d1.GetHashCode().Should().Be(d2.GetHashCode());
    }

    [Fact]
    public void DifferentValues_ShouldNotBeEqual()
    {
        var d1 = new MetricDiscrepancy("total_gmv", 1000m, 950m);
        var d2 = new MetricDiscrepancy("total_orders", 100m, 90m);

        d1.Should().NotBe(d2);
    }

    #endregion
}