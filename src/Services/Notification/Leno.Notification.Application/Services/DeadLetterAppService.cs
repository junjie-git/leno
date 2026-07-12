using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 死信管理应用服务实现，提供死信列表查询、批量重发与批量丢弃功能。
/// 批量操作记录审计日志。
/// </summary>
public sealed class DeadLetterAppService : IDeadLetterAppService
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly IUserContactService _userContactService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeadLetterAppService> _logger;

    public DeadLetterAppService(
        INotificationRecordRepository recordRepository,
        IEnumerable<INotificationChannel> channels,
        IUserContactService userContactService,
        IUnitOfWork unitOfWork,
        ILogger<DeadLetterAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(userContactService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        _channels = channels;
        _userContactService = userContactService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeadLetterListResultDto> GetDeadLettersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _recordRepository.GetDeadLetteredAsync(page, pageSize, ct);
        var total = await _recordRepository.CountDeadLetteredAsync(ct);

        return new DeadLetterListResultDto
        {
            Items = items.ConvertAll(ToDeadLetterDto),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<BatchOperationResultDto> BatchResendAsync(Guid operatorId, BatchDeadLetterRequestDto request, CancellationToken ct = default)
    {
        if (request.RecordIds is null || request.RecordIds.Count == 0)
        {
            return new BatchOperationResultDto { Errors = ["记录ID列表不可为空"] };
        }

        var result = new BatchOperationResultDto();
        var channelDict = _channels.ToDictionary(c => c.Channel);

        foreach (var recordId in request.RecordIds)
        {
            try
            {
                var record = await _recordRepository.GetByIdAsync(recordId, ct);
                if (record is null)
                {
                    result.FailureCount++;
                    result.Errors.Add($"记录 {recordId} 不存在");
                    continue;
                }

                if (record.Status != NotificationStatus.DeadLettered)
                {
                    result.FailureCount++;
                    result.Errors.Add($"记录 {recordId} 非死信状态，无法重发");
                    continue;
                }

                if (!channelDict.TryGetValue(record.Channel, out var sender))
                {
                    result.FailureCount++;
                    result.Errors.Add($"记录 {recordId} 找不到渠道 {record.Channel}");
                    continue;
                }

                // 重发：重置死信状态并重新发送
                record.MarkResend();
                var sendRequest = await BuildChannelSendRequestAsync(record, ct);
                var sendResult = await sender.SendAsync(sendRequest, ct);
                if (sendResult.Succeeded)
                {
                    record.MarkSucceeded(sendResult.ChannelMessageId);
                }
                else
                {
                    record.MarkFailed(sendResult.ErrorMessage ?? "手工重发失败", sendResult.ErrorCode);
                }

                await _recordRepository.UpdateAsync(record, ct);
                result.SuccessCount++;

                _logger.LogInformation("操作员 {OperatorId} 手工重发死信 RecordId={RecordId} 结果={Succeeded}",
                    operatorId, recordId, sendResult.Succeeded);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add($"记录 {recordId} 重发异常：{ex.Message}");
                _logger.LogError(ex, "手工重发死信异常 RecordId={RecordId} OperatorId={OperatorId}", recordId, operatorId);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // 审计日志
        _logger.LogWarning("AUDIT: 操作员 {OperatorId} 批量重发死信 {Count} 条，成功 {Success} 失败 {Failure}",
            operatorId, request.RecordIds.Count, result.SuccessCount, result.FailureCount);

        return result;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResultDto> BatchDiscardAsync(Guid operatorId, BatchDeadLetterRequestDto request, CancellationToken ct = default)
    {
        if (request.RecordIds is null || request.RecordIds.Count == 0)
        {
            return new BatchOperationResultDto { Errors = ["记录ID列表不可为空"] };
        }

        if (string.IsNullOrWhiteSpace(request.DiscardReason))
        {
            return new BatchOperationResultDto { Errors = ["丢弃原因不可为空"] };
        }

        var result = new BatchOperationResultDto();

        foreach (var recordId in request.RecordIds)
        {
            try
            {
                var record = await _recordRepository.GetByIdAsync(recordId, ct);
                if (record is null)
                {
                    result.FailureCount++;
                    result.Errors.Add($"记录 {recordId} 不存在");
                    continue;
                }

                if (record.Status != NotificationStatus.DeadLettered)
                {
                    result.FailureCount++;
                    result.Errors.Add($"记录 {recordId} 非死信状态，无法丢弃");
                    continue;
                }

                // 丢弃：记录丢弃原因
                record.MarkDiscarded(request.DiscardReason);

                await _recordRepository.UpdateAsync(record, ct);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add($"记录 {recordId} 丢弃异常：{ex.Message}");
                _logger.LogError(ex, "批量丢弃死信异常 RecordId={RecordId} OperatorId={OperatorId}", recordId, operatorId);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // 审计日志
        _logger.LogWarning("AUDIT: 操作员 {OperatorId} 批量丢弃死信 {Count} 条，原因：{Reason}，成功 {Success} 失败 {Failure}",
            operatorId, request.RecordIds.Count, request.DiscardReason, result.SuccessCount, result.FailureCount);

        return result;
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

    private static DeadLetterRecordDto ToDeadLetterDto(NotificationRecord record)
    {
        return new DeadLetterRecordDto
        {
            RecordId = record.Id,
            UserId = record.UserId,
            TemplateCode = record.TemplateCode,
            Channel = record.Channel,
            Title = record.Title,
            Content = record.Content,
            Status = record.Status,
            RetryCount = record.RetryCount,
            ErrorMessage = record.ErrorMessage,
            ErrorCode = record.ErrorCode,
            FailedAt = record.FailedAt,
            CreatedAt = record.CreatedAt
        };
    }
}