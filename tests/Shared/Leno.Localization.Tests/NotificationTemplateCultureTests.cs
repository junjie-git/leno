using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Localization.Tests;

/// <summary>
/// NotificationTemplateCulture 值对象单元测试（spec 6.2.13）。
/// 覆盖合法/非法 culture、Default 值、相等性比较。
/// </summary>
public sealed class NotificationTemplateCultureTests
{
    // ===== Default 值测试 =====

    /// <summary>
    /// Default 应为 zh-CN（当前阶段默认文化）。
    /// </summary>
    [Fact]
    public void Default_ShouldBeZhCN()
    {
        var defaultCulture = NotificationTemplateCulture.Default;

        defaultCulture.Culture.Should().Be("zh-CN");
    }

    /// <summary>
    /// Default.IsDefault 应为 true。
    /// </summary>
    [Fact]
    public void Default_IsDefault_ShouldBeTrue()
    {
        var defaultCulture = NotificationTemplateCulture.Default;

        defaultCulture.IsDefault.Should().BeTrue();
    }

    // ===== 合法 culture 测试 =====

    /// <summary>
    /// Create 合法 BCP 47 文化（zh-CN）应成功并保留原值。
    /// </summary>
    [Fact]
    public void Create_ValidZhCN_ShouldSucceed()
    {
        var culture = NotificationTemplateCulture.Create("zh-CN");

        culture.Culture.Should().Be("zh-CN");
        culture.IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// Create 合法 BCP 47 文化（en-US）应成功并保留原值。
    /// </summary>
    [Fact]
    public void Create_ValidEnUS_ShouldSucceed()
    {
        var culture = NotificationTemplateCulture.Create("en-US");

        culture.Culture.Should().Be("en-US");
        culture.IsDefault.Should().BeFalse();
    }

    /// <summary>
    /// Create 合法 BCP 47 文化（ja-JP）应成功并保留原值。
    /// </summary>
    [Fact]
    public void Create_ValidJaJP_ShouldSucceed()
    {
        var culture = NotificationTemplateCulture.Create("ja-JP");

        culture.Culture.Should().Be("ja-JP");
        culture.IsDefault.Should().BeFalse();
    }

    /// <summary>
    /// Create 应自动 trim 前后空白。
    /// </summary>
    [Fact]
    public void Create_WithWhitespace_ShouldTrim()
    {
        var culture = NotificationTemplateCulture.Create("  zh-CN  ");

        culture.Culture.Should().Be("zh-CN");
    }

    /// <summary>
    /// Create 不区分大小写比较 —— "ZH-cn" 应视为默认文化。
    /// </summary>
    [Fact]
    public void Create_CaseInsensitive_ShouldBeDefault()
    {
        var culture = NotificationTemplateCulture.Create("ZH-cn");

        culture.Culture.Should().Be("ZH-cn");
        culture.IsDefault.Should().BeTrue();
    }

    // ===== 非法 culture 测试 =====

    /// <summary>
    /// Create 空字符串应抛出 NotificationDomainException。
    /// </summary>
    [Fact]
    public void Create_EmptyString_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationTemplateCulture.Create("");

        var ex = act.Should().Throw<NotificationDomainException>().Which;
        ex.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CULTURE_INVALID");
    }

    /// <summary>
    /// Create null 应抛出 NotificationDomainException。
    /// </summary>
    [Fact]
    public void Create_Null_ShouldThrowNotificationDomainException()
    {
#pragma warning disable CS8625 // 测试场景：模拟 null 入参
        var act = () => NotificationTemplateCulture.Create(null!);
#pragma warning restore CS8625

        act.Should().Throw<NotificationDomainException>()
            .Which.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CULTURE_INVALID");
    }

    /// <summary>
    /// Create 纯空白字符串应抛出 NotificationDomainException。
    /// </summary>
    [Fact]
    public void Create_WhitespaceOnly_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationTemplateCulture.Create("   ");

        act.Should().Throw<NotificationDomainException>()
            .Which.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CULTURE_INVALID");
    }

    /// <summary>
    /// Create 非法 BCP 47 标识（如 "xx-99"）应抛出 NotificationDomainException。
    /// </summary>
    [Fact]
    public void Create_InvalidBcp47_ShouldThrowNotificationDomainException()
    {
        var act = () => NotificationTemplateCulture.Create("xx-99");

        act.Should().Throw<NotificationDomainException>()
            .Which.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CULTURE_INVALID");
    }

    /// <summary>
    /// Create 超长文化标识（> 16 字）应抛出 NotificationDomainException。
    /// </summary>
    [Fact]
    public void Create_TooLong_ShouldThrowNotificationDomainException()
    {
        var longCulture = new string('a', 17);

        var act = () => NotificationTemplateCulture.Create(longCulture);

        act.Should().Throw<NotificationDomainException>()
            .Which.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CULTURE_INVALID");
    }

    // ===== TryCreate 测试 =====

    /// <summary>
    /// TryCreate 合法 culture 应返回值对象。
    /// </summary>
    [Fact]
    public void TryCreate_ValidCulture_ShouldReturnValue()
    {
        var culture = NotificationTemplateCulture.TryCreate("en-US");

        culture.Should().NotBeNull();
        culture!.Culture.Should().Be("en-US");
    }

    /// <summary>
    /// TryCreate null 应返回 null（用于 EF Core 值转换回退）。
    /// </summary>
    [Fact]
    public void TryCreate_Null_ShouldReturnNull()
    {
        var culture = NotificationTemplateCulture.TryCreate(null);

        culture.Should().BeNull();
    }

    /// <summary>
    /// TryCreate 空字符串应返回 null。
    /// </summary>
    [Fact]
    public void TryCreate_EmptyString_ShouldReturnNull()
    {
        var culture = NotificationTemplateCulture.TryCreate("");

        culture.Should().BeNull();
    }

    /// <summary>
    /// TryCreate 非法 culture 应返回 null（不抛异常）。
    /// </summary>
    [Fact]
    public void TryCreate_InvalidCulture_ShouldReturnNull()
    {
        var culture = NotificationTemplateCulture.TryCreate("xx-99");

        culture.Should().BeNull();
    }

    // ===== 相等性测试 =====

    /// <summary>
    /// 相同文化的两个值对象应相等。
    /// </summary>
    [Fact]
    public void Equals_SameCulture_ShouldBeEqual()
    {
        var c1 = NotificationTemplateCulture.Create("zh-CN");
        var c2 = NotificationTemplateCulture.Create("zh-CN");

        c1.Equals(c2).Should().BeTrue();
        (c1 == c2).Should().BeTrue();
        (c1 != c2).Should().BeFalse();
    }

    /// <summary>
    /// 不同文化的两个值对象应不相等。
    /// </summary>
    [Fact]
    public void Equals_DifferentCulture_ShouldNotBeEqual()
    {
        var c1 = NotificationTemplateCulture.Create("zh-CN");
        var c2 = NotificationTemplateCulture.Create("en-US");

        c1.Equals(c2).Should().BeFalse();
        (c1 == c2).Should().BeFalse();
        (c1 != c2).Should().BeTrue();
    }

    /// <summary>
    /// 不区分大小写比较 —— "zh-CN" 与 "ZH-CN" 应相等。
    /// </summary>
    [Fact]
    public void Equals_CaseInsensitive_ShouldBeEqual()
    {
        var c1 = NotificationTemplateCulture.Create("zh-CN");
        var c2 = NotificationTemplateCulture.Create("ZH-CN");

        c1.Equals(c2).Should().BeTrue();
    }

    /// <summary>
    /// 与 null 比较应返回 false。
    /// </summary>
    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var c1 = NotificationTemplateCulture.Create("zh-CN");

        c1.Equals(null).Should().BeFalse();
        (c1 == null).Should().BeFalse();
    }

    /// <summary>
    /// 两个 null 值对象应相等（运算符重载）。
    /// </summary>
    [Fact]
    public void Equals_BothNull_ShouldBeEqual()
    {
        NotificationTemplateCulture? c1 = null;
        NotificationTemplateCulture? c2 = null;

        (c1 == c2).Should().BeTrue();
    }

    /// <summary>
    /// GetHashCode 对相同文化应返回相同哈希。
    /// </summary>
    [Fact]
    public void GetHashCode_SameCulture_ShouldBeEqual()
    {
        var c1 = NotificationTemplateCulture.Create("zh-CN");
        var c2 = NotificationTemplateCulture.Create("zh-CN");

        c1.GetHashCode().Should().Be(c2.GetHashCode());
    }

    /// <summary>
    /// ToString 应返回文化标识。
    /// </summary>
    [Fact]
    public void ToString_ShouldReturnCultureString()
    {
        var culture = NotificationTemplateCulture.Create("en-US");

        culture.ToString().Should().Be("en-US");
    }
}
