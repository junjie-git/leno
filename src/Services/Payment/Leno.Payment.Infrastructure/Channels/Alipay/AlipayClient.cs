using System.Globalization;
using System.Text;
using System.Text.Json;
using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels.Alipay;

/// <summary>
/// 支付宝渠道 HTTP 客户端，封装预下单/交易查询/退款/退款查询/交易关闭。
/// 支持 alipay.trade.precreate（当面付）、alipay.trade.page.pay（PC 网页）、
/// alipay.trade.wap.pay（手机网页）、alipay.trade.app.pay（App 支付）。
/// POST 请求为 form-encoded（公共参数 + biz_content），响应为 JSON。
/// 通过 <see cref="HttpClient"/> 向支付宝网关发起真实 HTTP 调用并解析响应。
/// </summary>
public class AlipayClient
{
    private const string DefaultGateway = "https://openapi.alipay.com/gateway.do";
    private const string PreCreateMethod = "alipay.trade.precreate";
    private const string PagePayMethod = "alipay.trade.page.pay";
    private const string WapPayMethod = "alipay.trade.wap.pay";
    private const string AppPayMethod = "alipay.trade.app.pay";
    private const string QueryMethod = "alipay.trade.query";
    private const string CloseMethod = "alipay.trade.close";
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
    public virtual async Task<AlipayPreCreateResult> PreCreateAsync(
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

        _logger.LogInformation("支付宝当面付预下单 Url={Url} OutTradeNo={OutTradeNo} Subject={Subject}",
            BuildUrl(), outTradeNo, subject);

        var responseContent = await PostFormAsync(BuildUrl(), parameters, ct);
        var dict = ParseResponse(responseContent, PreCreateMethod);
        return AlipayPreCreateResult.From(dict);
    }

    /// <summary>PC 网页支付（alipay.trade.page.pay），构建支付跳转 URL。</summary>
    public virtual string BuildPagePayUrl(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject, string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(outTradeNo))
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        if (string.IsNullOrWhiteSpace(totalAmount))
            throw new ArgumentException("支付金额不可为空", nameof(totalAmount));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("商品标题不可为空", nameof(subject));

        var bizContent = BuildJson(new Dictionary<string, string>
        {
            ["out_trade_no"] = outTradeNo,
            ["total_amount"] = totalAmount,
            ["subject"] = subject,
            ["product_code"] = "FAST_INSTANT_TRADE_PAY"
        });
        var parameters = BuildBaseParameters(config, PagePayMethod, bizContent);
        parameters["notify_url"] = config.NotifyUrl;
        parameters["return_url"] = returnUrl;
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝 PC 网页支付 OutTradeNo={OutTradeNo} Subject={Subject}",
            outTradeNo, subject);

        return BuildGetUrl(parameters);
    }

    /// <summary>手机网页支付（alipay.trade.wap.pay），构建支付跳转 URL。</summary>
    public virtual string BuildWapPayUrl(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject, string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(outTradeNo))
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        if (string.IsNullOrWhiteSpace(totalAmount))
            throw new ArgumentException("支付金额不可为空", nameof(totalAmount));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("商品标题不可为空", nameof(subject));

        var bizContent = BuildJson(new Dictionary<string, string>
        {
            ["out_trade_no"] = outTradeNo,
            ["total_amount"] = totalAmount,
            ["subject"] = subject,
            ["product_code"] = "QUICK_WAP_WAY"
        });
        var parameters = BuildBaseParameters(config, WapPayMethod, bizContent);
        parameters["notify_url"] = config.NotifyUrl;
        parameters["return_url"] = returnUrl;
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝 WAP 支付 OutTradeNo={OutTradeNo} Subject={Subject}",
            outTradeNo, subject);

        return BuildGetUrl(parameters);
    }

    /// <summary>App 支付（alipay.trade.app.pay），构建签名的订单参数字符串供 App SDK 使用。</summary>
    public virtual string BuildAppPayOrderString(
        ChannelConfig config, string outTradeNo, string totalAmount, string subject)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(outTradeNo))
            throw new ArgumentException("商户支付单号不可为空", nameof(outTradeNo));
        if (string.IsNullOrWhiteSpace(totalAmount))
            throw new ArgumentException("支付金额不可为空", nameof(totalAmount));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("商品标题不可为空", nameof(subject));

        var bizContent = BuildJson(new Dictionary<string, string>
        {
            ["out_trade_no"] = outTradeNo,
            ["total_amount"] = totalAmount,
            ["subject"] = subject,
            ["product_code"] = "QUICK_MSECURITY_PAY"
        });
        var parameters = BuildBaseParameters(config, AppPayMethod, bizContent);
        parameters["notify_url"] = config.NotifyUrl;
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝 App 支付 OutTradeNo={OutTradeNo} Subject={Subject}",
            outTradeNo, subject);

        // App 支付返回的是 key=value 编码的参数字符串
        return BuildQueryString(parameters);
    }

    /// <summary>交易关闭（alipay.trade.close），关闭未支付的交易。</summary>
    public virtual async Task<AlipayCloseResult> CloseAsync(
        ChannelConfig config, string outTradeNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var bizContent = BuildJson(new Dictionary<string, string> { ["out_trade_no"] = outTradeNo });
        var parameters = BuildBaseParameters(config, CloseMethod, bizContent);
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝交易关闭 Url={Url} OutTradeNo={OutTradeNo}", BuildUrl(), outTradeNo);

        var responseContent = await PostFormAsync(BuildUrl(), parameters, ct);
        var dict = ParseResponse(responseContent, CloseMethod);
        return AlipayCloseResult.From(dict);
    }

    /// <summary>交易查询，主动查询支付状态。</summary>
    public virtual async Task<AlipayQueryResult> QueryAsync(
        ChannelConfig config, string outTradeNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var bizContent = BuildJson(new Dictionary<string, string> { ["out_trade_no"] = outTradeNo });
        var parameters = BuildBaseParameters(config, QueryMethod, bizContent);
        parameters["sign"] = AlipaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("支付宝交易查询 Url={Url} OutTradeNo={OutTradeNo}", BuildUrl(), outTradeNo);

        var responseContent = await PostFormAsync(BuildUrl(), parameters, ct);
        var dict = ParseResponse(responseContent, QueryMethod);
        return AlipayQueryResult.From(dict);
    }

    /// <summary>交易退款。</summary>
    public virtual async Task<AlipayRefundResult> RefundAsync(
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

        _logger.LogInformation("支付宝交易退款 Url={Url} OutRequestNo={OutRequestNo} RefundAmount={RefundAmount}",
            BuildUrl(), outRequestNo, refundAmount);

        var responseContent = await PostFormAsync(BuildUrl(), parameters, ct);
        var dict = ParseResponse(responseContent, RefundMethod);
        return AlipayRefundResult.From(dict);
    }

    /// <summary>退款查询，主动查询退款到账状态。</summary>
    public virtual async Task<AlipayQueryRefundResult> QueryRefundAsync(
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

        _logger.LogInformation("支付宝退款查询 Url={Url} OutRequestNo={OutRequestNo}", BuildUrl(), outRequestNo);

        var responseContent = await PostFormAsync(BuildUrl(), parameters, ct);
        var dict = ParseResponse(responseContent, RefundQueryMethod);
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

    private string BuildGetUrl(Dictionary<string, string> parameters)
    {
        var queryString = BuildQueryString(parameters);
        return $"{BuildUrl()}?{queryString}";
    }

    private static string BuildQueryString(Dictionary<string, string> parameters)
    {
        return string.Join("&", parameters.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
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

    private async Task<string> PostFormAsync(string url, Dictionary<string, string> formData, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("支付宝网关 HTTP 调用失败 Url={Url} Status={Status} Body={Body}",
                url, response.StatusCode, body);
            throw new HttpRequestException($"支付宝网关返回非成功状态码 {response.StatusCode}");
        }

        return body;
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

/// <summary>交易关闭响应。</summary>
public sealed class AlipayCloseResult
{
    public string Code { get; init; } = string.Empty;
    public string Msg { get; init; } = string.Empty;
    public string? TradeNo { get; init; }
    public string? OutTradeNo { get; init; }
    public string? SubCode { get; init; }
    public string? SubMsg { get; init; }

    public bool IsSuccess => string.Equals(Code, "10000", StringComparison.Ordinal);

    public static AlipayCloseResult From(Dictionary<string, string?> dict) => new()
    {
        Code = GetField(dict, "code"),
        Msg = GetField(dict, "msg"),
        TradeNo = dict.GetValueOrDefault("trade_no"),
        OutTradeNo = dict.GetValueOrDefault("out_trade_no"),
        SubCode = dict.GetValueOrDefault("sub_code"),
        SubMsg = dict.GetValueOrDefault("sub_msg")
    };

    private static string GetField(Dictionary<string, string?> dict, string key)
        => dict.TryGetValue(key, out var v) && v is not null ? v : string.Empty;
}
