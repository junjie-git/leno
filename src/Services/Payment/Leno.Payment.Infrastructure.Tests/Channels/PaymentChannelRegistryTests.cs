using Leno.Payment.Domain.Services;
using Leno.Payment.Infrastructure.Channels;
using Moq;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// 阶段三 3.8 单元测试：验证 <see cref="PaymentChannelRegistry"/> 与
/// <see cref="PaymentChannelCapabilities"/> 驱动的渠道筛选能力。
///
/// 覆盖场景：
/// - GetAllChannels 返回全部（含禁用）
/// - GetEnabledChannels 仅返回已启用
/// - GetChannel 按 Key 查找（大小写不敏感）
/// - IsRegistered / IsEnabled 判定
/// - GetChannelsByCapability 按 <see cref="PaymentChannelCapabilities"/> 过滤（退款/查询/通知模式）
/// - null 构造函数参数抛 <see cref="ArgumentNullException"/>
/// - <see cref="PaymentChannelCapabilities.Default"/> / <see cref="PaymentChannelCapabilities.RefundOnly"/> 预设
/// </summary>
public class PaymentChannelRegistryTests
{
    /// <summary>
    /// 构造 Mock 适配器，设置 ChannelKey/DisplayName/Capabilities/IsEnabled。
    /// </summary>
    private static IPaymentChannelAdapter CreateMockAdapter(
        string channelKey,
        string displayName,
        bool isEnabled,
        PaymentChannelCapabilities capabilities)
    {
        var mock = new Mock<IPaymentChannelAdapter>();
        mock.SetupGet(a => a.ChannelKey).Returns(channelKey);
        mock.SetupGet(a => a.DisplayName).Returns(displayName);
        mock.SetupGet(a => a.IsEnabled).Returns(isEnabled);
        mock.SetupGet(a => a.Capabilities).Returns(capabilities);
        return mock.Object;
    }

    private static readonly PaymentChannelCapabilities FullCapabilities = new()
    {
        SupportsRefund = true,
        SupportsPartialCapture = false,
        SupportsQuery = true,
        AsyncNotifyMode = AsyncNotifyMode.Both
    };

    private static readonly PaymentChannelCapabilities QueryOnlyNoRefund = new()
    {
        SupportsRefund = false,
        SupportsPartialCapture = false,
        SupportsQuery = true,
        AsyncNotifyMode = AsyncNotifyMode.Polling
    };

    private static readonly PaymentChannelCapabilities RefundOnlyCapabilities = new()
    {
        SupportsRefund = true,
        SupportsPartialCapture = false,
        SupportsQuery = false,
        AsyncNotifyMode = AsyncNotifyMode.None
    };

    private static readonly string[] WeChatAndAlipayKeys = { "WeChatPay", "Alipay" };
    private static readonly string[] WeChatAndManualKeys = { "WeChatPay", "ManualChannel" };
    private static readonly string[] WeChatAndAppleKeys = { "WeChatPay", "ApplePay" };

    [Fact]
    public void GetAllChannels_ShouldReturnAllAdaptersIncludingDisabled()
    {
        var enabled = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var disabled = CreateMockAdapter("Alipay", "支付宝", isEnabled: false, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { enabled, disabled });

        var all = registry.GetAllChannels();

        all.Should().HaveCount(2);
        all.Select(m => m.ChannelKey).Should().Contain(WeChatAndAlipayKeys);
    }

    [Fact]
    public void GetEnabledChannels_ShouldReturnOnlyEnabledAdapters()
    {
        var enabled = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var disabled = CreateMockAdapter("Alipay", "支付宝", isEnabled: false, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { enabled, disabled });

        var enabledList = registry.GetEnabledChannels();

        enabledList.Should().HaveCount(1);
        enabledList.Single().ChannelKey.Should().Be("WeChatPay");
    }

    [Fact]
    public void GetChannel_ByKey_ShouldReturnMetadata()
    {
        var adapter = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { adapter });

        var meta = registry.GetChannel("WeChatPay");

        meta.Should().NotBeNull();
        meta!.ChannelKey.Should().Be("WeChatPay");
        meta.DisplayName.Should().Be("微信支付");
        meta.IsEnabled.Should().BeTrue();
        meta.Capabilities.Should().BeSameAs(FullCapabilities);
    }

    [Fact]
    public void GetChannel_ByKey_CaseInsensitive_ShouldReturnMetadata()
    {
        var adapter = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { adapter });

        registry.GetChannel("wechatpay").Should().NotBeNull();
        registry.GetChannel("WECHATPAY").Should().NotBeNull();
    }

    [Fact]
    public void GetChannel_ByUnknownKey_ShouldReturnNull()
    {
        var registry = new PaymentChannelRegistry(Array.Empty<IPaymentChannelAdapter>());

        registry.GetChannel("UnknownChannel").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetChannel_ByEmptyOrNullKey_ShouldReturnNull(string? channelKey)
    {
        var registry = new PaymentChannelRegistry(Array.Empty<IPaymentChannelAdapter>());

        registry.GetChannel(channelKey!).Should().BeNull();
    }

    [Fact]
    public void IsRegistered_RegisteredChannel_ShouldReturnTrue()
    {
        var adapter = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { adapter });

        registry.IsRegistered("WeChatPay").Should().BeTrue();
        registry.IsRegistered("wechatpay").Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_UnregisteredChannel_ShouldReturnFalse()
    {
        var registry = new PaymentChannelRegistry(Array.Empty<IPaymentChannelAdapter>());

        registry.IsRegistered("UnknownChannel").Should().BeFalse();
        registry.IsRegistered("").Should().BeFalse();
        registry.IsRegistered(null!).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_EnabledChannel_ShouldReturnTrue()
    {
        var enabled = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var disabled = CreateMockAdapter("Alipay", "支付宝", isEnabled: false, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { enabled, disabled });

        registry.IsEnabled("WeChatPay").Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_DisabledChannel_ShouldReturnFalse()
    {
        var disabled = CreateMockAdapter("Alipay", "支付宝", isEnabled: false, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { disabled });

        registry.IsRegistered("Alipay").Should().BeTrue();
        registry.IsEnabled("Alipay").Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_UnregisteredChannel_ShouldReturnFalse()
    {
        var registry = new PaymentChannelRegistry(Array.Empty<IPaymentChannelAdapter>());

        registry.IsEnabled("UnknownChannel").Should().BeFalse();
    }

    [Fact]
    public void GetChannelsByCapability_SupportsRefund_ShouldReturnOnlyRefundCapableEnabledChannels()
    {
        var refundable = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var nonRefundable = CreateMockAdapter("ApplePay", "Apple Pay", isEnabled: true, QueryOnlyNoRefund);
        var refundableButDisabled = CreateMockAdapter("TestDisabled", "禁用渠道", isEnabled: false, RefundOnlyCapabilities);
        var registry = new PaymentChannelRegistry(new[] { refundable, nonRefundable, refundableButDisabled });

        var result = registry.GetChannelsByCapability(c => c.SupportsRefund);

        result.Should().HaveCount(1);
        result.Single().ChannelKey.Should().Be("WeChatPay");
    }

    [Fact]
    public void GetChannelsByCapability_SupportsQuery_ShouldReturnAllQueryCapableEnabledChannels()
    {
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var applePay = CreateMockAdapter("ApplePay", "Apple Pay", isEnabled: true, QueryOnlyNoRefund);
        var refundOnly = CreateMockAdapter("ManualRefund", "手动退款", isEnabled: true, RefundOnlyCapabilities);
        var registry = new PaymentChannelRegistry(new[] { weChat, applePay, refundOnly });

        var result = registry.GetChannelsByCapability(c => c.SupportsQuery);

        result.Should().HaveCount(2);
        result.Select(m => m.ChannelKey).Should().Contain(WeChatAndAppleKeys);
        result.Should().NotContain(m => m.ChannelKey == "ManualRefund");
    }

    [Fact]
    public void GetChannelsByCapability_AsyncNotifyModeBoth_ShouldReturnOnlyDualTrackChannels()
    {
        var dualTrack = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var pollingOnly = CreateMockAdapter("Manual", "手动对账", isEnabled: true, QueryOnlyNoRefund);
        var registry = new PaymentChannelRegistry(new[] { dualTrack, pollingOnly });

        var result = registry.GetChannelsByCapability(c => c.AsyncNotifyMode == AsyncNotifyMode.Both);

        result.Should().HaveCount(1);
        result.Single().ChannelKey.Should().Be("WeChatPay");
    }

    [Fact]
    public void GetChannelsByCapability_SupportsPartialCapture_ShouldReturnOnlyPartialCaptureChannels()
    {
        var partialCaptureCaps = new PaymentChannelCapabilities
        {
            SupportsRefund = true,
            SupportsPartialCapture = true,
            SupportsQuery = true,
            AsyncNotifyMode = AsyncNotifyMode.Both
        };
        var partial = CreateMockAdapter("UnionPay", "银联", isEnabled: true, partialCaptureCaps);
        var normal = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { partial, normal });

        var result = registry.GetChannelsByCapability(c => c.SupportsPartialCapture);

        result.Should().HaveCount(1);
        result.Single().ChannelKey.Should().Be("UnionPay");
    }

    [Fact]
    public void GetChannelsByCapability_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        var registry = new PaymentChannelRegistry(Array.Empty<IPaymentChannelAdapter>());

        var act = () => registry.GetChannelsByCapability(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetChannelsByCapability_NoMatch_ShouldReturnEmptyList()
    {
        var adapter = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, FullCapabilities);
        var registry = new PaymentChannelRegistry(new[] { adapter });

        var result = registry.GetChannelsByCapability(c => c.AsyncNotifyMode == AsyncNotifyMode.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullAdapters_ShouldThrowArgumentNullException()
    {
        var act = () => new PaymentChannelRegistry(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_EmptyAdapters_AllQueriesReturnEmpty()
    {
        var registry = new PaymentChannelRegistry(Array.Empty<IPaymentChannelAdapter>());

        registry.GetAllChannels().Should().BeEmpty();
        registry.GetEnabledChannels().Should().BeEmpty();
        registry.GetChannelsByCapability(c => true).Should().BeEmpty();
    }

    [Fact]
    public void Capabilities_Default_ShouldSupportRefundAndQueryAndBothNotify()
    {
        var caps = PaymentChannelCapabilities.Default;

        caps.SupportsRefund.Should().BeTrue();
        caps.SupportsQuery.Should().BeTrue();
        caps.SupportsPartialCapture.Should().BeFalse();
        caps.AsyncNotifyMode.Should().Be(AsyncNotifyMode.Both);
    }

    [Fact]
    public void Capabilities_RefundOnly_ShouldSupportOnlyRefund()
    {
        var caps = PaymentChannelCapabilities.RefundOnly;

        caps.SupportsRefund.Should().BeTrue();
        caps.SupportsQuery.Should().BeFalse();
        caps.SupportsPartialCapture.Should().BeFalse();
        caps.AsyncNotifyMode.Should().Be(AsyncNotifyMode.None);
    }

    [Fact]
    public void Capabilities_WithInit_ShouldSetPropertiesCorrectly()
    {
        var caps = new PaymentChannelCapabilities
        {
            SupportsRefund = false,
            SupportsPartialCapture = true,
            SupportsQuery = false,
            AsyncNotifyMode = AsyncNotifyMode.Polling
        };

        caps.SupportsRefund.Should().BeFalse();
        caps.SupportsPartialCapture.Should().BeTrue();
        caps.SupportsQuery.Should().BeFalse();
        caps.AsyncNotifyMode.Should().Be(AsyncNotifyMode.Polling);
    }

    /// <summary>
    /// 业务场景：退款调度层通过注册表筛选支持退款的已启用渠道，
    /// 验证 <see cref="PaymentChannelCapabilities.SupportsRefund"/> 能力声明能驱动业务条件分支。
    /// </summary>
    [Fact]
    public void BusinessScenario_RefundEligibilityCheck_OnlyRefundCapableChannelsReturned()
    {
        // 模拟三个渠道：微信（退款+查询）、支付宝（退款+查询）、Apple Pay（无退款）
        var weChat = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, PaymentChannelCapabilities.Default);
        var alipay = CreateMockAdapter("Alipay", "支付宝", isEnabled: true, PaymentChannelCapabilities.Default);
        var applePayCaps = new PaymentChannelCapabilities
        {
            SupportsRefund = false,
            SupportsPartialCapture = false,
            SupportsQuery = false,
            AsyncNotifyMode = AsyncNotifyMode.None
        };
        var applePay = CreateMockAdapter("ApplePay", "Apple Pay", isEnabled: true, applePayCaps);
        var registry = new PaymentChannelRegistry(new[] { weChat, alipay, applePay });

        // 退款调度层查询：哪些渠道支持退款？
        var refundableChannels = registry.GetChannelsByCapability(c => c.SupportsRefund);

        refundableChannels.Should().HaveCount(2);
        refundableChannels.Select(m => m.ChannelKey).Should().Contain(WeChatAndAlipayKeys);
        refundableChannels.Should().NotContain(m => m.ChannelKey == "ApplePay");
    }

    /// <summary>
    /// 业务场景：通知处理层通过 <see cref="AsyncNotifyMode"/> 筛选需要轮询兜底的渠道。
    /// </summary>
    [Fact]
    public void BusinessScenario_PollingFallbackSelection_OnlyPollingCapableChannelsReturned()
    {
        var dualTrack = CreateMockAdapter("WeChatPay", "微信支付", isEnabled: true, PaymentChannelCapabilities.Default);
        var pollingOnlyCaps = new PaymentChannelCapabilities
        {
            SupportsRefund = true,
            SupportsPartialCapture = false,
            SupportsQuery = true,
            AsyncNotifyMode = AsyncNotifyMode.Polling
        };
        var pollingOnly = CreateMockAdapter("ManualChannel", "手动渠道", isEnabled: true, pollingOnlyCaps);
        var callbackOnlyCaps = new PaymentChannelCapabilities
        {
            SupportsRefund = true,
            SupportsPartialCapture = false,
            SupportsQuery = false,
            AsyncNotifyMode = AsyncNotifyMode.HttpCallback
        };
        var callbackOnly = CreateMockAdapter("CallbackOnly", "仅回调渠道", isEnabled: true, callbackOnlyCaps);
        var registry = new PaymentChannelRegistry(new[] { dualTrack, pollingOnly, callbackOnly });

        // 需要轮询兜底的渠道：AsyncNotifyMode 为 Polling 或 Both
        var pollingChannels = registry.GetChannelsByCapability(
            c => c.AsyncNotifyMode == AsyncNotifyMode.Polling || c.AsyncNotifyMode == AsyncNotifyMode.Both);

        pollingChannels.Should().HaveCount(2);
        pollingChannels.Select(m => m.ChannelKey).Should().Contain(WeChatAndManualKeys);
        pollingChannels.Should().NotContain(m => m.ChannelKey == "CallbackOnly");
    }
}
