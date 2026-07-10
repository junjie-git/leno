using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Leno.Payment.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels.WeChatPay;

/// <summary>
/// 微信支付渠道 HTTP 客户端，封装统一下单/订单查询/退款/退款查询。
/// 当前为模拟实现：构造 XML 请求并签名，解析模拟的 XML 响应；生产环境需配置真实商户证书与密钥后通过 <see cref="HttpClient"/> 发起调用。
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
        _logger.LogInformation("微信支付统一下单（模拟）Url={Url} OutTradeNo={OutTradeNo} TradeType={TradeType} Request={Request}",
            BuildUrl(UnifiedOrderPath), outTradeNo, tradeType, requestXml);

        // 模拟成功响应；生产环境：await PostXmlAsync(BuildUrl(UnifiedOrderPath), requestXml, ct)
        var dict = ParseXml(SimulateUnifiedOrderResponse(outTradeNo, tradeType));
        await Task.CompletedTask;
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

        _logger.LogInformation("微信支付订单查询（模拟）Url={Url} OutTradeNo={OutTradeNo}",
            BuildUrl(OrderQueryPath), outTradeNo);

        var dict = ParseXml(SimulateQueryOrderResponse(outTradeNo, paid: true));
        await Task.CompletedTask;
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

        _logger.LogInformation("微信支付退款（模拟）Url={Url} OutRefundNo={OutRefundNo} RefundFee={RefundFee}",
            BuildUrl(RefundPath), outRefundNo, refundFee);

        var dict = ParseXml(SimulateRefundResponse(outRefundNo));
        await Task.CompletedTask;
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

        _logger.LogInformation("微信支付退款查询（模拟）Url={Url} OutRefundNo={OutRefundNo}",
            BuildUrl(RefundQueryPath), outRefundNo);

        var dict = ParseXml(SimulateQueryRefundResponse(outRefundNo, succeeded: true));
        await Task.CompletedTask;
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

    private static string SimulateUnifiedOrderResponse(string outTradeNo, string tradeType)
    {
        var now = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var prepayId = "wx" + outTradeNo;
        var transactionId = "4200" + now + Random.Shared.Next(1000, 9999);
        var codeUrl = "weixin://wxpay/bizpayurl?pr=" + Random.Shared.Next(10000, 99999);
        var mwebUrl = "https://wx.tenpay.com/cgi-bin/mmpayweb-bin/checkmweb?prepay_id=" + prepayId;

        var sb = new StringBuilder("<xml>");
        sb.Append("<return_code>SUCCESS</return_code>");
        sb.Append("<result_code>SUCCESS</result_code>");
        sb.Append("<prepay_id>").Append(prepayId).Append("</prepay_id>");
        sb.Append("<transaction_id>").Append(transactionId).Append("</transaction_id>");
        if (tradeType == "NATIVE")
        {
            sb.Append("<code_url>").Append(codeUrl).Append("</code_url>");
        }
        else if (tradeType == "MWEB")
        {
            sb.Append("<mweb_url>").Append(mwebUrl).Append("</mweb_url>");
        }

        sb.Append("</xml>");
        return sb.ToString();
    }

    private static string SimulateQueryOrderResponse(string outTradeNo, bool paid)
    {
        var timeEnd = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var transactionId = "4200" + timeEnd + Random.Shared.Next(1000, 9999);

        var sb = new StringBuilder("<xml>");
        sb.Append("<return_code>SUCCESS</return_code>");
        sb.Append("<result_code>SUCCESS</result_code>");
        sb.Append("<out_trade_no>").Append(outTradeNo).Append("</out_trade_no>");
        sb.Append("<transaction_id>").Append(transactionId).Append("</transaction_id>");
        sb.Append("<trade_state>").Append(paid ? "SUCCESS" : "NOTPAY").Append("</trade_state>");
        if (paid)
        {
            sb.Append("<time_end>").Append(timeEnd).Append("</time_end>");
        }

        sb.Append("</xml>");
        return sb.ToString();
    }

    private static string SimulateRefundResponse(string outRefundNo)
    {
        var refundId = "5" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + Random.Shared.Next(1000, 9999);

        var sb = new StringBuilder("<xml>");
        sb.Append("<return_code>SUCCESS</return_code>");
        sb.Append("<result_code>SUCCESS</result_code>");
        sb.Append("<out_refund_no>").Append(outRefundNo).Append("</out_refund_no>");
        sb.Append("<refund_id>").Append(refundId).Append("</refund_id>");
        sb.Append("</xml>");
        return sb.ToString();
    }

    private static string SimulateQueryRefundResponse(string outRefundNo, bool succeeded)
    {
        var successTime = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        var sb = new StringBuilder("<xml>");
        sb.Append("<return_code>SUCCESS</return_code>");
        sb.Append("<result_code>SUCCESS</result_code>");
        sb.Append("<out_refund_no>").Append(outRefundNo).Append("</out_refund_no>");
        sb.Append("<refund_status>").Append(succeeded ? "SUCCESS" : "PROCESSING").Append("</refund_status>");
        if (succeeded)
        {
            sb.Append("<refund_success_time>").Append(successTime).Append("</refund_success_time>");
        }

        sb.Append("</xml>");
        return sb.ToString();
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
