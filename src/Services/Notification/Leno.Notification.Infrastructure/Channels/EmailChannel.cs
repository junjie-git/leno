using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels.Email;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 邮件发送渠道，通过 <see cref="SmtpClientWrapper"/> 发送 HTML 邮件。
/// 当前为模拟实现，实际部署需配置 SMTP 凭证。
/// </summary>
public sealed class EmailChannel : IChannel
{
    private readonly SmtpClientWrapper _smtpClient;
    private readonly ILogger<EmailChannel> _logger;

    public EmailChannel(SmtpClientWrapper smtpClient, ILogger<EmailChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(smtpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _smtpClient = smtpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc />
    public async Task<(bool Succeeded, string? FailReason)> SendAsync(NotificationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        // 收件地址由用户域提供，当前以占位空地址模拟（实际应通过防腐层查询用户邮箱）
        var toAddress = string.Empty;
        var result = await _smtpClient.SendAsync(toAddress, record.Title, record.Content, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning("邮件发送失败 RecordId={RecordId} Reason={Reason}", record.Id, result.FailReason);
        }

        return result;
    }
}
