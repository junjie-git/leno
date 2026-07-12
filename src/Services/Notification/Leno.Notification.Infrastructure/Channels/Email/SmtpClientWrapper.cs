using MailKit.Net.Smtp;
using MailKit.Security;
using Leno.Notification.Domain.ValueObjects;
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
    /// 发送邮件，返回发送结果。
    /// </summary>
    public async Task<ChannelSendResult> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return new ChannelSendResult(false, "收件地址为空", "EMAIL_EMPTY", null);
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning("邮件渠道未配置 SmtpHost");
            return new ChannelSendResult(false, "邮件渠道未配置", "EMAIL_CONFIG_MISSING", null);
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

            var messageId = await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("邮件已发送 To={To} Subject={Subject}", toAddress, subject);
            return new ChannelSendResult(true, null, null, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败 To={To} Subject={Subject}", toAddress, subject);
            return new ChannelSendResult(false, ex.Message, "EMAIL_EXCEPTION", null);
        }
    }
}
