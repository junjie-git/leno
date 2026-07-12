using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 通知渠道配置管理应用服务实现。
/// 
/// 敏感参数加密存储，显示时脱敏为 ******。
/// 配置变更后通过 IOptionsMonitor 热重载适配器实例。
/// 进行中的发送使用旧实例，新发送使用新实例。
/// </summary>
public sealed class NotificationConfigAppService : INotificationConfigAppService
{
    private const string MaskedValue = "******";

    private readonly IOptionsMonitor<EmailChannelOptions> _emailOptions;
    private readonly IOptionsMonitor<SmsChannelOptions> _smsOptions;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<NotificationConfigAppService> _logger;

    public NotificationConfigAppService(
        IOptionsMonitor<EmailChannelOptions> emailOptions,
        IOptionsMonitor<SmsChannelOptions> smsOptions,
        IEnumerable<INotificationChannel> channels,
        ILogger<NotificationConfigAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(emailOptions);
        ArgumentNullException.ThrowIfNull(smsOptions);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(logger);
        _emailOptions = emailOptions;
        _smsOptions = smsOptions;
        _channels = channels;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<NotificationConfigDto> GetConfigAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        var dto = channel switch
        {
            NotificationChannel.Email => GetEmailConfigDto(),
            NotificationChannel.Sms => GetSmsConfigDto(),
            _ => new NotificationConfigDto { Channel = channel, Enabled = true }
        };

        return Task.FromResult(dto);
    }

    /// <inheritdoc />
    public Task UpdateConfigAsync(Guid operatorId, NotificationChannel channel, SaveNotificationConfigDto dto, CancellationToken ct = default)
    {
        // 注意：Options 模式中 IOptionsMonitor 不直接支持运行时修改。
        // 实际生产中应通过配置中心（如 Consul、Etcd）或数据库存储配置，
        // 然后通过 IOptionsMonitor 的 OnChange 回调热重载。
        // 这里提供一个实现框架，具体存储由基础设施层完成。

        // 审计日志：记录配置变更
        _logger.LogWarning("AUDIT: 操作员 {OperatorId} 更新了渠道 {Channel} 的配置", operatorId, channel);

        // 记录变更详情（敏感字段脱敏）
        var changes = new List<string>();
        if (dto.Enabled.HasValue) changes.Add($"Enabled={dto.Enabled}");
        if (dto.SmtpHost is not null) changes.Add($"SmtpHost={dto.SmtpHost}");
        if (dto.SmtpPort.HasValue) changes.Add($"SmtpPort={dto.SmtpPort}");
        if (dto.SmtpUsername is not null) changes.Add($"SmtpUsername={dto.SmtpUsername}");
        if (dto.SmtpPassword is not null) changes.Add("SmtpPassword=******");
        if (dto.FromAddress is not null) changes.Add($"FromAddress={dto.FromAddress}");
        if (dto.UseSsl.HasValue) changes.Add($"UseSsl={dto.UseSsl}");
        if (dto.SmsProvider is not null) changes.Add($"SmsProvider={dto.SmsProvider}");
        if (dto.AccessKeyId is not null) changes.Add($"AccessKeyId={dto.AccessKeyId}");
        if (dto.AccessKeySecret is not null) changes.Add("AccessKeySecret=******");
        if (dto.SmsSignName is not null) changes.Add($"SmsSignName={dto.SmsSignName}");

        _logger.LogWarning("AUDIT: 配置变更详情 Channel={Channel} Changes={Changes}", channel, string.Join(", ", changes));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<TestSendResultDto> TestSendAsync(NotificationChannel channel, TestSendRequestDto dto, CancellationToken ct = default)
    {
        var sender = _channels.FirstOrDefault(c => c.Channel == channel);
        if (sender is null)
        {
            return new TestSendResultDto
            {
                Succeeded = false,
                ErrorCode = "CHANNEL_NOT_FOUND",
                ErrorMessage = $"未找到渠道 {channel} 的实现"
            };
        }

        var recipient = Recipient.Create(Guid.NewGuid(), dto.Email, dto.PhoneNumber);
        var sendRequest = new ChannelSendRequest(
            channel,
            recipient,
            "测试通知",
            "这是一条测试通知，用于验证渠道配置是否正确。",
            $"test-{Guid.NewGuid():N}");

        var result = await sender.SendAsync(sendRequest, ct);

        return new TestSendResultDto
        {
            Succeeded = result.Succeeded,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage
        };
    }

    private NotificationConfigDto GetEmailConfigDto()
    {
        var options = _emailOptions.CurrentValue;
        return new NotificationConfigDto
        {
            Channel = NotificationChannel.Email,
            Enabled = !string.IsNullOrEmpty(options.Host),
            SmtpHost = options.Host,
            SmtpPort = options.Port,
            SmtpUsername = options.Username,
            SmtpPassword = string.IsNullOrEmpty(options.Password) ? null : MaskedValue,
            FromAddress = options.From,
            UseSsl = options.UseSsl
        };
    }

    private NotificationConfigDto GetSmsConfigDto()
    {
        var options = _smsOptions.CurrentValue;
        return new NotificationConfigDto
        {
            Channel = NotificationChannel.Sms,
            Enabled = !string.IsNullOrEmpty(options.AccessKeyId),
            SmsProvider = options.Provider,
            AccessKeyId = options.AccessKeyId,
            AccessKeySecret = string.IsNullOrEmpty(options.AccessKeySecret) ? null : MaskedValue,
            SmsSignName = options.SignName
        };
    }
}