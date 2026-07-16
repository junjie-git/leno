using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Domain.Tests;

#region PointsAccount ConsumePoints / RevertPoints Tests

public class PointsAccountConsumeRevertTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ReferenceId = Guid.NewGuid();

    [Fact]
    public void ConsumePoints_Valid_ShouldDeductBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "");

        account.ConsumePoints(50, ReferenceId, "");

        account.Balance.Should().Be(50);
        account.TotalSpent.Should().Be(50);
        account.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void ConsumePoints_ZeroAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "");

        var act = () => account.ConsumePoints(0, ReferenceId, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void ConsumePoints_NegativeAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "");

        var act = () => account.ConsumePoints(-10, ReferenceId, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void ConsumePoints_InsufficientBalance_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 30, "");

        var act = () => account.ConsumePoints(50, ReferenceId, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*余额不足*");
    }

    [Fact]
    public void ConsumePoints_AllBalance_ShouldResultInZero()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "");

        account.ConsumePoints(100, ReferenceId, "");

        account.Balance.Should().Be(0);
        account.TotalSpent.Should().Be(100);
    }

    [Fact]
    public void ConsumePoints_ShouldPublishConsumedEvent()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "");

        account.ConsumePoints(30, ReferenceId, "兑换优惠券");

        var consumedEvent = account.DomainEvents.OfType<PointsConsumedEvent>().Single();
        consumedEvent.Amount.Should().Be(30);
        consumedEvent.ReferenceId.Should().Be(ReferenceId);
        consumedEvent.Reason.Should().Be("兑换优惠券");
    }

    [Fact]
    public void RevertPoints_Valid_ShouldDeductBalanceAndAllowNegative()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 50, "");

        account.RevertPoints(100, ReferenceId, "退款扣回");

        account.Balance.Should().Be(-50);
        account.TotalSpent.Should().Be(100);
    }

    [Fact]
    public void RevertPoints_ZeroAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var act = () => account.RevertPoints(0, ReferenceId, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void RevertPoints_NegativeAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var act = () => account.RevertPoints(-10, ReferenceId, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void RevertPoints_ShouldPublishRevertedEvent()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Consumption, 200, "");

        account.RevertPoints(150, ReferenceId, "退款扣回");

        var revertedEvent = account.DomainEvents.OfType<PointsRevertedEvent>().Single();
        revertedEvent.Amount.Should().Be(150);
        revertedEvent.ReferenceId.Should().Be(ReferenceId);
    }

    [Fact]
    public void RevertPoints_Multiple_ShouldAccumulateSpent()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Consumption, 500, "");

        account.RevertPoints(100, Guid.NewGuid(), "退款1");
        account.RevertPoints(200, Guid.NewGuid(), "退款2");

        account.Balance.Should().Be(200);
        account.TotalSpent.Should().Be(300);
    }
}

#endregion
