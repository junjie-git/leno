using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Channels.Email;

/// <summary>
/// SMTP 客户端封装（模拟实现）。
/// 实际实现应使用 MailKit 或 System.Net.Mail 异步发送邮件，当前为模拟桩。
/// </summary>
public sealed class SmtpClientWrapper
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpClientWrapper> _logger;

    public SmtpClientWrapper(IOptions<EmailOptions> options, ILogger<SmtpClientWrapper> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 发送邮件，返回是否成功。
    /// 当前为模拟实现，配置了 SmtpHost 则返回成功，否则返回失败。
    /// </summary>
    public Task<(bool Succeeded, string? FailReason)> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return Task.FromResult<(bool Succeeded, string? FailReason)>((false, "收件地址为空"));
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning("邮件渠道未配置 SmtpHost，模拟发送失败 To={To}", toAddress);
            return Task.FromResult<(bool Succeeded, string? FailReason)>((false, "邮件渠道未配置"));
        }

        _logger.LogInformation("邮件已发送（模拟）To={To} Subject={Subject}", toAddress, subject);
        return Task.FromResult<(bool Succeeded, string? FailReason)>((true, null));
    }
}
