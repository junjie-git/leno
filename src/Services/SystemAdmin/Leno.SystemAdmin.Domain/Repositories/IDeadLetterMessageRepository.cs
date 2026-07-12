using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Repositories;

/// <summary>
/// 死信消息仓储接口，定义在领域层，由基础设施层实现。
/// 支持按来源上下文、状态查询及按原始消息标识查找。
/// </summary>
public interface IDeadLetterMessageRepository : IRepository<DeadLetterMessage>
{
    /// <summary>
    /// 按原始消息标识查找死信消息。
    /// </summary>
    /// <param name="originalMessageId">原始消息标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<DeadLetterMessage?> GetByOriginalMessageIdAsync(string originalMessageId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询死信消息，支持来源上下文与状态过滤。
    /// </summary>
    /// <param name="sourceContext">来源上下文过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<DeadLetterMessage>> QueryAsync(string? sourceContext, DeadLetterStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计死信消息数量，支持来源上下文与状态过滤。
    /// </summary>
    /// <param name="sourceContext">来源上下文过滤，可空表示不限。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? sourceContext, DeadLetterStatus? status, CancellationToken ct = default);
}