using System.Text;
using System.Text.Json;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 阿里云短信发送渠道，通过 HTTP 调用阿里云短信 API。
/// </summary>
public sealed class AliyunSmsChannel : INotificationChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly SmsChannelOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AliyunSmsChannel> _logger;

    public AliyunSmsChannel(IOptions<SmsChannelOptions> options, HttpClient httpClient, ILogger<AliyunSmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Sms;

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

        try
        {
            using var timeoutCts = new CancellationTokenSource(HttpTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var requestBody = new
            {
                PhoneNumbers = phoneNumber,
                SignName = _options.SignName,
                TemplateCode = "SMS_000000", // 由模板系统控制
                TemplateParam = JsonSerializer.Serialize(new { content = request.Body }, JsonOptions)
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://dysmsapi.aliyuncs.com")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.AccessKeyId}");

            using var response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(linkedCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("阿里云短信已发送 Phone={Phone}", phoneNumber);
                return new ChannelSendResult(true, null, null, responseContent);
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
}

/// <summary>
/// 腾讯云短信发送渠道，通过 HTTP 调用腾讯云短信 API。
/// </summary>
public sealed class TencentSmsChannel : INotificationChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly SmsChannelOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TencentSmsChannel> _logger;

    public TencentSmsChannel(IOptions<SmsChannelOptions> options, HttpClient httpClient, ILogger<TencentSmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Sms;

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

        try
        {
            using var timeoutCts = new CancellationTokenSource(HttpTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var requestBody = new
            {
                PhoneNumberSet = new[] { phoneNumber },
                SignName = _options.SignName,
                TemplateId = "000000", // 由模板系统控制
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
                return new ChannelSendResult(true, null, null, responseContent);
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