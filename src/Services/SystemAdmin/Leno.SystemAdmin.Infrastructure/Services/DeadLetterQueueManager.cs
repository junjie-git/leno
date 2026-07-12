using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// 死信队列管理器实现，基于数据库仓储管理死信消息。
/// FetchAsync 查询仓储，RepublishAsync 标记消息为 Retried。
/// </summary>
public sealed class DeadLetterQueueManager : IDeadLetterQueueManager
{
    private readonly IDeadLetterMessageRepository _repository;
    private readonly ILogger<DeadLetterQueueManager> _logger;

    public DeadLetterQueueManager(IDeadLetterMessageRepository repository, ILogger<DeadLetterQueueManager> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
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

        // Retry is idempotent - if already retried, it returns without error
        message.Retry("system"); // System-initiated retry via queue manager

        await _repository.UpdateAsync(message, ct);

        _logger.LogInformation("死信消息 {MessageId} 已重投", messageId);
    }
}