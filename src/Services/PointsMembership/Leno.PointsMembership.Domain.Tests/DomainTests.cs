using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace Leno.PointsMembership.Domain.Tests;

public class PointsAccountTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeWithZeroBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        account.Id.Should().Be(AccountId);
        account.UserId.Should().Be(UserId);
        account.Balance.Should().Be(0);
        account.FrozenBalance.Should().Be(0);
        account.TotalEarned.Should().Be(0);
        account.TotalSpent.Should().Be(0);
    }

    [Fact]
    public void Create_EmptyAccountId_ShouldGenerateNewId()
    {
        var account = PointsAccount.Create(Guid.Empty, UserId);

        account.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => PointsAccount.Create(AccountId, Guid.Empty);

        act.Should().Throw<PointsDomainException>().WithMessage("*UserId*");
    }

    #region Earn

    [Fact]
    public void Earn_Valid_ShouldIncreaseBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        account.Earn(PointsSource.CheckIn, 50, "签到");

        account.Balance.Should().Be(50);
        account.TotalEarned.Should().Be(50);
        account.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Earn_ZeroAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var act = () => account.Earn(PointsSource.CheckIn, 0, "签到");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void Earn_NegativeAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var act = () => account.Earn(PointsSource.CheckIn, -10, "签到");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void Earn_MultipleTimes_ShouldAccumulate()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        account.Earn(PointsSource.CheckIn, 10, "签到");
        account.Earn(PointsSource.Consumption, 100, "消费");

        account.Balance.Should().Be(110);
        account.TotalEarned.Should().Be(110);
    }

    #endregion

    #region TryOffset

    [Fact]
    public void TryOffset_SufficientBalance_ShouldReturnOffsetAmount()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 200, "签到");

        var result = account.TryOffset(100);

        result.Should().Be(1m); // 100 points = 1 yuan
        account.Balance.Should().Be(200); // balance unchanged
    }

    [Fact]
    public void TryOffset_InsufficientBalance_ShouldReturnZero()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 50, "签到");

        var result = account.TryOffset(100);

        result.Should().Be(0);
    }

    [Fact]
    public void TryOffset_ZeroPoints_ShouldReturnZero()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var result = account.TryOffset(0);

        result.Should().Be(0);
    }

    [Fact]
    public void TryOffset_NegativePoints_ShouldReturnZero()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var result = account.TryOffset(-50);

        result.Should().Be(0);
    }

    [Fact]
    public void TryOffset_200Points_ShouldReturn2Yuan()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 200, "签到");

        var result = account.TryOffset(200);

        result.Should().Be(2m);
    }

    #endregion

    #region Freeze

    [Fact]
    public void Freeze_Valid_ShouldMoveBalanceToFrozen()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        account.Freeze(50, OrderId);

        account.Balance.Should().Be(50);
        account.FrozenBalance.Should().Be(50);
        account.FrozenEntries.Should().HaveCount(1);
        account.FrozenEntries[0].Amount.Should().Be(50);
        account.FrozenEntries[0].OrderId.Should().Be(OrderId);
        account.DomainEvents.Should().HaveCount(2); // Earn + Freeze
    }

    [Fact]
    public void Freeze_ZeroAmount_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var act = () => account.Freeze(0, OrderId);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void Freeze_EmptyOrderId_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var act = () => account.Freeze(50, Guid.Empty);

        act.Should().Throw<PointsDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void Freeze_InsufficientBalance_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 30, "签到");

        var act = () => account.Freeze(50, OrderId);

        act.Should().Throw<PointsDomainException>().WithMessage("*余额不足*");
    }

    [Fact]
    public void Freeze_AllBalance_ShouldResultInZeroBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        account.Freeze(100, OrderId);

        account.Balance.Should().Be(0);
        account.FrozenBalance.Should().Be(100);
    }

    #endregion

    #region ConfirmDeduct

    [Fact]
    public void ConfirmDeduct_Valid_ShouldReduceFrozenAndIncreaseSpent()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");
        account.Freeze(50, OrderId);

        account.ConfirmDeduct(OrderId);

        account.FrozenBalance.Should().Be(0);
        account.FrozenEntries.Should().BeEmpty();
        account.TotalSpent.Should().Be(50);
        account.Balance.Should().Be(50); // balance unchanged
    }

    [Fact]
    public void ConfirmDeduct_EmptyOrderId_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var act = () => account.ConfirmDeduct(Guid.Empty);

        act.Should().Throw<PointsDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void ConfirmDeduct_NotFound_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var act = () => account.ConfirmDeduct(OrderId);

        act.Should().Throw<PointsDomainException>().WithMessage("*冻结记录不存在*");
    }

    #endregion

    #region Release

    [Fact]
    public void Release_Valid_ShouldReturnFrozenToBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");
        account.Freeze(50, OrderId);

        account.Release(OrderId);

        account.FrozenBalance.Should().Be(0);
        account.FrozenEntries.Should().BeEmpty();
        account.Balance.Should().Be(100);
        account.TotalSpent.Should().Be(0); // not spent
    }

    [Fact]
    public void Release_EmptyOrderId_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        var act = () => account.Release(Guid.Empty);

        act.Should().Throw<PointsDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void Release_NotFound_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var act = () => account.Release(OrderId);

        act.Should().Throw<PointsDomainException>().WithMessage("*冻结记录不存在*");
    }

    #endregion

    #region Full Lifecycle

    [Fact]
    public void FullLifecycle_FreezeConfirm_ShouldTrackCorrectly()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Consumption, 1000, "消费");

        account.Freeze(300, OrderId);
        account.ConfirmDeduct(OrderId);

        account.Balance.Should().Be(700);
        account.FrozenBalance.Should().Be(0);
        account.TotalEarned.Should().Be(1000);
        account.TotalSpent.Should().Be(300);
    }

    [Fact]
    public void FullLifecycle_FreezeRelease_ShouldRestoreBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Consumption, 1000, "消费");

        account.Freeze(300, OrderId);
        account.Release(OrderId);

        account.Balance.Should().Be(1000);
        account.FrozenBalance.Should().Be(0);
        account.TotalSpent.Should().Be(0);
    }

    #endregion
}

public class MemberTests
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeAsLevel1()
    {
        var member = Member.Create(MemberId, UserId);

        member.Id.Should().Be(MemberId);
        member.UserId.Should().Be(UserId);
        member.CurrentLevel.Should().Be(1);
        member.TotalConsumption.Should().Be(0);
        member.Status.Should().Be(MemberStatus.Active);
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => Member.Create(MemberId, Guid.Empty);

        act.Should().Throw<PointsDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void AddConsumption_Valid_ShouldIncreaseTotal()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddConsumption(500m);

        member.TotalConsumption.Should().Be(500m);
    }

    [Fact]
    public void AddConsumption_Zero_ShouldThrowException()
    {
        var member = Member.Create(MemberId, UserId);

        var act = () => member.AddConsumption(0);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void AddConsumption_Negative_ShouldThrowException()
    {
        var member = Member.Create(MemberId, UserId);

        var act = () => member.AddConsumption(-100m);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void AddConsumption_Multiple_ShouldAccumulate()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddConsumption(500m);
        member.AddConsumption(300m);

        member.TotalConsumption.Should().Be(800m);
    }

    #region CheckUpgrade

    [Fact]
    public void CheckUpgrade_MatchesHigherLevel_ShouldUpgrade()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddConsumption(1000m);
        var thresholds = new List<LevelThreshold>
        {
            new(1, "普通会员", 0),
            new(2, "银卡会员", 500m),
            new(3, "金卡会员", 1000m)
        };

        member.CheckUpgrade(thresholds);

        member.CurrentLevel.Should().Be(3);
        member.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void CheckUpgrade_SameLevel_ShouldNotUpgrade()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddConsumption(100m);
        var thresholds = new List<LevelThreshold>
        {
            new(1, "普通会员", 0),
            new(2, "银卡会员", 500m)
        };

        member.CheckUpgrade(thresholds);

        member.CurrentLevel.Should().Be(1);
        member.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CheckUpgrade_NotEnoughConsumption_ShouldStayAtLevel1()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddConsumption(400m);
        var thresholds = new List<LevelThreshold>
        {
            new(1, "普通会员", 0),
            new(2, "银卡会员", 500m),
            new(3, "金卡会员", 1000m)
        };

        member.CheckUpgrade(thresholds);

        member.CurrentLevel.Should().Be(1);
    }

    [Fact]
    public void CheckUpgrade_SkipsDisabledLevels_ShouldStillMatchHighest()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddConsumption(1000m);
        var thresholds = new List<LevelThreshold>
        {
            new(1, "普通会员", 0),
            new(3, "金卡会员", 1000m)
        };

        member.CheckUpgrade(thresholds);

        member.CurrentLevel.Should().Be(3);
    }

    [Fact]
    public void CheckUpgrade_EmptyThresholds_ShouldNotChange()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddConsumption(1000m);

        member.CheckUpgrade(new List<LevelThreshold>());

        member.CurrentLevel.Should().Be(1);
    }

    #endregion

    #region Freeze / Unfreeze

    [Fact]
    public void Freeze_Active_ShouldBecomeFrozen()
    {
        var member = Member.Create(MemberId, UserId);

        member.Freeze();

        member.Status.Should().Be(MemberStatus.Frozen);
    }

    [Fact]
    public void Freeze_AlreadyFrozen_ShouldThrowException()
    {
        var member = Member.Create(MemberId, UserId);
        member.Freeze();

        var act = () => member.Freeze();

        act.Should().Throw<PointsDomainException>().WithMessage("*不可冻结*");
    }

    [Fact]
    public void Unfreeze_Frozen_ShouldBecomeActive()
    {
        var member = Member.Create(MemberId, UserId);
        member.Freeze();

        member.Unfreeze();

        member.Status.Should().Be(MemberStatus.Active);
    }

    [Fact]
    public void Unfreeze_Active_ShouldThrowException()
    {
        var member = Member.Create(MemberId, UserId);

        var act = () => member.Unfreeze();

        act.Should().Throw<PointsDomainException>().WithMessage("*不可解冻*");
    }

    #endregion
}

public class MembershipLevelTests
{
    private static readonly Guid LevelId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeAsEnabled()
    {
        var level = MembershipLevel.Create(LevelId, "金卡会员", 3, 1000m, 0.95m);

        level.Id.Should().Be(LevelId);
        level.Name.Should().Be("金卡会员");
        level.Level.Should().Be(3);
        level.MinConsumption.Should().Be(1000m);
        level.DiscountRate.Should().Be(0.95m);
        level.Status.Should().Be(MembershipLevelStatus.Enabled);
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => MembershipLevel.Create(LevelId, "", 3, 1000m, 0.95m);

        act.Should().Throw<PointsDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_ZeroLevel_ShouldThrowException()
    {
        var act = () => MembershipLevel.Create(LevelId, "测试", 0, 1000m, 0.95m);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void Create_NegativeMinConsumption_ShouldThrowException()
    {
        var act = () => MembershipLevel.Create(LevelId, "测试", 3, -1m, 0.95m);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可为负*");
    }

    [Fact]
    public void Create_DiscountRateOutOfRange_ShouldThrowException()
    {
        var act = () => MembershipLevel.Create(LevelId, "测试", 3, 1000m, 1.5m);

        act.Should().Throw<PointsDomainException>().WithMessage("*0-1*");
    }

    [Fact]
    public void Update_Valid_ShouldChangeFields()
    {
        var level = MembershipLevel.Create(LevelId, "金卡会员", 3, 1000m, 0.95m);

        level.Update("钻石会员", 4, 2000m, 0.9m);

        level.Name.Should().Be("钻石会员");
        level.Level.Should().Be(4);
        level.MinConsumption.Should().Be(2000m);
        level.DiscountRate.Should().Be(0.9m);
    }

    [Fact]
    public void Enable_Disabled_ShouldBecomeEnabled()
    {
        var level = MembershipLevel.Create(LevelId, "金卡会员", 3, 1000m, 0.95m);
        level.Disable();

        level.Enable();

        level.Status.Should().Be(MembershipLevelStatus.Enabled);
    }

    [Fact]
    public void Enable_AlreadyEnabled_ShouldThrowException()
    {
        var level = MembershipLevel.Create(LevelId, "金卡会员", 3, 1000m, 0.95m);

        var act = () => level.Enable();

        act.Should().Throw<PointsDomainException>().WithMessage("*已启用*");
    }

    [Fact]
    public void Disable_Enabled_ShouldBecomeDisabled()
    {
        var level = MembershipLevel.Create(LevelId, "金卡会员", 3, 1000m, 0.95m);

        level.Disable();

        level.Status.Should().Be(MembershipLevelStatus.Disabled);
    }

    [Fact]
    public void Disable_AlreadyDisabled_ShouldThrowException()
    {
        var level = MembershipLevel.Create(LevelId, "金卡会员", 3, 1000m, 0.95m);
        level.Disable();

        var act = () => level.Disable();

        act.Should().Throw<PointsDomainException>().WithMessage("*已停用*");
    }
}

public class MembershipPackageTests
{
    private static readonly Guid PackageId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeAsEnabled()
    {
        var package = MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "[\"免运费\",\"专属折扣\"]");

        package.Id.Should().Be(PackageId);
        package.Name.Should().Be("月度会员");
        package.Level.Should().Be(2);
        package.Price.Should().Be(29.9m);
        package.DurationDays.Should().Be(30);
        package.Status.Should().Be(PackageStatus.Enabled);
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => MembershipPackage.Create(PackageId, "", 2, 29.9m, 30, "[]");

        act.Should().Throw<PointsDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_ZeroPrice_ShouldThrowException()
    {
        var act = () => MembershipPackage.Create(PackageId, "月度会员", 2, 0, 30, "[]");

        act.Should().Throw<PointsDomainException>().WithMessage("*价格*");
    }

    [Fact]
    public void Create_ZeroDuration_ShouldThrowException()
    {
        var act = () => MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 0, "[]");

        act.Should().Throw<PointsDomainException>().WithMessage("*时长*");
    }

    [Fact]
    public void Create_EmptyBenefits_ShouldThrowException()
    {
        var act = () => MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*权益*");
    }

    [Fact]
    public void Update_Valid_ShouldChangeFields()
    {
        var package = MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "[]");

        package.Update("年度会员", 3, 299m, 365, "[\"免运费\",\"专属折扣\",\"优先客服\"]");

        package.Name.Should().Be("年度会员");
        package.Level.Should().Be(3);
        package.Price.Should().Be(299m);
        package.DurationDays.Should().Be(365);
    }

    [Fact]
    public void Enable_Disabled_ShouldBecomeEnabled()
    {
        var package = MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "[]");
        package.Disable();

        package.Enable();

        package.Status.Should().Be(PackageStatus.Enabled);
    }

    [Fact]
    public void Enable_AlreadyEnabled_ShouldThrowException()
    {
        var package = MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "[]");

        var act = () => package.Enable();

        act.Should().Throw<PointsDomainException>().WithMessage("*已启用*");
    }

    [Fact]
    public void Disable_Enabled_ShouldBecomeDisabled()
    {
        var package = MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "[]");

        package.Disable();

        package.Status.Should().Be(PackageStatus.Disabled);
    }

    [Fact]
    public void Disable_AlreadyDisabled_ShouldThrowException()
    {
        var package = MembershipPackage.Create(PackageId, "月度会员", 2, 29.9m, 30, "[]");
        package.Disable();

        var act = () => package.Disable();

        act.Should().Throw<PointsDomainException>().WithMessage("*已停用*");
    }
}

public class UserMembershipTests
{
    private static readonly Guid UserMembershipId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeAsPending()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);

        um.Id.Should().Be(UserMembershipId);
        um.UserId.Should().Be(UserId);
        um.PackageId.Should().Be(PackageId);
        um.Level.Should().Be(2);
        um.Status.Should().Be(UserMembershipStatus.Pending);
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => UserMembership.Create(UserMembershipId, Guid.Empty, PackageId, 2);

        act.Should().Throw<PointsDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Create_EmptyPackageId_ShouldThrowException()
    {
        var act = () => UserMembership.Create(UserMembershipId, UserId, Guid.Empty, 2);

        act.Should().Throw<PointsDomainException>().WithMessage("*PackageId*");
    }

    [Fact]
    public void Create_ZeroLevel_ShouldThrowException()
    {
        var act = () => UserMembership.Create(UserMembershipId, UserId, PackageId, 0);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void Activate_Valid_ShouldSetTimelineAndStatus()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);
        var startTime = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

        um.Activate(OrderId, startTime, 30);

        um.Status.Should().Be(UserMembershipStatus.Active);
        um.OrderId.Should().Be(OrderId);
        um.StartTime.Should().Be(startTime);
        um.EndTime.Should().Be(startTime.AddDays(30));
        um.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Activate_EmptyOrderId_ShouldThrowException()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);

        var act = () => um.Activate(Guid.Empty, DateTime.UtcNow, 30);

        act.Should().Throw<PointsDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void Activate_ZeroDuration_ShouldThrowException()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);

        var act = () => um.Activate(OrderId, DateTime.UtcNow, 0);

        act.Should().Throw<PointsDomainException>().WithMessage("*时长*");
    }

    [Fact]
    public void Activate_NotPending_ShouldThrowException()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);
        um.Activate(OrderId, DateTime.UtcNow, 30);

        var act = () => um.Activate(Guid.NewGuid(), DateTime.UtcNow, 30);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可激活*");
    }

    [Fact]
    public void Expire_Active_ShouldBecomeExpired()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);
        um.Activate(OrderId, DateTime.UtcNow, 30);

        um.Expire();

        um.Status.Should().Be(UserMembershipStatus.Expired);
    }

    [Fact]
    public void Expire_Pending_ShouldThrowException()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);

        var act = () => um.Expire();

        act.Should().Throw<PointsDomainException>().WithMessage("*不可过期*");
    }

    [Fact]
    public void Cancel_Pending_ShouldBecomeCancelled()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);

        um.Cancel();

        um.Status.Should().Be(UserMembershipStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Active_ShouldThrowException()
    {
        var um = UserMembership.Create(UserMembershipId, UserId, PackageId, 2);
        um.Activate(OrderId, DateTime.UtcNow, 30);

        var act = () => um.Cancel();

        act.Should().Throw<PointsDomainException>().WithMessage("*不可取消*");
    }
}

public class CheckInRecordTests
{
    private static readonly Guid RecordId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 7, 12);

    [Fact]
    public void CheckIn_Valid_ShouldCreateRecord()
    {
        var record = CheckInRecord.CheckIn(RecordId, UserId, Today, 5, 20);

        record.Id.Should().Be(RecordId);
        record.UserId.Should().Be(UserId);
        record.CheckInDate.Should().Be(Today);
        record.ContinuousDays.Should().Be(5);
        record.PointsAwarded.Should().Be(20);
    }

    [Fact]
    public void CheckIn_EmptyUserId_ShouldThrowException()
    {
        var act = () => CheckInRecord.CheckIn(RecordId, Guid.Empty, Today, 5, 20);

        act.Should().Throw<PointsDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void CheckIn_ZeroContinuousDays_ShouldThrowException()
    {
        var act = () => CheckInRecord.CheckIn(RecordId, UserId, Today, 0, 20);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void CheckIn_NegativePoints_ShouldThrowException()
    {
        var act = () => CheckInRecord.CheckIn(RecordId, UserId, Today, 5, -1);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可为负*");
    }
}

public class LevelThresholdTests
{
    [Fact]
    public void Constructor_Valid_ShouldCreate()
    {
        var threshold = new LevelThreshold(2, "银卡会员", 500m);

        threshold.Level.Should().Be(2);
        threshold.Name.Should().Be("银卡会员");
        threshold.MinConsumption.Should().Be(500m);
    }

    [Fact]
    public void Constructor_ZeroLevel_ShouldThrowException()
    {
        var act = () => new LevelThreshold(0, "银卡会员", 500m);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void Constructor_EmptyName_ShouldThrowException()
    {
        var act = () => new LevelThreshold(2, "", 500m);

        act.Should().Throw<PointsDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Constructor_NegativeConsumption_ShouldThrowException()
    {
        var act = () => new LevelThreshold(2, "银卡会员", -1m);

        act.Should().Throw<PointsDomainException>().WithMessage("*不可为负*");
    }
}

public class OrderAfterSalesWindowClosedEventConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    private static async Task InvokeHandleAsync(
        OrderAfterSalesWindowClosedEventConsumer consumer,
        OrderAfterSalesWindowClosedEvent evt,
        CancellationToken ct = default)
    {
        var method = typeof(OrderAfterSalesWindowClosedEventConsumer)
            .GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, [evt, ct])!;
    }

    [Fact]
    public async Task HandleAsync_ValidEvent_ShouldEarnPointsAndAddConsumption()
    {
        // Arrange
        var paidAmount = 150.75m;
        var expectedPoints = 150;
        var evt = new OrderAfterSalesWindowClosedEvent(OrderId, UserId, paidAmount, DateTime.UtcNow);

        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        var member = Member.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OrderAfterSalesWindowClosedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            loggerMock.Object,
            idempotencyStoreMock.Object);

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        account.Balance.Should().Be(expectedPoints);
        account.TotalEarned.Should().Be(expectedPoints);
        member.TotalConsumption.Should().Be(paidAmount);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ZeroPaidAmount_ShouldNotEarnPoints()
    {
        // Arrange
        var evt = new OrderAfterSalesWindowClosedEvent(OrderId, UserId, 0m, DateTime.UtcNow);

        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        var member = Member.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OrderAfterSalesWindowClosedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            loggerMock.Object,
            idempotencyStoreMock.Object);

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        account.Balance.Should().Be(0);
        account.TotalEarned.Should().Be(0);
        // Member.AddConsumption(0) would throw, so it should not be called
        member.TotalConsumption.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_AccountNotFound_ShouldStillProcessMember()
    {
        // Arrange
        var paidAmount = 200m;
        var evt = new OrderAfterSalesWindowClosedEvent(OrderId, UserId, paidAmount, DateTime.UtcNow);

        var member = Member.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OrderAfterSalesWindowClosedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            loggerMock.Object,
            idempotencyStoreMock.Object);

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        member.TotalConsumption.Should().Be(paidAmount);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MemberNotFound_ShouldStillEarnPoints()
    {
        // Arrange
        var paidAmount = 300m;
        var expectedPoints = 300;
        var evt = new OrderAfterSalesWindowClosedEvent(OrderId, UserId, paidAmount, DateTime.UtcNow);

        var account = PointsAccount.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OrderAfterSalesWindowClosedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            loggerMock.Object,
            idempotencyStoreMock.Object);

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        account.Balance.Should().Be(expectedPoints);
        account.TotalEarned.Should().Be(expectedPoints);
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithLevelThresholds_ShouldCheckUpgrade()
    {
        // Arrange
        var paidAmount = 1000m;
        var expectedPoints = 1000;
        var evt = new OrderAfterSalesWindowClosedEvent(OrderId, UserId, paidAmount, DateTime.UtcNow);

        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        var member = Member.Create(Guid.NewGuid(), UserId);

        var level1 = MembershipLevel.Create(Guid.NewGuid(), "普通会员", 1, 0m, 1m);
        var level2 = MembershipLevel.Create(Guid.NewGuid(), "银卡会员", 2, 500m, 0.98m);
        var level3 = MembershipLevel.Create(Guid.NewGuid(), "金卡会员", 3, 1000m, 0.95m);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel> { level1, level2, level3 });

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OrderAfterSalesWindowClosedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            loggerMock.Object,
            idempotencyStoreMock.Object);

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        account.Balance.Should().Be(expectedPoints);
        member.TotalConsumption.Should().Be(paidAmount);
        member.CurrentLevel.Should().Be(3); // Upgraded to level 3
        uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FractionalPaidAmount_ShouldFloorToIntPoints()
    {
        // Arrange
        var paidAmount = 99.99m;
        var expectedPoints = 99;
        var evt = new OrderAfterSalesWindowClosedEvent(OrderId, UserId, paidAmount, DateTime.UtcNow);

        var account = PointsAccount.Create(Guid.NewGuid(), UserId);
        var member = Member.Create(Guid.NewGuid(), UserId);

        var accountRepoMock = new Mock<IPointsAccountRepository>();
        accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var memberRepoMock = new Mock<IMemberRepository>();
        memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var levelRepoMock = new Mock<IMembershipLevelRepository>();
        levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OrderAfterSalesWindowClosedEventConsumer>>();
        var idempotencyStoreMock = new Mock<IIdempotencyStore>();

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            accountRepoMock.Object,
            memberRepoMock.Object,
            levelRepoMock.Object,
            uowMock.Object,
            loggerMock.Object,
            idempotencyStoreMock.Object);

        // Act
        await InvokeHandleAsync(consumer, evt);

        // Assert
        account.Balance.Should().Be(expectedPoints);
        account.TotalEarned.Should().Be(expectedPoints);
    }
}

public class MemberLevelTests
{
    [Fact]
    public void Create_Valid_ShouldInitializeWithCorrectValues()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 2, "银卡会员", 500, 2000, "银卡等级");

        level.Level.Should().Be(2);
        level.Name.Should().Be("银卡会员");
        level.MinGrowthValue.Should().Be(500);
        level.MaxGrowthValue.Should().Be(2000);
        level.Description.Should().Be("银卡等级");
    }

    [Fact]
    public void Create_LevelOutOfRange_ShouldThrowException()
    {
        var act = () => MemberLevel.Create(Guid.NewGuid(), 5, "无效", 0, 0, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*0-4*");
    }

    [Fact]
    public void Create_NegativeLevel_ShouldThrowException()
    {
        var act = () => MemberLevel.Create(Guid.NewGuid(), -1, "无效", 0, 0, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*0-4*");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => MemberLevel.Create(Guid.NewGuid(), 1, "", 0, 0, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_NegativeMinGrowth_ShouldThrowException()
    {
        var act = () => MemberLevel.Create(Guid.NewGuid(), 1, "测试", -1, 100, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*不可为负*");
    }

    [Fact]
    public void Create_MaxLessThanOrEqualToMin_ShouldThrowException()
    {
        var act = () => MemberLevel.Create(Guid.NewGuid(), 1, "测试", 100, 50, "");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于最低*");
    }

    [Fact]
    public void Create_V0_ShouldHaveZeroMinGrowth()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, "基础");

        level.MinGrowthValue.Should().Be(0);
        level.MaxGrowthValue.Should().Be(100);
    }

    [Fact]
    public void Create_V4_ShouldHaveUnlimitedMaxGrowth()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 4, "V4", 10000, 0, "顶级");

        level.MinGrowthValue.Should().Be(10000);
        level.MaxGrowthValue.Should().Be(0);
    }

    [Fact]
    public void Matches_WithinRange_ShouldReturnTrue()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 2, "银卡", 500, 2000, "");

        level.Matches(500).Should().BeTrue();
        level.Matches(1000).Should().BeTrue();
    }

    [Fact]
    public void Matches_BelowMin_ShouldReturnFalse()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 2, "银卡", 500, 2000, "");

        level.Matches(499).Should().BeFalse();
    }

    [Fact]
    public void Matches_AboveMax_ShouldReturnFalse()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 2, "银卡", 500, 2000, "");

        level.Matches(2000).Should().BeFalse();
        level.Matches(3000).Should().BeFalse();
    }

    [Fact]
    public void Matches_UnlimitedMax_ShouldReturnTrue()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 4, "V4", 10000, 0, "");

        level.Matches(10000).Should().BeTrue();
        level.Matches(50000).Should().BeTrue();
    }

    [Fact]
    public void IsQualified_MeetsThreshold_ShouldReturnTrue()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 3, "金卡", 2000, 10000, "");

        level.IsQualified(2000).Should().BeTrue();
        level.IsQualified(5000).Should().BeTrue();
    }

    [Fact]
    public void IsQualified_BelowThreshold_ShouldReturnFalse()
    {
        var level = MemberLevel.Create(Guid.NewGuid(), 3, "金卡", 2000, 10000, "");

        level.IsQualified(1999).Should().BeFalse();
    }

    [Fact]
    public void EvaluateLevel_MatchesV3_ShouldReturn3()
    {
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 500, 2000, ""),
            MemberLevel.Create(Guid.NewGuid(), 3, "V3", 2000, 10000, ""),
            MemberLevel.Create(Guid.NewGuid(), 4, "V4", 10000, 0, "")
        };

        var level = MemberLevel.EvaluateLevel(3000, levels);

        level.Should().Be(3);
    }

    [Fact]
    public void EvaluateLevel_BelowV0_ShouldReturn0()
    {
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, "")
        };

        var level = MemberLevel.EvaluateLevel(0, levels);

        level.Should().Be(0);
    }

    [Fact]
    public void EvaluateLevel_HighGrowth_ShouldReturn4()
    {
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 500, 2000, ""),
            MemberLevel.Create(Guid.NewGuid(), 3, "V3", 2000, 10000, ""),
            MemberLevel.Create(Guid.NewGuid(), 4, "V4", 10000, 0, "")
        };

        var level = MemberLevel.EvaluateLevel(15000, levels);

        level.Should().Be(4);
    }
}

public class MemberGrowthValueTests
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_Valid_ShouldInitializeGrowthValueToZero()
    {
        var member = Member.Create(MemberId, UserId);

        member.GrowthValue.Should().Be(0);
        member.CurrentGrowthLevel.Should().Be(0);
        member.LevelChangeHistories.Should().BeEmpty();
    }

    [Fact]
    public void AddGrowthValue_Valid_ShouldIncrease()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddGrowthValue(100, "消费返积分");

        member.GrowthValue.Should().Be(100);
    }

    [Fact]
    public void AddGrowthValue_Zero_ShouldThrowException()
    {
        var member = Member.Create(MemberId, UserId);

        var act = () => member.AddGrowthValue(0, "测试");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void AddGrowthValue_Negative_ShouldThrowException()
    {
        var member = Member.Create(MemberId, UserId);

        var act = () => member.AddGrowthValue(-10, "测试");

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void AddGrowthValue_Multiple_ShouldAccumulate()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddGrowthValue(100, "消费");
        member.AddGrowthValue(200, "消费");

        member.GrowthValue.Should().Be(300);
    }

    [Fact]
    public void EvaluateGrowthLevel_UpgradeFromV0ToV2_ShouldPublishEvent()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddGrowthValue(600, "消费");
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 500, 2000, "")
        };

        member.EvaluateGrowthLevel(levels);

        member.CurrentGrowthLevel.Should().Be(2);
        member.LevelChangeHistories.Should().HaveCount(1);
        member.LevelChangeHistories[0].OldLevel.Should().Be(0);
        member.LevelChangeHistories[0].NewLevel.Should().Be(2);
        member.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void EvaluateGrowthLevel_SameLevel_ShouldNotChange()
    {
        var member = Member.Create(MemberId, UserId);
        member.AddGrowthValue(50, "消费");
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, "")
        };

        member.EvaluateGrowthLevel(levels);

        member.CurrentGrowthLevel.Should().Be(0);
        member.LevelChangeHistories.Should().BeEmpty();
        member.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateGrowthLevel_MultipleUpgrades_ShouldRecordAll()
    {
        var member = Member.Create(MemberId, UserId);
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 500, 2000, ""),
            MemberLevel.Create(Guid.NewGuid(), 3, "V3", 2000, 10000, ""),
            MemberLevel.Create(Guid.NewGuid(), 4, "V4", 10000, 0, "")
        };

        member.AddGrowthValue(600, "第一次");
        member.EvaluateGrowthLevel(levels);
        member.AddGrowthValue(2500, "第二次");
        member.EvaluateGrowthLevel(levels);

        member.CurrentGrowthLevel.Should().Be(3);
        member.LevelChangeHistories.Should().HaveCount(2);
    }
}

public class PointsExpiryTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void ExpirePoints_Valid_ShouldReduceBalance()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        account.ExpirePoints(50);

        account.Balance.Should().Be(50);
        account.DomainEvents.Should().HaveCount(2); // Earn + Expired
    }

    [Fact]
    public void ExpirePoints_Zero_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var act = () => account.ExpirePoints(0);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void ExpirePoints_Negative_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        var act = () => account.ExpirePoints(-10);

        act.Should().Throw<PointsDomainException>().WithMessage("*大于 0*");
    }

    [Fact]
    public void ExpirePoints_InsufficientBalance_ShouldThrowException()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 30, "签到");

        var act = () => account.ExpirePoints(50);

        act.Should().Throw<PointsDomainException>().WithMessage("*余额不足*");
    }

    [Fact]
    public void ExpirePoints_AllBalance_ShouldResultInZero()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        account.ExpirePoints(100);

        account.Balance.Should().Be(0);
    }

    [Fact]
    public void ExpirePoints_ShouldPublishExpiredEvent()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.CheckIn, 100, "签到");

        account.ExpirePoints(30);

        var expiredEvent = account.DomainEvents.OfType<PointsExpiredEvent>().Single();
        expiredEvent.UserId.Should().Be(UserId);
        expiredEvent.Points.Should().Be(30);
    }
}

public class MemberLevelChangeHistoryTests
{
    [Fact]
    public void Constructor_Valid_ShouldCreate()
    {
        var now = DateTime.UtcNow;
        var history = new MemberLevelChangeHistory(0, 2, 600, now, "评估升级");

        history.OldLevel.Should().Be(0);
        history.NewLevel.Should().Be(2);
        history.GrowthValue.Should().Be(600);
        history.ChangedAt.Should().Be(now);
        history.Reason.Should().Be("评估升级");
    }
}