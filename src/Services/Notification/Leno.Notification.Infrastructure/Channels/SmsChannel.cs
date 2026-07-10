using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels.Sms;
using Leno.Notification.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 短信发送渠道，通过 <see cref="SmsClient"/> 调用短信服务商 API。
/// 当前为模拟实现，实际部署需配置服务商凭证。
/// </summary>
public sealed class SmsChannel : IChannel
{
    private readonly SmsClient _smsClient;
    private readonly UserContactAntiCorruptionService _userContactService;
    private readonly ILogger<SmsChannel> _logger;

    public SmsChannel(SmsClient smsClient, UserContactAntiCorruptionService userContactService, ILogger<SmsChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(smsClient);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(logger);
        _smsClient = smsClient;
        _userContactService = userContactService;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc />
    public async Task<(bool Succeeded, string? FailReason)> SendAsync(NotificationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var contacts = await _userContactService.GetContactsAsync(record.UserId, ct);
        var phoneNumber = contacts?.PhoneNumber ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning("用户手机号为空，跳过短信发送 UserId={UserId}", record.UserId);
            return (false, "用户手机号为空");
        }
        var result = await _smsClient.SendAsync(phoneNumber, record.Content, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning("短信发送失败 RecordId={RecordId} Reason={Reason}", record.Id, result.FailReason);
        }

        return result;
    }
}
