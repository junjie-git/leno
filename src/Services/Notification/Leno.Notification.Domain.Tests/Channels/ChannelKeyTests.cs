using Leno.Notification.Domain.Channels;

namespace Leno.Notification.Domain.Tests.Channels;

/// <summary>
/// ChannelKey 强类型字符串单元测试，覆盖隐式转换 / 相等性 / 预定义值。
/// </summary>
public class ChannelKeyTests
{
    #region 预定义值

    [Fact]
    public void Sms_PredefinedKey_ShouldHaveExpectedValue()
    {
        var key = ChannelKey.Sms;

        key.Value.Should().Be("Sms");
        ((string)key).Should().Be("Sms");
        key.ToString().Should().Be("Sms");
    }

    [Fact]
    public void Email_PredefinedKey_ShouldHaveExpectedValue()
    {
        var key = ChannelKey.Email;

        key.Value.Should().Be("Email");
        ((string)key).Should().Be("Email");
    }

    [Fact]
    public void InApp_PredefinedKey_ShouldHaveExpectedValue()
    {
        var key = ChannelKey.InApp;

        key.Value.Should().Be("InApp");
        ((string)key).Should().Be("InApp");
    }

    [Fact]
    public void Push_PredefinedKey_ShouldHaveExpectedValue()
    {
        var key = ChannelKey.Push;

        key.Value.Should().Be("Push");
    }

    [Fact]
    public void IM_PredefinedKey_ShouldHaveExpectedValue()
    {
        var key = ChannelKey.IM;

        key.Value.Should().Be("IM");
    }

    [Fact]
    public void Webhook_PredefinedKey_ShouldHaveExpectedValue()
    {
        var key = ChannelKey.Webhook;

        key.Value.Should().Be("Webhook");
    }

    #endregion

    #region 隐式转换

    [Fact]
    public void ImplicitConversion_ToString_ShouldReturnValue()
    {
        ChannelKey key = ChannelKey.Sms;
        string value = key;

        value.Should().Be("Sms");
    }

    [Fact]
    public void ImplicitConversion_FromString_ShouldCreateKey()
    {
        ChannelKey key = "CustomChannel";

        key.Value.Should().Be("CustomChannel");
    }

    [Fact]
    public void ImplicitConversion_NullString_ShouldProduceEmptyValue()
    {
        // readonly record struct 接收 null 时 Value 为 null
#pragma warning disable CS8625
        ChannelKey key = (string)null;
#pragma warning restore CS8625

        key.Value.Should().BeNull();
        key.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_EmptyString_ShouldProduceEmptyKey()
    {
        ChannelKey key = string.Empty;

        key.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region 相等性

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        var key1 = ChannelKey.Sms;
        var key2 = new ChannelKey("Sms");

        key1.Should().Be(key2);
        (key1 == key2).Should().BeTrue();
        (key1 != key2).Should().BeFalse();
        key1.GetHashCode().Should().Be(key2.GetHashCode());
    }

    [Fact]
    public void Inequality_DifferentValue_ShouldNotBeEqual()
    {
        var key1 = ChannelKey.Sms;
        var key2 = ChannelKey.Email;

        key1.Should().NotBe(key2);
        (key1 != key2).Should().BeTrue();
        (key1 == key2).Should().BeFalse();
    }

    [Fact]
    public void Equality_CustomKey_ShouldCompareByValue()
    {
        var key1 = new ChannelKey("Custom");
        var key2 = new ChannelKey("Custom");

        key1.Should().Be(key2);
        key1.GetHashCode().Should().Be(key2.GetHashCode());
    }

    [Fact]
    public void Equality_EmptyKeys_ShouldBeEqual()
    {
        var key1 = ChannelKey.Empty;
        var key2 = new ChannelKey(string.Empty);

        key1.Should().Be(key2);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var key = new ChannelKey("Webhook");

        key.ToString().Should().Be("Webhook");
    }

    [Fact]
    public void ToString_EmptyKey_ShouldReturnEmptyString()
    {
        var key = ChannelKey.Empty;

        key.ToString().Should().Be(string.Empty);
    }

    #endregion

    #region IsEmpty

    [Fact]
    public void IsEmpty_EmptyKey_ShouldReturnTrue()
    {
        ChannelKey key = ChannelKey.Empty;

        key.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhitespaceKey_ShouldReturnTrue()
    {
        var key = new ChannelKey("   ");

        key.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_NonEmptyKey_ShouldReturnFalse()
    {
        var key = ChannelKey.Sms;

        key.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_NullValue_ShouldReturnTrue()
    {
#pragma warning disable CS8625
        var key = new ChannelKey(null);
#pragma warning restore CS8625

        key.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region 字典查找

    [Fact]
    public void DictionaryLookup_ByChannelKey_ShouldFindValue()
    {
        var dict = new Dictionary<ChannelKey, string>
        {
            [ChannelKey.Sms] = "短信",
            [ChannelKey.Email] = "邮件",
            [ChannelKey.InApp] = "站内信"
        };

        dict.TryGetValue(ChannelKey.Sms, out var value).Should().BeTrue();
        value.Should().Be("短信");

        dict.TryGetValue(new ChannelKey("Sms"), out var value2).Should().BeTrue();
        value2.Should().Be("短信");
    }

    [Fact]
    public void DictionaryLookup_UnregisteredKey_ShouldNotFindValue()
    {
        var dict = new Dictionary<ChannelKey, string>
        {
            [ChannelKey.Sms] = "短信"
        };

        dict.TryGetValue(ChannelKey.Push, out _).Should().BeFalse();
    }

    #endregion
}
