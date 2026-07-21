using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 死信消息管理应用服务实现。
/// 重投操作调用仓储标记状态，不直接操作消息队列。
/// </summary>
public sealed class DeadLetterAppService : IDeadLetterAppService
{
    private readonly IDeadLetterMessageRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeadLetterAppService> _logger;

    public DeadLetterAppService(
        IDeadLetterMessageRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeadLetterAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeadLetterListResultDto> QueryAsync(string? sourceContext, DeadLetterStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _repository.QueryAsync(sourceContext, status, page, pageSize, ct);
        var total = await _repository.CountAsync(sourceContext, status, ct);

        return new DeadLetterListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<DeadLetterMessageDto?> GetByIdAsync(Guid messageId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(messageId, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task RetryAsync(Guid messageId, string operatorId, CancellationToken ct = default)
    {
        var entity = await RequireMessageAsync(messageId, ct);
        entity.Retry(operatorId);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("死信消息 {MessageId} 已由 {OperatorId} 重投", messageId, operatorId);
    }

    /// <inheritdoc />
    public async Task DiscardAsync(Guid messageId, string operatorId, string reason, CancellationToken ct = default)
    {
        var entity = await RequireMessageAsync(messageId, ct);
        entity.Discard(operatorId, reason);

        await _repository.UpdateAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("死信消息 {MessageId} 已由 {OperatorId} 丢弃，原因：{Reason}", messageId, operatorId, reason);
    }

    /// <inheritdoc />
    public async Task<BatchOperationResultDto> BatchRetryAsync(List<Guid> messageIds, string operatorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        var result = new BatchOperationResultDto
        {
            Errors = new List<BatchOperationErrorDto>()
        };

        // 先按 ID 批量加载所有死信消息，逐条应用 Retry 状态变更，最后一次 SaveEntitiesAsync 提交事务
        var messagesToUpdate = new List<DeadLetterMessage>();
        foreach (var messageId in messageIds)
        {
            var entity = await _repository.GetByIdAsync(messageId, ct);
            if (entity is null)
            {
                result.FailureCount++;
                result.Errors.Add(new BatchOperationErrorDto
                {
                    MessageId = messageId,
                    Error = $"死信消息 {messageId} 不存在"
                });
                continue;
            }

            try
            {
                entity.Retry(operatorId);
                messagesToUpdate.Add(entity);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add(new BatchOperationErrorDto
                {
                    MessageId = messageId,
                    Error = ex.Message
                });
            }
        }

        // 单次事务提交所有可重投的消息，避免逐条 SaveEntitiesAsync 造成中途失败状态不一致
        foreach (var entity in messagesToUpdate)
        {
            await _repository.UpdateAsync(entity, ct);
        }

        if (messagesToUpdate.Count > 0)
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        result.SuccessCount = messagesToUpdate.Count;
        _logger.LogInformation(
            "批量重投完成 OperatorId={OperatorId} Success={Success} Failure={Failure}",
            operatorId, result.SuccessCount, result.FailureCount);

        return result;
    }

    /// <inheritdoc />
    public async Task<BatchOperationResultDto> BatchDiscardAsync(List<Guid> messageIds, string operatorId, string reason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        var result = new BatchOperationResultDto
        {
            Errors = new List<BatchOperationErrorDto>()
        };

        // 先按 ID 批量加载所有死信消息，逐条应用 Discard 状态变更，最后一次 SaveEntitiesAsync 提交事务
        var messagesToUpdate = new List<DeadLetterMessage>();
        foreach (var messageId in messageIds)
        {
            var entity = await _repository.GetByIdAsync(messageId, ct);
            if (entity is null)
            {
                result.FailureCount++;
                result.Errors.Add(new BatchOperationErrorDto
                {
                    MessageId = messageId,
                    Error = $"死信消息 {messageId} 不存在"
                });
                continue;
            }

            try
            {
                entity.Discard(operatorId, reason);
                messagesToUpdate.Add(entity);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add(new BatchOperationErrorDto
                {
                    MessageId = messageId,
                    Error = ex.Message
                });
            }
        }

        // 单次事务提交所有可丢弃的消息，避免逐条 SaveEntitiesAsync 造成中途失败状态不一致
        foreach (var entity in messagesToUpdate)
        {
            await _repository.UpdateAsync(entity, ct);
        }

        if (messagesToUpdate.Count > 0)
        {
            await _unitOfWork.SaveEntitiesAsync(ct);
        }

        result.SuccessCount = messagesToUpdate.Count;
        _logger.LogInformation(
            "批量丢弃完成 OperatorId={OperatorId} Success={Success} Failure={Failure}",
            operatorId, result.SuccessCount, result.FailureCount);

        return result;
    }

    private async Task<DeadLetterMessage> RequireMessageAsync(Guid messageId, CancellationToken ct)
        => await _repository.GetByIdAsync(messageId, ct)
           ?? throw new InvalidOperationException($"死信消息 {messageId} 不存在");

    private static DeadLetterMessageDto ToDto(DeadLetterMessage entity)
        => new()
        {
            MessageId = entity.MessageId,
            OriginalMessageId = entity.OriginalMessageId,
            SourceContext = entity.SourceContext,
            OriginalTopic = entity.OriginalTopic,
            Payload = entity.Payload,
            Headers = entity.Headers,
            ErrorReason = entity.ErrorReason,
            Status = entity.Status,
            OperatorId = entity.OperatorId,
            DiscardReason = entity.DiscardReason,
            OccurredAt = entity.OccurredAt,
            ProcessedAt = entity.ProcessedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}