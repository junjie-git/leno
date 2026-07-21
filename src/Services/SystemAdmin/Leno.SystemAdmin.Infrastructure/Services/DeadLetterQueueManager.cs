using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Abstractions;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 死信队列管理器实现，基于数据库仓储管理死信消息。
/// FetchAsync 查询仓储，RepublishAsync 通过 <see cref="IEventBus"/> 真正重投原始集成事件并标记消息为 Retried。
/// </summary>
public sealed class DeadLetterQueueManager : IDeadLetterQueueManager
{
    private readonly IDeadLetterMessageRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeadLetterQueueManager> _logger;

    public DeadLetterQueueManager(
        IDeadLetterMessageRepository repository,
        IEventBus eventBus,
        IUnitOfWork unitOfWork,
        ILogger<DeadLetterQueueManager> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _eventBus = eventBus;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<DeadLetterMessage>> FetchAsync(string? sourceContext, int page, int pageSize, CancellationToken ct = default)
    {
        return await _repository.QueryAsync(sourceContext, status: null, page, pageSize, ct);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string? sourceContext, CancellationToken ct = default)
    {
        return await _repository.CountAsync(sourceContext, status: null, ct);
    }

    /// <inheritdoc />
    public async Task RepublishAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _repository.GetByIdAsync(messageId, ct);
        if (message is null)
        {
            throw new InvalidOperationException($"死信消息 {messageId} 不存在");
        }

        // 幂等：已重投则跳过重复发布，避免向 MQ 重复发送事件
        if (message.Status == DeadLetterStatus.Retried)
        {
            _logger.LogInformation("死信消息 {MessageId} 已重投，跳过重复重投", messageId);
            return;
        }

        if (message.Status == DeadLetterStatus.Discarded)
        {
            throw new InvalidOperationException($"死信消息 {messageId} 已丢弃，不可重投");
        }

        // 真正重投：反序列化原始集成事件并通过事件总线重新发布到 MQ
        await DeadLetterRepublishHelper.RepublishViaEventBusAsync(_eventBus, message, _logger, ct);

        // 重投成功后标记消息状态为 Retried 并持久化（经发件箱投递领域事件）
        message.Retry("system");
        await _repository.UpdateAsync(message, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("死信消息 {MessageId} 已通过事件总线重投", messageId);
    }
}