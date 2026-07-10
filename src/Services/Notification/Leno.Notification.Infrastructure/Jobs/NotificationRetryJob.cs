using Leno.Notification.Domain.Repositories;
using Leno.Notification.Infrastructure.Channels;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Jobs;

/// <summary>
/// 通知重试任务，轮询失败通知，按重试次数决定重试或放弃。
/// </summary>
public sealed class NotificationRetryJob
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IEnumerable<IChannel> _channels;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationRetryJob> _logger;

    public NotificationRetryJob(
        INotificationRecordRepository recordRepository,
        IEnumerable<IChannel> channels,
        IUnitOfWork unitOfWork,
        ILogger<NotificationRetryJob> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        _channels = channels;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// 执行一轮重试，处理最多 <paramref name="batchSize"/> 条可重试通知。
    /// </summary>
    public async Task ExecuteAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var retryable = await _recordRepository.GetRetryableAsync(batchSize, ct);
        if (retryable.Count == 0)
        {
            return;
        }

        var channelDict = _channels.ToDictionary(c => c.Channel);
        foreach (var record in retryable)
        {
            record.ResetForRetry();

            if (channelDict.TryGetValue(record.Channel, out var sender))
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
                        record.MarkFailed(result.FailReason ?? "重试发送失败");
                        if (!record.CanRetry)
                        {
                            record.MarkAbandoned();
                            _logger.LogWarning("通知超过最大重试次数，已放弃 RecordId={RecordId} RetryCount={RetryCount}", record.Id, record.RetryCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "重试发送异常 RecordId={RecordId}", record.Id);
                    record.MarkFailed(ex.Message);
                }

                await _recordRepository.UpdateAsync(record, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("通知重试完成 处理 {Count} 条", retryable.Count);
    }
}
