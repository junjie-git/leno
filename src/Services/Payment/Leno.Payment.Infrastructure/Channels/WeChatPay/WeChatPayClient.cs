using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels.WeChatPay;

/// <summary>
/// 微信支付渠道 HTTP 客户端，封装统一下单/订单查询/退款/退款查询。
/// 通过 <see cref="HttpClient"/> 向微信支付网关发起真实 HTTP 调用（XML 报文）并解析响应。
/// </summary>
public sealed class WeChatPayClient
{
    private const string DefaultHost = "https://api.mch.weixin.qq.com";
    private const string UnifiedOrderPath = "/pay/unifiedorder";
    private const string OrderQueryPath = "/pay/orderquery";
    private const string RefundPath = "/secapi/pay/refund";
    private const string RefundQueryPath = "/pay/refundquery";

    private readonly HttpClient _httpClient;
    private readonly ILogger<WeChatPayClient> _logger;

    public WeChatPayClient(HttpClient httpClient, ILogger<WeChatPayClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>统一下单，获取预支付参数。</summary>
    public async Task<WeChatPayUnifiedOrderResult> UnifiedOrderAsync(
        ChannelConfig config, string outTradeNo, int totalFee, string body, string tradeType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var parameters = BuildBaseParameters(config);
        parameters["body"] = body;
        parameters["out_trade_no"] = outTradeNo;
        parameters["total_fee"] = totalFee.ToString(CultureInfo.InvariantCulture);
        parameters["spbill_create_ip"] = "127.0.0.1";
        parameters["notify_url"] = config.NotifyUrl;
        parameters["trade_type"] = tradeType;
        parameters["sign"] = WeChatPaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        var requestXml = BuildXml(parameters);
        _logger.LogInformation("微信支付统一下单 Url={Url} OutTradeNo={OutTradeNo} TradeType={TradeType} Request={Request}",
            BuildUrl(UnifiedOrderPath), outTradeNo, tradeType, requestXml);

        var responseXml = await PostXmlAsync(BuildUrl(UnifiedOrderPath), requestXml, ct);
        var dict = ParseXml(responseXml);
        return WeChatPayUnifiedOrderResult.From(dict);
    }

    /// <summary>订单查询，主动查询支付状态。</summary>
    public async Task<WeChatPayQueryOrderResult> QueryOrderAsync(
        ChannelConfig config, string outTradeNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var parameters = BuildBaseParameters(config);
        parameters["out_trade_no"] = outTradeNo;
        parameters["sign"] = WeChatPaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("微信支付订单查询 Url={Url} OutTradeNo={OutTradeNo}",
            BuildUrl(OrderQueryPath), outTradeNo);

        var requestXml = BuildXml(parameters);
        var responseXml = await PostXmlAsync(BuildUrl(OrderQueryPath), requestXml, ct);
        var dict = ParseXml(responseXml);
        return WeChatPayQueryOrderResult.From(dict);
    }

    /// <summary>申请退款。</summary>
    public async Task<WeChatPayRefundResult> RefundAsync(
        ChannelConfig config, string outTradeNo, string outRefundNo, int totalFee, int refundFee, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var parameters = BuildBaseParameters(config);
        parameters["out_trade_no"] = outTradeNo;
        parameters["out_refund_no"] = outRefundNo;
        parameters["total_fee"] = totalFee.ToString(CultureInfo.InvariantCulture);
        parameters["refund_fee"] = refundFee.ToString(CultureInfo.InvariantCulture);
        parameters["op_user_id"] = config.MchId;
        parameters["notify_url"] = config.RefundNotifyUrl;
        parameters["sign"] = WeChatPaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("微信支付退款 Url={Url} OutRefundNo={OutRefundNo} RefundFee={RefundFee}",
            BuildUrl(RefundPath), outRefundNo, refundFee);

        var requestXml = BuildXml(parameters);
        var responseXml = await PostXmlAsync(BuildUrl(RefundPath), requestXml, ct);
        var dict = ParseXml(responseXml);
        return WeChatPayRefundResult.From(dict);
    }

    /// <summary>退款查询，主动查询退款到账状态。</summary>
    public async Task<WeChatPayQueryRefundResult> QueryRefundAsync(
        ChannelConfig config, string outRefundNo, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var parameters = BuildBaseParameters(config);
        parameters["out_refund_no"] = outRefundNo;
        parameters["sign"] = WeChatPaySignatureHelper.GenerateSign(parameters, config.ApiKey);

        _logger.LogInformation("微信支付退款查询 Url={Url} OutRefundNo={OutRefundNo}",
            BuildUrl(RefundQueryPath), outRefundNo);

        var requestXml = BuildXml(parameters);
        var responseXml = await PostXmlAsync(BuildUrl(RefundQueryPath), requestXml, ct);
        var dict = ParseXml(responseXml);
        return WeChatPayQueryRefundResult.From(dict);
    }

    private static Dictionary<string, string> BuildBaseParameters(ChannelConfig config)
    {
        return new Dictionary<string, string>
        {
            ["appid"] = config.AppId,
            ["mch_id"] = config.MchId,
            ["nonce_str"] = Guid.NewGuid().ToString("N")
        };
    }

    private string BuildUrl(string path)
    {
        var baseAddress = _httpClient.BaseAddress;
        return baseAddress is null
            ? DefaultHost + path
            : new Uri(baseAddress, path).ToString();
    }

    private static string BuildXml(Dictionary<string, string> parameters)
    {
        var sb = new StringBuilder("<xml>");
        foreach (var kv in parameters)
        {
            sb.Append('<').Append(kv.Key).Append('>')
              .Append(kv.Value)
              .Append("</").Append(kv.Key).Append('>');
        }

        sb.Append("</xml>");
        return sb.ToString();
    }

    private static Dictionary<string, string> ParseXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("微信支付响应 XML 缺少根节点");
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var el in root.Elements())
        {
            dict[el.Name.LocalName] = el.Value;
        }

        return dict;
    }

    private async Task<string> PostXmlAsync(string url, string xml, CancellationToken ct)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        using var response = await _httpClient.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("微信支付网关 HTTP 调用失败 Url={Url} Status={Status} Body={Body}",
                url, response.StatusCode, body);
            throw new HttpRequestException($"微信支付网关返回非成功状态码 {response.StatusCode}");
        }

        return body;
    }
}

/// <summary>统一下单响应。</summary>
public sealed class WeChatPayUnifiedOrderResult
{
    public string? ReturnCode { get; init; }
    public string? ResultCode { get; init; }
    public string? PrepayId { get; init; }
    public string? CodeUrl { get; init; }
    public string? MwebUrl { get; init; }
    public string? TransactionId { get; init; }
    public string? ErrCodeDes { get; init; }

    public bool IsSuccess =>
        string.Equals(ReturnCode, "SUCCESS", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ResultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayUnifiedOrderResult From(Dictionary<string, string> dict) => new()
    {
        ReturnCode = dict.GetValueOrDefault("return_code"),
        ResultCode = dict.GetValueOrDefault("result_code"),
        PrepayId = dict.GetValueOrDefault("prepay_id"),
        CodeUrl = dict.GetValueOrDefault("code_url"),
        MwebUrl = dict.GetValueOrDefault("mweb_url"),
        TransactionId = dict.GetValueOrDefault("transaction_id"),
        ErrCodeDes = dict.GetValueOrDefault("err_code_des")
    };
}

/// <summary>订单查询响应。</summary>
public sealed class WeChatPayQueryOrderResult
{
    public string? ReturnCode { get; init; }
    public string? ResultCode { get; init; }
    public string? TradeState { get; init; }
    public string? TransactionId { get; init; }
    public string? TimeEnd { get; init; }

    public bool IsPaid => string.Equals(TradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayQueryOrderResult From(Dictionary<string, string> dict) => new()
    {
        ReturnCode = dict.GetValueOrDefault("return_code"),
        ResultCode = dict.GetValueOrDefault("result_code"),
        TradeState = dict.GetValueOrDefault("trade_state"),
        TransactionId = dict.GetValueOrDefault("transaction_id"),
        TimeEnd = dict.GetValueOrDefault("time_end")
    };
}

/// <summary>退款响应。</summary>
public sealed class WeChatPayRefundResult
{
    public string? ReturnCode { get; init; }
    public string? ResultCode { get; init; }
    public string? RefundId { get; init; }
    public string? OutRefundNo { get; init; }
    public string? ErrCodeDes { get; init; }

    public bool IsSuccess =>
        string.Equals(ReturnCode, "SUCCESS", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ResultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayRefundResult From(Dictionary<string, string> dict) => new()
    {
        ReturnCode = dict.GetValueOrDefault("return_code"),
        ResultCode = dict.GetValueOrDefault("result_code"),
        RefundId = dict.GetValueOrDefault("refund_id"),
        OutRefundNo = dict.GetValueOrDefault("out_refund_no"),
        ErrCodeDes = dict.GetValueOrDefault("err_code_des")
    };
}

/// <summary>退款查询响应。</summary>
public sealed class WeChatPayQueryRefundResult
{
    public string? ReturnCode { get; init; }
    public string? ResultCode { get; init; }
    public string? RefundStatus { get; init; }
    public string? RefundSuccessTime { get; init; }

    public bool Succeeded => string.Equals(RefundStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase);

    public static WeChatPayQueryRefundResult From(Dictionary<string, string> dict) => new()
    {
        ReturnCode = dict.GetValueOrDefault("return_code"),
        ResultCode = dict.GetValueOrDefault("result_code"),
        RefundStatus = dict.GetValueOrDefault("refund_status"),
        RefundSuccessTime = dict.GetValueOrDefault("refund_success_time")
    };
}
