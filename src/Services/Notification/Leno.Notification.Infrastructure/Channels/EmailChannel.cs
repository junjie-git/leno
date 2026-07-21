using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// SMTP 邮件发送渠道，通过 MailKit 发送 HTML 邮件。
/// </summary>
public sealed class SmtpEmailChannel : INotificationChannel
{
    private static readonly TimeSpan SmtpTimeout = TimeSpan.FromSeconds(10);

    private readonly EmailChannelOptions _options;
    private readonly ILogger<SmtpEmailChannel> _logger;

    public SmtpEmailChannel(IOptions<EmailChannelOptions> options, ILogger<SmtpEmailChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc />
    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var toAddress = request.Recipient.Email;
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            _logger.LogWarning("用户邮箱为空，跳过邮件发送 UserId={UserId}", request.Recipient.UserId);
            return new ChannelSendResult(false, "用户邮箱为空", "EMAIL_EMPTY", null);
        }

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogWarning("邮件渠道未配置 Host");
            return new ChannelSendResult(false, "邮件渠道未配置", "EMAIL_CONFIG_MISSING", null);
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_options.From));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = request.Subject;
            message.Body = new TextPart("html") { Text = request.Body };

            using var client = new SmtpClient();
            using var timeoutCts = new CancellationTokenSource(SmtpTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var port = _options.Port > 0 ? _options.Port : 587;
            var secureSocketOptions = _options.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            try
            {
                await client.ConnectAsync(_options.Host, port, secureSocketOptions, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("SMTP 连接超时 Host={Host} Port={Port}", _options.Host, port);
                return new ChannelSendResult(false, $"SMTP 连接超时 ({_options.Host}:{port})", "SMTP_CONNECT_TIMEOUT", null);
            }

            if (!string.IsNullOrEmpty(_options.Username))
            {
                // P1-29：将 AuthenticateAsync 包入 try-catch，认证超时映射为 SMTP_AUTH_TIMEOUT 而非 EMAIL_EXCEPTION。
                try
                {
                    await client.AuthenticateAsync(_options.Username, _options.Password, linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    _logger.LogWarning("SMTP 认证超时 Host={Host} Port={Port}", _options.Host, port);
                    return new ChannelSendResult(false, "SMTP 认证超时", "SMTP_AUTH_TIMEOUT", null);
                }
            }

            var messageId = await client.SendAsync(message, linkedCts.Token);
            await client.DisconnectAsync(true, CancellationToken.None);

            _logger.LogInformation("邮件已发送 To={To} Subject={Subject}", toAddress, request.Subject);
            return new ChannelSendResult(true, null, null, messageId);
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode == SmtpErrorCode.RecipientNotAccepted
            || ex.StatusCode == SmtpStatusCode.MailboxUnavailable
            || ex.StatusCode == SmtpStatusCode.MailboxNameNotAllowed)
        {
            // 550 - non-retryable
            _logger.LogWarning(ex, "邮件被拒绝（不可重试） To={To} StatusCode={StatusCode}", toAddress, ex.StatusCode);
            return new ChannelSendResult(false, ex.Message, "SMTP_NON_RETRYABLE", null);
        }
        catch (SmtpCommandException ex) when (ex.StatusCode == SmtpStatusCode.ServiceClosingTransmissionChannel
            || ex.StatusCode == SmtpStatusCode.MailboxBusy
            || ex.StatusCode == SmtpStatusCode.InsufficientStorage)
        {
            // 421, 450, 452 - retryable
            _logger.LogWarning(ex, "邮件发送临时失败（可重试） To={To} StatusCode={StatusCode}", toAddress, ex.StatusCode);
            return new ChannelSendResult(false, ex.Message, "SMTP_RETRYABLE", null);
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogError(ex, "邮件发送失败 To={To} StatusCode={StatusCode}", toAddress, ex.StatusCode);
            return new ChannelSendResult(false, ex.Message, "SMTP_EXCEPTION", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送异常 To={To} Subject={Subject}", toAddress, request.Subject);
            return new ChannelSendResult(false, ex.Message, "EMAIL_EXCEPTION", null);
        }
    }
}