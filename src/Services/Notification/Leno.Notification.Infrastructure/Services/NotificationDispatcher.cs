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

        // 任务 2.2.2：合并 SaveChanges，将 SaveChanges 次数从 2N（N=渠道数）降为 2（创建记录 + 状态更新）。
        // 原实现每个 channel 内执行 2 次 SaveChanges（创建记录 1 次 + 状态更新 1 次），
        // N 个渠道共 2N 次网络往返；合并后所有渠道记录创建合并为 1 次 SaveChanges，
        // 所有状态更新合并为 1 次 SaveChanges，且保证事务一致性（同一事务内全部提交或全部回滚）。
        //
        // 阶段 1：创建所有渠道的 NotificationRecord 并 Add 到 DbContext（不 SaveChanges）。
        // P1-26：每个 channel 的创建放入独立 try-catch，单渠道失败（模板查询/渲染/记录创建）不影响其他渠道。
        var records = new List<NotificationRecord>(channels.Count);
        foreach (var channel in channels)
        {
            try
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
                records.Add(record);
            }
            catch (Exception ex)
            {
                // P1-26：单渠道失败（模板查询/渲染/记录创建等）不影响其他渠道发送，
                // DispatchAsync 不抛异常，确保 Email 失败时 Sms 仍正常发送。
                _logger.LogError(ex, "渠道处理异常，跳过该渠道 UserId={UserId} TemplateCode={TemplateCode} Channel={Channel}",
                    userId, templateCode, channel);
            }
        }

        // 阶段 2：单次 SaveChanges 提交所有渠道的记录创建（N 次 → 1 次）。
        // 所有记录在同一事务内提交，要么全部成功要么全部回滚，保持事务一致性。
        if (records.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // 阶段 3：逐个发送（不涉及 SaveChanges，仅投递到渠道网关）。
        // 使用构造时缓存的渠道字典，避免每次重建触发 ToDictionary 重复键异常。
        // 单渠道发送失败不影响其他渠道，记录 MarkFailed 后续由阶段 4 统一持久化。
        foreach (var record in records)
        {
            try
            {
                if (!_channelDict.TryGetValue(record.Channel, out var sender))
                {
                    _logger.LogWarning("未找到渠道发送器 Channel={Channel}，跳过发送 RecordId={RecordId}",
                        record.Channel, record.Id);
                    continue;
                }

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
                _logger.LogError(ex, "通知发送异常 RecordId={RecordId} Channel={Channel}", record.Id, record.Channel);
                // 状态机约束：MarkFailed 仅在 Sending 状态可调用。
                // MarkSending 成功后状态为 Sending，可安全 MarkFailed；
                // 若 MarkSending 前抛异常（如字典查找失败），状态仍为 Pending，跳过 MarkFailed 避免二次抛出。
                if (record.Status == NotificationStatus.Sending)
                {
                    record.MarkFailed(ex.Message, "DISPATCH_EXCEPTION");
                }
            }
        }

        // 阶段 4：单次 SaveChanges 提交所有渠道的状态更新（N 次 → 1 次）。
        // EF Core 跟踪实体自动检测变更，无需显式 UpdateAsync。
        // 所有状态更新在同一事务内提交，保持事务一致性。
        if (records.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
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