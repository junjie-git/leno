using System.Globalization;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付宝渠道适配器，实现 <see cref="IPaymentChannelAdapter"/>。
/// 通过 <see cref="Alipay.AlipayClient"/> 与支付宝交互，屏蔽渠道差异。
/// </summary>
public sealed class AlipayAdapter : IPaymentChannelAdapter
{
    private readonly Alipay.AlipayClient _client;
    private readonly IChannelConfigProvider _configProvider;
    private readonly ILogger<AlipayAdapter> _logger;

    public AlipayAdapter(
        Alipay.AlipayClient client,
        IChannelConfigProvider configProvider,
        ILogger<AlipayAdapter> logger)
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

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var totalAmount = paymentOrder.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var subject = $"订单 {paymentOrder.OrderId} 支付宝支付";

        var result = await _client.PreCreateAsync(config, paymentOrder.OutTradeNo, totalAmount, subject, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("支付宝预下单失败 OutTradeNo={OutTradeNo} SubMsg={SubMsg}",
                paymentOrder.OutTradeNo, result.SubMsg);
        }

        return new ChannelPaymentResult
        {
            CodeUrl = result.QrCode,
            ChannelTradeNo = result.TradeNo
        };
    }

    /// <inheritdoc />
    public async Task<ChannelPaymentQueryResult> QueryPaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outTradeNo))
        {
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        }

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var result = await _client.QueryAsync(config, outTradeNo, ct);

        return new ChannelPaymentQueryResult
        {
            IsPaid = result.IsPaid,
            ChannelTradeNo = result.TradeNo,
            PaidAt = ParseAlipayTime(result.SendPayDate)
        };
    }

    /// <inheritdoc />
    public async Task<ChannelRefundResult> CreateRefundAsync(RefundOrder refundOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(refundOrder);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var refundAmount = refundOrder.RefundAmount.ToString("0.00", CultureInfo.InvariantCulture);
        // 模拟环境以 PaymentId 作为原支付单号占位；生产环境应使用原支付单 OutTradeNo
        var outTradeNo = refundOrder.PaymentId.ToString();

        var result = await _client.RefundAsync(config, outTradeNo, refundOrder.OutRefundNo, refundAmount, "用户退款", ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("支付宝退款失败 OutRefundNo={OutRefundNo} SubMsg={SubMsg}",
                refundOrder.OutRefundNo, result.SubMsg);
        }

        return new ChannelRefundResult
        {
            ChannelRefundNo = result.TradeNo ?? refundOrder.OutRefundNo,
            Succeeded = result.IsSuccess
        };
    }

    /// <inheritdoc />
    public async Task<ChannelRefundQueryResult> QueryRefundAsync(string outRefundNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outRefundNo))
        {
            throw new ArgumentException("商户退款单号不可为空", nameof(outRefundNo));
        }

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        // 模拟环境以退款单号回查；生产环境需原支付单 OutTradeNo
        var outTradeNo = outRefundNo;

        var result = await _client.QueryRefundAsync(config, outTradeNo, outRefundNo, ct);

        return new ChannelRefundQueryResult
        {
            Succeeded = result.Succeeded,
            RefundedAt = ParseAlipayTime(result.GmtRefundPay)
        };
    }

    /// <inheritdoc />
    public async Task<ChannelNotifyResult> VerifyNotifyAsync(
        string rawBody, Dictionary<string, string> headers, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentNullException.ThrowIfNull(headers);

        // 支付宝通知为表单字段；优先使用传入字典（由 Handler 解析的表单字段），否则解析 rawBody
        var dict = headers.Count > 0
            ? new Dictionary<string, string>(headers, StringComparer.Ordinal)
            : ParseForm(rawBody);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var sign = GetField(dict, "sign");
        var verified = Alipay.AlipaySignatureHelper.VerifySign(dict, config.ApiKey, sign);

        var tradeStatus = GetField(dict, "trade_status");
        var isPaid = string.Equals(tradeStatus, "TRADE_SUCCESS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tradeStatus, "TRADE_FINISHED", StringComparison.OrdinalIgnoreCase);

        var outRequestNo = GetField(dict, "out_request_no");
        var isRefund = !string.IsNullOrEmpty(outRequestNo);

        decimal? refundAmount = null;
        var refundFeeText = GetField(dict, "refund_fee");
        if (isRefund && decimal.TryParse(refundFeeText, CultureInfo.InvariantCulture, out var refundFee))
        {
            refundAmount = refundFee;
        }

        return new ChannelNotifyResult
        {
            Verified = verified,
            OrderId = Guid.Empty,
            ChannelTradeNo = GetField(dict, "trade_no"),
            IsPaid = isPaid,
            PaidAt = ParseAlipayTime(GetField(dict, "gmt_payment")),
            IsRefund = isRefund,
            RefundAmount = refundAmount
        };
    }

    private static Dictionary<string, string> ParseForm(string rawBody)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(rawBody))
        {
            return dict;
        }

        foreach (var pair in rawBody.Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..idx]);
            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            dict[key] = value;
        }

        return dict;
    }

    private static string? GetField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : null;

    private static DateTime? ParseAlipayTime(string? time)
    {
        if (string.IsNullOrEmpty(time))
        {
            return null;
        }

        return DateTime.ParseExact(
            time,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
