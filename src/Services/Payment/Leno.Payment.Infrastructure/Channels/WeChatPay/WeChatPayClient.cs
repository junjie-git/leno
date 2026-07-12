using System.Globalization;
using System.Text;
using System.Text.Json;
using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Payment.Infrastructure.Channels.WeChatPay;

/// <summary>
/// 微信支付渠道 HTTP 客户端，基于 APIv3 JSON 协议。
/// 封装统一下单/订单查询/关闭订单/退款/退款查询，使用 RSA-SHA256 签名。
/// 通过 <see cref="HttpClient"/> 向微信支付网关发起真实 HTTP 调用并解析 JSON 响应。
/// </summary>
public class WeChatPayClient
{
    private const string DefaultHost = "https://api.mch.weixin.qq.com";
    private const string CreatePaymentPath = "/v3/pay/transactions/{0}";
    private const string QueryPaymentPath = "/v3/pay/transactions/out-trade-no/{0}";
    private const string ClosePaymentPath = "/v3/pay/transactions/out-trade-no/{0}/close";
    private const string RefundPath = "/v3/refund/domestic/refunds";
    private const string QueryRefundPath = "/v3/refund/domestic/refunds/{0}";

    private readonly HttpClient _httpClient;
    private readonly WeChatPayOptions _options;
    private readonly ILogger<WeChatPayClient> _logger;

    public WeChatPayClient(HttpClient httpClient, IOptions<WeChatPayOptions> options, ILogger<WeChatPayClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>统一下单（JSAPI/Native/H5），获取预支付参数。</summary>
    public virtual async Task<WeChatPayUnifiedOrderResult> UnifiedOrderAsync(
        ChannelConfig config, string outTradeNo, int totalFee, string description, string tradeType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = string.Format(CreatePaymentPath, tradeType.ToLowerInvariant());
        var url = BuildUrl(path);
        var body = BuildCreatePaymentJson(config, outTradeNo, totalFee, description, tradeType);

        var responseJson = await SendAsync(HttpMethod.Post, path, url, body, ct);
        var doc = JsonDocument.Parse(responseJson);
        return WeChatPayUnifiedOrderResult.From(doc.RootElement);
    }

    /// <summary>订单查询，主动查询支付状态。</summary>
    public virtual async Task<WeChatPayQueryOrderResult> QueryOrderAsync(
        ChannelConfig config, string outTradeNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = string.Format(QueryPaymentPath, outTradeNo);
        var url = BuildUrl(path) + $"?mchid={config.MchId}";

        var responseJson = await SendAsync(HttpMethod.Get, path, url, string.Empty, ct);
        var doc = JsonDocument.Parse(responseJson);
        return WeChatPayQueryOrderResult.From(doc.RootElement);
    }

    /// <summary>关闭订单。</summary>
    public virtual async Task CloseOrderAsync(
        ChannelConfig config, string outTradeNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = string.Format(ClosePaymentPath, outTradeNo);
        var url = BuildUrl(path);
        var body = BuildJson(new Dictionary<string, string>
        {
            ["mchid"] = config.MchId
        });

        await SendAsync(HttpMethod.Post, path, url, body, ct);
    }

    /// <summary>申请退款。</summary>
    public virtual async Task<WeChatPayRefundResult> RefundAsync(
        ChannelConfig config, string outTradeNo, string outRefundNo, int totalFee, int refundFee, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = RefundPath;
        var url = BuildUrl(path);
        var body = BuildRefundJson(config, outTradeNo, outRefundNo, totalFee, refundFee);

        var responseJson = await SendAsync(HttpMethod.Post, path, url, body, ct);
        var doc = JsonDocument.Parse(responseJson);
        return WeChatPayRefundResult.From(doc.RootElement);
    }

    /// <summary>退款查询，主动查询退款到账状态。</summary>
    public virtual async Task<WeChatPayQueryRefundResult> QueryRefundAsync(
        ChannelConfig config, string outRefundNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = string.Format(QueryRefundPath, outRefundNo);
        var url = BuildUrl(path);

        var responseJson = await SendAsync(HttpMethod.Get, path, url, string.Empty, ct);
        var doc = JsonDocument.Parse(responseJson);
        return WeChatPayQueryRefundResult.From(doc.RootElement);
    }

    private string BuildCreatePaymentJson(ChannelConfig config, string outTradeNo, int totalFee, string description, string tradeType)
    {
        var json = new StringBuilder();
        json.Append('{');
        AppendJsonProperty(json, "appid", config.AppId);
        json.Append(',');
        AppendJsonProperty(json, "mchid", config.MchId);
        json.Append(',');
        AppendJsonProperty(json, "description", description);
        json.Append(',');
        AppendJsonProperty(json, "out_trade_no", outTradeNo);
        json.Append(',');
        AppendJsonProperty(json, "notify_url", config.NotifyUrl);
        json.Append(',');
        json.Append("\"amount\":{");
        AppendJsonProperty(json, "total", totalFee.ToString(CultureInfo.InvariantCulture));
        json.Append(',');
        AppendJsonProperty(json, "currency", "CNY");
        json.Append('}');

        if (string.Equals(tradeType, "JSAPI", StringComparison.OrdinalIgnoreCase))
        {
            json.Append(',');
            json.Append("\"payer\":{\"openid\":\"\"}");
        }
        else if (string.Equals(tradeType, "H5", StringComparison.OrdinalIgnoreCase))
        {
            json.Append(',');
            json.Append("\"scene_info\":{");
            AppendJsonProperty(json, "payer_client_ip", "127.0.0.1");
            json.Append(',');
            json.Append("\"h5_info\":{");
            AppendJsonProperty(json, "type", "Wap");
            json.Append('}');
            json.Append('}');
        }

        json.Append('}');
        return json.ToString();
    }

    private string BuildRefundJson(ChannelConfig config, string outTradeNo, string outRefundNo, int totalFee, int refundFee)
    {
        var json = new StringBuilder();
        json.Append('{');
        AppendJsonProperty(json, "out_trade_no", outTradeNo);
        json.Append(',');
        AppendJsonProperty(json, "out_refund_no", outRefundNo);
        json.Append(',');
        AppendJsonProperty(json, "notify_url", config.RefundNotifyUrl);
        json.Append(',');
        json.Append("\"amount\":{");
        AppendJsonProperty(json, "refund", refundFee.ToString(CultureInfo.InvariantCulture));
        json.Append(',');
        AppendJsonProperty(json, "total", totalFee.ToString(CultureInfo.InvariantCulture));
        json.Append(',');
        AppendJsonProperty(json, "currency", "CNY");
        json.Append('}');
        json.Append('}');
        return json.ToString();
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

    private static void AppendJsonProperty(StringBuilder sb, string name, string value)
    {
        sb.Append('"').Append(name).Append("\":\"").Append(value).Append('"');
    }

    private string BuildUrl(string path)
    {
        var baseAddress = _httpClient.BaseAddress;
        return baseAddress is null
            ? DefaultHost + path
            : new Uri(baseAddress, path).ToString();
    }

    private string GetPrivateKey()
    {
        if (!string.IsNullOrEmpty(_options.PrivateKey))
        {
            return _options.PrivateKey;
        }

        if (!string.IsNullOrEmpty(_options.PrivateKeyPath))
        {
            return WeChatPayV3SignatureHelper.LoadPrivateKeyFromFile(_options.PrivateKeyPath);
        }

        throw new InvalidOperationException("微信支付 V3 私钥未配置，请设置 PrivateKey 或 PrivateKeyPath");
    }

    private async Task<string> SendAsync(HttpMethod method, string path, string url, string body, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");
        var privateKey = GetPrivateKey();
        var authorization = WeChatPayV3SignatureHelper.GenerateAuthorization(
            method.Method, path, body, timestamp, nonce, privateKey, _options.MchId, _options.SerialNo);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "Leno-Payment/1.0");

        if (method != HttpMethod.Get && !string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        _logger.LogInformation("微信支付 V3 请求 Method={Method} Url={Url} Body={Body}", method, url, body);

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("微信支付 V3 网关 HTTP 调用失败 Url={Url} Status={Status} Body={Body}",
                url, response.StatusCode, responseBody);
            throw new HttpRequestException($"微信支付网关返回非成功状态码 {response.StatusCode}: {responseBody}");
        }

        return responseBody;
    }
}

/// <summary>统一下单响应。</summary>
public sealed class WeChatPayUnifiedOrderResult
{
    public string? PrepayId { get; init; }
    public string? CodeUrl { get; init; }
    public string? H5Url { get; init; }
    public string? TransactionId { get; init; }
    public string? TradeState { get; init; }
    public string? ErrCodeDes { get; init; }

    public bool IsSuccess => PrepayId != null || CodeUrl != null || H5Url != null;

    public static WeChatPayUnifiedOrderResult From(JsonElement root)
    {
        return new WeChatPayUnifiedOrderResult
        {
            PrepayId = root.TryGetProperty("prepay_id", out var prepay) ? prepay.GetString() : null,
            CodeUrl = root.TryGetProperty("code_url", out var codeUrl) ? codeUrl.GetString() : null,
            H5Url = root.TryGetProperty("h5_url", out var h5Url) ? h5Url.GetString() : null,
            TransactionId = root.TryGetProperty("transaction_id", out var txnId) ? txnId.GetString() : null,
            TradeState = root.TryGetProperty("trade_state", out var state) ? state.GetString() : null,
            ErrCodeDes = root.TryGetProperty("message", out var msg) ? msg.GetString() : null
        };
    }
}

/// <summary>订单查询响应。</summary>
public sealed class WeChatPayQueryOrderResult
{
    public string? TradeState { get; init; }
    public string? TransactionId { get; init; }
    public string? TimeEnd { get; init; }

    public bool IsPaid => string.Equals(TradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayQueryOrderResult From(JsonElement root)
    {
        return new WeChatPayQueryOrderResult
        {
            TradeState = root.TryGetProperty("trade_state", out var state) ? state.GetString() : null,
            TransactionId = root.TryGetProperty("transaction_id", out var txnId) ? txnId.GetString() : null,
            TimeEnd = root.TryGetProperty("success_time", out var timeEnd) ? timeEnd.GetString() : null
        };
    }
}

/// <summary>退款响应。</summary>
public sealed class WeChatPayRefundResult
{
    public string? RefundId { get; init; }
    public string? OutRefundNo { get; init; }
    public string? Status { get; init; }
    public string? ErrCodeDes { get; init; }

    public bool IsSuccess =>
        string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "PROCESSING", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayRefundResult From(JsonElement root)
    {
        return new WeChatPayRefundResult
        {
            RefundId = root.TryGetProperty("refund_id", out var refundId) ? refundId.GetString() : null,
            OutRefundNo = root.TryGetProperty("out_refund_no", out var outRefundNo) ? outRefundNo.GetString() : null,
            Status = root.TryGetProperty("status", out var status) ? status.GetString() : null,
            ErrCodeDes = root.TryGetProperty("message", out var msg) ? msg.GetString() : null
        };
    }
}

/// <summary>退款查询响应。</summary>
public sealed class WeChatPayQueryRefundResult
{
    public string? RefundStatus { get; init; }
    public string? RefundSuccessTime { get; init; }

    public bool Succeeded => string.Equals(RefundStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayQueryRefundResult From(JsonElement root)
    {
        return new WeChatPayQueryRefundResult
        {
            RefundStatus = root.TryGetProperty("status", out var status) ? status.GetString() : null,
            RefundSuccessTime = root.TryGetProperty("success_time", out var successTime) ? successTime.GetString() : null
        };
    }
}