using System.Text;
using System.Text.Json;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Channels.Sms;

/// <summary>
/// 短信服务商客户端，通过 HTTP 调用短信服务商 API。
/// </summary>
public sealed class SmsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SmsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmsClient> _logger;

    public SmsClient(IOptions<SmsOptions> options, HttpClient httpClient, ILogger<SmsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 发送短信，返回发送结果。
    /// </summary>
    public async Task<ChannelSendResult> SendAsync(string phoneNumber, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new ChannelSendResult(false, "手机号为空", "SMS_PHONE_EMPTY", null);
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKey))
        {
            _logger.LogWarning("短信渠道未配置 AccessKey");
            return new ChannelSendResult(false, "短信渠道未配置", "SMS_CONFIG_MISSING", null);
        }

        try
        {
            var requestUrl = string.IsNullOrWhiteSpace(_options.Endpoint)
                ? "https://dysmsapi.aliyuncs.com"
                : _options.Endpoint;

            var requestBody = new
            {
                PhoneNumbers = phoneNumber,
                SignName = _options.SignName,
                TemplateCode = _options.TemplateCode,
                TemplateParam = JsonSerializer.Serialize(new { content }, JsonOptions)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.AccessKey}");

            using var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("短信已发送 Phone={Phone}", phoneNumber);
                return new ChannelSendResult(true, null, null, responseContent);
            }

            _logger.LogWarning("短信发送失败 Phone={Phone} Status={Status} Response={Response}", phoneNumber, response.StatusCode, responseContent);
            return new ChannelSendResult(false, $"短信服务返回 {(int)response.StatusCode}", "SMS_HTTP_ERROR", responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "短信发送异常 Phone={Phone}", phoneNumber);
            return new ChannelSendResult(false, ex.Message, "SMS_EXCEPTION", null);
        }
    }
}
