using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Leno.Notification.Infrastructure.Channels.Email;

/// <summary>
/// SMTP 客户端封装，基于 MailKit 异步发送邮件。
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
    /// </summary>
    public async Task<(bool Succeeded, string? FailReason)> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return (false, "收件地址为空");
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning("邮件渠道未配置 SmtpHost");
            return (false, "邮件渠道未配置");
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            var port = _options.Port > 0 ? _options.Port : 587;
            var secureSocketOptions = _options.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;
            await client.ConnectAsync(_options.SmtpHost, port, secureSocketOptions, ct);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("邮件已发送 To={To} Subject={Subject}", toAddress, subject);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败 To={To} Subject={Subject}", toAddress, subject);
            return (false, ex.Message);
        }
    }
}
