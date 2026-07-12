using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 索引重建应用服务接口。
/// </summary>
public interface IIndexRebuildAppService
{
    /// <summary>触发索引重建，创建任务并开始执行。</summary>
    Task<IndexRebuildTaskDto> TriggerAsync(TriggerIndexRebuildDto dto, string triggeredBy, CancellationToken ct = default);

    /// <summary>按标识获取索引重建任务详情。</summary>
    Task<IndexRebuildTaskDto?> GetByIdAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>重试失败的任务。</summary>
    Task<IndexRebuildTaskDto> RetryAsync(Guid taskId, string triggeredBy, CancellationToken ct = default);

    /// <summary>分页查询索引重建任务，支持目标上下文与状态过滤。</summary>
    Task<IndexRebuildTaskListResultDto> QueryAsync(string? targetContext, RebuildTaskStatus? status, int page, int pageSize, CancellationToken ct = default);
}