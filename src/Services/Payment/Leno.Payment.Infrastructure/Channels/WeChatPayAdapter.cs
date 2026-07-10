using System.Globalization;
using System.Xml.Linq;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 微信支付渠道适配器，实现 <see cref="IPaymentChannelAdapter"/>。
/// 通过 <see cref="WeChatPayClient"/> 与微信支付交互，屏蔽渠道差异。
/// </summary>
public sealed class WeChatPayAdapter : IPaymentChannelAdapter
{
    private readonly WeChatPay.WeChatPayClient _client;
    private readonly IChannelConfigProvider _configProvider;
    private readonly ILogger<WeChatPayAdapter> _logger;

    public WeChatPayAdapter(
        WeChatPay.WeChatPayClient client,
        IChannelConfigProvider configProvider,
        ILogger<WeChatPayAdapter> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChannelPaymentResult> CreatePaymentAsync(
        PaymentOrder paymentOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paymentOrder);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.WeChatPay, ct);
        var totalFee = (int)Math.Round(paymentOrder.Amount * 100m);
        var body = $"订单 {paymentOrder.OrderId} 微信支付";

        // 默认扫码支付（NATIVE）；H5 场景传 MWEB，JSAPI 场景传 JSAPI 并携带 openid
        const string tradeType = "NATIVE";
        var result = await _client.UnifiedOrderAsync(config, paymentOrder.OutTradeNo, totalFee, body, tradeType, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("微信支付统一下单失败 OutTradeNo={OutTradeNo} ErrCodeDes={ErrCodeDes}",
                paymentOrder.OutTradeNo, result.ErrCodeDes);
        }

        return new ChannelPaymentResult
        {
            PrepayId = result.PrepayId,
            CodeUrl = result.CodeUrl,
            H5Url = result.MwebUrl,
            ChannelTradeNo = result.TransactionId
        };
    }

    /// <inheritdoc />
    public async Task<ChannelPaymentQueryResult> QueryPaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outTradeNo))
        {
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        }

        var config = await _configProvider.GetConfigAsync(PaymentChannel.WeChatPay, ct);
        var result = await _client.QueryOrderAsync(config, outTradeNo, ct);

        return new ChannelPaymentQueryResult
        {
            IsPaid = result.IsPaid,
            ChannelTradeNo = result.TransactionId,
            PaidAt = ParseWeChatTime(result.TimeEnd)
        };
    }

    /// <inheritdoc />
    public async Task<ChannelRefundResult> CreateRefundAsync(RefundOrder refundOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(refundOrder);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.WeChatPay, ct);
        var refundFee = (int)Math.Round(refundOrder.RefundAmount * 100m);
        var outTradeNo = refundOrder.OutTradeNo;

        var result = await _client.RefundAsync(config, outTradeNo, refundOrder.OutRefundNo, refundFee, refundFee, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("微信支付退款失败 OutRefundNo={OutRefundNo} ErrCodeDes={ErrCodeDes}",
                refundOrder.OutRefundNo, result.ErrCodeDes);
        }

        return new ChannelRefundResult
        {
            ChannelRefundNo = result.RefundId,
            Succeeded = result.IsSuccess
        };
    }

    /// <inheritdoc />
    public async Task<ChannelRefundQueryResult> QueryRefundAsync(string outTradeNo, string outRefundNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outRefundNo))
        {
            throw new ArgumentException("商户退款单号不可为空", nameof(outRefundNo));
        }

        _ = outTradeNo;
        var config = await _configProvider.GetConfigAsync(PaymentChannel.WeChatPay, ct);
        var result = await _client.QueryRefundAsync(config, outRefundNo, ct);

        return new ChannelRefundQueryResult
        {
            Succeeded = result.Succeeded,
            RefundedAt = ParseWeChatTime(result.RefundSuccessTime)
        };
    }

    /// <inheritdoc />
    public async Task<ChannelNotifyResult> VerifyNotifyAsync(
        string rawBody, Dictionary<string, string> headers, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(headers);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.WeChatPay, ct);

        var dict = ParseXml(rawBody);
        var sign = GetField(dict, "sign");
        var verified = WeChatPay.WeChatPaySignatureHelper.VerifySign(dict, config.ApiKey, sign);

        var resultCode = GetField(dict, "result_code");
        var outRefundNo = GetField(dict, "out_refund_no");
        var isRefund = !string.IsNullOrEmpty(outRefundNo);
        var isPaid = !isRefund && string.Equals(resultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase);

        decimal? refundAmount = null;
        if (isRefund && int.TryParse(GetField(dict, "refund_fee"), CultureInfo.InvariantCulture, out var refundFee))
        {
            refundAmount = refundFee / 100m;
        }

        return new ChannelNotifyResult
        {
            Verified = verified,
            OrderId = Guid.Empty,
            ChannelTradeNo = GetField(dict, "transaction_id"),
            IsPaid = isPaid,
            PaidAt = ParseWeChatTime(GetField(dict, "time_end")),
            IsRefund = isRefund,
            RefundAmount = refundAmount
        };
    }

    private static Dictionary<string, string> ParseXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("微信支付通知 XML 缺少根节点");
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var el in root.Elements())
        {
            dict[el.Name.LocalName] = el.Value;
        }

        return dict;
    }

    private static string? GetField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : null;

    private static DateTime? ParseWeChatTime(string? timeEnd)
    {
        if (string.IsNullOrEmpty(timeEnd) || timeEnd.Length != 14)
        {
            return null;
        }

        return DateTime.ParseExact(
            timeEnd,
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
