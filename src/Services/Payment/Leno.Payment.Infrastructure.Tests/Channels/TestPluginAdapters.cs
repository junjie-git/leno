using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Services;

namespace Leno.Payment.Infrastructure.Tests.Channels;

/// <summary>
/// 测试用插件适配器桩（模拟 UnionPay 渠道），仅用于 <see cref="PaymentChannelPluginLoaderTests"/> 验证
/// <see cref="PaymentChannelPluginLoader"/> 通过 <c>Assembly.LoadFrom</c> 扫描程序集并识别适配器类型。
/// 该类为 public，满足加载器 <c>type.IsPublic</c> 过滤条件；异步方法返回默认结果以保持桩完整性。
/// </summary>
public sealed class TestUnionPayPluginAdapter : IPaymentChannelAdapter
{
    public string ChannelKey => "UnionPay";

    public string DisplayName => "银联支付（测试插件）";

    public PaymentChannelCapabilities Capabilities { get; } = new PaymentChannelCapabilities
    {
        SupportsRefund = true,
        SupportsPartialCapture = true,
        SupportsQuery = true,
        AsyncNotifyMode = AsyncNotifyMode.HttpCallback
    };

    public bool IsEnabled => true;

    public Task<ChannelPaymentResult> CreatePaymentAsync(PaymentOrder paymentOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paymentOrder);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelPaymentResult
        {
            CodeUrl = "https://qr.unionpay.com/test-plugin",
            ChannelTradeNo = "UNION_TEST_" + paymentOrder.OutTradeNo
        });
    }

    public Task<ChannelPaymentQueryResult> QueryPaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outTradeNo);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelPaymentQueryResult
        {
            IsPaid = false,
            ChannelTradeNo = null,
            PaidAt = null,
            Amount = null
        });
    }

    public Task<ChannelRefundResult> CreateRefundAsync(RefundOrder refundOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(refundOrder);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelRefundResult
        {
            ChannelRefundNo = "UNION_REFUND_" + refundOrder.OutRefundNo,
            Succeeded = true
        });
    }

    public Task<ChannelRefundQueryResult> QueryRefundAsync(string outTradeNo, string outRefundNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outTradeNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(outRefundNo);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelRefundQueryResult
        {
            Succeeded = false,
            RefundedAt = null
        });
    }

    public Task<ChannelPaymentCloseResult> ClosePaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outTradeNo);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelPaymentCloseResult
        {
            Succeeded = true,
            ChannelTradeNo = null
        });
    }

    public Task<ChannelNotifyResult> VerifyNotifyAsync(string rawBody, Dictionary<string, string> headers, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(headers);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelNotifyResult
        {
            Verified = false,
            OrderId = Guid.Empty,
            OutTradeNo = null,
            ChannelTradeNo = null,
            IsPaid = false,
            PaidAt = null,
            IsRefund = false,
            RefundAmount = null,
            Amount = null
        });
    }
}

/// <summary>
/// 测试用插件适配器桩（模拟 ApplePay 渠道），仅用于 <see cref="PaymentChannelPluginLoaderTests"/> 验证
/// 加载器能从同一程序集识别多个适配器类型。该类为 public 非抽象，满足加载器过滤条件。
/// </summary>
public sealed class TestApplePayPluginAdapter : IPaymentChannelAdapter
{
    public string ChannelKey => "ApplePay";

    public string DisplayName => "Apple Pay（测试插件）";

    public PaymentChannelCapabilities Capabilities { get; } = new PaymentChannelCapabilities
    {
        SupportsRefund = false,
        SupportsPartialCapture = false,
        SupportsQuery = false,
        AsyncNotifyMode = AsyncNotifyMode.None
    };

    public bool IsEnabled => true;

    public Task<ChannelPaymentResult> CreatePaymentAsync(PaymentOrder paymentOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paymentOrder);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelPaymentResult
        {
            PrepayId = "APPLE_PAY_TEST_SESSION",
            ChannelTradeNo = "APPLE_TEST_" + paymentOrder.OutTradeNo
        });
    }

    public Task<ChannelPaymentQueryResult> QueryPaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outTradeNo);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelPaymentQueryResult
        {
            IsPaid = false,
            ChannelTradeNo = null,
            PaidAt = null,
            Amount = null
        });
    }

    public Task<ChannelRefundResult> CreateRefundAsync(RefundOrder refundOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(refundOrder);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelRefundResult
        {
            ChannelRefundNo = null,
            Succeeded = false
        });
    }

    public Task<ChannelRefundQueryResult> QueryRefundAsync(string outTradeNo, string outRefundNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outTradeNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(outRefundNo);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelRefundQueryResult
        {
            Succeeded = false,
            RefundedAt = null
        });
    }

    public Task<ChannelPaymentCloseResult> ClosePaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outTradeNo);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelPaymentCloseResult
        {
            Succeeded = true,
            ChannelTradeNo = null
        });
    }

    public Task<ChannelNotifyResult> VerifyNotifyAsync(string rawBody, Dictionary<string, string> headers, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(headers);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ChannelNotifyResult
        {
            Verified = false,
            OrderId = Guid.Empty,
            OutTradeNo = null,
            ChannelTradeNo = null,
            IsPaid = false,
            PaidAt = null,
            IsRefund = false,
            RefundAmount = null,
            Amount = null
        });
    }
}
