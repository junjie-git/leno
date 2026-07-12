using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Domain.Tests;

public class PaymentChannelConfigTests
{
    [Fact]
    public void Create_Valid_ShouldCreateEnabledConfig()
    {
        var config = PaymentChannelConfig.Create(
            Guid.NewGuid(),
            PaymentChannel.WeChatPay,
            "ApiKey",
            "encrypted_value_1234567890",
            "微信支付 API 密钥");

        config.Channel.Should().Be(PaymentChannel.WeChatPay);
        config.ConfigName.Should().Be("ApiKey");
        config.ConfigValue.Should().Be("encrypted_value_1234567890");
        config.Description.Should().Be("微信支付 API 密钥");
        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => PaymentChannelConfig.Create(
            Guid.Empty,
            PaymentChannel.WeChatPay,
            "ApiKey",
            "encrypted_value",
            null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*ConfigId*");
    }

    [Fact]
    public void Create_DefaultChannel_ShouldThrowException()
    {
        var act = () => PaymentChannelConfig.Create(
            Guid.NewGuid(),
            (PaymentChannel)999,
            "ApiKey",
            "encrypted_value",
            null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*渠道*");
    }

    [Fact]
    public void Create_EmptyConfigName_ShouldThrowException()
    {
        var act = () => PaymentChannelConfig.Create(
            Guid.NewGuid(),
            PaymentChannel.WeChatPay,
            "",
            "encrypted_value",
            null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*配置项名称*");
    }

    [Fact]
    public void Create_WhitespaceConfigName_ShouldThrowException()
    {
        var act = () => PaymentChannelConfig.Create(
            Guid.NewGuid(),
            PaymentChannel.WeChatPay,
            "   ",
            "encrypted_value",
            null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*配置项名称*");
    }

    [Fact]
    public void Create_EmptyConfigValue_ShouldThrowException()
    {
        var act = () => PaymentChannelConfig.Create(
            Guid.NewGuid(),
            PaymentChannel.WeChatPay,
            "ApiKey",
            "",
            null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*配置项值*");
    }

    [Fact]
    public void Create_WhitespaceConfigValue_ShouldThrowException()
    {
        var act = () => PaymentChannelConfig.Create(
            Guid.NewGuid(),
            PaymentChannel.WeChatPay,
            "ApiKey",
            "   ",
            null);

        act.Should().Throw<PaymentDomainException>().WithMessage("*配置项值*");
    }

    [Fact]
    public void Enable_WhenDisabled_ShouldEnable()
    {
        var config = CreateConfig();
        config.Disable();

        config.Enable();

        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ShouldThrowException()
    {
        var config = CreateConfig();

        var act = () => config.Enable();

        act.Should().Throw<PaymentDomainException>().WithMessage("*已启用*");
    }

    [Fact]
    public void Disable_WhenEnabled_ShouldDisable()
    {
        var config = CreateConfig();

        config.Disable();

        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ShouldThrowException()
    {
        var config = CreateConfig();
        config.Disable();

        var act = () => config.Disable();

        act.Should().Throw<PaymentDomainException>().WithMessage("*已禁用*");
    }

    [Fact]
    public void UpdateConfigValue_Valid_ShouldUpdateValue()
    {
        var config = CreateConfig();

        config.UpdateConfigValue("new_encrypted_value");

        config.ConfigValue.Should().Be("new_encrypted_value");
    }

    [Fact]
    public void UpdateConfigValue_Empty_ShouldThrowException()
    {
        var config = CreateConfig();

        var act = () => config.UpdateConfigValue("");

        act.Should().Throw<PaymentDomainException>().WithMessage("*配置项值*");
    }

    [Fact]
    public void Enable_ShouldAddDomainEvent()
    {
        var config = CreateConfig();
        config.Disable();
        config.ClearDomainEvents();

        config.Enable();

        config.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Disable_ShouldAddDomainEvent()
    {
        var config = CreateConfig();

        config.Disable();

        config.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateConfigValue_ShouldAddDomainEvent()
    {
        var config = CreateConfig();

        config.UpdateConfigValue("new_value");

        config.DomainEvents.Should().HaveCount(1);
    }

    private static PaymentChannelConfig CreateConfig()
    {
        return PaymentChannelConfig.Create(
            Guid.NewGuid(),
            PaymentChannel.Alipay,
            "AppId",
            "encrypted_app_id_12345",
            "支付宝应用标识");
    }
}