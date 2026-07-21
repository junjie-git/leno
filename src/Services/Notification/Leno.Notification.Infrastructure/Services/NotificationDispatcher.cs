using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 通知调度器实现，编排模板查询、偏好查询、渲染、渠道选择与发送。
/// 消费者调用 <see cref="DispatchAsync"/> 完成通知全流程。
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly INotificationRecordRepository _recordRepository;
    private readonly ITemplateRenderer _renderer;
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationChannel> _channelDict;
    private readonly IUserContactService _userContactService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationTemplateRepository templateRepository,
        INotificationPreferenceRepository preferenceRepository,
        INotificationRecordRepository recordRepository,
        ITemplateRenderer renderer,
        IEnumerable<INotificationChannel> channels,
        IUserContactService userContactService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);
        ArgumentNullException.ThrowIfNull(preferenceRepository);
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _templateRepository = templateRepository;
        _preferenceRepository = preferenceRepository;
        _recordRepository = recordRepository;
        _renderer = renderer;
        // 构造时一次性构建渠道字典并缓存，避免每次 DispatchAsync 重建触发 ToDictionary 重复键异常。
        // P0-1 修复后：SmsChannel 外壳作为唯一的 Sms INotificationChannel 注册，Channel 值不再重复。
        _channelDict = channels.ToDictionary(c => c.Channel);
        _userContactService = userContactService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(Guid userId, string templateCode, Guid? eventId, Dictionary<string, string> variables, CancellationToken ct = default)
    {
        // 幂等：同一事件已产生通知记录则跳过
        if (eventId.HasValue && await _recordRepository.ExistsByEventIdAsync(eventId.Value, ct))
        {
            _logger.LogInformation("通知已发送，跳过重复调度 EventId={EventId} TemplateCode={TemplateCode}", eventId, templateCode);
            return;
        }

        // 查询用户偏好
        var preference = await _preferenceRepository.GetByUserIdAsync(userId, ct);
        var channels = preference is not null && preference.Status == PreferenceStatus.Active
            ? preference.GetChannels(templateCode)
            : [NotificationChannel.InApp];

        // 按渠道创建通知记录并发送（使用构造时缓存的渠道字典，避免每次重建触发 ToDictionary 重复键异常）
        foreach (var channel in channels)
        {
            var template = await _templateRepository.GetEnabledAsync(templateCode, channel, ct);
            if (template is null)
            {
                _logger.LogWarning("未找到启用模板 TemplateCode={TemplateCode} Channel={Channel}，跳过", templateCode, channel);
                continue;
            }

            var (title, content) = _renderer.Render(template, variables);
            var record = NotificationRecord.Create(
                Guid.NewGuid(), userId, templateCode, eventId, channel, title, content);

            await _recordRepository.AddAsync(record, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // 立即发送
            if (_channelDict.TryGetValue(channel, out var sender))
            {
                try
                {
                    record.MarkSending();
                    var sendRequest = await BuildChannelSendRequestAsync(record, ct);
                    var result = await sender.SendAsync(sendRequest, ct);
                    if (result.Succeeded)
                    {
                        record.MarkSucceeded(result.ChannelMessageId);
                    }
                    else
                    {
                        record.MarkFailed(result.ErrorMessage ?? "发送失败", result.ErrorCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "通知发送异常 RecordId={RecordId} Channel={Channel}", record.Id, channel);
                    record.MarkFailed(ex.Message, "DISPATCH_EXCEPTION");
                }

                await _recordRepository.UpdateAsync(record, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("通知调度完成 UserId={UserId} TemplateCode={TemplateCode} Channels={Channels}", userId, templateCode, string.Join(",", channels));
    }

    private async Task<ChannelSendRequest> BuildChannelSendRequestAsync(NotificationRecord record, CancellationToken ct)
    {
        var contacts = await _userContactService.GetContactsAsync(record.UserId, ct);
        var recipient = Recipient.Create(
            record.UserId,
            contacts?.Email,
            contacts?.PhoneNumber);

        return new ChannelSendRequest(
            record.Channel,
            recipient,
            record.Title,
            record.Content,
            record.IdempotencyKey ?? string.Empty);
    }
}