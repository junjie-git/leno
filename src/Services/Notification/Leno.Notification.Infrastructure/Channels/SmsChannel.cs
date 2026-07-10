using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels.Sms;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 短信发送渠道，通过 <see cref="SmsClient"/> 调用短信服务商 API。
/// 当前为模拟实现，实际部署需配置服务商凭证。
/// </summary>
public sealed class SmsChannel : IChannel
{
    private readonly SmsClient _smsClient;
    private readonly ILogger<SmsChannel> _logger;

    public SmsChannel(SmsClient smsClient, ILogger<SmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(smsClient);
        ArgumentNullException.ThrowIfNull(logger);
        _smsClient = smsClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc />
    public async Task<(bool Succeeded, string? FailReason)> SendAsync(NotificationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        // 手机号由用户域提供，当前以占位空号模拟（实际应通过防腐层查询用户手机号）
        var phoneNumber = string.Empty;
        var result = await _smsClient.SendAsync(phoneNumber, record.Content, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning("短信发送失败 RecordId={RecordId} Reason={Reason}", record.Id, result.FailReason);
        }

        return result;
    }
}
