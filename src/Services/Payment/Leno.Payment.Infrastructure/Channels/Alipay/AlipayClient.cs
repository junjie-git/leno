using System.Globalization;
using System.Text;
using System.Text.Json;
using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels.Alipay;

/// <summary>
/// 支付宝渠道 HTTP 客户端，封装当面付预下单/交易查询/退款/退款查询。
/// 请求为 form-encoded（公共参数 + biz_content），响应为 JSON。
/// 当前为模拟实现：构造请求并签名，解析模拟的 JSON 响应；生产环境需配置真实 RSA 密钥后通过 <see cref="HttpClient"/> 发起调用。
/// </summary>
public sealed class AlipayClient
{
    private const string DefaultGateway = "https://openapi.alipay.com/gateway.do";
    private const string PreCreateMethod = "alipay.trade.precreate";
    private const string QueryMethod = "alipay.trade.query";
    private const string RefundMethod = "alipay.trade.refund";
    private const string RefundQueryMethod = "alipay.trade.fastpay.refund.query";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlipayClient> _logger;

    public AlipayClient(HttpClient httpClient, ILogger<AlipayClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>当面付预下单（扫码支付），获取二维码链接。</summary>
    public async Task<AlipayPreCreateResult> PreCreateAsync(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var bizContent = BuildJson(new Dictionary<string, string>
        {
            ["out_trade_no"] = outTradeNo,
            ["total_amount"] = totalAmount,
            ["subject"] = subject
        });
        var parameters = BuildBaseParameters(config, PreCreateMethod, bizContent);
        parameters["notify_url"] = config.NotifyUrl;
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝当面付预下单（模拟）Url={Url} OutTradeNo={OutTradeNo} Subject={Subject}",
            BuildUrl(), outTradeNo, subject);

        var dict = ParseResponse(SimulatePreCreateResponse(outTradeNo), PreCreateMethod);
        await Task.CompletedTask;
        return AlipayPreCreateResult.From(dict);
    }

    /// <summary>交易查询，主动查询支付状态。</summary>
    public async Task<AlipayQueryResult> QueryAsync(
        ChannelConfig config, string outTradeNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var bizContent = BuildJson(new Dictionary<string, string> { ["out_trade_no"] = outTradeNo });
        var parameters = BuildBaseParameters(config, QueryMethod, bizContent);
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝交易查询（模拟）Url={Url} OutTradeNo={OutTradeNo}", BuildUrl(), outTradeNo);

        var dict = ParseResponse(SimulateQueryResponse(outTradeNo, paid: true), QueryMethod);
        await Task.CompletedTask;
        return AlipayQueryResult.From(dict);
    }

    /// <summary>交易退款。</summary>
    public async Task<AlipayRefundResult> RefundAsync(
        ChannelConfig config, string outTradeNo, string outRequestNo, string refundAmount, string refundReason, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var bizContent = BuildJson(new Dictionary<string, string>
        {
            ["out_trade_no"] = outTradeNo,
            ["out_request_no"] = outRequestNo,
            ["refund_amount"] = refundAmount,
            ["refund_reason"] = refundReason
        });
        var parameters = BuildBaseParameters(config, RefundMethod, bizContent);
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝交易退款（模拟）Url={Url} OutRequestNo={OutRequestNo} RefundAmount={RefundAmount}",
            BuildUrl(), outRequestNo, refundAmount);

        var dict = ParseResponse(SimulateRefundResponse(outTradeNo), RefundMethod);
        await Task.CompletedTask;
        return AlipayRefundResult.From(dict);
    }

    /// <summary>退款查询，主动查询退款到账状态。</summary>
    public async Task<AlipayQueryRefundResult> QueryRefundAsync(
        ChannelConfig config, string outTradeNo, string outRequestNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var bizContent = BuildJson(new Dictionary<string, string>
        {
            ["out_trade_no"] = outTradeNo,
            ["out_request_no"] = outRequestNo
        });
        var parameters = BuildBaseParameters(config, RefundQueryMethod, bizContent);
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝退款查询（模拟）Url={Url} OutRequestNo={OutRequestNo}", BuildUrl(), outRequestNo);

        var dict = ParseResponse(SimulateQueryRefundResponse(outRequestNo, succeeded: true), RefundQueryMethod);
        await Task.CompletedTask;
        return AlipayQueryRefundResult.From(dict);
    }

    private static Dictionary<string, string> BuildBaseParameters(ChannelConfig config, string method, string bizContent)
    {
        return new Dictionary<string, string>
        {
            ["app_id"] = config.AppId,
            ["method"] = method,
            ["charset"] = "UTF-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["version"] = "1.0",
            ["biz_content"] = bizContent
        };
    }

    private string BuildUrl()
    {
        var baseAddress = _httpClient.BaseAddress;
        return baseAddress is null ? DefaultGateway : baseAddress.ToString();
    }

    private static string BuildJson(Dictionary<string, string> fields)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var kv in fields)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append('"').Append(kv.Key).Append("\":\"").Append(kv.Value).Append('"');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static Dictionary<string, string?> ParseResponse(string json, string method)
    {
        var wrapper = method.Replace('.', '_') + "_response";
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(wrapper, out var inner) && inner.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in inner.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
            }
        }

        return dict;
    }

    private static string SimulatePreCreateResponse(string outTradeNo)
    {
        var qrCode = "https://qr.alipay.com/" + Random.Shared.Next(100000000, 999999999);
        var tradeNo = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + Random.Shared.Next(1000, 9999);
        return "{\"alipay_trade_precreate_response\":{\"code\":\"10000\",\"msg\":\"Success\","
            + "\"out_trade_no\":\"" + outTradeNo + "\",\"qr_code\":\"" + qrCode + "\",\"trade_no\":\"" + tradeNo + "\"},\"sign\":\"simulated\"}";
    }

    private static string SimulateQueryResponse(string outTradeNo, bool paid)
    {
        var tradeNo = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + Random.Shared.Next(1000, 9999);
        var sendPayDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return "{\"alipay_trade_query_response\":{\"code\":\"10000\",\"msg\":\"Success\","
            + "\"out_trade_no\":\"" + outTradeNo + "\",\"trade_no\":\"" + tradeNo + "\","
            + "\"trade_status\":\"" + (paid ? "TRADE_SUCCESS" : "WAIT_BUYER_PAY") + "\","
            + "\"send_pay_date\":\"" + (paid ? sendPayDate : string.Empty) + "\"},\"sign\":\"simulated\"}";
    }

    private static string SimulateRefundResponse(string outTradeNo)
    {
        var tradeNo = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + Random.Shared.Next(1000, 9999);
        return "{\"alipay_trade_refund_response\":{\"code\":\"10000\",\"msg\":\"Success\","
            + "\"out_trade_no\":\"" + outTradeNo + "\",\"trade_no\":\"" + tradeNo + "\",\"fund_change\":\"Y\"},\"sign\":\"simulated\"}";
    }

    private static string SimulateQueryRefundResponse(string outRequestNo, bool succeeded)
    {
        var gmtRefundPay = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return "{\"alipay_trade_fastpay_refund_query_response\":{\"code\":\"10000\",\"msg\":\"Success\","
            + "\"out_request_no\":\"" + outRequestNo + "\","
            + "\"refund_status\":\"" + (succeeded ? "REFUND_SUCCESS" : "REFUND_PROCESSING") + "\","
            + "\"gmt_refund_pay\":\"" + (succeeded ? gmtRefundPay : string.Empty) + "\"},\"sign\":\"simulated\"}";
    }
}

/// <summary>当面付预下单响应。</summary>
public sealed class AlipayPreCreateResult
{
    public string Code { get; init; } = string.Empty;
    public string Msg { get; init; } = string.Empty;
    public string? QrCode { get; init; }
    public string? TradeNo { get; init; }
    public string? OutTradeNo { get; init; }
    public string? SubCode { get; init; }
    public string? SubMsg { get; init; }

    public bool IsSuccess => string.Equals(Code, "10000", StringComparison.Ordinal);

    public static AlipayPreCreateResult From(Dictionary<string, string?> dict) => new()
    {
        Code = GetField(dict, "code"),
        Msg = GetField(dict, "msg"),
        QrCode = dict.GetValueOrDefault("qr_code"),
        TradeNo = dict.GetValueOrDefault("trade_no"),
        OutTradeNo = dict.GetValueOrDefault("out_trade_no"),
        SubCode = dict.GetValueOrDefault("sub_code"),
        SubMsg = dict.GetValueOrDefault("sub_msg")
    };

    private static string GetField(Dictionary<string, string?> dict, string key)
        => dict.TryGetValue(key, out var v) && v is not null ? v : string.Empty;
}

/// <summary>交易查询响应。</summary>
public sealed class AlipayQueryResult
{
    public string Code { get; init; } = string.Empty;
    public string TradeStatus { get; init; } = string.Empty;
    public string? TradeNo { get; init; }
    public string? SendPayDate { get; init; }

    public bool IsPaid =>
        string.Equals(TradeStatus, "TRADE_SUCCESS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(TradeStatus, "TRADE_FINISHED", StringComparison.OrdinalIgnoreCase);

    public static AlipayQueryResult From(Dictionary<string, string?> dict) => new()
    {
        Code = GetField(dict, "code"),
        TradeStatus = GetField(dict, "trade_status"),
        TradeNo = dict.GetValueOrDefault("trade_no"),
        SendPayDate = dict.GetValueOrDefault("send_pay_date")
    };

    private static string GetField(Dictionary<string, string?> dict, string key)
        => dict.TryGetValue(key, out var v) && v is not null ? v : string.Empty;
}

/// <summary>交易退款响应。</summary>
public sealed class AlipayRefundResult
{
    public string Code { get; init; } = string.Empty;
    public string FundChange { get; init; } = string.Empty;
    public string? TradeNo { get; init; }
    public string? OutTradeNo { get; init; }
    public string? SubMsg { get; init; }

    public bool IsSuccess => string.Equals(Code, "10000", StringComparison.Ordinal);

    public static AlipayRefundResult From(Dictionary<string, string?> dict) => new()
    {
        Code = GetField(dict, "code"),
        FundChange = GetField(dict, "fund_change"),
        TradeNo = dict.GetValueOrDefault("trade_no"),
        OutTradeNo = dict.GetValueOrDefault("out_trade_no"),
        SubMsg = dict.GetValueOrDefault("sub_msg")
    };

    private static string GetField(Dictionary<string, string?> dict, string key)
        => dict.TryGetValue(key, out var v) && v is not null ? v : string.Empty;
}

/// <summary>退款查询响应。</summary>
public sealed class AlipayQueryRefundResult
{
    public string Code { get; init; } = string.Empty;
    public string RefundStatus { get; init; } = string.Empty;
    public string? GmtRefundPay { get; init; }

    public bool Succeeded => string.Equals(RefundStatus, "REFUND_SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static AlipayQueryRefundResult From(Dictionary<string, string?> dict) => new()
    {
        Code = GetField(dict, "code"),
        RefundStatus = GetField(dict, "refund_status"),
        GmtRefundPay = dict.GetValueOrDefault("gmt_refund_pay")
    };

    private static string GetField(Dictionary<string, string?> dict, string key)
        => dict.TryGetValue(key, out var v) && v is not null ? v : string.Empty;
}
