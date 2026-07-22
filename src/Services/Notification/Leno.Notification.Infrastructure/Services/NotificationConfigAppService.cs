using Leno.Infrastructure.Configuration;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 通知渠道配置管理应用服务实现。
///
/// 敏感参数加密存储，显示时脱敏为 ******。
/// 配置变更后通过 <see cref="ConsulReloadableConfigurationProvider"/> 触发 IOptionsMonitor 热重载：
/// 进行中的发送使用旧实例，新发送使用新实例。
/// </summary>
public sealed class NotificationConfigAppService : INotificationConfigAppService
{
    private const string MaskedValue = "******";

    private static readonly IReadOnlyDictionary<NotificationChannel, string> ChannelConfigPrefix = new Dictionary<NotificationChannel, string>
    {
        [NotificationChannel.Email] = "Notification:Email",
        [NotificationChannel.Sms] = "Notification:Sms"
    };

    private readonly IOptionsMonitor<EmailChannelOptions> _emailOptions;
    private readonly IOptionsMonitor<SmsChannelOptions> _smsOptions;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly INotificationConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ConsulReloadableConfigurationProvider _configReloadProvider;
    private readonly ILogger<NotificationConfigAppService> _logger;

    public NotificationConfigAppService(
        IOptionsMonitor<EmailChannelOptions> emailOptions,
        IOptionsMonitor<SmsChannelOptions> smsOptions,
        IEnumerable<INotificationChannel> channels,
        INotificationConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        ConsulReloadableConfigurationProvider configReloadProvider,
        ILogger<NotificationConfigAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(emailOptions);
        ArgumentNullException.ThrowIfNull(smsOptions);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(configRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(configReloadProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _emailOptions = emailOptions;
        _smsOptions = smsOptions;
        _channels = channels;
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _configReloadProvider = configReloadProvider;
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
    public async Task UpdateConfigAsync(Guid operatorId, NotificationChannel channel, SaveNotificationConfigDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // 审计日志：记录配置变更
        _logger.LogWarning("AUDIT: 操作员 {OperatorId} 更新了渠道 {Channel} 的配置", operatorId, channel);

        // 收集需要持久化 + 热重载的配置项 (ConfigKey, ConfigValue, IsSensitive)
        var changes = BuildChangeSet(channel, dto);

        // 记录变更详情（敏感字段脱敏）
        if (changes.Count > 0)
        {
            var maskedSummary = string.Join(", ", changes.Select(c => $"{c.ConfigKey}={(c.IsSensitive ? MaskedValue : c.ConfigValue)}"));
            _logger.LogWarning("AUDIT: 配置变更详情 Channel={Channel} Changes={Changes}", channel, maskedSummary);
        }
        else
        {
            _logger.LogWarning("AUDIT: 配置变更详情 Channel={Channel} Changes=（无字段更新）", channel);
        }

        // 持久化到数据库（按 Channel + ConfigKey 业务唯一键 upsert）
        foreach (var change in changes)
        {
            var existing = await _configRepository.GetAsync(channel, change.ConfigKey, ct);
            if (existing is null)
            {
                var config = NotificationConfig.Create(
                    Guid.NewGuid(),
                    channel,
                    change.ConfigKey,
                    change.ConfigValue,
                    description: null,
                    isSensitive: change.IsSensitive);
                await _configRepository.AddAsync(config, ct);
            }
            else
            {
                existing.UpdateValue(change.ConfigValue);
                await _configRepository.UpdateAsync(existing, ct);
            }
        }

        // 同一事务内保存聚合变更与领域事件
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 触发 IOptionsMonitor 热重载：将变更值写入 ConsulReloadableConfigurationProvider
        // 使 EmailChannelOptions / SmsChannelOptions 的 IOptionsMonitor 监听到 ReloadToken 变化并重绑。
        // Enabled 字段不属于 IOptionsMonitor 绑定路径，仅持久化用于运营端展示，跳过热重载。
        if (ChannelConfigPrefix.TryGetValue(channel, out var configPrefix))
        {
            foreach (var change in changes)
            {
                if (string.Equals(change.ConfigKey, "Enabled", StringComparison.Ordinal))
                {
                    continue;
                }

                _configReloadProvider.SetValue($"{configPrefix}:{change.ConfigKey}", change.ConfigValue);
            }
        }

        _logger.LogInformation("配置已持久化并触发热重载 Channel={Channel} OperatorId={OperatorId}", channel, operatorId);
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

    /// <summary>
    /// 根据渠道与 DTO 字段构建变更集，仅纳入 DTO 中显式赋值的字段。
    /// </summary>
    private static List<ConfigChange> BuildChangeSet(NotificationChannel channel, SaveNotificationConfigDto dto)
    {
        var changes = new List<ConfigChange>();

        if (dto.Enabled.HasValue)
        {
            changes.Add(new ConfigChange("Enabled", dto.Enabled.Value.ToString(), IsSensitive: false));
        }

        switch (channel)
        {
            case NotificationChannel.Email:
                if (dto.SmtpHost is not null)
                {
                    changes.Add(new ConfigChange("Host", dto.SmtpHost, IsSensitive: false));
                }
                if (dto.SmtpPort.HasValue)
                {
                    changes.Add(new ConfigChange("Port", dto.SmtpPort.Value.ToString(), IsSensitive: false));
                }
                if (dto.SmtpUsername is not null)
                {
                    changes.Add(new ConfigChange("Username", dto.SmtpUsername, IsSensitive: false));
                }
                if (dto.SmtpPassword is not null)
                {
                    changes.Add(new ConfigChange("Password", dto.SmtpPassword, IsSensitive: true));
                }
                if (dto.FromAddress is not null)
                {
                    changes.Add(new ConfigChange("From", dto.FromAddress, IsSensitive: false));
                }
                if (dto.UseSsl.HasValue)
                {
                    changes.Add(new ConfigChange("UseSsl", dto.UseSsl.Value.ToString(), IsSensitive: false));
                }
                break;

            case NotificationChannel.Sms:
                if (dto.SmsProvider is not null)
                {
                    changes.Add(new ConfigChange("Provider", dto.SmsProvider, IsSensitive: false));
                }
                if (dto.AccessKeyId is not null)
                {
                    changes.Add(new ConfigChange("AccessKeyId", dto.AccessKeyId, IsSensitive: false));
                }
                if (dto.AccessKeySecret is not null)
                {
                    changes.Add(new ConfigChange("AccessKeySecret", dto.AccessKeySecret, IsSensitive: true));
                }
                if (dto.SmsSignName is not null)
                {
                    changes.Add(new ConfigChange("SignName", dto.SmsSignName, IsSensitive: false));
                }
                break;
        }

        return changes;
    }

    private sealed record ConfigChange(string ConfigKey, string ConfigValue, bool IsSensitive);
}
