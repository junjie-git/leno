using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Channels;
using Moq;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// 阶段三 3.8 单元测试：验证 <see cref="PaymentChannelFactory"/> 通过 DI 注入
/// <c>IEnumerable&lt;IPaymentChannelAdapter&gt;</c> 并按 <see cref="IPaymentChannelAdapter.ChannelKey"/>
/// 构建字典查找的能力，取代原 switch/if-else 分支。
///
/// 覆盖场景：
/// - 按 ChannelKey 字符串查找（含大小写不敏感）
/// - 按 <see cref="PaymentChannel"/> 枚举查找（向后兼容入口）
/// - 禁用渠道（IsEnabled=false）被排除
/// - 未知渠道 Key 抛 <see cref="PaymentDomainException"/>
/// - 空/null Key 抛 <see cref="ArgumentException"/>
/// - ListEnabledChannels / ListEnabledMetadata 正确返回
/// </summary>
public class PaymentChannelFactoryTests
{
    /// <summary>
    /// 构造 Mock 适配器，设置 ChannelKey/DisplayName/Capabilities/IsEnabled。
    /// </summary>
    private static IPaymentChannelAdapter CreateMockAdapter(
        string channelKey,
        string displayName,
        bool isEnabled,
        PaymentChannelCapabilities? capabilities = null)
    {
        var mock = new Mock<IPaymentChannelAdapter>();
        mock.SetupGet(a => a.ChannelKey).Returns(channelKey);
        mock.SetupGet(a => a.DisplayName).Returns(displayName);
        mock.SetupGet(a => a.IsEnabled).Returns(isEnabled);
        mock.SetupGet(a => a.Capabilities).Returns(capabilities ?? PaymentChannelCapabilities.Default);
        return mock.Object;
    }

    [Fact]
    public void GetAdapter_ByChannelKey_ShouldReturnMatchingAdapter()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var alipay = CreateMockAdapter("Alipay", "支付宝", isEnabled: true);
        var factory = new PaymentChannelFactory(new[] { weChat, alipay });

        var result = factory.GetAdapter("Alipay");

        result.Should().BeSameAs(alipay);
    }

    [Fact]
    public void GetAdapter_ByChannelKey_CaseInsensitive_ShouldReturnMatchingAdapter()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var factory = new PaymentChannelFactory(new[] { weChat });

        var lower = factory.GetAdapter("wechatpay");
        var upper = factory.GetAdapter("WECHATPAY");
        var mixed = factory.GetAdapter("WeChatPay");

        lower.Should().BeSameAs(weChat);
        upper.Should().BeSameAs(weChat);
        mixed.Should().BeSameAs(weChat);
    }

    [Fact]
    public void GetAdapter_ByEnum_ShouldReturnAdapterWhoseKeyMatchesEnumName()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var alipay = CreateMockAdapter("Alipay", "支付宝", isEnabled: true);
        var factory = new PaymentChannelFactory(new[] { weChat, alipay });

        var byEnumWeChat = factory.GetAdapter(PaymentChannel.WeChatPay);
        var byEnumAlipay = factory.GetAdapter(PaymentChannel.Alipay);

        byEnumWeChat.Should().BeSameAs(weChat);
        byEnumAlipay.Should().BeSameAs(alipay);
    }

    [Fact]
    public void GetAdapter_ByUnknownKey_ShouldThrowPaymentDomainException()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var factory = new PaymentChannelFactory(new[] { weChat });

        var act = () => factory.GetAdapter("UnionPay");

        var ex = act.Should().Throw<PaymentDomainException>();
        ex.Which.ErrorCode.Should().Be("PAYMENT_CHANNEL_NOT_FOUND");
        ex.Which.Message.Should().Contain("UnionPay");
    }

    [Fact]
    public void GetAdapter_ByUnknownEnum_ShouldThrowPaymentDomainException()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var factory = new PaymentChannelFactory(new[] { weChat });

        var act = () => factory.GetAdapter(PaymentChannel.Alipay);

        act.Should().Throw<PaymentDomainException>()
           .Which.ErrorCode.Should().Be("PAYMENT_CHANNEL_NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetAdapter_ByEmptyOrNullKey_ShouldThrowArgumentException(string? channelKey)
    {
        var factory = new PaymentChannelFactory(Array.Empty<IPaymentChannelAdapter>());

        var act = () => factory.GetAdapter(channelKey!);

        act.Should().Throw<ArgumentException>().WithMessage("*渠道标识*");
    }

    [Fact]
    public void Constructor_DisabledAdapters_ShouldBeExcludedFromLookup()
    {
        var enabled = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var disabled = CreateMockAdapter("Alipay", "支付宝", isEnabled: false);
        var factory = new PaymentChannelFactory(new[] { enabled, disabled });

        var act = () => factory.GetAdapter("Alipay");

        act.Should().Throw<PaymentDomainException>();
        factory.GetAdapter("WeChatPay").Should().BeSameAs(enabled);
    }

    [Fact]
    public void Constructor_OnlyDisabledAdapters_ListEnabledChannelsShouldBeEmpty()
    {
        var disabled = CreateMockAdapter("Alipay", "支付宝", isEnabled: false);
        var factory = new PaymentChannelFactory(new[] { disabled });

        factory.ListEnabledChannels().Should().BeEmpty();
        factory.ListEnabledMetadata().Should().BeEmpty();
    }

    [Fact]
    public void ListEnabledChannels_ShouldReturnAllEnabledKeys()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true);
        var alipay = CreateMockAdapter("Alipay", "支付宝", isEnabled: true);
        var disabled = CreateMockAdapter("UnionPay", "银联", isEnabled: false);
        var factory = new PaymentChannelFactory(new[] { weChat, alipay, disabled });

        var keys = factory.ListEnabledChannels();

        keys.Should().HaveCount(2);
        keys.Should().Contain("WeChatPay");
        keys.Should().Contain("Alipay");
        keys.Should().NotContain("UnionPay");
    }

    [Fact]
    public void ListEnabledMetadata_ShouldReturnMetadataForEnabledAdapters()
    {
        var weChatCaps = new PaymentChannelCapabilities
        {
            SupportsRefund = true,
            SupportsPartialCapture = false,
            SupportsQuery = true,
            AsyncNotifyMode = AsyncNotifyMode.Both
        };
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, weChatCaps);
        var factory = new PaymentChannelFactory(new[] { weChat });

        var metadata = factory.ListEnabledMetadata();

        metadata.Should().HaveCount(1);
        var meta = metadata.Single();
        meta.ChannelKey.Should().Be("WeChatPay");
        meta.DisplayName.Should().Be("微信支付");
        meta.IsEnabled.Should().BeTrue();
        meta.Capabilities.Should().BeSameAs(weChatCaps);
    }

    [Fact]
    public void Constructor_NullAdapters_ShouldThrowArgumentNullException()
    {
        var act = () => new PaymentChannelFactory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_EmptyAdapters_GetAnyAdapterShouldThrow()
    {
        var factory = new PaymentChannelFactory(Array.Empty<IPaymentChannelAdapter>());

        factory.ListEnabledChannels().Should().BeEmpty();
        var act = () => factory.GetAdapter("AnyChannel");
        act.Should().Throw<PaymentDomainException>();
    }
}
