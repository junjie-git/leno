using Leno.Notification.Domain.Repositories;
using Leno.Notification.Infrastructure.Channels;
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
    private readonly IEnumerable<IChannel> _channels;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationDispatchJob> _logger;

    public NotificationDispatchJob(
        INotificationRecordRepository recordRepository,
        IEnumerable<IChannel> channels,
        IUnitOfWork unitOfWork,
        ILogger<NotificationDispatchJob> logger)
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
    /// 执行一轮调度，处理最多 <paramref name="batchSize"/> 条待发送通知。
    /// </summary>
    public async Task ExecuteAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var pending = await _recordRepository.GetPendingAsync(batchSize, ct);
        if (pending.Count == 0)
        {
            return;
        }

        var channelDict = _channels.ToDictionary(c => c.Channel);
        foreach (var record in pending)
        {
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
                        record.MarkFailed(result.FailReason ?? "发送失败");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "调度发送异常 RecordId={RecordId}", record.Id);
                    record.MarkFailed(ex.Message);
                }

                await _recordRepository.UpdateAsync(record, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("通知调度完成 处理 {Count} 条", pending.Count);
    }
}
