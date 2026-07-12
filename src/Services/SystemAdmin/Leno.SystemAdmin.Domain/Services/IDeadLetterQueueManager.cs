using Leno.SystemAdmin.Domain.Aggregates;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 死信队列管理器领域服务接口，定义在领域层，由基础设施层实现。
/// 负责死信消息的查询与重投操作。
/// </summary>
public interface IDeadLetterQueueManager
{
    /// <summary>
    /// 分页获取死信消息，支持来源上下文过滤。
    /// </summary>
    /// <param name="sourceContext">来源上下文过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<DeadLetterMessage>> FetchAsync(string? sourceContext, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 统计死信消息数量，支持来源上下文过滤。
    /// </summary>
    /// <param name="sourceContext">来源上下文过滤，可空表示不限。</param>
    /// <param name="ct">取消令牌。</param>
    Task<int> CountAsync(string? sourceContext, CancellationToken ct = default);

    /// <summary>
    /// 重投指定死信消息，将其状态标记为 Retried。
    /// </summary>
    /// <param name="messageId">死信消息标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task RepublishAsync(Guid messageId, CancellationToken ct = default);
}