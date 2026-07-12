using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Services;
using Leno.Payment.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels;

/// <summary>
/// 微信支付渠道适配器（APIv3），实现 <see cref="IPaymentChannelAdapter"/>。
/// 通过 <see cref="WeChatPay.WeChatPayClient"/> 与微信支付 V3 API 交互，屏蔽渠道差异。
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
        var description = $"订单 {paymentOrder.OrderId} 微信支付";

        // 默认 Native 扫码支付
        const string tradeType = "NATIVE";
        var result = await _client.UnifiedOrderAsync(config, paymentOrder.OutTradeNo, totalFee, description, tradeType, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("微信支付 V3 统一下单失败 OutTradeNo={OutTradeNo} ErrCodeDes={ErrCodeDes}",
                paymentOrder.OutTradeNo, result.ErrCodeDes);
        }

        return new ChannelPaymentResult
        {
            PrepayId = result.PrepayId,
            CodeUrl = result.CodeUrl,
            H5Url = result.H5Url,
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
    public async Task<ChannelPaymentCloseResult> ClosePaymentAsync(string outTradeNo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outTradeNo))
        {
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        }

        var config = await _configProvider.GetConfigAsync(PaymentChannel.WeChatPay, ct);
        await _client.CloseOrderAsync(config, outTradeNo, ct);

        return new ChannelPaymentCloseResult
        {
            Succeeded = true,
            ChannelTradeNo = null
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
            _logger.LogWarning("微信支付 V3 退款失败 OutRefundNo={OutRefundNo} ErrCodeDes={ErrCodeDes}",
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

        // V3 回调签名验证：使用 Wechatpay-Signature 头
        var timestamp = GetHeader(headers, "Wechatpay-Timestamp");
        var nonce = GetHeader(headers, "Wechatpay-Nonce");
        var signature = GetHeader(headers, "Wechatpay-Signature");
        var serialNo = GetHeader(headers, "Wechatpay-Serial");

        // 使用 APIv3 密钥进行验证（V3 回调使用平台公钥验证签名，此处用 ApiV3Key 作为平台公钥）
        var verified = WeChatPay.WeChatPayV3SignatureHelper.VerifyNotifySign(
            timestamp ?? string.Empty, nonce ?? string.Empty, rawBody, signature ?? string.Empty, config.ApiKey);

        if (!verified)
        {
            _logger.LogWarning("微信支付 V3 回调验签失败 OutTradeNo 未知");
        }

        // 解析回调 JSON 获取交易信息
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(rawBody).RootElement;
        }
        catch (JsonException)
        {
            return new ChannelNotifyResult { Verified = false };
        }

        var eventType = root.TryGetProperty("event_type", out var evt) ? evt.GetString() : null;
        var resource = root.TryGetProperty("resource", out var res) ? res : default;

        string? decryptData = null;
        if (resource.ValueKind != JsonValueKind.Undefined)
        {
            var ciphertext = resource.TryGetProperty("ciphertext", out var ctNode) ? ctNode.GetString() : null;
            var associatedData = resource.TryGetProperty("associated_data", out var adNode) ? adNode.GetString() : null;
            var nonceStr = resource.TryGetProperty("nonce", out var nNode) ? nNode.GetString() : null;

            if (!string.IsNullOrEmpty(ciphertext) && !string.IsNullOrEmpty(nonceStr))
            {
                decryptData = DecryptNotifyData(ciphertext, associatedData, nonceStr, config.ApiKey);
            }
        }

        var isPaid = false;
        var isRefund = false;
        string? channelTradeNo = null;
        DateTime? paidAt = null;
        decimal? refundAmount = null;

        if (!string.IsNullOrEmpty(decryptData))
        {
            try
            {
                var dataRoot = JsonDocument.Parse(decryptData).RootElement;
                channelTradeNo = dataRoot.TryGetProperty("transaction_id", out var txnId) ? txnId.GetString() : null;
                var tradeState = dataRoot.TryGetProperty("trade_state", out var state) ? state.GetString() : null;
                isPaid = string.Equals(tradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase);

                var successTime = dataRoot.TryGetProperty("success_time", out var st) ? st.GetString() : null;
                paidAt = ParseWeChatTime(successTime);

                var refundStatus = dataRoot.TryGetProperty("refund_status", out var rs) ? rs.GetString() : null;
                isRefund = !string.IsNullOrEmpty(refundStatus);

                if (isRefund && dataRoot.TryGetProperty("amount", out var amountNode))
                {
                    var refundAmt = amountNode.TryGetProperty("refund", out var ra) ? ra.GetInt32() : 0;
                    refundAmount = refundAmt / 100m;
                }
            }
            catch (JsonException)
            {
                // 解密数据解析失败，保持默认值
            }
        }

        return new ChannelNotifyResult
        {
            Verified = verified,
            OrderId = Guid.Empty,
            ChannelTradeNo = channelTradeNo,
            IsPaid = isPaid,
            PaidAt = paidAt,
            IsRefund = isRefund,
            RefundAmount = refundAmount
        };
    }

    private static string? GetHeader(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var v) ? v : null;

    private static string? DecryptNotifyData(string ciphertext, string? associatedData, string nonce, string apiV3Key)
    {
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(apiV3Key);
            var nonceBytes = Encoding.UTF8.GetBytes(nonce);
            var associatedBytes = string.IsNullOrEmpty(associatedData)
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(associatedData);
            var cipherBytes = Convert.FromBase64String(ciphertext);

            var tag = new byte[16];
            var plaintextBytes = new byte[cipherBytes.Length - 16];

            Array.Copy(cipherBytes, 0, plaintextBytes, 0, plaintextBytes.Length);
            Array.Copy(cipherBytes, plaintextBytes.Length, tag, 0, tag.Length);

            using var aesGcm = new AesGcm(keyBytes, tag.Length);
            aesGcm.Decrypt(nonceBytes, plaintextBytes, tag, plaintextBytes, associatedBytes);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ParseWeChatTime(string? timeEnd)
    {
        if (string.IsNullOrEmpty(timeEnd))
        {
            return null;
        }

        // V3 返回 RFC3339 格式: 2018-06-08T10:34:56+08:00
        if (DateTime.TryParse(timeEnd, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt;
        }

        // 兼容旧格式: yyyyMMddHHmmss
        if (timeEnd.Length == 14)
        {
            return DateTime.ParseExact(
                timeEnd,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        return null;
    }
}