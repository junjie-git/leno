using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Jobs;

/// <summary>
/// 通知重试任务，分两阶段处理：
/// 1. 扫描 Failed 状态且 CanRetry 的记录 → 根据错误码分类，不可重试直接死信，可重试安排退避重试
/// 2. 扫描 Retried 状态且 NextRetryAt 已到期的记录 → 执行实际重试发送
/// </summary>
public sealed class NotificationRetryJob
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationChannel> _channelDict;
    private readonly IUserContactService _userContactService;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationRetryJob> _logger;

    public NotificationRetryJob(
        INotificationRecordRepository recordRepository,
        IEnumerable<INotificationChannel> channels,
        IUserContactService userContactService,
        IRetryPolicy retryPolicy,
        IUnitOfWork unitOfWork,
        ILogger<NotificationRetryJob> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        // 构造时一次性构建渠道字典并缓存，避免每次 ExecuteAsync 重建触发 ToDictionary 重复键异常。
        _channelDict = channels.ToDictionary(c => c.Channel);
        _userContactService = userContactService;
        _retryPolicy = retryPolicy;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// 执行一轮重试，处理最多 <paramref name="batchSize"/> 条记录。
    /// </summary>
    public async Task ExecuteAsync(int batchSize = 50, CancellationToken ct = default)
    {
        // 阶段 1：处理 Failed 状态的新失败记录（分类 + 安排重试）
        await ProcessFailedRecordsAsync(batchSize, ct);

        // 阶段 2：处理 Retried 状态且 NextRetryAt 已到期的记录（执行实际重试）
        await ProcessScheduledRetriesAsync(batchSize, ct);
    }

    /// <summary>
    /// 阶段 1：扫描 Failed 记录，根据错误分类决定安排重试或直接移入死信。
    /// </summary>
    private async Task ProcessFailedRecordsAsync(int batchSize, CancellationToken ct)
    {
        var failedRecords = await _recordRepository.GetRetryableAsync(batchSize, ct);
        if (failedRecords.Count == 0)
        {
            return;
        }

        foreach (var record in failedRecords)
        {
            // 错误分类：不可重试 → 直接死信
            if (!_retryPolicy.ShouldRetry(record.ErrorCode))
            {
                record.ScheduleRetry(); // 必须先进入 Retried 才能 MoveToDeadLetter
                record.MoveToDeadLetter($"不可重试错误：{record.ErrorCode} - {record.ErrorMessage}");
                _logger.LogWarning("通知不可重试，直接移入死信 RecordId={RecordId} ErrorCode={ErrorCode}",
                    record.Id, record.ErrorCode);
                await _recordRepository.UpdateAsync(record, ct);
                continue;
            }

            // 可重试：安排指数退避
            var delay = _retryPolicy.NextDelay(record.RetryCount);
            var nextRetryAt = DateTime.UtcNow.Add(delay);
            record.ScheduleRetry(nextRetryAt);
            _logger.LogInformation("通知已安排重试 RecordId={RecordId} RetryCount={RetryCount} NextRetryAt={NextRetryAt} Delay={Delay}",
                record.Id, record.RetryCount, nextRetryAt, delay);

            await _recordRepository.UpdateAsync(record, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("失败记录分类完成 处理 {Count} 条", failedRecords.Count);
    }

    /// <summary>
    /// 阶段 2：扫描 Retried 且 NextRetryAt 已到期的记录，执行实际重试发送。
    /// </summary>
    private async Task ProcessScheduledRetriesAsync(int batchSize, CancellationToken ct)
    {
        var scheduledRecords = await _recordRepository.GetRetriedWithExpiredNextRetryAsync(batchSize, ct);
        if (scheduledRecords.Count == 0)
        {
            return;
        }

        foreach (var record in scheduledRecords)
        {
            try
            {
                if (!_channelDict.TryGetValue(record.Channel, out var sender))
                {
                    _logger.LogWarning("重试记录找不到渠道实现 RecordId={RecordId} Channel={Channel}", record.Id, record.Channel);
                    continue;
                }

                // P1-24：MarkSending 移入 try 块，状态机异常（如并发改状态）不中断整批。
                // MarkSending 从 Retried → Sending
                record.MarkSending();

                var sendRequest = await BuildChannelSendRequestAsync(record, ct);
                var result = await sender.SendAsync(sendRequest, ct);
                if (result.Succeeded)
                {
                    record.MarkSucceeded(result.ChannelMessageId);
                    _logger.LogInformation("重试发送成功 RecordId={RecordId}", record.Id);
                }
                else
                {
                    record.MarkFailed(result.ErrorMessage ?? "重试发送失败", result.ErrorCode);

                    // 检查是否还能继续重试
                    if (!record.CanRetry)
                    {
                        record.ScheduleRetry();
                        record.MoveToDeadLetter($"超过最大重试次数 {record.RetryCount}/{record.MaxRetry}");
                        _logger.LogWarning("通知超过最大重试次数，已移入死信 RecordId={RecordId} RetryCount={RetryCount}",
                            record.Id, record.RetryCount);
                    }
                    else
                    {
                        // 仍然可重试 → 继续安排下一次重试
                        var delay = _retryPolicy.NextDelay(record.RetryCount);
                        var nextRetryAt = DateTime.UtcNow.Add(delay);
                        record.ScheduleRetry(nextRetryAt);
                        _logger.LogInformation("重试失败，继续安排下一次重试 RecordId={RecordId} RetryCount={RetryCount} Delay={Delay}",
                            record.Id, record.RetryCount, delay);
                    }
                }

                await _recordRepository.UpdateAsync(record, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重试发送异常 RecordId={RecordId}", record.Id);

                // P1-24：仅当已进入 Sending 状态（MarkSending 已执行）才尝试标记失败并安排下一次重试。
                // 若 MarkSending 本身抛出（状态机异常），记录保持原状态（Retried），跳过本条继续下一条，
                // 下次 Job 执行时会重新拾取（GetRetriedWithExpiredNextRetryAsync 仍可命中）。
                if (record.Status == NotificationStatus.Sending)
                {
                    try
                    {
                        record.MarkFailed(ex.Message, "RETRY_EXCEPTION");

                        if (!record.CanRetry)
                        {
                            record.ScheduleRetry();
                            record.MoveToDeadLetter($"超过最大重试次数 {record.RetryCount}/{record.MaxRetry}（异常）");
                        }
                        else
                        {
                            var delay = _retryPolicy.NextDelay(record.RetryCount);
                            record.ScheduleRetry(DateTime.UtcNow.Add(delay));
                        }

                        await _recordRepository.UpdateAsync(record, ct);
                    }
                    catch (Exception innerEx)
                    {
                        // 状态机二次异常时不再尝试，记录保持当前状态，等待人工介入或下次 Job 拾取
                        _logger.LogError(innerEx, "重试异常处理后状态机仍失败 RecordId={RecordId} Status={Status}",
                            record.Id, record.Status);
                    }
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("计划重试执行完成 处理 {Count} 条", scheduledRecords.Count);
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
