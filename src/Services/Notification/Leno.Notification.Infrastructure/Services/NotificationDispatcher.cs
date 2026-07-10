using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Channels;
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
    private readonly IEnumerable<IChannel> _channels;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationTemplateRepository templateRepository,
        INotificationPreferenceRepository preferenceRepository,
        INotificationRecordRepository recordRepository,
        ITemplateRenderer renderer,
        IEnumerable<IChannel> channels,
        IUnitOfWork unitOfWork,
        ILogger<NotificationDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(templateRepository);
        ArgumentNullException.ThrowIfNull(preferenceRepository);
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _templateRepository = templateRepository;
        _preferenceRepository = preferenceRepository;
        _recordRepository = recordRepository;
        _renderer = renderer;
        _channels = channels;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(Guid userId, string eventType, Guid? eventId, Dictionary<string, string> variables, CancellationToken ct = default)
    {
        // 幂等：同一事件已产生通知记录则跳过
        if (eventId.HasValue && await _recordRepository.ExistsByEventIdAsync(eventId.Value, ct))
        {
            _logger.LogInformation("通知已发送，跳过重复调度 EventId={EventId} EventType={EventType}", eventId, eventType);
            return;
        }

        // 查询用户偏好
        var preference = await _preferenceRepository.GetByUserIdAsync(userId, ct);
        var channels = preference is not null && preference.Status == PreferenceStatus.Active
            ? preference.GetChannels(eventType)
            : [NotificationChannel.InApp];

        // 按渠道创建通知记录并发送
        var channelDict = _channels.ToDictionary(c => c.Channel);
        foreach (var channel in channels)
        {
            var template = await _templateRepository.GetEnabledAsync(eventType, channel, ct);
            if (template is null)
            {
                _logger.LogWarning("未找到启用模板 EventType={EventType} Channel={Channel}，跳过", eventType, channel);
                continue;
            }

            var (title, content) = _renderer.Render(template, variables);
            var record = NotificationRecord.Create(
                Guid.NewGuid(), userId, eventType, eventId, channel, title, content);

            await _recordRepository.AddAsync(record, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // 立即发送
            if (channelDict.TryGetValue(channel, out var sender))
            {
                try
                {
                    var result = await sender.SendAsync(record, ct);
                    if (result.Succeeded)
                    {
                        record.MarkSent();
                    }
                    else
                    {
                        record.MarkFailed(result.FailReason ?? "发送失败");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "通知发送异常 RecordId={RecordId} Channel={Channel}", record.Id, channel);
                    record.MarkFailed(ex.Message);
                }

                await _recordRepository.UpdateAsync(record, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("通知调度完成 UserId={UserId} EventType={EventType} Channels={Channels}", userId, eventType, string.Join(",", channels));
    }
}
