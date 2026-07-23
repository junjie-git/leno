using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 阿里云短信发送提供商，通过 HTTP 调用阿里云短信 API。
/// 实现 <see cref="ISmsProvider"/>，由 <see cref="SmsChannel"/> 外壳类按 <see cref="IChannelSelector"/> 选择。
/// 使用阿里云 RPC 风格 HMAC-SHA1 签名算法认证。
/// </summary>
public sealed class AliyunSmsProvider : ISmsProvider
{
    private const string AliyunSmsEndpoint = "https://dysmsapi.aliyuncs.com/";
    private const string ApiVersion = "2017-05-25";
    private const string DefaultRegionId = "cn-hangzhou";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly SmsChannelOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AliyunSmsProvider> _logger;

    public AliyunSmsProvider(IOptions<SmsChannelOptions> options, HttpClient httpClient, ILogger<AliyunSmsProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "Aliyun";

    /// <inheritdoc />
    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var phoneNumber = request.Recipient.PhoneNumber;
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning("用户手机号为空，跳过短信发送 UserId={UserId}", request.Recipient.UserId);
            return new ChannelSendResult(false, "用户手机号为空", "SMS_PHONE_EMPTY", null);
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKeyId))
        {
            _logger.LogWarning("短信渠道未配置 AccessKeyId");
            return new ChannelSendResult(false, "短信渠道未配置", "SMS_CONFIG_MISSING", null);
        }

        var smsTemplateCode = request.SmsTemplateCode;
        if (string.IsNullOrWhiteSpace(smsTemplateCode))
        {
            _logger.LogWarning("短信模板编码未配置，跳过发送 UserId={UserId}", request.Recipient.UserId);
            return new ChannelSendResult(false, "短信模板编码未配置", "SMS_TEMPLATE_CODE_MISSING", null);
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(HttpTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            // P1-27：构造阿里云 RPC 风格请求参数，使用 HMAC-SHA1 签名算法认证。
            var parameters = BuildRequestParameters(phoneNumber, smsTemplateCode, request.Body);
            var signature = ComputeSignature(parameters, _options.AccessKeySecret);
            parameters["Signature"] = signature;

            var queryString = BuildQueryString(parameters);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{AliyunSmsEndpoint}?{queryString}");

            using var response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(linkedCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("阿里云短信已发送 Phone={Phone}", phoneNumber);

                // 从响应 JSON 中解析 BizId 作为 ChannelMessageId，
                // 替代将整个响应体作为 ChannelMessageId（被 HasMaxLength(128) 截断后与回执不匹配）
                string? bizId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("BizId", out var bizIdElement))
                    {
                        bizId = bizIdElement.GetString();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "解析阿里云短信响应失败，BizId 不可用 Response={Response}", responseContent);
                }

                return new ChannelSendResult(true, null, null, bizId);
            }

            _logger.LogWarning("阿里云短信发送失败 Phone={Phone} Status={Status} Response={Response}", phoneNumber, response.StatusCode, responseContent);
            return new ChannelSendResult(false, $"阿里云短信服务返回 {(int)response.StatusCode}", "SMS_HTTP_ERROR", responseContent);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("阿里云短信发送超时 Phone={Phone}", phoneNumber);
            return new ChannelSendResult(false, "阿里云短信发送超时", "SMS_TIMEOUT", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "阿里云短信发送异常 Phone={Phone}", phoneNumber);
            return new ChannelSendResult(false, ex.Message, "SMS_EXCEPTION", null);
        }
    }

    /// <summary>
    /// 构造阿里云 SMS API 请求参数（公共参数 + 业务参数）。
    /// </summary>
    private SortedDictionary<string, string> BuildRequestParameters(string phoneNumber, string smsTemplateCode, string body)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Format"] = "JSON",
            ["Version"] = ApiVersion,
            ["AccessKeyId"] = _options.AccessKeyId,
            ["SignatureMethod"] = "HMAC-SHA1",
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["SignatureVersion"] = "1.0",
            ["SignatureNonce"] = Guid.NewGuid().ToString("N"),
            ["Action"] = "SendSms",
            ["RegionId"] = DefaultRegionId,
            ["PhoneNumbers"] = phoneNumber,
            ["SignName"] = _options.SignName,
            ["TemplateCode"] = smsTemplateCode,
            ["TemplateParam"] = JsonSerializer.Serialize(new { content = body }, JsonOptions)
        };
    }

    /// <summary>
    /// 计算阿里云 RPC 风格签名。
    /// 算法：Signature = Base64(HMAC-SHA1(StringToSign, AccessKeySecret + "&amp;"))
    /// StringToSign = HTTPMethod + "&amp;" + percent_encode("/") + "&amp;" + percent_encode(canonicalized_query_string)
    /// </summary>
    private static string ComputeSignature(SortedDictionary<string, string> parameters, string accessKeySecret)
    {
        // 1. 构造规范化查询字符串（参数已按 key 排序，每个 key 和 value 进行 RFC3986 编码）
        var canonicalized = string.Join("&", parameters.Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));

        // 2. 构造待签名字符串：GET&%2F&{percent_encode(canonicalized)}
        var stringToSign = "GET&" + PercentEncode("/") + "&" + PercentEncode(canonicalized);

        // 3. HMAC-SHA1 签名，密钥为 AccessKeySecret + "&"
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(accessKeySecret + "&"));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 构造 URL 查询字符串（使用 RFC3986 编码）。
    /// </summary>
    private static string BuildQueryString(SortedDictionary<string, string> parameters)
    {
        return string.Join("&", parameters.Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));
    }

    /// <summary>
    /// 阿里云 RPC 风格的 RFC3986 百分号编码。
    /// 规则：A-Z a-z 0-9 - _ . ~ 不编码，其余字符编码为 %XX（大写十六进制）。
    /// </summary>
    private static string PercentEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // 使用 Uri.EscapeDataString 进行基础编码，然后修正差异：
        // Uri.EscapeDataString 已对大部分字符编码，但需确保 * 和空格的编码符合 RFC3986
        var encoded = Uri.EscapeDataString(value);
        // Uri.EscapeDataString 将空格编码为 %20（符合 RFC3986），* 不编码（符合 RFC3986）
        return encoded;
    }
}

/// <summary>
/// 腾讯云短信发送提供商，通过 HTTP 调用腾讯云短信 API。
/// 实现 <see cref="ISmsProvider"/>，由 <see cref="SmsChannel"/> 外壳类按 <see cref="IChannelSelector"/> 选择。
/// </summary>
public sealed class TencentSmsProvider : ISmsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly SmsChannelOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TencentSmsProvider> _logger;

    public TencentSmsProvider(IOptions<SmsChannelOptions> options, HttpClient httpClient, ILogger<TencentSmsProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "Tencent";

    /// <inheritdoc />
    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var phoneNumber = request.Recipient.PhoneNumber;
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning("用户手机号为空，跳过短信发送 UserId={UserId}", request.Recipient.UserId);
            return new ChannelSendResult(false, "用户手机号为空", "SMS_PHONE_EMPTY", null);
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKeyId))
        {
            _logger.LogWarning("短信渠道未配置 AccessKeyId");
            return new ChannelSendResult(false, "短信渠道未配置", "SMS_CONFIG_MISSING", null);
        }

        var smsTemplateCode = request.SmsTemplateCode;
        if (string.IsNullOrWhiteSpace(smsTemplateCode))
        {
            _logger.LogWarning("短信模板编码未配置，跳过发送 UserId={UserId}", request.Recipient.UserId);
            return new ChannelSendResult(false, "短信模板编码未配置", "SMS_TEMPLATE_CODE_MISSING", null);
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(HttpTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var requestBody = new
            {
                PhoneNumberSet = new[] { phoneNumber },
                SignName = _options.SignName,
                TemplateId = smsTemplateCode,
                TemplateParamSet = new[] { request.Body }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://sms.tencentcloudapi.com")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.AccessKeyId}");

            using var response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(linkedCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("腾讯云短信已发送 Phone={Phone}", phoneNumber);

                // 从响应 JSON 中解析 SerialNo 作为 ChannelMessageId，
                // 替代将整个响应体作为 ChannelMessageId（被 HasMaxLength(128) 截断后与回执不匹配）
                string? serialNo = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("SerialNo", out var serialNoElement))
                    {
                        serialNo = serialNoElement.GetString();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "解析腾讯云短信响应失败，SerialNo 不可用 Response={Response}", responseContent);
                }

                return new ChannelSendResult(true, null, null, serialNo);
            }

            _logger.LogWarning("腾讯云短信发送失败 Phone={Phone} Status={Status} Response={Response}", phoneNumber, response.StatusCode, responseContent);
            return new ChannelSendResult(false, $"腾讯云短信服务返回 {(int)response.StatusCode}", "SMS_HTTP_ERROR", responseContent);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("腾讯云短信发送超时 Phone={Phone}", phoneNumber);
            return new ChannelSendResult(false, "腾讯云短信发送超时", "SMS_TIMEOUT", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "腾讯云短信发送异常 Phone={Phone}", phoneNumber);
            return new ChannelSendResult(false, ex.Message, "SMS_EXCEPTION", null);
        }
    }
}

/// <summary>
/// 短信渠道外壳类，按 <see cref="IChannelSelector"/> 在运行时选择具体的 <see cref="ISmsProvider"/> 发送。
/// 作为唯一的 <see cref="INotificationChannel"/>（<see cref="NotificationChannel.Sms"/>）注册到 DI，
/// 避免多个 SMS 实现注册为 <see cref="INotificationChannel"/> 时 <c>ToDictionary</c> 抛重复键异常。
/// </summary>
public sealed class SmsChannel : INotificationChannel
{
    private static readonly NotificationChannelMetadata MetadataValue = new(
        ChannelKey.Sms,
        "短信",
        new NotificationChannelCapabilities(
            RequiresRateLimit: true,
            SupportsAsyncReceipt: true,
            IsIdempotent: false,
            SupportsTemplate: true,
            Timeout: TimeSpan.FromSeconds(30)),
        IsEnabled: true,
        Priority: 10);

    private readonly Dictionary<string, ISmsProvider> _providers;
    private readonly IChannelSelector _channelSelector;
    private readonly ILogger<SmsChannel> _logger;

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc />
    public ChannelKey ChannelKey => ChannelKey.Sms;

    /// <inheritdoc />
    public NotificationChannelMetadata Metadata => MetadataValue;

    public SmsChannel(
        IEnumerable<ISmsProvider> providers,
        IChannelSelector channelSelector,
        ILogger<SmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(channelSelector);
        ArgumentNullException.ThrowIfNull(logger);

        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _channelSelector = channelSelector;
        _logger = logger;

        if (_providers.Count == 0)
        {
            _logger.LogWarning("SmsChannel 注册时未提供任何 ISmsProvider 实现，所有短信发送将返回 SMS_PROVIDER_NOT_FOUND");
        }
    }

    /// <inheritdoc />
    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerName = _channelSelector.SelectSmsProvider();
        if (string.IsNullOrWhiteSpace(providerName) || !_providers.TryGetValue(providerName, out var provider))
        {
            _logger.LogWarning("未找到短信提供商 Provider={Provider}", providerName);
            return new ChannelSendResult(false, "短信提供商未配置", "SMS_PROVIDER_NOT_FOUND", null);
        }

        return await provider.SendAsync(request, ct).ConfigureAwait(false);
    }
}
