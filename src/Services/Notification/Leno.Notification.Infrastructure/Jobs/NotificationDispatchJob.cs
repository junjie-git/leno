using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Jobs;

/// <summary>
/// 通知发送调度任务，轮询待发送通知并按渠道分发。
/// 可由 HostedService 或外部调度器定时触发。
/// </summary>
public sealed class NotificationDispatchJob
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationChannel> _channelDict;
    private readonly IUserContactService _userContactService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationDispatchJob> _logger;

    public NotificationDispatchJob(
        INotificationRecordRepository recordRepository,
        IEnumerable<INotificationChannel> channels,
        IUserContactService userContactService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationDispatchJob> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        // 构造时一次性构建渠道字典并缓存，避免每次 ExecuteAsync 重建触发 ToDictionary 重复键异常。
        _channelDict = channels.ToDictionary(c => c.Channel);
        _userContactService = userContactService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// 执行一轮调度，处理最多 <paramref name="batchSize"/> 条待发送通知。
    /// </summary>
    public async Task ExecuteAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var pending = await _recordRepository.GetPendingAsync(batchSize, ct);
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var record in pending)
        {
            if (_channelDict.TryGetValue(record.Channel, out var sender))
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
                    _logger.LogError(ex, "调度发送异常 RecordId={RecordId}", record.Id);
                    record.MarkFailed(ex.Message, "DISPATCH_EXCEPTION");
                }

                await _recordRepository.UpdateAsync(record, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("通知调度完成 处理 {Count} 条", pending.Count);
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