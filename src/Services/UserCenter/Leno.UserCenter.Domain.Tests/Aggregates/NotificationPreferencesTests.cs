using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Exceptions;
using Leno.UserCenter.Domain.ValueObjects;

namespace Leno.UserCenter.Domain.Tests.Aggregates;

/// <summary>
/// NotificationPreferences（通知偏好聚合）单元测试。
/// 重点覆盖两条业务不变量：
/// - INV-NP-01：站内信（InApp）渠道默认开启且不可关闭；
/// - INV-NP-02：启用免打扰必须同时提供起止时间。
/// </summary>
public class NotificationPreferencesTests
{
    private static NotificationPreferences Create() => NotificationPreferences.Create(Guid.NewGuid(), Guid.NewGuid());

    #region Create 默认值

    [Fact]
    public void Create_Should_Initialize_All_Event_Types_With_Default_Matrix()
    {
        var preferences = Create();

        preferences.Items.Should().HaveCount(Enum.GetValues<NotificationEventType>().Length,
            "每个事件类型都应有一条默认偏好项");
        preferences.Items.Should().OnlyContain(i => i.InAppEnabled,
            "INV-NP-01：InApp 渠道默认开启");
        preferences.Items.Should().OnlyContain(i => !i.SmsEnabled && !i.EmailEnabled,
            "Sms/Email 渠道默认关闭");
        preferences.DndEnabled.Should().BeFalse();
        preferences.DndStart.Should().BeNull();
        preferences.DndEnd.Should().BeNull();
    }

    [Fact]
    public void Create_With_Empty_Ids_Should_Throw_DomainException()
    {
        var createEmptyId = () => NotificationPreferences.Create(Guid.Empty, Guid.NewGuid());
        var createEmptyUser = () => NotificationPreferences.Create(Guid.NewGuid(), Guid.Empty);

        createEmptyId.Should().Throw<UserCenterDomainException>().WithMessage("*标识不可为空*");
        createEmptyUser.Should().Throw<UserCenterDomainException>().WithMessage("*用户标识不可为空*");
    }

    #endregion

    #region UpdateChannel（INV-NP-01）

    [Fact]
    public void UpdateChannel_With_Sms_Should_Toggle_Sms_Only()
    {
        var preferences = Create();

        preferences.UpdateChannel(NotificationEventType.OrderStatus, NotificationChannel.Sms, enabled: true);

        var item = preferences.Items.Single(i => i.EventType == NotificationEventType.OrderStatus);
        item.SmsEnabled.Should().BeTrue();
        item.InAppEnabled.Should().BeTrue("InApp 不受其他渠道开关影响");
        item.EmailEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateChannel_With_InApp_Disable_Should_Throw_DomainException()
    {
        var preferences = Create();

        var update = () => preferences.UpdateChannel(
            NotificationEventType.OrderStatus, NotificationChannel.InApp, enabled: false);

        update.Should().Throw<UserCenterDomainException>()
            .WithMessage("*站内信渠道默认开启且不可关闭*");
    }

    [Fact]
    public void UpdateChannel_With_Unknown_Event_Type_Should_Append_Item_With_Defaults()
    {
        var preferences = Create();

        preferences.UpdateChannel(NotificationEventType.SystemNotice, NotificationChannel.Email, enabled: true);

        preferences.Items.Should().Contain(i => i.EventType == NotificationEventType.SystemNotice);
        var item = preferences.Items.Single(i => i.EventType == NotificationEventType.SystemNotice);
        item.EmailEnabled.Should().BeTrue();
        item.InAppEnabled.Should().BeTrue("追加项的 InApp 按默认强制开启");
    }

    #endregion

    #region ReplaceAll（整表保存，InApp 强制开启）

    [Fact]
    public void ReplaceAll_Should_Reset_Then_Apply_Settings_Forcing_InApp_On()
    {
        var preferences = Create();
        preferences.UpdateChannel(NotificationEventType.OrderStatus, NotificationChannel.Sms, enabled: true);

        preferences.ReplaceAll(
        [
            (NotificationEventType.CouponArrival, NotificationChannel.Sms, true),
            (NotificationEventType.CouponArrival, NotificationChannel.InApp, false), // 应被忽略并强制为 true
            (NotificationEventType.PointsEarned, NotificationChannel.Email, true),
        ]);

        var coupon = preferences.Items.Single(i => i.EventType == NotificationEventType.CouponArrival);
        coupon.SmsEnabled.Should().BeTrue();
        coupon.InAppEnabled.Should().BeTrue("整表保存时 InApp 前端传 false 也被强制为开启");

        var order = preferences.Items.Single(i => i.EventType == NotificationEventType.OrderStatus);
        order.SmsEnabled.Should().BeFalse("ReplaceAll 先重置为默认，未提交的开关应回到关闭");

        var points = preferences.Items.Single(i => i.EventType == NotificationEventType.PointsEarned);
        points.EmailEnabled.Should().BeTrue();
    }

    [Fact]
    public void ReplaceAll_With_Null_Settings_Should_Throw()
    {
        var preferences = Create();

        var replace = () => preferences.ReplaceAll(null!);

        replace.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateDnd（INV-NP-02）

    [Fact]
    public void UpdateDnd_Enabled_With_Times_Should_Persist()
    {
        var preferences = Create();
        var start = new TimeSpan(22, 0, 0);
        var end = new TimeSpan(8, 0, 0);

        preferences.UpdateDnd(enabled: true, start, end);

        preferences.DndEnabled.Should().BeTrue();
        preferences.DndStart.Should().Be(start);
        preferences.DndEnd.Should().Be(end);
    }

    [Fact]
    public void UpdateDnd_Enabled_Missing_Times_Should_Throw_DomainException()
    {
        var preferences = Create();

        var update = () => preferences.UpdateDnd(enabled: true, start: new TimeSpan(22, 0, 0), end: null);

        update.Should().Throw<UserCenterDomainException>()
            .WithMessage("*必须同时提供起止时间*");
    }

    [Fact]
    public void UpdateDnd_Disabled_Should_Clear_Times()
    {
        var preferences = Create();
        preferences.UpdateDnd(enabled: true, new TimeSpan(22, 0, 0), new TimeSpan(8, 0, 0));

        preferences.UpdateDnd(enabled: false, null, null);

        preferences.DndEnabled.Should().BeFalse();
        preferences.DndStart.Should().BeNull();
        preferences.DndEnd.Should().BeNull();
    }

    #endregion
}
