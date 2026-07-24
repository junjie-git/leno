using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Localization.Tests;

/// <summary>
/// NotificationTemplate 聚合根 Culture 维度默认行为单元测试。
/// 验证 null Culture = zh-CN 默认行为不变（国际化预留扩展位核心验收标准）。
/// </summary>
public sealed class NotificationTemplateCultureDefaultBehaviorTests
{
    /// <summary>
    /// 构造辅助：创建合法的模板变量列表。
    /// </summary>
    private static List<TemplateVariable> CreateVariables() =>
        new() { TemplateVariable.Create("userName") };

    /// <summary>
    /// Create 不传 culture 时，Culture 应为 null（默认行为不变）。
    /// </summary>
    [Fact]
    public void Create_WithoutCulture_ShouldHaveNullCulture()
    {
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "订单创建通知",
            NotificationChannel.Sms,
            "订单已创建",
            "您的订单 {{orderNo}} 已创建",
            CreateVariables());

        template.Culture.Should().BeNull();
    }

    /// <summary>
    /// Create 不传 culture 时，EffectiveCulture 应回退到 Default（zh-CN）。
    /// </summary>
    [Fact]
    public void Create_WithoutCulture_EffectiveCultureShouldBeDefault()
    {
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "订单创建通知",
            NotificationChannel.Sms,
            "订单已创建",
            "您的订单 {{orderNo}} 已创建",
            CreateVariables());

        template.EffectiveCulture.Should().Be(NotificationTemplateCulture.Default);
        template.EffectiveCulture.Culture.Should().Be("zh-CN");
    }

    /// <summary>
    /// Create 显式传 null culture 时，Culture 应为 null（默认行为不变）。
    /// </summary>
    [Fact]
    public void Create_WithNullCulture_ShouldHaveNullCulture()
    {
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "订单创建通知",
            NotificationChannel.Sms,
            "订单已创建",
            "您的订单 {{orderNo}} 已创建",
            CreateVariables(),
            culture: null);

        template.Culture.Should().BeNull();
        template.EffectiveCulture.Should().Be(NotificationTemplateCulture.Default);
    }

    /// <summary>
    /// Create 传入具体 culture（en-US）时，Culture 应为该值，EffectiveCulture 应为该值。
    /// </summary>
    [Fact]
    public void Create_WithSpecificCulture_ShouldHaveThatCulture()
    {
        var enUs = NotificationTemplateCulture.Create("en-US");

        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "Order Created Notification",
            NotificationChannel.Sms,
            "Order Created",
            "Your order {{orderNo}} has been created",
            CreateVariables(),
            culture: enUs);

        template.Culture.Should().Be(enUs);
        template.EffectiveCulture.Should().Be(enUs);
        template.EffectiveCulture.Culture.Should().Be("en-US");
    }

    /// <summary>
    /// UpdateCulture 设置具体 culture 后，Culture 应更新，EffectiveCulture 应为该值。
    /// </summary>
    [Fact]
    public void UpdateCulture_WithSpecificCulture_ShouldUpdate()
    {
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "订单创建通知",
            NotificationChannel.Sms,
            "订单已创建",
            "您的订单 {{orderNo}} 已创建",
            CreateVariables());

        template.Culture.Should().BeNull();

        var enUs = NotificationTemplateCulture.Create("en-US");
        template.UpdateCulture(enUs);

        template.Culture.Should().Be(enUs);
        template.EffectiveCulture.Should().Be(enUs);
    }

    /// <summary>
    /// UpdateCulture 传 null 应将 Culture 重置为 null（回退到 zh-CN 默认行为）。
    /// </summary>
    [Fact]
    public void UpdateCulture_WithNull_ShouldResetToNull()
    {
        var enUs = NotificationTemplateCulture.Create("en-US");
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "Order Created",
            NotificationChannel.Sms,
            "Order Created",
            "Your order {{orderNo}} has been created",
            CreateVariables(),
            culture: enUs);

        template.Culture.Should().Be(enUs);

        template.UpdateCulture(null);

        template.Culture.Should().BeNull();
        template.EffectiveCulture.Should().Be(NotificationTemplateCulture.Default);
    }

    /// <summary>
    /// 默认行为验证：不传 culture 创建的模板，EffectiveCulture.IsDefault 应为 true（zh-CN）。
    /// </summary>
    [Fact]
    public void Create_WithoutCulture_EffectiveCultureIsDefaultShouldBeTrue()
    {
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "订单创建通知",
            NotificationChannel.Sms,
            "订单已创建",
            "您的订单 {{orderNo}} 已创建",
            CreateVariables());

        template.EffectiveCulture.IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// 默认行为验证：传入 en-US culture 创建的模板，EffectiveCulture.IsDefault 应为 false。
    /// </summary>
    [Fact]
    public void Create_WithEnUSCulture_EffectiveCultureIsDefaultShouldBeFalse()
    {
        var enUs = NotificationTemplateCulture.Create("en-US");
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "OrderCreated",
            "Order Created",
            NotificationChannel.Sms,
            "Order Created",
            "Your order {{orderNo}} has been created",
            CreateVariables(),
            culture: enUs);

        template.EffectiveCulture.IsDefault.Should().BeFalse();
    }
}
