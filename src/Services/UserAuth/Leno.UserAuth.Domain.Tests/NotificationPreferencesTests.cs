using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Tests;

public class NotificationPreferencesTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateDefaultPreferences()
    {
        var id = Guid.NewGuid();

        var preferences = NotificationPreferences.Create(id, UserId);

        preferences.Id.Should().Be(id);
        preferences.UserId.Should().Be(UserId);
        preferences.DndEnabled.Should().BeFalse();
        preferences.DndStart.Should().BeNull();
        preferences.DndEnd.Should().BeNull();
        // 默认包含全部 7 种事件类型
        preferences.Items.Should().HaveCount(7);
        // 每项 InApp 默认开启，Sms/Email 默认关闭
        foreach (var item in preferences.Items)
        {
            item.InAppEnabled.Should().BeTrue("站内信默认开启");
            item.SmsEnabled.Should().BeFalse("短信默认关闭");
            item.EmailEnabled.Should().BeFalse("邮件默认关闭");
        }
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => NotificationPreferences.Create(Guid.Empty, UserId);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*标识不可为空*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => NotificationPreferences.Create(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*用户标识不可为空*");
    }

    #endregion

    #region UpdateChannel

    [Fact]
    public void UpdateChannel_EnableSms_ShouldUpdateSmsOnly()
    {
        var preferences = CreateDefault();

        preferences.UpdateChannel(NotificationEventType.OrderStatus, NotificationChannel.Sms, true);

        var item = preferences.Items.First(i => i.EventType == NotificationEventType.OrderStatus);
        item.SmsEnabled.Should().BeTrue();
        item.EmailEnabled.Should().BeFalse();
        item.InAppEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateChannel_DisableInApp_ShouldThrowException()
    {
        var preferences = CreateDefault();

        var act = () => preferences.UpdateChannel(
            NotificationEventType.OrderStatus, NotificationChannel.InApp, false);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*站内信*不可关闭*");
    }

    [Fact]
    public void UpdateChannel_EnableInApp_ShouldRemainTrue()
    {
        var preferences = CreateDefault();

        preferences.UpdateChannel(NotificationEventType.OrderStatus, NotificationChannel.InApp, true);

        var item = preferences.Items.First(i => i.EventType == NotificationEventType.OrderStatus);
        item.InAppEnabled.Should().BeTrue();
    }

    #endregion

    #region ReplaceAll

    [Fact]
    public void ReplaceAll_ShouldReplaceMatrixButKeepInAppEnabled()
    {
        var preferences = CreateDefault();

        var settings = new[]
        {
            (NotificationEventType.OrderStatus, NotificationChannel.Sms, true),
            (NotificationEventType.OrderStatus, NotificationChannel.Email, true),
            (NotificationEventType.OrderStatus, NotificationChannel.InApp, false), // 应被强制改为 true
            (NotificationEventType.SystemNotice, NotificationChannel.Sms, true)
        };

        preferences.ReplaceAll(settings);

        var orderItem = preferences.Items.First(i => i.EventType == NotificationEventType.OrderStatus);
        orderItem.InAppEnabled.Should().BeTrue("站内信强制开启");
        orderItem.SmsEnabled.Should().BeTrue();
        orderItem.EmailEnabled.Should().BeTrue();

        var systemItem = preferences.Items.First(i => i.EventType == NotificationEventType.SystemNotice);
        systemItem.InAppEnabled.Should().BeTrue();
        systemItem.SmsEnabled.Should().BeTrue();
        systemItem.EmailEnabled.Should().BeFalse();

        // 未在 settings 中显式覆盖的事件保留默认值
        var logisticsItem = preferences.Items.First(i => i.EventType == NotificationEventType.LogisticsUpdate);
        logisticsItem.InAppEnabled.Should().BeTrue();
        logisticsItem.SmsEnabled.Should().BeFalse();
        logisticsItem.EmailEnabled.Should().BeFalse();
    }

    [Fact]
    public void ReplaceAll_NullSettings_ShouldThrowException()
    {
        var preferences = CreateDefault();

        var act = () => preferences.ReplaceAll(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateDnd

    [Fact]
    public void UpdateDnd_Enable_ShouldSetTimes()
    {
        var preferences = CreateDefault();
        var start = new TimeSpan(22, 0, 0);
        var end = new TimeSpan(8, 0, 0);

        preferences.UpdateDnd(true, start, end);

        preferences.DndEnabled.Should().BeTrue();
        preferences.DndStart.Should().Be(start);
        preferences.DndEnd.Should().Be(end);
    }

    [Fact]
    public void UpdateDnd_EnableWithoutTimes_ShouldThrowException()
    {
        var preferences = CreateDefault();

        var act = () => preferences.UpdateDnd(true, null, null);

        act.Should().Throw<UserAuthDomainException>()
            .WithMessage("*免打扰时必须同时提供起止时间*");
    }

    [Fact]
    public void UpdateDnd_Disable_ShouldClearTimes()
    {
        var preferences = CreateDefault();
        preferences.UpdateDnd(true, new TimeSpan(22, 0, 0), new TimeSpan(8, 0, 0));

        preferences.UpdateDnd(false, null, null);

        preferences.DndEnabled.Should().BeFalse();
        preferences.DndStart.Should().BeNull();
        preferences.DndEnd.Should().BeNull();
    }

    #endregion

    private static NotificationPreferences CreateDefault()
        => NotificationPreferences.Create(Guid.NewGuid(), UserId);
}
