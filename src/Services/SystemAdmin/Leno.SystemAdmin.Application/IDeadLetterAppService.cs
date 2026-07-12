using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 死信消息管理应用服务接口。
/// </summary>
public interface IDeadLetterAppService
{
    /// <summary>分页查询死信消息，支持来源上下文与状态过滤。</summary>
    Task<DeadLetterListResultDto> QueryAsync(string? sourceContext, DeadLetterStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按标识获取死信消息详情。</summary>
    Task<DeadLetterMessageDto?> GetByIdAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>重投指定死信消息（幂等：已重投返回当前状态）。</summary>
    Task RetryAsync(Guid messageId, string operatorId, CancellationToken ct = default);

    /// <summary>丢弃指定死信消息。</summary>
    Task DiscardAsync(Guid messageId, string operatorId, string reason, CancellationToken ct = default);

    /// <summary>批量重投死信消息。</summary>
    Task<BatchOperationResultDto> BatchRetryAsync(List<Guid> messageIds, string operatorId, CancellationToken ct = default);

    /// <summary>批量丢弃死信消息。</summary>
    Task<BatchOperationResultDto> BatchDiscardAsync(List<Guid> messageIds, string operatorId, string reason, CancellationToken ct = default);
}