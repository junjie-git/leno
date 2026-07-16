using System.Globalization;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 支付宝渠道适配器，实现 <see cref="IPaymentChannelAdapter"/>。
/// 通过 <see cref="Alipay.AlipayClient"/> 与支付宝交互，屏蔽渠道差异。
/// 支持扫码支付（precreate）、PC 网页支付（page.pay）、手机网页支付（wap.pay）、App 支付（app.pay）。
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
    /// <remarks>默认使用扫码支付（precreate）。如需指定场景，请使用 <see cref="CreatePaymentAsync(PaymentOrder, PaymentScene, string?, CancellationToken)"/>。</remarks>
    public async Task<ChannelPaymentResult> CreatePaymentAsync(
        PaymentOrder paymentOrder, CancellationToken ct = default)
    {
        return await CreatePaymentAsync(paymentOrder, PaymentScene.QrCode, null, ct);
    }

    /// <summary>
    /// 向支付宝发起下单，支持指定支付场景。
    /// </summary>
    /// <param name="paymentOrder">支付单聚合。</param>
    /// <param name="scene">支付场景（QrCode/Page/Wap/App）。</param>
    /// <param name="returnUrl">同步回跳地址（Page/Wap 场景必需）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task<ChannelPaymentResult> CreatePaymentAsync(
        PaymentOrder paymentOrder, PaymentScene scene, string? returnUrl = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paymentOrder);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var totalAmount = paymentOrder.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var subject = $"订单 {paymentOrder.OrderId} 支付宝支付";

        return scene switch
        {
            PaymentScene.QrCode => await CreateQrCodePaymentAsync(config, paymentOrder.OutTradeNo, totalAmount, subject, ct),
            PaymentScene.Page => CreatePagePaymentUrl(config, paymentOrder.OutTradeNo, totalAmount, subject, returnUrl ?? string.Empty),
            PaymentScene.Wap => CreateWapPaymentUrl(config, paymentOrder.OutTradeNo, totalAmount, subject, returnUrl ?? string.Empty),
            PaymentScene.App => CreateAppPaymentOrderString(config, paymentOrder.OutTradeNo, totalAmount, subject),
            _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, "不支持的支付场景")
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
            PaidAt = ParseAlipayTime(result.SendPayDate),
            Amount = result.TotalAmount
        };
    }

    /// <inheritdoc />
    public async Task<ChannelPaymentCloseResult> ClosePaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outTradeNo))
        {
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        }

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var result = await _client.CloseAsync(config, outTradeNo, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("支付宝交易关闭失败 OutTradeNo={OutTradeNo} SubMsg={SubMsg}",
                outTradeNo, result.SubMsg);
        }

        return new ChannelPaymentCloseResult
        {
            Succeeded = result.IsSuccess,
            ChannelTradeNo = result.TradeNo
        };
    }

    /// <inheritdoc />
    public async Task<ChannelRefundResult> CreateRefundAsync(RefundOrder refundOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(refundOrder);

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);
        var refundAmount = refundOrder.RefundAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var outTradeNo = refundOrder.OutTradeNo;

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
    public async Task<ChannelRefundQueryResult> QueryRefundAsync(string outTradeNo, string outRefundNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outRefundNo))
        {
            throw new ArgumentException("商户退款单号不可为空", nameof(outRefundNo));
        }

        var config = await _configProvider.GetConfigAsync(PaymentChannel.Alipay, ct);

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

        // 支付宝 total_amount 单位为元，解析为实付金额用于强校验
        decimal? amount = null;
        var totalAmountText = GetField(dict, "total_amount");
        if (isPaid && decimal.TryParse(totalAmountText, CultureInfo.InvariantCulture, out var totalAmount))
        {
            amount = totalAmount;
        }

        return new ChannelNotifyResult
        {
            Verified = verified,
            OrderId = Guid.Empty,
            ChannelTradeNo = GetField(dict, "trade_no"),
            IsPaid = isPaid,
            PaidAt = ParseAlipayTime(GetField(dict, "gmt_payment")),
            IsRefund = isRefund,
            RefundAmount = refundAmount,
            Amount = amount
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

    private async Task<ChannelPaymentResult> CreateQrCodePaymentAsync(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject, CancellationToken ct)
    {
        var result = await _client.PreCreateAsync(config, outTradeNo, totalAmount, subject, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("支付宝当面付预下单失败 OutTradeNo={OutTradeNo} SubMsg={SubMsg}",
                outTradeNo, result.SubMsg);
        }

        return new ChannelPaymentResult
        {
            CodeUrl = result.QrCode,
            ChannelTradeNo = result.TradeNo
        };
    }

    private ChannelPaymentResult CreatePagePaymentUrl(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject, string returnUrl)
    {
        var url = _client.BuildPagePayUrl(config, outTradeNo, totalAmount, subject, returnUrl);

        return new ChannelPaymentResult
        {
            H5Url = url,
            ChannelTradeNo = outTradeNo
        };
    }

    private ChannelPaymentResult CreateWapPaymentUrl(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject, string returnUrl)
    {
        var url = _client.BuildWapPayUrl(config, outTradeNo, totalAmount, subject, returnUrl);

        return new ChannelPaymentResult
        {
            H5Url = url,
            ChannelTradeNo = outTradeNo
        };
    }

    private ChannelPaymentResult CreateAppPaymentOrderString(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject)
    {
        var orderString = _client.BuildAppPayOrderString(config, outTradeNo, totalAmount, subject);

        return new ChannelPaymentResult
        {
            PrepayId = orderString,
            ChannelTradeNo = outTradeNo
        };
    }
}
