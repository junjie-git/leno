using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Channels.Sms;

/// <summary>
/// 短信服务商客户端（模拟实现）。
/// 实际实现应通过 HTTP 调用阿里云/腾讯云短信 API，当前为模拟桩。
/// </summary>
public sealed class SmsClient
{
    private readonly SmsOptions _options;
    private readonly ILogger<SmsClient> _logger;

    public SmsClient(IOptions<SmsOptions> options, ILogger<SmsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 发送短信，返回是否成功。
    /// 当前为模拟实现，配置了 AccessKey 则返回成功，否则返回失败。
    /// </summary>
    public Task<(bool Succeeded, string? FailReason)> SendAsync(string phoneNumber, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Task.FromResult<(bool Succeeded, string? FailReason)>((false, "手机号为空"));
        }

        if (string.IsNullOrWhiteSpace(_options.AccessKey))
        {
            _logger.LogWarning("短信渠道未配置 AccessKey，模拟发送失败 Phone={Phone}", phoneNumber);
            return Task.FromResult<(bool Succeeded, string? FailReason)>((false, "短信渠道未配置"));
        }

        _logger.LogInformation("短信已发送（模拟）Phone={Phone} Content={Content}", phoneNumber, content);
        return Task.FromResult<(bool Succeeded, string? FailReason)>((true, null));
    }
}
