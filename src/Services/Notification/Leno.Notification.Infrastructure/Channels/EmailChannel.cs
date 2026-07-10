using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels.Email;
using Leno.Notification.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 邮件发送渠道，通过 <see cref="SmtpClientWrapper"/> 发送 HTML 邮件。
/// 当前为模拟实现，实际部署需配置 SMTP 凭证。
/// </summary>
public sealed class EmailChannel : IChannel
{
    private readonly SmtpClientWrapper _smtpClient;
    private readonly UserContactAntiCorruptionService _userContactService;
    private readonly ILogger<EmailChannel> _logger;

    public EmailChannel(SmtpClientWrapper smtpClient, UserContactAntiCorruptionService userContactService, ILogger<EmailChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(smtpClient);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(logger);
        _smtpClient = smtpClient;
        _userContactService = userContactService;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc />
    public async Task<(bool Succeeded, string? FailReason)> SendAsync(NotificationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var contacts = await _userContactService.GetContactsAsync(record.UserId, ct);
        var toAddress = contacts?.Email ?? string.Empty;
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            _logger.LogWarning("用户邮箱为空，跳过邮件发送 UserId={UserId}", record.UserId);
            return (false, "用户邮箱为空");
        }
        var result = await _smtpClient.SendAsync(toAddress, record.Title, record.Content, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning("邮件发送失败 RecordId={RecordId} Reason={Reason}", record.Id, result.FailReason);
        }

        return result;
    }
}
