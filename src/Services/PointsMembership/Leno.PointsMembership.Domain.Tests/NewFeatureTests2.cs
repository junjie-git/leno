using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Domain.Tests;

#region PointsSource Enum Tests

public class PointsSourceEnumTests
{
    [Fact]
    public void PointsSource_HasAllExpectedValues()
    {
        var values = Enum.GetValues<PointsSource>();
        values.Should().Contain(PointsSource.CheckIn);
        values.Should().Contain(PointsSource.Consumption);
        values.Should().Contain(PointsSource.Activity);
        values.Should().Contain(PointsSource.Refund);
        values.Should().Contain(PointsSource.Offset);
        values.Should().Contain(PointsSource.Review);
        values.Should().Contain(PointsSource.NewUser);
        values.Should().Contain(PointsSource.CouponExchange);
    }

    [Fact]
    public void PointsTxType_HasAllExpectedValues()
    {
        var values = Enum.GetValues<PointsTxType>();
        values.Should().Contain(PointsTxType.Earn);
        values.Should().Contain(PointsTxType.Freeze);
        values.Should().Contain(PointsTxType.ConfirmDeduct);
        values.Should().Contain(PointsTxType.Release);
        values.Should().Contain(PointsTxType.Refund);
        values.Should().Contain(PointsTxType.Consume);
        values.Should().Contain(PointsTxType.Revert);
        values.Should().Contain(PointsTxType.CouponExchange);
    }
}

#endregion
